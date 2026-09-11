using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Marqueur de position d'apparition. Aucun comportement : c'est une donnée de niveau.
    /// Déplace/oriente simplement cet objet dans la scène pour changer le spawn,
    /// la flèche du gizmo indique la direction de regard.
    /// </summary>
    public class SpawnPoint : MonoBehaviour
    {
        [SerializeField] private string _label = "Spawn";
        [SerializeField] private Color _gizmoColor = new Color(0.25f, 0.85f, 1f, 1f);
        [SerializeField, Min(0.05f)] private float _radius = 0.35f;
        [SerializeField, Min(0.1f)] private float _height = 1.8f;

        public string Label { get { return _label; } }
        public Vector3 Position { get { return transform.position; } }
        public Quaternion Rotation { get { return transform.rotation; } }

        private void OnDrawGizmos()
        {
            Gizmos.color = _gizmoColor;

            Vector3 basePos = transform.position;
            Vector3 topPos = basePos + Vector3.up * _height;

            Gizmos.DrawWireSphere(basePos + Vector3.up * _radius, _radius);
            Gizmos.DrawLine(basePos, topPos);

            // Flèche de direction
            Vector3 eye = basePos + Vector3.up * (_height * 0.9f);
            Vector3 tip = eye + transform.forward * 0.9f;
            Gizmos.DrawLine(eye, tip);
            Gizmos.DrawLine(tip, tip - transform.forward * 0.25f + transform.right * 0.15f);
            Gizmos.DrawLine(tip, tip - transform.forward * 0.25f - transform.right * 0.15f);
        }
    }
}
