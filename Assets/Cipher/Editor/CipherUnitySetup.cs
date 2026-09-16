using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

public sealed class CipherImporter : AssetPostprocessor
{
    void OnPreprocessAnimation()
    {
        if (assetPath != "Assets/Cipher/Cipher.fbx") return;
        var importer = (ModelImporter)assetImporter;
        var clips = importer.defaultClipAnimations;
        foreach (var clip in clips) clip.loopTime = clip.name.EndsWith("Cipher_Idle", StringComparison.Ordinal);
        importer.clipAnimations = clips;
    }
    void OnPreprocessModel()
    {
        if (assetPath != "Assets/Cipher/Cipher.fbx") return;
        var importer = (ModelImporter)assetImporter;
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.importAnimation = true;
        importer.importCameras = false;
        importer.importLights = false;
        importer.addCollider = false;
        importer.globalScale = 1;
        importer.useFileScale = true;
        importer.importNormals = ModelImporterNormals.Import;
        importer.importTangents = ModelImporterTangents.CalculateMikk;
        importer.skinWeights = ModelImporterSkinWeights.Custom;
        importer.maxBonesPerVertex = 4;
        importer.minBoneWeight = 0.00001f;
        importer.optimizeGameObjects = false;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        var desc = importer.humanDescription;
        desc.human = HumanTrait.BoneName.Where(n => n != "Jaw" && !n.Contains("UpperChest")).Select(n => new HumanBone {
            humanName = n, boneName = n.Replace(" ", ""), limit = new HumanLimit { useDefaultValues = true }
        }).Concat(HumanTrait.BoneName.Where(n => n.Contains("UpperChest")).Select(n => new HumanBone {
            humanName = n, boneName = "UpperChest", limit = new HumanLimit { useDefaultValues = true }
        })).ToArray();
        desc.upperArmTwist = 0.5f; desc.lowerArmTwist = 0.5f;
        desc.upperLegTwist = 0.5f; desc.lowerLegTwist = 0.5f;
        desc.armStretch = 0; desc.legStretch = 0; desc.feetSpacing = 0;
        importer.humanDescription = desc;
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith("Assets/Cipher/Textures/", StringComparison.Ordinal)) return;
        var importer = (TextureImporter)assetImporter;
        bool normal = assetPath.EndsWith("_Normal.png", StringComparison.Ordinal);
        importer.textureType = normal ? TextureImporterType.NormalMap : TextureImporterType.Default;
        importer.sRGBTexture = assetPath.Contains("_BaseColor") || assetPath.Contains("_Emission");
        importer.maxTextureSize = 4096;
        importer.alphaSource = TextureImporterAlphaSource.FromInput;
        importer.alphaIsTransparency = false;
    }
}

public static class CipherUnitySetup
{
    const string Root = "Assets/Cipher";
    static Texture2D Texture(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Root + "/Textures/Cipher_" + name + ".png");

    [MenuItem("Tools/Cipher/Create URP material and prefab")]
    public static void Build()
    {
        AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Install Universal RP before running Cipher setup.");
        string materialPath = Root + "/Cipher_URP.mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(materialPath);
        if (material == null) { material = new Material(shader); AssetDatabase.CreateAsset(material, materialPath); }
        material.shader = shader;
        material.SetTexture("_BaseMap", Texture("BaseColor"));
        material.SetColor("_BaseColor", Color.white);
        material.SetTexture("_BumpMap", Texture("Normal"));
        material.SetFloat("_BumpScale", 1);
        material.EnableKeyword("_NORMALMAP");
        material.SetTexture("_MetallicGlossMap", Texture("MetallicSmoothness"));
        material.SetFloat("_Metallic", 1); material.SetFloat("_Smoothness", 1);
        material.SetFloat("_SmoothnessTextureChannel", 0);
        material.EnableKeyword("_METALLICSPECGLOSSMAP");
        material.SetTexture("_EmissionMap", Texture("Emission"));
        material.SetColor("_EmissionColor", Color.white * 1.6f);
        material.EnableKeyword("_EMISSION");
        material.SetFloat("_Cull", 2);
        material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.BakedEmissive;
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Cipher.fbx");
        if (model == null) throw new InvalidOperationException("Cipher.fbx has not imported.");
        var avatar = AssetDatabase.LoadAllAssetsAtPath(Root + "/Cipher.fbx").OfType<Avatar>().FirstOrDefault();
        if (avatar == null || !avatar.isValid || !avatar.isHuman) throw new InvalidOperationException("Cipher Humanoid Avatar validation failed.");
        var clips = AssetDatabase.LoadAllAssetsAtPath(Root + "/Cipher.fbx").OfType<AnimationClip>().Where(c => !c.name.StartsWith("__preview__")).ToArray();
        string controllerPath = Root + "/Cipher_Starter.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var sm = controller.layers[0].stateMachine;
        foreach (var child in sm.states) sm.RemoveState(child.state);
        foreach (var clip in clips)
        {
            string simple = clip.name.Substring(clip.name.LastIndexOf("Cipher_", StringComparison.Ordinal));
            var state = sm.AddState(simple); state.motion = clip;
            if (simple == "Cipher_Idle") sm.defaultState = state;
            if (simple == "Cipher_Idle")
            {
                var settings = AnimationUtility.GetAnimationClipSettings(clip); settings.loopTime = true;
                AnimationUtility.SetAnimationClipSettings(clip, settings);
            }
        }
        var go = (GameObject)PrefabUtility.InstantiatePrefab(model);
        go.name = "Cipher";
        foreach (var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>())
        {
            renderer.sharedMaterials = Enumerable.Repeat(material, renderer.sharedMaterials.Length).ToArray();
            renderer.quality = SkinQuality.Bone4;
            renderer.updateWhenOffscreen = false;
        }
        var animator = go.GetComponent<Animator>() ?? go.AddComponent<Animator>();
        animator.avatar = avatar; animator.runtimeAnimatorController = controller; animator.applyRootMotion = false;
        PrefabUtility.SaveAsPrefabAsset(go, Root + "/Cipher.prefab");
        Validate(go, clips);
        UnityEngine.Object.DestroyImmediate(go);
        EditorUtility.SetDirty(material); AssetDatabase.SaveAssets();
        if (AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Weapons/Whisper_AR/Whisper_AR.fbx") != null) WhisperUnitySetup.Build();
        if (Application.isBatchMode)
        {
            string package = Path.GetFullPath(Path.Combine(Application.dataPath, "../../Cipher_URP.unitypackage"));
            AssetDatabase.ExportPackage(Root, package, ExportPackageOptions.Recurse);
        }
        Debug.Log("CIPHER_VALIDATION_SUCCESS: URP material, humanoid avatar, prefab, and package created.");
    }
    [Serializable] class Report
    {
        public string unityVersion; public bool validAvatar; public bool humanoid; public int skinnedMeshes;
        public int mappedBones; public string[] clips; public string[] missingRequiredBones;
        public float heightMeters; public bool finitePoseSamples; public int triangles;
        public float scanHandTravelMeters;
    }
    static void Validate(GameObject go, AnimationClip[] clips)
    {
        var animator = go.GetComponent<Animator>();
        var renderers = go.GetComponentsInChildren<SkinnedMeshRenderer>();
        var required = new[] { HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Head,
            HumanBodyBones.LeftUpperArm, HumanBodyBones.RightUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightLowerArm,
            HumanBodyBones.LeftHand, HumanBodyBones.RightHand, HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg,
            HumanBodyBones.LeftLowerLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.LeftFoot, HumanBodyBones.RightFoot };
        var missing = required.Where(b => animator.GetBoneTransform(b) == null).Select(b => b.ToString()).ToArray();
        var report = new Report { unityVersion = Application.unityVersion, validAvatar = animator.avatar.isValid,
            humanoid = animator.avatar.isHuman, skinnedMeshes = renderers.Length, missingRequiredBones = missing,
            clips = clips.Select(c => c.name).ToArray(), finitePoseSamples = true,
            triangles = renderers.Sum(r => r.sharedMesh.triangles.Length / 3) };
        for (int i = 0; i < (int)HumanBodyBones.LastBone; ++i) if (animator.GetBoneTransform((HumanBodyBones)i) != null) report.mappedBones++;
        float ymin = float.PositiveInfinity, ymax = float.NegativeInfinity;
        foreach (var r in renderers)
        {
            var baked = new Mesh(); r.BakeMesh(baked);
            foreach (var p in baked.vertices) { var wp = r.transform.TransformPoint(p); ymin = Mathf.Min(ymin, wp.y); ymax = Mathf.Max(ymax, wp.y); }
            UnityEngine.Object.DestroyImmediate(baked);
        }
        report.heightMeters = ymax - ymin;
        Vector3 relaxedHand = Vector3.zero, scanHand = Vector3.zero;
        animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        foreach (var clip in clips)
        {
            animator.Rebind();
            var graph = PlayableGraph.Create("CipherValidation");
            graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
            var playable = AnimationClipPlayable.Create(graph, clip);
            var output = AnimationPlayableOutput.Create(graph, "Cipher", animator);
            output.SetSourcePlayable(playable); graph.Play();
            playable.SetTime(Mathf.Min(clip.length * .5f, 1)); graph.Evaluate(.001f);
            if (clip.name.EndsWith("Cipher_Relaxed", StringComparison.Ordinal)) relaxedHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
            if (clip.name.EndsWith("Cipher_Scan", StringComparison.Ordinal)) scanHand = animator.GetBoneTransform(HumanBodyBones.RightHand).position;
            foreach (var t in go.GetComponentsInChildren<Transform>())
                if (float.IsNaN(t.position.sqrMagnitude) || float.IsInfinity(t.position.sqrMagnitude)) report.finitePoseSamples = false;
            foreach (var renderer in renderers)
            {
                var baked = new Mesh(); renderer.BakeMesh(baked);
                if (baked.vertices.Any(p => float.IsNaN(p.sqrMagnitude) || float.IsInfinity(p.sqrMagnitude))) report.finitePoseSamples = false;
                UnityEngine.Object.DestroyImmediate(baked);
            }
            graph.Destroy();
        }
        report.scanHandTravelMeters = Vector3.Distance(relaxedHand, scanHand);
        if (missing.Length != 0 || !report.finitePoseSamples || report.heightMeters < 1.65f || report.heightMeters > 1.95f || report.scanHandTravelMeters < .1f)
            throw new InvalidOperationException(JsonUtility.ToJson(report));
        string path = Application.isBatchMode ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../unity-validation.json")) : Root + "/Validation.json";
        File.WriteAllText(path, JsonUtility.ToJson(report, true));
        Debug.Log(JsonUtility.ToJson(report, true));
    }
}
