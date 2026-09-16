using UnityEngine;

public class ExtractionObjective : ModeObjective
{
    [SerializeField] private ContestVolume zone;
    [Tooltip("The rail the payload rides. Attacker end is -100%, middle node 0%, defender end +100%.")]
    [SerializeField] private PayloadPath path;
    [Tooltip("Percent per second, per player on point, while solely controlled.")]
    [SerializeField] private float speedPerPlayer = 3f;
    [Tooltip("Percent per second the unattended payload drifts toward the last claimer's zone.")]
    [SerializeField] private float driftSpeed = 1.5f;

    private float position;
    private Team lastClaimant = Team.None;

    public override GameMode Mode => GameMode.Extraction;

    public override bool UsesMasterTimer => false;

    public float Position => position;

    public Team Claimant => lastClaimant;

    private void Awake()
    {
        BotBrain.ObjectiveAnchors.Clear();

        if (zone != null)
        {
            BotBrain.ObjectiveAnchors.Add(zone.transform);
        }
    }

    public override void ResetForRound()
    {
        position = 0f;
        lastClaimant = Team.None;
        UpdateVisualPosition();
    }

    public override Team? TickLive(float deltaTime)
    {
        if (zone == null)
        {
            return null;
        }

        int attackers = zone.CountAlive(Team.Attackers);
        int defenders = zone.CountAlive(Team.Defenders);

        if (attackers > 0 && defenders > 0)
        {

        }
        else if (attackers > 0)
        {
            lastClaimant = Team.Attackers;
            position -= speedPerPlayer * attackers * deltaTime;
        }
        else if (defenders > 0)
        {
            lastClaimant = Team.Defenders;
            position += speedPerPlayer * defenders * deltaTime;
        }
        else if (lastClaimant != Team.None)
        {
            position += lastClaimant == Team.Attackers ? -driftSpeed * deltaTime : driftSpeed * deltaTime;
        }

        position = Mathf.Clamp(position, -100f, 100f);
        UpdateVisualPosition();

        if (position <= -100f)
        {
            return Team.Attackers;
        }

        if (position >= 100f)
        {
            return Team.Defenders;
        }

        return null;
    }

    public override bool ShouldEnterOvertime() => false;

    public override Team? TickOvertime(float deltaTime) => null;

    public override bool OvertimeExpired => true;

    public override Team ResolveTimerExpiry()
    {
        if (Mathf.Approximately(position, 0f))
        {
            return Team.None;
        }

        return position < 0f ? Team.Attackers : Team.Defenders;
    }

    public override Team ResolveOvertimeExpiry() => Team.None;

    private void UpdateVisualPosition()
    {
        if (zone == null || path == null || !path.IsValid)
        {
            return;
        }

        zone.transform.position = path.Evaluate(position);
    }

    public override string StatusText
    {
        get
        {
            string holder = lastClaimant == Team.None ? "NEUTRAL" : lastClaimant.DisplayName().ToUpperInvariant();
            return $"PAYLOAD {position:0}%  ·  {holder}";
        }
    }

    public override void WriteNetProgress(out float a, out float b, out float c)
    {
        a = position;
        b = 0f;
        c = 0f;
    }

    public override void ReadNetProgress(float a, float b, float c)
    {
        position = a;
    }
}
