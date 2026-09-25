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

        [Header("Charniere (corps skinne)")]
        [SerializeField]
        [Tooltip("Oriente les deux os autour de l'axe reel du coude / du genou, au lieu de la " +
                 "rotation la plus courte. Indispensable pour une peau : sans roulis controle, " +
                 "l'avant-bras vrille sur lui-meme et la peau du coude se tord.")]
        private bool _hingeMode;

        [SerializeField]
        [Tooltip("Os de torsion de l'avant-bras (optionnel). Il encaisse une part de la rotation " +
                 "du poignet, comme le radius qui tourne autour du cubitus.")]
        private Transform _twist;

        [SerializeField, Range(0f, 1f)] private float _twistShare = 0.5f;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos;

        private Quaternion _upperRestLocal;
        private Quaternion _lowerRestLocal;
        private Vector3 _upperAxisLocal;
        private Vector3 _lowerAxisLocal;
        private bool _bound;

        private TwoBoneIkSolver.Result _lastSolve;

        private Vector3 _upperHingeLocal;
        private Vector3 _lowerHingeLocal;
        private Quaternion _twistRestLocal;
        private Vector3 _endReferenceLocal;

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

            // L'axe de la charniere, mesure sur la pose de liaison (coude deja un peu plie).
            // Bras tendu en liaison : on se rabat sur le pole, avec le meme signe que le
            // solveur (voir HingeAxis).
            Vector3 hinge = Vector3.Cross(upperWorld, lowerWorld);
            if (hinge.sqrMagnitude < 1e-8f)
            {
                hinge = Vector3.Cross(_poleSpace.TransformDirection(_poleDirection), upperWorld + lowerWorld);
            }

            hinge.Normalize();
            _upperHingeLocal = _upper.InverseTransformDirection(hinge);
            _lowerHingeLocal = _lower.InverseTransformDirection(hinge);

            if (_twist != null) _twistRestLocal = Quaternion.Inverse(_lower.rotation) * _twist.rotation;

            // Reference de torsion : le cote de l'avant-bras, vu depuis la main de liaison.
            Vector3 side = Vector3.Cross(lowerWorld.normalized, hinge);
            _endReferenceLocal = Quaternion.Inverse(_end.rotation) * side;

            _bound = true;
        }

        /// <summary>Place la chaîne d'os pour que l'extrémité atteigne cette pose monde.</summary>
        public void ApplyWorldPose(Vector3 endPosition, Quaternion endRotation)
        {
            if (!_bound) CaptureBindPose();
            if (!_bound) return;

            ApplyWorldPose(endPosition, endRotation, _poleSpace.TransformDirection(_poleDirection));
        }

        /// <summary>
        /// Même chose avec un pôle donné en monde. Sert à corriger un membre ANIMÉ (animation
        /// capturée) : le pôle est alors la direction du coude de l'animation, pour que la
        /// correction déplace la main sans retourner le coude.
        /// </summary>
        public void ApplyWorldPose(Vector3 endPosition, Quaternion endRotation, Vector3 pole)
        {
            if (!_bound) CaptureBindPose();
            if (!_bound) return;

            _lastSolve = TwoBoneIkSolver.Solve(_upper.position, endPosition, _upperLength, _lowerLength, pole);

            Quaternion endWorld = endRotation * Quaternion.Euler(_endRotationOffset);

            if (_hingeMode)
            {
                Vector3 hinge = HingeAxis(_lastSolve.UpperDirection, _lastSolve.LowerDirection, pole,
                    endPosition - _upper.position);

                _upper.rotation = Frame(_lastSolve.UpperDirection, hinge) * Quaternion.Inverse(Frame(_upperAxisLocal, _upperHingeLocal));
                _lower.rotation = Frame(_lastSolve.LowerDirection, hinge) * Quaternion.Inverse(Frame(_lowerAxisLocal, _lowerHingeLocal));

                if (_twist != null)
                {
                    Vector3 axis = _lastSolve.LowerDirection;
                    Vector3 side = Vector3.Cross(axis, hinge);
                    Vector3 wanted = Vector3.ProjectOnPlane(endWorld * _endReferenceLocal, axis);
                    float angle = wanted.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(side, wanted, axis) : 0f;
                    _twist.rotation = Quaternion.AngleAxis(angle * _twistShare, axis) * (_lower.rotation * _twistRestLocal);
                }
            }
            else
            {
                AlignBone(_upper, _upperRestLocal, _upperAxisLocal, _lastSolve.UpperDirection);
                AlignBone(_lower, _lowerRestLocal, _lowerAxisLocal, _lastSolve.LowerDirection);
            }

            _end.rotation = endWorld;
        }

        /// <summary>
        /// Remet l'os de torsion d'après la rotation ACTUELLE de l'avant-bras et de la main, sans
        /// IK. Une animation capturée ne connaît pas cet os : sans ce recalage, toute la
        /// pronation passerait par le poignet et la peau de l'avant-bras vrillerait.
        /// </summary>
        public void UpdateTwist()
        {
            if (!_bound || !_hingeMode || _twist == null) return;

            Vector3 axis = _lower.TransformDirection(_lowerAxisLocal);
            Vector3 hinge = _lower.TransformDirection(_lowerHingeLocal);
            Vector3 side = Vector3.Cross(axis, hinge);
            Vector3 wanted = Vector3.ProjectOnPlane(_end.rotation * _endReferenceLocal, axis);
            float angle = wanted.sqrMagnitude > 1e-8f ? Vector3.SignedAngle(side, wanted, axis) : 0f;
            _twist.rotation = Quaternion.AngleAxis(angle * _twistShare, axis) * (_lower.rotation * _twistRestLocal);
        }

        /// <summary>
        /// L'axe du coude : perpendiculaire au plan bras / avant-bras. Membre presque tendu, ce
        /// plan n'existe plus — on prend celui du pole, avec le meme sens (un coude plie vers le
        /// pole donne exactement Cross(pole, direction)).
        /// </summary>
        private static Vector3 HingeAxis(Vector3 upperDirection, Vector3 lowerDirection, Vector3 pole, Vector3 reach)
        {
            Vector3 hinge = Vector3.Cross(upperDirection, lowerDirection);
            Vector3 fallback = Vector3.Cross(pole, reach);

            if (fallback.sqrMagnitude < 1e-8f) fallback = Vector3.Cross(Vector3.up, reach);
            fallback.Normalize();

            // Fondu entre les deux quand le coude s'ouvre : pas de saut de roulis au moment ou
            // le bras se tend.
            float bend = hinge.magnitude;
            if (bend < 1e-5f) return fallback;

            hinge /= bend;
            if (Vector3.Dot(hinge, fallback) < 0f && bend < 0.2f) hinge = -hinge;

            return Vector3.Slerp(fallback, hinge, Mathf.Clamp01(bend / 0.2f)).normalized;
        }

        /// <summary>Rotation qui envoie +Z sur <paramref name="z"/> et +X sur <paramref name="x"/> (orthogonalise).</summary>
        private static Quaternion Frame(Vector3 z, Vector3 x)
        {
            Vector3 up = Vector3.Cross(z, x);
            if (up.sqrMagnitude < 1e-10f) return Quaternion.LookRotation(z);
            return Quaternion.LookRotation(z, up);
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
