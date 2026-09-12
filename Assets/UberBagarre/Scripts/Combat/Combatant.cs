using System;
using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Le combattant : ce qui est commun au joueur, aux ennemis et à tout ce qui se bat.
    ///
    /// Il n'implémente aucun comportement — il rassemble vie, endurance, statistiques et état,
    /// et les tient cohérents. Le joueur y branche ses entrées, l'ennemi son IA : le moteur de
    /// combat en dessous est rigoureusement le même.
    ///
    /// Le registre statique <see cref="All"/> évite un singleton de gameplay tout en permettant
    /// à une IA de trouver une cible sans référence câblée à la main.
    /// </summary>
    public class Combatant : MonoBehaviour
    {
        private static readonly List<Combatant> _all = new List<Combatant>();

        [Header("Identite")]
        [SerializeField] private Faction _faction = Faction.Enemy;
        [SerializeField] private string _displayName = "Combattant";

        [Header("Systemes")]
        [SerializeField] private HealthSystem _health;
        [SerializeField] private StaminaSystem _stamina;
        [SerializeField] private CombatantStats _stats;

        [Header("Reperes")]
        [SerializeField]
        [Tooltip("Origine de visee : la tete. Sert aux distances de combat et a l'orientation.")]
        private Transform _aimOrigin;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Etat courant, en lecture seule. Pratique pour suivre le combat dans l'Inspector.")]
        private CombatantState _debugState;

        private readonly CombatantStateMachine _state = new CombatantStateMachine();

        public static IReadOnlyList<Combatant> All { get { return _all; } }

        public Faction Faction { get { return _faction; } }
        public string DisplayName { get { return _displayName; } }
        public HealthSystem Health { get { return _health; } }
        public StaminaSystem Stamina { get { return _stamina; } }
        public CombatantStats Stats { get { return _stats; } }
        public CombatantStateMachine State { get { return _state; } }

        public Transform AimOrigin { get { return _aimOrigin != null ? _aimOrigin : transform; } }
        public Vector3 AimPosition { get { return AimOrigin.position; } }

        public bool IsAlive { get { return _health == null || _health.IsAlive; } }
        public bool CanAct { get { return IsAlive && _state.CanAct; } }

        public event Action<Combatant, DamageInfo> Damaged;
        public event Action<Combatant> Died;

        private void Awake()
        {
            if (_health == null) _health = GetComponent<HealthSystem>();
            if (_stamina == null) _stamina = GetComponent<StaminaSystem>();
            if (_stats == null) _stats = GetComponent<CombatantStats>();

            ApplyStats();
        }

        private void OnEnable()
        {
            _all.Add(this);

            if (_health != null)
            {
                _health.Damaged += OnDamaged;
                _health.Died += OnDied;
            }
        }

        private void OnDisable()
        {
            _all.Remove(this);

            if (_health != null)
            {
                _health.Damaged -= OnDamaged;
                _health.Died -= OnDied;
            }
        }

        private void Update()
        {
            _state.Tick(Time.deltaTime);
            _debugState = _state.Current;
        }

        /// <summary>Reporte les statistiques sur les systèmes concrets. À rappeler après une amélioration.</summary>
        public void ApplyStats()
        {
            if (_stats == null) return;

            if (_health != null) _health.SetMaxHealth(_stats.Get(StatType.MaxHealth), true);
            if (_stamina != null) _stamina.MaxStamina = _stats.Get(StatType.MaxStamina);
        }

        /// <summary>Remet le combattant à neuf : vie, endurance, état. Utilisé par la relance de combat.</summary>
        public void Revive()
        {
            if (_health != null) _health.ResetToFull();
            if (_stamina != null) _stamina.Refill();

            _state.Enter(CombatantState.Idle);
        }

        /// <summary>Le combattant hostile vivant le plus proche. Utilisé par l'IA pour trouver sa cible.</summary>
        public Combatant FindNearestOpponent(float maxDistance)
        {
            Combatant best = null;
            float bestSqr = maxDistance * maxDistance;

            for (int i = 0; i < _all.Count; i++)
            {
                Combatant other = _all[i];
                if (other == this || other.Faction == _faction || !other.IsAlive) continue;

                float sqr = (other.transform.position - transform.position).sqrMagnitude;
                if (sqr > bestSqr) continue;

                bestSqr = sqr;
                best = other;
            }

            return best;
        }

        public float DistanceTo(Combatant other)
        {
            if (other == null) return float.MaxValue;

            Vector3 delta = other.transform.position - transform.position;
            delta.y = 0f;
            return delta.magnitude;
        }

        private void OnDamaged(DamageInfo info)
        {
            Action<Combatant, DamageInfo> damaged = Damaged;
            if (damaged != null) damaged(this, info);
        }

        private void OnDied(DamageInfo info)
        {
            _state.Enter(CombatantState.Dead);

            Action<Combatant> died = Died;
            if (died != null) died(this);
        }
    }
}
