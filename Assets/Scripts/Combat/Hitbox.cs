using UnityEngine;

public class Hitbox : MonoBehaviour, IDamageable
{
    public enum Zone
    {
        Body,
        Head,
        Limb
    }

    [SerializeField] private Zone zone = Zone.Body;
    [SerializeField] private Health owner;

    public Zone HitZone => zone;
    public Health Owner => owner;

    public bool IsAlive => owner != null && owner.IsAlive;
    public Team Team => owner != null ? owner.Team : Team.None;
    public Transform Transform => owner != null ? owner.transform : transform;

    private void Awake()
    {
        if (owner == null)
        {
            owner = GetComponentInParent<Health>();
        }
    }

    public void Configure(Zone hitZone, Health health)
    {
        zone = hitZone;
        owner = health;
    }

    public float ApplyDamage(DamageInfo info)
    {
        if (owner == null)
        {
            return 0f;
        }

        if (zone == Zone.Head)
        {
            info.isHeadshot = true;
        }

        return owner.ApplyDamage(info);
    }
}
