using System;
using UnityEngine;

public sealed class OperativeDefinition
{

    public struct AbilitySlot
    {
        public Ability.Slot slot;
        public Type type;
        public string name;
        public string key;

        public AbilitySlot(Ability.Slot slot, Type type, string name, string key)
        {
            this.slot = slot;
            this.type = type;
            this.name = name;
            this.key = key;
        }
    }

    public OperativeId id;
    public string name;
    public HeroRole role;

    public LoadoutRole baseRole;

    public float maxHealth = 100f;
    public float maxArmor = 0f;
    public float moveSpeed = 6f;

    public bool flight;

    public float bodyScale => OperativeBody.ScaleFor(role);

    public string primaryWeapon;

    public AbilitySlot[] abilities;

    public float ultChargeSeconds;

    public Color color;

    public string nativeName
    {
        get
        {
            foreach (AbilitySlot ability in abilities)
            {
                if (ability.slot != Ability.Slot.Ultimate)
                {
                    return ability.name;
                }
            }

            return abilities.Length > 0 ? abilities[0].name : string.Empty;
        }
    }

    public string ultimateName
    {
        get
        {
            foreach (AbilitySlot ability in abilities)
            {
                if (ability.slot == Ability.Slot.Ultimate)
                {
                    return ability.name;
                }
            }

            return abilities.Length > 0 ? abilities[abilities.Length - 1].name : string.Empty;
        }
    }

    public OperativeDefinition(
        OperativeId id,
        HeroRole role,
        LoadoutRole baseRole,
        string primaryWeapon,
        float maxHealth,
        float maxArmor,
        float moveSpeed,
        bool flight,
        float ultChargeSeconds,
        Color color,
        params AbilitySlot[] abilities)
    {
        this.id = id;
        this.name = id.ToString();
        this.role = role;
        this.baseRole = baseRole;
        this.primaryWeapon = primaryWeapon;
        this.maxHealth = maxHealth;
        this.maxArmor = maxArmor;
        this.moveSpeed = moveSpeed;
        this.flight = flight;
        this.ultChargeSeconds = ultChargeSeconds;
        this.color = color;
        this.abilities = abilities;
    }
}
