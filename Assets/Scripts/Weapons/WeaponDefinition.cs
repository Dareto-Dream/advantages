using UnityEngine;

[CreateAssetMenu(fileName = "WPN_", menuName = "Advantage/Weapon Definition")]
public class WeaponDefinition : ScriptableObject
{
    [Header("Identity")]
    public string displayName = "Rifle";
    public LoadoutRole preferredRole = LoadoutRole.Support;

    [Header("Damage")]
    [Min(1f)] public float damage = 22f;
    [Min(1f)] public float headshotMultiplier = 2f;
    [Range(0f, 1f)] public float limbMultiplier = 0.85f;
    [Min(1f)] public float range = 120f;
    [Tooltip("Damage multiplier at max range. 1 = no falloff.")]
    [Range(0.1f, 1f)] public float falloffAtMaxRange = 0.55f;
    [Tooltip("Distance at which falloff starts.")]
    [Min(1f)] public float falloffStart = 30f;

    [Header("Fire")]
    [Min(1)] public int pelletsPerShot = 1;
    [Min(30f)] public float roundsPerMinute = 620f;
    public bool automatic = true;

    [Header("Ammo")]
    [Min(1)] public int magazineSize = 30;
    [Tooltip("Unused - reserve ammo is unlimited for every operative; a reload always fills the mag.")]
    [Min(0)] public int reserveAmmo = 120;
    [Min(0.2f)] public float reloadSeconds = 2.1f;
    [Tooltip("Beam weapons: never runs dry, never reloads, HUD shows infinity.")]
    public bool infiniteAmmo = false;

    [Header("Support")]
    [Tooltip("HP restored to an allied combatant this shot hits. 0 = a normal weapon.")]
    [Min(0f)] public float healPerHit = 0f;

    [Header("Accuracy")]
    [Tooltip("Cone half-angle while hip firing, in degrees.")]
    [Range(0f, 12f)] public float hipSpreadDegrees = 2.2f;
    [Tooltip("Cone half-angle while aiming, in degrees.")]
    [Range(0f, 12f)] public float adsSpreadDegrees = 0.35f;
    [Tooltip("Extra spread added per shot while holding the trigger.")]
    [Range(0f, 4f)] public float spreadPerShot = 0.45f;
    [Range(0f, 12f)] public float maxBloom = 3.5f;
    [Tooltip("Degrees of bloom recovered per second.")]
    [Range(0.5f, 30f)] public float bloomRecovery = 7f;
    [Tooltip("Spread multiplier while airborne. Keeps bhop players honest.")]
    [Range(1f, 5f)] public float airborneSpreadMultiplier = 2.4f;

    [Header("Recoil (degrees per shot)")]
    public float recoilUp = 0.55f;
    public float recoilSide = 0.25f;
    [Tooltip("How fast the view returns to where the player was aiming.")]
    public float recoilRecovery = 9f;

    [Header("Aim Down Sights")]
    [Range(20f, 90f)] public float adsFieldOfView = 48f;
    [Min(0.02f)] public float adsSeconds = 0.14f;
    [Range(0.2f, 1f)] public float adsMoveSpeedMultiplier = 0.6f;

    [Header("Reticle")]
    [Tooltip("Hip-fire crosshair shape. Scoped/reflex optics override this while aiming.")]
    public CrosshairStyle crosshairStyle = CrosshairStyle.Cross;
    [Tooltip("Centre gap of the crosshair arms, in reference pixels, before spread is added.")]
    [Min(0f)] public float crosshairBaseGap = 6f;
    [Tooltip("Reference pixels the crosshair opens per degree of current spread.")]
    [Min(0f)] public float crosshairSpreadScale = 26f;
    public bool crosshairShowDot = false;
    public Color crosshairColor = new Color(0.93f, 0.95f, 0.98f, 1f);

    [Header("Optic")]
    [Tooltip("Iron = plain ADS. Reflex = clean dot while aiming. Scoped = full-screen scope overlay, " +
        "crosshair hidden while aiming (pair with a low adsFieldOfView).")]
    public OpticType optic = OpticType.Iron;

    [Header("Feel")]
    public float impactForce = 12f;
    public float screenShake = 0.35f;
    public Color tracerColor = new Color(1f, 0.85f, 0.35f, 1f);

    public float SecondsBetweenShots => 60f / Mathf.Max(1f, roundsPerMinute);

    public float ZoneMultiplier(Hitbox.Zone zone)
    {
        switch (zone)
        {
            case Hitbox.Zone.Head: return headshotMultiplier;
            case Hitbox.Zone.Limb: return limbMultiplier;
            default: return 1f;
        }
    }

    public float DamageAtDistance(float distance)
    {
        if (distance <= falloffStart || range <= falloffStart)
        {
            return damage;
        }

        float t = Mathf.InverseLerp(falloffStart, range, distance);
        return damage * Mathf.Lerp(1f, falloffAtMaxRange, t);
    }
}

public enum CrosshairStyle
{
    Cross,
    Dot,
    None
}

public enum OpticType
{
    Iron,
    Reflex,
    Scoped
}
