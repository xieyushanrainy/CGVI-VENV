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

    void Update()
    {
        bool triggerPressed = triggerAction.action.IsPressed();

        if (triggerPressed)
        {
            float currentY = controller.localPosition.y;

            if (!controlling)
            {
                lastY = currentY;
                controlling = true;
                return;
            }

            float delta = currentY - lastY;

            Vector3 pos = cameraOffset.localPosition;
            pos.y += delta * sensitivity;
            pos.y = Math.Clamp(pos.y, yMin, yMax);

            cameraOffset.localPosition = pos;

            lastY = currentY;
        }
        else
        {
            controlling = false;
        }
    }
}