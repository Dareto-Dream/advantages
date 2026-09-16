using System.Collections.Generic;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.UI;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public static class AdvantageSceneBuilder
{
    private const string ScenesFolder = "Assets/Scenes";
    private const string ArenaPath = ScenesFolder + "/Arena.unity";
    private const string MenuPath = ScenesFolder + "/MainMenu.unity";
    private const string ConvergencePath = ScenesFolder + "/Arena_Convergence.unity";
    private const string ExtractionPath = ScenesFolder + "/Arena_Extraction.unity";
    private const string BreachPath = ScenesFolder + "/Arena_Breach.unity";
    private const string DominionPath = ScenesFolder + "/Arena_Dominion.unity";
    private const string ActionsAssetPath = "Assets/InputSystem_Actions.inputactions";
    private const string UiPrefabsFolder = "Assets/UI/Prefabs";

    private static readonly bool GreyboxOnly = true;

    [MenuItem("Advantage/Build/3 - Build Scenes", priority = 2)]
    public static void BuildScenes()
    {
        if (!AdvantageAssetBuilder.TmpEssentialsPresent())
        {
            Debug.LogError("[Advantage] Import TextMeshPro essentials first (Advantage > Build > 1).");
            return;
        }

        AdvantageAssetBuilder.EnsureFolder(ScenesFolder);

        BuildMenuScene();
        BuildArenaScene();
        BuildConvergenceScene();
        BuildExtractionScene();
        BuildBreachScene();
        BuildDominionScene();
        RegisterBuildSettings();

        Debug.Log("[Advantage] Scenes built.");
    }

    [MenuItem("Advantage/Build/4 - Build Everything", priority = 3)]
    public static void BuildEverything()
    {
        AdvantageAssetBuilder.ImportTmpEssentials();
        AdvantageAssetBuilder.GenerateAll();
        BuildScenes();
    }

    private static void BuildMenuScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        GameObject cameraObject = new GameObject("MenuCamera");
        cameraObject.tag = "MainCamera";
        Camera camera = cameraObject.AddComponent<Camera>();
        camera.clearFlags = CameraClearFlags.SolidColor;
        camera.backgroundColor = new Color(0.04f, 0.05f, 0.07f);
        camera.cullingMask = 0;
        cameraObject.AddComponent<AudioListener>();

        InstantiateUi("MainMenuCanvas");
        InstantiateUi("LoadingCanvas");
        CreateEventSystem();

        new GameObject("DEBUG_MapPicker").AddComponent<DebugMapPicker>();

        EditorSceneManager.SaveScene(scene, MenuPath);
    }

    private static void BuildArenaScene()
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AdvantageAssetBuilder.Palette palette = AdvantageAssetBuilder.BuildMaterials();

        CreateLighting();
        GameObject map = CreateMap(palette);

        NavMeshSurface surface = map.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.12f;
        map.AddComponent<NavMeshBaker>();

        new GameObject("WeaponFx").AddComponent<WeaponFx>();

        CreateMatchManager(palette);
        CreateHud();
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, ArenaPath);
    }

    [MenuItem("Advantage/Build/Modes/Convergence - The Spire Root")]
    public static void BuildConvergenceScene()
    {
        BuildModeScene(ConvergencePath, CreateConvergenceMap);
    }

    [MenuItem("Advantage/Build/Modes/Extraction - Vault Approach")]
    public static void BuildExtractionScene()
    {
        BuildModeScene(ExtractionPath, CreateExtractionMap);
    }

    [MenuItem("Advantage/Build/Modes/Breach - Reactor Core")]
    public static void BuildBreachScene()
    {
        BuildModeScene(BreachPath, CreateBreachMap);
    }

    [MenuItem("Advantage/Build/Modes/Dominion - The Scattered Ruins")]
    public static void BuildDominionScene()
    {
        BuildModeScene(DominionPath, CreateDominionMap);
    }

    private static void BuildModeScene(string scenePath, System.Func<AdvantageAssetBuilder.Palette, GameObject> buildMap)
    {
        Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

        AdvantageAssetBuilder.Palette palette = AdvantageAssetBuilder.BuildMaterials();

        CreateLighting();
        GameObject map = buildMap(palette);

        NavMeshSurface surface = map.AddComponent<NavMeshSurface>();
        surface.collectObjects = CollectObjects.Children;
        surface.useGeometry = NavMeshCollectGeometry.PhysicsColliders;
        surface.overrideVoxelSize = true;
        surface.voxelSize = 0.12f;
        map.AddComponent<NavMeshBaker>();

        new GameObject("WeaponFx").AddComponent<WeaponFx>();

        CreateMatchManager(palette);
        CreateHud();
        new GameObject("ObjectiveHudOverlay").AddComponent<ObjectiveHudOverlay>();
        CreateEventSystem();

        EditorSceneManager.SaveScene(scene, scenePath);

        RegisterBuildSettings();
    }

    private static GameObject CreateConvergenceMap(AdvantageAssetBuilder.Palette palette)
    {
        GameObject map = new GameObject("Map_Convergence");

        Box(map, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(70f, 1f, 44f), palette.ground);
        Perimeter(map, 35.5f, 22.5f, 7f, 6f, palette.wall);

        Box(map, "Site_Visual", new Vector3(0f, 0.15f, 0f), new Vector3(14f, 0.3f, 14f), palette.siteMarker);
        GameObject siteZone = CreateContestVolume(map, "SitePoint", new Vector3(0f, 1f, 0f), new Vector3(14f, 4f, 14f));

        if (!GreyboxOnly)
        {

            Pillar(map, "Spire", new Vector3(0f, 0f, 0f), 1.6f, 9f, palette.wall);
            CurvedWall(map, "Rotunda_NE", new Vector3(0f, 1.75f, 0f), 11f, 12f, 78f, 3.5f, 0.8f, palette.cover);
            CurvedWall(map, "Rotunda_NW", new Vector3(0f, 1.75f, 0f), 11f, 102f, 168f, 3.5f, 0.8f, palette.cover);
            CurvedWall(map, "Rotunda_SW", new Vector3(0f, 1.75f, 0f), 11f, 192f, 258f, 3.5f, 0.8f, palette.cover);
            CurvedWall(map, "Rotunda_SE", new Vector3(0f, 1.75f, 0f), 11f, 282f, 348f, 3.5f, 0.8f, palette.cover);

            MirrorCurvedWall(map, "Flank_Arc_N", new Vector3(0f, 2f, 0f), 22f, 30f, 68f, 4f, 1f, palette.wall);
            MirrorCurvedWall(map, "Flank_Arc_S", new Vector3(0f, 2f, 0f), 22f, -68f, -30f, 4f, 1f, palette.wall);

            MirrorAngled(map, "Mid_Cover_N", new Vector3(16f, 1f, 8f), new Vector3(4f, 2f, 2f), 28f, palette.cover);
            MirrorAngled(map, "Mid_Cover_S", new Vector3(16f, 1f, -8f), new Vector3(4f, 2f, 2f), -28f, palette.cover);
            MirrorPillar(map, "Outer_Column", new Vector3(24f, 0f, 0f), 1.4f, 5f, palette.wall);
        }

        BuildSpawnRoomsAndPoints(map, palette, 31f, 25f);

        ConvergenceObjective objective = new GameObject("ModeObjective_Convergence").AddComponent<ConvergenceObjective>();
        objective.transform.SetParent(map.transform, false);
        Wire(objective, "point", siteZone.GetComponent<ContestVolume>());

        return map;
    }

    private static GameObject CreateExtractionMap(AdvantageAssetBuilder.Palette palette)
    {
        GameObject map = new GameObject("Map_Extraction");

        Box(map, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(76f, 1f, 40f), palette.ground);
        Perimeter(map, 38.5f, 20.5f, 6f, 6f, palette.wall);

        GameObject attackerEnd = PathMarker(map, "PathNode_AttackerEnd", new Vector3(-26f, 1f, 0f), palette.attacker, true);
        GameObject middle = PathMarker(map, "PathNode_Middle", new Vector3(0f, 1f, 0f), palette.siteMarker, false);
        GameObject defenderEnd = PathMarker(map, "PathNode_DefenderEnd", new Vector3(26f, 1f, 0f), palette.defender, true);

        GameObject pathObject = new GameObject("PayloadPath");
        pathObject.transform.SetParent(map.transform, false);
        PayloadPath path = pathObject.AddComponent<PayloadPath>();

        LineRenderer line = pathObject.AddComponent<LineRenderer>();
        line.useWorldSpace = true;
        line.widthMultiplier = 0.35f;
        line.numCornerVertices = 4;
        line.sharedMaterial = palette.siteMarker;
        line.positionCount = 3;
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        WireArray(path, "nodes", new Object[] { attackerEnd.transform, middle.transform, defenderEnd.transform });
        Wire(path, "line", line);

        line.SetPosition(0, attackerEnd.transform.position);
        line.SetPosition(1, middle.transform.position);
        line.SetPosition(2, defenderEnd.transform.position);

        GameObject payload = CreateContestVolume(map, "Payload", new Vector3(0f, 1f, 0f), new Vector3(4f, 3f, 4f));

        Box(payload, "Payload_Visual", Vector3.zero, new Vector3(2f, 1.6f, 2f), palette.siteMarker, false);

        if (!GreyboxOnly)
        {

            MirrorAngled(map, "Chicane_Hi", new Vector3(9f, 1.75f, 4.5f), new Vector3(2f, 3.5f, 13f), 34f, palette.wall);
            MirrorAngled(map, "Chicane_Lo", new Vector3(17f, 1f, -6f), new Vector3(2f, 2f, 9f), -30f, palette.wall);
            MirrorCurvedWall(map, "Gallery", new Vector3(20f, 2f, 0f), 12f, 42f, 150f, 4f, 1f, palette.wall);

            MirrorAngled(map, "Lane_Cover_A", new Vector3(14f, 1f, 8f), new Vector3(3f, 2f, 2f), 20f, palette.cover);
            MirrorAngled(map, "Lane_Cover_B", new Vector3(13f, 1f, -6f), new Vector3(3f, 2f, 2f), -18f, palette.cover);
            MirrorPillar(map, "Lane_Column", new Vector3(24f, 0f, 4f), 1.3f, 4.5f, palette.wall);
        }

        BuildSpawnRoomsAndPoints(map, palette, 33f, 27f);

        ExtractionObjective objective = new GameObject("ModeObjective_Extraction").AddComponent<ExtractionObjective>();
        objective.transform.SetParent(map.transform, false);
        Wire(objective, "zone", payload.GetComponent<ContestVolume>());
        Wire(objective, "path", path);

        return map;
    }

    private static GameObject PathMarker(GameObject parent, string name, Vector3 position, Material material, bool isEnd)
    {
        GameObject marker = new GameObject(name);
        marker.transform.SetParent(parent.transform, false);
        marker.transform.localPosition = position;

        Box(marker, "Pad", new Vector3(0f, -0.9f, 0f), new Vector3(isEnd ? 6f : 4f, 0.2f, isEnd ? 10f : 4f), material, false);
        Box(marker, "Post", Vector3.zero, new Vector3(0.4f, isEnd ? 3f : 1.6f, 0.4f), material, false);

        return marker;
    }

    private static GameObject CreateBreachMap(AdvantageAssetBuilder.Palette palette)
    {
        GameObject map = new GameObject("Map_Breach");

        Box(map, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(64f, 1f, 44f), palette.ground);
        Perimeter(map, 32.5f, 22.5f, 7f, 6f, palette.wall);

        Box(map, "Site_Visual", new Vector3(14f, 0.15f, 0f), new Vector3(10f, 0.3f, 10f), palette.siteMarker);
        GameObject siteZone = CreateContestVolume(map, "SitePoint", new Vector3(14f, 1f, 0f), new Vector3(10f, 4f, 10f));

        if (!GreyboxOnly)
        {

            Pillar(map, "Reactor_Core", new Vector3(14f, 0f, 0f), 1.8f, 8f, palette.gunBody);
            CurvedWall(map, "Reactor_Housing_N", new Vector3(14f, 2f, 0f), 9f, 20f, 160f, 4f, 0.9f, palette.wall);
            CurvedWall(map, "Reactor_Housing_S", new Vector3(14f, 2f, 0f), 9f, 200f, 340f, 4f, 0.9f, palette.wall);

            AngledBox(map, "Funnel_N", new Vector3(-1f, 2f, 9f), new Vector3(1f, 4f, 20f), 52f, palette.wall);
            AngledBox(map, "Funnel_S", new Vector3(-1f, 2f, -9f), new Vector3(1f, 4f, 20f), -52f, palette.wall);

            MirrorAngled(map, "Approach_Cover", new Vector3(6f, 1f, 7f), new Vector3(3f, 2f, 2f), 30f, palette.cover);
            Box(map, "Approach_Cover_West_A", new Vector3(-8f, 1f, -6f), new Vector3(2.5f, 2f, 2.5f), palette.cover);
            Box(map, "Approach_Cover_West_B", new Vector3(-8f, 1f, 6f), new Vector3(2.5f, 2f, 2.5f), palette.cover);
            MirrorPillar(map, "Coolant_Column", new Vector3(4f, 0f, 14f), 1.2f, 4f, palette.wall);
        }

        BuildSpawnRoomsAndPoints(map, palette, 29f, 23f);

        BreachObjective objective = new GameObject("ModeObjective_Breach").AddComponent<BreachObjective>();
        objective.transform.SetParent(map.transform, false);
        Wire(objective, "point", siteZone.GetComponent<ContestVolume>());

        return map;
    }

    private static GameObject CreateDominionMap(AdvantageAssetBuilder.Palette palette)
    {
        GameObject map = new GameObject("Map_Dominion");

        Box(map, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(74f, 1f, 50f), palette.ground);
        Perimeter(map, 37.5f, 25.5f, 8f, 6f, palette.wall);

        GameObject comms = CreateTerminal(map, "Terminal_CommsRelay", new Vector3(0f, 0f, 15f), palette, DominionTerminal.Site.CommsRelay);
        GameObject supply = CreateTerminal(map, "Terminal_SupplyDepot", new Vector3(0f, 0f, 0f), palette, DominionTerminal.Site.SupplyDepot);
        GameObject transit = CreateTerminal(map, "Terminal_TransitHub", new Vector3(0f, 0f, -15f), palette, DominionTerminal.Site.TransitHub);

        if (!GreyboxOnly)
        {

            CurvedWall(map, "Ruin_Hub_N", new Vector3(0f, 1.5f, 0f), 8f, 25f, 92f, 3f, 0.8f, palette.cover);
            CurvedWall(map, "Ruin_Hub_S", new Vector3(0f, 1.5f, 0f), 8f, 205f, 272f, 3f, 0.8f, palette.cover);
            MirrorCurvedWall(map, "Ruin_Comms", new Vector3(0f, 1.5f, 15f), 10f, 25f, 95f, 3f, 0.8f, palette.cover);
            MirrorCurvedWall(map, "Ruin_Transit", new Vector3(0f, 1.5f, -15f), 10f, -95f, -25f, 3f, 0.8f, palette.cover);

            MirrorAngled(map, "Rubble_A", new Vector3(14f, 1f, 8f), new Vector3(4f, 2f, 2f), 40f, palette.wall);
            MirrorAngled(map, "Rubble_B", new Vector3(16f, 0.75f, -9f), new Vector3(3.5f, 1.5f, 2f), -25f, palette.cover);
            MirrorPillar(map, "Broken_Column_A", new Vector3(9f, 0f, 20f), 1.2f, 3.5f, palette.wall);
            MirrorPillar(map, "Broken_Column_B", new Vector3(22f, 0f, 4f), 1.3f, 5f, palette.wall);
            MirrorPillar(map, "Broken_Column_C", new Vector3(12f, 0f, -18f), 1.1f, 2.5f, palette.wall);
        }

        BuildSpawnRoomsAndPoints(map, palette, 34f, 28f);

        DominionObjective objective = new GameObject("ModeObjective_Dominion").AddComponent<DominionObjective>();
        objective.transform.SetParent(map.transform, false);

        Object[] terminals =
        {
            comms.GetComponent<DominionTerminal>(),
            supply.GetComponent<DominionTerminal>(),
            transit.GetComponent<DominionTerminal>()
        };
        WireArray(objective, "terminals", terminals);

        return map;
    }

    private static GameObject CreateTerminal(GameObject parent, string name, Vector3 groundCenter, AdvantageAssetBuilder.Palette palette, DominionTerminal.Site site)
    {
        GameObject terminal = CreateContestVolume(parent, name, groundCenter + new Vector3(0f, 2f, 0f), new Vector3(6f, 4f, 6f));

        Box(terminal, "Pad", new Vector3(0f, -1.95f, 0f), new Vector3(6f, 0.1f, 6f), palette.siteMarker);
        Box(terminal, "Console", new Vector3(0f, -1.1f, 0f), new Vector3(1f, 1.8f, 1f), palette.gunBody);

        DominionTerminal dominionTerminal = terminal.AddComponent<DominionTerminal>();
        Wire(dominionTerminal, "zone", terminal.GetComponent<ContestVolume>());
        WireEnum(dominionTerminal, "site", (int)site);

        return terminal;
    }

    private static void BuildSpawnRoomsAndPoints(GameObject map, AdvantageAssetBuilder.Palette palette, float roomX, float gateX)
    {
        const float halfZ = 8f;
        const float wallY = 3f;
        const float roofY = 6.3f;

        float backX = roomX + 3.5f;
        float midX = (gateX + backX) * 0.5f;
        float spanX = backX - gateX;

        MirrorX(map, "SpawnRoom_Wall_North", new Vector3(midX, wallY, halfZ), new Vector3(spanX, 6f, 1f), palette.wall);
        MirrorX(map, "SpawnRoom_Wall_South", new Vector3(midX, wallY, -halfZ), new Vector3(spanX, 6f, 1f), palette.wall);
        MirrorX(map, "SpawnRoom_Wall_Back", new Vector3(backX, wallY, 0f), new Vector3(1f, 6f, halfZ * 2f + 1f), palette.wall);
        MirrorX(map, "SpawnRoom_Roof", new Vector3(midX, roofY, 0f), new Vector3(spanX + 1f, 0.6f, halfZ * 2f + 1f), palette.wall);

        MirrorX(map, "SpawnRoom_Wall_Seg_S", new Vector3(gateX, wallY, -7.25f), new Vector3(1f, 6f, 1.5f), palette.wall);
        MirrorX(map, "SpawnRoom_Wall_Seg_MS", new Vector3(gateX, wallY, -2.5f), new Vector3(1f, 6f, 2f), palette.wall);
        MirrorX(map, "SpawnRoom_Wall_Seg_MN", new Vector3(gateX, wallY, 2.5f), new Vector3(1f, 6f, 2f), palette.wall);
        MirrorX(map, "SpawnRoom_Wall_Seg_N", new Vector3(gateX, wallY, 7.25f), new Vector3(1f, 6f, 1.5f), palette.wall);

        MirrorPanel(map, "SpawnPanel_S", new Vector3(gateX, 2.85f, -5f), new Vector3(0.3f, 5.5f, 3f), palette);
        MirrorPanel(map, "SpawnPanel_M", new Vector3(gateX, 2.85f, 0f), new Vector3(0.3f, 5.5f, 3f), palette);
        MirrorPanel(map, "SpawnPanel_N", new Vector3(gateX, 2.85f, 5f), new Vector3(0.3f, 5.5f, 3f), palette);

        GameObject spawnsRoot = new GameObject("Spawns");
        spawnsRoot.transform.SetParent(map.transform, false);

        float[] offsets = { 6f, 2f, -2f, -6f };
        for (int i = 0; i < offsets.Length; i++)
        {
            CreateSpawn(spawnsRoot, Team.Attackers, new Vector3(-roomX, 0.1f, offsets[i]), 90f, i);
            CreateSpawn(spawnsRoot, Team.Defenders, new Vector3(roomX, 0.1f, offsets[i]), -90f, i);
        }

        CreateSpawnZone(map, "SpawnZone_W", new Vector3(-midX, 3f, 0f), new Vector3(spanX + 3f, 6f, halfZ * 2f), Team.Attackers);
        CreateSpawnZone(map, "SpawnZone_E", new Vector3(midX, 3f, 0f), new Vector3(spanX + 3f, 6f, halfZ * 2f), Team.Defenders);
    }

    private static void CreateSpawnZone(GameObject parent, string name, Vector3 center, Vector3 size, Team team)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = center;

        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;

        go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        go.AddComponent<SpawnZone>().Configure(team, 55f, 1.5f);
    }

    private static GameObject CreateContestVolume(GameObject parent, string name, Vector3 center, Vector3 size)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent.transform, false);
        go.transform.localPosition = center;

        BoxCollider collider = go.AddComponent<BoxCollider>();
        collider.isTrigger = true;
        collider.size = size;

        go.AddComponent<NavMeshModifier>().ignoreFromBuild = true;
        go.AddComponent<ContestVolume>();

        return go;
    }

    private static void Wire(Object target, string fieldName, Object value)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogError($"[Advantage] Field '{fieldName}' not found on {target.GetType().Name}.");
            return;
        }

        property.objectReferenceValue = value;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireArray(Object target, string fieldName, Object[] values)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogError($"[Advantage] Array field '{fieldName}' not found on {target.GetType().Name}.");
            return;
        }

        property.arraySize = values.Length;
        for (int i = 0; i < values.Length; i++)
        {
            property.GetArrayElementAtIndex(i).objectReferenceValue = values[i];
        }

        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void WireEnum(Object target, string fieldName, int enumValueIndex)
    {
        SerializedObject serialized = new SerializedObject(target);
        SerializedProperty property = serialized.FindProperty(fieldName);

        if (property == null)
        {
            Debug.LogError($"[Advantage] Enum field '{fieldName}' not found on {target.GetType().Name}.");
            return;
        }

        property.enumValueIndex = enumValueIndex;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateLighting()
    {
        GameObject lightObject = new GameObject("Sun");
        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Directional;
        light.intensity = 1.15f;
        light.color = new Color(1f, 0.96f, 0.9f);
        light.shadows = LightShadows.Soft;
        lightObject.transform.rotation = Quaternion.Euler(48f, 138f, 0f);

        RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(0.31f, 0.35f, 0.42f);
        RenderSettings.ambientEquatorColor = new Color(0.22f, 0.24f, 0.28f);
        RenderSettings.ambientGroundColor = new Color(0.12f, 0.12f, 0.14f);
        RenderSettings.fog = true;
        RenderSettings.fogMode = FogMode.Linear;
        RenderSettings.fogColor = new Color(0.09f, 0.10f, 0.13f);
        RenderSettings.fogStartDistance = 60f;
        RenderSettings.fogEndDistance = 190f;
    }

    private static GameObject CreateMap(AdvantageAssetBuilder.Palette palette)
    {
        GameObject map = new GameObject("Map");

        Box(map, "Floor", new Vector3(0f, -0.5f, 0f), new Vector3(72f, 1f, 48f), palette.ground);
        Perimeter(map, 36.5f, 24.5f, 8f, 6f, palette.wall);

        Box(map, "Site", new Vector3(0f, 0.15f, 0f), new Vector3(16f, 0.3f, 16f), palette.siteMarker);

        if (!GreyboxOnly)
        {

            CurvedWall(map, "Site_Shield_N", new Vector3(0f, 1f, 0f), 8f, 35f, 145f, 2f, 0.8f, palette.cover);
            CurvedWall(map, "Site_Shield_S", new Vector3(0f, 1f, 0f), 8f, 215f, 325f, 2f, 0.8f, palette.cover);
            MirrorPillar(map, "Site_Column", new Vector3(6f, 0f, 0f), 1.2f, 4.5f, palette.wall);

            MirrorAngled(map, "Lane_Divider_N", new Vector3(19f, 2f, 10f), new Vector3(18f, 4f, 1f), 12f, palette.wall);
            MirrorAngled(map, "Lane_Divider_S", new Vector3(19f, 2f, -10f), new Vector3(18f, 4f, 1f), -12f, palette.wall);
            MirrorCurvedWall(map, "Bend_N", new Vector3(6f, 2f, 0f), 16f, 55f, 128f, 4f, 1f, palette.wall);
            MirrorCurvedWall(map, "Bend_S", new Vector3(6f, 2f, 0f), 16f, -128f, -55f, 4f, 1f, palette.wall);
            MirrorPillar(map, "Mid_Pillar", new Vector3(9f, 0f, 0f), 1.3f, 4f, palette.wall);
            MirrorX(map, "Crate_A", new Vector3(13f, 0.75f, 4.5f), new Vector3(2.5f, 1.5f, 2.5f), palette.cover);
            MirrorX(map, "Crate_B", new Vector3(13f, 0.75f, -4.5f), new Vector3(2.5f, 1.5f, 2.5f), palette.cover);
            MirrorAngled(map, "Crate_C", new Vector3(22f, 1f, 0f), new Vector3(3f, 2f, 6f), 18f, palette.cover);
            MirrorX(map, "Crate_D", new Vector3(28f, 0.6f, 15f), new Vector3(4f, 1.2f, 4f), palette.cover);
            MirrorX(map, "Crate_E", new Vector3(28f, 0.6f, -15f), new Vector3(4f, 1.2f, 4f), palette.cover);
            MirrorX(map, "Outer_Block", new Vector3(6f, 1.5f, 19f), new Vector3(8f, 3f, 3f), palette.wall);
            MirrorX(map, "Outer_Block_S", new Vector3(6f, 1.5f, -19f), new Vector3(8f, 3f, 3f), palette.wall);

            MirrorX(map, "Catwalk", new Vector3(30f, 3f, 15f), new Vector3(10f, 0.5f, 8f), palette.wall);
            MirrorRamp(map, "Catwalk_Ramp", new Vector3(30f, 1.6f, 9f), new Vector3(8f, 0.5f, 8f), -21f, palette.wall);
        }

        BuildSpawnRoomsAndPoints(map, palette, 31f, 25f);

        return map;
    }

    private static void Perimeter(GameObject map, float halfX, float halfZ, float chamfer, float height, Material wall)
    {
        if (GreyboxOnly)
        {
            BoxArena(map, "Wall", halfX, halfZ, height, wall);
        }
        else
        {
            ChamferedArena(map, "Wall", halfX, halfZ, chamfer, height, wall);
        }
    }

    private static void BoxArena(GameObject map, string prefix, float halfX, float halfZ, float height, Material wall)
    {
        float y = height * 0.5f;
        const float t = 1f;

        Box(map, $"{prefix}_North", new Vector3(0f, y, halfZ), new Vector3(halfX * 2f + t, height, t), wall);
        Box(map, $"{prefix}_South", new Vector3(0f, y, -halfZ), new Vector3(halfX * 2f + t, height, t), wall);
        Box(map, $"{prefix}_East", new Vector3(halfX, y, 0f), new Vector3(t, height, halfZ * 2f + t), wall);
        Box(map, $"{prefix}_West", new Vector3(-halfX, y, 0f), new Vector3(t, height, halfZ * 2f + t), wall);
    }

    private static void MirrorX(GameObject parent, string name, Vector3 center, Vector3 size, Material material)
    {
        Box(parent, $"{name}_E", center, size, material);
        Box(parent, $"{name}_W", new Vector3(-center.x, center.y, center.z), size, material);
    }

    private static void MirrorRamp(GameObject parent, string name, Vector3 center, Vector3 size, float pitch, Material material)
    {
        GameObject east = Box(parent, $"{name}_E", center, size, material);
        east.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);

        GameObject west = Box(parent, $"{name}_W", new Vector3(-center.x, center.y, center.z), size, material);
        west.transform.localRotation = Quaternion.Euler(pitch, 0f, 0f);
    }

    private static GameObject AngledBox(GameObject parent, string name, Vector3 center, Vector3 size, float yawDegrees, Material material)
    {
        GameObject box = Box(parent, name, center, size, material);
        box.transform.localRotation = Quaternion.Euler(0f, yawDegrees, 0f);
        return box;
    }

    private static void MirrorAngled(GameObject parent, string name, Vector3 center, Vector3 size, float yawDegrees, Material material)
    {
        AngledBox(parent, $"{name}_E", center, size, yawDegrees, material);
        AngledBox(parent, $"{name}_W", new Vector3(-center.x, center.y, center.z), size, -yawDegrees, material);
    }

    private static void CurvedWall(GameObject parent, string name, Vector3 center, float radius,
        float startDeg, float endDeg, float height, float thickness, Material material)
    {
        float sweep = Mathf.Abs(endDeg - startDeg);
        int segments = Mathf.Max(2, Mathf.CeilToInt(sweep * Mathf.Deg2Rad * radius / 3f));
        float step = (endDeg - startDeg) / segments;
        float chord = 2f * radius * Mathf.Sin(Mathf.Abs(step) * Mathf.Deg2Rad * 0.5f) + 0.3f;

        for (int i = 0; i < segments; i++)
        {
            float mid = startDeg + step * (i + 0.5f);
            float rad = mid * Mathf.Deg2Rad;
            Vector3 pos = new Vector3(center.x + Mathf.Cos(rad) * radius, center.y, center.z + Mathf.Sin(rad) * radius);

            GameObject seg = Box(parent, $"{name}_{i}", pos, new Vector3(thickness, height, chord), material);
            seg.transform.localRotation = Quaternion.Euler(0f, -mid, 0f);
        }
    }

    private static void MirrorCurvedWall(GameObject parent, string name, Vector3 center, float radius,
        float startDeg, float endDeg, float height, float thickness, Material material)
    {
        CurvedWall(parent, $"{name}_E", center, radius, startDeg, endDeg, height, thickness, material);
        CurvedWall(parent, $"{name}_W", new Vector3(-center.x, center.y, center.z), radius,
            180f - startDeg, 180f - endDeg, height, thickness, material);
    }

    private static GameObject Pillar(GameObject parent, string name, Vector3 groundCenter, float radius, float height, Material material)
    {
        GameObject cyl = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cyl.name = name;
        cyl.transform.SetParent(parent.transform, false);
        cyl.transform.localPosition = groundCenter + new Vector3(0f, height * 0.5f, 0f);
        cyl.transform.localScale = new Vector3(radius * 2f, height * 0.5f, radius * 2f);
        cyl.GetComponent<MeshRenderer>().sharedMaterial = material;
        cyl.isStatic = true;
        return cyl;
    }

    private static void MirrorPillar(GameObject parent, string name, Vector3 groundCenter, float radius, float height, Material material)
    {
        Pillar(parent, $"{name}_E", groundCenter, radius, height, material);
        Pillar(parent, $"{name}_W", new Vector3(-groundCenter.x, groundCenter.y, groundCenter.z), radius, height, material);
    }

    private static void ChamferedArena(GameObject map, string prefix, float halfX, float halfZ, float chamfer, float height, Material wall)
    {
        float y = height * 0.5f;
        const float t = 1f;

        Box(map, $"{prefix}_North", new Vector3(0f, y, halfZ), new Vector3((halfX - chamfer) * 2f, height, t), wall);
        Box(map, $"{prefix}_South", new Vector3(0f, y, -halfZ), new Vector3((halfX - chamfer) * 2f, height, t), wall);
        Box(map, $"{prefix}_East", new Vector3(halfX, y, 0f), new Vector3(t, height, (halfZ - chamfer) * 2f), wall);
        Box(map, $"{prefix}_West", new Vector3(-halfX, y, 0f), new Vector3(t, height, (halfZ - chamfer) * 2f), wall);

        float diag = chamfer * Mathf.Sqrt(2f) + t;
        float cx = halfX - chamfer * 0.5f;
        float cz = halfZ - chamfer * 0.5f;

        AngledBox(map, $"{prefix}_NE", new Vector3(cx, y, cz), new Vector3(t, height, diag), 135f, wall);
        AngledBox(map, $"{prefix}_NW", new Vector3(-cx, y, cz), new Vector3(t, height, diag), -135f, wall);
        AngledBox(map, $"{prefix}_SE", new Vector3(cx, y, -cz), new Vector3(t, height, diag), 45f, wall);
        AngledBox(map, $"{prefix}_SW", new Vector3(-cx, y, -cz), new Vector3(t, height, diag), -45f, wall);
    }

    private static GameObject Box(GameObject parent, string name, Vector3 center, Vector3 size, Material material, bool markStatic = true)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.transform.SetParent(parent.transform, false);
        box.transform.localPosition = center;
        box.transform.localScale = size;
        box.GetComponent<MeshRenderer>().sharedMaterial = material;
        box.isStatic = markStatic;
        return box;
    }

    private static void MirrorPanel(GameObject parent, string name, Vector3 center, Vector3 size, AdvantageAssetBuilder.Palette palette)
    {
        Panel(parent, $"{name}_E", center, size, palette, Team.Defenders);
        Panel(parent, $"{name}_W", new Vector3(-center.x, center.y, center.z), size, palette, Team.Attackers);
    }

    private static GameObject Panel(GameObject parent, string name, Vector3 center, Vector3 size, AdvantageAssetBuilder.Palette palette, Team owningTeam)
    {
        GameObject panel = GameObject.CreatePrimitive(PrimitiveType.Cube);
        panel.name = name;
        panel.transform.SetParent(parent.transform, false);
        panel.transform.localPosition = center;
        panel.transform.localScale = size;

        panel.GetComponent<MeshRenderer>().sharedMaterial = palette.panelFriendly;

        panel.AddComponent<NavMeshModifier>().ignoreFromBuild = true;

        SpawnPanel spawnPanel = panel.AddComponent<SpawnPanel>();
        WireEnum(spawnPanel, "owningTeam", (int)owningTeam);
        Wire(spawnPanel, "friendlyMaterial", palette.panelFriendly);
        Wire(spawnPanel, "hostileMaterial", palette.panelHostile);

        return panel;
    }

    private static void CreateSpawn(GameObject parent, Team team, Vector3 position, float yaw, int index)
    {
        GameObject spawn = new GameObject($"Spawn_{team}_{index}");
        spawn.transform.SetParent(parent.transform, false);
        spawn.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
        spawn.AddComponent<SpawnPoint>().SetTeam(team);
    }

    private static void CreateMatchManager(AdvantageAssetBuilder.Palette palette)
    {
        GameObject go = new GameObject("MatchManager");
        MatchManager manager = go.AddComponent<MatchManager>();

        SerializedObject serialized = new SerializedObject(manager);
        serialized.FindProperty("playerPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<PlayerController>($"{AdvantageAssetBuilder.PrefabsFolder}/Player.prefab");
        serialized.FindProperty("botPrefab").objectReferenceValue =
            AssetDatabase.LoadAssetAtPath<BotBrain>($"{AdvantageAssetBuilder.PrefabsFolder}/Bot.prefab");
        serialized.FindProperty("attackerMaterial").objectReferenceValue = palette.attacker;
        serialized.FindProperty("defenderMaterial").objectReferenceValue = palette.defender;
        serialized.ApplyModifiedPropertiesWithoutUndo();
    }

    private static void CreateHud()
    {
        InstantiateUi("HudCanvas");
        InstantiateUi("PauseMenuCanvas");
        new GameObject("AbilityHudOverlay").AddComponent<AbilityHudOverlay>();
        new GameObject("AllyRosterOverlay").AddComponent<AllyRosterOverlay>();
    }

    private static GameObject InstantiateUi(string prefabName)
    {
        string path = $"{UiPrefabsFolder}/{prefabName}.prefab";
        GameObject prefab = AssetDatabase.LoadAssetAtPath<GameObject>(path);

        if (prefab == null)
        {
            Debug.LogError($"[Advantage] UI prefab missing: {path}");
            return null;
        }

        return (GameObject)PrefabUtility.InstantiatePrefab(prefab);
    }

    private static void CreateEventSystem()
    {
        GameObject go = new GameObject("EventSystem");
        go.AddComponent<EventSystem>();

        InputSystemUIInputModule module = go.AddComponent<InputSystemUIInputModule>();
        InputActionAsset actions = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsAssetPath);

        if (actions == null)
        {
            Debug.LogWarning("[Advantage] InputSystem_Actions.inputactions not found - UI input will not work.");
            return;
        }

        module.actionsAsset = actions;
        module.point = Reference("UI/Point");
        module.leftClick = Reference("UI/Click");
        module.rightClick = Reference("UI/RightClick");
        module.middleClick = Reference("UI/MiddleClick");
        module.scrollWheel = Reference("UI/ScrollWheel");
        module.move = Reference("UI/Navigate");
        module.submit = Reference("UI/Submit");
        module.cancel = Reference("UI/Cancel");
    }

    private static InputActionReference Reference(string actionPath)
    {
        foreach (Object asset in AssetDatabase.LoadAllAssetsAtPath(ActionsAssetPath))
        {
            if (asset is InputActionReference reference &&
                reference.action != null &&
                reference.action.actionMap != null &&
                $"{reference.action.actionMap.name}/{reference.action.name}" == actionPath)
            {
                return reference;
            }
        }

        return null;
    }

    private static void RegisterBuildSettings()
    {
        List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>
        {
            new EditorBuildSettingsScene(MenuPath, true),
            new EditorBuildSettingsScene(ArenaPath, true),
            new EditorBuildSettingsScene(ConvergencePath, true),
            new EditorBuildSettingsScene(ExtractionPath, true),
            new EditorBuildSettingsScene(BreachPath, true),
            new EditorBuildSettingsScene(DominionPath, true)
        };

        EditorBuildSettings.scenes = scenes.ToArray();
        SyncActiveBuildProfileScenes(scenes);
    }

    private static void SyncActiveBuildProfileScenes(List<EditorBuildSettingsScene> scenes)
    {
        try
        {
            System.Type profileType = System.Type.GetType("UnityEditor.Build.Profile.BuildProfile, UnityEditor");
            if (profileType == null)
            {
                return;
            }

            System.Reflection.MethodInfo getActive = profileType.GetMethod(
                "GetActiveBuildProfile", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static);
            object activeProfile = getActive?.Invoke(null, null);
            if (activeProfile == null)
            {
                return;
            }

            System.Reflection.PropertyInfo scenesProperty = profileType.GetProperty(
                "scenes", System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);
            if (scenesProperty == null || !scenesProperty.CanWrite)
            {
                Debug.LogWarning(
                    "[Advantage] Unity's Build Profile API looks different than expected - could not confirm the " +
                    "active build profile's own scene list is in sync. If a mode scene still refuses to load, open " +
                    "File > Build Profiles and add it (or disable that profile's scene-list override) manually.");
                return;
            }

            scenesProperty.SetValue(activeProfile, scenes.ToArray());
            EditorUtility.SetDirty((Object)activeProfile);
            AssetDatabase.SaveAssets();
        }
        catch (System.Exception exception)
        {
            Debug.LogWarning(
                $"[Advantage] Could not sync the active Build Profile's scene list ({exception.Message}). If a mode " +
                "scene still refuses to load, open File > Build Profiles and add it manually.");
        }
    }
}
