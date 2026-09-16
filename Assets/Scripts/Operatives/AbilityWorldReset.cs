using UnityEngine;

public static class AbilityWorldReset
{
    public static void ClearAll()
    {
        DestroyGameObjects<AbilityDeployable>();
        DestroyGameObjects<AbilityZone>();
        DestroyGameObjects<RootTrap>();
        DestroyGameObjects<StickyMine>();
        DestroyGameObjects<AbilityProjectile>();
        DestroyGameObjects<CipherClone>();
        DestroyGameObjects<MotionDetector>();
        DestroyGameObjects<WardenOrbit>();
        DestroyGameObjects<SeekingOrb>();
        DestroyGameObjects<HealTether>();

        DestroyComponents<BurnEffect>();
        DestroyComponents<GuardBuff>();
    }

    private static void DestroyGameObjects<T>() where T : Component
    {
        foreach (T item in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
        {
            if (item != null)
            {
                Object.Destroy(item.gameObject);
            }
        }
    }

    private static void DestroyComponents<T>() where T : Component
    {
        foreach (T item in Object.FindObjectsByType<T>(FindObjectsInactive.Include))
        {
            if (item != null)
            {
                Object.Destroy(item);
            }
        }
    }
}
