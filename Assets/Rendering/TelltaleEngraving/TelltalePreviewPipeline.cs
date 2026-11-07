using UnityEngine;
using UnityEngine.Rendering;
namespace Torbellino.Rendering{
public sealed class TelltalePreviewPipeline:MonoBehaviour{
 public RenderPipelineAsset pipeline;RenderPipelineAsset previousDefault,previousQuality;bool applied;
 void OnEnable(){if(!Application.isPlaying||!pipeline)return;previousDefault=GraphicsSettings.defaultRenderPipeline;previousQuality=QualitySettings.renderPipeline;GraphicsSettings.defaultRenderPipeline=pipeline;QualitySettings.renderPipeline=pipeline;applied=true;}
 void OnDisable(){if(!applied)return;if(GraphicsSettings.defaultRenderPipeline==pipeline)GraphicsSettings.defaultRenderPipeline=previousDefault;if(QualitySettings.renderPipeline==pipeline)QualitySettings.renderPipeline=previousQuality;applied=false;}
}
}
