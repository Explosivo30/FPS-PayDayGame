using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.RenderGraphModule.Util;
using UnityEngine.Rendering.Universal;
namespace Torbellino.Rendering{
[DisallowMultipleRendererFeature("Risograph Game")]
public sealed class RisographGameRendererFeature:ScriptableRendererFeature{
 public Material material;PrintPass pass;
 public override void Create(){pass=material?new PrintPass(material):null;}
 public override void AddRenderPasses(ScriptableRenderer renderer,ref RenderingData data){var c=data.cameraData;if(pass==null||!c.resolveFinalTarget)return;if(c.cameraType!=CameraType.Game&&c.cameraType!=CameraType.SceneView)return;renderer.EnqueuePass(pass);}
 sealed class PrintPass:ScriptableRenderPass{
  readonly Material material;public PrintPass(Material value){material=value;renderPassEvent=RenderPassEvent.AfterRenderingPostProcessing;requiresIntermediateTexture=true;}
  public override void RecordRenderGraph(RenderGraph graph,ContextContainer frame){var r=frame.Get<UniversalResourceData>();if(r.isActiveTargetBackBuffer||!r.activeColorTexture.IsValid())return;var source=r.activeColorTexture;var desc=graph.GetTextureDesc(source);desc.name="Risograph Game Color";desc.clearBuffer=false;var dest=graph.CreateTexture(desc);graph.AddBlitPass(new RenderGraphUtils.BlitMaterialParameters(source,dest,material,0),passName:"Risograph Game");r.cameraColor=dest;}
 }
}
}
