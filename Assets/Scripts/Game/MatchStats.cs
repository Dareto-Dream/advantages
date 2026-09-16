using System.Collections.Generic;
using UnityEngine;

public sealed class CombatantStats
{
    public Health health;

    public int kills;
    public int deaths;
    public int assists;
    public int knockouts;
    public int finalHits;

    public float damageDealt;
    public float damageBlocked;
    public float healingDone;

    public string Name => health != null ? health.DisplayName : "—";
    public Team Team => health != null ? health.Team : Team.None;
    public bool IsAlive => health != null && health.IsAlive;

    public string RoleLabel
    {
        get
        {
            if (health == null)
            {
                return "—";
            }

            OperativeController op = health.GetComponent<OperativeController>();
            if (op != null && op.Abilities.Count > 0)
            {
                return op.Definition.name;
            }

            BotOperative botOp = health.GetComponent<BotOperative>();
            if (botOp != null)
            {
                return OperativeRoster.Get(botOp.Id).name;
            }

            BotBrain bot = health.GetComponent<BotBrain>();
            return bot != null ? bot.Role.ToString() : "—";
        }
    }
}

public class MatchStats : MonoBehaviour
{
    public static MatchStats Instance { get; private set; }

    private const float AssistWindow = 10f;
    private const float HealAssistWindow = 6f;

    private struct Contribution
    {
        public Health source;
        public float time;
    }

    private readonly Dictionary<Health, CombatantStats> table = new Dictionary<Health, CombatantStats>();
    private readonly List<CombatantStats> ordered = new List<CombatantStats>();
    private readonly Dictionary<Health, List<Contribution>> recentDamage = new Dictionary<Health, List<Contribution>>();
    private readonly Dictionary<Health, List<Contribution>> recentHeals = new Dictionary<Health, List<Contribution>>();

    public IReadOnlyList<CombatantStats> All => ordered;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (FindAnyObjectByType<MatchManager>() != null)
        {
            EnsureExists();
        }
    }

    public static void EnsureExists()
    {
        if (Instance != null)
        {
            return;
        }

        GameObject go = new GameObject("~MatchStats");
        go.AddComponent<MatchStats>();
        go.AddComponent<ScoreboardOverlay>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
    }

    private void OnDestroy()
    {
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void Update()
    {

        IReadOnlyList<Health> all = CombatantRegistry.All;
        for (int i = 0; i < all.Count; i++)
        {
            Health h = all[i];
            if (h == null || h.IsDecoy || table.ContainsKey(h))
            {
                continue;
            }

            CombatantStats stats = new CombatantStats { health = h };
            table[h] = stats;
            ordered.Add(stats);

            Health captured = h;
            h.DamageResolved += result => HandleDamageResolved(captured, result);
            h.Died += info => HandleDied(captured, info);
        }
    }

    public CombatantStats For(Health health)
    {
        return health != null && table.TryGetValue(health, out CombatantStats stats) ? stats : null;
    }

    public void RecordHeal(Health healer, Health target, float amount)
    {
        if (healer == null || target == null || amount <= 0f)
        {
            return;
        }

        CombatantStats stats = For(healer);
        if (stats != null)
        {
            stats.healingDone += amount;
        }

        if (target != healer)
        {
            AddContribution(recentHeals, target, healer);
        }
    }

    public void ResetAll()
    {
        foreach (CombatantStats stats in ordered)
        {
            stats.kills = stats.deaths = stats.assists = stats.knockouts = stats.finalHits = 0;
            stats.damageDealt = stats.damageBlocked = stats.healingDone = 0f;
        }

        recentDamage.Clear();
        recentHeals.Clear();
    }

    public void RecordShieldDamage(Health owner, float amount)
    {
        if (amount <= 0f)
        {
            return;
        }

        CombatantStats stats = For(owner);
        if (stats != null)
        {
            stats.damageBlocked += amount;
        }
    }

    private void HandleDamageResolved(Health victim, DamageResult result)
    {
        CombatantStats victimStats = For(victim);
        if (victimStats != null)
        {

            victimStats.damageBlocked += result.healthDamage + result.armorDamage;
        }

        Health attacker = result.info.instigator != null
            ? result.info.instigator.GetComponentInParent<Health>()
            : null;

        if (attacker == null || attacker == victim || !victim.Team.IsHostileTo(attacker.Team))
        {
            return;
        }

        CombatantStats attackerStats = For(attacker);
        if (attackerStats != null)
        {
            attackerStats.damageDealt += result.healthDamage + result.armorDamage;
        }

        AddContribution(recentDamage, victim, attacker);
    }

    private void HandleDied(Health victim, DamageInfo info)
    {
        CombatantStats victimStats = For(victim);
        if (victimStats != null)
        {
            victimStats.deaths++;
        }

        Health killer = info.instigator != null ? info.instigator.GetComponentInParent<Health>() : null;
        bool validKiller = killer != null && killer != victim && victim.Team.IsHostileTo(killer.Team);

        if (validKiller)
        {
            CombatantStats killerStats = For(killer);
            if (killerStats != null)
            {
                killerStats.kills++;
                killerStats.finalHits++;
                killerStats.knockouts++;
            }
        }

        HashSet<Health> assisted = new HashSet<Health>();

        if (recentDamage.TryGetValue(victim, out List<Contribution> damagers))
        {
            foreach (Contribution c in damagers)
            {
                if (Time.time - c.time > AssistWindow || c.source == null || c.source == killer || c.source == victim)
                {
                    continue;
                }

                if (victim.Team.IsHostileTo(c.source.Team))
                {
                    assisted.Add(c.source);
                }
            }
        }

        if (validKiller && recentHeals.TryGetValue(killer, out List<Contribution> healers))
        {
            foreach (Contribution c in healers)
            {
                if (Time.time - c.time > HealAssistWindow || c.source == null || c.source == killer)
                {
                    continue;
                }

                if (victim.Team.IsHostileTo(c.source.Team))
                {
                    assisted.Add(c.source);
                }
            }
        }

        foreach (Health helper in assisted)
        {
            CombatantStats stats = For(helper);
            if (stats != null)
            {
                stats.assists++;
                stats.knockouts++;
            }
        }

        recentDamage.Remove(victim);
    }

    private void AddContribution(Dictionary<Health, List<Contribution>> map, Health key, Health source)
    {
        if (!map.TryGetValue(key, out List<Contribution> list))
        {
            list = new List<Contribution>();
            map[key] = list;
        }

        for (int i = 0; i < list.Count; i++)
        {
            if (list[i].source == source)
            {
                list[i] = new Contribution { source = source, time = Time.time };
                return;
            }
        }

        list.Add(new Contribution { source = source, time = Time.time });
    }
}
