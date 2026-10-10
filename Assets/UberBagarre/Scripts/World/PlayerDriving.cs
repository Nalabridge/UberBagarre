using System.Collections;
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

        [SerializeField, Min(2f)] private float _distance = 5.6f;
        [SerializeField, Min(0.5f)] private float _height = 1.5f;
        [SerializeField] private float _pitch = 9f;
        [SerializeField] private Vector2 _fieldOfView = new Vector2(60f, 72f);
        [SerializeField, Min(0.01f)] private float _mouseSensitivity = 0.12f;

        /// <summary>Les vues au volant : poursuite proche, poursuite éloignée, place du conducteur (touche V).</summary>
        public enum View
        {
            Proche = 0,
            Eloignee = 1,
            Conducteur = 2
        }

        private static View _view = View.Proche;
        private Vector3 _pivotPosition;
        private Vector3 _pivotVelocity;
        private float _yawVelocity;
        private float _lookYaw;
        private float _lookPitch;
        private Vector3 _lastCarVelocity;
        private Vector3 _headOffset;
        private float _defaultNear = 0.3f;

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
        private float _hotwire;
        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private bool _lookBack;

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
                _defaultNear = _chaseCamera.nearClipPlane;
                _chaseCamera.enabled = true;
                _lookYaw = 0f;
                _lookPitch = 0f;
                _lastCarVelocity = car.Body != null ? car.Body.linearVelocity : Vector3.zero;
                ApplyView();
                PlaceCamera(1f, true);
            }

            if (_gameCamera != null) _gameCamera.enabled = false;
            FollowSeat();
        }

        /// <summary>Descendre. <paramref name="place"/> = poser le joueur à côté de la portière.</summary>
        /// <summary>Sortir de la voiture tout de suite (une arrestation, une coupure).</summary>
        public void ForceExit()
        {
            if (_car != null) Exit(false);
        }

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
            CarCockpit cockpit = car.GetComponent<CarCockpit>();
            if (cockpit != null) cockpit.SetInside(false);
            if (_chaseCamera != null) _chaseCamera.nearClipPlane = _defaultNear;
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

            // Descendre se voit : la portière claque, la caméra quitte l'arrière de la voiture et
            // glisse jusqu'aux yeux du joueur, debout à côté de la portière.
            if (place && _gameCamera != null && _chaseCamera != null && _chaseCamera.enabled)
            {
                StartCoroutine(StepOut(car));
            }
            else
            {
                if (_gameCamera != null) _gameCamera.enabled = true;
                if (_chaseCamera != null) _chaseCamera.enabled = false;
            }

            _exiting = false;
        }

        [SerializeField, Min(0.1f)] private float _stepOutDuration = 0.75f;
        private AudioSource _doorSource;
        private AudioClip _doorClip;

        private IEnumerator StepOut(DrivableCar car)
        {
            if (_input != null) _input.SetGameplayLock(this, true);
            PlayDoor(car != null ? car.transform.position : transform.position);

            Transform cam = _chaseCamera.transform;
            Vector3 fromPosition = cam.position;
            Quaternion fromRotation = cam.rotation;
            float fromFov = _chaseCamera.fieldOfView;

            float t = 0f;
            while (t < 1f)
            {
                t = Mathf.Min(1f, t + Time.deltaTime / _stepOutDuration);
                float e = t * t * (3f - 2f * t);

                // La cible bouge avec la tête (léger balancement du pas) : on la relit à chaque image.
                Transform eye = _gameCamera.transform;
                cam.position = Vector3.Lerp(fromPosition, eye.position, e);
                cam.rotation = Quaternion.Slerp(fromRotation, eye.rotation, e);
                _chaseCamera.fieldOfView = Mathf.Lerp(fromFov, _gameCamera.fieldOfView, e);
                yield return null;
            }

            _gameCamera.enabled = true;
            _chaseCamera.enabled = false;
            if (_input != null) _input.SetGameplayLock(this, false);
        }

        /// <summary>La portière : le déclic de la poignée, puis le claquement sourd.</summary>
        private void PlayDoor(Vector3 at)
        {
            if (_doorSource == null)
            {
                // Sa propre source, sur un objet à part : on la place à la portière sans déplacer le joueur.
                GameObject holder = new GameObject("Son de portiere");
                holder.transform.SetParent(transform, false);
                _doorSource = holder.AddComponent<AudioSource>();
                _doorSource.playOnAwake = false;
                _doorSource.spatialBlend = 0.6f;
            }

            if (_doorClip == null) _doorClip = CarAudio.DoorClip;

            _doorSource.transform.position = at;
            _doorSource.PlayOneShot(_doorClip, 0.8f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
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

                if (_car.NeedsHotwire)
                {
                    // Une voiture volée n'a pas de clé : on fait les fils sous le volant.
                    _hotwire += Time.deltaTime / 2.6f;
                    _car.SetInput(0f, 0f, true);
                    if (_hotwire >= 1f)
                    {
                        _hotwire = 0f;
                        _car.NeedsHotwire = false;
                    }
                }
                else
                {
                    _car.SetInput(_input.Move.y, _input.Move.x, handbrake);
                    _car.Honk(horn);
                    // Ceux qui sont devant entendent le klaxon (ils se rangent).
                    if (horn) TrafficDriver.NotifyHorn(_car.transform.position, _car.transform.forward);
                }

                // V : la vue suivante (proche, éloignée, conducteur).
                if (provider != null && bindings != null && provider.GetPressedThisFrame(bindings.attackLowKick))
                {
                    _view = (View)(((int)_view + 1) % 3);
                    ApplyView();
                    PlaceCamera(1f, true);
                }

                _lookBack = provider != null && bindings != null && provider.GetHeld(bindings.crouch);

                Vector2 look = _input.LookDelta;
                if (look.sqrMagnitude > 0.01f)
                {
                    if (_view == View.Conducteur)
                    {
                        _lookYaw = Mathf.Clamp(_lookYaw + look.x * _mouseSensitivity, -135f, 135f);
                        _lookPitch = Mathf.Clamp(_lookPitch - look.y * _mouseSensitivity, -45f, 35f);
                    }
                    else
                    {
                        _orbitYaw += look.x * _mouseSensitivity;
                        _orbitPitch = Mathf.Clamp(_orbitPitch - look.y * _mouseSensitivity, -12f, 35f);
                    }

                    _idleMouse = 0f;
                }

                // Le premier appui sur E est celui qui a fait monter : on ne redescend pas aussitôt.
                if (_input.InteractPressed && Time.time - _enteredAt > 0.3f)
                {
                    if (Mathf.Abs(speed) <= _maxExitSpeed) Exit(true);
                }
            }

            if (_car == null) return;

            Hint = _car.NeedsHotwire ? "Tu fais les fils sous le volant…  " + Mathf.RoundToInt(_hotwire * 100f) + " %"
                : Mathf.Abs(speed) <= _maxExitSpeed ? "E  descendre" : "Ralentis pour descendre";
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

        /// <summary>La vue choisie : l'habitacle se montre de l'intérieur, la caméra s'approche des yeux.</summary>
        private void ApplyView()
        {
            if (_car == null || _chaseCamera == null) return;
            CarCockpit cockpit = _car.GetComponent<CarCockpit>();
            bool inside = _view == View.Conducteur && cockpit != null && cockpit.Eye != null;
            if (cockpit != null) cockpit.SetInside(inside);
            _chaseCamera.nearClipPlane = inside ? 0.03f : _defaultNear;
            _lookYaw = 0f;
            _lookPitch = 0f;
        }

        private void PlaceCamera(float dt, bool snap)
        {
            CarCockpit cockpit = _car.GetComponent<CarCockpit>();
            if (_view == View.Conducteur && cockpit != null && cockpit.Eye != null)
            {
                PlaceInside(cockpit, dt, snap);
                return;
            }

            Transform car = _car.transform;
            float speed = _car.ForwardSpeed;
            float speed01 = Mathf.Clamp01(Mathf.Abs(speed) / 30f);
            bool far = _view == View.Eloignee;

            // Le cap suivi : celui de la voiture, et en glisse un peu celui de la trajectoire — on
            // voit la voiture partir de travers, comme dans GTA. En marche arrière, on ne fait pas
            // volte-face.
            float carYaw = car.eulerAngles.y;
            Vector3 velocity = _car.Body != null ? _car.Body.linearVelocity : Vector3.zero;
            Vector3 flat = new Vector3(velocity.x, 0f, velocity.z);
            float targetYaw = carYaw;
            if (speed > 3f && flat.sqrMagnitude > 9f)
            {
                float travel = Mathf.Atan2(flat.x, flat.z) * Mathf.Rad2Deg;
                targetYaw = Mathf.LerpAngle(carYaw, travel, Mathf.Clamp01((flat.magnitude - 3f) / 8f) * 0.45f);
            }

            if (speed < -2f) targetYaw = _followYaw;
            if (_lookBack) targetYaw = carYaw + 180f;
            float lag = _lookBack ? 0.05f : Mathf.Lerp(0.3f, 0.16f, speed01);
            _followYaw = snap ? targetYaw : Mathf.SmoothDampAngle(_followYaw, targetYaw, ref _yawVelocity, lag, 720f, dt);

            _idleMouse += dt;
            if (_idleMouse > 1.4f)
            {
                float back = 1f - Mathf.Exp(-2.5f * dt);
                _orbitYaw = Mathf.LerpAngle(_orbitYaw, 0f, back);
                _orbitPitch = Mathf.Lerp(_orbitPitch, 0f, back);
            }

            // Le point visé suit la voiture par un ressort : la caméra se laisse distancer à
            // l'accélération et rattrape au freinage — on sent la poussée et le freinage.
            Vector3 anchor = car.position + Vector3.up * (far ? 2.0f : _height);
            if (snap)
            {
                _pivotPosition = anchor;
                _pivotVelocity = Vector3.zero;
            }
            else
            {
                _pivotPosition = Vector3.SmoothDamp(_pivotPosition, anchor, ref _pivotVelocity, 0.07f, Mathf.Infinity, dt);
                // Jamais trop loin derrière (un choc, une téléportation).
                if ((_pivotPosition - anchor).sqrMagnitude > 4f) _pivotPosition = anchor + (_pivotPosition - anchor).normalized * 2f;
            }

            float pitch = (far ? _pitch + 4f : _pitch) - speed01 * 2f + _orbitPitch;
            Quaternion rotation = Quaternion.Euler(pitch, _followYaw + _orbitYaw, 0f);
            float distance = far ? Mathf.Lerp(7.6f, 8.8f, speed01) : Mathf.Lerp(_distance, _distance * 1.15f, speed01);
            Vector3 back3 = rotation * Vector3.back;

            // Un mur entre la voiture et la caméra : la caméra passe devant.
            int count = Physics.SphereCastNonAlloc(_pivotPosition, 0.3f, back3, _hits, distance, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c.transform.IsChildOf(car) || c.transform.IsChildOf(transform)) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                distance = Mathf.Min(distance, Mathf.Max(1.2f, _hits[i].distance));
            }

            Transform cam = _chaseCamera.transform;
            Vector3 position = _pivotPosition + back3 * distance;
            cam.position = snap ? position : Vector3.Lerp(cam.position, position, 1f - Mathf.Exp(-20f * dt));

            // On regarde un peu devant la voiture, d'autant plus qu'elle va vite.
            Vector3 lookAhead = car.forward * Mathf.Clamp(speed * 0.1f, -1f, 3f);
            if (_lookBack) lookAhead = -car.forward * 1.5f;
            Quaternion look = Quaternion.LookRotation(_pivotPosition + lookAhead + Vector3.up * -0.25f - cam.position, Vector3.up);
            cam.rotation = look * Shake(speed01, dt);

            float fov = Mathf.Lerp(_fieldOfView.x, _fieldOfView.y, speed01 * speed01);
            _chaseCamera.fieldOfView = snap ? fov : Mathf.Lerp(_chaseCamera.fieldOfView, fov, 1f - Mathf.Exp(-3f * dt));
        }

        /// <summary>
        /// La place du conducteur : la caméra aux yeux, qui roule avec la voiture ; la tête se
        /// penche dans les virages et plonge au freinage (l'inertie du corps), le regard
        /// accompagne le volant, la souris tourne la tête (elle revient d'elle-même).
        /// </summary>
        private void PlaceInside(CarCockpit cockpit, float dt, bool snap)
        {
            Transform car = _car.transform;
            Vector3 velocity = _car.Body != null ? _car.Body.linearVelocity : Vector3.zero;
            Vector3 accel = dt > 0f && !snap ? (velocity - _lastCarVelocity) / dt : Vector3.zero;
            _lastCarVelocity = velocity;

            Vector3 localAccel = car.InverseTransformDirection(accel);
            Vector3 wanted = new Vector3(Mathf.Clamp(-localAccel.x * 0.004f, -0.05f, 0.05f), Mathf.Clamp(-localAccel.y * 0.002f, -0.03f, 0.03f),
                Mathf.Clamp(-localAccel.z * 0.004f, -0.05f, 0.05f));
            _headOffset = snap ? Vector3.zero : Vector3.Lerp(_headOffset, wanted, 1f - Mathf.Exp(-6f * dt));

            _idleMouse += dt;
            if (_idleMouse > 1.6f && !_lookBack)
            {
                float back = 1f - Mathf.Exp(-3f * dt);
                _lookYaw = Mathf.LerpAngle(_lookYaw, 0f, back);
                _lookPitch = Mathf.Lerp(_lookPitch, 0f, back);
            }

            float yaw = _lookBack ? 160f : _lookYaw + _car.WheelAngle * 0.35f;
            Transform cam = _chaseCamera.transform;
            cam.position = cockpit.Eye.position + car.TransformDirection(_headOffset);
            cam.rotation = car.rotation * Quaternion.Euler(5f + _lookPitch, yaw, 0f) *
                           Shake(Mathf.Clamp01(Mathf.Abs(_car.ForwardSpeed) / 30f) * 0.6f, dt);
            _chaseCamera.fieldOfView = snap ? 64f : Mathf.Lerp(_chaseCamera.fieldOfView, 64f, 1f - Mathf.Exp(-4f * dt));
        }

        /// <summary>Un tremblement léger qui grandit avec la vitesse (la route qui passe sous les roues).</summary>
        private static Quaternion Shake(float speed01, float dt)
        {
            float k = speed01 * speed01 * 0.22f;
            if (k < 0.001f) return Quaternion.identity;
            float t = Time.time * 13f;
            return Quaternion.Euler((Mathf.PerlinNoise(t, 3.1f) - 0.5f) * k, (Mathf.PerlinNoise(7.7f, t) - 0.5f) * k, 0f);
        }
    }
}
