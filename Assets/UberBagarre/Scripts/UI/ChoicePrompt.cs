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

            float u = Mathf.Max(0.6f, Screen.height / 1080f);
            float sw = Screen.width, sh = Screen.height;
            GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.45f));

            float width = Mathf.Min(sw - 80f * u, 760f * u);
            float rowHeight = 58f * u;
            float height = 140f * u + _options.Length * (rowHeight + 10f * u);
            Rect panel = new Rect((sw - width) * 0.5f, sh * 0.5f - height * 0.5f, width, height);

            Color accent = new Color(1f, 0.2f, 0.55f);
            GuiKit.Glow(panel, new Color(0f, 0f, 0f, 0.6f), 22f * u, 26f * u);
            GuiKit.Rounded(panel, new Color(0.075f, 0.068f, 0.11f, 0.96f), 22f * u);
            GuiKit.RoundedOutline(panel, new Color(1f, 1f, 1f, 0.08f), 22f * u, 1f);

            GUIStyle speaker = GuiKit.Text(Mathf.RoundToInt(15 * u), GuiKit.Weight.Black, TextAnchor.MiddleLeft);
            GUIStyle question = GuiKit.Text(Mathf.RoundToInt(22 * u), GuiKit.Weight.Bold, TextAnchor.UpperLeft, true);
            GUIStyle option = GuiKit.Text(Mathf.RoundToInt(19 * u), GuiKit.Weight.Bold, TextAnchor.MiddleLeft, true);
            GUIStyle key = GuiKit.Text(Mathf.RoundToInt(16 * u), GuiKit.Weight.Black, TextAnchor.MiddleCenter);

            if (!string.IsNullOrEmpty(_speaker))
                GuiKit.ShadowLabel(new Rect(panel.x + 32f * u, panel.y + 20f * u, width, 22f * u), _speaker, speaker, accent, 0.3f);
            GuiKit.ShadowLabel(new Rect(panel.x + 32f * u, panel.y + 46f * u, width - 64f * u, 70f * u), _question, question, Color.white, 0.4f);

            Event e = Event.current;
            for (int i = 0; i < _options.Length; i++)
            {
                Rect row = new Rect(panel.x + 24f * u, panel.y + 124f * u + i * (rowHeight + 10f * u), width - 48f * u, rowHeight);
                bool hover = row.Contains(e.mousePosition);
                if (hover && e.type == EventType.MouseMove) _selected = i;
                bool selected = i == _selected;

                GuiKit.Rounded(row, selected ? new Color(accent.r, accent.g, accent.b, 0.2f) : new Color(1f, 1f, 1f, 0.05f), 14f * u);
                if (selected) GuiKit.RoundedOutline(row, accent, 14f * u, 1.5f * u);

                Rect cap = new Rect(row.x + 14f * u, row.center.y - 16f * u, 32f * u, 32f * u);
                GuiKit.Rounded(cap, selected ? accent : new Color(1f, 1f, 1f, 0.12f), 8f * u);
                GuiKit.ShadowLabel(cap, (i + 1).ToString(), key, Color.white, 0.2f);
                GuiKit.ShadowLabel(new Rect(cap.xMax + 16f * u, row.y, row.width - 80f * u, row.height), _options[i], option,
                    selected ? Color.white : new Color(0.8f, 0.8f, 0.85f), 0.4f);

                if (hover && e.type == EventType.MouseDown && e.button == 0 && Time.unscaledTime - _openedAt > 0.35f)
                {
                    e.Use();
                    Choose(i);
                    return;
                }
            }
        }
    }
}
