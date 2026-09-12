using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Point de branchement entre les événements de combat et tout ce qui se voit et s'entend.
    ///
    /// Les systèmes de combat n'appellent jamais la caméra, l'audio ou l'interface : ils
    /// émettent des événements. Cette classe est la seule à connaître les deux mondes.
    /// Conséquence : ajouter un retour d'impact (particules, manette qui vibre, ralenti)
    /// se fera ici, sans toucher une ligne du combat.
    /// </summary>
    public class CombatFeedbackRelay : MonoBehaviour
    {
        [Header("Source")]
        [SerializeField] private AttackExecutor _executor;
        [SerializeField] private Combatant _combatant;

        [Header("Sorties")]
        [SerializeField] private CameraShake _cameraShake;
        [SerializeField] private ImpactAudio _audio;

        [Header("Coup recu")]
        [SerializeField, Min(0f)]
        [Tooltip("Secousse quand c'est NOUS qui encaissons. Toujours plus forte que celle d'un coup porte.")]
        private float _takenHitShake = 0.45f;

        [SerializeField, Min(0f)] private float _takenHitShakeDuration = 0.35f;

        private void OnEnable()
        {
            if (_executor != null)
            {
                _executor.AttackStarted += OnAttackStarted;
                _executor.HitLanded += OnHitLanded;
            }

            if (_combatant != null && _combatant.Health != null)
            {
                _combatant.Health.Damaged += OnDamageTaken;
            }
        }

        private void OnDisable()
        {
            if (_executor != null)
            {
                _executor.AttackStarted -= OnAttackStarted;
                _executor.HitLanded -= OnHitLanded;
            }

            if (_combatant != null && _combatant.Health != null)
            {
                _combatant.Health.Damaged -= OnDamageTaken;
            }
        }

        private void OnAttackStarted(AttackData attack, View.HandSide side)
        {
            if (_audio != null) _audio.PlayWhoosh();
        }

        private void OnHitLanded(AttackData attack, Hurtbox hurtbox, Vector3 point)
        {
            if (_cameraShake != null) _cameraShake.Play(attack.shakeIntensity * 2.2f, attack.shakeDuration);
            if (_audio != null) _audio.PlayImpact(attack.isHeavy);
        }

        private void OnDamageTaken(DamageInfo info)
        {
            // Encaisser doit secouer plus fort que frapper : sinon on ne distingue pas les deux.
            float severity = Mathf.Clamp01(info.Amount / 20f);

            if (_cameraShake != null)
            {
                _cameraShake.Play(_takenHitShake * (0.5f + severity), _takenHitShakeDuration);
            }

            if (_audio != null)
            {
                _audio.PlayImpact(info.IsHeavy);
                _audio.PlayHurt();
            }
        }
    }
}
