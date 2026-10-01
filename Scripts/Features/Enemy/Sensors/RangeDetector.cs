using UnityEngine;

public class RangeDetector : MonoBehaviour
{
    public float radius = 3f;
    public LayerMask mask;
    public GameObject DetectedTarget { get; set; }

    public GameObject UpdateDetector()
    {
        Collider[] hit = Physics.OverlapSphere(transform.position, radius, mask);
        if (hit.Length > 0) { DetectedTarget = hit[0].gameObject; }
        else { DetectedTarget = null; }
        return DetectedTarget;
    }
    private void OnDrawGizmos()
    {
        if (DetectedTarget != null)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.green;
        }
        Gizmos.DrawWireSphere(transform.position, radius);

    }
}
