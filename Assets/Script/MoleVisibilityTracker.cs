using System;
using UnityEngine;
using Ubiq.Messaging;

// =============================================================================
//  MoleVisibilityTracker.cs
//
//  Computes the mole player's visibility state and broadcasts
//  MoleStateMessage over Ubiq so the remote peer (and ScoreManager) always
//  has authoritative mole-state data.
//
//  Visibility is determined by whether the XR camera (head/eye) is above a
//  configurable threshold above the top of the mole box — the same logic
//  used by LocalRolePosePublisher, but with the additional scoring fields
//  (exposureSequence, activeHoleId) that LocalRolePosePublisher omits.
//
//  MESSAGE ROUTING
//  ---------------
//  This component registers with NetworkScene.Register(this).
//  On the Mole player's machine:
//    - Computes state each tick, sends MoleStateMessage when it changes.
//    - Fires OnMoleStateUpdate locally so ScoreManager on the same machine
//      is immediately aware of the latest state.
//  On the Hammer player's machine:
//    - ProcessMessage() receives the remote Mole player's state updates.
//    - Fires OnMoleStateUpdate so ScoreManager (if it is the authority) can
//      validate incoming HitAttemptMessages against the current mole state.
//
//  SETUP
//  -----
//  1. Attach to GameManager/MoleVisibilityTracker.
//  2. Assign Inspector fields:
//       xrCameraTransform   → XR Origin / Camera Offset / Main Camera
//       playerMoleTransform → PlayerMole (the local visual object, optional)
//       moleBoxTransform    → Mole box (auto-derives topY from its scale)
//       moleBoxTopY         → Fallback if moleBoxTransform is unassigned
//  3. Call SetActiveHole(id) from your mole-placement logic when the mole
//     moves to a new hole (id 0–4, or -1 to clear).
//  4. MoleVisibilityTracker only SENDS when the local role is Mole.  When the
//     local role is Hammer it is receive-only (ProcessMessage still fires the
//     event, so ScoreManager always gets the remote mole state).
// =============================================================================

/// <summary>
/// Tracks and publishes the mole player's visibility state for the scoring
/// system.  Fires <see cref="OnMoleStateUpdate"/> on both clients so that
/// <see cref="ScoreManager"/> always has the latest mole state regardless of
/// which client holds authority.
///
/// Attach to GameManager/MoleVisibilityTracker.
/// </summary>
public class MoleVisibilityTracker : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("XR / Mole References")]
    [Tooltip("XR Origin / Camera Offset / Main Camera.\n" +
             "Used to determine head-Y for visibility checks and to derive\n" +
             "the published body position.  Falls back to Camera.main at Start.")]
    [SerializeField] private Transform xrCameraTransform;

    [Tooltip("The PlayerMole root transform (local visual object only).\n" +
             "Not used to compute the published position — that is always\n" +
             "derived from xrCameraTransform minus headToPivotOffsetY.\n" +
             "Assign for reference / optional local visual sync only.")]
    [SerializeField] private Transform playerMoleTransform;

    [Header("Mole Box")]
    [Tooltip("Optional: drag the Mole box here to auto-derive its top Y from\n" +
             "its world scale.  Assumes the pivot is at the object centre\n" +
             "(standard Unity cube).  If not assigned, moleBoxTopY is used.")]
    [SerializeField] private Transform moleBoxTransform;

    [Tooltip("Explicit world-space Y of the mole box's top surface.\n" +
             "Only used when moleBoxTransform is not assigned.")]
    [SerializeField] private float moleBoxTopY = 0.5f;

    [Header("Exposure")]
    [Tooltip("Metres the XR camera must be ABOVE the box top to count as visible.\n" +
             "0 = any peek above the rim counts.\n" +
             "Negative = eye may be slightly below the top and still count.")]
    [SerializeField] private float exposureThreshold = 0f;

    [Header("Send Settings")]
    [Tooltip("Maximum MoleStateMessages sent per second (Mole player only).")]
    [SerializeField] private float sendRate = 15f;

    [Tooltip("Minimum position delta (metres) needed to trigger a state send.\n" +
             "Keeps network traffic near zero while the mole is stationary.")]
    [SerializeField] private float positionThreshold = 0.01f;

    // -------------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fired on BOTH clients whenever a new mole state is computed locally
    /// (on the Mole player's machine) or received from the remote peer via
    /// ProcessMessage (on the Hammer player's machine).
    ///
    /// <see cref="ScoreManager"/> subscribes to this event so it always has
    /// up-to-date mole state regardless of which peer is the authority.
    /// </summary>
    public event Action<MoleStateMessage> OnMoleStateUpdate;

    /// <summary>Current active hole id (0–4, or -1 if unset).</summary>
    public int ActiveHoleId => activeHoleId;

    /// <summary>
    /// Whether the mole is currently considered visible (eye above box top +
    /// threshold).  Updated every send tick on the Mole player's machine.
    /// Read by <see cref="LocalRolePosePublisher"/> so both pose sync and
    /// scoring use the exact same visibility result.
    /// </summary>
    public bool IsVisible { get; private set; }

    /// <summary>
    /// World-space Y of the mole box top surface, recomputed each send tick.
    /// Read by <see cref="LocalRolePosePublisher"/> to pin the published mole
    /// position to the box rim — the natural pop-up origin, with no separate
    /// tunable offset that could drift out of sync.
    /// </summary>
    public float BoxTopY { get; private set; }

    /// <summary>
    /// Call this when the mole moves to a new hole.
    /// Triggers an immediate state send on the next tick if the id changed.
    /// Pass -1 to signal that the mole is not currently in any hole.
    /// </summary>
    public void SetActiveHole(int holeId)
    {
        activeHoleId = holeId;
    }

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private NetworkContext   context;
    private bool             networkAvailable;
    private RoleManager.Role localRole;

    // Last-sent values for change detection.
    private bool    lastSentVisible;
    private int     lastSentHoleId   = -2; // -2 forces a send on the first tick.
    private Vector3 lastSentPosition;
    private bool    firstSend        = true;

    private float sendTimer;
    private int   sendSequence;     // Per-message monotonic counter.

    // Exposure tracking — incrementing each time the mole becomes newly visible.
    private int  exposureSequence;
    private bool wasVisible;

    private int activeHoleId = 0;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        localRole = GameData.LocalRole;

        // Fall back to Camera.main if the Inspector field was not wired.
        if (xrCameraTransform == null)
        {
            if (Camera.main != null)
                xrCameraTransform = Camera.main.transform;
            else
                Debug.LogWarning("[MoleVisibilityTracker] xrCameraTransform is not assigned " +
                                 "and no Camera.main was found. Assign it in the Inspector.", this);
        }

        try
        {
            context          = NetworkScene.Register(this);
            networkAvailable = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[MoleVisibilityTracker] NetworkScene not found — " +
                             "networking disabled. Run via the lobby scene for a live " +
                             $"connection.\n{e.Message}");
        }
    }

    private void Update()
    {
        // Only the Mole player publishes state; the Hammer player's tracker is
        // receive-only (ProcessMessage below handles incoming messages).
        if (localRole != RoleManager.Role.Mole) return;

        sendTimer -= Time.unscaledDeltaTime;
        if (sendTimer > 0f) return;
        sendTimer = 1f / Mathf.Max(sendRate, 1f);

        TrySendState();
    }

    // -------------------------------------------------------------------------
    //  State computation and outbound send
    // -------------------------------------------------------------------------

    private void TrySendState()
    {
        if (xrCameraTransform == null) return;

        // ── Visibility ────────────────────────────────────────────────────────
        float boxTopY   = ResolveBoxTopY();
        bool  isVisible = xrCameraTransform.position.y > boxTopY + exposureThreshold;
        IsVisible = isVisible; // expose to LocalRolePosePublisher
        BoxTopY   = boxTopY;   // expose to LocalRolePosePublisher

        // ── Body position ─────────────────────────────────────────────────────
        // Pin Y to the box top surface — the natural pop-up origin for a
        // whack-a-mole avatar.  XZ follows the player's camera so the position
        // tracks movement inside the box.  No head-to-body offset needed:
        // boxTopY is already computed above for the visibility check, so this
        // costs nothing extra and has no separate tunable that could drift.
        Vector3 bodyPos = new Vector3(
            xrCameraTransform.position.x,
            boxTopY,
            xrCameraTransform.position.z);

        // ── Exposure sequence ─────────────────────────────────────────────────
        // Increment each time the mole transitions from hidden → visible so that
        // ScoreManager can identify distinct exposures and avoid double hits.
        if (isVisible && !wasVisible)
            exposureSequence++;
        wasVisible = isVisible;

        // ── Change detection ──────────────────────────────────────────────────
        bool changed = firstSend
            || isVisible    != lastSentVisible
            || activeHoleId != lastSentHoleId
            || PositionChanged(bodyPos);

        if (!changed) return;

        var msg = new MoleStateMessage
        {
            isVisible        = isVisible,
            activeHoleId     = activeHoleId,
            molePosition     = bodyPos,
            exposureSequence = exposureSequence
        };

        // Fire locally so ScoreManager on this machine is also notified.
        OnMoleStateUpdate?.Invoke(msg);

        if (networkAvailable)
        {
            try
            {
                context.SendJson(msg);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[MoleVisibilityTracker] SendJson failed: {e.Message}");
                return;
            }
        }

        lastSentVisible  = isVisible;
        lastSentHoleId   = activeHoleId;
        lastSentPosition = bodyPos;
        firstSend        = false;
    }

    // -------------------------------------------------------------------------
    //  Ubiq inbound — receives the remote Mole player's state messages
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by Ubiq when the remote peer's MoleVisibilityTracker sends a
    /// MoleStateMessage.  Fires <see cref="OnMoleStateUpdate"/> so that
    /// <see cref="ScoreManager"/> receives the latest mole state via the same
    /// event path used for locally-computed state — no special casing required.
    /// </summary>
    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        var msg = message.FromJson<MoleStateMessage>();
        OnMoleStateUpdate?.Invoke(msg);
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    private float ResolveBoxTopY()
    {
        if (moleBoxTransform != null)
            return moleBoxTransform.position.y + moleBoxTransform.lossyScale.y * 0.5f;
        return moleBoxTopY;
    }

    private bool PositionChanged(Vector3 current)
        => Vector3.Distance(current, lastSentPosition) > positionThreshold;
}
