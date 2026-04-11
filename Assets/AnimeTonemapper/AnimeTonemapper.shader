Shader "Hidden/Torbellino/AnimeTonemapper"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float  _Exposure;
    float  _Contrast;
    float  _Saturation;
    float  _ShadowTint;
    float  _HighlightSoft;
    float4 _ShadowColor;

    float3 AnimeTonemap(float3 x, float soft)
    {
        return x / (x + soft);
    }

    float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

    float3 LinearToGamma(float3 c)
    {
        return pow(max(c, 0.0001), 1.0 / 2.2);
    }

    float3 GammaToLinear(float3 c)
    {
        return pow(max(c, 0.0001), 2.2);
    }

    float4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        float3 c = color.rgb;

        // 1. Exposure (en linear)
        c *= _Exposure;

        // 2. Tonemapping filmic — opera en linear, correcto
        c = AnimeTonemap(c, _HighlightSoft);

        // 3. Convertir a gamma para operaciones perceptuales
        c = LinearToGamma(c);

        // 4. Contraste
        c = (c - 0.5) * _Contrast + 0.5;
        c = saturate(c);

        // 5. Saturacion
        float luma = Luma(c);
        c = lerp(float3(luma, luma, luma), c, _Saturation);

        // 6. Shadow tint
        luma = Luma(c);
        float shadowMask = 1.0 - smoothstep(0.0, 0.4, luma);
        c = lerp(c, c + _ShadowColor.rgb * _ShadowTint, shadowMask);
        c = saturate(c);

        // 7. Volver a linear
        c = GammaToLinear(c);

        return float4(saturate(c), color.a);
    }
    ENDHLSL

    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Blend Off Cull Off

        Pass
        {
            Name "AnimeTonemapper"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            ENDHLSL
        }
    }
}
