using UnityEngine;

public class SeekingOrb : MonoBehaviour
{
    private GameObject ownerGo;
    private Team ownerTeam;
    private float seekRange;
    private float healAmount;
    private float enemyDamage;
    private float dieAt;
    private float speed = 11f;
    private Vector3 velocity;
    private Health chase;

    public static void Spawn(AbilityContext ctx, Vector3 origin, Vector3 initialDir, float seekRange, float healAmount, float enemyDamage, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        go.name = "SeekingOrb";
        Object.Destroy(go.GetComponent<Collider>());
        go.transform.position = origin;
        go.transform.localScale = Vector3.one * 0.32f;
        go.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(color);

        SeekingOrb orb = go.AddComponent<SeekingOrb>();
        orb.ownerGo = ctx.Owner;
        orb.ownerTeam = ctx.Team;
        orb.seekRange = seekRange;
        orb.healAmount = healAmount;
        orb.enemyDamage = enemyDamage;
        orb.dieAt = Time.time + 4f;
        orb.velocity = initialDir.normalized * orb.speed;
    }

    private void Update()
    {
        if (Time.time >= dieAt)
        {
            Destroy(gameObject);
            return;
        }

        if (chase == null || !chase.IsAlive)
        {
            chase = FindInjuredAlly();
        }

        if (chase != null)
        {
            Vector3 want = (chase.transform.position + Vector3.up - transform.position).normalized * speed;
            velocity = Vector3.MoveTowards(velocity, want, 40f * Time.deltaTime);
        }

        transform.position += velocity * Time.deltaTime;

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive)
            {
                continue;
            }

            if ((health.transform.position + Vector3.up - transform.position).sqrMagnitude > 1.2f)
            {
                continue;
            }

            if (health.Team.IsHostileTo(ownerTeam))
            {
                health.ApplyDamage(new DamageInfo(enemyDamage, transform.position, velocity.normalized, ownerGo, "Orb Storm"));
                Destroy(gameObject);
                return;
            }

            if (health.gameObject != ownerGo && health.CurrentHealth < health.MaxHealth)
            {
                float before = health.CurrentHealth;
                health.Heal(healAmount);
                MatchStats.Instance?.RecordHeal(ownerGo != null ? ownerGo.GetComponentInParent<Health>() : null, health, health.CurrentHealth - before);
                Destroy(gameObject);
                return;
            }
        }
    }

    private Health FindInjuredAlly()
    {
        Health best = null;
        float bestDist = seekRange;

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || health.gameObject == ownerGo
                || health.Team.IsHostileTo(ownerTeam) || health.CurrentHealth >= health.MaxHealth)
            {
                continue;
            }

            float dist = Vector3.Distance(transform.position, health.transform.position);
            if (dist < bestDist)
            {
                bestDist = dist;
                best = health;
            }
        }

        return best;
    }
}
