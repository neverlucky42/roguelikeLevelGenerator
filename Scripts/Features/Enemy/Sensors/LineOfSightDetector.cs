using UnityEngine;

public class LineOfSightDetector : MonoBehaviour
{
    public Transform head;
    public LayerMask playerMask;
    public float detectionRange;

    public GameObject? PerformDetection(GameObject potentialTarget)
    {
        RaycastHit hit;

        Vector3 direction = potentialTarget.transform.position - head.position;

        Physics.Raycast(head.position, direction, out hit, detectionRange, playerMask);

        if (hit.collider != null)
        {
            Debug.Log(hit.collider.gameObject);
            if (hit.collider.gameObject == potentialTarget)
            {
                Debug.DrawLine(head.position, hit.point, Color.red);
                return hit.collider.gameObject;
            }
        }
        return null;

    }

}
