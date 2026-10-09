using System;
using UberBagarre.Player;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Un choix de l'histoire : une question, deux à quatre réponses. Touches 1 à 4, flèches et
    /// Entrée, ou la souris. Le jeu se fige derrière (le temps, lui, continue : on ne réfléchit
    /// pas éternellement devant quelqu'un qui attend une réponse… mais sans limite imposée).
    /// </summary>
    public class ChoicePrompt : MonoBehaviour
    {
        private static ChoicePrompt _instance;

        private static readonly Core.InputBinding[] Keys =
        {
            Core.InputBinding.FromKey(KeyCode.Alpha1), Core.InputBinding.FromKey(KeyCode.Alpha2),
            Core.InputBinding.FromKey(KeyCode.Alpha3), Core.InputBinding.FromKey(KeyCode.Alpha4)
        };

        private static readonly Core.InputBinding[] Pad =
        {
            Core.InputBinding.FromKey(KeyCode.Keypad1), Core.InputBinding.FromKey(KeyCode.Keypad2),
            Core.InputBinding.FromKey(KeyCode.Keypad3), Core.InputBinding.FromKey(KeyCode.Keypad4)
        };

        private string _question;
        private string _speaker;
        private string[] _options;
        private Action<int> _callback;
        private int _selected;
        private float _openedAt;
        private PlayerInputReader _input;

        /// <summary>Un choix est à l'écran.</summary>
        public static bool Open { get { return _instance != null && _instance._options != null; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        /// <summary>Pose la question ; <paramref name="callback"/> reçoit l'indice de la réponse.</summary>
        public static void Ask(string speaker, string question, string[] options, Action<int> callback)
        {
            if (options == null || options.Length == 0) return;
            if (_instance == null) _instance = new GameObject("Choix de l'histoire").AddComponent<ChoicePrompt>();

            ChoicePrompt p = _instance;
            p._speaker = speaker;
            p._question = question;
            p._options = options;
            p._callback = callback;
            p._selected = 0;
            p._openedAt = Time.unscaledTime;
            if (p._input == null) p._input = FindAnyObjectByType<PlayerInputReader>();
            if (p._input != null) p._input.SetGameplayLock(p, true);
            ModalScreen.Set(p, true);
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }

        private void Choose(int index)
        {
            Action<int> callback = _callback;
            _options = null;
            _callback = null;
            if (_input != null) _input.SetGameplayLock(this, false);
            ModalScreen.Set(this, false);
            Cursor.lockState = CursorLockMode.Locked;
            Cursor.visible = false;
            if (callback != null) callback(index);
        }

        private void Update()
        {
            if (_options == null || _input == null || _input.Provider == null || _input.Bindings == null) return;
            if (Time.unscaledTime - _openedAt < 0.35f) return;

            var provider = _input.Provider;
            var b = _input.Bindings;
            for (int i = 0; i < _options.Length && i < 4; i++)
            {
                if (provider.GetPressedThisFrame(Keys[i]) || provider.GetPressedThisFrame(Pad[i]))
                {
                    Choose(i);
                    return;
                }
            }

            if (provider.GetPressedThisFrame(b.phoneUp) || provider.GetPressedThisFrame(b.moveForward))
                _selected = (_selected + _options.Length - 1) % _options.Length;
            if (provider.GetPressedThisFrame(b.phoneDown) || provider.GetPressedThisFrame(b.moveBackward))
                _selected = (_selected + 1) % _options.Length;
            if (provider.GetPressedThisFrame(b.phoneSelect) || _input.InteractPressed) Choose(_selected);
        }

        private void OnGUI()
        {
            if (_options == null) return;
            GUI.depth = -60;

            float u = UiTheme.Unit;
            float sw = Screen.width, sh = Screen.height;
            float appear = UiTheme.EaseOut((Time.unscaledTime - _openedAt) / 0.3f);

            // Le jeu reste visible : un voile qui ne fonce que le bas de l'écran, là où est le choix.
            const int bands = 16;
            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                GuiKit.Fill(new Rect(0f, sh * (0.35f + 0.65f * i / bands), sw, sh * 0.65f / bands + 1f),
                    new Color(0f, 0f, 0f, 0.62f * t * appear));
            }

            float width = Mathf.Min(sw - 80f * u, 780f * u);
            float rowHeight = 54f * u;
            GUIStyle speaker = UiTheme.Text(13f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            GUIStyle question = GuiKit.Text(UiTheme.Size(22f), GuiKit.Weight.Medium, TextAnchor.UpperLeft, true);
            GUIStyle option = GuiKit.Text(UiTheme.Size(18f), GuiKit.Weight.Medium, TextAnchor.MiddleLeft, true);
            GUIStyle key = UiTheme.Text(14f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);

            float pad = 30f * u;
            float questionHeight = question.CalcHeight(new GUIContent(_question ?? ""), width - pad * 2f);
            float headerHeight = (string.IsNullOrEmpty(_speaker) ? 0f : 24f * u) + questionHeight + 22f * u;
            float height = pad + headerHeight + _options.Length * (rowHeight + 6f * u) + pad - 6f * u;
            Rect panel = new Rect((sw - width) * 0.5f, sh * 0.62f - height * 0.5f + (1f - appear) * 20f * u, width, height);

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * appear;
            UiTheme.DrawPanel(panel, 14f * u);

            float y = panel.y + pad;
            if (!string.IsNullOrEmpty(_speaker))
            {
                UiTheme.Label(new Rect(panel.x + pad, y, width - pad * 2f, 20f * u), _speaker.ToUpperInvariant(), speaker, UiTheme.Accent);
                y += 24f * u;
            }

            UiTheme.Label(new Rect(panel.x + pad, y, width - pad * 2f, questionHeight), _question, question, UiTheme.Ink);
            y += questionHeight + 22f * u;

            Event e = Event.current;
            for (int i = 0; i < _options.Length; i++)
            {
                Rect row = new Rect(panel.x + pad - 12f * u, y + i * (rowHeight + 6f * u), width - pad * 2f + 24f * u, rowHeight);
                bool hover = row.Contains(e.mousePosition);
                if (hover && UiTheme.MouseMoved) _selected = i;
                bool selected = i == _selected;

                if (selected)
                {
                    GuiKit.Rounded(row, new Color(1f, 1f, 1f, 0.08f), 8f * u);
                    GuiKit.Rounded(new Rect(row.x, row.y + 12f * u, 3f * u, row.height - 24f * u), UiTheme.Accent, 1.5f * u);
                }
                else if (hover)
                {
                    GuiKit.Rounded(row, new Color(1f, 1f, 1f, 0.04f), 8f * u);
                }

                // Le numéro dans un cabochon de touche : 1 à 4 au clavier.
                Rect cap = new Rect(row.x + 16f * u, row.center.y - 13f * u, 26f * u, 26f * u);
                GuiKit.Rounded(cap, selected ? new Color(1f, 1f, 1f, 0.92f) : new Color(1f, 1f, 1f, 0.12f), 5f * u);
                UiTheme.Label(cap, (i + 1).ToString(), key, selected ? new Color(0.06f, 0.07f, 0.09f) : UiTheme.Ink);
                UiTheme.Label(new Rect(cap.xMax + 16f * u, row.y, row.width - 80f * u, row.height), _options[i], option,
                    selected ? UiTheme.Ink : UiTheme.InkDim);

                if (hover && e.type == EventType.MouseDown && e.button == 0 && Time.unscaledTime - _openedAt > 0.35f)
                {
                    e.Use();
                    GuiKit.Alpha = previous;
                    Choose(i);
                    return;
                }
            }

            GuiKit.Alpha = previous;
        }
    }
}
