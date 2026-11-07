#ifndef TE_LIGHTING
#define TE_LIGHTING
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
float4 TEShadowCoord(TEVaryings v){
 #if defined(_MAIN_LIGHT_SHADOWS_SCREEN)
 return ComputeScreenPos(TransformWorldToHClip(v.positionWS));
 #else
 return TransformWorldToShadowCoord(v.positionWS);
 #endif
}
float TELuma(float3 c){return dot(c,float3(.2126,.7152,.0722));}
void TEAccumulate(Light l,float3 n,inout float3 direct){direct+=l.color*saturate(dot(n,l.direction))*l.distanceAttenuation*l.shadowAttenuation;}
float3 TELight(TEVaryings v,float3 n,out float lightLevel){
 float3 direct=0;
 Light mainLight=GetMainLight(TEShadowCoord(v));TEAccumulate(mainLight,n,direct);
 #if defined(_ADDITIONAL_LIGHTS)
 InputData inputData=(InputData)0;inputData.positionWS=v.positionWS;inputData.normalWS=n;inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);
 #if USE_FORWARD_PLUS
 UNITY_LOOP for(uint lightIndex=0;lightIndex<min(URP_FP_DIRECTIONAL_LIGHTS_COUNT,MAX_VISIBLE_LIGHTS);lightIndex++){Light light=GetAdditionalLight(lightIndex,v.positionWS,half4(1,1,1,1));TEAccumulate(light,n,direct);}
 #endif
 uint pixelLightCount=GetAdditionalLightsCount();
 LIGHT_LOOP_BEGIN(pixelLightCount)
 Light light=GetAdditionalLight(lightIndex,v.positionWS,half4(1,1,1,1));TEAccumulate(light,n,direct);
 LIGHT_LOOP_END
 #endif
 float3 ambient=max(SampleSH(n),0)*_AmbientStrength;
 lightLevel=TELuma(direct)*_LightingIntensity+TELuma(ambient);
 float width=max(_Transition,.001);
 float middle=smoothstep(_ShadowThreshold-width,_ShadowThreshold+width,lightLevel);
 float high=smoothstep(max(_HighlightThreshold,_ShadowThreshold+.02)-width,max(_HighlightThreshold,_ShadowThreshold+.02)+width,lightLevel);
 float3 painted=lerp(_ShadowTint.rgb,_MidTint.rgb,middle);painted=lerp(painted,_HighlightTint.rgb,high);
 float brightness=saturate(lightLevel);
 // Small continuous component preserves form within each painted region.
 painted*=lerp(1,.55+.65*brightness,_VolumeBlend);
 float3 lightHue=direct+ambient;lightHue/=max(TELuma(lightHue),.08);
 return painted*lerp(float3(1,1,1),clamp(lightHue,.3,2),.22)*min(1,lightLevel/.12);
}
float3 TEColor(float3 color){float y=TELuma(color);color=max(0,lerp(y.xxx,color,_Saturation));color=max(0,(color-.18)*_Contrast+.18);if(_Quantization>1)color=floor(color*_Quantization+.5)/_Quantization;return color;}
#endif
