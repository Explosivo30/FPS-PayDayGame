using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

public class AnimeTonemapperFeature : ScriptableRendererFeature
{
    private AnimeTonemapperPass _pass;
    private Material _material;

    public override void Create()
    {
        var shader = Shader.Find("Hidden/Torbellino/AnimeTonemapper");
        if (shader == null)
        {
            Debug.LogWarning("[AnimeTonemapper] Shader no encontrado: Hidden/Torbellino/AnimeTonemapper");
            return;
        }
        _material = CoreUtils.CreateEngineMaterial(shader);
        _pass = new AnimeTonemapperPass(_material);
    }

    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        if (_material == null || _pass == null) return;

        // Solo en game/scene view, no en previews
        if (renderingData.cameraData.cameraType == CameraType.Preview) return;

        renderer.EnqueuePass(_pass);
    }

    protected override void Dispose(bool disposing)
    {
        CoreUtils.Destroy(_material);
    }
}
