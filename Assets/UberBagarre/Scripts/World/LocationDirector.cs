using System;
using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Les lieux du jeu, et le passage de l'un à l'autre.
    ///
    /// Le prologue se passe à deux endroits : la maison et la rue devant le club. Ils vivent
    /// dans la MÊME scène, très loin l'un de l'autre, et un seul est allumé à la fois.
    ///
    /// Pourquoi pas deux scènes Unity : parce que tout ce qui traverse le prologue — le joueur,
    /// son téléphone, le scénario, la fiche de mission — devrait alors survivre au chargement,
    /// donc être marqué comme persistant, donc exister en double le temps d'une transition. Le
    /// premier bogue de ce genre est invisible et le second est incompréhensible. Un
    /// déplacement dans une scène unique n'a aucun de ces problèmes, et le fondu au noir rend
    /// les deux méthodes strictement identiques pour le joueur.
    ///
    /// La contrepartie assumée : une scène plus lourde à charger, et des coordonnées monde très
    /// écartées. Aucune des deux ne se voit en jeu.
    ///
    /// Le déplacement passe par ISpawnReceiver, exactement comme le spawn de combat : c'est le
    /// seul chemin qui désactive le CharacterController le temps du saut ET réaligne l'angle de
    /// visée. Déplacer le joueur « à la main » le ferait revenir à sa position d'avant dès
    /// l'image suivante, sans la moindre erreur pour l'expliquer.
    /// </summary>
    public class LocationDirector : MonoBehaviour
    {
        [Serializable]
        public class Location
        {
            [Tooltip("Nom lisible, utilise par le scenario : « Maison », « Rue ».")]
            public string name = "Lieu";

            [Tooltip("Racine du decor. Elle est desactivee quand on n'y est pas.")]
            public Transform root;

            [Tooltip("Ou arrive le joueur, et dans quelle direction il regarde.")]
            public Transform arrival;
        }

        [Header("References")]
        [SerializeField] private GameObject _player;

        [Header("Lieux")]
        [SerializeField] private Location[] _locations = new Location[0];

        [SerializeField]
        [Tooltip("Lieu actif au demarrage.")]
        private int _startIndex;

        private int _current = -1;

        /// <summary>Déclenché après chaque arrivée, avec le nom du lieu.</summary>
        public event Action<string> Arrived;

        public string CurrentName
        {
            get
            {
                if (_current < 0 || _current >= _locations.Length) return string.Empty;
                return _locations[_current].name;
            }
        }

        private void Awake()
        {
            // Au démarrage on n'allume que le lieu de départ, sans téléporter : le joueur y est
            // déjà placé par le générateur de scène, et le déplacer ferait perdre l'orientation
            // de départ écrite dans le décor.
            for (int i = 0; i < _locations.Length; i++)
            {
                Location location = _locations[i];
                if (location == null || location.root == null) continue;

                location.root.gameObject.SetActive(i == _startIndex);
            }

            _current = _startIndex;
        }

        public bool GoTo(string locationName)
        {
            for (int i = 0; i < _locations.Length; i++)
            {
                if (_locations[i] == null || _locations[i].name != locationName) continue;

                GoTo(i);
                return true;
            }

            Debug.LogWarning("[UberBagarre] Lieu inconnu : '" + locationName + "'.", this);
            return false;
        }

        public void GoTo(int index)
        {
            if (index < 0 || index >= _locations.Length)
            {
                Debug.LogWarning("[UberBagarre] Index de lieu hors bornes : " + index + ".", this);
                return;
            }

            for (int i = 0; i < _locations.Length; i++)
            {
                Location location = _locations[i];
                if (location == null || location.root == null) continue;

                location.root.gameObject.SetActive(i == index);
            }

            _current = index;

            Location destination = _locations[index];
            if (destination.arrival != null) Teleport(destination.arrival.position, destination.arrival.rotation);

            Action<string> handler = Arrived;
            if (handler != null) handler(destination.name);
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_player == null) return;

            _player.transform.SetPositionAndRotation(position, rotation);

            ISpawnReceiver[] receivers = _player.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++)
            {
                receivers[i].OnSpawned(position, rotation);
            }
        }
    }
}
