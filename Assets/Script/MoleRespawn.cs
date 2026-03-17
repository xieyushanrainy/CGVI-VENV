using UnityEngine;
using UnityEngine.InputSystem;

public class MoleRespawn : MonoBehaviour
{
    public InputActionReference respawnAction;
    [SerializeField] private Transform moleSpawnPoint;
    [SerializeField] private Transform xrOrigin;

    public RoleManager.Role playerRole;

    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {

    }

    // Update is called once per frame
    void Update()
    {
        if (respawnAction.action.WasPressedThisFrame())
        {
            if (playerRole == RoleManager.Role.Mole)
            {
                Respawn();
            }
        }
        
    }

    void Respawn()
    {
        xrOrigin.SetPositionAndRotation(moleSpawnPoint.position, moleSpawnPoint.rotation);
    }
}
