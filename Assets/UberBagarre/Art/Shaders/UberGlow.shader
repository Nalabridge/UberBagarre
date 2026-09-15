// Halo et cone de lumiere : geometrie additive, bords adoucis par l'angle de vue.
//
// A quoi ca sert : une lampe ponctuelle Unity eclaire les surfaces, mais l'air
// entre la lampe et le sol reste parfaitement transparent. Dans la realite, une
// nuit humide diffuse la lumiere dans la brume, et c'est ce cone visible qui dit
// "il fait nuit, il y a de l'humidite" bien plus que la couleur de l'eclairage.
//
// Rendu en additif, sans ecriture de profondeur : plusieurs cones se traversent
// sans se decouper entre eux. La disparition sur les bords vient du produit
// scalaire vue/normale — un cone dont la silhouette est nette se lit comme un
// cone en plastique, pas comme de la lumiere.
Shader "UberBagarre/Glow"
{
    Properties
    {
        _Color ("Couleur", Color) = (1, 0.8, 0.5, 1)
        _Intensity ("Intensite", Float) = 1.0
        _EdgeSoftness ("Douceur du bord", Range(0.2, 8)) = 2.2
        _TopFade ("Fondu vers le bas", Range(0, 1)) = 0.75
        _Flip ("Inverser le fondu", Range(0, 1)) = 0
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent+10" "IgnoreProjector" = "True" }

        // La couleur sortante est DEJA multipliee par alpha : un Blend SrcAlpha One la
        // multiplierait une seconde fois et le halo disparaitrait presque entierement.
        Blend One One
        ZWrite Off
        Cull Off
        Lighting Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _EdgeSoftness;
            float _TopFade;
            float _Flip;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                float2 uv : TEXCOORD2;
                float height : TEXCOORD3;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                o.uv = v.uv;
                // Position le long de l'axe local Y, ramenee dans [0,1] pour un
                // cone ou un cylindre Unity (qui vont de -0,5 a +0,5).
                o.height = saturate(v.vertex.y + 0.5);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                float facing = saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));

                // Le bord du volume est celui ou la normale est perpendiculaire a la
                // vue : c'est la qu'on doit disparaitre, exactement l'inverse d'un
                // eclairage classique.
                float silhouette = pow(1.0 - facing, _EdgeSoftness);
                float alpha = 1.0 - silhouette;

                float along = lerp(i.height, 1.0 - i.height, _Flip);
                alpha *= lerp(1.0, along, _TopFade);

                alpha = saturate(alpha) * _Color.a;

                return fixed4(_Color.rgb * _Intensity * alpha, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
