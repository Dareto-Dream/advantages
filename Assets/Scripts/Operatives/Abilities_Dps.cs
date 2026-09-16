using UnityEngine;

public class SpotterAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Spotter Drone";
        cooldown = 16f;
        maxCharges = 3;
        aiHint = AiHint.Deployable;
    }

    public override float AiRange => 26f;

    protected override void OnActivate()
    {
        Vector3 point = AimPoint(32f) + Vector3.up * 0.6f;

        AbilityDeployable deployable = AbilityDeployable.Spawn(
            ctx, point, Quaternion.identity,
            new Vector3(0.4f, 1.2f, 0.4f), 70f, 14f,
            new Color(0.85f, 0.30f, 0.36f), "Spotter");

        AbilityZone zone = deployable.AddZone(new Color(0.85f, 0.30f, 0.36f));
        zone.radius = 20f;
        zone.outlineEnemies = true;

        AbilityFx.Flash(point, 1.5f, new Color(0.85f, 0.3f, 0.36f));
    }
}

public class ViperDashAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Dash";
        cooldown = 8f;
        aiHint = AiHint.GapClose;
    }

    public override float AiRange => 30f;

    protected override void OnActivate()
    {
        ctx.Status.Apply("viper.dash", 2f, move: 1.6f);
        AbilityFx.Flash(ctx.Transform.position, 1.2f, new Color(0.95f, 0.55f, 0.6f));
    }
}

public class NewHeightsAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "New Heights";
        cooldown = 8f;
        aiHint = AiHint.SelfMobility;
    }

    protected override bool ReadyCheck()
    {
        return ctx.Movement != null && !ctx.Movement.IsGrounded;
    }

    protected override void OnActivate()
    {
        ctx.Movement.AirJump();
        AbilityFx.Flash(ctx.Transform.position, 1.4f, new Color(0.95f, 0.6f, 0.65f));
    }
}

public class DeadeyeAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Deadeye Protocol";
        activeDuration = 6f;
        cooldown = 3f;
        aiHint = AiHint.UltOffensive;
    }

    protected override void OnActivate()
    {
        ctx.Status.Apply("deadeye", activeDuration, outgoing: 1.6f);
        AbilityFx.Flash(ctx.Transform.position, 2.5f, new Color(0.85f, 0.3f, 0.36f));
    }

    protected override void OnTick(float deltaTime)
    {
        foreach (Health health in CombatantRegistry.All)
        {
            if (health != null && health.IsAlive && health.Team.IsHostileTo(ctx.Team))
            {
                StatusEffects.For(health)?.ApplyOutline(0.3f, StatusEffects.OutlineScope.Team, ctx.Health);
            }
        }
    }
}

public class BurstAbility : Ability
{
    private const float Radius = 8f;

    protected override void Configure()
    {
        DisplayName = "Burst";
        cooldown = 10f;
        aiHint = AiHint.Nova;
    }

    public override float AiRange => 6f;

    protected override void OnActivate()
    {
        Vector3 origin = ctx.Transform.position + Vector3.up;

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive)
            {
                continue;
            }

            if ((health.transform.position + Vector3.up - origin).sqrMagnitude > Radius * Radius)
            {
                continue;
            }

            if (health.Team.IsHostileTo(ctx.Team))
            {
                health.ApplyDamage(new DamageInfo(40f, health.transform.position,
                    (health.transform.position - origin).normalized, ctx.Owner, "Burst"));
            }
            else
            {
                float before = health.CurrentHealth;
                health.Heal(40f);
                float healed = health.CurrentHealth - before;
                ctx.AbilityOwner?.ReportHealingDone(healed);
                MatchStats.Instance?.RecordHeal(ctx.Health, health, healed);
            }
        }

        for (int i = 0; i < 12; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 30f, 0f) * Vector3.forward;
            WeaponFx.Instance.PlayTracer(origin, origin + dir * Radius, new Color(1f, 0.9f, 0.4f));
        }

        AbilityFx.Flash(origin, Radius, new Color(1f, 0.85f, 0.4f));
    }
}

public class BlinkDashAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Blink Dash";
        cooldown = 9f;
        aiHint = AiHint.Escape;
    }

    public override float CooldownReductionPerHit => 0.1f;

    protected override void OnActivate()
    {
        Vector3 dir;
        Vector2 moveInput = ctx.Movement != null ? ctx.Movement.MoveInput : Vector2.zero;

        if (moveInput.sqrMagnitude > 0.04f)
        {
            Vector3 forward = Vector3.ProjectOnPlane(ctx.Transform.forward, Vector3.up).normalized;
            Vector3 right = Vector3.ProjectOnPlane(ctx.Transform.right, Vector3.up).normalized;
            dir = (forward * moveInput.y + right * moveInput.x).normalized;
        }
        else
        {
            dir = Vector3.ProjectOnPlane(ctx.AimDirection, Vector3.up).normalized;
        }

        if (dir.sqrMagnitude < 0.01f)
        {
            dir = ctx.Transform.forward;
        }

        SelfLaunch(dir * 15f + Vector3.up * 1.5f, 0.4f);
        AbilityFx.Flash(ctx.Transform.position, 1.5f, new Color(0.95f, 0.8f, 0.3f));
    }
}

public class RevertAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Revert";
        cooldown = 12f;
        aiHint = AiHint.Escape;
    }

    protected override void OnActivate()
    {
        PositionHistory history = ctx.Owner.GetComponent<PositionHistory>();
        if (history != null && history.TryGet(3f, out PositionHistory.Snapshot snap))
        {
            SelfTeleport(snap.position, snap.yaw);
            ctx.Health.RestorePools(snap.health, snap.armor);
        }

        ctx.Weapons?.ForceReload();
        AbilityFx.Flash(ctx.Transform.position + Vector3.up, 2f, new Color(0.7f, 0.9f, 1f));
    }
}

public class OverdriveAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Overdrive";
        activeDuration = 6f;
        cooldown = 3f;
        aiHint = AiHint.UltUtility;
    }

    protected override void OnActivate()
    {
        ctx.Status.Apply("overdrive", activeDuration, move: 1.45f);
        AbilityFx.Flash(ctx.Transform.position, 2f, new Color(0.95f, 0.76f, 0.26f));
    }

    protected override void OnTick(float deltaTime)
    {
        ctx.AbilityOwner?.GetAbility(Ability.Slot.Secondary)?.ClearCooldown();
    }

    protected override void OnEnd()
    {
        ctx.Status.Clear("overdrive");
    }
}

public class StickyMineAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Sticky Mine";
        cooldown = 8f;
        maxCharges = 2;
        aiHint = AiHint.OffensiveBurst;
    }

    public override float AiRange => 20f;

    protected override void OnActivate()
    {
        Vector3 origin = ctx.EyePosition + ctx.AimDirection * 0.6f;
        StickyMine.Spawn(ctx, origin, ctx.AimDirection * 22f + Vector3.up * 2f, 75f, 4.5f);
    }
}

public class GotchaAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Gotcha";
        cooldown = 8f;
        aiHint = AiHint.Control;
    }

    public override float AiRange => 24f;

    protected override void OnActivate()
    {
        Vector3 origin = ctx.EyePosition + ctx.AimDirection * 0.5f;
        GameObject owner = ctx.Owner;
        Team team = ctx.Team;

        AbilityProjectile.Spawn(ctx, origin, ctx.AimDirection * 34f, 0.15f, 0.5f, 24f,
            new Color(0.8f, 0.85f, 0.95f),
            (point, hit) =>
            {
                Health target = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (target != null && target.IsAlive && target.Team.IsHostileTo(team))
                {
                    AbilityCc.Stun(target, 2f);
                    AbilityFx.Flash(target.transform.position + Vector3.up, 1.8f, new Color(0.8f, 0.85f, 0.95f));
                }
            });
    }
}

public class MolotovAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Molotov";
        cooldown = 18f;
        aiHint = AiHint.Control;
    }

    public override float AiRange => 26f;

    protected override void OnActivate()
    {
        Vector3 origin = ctx.EyePosition + ctx.AimDirection * 0.5f;
        GameObject owner = ctx.Owner;
        Team team = ctx.Team;
        AbilityContext context = ctx;

        AbilityProjectile.Spawn(ctx, origin, ctx.AimDirection * 26f + Vector3.up * 3f, 1f, 0.3f, 4f,
            new Color(1f, 0.5f, 0.15f),
            (point, hit) =>
            {
                Health target = hit != null ? hit.GetComponentInParent<Health>() : null;
                if (target != null && target.IsAlive && target.Team.IsHostileTo(team))
                {
                    BurnEffect.Apply(target, owner, 15f, 10f);
                }

                AbilityZone fire = AbilityZone.Spawn(context, point, 4.5f, 10f, new Color(1f, 0.45f, 0.12f), "Molotov");
                fire.enemyDamagePerSecond = 15f;
                AbilityFx.Flash(point, 4f, new Color(1f, 0.5f, 0.15f));
            });
    }
}

public class MeltdownAbility : Ability
{
    private AbilityZone zone;
    private float nextBoomTime;

    protected override void Configure()
    {
        DisplayName = "Meltdown";
        activeDuration = 5f;
        cooldown = 3f;
        aiHint = AiHint.UltOffensive;
    }

    public override float AiRange => 30f;

    protected override void OnActivate()
    {
        zone = AbilityZone.Spawn(ctx, AimPoint(40f), 9f, activeDuration, new Color(0.95f, 0.5f, 0.2f), "Meltdown");
        zone.enemyDamagePerSecond = 42f;
        nextBoomTime = 0f;
    }

    protected override void OnTick(float deltaTime)
    {
        if (zone == null || Time.time < nextBoomTime)
        {
            return;
        }

        nextBoomTime = Time.time + 0.5f;
        Vector2 jitter = Random.insideUnitCircle * 8f;
        AbilityFx.Flash(zone.transform.position + new Vector3(jitter.x, 0.5f, jitter.y), 3f, new Color(1f, 0.5f, 0.15f));
    }

    protected override void OnEnd()
    {
        if (zone != null)
        {
            Destroy(zone.gameObject);
        }
    }
}

public class ReconScanAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Recon Scan";
        cooldown = 12f;
        aiHint = AiHint.Control;
    }

    public override float AiRange => 22f;

    protected override void OnActivate()
    {
        AbilityFx.Flash(ctx.Transform.position, 3f, new Color(0.55f, 0.45f, 0.95f));

        Vector3 origin = ctx.Transform.position;
        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || !health.Team.IsHostileTo(ctx.Team))
            {
                continue;
            }

            if ((health.transform.position - origin).sqrMagnitude <= 26f * 26f)
            {
                StatusEffects.For(health)?.ApplyOutline(5f, StatusEffects.OutlineScope.Caster, ctx.Health, showHealth: true);
            }
        }
    }
}

public class DeceptionAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Deception";
        cooldown = 15f;
        aiHint = AiHint.Escape;
    }

    protected override void OnActivate()
    {
        CipherClone.Spawn(ctx);
        AbilityFx.Flash(ctx.Transform.position, 2f, new Color(0.56f, 0.46f, 0.95f));
    }
}

public class AllSeeingAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "All-seeing";
        cooldown = 14f;
        maxCharges = 3;
        aiHint = AiHint.Deployable;
    }

    public override float AiRange => 26f;

    protected override void OnActivate()
    {
        MotionDetector.Spawn(ctx, AimPoint(28f), new Color(0.56f, 0.46f, 0.95f));
        AbilityFx.Flash(ctx.Transform.position, 1.5f, new Color(0.56f, 0.46f, 0.95f));
    }
}

public class BlackoutAbility : Ability
{
    private AbilityZone zone;

    protected override void Configure()
    {
        DisplayName = "Blackout";
        activeDuration = 6f;
        cooldown = 3f;
        aiHint = AiHint.UltUtility;
    }

    public override float AiRange => 28f;

    protected override void OnActivate()
    {
        zone = AbilityZone.Spawn(ctx, AimPoint(35f), 11f, activeDuration, new Color(0.4f, 0.35f, 0.7f), "Blackout");
        zone.enemyLockAbilities = true;
        zone.outlineEnemies = true;
        zone.enemyMoveMult = 0.9f;
    }

    protected override void OnEnd()
    {
        if (zone != null)
        {
            Destroy(zone.gameObject);
        }
    }
}
