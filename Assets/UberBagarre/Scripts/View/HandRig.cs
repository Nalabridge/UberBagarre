using System;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Une main articulée : paume + 5 doigts de 3 phalanges.
    ///
    /// Le poing n'est pas un cube : c'est le résultat de la fermeture réelle des doigts.
    /// Conséquence utile pour la suite : au moment de l'impact d'un coup, on pourra
    /// vérifier que la main est bien fermée, ouvrir la main pour une parade, ou tendre
    /// l'index pour une provocation, sans rien changer ailleurs.
    ///
    /// Convention de la main : +Z vers les doigts, +Y vers le dos de la main,
    /// donc fermer un doigt = tourner autour de +X (le +Z part vers -Y, côté paume).
    /// </summary>
    public class HandRig : MonoBehaviour
    {
        [Serializable]
        public class Finger
        {
            public string name = "Doigt";

            [Tooltip("Phalange a la base, enfant de la paume.")]
            public Transform proximal;

            [Tooltip("Phalange intermediaire.")]
            public Transform middle;

            [Tooltip("Derniere phalange.")]
            public Transform distal;

            [Header("Fermeture (degres au poing serre)")]
            public float proximalCurl = 80f;
            public float middleCurl = 95f;
            public float distalCurl = 65f;

            [Tooltip("Axe de flexion, en espace local de la phalange.")]
            public Vector3 curlAxis = Vector3.right;

            [Range(0f, 1f)]
            [Tooltip("Retard de fermeture. Les doigts ne se ferment pas tous en meme temps.")]
            public float closeDelay;

            [Tooltip("Multiplicateur de fermeture propre au doigt. L'auriculaire se ferme plus, l'index moins.")]
            public float curlScale = 1f;

            [Tooltip("Rotation supplementaire de la base au poing serre (degres, Euler local). Les " +
                     "doigts se resserrent en se fermant ; le pouce, lui, pivote pour venir se " +
                     "poser en travers de l'index et du majeur au lieu de rentrer dans la paume.")]
            public Vector3 fistRotation;

            [NonSerialized] public Quaternion ProximalRest;
            [NonSerialized] public Quaternion MiddleRest;
            [NonSerialized] public Quaternion DistalRest;
        }

        [SerializeField] private HandSide _side = HandSide.Right;
        [SerializeField] private Transform _palm;

        [SerializeField]
        [Tooltip("Position des articulations dans le repere de la paume. Mesuree automatiquement " +
                 "lors du branchement d'un modele 3D, car chaque main a ses propres proportions.")]
        private Vector3 _knuckleOffset = new Vector3(0f, -0.008f, 0.052f);
        [SerializeField] private Finger[] _fingers = new Finger[0];

        [Header("Reactivite")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Vitesse de fermeture / ouverture de la main.")]
        private float _gripResponse = 14f;

        private float _grip = 1f;
        private float _targetGrip = 1f;
        private bool _restCaptured;

        private Vector3[] _poseCurls;
        private Vector3 _thumbRotation;
        private float _poseWeight;

        public HandSide Side { get { return _side; } }
        public Transform Palm { get { return _palm; } }

        /// <summary>Position du point d'impact : le devant des articulations, pas le poignet.</summary>
        public Vector3 KnucklePosition
        {
            get { return _palm != null ? _palm.TransformPoint(_knuckleOffset) : transform.position; }
        }

        /// <summary>0 = main ouverte, 1 = poing serré. Réglé par l'animation de combat.</summary>
        public float TargetGrip
        {
            get { return _targetGrip; }
            set { _targetGrip = Mathf.Clamp01(value); }
        }

        /// <summary>Fermeture réellement appliquée (lissée). Sert à valider un impact poing fermé.</summary>
        public float CurrentGrip { get { return _grip; } }

        /// <summary>
        /// Une prise précise, doigt par doigt, au lieu d'une fermeture globale : tenir un
        /// téléphone n'est pas « un poing à moitié fermé ». Les quatre doigts s'enroulent autour
        /// du bord, le pouce s'écarte et se pose sur l'écran — des courbures que le seul curseur
        /// de fermeture ne peut pas produire. <paramref name="curls"/> : (base, milieu, bout) en
        /// degrés, dans l'ordre des doigts de la main ; <paramref name="thumbRotation"/> :
        /// rotation supplémentaire de la base du pouce. Poids 0 = la fermeture normale reprend.
        /// </summary>
        public void SetPoseOverride(float weight, Vector3[] curls, Vector3 thumbRotation)
        {
            _poseWeight = Mathf.Clamp01(weight);
            if (curls != null) _poseCurls = curls;
            _thumbRotation = thumbRotation;
        }

        private void Awake()
        {
            CaptureRestPose();
            _grip = _targetGrip;
        }

        private void CaptureRestPose()
        {
            if (_restCaptured) return;

            for (int i = 0; i < _fingers.Length; i++)
            {
                Finger finger = _fingers[i];
                if (finger == null) continue;

                if (finger.proximal != null) finger.ProximalRest = finger.proximal.localRotation;
                if (finger.middle != null) finger.MiddleRest = finger.middle.localRotation;
                if (finger.distal != null) finger.DistalRest = finger.distal.localRotation;
            }

            _restCaptured = true;
        }

        private void LateUpdate()
        {
            CaptureRestPose();

            float t = 1f - Mathf.Exp(-_gripResponse * Time.deltaTime);
            _grip = Mathf.Lerp(_grip, _targetGrip, t);

            for (int i = 0; i < _fingers.Length; i++)
            {
                ApplyFinger(_fingers[i], i);
            }
        }

        private static bool IsThumb(Finger finger, int index)
        {
            string name = finger.name ?? string.Empty;
            return name.Contains("Pouce") || name.Contains("Thumb") || name.Contains("thumb") || index == 4;
        }

        private void ApplyFinger(Finger finger, int index)
        {
            if (finger == null) return;

            // Le retard donne une fermeture en cascade, de l'auriculaire vers l'index :
            // c'est ce qui distingue une vraie main d'une main qui se ferme d'un bloc.
            float delayed = finger.closeDelay >= 0.999f
                ? _grip
                : Mathf.Clamp01((_grip - finger.closeDelay) / (1f - finger.closeDelay));

            float amount = delayed * finger.curlScale;
            Vector3 axis = finger.curlAxis.sqrMagnitude > 1e-6f ? finger.curlAxis.normalized : Vector3.right;

            float proximal = finger.proximalCurl * amount;
            float middle = finger.middleCurl * amount;
            float distal = finger.distalCurl * amount;
            Quaternion spread = Quaternion.identity;

            if (_poseWeight > 0.001f && _poseCurls != null && index < _poseCurls.Length)
            {
                Vector3 pose = _poseCurls[index];
                proximal = Mathf.Lerp(proximal, pose.x, _poseWeight);
                middle = Mathf.Lerp(middle, pose.y, _poseWeight);
                distal = Mathf.Lerp(distal, pose.z, _poseWeight);

                if (IsThumb(finger, index))
                {
                    spread = Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(_thumbRotation), _poseWeight);
                }
            }

            if (finger.proximal != null)
            {
                Quaternion fist = finger.fistRotation.sqrMagnitude > 1e-6f
                    ? Quaternion.Slerp(Quaternion.identity, Quaternion.Euler(finger.fistRotation), Mathf.Clamp01(amount) * (1f - _poseWeight))
                    : Quaternion.identity;

                finger.proximal.localRotation = finger.ProximalRest * fist * spread * Quaternion.AngleAxis(proximal, axis);
            }

            if (finger.middle != null)
            {
                finger.middle.localRotation = finger.MiddleRest * Quaternion.AngleAxis(middle, axis);
            }

            if (finger.distal != null)
            {
                finger.distal.localRotation = finger.DistalRest * Quaternion.AngleAxis(distal, axis);
            }
        }

        private void OnDrawGizmosSelected()
        {
            if (_palm == null) return;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(KnucklePosition, 0.03f);
        }
    }
}
