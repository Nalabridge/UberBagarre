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

        [Header("Amplitude")]
        [SerializeField, Min(0f)] private float _frequency = 8.5f;
        [SerializeField, Min(0f)] private float _verticalAmplitude = 0.032f;
        [SerializeField, Min(0f)] private float _horizontalAmplitude = 0.022f;
        [SerializeField, Min(0f)] private float _rollAmplitude = 0.7f;

        [Header("Lissage")]
        [SerializeField, Min(0.1f)] private float _blendSpeed = 9f;

        private Vector3 _basePosition;
        private float _phase;
        private float _currentWeight;

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

            if (_currentWeight <= 0.0001f)
            {
                transform.localPosition = _basePosition;
                transform.localRotation = Quaternion.identity;
                _phase = 0f;
                return;
            }

            _phase += Time.deltaTime * _frequency * Mathf.Max(0.35f, _currentWeight);

            // Le pas vertical va deux fois plus vite que le balancement latéral :
            // un cycle de marche = deux appuis au sol, mais un seul aller-retour du bassin.
            float vertical = Mathf.Sin(_phase * 2f) * _verticalAmplitude * _currentWeight;
            float horizontal = Mathf.Sin(_phase) * _horizontalAmplitude * _currentWeight;
            float roll = -Mathf.Sin(_phase) * _rollAmplitude * _currentWeight;

            transform.localPosition = _basePosition + new Vector3(horizontal, vertical, 0f);
            transform.localRotation = Quaternion.Euler(0f, 0f, roll);
        }
    }
}
