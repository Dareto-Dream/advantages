using UnityEngine;

public static class TeamPalette
{

    public static readonly Color Friendly = new Color(0.28f, 0.62f, 1f, 1f);

    public static readonly Color Hostile = new Color(0.95f, 0.32f, 0.32f, 1f);

    public static readonly Color Neutral = new Color(0.70f, 0.74f, 0.80f, 1f);

    public static Color For(Team team)
    {
        if (team == Team.None)
        {
            return Neutral;
        }

        return team == MatchSettings.PlayerTeam ? Friendly : Hostile;
    }

    public static bool IsFriendly(Team team) => team != Team.None && team == MatchSettings.PlayerTeam;
}
