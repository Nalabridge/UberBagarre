using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Fait le lien entre l'état du joueur (entrées, déplacement) et l'affichage des mains.
    ///
    /// Pourquoi un composant séparé plutôt que de lire les touches directement dans
    /// FirstPersonHands : parce que l'ennemi utilisera le même système d'animation de bras,
    /// mais piloté par son IA. Si le composant d'affichage lisait le clavier, il serait
    /// inutilisable pour l'ennemi — et pour tout personnage non joueur ensuite.
    /// </summary>
    public class PlayerHandsDriver : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private FirstPersonHands _hands;

        [Header("Reactivite")]
        [SerializeField, Min(0.5f)] private float _guardBlendSpeed = 10f;
        [SerializeField, Min(0.5f)] private float _sprintBlendSpeed = 6f;

        private float _guardWeight;
        private float _sprintWeight;

        private void Awake()
        {
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();

            if (_hands == null)
            {
                Debug.LogError("[UberBagarre] PlayerHandsDriver sur " + name + " : 'Hands' n'est pas assigne.", this);
            }
        }

        private void Update()
        {
            if (_hands == null) return;

            float dt = Time.deltaTime;

            float guardTarget = (_input != null && _input.GuardHeld) ? 1f : 0f;
            _guardWeight = Mathf.MoveTowards(_guardWeight, guardTarget, _guardBlendSpeed * dt);

            float sprintTarget = (_motor != null && _motor.IsSprinting) ? 1f : 0f;
            _sprintWeight = Mathf.MoveTowards(_sprintWeight, sprintTarget, _sprintBlendSpeed * dt);

            _hands.GuardWeight = _guardWeight;
            _hands.SprintWeight = _sprintWeight;

            if (_input != null) _hands.SetLookDelta(_input.LookDelta, dt);

            if (_motor != null)
            {
                Vector3 localVelocity = _motor.transform.InverseTransformDirection(_motor.HorizontalVelocity);
                _hands.SetLocomotion(localVelocity, _motor.NormalizedSpeed);
            }
        }
    }
}
