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
        [SerializeField, Min(0.05f)] private float _travelUp = 0.22f;
        [SerializeField, Min(0.05f)] private float _travelDown = 0.22f;
        [SerializeField, Min(1000f)] private float _spring = 36000f;
        [SerializeField, Min(100f)] private float _damper = 3800f;
        [SerializeField] private Vector3 _centerOfMass = new Vector3(0f, 0.38f, 0.12f);

        [Header("Moteur et freins")]
        [SerializeField, Min(1f)] private float _maxSpeed = 32f;
        [SerializeField, Min(1f)] private float _reverseSpeed = 9f;
        [SerializeField, Min(100f)] private float _engineForce = 9800f;
        [SerializeField, Min(100f)] private float _brakeForce = 15000f;
        [SerializeField, Min(0f)] private float _rollingResistance = 55f;
        [SerializeField, Min(0f)] private float _airDrag = 0.42f;

        [Header("Direction et adherence")]
        [SerializeField, Range(5f, 45f)] private float _maxSteer = 34f;
        [SerializeField, Range(2f, 30f)] private float _highSpeedSteer = 9f;
        [SerializeField, Min(0.5f)] private float _steerSpeed = 3.2f;
        [SerializeField, Min(0.1f)] private float _grip = 1.25f;
        [SerializeField, Range(0.05f, 1f)] private float _handbrakeRearGrip = 0.3f;
        [SerializeField, Min(0f)] private float _downforce = 3.5f;

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
        private bool _occupied;
        private bool _autopilot;
        private float _upsideDownFor;
        private Vector3 _lastSafePosition;
        private Quaternion _lastSafeRotation;
        private float _safeTimer;
        private float _settledFor;

        private static readonly List<DrivableCar> _all = new List<DrivableCar>();
        private static AudioClip _engineClip;
        private static AudioClip _tireClip;
        private static AudioClip _hornClip;
        private static AudioClip _thumpClip;

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
            get
            {
                float speed01 = Mathf.Clamp01(Mathf.Abs(ForwardSpeed) / _maxSpeed);
                return Mathf.Lerp(_maxSteer, _highSpeedSteer, speed01);
            }
        }

        public float MaxSpeed { get { return _maxSpeed; } }

        /// <summary>Distance entre les essieux (mètres), mesurée sur les roues.</summary>
        public float Wheelbase
        {
            get
            {
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

                if (_engine != null)
                {
                    if (value && !_engine.isPlaying) _engine.Play();
                    if (!value) _engine.Stop();
                }

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

                if (_engine != null)
                {
                    if (value && !_engine.isPlaying) _engine.Play();
                    if (!value) _engine.Stop();
                }

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
            if (_horn == null) return;
            if (on && !_horn.isPlaying) _horn.Play();
            if (!on && _horn.isPlaying) _horn.Stop();
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

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            if (Sleeping && !_occupied) return;
            if (Park(dt)) return;

            float speed = ForwardSpeed;
            float speed01 = Mathf.Clamp01(Mathf.Abs(speed) / _maxSpeed);

            float steerLimit = Mathf.Lerp(_maxSteer, _highSpeedSteer, speed01);
            _steer = Mathf.MoveTowards(_steer, _steerInput * steerLimit, _steerSpeed * steerLimit * dt);

            int grounded = 0;
            int driven = 0;
            for (int i = 0; i < _wheels.Length; i++)
            {
                if (_wheels[i] != null && !_wheels[i].front) driven++;
            }

            driven = Mathf.Max(1, driven);

            // Gaz dans le sens de la marche = moteur ; à contre-sens = frein, puis marche
            // arrière une fois presque arrêté. Comme toutes les voitures de jeu.
            bool braking = (_throttle > 0.05f && speed < -0.6f) || (_throttle < -0.05f && speed > 0.6f);
            float drive = 0f;
            if (!braking && !_handbrake)
            {
                if (_throttle > 0f && speed < _maxSpeed) drive = _throttle * _engineForce * (1f - Mathf.Clamp01(speed / _maxSpeed) * 0.55f);
                if (_throttle < 0f && speed > -_reverseSpeed) drive = _throttle * _engineForce * 0.55f;
                drive *= 1f - _shiftDip;
            }

            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel w = _wheels[i];
                if (w == null || w.pivot == null) continue;

                Vector3 origin = transform.TransformPoint(w.rest + Vector3.up * _travelUp);
                Vector3 down = -transform.up;
                float reach = _travelUp + _travelDown + _wheelRadius;

                RaycastHit hit;
                w.grounded = Physics.Raycast(origin, down, out hit, reach, ~0, QueryTriggerInteraction.Ignore) &&
                             !hit.collider.transform.IsChildOf(transform);

                if (!w.grounded)
                {
                    w.load = 0f;
                    w.slip = 0f;
                    continue;
                }

                grounded++;

                // --- suspension
                float spring = hit.distance - _wheelRadius;
                Vector3 pointVelocity = _body.GetPointVelocity(hit.point);
                // Bornée : une roue qui tape un trottoir, un poteau ou le toit d'une autre voiture
                // à pleine vitesse donnait une force d'amortisseur énorme — la voiture décollait,
                // et plus rien ne l'arrêtait (« parti dans le ciel à 20 000 km/h »).
                float compressionSpeed = Mathf.Clamp(-Vector3.Dot(pointVelocity, transform.up), -4f, 4f);
                float force = (Mathf.Max(0f, w.length - spring)) * _spring + compressionSpeed * _damper;
                float maxForce = _body.mass * -Physics.gravity.y * 2.5f;
                force = Mathf.Clamp(force, 0f, maxForce);
                w.load = force;
                _body.AddForceAtPosition(transform.up * force, origin);

                // --- pneus
                Quaternion steerRotation = w.front ? Quaternion.AngleAxis(_steer, transform.up) : Quaternion.identity;
                Vector3 forward = Vector3.ProjectOnPlane(steerRotation * transform.forward, hit.normal).normalized;
                Vector3 right = Vector3.Cross(hit.normal, forward);

                float lateral = Vector3.Dot(pointVelocity, right);
                float longitudinal = Vector3.Dot(pointVelocity, forward);

                float grip = _grip;
                if (_handbrake && !w.front) grip *= _handbrakeRearGrip;
                float limit = force * grip;

                // L'impulsion qui annulerait le glissement latéral, bornée par l'adhérence :
                // au-delà, le pneu glisse (et crisse).
                float share = _body.mass / Mathf.Max(1, _wheels.Length);
                float wanted = -lateral * share / dt;
                float side = Mathf.Clamp(wanted, -limit, limit);
                w.slip = Mathf.Abs(wanted) > limit ? Mathf.Abs(lateral) : 0f;

                float along = 0f;
                if (!w.front) along += drive / driven;
                if (braking) along -= Mathf.Sign(longitudinal) * _brakeForce / _wheels.Length;
                if (_handbrake && !w.front) along -= Mathf.Sign(longitudinal) * Mathf.Min(_brakeForce * 0.3f, Mathf.Abs(longitudinal) * share / dt);
                along -= longitudinal * _rollingResistance / _wheels.Length;
                // Garée, ou arrêtée par son conducteur (feu, bouchon) : elle ne roule pas toute seule.
                if (!Driving || (_autopilot && _handbrake)) along -= Mathf.Clamp(longitudinal * share / dt, -limit, limit);
                along = Mathf.Clamp(along, -limit * 1.1f, limit * 1.1f);

                _body.AddForceAtPosition(right * side + forward * along, hit.point);
            }

            if (grounded > 0)
            {
                Vector3 v = _body.linearVelocity;
                _body.AddForce(-v * v.magnitude * _airDrag);
                _body.AddForce(-transform.up * v.magnitude * v.magnitude * _downforce);
            }

            LimitSpeed();
            TrackSafety(dt, grounded);
            SweepPedestrians(speed, dt);
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

            float speed = ForwardSpeed;
            UpdateWheels(speed, dt);
            UpdateGearbox(speed, dt);
            UpdateAudio(speed);

            if (_brakeLight != null)
            {
                bool braking = _handbrake || (_throttle > 0.05f && speed < -0.6f) || (_throttle < -0.05f && speed > 0.6f);
                float target = !Driving ? 0f : braking ? _brakeLightIntensity : _brakeLightIntensity * 0.25f;
                _brakeLight.intensity = Mathf.MoveTowards(_brakeLight.intensity, target, dt * 12f);
                _brakeLight.enabled = _brakeLight.intensity > 0.01f;
            }
        }

        private void UpdateWheels(float speed, float dt)
        {
            for (int i = 0; i < _wheels.Length; i++)
            {
                Wheel w = _wheels[i];
                if (w == null || w.pivot == null) continue;

                // Débattement : la roue suit le sol (reposée par le rayon de la physique).
                Vector3 origin = transform.TransformPoint(w.rest + Vector3.up * _travelUp);
                float travel = _travelUp + _travelDown;
                RaycastHit hit;
                float drop = travel;
                if (Physics.Raycast(origin, -transform.up, out hit, travel + _wheelRadius, ~0, QueryTriggerInteraction.Ignore) &&
                    !hit.collider.transform.IsChildOf(transform))
                {
                    drop = Mathf.Clamp(hit.distance - _wheelRadius, 0f, travel);
                }

                Vector3 wanted = transform.TransformPoint(w.rest + Vector3.up * (_travelUp - drop));
                w.pivot.position = Vector3.Lerp(w.pivot.position, wanted, 1f - Mathf.Exp(-30f * dt));
                w.pivot.rotation = transform.rotation * Quaternion.Euler(0f, w.front ? _steer : 0f, 0f);

                // Frein à main : les roues arrière bloquées ne tournent plus.
                float spin = _handbrake && !w.front ? 0f : speed / _wheelRadius * Mathf.Rad2Deg;
                w.angle = Mathf.Repeat(w.angle + spin * dt, 360f);
                if (w.spin != null) w.spin.localRotation = Quaternion.Euler(w.angle, 0f, 0f);
            }
        }

        /// <summary>Cinq rapports : le régime grimpe dans chacun, retombe au passage du suivant.</summary>
        private void UpdateGearbox(float speed, float dt)
        {
            float s = Mathf.Abs(speed);
            float[] tops = { 0.2f, 0.38f, 0.58f, 0.8f, 1.05f };

            if (speed < -0.5f)
            {
                _gear = 0;
            }
            else
            {
                int gear = 1;
                float fraction = s / _maxSpeed;
                while (gear < tops.Length && fraction > tops[gear - 1]) gear++;
                if (gear != _gear && _gear != 0 && gear > _gear) _shiftDip = 1f;
                _gear = gear;
            }

            float low = _gear <= 1 ? 0f : tops[Mathf.Max(0, _gear - 2)];
            float high = _gear == 0 ? _reverseSpeed / _maxSpeed : tops[Mathf.Clamp(_gear - 1, 0, tops.Length - 1)];
            float within = Mathf.InverseLerp(low, high, s / _maxSpeed);

            float idle = 0.12f;
            float load = Mathf.Abs(_throttle);
            float target = Mathf.Lerp(idle, 1f, within) * (0.8f + 0.2f * load);
            if (Driving && load > 0.1f && s < 1f) target = Mathf.Max(target, 0.3f + 0.3f * load);

            _rpm = Mathf.Lerp(_rpm, target, 1f - Mathf.Exp(-8f * dt));
            _shiftDip = Mathf.MoveTowards(_shiftDip, 0f, dt * 4f);
        }

        private void UpdateAudio(float speed)
        {
            if (_engine != null && Driving)
            {
                _engine.pitch = Mathf.Lerp(0.55f, 1.9f, _rpm) * (1f - _shiftDip * 0.12f);
                _engine.volume = Mathf.Lerp(0.28f, 0.62f, Mathf.Abs(_throttle)) * (0.75f + 0.25f * _rpm) *
                                 (_occupied ? 1f : 0.55f);
            }

            if (_tires != null)
            {
                float slip = 0f;
                for (int i = 0; i < _wheels.Length; i++)
                {
                    if (_wheels[i] != null && _wheels[i].grounded) slip = Mathf.Max(slip, _wheels[i].slip);
                }

                float volume = Mathf.Clamp01((slip - 2f) / 6f) * 0.7f;
                if (_handbrake && Mathf.Abs(speed) > 4f) volume = Mathf.Max(volume, 0.45f);
                _tires.volume = Mathf.MoveTowards(_tires.volume, volume, Time.deltaTime * 3f);
                if (_tires.volume > 0.01f && !_tires.isPlaying) _tires.Play();
                if (_tires.volume <= 0.01f && _tires.isPlaying) _tires.Stop();
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

            if (_impacts == null || impact < 2.5f) return;
            _impacts.pitch = UnityEngine.Random.Range(0.8f, 1.1f);
            _impacts.PlayOneShot(_thumpClip, Mathf.Clamp01(impact / 14f));
        }

        // ------------------------------------------------------------------ sons fabriqués

        private void SetupAudio()
        {
            BuildClips();

            if (_engine != null)
            {
                _engine.clip = _engineClip;
                _engine.loop = true;
                _engine.playOnAwake = false;
                _engine.spatialBlend = 0.85f;
            }

            if (_tires != null)
            {
                _tires.clip = _tireClip;
                _tires.loop = true;
                _tires.playOnAwake = false;
                _tires.volume = 0f;
                _tires.spatialBlend = 0.85f;
            }

            if (_horn != null)
            {
                _horn.clip = _hornClip;
                _horn.loop = true;
                _horn.playOnAwake = false;
                _horn.spatialBlend = 0.8f;
            }

            if (_impacts != null)
            {
                _impacts.playOnAwake = false;
                _impacts.spatialBlend = 0.85f;
            }
        }

        private static void BuildClips()
        {
            const int rate = 22050;
            System.Random random = new System.Random(71);

            if (_engineClip == null)
            {
                // Un quatre cylindres au ralenti : une fondamentale à 40 Hz (deux explosions par
                // tour à 1200 tr/min), ses harmoniques, un souffle d'échappement. Une seconde
                // entière : toutes les fréquences y font un nombre entier de périodes, la boucle
                // ne claque pas.
                float[] data = new float[rate];
                float noise = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float v = 0f;
                    v += Mathf.Sin(2f * Mathf.PI * 40f * t) * 0.55f;
                    v += Mathf.Sin(2f * Mathf.PI * 80f * t + 0.6f) * 0.32f;
                    v += Mathf.Sin(2f * Mathf.PI * 120f * t + 1.1f) * 0.18f;
                    v += Mathf.Sin(2f * Mathf.PI * 160f * t + 0.3f) * 0.12f;
                    v += Mathf.Sin(2f * Mathf.PI * 20f * t) * 0.2f;
                    noise = noise * 0.93f + ((float)random.NextDouble() * 2f - 1f) * 0.07f;
                    float pulse = 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(2f * Mathf.PI * 40f * t));
                    data[i] = Mathf.Clamp((v * 0.5f + noise * 1.6f) * pulse, -1f, 1f) * 0.8f;
                }

                _engineClip = AudioClip.Create("Moteur (synthese)", data.Length, 1, rate, false);
                _engineClip.SetData(data, 0);
            }

            if (_tireClip == null)
            {
                float[] data = new float[rate];
                float a = 0f, b = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    float white = (float)random.NextDouble() * 2f - 1f;
                    a = a * 0.6f + white * 0.4f;
                    b = b * 0.97f + a * 0.03f;
                    float t = i / (float)rate;
                    float squeal = Mathf.Sin(2f * Mathf.PI * 900f * t + Mathf.Sin(2f * Mathf.PI * 7f * t) * 3f) * 0.25f;
                    data[i] = Mathf.Clamp((a - b) * 0.8f + squeal, -1f, 1f) * 0.6f;
                }

                _tireClip = AudioClip.Create("Pneus (synthese)", data.Length, 1, rate, false);
                _tireClip.SetData(data, 0);
            }

            if (_hornClip == null)
            {
                // Deux tons (fa dièse et la dièse), un peu carrés : le klaxon des vieilles berlines.
                float[] data = new float[rate / 2];
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float v = Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 370f * t)) * 0.5f +
                              Mathf.Sign(Mathf.Sin(2f * Mathf.PI * 466f * t)) * 0.5f;
                    data[i] = v * 0.28f;
                }

                _hornClip = AudioClip.Create("Klaxon (synthese)", data.Length, 1, rate, false);
                _hornClip.SetData(data, 0);
            }

            if (_thumpClip == null)
            {
                float[] data = new float[rate * 2 / 5];
                float low = 0f;
                for (int i = 0; i < data.Length; i++)
                {
                    float t = i / (float)rate;
                    float white = (float)random.NextDouble() * 2f - 1f;
                    low = low * 0.9f + white * 0.1f;
                    float envelope = Mathf.Exp(-t * 14f);
                    float body = Mathf.Sin(2f * Mathf.PI * (70f - t * 60f) * t) * 0.9f;
                    float rattle = white * Mathf.Exp(-t * 30f) * 0.5f;
                    data[i] = Mathf.Clamp((body + low * 2f) * envelope + rattle, -1f, 1f) * 0.9f;
                }

                _thumpClip = AudioClip.Create("Choc (synthese)", data.Length, 1, rate, false);
                _thumpClip.SetData(data, 0);
            }
        }
    }
}
