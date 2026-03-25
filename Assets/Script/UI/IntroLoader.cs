using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

/// <summary>
/// Dynamically creates a world-space Canvas intro panel, shows a role-appropriate
/// image, and manages the async scene transition with a minimum display time.
/// </summary>
public class IntroLoader : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Role Sprites")]
    [Tooltip("Sprite displayed when the local player's role is Hammer.")]
    [SerializeField] private Sprite hammerSprite;

    [Tooltip("Sprite displayed when the local player's role is Mole.")]
    [SerializeField] private Sprite moleSprite;

    [Header("Canvas Dimensions")]
    [Tooltip("Width of the intro canvas in pixels (match your source image width).")]
    [SerializeField] private float canvasWidth = 1536f;

    [Tooltip("Height of the intro canvas in pixels (match your source image height).")]
    [SerializeField] private float canvasHeight = 1024f;

    [Tooltip("Conversion factor from canvas pixels to world-space metres.\n" +
             "Default 0.002 → 1 px = 2 mm, so a 1536×1024 canvas becomes\n" +
             "3.072 m × 2.048 m in the scene — comfortably readable in VR at 2 m.\n\n" +
             "Increase this value to make the panel larger; decrease to shrink it.")]
    [SerializeField] private float pixelsToMetres = 0.002f;

    [Header("XR Camera")]
    [Tooltip("Transform of the XR camera (the 'Main Camera' child of XR Origin).\n" +
             "Auto-falls back to Camera.main if left empty.")]
    [SerializeField] private Transform xrCameraTransform;

    [Tooltip("Distance in metres between the camera and the canvas centre.")]
    [SerializeField] private float panelDistance = 2f;

    [Header("Scene Loading")]
    [Tooltip("Exact name of the scene to load (must be in Build Settings).")]
    [SerializeField] private string sceneName = "GameScene";

    [Tooltip("Minimum seconds the intro canvas stays visible, even if the scene\n" +
             "finishes loading sooner.")]
    [SerializeField] private float minimumDisplayTime = 3f;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private GameObject _spawnedCanvas;
    private bool       _isLoading;

    // -------------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Builds the intro canvas for <paramref name="role"/>, then begins the
    /// async scene load.  Safe to call from RolePanelController.HandleGameStart.
    /// Does nothing if a load is already in progress.
    /// </summary>
    /// <param name="role">The local player's assigned role (Hammer or Mole).</param>
    public void StartIntroAndLoad(RoleManager.Role role)
    {
        if (_isLoading)
        {
            Debug.LogWarning("[IntroLoader] Load already in progress — ignoring duplicate call.");
            return;
        }

        _isLoading = true;
        StartCoroutine(IntroLoadCoroutine(role));
    }

    // -------------------------------------------------------------------------
    //  Coroutine
    // -------------------------------------------------------------------------

    private IEnumerator IntroLoadCoroutine(RoleManager.Role role)
    {
        // ── Step 1: Build the intro canvas and display the role image ─────────
        _spawnedCanvas = BuildCanvas(role);

        if (string.IsNullOrEmpty(sceneName))
        {
            Debug.LogError("[IntroLoader] sceneName is empty — cannot load scene.");
            yield break;
        }

        AsyncOperation asyncLoad = SceneManager.LoadSceneAsync(sceneName);

        if (asyncLoad == null)
        {
            Debug.LogError($"[IntroLoader] LoadSceneAsync returned null for '{sceneName}'. " +
                           "Ensure the scene is added to Build Settings.");
            yield break;
        }

        asyncLoad.allowSceneActivation = false;

        // ── Step 3: Wait until both conditions are satisfied ──────────────────
        float elapsed = 0f;

        while (true)
        {
            elapsed += Time.deltaTime;

            // progress stalls at exactly 0.9 while allowSceneActivation is false.
            bool sceneReady  = asyncLoad.progress >= 0.9f;
            bool timerDone   = elapsed >= minimumDisplayTime;

            if (sceneReady && timerDone)
                break;

            yield return null;
        }

        // ── Step 4: Hand control to Unity to finalise the scene switch ────────
        asyncLoad.allowSceneActivation = true;

        // One extra frame for the scene to fully activate before we destroy the canvas.
        yield return null;

        // ── Step 5: Clean up ──────────────────────────────────────────────────
        if (_spawnedCanvas != null)
        {
            Destroy(_spawnedCanvas);
            _spawnedCanvas = null;
        }

        _isLoading = false;
    }

    // -------------------------------------------------------------------------
    //  Dynamic canvas construction
    // -------------------------------------------------------------------------

    /// <summary>
    /// Creates a world-space Canvas + Image entirely at runtime in front of the
    /// XR camera.  No prefab is required.
    /// </summary>
    /// <param name="role">Used to pick the correct intro sprite.</param>
    /// <returns>The root canvas GameObject.</returns>
    private GameObject BuildCanvas(RoleManager.Role role)
    {
        // ── Resolve camera ─────────────────────────────────────────────────────
        Transform cam = xrCameraTransform;
        if (cam == null && Camera.main != null)
        {
            cam = Camera.main.transform;
        }
        else
        {
            Debug.LogError("[IntroLoader] No camera found (xrCameraTransform is null AND Camera.main is null). " +
                           "Canvas will be placed at world origin — it may not be visible!");
        }

        // ── Compute world position and rotation ───────────────────────────────
        // Flatten the camera forward onto the horizontal plane so the canvas is
        // always placed at eye level, regardless of where the player is looking
        // (e.g. down at the Ready button).  This also avoids gimbal-lock issues
        // with LookRotation when cam.forward has a large Y component.
        Vector3 flatForward = cam != null
            ? Vector3.ProjectOnPlane(cam.forward, Vector3.up)
            : Vector3.forward;

        if (flatForward.sqrMagnitude < 0.001f)
            flatForward = Vector3.forward;   // safety: camera pointing straight up/down
        else
            flatForward.Normalize();

        // Place canvas panelDistance metres ahead at camera eye height.
        Vector3 spawnPos = cam != null
            ? cam.position + flatForward * panelDistance
            : flatForward * panelDistance;

        // Canvas +Z must point AWAY from the camera so the viewable face (-Z side)
        // faces the player.  Pointing +Z toward the camera (the previous approach)
        // caused the content to appear mirrored because the camera was seeing the
        // back face.  Using the flat forward direction also guarantees a perfectly
        // vertical canvas regardless of head pitch.
        Quaternion spawnRot = Quaternion.LookRotation(flatForward, Vector3.up);

        // ── Root canvas GameObject ────────────────────────────────────────────
        var canvasGO = new GameObject("IntroCanvas_Runtime");
        canvasGO.transform.SetPositionAndRotation(spawnPos, spawnRot);

        // Scale pixels → metres.  At 0.001 the 1536×1024 canvas is
        // 1.536 m × 1.024 m — visible and readable at 2 m in VR.
        canvasGO.transform.localScale = Vector3.one * pixelsToMetres;

        // ── Canvas component (World Space render mode) ─────────────────────────
        var canvas = canvasGO.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.WorldSpace;

        // Size the RectTransform to match the source image resolution.
        var canvasRT = canvasGO.GetComponent<RectTransform>();
        canvasRT.sizeDelta = new Vector2(canvasWidth, canvasHeight);

        // CanvasScaler prevents Unity from applying automatic DPI/reference-
        // resolution scaling that would fight our manual pixelsToMetres scaling.
        var scaler = canvasGO.AddComponent<CanvasScaler>();
        scaler.dynamicPixelsPerUnit = 1f;

        // ── Image child ───────────────────────────────────────────────────────
        var imageGO = new GameObject("IntroImage");
        imageGO.transform.SetParent(canvasGO.transform, worldPositionStays: false);

        var img = imageGO.AddComponent<Image>();

        Sprite selected = role == RoleManager.Role.Hammer ? hammerSprite : moleSprite;
        if (selected != null)
        {
            img.sprite = selected;
            img.color  = Color.white;   // ensure alpha=1 if the prefab/default tinted it
        }
        else
        {
            Debug.LogWarning($"[IntroLoader] No sprite assigned for role '{role}' — canvas will be blank.");
        }

        // Stretch the image to fill the full canvas area.
        var imgRT = imageGO.GetComponent<RectTransform>();
        imgRT.anchorMin = Vector2.zero;
        imgRT.anchorMax = Vector2.one;
        imgRT.offsetMin = Vector2.zero;
        imgRT.offsetMax = Vector2.zero;

        return canvasGO;
    }

    // -------------------------------------------------------------------------
    //  Unity cleanup
    // -------------------------------------------------------------------------

    private void OnDestroy()
    {
        // Prevent canvas leaking if this component is destroyed mid-load.
        if (_spawnedCanvas != null)
            Destroy(_spawnedCanvas);
    }
}
