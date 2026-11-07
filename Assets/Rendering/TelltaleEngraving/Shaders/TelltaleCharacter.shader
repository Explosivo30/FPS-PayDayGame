Shader "Torbellino/Telltale Engraving/Character"
{
 Properties {
 [Header(Base Material)]
 [MainTexture] _BaseMap("Original albedo",2D)="white"{}
 [MainColor] _BaseColor("Original color multiplier",Color)=(1,1,1,1)
 _Saturation("Saturation",Range(0,2))=1
 _Contrast("Contrast",Range(.5,2))=1
 _Quantization("Color quantization (0 off)",Range(0,16))=0
 [Normal] _BumpMap("Original normal map",2D)="bump"{}
 [Toggle] _UseNormalMap("Use normal map",Float)=0
 _BumpScale("Normal scale",Range(0,2))=1
 _OcclusionMap("Original occlusion (green)",2D)="white"{}
 [Toggle] _UseOcclusion("Use occlusion map",Float)=0
 _OcclusionStrength("Occlusion strength",Range(0,1))=1
 _EmissionMap("Original emission",2D)="white"{}
 [HDR] _EmissionColor("Emission color",Color)=(0,0,0,1)
 [Toggle] _UseEmission("Use emission",Float)=0
 _Metallic("Painted metal highlight",Range(0,1))=0
 _Smoothness("Highlight concentration",Range(0,1))=.4
 [Header(Graphic Novel Lighting)]
 _ShadowTint("Shadow tint",Color)=(.28,.34,.46,1)
 _MidTint("Midtone tint",Color)=(.8,.84,.9,1)
 _HighlightTint("Highlight tint",Color)=(1,1,.98,1)
 _ShadowThreshold("Shadow threshold",Range(0,1))=.35
 _HighlightThreshold("Highlight threshold",Range(0,2))=.7
 _Transition("Band transition",Range(.001,.25))=.055
 _LightingIntensity("Direct light response",Range(0,2))=1
 _AmbientStrength("Environment response",Range(0,2))=.35
 _VolumeBlend("Continuous form within bands",Range(0,.5))=.18
 [Header(Engraving Texture)]
 [Toggle] _HatchEnable("Enable hatching",Float)=0
 _HatchTex("Hatch distance / breakup / grain",2D)="white"{}
 _HatchIntensity("Hatch intensity",Range(0,1))=.28
 _HatchScale("Texture tiles per mesh unit or UV",Float)=8
 [Toggle] _HatchWorldUnits("Consistent object anchored size in metres",Float)=0
 _HatchThickness("Line coverage",Range(.03,.55))=.15
 _HatchWaviness("Stroke waviness",Range(0,1))=1
 _HatchAngle("Hatch angle (degrees)",Range(-180,180))=25
 _CrossAngle("Second angle (degrees)",Range(-180,180))=-55
 _CrossStrength("Deep shadow crosshatch",Range(0,1))=.25
 _HatchStart("Shadow hatching threshold",Range(0,1))=.45
 _HatchContrast("Shadow ink contrast",Range(.5,4))=1.2
 [Enum(ArtistUV,0,ObjectTriplanar,1)] _Projection("Pattern coordinates",Float)=0
 _InkColor("Ink tint",Color)=(.014,.022,.04,1)
 [Header(Ink Imperfections)]
 _GrainIntensity("Surface grain",Range(0,.1))=0
 _InkVariation("Line breakup",Range(0,1))=.12
 _GrainScale("Grain frequency",Range(.5,8))=2
 _TextureInfluence("Albedo darkness influence",Range(0,1))=.12
 [Header(Authored Internal Lines)]
 _DetailInkMap("Internal ink mask (black draws ink)",2D)="white"{}
 _InternalInkStrength("Authored internal line strength",Range(0,1))=0
 [Header(Geometric Outline Companion)]
 _OutlineWeight("Outline weight (rebuild companion after changes)",Range(0,2))=1
 [Header(Advanced Settings)]
 [HideInInspector] _Surface("Surface",Float)=0
 [HideInInspector] _SrcBlend("Source blend",Float)=1
 [HideInInspector] _DstBlend("Destination blend",Float)=0
 [HideInInspector] _ZWrite("Depth write",Float)=1
 [HideInInspector] _Cull("Cull",Float)=2
 [Toggle] _AlphaClip("Cutout",Float)=0
 _Cutoff("Alpha cutoff",Range(0,1))=.5
 }
 SubShader {
 Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
 HLSLINCLUDE
 #include "TEInput.hlsl"
 ENDHLSL
 Pass {
 Name "GraphicNovel"
 Blend [_SrcBlend] [_DstBlend]
 ZWrite [_ZWrite]
 Cull [_Cull]
 Tags {"LightMode"="UniversalForwardOnly"}
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex TEVertex
 #pragma fragment TEFragment
 #pragma multi_compile_instancing
 #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
 #pragma multi_compile _ _ADDITIONAL_LIGHTS
 #pragma multi_compile _ _FORWARD_PLUS
 #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
 #pragma multi_compile_fragment _ _SHADOWS_SOFT
 #pragma multi_compile_fog
 #include "TELighting.hlsl"
 #include "TEHatching.hlsl"
 float4 TEFragment(TEVaryings v):SV_Target{
 UNITY_SETUP_INSTANCE_ID(v);UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);TEClip(v.uv);
 float3 n=normalize(v.normalWS);
 if(_UseNormalMap>.5){float3 t=normalize(v.tangentWS.xyz);float3 b=cross(n,t)*v.tangentWS.w;float3 normalTS=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,v.uv),_BumpScale);n=normalize(mul(normalTS,float3x3(t,b,n)));}
 float3 albedo=TEColor(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).rgb*_BaseColor.rgb);
 float occlusion=_UseOcclusion>.5?lerp(1,SAMPLE_TEXTURE2D(_OcclusionMap,sampler_OcclusionMap,v.uv).g,_OcclusionStrength):1;
 float lightLevel;float3 color=albedo*TELight(v,n,lightLevel)*occlusion;
 Light key=GetMainLight(TEShadowCoord(v));float3 eye=GetWorldSpaceNormalizeViewDir(v.positionWS);
 float spec=pow(saturate(dot(n,normalize(key.direction+eye))),lerp(12,120,_Smoothness));
 color+=smoothstep(.5,.65,spec)*_Metallic*.16*key.color*key.shadowAttenuation;
 color=TEHatch(v,color,lightLevel*occlusion,TELuma(albedo));
 if(_InternalInkStrength>0)color=lerp(color,_InkColor.rgb,(1-SAMPLE_TEXTURE2D(_DetailInkMap,sampler_DetailInkMap,v.uv).r)*_InternalInkStrength);
 if(_UseEmission>.5)color+=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,v.uv).rgb*_EmissionColor.rgb;
 float alpha=_Surface>.5?SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).a*_BaseColor.a:1;
 return float4(MixFog(color,v.fog),alpha);
 }
 ENDHLSL
 }
 Pass {
 Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0
 HLSLPROGRAM
 #pragma vertex TEShadowVertex
 #pragma fragment TEShadowFragment
 #pragma multi_compile_instancing
 #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
 float3 _LightDirection,_LightPosition;
 TEVaryings TEShadowVertex(TEAttributes v){TEVaryings o=TEVertex(v);float3 direction=_LightDirection;
 #if _CASTING_PUNCTUAL_LIGHT_SHADOW
 direction=normalize(_LightPosition-o.positionWS);
 #endif
 o.positionCS=ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(o.positionWS,normalize(o.normalWS),direction)));return o;}
 float4 TEShadowFragment(TEVaryings v):SV_Target{clip(.5-_Surface);TEClip(v.uv);return 0;}
 ENDHLSL
 }
 Pass {
 Name "DepthOnly" Tags {"LightMode"="DepthOnly"} ZWrite On ColorMask R
 HLSLPROGRAM
 #pragma vertex TEVertex
 #pragma fragment TEDepth
 #pragma multi_compile_instancing
 float4 TEDepth(TEVaryings v):SV_Target{clip(.5-_Surface);TEClip(v.uv);return 0;}
 ENDHLSL
 }
 Pass {
 Name "DepthNormals" Tags {"LightMode"="DepthNormalsOnly"} ZWrite On
 HLSLPROGRAM
 #pragma vertex TEVertex
 #pragma fragment TENormals
 #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
 #pragma multi_compile_instancing
 float4 TENormals(TEVaryings v):SV_Target{clip(.5-_Surface);TEClip(v.uv);float3 n=normalize(v.normalWS);
 #if defined(_GBUFFER_NORMALS_OCT)
 float2 oct=PackNormalOctQuadEncode(n);return float4(PackFloat2To888(saturate(oct*.5+.5)),0);
 #else
 return float4(n,0);
 #endif
 }
 ENDHLSL
 }
 }
 Fallback Off
}
