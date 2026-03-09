using UnityEngine;

public class ScaledHandMovement : MonoBehaviour
{
    public Transform trackedController;
    public float movementScale = 8f;

    Vector3 startControllerPos;
    Vector3 startAnchorPos;

    void Start()
    {
        startControllerPos = trackedController.position;
        startAnchorPos = transform.position;
    }

    void Update()
    {
        Vector3 delta = trackedController.position - startControllerPos;
        delta.x *= movementScale;
        delta.z *= movementScale;
        transform.position = startAnchorPos + delta;
        transform.rotation = trackedController.rotation;
    }
}