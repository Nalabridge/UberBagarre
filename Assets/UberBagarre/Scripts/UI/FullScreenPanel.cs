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

        protected static readonly Color Accent = new Color(1f, 0.2f, 0.55f);
        protected static readonly Color Ink = new Color(0.93f, 0.93f, 0.95f);
        protected static readonly Color Dim = new Color(0.62f, 0.63f, 0.68f);
        protected static readonly Color Good = new Color(0.36f, 0.95f, 0.52f);
        protected static readonly Color Bad = new Color(1f, 0.34f, 0.3f);
        protected static readonly Color Warn = new Color(1f, 0.76f, 0.28f);

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
            float u = Mathf.Max(0.55f, sh / 1080f);

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = _open;

            if (Event.current.type == EventType.Repaint) _registered = 0;

            GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0.015f, 0.015f, 0.03f, 0.9f));
            GuiKit.Fill(new Rect(0f, 0f, sw, 4f * u), Accent);

            float margin = 64f * u;
            GuiKit.OutlinedLabel(new Rect(margin, 34f * u, sw - margin * 2f, 60f * u), Title,
                GuiKit.Style(Mathf.RoundToInt(40 * u), FontStyle.Bold, TextAnchor.MiddleLeft), Ink, Color.black, 1f);

            if (!string.IsNullOrEmpty(Subtitle))
            {
                GuiKit.OutlinedLabel(new Rect(margin, 88f * u, sw - margin * 2f, 26f * u), Subtitle,
                    GuiKit.Style(Mathf.RoundToInt(17 * u), FontStyle.Normal, TextAnchor.MiddleLeft), Dim, Color.black, 1f);
            }

            if (_progress != null)
            {
                GuiKit.OutlinedLabel(new Rect(sw * 0.5f, 34f * u, sw * 0.5f - margin, 60f * u), _progress.Money + " €",
                    GuiKit.Style(Mathf.RoundToInt(34 * u), FontStyle.Bold, TextAnchor.MiddleRight), Good, Color.black, 1f);
            }

            Rect area = new Rect(margin, 130f * u, sw - margin * 2f, sh - 130f * u - 70f * u);
            DrawContent(area, u);

            GuiKit.OutlinedLabel(new Rect(margin, sh - 52f * u, sw - margin * 2f, 30f * u),
                "Flèches / souris : choisir     Entrée / clic : valider     Échap : retour",
                GuiKit.Style(Mathf.RoundToInt(15 * u), FontStyle.Normal, TextAnchor.MiddleLeft), new Color(1f, 1f, 1f, 0.45f), Color.black, 1f);

            if (!string.IsNullOrEmpty(_toast) && Time.unscaledTime < _toastUntil)
            {
                Rect t = new Rect(sw * 0.5f - 320f * u, sh - 110f * u, 640f * u, 40f * u);
                GuiKit.Fill(t, new Color(0f, 0f, 0f, 0.8f));
                GuiKit.Outline(t, 2f, _toastBad ? Bad : Good);
                GuiKit.OutlinedLabel(t, _toast, GuiKit.Style(Mathf.RoundToInt(18 * u), FontStyle.Bold, TextAnchor.MiddleCenter),
                    _toastBad ? Bad : Good, Color.black, 1f);
            }

            if (Event.current.type == EventType.Repaint)
            {
                _count = _registered;
                if (_count > 0 && _focus >= _count) _focus = _count - 1;
                if (_confirm) _confirm = false;
            }

            GuiKit.Alpha = previous;
        }

        /// <summary>
        /// Un bouton. Il prend le focus au survol (ou aux flèches) et renvoie vrai quand on
        /// l'active (clic, ou Entrée quand il a le focus). Désactivé, il s'affiche grisé et
        /// refuse avec un son.
        /// </summary>
        protected bool Button(Rect rect, string label, string detail, bool enabled, float u)
        {
            int index = _registered;
            if (Event.current.type == EventType.Repaint) _registered++;

            Vector2 mouse = Event.current.mousePosition;
            bool hover = rect.Contains(mouse);
            if (hover && Event.current.type == EventType.Repaint && (mouse - _lastMouse).sqrMagnitude > 0.5f)
            {
                if (_focus != index) Play(_tick);
                _focus = index;
            }

            if (Event.current.type == EventType.Repaint) _lastMouse = mouse;

            bool focused = _focus == index;

            GuiKit.Fill(rect, focused ? new Color(Accent.r, Accent.g, Accent.b, enabled ? 0.28f : 0.12f) : new Color(1f, 1f, 1f, 0.05f));
            if (focused) GuiKit.Fill(new Rect(rect.x, rect.y, 4f * u, rect.height), Accent);

            Color ink = enabled ? Ink : new Color(1f, 1f, 1f, 0.35f);
            bool two = !string.IsNullOrEmpty(detail);
            GuiKit.OutlinedLabel(new Rect(rect.x + 16f * u, rect.y + (two ? 6f * u : 0f), rect.width - 24f * u, two ? rect.height * 0.5f : rect.height),
                label, GuiKit.Style(Mathf.RoundToInt(19 * u), FontStyle.Bold, TextAnchor.MiddleLeft), ink, Color.black, 1f);

            if (two)
            {
                GuiKit.OutlinedLabel(new Rect(rect.x + 16f * u, rect.y + rect.height * 0.48f, rect.width - 24f * u, rect.height * 0.45f),
                    detail, GuiKit.Style(Mathf.RoundToInt(14 * u), FontStyle.Normal, TextAnchor.MiddleLeft, true),
                    enabled ? Dim : new Color(1f, 1f, 1f, 0.3f), Color.black, 1f);
            }

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
            GuiKit.OutlinedLabel(rect, text, GuiKit.Style(Mathf.RoundToInt(size * u), style, anchor, wrap), color, Color.black, 1f);
        }

        private void Play(AudioClip clip)
        {
            if (_audio != null && clip != null) _audio.PlayOneShot(clip, 0.5f);
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
