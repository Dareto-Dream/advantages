using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
public class ContestVolume : MonoBehaviour
{
    private readonly HashSet<Health> occupants = new HashSet<Health>();

    private void Awake()
    {
        GetComponent<Collider>().isTrigger = true;
    }

    private void OnTriggerEnter(Collider other)
    {
        Health health = other.GetComponentInParent<Health>();
        if (health != null)
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

    public int CountAlive(Team team)
    {
        int count = 0;
        foreach (Health health in occupants)
        {
            if (health != null && health.IsAlive && health.Team == team)
            {
                count++;
            }
        }

        return count;
    }

    public bool IsContested()
    {
        return CountAlive(Team.Attackers) > 0 && CountAlive(Team.Defenders) > 0;
    }

    public bool IsEmpty()
    {
        return CountAlive(Team.Attackers) == 0 && CountAlive(Team.Defenders) == 0;
    }

    public Team SoleOccupant()
    {
        bool attackers = CountAlive(Team.Attackers) > 0;
        bool defenders = CountAlive(Team.Defenders) > 0;

        if (attackers && defenders)
        {
            return Team.None;
        }

        if (attackers)
        {
            return Team.Attackers;
        }

        if (defenders)
        {
            return Team.Defenders;
        }

        return Team.None;
    }
}
