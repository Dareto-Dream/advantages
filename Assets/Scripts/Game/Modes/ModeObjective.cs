using UnityEngine;

public abstract class ModeObjective : MonoBehaviour
{
    public abstract GameMode Mode { get; }

    public virtual Team HeadStartTeam => Team.None;

    public virtual bool UsesMasterTimer => true;

    public virtual float RoundSeconds => MatchSettings.RoundSeconds;

    public virtual bool AllowsRespawns => true;

    public virtual void ResetForRound()
    {
    }

    public abstract Team? TickLive(float deltaTime);

    public virtual bool WantsImmediateOvertime()
    {
        return false;
    }

    public abstract bool ShouldEnterOvertime();

    public virtual void EnterOvertime()
    {
    }

    public abstract Team? TickOvertime(float deltaTime);

    public abstract bool OvertimeExpired { get; }

    public abstract Team ResolveTimerExpiry();

    public abstract Team ResolveOvertimeExpiry();

    public abstract string StatusText { get; }

    public virtual void WriteNetProgress(out float a, out float b, out float c)
    {
        a = 0f;
        b = 0f;
        c = 0f;
    }

    public virtual void ReadNetProgress(float a, float b, float c)
    {
    }
}
