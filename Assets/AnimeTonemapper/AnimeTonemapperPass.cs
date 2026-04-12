using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class AnimeTonemapperPass : ScriptableRenderPass
{
    private static readonly int s_Exposure           = Shader.PropertyToID("_Exposure");
    private static readonly int s_Contrast           = Shader.PropertyToID("_Contrast");
    private static readonly int s_Saturation         = Shader.PropertyToID("_Saturation");
    private static readonly int s_NeonBoost          = Shader.PropertyToID("_NeonBoost");
    private static readonly int s_ShadowHueShift     = Shader.PropertyToID("_ShadowHueShift");
    private static readonly int s_ShadowHueStr       = Shader.PropertyToID("_ShadowHueStr");
    private static readonly int s_ShadowThreshold    = Shader.PropertyToID("_ShadowThreshold");
    private static readonly int s_ShadowColor        = Shader.PropertyToID("_ShadowColor");
    private static readonly int s_HighlightSoft      = Shader.PropertyToID("_HighlightSoft");
    private static readonly int s_HighlightHueShift  = Shader.PropertyToID("_HighlightHueShift");
    private static readonly int s_HighlightHueStr    = Shader.PropertyToID("_HighlightHueStr");
    private static readonly int s_HighlightThreshold = Shader.PropertyToID("_HighlightThreshold");
    private static readonly int s_CelBanding         = Shader.PropertyToID("_CelBanding");
    private static readonly int s_CelBands           = Shader.PropertyToID("_CelBands");
    private static readonly int s_CelHardness        = Shader.PropertyToID("_CelHardness");

    private Material _material;

    public AnimeTonemapperPass(Material mat)
    {
        _material = mat;
        renderPassEvent = RenderPassEvent.BeforeRenderingPostProcessing;
    }

    private class PassData
    {
        public TextureHandle src;
        public Material material;
    }

    public override void RecordRenderGraph(RenderGraph rg, ContextContainer frame)
    {
        var stack = VolumeManager.instance.stack;
        var comp  = stack.GetComponent<AnimeTonemapper>();
        if (comp == null || !comp.IsActive()) return;

        var resource = frame.Get<UniversalResourceData>();
        var src      = resource.activeColorTexture;

        _material.SetFloat(s_Exposure,           comp.exposure.value);
        _material.SetFloat(s_Contrast,           comp.contrast.value);
        _material.SetFloat(s_Saturation,         comp.saturation.value);
        _material.SetFloat(s_NeonBoost,          comp.neonBoost.value);
        _material.SetFloat(s_ShadowHueShift,     comp.shadowHueShift.value);
        _material.SetFloat(s_ShadowHueStr,       comp.shadowHueStr.value);
        _material.SetFloat(s_ShadowThreshold,    comp.shadowThreshold.value);
        _material.SetColor(s_ShadowColor,        comp.shadowColor.value);
        _material.SetFloat(s_HighlightSoft,      comp.highlightSoft.value);
        _material.SetFloat(s_HighlightHueShift,  comp.highlightHueShift.value);
        _material.SetFloat(s_HighlightHueStr,    comp.highlightHueStr.value);
        _material.SetFloat(s_HighlightThreshold, comp.highlightThreshold.value);
        _material.SetFloat(s_CelBanding,         comp.celBanding.value ? 1f : 0f);
        _material.SetFloat(s_CelBands,           comp.celBands.value);
        _material.SetFloat(s_CelHardness,        comp.celHardness.value);

        var desc = rg.GetTextureDesc(src);
        desc.name = "AnimeTonemapper_Temp";
        var dst = rg.CreateTexture(desc);

        using (var builder = rg.AddRasterRenderPass<PassData>("Anime Tonemapper", out var data))
        {
            data.src      = src;
            data.material = _material;

            builder.UseTexture(src, AccessFlags.Read);
            builder.SetRenderAttachment(dst, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1, 1, 0, 0), d.material, 0);
            });
        }

        using (var builder = rg.AddRasterRenderPass<PassData>("Anime Tonemapper Copy Back", out var data))
        {
            data.src      = dst;
            data.material = _material;

            builder.UseTexture(dst, AccessFlags.Read);
            builder.SetRenderAttachment(src, 0, AccessFlags.Write);

            builder.SetRenderFunc((PassData d, RasterGraphContext ctx) =>
            {
                Blitter.BlitTexture(ctx.cmd, d.src, new Vector4(1, 1, 0, 0), d.material, 0);
            });
        }
    }
}
