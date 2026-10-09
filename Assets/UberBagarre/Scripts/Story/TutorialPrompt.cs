using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// Le rappel de touche pendant le tutoriel : une touche, une phrase, un compteur.
    ///
    /// Le principe qui gouverne tout : **on n'apprend pas une commande en la lisant, on
    /// l'apprend en la réussissant.** D'où le compteur. Tant que le joueur n'a pas placé ses
    /// deux directs, l'étape ne passe pas — et ce n'est pas une punition, c'est la seule
    /// garantie que la touche a été essayée. Un tutoriel qui affiche « CLIC GAUCHE : direct »
    /// pendant trois secondes puis passe à la suite n'a rien enseigné à qui regardait ailleurs.
    ///
    /// Le compteur sert aussi de retour immédiat : chaque coup porté le fait avancer, donc le
    /// joueur sait tout de suite si son coup a compté. C'est exactement l'information qui
    /// manque quand on découvre un système de combat.
    /// </summary>
    public class TutorialPrompt : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField] private Vector2 _margin = new Vector2(26f, 30f);
        [SerializeField, Min(160f)] private float _width = 330f;

        [Header("Apparence")]
        [SerializeField] private Color _accent = new Color(1f, 0.74f, 0.29f);
        [SerializeField] private Color _panelColor = new Color(0.03f, 0.05f, 0.08f, 0.82f);
        [SerializeField] private Color _keyColor = new Color(0.98f, 0.96f, 0.90f);

        private string _key;
        private string _instruction;
        private int _done;
        private int _required;
        private float _visibility;
        private float _flash;

        public bool IsShowing
        {
            get { return !string.IsNullOrEmpty(_instruction); }
        }

        /// <summary>Nombre de réussites enregistrées pour la consigne en cours.</summary>
        public int Progress
        {
            get { return _done; }
        }

        public bool IsSatisfied
        {
            get { return _required <= 0 || _done >= _required; }
        }

        public void Show(string key, string instruction, int required)
        {
            _key = key;
            _instruction = instruction;
            _required = Mathf.Max(0, required);
            _done = 0;
            _flash = 0f;
        }

        public void Hide()
        {
            _instruction = null;
            _key = null;
            _required = 0;
            _done = 0;
        }

        /// <summary>Enregistre une réussite. Sans effet si aucune consigne n'est affichée.</summary>
        public void Score()
        {
            if (string.IsNullOrEmpty(_instruction)) return;
            if (_required > 0 && _done >= _required) return;

            _done++;
            _flash = 1f;
        }

        private void Update()
        {
            _visibility = Mathf.MoveTowards(_visibility, IsShowing ? 1f : 0f, Time.unscaledDeltaTime * 4f);
            _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime * 2.6f);
        }

        private void OnGUI()
        {
            // La cinematique d'avant-combat prend l'ecran : pas d'interface de jeu par-dessus.
            if (FightIntro.AnyPlaying) return;

            if (_visibility <= 0.01f || string.IsNullOrEmpty(_instruction)) return;

            // En haut à droite, sous l'argent et les étoiles : là où l'œil va chercher une aide,
            // sans rien cacher du combat au centre ni de la carte en bas à gauche.
            float u = UiTheme.Unit;
            float width = Mathf.Max(_width, 330f) * u;
            float pad = 16f * u;
            GUIStyle keyStyle = UiTheme.Text(14f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle text = GuiKit.Text(UiTheme.Size(16f), GuiKit.Weight.Medium, TextAnchor.MiddleLeft, true);

            float capWidth = Mathf.Max(32f * u, keyStyle.CalcSize(new GUIContent(_key ?? "")).x + 18f * u);
            float textWidth = width - pad * 2f - capWidth - 14f * u;
            float textHeight = Mathf.Max(30f * u, text.CalcHeight(new GUIContent(_instruction), textWidth));
            float height = pad * 2f + textHeight + (_required > 0 ? 14f * u : 0f);

            float slide = (1f - UiTheme.EaseOut(_visibility)) * 20f * u;
            Rect panel = new Rect(Screen.width - 30f * u - width + slide, UiTheme.TopRight(150f), width, height);

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * _visibility;
            UiTheme.DrawPanel(panel, 10f * u);

            // Une réussite fait briller le bord : le coup a compté.
            if (_flash > 0.01f) GuiKit.RoundedOutline(panel, new Color(_accent.r, _accent.g, _accent.b, _flash), 10f * u, 2f * u);

            Rect cap = new Rect(panel.x + pad, panel.y + pad + (textHeight - 30f * u) * 0.5f, capWidth, 30f * u);
            GuiKit.Rounded(cap, new Color(1f, 1f, 1f, 0.92f), 5f * u);
            UiTheme.Label(cap, _key, keyStyle, new Color(0.06f, 0.07f, 0.09f));
            UiTheme.Label(new Rect(cap.xMax + 14f * u, panel.y + pad, textWidth, textHeight), _instruction, text, UiTheme.Ink);

            if (_required > 0)
            {
                // Des pastilles plutôt qu'un « 2 / 3 » : on lit combien il en reste d'un coup
                // d'œil, sans avoir à faire la soustraction en plein combat.
                float pipY = panel.yMax - pad - 4f * u;
                for (int i = 0; i < _required; i++)
                {
                    Rect pip = new Rect(cap.xMax + 14f * u + i * 22f * u, pipY, 16f * u, 4f * u);
                    GuiKit.Rounded(pip, i < _done ? _accent : new Color(1f, 1f, 1f, 0.16f), 2f * u);
                }
            }

            GuiKit.Alpha = previous;
        }
    }
}
