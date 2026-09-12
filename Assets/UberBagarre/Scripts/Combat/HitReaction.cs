using UberBagarre.Core;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Réaction du corps à un coup reçu : le buste encaisse, le combattant recule, et il perd
    /// brièvement le contrôle.
    ///
    /// Sans cette perte de contrôle, encaisser n'a aucune conséquence et le combat devient un
    /// échange de dégâts où personne n'est jamais gêné. L'état Hit est prioritaire sur
    /// l'attaque : un coup reçu en pleine préparation annule le coup en cours.
    ///
    /// Les réactions sont graduées : un jab fait tourner la tête, un uppercut casse la posture.
    /// </summary>
    public class HitReaction : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private HealthSystem _health;
        [SerializeField] private ProceduralLocomotion _locomotion;
        [SerializeField] private AttackExecutor _executor;

        [SerializeField]
        [Tooltip("Ce qui encaisse le recul. Doit implementer IImpulseReceiver (moteur du joueur ou de l'ennemi).")]
        private MonoBehaviour _impulseReceiver;

        [Header("Reaction legere")]
        [SerializeField, Min(0f)] private float _lightDuration = 0.20f;
        [SerializeField] private float _lightAngle = 9f;
        [SerializeField, Min(0f)] private float _lightKnockback = 1.1f;

        [Header("Reaction lourde")]
        [SerializeField, Min(0f)] private float _heavyDuration = 0.42f;
        [SerializeField] private float _heavyAngle = 22f;
        [SerializeField, Min(0f)] private float _heavyKnockback = 3.2f;

        [Header("Retour")]
        [SerializeField, Min(0.5f)] private float _recoverySpeed = 6f;

        private IImpulseReceiver _receiver;
        private Vector3 _currentAngle;
        private Vector3 _targetAngle;
        private float _holdTimer;

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_health == null && _combatant != null) _health = _combatant.Health;

            _receiver = _impulseReceiver as IImpulseReceiver;

            if (_impulseReceiver != null && _receiver == null)
            {
                Debug.LogWarning("[UberBagarre] HitReaction sur " + name + " : le composant de recul " +
                                 "n'implemente pas IImpulseReceiver, le recul sera ignore.", this);
            }
        }

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            bool heavy = info.IsHeavy || info.Zone == HitZone.Head;

            float duration = heavy ? _heavyDuration : _lightDuration;
            float angle = heavy ? _heavyAngle : _lightAngle;
            float knockback = heavy ? _heavyKnockback : _lightKnockback;

            if (_combatant != null)
            {
                _combatant.State.TryEnter(CombatantState.Hit, duration);
            }

            // Un coup encaisse interrompt le coup qu'on etait en train de porter.
            if (_executor != null && _executor.IsAttacking) _executor.Cancel();

            Vector3 local = transform.InverseTransformDirection(info.Direction.normalized);

            // La tete part dans le sens du coup : tangage si le coup vient de face,
            // roulis s'il vient de cote.
            _targetAngle = new Vector3(-local.z * angle, local.x * angle * 0.4f, -local.x * angle);
            _holdTimer = duration * 0.5f;

            if (_receiver != null)
            {
                Vector3 push = info.Direction.normalized * (knockback * Mathf.Max(0.4f, info.ImpactForce * 0.25f));
                push.y = 0f;
                _receiver.ApplyImpulse(push);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_holdTimer > 0f) _holdTimer -= dt;
            else _targetAngle = Vector3.MoveTowards(_targetAngle, Vector3.zero, _recoverySpeed * 12f * dt);

            _currentAngle = Vector3.Lerp(_currentAngle, _targetAngle, 1f - Mathf.Exp(-_recoverySpeed * 2f * dt));

            if (_locomotion != null) _locomotion.HitReactionEuler = _currentAngle;
        }
    }
}
