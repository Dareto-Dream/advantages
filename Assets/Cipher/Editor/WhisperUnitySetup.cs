using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

public sealed class WhisperImporter : AssetPostprocessor
{
    const string Folder = "Assets/Cipher/Weapons/Whisper_AR/";
    void OnPreprocessModel()
    {
        if (assetPath != Folder + "Whisper_AR.fbx") return;
        var m = (ModelImporter)assetImporter;
        m.animationType = ModelImporterAnimationType.None; m.importAnimation = false;
        m.importCameras = false; m.importLights = false; m.addCollider = false;
        m.globalScale = 1; m.useFileScale = true; m.importNormals = ModelImporterNormals.Import;
        m.importTangents = ModelImporterTangents.CalculateMikk;
        m.materialImportMode = ModelImporterMaterialImportMode.None;
    }
    void OnPreprocessTexture()
    {
        if (!assetPath.StartsWith(Folder + "Textures/", StringComparison.Ordinal)) return;
        var t = (TextureImporter)assetImporter;
        t.textureType = assetPath.EndsWith("_Normal.png", StringComparison.Ordinal) ? TextureImporterType.NormalMap : TextureImporterType.Default;
        t.sRGBTexture = assetPath.Contains("_BaseColor") || assetPath.Contains("_Emission");
        t.alphaSource = TextureImporterAlphaSource.FromInput; t.alphaIsTransparency = false; t.maxTextureSize = 2048;
    }
}

public static class WhisperUnitySetup
{
    const string Root = "Assets/Cipher";
    const string Folder = Root + "/Weapons/Whisper_AR";
    static Transform Find(GameObject go, string name) => go.GetComponentsInChildren<Transform>(true).First(t => t.name == name);
    static Texture2D Tex(string name) => AssetDatabase.LoadAssetAtPath<Texture2D>(Folder + "/Textures/Whisper_" + name + ".png");
    static Material Material(string name, Shader shader)
    {
        string path = Folder + "/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null) { m = new Material(shader); AssetDatabase.CreateAsset(m, path); }
        m.shader = shader; return m;
    }
    [MenuItem("Tools/Cipher/Create Whisper AR and holding prefab")]
    public static void Build()
    {
        var shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null) throw new InvalidOperationException("Install Universal RP first.");
        var mat = Material("Whisper_URP", shader);
        mat.SetTexture("_BaseMap", Tex("BaseColor")); mat.SetColor("_BaseColor", Color.white);
        mat.SetTexture("_BumpMap", Tex("Normal")); mat.SetFloat("_BumpScale", 1); mat.EnableKeyword("_NORMALMAP");
        mat.SetTexture("_MetallicGlossMap", Tex("MetallicSmoothness")); mat.SetFloat("_Metallic", 1); mat.SetFloat("_Smoothness", 1);
        mat.SetFloat("_SmoothnessTextureChannel", 0); mat.EnableKeyword("_METALLICSPECGLOSSMAP");
        mat.SetTexture("_EmissionMap", Tex("Emission")); mat.SetColor("_EmissionColor", Color.white * 2); mat.EnableKeyword("_EMISSION");
        var lens = Material("Whisper_Lens_URP", shader);
        lens.SetColor("_BaseColor", new Color(.04f,.32f,.39f,.18f)); lens.SetFloat("_Metallic", .1f); lens.SetFloat("_Smoothness", .86f);
        lens.SetFloat("_Surface", 1); lens.SetFloat("_Blend", 0); lens.SetFloat("_ZWrite", 0); lens.SetFloat("_Cull", 0);
        lens.SetFloat("_SrcBlend", (float)BlendMode.SrcAlpha); lens.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
        lens.SetFloat("_SrcBlendAlpha", (float)BlendMode.One); lens.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
        lens.EnableKeyword("_SURFACE_TYPE_TRANSPARENT"); lens.SetOverrideTag("RenderType", "Transparent"); lens.renderQueue = (int)RenderQueue.Transparent;
        lens.SetShaderPassEnabled("ShadowCaster", false);
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(Folder + "/Whisper_AR.fbx");
        if (model == null) throw new InvalidOperationException("Whisper_AR.fbx is missing.");
        var weapon = (GameObject)PrefabUtility.InstantiatePrefab(model); weapon.name = "Whisper_AR";
        foreach (var r in weapon.GetComponentsInChildren<MeshRenderer>())
            r.sharedMaterials = Enumerable.Repeat(r.name.Contains("ReflexLens") ? lens : mat, r.sharedMaterials.Length).ToArray();
        var gunPrefab = PrefabUtility.SaveAsPrefabAsset(weapon, Folder + "/Whisper_AR.prefab");
        UnityEngine.Object.DestroyImmediate(weapon);
        var cipherPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Root + "/Cipher.prefab");
        var cipher = (GameObject)PrefabUtility.InstantiatePrefab(cipherPrefab); cipher.name = "Cipher_WithWhisper";
        var animator = cipher.GetComponent<Animator>(); animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
        var clip = AssetDatabase.LoadAllAssetsAtPath(Root + "/Cipher.fbx").OfType<AnimationClip>().First(c => !c.name.StartsWith("__preview__", StringComparison.Ordinal) && c.name.EndsWith("Cipher_WhisperHold", StringComparison.Ordinal));
        string controllerPath = Root + "/Cipher_Whisper.controller";
        var controller = AssetDatabase.LoadAssetAtPath<AnimatorController>(controllerPath);
        if (controller == null) controller = AnimatorController.CreateAnimatorControllerAtPath(controllerPath);
        var layers = controller.layers; layers[0].iKPass = true; controller.layers = layers;
        var sm = controller.layers[0].stateMachine;
        foreach (var child in sm.states) sm.RemoveState(child.state);
        var state = sm.AddState("Cipher_WhisperHold"); state.motion = clip; sm.defaultState = state;
        animator.runtimeAnimatorController = controller; animator.Rebind();
        var equipped = (GameObject)PrefabUtility.InstantiatePrefab(gunPrefab);
        var grip = Find(equipped, "Whisper_RightGrip"); var support = Find(equipped, "Whisper_LeftSupport");
        Vector3 localGripPosition = equipped.transform.InverseTransformPoint(grip.position);
        Quaternion localGripRotation = Quaternion.Inverse(equipped.transform.rotation) * grip.rotation;
        equipped.transform.SetParent(animator.GetBoneTransform(HumanBodyBones.RightHand), false);
        equipped.transform.localRotation = Quaternion.Inverse(localGripRotation);
        equipped.transform.localPosition = -(equipped.transform.localRotation * localGripPosition);
        equipped.transform.localScale = Vector3.one;
        var ik = cipher.GetComponent<CipherWeaponGrip>() ?? cipher.AddComponent<CipherWeaponGrip>(); ik.leftHandTarget = support; ik.weight = 1;
        var graph = PlayableGraph.Create("WhisperHoldingValidation"); graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
        var playable = AnimationClipPlayable.Create(graph, clip); playable.SetApplyPlayableIK(true);
        var output = AnimationPlayableOutput.Create(graph, "Cipher", animator); output.SetSourcePlayable(playable); graph.Play();
        playable.SetTime(clip.length * .5); graph.Evaluate(.016f); graph.Evaluate(.016f);
        float rightError = Vector3.Distance(grip.position, animator.GetBoneTransform(HumanBodyBones.RightHand).position);
        float leftError = Vector3.Distance(support.position, animator.GetBoneTransform(HumanBodyBones.LeftHand).position);
        var muzzle = Find(equipped, "Whisper_Muzzle"); var suppressor = Find(equipped, "Whisper_Suppressor");
        float muzzleAlignment = Vector3.Dot(muzzle.forward, (muzzle.position - suppressor.position).normalized);
        var renderers = equipped.GetComponentsInChildren<MeshFilter>();
        if (rightError > .001f || leftError > .04f || muzzleAlignment < .999f || renderers.Length != 5 || renderers.Any(r => r.sharedMesh.uv.Length != r.sharedMesh.vertexCount))
            throw new InvalidOperationException($"Whisper fit failed: right={rightError}, left={leftError}, meshes={renderers.Length}");
        PrefabUtility.SaveAsPrefabAsset(cipher, Root + "/Cipher_WithWhisper.prefab");
        var report = new Report { unityVersion = Application.unityVersion, weaponMeshes = renderers.Length, weaponTriangles = renderers.Sum(r => r.sharedMesh.triangles.Length / 3),
            rightGripErrorMeters = rightError, leftGripErrorMeters = leftError, humanoid = animator.avatar.isValid && animator.avatar.isHuman,
            holdingClip = clip.name, defaultState = "Cipher_WhisperHold", supportHandIK = true, muzzleForwardAlignment = muzzleAlignment };
        string reportPath = Application.isBatchMode ? Path.GetFullPath(Path.Combine(Application.dataPath, "../../whisper-unity-validation.json")) : Folder + "/Validation.json";
        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
        graph.Destroy(); UnityEngine.Object.DestroyImmediate(cipher);
        EditorUtility.SetDirty(mat); EditorUtility.SetDirty(lens); AssetDatabase.SaveAssets();
        Debug.Log("WHISPER_VALIDATION_SUCCESS " + JsonUtility.ToJson(report));
    }
    [Serializable] class Report
    {
        public string unityVersion; public int weaponMeshes; public int weaponTriangles;
        public float rightGripErrorMeters; public float leftGripErrorMeters; public bool humanoid;
        public string holdingClip; public string defaultState; public bool supportHandIK;
        public float muzzleForwardAlignment;
    }
}
