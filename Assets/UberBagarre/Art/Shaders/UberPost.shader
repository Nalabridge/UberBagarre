// Chaine de post-traitement complete, ecrite a la main.
//
// Pourquoi a la main : le projet est en Built-in Render Pipeline sans le paquet
// Post Processing. Sans bloom, une enseigne au neon rend exactement comme un mur
// peint en rose — la valeur du pixel est plafonnee a 1 et rien ne deborde. C'est
// pour cette raison qu'une scene de nuit parait "cheap" : ce n'est pas la
// geometrie, c'est qu'aucune source lumineuse ne se comporte comme une source.
//
// Quatre passes :
//   0 - Prefiltre  : isole ce qui depasse le seuil, avec genou doux et moyenne
//                    de Karis (sinon un seul pixel tres brillant devient une
//                    etoile clignotante des que la camera bouge).
//   1 - Reduction  : filtre 13 taps (Jimenez, Siggraph 2014). Un simple bilineaire
//                    fait "pulser" le bloom pendant les deplacements.
//   2 - Agrandiss. : filtre en tente 9 taps, additionne au niveau superieur.
//   3 - Composite  : bloom + exposition + tonemap ACES + etalonnage + vignette +
//                    aberration chromatique + grain.
Shader "UberBagarre/Post"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    float4 _MainTex_TexelSize;
    float4 _MainTex_ST;

    sampler2D _BloomTex;
    sampler2D _SourceTex;

    // x = seuil, y = seuil - genou, z = 2 * genou, w = 0.25 / genou
    float4 _Filter;
    float _SampleScale;

    float _BloomIntensity;
    float4 _BloomTint;

    float _Exposure;
    float _Contrast;
    float _Saturation;
    float4 _ColorFilter;
    float4 _Shadows;
    float4 _Highlights;

    // x = intensite, y = douceur, z = rondeur
    float4 _Vignette;
    float4 _VignetteColor;

    float _Aberration;
    float _GrainIntensity;
    float _GrainScale;
    float _Seed;

    // 1 = le projet est en espace lineaire, 0 = en gamma.
    float _LinearMode;

    struct appdata_post
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
    };

    struct v2f_post
    {
        float4 pos : SV_POSITION;
        float2 uv : TEXCOORD0;
    };

    v2f_post VertPost(appdata_post v)
    {
        v2f_post o;
        o.pos = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;
        return o;
    }

    float Brightness(float3 c)
    {
        return max(c.r, max(c.g, c.b));
    }

    // Moyenne de Karis : on pondere chaque groupe de taps par 1/(1+luminance).
    // Sans elle, un pixel isole a 40 de luminance domine tout le voisinage et
    // produit un scintillement franc des que la camera tourne d'un demi-pixel.
    float KarisWeight(float3 c)
    {
        return 1.0 / (1.0 + Brightness(c));
    }

    float3 SampleSafe(sampler2D tex, float2 uv)
    {
        float3 c = tex2D(tex, uv).rgb;
        // Un NaN ou un Inf se propage a toute la pyramide de bloom et repeint
        // l'ecran en noir. On le coupe ici, une fois, plutot que de le chercher.
        c = max(c, 0.0);
        return min(c, 60.0);
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        // ------------------------------------------------------ 0 : prefiltre
        Pass
        {
            CGPROGRAM
            #pragma vertex VertPost
            #pragma fragment FragPrefilter
            #pragma target 3.0

            float4 FragPrefilter(v2f_post i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;

                float3 a = SampleSafe(_MainTex, i.uv + texel * float2(-1.0, -1.0));
                float3 b = SampleSafe(_MainTex, i.uv + texel * float2( 1.0, -1.0));
                float3 c = SampleSafe(_MainTex, i.uv + texel * float2(-1.0,  1.0));
                float3 d = SampleSafe(_MainTex, i.uv + texel * float2( 1.0,  1.0));

                float wa = KarisWeight(a);
                float wb = KarisWeight(b);
                float wc = KarisWeight(c);
                float wd = KarisWeight(d);

                float3 color = (a * wa + b * wb + c * wc + d * wd) / max(1e-4, wa + wb + wc + wd);

                // Seuil a genou doux : une coupure franche fait apparaitre un
                // contour net autour des sources, tres visible sur un degrade.
                float brightness = Brightness(color);
                float soft = brightness - _Filter.y;
                soft = clamp(soft, 0.0, _Filter.z);
                soft = soft * soft * _Filter.w;

                float contribution = max(soft, brightness - _Filter.x);
                contribution /= max(brightness, 1e-4);

                return float4(color * contribution, 1.0);
            }
            ENDCG
        }

        // ---------------------------------------------------- 1 : reduction
        Pass
        {
            CGPROGRAM
            #pragma vertex VertPost
            #pragma fragment FragDownsample
            #pragma target 3.0

            float4 FragDownsample(v2f_post i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy;
                float2 uv = i.uv;

                float3 center = SampleSafe(_MainTex, uv);

                float3 tl = SampleSafe(_MainTex, uv + texel * float2(-1.0,  1.0));
                float3 tr = SampleSafe(_MainTex, uv + texel * float2( 1.0,  1.0));
                float3 bl = SampleSafe(_MainTex, uv + texel * float2(-1.0, -1.0));
                float3 br = SampleSafe(_MainTex, uv + texel * float2( 1.0, -1.0));

                float3 t  = SampleSafe(_MainTex, uv + texel * float2( 0.0,  2.0));
                float3 bo = SampleSafe(_MainTex, uv + texel * float2( 0.0, -2.0));
                float3 l  = SampleSafe(_MainTex, uv + texel * float2(-2.0,  0.0));
                float3 r  = SampleSafe(_MainTex, uv + texel * float2( 2.0,  0.0));

                float3 tl2 = SampleSafe(_MainTex, uv + texel * float2(-2.0,  2.0));
                float3 tr2 = SampleSafe(_MainTex, uv + texel * float2( 2.0,  2.0));
                float3 bl2 = SampleSafe(_MainTex, uv + texel * float2(-2.0, -2.0));
                float3 br2 = SampleSafe(_MainTex, uv + texel * float2( 2.0, -2.0));

                float3 color = center * 0.125;
                color += (tl + tr + bl + br) * 0.125;
                color += (t + bo + l + r) * 0.0625;
                color += (tl2 + tr2 + bl2 + br2) * 0.03125;

                return float4(color, 1.0);
            }
            ENDCG
        }

        // ------------------------------------------- 2 : agrandissement additif
        Pass
        {
            Blend One One

            CGPROGRAM
            #pragma vertex VertPost
            #pragma fragment FragUpsample
            #pragma target 3.0

            float4 FragUpsample(v2f_post i) : SV_Target
            {
                float2 texel = _MainTex_TexelSize.xy * _SampleScale;
                float2 uv = i.uv;

                float3 color = SampleSafe(_MainTex, uv) * 4.0;

                color += SampleSafe(_MainTex, uv + texel * float2(-1.0,  0.0)) * 2.0;
                color += SampleSafe(_MainTex, uv + texel * float2( 1.0,  0.0)) * 2.0;
                color += SampleSafe(_MainTex, uv + texel * float2( 0.0, -1.0)) * 2.0;
                color += SampleSafe(_MainTex, uv + texel * float2( 0.0,  1.0)) * 2.0;

                color += SampleSafe(_MainTex, uv + texel * float2(-1.0, -1.0));
                color += SampleSafe(_MainTex, uv + texel * float2( 1.0, -1.0));
                color += SampleSafe(_MainTex, uv + texel * float2(-1.0,  1.0));
                color += SampleSafe(_MainTex, uv + texel * float2( 1.0,  1.0));

                return float4(color * (1.0 / 16.0), 1.0);
            }
            ENDCG
        }

        // ----------------------------------------------------- 3 : composite
        Pass
        {
            CGPROGRAM
            #pragma vertex VertPost
            #pragma fragment FragComposite
            #pragma target 3.0

            // Courbe ACES approchee (Krzysztof Narkowicz). Elle fait deux choses
            // qu'un simple saturate() ne fait pas : elle desature progressivement
            // les tres hautes lumieres — sans quoi un neon rouge sature devient un
            // aplat rouge pur sans coeur blanc — et elle garde du detail dans les
            // ombres au lieu de les ecraser a zero.
            float3 AcesFilmic(float3 x)
            {
                const float a = 2.51;
                const float b = 0.03;
                const float c = 2.43;
                const float d = 0.59;
                const float e = 0.14;
                return saturate((x * (a * x + b)) / (x * (c * x + d) + e));
            }

            float Hash(float2 p, float seed)
            {
                float3 v = float3(p.xy, seed);
                v = frac(v * 0.1031);
                v += dot(v, v.yzx + 33.33);
                return frac((v.x + v.y) * v.z);
            }

            float4 FragComposite(v2f_post i) : SV_Target
            {
                float2 uv = i.uv;
                float2 centered = uv - 0.5;

                // Aberration chromatique : les trois canaux ne sont pas echantillonnes
                // au meme endroit, l'ecart croissant avec la distance au centre. C'est
                // ce que fait un vrai objectif, et c'est ce qui manque le plus a une
                // image "propre" pour ressembler a une prise de vue.
                float3 color;
                if (_Aberration > 0.0001)
                {
                    float2 offset = centered * _Aberration * 0.01;
                    color.r = tex2D(_MainTex, uv + offset).r;
                    color.g = tex2D(_MainTex, uv).g;
                    color.b = tex2D(_MainTex, uv - offset).b;
                }
                else
                {
                    color = tex2D(_MainTex, uv).rgb;
                }

                color = max(color, 0.0);

                // Le traitement se fait en lineaire. Si le projet est en gamma, les
                // valeurs lues sont deja encodees : les tonemapper telles quelles
                // delave l'image entiere.
                if (_LinearMode < 0.5) color = GammaToLinearSpace(color);

                float3 bloom = tex2D(_BloomTex, uv).rgb;
                if (_LinearMode < 0.5) bloom = GammaToLinearSpace(max(bloom, 0.0));

                color += bloom * _BloomIntensity * _BloomTint.rgb;

                color *= _Exposure;

                // Etalonnage AVANT le tonemap : appliquer un contraste apres la
                // courbe recrase les hautes lumieres qu'elle vient justement de sauver.
                float luminance = dot(color, float3(0.2126, 0.7152, 0.0722));
                color = lerp(float3(luminance, luminance, luminance), color, _Saturation);
                color = (color - 0.18) * _Contrast + 0.18;
                color = max(color, 0.0);
                color *= _ColorFilter.rgb;

                // Teinte separee des ombres et des hautes lumieres : c'est ce qui
                // donne a une scene de nuit son bleu dans les noirs et son ambre
                // dans les lampadaires, au lieu d'un gris uniforme assombri.
                float shadowMask = saturate(1.0 - luminance * 2.2);
                float highlightMask = saturate(luminance * 1.4 - 0.25);
                color *= lerp(float3(1.0, 1.0, 1.0), _Shadows.rgb, shadowMask * _Shadows.a);
                color *= lerp(float3(1.0, 1.0, 1.0), _Highlights.rgb, highlightMask * _Highlights.a);

                color = AcesFilmic(color);

                // Vignette : la rondeur corrige l'aspect ratio, sinon elle devient
                // une ellipse ecrasee sur un ecran large.
                if (_Vignette.x > 0.0001)
                {
                    float2 v = centered;
                    float aspect = _MainTex_TexelSize.z / max(1.0, _MainTex_TexelSize.w);
                    v.x *= lerp(1.0, aspect, _Vignette.z);
                    float distance = length(v) * 1.4142136;
                    float mask = 1.0 - smoothstep(_Vignette.y, 1.0, distance * (1.0 + _Vignette.x));
                    mask = saturate(lerp(1.0, mask, _Vignette.x));
                    color = lerp(_VignetteColor.rgb * color, color, mask);
                }

                // Grain : module la luminance plutot que de s'ajouter. Un grain
                // additif eclaircit les noirs et donne un voile gris.
                if (_GrainIntensity > 0.0001)
                {
                    float2 grainUv = uv * _MainTex_TexelSize.zw / max(1.0, _GrainScale);
                    float noise = Hash(floor(grainUv), _Seed) - 0.5;
                    // Plus visible dans les zones sombres, comme une vraie pellicule.
                    float response = 1.0 - saturate(dot(color, float3(0.333, 0.333, 0.333)));
                    color += noise * _GrainIntensity * (0.35 + 0.65 * response);
                    color = max(color, 0.0);
                }

                if (_LinearMode < 0.5) color = LinearToGammaSpace(color);

                return float4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
