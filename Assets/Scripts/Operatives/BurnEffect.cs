using UnityEngine;

public class BurnEffect : MonoBehaviour
{
    private const float TickInterval = 0.4f;

    private Health target;
    private GameObject instigator;
    private float dps;
    private float expireAt;
    private float nextTickAt;

    public static void Apply(Health target, GameObject instigator, float dps, float seconds)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        BurnEffect burn = target.GetComponent<BurnEffect>();
        if (burn == null)
        {
            burn = target.gameObject.AddComponent<BurnEffect>();
            burn.target = target;
            burn.nextTickAt = Time.time + TickInterval;
        }

        burn.instigator = instigator;
        burn.dps = Mathf.Max(burn.dps, dps);
        burn.expireAt = Time.time + seconds;
    }

    private void Update()
    {
        if (target == null || !target.IsAlive || Time.time >= expireAt)
        {
            Destroy(this);
            return;
        }

        if (Time.time >= nextTickAt)
        {
            nextTickAt = Time.time + TickInterval;
            target.ApplyDamage(new DamageInfo(dps * TickInterval, target.transform.position, Vector3.up, instigator, "Burn"));
        }
    }
}
