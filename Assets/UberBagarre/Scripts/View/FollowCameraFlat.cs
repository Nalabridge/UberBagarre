using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Garde un objet au-dessus de la caméra, sans la suivre en hauteur ni en rotation.
    ///
    /// Sert à la bruine. Une pluie qui couvrirait toute la rue demanderait cent fois plus de
    /// particules pour un résultat identique : seules celles qui sont dans le champ comptent.
    /// On déplace donc un volume d'émission modeste au-dessus du joueur.
    ///
    /// La hauteur, elle, reste FIXE. Si le volume suivait la caméra en y, les gouttes
    /// naîtraient toujours à la même distance au-dessus des yeux et sembleraient accrochées
    /// à la tête du joueur — le défaut classique des systèmes de pluie attachés à la caméra.
    ///
    /// Le déplacement est quantifié : bouger l'émetteur en continu ferait glisser toute la
    /// nappe de pluie avec le joueur, ce que l'œil lit immédiatement comme un décalage. En
    /// ne le déplaçant que par pas de plusieurs mètres, les gouttes déjà tombées restent où
    /// elles sont et seules les nouvelles apparaissent ailleurs.
    /// </summary>
    [DisallowMultipleComponent]
    public class FollowCameraFlat : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Caméra suivie. Vide = la caméra active au moment du démarrage.")]
        private Transform _target;

        [SerializeField, Min(0f)]
        [Tooltip("Hauteur fixe de l'émetteur, indépendante de la caméra.")]
        private float _height = 14f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Pas de déplacement. Plus il est grand, moins la nappe glisse avec le joueur.")]
        private float _step = 6f;

        private void LateUpdate()
        {
            if (_target == null)
            {
                Camera camera = Camera.main;
                if (camera == null) return;

                _target = camera.transform;
            }

            Vector3 position = _target.position;

            float x = Mathf.Round(position.x / _step) * _step;
            float z = Mathf.Round(position.z / _step) * _step;

            transform.position = new Vector3(x, _height, z);
        }
    }
}
