using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.AI;

public static class AdvantageAssetBuilder
{
    public const string MaterialsFolder = "Assets/Materials";
    public const string WeaponsFolder = "Assets/Data/Weapons";
    public const string LoadoutsFolder = "Assets/Data/Loadouts";
    public const string ResourcesFolder = "Assets/Resources";
    public const string PrefabsFolder = "Assets/Prefabs";

    [MenuItem("Advantage/Build/1 - Import TextMeshPro Essentials", priority = 0)]
    public static void ImportTmpEssentials()
    {
        if (TmpEssentialsPresent())
        {
            Debug.Log("[Advantage] TextMeshPro essentials already imported.");
            return;
        }

        UnityEditor.PackageManager.PackageInfo package =
            UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.ugui/package.json");

        if (package == null)
        {
            Debug.LogError("[Advantage] Could not locate com.unity.ugui to import TMP essentials.");
            return;
        }

        string packagePath = Path.Combine(package.resolvedPath, "Package Resources", "TMP Essential Resources.unitypackage");
        if (!File.Exists(packagePath))
        {
            Debug.LogError($"[Advantage] TMP essentials package not found at {packagePath}");
            return;
        }

        AssetDatabase.ImportPackage(packagePath, false);
        AssetDatabase.Refresh();
        Debug.Log("[Advantage] Imported TextMeshPro essentials.");
    }

    public static bool TmpEssentialsPresent()
    {
        return AssetDatabase.LoadAssetAtPath<Object>("Assets/TextMesh Pro/Resources/TMP Settings.asset") != null;
    }

    [MenuItem("Advantage/Build/2 - Generate Assets and Prefabs", priority = 1)]
    public static void GenerateAll()
    {
        EnsureFolder("Assets/Data");
        EnsureFolder(WeaponsFolder);
        EnsureFolder(LoadoutsFolder);
        EnsureFolder(ResourcesFolder);
        EnsureFolder(PrefabsFolder);
        EnsureFolder(MaterialsFolder);

        Palette palette = BuildMaterials();
        List<WeaponDefinition> weapons = BuildWeapons();
        List<WeaponDefinition> operativeWeapons = BuildOperativeWeapons();
        BuildLoadouts(weapons);
        BuildOperativeWeaponLibrary(operativeWeapons, weapons);

        List<WeaponDefinition> viewmodelWeapons = new List<WeaponDefinition>(weapons);
        viewmodelWeapons.AddRange(operativeWeapons);
        BuildPlayerPrefab(palette, viewmodelWeapons);
        BuildBotPrefab(palette);

        AssetDatabase.SaveAssets();
        AssetDatabase.Refresh();
        Debug.Log("[Advantage] Assets and prefabs generated.");
    }

    public class Palette
    {
        public Material ground;
        public Material wall;
        public Material cover;
        public Material attacker;
        public Material defender;
        public Material gunBody;
        public Material gunAccent;
        public Material siteMarker;

        public Material panelFriendly;

        public Material panelHostile;
    }

    public static Palette BuildMaterials()
    {
        return new Palette
        {
            ground = Material("M_Ground", new Color(0.20f, 0.21f, 0.24f), 0.85f),
            wall = Material("M_Wall", new Color(0.30f, 0.32f, 0.36f), 0.75f),
            cover = Material("M_Cover", new Color(0.42f, 0.40f, 0.36f), 0.7f),
            attacker = Material("M_Loadout_Attacker", new Color(0.95f, 0.45f, 0.18f), 0.55f),
            defender = Material("M_Loadout_Defender", new Color(0.22f, 0.55f, 0.95f), 0.55f),
            gunBody = Material("M_GunBody", new Color(0.10f, 0.11f, 0.13f), 0.45f),
            gunAccent = Material("M_GunAccent", new Color(0.18f, 0.80f, 0.68f), 0.35f),
            siteMarker = Material("M_Site", new Color(0.95f, 0.80f, 0.25f), 0.8f),
            panelFriendly = TransparentMaterial("M_SpawnPanel_Friendly", new Color(0.24f, 0.95f, 0.44f, 0.42f)),
            panelHostile = TransparentMaterial("M_SpawnPanel_Hostile", new Color(0.98f, 0.26f, 0.28f, 0.42f))
        };
    }

    private static Material Material(string name, Color color, float smoothness)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", 1f - smoothness);
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    private static Material TransparentMaterial(string name, Color color)
    {
        string path = $"{MaterialsFolder}/{name}.mat";
        Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

        if (material == null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            material = new Material(shader);
            AssetDatabase.CreateAsset(material, path);
        }

        material.color = color;

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", color);
        }

        if (material.HasProperty("_Surface"))
        {
            material.SetFloat("_Surface", 1f);
            material.SetFloat("_Blend", 0f);
            material.SetFloat("_SrcBlend", (float)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetFloat("_DstBlend", (float)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetFloat("_ZWrite", 0f);
            material.SetFloat("_AlphaClip", 0f);
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHAMODULATE_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }
        else if (material.HasProperty("_Mode"))
        {

            material.SetFloat("_Mode", 3f);
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
            material.SetInt("_ZWrite", 0);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;
        }

        EditorUtility.SetDirty(material);
        return material;
    }

    public static List<WeaponDefinition> BuildWeapons()
    {
        List<WeaponDefinition> weapons = new List<WeaponDefinition>
        {
            Weapon("WPN_SMG", weapon =>
            {
                weapon.displayName = "Vector SMG";
                weapon.preferredRole = LoadoutRole.Light;
                weapon.damage = 15f;
                weapon.headshotMultiplier = 1.8f;
                weapon.range = 70f;
                weapon.falloffStart = 16f;
                weapon.falloffAtMaxRange = 0.5f;
                weapon.roundsPerMinute = 900f;
                weapon.automatic = true;
                weapon.magazineSize = 32;
                weapon.reserveAmmo = 160;
                weapon.reloadSeconds = 1.7f;
                weapon.hipSpreadDegrees = 2.6f;
                weapon.adsSpreadDegrees = 0.9f;
                weapon.spreadPerShot = 0.32f;
                weapon.maxBloom = 3.6f;
                weapon.bloomRecovery = 9f;
                weapon.recoilUp = 0.32f;
                weapon.recoilSide = 0.2f;
                weapon.adsFieldOfView = 56f;
                weapon.adsSeconds = 0.1f;
                weapon.tracerColor = new Color(0.55f, 0.95f, 1f);
            }),

            Weapon("WPN_Rifle", weapon =>
            {
                weapon.displayName = "Vantage Rifle";
                weapon.preferredRole = LoadoutRole.Support;
                weapon.damage = 24f;
                weapon.headshotMultiplier = 2.1f;
                weapon.range = 120f;
                weapon.falloffStart = 34f;
                weapon.falloffAtMaxRange = 0.7f;
                weapon.roundsPerMinute = 620f;
                weapon.automatic = true;
                weapon.magazineSize = 30;
                weapon.reserveAmmo = 120;
                weapon.reloadSeconds = 2.1f;
                weapon.hipSpreadDegrees = 2.2f;
                weapon.adsSpreadDegrees = 0.3f;
                weapon.spreadPerShot = 0.42f;
                weapon.maxBloom = 3.2f;
                weapon.bloomRecovery = 7f;
                weapon.recoilUp = 0.55f;
                weapon.recoilSide = 0.24f;
                weapon.adsFieldOfView = 48f;
                weapon.adsSeconds = 0.14f;
                weapon.tracerColor = new Color(1f, 0.85f, 0.35f);
            }),

            Weapon("WPN_Shotgun", weapon =>
            {
                weapon.displayName = "Breaker 12";
                weapon.preferredRole = LoadoutRole.Heavy;
                weapon.damage = 11f;
                weapon.pelletsPerShot = 9;
                weapon.headshotMultiplier = 1.5f;
                weapon.range = 30f;
                weapon.falloffStart = 6f;
                weapon.falloffAtMaxRange = 0.25f;
                weapon.roundsPerMinute = 95f;
                weapon.automatic = false;
                weapon.magazineSize = 6;
                weapon.reserveAmmo = 36;
                weapon.reloadSeconds = 2.6f;
                weapon.hipSpreadDegrees = 4.5f;
                weapon.adsSpreadDegrees = 3.2f;
                weapon.spreadPerShot = 0.2f;
                weapon.maxBloom = 1.5f;
                weapon.bloomRecovery = 5f;
                weapon.recoilUp = 2.4f;
                weapon.recoilSide = 0.5f;
                weapon.adsFieldOfView = 60f;
                weapon.adsSeconds = 0.18f;
                weapon.impactForce = 24f;
                weapon.tracerColor = new Color(1f, 0.55f, 0.3f);
            }),

            Weapon("WPN_Pistol", weapon =>
            {
                weapon.displayName = "Sidearm";
                weapon.preferredRole = LoadoutRole.Support;
                weapon.damage = 18f;
                weapon.headshotMultiplier = 2.2f;
                weapon.range = 60f;
                weapon.falloffStart = 20f;
                weapon.falloffAtMaxRange = 0.6f;
                weapon.roundsPerMinute = 400f;
                weapon.automatic = false;
                weapon.magazineSize = 12;
                weapon.reserveAmmo = 60;
                weapon.reloadSeconds = 1.4f;
                weapon.hipSpreadDegrees = 1.8f;
                weapon.adsSpreadDegrees = 0.4f;
                weapon.spreadPerShot = 0.5f;
                weapon.maxBloom = 3f;
                weapon.bloomRecovery = 10f;
                weapon.recoilUp = 0.9f;
                weapon.recoilSide = 0.3f;
                weapon.adsFieldOfView = 52f;
                weapon.adsSeconds = 0.11f;
                weapon.tracerColor = new Color(0.9f, 0.9f, 0.95f);
            })
        };

        return weapons;
    }

    public static List<WeaponDefinition> BuildOperativeWeapons()
    {
        return new List<WeaponDefinition>
        {
            Weapon("WPN_AegisLMG", w =>
            {
                w.displayName = "Aegis LMG";
                w.preferredRole = LoadoutRole.Heavy;
                w.damage = 17f; w.headshotMultiplier = 1.7f;
                w.range = 90f; w.falloffStart = 22f; w.falloffAtMaxRange = 0.55f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 520f; w.automatic = true;
                w.magazineSize = 60; w.reserveAmmo = 240; w.reloadSeconds = 3.4f;
                w.hipSpreadDegrees = 3.2f; w.adsSpreadDegrees = 1.2f; w.spreadPerShot = 0.28f;
                w.maxBloom = 4.4f; w.bloomRecovery = 6f;
                w.recoilUp = 0.32f; w.recoilSide = 0.22f; w.recoilRecovery = 8f;
                w.adsFieldOfView = 55f; w.adsSeconds = 0.17f; w.adsMoveSpeedMultiplier = 0.5f;
                w.impactForce = 12f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 11f; w.crosshairSpreadScale = 30f;
                w.crosshairColor = new Color(0.6f, 0.78f, 1f); w.optic = OpticType.Iron;
                w.tracerColor = new Color(0.55f, 0.75f, 1f);
            }),

            Weapon("WPN_BreachScattergun", w =>
            {
                w.displayName = "Breach Scattergun";
                w.preferredRole = LoadoutRole.Heavy;
                w.damage = 9f; w.headshotMultiplier = 1.5f;
                w.range = 30f; w.falloffStart = 6f; w.falloffAtMaxRange = 0.2f;
                w.pelletsPerShot = 10; w.roundsPerMinute = 78f; w.automatic = false;
                w.magazineSize = 6; w.reserveAmmo = 42; w.reloadSeconds = 3.1f;
                w.hipSpreadDegrees = 4.8f; w.adsSpreadDegrees = 3.4f; w.spreadPerShot = 0.2f;
                w.maxBloom = 1.6f; w.bloomRecovery = 5f;
                w.recoilUp = 2.6f; w.recoilSide = 0.5f; w.recoilRecovery = 7f;
                w.adsFieldOfView = 62f; w.adsSeconds = 0.16f; w.adsMoveSpeedMultiplier = 0.6f;
                w.impactForce = 22f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 20f; w.crosshairSpreadScale = 10f;
                w.crosshairColor = new Color(0.95f, 0.7f, 0.5f); w.optic = OpticType.Iron;
                w.tracerColor = new Color(1f, 0.55f, 0.3f);
            }),

            Weapon("WPN_LongshotDMR", w =>
            {
                w.displayName = "Longshot DMR";
                w.preferredRole = LoadoutRole.Support;
                w.damage = 58f; w.headshotMultiplier = 2.5f;
                w.range = 250f; w.falloffStart = 60f; w.falloffAtMaxRange = 0.85f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 55f; w.automatic = false;
                w.magazineSize = 8; w.reserveAmmo = 48; w.reloadSeconds = 2.6f;
                w.hipSpreadDegrees = 3.6f; w.adsSpreadDegrees = 0.12f; w.spreadPerShot = 0.7f;
                w.maxBloom = 3.5f; w.bloomRecovery = 4f;
                w.recoilUp = 1.5f; w.recoilSide = 0.3f; w.recoilRecovery = 6f;
                w.adsFieldOfView = 22f; w.adsSeconds = 0.3f; w.adsMoveSpeedMultiplier = 0.35f;
                w.impactForce = 16f;
                w.crosshairStyle = CrosshairStyle.Dot; w.crosshairBaseGap = 0f; w.crosshairSpreadScale = 20f;
                w.crosshairShowDot = true;
                w.crosshairColor = new Color(0.95f, 0.55f, 0.6f); w.optic = OpticType.Scoped;
                w.tracerColor = new Color(1f, 0.8f, 0.85f);
            }),

            Weapon("WPN_TwinVipers", w =>
            {
                w.displayName = "Twin Vipers";
                w.preferredRole = LoadoutRole.Light;
                w.damage = 11f; w.headshotMultiplier = 1.6f;
                w.range = 55f; w.falloffStart = 12f; w.falloffAtMaxRange = 0.45f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 1080f; w.automatic = true;
                w.magazineSize = 40; w.reserveAmmo = 200; w.reloadSeconds = 1.9f;
                w.hipSpreadDegrees = 3f; w.adsSpreadDegrees = 1.4f; w.spreadPerShot = 0.34f;
                w.maxBloom = 4.2f; w.bloomRecovery = 9f;
                w.recoilUp = 0.3f; w.recoilSide = 0.28f; w.recoilRecovery = 11f;
                w.adsFieldOfView = 58f; w.adsSeconds = 0.09f; w.adsMoveSpeedMultiplier = 0.7f;
                w.impactForce = 8f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 5f; w.crosshairSpreadScale = 24f;
                w.crosshairColor = new Color(1f, 0.92f, 0.6f); w.optic = OpticType.Reflex;
                w.tracerColor = new Color(0.7f, 0.95f, 1f);
            }),

            Weapon("WPN_ArcLauncher", w =>
            {
                w.displayName = "Arc Launcher";
                w.preferredRole = LoadoutRole.Support;
                w.damage = 42f; w.headshotMultiplier = 1.3f;
                w.range = 65f; w.falloffStart = 15f; w.falloffAtMaxRange = 0.6f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 75f; w.automatic = false;
                w.magazineSize = 5; w.reserveAmmo = 30; w.reloadSeconds = 2.8f;
                w.hipSpreadDegrees = 3.4f; w.adsSpreadDegrees = 2f; w.spreadPerShot = 0.4f;
                w.maxBloom = 2.5f; w.bloomRecovery = 5f;
                w.recoilUp = 1.6f; w.recoilSide = 0.35f; w.recoilRecovery = 7f;
                w.adsFieldOfView = 60f; w.adsSeconds = 0.18f; w.adsMoveSpeedMultiplier = 0.6f;
                w.impactForce = 16f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 14f; w.crosshairSpreadScale = 12f;
                w.crosshairColor = new Color(1f, 0.65f, 0.35f); w.optic = OpticType.Iron;
                w.tracerColor = new Color(1f, 0.55f, 0.2f);
            }),

            Weapon("WPN_WhisperAR", w =>
            {
                w.displayName = "Whisper AR";
                w.preferredRole = LoadoutRole.Light;
                w.damage = 20f; w.headshotMultiplier = 2f;
                w.range = 110f; w.falloffStart = 30f; w.falloffAtMaxRange = 0.65f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 640f; w.automatic = true;
                w.magazineSize = 30; w.reserveAmmo = 150; w.reloadSeconds = 2.1f;
                w.hipSpreadDegrees = 2.2f; w.adsSpreadDegrees = 0.4f; w.spreadPerShot = 0.4f;
                w.maxBloom = 3.2f; w.bloomRecovery = 7f;
                w.recoilUp = 0.42f; w.recoilSide = 0.2f; w.recoilRecovery = 9f;
                w.adsFieldOfView = 46f; w.adsSeconds = 0.13f; w.adsMoveSpeedMultiplier = 0.6f;
                w.impactForce = 10f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 6f; w.crosshairSpreadScale = 22f;
                w.crosshairColor = new Color(0.72f, 0.66f, 1f); w.optic = OpticType.Reflex;
                w.tracerColor = new Color(0.5f, 0.42f, 0.75f);
            }),

            Weapon("WPN_MenderSMG", w =>
            {
                w.displayName = "Mender SMG";
                w.preferredRole = LoadoutRole.Support;
                w.damage = 9f; w.healPerHit = 13f; w.headshotMultiplier = 1.6f;
                w.range = 60f; w.falloffStart = 14f; w.falloffAtMaxRange = 0.5f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 780f; w.automatic = true;
                w.magazineSize = 32; w.reserveAmmo = 160; w.reloadSeconds = 1.9f;
                w.hipSpreadDegrees = 2.8f; w.adsSpreadDegrees = 1f; w.spreadPerShot = 0.34f;
                w.maxBloom = 3.6f; w.bloomRecovery = 9f;
                w.recoilUp = 0.3f; w.recoilSide = 0.2f; w.recoilRecovery = 10f;
                w.adsFieldOfView = 55f; w.adsSeconds = 0.1f; w.adsMoveSpeedMultiplier = 0.65f;
                w.impactForce = 6f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 6f; w.crosshairSpreadScale = 24f;
                w.crosshairColor = new Color(0.5f, 0.95f, 0.7f); w.optic = OpticType.Reflex;
                w.tracerColor = new Color(0.4f, 0.95f, 0.7f);
            }),

            Weapon("WPN_PulseCarbine", w =>
            {
                w.displayName = "Pulse Carbine";
                w.preferredRole = LoadoutRole.Support;
                w.damage = 22f; w.headshotMultiplier = 1.9f;
                w.range = 95f; w.falloffStart = 26f; w.falloffAtMaxRange = 0.6f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 460f; w.automatic = true;
                w.magazineSize = 24; w.reserveAmmo = 120; w.reloadSeconds = 2f;
                w.hipSpreadDegrees = 2f; w.adsSpreadDegrees = 0.45f; w.spreadPerShot = 0.45f;
                w.maxBloom = 3f; w.bloomRecovery = 8f;
                w.recoilUp = 0.5f; w.recoilSide = 0.22f; w.recoilRecovery = 9f;
                w.adsFieldOfView = 50f; w.adsSeconds = 0.12f; w.adsMoveSpeedMultiplier = 0.6f;
                w.impactForce = 9f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 6f; w.crosshairSpreadScale = 22f;
                w.crosshairColor = new Color(0.4f, 0.9f, 0.9f); w.optic = OpticType.Reflex;
                w.tracerColor = new Color(0.35f, 0.9f, 0.9f);
            }),

            Weapon("WPN_IronKnuckles", w =>
            {
                w.displayName = "Iron Knuckles";
                w.preferredRole = LoadoutRole.Heavy;
                w.damage = 25f; w.headshotMultiplier = 1f; w.limbMultiplier = 1f;
                w.range = 4f; w.falloffStart = 4f; w.falloffAtMaxRange = 1f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 70f; w.automatic = false;
                w.magazineSize = 1; w.reserveAmmo = 999; w.reloadSeconds = 1f;
                w.hipSpreadDegrees = 0f; w.adsSpreadDegrees = 0f; w.spreadPerShot = 0f;
                w.maxBloom = 0f; w.bloomRecovery = 1f;
                w.recoilUp = 1.2f; w.recoilSide = 0.4f; w.recoilRecovery = 10f;
                w.adsFieldOfView = 65f; w.adsSeconds = 0.12f; w.adsMoveSpeedMultiplier = 0.85f;
                w.impactForce = 20f;
                w.crosshairStyle = CrosshairStyle.Dot; w.crosshairShowDot = true;
                w.crosshairBaseGap = 0f; w.crosshairSpreadScale = 0f;
                w.crosshairColor = new Color(0.95f, 0.7f, 0.5f); w.optic = OpticType.Iron;
                w.tracerColor = new Color(1f, 0.6f, 0.35f);
            }),

            Weapon("WPN_BastionProjector", w =>
            {
                w.displayName = "Bastion Projector";
                w.preferredRole = LoadoutRole.Support;
                w.damage = 24f; w.headshotMultiplier = 1.7f;
                w.range = 80f; w.falloffStart = 20f; w.falloffAtMaxRange = 0.55f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 400f; w.automatic = true;
                w.magazineSize = 20; w.reserveAmmo = 100; w.reloadSeconds = 2.2f;
                w.hipSpreadDegrees = 2.4f; w.adsSpreadDegrees = 0.8f; w.spreadPerShot = 0.4f;
                w.maxBloom = 3f; w.bloomRecovery = 8f;
                w.recoilUp = 0.45f; w.recoilSide = 0.22f; w.recoilRecovery = 9f;
                w.adsFieldOfView = 52f; w.adsSeconds = 0.13f; w.adsMoveSpeedMultiplier = 0.6f;
                w.impactForce = 10f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 8f; w.crosshairSpreadScale = 18f;
                w.crosshairColor = new Color(0.85f, 0.9f, 0.45f); w.optic = OpticType.Iron;
                w.tracerColor = new Color(0.8f, 0.9f, 0.4f);
            }),

            Weapon("WPN_HealingBeams", w =>
            {
                w.displayName = "Healing Beams";
                w.preferredRole = LoadoutRole.Support;
                w.damage = 1f; w.healPerHit = 5f; w.headshotMultiplier = 1f; w.limbMultiplier = 1f;
                w.range = 60f; w.falloffStart = 60f; w.falloffAtMaxRange = 1f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 600f; w.automatic = true;
                w.magazineSize = 999; w.reserveAmmo = 0; w.reloadSeconds = 1f; w.infiniteAmmo = true;
                w.hipSpreadDegrees = 0.4f; w.adsSpreadDegrees = 0.4f; w.spreadPerShot = 0f;
                w.maxBloom = 0f; w.bloomRecovery = 1f;
                w.recoilUp = 0f; w.recoilSide = 0f; w.recoilRecovery = 12f;
                w.adsFieldOfView = 62f; w.adsSeconds = 0.1f; w.adsMoveSpeedMultiplier = 1f;
                w.impactForce = 0f;
                w.crosshairStyle = CrosshairStyle.Dot; w.crosshairShowDot = true;
                w.crosshairBaseGap = 0f; w.crosshairSpreadScale = 0f;
                w.crosshairColor = new Color(0.3f, 0.95f, 0.7f); w.optic = OpticType.Iron;
                w.tracerColor = new Color(0.3f, 0.95f, 0.6f);
            }),

            Weapon("WPN_AegisCarbine", w =>
            {
                w.displayName = "Aegis Carbine";
                w.preferredRole = LoadoutRole.Support;
                w.damage = 12f; w.headshotMultiplier = 1.3f;
                w.range = 70f; w.falloffStart = 20f; w.falloffAtMaxRange = 0.6f;
                w.pelletsPerShot = 1; w.roundsPerMinute = 340f; w.automatic = false;
                w.magazineSize = 24; w.reserveAmmo = 144; w.reloadSeconds = 2.2f;
                w.hipSpreadDegrees = 2.2f; w.adsSpreadDegrees = 0.5f; w.spreadPerShot = 0.4f;
                w.maxBloom = 3f; w.bloomRecovery = 8f;
                w.recoilUp = 0.5f; w.recoilSide = 0.22f; w.recoilRecovery = 9f;
                w.adsFieldOfView = 52f; w.adsSeconds = 0.12f; w.adsMoveSpeedMultiplier = 0.65f;
                w.impactForce = 8f;
                w.crosshairStyle = CrosshairStyle.Cross; w.crosshairBaseGap = 6f; w.crosshairSpreadScale = 20f;
                w.crosshairColor = new Color(0.85f, 0.9f, 0.45f); w.optic = OpticType.Reflex;
                w.tracerColor = new Color(0.85f, 0.9f, 0.4f);
            })
        };
    }

    private static void BuildOperativeWeaponLibrary(List<WeaponDefinition> operativeWeapons, List<WeaponDefinition> baseWeapons)
    {
        string path = $"{ResourcesFolder}/{OperativeWeaponLibrary.ResourcePath}.asset";
        OperativeWeaponLibrary library = AssetDatabase.LoadAssetAtPath<OperativeWeaponLibrary>(path);

        if (library == null)
        {
            library = ScriptableObject.CreateInstance<OperativeWeaponLibrary>();
            AssetDatabase.CreateAsset(library, path);
        }

        WeaponDefinition sidearm = baseWeapons.Find(x => x.name == "WPN_Pistol");

        WeaponDefinition ironKnuckles = operativeWeapons.Find(x => x.name == "WPN_IronKnuckles");

        List<OperativeWeaponLibrary.Entry> entries = new List<OperativeWeaponLibrary.Entry>();
        foreach (OperativeDefinition def in OperativeRoster.All)
        {
            entries.Add(new OperativeWeaponLibrary.Entry
            {
                operative = def.id,
                primary = operativeWeapons.Find(x => x.name == def.primaryWeapon),

                secondary = def.id == OperativeId.Rook ? ironKnuckles : null
            });
        }

        library.Configure(sidearm, entries);
        EditorUtility.SetDirty(library);
    }

    private static WeaponDefinition Weapon(string name, System.Action<WeaponDefinition> configure)
    {
        string path = $"{WeaponsFolder}/{name}.asset";
        WeaponDefinition weapon = AssetDatabase.LoadAssetAtPath<WeaponDefinition>(path);

        if (weapon == null)
        {
            weapon = ScriptableObject.CreateInstance<WeaponDefinition>();
            AssetDatabase.CreateAsset(weapon, path);
        }

        configure(weapon);
        EditorUtility.SetDirty(weapon);
        return weapon;
    }

    public static LoadoutLibrary BuildLoadouts(List<WeaponDefinition> weapons)
    {
        WeaponDefinition smg = weapons.Find(w => w.name == "WPN_SMG");
        WeaponDefinition rifle = weapons.Find(w => w.name == "WPN_Rifle");
        WeaponDefinition shotgun = weapons.Find(w => w.name == "WPN_Shotgun");
        WeaponDefinition pistol = weapons.Find(w => w.name == "WPN_Pistol");

        List<LoadoutPreset> presets = new List<LoadoutPreset>
        {
            Preset(LoadoutRole.Light, "Light", new Color(0.35f, 0.9f, 0.85f), new List<WeaponDefinition> { smg, pistol }),
            Preset(LoadoutRole.Support, "Support", new Color(0.4f, 0.75f, 1f), new List<WeaponDefinition> { rifle, pistol }),
            Preset(LoadoutRole.Heavy, "Heavy", new Color(1f, 0.6f, 0.3f), new List<WeaponDefinition> { shotgun, pistol })
        };

        string libraryPath = $"{ResourcesFolder}/{LoadoutLibrary.ResourcePath}.asset";
        LoadoutLibrary library = AssetDatabase.LoadAssetAtPath<LoadoutLibrary>(libraryPath);

        if (library == null)
        {
            library = ScriptableObject.CreateInstance<LoadoutLibrary>();
            AssetDatabase.CreateAsset(library, libraryPath);
        }

        library.SetPresets(presets);
        EditorUtility.SetDirty(library);
        return library;
    }

    private static LoadoutPreset Preset(LoadoutRole role, string displayName, Color accent, List<WeaponDefinition> weapons)
    {
        string path = $"{LoadoutsFolder}/LDT_{displayName}.asset";
        LoadoutPreset preset = AssetDatabase.LoadAssetAtPath<LoadoutPreset>(path);

        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<LoadoutPreset>();
            AssetDatabase.CreateAsset(preset, path);
        }

        preset.role = role;
        preset.displayName = displayName;
        preset.blurb = LoadoutStats.Blurb(role);
        preset.accentColor = accent;
        preset.weapons = weapons;
        preset.overrideStats = false;
        preset.stats = LoadoutStats.For(role);

        EditorUtility.SetDirty(preset);
        return preset;
    }

    public static GameObject BuildPlayerPrefab(Palette palette, List<WeaponDefinition> weapons)
    {
        GameObject root = new GameObject("Player");
        root.layer = 0;

        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.35f;
        capsule.center = new Vector3(0f, 0.9f, 0f);

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.mass = 80f;
        body.freezeRotation = true;
        body.interpolation = RigidbodyInterpolation.Interpolate;
        body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

        capsule.sharedMaterial = EnsurePhysicsMaterial();

        Health health = root.AddComponent<Health>();
        health.SetDisplayName("Player");

        Hitbox bodyHitbox = root.AddComponent<Hitbox>();
        bodyHitbox.Configure(Hitbox.Zone.Body, health);

        movement mover = root.AddComponent<movement>();
        PlayerLook look = root.AddComponent<PlayerLook>();

        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(root.transform, false);

        GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        mesh.name = "Body";
        mesh.transform.SetParent(visuals.transform, false);
        mesh.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        mesh.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        Object.DestroyImmediate(mesh.GetComponent<Collider>());
        mesh.GetComponent<MeshRenderer>().sharedMaterial = palette.attacker;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(visuals.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
        head.transform.localScale = Vector3.one * 0.38f;
        head.GetComponent<MeshRenderer>().sharedMaterial = palette.attacker;

        Hitbox headHitbox = head.AddComponent<Hitbox>();
        headHitbox.Configure(Hitbox.Zone.Head, health);

        GameObject pivot = new GameObject("CameraPivot");
        pivot.transform.SetParent(root.transform, false);
        pivot.transform.localPosition = new Vector3(0f, 1.62f, 0f);

        GameObject cameraObject = new GameObject("PlayerCamera");
        cameraObject.tag = "MainCamera";
        cameraObject.transform.SetParent(pivot.transform, false);

        Camera camera = cameraObject.AddComponent<Camera>();
        camera.fieldOfView = 70f;
        camera.nearClipPlane = 0.03f;
        camera.farClipPlane = 400f;
        cameraObject.AddComponent<AudioListener>();

        GameObject rig = new GameObject("WeaponRig");
        rig.transform.SetParent(pivot.transform, false);

        WeaponView view = rig.AddComponent<WeaponView>();
        List<WeaponView.Entry> entries = new List<WeaponView.Entry>();

        foreach (WeaponDefinition weapon in weapons)
        {
            entries.Add(BuildGunModel(rig.transform, weapon, palette));
        }

        view.SetEntries(entries);

        WeaponController controller = rig.AddComponent<WeaponController>();

        Loadout loadout = root.AddComponent<Loadout>();
        root.AddComponent<StatusEffects>();
        root.AddComponent<PositionHistory>();
        OperativeController operative = root.AddComponent<OperativeController>();
        PlayerController player = root.AddComponent<PlayerController>();

        SerializedObject serializedView = new SerializedObject(view);
        serializedView.FindProperty("mover").objectReferenceValue = mover;
        serializedView.FindProperty("look").objectReferenceValue = look;
        serializedView.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedController = new SerializedObject(controller);
        serializedController.FindProperty("aimOrigin").objectReferenceValue = pivot.transform;
        serializedController.FindProperty("view").objectReferenceValue = view;
        serializedController.FindProperty("viewCamera").objectReferenceValue = camera;
        serializedController.FindProperty("ownerHealth").objectReferenceValue = health;
        serializedController.FindProperty("look").objectReferenceValue = look;
        serializedController.FindProperty("mover").objectReferenceValue = mover;
        serializedController.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedLook = new SerializedObject(look);
        serializedLook.FindProperty("cameraPivot").objectReferenceValue = pivot.transform;
        serializedLook.FindProperty("eyeHeight").floatValue = 1.62f;
        serializedLook.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedLoadout = new SerializedObject(loadout);
        serializedLoadout.FindProperty("bodyRoot").objectReferenceValue = visuals.transform;
        serializedLoadout.FindProperty("mover").objectReferenceValue = mover;
        serializedLoadout.FindProperty("health").objectReferenceValue = health;
        serializedLoadout.FindProperty("weapons").objectReferenceValue = controller;
        serializedLoadout.ApplyModifiedPropertiesWithoutUndo();

        SerializedObject serializedPlayer = new SerializedObject(player);
        serializedPlayer.FindProperty("mover").objectReferenceValue = mover;
        serializedPlayer.FindProperty("look").objectReferenceValue = look;
        serializedPlayer.FindProperty("weapons").objectReferenceValue = controller;
        serializedPlayer.FindProperty("loadout").objectReferenceValue = loadout;
        serializedPlayer.FindProperty("operative").objectReferenceValue = operative;
        serializedPlayer.FindProperty("health").objectReferenceValue = health;
        serializedPlayer.FindProperty("visuals").objectReferenceValue = visuals.transform;
        serializedPlayer.ApplyModifiedPropertiesWithoutUndo();

        int localBodyLayer = EnsureLayer("LocalBody");
        SetLayerRecursively(visuals, localBodyLayer);
        camera.cullingMask &= ~(1 << localBodyLayer);

        EditorUtility.SetDirty(view);

        string path = $"{PrefabsFolder}/Player.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    private static WeaponView.Entry BuildGunModel(Transform parent, WeaponDefinition weapon, Palette palette)
    {
        GameObject model = new GameObject($"Gun_{weapon.name}");
        model.transform.SetParent(parent, false);

        float length = weapon.pelletsPerShot > 1 ? 0.5f : weapon.automatic ? 0.55f : 0.34f;

        GameObject receiver = GameObject.CreatePrimitive(PrimitiveType.Cube);
        receiver.name = "Receiver";
        receiver.transform.SetParent(model.transform, false);
        receiver.transform.localPosition = new Vector3(0f, 0f, length * 0.5f);
        receiver.transform.localScale = new Vector3(0.07f, 0.10f, length);
        Object.DestroyImmediate(receiver.GetComponent<Collider>());
        receiver.GetComponent<MeshRenderer>().sharedMaterial = palette.gunBody;

        GameObject grip = GameObject.CreatePrimitive(PrimitiveType.Cube);
        grip.name = "Grip";
        grip.transform.SetParent(model.transform, false);
        grip.transform.localPosition = new Vector3(0f, -0.10f, 0.09f);
        grip.transform.localRotation = Quaternion.Euler(14f, 0f, 0f);
        grip.transform.localScale = new Vector3(0.055f, 0.15f, 0.065f);
        Object.DestroyImmediate(grip.GetComponent<Collider>());
        grip.GetComponent<MeshRenderer>().sharedMaterial = palette.gunBody;

        GameObject sight = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sight.name = "Sight";
        sight.transform.SetParent(model.transform, false);
        sight.transform.localPosition = new Vector3(0f, 0.075f, length * 0.55f);
        sight.transform.localScale = new Vector3(0.02f, 0.03f, 0.07f);
        Object.DestroyImmediate(sight.GetComponent<Collider>());
        sight.GetComponent<MeshRenderer>().sharedMaterial = palette.gunAccent;

        GameObject magazine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        magazine.name = "Magazine";
        magazine.transform.SetParent(model.transform, false);
        magazine.transform.localPosition = new Vector3(0f, -0.085f, length * 0.62f);
        magazine.transform.localScale = new Vector3(0.05f, 0.13f, 0.055f);
        Object.DestroyImmediate(magazine.GetComponent<Collider>());
        magazine.GetComponent<MeshRenderer>().sharedMaterial = palette.gunAccent;

        GameObject muzzle = new GameObject("Muzzle");
        muzzle.transform.SetParent(model.transform, false);
        muzzle.transform.localPosition = new Vector3(0f, 0f, length + 0.04f);

        return new WeaponView.Entry
        {
            weapon = weapon,
            model = model.transform,
            muzzle = muzzle.transform
        };
    }

    public static GameObject BuildBotPrefab(Palette palette)
    {
        GameObject root = new GameObject("Bot");

        CapsuleCollider capsule = root.AddComponent<CapsuleCollider>();
        capsule.height = 1.8f;
        capsule.radius = 0.35f;
        capsule.center = new Vector3(0f, 0.9f, 0f);

        Rigidbody body = root.AddComponent<Rigidbody>();
        body.isKinematic = true;
        body.useGravity = false;

        NavMeshAgent agent = root.AddComponent<NavMeshAgent>();
        agent.radius = 0.35f;
        agent.height = 1.8f;
        agent.baseOffset = 0f;
        agent.speed = 5.5f;
        agent.acceleration = 24f;
        agent.angularSpeed = 720f;
        agent.stoppingDistance = 0.6f;
        agent.autoBraking = false;

        Health health = root.AddComponent<Health>();
        health.SetDisplayName("Bot");

        Hitbox bodyHitbox = root.AddComponent<Hitbox>();
        bodyHitbox.Configure(Hitbox.Zone.Body, health);

        GameObject visuals = new GameObject("Visuals");
        visuals.transform.SetParent(root.transform, false);

        GameObject mesh = GameObject.CreatePrimitive(PrimitiveType.Capsule);
        mesh.name = "Body";
        mesh.transform.SetParent(visuals.transform, false);
        mesh.transform.localPosition = new Vector3(0f, 0.9f, 0f);
        mesh.transform.localScale = new Vector3(0.7f, 0.9f, 0.7f);
        Object.DestroyImmediate(mesh.GetComponent<Collider>());
        mesh.GetComponent<MeshRenderer>().sharedMaterial = palette.defender;

        GameObject head = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        head.name = "Head";
        head.transform.SetParent(root.transform, false);
        head.transform.localPosition = new Vector3(0f, 1.72f, 0f);
        head.transform.localScale = Vector3.one * 0.38f;
        head.GetComponent<MeshRenderer>().sharedMaterial = palette.defender;

        Hitbox headHitbox = head.AddComponent<Hitbox>();
        headHitbox.Configure(Hitbox.Zone.Head, health);

        GameObject nose = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nose.name = "Facing";
        nose.transform.SetParent(visuals.transform, false);
        nose.transform.localPosition = new Vector3(0f, 1.2f, 0.34f);
        nose.transform.localScale = new Vector3(0.12f, 0.12f, 0.3f);
        Object.DestroyImmediate(nose.GetComponent<Collider>());
        nose.GetComponent<MeshRenderer>().sharedMaterial = palette.gunBody;

        GameObject eyes = new GameObject("Eyes");
        eyes.transform.SetParent(root.transform, false);
        eyes.transform.localPosition = new Vector3(0f, 1.6f, 0.2f);

        root.AddComponent<StatusEffects>();
        root.AddComponent<PositionHistory>();
        root.AddComponent<BotOperative>();
        BotBrain brain = root.AddComponent<BotBrain>();

        SerializedObject serialized = new SerializedObject(brain);
        serialized.FindProperty("health").objectReferenceValue = health;
        serialized.FindProperty("eyes").objectReferenceValue = eyes.transform;
        serialized.FindProperty("visuals").objectReferenceValue = visuals.transform;
        serialized.ApplyModifiedPropertiesWithoutUndo();

        string path = $"{PrefabsFolder}/Bot.prefab";
        GameObject prefab = PrefabUtility.SaveAsPrefabAsset(root, path);
        Object.DestroyImmediate(root);
        return prefab;
    }

    public static int EnsureLayer(string name)
    {
        int existing = LayerMask.NameToLayer(name);
        if (existing >= 0)
        {
            return existing;
        }

        SerializedObject tagManager = new SerializedObject(
            AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]
        );
        SerializedProperty layers = tagManager.FindProperty("layers");

        for (int i = 8; i < layers.arraySize; i++)
        {
            SerializedProperty layer = layers.GetArrayElementAtIndex(i);
            if (string.IsNullOrEmpty(layer.stringValue))
            {
                layer.stringValue = name;
                tagManager.ApplyModifiedPropertiesWithoutUndo();
                return i;
            }
        }

        Debug.LogWarning($"[Advantage] No free layer slot for '{name}'.");
        return 0;
    }

    public static void SetLayerRecursively(GameObject target, int layer)
    {
        target.layer = layer;

        foreach (Transform child in target.transform)
        {
            SetLayerRecursively(child.gameObject, layer);
        }
    }

    private static PhysicsMaterial EnsurePhysicsMaterial()
    {

        const string path = "Assets/Materials/PM_Player.asset";
        PhysicsMaterial material = AssetDatabase.LoadAssetAtPath<PhysicsMaterial>(path);

        if (material == null)
        {
            material = new PhysicsMaterial("PM_Player");
            AssetDatabase.CreateAsset(material, path);
        }

        material.dynamicFriction = 0f;
        material.staticFriction = 0f;
        material.bounciness = 0f;
        material.frictionCombine = PhysicsMaterialCombine.Minimum;
        material.bounceCombine = PhysicsMaterialCombine.Minimum;
        EditorUtility.SetDirty(material);
        return material;
    }

    public static void EnsureFolder(string path)
    {
        if (AssetDatabase.IsValidFolder(path))
        {
            return;
        }

        string parent = Path.GetDirectoryName(path).Replace('\\', '/');
        string leaf = Path.GetFileName(path);

        if (!AssetDatabase.IsValidFolder(parent))
        {
            EnsureFolder(parent);
        }

        AssetDatabase.CreateFolder(parent, leaf);
    }
}
