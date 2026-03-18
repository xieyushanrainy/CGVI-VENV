using UnityEngine;
using UnityEngine.UI;
using System.Collections;

/// <summary>
/// Spawns its own full-screen red overlay Canvas at runtime — no manual
/// Canvas or Image setup required in the scene.
///
/// Attach this component anywhere in the scene (e.g. GameManager).
/// It self-initialises on Start().
/// </summary>
public class damageEffect : MonoBehaviour
{
    [SerializeField] private float flashDuration  = 0.5f; // seconds the red holds before fading
    [SerializeField] private float fadeSpeed      = 2f;   // alpha units drained per second
    [SerializeField] [Range(0f, 1f)] private float maxAlpha = 0.7f; // peak opacity of the red overlay

    [Header("Debug")]
    [Tooltip("Tick this in the Inspector (or set from code) to force the damage flash after 2 frames, " +
             "bypassing all normal guards (overlay-null check, active-hierarchy check, role check).\n" +
             "The flag resets itself automatically after triggering.")]
    [SerializeField] private bool forceFlash = false;

    private Image               damageOverlay;
    private GameObject          overlayCanvasGO;
    private Coroutine           currentFlash;
    private ScoreManager        scoreManager;
    private MoleVisibilityTracker moleTracker;
    private MoleCameraOffsetRaiseController moleCameraController;
    private int                 lastHammerScore;
    private bool                forceFlashPending;

    private void Start()
    {
        // Only the Mole player sees the hit flash.
        if (GameData.LocalRole != RoleManager.Role.Mole)
        {
            enabled = false;
            return;
        }

        CreateOverlay();

        // ── Visibility events — fires immediately on both clients ──────────────
        // Subscribing here (rather than OnScoreUpdated) is critical because
        // ScoreManager never calls BroadcastScore() on the hidden→visible
        // transition, so OnScoreUpdated would arrive with up to a 2s delay.
        moleTracker = FindFirstObjectByType<MoleVisibilityTracker>();
        if (moleTracker != null)
            moleTracker.OnMoleStateUpdate += HandleMoleState;
        else
            Debug.LogWarning("[damageEffect] MoleVisibilityTracker not found.", this);

        // ── Hit events — fires when the authority validates a hit ──────────────
        scoreManager = FindFirstObjectByType<ScoreManager>();
        if (scoreManager != null)
            scoreManager.OnScoreUpdated += HandleScoreUpdated;
        else
            Debug.LogWarning("[damageEffect] ScoreManager not found — hit flash disabled.", this);

        moleCameraController = FindFirstObjectByType<MoleCameraOffsetRaiseController>();
    }

    private void Update()
    {
        if (forceFlash && !forceFlashPending)
        {
            forceFlash        = false;
            forceFlashPending = true;
            StartCoroutine(ForceFlashCoroutine());
        }
    }

    private void OnDestroy()
    {
        if (moleTracker  != null) moleTracker.OnMoleStateUpdate  -= HandleMoleState;
        if (scoreManager != null) scoreManager.OnScoreUpdated    -= HandleScoreUpdated;

        if (overlayCanvasGO != null)
            Destroy(overlayCanvasGO);
    }

    // -------------------------------------------------------------------------
    //  Runtime Canvas / Image creation
    // -------------------------------------------------------------------------

    private void CreateOverlay()
    {
        // ── Find the XR camera ────────────────────────────────────────────────
        // We need to parent the canvas to the camera so it always sits in front
        // of the player's view regardless of head rotation.
        Camera cam = Camera.main;
        if (cam == null)
        {
            Debug.LogWarning("[damageEffect] No Camera.main found — overlay will not appear.", this);
            return;
        }

        // ── Canvas (Screen Space - Camera) ────────────────────────────────────
        // ScreenSpaceOverlay is not visible in HMD. For XR, bind a Canvas to
        // the XR camera so it renders into the headset eye buffers.
        overlayCanvasGO = new GameObject("DamageFlashCanvas");
        var canvasGO = overlayCanvasGO;
        DontDestroyOnLoad(canvasGO);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceCamera;
        canvas.worldCamera  = cam;
        // Use a fixed plane distance large enough to survive nearClipPlane
        // rounding and XR rig movement, but small enough to always be in front
        // of scene geometry.  nearClipPlane + 0.01 is too tight for some XR
        // cameras whose nearClipPlane is already 0.1 — bump to a safer value.
        canvas.planeDistance = 0.31f;
        canvas.sortingOrder  = 100; // render on top of all other canvases
        canvas.pixelPerfect  = false;

        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        // ── Image ─────────────────────────────────────────────────────────────
        var imageGO = new GameObject("RedOverlay");
        imageGO.transform.SetParent(canvasGO.transform, false);

        damageOverlay = imageGO.AddComponent<Image>();

        // Image with sprite = null does not render in many Unity versions.
        // Create a minimal 1×1 white sprite so the color field is applied correctly.
        var tex = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        tex.SetPixel(0, 0, Color.white);
        tex.Apply();
        damageOverlay.sprite = Sprite.Create(tex, new Rect(0, 0, 1, 1), Vector2.one * 0.5f);

        damageOverlay.color = new Color(0.8f, 0f, 0f, 0f); // start fully transparent
        damageOverlay.raycastTarget = false;

        // Stretch the image to fill the canvas rect.
        var rt = damageOverlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Ensure no GraphicRaycaster is present — it would intercept UI pointer events.
        var raycaster = canvasGO.GetComponent<GraphicRaycaster>();
        if (raycaster != null) Destroy(raycaster);

        // Disabled by default; enabled only while the flash is playing.
        canvasGO.SetActive(false);

        Debug.Log($"[damageEffect] Overlay created on camera '{cam.name}' (ScreenSpaceCamera)", this);
    }

    // -------------------------------------------------------------------------
    //  Event handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fires on both clients via MoleVisibilityTracker — immediate and reliable
    /// for both the show and hide transitions.
    /// </summary>
    private void HandleMoleState(MoleStateMessage msg)
    {
        // Don't cancel the flash if the mole became hidden due to a hit reaction —
        // the sink is caused by the same hit that triggered the flash, so cancelling
        // it here would wipe the effect immediately after it starts.
        if (!msg.isVisible && (moleCameraController == null || !moleCameraController.IsRecovering))
            CancelFlash();   // mole voluntarily hid — clear instantly
    }

    /// <summary>
    /// Listen for confirmed hits from the authority ScoreManager.
    /// Kept separate from visibility so it can trigger an additional flash
    /// (or a different effect) specifically on a validated hit.
    /// </summary>
    private void HandleScoreUpdated(ScoreUpdateMessage msg)
    {
        if (msg.hammerScore > lastHammerScore)
            FlashDamage();

        lastHammerScore = msg.hammerScore;
    }

    // -------------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------------

    public void FlashDamage()
    {
        if (damageOverlay == null) return;

        if (!gameObject.activeInHierarchy) return;

        if (currentFlash != null)
            StopCoroutine(currentFlash);

        overlayCanvasGO.SetActive(true);
        currentFlash = StartCoroutine(FlashCoroutine());
    }

    public void CancelFlash()
    {
        if (currentFlash != null)
        {
            StopCoroutine(currentFlash);
            currentFlash = null;
        }

        if (damageOverlay != null)
            damageOverlay.color = new Color(0.8f, 0f, 0f, 0f);

        if (overlayCanvasGO != null)
            overlayCanvasGO.SetActive(false);
    }

    // -------------------------------------------------------------------------
    //  Coroutine
    // -------------------------------------------------------------------------

    // Waits 2 frames so CreateOverlay() (called from Start) has time to finish,
    // then fires the flash directly — bypassing all guards.
    private IEnumerator ForceFlashCoroutine()
    {
        yield return null;
        yield return null;

        forceFlashPending = false;

        if (damageOverlay == null)
        {
            Debug.LogWarning("[damageEffect] forceFlash: overlay is still null after 2 frames — flash skipped.", this);
            yield break;
        }

        if (currentFlash != null)
            StopCoroutine(currentFlash);

        overlayCanvasGO.SetActive(true);
        currentFlash = StartCoroutine(FlashCoroutine());
    }

    private IEnumerator FlashCoroutine()
    {
        damageOverlay.color = new Color(0.8f, 0f, 0f, maxAlpha);

        float timer = 0f;
        while (timer < flashDuration)
        {
            timer += Time.deltaTime;
            yield return null;
        }

        while (damageOverlay.color.a > 0f)
        {
            Color c = damageOverlay.color;
            c.a -= Time.deltaTime * fadeSpeed;
            damageOverlay.color = c;
            yield return null;
        }

        damageOverlay.color = new Color(0.8f, 0f, 0f, 0f);
        currentFlash = null;

        if (overlayCanvasGO != null)
            overlayCanvasGO.SetActive(false);
    }

}
