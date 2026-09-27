using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Un battant de porte automatique (la concession, la station-service) : il glisse entre sa
    /// place fermée et sa place ouverte quand le joueur s'approche, et se referme derrière lui.
    /// La carte donne les deux places : les repères « Closed » et « Open » posés à côté du
    /// battant. Le chuintement vient du son de la porte d'origine s'il existe.
    /// </summary>
    public class SlidingDoor : MonoBehaviour
    {
        [SerializeField] private Vector3 _closedLocal;
        [SerializeField] private Vector3 _openLocal;
        [SerializeField, Min(0.5f)] private float _radius = 2.6f;
        [SerializeField, Min(0.1f)] private float _speed = 1.8f;
        [SerializeField] private AudioSource _sound;

        private float _t;
        private bool _wasOpening;

        /// <summary>Pose le battant entre ses deux repères (à l'exécution).</summary>
        public static SlidingDoor Install(Transform panel, Transform closed, Transform open, AudioSource sound)
        {
            if (panel == null || closed == null || open == null || panel.parent == null) return null;

            SlidingDoor door = panel.GetComponent<SlidingDoor>();
            if (door == null)
            {
                if (panel.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody body = panel.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                }

                door = panel.gameObject.AddComponent<SlidingDoor>();
            }

            // Le battant est posé sur son repère fermé ; l'ouvert est décalé d'autant.
            Transform parent = panel.parent;
            door._closedLocal = panel.localPosition;
            door._openLocal = panel.localPosition + parent.InverseTransformVector(open.position - closed.position);
            door._sound = sound;
            return door;
        }

        private void Update()
        {
            Transform viewer = SwingDoor.Viewer;
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;

            Vector3 center = transform.parent != null ? transform.parent.TransformPoint(_closedLocal) : transform.position;
            bool opening = viewer != null && (viewer.position - center).sqrMagnitude < _radius * _radius;

            if (opening != _wasOpening && _sound != null && _sound.clip != null) _sound.Play();
            _wasOpening = opening;

            float target = opening ? 1f : 0f;
            if (Mathf.Approximately(_t, target)) return;

            _t = Mathf.MoveTowards(_t, target, _speed * Time.deltaTime);
            float eased = _t * _t * (3f - 2f * _t);
            transform.localPosition = Vector3.Lerp(_closedLocal, _openLocal, eased);
        }
    }
}
