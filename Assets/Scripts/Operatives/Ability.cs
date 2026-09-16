using UnityEngine;

public abstract class Ability : MonoBehaviour
{
    public enum Slot
    {
        Primary,
        Secondary,
        Special,
        Ultimate
    }

    public enum AiHint
    {
        OffensiveBurst,
        OffensivePoke,
        Control,
        Deployable,
        GapClose,
        Escape,
        Nova,
        TeamHeal,
        TeamShield,
        Revive,
        SelfMobility,
        UltOffensive,
        UltDefensive,
        UltUtility
    }

    public Slot slot = Slot.Primary;

    [SerializeField] protected float cooldown = 8f;
    [SerializeField] protected float activeDuration;
    [Tooltip("Stored uses. >1 turns 'cooldown' into the per-charge regeneration time.")]
    [SerializeField] protected int maxCharges = 1;

    protected bool usesFuel;
    protected float fuelCapacity = 100f;
    protected float fuelRegenPerSecond = 20f;
    protected float fuelPerActivation = 50f;
    private float fuel;

    protected AiHint aiHint = AiHint.OffensivePoke;
    public AiHint Ai => aiHint;

    public virtual float AiRange => 14f;

    public virtual float CooldownReductionPerHit => 0f;

    protected AbilityContext ctx;

    private float nextChargeReadyAt;
    private float activeUntil;
    private int charges = 1;
    private bool active;

    public string KeyLabel { get; set; } = "E";

    public string DisplayName { get; protected set; } = "Ability";
    public bool IsUltimate => slot == Slot.Ultimate;

    public bool IsActive => active;
    public int Charges => charges;
    public int MaxCharges => maxCharges;

    public bool UsesFuel => usesFuel;

    public float FuelFraction01 => !usesFuel || fuelCapacity <= 0f ? 0f : Mathf.Clamp01(fuel / fuelCapacity);

    public bool FuelReady => !usesFuel || fuel >= fuelPerActivation;

    public float CooldownRemaining => charges >= maxCharges ? 0f : Mathf.Max(0f, nextChargeReadyAt - Time.time);
    public bool OnCooldown => CooldownRemaining > 0.01f;

    public float CooldownFraction01 => cooldown <= 0f ? 0f : Mathf.Clamp01(CooldownRemaining / cooldown);

    public float ActiveFraction01 =>
        active && activeDuration > 0f ? Mathf.Clamp01((activeUntil - Time.time) / activeDuration) : 0f;

    public void Bind(AbilityContext context)
    {
        ctx = context;
        Configure();
        maxCharges = Mathf.Max(1, maxCharges);
        charges = maxCharges;
        fuel = fuelCapacity;
    }

    protected virtual void Configure()
    {
    }

    public bool CanActivate()
    {
        if (ctx == null || active)
        {
            return false;
        }

        if (usesFuel)
        {
            if (fuel < fuelPerActivation)
            {
                return false;
            }
        }
        else if (charges <= 0)
        {
            return false;
        }

        if (ctx.Health == null || !ctx.Health.IsAlive)
        {
            return false;
        }

        if (ctx.Status != null && ctx.Status.AbilitiesLocked)
        {
            return false;
        }

        return ReadyCheck();
    }

    protected virtual bool ReadyCheck()
    {
        return true;
    }

    public bool TryActivate()
    {
        if (!CanActivate())
        {
            return false;
        }

        OnActivate();

        if (activeDuration > 0f)
        {
            active = true;
            activeUntil = Time.time + activeDuration;
        }
        else
        {
            ConsumeCharge();
        }

        return true;
    }

    protected virtual void OnActivate()
    {
    }

    protected virtual void OnTick(float deltaTime)
    {
    }

    protected virtual void OnEnd()
    {
    }

    private void Update()
    {
        if (usesFuel && fuel < fuelCapacity)
        {
            fuel = Mathf.Min(fuelCapacity, fuel + fuelRegenPerSecond * Time.deltaTime);
        }

        RechargeTick();

        if (!active)
        {
            return;
        }

        OnTick(Time.deltaTime);

        if (Time.time >= activeUntil)
        {
            active = false;
            ConsumeCharge();
            OnEnd();
        }
    }

    private void RechargeTick()
    {
        if (charges >= maxCharges || Time.time < nextChargeReadyAt)
        {
            return;
        }

        charges++;
        if (charges < maxCharges)
        {
            nextChargeReadyAt = Time.time + cooldown;
        }
    }

    private void ConsumeCharge()
    {
        if (usesFuel)
        {
            fuel = Mathf.Max(0f, fuel - fuelPerActivation);
            return;
        }

        bool wasFull = charges >= maxCharges;
        charges = Mathf.Max(0, charges - 1);

        if (wasFull)
        {
            nextChargeReadyAt = Time.time + cooldown;
        }
    }

    public void ForceReset()
    {
        if (active)
        {
            active = false;
            OnEnd();
        }

        charges = maxCharges;
        nextChargeReadyAt = 0f;
        fuel = fuelCapacity;
    }

    public void ClearCooldown()
    {
        charges = maxCharges;
        nextChargeReadyAt = 0f;
        fuel = fuelCapacity;
    }

    public void ReduceCooldown(float seconds)
    {
        if (charges < maxCharges && nextChargeReadyAt > Time.time)
        {
            nextChargeReadyAt -= seconds;
        }
    }

    protected void SelfLaunch(Vector3 velocity, float unclampedSeconds)
    {
        ctx.RequestLaunch?.Invoke(velocity, unclampedSeconds);
    }

    protected void SelfTeleport(Vector3 position, float yaw)
    {
        ctx.RequestTeleport?.Invoke(position, yaw);
    }

    protected bool AimRaycast(float maxDistance, out RaycastHit hit)
    {
        return Physics.Raycast(ctx.EyePosition, ctx.AimDirection, out hit, maxDistance, ~0, QueryTriggerInteraction.Ignore);
    }

    protected Vector3 AimPoint(float maxDistance)
    {
        return AimRaycast(maxDistance, out RaycastHit hit)
            ? hit.point
            : ctx.EyePosition + ctx.AimDirection * maxDistance;
    }

    protected Health AimAlly(float maxDistance)
    {
        if (!AimRaycast(maxDistance, out RaycastHit hit))
        {
            return null;
        }

        Health health = hit.collider.GetComponentInParent<Health>();
        if (health == null || health == ctx.Health || !health.IsAlive)
        {
            return null;
        }

        return health.Team.IsHostileTo(ctx.Team) ? null : health;
    }

    public static void ExplodeDamage(
        Vector3 center,
        float radius,
        float maxDamage,
        Team attackerTeam,
        GameObject instigator,
        string sourceName)
    {
        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || !health.Team.IsHostileTo(attackerTeam))
            {
                continue;
            }

            float distance = Vector3.Distance(center, health.transform.position + Vector3.up * 1f);
            if (distance > radius)
            {
                continue;
            }

            float falloff = 1f - distance / radius;
            float amount = maxDamage * Mathf.Clamp01(0.35f + 0.65f * falloff);
            Vector3 push = (health.transform.position - center).normalized;
            health.ApplyDamage(new DamageInfo(amount, health.transform.position, push, instigator, sourceName)
            {
                impactForce = 6f
            });
        }
    }
}
