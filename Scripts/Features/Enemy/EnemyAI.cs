//using UnityEngine;
//using UnityEngine.AI;

//public class EnemyAI : MonoBehaviour
//{
//    public Transform Player;
//    public NavMeshAgent EnemyAgent;
//    public float attackDistance = 5f;
//    public Animator EnemyAnimator;
//    public float attackCooldown = 1.0f;
//    private float lastAttackTime;
//    public Enemy enemy;
//    public LayerMask PlayerMask;

//    private void Awake()
//    {
//        EnemyAgent = GetComponent<NavMeshAgent>();
//        EnemyAnimator = GetComponent<Animator>();
//        enemy = GetComponent<Enemy>();
//    }
//    void Start()
//    {
//        lastAttackTime = -attackCooldown;
//    }


//    void Update()
//    {
//        if (enemy.IsDead)
//        {
//            return;
//        }

//        if (Player == null)
//        {
//            return;
//        }
//        float Distance = Vector3.Distance(Player.position, transform.position);

//        if (Distance > attackDistance)
//        {
//            EnemyAgent.isStopped = false;
//            EnemyAgent.SetDestination(Player.position);
//            EnemyAnimator.SetFloat("Speed", EnemyAgent.velocity.magnitude);
//        }
//        else
//        {
//            EnemyAgent.isStopped = true;
//            EnemyAnimator.SetFloat("Speed", 0);
//            attack();
//        }
//    }
//    void attack()
//    {
//        if (Time.time - lastAttackTime >= attackCooldown)
//        {
//            EnemyAnimator.SetTrigger("EnemyAttack");
//            lastAttackTime = Time.time;

//            Collider[] hit = Physics.OverlapSphere(transform.position, 2f, PlayerMask);
//            foreach (Collider col in hit)
//            {
//                if (col.TryGetComponent<IDamagable>(out IDamagable Player))
//                {
//                    Player.TakeDamage(1);
//                }

//            }
//        }

//    }
//}
