using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class MainMenuController : MonoBehaviour
{
    private enum Screen
    {
        Boot,
        Home,
        Title,
        Lobby,
        HeroSelect,
        Settings,
        Controls,
        Heroes,
        Mail,
        Friends
    }

    [Header("Screens")]
    [Tooltip("Click-to-start screen. Shown once per launch, before the home page.")]
    [SerializeField] private GameObject bootPanel;
    [Tooltip("Home page: the animation slot.")]
    [SerializeField] private GameObject homePanel;
    [Tooltip("Legacy title screen. Kept for the old wiring; the home page replaced it as the landing.")]
    [SerializeField] private GameObject titlePanel;
    [Tooltip("Play menu: team composition, hero centre-piece, Play.")]
    [SerializeField] private GameObject lobbyPanel;
    [Tooltip("Hero picker opened from Change Hero.")]
    [SerializeField] private GameObject heroSelectPanel;
    [SerializeField] private GameObject settingsPanel;
    [SerializeField] private GameObject controlsPanel;
    [SerializeField] private GameObject heroesPanel;
    [SerializeField] private GameObject mailPanel;
    [SerializeField] private GameObject friendsPanel;

    [Tooltip("Top navigation bar. Lives outside every screen so it stays put; shown on every screen except boot.")]
    [SerializeField] private GameObject topBar;

    [Header("Boot / login")]
    [Tooltip("The CLICK TO START / LOGGING IN line on the boot screen.")]
    [SerializeField] private TextMeshProUGUI bootStatusLabel;
    [SerializeField] private string clickToStartText = "CLICK TO START";
    [SerializeField] private string loggingInText = "LOGGING IN…";
    [Tooltip("Seconds the fake login sits on LOGGING IN before the home page opens.")]
    [SerializeField] private float loginDelay = 1.1f;

    [Header("Play menu")]
    [Tooltip("One line per team slot. Slot 0 is you; the rest are open friend slots.")]
    [SerializeField] private List<TextMeshProUGUI> teamSlots = new List<TextMeshProUGUI>();
    [Tooltip("Centre-piece label showing the current main hero.")]
    [SerializeField] private TextMeshProUGUI heroNameLabel;
    [Tooltip("Centre-piece icon swatch, tinted to the current main hero's colour.")]
    [SerializeField] private Image heroIconImage;

    [Header("Settings")]
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityLabel;

    [Header("Palette")]
    [SerializeField] private Color accent = new Color(0.20f, 0.85f, 0.72f, 1f);
    [SerializeField] private Color muted = new Color(0.62f, 0.67f, 0.75f, 1f);

    private static bool bootScreenDismissed;

    private Screen current = Screen.Boot;
    private bool loggingIn;

    private void Awake()
    {
        MatchSettings.Load();
    }

    private void Start()
    {
        CursorService.Unlock();
        Time.timeScale = 1f;

        SeedWidgets();

        if (bootPanel != null && !bootScreenDismissed)
        {
            if (bootStatusLabel != null)
            {
                bootStatusLabel.text = clickToStartText;
            }

            Show(Screen.Boot);
        }
        else
        {
            ShowHome();
        }
    }

    private void SeedWidgets()
    {
        if (sensitivitySlider != null)
        {
            sensitivitySlider.SetValueWithoutNotify(MatchSettings.MouseSensitivity);
        }

        RefreshSensitivityLabel();
    }

    private void Show(Screen screen)
    {

        if (current == Screen.Settings && screen != Screen.Settings)
        {
            MatchSettings.Save();
        }

        current = screen;

        SetActive(bootPanel, screen == Screen.Boot);
        SetActive(homePanel, screen == Screen.Home);
        SetActive(titlePanel, screen == Screen.Title);
        SetActive(lobbyPanel, screen == Screen.Lobby);
        SetActive(heroSelectPanel, screen == Screen.HeroSelect);
        SetActive(settingsPanel, screen == Screen.Settings);
        SetActive(controlsPanel, screen == Screen.Controls);
        SetActive(heroesPanel, screen == Screen.Heroes);
        SetActive(mailPanel, screen == Screen.Mail);
        SetActive(friendsPanel, screen == Screen.Friends);

        SetActive(topBar, screen != Screen.Boot);
    }

    private static void SetActive(GameObject target, bool value)
    {
        if (target != null)
        {
            target.SetActive(value);
        }
    }

    private IEnumerator LoginThenHome()
    {
        yield return new WaitForSecondsRealtime(Mathf.Max(0f, loginDelay));

        bootScreenDismissed = true;
        loggingIn = false;
        ShowHome();
    }

    private void RefreshLobby()
    {
        for (int i = 0; i < teamSlots.Count; i++)
        {
            if (teamSlots[i] == null)
            {
                continue;
            }

            teamSlots[i].text = i == 0
                ? $"<color=#{Hex(accent)}>{MatchSettings.PlayerName}</color>\n<size=60%><color=#{Hex(muted)}>YOU</color></size>"
                : $"<color=#{Hex(muted)}>OPEN</color>";
        }

        RefreshHero();
    }

    private void RefreshHero()
    {
        if (HeroRoster.All.Length == 0)
        {
            return;
        }

        HeroDefinition hero = HeroRoster.Get(MatchSettings.HeroIndex);

        if (heroNameLabel != null)
        {
            heroNameLabel.text = hero.name.ToUpperInvariant();
        }

        if (heroIconImage != null)
        {
            heroIconImage.color = hero.color;
        }
    }

    private void RefreshSensitivityLabel()
    {
        if (sensitivityLabel != null)
        {
            sensitivityLabel.text = $"Mouse sensitivity  ·  {MatchSettings.MouseSensitivity:0.00}";
        }
    }

    private static string Hex(Color color)
    {
        return ColorUtility.ToHtmlStringRGB(color);
    }

    public void DismissBootScreen()
    {
        if (bootScreenDismissed || loggingIn)
        {
            return;
        }

        loggingIn = true;

        if (bootStatusLabel != null)
        {
            bootStatusLabel.text = loggingInText;
        }

        if (bootPanel != null && bootPanel.TryGetComponent(out Button bootButton))
        {
            bootButton.interactable = false;
        }

        StartCoroutine(LoginThenHome());
    }

    public void ShowHome()
    {
        Show(Screen.Home);
    }

    public void ShowTitle()
    {
        Show(Screen.Title);
    }

    public void ShowLobby()
    {
        Show(Screen.Lobby);
        RefreshLobby();
    }

    public void ShowSettings()
    {
        Show(Screen.Settings);
    }

    public void ShowControls()
    {
        Show(Screen.Controls);
    }

    public void ShowHeroes()
    {
        Show(Screen.Heroes);
    }

    public void ShowMail()
    {
        Show(Screen.Mail);
    }

    public void ShowFriends()
    {
        Show(Screen.Friends);
    }

    public void ChangeHero()
    {
        Show(Screen.HeroSelect);
    }

    public void SelectHero(int index)
    {
        if (HeroRoster.All.Length == 0)
        {
            return;
        }

        MatchSettings.HeroIndex = Mathf.Clamp(index, 0, HeroRoster.All.Length - 1);
        RefreshHero();
        ShowLobby();
    }

    public void EnterQueue()
    {
        StartMatch();
    }

    public void SetMouseSensitivity(float value)
    {
        MatchSettings.MouseSensitivity = value;
        RefreshSensitivityLabel();
    }

    public void StartMatch()
    {
        if (OnlineMenuPanel.Instance != null)
        {
            OnlineMenuPanel.Instance.BeginQueue();
            return;
        }

        Debug.LogWarning("[MainMenu] OnlineMenuPanel isn't up yet - falling back to an offline match.");
        SceneFlow.StartMatch();
    }

    public void Quit()
    {
        SceneFlow.Quit();
    }
}
