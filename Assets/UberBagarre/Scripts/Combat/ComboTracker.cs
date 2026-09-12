using System;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Compte les coups enchaînés sans interruption.
    ///
    /// Un compteur de combo n'est pas qu'un chiffre à l'écran : c'est le retour qui dit au
    /// joueur « continue, tu contrôles l'échange ». Il faut donc qu'il se brise, sinon il ne
    /// récompense rien — d'où la fenêtre de temps, et la remise à zéro quand on encaisse.
    /// </summary>
    public class ComboTracker : MonoBehaviour
    {
        [SerializeField] private Combatant _owner;

        [SerializeField, Min(0.2f)]
        [Tooltip("Delai maximal entre deux coups pour que le combo continue.")]
        private float _window = 1.6f;

        [SerializeField]
        [Tooltip("Encaisser un coup brise le combo. Sans ca, il n'y a aucun risque a foncer.")]
        private bool _breakOnDamageTaken = true;

        private float _timeLeft;

        public int Count { get; private set; }
        public float TotalDamage { get; private set; }
        public float TimeLeft { get { return _timeLeft; } }
        public float Window { get { return _window; } }

        /// <summary>Émis à chaque coup qui prolonge le combo. Paramètres : nombre de coups, dégâts cumulés.</summary>
        public event Action<int, float> Advanced;

        public event Action<int, float> Ended;

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;
        }

        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (_owner == null) return;

            if (_breakOnDamageTaken && victim == _owner)
            {
                Break();
                return;
            }

            if (info.Attacker != _owner.gameObject) return;

            Count++;
            TotalDamage += info.Amount;
            _timeLeft = _window;

            Action<int, float> advanced = Advanced;
            if (advanced != null) advanced(Count, TotalDamage);
        }

        private void Update()
        {
            if (_timeLeft <= 0f) return;

            _timeLeft -= Time.deltaTime;
            if (_timeLeft > 0f) return;

            Break();
        }

        private void Break()
        {
            if (Count <= 0)
            {
                _timeLeft = 0f;
                return;
            }

            Action<int, float> ended = Ended;
            if (ended != null) ended(Count, TotalDamage);

            Count = 0;
            TotalDamage = 0f;
            _timeLeft = 0f;
        }
    }
}
