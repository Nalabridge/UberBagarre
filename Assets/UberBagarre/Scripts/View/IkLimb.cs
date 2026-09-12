using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Un membre à deux os piloté par IK : bras (bras + avant-bras + poignet)
    /// ou jambe (cuisse + tibia + cheville).
    ///
    /// Ce composant est AGNOSTIQUE AU SQUELETTE. Il ne suppose aucune convention d'orientation
    /// d'os : au réveil, il mesure lui-même dans quelle direction locale chaque os pointe, et
    /// mémorise sa pose de repos. C'est ce qui permet de brancher n'importe quel modèle 3D
    /// importé (Mixamo, asset store, modèle maison) sans retoucher une ligne de code.
    ///
    /// Sans cette mesure, il faudrait que chaque os du modèle ait son axe +Z aligné sur l'os,
    /// ce qu'aucun rig du commerce ne fait.
    /// </summary>
    public class IkLimb : MonoBehaviour
    {
        [Header("Chaine d'os")]
        [SerializeField] private Transform _upper;
        [SerializeField] private Transform _lower;

        [SerializeField]
        [Tooltip("Extremite : poignet pour un bras, cheville pour une jambe.")]
        private Transform _end;

        [Header("Proportions (metres)")]
        [SerializeField]
        [Tooltip("Mesure les longueurs sur le squelette reel au demarrage. A laisser coche.")]
        private bool _autoMeasureLengths = true;

        [SerializeField, Min(0.02f)] private float _upperLength = 0.30f;
        [SerializeField, Min(0.02f)] private float _lowerLength = 0.26f;

        [Header("Orientation de l'articulation")]
        [SerializeField]
        [Tooltip("Repere dans lequel la direction du coude / genou est exprimee. En general la racine du corps.")]
        private Transform _poleSpace;

        [SerializeField]
        [Tooltip("Direction vers laquelle le coude / genou s'ecarte.")]
        private Vector3 _poleDirection = new Vector3(0f, -1f, -0.3f);

        [Header("Extremite")]
        [SerializeField]
        [Tooltip("Correction d'orientation de la main / du pied, pour un modele dont les axes different.")]
        private Vector3 _endRotationOffset;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos;

        private Quaternion _upperRestLocal;
        private Quaternion _lowerRestLocal;
        private Vector3 _upperAxisLocal;
        private Vector3 _lowerAxisLocal;
        private bool _bound;

        private TwoBoneIkSolver.Result _lastSolve;

        public Transform End { get { return _end; } }
        public Transform Upper { get { return _upper; } }
        public Transform Lower { get { return _lower; } }
        public float TotalLength { get { return _upperLength + _lowerLength; } }

        /// <summary>Position de l'articulation de base (épaule ou hanche).</summary>
        public Vector3 RootPosition
        {
            get { return _upper != null ? _upper.position : transform.position; }
        }

        private void Awake()
        {
            CaptureBindPose();
        }

        /// <summary>
        /// Mémorise la pose de repos et mesure l'orientation réelle des os.
        /// Appelé automatiquement au réveil, et par l'outil de branchement de modèle.
        /// </summary>
        public void CaptureBindPose()
        {
            if (_upper == null || _lower == null || _end == null) return;

            _upperRestLocal = _upper.localRotation;
            _lowerRestLocal = _lower.localRotation;

            Vector3 upperWorld = _lower.position - _upper.position;
            Vector3 lowerWorld = _end.position - _lower.position;

            if (upperWorld.sqrMagnitude < 1e-8f || lowerWorld.sqrMagnitude < 1e-8f)
            {
                Debug.LogWarning("[UberBagarre] IkLimb sur " + name + " : os de longueur nulle, membre ignore.", this);
                return;
            }

            _upperAxisLocal = _upper.InverseTransformDirection(upperWorld.normalized);
            _lowerAxisLocal = _lower.InverseTransformDirection(lowerWorld.normalized);

            if (_autoMeasureLengths)
            {
                _upperLength = upperWorld.magnitude;
                _lowerLength = lowerWorld.magnitude;
            }

            if (_poleSpace == null) _poleSpace = transform;

            _bound = true;
        }

        /// <summary>Place la chaîne d'os pour que l'extrémité atteigne cette pose monde.</summary>
        public void ApplyWorldPose(Vector3 endPosition, Quaternion endRotation)
        {
            if (!_bound) CaptureBindPose();
            if (!_bound) return;

            Vector3 pole = _poleSpace.TransformDirection(_poleDirection);
            _lastSolve = TwoBoneIkSolver.Solve(_upper.position, endPosition, _upperLength, _lowerLength, pole);

            AlignBone(_upper, _upperRestLocal, _upperAxisLocal, _lastSolve.UpperDirection);
            AlignBone(_lower, _lowerRestLocal, _lowerAxisLocal, _lastSolve.LowerDirection);

            _end.rotation = endRotation * Quaternion.Euler(_endRotationOffset);
        }

        /// <summary>
        /// Oriente un os vers une direction, quelle que soit son orientation d'origine.
        ///
        /// On repart systématiquement de la pose de repos avant de corriger : sinon l'erreur
        /// de rotation s'accumulerait d'une frame à l'autre et le membre finirait par vriller.
        /// </summary>
        private static void AlignBone(Transform bone, Quaternion restLocal, Vector3 axisLocal, Vector3 targetDirection)
        {
            bone.localRotation = restLocal;

            Vector3 current = bone.TransformDirection(axisLocal);
            if (current.sqrMagnitude < 1e-8f || targetDirection.sqrMagnitude < 1e-8f) return;

            bone.rotation = Quaternion.FromToRotation(current, targetDirection) * bone.rotation;
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _upper == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(RootPosition, 0.02f);
            Gizmos.DrawLine(RootPosition, _lastSolve.JointPosition);
            Gizmos.DrawWireSphere(_lastSolve.JointPosition, 0.015f);
            Gizmos.DrawLine(_lastSolve.JointPosition, _lastSolve.ReachedPosition);

            Gizmos.color = Color.yellow;
            Transform space = _poleSpace != null ? _poleSpace : transform;
            Gizmos.DrawRay(RootPosition, space.TransformDirection(_poleDirection.normalized) * 0.15f);
        }
    }
}
