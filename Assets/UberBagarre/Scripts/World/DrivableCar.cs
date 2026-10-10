using System;
using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Une voiture qu'on conduit. Physique d'arcade, mais une vraie : un corps rigide posé sur
    /// quatre suspensions (un rayon par roue, un ressort et un amortisseur), des pneus qui
    /// accrochent en travers et patinent au-delà de leur adhérence, un frein à main qui libère
    /// l'arrière. Ce n'est pas une simulation — pas de boîte, pas de couple moteur par régime —
    /// mais la voiture a du poids : elle plonge au freinage, s'assoit à l'accélération, glisse
    /// quand on la jette dans un virage.
    ///
    /// Le son du moteur est fabriqué ici (pas de fichier) : quelques harmoniques d'un quatre
    /// cylindres, un grain de bruit, et le régime qui monte et retombe à chaque rapport.
    ///
    /// Qui conduit n'est pas son affaire : <see cref="SetInput"/> vient du joueur
    /// (voir <c>PlayerDriving</c>) ou d'un conducteur de la circulation (<c>TrafficDriver</c>,
    /// qui passe la voiture en <see cref="Autopilot"/>) : la même voiture, la même physique.
    /// Garée, elle serre son frein à main et coupe ses phares.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class DrivableCar : MonoBehaviour
    {
        [Serializable]
        public class Wheel
        {
            [Tooltip("Pivot de la roue (braquage, debattement). Son repos = roue posee au sol.")]
            public Transform pivot;

            [Tooltip("Enfant du pivot qui tourne avec la vitesse.")]
            public Transform spin;

            public bool front;

            [NonSerialized] public Vector3 rest;
            [NonSerialized] public Vector3 origin;
            [NonSerialized] public Vector3 contact;
            [NonSerialized] public Vector3 normal;
            [NonSerialized] public float compression;
            [NonSerialized] public float length;
            [NonSerialized] public bool grounded;
            [NonSerialized] public float load;
            [NonSerialized] public float slip;
            [NonSerialized] public float angle;
        }

        [SerializeField] private Wheel[] _wheels = new Wheel[0];

        [Header("Places")]
        [SerializeField]
        [Tooltip("Siege conducteur : le joueur y est range pendant la conduite.")]
        private Transform _seat;

        [SerializeField] private string _displayName = "Voiture";

        [Header("Suspension")]
        [SerializeField, Min(0.05f)] private float _wheelRadius = 0.34f;
        [SerializeField, Min(0.05f)] private float _travelUp = 0.16f;
        [SerializeField, Min(0.05f)] private float _travelDown = 0.18f;

        [SerializeField, Range(1f, 3.5f)]
        [Tooltip("Frequence propre de la suspension (Hz) : 1,9 = berline ferme, 2,5 = sportive.")]
        private float _suspensionFrequency = 1.9f;

        [SerializeField, Range(0.1f, 1f)] private float _bumpDamping = 0.32f;
        [SerializeField, Range(0.1f, 1f)] private float _reboundDamping = 0.55f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Barres anti-roulis (fraction de la raideur d'un ressort).")]
        private float _antiRollFront = 0.2f;

        [SerializeField, Range(0f, 1.5f)] private float _antiRollRear = 0.1f;
        [SerializeField] private Vector3 _centerOfMass = new Vector3(0f, 0.38f, 0.06f);

        [Header("Moteur et freins")]
        [SerializeField, Min(1f)] private float _maxSpeed = 32f;
        [SerializeField, Min(1f)] private float _reverseSpeed = 9f;

        [SerializeField, Min(100f)]
        [Tooltip("Poussee de reference (N) : fixe la puissance du moteur avec la vitesse de pointe.")]
        private float _engineForce = 9800f;

        [SerializeField] private float[] _gearRatios = { 3.45f, 2.15f, 1.5f, 1.12f, 0.9f, 0.74f };
        [SerializeField, Min(0.5f)] private float _finalDrive = 3.9f;
        [SerializeField, Min(500f)] private float _idleRpm = 850f;
        [SerializeField, Min(2000f)] private float _redline = 6600f;
        [SerializeField, Min(1000f)] private float _peakRpm = 5200f;

        [Header("Direction et adherence")]
        [SerializeField, Range(10f, 50f)] private float _maxSteer = 36f;

        [SerializeField, Min(1f)]
        [Tooltip("Vitesse (m/s) a laquelle le braquage maximal est divise par deux.")]
        private float _steerFalloff = 13f;

        [SerializeField, Range(2f, 20f)] private float _minSteer = 7.5f;
        [SerializeField, Min(10f)] private float _steerRate = 110f;
        [SerializeField, Min(0.1f)] private float _grip = 1.05f;
        [SerializeField, Min(0.1f)] private float _rearGrip = 1.1f;
        [SerializeField, Range(2f, 20f)] private float _tirePeakFront = 7.5f;
        [SerializeField, Range(2f, 20f)] private float _tirePeakRear = 6f;
        [SerializeField, Range(0f, 0.4f)] private float _loadSensitivity = 0.12f;
        [SerializeField, Range(0.05f, 1f)] private float _handbrakeRearGrip = 0.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Hauteur (fraction du centre de gravite) ou s'appliquent les forces des pneus.")]
        private float _rollCenter = 0.05f;

        [SerializeField, Min(0f)] private float _downforce = 0.6f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Aide au contre-braquage (joueur) : les roues avant suivent la glisse.")]
        private float _counterSteer = 0.85f;

        [SerializeField, Range(0f, 2f)]
        [Tooltip("Stabilite : amortit le lacet que le volant ne demande pas (sauf frein a main).")]
        private float _stability = 0.6f;

        [Header("Feux")]
        [SerializeField] private Light[] _headlights = new Light[0];
        [SerializeField] private Light _brakeLight;
        [SerializeField, Min(0f)] private float _brakeLightIntensity = 2.4f;

        [Header("Son")]
        [SerializeField] private AudioSource _engine;
        [SerializeField] private AudioSource _tires;
        [SerializeField] private AudioSource _horn;
        [SerializeField] private AudioSource _impacts;

        private Rigidbody _body;
        private Interactable _interactable;
        private float _throttle;
        private float _steerInput;
        private bool _handbrake;
        private float _steer;
        private float _rpm;
        private int _gear = 1;
        private float _shiftDip;
        private float _shiftTimer;
        private float _engineRpm;
        private float _pedal;

        // Suspension calculée au réveil (masse, fréquence, amortissement).
        private float _spring;
        private float _bump;
        private float _rebound;
        private float _peakTorque;
        private float _drag;
        private float _brakeTotal;
        private float _rollResistance;
        private bool _occupied;
        private bool _autopilot;
        private float _upsideDownFor;
        private Vector3 _lastSafePosition;
        private Quaternion _lastSafeRotation;
        private float _safeTimer;
        private float _settledFor;

        private static readonly List<DrivableCar> _all = new List<DrivableCar>();

        /// <summary>La voiture que le joueur conduit, ou null.</summary>
        public static DrivableCar Driven { get; private set; }

        /// <summary>Toutes les voitures conduisibles actives.</summary>
        public static IReadOnlyList<DrivableCar> All { get { return _all; } }

        /// <summary>Le joueur demande à monter (E sur la portière).</summary>
        public static event Action<DrivableCar> EnterRequested;

        /// <summary>À qui est la voiture : ça décide de ce que fait E sur la portière.</summary>
        public enum Access
        {
            /// <summary>N'importe qui monte (le bac à sable, les voitures de test).</summary>
            Libre = 0,

            /// <summary>La voiture du joueur (la sienne, ou achetée) : elle s'ouvre.</summary>
            Perso = 1,

            /// <summary>Garée par la ville : fermée à clé (le plus souvent). Il faut la crocheter.</summary>
            Garee = 2,

            /// <summary>Dans la circulation, un conducteur au volant : il faut l'en sortir.</summary>
            Circulation = 3
        }

        [Header("Propriété")]
        [SerializeField] private Access _access = Access.Libre;
        [SerializeField] private bool _locked;

        public Access Kind
        {
            get { return _access; }
            set
            {
                _access = value;
                RefreshAccessLabel();
            }
        }

        /// <summary>Portière verrouillée : il faut la crocheter.</summary>
        public bool Locked
        {
            get { return _locked; }
            set
            {
                _locked = value;
                RefreshAccessLabel();
            }
        }

        /// <summary>Volée par le joueur (la police la cherche).</summary>
        public bool Stolen { get; set; }

        /// <summary>Pas de clé : il faut faire les fils avant de démarrer.</summary>
        public bool NeedsHotwire { get; set; }

        /// <summary>Portière verrouillée : le joueur veut la crocheter.</summary>
        public static event Action<DrivableCar> LockpickRequested;

        /// <summary>Un conducteur au volant : le joueur veut l'en sortir.</summary>
        public static event Action<DrivableCar> CarjackRequested;

        private void RefreshAccessLabel()
        {
            Interactable door = _interactable != null ? _interactable : GetComponent<Interactable>();
            if (door == null) return;

            switch (_access)
            {
                case Access.Perso: door.Label = "Monter"; break;
                case Access.Garee: door.Label = _locked ? "Crocheter la portière" : Stolen ? "Monter" : "Voler la voiture"; break;
                case Access.Circulation: door.Label = "Sortir le conducteur"; break;
            }
        }

        public Transform Seat { get { return _seat != null ? _seat : transform; } }
        public string DisplayName { get { return _displayName; } }

        /// <summary>Renomme la voiture (« Ta caisse ») et son invite.</summary>
        public void Rename(string displayName)
        {
            _displayName = displayName;
            Interactable door = GetComponent<Interactable>();
            if (door != null) door.Hint = displayName;
        }
        public Rigidbody Body { get { return _body; } }

        /// <summary>Vitesse le long de l'avant de la voiture (négative en marche arrière).</summary>
        public float ForwardSpeed { get { return _body != null ? Vector3.Dot(_body.linearVelocity, transform.forward) : 0f; } }

        public float SpeedKmh { get { return _body != null ? _body.linearVelocity.magnitude * 3.6f : 0f; } }

        /// <summary>Braquage maximal (degrés) à la vitesse actuelle : un volant à fond donne ça.</summary>
        public float SteerLimit
        {
            get { return LockAt(ForwardSpeed); }
        }

        /// <summary>Le braquage maximal diminue avec la vitesse (comme une direction assistée).</summary>
        private float LockAt(float speed)
        {
            return Mathf.Max(_minSteer, _maxSteer / (1f + Mathf.Abs(speed) / _steerFalloff));
        }

        /// <summary>Angle actuel des roues avant (degrés, + à droite) : le volant de l'habitacle le suit.</summary>
        public float WheelAngle { get { return _steer; } }

        /// <summary>Régime moteur en tours par minute (le son du moteur).</summary>
        public float EngineRpm { get { return _engineRpm; } }

        /// <summary>Zone rouge du moteur (tr/min).</summary>
        public float Redline { get { return _redline; } }

        /// <summary>Pédale d'accélérateur (0 à 1), coupée pendant un passage de rapport.</summary>
        public float EngineLoad { get { return _shiftTimer > 0f ? 0f : _pedal; } }

        /// <summary>Un rapport vient de passer (le son coupe et repart).</summary>
        public float ShiftDip { get { return _shiftDip; } }

        /// <summary>Le plus fort glissement des pneus au sol (m/s) : le crissement.</summary>
        public float TireSlip
        {
            get
            {
                float slip = 0f;
                for (int i = 0; i < _wheels.Length; i++)
                {
                    if (_wheels[i] != null && _wheels[i].grounded) slip = Mathf.Max(slip, _wheels[i].slip);
                }

                return slip;
            }
        }

        public bool Handbrake { get { return _handbrake; } }

        /// <summary>Freine (pédale ou frein à main) : les feux stop.</summary>
        public bool Braking
        {
            get
            {
                float speed = ForwardSpeed;
                return _handbrake || (_throttle > 0.05f && speed < -0.6f) || (_throttle < -0.05f && speed > 0.6f);
            }
        }

        /// <summary>Quelqu'un tient le volant (le moteur tourne).</summary>
        public bool EngineOn { get { return Driving; } }

        public float MaxSpeed { get { return _maxSpeed; } }

        private float _wheelbase;

        /// <summary>Distance entre les essieux (mètres), mesurée sur les roues.</summary>
        public float Wheelbase
        {
            get
            {
                if (_wheelbase > 0f) return _wheelbase;
                float front = 0f, rear = 0f;
                int nf = 0, nr = 0;
                for (int i = 0; i < _wheels.Length; i++)
                {
                    if (_wheels[i] == null || _wheels[i].pivot == null) continue;
                    float z = transform.InverseTransformPoint(_wheels[i].pivot.position).z;
                    if (_wheels[i].front) { front += z; nf++; }
                    else { rear += z; nr++; }
                }

                return nf > 0 && nr > 0 ? Mathf.Max(1.5f, front / nf - rear / nr) : 2.7f;
            }
        }

        /// <summary>
        /// Conduite par la circulation : la voiture roule (phares, moteur, pas de frein de
        /// parking), mais le joueur peut toujours la prendre — elle n'est pas « la sienne ».
        /// </summary>
        public bool Autopilot
        {
            get { return _autopilot; }
            set
            {
                if (_autopilot == value) return;
                _autopilot = value;
                if (_occupied) return;

                for (int i = 0; i < _headlights.Length; i++)
                {
                    if (_headlights[i] != null) _headlights[i].enabled = value;
                }

                if (_audio != null) _audio.EngineRunning(value);

                if (value && _body != null)
                {
                    _body.isKinematic = false;
                    _body.WakeUp();
                }

                if (!value) SetInput(0f, 0f, true);
            }
        }

        /// <summary>
        /// Endormie par son conducteur (loin du joueur) : plus aucune force, c'est lui qui la
        /// fait glisser le long de sa route.
        /// </summary>
        public bool Sleeping { get; set; }

        /// <summary>Quelqu'un (joueur ou circulation) tient le volant.</summary>
        private bool Driving { get { return _occupied || _autopilot; } }

        /// <summary>Régime affiché (0 à 1) et rapport engagé (0 = marche arrière).</summary>
        public float Rpm { get { return _rpm; } }
        public int Gear { get { return _gear; } }

        public bool Occupied
        {
            get { return _occupied; }
            set
            {
                _occupied = value;
                if (value) _autopilot = false;
                if (value) Driven = this;
                else if (Driven == this) Driven = null;
                if (_interactable != null) _interactable.SetAvailable(!value);
                if (!value) SetInput(0f, 0f, true);
                for (int i = 0; i < _headlights.Length; i++)
                {
                    if (_headlights[i] != null) _headlights[i].enabled = value;
                }

                if (_audio != null) _audio.EngineRunning(value);

                if (value)
                {
                    _body.isKinematic = false;
                    _body.WakeUp();
                }
            }
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _body.isKinematic = false;
            _body.interpolation = RigidbodyInterpolation.Interpolate;
            _body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;
            // Sortie d'un chevauchement (apparition, collision brutale) : en douceur, pas en boulet.
            _body.maxDepenetrationVelocity = 3f;
            _body.maxAngularVelocity = 7f;
            _body.centerOfMass = _centerOfMass;
            if (_body.mass < 100f) _body.mass = 1250f;
            _body.linearDamping = 0.02f;
            _body.angularDamping = 0.6f;

            Tune();

            // Le ressort est tendu pour qu'au repos, chargée de son propre poids, la roue soit
            // exactement là où le modèle la dessine.
            float sag = _body.mass * -Physics.gravity.y / Mathf.Max(1, _wheels.Length) / _spring;

            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel w = _wheels[i];
                if (w == null || w.pivot == null) continue;
                w.rest = transform.InverseTransformPoint(w.pivot.position);
                w.length = _travelUp + sag;
            }

            _wheelbase = 0f;
            _wheelbase = Wheelbase;

            _interactable = GetComponent<Interactable>();
            if (_interactable != null) _interactable.Activated += OnActivated;

            // Une carrosserie qui frotte un mur glisse le long : sans cela elle s'y accroche et
            // la voiture pivote sur son coin.
            PhysicsMaterial slick = new PhysicsMaterial("Carrosserie");
            slick.dynamicFriction = 0.15f;
            slick.staticFriction = 0.15f;
            slick.frictionCombine = PhysicsMaterialCombine.Minimum;
            slick.bounciness = 0.05f;
            Collider[] colliders = GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++)
            {
                if (!colliders[i].isTrigger) colliders[i].sharedMaterial = slick;
            }

            RefreshAccessLabel();
            SetupAudio();
            _lastSafePosition = transform.position;
            _lastSafeRotation = transform.rotation;
            Occupied = false;
        }

        private void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
            if (Driven == this) Driven = null;
        }

        private void OnDestroy()
        {
            if (_interactable != null) _interactable.Activated -= OnActivated;
        }

        private void OnActivated(Interactable source)
        {
            if (_occupied) return;

            switch (_access)
            {
                case Access.Garee:
                    if (_locked)
                    {
                        if (LockpickRequested != null) LockpickRequested(this);
                        return;
                    }

                    // Pas fermée à clé : on monte, mais c'est un vol, et il n'y a pas de clé.
                    if (!Stolen)
                    {
                        Stolen = true;
                        NeedsHotwire = true;
                        Crimes.Report(Crime.VolDeVoiture, transform.position, gameObject);
                        RefreshAccessLabel();
                    }

                    break;

                case Access.Circulation:
                    if (_autopilot)
                    {
                        if (CarjackRequested != null) CarjackRequested(this);
                        return;
                    }

                    break;
            }

            if (EnterRequested != null) EnterRequested(this);
        }

        /// <summary>Le joueur monte (après un crochetage, une sortie de force…).</summary>
        public void RequestEnter()
        {
            if (!_occupied && EnterRequested != null) EnterRequested(this);
        }

        /// <summary>Le conducteur est sorti de force : la voiture est au joueur, moteur tournant.</summary>
        public void TakenByForce()
        {
            _access = Access.Garee;
            _locked = false;
            Stolen = true;
            NeedsHotwire = false;
            RefreshAccessLabel();
        }

        /// <summary>La serrure a cédé (crochetage réussi).</summary>
        public void Unlock(bool stolen)
        {
            _locked = false;
            if (stolen)
            {
                Stolen = true;
                NeedsHotwire = true;
            }

            RefreshAccessLabel();
        }

        /// <summary>Gaz (+) / frein puis marche arrière (−), volant (−1 gauche, +1 droite), frein à main.</summary>
        public void SetInput(float throttle, float steer, bool handbrake)
        {
            _throttle = Mathf.Clamp(throttle, -1f, 1f);
            _steerInput = Mathf.Clamp(steer, -1f, 1f);
            _handbrake = handbrake;
        }

        public void Honk(bool on)
        {
            if (_audio != null) _audio.Honk(on);
        }

        /// <summary>
        /// Où descendre : côté conducteur si c'est libre, sinon de l'autre côté, sinon derrière,
        /// sinon sur le toit (une voiture coincée entre deux murs ne retient personne).
        /// </summary>
        public Vector3 ExitPoint(float radius, float height)
        {
            Vector3[] offsets =
            {
                new Vector3(-1.55f, 0f, 0.3f), new Vector3(1.55f, 0f, 0.3f), new Vector3(0f, 0f, -3.1f),
                new Vector3(0f, 0f, 3.2f)
            };

            for (int i = 0; i < offsets.Length; i++)
            {
                Vector3 p = transform.TransformPoint(offsets[i]);
                p.y = transform.position.y + 0.05f;

                RaycastHit ground;
                if (Physics.Raycast(p + Vector3.up * 1.5f, Vector3.down, out ground, 4f, ~0, QueryTriggerInteraction.Ignore))
                {
                    p.y = ground.point.y + 0.02f;
                }

                Vector3 bottom = p + Vector3.up * (radius + 0.05f);
                Vector3 top = p + Vector3.up * (height - radius);
                if (!Physics.CheckCapsule(bottom, top, radius * 0.95f, ~0, QueryTriggerInteraction.Ignore)) return p;
            }

            return transform.position + Vector3.up * 2.2f;
        }

        /// <summary>Remet la voiture sur ses roues (retournée, coincée, tombée).</summary>
        public void Recover()
        {
            Vector3 forward = transform.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-3f) forward = Vector3.forward;

            _body.linearVelocity = Vector3.zero;
            _body.angularVelocity = Vector3.zero;

            Vector3 position = transform.position.y < -10f ? _lastSafePosition : transform.position + Vector3.up * 1.2f;
            Quaternion rotation = transform.position.y < -10f
                ? _lastSafeRotation
                : Quaternion.LookRotation(forward.normalized, Vector3.up);

            _body.position = position;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(position, rotation);
        }

        // ------------------------------------------------------------------ physique

        /// <summary>
        /// Les grandeurs qui découlent de la masse : raideur des ressorts (fréquence propre),
        /// amortisseurs (fraction de l'amortissement critique), couple du moteur (calé pour que la
        /// vitesse de pointe soit celle du modèle), traînée, freins.
        /// </summary>
        private void Tune()
        {
            float mass = _body.mass;
            float corner = mass / Mathf.Max(1, _wheels.Length);
            float omega = 2f * Mathf.PI * _suspensionFrequency;
            _spring = corner * omega * omega;
            float critical = 2f * Mathf.Sqrt(_spring * corner);
            _bump = _bumpDamping * critical;
            _rebound = _reboundDamping * critical;

            float peakPower = _engineForce * _maxSpeed * 0.62f;
            _peakTorque = peakPower / (_peakRpm * 2f * Mathf.PI / 60f) / 0.93f;
            _rollResistance = 0.012f * mass * 9.81f;
            float wheelPower = peakPower * 0.88f;
            _drag = Mathf.Max(0.25f, (wheelPower / _maxSpeed - _rollResistance) / (_maxSpeed * _maxSpeed));
            _brakeTotal = 1.15f * mass * 9.81f;
            _engineRpm = _idleRpm;
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (Sleeping && !_occupied) return;
            if (Park(dt)) return;

            Vector3 up = transform.up;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;
            float speed = ForwardSpeed;
            float mass = _body.mass;
            float g = -Physics.gravity.y;

            Steering(speed, up, forward, right, dt);

            // Gaz dans le sens de la marche = moteur ; à contre-sens = frein, puis marche
            // arrière une fois presque arrêté. Comme toutes les voitures de jeu.
            bool braking = (_throttle > 0.05f && speed < -0.6f) || (_throttle < -0.05f && speed > 0.6f);
            float drive = Drivetrain(speed, dt);
            if (braking || _handbrake) drive = 0f;
            float brake = braking ? Mathf.Abs(_throttle) * _brakeTotal : 0f;

            // --- suspension : la compression de chaque roue d'abord (les barres anti-roulis
            // relient les deux roues d'un essieu)
            int grounded = 0;
            float reach = _travelUp + _travelDown + _wheelRadius;
            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel w = _wheels[i];
                if (w == null || w.pivot == null) continue;

                w.origin = transform.TransformPoint(w.rest + Vector3.up * _travelUp);
                RaycastHit hit;
                w.grounded = Physics.Raycast(w.origin, -up, out hit, reach, ~0, QueryTriggerInteraction.Ignore) &&
                             !hit.collider.transform.IsChildOf(transform);

                if (!w.grounded)
                {
                    w.compression = 0f;
                    w.load = 0f;
                    w.slip = 0f;
                    continue;
                }

                grounded++;
                w.contact = hit.point;
                w.normal = hit.normal;
                w.compression = Mathf.Max(0f, w.length - (hit.distance - _wheelRadius));
            }

            float maxLoad = mass * g * 1.8f;
            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel w = _wheels[i];
                if (w == null || w.pivot == null || !w.grounded) continue;

                Vector3 pointVelocity = _body.GetPointVelocity(w.contact);
                // Bornée : une roue qui tape un trottoir à pleine vitesse ne fait pas décoller la voiture.
                float closing = Mathf.Clamp(-Vector3.Dot(pointVelocity, up), -3f, 3f);
                float damper = (closing > 0f ? _bump : _rebound) * closing;

                Wheel other = Partner(i);
                float antiRoll = other != null ? (w.front ? _antiRollFront : _antiRollRear) * _spring * (w.compression - other.compression) : 0f;

                float force = Mathf.Clamp(w.compression * _spring + damper + antiRoll, 0f, maxLoad);
                w.load = force;
                _body.AddForceAtPosition(up * force, w.origin);
            }

            // --- pneus : angle de dérive, adhérence qui sature, cercle d'adhérence
            Vector3 com = _body.worldCenterOfMass;
            float baseLoad = mass * g / Mathf.Max(1, _wheels.Length);
            float share = mass / Mathf.Max(1, _wheels.Length);
            int drivenCount = 0;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != null && !_wheels[i].front && _wheels[i].grounded) drivenCount++;
            }

            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel w = _wheels[i];
                if (w == null || w.pivot == null || !w.grounded || w.load <= 0f) continue;

                Vector3 pointVelocity = _body.GetPointVelocity(w.contact);
                Quaternion steerRotation = w.front ? Quaternion.AngleAxis(_steer, up) : Quaternion.identity;
                Vector3 tireForward = Vector3.ProjectOnPlane(steerRotation * forward, w.normal).normalized;
                Vector3 tireRight = Vector3.Cross(w.normal, tireForward);

                float lateral = Vector3.Dot(pointVelocity, tireRight);
                float longitudinal = Vector3.Dot(pointVelocity, tireForward);
                float slide = Mathf.Sqrt(lateral * lateral + longitudinal * longitudinal);

                float mu = (w.front ? _grip : _rearGrip) * (1f - _loadSensitivity * (w.load / baseLoad - 1f));
                if (_handbrake && !w.front && !_autopilot) mu *= _handbrakeRearGrip;
                float limit = Mathf.Max(0f, mu) * w.load;

                // Dérive : la courbe du pneu ; à l'arrêt (pas d'angle défini) un simple amortissement.
                float alpha = Mathf.Atan2(lateral, Mathf.Max(Mathf.Abs(longitudinal), 0.5f));
                float peak = (w.front ? _tirePeakFront : _tirePeakRear) * Mathf.Deg2Rad;
                float curve = -limit * TireCurve(alpha, peak);
                float damped = Mathf.Clamp(-lateral * share / dt * 0.6f, -limit, limit);
                float blend = Mathf.Clamp01((slide - 1.5f) / 3.5f);
                float side = Mathf.Lerp(damped, curve, blend);

                float along = 0f;
                if (!w.front && drivenCount > 0) along += drive / drivenCount;
                if (braking)
                {
                    float bias = w.front ? 0.66f : 0.34f;
                    along -= Mathf.Sign(longitudinal) * Mathf.Min(brake * bias * 0.5f, limit);
                }

                if (_handbrake && !w.front && !_autopilot)
                {
                    along -= Mathf.Sign(longitudinal) * Mathf.Min(0.35f * _brakeTotal * 0.5f, Mathf.Abs(longitudinal) * share / dt);
                }

                along -= Mathf.Abs(longitudinal) > 0.3f
                    ? Mathf.Sign(longitudinal) * _rollResistance / _wheels.Length
                    : longitudinal / 0.3f * _rollResistance / _wheels.Length;

                // Garée, ou arrêtée par son conducteur (feu, bouchon) : elle ne roule pas toute seule.
                if (!Driving || (_autopilot && _handbrake)) along -= Mathf.Clamp(longitudinal * share / dt, -limit, limit);

                // Cercle d'adhérence : le freinage passe d'abord ; le moteur prend ce que le virage laisse.
                if (braking || (_handbrake && !w.front))
                {
                    along = Mathf.Clamp(along, -limit, limit);
                    float room = Mathf.Sqrt(Mathf.Max(0f, limit * limit - along * along));
                    side = Mathf.Clamp(side, -room, room);
                }
                else
                {
                    side = Mathf.Clamp(side, -limit, limit);
                    float room = Mathf.Sqrt(Mathf.Max(0f, limit * limit - side * side * 0.85f));
                    along = Mathf.Clamp(along, -room, room);
                }

                w.slip = Mathf.Abs(side) >= limit * 0.98f ? Mathf.Abs(lateral) : Mathf.Abs(alpha) > peak * 1.3f ? Mathf.Abs(lateral) * 0.6f : 0f;
                if (braking && Mathf.Abs(along) >= limit * 0.98f) w.slip = Mathf.Max(w.slip, Mathf.Abs(longitudinal) * 0.5f);

                // Les forces des pneus s'appliquent un peu au-dessus du sol (centre de roulis) :
                // posées au ras du sol, elles faisaient basculer la voiture dans les virages.
                Vector3 application = w.contact + up * (Vector3.Dot(com - w.contact, up) * _rollCenter);
                _body.AddForceAtPosition(tireRight * side + tireForward * along, application);
            }

            if (grounded > 0)
            {
                Vector3 v = _body.linearVelocity;
                float sp = v.magnitude;
                _body.AddForce(-v * sp * _drag);
                _body.AddForce(-up * sp * sp * _downforce);
            }

            Stabilize(grounded, speed, up, forward);
            LimitSpeed();
            TrackSafety(dt, grounded);
            SweepPedestrians(speed, dt);
        }

        /// <summary>
        /// Le volant : braquage qui diminue avec la vitesse, retour au centre plus vif, et pour le
        /// joueur une aide au contre-braquage — les roues avant suivent la glisse de l'arrière.
        /// </summary>
        private void Steering(float speed, Vector3 up, Vector3 forward, Vector3 right, float dt)
        {
            float lockAngle = LockAt(speed);
            float assist = 0f;
            if (_occupied && _counterSteer > 0f && speed > 3f)
            {
                Vector3 flat = Vector3.ProjectOnPlane(_body.linearVelocity, up);
                if (flat.sqrMagnitude > 9f)
                {
                    float beta = Mathf.Atan2(Vector3.Dot(flat, right), Vector3.Dot(flat, forward)) * Mathf.Rad2Deg;
                    assist = Mathf.Clamp(beta * _counterSteer, -lockAngle, lockAngle) * (1f - 0.6f * Mathf.Abs(_steerInput));
                }
            }

            float target = _steerInput * lockAngle + assist;
            float rate = _steerRate * (Mathf.Abs(target) < Mathf.Abs(_steer) ? 1.6f : 1f);
            _steer = Mathf.MoveTowards(_steer, target, rate * dt);
        }

        /// <summary>
        /// Moteur et boîte automatique : couple selon le régime, six rapports, embrayage qui patine
        /// au démarrage, coupure au passage des rapports, frein moteur. Rend la poussée aux roues.
        /// </summary>
        private float Drivetrain(float speed, float dt)
        {
            float wheelRpm = Mathf.Abs(speed) / _wheelRadius * 60f / (2f * Mathf.PI);
            bool reverse = _throttle < -0.05f && speed < 0.8f;
            if (reverse) _gear = 0;
            else if (_gear == 0 && (_throttle > 0.05f || speed > 0.5f)) _gear = 1;

            int count = _gearRatios != null ? _gearRatios.Length : 0;
            float ratio = (_gear == 0 || count == 0 ? 3.3f : _gearRatios[Mathf.Clamp(_gear - 1, 0, count - 1)]) * _finalDrive;
            float rpm = wheelRpm * ratio;

            _pedal = Driving ? (_gear == 0 ? Mathf.Max(0f, -_throttle) : Mathf.Max(0f, _throttle)) : 0f;
            float launch = _idleRpm + (2600f - _idleRpm) * _pedal;
            float engine = Mathf.Max(rpm, Mathf.Abs(speed) < 6f && _gear <= 1 ? launch : _idleRpm);
            _engineRpm = Mathf.Lerp(_engineRpm, engine, 1f - Mathf.Exp(-14f * dt));
            _rpm = Mathf.Clamp01(_engineRpm / _redline);

            if (_shiftTimer > 0f)
            {
                _shiftTimer -= dt;
            }
            else if (_gear >= 1 && count > 0)
            {
                if (rpm > _redline * 0.93f && _gear < count)
                {
                    _gear++;
                    _shiftTimer = 0.2f;
                    _shiftDip = 1f;
                }
                else if (_gear > 1 && rpm < _redline * 0.38f)
                {
                    _gear--;
                    _shiftTimer = 0.12f;
                }
            }

            _shiftDip = Mathf.MoveTowards(_shiftDip, 0f, dt * 4f);
            if (!Driving) return 0f;

            float force = 0f;
            if (_shiftTimer <= 0f) force = Torque(_engineRpm) * ratio * 0.88f / _wheelRadius * _pedal;

            // Le pied levé : le moteur retient (pas en marche arrière, pas à l'arrêt).
            if (_pedal < 0.05f && _gear >= 1 && Mathf.Abs(speed) > 0.5f)
            {
                force -= _engineRpm / _redline * 0.035f * _body.mass * 9.81f * Mathf.Sign(speed);
            }

            if (_gear == 0)
            {
                force = -force;
                if (speed < -_reverseSpeed) force = 0f;
            }

            return force;
        }

        /// <summary>Le couple selon le régime : plein vers 5 000 tr/min, qui retombe après, coupé au rupteur.</summary>
        private float Torque(float rpm)
        {
            float x = rpm / _peakRpm;
            float t = x <= 1f ? 0.72f + 0.28f * Mathf.Sin(x * Mathf.PI * 0.5f) : 1f - 0.55f * Mathf.Pow(x - 1f, 1.2f);
            if (rpm > _redline) t *= Mathf.Max(0f, 1f - (rpm - _redline) / 200f);
            return _peakTorque * Mathf.Max(0f, t);
        }

        /// <summary>Courbe de pneu normalisée : montée jusqu'au pic de dérive, puis 90 % au-delà (glisse rattrapable).</summary>
        private static float TireCurve(float alpha, float peak)
        {
            float x = Mathf.Abs(alpha) / Mathf.Max(0.01f, peak);
            float y = x < 1f ? x * (2f - x) : 1f - 0.1f * Mathf.Min(1f, (x - 1f) * 0.5f);
            return alpha < 0f ? -y : y;
        }

        /// <summary>La roue de l'autre côté du même essieu (barre anti-roulis).</summary>
        private Wheel Partner(int index)
        {
            Wheel w = _wheels[index];
            float side = transform.InverseTransformPoint(w.pivot.position).x;
            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel o = _wheels[i];
                if (i == index || o == null || o.pivot == null || o.front != w.front) continue;
                float x = transform.InverseTransformPoint(o.pivot.position).x;
                if (Mathf.Sign(x) != Mathf.Sign(side)) return o;
            }

            return null;
        }

        /// <summary>
        /// Les aides : au sol, le roulis au-delà de 9° est rappelé fermement (une voiture ne se
        /// couche plus dans un virage serré ou sur un trottoir) ; le lacet que le volant ne demande
        /// pas est amorti (sauf au frein à main : on peut toujours la faire tourner).
        /// </summary>
        private void Stabilize(int grounded, float speed, Vector3 up, Vector3 forward)
        {
            float mass = _body.mass;
            if (grounded >= 2)
            {
                Vector3 r = transform.right;
                float roll = Mathf.Atan2(r.y, up.y) * Mathf.Rad2Deg;
                float excess = Mathf.Max(0f, Mathf.Abs(roll) - 9f);
                float rollRate = Vector3.Dot(_body.angularVelocity, forward);
                float k = 1800f * mass / 1380f;
                float torque = -Mathf.Sign(roll) * excess * k - rollRate * 0.35f * k * (excess > 0f ? 1f : 0.15f);
                _body.AddTorque(forward * torque);
            }

            if (grounded >= 3 && !_handbrake && Mathf.Abs(speed) > 4f && _stability > 0f)
            {
                float yawRate = Vector3.Dot(_body.angularVelocity, up);
                float wanted = speed * Mathf.Tan(_steer * Mathf.Deg2Rad) / Mathf.Max(1.5f, Wheelbase);
                _body.AddTorque(-up * (yawRate - wanted) * _stability * mass);
            }
        }

        private readonly Collider[] _sweep = new Collider[8];

        /// <summary>
        /// Un passant juste devant le pare-chocs est renversé AVANT le contact : sa capsule
        /// (cinématique, donc d'une masse infinie pour la physique) arrêterait la voiture net,
        /// comme un poteau.
        /// </summary>
        private void SweepPedestrians(float speed, float dt)
        {
            if (!_occupied || Mathf.Abs(speed) < 3f) return;

            float ahead = Mathf.Abs(speed) * dt * 3f + 0.4f;
            Vector3 center = transform.TransformPoint(new Vector3(0f, 0.9f, Mathf.Sign(speed) * (2.3f + ahead * 0.5f)));
            Vector3 half = new Vector3(1f, 0.8f, ahead * 0.5f + 0.2f);

            int count = Physics.OverlapBoxNonAlloc(center, half, _sweep, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                MocapWalker walker = _sweep[i] != null ? _sweep[i].GetComponentInParent<MocapWalker>() : null;
                if (walker != null && !walker.IsDown)
                {
                    walker.KnockOver(_body.linearVelocity);
                    if (_occupied && Mathf.Abs(ForwardSpeed) > 6f) Crimes.Report(Crime.Delit, walker.transform.position, walker.gameObject);
                }
            }
        }

        /// <summary>
        /// Une voiture garée, immobile, loin de celle qu'on conduit, ne calcule rien : elle
        /// devient cinématique. Elle se réveille quand la voiture du joueur approche — pour
        /// pouvoir être poussée, emboutie, et pas rester plantée comme un mur.
        /// </summary>
        private bool Park(float dt)
        {
            if (Driving)
            {
                _settledFor = 0f;
                if (_body.isKinematic) _body.isKinematic = false;
                return false;
            }

            DrivableCar driven = Driven;
            bool near = driven != null && driven != this &&
                        (driven.transform.position - transform.position).sqrMagnitude < 45f * 45f;

            if (_body.isKinematic)
            {
                if (!near) return true;
                _body.isKinematic = false;
                _settledFor = 0f;
                return false;
            }

            _settledFor = _body.linearVelocity.sqrMagnitude < 0.01f && _body.angularVelocity.sqrMagnitude < 0.01f
                ? _settledFor + dt
                : 0f;

            if (!near && _settledFor > 1.5f)
            {
                _body.isKinematic = true;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Garde-fou physique : jamais plus vite que ce qu'une voiture peut faire, jamais un bond
        /// vertical de fusée, jamais une toupie. Si malgré tout elle part (un choc impossible),
        /// elle revient à sa dernière position sûre.
        /// </summary>
        private void LimitSpeed()
        {
            Vector3 v = _body.linearVelocity;
            float cap = Mathf.Max(_maxSpeed * 1.6f, 20f);

            if (v.magnitude > cap * 2.5f || !IsFinite(v) || transform.position.y > _lastSafePosition.y + 40f)
            {
                // Partie en vrille (un choc impossible) : retour à la dernière position sûre.
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
                _body.position = _lastSafePosition;
                _body.rotation = _lastSafeRotation;
                transform.SetPositionAndRotation(_lastSafePosition, _lastSafeRotation);
                return;
            }

            if (v.magnitude > cap) v = v.normalized * cap;
            if (v.y > 8f) v.y = 8f;
            _body.linearVelocity = v;

            Vector3 w = _body.angularVelocity;
            if (w.magnitude > 6f) _body.angularVelocity = w.normalized * 6f;
        }

        private static bool IsFinite(Vector3 v)
        {
            return !(float.IsNaN(v.x) || float.IsNaN(v.y) || float.IsNaN(v.z) ||
                     float.IsInfinity(v.x) || float.IsInfinity(v.y) || float.IsInfinity(v.z));
        }

        private void TrackSafety(float dt, int grounded)
        {
            // Retournée et immobile : on la remet sur ses roues au bout de quelques secondes.
            if (Vector3.Dot(transform.up, Vector3.up) < 0.3f && _body.linearVelocity.magnitude < 2f)
            {
                _upsideDownFor += dt;
                if (_upsideDownFor > 2.5f)
                {
                    _upsideDownFor = 0f;
                    Recover();
                }
            }
            else
            {
                _upsideDownFor = 0f;
            }

            if (transform.position.y < -20f) Recover();

            _safeTimer += dt;
            if (grounded == _wheels.Length && _safeTimer > 1.5f && Vector3.Dot(transform.up, Vector3.up) > 0.9f)
            {
                _safeTimer = 0f;
                _lastSafePosition = transform.position + Vector3.up * 0.5f;
                _lastSafeRotation = transform.rotation;
            }
        }

        // ------------------------------------------------------------------ rendu et son

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            // Garée et endormie : ses roues sont déjà posées, rien à recalculer.
            if (!Driving && _body.isKinematic) return;
            if (Sleeping && !_occupied) return;

            UpdateWheels(ForwardSpeed, dt);
            if (_audio != null) _audio.Tick(dt);

            if (_brakeLight != null)
            {
                float target = !Driving ? 0f : Braking ? _brakeLightIntensity : _brakeLightIntensity * 0.25f;
                _brakeLight.intensity = Mathf.MoveTowards(_brakeLight.intensity, target, dt * 12f);
                _brakeLight.enabled = _brakeLight.intensity > 0.01f;
            }
        }

        private void UpdateWheels(float speed, float dt)
        {
            float travel = _travelUp + _travelDown;
            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel w = _wheels[i];
                if (w == null || w.pivot == null) continue;

                // Débattement : la roue suit le sol, d'après la compression calculée par la physique.
                float drop = w.grounded ? Mathf.Clamp(w.length - w.compression, 0f, travel) : travel;
                Vector3 wanted = transform.TransformPoint(w.rest + Vector3.up * (_travelUp - drop));
                w.pivot.position = Vector3.Lerp(w.pivot.position, wanted, 1f - Mathf.Exp(-30f * dt));
                w.pivot.rotation = transform.rotation * Quaternion.Euler(0f, w.front ? _steer : 0f, 0f);

                // Frein à main : les roues arrière bloquées ne tournent plus.
                float spin = _handbrake && !w.front ? 0f : speed / _wheelRadius * Mathf.Rad2Deg;
                w.angle = Mathf.Repeat(w.angle + spin * dt, 360f);
                if (w.spin != null) w.spin.localRotation = Quaternion.Euler(w.angle, 0f, 0f);
            }
        }

        private void OnCollisionEnter(Collision collision)
        {
            float impact = collision.relativeVelocity.magnitude;

            MocapWalker walker = collision.collider.GetComponentInParent<MocapWalker>();
            if (walker != null && impact > 3f)
            {
                bool fresh = !walker.IsDown;
                walker.KnockOver(_body.linearVelocity);
                if (fresh && _occupied && impact > 6f) Crimes.Report(Crime.Delit, walker.transform.position, walker.gameObject);
            }

            if (_audio != null && impact >= 2.5f) _audio.Crash(impact, collision.contactCount > 0 ? collision.GetContact(0).point : transform.position);
        }

        // ------------------------------------------------------------------ son

        private CarAudio _audio;

        /// <summary>Le son de la voiture : moteur, pneus, klaxon, chocs (voir <see cref="CarAudio"/>).</summary>
        private void SetupAudio()
        {
            _audio = GetComponent<CarAudio>();
            if (_audio == null) _audio = gameObject.AddComponent<CarAudio>();
            _audio.Bind(this, _engine, _tires, _horn, _impacts);
        }
    }
}
