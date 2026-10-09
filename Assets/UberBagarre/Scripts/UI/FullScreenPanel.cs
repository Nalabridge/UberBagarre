using System.Collections.Generic;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Un écran plein qui s'ouvre sur un meuble : l'armoire (tenue, entraînement), l'ordinateur
    /// (jeux en ligne, paris, immobilier...). Le jeu s'arrête derrière (plus de coups, plus de
    /// déplacement, la souris est libre), Échap referme.
    ///
    /// La base s'occupe de tout ce qui est commun : ouverture et fermeture, verrous, curseur,
    /// navigation au clavier (flèches, Entrée, Échap) ET à la souris (survol, clic), sons, fond.
    /// Les écrans dessinent leur contenu avec <see cref="Button"/> : un bouton reçoit le focus
    /// au survol ou aux flèches, et s'active au clic ou à Entrée.
    /// </summary>
    public abstract class FullScreenPanel : MonoBehaviour
    {
        [SerializeField] protected PlayerInputReader _input;
        [SerializeField] protected CursorLockController _cursor;
        [SerializeField] protected PlayerProgress _progress;

        [SerializeField]
        [Tooltip("Les meubles qui ouvrent cet écran (E dessus). Une armoire, un ordinateur par maison.")]
        private Interactable[] _sources = new Interactable[0];

        private readonly List<Interactable> _attached = new List<Interactable>();

        // La charte commune (UiTheme) : un seul accent, l'ambre ; la couleur porte un sens.
        protected static readonly Color Accent = UiTheme.Accent;
        protected static readonly Color Ink = UiTheme.Ink;
        protected static readonly Color Dim = UiTheme.InkDim;
        protected static readonly Color Good = UiTheme.Good;
        protected static readonly Color Bad = UiTheme.Bad;
        protected static readonly Color Warn = new Color(1f, 0.56f, 0.22f);

        private float _open;
        private int _focus;
        private int _count;
        private int _registered;
        private bool _confirm;
        private Vector2 _lastMouse;
        private string _toast;
        private float _toastUntil;
        private bool _toastBad;

        private AudioSource _audio;
        private AudioClip _tick;
        private AudioClip _ok;
        private AudioClip _deny;
        private AudioClip _cash;

        /// <summary>L'écran ouvert, s'il y en a un (un seul à la fois).</summary>
        public static FullScreenPanel Current { get; private set; }

        public static bool AnyOpen { get { return Current != null; } }

        public bool IsOpen { get; private set; }

        protected int Focus
        {
            get { return _focus; }
            set { _focus = Mathf.Max(0, value); }
        }

        protected float OpenTime { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Current = null;
        }

        protected abstract string Title { get; }

        protected virtual string Subtitle { get { return null; } }

        /// <summary>Le contenu, dans la zone donnée ; <paramref name="u"/> = 1 à 1080 pixels de haut.</summary>
        protected abstract void DrawContent(Rect area, float u);

        /// <summary>
        /// Un écran peut dessiner son propre cadre (l'ordinateur : un bureau et un navigateur) :
        /// il renvoie alors la zone où son contenu se dessine.
        /// </summary>
        protected virtual bool CustomFrame
        {
            get { return false; }
        }

        protected virtual Rect DrawFrame(float sw, float sh, float u)
        {
            return new Rect(64f * u, 130f * u, sw - 128f * u, sh - 200f * u);
        }

        /// <summary>Flèches gauche / droite : à l'écran d'en faire ce qu'il veut (onglets, valeurs).</summary>
        protected virtual void OnHorizontal(int direction)
        {
        }

        /// <summary>Retour : ferme, sauf si l'écran a un niveau à remonter (renvoie vrai).</summary>
        protected virtual bool OnBack()
        {
            return false;
        }

        protected virtual void OnOpened()
        {
        }

        protected virtual void OnClosed()
        {
        }

        protected virtual void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.ignoreListenerPause = true;

            _tick = Tone("Ecran (deplacement)", 1500f, 1500f, 0.03f, 0.35f);
            _ok = Tone("Ecran (validation)", 700f, 1100f, 0.09f, 0.5f);
            _deny = Tone("Ecran (refus)", 320f, 220f, 0.16f, 0.5f);
            _cash = Cash();
        }

        protected virtual void OnDestroy()
        {
            if (Current == this) Current = null;
            if (_tick != null) Destroy(_tick);
            if (_ok != null) Destroy(_ok);
            if (_deny != null) Destroy(_deny);
            if (_cash != null) Destroy(_cash);
        }

        // ------------------------------------------------------------------ ouverture

        public void Open()
        {
            if (IsOpen || AnyOpen || GameMenu.IsOpen) return;

            IsOpen = true;
            Current = this;
            OpenTime = 0f;
            _focus = 0;
            _confirm = false;
            _toast = null;

            ModalScreen.Set(this, true);
            if (_input != null) _input.SetGameplayLock(this, true);

            if (_cursor != null)
            {
                _cursor.enabled = false;
                _cursor.SetLocked(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            Play(_ok);
            OnOpened();
        }

        public void Close()
        {
            if (!IsOpen) return;

            IsOpen = false;
            if (Current == this) Current = null;

            ModalScreen.Set(this, false);
            if (_input != null) _input.SetGameplayLock(this, false);

            if (_cursor != null)
            {
                _cursor.enabled = true;
                _cursor.SetLocked(true);
            }

            Play(_tick);
            OnClosed();
        }

        protected virtual void OnEnable()
        {
            for (int i = 0; i < _sources.Length; i++) Attach(_sources[i]);
        }

        protected virtual void OnDisable()
        {
            if (IsOpen) Close();

            for (int i = 0; i < _attached.Count; i++)
            {
                if (_attached[i] != null) _attached[i].Activated -= OnSourceActivated;
            }

            _attached.Clear();
        }

        /// <summary>Un meuble de plus ouvre cet écran (les maisons achetées en cours de partie).</summary>
        public void Attach(Interactable source)
        {
            if (source == null || _attached.Contains(source)) return;
            _attached.Add(source);
            source.Activated += OnSourceActivated;
        }

        private void OnSourceActivated(Interactable source)
        {
            Open();
        }

        protected virtual void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _open = Mathf.MoveTowards(_open, IsOpen ? 1f : 0f, dt / 0.18f);
            if (!IsOpen) return;

            OpenTime += dt;
            if (OpenTime < 0.1f || _input == null || _input.Provider == null || _input.Bindings == null) return;

            var p = _input.Provider;
            var b = _input.Bindings;

            if (_input.ReleaseCursorPressed || p.GetPressedThisFrame(b.phoneBack))
            {
                if (!OnBack()) Close();
                return;
            }

            if (p.GetPressedThisFrame(b.phoneUp) || p.GetPressedThisFrame(b.moveForward)) Step(-1);
            if (p.GetPressedThisFrame(b.phoneDown) || p.GetPressedThisFrame(b.moveBackward)) Step(1);
            if (p.GetPressedThisFrame(b.phoneLeft) || p.GetPressedThisFrame(b.moveLeft)) OnHorizontal(-1);
            if (p.GetPressedThisFrame(b.phoneRight) || p.GetPressedThisFrame(b.moveRight)) OnHorizontal(1);

            if (p.GetPressedThisFrame(b.phoneSelect) || _input.InteractPressed || p.GetPressedThisFrame(b.jump)) _confirm = true;
        }

        private void Step(int direction)
        {
            if (_count <= 0) return;
            _focus = (_focus + direction + _count) % _count;
            Play(_tick);
        }

        // ------------------------------------------------------------------ dessin

        private void OnGUI()
        {
            if (_open <= 0.001f) return;

            GUI.depth = -45;
            float sw = Screen.width;
            float sh = Screen.height;
            float u = UiTheme.Unit;

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = _open;

            if (Event.current.type == EventType.Repaint) _registered = 0;

            if (CustomFrame)
            {
                DrawContent(DrawFrame(sw, sh, u), u);
            }
            else
            {
                // Le jeu reste deviné derrière : un voile sombre, et un panneau de verre au milieu.
                GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0.01f, 0.012f, 0.018f, 0.8f));

                float width = Mathf.Min(sw - 96f * u, 1640f * u);
                float rise = (1f - UiTheme.EaseOut(OpenTime / 0.25f)) * 20f * u;
                Rect panel = new Rect((sw - width) * 0.5f, 48f * u + rise, width, sh - 130f * u);
                UiTheme.DrawPanel(panel, 14f * u);

                float margin = 40f * u;
                UiTheme.Label(new Rect(panel.x + margin, panel.y + 24f * u, panel.width - margin * 2f, 48f * u), Title, UiTheme.Title(34f), Ink);

                if (!string.IsNullOrEmpty(Subtitle))
                {
                    UiTheme.Label(new Rect(panel.x + margin, panel.y + 70f * u, panel.width - margin * 2f - 260f * u, 24f * u), Subtitle,
                        UiTheme.Body(16f), Dim);
                }

                if (_progress != null)
                {
                    // Le solde, en haut à droite, comme sur le HUD.
                    UiTheme.Label(new Rect(panel.xMax - margin - 300f * u, panel.y + 26f * u, 300f * u, 16f * u), "ARGENT",
                        UiTheme.Text(11f, GuiKit.Weight.Bold, TextAnchor.MiddleRight), UiTheme.InkFaint);
                    UiTheme.Label(new Rect(panel.xMax - margin - 300f * u, panel.y + 42f * u, 300f * u, 34f * u), Euros(_progress.Money),
                        UiTheme.Text(26f, GuiKit.Weight.Bold, TextAnchor.MiddleRight), Ink);
                }

                GuiKit.Fill(new Rect(panel.x + margin, panel.y + 108f * u, panel.width - margin * 2f, 1f), UiTheme.Line);

                Rect area = new Rect(panel.x + margin, panel.y + 126f * u, panel.width - margin * 2f, panel.height - 126f * u - 30f * u);
                DrawContent(area, u);

                DrawHints(sw, sh, u);
            }

            if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime < _toastUntil)
            {
                float fade = Mathf.Clamp01((_toastUntil - Time.unscaledTime) / 0.3f);
                UiTheme.DrawToast(_toast, _toastBad ? Bad : Good, sh - 130f * u, fade);
            }

            if (Event.current.type == EventType.Repaint)
            {
                _count = _registered;
                if (_count > 0 && _focus >= _count) _focus = _count - 1;
                if (_confirm) _confirm = false;
            }

            GuiKit.Alpha = previous;
        }

        /// <summary>Les touches de l'écran, en bas au centre.</summary>
        private void DrawHints(float sw, float sh, float u)
        {
            Core.InputBindings b = _input != null ? _input.Bindings : null;
            string[] keys =
            {
                "↑ ↓", "Choisir",
                b != null ? UiTheme.KeyName(b.phoneSelect) : "Entrée", "Valider",
                b != null ? UiTheme.KeyName(b.releaseCursor) : "Échap", "Retour"
            };

            GUIStyle keyStyle = UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle actionStyle = UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            float total = 0f;
            for (int i = 0; i < keys.Length; i += 2)
            {
                float cap = Mathf.Max(22f * u, keyStyle.CalcSize(new GUIContent(keys[i])).x + 12f * u);
                total += cap + 7f * u + actionStyle.CalcSize(new GUIContent(keys[i + 1])).x + 22f * u;
            }

            float x = (sw - total + 22f * u) * 0.5f;
            for (int i = 0; i < keys.Length; i += 2) x = UiTheme.KeyHint(x, sh - 58f * u, keys[i], keys[i + 1], u);
        }

        /// <summary>« 12 400 € ».</summary>
        protected static string Euros(int value)
        {
            string digits = Mathf.Abs(value).ToString();
            System.Text.StringBuilder text = new System.Text.StringBuilder(digits.Length + 6);
            if (value < 0) text.Append('-');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) text.Append(' ');
                text.Append(digits[i]);
            }

            return text.Append(" €").ToString();
        }

        /// <summary>
        /// Un bouton. Il prend le focus au survol (ou aux flèches) et renvoie vrai quand on
        /// l'active (clic, ou Entrée quand il a le focus). Désactivé, il s'affiche grisé et
        /// refuse avec un son.
        /// </summary>
        protected bool Button(Rect rect, string label, string detail, bool enabled, float u)
        {
            bool focused, hover;
            bool activated = Item(rect, enabled, out focused, out hover);

            float radius = Mathf.Min(8f * u, rect.height * 0.5f);
            if (focused)
            {
                GuiKit.Rounded(rect, new Color(1f, 1f, 1f, enabled ? 0.09f : 0.05f), radius);
                GuiKit.Rounded(new Rect(rect.x, rect.y + rect.height * 0.22f, 3f * u, rect.height * 0.56f),
                    enabled ? Accent : new Color(1f, 1f, 1f, 0.3f), 1.5f * u);
            }
            else
            {
                GuiKit.Rounded(rect, new Color(1f, 1f, 1f, hover ? 0.06f : 0.035f), radius);
            }

            Color ink = enabled ? (focused ? Ink : new Color(0.86f, 0.87f, 0.9f)) : new Color(1f, 1f, 1f, 0.35f);
            bool two = !string.IsNullOrEmpty(detail);
            UiTheme.Label(new Rect(rect.x + 20f * u, rect.y + (two ? 6f * u : 0f), rect.width - 28f * u, two ? rect.height * 0.5f : rect.height),
                label, UiTheme.Text(18f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft), ink);

            if (two)
            {
                UiTheme.Label(new Rect(rect.x + 20f * u, rect.y + rect.height * 0.48f, rect.width - 28f * u, rect.height * 0.45f),
                    detail, GuiKit.Text(UiTheme.Size(14f), GuiKit.Weight.Regular, TextAnchor.MiddleLeft, true),
                    enabled ? Dim : new Color(1f, 1f, 1f, 0.3f));
            }

            return activated;
        }

        /// <summary>
        /// Un élément focalisable sans dessin : l'écran le dessine lui-même. Prend le focus au
        /// survol (ou aux flèches) et renvoie vrai quand on l'active (clic, ou Entrée quand il
        /// a le focus) ; désactivé, il refuse avec un son.
        /// </summary>
        protected bool Item(Rect rect, bool enabled, out bool focused, out bool hover)
        {
            int index = _registered;
            if (Event.current.type == EventType.Repaint) _registered++;

            Vector2 mouse = Event.current.mousePosition;
            hover = rect.Contains(mouse);
            if (hover && Event.current.type == EventType.Repaint && (mouse - _lastMouse).sqrMagnitude > 0.5f)
            {
                if (_focus != index) Play(_tick);
                _focus = index;
            }

            if (Event.current.type == EventType.Repaint) _lastMouse = mouse;

            focused = _focus == index;

            bool clicked = Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover;
            if (clicked) Event.current.Use();

            bool keyed = focused && _confirm && Event.current.type == EventType.Repaint;
            if (keyed) _confirm = false;

            if (!clicked && !keyed) return false;

            if (!enabled)
            {
                Play(_deny);
                return false;
            }

            Play(_ok);
            return true;
        }

        /// <summary>L'index de l'élément qui a le focus (pour l'aperçu de ce qu'on survole).</summary>
        protected int FocusIndex
        {
            get { return _focus; }
        }

        /// <summary>Remet le focus sur le premier élément (changement d'onglet).</summary>
        protected void ResetFocus()
        {
            _focus = 0;
        }

        /// <summary>Un message en bas de l'écran (vert : c'est fait ; rouge : impossible).</summary>
        protected void Toast(string text, bool bad)
        {
            _toast = text;
            _toastBad = bad;
            _toastUntil = Time.unscaledTime + 2.6f;
            if (bad) Play(_deny);
        }

        protected void PlayCash()
        {
            Play(_cash);
        }

        protected void PlayDeny()
        {
            Play(_deny);
        }

        protected void PlayTick()
        {
            Play(_tick);
        }

        protected static void Text(Rect rect, string text, int size, FontStyle style, TextAnchor anchor, Color color, float u, bool wrap = false)
        {
            GuiKit.ShadowLabel(rect, text, GuiKit.Style(Mathf.RoundToInt(size * u), style, anchor, wrap), color, 0.55f);
        }

        private void Play(AudioClip clip)
        {
            if (_audio != null && clip != null) _audio.PlayOneShot(clip, 0.5f * Core.GameSettings.Volume(Core.AudioChannel.Interface));
        }

        // ------------------------------------------------------------------ sons

        private const int Rate = 22050;

        private static AudioClip Tone(string name, float from, float to, float duration, float level)
        {
            int length = Mathf.RoundToInt(duration * Rate);
            float[] data = new float[length];
            float phase = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)length;
                phase += Mathf.Lerp(from, to, t) / Rate;
                phase -= Mathf.Floor(phase);
                data[n] = Mathf.Sin(phase * Mathf.PI * 2f) * Mathf.Clamp01(t * 20f) * (1f - t) * level;
            }

            AudioClip clip = AudioClip.Create(name, length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Tiroir-caisse : deux tintements clairs.</summary>
        private static AudioClip Cash()
        {
            int length = Mathf.RoundToInt(0.5f * Rate);
            float[] data = new float[length];
            float[] notes = { 1760f, 2637f };

            for (int i = 0; i < notes.Length; i++)
            {
                int start = Mathf.RoundToInt(i * 0.09f * Rate);
                for (int n = start; n < length; n++)
                {
                    float t = (n - start) / (float)Rate;
                    data[n] += Mathf.Sin(2f * Mathf.PI * notes[i] * t) * Mathf.Exp(-t * 9f) * 0.3f;
                }
            }

            AudioClip clip = AudioClip.Create("Ecran (caisse)", length, 1, Rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
