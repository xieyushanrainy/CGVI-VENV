using System;
using UnityEngine;

// =============================================================================
//  HitAttemptMessage.cs
//
//  Ubiq-compatible message type for hammer hit attempts.
//  Sent by HammerHitAttemptSender and validated by ScoreManager (authority only).
//
//  The authority NEVER trusts this message blindly.  It cross-checks:
//    1. Is the mole currently visible?
//    2. Does holeId match the authoritative activeHoleId?
//    3. Is hammerPosition within hitRadius of the authoritative hole centre?
//    4. Has this exposure already been scored?
//  All four conditions must pass before any score is awarded.
// =============================================================================

/// <summary>
/// Represents a hammer swing that entered a hole hit zone with sufficient speed.
/// Sent by <see cref="HammerHitAttemptSender"/> over Ubiq for authority-side
/// validation by <see cref="ScoreManager"/>.
///
/// This message alone does NOT award score — it is a claim that must be
/// validated by the authority.
/// </summary>
[Serializable]
public struct HitAttemptMessage
{
    /// <summary>
    /// The hole index (0–4) whose trigger collider was entered.
    /// Must match the authority's current activeHoleId for the hit to count.
    /// </summary>
    public int holeId;

    /// <summary>
    /// World-space position of the hammer at the moment the trigger fired.
    /// Used by the authority for secondary proximity validation against
    /// the authoritative hole-centre transform.
    /// </summary>
    public Vector3 hammerPosition;

    /// <summary>
    /// Monotonically increasing counter per sender.
    /// Allows ScoreManager to detect and discard duplicate messages if needed.
    /// </summary>
    public int hitSequence;
}
