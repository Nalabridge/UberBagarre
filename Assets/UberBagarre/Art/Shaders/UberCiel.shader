// Le ciel du jeu : une atmosphère qui suit le soleil (bleu profond le jour, horizon embrasé au
// couchant, nuit étoilée), de vrais nuages éclairés côté soleil et bordés de lumière à contre-
// jour, des cirrus en altitude, la lune. La couverture nuageuse suit la météo.
//
// Les calculs sont dans UberSky.hlsl (l'eau s'en sert pour ses reflets). Non éclairé, sans passe
// propre à un moteur : il marche en rendu intégré comme sous URP.
// Globaux posés par TimeOfDay : _UberSunDir (vers le soleil), _UberMoonDir, _UberNight (0 à 1),
// _UberOvercast (0 à 1).
Shader "UberBagarre/Ciel"
{
    Properties
    {
        [Header(Nuages)]
        _CloudCoverage ("Couverture", Range(0, 1)) = 0.5
        _CloudSpeed ("Vitesse", Range(0, 0.1)) = 0.012
        _CloudScale ("Taille (petit = gros nuages)", Range(0.2, 3)) = 1.25

        [Header(Soleil et lune)]
        _SunSize ("Taille du soleil", Range(0.005, 0.1)) = 0.028
        _SunIntensity ("Eclat du soleil", Range(0, 30)) = 14
        _MoonSize ("Taille de la lune", Range(0.005, 0.1)) = 0.035

        [Header(Nuit)]
        _StarDensity ("Densite des etoiles", Range(0, 1)) = 0.45

        [Header(Image)]
        _Exposure ("Exposition", Range(0.2, 3)) = 1
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
            #include "Assets/UberBagarre/Art/Shaders/UberSky.hlsl"

            float _CloudCoverage, _CloudSpeed, _CloudScale;
            float _SunSize, _SunIntensity, _MoonSize;
            float _StarDensity, _Exposure;

            float4 _UberSunDir;
            float4 _UberMoonDir;
            float _UberNight;
            float _UberOvercast;

            struct appdata { float4 vertex : POSITION; };
            struct v2f { float4 pos : SV_POSITION; float3 dir : TEXCOORD0; };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.dir = v.vertex.xyz;
                return o;
            }

            float3 Stars(float3 dir, float night)
            {
                if (night < 0.01 || dir.y <= 0.0) return 0;
                float2 cell = dir.xz / (dir.y + 0.35) * 110.0;
                float star = UberSkyHash(floor(cell));
                float2 f = frac(cell) - 0.5;
                float twinkle = 0.65 + 0.35 * sin(_Time.y * (1.5 + star * 3.0) + star * 40.0);
                float s = step(1.0 - _StarDensity * 0.07, star) * smoothstep(0.32, 0.0, length(f)) * twinkle;
                float3 tint = lerp(float3(0.8, 0.88, 1.0), float3(1.0, 0.92, 0.8), frac(star * 17.0));
                return tint * s * night * saturate(dir.y * 4.0) * 1.4;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float3 dir = normalize(i.dir);
                float night = saturate(_UberNight);
                float overcast = saturate(_UberOvercast);
                float3 sunDir = dot(_UberSunDir.xyz, _UberSunDir.xyz) > 0.001 ? normalize(_UberSunDir.xyz) : normalize(float3(0.3, 0.6, 0.4));
                float3 moonDir = dot(_UberMoonDir.xyz, _UberMoonDir.xyz) > 0.001 ? normalize(_UberMoonDir.xyz) : -sunDir;

                float3 col = UberSkyGradient(dir, sunDir, night, overcast);
                col += Stars(dir, night) * (1.0 - overcast);

                // Le disque du soleil, au bord adouci (caché par les nuages plus bas).
                float sunAngle = distance(dir, sunDir);
                float sun = 1.0 - smoothstep(_SunSize * 0.85, _SunSize, sunAngle);
                col += UberSkySunColor(sunDir) * sun * _SunIntensity * saturate(sunDir.y * 10.0 + 0.3) * (1.0 - overcast);

                // La lune : un disque pâle, légèrement grêlé, et son halo.
                float moonAngle = distance(dir, moonDir);
                float moon = 1.0 - smoothstep(_MoonSize * 0.9, _MoonSize, moonAngle);
                float craters = 0.85 + 0.15 * UberSkyNoise((dir.xy - moonDir.xy) * 120.0);
                float moonGlow = pow(saturate(dot(dir, moonDir)), 200.0) * 0.25;
                col += float3(0.85, 0.9, 1.0) * (moon * 1.6 * craters + moonGlow) * night * (1.0 - overcast * 0.8);

                float4 clouds = UberSkyClouds(dir, sunDir, night, overcast, _Time.y, _CloudCoverage, _CloudSpeed, _CloudScale);
                col = lerp(col, clouds.rgb, clouds.a);

                return fixed4(col * _Exposure, 1.0);
            }
            ENDCG
        }
    }

    Fallback Off
}
