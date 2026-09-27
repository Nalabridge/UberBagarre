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
