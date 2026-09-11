using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Un bras première personne : épaule → bras → avant-bras → poing.
    ///
    /// Ce composant ne décide de rien. Il reçoit une position/rotation de poing en monde,
    /// et place la chaîne d'os pour l'atteindre. C'est volontaire : remplacer les primitives
    /// par un vrai modèle 3D plus tard consistera uniquement à réassigner les trois
    /// transforms d'os ci-dessous — aucun autre script du jeu ne changera.
    ///
    /// Convention d'os : l'axe +Z local de chaque os pointe vers l'os suivant.
    /// </summary>
    public class FirstPersonArm : MonoBehaviour
    {
        [Header("Identite")]
        [SerializeField] private HandSide _side = HandSide.Left;

        [Header("Chaine d'os (+Z suit l'os)")]
        [SerializeField]
        [Tooltip("Premier os. Doit etre enfant de cet objet (qui sert d'ancrage d'epaule).")]
        private Transform _upperArm;

        [SerializeField]
        [Tooltip("Deuxieme os, enfant du bras, place a (0, 0, Upper Length).")]
        private Transform _forearm;

        [SerializeField]
        [Tooltip("Le poing, enfant de l'avant-bras, place a (0, 0, Forearm Length).")]
        private Transform _fist;

        [Header("Proportions (metres)")]
        [SerializeField, Min(0.02f)] private float _upperLength = 0.28f;
        [SerializeField, Min(0.02f)] private float _forearmLength = 0.26f;

        [Header("Orientation du coude")]
        [SerializeField]
        [Tooltip("Direction vers laquelle le coude s'ecarte, en espace local. Bas + exterieur = garde de boxe.")]
        private Vector3 _poleDirection = new Vector3(-0.35f, -1f, -0.25f);

        [Header("Debug")]
        [SerializeField] private bool _drawGizmos;

        private ArmIkSolver.Result _lastSolve;

        public HandSide Side { get { return _side; } }
        public Transform Fist { get { return _fist; } }

        /// <summary>Longueur totale du bras tendu. Sert à borner la portée des attaques.</summary>
        public float TotalLength { get { return _upperLength + _forearmLength; } }

        /// <summary>Place la chaîne d'os pour que le poing atteigne cette position/rotation monde.</summary>
        public void ApplyWorldPose(Vector3 fistPosition, Quaternion fistRotation)
        {
            if (_upperArm == null || _forearm == null || _fist == null) return;

            Vector3 pole = transform.TransformDirection(_poleDirection);
            _lastSolve = ArmIkSolver.Solve(transform.position, fistPosition, _upperLength, _forearmLength, pole);

            // Le solveur suppose que le bras part exactement de l'ancrage d'epaule.
            _upperArm.localPosition = Vector3.zero;
            _upperArm.rotation = _lastSolve.UpperRotation;
            _forearm.rotation = _lastSolve.ForearmRotation;
            _fist.rotation = fistRotation;

            // Les positions des os découlent de la hiérarchie : on garantit seulement que
            // les décalages locaux correspondent aux longueurs configurées, pour que le
            // poing atterrisse exactement au bout de la chaîne.
            _forearm.localPosition = new Vector3(0f, 0f, _upperLength);
            _fist.localPosition = new Vector3(0f, 0f, _forearmLength);
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmos || _upperArm == null) return;

            Gizmos.color = Color.cyan;
            Gizmos.DrawWireSphere(transform.position, 0.02f);
            Gizmos.DrawLine(transform.position, _lastSolve.ElbowPosition);
            Gizmos.DrawWireSphere(_lastSolve.ElbowPosition, 0.015f);
            Gizmos.DrawLine(_lastSolve.ElbowPosition, _lastSolve.ReachedPosition);

            Gizmos.color = Color.yellow;
            Gizmos.DrawRay(transform.position, transform.TransformDirection(_poleDirection.normalized) * 0.15f);
        }
    }
}
