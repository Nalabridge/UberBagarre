// Über Bagarre — l'eau de la carte (port, étangs, mer).
//
// Sous URP : une eau qui vit.
// - trois houles de normales croisées (grande houle, vagues, frisottis), plus calmes au loin ;
// - la couleur dépend de la profondeur réelle (lue dans la profondeur de la scène) : turquoise et
//   transparente au bord, bleu profond et opaque au large ;
// - l'écume roule le long des berges et coiffe les crêtes ;
// - le reflet est celui du VRAI ciel du jeu (les mêmes nuages, le même couchant), avec le reflet
//   du soleil qui scintille sur les vagues ;
// - un léger mouvement de houle si le maillage a assez de sommets.
// En rendu intégré : l'ancienne eau (deux houles, reflet, plus opaque en rasant).
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

        [Header(URP)]
        _DeepColor ("Eau profonde", Color) = (0.012, 0.075, 0.13, 1)
        _ShallowColor ("Eau peu profonde", Color) = (0.07, 0.36, 0.38, 1)
        _DepthFalloff ("Profondeur de la transition", Range(0.02, 2)) = 0.28
        _DeepAlpha ("Opacite au large", Range(0, 1)) = 0.94
        _FoamDistance ("Ecume le long des berges (m)", Range(0, 4)) = 1.1
        _Whitecaps ("Ecume sur les cretes", Range(0, 1)) = 0.35
        _SunSparkle ("Scintillement du soleil", Range(0, 20)) = 7
        _WaveHeight ("Houle (m)", Range(0, 1)) = 0.12
    }

    // --- URP
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
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
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Assets/UberBagarre/Art/Shaders/UberSky.hlsl"

            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

            CBUFFER_START(UnityPerMaterial)
                float4 _BumpMap_ST;
                half4 _Color;
                half4 _HorizonColor;
                half _BumpScale;
                float _Tiling;
                float _Speed;
                half _Glossiness;
                half4 _DeepColor;
                half4 _ShallowColor;
                half _DepthFalloff;
                half _DeepAlpha;
                half _FoamDistance;
                half _Whitecaps;
                half _SunSparkle;
                half _WaveHeight;
            CBUFFER_END

            // Posés par TimeOfDay : le ciel que l'eau reflète.
            float4 _UberSunDir;
            float _UberNight;
            float _UberOvercast;

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
                float4 shadowCoord : TEXCOORD1;
                half fogFactor : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                float3 positionWS = TransformObjectToWorld(input.positionOS.xyz);
                float t = _Time.y * _Speed;
                positionWS.y += (sin(positionWS.x * 0.11 + t * 0.9) * 0.6 + sin(positionWS.z * 0.17 - t * 1.3) * 0.4) * _WaveHeight;

                output.positionWS = positionWS;
                output.positionCS = TransformWorldToHClip(positionWS);
                output.shadowCoord = TransformWorldToShadowCoord(positionWS);
                output.fogFactor = ComputeFogFactor(output.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float3 positionWS = input.positionWS;
                float3 view = GetWorldSpaceNormalizeViewDir(positionWS);
                float distance = length(positionWS - _WorldSpaceCameraPos);
                float t = _Time.y * _Speed;

                // Les vagues : trois houles croisées, plus calmes au loin (sinon elles grésillent).
                float2 p = positionWS.xz * _Tiling;
                half3 a = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, p * 0.35 + float2(t * 0.010, t * 0.004)), _BumpScale * 1.2);
                half3 b = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, p + float2(-t * 0.016, t * 0.012)), _BumpScale);
                half3 c = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, p * 3.1 + float2(t * 0.03, -t * 0.025)), _BumpScale * 0.5);
                half3 waves = normalize(half3(a.xy + b.xy + c.xy, a.z * b.z * c.z));
                waves.xy *= lerp(1.0, 0.3, saturate(distance / 260.0));
                float3 normalWS = normalize(float3(waves.x, max(waves.z, 0.05), waves.y));

                // La profondeur d'eau sous ce point, lue dans la profondeur de la scène.
                float2 screenUV = GetNormalizedScreenSpaceUV(input.positionCS);
                float sceneDepth = LinearEyeDepth(SampleSceneDepth(screenUV), _ZBufferParams);
                float waterDepth = -TransformWorldToView(positionWS).z;
                float thickness = max(sceneDepth - waterDepth, 0.0);
                float shallow = exp(-thickness * _DepthFalloff);

                half3 deep = lerp(_DeepColor.rgb, _Color.rgb * 1.6, 0.25);
                half3 body = lerp(deep, _ShallowColor.rgb, shallow);
                half bodyAlpha = lerp(_DeepAlpha, 0.22, shallow);

                Light mainLight = GetMainLight(input.shadowCoord, positionWS, half4(1, 1, 1, 1));
                half3 ambient = SampleSH(float3(0, 1, 0));
                half sunUp = saturate(mainLight.direction.y);
                half3 sunLight = mainLight.color * mainLight.distanceAttenuation * mainLight.shadowAttenuation;
                body *= ambient * 0.9 + sunLight * sunUp * 0.35;

                // Le reflet : le ciel du jeu, jamais sous l'horizon.
                float3 sunDir = dot(_UberSunDir.xyz, _UberSunDir.xyz) > 0.001 ? normalize(_UberSunDir.xyz) : mainLight.direction;
                float3 reflected = reflect(-view, normalWS);
                reflected.y = abs(reflected.y);
                half3 sky = UberSkyReflection(reflected, sunDir, saturate(_UberNight), saturate(_UberOvercast), _Time.y);

                half fresnel = 0.02 + 0.98 * pow(1.0 - saturate(dot(normalWS, view)), 5.0);
                half spec = pow(saturate(dot(reflected, mainLight.direction)), 900.0) * _SunSparkle
                          + pow(saturate(dot(reflected, mainLight.direction)), 90.0) * 0.25;
                half3 specular = sunLight * spec;

                half3 rgb = body * bodyAlpha * (1.0 - fresnel) + sky * fresnel + specular;
                half alpha = saturate(bodyAlpha * (1.0 - fresnel) + fresnel);

                // L'écume : le long des berges (là où l'eau est mince) et sur les crêtes.
                float shore = 1.0 - smoothstep(0.0, max(_FoamDistance, 0.01), thickness);
                float pattern = UberSkyFbm(positionWS.xz * 1.6 + float2(t * 0.3, t * 0.17), 3);
                float foam = shore * smoothstep(0.35, 0.7, pattern + shore * 0.35);
                foam += smoothstep(0.55, 0.9, length(waves.xy)) * _Whitecaps * (1.0 - saturate(distance / 180.0));
                foam = saturate(foam);
                half3 foamColor = (ambient + sunLight * sunUp * 0.8) * 0.9;
                rgb = lerp(rgb, foamColor, foam);
                alpha = lerp(alpha, 1.0, foam);

                rgb = MixFogColor(rgb, unity_FogColor.rgb * alpha, input.fogFactor);
                return half4(rgb, alpha);
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
