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

            [NonSerialized] public Quaternion ProximalRest;
            [NonSerialized] public Quaternion MiddleRest;
            [NonSerialized] public Quaternion DistalRest;
        }

        [SerializeField] private HandSide _side = HandSide.Right;
        [SerializeField] private Transform _palm;
        [SerializeField] private Finger[] _fingers = new Finger[0];

        [Header("Reactivite")]
        [SerializeField, Min(0.5f)]
        [Tooltip("Vitesse de fermeture / ouverture de la main.")]
        private float _gripResponse = 14f;

        private float _grip = 1f;
        private float _targetGrip = 1f;
        private bool _restCaptured;

        public HandSide Side { get { return _side; } }
        public Transform Palm { get { return _palm; } }

        /// <summary>Position du point d'impact : le devant des articulations, pas le poignet.</summary>
        public Vector3 KnucklePosition
        {
            get { return _palm != null ? _palm.TransformPoint(new Vector3(0f, 0f, 0.085f)) : transform.position; }
        }

        /// <summary>0 = main ouverte, 1 = poing serré. Réglé par l'animation de combat.</summary>
        public float TargetGrip
        {
            get { return _targetGrip; }
            set { _targetGrip = Mathf.Clamp01(value); }
        }

        /// <summary>Fermeture réellement appliquée (lissée). Sert à valider un impact poing fermé.</summary>
        public float CurrentGrip { get { return _grip; } }

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
                ApplyFinger(_fingers[i]);
            }
        }

        private void ApplyFinger(Finger finger)
        {
            if (finger == null) return;

            // Le retard donne une fermeture en cascade, de l'auriculaire vers l'index :
            // c'est ce qui distingue une vraie main d'une main qui se ferme d'un bloc.
            float delayed = finger.closeDelay >= 0.999f
                ? _grip
                : Mathf.Clamp01((_grip - finger.closeDelay) / (1f - finger.closeDelay));

            float amount = delayed * finger.curlScale;
            Vector3 axis = finger.curlAxis.sqrMagnitude > 1e-6f ? finger.curlAxis.normalized : Vector3.right;

            if (finger.proximal != null)
            {
                finger.proximal.localRotation = finger.ProximalRest * Quaternion.AngleAxis(finger.proximalCurl * amount, axis);
            }

            if (finger.middle != null)
            {
                finger.middle.localRotation = finger.MiddleRest * Quaternion.AngleAxis(finger.middleCurl * amount, axis);
            }

            if (finger.distal != null)
            {
                finger.distal.localRotation = finger.DistalRest * Quaternion.AngleAxis(finger.distalCurl * amount, axis);
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
