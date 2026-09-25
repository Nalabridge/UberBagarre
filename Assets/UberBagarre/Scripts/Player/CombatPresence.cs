using UberBagarre.Combat;
using UberBagarre.Enemy;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Est-on en train de se battre ?
    ///
    /// La réponse change tout ce que voit le joueur. Hors combat, un homme qui rentre chez lui
    /// ne marche pas les poings levés, ne fait pas de roulades et n'a pas de barre de vie sous
    /// les yeux : les mains pendent, le HUD disparaît, l'esquive et la glissade sont rangées.
    /// Dès qu'un adversaire s'engage, la garde monte et tout revient.
    ///
    /// On est en combat quand un adversaire VIVANT et ENGAGÉ (son cerveau tourne, rien ne le
    /// retient) est à portée, ou quand on vient soi-même de frapper, de garder ou d'encaisser :
    /// un joueur qui lance les hostilités d'un coup de poing doit avoir la garde aussitôt, et
    /// elle ne retombe pas au premier pas de recul.
    /// </summary>
    [DefaultExecutionOrder(-50)]
    [DisallowMultipleComponent]
    public class CombatPresence : MonoBehaviour
    {
        [SerializeField] private Combatant _self;
        [SerializeField] private AttackExecutor _executor;
        [SerializeField] private PlayerInputReader _input;

        [SerializeField, Min(1f)]
        [Tooltip("Distance a laquelle un adversaire engage met en combat.")]
        private float _engageRadius = 11f;

        [SerializeField, Min(0f)]
        [Tooltip("Duree pendant laquelle on reste en combat apres sa derniere action ou le dernier coup recu.")]
        private float _linger = 6f;

        [SerializeField, Min(0.1f)] private float _raiseSpeed = 4.5f;
        [SerializeField, Min(0.1f)] private float _lowerSpeed = 1.2f;

        private float _lastAction = -100f;
        private float _nextScan;
        private bool _engaged;

        /// <summary>La présence du joueur, pour les affichages qui n'ont pas de référence câblée.</summary>
        public static CombatPresence Player { get; private set; }

        public bool InCombat { get; private set; }

        /// <summary>0 = hors combat, 1 = en combat, avec une transition (la garde monte vite, redescend lentement).</summary>
        public float Weight { get; private set; }

        /// <summary>Force le mode combat (bac à sable, triche).</summary>
        public bool Forced { get; set; }

        private void Awake()
        {
            if (_self == null) _self = GetComponent<Combatant>();
            if (_executor == null) _executor = GetComponent<AttackExecutor>();
            if (_input == null) _input = GetComponent<PlayerInputReader>();
        }

        private void OnEnable()
        {
            Player = this;
            if (_self != null) _self.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (Player == this) Player = null;
            if (_self != null) _self.Damaged -= OnDamaged;
        }

        private void OnDamaged(Combatant self, DamageInfo info)
        {
            _lastAction = Time.time;
        }

        /// <summary>Signale une action de combat (un coup, une garde) : on entre en combat.</summary>
        public void MarkAction()
        {
            _lastAction = Time.time;
        }

        private void Update()
        {
            if (_executor != null && (_executor.IsAttacking || _executor.IsCharging)) _lastAction = Time.time;
            if (_input != null && _input.GuardHeld) _lastAction = Time.time;

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.2f;
                _engaged = HostileEngaged();
            }

            InCombat = Forced || _engaged || Time.time - _lastAction < _linger;

            float target = InCombat ? 1f : 0f;
            float speed = target > Weight ? _raiseSpeed : _lowerSpeed;
            Weight = Mathf.MoveTowards(Weight, target, speed * Time.deltaTime);
        }

        private bool HostileEngaged()
        {
            if (_self == null || EnemyBrain.HoldAll) return false;

            Vector3 position = transform.position;
            float radius = _engageRadius * _engageRadius;

            var all = Combatant.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant other = all[i];
                if (other == null || other == _self || !other.IsAlive || !other.isActiveAndEnabled) continue;
                if (other.Faction == _self.Faction || other.Faction == Faction.Neutral) continue;
                if ((other.transform.position - position).sqrMagnitude > radius) continue;

                EnemyBrain brain = other.GetComponent<EnemyBrain>();
                if (brain != null && brain.enabled) return true;
            }

            return false;
        }
    }
}
