using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// L'objectif courant, en haut à gauche.
    ///
    /// Un seul objectif à la fois, jamais une liste. Une liste de tâches transforme un prologue
    /// en formulaire : le joueur la lit une fois, la consulte plus jamais, et se retrouve perdu
    /// exactement au moment où il aurait fallu qu'il regarde. Une ligne unique qui CHANGE est au
    /// contraire un événement : le changement attire l'œil, donc l'information arrive.
    ///
    /// D'où aussi la pulsation courte quand le texte change, et le petit accusé de réception
    /// barré quand l'objectif est rempli : sans lui, on ne sait pas si on vient de réussir
    /// quelque chose ou si le jeu a simplement changé d'avis.
    /// </summary>
    public class ObjectiveDisplay : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Vector2 _margin = new Vector2(26f, 24f);
        [SerializeField, Min(120f)] private float _width = 360f;

        [Header("Apparence")]
        [SerializeField] private Color _accent = new Color(0.98f, 0.80f, 0.30f);
        [SerializeField] private Color _doneColor = new Color(0.45f, 0.86f, 0.52f);
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.55f);
        [SerializeField] private int _fontSize = 15;

        [SerializeField, Min(0.1f)]
        [Tooltip("Durée pendant laquelle l'objectif accompli reste affiché, barré.")]
        private float _completedHold = 1.5f;

        private string _objective = string.Empty;
        private string _completed;
        private float _completedAge;
        private float _appearAge = 99f;
        private float _visibility;

        public string Current
        {
            get { return _objective; }
        }

        /// <summary>Change l'objectif. Passer une chaîne vide efface l'affichage.</summary>
        public void Set(string objective)
        {
            if (objective == _objective) return;

            // L'ancien objectif ne disparaît pas : il s'affiche barré une seconde et demie.
            // C'est le seul moment où le joueur apprend qu'il vient de réussir quelque chose.
            if (!string.IsNullOrEmpty(_objective))
            {
                _completed = _objective;
                _completedAge = 0f;
            }

            _objective = objective ?? string.Empty;
            _appearAge = 0f;
        }

        public void Clear()
        {
            Set(string.Empty);
        }

        private void Update()
        {
            _appearAge += Time.unscaledDeltaTime;
            _completedAge += Time.unscaledDeltaTime;

            bool visible = !string.IsNullOrEmpty(_objective)
                           || (!string.IsNullOrEmpty(_completed) && _completedAge < _completedHold);

            _visibility = Mathf.MoveTowards(_visibility, visible ? 1f : 0f, Time.unscaledDeltaTime * 4f);
        }

        private void OnGUI()
        {
            if (_visibility <= 0.01f) return;

            float y = _margin.y;
            bool showCompleted = !string.IsNullOrEmpty(_completed) && _completedAge < _completedHold;

            if (showCompleted)
            {
                float fade = Mathf.Clamp01((_completedHold - _completedAge) * 2f) * _visibility;
                DrawRow(_margin.x, y, _completed, _doneColor, fade, true, 1f);
                y += 28f;
            }

            if (string.IsNullOrEmpty(_objective)) return;

            // Pulsation courte à l'apparition : trois battements, puis plus rien. Un clignotement
            // permanent devient du bruit et on cesse de le voir.
            float pulse = _appearAge < 1.2f
                ? 1f + 0.25f * Mathf.Abs(Mathf.Sin(_appearAge * 9f)) * (1f - _appearAge / 1.2f)
                : 1f;

            DrawRow(_margin.x, y, _objective, _accent, _visibility, false, pulse);
        }

        private void DrawRow(float x, float y, string text, Color color, float alpha, bool struck, float pulse)
        {
            GUIStyle style = GuiKit.Style(_fontSize, FontStyle.Bold, TextAnchor.MiddleLeft);
            style.wordWrap = true;

            float textWidth = _width - 34f;
            float height = Mathf.Max(24f, style.CalcHeight(new GUIContent(text), textWidth) + 8f);

            Rect panel = new Rect(x, y, _width, height);

            Color background = _panelColor;
            background.a *= alpha;
            GuiKit.Fill(panel, background);

            // Le chevron : il dit « voici ce qu'il faut faire » sans un mot de plus.
            Color mark = new Color(color.r, color.g, color.b, alpha * pulse);
            GuiKit.Fill(new Rect(x, y, 3f, height), mark);
            GuiKit.Fill(new Rect(x + 12f, y + height * 0.5f - 4f, 8f, 8f), mark);

            Rect textRect = new Rect(x + 28f, y + 4f, textWidth, height - 8f);

            GuiKit.OutlinedLabel(textRect, text, style,
                new Color(color.r, color.g, color.b, alpha),
                new Color(0f, 0f, 0f, 0.85f * alpha), 1.2f);

            if (!struck) return;

            GuiKit.Fill(new Rect(textRect.x, textRect.y + textRect.height * 0.5f - 1f,
                Mathf.Min(textWidth, style.CalcSize(new GUIContent(text)).x), 2f),
                new Color(color.r, color.g, color.b, alpha * 0.9f));
        }
    }
}
