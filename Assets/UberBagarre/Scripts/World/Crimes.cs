using System;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>Ce que la police peut reprocher au joueur.</summary>
    public enum Crime
    {
        /// <summary>Une bagarre en pleine rue (pas une course de l'appli dans un coin abrité).</summary>
        Bagarre,

        /// <summary>Frapper un passant qui n'avait rien demandé.</summary>
        Agression,

        /// <summary>Frapper quelqu'un qui est déjà au sol.</summary>
        Acharnement,

        /// <summary>Crocheter, voler une voiture garée.</summary>
        VolDeVoiture,

        /// <summary>Sortir un conducteur de sa voiture.</summary>
        Carjacking,

        /// <summary>Renverser un piéton.</summary>
        Delit,

        /// <summary>Frapper un policier.</summary>
        AgressionPolicier,

        /// <summary>Entrer par effraction.</summary>
        Effraction
    }

    /// <summary>
    /// Le point de passage entre ce que fait le joueur et la police : chaque système signale ce
    /// qui s'est passé, où, et contre qui ; la police décide qui l'a vu et ce qu'elle en fait.
    /// </summary>
    public static class Crimes
    {
        /// <summary>Un délit : quoi, où, la victime (ou null).</summary>
        public static event Action<Crime, Vector3, GameObject> Committed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Committed = null;
        }

        public static void Report(Crime crime, Vector3 position, GameObject victim = null)
        {
            Action<Crime, Vector3, GameObject> handler = Committed;
            if (handler != null) handler(crime, position, victim);
        }

        public static string Describe(Crime crime)
        {
            switch (crime)
            {
                case Crime.Bagarre: return "Rixe sur la voie publique";
                case Crime.Agression: return "Agression";
                case Crime.Acharnement: return "Coups sur une personne à terre";
                case Crime.VolDeVoiture: return "Vol de véhicule";
                case Crime.Carjacking: return "Vol de véhicule avec violence";
                case Crime.Delit: return "Délit de fuite";
                case Crime.AgressionPolicier: return "Violences sur agent";
                case Crime.Effraction: return "Effraction";
                default: return "Délit";
            }
        }
    }
}
