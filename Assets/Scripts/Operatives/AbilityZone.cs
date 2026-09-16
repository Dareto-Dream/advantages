using UnityEngine;

public class AbilityZone : MonoBehaviour
{
    private const string AllyTag = "zone.ally";
    private const string EnemyTag = "zone.enemy";

    public GameObject owner;
    public Team ownerTeam;
    public string sourceName = "Field";
    public float radius = 6f;
    public float life = 6f;
    public Transform follow;

    [Header("Ally effects (per frame while inside)")]
    public float allyHealPerSecond;
    public float allyOverhealTo;
    public float allyIncomingMult = 1f;
    public float allyMoveMult = 1f;
    public float allyOutgoingMult = 1f;

    [Header("Enemy effects")]
    public float enemyMoveMult = 1f;
    public float enemyIncomingMult = 1f;
    public bool enemyLockAbilities;
    public bool outlineEnemies;
    public float enemyDamagePerSecond;

    private float overhealGranted;
    private float nextDamageTime;
    private Health ownerHealth;
    private bool ownerHealthResolved;

    private Health OwnerHealth
    {
        get
        {
            if (!ownerHealthResolved)
            {
                ownerHealthResolved = true;
                ownerHealth = owner != null ? owner.GetComponentInParent<Health>() : null;
            }

            return ownerHealth;
        }
    }

    public static AbilityZone Spawn(AbilityContext ctx, Vector3 position, float radius, float life, Color color, string sourceName)
    {
        GameObject go = new GameObject($"AbilityZone_{sourceName}");
        go.transform.position = position;

        AbilityZone zone = go.AddComponent<AbilityZone>();
        zone.owner = ctx.Owner;
        zone.ownerTeam = ctx.Team;
        zone.sourceName = sourceName;
        zone.radius = radius;
        zone.life = life;

        AbilityVisuals.GroundDisc(go.transform, radius, color);
        return zone;
    }

    private void Update()
    {
        life -= Time.deltaTime;
        if (life <= 0f)
        {
            Destroy(gameObject);
            return;
        }

        if (follow != null)
        {
            transform.position = follow.position;
        }

        bool doDamage = enemyDamagePerSecond > 0f && Time.time >= nextDamageTime;
        float damageThisTick = 0f;
        if (doDamage)
        {
            damageThisTick = enemyDamagePerSecond * 0.4f;
            nextDamageTime = Time.time + 0.4f;
        }

        float radiusSqr = radius * radius;

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive)
            {
                continue;
            }

            if ((health.transform.position - transform.position).sqrMagnitude > radiusSqr)
            {
                continue;
            }

            bool hostile = health.Team.IsHostileTo(ownerTeam);

            if (!hostile)
            {
                if (allyHealPerSecond > 0f)
                {
                    float before = health.CurrentHealth;
                    health.Heal(allyHealPerSecond * Time.deltaTime);
                    MatchStats.Instance?.RecordHeal(OwnerHealth, health, health.CurrentHealth - before);
                }

                if (allyOverhealTo > 0f && overhealGranted < allyOverhealTo)
                {
                    float step = Mathf.Min(allyOverhealTo - overhealGranted, allyOverhealTo * Time.deltaTime);
                    health.AddArmor(step);
                    overhealGranted += step;
                    MatchStats.Instance?.RecordHeal(OwnerHealth, health, step);
                }

                if (allyIncomingMult != 1f || allyMoveMult != 1f || allyOutgoingMult != 1f)
                {
                    StatusEffects.For(health)?.Apply(AllyTag, 0.25f,
                        move: allyMoveMult, incoming: allyIncomingMult, outgoing: allyOutgoingMult);
                }
            }
            else
            {
                if (enemyMoveMult != 1f || enemyIncomingMult != 1f || enemyLockAbilities)
                {
                    StatusEffects.For(health)?.Apply(EnemyTag, 0.25f,
                        move: enemyMoveMult, incoming: enemyIncomingMult,
                        lockAbilities: enemyLockAbilities);
                }

                if (outlineEnemies)
                {
                    StatusEffects.For(health)?.ApplyOutline(0.3f, StatusEffects.OutlineScope.Team, OwnerHealth);
                }

                if (damageThisTick > 0f)
                {
                    health.ApplyDamage(new DamageInfo(
                        damageThisTick,
                        health.transform.position,
                        Vector3.down,
                        owner,
                        sourceName));
                }
            }
        }
    }
}
