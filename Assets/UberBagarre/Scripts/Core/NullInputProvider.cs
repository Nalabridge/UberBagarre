using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// Backend de secours : ne lit rien du tout.
    /// Utilisé si le projet n'a ni l'ancien Input Manager ni le nouveau Input System actifs,
    /// ce qui ne devrait jamais arriver, mais évite un crash silencieux si ça arrive.
    /// </summary>
    public sealed class NullInputProvider : IInputProvider
    {
        public string DisplayName
        {
            get { return "AUCUN backend d'input actif"; }
        }

        public bool GetHeld(InputBinding binding) { return false; }
        public bool GetPressedThisFrame(InputBinding binding) { return false; }
        public bool GetReleasedThisFrame(InputBinding binding) { return false; }
        public Vector2 GetLookDelta() { return Vector2.zero; }
    }
}
