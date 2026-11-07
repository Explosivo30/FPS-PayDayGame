using System;
using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

/// <summary>Builds a scene-owned palette and pipeline around Edgar's existing shader assets.</summary>
public static class AlphaDistrictArt
{
    public const string ArtPath = "Assets/Art/AlphaDistrict/Rendering";
    const string ToonPath = "Assets/Shader/ShaderToon/ToonShader.shadergraph";
    const string OutlinePath = "Assets/Shader/Outline/Outlines.mat";
    const string TonePath = "Assets/AnimeTonemapper/AnimeTonemapper.shader";
    public sealed class Palette
    {
        public Material floor, wall, ink, jade, ivory, copper, signal, hazard, foliage, destructible, fractureInside;
    }
    public static Palette CreateMaterials()
    {
        Directory.CreateDirectory(ArtPath); AssetDatabase.Refresh();
        return new Palette
        {
            floor = Pigment("Deck petrol", new Color(.18f,.30f,.38f)),
            wall = Pigment("Enamel sky", new Color(.49f,.69f,.79f)),
            ink = Pigment("Deep indigo", new Color(.075f,.105f,.19f)),
            jade = Pigment("Machine teal", new Color(.09f,.52f,.55f)),
            ivory = Pigment("Enamel cream", new Color(.91f,.88f,.73f)),
            copper = Pigment("Conduit orange", new Color(.91f,.36f,.11f)),
            signal = Pigment("Signal mint", new Color(.19f,.92f,.63f), true),
            hazard = Pigment("Hazard yellow", new Color(1,.76f,.14f), true),
            foliage = Pigment("Leaf jade", new Color(.32f,.72f,.33f)),
            destructible = Pigment("Breakable coral", new Color(.96f,.28f,.22f)),
            fractureInside = Pigment("Fresh orange ceramic", new Color(1,.65f,.29f))
        };
    }
    static Material Pigment(string name, Color color, bool luminous = false)
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>(ToonPath);
        if (!shader) throw new InvalidOperationException("The project's original ToonShader graph is unavailable: " + ToonPath);
        string path = ArtPath + "/" + name + ".mat";
        var material = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (!material) { material = new Material(shader); AssetDatabase.CreateAsset(material, path); }
        material.shader = shader;
        string texturePath = ArtPath + "/" + name + " pigment.asset";
        var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
        if (!texture)
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = name + " pigment", filterMode = FilterMode.Point };
            AssetDatabase.CreateAsset(texture, texturePath);
        }
        texture.SetPixel(0, 0, color); texture.Apply(); EditorUtility.SetDirty(texture);
        material.SetTexture("_MainTexture", texture);
        material.SetFloat("_ToonRampOffset", .50f);
        material.SetFloat("_ToonRampSmoothness", .018f);
        material.SetColor("_ToonRampTinting", luminous ? new Color(.9f,.9f,.9f,1) : new Color(.40f,.44f,.55f,1));
        material.SetFloat("_RimLightBrightness", luminous ? .04f : .08f);
        material.SetFloat("_RimPower", 5);
        material.enableInstancing = true; EditorUtility.SetDirty(material); return material;
    }
    public static AlphaDistrictVisuals CreateSetup(Transform root)
    {
        var palette = CreateMaterials();
        var renderer = BuildRenderer(); var pipeline = BuildPipeline(renderer);
        var visuals = root.GetComponent<AlphaDistrictVisuals>(); if (!visuals) visuals = root.gameObject.AddComponent<AlphaDistrictVisuals>();
        visuals.pipeline = pipeline; visuals.actorTemplate = palette.ivory;
        var volumeRoot = new GameObject("District / original anime volume"); volumeRoot.transform.SetParent(root, false);
        var volume = volumeRoot.AddComponent<Volume>(); volume.isGlobal = true; volume.priority = 100;
        var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(ArtPath + "/District volume.asset");
        if (!profile) { profile = ScriptableObject.CreateInstance<VolumeProfile>(); AssetDatabase.CreateAsset(profile, ArtPath + "/District volume.asset"); }
        var anime = Get<AnimeTonemapper>(profile); anime.active = true;
        anime.exposure.Override(1.18f); anime.contrast.Override(1.03f); anime.saturation.Override(1.12f); anime.neonBoost.Override(1.06f);
        anime.shadowHueShift.Override(.64f); anime.shadowHueStr.Override(.25f); anime.shadowThreshold.Override(.24f);
        anime.shadowColor.Override(new Color(.07f,.10f,.22f)); anime.highlightSoft.Override(.72f);
        anime.highlightHueShift.Override(.10f); anime.highlightHueStr.Override(.08f); anime.highlightThreshold.Override(.8f);
        // Surface shading supplies the banding. Do not quantize the complete image a second time.
        anime.celBanding.Override(false); anime.celBands.Override(3); anime.celHardness.Override(.85f);
        Get<Tonemapping>(profile).mode.Override(TonemappingMode.None);
        Get<Bloom>(profile).intensity.Override(.04f);
        Get<DepthOfField>(profile).active = false;
        Get<MotionBlur>(profile).intensity.Override(0);
        foreach (var component in profile.components)
        {
            if (string.IsNullOrEmpty(AssetDatabase.GetAssetPath(component))) AssetDatabase.AddObjectToAsset(component, profile);
            EditorUtility.SetDirty(component);
        }
        EditorUtility.SetDirty(profile); volume.sharedProfile = profile;
        var lightRoot = new GameObject("District / sharp sun"); lightRoot.transform.SetParent(root, false);
        var sun = lightRoot.AddComponent<Light>(); sun.type = LightType.Directional; sun.intensity = 1.25f;
        sun.color = new Color(1,.97f,.87f); sun.transform.rotation = Quaternion.Euler(48,-34,0); sun.shadows = LightShadows.Hard;
        RenderSettings.sun = sun; RenderSettings.skybox = null; RenderSettings.ambientMode = AmbientMode.Trilight;
        RenderSettings.ambientSkyColor = new Color(.45f,.58f,.74f); RenderSettings.ambientEquatorColor = new Color(.25f,.31f,.40f);
        RenderSettings.ambientGroundColor = new Color(.13f,.18f,.26f); RenderSettings.reflectionIntensity = .12f;
        RenderSettings.fog = true; RenderSettings.fogMode = FogMode.Linear; RenderSettings.fogColor = new Color(.40f,.60f,.72f);
        RenderSettings.fogStartDistance = 90; RenderSettings.fogEndDistance = 190;
        AssetDatabase.SaveAssets(); return visuals;
    }
    static T Get<T>(VolumeProfile profile) where T : VolumeComponent
    { return profile.TryGet(out T value) ? value : profile.Add<T>(true); }
    static UniversalRendererData BuildRenderer()
    {
        string path = ArtPath + "/District renderer.asset";
        var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
        if (!renderer)
        {
            var original = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Settings/PC_Renderer.asset");
            if (!original) throw new InvalidOperationException("PC renderer asset unavailable.");
            renderer = Object.Instantiate(original); renderer.name = "District renderer"; renderer.rendererFeatures.Clear(); AssetDatabase.CreateAsset(renderer, path);
        }
        AlphaDistrictAnimeFeature feature = null;
        foreach (var item in renderer.rendererFeatures) if (item is AlphaDistrictAnimeFeature existing) feature = existing;
        if (!feature)
        {
            feature = ScriptableObject.CreateInstance<AlphaDistrictAnimeFeature>(); feature.name = "District / Edgar outline and anime";
            AssetDatabase.AddObjectToAsset(feature, renderer); renderer.rendererFeatures.Clear(); renderer.rendererFeatures.Add(feature);
        }
        var sourceOutline = AssetDatabase.LoadAssetAtPath<Material>(OutlinePath);
        if (!sourceOutline) throw new InvalidOperationException("Original outline material unavailable.");
        string outlinePath = ArtPath + "/Original outline district settings.mat";
        var outline = AssetDatabase.LoadAssetAtPath<Material>(outlinePath);
        if (!outline) { outline = new Material(sourceOutline); AssetDatabase.CreateAsset(outline, outlinePath); }
        outline.SetFloat("_OutlineThickness", 1.1f); outline.SetColor("_OutlineColor", new Color(.022f,.031f,.065f,1));
        string tonePath = ArtPath + "/Original anime shader reference.mat";
        var tone = AssetDatabase.LoadAssetAtPath<Material>(tonePath);
        if (!tone)
        {
            var shader = AssetDatabase.LoadAssetAtPath<Shader>(TonePath);
            if (!shader) throw new InvalidOperationException("Original anime tonemapper shader unavailable.");
            tone = new Material(shader); AssetDatabase.CreateAsset(tone, tonePath);
        }
        feature.outlineMaterial = outline; feature.tonemapperMaterial = tone; feature.SetActive(true); feature.Create();
        var serializedRenderer = new SerializedObject(renderer); var featureMap = serializedRenderer.FindProperty("m_RendererFeatureMap");
        if (AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out string _, out long localId))
        { featureMap.arraySize = 1; featureMap.GetArrayElementAtIndex(0).longValue = localId; serializedRenderer.ApplyModifiedPropertiesWithoutUndo(); }
        renderer.SetDirty(); EditorUtility.SetDirty(outline); EditorUtility.SetDirty(feature); EditorUtility.SetDirty(renderer); return renderer;
    }
    static UniversalRenderPipelineAsset BuildPipeline(UniversalRendererData renderer)
    {
        string path = ArtPath + "/District pipeline.asset";
        var pipeline = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>(path);
        if (!pipeline)
        {
            var source = AssetDatabase.LoadAssetAtPath<UniversalRenderPipelineAsset>("Assets/Settings/PC_RPAsset.asset");
            if (!source) throw new InvalidOperationException("PC pipeline asset unavailable.");
            pipeline = Object.Instantiate(source); pipeline.name = "District pipeline"; AssetDatabase.CreateAsset(pipeline, path);
        }
        var serialized = new SerializedObject(pipeline); var renderers = serialized.FindProperty("m_RendererDataList");
        renderers.arraySize = 2; renderers.GetArrayElementAtIndex(0).objectReferenceValue = renderer;
        // The shared core initially serializes renderer index 1. Keep that valid until both cameras switch together.
        renderers.GetArrayElementAtIndex(1).objectReferenceValue = AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Art/Laboratory/LaboratoryRenderer.asset");
        serialized.FindProperty("m_DefaultRendererIndex").intValue = 0;
        serialized.FindProperty("m_MainLightShadowmapResolution").intValue = 2048;
        serialized.FindProperty("m_ShadowDistance").floatValue = 145;
        serialized.FindProperty("m_SoftShadowsSupported").boolValue = false;
        serialized.ApplyModifiedPropertiesWithoutUndo(); EditorUtility.SetDirty(pipeline); return pipeline;
    }
}
