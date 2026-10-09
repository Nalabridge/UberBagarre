using System;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Points de vie génériques, utilisables par le joueur, les ennemis, un sac de frappe.
    ///
    /// Ce composant ne connaît ni animation, ni interface, ni caméra : il émet des événements.
    /// La vignette rouge, le HUD, la secousse de caméra et la réaction d'animation s'y abonnent
    /// chacun de leur côté. Ajouter un nouveau retour visuel plus tard ne demandera donc aucune
    /// modification ici.
    /// </summary>
    public class HealthSystem : MonoBehaviour, IDamageable
    {
        [SerializeField, Min(1f)] private float _maxHealth = 100f;
        [SerializeField] private bool _invulnerable;

        [Header("Debug")]
        [SerializeField] private bool _logDamage;

        private float _current;
        private int _faction = -1;

        private int FactionOf()
        {
            Combatant combatant = GetComponentInParent<Combatant>();
            return combatant != null ? (int)combatant.Faction : (int)Faction.Neutral;
        }

        public event Action<DamageInfo> Damaged;
        public event Action<DamageInfo> Died;
        public event Action<float> Healed;

        public float MaxHealth { get { return _maxHealth; } }
        public float Current { get { return _current; } }
        public float Normalized { get { return _maxHealth <= 0f ? 0f : Mathf.Clamp01(_current / _maxHealth); } }
        public bool IsAlive { get { return _current > 0f; } }

        public bool Invulnerable
        {
            get { return _invulnerable; }
            set { _invulnerable = value; }
        }

        /// <summary>
        /// Invincibilité du menu de triche. Distincte de <see cref="Invulnerable"/>, que
        /// l'esquive allume et éteint à chaque roulade : partager le même drapeau aurait coupé
        /// le godmode à la première esquive.
        /// </summary>
        public bool GodMode { get; set; }

        private void Awake()
        {
            _current = _maxHealth;
        }

        /// <summary>
        /// Un coup sur un combattant déjà K.O. (au sol) : il ne perd plus de vie, mais le coup
        /// porte quand même — les fractures continuent de compter, la consigne d'un client aussi.
        /// </summary>
        public event Action<DamageInfo> HitWhileDown;

        /// <summary>Même chose, pour tous (la police, les témoins, la réputation).</summary>
        public static event Action<HealthSystem, DamageInfo> AnyHitWhileDown;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyHitWhileDown = null;
        }

        public void ApplyDamage(DamageInfo info)
        {
            if (!IsAlive && !GodMode && info.Amount > 0f)
            {
                Action<DamageInfo> down = HitWhileDown;
                if (down != null) down(info);
                Action<HealthSystem, DamageInfo> any = AnyHitWhileDown;
                if (any != null) any(this, info);
                return;
            }

            if (!IsAlive || _invulnerable || GodMode || info.Amount <= 0f) return;

            // La difficulté choisie : les coups reçus par le joueur, et les siens.
            if (_faction < 0) _faction = FactionOf();
            if (_faction == (int)Faction.Player) info.Amount *= Core.GameSettings.DamageTaken;
            else if (info.AttackerFaction == Faction.Player) info.Amount *= Core.GameSettings.DamageDealt;

            _current = Mathf.Max(0f, _current - info.Amount);

            if (_logDamage)
            {
                Debug.Log("[UberBagarre] " + name + " prend " + info.Amount.ToString("0.0") +
                          " degats (" + info.Zone + "), vie " + _current.ToString("0") + "/" + _maxHealth, this);
            }

            Action<DamageInfo> damaged = Damaged;
            if (damaged != null) damaged(info);

            if (_current <= 0f)
            {
                Action<DamageInfo> died = Died;
                if (died != null) died(info);
            }
        }

        public void Heal(float amount)
        {
            if (amount <= 0f || !IsAlive) return;

            _current = Mathf.Min(_maxHealth, _current + amount);

            Action<float> healed = Healed;
            if (healed != null) healed(amount);
        }

        /// <summary>Change le maximum, typiquement quand les statistiques évoluent.</summary>
        public void SetMaxHealth(float value, bool refill)
        {
            _maxHealth = Mathf.Max(1f, value);
            _current = refill ? _maxHealth : Mathf.Min(_current, _maxHealth);
        }

        [ContextMenu("Remettre a plein")]
        public void ResetToFull()
        {
            _current = _maxHealth;
        }
    }
}
