using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>Quel backend d'input utiliser.</summary>
    public enum InputBackend
    {
        /// <summary>Choisit automatiquement ce qui est disponible dans le projet.</summary>
        Auto = 0,
        LegacyInputManager = 1,
        NewInputSystem = 2
    }

    /// <summary>Crée le bon <see cref="IInputProvider"/> selon ce qui est réellement compilé dans le projet.</summary>
    public static class InputProviderFactory
    {
        public static IInputProvider Create(InputBackend preferred)
        {
            if (preferred == InputBackend.NewInputSystem)
            {
#if ENABLE_INPUT_SYSTEM
                return new NewInputSystemProvider();
#else
                Debug.LogWarning("[UberBagarre] Backend 'NewInputSystem' demande mais le package Input System n'est pas actif. Retour au choix automatique.");
#endif
            }

            if (preferred == InputBackend.LegacyInputManager)
            {
#if ENABLE_LEGACY_INPUT_MANAGER
                return new LegacyInputProvider();
#else
                Debug.LogWarning("[UberBagarre] Backend 'LegacyInputManager' demande mais l'ancien Input Manager n'est pas actif. Retour au choix automatique.");
#endif
            }

            // Auto : l'ancien Input Manager d'abord car il ne demande aucun package.
#if ENABLE_LEGACY_INPUT_MANAGER
            return new LegacyInputProvider();
#elif ENABLE_INPUT_SYSTEM
            return new NewInputSystemProvider();
#else
            Debug.LogError("[UberBagarre] Aucun systeme d'input actif. Project Settings > Player > Active Input Handling : " +
                           "choisis 'Input Manager (Old)' ou 'Both'.");
            return new NullInputProvider();
#endif
        }
    }
}
