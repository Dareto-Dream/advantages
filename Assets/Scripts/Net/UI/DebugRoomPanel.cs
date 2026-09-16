using System;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class DebugRoomPanel : MonoBehaviour
{
    private Canvas canvas;
    private GameObject panel;
    private TextMeshProUGUI statusLabel;
    private Button botsButton;
    private float nextRefreshAt;

    private static bool visible;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {

        if (NetConfig.WantsServerRole())
        {
            return;
        }

        UnityEngine.SceneManagement.SceneManager.sceneLoaded += (scene, mode) =>
        {
            if (scene.name == SceneFlow.MainMenu)
            {
                return;
            }

            GameObject host = new GameObject("~DebugRoomPanel");
            host.AddComponent<DebugRoomPanel>();
        };
    }

    private void Start()
    {
        Build();
        SetVisible(visible);
    }

    private void Update()
    {
        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.f1Key.wasPressedThisFrame)
        {
            SetVisible(!visible);
        }

        if (visible && Time.unscaledTime >= nextRefreshAt)
        {
            nextRefreshAt = Time.unscaledTime + 0.5f;
            RefreshStatus();
        }
    }

    private void Build()
    {
        canvas = NetUi.Canvas("DebugRoomCanvas", 200);
        canvas.transform.SetParent(transform, false);

        RectTransform toggleHolder = NetUi.Box(
            canvas.transform,
            "ToggleHolder",
            new Vector2(0f, 0f),
            new Vector2(12f, 12f),
            new Vector2(120f, 30f),
            new Color(0f, 0f, 0f, 0f));

        VerticalLayoutGroup toggleColumn = NetUi.Column(toggleHolder, "Column", 0f, 0);
        NetUi.Button(toggleColumn.transform, "DEBUG (F1)", () => SetVisible(!visible), NetUi.Panel, 30f);

        panel = NetUi.Box(
            canvas.transform,
            "Panel",
            new Vector2(0f, 0f),
            new Vector2(12f, 50f),
            new Vector2(300f, 560f),
            NetUi.Panel).gameObject;

        VerticalLayoutGroup column = NetUi.Column(panel.transform, "Column", 5f, 12);

        NetUi.Label(column.transform, "ROOM CONTROLS", 16f, NetUi.Accent);
        statusLabel = NetUi.Label(column.transform, string.Empty, 12f, NetUi.Dim);
        statusLabel.textWrappingMode = TextWrappingModes.Normal;
        statusLabel.GetComponent<LayoutElement>().minHeight = 88f;

        botsButton = NetUi.Button(column.transform, "BOTS: ON", ToggleBots, NetUi.Accent * 0.5f, 32f);

        HorizontalLayoutGroup botRow = NetUi.Row(column.transform, "BotRow", 4f, 28f);
        NetUi.Button(botRow.transform, "+ BOT", () => Send(RoomCommand.AddBot, 0, () => MatchManager.Instance?.ServerAddBot(Team.None)), null, 28f);
        NetUi.Button(botRow.transform, "- BOT", () => Send(RoomCommand.RemoveBot, 0, () => MatchManager.Instance?.ServerRemoveBot(Team.None)), null, 28f);
        NetUi.Button(botRow.transform, "CLEAR", () => Send(RoomCommand.ClearBots, 0, () => MatchManager.Instance?.ClearAllBots()), null, 28f);

        HorizontalLayoutGroup teamRow = NetUi.Row(column.transform, "TeamRow", 4f, 28f);
        NetUi.Button(teamRow.transform, "TEAM -", () => ChangeTeamSize(-1), null, 28f);
        NetUi.Button(teamRow.transform, "TEAM +", () => ChangeTeamSize(1), null, 28f);

        NetUi.Button(column.transform, "SKIP PHASE", () => Send(RoomCommand.SkipPhase, 0, () => MatchManager.Instance?.ForceEndPhase()), null, 30f);
        NetUi.Button(column.transform, "RESTART MATCH", () => Send(RoomCommand.RestartMatch, 0, () => MatchManager.Instance?.RestartMatch()), null, 30f);
        NetUi.Button(column.transform, "HEAL EVERYONE", () => Send(RoomCommand.HealAll, 0, HealAllLocally), null, 30f);
        NetUi.Button(column.transform, "PAUSE / RESUME", TogglePause, null, 30f);

        NetUi.Label(column.transform, "MODE", 12f, NetUi.Dim);
        HorizontalLayoutGroup modeRow = NetUi.Row(column.transform, "ModeRow", 3f, 26f);

        foreach (GameModeInfo.Entry entry in GameModeInfo.All)
        {
            GameMode mode = entry.mode;
            string label = entry.displayName.Length > 4 ? entry.displayName.Substring(0, 4) : entry.displayName;
            NetUi.Button(modeRow.transform, label.ToUpperInvariant(), () => SwitchMode(mode), null, 26f);
        }

        NetUi.Button(column.transform, "LEAVE MATCH", LeaveMatch, NetUi.Bad * 0.5f, 30f);
    }

    private void SetVisible(bool value)
    {
        visible = value;

        if (panel != null)
        {
            panel.SetActive(value);
        }

        if (value)
        {
            RefreshStatus();
        }
    }

    private void Send(RoomCommand command, int argument, Action offlineAction)
    {
        if (NetContext.IsClient)
        {
            if (NetClient.Instance == null)
            {
                return;
            }

            if (!NetClient.Instance.IsHost)
            {
                SetStatus("Only the room host can change the room.");
                return;
            }

            NetClient.Instance.SendRoomCommand(command, argument);
            return;
        }

        offlineAction?.Invoke();
    }

    private void ToggleBots()
    {
        bool wanted = !BotsCurrentlyOn();
        Send(RoomCommand.SetBotsEnabled, wanted ? 1 : 0, () =>
        {
            MatchManager match = MatchManager.Instance;
            if (match == null)
            {
                return;
            }

            MatchSettings.BotsEnabled = wanted;
            match.RefreshBots(MatchSettings.TeamSize, wanted);
        });

        NetUi.SetButtonText(botsButton, wanted ? "BOTS: ON" : "BOTS: OFF");
    }

    private bool BotsCurrentlyOn()
    {
        if (NetContext.IsClient && NetClient.Instance != null)
        {
            return NetClient.Instance.BotsEnabled;
        }

        return MatchSettings.BotsEnabled;
    }

    private void ChangeTeamSize(int delta)
    {
        int current = NetContext.IsClient && NetClient.Instance != null
            ? NetClient.Instance.TeamSize
            : MatchSettings.TeamSize;

        int wanted = Mathf.Clamp(current + delta, 1, 8);

        Send(RoomCommand.SetTeamSize, wanted, () =>
        {
            MatchSettings.TeamSize = wanted;
            MatchManager.Instance?.RefreshBots(wanted, MatchSettings.BotsEnabled);
        });
    }

    private void SwitchMode(GameMode mode)
    {
        if (NetContext.IsClient)
        {
            Send(RoomCommand.SetMode, (int)mode, null);
            return;
        }

        SceneFlow.StartMatch(mode);
    }

    private void TogglePause()
    {
        bool wanted = Mathf.Approximately(Time.timeScale, 1f);

        Send(RoomCommand.SetPaused, wanted ? 1 : 0, () => Time.timeScale = wanted ? 0f : 1f);
    }

    private static void HealAllLocally()
    {
        foreach (Health health in CombatantRegistry.All)
        {
            if (health != null && health.IsAlive)
            {
                health.RestorePools(health.MaxHealth, health.MaxArmor);
            }
        }

        MatchManager.Instance?.Player?.Weapons?.RefillAll();
    }

    private void LeaveMatch()
    {
        NetClient.Instance?.Leave();
        BackendSocket.Instance.SetPresence("menu");
        SceneFlow.GoToMainMenu();
    }

    private void RefreshStatus()
    {
        if (statusLabel == null)
        {
            return;
        }

        System.Text.StringBuilder text = new System.Text.StringBuilder();

        MatchManager match = MatchManager.Instance;
        if (match != null)
        {
            text.Append($"phase {match.CurrentPhase}  round {match.RoundNumber}\n");
            text.Append($"score {match.AttackerScore}-{match.DefenderScore}  bots {match.Bots.Count}\n");
        }

        if (NetContext.IsClient && NetClient.Instance != null)
        {
            NetClient client = NetClient.Instance;
            text.Append($"online  {(client.IsHost ? "host" : "guest")}\n");
            text.Append($"ping {client.Ping * 1000f:0} ms  drift {client.PredictionError:0.00} m\n");
            text.Append($"players {client.Roster.Count}  team size {client.TeamSize}");

            if (!string.IsNullOrEmpty(client.DisconnectReason))
            {
                text.Append($"\n<color=#ff6060>{client.DisconnectReason}</color>");
            }
        }
        else if (NetContext.IsServer)
        {
            text.Append("dedicated server");
        }
        else
        {
            text.Append("offline practice");
        }

        statusLabel.text = text.ToString();

        NetUi.SetButtonText(botsButton, BotsCurrentlyOn() ? "BOTS: ON" : "BOTS: OFF");
    }

    private void SetStatus(string message)
    {
        if (statusLabel != null)
        {
            statusLabel.text = message;
        }
    }
}
