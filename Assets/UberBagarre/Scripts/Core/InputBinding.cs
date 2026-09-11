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

        public static InputBinding FromKey(KeyCode keyCode)
        {
            return new InputBinding { source = InputSource.Key, key = keyCode, mouseButton = 0 };
        }

        public static InputBinding FromMouse(int button)
        {
            return new InputBinding { source = InputSource.MouseButton, key = KeyCode.None, mouseButton = button };
        }

        /// <summary>Une touche laissée sur None est considérée comme "non assignée" et ne déclenche rien.</summary>
        public bool IsAssigned
        {
            get { return source == InputSource.MouseButton || key != KeyCode.None; }
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

            return key == KeyCode.None ? "(non assigne)" : key.ToString();
        }
    }
}
