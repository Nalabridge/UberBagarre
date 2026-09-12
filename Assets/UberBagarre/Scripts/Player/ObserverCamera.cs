using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Caméra d'observation orbitale (F3), qui tourne autour du joueur.
    ///
    /// Elle existe parce qu'un jeu en première personne a un angle mort énorme : **on ne voit
    /// jamais son propre personnage.** Tout le travail d'animation, de matière, de chute et de
    /// marques de coup porte sur un corps que le joueur ne regarde jamais — et quand il dit
    /// « les animations sont horribles », ni lui ni moi ne pouvons savoir de quoi on parle.
    ///
    /// Le point important : le gameplay CONTINUE. On peut marcher, courir, frapper, se faire
    /// toucher et tomber pendant qu'on orbite. C'est donc un vrai outil de jugement, pas une
    /// caméra libre décorative — la seule façon de voir ce qu'on est en train de régler.
    ///
    /// La souris pilote l'orbite au lieu de la visée tant que le mode est actif, sinon tourner la
    /// caméra ferait pivoter le personnage en même temps.
    /// </summary>
    public class ObserverCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerLook _look;

        [SerializeField]
        [Tooltip("La camera premiere personne. Elle est eteinte pendant l'observation.")]
        private Camera _gameCamera;

        [SerializeField]
        [Tooltip("La camera d'observation. Elle doit porter le tag MainCamera, comme l'autre, " +
                 "pour que Camera.main suive le point de vue reellement allume.")]
        private Camera _observerCamera;

        [SerializeField]
        [Tooltip("Ce autour de quoi on tourne. En general la tete du joueur.")]
        private Transform _target;

        [Header("Orbite")]
        [SerializeField, Min(0.5f)] private float _distance = 3.2f;
        [SerializeField] private Vector2 _distanceRange = new Vector2(1.2f, 12f);
        [SerializeField, Min(0.1f)] private float _zoomSpeed = 6f;
        [SerializeField, Min(0.01f)] private float _sensitivity = 0.18f;
        [SerializeField] private Vector2 _pitchRange = new Vector2(-80f, 80f);
        [SerializeField] private Vector3 _targetOffset = new Vector3(0f, -0.25f, 0f);

        [SerializeField, Min(0f)]
        [Tooltip("Lissage du suivi. Une camera qui collerait au joueur rendrait la marche illisible, " +
                 "puisque c'est justement le mouvement du corps qu'on veut observer.")]
        private float _followSmoothing = 9f;

        [Header("Depart")]
        [SerializeField] private float _startYaw = 150f;
        [SerializeField] private float _startPitch = 12f;

        private float _yaw;
        private float _pitch;
        private Vector3 _smoothedCentre;
        private bool _active;

        public bool IsActive { get { return _active; } }

        private void Awake()
        {
            _yaw = _startYaw;
            _pitch = _startPitch;

            if (_target != null) _smoothedCentre = _target.position;
            if (_observerCamera != null) _observerCamera.enabled = false;
        }

        private void Update()
        {
            if (_input != null && _input.ToggleObserverPressed) SetActive(!_active);
            if (!_active) return;

            float dt = Time.unscaledDeltaTime;

            if (_input != null)
            {
                Vector2 delta = _input.LookDelta;
                _yaw += delta.x * _sensitivity;
                _pitch = Mathf.Clamp(_pitch - delta.y * _sensitivity, _pitchRange.x, _pitchRange.y);

                if (_input.ObserverZoomInHeld) _distance -= _zoomSpeed * dt;
                if (_input.ObserverZoomOutHeld) _distance += _zoomSpeed * dt;

                _distance = Mathf.Clamp(_distance, _distanceRange.x, _distanceRange.y);
            }

            ApplyPose(dt);
        }

        private void ApplyPose(float dt)
        {
            if (_observerCamera == null || _target == null) return;

            Vector3 centre = _target.position + _targetOffset;

            _smoothedCentre = _followSmoothing <= 0f
                ? centre
                : Vector3.Lerp(_smoothedCentre, centre, 1f - Mathf.Exp(-_followSmoothing * dt));

            Quaternion rotation = Quaternion.Euler(_pitch, _yaw, 0f);
            _observerCamera.transform.position = _smoothedCentre + rotation * Vector3.back * _distance;
            _observerCamera.transform.rotation = rotation;
        }

        public void SetActive(bool active)
        {
            _active = active;

            if (_observerCamera != null) _observerCamera.enabled = active;
            if (_gameCamera != null) _gameCamera.enabled = !active;

            // La souris sert a l'orbite, donc plus a la visee : sans ca le personnage pivoterait
            // en meme temps que la camera et on n'observerait jamais le meme cote du corps.
            if (_look != null) _look.LookEnabled = !active;

            if (active) ApplyPose(1f);
        }

        private void OnDisable()
        {
            if (_active) SetActive(false);
        }
    }
}
