using System;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Une chose sur laquelle on peut appuyer sur E : une porte, une voiture, une table.
    ///
    /// Le composant ne fait RIEN tout seul, et c'est voulu : il déclare une intention
    /// (« ceci est activable, voici ce que ça dit, voici sa portée ») et laisse le système
    /// d'interaction décider quand la déclencher. Sans cette séparation, chaque objet
    /// interactif aurait sa propre lecture du clavier et sa propre idée de ce qu'est « être
    /// assez près » — et deux objets voisins finiraient par s'activer en même temps.
    /// </summary>
    public class Interactable : MonoBehaviour
    {
        [Header("Affichage")]
        [SerializeField]
        [Tooltip("Ce que le joueur lit : un VERBE. « Voiture » ne dit pas ce qui va se passer, " +
                 "« Monter en voiture » si.")]
        private string _label = "Interagir";

        [SerializeField]
        [Tooltip("Seconde ligne, facultative : la raison, pas l'action.")]
        private string _hint;

        [Header("Portee")]
        [SerializeField, Min(0.2f)] private float _range = 2.6f;

        [SerializeField]
        [Tooltip("Point vise par le systeme d'interaction. Vide = le centre de l'objet.")]
        private Transform _focus;

        [Header("Etat")]
        [SerializeField]
        [Tooltip("Une interaction desactivee reste dans la scene mais n'est ni visee ni affichee. " +
                 "C'est ainsi que le scenario ouvre et ferme des actions sans creer ni detruire " +
                 "d'objets en cours de partie.")]
        private bool _enabledForPlayer = true;

        [SerializeField]
        [Tooltip("Ne peut etre activee qu'une fois.")]
        private bool _once;

        private bool _used;

        /// <summary>Déclenché quand le joueur active l'objet.</summary>
        public event Action<Interactable> Activated;

        public string Label { get { return _label; } set { _label = value; } }
        public string Hint { get { return _hint; } set { _hint = value; } }
        public float Range { get { return _range; } }

        public Vector3 FocusPoint
        {
            get { return _focus != null ? _focus.position : transform.position; }
        }

        public bool Available
        {
            get { return _enabledForPlayer && !(_once && _used); }
        }

        public void SetAvailable(bool available)
        {
            _enabledForPlayer = available;
        }

        /// <summary>Remet une interaction « une seule fois » à zéro (relance du prologue).</summary>
        public void ResetUsage()
        {
            _used = false;
        }

        public void Activate()
        {
            if (!Available) return;

            _used = true;
            if (Activated != null) Activated(this);
        }
    }
}
