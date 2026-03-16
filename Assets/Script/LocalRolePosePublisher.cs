using System;
using UnityEngine;
using Ubiq.Messaging;

// =============================================================================
//  LocalRolePosePublisher.cs
//
//  Reads the local player's pose every send tick and broadcasts anF
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
//  localRole            → Populated from GameData.LocalRole at Start
//  sendRate             → Max messages per second (default 15 Hz)
//  positionThreshold    → Min position change (metres) to trigger a send
//  remoteReceiver       → RemoteRolePoseReceiver on OpponentSimulator
//  moleVisibilityTracker→ MoleVisibilityTracker on the same GameObject.
//                          Provides IsVisible so pose sync and scoring share
//                          the exact same box-top + threshold calculation.
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

    [Header("Receiver")]
    [Tooltip("Drag the RemoteRolePoseReceiver on OpponentSimulator here.\n" +
             "This component forwards incoming remote pose messages to it.")]
    [SerializeField] private RemoteRolePoseReceiver remoteReceiver;

    [Tooltip("MoleVisibilityTracker on the same GameManager.\n" +
             "Its IsVisible property (reusing the same box-top + threshold maths) is\n" +
             "used for pose sync so that visibility is never computed twice.")]
    [SerializeField] private MoleVisibilityTracker moleVisibilityTracker;

    // -------------------------------------------------------------------------
    //  Inspector fields — Role
    // -------------------------------------------------------------------------

    [Header("Role")]
    [Tooltip("Overwritten at Start from GameData.LocalRole.\n" +
             "Can be set manually in the Editor for testing.")]
    public RoleManager.Role localRole = RoleManager.Role.Hammer;

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

        // ── Exposure (visibility) ─────────────────────────────────────────────
        // Delegated to MoleVisibilityTracker so that pose sync and scoring
        // always use the exact same box-top + threshold calculation.
        bool isVisible = moleVisibilityTracker != null
            ? moleVisibilityTracker.IsVisible
            : false;

        // ── Body position (published to remote) ──────────────────────────────
        // When visible (popped up), use the actual camera Y so the opponent
        // sees the mole at the correct height.
        // When hidden (inside the box), pin Y to the box top so the opponent
        // simulator sits flush at the rim rather than floating at head height.
        float   boxTopY = moleVisibilityTracker != null ? moleVisibilityTracker.BoxTopY : 0f;
        float   posY    = isVisible ? xrCameraTransform.position.y : boxTopY;
        Vector3 bodyPos = new Vector3(xrCameraTransform.position.x, posY, xrCameraTransform.position.z);

        // In mock mode always send so the receiver stays live in the Editor.
        if (!offlineMockMode && !firstSend && !PositionChanged(bodyPos) && lastSentVisibility == isVisible)
            return;

        Send(RemoteRoleType.Mole, bodyPos, isVisible);
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

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
            if (remoteReceiver != null)
                remoteReceiver.OnRemotePoseReceived(msg);
        }
        else if (networkAvailable)
        {
            try
            {
                context.SendJson(msg);
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
