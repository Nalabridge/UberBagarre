using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Un membre à deux os piloté par IK : bras (bras + avant-bras + poignet)
    /// ou jambe (cuisse + tibia + cheville).
    ///
    /// Ce composant ne décide de rien : on lui donne « l'extrémité va ici, orientée comme ça »,
    /// et il place la chaîne d'os. C'est ce qui permettra de remplacer les primitives par un
    /// vrai modèle 3D en réassignant simplement les trois transforms d'os.
    ///
    /// Convention : l'axe +Z local de chaque os pointe vers l'os suivant.
    /// L'objet portant ce composant EST l'articulation de base (épaule ou hanche).
    /// </summary>
    public class IkLimb : MonoBehaviour
    {
        [Header("Chaine d'os (+Z suit l'os)")]
        [SerializeField] private Transform _upper;
        [SerializeField] private Transform _lower;

        [SerializeField]
        [Tooltip("Extremite : poignet pour un bras, cheville pour une jambe.")]
        private Transform _end;

        [Header("Proportions (metres)")]
        [SerializeField, Min(0.02f)] private float _upperLength = 0.30f;
        [SerializeField, Min(0.02f)] private float _lowerLength = 0.26f;

        [Header("Orientation de l'articulation")]
        [SerializeField]
        [Tooltip("Direction vers laquelle le coude / genou s'ecarte, en espace local.")]
        private Vector3 _poleDirection = new Vector3(0f, -1f, -0.3f);

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos;

        private TwoBoneIkSolver.Result _lastSolve;

        public Transform End { get { return _end; } }
        public float TotalLength { get { return _upperLength + _lowerLength; } }
        public Vector3 RootPosition { get { return transform.position; } }

        /// <summary>Place la chaîne d'os pour que l'extrémité atteigne cette pose monde.</summary>
        public void ApplyWorldPose(Vector3 endPosition, Quaternion endRotation)
        {
            if (_upper == null || _lower == null || _end == null) return;

            Vector3 pole = transform.TransformDirection(_poleDirection);
            _lastSolve = TwoBoneIkSolver.Solve(transform.position, endPosition, _upperLength, _lowerLength, pole);

            // Le solveur suppose que le membre part exactement de l'articulation de base.
            _upper.localPosition = Vector3.zero;
            _upper.rotation = _lastSolve.UpperRotation;
            _lower.rotation = _lastSolve.LowerRotation;
            _end.rotation = endRotation;

            _lower.localPosition = new Vector3(0f, 0f, _upperLength);
            _end.localPosition = new Vector3(0f, 0f, _lowerLength);
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.02f);
            Gizmos.DrawLine(transform.position, _lastSolve.JointPosition);
            Gizmos.DrawWireSphere(_lastSolve.JointPosition, 0.015f);
            Gizmos.DrawLine(_lastSolve.JointPosition, _lastSolve.ReachedPosition);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.TransformDirection(_poleDirection.normalized) * 0.15f);
        }
    }
}
