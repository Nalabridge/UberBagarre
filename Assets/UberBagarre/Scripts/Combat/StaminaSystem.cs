using System;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Endurance, séparée de la vie.
    ///
    /// Volontairement générique : elle sera consommée par les coups, les esquives, le sprint
    /// et plus tard les attaques lourdes. Le délai avant régénération est ce qui la rend
    /// tactique — enchaîner cinq crochets a un coût, il faut savoir s'arrêter.
    /// </summary>
    public class StaminaSystem : MonoBehaviour
    {
        [SerializeField, Min(1f)] private float _maxStamina = 100f;
        [SerializeField, Min(0f)] private float _regenPerSecond = 22f;

        [SerializeField, Min(0f)]
        [Tooltip("Delai apres une depense avant que la regeneration reprenne.")]
        private float _regenDelay = 0.8f;

        [SerializeField]
        [Tooltip("Autorise l'action meme s'il ne reste pas tout le cout, puis descend a zero.")]
        private bool _allowPartialSpend = true;

        private float _current;
        private float _regenBlockedUntil;

        public event Action<float> Spent;
        public event Action Depleted;

        public float Max { get { return _maxStamina; } }
        public float Current { get { return _current; } }
        public float Normalized { get { return _maxStamina <= 0f ? 0f : Mathf.Clamp01(_current / _maxStamina); } }
        public bool IsEmpty { get { return _current <= 0.01f; } }

        public float MaxStamina
        {
            get { return _maxStamina; }
            set
            {
                _maxStamina = Mathf.Max(1f, value);
                _current = Mathf.Min(_current, _maxStamina);
            }
        }

        private void Awake()
        {
            _current = _maxStamina;
        }

        public bool CanSpend(float amount)
        {
            if (amount <= 0f) return true;
            return _allowPartialSpend ? _current > 0.01f : _current >= amount;
        }

        public bool TrySpend(float amount)
        {
            if (!CanSpend(amount)) return false;

            _current = Mathf.Max(0f, _current - amount);
            _regenBlockedUntil = Time.time + _regenDelay;

            Action<float> spent = Spent;
            if (spent != null) spent(amount);

            if (_current <= 0.01f)
            {
                Action depleted = Depleted;
                if (depleted != null) depleted();
            }

            return true;
        }

        public void Refill()
        {
            _current = _maxStamina;
        }

        /// <summary>Rend une partie de l'endurance, par exemple en récompense d'une parade réussie.</summary>
        public void Refill(float amount)
        {
            if (amount <= 0f) return;
            _current = Mathf.Min(_maxStamina, _current + amount);
        }

        private void Update()
        {
            if (_current >= _maxStamina || Time.time < _regenBlockedUntil) return;

            _current = Mathf.Min(_maxStamina, _current + _regenPerSecond * Time.deltaTime);
        }
    }
}
