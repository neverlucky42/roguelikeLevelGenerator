using UnityEngine;
using UnityEngine.InputSystem;

public class InputController : MonoBehaviour
{
    private Vector2 m_LStick;
    private Animator m_Animator;
    public float RotationSpeed = 10f;
    public float animationSmooth = 0.1f;
    private float IdleIndex = 0;
    public float SpeedMultiplier = 1.0f;
    public GUIManager GUI_M;
    public CharacterController CRPlayer;
    public float gravity = -9.81f;
    public float verticalVelocity;
    public void Awake()
    {
        m_Animator = GetComponent<Animator>();
        CRPlayer = GetComponent<CharacterController>();
    }
    void Start()
    {
        InvokeRepeating("PlayRandomide", 3, 3);
    }

    
    void Update()
    {
        Vector3 Move = new Vector3(m_LStick.x, 0, m_LStick.y);
        if (Move.sqrMagnitude > 0.001f) 
        {
            Quaternion TargetRotate = Quaternion.LookRotation(Move, Vector3.up);
            transform.rotation = Quaternion.Slerp(transform.rotation, TargetRotate, RotationSpeed * Time.deltaTime);
        }
        m_Animator.SetFloat("X", Move.magnitude, 0.1f, Time.deltaTime);
        m_Animator.SetFloat("IdleIndex", IdleIndex, animationSmooth, Time.deltaTime);
    }
    
    public void OnMove(InputAction.CallbackContext Context)
    {
        m_LStick = Context.ReadValue<Vector2>();
    }
    public void OnFire(InputAction.CallbackContext Context)
    {
        if (Context.performed)
        {
            m_Animator.SetTrigger("MeleeAttack");
            Debug.Log("fire");
        }
    }
    public void OnDash(InputAction.CallbackContext Context)
    {
        if (Context.performed)
        {
            Debug.Log("dash");
        }
    }
    public void PlayRandomide()
    {
        IdleIndex = Random.Range(0, 4);
    }
    public void OnAnimatorMove()
    {
        Vector3 Delta = m_Animator.deltaPosition;
        //Delta *= SpeedMultiplier;
        //transform.position += Delta;
        //transform.rotation *= m_Animator.deltaRotation;
        if (CRPlayer.isGrounded)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity += gravity * Time.deltaTime;
        }
        Delta.y = verticalVelocity * Time.deltaTime;
        CRPlayer.Move(Delta);
    }


    [ContextMenu("SpeedUp 1")]
    void SpeedUp()
    {
        SetSpeed(2);
    }
    [ContextMenu("SpeedDown 1")]
    void SpeedDown()
    {
        SetSpeed(1);
    }
    void SetSpeed(float speed)
    {
        SpeedMultiplier = speed;
        GUI_M.SetSpeed(SpeedMultiplier);
    }
}
