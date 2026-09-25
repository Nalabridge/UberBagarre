using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Une lumière qui bat au rythme de la musique du club : dalles de piste, projecteurs
    /// colorés, écran derrière le DJ.
    ///
    /// Le tempo vient de <see cref="ClubMusic"/>, c'est-à-dire de la position de lecture réelle
    /// du son : la lumière tombe sur la grosse caisse, pas à côté. Sans musique, elle reste
    /// allumée à mi-puissance au lieu de s'éteindre — une salle muette n'est pas une salle noire.
    ///
    /// La couleur peut tourner dans une palette, une case par mesure : c'est ce qui donne à une
    /// piste de danse son mouvement, bien plus que l'intensité seule.
    /// </summary>
    [DisallowMultipleComponent]
    public class BeatLight : MonoBehaviour
    {
        [Header("Cibles")]
        [SerializeField]
        [Tooltip("Rendus a moduler (propriete _Intensity des neons). Vide = ceux de cet objet et de ses enfants.")]
        private Renderer[] _renderers;

        [SerializeField]
        [Tooltip("Lampes a moduler. Vide = celles de cet objet et de ses enfants.")]
        private Light[] _lights;

        [Header("Rythme")]
        [SerializeField, Min(0f)] private float _baseIntensity = 6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Niveau entre deux temps, en fraction du maximum.")]
        private float _floor = 0.25f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Decalage dans le temps : 0,5 = sur le contretemps.")]
        private float _offset;

        [SerializeField, Min(1)]
        [Tooltip("Ne s'allume qu'un temps sur N.")]
        private int _everyBeats = 1;

        [SerializeField]
        [Tooltip("Couleurs parcourues, une par mesure. Vide = couleur d'origine conservee.")]
        private Color[] _palette = new Color[0];

        [SerializeField, Min(0)]
        [Tooltip("Decale la palette : deux dalles voisines ne prennent pas la meme couleur.")]
        private int _paletteOffset;

        private MaterialPropertyBlock _block;
        private float[] _lightBase;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");
        private static readonly int ColorId = Shader.PropertyToID("_Color");

        private void Awake()
        {
            _block = new MaterialPropertyBlock();

            if (_renderers == null || _renderers.Length == 0) _renderers = GetComponentsInChildren<Renderer>(true);
            if (_lights == null || _lights.Length == 0) _lights = GetComponentsInChildren<Light>(true);

            _lightBase = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) _lightBase[i] = _lights[i] != null ? _lights[i].intensity : 0f;
        }

        private void LateUpdate()
        {
            float beats = ClubMusic.GlobalBeats - _offset;
            int beat = Mathf.FloorToInt(beats);
            float phase = beats - beat;

            float pulse = ClubMusic.Current != null ? Mathf.Exp(-phase * 6f) : 0.5f;
            if (_everyBeats > 1 && ((beat % _everyBeats) + _everyBeats) % _everyBeats != 0) pulse *= 0.15f;

            float factor = Mathf.Lerp(_floor, 1f, pulse);

            bool recolor = _palette != null && _palette.Length > 0;
            Color color = Color.white;
            if (recolor)
            {
                int index = ((beat / 4 + _paletteOffset) % _palette.Length + _palette.Length) % _palette.Length;
                color = _palette[index];
            }

            for (int i = 0; i < _renderers.Length; i++)
            {
                Renderer renderer = _renderers[i];
                if (renderer == null) continue;

                renderer.GetPropertyBlock(_block);
                _block.SetFloat(IntensityId, _baseIntensity * factor);
                if (recolor) _block.SetColor(ColorId, color);
                renderer.SetPropertyBlock(_block);
            }

            for (int i = 0; i < _lights.Length; i++)
            {
                Light light = _lights[i];
                if (light == null) continue;

                light.intensity = _lightBase[i] * factor;
                if (recolor) light.color = color;
            }
        }
    }
}
