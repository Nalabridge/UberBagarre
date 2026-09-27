using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le conducteur d'une voiture de la circulation. Il ne déplace pas la voiture : il tient
    /// le volant et les pédales d'une vraie <see cref="DrivableCar"/>, la même que celle du
    /// joueur. La voiture tourne donc avec ses roues avant, prend du roulis, freine avec son
    /// poids, dérape si on la percute.
    ///
    /// - Direction : poursuite pure — il vise un point de sa route à quelques mètres devant
    ///   (plus loin quand il va vite) et braque de l'angle qui y mène, d'après l'empattement.
    /// - Vitesse : il ralentit AVANT les virages (d'après l'angle de la route plus loin) et
    ///   pour ce qui est devant lui (voiture, passant, joueur), en gardant de quoi s'arrêter.
    /// - Coincé (un mur, une voiture en travers) : il recule en contrebraquant, puis repart.
    ///   Perdu (poussé loin de sa route, retourné), il est replacé sur sa route quand personne
    ///   ne regarde.
    /// - Loin du joueur, la physique s'endort : la voiture glisse le long de sa route, posée
    ///   sur la chaussée ; elle reprend ses roues en revenant à portée.
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
        [SerializeField, Min(1f)] private float _cornerSpeed = 5f;
        [SerializeField, Min(0.5f)] private float _comfortBraking = 4.5f;

        [SerializeField]
        [Tooltip("Au-dela, la physique s'endort et la voiture glisse sur sa route.")]
        private float _sleepDistance = 150f;

        [SerializeField]
        [Tooltip("Le corps du conducteur (cache quand le joueur prend la voiture).")]
        private GameObject _driverBody;

        private DrivableCar _car;
        private Rigidbody _body;
        private int _segment;
        private float _stuckFor;
        private float _reverseFor;
        private float _lostFor;
        private float _patience;
        private float _ignoreCarsUntil;
        private bool _asleep;
        private float _halfLength = 2.3f;
        private float _wheelbase = 2.7f;
        private float _honkAt;
        private readonly RaycastHit[] _hits = new RaycastHit[16];

        /// <summary>Transform de celui qu'on évite en priorité (le joueur).</summary>
        public static Transform Player { get; set; }

        private static readonly System.Collections.Generic.List<TrafficDriver> Active = new System.Collections.Generic.List<TrafficDriver>();
        private float _yieldingFor;
        private float _ignoreYieldUntil;

        private void OnEnable()
        {
            if (!Active.Contains(this)) Active.Add(this);
        }

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
            if (any) _halfLength = Mathf.Max(1.6f, Vector3.Dot(bounds.extents, new Vector3(
                Mathf.Abs(transform.forward.x), Mathf.Abs(transform.forward.y), Mathf.Abs(transform.forward.z))));
        }

        private void Start()
        {
            if (_path == null || _path.Length < 3)
            {
                enabled = false;
                return;
            }

            _wheelbase = _car.Wheelbase;
            _car.Kind = DrivableCar.Access.Circulation;
            _segment = Mathf.Abs(_startIndex) % _path.Length;
            PlaceOnPath(_segment, 0f);
            _car.Autopilot = true;
        }

        private void OnDisable()
        {
            Active.Remove(this);
            if (_car != null && !_car.Occupied) _car.Autopilot = false;
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

        private int Next(int i) { return (i + 1) % _path.Length; }

        // ------------------------------------------------------------------ conduite

        private void FixedUpdate()
        {
            if (_path == null || _path.Length < 3) return;

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
            Vector3 position = transform.position;
            AdvanceSegment(position);

            float distanceToViewer = ViewerDistance(position);
            if (distanceToViewer > _sleepDistance)
            {
                Glide(dt);
                return;
            }

            if (_asleep) Wake();

            float speed = _car.ForwardSpeed;
            float lookahead = Mathf.Clamp(4.5f + Mathf.Abs(speed) * 0.75f, 5f, 16f);
            Vector3 target = PointAhead(position, lookahead);

            // --- volant : poursuite pure (angle d'Ackermann vers le point visé)
            Vector3 local = transform.InverseTransformPoint(target);
            float alpha = Mathf.Atan2(local.x, Mathf.Max(0.1f, local.z));
            float steerDegrees = Mathf.Atan2(2f * _wheelbase * Mathf.Sin(alpha), lookahead) * Mathf.Rad2Deg;
            float steer = Mathf.Clamp(steerDegrees / Mathf.Max(1f, _car.SteerLimit), -1f, 1f);

            // --- vitesse voulue : virages à venir, puis obstacles
            float wanted = CornerSpeed(position, speed);
            float obstacle = Obstacle(position, speed);
            if (obstacle < float.MaxValue)
            {
                float room = Mathf.Max(0f, obstacle - 2f);
                wanted = Mathf.Min(wanted, Mathf.Sqrt(2f * _comfortBraking * room));
                if (room < 0.4f) wanted = 0f;
            }

            // --- carrefour : celui qui arrive le premier passe ; à égalité, priorité à droite
            float yieldAt = Yield(position, speed);
            if (yieldAt < float.MaxValue)
            {
                wanted = Mathf.Min(wanted, Mathf.Sqrt(2f * _comfortBraking * Mathf.Max(0f, yieldAt - 3.5f)));
                if (yieldAt < 4.5f) wanted = 0f;
            }

            // --- coincé : on recule en contrebraquant
            if (_reverseFor > 0f)
            {
                _reverseFor -= dt;
                _car.SetInput(-0.6f, -steer, false);
                return;
            }

            bool blocked = obstacle < 6f;
            _stuckFor = wanted > 2f && Mathf.Abs(speed) < 0.4f && !blocked ? _stuckFor + dt : 0f;
            if (_stuckFor > 3f)
            {
                _stuckFor = 0f;
                _reverseFor = 1.6f;
                return;
            }

            // --- perdu : loin de sa route ou sur le toit, replacé quand personne ne regarde
            float off = DistanceToPath(position);
            bool upsideDown = Vector3.Dot(transform.up, Vector3.up) < 0.4f;
            _lostFor = off > 9f || upsideDown ? _lostFor + dt : 0f;
            if (_lostFor > 5f && !Visible(position))
            {
                _lostFor = 0f;
                PlaceOnPath(_segment, 0f);
                return;
            }

            // --- pédales : un régulateur simple
            float error = wanted - speed;
            float throttle;
            bool hold = false;
            if (wanted < 0.3f && Mathf.Abs(speed) < 0.6f)
            {
                throttle = 0f;
                hold = true;
            }
            else if (error > 0f)
            {
                throttle = Mathf.Clamp(error * 0.45f + 0.15f, 0f, 1f);
            }
            else
            {
                throttle = Mathf.Clamp(error * 0.35f, -1f, 0f);
                if (speed < 0.8f) throttle = 0f;
            }

            _car.SetInput(throttle, steer, hold);
        }

        /// <summary>
        /// Passe au segment suivant quand la voiture a dépassé le bout du segment courant (sa
        /// projection sort du segment) — pas seulement quand elle touche le point.
        /// </summary>
        private void AdvanceSegment(Vector3 position)
        {
            for (int guard = 0; guard < 6; guard++)
            {
                Vector3 a = _path[_segment];
                Vector3 b = _path[Next(_segment)];
                Vector3 ab = Flat(b - a);
                float length = ab.magnitude;
                if (length < 0.01f)
                {
                    _segment = Next(_segment);
                    continue;
                }

                float along = Vector3.Dot(Flat(position - a), ab / length);
                if (along < length - 0.5f) return;
                _segment = Next(_segment);
            }
        }

        /// <summary>Le point de la route à <paramref name="distance"/> mètres devant la projection de la voiture.</summary>
        private Vector3 PointAhead(Vector3 position, float distance)
        {
            int i = _segment;
            Vector3 a = _path[i];
            Vector3 b = _path[Next(i)];
            Vector3 ab = Flat(b - a);
            float length = Mathf.Max(0.01f, ab.magnitude);
            float along = Mathf.Clamp(Vector3.Dot(Flat(position - a), ab / length), 0f, length);
            float left = distance;
            float remaining = length - along;

            for (int guard = 0; guard < _path.Length; guard++)
            {
                if (left <= remaining)
                {
                    float t = (along + left) / length;
                    return Vector3.Lerp(a, b, t);
                }

                left -= remaining;
                i = Next(i);
                a = _path[i];
                b = _path[Next(i)];
                length = Mathf.Max(0.01f, Flat(b - a).magnitude);
                along = 0f;
                remaining = length;
            }

            return b;
        }

        /// <summary>
        /// La vitesse permise par la route devant : pour chaque changement de cap à venir (sur la
        /// distance de freinage), la vitesse de virage selon l'angle, et ce qu'on peut encore
        /// perdre d'ici là.
        /// </summary>
        private float CornerSpeed(Vector3 position, float speed)
        {
            float horizon = 12f + speed * speed / (2f * _comfortBraking);
            float wanted = _cruise;
            float travelled = Flat(_path[Next(_segment)] - position).magnitude;
            int i = Next(_segment);

            for (int guard = 0; guard < 24 && travelled < horizon; guard++)
            {
                Vector3 inDir = Flat(_path[i] - _path[(i - 1 + _path.Length) % _path.Length]);
                Vector3 outDir = Flat(_path[Next(i)] - _path[i]);
                if (inDir.sqrMagnitude > 1e-4f && outDir.sqrMagnitude > 1e-4f)
                {
                    float angle = Vector3.Angle(inDir, outDir);
                    if (angle > 12f)
                    {
                        float corner = Mathf.Lerp(_cruise, _cornerSpeed, Mathf.InverseLerp(12f, 80f, angle));
                        float reachable = Mathf.Sqrt(corner * corner + 2f * _comfortBraking * Mathf.Max(0f, travelled - 3f));
                        wanted = Mathf.Min(wanted, reachable);
                    }
                }

                travelled += outDir.magnitude;
                i = Next(i);
            }

            return wanted;
        }

        /// <summary>Distance au premier obstacle MOBILE devant le pare-chocs, ou MaxValue.</summary>
        private float Obstacle(Vector3 position, float speed)
        {
            float reach = 6f + speed * speed / (2f * _comfortBraking) + Mathf.Abs(speed) * 0.8f;
            Vector3 forward = transform.forward;

            // On regarde là où la voiture va : dans un virage, un peu vers l'intérieur.
            Vector3 aim = Flat(PointAhead(position, Mathf.Min(reach, 10f)) - position);
            if (aim.sqrMagnitude > 1f) forward = Vector3.Slerp(forward, aim.normalized, 0.5f);

            Vector3 origin = position + Vector3.up * 0.9f + transform.forward * (_halfLength - 0.9f);
            int count = Physics.SphereCastNonAlloc(origin, 0.85f, forward, _hits, reach, ~0, QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            bool player = false;
            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;

                bool car = c.GetComponentInParent<DrivableCar>() != null;
                bool person = c is CharacterController || c.GetComponentInParent<MocapWalker>() != null ||
                              c.GetComponentInParent<Combatant>() != null;
                if (!car && !person) continue;

                // Deux voitures qui s'attendent à un carrefour ne s'attendront pas pour toujours :
                // au bout de quelques secondes, celle-ci passe (en poussant un peu s'il le faut).
                if (car && Time.time < _ignoreCarsUntil) continue;

                if (_hits[i].distance < nearest)
                {
                    nearest = _hits[i].distance;
                    player = Player != null && (c.transform.IsChildOf(Player) ||
                                                (DrivableCar.Driven != null && c.transform.IsChildOf(DrivableCar.Driven.transform)));
                }
            }

            _patience = nearest < 8f && Mathf.Abs(speed) < 0.5f ? _patience + Time.fixedDeltaTime : 0f;
            if (_patience > 6f && !player)
            {
                _patience = 0f;
                _ignoreCarsUntil = Time.time + 2.5f;
            }

            if (player && _patience > 2.5f && Time.time >= _honkAt)
            {
                _car.Honk(true);
                _honkAt = Time.time + 4f;
                Invoke("StopHonk", 0.45f);
            }

            return nearest;
        }

        /// <summary>
        /// Les trajectoires qui croisent la nôtre dans les prochains mètres : les autres voitures
        /// de la circulation (le long de leur route) et celle du joueur (le long de sa vitesse).
        /// Rend la distance au point de croisement si c'est à nous de céder, sinon MaxValue.
        /// </summary>
        private float Yield(Vector3 position, float speed)
        {
            if (Time.time < _ignoreYieldUntil) return float.MaxValue;

            Vector3 a0 = Flat(position);
            Vector3 a1 = Flat(PointAhead(position, 18f));
            Vector3 dirA = a1 - a0;
            if (dirA.sqrMagnitude < 1f) return float.MaxValue;
            dirA.Normalize();
            Vector3 right = new Vector3(dirA.z, 0f, -dirA.x);
            float mySpeed = Mathf.Max(2f, speed);
            float best = float.MaxValue;

            for (int i = 0; i < Active.Count; i++)
            {
                TrafficDriver other = Active[i];
                if (other == null || other == this || other._path == null || other._path.Length < 3) continue;
                Vector3 b0 = Flat(other.transform.position);
                if ((b0 - a0).sqrMagnitude > 35f * 35f) continue;
                Vector3 b1 = Flat(other.PointAhead(other.transform.position, 18f));
                float d = Conflict(a0, a1, dirA, right, mySpeed, b0, b1, Mathf.Max(2f, other._car.ForwardSpeed));
                if (d < best) best = d;
            }

            // La voiture du joueur : on lui laisse le passage (il ne s'arrêtera pas, lui).
            DrivableCar driven = DrivableCar.Driven;
            if (driven != null && driven.Body != null)
            {
                Vector3 b0 = Flat(driven.transform.position);
                Vector3 velocity = Flat(driven.Body.linearVelocity);
                if ((b0 - a0).sqrMagnitude < 35f * 35f && velocity.sqrMagnitude > 4f)
                {
                    float d = Conflict(a0, a1, dirA, right, mySpeed, b0, b0 + velocity * 2.2f, velocity.magnitude, true);
                    if (d < best) best = d;
                }
            }

            // On ne cède pas pour toujours (deux voitures qui se font des politesses).
            _yieldingFor = best < float.MaxValue && Mathf.Abs(speed) < 0.5f ? _yieldingFor + Time.fixedDeltaTime : 0f;
            if (_yieldingFor > 5f)
            {
                _yieldingFor = 0f;
                _ignoreYieldUntil = Time.time + 2.5f;
                return float.MaxValue;
            }

            return best;
        }

        private static float Conflict(Vector3 a0, Vector3 a1, Vector3 dirA, Vector3 right, float speedA,
            Vector3 b0, Vector3 b1, float speedB, bool alwaysYield = false)
        {
            Vector3 dirB = b1 - b0;
            if (dirB.sqrMagnitude < 1f) return float.MaxValue;

            // Seulement les trajectoires qui se CROISENT (pas la même voie, pas la voie d'en face).
            float angle = Vector3.Angle(dirA, dirB);
            if (angle < 25f || angle > 155f) return float.MaxValue;

            Vector3 x;
            if (!Intersect(a0, a1, b0, b1, out x)) return float.MaxValue;

            float dA = (x - a0).magnitude;
            float dB = (x - b0).magnitude;
            if (alwaysYield) return dA;

            float tA = dA / speedA;
            float tB = dB / speedB;
            bool otherFirst = tB < tA - 0.35f;
            bool tie = Mathf.Abs(tA - tB) <= 0.35f;
            bool otherOnRight = Vector3.Dot(right, b0 - a0) > 0f;

            return otherFirst || (tie && otherOnRight) ? dA : float.MaxValue;
        }

        private static bool Intersect(Vector3 p0, Vector3 p1, Vector3 q0, Vector3 q1, out Vector3 point)
        {
            point = Vector3.zero;
            float rx = p1.x - p0.x, rz = p1.z - p0.z;
            float sx = q1.x - q0.x, sz = q1.z - q0.z;
            float denominator = rx * sz - rz * sx;
            if (Mathf.Abs(denominator) < 1e-4f) return false;

            float qpx = q0.x - p0.x, qpz = q0.z - p0.z;
            float t = (qpx * sz - qpz * sx) / denominator;
            float u = (qpx * rz - qpz * rx) / denominator;
            if (t < 0f || t > 1f || u < 0f || u > 1f) return false;

            point = new Vector3(p0.x + t * rx, 0f, p0.z + t * rz);
            return true;
        }

        private void StopHonk()
        {
            if (_car != null) _car.Honk(false);
        }

        // ------------------------------------------------------------------ loin du joueur

        private void Glide(float dt)
        {
            if (!_asleep)
            {
                _asleep = true;
                _car.Sleeping = true;
                _body.isKinematic = true;
                _car.SetInput(0f, 0f, false);
            }

            Vector3 a = _path[_segment];
            Vector3 b = _path[Next(_segment)];
            Vector3 ab = b - a;
            float length = Mathf.Max(0.01f, Flat(ab).magnitude);
            float along = Mathf.Clamp(Vector3.Dot(Flat(transform.position - a), Flat(ab) / length), 0f, length);
            along += _cruise * 0.8f * dt;

            Vector3 p = Vector3.Lerp(a, b, along / length);
            Vector3 heading = Flat(PointAhead(p, 6f) - p);
            Quaternion rotation = heading.sqrMagnitude > 0.01f ? Quaternion.LookRotation(heading.normalized, Vector3.up) : transform.rotation;
            _body.MovePosition(p);
            _body.MoveRotation(Quaternion.Slerp(transform.rotation, rotation, 1f - Mathf.Exp(-3f * dt)));
        }

        private void Wake()
        {
            _asleep = false;
            _car.Sleeping = false;
            _body.isKinematic = false;
            _body.linearVelocity = transform.forward * (_cruise * 0.8f);
            _body.angularVelocity = Vector3.zero;
            _body.WakeUp();
        }

        // ------------------------------------------------------------------ outils

        private void PlaceOnPath(int segment, float along)
        {
            Vector3 a = _path[segment];
            Vector3 b = _path[Next(segment)];
            Vector3 p = Vector3.Lerp(a, b, Mathf.Clamp01(along));
            Vector3 heading = Flat(b - a);
            Quaternion rotation = heading.sqrMagnitude > 0.01f ? Quaternion.LookRotation(heading.normalized, Vector3.up) : transform.rotation;

            p += Vector3.up * 0.15f;
            _body.position = p;
            _body.rotation = rotation;
            transform.SetPositionAndRotation(p, rotation);
            if (!_body.isKinematic)
            {
                _body.linearVelocity = Vector3.zero;
                _body.angularVelocity = Vector3.zero;
            }

            _segment = segment;
        }

        private float DistanceToPath(Vector3 position)
        {
            Vector3 a = _path[_segment];
            Vector3 b = _path[Next(_segment)];
            Vector3 ab = Flat(b - a);
            float length = Mathf.Max(0.01f, ab.magnitude);
            float along = Mathf.Clamp(Vector3.Dot(Flat(position - a), ab / length), 0f, length);
            Vector3 closest = a + ab / length * along;
            return Flat(position - closest).magnitude;
        }

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

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
