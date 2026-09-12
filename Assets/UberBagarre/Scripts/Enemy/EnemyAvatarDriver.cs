using UberBagarre.Combat;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Enemy
{
    /// <summary>
    /// Équivalent ennemi de PlayerAvatarDriver : traduit l'état de l'ennemi en valeurs
    /// d'animation.
    ///
    /// C'est ici que se vérifie la promesse d'architecture : le corps, les bras, l'IK, le
    /// cycle de marche et l'exécuteur de coups sont EXACTEMENT les mêmes composants que ceux
    /// du joueur. Seule cette classe de pilotage change.
    /// </summary>
    public class EnemyAvatarDriver : MonoBehaviour
    {
        [SerializeField] private EnemyMotor _motor;
        [SerializeField] private Combatant _combatant;
        [SerializeField] private FirstPersonHands _arms;
        [SerializeField] private ProceduralLocomotion _locomotion;

        [SerializeField]
        [Tooltip("Optionnel. S'il est present, la garde VISIBLE correspond a la garde REELLE : " +
                 "le joueur peut alors lire qu'il va se faire bloquer.")]
        private GuardSystem _guard;

        [SerializeField, Min(0.5f)] private float _guardBlendSpeed = 8f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Garde de base d'un ennemi au repos : poings hauts, mais pas encore serres.")]
        private float _idleGuard = 0.35f;

        private float _guardWeight;

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_arms != null)
            {
                // Un ennemi vivant garde les poings hauts en permanence : il est en garde,
                // sauf quand il frappe ou qu'il encaisse. Quand il se couvre REELLEMENT, les
                // poings se collent au menton — c'est le signal que le coup va etre bloque.
                bool ready = _combatant == null || (_combatant.IsAlive && _combatant.CanAct);
                bool covering = _guard != null && _guard.IsGuarding;

                float target = covering ? 1f : (ready ? _idleGuard : 0f);
                _guardWeight = Mathf.MoveTowards(_guardWeight, target, _guardBlendSpeed * dt);
                _arms.GuardWeight = _guardWeight;
            }

            if (_locomotion != null && _motor != null)
            {
                _locomotion.SetState(_motor.HorizontalVelocity, _motor.IsGrounded, 0f, 0f);
            }

            if (_arms != null && _motor != null)
            {
                _arms.SetLocalVelocity(transform.InverseTransformDirection(_motor.HorizontalVelocity));
            }
        }
    }
}
