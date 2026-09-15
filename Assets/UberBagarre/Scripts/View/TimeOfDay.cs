using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Cycle jour / nuit : un seul curseur pilote soleil, lune, ciel, brume, ambiante et
    /// eclairage artificiel.
    ///
    /// Pourquoi un seul curseur : une ambiance nocturne n'est pas « la meme scene, moins
    /// lumineuse ». Baisser uniquement l'intensite du soleil donne une image grise et sale.
    /// La nuit change SIX choses a la fois — la direction et la couleur de la source
    /// principale, la couleur du ciel, la densite et la teinte de la brume, la couleur de
    /// l'ambiante, et surtout le fait que l'eclairage passe du soleil aux lampes. Les
    /// regler separement, c'est garantir qu'un seul d'entre eux sera oublie.
    ///
    /// Le materiau de ciel est DUPLIQUE au demarrage : le modifier directement modifierait
    /// l'asset sur le disque, et la scene resterait figee sur la derniere heure testee.
    /// </summary>
    [ExecuteAlways]
    [DisallowMultipleComponent]
    public class TimeOfDay : MonoBehaviour
    {
        [Header("Heure")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("0 = nuit noire, 1 = plein jour. Les valeurs intermediaires passent par " +
                 "l'heure bleue et le crepuscule.")]
        private float _day;

        [Header("Sources")]
        [SerializeField] private Light _sun;
        [SerializeField] private Light _moon;

        [SerializeField]
        [Tooltip("Racine du decor. Tous les NeonFlicker qu'elle contient s'eteignent au lever du jour.")]
        private Transform _cityRoot;

        [Header("Soleil (jour)")]
        [SerializeField] private Vector3 _sunAngles = new Vector3(42f, -40f, 0f);
        [SerializeField, Min(0f)] private float _sunIntensity = 1.35f;
        [SerializeField] private Color _sunColor = new Color(1f, 0.89f, 0.72f);

        [Header("Lune (nuit)")]
        [SerializeField] private Vector3 _moonAngles = new Vector3(28f, 152f, 0f);
        [SerializeField, Min(0f)] private float _moonIntensity = 0.22f;
        [SerializeField] private Color _moonColor = new Color(0.52f, 0.63f, 0.95f);

        [Header("Ciel")]
        [SerializeField] private Material _skyMaterial;
        [SerializeField, Min(0f)] private float _skyExposureNight = 0.30f;
        [SerializeField, Min(0f)] private float _skyExposureDay = 1.2f;
        [SerializeField] private Color _skyTintNight = new Color(0.16f, 0.20f, 0.34f);
        [SerializeField] private Color _skyTintDay = new Color(0.58f, 0.68f, 0.86f);
        [SerializeField] private Color _skyGroundNight = new Color(0.05f, 0.05f, 0.08f);
        [SerializeField] private Color _skyGroundDay = new Color(0.30f, 0.27f, 0.23f);

        [Header("Brume")]
        [SerializeField] private Color _fogNight = new Color(0.055f, 0.062f, 0.095f);
        [SerializeField] private Color _fogDay = new Color(0.63f, 0.64f, 0.66f);

        [SerializeField, Min(0f)]
        [Tooltip("Brume exponentielle. La nuit, une brume dense est ce qui rend les halos " +
                 "des lampadaires visibles et donne sa profondeur a la rue.")]
        private float _fogDensityNight = 0.020f;

        [SerializeField, Min(0f)] private float _fogDensityDay = 0.006f;

        [Header("Ambiante")]
        [SerializeField, Min(0f)] private float _ambientNight = 0.26f;
        [SerializeField, Min(0f)] private float _ambientDay = 1.05f;
        [SerializeField] private Color _ambientSkyNight = new Color(0.10f, 0.13f, 0.22f);
        [SerializeField] private Color _ambientGroundNight = new Color(0.045f, 0.042f, 0.05f);

        [Header("Eclairage artificiel")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Valeur du curseur en dessous de laquelle les lampes sont a pleine puissance.")]
        private float _lightsOnBelow = 0.22f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Valeur du curseur au-dessus de laquelle elles sont completement eteintes.")]
        private float _lightsOffAbove = 0.55f;

        private NeonFlicker[] _artificial;
        private Material _skyInstance;
        private bool _collected;

        public float Day
        {
            get { return _day; }
            set
            {
                _day = Mathf.Clamp01(value);
                Apply();
            }
        }

        private void OnEnable()
        {
            Collect();
            Apply();
        }

        private void OnDisable()
        {
            if (_skyInstance == null) return;

            if (Application.isPlaying) Destroy(_skyInstance);
            else DestroyImmediate(_skyInstance);

            _skyInstance = null;
        }

        private void OnValidate()
        {
            if (!isActiveAndEnabled) return;

            _lightsOffAbove = Mathf.Max(_lightsOffAbove, _lightsOnBelow + 0.01f);

            // La racine du decor est souvent branchee APRES l'ajout du composant : sans cette
            // remise a zero, la liste des sources artificielles resterait celle d'avant le
            // branchement, c'est-a-dire vide, et le curseur d'heure n'eteindrait rien.
            _collected = false;

            Apply();
        }

        private void Collect()
        {
            if (_collected) return;

            _artificial = _cityRoot == null
                ? new NeonFlicker[0]
                : _cityRoot.GetComponentsInChildren<NeonFlicker>(true);

            _collected = true;
        }

        [ContextMenu("Appliquer maintenant")]
        public void Apply()
        {
            Collect();

            float t = _day;

            ApplySun(t);
            ApplyMoon(t);
            ApplySky(t);
            ApplyAtmosphere(t);
            ApplyArtificial(t);
        }

        private void ApplySun(float t)
        {
            if (_sun == null) return;

            _sun.transform.rotation = Quaternion.Euler(_sunAngles);
            _sun.color = _sunColor;

            // Le soleil ne s'eteint pas lineairement : il disparait vite sous l'horizon.
            // Une decroissance au carre evite une longue plage ou la scene est eclairee
            // par un soleil fantome en meme temps que par les lampadaires.
            _sun.intensity = _sunIntensity * t * t;
            _sun.enabled = _sun.intensity > 0.002f;
        }

        private void ApplyMoon(float t)
        {
            if (_moon == null) return;

            _moon.transform.rotation = Quaternion.Euler(_moonAngles);
            _moon.color = _moonColor;
            _moon.intensity = _moonIntensity * (1f - t);
            _moon.enabled = _moon.intensity > 0.002f;
        }

        private void ApplySky(float t)
        {
            if (_skyMaterial == null) return;

            Material target = SkyTarget();
            if (target == null) return;

            SetFloat(target, "_Exposure", Mathf.Lerp(_skyExposureNight, _skyExposureDay, t));
            SetColor(target, "_SkyTint", Color.Lerp(_skyTintNight, _skyTintDay, t));
            SetColor(target, "_GroundColor", Color.Lerp(_skyGroundNight, _skyGroundDay, t));

            // Une atmosphere epaisse la nuit etale la lueur urbaine sur l'horizon au lieu
            // de laisser un noir uniforme : c'est ce qui donne a un ciel de ville sa teinte.
            SetFloat(target, "_AtmosphereThickness", Mathf.Lerp(1.9f, 1.35f, t));

            RenderSettings.skybox = target;
            RenderSettings.sun = t > 0.35f ? _sun : _moon;
        }

        /// <summary>
        /// Le materiau de ciel a modifier.
        ///
        /// En JEU, c'est une copie : ecrire dans l'asset le laisserait fige sur la derniere
        /// heure testee, y compris apres avoir quitte le mode Play.
        ///
        /// En EDITION, c'est l'asset lui-meme, et il ne faut surtout pas faire autrement.
        /// Une copie porte HideFlags.DontSave : la scene enregistrerait alors une reference
        /// de ciel vers un objet qui n'existe plus au rechargement, et rouvrir le projet
        /// donnerait un fond noir sans la moindre erreur pour l'expliquer.
        /// </summary>
        private Material SkyTarget()
        {
            if (!Application.isPlaying) return _skyMaterial;

            if (_skyInstance == null)
            {
                _skyInstance = new Material(_skyMaterial);
                _skyInstance.name = _skyMaterial.name + " (instance)";
                _skyInstance.hideFlags = HideFlags.HideAndDontSave;
            }

            return _skyInstance;
        }

        private void ApplyAtmosphere(float t)
        {
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.ExponentialSquared;
            RenderSettings.fogColor = Color.Lerp(_fogNight, _fogDay, t);
            RenderSettings.fogDensity = Mathf.Lerp(_fogDensityNight, _fogDensityDay, t);

            if (RenderSettings.skybox != null)
            {
                RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Skybox;
                RenderSettings.ambientIntensity = Mathf.Lerp(_ambientNight, _ambientDay, t);
                return;
            }

            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Color.Lerp(_ambientSkyNight, new Color(0.46f, 0.50f, 0.60f), t);
            RenderSettings.ambientEquatorColor = Color.Lerp(_ambientSkyNight * 0.7f, new Color(0.46f, 0.40f, 0.33f), t);
            RenderSettings.ambientGroundColor = Color.Lerp(_ambientGroundNight, new Color(0.22f, 0.19f, 0.16f), t);
        }

        private void ApplyArtificial(float t)
        {
            if (_artificial == null) return;

            float master = 1f - Mathf.InverseLerp(_lightsOnBelow, _lightsOffAbove, t);
            master = Mathf.Clamp01(master);

            for (int i = 0; i < _artificial.Length; i++)
            {
                if (_artificial[i] == null) continue;
                _artificial[i].Master = master;
            }
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }
    }
}
