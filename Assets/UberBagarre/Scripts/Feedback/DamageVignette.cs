using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Halo rouge sur les bords de l'écran quand le joueur encaisse.
    ///
    /// En première personne, le joueur ne se voit pas : sans ce retour, prendre un coup ne se
    /// remarque qu'en regardant la barre de vie, donc trop tard. La vignette porte
    /// l'information dans la vision périphérique, là où elle ne gêne pas la visée.
    ///
    /// Le dégradé est généré par code : aucun asset, et aucune dépendance au post-processing,
    /// donc ça marche dans les trois render pipelines.
    /// </summary>
    public class DamageVignette : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HealthSystem _health;

        [Header("Apparence")]
        [SerializeField] private Color _color = new Color(0.62f, 0.03f, 0.03f);
        [SerializeField, Range(0f, 1f)] private float _maxIntensity = 0.8f;

        [SerializeField, Range(0.3f, 0.95f)]
        [Tooltip("Rayon du trou central. Plus haut = vignette plus fine sur les bords.")]
        private float _innerRadius = 0.42f;

        [Header("Temps (secondes)")]
        [SerializeField, Min(0.01f)] private float _fadeIn = 0.05f;
        [SerializeField, Min(0f)] private float _hold = 0.09f;
        [SerializeField, Min(0.05f)] private float _fadeOut = 0.55f;

        [Header("Vie basse")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Sous ce niveau de vie, une vignette permanente pulse doucement.")]
        private float _lowHealthThreshold = 0.3f;

        [SerializeField, Range(0f, 1f)] private float _lowHealthIntensity = 0.3f;
        [SerializeField, Min(0.1f)] private float _lowHealthPulseSpeed = 2.2f;

        [Header("Activation")]
        [SerializeField] private bool _enabled = true;

        private Texture2D _texture;
        private float _current;
        private float _target;
        private float _holdTimer;

        public bool VignetteEnabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        private void Awake()
        {
            _texture = BuildRadialTexture(128, _innerRadius);
        }

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDestroy()
        {
            if (_texture != null) Destroy(_texture);
        }

        private void OnDamaged(DamageInfo info)
        {
            // L'intensite suit la severite du coup : une pichenette ne doit pas aveugler.
            float severity = Mathf.Clamp01(info.Amount / 25f);
            _target = Mathf.Max(_target, Mathf.Lerp(_maxIntensity * 0.45f, _maxIntensity, severity));
            _holdTimer = _hold;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_holdTimer > 0f)
            {
                _holdTimer -= dt;
                _current = Mathf.MoveTowards(_current, _target, dt / Mathf.Max(0.01f, _fadeIn));
            }
            else
            {
                _target = 0f;
                _current = Mathf.MoveTowards(_current, 0f, dt / Mathf.Max(0.05f, _fadeOut));
            }
        }

        private void OnGUI()
        {
            if (!_enabled || _texture == null) return;

            float intensity = _current;

            if (_health != null && _health.IsAlive && _health.Normalized <= _lowHealthThreshold)
            {
                float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * _lowHealthPulseSpeed);
                float lowHealth = Mathf.InverseLerp(_lowHealthThreshold, 0f, _health.Normalized);
                intensity = Mathf.Max(intensity, _lowHealthIntensity * lowHealth * pulse);
            }

            if (intensity <= 0.002f) return;

            Color previous = GUI.color;
            GUI.color = new Color(_color.r, _color.g, _color.b, Mathf.Clamp01(intensity));
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), _texture);
            GUI.color = previous;
        }

        /// <summary>Dégradé radial : transparent au centre, opaque sur les bords.</summary>
        private static Texture2D BuildRadialTexture(int size, float innerRadius)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Color[] pixels = new Color[size * size];
            Vector2 centre = new Vector2(size * 0.5f, size * 0.5f);
            float maxDistance = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float distance = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) / maxDistance;
                    float alpha = Mathf.InverseLerp(innerRadius, 1f, distance);

                    // Mise au carre : la transition centre-bord est plus douce, moins "cercle net".
                    alpha *= alpha;

                    pixels[y * size + x] = new Color(1f, 1f, 1f, Mathf.Clamp01(alpha));
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }
    }
}
