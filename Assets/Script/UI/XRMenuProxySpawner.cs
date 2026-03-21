using System.Collections.Generic;
using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

// =============================================================================
//  XRMenuProxySpawner.cs
//
//  Spawns three 3D XR-interactable proxy objects aligned with the positions
//  of the three end-game canvas buttons (Restart, Restart Switched, Exit).
//
//  WHY PROXIES?
//  ------------
//  A world-space Unity UI canvas rendered in a VR headset is often impossible
//  to interact with via XR controller ray or poke interactors because the
//  GraphicRaycaster and EventSystem pipeline may not be wired for XRI.
//  Instead, we place invisible (or styled) 3D objects with BoxColliders and
//  XRSimpleInteractable components directly over each button and route their
//  selectEntered events to the same EndGameController methods.
//
//  SETUP
//  -----
//  1. Attach this component to any persistent GameObject in the game scene.
//  2. In the Canvas hierarchy, place an empty child Transform at the centre of
//     each button and assign it to restartAnchor / restartSwitchedAnchor / exitAnchor.
//  3. Create a proxy prefab — a plain cube or invisible quad works well.
//     The prefab does not need a Collider or XRSimpleInteractable; they will
//     be added automatically at spawn time with a runtime warning if absent.
//  4. Assign all Inspector references.
//  5. Call ShowMenuAndSpawnProxies() when you want to open the end-game panel.
//     Call HideMenuAndClearProxies() when leaving the end-game state.
// =============================================================================

/// <summary>
/// Manages the lifecycle of three world-space proxy interactables that mirror
/// the end-game canvas buttons so they are usable with XR controllers.
/// </summary>
public class XRMenuProxySpawner : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Canvas References")]
    [Tooltip("The WorldSpaceCanvasSpawner used to show / hide the end-game canvas.\n" +
             "If assigned, ShowMenuAndSpawnProxies / HideMenuAndClearProxies will\n" +
             "also control canvas visibility.")]
    [SerializeField] private WorldSpaceCanvasSpawner canvasSpawner;

    [Tooltip("The target Canvas. Auto-populated from canvasSpawner if left blank.")]
    [SerializeField] private Canvas targetCanvas;

    [Header("Button Anchors")]
    [Tooltip("Empty Transform placed at the centre of the Restart (same role) button.")]
    [SerializeField] private Transform restartAnchor;

    [Tooltip("Empty Transform placed at the centre of the Restart Switched (swap roles) button.")]
    [SerializeField] private Transform restartSwitchedAnchor;

    [Tooltip("Empty Transform placed at the centre of the Exit (return to lobby) button.")]
    [SerializeField] private Transform exitAnchor;

    [Header("Proxy Prefab")]
    [Tooltip("Prefab instantiated for each proxy button.\n" +
             "A BoxCollider and XRSimpleInteractable are added automatically if absent.\n" +
             "Use a transparent / invisible material to keep the original button art visible.")]
    [SerializeField] private GameObject proxyButtonPrefab;

    [Tooltip("Desired world-space size of each proxy in metres (X=width, Y=height, Z=depth).\n" +
             "When parentToCanvas is true the scale is compensated for the canvas lossyScale\n" +
             "so this value always represents the actual world size seen in VR.\n\n" +
             "Default (0.18, 0.08, 0.02) fits a typical 180×80 pixel button on a 1000 ppu canvas.")]
    [SerializeField] private Vector3 proxyScale = new Vector3(0.18f, 0.08f, 0.02f);

    [Tooltip("Additional offset in local proxy space applied after the proxy is\n" +
             "positioned at the anchor. Use this for fine-tuning alignment.")]
    [SerializeField] private Vector3 proxyLocalOffset = Vector3.zero;

    [Tooltip("When true each proxy is parented to the canvas Transform so it\n" +
             "follows the canvas if ShowInFrontOfUser repositions it.")]
    [SerializeField] private bool parentToCanvas = true;

    [Header("Action Target")]
    [Tooltip("The EndGameController whose methods are called by the proxy buttons.")]
    [SerializeField] private EndGameController endGameController;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private readonly List<GameObject> _spawnedProxies = new();

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Auto-resolve canvas reference from the spawner.
        if (targetCanvas == null && canvasSpawner != null)
            targetCanvas = canvasSpawner.targetCanvas;
    }

    // -------------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Shows the canvas via <see cref="WorldSpaceCanvasSpawner"/>, clears any
    /// existing proxies, then spawns fresh ones aligned with the anchors.
    /// Call this whenever the end-game menu should open.
    /// </summary>
    public void ShowMenuAndSpawnProxies()
    {
        if (canvasSpawner != null)
            canvasSpawner.ShowCanvas();
        else if (targetCanvas != null)
            targetCanvas.gameObject.SetActive(true);

        ClearProxies();
        SpawnProxies();
    }

    /// <summary>
    /// Destroys all proxy buttons and hides the canvas.
    /// Call this when leaving the end-game state (e.g. before scene reload).
    /// </summary>
    public void HideMenuAndClearProxies()
    {
        ClearProxies();

        if (canvasSpawner != null)
            canvasSpawner.HideCanvas();
        else if (targetCanvas != null)
            targetCanvas.gameObject.SetActive(false);
    }

    /// <summary>
    /// Instantiates one proxy at each of the three anchor positions.
    /// Validates all required references before spawning; logs warnings for
    /// any that are missing.
    /// </summary>
    public void SpawnProxies()
    {
        // Re-resolve canvas in case it was assigned after Awake.
        if (targetCanvas == null && canvasSpawner != null)
            targetCanvas = canvasSpawner.targetCanvas;

        if (!ValidateReferences()) return;

        SpawnProxy(restartAnchor,         XRMenuProxyButton.ProxyButtonType.Restart,         "RestartProxy");
        SpawnProxy(restartSwitchedAnchor,  XRMenuProxyButton.ProxyButtonType.RestartSwitched,  "RestartSwitchedProxy");
        SpawnProxy(exitAnchor,             XRMenuProxyButton.ProxyButtonType.Exit,             "ExitProxy");
    }

    /// <summary>
    /// Destroys all previously spawned proxy buttons.
    /// </summary>
    public void ClearProxies()
    {
        foreach (var proxy in _spawnedProxies)
        {
            if (proxy != null)
                Destroy(proxy);
        }
        _spawnedProxies.Clear();
    }

    /// <summary>
    /// Destroys existing proxies and immediately re-spawns them at the current
    /// anchor positions. Useful after the canvas has been repositioned via
    /// <see cref="WorldSpaceCanvasSpawner.ShowInFrontOfUser"/>.
    /// </summary>
    public void RespawnProxies()
    {
        ClearProxies();
        SpawnProxies();
    }

    // -------------------------------------------------------------------------
    //  Internal — spawning logic
    // -------------------------------------------------------------------------

    private void SpawnProxy(Transform anchor, XRMenuProxyButton.ProxyButtonType type, string proxyName)
    {
        if (anchor == null)
        {
            Debug.LogWarning($"[XRMenuProxySpawner] Anchor for '{type}' is not assigned — proxy not spawned.");
            return;
        }

        // ── 1. Instantiate at anchor world position / identity rotation ───────
        GameObject proxy = Instantiate(proxyButtonPrefab, anchor.position, Quaternion.identity);
        proxy.name = proxyName;

        // ── 2. Orient to match the canvas face so the proxy "covers" the button ─
        proxy.transform.rotation = targetCanvas != null
            ? targetCanvas.transform.rotation
            : anchor.rotation;

        // ── 3. Parent to canvas (world pose preserved) ────────────────────────
        Transform parentTransform = null;
        if (parentToCanvas && targetCanvas != null)
        {
            proxy.transform.SetParent(targetCanvas.transform, worldPositionStays: true);
            parentTransform = targetCanvas.transform;
        }

        // ── 4. Apply local offset (fine-tuning in local proxy space) ──────────
        if (proxyLocalOffset != Vector3.zero)
            proxy.transform.localPosition += proxyLocalOffset;

        // ── 5. Scale — compensate for parent scale so proxyScale = world size ─
        ApplyCompensatedScale(proxy.transform, parentTransform);

        // ── 6. Ensure a Collider exists (auto-add with warning if missing) ─────
        EnsureCollider(proxy);

        // ── 7. Ensure XRSimpleInteractable exists (auto-add with warning) ──────
        XRSimpleInteractable interactable = EnsureInteractable(proxy);

        // ── 8. Configure XRMenuProxyButton ────────────────────────────────────
        XRMenuProxyButton btn = proxy.GetComponent<XRMenuProxyButton>();
        if (btn == null)
            btn = proxy.AddComponent<XRMenuProxyButton>();

        btn.buttonType  = type;
        btn.controller  = endGameController;
        btn.Init(interactable);   // Passes the resolved interactable and re-subscribes.

        _spawnedProxies.Add(proxy);
        Debug.Log($"[XRMenuProxySpawner] Spawned '{proxyName}' at {proxy.transform.position}.");
    }

    /// <summary>
    /// Sets the proxy's <c>localScale</c> so its actual world size equals
    /// <see cref="proxyScale"/>, compensating for the parent's lossyScale.
    /// </summary>
    private void ApplyCompensatedScale(Transform proxyTransform, Transform parent)
    {
        if (parent == null)
        {
            proxyTransform.localScale = proxyScale;
            return;
        }

        // Divide the desired world size by the parent's lossy scale component-wise.
        Vector3 ps = parent.lossyScale;
        proxyTransform.localScale = new Vector3(
            ps.x > 1e-6f ? proxyScale.x / ps.x : proxyScale.x,
            ps.y > 1e-6f ? proxyScale.y / ps.y : proxyScale.y,
            ps.z > 1e-6f ? proxyScale.z / ps.z : proxyScale.z
        );
    }

    /// <summary>
    /// Ensures the proxy has a <see cref="Collider"/>. If the prefab lacks one,
    /// a <see cref="BoxCollider"/> of size <c>(1,1,1)</c> is added (which maps
    /// to <see cref="proxyScale"/> in world space after scale compensation).
    /// The collider is made slightly generous on X/Y to ease controller aiming.
    /// </summary>
    private void EnsureCollider(GameObject proxy)
    {
        if (proxy.GetComponent<Collider>() != null) return;

        Debug.LogWarning($"[XRMenuProxySpawner] Prefab '{proxyButtonPrefab.name}' has no Collider — " +
                         "adding BoxCollider automatically.");

        var box = proxy.AddComponent<BoxCollider>();

        // (1,1,1) in local space == proxyScale in world space after ApplyCompensatedScale.
        // Slightly generous in X and Y to improve controller aiming in VR.
        box.size = new Vector3(1.1f, 1.2f, 1.0f);
    }

    /// <summary>
    /// Ensures the proxy has an <see cref="XRSimpleInteractable"/>.
    /// Adds one automatically (with a warning) if the prefab is missing it.
    /// </summary>
    private XRSimpleInteractable EnsureInteractable(GameObject proxy)
    {
        var interactable = proxy.GetComponent<XRSimpleInteractable>();
        if (interactable != null) return interactable;

        Debug.LogWarning($"[XRMenuProxySpawner] Prefab '{proxyButtonPrefab.name}' has no XRSimpleInteractable — " +
                         "adding one automatically. " +
                         "Ensure an XRInteractionManager exists in the scene.");

        return proxy.AddComponent<XRSimpleInteractable>();
    }

    /// <summary>
    /// Returns false if any blocking reference is missing (prefab or anchors).
    /// Logs a warning for each missing field.
    /// </summary>
    private bool ValidateReferences()
    {
        bool ok = true;

        if (proxyButtonPrefab == null)
        {
            Debug.LogWarning("[XRMenuProxySpawner] proxyButtonPrefab is not assigned. Cannot spawn proxies.");
            return false;   // Cannot continue without a prefab.
        }

        if (restartAnchor        == null) { Debug.LogWarning("[XRMenuProxySpawner] restartAnchor is not assigned.");        ok = false; }
        if (restartSwitchedAnchor == null) { Debug.LogWarning("[XRMenuProxySpawner] restartSwitchedAnchor is not assigned."); ok = false; }
        if (exitAnchor            == null) { Debug.LogWarning("[XRMenuProxySpawner] exitAnchor is not assigned.");            ok = false; }

        if (endGameController == null)
            Debug.LogWarning("[XRMenuProxySpawner] endGameController is not assigned — " +
                             "proxy buttons will fire their UnityEvent but won't call any game methods.");

        return ok;
    }

    // -------------------------------------------------------------------------
    //  Editor gizmos — visualise anchor positions and proxy extents in the
    //  Scene view when this GameObject is selected.
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    private void OnDrawGizmosSelected()
    {
        DrawAnchorGizmo(restartAnchor,        new Color(0.2f, 1f,   0.2f), "Restart");
        DrawAnchorGizmo(restartSwitchedAnchor, new Color(1f,   0.8f, 0.1f), "RestartSwitched");
        DrawAnchorGizmo(exitAnchor,            new Color(1f,   0.3f, 0.3f), "Exit");
    }

    private void DrawAnchorGizmo(Transform anchor, Color color, string label)
    {
        if (anchor == null) return;

        Gizmos.color = color;
        Gizmos.DrawWireCube(anchor.position, proxyScale);

        // Draw a sphere at the centre for easy spotting.
        Gizmos.DrawSphere(anchor.position, 0.005f);

        // Label above the wire cube.
        UnityEditor.Handles.Label(
            anchor.position + Vector3.up * (proxyScale.y * 0.5f + 0.015f),
            label
        );
    }
#endif
}
