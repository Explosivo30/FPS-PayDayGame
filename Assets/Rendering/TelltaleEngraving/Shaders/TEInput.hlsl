#ifndef TE_INPUT
#define TE_INPUT
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/ShaderLibrary/CommonMaterial.hlsl"
TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
TEXTURE2D(_OcclusionMap);SAMPLER(sampler_OcclusionMap);
TEXTURE2D(_EmissionMap);SAMPLER(sampler_EmissionMap);
TEXTURE2D(_HatchTex);SAMPLER(sampler_HatchTex);
TEXTURE2D(_DetailInkMap);SAMPLER(sampler_DetailInkMap);
CBUFFER_START(UnityPerMaterial)
float4 _BaseMap_ST,_BaseColor,_ShadowTint,_MidTint,_HighlightTint,_InkColor,_EmissionColor,_HatchTex_ST;
float _Saturation,_Contrast,_Quantization,_BumpScale,_UseNormalMap,_OcclusionStrength,_UseOcclusion,_UseEmission,_Metallic,_Smoothness;
float _ShadowThreshold,_HighlightThreshold,_Transition,_LightingIntensity,_AmbientStrength,_VolumeBlend,_Cutoff,_AlphaClip;
float _HatchEnable,_HatchIntensity,_HatchScale,_HatchThickness,_HatchAngle,_CrossAngle,_CrossStrength,_HatchStart,_HatchContrast,_Projection;
float _GrainIntensity,_InkVariation,_GrainScale,_TextureInfluence,_InternalInkStrength,_OutlineWeight;
float _Surface,_SrcBlend,_DstBlend,_ZWrite,_Cull;
float _HatchWorldUnits;
float _HatchWaviness;
CBUFFER_END
struct TEAttributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
struct TEVaryings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float3 normalWS:TEXCOORD1;float4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;float3 positionOS:TEXCOORD4;float3 normalOS:TEXCOORD5;float fog:TEXCOORD6;UNITY_VERTEX_INPUT_INSTANCE_ID UNITY_VERTEX_OUTPUT_STEREO};
float3 TEObjectHatchPosition(float3 localPosition){
 if(_HatchWorldUnits<.5)return localPosition;
 // Scale local axes into metres while preserving the object's origin and orientation.
 // Camera movement and object translation never change the engraving coordinates.
 float4x4 objectMatrix=GetObjectToWorldMatrix();
 float3 axisScale=float3(length(objectMatrix._m00_m10_m20),length(objectMatrix._m01_m11_m21),length(objectMatrix._m02_m12_m22));
 return localPosition*max(axisScale,.00001);
}
TEVaryings TEVertex(TEAttributes v){TEVaryings o=(TEVaryings)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_TRANSFER_INSTANCE_ID(v,o);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;o.tangentWS=float4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());o.positionOS=TEObjectHatchPosition(v.positionOS.xyz);o.normalOS=v.normalOS;o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
void TEClip(float2 uv){if(_AlphaClip>.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv).a*_BaseColor.a-_Cutoff);}
#endif

