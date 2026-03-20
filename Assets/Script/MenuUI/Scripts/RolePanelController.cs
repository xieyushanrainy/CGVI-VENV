using UnityEngine;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using Ubiq.Avatars;

/// <summary>
/// Drives the Role Panel UI: role text, switches remaining, Switch button,
/// Ready button, and opponent status text.
/// Wire all fields in the Inspector (see tooltips).
/// </summary>
public class RolePanelController : MonoBehaviour
{
    [Header("Role & Switches")]
    [Tooltip("Text that displays the current role.")]
    public Text roleText;

    [Tooltip("Text that shows how many switches are left.")]
    public Text switchesText;

    [Header("Buttons")]
    [Tooltip("Switch button – disabled when ready or no switches left.")]
    public Button switchButton;

    [Tooltip("Ready button – toggles between Ready / Un-ready.")]
    public Button readyButton;

    [Header("Opponent Status")]
    [Tooltip("Text showing whether the opponent has pressed Ready.")]
    public Text opponentStatusText;

    [Header("Scene Transition")]
    [Tooltip("Exact name of the game scene to load (must be in Build Settings).")]
    public string gameSceneName = "GameScene";

    [Header("References")]
    [Tooltip("(Optional) RoleManager in the scene. Auto-found if left empty.")]
    public RoleManager roleManager;

    private Text _readyButtonLabel;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        if (!roleManager)
            roleManager = FindFirstObjectByType<RoleManager>();

        if (roleManager == null)
        {
            Debug.LogWarning("[RolePanelController] No RoleManager found in scene.");
            return;
        }

        roleManager.OnRoleChanged          += HandleRoleChanged;
        roleManager.OnSwitchesChanged      += HandleSwitchesChanged;
        roleManager.OnReadyChanged         += HandleReadyChanged;
        roleManager.OnOpponentReadyChanged += HandleOpponentReadyChanged;
        roleManager.OnGameStart            += HandleGameStart;

        if (switchButton != null)
            switchButton.onClick.AddListener(OnSwitchClicked);

        if (readyButton != null)
        {
            readyButton.onClick.AddListener(OnReadyClicked);
            _readyButtonLabel = readyButton.GetComponentInChildren<Text>();
        }

        // Reflect current states immediately
        HandleRoleChanged(roleManager.LocalRole);
        HandleSwitchesChanged(roleManager.SwitchesRemaining);
        HandleReadyChanged(roleManager.IsLocalReady);
        HandleOpponentReadyChanged(roleManager.IsOpponentReady);
    }

    private void OnDestroy()
    {
        if (roleManager != null)
        {
            roleManager.OnRoleChanged          -= HandleRoleChanged;
            roleManager.OnSwitchesChanged      -= HandleSwitchesChanged;
            roleManager.OnReadyChanged         -= HandleReadyChanged;
            roleManager.OnOpponentReadyChanged -= HandleOpponentReadyChanged;
            roleManager.OnGameStart            -= HandleGameStart;
        }
    }

    // ------------------------------------------------------------------ //
    //  Event handlers
    // ------------------------------------------------------------------ //

    private void HandleRoleChanged(RoleManager.Role role)
    {
        if (roleText == null)
        {
            Debug.LogWarning("[RolePanelController] roleText is not assigned.");
            return;
        }

        bool hasRole = role == RoleManager.Role.Hammer || role == RoleManager.Role.Mole;
        SetLobbyElementsVisible(hasRole);

        switch (role)
        {
            case RoleManager.Role.Hammer:
                roleText.text = "Your Role: Hammer";
                break;
            case RoleManager.Role.Mole:
                roleText.text = "Your Role: Mole";
                break;
            case RoleManager.Role.None:
                roleText.text = "Waiting for another player to join...";
                break;
            default:
                roleText.text = "Please join a room first.";
                break;
        }

        RefreshButtons();
    }

    private void HandleSwitchesChanged(int remaining)
    {
        if (switchesText != null)
            switchesText.text = $"Switches left: {remaining}";

        RefreshButtons();
    }

    private void HandleReadyChanged(bool isReady)
    {
        if (_readyButtonLabel != null)
            _readyButtonLabel.text = isReady ? "Un-ready" : "Ready";

        RefreshButtons();
    }

    private void HandleOpponentReadyChanged(bool opponentReady)
    {
        if (opponentStatusText != null)
            opponentStatusText.text = opponentReady ? "Opponent: Ready ✓" : "Opponent: Not Ready";
    }

    private void HandleGameStart()
    {
        // Lock lobby UI when the game begins
        if (switchButton != null) switchButton.interactable = false;
        if (readyButton  != null) readyButton.interactable  = false;
        if (roleText     != null) roleText.text += "\nGame Starting!";

        // Carry role to next scene, then load it
        GameData.LocalRole = roleManager.LocalRole;

        // Clear lobby avatars: AvatarManager.UpdateLocalAvatar will despawn them
        // when avatarPrefab is null, preventing them from appearing in the arena.
        var avatarManager = FindFirstObjectByType<Ubiq.Avatars.AvatarManager>();
        if (avatarManager != null)
        {
            // Save the prefab reference so EndGameController can restore it on Exit.
            GameData.LobbyAvatarPrefab     = avatarManager.avatarPrefab;
            avatarManager.avatarPrefab = null;
        }

        // Hide the lobby menu so it doesn't carry over into the arena scene.
        var socialMenu = FindFirstObjectByType<Ubiq.Samples.SocialMenu>();
        if (socialMenu != null)
            socialMenu.gameObject.SetActive(false);

        SceneManager.LoadScene(gameSceneName);
    }

    // ------------------------------------------------------------------ //
    //  Visibility helpers
    // ------------------------------------------------------------------ //

    private void SetLobbyElementsVisible(bool visible)
    {
        if (switchesText      != null) switchesText.gameObject.SetActive(visible);
        if (switchButton      != null) switchButton.gameObject.SetActive(visible);
        if (readyButton       != null) readyButton.gameObject.SetActive(visible);
        if (opponentStatusText != null) opponentStatusText.gameObject.SetActive(visible);
    }

    // ------------------------------------------------------------------ //
    //  Button state
    // ------------------------------------------------------------------ //

    private void RefreshButtons()
    {
        if (roleManager == null) return;

        bool hasRole = roleManager.LocalRole == RoleManager.Role.Hammer ||
                       roleManager.LocalRole == RoleManager.Role.Mole;

        // Switch: needs a role, switches remaining, AND must not be ready
        if (switchButton != null)
            switchButton.interactable = hasRole &&
                                        roleManager.SwitchesRemaining > 0 &&
                                        !roleManager.IsLocalReady;

        // Ready toggle: needs a role (can always un-ready once ready)
        if (readyButton != null)
            readyButton.interactable = hasRole;
    }

    // ------------------------------------------------------------------ //
    //  Button callbacks
    // ------------------------------------------------------------------ //

    private void OnSwitchClicked()
    {
        roleManager?.RequestSwitch();
    }

    private void OnReadyClicked()
    {
        if (roleManager == null) return;

        if (roleManager.IsLocalReady)
            roleManager.RequestUnready();
        else
            roleManager.RequestReady();
    }
}
