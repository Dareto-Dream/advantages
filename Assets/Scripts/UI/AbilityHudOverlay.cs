using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class AbilityHudOverlay : MonoBehaviour
{
    private class Icon
    {
        public Image track;
        public Image fill;
        public TextMeshProUGUI key;
        public Image[] pips;
        public HudSymbol symbol;
    }

    private const int CellWidth = 104;
    private const int CellGap = 8;
    private const int CellHeight = 128;
    private const int UltSegments = 8;
    private const int MaxPips = 3;

    private static readonly Color Ready = new Color(0.24f, 0.85f, 0.72f, 1f);
    private static readonly Color Track = new Color(0.62f, 0.68f, 0.78f, 0.28f);
    private static readonly Color Charging = new Color(0.46f, 0.52f, 0.62f, 0.75f);
    private static readonly Color ActiveColor = new Color(1f, 0.82f, 0.30f, 1f);
    private static readonly Color KeyInk = new Color(0.88f, 0.92f, 0.97f, 0.92f);
    private static readonly Color FuelColor = new Color(0.93f, 0.24f, 0.20f, 0.95f);
    private static readonly Color FuelTrack = new Color(1f, 1f, 1f, 0.20f);

    private OperativeController operative;
    private OperativeId builtId;
    private bool built;

    private static readonly HudSymbol[] SpareSymbols =
    {
        HudSymbol.Concentric, HudSymbol.Hex, HudSymbol.SquareOutline, HudSymbol.Triangle,
        HudSymbol.Chevron, HudSymbol.Burst, HudSymbol.SlashRing, HudSymbol.Ring
    };

    private Transform row;
    private readonly List<Icon> icons = new List<Icon>();
    private readonly HashSet<HudSymbol> usedSymbols = new HashSet<HudSymbol>();

    private readonly Image[] ultSegments = new Image[UltSegments];
    private RectTransform ultStack;

    private GameObject fuelRoot;
    private Image fuelFill;
    private GameObject canvasRoot;

    private void Start()
    {
        BuildCanvas();
    }

    private void Update()
    {
        bool visible = !HudBuild.HudSuppressed;
        if (canvasRoot != null && canvasRoot.activeSelf != visible)
        {
            canvasRoot.SetActive(visible);
        }

        if (!visible)
        {
            return;
        }

        if (operative == null)
        {
            MatchManager match = MatchManager.Instance;
            if (match != null && match.Player != null)
            {
                operative = match.Player.GetComponent<OperativeController>();
            }

            if (operative == null)
            {
                return;
            }
        }

        IReadOnlyList<Ability> abilities = operative.Abilities;

        if (!built || builtId != operative.Id || icons.Count != abilities.Count)
        {
            RebuildIcons(abilities);
            builtId = operative.Id;
            built = true;
        }

        for (int i = 0; i < icons.Count && i < abilities.Count; i++)
        {
            UpdateIcon(icons[i], abilities[i], operative);
        }

        UpdateUltStack(abilities);
        UpdateFuel(abilities);
    }

    private void UpdateIcon(Icon icon, Ability ability, OperativeController op)
    {
        if (icon.fill == null || ability == null)
        {
            return;
        }

        if (ability.IsUltimate)
        {
            float charge = op.UltCharge01;
            icon.fill.fillAmount = charge;
            icon.fill.color = charge >= 1f ? ActiveColor : Charging;
        }
        else if (ability.IsActive)
        {
            icon.fill.fillAmount = 1f;
            icon.fill.color = ActiveColor;
        }
        else if (ability.UsesFuel)
        {
            icon.fill.fillAmount = ability.FuelFraction01;
            icon.fill.color = ability.FuelReady ? Ready : Charging;
        }
        else if (ability.OnCooldown)
        {
            icon.fill.fillAmount = 1f - ability.CooldownFraction01;
            icon.fill.color = Charging;
        }
        else
        {
            icon.fill.fillAmount = 1f;
            icon.fill.color = Ready;
        }

        if (icon.pips != null)
        {
            int max = Mathf.Min(ability.MaxCharges, MaxPips);
            for (int p = 0; p < icon.pips.Length; p++)
            {
                bool used = p < max;
                icon.pips[p].enabled = used;
                if (used)
                {
                    icon.pips[p].color = p < ability.Charges ? Ready : Track;
                }
            }
        }
    }

    private void UpdateUltStack(IReadOnlyList<Ability> abilities)
    {
        if (ultStack == null)
        {
            return;
        }

        bool hasUlt = false;
        for (int i = 0; i < abilities.Count; i++)
        {
            if (abilities[i] != null && abilities[i].IsUltimate)
            {
                hasUlt = true;
                break;
            }
        }

        if (ultStack.gameObject.activeSelf != hasUlt)
        {
            ultStack.gameObject.SetActive(hasUlt);
        }

        if (!hasUlt)
        {
            return;
        }

        float charge = operative.UltCharge01;
        float lit = charge * UltSegments;

        for (int i = 0; i < ultSegments.Length; i++)
        {

            bool on = lit >= i + 1;
            bool partial = !on && lit > i;
            ultSegments[i].color = on
                ? (charge >= 1f ? ActiveColor : Ready)
                : partial ? Charging : Track;
        }
    }

    private void UpdateFuel(IReadOnlyList<Ability> abilities)
    {
        if (fuelRoot == null)
        {
            return;
        }

        Ability fuelAbility = null;
        for (int i = 0; i < abilities.Count; i++)
        {
            if (abilities[i] != null && abilities[i].UsesFuel)
            {
                fuelAbility = abilities[i];
                break;
            }
        }

        bool show = fuelAbility != null;
        if (fuelRoot.activeSelf != show)
        {
            fuelRoot.SetActive(show);
        }

        if (!show)
        {
            return;
        }

        fuelFill.fillAmount = fuelAbility.FuelFraction01;
        fuelFill.color = fuelAbility.FuelReady
            ? FuelColor
            : new Color(FuelColor.r * 0.55f, FuelColor.g * 0.45f, FuelColor.b * 0.45f, 0.8f);
    }

    private void RebuildIcons(IReadOnlyList<Ability> abilities)
    {
        foreach (Icon icon in icons)
        {
            if (icon.track != null)
            {
                Destroy(icon.track.transform.parent.gameObject);
            }
        }

        icons.Clear();
        usedSymbols.Clear();

        int count = abilities.Count;
        for (int i = 0; i < count; i++)
        {

            float x = -(count - 1 - i) * (CellWidth + CellGap);
            icons.Add(BuildIcon(abilities[i], i, new Vector2(x, 0f)));
        }

        if (ultStack != null)
        {
            ultStack.anchoredPosition = new Vector2(-CellWidth * 0.5f, CellHeight + 8f);
            ultStack.SetAsLastSibling();
        }
    }

    private Icon BuildIcon(Ability ability, int index, Vector2 position)
    {
        RectTransform cell = HudBuild.Rect(row, $"Ability{index}", new Vector2(1f, 0f), position, new Vector2(CellWidth, CellHeight));

        HudSymbol symbol = DistinctSymbol(ability);
        Vector2 glyphSize = new Vector2(66f, 66f);

        Image track = HudBuild.Glyph(cell, "Track", symbol, new Vector2(0.5f, 1f), new Vector2(0f, -6f), glyphSize, Track);
        Image fill = HudBuild.Glyph(cell, "Fill", symbol, new Vector2(0.5f, 1f), new Vector2(0f, -6f), glyphSize, Ready);
        HudBuild.AsFill(fill, Image.FillMethod.Radial360, (int)Image.Origin360.Top);
        fill.fillClockwise = true;
        fill.fillAmount = 1f;

        Image[] pips = new Image[MaxPips];
        for (int p = 0; p < MaxPips; p++)
        {
            float px = (p - (MaxPips - 1) * 0.5f) * 13f;
            pips[p] = HudBuild.Glyph(cell, $"Pip{p}", HudSymbol.Disc, new Vector2(0.5f, 0f), new Vector2(px, 48f), new Vector2(8f, 8f), Track);
            pips[p].enabled = false;
        }

        HudBuild.Block(cell, "Rule", new Vector2(0.5f, 0f), new Vector2(0f, 38f), new Vector2(72f, 3f), new Color(0.88f, 0.92f, 0.97f, 0.7f));

        string label = string.IsNullOrEmpty(ability.KeyLabel) ? "?" : ability.KeyLabel.ToUpperInvariant();
        TextMeshProUGUI key = HudBuild.Text(cell, "Key", new Vector2(0.5f, 0f), new Vector2(0f, 4f), new Vector2(CellWidth, 30f),
            label.Length > 1 ? 16f : 24f, TextAlignmentOptions.Center, KeyInk);
        key.text = label;
        key.fontStyle = FontStyles.Bold;

        return new Icon { track = track, fill = fill, key = key, pips = pips, symbol = symbol };
    }

    private HudSymbol DistinctSymbol(Ability ability)
    {
        HudSymbol symbol = HudArt.ForAbility(ability);

        if (usedSymbols.Contains(symbol))
        {
            foreach (HudSymbol spare in SpareSymbols)
            {
                if (!usedSymbols.Contains(spare))
                {
                    symbol = spare;
                    break;
                }
            }
        }

        usedSymbols.Add(symbol);
        return symbol;
    }

    private void BuildCanvas()
    {
        Canvas canvas = HudBuild.Canvas(transform, "AbilityHudCanvas", 50);
        canvasRoot = canvas.gameObject;

        row = HudBuild.Rect(canvas.transform, "AbilityRow", new Vector2(1f, 0f), new Vector2(-32f, 34f),
            new Vector2(CellWidth * 4 + CellGap * 3, CellHeight));

        BuildUltStack();
        BuildFuelArc(canvas.transform);
    }

    private void BuildUltStack()
    {
        ultStack = HudBuild.Rect(row, "UltStack", new Vector2(1f, 0f), new Vector2(-CellWidth * 0.5f, CellHeight + 8f), new Vector2(100f, 108f));

        ultStack.pivot = new Vector2(0.5f, 0f);

        for (int i = 0; i < UltSegments; i++)
        {
            float t = i / (float)(UltSegments - 1);
            float width = Mathf.Lerp(94f, 24f, t);
            float y = i * 13f;
            ultSegments[i] = HudBuild.Block(ultStack, $"Seg{i}", new Vector2(0.5f, 0f), new Vector2(0f, y), new Vector2(width, 6f), Track);
        }
    }

    private void BuildFuelArc(Transform canvasParent)
    {
        RectTransform holder = HudBuild.Rect(canvasParent, "FuelArc", new Vector2(0.5f, 0.5f), new Vector2(58f, 0f), new Vector2(112f, 196f));
        fuelRoot = holder.gameObject;

        Image arcTrack = HudBuild.Glyph(holder, "Track", HudSymbol.FuelArc, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112f, 196f), FuelTrack);
        arcTrack.preserveAspect = false;

        fuelFill = HudBuild.Glyph(holder, "Fill", HudSymbol.FuelArc, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(112f, 196f), FuelColor);
        fuelFill.preserveAspect = false;
        HudBuild.AsFill(fuelFill, Image.FillMethod.Vertical, (int)Image.OriginVertical.Bottom);
        fuelFill.fillAmount = 0f;

        fuelRoot.SetActive(false);
    }
}
