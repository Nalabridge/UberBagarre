using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Oscillation de tête à la marche. Volontairement discrète : le jeu vise un rendu crédible,
    /// pas un effet de caméra voyant.
    ///
    /// Ce composant vit sur son PROPRE transform ("CameraBob") et n'écrit que dessus.
    /// C'est la règle de tout le rig caméra : un nœud = un effet. Camera shake, recul d'attaque
    /// et head bob ne se battent donc jamais pour la même valeur.
    /// </summary>
    public class HeadBob : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerMotor _motor;

        [Header("Activation")]
        [SerializeField] private bool _enabledBob = true;

        [Header("Amplitude a la marche")]
        [SerializeField, Min(0f)] private float _frequency = 8f;
        [SerializeField, Min(0f)] private float _verticalAmplitude = 0.020f;
        [SerializeField, Min(0f)] private float _horizontalAmplitude = 0.014f;
        [SerializeField, Min(0f)] private float _rollAmplitude = 0.5f;

        [Header("Renfort au sprint")]
        [SerializeField, Min(1f)] private float _sprintAmplitudeMultiplier = 1.4f;
        [SerializeField, Min(1f)] private float _sprintFrequencyMultiplier = 1.25f;

        [Header("Lissage")]
        [SerializeField, Min(0.1f)] private float _blendSpeed = 9f;

        private Vector3 _basePosition;
        private float _phase;
        private float _currentWeight;
        private float _sprintBlend;

        public bool BobEnabled
        {
            get { return _enabledBob; }
            set { _enabledBob = value; }
        }

        private void Awake()
        {
            _basePosition = transform.localPosition;
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
        }

        private void LateUpdate()
        {
            float targetWeight = 0f;
            if (_enabledBob && _motor != null && _motor.IsGrounded)
            {
                targetWeight = _motor.NormalizedSpeed;
            }

            _currentWeight = Mathf.MoveTowards(_currentWeight, targetWeight, _blendSpeed * Time.deltaTime);

            // Le passage marche <-> sprint est interpolé : un changement brutal d'amplitude se verrait.
            float sprintTarget = (_motor != null && _motor.IsSprinting) ? 1f : 0f;
            _sprintBlend = Mathf.MoveTowards(_sprintBlend, sprintTarget, _blendSpeed * Time.deltaTime);

            if (_currentWeight <= 0.0001f)
            {
                transform.localPosition = _basePosition;
                transform.localRotation = Quaternion.identity;
                _phase = 0f;
                return;
            }

            float amplitudeScale = Mathf.Lerp(1f, _sprintAmplitudeMultiplier, _sprintBlend);
            float frequencyScale = Mathf.Lerp(1f, _sprintFrequencyMultiplier, _sprintBlend);

            _phase += Time.deltaTime * _frequency * frequencyScale * Mathf.Max(0.35f, _currentWeight);

            // Le pas vertical va deux fois plus vite que le balancement latéral :
            // un cycle de marche = deux appuis au sol, mais un seul aller-retour du bassin.
            float weight = _currentWeight * amplitudeScale;
            float vertical = Mathf.Sin(_phase * 2f) * _verticalAmplitude * weight;
            float horizontal = Mathf.Sin(_phase) * _horizontalAmplitude * weight;
            float roll = -Mathf.Sin(_phase) * _rollAmplitude * weight;

            transform.localPosition = _basePosition + new Vector3(horizontal, vertical, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }
    }
}
