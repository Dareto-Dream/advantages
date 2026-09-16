using UnityEngine;

public static class AbilityCc
{
    public static void Stun(Health target, float seconds)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        StatusEffects.For(target)?.ApplyStun(seconds);
        target.GetComponent<BotBrain>()?.ApplyStun(seconds);
    }

    public static void Silence(Health target, float seconds)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        StatusEffects.For(target)?.ApplySilence(seconds);
    }

    public static void Displace(Health target, Vector3 velocity)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        StatusEffects status = StatusEffects.For(target);
        if (status != null && status.KnockbackImmune)
        {
            return;
        }

        movement mover = target.GetComponent<movement>();
        if (mover != null)
        {
            mover.AddLaunch(velocity, 0.4f);
            return;
        }

        target.GetComponent<BotBrain>()?.ApplyKnockback(velocity);
    }
}
