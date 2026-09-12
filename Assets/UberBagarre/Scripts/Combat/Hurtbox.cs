using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Zone touchable d'un combattant. À poser sur un objet muni d'un Collider (en trigger).
    ///
    /// Séparer la hurtbox de la vie permet plusieurs zones par personnage — tête, corps, bras —
    /// avec des multiplicateurs différents, tout en ne partageant qu'une seule barre de vie.
    /// </summary>
    public class Hurtbox : MonoBehaviour
    {
        [SerializeField] private HealthSystem _health;

        [SerializeField]
        [Tooltip("Statistiques du defenseur. C'est ici qu'intervient la defense.")]
        private CombatantStats _stats;

        [SerializeField] private Faction _faction = Faction.Enemy;
        [SerializeField] private HitZone _zone = HitZone.Body;

        [SerializeField, Min(0f)]
        [Tooltip("Multiplicateur de degats de cette zone. La tete encaisse plus.")]
        private float _damageMultiplier = 1f;

        public HealthSystem Health { get { return _health; } }
        public Faction Faction { get { return _faction; } }
        public HitZone Zone { get { return _zone; } }
        public float DamageMultiplier { get { return _damageMultiplier; } }

        /// <summary>Identité du combattant touché : sert à ne le compter qu'une fois par attaque.</summary>
        public object Owner
        {
            get { return _health != null ? (object)_health : this; }
        }

        private void Awake()
        {
            if (_health == null) _health = GetComponentInParent<HealthSystem>();
            if (_stats == null) _stats = GetComponentInParent<CombatantStats>();
        }

        public void Receive(DamageInfo info)
        {
            if (_health == null) return;

            info.Zone = _zone;
            info.Amount *= _damageMultiplier;

            // La defense s'applique ici, apres le multiplicateur de zone : une tete reste une
            // tete, mais un combattant resistant encaisse mieux partout.
            info.Amount = DamageCalculator.ApplyDefence(info.Amount, _stats);

            _health.ApplyDamage(info);
        }
    }
}
