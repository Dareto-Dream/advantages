using UnityEngine;

[RequireComponent(typeof(Health))]
public class MotionDetector : MonoBehaviour
{
    private const float Range = 6.5f;

    private Health health;
    private Health owner;
    private Team ownerTeam;
    private float expireAt;
    private float nextPingAt;

    public static MotionDetector Spawn(AbilityContext ctx, Vector3 position, Color color)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = "MotionDetector";
        go.transform.position = position + Vector3.up * 0.2f;
        go.transform.localScale = new Vector3(0.35f, 0.4f, 0.35f);
        go.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(color);

        Health h = go.AddComponent<Health>();
        h.SetCombatant(false);
        h.Team = ctx.Team;
        h.SetDisplayName("Detector");
        h.ConfigurePools(20f, 0f);
        h.Died += _ => Object.Destroy(go);

        MotionDetector detector = go.AddComponent<MotionDetector>();
        detector.health = h;
        detector.owner = ctx.Health;
        detector.ownerTeam = ctx.Team;
        detector.expireAt = Time.time + 20f;
        return detector;
    }

    private void Update()
    {
        if (health == null || !health.IsAlive || Time.time >= expireAt)
        {
            Destroy(gameObject);
            return;
        }

        float rangeSqr = Range * Range;
        bool tripped = false;

        foreach (Health candidate in CombatantRegistry.All)
        {
            if (candidate == null || !candidate.IsAlive || !candidate.Team.IsHostileTo(ownerTeam))
            {
                continue;
            }

            if ((candidate.transform.position - transform.position).sqrMagnitude > rangeSqr)
            {
                continue;
            }

            tripped = true;
            StatusEffects.For(candidate)?.ApplyOutline(1.5f, StatusEffects.OutlineScope.Team, owner);
        }

        if (tripped && Time.time >= nextPingAt)
        {
            nextPingAt = Time.time + 1.5f;
            AbilityFx.Flash(transform.position + Vector3.up * 0.5f, 2f, new Color(0.56f, 0.46f, 0.95f));
        }
    }
}
