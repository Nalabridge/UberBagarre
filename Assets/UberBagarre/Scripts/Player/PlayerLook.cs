using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Visée première personne : le lacet (gauche/droite) tourne le corps du joueur,
    /// le tangage (haut/bas) tourne uniquement la tête.
    ///
    /// Séparer les deux transforms est important : le déplacement suit le corps,
    /// donc regarder le sol ne doit pas faire marcher le joueur vers le sol.
    /// </summary>
    public class PlayerLook : MonoBehaviour, ISpawnReceiver
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;

        [SerializeField]
        [Tooltip("Transform tourne horizontalement : la racine du joueur.")]
        private Transform _yawTransform;

        [SerializeField]
        [Tooltip("Transform tourne verticalement : la tete.")]
        private Transform _pitchTransform;

        [Header("Sensibilite")]
        [SerializeField, Min(0.01f)] private float _sensitivity = 2.2f;
        [SerializeField] private bool _invertVertical;

        [Header("Limites")]
        [SerializeField, Range(-89f, 0f)] private float _minPitch = -85f;
        [SerializeField, Range(0f, 89f)] private float _maxPitch = 85f;

        [Header("Lissage")]
        [SerializeField, Range(0f, 0.2f)]
        [Tooltip("0 = brut (le plus reactif). 0.02-0.05 = lisse sans etre mou.")]
        private float _smoothingTime = 0.02f;

        private Vector2 _smoothedDelta;

        /// <summary>Désactive la visée sans désactiver le composant (état touché, menu, curseur libéré...).</summary>
        public bool LookEnabled { get; set; }

        /// <summary>
        /// Multiplicateur de sensibilité temporaire. L'appareil photo zoomé le baisse : à x4, la
        /// même main qui balaie la rue ferait sauter le cadre d'un bout à l'autre.
        /// </summary>
        public float SensitivityScale { get; set; }

        public float Yaw { get; private set; }
        public float Pitch { get; private set; }

        private void Awake()
        {
            LookEnabled = true;
            SensitivityScale = 1f;

            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_yawTransform == null) _yawTransform = transform;

            if (_pitchTransform == null)
            {
                Debug.LogError("[UberBagarre] PlayerLook sur " + name + " : 'Pitch Transform' (la tete) n'est pas assigne.", this);
            }

            Yaw = _yawTransform.eulerAngles.y;
            Pitch = 0f;
            ApplyRotations();
        }

        private void Update()
        {
            if (!LookEnabled || _input == null) return;

            Vector2 rawDelta = _input.LookDelta * (_sensitivity * Mathf.Clamp(SensitivityScale, 0.05f, 4f));

            if (_smoothingTime > 0.0001f)
            {
                // Lissage exponentiel indépendant du framerate.
                // unscaledDeltaTime : la visée doit rester nette même pendant un ralenti d'impact.
                float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime / _smoothingTime);
                _smoothedDelta = Vector2.Lerp(_smoothedDelta, rawDelta, t);
            }
            else
            {
                _smoothedDelta = rawDelta;
            }

            Yaw += _smoothedDelta.x;
            Pitch += _invertVertical ? _smoothedDelta.y : -_smoothedDelta.y;
            Pitch = Mathf.Clamp(Pitch, _minPitch, _maxPitch);

            ApplyRotations();
        }

        private void ApplyRotations()
        {
            if (_yawTransform != null)
            {
                _yawTransform.localRotation = Quaternion.Euler(0f, Yaw, 0f);
            }

            if (_pitchTransform != null)
            {
                _pitchTransform.localRotation = Quaternion.Euler(Pitch, 0f, 0f);
            }
        }

        /// <summary>Force les angles de visée (spawn, scripts de test, cinématique).</summary>
        public void SetLookAngles(float yaw, float pitch)
        {
            Yaw = yaw;
            Pitch = Mathf.Clamp(pitch, _minPitch, _maxPitch);
            _smoothedDelta = Vector2.zero;
            ApplyRotations();
        }

        public void OnSpawned(Vector3 position, Quaternion rotation)
        {
            SetLookAngles(rotation.eulerAngles.y, 0f);
        }
    }
}
