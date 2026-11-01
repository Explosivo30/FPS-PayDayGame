Shader "Hidden/Torbellino/Risograph"
{
    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off ZTest Always Cull Off Blend Off

        Pass
        {
            Name "Risograph Print"
            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _RisoTone;    // blend, exposure multiplier, contrast, saturation
            float4 _RisoLightness; // midtone lift, paper highlight recovery
            float4 _RisoScreen;  // cell pixels at 1080p, angle, halftone amount, palette size
            float4 _RisoTexture; // grain, wear, registration pixels at 1080p
            float4 _ShadowInk, _BlueInk, _PinkInk, _Paper;

            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float PaperNoise(float2 p)
            {
                float2 cell = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(cell), Hash(cell + float2(1, 0)), f.x),
                    lerp(Hash(cell + float2(0, 1)), Hash(cell + 1), f.x), f.y);
            }

            float Luma(float3 color) { return dot(color, float3(0.2126, 0.7152, 0.0722)); }

            float3 Perceptual(float3 color)
            {
                // Weight luminance more heavily to keep silhouettes legible.
                return float3(Luma(color) * 1.6, (color.r - color.b) * 0.65,
                    (color.g - (color.r + color.b) * 0.5) * 0.65);
            }

            void Candidate(float3 target, float3 ink, inout float firstDistance,
                inout float secondDistance, inout float3 first, inout float3 second)
            {
                float3 delta = target - Perceptual(ink);
                float distance = dot(delta, delta);
                if (distance < firstDistance)
                {
                    secondDistance = firstDistance;
                    second = first;
                    firstDistance = distance;
                    first = ink;
                }
                else if (distance < secondDistance)
                {
                    secondDistance = distance;
                    second = ink;
                }
            }

            float DotMask(float2 position, float coverage)
            {
                // Grow dark dots, then close the gaps with small paper holes.
                // sqrt makes dot AREA proportional to ink coverage.
                float inverse = step(0.5, coverage);
                float area = lerp(coverage, 1.0 - coverage, inverse);
                float2 local = frac(position + inverse * 0.5) - 0.5;
                float distance = length(local);
                float radius = sqrt(max(area, 0.0) / PI);
                // Derive filter width from the unwrapped grid to avoid seams in frac().
                float aa = max(max(fwidth(position.x), fwidth(position.y)) * 0.45, 0.0001);
                float mask = 1.0 - smoothstep(radius - aa, radius + aa, distance);
                mask = lerp(mask, 1.0 - mask, inverse);
                // Completely flat inks and bare paper stay flat.
                return coverage <= 0.001 ? 0.0 : (coverage >= 0.999 ? 1.0 : mask);
            }

            float3 ReadPrintColor(float2 uv)
            {
                float3 color = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv).rgb;
                #if !defined(UNITY_COLORSPACE_GAMMA)
                    color = LinearToSRGB(max(color, 0.0));
                #endif
                color *= _RisoTone.y;
                // Lift midtones before palette matching while keeping true blacks anchored.
                // A saturated lit surface should be able to reach paper, not stay dark ink.
                color = pow(saturate(color), lerp(1.0, 0.5, _RisoLightness.x));
                color = saturate((color - 0.5) * _RisoTone.z + 0.5);
                float luminance = Luma(color);
                color = saturate(lerp(luminance.xxx, color, _RisoTone.w));
                float paperLight = smoothstep(0.48, 0.88, luminance) * _RisoLightness.y;
                return lerp(color, _Paper.rgb, paperLight);
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                float4 source = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, uv);
                // Reference pixels keep the print scale independent of render resolution.
                float2 pixels = uv * float2(_ScreenParams.x / _ScreenParams.y, 1.0) * 1080.0;
                float2 registration = float2(_RisoTexture.z, -_RisoTexture.z * 0.45)
                    / (float2(_ScreenParams.x / _ScreenParams.y, 1.0) * 1080.0);
                float3 color = ReadPrintColor(uv);
                // Move warm areas as a separate ink plate instead of splitting RGB channels.
                if (_RisoTexture.z > 0.001)
                {
                    float3 shifted = ReadPrintColor(uv + registration);
                    float warmth = saturate(0.5 + (shifted.r - shifted.b) * 2.0);
                    color = lerp(color, shifted, warmth);
                }

                float3 target = Perceptual(color);
                float firstDistance = 1e6, secondDistance = 1e6;
                float3 first = _ShadowInk.rgb, second = _Paper.rgb;
                Candidate(target, _ShadowInk.rgb, firstDistance, secondDistance, first, second);
                if (_RisoScreen.w > 3.5)
                    Candidate(target, _BlueInk.rgb, firstDistance, secondDistance, first, second);
                Candidate(target, _PinkInk.rgb, firstDistance, secondDistance, first, second);
                Candidate(target, _Paper.rgb, firstDistance, secondDistance, first, second);

                // Consistent dark-on-light dots, even at palette boundaries.
                if (Luma(first) > Luma(second))
                {
                    float3 swap = first;
                    first = second;
                    second = swap;
                }
                float3 dark = Perceptual(first);
                float3 axis = Perceptual(second) - dark;
                float lightCoverage = saturate(dot(target - dark, axis) / max(dot(axis, axis), 0.00001));
                lightCoverage = lerp(step(0.5, lightCoverage), lightCoverage, _RisoScreen.z);
                float sine, cosine;
                sincos(_RisoScreen.y, sine, cosine);
                // At least three render pixels per cell prevents undersampled moiré.
                float cellSize = max(_RisoScreen.x, 3.0 * 1080.0 / _ScreenParams.y);
                float2 screen = float2(cosine * pixels.x - sine * pixels.y,
                    sine * pixels.x + cosine * pixels.y) / cellSize;
                float ink = DotMask(screen, 1.0 - lightCoverage);
                float3 printed = lerp(second, first, ink);

                // Static noise: the paper does not flicker when the camera is still.
                float fleck = Hash(floor(pixels));
                float fibers = PaperNoise(pixels * float2(0.3, 0.9));
                float wear = step(1.0 - _RisoTexture.y * 0.45, fleck);
                printed = lerp(printed, _Paper.rgb, wear);
                float grain = (fleck - 0.5) * 0.55 + (fibers - 0.5) * 0.45;
                printed = saturate(printed + grain * _RisoTexture.x);

                #if !defined(UNITY_COLORSPACE_GAMMA)
                    printed = SRGBToLinear(printed);
                #endif
                return half4(lerp(source.rgb, printed, _RisoTone.x), source.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
