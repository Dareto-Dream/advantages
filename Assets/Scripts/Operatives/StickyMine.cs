using UnityEngine;

public class StickyMine : MonoBehaviour
{
    private GameObject owner;
    private Team ownerTeam;
    private float damage = 70f;
    private float blastRadius = 4.5f;
    private float triggerRadius = 3f;
    private float armAt;
    private float expireAt;
    private bool stuck;
    private bool detonated;

    private Rigidbody body;

    public static StickyMine Spawn(AbilityContext ctx, Vector3 position, Vector3 velocity, float damage, float blastRadius)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "StickyMine";
        go.transform.position = position;
        go.transform.localScale = Vector3.one * 0.35f;
        go.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(new Color(0.95f, 0.45f, 0.15f));

        Rigidbody rb = go.AddComponent<Rigidbody>();
        rb.mass = 2f;
        rb.linearVelocity = velocity;
        rb.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        StickyMine mine = go.AddComponent<StickyMine>();
        mine.owner = ctx.Owner;
        mine.ownerTeam = ctx.Team;
        mine.damage = damage;
        mine.blastRadius = blastRadius;
        mine.body = rb;
        mine.armAt = Time.time + 0.6f;
        mine.expireAt = Time.time + 14f;
        return mine;
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (stuck)
        {
            return;
        }

        stuck = true;
        body.linearVelocity = Vector3.zero;
        body.angularVelocity = Vector3.zero;
        body.isKinematic = true;

        if (collision.transform.GetComponentInParent<Health>() is Health hitHealth && hitHealth.Team.IsHostileTo(ownerTeam))
        {
            transform.SetParent(collision.transform, true);
        }
    }

    private void Update()
    {
        if (detonated)
        {
            return;
        }

        if (Time.time >= expireAt)
        {
            Detonate();
            return;
        }

        if (!stuck || Time.time < armAt)
        {
            return;
        }

        float triggerSqr = triggerRadius * triggerRadius;
        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null || !health.IsAlive || !health.Team.IsHostileTo(ownerTeam))
            {
                continue;
            }

            if ((health.transform.position - transform.position).sqrMagnitude <= triggerSqr)
            {
                Detonate();
                return;
            }
        }
    }

    private void Detonate()
    {
        if (detonated)
        {
            return;
        }

        detonated = true;
        Ability.ExplodeDamage(transform.position, blastRadius, damage, ownerTeam, owner, "Sticky Mine");
        AbilityFx.Flash(transform.position, blastRadius, new Color(1f, 0.5f, 0.15f));
        Destroy(gameObject);
    }
}
