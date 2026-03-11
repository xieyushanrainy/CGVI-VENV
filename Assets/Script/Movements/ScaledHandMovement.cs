using UnityEngine;

public class ScaledHandMovement : MonoBehaviour
{
    public Transform trackedController;
    public float movementScale = 3f;

    [Tooltip("Euler angle offset applied on top of the controller rotation to preserve " +
             "the mesh's baked-in initial rotation (e.g. 90° around X).")]
    public Vector3 modelRotationOffset = new Vector3(90f, 0f, 0f);

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
        transform.rotation = trackedController.rotation * Quaternion.Euler(modelRotationOffset);
    }
}