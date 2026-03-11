using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation;

// Attach this script to each teleport point GameObject alongside TeleportationAnchor.
// Swaps to a glow material while the mole's teleport ray is hovering over this point.
[RequireComponent(typeof(TeleportationAnchor))]
[RequireComponent(typeof(Renderer))]
public class TeleportPointGlow : MonoBehaviour
{
    [Tooltip("The normal (default) material for the teleport point.")]
    public Material normalMaterial;

    [Tooltip("The glow material shown while the mole is aiming at this point.")]
    public Material glowMaterial;

    private TeleportationAnchor _anchor;
    private Renderer pointRenderer;

    void Awake()
    {
        _anchor = GetComponent<TeleportationAnchor>();
        pointRenderer = GetComponent<Renderer>();
    }

    void OnEnable()
    {
        _anchor.hoverEntered.AddListener(OnHoverEntered);
        _anchor.hoverExited.AddListener(OnHoverExited);
    }

    void OnDisable()
    {
        _anchor.hoverEntered.RemoveListener(OnHoverEntered);
        _anchor.hoverExited.RemoveListener(OnHoverExited);
    }

    private void OnHoverEntered(HoverEnterEventArgs args)
    {
        if (pointRenderer != null && glowMaterial != null)
            pointRenderer.material = glowMaterial;
    }

    private void OnHoverExited(HoverExitEventArgs args)
    {
        if (pointRenderer != null && normalMaterial != null)
            pointRenderer.material = normalMaterial;
    }
}
