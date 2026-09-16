using UnityEngine;

public class HealBurstAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Heal Burst";
        cooldown = 8f;
        maxCharges = 2;
        aiHint = AiHint.TeamHeal;
    }

    public override float AiRange => 30f;

    protected override void OnActivate()
    {
        Vector3 origin = ctx.EyePosition + ctx.AimDirection * 0.5f;
        AbilityContext context = ctx;

        AbilityProjectile.Spawn(ctx, origin, ctx.AimDirection * 24f + Vector3.up * 1.5f, 0.6f, 0.4f, 3f,
            new Color(0.3f, 0.85f, 0.55f),
            (point, hit) =>
            {
                const float radius = 4f;
                foreach (Health health in CombatantRegistry.All)
                {
                    if (health == null || !health.IsAlive || health.Team.IsHostileTo(context.Team))
                    {
                        continue;
                    }

                    if ((health.transform.position + Vector3.up - point).sqrMagnitude > radius * radius)
                    {
                        continue;
                    }

                    float before = health.CurrentHealth;
                    health.Heal(75f);
                    float healed = health.CurrentHealth - before;
                    context.AbilityOwner?.ReportHealingDone(healed);
                    MatchStats.Instance?.RecordHeal(context.Health, health, healed);
                }

                AbilityFx.Flash(point, radius, new Color(0.3f, 0.9f, 0.55f));
            });
    }
}

public class SecondChanceAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Second Chance";
        cooldown = 25f;
        aiHint = AiHint.Revive;
    }

    protected override void OnActivate()
    {
        bool revived = MatchManager.Instance != null
            && MatchManager.Instance.TryReviveNearest(ctx.Transform.position + ctx.Transform.forward, ctx.Team);

        if (revived)
        {
            ctx.AbilityOwner?.ReportHealingDone(120f);
            AbilityFx.Flash(ctx.Transform.position + Vector3.up, 3f, new Color(0.4f, 0.95f, 0.6f));
        }
    }
}

public class UsTogetherAbility : Ability
{
    private const float LinkRange = 16f;

    protected override void Configure()
    {
        DisplayName = "Us Together";
        cooldown = 10f;
        aiHint = AiHint.TeamHeal;
    }

    public override float AiRange => LinkRange;

    protected override void OnActivate()
    {
        int linked = 0;
        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || health == ctx.Health || health.Team.IsHostileTo(ctx.Team) || health.IsDecoy)
            {
                continue;
            }

            if ((health.transform.position - ctx.Transform.position).sqrMagnitude > LinkRange * LinkRange)
            {
                continue;
            }

            HealTether.Spawn(ctx.Owner, ctx.Transform, health, 12f, 8f, new Color(0.3f, 0.85f, 0.55f));
            if (++linked >= 3)
            {
                break;
            }
        }

        AbilityFx.Flash(ctx.Transform.position + Vector3.up, 2f, new Color(0.3f, 0.85f, 0.55f));
    }
}

public class MassTransfusionAbility : Ability
{
    private AbilityZone zone;

    protected override void Configure()
    {
        DisplayName = "Mass Transfusion";
        activeDuration = 5f;
        cooldown = 3f;
        aiHint = AiHint.UltDefensive;
    }

    protected override void OnActivate()
    {
        zone = AbilityZone.Spawn(ctx, ctx.Transform.position, 8.5f, activeDuration, new Color(0.3f, 0.85f, 0.55f), "Mass Transfusion");
        zone.follow = ctx.Transform;
        zone.allyHealPerSecond = 24f;
        zone.allyOverhealTo = 45f;
        AbilityVisuals.Dome(zone.transform, 8.5f, new Color(0.3f, 0.85f, 0.55f));
    }

    protected override void OnTick(float deltaTime)
    {
        ctx.AbilityOwner?.ReportHealingDone(24f * deltaTime);
    }

    protected override void OnEnd()
    {
        if (zone != null)
        {
            Destroy(zone.gameObject);
        }
    }
}

public class HasteFieldAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Haste Field";
        cooldown = 12f;
        aiHint = AiHint.TeamShield;
    }

    public override float AiRange => 8f;

    protected override void OnActivate()
    {
        AbilityZone zone = AbilityZone.Spawn(
            ctx, ctx.Transform.position + Vector3.down * 0.9f, 6.5f, 6f,
            new Color(0.26f, 0.8f, 0.8f), "Haste Field");
        zone.allyMoveMult = 1.25f;
        zone.allyOutgoingMult = 1.1f;
    }
}

public class RepairAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Repair";
        cooldown = 12f;
        maxCharges = 2;
        aiHint = AiHint.TeamHeal;
    }

    protected override void OnActivate()
    {
        float before = ctx.Health.CurrentHealth;
        ctx.Health.Heal(25f);
        float healed = ctx.Health.CurrentHealth - before;
        ctx.AbilityOwner?.ReportHealingDone(healed);
        MatchStats.Instance?.RecordHeal(ctx.Health, ctx.Health, healed);
        AbilityFx.Flash(ctx.Transform.position + Vector3.up, 1.2f, new Color(0.26f, 0.8f, 0.8f));
    }
}

public class GuardAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Guard";
        cooldown = 3f;
        aiHint = AiHint.TeamShield;
    }

    public override float AiRange => 32f;

    protected override void OnActivate()
    {
        Health target = ctx.PreferredAllyTarget?.Invoke(35f) ?? AimAlly(35f) ?? ctx.Health;
        GuardBuff.Apply(target, ctx.Health, 25f, 4f, 12f);
        ctx.AbilityOwner?.ReportHealingDone(25f);
        AbilityFx.Flash(target.transform.position + Vector3.up, 1.6f, new Color(0.26f, 0.8f, 0.8f));
    }
}

public class NetworkAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Network";
        activeDuration = 8f;
        cooldown = 3f;
        aiHint = AiHint.UltUtility;
    }

    protected override void OnActivate()
    {
        AbilityFx.Flash(ctx.Transform.position, 3f, new Color(0.26f, 0.8f, 0.8f));
    }

    protected override void OnTick(float deltaTime)
    {
        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || health.Team.IsHostileTo(ctx.Team))
            {
                continue;
            }

            StatusEffects.For(health)?.Apply("network", 0.3f, move: 1.2f, incoming: 0.9f, outgoing: 1.15f);
        }
    }
}

public class LifeorbAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Lifeorb";
        cooldown = 6f;
        maxCharges = 3;
        aiHint = AiHint.TeamHeal;
    }

    public override float AiRange => 18f;

    protected override void OnActivate()
    {
        Vector3 origin = ctx.EyePosition + ctx.AimDirection * 0.5f;
        AbilityContext context = ctx;

        AbilityProjectile.Spawn(ctx, origin, ctx.AimDirection * 20f + Vector3.up * 2.5f, 1f, 0.35f, 3f,
            new Color(0.76f, 0.9f, 0.5f),
            (point, hit) =>
            {
                Health direct = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (direct == null || direct.Team.IsHostileTo(context.Team) || direct.IsDecoy)
                {
                    return;
                }

                const float radius = 3.2f;
                foreach (Health health in CombatantRegistry.All)
                {
                    if (health == null || !health.IsAlive || health.Team.IsHostileTo(context.Team) || health.IsDecoy)
                    {
                        continue;
                    }

                    if ((health.transform.position + Vector3.up - point).sqrMagnitude > radius * radius)
                    {
                        continue;
                    }

                    float before = health.CurrentHealth;
                    health.Heal(65f);
                    float healed = health.CurrentHealth - before;
                    context.AbilityOwner?.ReportHealingDone(healed);
                    MatchStats.Instance?.RecordHeal(context.Health, health, healed);
                }

                AbilityFx.Flash(point, radius, new Color(0.76f, 0.95f, 0.5f));
            });
    }
}

public class OrbitAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Orbit";
        cooldown = 12f;
        aiHint = AiHint.TeamShield;
    }

    protected override void OnActivate()
    {
        WardenOrbit.Spawn(ctx, 3, 8f, 20f, 15f, new Color(0.76f, 0.9f, 0.5f));
        AbilityFx.Flash(ctx.Transform.position, 1.6f, new Color(0.76f, 0.9f, 0.5f));
    }
}

public class TetherorbAbility : Ability
{
    private HealTether active;

    protected override void Configure()
    {
        DisplayName = "Tetherorb";
        cooldown = 10f;
        aiHint = AiHint.TeamHeal;
    }

    public override float AiRange => 22f;

    protected override void OnActivate()
    {
        if (active != null)
        {
            Destroy(active.gameObject);
            active = null;
        }

        Vector3 origin = ctx.EyePosition + ctx.AimDirection * 0.5f;
        AbilityContext context = ctx;
        TetherorbAbility self = this;

        AbilityProjectile.Spawn(ctx, origin, ctx.AimDirection * 28f, 0.2f, 0.3f, 3f,
            new Color(0.76f, 0.9f, 0.5f),
            (point, hit) =>
            {
                Health target = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (target != null && target.IsAlive && !target.Team.IsHostileTo(context.Team) && !target.IsDecoy)
                {
                    self.active = HealTether.Spawn(context.Owner, context.Transform, target, 8f, 6f, new Color(0.76f, 0.9f, 0.5f));
                }
            });
    }
}

public class OrbStormAbility : Ability
{
    private float nextOrbAt;

    protected override void Configure()
    {
        DisplayName = "Orb Storm";
        activeDuration = 6f;
        cooldown = 3f;
        aiHint = AiHint.UltDefensive;
    }

    protected override void OnActivate()
    {
        nextOrbAt = Time.time;
        AbilityFx.Flash(ctx.Transform.position, 3f, new Color(0.76f, 0.9f, 0.5f));
    }

    protected override void OnTick(float deltaTime)
    {
        if (Time.time < nextOrbAt)
        {
            return;
        }

        nextOrbAt = Time.time + 0.4f;
        Vector3 dir = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward;
        SeekingOrb.Spawn(ctx, ctx.Transform.position + Vector3.up + dir, dir, 15f, 40f, 20f, new Color(0.76f, 0.95f, 0.5f));
    }
}
