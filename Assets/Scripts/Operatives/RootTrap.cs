using System.Collections.Generic;
using UnityEngine;

public class RootTrap : MonoBehaviour
{
    private const float Radius = 3f;
    private const float DamagePerTick = 10f;
    private const float TickInterval = 0.4f;

    private GameObject owner;
    private Team ownerTeam;
    private float expireAt;
    private float nextDamageAt;
    private readonly List<Health> lines = new List<Health>();

    public static RootTrap Spawn(AbilityContext ctx, Vector3 position, float life, Color color)
    {
        GameObject go = new GameObject("RootTrap");
        go.transform.position = position;

        RootTrap trap = go.AddComponent<RootTrap>();
        trap.owner = ctx.Owner;
        trap.ownerTeam = ctx.Team;
        trap.expireAt = Time.time + life;
        trap.nextDamageAt = Time.time + TickInterval;

        Health ownerHealth = ctx.Health;

        for (int i = 0; i < 3; i++)
        {
            Vector3 dir = Quaternion.Euler(0f, i * 120f, 0f) * Vector3.forward;

            GameObject bar = GameObject.CreatePrimitive(PrimitiveType.Cube);
            bar.name = "RootLine";
            bar.transform.SetParent(go.transform, true);
            bar.transform.position = position + dir * (Radius * 0.5f) + Vector3.up * 0.6f;
            bar.transform.rotation = Quaternion.LookRotation(Vector3.Cross(dir, Vector3.up));
            bar.transform.localScale = new Vector3(0.12f, 1.1f, Radius);
            bar.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(color);

            Health h = bar.AddComponent<Health>();
            h.SetCombatant(false);
            h.Team = ctx.Team;
            h.SetDisplayName("Root Line");
            h.ConfigurePools(50f, 0f);
            h.Died += _ => bar.SetActive(false);
            h.DamageResolved += result =>
                MatchStats.Instance?.RecordShieldDamage(ownerHealth, result.healthDamage + result.armorDamage);
            trap.lines.Add(h);
        }

        AbilityVisuals.GroundDisc(go.transform, Radius, color);
        return trap;
    }

    private void Update()
    {
        lines.RemoveAll(l => l == null || !l.IsAlive);

        if (lines.Count == 0 || Time.time >= expireAt)
        {
            Destroy(gameObject);
            return;
        }

        if (Time.time < nextDamageAt)
        {
            return;
        }

        nextDamageAt = Time.time + TickInterval;

        float radiusSqr = Radius * Radius;
        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || !health.Team.IsHostileTo(ownerTeam))
            {
                continue;
            }

            if ((health.transform.position - transform.position).sqrMagnitude > radiusSqr)
            {
                continue;
            }

            health.ApplyDamage(new DamageInfo(DamagePerTick, health.transform.position, Vector3.up, owner, "Root"));
        }
    }
}
