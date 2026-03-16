using System;
using System.Linq;
using UnityEngine;
using Ubiq.Messaging;
using Ubiq.Rooms;

// =============================================================================
//  ScoreManager.cs
//
//  Authority-only score truth for the 2-player Whack-a-Mole XR game.
//
//  WHY AUTHORITY OWNS SCORE TRUTH
//  --------------------------------
//  In a 2-player peer-to-peer session neither client is inherently a server,
//  so we designate the peer with the lexicographically lowest UUID as the
//  "authority" — mirroring the pattern already used in RoleManager.  All
//  scoring decisions are made exclusively by this one node to prevent
//  split-brain states (e.g. both clients independently scoring the same hit,
//  or disagreeing about how long the mole was visible).
//
//  Non-authority peers receive authoritative ScoreUpdateMessages and update
//  their HUD / display only.  They never modify any score value themselves.
//
//  MESSAGE FLOW
//  ------------
//
//    Client A  (e.g. Mole player / may be authority)
//    ┌────────────────────────────────────────────────┐
//    │ MoleVisibilityTracker ──MoleStateMsg──────────▶│ Client B's MoleVisibilityTracker
//    │       └─ fires OnMoleStateUpdate (local)       │       └─ fires OnMoleStateUpdate (ProcessMessage)
//    │                                                │
//    │ HammerHitAttemptSender ◀─HitAttemptMsg────────│ Client B's HammerHitAttemptSender
//    │       └─ fires OnHitAttemptEvent (ProcessMsg)  │       └─ fires OnHitAttemptEvent (local)
//    │                                                │
//    │ ScoreManager (subscribes to both events)       │ ScoreManager (subscribes to both events)
//    │   if IsAuthority() → validate & score          │   if IsAuthority() → validate & score
//    │   BroadcastScore() ─ScoreUpdateMsg────────────▶│   ProcessMessage → update display
//    └────────────────────────────────────────────────┘
//
//  SETUP
//  -----
//  1. Attach this component to GameManager/ScoreManager.
//  2. Drag the following into the Inspector slots:
//       moleTracker   → GameManager/MoleVisibilityTracker
//       hammerSender  → PlayerHammer (the HammerHitAttemptSender on PlayerHammer)
//       holeManager   → GameManager/HoleManager
//  3. Tune hammerPointsPerHit, molePointsPerSecond, and hitRadius.
//  4. Leave forceAuthority = false for production.
//     Enable it for single-player / offline Editor testing.
// =============================================================================

/// <summary>
/// Maintains the authoritative score state (hammer score and mole exposure
/// score) and broadcasts <see cref="ScoreUpdateMessage"/> to all peers after
/// every scoring event.
///
/// Attach to GameManager/ScoreManager.
/// </summary>
public class ScoreManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Dependencies")]
    [Tooltip("Drag GameManager/MoleVisibilityTracker here.\n" +
             "ScoreManager subscribes to its OnMoleStateUpdate event.")]
    [SerializeField] private MoleVisibilityTracker moleTracker;

    [Tooltip("Drag the HammerHitAttemptSender on PlayerHammer here.\n" +
             "ScoreManager subscribes to its OnHitAttemptEvent event.")]
    [SerializeField] private HammerHitAttemptSender hammerSender;

    [Tooltip("Drag GameManager/HoleManager here.\n" +
             "Used to look up authoritative hole-centre positions for hit validation.")]
    [SerializeField] private HoleManager holeManager;

    [Header("Scoring")]
    [Tooltip("Points awarded to the Hammer player for each validated hit.")]
    [SerializeField] private int hammerPointsPerHit = 1;

    [Tooltip("Score points added to the Mole player per second of exposure.\n" +
             "Accumulated continuously in Update() while isVisible == true.\n" +
             "Set to 0 if you only want hit-count vs. survival-time comparison.")]
    [SerializeField] private int molePointsPerSecond = 1;

    [Header("Hit Validation")]
    [Tooltip("Maximum distance (metres) the reported hammer position may be\n" +
             "from the authoritative hole-centre transform and still count as\n" +
             "a valid hit.  Acts as a server-side bounding-sphere check.\n" +
             "Increase if valid hits are being incorrectly rejected.")]
    [SerializeField] private float hitRadius = 0.4f;

    [Header("Authority")]
    [Tooltip("Force this instance to act as score authority regardless of UUID.\n" +
             "Useful for single-player / offline Editor testing.\n" +
             "Must be false in a real 2-player session.")]
    [SerializeField] private bool forceAuthority = false;

    [Header("Broadcast")]
    [Tooltip("Interval (seconds) at which the authority re-broadcasts the current\n" +
             "score to keep late-joining or desynced peers in sync.")]
    [SerializeField] private float broadcastInterval = 2f;

    // -------------------------------------------------------------------------
    //  Public read-only score state
    // -------------------------------------------------------------------------

    /// <summary>Total points accumulated by the Hammer player.</summary>
    public int   HammerScore { get; private set; }

    /// <summary>Total mole exposure score (seconds × molePointsPerSecond).</summary>
    public int   MoleScore   { get; private set; }

    // -------------------------------------------------------------------------
    //  Events
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fired on ALL clients (authority and non-authority) whenever the score
    /// state changes.  Subscribe from UI scripts to update the HUD.
    /// </summary>
    public event Action<ScoreUpdateMessage> OnScoreUpdated;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private NetworkContext context;
    private bool           networkAvailable;
    private RoomClient     room;

    // Authoritative snapshot of the current mole state, updated from events.
    private MoleStateMessage currentMoleState;

    // Per-exposure hit guard — each visible pop may only be scored once.
    private bool currentExposureHit;
    private int  lastScoredExposureSequence = -1;

    // Exposure start timestamp for time-based mole scoring.
    private float exposureStartTime;

    // Periodic broadcast timer.
    private float broadcastTimer;

    // Cached UI score components.
    private moleScore   moleScoreUI;
    private hammerScore hammerScoreUI;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        // ── Network setup ─────────────────────────────────────────────────────
        room = FindFirstObjectByType<RoomClient>();

        try
        {
            context          = NetworkScene.Register(this);
            networkAvailable = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[ScoreManager] NetworkScene not found — " +
                             "networking disabled. Run via the lobby scene for a live " +
                             $"connection.\n{e.Message}");
        }

        // ── Auto-find dependencies if Inspector slots were not wired ──────────
        if (moleTracker == null)
        {
            moleTracker = FindFirstObjectByType<MoleVisibilityTracker>();
            if (moleTracker == null)
                Debug.LogWarning("[ScoreManager] MoleVisibilityTracker not found. " +
                                 "Drag it into the Inspector slot.", this);
        }

        if (hammerSender == null)
        {
            hammerSender = FindFirstObjectByType<HammerHitAttemptSender>();
            if (hammerSender == null)
                Debug.LogWarning("[ScoreManager] HammerHitAttemptSender not found. " +
                                 "Drag it into the Inspector slot.", this);
        }

        if (holeManager == null)
        {
            holeManager = FindFirstObjectByType<HoleManager>();
            if (holeManager == null)
                Debug.LogWarning("[ScoreManager] HoleManager not found. " +
                                 "Drag it into the Inspector slot.", this);
        }

        // ── Cache UI score components ─────────────────────────────────────────
        moleScoreUI   = FindFirstObjectByType<moleScore>();
        hammerScoreUI = FindFirstObjectByType<hammerScore>();

        // ── Wire HUD updates — fires on both authority and non-authority ──────
        // OnScoreUpdated is invoked by BroadcastScore() (authority) and
        // ProcessMessage() (non-authority), so both clients update their UI
        // through the same single path.
        OnScoreUpdated += msg =>
        {
            if (moleScoreUI   != null) moleScoreUI.updateText(msg.moleScore);
            if (hammerScoreUI != null) hammerScoreUI.updateText(msg.hammerScore);
        };

        // ── Subscribe to events ───────────────────────────────────────────────
        if (moleTracker  != null) moleTracker.OnMoleStateUpdate  += HandleMoleState;
        if (hammerSender != null) hammerSender.OnHitAttemptEvent += HandleHitAttempt;

        broadcastTimer = broadcastInterval;
    }

    private void OnDestroy()
    {
        // Always unsubscribe to prevent dangling references after scene unload.
        if (moleTracker  != null) moleTracker.OnMoleStateUpdate  -= HandleMoleState;
        if (hammerSender != null) hammerSender.OnHitAttemptEvent -= HandleHitAttempt;
    }

    private void Update()
    {
        if (!IsAuthority()) return;

        // ── Accumulate mole exposure score while visible ───────────────────────
        // This runs every frame so the mole score grows smoothly in real time.
        if (currentMoleState.isVisible)
        {
            if (moleScoreUI != null)
                MoleScore = moleScoreUI.addScore(molePointsPerSecond);
        }
        // ── Periodic re-broadcast ──────────────────────────────────────────────
        broadcastTimer -= Time.deltaTime;
        if (broadcastTimer <= 0f)
        {
            broadcastTimer = broadcastInterval;
            BroadcastScore();
        }
    }

    // -------------------------------------------------------------------------
    //  MoleStateMessage handler
    // -------------------------------------------------------------------------

    private void HandleMoleState(MoleStateMessage msg)
    {
        // Both authority and non-authority receive this event.  Only the
        // authority makes scoring decisions; non-authority stores the state
        // as a defensive measure in case it later becomes authority mid-session.
        bool wasPreviouslyVisible = currentMoleState.isVisible;
        bool newExposureBegun     = msg.exposureSequence != currentMoleState.exposureSequence
                                    && msg.isVisible;

        currentMoleState = msg;

        if (!IsAuthority()) return;

        // ── New exposure begins: reset per-exposure hit guard  ─────────────────
        if (newExposureBegun && msg.exposureSequence != lastScoredExposureSequence)
        {
            currentExposureHit = false;
            Debug.Log($"[ScoreManager] New mole exposure | " +
                      $"holeId={msg.activeHoleId} seq={msg.exposureSequence}");
        }

        // ── Transition: hidden → visible  ─────────────────────────────────────
        if (msg.isVisible && !wasPreviouslyVisible)
        {
            exposureStartTime  = Time.time;
            currentExposureHit = false;
            Debug.Log($"[ScoreManager] Mole became visible | " +
                      $"holeId={msg.activeHoleId} seq={msg.exposureSequence}");
        }

        // ── Transition: visible → hidden  ─────────────────────────────────────
        // Note: the continuous accumulation in Update() handles most of the
        // time tracking; this broadcast ensures the score is immediately synced
        // when the mole hides (rather than waiting for the next interval tick).
        if (!msg.isVisible && wasPreviouslyVisible)
        {
            Debug.Log($"[ScoreManager] Mole hidden | moleScore={MoleScore}");
            BroadcastScore();
        }
    }

    // -------------------------------------------------------------------------
    //  HitAttemptMessage handler
    // -------------------------------------------------------------------------

    private void HandleHitAttempt(HitAttemptMessage msg)
    {
        if (!IsAuthority()) return;

        // ── Validation 1: mole must currently be visible ──────────────────────
        if (!currentMoleState.isVisible)
        {
            Debug.Log($"[ScoreManager] Rejected — mole not visible | holeId={msg.holeId}");
            return;
        }

        // ── Validation 2: hole id must match the active mole hole  ────────────
        if (msg.holeId != currentMoleState.activeHoleId)
        {
            Debug.Log($"[ScoreManager] Rejected — wrong hole | " +
                      $"attempted={msg.holeId} active={currentMoleState.activeHoleId}");
            return;
        }

        // ── Validation 3: this exposure has not already been scored ───────────
        // Each time the mole pops up it may only be hit once.
        if (currentExposureHit)
        {
            Debug.Log($"[ScoreManager] Rejected — exposure already scored | " +
                      $"seq={currentMoleState.exposureSequence}");
            return;
        }

        // ── Validation 4: hammer position within radius of the hole centre ─────
        // The authority cross-checks the reported hammer position against the
        // authoritative hole-centre transform to guard against obviously false
        // or stale positions sent by a misbehaving client.
        if (holeManager != null)
        {
            if (holeManager.TryGetHitZoneTransform(msg.holeId, out Transform holeTransform))
            {
                float dist = Vector3.Distance(msg.hammerPosition, holeTransform.position);
                if (dist > hitRadius)
                {
                    Debug.Log($"[ScoreManager] Rejected — hammer out of radius | " +
                              $"dist={dist:F2} m max={hitRadius:F2} m holeId={msg.holeId}");
                    return;
                }
            }
            else
            {
                Debug.LogWarning($"[ScoreManager] No hole transform for holeId={msg.holeId} — " +
                                  "skipping radius check.  Wire all 5 holes in HoleManager.", this);
            }
        }

        // ── Valid hit — award hammer score and lock this exposure ──────────────
        HammerScore                    = (hammerScoreUI != null) ? hammerScoreUI.hit() : HammerScore + hammerPointsPerHit;
        currentExposureHit             = true;
        lastScoredExposureSequence     = currentMoleState.exposureSequence;

        if (moleScoreUI != null) moleScoreUI.hit();

        Debug.Log($"[ScoreManager] *** HIT SCORED *** hammerScore={HammerScore} | " +
                  $"holeId={msg.holeId} seq={currentMoleState.exposureSequence}");

        BroadcastScore();
    }

    // -------------------------------------------------------------------------
    //  Score broadcast
    // -------------------------------------------------------------------------

    private void BroadcastScore()
    {
        var update = new ScoreUpdateMessage
        {
            hammerScore  = HammerScore,
            moleScore    = MoleScore,
            activeHoleId = currentMoleState.activeHoleId,
            moleVisible  = currentMoleState.isVisible
        };

        // Fire the local C# event first so UI on the authority's machine updates
        // without waiting for a network round-trip.
        OnScoreUpdated?.Invoke(update);

        if (!networkAvailable) return;

        try
        {
            context.SendJson(update);
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[ScoreManager] SendJson failed: {e.Message}");
        }
    }

    // -------------------------------------------------------------------------
    //  Ubiq inbound — receives ScoreUpdateMessage from the authority
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by Ubiq when the authority ScoreManager broadcasts a score update.
    /// Non-authority peers use this to update their local HUD / display state.
    ///
    /// The authority's own scores are already maintained locally via
    /// <see cref="HandleMoleState"/> and <see cref="HandleHitAttempt"/>;
    /// it does not need to process its own broadcasts.
    /// </summary>
    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        // The authority owns the score source of truth locally — receiving its
        // own broadcast back would overwrite live state with a stale snapshot.
        if (IsAuthority()) return;

        var update = message.FromJson<ScoreUpdateMessage>();
        HammerScore = update.hammerScore;
        MoleScore   = update.moleScore;

        Debug.Log($"[ScoreManager] Score synced from authority | " +
                  $"hammer={HammerScore} mole={MoleScore}");

        OnScoreUpdated?.Invoke(update);
    }

    // -------------------------------------------------------------------------
    //  Authority determination
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns true if this client is the designated score authority.
    ///
    /// Authority is the peer with the lexicographically lowest UUID in the room.
    /// This mirrors the deterministic pattern used in <see cref="RoleManager"/>
    /// and requires no explicit negotiation message — both clients independently
    /// arrive at the same answer.
    ///
    /// <c>forceAuthority</c> overrides the check for offline / Editor testing.
    /// </summary>
    private bool IsAuthority()
    {
        if (forceAuthority) return true;
        if (room == null)   return true; // No RoomClient → standalone mode.

        var peers = room.Peers?.ToList();
        if (peers == null || peers.Count == 0) return true; // Alone in room.

        string lowestPeer = peers.Min(p => p.uuid);
        return string.Compare(room.Me.uuid, lowestPeer, StringComparison.Ordinal) <= 0;
    }

    // -------------------------------------------------------------------------
    //  Round result
    // -------------------------------------------------------------------------

    /// <summary>
    /// Returns "Hammer", "Mole", or "Draw" based on final scores.
    /// Call this from your round-end logic to determine and display the winner.
    /// </summary>
    public string GetWinner()
    {
        if (HammerScore > MoleScore) return "Hammer";
        if (MoleScore   > HammerScore) return "Mole";
        return "Draw";
    }
}
