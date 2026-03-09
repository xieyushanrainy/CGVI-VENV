using UnityEngine;

// =============================================================================
//  HoleManager.cs
//
//  Central registry of all 5 hole transforms and their CapsuleCollider
//  hit zones.  Lets ScoreManager and other systems look up hole world
//  positions / colliders by id without keeping their own references.
//
//  SETUP
//  -----
//  1. Attach this component to GameManager/HoleManager.
//  2. In the Inspector, ensure the "Holes" array has exactly 5 entries.
//  3. Drag Hole_0 → slot [0], Hole_1 → slot [1], ... Hole_4 → slot [4].
//  4. Each Hole_X (or one of its children) must have a CapsuleCollider
//     component.  HoleManager finds it automatically at Awake — no extra
//     array to fill in.
// =============================================================================

/// <summary>
/// Stores references to all 5 hole transforms and auto-resolves their
/// <see cref="CapsuleCollider"/> hit zones at Awake.
/// Attach to GameManager/HoleManager.
/// </summary>
public class HoleManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Hole Transforms  (index 0 → 4)")]
    [Tooltip("Drag Hole_0, Hole_1, Hole_2, Hole_3, Hole_4 into slots [0]–[4].")]
    [SerializeField] private Transform[] holes = new Transform[5];

    // -------------------------------------------------------------------------
    //  Runtime state — resolved at Awake
    // -------------------------------------------------------------------------

    // One CapsuleCollider per hole, discovered from the hole transform or its
    // children.  Not shown in the Inspector because it is auto-populated.
    private CapsuleCollider[] capsuleColliders;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        capsuleColliders = new CapsuleCollider[holes.Length];

        for (int i = 0; i < holes.Length; i++)
        {
            if (holes[i] == null)
            {
                Debug.LogWarning($"[HoleManager] holes[{i}] is not assigned — " +
                                  "drag Hole_{i} into the Inspector slot.", this);
                continue;
            }

            // Search the hole root first, then its descendants.
            var col = holes[i].GetComponent<CapsuleCollider>()
                   ?? holes[i].GetComponentInChildren<CapsuleCollider>();

            if (col == null)
                Debug.LogWarning($"[HoleManager] No CapsuleCollider found on " +
                                 $"'{holes[i].name}' or its children (slot {i}).", this);

            capsuleColliders[i] = col;
        }
    }

    // -------------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns the primary transform for the given hole id (0–4).
    /// Throws <see cref="System.ArgumentOutOfRangeException"/> if the id is
    /// invalid, or <see cref="System.InvalidOperationException"/> if the slot
    /// has not been wired in the Inspector.
    /// </summary>
    public Transform GetHoleTransform(int holeId)
    {
        AssertValidId(holeId);
        if (holes[holeId] == null)
            throw new System.InvalidOperationException(
                $"[HoleManager] holes[{holeId}] is not assigned in the Inspector.");
        return holes[holeId];
    }

    /// <summary>
    /// Safe version of <see cref="GetHoleTransform"/>.
    /// Returns <c>false</c> if the id is out of range or the slot is unassigned.
    /// </summary>
    public bool TryGetHoleTransform(int holeId, out Transform holeTransform)
    {
        holeTransform = null;
        if (holeId < 0 || holeId >= holes.Length) return false;
        holeTransform = holes[holeId];
        return holeTransform != null;
    }

    /// <summary>
    /// Returns the transform of the <see cref="CapsuleCollider"/> on the given
    /// hole (resolved at Awake).  Falls back to the hole root transform when
    /// no capsule was found.
    /// Returns <c>false</c> if the id is out of range or nothing is available.
    /// </summary>
    public bool TryGetHitZoneTransform(int holeId, out Transform hitZoneTransform)
    {
        hitZoneTransform = null;
        if (holeId < 0 || holeId >= holes.Length) return false;

        // Prefer the CapsuleCollider's own transform for the most accurate centre.
        if (capsuleColliders != null && holeId < capsuleColliders.Length
            && capsuleColliders[holeId] != null)
        {
            hitZoneTransform = capsuleColliders[holeId].transform;
            return true;
        }

        // Fall back to the hole root transform.
        hitZoneTransform = holes[holeId];
        return hitZoneTransform != null;
    }

    /// <summary>
    /// Returns the <see cref="CapsuleCollider"/> for the given hole id so that
    /// callers can perform accurate capsule-space overlap tests.
    /// Returns <c>false</c> if the id is out of range or no capsule was found.
    /// </summary>
    public bool TryGetHitZoneCapsule(int holeId, out CapsuleCollider capsule)
    {
        capsule = null;
        if (capsuleColliders == null || holeId < 0 || holeId >= capsuleColliders.Length)
            return false;
        capsule = capsuleColliders[holeId];
        return capsule != null;
    }

    // -------------------------------------------------------------------------
    //  Private helpers
    // -------------------------------------------------------------------------

    private void AssertValidId(int holeId)
    {
        if (holeId < 0 || holeId >= holes.Length)
            throw new System.ArgumentOutOfRangeException(
                nameof(holeId),
                $"holeId must be in [0, {holes.Length - 1}], received {holeId}.");
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (holes != null && holes.Length != 5)
            Debug.LogWarning(
                "[HoleManager] 'Holes' array should have exactly 5 entries (Hole_0 to Hole_4).", this);
    }
#endif
}
