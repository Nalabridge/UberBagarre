using System;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Jauge d'étourdissement : encaisser beaucoup de coups d'affilée finit par sonner.
    ///
    /// C'est la mécanique qui manquait pour que matraquer serve à quelque chose d'autre qu'à faire
    /// descendre une barre. Sans elle, un échange est une soustraction : chacun retire des points
    /// de vie à l'autre jusqu'à zéro, et rien de ce qui se passe entre-temps n'a d'importance. La
    /// jauge crée un SECOND axe — la pression — qui se remplit vite, se vide lentement, et qui
    /// récompense l'agressivité soutenue par une vraie ouverture.
    ///
    /// Elle se vide toute seule après un court répit, donc elle punit le matraquage continu sans
    /// punir le combat normal. Et un coup à la tête la remplit deux fois plus : viser haut a
    /// désormais une conséquence au-delà du multiplicateur de dégâts.
    /// </summary>
    public class StunMeter : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private HealthSystem _health;
        [SerializeField] private AttackExecutor _executor;

        [Header("Remplissage")]
        [SerializeField, Min(1f)] private float _max = 100f;

        [SerializeField, Min(0f)]
        [Tooltip("Points de jauge par point de degat encaisse.")]
        private float _perDamage = 2.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Multiplicateur pour un coup a la tete. C'est ce qui donne une raison de viser " +
                 "haut au-dela des degats.")]
        private float _headMultiplier = 2f;

        [SerializeField, Min(0f)]
        [Tooltip("Part conservee quand le coup est bloque : se couvrir protege aussi de l'etourdissement.")]
        private float _blockedMultiplier = 0.3f;

        [Header("Vidange")]
        [SerializeField, Min(0f)] private float _decayPerSecond = 22f;

        [SerializeField, Min(0f)]
        [Tooltip("Repit necessaire avant que la jauge commence a descendre.")]
        private float _decayDelay = 1.1f;

        [Header("Etourdissement")]
        [SerializeField, Min(0.1f)] private float _stunDuration = 1.7f;

        [SerializeField, Min(0f)]
        [Tooltip("Temps mort apres un etourdissement. Sans lui, un adversaire sonne se fait " +
                 "re-sonner immediatement et ne rejoue plus jamais.")]
        private float _immunityAfterStun = 3.5f;

        [Header("Debug")]
        [SerializeField] private bool _logStun;

        private float _value;
        private float _decayBlockedUntil;
        private float _immuneUntil;

        public float Value { get { return _value; } }
        public float Max { get { return _max; } }
        public float Normalized { get { return _max <= 0f ? 0f : Mathf.Clamp01(_value / _max); } }

        /// <summary>Vrai tant que la jauge est dans son temps mort, juste après un étourdissement.</summary>
        public bool Immune { get { return Time.time < _immuneUntil; } }

        /// <summary>Émis quand la jauge se remplit. Les retours visuels et sonores s'y branchent.</summary>
        public event Action Stunned;

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_health == null && _combatant != null) _health = _combatant.Health;
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
            if (Immune) return;

            float gain = info.Amount * _perDamage;

            if (info.Zone == HitZone.Head) gain *= _headMultiplier;
            if (info.Blocked) gain *= _blockedMultiplier;

            _value = Mathf.Min(_max, _value + gain);
            _decayBlockedUntil = Time.time + _decayDelay;

            if (_value < _max) return;

            Trigger();
        }

        /// <summary>Déclenche l'étourdissement et remet la jauge à zéro.</summary>
        public void Trigger()
        {
            _value = 0f;
            _immuneUntil = Time.time + _immunityAfterStun;

            if (_combatant != null) _combatant.State.Enter(CombatantState.Stunned, _stunDuration);
            if (_executor != null && _executor.IsAttacking) _executor.Cancel();

            if (_logStun) Debug.Log("[UberBagarre] " + name + " est SONNE.", this);

            Action stunned = Stunned;
            if (stunned != null) stunned();
        }

        public void Reset()
        {
            _value = 0f;
            _decayBlockedUntil = 0f;
            _immuneUntil = 0f;
        }

        private void Update()
        {
            if (_value <= 0f || Time.time < _decayBlockedUntil) return;

            _value = Mathf.MoveTowards(_value, 0f, _decayPerSecond * Time.deltaTime);
        }
    }
}
