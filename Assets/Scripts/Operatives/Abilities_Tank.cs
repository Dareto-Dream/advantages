using System.Collections.Generic;
using UnityEngine;

public class BarricadeAbility : Ability
{
    protected override void Configure()
    {
        DisplayName = "Barricade";
        cooldown = 12f;
        maxCharges = 2;
        activeDuration = 0f;
        aiHint = AiHint.Deployable;
    }

    protected override void OnActivate()
    {
        Vector3 forward = Vector3.ProjectOnPlane(ctx.AimDirection, Vector3.up).normalized;
        if (forward.sqrMagnitude < 0.01f)
        {
            forward = ctx.Transform.forward;
        }

        Vector3 pos = ctx.Transform.position + forward * 3.2f;
        if (Physics.Raycast(pos + Vector3.up * 3f, Vector3.down, out RaycastHit hit, 6f, ~0, QueryTriggerInteraction.Ignore))
        {
            pos = hit.point;
        }

        pos += Vector3.up * 1.15f;
        AbilityDeployable.Spawn(
            ctx, pos, Quaternion.LookRotation(forward),
            new Vector3(4.2f, 2.3f, 0.4f), 125f, 20f,
            new Color(0.30f, 0.45f, 0.72f), "Barricade");

        AbilityFx.Flash(pos, 2f, new Color(0.4f, 0.6f, 1f));
    }
}

public class RootAbility : Ability
{
    private RootTrap trap;

    protected override void Configure()
    {
        DisplayName = "Root";
        cooldown = 15f;
        activeDuration = 10f;
        aiHint = AiHint.Control;
    }

    public override float AiRange => 6f;

    protected override void OnActivate()
    {
        trap = RootTrap.Spawn(ctx, ctx.Transform.position, activeDuration, new Color(0.35f, 0.55f, 0.9f));
        AbilityFx.Flash(ctx.Transform.position, 2f, new Color(0.4f, 0.6f, 1f));
    }

    protected override void OnEnd()
    {
        if (trap != null)
        {
            Destroy(trap.gameObject);
        }
    }
}

public class WrathAbility : Ability
{
    private const int Pellets = 12;
    private const float PelletDamage = 11f;
    private const float Range = 14f;
    private const float ConeDegrees = 9f;

    protected override void Configure()
    {
        DisplayName = "Wrath";
        cooldown = 1.6f;
        aiHint = AiHint.OffensiveBurst;
    }

    public override float AiRange => 11f;

    private readonly RaycastHit[] hits = new RaycastHit[8];

    protected override void OnActivate()
    {
        Vector3 origin = ctx.EyePosition;
        Vector3 forward = ctx.AimDirection;
        Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
        Vector3 up = Vector3.Cross(forward, right);

        for (int i = 0; i < Pellets; i++)
        {
            Vector2 disc = Random.insideUnitCircle * Mathf.Tan(ConeDegrees * Mathf.Deg2Rad);
            Vector3 dir = (forward + right * disc.x + up * disc.y).normalized;
            Vector3 end = origin + dir * Range;

            int count = Physics.RaycastNonAlloc(origin, dir, hits, Range, ~0, QueryTriggerInteraction.Ignore);
            RaycastHit best = default;
            bool found = false;

            for (int h = 0; h < count; h++)
            {
                if (hits[h].collider.transform.IsChildOf(ctx.Transform))
                {
                    continue;
                }

                if (!found || hits[h].distance < best.distance)
                {
                    best = hits[h];
                    found = true;
                }
            }

            if (found)
            {
                end = best.point;

                Hitbox hitbox = best.collider.GetComponent<Hitbox>();
                IDamageable target = hitbox != null
                    ? (IDamageable)hitbox
                    : best.collider.GetComponentInParent<Health>();

                if (target != null && target.IsAlive)
                {
                    bool head = hitbox != null && hitbox.HitZone == Hitbox.Zone.Head;
                    float mult = head ? 2f
                        : hitbox != null && hitbox.HitZone == Hitbox.Zone.Limb ? 0.9f : 1f;

                    target.ApplyDamage(new DamageInfo(PelletDamage * mult, best.point, dir, ctx.Owner, "Wrath")
                    {
                        impactForce = 3f,
                        isHeadshot = head
                    });
                }
            }

            WeaponFx.Instance.PlayTracer(origin, end, new Color(0.6f, 0.78f, 1f));
        }

        AbilityFx.Flash(origin + forward * 1.2f, 1.4f, new Color(0.55f, 0.72f, 1f));
    }
}

public class FortressAbility : Ability
{
    private AbilityZone zone;

    protected override void Configure()
    {
        DisplayName = "Fortress";
        activeDuration = 8f;
        cooldown = 3f;
        aiHint = AiHint.UltDefensive;
    }

    protected override void OnActivate()
    {
        ctx.Status.Apply("fortress.root", activeDuration, move: 0.05f, knockbackImmune: true);

        zone = AbilityZone.Spawn(ctx, ctx.Transform.position, 7f, activeDuration, new Color(0.35f, 0.55f, 0.9f), "Fortress");
        zone.follow = ctx.Transform;
        zone.allyIncomingMult = 0.5f;
        zone.enemyDamagePerSecond = 12.5f;

        AbilityVisuals.Dome(zone.transform, 7f, new Color(0.35f, 0.55f, 0.9f));
        AbilityFx.Flash(ctx.Transform.position, 4f, new Color(0.4f, 0.6f, 1f));
    }

    protected override void OnEnd()
    {
        ctx.Status.Clear("fortress.root");
        if (zone != null)
        {
            Destroy(zone.gameObject);
        }
    }
}

public class DemolishAbility : Ability
{
    private const float Radius = 5f;

    protected override void Configure()
    {
        DisplayName = "Demolish";
        cooldown = 8f;
        aiHint = AiHint.OffensiveBurst;
    }

    public override float AiRange => 4.5f;

    protected override void OnActivate()
    {
        Vector3 center = ctx.Transform.position;
        bool chained = ctx.Status != null && ctx.Status.Has("rook.charge");
        float damage = chained ? 150f : 75f;

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || health == ctx.Health || !health.Team.IsHostileTo(ctx.Team))
            {
                continue;
            }

            Vector3 to = health.transform.position - center;
            if (to.sqrMagnitude > Radius * Radius)
            {
                continue;
            }

            health.ApplyDamage(new DamageInfo(damage, health.transform.position, to.normalized, ctx.Owner, "Demolish")
            {
                impactForce = 10f
            });

            AbilityCc.Displace(health, to.normalized * 9f + Vector3.up * 2f);
            AbilityCc.Stun(health, 1f);
            AbilityCc.Silence(health, 6f);
        }

        AbilityFx.Flash(center, Radius, chained ? new Color(1f, 0.4f, 0.2f) : new Color(0.9f, 0.5f, 0.3f));
    }
}

public class ChargeAbility : Ability
{
    private readonly HashSet<Health> struck = new HashSet<Health>();

    protected override void Configure()
    {
        DisplayName = "Charge";
        activeDuration = 0.35f;
        aiHint = AiHint.GapClose;

        usesFuel = true;
        fuelCapacity = 100f;
        fuelRegenPerSecond = 18f;
        fuelPerActivation = 45f;
    }

    protected override void OnActivate()
    {
        struck.Clear();

        Vector3 dir = Vector3.ProjectOnPlane(ctx.AimDirection, Vector3.up).normalized;
        if (dir.sqrMagnitude < 0.01f)
        {
            dir = ctx.Transform.forward;
        }

        SelfLaunch(dir * 19f + Vector3.up * 1.5f, 0.5f);
        ctx.Status?.Apply("rook.charge", 1.5f);
        AbilityFx.Flash(ctx.Transform.position, 1.5f, new Color(0.9f, 0.55f, 0.35f));
    }

    protected override void OnTick(float deltaTime)
    {
        Vector3 forward = ctx.Transform.forward;
        Vector3 probe = ctx.Transform.position + Vector3.up + forward * 1.5f;
        Vector3 side = Vector3.Cross(Vector3.up, forward).normalized;

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || health == ctx.Health || !health.Team.IsHostileTo(ctx.Team))
            {
                continue;
            }

            if (struck.Contains(health) || (health.transform.position + Vector3.up - probe).sqrMagnitude > 6.25f)
            {
                continue;
            }

            struck.Add(health);
            health.ApplyDamage(new DamageInfo(25f, health.transform.position, forward, ctx.Owner, "Charge")
            {
                impactForce = 10f
            });

            float sign = Vector3.Dot(health.transform.position - ctx.Transform.position, side) >= 0f ? 1f : -1f;
            AbilityCc.Displace(health, side * sign * 10f + Vector3.up);
        }
    }
}

public class CaptureAbility : Ability
{
    private Health grabbed;
    private int smacks;
    private float nextSmackTime;

    protected override void Configure()
    {
        DisplayName = "Capture";
        cooldown = 12f;
        activeDuration = 1.2f;
        aiHint = AiHint.OffensiveBurst;
    }

    public override float AiRange => 3.8f;

    protected override void OnActivate()
    {
        grabbed = FindGrabTarget();
        smacks = 0;
        nextSmackTime = Time.time;

        if (grabbed != null)
        {
            AbilityFx.Flash(grabbed.transform.position + Vector3.up, 1.5f, new Color(0.9f, 0.45f, 0.3f));
        }
    }

    protected override void OnTick(float deltaTime)
    {
        if (grabbed == null || !grabbed.IsAlive)
        {
            return;
        }

        AbilityCc.Stun(grabbed, 0.5f);

        movement mover = grabbed.GetComponent<movement>();
        if (mover != null)
        {
            Vector3 pin = ctx.Transform.position + ctx.Transform.forward * 2f;
            mover.Teleport(pin, grabbed.transform.eulerAngles.y);
        }

        if (smacks < 2 && Time.time >= nextSmackTime)
        {
            smacks++;
            nextSmackTime = Time.time + 0.6f;

            grabbed.ApplyDamage(new DamageInfo(125f, grabbed.transform.position, Vector3.down, ctx.Owner, "Capture")
            {
                impactForce = 8f
            });
            AbilityCc.Stun(grabbed, 2f);
            AbilityFx.Flash(grabbed.transform.position, 2f, new Color(0.9f, 0.4f, 0.3f));
        }
    }

    protected override void OnEnd()
    {
        grabbed = null;
    }

    private Health FindGrabTarget()
    {
        Vector3 origin = ctx.Transform.position;
        Vector3 forward = ctx.Transform.forward;
        Health best = null;
        float bestSqr = 4f * 4f;

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || health == ctx.Health || !health.Team.IsHostileTo(ctx.Team))
            {
                continue;
            }

            Vector3 to = health.transform.position - origin;
            if (to.sqrMagnitude > bestSqr || Vector3.Dot(forward, to.normalized) < 0.2f)
            {
                continue;
            }

            best = health;
            bestSqr = to.sqrMagnitude;
        }

        return best;
    }
}

public class CheckmateAbility : Ability
{
    private readonly List<Health> targets = new List<Health>();
    private int index;
    private float nextHopTime;

    protected override void Configure()
    {
        DisplayName = "Checkmate";
        activeDuration = 1.9f;
        cooldown = 3f;
        aiHint = AiHint.UltOffensive;
    }

    public override float AiRange => 20f;

    protected override void OnActivate()
    {
        targets.Clear();
        index = 0;
        nextHopTime = Time.time;

        Vector3 origin = ctx.Transform.position;
        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || !health.Team.IsHostileTo(ctx.Team))
            {
                continue;
            }

            if ((health.transform.position - origin).sqrMagnitude <= 22f * 22f)
            {
                targets.Add(health);
            }
        }

        targets.Sort((a, b) =>
            (a.transform.position - origin).sqrMagnitude.CompareTo((b.transform.position - origin).sqrMagnitude));

        if (targets.Count > 4)
        {
            targets.RemoveRange(4, targets.Count - 4);
        }

        AbilityFx.Flash(origin, 3f, new Color(0.9f, 0.4f, 0.3f));
    }

    protected override void OnTick(float deltaTime)
    {
        if (index >= targets.Count || Time.time < nextHopTime)
        {
            return;
        }

        Health target = targets[index++];
        nextHopTime = Time.time + 0.35f;

        if (target == null || !target.IsAlive)
        {
            return;
        }

        Vector3 away = target.transform.position - ctx.Transform.position;
        away.y = 0f;
        if (away.sqrMagnitude < 0.01f)
        {
            away = ctx.Transform.forward;
        }

        Vector3 approach = -away.normalized;
        Vector3 landing = target.transform.position + approach * 1.6f + Vector3.up * 0.1f;
        float yaw = Quaternion.LookRotation(away.normalized).eulerAngles.y;

        SelfTeleport(landing, yaw);
        target.ApplyDamage(new DamageInfo(200f, target.transform.position, away.normalized, ctx.Owner, "Checkmate")
        {
            impactForce = 10f
        });
        ExplodeDamage(target.transform.position, 3f, 90f, ctx.Team, ctx.Owner, "Checkmate");
        AbilityFx.Flash(target.transform.position + Vector3.up, 2.5f, new Color(0.9f, 0.4f, 0.3f));
    }
}
