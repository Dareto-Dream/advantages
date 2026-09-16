public enum Team
{
    None = 0,
    Attackers = 1,
    Defenders = 2
}

public static class TeamExtensions
{
    public static bool IsHostileTo(this Team team, Team other)
    {
        if (team == Team.None || other == Team.None)
        {
            return true;
        }

        return team != other;
    }

    public static string DisplayName(this Team team)
    {
        switch (team)
        {
            case Team.Attackers: return "Attackers";
            case Team.Defenders: return "Defenders";
            default: return "Neutral";
        }
    }
}
