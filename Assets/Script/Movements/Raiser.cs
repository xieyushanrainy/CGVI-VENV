using UnityEngine;
using UnityEngine.InputSystem;

public class Raiser : MonoBehaviour
{
    public Transform controller;
    public Transform cameraOffset;

    public InputActionProperty triggerAction;

    public float sensitivity = 2f;

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
                // 第一次按下trigger，记录当前位置
                lastY = currentY;
                controlling = true;
                return;
            }

            float delta = currentY - lastY;

            Vector3 pos = cameraOffset.localPosition;
            pos.y += delta * sensitivity;

            cameraOffset.localPosition = pos;

            lastY = currentY;
        }
        else
        {
            controlling = false;
        }
    }
}