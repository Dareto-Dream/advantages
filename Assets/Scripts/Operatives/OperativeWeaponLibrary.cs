using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "OperativeWeaponLibrary", menuName = "Advantage/Operative Weapon Library")]
public class OperativeWeaponLibrary : ScriptableObject
{
    public const string ResourcePath = "OperativeWeaponLibrary";

    [System.Serializable]
    public class Entry
    {
        public OperativeId operative;
        public WeaponDefinition primary;

        [Tooltip("Optional off-hand. When set it takes slot 2 instead of the shared sidearm.")]
        public WeaponDefinition secondary;
    }

    [Tooltip("Shared backup weapon added as slot 2 for every operative.")]
    [SerializeField] private WeaponDefinition sidearm;
    [SerializeField] private List<Entry> entries = new List<Entry>();

    private static OperativeWeaponLibrary cached;

    public static OperativeWeaponLibrary Instance =>
        cached != null ? cached : (cached = Resources.Load<OperativeWeaponLibrary>(ResourcePath));

    public List<WeaponDefinition> ResolveFor(OperativeId id)
    {
        List<WeaponDefinition> result = new List<WeaponDefinition>();
        WeaponDefinition offHand = null;

        foreach (Entry entry in entries)
        {
            if (entry.operative == id && entry.primary != null)
            {
                result.Add(entry.primary);
                offHand = entry.secondary;
                break;
            }
        }

        WeaponDefinition slotTwo = offHand != null ? offHand : sidearm;
        if (slotTwo != null)
        {
            result.Add(slotTwo);
        }

        return result;
    }

    public static List<WeaponDefinition> WeaponsFor(OperativeId id)
    {
        OperativeWeaponLibrary library = Instance;
        return library != null ? library.ResolveFor(id) : null;
    }

    public void Configure(WeaponDefinition sidearmWeapon, List<Entry> newEntries)
    {
        sidearm = sidearmWeapon;
        entries = newEntries;
    }
}
