using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

/// <summary>Scene-owned rendering using the project's original outline graph and anime tonemapper.</summary>
public sealed class AlphaDistrictAnimeFeature : ScriptableRendererFeature
{
    public Material outlineMaterial, tonemapperMaterial;
    Material outlineInstance, toneInstance;
    InkPass ink;
    TonePass tone;
    public static int OutlineExecutions { get; private set; }
    public static int ToneExecutions { get; private set; }

    public override void Create()
    {
        CoreUtils.Destroy(outlineInstance); CoreUtils.Destroy(toneInstance);
        if (outlineMaterial) outlineInstance = new Material(outlineMaterial) { hideFlags = HideFlags.HideAndDontSave };
        if (tonemapperMaterial) toneInstance = new Material(tonemapperMaterial) { hideFlags = HideFlags.HideAndDontSave };
        ink = outlineInstance ? new InkPass(outlineInstance) : null;
        tone = toneInstance ? new TonePass(toneInstance) : null;
    }
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        var camera = renderingData.cameraData;
        if (camera.cameraType == CameraType.Preview || camera.cameraType == CameraType.Reflection) return;
        // World normals belong to the base camera. The weapon overlay must not outline the world a second time.
        if (ink != null && camera.renderType == CameraRenderType.Base) renderer.EnqueuePass(ink);
        // Process the completed world/weapon stack exactly once. The original feature applied its transform twice.
        if (tone != null && camera.resolveFinalTarget) renderer.EnqueuePass(tone);
    }
    protected override void Dispose(bool disposing)
    { CoreUtils.Destroy(outlineInstance); CoreUtils.Destroy(toneInstance); }

    sealed class InkPass : ScriptableRenderPass
    {
        readonly Material material;
        sealed class Data { public Material material; }
        public InkPass(Material value)
        {
            material = value; renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            requiresIntermediateTexture = true;
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var resources = frame.Get<UniversalResourceData>();
            if (!resources.activeColorTexture.IsValid()) return;
            using (var builder = graph.AddRasterRenderPass<Data>("Alpha / original outline graph", out var data))
            {
                data.material = material;
                if (resources.cameraDepthTexture.IsValid()) builder.UseTexture(resources.cameraDepthTexture, AccessFlags.Read);
                if (resources.cameraNormalsTexture.IsValid()) builder.UseTexture(resources.cameraNormalsTexture, AccessFlags.Read);
                // The user's graph outputs black ink with alpha; preserve the existing framebuffer beneath it.
                builder.SetRenderAttachment(resources.activeColorTexture, 0, AccessFlags.ReadWrite);
                builder.SetRenderFunc((Data d, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, new Vector4(1, 1, 0, 0), d.material, 0);
                    OutlineExecutions++;
                });
            }
        }
    }
    sealed class TonePass : ScriptableRenderPass
    {
        readonly Material material;
        sealed class Data { public TextureHandle source; public Material material; }
        public TonePass(Material value)
        {
            material = value; renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
            requiresIntermediateTexture = true;
        }
        public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
        {
            var component = VolumeManager.instance.stack.GetComponent<AnimeTonemapper>();
            if (component == null || !component.IsActive()) return;
            var resources = frame.Get<UniversalResourceData>();
            if (resources.isActiveTargetBackBuffer || !resources.activeColorTexture.IsValid()) return;
            material.SetFloat("_Exposure", component.exposure.value);
            material.SetFloat("_Contrast", component.contrast.value);
            material.SetFloat("_Saturation", component.saturation.value);
            material.SetFloat("_NeonBoost", component.neonBoost.value);
            material.SetFloat("_ShadowHueShift", component.shadowHueShift.value);
            material.SetFloat("_ShadowHueStr", component.shadowHueStr.value);
            material.SetFloat("_ShadowThreshold", component.shadowThreshold.value);
            material.SetColor("_ShadowColor", component.shadowColor.value);
            material.SetFloat("_HighlightSoft", component.highlightSoft.value);
            material.SetFloat("_HighlightHueShift", component.highlightHueShift.value);
            material.SetFloat("_HighlightHueStr", component.highlightHueStr.value);
            material.SetFloat("_HighlightThreshold", component.highlightThreshold.value);
            material.SetFloat("_CelBanding", component.celBanding.value ? 1 : 0);
            material.SetFloat("_CelBands", component.celBands.value);
            material.SetFloat("_CelHardness", component.celHardness.value);
            var source = resources.activeColorTexture;
            var description = graph.GetTextureDesc(source); description.name = "Alpha anime color";
            var destination = graph.CreateTexture(description);
            using (var builder = graph.AddRasterRenderPass<Data>("Alpha / original anime shader once", out var data))
            {
                data.source = source; data.material = material;
                builder.UseTexture(source, AccessFlags.Read);
                builder.SetRenderAttachment(destination, 0, AccessFlags.Write);
                builder.SetRenderFunc((Data d, RasterGraphContext context) =>
                {
                    Blitter.BlitTexture(context.cmd, d.source, new Vector4(1, 1, 0, 0), d.material, 0);
                    ToneExecutions++;
                });
            }
            resources.cameraColor = destination;
        }
    }
}
