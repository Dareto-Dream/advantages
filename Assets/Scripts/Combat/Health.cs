using System;
using UnityEngine;

public class Health : MonoBehaviour, IDamageable
{
    [Header("Pools")]
    [SerializeField] private float maxHealth = 100f;
    [SerializeField] private float maxArmor = 0f;

    [Header("Identity")]
    [SerializeField] private Team team = Team.None;
    [SerializeField] private string displayName = "Operator";

    [Header("Rules")]
    [SerializeField] private bool allowFriendlyFire = false;
    [SerializeField] private bool invulnerable = false;
    [Tooltip("Off for destructible props (ability barriers, spotters) so they don't count toward " +
        "the alive tally or get hunted as targets - they still take damage and die.")]
    [SerializeField] private bool registerAsCombatant = true;

    private float currentHealth;
    private float currentArmor;
    private bool isAlive = true;
    private float lethalGuardUntil = -1f;
    private float lastDamageTime = -999f;

    public bool IsDecoy { get; set; }

    public float TimeSinceDamage => Time.time - lastDamageTime;

    private StatusEffects status;
    private StatusEffects Status => status != null ? status : (status = GetComponent<StatusEffects>());

    public event Action<float, DamageInfo> Damaged;

    public event Action<DamageResult> DamageResolved;

    public event Action<DamageInfo> Died;

    public event Action Revived;

    public event Action<float, float> PoolsChanged;

    public event Action LethalHitBlocked;

    public float MaxHealth => maxHealth;
    public float CurrentHealth => currentHealth;
    public float CurrentArmor => currentArmor;
    public float MaxArmor => maxArmor;
    public float HealthFraction => maxHealth > 0f ? currentHealth / maxHealth : 0f;
    public float ArmorFraction => maxArmor > 0f ? currentArmor / maxArmor : 0f;
    public bool IsAlive => isAlive;
    public string DisplayName => displayName;
    public Transform Transform => transform;

    public Team Team
    {
        get => team;
        set => team = value;
    }

    private void Awake()
    {
        currentHealth = maxHealth;
        currentArmor = maxArmor;
    }

    private void OnEnable()
    {
        if (registerAsCombatant)
        {
            CombatantRegistry.Register(this);
        }
    }

    private void OnDisable()
    {
        CombatantRegistry.Unregister(this);
    }

    public void SetCombatant(bool value)
    {
        registerAsCombatant = value;
        if (value)
        {
            CombatantRegistry.Register(this);
        }
        else
        {
            CombatantRegistry.Unregister(this);
        }
    }

    private void Start()
    {
        RaisePoolsChanged();
    }

    public void SetDisplayName(string value)
    {
        displayName = value;
    }

    public void ConfigurePools(float newMaxHealth, float newMaxArmor)
    {
        maxHealth = Mathf.Max(1f, newMaxHealth);
        maxArmor = Mathf.Max(0f, newMaxArmor);
        ResetHealth();
    }

    public void ResetHealth()
    {
        currentHealth = maxHealth;
        currentArmor = maxArmor;
        isAlive = true;
        RaisePoolsChanged();
    }

    public void SetNetworkPools(float health, float armor, bool alive)
    {
        bool wasAlive = isAlive;

        currentHealth = Mathf.Clamp(health, 0f, maxHealth);
        currentArmor = Mathf.Clamp(armor, 0f, maxArmor);
        isAlive = alive;

        RaisePoolsChanged();

        if (wasAlive && !alive)
        {
            Died?.Invoke(new DamageInfo { amount = 0f, sourceName = "Network" });
        }
        else if (!wasAlive && alive)
        {
            Revived?.Invoke();
        }
    }

    public void RestorePools(float health, float armor)
    {
        if (!isAlive)
        {
            return;
        }

        currentHealth = Mathf.Clamp(health, 1f, maxHealth);
        currentArmor = Mathf.Clamp(armor, 0f, maxArmor);
        RaisePoolsChanged();
    }

    public void Heal(float amount)
    {
        if (!isAlive || amount <= 0f)
        {
            return;
        }

        currentHealth = Mathf.Min(maxHealth, currentHealth + amount);
        RaisePoolsChanged();
    }

    public void GrantLethalGuard(float seconds)
    {
        lethalGuardUntil = Time.time + seconds;
    }

    public void AddArmor(float amount)
    {
        if (!isAlive || amount <= 0f)
        {
            return;
        }

        maxArmor = Mathf.Max(maxArmor, currentArmor + amount);
        currentArmor = Mathf.Min(maxArmor, currentArmor + amount);
        RaisePoolsChanged();
    }

    public bool CanBeDamagedBy(Team attackerTeam)
    {
        if (!isAlive || invulnerable)
        {
            return false;
        }

        return allowFriendlyFire || team.IsHostileTo(attackerTeam);
    }

    public float ApplyDamage(DamageInfo info)
    {

        if (NetContext.IsClient)
        {
            return 0f;
        }

        if (!isAlive || invulnerable || info.amount <= 0f)
        {
            return 0f;
        }

        Team attackerTeam = ResolveAttackerTeam(info.instigator);
        if (!allowFriendlyFire && info.instigator != gameObject && !team.IsHostileTo(attackerTeam))
        {
            return 0f;
        }

        float afterStatus = info.amount * (Status != null ? Status.IncomingDamageMultiplier : 1f);
        float statusBlocked = Mathf.Max(0f, info.amount - afterStatus);
        float remaining = afterStatus;
        float armorDamage = 0f;

        if (currentArmor > 0f)
        {

            float absorbed = Mathf.Min(currentArmor, remaining * 0.5f);
            currentArmor -= absorbed;
            remaining -= absorbed;
            armorDamage = absorbed;
        }

        float guardBlocked = 0f;
        if (Time.time < lethalGuardUntil && remaining >= currentHealth && currentHealth > 1f)
        {
            guardBlocked = remaining - (currentHealth - 1f);
            remaining = currentHealth - 1f;
            lethalGuardUntil = -1f;
            LethalHitBlocked?.Invoke();
        }

        float dealt = Mathf.Min(currentHealth, remaining);
        currentHealth -= dealt;
        lastDamageTime = Time.time;

        RaisePoolsChanged();
        Damaged?.Invoke(info.amount, info);

        bool lethal = currentHealth <= 0f;
        DamageResolved?.Invoke(new DamageResult
        {
            info = info,
            victim = this,
            healthDamage = dealt,
            armorDamage = armorDamage,
            blocked = statusBlocked + armorDamage + guardBlocked,
            lethal = lethal
        });

        if (!lethal && (dealt > 0f || armorDamage > 0f) && info.instigator != null && info.instigator != gameObject)
        {
            Health attacker = info.instigator.GetComponentInParent<Health>();
            if (attacker != null && attacker != this)
            {
                StatusEffects.For(this)?.FlashHealthBar(attacker);
            }
        }

        if (lethal)
        {
            Kill(info);
        }

        return info.amount;
    }

    public void Kill(DamageInfo info)
    {
        if (!isAlive)
        {
            return;
        }

        isAlive = false;
        currentHealth = 0f;
        RaisePoolsChanged();
        Died?.Invoke(info);
    }

    private Team ResolveAttackerTeam(GameObject instigator)
    {
        if (instigator == null)
        {
            return Team.None;
        }

        Health attacker = instigator.GetComponentInParent<Health>();
        return attacker != null ? attacker.Team : Team.None;
    }

    private void RaisePoolsChanged()
    {
        PoolsChanged?.Invoke(HealthFraction, ArmorFraction);
    }
}
