using UnityEngine;

public static class OperativeBody
{

    public const float BaseHeight = 1.8f;
    public const float BaseRadius = 0.35f;

    public const float BaseEyeHeight = 1.62f;
    public const float BaseHeadLocalY = 1.72f;
    public const float BaseHeadScale = 0.38f;

    public static float ScaleFor(HeroRole role)
    {
        switch (role)
        {
            case HeroRole.Tank: return 1.32f;
            case HeroRole.Support: return 0.86f;
            default: return 1f;
        }
    }

    public static void ApplyCapsule(CapsuleCollider capsule, float scale)
    {
        if (capsule == null)
        {
            return;
        }

        capsule.height = BaseHeight * scale;
        capsule.radius = BaseRadius * Mathf.Lerp(1f, scale, 0.6f);
        capsule.center = new Vector3(0f, capsule.height * 0.5f, 0f);
    }
}
