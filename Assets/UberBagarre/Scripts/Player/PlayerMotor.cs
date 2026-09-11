using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Déplacement du joueur (sol, gravité, saut, inertie).
    /// NE s'occupe PAS de la visée (voir PlayerLook) ni du combat.
    ///
    /// Points d'extension prévus pour la suite :
    /// - SpeedMultiplier : le combat le baissera pendant un coup ("on ne court pas en frappant").
    /// - InputLocked : l'état "touché" ou "KO" coupera le déplacement sans toucher à ce script.
    /// - ApplyImpulse : l'esquive et le recul des coups passeront par là.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour, ISpawnReceiver
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;

        [Header("Vitesses (m/s)")]
        [SerializeField, Min(0f)] private float _walkSpeed = 3.1f;
        [SerializeField, Min(1f)] private float _sprintMultiplier = 1.75f;

        [SerializeField]
        [Tooltip("Le sprint ne s'applique que si on avance. Evite de sprinter en reculant ou en pas chasse.")]
        private bool _sprintRequiresForwardInput = true;
        [SerializeField, Range(0.1f, 1f)] private float _backwardMultiplier = 0.78f;
        [SerializeField, Range(0.1f, 1f)] private float _strafeMultiplier = 0.9f;

        [Header("Reactivite")]
        [SerializeField, Min(0.1f)] private float _acceleration = 22f;
        [SerializeField, Min(0.1f)] private float _deceleration = 28f;
        [SerializeField, Range(0f, 1f)] private float _airControl = 0.25f;

        [Header("Gravite et saut")]
        [SerializeField] private bool _allowJump = true;
        [SerializeField, Min(0f)] private float _jumpHeight = 0.85f;
        [SerializeField] private float _gravity = -23f;
        [SerializeField]
        [Tooltip("Petite vitesse verticale maintenue au sol pour que isGrounded reste fiable.")]
        private float _groundStickVelocity = -2f;

        [Header("Impulsions externes (esquive, recul)")]
        [SerializeField, Min(0f)] private float _externalVelocityDamping = 9f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private Vector3 _externalVelocity;
        private float _verticalVelocity;

        /// <summary>Multiplicateur global de vitesse. 1 = normal. Baissé pendant une attaque, 0 = immobile.</summary>
        public float SpeedMultiplier { get; set; }

        /// <summary>Quand true, les entrées de déplacement sont ignorées (gravité et impulsions continuent).</summary>
        public bool InputLocked { get; set; }

        public bool IsGrounded { get; private set; }

        /// <summary>Vrai uniquement quand le sprint s'applique réellement (au sol, en mouvement, vers l'avant).</summary>
        public bool IsSprinting { get; private set; }
        public Vector3 HorizontalVelocity { get { return _horizontalVelocity; } }
        public float CurrentSpeed { get { return _horizontalVelocity.magnitude; } }

        /// <summary>Vitesse actuelle ramenée sur 0..1 par rapport à la vitesse de marche. Utile pour le head bob.</summary>
        public float NormalizedSpeed
        {
            get { return _walkSpeed <= 0f ? 0f : Mathf.Clamp01(_horizontalVelocity.magnitude / _walkSpeed); }
        }

        public CharacterController Controller { get { return _controller; } }

        private void Awake()
        {
            SpeedMultiplier = 1f;
            _controller = GetComponent<CharacterController>();

            if (_input == null)
            {
                _input = GetComponentInParent<PlayerInputReader>();
                if (_input == null)
                {
                    Debug.LogError("[UberBagarre] PlayerMotor sur " + name + " n'a pas de PlayerInputReader : le joueur ne bougera pas.", this);
                }
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            IsGrounded = _controller.isGrounded;

            Vector2 moveInput = (_input != null && !InputLocked) ? _input.Move : Vector2.zero;
            IsSprinting = EvaluateSprint(moveInput);

            UpdateHorizontalVelocity(moveInput, IsSprinting, dt);
            UpdateVerticalVelocity(dt);

            _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, _externalVelocityDamping * dt);

            Vector3 motion = (_horizontalVelocity + _externalVelocity) * dt;
            motion.y += _verticalVelocity * dt;
            _controller.Move(motion);
        }

        private bool EvaluateSprint(Vector2 moveInput)
        {
            if (_input == null || InputLocked || !_input.SprintHeld) return false;
            if (!IsGrounded || moveInput.sqrMagnitude < 0.01f) return false;

            // Sprinter en marche arrière ou en pas chassé n'a pas de sens et casse la lisibilité du combat.
            return !_sprintRequiresForwardInput || moveInput.y > 0.35f;
        }

        private void UpdateHorizontalVelocity(Vector2 moveInput, bool sprinting, float dt)
        {
            // On pondère avant/arrière/côté séparément : reculer ou se déplacer latéralement
            // doit être plus lent, c'est ce qui donne une impression de corps et pas de caméra volante.
            Vector3 localWish = new Vector3(
                moveInput.x * _strafeMultiplier,
                0f,
                moveInput.y * (moveInput.y < 0f ? _backwardMultiplier : 1f));

            localWish = Vector3.ClampMagnitude(localWish, 1f);

            float targetSpeed = _walkSpeed * Mathf.Max(0f, SpeedMultiplier) * (sprinting ? _sprintMultiplier : 1f);
            Vector3 targetVelocity = transform.TransformDirection(localWish) * targetSpeed;

            bool accelerating = targetVelocity.sqrMagnitude > _horizontalVelocity.sqrMagnitude;
            float rate = accelerating ? _acceleration : _deceleration;
            if (!IsGrounded) rate *= _airControl;

            _horizontalVelocity = Vector3.MoveTowards(_horizontalVelocity, targetVelocity, rate * dt);
        }

        private void UpdateVerticalVelocity(float dt)
        {
            if (IsGrounded && _verticalVelocity < 0f)
            {
                _verticalVelocity = _groundStickVelocity;
            }

            bool wantsJump = _allowJump && !InputLocked && _input != null && _input.JumpPressed;
            if (wantsJump && IsGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(_gravity) * _jumpHeight);
            }

            _verticalVelocity += _gravity * dt;
        }

        /// <summary>Ajoute une impulsion amortie (esquive, recul d'un coup reçu).</summary>
        public void ApplyImpulse(Vector3 velocity)
        {
            _externalVelocity += velocity;
        }

        /// <summary>Coupe net l'inertie (entrée en état KO, respawn...).</summary>
        public void ResetVelocity()
        {
            _horizontalVelocity = Vector3.zero;
            _externalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        public void OnSpawned(Vector3 position, Quaternion rotation)
        {
            // Un CharacterController actif écrase les écritures directes de transform.position :
            // on le désactive le temps de la téléportation.
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;
            ResetVelocity();
        }
    }
}
