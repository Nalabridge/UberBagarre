using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Le squelette du combattant, au même endroit.
    ///
    /// Ce composant ne fait rien : il référence les os et mémorise leur pose de repos.
    /// Tout le reste (locomotion, combat, et plus tard les hurtbox par zone du corps)
    /// passe par lui, ce qui évite que chaque système aille chercher ses os par son
    /// propre chemin et casse dès qu'on renomme un objet.
    /// </summary>
    public class BodyRig : MonoBehaviour
    {
        [Header("Colonne")]
        [SerializeField] private Transform _pelvis;
        [SerializeField] private Transform _spine;
        [SerializeField] private Transform _chest;
        [SerializeField] private Transform _neck;
        [SerializeField] private Transform _head;

        [Header("Epaules")]
        [SerializeField]
        [Tooltip("Clavicules : elles avancent l'epaule dans un direct, la haussent dans une garde.")]
        private Transform _leftClavicle;

        [SerializeField] private Transform _rightClavicle;

        [Header("Jambes")]
        [SerializeField] private IkLimb _leftLeg;
        [SerializeField] private IkLimb _rightLeg;

        [Header("Bras")]
        [SerializeField] private IkLimb _leftArm;
        [SerializeField] private IkLimb _rightArm;
        [SerializeField] private HandRig _leftHand;
        [SerializeField] private HandRig _rightHand;

        public Transform Pelvis { get { return _pelvis; } }
        public Transform Spine { get { return _spine; } }
        public Transform Chest { get { return _chest; } }
        public Transform Neck { get { return _neck; } }
        public Transform Head { get { return _head; } }
        public Transform Clavicle(HandSide side) { return side == HandSide.Left ? _leftClavicle : _rightClavicle; }

        public IkLimb LeftLeg { get { return _leftLeg; } }
        public IkLimb RightLeg { get { return _rightLeg; } }
        public IkLimb LeftArm { get { return _leftArm; } }
        public IkLimb RightArm { get { return _rightArm; } }
        public HandRig LeftHand { get { return _leftHand; } }
        public HandRig RightHand { get { return _rightHand; } }

        public IkLimb Arm(HandSide side) { return side == HandSide.Left ? _leftArm : _rightArm; }
        public HandRig Hand(HandSide side) { return side == HandSide.Left ? _leftHand : _rightHand; }
        public IkLimb Leg(bool left) { return left ? _leftLeg : _rightLeg; }

        public Vector3 PelvisRestPosition { get; private set; }
        public Quaternion PelvisRestRotation { get; private set; }
        public Quaternion SpineRestRotation { get; private set; }
        public Quaternion ChestRestRotation { get; private set; }
        public Quaternion NeckRestRotation { get; private set; }
        public Quaternion HeadRestRotation { get; private set; }
        public Quaternion LeftClavicleRestRotation { get; private set; }
        public Quaternion RightClavicleRestRotation { get; private set; }

        public Quaternion ClavicleRestRotation(HandSide side)
        {
            return side == HandSide.Left ? LeftClavicleRestRotation : RightClavicleRestRotation;
        }

        private void Awake()
        {
            CaptureRestPose();
        }

        public void CaptureRestPose()
        {
            if (_pelvis != null)
            {
                PelvisRestPosition = _pelvis.localPosition;
                PelvisRestRotation = _pelvis.localRotation;
            }

            if (_spine != null) SpineRestRotation = _spine.localRotation;
            if (_chest != null) ChestRestRotation = _chest.localRotation;
            if (_neck != null) NeckRestRotation = _neck.localRotation;
            if (_head != null) HeadRestRotation = _head.localRotation;
            if (_leftClavicle != null) LeftClavicleRestRotation = _leftClavicle.localRotation;
            if (_rightClavicle != null) RightClavicleRestRotation = _rightClavicle.localRotation;
        }
    }
}
