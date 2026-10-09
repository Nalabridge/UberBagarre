using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Story;
using UberBagarre.UI;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// L'état du joueur entre deux bagarres : la faim et les blessures.
    ///
    /// Elles se sentent dans les poings. Un corps blessé frappe moins vite et moins fort, se
    /// déplace plus lourdement ; un ventre vide fatigue plus vite et récupère mal. Et au-delà
    /// d'un seuil, l'appli refuse de t'envoyer (<see cref="CanFight"/>) : deux blessures, un
    /// corps qui tient à peine debout, ou rien mangé depuis trop longtemps.
    ///
    /// On se remet :
    /// - en mangeant (restaurants, cafés, épicerie ; le frigo de la planque, si on a fait des
    ///   courses) ;
    /// - en se soignant : la pharmacie et le cabinet médical, ou la trousse de soins rangée à
    ///   la maison ; une nuit au lit guérit tout, mais creuse l'estomac.
    /// </summary>
    public class PlayerCondition : MonoBehaviour
    {
        private const string Source = "Etat";

        [SerializeField] private Combatant _combatant;
        [SerializeField] private PlayerProgress _progress;

        [SerializeField, Min(1f)]
        [Tooltip("Heures de jeu pour passer de rassasié à affamé (hors combat).")]
        private float _hoursToEmpty = 20f;

        [SerializeField, Min(0f)]
        [Tooltip("Faim en plus pour chaque bagarre gagnée ou perdue.")]
        private float _fightCost = 6f;

        private float _hunger;       // points de faim accumulés, versés par paquets
        private int _appliedKey = -1;
        private float _nextCheck;
        private readonly List<string> _chips = new List<string>(4);
        private readonly List<Color> _chipColors = new List<Color>(4);

        public static PlayerCondition Instance { get; private set; }

        /// <summary>Le nombre de pastilles d'état affichées (les bonus se rangent dessous).</summary>
        public static int ChipCount { get { return Instance != null ? Instance._chips.Count : 0; } }

        public float Satiety { get { return _progress != null ? _progress.Satiety : PlayerProgress.MaxSatiety; } }

        public int Injuries { get { return _progress != null ? _progress.Injuries : 0; } }

        public bool Hungry { get { return Satiety < 30f; } }

        public bool Starving { get { return Satiety < 8f; } }

        private void Awake()
        {
            Instance = this;
            if (_combatant == null) _combatant = GetComponent<Combatant>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        private void Start()
        {
            if (_progress == null) _progress = FindAnyObjectByType<PlayerProgress>();
            LoadingScreen.AddTip("Blessé ou le ventre vide, on frappe moins vite. Deux blessures, et l'appli refuse de t'envoyer.");
            LoadingScreen.AddTip("Le frigo de la planque se remplit à l'épicerie (courses de la semaine). La trousse de soins, à la pharmacie.");
        }

        /// <summary>
        /// Jusqu'où la vie remonte d'elle-même hors combat : un corps blessé ne se répare pas en
        /// marchant (il faut des soins, ou une nuit) ; le ventre vide, rien ne remonte.
        /// </summary>
        public float RegenCap
        {
            get
            {
                if (Starving) return 0f;
                return Mathf.Clamp01(1f - 0.25f * Mathf.Clamp(Injuries, 0, 3)) * (Hungry ? 0.8f : 1f);
            }
        }

        /// <summary>La vitesse à laquelle elle remonte (la faim la ralentit).</summary>
        public float RegenFactor { get { return Starving ? 0f : Hungry ? 0.5f : 1f; } }

        /// <summary>Une bagarre de plus : ça creuse.</summary>
        public void FoughtOnce()
        {
            if (_progress != null) _progress.Starve(_fightCost);
        }

        /// <summary>L'appli accepte-t-elle de t'envoyer ? Sinon, pourquoi.</summary>
        public bool CanFight(out string reason)
        {
            reason = null;
            if (Injuries >= 2)
            {
                reason = "Deux blessures : l'appli ne t'envoie plus. Soigne-toi (pharmacie, cabinet médical, trousse à la maison, ou une nuit au lit).";
                return false;
            }

            HealthSystem health = _combatant != null ? _combatant.Health : null;
            if (health != null && health.Normalized < 0.22f)
            {
                reason = "Tu tiens à peine debout. Mange, soigne-toi, ou dors avant de reprendre une course.";
                return false;
            }

            if (Satiety < 3f)
            {
                reason = "Tu n'as rien mangé depuis trop longtemps : tu ne tiendrais pas un round. Va manger.";
                return false;
            }

            return true;
        }

        private void Update()
        {
            if (_progress == null || GameMenu.IsOpen || ModalScreen.Active || Time.timeScale <= 0f) return;

            // Une minute de jeu par seconde : la faim suit l'heure de la ville.
            _hunger += Time.deltaTime / 60f / _hoursToEmpty * PlayerProgress.MaxSatiety;
            if (_hunger >= 0.5f)
            {
                _progress.Starve(_hunger);
                _hunger = 0f;
            }

            if (Time.time < _nextCheck) return;
            _nextCheck = Time.time + 0.5f;
            Apply();
        }

        /// <summary>Les malus sur les stats du joueur : recalculés seulement quand l'état change.</summary>
        private void Apply()
        {
            if (_combatant == null || _combatant.Stats == null) return;

            int injuries = Mathf.Clamp(Injuries, 0, 3);
            int hunger = Starving ? 2 : Hungry ? 1 : 0;
            bool weak = _combatant.Health != null && _combatant.Health.Normalized < 0.35f;
            int key = injuries * 100 + hunger * 10 + (weak ? 1 : 0);

            RebuildChips(injuries, hunger, weak);
            if (key == _appliedKey) return;
            _appliedKey = key;

            CombatantStats stats = _combatant.Stats;
            stats.RemoveBySource(Source);

            // Chaque blessure : des coups plus lents et moins lourds, un pas plus lourd.
            if (injuries > 0)
            {
                stats.AddModifier(new StatModifier { stat = StatType.AttackSpeed, percent = true, value = -0.11f * injuries, source = Source });
                stats.AddModifier(new StatModifier { stat = StatType.Strength, percent = true, value = -0.08f * injuries, source = Source });
                stats.AddModifier(new StatModifier { stat = StatType.MoveSpeed, percent = true, value = -0.05f * injuries, source = Source });
                stats.AddModifier(new StatModifier { stat = StatType.MaxStamina, percent = true, value = -0.08f * injuries, source = Source });
            }

            // La faim : on s'essouffle, on récupère mal ; affamé, les coups ralentissent aussi.
            if (hunger > 0)
            {
                stats.AddModifier(new StatModifier { stat = StatType.StaminaRegen, percent = true, value = hunger == 2 ? -0.45f : -0.25f, source = Source });
                stats.AddModifier(new StatModifier { stat = StatType.MaxStamina, percent = true, value = hunger == 2 ? -0.2f : -0.1f, source = Source });
                stats.AddModifier(new StatModifier { stat = StatType.AttackSpeed, percent = true, value = hunger == 2 ? -0.14f : -0.06f, source = Source });
                if (hunger == 2) stats.AddModifier(new StatModifier { stat = StatType.Strength, percent = true, value = -0.12f, source = Source });
            }

            // Presque K.O. : les bras pèsent.
            if (weak) stats.AddModifier(new StatModifier { stat = StatType.AttackSpeed, percent = true, value = -0.08f, source = Source });

            if (_combatant.Stamina != null) _combatant.Stamina.MaxStamina = stats.Get(StatType.MaxStamina);
        }

        private void RebuildChips(int injuries, int hunger, bool weak)
        {
            _chips.Clear();
            _chipColors.Clear();

            if (injuries > 0)
            {
                _chips.Add(injuries >= 2 ? "GRAVEMENT BLESSÉ  " + injuries + "/3" : "BLESSÉ  " + injuries + "/3");
                _chipColors.Add(new Color(1f, 0.34f, 0.3f));
            }

            if (hunger > 0)
            {
                _chips.Add(hunger == 2 ? "AFFAMÉ  —  coups ralentis" : "FAIM  —  mange quelque chose");
                _chipColors.Add(new Color(1f, 0.72f, 0.25f));
            }

            if (weak)
            {
                _chips.Add("À BOUT DE FORCES");
                _chipColors.Add(new Color(1f, 0.5f, 0.35f));
            }
        }

        private void OnGUI()
        {
            if (_chips.Count == 0 || ModalScreen.Active || GameMenu.IsOpen || FightIntroPlaying()) return;
            if (Core.GameSettings.Hud == 2 || UberBagarre.View.ShotCamera.Active || FullScreenPanel.AnyOpen) return;

            float u = UiTheme.Unit;
            float y = UiTheme.TopRight(140f);
            for (int i = 0; i < _chips.Count; i++) y = DrawChip(_chips[i], _chipColors[i], null, y, u, 0f);
        }

        /// <summary>
        /// Une pastille d'état, alignée à droite sous l'argent : un point de couleur, le texte, et
        /// éventuellement un minuteur. Rend le haut de la suivante.
        /// </summary>
        public static float DrawChip(string text, Color color, string right, float y, float u, float flash)
        {
            GUIStyle style = UiTheme.Text(13.5f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            GUIStyle timer = UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleRight);
            float h = 26f * u;
            float textWidth = style.CalcSize(new GUIContent(text)).x;
            float timerWidth = string.IsNullOrEmpty(right) ? 0f : timer.CalcSize(new GUIContent(right)).x + 12f * u;
            float w = textWidth + timerWidth + 38f * u;
            Rect r = new Rect(Screen.width - 30f * u - w, y, w, h);

            GuiKit.Rounded(r, new Color(0.04f, 0.045f, 0.06f, 0.72f + 0.2f * flash), 6f * u);
            GuiKit.Rounded(new Rect(r.x + 12f * u, r.center.y - 3.5f * u, 7f * u, 7f * u), color, 3.5f * u);
            UiTheme.Label(new Rect(r.x + 26f * u, r.y, textWidth + 4f, r.height), text, style, UiTheme.Ink);
            if (timerWidth > 0f) UiTheme.Label(new Rect(r.xMax - 12f * u - timerWidth, r.y, timerWidth, r.height), right, timer, UiTheme.InkDim);
            return r.yMax + 6f * u;
        }

        private static bool FightIntroPlaying()
        {
            return UberBagarre.Story.FightIntro.AnyPlaying;
        }

        public void Configure(Combatant combatant, PlayerProgress progress)
        {
            _combatant = combatant;
            _progress = progress;
        }
    }
}
