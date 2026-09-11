#if ENABLE_INPUT_SYSTEM
using UnityEngine;
using UnityEngine.InputSystem;

namespace UberBagarre.Core
{
    /// <summary>
    /// Backend "nouveau Input System" (package com.unity.inputsystem).
    /// Compilé uniquement si le package est installé et actif.
    ///
    /// On utilise volontairement l'API bas niveau (Keyboard.current / Mouse.current)
    /// plutôt qu'un asset .inputactions : aucune génération de code, aucun asset à maintenir,
    /// et les touches restent décrites par le même ScriptableObject InputBindings que l'autre backend.
    /// </summary>
    public sealed class NewInputSystemProvider : IInputProvider
    {
        /// <summary>
        /// Le nouveau système renvoie un delta en pixels écran, l'ancien un delta déjà multiplié
        /// par la sensibilité 0.1 de l'Input Manager. On normalise pour que la visée soit
        /// identique quel que soit le backend actif.
        /// </summary>
        private const float MouseDeltaToLegacyScale = 0.1f;

        public string DisplayName
        {
            get { return "Input System (nouveau systeme)"; }
        }

        public bool GetHeld(InputBinding binding)
        {
            ButtonControl control = ResolveControl(binding);
            return control != null && control.isPressed;
        }

        public bool GetPressedThisFrame(InputBinding binding)
        {
            ButtonControl control = ResolveControl(binding);
            return control != null && control.wasPressedThisFrame;
        }

        public bool GetReleasedThisFrame(InputBinding binding)
        {
            ButtonControl control = ResolveControl(binding);
            return control != null && control.wasReleasedThisFrame;
        }

        public Vector2 GetLookDelta()
        {
            Mouse mouse = Mouse.current;
            if (mouse == null) return Vector2.zero;
            return mouse.delta.ReadValue() * MouseDeltaToLegacyScale;
        }

        private static ButtonControl ResolveControl(InputBinding binding)
        {
            if (!binding.IsAssigned) return null;

            if (binding.source == InputSource.MouseButton)
            {
                Mouse mouse = Mouse.current;
                if (mouse == null) return null;
                switch (binding.mouseButton)
                {
                    case 0: return mouse.leftButton;
                    case 1: return mouse.rightButton;
                    case 2: return mouse.middleButton;
                    default: return null;
                }
            }

            Keyboard keyboard = Keyboard.current;
            if (keyboard == null) return null;

            Key key = ToKey(binding.key);
            return key == Key.None ? null : keyboard[key];
        }

        /// <summary>Traduit un KeyCode (ancien système) vers une Key (nouveau système).</summary>
        private static Key ToKey(KeyCode code)
        {
            switch (code)
            {
                case KeyCode.A: return Key.A;
                case KeyCode.B: return Key.B;
                case KeyCode.C: return Key.C;
                case KeyCode.D: return Key.D;
                case KeyCode.E: return Key.E;
                case KeyCode.F: return Key.F;
                case KeyCode.G: return Key.G;
                case KeyCode.H: return Key.H;
                case KeyCode.I: return Key.I;
                case KeyCode.J: return Key.J;
                case KeyCode.K: return Key.K;
                case KeyCode.L: return Key.L;
                case KeyCode.M: return Key.M;
                case KeyCode.N: return Key.N;
                case KeyCode.O: return Key.O;
                case KeyCode.P: return Key.P;
                case KeyCode.Q: return Key.Q;
                case KeyCode.R: return Key.R;
                case KeyCode.S: return Key.S;
                case KeyCode.T: return Key.T;
                case KeyCode.U: return Key.U;
                case KeyCode.V: return Key.V;
                case KeyCode.W: return Key.W;
                case KeyCode.X: return Key.X;
                case KeyCode.Y: return Key.Y;
                case KeyCode.Z: return Key.Z;

                case KeyCode.Alpha0: return Key.Digit0;
                case KeyCode.Alpha1: return Key.Digit1;
                case KeyCode.Alpha2: return Key.Digit2;
                case KeyCode.Alpha3: return Key.Digit3;
                case KeyCode.Alpha4: return Key.Digit4;
                case KeyCode.Alpha5: return Key.Digit5;
                case KeyCode.Alpha6: return Key.Digit6;
                case KeyCode.Alpha7: return Key.Digit7;
                case KeyCode.Alpha8: return Key.Digit8;
                case KeyCode.Alpha9: return Key.Digit9;

                case KeyCode.Space: return Key.Space;
                case KeyCode.Return: return Key.Enter;
                case KeyCode.Escape: return Key.Escape;
                case KeyCode.Tab: return Key.Tab;
                case KeyCode.Backspace: return Key.Backspace;
                case KeyCode.LeftShift: return Key.LeftShift;
                case KeyCode.RightShift: return Key.RightShift;
                case KeyCode.LeftControl: return Key.LeftCtrl;
                case KeyCode.RightControl: return Key.RightCtrl;
                case KeyCode.LeftAlt: return Key.LeftAlt;
                case KeyCode.RightAlt: return Key.RightAlt;

                case KeyCode.UpArrow: return Key.UpArrow;
                case KeyCode.DownArrow: return Key.DownArrow;
                case KeyCode.LeftArrow: return Key.LeftArrow;
                case KeyCode.RightArrow: return Key.RightArrow;

                case KeyCode.F1: return Key.F1;
                case KeyCode.F2: return Key.F2;
                case KeyCode.F3: return Key.F3;
                case KeyCode.F4: return Key.F4;
                case KeyCode.F5: return Key.F5;
                case KeyCode.F6: return Key.F6;
                case KeyCode.F7: return Key.F7;
                case KeyCode.F8: return Key.F8;
                case KeyCode.F9: return Key.F9;
                case KeyCode.F10: return Key.F10;
                case KeyCode.F11: return Key.F11;
                case KeyCode.F12: return Key.F12;

                default: return Key.None;
            }
        }
    }
}
#endif
