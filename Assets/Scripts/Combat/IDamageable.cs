using UnityEngine;

public interface IDamageable
{
    bool IsAlive { get; }
    Team Team { get; }
    Transform Transform { get; }

    float ApplyDamage(DamageInfo info);
}
