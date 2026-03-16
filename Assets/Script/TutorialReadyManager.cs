using System;
using System.Collections.Generic;
using UnityEngine;
using Ubiq.Messaging;
using Ubiq.Rooms;

// =============================================================================
//  TutorialReadyManager.cs
//
//  Manages the tutorial / exploration phase inside the arena scene.
//  Players are free to explore the arena until BOTH press Ready.
//  Once both are ready the OnBothReady event fires — subscribe to it to
//  start gameplay (enable HoleManager, begin a countdown, etc.).
//
//  Follows the same Ubiq peer-to-peer messaging pattern as RoleManager.
//
//  SETUP
//  -----
//  1. Attach this component to a persistent GameObject in the arena scene
//     (e.g. create an empty "TutorialManager" GameObject).
//  2. Ensure a NetworkScene (Ubiq) is present in the scene.
//  3. Connect TutorialReadyUI to this manager via Inspector or
//     FindFirstObjectByType (auto-wired in TutorialReadyUI.Start()).
//  4. Subscribe to OnBothReady to begin actual gameplay:
//       tutorialReadyManager.OnBothReady += StartGame;
// =============================================================================

/// <summary>
/// Network-aware manager for the tutorial exploration phase.
/// Tracks local and opponent ready states via Ubiq and fires
/// <see cref="OnBothReady"/> once both players confirm.
/// </summary>
public class TutorialReadyManager : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Public state
    // -------------------------------------------------------------------------

    /// <summary>True after the local player has pressed Ready.</summary>
    public bool IsLocalReady    { get; private set; }

    /// <summary>True after a ready message is received from the opponent.</summary>
    public bool IsOpponentReady { get; private set; }

    /// <summary>True once OnBothReady has fired (latched — never reverts).</summary>
    public bool BothReady       { get; private set; }

    // -------------------------------------------------------------------------
    //  Events
    // -------------------------------------------------------------------------

    /// <summary>Fires whenever the local ready state changes.</summary>
    public event Action<bool> OnLocalReadyChanged;

    /// <summary>Fires whenever the opponent ready state changes.</summary>
    public event Action<bool> OnOpponentReadyChanged;

    /// <summary>Fires once when both players are ready. Start gameplay here.</summary>
    public event Action OnBothReady;

    // -------------------------------------------------------------------------
    //  Ubiq internals
    // -------------------------------------------------------------------------

    private NetworkContext context;
    private RoomClient     room;

    // type: "ready"   → senderUuid populated
    // type: "unready" → senderUuid populated
    [Serializable]
    private struct ReadyMessage
    {
        public string type;
        public string senderUuid;
    }

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    /// <summary>True when Ubiq networking is available in this scene.</summary>
    public bool IsNetworked { get; private set; }

    private void Start()
    {
        try
        {
            context    = NetworkScene.Register(this);
            room       = FindFirstObjectByType<RoomClient>();
            IsNetworked = true;
            Debug.Log("[TutorialReadyManager] Tutorial / exploration phase active. " +
                      "Waiting for both players to press Ready.");
        }
        catch (KeyNotFoundException)
        {
            IsNetworked = false;
            Debug.LogWarning("[TutorialReadyManager] No NetworkScene found — running in offline mode. " +
                             "Ready/un-ready actions will only affect local state.");
        }
    }

    // -------------------------------------------------------------------------
    //  Public API  (called by TutorialReadyUI or any other UI script)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Mark the local player as ready. Sends a ready message over Ubiq.
    /// Has no effect once both players are already ready.
    /// </summary>
    public void RequestReady()
    {
        if (BothReady)   return;   // game already started
        if (IsLocalReady) return;

        IsLocalReady = true;
        OnLocalReadyChanged?.Invoke(IsLocalReady);
        Debug.Log("[TutorialReadyManager] Local player ready.");

        Send("ready");
        CheckBothReady();
    }

    /// <summary>
    /// Cancel the local player's ready state. Has no effect once the game
    /// has started (BothReady == true).
    /// </summary>
    public void RequestUnready()
    {
        if (BothReady)    return;   // cannot un-ready once game has started
        if (!IsLocalReady) return;

        IsLocalReady = false;
        OnLocalReadyChanged?.Invoke(IsLocalReady);
        Debug.Log("[TutorialReadyManager] Local player un-readied.");

        Send("unready");
    }

    // -------------------------------------------------------------------------
    //  Ubiq send / receive
    // -------------------------------------------------------------------------

    private void Send(string type)
    {
        if (!IsNetworked) return;

        try
        {
            context.SendJson(new ReadyMessage
            {
                type       = type,
                senderUuid = room?.Me?.uuid ?? "local"
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[TutorialReadyManager] SendJson failed: {e.Message}");
        }
    }

    /// <summary>
    /// Called automatically by Ubiq when a message arrives from another peer
    /// that has a TutorialReadyManager registered with the same network ID.
    /// </summary>
    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        var msg = message.FromJson<ReadyMessage>();

        // Ignore echoes of our own messages.
        if (room != null && msg.senderUuid == room.Me.uuid) return;

        switch (msg.type)
        {
            case "ready":
                IsOpponentReady = true;
                OnOpponentReadyChanged?.Invoke(IsOpponentReady);
                Debug.Log("[TutorialReadyManager] Opponent is ready.");
                CheckBothReady();
                break;

            case "unready":
                IsOpponentReady = false;
                OnOpponentReadyChanged?.Invoke(IsOpponentReady);
                Debug.Log("[TutorialReadyManager] Opponent un-readied.");
                break;

            default:
                Debug.LogWarning($"[TutorialReadyManager] Unknown message type: {msg.type}");
                break;
        }
    }

    // -------------------------------------------------------------------------
    //  Internal helpers
    // -------------------------------------------------------------------------

    private void CheckBothReady()
    {
        if (BothReady) return;
        if (!IsLocalReady || !IsOpponentReady) return;

        BothReady = true;
        Debug.Log("[TutorialReadyManager] Both players ready — starting game!");
        OnBothReady?.Invoke();
    }

    // -------------------------------------------------------------------------
    //  Debug helpers  (Editor + Development builds only)
    // -------------------------------------------------------------------------
#if UNITY_EDITOR || DEVELOPMENT_BUILD

    [ContextMenu("DEBUG – Simulate Opponent Ready")]
    public void Debug_SimulateOpponentReady()
    {
        IsOpponentReady = true;
        OnOpponentReadyChanged?.Invoke(IsOpponentReady);
        Debug.Log("[TutorialReadyManager][DEBUG] Simulated opponent ready.");
        CheckBothReady();
    }

    [ContextMenu("DEBUG – Simulate Opponent Un-ready")]
    public void Debug_SimulateOpponentUnready()
    {
        IsOpponentReady = false;
        OnOpponentReadyChanged?.Invoke(IsOpponentReady);
        Debug.Log("[TutorialReadyManager][DEBUG] Simulated opponent un-ready.");
    }

    [ContextMenu("DEBUG – Force Both Ready")]
    public void Debug_ForceBothReady()
    {
        IsLocalReady    = true;
        IsOpponentReady = true;
        OnLocalReadyChanged?.Invoke(IsLocalReady);
        OnOpponentReadyChanged?.Invoke(IsOpponentReady);
        CheckBothReady();
    }

    // -------------------------------------------------------------------------
    //  In-game debug panel  (OnGUI — visible in Play mode)
    // -------------------------------------------------------------------------

    [Header("Debug Panel")]
    [Tooltip("Show the in-game debug overlay panel.")]
    public bool showDebugPanel = true;

    private void OnGUI()
    {
        if (!showDebugPanel) return;

        GUILayout.BeginArea(new Rect(10, 10, 260, 160), GUI.skin.box);
        GUILayout.Label("<b>[TutorialReadyManager DEBUG]</b>", new GUIStyle(GUI.skin.label) { richText = true });
        GUILayout.Label($"Networked   : {IsNetworked}");
        GUILayout.Label($"Local ready : {IsLocalReady}");
        GUILayout.Label($"Opp ready   : {IsOpponentReady}");
        GUILayout.Label($"Both ready  : {BothReady}");

        GUILayout.Space(6);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Opp Ready"))   Debug_SimulateOpponentReady();
        if (GUILayout.Button("Opp Un-ready")) Debug_SimulateOpponentUnready();
        GUILayout.EndHorizontal();

        if (GUILayout.Button("Force Both Ready")) Debug_ForceBothReady();

        GUILayout.EndArea();
    }

#endif
}
