// Über Bagarre — les passes d'appoint d'une surface opaque sous URP : ombre portée, profondeur,
// normales (celles que lit l'occlusion ambiante). Mêmes calculs que celles d'URP Lit, mais
// écrites ici pour partager le bloc de matériau (UnityPerMaterial) du shader qui les inclut :
// le « SRP Batcher » d'URP regroupe alors ses rendus, ce qui compte sur une ville entière.
//
// À inclure après UberUrp.hlsl et après le CBUFFER du matériau (bloc HLSLINCLUDE du SubShader).
#ifndef UBER_URP_PASSES_INCLUDED
#define UBER_URP_PASSES_INCLUDED

// Posés par URP pendant le rendu des ombres.
float3 _LightDirection;
float3 _LightPosition;

struct UberDepthAttributes
{
    float4 positionOS : POSITION;
    float3 normalOS : NORMAL;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct UberDepthVaryings
{
    float4 positionCS : SV_POSITION;
    float3 normalWS : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

UberDepthVaryings UberShadowVert(UberDepthAttributes input)
{
    UberDepthVaryings output = (UberDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);

    float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
    float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
#if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
    float3 lightDirectionWS = normalize(_LightPosition - positionWS);
#else
    float3 lightDirectionWS = _LightDirection;
#endif

    output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
    output.normalWS = normalWS;
    return output;
}

half4 UberShadowFrag(UberDepthVaryings input) : SV_Target
{
    return 0;
}

UberDepthVaryings UberDepthVert(UberDepthAttributes input)
{
    UberDepthVaryings output = (UberDepthVaryings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_TRANSFER_INSTANCE_ID(input, output);
    output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
    output.normalWS = TransformObjectToWorldNormal(input.normalOS);
    return output;
}

half UberDepthFrag(UberDepthVaryings input) : SV_Target
{
    return input.positionCS.z;
}

half4 UberDepthNormalsFrag(UberDepthVaryings input) : SV_Target
{
    float3 normalWS = normalize(input.normalWS);
#if defined(_GBUFFER_NORMALS_OCT)
    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
    float2 remapped = saturate(octNormalWS * 0.5 + 0.5);
    return half4(PackFloat2To888(remapped), 0.0);
#else
    return half4(normalWS, 0.0);
#endif
}

#endif
