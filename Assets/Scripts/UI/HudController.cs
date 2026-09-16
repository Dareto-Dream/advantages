using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class HudController : MonoBehaviour
{
    [Header("Crosshair")]
    [SerializeField] private float crosshairBaseGap = 6f;
    [SerializeField] private float crosshairPixelsPerDegree = 26f;

    [Header("Crosshair References")]
    [SerializeField] private RectTransform crosshair;
    [Tooltip("Up, down, left, right. Distance from centre is driven by weapon spread.")]
    [SerializeField] private RectTransform[] crosshairArms = new RectTransform[4];
    [SerializeField] private RectTransform hitmarker;

    [Header("Vitals")]
    [SerializeField] private Image healthFill;
    [SerializeField] private Image armorFill;
    [Tooltip("Hidden for loadouts that spawn with no armour.")]
    [SerializeField] private GameObject armorTrack;
    [SerializeField] private TextMeshProUGUI healthText;
    [SerializeField] private TextMeshProUGUI speedText;
    [SerializeField] private Image damageVignette;

    [Header("Weapon")]
    [SerializeField] private TextMeshProUGUI ammoText;
    [SerializeField] private TextMeshProUGUI weaponText;
    [SerializeField] private TextMeshProUGUI reloadText;

    [Header("Match")]
    [SerializeField] private TextMeshProUGUI timerText;
    [SerializeField] private TextMeshProUGUI attackerScoreText;
    [SerializeField] private TextMeshProUGUI defenderScoreText;
    [SerializeField] private TextMeshProUGUI aliveText;
    [SerializeField] private TextMeshProUGUI bannerText;
    [SerializeField] private TextMeshProUGUI subBannerText;

    [Tooltip("Oldest line first. The feed scrolls up, newest at the bottom.")]
    [SerializeField] private List<TextMeshProUGUI> killFeed = new List<TextMeshProUGUI>();

    [Header("Palette")]
    [SerializeField] private Color accent = new Color(0.20f, 0.85f, 0.72f, 1f);
    [SerializeField] private Color danger = new Color(0.95f, 0.28f, 0.32f, 1f);
    [SerializeField] private Color muted = new Color(0.62f, 0.67f, 0.75f, 1f);
    [SerializeField] private Color ink = new Color(0.93f, 0.95f, 0.98f, 1f);
    [SerializeField] private Color headshotColor = new Color(1f, 0.85f, 0.30f, 1f);

    [Header("Tuning")]
    [Tooltip("Fraction of max health below which the bar turns red.")]
    [SerializeField] private float lowHealthFraction = 0.3f;
    [SerializeField] private float killFeedSeconds = 6f;

    private readonly List<float> killFeedExpiry = new List<float>();

    private MatchManager match;
    private PlayerController player;
    private float hitmarkerUntil;
    private float vignetteAlpha;

    private bool reticleBuilt;
    private RectTransform crosshairDot;
    private RectTransform scopeRoot;
    private Image scopeTop;
    private Image scopeBottom;
    private Image scopeLeft;
    private Image scopeRight;
    private Image scopeReticleH;
    private Image scopeReticleV;

    private void Awake()
    {
        killFeedExpiry.Clear();
        for (int i = 0; i < killFeed.Count; i++)
        {
            killFeedExpiry.Add(0f);
        }
    }

    private void Start()
    {

        EnsureOverlay<AbilityHudOverlay>("AbilityHudOverlay");
        EnsureOverlay<AllyRosterOverlay>("AllyRosterOverlay");
        EnsureOverlay<PlayerHudOverlay>("PlayerHudOverlay");
        EnsureOverlay<ObjectiveHudOverlay>("ObjectiveHudOverlay");

        RetireAuthoredWidgets();

        match = MatchManager.Instance;

        if (match != null)
        {
            match.PhaseChanged += HandlePhaseChanged;
            match.ScoreChanged += HandleScoreChanged;
            match.RoundEnded += HandleRoundEnded;
            match.MatchEnded += HandleMatchEnded;
            match.KillLogged += HandleKillLogged;
            HandleScoreChanged(match.AttackerScore, match.DefenderScore);
        }

        HideHitmarker();
        SetBanner(string.Empty, string.Empty);
    }

    private void OnDestroy()
    {
        if (match == null)
        {
            return;
        }

        match.PhaseChanged -= HandlePhaseChanged;
        match.ScoreChanged -= HandleScoreChanged;
        match.RoundEnded -= HandleRoundEnded;
        match.MatchEnded -= HandleMatchEnded;
        match.KillLogged -= HandleKillLogged;
    }

    private static void EnsureOverlay<T>(string name) where T : MonoBehaviour
    {
        if (FindAnyObjectByType<T>() == null)
        {
            new GameObject(name).AddComponent<T>();
        }
    }

    private void RetireAuthoredWidgets()
    {

        Transform scorePanel = CommonAncestor(timerText, attackerScoreText, defenderScoreText, aliveText);
        if (scorePanel != null && !Owns(scorePanel, crosshair) && !Owns(scorePanel, bannerText))
        {
            Retire(scorePanel);
        }
        else
        {
            Retire(timerText != null ? timerText.transform : null);
            Retire(attackerScoreText != null ? attackerScoreText.transform : null);
            Retire(defenderScoreText != null ? defenderScoreText.transform : null);
            Retire(aliveText != null ? aliveText.transform : null);
        }

        timerText = null;
        attackerScoreText = null;
        defenderScoreText = null;
        aliveText = null;

        Retire(healthFill != null ? healthFill.transform.parent : null);
        Retire(armorTrack != null ? armorTrack.transform : null);
        Retire(healthText != null ? healthText.transform : null);
        Retire(speedText != null ? speedText.transform : null);
        Retire(ammoText != null ? ammoText.transform : null);
        Retire(weaponText != null ? weaponText.transform : null);
        Retire(reloadText != null ? reloadText.transform : null);

        healthFill = null;
        armorFill = null;
        armorTrack = null;
        healthText = null;
        speedText = null;
        ammoText = null;
        weaponText = null;
        reloadText = null;
    }

    private static bool Owns(Transform ancestor, Component candidate) =>
        candidate != null && candidate.transform.IsChildOf(ancestor);

    private static void Retire(Transform target)
    {
        if (target != null && target.gameObject.activeSelf)
        {
            target.gameObject.SetActive(false);
        }
    }

    private static Transform CommonAncestor(params Component[] parts)
    {
        Transform result = null;

        foreach (Component part in parts)
        {
            if (part == null)
            {
                continue;
            }

            if (result == null)
            {
                result = part.transform;
                continue;
            }

            result = NearestShared(result, part.transform);
        }

        return result;
    }

    private static Transform NearestShared(Transform a, Transform b)
    {
        for (Transform walk = a; walk != null; walk = walk.parent)
        {
            if (b.IsChildOf(walk))
            {
                return walk;
            }
        }

        return a;
    }

    private void Update()
    {
        BindPlayer();
        UpdateCrosshair();
        UpdateVitals();
        UpdateMatchInfo();
        UpdateKillFeed();
        UpdateFeedback();
    }

    private void BindPlayer()
    {
        if (player != null || match == null || match.Player == null)
        {
            return;
        }

        player = match.Player;
        player.HitConfirmed += HandleHitConfirmed;
        player.Health.Damaged += HandleLocalDamaged;
    }

    private void UpdateCrosshair()
    {
        if (crosshair == null)
        {
            return;
        }

        EnsureRuntimeReticle();

        WeaponController weapons = player != null ? player.Weapons : null;
        WeaponDefinition weapon = weapons != null ? weapons.Current : null;

        float spread = weapons != null ? weapons.SpreadDegrees : 1f;
        float ads = weapons != null ? weapons.AdsProgress01 : 0f;

        CrosshairStyle style = weapon != null ? weapon.crosshairStyle : CrosshairStyle.Cross;
        OpticType optic = weapon != null ? weapon.optic : OpticType.Iron;
        Color color = weapon != null ? weapon.crosshairColor : ink;
        float baseGap = weapon != null ? weapon.crosshairBaseGap : crosshairBaseGap;
        float perDegree = weapon != null ? weapon.crosshairSpreadScale : crosshairPixelsPerDegree;
        bool wantDot = weapon != null && weapon.crosshairShowDot;

        bool dead = player != null && player.IsDead;
        bool scopedAim = optic == OpticType.Scoped && ads > 0.3f;

        if (optic == OpticType.Reflex && ads > 0.5f)
        {
            style = CrosshairStyle.Dot;
            wantDot = true;
            baseGap = 0f;
        }

        bool showArms = !dead && !scopedAim && style == CrosshairStyle.Cross;
        bool showDot = !dead && !scopedAim && (style == CrosshairStyle.Dot || wantDot);

        float gap = baseGap + spread * perDegree;

        for (int i = 0; i < crosshairArms.Length; i++)
        {
            RectTransform arm = crosshairArms[i];
            if (arm == null)
            {
                continue;
            }

            arm.gameObject.SetActive(showArms);
            if (!showArms)
            {
                continue;
            }

            float reach = i >= 2
                ? gap + arm.sizeDelta.x * 0.5f
                : gap + arm.sizeDelta.y * 0.5f;

            arm.anchoredPosition = CrosshairDirection(i) * reach;

            Image armImage = arm.GetComponent<Image>();
            if (armImage != null)
            {
                armImage.color = color;
            }
        }

        if (crosshairDot != null)
        {
            crosshairDot.gameObject.SetActive(showDot);
            Image dotImage = crosshairDot.GetComponent<Image>();
            if (dotImage != null)
            {
                dotImage.color = color;
            }
        }

        crosshair.gameObject.SetActive(!dead);

        UpdateScopeOverlay(optic == OpticType.Scoped && !dead ? ads : 0f, color);
    }

    private void EnsureRuntimeReticle()
    {
        if (reticleBuilt)
        {
            return;
        }

        reticleBuilt = true;

        for (int i = 0; i < crosshairArms.Length; i++)
        {
            RectTransform arm = crosshairArms[i];
            if (arm == null)
            {
                continue;
            }

            arm.sizeDelta = i >= 2 ? new Vector2(20f, 6f) : new Vector2(6f, 20f);
        }

        GameObject dot = new GameObject("CrosshairDot");
        dot.transform.SetParent(crosshair, false);
        Image dotImage = dot.AddComponent<Image>();
        dotImage.color = ink;
        dotImage.raycastTarget = false;
        crosshairDot = dot.GetComponent<RectTransform>();
        crosshairDot.sizeDelta = new Vector2(6f, 6f);
        crosshairDot.anchoredPosition = Vector2.zero;
        crosshairDot.gameObject.SetActive(false);

        Canvas canvas = GetComponentInParent<Canvas>();
        Transform overlayParent = canvas != null ? canvas.transform : transform;

        GameObject root = new GameObject("ScopeOverlay", typeof(RectTransform));
        root.transform.SetParent(overlayParent, false);
        scopeRoot = root.GetComponent<RectTransform>();
        scopeRoot.anchorMin = Vector2.zero;
        scopeRoot.anchorMax = Vector2.one;
        scopeRoot.offsetMin = Vector2.zero;
        scopeRoot.offsetMax = Vector2.zero;
        scopeRoot.SetAsLastSibling();

        scopeTop = MakeScopeQuad("ScopeTop");
        scopeBottom = MakeScopeQuad("ScopeBottom");
        scopeLeft = MakeScopeQuad("ScopeLeft");
        scopeRight = MakeScopeQuad("ScopeRight");
        scopeReticleH = MakeScopeQuad("ScopeReticleH");
        scopeReticleV = MakeScopeQuad("ScopeReticleV");

        scopeRoot.gameObject.SetActive(false);
    }

    private Image MakeScopeQuad(string name)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(scopeRoot, false);
        Image image = go.AddComponent<Image>();
        image.color = new Color(0f, 0f, 0f, 0f);
        image.raycastTarget = false;

        RectTransform rect = go.GetComponent<RectTransform>();
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        return image;
    }

    private void UpdateScopeOverlay(float ads, Color reticleColor)
    {
        if (scopeRoot == null)
        {
            return;
        }

        bool active = ads > 0.01f;
        scopeRoot.gameObject.SetActive(active);
        if (!active)
        {
            return;
        }

        float fracY = Mathf.Lerp(0f, 0.38f, ads);
        float fracX = Mathf.Lerp(0f, 0.40f, ads);
        Color mask = new Color(0f, 0f, 0f, Mathf.Lerp(0f, 0.94f, ads));

        SetAnchors(scopeTop, new Vector2(0f, 1f - fracY), new Vector2(1f, 1f), mask);
        SetAnchors(scopeBottom, new Vector2(0f, 0f), new Vector2(1f, fracY), mask);
        SetAnchors(scopeLeft, new Vector2(0f, 0f), new Vector2(fracX, 1f), mask);
        SetAnchors(scopeRight, new Vector2(1f - fracX, 0f), new Vector2(1f, 1f), mask);

        Color line = new Color(reticleColor.r, reticleColor.g, reticleColor.b, ads);
        SetAnchors(scopeReticleH, new Vector2(0.485f, 0.499f), new Vector2(0.515f, 0.501f), line);
        SetAnchors(scopeReticleV, new Vector2(0.499f, 0.470f), new Vector2(0.501f, 0.530f), line);
    }

    private static void SetAnchors(Image image, Vector2 anchorMin, Vector2 anchorMax, Color color)
    {
        if (image == null)
        {
            return;
        }

        RectTransform rect = image.rectTransform;
        rect.anchorMin = anchorMin;
        rect.anchorMax = anchorMax;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        image.color = color;
    }

    private static Vector2 CrosshairDirection(int index)
    {
        switch (index)
        {
            case 0: return Vector2.up;
            case 1: return Vector2.down;
            case 2: return Vector2.left;
            default: return Vector2.right;
        }
    }

    private void UpdateVitals()
    {
        if (player == null)
        {
            return;
        }

        Health health = player.Health;

        if (healthFill != null)
        {
            healthFill.fillAmount = health.HealthFraction;
            healthFill.color = health.HealthFraction < lowHealthFraction ? danger : accent;
        }

        if (armorFill != null)
        {
            armorFill.fillAmount = health.ArmorFraction;
        }

        if (armorTrack != null)
        {
            armorTrack.SetActive(health.MaxArmor > 0f);
        }

        if (healthText != null)
        {
            healthText.text = Mathf.CeilToInt(health.CurrentHealth).ToString();
        }

        WeaponController weapons = player.Weapons;
        if (weapons != null)
        {
            if (ammoText != null)
            {
                ammoText.text = weapons.Current != null && weapons.Current.infiniteAmmo
                    ? "<size=90%>∞</size>"
                    : $"{weapons.MagazineAmmo}<size=60%> / ∞</size>";
            }

            if (weaponText != null && weapons.Current != null)
            {
                weaponText.text = weapons.Current.displayName.ToUpperInvariant();
            }

            if (reloadText != null)
            {
                bool showReload = weapons.IsReloading || (weapons.UsesMagazine && weapons.MagazineAmmo == 0);
                reloadText.gameObject.SetActive(showReload);
                reloadText.text = weapons.IsReloading ? "RELOADING" : "PRESS R TO RELOAD";
            }
        }

        if (speedText != null && player.Mover != null)
        {
            float speed = player.Mover.HorizontalSpeed;
            speedText.text = $"{speed:0.0} u/s";
            speedText.color = Color.Lerp(muted, accent, player.Mover.SpeedExcess01);
        }
    }

    private void UpdateMatchInfo()
    {
        if (match == null)
        {
            return;
        }

        if (timerText != null)
        {
            float seconds = match.PhaseSecondsRemaining;
            bool showClock = match.CurrentPhase == MatchManager.Phase.Live
                || match.CurrentPhase == MatchManager.Phase.Overtime
                || match.CurrentPhase == MatchManager.Phase.Staging;
            timerText.text = showClock
                ? $"{Mathf.FloorToInt(seconds / 60f)}:{Mathf.FloorToInt(seconds % 60f):00}"
                : "--:--";
        }

        if (aliveText != null)
        {
            int allies = CombatantRegistry.AliveCount(MatchSettings.PlayerTeam);
            int enemies = CombatantRegistry.AliveCount(MatchSettings.EnemyTeam);
            aliveText.text = $"{allies} v {enemies}";
        }

        if (match.CurrentPhase == MatchManager.Phase.Staging && bannerText != null)
        {
            SetBanner($"ROUND {match.RoundNumber}  ·  SPAWN ROOM", $"Live in {Mathf.CeilToInt(match.PhaseSecondsRemaining)}  ·  Press O to change hero  ·  Hold TAB for the scoreboard");
        }
    }

    private void UpdateFeedback()
    {
        if (hitmarker != null && hitmarker.gameObject.activeSelf && Time.time >= hitmarkerUntil)
        {
            HideHitmarker();
        }

        if (damageVignette != null)
        {
            vignetteAlpha = Mathf.Max(0f, vignetteAlpha - Time.deltaTime * 1.6f);
            Color color = damageVignette.color;
            damageVignette.color = new Color(color.r, color.g, color.b, vignetteAlpha);
        }
    }

    private void UpdateKillFeed()
    {
        for (int i = 0; i < killFeed.Count; i++)
        {
            if (killFeed[i] != null && killFeed[i].gameObject.activeSelf && Time.time >= killFeedExpiry[i])
            {
                killFeed[i].gameObject.SetActive(false);
            }
        }
    }

    private void HandleHitConfirmed(float damage, bool headshot, bool kill)
    {
        if (hitmarker == null)
        {
            return;
        }

        hitmarker.gameObject.SetActive(true);
        hitmarkerUntil = Time.time + (kill ? 0.35f : 0.12f);

        Color color = kill ? danger : headshot ? headshotColor : ink;
        foreach (Image image in hitmarker.GetComponentsInChildren<Image>())
        {
            image.color = color;
        }

        hitmarker.localScale = Vector3.one * (kill ? 1.5f : headshot ? 1.25f : 1f);
    }

    private void HandleLocalDamaged(float amount, DamageInfo info)
    {
        vignetteAlpha = Mathf.Min(0.55f, vignetteAlpha + amount / 90f);
    }

    private void HideHitmarker()
    {
        if (hitmarker != null)
        {
            hitmarker.gameObject.SetActive(false);
        }
    }

    private void HandlePhaseChanged(MatchManager.Phase phase)
    {
        switch (phase)
        {
            case MatchManager.Phase.HeroSelect:

                SetBanner(string.Empty, string.Empty);
                break;
            case MatchManager.Phase.Staging:
                SetBanner($"ROUND {match.RoundNumber}  ·  SPAWN ROOM", "Press O to change hero");
                break;
            case MatchManager.Phase.Live:
                SetBanner(string.Empty, string.Empty);
                break;
        }
    }

    private void HandleScoreChanged(int attackers, int defenders)
    {
        if (attackerScoreText != null)
        {
            attackerScoreText.text = attackers.ToString();
            attackerScoreText.color = TeamPalette.For(Team.Attackers);
        }

        if (defenderScoreText != null)
        {
            defenderScoreText.text = defenders.ToString();
            defenderScoreText.color = TeamPalette.For(Team.Defenders);
        }
    }

    private void HandleRoundEnded(Team winner)
    {
        string headline = winner == Team.None
            ? "ROUND DRAW"
            : winner == MatchSettings.PlayerTeam ? "ROUND WON" : "ROUND LOST";

        string sub = match != null && match.ComebackTeam != Team.None && MatchSettings.ComebackBuffEnabled
            ? $"{match.ComebackTeam.DisplayName()} get comeback armour next round"
            : string.Empty;

        SetBanner(headline, sub);
    }

    private void HandleMatchEnded(Team winner)
    {
        SetBanner(
            winner == MatchSettings.PlayerTeam ? "VICTORY" : "DEFEAT",
            $"{match.AttackerScore} - {match.DefenderScore}   ·   Esc for the menu"
        );
    }

    private void HandleKillLogged(string killer, string victim, string weapon, Team killerTeam, bool headshot)
    {
        if (killFeed.Count == 0)
        {
            return;
        }

        for (int i = 0; i < killFeed.Count - 1; i++)
        {
            if (killFeed[i] == null || killFeed[i + 1] == null)
            {
                continue;
            }

            killFeed[i].text = killFeed[i + 1].text;
            killFeed[i].gameObject.SetActive(killFeed[i + 1].gameObject.activeSelf);
            killFeedExpiry[i] = killFeedExpiry[i + 1];
        }

        int last = killFeed.Count - 1;
        if (killFeed[last] == null)
        {
            return;
        }

        string color = ColorUtility.ToHtmlStringRGB(TeamColor(killerTeam));
        string mark = headshot ? " <size=80%>[HS]</size>" : string.Empty;
        killFeed[last].text = $"<color=#{color}>{killer}</color>  <size=75%>{weapon}</size>{mark}  {victim}";
        killFeed[last].gameObject.SetActive(true);
        killFeedExpiry[last] = Time.time + killFeedSeconds;
    }

    private Color TeamColor(Team team) => TeamPalette.For(team);

    private void SetBanner(string headline, string sub)
    {
        if (bannerText != null)
        {
            bannerText.text = headline;
        }

        if (subBannerText != null)
        {
            subBannerText.text = sub;
        }
    }
}
