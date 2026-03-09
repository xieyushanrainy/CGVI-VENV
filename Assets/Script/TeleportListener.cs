using UnityEngine;
using UnityEngine.XR.Interaction.Toolkit;

public class TeleportListener : MonoBehaviour
{
    UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor[] anchors;
    public MoleVisibilityTracker moleVisibilityTracker;

    void Start()
    {
        anchors = FindObjectsOfType<UnityEngine.XR.Interaction.Toolkit.Locomotion.Teleportation.TeleportationAnchor>();

        foreach(var a in anchors)
        {
            a.selectEntered.AddListener(OnTeleport);
        }
    }

    void OnTeleport(SelectEnterEventArgs args)
    {
        var anchor = args.interactableObject.transform;
        var idScript = anchor.GetComponent<AnchorIdMarker>();

        if(idScript != null)
        {
            moleVisibilityTracker.SetActiveHole(idScript.id);
            Debug.Log("Teleport to ID: " + idScript.id);
        }
    }
}