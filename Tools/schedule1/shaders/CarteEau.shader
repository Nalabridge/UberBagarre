// Über Bagarre — l'eau de la carte (port, étangs, mer) : deux houles de normales qui glissent
// l'une sur l'autre, reflet du ciel, plus opaque en rasant.
Shader "UberBagarre/Carte/Eau"
{
    Properties
    {
        _Color ("Eau vue de haut", Color) = (0.03, 0.08, 0.09, 0.78)
        _HorizonColor ("Eau en rasant", Color) = (0.05, 0.10, 0.13, 1)
        [Normal] _BumpMap ("Vagues", 2D) = "bump" {}
        _BumpScale ("Force des vagues", Float) = 0.6
        _Tiling ("Repetitions par metre", Float) = 0.08
        _Speed ("Vitesse", Float) = 0.6
        _Glossiness ("Lissage", Range(0, 1)) = 0.93
    }

    // --- URP
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal": "14.0" }
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }
            Blend One OneMinusSrcAlpha
            ZWrite Off

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            // Mélange prémultiplié : l'eau laisse voir le fond, mais ses reflets restent entiers.
            #define _SURFACE_TYPE_TRANSPARENT 1
            #define _ALPHAPREMULTIPLY_ON 1
            #include "Assets/UberBagarre/Art/Shaders/UberUrp.hlsl"

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BumpMap_ST;
                half4 _Color;
                half4 _HorizonColor;
                half _BumpScale;
                float _Tiling;
                float _Speed;
                half _Glossiness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float4 shadowCoord : TEXCOORD2;
                half fogFactor : TEXCOORD3;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.shadowCoord = GetShadowCoord(position);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float2 p = input.positionWS.xz * _Tiling;
                float t = _Time.x * _Speed;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, p + float2(t, t * 0.37)), _BumpScale);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, p * 1.73 + float2(-t * 0.41, t)), _BumpScale);
                half3 waves = normalize(half3(a.xy + b.xy, a.z * b.z));

                // Une surface d'eau à plat : l'espace tangent des vagues est le plan XZ du monde.
                float3 normalWS = normalize(float3(waves.x, waves.z, waves.y));

                float3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half fresnel = pow(1 - saturate(dot(view, normalWS)), 4);
                half3 albedo = lerp(_Color.rgb, _HorizonColor.rgb, fresnel);
                half alpha = lerp(_Color.a, 1, fresnel);
                return UberShadeAlpha(input.positionWS, normalWS, input.positionCS, input.shadowCoord, input.fogFactor,
                                      albedo, 0, _Glossiness, 1.0, half3(0, 0, 0), alpha);
            }
            ENDHLSL
        }
    }

    // --- Rendu intégré (Built-in)
    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:premul
        #pragma target 3.0

        sampler2D _BumpMap;
        fixed4 _Color;
        fixed4 _HorizonColor;
        half _BumpScale;
        float _Tiling;
        float _Speed;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz * _Tiling;
            float t = _Time.x * _Speed;
            half3 a = UnpackScaleNormal(tex2D(_BumpMap, p + float2(t, t * 0.37)), _BumpScale);
            half3 b = UnpackScaleNormal(tex2D(_BumpMap, p * 1.73 + float2(-t * 0.41, t)), _BumpScale);
            o.Normal = normalize(half3(a.xy + b.xy, a.z * b.z));

            half fresnel = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), 4);
            o.Albedo = lerp(_Color.rgb, _HorizonColor.rgb, fresnel);
            o.Alpha = lerp(_Color.a, 1, fresnel);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Diffuse"
}
