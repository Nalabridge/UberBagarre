using UberBagarre.Player;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Les premiers pas : en bas à gauche, les huit touches qui font le jeu (se déplacer,
    /// regarder, interagir, le téléphone, la carte, courir, frapper, se protéger). Chacune se
    /// coche dès qu'on l'a utilisée, puis s'efface ; le panneau disparaît quand tout est fait.
    /// Montré au début d'une partie tant que le joueur ne l'a pas complété une fois.
    /// </summary>
    public class FirstSteps : MonoBehaviour
    {
        private const string DoneKey = "UberBagarre.PremiersPas";

        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private ScreenFader _fader;

        private struct Step
        {
            public string Key;
            public string Text;
            public bool Done;
            public float DoneAt;
        }

        private Step[] _steps;
        private bool _active;
        private float _moved;
        private float _looked;

        /// <summary>Montre l'aide (nouvelle partie), sauf si elle a déjà été complétée une fois.</summary>
        public void Begin(bool force)
        {
            if (!force && PlayerPrefs.GetInt(DoneKey, 0) == 1) return;
            if (_input == null) _input = FindAnyObjectByType<PlayerInputReader>();
            Core.InputBindings b = _input != null ? _input.Bindings : null;

            _steps = new[]
            {
                new Step { Key = b != null ? Keys(b.moveForward, b.moveLeft, b.moveBackward, b.moveRight) : "ZQSD", Text = "Se déplacer" },
                new Step { Key = "Souris", Text = "Regarder" },
                new Step { Key = b != null ? UiTheme.KeyName(b.interact) : "E", Text = "Interagir (portes, gens, objets)" },
                new Step { Key = b != null ? UiTheme.KeyName(b.phone) : "T", Text = "Sortir le téléphone" },
                new Step { Key = b != null ? UiTheme.KeyName(b.openMap) : "M", Text = "La carte (clic : poser un point)" },
                new Step { Key = b != null ? UiTheme.KeyName(b.sprint) : "Maj", Text = "Courir" },
                new Step { Key = "Clics", Text = "Frapper (gauche : direct, droit : crochet)" },
                new Step { Key = b != null ? UiTheme.KeyName(b.guard) : "Ctrl", Text = "Se protéger (garde)" }
            };
            _moved = 0f;
            _looked = 0f;
            _active = true;
        }

        private static string Keys(Core.InputBinding up, Core.InputBinding left, Core.InputBinding down, Core.InputBinding right)
        {
            // En AZERTY, ce sont les touches de secours (Z, Q) qui tombent sous les doigts.
            return Name(up) + Name(left) + Name(down) + Name(right);
        }

        private static string Name(Core.InputBinding binding)
        {
            if (binding.source == Core.InputSource.Key && binding.alternateKey != KeyCode.None && IsAzerty()) return UiTheme.KeyName(binding.alternateKey);
            return UiTheme.KeyName(binding);
        }

        /// <summary>Le clavier du système est-il en AZERTY ? (la langue de Windows en est un bon indice)</summary>
        private static bool IsAzerty()
        {
            SystemLanguage language = Application.systemLanguage;
            return language == SystemLanguage.French;
        }

        private void Update()
        {
            if (!_active || _input == null || _steps == null) return;

            if (_input.Move.sqrMagnitude > 0.1f) _moved += Time.deltaTime;
            _looked += _input.LookDelta.magnitude;

            Check(0, _moved > 1f);
            Check(1, _looked > 200f);
            Check(2, _input.InteractPressed);
            Check(3, _input.PhonePressed);
            Check(4, _input.MapPressed);
            Check(5, _input.SprintHeld && _input.Move.sqrMagnitude > 0.1f);
            Check(6, _input.StraightPressed || _input.HookPressed);
            Check(7, _input.GuardHeld);

            // Tout est fait, et la dernière coche a eu le temps de se voir.
            bool all = true;
            float last = 0f;
            for (int i = 0; i < _steps.Length; i++)
            {
                all &= _steps[i].Done;
                last = Mathf.Max(last, _steps[i].DoneAt);
            }

            if (all && Time.unscaledTime - last > 2f)
            {
                _active = false;
                PlayerPrefs.SetInt(DoneKey, 1);
            }
        }

        private void Check(int i, bool used)
        {
            if (!used || _steps[i].Done) return;
            _steps[i].Done = true;
            _steps[i].DoneAt = Time.unscaledTime;
        }

        private void OnGUI()
        {
            if (!_active || _steps == null || Event.current.type != EventType.Repaint) return;
            if (GameMenu.IsOpen || ModalScreen.Active || FightIntro.AnyPlaying || LoadingScreen.Visible) return;
            if (View.ShotCamera.Active || FullScreenPanel.AnyOpen) return;
            if (_fader != null && !_fader.IsClear) return;

            float u = UiTheme.Unit;
            GUIStyle title = UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            GUIStyle text = UiTheme.Text(15f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            GUIStyle key = UiTheme.Text(12.5f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);

            // Les lignes cochées restent un instant, puis s'effacent.
            int shown = 0;
            for (int i = 0; i < _steps.Length; i++) if (!_steps[i].Done || Time.unscaledTime - _steps[i].DoneAt < 1.5f) shown++;
            if (shown == 0) return;

            // À gauche, sous l'objectif : loin de la mini-carte et du centre de l'écran.
            float row = 32f * u;
            float width = 360f * u;
            float height = 48f * u + shown * row;
            Rect panel = new Rect(30f * u, Mathf.Max(170f * u, Screen.height * 0.24f), width, height);
            UiTheme.DrawPanel(panel, 10f * u);
            UiTheme.Label(new Rect(panel.x + 18f * u, panel.y + 12f * u, width, 20f * u), "PREMIERS PAS", title, UiTheme.Accent);

            float y = panel.y + 40f * u;
            for (int i = 0; i < _steps.Length; i++)
            {
                Step s = _steps[i];
                float age = s.Done ? Time.unscaledTime - s.DoneAt : 0f;
                if (s.Done && age >= 1.5f) continue;

                float alpha = s.Done ? Mathf.Clamp01(1.5f - age) : 1f;
                float previous = GuiKit.Alpha;
                GuiKit.Alpha = previous * alpha;

                float capWidth = Mathf.Max(30f * u, key.CalcSize(new GUIContent(s.Key)).x + 14f * u);
                Rect cap = new Rect(panel.x + 18f * u, y + 4f * u, capWidth, row - 8f * u);
                GuiKit.Rounded(cap, s.Done ? new Color(UiTheme.Good.r, UiTheme.Good.g, UiTheme.Good.b, 0.9f) : new Color(1f, 1f, 1f, 0.9f), 5f * u);
                UiTheme.Label(cap, s.Key, key, new Color(0.06f, 0.07f, 0.09f));
                UiTheme.Label(new Rect(cap.xMax + 12f * u, y, width - capWidth - 44f * u, row), s.Text, text, s.Done ? UiTheme.Good : UiTheme.Ink);
                GuiKit.Alpha = previous;
                y += row;
            }
        }

        public void Configure(PlayerInputReader input, ScreenFader fader)
        {
            _input = input;
            _fader = fader;
        }
    }
}
