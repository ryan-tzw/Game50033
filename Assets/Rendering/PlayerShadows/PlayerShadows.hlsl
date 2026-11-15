#ifndef GAME50033_PLAYER_SHADOWS_INCLUDED
#define GAME50033_PLAYER_SHADOWS_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"

// both receivers share cascade selection/filtering; the weapon variant binds its environment-only atlas.
#if defined(_RECEIVE_WEAPON_SHADOWS)
    #define _PlayerShadowMap _WeaponShadowMap
    #define _PlayerWorldToShadow _WeaponWorldToShadow
    #define _PlayerShadowSplitSpheres _WeaponShadowSplitSpheres
    #define _PlayerShadowSplitRadii _WeaponShadowSplitRadii
    #define _PlayerShadowMapSize _WeaponShadowMapSize
    #define _PlayerShadowCascadeCount _WeaponShadowCascadeCount
    #define _PlayerShadowMapAvailable _WeaponShadowMapAvailable
#endif

TEXTURE2D_SHADOW(_PlayerShadowMap);
float4x4 _PlayerWorldToShadow[5];
float4 _PlayerShadowSplitSpheres[4];
float4 _PlayerShadowSplitRadii;
float4 _PlayerShadowMapSize;
int _PlayerShadowCascadeCount;
float _PlayerShadowMapAvailable;

float4 PlayerWorldToShadowCoord(float3 casterPositionWS)
{
    if (_PlayerShadowMapAvailable < 0.5)
        return TransformWorldToShadowCoord(casterPositionWS);
    if (_PlayerShadowMapAvailable > 1.5)
        return float4(0, 0, 0, 0);

    uint cascade = 0;
    if (_PlayerShadowCascadeCount > 1)
    {
        cascade = 4;
        for (uint i = 0; i < (uint)_PlayerShadowCascadeCount; i++)
        {
            float3 offset = casterPositionWS - _PlayerShadowSplitSpheres[i].xyz;
            if (dot(offset, offset) < _PlayerShadowSplitRadii[i]) { cascade = i; break; }
        }
    }
    return float4(mul(_PlayerWorldToShadow[cascade], float4(casterPositionWS, 1.0)).xyz, 0.0);
}

half PlayerMainLightShadow(float4 shadowCoord, float3 positionWS, half4 shadowMask, half4 occlusionProbeChannels)
{
    // unsupported/disabled cameras retain URP's normal shadow reception instead of sampling a previous camera's map.
    if (_PlayerShadowMapAvailable < 0.5)
        return MainLightShadow(shadowCoord, positionWS, shadowMask, occlusionProbeChannels);

    half4 shadowParams = GetMainLightShadowParams();
    half realtimeShadow = 1.0h;
#if defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    // availability 2 denotes a valid, empty caster set. retain baked shadows below but skip realtime sampling.
    if (_PlayerShadowMapAvailable < 1.5)
    {
        ShadowSamplingData sampling = GetMainLightShadowSamplingData();
        sampling.shadowmapSize = _PlayerShadowMapSize;
        sampling.shadowOffset0 = _PlayerShadowMapSize.xyxy * float4(-0.5, -0.5, 0.5, -0.5);
        sampling.shadowOffset1 = _PlayerShadowMapSize.xyxy * float4(-0.5, 0.5, 0.5, 0.5);
        realtimeShadow = SampleShadowmap(TEXTURE2D_ARGS(_PlayerShadowMap, sampler_LinearClampCompare),
            shadowCoord, sampling, shadowParams, false);
    }
#endif
#ifdef CALCULATE_BAKED_SHADOWS
    half bakedShadow = BakedShadow(shadowMask, occlusionProbeChannels, shadowParams);
#else
    half bakedShadow = 1.0h;
#endif
    return MixRealtimeAndBakedShadows(realtimeShadow, bakedShadow, GetMainLightShadowFade(positionWS));
}

#endif
