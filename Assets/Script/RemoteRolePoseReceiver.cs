using UnityEngine;

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

    [Tooltip("Euler angle offset applied to the OpponentMole to preserve the " +
             "model's baked-in rotation (e.g. 90° around Z). " +
             "Must match the modelRotationOffset set on MoleFollow.")]
    [SerializeField] private Vector3 moleModelRotationOffset = new Vector3(0f, 0f, 90f);

    [Tooltip("Seconds to keep OpponentMole active after isVisible flips false,\n" +
             "allowing the position lerp to smoothly carry the mole below the\n" +
             "box rim before the object is disabled.  0 = instant hide.")]
    [SerializeField] private float sinkOutDuration = 0.5f;

    [Tooltip("How far below the box-top rim the mole sinks after going hidden\n" +
             "(e.g. after a hit).  The LerpToTarget animation drives the mole\n" +
             "from camera-height down to boxTopY - this value, so it visually\n" +
             "drops into the box rather than freezing at the rim.\n" +
             "0 = old behaviour (lerps to the rim and lingers).")]
    [SerializeField] private float sinkBelowBoxDepth = 0.4f;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private Vector3        targetPosition;
    private RemoteRoleType remoteRole;
    private bool           remoteVisible;  // latest isVisible from network

    private bool           hasReceivedFirstMessage = false;
    private bool           moleWasHidden           = true;  // tracks previous frame visibility

    // Sink-out state: mole stays active and lerps to target after going hidden.
    private bool    isSinkingOut  = false;
    private float   sinkOutTimer  = 0f;

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
                // Interpolate while visible OR while sinking out after a hit.
                if (remoteVisible || isSinkingOut)
                {
                    // Track the published position throughout — the publisher now sends
                    // real camera Y even when hidden, so targetPosition naturally
                    // animates the descent without needing a frozen sinkTarget.
                    LerpToTarget(opponentMole, targetPosition, isMole: true);

                    if (isSinkingOut)
                    {
                        sinkOutTimer -= Time.deltaTime;
                        if (sinkOutTimer <= 0f)
                        {
                            isSinkingOut = false;
                            SetActive(opponentMole, false, nameof(opponentMole));
                        }
                    }
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
                SetActive(opponentHammer, false, nameof(opponentHammer));

                if (remoteVisible)
                {
                    // Cancel any in-progress sink-out — mole is popping up again.
                    isSinkingOut = false;

                    SetActive(opponentMole, true, nameof(opponentMole));

                    // On hidden → visible transition, place the mole just below the
                    // box rim so it smoothly lerps upward rather than snapping to
                    // full head-height in a single frame.
                    if (moleWasHidden && opponentMole != null)
                    {
                        opponentMole.transform.position = new Vector3(
                            targetPosition.x,
                            targetPosition.y - sinkBelowBoxDepth,
                            targetPosition.z);
                        opponentMole.transform.rotation = Quaternion.Euler(moleModelRotationOffset);
                    }
                }
                else if (!moleWasHidden && !isSinkingOut)
                {
                    // Mole just went hidden (visible → false).
                    // Freeze a sink target *below* the box rim so the lerp carries
                    // the mole downward into the box.  targetPosition.y is boxTopY
                    // here (pinned by the publisher), so subtracting sinkBelowBoxDepth
                    // gives a point clearly underground.
                    isSinkingOut = true;
                    sinkOutTimer = sinkOutDuration;
                    // opponentMole stays active — Update() will disable it when the timer expires.
                }
                // else: already hidden and not sinking — no state change needed.

                moleWasHidden = !remoteVisible;
                break;
        }
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    private void LerpToTarget(GameObject obj, Vector3 target, bool isMole = false)
    {
        if (obj == null) return;
        obj.transform.position = Vector3.Lerp(
            obj.transform.position,
            target,
            positionLerpSpeed * Time.deltaTime);
        if (isMole)
            obj.transform.rotation = Quaternion.Euler(moleModelRotationOffset);
    }

    // Convenience overload — lerps to the latest network targetPosition.
    private void LerpToTarget(GameObject obj, bool isMole = false)
        => LerpToTarget(obj, targetPosition, isMole);

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
