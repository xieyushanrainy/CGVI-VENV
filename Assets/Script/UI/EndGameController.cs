using System;
using System.Collections;
using UnityEngine;
using UnityEngine.SceneManagement;
using Ubiq.Messaging;
using Ubiq.Rooms;
using Ubiq.Avatars;

/// <summary>
/// End-game screen controller. Sends an Ubiq message so the remote peer
/// performs the same scene transition, then changes scene locally.
///
/// Wire to the three end-game UI buttons via the Inspector.
/// </summary>
public class EndGameController : MonoBehaviour
{
    // -------------------------------------------------------------------------
    //  Inspector fields
    // -------------------------------------------------------------------------

    [Header("Scene Names")]
    [Tooltip("Exact scene name (without .unity) of the main game scene.\n" +
             "Must match the name in File → Build Settings.")]
    [SerializeField] private string gameSceneName = "waack_mole";

    [Tooltip("Exact scene name (without .unity) of the lobby / entry scene.\n" +
             "Must match the name in File → Build Settings.")]
    [SerializeField] private string menuSceneName = "EnterRoomScene";

    [Header("Timing")]
    [Tooltip("Number of frames to wait after sending the Ubiq message before\n" +
             "calling SceneManager.LoadScene.  Gives the transport layer time\n" +
             "to flush the outgoing packet before the scene is torn down.")]
    [SerializeField] private int delayFrames = 2;

    [Header("World-Space Canvas")]
    [Tooltip("Optional. When assigned, the end-game canvas is hidden before\n" +
             "any scene transition so it does not linger during the load.")]
    [SerializeField] private WorldSpaceCanvasSpawner endGameCanvasSpawner;

    // -------------------------------------------------------------------------
    //  Ubiq message type
    // -------------------------------------------------------------------------

    [Serializable]
    private struct RestartMessage
    {
        public string type;        // "restart" | "restart_switch" | "exit"
        public string senderUuid;
    }

    // -------------------------------------------------------------------------
    //  Private state
    // -------------------------------------------------------------------------

    private NetworkContext context;
    private RoomClient     room;
    private bool           networkAvailable;

    // Guard: only the first action is honoured — prevents double-firing if both
    // players click a button at the same moment.
    private bool actionHandled;

    // -------------------------------------------------------------------------
    //  Unity lifecycle
    // -------------------------------------------------------------------------

    private void Start()
    {
        room = FindFirstObjectByType<RoomClient>();

        try
        {
            context          = NetworkScene.Register(this);
            networkAvailable = true;
        }
        catch (Exception e)
        {
            Debug.LogWarning("[EndGameController] NetworkScene not found — " +
                             $"running in offline / single-player mode.\n{e.Message}");
        }
    }

    // -------------------------------------------------------------------------
    //  Button callbacks  (wire these to the Inspector onClick events)
    // -------------------------------------------------------------------------

    /// <summary>
    /// Restart the game keeping the same Hammer / Mole assignment.
    /// Wire to the Restart button's onClick event.
    /// </summary>
    public void OnRestartClicked()
    {
        if (actionHandled)  {
            return;
        }

        SendRestartMessage("restart");
        HandleRestart(swapRole: false);
    }

    /// <summary>
    /// Restart the game with roles swapped (Hammer → Mole, Mole → Hammer).
    /// Wire to the "Restart with Different Role" button's onClick event.
    /// </summary>
    public void OnRestartSwitchedClicked()
    {
        if (actionHandled)  {
            return;
        }

        SendRestartMessage("restart_switch");
        HandleRestart(swapRole: true);
    }

    /// <summary>
    /// Return both players to the lobby / entry scene.
    /// Wire to the Exit button's onClick event.
    /// </summary>
    public void OnExitClicked()
    {
        if (actionHandled)  {
            return;
        }

        SendRestartMessage("exit");
        HandleExit();
    }

    // -------------------------------------------------------------------------
    //  Ubiq receive — called by Ubiq when the REMOTE peer sends a message
    // -------------------------------------------------------------------------

    public void ProcessMessage(ReferenceCountedSceneGraphMessage message)
    {
        var msg = message.FromJson<RestartMessage>();

        // Ignore echoes of our own outgoing messages.
        if (room != null && msg.senderUuid == room.Me.uuid) return;

        // If the local player already made a choice, ignore the peer's message.
        if (actionHandled) return;

        switch (msg.type)
        {
            case "restart":
                HandleRestart(swapRole: false);
                break;

            case "restart_switch":
                // The peer swapped *their* role; we swap ours to stay complementary.
                HandleRestart(swapRole: true);
                break;

            case "exit":
                HandleExit();
                break;

            default:
                Debug.LogWarning($"[EndGameController] Unknown message type: '{msg.type}'");
                break;
        }
    }

    // -------------------------------------------------------------------------
    //  Action handlers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Prepare GameData for the next round and trigger a game-scene reload.
    /// </summary>
    /// <param name="swapRole">
    /// When true the local player's role is flipped (Hammer ↔ Mole).
    /// Both clients call this independently with the same flag, which keeps
    /// the roles complementary since they are always symmetric.
    /// </param>
    private void HandleRestart(bool swapRole)
    {
        actionHandled = true;

        // Hide the world-space canvas before the scene tears down.
        endGameCanvasSpawner?.HideCanvas();

        if (swapRole)
        {
            // Flip role before the scene reload so RoleBasedSpawner, canvasControl,
            // LocalRolePosePublisher and MoleVisibilityTracker all read the new value.
            GameData.LocalRole = GameData.LocalRole == RoleManager.Role.Hammer
                ? RoleManager.Role.Mole
                : RoleManager.Role.Hammer;
        }

        // Reset static score fields so they don't carry stale data into the
        // new round (ScoreManager itself is recreated, but GameData persists).
        GameData.MoleScore   = 0;
        GameData.HammerScore = 0;

        StartCoroutine(LoadSceneDeferred(gameSceneName));
    }

    private void HandleExit()
    {
        actionHandled = true;

        // Hide the world-space canvas before the scene tears down.
        endGameCanvasSpawner?.HideCanvas();

        // ── Restore DontDestroyOnLoad objects hidden by RolePanelController ──────
        // The social menu was hidden and the avatar prefab was nulled before the
        // game scene was loaded.  Both objects survive across scenes, so we must
        // undo that here before returning to the lobby.

        var socialMenu = FindFirstObjectByType<Ubiq.Samples.SocialMenu>(FindObjectsInactive.Include);
        if (socialMenu != null)
            socialMenu.gameObject.SetActive(true);

        var avatarManager = FindFirstObjectByType<AvatarManager>();
        if (avatarManager != null && GameData.LobbyAvatarPrefab != null)
            avatarManager.avatarPrefab = GameData.LobbyAvatarPrefab;

        var roleManager = FindFirstObjectByType<RoleManager>();
        if (roleManager != null)
            roleManager.ResetSession();

        StartCoroutine(LoadSceneDeferred(menuSceneName));
    }

    // -------------------------------------------------------------------------
    //  Helpers
    // -------------------------------------------------------------------------

    /// <summary>
    /// Send a message to the remote peer. Swallows all exceptions so a network
    /// outage or offline session does not block the local scene transition.
    /// </summary>
    private void SendRestartMessage(string type)
    {
        if (!networkAvailable) return;

        try
        {
            context.SendJson(new RestartMessage
            {
                type       = type,
                senderUuid = room?.Me?.uuid ?? "local"
            });
        }
        catch (Exception e)
        {
            Debug.LogWarning($"[EndGameController] SendJson failed: {e.Message}");
        }
    }

    /// <summary>
    /// Wait <see cref="delayFrames"/> frames then load the target scene.
    /// The brief delay ensures that the Ubiq outgoing message buffer is flushed
    /// before Unity tears down the current scene.
    /// </summary>
    private IEnumerator LoadSceneDeferred(string sceneName)
    {
        for (int i = 0; i < delayFrames; i++)
            yield return null;

        SceneManager.LoadScene(sceneName);
    }
}
