using UnityEngine;

public class PositionHistory : MonoBehaviour
{
    public struct Snapshot
    {
        public float time;
        public Vector3 position;
        public float yaw;
        public Vector3 velocity;
        public float health;
        public float armor;
    }

    private const float Window = 5f;

    private readonly Snapshot[] ring = new Snapshot[300];
    private int head = -1;
    private int count;

    private Rigidbody body;
    private Health health;

    private void Awake()
    {
        body = GetComponent<Rigidbody>();
        health = GetComponent<Health>();
    }

    private void FixedUpdate()
    {
        head = (head + 1) % ring.Length;
        if (count < ring.Length)
        {
            count++;
        }

        ring[head] = new Snapshot
        {
            time = Time.time,
            position = transform.position,
            yaw = transform.eulerAngles.y,
            velocity = body != null ? body.linearVelocity : Vector3.zero,
            health = health != null ? health.CurrentHealth : 0f,
            armor = health != null ? health.CurrentArmor : 0f
        };
    }

    public bool TryGet(float secondsAgo, out Snapshot snapshot)
    {
        snapshot = default;
        if (count == 0)
        {
            return false;
        }

        float wanted = Time.time - Mathf.Clamp(secondsAgo, 0f, Window);
        Snapshot best = ring[head];
        float bestDelta = Mathf.Abs(best.time - wanted);

        for (int i = 1; i < count; i++)
        {
            Snapshot candidate = ring[(head - i + ring.Length) % ring.Length];
            float delta = Mathf.Abs(candidate.time - wanted);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                best = candidate;
            }
        }

        snapshot = best;
        return true;
    }
}
