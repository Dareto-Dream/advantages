using UnityEngine;

[RequireComponent(typeof(LineRenderer))]
public class HealTether : MonoBehaviour
{
    private Transform source;
    private Health target;
    private GameObject healer;
    private float healPerSecond;
    private float expireAt;
    private LineRenderer line;
    private readonly Vector3 lift = new Vector3(0f, 1.1f, 0f);

    public static HealTether Spawn(GameObject healer, Transform source, Health target, float healPerSecond, float seconds, Color color)
    {
        GameObject go = new GameObject("HealTether");
        LineRenderer lr = go.AddComponent<LineRenderer>();
        lr.material = AbilityVisuals.SolidUnlit(color);
        lr.widthMultiplier = 0.06f;
        lr.positionCount = 2;
        lr.numCapVertices = 2;
        lr.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

        HealTether tether = go.AddComponent<HealTether>();
        tether.healer = healer;
        tether.source = source;
        tether.target = target;
        tether.healPerSecond = healPerSecond;
        tether.expireAt = Time.time + seconds;
        tether.line = lr;
        return tether;
    }

    private void Update()
    {
        if (source == null || target == null || !target.IsAlive || Time.time >= expireAt)
        {
            Destroy(gameObject);
            return;
        }

        Vector3 a = source.position + lift;
        Vector3 b = target.transform.position + lift;

        if (Physics.Linecast(a, b, out RaycastHit hit, ~0, QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<Health>() == null)
        {

            Destroy(gameObject);
            return;
        }

        line.SetPosition(0, a);
        line.SetPosition(1, b);

        float before = target.CurrentHealth;
        target.Heal(healPerSecond * Time.deltaTime);
        float healed = target.CurrentHealth - before;
        if (healed > 0f)
        {
            MatchStats.Instance?.RecordHeal(healer != null ? healer.GetComponentInParent<Health>() : null, target, healed);
        }
    }
}
