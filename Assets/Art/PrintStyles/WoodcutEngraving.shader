Shader "HELIX/Print/Woodcut Engraving"
{
 Properties {
  _PaperColor("Paper tint (sRGB values)",Vector)=(.94,.89,.76,1)
  _InkColor("Ink tint (sRGB values)",Vector)=(.045,.065,.09,1)
  _Intensity("Effect strength",Range(0,1))=.85
  _Spacing("Line spacing in output pixels",Range(2,16))=6
  _Grain("Paper grain",Range(0,.3))=.07
  _EdgeStrength("Luminance edges",Range(0,4))=1.2
  _ColorRetention("Color stain",Range(0,1))=.35
  _Registration("Unused in engraving",Float)=0
 }
 SubShader {
  Tags { "RenderPipeline"="UniversalPipeline" }
  Pass {
   Name "Woodcut"
   ZWrite Off ZTest Always Cull Off
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex Vert
   #pragma fragment PrintFragment
   #pragma multi_compile _ STEREO_INSTANCING_ON STEREO_MULTIVIEW_ON
   #define PRINT_WOODCUT 1
   #include "PrintCommon.hlsl"
   ENDHLSL
  }
 }
}
