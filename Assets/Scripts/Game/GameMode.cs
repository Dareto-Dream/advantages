public enum GameMode
{
    Convergence,
    Extraction,
    Breach,
    Dominion
}

public static class GameModeInfo
{
    public struct Entry
    {
        public GameMode mode;
        public string sceneName;
        public string displayName;
        public string mapName;
    }

    public static readonly Entry[] All =
    {
        new Entry { mode = GameMode.Convergence, sceneName = "Arena_Convergence", displayName = "Convergence", mapName = "The Spire Root" },
        new Entry { mode = GameMode.Extraction, sceneName = "Arena_Extraction", displayName = "Extraction", mapName = "Vault Approach" },
        new Entry { mode = GameMode.Breach, sceneName = "Arena_Breach", displayName = "Breach", mapName = "Reactor Core" },
        new Entry { mode = GameMode.Dominion, sceneName = "Arena_Dominion", displayName = "Dominion", mapName = "The Scattered Ruins" }
    };

    public static Entry Get(GameMode mode)
    {
        foreach (Entry entry in All)
        {
            if (entry.mode == mode)
            {
                return entry;
            }
        }

        return All[0];
    }

    public static string SceneFor(GameMode mode)
    {
        return Get(mode).sceneName;
    }
}
