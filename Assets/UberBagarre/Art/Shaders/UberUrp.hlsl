// Über Bagarre — le socle des shaders éclairés sous URP (le rendu de Schedule 1).
//
// Nos shaders maison (la peau et ses bleus, la triplanaire de la carte, le sol mouillé)
// calculent leur surface eux-mêmes, puis passent ici pour être éclairés EXACTEMENT comme un
// matériau « URP Lit » : le soleil et ses ombres en cascades, les lampes de la ville (Forward+),
// l'occlusion ambiante (SSAO), l'ambiance du ciel, les reflets des sondes, le brouillard.
// Une surface maison posée à côté d'un matériau d'origine de Schedule 1 réagit donc à la même
// lumière de la même façon.
#ifndef UBER_URP_INCLUDED
#define UBER_URP_INCLUDED

#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

// La lumière d'URP sur une surface déjà calculée (espace monde). Pour une surface transparente,
// définir _SURFACE_TYPE_TRANSPARENT (et _ALPHAPREMULTIPLY_ON pour un mélange prémultiplié)
// avant d'inclure ce fichier.
half4 UberShadeAlpha(float3 positionWS, float3 normalWS, float4 positionCS, float4 shadowCoordVS, half fogFactor,
                     half3 albedo, half metallic, half smoothness, half occlusion, half3 emission, half alpha)
{
    InputData inputData = (InputData)0;
    inputData.positionWS = positionWS;
    inputData.positionCS = positionCS;
    inputData.normalWS = NormalizeNormalPerPixel(normalWS);
    inputData.viewDirectionWS = GetWorldSpaceNormalizeViewDir(positionWS);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    inputData.shadowCoord = shadowCoordVS;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    inputData.shadowCoord = TransformWorldToShadowCoord(positionWS);
#else
    inputData.shadowCoord = float4(0, 0, 0, 0);
#endif

    inputData.fogCoord = fogFactor;
    inputData.bakedGI = SampleSH(inputData.normalWS);
    inputData.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(positionCS);
    inputData.shadowMask = half4(1, 1, 1, 1);

    SurfaceData surface = (SurfaceData)0;
    surface.albedo = albedo;
    surface.metallic = metallic;
    surface.specular = half3(0, 0, 0);
    surface.smoothness = smoothness;
    surface.occlusion = occlusion;
    surface.emission = emission;
    surface.alpha = alpha;
    surface.normalTS = half3(0, 0, 1);

    half4 color = UniversalFragmentPBR(inputData, surface);
    color.rgb = MixFog(color.rgb, inputData.fogCoord);
    color.a = alpha;
    return color;
}

half4 UberShade(float3 positionWS, float3 normalWS, float4 positionCS, float4 shadowCoordVS, half fogFactor,
                half3 albedo, half metallic, half smoothness, half occlusion, half3 emission)
{
    return UberShadeAlpha(positionWS, normalWS, positionCS, shadowCoordVS, fogFactor,
                          albedo, metallic, smoothness, occlusion, emission, 1);
}

// Une normale de texture (espace tangent) vers le monde.
float3 UberTangentToWorld(half3 normalTS, float3 normalWS, float4 tangentWS)
{
    float sign = tangentWS.w * GetOddNegativeScale();
    float3 bitangent = sign * cross(normalWS, tangentWS.xyz);
    return TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangent, normalWS));
}

#endif
