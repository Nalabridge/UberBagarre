// Bruine : particules additives, sans aucune texture.
//
// Une particule etiree rendue avec un materiau uni est un rectangle plein, aux bords
// parfaitement nets : a l'ecran, cela ressemble a des traits de crayon blanc. La forme
// d'une goutte vient donc des coordonnees de texture — attenuation douce sur la largeur,
// disparition aux deux extremites — plutot que d'une image a fournir.
//
// Rendu en additif : une goutte n'occulte pas ce qu'il y a derriere, elle s'y ajoute.
// C'est ce qui lui donne son aspect de reflet plutot que de peinture, et c'est aussi ce
// qui la fait briller uniquement devant les zones sombres, comme une vraie pluie eclairee
// par les lampadaires.
Shader "UberBagarre/Rain"
{
    Properties
    {
        _Color ("Teinte", Color) = (0.75, 0.82, 1, 1)
        _Intensity ("Intensite", Float) = 1.0
        _Sharpness ("Nettete du trait", Range(0.5, 8)) = 2.4
    }

    SubShader
    {
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent" "IgnoreProjector" = "True" "PreviewType" = "Plane" }

        // Couleur premultipliee par alpha dans le fragment : additif pur.
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
            float _Sharpness;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = v.uv;
                o.color = v.color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Largeur : la goutte est un fil, pas une bande. La puissance concentre
                // la matiere au centre du quad.
                float across = 1.0 - abs(i.uv.x - 0.5) * 2.0;
                across = pow(saturate(across), _Sharpness);

                // Longueur : une goutte en mouvement est floue a ses deux bouts.
                float along = sin(saturate(i.uv.y) * 3.14159265);

                float alpha = across * along * i.color.a * _Color.a;

                return fixed4(_Color.rgb * i.color.rgb * _Intensity * alpha, alpha);
            }
            ENDCG
        }
    }

    Fallback Off
}
