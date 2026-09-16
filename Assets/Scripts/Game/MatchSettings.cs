using UnityEngine;

public static class MatchSettings
{

    public static int TeamSize = 4;

    public static string PlayerName = "Player";
    public static LoadoutRole PlayerRole = LoadoutRole.Support;
    public static Team PlayerTeam = Team.Attackers;

    public static bool BotsEnabled = true;

    public static int RoundsToWin = 6;
    public static float RoundSeconds = 90f;
    public static bool ComebackBuffEnabled = true;
    public static float MouseSensitivity = 1f;

    public static int HeroIndex = 0;

    public static GameMode SelectedMode = GameMode.Convergence;

    public static void ResetToDefaults()
    {
        PlayerName = "Player";
        PlayerRole = LoadoutRole.Support;
        PlayerTeam = Team.Attackers;
        TeamSize = 4;
        BotsEnabled = true;
        RoundsToWin = 6;
        RoundSeconds = 90f;
        ComebackBuffEnabled = true;
        HeroIndex = 0;
        SelectedMode = GameMode.Convergence;
    }

    public static Team EnemyTeam => PlayerTeam == Team.Attackers ? Team.Defenders : Team.Attackers;

    public static void Load()
    {
        PlayerName = PlayerPrefs.GetString("adv.playerName", PlayerName);
        PlayerRole = (LoadoutRole)PlayerPrefs.GetInt("adv.role", (int)PlayerRole);
        TeamSize = Mathf.Clamp(PlayerPrefs.GetInt("adv.teamSize", TeamSize), 1, 8);
        BotsEnabled = PlayerPrefs.GetInt("adv.bots", BotsEnabled ? 1 : 0) != 0;
        RoundsToWin = PlayerPrefs.GetInt("adv.rounds", RoundsToWin);
        MouseSensitivity = PlayerPrefs.GetFloat("adv.sens", MouseSensitivity);
        HeroIndex = PlayerPrefs.GetInt("adv.hero", HeroIndex);
    }

    public static void Save()
    {
        PlayerPrefs.SetString("adv.playerName", PlayerName);
        PlayerPrefs.SetInt("adv.role", (int)PlayerRole);
        PlayerPrefs.SetInt("adv.teamSize", TeamSize);
        PlayerPrefs.SetInt("adv.bots", BotsEnabled ? 1 : 0);
        PlayerPrefs.SetInt("adv.rounds", RoundsToWin);
        PlayerPrefs.SetFloat("adv.sens", MouseSensitivity);
        PlayerPrefs.SetInt("adv.hero", HeroIndex);
        PlayerPrefs.Save();
    }
}
