using System;
using UberBagarre.Core;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Chute et relevé.
    ///
    /// Pourquoi une chute PROCÉDURALE et pas un vrai ragdoll : le squelette est déjà piloté en
    /// permanence par l'IK et le cycle de marche. Un ragdoll physique se battrait avec eux à
    /// chaque frame, et il faudrait désactiver puis resynchroniser toute la chaîne — pour un
    /// résultat impossible à rejouer à l'identique, donc impossible à régler.
    ///
    /// Ici le corps bascule autour de ses pieds, reste au sol, puis se redresse. C'est
    /// déterministe, réglable au degré près, et ça laisse l'IK continuer son travail.
    ///
    /// Une chute n'est pas qu'un effet : c'est le seul moment où un combattant est réellement
    /// puni. D'où le temps au sol, pendant lequel il ne peut rien faire.
    /// </summary>
    public class KnockdownSystem : MonoBehaviour
    {
        private enum Phase
        {
            Standing = 0,
            Falling = 1,
            Grounded = 2,
            GettingUp = 3
        }

        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private HealthSystem _health;
        [SerializeField] private ProceduralLocomotion _locomotion;
        [SerializeField] private AttackExecutor _executor;

        [SerializeField]
        [Tooltip("Le noeud d'inclinaison : il contient TOUT ce qui doit se coucher avec le " +
                 "combattant - le corps, ses zones touchables et le repere de ses bras. Il " +
                 "bascule autour de sa base, c'est-a-dire des pieds.")]
        private Transform _bodyRoot;

        [SerializeField] private MonoBehaviour _impulseReceiver;

        [SerializeField]
        [Tooltip("Optionnel, pour un combattant vu en premiere personne : le noeud de camera, " +
                 "enfant de la tete. Il suit les yeux du corps qui tombe, exactement.")]
        private Transform _cameraRoot;

        [SerializeField, Min(0.05f)]
        [Tooltip("Hauteur minimale des yeux au-dessus du sol, tete posee par terre.")]
        private float _eyeGroundClearance = 0.2f;

        [SerializeField, Min(0.02f)]
        [Tooltip("Rayon de la tete pour les collisions de la camera pendant la chute : on ne " +
                 "tombe pas a travers un mur, on tombe contre.")]
        private float _cameraRadius = 0.14f;

        [Header("Declenchement")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance de chute quand le coup touche les jambes.")]
        private float _legHitChance = 0.7f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Chance de chute sur un coup lourd quand la vie est deja basse.")]
        private float _heavyHitLowHealthChance = 0.4f;

        [SerializeField, Range(0f, 1f)] private float _lowHealthThreshold = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Delai minimal entre deux chutes. Sans lui, un adversaire au sol n'en sort jamais.")]
        private float _cooldown = 3f;

        [Header("Mort")]
        [SerializeField]
        [Tooltip("A la mort, s'effondrer et NE PAS se relever. A activer pour un combattant vu en " +
                 "premiere personne, qui n'a pas de ragdoll : sans ca, mourir ne se voit pas du " +
                 "tout puisqu'on ne voit pas son propre corps. A laisser decoche quand un " +
                 "DeathRagdoll prend le relais, pour que les deux ne se disputent pas le corps.")]
        private bool _collapseOnDeath;

        [Header("Timings (secondes)")]
        [SerializeField, Min(0.05f)] private float _fallDuration = 0.42f;
        [SerializeField, Min(0.05f)] private float _groundedDuration = 1.25f;
        [SerializeField, Min(0.05f)] private float _getUpDuration = 1.05f;

        [Header("Pose au sol")]
        [SerializeField, Range(40f, 92f)] private float _fallAngle = 84f;
        [SerializeField, Min(0f)] private float _slideImpulse = 3.5f;

        [Header("Releve")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du releve passee a se ramasser avant de se redresser. C'est ce qui " +
                 "distingue un releve d'une simple rotation inverse de la chute.")]
        private float _gatherPhase = 0.38f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Bascule restante a la fin de la phase de ramassage : le torse a quitte le sol, " +
                 "mais le corps est encore plie.")]
        private float _gatherTilt = 0.42f;

        [SerializeField, Min(0f)]
        [Tooltip("De combien le bassin se plie au plus bas du releve. C'est la position accroupie " +
                 "par laquelle on passe forcement pour se remettre debout.")]
        private float _getUpPelvisDrop = 0.40f;

        [SerializeField]
        [Tooltip("Roulis ajoute au debut du releve : on se met d'abord sur le cote, on ne se " +
                 "redresse pas a plat dos comme une planche.")]
        private float _getUpRoll = 22f;

        private IImpulseReceiver _receiver;
        private Phase _phase;
        private float _timer;
        private float _cooldownTimer;
        private Vector3 _fallEuler;
        private float _weight;
        private float _getUpProgress;
        private bool _terminal;
        private Quaternion _restRotation = Quaternion.identity;
        private Vector3 _cameraRestPosition;
        private Quaternion _cameraRestRotation = Quaternion.identity;
        private Quaternion _tilt = Quaternion.identity;
        private readonly RaycastHit[] _hits = new RaycastHit[12];

        public bool IsDown { get { return _phase != Phase.Standing; } }
        public float Weight { get { return _weight; } }

        /// <summary>
        /// La bascule actuelle du corps, dans le repère du combattant (identité debout). Le repère
        /// des mains du joueur la suit : on tombe avec sa garde, on ne la laisse pas en l'air.
        /// </summary>
        public Quaternion Tilt { get { return _tilt; } }

        public event Action KnockedDown;
        public event Action GotUp;

        /// <summary>Émis quand le relevé commence (fin du temps passé au sol).</summary>
        public event Action GettingUp;

        /// <summary>
        /// Faux quand une animation capturée joue la chute (voir MocapDriver) : le corps ne
        /// doit alors pas AUSSI basculer d'un bloc autour des pieds.
        /// </summary>
        public bool TiltEnabled { get; set; } = true;

        /// <summary>Direction de la dernière chute (monde, horizontale) : vers où le corps part.</summary>
        public Vector3 LastFallDirection { get; private set; }

        /// <summary>Vrai si le combattant est au sol pour de bon (mort).</summary>
        public bool IsTerminal { get { return _terminal; } }

        /// <summary>Durées de chute, d'attente au sol et de relevé (celles des animations capturées).</summary>
        public void SetTimings(float fall, float grounded, float getUp)
        {
            _fallDuration = Mathf.Max(0.05f, fall);
            _groundedDuration = Mathf.Max(0.05f, grounded);
            _getUpDuration = Mathf.Max(0.05f, getUp);

            // Appelé depuis KnockedDown, la chute est déjà lancée avec l'ancienne durée.
            if (_phase == Phase.Falling) _timer = _fallDuration;
        }

        /// <summary>Émis pour N'IMPORTE quelle chute. Sert au décompte des statistiques.</summary>
        public static event Action<KnockdownSystem> AnyKnockedDown;

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_health == null && _combatant != null) _health = _combatant.Health;
            _receiver = _impulseReceiver as IImpulseReceiver;

            // On memorise la pose debout au lieu de supposer une rotation nulle : un modele 3D
            // importe peut tres bien arriver avec une orientation de base non identitaire, et se
            // relever le remettrait alors de travers.
            if (_bodyRoot != null) _restRotation = _bodyRoot.localRotation;

            if (_cameraRoot != null)
            {
                _cameraRestPosition = _cameraRoot.localPosition;
                _cameraRestRotation = _cameraRoot.localRotation;
            }
        }

        private void OnEnable()
        {
            if (_health == null) return;

            _health.Damaged += OnDamaged;
            _health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_health == null) return;

            _health.Damaged -= OnDamaged;
            _health.Died -= OnDied;
        }

        private void OnDied(DamageInfo info)
        {
            if (_collapseOnDeath) Collapse(info.Direction);
        }

        /// <summary>
        /// Chute définitive : on tombe et on ne se relève plus.
        ///
        /// Sert à la mort d'un combattant qui n'a pas de ragdoll. Le relevé est le seul chemin de
        /// sortie de la chute, donc il suffit de le condamner.
        /// </summary>
        public void Collapse(Vector3 direction)
        {
            _terminal = true;

            if (IsDown)
            {
                // Deja au sol : on empeche simplement le releve.
                if (_phase == Phase.GettingUp)
                {
                    _phase = Phase.Grounded;
                    _weight = 1f;
                }

                _timer = float.MaxValue;
                return;
            }

            Knockdown(direction);
        }

        private void OnDamaged(DamageInfo info)
        {
            if (IsDown || _cooldownTimer > 0f) return;

            // On ne tombe jamais d'un coup qu'on a bloque : sinon la garde protegerait des
            // degats tout en laissant la pire consequence passer, et garder n'aurait plus de sens.
            if (info.Blocked) return;

            float chance = 0f;

            if (info.Zone == HitZone.Leg) chance = Mathf.Max(chance, _legHitChance);
            if (info.Attack != null) chance = Mathf.Max(chance, info.Attack.knockdownChance);
            if (info.BonusKnockdownChance > 0f) chance = Mathf.Max(chance, info.BonusKnockdownChance);

            bool lowHealth = _health != null && _health.Normalized <= _lowHealthThreshold;
            if (info.IsHeavy && lowHealth) chance = Mathf.Max(chance, _heavyHitLowHealthChance);

            if (chance <= 0f || UnityEngine.Random.value > chance) return;

            Knockdown(info.Direction);
        }

        /// <summary>Fait tomber le combattant dans la direction donnée.</summary>
        public void Knockdown(Vector3 direction)
        {
            if (IsDown) return;

            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
            direction.Normalize();

            LastFallDirection = direction;
            Vector3 local = transform.InverseTransformDirection(direction);

            // On bascule dans le sens du coup : pousse de face = chute en arriere, frappe a
            // gauche = chute a droite. (Rotation positive autour de X = la tete part vers l'avant,
            // autour de Z = vers la gauche : les deux signes sont donc ceux de l'oppose du coup.
            // L'ancienne formule faisait tomber vers l'attaquant, face contre terre.)
            _fallEuler = new Vector3(local.z * _fallAngle, 0f, -local.x * _fallAngle);

            _phase = Phase.Falling;
            _timer = _fallDuration;

            if (_executor != null && _executor.IsAttacking) _executor.Cancel();
            if (_receiver != null) _receiver.ApplyImpulse(direction * _slideImpulse);

            Action knocked = KnockedDown;
            if (knocked != null) knocked();

            Action<KnockdownSystem> anyKnocked = AnyKnockedDown;
            if (anyKnocked != null) anyKnocked(this);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_cooldownTimer > 0f) _cooldownTimer -= dt;
            if (_phase == Phase.Standing) return;

            _timer -= dt;
            AdvancePhase();
            ApplyPose();

            // Tant qu'on est au sol, on est hors du combat : l'etat le dit aux autres systemes.
            if (_combatant != null) _combatant.State.Enter(CombatantState.Stunned, 0.2f);
        }

        private void AdvancePhase()
        {
            if (_timer > 0f) return;

            switch (_phase)
            {
                case Phase.Falling:
                    _phase = Phase.Grounded;
                    _timer = _terminal ? float.MaxValue : _groundedDuration;
                    break;

                case Phase.Grounded:
                    _phase = Phase.GettingUp;
                    _timer = _getUpDuration;

                    Action gettingUp = GettingUp;
                    if (gettingUp != null) gettingUp();
                    break;

                case Phase.GettingUp:
                    _phase = Phase.Standing;
                    _timer = 0f;
                    _cooldownTimer = _cooldown;

                    if (_combatant != null) _combatant.State.Enter(CombatantState.Idle);

                    Action gotUp = GotUp;
                    if (gotUp != null) gotUp();
                    break;
            }
        }

        private void ApplyPose()
        {
            switch (_phase)
            {
                case Phase.Falling:
                    // Accelere vers la fin : on part doucement puis on s'ecroule.
                    float fall = 1f - Mathf.Clamp01(_timer / _fallDuration);
                    _weight = fall * fall;
                    break;

                case Phase.Grounded:
                    _weight = 1f;
                    break;

                case Phase.GettingUp:
                    // Deux temps, et c'est tout l'interet.
                    //
                    // Une seule courbe qui ramene la bascule de 84 a 0 ne donne pas un releve :
                    // ca donne la chute jouee a l'envers, ce qui se lit immediatement comme
                    // faux. Un vrai releve se RAMASSE d'abord — le torse quitte le sol vite, le
                    // corps reste plie — puis se DEPLIE. La bascule et le pliage du bassin
                    // suivent donc deux courbes differentes, dephasees.
                    _getUpProgress = 1f - Mathf.Clamp01(_timer / _getUpDuration);

                    float gather = Mathf.Max(0.01f, _gatherPhase);

                    _weight = _getUpProgress < gather
                        ? Mathf.Lerp(1f, _gatherTilt, Smooth(_getUpProgress / gather))
                        : Mathf.Lerp(_gatherTilt, 0f, Smooth((_getUpProgress - gather) / (1f - gather)));
                    break;

                default:
                    _getUpProgress = 0f;
                    _weight = Mathf.MoveTowards(_weight, 0f, Time.deltaTime * 4f);
                    break;
            }

            // Petit soubresaut a l'arrivee au sol, puis immobilite.
            float settle = _phase == Phase.Grounded
                ? Mathf.Sin(Time.time * 9f) * Mathf.Max(0f, _timer - _groundedDuration + 0.35f) * 6f
                : 0f;

            // On se met sur le cote avant de se redresser : un corps qui se releve a plat dos,
            // d'une seule piece, n'existe pas. Le roulis part du cote ou on est tombe.
            float roll = 0f;

            if (_phase == Phase.GettingUp && Mathf.Abs(_getUpRoll) > 0.01f)
            {
                float side = _fallEuler.z >= 0f ? 1f : -1f;
                roll = Mathf.Sin(_getUpProgress * Mathf.PI) * _getUpRoll * side;
            }

            Quaternion tilt = Quaternion.Euler(
                _fallEuler * _weight + new Vector3(settle, 0f, settle * 0.4f + roll));
            _tilt = tilt;

            // Un SEUL transform bascule, et tout ce qui doit tomber est dessous : le corps, les
            // zones touchables, le repere des bras. Faire basculer trois transforms en parallele
            // finissait forcement par en oublier un — et l'oubli etait spectaculaire : les bras
            // de l'adversaire restaient tendus vers le ciel a hauteur d'yeux pendant qu'il etait
            // couche au sol, parce que la cible de leur IK vivait hors du corps.
            if (!TiltEnabled) tilt = Quaternion.identity;
            _tilt = tilt;

            if (_bodyRoot != null) _bodyRoot.localRotation = _restRotation * tilt;

            if (_locomotion != null)
            {
                _locomotion.KnockdownWeight = _weight;

                // Le bassin se plie au plus bas du releve : c'est la position accroupie par
                // laquelle on passe forcement pour se remettre debout. Appliquee seulement
                // pendant le releve — au sol, le corps est deja a plat, plier le bassin en
                // plus ne voudrait rien dire.
                _locomotion.ExtraPelvisDrop = _phase == Phase.GettingUp
                    ? Mathf.Sin(_getUpProgress * Mathf.PI) * _getUpPelvisDrop
                    : 0f;
            }

            ApplyCameraPose();
        }

        /// <summary>Adoucissement aux extrémités, pour que chaque temps du relevé démarre et finisse sans à-coup.</summary>
        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        /// <summary>
        /// Le point de vue suit les YEUX du corps qui tombe.
        ///
        /// La première version faisait descendre la caméra tout droit d'un mètre et l'inclinait
        /// d'une fraction de la bascule, pendant que le corps, lui, se couchait autour de ses pieds.
        /// Les yeux se retrouvaient au-dessus des chevilles, le corps allongé devant ou derrière :
        /// on regardait l'intérieur de sa propre nuque. Ici, la caméra subit EXACTEMENT la bascule
        /// du corps, autour du même pivot : elle décrit l'arc de la tête, se pose à une vingtaine de
        /// centimètres du sol, regarde le ciel quand on tombe sur le dos, puis remonte par la même
        /// position accroupie que le corps. La visée du joueur continue de s'appliquer dans ce
        /// repère couché. Un mur ou une marche derrière arrêtent la tête au lieu de la traverser.
        /// </summary>
        private void ApplyCameraPose()
        {
            if (_cameraRoot == null) return;

            Transform head = _cameraRoot.parent;
            Transform root = head != null ? head.parent : null;

            if (head == null || root == null || (_weight <= 0.0001f && _phase == Phase.Standing))
            {
                _cameraRoot.localPosition = _cameraRestPosition;
                _cameraRoot.localRotation = _cameraRestRotation;
                return;
            }

            // Tout se calcule dans le repere du combattant, celui ou la bascule du corps est definie.
            Vector3 pivot = _bodyRoot != null && _bodyRoot.parent == root ? _bodyRoot.localPosition : Vector3.zero;
            Vector3 eye = head.localPosition + head.localRotation * _cameraRestPosition;

            // Pendant le releve, le bassin plie : les yeux descendent avec lui.
            if (_locomotion != null) eye.y -= _locomotion.ExtraPelvisDrop;

            Vector3 target = pivot + _tilt * (eye - pivot);

            Vector3 from = root.TransformPoint(head.localPosition);
            Vector3 to = Clear(root, from, root.TransformPoint(target));

            _cameraRoot.localPosition = Quaternion.Inverse(head.localRotation) * (root.InverseTransformPoint(to) - head.localPosition);
            _cameraRoot.localRotation = Quaternion.Inverse(head.localRotation) * _tilt * head.localRotation * _cameraRestRotation;
        }

        /// <summary>La tête s'arrête contre ce qu'elle rencontre (mur, voiture, marche), et jamais sous le sol.</summary>
        private Vector3 Clear(Transform root, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;

            if (length > 0.001f)
            {
                Vector3 direction = delta / length;
                int count = Physics.SphereCastNonAlloc(from, _cameraRadius, direction, _hits, length, ~0,
                    QueryTriggerInteraction.Ignore);

                float nearest = length;
                for (int i = 0; i < count; i++)
                {
                    Collider collider = _hits[i].collider;
                    if (collider == null || collider.transform.IsChildOf(root)) continue;
                    if (_hits[i].distance <= 0f) continue;
                    nearest = Mathf.Min(nearest, _hits[i].distance);
                }

                to = from + direction * nearest;
            }

            // Le sol sous la tete : les yeux restent au-dessus, tete posee.
            int down = Physics.RaycastNonAlloc(to + Vector3.up * 0.3f, Vector3.down, _hits, 1.3f, ~0,
                QueryTriggerInteraction.Ignore);

            float ground = float.MinValue;
            for (int i = 0; i < down; i++)
            {
                Collider collider = _hits[i].collider;
                if (collider == null || collider.transform.IsChildOf(root)) continue;
                ground = Mathf.Max(ground, _hits[i].point.y);
            }

            if (ground > float.MinValue) to.y = Mathf.Max(to.y, ground + _eyeGroundClearance);
            return to;
        }

        /// <summary>
        /// Remet debout immédiatement, sans animation.
        ///
        /// Appelé par la relance de combat. Sans ça, relancer pendant une chute laissait un
        /// combattant couché à son point d'apparition, l'air cassé.
        /// </summary>
        public void ForceStand()
        {
            _phase = Phase.Standing;
            _terminal = false;
            _timer = 0f;
            _cooldownTimer = 0f;
            _weight = 0f;
            _fallEuler = Vector3.zero;
            _tilt = Quaternion.identity;

            _getUpProgress = 0f;

            if (_bodyRoot != null) _bodyRoot.localRotation = _restRotation;

            if (_locomotion != null)
            {
                _locomotion.KnockdownWeight = 0f;
                _locomotion.ExtraPelvisDrop = 0f;
            }

            if (_cameraRoot != null)
            {
                _cameraRoot.localPosition = _cameraRestPosition;
                _cameraRoot.localRotation = _cameraRestRotation;
            }
        }
    }
}
