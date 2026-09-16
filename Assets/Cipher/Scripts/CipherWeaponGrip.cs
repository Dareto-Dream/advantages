using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Animator))]
public sealed class CipherWeaponGrip : MonoBehaviour
{
    public Transform leftHandTarget;
    [Range(0, 1)] public float weight = 1;
    Animator animator;
    void Awake() { animator = GetComponent<Animator>(); }
    void OnAnimatorIK(int layerIndex)
    {
        if (animator == null) animator = GetComponent<Animator>();
        float w = leftHandTarget != null ? weight : 0;
        animator.SetIKPositionWeight(AvatarIKGoal.LeftHand, w);
        animator.SetIKRotationWeight(AvatarIKGoal.LeftHand, w);
        if (w > 0)
        {
            animator.SetIKPosition(AvatarIKGoal.LeftHand, leftHandTarget.position);
            animator.SetIKRotation(AvatarIKGoal.LeftHand, leftHandTarget.rotation);
        }
    }
}
