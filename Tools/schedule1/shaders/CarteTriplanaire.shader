// Über Bagarre — matériau « triplanaire » de la carte : la texture est plaquée selon la position
// dans le monde (les murs, les tôles, les falaises de la carte n'ont pas d'UV propres).
Shader "UberBagarre/Carte/Triplanaire"
{
    Properties
    {
        _Color ("Teinte", Color) = (1,1,1,1)
        _MainTex ("Albedo", 2D) = "white" {}
        [Normal] _BumpMap ("Normale", 2D) = "bump" {}
        _BumpScale ("Force de la normale", Float) = 1
        _Tiling ("Repetitions par metre", Float) = 1
        _Sharpness ("Nettete des transitions", Range(1, 16)) = 6
        _Glossiness ("Lissage", Range(0, 1)) = 0.2
        _Metallic ("Metal", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" }
        LOD 300

        CGPROGRAM
        #pragma surface surf Standard fullforwardshadows addshadow
        #pragma target 3.0
        #pragma shader_feature_local _NORMALMAP

        sampler2D _MainTex;
        sampler2D _BumpMap;
        fixed4 _Color;
        half _BumpScale;
        float _Tiling;
        half _Sharpness;
        half _Glossiness;
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
            float3 w = pow(abs(n), _Sharpness);
            w /= max(w.x + w.y + w.z, 1e-4);

            float3 p = IN.worldPos * _Tiling;
            fixed4 c = tex2D(_MainTex, p.zy) * w.x + tex2D(_MainTex, p.xz) * w.y + tex2D(_MainTex, p.xy) * w.z;
            c *= _Color;
            o.Albedo = c.rgb;
            o.Alpha = 1;
            o.Smoothness = _Glossiness;
            o.Metallic = _Metallic;

            float3 wn = n;
            #ifdef _NORMALMAP
            // Mélange « whiteout » des trois projections, en espace monde.
            half3 tx = UnpackScaleNormal(tex2D(_BumpMap, p.zy), _BumpScale);
            half3 ty = UnpackScaleNormal(tex2D(_BumpMap, p.xz), _BumpScale);
            half3 tz = UnpackScaleNormal(tex2D(_BumpMap, p.xy), _BumpScale);
            tx = half3(tx.xy + n.zy, abs(tx.z) * n.x);
            ty = half3(ty.xy + n.xz, abs(ty.z) * n.y);
            tz = half3(tz.xy + n.xy, abs(tz.z) * n.z);
            wn = normalize(tx.zyx * w.x + ty.xzy * w.y + tz.xyz * w.z);
            #endif

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
