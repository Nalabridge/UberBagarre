using UberBagarre.Combat;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Traduit l'état du joueur (entrées, déplacement) en valeurs d'animation.
    ///
    /// C'est la seule pièce qui connaît à la fois le clavier et le corps. Les composants
    /// d'affichage (FirstPersonHands, ProceduralLocomotion) n'ont donc aucune dépendance
    /// aux entrées : l'ennemi réutilisera exactement le même corps, piloté par son IA.
    /// </summary>
    public class PlayerAvatarDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private FirstPersonHands _hands;
        [SerializeField] private ProceduralLocomotion _locomotion;

        [SerializeField]
        [Tooltip("Optionnel. S'il est present, c'est LUI qui dit si la garde est reellement " +
                 "levee : les poings a l'ecran racontent alors la meme chose que les degats " +
                 "encaisses, au lieu de suivre la touche sans condition.")]
        private GuardSystem _guard;

        [SerializeField]
        [Tooltip("Optionnel : hors combat, les mains pendent le long du corps au lieu d'etre en garde.")]
        private CombatPresence _presence;

        [Header("Reactivite")]
        [SerializeField, Min(0.5f)] private float _guardBlendSpeed = 10f;
        [SerializeField, Min(0.5f)] private float _sprintBlendSpeed = 6f;

        private float _guardWeight;
        private float _sprintWeight;

        private void Awake()
        {
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
            if (_guard == null) _guard = GetComponentInParent<GuardSystem>();
            if (_presence == null) _presence = GetComponentInParent<CombatPresence>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_hands != null)
            {
                bool guarding = _guard != null
                    ? _guard.IsGuarding
                    : _input != null && _input.GuardHeld;

                float guardTarget = guarding ? 1f : 0f;

                // La fenetre de parade se voit : les poings se collent instantanement au menton,
                // sans le fondu habituel. Sans ce signal, parer serait un coup de des invisible.
                float blend = _guard != null && _guard.InParryWindow ? _guardBlendSpeed * 3f : _guardBlendSpeed;
                _guardWeight = Mathf.MoveTowards(_guardWeight, guardTarget, blend * dt);

                float sprintTarget = (_motor != null && _motor.IsSprinting) ? 1f : 0f;
                _sprintWeight = Mathf.MoveTowards(_sprintWeight, sprintTarget, _sprintBlendSpeed * dt);

                _hands.GuardWeight = _guardWeight;
                _hands.SprintWeight = _sprintWeight;
                _hands.RelaxedWeight = _presence != null ? 1f - _presence.Weight : 0f;

                if (_input != null) _hands.SetLookDelta(_input.LookDelta, dt);

                if (_motor != null)
                {
                    _hands.SetLocalVelocity(_motor.transform.InverseTransformDirection(_motor.HorizontalVelocity));
                }
            }

            if (_locomotion != null && _motor != null)
            {
                _locomotion.SetState(_motor.HorizontalVelocity, _motor.IsGrounded, _motor.CrouchAmount, _motor.SlideAmount);
            }
        }
    }
}
