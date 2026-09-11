using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Déplacement du joueur : marche, course, gravité, saut, accroupissement, glissade.
    /// NE s'occupe ni de la visée (PlayerLook) ni du combat.
    ///
    /// Points d'extension déjà en place pour la suite :
    /// - SpeedMultiplier : le combat le baissera pendant un coup ("on ne court pas en frappant") ;
    /// - InputLocked     : les états touché / KO couperont le déplacement sans toucher à ce script ;
    /// - ApplyImpulse    : esquive et recul des coups passeront par là.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    public class PlayerMotor : MonoBehaviour, ISpawnReceiver
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;

        [SerializeField]
        [Tooltip("La tete. S'accroupir abaisse physiquement les yeux : c'est le meme fait que la hauteur de capsule.")]
        private Transform _eyeTransform;

        [Header("Vitesses (m/s)")]
        [SerializeField, Min(0f)] private float _walkSpeed = 3.1f;
        [SerializeField, Min(1f)] private float _sprintMultiplier = 1.75f;
        [SerializeField, Range(0.1f, 1f)] private float _crouchSpeedMultiplier = 0.48f;
        [SerializeField, Range(0.1f, 1f)] private float _backwardMultiplier = 0.78f;
        [SerializeField, Range(0.1f, 1f)] private float _strafeMultiplier = 0.9f;

        [SerializeField]
        [Tooltip("Le sprint ne s'applique que si on avance. Evite de sprinter en reculant ou en pas chasse.")]
        private bool _sprintRequiresForwardInput = true;

        [Header("Reactivite")]
        [SerializeField, Min(0.1f)] private float _acceleration = 22f;
        [SerializeField, Min(0.1f)] private float _deceleration = 28f;
        [SerializeField, Range(0f, 1f)] private float _airControl = 0.25f;

        [Header("Gravite et saut")]
        [SerializeField] private bool _allowJump = true;
        [SerializeField, Min(0f)] private float _jumpHeight = 0.85f;
        [SerializeField] private float _gravity = -23f;
        [SerializeField] private float _groundStickVelocity = -2f;

        [Header("Hauteurs de capsule (metres)")]
        [SerializeField, Min(0.5f)] private float _standHeight = 1.8f;
        [SerializeField, Min(0.4f)] private float _crouchHeight = 1.18f;
        [SerializeField, Min(0.3f)] private float _slideHeight = 0.95f;

        [SerializeField, Min(0f)]
        [Tooltip("Distance entre le sommet de la capsule et les yeux.")]
        private float _eyeOffsetFromTop = 0.18f;

        [SerializeField, Min(0.5f)] private float _heightTransitionSpeed = 7f;

        [Header("Glissade")]
        [SerializeField] private bool _slideEnabled = true;

        [SerializeField, Min(0f)]
        [Tooltip("Vitesse au declenchement. Doit depasser la vitesse de course pour valoir le coup.")]
        private float _slideStartSpeed = 7.4f;

        [SerializeField, Min(0.1f)] private float _slideMaxDuration = 0.9f;
        [SerializeField, Min(0f)] private float _slideCooldown = 0.6f;
        [SerializeField, Min(0f)] private float _slideFriction = 5.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Vitesse minimale requise pour declencher une glissade.")]
        private float _slideMinEntrySpeed = 3.6f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part de controle directionnel conserve pendant la glissade.")]
        private float _slideSteering = 0.18f;

        [Header("Impulsions externes (esquive, recul)")]
        [SerializeField, Min(0f)] private float _externalVelocityDamping = 9f;

        private CharacterController _controller;
        private Vector3 _horizontalVelocity;
        private Vector3 _externalVelocity;
        private float _verticalVelocity;

        private Vector3 _slideDirection;
        private float _slideTimer;
        private float _slideCooldownTimer;
        private float _currentHeight;

        public float SpeedMultiplier { get; set; }
        public bool InputLocked { get; set; }

        public bool IsGrounded { get; private set; }
        public bool IsSprinting { get; private set; }
        public bool IsCrouching { get; private set; }
        public bool IsSliding { get; private set; }

        /// <summary>0 = debout, 1 = complètement accroupi. Lissé, destiné à l'animation.</summary>
        public float CrouchAmount { get; private set; }

        /// <summary>0 = pas de glissade, 1 = en pleine glissade. Lissé.</summary>
        public float SlideAmount { get; private set; }

        public Vector3 HorizontalVelocity { get { return _horizontalVelocity; } }
        public float CurrentSpeed { get { return _horizontalVelocity.magnitude; } }
        public CharacterController Controller { get { return _controller; } }

        public float NormalizedSpeed
        {
            get { return _walkSpeed <= 0f ? 0f : Mathf.Clamp01(_horizontalVelocity.magnitude / _walkSpeed); }
        }

        private void Awake()
        {
            SpeedMultiplier = 1f;
            _controller = GetComponent<CharacterController>();
            _currentHeight = _standHeight;

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

            UpdateSlideState(moveInput, dt);
            UpdateCrouchState(dt);

            IsSprinting = EvaluateSprint(moveInput);

            if (IsSliding) UpdateSlideVelocity(moveInput, dt);
            else UpdateHorizontalVelocity(moveInput, dt);

            UpdateVerticalVelocity(dt);
            UpdateHeight(dt);

            _externalVelocity = Vector3.MoveTowards(_externalVelocity, Vector3.zero, _externalVelocityDamping * dt);

            Vector3 motion = (_horizontalVelocity + _externalVelocity) * dt;
            motion.y += _verticalVelocity * dt;
            _controller.Move(motion);
        }

        // ------------------------------------------------------------------ course

        private bool EvaluateSprint(Vector2 moveInput)
        {
            if (_input == null || InputLocked || IsSliding || IsCrouching) return false;
            if (!_input.SprintHeld || !IsGrounded) return false;
            if (moveInput.sqrMagnitude < 0.01f) return false;

            return !_sprintRequiresForwardInput || moveInput.y > 0.35f;
        }

        // ------------------------------------------------------------------ accroupi / glissade

        private void UpdateSlideState(Vector2 moveInput, float dt)
        {
            if (_slideCooldownTimer > 0f) _slideCooldownTimer -= dt;

            if (IsSliding)
            {
                _slideTimer -= dt;

                bool tooSlow = _horizontalVelocity.magnitude < _walkSpeed * 0.55f;
                if (_slideTimer <= 0f || tooSlow || !IsGrounded) EndSlide();
            }
            else if (_slideEnabled && _input != null && !InputLocked && _input.CrouchPressed &&
                     IsGrounded && _slideCooldownTimer <= 0f &&
                     _horizontalVelocity.magnitude >= _slideMinEntrySpeed && moveInput.y > 0.3f)
            {
                StartSlide();
            }
        }

        private void StartSlide()
        {
            IsSliding = true;
            _slideTimer = _slideMaxDuration;

            Vector3 direction = _horizontalVelocity.sqrMagnitude > 0.01f ? _horizontalVelocity.normalized : transform.forward;
            _slideDirection = direction;
            _horizontalVelocity = direction * Mathf.Max(_slideStartSpeed, _horizontalVelocity.magnitude);
        }

        private void EndSlide()
        {
            IsSliding = false;
            _slideCooldownTimer = _slideCooldown;
        }

        private void UpdateSlideVelocity(Vector2 moveInput, float dt)
        {
            // Une glissade conserve son élan et le perd par frottement. On garde juste assez
            // de contrôle pour corriger sa trajectoire, pas pour tourner à angle droit.
            float speed = Mathf.Max(0f, _horizontalVelocity.magnitude - _slideFriction * dt);

            Vector3 steer = transform.right * (moveInput.x * _slideSteering);
            _slideDirection = (_slideDirection + steer * dt * 4f).normalized;
            _horizontalVelocity = _slideDirection * speed;
        }

        private void UpdateCrouchState(float dt)
        {
            bool wantsCrouch = _input != null && !InputLocked && _input.CrouchHeld;

            if (IsSliding)
            {
                IsCrouching = false;
            }
            else if (wantsCrouch)
            {
                IsCrouching = true;
            }
            else if (IsCrouching && CanStandUp())
            {
                IsCrouching = false;
            }

            float crouchTarget = IsCrouching ? 1f : 0f;
            CrouchAmount = Mathf.MoveTowards(CrouchAmount, crouchTarget, _heightTransitionSpeed * dt);
            SlideAmount = Mathf.MoveTowards(SlideAmount, IsSliding ? 1f : 0f, _heightTransitionSpeed * 1.4f * dt);
        }

        /// <summary>
        /// Vérifie qu'il y a la place de se relever.
        /// On ignore les colliders du joueur lui-même plutôt que d'utiliser un masque de calque :
        /// le projet n'impose aucun calque particulier, il reste donc utilisable tel quel.
        /// </summary>
        private bool CanStandUp()
        {
            float radius = _controller.radius * 0.95f;
            Vector3 bottom = transform.position + Vector3.up * radius;
            Vector3 top = transform.position + Vector3.up * (_standHeight - radius);

            Collider[] overlaps = Physics.OverlapCapsule(bottom, top, radius, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (!overlaps[i].transform.IsChildOf(transform)) return false;
            }

            return true;
        }

        private void UpdateHeight(float dt)
        {
            float targetHeight = _standHeight;
            if (IsSliding) targetHeight = _slideHeight;
            else if (IsCrouching) targetHeight = _crouchHeight;

            _currentHeight = Mathf.MoveTowards(_currentHeight, targetHeight, _heightTransitionSpeed * dt);

            _controller.height = _currentHeight;
            _controller.center = new Vector3(0f, _currentHeight * 0.5f, 0f);

            if (_eyeTransform != null)
            {
                Vector3 local = _eyeTransform.localPosition;
                local.y = Mathf.Max(0.2f, _currentHeight - _eyeOffsetFromTop);
                _eyeTransform.localPosition = local;
            }
        }

        // ------------------------------------------------------------------ déplacement

        private void UpdateHorizontalVelocity(Vector2 moveInput, float dt)
        {
            Vector3 localWish = new Vector3(
                moveInput.x * _strafeMultiplier,
                0f,
                moveInput.y * (moveInput.y < 0f ? _backwardMultiplier : 1f));

            localWish = Vector3.ClampMagnitude(localWish, 1f);

            float targetSpeed = _walkSpeed * Mathf.Max(0f, SpeedMultiplier);
            if (IsSprinting) targetSpeed *= _sprintMultiplier;
            if (IsCrouching) targetSpeed *= _crouchSpeedMultiplier;

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

            bool wantsJump = _allowJump && !InputLocked && !IsSliding && !IsCrouching &&
                             _input != null && _input.JumpPressed;

            if (wantsJump && IsGrounded)
            {
                _verticalVelocity = Mathf.Sqrt(2f * Mathf.Abs(_gravity) * _jumpHeight);
            }

            _verticalVelocity += _gravity * dt;
        }

        public void ApplyImpulse(Vector3 velocity)
        {
            _externalVelocity += velocity;
        }

        public void ResetVelocity()
        {
            _horizontalVelocity = Vector3.zero;
            _externalVelocity = Vector3.zero;
            _verticalVelocity = 0f;
        }

        public void OnSpawned(Vector3 position, Quaternion rotation)
        {
            bool wasEnabled = _controller.enabled;
            _controller.enabled = false;
            transform.SetPositionAndRotation(position, rotation);
            _controller.enabled = wasEnabled;
            ResetVelocity();
        }
    }
}
