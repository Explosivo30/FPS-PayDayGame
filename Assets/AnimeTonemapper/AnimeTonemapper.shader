Shader "Hidden/Torbellino/AnimeTonemapper"
{
    HLSLINCLUDE
    #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
    #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

    float  _Exposure;
    float  _Contrast;
    float  _Saturation;
    float  _NeonBoost;

    float  _ShadowHueShift;
    float  _ShadowHueStr;
    float  _ShadowThreshold;
    float4 _ShadowColor;

    float  _HighlightSoft;
    float  _HighlightHueShift;
    float  _HighlightHueStr;
    float  _HighlightThreshold;

    float  _CelBanding;
    float  _CelBands;
    float  _CelHardness;

    // -------------------------------------------------------------------
    // Helpers HSV
    // -------------------------------------------------------------------
    float3 RGBtoHSV(float3 c)
    {
        float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
        float4 p = c.g < c.b ? float4(c.bg, K.wz) : float4(c.gb, K.xy);
        float4 q = c.r < p.x ? float4(p.xyw, c.r) : float4(c.r, p.yzx);
        float d = q.x - min(q.w, q.y);
        float e = 1.0e-10;
        return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
    }

    float3 HSVtoRGB(float3 c)
    {
        float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
        float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
        return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
    }

    float Luma(float3 c) { return dot(c, float3(0.2126, 0.7152, 0.0722)); }

    float3 LinearToGamma(float3 c) { return pow(max(c, 0.0001), 1.0 / 2.2); }
    float3 GammaToLinear(float3 c) { return pow(max(c, 0.0001), 2.2); }

    // -------------------------------------------------------------------
    // Cel banding — cuantiza luma en N bandas, preserva hue y sat
    // -------------------------------------------------------------------
    float3 ApplyCelBanding(float3 c, float bands, float hardness)
    {
        float3 hsv = RGBtoHSV(c);
        float luma = hsv.z;

        // Cuantizacion suave/dura segun hardness
        float bandSize  = 1.0 / bands;
        float bandIndex = floor(luma / bandSize);
        float bandFloor = bandIndex * bandSize;
        float bandT     = (luma - bandFloor) / bandSize; // 0..1 dentro de la banda

        // Mezcla entre cuantizado duro y continuo segun hardness
        float quantized  = bandFloor + bandSize * 0.5;
        float continuous = luma;
        float newLuma    = lerp(continuous, quantized, hardness);

        hsv.z = newLuma;
        return HSVtoRGB(hsv);
    }

    // -------------------------------------------------------------------
    // Hue shift — desplaza hue hacia target en zonas definidas por mask
    // -------------------------------------------------------------------
    float3 HueShift(float3 c, float targetHue, float strength, float mask)
    {
        float3 hsv = RGBtoHSV(c);
        // Interpolacion circular del hue
        float hueDiff = targetHue - hsv.x;
        // Tomar el camino mas corto en el circulo de hue
        hueDiff = hueDiff - round(hueDiff);
        hsv.x += hueDiff * strength * mask;
        hsv.x = frac(hsv.x);
        return HSVtoRGB(hsv);
    }

    // -------------------------------------------------------------------
    // Neon boost — saturacion exponencial: colores ya saturados se saturan mas
    // -------------------------------------------------------------------
    float3 NeonBoost(float3 c, float boost)
    {
        float3 hsv = RGBtoHSV(c);
        hsv.y = saturate(pow(hsv.y, 1.0 / boost)); // exponente inverso = boost en saturados
        return HSVtoRGB(hsv);
    }

    // -------------------------------------------------------------------
    // Fragment
    // -------------------------------------------------------------------
    float4 Frag(Varyings input) : SV_Target
    {
        float2 uv = input.texcoord;
        float4 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
        float3 c = color.rgb;

        // 1. Exposure (linear)
        c *= _Exposure;

        // 2. Tonemapping filmic suave — comprime highlights
        c = c / (c + _HighlightSoft);

        // 3. A gamma para operaciones perceptuales
        c = LinearToGamma(c);

        // 4. Contraste
        c = (c - 0.5) * _Contrast + 0.5;
        c = saturate(c);

        // 5. Saturacion base
        float luma = Luma(c);
        c = lerp(float3(luma, luma, luma), c, _Saturation);
        c = saturate(c);

        // 6. Neon boost — saturacion exponencial
        c = NeonBoost(c, _NeonBoost);
        c = saturate(c);

        // 7. Shadow hue shift + color
        luma = Luma(c);
        float shadowMask = 1.0 - smoothstep(_ShadowThreshold * 0.5, _ShadowThreshold, luma);
        // Primero mezclar shadow color base
        c = lerp(c, c * (1.0 - shadowMask) + (_ShadowColor.rgb + c) * shadowMask * 0.5, shadowMask * 0.4);
        // Luego desplazar hue hacia azul/violeta
        c = HueShift(c, _ShadowHueShift, _ShadowHueStr, shadowMask);
        c = saturate(c);

        // 8. Highlight hue shift — mantener color saturado, no ir a blanco
        float highlightMask = smoothstep(_HighlightThreshold, 1.0, luma);
        c = HueShift(c, _HighlightHueShift, _HighlightHueStr, highlightMask);
        c = saturate(c);

        // 9. Cel banding
        if (_CelBanding > 0.5)
        {
            c = ApplyCelBanding(c, _CelBands, _CelHardness);
            c = saturate(c);
        }

        // 10. Volver a linear
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
