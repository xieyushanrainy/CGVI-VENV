using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit.Inputs.Haptics;

// =============================================================================
//  HapticManager.cs
//
//  Centralised controller haptic feedback for the 2-player Whack-a-Mole game.
//
//  HAPTIC DESIGN
//  -------------
//  Hammer player (right controller only — the swinging hand):
//    Hit confirmed : sharp heavy pulse  — amplitude 1.0,  duration 0.15 s
//                   Communicates solid impact; reward for a clean swing.
//    Miss          : brief soft buzz    — amplitude 0.25, duration 0.06 s
//                   Subtle cue that the swing was registered but nothing hit.
//
//  Mole player (both controllers — simulates full-body damage):
//    Being hit     : sustained strong pulse — amplitude 0.9, duration 0.4 s
//                   Longer duration makes the "damage" feel weighty and
//                   distinct from any incidental controller feedback.
//
//  USAGE
//  -----
//  Call from ScoreManager (already wired via soundManagerRef pattern):
//    Hammer hit   → hapticManager?.PlayHammerHaptic(true);
//    Hammer miss  → hapticManager?.PlayHammerHaptic(false);
//    Mole hit     → hapticManager?.PlayMoleHitHaptic();   (called in ProcessMessage)
//
//  SETUP
//  -----
//  1. Attach this component to GameManager (same object as ScoreManager).
//  2. Drag the HapticImpulsePlayer on XR Origin / Right Controller → rightHaptics.
//  3. Drag the HapticImpulsePlayer on XR Origin / Left  Controller → leftHaptics.
//  4. Drag this component into ScoreManager's hapticManager Inspector slot.
//  Auto-find fallback searches for HapticImpulsePlayer components by controller
//  GameObject name if the slots are left empty.
// =============================================================================

/// <summary>
/// Provides tunable haptic impulses for the Hammer and Mole players.
/// Attach to GameManager.  Wire into <see cref="ScoreManager.hapticManager"/>.
/// </summary>
public class HapticManager : MonoBehaviour
{
    // =========================================================================
    //  Inspector — Controller references
    // =========================================================================

    [Header("Controller References")]
    [Tooltip("HapticImpulsePlayer on XR Origin / Right Controller.\n" +
             "Auto-found by name if left empty.")]
    public HapticImpulsePlayer rightHaptics;

    [Tooltip("HapticImpulsePlayer on XR Origin / Left Controller.\n" +
             "Auto-found by name if left empty.")]
    public HapticImpulsePlayer leftHaptics;

    // =========================================================================
    //  Inspector — Hammer haptic tuning
    // =========================================================================

    [Header("Hammer — Hit Confirmed")]
    [Tooltip("Amplitude (0–1) when hammer confirms a valid hit.\n" +
             "1.0 = maximum vibration motor strength.")]
    [SerializeField] [Range(0f, 1f)] private float hitAmplitude    = 1.0f;

    [Tooltip("Duration in seconds of the confirmed-hit haptic pulse.\n" +
             "0.15 s gives a sharp, impactful thud without feeling too long.")]
    [SerializeField]                 private float hitDuration      = 0.15f;

    [Header("Hammer — Miss")]
    [Tooltip("Amplitude (0–1) when a hammer swing is rejected (miss).\n" +
             "Keep low so it feels clearly different from a confirmed hit.")]
    [SerializeField] [Range(0f, 1f)] private float missAmplitude   = 0.25f;

    [Tooltip("Duration in seconds of the miss haptic pulse.\n" +
             "0.06 s is a very brief buzz — just enough to notice.")]
    [SerializeField]                 private float missDuration     = 0.06f;

    // =========================================================================
    //  Inspector — Mole haptic tuning
    // =========================================================================

    [Header("Mole — Being Hit")]
    [Tooltip("Amplitude (0–1) when the mole player is hit.\n" +
             "Both controllers vibrate simultaneously for a full-body effect.")]
    [SerializeField] [Range(0f, 1f)] private float moleHitAmplitude = 0.9f;

    [Tooltip("Duration in seconds of the mole-hit haptic.\n" +
             "0.4 s is noticeably longer than the hammer pulses — feels like damage.")]
    [SerializeField]                 private float moleHitDuration  = 0.4f;

    // =========================================================================
    //  Unity lifecycle
    // =========================================================================

    private void Start()
    {
        if (rightHaptics == null || leftHaptics == null)
            AutoFindControllers();

        if (rightHaptics == null)
            Debug.LogWarning("[HapticManager] Right HapticImpulsePlayer not found — " +
                             "Hammer haptics will be silent. Assign it in the Inspector.", this);

        if (leftHaptics == null)
            Debug.LogWarning("[HapticManager] Left HapticImpulsePlayer not found — " +
                             "Mole left-hand haptic will be silent. Assign it in the Inspector.", this);
    }

    // =========================================================================
    //  Public API — Hammer player
    // =========================================================================

    /// <summary>
    /// Plays a haptic pulse on the Hammer player's right controller.
    /// <para><c>isHit = true</c>  → confirmed hit pulse (strong, sharp).</para>
    /// <para><c>isHit = false</c> → miss pulse (weak, brief).</para>
    /// Call on the authority (Hammer) machine only.
    /// </summary>
    public void PlayHammerHaptic(bool isHit)
    {
        if (rightHaptics == null) return;

        if (isHit)
            rightHaptics.SendHapticImpulse(hitAmplitude, hitDuration);
        else
            rightHaptics.SendHapticImpulse(missAmplitude, missDuration);
    }

    // =========================================================================
    //  Public API — Mole player
    // =========================================================================

    /// <summary>
    /// Vibrates both controllers on the Mole player's machine to simulate a
    /// full-body hit impact.
    /// Call on the non-authority (Mole) machine when a confirmed hit arrives
    /// via <see cref="ScoreManager.ProcessMessage"/>.
    /// </summary>
    public void PlayMoleHitHaptic()
    {
        rightHaptics?.SendHapticImpulse(moleHitAmplitude, moleHitDuration);
        leftHaptics?.SendHapticImpulse(moleHitAmplitude, moleHitDuration);
    }

    // =========================================================================
    //  Private helpers
    // =========================================================================

    private void AutoFindControllers()
    {
        var players = FindObjectsByType<HapticImpulsePlayer>(FindObjectsSortMode.None);
        foreach (var p in players)
        {
            string n = p.gameObject.name;
            if (rightHaptics == null &&
                (n.IndexOf("right", System.StringComparison.OrdinalIgnoreCase) >= 0))
                rightHaptics = p;

            if (leftHaptics == null &&
                (n.IndexOf("left", System.StringComparison.OrdinalIgnoreCase) >= 0))
                leftHaptics = p;

            if (rightHaptics != null && leftHaptics != null) break;
        }
    }
}
