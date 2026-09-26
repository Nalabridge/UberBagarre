using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Core;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le joueur au volant. E sur une portière : il monte, la caméra passe derrière la voiture ;
    /// E à l'arrêt (ou presque) : il descend, côté conducteur si c'est libre.
    ///
    /// Au volant, le corps du joueur n'existe plus pour le jeu : pas de déplacement, pas de
    /// visée, pas de coups, pas de téléphone ni d'interactions — ses composants sont coupés et
    /// son corps caché, rangé sur le siège. On les rend tels qu'on les a trouvés.
    ///
    /// Commandes : avancer/reculer = gaz et frein puis marche arrière, gauche/droite = volant,
    /// saut = frein à main, direct (clic gauche) = klaxon, la souris tourne la caméra (elle
    /// revient d'elle-même derrière la voiture).
    /// </summary>
    public class PlayerDriving : MonoBehaviour, ISpawnReceiver
    {
        [Header("Joueur")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private CharacterController _controller;
        [SerializeField] private Combatant _combatant;
        [SerializeField] private KnockdownSystem _knockdown;

        [SerializeField]
        [Tooltip("Coupes pendant la conduite (deplacement, visee, coups, balancement de tete...).")]
        private Behaviour[] _pauseWhileDriving = new Behaviour[0];

        [SerializeField]
        [Tooltip("Racine du corps visible du joueur : cache au volant.")]
        private Transform _visuals;

        [SerializeField] private InteractionSystem _interaction;
        [SerializeField] private PhoneDevice _phone;

        [Header("Cameras")]
        [SerializeField] private Camera _gameCamera;
        [SerializeField] private Camera _chaseCamera;

        [SerializeField, Min(2f)] private float _distance = 6.4f;
        [SerializeField, Min(0.5f)] private float _height = 1.55f;
        [SerializeField] private float _pitch = 9f;
        [SerializeField] private Vector2 _fieldOfView = new Vector2(62f, 76f);
        [SerializeField, Min(0.01f)] private float _mouseSensitivity = 0.12f;

        [SerializeField, Min(0f)]
        [Tooltip("Vitesse (m/s) au-dessus de laquelle on ne peut pas descendre.")]
        private float _maxExitSpeed = 7f;

        private DrivableCar _car;
        private readonly List<Renderer> _hidden = new List<Renderer>();
        private readonly List<Behaviour> _paused = new List<Behaviour>();
        private bool _phoneWasAvailable;
        private bool _interactionWasActive;
        private bool _exiting;
        private float _followYaw;
        private float _orbitYaw;
        private float _orbitPitch;
        private float _idleMouse;
        private float _enteredAt;
        private float _exitedAt = -10f;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Le joueur conduit.</summary>
        public static bool IsDriving { get; private set; }

        /// <summary>La voiture conduite, ou null.</summary>
        public DrivableCar Vehicle { get { return _car; } }

        /// <summary>Message d'aide du moment (pour l'affichage), vide sinon.</summary>
        public string Hint { get; private set; }

        public static PlayerDriving Instance { get; private set; }

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            DrivableCar.EnterRequested += OnEnterRequested;
        }

        private void OnDisable()
        {
            DrivableCar.EnterRequested -= OnEnterRequested;
            if (_car != null) Exit(false);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            IsDriving = false;
        }

        private bool CanEnter()
        {
            if (_car != null || DoorPortal.AnyPassing || FightIntro.AnyPlaying || ModalScreen.Active) return false;

            // Le E qui fait descendre peut encore être lu, la même image, par le système
            // d'interaction — qui viserait la portière qu'on vient de quitter.
            if (Time.time - _exitedAt < 0.4f) return false;
            if (_combatant != null && !_combatant.CanAct) return false;
            if (_knockdown != null && _knockdown.IsDown) return false;
            return true;
        }

        private void OnEnterRequested(DrivableCar car)
        {
            if (car == null || !CanEnter()) return;
            Enter(car);
        }

        private void Enter(DrivableCar car)
        {
            _car = car;
            _enteredAt = Time.time;
            IsDriving = true;

            if (_phone != null)
            {
                _phoneWasAvailable = _phone.Available;
                _phone.Lower();
                _phone.Available = false;
            }

            if (_interaction != null)
            {
                _interactionWasActive = _interaction.Active;
                _interaction.Active = false;
            }

            if (_input != null) _input.SetCombatLock(this, true);

            _paused.Clear();
            for (int i = 0; i < _pauseWhileDriving.Length; i++)
            {
                Behaviour b = _pauseWhileDriving[i];
                if (b == null || !b.enabled) continue;
                b.enabled = false;
                _paused.Add(b);
            }

            if (_controller != null) _controller.enabled = false;

            _hidden.Clear();
            Transform visuals = _visuals != null ? _visuals : transform;
            Renderer[] renderers = visuals.GetComponentsInChildren<Renderer>();
            for (int i = 0; i < renderers.Length; i++)
            {
                if (!renderers[i].enabled) continue;
                renderers[i].enabled = false;
                _hidden.Add(renderers[i]);
            }

            car.Occupied = true;

            _followYaw = car.transform.eulerAngles.y;
            _orbitYaw = 0f;
            _orbitPitch = 0f;

            if (_chaseCamera != null)
            {
                _chaseCamera.enabled = true;
                PlaceCamera(1f, true);
            }

            if (_gameCamera != null) _gameCamera.enabled = false;
            FollowSeat();
        }

        /// <summary>Descendre. <paramref name="place"/> = poser le joueur à côté de la portière.</summary>
        private void Exit(bool place)
        {
            DrivableCar car = _car;
            if (car == null) return;

            _exiting = true;
            _exitedAt = Time.time;
            _car = null;
            IsDriving = false;
            Hint = string.Empty;

            car.Honk(false);
            car.Occupied = false;

            for (int i = 0; i < _hidden.Count; i++)
            {
                if (_hidden[i] != null) _hidden[i].enabled = true;
            }

            _hidden.Clear();

            for (int i = 0; i < _paused.Count; i++)
            {
                if (_paused[i] != null) _paused[i].enabled = true;
            }

            _paused.Clear();

            if (_controller != null) _controller.enabled = true;

            if (place)
            {
                float radius = _controller != null ? _controller.radius : 0.35f;
                float height = _controller != null ? _controller.height : 1.8f;
                Vector3 position = car.ExitPoint(radius, height);
                Quaternion rotation = Quaternion.Euler(0f, car.transform.eulerAngles.y, 0f);

                ISpawnReceiver[] receivers = GetComponentsInChildren<ISpawnReceiver>(true);
                for (int i = 0; i < receivers.Length; i++)
                {
                    if (!ReferenceEquals(receivers[i], this)) receivers[i].OnSpawned(position, rotation);
                }
            }

            if (_input != null) _input.SetCombatLock(this, false);
            if (_phone != null) _phone.Available = _phoneWasAvailable;
            if (_interaction != null) _interaction.Active = _interactionWasActive;

            if (_gameCamera != null) _gameCamera.enabled = true;
            if (_chaseCamera != null) _chaseCamera.enabled = false;

            _exiting = false;
        }

        public void OnSpawned(Vector3 position, Quaternion rotation)
        {
            // Téléporté (hôpital, chargement) en pleine conduite : on laisse la voiture où elle est.
            if (_car != null && !_exiting) Exit(false);
        }

        private void Update()
        {
            if (_car == null) return;

            if (!_car.isActiveAndEnabled)
            {
                Exit(false);
                return;
            }

            bool live = _input != null && _input.GameplayInputEnabled;
            float speed = _car.ForwardSpeed;

            if (!live)
            {
                _car.SetInput(0f, 0f, false);
                _car.Honk(false);
            }
            else
            {
                IInputProvider provider = _input.Provider;
                InputBindings bindings = _input.Bindings;
                bool handbrake = provider != null && bindings != null && provider.GetHeld(bindings.jump);
                bool horn = provider != null && bindings != null && provider.GetHeld(bindings.attackStraight);

                _car.SetInput(_input.Move.y, _input.Move.x, handbrake);
                _car.Honk(horn);

                Vector2 look = _input.LookDelta;
                if (look.sqrMagnitude > 0.01f)
                {
                    _orbitYaw += look.x * _mouseSensitivity;
                    _orbitPitch = Mathf.Clamp(_orbitPitch - look.y * _mouseSensitivity, -12f, 35f);
                    _idleMouse = 0f;
                }

                // Le premier appui sur E est celui qui a fait monter : on ne redescend pas aussitôt.
                if (_input.InteractPressed && Time.time - _enteredAt > 0.3f)
                {
                    if (Mathf.Abs(speed) <= _maxExitSpeed) Exit(true);
                }
            }

            if (_car == null) return;

            Hint = Mathf.Abs(speed) <= _maxExitSpeed ? "E  descendre" : "Ralentis pour descendre";
            FollowSeat();
        }

        private void FollowSeat()
        {
            if (_car == null) return;

            Transform seat = _car.Seat;
            transform.SetPositionAndRotation(seat.position, Quaternion.Euler(0f, _car.transform.eulerAngles.y, 0f));
        }

        private void LateUpdate()
        {
            if (_car == null || _chaseCamera == null) return;

            FollowSeat();
            PlaceCamera(Time.deltaTime, false);
        }

        private void PlaceCamera(float dt, bool snap)
        {
            Transform car = _car.transform;
            float speed = _car.ForwardSpeed;
            float speed01 = Mathf.Clamp01(Mathf.Abs(speed) / 30f);

            // La caméra rattrape la voiture, sans la coller : un virage se voit.
            float carYaw = car.eulerAngles.y;
            if (speed < -2f) carYaw = _followYaw;          // en marche arrière on ne fait pas volte-face
            _followYaw = snap ? carYaw : Mathf.LerpAngle(_followYaw, carYaw, 1f - Mathf.Exp(-3.5f * dt));

            _idleMouse += dt;
            if (_idleMouse > 1.4f)
            {
                float back = 1f - Mathf.Exp(-2.5f * dt);
                _orbitYaw = Mathf.LerpAngle(_orbitYaw, 0f, back);
                _orbitPitch = Mathf.Lerp(_orbitPitch, 0f, back);
            }

            Quaternion rotation = Quaternion.Euler(_pitch + _orbitPitch, _followYaw + _orbitYaw, 0f);
            Vector3 pivot = car.position + Vector3.up * _height;
            float distance = Mathf.Lerp(_distance, _distance * 1.18f, speed01);
            Vector3 back3 = rotation * Vector3.back;

            // Un mur entre la voiture et la caméra : la caméra passe devant.
            int count = Physics.SphereCastNonAlloc(pivot, 0.3f, back3, _hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c.transform.IsChildOf(car) || c.transform.IsChildOf(transform)) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                distance = Mathf.Min(distance, Mathf.Max(1.2f, _hits[i].distance));
            }

            Transform cam = _chaseCamera.transform;
            Vector3 position = pivot + back3 * distance;
            cam.position = snap ? position : Vector3.Lerp(cam.position, position, 1f - Mathf.Exp(-18f * dt));
            cam.rotation = Quaternion.LookRotation(pivot + car.forward * 1.2f - cam.position, Vector3.up);

            float fov = Mathf.Lerp(_fieldOfView.x, _fieldOfView.y, speed01 * speed01);
            _chaseCamera.fieldOfView = snap ? fov : Mathf.Lerp(_chaseCamera.fieldOfView, fov, 1f - Mathf.Exp(-3f * dt));
        }
    }
}
