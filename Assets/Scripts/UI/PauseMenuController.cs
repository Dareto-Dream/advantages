using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class PauseMenuController : MonoBehaviour
{
    [Header("References")]
    [Tooltip("The scrim and its contents. Toggled to open and close the menu.")]
    [SerializeField] private GameObject panel;
    [SerializeField] private Slider sensitivitySlider;
    [SerializeField] private TextMeshProUGUI sensitivityValue;

    private bool isOpen;

    private void Start()
    {
        MatchSettings.Load();

        if (sensitivitySlider != null)
        {

            sensitivitySlider.SetValueWithoutNotify(MatchSettings.MouseSensitivity);
        }

        RefreshSensitivityLabel();
        SetOpen(false);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.escapeKey.wasPressedThisFrame)
        {
            SetOpen(!isOpen);
        }
    }

    public void SetOpen(bool open)
    {
        isOpen = open;

        if (panel != null)
        {
            panel.SetActive(open);
        }

        CursorService.Set(!open);

        MatchManager match = MatchManager.Instance;
        if (match != null && match.Player != null)
        {
            bool live = match.CurrentPhase == MatchManager.Phase.Live;
            match.Player.SetControlEnabled(!open && live);
        }

        Time.timeScale = open ? 0f : 1f;
    }

    private void RefreshSensitivityLabel()
    {
        if (sensitivityValue != null)
        {
            sensitivityValue.text = $"Mouse sensitivity  {MatchSettings.MouseSensitivity:0.00}";
        }
    }

    public void Resume()
    {
        SetOpen(false);
    }

    public void RestartMatch()
    {
        SetOpen(false);

        if (MatchManager.Instance != null)
        {
            MatchManager.Instance.RestartMatch();
        }
    }

    public void LeaveToMenu()
    {
        SceneFlow.GoToMainMenu();
    }

    public void SetMouseSensitivity(float value)
    {
        MatchSettings.MouseSensitivity = value;
        MatchSettings.Save();
        RefreshSensitivityLabel();

        MatchManager match = MatchManager.Instance;
        if (match != null && match.Player != null)
        {
            match.Player.Look.SetSensitivityScale(value);
        }
    }
}
