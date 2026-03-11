using System;

// =============================================================================
//  ScoreUpdateMessage.cs
//
//  Ubiq-compatible message broadcast by the authority ScoreManager to keep
//  all peers synchronised with the current score state.
//
//  This message flows: authority ScoreManager --> all peers.
//  Non-authority peers must only read this message and update their display;
//  they must NOT independently modify or recalculate any score values.
// =============================================================================

/// <summary>
/// Full score-state snapshot broadcast by the authority <see cref="ScoreManager"/>
/// after every scoring event and periodically thereafter.
///
/// All clients receive this via <c>ScoreManager.ProcessMessage</c> and update
/// their local score display (UI, HUD, etc.) accordingly.
/// </summary>
[Serializable]
public struct ScoreUpdateMessage
{
    /// <summary>Number of valid hits scored by the Hammer player this round.</summary>
    public int hammerScore;

    /// <summary>
    /// Accumulated mole exposure score this round (seconds × molePointsPerSecond).
    /// Tracks how long / bravely the mole player has been exposed.
    /// </summary>
    public int moleScore;

    /// <summary>
    /// Active hole id (0–4) at the time this message was sent.
    /// -1 if no hole is currently active.
    /// </summary>
    public int activeHoleId;

    /// <summary>Whether the mole was visible at the time this message was sent.</summary>
    public bool moleVisible;
}
