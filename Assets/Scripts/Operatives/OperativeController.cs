using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Health))]
public class OperativeController : MonoBehaviour, IAbilityOwner
{
    [Header("Parts")]
    [SerializeField] private Health health;
    [SerializeField] private movement mover;
    [SerializeField] private PlayerLook look;
    [SerializeField] private WeaponController weapons;
    [SerializeField] private Loadout loadout;
    [SerializeField] private StatusEffects status;
    [SerializeField] private OperativeCosmetics cosmetics;

    [Header("Ultimate charge")]
    [Tooltip("Charge gained per point of damage dealt to enemies.")]
    [SerializeField] private float chargePerDamageDealt = 0.0016f;
    [Tooltip("Charge gained per point of healing done to allies (the support equivalent of damage).")]
    [SerializeField] private float chargePerHealDone = 0.0013f;

    private OperativeId id = OperativeId.Bulwark;
    private readonly List<Ability> abilities = new List<Ability>();
    private Ability ultimate;
    private float ultCharge;
    private bool inputEnabled = true;

    private bool netDriven;
    private byte netAbilityEdges;
    private AbilityContext context;

    public event Action UltimateReady;

    public event Action<Ability.Slot> AbilityCast;

    public OperativeId Id => id;
    public OperativeDefinition Definition => OperativeRoster.Get(id);
    public IReadOnlyList<Ability> Abilities => abilities;
    public Ability Native => FindSlot(Ability.Slot.Primary);
    public Ability Ultimate => ultimate;
    public float UltCharge01 => Mathf.Clamp01(ultCharge);
    public bool UltReady => ultCharge >= 1f;

    private void Awake()
    {
        if (health == null) health = GetComponent<Health>();
        if (mover == null) mover = GetComponent<movement>();
        if (look == null) look = GetComponent<PlayerLook>();
        if (weapons == null) weapons = GetComponentInChildren<WeaponController>();
        if (loadout == null) loadout = GetComponent<Loadout>();
        if (status == null) status = GetComponent<StatusEffects>();
        if (status == null) status = gameObject.AddComponent<StatusEffects>();
        if (cosmetics == null) cosmetics = GetComponentInChildren<OperativeCosmetics>(true);

        context = BuildContext();
    }

    private void OnEnable()
    {
        if (weapons != null)
        {
            weapons.HitConfirmed += HandleDamageDealt;
            weapons.AllyHealed += ReportHealingDone;
        }
    }

    private void OnDisable()
    {
        if (weapons != null)
        {
            weapons.HitConfirmed -= HandleDamageDealt;
            weapons.AllyHealed -= ReportHealingDone;
        }
    }

    private AbilityContext BuildContext()
    {
        return new AbilityContext
        {
            Owner = gameObject,
            Transform = transform,
            Health = health,
            Movement = mover,
            Body = GetComponent<Rigidbody>(),
            Look = look,
            Weapons = weapons,
            Status = status,
            AbilityOwner = this,
            RequestLaunch = (velocity, seconds) => mover?.AddLaunch(velocity, seconds),
            RequestTeleport = (position, yaw) => mover?.Teleport(position, yaw)
        };
    }

    public Ability GetAbility(Ability.Slot wanted) => FindSlot(wanted);

    public void SetOperative(OperativeId newId)
    {
        id = newId;
        OperativeDefinition definition = OperativeRoster.Get(id);

        if (loadout != null)
        {
            loadout.SetRole(definition.baseRole);
        }

        health.ConfigurePools(definition.maxHealth, definition.maxArmor);
        if (mover != null)
        {
            LoadoutStats block = LoadoutStats.For(definition.baseRole);
            mover.SetOperativeStats(definition.moveSpeed, block.jumpForce);
            mover.SetFlightMode(definition.flight);
            mover.SetBodyScale(definition.bodyScale);
        }

        if (look != null)
        {
            look.SetEyeHeight(OperativeBody.BaseEyeHeight * definition.bodyScale);
        }

        if (loadout != null && loadout.BodyRoot != null)
        {
            loadout.BodyRoot.localScale = Vector3.one * definition.bodyScale;
        }

        if (cosmetics != null)
        {
            cosmetics.SetOperative(id);
        }

        if (weapons != null)
        {
            List<WeaponDefinition> kit = OperativeWeaponLibrary.WeaponsFor(id);
            if (kit != null && kit.Count > 0)
            {
                weapons.SetWeapons(kit);
            }
        }

        foreach (Ability existing in abilities)
        {
            if (existing != null)
            {
                Destroy(existing);
            }
        }

        abilities.Clear();
        ultimate = null;
        bool rmbAbility = false;

        foreach (OperativeDefinition.AbilitySlot def in definition.abilities)
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

            if (def.key == "RMB")
            {
                rmbAbility = true;
            }
        }

        if (weapons != null)
        {
            weapons.SetRmbAbilityMode(rmbAbility);
        }

        ultCharge = 0f;
    }

    public void SetOperativeByHeroIndex(int heroIndex)
    {
        OperativeId wanted = OperativeRoster.FromHeroIndex(heroIndex).id;
        if (abilities.Count > 0 && id == wanted)
        {
            return;
        }

        SetOperative(wanted);
    }

    public void SetInputEnabled(bool value)
    {
        inputEnabled = value;
    }

    public void ConfigureOwnership(bool isLocalPlayer)
    {
        cosmetics?.ConfigureOwnership(isLocalPlayer);
    }

    public void ResetForRespawn()
    {
        foreach (Ability ability in abilities)
        {
            ability?.ForceReset();
        }
    }

    private Ability FindSlot(Ability.Slot wanted)
    {
        foreach (Ability ability in abilities)
        {
            if (ability != null && ability.slot == wanted)
            {
                return ability;
            }
        }

        return null;
    }

    private void Update()
    {
        if (CanChargeUltimate())
        {
            TrickleCharge();
        }

        if (!inputEnabled || !CombatPhase())
        {
            return;
        }

        if (netDriven)
        {
            UpdateNetAbilities();
            return;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard == null)
        {
            return;
        }

        Mouse mouse = Mouse.current;

        foreach (Ability ability in abilities)
        {
            if (ability == null || ability.IsUltimate)
            {
                continue;
            }

            if (KeyPressed(ability.KeyLabel, keyboard, mouse) && ability.TryActivate())
            {
                AbilityCast?.Invoke(ability.slot);
            }
        }

        if (keyboard.qKey.wasPressedThisFrame && UltReady && ultimate != null && ultimate.TryActivate())
        {
            ultCharge = 0f;
        }
    }

    private void UpdateNetAbilities()
    {
        if (netAbilityEdges == 0)
        {
            return;
        }

        byte edges = netAbilityEdges;
        netAbilityEdges = 0;

        foreach (Ability ability in abilities)
        {
            if (ability == null || ability.IsUltimate)
            {
                continue;
            }

            if ((edges & SlotBit(ability.slot)) == 0)
            {
                continue;
            }

            if (ability.TryActivate())
            {
                AbilityCast?.Invoke(ability.slot);
            }
        }

        if ((edges & InputAbility.Ultimate) != 0 && UltReady && ultimate != null && ultimate.TryActivate())
        {
            ultCharge = 0f;
        }
    }

    private static byte SlotBit(Ability.Slot slot)
    {
        switch (slot)
        {
            case Ability.Slot.Primary: return InputAbility.Primary;
            case Ability.Slot.Secondary: return InputAbility.Secondary;
            case Ability.Slot.Special: return InputAbility.Special;
            case Ability.Slot.Ultimate: return InputAbility.Ultimate;
            default: return 0;
        }
    }

    public void SetNetDriven(bool value)
    {
        netDriven = value;
        netAbilityEdges = 0;
    }

    public void ApplyNetAbilityInput(byte pressedEdges)
    {
        netAbilityEdges |= pressedEdges;
    }

    private static bool KeyPressed(string label, Keyboard keyboard, Mouse mouse)
    {
        switch (label)
        {
            case "E": return keyboard.eKey.wasPressedThisFrame;
            case "SHIFT": return keyboard.leftShiftKey.wasPressedThisFrame || keyboard.rightShiftKey.wasPressedThisFrame;
            case "F": return keyboard.fKey.wasPressedThisFrame;
            case "SPACE": return keyboard.spaceKey.wasPressedThisFrame;
            case "RMB": return mouse != null && mouse.rightButton.wasPressedThisFrame;
            default: return false;
        }
    }

    private bool CanChargeUltimate()
    {
        MatchManager match = MatchManager.Instance;
        bool liveRound = match != null
            && (match.CurrentPhase == MatchManager.Phase.Live || match.CurrentPhase == MatchManager.Phase.Overtime);

        return liveRound && health != null && health.IsAlive;
    }

    private void TrickleCharge()
    {
        if (ultCharge >= 1f)
        {
            return;
        }

        float perSecond = 1f / Mathf.Max(1f, Definition.ultChargeSeconds);
        AddCharge(perSecond * Time.deltaTime);
    }

    private void HandleDamageDealt(float damage, bool headshot, bool kill)
    {
        if (CanChargeUltimate())
        {
            AddCharge(damage * chargePerDamageDealt);
        }

        foreach (Ability ability in abilities)
        {
            if (ability != null && ability.CooldownReductionPerHit > 0f)
            {
                ability.ReduceCooldown(ability.CooldownReductionPerHit);
            }
        }
    }

    public void ReportHealingDone(float amount)
    {
        if (CanChargeUltimate())
        {
            AddCharge(amount * chargePerHealDone);
        }
    }

    private void AddCharge(float amount)
    {
        if (ultCharge >= 1f || amount <= 0f)
        {
            return;
        }

        ultCharge = Mathf.Min(1f, ultCharge + amount);
        if (ultCharge >= 1f)
        {
            UltimateReady?.Invoke();
        }
    }

    private static bool CombatPhase()
    {
        MatchManager match = MatchManager.Instance;
        return match == null
            || match.CurrentPhase == MatchManager.Phase.Live
            || match.CurrentPhase == MatchManager.Phase.Overtime;
    }
}
