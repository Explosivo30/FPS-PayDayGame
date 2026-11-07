Shader "Torbellino/Telltale Engraving/Outline"
{
 Properties {
 [Toggle] _OutlineEnabled("Enable outline",Float)=1
 _OutlineColor("Outline color",Color)=(.012,.018,.032,1)
 _OutlineWidth("Width at 1080p (pixels)",Range(0,5))=1.15
 _DistanceCorrection("Constant screen width",Range(0,1))=1
 _ReferenceDistance("Reference distance for world width",Float)=4
 _NormalSensitivity("Suppress face-on extrusion",Range(0,1))=.6
 _EdgeRoughness("Stable edge variation",Range(0,.3))=.03
 _BaseMap("Source alpha texture",2D)="white"{}
 _BaseColor("Source alpha multiplier",Color)=(1,1,1,1)
 [Toggle] _AlphaClip("Source cutout",Float)=0
 _Cutoff("Cutout threshold",Range(0,1))=.5
 }
 SubShader{
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
 Pass{
 Tags {"LightMode"="SRPDefaultUnlit"} Cull Front ZWrite Off ZTest LEqual
 HLSLPROGRAM
 #pragma target 3.5
 #pragma vertex Vert
 #pragma fragment Frag
 #pragma multi_compile_instancing
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
 CBUFFER_START(UnityPerMaterial)
 float4 _OutlineColor,_BaseMap_ST,_BaseColor;
 float _OutlineEnabled,_OutlineWidth,_DistanceCorrection,_ReferenceDistance,_NormalSensitivity,_EdgeRoughness,_AlphaClip,_Cutoff;
 CBUFFER_END
 struct A{float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;UNITY_VERTEX_INPUT_INSTANCE_ID};
 struct V{float4 p:SV_POSITION;float2 uv:TEXCOORD0;UNITY_VERTEX_OUTPUT_STEREO};
 V Vert(A v){V o=(V)0;UNITY_SETUP_INSTANCE_ID(v);UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);float3 world=TransformObjectToWorld(v.p.xyz);float3 n=TransformObjectToWorldNormal(v.n);float4 p=TransformWorldToHClip(world);float3 nv=TransformWorldToViewDir(n);float2 dir=mul((float2x2)UNITY_MATRIX_P,nv.xy);float len=length(dir);dir/=max(len,.0001);float face=lerp(1,smoothstep(.02,.35,len),_NormalSensitivity);float rough=1+sin(dot(v.p.xyz,float3(13,29,17)))*_EdgeRoughness;float distance=length(GetCameraPositionWS()-world);float distanceScale=lerp(_ReferenceDistance/max(distance,.1),1,_DistanceCorrection);p.xy+=dir*(_OutlineWidth*rough*face*distanceScale*2/1080)*p.w*float2(_ScreenParams.y/_ScreenParams.x,1);o.p=p;o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;}
 float4 Frag(V v):SV_Target{clip(_OutlineEnabled-.5);if(_AlphaClip>.5)clip(SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv).a*_BaseColor.a-_Cutoff);return _OutlineColor;}
 ENDHLSL
 }
 }
}
