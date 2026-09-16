using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class SpawnZone : MonoBehaviour
{
    [SerializeField] private Team owningTeam = Team.None;
    [Tooltip("Health restored per second while inside.")]
    [SerializeField] private float healPerSecond = 55f;
    [Tooltip("Seconds since last taking damage before regen kicks in.")]
    [SerializeField] private float regenDelaySeconds = 1.5f;

    private readonly HashSet<Health> occupants = new HashSet<Health>();
    private readonly List<Health> scratch = new List<Health>();

    public void Configure(Team team, float heal, float delay)
    {
        owningTeam = team;
        healPerSecond = heal;
        regenDelaySeconds = delay;
    }

    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Health health = other.GetComponentInParent<Health>();
        if (health != null && health.Team == owningTeam)
        {
            occupants.Add(health);
        }
    }

    private void OnTriggerExit(Collider other)
    {
        Health health = other.GetComponentInParent<Health>();
        if (health != null)
        {
            occupants.Remove(health);
        }
    }

    private void OnDisable()
    {
        occupants.Clear();
    }

    private void Update()
    {
        if (occupants.Count == 0)
        {
            return;
        }

        float amount = healPerSecond * Time.deltaTime;

        scratch.Clear();
        scratch.AddRange(occupants);

        foreach (Health health in scratch)
        {
            if (health == null || !health.IsAlive)
            {
                occupants.Remove(health);
                continue;
            }

            if (health.CurrentHealth < health.MaxHealth && health.TimeSinceDamage >= regenDelaySeconds)
            {
                health.Heal(amount);
            }
        }
    }
}
