using System;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// Un personnage de l'histoire à qui l'on parle (E) : Coach Ray à la salle, Lina au cabinet,
    /// maman à la laverie, Nestor devant le motel… Il n'existe que quand l'histoire l'a posé
    /// quelque part ; debout, il regarde le joueur qui s'approche. Certains marchent (on suit
    /// Sami, on escorte Mme Keller).
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class StoryActor : MonoBehaviour
    {
        [SerializeField] private string _id = "ACTEUR";
        [SerializeField] private string _displayName = "Quelqu'un";
        [SerializeField] private Spectator _idle;
        [SerializeField] private MocapWalker _walker;

        private Interactable _talk;
        private Transform _home;

        public string Id { get { return _id; } }
        public string DisplayName { get { return _displayName; } }
        public MocapWalker Walker { get { return _walker; } }
        public bool Placed { get { return gameObject.activeSelf; } }

        /// <summary>Le joueur lui parle.</summary>
        public event Action<StoryActor> Talked;

        private void Awake()
        {
            _talk = GetComponent<Interactable>();
            _home = transform.parent;
            if (_talk != null) _talk.Activated += OnTalk;
        }

        private void OnDestroy()
        {
            if (_talk != null) _talk.Activated -= OnTalk;
        }

        private void OnTalk(Interactable source)
        {
            Action<StoryActor> handler = Talked;
            if (handler != null) handler(this);
        }

        /// <summary>Le poser là, face à <paramref name="yaw"/> ; sous <paramref name="parent"/> (un intérieur) si donné.</summary>
        public void Place(Vector3 position, float yaw, Transform parent = null)
        {
            transform.SetParent(parent != null ? parent : _home, true);
            transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            if (_walker != null) _walker.enabled = false;
            if (_idle != null)
            {
                _idle.enabled = true;
                _idle.Focus = SwingDoor.Viewer;
            }

            gameObject.SetActive(true);
            SetTalkable(true);
        }

        public void Hide()
        {
            gameObject.SetActive(false);
            transform.SetParent(_home, true);
        }

        public void SetTalkable(bool talkable)
        {
            if (_talk != null) _talk.SetAvailable(talkable);
        }

        public void SetLabel(string label)
        {
            if (_talk != null) _talk.Label = label;
        }

        /// <summary>Il part, et marche ce trajet une fois (s'il sait marcher).</summary>
        public bool Walk(Vector3[] path, float speed)
        {
            if (_walker == null || path == null || path.Length < 2) return false;
            transform.SetParent(_home, true);
            gameObject.SetActive(true);
            if (_idle != null) _idle.enabled = false;
            _walker.Walk(path, speed);
            return true;
        }

        public void Configure(string id, string displayName, Spectator idle, MocapWalker walker)
        {
            _id = id;
            _displayName = displayName;
            _idle = idle;
            _walker = walker;
        }
    }
}
