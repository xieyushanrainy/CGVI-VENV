using UnityEngine;

/// <summary>
/// Tracks whether the hammer is currently inside this hole's trigger volume
/// and highlights the hole material accordingly.
///
/// Attach to each Hole_X GameObject.
/// </summary>
public class HoleTriggerZone : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Visual Feedback")]
    [Tooltip("Renderer of the cylinder that represents the hole. " +
             "Auto-assigned by Reset().")]
    [SerializeField] private Renderer holeRenderer;

    [Tooltip("Material shown when no hammer is inside the hole.")]
    [SerializeField] private Material normalMaterial;

    [Tooltip("Material shown while the hammer is inside the hole.")]
    [SerializeField] private Material highlightMaterial;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private HoleIdMarker holeIdMarker;

    // -------------------------------------------------------------------------
    //  Unity – lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Search this GameObject first, then walk up the hierarchy.
        // This lets the script work whether it is on the hole root or on
        // a child that owns the trigger collider.
        holeIdMarker = GetComponent<HoleIdMarker>();
        if (holeIdMarker == null)
            holeIdMarker = GetComponentInParent<HoleIdMarker>();

        if (holeIdMarker == null)
            Debug.LogWarning($"[HoleTriggerZone] No HoleIdMarker found on '{name}' " +
                             "or any of its parents. Attach HoleIdMarker to the hole root.", this);
    }

    private void Start()
    {
        if (holeRenderer == null)
            Debug.LogWarning($"[HoleTriggerZone] holeRenderer is not assigned on '{name}'. " +
                             "Drag the Cylinder's MeshRenderer into the Inspector slot.", this);
        if (normalMaterial == null)
            Debug.LogWarning($"[HoleTriggerZone] normalMaterial is not assigned on '{name}'.", this);
        if (highlightMaterial == null)
            Debug.LogWarning($"[HoleTriggerZone] highlightMaterial is not assigned on '{name}'.", this);
    }

    // -------------------------------------------------------------------------
    //  Public read-only state
    // -------------------------------------------------------------------------

    /// <summary>The logical index of this hole (0–4).</summary>
    public int HoleId => holeIdMarker != null ? holeIdMarker.HoleId : -1;

    /// <summary>True while the hammer collider overlaps this trigger zone.</summary>
    public bool IsHammerInside { get; private set; }

    // -------------------------------------------------------------------------
    //  Unity – trigger callbacks
    // -------------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        if (!IsHammer(other))
            return;

        IsHammerInside = true;

        ApplyMaterial(highlightMaterial);
    }

    private void OnTriggerExit(Collider other)
    {
        if (!IsHammer(other))
            return;

        IsHammerInside = false;

        ApplyMaterial(normalMaterial);
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    // Returns true if the collider belongs to the hammer — checks the collider's
    // own GameObject first, then its attachedRigidbody's GameObject. This handles
    // the common case where the tag is on the parent PlayerHammer but the collider
    // is on a child (e.g. HammerHead).
    private static bool IsHammer(Collider col)
    {
        if (col.CompareTag("Hammer")) return true;
        return col.attachedRigidbody != null && col.attachedRigidbody.CompareTag("Hammer");
    }

    private void ApplyMaterial(Material mat)
    {
        if (holeRenderer == null)
        {
            Debug.LogWarning($"[HoleTriggerZone] holeRenderer is not assigned on '{name}'. " +
                             "Assign it in the Inspector or via Reset().", this);
            return;
        }

        if (mat == null)
            return;

        holeRenderer.material = mat;
    }

    // -------------------------------------------------------------------------
    //  Editor – auto-assign components via Reset()
    // -------------------------------------------------------------------------

#if UNITY_EDITOR
    /// <summary>
    /// Called by Unity when the component is first added or the user clicks
    /// Reset in the Inspector context menu.  Auto-discovers the holeRenderer.
    /// </summary>
    private void Reset()
    {
        if (holeRenderer == null)
            holeRenderer = GetComponentInChildren<Renderer>();
    }
#endif
}
