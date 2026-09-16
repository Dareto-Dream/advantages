using System.Collections.Generic;
using UnityEngine;

[RequireComponent(typeof(Collider))]
[RequireComponent(typeof(MeshRenderer))]
public class SpawnPanel : MonoBehaviour
{
    [SerializeField] private Team owningTeam = Team.None;
    [Tooltip("Green - shown when this is the local player's own spawn.")]
    [SerializeField] private Material friendlyMaterial;
    [Tooltip("Red - shown when this is the enemy spawn.")]
    [SerializeField] private Material hostileMaterial;

    private static readonly List<SpawnPanel> all = new List<SpawnPanel>();

    private Collider panelCollider;
    private MeshRenderer panelRenderer;
    private MatchManager match;
    private bool matchStarted;

    public Team OwningTeam => owningTeam;

    public static void RefreshAllPassage()
    {
        for (int i = 0; i < all.Count; i++)
        {
            if (all[i] != null)
            {
                all[i].RefreshPassage();
            }
        }
    }

    public void SetOwningTeam(Team team)
    {
        owningTeam = team;
    }

    private void Awake()
    {
        panelCollider = GetComponent<Collider>();
        panelRenderer = GetComponent<MeshRenderer>();
    }

    private void OnEnable()
    {
        all.Add(this);
        Bind();
    }

    private void Start()
    {
        Bind();
        ApplyTeamColour();
        RefreshPassage();
    }

    private void OnDisable()
    {
        all.Remove(this);

        if (match != null)
        {
            match.PhaseChanged -= HandlePhaseChanged;
            match = null;
        }
    }

    private void Bind()
    {
        if (match != null)
        {
            return;
        }

        match = MatchManager.Instance;
        if (match != null)
        {
            match.PhaseChanged += HandlePhaseChanged;
        }
    }

    private void HandlePhaseChanged(MatchManager.Phase phase)
    {
        if (phase != MatchManager.Phase.Staging && phase != MatchManager.Phase.HeroSelect)
        {
            matchStarted = true;
        }

        RefreshPassage();
    }

    private void ApplyTeamColour()
    {
        bool friendly = owningTeam == MatchSettings.PlayerTeam;
        Material chosen = friendly ? friendlyMaterial : hostileMaterial;

        if (chosen != null && panelRenderer != null)
        {
            panelRenderer.sharedMaterial = chosen;
        }
    }

    private bool OwnerHasHeadStart =>
        match != null && match.Objective != null && match.Objective.HeadStartTeam == owningTeam;

    private bool OwnerCanWalkThrough => matchStarted || OwnerHasHeadStart;

    public bool BlocksProjectile(Team shooterTeam)
    {
        if (shooterTeam != owningTeam)
        {
            return true;
        }

        return !matchStarted;
    }

    private void RefreshPassage()
    {
        if (panelCollider == null)
        {
            return;
        }

        bool open = OwnerCanWalkThrough;

        foreach (Health health in CombatantRegistry.All)
        {
            if (health == null)
            {
                continue;
            }

            bool ignore = open && health.Team == owningTeam;
            foreach (Collider bodyCollider in health.GetComponentsInChildren<Collider>(true))
            {
                if (bodyCollider != null)
                {
                    Physics.IgnoreCollision(panelCollider, bodyCollider, ignore);
                }
            }
        }
    }
}
