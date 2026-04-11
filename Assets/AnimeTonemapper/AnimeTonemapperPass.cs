using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

public class AnimeTonemapperPass : ScriptableRenderPass
{
    private static readonly int s_Exposure      = Shader.PropertyToID("_Exposure");
    private static readonly int s_Contrast      = Shader.PropertyToID("_Contrast");
    private static readonly int s_Saturation    = Shader.PropertyToID("_Saturation");
    private static readonly int s_ShadowTint    = Shader.PropertyToID("_ShadowTint");
    private static readonly int s_HighlightSoft = Shader.PropertyToID("_HighlightSoft");
    private static readonly int s_ShadowColor   = Shader.PropertyToID("_ShadowColor");

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

        // Pasar parametros al material
        _material.SetFloat(s_Exposure,      comp.exposure.value);
        _material.SetFloat(s_Contrast,      comp.contrast.value);
        _material.SetFloat(s_Saturation,    comp.saturation.value);
        _material.SetFloat(s_ShadowTint,    comp.shadowTint.value);
        _material.SetFloat(s_HighlightSoft, comp.highlightSoft.value);
        _material.SetColor(s_ShadowColor,   comp.shadowColor.value);

        // Textura temporal con mismos descriptors que el color activo
        var desc = rg.GetTextureDesc(src);
        desc.name = "AnimeTonemapper_Temp";
        var dst = rg.CreateTexture(desc);

        // Pass 1: blit de src -> dst con el efecto
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

        // Pass 2: copy back dst -> src (color activo)
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
