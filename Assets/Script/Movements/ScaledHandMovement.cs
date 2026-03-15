using UnityEngine;
using UnityEngine.InputSystem;

public class ScaledHandMovement : MonoBehaviour
{
    public Transform trackedController;
    public float movementScale = 3f;
    public InputActionProperty recallAction;

    // [Tooltip("Euler angle offset applied on top of the controller rotation to preserve " +
    //          "the mesh's baked-in initial rotation (e.g. 90° around X).")]
    // public Vector3 modelRotationOffset = new Vector3(90f, 0f, 0f);

    public InputActionProperty joystick;   // XR joystick
    public float distance = 0.3f;
    public float distanceSpeed = 1.0f;
    public float minDistance = 0.1f;
    public float maxDistance = 2.0f;

    Vector3 startControllerPos;
    Vector3 startAnchorPos;
    Vector3 startOffset;

    void Start()
    {
        Invoke(nameof(Init), 0.2f);
    }

    void Init()
    {
        startControllerPos = trackedController.position;
        startAnchorPos = transform.position;
        startOffset = transform.position - startControllerPos;
    }

    void Update()
    {
        if (recallAction.action.WasPressedThisFrame())
        {
            Recall();
        }

        Vector3 delta = trackedController.position - startControllerPos;

        delta *= movementScale;

        Vector2 input = joystick.action.ReadValue<Vector2>();
        distance += input.y * distanceSpeed * Time.deltaTime;
        distance = Mathf.Clamp(distance, minDistance, maxDistance);

        Vector3 basePos = startAnchorPos + delta;
        Vector3 forwardOffset = trackedController.forward * distance;

        transform.position = basePos + forwardOffset;
        transform.rotation = trackedController.rotation; // * Quaternion.Euler(modelRotationOffset);
    }

    void Recall()
    {
        startControllerPos = trackedController.position;
        startAnchorPos = trackedController.position + startOffset;

        transform.position = startAnchorPos;
        transform.rotation = trackedController.rotation;
    }
}

    