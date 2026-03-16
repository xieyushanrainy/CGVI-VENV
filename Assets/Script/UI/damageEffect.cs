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

    private Image        damageOverlay;
    private Coroutine    currentFlash;
    private ScoreManager scoreManager;
    private int          lastHammerScore;

    private void Start()
    {
        // Only the Mole player sees the hit flash.
        if (GameData.LocalRole != RoleManager.Role.Mole)
        {
            enabled = false;
            return;
        }

        CreateOverlay();

        scoreManager = FindFirstObjectByType<ScoreManager>();
        if (scoreManager != null)
            scoreManager.OnScoreUpdated += HandleScoreUpdated;
        else
            Debug.LogWarning("[damageEffect] ScoreManager not found — effect disabled.", this);
    }

    private void OnDestroy()
    {
        if (scoreManager != null)
            scoreManager.OnScoreUpdated -= HandleScoreUpdated;
    }

    // -------------------------------------------------------------------------
    //  Runtime Canvas / Image creation
    // -------------------------------------------------------------------------

    private void CreateOverlay()
    {
        // Canvas
        var canvasGO = new GameObject("DamageFlashCanvas");
        DontDestroyOnLoad(canvasGO); // survives scene reloads if needed

        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode  = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 999; // render on top of everything

        canvasGO.AddComponent<CanvasScaler>();   // keeps the overlay resolution-independent
        canvasGO.AddComponent<GraphicRaycaster>(); // required for a valid Canvas hierarchy

        // Full-screen Image
        var imageGO = new GameObject("RedOverlay");
        imageGO.transform.SetParent(canvasGO.transform, false);

        damageOverlay = imageGO.AddComponent<Image>();
        damageOverlay.color = new Color(0.8f, 0f, 0f, 0f); // start fully transparent

        // Stretch to fill the entire Canvas
        var rt = damageOverlay.rectTransform;
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = Vector2.zero;
        rt.offsetMax = Vector2.zero;

        // Prevent the overlay from blocking UI interactions
        damageOverlay.raycastTarget = false;
    }

    // -------------------------------------------------------------------------
    //  Score event handler
    // -------------------------------------------------------------------------

    private void HandleScoreUpdated(ScoreUpdateMessage msg)
    {
        // Mole went back into hiding → immediately clear any active flash.
        if (!msg.moleVisible)
        {
            CancelFlash();
        }
        // Hammer score increased → a validated hit just landed.
        else if (msg.hammerScore > lastHammerScore)
        {
            FlashDamage();
        }

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

    /// <summary>Immediately clears the red overlay (e.g. when the mole hides).</summary>
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
