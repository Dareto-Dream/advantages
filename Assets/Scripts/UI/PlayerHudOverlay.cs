using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class PlayerHudOverlay : MonoBehaviour
{
    private class WeaponRow
    {
        public GameObject root;
        public Image icon;
        public TextMeshProUGUI slot;
        public TextMeshProUGUI ammo;
        public Image reloadArc;
        public WeaponDefinition weapon;
    }

    private const int MaxWeaponRows = 3;
    private const float RowPitch = 84f;

    private static readonly Color Ink = new Color(0.93f, 0.95f, 0.98f, 1f);
    private static readonly Color Dim = new Color(0.72f, 0.78f, 0.86f, 0.45f);
    private static readonly Color Frame = new Color(0.86f, 0.91f, 0.97f, 0.65f);
    private static readonly Color TrackColor = new Color(0f, 0f, 0f, 0.55f);
    private static readonly Color HealthColor = new Color(0.24f, 0.85f, 0.72f, 1f);
    private static readonly Color LowColor = new Color(0.95f, 0.28f, 0.32f, 1f);
    private static readonly Color ArmorColor = new Color(0.72f, 0.82f, 0.95f, 0.95f);

    private PlayerController player;
    private GameObject canvasRoot;

    private Image healthFill;
    private Image armorFill;
    private GameObject armorTrack;
    private TextMeshProUGUI healthText;

    private Image portrait;
    private readonly List<WeaponRow> rows = new List<WeaponRow>();

    private void Start()
    {
        Build();
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

        if (player == null)
        {
            MatchManager match = MatchManager.Instance;
            player = match != null ? match.Player : null;
            if (player == null)
            {
                return;
            }
        }

        UpdateVitals();
        UpdateWeapons();
    }

    private void UpdateVitals()
    {
        Health health = player.Health;
        if (health == null)
        {
            return;
        }

        float fraction = health.HealthFraction;
        healthFill.fillAmount = fraction;
        healthFill.color = fraction < 0.3f ? LowColor : HealthColor;

        healthText.text = $"{Mathf.CeilToInt(health.CurrentHealth)}<alpha=#77>/{Mathf.CeilToInt(health.MaxHealth)}";

        bool hasArmor = health.MaxArmor > 0f;
        if (armorTrack.activeSelf != hasArmor)
        {
            armorTrack.SetActive(hasArmor);
        }

        if (hasArmor)
        {
            armorFill.fillAmount = health.ArmorFraction;
        }

        if (portrait != null)
        {
            portrait.color = player.IsDead ? Dim : Ink;
        }
    }

    private void UpdateWeapons()
    {
        WeaponController weapons = player.Weapons;
        if (weapons == null)
        {
            return;
        }

        IReadOnlyList<WeaponDefinition> carried = weapons.Weapons;
        int equipped = weapons.EquippedIndex;

        for (int i = 0; i < rows.Count; i++)
        {
            WeaponRow row = rows[i];
            bool used = carried != null && i < carried.Count && carried[i] != null;

            if (row.root.activeSelf != used)
            {
                row.root.SetActive(used);
            }

            if (!used)
            {
                continue;
            }

            WeaponDefinition weapon = carried[i];
            if (row.weapon != weapon)
            {
                row.weapon = weapon;
                row.icon.sprite = HudArt.Get(SymbolFor(weapon));
            }

            bool active = i == equipped;
            row.icon.color = active ? Ink : Dim;
            row.slot.color = active ? Ink : Dim;
            row.ammo.color = active ? Ink : Dim;

            if (weapon.infiniteAmmo)
            {
                row.ammo.text = "∞";
            }
            else
            {
                int mag = weapons.MagazineFor(weapon);
                row.ammo.text = $"{mag}<alpha=#77>/{weapon.magazineSize}";
            }

            bool reloading = active && weapons.IsReloading;
            if (row.reloadArc.enabled != reloading)
            {
                row.reloadArc.enabled = reloading;
            }

            if (reloading)
            {
                row.reloadArc.fillAmount = weapons.ReloadProgress;
            }
            else if (active && weapons.UsesMagazine && weapons.MagazineAmmo == 0)
            {

                row.ammo.color = LowColor;
            }
        }
    }

    private static HudSymbol SymbolFor(WeaponDefinition weapon)
    {
        if (weapon == null)
        {
            return HudSymbol.WeaponPistol;
        }

        return weapon.magazineSize > 0 && weapon.magazineSize <= 14 ? HudSymbol.WeaponPistol : HudSymbol.WeaponSmg;
    }

    private void Build()
    {
        Canvas canvas = HudBuild.Canvas(transform, "PlayerHudCanvas", 48);
        canvasRoot = canvas.gameObject;

        BuildPortraitAndWeapons(canvas.transform);
        BuildHealth(canvas.transform);
    }

    private void BuildPortraitAndWeapons(Transform parent)
    {
        RectTransform root = HudBuild.Rect(parent, "Vitals", new Vector2(0f, 0f), new Vector2(38f, 22f), new Vector2(660f, 220f));

        RectTransform plate = HudBuild.Rect(root, "Portrait", new Vector2(0f, 0f), new Vector2(0f, 8f), new Vector2(178f, 160f));
        Image frame = HudBuild.Glyph(plate, "Frame", HudSymbol.Parallelogram, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(178f, 160f), Frame);
        frame.preserveAspect = false;
        portrait = HudBuild.Glyph(plate, "Figure", HudSymbol.Person, new Vector2(0.5f, 0.5f), new Vector2(0f, -2f), new Vector2(80f, 80f), Ink);

        for (int i = 0; i < MaxWeaponRows; i++)
        {
            rows.Add(BuildWeaponRow(root, i));
        }
    }

    private WeaponRow BuildWeaponRow(Transform parent, int index)
    {

        float y = 130f - index * RowPitch;
        RectTransform row = HudBuild.Rect(parent, $"Weapon{index}", new Vector2(0f, 0f), new Vector2(196f, y), new Vector2(460f, 72f));

        TextMeshProUGUI slot = HudBuild.Text(row, "Slot", new Vector2(0f, 0.5f), new Vector2(0f, 0f), new Vector2(28f, 40f), 24f, TextAlignmentOptions.Left, Ink);
        slot.text = (index + 1).ToString();
        slot.fontStyle = FontStyles.Bold;

        Image icon = HudBuild.Glyph(row, "Icon", HudSymbol.WeaponSmg, new Vector2(0f, 0.5f), new Vector2(34f, 0f), new Vector2(152f, 66f), Ink);

        TextMeshProUGUI ammo = HudBuild.Text(row, "Ammo", new Vector2(0f, 0.5f), new Vector2(202f, 0f), new Vector2(200f, 46f), 32f, TextAlignmentOptions.Left, Ink);
        ammo.text = "0/0";

        Image reloadArc = HudBuild.Glyph(row, "Reload", HudSymbol.Ring, new Vector2(0f, 0.5f), new Vector2(172f, 0f), new Vector2(30f, 30f), Ink);
        HudBuild.AsFill(reloadArc, Image.FillMethod.Radial360, (int)Image.Origin360.Top);
        reloadArc.fillClockwise = true;
        reloadArc.enabled = false;

        WeaponRow built = new WeaponRow { root = row.gameObject, icon = icon, slot = slot, ammo = ammo, reloadArc = reloadArc };
        row.gameObject.SetActive(false);
        return built;
    }

    private void BuildHealth(Transform parent)
    {
        RectTransform root = HudBuild.Rect(parent, "Health", new Vector2(0.5f, 0f), new Vector2(0f, 34f), new Vector2(720f, 110f));

        RectTransform track = HudBuild.Rect(root, "Track", new Vector2(0.5f, 0f), new Vector2(0f, 52f), new Vector2(720f, 34f));
        HudBuild.Block(track, "Border", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(720f, 34f), Frame);
        HudBuild.Block(track, "Well", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(714f, 28f), TrackColor);

        RectTransform fillArea = HudBuild.Rect(track, "FillArea", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(708f, 22f));
        healthFill = HudBuild.Block(fillArea, "Fill", new Vector2(0f, 0.5f), Vector2.zero, new Vector2(708f, 22f), HealthColor);
        healthFill.rectTransform.anchorMin = new Vector2(0f, 0f);
        healthFill.rectTransform.anchorMax = new Vector2(1f, 1f);
        healthFill.rectTransform.offsetMin = Vector2.zero;
        healthFill.rectTransform.offsetMax = Vector2.zero;
        HudBuild.AsFill(healthFill, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);
        healthFill.fillAmount = 1f;

        RectTransform armor = HudBuild.Rect(root, "Armor", new Vector2(0.5f, 0f), new Vector2(0f, 92f), new Vector2(720f, 10f));
        armorTrack = armor.gameObject;
        HudBuild.Block(armor, "Well", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(714f, 10f), TrackColor);
        armorFill = HudBuild.Block(armor, "Fill", new Vector2(0f, 0.5f), new Vector2(3f, 0f), new Vector2(714f, 8f), ArmorColor);
        armorFill.rectTransform.pivot = new Vector2(0f, 0.5f);
        HudBuild.AsFill(armorFill, Image.FillMethod.Horizontal, (int)Image.OriginHorizontal.Left);
        armorFill.fillAmount = 1f;
        armorTrack.SetActive(false);

        healthText = HudBuild.Text(root, "Value", new Vector2(0.5f, 0f), new Vector2(0f, 8f), new Vector2(520f, 44f), 34f, TextAlignmentOptions.Center, Ink);
        healthText.fontStyle = FontStyles.Bold;
        healthText.text = "0/0";
    }
}
