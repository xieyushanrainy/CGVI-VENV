using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Ubiq.Messaging;
using Ubiq.Rooms;

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

    /// <summary>Fires immediately when the countdown begins (before the first tick).</summary>
    public event Action OnCountdownStarted;

    /// <summary>Fires each second during the pre-game countdown with seconds remaining (5 down to 1).</summary>
    public event Action<int> OnCountdownTick;

    // -------------------------------------------------------------------------
    //  XR Input
    // -------------------------------------------------------------------------

    [Header("XR Input")]
    [Tooltip("Assign the XRI RightHand/Activate action here (e.g. from the XRI Default Input Actions asset).")]
    [SerializeField] private InputActionReference rightHandActivate;

    [Header("Countdown")]
    [Tooltip("Seconds to count down after both players are ready before the game starts.")]
    [SerializeField] private int countdownSeconds = 5;

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

    private void OnEnable()
    {
        if (rightHandActivate != null)
        {
            rightHandActivate.action.performed += OnRightHandActivate;
            rightHandActivate.action.Enable();
        }
    }

    private void OnDisable()
    {
        if (rightHandActivate != null)
            rightHandActivate.action.performed -= OnRightHandActivate;
    }

    private void OnRightHandActivate(InputAction.CallbackContext ctx)
    {
        RequestReady();
    }

    private void Start()
    {
        try
        {
            context    = NetworkScene.Register(this);
            room       = FindFirstObjectByType<RoomClient>();
            IsNetworked = true;
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
                CheckBothReady();
                break;

            case "unready":
                IsOpponentReady = false;
                OnOpponentReadyChanged?.Invoke(IsOpponentReady);
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
        StartCoroutine(StartCountdown());
    }

    private IEnumerator StartCountdown()
    {
        OnCountdownStarted?.Invoke();
        for (int i = countdownSeconds; i > 0; i--)
        {
            OnCountdownTick?.Invoke(i);
            yield return new WaitForSeconds(1f);
        }
        Debug.Log("[TutorialReadyManager] Countdown complete — starting game!");
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

    [ContextMenu("DEBUG – Simulate Opponent Un-ready")]
    }
