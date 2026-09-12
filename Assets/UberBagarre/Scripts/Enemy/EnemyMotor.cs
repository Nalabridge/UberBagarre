using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Enemy
{
    /// <summary>
    /// Déplacement d'un ennemi. Volontairement simple et sans navigation :
    /// dans une arène de combat rapproché, un ennemi avance, recule et tourne autour de sa
    /// cible. Un système de pathfinding n'apporterait rien ici et masquerait les réglages
    /// de distance qu'on veut pouvoir ajuster au décimètre.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class EnemyMotor : MonoBehaviour, ISpawnReceiver, IImpulseReceiver
    {
        [Header("Deplacement")]
        [SerializeField, Min(0f)] private float _moveSpeed = 2.5f;
        [SerializeField, Min(0.1f)] private float _acceleration = 14f;
        [SerializeField, Min(0.1f)] private float _deceleration = 18f;
        [SerializeField, Min(10f)] private float _turnSpeed = 520f;

        [Header("Gravite")]
        [SerializeField] private float _gravity = -22f;
        [SerializeField] private float _groundStickVelocity = -2f;

        [Header("Impulsions")]
        [SerializeField, Min(0f)] private float _externalVelocityDamping = 8f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private Vector3 _externalVelocity;
        private Vector3 _desiredDirection;
        private float _desiredSpeedScale;
        private float _verticalVelocity;

        public bool IsGrounded { get; private set; }
        public Vector3 HorizontalVelocity { get { return _horizontalVelocity; } }

        /// <summary>0 à l'arrêt, 1 à pleine vitesse. Alimente l'animation de marche.</summary>
        public float NormalizedSpeed
        {
            get { return _moveSpeed <= 0f ? 0f : Mathf.Clamp01(_horizontalVelocity.magnitude / _moveSpeed); }
        }

        /// <summary>Multiplicateur global : le combat le baisse pendant un coup.</summary>
        public float SpeedMultiplier { get; set; }

        public bool MovementLocked { get; set; }

        private void Awake()
        {
            SpeedMultiplier = 1f;
            _controller = GetComponent<CharacterController>();
        }

        /// <summary>Direction monde souhaitée, et part de la vitesse maximale à utiliser.</summary>
        public void SetMoveIntent(Vector3 worldDirection, float speedScale)
        {
            worldDirection.y = 0f;
            _desiredDirection = worldDirection.sqrMagnitude > 0.0001f ? worldDirection.normalized : Vector3.zero;
            _desiredSpeedScale = Mathf.Clamp01(speedScale);
        }

        /// <summary>Oriente progressivement l'ennemi vers un point.</summary>
        public void FaceTowards(Vector3 worldPoint)
        {
            Vector3 delta = worldPoint - transform.position;
            delta.y = 0f;
            if (delta.sqrMagnitude < 0.0001f) return;

            Quaternion target = Quaternion.LookRotation(delta.normalized, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _turnSpeed * Time.deltaTime);
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            IsGrounded = _controller.isGrounded;

            Vector3 target = MovementLocked
                ? Vector3.zero
                : _desiredDirection * (_moveSpeed * _desiredSpeedScale * Mathf.Max(0f, SpeedMultiplier));

            bool accelerating = target.sqrMagnitude > _horizontalVelocity.sqrMagnitude;
            float rate = accelerating ? _acceleration : _deceleration;
            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, target, rate * dt);

            if (IsGrounded && _verticalVelocity < 0f) _verticalVelocity = _groundStickVelocity;
            _verticalVelocity += _gravity * dt;

            _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, _externalVelocityDamping * dt);

            Vector3 motion = (_horizontalVelocity + _externalVelocity) * dt;
            motion.y += _verticalVelocity * dt;
            _controller.Move(motion);

            // L'intention est consommee : sans ordre, l'ennemi s'arrete de lui-meme.
            _desiredDirection = Vector3.zero;
            _desiredSpeedScale = 0f;
        }

        public void ApplyImpulse(Vector3 velocity)
        {
            _externalVelocity += velocity;
        }

        public void OnSpawned(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;

            _horizontalVelocity = Vector3.zero;
            _externalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }
    }
}
