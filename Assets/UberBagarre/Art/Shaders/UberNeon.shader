// Tube de neon. Non eclaire, emissif, valeurs superieures a 1.
//
// Un materiau "Standard" avec une couleur vive plafonne a 1 : a l'ecran il rend
// comme du plastique peint, et le bloom n'a rien a faire deborder. Un neon doit
// sortir de la plage affichable — c'est cela, et rien d'autre, qui le fait lire
// comme une source de lumiere plutot que comme une surface coloree.
//
// _Intensity est pilote par MaterialPropertyBlock (voir NeonFlicker) pour que
// plusieurs enseignes partagent un seul materiau tout en clignotant chacune
// a son rythme.
Shader "UberBagarre/Neon"
{
    Properties
    {
        _Color ("Couleur", Color) = (1, 0.25, 0.6, 1)
        _Intensity ("Intensite", Float) = 6.0
        _CoreBoost ("Renfort du coeur", Range(0, 1)) = 0.55
        _RimPower ("Nettete du bord", Range(0.5, 8)) = 2.4
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" }
        LOD 100

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog
            #pragma target 3.0

            #include "UnityCG.cginc"

            fixed4 _Color;
            float _Intensity;
            float _CoreBoost;
            float _RimPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 viewDir : TEXCOORD1;
                UNITY_FOG_COORDS(2)
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.pos = UnityObjectToClipPos(v.vertex);
                o.worldNormal = UnityObjectToWorldNormal(v.normal);

                float3 worldPos = mul(unity_ObjectToWorld, v.vertex).xyz;
                o.viewDir = normalize(_WorldSpaceCameraPos - worldPos);

                UNITY_TRANSFER_FOG(o, o.pos);
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                // Face au tube, on voit le coeur du gaz : plus brillant. De profil,
                // on voit le verre : legerement plus sombre. Sans cette variation un
                // cylindre emissif rend comme un aplat decoupe.
                float facing = saturate(dot(normalize(i.worldNormal), normalize(i.viewDir)));
                float core = pow(facing, _RimPower);

                float3 color = _Color.rgb * _Intensity * (1.0 + _CoreBoost * core);

                fixed4 result = fixed4(color, 1.0);

                // La brume doit mordre les neons lointains, sinon l'enseigne du fond
                // est aussi nette que celle a trois metres et la profondeur disparait.
                UNITY_APPLY_FOG(i.fogCoord, result);
                return result;
            }
            ENDCG
        }
    }

    Fallback "Unlit/Color"
}
