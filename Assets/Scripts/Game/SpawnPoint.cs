using System.Collections.Generic;
using UnityEngine;

public class SpawnPoint : MonoBehaviour
{
    private static readonly List<SpawnPoint> all = new List<SpawnPoint>();

    [SerializeField] private Team team = Team.Attackers;

    public Team Team => team;

    public static IReadOnlyList<SpawnPoint> All => all;

    private void OnEnable()
    {
        all.Add(this);
    }

    private void OnDisable()
    {
        all.Remove(this);
    }

    public void SetTeam(Team value)
    {
        team = value;
    }

    public static List<SpawnPoint> For(Team team)
    {
        List<SpawnPoint> result = new List<SpawnPoint>();
        foreach (SpawnPoint point in all)
        {
            if (point.team == team)
            {
                result.Add(point);
            }
        }

        return result;
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = team == Team.Attackers ? new Color(0.95f, 0.45f, 0.2f) : new Color(0.25f, 0.65f, 1f);
        Gizmos.DrawWireSphere(transform.position + Vector3.up, 0.5f);
        Gizmos.DrawRay(transform.position + Vector3.up, transform.forward * 1.5f);
    }
}
