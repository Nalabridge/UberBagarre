// Chaine de post-traitement complete, ecrite a la main.
//
// Pourquoi a la main : le projet est en Built-in Render Pipeline sans le paquet
// Post Processing. Sans bloom, une enseigne au neon rend exactement comme un mur
// peint en rose — la valeur du pixel est plafonnee a 1 et rien ne deborde. C'est
// pour cette raison qu'une scene de nuit parait "cheap" : ce n'est pas la
// geometrie, c'est qu'aucune source lumineuse ne se comporte comme une source.
//
// Sept passes :
//   0 - Prefiltre  : isole ce qui depasse le seuil, avec genou doux et moyenne
//                    de Karis (sinon un seul pixel tres brillant devient une
//                    etoile clignotante des que la camera bouge).
//   1 - Reduction  : filtre 13 taps (Jimenez, Siggraph 2014). Un simple bilineaire
//                    fait "pulser" le bloom pendant les deplacements.
//   2 - Agrandiss. : filtre en tente 9 taps, additionne au niveau superieur.
//   3 - Composite  : bloom + lumiere volumetrique + exposition + tonemap ACES +
//                    etalonnage + vignette + aberration chromatique + grain.
//   4 - Volumetrique : la lumiere diffusee par l'air humide, calculee a partir des
//                    VRAIES lampes de la scene (position, cone, couleur, portee).
//   5 - FXAA       : anticrenelage sur l'image finale.
//   6 - TAA        : anticrenelage temporel, sur l'image HDR avant tout le reste.
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

    sampler2D _VolumetricTex;
    float _VolumetricOn;

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

                // Lumiere diffusee par l'air : deja lineaire, calculee a demi-resolution.
                // Elle est lisse par construction, donc un filtrage bilineaire suffit.
                if (_VolumetricOn > 0.5) color += tex2D(_VolumetricTex, uv).rgb;

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

        // ------------------------------------------- 4 : lumiere volumetrique
        //
        // Ce que les cones en maillage additif imitaient mal : l'air humide qui renvoie
        // une partie de la lumiere vers l'oeil. Un cone peint se voit comme un objet — il
        // a des bords, il traverse les murs, il ne reagit a rien. Ici, pour chaque pixel,
        // on INTEGRE la lumiere recue le long du rayon de vue, depuis la camera jusqu'a
        // la premiere surface, pour chaque lampe proche.
        //
        // L'integrale de 1/r^2 le long d'une droite a une forme exacte en arc tangente.
        // Les echantillons sont donc places a pas d'ANGLE constant vu depuis la lampe
        // (echantillonnage equi-angulaire) : dense pres de la lampe, la ou la lumiere
        // varie vite, espace au loin. Douze echantillons fixes, sans aucun tirage
        // aleatoire : le resultat est lisse et stable d'une image a l'autre. Un lancer de
        // rayons classique avec du bruit de tramage aurait justement produit le
        // grouillement qu'on cherche a supprimer.
        //
        // Le faisceau s'arrete sur la premiere surface (tampon de profondeur) : un
        // combattant devant un lampadaire coupe le faisceau derriere lui.
        Pass
        {
            CGPROGRAM
            #pragma vertex VertPost
            #pragma fragment FragVolumetric
            #pragma target 3.0

            #define VOL_MAX_LIGHTS 12
            #define VOL_STEPS 12

            sampler2D_float _CameraDepthTexture;

            float4 _FrustumBL;
            float4 _FrustumTL;
            float4 _FrustumTR;
            float4 _FrustumBR;

            // xyz = position, w = portee
            float4 _VolPos[VOL_MAX_LIGHTS];

            // xyz = axe du projecteur, w = cosinus du demi-angle exterieur (-2 = lampe ponctuelle)
            float4 _VolDir[VOL_MAX_LIGHTS];

            // rgb = couleur lineaire * intensite, w = 1 / (cos interieur - cos exterieur)
            float4 _VolColor[VOL_MAX_LIGHTS];

            float _VolCount;

            // x = densite, y = distance maximale, z = decroissance avec la hauteur, w = niveau du sol
            float4 _VolParams;

            float4 FragVolumetric(v2f_post i) : SV_Target
            {
                float2 uv = i.uv;

                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0) uv.y = 1.0 - uv.y;
                #endif

                float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
                float eyeDepth = LinearEyeDepth(rawDepth);

                // Rayon de vue reconstruit a partir des coins du frustum : composante
                // « profondeur » egale a 1, donc rayon * profondeur = point de la surface.
                float3 ray = lerp(lerp(_FrustumBL.xyz, _FrustumBR.xyz, uv.x),
                                  lerp(_FrustumTL.xyz, _FrustumTR.xyz, uv.x), uv.y);

                float rayLength = max(length(ray), 1e-5);
                float3 dir = ray / rayLength;
                float tMax = min(eyeDepth * rayLength, _VolParams.y);
                float3 origin = _WorldSpaceCameraPos;

                float3 total = float3(0.0, 0.0, 0.0);
                int count = (int)_VolCount;

                [loop]
                for (int k = 0; k < VOL_MAX_LIGHTS; k++)
                {
                    if (k >= count) break;

                    float3 lightPos = _VolPos[k].xyz;
                    float range = _VolPos[k].w;

                    // Point du rayon le plus proche de la lampe, et distance a ce point.
                    float t0 = dot(lightPos - origin, dir);
                    float h = length(origin + dir * t0 - lightPos);
                    if (h >= range) continue;

                    // Plancher : sans lui, un rayon qui frole l'ampoule recoit une valeur
                    // infinie, et le bloom en fait un soleil.
                    h = max(h, 0.35);

                    // Seule la partie du rayon situee dans la portee de la lampe compte.
                    float chord = sqrt(max(range * range - h * h, 0.0));
                    float ta = max(0.0, t0 - chord);
                    float tb = min(tMax, t0 + chord);
                    if (tb <= ta) continue;

                    float thetaA = atan((ta - t0) / h);
                    float thetaB = atan((tb - t0) / h);

                    float weight = 0.0;

                    [unroll]
                    for (int s = 0; s < VOL_STEPS; s++)
                    {
                        float theta = lerp(thetaA, thetaB, (s + 0.5) / VOL_STEPS);
                        float t = t0 + h * tan(theta);
                        float3 p = origin + dir * t;

                        float3 toPoint = p - lightPos;
                        float d = length(toPoint);
                        float3 l = toPoint / max(d, 1e-3);

                        // Cone du projecteur, bord adouci.
                        float cone = 1.0;
                        if (_VolDir[k].w > -1.5)
                        {
                            cone = saturate((dot(l, _VolDir[k].xyz) - _VolDir[k].w) * _VolColor[k].w);
                            cone *= cone;
                        }

                        // Extinction douce a la portee, comme la lampe elle-meme.
                        float fade = saturate(1.0 - (d * d) / (range * range));

                        // La brume est plus dense pres du sol : l'humidite y stagne.
                        float height = exp(-max(p.y - _VolParams.w, 0.0) * _VolParams.z);

                        weight += cone * fade * fade * height;
                    }

                    // Integrale exacte de 1/r^2 sur le segment, ponderee par la moyenne
                    // du cone, de la portee et de la hauteur le long du segment.
                    float integral = (thetaB - thetaA) / h * (weight / VOL_STEPS);
                    total += _VolColor[k].rgb * integral;
                }

                return float4(total * _VolParams.x, 1.0);
            }
            ENDCG
        }

        // ------------------------------------------------------------ 5 : FXAA
        //
        // Le rendu differe ne sait pas faire de MSAA. Sans anticrenelage, chaque arete
        // fine — rambarde, cable, bord d'enseigne — scintille des que la camera bouge,
        // et l'oeil le lit comme du bruit. FXAA repere les contrastes locaux sur l'image
        // finale et lisse le long des aretes, pas en travers.
        Pass
        {
            CGPROGRAM
            #pragma vertex VertPost
            #pragma fragment FragFxaa
            #pragma target 3.0

            float FxaaLuma(float3 c)
            {
                // Luminance PERCUE : l'algorithme doit comparer des contrastes visibles.
                return sqrt(dot(saturate(c), float3(0.299, 0.587, 0.114)));
            }

            float4 FragFxaa(v2f_post i) : SV_Target
            {
                float2 t = _MainTex_TexelSize.xy;
                float2 uv = i.uv;

                float4 center = tex2D(_MainTex, uv);
                float3 rgbNW = tex2D(_MainTex, uv + float2(-1.0, -1.0) * t).rgb;
                float3 rgbNE = tex2D(_MainTex, uv + float2( 1.0, -1.0) * t).rgb;
                float3 rgbSW = tex2D(_MainTex, uv + float2(-1.0,  1.0) * t).rgb;
                float3 rgbSE = tex2D(_MainTex, uv + float2( 1.0,  1.0) * t).rgb;

                float lumaNW = FxaaLuma(rgbNW);
                float lumaNE = FxaaLuma(rgbNE);
                float lumaSW = FxaaLuma(rgbSW);
                float lumaSE = FxaaLuma(rgbSE);
                float lumaM = FxaaLuma(center.rgb);

                float lumaMin = min(lumaM, min(min(lumaNW, lumaNE), min(lumaSW, lumaSE)));
                float lumaMax = max(lumaM, max(max(lumaNW, lumaNE), max(lumaSW, lumaSE)));

                // Contraste local trop faible : rien a lisser.
                if (lumaMax - lumaMin < max(0.0312, lumaMax * 0.125)) return center;

                float2 dir;
                dir.x = -((lumaNW + lumaNE) - (lumaSW + lumaSE));
                dir.y =  ((lumaNW + lumaSW) - (lumaNE + lumaSE));

                float dirReduce = max((lumaNW + lumaNE + lumaSW + lumaSE) * (0.25 * 0.125), 1.0 / 128.0);
                float rcpDirMin = 1.0 / (min(abs(dir.x), abs(dir.y)) + dirReduce);
                dir = clamp(dir * rcpDirMin, -8.0, 8.0) * t;

                float3 rgbA = 0.5 * (tex2D(_MainTex, uv + dir * (1.0 / 3.0 - 0.5)).rgb +
                                     tex2D(_MainTex, uv + dir * (2.0 / 3.0 - 0.5)).rgb);

                float3 rgbB = rgbA * 0.5 + 0.25 * (tex2D(_MainTex, uv - dir * 0.5).rgb +
                                                    tex2D(_MainTex, uv + dir * 0.5).rgb);

                float lumaB = FxaaLuma(rgbB);
                float3 result = (lumaB < lumaMin || lumaB > lumaMax) ? rgbA : rgbB;

                return float4(result, center.a);
            }
            ENDCG
        }

        // ------------------------------------------------------------- 6 : TAA
        //
        // Anticrenelage temporel. La camera est decalee d'une fraction de pixel differente a
        // chaque image (voir UberPostProcess) ; on accumule ces images dans un historique,
        // reprojete grace aux vecteurs de mouvement. Resultat : les aretes fines, les reflets
        // et les neons ne scintillent plus quand on bouge — ce que ni le FXAA ni le MSAA ne
        // savent faire, puisqu'ils ne voient qu'une image a la fois.
        //
        // Deux gardes-fous contre les trainees :
        // - l'historique est BORNE par les couleurs voisines de l'image courante : une couleur
        //   qui n'existe plus autour du pixel est rejetee ;
        // - le mouvement est lu sur le pixel le plus PROCHE du voisinage, pour que le bord d'un
        //   objet au premier plan emporte son propre mouvement et pas celui du fond.
        //
        // Le calcul se fait sur des couleurs COMPRESSEES (c / (1 + c)) : sans cela, un seul
        // pixel de neon a 40 domine la moyenne et clignote.
        Pass
        {
            CGPROGRAM
            #pragma vertex VertPost
            #pragma fragment FragTaa
            #pragma target 3.0

            sampler2D _HistoryTex;
            sampler2D_float _CameraDepthTexture;
            sampler2D_half _CameraMotionVectorsTexture;

            // xy = decalage de l'image courante, en coordonnees de texture
            float4 _Jitter;

            // x = poids de l'historique a l'arret, y = en mouvement, z = nettete
            float4 _TaaParams;

            float3 Compress(float3 c)
            {
                return c / (1.0 + Brightness(c));
            }

            float3 Expand(float3 c)
            {
                return c / max(1e-4, 1.0 - Brightness(c));
            }

            #if defined(UNITY_REVERSED_Z)
                #define TAA_CLOSER(a, b) step(b, a)
            #else
                #define TAA_CLOSER(a, b) step(a, b)
            #endif

            float2 ClosestFragment(float2 uv)
            {
                float2 k = abs(_MainTex_TexelSize.xy);

                float4 around = float4(
                    SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv - k),
                    SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + float2(k.x, -k.y)),
                    SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + float2(-k.x, k.y)),
                    SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv + k));

                float3 result = float3(0.0, 0.0, SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv));
                result = lerp(result, float3(-1.0, -1.0, around.x), TAA_CLOSER(around.x, result.z));
                result = lerp(result, float3( 1.0, -1.0, around.y), TAA_CLOSER(around.y, result.z));
                result = lerp(result, float3(-1.0,  1.0, around.z), TAA_CLOSER(around.z, result.z));
                result = lerp(result, float3( 1.0,  1.0, around.w), TAA_CLOSER(around.w, result.z));

                return uv + result.xy * k;
            }

            float4 FragTaa(v2f_post i) : SV_Target
            {
                float2 uv = i.uv;
                float2 k = abs(_MainTex_TexelSize.xy);

                // Profondeur et mouvement sont dans l'orientation de la camera ; l'image source
                // peut etre retournee sur certaines plateformes.
                float2 uvScene = uv;
                float flip = 1.0;
                #if UNITY_UV_STARTS_AT_TOP
                if (_MainTex_TexelSize.y < 0.0)
                {
                    uvScene.y = 1.0 - uvScene.y;
                    flip = -1.0;
                }
                #endif

                float2 motion = tex2D(_CameraMotionVectorsTexture, ClosestFragment(uvScene)).xy;
                motion.y *= flip;

                float2 uvCurrent = uv - _Jitter.xy;

                float3 color = Compress(SampleSafe(_MainTex, uvCurrent));
                float3 top = Compress(SampleSafe(_MainTex, uvCurrent + float2(0.0, k.y)));
                float3 bottom = Compress(SampleSafe(_MainTex, uvCurrent - float2(0.0, k.y)));
                float3 left = Compress(SampleSafe(_MainTex, uvCurrent - float2(k.x, 0.0)));
                float3 right = Compress(SampleSafe(_MainTex, uvCurrent + float2(k.x, 0.0)));

                float3 minimum = min(color, min(min(top, bottom), min(left, right)));
                float3 maximum = max(color, max(max(top, bottom), max(left, right)));

                // Legere accentuation : l'accumulation adoucit, on rend un peu de nettete.
                float3 blurred = (top + bottom + left + right) * 0.25;
                color = clamp(color + (color - blurred) * _TaaParams.z, minimum, maximum);

                float2 historyUv = uv - motion;
                float3 history = Compress(SampleSafe(_HistoryTex, historyUv));
                history = clamp(history, minimum, maximum);

                float pixelsMoved = length(motion * abs(_MainTex_TexelSize.zw));
                float weight = lerp(_TaaParams.x, _TaaParams.y, saturate(pixelsMoved / 6.0));

                // Historique hors de l'ecran : rien a reprendre.
                if (historyUv.x < 0.0 || historyUv.y < 0.0 || historyUv.x > 1.0 || historyUv.y > 1.0) weight = 0.0;

                return float4(Expand(lerp(color, history, weight)), 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
