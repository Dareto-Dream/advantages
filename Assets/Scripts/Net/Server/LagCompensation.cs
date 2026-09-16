using System.Collections.Generic;
using UnityEngine;

public static class LagCompensation
{
    private struct Sample
    {
        public float time;
        public Vector3 position;
        public Quaternion rotation;
    }

    private sealed class Track
    {
        public readonly List<Sample> samples = new List<Sample>(64);
        public Vector3 restorePosition;
        public Quaternion restoreRotation;
        public bool rewound;
    }

    private static readonly Dictionary<Health, Track> tracks = new Dictionary<Health, Track>();
    private static readonly List<Health> scratch = new List<Health>();
    private static bool rewindActive;

    public static void Capture(float now)
    {
        if (!NetContext.IsServer)
        {
            return;
        }

        scratch.Clear();

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null)
            {
                continue;
            }

            scratch.Add(health);

            if (!tracks.TryGetValue(health, out Track track))
            {
                track = new Track();
                tracks[health] = track;
            }

            track.samples.Add(new Sample
            {
                time = now,
                position = health.transform.position,
                rotation = health.transform.rotation,
            });

            float cutoff = now - NetConfig.LagCompensationWindow;
            int drop = 0;
            while (drop < track.samples.Count - 1 && track.samples[drop].time < cutoff)
            {
                drop++;
            }

            if (drop > 0)
            {
                track.samples.RemoveRange(0, drop);
            }
        }

        if (tracks.Count > scratch.Count)
        {
            List<Health> stale = new List<Health>();

            foreach (KeyValuePair<Health, Track> entry in tracks)
            {
                if (entry.Key == null || !scratch.Contains(entry.Key))
                {
                    stale.Add(entry.Key);
                }
            }

            foreach (Health key in stale)
            {
                tracks.Remove(key);
            }
        }
    }

    public static void Rewind(GameObject shooter, float rewindTo)
    {
        if (!NetContext.IsServer || rewindActive)
        {
            return;
        }

        Health shooterHealth = shooter != null ? shooter.GetComponentInParent<Health>() : null;
        bool moved = false;

        foreach (KeyValuePair<Health, Track> entry in tracks)
        {
            Health health = entry.Key;
            Track track = entry.Value;

            if (health == null || health == shooterHealth || track.samples.Count == 0)
            {
                continue;
            }

            if (!SampleAt(track, rewindTo, out Vector3 position, out Quaternion rotation))
            {
                continue;
            }

            track.restorePosition = health.transform.position;
            track.restoreRotation = health.transform.rotation;
            track.rewound = true;

            health.transform.SetPositionAndRotation(position, rotation);
            moved = true;
        }

        if (moved)
        {

            Physics.SyncTransforms();
        }

        rewindActive = true;
    }

    public static void Restore()
    {
        if (!rewindActive)
        {
            return;
        }

        rewindActive = false;
        bool moved = false;

        foreach (KeyValuePair<Health, Track> entry in tracks)
        {
            Track track = entry.Value;
            if (!track.rewound)
            {
                continue;
            }

            track.rewound = false;

            if (entry.Key != null)
            {
                entry.Key.transform.SetPositionAndRotation(track.restorePosition, track.restoreRotation);
                moved = true;
            }
        }

        if (moved)
        {
            Physics.SyncTransforms();
        }
    }

    private static bool SampleAt(Track track, float time, out Vector3 position, out Quaternion rotation)
    {
        List<Sample> samples = track.samples;

        if (samples.Count == 0)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            return false;
        }

        if (time <= samples[0].time)
        {
            position = samples[0].position;
            rotation = samples[0].rotation;
            return true;
        }

        Sample newest = samples[samples.Count - 1];
        if (time >= newest.time)
        {
            position = newest.position;
            rotation = newest.rotation;
            return true;
        }

        for (int i = samples.Count - 1; i > 0; i--)
        {
            Sample later = samples[i];
            Sample earlier = samples[i - 1];

            if (time < earlier.time)
            {
                continue;
            }

            float span = later.time - earlier.time;
            float t = span > 0.0001f ? (time - earlier.time) / span : 0f;

            position = Vector3.Lerp(earlier.position, later.position, t);
            rotation = Quaternion.Slerp(earlier.rotation, later.rotation, t);
            return true;
        }

        position = newest.position;
        rotation = newest.rotation;
        return true;
    }

    public static float RewindTimeFor(float roundTripTime)
    {
        float latency = Mathf.Clamp(roundTripTime * 0.5f, 0f, NetConfig.LagCompensationWindow);
        return Time.time - latency - NetConfig.InterpolationDelay;
    }

    public static void Clear()
    {
        Restore();
        tracks.Clear();
    }
}
