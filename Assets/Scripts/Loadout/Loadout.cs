using UnityEngine;

public class Loadout : MonoBehaviour
{
    [Header("Preset")]
    [SerializeField] private LoadoutRole role = LoadoutRole.Support;
    [SerializeField] private bool applyBodyScale = true;
    [SerializeField] private Transform bodyRoot;

    [Header("Targets")]
    [SerializeField] private movement mover;
    [SerializeField] private Health health;
    [SerializeField] private WeaponController weapons;

    private LoadoutStats stats;
    private LoadoutPreset preset;

    public LoadoutRole Role => role;
    public LoadoutStats Stats => stats;
    public LoadoutPreset Preset => preset;

    public Transform BodyRoot => bodyRoot != null ? bodyRoot : transform;

    private void Awake()
    {
        if (mover == null)
        {
            mover = GetComponent<movement>();
        }

        if (health == null)
        {
            health = GetComponent<Health>();
        }

        if (weapons == null)
        {
            weapons = GetComponentInChildren<WeaponController>();
        }

        Apply();
    }

    public void SetRole(LoadoutRole newRole)
    {
        role = newRole;
        Apply();
    }

    [ContextMenu("Apply Preset")]
    public void Apply()
    {
        preset = LoadoutLibrary.Resolve(role);
        stats = preset != null ? preset.ResolveStats() : LoadoutStats.For(role);

        if (applyBodyScale)
        {
            Transform target = bodyRoot != null ? bodyRoot : transform;
            target.localScale = stats.bodyScale;
        }

        if (mover != null)
        {
            mover.ApplyLoadout(stats);
        }

        if (health != null)
        {
            health.ConfigurePools(stats.maxHealth, stats.maxArmor);
        }

        if (weapons != null && preset != null && preset.weapons.Count > 0)
        {
            weapons.SetWeapons(preset.weapons);
        }
    }
}
