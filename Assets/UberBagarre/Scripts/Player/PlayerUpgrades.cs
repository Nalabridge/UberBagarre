using UberBagarre.Combat;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// L'entraînement, appliqué au corps : chaque niveau d'une qualité (Puissance, Endurance,
    /// Encaisse, Vitesse, Technique) ajoute ses bonus aux statistiques du joueur.
    ///
    /// Les bonus passent par les modificateurs de <see cref="CombatantStats"/>, source
    /// « Entrainement » : on les retire tous et on les repose à chaque changement, ce qui
    /// garantit qu'un bonus n'est jamais compté deux fois. Les paliers ont un nom : c'est la
    /// voie d'amélioration affichée dans le vestiaire.
    /// </summary>
    public class PlayerUpgrades : MonoBehaviour
    {
        public struct Perk
        {
            public string Name;
            public string Effect;

            public Perk(string name, string effect)
            {
                Name = name;
                Effect = effect;
            }
        }

        private const string Source = "Entrainement";

        /// <summary>Les cinq voies, cinq paliers chacune (index 0 = premier palier).</summary>
        public static readonly Perk[][] Paths =
        {
            new[]
            {
                new Perk("Poings durs", "+5 de force : chaque coup fait plus mal."),
                new Perk("Frappe lourde", "+5 de force."),
                new Perk("Casse-os", "+5 de force, les fractures viennent plus vite."),
                new Perk("Marteau", "+5 de force."),
                new Perk("Enclume", "+5 de force, +6 % de chance de mettre au sol.")
            },
            new[]
            {
                new Perk("Souffle", "+10 % d'endurance, +12 % de récupération."),
                new Perk("Récup", "+10 % d'endurance, +12 % de récupération."),
                new Perk("Second souffle", "+10 % d'endurance, +12 % de récupération."),
                new Perk("Inépuisable", "+10 % d'endurance, +12 % de récupération."),
                new Perk("Marathonien", "+10 % d'endurance, +12 % de récupération.")
            },
            new[]
            {
                new Perk("Cuir épais", "+8 % de vie, +3 de défense."),
                new Perk("Menton", "+8 % de vie, +3 de défense."),
                new Perk("Increvable", "+8 % de vie, +3 de défense."),
                new Perk("Mur", "+8 % de vie, +3 de défense."),
                new Perk("Roc", "+8 % de vie, +3 de défense.")
            },
            new[]
            {
                new Perk("Vif", "+3 % de vitesse de frappe et de marche, esquive plus tôt."),
                new Perk("Jeu de jambes", "+3 % de vitesse de frappe et de marche, esquive plus tôt."),
                new Perk("Éclair", "+3 % de vitesse de frappe et de marche, esquive plus tôt."),
                new Perk("Insaisissable", "+3 % de vitesse de frappe et de marche, esquive plus tôt."),
                new Perk("Fantôme", "+3 % de vitesse de frappe et de marche, esquive plus tôt.")
            },
            new[]
            {
                new Perk("Économe", "-6 % d'endurance par coup, ripostes +10 %."),
                new Perk("Précis", "-6 % d'endurance par coup, ripostes +10 %, +2 % de chutes."),
                new Perk("Contre", "-6 % d'endurance par coup, ripostes +10 %."),
                new Perk("Vicieux", "-6 % d'endurance par coup, ripostes +10 %, fractures plus faciles."),
                new Perk("Maître", "-6 % d'endurance par coup, ripostes +10 %, +2 % de chutes.")
            }
        };

        public static string StatName(Stat stat)
        {
            switch (stat)
            {
                case Stat.Puissance: return "PUISSANCE";
                case Stat.Endurance: return "ENDURANCE";
                case Stat.Encaisse: return "ENCAISSE";
                case Stat.Vitesse: return "VITESSE";
                default: return "TECHNIQUE";
            }
        }

        public static string StatPitch(Stat stat)
        {
            switch (stat)
            {
                case Stat.Puissance: return "Frapper plus fort. Casser plus vite.";
                case Stat.Endurance: return "Tenir la distance, frapper plus longtemps.";
                case Stat.Encaisse: return "Prendre des coups sans tomber.";
                case Stat.Vitesse: return "Frapper, bouger et esquiver plus vite.";
                default: return "Dépenser moins, contrer mieux.";
            }
        }

        [SerializeField] private PlayerProgress _progress;
        [SerializeField] private Combatant _combatant;
        [SerializeField] private PlayerMotor _motor;

        /// <summary>
        /// Facilité à casser (nez, côtes, jambe) : 0 = normal. Lue par les blessures des cibles.
        /// </summary>
        public static float FractureBonus { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            FractureBonus = 0f;
        }

        private void OnEnable()
        {
            if (_progress != null) _progress.Changed += Apply;
            Apply();
        }

        private void OnDisable()
        {
            if (_progress != null) _progress.Changed -= Apply;
        }

        private int _applied = -1;

        /// <summary>Repose tous les bonus d'après les niveaux actuels.</summary>
        public void Apply()
        {
            if (_progress == null || _combatant == null || _combatant.Stats == null) return;

            int power = _progress.StatLevel(Stat.Puissance);
            int endurance = _progress.StatLevel(Stat.Endurance);
            int toughness = _progress.StatLevel(Stat.Encaisse);
            int speed = _progress.StatLevel(Stat.Vitesse);
            int technique = _progress.StatLevel(Stat.Technique);

            // Rien n'a changé : on ne touche pas aux stats (et on ne remplit pas la vie).
            int signature = power + endurance * 6 + toughness * 36 + speed * 216 + technique * 1296;
            if (signature == _applied) return;
            _applied = signature;

            CombatantStats stats = _combatant.Stats;
            stats.RemoveBySource(Source);

            Add(stats, StatType.Strength, false, 5f * power);
            Add(stats, StatType.KnockdownPower, false, (power >= 5 ? 0.06f : 0f) + (technique >= 2 ? 0.02f : 0f) + (technique >= 5 ? 0.02f : 0f));

            Add(stats, StatType.MaxStamina, true, 0.10f * endurance);
            Add(stats, StatType.MaxHealth, true, 0.08f * toughness);
            Add(stats, StatType.Defense, false, 3f * toughness);

            Add(stats, StatType.AttackSpeed, true, 0.03f * speed);
            Add(stats, StatType.DodgeCooldown, true, -0.05f * speed);

            Add(stats, StatType.StaminaEfficiency, true, -0.06f * technique);
            Add(stats, StatType.RiposteBonus, false, 0.10f * technique);

            FractureBonus = (power >= 3 ? 0.25f : 0f) + (technique >= 4 ? 0.15f : 0f);
            TargetInjuries.Ease = FractureBonus;

            if (_motor != null) _motor.TrainingMultiplier = 1f + 0.03f * speed;
            if (_combatant.Stamina != null) _combatant.Stamina.RegenMultiplier = 1f + 0.12f * endurance;

            _combatant.ApplyStats();
        }

        private static void Add(CombatantStats stats, StatType stat, bool percent, float value)
        {
            if (Mathf.Abs(value) < 1e-4f) return;
            stats.AddModifier(new StatModifier { stat = stat, percent = percent, value = value, source = Source });
        }
    }
}
