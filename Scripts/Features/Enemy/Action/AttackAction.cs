using System;
using Unity.Behavior;
using UnityEngine;
using Action = Unity.Behavior.Action;
using Unity.Properties;

[Serializable, GeneratePropertyBag]
[NodeDescription(name: "Attack", story: "[Agent] attack [Target] and play [Animator] Animation", category: "Action", id: "029b9b099c0331ae851d4fc3da77386b")]
public partial class AttackAction : Action
{
    [SerializeReference] public BlackboardVariable<UnityEngine.AI.NavMeshAgent> Agent;
    [SerializeReference] public BlackboardVariable<GameObject> Target;
    [SerializeReference] public BlackboardVariable<Animator> Animator;
    [SerializeReference] public BlackboardVariable<LayerMask> PlayerMask;

    void Attack()
    {

            Animator.Value.SetTrigger("EnemyAttack");

            Collider[] hit = Physics.OverlapSphere(Agent.Value.gameObject.transform.position, 2f, PlayerMask.Value);
            foreach (Collider col in hit)
            {
                if (col.TryGetComponent<IDamagable>(out IDamagable Player))
                {
                    Player.TakeDamage(1);
                }

            }

    }
    protected override Status OnStart()
    {
        return Status.Running;
    }

    protected override Status OnUpdate()
    {
        Attack();
        return Status.Success;
    }

    protected override void OnEnd()
    {
    }
}

