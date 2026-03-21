using UnityEngine;

// =============================================================================
//  WorldSpaceCanvasSpawner.cs
//
//  Converts / manages a Canvas as a world-space UI panel that appears
//  comfortably in front of the player's eyes in VR.
//
//  USAGE
//  -----
//  1. Attach this component to a manager GameObject or directly to the Canvas.
//  2. Assign xrCamera (auto-falls-back to Camera.main) and targetCanvas.
//  3. Call ShowCanvas()  to enable + reposition the panel.
//     Call HideCanvas()  to disable it.
//
//  Integration with EndGameController / canvasControl:
//    • Call ShowCanvas() from canvasControl.End() or EndGameController when
//      opening the end-game menu so it snaps in front of the player.
//    • Call HideCanvas() when restarting / exiting the round.
// =============================================================================

/// <summary>
/// Manages a world-space Canvas, placing it comfortably in front of the
/// user's XR camera whenever <see cref="ShowCanvas"/> or
/// <see cref="ShowInFrontOfUser"/> is called.
/// </summary>
[RequireComponent(typeof(Canvas))]
public class WorldSpaceCanvasSpawner : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("References")]
    [Tooltip("The XR camera transform used to position and orient the canvas.\n" +
             "Leave unassigned to auto-detect Camera.main at Awake.")]
    [SerializeField] public Transform xrCamera;

    [Tooltip("The Canvas that will be switched to World Space and repositioned.\n" +
             "If left unassigned the Canvas on this GameObject is used.")]
    [SerializeField] public Canvas targetCanvas;

    [Header("Placement")]
    [Tooltip("Distance in metres the panel is placed in front of the camera.")]
    [SerializeField] public float distanceFromCamera = 1.5f;

    [Tooltip("World-Y offset applied after the forward placement.\n" +
             "A negative value positions the panel slightly below eye level.")]
    [SerializeField] public float verticalOffset = -0.1f;

    [Header("Orientation")]
    [Tooltip("When true the canvas is kept perfectly upright (no roll / tilt).")]
    [SerializeField] public bool keepUpright = true;

    [Tooltip("When true the forward direction used for placement is projected\n" +
             "onto the horizontal XZ plane, so the panel does not move up/down\n" +
             "when the player tilts their head.")]
    [SerializeField] public bool matchCameraForwardIgnoringPitch = true;

    [Tooltip("When true the canvas rotation is locked to world identity (no yaw / pitch / roll).\n" +
             "The canvas will always stand perfectly straight aligned with world axes,\n" +
             "regardless of which direction the player is facing.\n" +
             "When false the canvas rotates to face the player.")]
    [SerializeField] public bool useWorldAlignedRotation = true;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Awake()
    {
        // Auto-resolve XR camera.
        if (xrCamera == null)
        {
            if (Camera.main != null)
            {
                xrCamera = Camera.main.transform;
            }
            else
            {
                Debug.LogWarning("[WorldSpaceCanvasSpawner] xrCamera is not assigned " +
                                 "and Camera.main could not be found. " +
                                 "Assign xrCamera in the Inspector.");
            }
        }

        // Auto-resolve Canvas — prefer the serialised field, then try self.
        if (targetCanvas == null)
        {
            targetCanvas = GetComponent<Canvas>();
            if (targetCanvas == null)
            {
                Debug.LogWarning("[WorldSpaceCanvasSpawner] targetCanvas is not assigned " +
                                 "and no Canvas component was found on this GameObject. " +
                                 "Assign targetCanvas in the Inspector.");
            }
        }
    }

    // -------------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Enables the canvas GameObject and immediately repositions it in front
    /// of the user.
    /// </summary>
    public void ShowCanvas()
    {
        if (!ValidateReferences()) return;

        targetCanvas.gameObject.SetActive(true);
        ShowInFrontOfUser();
    }

    /// <summary>
    /// Disables the canvas GameObject.
    /// </summary>
    public void HideCanvas()
    {
        if (targetCanvas == null)
        {
            Debug.LogWarning("[WorldSpaceCanvasSpawner] HideCanvas called but targetCanvas is null.");
            return;
        }

        targetCanvas.gameObject.SetActive(false);
    }

    /// <summary>
    /// Switches the canvas to World Space render mode, then positions and
    /// rotates it so it appears comfortably in front of the player's eyes.
    /// Does NOT enable or disable the canvas — call <see cref="ShowCanvas"/>
    /// if you also need to make it visible.
    /// </summary>
    [ContextMenu("Show In Front Of User")]
    public void ShowInFrontOfUser()
    {
        if (!ValidateReferences()) return;

        // Force World Space render mode — required for VR placement.
        targetCanvas.renderMode = RenderMode.WorldSpace;

        // ------------------------------------------------------------------
        //  1. Compute forward direction
        // ------------------------------------------------------------------
        Vector3 forward;

        if (matchCameraForwardIgnoringPitch)
        {
            // Project camera forward onto the horizontal (XZ) plane so the
            // panel does not rise/fall with head pitch.
            Vector3 camForward = xrCamera.forward;
            forward = new Vector3(camForward.x, 0f, camForward.z);

            // Fall back to world forward if the camera is looking straight up/down.
            if (forward.sqrMagnitude < 0.001f)
                forward = Vector3.forward;
            else
                forward.Normalize();
        }
        else
        {
            forward = xrCamera.forward;
        }

        // ------------------------------------------------------------------
        //  2. Position the canvas
        // ------------------------------------------------------------------
        Vector3 targetPosition = xrCamera.position
                                 + forward * distanceFromCamera
                                 + Vector3.up * verticalOffset;

        targetCanvas.transform.position = targetPosition;

        // ------------------------------------------------------------------
        //  3. Orient the canvas to face the user
        // ------------------------------------------------------------------
        if (useWorldAlignedRotation)
        {
            // Lock to world identity — canvas stands perfectly straight with
            // no yaw, pitch, or roll relative to world axes.
            targetCanvas.transform.rotation = Quaternion.identity;
        }
        else
        {
        // Direction from canvas to camera (we want the canvas front to face
        // the camera, so we negate this in LookRotation).
        Vector3 dirToCamera = xrCamera.position - targetPosition;

        if (keepUpright)
        {
            // Remove vertical component so the canvas stands straight up.
            dirToCamera.y = 0f;

            if (dirToCamera.sqrMagnitude < 0.001f)
            {
                // Degenerate case: camera is directly above/below canvas origin.
                // Use the negated forward as a safe fallback.
                dirToCamera = -forward;
                dirToCamera.y = 0f;
            }
        }

        if (dirToCamera.sqrMagnitude > 0.001f)
        {
            // Negate so the canvas *front* faces the player (LookRotation sets
            // the positive-Z axis; the canvas normal is +Z by default).
            targetCanvas.transform.rotation = Quaternion.LookRotation(
                -dirToCamera.normalized,
                keepUpright ? Vector3.up : xrCamera.up
            );
        }
        }
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns false and logs a warning if any required reference is missing.
    /// </summary>
    private bool ValidateReferences()
    {
        if (xrCamera == null)
        {
            Debug.LogWarning("[WorldSpaceCanvasSpawner] xrCamera is null. " +
                             "Assign it in the Inspector or ensure Camera.main exists at Awake.");
            return false;
        }

        if (targetCanvas == null)
        {
            Debug.LogWarning("[WorldSpaceCanvasSpawner] targetCanvas is null. " +
                             "Assign it in the Inspector.");
            return false;
        }

        return true;
    }
}
