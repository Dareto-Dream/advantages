using UnityEngine;

public class ConvergenceObjective : ModeObjective
{
    [SerializeField] private ContestVolume point;
    [Tooltip("Percent per second the claiming team's bar fills at.")]
    [SerializeField] private float fillRatePerSecond = 1.67f;
    [Tooltip("Length of the Overtime countdown shown while a 99% team is contested.")]
    [SerializeField] private float overtimeSeconds = 15f;

    private float attackerProgress;
    private float defenderProgress;
    private Team lastSoleClaimant = Team.None;
    private bool overtimeFlag;
    private float overtimeRemaining;

    public override GameMode Mode => GameMode.Convergence;

    public override bool UsesMasterTimer => false;

    public float AttackerProgress => attackerProgress;
    public float DefenderProgress => defenderProgress;

    public bool OvertimeActive => overtimeFlag;

    public float OvertimeRemaining => Mathf.Max(0f, overtimeRemaining);

    public float OvertimeFraction01 => overtimeSeconds <= 0f ? 0f : Mathf.Clamp01(overtimeRemaining / overtimeSeconds);

    public Team Claimant => lastSoleClaimant;

    private void Awake()
    {
        BotBrain.ObjectiveAnchors.Clear();

        if (point != null)
        {
            BotBrain.ObjectiveAnchors.Add(point.transform);
        }
    }

    public override void ResetForRound()
    {
        attackerProgress = 0f;
        defenderProgress = 0f;
        lastSoleClaimant = Team.None;
        overtimeFlag = false;
        overtimeRemaining = 0f;
    }

    public override Team? TickLive(float deltaTime)
    {
        if (point == null)
        {
            return null;
        }

        bool contested = point.IsContested();
        Team sole = point.SoleOccupant();

        if (sole != Team.None)
        {
            lastSoleClaimant = sole;
        }

        if (lastSoleClaimant != Team.None)
        {
            float cap = contested ? 99f : 100f;

            if (lastSoleClaimant == Team.Attackers)
            {
                attackerProgress = Mathf.Min(cap, attackerProgress + fillRatePerSecond * deltaTime);
            }
            else
            {
                defenderProgress = Mathf.Min(cap, defenderProgress + fillRatePerSecond * deltaTime);
            }
        }

        TickOvertimeBar(contested, deltaTime);

        if (attackerProgress >= 100f)
        {
            return Team.Attackers;
        }

        if (defenderProgress >= 100f)
        {
            return Team.Defenders;
        }

        return null;
    }

    private void TickOvertimeBar(bool contested, float deltaTime)
    {
        float leaderProgress = lastSoleClaimant == Team.Attackers ? attackerProgress
            : lastSoleClaimant == Team.Defenders ? defenderProgress
            : 0f;

        bool shouldRun = contested && leaderProgress >= 99f;

        if (!shouldRun)
        {
            overtimeFlag = false;
            overtimeRemaining = 0f;
            return;
        }

        if (!overtimeFlag)
        {
            overtimeFlag = true;
            overtimeRemaining = overtimeSeconds;
        }

        overtimeRemaining = Mathf.Max(0f, overtimeRemaining - deltaTime);
    }

    public override bool ShouldEnterOvertime() => false;

    public override Team? TickOvertime(float deltaTime) => null;

    public override bool OvertimeExpired => true;

    public override Team ResolveTimerExpiry()
    {
        if (Mathf.Approximately(attackerProgress, defenderProgress))
        {
            return Team.None;
        }

        return attackerProgress > defenderProgress ? Team.Attackers : Team.Defenders;
    }

    public override Team ResolveOvertimeExpiry() => Team.None;

    public override string StatusText
    {
        get
        {
            string ot = overtimeFlag ? $"  ·  OVERTIME {Mathf.CeilToInt(OvertimeRemaining)}s" : string.Empty;
            return $"ATK {attackerProgress:0}%  ·  DEF {defenderProgress:0}%{ot}";
        }
    }

    public override void WriteNetProgress(out float a, out float b, out float c)
    {
        a = attackerProgress;
        b = defenderProgress;
        c = OvertimeFraction01;
    }

    public override void ReadNetProgress(float a, float b, float c)
    {
        attackerProgress = a;
        defenderProgress = b;
        overtimeFlag = c > 0f;
        overtimeRemaining = c * overtimeSeconds;
    }
}
