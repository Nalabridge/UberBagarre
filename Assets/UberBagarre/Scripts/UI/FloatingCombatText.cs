using System.Collections.Generic;
using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Chiffres de dégâts qui s'envolent au-dessus de la cible, et compteur de combo.
    ///
    /// Sans chiffre affiché, le joueur ne sait pas si un crochet fait vraiment plus mal qu'un
    /// direct : il le suppose. Le voir écrit rend l'équilibrage lisible en jouant, sans ouvrir
    /// la console.
    ///
    /// Les coups à la tête et les coups lourds sont écrits plus gros et d'une autre couleur :
    /// l'information « tu as bien placé ce coup » doit se lire sans lire le chiffre.
    ///
    /// La ZONE touchée est écrite sous le chiffre, et ce n'est pas de la décoration. Trois zones
    /// avec trois multiplicateurs différents ne servent à rien si le joueur ne peut pas savoir
    /// laquelle il vient d'atteindre : il verrait seulement des chiffres qui varient sans
    /// comprendre pourquoi, et ne pourrait donc jamais apprendre à viser. Même raison pour
    /// « BLOQUE » : sans ce mot, un coup absorbé à 72 % ressemble à un coup mal placé.
    /// </summary>
    public class FloatingCombatText : MonoBehaviour
    {
        private struct Entry
        {
            public Vector3 WorldPosition;
            public float Amount;
            public float Age;
            public bool Heavy;
            public bool Head;
            public bool Blocked;
            public HitZone Zone;
            public float HorizontalDrift;
        }

        [Header("References")]
        [SerializeField] private Camera _camera;
        [SerializeField] private Combatant _owner;
        [SerializeField] private ComboTracker _combo;

        [Header("Chiffres de degats")]
        [SerializeField] private bool _showDamageNumbers = true;
        [SerializeField, Min(0.2f)] private float _lifetime = 0.95f;
        [SerializeField, Min(0f)] private float _riseDistance = 0.85f;
        [SerializeField] private int _baseFontSize = 26;
        [SerializeField] private int _heavyFontSize = 38;
        [SerializeField] private Color _normalColor = new Color(1f, 0.96f, 0.85f);
        [SerializeField] private Color _heavyColor = new Color(1f, 0.72f, 0.20f);
        [SerializeField] private Color _headColor = new Color(1f, 0.42f, 0.32f);
        [SerializeField] private Color _legColor = new Color(0.62f, 0.88f, 1f);
        [SerializeField] private Color _blockedColor = new Color(0.65f, 0.72f, 0.80f);

        [SerializeField]
        [Tooltip("Ecrit la zone touchee sous le chiffre. A laisser actif : sans ca, trois zones " +
                 "de degats differents sont indistinguables en jouant.")]
        private bool _showZoneLabel = true;

        [Header("Combo")]
        [SerializeField] private bool _showCombo = true;
        [SerializeField] private Vector2 _comboScreenAnchor = new Vector2(0.82f, 0.36f);
        [SerializeField] private int _comboFontSize = 52;
        [SerializeField] private Color _comboColor = new Color(1f, 0.85f, 0.25f);

        private readonly List<Entry> _entries = new List<Entry>(24);
        private float _comboPop;
        private int _lastComboShown;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;
        }

        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (!_showDamageNumbers || _owner == null) return;

            // Seuls NOS coups sont affiches : voir les degats qu'on encaisse en chiffres
            // brouillerait la lecture, la vignette et le HUD s'en chargent deja.
            if (info.Attacker != _owner.gameObject) return;

            Entry entry = new Entry();
            entry.WorldPosition = info.Point != Vector3.zero ? info.Point : victim.AimPosition;
            entry.Amount = info.Amount;
            entry.Heavy = info.IsHeavy;
            entry.Head = info.Zone == HitZone.Head;
            entry.Blocked = info.Blocked;
            entry.Zone = info.Zone;
            entry.HorizontalDrift = Random.Range(-38f, 38f);

            _entries.Add(entry);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            for (int i = _entries.Count - 1; i >= 0; i--)
            {
                Entry entry = _entries[i];
                entry.Age += dt;

                if (entry.Age >= _lifetime) _entries.RemoveAt(i);
                else _entries[i] = entry;
            }

            if (_combo != null && _combo.Count != _lastComboShown)
            {
                if (_combo.Count > _lastComboShown) _comboPop = 1f;
                _lastComboShown = _combo.Count;
            }

            _comboPop = Mathf.MoveTowards(_comboPop, 0f, dt * 4f);
        }

        private void OnGUI()
        {
            if (_camera == null) _camera = Camera.main;

            DrawDamageNumbers();
            DrawCombo();
        }

        private void DrawDamageNumbers()
        {
            for (int i = 0; i < _entries.Count; i++)
            {
                Entry entry = _entries[i];
                float t = Mathf.Clamp01(entry.Age / _lifetime);

                // Montee qui ralentit : le chiffre jaillit puis s'immobilise en s'effacant.
                float rise = (1f - (1f - t) * (1f - t)) * _riseDistance;

                Vector2 gui;
                if (!GuiKit.WorldToGui(_camera, entry.WorldPosition + Vector3.up * rise, out gui)) continue;

                gui.x += entry.HorizontalDrift * t;

                // Sursaut initial : le chiffre depasse sa taille puis revient. Sans ce pic,
                // un chiffre qui apparait a taille fixe passe inapercu.
                float pop = 1f + Mathf.Max(0f, 1f - t * 6f) * 0.5f;

                // Un coup bloque n'a pas droit au gros chiffre : il ne doit pas se lire comme
                // une reussite.
                bool big = entry.Heavy && !entry.Blocked;
                int size = Mathf.RoundToInt((big ? _heavyFontSize : _baseFontSize) * pop);

                Color color = ZoneColor(entry);
                float alpha = Mathf.Clamp01(1f - Mathf.InverseLerp(0.55f, 1f, t));
                color.a = alpha;

                GUIStyle style = GuiKit.Style(size, FontStyle.Bold, TextAnchor.MiddleCenter);
                Rect rect = new Rect(gui.x - 100f, gui.y - 26f, 200f, 52f);

                string text = Mathf.RoundToInt(entry.Amount).ToString();
                if (entry.Head && !entry.Blocked) text += "!";

                GuiKit.OutlinedLabel(rect, text, style, color, new Color(0f, 0f, 0f, alpha * 0.9f), 2f);

                if (!_showZoneLabel) continue;

                GUIStyle small = GuiKit.Style(Mathf.RoundToInt(13f * pop), FontStyle.Bold, TextAnchor.MiddleCenter);
                Color labelColor = new Color(color.r, color.g, color.b, alpha * 0.9f);

                GuiKit.OutlinedLabel(new Rect(gui.x - 100f, gui.y + 14f, 200f, 20f),
                    entry.Blocked ? "BLOQUE" : ZoneName(entry.Zone), small,
                    labelColor, new Color(0f, 0f, 0f, alpha * 0.9f), 1.5f);
            }
        }

        private Color ZoneColor(Entry entry)
        {
            if (entry.Blocked) return _blockedColor;
            if (entry.Zone == HitZone.Head) return _headColor;
            if (entry.Zone == HitZone.Leg) return _legColor;
            return entry.Heavy ? _heavyColor : _normalColor;
        }

        private static string ZoneName(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return "TETE";
                case HitZone.Leg: return "JAMBES";
                case HitZone.Arm: return "BRAS";
                default: return "CORPS";
            }
        }

        private void DrawCombo()
        {
            if (!_showCombo || _combo == null || _combo.Count < 2) return;

            float pop = 1f + _comboPop * 0.45f;
            float urgency = _combo.Window <= 0f ? 1f : Mathf.Clamp01(_combo.TimeLeft / _combo.Window);

            Vector2 anchor = new Vector2(Screen.width * _comboScreenAnchor.x, Screen.height * _comboScreenAnchor.y);

            GUIStyle big = GuiKit.Style(Mathf.RoundToInt(_comboFontSize * pop), FontStyle.Bold, TextAnchor.MiddleCenter);
            Color color = Color.Lerp(new Color(1f, 0.35f, 0.2f), _comboColor, urgency);

            GuiKit.OutlinedLabel(new Rect(anchor.x - 120f, anchor.y - 40f, 240f, 80f),
                _combo.Count + "  COUPS", big, color, new Color(0f, 0f, 0f, 0.85f), 3f);

            GUIStyle small = GuiKit.Style(20, FontStyle.Bold, TextAnchor.MiddleCenter);
            GuiKit.OutlinedLabel(new Rect(anchor.x - 120f, anchor.y + 24f, 240f, 30f),
                Mathf.RoundToInt(_combo.TotalDamage) + " degats", small,
                new Color(1f, 1f, 1f, 0.9f), new Color(0f, 0f, 0f, 0.85f), 2f);

            // Jauge de temps restant : le joueur voit le combo lui filer entre les doigts.
            Rect timer = new Rect(anchor.x - 70f, anchor.y + 56f, 140f, 6f);
            GuiKit.Fill(timer, new Color(0f, 0f, 0f, 0.55f));
            GuiKit.Fill(new Rect(timer.x, timer.y, timer.width * urgency, timer.height), color);
        }
    }
}
