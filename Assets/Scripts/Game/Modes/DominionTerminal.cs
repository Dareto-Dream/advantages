using UnityEngine;

public class DominionTerminal : MonoBehaviour
{
    public enum TerminalState { Idle, Initializing, Active }
    public enum Site { CommsRelay, SupplyDepot, TransitHub }

    [SerializeField] private ContestVolume zone;
    [SerializeField] private Site site = Site.SupplyDepot;
    [Tooltip("Seconds a team alone on the point needs to arm it, or to take it off the other team.")]
    [SerializeField] private float armSeconds = 3f;

    private TerminalState state = TerminalState.Idle;
    private Team owningTeam = Team.None;
    private float armProgress;
    private Team armingTeam = Team.None;

    public TerminalState State => state;
    public Team OwningTeam => owningTeam;
    public Site TerminalSite => site;

    public float InitProgress01 => armSeconds > 0f ? Mathf.Clamp01(armProgress / armSeconds) : 0f;

    public Team ArmingTeam => armingTeam;

    public void ResetTerminal()
    {
        state = TerminalState.Idle;
        owningTeam = Team.None;
        armProgress = 0f;
        armingTeam = Team.None;
    }

    public void Tick(float deltaTime)
    {
        if (zone == null)
        {
            return;
        }

        Team sole = zone.SoleOccupant();

        if (sole == Team.None || sole == owningTeam)
        {
            armProgress = 0f;
            armingTeam = Team.None;
            return;
        }

        if (armingTeam != sole)
        {
            armingTeam = sole;
            armProgress = 0f;
        }

        armProgress += deltaTime;

        if (armProgress >= armSeconds)
        {

            state = TerminalState.Active;
            owningTeam = sole;
            armProgress = 0f;
            armingTeam = Team.None;
        }
    }
}
