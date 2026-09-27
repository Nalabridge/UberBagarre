using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Les effets de ce qu'on achète en ville : un repas, une bière, des bandes de boxe, des
    /// antidouleurs. Chacun est un bonus de stat qui dure quelques minutes, posé sur les
    /// statistiques du combattant avec sa propre source (il se retire tout seul, sans toucher
    /// aux bonus de l'entraînement).
    ///
    /// Le même effet racheté prolonge la durée au lieu de s'additionner : trois bières ne font
    /// pas un surhomme. Les effets en cours s'affichent en haut à droite, avec leur minuterie.
    /// </summary>
    public class PlayerBuffs : MonoBehaviour
    {
        [SerializeField] private Combatant _combatant;

        private sealed class Active
        {
            public string label;
            public StatType stat;
            public float value;
            public bool percent;
            public float until;
            public string source;
        }

        private readonly List<Active> _active = new List<Active>();
        private float _flash;

        public static PlayerBuffs Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
            if (_combatant == null) _combatant = GetComponent<Combatant>();
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
        }

        /// <summary>
        /// Un bonus pour <paramref name="minutes"/> minutes. <paramref name="amount"/> est en
        /// pour cent pour la puissance, la vitesse et l'endurance ; en points pour la défense.
        /// </summary>
        public void Add(string label, StatType stat, float amount, float minutes)
        {
            if (_combatant == null || _combatant.Stats == null) return;

            bool percent = stat != StatType.Defense && stat != StatType.KnockdownPower;
            float value = stat == StatType.Defense ? amount : amount / 100f;

            for (int i = 0; i < _active.Count; i++)
            {
                if (_active[i].label != label) continue;
                _active[i].until = Mathf.Max(_active[i].until, Time.time) + minutes * 60f;
                _flash = 1f;
                return;
            }

            Active a = new Active
            {
                label = label, stat = stat, value = value, percent = percent,
                until = Time.time + minutes * 60f, source = "Achat:" + label
            };
            _active.Add(a);
            _combatant.Stats.AddModifier(new StatModifier { stat = stat, percent = percent, value = value, source = a.source });
            Refresh(stat);
            _flash = 1f;
        }

        /// <summary>La vie du joueur (0 à 1).</summary>
        public float HealthNormalized
        {
            get { return _combatant != null && _combatant.Health != null ? _combatant.Health.Normalized : 1f; }
        }

        /// <summary>Rend des points de vie (sans dépasser le maximum).</summary>
        public void Heal(float amount)
        {
            if (_combatant != null && _combatant.Health != null) _combatant.Health.Heal(amount);
            _flash = 1f;
        }

        /// <summary>Vie et endurance au maximum.</summary>
        public void HealFull()
        {
            if (_combatant == null) return;
            if (_combatant.Health != null) _combatant.Health.Heal(_combatant.Health.MaxHealth);
            if (_combatant.Stamina != null) _combatant.Stamina.Refill();
            _flash = 1f;
        }

        private void Update()
        {
            _flash = Mathf.MoveTowards(_flash, 0f, Time.deltaTime * 1.5f);

            for (int i = _active.Count - 1; i >= 0; i--)
            {
                if (Time.time < _active[i].until) continue;
                Active a = _active[i];
                _active.RemoveAt(i);
                if (_combatant != null && _combatant.Stats != null) _combatant.Stats.RemoveBySource(a.source);
                Refresh(a.stat);
            }
        }

        /// <summary>
        /// L'endurance maximale est recopiée sur le système d'endurance ; les autres stats sont
        /// lues à chaque coup. (Pas de ApplyStats : il remplirait la vie.)
        /// </summary>
        private void Refresh(StatType stat)
        {
            if (stat != StatType.MaxStamina || _combatant == null || _combatant.Stamina == null) return;
            _combatant.Stamina.MaxStamina = _combatant.Stats.Get(StatType.MaxStamina);
        }

        private void OnGUI()
        {
            if (_active.Count == 0 || ModalScreen.Active || GameMenu.IsOpen) return;

            float u = Mathf.Max(0.6f, Screen.height / 1080f);
            float w = 300f * u;
            float h = 30f * u;
            float x = Screen.width - w - 24f * u;
            float y = 150f * u + PlayerCondition.ChipCount * (h + 6f * u);
            Color green = new Color(0.36f, 0.95f, 0.52f);

            for (int i = 0; i < _active.Count; i++)
            {
                Active a = _active[i];
                float left = Mathf.Max(0f, a.until - Time.time);
                string time = Mathf.FloorToInt(left / 60f) + ":" + Mathf.FloorToInt(left % 60f).ToString("00");
                Rect r = new Rect(x, y + i * (h + 6f * u), w, h);

                GuiKit.Rounded(r, new Color(0.07f, 0.064f, 0.1f, 0.78f + 0.2f * _flash), h * 0.5f);
                GuiKit.RoundedOutline(r, new Color(green.r, green.g, green.b, 0.45f), h * 0.5f, 1f);
                GuiKit.Rounded(new Rect(r.x + 10f * u, r.center.y - 4f * u, 8f * u, 8f * u), green, 4f * u);
                GuiKit.ShadowLabel(new Rect(r.x + 26f * u, r.y, r.width - 96f * u, r.height), a.label,
                    GuiKit.Text(Mathf.RoundToInt(14 * u), GuiKit.Weight.Bold, TextAnchor.MiddleLeft), Color.white, 0.5f);
                GuiKit.ShadowLabel(new Rect(r.xMax - 74f * u, r.y, 60f * u, r.height), time,
                    GuiKit.Text(Mathf.RoundToInt(14 * u), GuiKit.Weight.Medium, TextAnchor.MiddleRight), new Color(1f, 1f, 1f, 0.7f), 0.5f);
            }
        }
    }
}
