// Über Bagarre — feuillages, grillages, rubans, sacs : visibles des deux côtés, découpés à l'alpha.
// _Wind fait onduler doucement les herbes et les branches.
Shader "UberBagarre/Carte/DoubleFace"
{
    Properties
    {
        _Color ("Teinte", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normale", 2D) = "bump" {}
        _BumpScale ("Force de la normale", Float) = 1
        _Cutoff ("Seuil alpha", Range(0, 1)) = 0.5
        _Glossiness ("Lissage", Range(0, 1)) = 0.1
        _Metallic ("Metal", Range(0, 1)) = 0
        _Wind ("Vent", Range(0, 1)) = 0
    }

    // --- URP : même découpe, même vent ; les ombres portées suivent le vent et la découpe.
    SubShader
    {
        PackageRequirements { "com.unity.render-pipelines.universal": "14.0" }
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 300
        Cull Off

        HLSLINCLUDE
        #include "Assets/UberBagarre/Art/Shaders/UberUrp.hlsl"

        TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
        TEXTURE2D(_BumpMap); SAMPLER(sampler_BumpMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _MainTex_ST;
            float4 _BumpMap_ST;
            half4 _Color;
            half _BumpScale;
            half _Cutoff;
            half _Glossiness;
            half _Metallic;
            half _Wind;
        CBUFFER_END

        // Posés par URP pendant le rendu des ombres.
        float3 _LightDirection;
        float3 _LightPosition;

        float3 Sway(float3 positionOS)
        {
            if (_Wind > 0.001)
            {
                float3 wp = TransformObjectToWorld(positionOS);
                float t = _Time.y;
                float sway = sin(t * 1.6 + wp.x * 0.31 + wp.z * 0.23) * 0.6 + sin(t * 3.1 + wp.x * 0.9 - wp.z * 0.7) * 0.25;
                float height = saturate(positionOS.y * 0.8);
                positionOS.xz += sway * _Wind * 0.06 * height;
            }

            return positionOS;
        }

        struct ClipAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct ClipVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        ClipVaryings ShadowVert(ClipAttributes input)
        {
            ClipVaryings output = (ClipVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            float3 positionWS = TransformObjectToWorld(Sway(input.positionOS.xyz));
            float3 normalWS = TransformObjectToWorldNormal(input.normalOS);
        #if defined(_CASTING_PUNCTUAL_LIGHT_SHADOW)
            float3 lightDirectionWS = normalize(_LightPosition - positionWS);
        #else
            float3 lightDirectionWS = _LightDirection;
        #endif
            output.positionCS = ApplyShadowClamping(TransformWorldToHClip(ApplyShadowBias(positionWS, normalWS, lightDirectionWS)));
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.normalWS = normalWS;
            return output;
        }

        ClipVaryings DepthVert(ClipAttributes input)
        {
            ClipVaryings output = (ClipVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.positionCS = TransformObjectToHClip(Sway(input.positionOS.xyz));
            output.uv = TRANSFORM_TEX(input.uv, _MainTex);
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            return output;
        }

        void Cut(float2 uv)
        {
            clip(SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).a * _Color.a - _Cutoff);
        }

        half4 ShadowFrag(ClipVaryings input) : SV_Target
        {
            Cut(input.uv);
            return 0;
        }

        half DepthFrag(ClipVaryings input) : SV_Target
        {
            Cut(input.uv);
            return input.positionCS.z;
        }

        half4 DepthNormalsFrag(ClipVaryings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
        {
            Cut(input.uv);
            float3 normalWS = normalize(input.normalWS) * (IS_FRONT_VFACE(face, true, false) ? 1.0 : -1.0);
        #if defined(_GBUFFER_NORMALS_OCT)
            float2 remapped = saturate(PackNormalOctQuadEncode(normalWS) * 0.5 + 0.5);
            return half4(PackFloat2To888(remapped), 0.0);
        #else
            return half4(normalWS, 0.0);
        #endif
        }
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma shader_feature_local _NORMALMAP
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
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
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
                VertexPositionInputs position = GetVertexPositionInputs(Sway(input.positionOS.xyz));
                VertexNormalInputs normal = GetVertexNormalInputs(input.normalOS, input.tangentOS);
                output.positionCS = position.positionCS;
                output.positionWS = position.positionWS;
                output.normalWS = normal.normalWS;
                output.tangentWS = float4(normal.tangentWS, input.tangentOS.w);
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.shadowCoord = GetShadowCoord(position);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input, FRONT_FACE_TYPE face : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                clip(albedo.a - _Cutoff);

                // La face arrière éclairée comme la face avant.
                float side = IS_FRONT_VFACE(face, 1.0, -1.0);
                float3 normalWS = normalize(input.normalWS) * side;
            #if defined(_NORMALMAP)
                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                normalWS = UberTangentToWorld(normalTS, normalWS, input.tangentWS);
            #endif

                return UberShade(input.positionWS, normalWS, input.positionCS, input.shadowCoord, input.fogFactor,
                                 albedo.rgb, _Metallic, _Glossiness, 1.0, half3(0, 0, 0));
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
            #pragma vertex ShadowVert
            #pragma fragment ShadowFrag
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
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
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
            #pragma vertex DepthVert
            #pragma fragment DepthNormalsFrag
            #pragma multi_compile_instancing
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT
            ENDHLSL
        }
    }

    // --- Rendu intégré (Built-in)
    SubShader
    {
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "IgnoreProjector" = "True" }
        LOD 300
        Cull Off

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows alphatest:_Cutoff addshadow vertex:vert
        #pragma target 3.0
        #pragma shader_feature_local _NORMALMAP

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _BumpScale;
        half _Glossiness;
        half _Metallic;
        half _Wind;

        struct Input
        {
            float2 uv_MainTex;
            fixed facing : VFACE;
        };

        void vert(inout appdata_full v)
        {
            if (_Wind > 0.001)
            {
                float3 wp = mul(unity_ObjectToWorld, v.vertex).xyz;
                float t = _Time.y;
                float sway = sin(t * 1.6 + wp.x * 0.31 + wp.z * 0.23) * 0.6 + sin(t * 3.1 + wp.x * 0.9 - wp.z * 0.7) * 0.25;
                float height = saturate(v.vertex.y * 0.8);
                v.vertex.xz += sway * _Wind * 0.06 * height;
            }
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            o.Albedo = c.rgb;
            o.Alpha = c.a;
            o.Smoothness = _Glossiness;
            o.Metallic = _Metallic;

            half3 n = half3(0, 0, 1);
            #ifdef _NORMALMAP
            n = UnpackScaleNormal(tex2D(_BumpMap, IN.uv_MainTex), _BumpScale);
            #endif
            // La face arrière éclairée comme la face avant.
            n.z *= IN.facing > 0 ? 1 : -1;
            o.Normal = n;
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Cutout/VertexLit"
}
