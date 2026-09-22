Shader "Tower/Cel Metal"
{
    Properties
    {
        _BaseColor("Color",Color)=(.2,.5,.3,1)
        _EmissionColor("Emission",Color)=(0,0,0,1)
        _Gloss("Paint highlight",Range(0,1))=.25
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _EmissionColor;
                half _Gloss;
            CBUFFER_END
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct V { float4 positionCS:SV_POSITION;float3 normalWS:TEXCOORD0;float3 positionWS:TEXCOORD1;half fog:TEXCOORD2; };
            V vert(A v) {
                V o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;
            }
            half4 frag(V v):SV_Target {
                Light l=GetMainLight(TransformWorldToShadowCoord(v.positionWS));
                half3 n=normalize(v.normalWS);half d=dot(n,l.direction)*.5+.5;
                half band=d>.72?1.0:(d>.4?.72:.44);
                half shade=lerp(.65,1,l.shadowAttenuation);
                half3 eye=GetWorldSpaceNormalizeViewDir(v.positionWS);
                half spec=step(.975,dot(n,normalize(l.direction+eye)))*_Gloss;
                half rim=step(.79,1-saturate(dot(n,eye)))*.075;
                half3 color=_BaseColor.rgb*(band*shade*l.color*.85+.22)+spec*l.color+rim+_EmissionColor.rgb;
                return half4(MixFog(color,v.fog),1);
            }
            ENDHLSL
        }
        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode"="ShadowCaster" }
            ZWrite On ZTest LEqual ColorMask 0
            HLSLPROGRAM
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            float3 _LightDirection;
            float3 _LightPosition;
            struct A { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            float4 ShadowVert(A v):SV_POSITION
            {
                float3 positionWS=TransformObjectToWorld(v.positionOS.xyz);
                float3 normalWS=TransformObjectToWorldNormal(v.normalOS);
                #if _CASTING_PUNCTUAL_LIGHT_SHADOW
                float3 direction=normalize(_LightPosition-positionWS);
                #else
                float3 direction=_LightDirection;
                #endif
                float4 p=TransformWorldToHClip(ApplyShadowBias(positionWS,normalWS,direction));
                #if UNITY_REVERSED_Z
                p.z=min(p.z,UNITY_NEAR_CLIP_VALUE);
                #else
                p.z=max(p.z,UNITY_NEAR_CLIP_VALUE);
                #endif
                return p;
            }
            half4 ShadowFrag():SV_Target { return 0; }
            ENDHLSL
        }
        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode"="DepthOnly" }
            ZWrite On ColorMask R
            HLSLPROGRAM
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float4 DepthVert(float4 p:POSITION):SV_POSITION { return TransformObjectToHClip(p.xyz); }
            half4 DepthFrag():SV_Target { return 0; }
            ENDHLSL
        }
    }
}
