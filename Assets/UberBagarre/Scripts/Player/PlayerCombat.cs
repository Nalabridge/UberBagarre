using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Traduit les entrées du joueur en coups.
    ///
    /// C'est la seule pièce qui connaît à la fois la souris et le combat. L'exécuteur, lui,
    /// ne sait pas d'où vient l'ordre — d'où la possibilité de brancher une IA à sa place.
    ///
    /// Schéma par défaut :
    ///   Clic gauche              → direct (alterne gauche / droite)
    ///   Ctrl + clic gauche       → crochet
    ///   Alt  + clic gauche       → uppercut
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private AttackExecutor _executor;
        [SerializeField] private PlayerMotor _motor;

        [Header("Coups")]
        [SerializeField] private AttackData _straight;
        [SerializeField] private AttackData _hook;
        [SerializeField] private AttackData _uppercut;

        [Header("Effet sur le deplacement")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Vitesse conservee pendant un coup. On ne court pas en frappant.")]
        private float _attackSpeedMultiplier = 0.42f;

        [SerializeField, Min(0.5f)] private float _speedRecovery = 4f;

        private float _currentSpeedMultiplier = 1f;

        private void Awake()
        {
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
        }

        private void Update()
        {
            if (_input == null || _executor == null) return;

            if (_input.AttackPressed) TryAttack();
            UpdateMovementPenalty();
        }

        private void TryAttack()
        {
            AttackData attack = SelectAttack();
            if (attack != null) _executor.TryPlay(attack);
        }

        private AttackData SelectAttack()
        {
            if (_input.AttackModifierAltHeld && _uppercut != null) return _uppercut;
            if (_input.AttackModifierHeld && _hook != null) return _hook;
            return _straight;
        }

        private void UpdateMovementPenalty()
        {
            if (_motor == null) return;

            float target = _executor.IsAttacking ? _attackSpeedMultiplier : 1f;

            _currentSpeedMultiplier = _executor.IsAttacking
                ? target
                : Mathf.MoveTowards(_currentSpeedMultiplier, 1f, _speedRecovery * Time.deltaTime);

            _motor.SpeedMultiplier = _currentSpeedMultiplier;
        }
    }
}
