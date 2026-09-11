using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Compose la pose des deux poings et la transmet aux bras du corps.
    ///
    /// Les épaules appartiennent au VRAI corps (elles sont sur le buste), mais la cible des
    /// poings est calculée dans un repère de visée séparé. C'est le compromis classique du FPS :
    /// les bras restent solidaires d'un torse qui marche, respire et tourne, tout en gardant
    /// la garde bien cadrée à l'écran. L'IK relie les deux sans effort.
    ///
    /// Empilement des couches :
    ///   1. POSE DE BASE      garde → garde serrée → course (mélange par poids)
    ///   2. COUCHES ADDITIVES respiration + bruit organique + inertie de visée
    ///                        + balancement issu du cycle de marche
    ///   3. LISSAGE           donne du poids
    ///   4. COUCHE D'ATTAQUE  appliquée APRÈS le lissage, pour ne pas émousser un jab
    ///
    /// Aucune touche n'est lue ici : l'ennemi réutilisera ce composant, piloté par son IA.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class FirstPersonHands : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Repere dans lequel les poses sont exprimees (l'ancrage de visee).")]
        private Transform _poseSpace;

        [SerializeField] private IkLimb _leftArm;
        [SerializeField] private IkLimb _rightArm;
        [SerializeField] private HandRig _leftHand;
        [SerializeField] private HandRig _rightHand;

        [SerializeField]
        [Tooltip("Optionnel : fournit le balancement des bras synchronise avec les jambes.")]
        private ProceduralLocomotion _locomotion;

        [Header("Garde normale (espace de visee)")]
        [SerializeField] private HandPose _leftGuardPose = new HandPose(new Vector3(-0.17f, -0.16f, 0.27f), new Vector3(-6f, 22f, 6f));
        [SerializeField] private HandPose _rightGuardPose = new HandPose(new Vector3(0.16f, -0.19f, 0.21f), new Vector3(-4f, -20f, -8f));

        [Header("Garde serree (clic droit)")]
        [SerializeField] private HandPose _leftTightGuardPose = new HandPose(new Vector3(-0.11f, -0.08f, 0.22f), new Vector3(-12f, 30f, 10f));
        [SerializeField] private HandPose _rightTightGuardPose = new HandPose(new Vector3(0.11f, -0.08f, 0.21f), new Vector3(-12f, -30f, -10f));

        [Header("Course")]
        [SerializeField] private HandPose _leftSprintPose = new HandPose(new Vector3(-0.20f, -0.28f, 0.15f), new Vector3(14f, 26f, 4f));
        [SerializeField] private HandPose _rightSprintPose = new HandPose(new Vector3(0.20f, -0.28f, 0.15f), new Vector3(14f, -26f, -4f));

        [Header("Fermeture des mains")]
        [SerializeField, Range(0f, 1f)] private float _guardGrip = 1f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("On ne court pas les poings serres : les mains se relachent.")]
        private float _sprintGrip = 0.3f;

        [Header("Respiration")]
        [SerializeField, Min(0f)] private float _breathAmplitude = 0.007f;
        [SerializeField, Min(0f)] private float _breathRotationAmplitude = 1.3f;
        [SerializeField, Min(0f)] private float _breathFrequency = 0.28f;

        [Header("Micro-mouvement")]
        [SerializeField, Min(0f)] private float _idleNoiseAmplitude = 0.005f;
        [SerializeField, Min(0f)] private float _idleNoiseFrequency = 0.55f;

        [Header("Inertie de visee")]
        [SerializeField, Min(0f)] private float _swayAmount = 0.02f;
        [SerializeField, Min(0f)] private float _swayRotationAmount = 22f;
        [SerializeField, Min(0f)] private float _swayMaxOffset = 0.06f;
        [SerializeField, Min(0.5f)] private float _swayResponse = 9f;

        [Header("Deplacement")]
        [SerializeField] private float _strafeSwayAmount = 0.02f;
        [SerializeField, Min(0f)]
        [Tooltip("Conversion du balancement de bras en mouvement vertical accompagnant.")]
        private float _armSwingLift = 0.22f;

        [Header("Lissage")]
        [SerializeField, Min(0.5f)] private float _poseResponse = 13f;

        [Header("Debug")]
        [SerializeField] private bool _drawPoseGizmos;

        private HandPose _smoothedLeft;
        private HandPose _smoothedRight;

        private Vector2 _lookRate;
        private Vector2 _swayOffset;
        private Vector3 _localVelocity;

        private float _breathPhase;
        private float _noiseTime;

        private HandPose _leftAttackPose;
        private HandPose _rightAttackPose;
        private float _leftAttackWeight;
        private float _rightAttackWeight;
        private float _leftAttackGrip = -1f;
        private float _rightAttackGrip = -1f;

        public float GuardWeight { get; set; }
        public float SprintWeight { get; set; }

        private void Awake()
        {
            if (_poseSpace == null) _poseSpace = transform;
            _smoothedLeft = _leftGuardPose;
            _smoothedRight = _rightGuardPose;
        }

        public void SetLookDelta(Vector2 delta, float deltaTime)
        {
            _lookRate = delta / Mathf.Max(deltaTime, 0.0001f) * 0.01f;
        }

        public void SetLocalVelocity(Vector3 localVelocity)
        {
            _localVelocity = localVelocity;
        }

        /// <summary>Pose imposée par une attaque. grip &lt; 0 = laisser la fermeture par défaut.</summary>
        public void SetAttackPose(HandSide side, HandPose pose, float weight, float grip)
        {
            weight = Mathf.Clamp01(weight);

            if (side == HandSide.Left)
            {
                _leftAttackPose = pose;
                _leftAttackWeight = weight;
                _leftAttackGrip = grip;
            }
            else
            {
                _rightAttackPose = pose;
                _rightAttackWeight = weight;
                _rightAttackGrip = grip;
            }
        }

        public void ClearAttackPose(HandSide side)
        {
            if (side == HandSide.Left)
            {
                _leftAttackWeight = 0f;
                _leftAttackGrip = -1f;
            }
            else
            {
                _rightAttackWeight = 0f;
                _rightAttackGrip = -1f;
            }
        }

        public HandPose GetRestPose(HandSide side)
        {
            return side == HandSide.Left ? _smoothedLeft : _smoothedRight;
        }

        public HandPose GetGuardPose(HandSide side)
        {
            return side == HandSide.Left ? _leftGuardPose : _rightGuardPose;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _breathPhase += dt * _breathFrequency * Mathf.PI * 2f;
            if (_breathPhase > Mathf.PI * 2f) _breathPhase -= Mathf.PI * 2f;
            _noiseTime += dt;

            UpdateSway(dt);

            float t = 1f - Mathf.Exp(-_poseResponse * dt);
            _smoothedLeft = HandPose.Lerp(_smoothedLeft, ComposeIdlePose(HandSide.Left), t);
            _smoothedRight = HandPose.Lerp(_smoothedRight, ComposeIdlePose(HandSide.Right), t);

            ApplyHand(HandSide.Left, _leftArm, _leftHand, _smoothedLeft, _leftAttackPose, _leftAttackWeight, _leftAttackGrip);
            ApplyHand(HandSide.Right, _rightArm, _rightHand, _smoothedRight, _rightAttackPose, _rightAttackWeight, _rightAttackGrip);
        }

        private void UpdateSway(float dt)
        {
            Vector2 target = new Vector2(-_lookRate.x, -_lookRate.y) * _swayAmount;
            target.x = Mathf.Clamp(target.x, -_swayMaxOffset, _swayMaxOffset);
            target.y = Mathf.Clamp(target.y, -_swayMaxOffset, _swayMaxOffset);

            _swayOffset = Vector2.Lerp(_swayOffset, target, 1f - Mathf.Exp(-_swayResponse * dt));
        }

        private HandPose ComposeIdlePose(HandSide side)
        {
            bool isLeft = side == HandSide.Left;

            HandPose basePose = HandPose.Lerp(
                isLeft ? _leftGuardPose : _rightGuardPose,
                isLeft ? _leftTightGuardPose : _rightTightGuardPose,
                Mathf.Clamp01(GuardWeight));

            basePose = HandPose.Lerp(basePose, isLeft ? _leftSprintPose : _rightSprintPose, Mathf.Clamp01(SprintWeight));

            return basePose + Breathing(isLeft) + IdleNoise(isLeft) + Sway() + ArmSwing(side);
        }

        private HandPose Breathing(bool isLeft)
        {
            float wave = Mathf.Sin(_breathPhase + (isLeft ? 0f : 0.6f));

            return new HandPose(
                new Vector3(0f, wave * _breathAmplitude, wave * _breathAmplitude * 0.35f),
                new Vector3(-wave * _breathRotationAmplitude, 0f, 0f));
        }

        private HandPose IdleNoise(bool isLeft)
        {
            float seed = isLeft ? 0f : 37.4f;
            float x = (Mathf.PerlinNoise(_noiseTime * _idleNoiseFrequency, seed) - 0.5f) * 2f;
            float y = (Mathf.PerlinNoise(seed, _noiseTime * _idleNoiseFrequency) - 0.5f) * 2f;
            float z = (Mathf.PerlinNoise(_noiseTime * _idleNoiseFrequency * 0.7f, seed + 11f) - 0.5f) * 2f;

            return new HandPose(
                new Vector3(x, y, z) * _idleNoiseAmplitude,
                new Vector3(y, x, 0f) * _idleNoiseAmplitude * 120f);
        }

        private HandPose Sway()
        {
            return new HandPose(
                new Vector3(_swayOffset.x, _swayOffset.y, 0f),
                new Vector3(-_swayOffset.y * _swayRotationAmount, _swayOffset.x * _swayRotationAmount,
                    _swayOffset.x * _swayRotationAmount * 0.6f));
        }

        /// <summary>
        /// Balancement des bras. La valeur vient du CYCLE DE MARCHE, pas d'un sinus indépendant :
        /// le bras avance donc exactement quand la jambe opposée avance. C'est ce qui manquait
        /// avant, quand les mains montaient et descendaient sans rapport avec les pas.
        /// </summary>
        private HandPose ArmSwing(HandSide side)
        {
            float swing = _locomotion != null ? _locomotion.ArmSwing(side) : 0f;

            return new HandPose(
                new Vector3(
                    -_localVelocity.x * _strafeSwayAmount,
                    -Mathf.Abs(swing) * _armSwingLift,
                    swing),
                new Vector3(-swing * 40f, 0f, 0f));
        }

        private void ApplyHand(HandSide side, IkLimb arm, HandRig hand, HandPose restPose,
            HandPose attackPose, float attackWeight, float attackGrip)
        {
            if (arm == null) return;

            HandPose finalPose = attackWeight > 0f ? HandPose.Lerp(restPose, attackPose, attackWeight) : restPose;

            Vector3 worldPosition = _poseSpace.TransformPoint(finalPose.position);
            Quaternion worldRotation = _poseSpace.rotation * finalPose.Rotation;
            arm.ApplyWorldPose(worldPosition, worldRotation);

            if (hand == null) return;

            float grip = Mathf.Lerp(_guardGrip, _sprintGrip, Mathf.Clamp01(SprintWeight));
            if (attackGrip >= 0f) grip = Mathf.Lerp(grip, attackGrip, attackWeight);

            hand.TargetGrip = grip;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawPoseGizmos) return;

            Transform space = _poseSpace != null ? _poseSpace : transform;

            DrawPoseGizmo(space, _leftGuardPose, Color.green);
            DrawPoseGizmo(space, _rightGuardPose, Color.green);
            DrawPoseGizmo(space, _leftTightGuardPose, Color.cyan);
            DrawPoseGizmo(space, _rightTightGuardPose, Color.cyan);
            DrawPoseGizmo(space, _leftSprintPose, Color.yellow);
            DrawPoseGizmo(space, _rightSprintPose, Color.yellow);
        }

        private void DrawPoseGizmo(Transform space, HandPose pose, Color color)
        {
            Gizmos.color = color;
            Vector3 world = space.TransformPoint(pose.position);
            Gizmos.DrawWireCube(world, Vector3.one * 0.05f);
            Gizmos.DrawRay(world, space.rotation * pose.Rotation * Vector3.forward * 0.08f);
        }
    }
}
