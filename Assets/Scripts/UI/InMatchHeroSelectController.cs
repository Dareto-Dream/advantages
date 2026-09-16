using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;

public class InMatchHeroSelectController : MonoBehaviour
{
    private enum Filter
    {
        All,
        Tank,
        Dps,
        Support
    }

    private class Card
    {
        public Button button;
        public Image background;
        public GameObject border;
        public Image portrait;
        public Image slash;
        public int heroIndex;
        public bool hovered;
    }

    private const int GridColumns = 3;
    private const int GridRows = 4;
    private const float CardWidth = 168f;
    private const float CardHeight = 212f;
    private const float CardGap = 14f;
    private const int RosterSlots = 4;

    [Tooltip("Legacy authored picker panel, if any - hidden on Start; the runtime picker replaces it.")]
    [SerializeField] private GameObject panel;
    [Tooltip("Closed-state hint shown in the spawn room during staging.")]
    [SerializeField] private TextMeshProUGUI hintText;

    [Header("Palette")]
    [SerializeField] private Color backdropColor = new Color(0.03f, 0.04f, 0.06f, 0.90f);
    [SerializeField] private Color panelColor = new Color(0.09f, 0.10f, 0.13f, 0.92f);
    [SerializeField] private Color cardIdle = new Color(0.13f, 0.14f, 0.17f, 0.96f);
    [SerializeField] private Color cardHover = new Color(0.18f, 0.20f, 0.24f, 0.98f);
    [SerializeField] private Color cardSelected = new Color(0.15f, 0.35f, 0.34f, 1f);
    [SerializeField] private Color tabIdle = new Color(0.13f, 0.14f, 0.17f, 0.96f);
    [SerializeField] private Color tabActive = new Color(0.20f, 0.44f, 0.42f, 1f);
    [SerializeField] private Color accent = new Color(0.24f, 0.85f, 0.72f, 1f);
    [SerializeField] private Color ink = new Color(0.93f, 0.95f, 0.98f, 1f);
    [SerializeField] private Color muted = new Color(0.62f, 0.67f, 0.75f, 1f);

    private bool isOpen;
    private bool built;
    private MatchManager.Phase lastSeenPhase = MatchManager.Phase.HeroSelect;

    private Filter filter = Filter.All;
    private int pendingIndex;

    private GameObject root;
    private TextMeshProUGUI subText;
    private RectTransform gridContainer;
    private readonly Button[] tabButtons = new Button[4];
    private readonly TextMeshProUGUI[] tabLabels = new TextMeshProUGUI[4];
    private readonly Image[] tabGlyphs = new Image[4];
    private readonly List<Card> cards = new List<Card>();

    private Image modelFigure;
    private TextMeshProUGUI modelName;

    private TextMeshProUGUI statName;
    private TextMeshProUGUI statRole;
    private Image statPortrait;
    private readonly Image[] abilityGlyphs = new Image[4];
    private readonly TextMeshProUGUI[] abilityKeys = new TextMeshProUGUI[4];
    private readonly TextMeshProUGUI[] abilityNames = new TextMeshProUGUI[4];
    private readonly TextMeshProUGUI[] abilityBlurbs = new TextMeshProUGUI[4];
    private readonly GameObject[] abilityRows = new GameObject[4];
    private TextMeshProUGUI statVitals;
    private readonly Image[] weaponIcons = new Image[2];
    private readonly TextMeshProUGUI[] weaponStats = new TextMeshProUGUI[2];
    private readonly GameObject[] weaponCells = new GameObject[2];

    private readonly GameObject[] rosterCells = new GameObject[RosterSlots];
    private readonly Image[] rosterAvatar = new Image[RosterSlots];
    private readonly Image[] rosterFrame = new Image[RosterSlots];
    private readonly TextMeshProUGUI[] rosterName = new TextMeshProUGUI[RosterSlots];

    private void Start()
    {
        if (panel != null)
        {
            panel.SetActive(false);
        }

        pendingIndex = Mathf.Clamp(MatchSettings.HeroIndex, 0, Mathf.Max(0, HeroRoster.All.Length - 1));

        if (HeroRoster.All.Length > 0)
        {
            BuildUi();
            SetRootActive(false);
        }

        UpdateHint();
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
        MatchManager match = MatchManager.Instance;
        if (match == null)
        {
            return;
        }

        MatchManager.Phase phase = match.CurrentPhase;
        bool heroSelect = phase == MatchManager.Phase.HeroSelect;
        bool staging = phase == MatchManager.Phase.Staging;

        if (phase != lastSeenPhase)
        {
            lastSeenPhase = phase;
            if (isOpen && !heroSelect)
            {
                SetOpen(false);
            }
        }

        if (heroSelect && !isOpen)
        {
            SetOpen(true);
        }

        if (hintText != null)
        {
            hintText.gameObject.SetActive(staging && !isOpen);
        }

        if (isOpen && subText != null)
        {
            subText.text = SubTextFor(match, heroSelect);
        }

        if (isOpen)
        {
            RefreshRoster();
        }

        Keyboard keyboard = Keyboard.current;
        if (staging && keyboard != null && keyboard.oKey.wasPressedThisFrame)
        {
            SetOpen(!isOpen);
        }
    }

    private string SubTextFor(MatchManager match, bool heroSelect)
    {
        if (!heroSelect)
        {
            return "Press O to close";
        }

        int seconds = Mathf.CeilToInt(match.PhaseSecondsRemaining);

        if (match.PlayerReady)
        {
            return seconds > 0 ? $"Locked in — starting in {seconds}s" : "Starting…";
        }

        return seconds > 0
            ? $"Click an operative, then press Confirm  ·  {seconds}s"
            : "Confirm an operative to continue";
    }

    public void SetOpen(bool open)
    {
        MatchManager match = MatchManager.Instance;
        MatchManager.Phase phase = match != null ? match.CurrentPhase : MatchManager.Phase.MatchOver;
        bool allowed = phase == MatchManager.Phase.HeroSelect || phase == MatchManager.Phase.Staging;

        if (open && !allowed)
        {
            return;
        }

        isOpen = open;
        SetRootActive(open);
        CursorService.Set(!open);

        if (open)
        {
            pendingIndex = Mathf.Clamp(MatchSettings.HeroIndex, 0, HeroRoster.All.Length - 1);
            filter = Filter.All;
            UpdateTabVisuals();
            RebuildGrid();
            PreviewHero(pendingIndex);
        }

        if (match != null && match.Player != null)
        {
            bool canMove = !open && phase == MatchManager.Phase.Staging;
            match.Player.SetControlEnabled(canMove);
        }
    }

    public void SelectHero(int index)
    {
        pendingIndex = Mathf.Clamp(index, 0, Mathf.Max(0, HeroRoster.All.Length - 1));
        Confirm();
    }

    private void SetFilter(Filter next)
    {
        filter = next;
        UpdateTabVisuals();
        RebuildGrid();
    }

    private void PreviewHero(int index)
    {
        if (HeroRoster.All.Length == 0)
        {
            return;
        }

        pendingIndex = Mathf.Clamp(index, 0, HeroRoster.All.Length - 1);

        HeroDefinition hero = HeroRoster.All[pendingIndex];
        OperativeDefinition op = OperativeRoster.All[pendingIndex];
        Color solid = new Color(hero.color.r, hero.color.g, hero.color.b, 1f);

        if (modelFigure != null)
        {
            modelFigure.color = solid;
        }

        if (modelName != null)
        {
            modelName.text = hero.name.ToUpperInvariant();
            modelName.color = solid;
        }

        FillStatPanel(hero, op, solid);
        UpdateCardVisuals();
    }

    private void FillStatPanel(HeroDefinition hero, OperativeDefinition op, Color solid)
    {
        statName.text = hero.name.ToUpperInvariant();
        statName.color = ink;

        statRole.text = $"{HeroRoster.RoleLabel(hero.role)}   ·   {hero.archetype.ToUpperInvariant()}";
        statRole.color = solid;

        statPortrait.color = solid;

        List<OperativeDefinition.AbilitySlot> ordered = new List<OperativeDefinition.AbilitySlot>();
        foreach (OperativeDefinition.AbilitySlot slot in op.abilities)
        {
            if (slot.slot != Ability.Slot.Ultimate)
            {
                ordered.Add(slot);
            }
        }

        foreach (OperativeDefinition.AbilitySlot slot in op.abilities)
        {
            if (slot.slot == Ability.Slot.Ultimate)
            {
                ordered.Add(slot);
            }
        }

        for (int i = 0; i < abilityRows.Length; i++)
        {
            bool used = i < ordered.Count;
            abilityRows[i].SetActive(used);
            if (!used)
            {
                continue;
            }

            OperativeDefinition.AbilitySlot slot = ordered[i];
            abilityGlyphs[i].sprite = HudArt.Get(SlotSymbol(slot.slot));
            abilityGlyphs[i].color = solid;
            abilityKeys[i].text = string.IsNullOrEmpty(slot.key) ? "?" : slot.key.ToUpperInvariant();
            abilityNames[i].text = slot.name;
            abilityBlurbs[i].text = slot.slot == Ability.Slot.Ultimate ? hero.ultimate : SlotWord(slot.slot);
        }

        string armour = op.maxArmor > 0f ? $"  <color=#{ColorUtility.ToHtmlStringRGB(muted)}>+{op.maxArmor:0}</color>" : string.Empty;
        string flight = op.flight ? "  ·  FLIGHT" : string.Empty;
        statVitals.text = $"HP {op.maxHealth:0}{armour}\n{MobilityWord(op.moveSpeed)}  ·  {op.moveSpeed:0.0} u/s{flight}";

        List<WeaponDefinition> kit = OperativeWeaponLibrary.WeaponsFor(op.id);
        for (int i = 0; i < weaponCells.Length; i++)
        {
            bool used = kit != null && i < kit.Count && kit[i] != null;
            weaponCells[i].SetActive(used);
            if (!used)
            {
                continue;
            }

            WeaponDefinition weapon = kit[i];
            weaponIcons[i].sprite = HudArt.Get(weapon.magazineSize > 0 && weapon.magazineSize <= 14
                ? HudSymbol.WeaponPistol
                : HudSymbol.WeaponSmg);
            weaponIcons[i].color = ink;

            string rounds = weapon.infiniteAmmo ? "∞" : weapon.magazineSize.ToString();
            weaponStats[i].text = $"{rounds} rnd  ·  {weapon.roundsPerMinute:0} rpm";
        }
    }

    private static HudSymbol SlotSymbol(Ability.Slot slot)
    {
        switch (slot)
        {
            case Ability.Slot.Primary: return HudSymbol.Triangle;
            case Ability.Slot.Secondary: return HudSymbol.DoubleChevron;
            case Ability.Slot.Special: return HudSymbol.SquareOutline;
            default: return HudSymbol.DotRing;
        }
    }

    private static string SlotWord(Ability.Slot slot)
    {
        switch (slot)
        {
            case Ability.Slot.Primary: return "Primary ability";
            case Ability.Slot.Secondary: return "Secondary ability";
            case Ability.Slot.Special: return "Signature ability";
            default: return "Ultimate";
        }
    }

    private static string MobilityWord(float moveSpeed)
    {
        if (moveSpeed <= 4.5f) return "ANCHORED";
        if (moveSpeed <= 6f) return "STANDARD";
        if (moveSpeed <= 7f) return "MOBILE";
        return "BLINK";
    }

    private void Confirm()
    {
        if (HeroRoster.All.Length == 0)
        {
            return;
        }

        int index = Mathf.Clamp(pendingIndex, 0, HeroRoster.All.Length - 1);
        MatchSettings.HeroIndex = index;
        MatchSettings.Save();
        UpdateHint();

        MatchManager match = MatchManager.Instance;
        if (match != null)
        {
            match.MarkPlayerReady();

            if (match.Player != null)
            {
                match.Player.SetOperativeByHeroIndex(index);
            }

            if (match.CurrentPhase == MatchManager.Phase.Staging)
            {
                SetOpen(false);
            }
        }
        else
        {
            SetOpen(false);
        }
    }

    private void UpdateHint()
    {
        if (hintText == null)
        {
            return;
        }

        string heroName = HeroRoster.All.Length > 0 ? HeroRoster.Get(MatchSettings.HeroIndex).name : "NONE";
        hintText.text = $"PRESS O TO CHANGE OPERATIVE  ·  {heroName.ToUpperInvariant()}";
    }

    private void RebuildGrid()
    {
        if (gridContainer == null)
        {
            return;
        }

        for (int i = gridContainer.childCount - 1; i >= 0; i--)
        {
            Destroy(gridContainer.GetChild(i).gameObject);
        }

        cards.Clear();

        for (int i = 0; i < HeroRoster.All.Length; i++)
        {
            if (!MatchesFilter(HeroRoster.All[i].role, filter))
            {
                continue;
            }

            cards.Add(MakeHeroCard(gridContainer, HeroRoster.All[i], i));
        }

        for (int i = cards.Count; i < GridColumns * GridRows; i++)
        {
            MakeEmptyCard(gridContainer, i);
        }

        UpdateCardVisuals();
    }

    private void UpdateCardVisuals()
    {
        foreach (Card card in cards)
        {
            bool selected = card.heroIndex == pendingIndex;

            card.background.color = selected ? cardSelected : card.hovered ? cardHover : cardIdle;
            if (card.border.activeSelf != selected)
            {
                card.border.SetActive(selected);
            }

            float portraitAlpha = selected ? 1f : card.hovered ? 0.9f : 0.35f;
            Color portrait = card.portrait.color;
            card.portrait.color = new Color(portrait.r, portrait.g, portrait.b, portraitAlpha);
        }
    }

    private void UpdateTabVisuals()
    {
        for (int i = 0; i < tabButtons.Length; i++)
        {
            bool active = i == (int)filter;

            if (tabButtons[i] != null && tabButtons[i].targetGraphic is Image image)
            {
                image.color = active ? tabActive : tabIdle;
            }

            if (tabLabels[i] != null)
            {
                tabLabels[i].color = active ? ink : muted;
            }

            if (tabGlyphs[i] != null)
            {
                tabGlyphs[i].color = active ? ink : muted;
            }
        }
    }

    private void AddHoverLift(Card card)
    {
        EventTrigger trigger = card.button.gameObject.AddComponent<EventTrigger>();

        EventTrigger.Entry enter = new EventTrigger.Entry { eventID = EventTriggerType.PointerEnter };
        enter.callback.AddListener(_ =>
        {
            card.hovered = true;
            UpdateCardVisuals();
        });
        trigger.triggers.Add(enter);

        EventTrigger.Entry exit = new EventTrigger.Entry { eventID = EventTriggerType.PointerExit };
        exit.callback.AddListener(_ =>
        {
            card.hovered = false;
            UpdateCardVisuals();
        });
        trigger.triggers.Add(exit);
    }

    private static bool MatchesFilter(HeroRole role, Filter filter)
    {
        switch (filter)
        {
            case Filter.Tank: return role == HeroRole.Tank;
            case Filter.Dps: return role == HeroRole.Dps;
            case Filter.Support: return role == HeroRole.Support;
            default: return true;
        }
    }

    private void SetRootActive(bool value)
    {
        if (root != null)
        {
            root.SetActive(value);
        }
    }

    private void BuildUi()
    {
        if (built)
        {
            return;
        }

        built = true;

        GameObject canvasObject = new GameObject("HeroSelectCanvas");

        Canvas canvas = canvasObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 100;

        CanvasScaler scaler = canvasObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        canvasObject.AddComponent<GraphicRaycaster>();
        root = canvasObject;

        Image backdrop = MakeImage(canvasObject.transform, "Backdrop", backdropColor);
        Stretch(backdrop.rectTransform);
        backdrop.raycastTarget = true;

        BuildTabs(canvasObject.transform);
        BuildGrid(canvasObject.transform);
        BuildModel(canvasObject.transform);
        BuildStatPanel(canvasObject.transform);
        BuildRoster(canvasObject.transform);
        BuildConfirm(canvasObject.transform);
    }

    private void BuildTabs(Transform parent)
    {

        Button all = MakeTextButton(parent, "Tab_All", "ALL", 18f, out TextMeshProUGUI allLabel);
        Anchored(all.GetComponent<RectTransform>(), TopLeft, TopLeft, TopLeft, new Vector2(88f, -46f), new Vector2(136f, 44f));
        all.onClick.AddListener(() => SetFilter(Filter.All));
        tabButtons[0] = all;
        tabLabels[0] = allLabel;

        HudSymbol[] roleGlyphs = { HudSymbol.Shield, HudSymbol.Triangle, HudSymbol.Plus };
        for (int i = 0; i < roleGlyphs.Length; i++)
        {
            Button tab = MakeButton(parent, $"Tab_Role{i}", out Image background);
            Anchored(tab.GetComponent<RectTransform>(), TopLeft, TopLeft, TopLeft,
                new Vector2(240f + i * 52f, -46f), new Vector2(44f, 44f));
            background.color = tabIdle;

            Image glyph = HudBuild.Glyph(tab.transform, "Glyph", roleGlyphs[i], new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(24f, 24f), muted);
            tabGlyphs[i + 1] = glyph;

            Filter captured = (Filter)(i + 1);
            tab.onClick.AddListener(() => SetFilter(captured));
            tabButtons[i + 1] = tab;
        }

        TextMeshProUGUI title = MakeText(parent, "Title", "SELECT OPERATIVE", 30f, FontStyles.Bold, TextAlignmentOptions.Left);
        Anchored(title.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(452f, -42f), new Vector2(700f, 36f));
        title.color = ink;

        subText = MakeText(parent, "SubText", string.Empty, 17f, FontStyles.Normal, TextAlignmentOptions.Left);
        Anchored(subText.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(454f, -76f), new Vector2(700f, 24f));
        subText.color = muted;

        UpdateTabVisuals();
    }

    private void BuildGrid(Transform parent)
    {
        float width = GridColumns * CardWidth + (GridColumns - 1) * CardGap;
        float height = GridRows * CardHeight + (GridRows - 1) * CardGap;

        GameObject gridObject = new GameObject("Grid", typeof(RectTransform));
        gridObject.transform.SetParent(parent, false);
        gridContainer = gridObject.GetComponent<RectTransform>();
        Anchored(gridContainer, TopLeft, TopLeft, TopLeft, new Vector2(88f, -110f), new Vector2(width, height));

        GridLayoutGroup grid = gridObject.AddComponent<GridLayoutGroup>();
        grid.cellSize = new Vector2(CardWidth, CardHeight);
        grid.spacing = new Vector2(CardGap, CardGap);
        grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
        grid.constraintCount = GridColumns;
        grid.childAlignment = TextAnchor.UpperLeft;
    }

    private void MakeEmptyCard(Transform parent, int index)
    {
        Image slot = MakeImage(parent, $"Empty{index}", new Color(0.10f, 0.11f, 0.14f, 0.75f));
        slot.raycastTarget = false;
    }

    private Card MakeHeroCard(Transform parent, HeroDefinition hero, int heroIndex)
    {
        Button button = MakeButton(parent, $"Card_{hero.name}", out Image background);
        background.color = cardIdle;

        button.gameObject.AddComponent<RectMask2D>();

        Color solid = new Color(hero.color.r, hero.color.g, hero.color.b, 1f);

        Image slash = MakeImage(button.transform, "Slash", new Color(solid.r, solid.g, solid.b, 0.55f));
        RectTransform slashRect = slash.rectTransform;
        slashRect.anchorMin = slashRect.anchorMax = slashRect.pivot = new Vector2(0.5f, 0.5f);
        slashRect.anchoredPosition = Vector2.zero;
        slashRect.sizeDelta = new Vector2(Mathf.Sqrt(CardWidth * CardWidth + CardHeight * CardHeight), 7f);
        slashRect.localRotation = Quaternion.Euler(0f, 0f, -Mathf.Atan2(CardHeight, CardWidth) * Mathf.Rad2Deg);
        slash.raycastTarget = false;

        Image portrait = HudBuild.Glyph(button.transform, "Portrait", HudSymbol.Person,
            new Vector2(0.5f, 0.5f), new Vector2(0f, 16f), new Vector2(104f, 104f), new Color(solid.r, solid.g, solid.b, 0.35f));

        Image footer = MakeImage(button.transform, "Footer", new Color(0f, 0f, 0f, 0.55f));
        Band(footer.rectTransform, 0f, 0.24f, 0f, 0f);
        footer.raycastTarget = false;

        HudBuild.Glyph(footer.transform, "RoleGlyph", RoleSymbol(hero.role), new Vector2(0f, 0.5f), new Vector2(16f, 0f), new Vector2(20f, 20f), solid);

        TextMeshProUGUI name = MakeText(footer.transform, "Name", hero.name.ToUpperInvariant(), 17f, FontStyles.Bold, TextAlignmentOptions.Left);
        Band(name.rectTransform, 0f, 1f, 44f, 8f);
        name.color = ink;

        GameObject border = new GameObject("Border", typeof(RectTransform));
        border.transform.SetParent(button.transform, false);
        Stretch(border.GetComponent<RectTransform>());

        const float stroke = 3f;
        HudBuild.Block(border.transform, "Top", new Vector2(0.5f, 1f), Vector2.zero, new Vector2(CardWidth, stroke), accent);
        HudBuild.Block(border.transform, "Bottom", new Vector2(0.5f, 0f), Vector2.zero, new Vector2(CardWidth, stroke), accent);
        HudBuild.Block(border.transform, "Left", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(stroke, CardHeight), accent);
        HudBuild.Block(border.transform, "Right", new Vector2(1f, 0.5f), Vector2.zero, new Vector2(stroke, CardHeight), accent);
        border.SetActive(false);

        Card card = new Card
        {
            button = button,
            background = background,
            border = border,
            portrait = portrait,
            slash = slash,
            heroIndex = heroIndex
        };

        int captured = heroIndex;
        button.onClick.AddListener(() => PreviewHero(captured));
        AddHoverLift(card);

        return card;
    }

    private static HudSymbol RoleSymbol(HeroRole role)
    {
        switch (role)
        {
            case HeroRole.Tank: return HudSymbol.Shield;
            case HeroRole.Support: return HudSymbol.Plus;
            default: return HudSymbol.Triangle;
        }
    }

    private void BuildModel(Transform parent)
    {
        GameObject holder = new GameObject("Model", typeof(RectTransform));
        holder.transform.SetParent(parent, false);
        Anchored(holder.GetComponent<RectTransform>(), TopLeft, TopLeft, TopLeft, new Vector2(700f, -104f), new Vector2(780f, 600f));

        modelFigure = HudBuild.Glyph(holder.transform, "Figure", HudSymbol.Person, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(460f, 460f), Color.grey);

        modelName = MakeText(holder.transform, "Name", string.Empty, 26f, FontStyles.Bold, TextAlignmentOptions.Center);
        Band(modelName.rectTransform, 0f, 0.09f, 0f, 0f);
    }

    private void BuildStatPanel(Transform parent)
    {
        Image panelImage = MakeImage(parent, "StatPanel", panelColor);
        Anchored(panelImage.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(1544f, -46f), new Vector2(330f, 646f));
        panelImage.raycastTarget = false;

        Transform p = panelImage.transform;

        statName = MakeText(p, "Name", string.Empty, 32f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Anchored(statName.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(18f, -14f), new Vector2(294f, 40f));

        statRole = MakeText(p, "Role", string.Empty, 14f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Anchored(statRole.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(20f, -54f), new Vector2(294f, 20f));

        statPortrait = HudBuild.Glyph(p, "Portrait", HudSymbol.Person, TopLeft, new Vector2(20f, -80f), new Vector2(42f, 42f), Color.grey);

        HudBuild.Block(p, "Rule", TopLeft, new Vector2(18f, -130f), new Vector2(294f, 2f), new Color(1f, 1f, 1f, 0.16f));

        for (int i = 0; i < abilityRows.Length; i++)
        {
            abilityRows[i] = BuildAbilityRow(p, i);
        }

        HudBuild.Block(p, "Rule2", TopLeft, new Vector2(18f, -474f), new Vector2(294f, 2f), new Color(1f, 1f, 1f, 0.16f));

        statVitals = MakeText(p, "Vitals", string.Empty, 19f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Anchored(statVitals.rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(20f, -484f), new Vector2(294f, 56f));

        for (int i = 0; i < weaponCells.Length; i++)
        {
            GameObject cell = new GameObject($"Weapon{i}", typeof(RectTransform));
            cell.transform.SetParent(p, false);
            Anchored(cell.GetComponent<RectTransform>(), TopLeft, TopLeft, TopLeft, new Vector2(20f + i * 150f, -548f), new Vector2(142f, 82f));

            weaponIcons[i] = HudBuild.Glyph(cell.transform, "Icon", HudSymbol.WeaponSmg, new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(96f, 42f), ink);

            weaponStats[i] = MakeText(cell.transform, "Stats", string.Empty, 13f, FontStyles.Normal, TextAlignmentOptions.Top);
            Anchored(weaponStats[i].rectTransform, new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0.5f, 0f), new Vector2(0f, 2f), new Vector2(142f, 34f));
            weaponStats[i].color = muted;

            weaponCells[i] = cell;
            cell.SetActive(false);
        }
    }

    private GameObject BuildAbilityRow(Transform parent, int index)
    {
        GameObject row = new GameObject($"Ability{index}", typeof(RectTransform));
        row.transform.SetParent(parent, false);
        Anchored(row.GetComponent<RectTransform>(), TopLeft, TopLeft, TopLeft, new Vector2(18f, -142f - index * 84f), new Vector2(294f, 78f));

        abilityGlyphs[index] = HudBuild.Glyph(row.transform, "Glyph", HudSymbol.Triangle, new Vector2(0f, 1f), new Vector2(6f, -6f), new Vector2(40f, 40f), Color.grey);

        abilityKeys[index] = MakeText(row.transform, "Key", string.Empty, 14f, FontStyles.Bold, TextAlignmentOptions.Center);
        Anchored(abilityKeys[index].rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(0f, -50f), new Vector2(52f, 20f));
        abilityKeys[index].color = muted;

        abilityNames[index] = MakeText(row.transform, "Name", string.Empty, 18f, FontStyles.Bold, TextAlignmentOptions.TopLeft);
        Anchored(abilityNames[index].rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(60f, -6f), new Vector2(232f, 26f));

        abilityBlurbs[index] = MakeText(row.transform, "Blurb", string.Empty, 12f, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        Anchored(abilityBlurbs[index].rectTransform, TopLeft, TopLeft, TopLeft, new Vector2(60f, -30f), new Vector2(232f, 42f));
        abilityBlurbs[index].color = muted;
        abilityBlurbs[index].textWrappingMode = TextWrappingModes.Normal;
        abilityBlurbs[index].overflowMode = TextOverflowModes.Ellipsis;

        row.SetActive(false);
        return row;
    }

    private void BuildRoster(Transform parent)
    {
        const float cellWidth = 226f;
        const float gap = 12f;
        const float startX = 800f;

        for (int i = 0; i < rosterCells.Length; i++)
        {
            GameObject cell = new GameObject($"Roster{i}", typeof(RectTransform));
            cell.transform.SetParent(parent, false);

            Image background = cell.AddComponent<Image>();
            background.color = new Color(0f, 0f, 0f, 0.45f);
            background.raycastTarget = false;
            Anchored(cell.GetComponent<RectTransform>(), TopLeft, TopLeft, TopLeft,
                new Vector2(startX + i * (cellWidth + gap), -712f), new Vector2(cellWidth, 232f));

            Image frame = HudBuild.Glyph(cell.transform, "Frame", HudSymbol.SquareOutline, new Vector2(0.5f, 1f), new Vector2(0f, -12f), new Vector2(150f, 150f), muted);
            rosterFrame[i] = frame;

            rosterAvatar[i] = HudBuild.Glyph(cell.transform, "Avatar", HudSymbol.Person, new Vector2(0.5f, 1f), new Vector2(0f, -32f), new Vector2(104f, 104f), Color.grey);

            TextMeshProUGUI name = MakeText(cell.transform, "Name", string.Empty, 19f, FontStyles.Bold, TextAlignmentOptions.Center);
            Band(name.rectTransform, 0.04f, 0.22f, 8f, 8f);
            name.color = ink;
            rosterName[i] = name;

            rosterCells[i] = cell;
        }
    }

    private void RefreshRoster()
    {
        if (rosterCells[0] == null || HeroRoster.All.Length == 0)
        {
            return;
        }

        int pending = Mathf.Clamp(pendingIndex, 0, HeroRoster.All.Length - 1);
        SetRosterSlot(0, MatchSettings.PlayerName, HeroRoster.All[pending]);

        int slot = 1;
        MatchManager match = MatchManager.Instance;
        if (match != null)
        {
            foreach (BotBrain bot in match.Bots)
            {
                if (slot >= rosterCells.Length)
                {
                    break;
                }

                if (bot == null || bot.Health == null || bot.Health.Team != MatchSettings.PlayerTeam)
                {
                    continue;
                }

                BotOperative botOp = bot.GetComponent<BotOperative>();
                int heroIndex = botOp != null ? Mathf.Clamp((int)botOp.Id, 0, HeroRoster.All.Length - 1) : 0;
                SetRosterSlot(slot, bot.Health.DisplayName, HeroRoster.All[heroIndex]);
                slot++;
            }
        }

        for (int i = slot; i < rosterCells.Length; i++)
        {
            if (rosterCells[i] == null)
            {
                continue;
            }

            rosterCells[i].SetActive(true);
            rosterAvatar[i].color = new Color(0.45f, 0.48f, 0.54f, 0.6f);
            rosterFrame[i].color = new Color(0.45f, 0.48f, 0.54f, 0.5f);
            rosterName[i].text = "—";
            rosterName[i].color = muted;
        }
    }

    private void SetRosterSlot(int index, string label, HeroDefinition hero)
    {
        if (rosterCells[index] == null)
        {
            return;
        }

        rosterCells[index].SetActive(true);

        Color solid = new Color(hero.color.r, hero.color.g, hero.color.b, 1f);
        rosterAvatar[index].color = solid;
        rosterFrame[index].color = solid;
        rosterName[index].text = label.ToUpperInvariant();
        rosterName[index].color = ink;
    }

    private void BuildConfirm(Transform parent)
    {
        Button confirm = MakeTextButton(parent, "Confirm", "CONFIRM", 24f, out TextMeshProUGUI label);
        Anchored(confirm.GetComponent<RectTransform>(), BottomCentre, BottomCentre, BottomCentre, new Vector2(0f, 34f), new Vector2(360f, 62f));
        label.color = Color.white;

        confirm.transition = Selectable.Transition.ColorTint;
        ColorBlock colors = confirm.colors;
        colors.normalColor = new Color(0.16f, 0.35f, 0.34f, 1f);
        colors.highlightedColor = new Color(0.22f, 0.47f, 0.45f, 1f);
        colors.pressedColor = new Color(0.12f, 0.26f, 0.26f, 1f);
        colors.selectedColor = colors.highlightedColor;
        colors.disabledColor = new Color(0.2f, 0.2f, 0.2f, 1f);
        colors.colorMultiplier = 1f;
        confirm.colors = colors;

        confirm.onClick.AddListener(Confirm);
    }

    private static readonly Vector2 TopLeft = new Vector2(0f, 1f);
    private static readonly Vector2 BottomCentre = new Vector2(0.5f, 0f);

    private static void Stretch(RectTransform rect)
    {
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    private static void Anchored(RectTransform rect, Vector2 anchorMin, Vector2 anchorMax, Vector2 pivot, Vector2 anchoredPosition, Vector2 size)
    {
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.pivot = pivot;
        rect.anchoredPosition = anchoredPosition;
        rect.sizeDelta = size;
    }

    private static void Band(RectTransform rect, float yMin, float yMax, float left, float right)
    {
        rect.anchorMin = new Vector2(0f, yMin);
        rect.anchorMax = new Vector2(1f, yMax);
        rect.offsetMin = new Vector2(left, 0f);
        rect.offsetMax = new Vector2(-right, 0f);
    }

    private static Image MakeImage(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);
        Image image = go.AddComponent<Image>();
        image.color = color;
        return image;
    }

    private TextMeshProUGUI MakeText(Transform parent, string name, string text, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        TextMeshProUGUI label = go.AddComponent<TextMeshProUGUI>();
        label.text = text;
        label.fontSize = size;
        label.fontStyle = style;
        label.alignment = alignment;
        label.color = ink;
        label.raycastTarget = false;
        label.richText = true;
        label.textWrappingMode = TextWrappingModes.NoWrap;
        return label;
    }

    private Button MakeButton(Transform parent, string name, out Image background)
    {
        GameObject go = new GameObject(name, typeof(RectTransform));
        go.transform.SetParent(parent, false);

        background = go.AddComponent<Image>();
        background.color = cardIdle;

        Button button = go.AddComponent<Button>();
        button.transition = Selectable.Transition.None;
        button.targetGraphic = background;
        return button;
    }

    private Button MakeTextButton(Transform parent, string name, string text, float fontSize, out TextMeshProUGUI label)
    {
        Button button = MakeButton(parent, name, out Image _);
        label = MakeText(button.transform, "Label", text, fontSize, FontStyles.Bold, TextAlignmentOptions.Center);
        Stretch(label.rectTransform);
        return button;
    }
}
