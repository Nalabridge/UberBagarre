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

        [SerializeField]
        [Tooltip("Optionnel : les boucles de garde capturees (marche, blocage, encaisse) ajoutees a la garde.")]
        private MocapArms _captured;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du balancement calcule gardee quand la marche capturee est la.")]
        private float _proceduralSwingWithCapture = 0.35f;

        [Header("Epaules")]
        [SerializeField]
        [Tooltip("Clavicules. Quand la cible du poing est hors de portee du bras, l'epaule " +
                 "s'avance : c'est le dernier centimetre d'un direct, celui qui porte.")]
        private Transform _leftClavicle;

        [SerializeField] private Transform _rightClavicle;

        [SerializeField, Min(0f)]
        [Tooltip("Avancee maximale de l'epaule, en metres.")]
        private float _shoulderReach = 0.07f;

        // Garde haute de bagarreur, pour un bras reel (52 cm, epaule 23 cm sous les yeux et
        // 10 cm en arriere) : poings a hauteur du menton, jointures vers le haut, coudes bas
        // qui couvrent les cotes. Vue de l'interieur, les deux poings montent du bas de l'image.
        // Les coups partent de la et vissent le poing a plat a l'impact.
        [Header("Garde normale (espace de visee)")]
        [SerializeField] private HandPose _leftGuardPose = new HandPose(new Vector3(-0.115f, -0.155f, 0.245f), new Vector3(-50f, 18f, 72f));
        [SerializeField] private HandPose _rightGuardPose = new HandPose(new Vector3(0.130f, -0.190f, 0.190f), new Vector3(-50f, -16f, -76f));

        [Header("Garde serree (clic droit)")]
        [SerializeField] private HandPose _leftTightGuardPose = new HandPose(new Vector3(-0.078f, -0.085f, 0.195f), new Vector3(-62f, 30f, 82f));
        [SerializeField] private HandPose _rightTightGuardPose = new HandPose(new Vector3(0.078f, -0.095f, 0.180f), new Vector3(-62f, -30f, -82f));

        [Header("Course")]
        [SerializeField] private HandPose _leftSprintPose = new HandPose(new Vector3(-0.215f, -0.36f, 0.12f), new Vector3(10f, 24f, 60f));
        [SerializeField] private HandPose _rightSprintPose = new HandPose(new Vector3(0.215f, -0.36f, 0.12f), new Vector3(10f, -24f, -60f));

        // Hors combat : les bras pendent le long du corps, paumes vers les cuisses, mains
        // entrouvertes. Hors du champ de la camera — un homme qui marche ne voit pas ses mains.
        [Header("Hors combat")]
        [SerializeField] private HandPose _leftRelaxedPose = new HandPose(new Vector3(-0.225f, -0.66f, 0.0f), new Vector3(80f, 0f, 88f));
        [SerializeField] private HandPose _rightRelaxedPose = new HandPose(new Vector3(0.225f, -0.66f, 0.0f), new Vector3(80f, 0f, -88f));
        [SerializeField, Range(0f, 1f)] private float _relaxedGrip = 0.28f;

        [Header("Fermeture des mains")]
        [SerializeField, Range(0f, 1f)] private float _guardGrip = 1f;
        [SerializeField, Range(0f, 1f)]
        [Tooltip("On ne court pas les poings serres : les mains se relachent.")]
        private float _sprintGrip = 0.3f;

        [Header("Respiration")]
        [SerializeField, Min(0f)] private float _breathAmplitude = 0.0045f;
        [SerializeField, Min(0f)] private float _breathRotationAmplitude = 1.3f;
        [SerializeField, Min(0f)] private float _breathFrequency = 0.28f;

        [Header("Micro-mouvement")]
        [SerializeField, Min(0f)] private float _idleNoiseAmplitude = 0.0016f;
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

        private Vector3 _leftHoldPosition;
        private Quaternion _leftHoldRotation = Quaternion.identity;
        private float _leftHoldWeight;
        private float _leftHoldGrip;
        private Vector3 _rightHoldPosition;
        private Quaternion _rightHoldRotation = Quaternion.identity;
        private float _rightHoldWeight;
        private float _rightHoldGrip;

        private Vector3[] _leftHoldCurls;
        private Vector3 _leftHoldThumb;
        private Vector3[] _rightHoldCurls;
        private Vector3 _rightHoldThumb;

        private Quaternion _leftClavicleRest;
        private Quaternion _rightClavicleRest;

        /// <summary>Position de l'épaule (racine du bras), en monde.</summary>
        public Vector3 ArmRoot(HandSide side)
        {
            IkLimb arm = side == HandSide.Left ? _leftArm : _rightArm;
            return arm != null ? arm.RootPosition : PoseSpace.position;
        }

        /// <summary>Longueur du bras tendu (bras + avant-bras).</summary>
        public float ArmReach(HandSide side)
        {
            IkLimb arm = side == HandSide.Left ? _leftArm : _rightArm;
            return arm != null ? arm.TotalLength : 0.5f;
        }

        /// <summary>Le poignet.</summary>
        public Transform ArmEnd(HandSide side)
        {
            IkLimb arm = side == HandSide.Left ? _leftArm : _rightArm;
            return arm != null ? arm.End : null;
        }

        /// <summary>Repère dans lequel les poses sont exprimées. Les coups de pied s'en servent aussi.</summary>
        public Transform PoseSpace
        {
            get { return _poseSpace != null ? _poseSpace : transform; }
        }

        public float GuardWeight { get; set; }
        public float SprintWeight { get; set; }

        /// <summary>0 = en garde, 1 = hors combat (bras le long du corps).</summary>
        public float RelaxedWeight { get; set; }

        /// <summary>
        /// 0 = mains en place, 1 = mains baissées hors du champ. Sert au mode appareil photo :
        /// l'image est le viseur, des poings au premier plan n'y ont rien à faire.
        /// </summary>
        public float Lowered { get; set; }

        private void Awake()
        {
            if (_poseSpace == null) _poseSpace = transform;
            _smoothedLeft = _leftGuardPose;
            _smoothedRight = _rightGuardPose;

            if (_leftClavicle != null) _leftClavicleRest = _leftClavicle.localRotation;
            if (_rightClavicle != null) _rightClavicleRest = _rightClavicle.localRotation;
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

        /// <summary>
        /// La main tient un objet : le poignet va à cette pose MONDE, les doigts se ferment
        /// autour. À appeler à chaque image par l'objet tenu (le téléphone), avant les mains.
        /// Un poids de 0 rend la main à la garde.
        /// </summary>
        public void SetHold(HandSide side, Vector3 wristPosition, Quaternion wristRotation, float weight, float grip)
        {
            weight = Mathf.Clamp01(weight);

            if (side == HandSide.Left)
            {
                _leftHoldPosition = wristPosition;
                _leftHoldRotation = wristRotation;
                _leftHoldWeight = weight;
                _leftHoldGrip = grip;
            }
            else
            {
                _rightHoldPosition = wristPosition;
                _rightHoldRotation = wristRotation;
                _rightHoldWeight = weight;
                _rightHoldGrip = grip;
            }
        }

        /// <summary>
        /// Même chose, avec une prise doigt par doigt (voir <see cref="HandRig.SetPoseOverride"/>)
        /// au lieu d'une simple fermeture.
        /// </summary>
        public void SetHold(HandSide side, Vector3 wristPosition, Quaternion wristRotation, float weight,
            Vector3[] fingerCurls, Vector3 thumbRotation)
        {
            SetHold(side, wristPosition, wristRotation, weight, 0.5f);

            if (side == HandSide.Left)
            {
                _leftHoldCurls = fingerCurls;
                _leftHoldThumb = thumbRotation;
            }
            else
            {
                _rightHoldCurls = fingerCurls;
                _rightHoldThumb = thumbRotation;
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

            // La garde monte et descend en courbe : un bras qui se leve lineairement se lit
            // comme un objet deplace, pas comme un geste.
            float relaxed = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(RelaxedWeight));
            basePose = HandPose.Lerp(basePose, isLeft ? _leftRelaxedPose : _rightRelaxedPose, relaxed);

            bool captured = _captured != null && _captured.AmbientActive;
            HandPose swing = ArmSwing(side);
            if (captured)
            {
                swing = new HandPose(swing.position * _proceduralSwingWithCapture, swing.euler * _proceduralSwingWithCapture);
            }

            HandPose pose = basePose + Breathing(isLeft) + IdleNoise(isLeft) + Sway() + swing;
            return captured ? Captured(side, pose, relaxed) : pose;
        }

        /// <summary>
        /// Les boucles capturées : en garde seulement (ni bras ballants, ni course). La marche
        /// suit la phase des jambes ; le blocage, le poids de la garde serrée.
        /// </summary>
        private HandPose Captured(HandSide side, HandPose pose, float relaxed)
        {
            float combat = (1f - relaxed) * (1f - Mathf.Clamp01(SprintWeight));
            if (combat <= 0.001f) return pose;

            float walk = 0f;
            float phase = 0f;
            if (_locomotion != null)
            {
                // MoveWeight vaut ~0,6 a la marche : la boucle capturee est une marche de combat.
                walk = Mathf.Clamp01(_locomotion.MoveWeight / 0.6f) * combat;
                phase = _locomotion.Phase;
            }

            Vector3 offset;
            Quaternion turn;
            if (!_captured.Ambient(side, phase, walk, Mathf.Clamp01(GuardWeight) * combat, out offset, out turn)) return pose;

            return new HandPose(pose.position + offset * combat, (turn * pose.Rotation).eulerAngles);
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
                new Vector3(y, x, 0f) * _idleNoiseAmplitude * 60f);
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

            // Objet tenu (le telephone) : il l'emporte sur la garde, au prorata de son poids.
            bool isLeft = side == HandSide.Left;
            float holdWeight = isLeft ? _leftHoldWeight : _rightHoldWeight;

            if (holdWeight > 0f)
            {
                worldPosition = Vector3.Lerp(worldPosition, isLeft ? _leftHoldPosition : _rightHoldPosition, holdWeight);
                worldRotation = Quaternion.Slerp(worldRotation, isLeft ? _leftHoldRotation : _rightHoldRotation, holdWeight);
            }

            if (Lowered > 0.001f)
            {
                Vector3 low = _poseSpace.TransformPoint(new Vector3(isLeft ? -0.2f : 0.2f, -0.62f, 0.12f));
                worldPosition = Vector3.Lerp(worldPosition, low, Mathf.Clamp01(Lowered));
            }

            ReachWithShoulder(side, arm, worldPosition);
            arm.ApplyWorldPose(worldPosition, worldRotation);

            if (hand == null) return;

            float grip = Mathf.Lerp(_guardGrip, _sprintGrip, Mathf.Clamp01(SprintWeight));
            grip = Mathf.Lerp(grip, _relaxedGrip, Mathf.Clamp01(RelaxedWeight));
            if (attackGrip >= 0f) grip = Mathf.Lerp(grip, attackGrip, attackWeight);
            if (holdWeight > 0f) grip = Mathf.Lerp(grip, isLeft ? _leftHoldGrip : _rightHoldGrip, holdWeight);

            hand.TargetGrip = grip;

            Vector3[] curls = isLeft ? _leftHoldCurls : _rightHoldCurls;
            hand.SetPoseOverride(curls != null ? holdWeight : 0f, curls, isLeft ? _leftHoldThumb : _rightHoldThumb);
        }

        /// <summary>
        /// L'épaule suit le poing quand le bras ne suffit plus : la clavicule pivote pour
        /// avancer l'articulation vers la cible, jusqu'à quelques centimètres. Un bras tendu
        /// sans ce mouvement se lit comme un bras de mannequin ; avec, comme un coup qui porte.
        /// </summary>
        private void ReachWithShoulder(HandSide side, IkLimb arm, Vector3 target)
        {
            Transform clavicle = side == HandSide.Left ? _leftClavicle : _rightClavicle;
            if (clavicle == null) return;

            clavicle.localRotation = side == HandSide.Left ? _leftClavicleRest : _rightClavicleRest;
            if (_shoulderReach <= 0f) return;

            Vector3 shoulder = arm.RootPosition;
            float excess = Vector3.Distance(shoulder, target) - arm.TotalLength * 0.92f;
            if (excess <= 0f) return;

            Vector3 lever = shoulder - clavicle.position;
            float leverLength = lever.magnitude;
            if (leverLength < 0.02f) return;

            Vector3 toward = (target - shoulder).normalized;
            Vector3 axis = Vector3.Cross(lever, toward);
            if (axis.sqrMagnitude < 1e-8f) return;

            float move = Mathf.Min(excess, _shoulderReach);
            float angle = Mathf.Min(move / leverLength * Mathf.Rad2Deg, 24f);
            clavicle.rotation = Quaternion.AngleAxis(angle, axis.normalized) * clavicle.rotation;
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
