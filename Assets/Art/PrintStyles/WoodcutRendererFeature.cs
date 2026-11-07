using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;

namespace Torbellino.Rendering
{
    [DisallowMultipleRendererFeature("Woodcut Engraving")]
    public sealed class WoodcutRendererFeature : ScriptableRendererFeature
    {
        public Material material;
        PrintPass pass;
        public override void Create() { pass = material ? new PrintPass(material) : null; }
        public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData data)
        {
            if (pass == null || !data.cameraData.resolveFinalTarget) return;
            if(data.cameraData.cameraType != CameraType.Game && data.cameraData.cameraType != CameraType.SceneView) return;
            renderer.EnqueuePass(pass);
        }
        sealed class PrintPass : ScriptableRenderPass
        {
            readonly Material material;
            public PrintPass(Material value) { material=value; renderPassEvent=RenderPassEvent.AfterRenderingPostProcessing; requiresIntermediateTexture=true; }
            public override void RecordRenderGraph(RenderGraph graph, ContextContainer frame)
            {
                var resources=frame.Get<UniversalResourceData>();
                if(resources.isActiveTargetBackBuffer) return;
                var source=resources.activeColorTexture;
                var desc=graph.GetTextureDesc(source); desc.name="Woodcut Engraving"; desc.clearBuffer=false;
                var destination=graph.CreateTexture(desc);
                graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source,destination,material,0),passName:"Woodcut Engraving");
                resources.cameraColor=destination;
            }
        }
    }
}
