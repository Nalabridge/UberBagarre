using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le conducteur d'une voiture de la circulation. Il ne déplace pas la voiture : il tient
    /// le volant et les pédales d'une vraie <see cref="DrivableCar"/>, la même que celle du
    /// joueur (roulis, poids, freinage, chocs).
    ///
    /// Ce qu'il sait faire :
    /// - **Rester dans sa voie** : contrôleur de Stanley sur une route préparée (virages
    ///   arrondis) — il ne coupe plus les virages, il ne mord plus sur la voie d'en face.
    /// - **Doser** : c'est le régulateur (<see cref="TrafficFlow"/>) qui lui donne
    ///   l'accélération voulue — suivre à bonne distance, ralentir avant les virages, céder au
    ///   carrefour. Lui la traduit en gaz et en frein progressifs.
    /// - **Voir** : devant lui, le long de SA route (pas tout droit), il cherche ce qui n'est pas
    ///   dans la circulation : le joueur à pied ou au volant, un passant, une voiture garée de
    ///   travers. Il freine, klaxonne si on le bloque, et contourne un obstacle immobile quand
    ///   la voie d'en face est libre.
    /// - **Faire demi-tour** au bout d'une impasse, en trois manœuvres.
    /// - **Se tirer d'affaire** : poussé hors de sa route ou retourné, il est replacé quand
    ///   personne ne regarde ; loin du joueur, la physique s'endort et il glisse sur sa route.
    ///
    /// Le joueur peut lui prendre sa voiture (E sur la portière quand elle est arrêtée) : le
    /// conducteur descend et s'en va, la voiture est à lui.
    /// </summary>
    [RequireComponent(typeof(DrivableCar))]
    public class TrafficDriver : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Points de la boucle (monde), deja decales sur la voie de droite.")]
        private Vector3[] _path = new Vector3[0];

        [SerializeField] private int _startIndex;
        [SerializeField, Min(2f)] private float _cruise = 11f;

        [SerializeField]
        [Tooltip("Au-dela, la physique s'endort et la voiture glisse sur sa route.")]
        private float _sleepDistance = 150f;

        [SerializeField]
        [Tooltip("Le corps du conducteur (cache quand le joueur prend la voiture).")]
        private GameObject _driverBody;

        /// <summary>Celui qu'on évite en priorité (le joueur).</summary>
        public static Transform Player { get; set; }

        private DrivableCar _car;
        private Rigidbody _body;
        private TrafficFlow _flow;
        private TrafficAgent _agent;
        private readonly TurnaroundManeuver _maneuver = new TurnaroundManeuver();

        private Vector3 _centerOffset;
        private float _halfLength = 2.3f;
        private float _halfWidth = 0.95f;
        private float _wheelbase = 2.7f;
        private float _groundOffset;

        private bool _asleep;
        private float _sensorTimer;
        private float _turningFor;
        private float _lostFor;
        private float _jammedFor;
        private float _reverseFor;
        private float _honkUntil;
        private float _nextHonk;
        private bool _playerAhead;
        private bool _obstacleFixed;
        private Collider _obstacle;
        private Transform _passing;

        // Contournement d'un obstacle immobile par la voie d'en face.
        private float _passUntilS = -1f;
        private float _offset;

        private readonly Collider[] _hits = new Collider[24];
        private readonly RaycastHit[] _rays = new RaycastHit[8];

        public void SetPath(Vector3[] path, int start, float cruise)
        {
            _path = path;
            _startIndex = start;
            _cruise = cruise;
        }

        private void Awake()
        {
            _car = GetComponent<DrivableCar>();
            _body = GetComponent<Rigidbody>();

            bool any;
            Bounds bounds = CityRules.RendererBounds(transform, out any);
            if (any)
            {
                Vector3 local = transform.InverseTransformPoint(bounds.center);
                _centerOffset = new Vector3(local.x, 0f, local.z);
                Vector3 f = transform.forward;
                Vector3 r = transform.right;
                _halfLength = Mathf.Max(1.6f, Mathf.Abs(f.x) * bounds.extents.x + Mathf.Abs(f.y) * bounds.extents.y + Mathf.Abs(f.z) * bounds.extents.z);
                _halfWidth = Mathf.Clamp(Mathf.Abs(r.x) * bounds.extents.x + Mathf.Abs(r.y) * bounds.extents.y + Mathf.Abs(r.z) * bounds.extents.z, 0.7f, 1.3f);
            }

            _maneuver.Front = _halfLength;
            _maneuver.Rear = _halfLength;
            _maneuver.HalfWidth = _halfWidth;
        }

        private void Start()
        {
            if (_path == null || _path.Length < 4)
            {
                enabled = false;
                return;
            }

            _wheelbase = _car.Wheelbase;
            _car.Kind = DrivableCar.Access.Circulation;
            _flow = TrafficSystem.Join(_path, _cruise);
            if (_flow == null)
            {
                enabled = false;
                return;
            }

            _agent = new TrafficAgent
            {
                Id = GetInstanceID(),
                HalfLength = _halfLength,
                Cruise = _cruise,
                Owner = this
            };

            // La hauteur du pivot au-dessus de la route (là où le constructeur l'a posée).
            Vector3 start = _path[Mathf.Abs(_startIndex) % _path.Length];
            _groundOffset = Mathf.Clamp(transform.position.y - start.y, -0.5f, 1f);

            _agent.S = _flow.Track.Project(start, 0f, -1f);
            PlaceAt(_agent.S);
            _flow.Add(_agent);
            _car.Autopilot = true;
            _sensorTimer = Random.value * 0.1f;
        }

        private void OnDisable()
        {
            if (_flow != null && _agent != null) _flow.Remove(_agent);
            if (_car != null && !_car.Occupied) _car.Autopilot = false;
        }

        private void OnEnable()
        {
            if (_flow != null && _agent != null) _flow.Add(_agent);
        }

        /// <summary>Le joueur sort le conducteur : il disparaît, la voiture s'arrête là.</summary>
        public void Evict()
        {
            if (_driverBody != null) _driverBody.SetActive(false);
            if (_asleep) Wake();
            _car.Sleeping = false;
            _car.Autopilot = false;
            _car.SetInput(0f, 0f, true);
            enabled = false;
        }

        // ================================================================== conduite

        private Vector3 Center
        {
            get
            {
                Vector3 c = transform.TransformPoint(_centerOffset);
                return c;
            }
        }

        private Vector3 Forward
        {
            get
            {
                Vector3 f = transform.forward;
                f.y = 0f;
                return f.sqrMagnitude > 1e-4f ? f.normalized : Vector3.forward;
            }
        }

        private void FixedUpdate()
        {
            if (_flow == null) return;

            // Le joueur a pris la voiture : le conducteur s'en va.
            if (_car.Occupied)
            {
                if (_driverBody != null) _driverBody.SetActive(false);
                _car.Sleeping = false;
                _car.Autopilot = false;
                enabled = false;
                return;
            }

            float dt = Time.fixedDeltaTime;
            TrafficTrack track = _flow.Track;
            Vector3 center = Center;

            // --- loin du joueur : le régulateur conduit, la voiture suit sa position.
            if (ViewerDistance(center) > _sleepDistance)
            {
                Sleep();
                Vector3 p = track.Point(_agent.S);
                Vector3 t = track.Tangent(_agent.S);
                Quaternion rotation = Quaternion.LookRotation(t, Vector3.up);
                Vector3 pivot = p - rotation * _centerOffset + Vector3.up * _groundOffset;
                _body.MovePosition(pivot);
                _body.MoveRotation(Quaternion.Slerp(_body.rotation, rotation, 1f - Mathf.Exp(-6f * dt)));
                return;
            }

            if (_asleep && !Wake()) return;

            float speed = _car.ForwardSpeed;
            _agent.Speed = speed;

            if (_agent.Turning >= 0)
            {
                Turn(track, center, speed, dt);
                return;
            }

            _agent.S = track.Project(center, _agent.S, 10f);

            int turn = _flow.TurnaroundAt(_agent);
            if (turn >= 0 && speed < 2f)
            {
                _agent.Turning = turn;
                _agent.S = track.Turnarounds[turn].Start;
                _maneuver.Reset();
                _turningFor = 0f;
                Turn(track, center, speed, dt);
                return;
            }

            Sense(track, speed, dt);
            Recover(track, center, speed, dt);
            if (_reverseFor > 0f)
            {
                _reverseFor -= dt;
                _car.SetInput(-0.5f, 0f, false);
                return;
            }

            // --- volant
            UpdatePass(track);
            float angle = TrafficSteering.Angle(track, _agent.S, center, Forward, speed, _wheelbase, _offset);
            float steer = Mathf.Clamp(angle / Mathf.Max(5f, _car.SteerLimit), -1f, 1f);

            // --- pédales
            float desired = _agent.Desired;
            // En contournant par la voie d'en face : au pas.
            if (Mathf.Abs(_offset) > 0.3f) desired = Mathf.Min(desired, (4f - speed) * 1.2f);
            if (_agent.ObstacleGap < 1.2f && speed > 0.3f) desired = -7f;

            float throttle;
            bool hold = false;
            if (desired <= 0.05f && speed < 0.35f)
            {
                throttle = 0f;
                hold = true;
            }
            else if (desired >= 0f)
            {
                // Le moteur donne ~6,5 m/s² à fond ; un peu de gaz pour les frottements.
                throttle = Mathf.Clamp(desired / 6.5f + 0.03f + speed * 0.006f, 0f, 1f);
            }
            else if (desired > -0.35f)
            {
                throttle = 0f;
            }
            else
            {
                // Frein proportionnel (~11 m/s² à fond).
                throttle = Mathf.Clamp(desired / 11f, -1f, -0.06f);
            }

            _car.SetInput(throttle, steer, hold);
            Honk();
        }

        // ------------------------------------------------------------------ demi-tour

        private void Turn(TrafficTrack track, Vector3 center, float speed, float dt)
        {
            TrafficTrack.Turnaround turn = track.Turnarounds[_agent.Turning];
            _turningFor += dt;

            float x, y;
            turn.Local(center, out x, out y);
            Vector3 f = Forward;
            float hx = f.x * turn.AxisX.x + f.z * turn.AxisX.z;
            float hy = f.x * turn.AxisY.x + f.z * turn.AxisY.z;
            float heading = Mathf.Atan2(hy, hx);
            if (heading < -Mathf.PI * 0.5f) heading += 2f * Mathf.PI;

            bool ahead = Blocked(1f);
            bool behind = Blocked(-1f);

            float target, steer;
            _maneuver.Step(turn, x, y, heading, speed, dt, ahead, behind, out target, out steer);

            bool giveUp = _maneuver.Swings > 9 || _turningFor > 45f;
            if (_maneuver.Current == TurnaroundManeuver.Phase.Done || (giveUp && !Visible(center)))
            {
                float s = track.Project(center, turn.Rejoin - 2f, 9f);
                if (giveUp)
                {
                    s = turn.Rejoin;
                    PlaceAt(s);
                }

                _flow.FinishTurn(_agent, s);
                _turningFor = 0f;
                return;
            }

            // Vitesse de manœuvre : un filet de gaz, freinage franc à l'arrêt voulu.
            float error = target - speed;
            float throttle;
            bool hold = false;
            if (Mathf.Abs(target) < 0.05f)
            {
                throttle = Mathf.Abs(speed) > 0.2f ? -Mathf.Sign(speed) * 0.6f : 0f;
                hold = Mathf.Abs(speed) <= 0.2f;
            }
            else
            {
                throttle = Mathf.Clamp(Mathf.Sign(target) * 0.12f + error * 0.35f, -0.6f, 0.6f);
                // Changer de sens : d'abord s'arrêter.
                if (Mathf.Sign(target) != Mathf.Sign(speed) && Mathf.Abs(speed) > 0.3f) throttle = -Mathf.Sign(speed) * 0.6f;
            }

            // + vers la voie de retour en avançant ; le volant de Unity : - à gauche.
            float wheel = -steer * turn.Side;
            _car.SetInput(throttle, wheel, hold);
        }

        /// <summary>Un mur, un poteau, quelqu'un, à moins d'un mètre devant (ou derrière) le pare-chocs.</summary>
        private bool Blocked(float direction)
        {
            Vector3 f = Forward;
            Vector3 origin = Center + Vector3.up * 0.7f + f * (direction * (_halfLength - 0.3f));
            int count = Physics.BoxCastNonAlloc(origin, new Vector3(_halfWidth * 0.9f, 0.35f, 0.1f), f * direction, _rays,
                Quaternion.LookRotation(f), 1.1f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _rays[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (_rays[i].distance <= 0f) continue;
                return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ capteurs

        /// <summary>
        /// Regarde devant, le long de la route (pas tout droit : dans un virage, la route tourne),
        /// pour ce qui n'est pas dans la circulation : le joueur, un passant, une voiture garée.
        /// </summary>
        private void Sense(TrafficTrack track, float speed, float dt)
        {
            _sensorTimer -= dt;
            if (_sensorTimer > 0f) return;
            _sensorTimer = 0.1f;

            float reach = Mathf.Min(40f, 5f + speed * speed / (2f * 2.2f) + Mathf.Max(0f, speed) * 1.2f);
            float front = _agent.S + _halfLength;
            float nearest = float.MaxValue;
            float nearestSpeed = 0f;
            bool player = false;
            Collider found = null;

            for (float d = 1.2f; d < reach; d += 2.4f)
            {
                float s = front + d;
                Vector3 tangent = track.Tangent(s);
                Vector3 p = track.Point(s) + Vector3.up * 0.9f;
                // Le couloir suit le décalage de contournement.
                Vector3 right = new Vector3(tangent.z, 0f, -tangent.x);
                p += right * _offset;

                int count = Physics.OverlapBoxNonAlloc(p, new Vector3(_halfWidth + 0.15f, 0.8f, 1.25f), _hits,
                    Quaternion.LookRotation(tangent), ~0, QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider c = _hits[i];
                    if (c == null || c.transform.IsChildOf(transform)) continue;
                    if (_passUntilS >= 0f && _passing != null && c.transform.IsChildOf(_passing)) continue;

                    Vector3 velocity;
                    bool isPlayer;
                    if (!Moving(c, out velocity, out isPlayer)) continue;

                    float gap = d - 1.25f;
                    if (gap >= nearest) continue;
                    nearest = gap;
                    nearestSpeed = Vector3.Dot(velocity, tangent);
                    player = isPlayer;
                    found = c;
                }

                if (nearest < float.MaxValue) break;
            }

            _agent.ObstacleGap = Mathf.Max(0f, nearest);
            _agent.ObstacleSpeed = nearestSpeed;
            _playerAhead = player;
            _obstacle = found;
            // Immobile, et pas une voiture de la circulation (celle-là repartira : on l'attend).
            TrafficDriver other = found != null ? found.GetComponentInParent<TrafficDriver>() : null;
            _obstacleFixed = found != null && Mathf.Abs(nearestSpeed) < 0.3f && !player && (other == null || !other.enabled);
        }

        /// <summary>L'objet entier auquel appartient un collider (la voiture, la personne).</summary>
        private static Transform Entity(Collider c)
        {
            DrivableCar car = c.GetComponentInParent<DrivableCar>();
            if (car != null) return car.transform;
            if (c.attachedRigidbody != null) return c.attachedRigidbody.transform;
            return c.transform;
        }

        /// <summary>Ce qui compte comme obstacle : quelqu'un, ou un véhicule ; pas le décor (la route est libre par construction).</summary>
        private static bool Moving(Collider c, out Vector3 velocity, out bool isPlayer)
        {
            velocity = Vector3.zero;
            isPlayer = Player != null && c.transform.IsChildOf(Player);

            DrivableCar car = c.GetComponentInParent<DrivableCar>();
            if (car != null)
            {
                if (car.Body != null) velocity = car.Body.linearVelocity;
                if (car == DrivableCar.Driven) isPlayer = true;
                return true;
            }

            CharacterController controller = c as CharacterController;
            if (controller != null)
            {
                velocity = controller.velocity;
                return true;
            }

            if (isPlayer) return true;
            if (c.GetComponentInParent<MocapWalker>() != null) return true;
            if (c.GetComponentInParent<Combatant>() != null) return true;

            Rigidbody body = c.attachedRigidbody;
            if (body != null && !body.isKinematic && body.mass > 30f)
            {
                velocity = body.linearVelocity;
                return true;
            }

            return false;
        }

        /// <summary>
        /// Un obstacle immobile qui n'est pas dans la circulation (une voiture garée en travers,
        /// la voiture abandonnée du joueur) : après quelques secondes, on passe par la voie
        /// d'en face si personne n'arrive.
        /// </summary>
        private void UpdatePass(TrafficTrack track)
        {
            float target = 0f;

            if (_passUntilS >= 0f)
            {
                if (track.Ahead(_agent.S, _passUntilS) > track.Length * 0.5f)
                {
                    _passUntilS = -1f;
                    _passing = null;
                }
                else
                {
                    target = -3.1f;
                }
            }
            else if (_obstacleFixed && _agent.ObstacleGap < 10f && _agent.StoppedFor > 4f && _agent.Held.Count == 0 && _obstacle != null)
            {
                float obstacleEnd = _agent.S + _halfLength + _agent.ObstacleGap + ObstacleLength(_obstacle) + _halfLength + 4f;
                if (OppositeClear(track, obstacleEnd))
                {
                    _passUntilS = track.Wrap(obstacleEnd);
                    _passing = Entity(_obstacle);
                    target = -3.1f;
                }
            }

            _offset = Mathf.MoveTowards(_offset, target, Time.fixedDeltaTime * 1.2f);
        }

        private static float ObstacleLength(Collider c)
        {
            Bounds b = c.bounds;
            return Mathf.Clamp(Mathf.Max(b.size.x, b.size.z), 0.5f, 7f);
        }

        /// <summary>La voie d'en face est-elle libre, de nous jusqu'au bout de l'obstacle (et ce qui arrive en face) ?</summary>
        private bool OppositeClear(TrafficTrack track, float until)
        {
            float span = track.Ahead(_agent.S, until);
            for (float d = 0f; d <= span + 25f; d += 2.5f)
            {
                float s = _agent.S + d;
                Vector3 tangent = track.Tangent(s);
                Vector3 left = new Vector3(-tangent.z, 0f, tangent.x);
                Vector3 p = track.Point(s) + left * 3.3f + Vector3.up * 0.9f;
                int count = Physics.OverlapBoxNonAlloc(p, new Vector3(1.4f, 0.8f, 1.3f), _hits, Quaternion.LookRotation(tangent), ~0,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    Collider c = _hits[i];
                    if (c == null || c.transform.IsChildOf(transform)) continue;
                    Vector3 v;
                    bool isPlayer;
                    if (Moving(c, out v, out isPlayer)) return false;
                }
            }

            return true;
        }

        private void Honk()
        {
            bool annoyed = (_playerAhead && _agent.StoppedFor > 2.5f) || (_obstacle != null && _agent.StoppedFor > 7f);
            if (annoyed && Time.time >= _nextHonk)
            {
                _honkUntil = Time.time + (_playerAhead ? 0.5f : 0.3f);
                _nextHonk = Time.time + Random.Range(4f, 7f);
            }

            _car.Honk(Time.time < _honkUntil);
        }

        // ------------------------------------------------------------------ incidents

        private void Recover(TrafficTrack track, Vector3 center, float speed, float dt)
        {
            // Perdue : loin de sa route ou sur le toit ; replacée quand personne ne regarde.
            float off = Mathf.Abs(track.Lateral(center, _agent.S));
            bool upsideDown = Vector3.Dot(transform.up, Vector3.up) < 0.4f;
            _lostFor = off > 6f || upsideDown ? _lostFor + dt : 0f;
            if (_lostFor > 4f && !Visible(center))
            {
                _lostFor = 0f;
                PlaceAt(FreeSpot(track, _agent.S));
                return;
            }

            // Coincée contre quelque chose que les capteurs ne voient pas (un trottoir, un
            // poteau) : elle veut avancer mais n'avance pas. Petite marche arrière, puis on
            // reprend ; et si ça dure, on la replace hors de vue.
            bool wants = _agent.Desired > 0.4f && _agent.ObstacleGap > 6f;
            _jammedFor = wants && speed < 0.3f ? _jammedFor + dt : 0f;
            if (_jammedFor > 3f)
            {
                _jammedFor = 0f;
                _reverseFor = 1.2f;
            }

            if (_agent.StoppedFor > 30f && !Visible(center) && !_playerAhead)
            {
                _agent.StoppedFor = 0f;
                PlaceAt(FreeSpot(track, _agent.S + 25f));
            }
        }

        /// <summary>Une place libre sur la route à partir de <paramref name="s"/> (personne dessus).</summary>
        private float FreeSpot(TrafficTrack track, float s)
        {
            for (int k = 0; k < 40; k++)
            {
                float candidate = track.Wrap(s + k * 4f);
                Vector3 p = track.Point(candidate) + Vector3.up * 1f;
                Quaternion r = Quaternion.LookRotation(track.Tangent(candidate));
                int count = Physics.OverlapBoxNonAlloc(p, new Vector3(_halfWidth + 0.3f, 0.6f, _halfLength + 1f), _hits, r, ~0,
                    QueryTriggerInteraction.Ignore);
                bool clear = true;
                for (int i = 0; i < count && clear; i++)
                {
                    Collider c = _hits[i];
                    if (c == null || c.transform.IsChildOf(transform)) continue;
                    Vector3 v;
                    bool isPlayer;
                    if (Moving(c, out v, out isPlayer)) clear = false;
                }

                if (clear && !InTurnaround(track, candidate)) return candidate;
            }

            return track.Wrap(s);
        }

        private static bool InTurnaround(TrafficTrack track, float s)
        {
            for (int t = 0; t < track.Turnarounds.Count; t++)
            {
                TrafficTrack.Turnaround turn = track.Turnarounds[t];
                if (track.Ahead(turn.Start - 6f, s) <= track.Ahead(turn.Start - 6f, turn.Rejoin)) return true;
            }

            return false;
        }

        private void PlaceAt(float s)
        {
            TrafficTrack track = _flow.Track;
            Vector3 p = track.Point(s);
            Quaternion rotation = Quaternion.LookRotation(track.Tangent(s), Vector3.up);
            Vector3 pivot = p - rotation * _centerOffset + Vector3.up * (_groundOffset + 0.15f);

            _body.position = pivot;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(pivot, rotation);
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            _agent.S = track.Wrap(s);
            _agent.Speed = 0f;
            _agent.Turning = -1;
            _passUntilS = -1f;
            _passing = null;
            _offset = 0f;
        }

        // ------------------------------------------------------------------ loin du joueur

        private void Sleep()
        {
            if (_asleep) return;
            _asleep = true;
            _agent.Asleep = true;
            _agent.Turning = -1;
            _agent.ObstacleGap = float.MaxValue;
            _car.Sleeping = true;
            _car.Honk(false);
            _body.isKinematic = true;
            _car.SetInput(0f, 0f, false);
            _passUntilS = -1f;
            _offset = 0f;
        }

        /// <summary>Reprend ses roues, si la place est libre (sinon elle attend, endormie).</summary>
        private bool Wake()
        {
            // En pleine manœuvre simulée : elle réapparaît au bout, sur la voie de retour.
            if (_agent.Turning >= 0)
            {
                _flow.FinishTurn(_agent, _flow.Track.Turnarounds[_agent.Turning].Rejoin);
            }

            Vector3 p = Center + Vector3.up * 1f;
            int count = Physics.OverlapBoxNonAlloc(p, new Vector3(_halfWidth, 0.5f, _halfLength), _hits, transform.rotation, ~0,
                QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i];
                if (c == null || c.transform.IsChildOf(transform)) continue;
                Vector3 v;
                bool isPlayer;
                if (Moving(c, out v, out isPlayer)) return false;
            }

            _asleep = false;
            _agent.Asleep = false;
            _car.Sleeping = false;
            _body.isKinematic = false;
            _body.linearVelocity = Forward * Mathf.Max(0f, _agent.Speed);
            _body.angularVelocity = Vector3.zero;
            _body.WakeUp();
            return true;
        }

        // ------------------------------------------------------------------ outils

        private static float ViewerDistance(Vector3 position)
        {
            Transform viewer = Player;
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            return viewer != null ? Vector3.Distance(viewer.position, position) : 0f;
        }

        private static bool Visible(Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            if (Vector3.Distance(camera.transform.position, position) > 90f) return false;

            Vector3 viewport = camera.WorldToViewportPoint(position);
            return viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
        }
    }
}
