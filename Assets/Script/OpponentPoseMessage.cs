using System;
using UnityEngine;

// =============================================================================
//  OpponentPoseMessage.cs
//  Shared message types for lightweight opponent pose syncing over Ubiq.
//
//  Both LocalRolePosePublisher and RemoteRolePoseReceiver depend on these types.
//  Keep this file in the same assembly (Assets/Script) as the other two.
// =============================================================================

/// <summary>
/// Identifies which role the remote player is playing.
/// Kept independent of RoleManager.Role so the networking layer has no
/// dependency on game-logic code.
/// </summary>
public enum RemoteRoleType
{
    Hammer = 0,
    Mole   = 1
}

/// <summary>
/// Minimal pose snapshot sent over the Ubiq network once per send tick.
///
/// Serialized by Unity's JsonUtility — all fields are primitive or built-in
/// Unity types, so serialization is automatic and allocation-free.
///
/// Payload breakdown (approximate JSON bytes):
///   role       →  1 byte  (int enum)
///   position   → ~45 bytes (three floats as text)
///   isVisible  →  4–5 bytes
///   sequence   →  1–5 bytes
///   timestamp  → ~18 bytes (double as text)
///   JSON delimiters ~= 30 bytes overhead
/// Total ≈ ~100–120 bytes per message.  At 15 Hz that is under 2 KB/s —
/// well within the budget of any shared multiplayer session.
/// </summary>
[Serializable]
public struct OpponentPoseMessage
{
    /// <summary>
    /// The role of the player who sent this message (not the recipient).
    /// Hammer → position is the right-hand controller world position.
    /// Mole   → position is the PlayerMole body-root world position.
    /// </summary>
    public RemoteRoleType role;

    /// <summary>
    /// World-space position of the tracked object for this role:
    ///   Hammer → XR Right Controller
    ///   Mole   → PlayerMole root (body position, not head)
    /// Rotation is intentionally omitted to keep the payload small.
    /// </summary>
    public Vector3 position;

    /// <summary>
    /// Visibility flag.
    /// Hammer: always true (the hammer is always shown when held).
    /// Mole:   true  = the player's eye is above the Mole box (exposed);
    ///         false = the player is hiding inside the box.
    ///
    /// The Mole explicitly sends false rather than simply stopping messages
    /// so the remote side can hide the avatar cleanly instead of leaving it
    /// frozen at the last visible position.
    /// </summary>
    public bool isVisible;

    /// <summary>
    /// Monotonically increasing counter. Incremented each time a message is
    /// sent. The receiver can use this to detect out-of-order or dropped
    /// messages if needed.
    /// </summary>
    public int sequence;

    /// <summary>
    /// Optional send timestamp in seconds (Time.unscaledTime cast to double).
    /// Useful for latency debugging. The receiver does not need to parse this
    /// field for normal gameplay.
    /// </summary>
    public double timestamp;
}
