using UnityEngine;

public class BreachObjective : ModeObjective
{
    [SerializeField] private ContestVolume point;
    [Tooltip("Percent per second the bar fills while an attacker is on point.")]
    [SerializeField] private float fillRatePerSecond = 2.2f;
    [Tooltip("Percent per second the bar decays toward its floor while unoccupied.")]
    [SerializeField] private float decayRatePerSecond = 3.3f;
    [SerializeField] private float checkpointTimeBonusSeconds = 20f;
    [SerializeField] private float overtimeSeconds = 25f;

    private static readonly float[] Checkpoints = { 25f, 50f, 75f };

    private float capture;
    private float floor;
    private int nextCheckpointIndex;
    private bool overtimeActive;
    private float overtimeRemaining;

    public override GameMode Mode => GameMode.Breach;

    public override Team HeadStartTeam => Team.Defenders;

    public float Capture => capture;
    public float Floor => floor;

    public int CheckpointsReached => nextCheckpointIndex;

    public static int CheckpointCount => Checkpoints.Length;

    public bool OvertimeActive => overtimeActive;

    public float OvertimeRemaining => Mathf.Max(0f, overtimeRemaining);

    public bool AttackerContesting => AttackerOnPoint;

    private bool AttackerOnPoint => point != null && point.CountAlive(Team.Attackers) > 0;

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
        capture = 0f;
        floor = 0f;
        nextCheckpointIndex = 0;
        overtimeActive = false;
        overtimeRemaining = 0f;
    }

    public override Team? TickLive(float deltaTime)
    {
        Advance(deltaTime);
        return capture >= 100f ? Team.Attackers : (Team?)null;
    }

    private void Advance(float deltaTime)
    {
        if (point == null)
        {
            return;
        }

        capture = AttackerOnPoint
            ? Mathf.Min(100f, capture + fillRatePerSecond * deltaTime)
            : Mathf.Max(floor, capture - decayRatePerSecond * deltaTime);

        while (nextCheckpointIndex < Checkpoints.Length && capture >= Checkpoints[nextCheckpointIndex])
        {
            floor = Checkpoints[nextCheckpointIndex];
            nextCheckpointIndex++;

            if (MatchManager.Instance != null)
            {
                MatchManager.Instance.ExtendPhaseTimer(checkpointTimeBonusSeconds);
            }
        }
    }

    public override bool ShouldEnterOvertime() => AttackerOnPoint;

    public override void EnterOvertime()
    {
        overtimeActive = true;
        overtimeRemaining = overtimeSeconds;
    }

    public override Team? TickOvertime(float deltaTime)
    {

        if (!AttackerOnPoint)
        {
            overtimeRemaining -= deltaTime;
        }

        Advance(deltaTime);
        return capture >= 100f ? Team.Attackers : (Team?)null;
    }

    public override bool OvertimeExpired => overtimeActive && overtimeRemaining <= 0f;

    public override Team ResolveTimerExpiry() => Team.Defenders;

    public override Team ResolveOvertimeExpiry() => Team.Defenders;

    public override string StatusText
    {
        get
        {
            string ot = overtimeActive ? $"  ·  OT {Mathf.CeilToInt(Mathf.Max(0f, overtimeRemaining))}s" : string.Empty;
            return $"CAPTURE {capture:0}%  (floor {floor:0}%){ot}";
        }
    }

    public override void WriteNetProgress(out float a, out float b, out float c)
    {
        a = capture;
        b = floor;
        c = nextCheckpointIndex;
    }

    public override void ReadNetProgress(float a, float b, float c)
    {
        capture = a;
        floor = b;
        nextCheckpointIndex = Mathf.RoundToInt(c);
    }
}
