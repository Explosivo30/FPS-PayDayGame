@'
using UnityEngine;

/// <summary>Veteran identity and spatial warning sounds. Geometry is authored in the dedicated model prefabs.</summary>
[DefaultExecutionOrder(90)]
public sealed class EnemySignals : MonoBehaviour
{
    AudioSource source;
    AudioClip normalTone, veteranTone;
    NormalEnemyStateMachine owner;
    bool paused;
    public int VeteranParts { get; private set; }
    public bool HasVeteranModel { get; private set; }
    public void SetVeteran()
    {
        if (HasVeteranModel) return;
        var rig = VeteranRig.Install(owner);
        if (rig == null) return;
        HasVeteranModel = true;
        VeteranParts = rig.GetComponentsInChildren<MeshRenderer>().Length;
    }
    void Awake()
    {
        owner = GetComponent<NormalEnemyStateMachine>();
        source = gameObject.AddComponent<AudioSource>(); source.playOnAwake = false; source.spatialBlend = 1;
        source.minDistance = 3; source.maxDistance = 28; source.rolloffMode = AudioRolloffMode.Linear; source.volume = .3f;
        normalTone = Tone(480, .18f, false); veteranTone = Tone(owner.IsMelee ? 180 : 780, owner.IsMelee ? .65f : .4f, true);
    }
    public static AudioClip Tone(float hz, float duration, bool doubleTone)
    {
        const int rate = 22050; var data = new float[Mathf.CeilToInt(rate * duration)];
        for (int i = 0; i < data.Length; i++)
        {
            float t = (float)i / rate, envelope = Mathf.Sin(Mathf.PI * i / data.Length);
            float gate = doubleTone ? (.45f + .55f * Mathf.Sin(t * 45) * Mathf.Sin(t * 45)) : 1;
            data[i] = Mathf.Sin(2 * Mathf.PI * (hz * t + 160 * t * t)) * envelope * gate * .3f;
        }
        var clip = AudioClip.Create("Robot warning", data.Length, 1, rate, false); clip.SetData(data, 0); return clip;
    }
    public void Attack(bool special) { if (!WeaponAction.CombatPaused) source.PlayOneShot(special ? veteranTone : normalTone); }
    void Update()
    {
        if (paused != WeaponAction.CombatPaused) { paused = WeaponAction.CombatPaused; if (paused) source.Pause(); else source.UnPause(); }
        if (owner.IsDead) source.Stop();
    }
    void OnDestroy()
    {
        if (normalTone != null) Destroy(normalTone); if (veteranTone != null) Destroy(veteranTone);
    }
}
'@ | Set-Content -LiteralPath 'Assets\Scripts\AI\EnemySignals.cs' -Encoding utf8
@'
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

/// <summary>Imports the full reference-authored veteran models without changing the normal prefabs.</summary>
public static class VeteranModelBuild
{
    const string Art = "Assets/Art/Veterans";
    const string Prefabs = "Assets/Resources/Veterans";
    [MenuItem("Tools/HELIX/Build Ariete and Aguja models")]
    public static void Configure()
    {
        Directory.CreateDirectory(Art + "/Materials"); Directory.CreateDirectory(Art + "/Meshes"); Directory.CreateDirectory(Prefabs);
        AssetDatabase.Refresh();
        var materials = new Dictionary<string, Material>();
        materials["Ivory"] = Mat("Ivory", new Color(.83f, .77f, .65f), .12f, .44f);
        materials["Graphite"] = Mat("Graphite", new Color(.045f, .055f, .068f), .30f, .48f);
        materials["Orange"] = Mat("Orange", new Color(.99f, .275f, .025f), .15f, .42f);
        materials["Steel"] = Mat("Steel", new Color(.13f, .15f, .18f), .60f, .36f);
        materials["Black"] = Mat("Black", new Color(.009f, .013f, .018f), .10f, .65f);
        materials["Amber"] = Mat("Amber", new Color(1, .32f, .015f), .05f, .30f, true);
        Make("Ariete", true, materials); Make("Aguja", false, materials);
        AssetDatabase.SaveAssets(); Debug.Log("AUTHORED VETERANS READY: Ariete + Aguja");
    }
    static Material Mat(string name, Color color, float metal, float roughness, bool emission = false)
    {
        string path = Art + "/Materials/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (material == null) { material = new Material(Shader.Find("Universal Render Pipeline/Lit")); AssetDatabase.CreateAsset(material, path); }
        material.SetColor("_BaseColor", color); material.SetFloat("_Metallic", metal); material.SetFloat("_Smoothness", 1 - roughness);
        if (emission) { material.EnableKeyword("_EMISSION"); material.SetColor("_EmissionColor", color * .8f); }
        material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
    }
    static Transform Joint(string name, Transform parent, Vector3 world)
    {
        var t = new GameObject(name).transform; t.SetParent(parent, false); t.position = world; return t;
    }
    static void Make(string name, bool melee, Dictionary<string, Material> materials)
    {
        string modelPath = Art + "/" + name + ".fbx";
        var importer = (ModelImporter)AssetImporter.GetAtPath(modelPath);
        importer.importAnimation = false; importer.bakeAxisConversion = true;
        importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
        importer.SaveAndReimport();
        var root = new GameObject(name + " visual"); var rig = root.AddComponent<VeteranRig>(); rig.modelName = name;
        var joints = new Dictionary<string, Transform>();
        if (melee)
        {
            joints["Pelvis"] = Joint("Pelvis", root.transform, new Vector3(0, 1.08f, 0));
            rig.torso = joints["Torso"] = Joint("Torso", root.transform, new Vector3(0, 1.35f, 0));
            rig.head = joints["Head"] = Joint("Head", rig.torso, new Vector3(0, 2.11f, 0));
            foreach (var side in new[] { "L", "R" })
            {
                float s = side == "L" ? -1 : 1;
                joints["UpperArm_" + side] = Joint("UpperArm_" + side, rig.torso, new Vector3(s * .65f, 1.94f, 0));
                joints["Forearm_" + side] = Joint("Forearm_" + side, joints["UpperArm_" + side], new Vector3(s * (side == "R" ? .79f : .75f), 1.43f, .01f));
                joints["Thigh_" + side] = Joint("Thigh_" + side, root.transform, new Vector3(s * .27f, 1.05f, 0));
                joints["Shin_" + side] = Joint("Shin_" + side, joints["Thigh_" + side], new Vector3(s * .38f, .63f, 0));
            }
            rig.leftArm = joints["UpperArm_L"]; rig.rightArm = joints["UpperArm_R"];
            rig.leftForearm = joints["Forearm_L"]; rig.rightForearm = joints["Forearm_R"];
            rig.leftThigh = joints["Thigh_L"]; rig.rightThigh = joints["Thigh_R"];
            rig.leftShin = joints["Shin_L"]; rig.rightShin = joints["Shin_R"];
        }
        else
        {
            joints["Body"] = root.transform;
            rig.gunPivot = joints["Gun"] = Joint("Aguja gun pivot", root.transform, new Vector3(0, 1.105f, .55f));
            rig.muzzle = Joint("Aguja muzzle", rig.gunPivot, new Vector3(0, 1.115f, 1.675f));
        }
        var model = Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>(modelPath), root.transform);
        var groups = new Dictionary<string, List<MeshFilter>>();
        foreach (var filter in model.GetComponentsInChildren<MeshFilter>())
        {
            string joint = joints.Keys.OrderByDescending(k => k.Length).FirstOrDefault(k => filter.name.StartsWith(k + "_", StringComparison.Ordinal));
            if (joint == null) throw new Exception("Unbound veteran part " + filter.name);
            var renderer = filter.GetComponent<MeshRenderer>();
            string materialName = renderer.sharedMaterial != null ? renderer.sharedMaterial.name : "";
            if (!materials.ContainsKey(materialName)) throw new Exception("Unknown veteran material " + materialName + " on " + filter.name);
            string key = joint + "|" + materialName;
            if (!groups.ContainsKey(key)) groups[key] = new List<MeshFilter>(); groups[key].Add(filter);
        }
        foreach (var pair in groups)
        {
            var key = pair.Key.Split('|'); var parent = joints[key[0]];
            string meshName = name + "_" + key[0] + "_" + key[1];
            var mesh = AssetDatabase.LoadAssetAtPath<Mesh>(Art + "/Meshes/" + meshName + ".asset");
            if (mesh == null) { mesh = new Mesh(); AssetDatabase.CreateAsset(mesh, Art + "/Meshes/" + meshName + ".asset"); }
            else mesh.Clear();
            mesh.indexFormat = IndexFormat.UInt32;
            mesh.CombineMeshes(pair.Value.Select(f => new CombineInstance { mesh = f.sharedMesh, transform = parent.worldToLocalMatrix * f.transform.localToWorldMatrix }).ToArray(), true, true);
            mesh.name = meshName; mesh.RecalculateBounds(); EditorUtility.SetDirty(mesh);
            var part = new GameObject(key[0] + "_" + key[1], typeof(MeshFilter), typeof(MeshRenderer)); part.layer = 6;
            part.transform.SetParent(parent, false); part.GetComponent<MeshFilter>().sharedMesh = mesh; part.GetComponent<MeshRenderer>().sharedMaterial = materials[key[1]];
            if (key[1] == "Ivory" || key[1] == "Graphite")
            {
                var collider = part.AddComponent<MeshCollider>(); collider.sharedMesh = mesh; collider.convex = true;
            }
        }
        Object.DestroyImmediate(model);
        foreach (var t in root.GetComponentsInChildren<Transform>(true)) t.gameObject.layer = 6;
        PrefabUtility.SaveAsPrefabAsset(root, Prefabs + "/" + name + "Visual.prefab"); Object.DestroyImmediate(root);
    }
    public static void Play() { Configure(); TowerValidation.Play(); }
    public static void Build() { Configure(); ShareBuild.BuildWindows(); }
    public static void ConfigureAndQuit() { Configure(); EditorApplication.Exit(0); }
}
'@ | Set-Content -LiteralPath 'Assets\Editor\Tower\VeteranModelBuild.cs' -Encoding utf8
$taskProcess=Start-Process -FilePath 'C:\Program Files\Unity\Hub\Editor\6000.0.58f2\Editor\Unity.exe' -ArgumentList @('-batchmode','-quit','-nographics','-projectPath','"D:\Unity\FPS-PayDayGame"','-executeMethod','VeteranModelBuild.ConfigureAndQuit','-logFile','"D:\Unity\FPS-PayDayGame\Artifacts\Veterans\import.log"') -WindowStyle Hidden -PassThru
Write-Output "Model import and rig build: $($taskProcess.Id)"