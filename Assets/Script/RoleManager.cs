using UnityEngine;
using Ubiq.Messaging;
using Ubiq.Rooms;
using System;
using System.Linq;

public class RoleManager : MonoBehaviour
{
    public enum Role
    {
        None,
        Hammer,
        Mole
    }

    private NetworkContext context;
    private RoomClient room;

    public Role LocalRole { get; private set; } = Role.None;

    private bool rolesAssigned = false;

    [Serializable]
    struct RoleMessage
    {
        public string hammerId;
        public string moleId;
    }


    void Start()
    {
        Debug.Log("Start");
        context = NetworkScene.Register(this);
        room = FindFirstObjectByType<RoomClient>();

        //context += OnMessage;
        room.OnPeerAdded.AddListener(OnPeerChanged);
        room.OnPeerRemoved.AddListener(OnPeerChanged);
    }

    void OnPeerChanged(IPeer peer)
    {
        Debug.Log("Peer changed");
        TryAssignRoles();
    }

    void TryAssignRoles()
    {
        if (rolesAssigned)
        {
            Debug.Log("Role assigned: My Role:" + LocalRole);
            return;
        }


        // Only authority randomises roles
        if (!IsAuthority())
            return;

        var peers = room.Peers.ToList();

        if (peers.Count != 1)
            return;

        rolesAssigned = true;

        // Randomise
        int r = UnityEngine.Random.Range(0, 2);

        string hammer = r == 0 ? peers[0].uuid : room.Me.uuid;
        string mole   = r == 0 ? room.Me.uuid : peers[0].uuid;

        RoleMessage msg = new RoleMessage()
        {
            hammerId = hammer,
            moleId = mole
        };

        context.SendJson(msg);
        ApplyRoles(msg);
    }

    bool IsAuthority()
    {
        // Authority = lowest uuid (simple deterministic rule)
        var myId = room.Me.uuid;
        var lowest = room.Peers.Min(p => p.uuid);

        return myId.CompareTo(lowest) <= 0;
    }

    public void ProcessMessage(ReferenceCountedSceneGraphMessage msg)
    {
        var roleMsg = msg.FromJson<RoleMessage>();
        ApplyRoles(roleMsg);
    }

    void ApplyRoles(RoleMessage msg)
    {
        if (room.Me.uuid == msg.hammerId)
        {
            LocalRole = Role.Hammer;
            Debug.Log("Assigned Hammer");
        }
        else if (room.Me.uuid == msg.moleId)
        {
            LocalRole = Role.Mole;
            Debug.Log("Assigned Mole");
        }
        else
        {
            LocalRole = Role.None;
        }

        // TODO:
        // Trigger UI update here
        // e.g. OnRoleAssigned?.Invoke(LocalRole);
    }
}
