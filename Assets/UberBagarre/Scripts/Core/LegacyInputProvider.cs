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
            if (binding.source == InputSource.MouseButton) return Input.GetMouseButton(binding.mouseButton);

            return Held(binding.key) || Held(binding.alternateKey);
        }

        public bool GetPressedThisFrame(InputBinding binding)
        {
            if (!binding.IsAssigned) return false;
            if (binding.source == InputSource.MouseButton) return Input.GetMouseButtonDown(binding.mouseButton);

            return Pressed(binding.key) || Pressed(binding.alternateKey);
        }

        public bool GetReleasedThisFrame(InputBinding binding)
        {
            if (!binding.IsAssigned) return false;
            if (binding.source == InputSource.MouseButton) return Input.GetMouseButtonUp(binding.mouseButton);

            return Released(binding.key) || Released(binding.alternateKey);
        }

        private static bool Held(KeyCode key)
        {
            return key != KeyCode.None && Input.GetKey(key);
        }

        private static bool Pressed(KeyCode key)
        {
            return key != KeyCode.None && Input.GetKeyDown(key);
        }

        private static bool Released(KeyCode key)
        {
            return key != KeyCode.None && Input.GetKeyUp(key);
        }

        public float GetScrollDelta()
        {
            float y = Input.mouseScrollDelta.y;
            return Mathf.Abs(y) < 0.001f ? 0f : Mathf.Clamp(y, -4f, 4f);
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
