using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// Le fondu au noir, et les cartons de texte par-dessus.
    ///
    /// Il sert deux choses qu'aucun autre outil ne fait aussi bien :
    ///
    /// 1. **Masquer un déplacement instantané.** Le prologue change de lieu — la maison, puis la
    ///    rue. Sans fondu, la téléportation est visible et le joueur comprend qu'il a été déplacé
    ///    plutôt que d'avoir voyagé. Avec, il comprend qu'il a conduit.
    /// 2. **Faire passer le temps.** « 20 MINUTES PLUS TARD » écrit sur du noir coûte deux lignes
    ///    de code et remplace un trajet en voiture qu'il faudrait construire, conduire et tester.
    ///
    /// Tout est en temps non mis à l'échelle : un fondu qui ralentirait avec le ralenti d'impact
    /// donnerait une transition dont la durée dépend de ce qui vient de se passer en combat.
    /// </summary>
    public class ScreenFader : MonoBehaviour
    {
        [SerializeField] private Color _color = Color.black;
        [SerializeField, Min(0.05f)] private float _defaultDuration = 0.7f;
        [SerializeField] private int _cardFontSize = 26;
        [SerializeField] private Color _cardColor = new Color(0.93f, 0.92f, 0.88f);

        private float _alpha;
        private float _target;
        private float _speed = 1.4f;

        private string _card;
        private float _cardAlpha;
        private float _cardTarget;

        public bool IsBlack { get { return _alpha > 0.99f; } }
        public bool IsClear { get { return _alpha < 0.01f; } }
        public float Alpha { get { return _alpha; } }

        /// <summary>Assombrit l'écran. Ne rend pas la main : l'appelant attend IsBlack.</summary>
        public void FadeOut(float duration)
        {
            _target = 1f;
            _speed = 1f / Mathf.Max(0.05f, duration);
        }

        public void FadeOut()
        {
            FadeOut(_defaultDuration);
        }

        public void FadeIn(float duration)
        {
            _target = 0f;
            _speed = 1f / Mathf.Max(0.05f, duration);
            ShowCard(null);
        }

        public void FadeIn()
        {
            FadeIn(_defaultDuration);
        }

        /// <summary>Texte affiché par-dessus le noir. Passer null l'efface.</summary>
        public void ShowCard(string text)
        {
            _card = text;
            _cardTarget = string.IsNullOrEmpty(text) ? 0f : 1f;
        }

        /// <summary>Force l'écran au noir sans transition (démarrage du prologue).</summary>
        public void SetBlackImmediate()
        {
            _alpha = 1f;
            _target = 1f;
        }

        private void Update()
        {
            _alpha = Mathf.MoveTowards(_alpha, _target, _speed * Time.unscaledDeltaTime);
            _cardAlpha = Mathf.MoveTowards(_cardAlpha, _cardTarget * _alpha, 1.8f * Time.unscaledDeltaTime);
        }

        private void OnGUI()
        {
            if (_alpha <= 0.001f) return;

            // Profondeur très haute : le voile passe par-dessus le HUD, les sous-titres et le
            // téléphone. Un fondu au noir qui laisserait l'interface visible ne masquerait rien.
            int previousDepth = GUI.depth;
            GUI.depth = -1000;

            GuiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height),
                new Color(_color.r, _color.g, _color.b, _alpha));

            if (_cardAlpha > 0.01f && !string.IsNullOrEmpty(_card))
            {
                GuiKit.OutlinedLabel(new Rect(0f, 0f, Screen.width, Screen.height), _card,
                    GuiKit.Style(_cardFontSize, FontStyle.Bold, TextAnchor.MiddleCenter),
                    new Color(_cardColor.r, _cardColor.g, _cardColor.b, _cardAlpha),
                    new Color(0f, 0f, 0f, _cardAlpha), 1.5f);
            }

            GUI.depth = previousDepth;
        }
    }
}
