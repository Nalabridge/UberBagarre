using UberBagarre.Enemy;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Dans le bac à sable, le combat ne commence qu'une fois la course ACCEPTÉE sur le
    /// téléphone.
    ///
    /// C'est la règle du jeu tout entier — l'appli commande, on livre — et le bac à sable n'a
    /// aucune raison d'y faire exception : l'adversaire attend dans la rue, garde baissée,
    /// jusqu'à ce que la commande tombe et que le joueur la prenne. Ensuite, R relance les
    /// combats directement : on ne redemande pas la course à chaque essai de réglage.
    /// </summary>
    public class SandboxOrder : MonoBehaviour
    {
        [SerializeField] private PhoneDevice _phone;
        [SerializeField] private PlayerInputReader _input;

        [SerializeField]
        [Tooltip("Decocher pour retrouver l'ancien bac a sable : combat immediat.")]
        private bool _requireOrder = true;

        [SerializeField, Min(0f)]
        [Tooltip("Delai avant que la commande tombe, le temps de prendre ses reperes.")]
        private float _delay = 2f;

        private bool _accepted;
        private bool _notified;
        private float _notifyAt;
        private GUIStyle _style;

        public bool Accepted
        {
            get { return _accepted || !_requireOrder; }
        }

        private void Start()
        {
            if (!_requireOrder || _phone == null)
            {
                _accepted = true;
                return;
            }

            EnemyBrain.HoldAll = true;
            _notifyAt = Time.time + _delay;
        }

        private void OnDestroy()
        {
            if (!_accepted) EnemyBrain.HoldAll = false;
        }

        private void Update()
        {
            if (_accepted || _phone == null) return;

            if (!_notified && Time.time >= _notifyAt)
            {
                _notified = true;
                _phone.Available = true;
                _phone.SetScreen(PhoneDevice.Screen.Accueil);
                _phone.Raise();
            }

            if (!_notified || _input == null) return;

            // Accepter = E pendant que la carte de la course est affichée, téléphone levé :
            // le même geste que dans l'histoire.
            if (_input.InteractPressed && _phone.IsRaised && _phone.Current == PhoneDevice.Screen.Accueil)
            {
                Accept();
            }
        }

        /// <summary>Accepte la course et lâche les adversaires.</summary>
        public void Accept()
        {
            if (_accepted) return;

            _accepted = true;
            EnemyBrain.HoldAll = false;

            if (_phone == null) return;

            _phone.SetScreen(PhoneDevice.Screen.Mission);
            _phone.Lower();
        }

        private void OnGUI()
        {
            if (_accepted || !_notified) return;

            if (_style == null) _style = GuiKit.Style(18, FontStyle.Bold, TextAnchor.MiddleCenter);

            Rect band = new Rect(Screen.width * 0.5f - 300f, 24f, 600f, 34f);
            GuiKit.Fill(band, new Color(0f, 0f, 0f, 0.6f));

            string text = _phone != null && _phone.IsRaised
                ? "NOUVELLE COURSE — E pour accepter, le combat commence"
                : "NOUVELLE COURSE — T pour sortir le telephone";

            GuiKit.OutlinedLabel(band, text, _style, new Color(1f, 0.85f, 0.4f), new Color(0f, 0f, 0f, 0.8f), 1f);
        }
    }
}
