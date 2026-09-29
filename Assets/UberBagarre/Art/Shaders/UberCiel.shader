// Le ciel de Schedule 1, refait.
//
// Schedule 1 utilise le ciel du pack « Sky Studio » (Funly) : un dégradé à trois couleurs,
// un soleil chaud au bord adouci, des nuages de bruit qui défilent lentement, des étoiles la
// nuit. Le pack n'est pas dans le projet ; ce shader en reprend le principe et les VALEURS
// exactes de leur matériau (couleurs du dégradé et ses trois positions, taille et halo du
// soleil, densité, hauteur, vitesse et couleurs des nuages).
//
// Non éclairé et sans passe propre à un moteur : il marche en Built-in comme sous URP.
// La direction du soleil et la part de nuit viennent de TimeOfDay (_UberSunDir, _UberNight).
Shader "UberBagarre/Ciel"
{
    Properties
    {
        [Header(Degrade de jour)]
        _SkyLower ("Bas", Color) = (0.7398424, 0.7659882, 0.78953505, 1)
        _SkyMiddle ("Milieu", Color) = (0.736243, 0.7931266, 0.8667112, 1)
        _SkyUpper ("Haut", Color) = (0.8131977, 0.86885315, 0.9261664, 1)
        _FadeBegin ("Debut du degrade", Range(-1, 1)) = -0.061
        _FadeMiddle ("Milieu du degrade", Range(-1, 1)) = 0.518
        _FadeEnd ("Fin du degrade", Range(-1, 1)) = 0.62
        _HorizonScale ("Echelle de l'horizon", Range(0.1, 2)) = 0.7

        [Header(Nuit)]
        _NightLower ("Bas (nuit)", Color) = (0.035, 0.045, 0.075, 1)
        _NightMiddle ("Milieu (nuit)", Color) = (0.02, 0.028, 0.055, 1)
        _NightUpper ("Haut (nuit)", Color) = (0.008, 0.012, 0.03, 1)
        _StarDensity ("Densite des etoiles", Range(0, 1)) = 0.35

        [Header(Soleil)]
        _SunColor ("Couleur", Color) = (1, 0.68390805, 0.49124312, 1)
        _SunRadius ("Rayon", Range(0.005, 0.2)) = 0.04
        _SunEdgeFade ("Bord adouci", Range(0, 1)) = 0.134
        _SunBoost ("Intensite", Range(0, 20)) = 10
        _SunGlow ("Halo", Range(0, 2)) = 0.35

        [Header(Nuages)]
        _CloudColor1 ("Couleur eclairee", Color) = (0.8691244, 0.88730156, 0.8982079, 1)
        _CloudColor2 ("Couleur ombree", Color) = (0.6267793, 0.77011293, 0.8786992, 1)
        _CloudDensity ("Densite", Range(0, 1)) = 0.799
        _CloudHeight ("Hauteur", Range(0.05, 2)) = 0.6
        _CloudAlpha ("Opacite", Range(0, 1)) = 0.57777774
        _CloudSpeed ("Vitesse", Range(0, 0.2)) = 0.02
        _CloudDirection ("Direction (radians)", Range(0, 6.2832)) = 3.41
        _CloudTiling ("Repetitions", Range(0.2, 10)) = 3.01
        _CloudFadePosition ("Fondu vers l'horizon", Range(0, 1)) = 0.963
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            fixed4 _SkyLower, _SkyMiddle, _SkyUpper;
            float _FadeBegin, _FadeMiddle, _FadeEnd, _HorizonScale;
            fixed4 _NightLower, _NightMiddle, _NightUpper;
            float _StarDensity;
            fixed4 _SunColor;
            float _SunRadius, _SunEdgeFade, _SunBoost, _SunGlow;
            fixed4 _CloudColor1, _CloudColor2;
            float _CloudDensity, _CloudHeight, _CloudAlpha, _CloudSpeed, _CloudDirection, _CloudTiling, _CloudFadePosition;

            // Posés par TimeOfDay (globaux) : la direction VERS le soleil, et la part de nuit (0 à 1).
            float4 _UberSunDir;
            float _UberNight;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float Hash(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            float Noise(float2 p)
            {
                float2 i = floor(p);
                float2 f = frac(p);
                f = f * f * (3.0 - 2.0 * f);
                return lerp(lerp(Hash(i), Hash(i + float2(1, 0)), f.x), lerp(Hash(i + float2(0, 1)), Hash(i + float2(1, 1)), f.x), f.y);
            }

            float Fbm(float2 p)
            {
                float v = 0.0;
                float a = 0.5;
                for (int k = 0; k < 5; k++)
                {
                    v += Noise(p) * a;
                    p = p * 2.03 + 17.1;
                    a *= 0.5;
                }

                return v;
            }

            fixed3 Gradient(float y, fixed3 lower, fixed3 middle, fixed3 upper)
            {
                float yy = y / max(_HorizonScale, 0.01);
                if (yy < _FadeMiddle) return lerp(lower, middle, smoothstep(_FadeBegin, _FadeMiddle, yy));
                return lerp(middle, upper, smoothstep(_FadeMiddle, _FadeEnd, yy));
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float night = saturate(_UberNight);
                float3 sunDir = dot(_UberSunDir.xyz, _UberSunDir.xyz) > 0.001 ? normalize(_UberSunDir.xyz) : normalize(float3(0.3, 0.6, 0.4));

                fixed3 day = Gradient(dir.y, _SkyLower.rgb, _SkyMiddle.rgb, _SkyUpper.rgb);
                fixed3 dark = Gradient(dir.y, _NightLower.rgb, _NightMiddle.rgb, _NightUpper.rgb);
                fixed3 color = lerp(day, dark, night);

                // Le soleil : un disque au bord adouci, un halo large autour.
                float d = distance(dir, sunDir);
                float disc = 1.0 - smoothstep(_SunRadius * (1.0 - _SunEdgeFade), _SunRadius, d);
                float glow = pow(saturate(1.0 - d * 0.6), 6.0) * _SunGlow;
                float above = saturate(sunDir.y * 8.0 + 0.5);
                color += _SunColor.rgb * (disc * _SunBoost + glow) * above * (1.0 - night);

                // Les étoiles : la nuit seulement, au-dessus de l'horizon.
                if (night > 0.01 && dir.y > 0.0)
                {
                    float2 cell = dir.xz / (dir.y + 0.35) * 90.0;
                    float star = Hash(floor(cell));
                    float2 f = frac(cell) - 0.5;
                    float twinkle = 0.7 + 0.3 * sin(_Time.y * (2.0 + star * 3.0) + star * 40.0);
                    float s = step(1.0 - _StarDensity * 0.08, star) * smoothstep(0.35, 0.0, length(f)) * twinkle;
                    color += s * night * saturate(dir.y * 4.0);
                }

                // Les nuages : un plan de bruit au-dessus de la ville, qui défile.
                if (dir.y > 0.0)
                {
                    float2 plane = dir.xz / (dir.y + _CloudHeight * 0.25);
                    float2 wind = float2(cos(_CloudDirection), sin(_CloudDirection)) * _Time.y * _CloudSpeed;
                    float n = Fbm(plane * _CloudTiling * 0.35 + wind);
                    float cover = smoothstep(1.0 - _CloudDensity * 0.72, 1.05 - _CloudDensity * 0.35, n);
                    float fade = smoothstep(0.0, 1.0 - _CloudFadePosition + 0.08, dir.y);
                    fixed3 cloud = lerp(_CloudColor2.rgb, _CloudColor1.rgb, saturate(n * 1.4 - 0.2));
                    cloud = lerp(cloud, cloud * 0.12, night);
                    color = lerp(color, cloud, cover * fade * _CloudAlpha);
                }

                return fixed4(color, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
