using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Torbellino.Rendering;

public static class RisographSetup
{
    public const string DefaultProfilePath = "Assets/Settings/DefaultVolumeProfile.asset";
    public const string PresetPath = "Assets/Rendering/Risograph/RisographReference.asset";
    public const string FantasyPresetPath = "Assets/Rendering/Risograph/RisographFantasy.asset";
    public static readonly string[] RendererPaths = {
        "Assets/Settings/PC_Renderer.asset",
        "Assets/Art/Laboratory/LaboratoryRenderer.asset",
        "Assets/Settings/Mobile_Renderer.asset"
    };

    [MenuItem("Tools/Rendering/Risograph/Install reference style")]
    public static void Install()
    {
        var shader = AssetDatabase.LoadAssetAtPath<Shader>("Assets/Rendering/Risograph/Risograph.shader");
        if (shader == null) throw new System.InvalidOperationException("Risograph shader missing.");
        foreach (var path in RendererPaths)
        {
            var renderer = AssetDatabase.LoadAssetAtPath<UniversalRendererData>(path);
            if (renderer == null) throw new System.InvalidOperationException("Renderer missing: " + path);
            var feature = renderer.rendererFeatures.Find(f => f is RisographRendererFeature) as RisographRendererFeature;
            if (feature == null)
            {
                feature = ScriptableObject.CreateInstance<RisographRendererFeature>();
                feature.name = "Risograph";
                AssetDatabase.AddObjectToAsset(feature, renderer);
                renderer.rendererFeatures.Add(feature);
            }
            var serialized = new SerializedObject(feature);
            serialized.FindProperty("shader").objectReferenceValue = shader;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            feature.SetActive(true);
            // Keep URP's recovery map in sync with its serialized feature list.
            var rendererState = new SerializedObject(renderer);
            var featureMap = rendererState.FindProperty("m_RendererFeatureMap");
            featureMap.arraySize = renderer.rendererFeatures.Count;
            for (int i = 0; i < renderer.rendererFeatures.Count; i++)
            {
                AssetDatabase.TryGetGUIDAndLocalFileIdentifier(renderer.rendererFeatures[i], out string _, out long localId);
                featureMap.GetArrayElementAtIndex(i).longValue = localId;
            }
            rendererState.ApplyModifiedPropertiesWithoutUndo();
            renderer.SetDirty();
            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);
        }

        var defaults = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultProfilePath);
        if (defaults == null) throw new System.InvalidOperationException("Default Volume profile missing.");
        ApplyReference(defaults);
        var preset = AssetDatabase.LoadAssetAtPath<VolumeProfile>(PresetPath);
        if (preset == null)
        {
            preset = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(preset, PresetPath);
        }
        ApplyReference(preset);
        if (VolumeManager.instance.isInitialized)
            VolumeManager.instance.SetGlobalDefaultProfile(defaults);
        AssetDatabase.SaveAssets();
        Debug.Log("RISOGRAPH INSTALLED: desktop, laboratory and mobile renderers; global default Volume.");
    }

    static void ApplyReference(VolumeProfile profile)
    {
        if (!profile.TryGet<RisographVolume>(out var riso))
        {
            riso = profile.Add<RisographVolume>(true);
            AssetDatabase.AddObjectToAsset(riso, profile);
        }
        riso.active = true;
        riso.SetAllOverridesTo(true);
        riso.intensity.Override(1f);
        EditorUtility.SetDirty(riso);
        EditorUtility.SetDirty(profile);
    }

    [MenuItem("Tools/Rendering/Risograph/Apply vivid reference style")]
    public static void ApplyVividReference()
    {
        Install();
        foreach (var path in new[] { DefaultProfilePath, PresetPath })
        {
            var profile = AssetDatabase.LoadAssetAtPath<VolumeProfile>(path);
            profile.TryGet<RisographVolume>(out var riso);
            riso.exposure.Override(0.2f);
            riso.contrast.Override(1.08f);
            riso.saturation.Override(1.3f);
            riso.midtoneLift.Override(0.65f);
            riso.paperHighlights.Override(0.6f);
            riso.paletteSize.Override(RisoPaletteSize.Four);
            riso.shadowInk.Override(new Color(0.157f, 0.216f, 0.478f));
            riso.blueInk.Override(new Color(0.31f, 0.48f, 0.82f));
            riso.pinkInk.Override(new Color(1f, 0.29f, 0.54f));
            riso.paper.Override(new Color(1f, 0.918f, 0.792f));
            riso.halftone.Override(0.7f);
            riso.paperGrain.Override(0.085f);
            riso.inkWear.Override(0.018f);
            riso.misregistration.Override(0.65f);
            EditorUtility.SetDirty(riso);
            EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets();
        if (VolumeManager.instance.isInitialized)
            VolumeManager.instance.SetGlobalDefaultProfile(AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultProfilePath));
    }

    [MenuItem("Tools/Rendering/Risograph/Apply fantasy style")]
    public static void ApplyFantasy()
    {
        Install();
        var fantasy = AssetDatabase.LoadAssetAtPath<VolumeProfile>(FantasyPresetPath);
        if (fantasy == null)
        {
            fantasy = ScriptableObject.CreateInstance<VolumeProfile>();
            AssetDatabase.CreateAsset(fantasy, FantasyPresetPath);
        }
        ApplyReference(fantasy);
        foreach (var profile in new[] { AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultProfilePath), fantasy })
        {
            profile.TryGet<RisographVolume>(out var riso);
            riso.paletteSize.Override(RisoPaletteSize.Four);
            riso.shadowInk.Override(new Color(52f / 255f, 32f / 255f, 92f / 255f));
            riso.blueInk.Override(new Color(136f / 255f, 117f / 255f, 230f / 255f));
            riso.pinkInk.Override(new Color(247f / 255f, 120f / 255f, 196f / 255f));
            riso.paper.Override(new Color(1f, 231f / 255f, 197f / 255f));
            riso.exposure.Override(0.15f);
            riso.contrast.Override(1.04f);
            riso.saturation.Override(1.2f);
            riso.midtoneLift.Override(0.72f);
            riso.paperHighlights.Override(0.55f);
            riso.halftone.Override(0.65f);
            riso.paperGrain.Override(0.065f);
            riso.inkWear.Override(0.012f);
            riso.misregistration.Override(0.45f);
            EditorUtility.SetDirty(riso);
            EditorUtility.SetDirty(profile);
        }
        AssetDatabase.SaveAssets();
        if (VolumeManager.instance.isInitialized)
            VolumeManager.instance.SetGlobalDefaultProfile(AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultProfilePath));
        Debug.Log("RISOGRAPH FANTASY APPLIED: violet, lavender, pink and peach paper.");
    }

    [MenuItem("Tools/Rendering/Risograph/Edit global settings")]
    static void SelectSettings()
    {
        Selection.activeObject = AssetDatabase.LoadAssetAtPath<VolumeProfile>(DefaultProfilePath);
        EditorGUIUtility.PingObject(Selection.activeObject);
    }
}
