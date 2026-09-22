using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Point d'entree unique des reglages graphiques.
    ///
    /// Pourquoi une facade plutot que laisser le menu parler a chaque effet : il y a DEUX
    /// cameras dans la scene (jeu et observation), chacune avec sa chaine de post-traitement.
    /// Un menu qui reglerait « la » camera n'en reglerait qu'une, et le curseur de bloom
    /// donnerait un resultat different selon la touche F3 — un comportement impossible a
    /// diagnostiquer pour qui joue. Toutes les cibles sont donc cablees ici, et un reglage
    /// s'applique a toutes a la fois.
    ///
    /// Les valeurs sont sauvegardees : regler l'ambiance est un travail de patience, et le
    /// perdre a chaque retour dans l'editeur rend ce travail impossible.
    /// </summary>
    [DisallowMultipleComponent]
    public class GraphicsDirector : MonoBehaviour
    {
        private const string PrefsPrefix = "UberBagarre.Gfx.";

        public enum Preset
        {
            Sobre = 0,
            Cinema = 1,
            Batard = 2
        }

        [Header("Cibles")]
        [SerializeField] private UberPostProcess[] _post;
        [SerializeField] private PlanarReflection[] _reflections;
        [SerializeField] private TimeOfDay _timeOfDay;

        [Header("Valeurs de depart")]
        [SerializeField, Min(0f)] private float _bloom = 1.15f;
        [SerializeField, Min(0f)] private float _threshold = 1.05f;
        [SerializeField, Range(0.1f, 4f)] private float _exposure = 1f;
        [SerializeField, Range(0f, 2f)] private float _saturation = 1.06f;
        [SerializeField, Range(0.5f, 2f)] private float _contrast = 1.08f;
        [SerializeField, Range(0f, 1f)] private float _vignette = 0.42f;
        [SerializeField, Range(0f, 0.5f)] private float _grain = 0.055f;
        [SerializeField, Range(0f, 4f)] private float _aberration = 0.55f;
        [SerializeField] private bool _postEnabled = true;
        [SerializeField] private bool _reflectionsEnabled = true;
        [SerializeField, Range(1, 8)] private int _reflectionDownsample = 2;
        [SerializeField, Range(0f, 1f)] private float _day;

        [Header("Persistance")]
        [SerializeField]
        [Tooltip("Recharger les reglages sauvegardes au demarrage.")]
        private bool _restoreSaved = true;

        [SerializeField]
        [Tooltip("L'heure fait-elle partie des reglages rechargeables ? Dans un bac a sable, oui : " +
                 "c'est un reglage de confort. Dans une scene d'histoire, non — une planque a " +
                 "2 h du matin en plein soleil parce qu'on avait pousse le curseur la veille " +
                 "n'a aucun sens, et rien a l'ecran n'expliquerait pourquoi.")]
        private bool _restoreDay = true;

        public float Bloom { get { return _bloom; } set { _bloom = Mathf.Max(0f, value); Push(); } }
        public float Threshold { get { return _threshold; } set { _threshold = Mathf.Max(0f, value); Push(); } }
        public float Exposure { get { return _exposure; } set { _exposure = Mathf.Clamp(value, 0.1f, 4f); Push(); } }
        public float Saturation { get { return _saturation; } set { _saturation = Mathf.Clamp(value, 0f, 2f); Push(); } }
        public float Contrast { get { return _contrast; } set { _contrast = Mathf.Clamp(value, 0.5f, 2f); Push(); } }
        public float Vignette { get { return _vignette; } set { _vignette = Mathf.Clamp01(value); Push(); } }
        public float Grain { get { return _grain; } set { _grain = Mathf.Clamp(value, 0f, 0.5f); Push(); } }
        public float Aberration { get { return _aberration; } set { _aberration = Mathf.Clamp(value, 0f, 4f); Push(); } }
        public bool PostEnabled { get { return _postEnabled; } set { _postEnabled = value; Push(); } }
        public bool ReflectionsEnabled { get { return _reflectionsEnabled; } set { _reflectionsEnabled = value; Push(); } }

        public int ReflectionDownsample
        {
            get { return _reflectionDownsample; }
            set { _reflectionDownsample = Mathf.Clamp(value, 1, 8); Push(); }
        }

        public float Day { get { return _day; } set { _day = Mathf.Clamp01(value); Push(); } }

        private void Start()
        {
            if (_restoreSaved) Load();
            Push();
        }

        /// <summary>Applique les valeurs courantes a toutes les cibles cablees.</summary>
        public void Push()
        {
            if (_post != null)
            {
                for (int i = 0; i < _post.Length; i++)
                {
                    UberPostProcess post = _post[i];
                    if (post == null) continue;

                    post.Enabled = _postEnabled;
                    post.BloomIntensity = _bloom;
                    post.Threshold = _threshold;
                    post.Exposure = _exposure;
                    post.Saturation = _saturation;
                    post.Contrast = _contrast;
                    post.Vignette = _vignette;
                    post.Grain = _grain;
                    post.Aberration = _aberration;
                }
            }

            if (_reflections != null)
            {
                for (int i = 0; i < _reflections.Length; i++)
                {
                    PlanarReflection reflection = _reflections[i];
                    if (reflection == null) continue;

                    reflection.Enabled = _reflectionsEnabled;
                    reflection.Downsample = _reflectionDownsample;
                }
            }

            if (_timeOfDay != null) _timeOfDay.Day = _day;
        }

        /// <summary>
        /// Trois ambiances pretes a l'emploi. « Batard » pousse volontairement au-dela du
        /// raisonnable : c'est un reglage de demonstration, pas une reference.
        /// </summary>
        public void ApplyPreset(Preset preset)
        {
            switch (preset)
            {
                case Preset.Sobre:
                    _bloom = 0.55f;
                    _threshold = 1.25f;
                    _exposure = 1f;
                    _saturation = 1f;
                    _contrast = 1.02f;
                    _vignette = 0.22f;
                    _grain = 0.02f;
                    _aberration = 0.15f;
                    break;

                case Preset.Batard:
                    _bloom = 2.6f;
                    _threshold = 0.72f;
                    _exposure = 1.18f;
                    _saturation = 1.28f;
                    _contrast = 1.16f;
                    _vignette = 0.58f;
                    _grain = 0.075f;
                    _aberration = 1.35f;
                    break;

                default:
                    _bloom = 1.15f;
                    _threshold = 1.05f;
                    _exposure = 1f;
                    _saturation = 1.06f;
                    _contrast = 1.08f;
                    _vignette = 0.42f;
                    _grain = 0.055f;
                    _aberration = 0.55f;
                    break;
            }

            Push();
            Save();
        }

        public void Save()
        {
            PlayerPrefs.SetFloat(PrefsPrefix + "bloom", _bloom);
            PlayerPrefs.SetFloat(PrefsPrefix + "seuil", _threshold);
            PlayerPrefs.SetFloat(PrefsPrefix + "expo", _exposure);
            PlayerPrefs.SetFloat(PrefsPrefix + "satu", _saturation);
            PlayerPrefs.SetFloat(PrefsPrefix + "contraste", _contrast);
            PlayerPrefs.SetFloat(PrefsPrefix + "vignette", _vignette);
            PlayerPrefs.SetFloat(PrefsPrefix + "grain", _grain);
            PlayerPrefs.SetFloat(PrefsPrefix + "aberration", _aberration);
            PlayerPrefs.SetFloat(PrefsPrefix + "jour", _day);
            PlayerPrefs.SetInt(PrefsPrefix + "post", _postEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PrefsPrefix + "reflets", _reflectionsEnabled ? 1 : 0);
            PlayerPrefs.SetInt(PrefsPrefix + "refletsQualite", _reflectionDownsample);
            PlayerPrefs.Save();
        }

        public void Load()
        {
            _bloom = PlayerPrefs.GetFloat(PrefsPrefix + "bloom", _bloom);
            _threshold = PlayerPrefs.GetFloat(PrefsPrefix + "seuil", _threshold);
            _exposure = PlayerPrefs.GetFloat(PrefsPrefix + "expo", _exposure);
            _saturation = PlayerPrefs.GetFloat(PrefsPrefix + "satu", _saturation);
            _contrast = PlayerPrefs.GetFloat(PrefsPrefix + "contraste", _contrast);
            _vignette = PlayerPrefs.GetFloat(PrefsPrefix + "vignette", _vignette);
            _grain = PlayerPrefs.GetFloat(PrefsPrefix + "grain", _grain);
            _aberration = PlayerPrefs.GetFloat(PrefsPrefix + "aberration", _aberration);
            if (_restoreDay) _day = PlayerPrefs.GetFloat(PrefsPrefix + "jour", _day);
            _postEnabled = PlayerPrefs.GetInt(PrefsPrefix + "post", _postEnabled ? 1 : 0) != 0;
            _reflectionsEnabled = PlayerPrefs.GetInt(PrefsPrefix + "reflets", _reflectionsEnabled ? 1 : 0) != 0;
            _reflectionDownsample = PlayerPrefs.GetInt(PrefsPrefix + "refletsQualite", _reflectionDownsample);
        }

        public static void ClearSaved()
        {
            string[] keys =
            {
                "bloom", "seuil", "expo", "satu", "contraste", "vignette", "grain",
                "aberration", "jour", "post", "reflets", "refletsQualite"
            };

            for (int i = 0; i < keys.Length; i++) PlayerPrefs.DeleteKey(PrefsPrefix + keys[i]);
            PlayerPrefs.Save();
        }
    }
}
