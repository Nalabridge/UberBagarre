using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Une personne du groupe devant le club.
    ///
    /// Elle sert à une seule chose, et cette chose est tout le sel de la scène : **rendre la
    /// cible difficile à trouver.** « Trouve Bruno Moretti » n'est une consigne que s'il y a
    /// quelqu'un d'autre à confondre avec lui. Un ennemi seul au milieu de la rue rendrait la
    /// fiche de signalement — veste rouge, crâne rasé — parfaitement inutile, et avec elle
    /// toute la scène d'identification.
    ///
    /// Elle bouge, et c'est indispensable. Une silhouette parfaitement immobile se lit comme un
    /// mannequin, et un mannequin ne trompe personne : le joueur repérerait la cible au simple
    /// fait qu'elle est la seule à respirer. Le mouvement ici n'est pas de la décoration, c'est
    /// ce qui rend le décalage invisible.
    /// </summary>
    public class CrowdMember : MonoBehaviour
    {
        [Header("Identite")]
        [SerializeField] private string _displayName = "Inconnu";

        [SerializeField]
        [Tooltip("Cette personne est-elle la cible de la mission ? Une seule doit l'etre.")]
        private bool _isTarget;

        [SerializeField]
        [Tooltip("Ce que le joueur lit en la detaillant : de quoi comparer avec le signalement.")]
        private string _description = "Rien a signaler.";

        [Header("Vie")]
        [SerializeField]
        [Tooltip("Noeud qui se balance. En pratique le bassin ou le buste.")]
        private Transform _swayRoot;

        [SerializeField, Min(0f)] private float _swayAngle = 2.4f;
        [SerializeField, Min(0.05f)] private float _swaySpeed = 0.7f;

        [SerializeField]
        [Tooltip("Tourne la tete vers le joueur quand il approche. C'est le detail qui fait " +
                 "passer un groupe de figurants pour des gens.")]
        private Transform _head;

        [SerializeField] private Transform _lookTarget;
        [SerializeField, Min(0.5f)] private float _noticeDistance = 7f;
        [SerializeField, Range(0f, 80f)] private float _maxHeadYaw = 55f;

        private Quaternion _swayRest;
        private Quaternion _headRest;
        private float _seed;
        private float _headBlend;

        public string DisplayName { get { return _displayName; } }
        public bool IsTarget { get { return _isTarget; } }
        public string Description { get { return _description; } }

        /// <summary>Point visé par l'identification : la tête si elle existe, sinon le buste.</summary>
        public Vector3 LabelPoint
        {
            get
            {
                if (_head != null) return _head.position + Vector3.up * 0.18f;
                return transform.position + Vector3.up * 1.85f;
            }
        }

        private void Awake()
        {
            if (_swayRoot != null) _swayRest = _swayRoot.localRotation;
            if (_head != null) _headRest = _head.localRotation;

            _seed = Mathf.Abs(transform.position.x * 4.7f + transform.position.z * 2.3f) % 53f;
        }

        private void LateUpdate()
        {
            Sway();
            LookAtPlayer();
        }

        private void Sway()
        {
            if (_swayRoot == null) return;

            float t = Time.time * _swaySpeed + _seed;

            // Deux fréquences sans rapport simple : un balancement sur une seule sinusoïde
            // revient exactement au même point à intervalle régulier, et l'œil le remarque.
            float pitch = Mathf.Sin(t) * _swayAngle;
            float roll = Mathf.Sin(t * 0.61f + 1.7f) * _swayAngle * 0.7f;
            float yaw = Mathf.Sin(t * 0.37f + 3.1f) * _swayAngle * 1.3f;

            _swayRoot.localRotation = _swayRest * Quaternion.Euler(pitch, yaw, roll);
        }

        private void LookAtPlayer()
        {
            if (_head == null || _lookTarget == null) return;

            Vector3 toTarget = _lookTarget.position - _head.position;
            if (toTarget.sqrMagnitude < 0.0004f) return;

            // Tout le calcul se fait dans le repère du PARENT de la tête. C'est ce qui rend
            // la limite du cou exprimable simplement : un angle mesuré dans le monde devrait
            // être corrigé de l'orientation du corps à chaque image, et une erreur de signe y
            // passe inaperçue jusqu'au jour où un figurant se retourne la tête à 180 degrés.
            Transform parent = _head.parent;
            Vector3 local = parent != null
                ? parent.InverseTransformDirection(toTarget.normalized)
                : toTarget.normalized;

            float yaw = Mathf.Atan2(local.x, local.z) * Mathf.Rad2Deg;
            float pitch = -Mathf.Asin(Mathf.Clamp(local.y, -1f, 1f)) * Mathf.Rad2Deg;

            bool reachable = Mathf.Abs(yaw) <= _maxHeadYaw;
            bool close = toTarget.magnitude < _noticeDistance;

            _headBlend = Mathf.MoveTowards(_headBlend, reachable && close ? 1f : 0f,
                Time.deltaTime * 2.2f);

            if (_headBlend <= 0.001f)
            {
                _head.localRotation = _headRest;
                return;
            }

            yaw = Mathf.Clamp(yaw, -_maxHeadYaw, _maxHeadYaw);
            pitch = Mathf.Clamp(pitch, -18f, 22f);

            _head.localRotation = Quaternion.Slerp(_headRest,
                Quaternion.Euler(pitch, yaw, 0f), _headBlend);
        }
    }
}
