using UnityEngine;

public static class HeroRoster
{
    public static readonly HeroDefinition[] All =
    {
        new HeroDefinition(
            "Bulwark", HeroRole.Tank, "Fortress", "Heavy automatic cannon",
            "Walking fortress. Very high HP, a deployable frontal barrier, and can anchor himself to the ground to become extremely difficult to move. Slow but powerful - creates safe space for the team.",
            "Fortress - plants himself and projects a huge protective field around nearby allies.",
            "The tank you pick when you want to hold a position.",
            new Color(0.33f, 0.56f, 0.85f)),
        new HeroDefinition(
            "Rook", HeroRole.Tank, "Bruiser", "Breach Scattergun + Iron Knuckles",
            "Aggressive initiator. Lower durability than Bulwark but extremely high mobility - leaps and charges into enemies with knockback and displacement, and gets stronger while fighting multiple enemies.",
            "Checkmate - locks onto several nearby enemies and violently launches himself between them.",
            "Protects the team by making the enemy deal with him instead.",
            new Color(0.74f, 0.42f, 0.30f)),
        new HeroDefinition(
            "Viper", HeroRole.Dps, "Marksman", "Longshot DMR",
            "Long range precision. Headshots are extremely rewarding and a deployable spotter drone helps line them up. A short dash and a mid-air jump let her reposition off a peek.",
            "Deadeye Protocol - briefly reveals every enemy on the map and massively increases weapon damage.",
            "Your dedicated sniper/marksman.",
            new Color(0.85f, 0.30f, 0.36f)),
        new HeroDefinition(
            "Rush", HeroRole.Dps, "Flanker", "Twin Vipers",
            "Extremely fast. Blink-dashes into isolated targets, throws a healing/damaging nova, and can rewind himself 3 seconds to undo a bad fight. Weak against grouped enemies.",
            "Overdrive - a huge temporary movement-speed increase with instant dash resets.",
            "Gets behind the enemy and makes their supports miserable.",
            new Color(0.95f, 0.76f, 0.26f)),
        new HeroDefinition(
            "Forge", HeroRole.Dps, "Area Control", "Arc Launcher",
            "Explosive projectiles and sticky mines that block routes with incendiary or energy fields. Poor close-range survivability - he denies space rather than dueling in it.",
            "Meltdown - saturates a large area with explosives over several seconds.",
            "Makes parts of the map dangerous to stand in.",
            new Color(0.90f, 0.55f, 0.20f)),
        new HeroDefinition(
            "Cipher", HeroRole.Dps, "Tactical", "Whisper AR",
            "Recon and deception. Reveals enemy health for herself, drops motion detectors for the team, and spawns a decoy clone that mirrors her movement and abilities.",
            "Blackout - an area that outlines enemies, hard-locks their abilities and slightly slows them.",
            "Wins fights through information and disruption rather than raw damage.",
            new Color(0.56f, 0.46f, 0.95f)),
        new HeroDefinition(
            "Medica", HeroRole.Support, "Medic", "Mender SMG",
            "Heals allies just by hitting them, throws a burst-heal bubble, links healing tethers to the team, and can revive the last ally to fall. Weak offensively.",
            "Mass Transfusion - rapidly heals nearby allies and grants temporary overheal.",
            "The classic keep-everyone-alive support.",
            new Color(0.30f, 0.80f, 0.55f)),
        new HeroDefinition(
            "Relay", HeroRole.Support, "Enabler", "Healing Beams",
            "Flies. Heals with an infinite beam and makes the team better rather than tankier - speed and damage fields, armour guards, and modest self-repair.",
            "Network - links the whole team, sharing speed, damage and mitigation buffs regardless of position.",
            "The support who makes a coordinated team explode into action.",
            new Color(0.26f, 0.80f, 0.80f)),
        new HeroDefinition(
            "Warden", HeroRole.Support, "Orb Weaver", "Aegis Carbine + orbs",
            "Every point of healing comes from orbs - lobbed Lifeorbs, a defensive Orbit, a sticky Tetherorb, and an ultimate that rains seeking heal-orbs on the wounded.",
            "Orb Storm - continuously emits seeking Lifeorbs that home onto injured allies (and sting enemies caught in the spiral).",
            "\"You aren't killing my team here.\"",
            new Color(0.76f, 0.80f, 0.30f))
    };

    public static HeroDefinition Get(int index)
    {
        if (All.Length == 0)
        {
            return default;
        }

        return All[Mathf.Clamp(index, 0, All.Length - 1)];
    }

    public static string RoleLabel(HeroRole role)
    {
        switch (role)
        {
            case HeroRole.Tank: return "TANK";
            case HeroRole.Dps: return "DPS";
            default: return "SUPPORT";
        }
    }
}
