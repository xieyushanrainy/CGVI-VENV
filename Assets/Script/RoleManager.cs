using UnityEngine;
using Ubiq.Messaging;
using Ubiq.Rooms;
using System;
using System.Linq;

public class RoleManager : MonoBehaviour
{
    public enum Role
    {
        NotInRoom,
        None,
        Hammer,
        Mole
    }

    public const int MaxSwitches = 3;

    private NetworkContext context;
    private RoomClient room;

    public Role LocalRole       { get; private set; } = Role.NotInRoom;
    public int  SwitchesRemaining { get; private set; } = MaxSwitches;
    public bool IsLocalReady    { get; private set; } = false;
    public bool IsOpponentReady { get; private set; } = false;

    private bool rolesAssigned = false;
    private bool gameStarted   = false;

    /// <summary>Fires whenever the local role changes.</summary>
    public event Action<Role> OnRoleChanged;

    /// <summary>Fires whenever SwitchesRemaining changes.</summary>
    public event Action<int> OnSwitchesChanged;

    /// <summary>Fires whenever the local ready state changes.</summary>
    public event Action<bool> OnReadyChanged;

    /// <summary>Fires whenever the opponent ready state changes.</summary>
    public event Action<bool> OnOpponentReadyChanged;

    /// <summary>Fires once when both players are ready.</summary>
    public event Action OnGameStart;

    // type: "role"    -> hammerId / moleId populated
    // type: "switch"  -> requesterUuid populated
    // type: "ready"   -> requesterUuid populated
    // type: "unready" -> requesterUuid populated
    [Serializable]
    struct NetworkMessage
    {
        public string type;
        public string hammerId;
        public string moleId;
        public string requesterUuid;
    }

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    void Start()
    {
        Debug.Log("[RoleManager] Start");
        context = NetworkScene.Register(this);
        room = FindFirstObjectByType<RoomClient>();

        room.OnPeerAdded.AddListener(OnPeerChanged);
        room.OnPeerRemoved.AddListener(OnPeerChanged);
        room.OnJoinedRoom.AddListener(OnJoinedRoom);

        // Notify UI of initial states
        OnRoleChanged?.Invoke(LocalRole);
        OnSwitchesChanged?.Invoke(SwitchesRemaining);
        OnReadyChanged?.Invoke(IsLocalReady);
        OnOpponentReadyChanged?.Invoke(IsOpponentReady);
    }

    // ------------------------------------------------------------------ //
    //  Room events
    // ------------------------------------------------------------------ //

    void OnJoinedRoom(IRoom joinedRoom)
    {
        if (LocalRole == Role.NotInRoom)
        {
            LocalRole = Role.None;
            OnRoleChanged?.Invoke(LocalRole);
        }
    }

    void OnPeerChanged(IPeer peer)
    {
        Debug.Log("[RoleManager] Peer changed");
        TryAssignRoles();
    }

    // ------------------------------------------------------------------ //
    //  Role assignment
    // ------------------------------------------------------------------ //

    void TryAssignRoles()
    {
        if (rolesAssigned)
        {
            Debug.Log("[RoleManager] Roles already assigned. My Role: " + LocalRole);
            return;
        }

        if (!IsAuthority())
            return;

        var peers = room.Peers.ToList();
        if (peers.Count != 1)
            return;

        rolesAssigned = true;

        int r = UnityEngine.Random.Range(0, 2);
        string hammer = r == 0 ? peers[0].uuid : room.Me.uuid;
        string mole   = r == 0 ? room.Me.uuid   : peers[0].uuid;

        var msg = new NetworkMessage { type = "role", hammerId = hammer, moleId = mole };
        context.SendJson(msg);
        ApplyRoles(hammer, mole);
    }

    bool IsAuthority()
    {
        var myId   = room.Me.uuid;
        var lowest = room.Peers.Min(p => p.uuid);
        return myId.CompareTo(lowest) <= 0;
    }

    // ------------------------------------------------------------------ //
    //  Switch
    // ------------------------------------------------------------------ //

    /// <summary>
    /// Called by the UI Switch button. Only allowed when not ready.
    /// Spends one switch token, resets both ready states, swaps roles.
    /// </summary>
    public void RequestSwitch()
    {
        if (SwitchesRemaining <= 0)
        {
            Debug.Log("[RoleManager] No switches remaining.");
            return;
        }
        if (LocalRole != Role.Hammer && LocalRole != Role.Mole)
        {
            Debug.Log("[RoleManager] Cannot switch: no active role.");
            return;
        }
        if (IsLocalReady)
        {
            Debug.Log("[RoleManager] Cannot switch while ready — un-ready first.");
            return;
        }

        SwitchesRemaining--;
        OnSwitchesChanged?.Invoke(SwitchesRemaining);

        // Switching always resets both ready states
        ResetBothReadyStates();

        var msg = new NetworkMessage { type = "switch", requesterUuid = room.Me.uuid };
        context.SendJson(msg);
        ApplySwitch();
    }

    // ------------------------------------------------------------------ //
    //  Ready
    // ------------------------------------------------------------------ //

    /// <summary>Mark the local player as ready. Triggers game start if opponent is also ready.</summary>
    public void RequestReady()
    {
        if (LocalRole != Role.Hammer && LocalRole != Role.Mole)
        {
            Debug.Log("[RoleManager] Cannot ready: no active role.");
            return;
        }
        if (IsLocalReady) return;

        IsLocalReady = true;
        OnReadyChanged?.Invoke(IsLocalReady);
        Debug.Log("[RoleManager] Local player ready.");

        context.SendJson(new NetworkMessage { type = "ready", requesterUuid = room.Me.uuid });
        CheckGameStart();
    }

    /// <summary>Cancel the local player's ready state.</summary>
    public void RequestUnready()
    {
        if (!IsLocalReady) return;

        IsLocalReady = false;
        OnReadyChanged?.Invoke(IsLocalReady);
        Debug.Log("[RoleManager] Local player un-readied.");

        context.SendJson(new NetworkMessage { type = "unready", requesterUuid = room.Me.uuid });
    }

    // ------------------------------------------------------------------ //
    //  Network message handling
    // ------------------------------------------------------------------ //

    public void ProcessMessage(ReferenceCountedSceneGraphMessage msg)
    {
        var netMsg = msg.FromJson<NetworkMessage>();

        switch (netMsg.type)
        {
            case "role":
                ApplyRoles(netMsg.hammerId, netMsg.moleId);
                break;

            case "switch":
                if (netMsg.requesterUuid != room.Me.uuid)
                {
                    // Opponent switched: reset both ready states, then swap our role
                    ResetBothReadyStates();
                    ApplySwitch();
                }
                break;

            case "ready":
                if (netMsg.requesterUuid != room.Me.uuid)
                {
                    IsOpponentReady = true;
                    OnOpponentReadyChanged?.Invoke(IsOpponentReady);
                    Debug.Log("[RoleManager] Opponent is ready.");
                    CheckGameStart();
                }
                break;

            case "unready":
                if (netMsg.requesterUuid != room.Me.uuid)
                {
                    IsOpponentReady = false;
                    OnOpponentReadyChanged?.Invoke(IsOpponentReady);
                    Debug.Log("[RoleManager] Opponent un-readied.");
                }
                break;
        }
    }

    // ------------------------------------------------------------------ //
    //  Apply helpers
    // ------------------------------------------------------------------ //

    void ApplyRoles(string hammerId, string moleId)
    {
        if (room.Me.uuid == hammerId)
        {
            LocalRole = Role.Hammer;
            Debug.Log("[RoleManager] Assigned Hammer");
        }
        else if (room.Me.uuid == moleId)
        {
            LocalRole = Role.Mole;
            Debug.Log("[RoleManager] Assigned Mole");
        }
        else
        {
            LocalRole = Role.None;
        }

        OnRoleChanged?.Invoke(LocalRole);
    }

    void ApplySwitch()
    {
        if (LocalRole == Role.Hammer)
        {
            LocalRole = Role.Mole;
            Debug.Log("[RoleManager] Switched to Mole");
        }
        else if (LocalRole == Role.Mole)
        {
            LocalRole = Role.Hammer;
            Debug.Log("[RoleManager] Switched to Hammer");
        }

        OnRoleChanged?.Invoke(LocalRole);
    }

    /// <summary>
    /// Resets both local and opponent ready states. Called whenever a switch occurs,
    /// since a switch invalidates whatever both players previously agreed to.
    /// </summary>
    void ResetBothReadyStates()
    {
        if (IsLocalReady)
        {
            IsLocalReady = false;
            OnReadyChanged?.Invoke(IsLocalReady);
            Debug.Log("[RoleManager] Local ready reset due to role switch.");
        }
        if (IsOpponentReady)
        {
            IsOpponentReady = false;
            OnOpponentReadyChanged?.Invoke(IsOpponentReady);
            Debug.Log("[RoleManager] Opponent ready reset due to role switch.");
        }
    }

    void CheckGameStart()
    {
        if (gameStarted) return;
        if (IsLocalReady && IsOpponentReady)
        {
            gameStarted = true;
            Debug.Log("[RoleManager] Both players ready — game starting!");
            OnGameStart?.Invoke();
        }
    }
}
