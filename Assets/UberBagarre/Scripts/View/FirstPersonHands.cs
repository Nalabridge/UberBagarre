using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Compose la pose des deux poings à chaque frame, puis la donne aux bras.
    ///
    /// La pose finale est un empilement de couches, dans cet ordre :
    ///
    ///   1. POSE DE BASE     garde normale → garde serrée → course   (mélangées par des poids)
    ///   2. COUCHES ADDITIVES respiration + micro-mouvement + inertie de visée + déplacement
    ///   3. LISSAGE          donne du poids : la main ne se téléporte jamais
    ///   4. COUCHE D'ATTAQUE appliquée APRÈS le lissage (phase 3)
    ///
    /// Pourquoi l'attaque passe après le lissage : un lissage écraserait la vivacité d'un jab.
    /// Une attaque impose sa propre position, avec son propre timing, et le poids du mélange
    /// gère l'entrée et la sortie de coup.
    ///
    /// Ce composant ne lit AUCUNE entrée clavier/souris : il est piloté de l'extérieur
    /// (voir PlayerHandsDriver). C'est ce qui permettra à l'ennemi de réutiliser le même
    /// système d'animation, piloté par son IA au lieu des touches.
    /// </summary>
    [DefaultExecutionOrder(100)]
    public class FirstPersonHands : MonoBehaviour
    {
        [Header("Bras")]
        [SerializeField] private FirstPersonArm _leftArm;
        [SerializeField] private FirstPersonArm _rightArm;

        [Header("Garde normale (espace camera)")]
        [SerializeField] private HandPose _leftGuardPose = new HandPose(new Vector3(-0.17f, -0.17f, 0.32f), new Vector3(-8f, 18f, 8f));
        [SerializeField] private HandPose _rightGuardPose = new HandPose(new Vector3(0.16f, -0.20f, 0.26f), new Vector3(-6f, -16f, -10f));

        [Header("Garde serree (clic droit maintenu)")]
        [SerializeField] private HandPose _leftTightGuardPose = new HandPose(new Vector3(-0.11f, -0.09f, 0.26f), new Vector3(-14f, 26f, 12f));
        [SerializeField] private HandPose _rightTightGuardPose = new HandPose(new Vector3(0.11f, -0.09f, 0.25f), new Vector3(-14f, -26f, -12f));

        [Header("Course (sprint)")]
        [SerializeField] private HandPose _leftSprintPose = new HandPose(new Vector3(-0.21f, -0.30f, 0.20f), new Vector3(10f, 22f, 6f));
        [SerializeField] private HandPose _rightSprintPose = new HandPose(new Vector3(0.21f, -0.30f, 0.20f), new Vector3(10f, -22f, -6f));

        [Header("Respiration")]
        [SerializeField, Min(0f)] private float _breathAmplitude = 0.007f;
        [SerializeField, Min(0f)] private float _breathRotationAmplitude = 1.3f;
        [SerializeField, Min(0f)]
        [Tooltip("En cycles par seconde. 0.28 = un cycle toutes les 3.5 s, rythme d'un corps au repos.")]
        private float _breathFrequency = 0.28f;

        [Header("Micro-mouvement (bruit organique)")]
        [SerializeField, Min(0f)] private float _idleNoiseAmplitude = 0.005f;
        [SerializeField, Min(0f)] private float _idleNoiseFrequency = 0.55f;

        [Header("Inertie de visee")]
        [SerializeField, Min(0f)]
        [Tooltip("Retard des mains quand on tourne la camera. C'est le principal indice de poids en FPS.")]
        private float _swayAmount = 0.02f;

        [SerializeField, Min(0f)] private float _swayRotationAmount = 22f;
        [SerializeField, Min(0f)] private float _swayMaxOffset = 0.06f;
        [SerializeField, Min(0.5f)] private float _swayResponse = 9f;

        [Header("Deplacement")]
        [SerializeField] private float _strafeSwayAmount = 0.022f;
        [SerializeField] private float _forwardSwayAmount = 0.018f;
        [SerializeField, Min(0f)] private float _walkBobAmount = 0.012f;
        [SerializeField, Min(0f)] private float _walkBobFrequency = 8f;

        [Header("Lissage")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Plus haut = mains plus reactives et plus seches. Plus bas = plus lourdes.")]
        private float _poseResponse = 13f;

        [Header("Debug")]
        [SerializeField] private bool _drawPoseGizmos;

        private HandPose _smoothedLeft;
        private HandPose _smoothedRight;

        private Vector2 _lookRate;
        private Vector2 _swayOffset;
        private Vector3 _localVelocity;
        private float _normalizedSpeed;

        private float _breathPhase;
        private float _noiseTime;
        private float _walkPhase;

        private HandPose _leftAttackPose;
        private HandPose _rightAttackPose;
        private float _leftAttackWeight;
        private float _rightAttackWeight;

        /// <summary>0 = garde normale, 1 = garde serrée. Piloté par le joueur ou par l'IA.</summary>
        public float GuardWeight { get; set; }

        /// <summary>0 = à l'arrêt ou en marche, 1 = en course.</summary>
        public float SprintWeight { get; set; }

        public FirstPersonArm LeftArm { get { return _leftArm; } }
        public FirstPersonArm RightArm { get { return _rightArm; } }

        private void Awake()
        {
            _smoothedLeft = _leftGuardPose;
            _smoothedRight = _rightGuardPose;
        }

        /// <summary>Déplacement souris de la frame. Converti en vitesse pour rester indépendant du framerate.</summary>
        public void SetLookDelta(Vector2 delta, float deltaTime)
        {
            _lookRate = delta / Mathf.Max(deltaTime, 0.0001f) * 0.01f;
        }

        /// <summary>Vitesse du joueur exprimée dans son propre repère, et vitesse normalisée 0..1.</summary>
        public void SetLocomotion(Vector3 localVelocity, float normalizedSpeed)
        {
            _localVelocity = localVelocity;
            _normalizedSpeed = Mathf.Clamp01(normalizedSpeed);
        }

        /// <summary>Pose imposée par une attaque. weight = 0 rend la main à la garde, 1 = attaque pure.</summary>
        public void SetAttackPose(HandSide side, HandPose pose, float weight)
        {
            weight = Mathf.Clamp01(weight);
            if (side == HandSide.Left)
            {
                _leftAttackPose = pose;
                _leftAttackWeight = weight;
            }
            else
            {
                _rightAttackPose = pose;
                _rightAttackWeight = weight;
            }
        }

        public void ClearAttackPose(HandSide side)
        {
            if (side == HandSide.Left) _leftAttackWeight = 0f;
            else _rightAttackWeight = 0f;
        }

        /// <summary>Pose de repos courante, lissage compris. Point de départ naturel d'une attaque.</summary>
        public HandPose GetRestPose(HandSide side)
        {
            return side == HandSide.Left ? _smoothedLeft : _smoothedRight;
        }

        /// <summary>Pose de garde configurée, sans aucune couche additive.</summary>
        public HandPose GetGuardPose(HandSide side)
        {
            return side == HandSide.Left ? _leftGuardPose : _rightGuardPose;
        }

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            AdvanceClocks(dt);
            UpdateSway(dt);

            HandPose left = ComposeIdlePose(HandSide.Left);
            HandPose right = ComposeIdlePose(HandSide.Right);

            float t = 1f - Mathf.Exp(-_poseResponse * dt);
            _smoothedLeft = HandPose.Lerp(_smoothedLeft, left, t);
            _smoothedRight = HandPose.Lerp(_smoothedRight, right, t);

            HandPose finalLeft = _leftAttackWeight > 0f
                ? HandPose.Lerp(_smoothedLeft, _leftAttackPose, _leftAttackWeight)
                : _smoothedLeft;

            HandPose finalRight = _rightAttackWeight > 0f
                ? HandPose.Lerp(_smoothedRight, _rightAttackPose, _rightAttackWeight)
                : _smoothedRight;

            ApplyToArm(_leftArm, finalLeft);
            ApplyToArm(_rightArm, finalRight);
        }

        private void AdvanceClocks(float dt)
        {
            _breathPhase += dt * _breathFrequency * Mathf.PI * 2f;
            if (_breathPhase > Mathf.PI * 2f) _breathPhase -= Mathf.PI * 2f;

            _noiseTime += dt;
            _walkPhase += dt * _walkBobFrequency * Mathf.Max(0.2f, _normalizedSpeed);
        }

        private void UpdateSway(float dt)
        {
            Vector2 target = new Vector2(-_lookRate.x, -_lookRate.y) * _swayAmount;
            target.x = Mathf.Clamp(target.x, -_swayMaxOffset, _swayMaxOffset);
            target.y = Mathf.Clamp(target.y, -_swayMaxOffset, _swayMaxOffset);

            float t = 1f - Mathf.Exp(-_swayResponse * dt);
            _swayOffset = Vector2.Lerp(_swayOffset, target, t);
        }

        private HandPose ComposeIdlePose(HandSide side)
        {
            bool isLeft = side == HandSide.Left;

            HandPose guard = isLeft ? _leftGuardPose : _rightGuardPose;
            HandPose tight = isLeft ? _leftTightGuardPose : _rightTightGuardPose;
            HandPose sprint = isLeft ? _leftSprintPose : _rightSprintPose;

            HandPose basePose = HandPose.Lerp(guard, tight, Mathf.Clamp01(GuardWeight));
            basePose = HandPose.Lerp(basePose, sprint, Mathf.Clamp01(SprintWeight));

            return basePose + Breathing(isLeft) + IdleNoise(isLeft) + Sway() + Locomotion(isLeft);
        }

        private HandPose Breathing(bool isLeft)
        {
            // Les deux mains sont légèrement déphasées : parfaitement synchrones, ça fait mécanique.
            float phase = _breathPhase + (isLeft ? 0f : 0.6f);
            float wave = Mathf.Sin(phase);

            return new HandPose(
                new Vector3(0f, wave * _breathAmplitude, wave * _breathAmplitude * 0.35f),
                new Vector3(-wave * _breathRotationAmplitude, 0f, 0f));
        }

        private HandPose IdleNoise(bool isLeft)
        {
            // Perlin plutôt qu'un sinus : le mouvement ne se répète pas de façon perceptible.
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

        private HandPose Locomotion(bool isLeft)
        {
            // Les bras traînent derrière le corps qui accélère, et oscillent en opposition de phase.
            float phase = _walkPhase + (isLeft ? 0f : Mathf.PI);
            float bob = Mathf.Sin(phase) * _walkBobAmount * _normalizedSpeed;

            return new HandPose(
                new Vector3(
                    -_localVelocity.x * _strafeSwayAmount,
                    bob,
                    -Mathf.Max(0f, _localVelocity.z) * _forwardSwayAmount),
                new Vector3(bob * 90f, 0f, 0f));
        }

        private void ApplyToArm(FirstPersonArm arm, HandPose pose)
        {
            if (arm == null) return;

            Vector3 worldPosition = transform.TransformPoint(pose.position);
            Quaternion worldRotation = transform.rotation * pose.Rotation;
            arm.ApplyWorldPose(worldPosition, worldRotation);
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawPoseGizmos) return;

            DrawPoseGizmo(_leftGuardPose, Color.green);
            DrawPoseGizmo(_rightGuardPose, Color.green);
            DrawPoseGizmo(_leftTightGuardPose, Color.cyan);
            DrawPoseGizmo(_rightTightGuardPose, Color.cyan);
            DrawPoseGizmo(_leftSprintPose, Color.yellow);
            DrawPoseGizmo(_rightSprintPose, Color.yellow);
        }

        private void DrawPoseGizmo(HandPose pose, Color color)
        {
            Gizmos.color = color;
            Vector3 world = transform.TransformPoint(pose.position);
            Gizmos.DrawWireCube(world, Vector3.one * 0.05f);
            Gizmos.DrawRay(world, transform.rotation * pose.Rotation * Vector3.forward * 0.08f);
        }
    }
}
