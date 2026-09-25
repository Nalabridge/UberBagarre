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

        [Header("Securite")]
        [SerializeField]
        [Tooltip("Sous cette hauteur (sous l'arrivee du lieu courant), le joueur est considere comme " +
                 "tombe hors du decor et ramene a l'arrivee.")]
        private float _fallLimit = 12f;

        private int _current = -1;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Déclenché après chaque arrivée, avec le nom du lieu.</summary>
        public event Action<string> Arrived;

        /// <summary>
        /// Filet de sécurité sous le décor. Le menu de triche le coupe pendant le vol libre :
        /// passer sous le sol y est voulu.
        /// </summary>
        public bool FallGuard
        {
            get { return !_fallGuardSuspended; }
            set { _fallGuardSuspended = !value; }
        }

        private bool _fallGuardSuspended;

        public int Count
        {
            get { return _locations.Length; }
        }

        public string NameAt(int index)
        {
            if (index < 0 || index >= _locations.Length || _locations[index] == null) return string.Empty;
            return _locations[index].name;
        }

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

        /// <summary>
        /// Filet de sécurité : un joueur qui passe sous le sol est ramené à l'arrivée du lieu.
        ///
        /// Tomber dans le vide ne doit JAMAIS laisser la partie dans un état sans issue : le
        /// scénario attend une action que le joueur ne peut plus faire, et rien à l'écran ne dit
        /// quoi faire. Le cas est journalisé, pour qu'on sache où le sol manquait.
        /// </summary>
        private void LateUpdate()
        {
            if (_player == null || _current < 0 || _current >= _locations.Length) return;
            if (_fallGuardSuspended) return;

            Location location = _locations[_current];
            if (location == null || location.arrival == null) return;

            if (_player.transform.position.y > location.arrival.position.y - _fallLimit) return;

            Debug.LogWarning("[UberBagarre] Le joueur est tombe sous le decor (" + location.name + ", " +
                             _player.transform.position + ") : retour a l'arrivee.", this);

            Teleport(location.arrival.position, location.arrival.rotation);
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

            // Les colliders du lieu qu'on vient d'allumer doivent exister dans la simulation
            // AVANT que le joueur y soit pose : sinon, la premiere image, il n'y a rien sous lui.
            Physics.SyncTransforms();

            Location destination = _locations[index];

            if (destination.arrival != null)
            {
                Teleport(destination.arrival.position, destination.arrival.rotation);
            }
            else
            {
                Debug.LogWarning("[UberBagarre] Le lieu '" + destination.name + "' n'a pas de point d'arrivee : " +
                                 "le joueur n'est pas deplace.", this);
            }

            Action<string> handler = Arrived;
            if (handler != null) handler(destination.name);
        }

        private void Teleport(Vector3 position, Quaternion rotation)
        {
            if (_player == null) return;

            position = SnapToGround(position);

            _player.transform.SetPositionAndRotation(position, rotation);

            ISpawnReceiver[] receivers = _player.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++)
            {
                receivers[i].OnSpawned(position, rotation);
            }

            Physics.SyncTransforms();
        }

        /// <summary>
        /// Pose le point d'arrivée sur le VRAI sol : un rayon vers le bas depuis un peu au-dessus
        /// du point prévu. Si le décor a bougé, ou si le point est un poil sous la surface, le
        /// joueur atterrit quand même sur quelque chose. Si rien n'est trouvé, c'est signalé.
        /// </summary>
        private Vector3 SnapToGround(Vector3 position)
        {
            Vector3 origin = position + Vector3.up * 2.5f;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 8f, ~0, QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            bool found = false;
            Vector3 ground = position;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hits[i].collider;
                if (collider == null || collider.attachedRigidbody != null) continue;

                // Un autre personnage n'est pas un sol : on n'atterrit pas sur une tete.
                if (collider is CharacterController) continue;
                if (collider.transform.IsChildOf(_player.transform)) continue;
                if (_hits[i].distance >= best) continue;

                best = _hits[i].distance;
                ground = _hits[i].point;
                found = true;
            }

            if (!found)
            {
                Debug.LogWarning("[UberBagarre] Aucun sol sous le point d'arrivee " + position + ".", this);
                return position;
            }

            return ground + Vector3.up * 0.03f;
        }
    }
}
