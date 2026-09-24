using System;
using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>D'où vient physiquement une entrée.</summary>
    public enum InputSource
    {
        Key = 0,
        MouseButton = 1
    }

    /// <summary>
    /// Une touche assignable. Sérialisable, donc éditable dans l'Inspector.
    /// On passe par cette structure (et pas par <see cref="KeyCode"/> directement)
    /// pour pouvoir assigner indifféremment une touche clavier ou un bouton souris.
    /// </summary>
    [Serializable]
    public struct InputBinding
    {
        [Tooltip("Touche clavier, ou bouton de souris ?")]
        public InputSource source;

        [Tooltip("Touche utilisée quand la source est 'Key'.")]
        public KeyCode key;

        [Tooltip("Bouton utilisé quand la source est 'MouseButton' : 0 = gauche, 1 = droit, 2 = molette.")]
        public int mouseButton;

        /// <summary>
        /// Seconde touche qui fait la même chose. Elle existe pour les claviers AZERTY : l'ancien
        /// Input Manager lit la LETTRE produite par la touche, pas sa position, donc W / A ne
        /// tombent pas sous les doigts en AZERTY. Z et Q en secours rendent ZQSD jouable sans
        /// casser WASD.
        /// </summary>
        [Tooltip("Touche de secours (ex. Z pour avancer en AZERTY). None = aucune.")]
        public KeyCode alternateKey;

        public static InputBinding FromKey(KeyCode keyCode)
        {
            return new InputBinding { source = InputSource.Key, key = keyCode, mouseButton = 0 };
        }

        /// <summary>Une touche et sa touche de secours.</summary>
        public static InputBinding FromKeys(KeyCode keyCode, KeyCode alternate)
        {
            return new InputBinding { source = InputSource.Key, key = keyCode, mouseButton = 0, alternateKey = alternate };
        }

        public static InputBinding FromMouse(int button)
        {
            return new InputBinding { source = InputSource.MouseButton, key = KeyCode.None, mouseButton = button };
        }

        /// <summary>Une touche laissée sur None est considérée comme "non assignée" et ne déclenche rien.</summary>
        public bool IsAssigned
        {
            get { return source == InputSource.MouseButton || key != KeyCode.None || alternateKey != KeyCode.None; }
        }

        public override string ToString()
        {
            if (source == InputSource.MouseButton)
            {
                switch (mouseButton)
                {
                    case 0: return "Clic gauche";
                    case 1: return "Clic droit";
                    case 2: return "Clic molette";
                    default: return "Souris " + mouseButton;
                }
            }

            if (key == KeyCode.None && alternateKey == KeyCode.None) return "(non assigne)";
            if (alternateKey == KeyCode.None || alternateKey == key) return key.ToString();
            if (key == KeyCode.None) return alternateKey.ToString();

            return key + " / " + alternateKey;
        }
    }
}
