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

        [SerializeField]
        [Tooltip("Optionnel. Donne a la garde et a la parade un retour qui leur est propre.")]
        private GuardSystem _guard;

        [SerializeField]
        [Tooltip("Optionnel. Le bruit d'un corps qui tombe.")]
        private KnockdownSystem _knockdown;

        [Header("Sorties")]
        [SerializeField] private CameraShake _cameraShake;
        [SerializeField] private ImpactAudio _audio;

        [Header("Coup recu")]
        [SerializeField, Min(0f)]
        [Tooltip("Secousse quand c'est NOUS qui encaissons. Toujours plus forte que celle d'un coup porte.")]
        private float _takenHitShake = 0.28f;

        [SerializeField, Min(0f)] private float _takenHitShakeDuration = 0.25f;

        [Header("Garde")]
        [SerializeField, Min(0f)]
        [Tooltip("Secousse d'un coup bloque : nette mais courte. Le coup est arrete, pas encaisse.")]
        private float _blockShake = 0.10f;

        [SerializeField, Min(0f)]
        [Tooltip("Secousse d'une parade reussie. Franche : c'est un moment fort, il doit se sentir.")]
        private float _parryShake = 0.18f;

        /// <summary>
        /// Vrai pendant la frame où un coup vient d'être paré.
        ///
        /// Une parade annule les dégâts, donc la vie ne bouge pas et rien ne le signale. Ce
        /// drapeau permet à l'interface de l'afficher pendant un court instant.
        /// </summary>
        public float ParryFlash { get; private set; }

        public float BlockFlash { get; private set; }

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

            if (_guard != null)
            {
                _guard.Parried += OnParried;
                _guard.Blocked += OnBlocked;
            }

            if (_knockdown != null) _knockdown.KnockedDown += OnKnockedDown;
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

            if (_guard != null)
            {
                _guard.Parried -= OnParried;
                _guard.Blocked -= OnBlocked;
            }

            if (_knockdown != null) _knockdown.KnockedDown -= OnKnockedDown;
        }

        private void OnKnockedDown()
        {
            if (_audio != null) _audio.PlayFall();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            ParryFlash = Mathf.MoveTowards(ParryFlash, 0f, dt * 1.6f);
            BlockFlash = Mathf.MoveTowards(BlockFlash, 0f, dt * 3f);
        }

        private void OnParried(DamageInfo info)
        {
            ParryFlash = 1f;

            if (_cameraShake != null) _cameraShake.Play(_parryShake, 0.18f);
            if (_audio != null) _audio.PlayParry();
        }

        private void OnBlocked(DamageInfo info)
        {
            BlockFlash = 1f;

            if (_cameraShake != null) _cameraShake.Play(_blockShake, 0.12f);
            if (_audio != null) _audio.PlayBlock();
        }

        private void OnAttackStarted(AttackData attack, View.HandSide side)
        {
            if (_audio != null) _audio.PlayWhoosh();
        }

        private void OnHitLanded(AttackData attack, Hurtbox hurtbox, Vector3 point)
        {
            if (_cameraShake != null) _cameraShake.Play(attack.shakeIntensity * 1.1f, attack.shakeDuration);
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
