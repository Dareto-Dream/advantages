using System;
using System.Collections.Generic;
using UnityEngine;

public class DominionObjective : ModeObjective
{
    [SerializeField] private DominionTerminal[] terminals = new DominionTerminal[0];
    [Tooltip("Percent per second each held point adds to its owner's bar.")]
    [SerializeField] private float ratePerPointPerSecond = 6f;

    private float attackerPool;
    private float defenderPool;
    private bool overtimeFlag;
    private Team overtimeTeam = Team.None;

    public override GameMode Mode => GameMode.Dominion;

    public override bool UsesMasterTimer => false;

    public float AttackerPool => attackerPool;
    public float DefenderPool => defenderPool;

    public float DataTarget => 100f;

    public IReadOnlyList<DominionTerminal> Terminals => terminals;

    public bool OvertimeActive => overtimeFlag;

    public Team OvertimeTeam => overtimeTeam;

    private void Awake()
    {
        BotBrain.ObjectiveAnchors.Clear();

        foreach (DominionTerminal terminal in terminals)
        {
            if (terminal != null)
            {
                BotBrain.ObjectiveAnchors.Add(terminal.transform);
            }
        }
    }

    public override void ResetForRound()
    {
        attackerPool = 0f;
        defenderPool = 0f;
        overtimeFlag = false;
        overtimeTeam = Team.None;

        foreach (DominionTerminal terminal in terminals)
        {
            if (terminal != null)
            {
                terminal.ResetTerminal();
            }
        }
    }

    public override Team? TickLive(float deltaTime)
    {
        foreach (DominionTerminal terminal in terminals)
        {
            terminal?.Tick(deltaTime);
        }

        int attackerPoints = PointsHeldBy(Team.Attackers);
        int defenderPoints = PointsHeldBy(Team.Defenders);

        attackerPool = Accrue(attackerPool, attackerPoints, defenderPoints, deltaTime);
        defenderPool = Accrue(defenderPool, defenderPoints, attackerPoints, deltaTime);

        overtimeTeam = attackerPool >= 99f ? Team.Attackers : defenderPool >= 99f ? Team.Defenders : Team.None;
        overtimeFlag = overtimeTeam != Team.None
                       && PointsHeldBy(overtimeTeam == Team.Attackers ? Team.Defenders : Team.Attackers) > 0;

        if (attackerPool >= 100f)
        {
            return Team.Attackers;
        }

        if (defenderPool >= 100f)
        {
            return Team.Defenders;
        }

        return null;
    }

    private float Accrue(float pool, int ownPoints, int enemyPoints, float deltaTime)
    {
        if (ownPoints <= 0)
        {
            return pool;
        }

        float cap = enemyPoints > 0 ? 99f : 100f;
        return Mathf.Min(cap, pool + ratePerPointPerSecond * ownPoints * deltaTime);
    }

    private int PointsHeldBy(Team team)
    {
        int count = 0;
        foreach (DominionTerminal terminal in terminals)
        {
            if (terminal != null && terminal.State == DominionTerminal.TerminalState.Active && terminal.OwningTeam == team)
            {
                count++;
            }
        }

        return count;
    }

    public override bool WantsImmediateOvertime() => false;

    public override bool ShouldEnterOvertime() => false;

    public override Team? TickOvertime(float deltaTime) => null;

    public override bool OvertimeExpired => true;

    public override Team ResolveTimerExpiry()
    {
        if (Mathf.Approximately(attackerPool, defenderPool))
        {
            return Team.None;
        }

        return attackerPool > defenderPool ? Team.Attackers : Team.Defenders;
    }

    public override Team ResolveOvertimeExpiry() => Team.None;

    public override string StatusText
    {
        get
        {
            string ot = overtimeFlag ? "  ·  OVERTIME" : string.Empty;
            return $"ATK {attackerPool:0}%  ·  DEF {defenderPool:0}%{ot}";
        }
    }

    public override void WriteNetProgress(out float a, out float b, out float c)
    {
        a = attackerPool;
        b = defenderPool;
        c = 0f;
    }

    public override void ReadNetProgress(float a, float b, float c)
    {
        attackerPool = a;
        defenderPool = b;
    }
}
