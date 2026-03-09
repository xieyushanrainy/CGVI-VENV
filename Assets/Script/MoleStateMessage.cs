using System;
using UnityEngine;

// =============================================================================
//  MoleStateMessage.cs
//
//  Ubiq-compatible message type for broadcasting mole visibility state.
//  Sent by MoleVisibilityTracker and consumed by ScoreManager via C# event.
//
//  All fields are Unity-serializable primitives or built-in types so
//  JsonUtility handles them without custom converters.
//  Approximate JSON payload: ~90–110 bytes.  At 15 Hz ≈ 1.5 KB/s.
// =============================================================================

/// <summary>
/// Snapshot of the mole player's current visibility and active hole state.
/// Sent over Ubiq by <see cref="MoleVisibilityTracker"/> whenever the state
/// changes meaningfully.
///
/// <see cref="ScoreManager"/> (authority) uses this to track authoritative
/// mole exposure and validate incoming <see cref="HitAttemptMessage"/>s.
/// </summary>
[Serializable]
public struct MoleStateMessage
{
    /// <summary>
    /// True when the mole player's XR camera (eye/head) is above the mole-box
    /// top threshold — meaning the mole is exposed and hittable.
    /// </summary>
    public bool isVisible;

    /// <summary>
    /// Index of the hole the mole is currently occupying (0–4).
    /// -1 if the mole is not committed to any hole or the value is not yet set.
    /// </summary>
    public int activeHoleId;

    /// <summary>
    /// World-space body position of the mole computed as:
    ///   bodyPos = xrCamera.position − (0, headToPivotOffsetY, 0)
    /// Used by ScoreManager for additional proximity validation on hit attempts.
    /// </summary>
    public Vector3 molePosition;

    /// <summary>
    /// Monotonically increasing counter, incremented each time the mole
    /// transitions from hidden to visible (i.e. each new exposure begins).
    ///
    /// ScoreManager uses this to ensure each individual exposure is scored
    /// at most once even when multiple hit attempts arrive for the same pop.
    /// </summary>
    public int exposureSequence;
}
