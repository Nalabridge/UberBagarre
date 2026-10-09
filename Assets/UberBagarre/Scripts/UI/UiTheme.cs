using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// La charte de l'interface : une seule palette, une seule échelle de textes, les mêmes
    /// panneaux partout (carte, magasins, menus, HUD).
    ///
    /// L'idée : de la sobriété façon jeu fini, pas d'arcade. Des panneaux sombres et
    /// légèrement translucides (on devine la ville derrière), un texte blanc cassé, une seule
    /// couleur d'accent chaude (l'ambre du GPS), et la couleur ne sert qu'à porter une
    /// information (vie, danger, catégorie d'un lieu) — jamais à décorer.
    /// </summary>
    public static class UiTheme
    {
        // --- surfaces
        public static readonly Color Backdrop = new Color(0.015f, 0.02f, 0.03f, 0.78f);
        public static readonly Color Panel = new Color(0.055f, 0.062f, 0.078f, 0.9f);
        public static readonly Color PanelSoft = new Color(0.09f, 0.1f, 0.12f, 0.82f);
        public static readonly Color Raised = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color Hover = new Color(1f, 1f, 1f, 0.09f);
        public static readonly Color Line = new Color(1f, 1f, 1f, 0.08f);
        public static readonly Color Shadow = new Color(0f, 0f, 0f, 0.45f);

        // --- texte
        public static readonly Color Ink = new Color(0.95f, 0.95f, 0.96f, 1f);
        public static readonly Color InkDim = new Color(0.66f, 0.69f, 0.74f, 1f);
        public static readonly Color InkFaint = new Color(0.45f, 0.48f, 0.53f, 1f);

        // --- sens
        public static readonly Color Accent = new Color(1f, 0.74f, 0.29f, 1f);
        public static readonly Color Good = new Color(0.38f, 0.85f, 0.52f, 1f);
        public static readonly Color Bad = new Color(0.94f, 0.3f, 0.28f, 1f);
        public static readonly Color Info = new Color(0.4f, 0.7f, 1f, 1f);

        /// <summary>L'échelle de l'interface : 1 en 1080p, multipliée par la taille choisie dans les réglages.</summary>
        public static float Unit { get { return Screen.height / 1080f * Core.GameSettings.UiScale; } }

        // ------------------------------------------------------------------ disposition du HUD

        /// <summary>La mini-carte : en bas à gauche, en paysage, comme dans GTA.</summary>
        public static Rect MinimapRect()
        {
            float u = Unit;
            float w = 300f * u, h = 188f * u;
            return new Rect(30f * u, Screen.height - 30f * u - 18f * u - h, w, h);
        }

        /// <summary>Le coin haut droit : l'argent, puis ce qui s'empile dessous (niveau, étoiles).</summary>
        public static float TopRight(float line)
        {
            return 26f * Unit + line * Unit;
        }

        /// <summary>Une taille de texte à l'échelle de l'écran (en pixels 1080p).</summary>
        public static int Size(float px)
        {
            return Mathf.Max(8, Mathf.RoundToInt(px * Unit));
        }

        public static GUIStyle Title(float px) { return GuiKit.Text(Size(px), GuiKit.Weight.Black, TextAnchor.MiddleLeft); }
        public static GUIStyle Heading(float px) { return GuiKit.Text(Size(px), GuiKit.Weight.Bold, TextAnchor.MiddleLeft); }
        public static GUIStyle Body(float px) { return GuiKit.Text(Size(px), GuiKit.Weight.Medium, TextAnchor.MiddleLeft); }
        public static GUIStyle Light(float px) { return GuiKit.Text(Size(px), GuiKit.Weight.Regular, TextAnchor.MiddleLeft); }

        public static GUIStyle Text(float px, GuiKit.Weight weight, TextAnchor anchor)
        {
            return GuiKit.Text(Size(px), weight, anchor);
        }

        /// <summary>Un panneau : ombre douce, fond sombre, filet clair.</summary>
        public static void DrawPanel(Rect rect, float radius)
        {
            GuiKit.Glow(rect, new Color(0f, 0f, 0f, 0.35f), radius, 18f * Unit);
            GuiKit.Rounded(rect, Panel, radius);
            GuiKit.RoundedOutline(rect, Line, radius, 1f);
        }

        /// <summary>Un texte simple (sans contour), à la couleur donnée.</summary>
        public static void Label(Rect rect, string text, GUIStyle style, Color color)
        {
            Color saved = GUI.contentColor;
            GUI.contentColor = new Color(color.r, color.g, color.b, color.a * GuiKit.Alpha);
            GUI.Label(rect, text, style);
            GUI.contentColor = saved;
        }

        /// <summary>Une touche du clavier dessinée comme un cabochon, suivie de ce qu'elle fait.</summary>
        public static float KeyHint(float x, float y, string key, string action, float unit)
        {
            GUIStyle keyStyle = Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle actionStyle = Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            float h = 22f * unit;
            float w = Mathf.Max(h, keyStyle.CalcSize(new GUIContent(key)).x + 12f * unit);
            Rect cap = new Rect(x, y, w, h);
            GuiKit.Rounded(cap, new Color(1f, 1f, 1f, 0.9f), 4f * unit);
            Label(cap, key, keyStyle, new Color(0.06f, 0.07f, 0.09f));
            float aw = actionStyle.CalcSize(new GUIContent(action)).x;
            Label(new Rect(cap.xMax + 7f * unit, y, aw + 4f, h), action, actionStyle, InkDim);
            return cap.xMax + 7f * unit + aw + 22f * unit;
        }

        /// <summary>Le nom court d'une touche, en français (« Maj », « Espace », « Clic droit »).</summary>
        public static string KeyName(Core.InputBinding binding)
        {
            if (binding.source == Core.InputSource.MouseButton)
            {
                switch (binding.mouseButton)
                {
                    case 0: return "Clic";
                    case 1: return "Clic droit";
                    case 2: return "Molette";
                    default: return "Souris " + binding.mouseButton;
                }
            }

            KeyCode key = binding.key != KeyCode.None ? binding.key : binding.alternateKey;
            return KeyName(key);
        }

        public static string KeyName(KeyCode key)
        {
            switch (key)
            {
                case KeyCode.None: return "—";
                case KeyCode.Space: return "Espace";
                case KeyCode.LeftShift: return "Maj";
                case KeyCode.RightShift: return "Maj D";
                case KeyCode.LeftControl: return "Ctrl";
                case KeyCode.RightControl: return "Ctrl D";
                case KeyCode.LeftAlt: return "Alt";
                case KeyCode.RightAlt: return "Alt Gr";
                case KeyCode.Escape: return "Échap";
                case KeyCode.Return: return "Entrée";
                case KeyCode.KeypadEnter: return "Entrée";
                case KeyCode.Backspace: return "Retour";
                case KeyCode.Tab: return "Tab";
                case KeyCode.UpArrow: return "↑";
                case KeyCode.DownArrow: return "↓";
                case KeyCode.LeftArrow: return "←";
                case KeyCode.RightArrow: return "→";
                case KeyCode.Mouse0: return "Clic";
                case KeyCode.Mouse1: return "Clic droit";
                case KeyCode.Mouse2: return "Molette";
                case KeyCode.Equals: return "=";
                case KeyCode.Minus: return "-";
                case KeyCode.CapsLock: return "Verr. Maj";
                case KeyCode.Delete: return "Suppr";
                case KeyCode.Insert: return "Inser";
            }

            string name = key.ToString();
            if (name.StartsWith("Alpha")) return name.Substring(5);
            if (name.StartsWith("Keypad")) return "Pavé " + name.Substring(6);
            return name;
        }

        /// <summary>
        /// La hauteur occupée en haut à gauche par la fiche de course (l'objectif se range
        /// dessous). Remise à zéro par celui qui la dessine, à chaque image.
        /// </summary>
        public static float TopLeftUsed;

        /// <summary>
        /// L'invite d'action sous le réticule : une touche, ce qu'elle fait, et une précision
        /// facultative. Jamais sur le réticule : elle ne doit pas masquer ce qu'on vise.
        /// </summary>
        public static void DrawPrompt(string keyText, string label, string hint, float visibility)
        {
            float u = Unit;
            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * visibility;

            GUIStyle keyStyle = Text(13f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle labelStyle = Text(16f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            GUIStyle hintStyle = Text(12.5f, GuiKit.Weight.Regular, TextAnchor.MiddleLeft);

            float keyW = Mathf.Max(26f * u, keyStyle.CalcSize(new GUIContent(keyText)).x + 14f * u);
            float textW = Mathf.Max(labelStyle.CalcSize(new GUIContent(label)).x,
                string.IsNullOrEmpty(hint) ? 0f : Mathf.Min(420f * u, hintStyle.CalcSize(new GUIContent(hint)).x));
            float width = keyW + textW + 34f * u;
            float height = string.IsNullOrEmpty(hint) ? 38f * u : 56f * u;

            float x = Screen.width * 0.5f - width * 0.5f;
            float y = Screen.height * 0.5f + 44f * u + (1f - visibility) * 8f * u;
            Rect panel = new Rect(x, y, width, height);
            GuiKit.Glow(panel, new Color(0f, 0f, 0f, 0.3f), 8f * u, 10f * u);
            GuiKit.Rounded(panel, new Color(0.04f, 0.045f, 0.055f, 0.82f), 8f * u);

            Rect key = new Rect(x + 10f * u, y + 9f * u, keyW, 22f * u);
            GuiKit.Rounded(key, new Color(1f, 1f, 1f, 0.92f), 4f * u);
            Label(key, keyText, keyStyle, new Color(0.06f, 0.07f, 0.09f));
            Label(new Rect(key.xMax + 12f * u, y + 8f * u, textW + 8f, 24f * u), label, labelStyle, Ink);

            if (!string.IsNullOrEmpty(hint))
            {
                Label(new Rect(key.xMax + 12f * u, y + 31f * u, textW + 8f, 18f * u), hint, hintStyle, InkDim);
            }

            GuiKit.Alpha = previous;
        }

        /// <summary>Un message bref au centre haut de l'écran (refus, alerte) : une pastille, un point de couleur.</summary>
        public static void DrawToast(string text, Color tone, float y, float alpha)
        {
            float u = Unit;
            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * alpha;
            GUIStyle style = Text(16f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            float w = Mathf.Min(Screen.width - 60f * u, style.CalcSize(new GUIContent(text)).x + 52f * u);
            Rect r = new Rect((Screen.width - w) * 0.5f, y, w, 38f * u);
            GuiKit.Glow(r, new Color(0f, 0f, 0f, 0.3f), 8f * u, 10f * u);
            GuiKit.Rounded(r, new Color(0.04f, 0.045f, 0.055f, 0.86f), 8f * u);
            GuiKit.Rounded(new Rect(r.x + 16f * u, r.center.y - 4f * u, 8f * u, 8f * u), tone, 4f * u);
            Label(new Rect(r.x + 34f * u, r.y, w - 44f * u, r.height), text, style, Ink);
            GuiKit.Alpha = previous;
        }

        private static Vector2 _mouseLast;
        private static int _mouseFrame = -1;
        private static bool _mouseMoved;

        /// <summary>
        /// La souris a bougé depuis l'image précédente : le survol peut prendre la sélection
        /// (sans voler celle du clavier quand la souris est immobile sous la liste).
        /// </summary>
        public static bool MouseMoved
        {
            get
            {
                Event e = Event.current;
                if (e == null) return false;
                if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag) return true;
                if (e.type == EventType.Repaint && Time.frameCount != _mouseFrame)
                {
                    Vector2 m = GUIUtility.GUIToScreenPoint(e.mousePosition);
                    _mouseMoved = (m - _mouseLast).sqrMagnitude > 0.25f;
                    _mouseLast = m;
                    _mouseFrame = Time.frameCount;
                }

                return _mouseMoved && Time.frameCount == _mouseFrame;
            }
        }

        /// <summary>Courbe d'animation : départ vif, arrivée en douceur.</summary>
        public static float EaseOut(float t)
        {
            t = Mathf.Clamp01(t);
            float u = 1f - t;
            return 1f - u * u * u;
        }

        public static float EaseInOut(float t)
        {
            t = Mathf.Clamp01(t);
            return t < 0.5f ? 4f * t * t * t : 1f - Mathf.Pow(-2f * t + 2f, 3f) * 0.5f;
        }

        /// <summary>Luminance perçue : décide d'un texte noir ou blanc sur une couleur.</summary>
        public static float Luma(Color c)
        {
            return c.r * 0.299f + c.g * 0.587f + c.b * 0.114f;
        }
    }
}
