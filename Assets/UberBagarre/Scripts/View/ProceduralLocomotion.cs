using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Cycle de marche / course procédural, avec appui réel des pieds au sol.
    ///
    /// Le principe, et c'est ce qui sépare une vraie marche d'une fausse :
    /// **un pied posé ne bouge pas.** Il reste à sa position MONDE pendant toute sa phase
    /// d'appui, pendant que le corps avance par-dessus. Puis il décolle, se pose plus loin,
    /// et c'est l'autre pied qui prend le relais.
    ///
    /// Faire l'inverse — bouger les pieds avec le corps et rajouter un balancement —
    /// donne le patinage caractéristique des animations bricolées.
    ///
    /// Le bassin, le buste et le balancement des bras découlent ensuite du même cycle,
    /// donc tout le corps reste synchrone par construction.
    /// </summary>
    [DefaultExecutionOrder(80)]
    public class ProceduralLocomotion : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private BodyRig _rig;

        [SerializeField]
        [Tooltip("Racine du personnage. Sert de repere pour la hauteur du sol et l'orientation.")]
        private Transform _root;

        [Header("Foulee")]
        [SerializeField, Min(0.1f)]
        [Tooltip("Distance parcourue par CYCLE COMPLET (deux pas), pas par pas. " +
                 "Se tromper ici donne une cadence deux fois trop rapide, effet 'petits pas presses'.")]
        private float _walkStride = 1.45f;

        [SerializeField, Min(0.1f)] private float _runStride = 2.3f;
        [SerializeField, Min(0.01f)] private float _stepHeight = 0.11f;

        [SerializeField, Min(0f)]
        [Tooltip("Hauteur de la cheville au-dessus du sol. La cible d'IK est la cheville, pas la semelle : " +
                 "sans ce decalage, la jambe se tend a l'extreme et le pied s'enfonce dans le sol.")]
        private float _ankleHeight = 0.09f;

        [SerializeField, Range(0.4f, 0.9f)]
        [Tooltip("Part du cycle passee au sol. 0.6 = marche, 0.4 = course (temps de suspension).")]
        private float _stanceRatio = 0.62f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Vitesse a partir de laquelle la foulee est consideree comme une course.")]
        private float _runSpeedThreshold = 4.2f;

        [Header("Garde a l'arret (stance de boxe)")]
        [SerializeField] private Vector3 _idleLeftFoot = new Vector3(-0.14f, 0f, 0.19f);
        [SerializeField] private Vector3 _idleRightFoot = new Vector3(0.16f, 0f, -0.17f);
        [SerializeField, Min(0.05f)]
        [Tooltip("Vitesse de replacement des pieds a l'arret. Produit de petits pas d'ajustement.")]
        private float _idleFootAdjustSpeed = 1.4f;

        [Header("Bassin")]
        [SerializeField] private float _pelvisBob = 0.045f;
        [SerializeField] private float _pelvisSway = 0.035f;
        [SerializeField] private float _pelvisYaw = 4.5f;
        [SerializeField] private float _pelvisRoll = 3f;

        [Header("Buste")]
        [SerializeField]
        [Tooltip("Le buste tourne a l'oppose du bassin : c'est la contre-rotation naturelle de la marche.")]
        private float _torsoCounterYaw = 6f;

        [SerializeField] private float _torsoLeanPerSpeed = 2.2f;
        [SerializeField, Min(0f)] private float _maxTorsoLean = 11f;

        [Header("Accroupi / glissade")]
        [SerializeField] private float _crouchPelvisDrop = 0.42f;
        [SerializeField] private float _slidePelvisDrop = 0.62f;
        [SerializeField] private float _slideLean = 14f;

        [Header("Balancement des bras")]
        [SerializeField, Min(0f)] private float _armSwingAmount = 0.11f;

        [Header("Sol")]
        [SerializeField] private LayerMask _groundMask = ~0;
        [SerializeField, Min(0.1f)] private float _groundProbeDistance = 2.5f;

        [Header("Debug")]
        [SerializeField] private bool _drawFootGizmos;

        private Vector3 _velocity;
        private bool _grounded = true;
        private float _crouchAmount;
        private float _slideAmount;

        private float _phase;
        private float _moveWeight;

        private readonly Vector3[] _footPosition = new Vector3[2];
        private readonly Vector3[] _swingStart = new Vector3[2];
        private readonly bool[] _wasSwinging = new bool[2];
        private bool _initialised;

        /// <summary>Alimenté chaque frame par le pilote du personnage (joueur ou IA).</summary>
        public void SetState(Vector3 worldVelocity, bool grounded, float crouchAmount, float slideAmount)
        {
            _velocity = worldVelocity;
            _grounded = grounded;
            _crouchAmount = Mathf.Clamp01(crouchAmount);
            _slideAmount = Mathf.Clamp01(slideAmount);
        }

        /// <summary>
        /// Décalage avant/arrière du bras, en mètres. Positif = le poing part vers l'avant.
        /// Un bras avance quand la jambe opposée avance : c'est la coordination croisée humaine.
        /// </summary>
        public float ArmSwing(HandSide side)
        {
            float offset = side == HandSide.Left ? 0.5f : 0f;
            return Mathf.Sin((_phase + offset) * Mathf.PI * 2f) * _armSwingAmount * _moveWeight;
        }

        /// <summary>Intensité du déplacement, 0 à l'arrêt, 1 en course. Utile aux autres systèmes.</summary>
        public float MoveWeight { get { return _moveWeight; } }

        /// <summary>
        /// Rotation du buste demandée par le combat, en degrés. Additive par-dessus la marche.
        ///
        /// Passer par une propriété plutôt que d'écrire directement sur les os évite que la
        /// locomotion et l'attaque se marchent dessus : ici, la locomotion reste seule à écrire
        /// sur la colonne, et elle intègre ce que le combat lui demande.
        /// </summary>
        public Vector3 CombatBodyEuler { get; set; }

        /// <summary>
        /// Rotation du buste due à un coup ENCAISSÉ, en degrés. Additive elle aussi.
        ///
        /// Séparée de CombatBodyEuler pour une raison concrète : on peut être touché au milieu
        /// de son propre coup. Les deux doivent pouvoir coexister sans que l'un efface l'autre.
        /// </summary>
        public Vector3 HitReactionEuler { get; set; }

        private void Start()
        {
            if (_root == null) _root = transform;
            ResetFeetToIdle();
            _initialised = true;
        }

        private void LateUpdate()
        {
            if (_rig == null || !_initialised) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            Vector3 flatVelocity = new Vector3(_velocity.x, 0f, _velocity.z);
            float speed = flatVelocity.magnitude;

            UpdatePhase(speed, dt);
            UpdateFeet(flatVelocity, speed, dt);
            UpdatePelvisAndTorso(speed);
        }

        private void UpdatePhase(float speed, float dt)
        {
            float targetWeight = Mathf.Clamp01(speed / Mathf.Max(0.2f, _runSpeedThreshold * 0.55f));
            if (!_grounded || _slideAmount > 0.5f) targetWeight = 0f;

            _moveWeight = Mathf.MoveTowards(_moveWeight, targetWeight, 5f * dt);

            float stride = Mathf.Lerp(_walkStride, _runStride, Mathf.Clamp01(speed / _runSpeedThreshold));
            float cadence = speed / Mathf.Max(0.1f, stride);

            _phase += cadence * dt;
            if (_phase > 1f) _phase -= Mathf.Floor(_phase);
        }

        private void UpdateFeet(Vector3 flatVelocity, float speed, float dt)
        {
            for (int i = 0; i < 2; i++)
            {
                bool isLeft = i == 0;
                IkLimb leg = _rig.Leg(isLeft);
                if (leg == null) continue;

                Vector3 idleTarget = IdleFootWorld(isLeft);
                Vector3 cycleTarget = CycleFootTarget(i, isLeft, leg, flatVelocity, speed);

                if (_moveWeight <= 0.01f)
                {
                    // À l'arrêt, les pieds rejoignent la garde par petits ajustements plutôt
                    // que de glisser d'un bloc : un corps qui pivote fait des pas, il ne tourne pas sur place.
                    _footPosition[i] = Vector3.MoveTowards(_footPosition[i], idleTarget, _idleFootAdjustSpeed * dt);
                }
                else
                {
                    _footPosition[i] = Vector3.Lerp(idleTarget, cycleTarget, _moveWeight);
                }

                Quaternion footRotation = FootRotation(i, flatVelocity);
                leg.ApplyWorldPose(_footPosition[i], footRotation);
            }
        }

        private Vector3 CycleFootTarget(int index, bool isLeft, IkLimb leg, Vector3 flatVelocity, float speed)
        {
            float phase = Mathf.Repeat(_phase + (isLeft ? 0.5f : 0f), 1f);
            bool swinging = phase >= _stanceRatio;

            Vector3 hip = leg.RootPosition;
            Vector3 ground = GroundPoint(hip);

            if (!swinging)
            {
                // Phase d'appui : le pied NE BOUGE PAS. C'est le corps qui passe au-dessus.
                if (_wasSwinging[index])
                {
                    _wasSwinging[index] = false;
                    _footPosition[index] = new Vector3(_footPosition[index].x, ground.y + _ankleHeight, _footPosition[index].z);
                }

                return _footPosition[index];
            }

            if (!_wasSwinging[index])
            {
                _wasSwinging[index] = true;
                _swingStart[index] = _footPosition[index];
            }

            float stride = Mathf.Lerp(_walkStride, _runStride, Mathf.Clamp01(speed / _runSpeedThreshold));
            Vector3 direction = flatVelocity.sqrMagnitude > 0.0001f ? flatVelocity.normalized : _root.forward;
            // Le pied plante recule de stanceRatio x foulee par rapport au corps : il doit donc
            // se poser environ a +0.31 foulee devant la hanche pour repartir symetriquement.
            Vector3 landing = ground + direction * (stride * 0.31f);

            float t = Mathf.InverseLerp(_stanceRatio, 1f, phase);
            float smooth = t * t * (3f - 2f * t);

            Vector3 position = Vector3.Lerp(_swingStart[index], landing, smooth);
            position.y = ground.y + _ankleHeight + Mathf.Sin(t * Mathf.PI) * _stepHeight;

            return position;
        }

        private Vector3 IdleFootWorld(bool isLeft)
        {
            Vector3 local = isLeft ? _idleLeftFoot : _idleRightFoot;
            Vector3 world = _root.TransformPoint(local);
            world.y = GroundPoint(world).y + _ankleHeight;
            return world;
        }

        private Quaternion FootRotation(int index, Vector3 flatVelocity)
        {
            Vector3 forward = flatVelocity.sqrMagnitude > 0.01f
                ? Vector3.Lerp(_root.forward, flatVelocity.normalized, _moveWeight * 0.6f)
                : _root.forward;

            forward.y = 0f;
            if (forward.sqrMagnitude < 0.0001f) forward = Vector3.forward;

            // Le pied se relève légèrement pendant le vol, et retombe talon en premier.
            float phase = Mathf.Repeat(_phase + (index == 0 ? 0.5f : 0f), 1f);
            float pitch = 0f;

            if (phase >= _stanceRatio)
            {
                float t = Mathf.InverseLerp(_stanceRatio, 1f, phase);
                pitch = Mathf.Sin(t * Mathf.PI) * -14f * _moveWeight;
            }

            return Quaternion.LookRotation(forward.normalized, Vector3.up) * Quaternion.Euler(pitch, 0f, 0f);
        }

        private void UpdatePelvisAndTorso(float speed)
        {
            Transform pelvis = _rig.Pelvis;
            if (pelvis == null) return;

            float cycle = _phase * Mathf.PI * 2f;

            // Deux creux par cycle : le bassin descend à chaque réception de pied.
            float bob = -_pelvisBob * (0.5f - 0.5f * Mathf.Cos(cycle * 2f)) * _moveWeight;
            float sway = Mathf.Sin(cycle) * _pelvisSway * _moveWeight;

            float drop = _crouchPelvisDrop * _crouchAmount + _slidePelvisDrop * _slideAmount;

            pelvis.localPosition = _rig.PelvisRestPosition + new Vector3(sway, bob - drop, 0f);
            pelvis.localRotation = _rig.PelvisRestRotation * Quaternion.Euler(
                0f,
                -Mathf.Sin(cycle) * _pelvisYaw * _moveWeight,
                -Mathf.Sin(cycle) * _pelvisRoll * _moveWeight);

            float lean = Mathf.Clamp(speed * _torsoLeanPerSpeed, 0f, _maxTorsoLean);
            lean = Mathf.Lerp(lean, -_slideLean, _slideAmount);
            lean += _crouchAmount * 6f;

            // Le buste encaisse la moitié de la rotation de combat, la poitrine l'autre moitié :
            // la torsion se répartit le long de la colonne au lieu de casser à un seul endroit.
            Vector3 combat = (CombatBodyEuler + HitReactionEuler) * 0.5f;

            if (_rig.Spine != null)
            {
                _rig.Spine.localRotation = _rig.SpineRestRotation * Quaternion.Euler(
                    lean * 0.5f + combat.x,
                    Mathf.Sin(cycle) * _torsoCounterYaw * 0.5f * _moveWeight + combat.y,
                    combat.z);
            }

            if (_rig.Chest != null)
            {
                _rig.Chest.localRotation = _rig.ChestRestRotation * Quaternion.Euler(
                    lean * 0.5f + combat.x,
                    Mathf.Sin(cycle) * _torsoCounterYaw * 0.5f * _moveWeight + combat.y,
                    combat.z);
            }
        }

        private Vector3 GroundPoint(Vector3 from)
        {
            Vector3 origin = new Vector3(from.x, from.y + 0.2f, from.z);

            RaycastHit hit;
            if (Physics.Raycast(origin, Vector3.down, out hit, _groundProbeDistance, _groundMask, QueryTriggerInteraction.Ignore))
            {
                return hit.point;
            }

            return new Vector3(from.x, _root.position.y, from.z);
        }

        private void ResetFeetToIdle()
        {
            _footPosition[0] = IdleFootWorld(true);
            _footPosition[1] = IdleFootWorld(false);
            _swingStart[0] = _footPosition[0];
            _swingStart[1] = _footPosition[1];
        }

        private void OnDrawGizmos()
        {
            if (!_drawFootGizmos || !_initialised) return;

            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(_footPosition[0], 0.05f);
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(_footPosition[1], 0.05f);
        }
    }
}
