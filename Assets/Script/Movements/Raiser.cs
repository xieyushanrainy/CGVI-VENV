using UnityEngine;
using UnityEngine.InputSystem;
using System;

public class Raiser : MonoBehaviour
{
    public enum MoleMovementMode
    {
        Drag,
        Stand
    }
    public Transform hand;
    public Transform headset;
    public Transform cameraOffset;

    public InputActionProperty triggerAction;

    public float handSensitivity = 6f;
    public float headSensitivity = 10f;
    public float yMin = 0.07f;
    public float yMax = 4.5f;

    float lastY;
    bool controlling = false;
    MoleMovementMode moleMovementMode = MoleMovementMode.Drag;
    Transform controller;
    float sensitivity = 1.0f;

    void Start()
    {
        if (GameData.LocalRole != RoleManager.Role.Mole)
        {
            Debug.Log("[Raiser] Local role is not Mole — disabling.", this);
            return;
        }

        triggerAction.action.Enable();
        if (moleMovementMode == MoleMovementMode.Drag)
        {
            controller = hand;
            sensitivity = handSensitivity;
        }
        else if (moleMovementMode == MoleMovementMode.Stand)
        {
            controller = headset;
            sensitivity = headSensitivity;
        }
        else
        {
            Debug.Log("[Raiser] Mole move mode not set");
            return;
        }
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
        float newY;
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
            if (moleMovementMode == MoleMovementMode.Drag)
            {
                newY  = Math.Clamp(currentCameraY - delta * sensitivity, yMin, yMax);
            }
            else if (moleMovementMode == MoleMovementMode.Stand)
            {
                newY  = Math.Clamp(currentCameraY + delta * sensitivity, yMin, yMax);
            }
            else
            {
                newY = lastY;
            }
                
            lastY = currentControllerY;
        }
        else
        {
            controlling = false;
            newY = currentCameraY;
        }
        return newY;
    }

    /// <summary>
    /// Called by a UI Toggle's OnValueChanged event.
    /// isDrag == true  → Drag mode (hand controller drives height).
    /// isDrag == false → Stand mode (headset drives height).
    /// </summary>
    public void SetMovementMode(bool isDrag)
    {
        moleMovementMode = isDrag ? MoleMovementMode.Stand : MoleMovementMode.Drag;
        controller  = isDrag ? headset    : hand;
        sensitivity = isDrag ? headSensitivity : handSensitivity;
        controlling = false; // reset delta tracking when mode changes
    }

    void Update()
    {
        if (cameraOffset == null || controller == null) return;
        Vector3 pos = cameraOffset.localPosition;
        pos.y = ComputeNewY(pos.y);
        cameraOffset.localPosition = pos;
    }
}