// Über Bagarre — le matériau « triplanaire » de Schedule 1 (murs, briques, tôles, falaises) :
// la texture est plaquée selon la position dans le monde, ces maillages n'ayant pas d'UV propres.
//
// Schedule 1 le fait avec un Shader Graph maison qui n'est pas dans l'export. Celui-ci en refait
// le calcul (le nœud « Triplanar » de Shader Graph) et lit ses propriétés SOUS LEURS NOMS
// D'ORIGINE : les matériaux de la carte sont ceux du jeu, tels quels, seul le shader est
// remplacé. _Tiling (répétitions par mètre), _BlendingEdges (netteté des raccords entre les
// trois projections), _Rotation (en degrés), _NormalStrength, _BaseColor, _Smoothness, _Metallic.
//
// Deux versions : URP (le rendu de Schedule 1, éclairée exactement comme « URP Lit ») et le
// rendu intégré, pour un projet où URP n'est pas encore installé.
Shader "UberBagarre/Carte/Triplanaire"
{
    Properties
    {
        _BaseColor ("Teinte", Color) = (1, 1, 1, 1)
        _DiffuseTexture ("Albedo", 2D) = "white" {}
        [Normal] _NormalTexture ("Normale", 2D) = "bump" {}
        _NormalStrength ("Force de la normale", Float) = 1
        _Tiling ("Repetitions par metre", Float) = 1
        _BlendingEdges ("Nettete des raccords", Float) = 1
        _Rotation ("Rotation (degres)", Float) = 0
        _Smoothness ("Lissage", Float) = 0
        _Metallic ("Metal", Range(0, 1)) = 0
        _Metallic_Map ("Carte de metal (inutilisee)", 2D) = "white" {}
    }

    // --- URP
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal": "14.0" }
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" "Queue" = "Geometry" }
        LOD 300

        HLSLINCLUDE
        #include "Assets/UberBagarre/Art/Shaders/UberUrp.hlsl"

        TEXTURE2D(_DiffuseTexture); SAMPLER(sampler_DiffuseTexture);
        TEXTURE2D(_NormalTexture); SAMPLER(sampler_NormalTexture);

        CBUFFER_START(UnityPerMaterial)
            float4 _DiffuseTexture_ST;
            float4 _NormalTexture_ST;
            float4 _Metallic_Map_ST;
            half4 _BaseColor;
            half _NormalStrength;
            float _Tiling;
            half _BlendingEdges;
            float _Rotation;
            half _Smoothness;
            half _Metallic;
        CBUFFER_END

        #include "Assets/UberBagarre/Art/Shaders/UberUrpPasses.hlsl"
        ENDHLSL

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
                float3 n = normalize(input.normalWS);

                // Le nœud Triplanar : poids = |normale| ^ netteté, normalisés.
                float3 w = pow(abs(n), max(_BlendingEdges, 0.01));
                w /= max(w.x + w.y + w.z, 1e-4);

                // Les trois projections, tournées de _Rotation degrés dans leur plan.
                float s, c;
                sincos(radians(_Rotation), s, c);
                float2x2 turn = float2x2(c, -s, s, c);
                float3 p = input.positionWS * _Tiling;
                float2 ux = mul(turn, p.zy);
                float2 uy = mul(turn, p.xz);
                float2 uz = mul(turn, p.xy);

                half4 albedo = SAMPLE_TEXTURE2D(_DiffuseTexture, sampler_DiffuseTexture, ux) * w.x
                             + SAMPLE_TEXTURE2D(_DiffuseTexture, sampler_DiffuseTexture, uy) * w.y
                             + SAMPLE_TEXTURE2D(_DiffuseTexture, sampler_DiffuseTexture, uz) * w.z;
                albedo *= _BaseColor;

                // Normales : mélange « whiteout » des trois projections, en espace monde (comme le
                // nœud Triplanar en mode normale). Le relief est ramené du repère tourné au plan.
                float2x2 back = float2x2(c, s, -s, c);
                half3 tx = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, ux), _NormalStrength);
                half3 ty = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, uy), _NormalStrength);
                half3 tz = UnpackNormalScale(SAMPLE_TEXTURE2D(_NormalTexture, sampler_NormalTexture, uz), _NormalStrength);
                tx.xy = mul(back, tx.xy);
                ty.xy = mul(back, ty.xy);
                tz.xy = mul(back, tz.xy);
                tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
                ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
                tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
                float3 normalWS = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);

                return UberShade(input.positionWS, normalWS, input.positionCS, input.shadowCoord, input.fogFactor,
                                 albedo.rgb, _Metallic, saturate(_Smoothness), 1.0, half3(0, 0, 0));
            }
            ENDHLSL
        }

        Pass
        {
            Name "ShadowCaster"
            Tags { "LightMode" = "ShadowCaster" }
            ZWrite On
            ZTest LEqual
            ColorMask 0

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UberShadowVert
            #pragma fragment UberShadowFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_vertex _ _CASTING_PUNCTUAL_LIGHT_SHADOW
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UberDepthVert
            #pragma fragment UberDepthFrag
            #pragma multi_compile_instancing
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex UberDepthVert
            #pragma fragment UberDepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }

    // --- Rendu intégré (Built-in)
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0

        sampler2D _DiffuseTexture;
        sampler2D _NormalTexture;
        fixed4 _BaseColor;
        half _NormalStrength;
        float _Tiling;
        half _BlendingEdges;
        float _Rotation;
        half _Smoothness;
        half _Metallic;

        struct Input
        {
            float3 worldPos;
            float3 worldNormal;
            INTERNAL_DATA
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float3 n = normalize(WorldNormalVector(IN, float3(0, 0, 1)));
            float3 w = pow(abs(n), max(_BlendingEdges, 0.01));
            w /= max(w.x + w.y + w.z, 1e-4);

            float s, c;
            sincos(radians(_Rotation), s, c);
            float2x2 turn = float2x2(c, -s, s, c);
            float3 p = IN.worldPos * _Tiling;
            float2 ux = mul(turn, p.zy);
            float2 uy = mul(turn, p.xz);
            float2 uz = mul(turn, p.xy);

            fixed4 col = tex2D(_DiffuseTexture, ux) * w.x + tex2D(_DiffuseTexture, uy) * w.y + tex2D(_DiffuseTexture, uz) * w.z;
            col *= _BaseColor;
            o.Albedo = col.rgb;
            o.Alpha = 1;
            o.Smoothness = saturate(_Smoothness);
            o.Metallic = _Metallic;

            float2x2 back = float2x2(c, s, -s, c);
            half3 tx = UnpackScaleNormal(tex2D(_NormalTexture, ux), _NormalStrength);
            half3 ty = UnpackScaleNormal(tex2D(_NormalTexture, uy), _NormalStrength);
            half3 tz = UnpackScaleNormal(tex2D(_NormalTexture, uz), _NormalStrength);
            tx.xy = mul(back, tx.xy);
            ty.xy = mul(back, ty.xy);
            tz.xy = mul(back, tz.xy);
            tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
            ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
            tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
            float3 wn = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);

            // Retour en espace tangent pour le modèle d'éclairage.
            float3 t = WorldNormalVector(IN, float3(1, 0, 0));
            float3 b = WorldNormalVector(IN, float3(0, 1, 0));
            float3 local = float3(dot(wn, t), dot(wn, b), dot(wn, n));
            o.Normal = dot(local, local) > 1e-6 ? normalize(local) : float3(0, 0, 1);
        }
        ENDCG
    }

    FallBack "Diffuse"
}
