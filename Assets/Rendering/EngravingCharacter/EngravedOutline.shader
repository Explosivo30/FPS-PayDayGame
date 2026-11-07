Shader "Torbellino/Characters/Engraved Outline"
{
 Properties { _InkColor("Contour ink",Color)=(.012,.019,.03,1) _Width("Width at 1080p",Range(0,6))=1.5 }
 SubShader {
 Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry+1"}
 Pass {
  Tags {"LightMode"="SRPDefaultUnlit"}
  Cull Front ZWrite Off ZTest LEqual
  HLSLPROGRAM
  #pragma vertex Vert
  #pragma fragment Frag
  #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
  CBUFFER_START(UnityPerMaterial)
  float4 _InkColor;float _Width;
  CBUFFER_END
  struct A {float4 p:POSITION;float3 n:NORMAL;};
  float4 Vert(A v):SV_POSITION {
   float4 p=TransformObjectToHClip(v.p.xyz);
   float3 nVS=TransformWorldToViewDir(TransformObjectToWorldNormal(v.n));
   float2 direction=mul((float2x2)UNITY_MATRIX_P,nVS.xy);
   direction=normalize(direction+float2(.00001,0));
   float irregular=.72+.22*sin(dot(v.p.xyz,float3(47,29,71)))+.06*sin(v.p.y*139);
   p.xy+=direction*(_Width*irregular*2/1080)*p.w*float2(_ScreenParams.y/_ScreenParams.x,1);
   return p;
  }
  half4 Frag():SV_Target{return _InkColor;}
  ENDHLSL
 }
 }
}
