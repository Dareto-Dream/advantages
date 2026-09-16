using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class ScoreboardOverlay : MonoBehaviour
{
    private class Row
    {
        public GameObject go;
        public TextMeshProUGUI left;
        public TextMeshProUGUI right;
    }

    private class TeamBlock
    {
        public TextMeshProUGUI title;
        public TextMeshProUGUI totals;
        public TextMeshProUGUI header;
        public readonly List<Row> rows = new List<Row>();
    }

    private const int MaxRowsPerTeam = 4;
    private const string Mono = "<mspace=0.62em>";

    private static readonly Color Ink = new Color(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color Muted = new Color(0.62f, 0.67f, 0.75f, 1f);
    private static readonly Color SelfRow = new Color(0.24f, 0.85f, 0.72f, 1f);

    private GameObject root;
    private TextMeshProUGUI headline;
    private readonly List<TeamBlock> blocks = new List<TeamBlock>(2);
    private readonly List<CombatantStats> scratch = new List<CombatantStats>();

    private void Start()
    {
        BuildUi();
        root.SetActive(false);
    }

    private void OnDestroy()
    {
        if (root != null)
        {
            Destroy(root);
        }
    }

    private void Update()
    {
        bool show = WantsShow();
        if (root.activeSelf != show)
        {
            root.SetActive(show);
        }

        if (show)
        {
            Refresh();
        }
    }

    private static bool WantsShow()
    {
        MatchManager match = MatchManager.Instance;
        if (match == null || match.CurrentPhase == MatchManager.Phase.HeroSelect)
        {
            return false;
        }

        Keyboard keyboard = Keyboard.current;
        if (keyboard != null && keyboard.tabKey.isPressed)
        {
            return true;
        }

        Gamepad pad = Gamepad.current;
        return pad != null && pad.selectButton.isPressed;
    }

    private void Refresh()
    {
        MatchManager match = MatchManager.Instance;
        MatchStats stats = MatchStats.Instance;
        if (match == null || stats == null)
        {
            return;
        }

        Team playerTeam = MatchSettings.PlayerTeam;
        Team enemyTeam = MatchSettings.EnemyTeam;

        headline.text = $"{TeamName(Team.Attackers)}  <b>{match.AttackerScore}</b>   —   <b>{match.DefenderScore}</b>  {TeamName(Team.Defenders)}"
            + $"    <size=70%><color=#9AA3AF>ROUND {match.RoundNumber} · {PhaseLabel(match.CurrentPhase)}</color></size>";

        FillBlock(blocks[0], playerTeam, stats, true);
        FillBlock(blocks[1], enemyTeam, stats, false);
    }

    private void FillBlock(TeamBlock block, Team team, MatchStats stats, bool isPlayerTeam)
    {
        Color teamColor = TeamColor(team);
        block.title.text = $"<color=#{Hex(teamColor)}>{TeamName(team)}</color>{(isPlayerTeam ? "  <size=65%><color=#9AA3AF>YOUR TEAM</color></size>" : string.Empty)}";
        block.header.text = $"{Mono}{"K",2} {"D",2} {"A",2}   {"KO",3}   {"DMG",6} {"BLK",6} {"HEAL",6}   {"FH",3}</mspace>";

        scratch.Clear();
        foreach (CombatantStats s in stats.All)
        {
            if (s.Team == team)
            {
                scratch.Add(s);
            }
        }

        Health localHealth = MatchManager.Instance != null && MatchManager.Instance.Player != null
            ? MatchManager.Instance.Player.Health
            : null;

        scratch.Sort((a, b) =>
        {
            if (a.health == localHealth != (b.health == localHealth))
            {
                return a.health == localHealth ? -1 : 1;
            }

            if (a.kills != b.kills)
            {
                return b.kills.CompareTo(a.kills);
            }

            return b.damageDealt.CompareTo(a.damageDealt);
        });

        int k = 0, d = 0, asst = 0, ko = 0;
        float dmg = 0f, blk = 0f, heal = 0f;

        for (int i = 0; i < block.rows.Count; i++)
        {
            Row row = block.rows[i];
            if (i >= scratch.Count)
            {
                row.go.SetActive(false);
                continue;
            }

            CombatantStats s = scratch[i];
            row.go.SetActive(true);

            bool isLocal = s.health == localHealth;
            Color nameColor = isLocal ? SelfRow : (s.IsAlive ? Ink : Muted);
            row.left.color = nameColor;
            row.right.color = s.IsAlive ? Ink : Muted;
            row.left.text = $"{s.Name}   <size=72%><color=#9AA3AF>{s.RoleLabel}</color></size>{(s.IsAlive ? string.Empty : "  <size=60%><color=#9AA3AF>DOWN</color></size>")}";
            row.right.text = FormatStats(s);

            k += s.kills;
            d += s.deaths;
            asst += s.assists;
            ko += s.knockouts;
            dmg += s.damageDealt;
            blk += s.damageBlocked;
            heal += s.healingDone;
        }

        block.totals.text = $"<color=#9AA3AF>KDA</color> {k}/{d}/{asst}   <color=#9AA3AF>KO</color> {ko}   "
            + $"<color=#9AA3AF>DMG</color> {Ri(dmg)}   <color=#9AA3AF>BLK</color> {Ri(blk)}   <color=#9AA3AF>HEAL</color> {Ri(heal)}";
    }

    private static string FormatStats(CombatantStats s)
    {
        return $"{Mono}{s.kills,2} {s.deaths,2} {s.assists,2}   {s.knockouts,3}   "
            + $"{Ri(s.damageDealt),6} {Ri(s.damageBlocked),6} {Ri(s.healingDone),6}   {s.finalHits,3}</mspace>";
    }

    private static int Ri(float value) => Mathf.RoundToInt(value);

    private void BuildUi()
    {
        GameObject canvasObject = new GameObject("ScoreboardCanvas");
        canvasObject.transform.SetParent(transform, false);

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 80;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        root = new GameObject("Root", typeof(RectTransform));
        root.transform.SetParent(canvasObject.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image dim = root.AddComponent<Image>();
        dim.color = new Color(0.02f, 0.03f, 0.05f, 0.72f);
        dim.raycastTarget = false;

        GameObject panel = new GameObject("Panel", typeof(RectTransform));
        panel.transform.SetParent(root.transform, false);
        RectTransform panelRect = panel.GetComponent<RectTransform>();
        panelRect.anchorMin = new Vector2(0.5f, 0.5f);
        panelRect.anchorMax = new Vector2(0.5f, 0.5f);
        panelRect.pivot = new Vector2(0.5f, 0.5f);
        panelRect.sizeDelta = new Vector2(1040f, 600f);

        Image panelBg = panel.AddComponent<Image>();
        panelBg.color = new Color(0.08f, 0.09f, 0.12f, 0.96f);
        panelBg.raycastTarget = false;

        headline = MakeText(panel.transform, "Headline", new Vector2(0f, -14f), new Vector2(1000f, 40f), 26f, TextAlignmentOptions.Center, new Vector2(0.5f, 1f));

        blocks.Add(BuildTeamBlock(panel.transform, -74f));
        blocks.Add(BuildTeamBlock(panel.transform, -320f));
    }

    private TeamBlock BuildTeamBlock(Transform parent, float top)
    {
        TeamBlock block = new TeamBlock();

        block.title = MakeText(parent, "TeamTitle", new Vector2(28f, top), new Vector2(500f, 26f), 20f, TextAlignmentOptions.Left, new Vector2(0f, 1f));
        block.totals = MakeText(parent, "TeamTotals", new Vector2(-28f, top - 2f), new Vector2(660f, 24f), 15f, TextAlignmentOptions.Right, new Vector2(1f, 1f));

        block.header = MakeText(parent, "ColHeader", new Vector2(-28f, top - 30f), new Vector2(470f, 20f), 15f, TextAlignmentOptions.Right, new Vector2(1f, 1f));
        block.header.color = Muted;
        MakeText(parent, "NameHeader", new Vector2(28f, top - 30f), new Vector2(300f, 20f), 15f, TextAlignmentOptions.Left, new Vector2(0f, 1f)).text =
            "<color=#9AA3AF>OPERATIVE</color>";

        for (int i = 0; i < MaxRowsPerTeam; i++)
        {
            float y = top - 54f - i * 40f;

            GameObject rowGo = new GameObject($"Row{i}", typeof(RectTransform));
            rowGo.transform.SetParent(parent, false);
            RectTransform rowRect = rowGo.GetComponent<RectTransform>();
            rowRect.anchorMin = new Vector2(0f, 1f);
            rowRect.anchorMax = new Vector2(1f, 1f);
            rowRect.pivot = new Vector2(0.5f, 1f);
            rowRect.offsetMin = new Vector2(0f, y - 34f);
            rowRect.offsetMax = new Vector2(0f, y);

            Image stripe = rowGo.AddComponent<Image>();
            stripe.color = new Color(1f, 1f, 1f, i % 2 == 0 ? 0.03f : 0f);
            stripe.raycastTarget = false;

            Row row = new Row
            {
                go = rowGo,
                left = MakeText(rowGo.transform, "Left", new Vector2(28f, 0f), new Vector2(430f, 32f), 17f, TextAlignmentOptions.Left, new Vector2(0f, 0.5f)),
                right = MakeText(rowGo.transform, "Right", new Vector2(-28f, 0f), new Vector2(470f, 32f), 17f, TextAlignmentOptions.Right, new Vector2(1f, 0.5f))
            };

            block.rows.Add(row);
        }

        return block;
    }

    private static TextMeshProUGUI MakeText(Transform parent, string name, Vector2 anchoredPosition, Vector2 size, float fontSize, TextAlignmentOptions alignment, Vector2 anchor)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.fontSize = fontSize;
        text.alignment = alignment;
        text.color = Ink;
        text.raycastTarget = false;
        text.richText = true;
        text.textWrappingMode = TextWrappingModes.NoWrap;
        text.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = anchor;
        rect.anchorMax = anchor;
        rect.pivot = anchor;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
        return text;
    }

    private static Color TeamColor(Team team) => TeamPalette.For(team);

    private static string TeamName(Team team) => team.DisplayName().ToUpperInvariant();

    private static string Hex(Color color) => ColorUtility.ToHtmlStringRGB(color);

    private static string PhaseLabel(MatchManager.Phase phase)
    {
        switch (phase)
        {
            case MatchManager.Phase.Staging: return "SPAWN ROOM";
            case MatchManager.Phase.RoundOver: return "ROUND OVER";
            case MatchManager.Phase.MatchOver: return "MATCH OVER";
            default: return phase.ToString().ToUpperInvariant();
        }
    }
}
