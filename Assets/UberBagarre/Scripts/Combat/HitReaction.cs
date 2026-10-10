using UberBagarre.Core;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Réaction du corps à un coup reçu : le buste encaisse, le combattant recule, et il perd
    /// brièvement le contrôle.
    ///
    /// Sans cette perte de contrôle, encaisser n'a aucune conséquence et le combat devient un
    /// échange de dégâts où personne n'est jamais gêné. L'état Hit est prioritaire sur
    /// l'attaque : un coup reçu en pleine préparation annule le coup en cours.
    ///
    /// Les réactions sont graduées : un jab fait tourner la tête, un uppercut casse la posture.
    /// </summary>
    public class HitReaction : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private HealthSystem _health;
        [SerializeField] private ProceduralLocomotion _locomotion;
        [SerializeField] private AttackExecutor _executor;

        [SerializeField]
        [Tooltip("Ce qui encaisse le recul. Doit implementer IImpulseReceiver (moteur du joueur ou de l'ennemi).")]
        private MonoBehaviour _impulseReceiver;

        [Header("Reaction legere")]
        [SerializeField, Min(0f)]
        [Tooltip("Perte de controle apres un coup leger. Elle doit couvrir l'intervalle jusqu'au " +
                 "coup suivant d'un enchainement (~0,2-0,27 s) : a 0,20 s l'adversaire reprenait " +
                 "la main entre deux directs et esquivait le troisieme, le combo se cassait.")]
        private float _lightStun = 0.30f;
        [SerializeField] private float _lightAngle = 9f;

        [SerializeField, Min(0f)]
        [Tooltip("Vitesse de recul, en m/s. Voir la note sur la distance reellement parcourue.")]
        private float _lightKnockback = 1.9f;

        [Header("Reaction lourde")]
        [SerializeField, Min(0f)] private float _heavyStun = 0.48f;
        [SerializeField] private float _heavyAngle = 22f;
        [SerializeField, Min(0f)] private float _heavyKnockback = 2.2f;

        [Header("Coup bloque")]
        [SerializeField, Min(0f)]
        [Tooltip("Part du recul conservee quand le coup est bloque. La perte de controle, elle, " +
                 "est entierement annulee : c'est CA que paie la garde.")]
        private float _blockedKnockbackScale = 0.45f;

        [SerializeField] private float _blockedAngle = 3.5f;

        [Header("Coup dans les jambes")]
        [SerializeField, Min(0f)]
        [Tooltip("Un coup bas ne fait pas tourner la tete : il deporte le bassin et fait perdre l'equilibre.")]
        private float _legAngleScale = 0.35f;

        [SerializeField, Min(0f)] private float _legKnockbackScale = 1.25f;

        [Header("Recul")]
        [SerializeField, Min(0f)]
        [Tooltip("Recul minimal, quelle que soit la force du coup. En dessous, le recul existe " +
                 "dans les chiffres mais ne se VOIT pas : 0,9 m/s amorti ne deplace que 5 cm.")]
        private float _knockbackBase = 0.55f;

        [SerializeField, Min(0f)]
        [Tooltip("Part du recul proportionnelle a la force d'impact du coup.")]
        private float _knockbackPerImpactForce = 0.07f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Plafond du facteur de recul : meme un uppercut charge ne projette pas a l'autre bout de la rue.")]
        private float _maxKnockbackFactor = 1.1f;

        [Header("Coup de conclusion")]
        [SerializeField, Min(0f)]
        [Tooltip("Le dernier coup d'un enchainement envoie valser : perte de controle et recul " +
                 "multiplies d'autant.")]
        private float _finisherStun = 0.65f;

        [SerializeField, Min(1f)] private float _finisherKnockbackScale = 1.8f;

        [SerializeField, Range(0.2f, 1f)]
        [Tooltip("Le JOUEUR perd la main moins longtemps qu'un adversaire : etre sonne sur un " +
                 "coup encaisse se lit comme un choc ; l'etre aussi longtemps qu'un PNJ se lit " +
                 "comme une manette qui ne repond plus.")]
        private float _playerStunScale = 0.65f;

        [Header("Retour")]
        [SerializeField, Min(0.5f)] private float _recoverySpeed = 6f;

        [Header("Usure de la perte de controle")]
        [SerializeField, Min(1)]
        [Tooltip("Coups encaisses d'affilee avant que la perte de controle ne s'use. Au-dela, " +
                 "chaque coup sonne un peu moins : on ne peut pas enfermer quelqu'un dans un " +
                 "enchainement infini, il finit par se couvrir ou s'esquiver.")]
        private int _hitsBeforeFatigue = 3;

        [SerializeField, Range(0.1f, 1f)]
        [Tooltip("Perte de controle restante, au plus usee (sept coups d'affilee et plus).")]
        private float _minStunScale = 0.45f;

        private IImpulseReceiver _receiver;
        private int _hitsInRow;
        private float _lastHitTime = -10f;
        private Vector3 _currentAngle;
        private Vector3 _targetAngle;
        private float _holdTimer;

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_health == null && _combatant != null) _health = _combatant.Health;

            _receiver = _impulseReceiver as IImpulseReceiver;

            if (_impulseReceiver != null && _receiver == null)
            {
                Debug.LogWarning("[UberBagarre] HitReaction sur " + name + " : le composant de recul " +
                                 "n'implemente pas IImpulseReceiver, le recul sera ignore.", this);
            }
        }

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            if (info.Blocked)
            {
                OnBlocked(info);
                return;
            }

            bool heavy = info.IsHeavy || info.Zone == HitZone.Head || info.IsFinisher;

            float duration = info.IsFinisher ? _finisherStun : heavy ? _heavyStun : _lightStun;
            float angle = heavy ? _heavyAngle : _lightAngle;
            float knockback = heavy ? _heavyKnockback : _lightKnockback;
            float maxFactor = _maxKnockbackFactor;

            if (info.IsFinisher)
            {
                angle *= 1.3f;
                knockback *= _finisherKnockbackScale;
                maxFactor *= _finisherKnockbackScale;
            }

            if (info.Zone == HitZone.Leg)
            {
                angle *= _legAngleScale;
                knockback *= _legKnockbackScale;
            }

            // Les coups qui s'enchaînent sans répit usent la perte de contrôle.
            _hitsInRow = Time.time - _lastHitTime < 0.9f ? _hitsInRow + 1 : 1;
            _lastHitTime = Time.time;
            float fatigue = Mathf.Clamp01((_hitsInRow - _hitsBeforeFatigue) / 4f);
            duration *= Mathf.Lerp(1f, _minStunScale, fatigue);

            if (_combatant != null)
            {
                if (_combatant.Faction == Faction.Player) duration *= _playerStunScale;
                _combatant.State.TryEnter(CombatantState.Hit, duration);
            }

            // Un coup encaisse interrompt le coup qu'on etait en train de porter.
            if (_executor != null && _executor.IsAttacking) _executor.Cancel();

            Vector3 local = transform.InverseTransformDirection(info.Direction.normalized);

            // La tete part dans le sens du coup : tangage si le coup vient de face,
            // roulis s'il vient de cote.
            _targetAngle = new Vector3(-local.z * angle, local.x * angle * 0.4f, -local.x * angle);
            _holdTimer = duration * 0.5f;

            if (_receiver != null)
            {
                // Le recul est une VITESSE, amortie lineairement par le moteur. La distance
                // reellement parcourue vaut v2 / (2 x amortissement) : avec l'amortissement de 8
                // des moteurs, 0,9 m/s ne deplacait que 5 cm. Invisible. A 2 m/s on recule de
                // 25 cm, a 4 m/s d'un bon metre : c'est la plage ou le coup se VOIT porter.
                float speed = knockback * Mathf.Min(_knockbackBase + info.ImpactForce * _knockbackPerImpactForce, maxFactor);

                Vector3 push = info.Direction.normalized * speed;
                push.y = 0f;
                _receiver.ApplyImpulse(push);
            }
        }

        /// <summary>
        /// Réaction à un coup bloqué : le corps encaisse un peu, et c'est tout.
        ///
        /// Pas d'état Hit, pas d'annulation du coup en cours. Un combattant qui bloque garde
        /// l'initiative — il peut riposter immédiatement. C'est exactement la récompense de la
        /// garde, et la raison pour laquelle elle coûte de l'endurance.
        /// </summary>
        private void OnBlocked(DamageInfo info)
        {
            Vector3 local = transform.InverseTransformDirection(info.Direction.normalized);

            _targetAngle = new Vector3(-local.z * _blockedAngle, 0f, -local.x * _blockedAngle);
            _holdTimer = 0.08f;

            if (_receiver == null) return;

            float speed = _lightKnockback * _blockedKnockbackScale *
                          Mathf.Min(_knockbackBase + info.ImpactForce * _knockbackPerImpactForce, _maxKnockbackFactor);

            Vector3 push = info.Direction.normalized * speed;
            push.y = 0f;
            _receiver.ApplyImpulse(push);
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_holdTimer > 0f) _holdTimer -= dt;
            else _targetAngle = Vector3.MoveTowards(_targetAngle, Vector3.zero, _recoverySpeed * 12f * dt);

            _currentAngle = Vector3.Lerp(_currentAngle, _targetAngle, 1f - Mathf.Exp(-_recoverySpeed * 2f * dt));

            if (_locomotion != null) _locomotion.HitReactionEuler = _currentAngle;
        }
    }
}
