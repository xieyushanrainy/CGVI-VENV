using System;
using UnityEngine;
using Ubiq.Messaging;

/// <summary>
/// Detects local hammer–hole hit-zone trigger events and sends
/// <see cref="HitAttemptMessage"/> over Ubiq for authority-side validation.
///
/// Attach to PlayerHammer (XR Origin / Right Controller / PlayerHammer).
/// </summary>
public class HammerHitAttemptSender : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Swing Speed")]
    [Tooltip("Minimum metres-per-second hammer speed required for a hit attempt\n" +
             "to be sent.  Prevents slow, accidental grazes from registering.\n" +
             "Recommended: 1.0–2.0 m/s for XR hammer swings.")]
    [SerializeField] private float minSwingSpeed = 1.0f;

    [Header("Spam Prevention")]
    [Tooltip("Minimum seconds that must elapse between consecutive hit attempts.\n" +
             "Prevents the same swing from firing multiple messages as the hammer\n" +
             "passes through the trigger volume.  Recommended: 0.2–0.5 s.")]
    [SerializeField] private float hitCooldown = 0.3f;

    // -------------------------------------------------------------------------
    //  Public API
    // -------------------------------------------------------------------------

    /// <summary>
    /// Fired on BOTH clients whenever a hit attempt is relevant to this machine:
    /// <list type="bullet">
    ///   <item>Hammer player's machine: fired when the local swing is sent.</item>
    ///   <item>Mole player's machine: fired when a remote attempt arrives via
    ///   <see cref="ProcessMessage"/>.</item>
    /// </list>
    /// <see cref="ScoreManager"/> subscribes to this event so it receives
    /// hit attempts via one consistent path regardless of role.
    /// </summary>
    public event Action<HitAttemptMessage> OnHitAttemptEvent;

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private NetworkContext context;
    private bool           networkAvailable;

    // Swing speed is computed each Update from frame-to-frame position delta.
    private Vector3 prevPosition;
    private float   currentSwingSpeed;

    // Cooldown tracking.
    private float lastHitTime = -999f;

    // Monotonically increasing counter for deduplication on the receiver side.
    private int hitSequence;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        prevPosition = transform.position;

        try
        {
            context          = NetworkScene.Register(this);
            networkAvailable = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[HammerHitAttemptSender] NetworkScene not found — " +
                             "networking disabled. Run via the lobby scene for a live " +
                             $"connection.\n{e.Message}");
        }
    }

    private void Update()
    {
        // Track hammer velocity each frame so it is available the moment
        // OnTriggerEnter fires (which may occur in the same frame).
        float dt = Time.deltaTime;
        if (dt > 0.1f)
            currentSwingSpeed = Vector3.Distance(transform.position, prevPosition) / dt;
        prevPosition = transform.position;
    }

    // -------------------------------------------------------------------------
    //  Trigger detection
    // -------------------------------------------------------------------------

    private void OnTriggerEnter(Collider other)
    {
        // ── Cooldown guard ────────────────────────────────────────────────────
        // Prevents the same downswing from sending multiple messages as the
        // hammer travels through the trigger volume.
        if (Time.time < lastHitTime + hitCooldown) return;

        // ── Identify the hole by HoleIdMarker ─────────────────────────────────
        // Check the collider's own GameObject and then its parent, so the marker
        // can sit on the HitZone object itself or on the parent Hole_X object.
        var marker = other.GetComponent<HoleIdMarker>()
                  ?? other.GetComponentInParent<HoleIdMarker>();
        if (marker == null) return; // Not a hole hit zone — ignore.

        SendHitAttempt(marker.HoleId);
    }

    // -------------------------------------------------------------------------
    //  Message send
    // -------------------------------------------------------------------------

    private void SendHitAttempt(int holeId)
    {
        var msg = new HitAttemptMessage
        {
            holeId         = holeId,
            hammerPosition = transform.position,
            hitSequence    = hitSequence++
        };

        // Fire locally first so ScoreManager on THIS machine is notified
        // immediately — important when the Hammer player is the authority.
        OnHitAttemptEvent?.Invoke(msg);

        if (networkAvailable)
        {
            try
            {
                context.SendJson(msg);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[HammerHitAttemptSender] SendJson failed: {e.Message}");
            }
        }

        lastHitTime = Time.time;
    }

    // -------------------------------------------------------------------------
    //  Ubiq inbound — receives the REMOTE Hammer player's hit attempts
    // -------------------------------------------------------------------------

    /// <summary>
    /// Called by Ubiq when the remote Hammer player's HammerHitAttemptSender
    /// sends a hit attempt message.  Fires <see cref="OnHitAttemptEvent"/> so
    /// <see cref="ScoreManager"/> on this machine (if it is the authority)
    /// can validate and score the attempt.
    /// </summary>
    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        var msg = message.FromJson<HitAttemptMessage>();
        OnHitAttemptEvent?.Invoke(msg);
    }
}
