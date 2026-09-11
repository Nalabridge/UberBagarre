using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// Abstraction au-dessus du système d'input d'Unity.
    /// Le gameplay ne parle JAMAIS directement à UnityEngine.Input : il parle à cette interface.
    /// Conséquence : on peut changer de backend (ancien Input Manager, nouveau Input System,
    /// rejeu de test automatisé, enregistrement de replay...) sans toucher au gameplay.
    /// </summary>
    public interface IInputProvider
    {
        string DisplayName { get; }

        bool GetHeld(InputBinding binding);
        bool GetPressedThisFrame(InputBinding binding);
        bool GetReleasedThisFrame(InputBinding binding);

        /// <summary>Déplacement de la souris depuis la frame précédente, normalisé pour être identique entre backends.</summary>
        Vector2 GetLookDelta();
    }
}
