using TMPro;
using UnityEngine;

/// <summary>
/// Updates the mole instruction board label when Leg-day mode is toggled.
/// Wire the same Toggle's OnValueChanged to this component's SetMovementMode,
/// alongside Raiser.SetMovementMode.
/// </summary>
public class MoleInstructionBoard : MonoBehaviour
{
    [Tooltip("The TextMeshPro label on the instruction board to update.")]
    public TextMeshProUGUI instructionLabel;

    private const string LegDayInstructions =
        "\u2022 Grip to aim and teleport\n" +
        "\u2022 hold Grip + stand to emerge, \n" +
        "\u2022 hold Grip + sit to hide\n\n" +
        "*  Make sure your chair is stable, and use your free hand on the table for balance";

    private const string DragInstructions =
        "\u2022  Raise your hand, then hold Grip and pull down to emerge \n" +
        "\u2022  Hold Grip and straighten your arm to hide";

    private void Start()
    {
        // Default to drag mode instructions on start
        if (instructionLabel != null)
            instructionLabel.text = DragInstructions;
    }

    /// <summary>
    /// Call this from the Leg-day Toggle's OnValueChanged event.
    /// isDrag == true  → Leg-day / Stand mode.
    /// isDrag == false → Drag mode.
    /// Mirrors the signature of Raiser.SetMovementMode so both can share the same Toggle event.
    /// </summary>
    public void SetMovementMode(bool isDrag)
    {
        if (instructionLabel == null) return;
        instructionLabel.text = isDrag ? LegDayInstructions : DragInstructions;
    }
}
