using UnityEngine;
using UnityEngine.SceneManagement;


// Main Big kahuna for scene managing. this is the only place that should call sceneManager.LoadScene() directly.
// this helps ensure that loading screen is always used, and the cursor is unlocked when leaving a match
public static class SceneFlow
{
    public const string MainMenu = "MainMenu";

    public const string Arena = "Arena";

    public static void GoToMainMenu()
    {
        // unlock cursor and reset time when you go back to main menu, 
        // so you can actually use the menu lulz
        CursorService.Unlock();
        UnityEngine.Time.timeScale = 1f;
        SceneManager.LoadScene(MainMenu);
    }

    public static void StartMatch(GameMode? forcedMode = null)
    {
        // pick a random mode if the debug menu didnt force when (this should be removed in v1)
        MatchSettings.SelectedMode = forcedMode ?? PickRandomMode();
        MatchSettings.Save();
        UnityEngine.Time.timeScale = 1f;

        // pull info for selected mode, and load translation for it (there is no translation its just a list 😭)
        string sceneName = GameModeInfo.SceneFor(MatchSettings.SelectedMode);

        // find our loading screen if it exists, then load the scene through it
        // loader is not required to boot in, this is to prevent stupid crashes
        LoadingScreenController loader = Object.FindAnyObjectByType<LoadingScreenController>();
        if (loader != null)
        {
            loader.BeginLoad(sceneName);
        }
        else
        {
            SceneManager.LoadScene(sceneName);
        }
    }

    // a cool randomizer script that totally works and is not just stolen from stack overflow 😎
    private static GameMode PickRandomMode()
    {
        System.Array values = System.Enum.GetValues(typeof(GameMode));
        return (GameMode)values.GetValue(UnityEngine.Random.Range(0, values.Length));
    }

    // I wonder bro
    public static void Quit()
    {
        #if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
        #else
                UnityEngine.Application.Quit();
        #endif
    }
}
