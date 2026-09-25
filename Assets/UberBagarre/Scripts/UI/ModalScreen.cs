using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Un écran qui prend toute l'attention : un menu, une lettre qu'on lit.
    ///
    /// Pendant ce temps, rien du jeu ne doit répondre aux touches : E ne doit pas activer la
    /// porte derrière la lettre, la touche du téléphone ne doit pas le sortir par-dessus le
    /// menu. Plutôt que d'apprendre à chaque système l'existence de chaque écran, les écrans
    /// se déclarent ici et les systèmes n'ont qu'une question à poser.
    ///
    /// Un verrou par propriétaire, comme pour les commandes : un menu de pause ouvert par-dessus
    /// une lettre ne libère pas le jeu en se refermant.
    /// </summary>
    public static class ModalScreen
    {
        private static readonly List<object> Owners = new List<object>(2);

        /// <summary>Un écran plein est-il ouvert ?</summary>
        public static bool Active
        {
            get { return Owners.Count > 0; }
        }

        public static void Set(object owner, bool open)
        {
            if (owner == null) return;

            if (open)
            {
                if (!Owners.Contains(owner)) Owners.Add(owner);
            }
            else
            {
                Owners.Remove(owner);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Owners.Clear();
        }
    }
}
