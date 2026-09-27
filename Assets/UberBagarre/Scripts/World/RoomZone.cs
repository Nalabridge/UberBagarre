using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Une pièce, vue comme une boîte : sert à savoir si quelqu'un y est (la salle du fond du
    /// Vertigo, où la commande du Taureau tombe quand on y entre).
    /// </summary>
    public class RoomZone : MonoBehaviour
    {
        [SerializeField] private Vector3 _size = new Vector3(4f, 3f, 4f);

        public Vector3 Size { get { return _size; } }

        /// <summary>Le point (monde) est-il dans la pièce ? Faux si la pièce est éteinte.</summary>
        public bool Contains(Vector3 world)
        {
            if (!isActiveAndEnabled) return false;
            Vector3 local = transform.InverseTransformPoint(world);
            Vector3 half = _size * 0.5f;
            return Mathf.Abs(local.x) <= half.x && Mathf.Abs(local.y) <= half.y && Mathf.Abs(local.z) <= half.z;
        }

        /// <summary>Le joueur (celui qui pousse les portes) est-il dans la pièce ?</summary>
        public bool ContainsPlayer()
        {
            Transform viewer = SwingDoor.Viewer;
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            return viewer != null && Contains(viewer.position);
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = transform.localToWorldMatrix;
            Gizmos.color = new Color(1f, 0.2f, 0.2f, 0.6f);
            Gizmos.DrawWireCube(Vector3.zero, _size);
        }
    }
}
