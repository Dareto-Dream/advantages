using System;
using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Health))]
public class BotOperative : MonoBehaviour, IAbilityOwner
{
    [SerializeField] private float chargePerDamageDealt = 0.0016f;
    [SerializeField] private float chargePerHealDone = 0.0013f;

    private Health health;
    private StatusEffects status;
    private BotBrain brain;
    private AbilityContext context;

    private OperativeId id = OperativeId.Bulwark;
    private readonly List<Ability> abilities = new List<Ability>();
    private Ability ultimate;
    private float ultCharge;

    public OperativeId Id => id;
    public IReadOnlyList<Ability> Abilities => abilities;
    public float UltCharge01 => Mathf.Clamp01(ultCharge);
    public bool UltReady => ultCharge >= 1f;

    public event Action<Ability.Slot> AbilityCast;

    private void Awake()
    {
        health = GetComponent<Health>();
        status = GetComponent<StatusEffects>();
        if (status == null)
        {
            status = gameObject.AddComponent<StatusEffects>();
        }

        brain = GetComponent<BotBrain>();
        context = BuildContext();
    }

    private void Update()
    {
        if (ultCharge < 1f && CanCharge())
        {
            float seconds = Mathf.Max(1f, OperativeRoster.Get(id).ultChargeSeconds);
            ultCharge = Mathf.Min(1f, ultCharge + Time.deltaTime / seconds);
        }
    }

    private AbilityContext BuildContext()
    {
        return new AbilityContext
        {
            Owner = gameObject,
            Transform = transform,
            Health = health,
            Status = status,
            AbilityOwner = this,
            RequestLaunch = (velocity, seconds) => brain?.NavLaunch(velocity, seconds),
            RequestTeleport = (position, yaw) => brain?.NavBlink(position, yaw),
            PreferredAllyTarget = range => brain != null ? brain.PickAllyToSupport(range) : health
        };
    }

    public void SetOperative(OperativeId newId)
    {
        id = newId;

        foreach (Ability existing in abilities)
        {
            if (existing != null)
            {
                Destroy(existing);
            }
        }

        abilities.Clear();
        ultimate = null;

        foreach (OperativeDefinition.AbilitySlot def in OperativeRoster.Get(id).abilities)
        {
            Ability ability = (Ability)gameObject.AddComponent(def.type);
            ability.slot = def.slot;
            ability.KeyLabel = def.key;
            ability.Bind(context);
            abilities.Add(ability);

            if (def.slot == Ability.Slot.Ultimate)
            {
                ultimate = ability;
            }
        }

        ultCharge = 0f;
    }

    public Ability Get(Ability.Slot slot)
    {
        foreach (Ability ability in abilities)
        {
            if (ability != null && ability.slot == slot)
            {
                return ability;
            }
        }

        return null;
    }

    public Ability GetAbility(Ability.Slot slot) => Get(slot);

    public bool TryCast(Ability.Slot slot)
    {
        Ability ability = Get(slot);
        if (ability == null)
        {
            return false;
        }

        if (slot == Ability.Slot.Ultimate && !UltReady)
        {
            return false;
        }

        if (!ability.TryActivate())
        {
            return false;
        }

        if (slot == Ability.Slot.Ultimate)
        {
            ultCharge = 0f;
        }
        else
        {
            AbilityCast?.Invoke(slot);
        }

        return true;
    }

    public void OnWeaponHit()
    {
        foreach (Ability ability in abilities)
        {
            if (ability != null && ability.CooldownReductionPerHit > 0f)
            {
                ability.ReduceCooldown(ability.CooldownReductionPerHit);
            }
        }
    }

    public void ReportDamageDealt(float amount)
    {
        if (CanCharge())
        {
            AddCharge(amount * chargePerDamageDealt);
        }
    }

    public void ReportHealingDone(float amount)
    {
        if (CanCharge())
        {
            AddCharge(amount * chargePerHealDone);
        }
    }

    public void ResetForRespawn()
    {
        foreach (Ability ability in abilities)
        {
            ability?.ForceReset();
        }
    }

    private void AddCharge(float amount)
    {
        if (amount > 0f && ultCharge < 1f)
        {
            ultCharge = Mathf.Min(1f, ultCharge + amount);
        }
    }

    private bool CanCharge()
    {
        MatchManager match = MatchManager.Instance;
        bool live = match != null
            && (match.CurrentPhase == MatchManager.Phase.Live || match.CurrentPhase == MatchManager.Phase.Overtime);

        return live && health != null && health.IsAlive;
    }
}
