using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Fait vivre une source lumineuse : neon, lampadaire, enseigne.
    ///
    /// Une lumiere parfaitement stable est le detail qui trahit le plus vite une scene
    /// generee. Dans la rue, rien n'est stable : un tube fluorescent bourdonne a 100 Hz,
    /// un neon fatigue s'eteint une demi-seconde puis repart en trois coups, une lampe a
    /// sodium respire lentement. Ce sont ces variations, pas la geometrie, qui font croire
    /// a un lieu.
    ///
    /// Le composant module ENSEMBLE l'intensite du materiau et celle de la lampe. C'est le
    /// point qui compte : un tube dont la couleur clignote sans que son halo bouge se lit
    /// immediatement comme une texture animee, pas comme une lumiere.
    ///
    /// L'intensite du materiau passe par un MaterialPropertyBlock : dix enseignes peuvent
    /// donc partager un seul materiau et clignoter chacune a son rythme, sans creer dix
    /// instances de materiau au chargement.
    /// </summary>
    [DisallowMultipleComponent]
    public class NeonFlicker : MonoBehaviour
    {
        public enum Pattern
        {
            /// <summary>Respiration lente, a peine perceptible. Pour une enseigne en bon etat.</summary>
            Calme = 0,

            /// <summary>Bourdonnement rapide et serre. Tube fluorescent de parking.</summary>
            Bourdonnement = 1,

            /// <summary>Coupures franches et redemarrages en saccade. Neon en fin de vie.</summary>
            Fatigue = 2,

            /// <summary>Pulsation nette et reguliere. Enseigne animee.</summary>
            Pulsation = 3
        }

        [Header("Cibles")]
        [SerializeField]
        [Tooltip("Rendus dont la propriete _Intensity est modulee. Vide = ceux de cet objet et de ses enfants.")]
        private Renderer[] _renderers;

        [SerializeField]
        [Tooltip("Lampes modulees en meme temps. Vide = celles de cet objet et de ses enfants.")]
        private Light[] _lights;

        [Header("Comportement")]
        [SerializeField] private Pattern _pattern = Pattern.Calme;

        [SerializeField, Min(0f)]
        [Tooltip("Intensite du materiau au repos. Au-dessus de 1 pour que le bloom la fasse deborder.")]
        private float _baseIntensity = 6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Amplitude de la variation, en fraction de l'intensite de repos.")]
        private float _amount = 0.12f;

        [SerializeField, Min(0.05f)] private float _speed = 1.4f;

        [SerializeField]
        [Tooltip("Decale la phase. Deux enseignes cote a cote avec la meme graine clignotent " +
                 "a l'unisson, ce qui se voit immediatement.")]
        private float _seed;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Facteur global, pilote par le cycle jour / nuit. 0 = eteint.")]
        private float _master = 1f;

        private MaterialPropertyBlock _block;
        private float[] _lightBaseIntensity;
        private float _current = 1f;

        private static readonly int IntensityId = Shader.PropertyToID("_Intensity");

        /// <summary>Extinction progressive au lever du jour (voir TimeOfDay).</summary>
        public float Master
        {
            get { return _master; }
            set { _master = Mathf.Clamp01(value); }
        }

        public float BaseIntensity
        {
            get { return _baseIntensity; }
            set { _baseIntensity = Mathf.Max(0f, value); }
        }

        private void Awake()
        {
            _block = new MaterialPropertyBlock();

            if (_renderers == null || _renderers.Length == 0)
            {
                _renderers = GetComponentsInChildren<Renderer>(true);
            }

            if (_lights == null || _lights.Length == 0)
            {
                _lights = GetComponentsInChildren<Light>(true);
            }

            if (_seed <= 0f) _seed = Mathf.Abs(transform.position.x * 3.1f + transform.position.z * 7.7f) % 97f;

            _lightBaseIntensity = new float[_lights.Length];
            for (int i = 0; i < _lights.Length; i++)
            {
                _lightBaseIntensity[i] = _lights[i] == null ? 0f : _lights[i].intensity;
            }
        }

        private void LateUpdate()
        {
            float target = Evaluate(Time.time * _speed + _seed);

            // Un lissage tres court : la coupure d'un neon est franche, mais le gaz met
            // quelques millisecondes a s'eteindre. Sans ce lissage, la variation se lit
            // comme un interrupteur numerique.
            _current = Mathf.Lerp(_current, target, 1f - Mathf.Exp(-28f * Time.deltaTime));

            float factor = _current * _master;
            Apply(factor);
        }

        private float Evaluate(float t)
        {
            switch (_pattern)
            {
                case Pattern.Bourdonnement:
                {
                    // Deux frequences proches : elles se dephasent lentement et produisent
                    // un battement, exactement ce qu'on entend et voit sur un tube use.
                    float a = Mathf.Sin(t * 17f);
                    float b = Mathf.Sin(t * 23.3f);
                    return 1f - _amount * (0.5f - 0.5f * (a * b));
                }

                case Pattern.Fatigue:
                {
                    // Long temps allume, puis une salve de coupures. Le seuil eleve rend
                    // l'evenement rare : un neon qui clignote en permanence devient un gag.
                    float slow = Mathf.PerlinNoise(t * 0.35f, _seed);
                    if (slow > 0.74f)
                    {
                        float stutter = Mathf.PerlinNoise(t * 26f, _seed + 11f);
                        return stutter > 0.45f ? 1f : 0.04f;
                    }

                    return 1f - _amount * Mathf.PerlinNoise(t * 3.1f, _seed + 5f);
                }

                case Pattern.Pulsation:
                {
                    float wave = 0.5f + 0.5f * Mathf.Sin(t * 2.4f);
                    return 1f - _amount * (1f - wave);
                }

                default:
                {
                    float noise = Mathf.PerlinNoise(t * 0.9f, _seed);
                    return 1f - _amount * noise;
                }
            }
        }

        private void Apply(float factor)
        {
            if (_renderers != null)
            {
                float intensity = _baseIntensity * factor;

                for (int i = 0; i < _renderers.Length; i++)
                {
                    Renderer renderer = _renderers[i];
                    if (renderer == null) continue;

                    renderer.GetPropertyBlock(_block);
                    _block.SetFloat(IntensityId, intensity);
                    renderer.SetPropertyBlock(_block);
                }
            }

            if (_lights == null || _lightBaseIntensity == null) return;

            for (int i = 0; i < _lights.Length && i < _lightBaseIntensity.Length; i++)
            {
                Light light = _lights[i];
                if (light == null) continue;

                light.intensity = _lightBaseIntensity[i] * factor;
            }
        }
    }
}
