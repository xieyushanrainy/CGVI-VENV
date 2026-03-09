using System;
using UnityEngine;
using Ubiq.Messaging;

// =============================================================================
//  LocalRolePosePublisher.cs
//
//  Reads the local player's pose every send tick and broadcasts an
//  OpponentPoseMessage over Ubiq so the remote peer can drive its
//  OpponentSimulator.
//
//  IMPORTANT — HOW UBIQ ROUTING WORKS
//  ------------------------------------
//  NetworkScene.Register(this) assigns a NetworkId derived from this
//  component's position in the scene graph (GameObject name path).
//  When this script on Client A calls context.SendJson(...), the message is
//  delivered to the matching LocalRolePosePublisher.ProcessMessage() on
//  Client B — because both instances occupy the same scene graph path.
//
//  This means ProcessMessage() here receives the *remote* player's pose,
//  NOT a loopback of local sends.  We forward that data to
//  RemoteRolePoseReceiver for visualisation.
//
//  SETUP
//  -----
//  1. Attach this script to GameManager (or any persistent scene object).
//  2. Fill in the Inspector fields (listed below).
//  3. Drag the OpponentSimulator's RemoteRolePoseReceiver component into
//     the "remoteReceiver" slot.
//
//  Inspector fields
//  ----------------
//  xrCameraTransform    → XR Origin / Camera Offset / Main Camera
//  rightHandTransform   → XR Origin / Right Controller
//  playerMoleTransform  → PlayerMole (optional; driven by MoleFollow.cs for
//                          local visuals — NOT used to derive published position)
//  moleBoxTransform     → Mole box   (optional; auto-reads top Y from bounds)
//  moleBoxTopY          → Fallback explicit world-space Y of the box's top
//  localRole            → Populated from GameData.LocalRole at Start
//  headToPivotOffsetY   → Metres below the XR camera to place the body pivot.
//                          Published Mole position = camera.pos with Y -= this.
//                          Default 1.2 = (1 - 1/3) * 1.8  (standard body ratio)
//  exposureThreshold    → Metres the eye/camera must be ABOVE the box top
//                          to be considered exposed (default 0 = any peek counts)
//  sendRate             → Max messages per second (default 15 Hz)
//  positionThreshold    → Min position change (metres) to trigger a send
//  remoteReceiver       → RemoteRolePoseReceiver on OpponentSimulator
// =============================================================================

/// <summary>
/// Publishes the local player's pose over Ubiq and forwards incoming remote
/// pose messages to <see cref="RemoteRolePoseReceiver"/> for visualisation.
///
/// Attach to GameManager (or any persistent scene object).
/// </summary>
public class LocalRolePosePublisher : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields — References
    // -------------------------------------------------------------------------

    [Header("XR References")]
    [Tooltip("XR Origin / Camera Offset / Main Camera")]
    [SerializeField] private Transform xrCameraTransform;

    [Tooltip("XR Origin / Right Controller — used when role is Hammer")]
    [SerializeField] private Transform rightHandTransform;

    [Tooltip("PlayerMole root transform.\n" +
             "PlayerMole is purely a local visual representation driven by MoleFollow.cs.\n" +
             "Its position is NOT used to derive what is published over the network.\n" +
             "The published body position is always computed from xrCameraTransform\n" +
             "using headToPivotOffsetY, so it is consistent regardless of whether\n" +
             "this field is assigned.")]
    [SerializeField] private Transform playerMoleTransform;

    [Header("Mole Box")]
    [Tooltip("Optional: drag the Mole box here to auto-derive its top Y from its scale.\n" +
             "Assumes the pivot is at the object's centre. " +
             "If not assigned, moleBoxTopY is used instead.")]
    [SerializeField] private Transform moleBoxTransform;

    [Tooltip("World-space Y of the Mole box's top surface.\n" +
             "Only used when moleBoxTransform is not assigned.")]
    [SerializeField] private float moleBoxTopY = 0.5f;

    [Header("Receiver")]
    [Tooltip("Drag the RemoteRolePoseReceiver on OpponentSimulator here.\n" +
             "This component forwards incoming remote pose messages to it.")]
    [SerializeField] private RemoteRolePoseReceiver remoteReceiver;

    // -------------------------------------------------------------------------
    //  Inspector fields — Role
    // -------------------------------------------------------------------------

    [Header("Role")]
    [Tooltip("Overwritten at Start from GameData.LocalRole.\n" +
             "Can be set manually in the Editor for testing.")]
    public RoleManager.Role localRole = RoleManager.Role.Hammer;

    // -------------------------------------------------------------------------
    //  Inspector fields — Mole body pivot
    // -------------------------------------------------------------------------

    [Header("Mole Body Pivot")]
    [Tooltip("How many metres BELOW the XR camera (eye/head) the PlayerMole pivot sits.\n" +
             "This is the only value used to compute the published body position:\n" +
             "  bodyPos   = xrCamera.position\n" +
             "  bodyPos.y -= headToPivotOffsetY\n" +
             "\n" +
             "PlayerMole (the local mesh) is a visual-only object driven by MoleFollow.cs.\n" +
             "Its transform is NOT read here; this field makes the offset explicit\n" +
             "and tunable in the Inspector.\n" +
             "\n" +
             "Default 1.2 m is derived from the standard body ratio:\n" +
             "  headToPivotOffsetY = (1 - eyeOffsetRatio) * bodyHeight\n" +
             "  e.g. (1 - 1/3) * 1.8 = 1.2 m")]
    [SerializeField] private float headToPivotOffsetY = 1.2f;

    // -------------------------------------------------------------------------
    //  Inspector fields — Exposure
    // -------------------------------------------------------------------------

    [Header("Mole Exposure")]
    [Tooltip("The eye/camera must be at least this many metres ABOVE the box\n" +
             "top to be considered exposed (isVisible = true).\n" +
             "0 = any amount above the box counts.\n" +
             "Negative = the eye can be slightly below the top and still count.")]
    [SerializeField] private float exposureThreshold = 0f;

    // -------------------------------------------------------------------------
    //  Inspector fields — Send settings
    // -------------------------------------------------------------------------

    [Header("Send Settings")]
    [Tooltip("Maximum number of messages sent per second.\n" +
             "15 Hz is recommended for this project: responsive enough for\n" +
             "the fast hammer swings, cheap enough for a shared server.")]
    [SerializeField] private float sendRate = 15f;

    [Tooltip("A new position message is only sent if the tracked object has\n" +
             "moved more than this distance (metres) since the last send.\n" +
             "Keeps traffic near zero when neither player is moving.")]
    [SerializeField] private float positionThreshold = 0.005f;

    [Header("Editor / Testing")]
    [Tooltip("When enabled, skips Ubiq registration entirely and feeds the\n" +
             "computed pose straight to RemoteRolePoseReceiver on this machine.\n" +
             "Use this when running the game scene directly in the Editor\n" +
             "without a NetworkScene present.\n" +
             "Has no effect in a real multiplayer session — disable before\n" +
             "final playtesting.")]
    [SerializeField] private bool offlineMockMode = false;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private NetworkContext context;
    private bool           networkAvailable = false;
    private float          sendTimer;
    private int            sequence;

    private Vector3 lastSentPosition;
    private bool    lastSentVisibility;
    private bool    firstSend = true;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // Override the inspector default with the actual runtime role.
        localRole = GameData.LocalRole;

        if (offlineMockMode)
        {
            Debug.Log("[LocalRolePosePublisher] Offline mock mode enabled — " +
                      "Ubiq registration skipped. Poses will be fed locally.");
            return;
        }

        // NetworkScene.Register throws a KeyNotFoundException when there is no
        // NetworkScene in the scene (e.g. running the game scene standalone in
        // the Editor without going through the lobby scene that carries the
        // NetworkScene via DontDestroyOnLoad).
        try
        {
            context = NetworkScene.Register(this);
            networkAvailable = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[LocalRolePosePublisher] NetworkScene not found — " +
                             "networking disabled. Run via the lobby scene for a live " +
                             $"connection, or enable Offline Mock Mode for Editor testing.\n{e.Message}");
        }
    }

    private void Update()
    {
        // Rate-limit sends to sendRate Hz.
        sendTimer -= Time.unscaledDeltaTime;
        if (sendTimer > 0f) return;
        sendTimer = 1f / Mathf.Max(sendRate, 1f);

        TrySendPose();
    }

    // -------------------------------------------------------------------------
    //  Pose publishing
    // -------------------------------------------------------------------------

    private void TrySendPose()
    {
        switch (localRole)
        {
            case RoleManager.Role.Hammer: TrySendHammerPose(); break;
            case RoleManager.Role.Mole:   TrySendMolePose();   break;
            default:
                // NotInRoom / None — role not yet confirmed, nothing to send.
                break;
        }
    }

    // --- Hammer ---------------------------------------------------------------

    private void TrySendHammerPose()
    {
        if (rightHandTransform == null)
        {
            Debug.LogWarning("[LocalRolePosePublisher] rightHandTransform is not assigned.", this);
            return;
        }

        Vector3    pos     = rightHandTransform.position;
        const bool visible = true; // Hammer is always visible to the opponent.

        // In mock mode always send so the receiver stays live in the Editor.
        if (!offlineMockMode && !firstSend && !PositionChanged(pos) && lastSentVisibility == visible)
            return;

        Send(RemoteRoleType.Hammer, pos, visible);
    }

    // --- Mole -----------------------------------------------------------------

    private void TrySendMolePose()
    {
        if (xrCameraTransform == null)
        {
            Debug.LogWarning("[LocalRolePosePublisher] xrCameraTransform is not assigned.", this);
            return;
        }

        // ── Body position (published to remote) ──────────────────────────────
        // Always derived from the XR camera using headToPivotOffsetY.
        // PlayerMole (the local mesh object) is a visual-only representation
        // driven by MoleFollow.cs — its transform is intentionally NOT read
        // here, so the networked position is always authoritative and consistent
        // regardless of whether the local PlayerMole GameObject is active.
        Vector3 bodyPos   = xrCameraTransform.position;
        bodyPos.y        -= headToPivotOffsetY;

        // ── Exposure (visibility) ─────────────────────────────────────────────
        // Determined purely by the XR camera (eye/head) Y compared to the
        // top surface of the Mole box — NOT by the fake body mesh position.
        // Using the head means: as soon as the player peeks above the rim,
        // their avatar becomes visible to the Hammer player.
        float boxTopY   = ResolveBoxTopY();
        bool  isVisible = xrCameraTransform.position.y > boxTopY + exposureThreshold;

        // In mock mode always send so the receiver stays live in the Editor.
        if (!offlineMockMode && !firstSend && !PositionChanged(bodyPos) && lastSentVisibility == isVisible)
            return;

        Send(RemoteRoleType.Mole, bodyPos, isVisible);
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    private float ResolveBoxTopY()
    {
        if (moleBoxTransform != null)
        {
            // Assumes pivot is at the object centre (standard Unity cube).
            return moleBoxTransform.position.y + moleBoxTransform.lossyScale.y * 0.5f;
        }
        return moleBoxTopY;
    }

    private bool PositionChanged(Vector3 current)
        => Vector3.Distance(current, lastSentPosition) > positionThreshold;

    private void Send(RemoteRoleType role, Vector3 position, bool isVisible)
    {
        var msg = new OpponentPoseMessage
        {
            role      = role,
            position  = position,
            isVisible = isVisible,
            sequence  = sequence++,
            timestamp = (double)Time.unscaledTime
        };

        if (offlineMockMode)
        {
            // Offline mock: bypass Ubiq entirely and drive the local receiver
            // directly so you can preview opponent visualisation in the Editor.
            Debug.Log($"[LocalRolePosePublisher] MOCK SEND | role={msg.role} " +
                      $"pos={msg.position:F2} visible={msg.isVisible} seq={msg.sequence}");
            if (remoteReceiver != null)
                remoteReceiver.OnRemotePoseReceived(msg);
        }
        else if (networkAvailable)
        {
            try
            {
                context.SendJson(msg);
                Debug.Log($"[LocalRolePosePublisher] SENT | role={msg.role} " +
                          $"pos={msg.position:F2} visible={msg.isVisible} seq={msg.sequence}");
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[LocalRolePosePublisher] SendJson failed: {e.Message}");
                return;
            }
        }
        else
        {
            return; // No network and not in mock mode — nothing to do.
        }

        lastSentPosition   = position;
        lastSentVisibility = isVisible;
        firstSend          = false;
    }

    // -------------------------------------------------------------------------
    //  Ubiq inbound — receives the REMOTE player's messages
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by Ubiq when the remote peer's LocalRolePosePublisher sends a
    /// message.  Forwards the decoded data to RemoteRolePoseReceiver, which
    /// handles interpolation and visibility of the OpponentSimulator children.
    /// </summary>
    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        if (remoteReceiver == null)
        {
            Debug.LogWarning("[LocalRolePosePublisher] remoteReceiver is not assigned — " +
                             "drag the RemoteRolePoseReceiver on OpponentSimulator into " +
                             "the Inspector slot.", this);
            return;
        }

        var msg = message.FromJson<OpponentPoseMessage>();
        remoteReceiver.OnRemotePoseReceived(msg);
    }
}
