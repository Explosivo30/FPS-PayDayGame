#ifndef CUSTOM_LIGHTING_INCLUDED
#define CUSTOM_LIGHTING_INCLUDED

// Only include these if not in preview mode
#if !defined(SHADERGRAPH_PREVIEW)
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"
    
    // Fixed pragma directives
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS
    #pragma multi_compile _ _MAIN_LIGHT_SHADOWS_CASCADE
    #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
    #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
    #pragma multi_compile_fragment _ _SHADOWS_SOFT
    #pragma multi_compile _ _SHADOWS_SHADOWMASK
    #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
    #pragma multi_compile _ LIGHTMAP_ON
    #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
#endif

// For fullscreen shaders, we need to reconstruct world position from screen coordinates
float3 ReconstructWorldPosition(float2 screenUV, float depth)
{
#if defined(SHADERGRAPH_PREVIEW)
    return float3(0, 0, 0);
#else
    // Convert screen UV to NDC
    float2 ndc = screenUV * 2.0 - 1.0;
    
    // Reconstruct world position using camera matrices
    float4 worldPos = mul(unity_CameraInvProjection, float4(ndc, depth, 1.0));
    worldPos.xyz /= worldPos.w;
    worldPos = mul(unity_CameraToWorld, worldPos);
    
    return worldPos.xyz;
#endif
}

// Reconstruct world normal from depth buffer
float3 ReconstructWorldNormal(float2 screenUV)
{
#if defined(SHADERGRAPH_PREVIEW)
    return float3(0, 0, 1);
#else
    // Sample depth at current pixel and neighbors
    float2 texelSize = _ScreenParams.zw - 1.0;
    
    float depth = SampleSceneDepth(screenUV);
    float depthR = SampleSceneDepth(screenUV + float2(texelSize.x, 0));
    float depthU = SampleSceneDepth(screenUV + float2(0, texelSize.y));
    
    // Reconstruct world positions
    float3 worldPos = ReconstructWorldPosition(screenUV, depth);
    float3 worldPosR = ReconstructWorldPosition(screenUV + float2(texelSize.x, 0), depthR);
    float3 worldPosU = ReconstructWorldPosition(screenUV + float2(0, texelSize.y), depthU);
    
    // Calculate normal using cross product
    float3 right = worldPosR - worldPos;
    float3 up = worldPosU - worldPos;
    
    return normalize(cross(right, up));
#endif
}

void MainLight_float(float2 ScreenUV, out float3 Direction, out float3 Color, out float ShadowAtten)
{
#if defined(SHADERGRAPH_PREVIEW)
    Direction = float3(0.5, 0.5, 0);
    Color = 1;
    ShadowAtten = 1;
#else
    // Sample depth to reconstruct world position
    float depth = SampleSceneDepth(ScreenUV);
    float3 worldPos = ReconstructWorldPosition(ScreenUV, depth);
    
    float4 shadowCoord = TransformWorldToShadowCoord(worldPos);
    Light mainLight = GetMainLight(shadowCoord);
    Direction = mainLight.direction;
    Color = mainLight.color;
    
#if !defined(_MAIN_LIGHT_SHADOWS) || defined(_RECEIVE_SHADOWS_OFF)
    ShadowAtten = 1.0;
#else
        ShadowSamplingData shadowSamplingData = GetMainLightShadowSamplingData();
        float shadowStrength = GetMainLightShadowStrength();
        ShadowAtten = SampleShadowmap(shadowCoord, TEXTURE2D_ARGS(_MainLightShadowmapTexture,
            sampler_MainLightShadowmapTexture),
            shadowSamplingData, shadowStrength, false);
#endif
#endif
}

void ReconstructNormal_float(float2 ScreenUV, out float3 WorldNormal)
{
    WorldNormal = ReconstructWorldNormal(ScreenUV);
}

void DirectSpecular_float(float Smoothness, float3 Direction, float3 WorldNormal, float3 WorldView, out float3 Out)
{
#if defined(SHADERGRAPH_PREVIEW)
    Out = 0;
#else
    float4 White = 1;
    Smoothness = exp2(10 * Smoothness + 1);
    WorldNormal = normalize(WorldNormal);
    WorldView = SafeNormalize(WorldView);
    Out = LightingSpecular(White.rgb, Direction, WorldNormal, WorldView, White, Smoothness);
#endif
}

void AdditionalLights_float(float Smoothness, float2 ScreenUV, float3 WorldNormal, float3 WorldView, out float3 Diffuse, out float3 Specular)
{
    float3 diffuseColor = 0;
    float3 specularColor = 0;
    
#if !defined(SHADERGRAPH_PREVIEW)
    // Reconstruct world position from screen coordinates
    float depth = SampleSceneDepth(ScreenUV);
    float3 worldPosition = ReconstructWorldPosition(ScreenUV, depth);
    
    float4 White = 1;
    Smoothness = exp2(10 * Smoothness + 1);
    WorldNormal = normalize(WorldNormal);
    WorldView = SafeNormalize(WorldView);
    
    int pixelLightCount = GetAdditionalLightsCount();
    for (int i = 0; i < pixelLightCount; ++i)
    {
        Light light = GetAdditionalLight(i, worldPosition);
        float3 attenuatedLightColor = light.color * (light.distanceAttenuation * light.shadowAttenuation);
        diffuseColor += LightingLambert(attenuatedLightColor, light.direction, WorldNormal);
        specularColor += LightingSpecular(attenuatedLightColor, light.direction, WorldNormal, WorldView, White, Smoothness);
    }
#endif
    
    Diffuse = diffuseColor;
    Specular = specularColor;
}

#endif