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
                new Step { Key = b != null ? b.interact.ToString() : "E", Text = "Interagir (portes, gens, objets)" },
                new Step { Key = b != null ? b.phone.ToString() : "T", Text = "Sortir le téléphone" },
                new Step { Key = b != null ? b.openMap.ToString() : "M", Text = "La carte (clic : poser un point)" },
                new Step { Key = b != null ? b.sprint.ToString() : "Maj", Text = "Courir" },
                new Step { Key = "Clics", Text = "Frapper (gauche : direct, droit : crochet)" },
                new Step { Key = b != null ? b.guard.ToString() : "Ctrl", Text = "Se protéger (garde)" }
            };
            _moved = 0f;
            _looked = 0f;
            _active = true;
        }

        private static string Keys(Core.InputBinding up, Core.InputBinding left, Core.InputBinding down, Core.InputBinding right)
        {
            return up.ToString() + left + down + right;
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
            if (_fader != null && !_fader.IsClear) return;

            float u = Mathf.Max(0.6f, Screen.height / 1080f);
            GUIStyle title = GuiKit.Text(Mathf.RoundToInt(15 * u), GuiKit.Weight.Black, TextAnchor.MiddleLeft);
            GUIStyle text = GuiKit.Text(Mathf.RoundToInt(16 * u), GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            GUIStyle key = GuiKit.Text(Mathf.RoundToInt(13 * u), GuiKit.Weight.Black, TextAnchor.MiddleCenter);

            // Les lignes cochées restent un instant (vertes), puis s'effacent.
            int shown = 0;
            for (int i = 0; i < _steps.Length; i++) if (!_steps[i].Done || Time.unscaledTime - _steps[i].DoneAt < 1.5f) shown++;
            if (shown == 0) return;

            float row = 34f * u;
            float width = 380f * u;
            float height = 46f * u + shown * row;
            Rect panel = new Rect(24f * u, Screen.height - height - 150f * u, width, height);
            GuiKit.Rounded(panel, new Color(0.06f, 0.055f, 0.09f, 0.82f), 14f * u);
            GuiKit.ShadowLabel(new Rect(panel.x + 18f * u, panel.y + 10f * u, width, 24f * u), "PREMIERS PAS", title,
                new Color(1f, 0.25f, 0.6f), 0.3f);

            float y = panel.y + 40f * u;
            for (int i = 0; i < _steps.Length; i++)
            {
                Step s = _steps[i];
                float age = s.Done ? Time.unscaledTime - s.DoneAt : 0f;
                if (s.Done && age >= 1.5f) continue;

                float alpha = s.Done ? Mathf.Clamp01(1.5f - age) : 1f;
                Color ink = s.Done ? new Color(0.45f, 1f, 0.55f, alpha) : new Color(1f, 1f, 1f, 0.92f);

                float capWidth = Mathf.Max(34f * u, key.CalcSize(new GUIContent(s.Key)).x + 16f * u);
                Rect cap = new Rect(panel.x + 18f * u, y + 4f * u, capWidth, row - 8f * u);
                GuiKit.Rounded(cap, s.Done ? new Color(0.3f, 0.8f, 0.4f, 0.5f * alpha) : new Color(1f, 1f, 1f, 0.16f), 6f * u);
                GuiKit.ShadowLabel(cap, s.Key, key, ink, 0.2f);
                GuiKit.ShadowLabel(new Rect(cap.xMax + 12f * u, y, width - capWidth - 40f * u, row), (s.Done ? "✓  " : "") + s.Text, text, ink, 0.35f);
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
