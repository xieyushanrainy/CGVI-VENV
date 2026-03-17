using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class Raiser : MonoBehaviour
{
    public Transform controller;
    public Transform cameraOffset;

    public InputActionProperty triggerAction;

    public float sensitivity = 2f;
    public float yMin = 0.07f;
    public float yMax = 4.5f;

    float lastY;
    bool controlling = false;

    void Start()
    {
        triggerAction.action.Enable();
    }

    /// <summary>
    /// Computes the new camera-offset Y for this frame based on controller delta.
    /// Manages its own trigger / delta state internally.
    ///
    /// Pass the caller's own tracked desired-Y (not the actual camera offset Y that
    /// may have been modified by an external penalty) so delta tracking stays clean.
    /// </summary>
    /// <param name="currentCameraY">The current intended camera offset localPosition.y.</param>
    /// <returns>The clamped Y after applying the controller delta this frame.</returns>
    public float ComputeNewY(float currentCameraY)
    {
        bool triggerPressed = triggerAction.action != null && triggerAction.action.IsPressed();

        if (triggerPressed)
        {
            float currentControllerY = controller.localPosition.y;

            if (!controlling)
            {
                lastY       = currentControllerY;
                controlling = true;
                return currentCameraY;
            }

            float delta = currentControllerY - lastY;
            float newY  = Math.Clamp(currentCameraY + delta * sensitivity, yMin, yMax);
            lastY = currentControllerY;
            return newY;
        }
        else
        {
            controlling = false;
            return currentCameraY;
        }
    }

    void Update()
    {
        if (cameraOffset == null) return;
        Vector3 pos = cameraOffset.localPosition;
        pos.y = ComputeNewY(pos.y);
        cameraOffset.localPosition = pos;
    }
}