using UnityEngine;

public class CipherLocomotion : MonoBehaviour
{
    [SerializeField] private Animator animator;
    [SerializeField] private movement mover;

    private static readonly int SpeedParam = Animator.StringToHash("Speed");
    private static readonly int GroundedParam = Animator.StringToHash("Grounded");
    private static readonly int JumpParam = Animator.StringToHash("Jump");

    private void Awake()
    {
        if (animator == null) animator = GetComponent<Animator>();
        if (mover == null) mover = GetComponentInParent<movement>();
    }

    private void OnEnable()
    {
        if (mover != null) mover.Jumped += HandleJumped;
    }

    private void OnDisable()
    {
        if (mover != null) mover.Jumped -= HandleJumped;
    }

    private void Update()
    {
        if (animator == null || mover == null)
        {
            return;
        }

        float normalizedSpeed = mover.MoveSpeed > 0.01f ? mover.HorizontalSpeed / mover.MoveSpeed : 0f;
        animator.SetFloat(SpeedParam, normalizedSpeed);
        animator.SetBool(GroundedParam, mover.IsGrounded);
    }

    private void HandleJumped()
    {
        if (animator != null)
        {
            animator.SetTrigger(JumpParam);
        }
    }
}
