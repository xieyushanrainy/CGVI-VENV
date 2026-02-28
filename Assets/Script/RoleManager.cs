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

    public Role LocalRole { get; private set; } = Role.NotInRoom;
    public int SwitchesRemaining { get; private set; } = MaxSwitches;

    /// <summary>Fires whenever the local role changes.</summary>
    public event Action<Role> OnRoleChanged;

    /// <summary>Fires whenever SwitchesRemaining changes, passing the new count.</summary>
    public event Action<int> OnSwitchesChanged;

    private bool rolesAssigned = false;

    // Single message struct used for all network traffic.
    // type: "role"   -> hammerId / moleId populated
    // type: "switch" -> requesterUuid populated
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
    /// Called by the UI Switch button. Spends one switch token, swaps both
    /// players' roles, and notifies the remote peer.
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

        SwitchesRemaining--;
        OnSwitchesChanged?.Invoke(SwitchesRemaining);

        var msg = new NetworkMessage { type = "switch", requesterUuid = room.Me.uuid };
        context.SendJson(msg);
        ApplySwitch();
    }

    // ------------------------------------------------------------------ //
    //  Network message handling
    // ------------------------------------------------------------------ //

    public void ProcessMessage(ReferenceCountedSceneGraphMessage msg)
    {
        var netMsg = msg.FromJson<NetworkMessage>();

        if (netMsg.type == "role")
        {
            ApplyRoles(netMsg.hammerId, netMsg.moleId);
        }
        else if (netMsg.type == "switch")
        {
            // The requester already applied the switch locally; only the
            // remote peer needs to react here.
            if (netMsg.requesterUuid != room.Me.uuid)
            {
                ApplySwitch();
            }
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
}
