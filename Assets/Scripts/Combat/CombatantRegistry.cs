using System.Collections.Generic;

public static class CombatantRegistry
{
    private static readonly List<Health> all = new List<Health>();

    public static IReadOnlyList<Health> All => all;

    public static void Register(Health health)
    {
        if (health != null && !all.Contains(health))
        {
            all.Add(health);
        }
    }

    public static void Unregister(Health health)
    {
        all.Remove(health);
    }

    public static int AliveCount(Team team)
    {
        int count = 0;
        foreach (Health health in all)
        {
            if (health != null && health.IsAlive && health.Team == team && !health.IsDecoy)
            {
                count++;
            }
        }

        return count;
    }
}
