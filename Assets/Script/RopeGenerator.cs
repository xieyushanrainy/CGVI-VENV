using UnityEngine;

public class RopeGenerator : MonoBehaviour
{
    public GameObject segmentPrefab;
    public int segmentCount = 10;
    public float segmentLength = 1.0f;

    public float distance = 0.1f;

    void Start()
    {
        Vector3[] directions = new Vector3[]
        {
            new Vector3( distance, 0, 0),
            new Vector3(-distance, 0, 0), 
            new Vector3(0, 0,  distance),
            new Vector3(0, 0, -distance)
        };

        foreach (var offset in directions)
        {
            CreateRope(offset);
        }
    }

    void CreateRope(Vector3 offset)
    {
        Rigidbody prevBody = null;

        for (int i = 0; i < segmentCount; i++)
        {
            GameObject seg = Instantiate(segmentPrefab, transform);

            seg.transform.localPosition = offset + new Vector3(0, -i * segmentLength, 0);

            Rigidbody rb = seg.GetComponent<Rigidbody>();

            if (i == 0)
            {
                rb.isKinematic = true;
            }
            else  
            {  
                HingeJoint joint = seg.AddComponent<HingeJoint>();  
                joint.connectedBody = prevBody;

                joint.autoConfigureConnectedAnchor = false;
                joint.anchor = new Vector3(0, segmentLength / 2f, 0);
                joint.connectedAnchor = new Vector3(0, -segmentLength / 2f, 0);

                //if (i == 1)
                    rb.AddTorque(Vector3.forward * 0.5f, ForceMode.Impulse);
            }

            prevBody = rb;
        }
    }
}