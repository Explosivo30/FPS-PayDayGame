using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Torbellino.Rendering
{
    [DisallowMultipleRendererFeature("Risograph")]
    public sealed class RisographRendererFeature : ScriptableRendererFeature
    {
        // Serialized reference keeps the shader in player builds, including IL2CPP.
        [SerializeField] Shader shader;
        [SerializeField] bool showInSceneView = true;
        Material material;
        RisoPass pass;

        public override void Create()
        {
            CoreUtils.Destroy(material);
            pass = null;
            if (shader == null) shader = Shader.Find("Hidden/Torbellino/Risograph");
            if (shader == null) return;
            material = CoreUtils.CreateEngineMaterial(shader);
            pass = new RisoPass(material);
        }

        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
        {
            var camera = renderingData.cameraData;
            if (pass == null || !camera.resolveFinalTarget) return;
            if (camera.cameraType != CameraType.Game && camera.cameraType != CameraType.SceneView) return;
            if (camera.isSceneViewCamera && !showInSceneView) return;
            var settings = VolumeManager.instance.stack.GetComponent<RisographVolume>();
            if (settings == null || !settings.IsActive()) return;
            pass.SetSettings(settings);
            renderer.EnqueuePass(pass);
        }

        protected override void Dispose(bool disposing)
        {
            pass = null;
            CoreUtils.Destroy(material);
        }

        sealed class RisoPass : ScriptableRenderPass
        {
            readonly Material material;
            static readonly int Tone = Shader.PropertyToID("_RisoTone");
            static readonly int Lightness = Shader.PropertyToID("_RisoLightness");
            static readonly int Screen = Shader.PropertyToID("_RisoScreen");
            static readonly int Texture = Shader.PropertyToID("_RisoTexture");
            static readonly int Shadow = Shader.PropertyToID("_ShadowInk");
            static readonly int Blue = Shader.PropertyToID("_BlueInk");
            static readonly int Pink = Shader.PropertyToID("_PinkInk");
            static readonly int Paper = Shader.PropertyToID("_Paper");

            public RisoPass(Material material)
            {
                this.material = material;
                renderPassEvent = RenderPassEvent.AfterRenderingPostProcessing;
                requiresIntermediateTexture = true;
            }

            public void SetSettings(RisographVolume settings)
            {
                material.SetVector(Tone, new Vector4(settings.intensity.value, Mathf.Pow(2f, settings.exposure.value),
                    settings.contrast.value, settings.saturation.value));
                material.SetVector(Lightness, new Vector4(settings.midtoneLift.value, settings.paperHighlights.value, 0f, 0f));
                material.SetVector(Screen, new Vector4(settings.dotSize.value, settings.screenAngle.value * Mathf.Deg2Rad,
                    settings.halftone.value, (int)settings.paletteSize.value));
                material.SetVector(Texture, new Vector4(settings.paperGrain.value, settings.inkWear.value,
                    settings.misregistration.value, 0f));
                // Palette math operates in perceptual sRGB, so don't linearize these swatches.
                material.SetVector(Shadow, (Vector4)settings.shadowInk.value);
                material.SetVector(Blue, (Vector4)settings.blueInk.value);
                material.SetVector(Pink, (Vector4)settings.pinkInk.value);
                material.SetVector(Paper, (Vector4)settings.paper.value);
            }

            public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
            {
                var resources = frameData.Get<UniversalResourceData>();
                if (resources.isActiveTargetBackBuffer) return;
                var source = resources.activeColorTexture;
                var descriptor = renderGraph.GetTextureDesc(source);
                descriptor.name = "Risograph Color";
                descriptor.clearBuffer = false;
                var destination = renderGraph.CreateTexture(descriptor);
                var parameters = new RenderGraphUtils.BlitMaterialParameters(source, destination, material, 0);
                renderGraph.AddBlitPass(parameters, passName: "Risograph Print");
                resources.cameraColor = destination;
            }
        }
    }
}
