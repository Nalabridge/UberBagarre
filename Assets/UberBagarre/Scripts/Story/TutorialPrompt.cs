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
        [SerializeField] private Color _accent = new Color(0.42f, 0.82f, 1f);
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

            const float height = 62f;
            float x = _margin.x;
            float y = Screen.height - _margin.y - height;

            Color panel = _panelColor;
            panel.a *= _visibility;
            GuiKit.Fill(new Rect(x, y, _width, height), panel);
            GuiKit.Outline(new Rect(x, y, _width, height), 2f,
                new Color(_accent.r, _accent.g, _accent.b, _visibility * (0.5f + 0.5f * _flash)));

            // La touche dans un cadre, comme sur un clavier : c'est reconnu sans légende.
            Rect keyRect = new Rect(x + 12f, y + 12f, Mathf.Max(38f, 13f + _key.Length * 9f), 26f);
            GuiKit.Fill(keyRect, new Color(1f, 1f, 1f, 0.10f * _visibility));
            GuiKit.Outline(keyRect, 1.5f, new Color(_keyColor.r, _keyColor.g, _keyColor.b, _visibility * 0.8f));

            GuiKit.OutlinedLabel(keyRect, _key, GuiKit.Style(13, FontStyle.Bold, TextAnchor.MiddleCenter),
                new Color(_keyColor.r, _keyColor.g, _keyColor.b, _visibility),
                new Color(0f, 0f, 0f, 0.85f * _visibility), 1f);

            GUIStyle text = GuiKit.Style(14, FontStyle.Bold, TextAnchor.MiddleLeft, true);

            GuiKit.OutlinedLabel(new Rect(keyRect.xMax + 12f, y + 8f, _width - keyRect.width - 30f, 34f),
                _instruction, text,
                new Color(1f, 1f, 1f, _visibility), new Color(0f, 0f, 0f, 0.85f * _visibility), 1.2f);

            if (_required <= 0) return;

            // Des pastilles plutôt qu'un « 2 / 3 » : on lit combien il en reste d'un coup d'œil,
            // sans avoir à faire la soustraction en plein combat.
            float pipY = y + height - 14f;

            for (int i = 0; i < _required; i++)
            {
                Rect pip = new Rect(x + 14f + i * 16f, pipY, 11f, 5f);
                bool filled = i < _done;

                GuiKit.Fill(pip, filled
                    ? new Color(_accent.r, _accent.g, _accent.b, _visibility)
                    : new Color(1f, 1f, 1f, 0.18f * _visibility));
            }
        }
    }
}
