using System.Collections.Generic;
using UnityEngine;

public class WardenOrbit : MonoBehaviour
{
    private class Orb
    {
        public Transform transform;
        public float angle;
        public bool spent;
    }

    private Transform owner;
    private GameObject ownerGo;
    private Team ownerTeam;
    private float expireAt;
    private float allyHeal;
    private float enemyDamage;
    private readonly List<Orb> orbs = new List<Orb>();

    private const float Radius = 1.9f;
    private const float Height = 1.1f;
    private const float Spin = 140f;

    public static WardenOrbit Spawn(AbilityContext ctx, int count, float seconds, float allyHeal, float enemyDamage, Color color)
    {
        GameObject go = new GameObject("WardenOrbit");
        go.transform.SetParent(ctx.Transform, false);

        WardenOrbit orbit = go.AddComponent<WardenOrbit>();
        orbit.owner = ctx.Transform;
        orbit.ownerGo = ctx.Owner;
        orbit.ownerTeam = ctx.Team;
        orbit.expireAt = Time.time + seconds;
        orbit.allyHeal = allyHeal;
        orbit.enemyDamage = enemyDamage;

        for (int i = 0; i < count; i++)
        {
            GameObject ball = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            ball.name = "Orb";
            Object.Destroy(ball.GetComponent<Collider>());
            ball.transform.SetParent(go.transform, false);
            ball.transform.localScale = Vector3.one * 0.3f;
            ball.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(color);
            orbit.orbs.Add(new Orb { transform = ball.transform, angle = i * (360f / count) });
        }

        return orbit;
    }

    private void Update()
    {
        if (owner == null || Time.time >= expireAt)
        {
            Destroy(gameObject);
            return;
        }

        bool anyLeft = false;

        foreach (Orb orb in orbs)
        {
            if (orb.spent)
            {
                continue;
            }

            anyLeft = true;
            orb.angle += Spin * Time.deltaTime;
            float rad = orb.angle * Mathf.Deg2Rad;
            Vector3 world = owner.position + new Vector3(Mathf.Cos(rad) * Radius, Height, Mathf.Sin(rad) * Radius);
            orb.transform.position = world;

            foreach (Health health in CombatantRegistry.All)
            {
                if (health == null || !health.IsAlive || health == owner.GetComponent<Health>())
                {
                    continue;
                }

                if ((health.transform.position + Vector3.up - world).sqrMagnitude > 0.9f)
                {
                    continue;
                }

                if (health.Team.IsHostileTo(ownerTeam))
                {
                    health.ApplyDamage(new DamageInfo(enemyDamage, world, Vector3.up, ownerGo, "Orbit"));
                }
                else
                {
                    float before = health.CurrentHealth;
                    health.Heal(allyHeal);
                    MatchStats.Instance?.RecordHeal(ownerGo != null ? ownerGo.GetComponentInParent<Health>() : null, health, health.CurrentHealth - before);
                }

                orb.spent = true;
                orb.transform.gameObject.SetActive(false);
                break;
            }
        }

        if (!anyLeft)
        {
            Destroy(gameObject);
        }
    }
}
