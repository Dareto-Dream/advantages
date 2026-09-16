using System;
using UnityEngine;
using Slot = OperativeDefinition.AbilitySlot;

public static class OperativeRoster
{
    private static Slot E(Type type, string name) => new Slot(Ability.Slot.Primary, type, name, "E");
    private static Slot Shift(Type type, string name) => new Slot(Ability.Slot.Secondary, type, name, "SHIFT");
    private static Slot F(Type type, string name) => new Slot(Ability.Slot.Special, type, name, "F");
    private static Slot Rmb(Type type, string name) => new Slot(Ability.Slot.Special, type, name, "RMB");
    private static Slot Space(Type type, string name) => new Slot(Ability.Slot.Special, type, name, "SPACE");
    private static Slot Q(Type type, string name) => new Slot(Ability.Slot.Ultimate, type, name, "Q");

    public static readonly OperativeDefinition[] All =
    {
        new OperativeDefinition(
            OperativeId.Bulwark, HeroRole.Tank, LoadoutRole.Heavy, "WPN_AegisLMG",
            850f, 0f, 4.0f, false, 75f, new Color(0.33f, 0.56f, 0.85f),
            E(typeof(BarricadeAbility), "Barricade"),
            Shift(typeof(RootAbility), "Root"),
            Rmb(typeof(WrathAbility), "Wrath"),
            Q(typeof(FortressAbility), "Fortress")),

        new OperativeDefinition(
            OperativeId.Rook, HeroRole.Tank, LoadoutRole.Heavy, "WPN_BreachScattergun",
            650f, 0f, 4.8f, false, 70f, new Color(0.74f, 0.42f, 0.30f),
            E(typeof(DemolishAbility), "Demolish"),
            Shift(typeof(ChargeAbility), "Charge"),
            F(typeof(CaptureAbility), "Capture"),
            Q(typeof(CheckmateAbility), "Checkmate")),

        new OperativeDefinition(
            OperativeId.Viper, HeroRole.Dps, LoadoutRole.Support, "WPN_LongshotDMR",
            250f, 0f, 6.8f, false, 85f, new Color(0.85f, 0.30f, 0.36f),
            E(typeof(SpotterAbility), "Spotter Drone"),
            Shift(typeof(ViperDashAbility), "Dash"),
            Space(typeof(NewHeightsAbility), "New Heights"),
            Q(typeof(DeadeyeAbility), "Deadeye Protocol")),

        new OperativeDefinition(
            OperativeId.Rush, HeroRole.Dps, LoadoutRole.Light, "WPN_TwinVipers",
            200f, 0f, 8.0f, false, 70f, new Color(0.95f, 0.76f, 0.26f),
            E(typeof(BurstAbility), "Burst"),
            Shift(typeof(BlinkDashAbility), "Blink Dash"),
            F(typeof(RevertAbility), "Revert"),
            Q(typeof(OverdriveAbility), "Overdrive")),

        new OperativeDefinition(
            OperativeId.Forge, HeroRole.Dps, LoadoutRole.Support, "WPN_ArcLauncher",
            200f, 0f, 6.0f, false, 80f, new Color(0.90f, 0.55f, 0.20f),
            E(typeof(StickyMineAbility), "Sticky Mine"),
            Shift(typeof(GotchaAbility), "Gotcha"),
            Rmb(typeof(MolotovAbility), "Molotov"),
            Q(typeof(MeltdownAbility), "Meltdown")),

        new OperativeDefinition(
            OperativeId.Cipher, HeroRole.Dps, LoadoutRole.Light, "WPN_WhisperAR",
            150f, 0f, 6.8f, false, 80f, new Color(0.56f, 0.46f, 0.95f),
            E(typeof(ReconScanAbility), "Recon Scan"),
            Shift(typeof(DeceptionAbility), "Deception"),
            F(typeof(AllSeeingAbility), "All-seeing"),
            Q(typeof(BlackoutAbility), "Blackout")),

        new OperativeDefinition(
            OperativeId.Medica, HeroRole.Support, LoadoutRole.Support, "WPN_MenderSMG",
            275f, 0f, 6.6f, false, 80f, new Color(0.30f, 0.80f, 0.55f),
            E(typeof(HealBurstAbility), "Heal Burst"),
            Shift(typeof(SecondChanceAbility), "Second Chance"),
            Rmb(typeof(UsTogetherAbility), "Us Together"),
            Q(typeof(MassTransfusionAbility), "Mass Transfusion")),

        new OperativeDefinition(
            OperativeId.Relay, HeroRole.Support, LoadoutRole.Support, "WPN_HealingBeams",
            200f, 0f, 6.5f, true, 80f, new Color(0.26f, 0.80f, 0.80f),
            E(typeof(HasteFieldAbility), "Haste Field"),
            Shift(typeof(RepairAbility), "Repair"),
            Rmb(typeof(GuardAbility), "Guard"),
            Q(typeof(NetworkAbility), "Network")),

        new OperativeDefinition(
            OperativeId.Warden, HeroRole.Support, LoadoutRole.Support, "WPN_AegisCarbine",
            225f, 0f, 6.6f, false, 85f, new Color(0.76f, 0.80f, 0.30f),
            E(typeof(LifeorbAbility), "Lifeorb"),
            Shift(typeof(OrbitAbility), "Orbit"),
            Rmb(typeof(TetherorbAbility), "Tetherorb"),
            Q(typeof(OrbStormAbility), "Orb Storm"))
    };

    public static OperativeDefinition Get(OperativeId id)
    {
        int index = (int)id;
        return index >= 0 && index < All.Length ? All[index] : All[0];
    }

    public static OperativeDefinition FromHeroIndex(int heroIndex)
    {
        return All[Mathf.Clamp(heroIndex, 0, All.Length - 1)];
    }

    public static int HeroIndexOf(OperativeId id)
    {
        int index = (int)id;
        return index >= 0 && index < All.Length ? index : 0;
    }
}
