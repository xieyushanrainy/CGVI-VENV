using UnityEngine;

/// <summary>
/// Hides the rope GameObject when Stand mode is active (toggle ON),
/// and shows it when Drag mode is active (toggle OFF).
///
/// Wire this alongside Raiser.SetMovementMode on the same UI Toggle's
/// OnValueChanged event — both components receive the same bool value.
/// </summary>
public class RopeVisibilityToggle : MonoBehaviour
{
    [Tooltip("The rope GameObject to show/hide.")]
    public GameObject ropeObject;

    /// <summary>
    /// Called by the UI Toggle's OnValueChanged event.
    /// isDrag == true  → Stand mode (headset drives height) → hide rope.
    /// isDrag == false → Drag mode (hand drives height) → show rope.
    /// </summary>
    public void SetMovementMode(bool isDrag)
    {
        if (ropeObject == null) return;
        ropeObject.SetActive(!isDrag);
    }
}
