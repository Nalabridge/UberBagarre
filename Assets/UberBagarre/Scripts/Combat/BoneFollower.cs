using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Colle un objet à un os, après toute l'animation (y compris la réaction physique aux
    /// coups). Sert aux zones touchables : la tête qu'on frappe est là où la tête EST — penchée,
    /// esquivée, projetée en arrière par le coup précédent — et pas dans une capsule fixe
    /// plantée au-dessus des pieds.
    ///
    /// Les zones restent rangées sous un même parent (désactivé d'un bloc à la mort) : c'est
    /// ce composant, et non la hiérarchie, qui les attache au squelette.
    /// </summary>
    [DefaultExecutionOrder(600)]
    public class BoneFollower : MonoBehaviour
    {
        [SerializeField] private Transform _bone;
        [SerializeField] private Vector3 _offset;

        public Transform Bone { get { return _bone; } }

        private void LateUpdate()
        {
            if (_bone == null) return;
            transform.SetPositionAndRotation(_bone.TransformPoint(_offset), _bone.rotation);
        }
    }
}
