using UnityEngine;

public struct DamageResult
{
    public DamageInfo info;
    public Health victim;

    public float healthDamage;

    public float armorDamage;

    public float blocked;

    public bool lethal;
}
