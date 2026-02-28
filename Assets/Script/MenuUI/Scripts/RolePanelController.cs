using UnityEngine;
using UnityEngine.UI;

public class RolePanelController : MonoBehaviour
{
    [Tooltip("Text component used to display the current role.")]
    public Text roleText;

    [Tooltip("(Optional) Drag the RoleManager GameObject here. " +
             "If empty the script will search the scene automatically.")]
    public RoleManager roleManager;

    // ------------------------------------------------------------------ //
    //  Unity lifecycle
    // ------------------------------------------------------------------ //

    private void Start()
    {
        if (!roleManager)
        {
            roleManager = FindFirstObjectByType<RoleManager>();
        }

        if (roleManager == null)
        {
            Debug.LogWarning("[RolePanelController] No RoleManager found in scene.");
            return;
        }

        // Subscribe to future role changes
        roleManager.OnRoleChanged += HandleRoleChanged;

        // Reflect whatever state RoleManager is already in
        HandleRoleChanged(roleManager.LocalRole);
    }

    private void OnDestroy()
    {
        if (roleManager != null)
        {
            roleManager.OnRoleChanged -= HandleRoleChanged;
        }
    }

    // ------------------------------------------------------------------ //
    //  Role change handler
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
    }
}
