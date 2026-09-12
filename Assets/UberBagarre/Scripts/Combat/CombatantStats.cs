using System;
using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>Les statistiques améliorables d'un combattant.</summary>
    public enum StatType
    {
        MaxHealth = 0,
        MaxStamina = 1,
        StaminaRegen = 2,
        Strength = 3,
        AttackSpeed = 4,
        Defense = 5,
        MoveSpeed = 6,
        DodgeDistance = 7,
        DodgeCooldown = 8
    }

    /// <summary>Un bonus de stat. Plat ou en pourcentage, avec une source pour pouvoir le retirer.</summary>
    [Serializable]
    public struct StatModifier
    {
        public StatType stat;
        public bool percent;
        public float value;
        public string source;
    }

    /// <summary>Valeurs de base d'un archétype de combattant. Partageable entre plusieurs ennemis.</summary>
    [CreateAssetMenu(fileName = "Stats", menuName = "Uber Bagarre/Statistiques", order = 2)]
    public class CombatantStatsData : ScriptableObject
    {
        public float maxHealth = 100f;
        public float maxStamina = 100f;
        public float staminaRegen = 22f;

        [Tooltip("Augmente les degats infliges.")]
        public float strength = 10f;

        [Tooltip("Multiplie la vitesse d'execution des coups.")]
        public float attackSpeed = 1f;

        [Tooltip("Reduit les degats recus, par rendement decroissant.")]
        public float defense = 10f;

        public float moveSpeed = 1f;
        public float dodgeDistance = 1f;
        public float dodgeCooldown = 1f;
    }

    /// <summary>
    /// Statistiques d'un combattant : valeurs de base + modificateurs.
    ///
    /// Aucun système ne code une valeur en dur. Les dégâts d'un coup sont ceux de l'attaque,
    /// pondérés par la force de celui qui frappe et la défense de celui qui encaisse. Le jour
    /// où il y aura des améliorations, des équipements ou des buffs, ils ajouteront des
    /// modificateurs ici et tout le reste suivra sans être modifié.
    /// </summary>
    public class CombatantStats : MonoBehaviour
    {
        [SerializeField] private CombatantStatsData _baseStats;

        [Header("Surcharge locale (si pas d'asset)")]
        [SerializeField] private float _fallbackMaxHealth = 100f;
        [SerializeField] private float _fallbackMaxStamina = 100f;
        [SerializeField] private float _fallbackStrength = 10f;
        [SerializeField] private float _fallbackDefense = 10f;

        private readonly List<StatModifier> _modifiers = new List<StatModifier>();

        /// <summary>
        /// Valeurs de base écrasées à l'exécution.
        ///
        /// Distinct des modificateurs, et c'est important : un modificateur s'AJOUTE à la base et
        /// sert aux bonus d'équipement ou de buff. Un override REMPLACE la base, ce qui est la
        /// seule façon correcte de répondre à « mets-moi 250 PV » — avec un modificateur il
        /// faudrait connaître la base pour calculer l'écart, et le réglage casserait dès que la
        /// base change.
        /// </summary>
        private readonly Dictionary<StatType, float> _overrides = new Dictionary<StatType, float>();

        public event Action Changed;

        public float Get(StatType stat)
        {
            float value = Base(stat);
            float percent = 0f;

            for (int i = 0; i < _modifiers.Count; i++)
            {
                StatModifier modifier = _modifiers[i];
                if (modifier.stat != stat) continue;

                if (modifier.percent) percent += modifier.value;
                else value += modifier.value;
            }

            return value * (1f + percent);
        }

        /// <summary>Remplace la valeur de base d'une stat. Les modificateurs continuent de s'y ajouter.</summary>
        public void SetOverride(StatType stat, float value)
        {
            _overrides[stat] = value;
            Raise();
        }

        public void ClearOverride(StatType stat)
        {
            if (_overrides.Remove(stat)) Raise();
        }

        public void ClearAllOverrides()
        {
            if (_overrides.Count == 0) return;

            _overrides.Clear();
            Raise();
        }

        public bool HasOverride(StatType stat)
        {
            return _overrides.ContainsKey(stat);
        }

        public void AddModifier(StatModifier modifier)
        {
            _modifiers.Add(modifier);
            Raise();
        }

        public void RemoveBySource(string source)
        {
            _modifiers.RemoveAll(m => m.source == source);
            Raise();
        }

        private void Raise()
        {
            Action changed = Changed;
            if (changed != null) changed();
        }

        private float Base(StatType stat)
        {
            float overridden;
            if (_overrides.TryGetValue(stat, out overridden)) return overridden;

            if (_baseStats != null)
            {
                switch (stat)
                {
                    case StatType.MaxHealth: return _baseStats.maxHealth;
                    case StatType.MaxStamina: return _baseStats.maxStamina;
                    case StatType.StaminaRegen: return _baseStats.staminaRegen;
                    case StatType.Strength: return _baseStats.strength;
                    case StatType.AttackSpeed: return _baseStats.attackSpeed;
                    case StatType.Defense: return _baseStats.defense;
                    case StatType.MoveSpeed: return _baseStats.moveSpeed;
                    case StatType.DodgeDistance: return _baseStats.dodgeDistance;
                    case StatType.DodgeCooldown: return _baseStats.dodgeCooldown;
                }
            }

            switch (stat)
            {
                case StatType.MaxHealth: return _fallbackMaxHealth;
                case StatType.MaxStamina: return _fallbackMaxStamina;
                case StatType.Strength: return _fallbackStrength;
                case StatType.Defense: return _fallbackDefense;
                case StatType.StaminaRegen: return 22f;
                default: return 1f;
            }
        }
    }

    /// <summary>
    /// Le calcul des dégâts, à un seul endroit.
    ///
    /// Le centraliser garantit qu'un futur coup de pied, une arme ou une attaque ennemie
    /// utiliseront exactement la même formule — et qu'équilibrer le jeu se fera ici, pas dans
    /// quinze fichiers.
    /// </summary>
    public static class DamageCalculator
    {
        /// <summary>Référence de défense. Plus elle est haute, moins la défense compte.</summary>
        private const float DefenceScale = 60f;

        public static float ComputeOutgoing(AttackData attack, CombatantStats attacker)
        {
            if (attack == null) return 0f;

            float strength = attacker != null ? attacker.Get(StatType.Strength) : 10f;

            // La force ajoute un pourcentage plutot qu'un plat : un jab ne devient jamais
            // aussi puissant qu'un uppercut juste parce qu'on est fort.
            return attack.damage * (1f + strength * 0.01f);
        }

        public static float ApplyDefence(float incoming, CombatantStats defender)
        {
            if (defender == null) return incoming;

            float defence = Mathf.Max(0f, defender.Get(StatType.Defense));

            // Rendement decroissant : la defense reduit sans jamais annuler.
            return incoming * (DefenceScale / (DefenceScale + defence));
        }
    }
}
