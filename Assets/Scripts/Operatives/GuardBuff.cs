using UnityEngine;

public class GuardBuff : MonoBehaviour
{
    private Health target;
    private Health source;
    private float armorPerTick;
    private float tickInterval;
    private float expireAt;
    private float nextTickAt;

    public static void Apply(Health target, Health source, float armorPerTick, float tickInterval, float seconds)
    {
        if (target == null || !target.IsAlive)
        {
            return;
        }

        GuardBuff buff = target.GetComponent<GuardBuff>();
        if (buff == null)
        {
            buff = target.gameObject.AddComponent<GuardBuff>();
            buff.target = target;
            buff.nextTickAt = Time.time;
        }

        buff.source = source;
        buff.armorPerTick = armorPerTick;
        buff.tickInterval = tickInterval;
        buff.expireAt = Time.time + seconds;
    }

    private void Update()
    {
        if (target == null || !target.IsAlive || Time.time >= expireAt)
        {
            Destroy(this);
            return;
        }

        StatusEffects.For(target)?.Apply("relay.guard", 0.4f, move: 1.1f);

        if (Time.time >= nextTickAt)
        {
            nextTickAt = Time.time + tickInterval;
            float before = target.CurrentArmor;
            target.AddArmor(armorPerTick);
            MatchStats.Instance?.RecordHeal(source, target, target.CurrentArmor - before);
        }
    }
}
