// Über Bagarre — l'eau de la carte (port, étangs, mer) : deux houles de normales qui glissent
// l'une sur l'autre, reflet du ciel, plus opaque en rasant.
Shader "UberBagarre/Carte/Eau"
{
    Properties
    {
        _Color ("Eau vue de haut", Color) = (0.03, 0.08, 0.09, 0.78)
        _HorizonColor ("Eau en rasant", Color) = (0.05, 0.10, 0.13, 1)
        [Normal] _BumpMap ("Vagues", 2D) = "bump" {}
        _BumpScale ("Force des vagues", Float) = 0.6
        _Tiling ("Repetitions par metre", Float) = 0.08
        _Speed ("Vitesse", Float) = 0.6
        _Glossiness ("Lissage", Range(0, 1)) = 0.93
    }

    SubShader
    {
        Tags { "Queue" = "Transparent-10" "RenderType" = "Transparent" "IgnoreProjector" = "True" }
        LOD 300
        ZWrite Off

        CGPROGRAM
        #pragma surface surf Standard alpha:premul
        #pragma target 3.0

        sampler2D _BumpMap;
        fixed4 _Color;
        fixed4 _HorizonColor;
        half _BumpScale;
        float _Tiling;
        float _Speed;
        half _Glossiness;

        struct Input
        {
            float3 worldPos;
            float3 viewDir;
        };

        void surf(Input IN, inout SurfaceOutputStandard o)
        {
            float2 p = IN.worldPos.xz * _Tiling;
            float t = _Time.x * _Speed;
            half3 a = UnpackScaleNormal(tex2D(_BumpMap, p + float2(t, t * 0.37)), _BumpScale);
            half3 b = UnpackScaleNormal(tex2D(_BumpMap, p * 1.73 + float2(-t * 0.41, t)), _BumpScale);
            o.Normal = normalize(half3(a.xy + b.xy, a.z * b.z));

            half fresnel = pow(1 - saturate(dot(normalize(IN.viewDir), o.Normal)), 4);
            o.Albedo = lerp(_Color.rgb, _HorizonColor.rgb, fresnel);
            o.Alpha = lerp(_Color.a, 1, fresnel);
            o.Smoothness = _Glossiness;
            o.Metallic = 0;
        }
        ENDCG
    }

    FallBack "Legacy Shaders/Transparent/Diffuse"
}
