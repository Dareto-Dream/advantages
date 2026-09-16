using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class ObjectiveHudOverlay : MonoBehaviour
{
    private const int BreachSegments = 4;

    private static readonly Color Ink = new Color(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color Faint = new Color(0.86f, 0.91f, 0.97f, 0.72f);
    private static readonly Color Track = new Color(1f, 1f, 1f, 0.16f);
    private static readonly Color Well = new Color(0.02f, 0.03f, 0.05f, 0.78f);
    private static readonly Color Overtime = new Color(1f, 0.82f, 0.22f, 1f);

    private MatchManager match;
    private ModeObjective objective;

    private RectTransform widgetRoot;

    private GameObject clockRoot;
    private TextMeshProUGUI clockText;
    private TextMeshProUGUI friendlyScoreText;
    private TextMeshProUGUI enemyScoreText;

    private GameObject overtimeRoot;
    private Image overtimeFill;

    private GameObject convergenceRoot;
    private Image convFriendlyFill;
    private Image convEnemyFill;
    private Image convCentre;
    private TextMeshProUGUI convFriendlyPct;
    private TextMeshProUGUI convEnemyPct;

    private GameObject extractionRoot;
    private RectTransform extractionFill;
    private Image extractionFillImage;
    private RectTransform extractionChevrons;
    private Image extractionChevronImage;

    private GameObject breachRoot;
    private readonly Image[] breachFills = new Image[BreachSegments];
    private readonly Image[] breachTracks = new Image[BreachSegments];

    private GameObject dominionRoot;
    private readonly Image[] dominionRings = new Image[3];
    private readonly Image[] dominionCores = new Image[3];
    private RectTransform domFriendlyFill;
    private Image domFriendlyImage;
    private RectTransform domEnemyFill;
    private Image domEnemyImage;

    private void Start()
    {
        match = MatchManager.Instance;
        objective = match != null ? match.Objective : FindAnyObjectByType<ModeObjective>();

        BuildUi();
        ShowOnly(null);
    }

    private void Update()
    {
        bool visible = !HudBuild.HudSuppressed;
        widgetRoot.gameObject.SetActive(visible);
        clockRoot.SetActive(visible);
        if (!visible)
        {
            return;
        }

        UpdateClock();

        if (objective == null)
        {
            ShowOnly(null);
            overtimeRoot.SetActive(false);
            return;
        }

        switch (objective.Mode)
        {
            case GameMode.Convergence: ShowOnly(convergenceRoot); UpdateConvergence(); break;
            case GameMode.Extraction: ShowOnly(extractionRoot); UpdateExtraction(); break;
            case GameMode.Breach: ShowOnly(breachRoot); UpdateBreach(); break;
            case GameMode.Dominion: ShowOnly(dominionRoot); UpdateDominion(); break;
            default: ShowOnly(null); break;
        }

        UpdateOvertime();
    }

    private void ShowOnly(GameObject active)
    {
        if (convergenceRoot != null) convergenceRoot.SetActive(active == convergenceRoot);
        if (extractionRoot != null) extractionRoot.SetActive(active == extractionRoot);
        if (breachRoot != null) breachRoot.SetActive(active == breachRoot);
        if (dominionRoot != null) dominionRoot.SetActive(active == dominionRoot);
    }

    private void UpdateClock()
    {
        if (match == null)
        {
            return;
        }

        bool running = match.CurrentPhase == MatchManager.Phase.Live
                       || match.CurrentPhase == MatchManager.Phase.Overtime
                       || match.CurrentPhase == MatchManager.Phase.Staging;

        if (!running || !match.PhaseIsTimed)
        {
            clockText.text = string.Empty;
        }
        else
        {
            float seconds = Mathf.Max(0f, match.PhaseSecondsRemaining);
            clockText.text = $"{Mathf.FloorToInt(seconds / 60f)}:{Mathf.FloorToInt(seconds % 60f):00}";
        }

        bool friendlyIsAttacker = MatchSettings.PlayerTeam == Team.Attackers;
        friendlyScoreText.text = (friendlyIsAttacker ? match.AttackerScore : match.DefenderScore).ToString();
        enemyScoreText.text = (friendlyIsAttacker ? match.DefenderScore : match.AttackerScore).ToString();
    }

    private void UpdateConvergence()
    {
        if (!(objective is ConvergenceObjective conv))
        {
            return;
        }

        Team friendly = MatchSettings.PlayerTeam;
        float friendlyProgress = friendly == Team.Attackers ? conv.AttackerProgress : conv.DefenderProgress;
        float enemyProgress = friendly == Team.Attackers ? conv.DefenderProgress : conv.AttackerProgress;

        convFriendlyFill.fillAmount = friendlyProgress / 100f;
        convEnemyFill.fillAmount = enemyProgress / 100f;
        convFriendlyPct.text = $"{friendlyProgress:0}%";
        convEnemyPct.text = $"{enemyProgress:0}%";

        convCentre.color = Mathf.Abs(friendlyProgress - enemyProgress) < 0.5f
            ? TeamPalette.Neutral
            : TeamPalette.For(friendlyProgress > enemyProgress ? friendly : MatchSettings.EnemyTeam);
    }

    private void UpdateExtraction()
    {
        if (!(objective is ExtractionObjective ext))
        {
            return;
        }

        float offset01 = Mathf.Clamp(ext.Position / 100f, -1f, 1f);
        Team leader = ext.Position < 0f ? Team.Attackers : ext.Position > 0f ? Team.Defenders : Team.None;
        Color color = TeamPalette.For(leader);

        const float halfWidth = 320f;
        float x = offset01 * halfWidth;

        extractionFill.anchoredPosition = new Vector2(x * 0.5f, -26f);
        extractionFill.sizeDelta = new Vector2(Mathf.Abs(x), 24f);
        extractionFillImage.color = color;

        bool moving = Mathf.Abs(x) > 1f;
        extractionChevrons.gameObject.SetActive(moving);
        if (moving)
        {
            extractionChevrons.anchoredPosition = new Vector2(x + Mathf.Sign(x) * 26f, -26f);
            extractionChevrons.localScale = new Vector3(Mathf.Sign(x), 1f, 1f);
            extractionChevronImage.color = color;
        }
    }

    private void UpdateBreach()
    {
        if (!(objective is BreachObjective breach))
        {
            return;
        }

        Color captureColor = TeamPalette.For(Team.Attackers);
        float capture = breach.Capture;

        for (int i = 0; i < BreachSegments; i++)
        {

            float share = Mathf.Clamp01((capture - i * 25f) / 25f);
            breachFills[i].fillAmount = share * SegmentSweep;
            breachFills[i].color = breach.CheckpointsReached > i ? Overtime : captureColor;
            breachTracks[i].color = Track;
        }
    }

    private void UpdateDominion()
    {
        if (!(objective is DominionObjective dom))
        {
            return;
        }

        System.Collections.Generic.IReadOnlyList<DominionTerminal> terminals = dom.Terminals;

        for (int slot = 0; slot < dominionRings.Length; slot++)
        {
            DominionTerminal terminal = FindTerminalForSlot(terminals, slot);
            Image ring = dominionRings[slot];
            Image core = dominionCores[slot];

            if (terminal == null)
            {
                ring.fillAmount = 0f;
                core.enabled = false;
                continue;
            }

            bool held = terminal.State == DominionTerminal.TerminalState.Active;
            bool arming = terminal.ArmingTeam != Team.None && terminal.InitProgress01 > 0.001f;

            if (arming)
            {

                ring.fillAmount = terminal.InitProgress01;
                ring.color = TeamPalette.For(terminal.ArmingTeam);
                core.enabled = held;
                core.color = held ? TeamPalette.For(terminal.OwningTeam) : Track;
            }
            else if (held)
            {
                ring.fillAmount = 1f;
                ring.color = TeamPalette.For(terminal.OwningTeam);
                core.enabled = true;
                core.color = TeamPalette.For(terminal.OwningTeam);
            }
            else
            {
                ring.fillAmount = 0f;
                core.enabled = false;
            }
        }

        float target = Mathf.Max(1f, dom.DataTarget);
        bool friendlyIsAttacker = MatchSettings.PlayerTeam == Team.Attackers;
        float friendly = (friendlyIsAttacker ? dom.AttackerPool : dom.DefenderPool) / target;
        float enemy = (friendlyIsAttacker ? dom.DefenderPool : dom.AttackerPool) / target;

        const float half = 208f;
        domFriendlyFill.sizeDelta = new Vector2(Mathf.Clamp01(friendly) * half, 22f);
        domEnemyFill.sizeDelta = new Vector2(Mathf.Clamp01(enemy) * half, 22f);
        domFriendlyImage.color = TeamPalette.Friendly;
        domEnemyImage.color = TeamPalette.Hostile;
    }

    private static DominionTerminal FindTerminalForSlot(System.Collections.Generic.IReadOnlyList<DominionTerminal> terminals, int slot)
    {
        if (terminals == null)
        {
            return null;
        }

        DominionTerminal.Site wanted = (DominionTerminal.Site)slot;
        foreach (DominionTerminal terminal in terminals)
        {
            if (terminal != null && terminal.TerminalSite == wanted)
            {
                return terminal;
            }
        }

        return slot < terminals.Count ? terminals[slot] : null;
    }

    private void UpdateOvertime()
    {
        bool active = false;
        float fill = 0f;

        switch (objective)
        {
            case ConvergenceObjective conv when conv.OvertimeActive:

                active = true;
                fill = conv.OvertimeFraction01;
                break;
            case BreachObjective breach when breach.OvertimeActive:
                active = true;
                fill = breach.OvertimeRemaining / 25f;
                break;
            case DominionObjective dom when dom.OvertimeActive:

                active = true;
                fill = Mathf.PingPong(Time.time * 0.6f, 1f);
                break;
        }

        overtimeRoot.SetActive(active);
        if (active)
        {
            overtimeFill.fillAmount = Mathf.Clamp01(fill);
        }
    }

    private const float SegmentSweep = 0.22f;

    private void BuildUi()
    {
        Canvas canvas = HudBuild.Canvas(transform, "ObjectiveHudCanvas", 50);

        BuildClock(canvas.transform);

        widgetRoot = HudBuild.Rect(canvas.transform, "Widget", new Vector2(0.5f, 1f), new Vector2(0f, -58f), new Vector2(760f, 200f));

        convergenceRoot = BuildConvergence(widgetRoot);
        extractionRoot = BuildExtraction(widgetRoot);
        breachRoot = BuildBreach(widgetRoot);
        dominionRoot = BuildDominion(widgetRoot);
        overtimeRoot = BuildOvertime(widgetRoot);
    }

    private void BuildClock(Transform parent)
    {
        RectTransform row = HudBuild.Rect(parent, "Clock", new Vector2(0.5f, 1f), new Vector2(0f, -10f), new Vector2(420f, 46f));
        clockRoot = row.gameObject;

        clockText = HudBuild.Text(row, "Time", new Vector2(0.5f, 1f), new Vector2(0f, -2f), new Vector2(200f, 42f), 30f, TextAlignmentOptions.Top, Ink);
        clockText.text = "0:00";

        friendlyScoreText = HudBuild.Text(row, "Friendly", new Vector2(0.5f, 1f), new Vector2(-116f, -2f), new Vector2(90f, 42f), 32f, TextAlignmentOptions.Top, TeamPalette.Friendly);
        friendlyScoreText.fontStyle = FontStyles.Bold;
        friendlyScoreText.text = "0";

        enemyScoreText = HudBuild.Text(row, "Enemy", new Vector2(0.5f, 1f), new Vector2(116f, -2f), new Vector2(90f, 42f), 32f, TextAlignmentOptions.Top, TeamPalette.Hostile);
        enemyScoreText.fontStyle = FontStyles.Bold;
        enemyScoreText.text = "0";
    }

    private GameObject BuildConvergence(Transform parent)
    {
        RectTransform root = HudBuild.Rect(parent, "Convergence", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(760f, 120f));

        convFriendlyFill = BuildOutlinedBar(root, "Friendly", new Vector2(-206f, -28f), new Vector2(320f, 30f),
            Image.OriginHorizontal.Left, TeamPalette.Friendly);
        convEnemyFill = BuildOutlinedBar(root, "Enemy", new Vector2(206f, -28f), new Vector2(320f, 30f),
            Image.OriginHorizontal.Right, TeamPalette.Hostile);

        convCentre = HudBuild.Glyph(root, "Relay", HudSymbol.DotRing, new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(46f, 46f), TeamPalette.Neutral);

        convFriendlyPct = HudBuild.Text(root, "FriendlyPct", new Vector2(0.5f, 1f), new Vector2(-206f, -50f), new Vector2(320f, 24f), 16f, TextAlignmentOptions.Top, Faint);
        convEnemyPct = HudBuild.Text(root, "EnemyPct", new Vector2(0.5f, 1f), new Vector2(206f, -50f), new Vector2(320f, 24f), 16f, TextAlignmentOptions.Top, Faint);

        return root.gameObject;
    }

    private GameObject BuildExtraction(Transform parent)
    {
        RectTransform root = HudBuild.Rect(parent, "Extraction", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(760f, 120f));

        HudBuild.Block(root, "Border", new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(668f, 32f), Faint);
        HudBuild.Block(root, "Well", new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(660f, 24f), Well);

        RectTransform fill = HudBuild.Rect(root, "Fill", new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(0f, 24f));
        extractionFill = fill;
        extractionFillImage = fill.gameObject.AddComponent<Image>();
        extractionFillImage.sprite = HudArt.Solid;
        extractionFillImage.raycastTarget = false;

        HudBuild.Block(root, "Notch", new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(4f, 40f), Ink);

        RectTransform chevrons = HudBuild.Rect(root, "Chevrons", new Vector2(0.5f, 1f), new Vector2(0f, -26f), new Vector2(34f, 26f));
        extractionChevrons = chevrons;
        extractionChevronImage = HudBuild.Glyph(chevrons, "Glyph", HudSymbol.DoubleChevron, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(34f, 26f), Ink);
        chevrons.gameObject.SetActive(false);

        return root.gameObject;
    }

    private GameObject BuildBreach(Transform parent)
    {
        RectTransform root = HudBuild.Rect(parent, "Breach", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(760f, 200f));

        Vector2 centre = new Vector2(0f, -90f);
        Vector2 size = new Vector2(158f, 158f);

        Image.Origin360[] origins =
        {
            Image.Origin360.Top,
            Image.Origin360.Right,
            Image.Origin360.Bottom,
            Image.Origin360.Left
        };

        for (int i = 0; i < BreachSegments; i++)
        {
            Image track = HudBuild.Glyph(root, $"SegTrack{i}", HudSymbol.Ring, new Vector2(0.5f, 1f), centre, size, Track);
            HudBuild.AsFill(track, Image.FillMethod.Radial360, (int)origins[i]);
            track.fillClockwise = true;
            track.fillAmount = SegmentSweep;
            breachTracks[i] = track;

            Image fill = HudBuild.Glyph(root, $"SegFill{i}", HudSymbol.Ring, new Vector2(0.5f, 1f), centre, size, TeamPalette.For(Team.Attackers));
            HudBuild.AsFill(fill, Image.FillMethod.Radial360, (int)origins[i]);
            fill.fillClockwise = true;
            fill.fillAmount = 0f;
            breachFills[i] = fill;
        }

        return root.gameObject;
    }

    private GameObject BuildDominion(Transform parent)
    {
        RectTransform root = HudBuild.Rect(parent, "Dominion", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(760f, 180f));

        float[] xs = { -152f, 0f, 152f };
        for (int i = 0; i < 3; i++)
        {
            Vector2 centre = new Vector2(xs[i], -48f);

            HudBuild.Glyph(root, $"SiteTrack{i}", HudSymbol.Ring, new Vector2(0.5f, 1f), centre, new Vector2(84f, 84f), Track);

            Image ring = HudBuild.Glyph(root, $"SiteRing{i}", HudSymbol.Ring, new Vector2(0.5f, 1f), centre, new Vector2(84f, 84f), Track);
            HudBuild.AsFill(ring, Image.FillMethod.Radial360, (int)Image.Origin360.Top);
            ring.fillClockwise = true;
            dominionRings[i] = ring;

            Image core = HudBuild.Glyph(root, $"SiteCore{i}", HudSymbol.Disc, new Vector2(0.5f, 1f), centre, new Vector2(44f, 44f), Track);
            core.enabled = false;
            dominionCores[i] = core;
        }

        HudBuild.Block(root, "TugBorder", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(424f, 30f), Faint);
        HudBuild.Block(root, "TugWell", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(416f, 22f), Well);

        domFriendlyFill = HudBuild.Rect(root, "TugFriendly", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(0f, 22f));
        domFriendlyFill.pivot = new Vector2(1f, 0.5f);
        domFriendlyImage = domFriendlyFill.gameObject.AddComponent<Image>();
        domFriendlyImage.sprite = HudArt.Solid;
        domFriendlyImage.raycastTarget = false;

        domEnemyFill = HudBuild.Rect(root, "TugEnemy", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(0f, 22f));
        domEnemyFill.pivot = new Vector2(0f, 0.5f);
        domEnemyImage = domEnemyFill.gameObject.AddComponent<Image>();
        domEnemyImage.sprite = HudArt.Solid;
        domEnemyImage.raycastTarget = false;

        HudBuild.Block(root, "TugNotch", new Vector2(0.5f, 1f), new Vector2(0f, -120f), new Vector2(4f, 34f), Ink);

        return root.gameObject;
    }

    private GameObject BuildOvertime(Transform parent)
    {
        RectTransform root = HudBuild.Rect(parent, "Overtime", new Vector2(0.5f, 1f), new Vector2(0f, -178f), new Vector2(220f, 22f));

        HudBuild.Block(root, "RuleLeft", new Vector2(0.5f, 1f), new Vector2(-134f, -11f), new Vector2(40f, 5f), Ink);
        HudBuild.Block(root, "RuleRight", new Vector2(0.5f, 1f), new Vector2(134f, -11f), new Vector2(40f, 5f), Ink);

        HudBuild.Block(root, "Well", new Vector2(0.5f, 1f), new Vector2(0f, -11f), new Vector2(220f, 22f), Well);

        RectTransform fillArea = HudBuild.Rect(root, "FillArea", new Vector2(0.5f, 1f), new Vector2(0f, -11f), new Vector2(214f, 16f));
        overtimeFill = fillArea.gameObject.AddComponent<Image>();
        overtimeFill.sprite = HudArt.Solid;
        overtimeFill.color = Overtime;
        overtimeFill.raycastTarget = false;
        HudBuild.AsFill(overtimeFill, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);

        root.gameObject.SetActive(false);
        return root.gameObject;
    }

    private static Image BuildOutlinedBar(Transform parent, string name, Vector2 position, Vector2 size, Image.OriginHorizontal origin, Color color)
    {
        RectTransform holder = HudBuild.Rect(parent, name, new Vector2(0.5f, 1f), position, size);

        HudBuild.Block(holder, "Border", new Vector2(0.5f, 0.5f), Vector2.zero, size, Faint);
        HudBuild.Block(holder, "Well", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(6f, 6f), Well);

        Image fill = HudBuild.Block(holder, "Fill", new Vector2(0.5f, 0.5f), Vector2.zero, size - new Vector2(10f, 10f), color);
        HudBuild.AsFill(fill, Image.FillMethod.Horizontal, (int)origin);
        return fill;
    }
}
