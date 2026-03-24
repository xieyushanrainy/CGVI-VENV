using UnityEngine;
using UnityEngine.InputSystem;

public class ScaledHandMovement : MonoBehaviour
{
    public Transform trackedController;
    public float movementScale = 3f;
    public InputActionProperty recallAction;

    [Tooltip("Euler angle offset applied on top of the controller rotation to preserve " +
             "the mesh's baked-in initial rotation (e.g. 90° around X).")]
    public Vector3 startOffsetRotation = new Vector3(30f, 0f, 0f);

    public InputActionProperty joystick;   // XR joystick
    public Transform xrCamera;

    public Transform xrOrigin;
    public Transform spawnPoint;

    public float distance = 0.3f;
    public float distanceSpeed = 1.0f;
    public float minDistance = 0.1f;
    public float maxDistance = 2.0f;

    Vector3 startControllerPos;
    Vector3 startAnchorPos;
    Vector3 startOffsetPosition;

    bool initialized = false;
    // Rigidbody rb;

    void Start()
    {
        // rb = GetComponent<Rigidbody>();
    }

    void Init()
    {
        startControllerPos = trackedController.position;
        startAnchorPos = transform.position;
        startOffsetPosition = transform.position - startControllerPos;
    }

    void Update()
    {
        if (!initialized)
        {
            if (trackedController.position.y > 2.5f)
            {
                Init();
                initialized = true;
            }
            return;
        }

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
        transform.rotation = trackedController.rotation * Quaternion.Euler(startOffsetRotation);
        // rb.MovePosition(basePos + forwardOffset);
        // rb.MoveRotation(trackedController.rotation);
    }

    void Recall()
    {
        startControllerPos = trackedController.position;
        startAnchorPos = trackedController.position + startOffsetPosition;

        transform.position = startAnchorPos;
        transform.rotation = trackedController.rotation * Quaternion.Euler(startOffsetRotation);
        ResetXROrigin(spawnPoint);
    }

    void ResetXROrigin(Transform target)
    {
        xrOrigin.position = target.position;

        float cameraYaw = xrCamera.eulerAngles.y;
        float targetYaw = target.eulerAngles.y;

        float deltaYaw = targetYaw - cameraYaw;

        xrOrigin.Rotate(0, deltaYaw, 0);
    }
}

    