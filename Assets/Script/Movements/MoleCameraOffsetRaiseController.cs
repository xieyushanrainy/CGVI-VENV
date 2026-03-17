using UnityEngine;

// =============================================================================
//  MoleCameraOffsetRaiseController.cs
//
//  Replaces Raiser.cs with a two-layer height controller for the mole player:
//
//    Layer 1 — baseRaiseAmount  (0–1):  driven by controller Y movement while
//              the trigger is held.  This is the player's expressed intent.
//
//    Layer 2 — hitPenaltyAmount (0–1):  set to 1.0 on a confirmed hit, then
//              linearly fades to 0 over recoverDuration seconds.
//
//  FINAL HEIGHT FORMULA
//  --------------------
//    finalRaiseAmount = Clamp01(baseRaiseAmount - hitPenaltyAmount)
//
//  WHY SUBTRACTION, NOT A FULL LOCK-OUT:
//    Using subtraction (rather than forcing finalRaiseAmount = 0 for a fixed
//    duration) preserves player agency while still enforcing the hit reaction.
//    Immediately after a hit hitPenaltyAmount = 1, so even if the player
//    holds their controller at full height (base = 1), the result is 0 and the
//    mole is underground.  As the penalty fades over recoverDuration, the
//    player's actual input gradually "wins" again — they only need to keep the
//    controller raised and the mole re-emerges naturally.  If the player lowers
//    their controller while recovering, base stays low and the mole stays
//    hidden even after full recovery.  This makes the hit reaction feel
//    physical without locking the player into a passive wait state.
//
//  WHY CAMERA OFFSET, NOT MAIN CAMERA:
//    Camera Offset is the spatial root of the XR rig (parent of Main Camera
//    and both controller tracked poses).  Moving it shifts the entire stereo
//    view origin as a unit, preserving the correct eye separation and the
//    physical relationship between the tracked hands and the rendered world.
//    Moving Main Camera directly would shift only one eye's render, break
//    XR stereo alignment, and cause nausea-inducing hand / world mismatches.
//
//  REMOTE HAMMER PLAYER SEES THE EFFECT AUTOMATICALLY:
//    LocalRolePosePublisher already runs every frame, reading IsVisible from
//    MoleVisibilityTracker and publishing the mole body position.  Once this
//    controller drives Camera Offset below the hole, IsVisible → false and
//    the publisher broadcasts that state.  The hammer player's
//    RemoteRolePoseReceiver hides OpponentMole.  As recovery proceeds and
//    finalRaiseAmount climbs back above visibleThreshold, IsVisible flips
//    true again and OpponentMole reappears — no extra network messages needed.
//    Note: this controller exposes IsVisible directly so LocalRolePosePublisher
//    can optionally read it instead of (or alongside) MoleVisibilityTracker.
//
//  SETUP
//  -----
//  1. Keep Raiser.cs on the mole player GameObject — this controller drives it.
//  2. Attach this script to the same GameObject (e.g. GameManager or PlayerMole).
//  3. Assign Inspector fields:
//       cameraOffsetTransform  →  XR Origin / Camera Offset
//       raiser                 →  the Raiser component on this (or another) GameObject
//                                 (its controller, triggerAction, sensitivity, yMin and
//                                  yMax are reused as-is; its standalone Update is
//                                  disabled at runtime so only this controller drives
//                                  the camera, letting the penalty layer sit on top)
//       scoreManager           →  GameManager / ScoreManager  (auto-found if blank)
//  4. Tune visibleThreshold and recoverDuration here in the Inspector.
//     Tune sensitivity, yMin (hidden height) and yMax (raised height) on Raiser.
//  5. Leave isActive = true.  Set false to suppress movement before roles
//     are assigned (the script also self-disables if the local role is not Mole).
// =============================================================================

/// <summary>
/// Two-layer mole emergence controller for the mole player's XR rig.
///
/// Layer 1 (<see cref="BaseRaiseAmount"/>) is driven by <see cref="Raiser.ComputeNewY"/>.
/// Layer 2 (<see cref="hitPenaltyAmount"/>) is set by <see cref="ApplyHitReaction"/>
/// and fades over <see cref="recoverDuration"/> seconds.
///
/// <see cref="FinalRaiseAmount"/> = Clamp01(base − penalty) drives Camera Offset
/// between <see cref="Raiser.yMin"/> and <see cref="Raiser.yMax"/>.
/// </summary>
public class MoleCameraOffsetRaiseController : MonoBehaviour
{
    // =========================================================================
    //  Inspector — References
    // =========================================================================

    [Header("References")]
    [Tooltip("XR Origin / Camera Offset — the transform that is moved to raise " +
             "and lower the mole player's viewpoint.\n\n" +
             "IMPORTANT: assign Camera Offset here, NOT Main Camera.  Moving " +
             "Camera Offset shifts the entire stereo view origin correctly.")]
    [SerializeField] private Transform cameraOffsetTransform;

    [Tooltip("The Raiser component that owns the controller reference, trigger action, " +
             "sensitivity, yMin and yMax.\n\n" +
             "This controller disables Raiser's standalone Update at runtime so only " +
             "MoleCameraOffsetRaiseController drives the camera, letting the hit-penalty " +
             "layer be applied on top of Raiser's well-tuned input logic.")]
    [SerializeField] private Raiser raiser;

    // =========================================================================
    //  Inspector — Raise Settings
    // =========================================================================

    [Header("Raise Settings")]
    [Tooltip("finalRaiseAmount must be at or above this threshold for IsVisible " +
             "to return true.  Prevents the 'partially peeking' state from " +
             "counting as an exposed target.\n" +
             "Range: 0 (any emergence counts) – 1 (must be fully raised).")]
    [SerializeField] [Range(0f, 1f)] private float visibleThreshold = 0.15f;

    [Tooltip("When true, Camera Offset smoothly lerps to the target height each " +
             "frame.  Reduces visual jitter on low-frequency controller updates.")]
    [SerializeField] private bool useSmoothing = true;

    [Tooltip("Lerp speed toward target Camera Offset height (units per second).\n" +
             "Only used when useSmoothing is true.\n" +
             "Recommended: 6 – 12.")]
    [SerializeField] private float smoothSpeed = 8f;

    // =========================================================================
    //  Inspector — Hit Recovery
    // =========================================================================

    [Header("Hit Recovery")]
    [Tooltip("How long (seconds) the hit penalty takes to fully fade after " +
             "ApplyHitReaction() is called.\n\n" +
             "Set this to match the damageEffect recoverDuration (default 2 s) " +
             "so the red screen flash and the mole re-emergence finish together.")]
    [SerializeField] private float recoverDuration = 2f;

    [Tooltip("Normalised raise position [0\u20131] the mole's base height is reset to on a hit.\n\n" +
             "0 = fully underground (yMin), 1 = fully raised (yMax).\n" +
             "After recovery completes the mole rests here instead of returning to its\n" +
             "pre-hit height.  The player can still raise above this by holding the trigger.")]
    [SerializeField] [Range(0f, 1f)] private float postHitReturnRaiseAmount = 0f;

    // =========================================================================
    //  Inspector — Score Integration
    // =========================================================================

    [Header("Score Integration")]
    [Tooltip("ScoreManager in the scene.\n" +
             "This controller subscribes to OnScoreUpdated and calls " +
             "ApplyHitReaction() automatically when hammerScore increases.\n" +
             "Left blank → auto-found via FindFirstObjectByType at Start.")]
    [SerializeField] private ScoreManager scoreManager;

    // =========================================================================
    //  Inspector — Activation
    // =========================================================================

    [Header("Activation")]
    [Tooltip("Set false before roles are assigned to suppress all movement.\n" +
             "The component also self-disables at Start if the local role is " +
             "not Mole, so this flag only needs manual intervention in the Editor.")]
    [SerializeField] private bool isActive = true;

    // =========================================================================
    //  Public Properties
    // =========================================================================

    /// <summary>
    /// Raw raise amount driven by controller input, in [0, 1].
    /// Represents how high the player intends to emerge: 0 = fully hidden,
    /// 1 = fully raised.  Unaffected by hit penalty.
    /// </summary>
    public float BaseRaiseAmount  { get; private set; }

    /// <summary>
    /// Effective raise amount after hit penalty is applied.
    ///
    ///   finalRaiseAmount = Clamp01( baseRaiseAmount - hitPenaltyAmount )
    ///
    /// This drives Camera Offset height and <see cref="IsVisible"/>.
    /// During recovery hitPenaltyAmount fades 1 → 0, so finalRaiseAmount
    /// gradually opens up again — but only as fast as the penalty allows.
    /// </summary>
    public float FinalRaiseAmount { get; private set; }

    /// <summary>
    /// True when <see cref="FinalRaiseAmount"/> is at or above
    /// <see cref="visibleThreshold"/>.
    ///
    /// Consumed by <see cref="LocalRolePosePublisher"/> (or
    /// <see cref="MoleVisibilityTracker"/>) to broadcast the isVisible flag so
    /// the hammer player's OpponentMole hides/shows automatically — the same
    /// effect the mole player experiences locally.
    /// </summary>
    public bool IsVisible => FinalRaiseAmount >= visibleThreshold;

    /// <summary>True while the hit penalty is fading (between 0 and recoverDuration).</summary>
    public bool IsRecovering => isRecovering;

    // =========================================================================
    //  Private state
    // =========================================================================

    // ── Hit recovery ──────────────────────────────────────────────────────────
    private float hitPenaltyAmount;   // 1.0 on hit, fades linearly to 0.0
    private float recoverTimer;
    private bool  isRecovering;

    // ── Base height tracking (driven by Raiser.ComputeNewY) ──────────────────
    private float baseY;   // desired camera-offset Y before penalty, in [raiser.yMin, raiser.yMax]

    // ── Score integration ─────────────────────────────────────────────────────
    private int lastHammerScore;

    // =========================================================================
    //  Unity lifecycle
    // =========================================================================

    private void Start()
    {
        // Self-disable for the hammer player — this controller is mole-only.
        if (GameData.LocalRole != RoleManager.Role.Mole)
        {
            Debug.Log("[MoleCameraOffsetRaiseController] Local role is not Mole — disabling.", this);
            enabled = false;
            return;
        }

        // ── Delegate to Raiser for input handling ─────────────────────────────
        // Raiser owns the controller reference, trigger action, sensitivity, and
        // yMin / yMax.  Disable its standalone Update here so only this controller
        // moves the camera, letting the hit-penalty layer sit cleanly on top.
        if (raiser != null)
        {
            raiser.enabled = false;
            baseY = raiser.yMin;
        }
        else
        {
            Debug.LogWarning("[MoleCameraOffsetRaiseController] Raiser is not assigned — " +
                             "controller-driven raising will not work.", this);
        }

        // ── Wire up hit-reaction to ScoreManager events ───────────────────────
        // This mirrors exactly how damageEffect.cs subscribes to OnScoreUpdated,
        // ensuring both the red flash and the camera sink happen in the same frame
        // when the authoritative hammer score increments.
        if (scoreManager == null)
            scoreManager = FindFirstObjectByType<ScoreManager>();

        if (scoreManager != null)
            scoreManager.OnScoreUpdated += HandleScoreUpdated;
        else
            Debug.LogWarning("[MoleCameraOffsetRaiseController] ScoreManager not found — " +
                             "hit reaction must be triggered manually via ApplyHitReaction() " +
                             "or through MoleHitReceiver.", this);

        ValidateReferences();
    }

    private void OnDestroy()
    {
        if (scoreManager != null)
            scoreManager.OnScoreUpdated -= HandleScoreUpdated;
    }

    private void Update()
    {
        if (!isActive) return;

        // ── Step 1: Accumulate baseRaiseAmount from controller input ──────────
        ComputeBaseRaiseAmount();

        // ── Step 2: Advance the hit penalty fade ─────────────────────────────
        AdvanceRecovery();

        // ── Step 3: Combine input and penalty into the final raise value ──────
        //
        // DESIGN: subtraction means a hit immediately floors the mole (penalty=1
        // overrides any base value), while preserving the player's control signal
        // untouched.  As penalty fades, the player's already-held position
        // naturally lifts the mole again — no extra "hold up" gesture required.
        FinalRaiseAmount = Mathf.Clamp01(BaseRaiseAmount - hitPenaltyAmount);

        // ── Step 4: Move Camera Offset to the interpolated height ─────────────
        ApplyCameraOffsetPosition();
    }

    // =========================================================================
    //  Public API
    // =========================================================================

    /// <summary>
    /// Triggers the hit reaction: forces <see cref="hitPenaltyAmount"/> to 1,
    /// causing <see cref="FinalRaiseAmount"/> to collapse to 0 immediately.
    ///
    /// Over the next <see cref="recoverDuration"/> seconds the penalty linearly
    /// fades to 0.  The mole re-emerges automatically if the player is still
    /// holding the controller raised — player agency is never fully removed.
    ///
    /// Can be called from:
    ///   1. ScoreManager.OnScoreUpdated subscriber (wired automatically in Start).
    ///   2. <see cref="MoleHitReceiver"/> via explicit Ubiq message (optional).
    ///   3. Any editor test button or game-flow script.
    ///
    /// Safe to call while already recovering — restarts the countdown.
    /// </summary>
    public void ApplyHitReaction()
    {
        isRecovering     = true;
        recoverTimer     = 0f;
        hitPenaltyAmount = 1f;

        // Reset the accumulated base position so the mole re-emerges at the
        // configured lower height after recovery, not its pre-hit position.
        if (raiser != null)
            baseY = Mathf.Lerp(raiser.yMin, raiser.yMax, postHitReturnRaiseAmount);
        BaseRaiseAmount = postHitReturnRaiseAmount;

        Debug.Log("[MoleCameraOffsetRaiseController] Hit reaction applied — mole sinking.", this);
    }

    // =========================================================================
    //  Private helpers
    // =========================================================================

    /// <summary>
    /// Delegates raise-amount computation to <see cref="Raiser.ComputeNewY"/>.
    ///
    /// <see cref="baseY"/> tracks the intended camera-offset Y (without penalty)
    /// in Raiser's [yMin, yMax] space.  We pass <c>baseY</c> — not the actual
    /// camera offset Y — so the hit-penalty shift does not corrupt Raiser's
    /// internal delta tracking.  The result is normalised to [0, 1] for the
    /// penalty layer to operate in a controller-agnostic space.
    /// </summary>
    private void ComputeBaseRaiseAmount()
    {
        if (raiser == null) return;

        baseY = raiser.ComputeNewY(baseY);

        float range = raiser.yMax - raiser.yMin;
        BaseRaiseAmount = range > 0f
            ? Mathf.Clamp01((baseY - raiser.yMin) / range)
            : 0f;
    }

    /// <summary>
    /// Linearly fades <see cref="hitPenaltyAmount"/> from 1 → 0 over
    /// <see cref="recoverDuration"/> seconds after a hit.
    /// </summary>
    private void AdvanceRecovery()
    {
        if (!isRecovering) return;

        recoverTimer += Time.deltaTime;

        // Linear interpolation t = [0, 1] over recoverDuration.
        float t          = Mathf.Clamp01(recoverTimer / recoverDuration);
        hitPenaltyAmount = 1f - t;   // 1.0 at hit, 0.0 at full recovery

        if (recoverTimer >= recoverDuration)
        {
            hitPenaltyAmount = 0f;
            isRecovering     = false;
            Debug.Log("[MoleCameraOffsetRaiseController] Recovery complete — full emergence restored.", this);
        }
    }

    /// <summary>
    /// Moves Camera Offset's local Y between <see cref="Raiser.yMin"/> and
    /// <see cref="Raiser.yMax"/> according to <see cref="FinalRaiseAmount"/>.
    ///
    /// Only the Y axis is modified; X and Z stay at their current local values
    /// so horizontal XR rig positioning is never disturbed.
    /// </summary>
    private void ApplyCameraOffsetPosition()
    {
        if (cameraOffsetTransform == null || raiser == null)
            return;

        float targetY = Mathf.Lerp(raiser.yMin, raiser.yMax, FinalRaiseAmount);

        Vector3 pos = cameraOffsetTransform.localPosition;

        pos.y = useSmoothing
            ? Mathf.Lerp(pos.y, targetY, smoothSpeed * Time.deltaTime)
            : targetY;

        cameraOffsetTransform.localPosition = pos;
    }

    /// <summary>
    /// Subscribes to <see cref="ScoreManager.OnScoreUpdated"/>.
    /// Triggers <see cref="ApplyHitReaction"/> whenever hammerScore increases,
    /// in sync with damageEffect.FlashDamage() which uses the same event.
    /// </summary>
    private void HandleScoreUpdated(ScoreUpdateMessage msg)
    {
        if (msg.hammerScore > lastHammerScore)
            ApplyHitReaction();

        lastHammerScore = msg.hammerScore;
    }

    private void ValidateReferences()
    {
        if (cameraOffsetTransform == null)
            Debug.LogWarning("[MoleCameraOffsetRaiseController] cameraOffsetTransform is not assigned. " +
                             "Camera movement is disabled.", this);
        if (raiser == null)
            Debug.LogWarning("[MoleCameraOffsetRaiseController] raiser is not assigned. " +
                             "Controller-driven raising is disabled.", this);
    }
}
