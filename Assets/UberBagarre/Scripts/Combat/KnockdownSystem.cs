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
        [Tooltip("Optionnel, pour un combattant vu en premiere personne. En vue subjective, " +
                 "basculer le corps ne se voit PAS : c'est le point de vue qui doit descendre et " +
                 "rouler, sinon tomber ne se distingue pas d'une simple perte de controle.")]
        private Transform _cameraRoot;

        [SerializeField, Min(0f)]
        [Tooltip("De combien le point de vue descend quand on est au sol, en metres.")]
        private float _cameraDrop = 1.02f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part de la bascule du corps reportee sur le point de vue. A 1 on a la nausee.")]
        private float _cameraTiltScale = 0.55f;

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

        public bool IsDown { get { return _phase != Phase.Standing; } }
        public float Weight { get { return _weight; } }

        public event Action KnockedDown;
        public event Action GotUp;

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

            Vector3 local = transform.InverseTransformDirection(direction);

            // On bascule dans le sens du coup : pousse de face = chute en arriere.
            _fallEuler = new Vector3(-local.z * _fallAngle, 0f, local.x * _fallAngle);

            _phase = Phase.Falling;
            _timer = _fallDuration;

            if (_executor != null && _executor.IsAttacking) _executor.Cancel();
            if (_receiver != null) _receiver.ApplyImpulse(direction * _slideImpulse);

            Action knocked = KnockedDown;
            if (knocked != null) knocked();
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

            // Un SEUL transform bascule, et tout ce qui doit tomber est dessous : le corps, les
            // zones touchables, le repere des bras. Faire basculer trois transforms en parallele
            // finissait forcement par en oublier un — et l'oubli etait spectaculaire : les bras
            // de l'adversaire restaient tendus vers le ciel a hauteur d'yeux pendant qu'il etait
            // couche au sol, parce que la cible de leur IK vivait hors du corps.
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

        private void ApplyCameraPose()
        {
            if (_cameraRoot == null) return;

            _cameraRoot.localPosition = _cameraRestPosition + Vector3.down * (_cameraDrop * _weight);
            _cameraRoot.localRotation = _cameraRestRotation *
                Quaternion.Euler(_fallEuler * (_weight * _cameraTiltScale));
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
