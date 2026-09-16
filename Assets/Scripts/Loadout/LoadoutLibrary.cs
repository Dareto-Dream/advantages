using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "LoadoutLibrary", menuName = "Advantage/Loadout Library")]
public class LoadoutLibrary : ScriptableObject
{
    public const string ResourcePath = "LoadoutLibrary";

    [SerializeField] private List<LoadoutPreset> presets = new List<LoadoutPreset>();

    private static LoadoutLibrary cached;

    public IReadOnlyList<LoadoutPreset> Presets => presets;

    public static LoadoutLibrary Instance
    {
        get
        {
            if (cached == null)
            {
                cached = Resources.Load<LoadoutLibrary>(ResourcePath);
            }

            return cached;
        }
    }

    public LoadoutPreset Get(LoadoutRole role)
    {
        foreach (LoadoutPreset preset in presets)
        {
            if (preset != null && preset.role == role)
            {
                return preset;
            }
        }

        return presets.Count > 0 ? presets[0] : null;
    }

    public static LoadoutPreset Resolve(LoadoutRole role)
    {
        LoadoutLibrary library = Instance;
        return library != null ? library.Get(role) : null;
    }

    public void SetPresets(List<LoadoutPreset> newPresets)
    {
        presets = newPresets;
    }
}
