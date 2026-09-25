using System.Collections.Generic;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// Les sous-titres.
    ///
    /// Chaque réplique est aussi DITE par <see cref="DialogueVoice"/> (voix de synthèse
    /// pré-enregistrée, ou babillage pour les répliques calculées). Le sous-titre reste
    /// affiché au moins aussi longtemps que la voix parle : un texte qui disparaît pendant que
    /// le personnage finit sa phrase se lit comme un bug.
    ///
    /// Deux règles d'affichage qui comptent plus que le style :
    ///
    /// 1. **Le nom de celui qui parle est toujours visible.** Sans lui, une conversation
    ///    téléphonique entre deux personnages qu'on ne voit ni l'un ni l'autre devient
    ///    illisible dès la deuxième réplique.
    /// 2. **On peut passer.** Un joueur qui relance le prologue pour la dixième fois pendant un
    ///    test n'a pas à réécouter le dialogue, sinon il cesse de le tester.
    /// </summary>
    public class SubtitleDisplay : MonoBehaviour
    {
        [Header("Placement")]
        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Hauteur du bas de l'écran, en fraction de la hauteur totale.")]
        private float _bottomMargin = 0.13f;

        [SerializeField, Range(0.3f, 1f)] private float _widthFraction = 0.62f;

        [Header("Apparence")]
        [SerializeField] private int _fontSize = 17;
        [SerializeField] private Color _textColor = new Color(0.96f, 0.96f, 0.94f);
        [SerializeField] private Color _speakerColor = new Color(0.98f, 0.80f, 0.30f);
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.62f);

        [SerializeField, Min(0.05f)]
        [Tooltip("Durée du fondu d'entrée et de sortie.")]
        private float _fade = 0.18f;

        [Header("Voix")]
        [SerializeField] private DialogueVoice _voice;

        [SerializeField, Min(0f)]
        [Tooltip("Silence laissé après la voix avant la réplique suivante.")]
        private float _voiceTail = 0.3f;

        private readonly Queue<DialogueLine> _queue = new Queue<DialogueLine>();
        private DialogueLine _current;
        private bool _hasCurrent;
        private float _elapsed;
        private float _visibility;

        /// <summary>Reste-t-il quelque chose à dire ?</summary>
        public bool IsSpeaking
        {
            get { return _hasCurrent || _queue.Count > 0; }
        }

        public void Play(IList<DialogueLine> lines)
        {
            if (lines == null) return;

            for (int i = 0; i < lines.Count; i++) _queue.Enqueue(lines[i]);
        }

        public void Play(DialogueLine line)
        {
            _queue.Enqueue(line);
        }

        /// <summary>Passe la réplique en cours. Vider la file entière serait trop brutal :
        /// le joueur veut accélérer, pas sauter la scène.</summary>
        public void Skip()
        {
            if (!_hasCurrent) return;

            _elapsed = _current.Duration;
            if (_voice != null) _voice.Stop();
        }

        public void Clear()
        {
            if (_voice != null) _voice.Stop();
            _queue.Clear();
            _hasCurrent = false;
            _elapsed = 0f;
        }

        private void Update()
        {
            if (!_hasCurrent && _queue.Count > 0)
            {
                _current = _queue.Dequeue();
                _hasCurrent = true;
                _elapsed = 0f;

                if (_voice != null)
                {
                    float spoken = _voice.Speak(_current.Speaker, _current.Text);
                    if (spoken > 0f) _current.Duration = Mathf.Max(_current.Duration, spoken + _voiceTail);
                }
            }

            if (_hasCurrent)
            {
                _elapsed += Time.unscaledDeltaTime;
                if (_elapsed >= _current.Duration) _hasCurrent = false;
            }

            // Temps non mis à l'échelle : le ralenti d'impact ne doit pas ralentir la lecture.
            float target = _hasCurrent ? 1f : 0f;
            _visibility = Mathf.MoveTowards(_visibility, target, Time.unscaledDeltaTime / Mathf.Max(0.02f, _fade));
        }

        private void OnGUI()
        {
            if (_visibility <= 0.01f || !_hasCurrent) return;

            float width = Screen.width * _widthFraction;
            float x = (Screen.width - width) * 0.5f;

            GUIStyle textStyle = GuiKit.Style(_fontSize, FontStyle.Normal, TextAnchor.UpperCenter);
            textStyle.wordWrap = true;

            float textHeight = textStyle.CalcHeight(new GUIContent(_current.Text), width - 28f);
            float speakerHeight = string.IsNullOrEmpty(_current.Speaker) ? 0f : 20f;
            float height = textHeight + speakerHeight + 22f;

            float y = Screen.height * (1f - _bottomMargin) - height;

            Color panel = _panelColor;
            panel.a *= _visibility;
            GuiKit.Fill(new Rect(x, y, width, height), panel);

            // Un filet coloré à gauche : il rattache visuellement le bloc à celui qui parle,
            // et suffit à distinguer deux interlocuteurs sans changer la couleur du texte.
            GuiKit.Fill(new Rect(x, y, 3f, height), new Color(_speakerColor.r, _speakerColor.g,
                _speakerColor.b, _visibility * 0.9f));

            float inner = y + 8f;

            if (speakerHeight > 0f)
            {
                GuiKit.OutlinedLabel(new Rect(x + 14f, inner, width - 28f, 20f),
                    _current.Speaker.ToUpperInvariant(),
                    GuiKit.Style(12, FontStyle.Bold, TextAnchor.UpperLeft),
                    new Color(_speakerColor.r, _speakerColor.g, _speakerColor.b, _visibility),
                    new Color(0f, 0f, 0f, 0.85f * _visibility), 1f);

                inner += speakerHeight;
            }

            GuiKit.OutlinedLabel(new Rect(x + 14f, inner, width - 28f, textHeight),
                _current.Text, textStyle,
                new Color(_textColor.r, _textColor.g, _textColor.b, _visibility),
                new Color(0f, 0f, 0f, 0.9f * _visibility), 1.2f);
        }
    }
}
