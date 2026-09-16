using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AllyRosterOverlay : MonoBehaviour
{
    private const int MaxRows = 3;
    private const float RowPitch = 116f;
    private const float TileSize = 104f;
    private const float PlateWidth = 210f;

    private class Row
    {
        public GameObject go;
        public Image tile;
        public Image figure;
        public Image plateFill;
        public Image armorPip;
        public TextMeshProUGUI name;
    }

    private RectTransform panel;
    private readonly List<Row> rows = new List<Row>(MaxRows);
    private readonly List<Health> scratch = new List<Health>(8);

    private static readonly Color Ink = new Color(0.95f, 0.97f, 1f, 1f);
    private static readonly Color Down = new Color(0.55f, 0.58f, 0.63f, 1f);
    private static readonly Color Track = new Color(1f, 1f, 1f, 0.14f);
    private static readonly Color Well = new Color(0f, 0f, 0f, 0.5f);
    private static readonly Color HealthGood = new Color(0.30f, 0.82f, 0.55f, 0.85f);
    private static readonly Color HealthLow = new Color(0.92f, 0.32f, 0.32f, 0.85f);
    private static readonly Color Armor = new Color(0.72f, 0.80f, 0.92f, 1f);

    private void Start()
    {
        BuildUi();
        panel.gameObject.SetActive(false);
    }

    private void Update()
    {
        if (HudBuild.HudSuppressed || !LocalPlayerIsSupport())
        {
            if (panel.gameObject.activeSelf)
            {
                panel.gameObject.SetActive(false);
            }

            return;
        }

        panel.gameObject.SetActive(true);
        Refresh();
    }

    private static bool LocalPlayerIsSupport()
    {
        MatchManager match = MatchManager.Instance;
        PlayerController player = match != null ? match.Player : null;
        OperativeController op = player != null ? player.Operative : null;
        return op != null && op.Abilities.Count > 0 && op.Definition.role == HeroRole.Support;
    }

    private void Refresh()
    {
        MatchManager match = MatchManager.Instance;
        Health local = match != null && match.Player != null ? match.Player.Health : null;

        scratch.Clear();
        foreach (Health h in CombatantRegistry.All)
        {
            if (h == null || h == local || h.IsDecoy || h.Team != MatchSettings.PlayerTeam)
            {
                continue;
            }

            scratch.Add(h);
            if (scratch.Count >= MaxRows)
            {
                break;
            }
        }

        for (int i = 0; i < rows.Count; i++)
        {
            Row row = rows[i];
            if (i >= scratch.Count)
            {
                row.go.SetActive(false);
                continue;
            }

            Health h = scratch[i];
            row.go.SetActive(true);

            bool alive = h.IsAlive;
            Color operative = OperativeColor(h);

            row.name.text = h.DisplayName.ToUpperInvariant();
            row.name.color = alive ? Ink : Down;

            row.tile.color = alive ? operative : Down;
            row.figure.color = alive ? operative : Down;

            float frac = Mathf.Clamp01(h.HealthFraction);
            row.plateFill.fillAmount = alive ? frac : 0f;
            row.plateFill.color = Color.Lerp(HealthLow, HealthGood, frac);

            bool hasArmor = alive && h.MaxArmor > 0f && h.ArmorFraction > 0.01f;
            row.armorPip.enabled = hasArmor;
            if (hasArmor)
            {
                row.armorPip.fillAmount = Mathf.Clamp01(h.ArmorFraction);
            }
        }
    }

    private static Color OperativeColor(Health h)
    {
        OperativeController op = h.GetComponent<OperativeController>();
        if (op != null && op.Abilities.Count > 0)
        {
            return op.Definition.color;
        }

        BotOperative botOp = h.GetComponent<BotOperative>();
        if (botOp != null)
        {
            return OperativeRoster.Get(botOp.Id).color;
        }

        return Track;
    }

    private void BuildUi()
    {
        Canvas canvas = HudBuild.Canvas(transform, "AllyRosterCanvas", 50);

        panel = HudBuild.Rect(canvas.transform, "Panel", new Vector2(0f, 1f), new Vector2(30f, -28f),
            new Vector2(TileSize + PlateWidth + 14f, MaxRows * RowPitch));

        for (int i = 0; i < MaxRows; i++)
        {
            rows.Add(BuildRow(i));
        }
    }

    private Row BuildRow(int index)
    {
        RectTransform row = HudBuild.Rect(panel, $"Row{index}", new Vector2(0f, 1f), new Vector2(0f, -index * RowPitch),
            new Vector2(TileSize + PlateWidth + 14f, TileSize));

        RectTransform tileRoot = HudBuild.Rect(row, "Tile", new Vector2(0f, 1f), Vector2.zero, new Vector2(TileSize, TileSize));
        HudBuild.Block(tileRoot, "Well", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TileSize, TileSize), Well);
        Image tile = HudBuild.Glyph(tileRoot, "Frame", HudSymbol.SquareOutline, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(TileSize, TileSize), Ink);
        Image figure = HudBuild.Glyph(tileRoot, "Figure", HudSymbol.Person, new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(56f, 56f), Ink);

        RectTransform plate = HudBuild.Rect(row, "Plate", new Vector2(0f, 1f), new Vector2(TileSize + 14f, -14f), new Vector2(PlateWidth, 56f));
        HudBuild.Block(plate, "Border", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PlateWidth, 56f), Track);
        HudBuild.Block(plate, "Well", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(PlateWidth - 4f, 52f), Well);

        Image plateFill = HudBuild.Block(plate, "Fill", new Vector2(0f, 0.5f), new Vector2(2f, 0f), new Vector2(PlateWidth - 4f, 52f), HealthGood);
        plateFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        HudBuild.AsFill(plateFill, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);
        plateFill.fillAmount = 1f;

        Image armorPip = HudBuild.Block(plate, "Armor", new Vector2(0f, 1f), new Vector2(2f, -2f), new Vector2(PlateWidth - 4f, 6f), Armor);
        armorPip.rectTransform.pivot = new Vector2(0f, 1f);
        HudBuild.AsFill(armorPip, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);
        armorPip.fillAmount = 1f;
        armorPip.enabled = false;

        TextMeshProUGUI name = HudBuild.Text(plate, "Name", new Vector2(0f, 0.5f), new Vector2(12f, 0f), new Vector2(PlateWidth - 20f, 40f),
            20f, TextAlignmentOptions.Left, Ink);
        name.fontStyle = FontStyles.Bold;

        return new Row { go = row.gameObject, tile = tile, figure = figure, plateFill = plateFill, armorPip = armorPip, name = name };
    }
}
