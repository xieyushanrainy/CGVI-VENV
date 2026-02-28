using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Displays role status, remaining switch count, and drives the Switch button.
/// Wire all fields in the Inspector (see field tooltips).
/// </summary>
public class RolePanelController : MonoBehaviour
{
    [Tooltip("Text component used to display the current role.")]
    public Text roleText;

    [Tooltip("Text component that shows how many switches are left.")]
    public Text switchesText;

    [Tooltip("The Switch button – enabled only when the player has an active role and switches remaining.")]
    public Button switchButton;

    [Tooltip("(Optional) Drag the RoleManager GameObject here. " +
             "If empty the script will search the scene automatically.")]
    public RoleManager roleManager;

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

        roleManager.OnRoleChanged    += HandleRoleChanged;
        roleManager.OnSwitchesChanged += HandleSwitchesChanged;

        if (switchButton != null)
            switchButton.onClick.AddListener(OnSwitchClicked);

        // Reflect current states immediately
        HandleRoleChanged(roleManager.LocalRole);
        HandleSwitchesChanged(roleManager.SwitchesRemaining);
    }

    private void OnDestroy()
    {
        if (roleManager != null)
        {
            roleManager.OnRoleChanged    -= HandleRoleChanged;
            roleManager.OnSwitchesChanged -= HandleSwitchesChanged;
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

        RefreshSwitchButton();
    }

    private void HandleSwitchesChanged(int remaining)
    {
        if (switchesText != null)
            switchesText.text = $"Switches left: {remaining}";

        RefreshSwitchButton();
    }

    private void RefreshSwitchButton()
    {
        if (switchButton == null || roleManager == null) return;

        bool hasRole = roleManager.LocalRole == RoleManager.Role.Hammer ||
                       roleManager.LocalRole == RoleManager.Role.Mole;
        switchButton.interactable = hasRole && roleManager.SwitchesRemaining > 0;
    }

    private void OnSwitchClicked()
    {
        roleManager?.RequestSwitch();
    }
}
