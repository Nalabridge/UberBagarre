using System;
using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Esquive : un déplacement bref et incontrôlable, avec une fenêtre d'invulnérabilité.
    ///
    /// Ce qui fait qu'une esquive est une compétence et non un bouton magique, c'est que la
    /// fenêtre d'invulnérabilité ne couvre PAS toute l'esquive : il faut esquiver au bon moment,
    /// pas esquiver tout le temps. Trop tôt, on est encore vulnérable à la fin ; trop tard,
    /// le coup est déjà passé.
    ///
    /// La structure est prête pour une « esquive parfaite » : il suffira de mesurer l'écart
    /// entre le début de l'esquive et l'ouverture de la fenêtre d'impact adverse.
    /// </summary>
    public class DodgeSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private StaminaSystem _stamina;

        [SerializeField]
        [Tooltip("Ce qui recoit l'elan. Doit implementer IImpulseReceiver.")]
        private MonoBehaviour _impulseReceiver;

        [Header("Deplacement")]
        [SerializeField, Min(0.1f)] private float _speed = 8.5f;
        [SerializeField, Min(0.05f)] private float _duration = 0.26f;
        [SerializeField, Min(0f)] private float _cooldown = 0.55f;
        [SerializeField, Min(0f)] private float _staminaCost = 18f;

        [Header("Fenetre d'invulnerabilite (fraction de la duree)")]
        [SerializeField, Range(0f, 1f)] private float _invulnerableStart = 0.08f;
        [SerializeField, Range(0f, 1f)] private float _invulnerableEnd = 0.72f;

        [Header("Debug")]
        [SerializeField] private bool _logDodges;

        private IImpulseReceiver _receiver;
        private float _timer;
        private float _cooldownTimer;
        private bool _invulnerableApplied;

        public event Action<Vector3> Dodged;

        public bool IsDodging { get { return _timer > 0f; } }
        public bool IsReady { get { return _timer <= 0f && _cooldownTimer <= 0f; } }
        public float CooldownRemaining { get { return Mathf.Max(0f, _cooldownTimer); } }

        public bool IsInvulnerable
        {
            get
            {
                if (_timer <= 0f) return false;

                float progress = 1f - (_timer / _duration);
                return progress >= _invulnerableStart && progress <= _invulnerableEnd;
            }
        }

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_stamina == null && _combatant != null) _stamina = _combatant.Stamina;

            _receiver = _impulseReceiver as IImpulseReceiver;
        }

        /// <summary>Tente une esquive dans une direction monde. Renvoie faux si impossible.</summary>
        public bool TryDodge(Vector3 worldDirection)
        {
            if (!IsReady) return false;
            if (_combatant != null && !_combatant.CanAct) return false;
            if (_stamina != null && !_stamina.CanSpend(_staminaCost)) return false;

            worldDirection.y = 0f;
            if (worldDirection.sqrMagnitude < 0.0001f) worldDirection = -transform.forward;
            worldDirection.Normalize();

            if (_stamina != null) _stamina.TrySpend(_staminaCost);
            if (_combatant != null) _combatant.State.TryEnter(CombatantState.Dodging, _duration);

            _timer = _duration;
            _cooldownTimer = _duration + _cooldown;

            if (_receiver != null) _receiver.ApplyImpulse(worldDirection * _speed);

            if (_logDodges) Debug.Log("[UberBagarre] " + name + " esquive vers " + worldDirection, this);

            Action<Vector3> dodged = Dodged;
            if (dodged != null) dodged(worldDirection);

            return true;
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_cooldownTimer > 0f) _cooldownTimer -= dt;

            if (_timer > 0f)
            {
                _timer -= dt;
                if (_timer < 0f) _timer = 0f;
            }

            UpdateInvulnerability();
        }

        /// <summary>
        /// L'invulnérabilité passe par le drapeau de la vie plutôt que par un test dans la hurtbox :
        /// elle protège ainsi de TOUTES les sources de dégâts, présentes et futures.
        /// </summary>
        private void UpdateInvulnerability()
        {
            if (_combatant == null || _combatant.Health == null) return;

            bool shouldBeInvulnerable = IsInvulnerable;
            if (shouldBeInvulnerable == _invulnerableApplied) return;

            _combatant.Health.Invulnerable = shouldBeInvulnerable;
            _invulnerableApplied = shouldBeInvulnerable;
        }
    }
}
