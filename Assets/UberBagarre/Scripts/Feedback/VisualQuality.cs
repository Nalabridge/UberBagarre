using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Réglages de rendu appliqués au lancement.
    ///
    /// Un projet Unity neuf démarre avec un anticrénelage désactivé, des ombres courtes et un
    /// filtrage de textures minimal. Ce n'est pas la géométrie qui fait « vieux jeu » dans ce
    /// cas, ce sont les bords en escalier et l'absence de profondeur : la même scène avec MSAA,
    /// des ombres portées franches et un peu de brume atmosphérique change complètement.
    ///
    /// Appliqué par code plutôt que dans les Quality Settings du projet : le générateur de scène
    /// ne touche ainsi à aucun réglage global, et le résultat est identique pour qui ouvre le
    /// projet sans rien configurer.
    /// </summary>
    public class VisualQuality : MonoBehaviour
    {
        [Header("Anticrenelage")]
        [SerializeField]
        [Tooltip("MSAA. 0 = desactive, 2/4/8 = nombre d'echantillons. 8 sur un PC recent.")]
        private int _antiAliasing = 8;

        [Header("Textures")]
        [SerializeField] private AnisotropicFiltering _anisotropicFiltering = AnisotropicFiltering.ForceEnable;

        [Header("Ombres")]
        [SerializeField] private ShadowQuality _shadowQuality = ShadowQuality.All;
        [SerializeField] private UnityEngine.ShadowResolution _shadowResolution = UnityEngine.ShadowResolution.VeryHigh;
        [SerializeField, Min(10f)] private float _shadowDistance = 70f;
        [SerializeField, Range(0, 4)] private int _shadowCascades = 4;

        [Header("Brume")]
        [SerializeField]
        [Tooltip("Une brume legere donne de la profondeur : sans elle, un decor proche et un " +
                 "decor lointain ont exactement le meme contraste, et la scene parait plate.")]
        private bool _enableFog = true;

        [SerializeField] private Color _fogColor = new Color(0.66f, 0.62f, 0.57f);
        [SerializeField, Min(1f)] private float _fogStart = 22f;
        [SerializeField, Min(2f)] private float _fogEnd = 95f;

        [Header("Ambiance")]
        [SerializeField]
        [Tooltip("Lumiere ambiante : evite des ombres d'un noir mort.")]
        private bool _overrideAmbient = true;

        [SerializeField, Min(0f)]
        [Tooltip("Intensite de l'ambiante quand elle vient du ciel.")]
        private float _ambientIntensity = 1.05f;

        [Tooltip("Utilises seulement s'il n'y a PAS de ciel : trois teintes valent mieux qu'un gris uni.")]
        [SerializeField] private Color _ambientSky = new Color(0.46f, 0.50f, 0.60f);
        [SerializeField] private Color _ambientEquator = new Color(0.46f, 0.40f, 0.33f);
        [SerializeField] private Color _ambientGround = new Color(0.22f, 0.19f, 0.16f);

        private void Awake()
        {
            Apply();
        }

        [ContextMenu("Appliquer maintenant")]
        public void Apply()
        {
            QualitySettings.antiAliasing = Mathf.Max(0, _antiAliasing);
            QualitySettings.anisotropicFiltering = _anisotropicFiltering;

            QualitySettings.shadows = _shadowQuality;
            QualitySettings.shadowResolution = _shadowResolution;
            QualitySettings.shadowDistance = _shadowDistance;
            QualitySettings.shadowCascades = _shadowCascades;

            QualitySettings.softParticles = true;
            QualitySettings.realtimeReflectionProbes = true;

            if (_enableFog)
            {
                RenderSettings.fog = true;
                RenderSettings.fogMode = FogMode.Linear;
                RenderSettings.fogColor = _fogColor;
                RenderSettings.fogStartDistance = _fogStart;
                RenderSettings.fogEndDistance = Mathf.Max(_fogStart + 1f, _fogEnd);
            }

            if (!_overrideAmbient) return;

            // Quand la scene a un ciel, c'est LUI qui doit fournir l'ambiante : les couleurs
            // suivent alors automatiquement le soleil, et regler l'un regle l'autre. Trois
            // teintes fixes ne servent que de repli, quand il n'y a pas de ciel du tout.
            if (RenderSettings.skybox != null)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = _ambientIntensity;
                return;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = _ambientSky;
            RenderSettings.ambientEquatorColor = _ambientEquator;
            RenderSettings.ambientGroundColor = _ambientGround;
        }
    }
}
