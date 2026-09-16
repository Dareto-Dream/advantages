using UnityEngine;

[RequireComponent(typeof(Health))]
public class AbilityDeployable : MonoBehaviour
{
    private Health health;
    private float despawnAt;

    public static AbilityDeployable Spawn(
        AbilityContext ctx,
        Vector3 position,
        Quaternion rotation,
        Vector3 size,
        float hp,
        float life,
        Color color,
        string label)
    {
        GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
        go.name = label;
        go.transform.SetPositionAndRotation(position, rotation);
        go.transform.localScale = size;
        go.GetComponent<MeshRenderer>().sharedMaterial = AbilityVisuals.SolidUnlit(color);

        Health h = go.AddComponent<Health>();
        h.SetCombatant(false);
        h.Team = ctx.Team;
        h.SetDisplayName(label);
        h.ConfigurePools(hp, 0f);

        AbilityDeployable deployable = go.AddComponent<AbilityDeployable>();
        deployable.health = h;

        deployable.despawnAt = life > 0f ? Time.time + life : float.MaxValue;
        h.Died += _ => Object.Destroy(go, 0.1f);

        Health ownerHealth = ctx.Health;
        h.DamageResolved += result =>
            MatchStats.Instance?.RecordShieldDamage(ownerHealth, result.healthDamage + result.armorDamage);

        return deployable;
    }

    public AbilityZone AddZone(Color color)
    {
        AbilityZone zone = gameObject.AddComponent<AbilityZone>();
        zone.owner = gameObject;
        zone.ownerTeam = health.Team;
        zone.life = float.MaxValue;
        return zone;
    }

    private void Update()
    {
        if (Time.time >= despawnAt)
        {
            Destroy(gameObject);
        }
    }
}
