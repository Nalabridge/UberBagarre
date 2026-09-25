using UberBagarre.Combat;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Un spectateur : il regarde le combat, se balance sur la musique, lève les bras sur un
    /// beau coup, grimace sur un coup dur, et exulte au K.O.
    ///
    /// Il a le MÊME corps que les combattants (même squelette, mêmes jambes procédurales) mais
    /// aucun système de combat : pas de vie, pas de zones de frappe, pas de cerveau. Un public
    /// n'a besoin que de trois choses — regarder au bon endroit, réagir au bon moment, et ne
    /// pas réagir tous en même temps.
    ///
    /// Ce dernier point décide de tout. Trente personnes qui lèvent les bras à la même image
    /// se lisent comme une animation ; les mêmes, avec chacune un temps de réaction entre 80 et
    /// 400 ms et un tempérament à elle, se lisent comme une foule. D'où le délai tiré au
    /// hasard avant chaque réaction, et le <see cref="_temperament"/>.
    ///
    /// Exécuté APRÈS la locomotion (80) : les jambes et le buste sont posés, les bras et la
    /// tête se placent par-dessus.
    /// </summary>
    [DefaultExecutionOrder(95)]
    public class Spectator : MonoBehaviour
    {
        public enum Mood
        {
            /// <summary>Autour de la fosse : suit le combat, réagit aux coups.</summary>
            Spectateur = 0,

            /// <summary>Sur la piste : danse face à la scène, bras en l'air sur les temps forts.</summary>
            Danseur = 1,

            /// <summary>Au bar : un verre à la main, regarde vaguement ailleurs.</summary>
            Accoude = 2
        }

        [Header("Corps")]
        [SerializeField] private BodyRig _rig;
        [SerializeField] private ProceduralLocomotion _locomotion;

        [Header("Comportement")]
        [SerializeField] private Mood _mood = Mood.Spectateur;

        [SerializeField]
        [Tooltip("Ce qu'il regarde quand rien ne se passe : le centre de la fosse, la scene, le bar.")]
        private Transform _focus;

        [SerializeField, Min(1f)]
        [Tooltip("Un coup porte plus loin que ca ne le concerne pas.")]
        private float _reactionRadius = 14f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("0 = blase, 1 = hurle a chaque coup.")]
        private float _temperament = 0.6f;

        [SerializeField] private float _seed;

        [SerializeField, Min(0f)]
        [Tooltip("Vitesse a laquelle il se tourne vers l'action, en degres par seconde.")]
        private float _turnSpeed = 70f;

        private float _excitement;
        private float _wince;
        private float _pendingExcitement;
        private float _pendingWince;
        private float _pendingAt = -1f;
        private float _celebrateUntil;
        private float _lookYaw;
        private float _lookPitch;

        private Quaternion _neckRest;
        private bool _captured;

        public Mood Behaviour
        {
            get { return _mood; }
            set { _mood = value; }
        }

        public Transform Focus
        {
            get { return _focus; }
            set { _focus = value; }
        }

        private void Awake()
        {
            if (_rig == null) _rig = GetComponentInChildren<BodyRig>();
            if (_locomotion == null) _locomotion = GetComponentInChildren<ProceduralLocomotion>();
        }

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;
            Combatant.AnyDied += OnAnyDied;
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;
            Combatant.AnyDied -= OnAnyDied;
        }

        /// <summary>Fait exulter ce spectateur, par exemple quand le combat commence.</summary>
        public void Cheer(float strength, float duration)
        {
            ScheduleReaction(strength, 0f);
            _celebrateUntil = Mathf.Max(_celebrateUntil, Time.time + duration);
        }

        // ------------------------------------------------------------------ réactions

        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (_mood == Mood.Accoude || victim == null || !Near(victim.transform.position)) return;

            float strength = info.Blocked ? 0.15f : Mathf.Clamp01(0.25f + info.Amount / 35f);
            float wince = !info.Blocked && (info.Amount >= 18f || info.Zone == HitZone.Head) ? 0.8f : 0f;

            ScheduleReaction(strength, wince);
        }

        private void OnAnyDied(Combatant victim, DamageInfo info)
        {
            if (_mood == Mood.Accoude || victim == null || !Near(victim.transform.position)) return;

            ScheduleReaction(1f, 0f);
            _celebrateUntil = Time.time + 3.5f + Hash(3.1f) * 2f;
        }

        private void ScheduleReaction(float strength, float wince)
        {
            // Chacun réagit avec son propre retard : c'est ce qui fait une foule.
            float delay = 0.08f + Hash(Time.time * 1.7f) * 0.32f;

            _pendingExcitement = Mathf.Max(_pendingExcitement, strength);
            _pendingWince = Mathf.Max(_pendingWince, wince);
            if (_pendingAt < 0f) _pendingAt = Time.time + delay;
        }

        private bool Near(Vector3 point)
        {
            return (point - transform.position).sqrMagnitude <= _reactionRadius * _reactionRadius;
        }

        // ------------------------------------------------------------------ pose

        private void LateUpdate()
        {
            if (_rig == null) return;

            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            if (!_captured)
            {
                if (_rig.Neck != null) _neckRest = _rig.Neck.localRotation;
                _captured = true;
            }

            ResolvePending();

            float target = Time.time < _celebrateUntil ? 1f : 0f;
            _excitement = Mathf.MoveTowards(_excitement, target, (target > _excitement ? 4f : 0.35f) * dt);
            _wince = Mathf.MoveTowards(_wince, 0f, 1.1f * dt);

            Vector3 lookPoint = LookPoint();

            TurnTowards(lookPoint, dt);
            PoseBody();
            PoseArms();
            PoseHead(lookPoint, dt);
        }

        private void ResolvePending()
        {
            if (_pendingAt < 0f || Time.time < _pendingAt) return;

            float gain = Mathf.Lerp(0.45f, 1.15f, _temperament);
            _excitement = Mathf.Clamp01(Mathf.Max(_excitement, _pendingExcitement * gain));
            _wince = Mathf.Max(_wince, _pendingWince);

            _pendingExcitement = 0f;
            _pendingWince = 0f;
            _pendingAt = -1f;
        }

        /// <summary>
        /// Où regarder : les combattants proches s'il y en a — la moyenne de leurs positions,
        /// donc le milieu de l'échange —, sinon le point de repère (fosse, scène, bar).
        /// </summary>
        private Vector3 LookPoint()
        {
            Vector3 fallback = _focus != null ? _focus.position + Vector3.up * 1.4f : transform.position + transform.forward * 3f;
            if (_mood != Mood.Spectateur) return fallback;

            Vector3 sum = Vector3.zero;
            int count = 0;

            var all = Combatant.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant combatant = all[i];
                if (combatant == null || combatant.Faction == Faction.Neutral) continue;
                if (!Near(combatant.transform.position)) continue;

                sum += combatant.transform.position;
                count++;
            }

            if (count == 0) return fallback;

            return sum / count + Vector3.up * 1.3f;
        }

        private void TurnTowards(Vector3 point, float dt)
        {
            Vector3 flat = point - transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude < 0.04f) return;

            // Un danseur ne pivote pas vers l'action : il reste tourné vers la scène, en
            // oscillant un peu avec la musique.
            Quaternion goal = Quaternion.LookRotation(flat.normalized, Vector3.up);
            if (_mood == Mood.Danseur)
            {
                goal *= Quaternion.Euler(0f, Mathf.Sin(ClubMusic.GlobalBeats * Mathf.PI * 0.25f + _seed) * 25f, 0f);
            }

            transform.rotation = Quaternion.RotateTowards(transform.rotation, goal, _turnSpeed * dt);
        }

        private void PoseBody()
        {
            if (_locomotion == null) return;

            float beats = ClubMusic.GlobalBeats + _seed * 0.13f;
            float pulse = Mathf.Exp(-(beats - Mathf.Floor(beats)) * 5f);

            float bounce;
            float lean;

            switch (_mood)
            {
                case Mood.Danseur:
                    bounce = 0.02f + 0.06f * pulse;
                    lean = 4f + 3f * pulse;
                    break;

                case Mood.Accoude:
                    bounce = 0.01f * pulse;
                    lean = 6f;
                    break;

                default:
                    // Le public se penche vers le combat quand ça chauffe, et saute au K.O.
                    bounce = 0.012f * pulse + _excitement * 0.05f * Mathf.Abs(Mathf.Sin(Time.time * 7f + _seed));
                    lean = 3f + _excitement * 9f - _wince * 8f;
                    break;
            }

            _locomotion.ExtraPelvisDrop = bounce;
            _locomotion.CombatBodyEuler = new Vector3(lean, 0f, Mathf.Sin(Time.time * 0.6f + _seed) * 2f);
        }

        private void PoseArms()
        {
            PoseArm(HandSide.Left, -1f);
            PoseArm(HandSide.Right, 1f);
        }

        private void PoseArm(HandSide side, float sign)
        {
            IkLimb arm = _rig.Arm(side);
            if (arm == null) return;

            Transform root = transform;
            Vector3 shoulder = arm.RootPosition;
            Vector3 forward = root.forward;
            Vector3 right = root.right;
            Vector3 up = Vector3.up;

            // Bras le long du corps, un peu en avant pour ne pas traverser la hanche.
            Vector3 idle = shoulder - up * 0.50f + forward * 0.08f + right * (sign * 0.07f);

            // Accoudé : la main droite tient un verre devant la poitrine.
            if (_mood == Mood.Accoude && side == HandSide.Right)
            {
                idle = shoulder - up * 0.20f + forward * 0.30f - right * 0.05f;
            }

            Vector3 target = idle;

            float beats = ClubMusic.GlobalBeats + _seed * 0.13f;
            float pulse = Mathf.Exp(-(beats - Mathf.Floor(beats)) * 5f);

            if (_mood == Mood.Danseur)
            {
                // Un bras sur deux temps, en alternance : le geste de club le plus universel.
                bool raised = (Mathf.FloorToInt(beats / 2f) % 2 == 0) == (side == HandSide.Right);
                Vector3 dance = shoulder + up * (0.18f + 0.22f * pulse) + forward * 0.22f + right * (sign * 0.10f);
                target = Vector3.Lerp(target, dance, raised ? 0.9f : 0.25f);
            }

            // Les deux bras en l'air, poings qui battent la mesure.
            float pump = Mathf.Sin(Time.time * 9f + _seed + (side == HandSide.Left ? 0f : 1.3f)) * 0.07f;
            Vector3 cheer = shoulder + up * (0.46f + pump) + forward * 0.12f + right * (sign * 0.14f);
            target = Vector3.Lerp(target, cheer, _excitement);

            // Mains vers le visage sur un coup qui fait mal à voir.
            if (_rig.Neck != null)
            {
                Vector3 face = _rig.Neck.position + up * 0.12f + forward * 0.14f + right * (sign * 0.07f);
                target = Vector3.Lerp(target, face, _wince);
            }

            Quaternion rotation = Quaternion.LookRotation(forward, up);
            arm.ApplyWorldPose(target, rotation);
        }

        private void PoseHead(Vector3 lookPoint, float dt)
        {
            Transform neck = _rig.Neck;
            Transform chest = _rig.Chest;
            if (neck == null || chest == null) return;

            neck.localRotation = _neckRest;

            Vector3 direction = lookPoint - neck.position;
            if (direction.sqrMagnitude < 0.01f) return;

            Vector3 local = chest.InverseTransformDirection(direction.normalized);
            float yaw = Mathf.Clamp(Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg, -65f, 65f);
            float pitch = Mathf.Clamp(-Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg, -35f, 30f);

            // Le mouvement de tête d'un danseur suit le temps, pas l'action.
            if (_mood == Mood.Danseur)
            {
                float beats = ClubMusic.GlobalBeats + _seed * 0.13f;
                pitch += Mathf.Exp(-(beats - Mathf.Floor(beats)) * 5f) * 12f;
            }

            float blend = 1f - Mathf.Exp(-dt * 6f);
            _lookYaw = Mathf.Lerp(_lookYaw, yaw, blend);
            _lookPitch = Mathf.Lerp(_lookPitch, pitch, blend);

            neck.localRotation = _neckRest * Quaternion.Euler(_lookPitch, _lookYaw, 0f);
        }

        private float Hash(float salt)
        {
            float value = Mathf.Sin((_seed + 1f) * 12.9898f + salt * 78.233f) * 43758.5453f;
            return value - Mathf.Floor(value);
        }
    }
}
