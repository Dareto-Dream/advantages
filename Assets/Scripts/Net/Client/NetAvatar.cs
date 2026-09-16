using System.Collections.Generic;
using UnityEngine;

public class NetAvatar : MonoBehaviour
{
    private struct Sample
    {
        public float time;
        public Vector3 position;
        public Vector3 velocity;
        public float yaw;
        public float pitch;
        public bool alive;
    }

    private const int MaxSamples = 32;

    private readonly List<Sample> samples = new List<Sample>(MaxSamples);

    private PlayerController controller;
    private Health health;
    private Transform cameraPivot;

    public ushort EntityId { get; private set; }

    public bool IsBot { get; private set; }

    public EntityState Latest { get; private set; }

    public void Initialise(ushort entityId, bool isBot, PlayerController owner)
    {
        EntityId = entityId;
        IsBot = isBot;
        controller = owner;
        health = owner != null ? owner.Health : GetComponent<Health>();
        cameraPivot = owner != null && owner.Look != null ? owner.Look.CameraPivot : null;
    }

    public void Push(EntityState state)
    {
        Latest = state;

        samples.Add(new Sample
        {

            time = Time.time,
            position = state.position,
            velocity = state.velocity,
            yaw = state.yaw,
            pitch = state.pitch,
            alive = state.Has(EntityFlag.Alive),
        });

        while (samples.Count > MaxSamples)
        {
            samples.RemoveAt(0);
        }

        ApplyPools(state);
    }

    private void ApplyPools(EntityState state)
    {

        if (health != null)
        {
            health.SetNetworkPools(state.health, state.armor, state.Has(EntityFlag.Alive));
        }
    }

    private void Update()
    {
        if (samples.Count == 0)
        {
            return;
        }

        float renderTime = Time.time - NetConfig.InterpolationDelay;

        if (renderTime <= samples[0].time)
        {
            Apply(samples[0].position, samples[0].yaw, samples[0].pitch);
            return;
        }

        Sample newest = samples[samples.Count - 1];

        if (renderTime >= newest.time)
        {

            float ahead = Mathf.Min(renderTime - newest.time, NetConfig.InterpolationDelay);
            Apply(newest.position + newest.velocity * ahead, newest.yaw, newest.pitch);
            return;
        }

        for (int i = samples.Count - 1; i > 0; i--)
        {
            Sample later = samples[i];
            Sample earlier = samples[i - 1];

            if (renderTime < earlier.time)
            {
                continue;
            }

            float span = later.time - earlier.time;
            float t = span > 0.0001f ? (renderTime - earlier.time) / span : 0f;

            Apply(
                Vector3.Lerp(earlier.position, later.position, t),
                Mathf.LerpAngle(earlier.yaw, later.yaw, t),
                Mathf.LerpAngle(earlier.pitch, later.pitch, t));

            if (i - 1 > 0)
            {
                samples.RemoveRange(0, i - 1);
            }

            return;
        }
    }

    private void Apply(Vector3 position, float yaw, float pitch)
    {
        transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));

        if (cameraPivot != null)
        {
            cameraPivot.rotation = Quaternion.Euler(pitch, yaw, 0f);
        }
    }

    public void SetOperative(OperativeId id)
    {
        if (controller == null)
        {
            return;
        }

        OperativeDefinition definition = OperativeRoster.Get(id);
        int heroIndex = OperativeRoster.HeroIndexOf(id);

        if (heroIndex >= 0 && definition != null)
        {
            controller.SetOperativeByHeroIndex(heroIndex);
        }
    }
}
