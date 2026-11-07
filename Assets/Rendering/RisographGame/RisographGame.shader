Shader "Hidden/Torbellino/RisographGame"
{
    Properties
    {
        [Header(Print and Color)]
        _Intensity("Print blend", Range(0,1)) = 0.92
        _Exposure("Exposure EV", Range(-2,2)) = 0
        _Contrast("Contrast", Range(.5,2)) = 1.04
        _Saturation("Saturation", Range(0,2)) = 1.02
        _MidtoneLift("Midtone lift", Range(0,1)) = 0.24
        _PaperHighlights("Paper highlight recovery", Range(0,1)) = 0.16
        _ColorRetention("Original color retention", Range(0,1)) = 0.28
        _FormRetention("Original tonal form", Range(0,1)) = 0.3
        [Header(Four Printing Inks in sRGB)]
        _ShadowInk("Indigo key ink (sRGB)", Vector) = (.055,.08,.13,1)
        _MainInk("Machine teal ink (sRGB)", Vector) = (.12,.48,.48,1)
        _AccentInk("Conduit copper ink (sRGB)", Vector) = (.86,.34,.16,1)
        _Paper("Cream paper (sRGB)", Vector) = (.96,.91,.8,1)
        [Header(Fine Static Print)]
        _DotSize("Cell size at 1080p", Range(2,12)) = 3.2
        _ScreenAngle("Screen angle in degrees", Range(0,90)) = 35
        _Halftone("Halftone amount", Range(0,1)) = 0.58
        _Grain("Paper grain", Range(0,.3)) = 0.018
        _Wear("Ink wear", Range(0,.15)) = 0.002
        _Registration("Warm plate offset at 1080p", Range(0,2)) = 0
        [Header(Readability Color Heuristics)]
        _EdgePreservation("Structural edge source recovery", Range(0,1)) = 0.45
        _SignalPreservation("Saturated bright color recovery", Range(0,1)) = 0.55
        _SignalSaturation("Color recovery saturation start", Range(0,1)) = 0.5
        _SignalLuminance("Color recovery luminance start", Range(0,1)) = 0.12
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off
        Pass
        {
            Name "Risograph Game Print"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _ShadowInk, _MainInk, _AccentInk, _Paper;
                float _Intensity, _Exposure, _Contrast, _Saturation, _MidtoneLift, _PaperHighlights;
                float _ColorRetention, _FormRetention, _DotSize, _ScreenAngle, _Halftone, _Grain, _Wear, _Registration;
                float _EdgePreservation, _SignalPreservation, _SignalSaturation, _SignalLuminance;
            CBUFFER_END
            float Luma(float3 c) { return dot(c,float3(.2126,.7152,.0722)); }
            float3 ToDisplay(float3 c) {
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    return LinearToSRGB(max(c,0));
                #else
                    return c;
                #endif
            }
            float3 ToOutput(float3 c) {
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    return SRGBToLinear(saturate(c));
                #else
                    return c;
                #endif
            }
            float3 ReadDisplay(float2 uv) { return saturate(ToDisplay(SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,saturate(uv)).rgb)); }
            float3 Prepare(float3 c) {
                c=pow(saturate(c*exp2(_Exposure)),lerp(1,.65,_MidtoneLift));
                c=max((c-.5)*_Contrast+.5,0);
                float y=Luma(c);c=saturate(lerp(y.xxx,c,_Saturation));
                return lerp(c,_Paper.rgb,smoothstep(.72,.98,Luma(c))*_PaperHighlights);
            }
            float Hash(float2 p) { float3 q=frac(float3(p.xyx)*.1031);q+=dot(q,q.yzx+33.33);return frac((q.x+q.y)*q.z); }
            float3 Perceptual(float3 c) { return float3(Luma(c)*1.8,(c.r-c.b)*.72,(c.g-(c.r+c.b)*.5)*.72); }
            void Candidate(float3 target,float3 pigment,inout float nearest,inout float next,inout float3 first,inout float3 second) {
                float3 delta=target-Perceptual(pigment);float d=dot(delta,delta);
                if(d<nearest){next=nearest;second=first;nearest=d;first=pigment;}
                else if(d<next){next=d;second=pigment;}
            }
            float Dots(float2 grid,float coverage) {
                float inverse=step(.5,coverage);float area=lerp(coverage,1-coverage,inverse);
                float2 local=frac(grid+inverse*.5)-.5;float distance=length(local);
                float radius=sqrt(max(area,0)/PI);
                float aa=max(max(fwidth(grid.x),fwidth(grid.y))*.45,.0001);
                float ink=1-smoothstep(radius-aa,radius+aa,distance);ink=lerp(ink,1-ink,inverse);
                return coverage<=.001?0:(coverage>=.999?1:ink);
            }
            half4 Frag(Varyings input):SV_Target {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                float4 source=SAMPLE_TEXTURE2D_X(_BlitTexture,sampler_LinearClamp,uv);
                float3 original=saturate(ToDisplay(source.rgb));
                float3 prepared=Prepare(original);
                // _ScreenParams provides the actual camera target, including GPU preview renders.
                float2 size=max(_ScreenParams.xy,float2(1,1));
                float2 refSize=float2(size.x/size.y,1)*1080;
                float2 pixels=uv*refSize;
                if(_Registration>.001){float2 shift=float2(_Registration,-_Registration*.4)/refSize;float3 moved=Prepare(ReadDisplay(uv+shift));float warm=saturate((moved.r-moved.b)*2);prepared=lerp(prepared,moved,warm*.5);}
                float3 target=Perceptual(prepared),first=_ShadowInk.rgb,second=_Paper.rgb;
                float a=1e6,b=1e6;
                Candidate(target,_ShadowInk.rgb,a,b,first,second);Candidate(target,_MainInk.rgb,a,b,first,second);
                Candidate(target,_AccentInk.rgb,a,b,first,second);Candidate(target,_Paper.rgb,a,b,first,second);
                if(Luma(first)>Luma(second)){float3 temp=first;first=second;second=temp;}
                float3 dark=Perceptual(first),axis=Perceptual(second)-dark;
                float lightCoverage=saturate(dot(target-dark,axis)/max(dot(axis,axis),.00001));
                lightCoverage=lerp(step(.5,lightCoverage),lightCoverage,_Halftone);
                float sine,cosine;sincos(radians(_ScreenAngle),sine,cosine);
                float cell=max(_DotSize,3*1080/size.y);
                float2 grid=float2(cosine*pixels.x-sine*pixels.y,sine*pixels.x+cosine*pixels.y)/cell;
                float coverage=Dots(grid,1-lightCoverage);
                float3 printed=lerp(second,first,coverage);
                // Carry source shading on the LOW-frequency ink mixture, never use dot luma for normalization.
                float3 meanInk=lerp(first,second,lightCoverage);
                float tonalRatio=clamp((Luma(prepared)+.025)/(Luma(meanInk)+.025),.65,1.35);
                printed*=lerp(1,tonalRatio,_FormRetention);
                printed=lerp(printed,prepared,_ColorRetention);
                float maxChannel=max(original.r,max(original.g,original.b));float minChannel=min(original.r,min(original.g,original.b));
                float saturation=(maxChannel-minChannel)/max(maxChannel,.001);
                // Heuristic guard, not an identification of warnings, enemies or emissive objects.
                float signal=smoothstep(_SignalSaturation,min(_SignalSaturation+.22,1.001),saturation)
                           *smoothstep(_SignalLuminance,min(_SignalLuminance+.2,1.001),Luma(original));
                printed=lerp(printed,original,signal*_SignalPreservation);
                if(_EdgePreservation>.001){
                    float2 texel=1/size;
                    float gx=abs(Luma(ReadDisplay(uv+float2(texel.x,0)))-Luma(ReadDisplay(uv-float2(texel.x,0))));
                    float gy=abs(Luma(ReadDisplay(uv+float2(0,texel.y)))-Luma(ReadDisplay(uv-float2(0,texel.y))));
                    float edge=smoothstep(.06,.3,gx+gy);
                    printed=lerp(printed,original,edge*_EdgePreservation);
                }
                // Wear is intentionally very low and fades out on guarded saturated pixels.
                float noise=Hash(floor(pixels));float wear=step(1-_Wear*.45,noise)*(1-signal*.8);
                printed=lerp(printed,_Paper.rgb,wear);
                printed=saturate(printed+(noise-.5)*_Grain*(1-signal*.6));
                return half4(lerp(source.rgb,ToOutput(printed),_Intensity),source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}

