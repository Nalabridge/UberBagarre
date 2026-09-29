// Peau (et vetements) qui gardent la trace des coups.
//
// Les bleus etaient des ovales en relief colles sur le corps : de loin, on voyait une
// pastille posee sur le personnage, pas une marque dans la peau. Ici, rien n'est ajoute
// a la scene. Chaque morceau de corps recoit jusqu'a huit points d'impact (position
// dans l'espace de l'objet, rayon, intensite) et le shader assombrit la surface autour,
// avec un contour irregulier et une marbrure : l'hematome est dans la texture elle-meme.
//
// Les positions sont dans l'espace de l'objet, donc la marque suit le membre quand il
// bouge ; les distances sont mesurees apres application de l'echelle de l'objet, donc un
// bleu reste rond sur un torse etire en largeur.
//
// La meme matiere sert aux vetements, avec une autre teinte : un coup au ventre laisse
// une trace sombre sur le tee-shirt (poussiere, sueur), pas un bleu violet sur le tissu.
Shader "UberBagarre/Peau"
{
    Properties
    {
        _Color ("Teinte", Color) = (1, 1, 1, 1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Relief", 2D) = "bump" {}
        _BumpScale ("Force du relief", Range(0, 2)) = 1.0
        _Glossiness ("Lissage", Range(0, 1)) = 0.16
        _Metallic ("Metallique", Range(0, 1)) = 0.0

        _MarkTint ("Teinte de la marque (halo)", Color) = (0.62, 0.36, 0.50, 1)
        _MarkCore ("Teinte de la marque (coeur)", Color) = (0.55, 0.22, 0.30, 1)
        _MarkSwelling ("Gonflement (brillance au coeur)", Range(0, 0.5)) = 0.12
        _MarkNoise ("Irregularite du contour", Range(0, 1)) = 0.55
        [Toggle] _MarkSpace ("Marques en position de repos (corps skinne)", Float) = 0
    }

    // --- URP (le rendu de Schedule 1) : la même peau, éclairée par URP.
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

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                half4 _Color;
                half _BumpScale;
                half _Glossiness;
                half _Metallic;
                half4 _MarkTint;
                half4 _MarkCore;
                half _MarkSwelling;
                half _MarkNoise;
                float _MarkSpace;
            CBUFFER_END

            // Remplis par BruiseSystem (bloc de propriétés propre à chaque rendu).
            float4 _Marks[8];
            float4 _MarkInfo[8];
            float _MarkCount;

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float4 tangentOS : TANGENT;
                float2 uv : TEXCOORD0;
                float3 rest : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float3 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float4 tangentWS : TEXCOORD3;
                float3 markPosition : TEXCOORD4;
                float4 shadowCoord : TEXCOORD5;
                half fogFactor : TEXCOORD6;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            float3 ObjectScale()
            {
                float4x4 m = GetObjectToWorldMatrix();
                return float3(length(m._m00_m10_m20), length(m._m01_m11_m21), length(m._m02_m12_m22));
            }

            float Hash(float3 p)
            {
                p = frac(p * 0.3183099 + 0.1);
                p *= 17.0;
                return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
            }

            float Noise(float3 x)
            {
                float3 i = floor(x);
                float3 f = frac(x);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                                 lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                            lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                                 lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
            }

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
                output.uv = TRANSFORM_TEX(input.uv, _MainTex);
                output.markPosition = _MarkSpace > 0.5 ? input.rest : input.positionOS.xyz * ObjectScale();
                output.shadowCoord = GetShadowCoord(position);
                output.fogFactor = ComputeFogFactor(position.positionCS.z);
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, input.uv) * _Color;
                float3 scale = ObjectScale();

                float halo = 0.0;
                float core = 0.0;
                int count = (int)_MarkCount;

                [loop]
                for (int i = 0; i < 8; i++)
                {
                    if (i >= count) break;
                    float4 mark = _Marks[i];
                    float4 info = _MarkInfo[i];
                    float3 delta = input.markPosition - mark.xyz * scale;
                    float seed = info.y * 7.31;
                    float wobble = (Noise(input.markPosition * 32.0 + seed) - 0.5) * _MarkNoise +
                                   (Noise(input.markPosition * 85.0 + seed * 1.7) - 0.5) * _MarkNoise * 0.35;
                    float dist = length(delta) / max(mark.w, 0.005) + wobble;
                    float h = saturate(1.0 - dist);
                    h = h * h * (3.0 - 2.0 * h);
                    float c = saturate(1.0 - dist * 1.9);
                    halo = max(halo, h * info.x);
                    core = max(core, c * c * info.x);
                }

                float mottle = Noise(input.markPosition * 140.0);
                halo *= lerp(0.72, 1.0, mottle);
                albedo.rgb *= lerp(float3(1, 1, 1), _MarkTint.rgb, halo);
                albedo.rgb *= lerp(float3(1, 1, 1), _MarkCore.rgb, core);

                half3 normalTS = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, input.uv), _BumpScale);
                float3 normalWS = UberTangentToWorld(normalTS, normalize(input.normalWS), input.tangentWS);

                return UberShade(input.positionWS, normalWS, input.positionCS, input.shadowCoord, input.fogFactor,
                                 albedo.rgb, _Metallic, saturate(_Glossiness + core * _MarkSwelling), 1.0, half3(0, 0, 0));
            }
            ENDHLSL
        }

        // Ombres portées, profondeur et normales (pour l'occlusion ambiante) : celles d'URP Lit.
        UsePass "Universal Render Pipeline/Lit/SHADOWCASTER"
        UsePass "Universal Render Pipeline/Lit/DEPTHONLY"
        UsePass "Universal Render Pipeline/Lit/DEPTHNORMALS"
    }

    // --- Rendu intégré (Built-in) : la version d'origine.
    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows vertex:vert
        #pragma target 3.5

        sampler2D _MainTex;
        sampler2D _BumpMap;
        half _BumpScale;
        fixed4 _Color;
        half _Glossiness;
        half _Metallic;

        fixed4 _MarkTint;
        fixed4 _MarkCore;
        half _MarkSwelling;
        half _MarkNoise;
        float _MarkSpace;

        // Remplis par BruiseSystem, via un bloc de proprietes propre a chaque rendu.
        // _Marks    : xyz = centre (espace objet), w = rayon (metres)
        // _MarkInfo : x = intensite (0..1, monte a l'apparition), y = graine de forme
        float4 _Marks[8];
        float4 _MarkInfo[8];
        float _MarkCount;

        struct Input
        {
            float2 uv_MainTex;
            float3 markPosition;
        };

        float3 ObjectScale()
        {
            return float3(length(unity_ObjectToWorld._m00_m10_m20),
                          length(unity_ObjectToWorld._m01_m11_m21),
                          length(unity_ObjectToWorld._m02_m12_m22));
        }

        void vert(inout appdata_full v, out Input o)
        {
            UNITY_INITIALIZE_OUTPUT(Input, o);

            // Position de l'objet, en metres : l'echelle est appliquee, pas la rotation.
            // Sur un corps skinne, le sommet a deja ete deforme : la marque glisserait sur la
            // peau a chaque mouvement. On lit alors la position de REPOS rangee dans le 3e
            // canal d'UV par l'import du corps — la marque reste collee a la chair.
            o.markPosition = _MarkSpace > 0.5 ? v.texcoord2.xyz : v.vertex.xyz * ObjectScale();
        }

        float Hash(float3 p)
        {
            p = frac(p * 0.3183099 + 0.1);
            p *= 17.0;
            return frac(p.x * p.y * p.z * (p.x + p.y + p.z));
        }

        // Bruit de valeur trilineaire : assez pour deformer un contour, sans texture.
        float Noise(float3 x)
        {
            float3 i = floor(x);
            float3 f = frac(x);
            f = f * f * (3.0 - 2.0 * f);

            return lerp(lerp(lerp(Hash(i + float3(0, 0, 0)), Hash(i + float3(1, 0, 0)), f.x),
                             lerp(Hash(i + float3(0, 1, 0)), Hash(i + float3(1, 1, 0)), f.x), f.y),
                        lerp(lerp(Hash(i + float3(0, 0, 1)), Hash(i + float3(1, 0, 1)), f.x),
                             lerp(Hash(i + float3(0, 1, 1)), Hash(i + float3(1, 1, 1)), f.x), f.y), f.z);
        }

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 albedo = tex2D(_MainTex, IN.uv_MainTex) * _Color;
            float3 scale = ObjectScale();

            float halo = 0.0;
            float core = 0.0;

            int count = (int)_MarkCount;

            [loop]
            for (int i = 0; i < 8; i++)
            {
                if (i >= count) break;

                float4 mark = _Marks[i];
                float4 info = _MarkInfo[i];

                float3 delta = IN.markPosition - mark.xyz * scale;
                float seed = info.y * 7.31;

                // Le contour : la distance est deformee par deux octaves de bruit. Un disque
                // parfait se lit comme un autocollant.
                float wobble = (Noise(IN.markPosition * 32.0 + seed) - 0.5) * _MarkNoise +
                               (Noise(IN.markPosition * 85.0 + seed * 1.7) - 0.5) * _MarkNoise * 0.35;

                float dist = length(delta) / max(mark.w, 0.005) + wobble;

                float h = saturate(1.0 - dist);
                h = h * h * (3.0 - 2.0 * h);

                float c = saturate(1.0 - dist * 1.9);

                halo = max(halo, h * info.x);
                core = max(core, c * c * info.x);
            }

            // La marbrure : un hematome n'est jamais d'une seule couleur.
            float mottle = Noise(IN.markPosition * 140.0);
            halo *= lerp(0.72, 1.0, mottle);

            albedo.rgb *= lerp(float3(1, 1, 1), _MarkTint.rgb, halo);
            albedo.rgb *= lerp(float3(1, 1, 1), _MarkCore.rgb, core);

            float3 bump = UnpackNormal(tex2D(_BumpMap, IN.uv_MainTex));
            bump.xy *= _BumpScale;

            o.Albedo = albedo.rgb;
            o.Normal = normalize(bump);
            o.Metallic = _Metallic;
            o.Smoothness = saturate(_Glossiness + core * _MarkSwelling);
            o.Alpha = 1.0;
        }
        ENDCG
    }

    FallBack "Diffuse"
}
