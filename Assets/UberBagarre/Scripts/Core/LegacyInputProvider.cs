#if ENABLE_LEGACY_INPUT_MANAGER
using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// Backend "ancien Input Manager" (UnityEngine.Input).
    /// Compilé uniquement si Project Settings > Player > Active Input Handling
    /// vaut "Input Manager (Old)" ou "Both".
    /// </summary>
    public sealed class LegacyInputProvider : IInputProvider
    {
        private bool _mouseAxesAvailable = true;

        public string DisplayName
        {
            get { return "Input Manager (ancien systeme)"; }
        }

        public bool GetHeld(InputBinding binding)
        {
            if (!binding.IsAssigned) return false;
            return binding.source == InputSource.MouseButton
                ? Input.GetMouseButton(binding.mouseButton)
                : Input.GetKey(binding.key);
        }

        public bool GetPressedThisFrame(InputBinding binding)
        {
            if (!binding.IsAssigned) return false;
            return binding.source == InputSource.MouseButton
                ? Input.GetMouseButtonDown(binding.mouseButton)
                : Input.GetKeyDown(binding.key);
        }

        public bool GetReleasedThisFrame(InputBinding binding)
        {
            if (!binding.IsAssigned) return false;
            return binding.source == InputSource.MouseButton
                ? Input.GetMouseButtonUp(binding.mouseButton)
                : Input.GetKeyUp(binding.key);
        }

        public Vector2 GetLookDelta()
        {
            if (!_mouseAxesAvailable) return Vector2.zero;

            // Les axes "Mouse X" / "Mouse Y" existent par defaut dans l'Input Manager,
            // mais un projet peut les avoir supprimes : on degrade proprement au lieu de spammer des exceptions.
            try
            {
                return new Vector2(Input.GetAxisRaw("Mouse X"), Input.GetAxisRaw("Mouse Y"));
            }
            catch (System.ArgumentException)
            {
                _mouseAxesAvailable = false;
                Debug.LogError("[UberBagarre] Les axes souris 'Mouse X' / 'Mouse Y' sont absents de l'Input Manager. " +
                               "Project Settings > Input Manager : ils doivent exister pour que la visee fonctionne.");
                return Vector2.zero;
            }
        }
    }
}
#endif
