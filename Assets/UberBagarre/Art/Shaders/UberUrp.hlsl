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

// La lumière d'URP sur une surface déjà calculée (espace monde), SANS le brouillard : pour qui
// ajoute sa propre lumière avant de brouiller (le feuillage, les cheveux). Pour une surface
// transparente, définir _SURFACE_TYPE_TRANSPARENT (et _ALPHAPREMULTIPLY_ON pour un mélange
// prémultiplié) avant d'inclure ce fichier.
half4 UberShadeUnfogged(float3 positionWS, float3 normalWS, float4 positionCS, float4 shadowCoordVS,
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

    inputData.fogCoord = 0;
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
    color.a = alpha;
    return color;
}

// La même, brouillard compris. Attention : la valeur « sans brouillard » de fogFactor dépend du
// mode (1 en linéaire, 0 en exponentiel) — on ne passe jamais une constante, toujours celle
// calculée au sommet (ComputeFogFactor).
half4 UberShadeAlpha(float3 positionWS, float3 normalWS, float4 positionCS, float4 shadowCoordVS, half fogFactor,
                     half3 albedo, half metallic, half smoothness, half occlusion, half3 emission, half alpha)
{
    half4 color = UberShadeUnfogged(positionWS, normalWS, positionCS, shadowCoordVS, albedo, metallic, smoothness, occlusion,
                                    emission, alpha);
    color.rgb = MixFog(color.rgb, fogFactor);
    return color;
}

half4 UberShade(float3 positionWS, float3 normalWS, float4 positionCS, float4 shadowCoordVS, half fogFactor,
                half3 albedo, half metallic, half smoothness, half occlusion, half3 emission)
{
    return UberShadeAlpha(positionWS, normalWS, positionCS, shadowCoordVS, fogFactor,
                          albedo, metallic, smoothness, occlusion, emission, 1);
}

// Le feuillage (sapins, herbe). Une feuille n'est pas une plaque : la lumière l'enveloppe et la
// traverse. Éclairé comme une surface ordinaire, un sapin devient un damier de faces claires et
// de faces noires, et ses propres ombres le hachent de taches dures — c'était le « feuillage
// bizarre ». Ici :
// - la normale est ramenée vers le haut : la ramure s'éclaire comme un volume, du sommet ;
// - l'intérieur de la ramure reçoit moins de ciel (occlusion) : les ombres restent d'un vert
//   profond au lieu de virer au vert clair bleuté du ciel ;
// - l'ombre que l'arbre se porte à lui-même est un peu adoucie (la lumière passe entre les aiguilles) ;
// - à contre-jour, le soleil traverse les feuilles (translucidité) ;
// - aucun reflet brillant.
half4 UberShadeFoliage(float3 positionWS, float3 normalWS, float4 positionCS, float4 shadowCoordVS, half fogFactor,
                       half3 albedo, half translucency)
{
    half3 n = normalize(lerp(normalize(normalWS), half3(0, 1, 0), 0.55));
    half4 color = UberShadeUnfogged(positionWS, n, positionCS, shadowCoordVS, albedo, 0, 0.04, 0.55, half3(0, 0, 0), 1);

#if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
    float4 shadowCoord = shadowCoordVS;
#elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
    float4 shadowCoord = TransformWorldToShadowCoord(positionWS);
#else
    float4 shadowCoord = float4(0, 0, 0, 0);
#endif
    Light mainLight = GetMainLight(shadowCoord, positionWS, half4(1, 1, 1, 1));
    half3 light = mainLight.color * mainLight.distanceAttenuation;

    half wrap = saturate(dot(n, mainLight.direction) * 0.5 + 0.5);
    half fill = (1.0 - mainLight.shadowAttenuation) * wrap * 0.12;
    half3 view = GetWorldSpaceNormalizeViewDir(positionWS);
    half back = pow(saturate(dot(view, -mainLight.direction)), 3.0) * translucency * lerp(0.35, 1.0, mainLight.shadowAttenuation);

    color.rgb += albedo * light * (fill + back * 0.6);
    color.rgb = MixFog(color.rgb, fogFactor);
    return color;
}

// Une normale de texture (espace tangent) vers le monde.
float3 UberTangentToWorld(half3 normalTS, float3 normalWS, float4 tangentWS)
{
    float sign = tangentWS.w * GetOddNegativeScale();
    float3 bitangent = sign * cross(normalWS, tangentWS.xyz);
    return TransformTangentToWorld(normalTS, half3x3(tangentWS.xyz, bitangent, normalWS));
}

#endif
