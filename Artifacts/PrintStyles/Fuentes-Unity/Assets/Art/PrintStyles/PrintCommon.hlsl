#ifndef HELIX_PRINT_STYLES
#define HELIX_PRINT_STYLES
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
CBUFFER_START(UnityPerMaterial)
float4 _PaperColor, _InkColor;
float _Intensity, _Spacing, _Grain, _EdgeStrength, _ColorRetention, _Registration;
CBUFFER_END
float Lum(float3 c) { return dot(c,float3(.2126,.7152,.0722)); }
float3 ReadPrint(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,saturate(uv)).rgb; }
float NoisePrint(float2 p) { return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453); }
// Derivative-filtered lines fade to their coverage when too fine to resolve.
float Cut(float phase,float coverage) {
    if(coverage<=0) return 0;
    if(coverage>=1) return 1;
    float w=max(fwidth(phase),.025);
    float stripe=1-smoothstep(coverage-w,coverage+w,abs(frac(phase)-.5)*2);
    return lerp(stripe,coverage,saturate(w-.35));
}
float DotPrint(float2 p,float coverage) {
    float d=length(frac(p)-.5);
    float radius=sqrt(saturate(coverage)/3.14159265);
    float aa=max(fwidth(d),.035);
    return 1-smoothstep(radius-aa,radius+aa,d);
}
half4 PrintFragment(Varyings v):SV_Target {
    UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(v);
    float2 uv=v.texcoord, texel=_BlitTexture_TexelSize.xy;
    // Work in display-referred color. URP supplies linear scene color after post FX.
    float3 original=ReadPrint(uv);
    float3 c=saturate(LinearToSRGB(max(original,0)));
    float y=Lum(c);
    float2 pixel=uv*_BlitTexture_TexelSize.zw;
    // Stable paper grain: no _Time and no per-frame random seed.
    float grain=NoisePrint(floor(pixel))-.5;
    float3 paper=_PaperColor.rgb*(1+grain*_Grain);
    float edge=abs(Lum(LinearToSRGB(max(ReadPrint(uv+float2(texel.x,0)),0)))-Lum(LinearToSRGB(max(ReadPrint(uv-float2(texel.x,0)),0))))
              +abs(Lum(LinearToSRGB(max(ReadPrint(uv+float2(0,texel.y)),0)))-Lum(LinearToSRGB(max(ReadPrint(uv-float2(0,texel.y)),0))));
    float3 result;
#if defined(PRINT_WOODCUT)
    float darkness=saturate((.85-pow(y,.60))/.85);
    float2 p=(uv * float2(_ScreenParams.x/_ScreenParams.y,1)*1080)/max(_Spacing,3*1080/_ScreenParams.y);
    float bend=.09*sin(p.y*.53)+.04*sin(p.x*.91);
    float lines=Cut(p.x*.8+p.y*.6+bend,saturate((darkness-.12)*1.15));
    float cross=Cut(-p.x*.6+p.y*.8+bend,saturate((darkness-.65)*2.2));
    float solid=smoothstep(.76,.94,darkness);
    float coverage=max(max(lines,cross),solid);
    // A restrained original-color stain retains gameplay color families.
    float3 stainedPaper=paper*lerp(float3(1,1,1),.4+.6*c,_ColorRetention);
    result=lerp(stainedPaper,_InkColor.rgb,coverage);
#endif
    result=lerp(result,_InkColor.rgb,saturate(edge*_EdgeStrength));
    return half4(lerp(original,SRGBToLinear(saturate(result)),_Intensity),1);
}
#endif



