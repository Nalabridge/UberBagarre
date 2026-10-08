// Les cheveux et les barbes : des coques posées sur la tête (HairBuilder), qui doivent se lire
// comme des cheveux et pas comme un casque.
//
// - Mèches : des stries fines dans le sens de la pousse (la tangente du maillage), regroupées
//   en touffes un peu plus claires ou plus sombres.
// - Reflets : deux lobes de Kajiya-Kay le long des mèches — une ligne blanche nette, et un
//   second reflet teinté de la couleur des cheveux, décalé — ce qui fait « briller » une
//   chevelure sans la rendre plastique.
// - Bord : la densité (couleur.a) et la proximité du bord (couleur.r) découpent les mèches
//   une à une : un front effrangé, des côtés rasés qui laissent voir la peau (dégradé), une
//   barbe dont le contour se perd dans la joue.
Shader "UberBagarre/Cheveux"
{
    Properties
    {
        _Color ("Couleur", Color) = (0.045, 0.038, 0.034, 1)
        _Strands ("Mèches (tour de tête)", Float) = 420
        _Shine ("Reflets", Range(0, 2)) = 0.8
        _Variation ("Variation des mèches", Range(0, 1)) = 0.45
    }

    // --- URP
    SubShader
    {
        PackageRequirements
        {
            "com.unity.render-pipelines.universal": "14.0"
        }
        Tags { "Queue" = "AlphaTest" "RenderType" = "TransparentCutout" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }
        LOD 300

        HLSLINCLUDE
        #include "Assets/UberBagarre/Art/Shaders/UberUrp.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half4 _Color;
            float _Strands;
            half _Shine;
            half _Variation;
        CBUFFER_END

        // Posés par URP pendant le rendu des ombres.
        float3 _LightDirection;
        float3 _LightPosition;

        float HairHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float HairNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(HairHash(i), HairHash(i + float2(1, 0)), f.x),
                        lerp(HairHash(i + float2(0, 1)), HairHash(i + float2(1, 1)), f.x), f.y);
        }

        // strand : la teinte de la mèche (0..1) ; fine : un tirage par mèche, pour la découpe.
        void HairPattern(float2 uv, out float strand, out float fine)
        {
            float x = uv.x * _Strands;
            strand = HairNoise(float2(x, uv.y * 3.0)) * 0.6 + HairNoise(float2(x * 0.18, uv.y * 1.5 + 7.0)) * 0.4;
            fine = HairHash(float2(floor(x * 2.0), floor(uv.y * 90.0)));
        }

        // Densité et bord : on garde une mèche si son tirage passe sous la densité locale.
        void HairCut(float2 uv, half4 color)
        {
            float strand, fine;
            HairPattern(uv, strand, fine);
            float edge = color.r;
            float density = color.a * (1.0 - edge * edge * (0.45 + 0.55 * fine));
            clip(density - (0.08 + 0.8 * fine));
        }

        struct ClipAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        struct ClipVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 normalWS : TEXCOORD1;
            half4 color : COLOR;
            UNITY_VERTEX_INPUT_INSTANCE_ID
        };

        ClipVaryings ShadowVert(ClipAttributes input)
        {
            ClipVaryings output = (ClipVaryings)0;
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
            output.uv = input.uv;
            output.normalWS = normalWS;
            output.color = input.color;
            return output;
        }

        ClipVaryings DepthVert(ClipAttributes input)
        {
            ClipVaryings output = (ClipVaryings)0;
            UNITY_SETUP_INSTANCE_ID(input);
            UNITY_TRANSFER_INSTANCE_ID(input, output);
            output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
            output.uv = input.uv;
            output.normalWS = TransformObjectToWorldNormal(input.normalOS);
            output.color = input.color;
            return output;
        }

        half4 ShadowFrag(ClipVaryings input) : SV_Target
        {
            HairCut(input.uv, input.color);
            return 0;
        }

        half DepthFrag(ClipVaryings input) : SV_Target
        {
            HairCut(input.uv, input.color);
            return input.positionCS.z;
        }

        half4 DepthNormalsFrag(ClipVaryings input) : SV_Target
        {
            HairCut(input.uv, input.color);
            float3 normalWS = normalize(input.normalWS);
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
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _FORWARD_PLUS _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 tangentWS : TEXCOORD3;
                float4 shadowCoord : TEXCOORD4;
                half fogFactor : TEXCOORD5;
                half4 color : COLOR;
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
                output.tangentWS = normal.tangentWS;
                output.uv = input.uv;
                output.color = input.color;
                output.shadowCoord = GetShadowCoord(position);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                float strand, fine;
                HairPattern(input.uv, strand, fine);
                HairCut(input.uv, input.color);

                half3 albedo = _Color.rgb * lerp(1.0 - _Variation, 1.0 + _Variation * 0.6, strand);
                float3 n = normalize(input.normalWS);

                // Les cheveux absorbent : peu de reflet « surface », c'est Kajiya-Kay qui brille.
                half4 color = UberShadeUnfogged(input.positionWS, n, input.positionCS, input.shadowCoord,
                                                albedo, 0, 0.18, 1.0, half3(0, 0, 0), 1);

            #if defined(REQUIRES_VERTEX_SHADOW_COORD_INTERPOLATOR)
                float4 shadowCoord = input.shadowCoord;
            #elif defined(MAIN_LIGHT_CALCULATE_SHADOWS)
                float4 shadowCoord = TransformWorldToShadowCoord(input.positionWS);
            #else
                float4 shadowCoord = float4(0, 0, 0, 0);
            #endif
                Light light = GetMainLight(shadowCoord, input.positionWS, half4(1, 1, 1, 1));
                float3 v = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float3 h = normalize(light.direction + v);
                float3 t = normalize(input.tangentWS);
                float shift = (strand - 0.5) * 0.35;
                float3 t1 = normalize(t + n * (0.08 + shift));
                float3 t2 = normalize(t + n * (-0.12 + shift));
                float d1 = dot(t1, h);
                float d2 = dot(t2, h);
                float s1 = pow(saturate(sqrt(saturate(1.0 - d1 * d1))), 90.0);
                float s2 = pow(saturate(sqrt(saturate(1.0 - d2 * d2))), 22.0) * smoothstep(0.25, 0.9, strand);
                float wrap = saturate(dot(n, light.direction) * 0.6 + 0.4);
                half3 spec = s1 * 0.2 + s2 * 0.3 * saturate(albedo * 3.0 + 0.05);
                color.rgb += light.color * (light.shadowAttenuation * light.distanceAttenuation * wrap * _Shine) * spec;

                color.rgb = MixFog(color.rgb, input.fogFactor);
                return half4(color.rgb, 1);
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

        CGPROGRAM
        #pragma surface surf StandardSpecular fullforwardshadows addshadow vertex:vert
        #pragma target 3.0

        fixed4 _Color;
        float _Strands;
        half _Shine;
        half _Variation;

        struct Input
        {
            float2 hairUV;
            float4 color : COLOR;
        };

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);
            o.hairUV = v.texcoord.xy;
        }

        float HairHash(float2 p)
        {
            p = frac(p * float2(123.34, 456.21));
            p += dot(p, p + 45.32);
            return frac(p.x * p.y);
        }

        float HairNoise(float2 p)
        {
            float2 i = floor(p);
            float2 f = frac(p);
            f = f * f * (3.0 - 2.0 * f);
            return lerp(lerp(HairHash(i), HairHash(i + float2(1, 0)), f.x),
                        lerp(HairHash(i + float2(0, 1)), HairHash(i + float2(1, 1)), f.x), f.y);
        }

        void surf(Input IN, inout SurfaceOutputStandardSpecular o)
        {
            float x = IN.hairUV.x * _Strands;
            float strand = HairNoise(float2(x, IN.hairUV.y * 3.0)) * 0.6 + HairNoise(float2(x * 0.18, IN.hairUV.y * 1.5 + 7.0)) * 0.4;
            float fine = HairHash(float2(floor(x * 2.0), floor(IN.hairUV.y * 90.0)));
            float edge = IN.color.r;
            float density = IN.color.a * (1.0 - edge * edge * (0.45 + 0.55 * fine));
            clip(density - (0.08 + 0.8 * fine));

            o.Albedo = _Color.rgb * lerp(1.0 - _Variation, 1.0 + _Variation * 0.6, strand);
            o.Specular = 0.06 * _Shine;
            o.Smoothness = 0.45 * strand;
            o.Alpha = 1;
        }
        ENDCG
    }

    Fallback Off
}
