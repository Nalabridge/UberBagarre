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
            // La cinematique d'avant-combat prend l'ecran : pas d'interface de jeu par-dessus.
            if (FightIntro.AnyPlaying) return;

            // Un écran plein (armoire, ordinateur, comptoir) ou le menu : l'objectif s'efface derrière.
            if (UI.ModalScreen.Active || UI.GameMenu.IsOpen || View.ShotCamera.Active) return;
            if (Core.GameSettings.Hud == 2) return;
            if (_visibility <= 0.01f) return;

            // En haut à gauche, là où GTA met ses consignes (la mini-carte est en bas).
            float u = UiTheme.Unit;
            float x = 30f * u;
            // Sous la fiche de course quand il y en a une.
            float y = Mathf.Max(30f * u, UiTheme.TopLeftUsed);
            bool showCompleted = !string.IsNullOrEmpty(_completed) && _completedAge < _completedHold;

            if (showCompleted)
            {
                float fade = Mathf.Clamp01((_completedHold - _completedAge) * 2f) * _visibility;
                y = DrawRow(x, y, _completed, _doneColor, fade, true, 1f, u) + 8f * u;
            }

            if (string.IsNullOrEmpty(_objective)) return;

            // Pulsation courte à l'apparition : trois battements, puis plus rien. Un clignotement
            // permanent devient du bruit et on cesse de le voir.
            float pulse = _appearAge < 1.2f
                ? 1f + 0.25f * Mathf.Abs(Mathf.Sin(_appearAge * 9f)) * (1f - _appearAge / 1.2f)
                : 1f;

            DrawRow(x, y, _objective, _accent, _visibility, false, pulse, u);
        }

        private float DrawRow(float x, float y, string text, Color color, float alpha, bool struck, float pulse, float u)
        {
            float width = Mathf.Min(_width, 380f) * u;
            GUIStyle caption = UiTheme.Text(11f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            GUIStyle style = GuiKit.Text(UiTheme.Size(15.5f), GuiKit.Weight.Medium, TextAnchor.UpperLeft, true);

            float textWidth = width - 32f * u;
            float textHeight = style.CalcHeight(new GUIContent(text), textWidth);
            float height = textHeight + 34f * u;
            Rect panel = new Rect(x, y, width, height);

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = alpha;
            GuiKit.Rounded(panel, new Color(0.03f, 0.035f, 0.045f, 0.62f), 8f * u);
            GuiKit.Fill(new Rect(panel.x, panel.y + 9f * u, 3f * u, panel.height - 18f * u), new Color(color.r, color.g, color.b, Mathf.Min(1f, pulse)));

            UiTheme.Label(new Rect(x + 16f * u, y + 7f * u, width, 14f * u), struck ? "FAIT" : "OBJECTIF", caption, color);
            Rect textRect = new Rect(x + 16f * u, y + 23f * u, textWidth, textHeight);
            GuiKit.ShadowLabel(textRect, text, style, struck ? UiTheme.InkDim : UiTheme.Ink, 0.4f);

            if (struck)
            {
                GuiKit.Fill(new Rect(textRect.x, textRect.y + Mathf.Min(textHeight, 20f * u) * 0.5f, Mathf.Min(textWidth, style.CalcSize(new GUIContent(text)).x), 1.5f * u),
                    new Color(color.r, color.g, color.b, 0.9f));
            }

            GuiKit.Alpha = previous;
            return panel.yMax;
        }
    }
}
