using UnityEngine;
using static System.Net.WebRequestMethods;


public class PunchDetector : MonoBehaviour
{
    public Transform handPoint; // точка на косточке кулака
    public float hitRadius = 0.15f;
    public LayerMask hitLayer;

    public void Punch()
    {
        Collider[] hits = Physics.OverlapSphere(
        handPoint.position,
        hitRadius,
        hitLayer
        );

        foreach (Collider hit in hits)
        {
            Debug.Log("ѕопадание по: " + hit.name);
            if (hit.TryGetComponent<IDamagable>(out var damagable))
            {
                damagable.TakeDamage(1);
            }
        }
    }
    private void OnDrawGizmosSelected()
    {
        if (handPoint != null)
        {
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(handPoint.position, hitRadius);
        }
    }
}
