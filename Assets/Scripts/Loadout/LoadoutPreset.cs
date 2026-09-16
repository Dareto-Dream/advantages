using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LDT_", menuName = "Advantage/Loadout Preset")]
public class LoadoutPreset : ScriptableObject
{
    public LoadoutRole role = LoadoutRole.Support;
    public string displayName = "Support";
    [TextArea(2, 4)] public string blurb = "";
    public Color accentColor = Color.white;
    public List<WeaponDefinition> weapons = new List<WeaponDefinition>();

    [Header("Stat Overrides")]
    public bool overrideStats = false;
    public LoadoutStats stats;

    public LoadoutStats ResolveStats()
    {
        return overrideStats ? stats : LoadoutStats.For(role);
    }
}
