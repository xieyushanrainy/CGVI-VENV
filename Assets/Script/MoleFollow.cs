using UnityEngine;

/// <summary>
/// Keeps the Mole character positioned at the player's camera (head) while
/// remaining perfectly upright (no tilt or roll inherited from head tracking).
///
/// Attach this component directly to the Mole prefab root.
/// Assign 'cameraTransform' in the Inspector (drag the XR Main Camera in).
/// The script unparents the Mole at Start so it is never rotated by the rig.
/// </summary>
public class MoleFollow : MonoBehaviour
{
    [Header("Target")]
    [Tooltip("The XR Main Camera transform that the Mole should follow. " +
             "Falls back to Camera.main at Start if not assigned.")]
    [SerializeField] private Transform cameraTransform;

    [Header("Position Offset")]
    [Tooltip("Offset applied relative to the player's yaw (horizontal facing direction). " +
             "Z moves the Mole in front/behind, X moves left/right, Y raises/lowers. " +
             "Example: (0, -1.6, 1) places the Mole on the floor 1 m in front.")]
    [SerializeField] private Vector3 positionOffset = new Vector3(0f, -1.6f, 0f);

    [Header("Rotation")]
    [Tooltip("When true the Mole rotates to match the camera's Y-axis (yaw) " +
             "so it faces the same direction as the player. " +
             "When false the Mole keeps a fixed world rotation.")]
    [SerializeField] private bool matchYaw = true;

    // -------------------------------------------------------------------------

    private void Start()
    {
        // Fall back to Camera.main if nothing is assigned in the Inspector.
        if (cameraTransform == null)
        {
            if (Camera.main != null)
            {
                cameraTransform = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("[MoleFollow] cameraTransform is not assigned and no Camera.main found. " +
                                 "Drag the XR Main Camera into this field.", this);
                enabled = false;
                return;
            }
        }

        // Detach from any parent so the rig's rotations are never inherited.
        transform.SetParent(null, worldPositionStays: true);
    }

    private void LateUpdate()
    {
        // LateUpdate runs after all XR tracking updates, giving us the final
        // position of the rig for this frame.

        // --- Position ---
        // Use only the camera's yaw to rotate the offset so the Mole follows
        // the player's floor position rather than being locked to the head view.
        float cameraYaw = cameraTransform.eulerAngles.y;
        Vector3 worldOffset = Quaternion.Euler(0f, cameraYaw, 0f) * positionOffset;
        transform.position = cameraTransform.position + worldOffset;

        // --- Rotation ---
        // Extract only the yaw (Y-axis rotation) from the camera so the
        // Mole stays upright regardless of any pitch/roll from head tracking.
        if (matchYaw)
        {
            float yaw = cameraTransform.eulerAngles.y;
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
        }
        else
        {
            transform.rotation = Quaternion.identity;
        }
    }
}
