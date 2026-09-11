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

        [Header("Reactivite")]
        [SerializeField, Min(0.5f)] private float _guardBlendSpeed = 10f;
        [SerializeField, Min(0.5f)] private float _sprintBlendSpeed = 6f;

        private float _guardWeight;
        private float _sprintWeight;

        private void Awake()
        {
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_hands != null)
            {
                float guardTarget = (_input != null && _input.GuardHeld) ? 1f : 0f;
                _guardWeight = Mathf.MoveTowards(_guardWeight, guardTarget, _guardBlendSpeed * dt);

                float sprintTarget = (_motor != null && _motor.IsSprinting) ? 1f : 0f;
                _sprintWeight = Mathf.MoveTowards(_sprintWeight, sprintTarget, _sprintBlendSpeed * dt);

                _hands.GuardWeight = _guardWeight;
                _hands.SprintWeight = _sprintWeight;

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
