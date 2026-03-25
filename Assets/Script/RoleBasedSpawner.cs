using UnityEngine;

/// <summary>
/// Moves the XR Origin to the correct spawn point and enables the appropriate
/// gameplay object (Hammer or Mole) based on the role stored in GameData.
///
/// Attach to any persistent GameObject in the game scene.
/// Assign all references via the Inspector before entering Play mode.
/// </summary>
public class RoleBasedSpawner : MonoBehaviour
{
    [Header("Spawn Points")]
    [SerializeField] private Transform hammerSpawnPoint;
    [SerializeField] private Transform moleSpawnPoint;

    [Header("XR Rig")]
    [SerializeField] private Transform xrOrigin;

    [Header("Local Role-Specific Objects")]
    [SerializeField] private GameObject hammerObject;
    [SerializeField] private GameObject moleObject;

    [Header("Opponent Role-Specific Objects")]
    [SerializeField] private GameObject opponentHammerObject;
    [SerializeField] private GameObject opponentMoleObject;

    /// <summary>
    /// Exposed publicly so the role can be overridden in the Inspector during
    /// development / testing. At runtime this is overwritten from GameData.
    /// </summary>
    public RoleManager.Role playerRole;

    [Header("Tutorial Integration")]
    [Tooltip("Assign the TutorialReadyManager from the scene. When both players are ready, " +
             "the player is returned to their role spawn point.")]
    [SerializeField] private TutorialReadyManager tutorialReadyManager;

    private void Start()
    {
        // Read the role that was set before the scene was loaded.
        playerRole = GameData.LocalRole;

        ApplySpawnPoint();
        ApplyRoleObjects();

        if (tutorialReadyManager != null)
            tutorialReadyManager.OnCountdownStarted += ApplySpawnPoint;
        else
            Debug.LogWarning("[RoleBasedSpawner] tutorialReadyManager is not assigned — " +
                             "spawn-on-ready will not fire. Assign it in the Inspector.", this);
    }

    private void OnDestroy()
    {
        if (tutorialReadyManager != null)
            tutorialReadyManager.OnCountdownStarted -= ApplySpawnPoint;
    }

    /// <summary>
    /// Teleports the XR Origin to the spawn point matching the current role.
    /// </summary>
    private void ApplySpawnPoint()
    {
        if (xrOrigin == null)
        {
            Debug.LogWarning("[RoleBasedSpawner] xrOrigin is not assigned.", this);
            return;
        }

        Transform spawnPoint = playerRole == RoleManager.Role.Hammer
            ? hammerSpawnPoint
            : moleSpawnPoint;

        if (spawnPoint == null)
        {
            Debug.LogWarning(
                $"[RoleBasedSpawner] Spawn point for role '{playerRole}' is not assigned.", this);
            return;
        }

        xrOrigin.SetPositionAndRotation(spawnPoint.position, spawnPoint.rotation);
    }

    /// <summary>
    /// Enables the object that matches the player's role and disables the other.
    /// Also enables the opponent's matching object and disables the other.
    /// </summary>
    private void ApplyRoleObjects()
    {
        bool isHammer = playerRole == RoleManager.Role.Hammer;

        SetActive(hammerObject,         isHammer,  "hammerObject");
        SetActive(moleObject,           !isHammer, "moleObject");

        // Opponent is always the opposite role.
        SetActive(opponentHammerObject, !isHammer, "opponentHammerObject");
        SetActive(opponentMoleObject,   isHammer,  "opponentMoleObject");
    }

    /// <summary>
    /// Safely sets a GameObject active/inactive with a null guard.
    /// </summary>
    private void SetActive(GameObject obj, bool active, string label)
    {
        if (obj == null)
        {
            Debug.LogWarning($"[RoleBasedSpawner] '{label}' is not assigned.", this);
            return;
        }

        obj.SetActive(active);
    }
}
