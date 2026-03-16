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
    [SerializeField] private float flashDuration = 0.5f; // seconds the red holds before fading
    [SerializeField] private float fadeSpeed     = 2f;   // alpha units drained per second

    [Header("Debug")]
    [Tooltip("Force-enable the effect regardless of local role. " +
             "Use in the Editor when GameData.LocalRole is not set via the lobby.")]
    [SerializeField] private bool debugForceEnable = false;

    private Image               damageOverlay;
    private Coroutine           currentFlash;
    private ScoreManager        scoreManager;
    private MoleVisibilityTracker moleTracker;
    private int                 lastHammerScore;

    private void Start()
    {
        // Only the Mole player sees the hit flash.
        // debugForceEnable bypasses the check for Editor testing.
        if (!debugForceEnable && GameData.LocalRole != RoleManager.Role.Mole)
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
    }

    private void OnDestroy()
    {
        if (moleTracker  != null) moleTracker.OnMoleStateUpdate  -= HandleMoleState;
        if (scoreManager != null) scoreManager.OnScoreUpdated    -= HandleScoreUpdated;
    }

    // -------------------------------------------------------------------------
    //  Runtime Canvas / Image creation
    // -------------------------------------------------------------------------

    private void CreateOverlay()
    {
        var canvasGO = new GameObject("DamageFlashCanvas");
        DontDestroyOnLoad(canvasGO);

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode   = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999;

        canvasGO.AddComponent<CanvasScaler>();
        canvasGO.AddComponent<GraphicRaycaster>();

        var imageGO = new GameObject("RedOverlay");
        imageGO.transform.SetParent(canvasGO.transform, false);

        damageOverlay = imageGO.AddComponent<Image>();
        damageOverlay.color = new Color(0.8f, 0f, 0f, 0f);

        var rt = damageOverlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        damageOverlay.raycastTarget = false;
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
        if (msg.isVisible)
            FlashDamage();   // DEBUG: flash as soon as mole pops up
        else
            CancelFlash();   // mole hid — clear instantly
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
        if (currentFlash != null)
            StopCoroutine(currentFlash);

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
    }

    // -------------------------------------------------------------------------
    //  Coroutine
    // -------------------------------------------------------------------------

    private IEnumerator FlashCoroutine()
    {
        damageOverlay.color = new Color(0.8f, 0f, 0f, 0.7f);

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
    }
}
