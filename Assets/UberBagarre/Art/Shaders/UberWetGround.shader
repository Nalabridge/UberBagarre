// Bitume mouille avec reflet planaire.
//
// C'est la piece maitresse de l'ambiance nocturne. Une lampe et une enseigne
// eclairent des surfaces ; un sol mouille les REFLETE, et un reflet est la seule
// chose qui relie visuellement le haut et le bas d'une image. Sans lui, une rue
// de nuit est une bande noire surmontee de lumieres, ce qui se lit comme un
// decor de theatre.
//
// Le reflet n'est pas une approximation par sonde d'environnement : une seconde
// camera rend reellement la scene, miroir par rapport au plan du sol, dans une
// texture (voir PlanarReflection.cs). Les neons, les lampadaires, les voitures
// et les combattants y sont, en mouvement, exacts. C'est ce qu'on attend d'un
// rendu "comme du ray tracing" sur une chaussee humide, pour le prix d'un
// deuxieme rendu de scene.
//
// Le masque de flaques decide ou la chaussee est mouillee : une rue uniformement
// miroir ressemble a une patinoire. Les flaques donnent aussi de la lecture au
// sol, donc un repere de distance pendant un combat.
Shader "UberBagarre/WetGround"
{
    Properties
    {
        _Color ("Teinte", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Relief", 2D) = "bump" {}
        _BumpScale ("Force du relief", Range(0, 2)) = 1.0
        _Glossiness ("Rugosite inverse (sec)", Range(0, 1)) = 0.12
        _Metallic ("Metallique", Range(0, 1)) = 0.0

        _WetMask ("Masque de flaques (R)", 2D) = "black" {}
        _WetLevel ("Niveau d'humidite", Range(0, 1)) = 0.75
        _WetDarken ("Assombrissement mouille", Range(0, 1)) = 0.55

        _ReflectionTex ("Reflet planaire", 2D) = "black" {}
        _ReflectionStrength ("Force du reflet", Range(0, 2)) = 1.0
        _ReflectionTint ("Teinte du reflet", Color) = (1, 1, 1, 1)
        _FresnelPower ("Nettete de Fresnel", Range(0.5, 8)) = 3.2
        _FresnelBase ("Reflet a la verticale", Range(0, 1)) = 0.10

        _RippleStrength ("Force des ondulations", Range(0, 0.05)) = 0.010
        _RippleScale ("Echelle des ondulations", Float) = 1.6
        _RippleSpeed ("Vitesse des ondulations", Float) = 0.35
        _RippleFadeDistance ("Distance de fondu des ondulations", Float) = 28
    }

    // --- URP (le rendu de Schedule 1) : bitume et flaques. Plus de miroir planaire (URP ne le
    // déclenche pas) : les flaques reflètent le ciel et les sondes, comme la chaussée de Schedule 1.
    SubShader
    {
        // Ignoré tant qu'URP n'est pas installé : le shader reste valide en rendu intégré.
        PackageRequirements { "com.unity.render-pipelines.universal": "14.0" }
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing
            #include "Assets/UberBagarre/Art/Shaders/UberUrp.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);
            TEXTURE2D(_WetMask); SAMPLER(sampler_WetMask);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _WetMask_ST;
                half4 _Color;
                half _BumpScale;
                half _Glossiness;
                half _Metallic;
                half _WetLevel;
                half _WetDarken;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float4 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                VertexPositionInputs position = GetVertexPositionInputs(input.positionOS.xyz);
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.tangentWS = float4(normal.tangentWS, input.tangentOS.w);
                output.uv = float4(TRANSFORM_TEX(input.uv, _MainTex), TRANSFORM_TEX(input.uv, _WetMask));
                output.shadowCoord = GetShadowCoord(position);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv.xy) * _Color;
                half puddle = SAMPLE_TEXTURE2D(_WetMask, sampler_WetMask, input.uv.zw).r * _WetLevel;

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv.xy), _BumpScale * (1.0 - puddle * 0.85));
                float3 normalWS = UberTangentToWorld(normalTS, normalize(input.normalWS), input.tangentWS);

                // L'eau assombrit et lisse : mais pas en miroir parfait (0,82), comme une vraie flaque.
                half3 color = albedo.rgb * lerp(1.0, 1.0 - _WetDarken, puddle);
                half smoothness = lerp(_Glossiness, 0.82, puddle);
                return UberShade(input.positionWS, normalWS, input.positionCS, input.shadowCoord, input.fogFactor,
                                 color, _Metallic, smoothness, 1.0, half3(0, 0, 0));
            }
            ENDHLSL
        }

        // Ombres portées, profondeur et normales (pour l'occlusion ambiante) : celles d'URP Lit.
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    // --- Rendu intégré (Built-in) : la version d'origine, avec le reflet planaire.
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows
        #pragma target 3.0

        sampler2D _MainTex;
        sampler2D _BumpMap;
        half _BumpScale;
        sampler2D _WetMask;
        sampler2D _ReflectionTex;

        fixed4 _Color;
        half _Glossiness;
        half _Metallic;

        half _WetLevel;
        half _WetDarken;

        half _ReflectionStrength;
        fixed4 _ReflectionTint;
        half _FresnelPower;
        half _FresnelBase;

        half _RippleStrength;
        half _RippleScale;
        half _RippleSpeed;
        float _RippleFadeDistance;

        struct Input
        {
            float2 uv_MainTex;
            float2 uv_WetMask;
            float4 screenPos;
            float3 worldPos;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;

            // Masque de flaques. Le canal rouge suffit : ce n'est pas une donnee
            // colorimetrique, juste "mouille / sec".
            half puddle = tex2D(_WetMask, IN.uv_WetMask).r * _WetLevel;

            // De l'eau sur une surface rugueuse remplit ses asperites : elle
            // devient plus sombre ET plus lisse. Les deux ensemble, sinon ca ne
            // ressemble pas a de l'eau mais a du vernis.
            o.Albedo = albedo.rgb * lerp(1.0, 1.0 - _WetDarken, puddle);
            o.Metallic = _Metallic;
            o.Smoothness = lerp(_Glossiness, 0.94, puddle);

            // Relief du bitume : les gravillons accrochent la lumiere des lampadaires. Dans
            // une flaque, l'eau remplit les creux et la surface redevient un miroir plat.
            float3 bump = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            bump.xy *= _BumpScale * (1.0 - puddle * 0.85);
            o.Normal = normalize(bump);

            float cameraDistance = length(_WorldSpaceCameraPos - IN.worldPos);

            // Ondulations : deux trains d'ondes de periodes differentes. Un seul
            // sinus produit un moire regulier immediatement identifiable.
            float2 rippleInput = IN.worldPos.xz * _RippleScale;
            float t = _Time.y * _RippleSpeed;

            float2 ripple;
            ripple.x = sin(rippleInput.x * 1.9 + t * 1.7) + sin(rippleInput.y * 2.7 - t * 1.1);
            ripple.y = cos(rippleInput.y * 2.1 - t * 1.3) + cos(rippleInput.x * 3.1 + t * 0.9);
            ripple *= 0.5 * _RippleStrength * puddle;

            // Au loin, une ondulation devient plus fine qu'un pixel : elle ne se voit plus
            // comme de l'eau mais comme un grouillement. On l'eteint progressivement.
            ripple *= saturate(1.0 - cameraDistance / max(_RippleFadeDistance, 1.0));

            float2 screenUv = IN.screenPos.xy / max(IN.screenPos.w, 1e-5);
            screenUv += ripple;
            screenUv = saturate(screenUv);

            fixed3 reflection = tex2D(_ReflectionTex, screenUv).rgb * _ReflectionTint.rgb;

            // Fresnel : de face, une flaque est presque transparente ; en regardant
            // au loin, elle devient un miroir. Sans ce terme, le sol reflete autant
            // sous les pieds qu'a l'horizon, ce qui se voit tout de suite comme faux.
            float3 viewDir = normalize(_WorldSpaceCameraPos - IN.worldPos);
            float fresnel = pow(1.0 - saturate(viewDir.y), _FresnelPower);
            fresnel = saturate(_FresnelBase + fresnel * (1.0 - _FresnelBase));

            // Le reflet passe par l'emission : il ne doit pas etre re-eclaire par
            // les lampes de la scene, il porte deja sa propre lumiere.
            o.Emission = reflection * fresnel * puddle * _ReflectionStrength;
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
