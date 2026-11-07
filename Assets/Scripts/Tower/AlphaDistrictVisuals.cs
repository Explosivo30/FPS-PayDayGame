using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

/// <summary>The district's temporary pipeline and instance materials; original campaign assets remain unchanged.</summary>
[DefaultExecutionOrder(110)]
public sealed class AlphaDistrictVisuals : MonoBehaviour
{
    public UniversalRenderPipelineAsset pipeline;
    public Material actorTemplate;
    public int StyledRenderers { get; private set; }
    public int CameraCount => cameras.Count;
    public bool UsingLocalPipeline => GraphicsSettings.defaultRenderPipeline == pipeline && QualitySettings.renderPipeline == pipeline;
    readonly Dictionary<Material, Material> remapped = new Dictionary<Material, Material>();
    readonly Dictionary<Color32, Texture2D> colors = new Dictionary<Color32, Texture2D>();
    readonly List<CameraState> cameras = new List<CameraState>();
    readonly List<MaterialState> originalMaterials = new List<MaterialState>();
    readonly List<Binding> feedback = new List<Binding>();
    MaterialPropertyBlock properties;
    RenderPipelineAsset oldDefault, oldQuality;
    bool installed;
    static readonly FieldInfo RendererIndex = typeof(UniversalAdditionalCameraData).GetField("m_RendererIndex", BindingFlags.Instance | BindingFlags.NonPublic);
    static readonly Color ActorShadow = new Color(.46f, .49f, .60f, 1);
    sealed class CameraState
    { public UniversalAdditionalCameraData data; public int index; public Color background; public CameraClearFlags clear; }
    sealed class Binding
    { public Renderer renderer; public NormalEnemyStateMachine owner; public Color baseColor; public Texture texture; }
    sealed class MaterialState { public Renderer renderer; public Material[] materials; }

    void Awake()
    {
        properties = new MaterialPropertyBlock();
        if (!pipeline || !actorTemplate) { Debug.LogError("Alpha rendering assets have not been assigned."); return; }
        oldDefault = GraphicsSettings.defaultRenderPipeline; oldQuality = QualitySettings.renderPipeline;
        GraphicsSettings.defaultRenderPipeline = pipeline; QualitySettings.renderPipeline = pipeline; installed = true;
    }
    IEnumerator Start()
    {
        while (TowerSession.Instance == null || TowerSession.Instance.player == null) yield return null;
        var player = TowerSession.Instance.player;
        if (!player) yield break;
        foreach (var camera in player.GetComponentsInChildren<Camera>(true))
        {
            var data = camera.GetUniversalAdditionalCameraData();
            cameras.Add(new CameraState { data = data, index = RendererIndex != null ? (int)RendererIndex.GetValue(data) : 1,
                background = camera.backgroundColor, clear = camera.clearFlags });
            data.SetRenderer(0);
            if (data.renderType == CameraRenderType.Base)
            {
                camera.clearFlags = CameraClearFlags.SolidColor;
                camera.backgroundColor = new Color(.24f, .43f, .64f);
            }
        }
        Stylize(player.gameObject);
        Debug.Log("ALPHA STYLE: original ToonShader graph + original AnimeTonemapper shader; local renderer on " + CameraCount + " player cameras.");
    }
    public void Stylize(GameObject target)
    {
        if (!target || !actorTemplate) return;
        var owner = target.GetComponentInParent<NormalEnemyStateMachine>();
        foreach (var renderer in target.GetComponentsInChildren<Renderer>(true))
        {
            if (renderer is ParticleSystemRenderer || renderer is LineRenderer || renderer is TrailRenderer || renderer is SpriteRenderer) continue;
            var materials = renderer.sharedMaterials; var originals = (Material[])materials.Clone();
            bool changed = false; Color first = Color.white; Texture firstTexture = null;
            for (int i = 0; i < materials.Length; i++)
            {
                var source = materials[i];
                if (!source || !source.shader || source.shader == actorTemplate.shader || source.renderQueue >= 3000) continue;
                // Preserve emissive warning meshes and VFX, including eye and muzzle signals.
                if (source.shader.name.Contains("Unlit") || (source.HasProperty("_EmissionColor") && source.GetColor("_EmissionColor").maxColorComponent > .2f)) continue;
                if (!remapped.TryGetValue(source, out var material))
                {
                    material = new Material(actorTemplate) { name = source.name + " / district toon", hideFlags = HideFlags.DontSave };
                    Color color = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : source.HasProperty("_Color") ? source.GetColor("_Color") : Color.white;
                    if (owner) color = ActorPigment(color);
                    Texture texture = source.HasProperty("_BaseMap") ? source.GetTexture("_BaseMap") : source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
                    if (texture == null || texture.name == "white" || texture.name == "Default-White") texture = ColorTexture(color);
                    material.SetTexture("_MainTexture", texture);
                    material.SetColor("_ToonRampTinting", ActorShadow);
                    remapped.Add(source, material);
                }
                if (i == 0)
                {
                    first = source.HasProperty("_BaseColor") ? source.GetColor("_BaseColor") : Color.white;
                    if (owner) first = ActorPigment(first);
                    firstTexture = material.GetTexture("_MainTexture");
                }
                materials[i] = material; changed = true;
            }
            if (!changed) continue;
            originalMaterials.Add(new MaterialState { renderer = renderer, materials = originals });
            renderer.sharedMaterials = materials; StyledRenderers++;
            if (owner && firstTexture) feedback.Add(new Binding { renderer = renderer, owner = owner, baseColor = first, texture = firstTexture });
        }
    }
    static Color ActorPigment(Color color)
    {
        float maximum = Mathf.Max(color.r, Mathf.Max(color.g, color.b));
        // Titanium/steel authored for PBR depended on reflections. Keep graphite/ink dark,
        // but lift medium-dark flat armour so the original toon graph retains its silhouette in shade.
        if (maximum >= .10f && maximum < .43f)
        {
            float scale = .43f / maximum; color.r *= scale; color.g *= scale; color.b *= scale;
        }
        return color;
    }
    Texture2D ColorTexture(Color color)
    {
        Color32 key = color;
        if (!colors.TryGetValue(key, out var texture))
        {
            texture = new Texture2D(1, 1, TextureFormat.RGBA32, false) { name = "District actor pigment", hideFlags = HideFlags.DontSave, filterMode = FilterMode.Point };
            texture.SetPixel(0, 0, color); texture.Apply(); colors.Add(key, texture);
        }
        return texture;
    }
    void LateUpdate()
    {
        // RobotPresentation writes its normal warning/flash block before this component. Bridge that signal to the user's graph.
        for (int i = feedback.Count - 1; i >= 0; i--)
        {
            var binding = feedback[i];
            if (!binding.renderer || !binding.owner) { feedback.RemoveAt(i); continue; }
            binding.renderer.GetPropertyBlock(properties);
            Color emission = properties.GetColor("_EmissionColor");
            properties.SetColor("_ToonRampTinting", ActorShadow + emission * .55f);
            properties.SetTexture("_MainTexture", binding.owner.IsDead ? ColorTexture(binding.baseColor * .35f) : binding.texture);
            binding.renderer.SetPropertyBlock(properties);
        }
    }
    void OnDestroy()
    {
        if (installed)
        {
            if (GraphicsSettings.defaultRenderPipeline == pipeline) GraphicsSettings.defaultRenderPipeline = oldDefault;
            if (QualitySettings.renderPipeline == pipeline) QualitySettings.renderPipeline = oldQuality;
            foreach (var state in cameras) if (state.data)
            {
                state.data.SetRenderer(state.index);
                var camera = state.data.GetComponent<Camera>();
                if (camera) { camera.backgroundColor = state.background; camera.clearFlags = state.clear; }
            }
        }
        foreach (var state in originalMaterials) if (state.renderer) state.renderer.sharedMaterials = state.materials;
        foreach (var material in remapped.Values) if (material) Destroy(material);
        foreach (var texture in colors.Values) if (texture) Destroy(texture);
    }
}
