using UnityEngine;

public struct DamageInfo
{
    public float amount;
    public Vector3 point;
    public Vector3 direction;
    public float impactForce;
    public bool isHeadshot;
    public GameObject instigator;
    public string sourceName;

    public DamageInfo(float amount, Vector3 point, Vector3 direction, GameObject instigator, string sourceName)
    {
        this.amount = amount;
        this.point = point;
        this.direction = direction;
        this.impactForce = 0f;
        this.isHeadshot = false;
        this.instigator = instigator;
        this.sourceName = sourceName;
    }
}
