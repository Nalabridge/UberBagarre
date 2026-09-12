using System;
using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Garde et parade.
    ///
    /// Deux choses très différentes, volontairement liées à la même touche :
    /// - **garder** réduit les dégâts mais coûte de l'endurance à chaque coup encaissé.
    ///   C'est une position d'attente, pas une position gratuite ;
    /// - **parer** est la courte fenêtre au tout début de la garde. Elle annule le coup ET
    ///   déséquilibre l'attaquant.
    ///
    /// C'est ce qui sépare « maintenir la garde en permanence » de « lever la garde au bon
    /// moment ». Sans la fenêtre, garder serait toujours la meilleure option ; sans le coût,
    /// ce serait la seule.
    ///
    /// La garde ne protège que de face : contourner l'adversaire reste payant.
    /// </summary>
    public class GuardSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private StaminaSystem _stamina;

        [Header("Blocage")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part des degats absorbee en garde.")]
        private float _damageReduction = 0.72f;

        [SerializeField, Min(0f)]
        [Tooltip("Endurance perdue a chaque coup bloque. Garder longtemps doit se payer.")]
        private float _staminaPerBlock = 11f;

        [SerializeField, Range(30f, 180f)]
        [Tooltip("Angle frontal protege. Au-dela, la garde ne sert a rien.")]
        private float _guardCone = 120f;

        [Header("Parade")]
        [SerializeField, Min(0f)]
        [Tooltip("Duree de la fenetre de parade, a partir du moment ou la garde est levee.")]
        private float _parryWindow = 0.26f;

        [SerializeField, Min(0f)] private float _parryStaggerDuration = 0.6f;
        [SerializeField, Min(0f)] private float _parryPushback = 4.5f;
        [SerializeField, Min(0f)] private float _parryStaminaRefund = 16f;

        [Header("Debug")]
        [SerializeField] private bool _logGuard;

        private float _guardHeldTime;
        private bool _guarding;

        public bool IsGuarding { get { return _guarding; } }

        /// <summary>Vrai pendant la fenêtre de parade, juste après la levée de garde.</summary>
        public bool InParryWindow { get { return _guarding && _guardHeldTime <= _parryWindow; } }

        /// <summary>Émis quand un coup est paré. Les retours visuels et sonores s'y branchent.</summary>
        public event Action<DamageInfo> Parried;

        public event Action<DamageInfo> Blocked;

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_stamina == null && _combatant != null) _stamina = _combatant.Stamina;
        }

        /// <summary>Appelé chaque frame par le pilote (entrées du joueur ou IA).</summary>
        public void SetGuard(bool guarding)
        {
            if (guarding && !_guarding) _guardHeldTime = 0f;

            _guarding = guarding && (_combatant == null || _combatant.IsAlive);
        }

        private void Update()
        {
            if (_guarding) _guardHeldTime += Time.deltaTime;
            else _guardHeldTime = 0f;
        }

        /// <summary>
        /// Filtre un coup entrant. Renvoie les dégâts réellement encaissés.
        /// Appelé par la hurtbox avant de toucher à la vie.
        /// </summary>
        public float FilterIncomingDamage(DamageInfo info, out bool parried, out bool blocked)
        {
            parried = false;
            blocked = false;

            if (!_guarding) return info.Amount;
            if (!IsFrontal(info.Direction)) return info.Amount;

            if (InParryWindow)
            {
                parried = true;
                OnParried(info);
                return 0f;
            }

            blocked = true;

            if (_stamina != null) _stamina.TrySpend(_staminaPerBlock);

            // Garde brisee : plus d'endurance, plus de protection. C'est ce qui empeche
            // de rester indefiniment derriere sa garde.
            if (_stamina != null && _stamina.IsEmpty)
            {
                if (_combatant != null) _combatant.State.TryEnter(CombatantState.Stunned, 0.7f);
                return info.Amount;
            }

            Action<DamageInfo> handler = Blocked;
            if (handler != null) handler(info);

            if (_logGuard) Debug.Log("[UberBagarre] " + name + " bloque " + info.Amount.ToString("0") + " degats", this);

            return info.Amount * (1f - _damageReduction);
        }

        private bool IsFrontal(Vector3 attackDirection)
        {
            attackDirection.y = 0f;
            if (attackDirection.sqrMagnitude < 0.0001f) return true;

            // La direction du coup pointe vers nous : on la retourne pour la comparer a notre avant.
            float angle = Vector3.Angle(transform.forward, -attackDirection.normalized);
            return angle <= _guardCone * 0.5f;
        }

        private void OnParried(DamageInfo info)
        {
            if (_stamina != null) _stamina.Refill(_parryStaminaRefund);

            // L'attaquant paie sa tentative : c'est la recompense du timing.
            if (info.Attacker != null)
            {
                Combatant attacker = info.Attacker.GetComponent<Combatant>();
                if (attacker != null) attacker.State.TryEnter(CombatantState.Stunned, _parryStaggerDuration);

                AttackExecutor executor = info.Attacker.GetComponent<AttackExecutor>();
                if (executor != null && executor.IsAttacking) executor.Cancel();

                IImpulseReceiver receiver = info.Attacker.GetComponent<IImpulseReceiver>();
                if (receiver != null) receiver.ApplyImpulse(info.Direction.normalized * _parryPushback);
            }

            if (_logGuard) Debug.Log("[UberBagarre] " + name + " PARE le coup", this);

            Action<DamageInfo> handler = Parried;
            if (handler != null) handler(info);
        }
    }
}
