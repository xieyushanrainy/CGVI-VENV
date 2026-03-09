using UnityEngine;

// =============================================================================
//  RemoteRolePoseReceiver.cs
//
//  Visualises the remote player's position on the local machine by driving
//  the OpponentSimulator child objects:
//      OpponentSimulator / OpponentHammer
//      OpponentSimulator / OpponentMole
//
//  This component does NOT register with Ubiq directly.
//  Instead, LocalRolePosePublisher.ProcessMessage() receives incoming network
//  messages and calls OnRemotePoseReceived() here.  This design keeps the
//  networking concern in one place (the publisher) while isolating the
//  visualisation concern here.
//
//  SETUP
//  -----
//  1. Attach this script to the OpponentSimulator GameObject.
//  2. Fill in the Inspector fields (listed below).
//  3. Drag this component into the LocalRolePosePublisher's "remoteReceiver"
//     slot on GameManager so the publisher can forward messages here.
//
//  Inspector fields
//  ----------------
//  opponentHammer     → OpponentSimulator / OpponentHammer
//  opponentMole       → OpponentSimulator / OpponentMole
//  positionLerpSpeed  → Lerp speed toward received position (default 15)
//
//  BEHAVIOUR SUMMARY
//  -----------------
//  Both OpponentHammer and OpponentMole start disabled at runtime.
//  After the first message arrives:
//
//    Remote role = Hammer
//      • OpponentHammer enabled, lerped to received position each frame.
//      • OpponentMole  disabled.
//
//    Remote role = Mole
//      • OpponentHammer disabled.
//      • OpponentMole enabled only when isVisible = true.
//        When isVisible flips from false → true the object is snapped
//        instantly to the received position to avoid a lerp-in pop.
//      • OpponentMole  disabled when isVisible = false (mole is hiding).
// =============================================================================

/// <summary>
/// Visualises the remote player's position by driving OpponentHammer and
/// OpponentMole.  Receives data via <see cref="OnRemotePoseReceived"/>, which
/// is called by <see cref="LocalRolePosePublisher"/> when a network message
/// arrives.
///
/// Attach to OpponentSimulator.
/// </summary>
public class RemoteRolePoseReceiver : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("References")]
    [Tooltip("OpponentSimulator / OpponentHammer")]
    [SerializeField] private GameObject opponentHammer;

    [Tooltip("OpponentSimulator / OpponentMole")]
    [SerializeField] private GameObject opponentMole;

    [Header("Smoothing")]
    [Tooltip("How fast the active opponent object lerps toward the received\n" +
             "position each frame.  Higher = snappier.\n" +
             "Recommended range: 10–20 for a responsive XR feel.")]
    [SerializeField] private float positionLerpSpeed = 15f;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private Vector3        targetPosition;
    private RemoteRoleType remoteRole;
    private bool           remoteVisible;  // latest isVisible from network

    private bool           hasReceivedFirstMessage = false;
    private bool           moleWasHidden           = true;  // tracks previous frame visibility

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Both objects are hidden until the first network message clarifies
        // the remote player's role.
        SetActive(opponentHammer, false, nameof(opponentHammer));
        SetActive(opponentMole,   false, nameof(opponentMole));
    }

    private void Update()
    {
        if (!hasReceivedFirstMessage) return;

        switch (remoteRole)
        {
            case RemoteRoleType.Hammer:
                LerpToTarget(opponentHammer);
                break;

            case RemoteRoleType.Mole:
                // Only interpolate while the mole is actually visible on screen.
                if (remoteVisible)
                {
                    LerpToTarget(opponentMole);
                }
                else
                {
                    // Lerp is skipped because remoteVisible = false.
                    // OpponentMole is disabled — check that xrCamera Y is above
                    // moleBoxTopY + exposureThreshold in LocalRolePosePublisher.
                    Debug.Log("[RemoteRolePoseReceiver] Lerp SKIPPED — remoteVisible is false. " +
                              "OpponentMole is hidden. Raise the camera or lower moleBoxTopY.");
                }
                break;
        }
    }

    // -------------------------------------------------------------------------
    //  Public API — called by LocalRolePosePublisher.ProcessMessage()
    // -------------------------------------------------------------------------

    /// <summary>
    /// Applies a decoded <see cref="OpponentPoseMessage"/> received from the
    /// remote player.  Updates target position, visibility, and object states.
    /// </summary>
    public void OnRemotePoseReceived(OpponentPoseMessage msg)
    {
        remoteRole     = msg.role;
        targetPosition = msg.position;
        remoteVisible  = msg.isVisible;

        Debug.Log($"[RemoteRolePoseReceiver] RECEIVED | role={msg.role} " +
                  $"targetPos={msg.position:F2} visible={msg.isVisible} seq={msg.sequence}");

        ApplyVisibility();
        hasReceivedFirstMessage = true;
    }

    // -------------------------------------------------------------------------
    //  Visibility management
    // -------------------------------------------------------------------------

    private void ApplyVisibility()
    {
        switch (remoteRole)
        {
            case RemoteRoleType.Hammer:
                SetActive(opponentHammer, true,  nameof(opponentHammer));
                SetActive(opponentMole,   false, nameof(opponentMole));
                moleWasHidden = true; // Reset so a later mole snap works.
                break;

            case RemoteRoleType.Mole:
                SetActive(opponentHammer, false,         nameof(opponentHammer));
                SetActive(opponentMole,   remoteVisible, nameof(opponentMole));

                // When the mole transitions from hidden → visible, snap it
                // instantly to the current target position so it doesn't lerp
                // in visibly from its last known location.
                if (remoteVisible && moleWasHidden && opponentMole != null)
                {
                    opponentMole.transform.position = targetPosition;
                    Debug.Log($"[RemoteRolePoseReceiver] OpponentMole SNAPPED to {targetPosition:F2} (was hidden)");
                }

                if (remoteVisible != !moleWasHidden)
                    Debug.Log($"[RemoteRolePoseReceiver] OpponentMole visibility → {remoteVisible}");

                moleWasHidden = !remoteVisible;
                break;
        }
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    private void LerpToTarget(GameObject obj)
    {
        if (obj == null) return;
        obj.transform.position = Vector3.Lerp(
            obj.transform.position,
            targetPosition,
            positionLerpSpeed * Time.deltaTime);
    }

    private void SetActive(GameObject obj, bool active, string label)
    {
        if (obj == null)
        {
            Debug.LogWarning($"[RemoteRolePoseReceiver] '{label}' is not assigned.", this);
            return;
        }

        // Avoid redundant SetActive calls (they trigger messages in the engine).
        if (obj.activeSelf != active)
            obj.SetActive(active);
    }
}
