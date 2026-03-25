using UnityEngine;

/// <summary>
/// Tags a hole object or hit-zone collider with its logical index (0–4).
/// Read by <see cref="HammerHitAttemptSender"/> during trigger events
/// to include the correct hole id in the <see cref="HitAttemptMessage"/>.
///
/// Attach to each Hole_X object or its HitZone child.
/// </summary>
public class HoleIdMarker : MonoBehaviour
{
    [SerializeField]
    [Tooltip("Index of this hole in the range [0, 4].\n" +
             "Must be unique per hole. Validated in the Editor on change.")]
    private int holeId = 0;

    /// <summary>The 0-based index of this hole (0–4).</summary>
    public int HoleId => holeId;

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (holeId < 0 || holeId > 4)
            Debug.LogWarning(
                $"[HoleIdMarker] holeId {holeId} on '{name}' is out of range [0, 4]. " +
                "Please correct it in the Inspector.", this);
    }
#endif
}
