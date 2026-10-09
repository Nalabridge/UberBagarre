using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.View;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UberBagarre.World
{
    /// <summary>
    /// Un passant : il fait le tour de la ville sur le trottoir, à son pas, avec la marche et
    /// l'attente capturées du paquet FS (Third Person Controller).
    ///
    /// Ce n'est pas un combattant : ni vie, ni cerveau, ni zones de frappe — une ville a besoin
    /// de monde qui passe, pas de cibles. Mais il se comporte comme quelqu'un :
    ///
    /// - **Il ne traverse rien.** Chaque pas est vérifié contre le décor (murs, poteaux, bancs,
    ///   voitures garées) : il contourne ce qu'il voit venir, et glisse le long de ce qu'il
    ///   frôle au lieu de passer au travers. Coincé, il reprend son chemin un peu plus loin.
    /// - **Il partage le trottoir.** Deux passants qui se croisent se décalent chacun sur leur
    ///   droite ; on contourne quelqu'un d'arrêté au lieu de piétiner derrière lui.
    /// - **Il regarde avant de traverser** : au bord du trottoir, si une voiture arrive, il
    ///   attend qu'elle passe, puis traverse en pressant un peu le pas.
    /// - **Il vit.** Il marche en courbes (pas en pivotant sur un talon), tourne la tête vers
    ///   le joueur qui passe près de lui, vers un klaxon, vers une vitrine ; chacun a sa
    ///   posture. Il s'arrête parfois : regarder son téléphone, une vitrine, autour de lui, ou
    ///   discuter avec quelqu'un qu'il croise. La nuit, la plupart rentrent chez eux.
    ///
    /// Une voiture qui fonce sur lui : il se jette sur le côté (le côté libre). Trop tard : il
    /// est renversé (la chute capturée), reste un moment au sol et se relève.
    ///
    /// Sans le paquet, il marche avec la locomotion calculée des combattants.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class MocapWalker : MonoBehaviour
    {
        [Header("Trajet")]
        [SerializeField]
        [Tooltip("Points du tour (monde), parcourus en boucle.")]
        private Vector3[] _path = new Vector3[0];

        [SerializeField] private int _startIndex;
        [SerializeField, Min(0.3f)] private float _speed = 1.25f;
        [SerializeField, Range(0f, 1f)] private float _pauseChance = 0.25f;
        [SerializeField] private Vector2 _pauseDuration = new Vector2(2f, 7f);

        [Header("Corps")]
        [SerializeField] private MocapLibrary _library;
        [SerializeField] private Animator _animator;
        [SerializeField] private BodyRig _rig;

        [SerializeField]
        [Tooltip("Secours sans animations capturees : la marche calculee.")]
        private ProceduralLocomotion _locomotion;

        [Header("Voisinage")]
        [SerializeField, Min(0.3f)] private float _personalSpace = 1.4f;
        [SerializeField, Min(1f)] private float _fightRadius = 12f;

        private enum Activity
        {
            None,
            LookAround,
            Phone,
            Window,
            Chat,
            WaitToCross
        }

        private static readonly List<MocapWalker> Walkers = new List<MocapWalker>();

        private const float Radius = 0.24f;

        private Rigidbody _body;
        private Collider _collider;
        private int _next;
        private float _pause;
        private float _currentSpeed;
        private float _hurry = 1f;
        private float _hurryUntil;
        private float _nextScan;
        private bool _blocked;
        private Vector3 _heading = Vector3.forward;
        private float _turnRate;
        private Vector3 _peopleSteer;
        private Vector3 _wallSteer;
        private float _groundY;
        private bool _hasGround;

        // Bloqué sans avancer : on finit par prendre le point suivant du trajet.
        private Vector3 _progressFrom;
        private float _progressTimer;

        // Personnalité : posture, curiosité, sociabilité, couche-tard.
        private float _posture;
        private float _curiosity;
        private bool _sociable;
        private bool _nightOwl;
        private bool _reverse;
        private bool _resting;
        private float _nextRestCheck;

        // Activité pendant une pause.
        private Activity _activity;
        private Vector3 _facing;
        private MocapWalker _partner;
        private float _crossWait;
        private float _nextChatCheck;

        // Regard.
        private Vector3 _lookPoint;
        private float _lookUntil;
        private float _nextGlance;
        private float _playerCooldown;
        private Vector3 _attention;
        private float _attentionUntil;
        private float _yaw;
        private float _pitch;
        private float _yawVelocity;
        private float _pitchVelocity;

        // Téléphone en main.
        private float _phoneWeight;
        private GameObject _phone;
        private Quaternion _rightHandRest = Quaternion.identity;
        private bool _handMeasured;
        private static Material _phoneMaterial;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _idle;
        private AnimationClipPlayable _walk;
        private bool _animated;
        private AnimationClipPlayable _react;

        private float _dodgeUntil;
        private Vector3 _dodge;
        private int _downStage;          // 0 debout, 1 chute, 2 au sol, 3 relevage
        private float _downTimer;
        private Vector3 _slide;

        private readonly RaycastHit[] _hits = new RaycastHit[12];

        /// <summary>Vrai tant qu'il est au sol (renversé) ou en train de se relever.</summary>
        public bool IsDown { get { return _downStage != 0; } }

        private bool _oneWay;

        /// <summary>Arrivé au bout d'un trajet à sens unique (un personnage de l'histoire).</summary>
        public bool Arrived { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Walkers.Clear();
            _phoneMaterial = null;
        }

        /// <summary>
        /// Un trajet à parcourir une seule fois, sans pause (un personnage qu'on suit, qu'on
        /// escorte) : il part du premier point et s'arrête au dernier.
        /// </summary>
        public void Walk(Vector3[] path, float speed)
        {
            if (path == null || path.Length < 2) return;
            _path = path;
            _speed = speed;
            _pauseChance = 0f;
            _oneWay = true;
            Arrived = false;
            _next = 1;
            _pause = 0f;
            _activity = Activity.None;
            if (_body == null) _body = GetComponent<Rigidbody>();
            transform.position = path[0];
            if (_body != null) _body.position = path[0];
            _heading = Flat(path[1] - path[0]).normalized;
            Face(_heading, 1f);
            enabled = true;
        }

        /// <summary>Pose le trajet (constructeur de la ville).</summary>
        public void SetPath(Vector3[] path, int start, float speed)
        {
            _path = path;
            _startIndex = start;
            _speed = speed;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
            _collider = GetComponent<Collider>();
            _body.isKinematic = true;
            _body.interpolation = RigidbodyInterpolation.Interpolate;

            // Chacun son allure et ses manières (tirées de son nom d'objet : stable d'une partie à l'autre).
            System.Random random = new System.Random(name.GetHashCode());
            _posture = (float)(random.NextDouble() * 10.0 - 3.0);
            _curiosity = 0.35f + (float)random.NextDouble() * 0.55f;
            _sociable = random.NextDouble() < 0.45;
            _nightOwl = random.NextDouble() < 0.35;
            // Pas tous dans le même sens : on se croise sur les trottoirs.
            _reverse = random.NextDouble() < 0.4;
            _nextGlance = Time.time + 2f + (float)random.NextDouble() * 6f;
        }

        private void Start()
        {
            if (_path == null || _path.Length < 2)
            {
                enabled = false;
                return;
            }

            if (!_oneWay)
            {
                _next = Advance(_startIndex % _path.Length);
                transform.position = _path[_startIndex % _path.Length];
                _heading = Flat(_path[_next] - transform.position);
                _heading = _heading.sqrMagnitude > 1e-4f ? _heading.normalized : transform.forward;
                Face(_heading, 1f);
            }

            _progressFrom = transform.position;
            BuildGraph();

            if (_locomotion != null) _locomotion.enabled = !_animated;

            // Des doigts à demi fermés : une main qui marche ne serre pas le poing.
            if (_rig != null)
            {
                if (_rig.LeftHand != null) _rig.LeftHand.TargetGrip = 0.3f;
                if (_rig.RightHand != null) _rig.RightHand.TargetGrip = 0.3f;
                _handMeasured = HandFrame.Measure(_rig.RightArm, true, out _rightHandRest);
            }
        }

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;
            if (!Walkers.Contains(this)) Walkers.Add(this);
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;
            Walkers.Remove(this);
            if (_partner != null && _partner._partner == this) _partner.EndChat();
            if (_phone != null) _phone.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
            if (_phone != null) Destroy(_phone);
        }

        private void BuildGraph()
        {
            _animated = false;
            if (_library == null || _animator == null || _animator.avatar == null || !_animator.avatar.isHuman) return;

            AnimationClip idle = _library.relaxedIdle != null ? _library.relaxedIdle : _library.combatIdle;
            AnimationClip walk = _library.relaxedWalk != null ? _library.relaxedWalk : _library.walkForward;
            if (idle == null || walk == null) return;

            _graph = PlayableGraph.Create(name + " (marche)");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Corps", _animator);
            _mixer = AnimationMixerPlayable.Create(_graph, 3);
            output.SetSourcePlayable(_mixer);

            _idle = AnimationClipPlayable.Create(_graph, idle);
            _walk = AnimationClipPlayable.Create(_graph, walk);
            _walk.SetApplyFootIK(true);
            _graph.Connect(_idle, 0, _mixer, 0);
            _graph.Connect(_walk, 0, _mixer, 1);

            // Chacun sa phase : vingt passants qui posent le même pied à la même image se
            // lisent comme une chorégraphie.
            _walk.SetTime(Random.value * walk.length);
            _idle.SetTime(Random.value * idle.length);

            _animator.enabled = true;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.CullCompletely;
            _graph.Play();
            _animated = true;
        }

        // ================================================================== déplacement

        private void FixedUpdate()
        {
            if (_path == null || _path.Length < 2) return;

            float dt = Time.fixedDeltaTime;
            Vector3 position = _body.position;

            UpdateRest(position);
            if (_resting) return;

            if (_downStage != 0)
            {
                // Renversé : il glisse sur l'élan du choc (pas à travers un mur), puis ne bouge plus.
                _slide = Vector3.MoveTowards(_slide, Vector3.zero, dt * 9f);
                _currentSpeed = 0f;
                if (_slide.sqrMagnitude > 1e-4f) _body.MovePosition(SafeStep(position, _slide * dt));
                return;
            }

            if (Time.time < _dodgeUntil)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 3.4f, dt * 14f);
                _body.MovePosition(Ground(SafeStep(position, _dodge * (_currentSpeed * dt)), dt));
                Face(_dodge, 1f - Mathf.Exp(-10f * dt));
                return;
            }

            // --- le trajet : on vise un point qui glisse vers le suivant (des courbes, pas des pivots)
            Vector3 target = _path[_next];
            Vector3 to = Flat(target - position);
            if (to.magnitude < 0.7f)
            {
                if (_oneWay && _next == _path.Length - 1)
                {
                    if (to.magnitude < 0.35f)
                    {
                        Arrived = true;
                        _currentSpeed = 0f;
                        return;
                    }
                }
                else
                {
                    _next = Advance(_next);
                    _progressFrom = position;
                    _progressTimer = 0f;
                    if (Random.value < _pauseChance) StartPause(position);
                    target = _path[_next];
                    to = Flat(target - position);
                }
            }

            Vector3 carrot = target;
            if (!_oneWay || _next < _path.Length - 1)
            {
                Vector3 after = _path[_oneWay ? Mathf.Min(_next + 1, _path.Length - 1) : Advance(_next)];
                float blend = Mathf.Clamp01(1f - to.magnitude / 1.8f) * 0.6f;
                carrot = Vector3.Lerp(target, after, blend);
            }

            Vector3 desired = Flat(carrot - position);
            desired = desired.sqrMagnitude > 1e-4f ? desired.normalized : _heading;

            bool near = NearCamera(position, 90f);
            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.16f + Random.value * 0.06f;
                Scan(position, desired, near);
            }

            // --- vitesse
            bool paused = _pause > 0f || _activity == Activity.WaitToCross;
            if (_pause > 0f)
            {
                _pause -= dt;
                if (_pause <= 0f) EndActivity();
            }

            float wanted = paused || _blocked ? 0f : _speed * _hurry;
            // On ralentit pour tourner (on ne prend pas un virage serré à pleine allure).
            float bend = Vector3.Angle(_heading, desired);
            wanted *= Mathf.Lerp(1f, 0.45f, Mathf.InverseLerp(35f, 120f, bend));
            _currentSpeed = Mathf.MoveTowards(_currentSpeed, wanted, dt * (wanted > _currentSpeed ? 1.8f : 3f));

            // --- cap : le chemin, plus l'évitement des gens et du décor ; il tourne en douceur
            Vector3 steer = desired + _peopleSteer + _wallSteer;
            steer = Flat(steer);
            if (paused && _facing.sqrMagnitude > 0.01f) steer = _facing;
            if (steer.sqrMagnitude < 1e-4f) steer = _heading;
            steer.Normalize();

            float maxTurn = (paused ? 140f : 200f) * dt;
            float angle = Vector3.SignedAngle(_heading, steer, Vector3.up);
            float turn = Mathf.Clamp(angle, -maxTurn, maxTurn);
            _heading = Quaternion.AngleAxis(turn, Vector3.up) * _heading;
            _turnRate = Mathf.Lerp(_turnRate, turn / Mathf.Max(1e-4f, dt), 1f - Mathf.Exp(-6f * dt));

            Vector3 step = _heading * (_currentSpeed * dt);
            Vector3 next = near ? SafeStep(position, step) : position + step;
            next = Ground(next, dt, target.y);

            _body.MovePosition(next);
            Face(_heading, 1f - Mathf.Exp(-8f * dt));

            // Coincé (un mur que le trajet traverse, une foule) : il prend le point suivant.
            if (!paused && wanted > 0.2f)
            {
                _progressTimer += dt;
                if (_progressTimer > 2.5f)
                {
                    if (Flat(position - _progressFrom).magnitude < 0.6f) SkipAhead(position);
                    _progressFrom = position;
                    _progressTimer = 0f;
                }
            }
            else
            {
                _progressFrom = position;
                _progressTimer = 0f;
            }
        }

        /// <summary>
        /// Un pas qui ne traverse rien : une capsule glissée du point de départ ; contre un
        /// mur, on garde ce qui longe le mur (on glisse), on perd ce qui rentre dedans.
        /// </summary>
        private Vector3 SafeStep(Vector3 position, Vector3 step)
        {
            step.y = 0f;
            float length = step.magnitude;
            if (length < 1e-5f) return position;

            Vector3 direction = step / length;
            RaycastHit hit;
            if (!Cast(position, direction, length, out hit)) return position + step;

            float allowed = Mathf.Max(0f, hit.distance - 0.02f);
            Vector3 moved = position + direction * allowed;
            Vector3 normal = Flat(hit.normal);
            if (normal.sqrMagnitude < 1e-4f) return moved;
            normal.Normalize();

            // Le reste du pas, le long du mur.
            Vector3 rest = direction * (length - allowed);
            Vector3 along = rest - normal * Vector3.Dot(rest, normal);
            float alongLength = along.magnitude;
            if (alongLength < 1e-5f) return moved;

            RaycastHit second;
            if (Cast(moved, along / alongLength, alongLength, out second))
            {
                return moved + along / alongLength * Mathf.Max(0f, second.distance - 0.02f);
            }

            return moved + along;
        }

        /// <summary>La capsule du passant contre le décor (pas contre les gens ni contre lui-même).</summary>
        private bool Cast(Vector3 position, Vector3 direction, float distance, out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            Vector3 bottom = position + Vector3.up * (Radius + 0.32f);
            Vector3 top = position + Vector3.up * 1.5f;
            int count = Physics.CapsuleCastNonAlloc(bottom, top, Radius, direction, _hits, distance + 0.02f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                RaycastHit hit = _hits[i];
                if (hit.collider == null || !Solid(hit.collider)) continue;
                // Déjà dedans (distance 0) : on ne s'y accroche pas, on en sort.
                if (hit.distance <= 0f) continue;
                // Le sol qui monte (une rampe, un trottoir en pente) n'est pas un mur.
                if (hit.normal.y > 0.55f) continue;
                if (hit.distance < best)
                {
                    best = hit.distance;
                    nearest = hit;
                    found = true;
                }
            }

            return found;
        }

        /// <summary>Le décor qui arrête un piéton : tout sauf lui, les autres gens et les voitures qui roulent.</summary>
        private bool Solid(Collider c)
        {
            if (c.transform.IsChildOf(transform)) return false;
            if (c.GetComponentInParent<MocapWalker>() != null) return false;
            if (c is CharacterController) return false;
            if (c.GetComponentInParent<Combatant>() != null) return false;

            Rigidbody body = c.attachedRigidbody;
            if (body != null && !body.isKinematic && body.linearVelocity.sqrMagnitude > 1f) return false;
            return true;
        }

        /// <summary>La hauteur du sol sous ses pieds (trottoir, bordure, marche), lissée.</summary>
        private Vector3 Ground(Vector3 next, float dt, float fallback = float.NaN)
        {
            RaycastHit hit;
            Vector3 origin = next + Vector3.up * 1.1f;
            bool found = false;
            int count = Physics.RaycastNonAlloc(origin, Vector3.down, _hits, 2.6f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            hit = new RaycastHit();
            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform) || c.GetComponentInParent<MocapWalker>() != null) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;
                if (_hits[i].distance < best)
                {
                    best = _hits[i].distance;
                    hit = _hits[i];
                    found = true;
                }
            }

            float y;
            if (found) y = hit.point.y;
            else if (!float.IsNaN(fallback)) y = fallback;
            else y = next.y;

            if (!_hasGround)
            {
                _groundY = y;
                _hasGround = true;
            }

            _groundY = Mathf.Lerp(_groundY, y, 1f - Mathf.Exp(-12f * dt));
            next.y = _groundY;
            return next;
        }

        // ------------------------------------------------------------------ ce qui l'entoure

        private void Scan(Vector3 position, Vector3 desired, bool near)
        {
            _blocked = false;
            _peopleSteer = Vector3.zero;
            _wallSteer = Vector3.zero;
            _hurry = Time.time < _hurryUntil ? 1.55f : 1f;

            WatchTraffic(position);
            if (!near) return;

            Vector3 right = new Vector3(_heading.z, 0f, -_heading.x);
            Vector3 myVelocity = _heading * _currentSpeed;

            // --- les gens : on se décale à droite pour croiser, on contourne qui est arrêté
            for (int i = 0; i < Walkers.Count; i++)
            {
                MocapWalker other = Walkers[i];
                if (other == null || other == this || other._resting || other.IsDown) continue;
                if (other == _partner) continue;
                AvoidPerson(position, myVelocity, right, other.transform.position, other._heading * other._currentSpeed);
            }

            if (TrafficDriver.Player != null) AvoidPerson(position, myVelocity, right, TrafficDriver.Player.position, Vector3.zero);

            IReadOnlyList<Combatant> all = Combatant.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant other = all[i];
                if (other == null) continue;
                if (TrafficDriver.Player != null && other.transform.IsChildOf(TrafficDriver.Player)) continue;
                AvoidPerson(position, myVelocity, right, other.transform.position, Vector3.zero);
            }

            // --- le décor : on voit venir le poteau, le banc, la voiture garée
            WatchObstacles(position, desired);

            // --- traverser : on regarde si une voiture arrive
            WatchCrossing(position, desired);

            // --- une rencontre : deux sociables qui se croisent s'arrêtent pour parler
            if (_sociable && _activity == Activity.None && _pause <= 0f && !_oneWay && Time.time >= _nextChatCheck) LookForChat(position);
        }

        private void AvoidPerson(Vector3 position, Vector3 myVelocity, Vector3 right, Vector3 other, Vector3 otherVelocity)
        {
            Vector3 offset = Flat(other - position);
            float distance = offset.magnitude;
            if (distance > 4.5f || distance < 1e-3f) return;

            // Trop près : on s'écarte.
            if (distance < 0.8f) _peopleSteer -= offset / distance * (0.8f - distance) * 2.2f;

            Vector3 relative = Flat(myVelocity - otherVelocity);
            float closing = relative.sqrMagnitude;
            if (closing < 0.04f) return;

            float t = Mathf.Clamp(Vector3.Dot(offset, relative) / closing, 0f, 3f);
            if (t <= 0f) return;
            Vector3 miss = offset - relative * t;
            if (miss.magnitude > 0.95f || t > 2.6f) return;

            // On croise par la droite (comme sur un trottoir) ; plus c'est proche, plus franc.
            float strength = (1f - t / 2.6f) * 1.1f;
            _peopleSteer += right * strength;

            // Quelqu'un arrêté juste devant : on ralentit en le contournant.
            if (otherVelocity.sqrMagnitude < 0.04f && distance < _personalSpace && Vector3.Dot(offset, _heading) > 0.5f * distance)
            {
                _peopleSteer += right * 0.6f;
            }
        }

        private void WatchObstacles(Vector3 position, Vector3 desired)
        {
            const float reach = 1.6f;
            RaycastHit hit;
            if (!Probe(position, desired, reach, out hit)) return;

            Vector3 normal = Flat(hit.normal);
            if (normal.sqrMagnitude < 1e-4f) normal = -desired;
            normal.Normalize();

            // De quel côté passer : celui qui est libre (en essayant ±35°, puis ±70°).
            Vector3 left = Quaternion.AngleAxis(-35f, Vector3.up) * desired;
            Vector3 rightDir = Quaternion.AngleAxis(35f, Vector3.up) * desired;
            RaycastHit probe;
            bool leftFree = !Probe(position, left, reach, out probe);
            bool rightFree = !Probe(position, rightDir, reach, out probe);
            if (!leftFree && !rightFree)
            {
                left = Quaternion.AngleAxis(-70f, Vector3.up) * desired;
                rightDir = Quaternion.AngleAxis(70f, Vector3.up) * desired;
                leftFree = !Probe(position, left, 1.1f, out probe);
                rightFree = !Probe(position, rightDir, 1.1f, out probe);
            }

            float urgency = 1f - hit.distance / reach;
            if (rightFree) _wallSteer += (rightDir - desired) * (1.4f * urgency + 0.3f);
            else if (leftFree) _wallSteer += (left - desired) * (1.4f * urgency + 0.3f);
            else if (hit.distance < 0.5f) _blocked = true;

            _wallSteer += normal * urgency * 0.4f;
        }

        private bool Probe(Vector3 position, Vector3 direction, float reach, out RaycastHit nearest)
        {
            nearest = new RaycastHit();
            bool found = false;
            float best = float.MaxValue;
            float[] heights = { 0.45f, 1.05f };
            for (int h = 0; h < heights.Length; h++)
            {
                int count = Physics.SphereCastNonAlloc(position + Vector3.up * heights[h], Radius * 0.9f, direction, _hits, reach, ~0,
                    QueryTriggerInteraction.Ignore);
                for (int i = 0; i < count; i++)
                {
                    RaycastHit hit = _hits[i];
                    if (hit.collider == null || !Solid(hit.collider) || hit.distance <= 0f) continue;
                    if (hit.normal.y > 0.55f) continue;
                    if (hit.distance < best)
                    {
                        best = hit.distance;
                        nearest = hit;
                        found = true;
                    }
                }
            }

            return found;
        }

        /// <summary>Au bord du trottoir, la chaussée devant : une voiture arrive ? On attend qu'elle passe.</summary>
        private void WatchCrossing(Vector3 position, Vector3 desired)
        {
            float here = TrafficSystem.RoadDistance(position);
            Vector3 ahead = position + desired * 2.6f;
            float there = TrafficSystem.RoadDistance(ahead);

            // Sur la chaussée : on presse le pas.
            if (here < 2.2f) _hurry = Mathf.Max(_hurry, 1.25f);

            bool atCurb = here > 2.4f && there < 2.1f;
            if (_activity == Activity.WaitToCross)
            {
                _crossWait += 0.18f;
                bool coming = TrafficSystem.CarComing(position + desired * 4f, 4.5f);
                if (!coming || _crossWait > 15f || !atCurb)
                {
                    _activity = Activity.None;
                    _facing = Vector3.zero;
                }

                return;
            }

            if (atCurb && _pause <= 0f && TrafficSystem.CarComing(position + desired * 4f, 4.5f))
            {
                _activity = Activity.WaitToCross;
                _crossWait = 0f;
                _facing = desired;
            }
        }

        private void LookForChat(Vector3 position)
        {
            _nextChatCheck = Time.time + 1f;
            for (int i = 0; i < Walkers.Count; i++)
            {
                MocapWalker other = Walkers[i];
                if (other == null || other == this || !other._sociable || other._oneWay || other._resting || other.IsDown) continue;
                if (other._activity != Activity.None || other._pause > 0f || other._partner != null) continue;

                Vector3 offset = Flat(other.transform.position - position);
                float distance = offset.magnitude;
                if (distance > 2.6f || distance < 0.6f) continue;
                if (Vector3.Dot(_heading, other._heading) > -0.5f) continue;

                // Une rencontre sur six seulement : sinon toute la ville bavarde.
                if (Random.value > 0.16f)
                {
                    _nextChatCheck = Time.time + 12f;
                    return;
                }

                float duration = Random.Range(5f, 10f);
                BeginChat(other, offset / distance, duration);
                other.BeginChat(this, -offset / distance, duration);
                return;
            }
        }

        private void BeginChat(MocapWalker partner, Vector3 facing, float duration)
        {
            _partner = partner;
            _activity = Activity.Chat;
            _facing = facing;
            _pause = duration;
            _nextChatCheck = Time.time + duration + 25f;
        }

        private void EndChat()
        {
            _partner = null;
            if (_activity == Activity.Chat)
            {
                _activity = Activity.None;
                _pause = 0f;
                _facing = Vector3.zero;
            }
        }

        /// <summary>Une pause à un coin : regarder autour, son téléphone, une vitrine.</summary>
        private void StartPause(Vector3 position)
        {
            _pause = Random.Range(_pauseDuration.x, _pauseDuration.y);
            _facing = Vector3.zero;

            // Une vitrine (un mur tout près, d'un côté) ?
            Vector3 right = new Vector3(_heading.z, 0f, -_heading.x);
            RaycastHit hit;
            bool wallRight = Probe(position, right, 3.2f, out hit);
            Vector3 wallNormal = Flat(hit.normal);
            bool wallLeft = !wallRight && Probe(position, -right, 3.2f, out hit);
            if (wallLeft) wallNormal = Flat(hit.normal);

            float roll = Random.value;
            if ((wallRight || wallLeft) && roll < 0.35f)
            {
                _activity = Activity.Window;
                _facing = wallNormal.sqrMagnitude > 1e-4f ? -wallNormal.normalized : (wallRight ? right : -right);
                _lookPoint = hit.point + Vector3.up * 0.4f;
            }
            else if (roll < 0.7f)
            {
                _activity = Activity.Phone;
            }
            else
            {
                _activity = Activity.LookAround;
            }
        }

        private void EndActivity()
        {
            if (_activity == Activity.Chat && _partner != null)
            {
                MocapWalker partner = _partner;
                _partner = null;
                if (partner._partner == this) partner.EndChat();
            }

            _activity = Activity.None;
            _facing = Vector3.zero;
        }

        /// <summary>Le chemin est barré : il prend le point d'après (et, hors de vue, s'y rend).</summary>
        private void SkipAhead(Vector3 position)
        {
            if (_oneWay && _next >= _path.Length - 1) return;
            _next = _oneWay ? Mathf.Min(_path.Length - 1, _next + 1) : Advance(_next);

            if (!NearCamera(position, 60f) || !Visible(position))
            {
                Vector3 p = _path[_next];
                _body.position = p;
                transform.position = p;
                _hasGround = false;
            }
        }

        /// <summary>La voiture du joueur arrive droit dessus ? Un bond de côté — du côté libre.</summary>
        private void WatchTraffic(Vector3 position)
        {
            DrivableCar car = DrivableCar.Driven;
            if (car == null || car.Body == null || Time.time < _dodgeUntil) return;

            Vector3 velocity = car.Body.linearVelocity;
            velocity.y = 0f;
            float speed = velocity.magnitude;
            if (speed < 2.5f) return;

            Vector3 relative = position - car.transform.position;
            relative.y = 0f;
            if (relative.sqrMagnitude > 40f * 40f) return;

            float t = Mathf.Clamp(Vector3.Dot(relative, velocity) / (speed * speed), 0f, 1.6f);
            if (t <= 0f) return;

            Vector3 closest = relative - velocity * t;
            if (closest.magnitude > 2.3f) return;

            Vector3 away = closest.sqrMagnitude > 0.04f
                ? closest.normalized
                : Vector3.Cross(Vector3.up, velocity / speed) * (Random.value < 0.5f ? 1f : -1f);

            // Le côté choisi est un mur ? L'autre.
            RaycastHit hit;
            if (Probe(position, away, 1.4f, out hit) && !Probe(position, -away, 1.4f, out hit)) away = -away;

            _dodge = away;
            _dodgeUntil = Time.time + Mathf.Lerp(0.5f, 0.85f, Random.value);
            _hurryUntil = Time.time + 10f;
            _pause = 0f;
            EndActivity();
            Notice(car.transform.position + Vector3.up, 2.5f);
        }

        /// <summary>Quelque chose attire son regard (un klaxon, un cri, une voiture qui fonce).</summary>
        public void Notice(Vector3 point, float seconds)
        {
            _attention = point;
            _attentionUntil = Time.time + seconds;
        }

        /// <summary>Il s'arrête là (un témoin qui appelle la police) pendant <paramref name="seconds"/> s.</summary>
        public void HoldStill(float seconds)
        {
            _pause = Mathf.Max(_pause, seconds);
            _activity = Activity.Phone;
        }

        /// <summary>Il repart (l'appel est fini, ou on l'a dissuadé).</summary>
        public void Release()
        {
            _pause = 0f;
            EndActivity();
        }

        /// <summary>Il presse le pas (provoqué, bousculé, témoin d'une bagarre) pendant <paramref name="seconds"/> s.</summary>
        public void Hurry(float seconds)
        {
            _hurryUntil = Mathf.Max(_hurryUntil, Time.time + seconds);
            _pause = 0f;
            EndActivity();
        }

        // ------------------------------------------------------------------ la nuit

        /// <summary>La nuit, la plupart rentrent chez eux (ils disparaissent hors de vue, reviennent au matin).</summary>
        private void UpdateRest(Vector3 position)
        {
            if (Time.time < _nextRestCheck || _oneWay) return;
            _nextRestCheck = Time.time + 2f + Random.value;

            WorldClock clock = WorldClock.Instance;
            if (clock == null || _nightOwl)
            {
                if (_resting) SetResting(false);
                return;
            }

            float hour = clock.Hour;
            bool night = hour >= 23.5f || hour < 6f;
            if (night == _resting) return;
            if (NearCamera(position, 45f) && Visible(position)) return;
            SetResting(night);
        }

        private void SetResting(bool resting)
        {
            _resting = resting;
            Renderer[] renderers = GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = !resting;
            if (_collider != null) _collider.enabled = !resting;
            if (_phone != null) _phone.SetActive(false);
            if (resting) EndActivity();
        }

        // ------------------------------------------------------------------ renversé

        /// <summary>
        /// Renversé par une voiture : il tombe du côté où on l'a poussé, reste au sol un moment
        /// et se relève. Il ne bloque plus rien pendant ce temps.
        /// </summary>
        public void KnockOver(Vector3 push)
        {
            if (_downStage != 0 || !isActiveAndEnabled) return;

            push.y = 0f;
            _slide = push.normalized * Mathf.Clamp(push.magnitude * 0.3f, 1.5f, 6f);
            _hurryUntil = Time.time + 15f;
            _pause = 0f;
            _dodgeUntil = 0f;
            EndActivity();

            if (!_animated || _library == null)
            {
                // Sans les chutes capturées : bousculé, il laisse passer la voiture et repart.
                if (_collider != null) _collider.enabled = false;
                _downStage = 2;
                _downTimer = 1.2f;
                return;
            }

            // Poussé par derrière : il tombe en avant ; de face : sur le dos.
            bool fromBehind = Vector3.Dot(transform.forward, push) > 0f;
            AnimationClip fall = fromBehind && _library.knockDownFront != null ? _library.knockDownFront : _library.knockDownBack;
            if (fall == null) return;

            if (_collider != null) _collider.enabled = false;
            _downStage = 1;
            _downTimer = fall.length;
            PlayReaction(fall);
        }

        private void PlayReaction(AnimationClip clip)
        {
            if (!_graph.IsValid()) return;

            if (_react.IsValid())
            {
                _graph.Disconnect(_mixer, 2);
                _react.Destroy();
            }

            _react = AnimationClipPlayable.Create(_graph, clip);
            _react.SetApplyFootIK(false);
            _graph.Connect(_react, 0, _mixer, 2);
            _react.SetTime(0);
        }

        private void UpdateDown(float dt)
        {
            if (_downStage == 0) return;

            _downTimer -= dt;

            if (_downStage == 1 && _downTimer <= 0f)
            {
                // Au sol : la dernière image de la chute, figée.
                if (_react.IsValid()) _react.Pause();
                _downStage = 2;
                _downTimer = Random.Range(2.2f, 4f);
            }
            else if (_downStage == 2 && _downTimer <= 0f)
            {
                AnimationClip up = _library != null ? _library.gettingUp : null;
                if (up == null)
                {
                    StandUp();
                    return;
                }

                _downStage = 3;
                _downTimer = up.length;
                PlayReaction(up);
            }
            else if (_downStage == 3 && _downTimer <= 0f)
            {
                StandUp();
            }
        }

        private void StandUp()
        {
            _downStage = 0;
            if (_collider != null) _collider.enabled = true;
            _pause = Random.Range(0.5f, 1.5f);
        }

        /// <summary>Une bagarre éclate à côté : on presse le pas, sans courir — et on regarde.</summary>
        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (victim == null) return;
            if ((victim.transform.position - transform.position).sqrMagnitude < _fightRadius * _fightRadius)
            {
                _hurryUntil = Time.time + 8f;
                if (_activity != Activity.None && _activity != Activity.WaitToCross) Release();
                Notice(victim.transform.position + Vector3.up * 1.4f, 3f);
            }
        }

        private void Face(Vector3 direction, float t)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            _body.MoveRotation(Quaternion.Slerp(_body.rotation, wanted, t));
        }

        // ================================================================== animation

        private void Update()
        {
            UpdateDown(Time.deltaTime);
            if (_resting) return;

            // Le mélange marche / attente suit la vitesse, sans « patiner » au démarrage.
            float walk = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.05f, 0.75f, _currentSpeed));

            if (_animated && _mixer.IsValid())
            {
                float down = _downStage != 0 ? 1f : 0f;
                _mixer.SetInputWeight(0, (1f - walk) * (1f - down));
                _mixer.SetInputWeight(1, walk * (1f - down));
                _mixer.SetInputWeight(2, down);

                float clipSpeed = _library != null ? Mathf.Max(0.3f, _library.relaxedWalkSpeed) : 1.3f;
                _walk.SetSpeed(Mathf.Clamp(Mathf.Max(_currentSpeed, 0.6f) / clipSpeed, 0.6f, 1.7f));

                Wrap(_walk);
                Wrap(_idle);
            }
            else if (_locomotion != null)
            {
                _locomotion.SetState(transform.forward * _currentSpeed, true, 0f, 0f);
            }
        }

        /// <summary>
        /// Après l'animation capturée : la posture de chacun, le corps qui se penche dans les
        /// virages, la tête qui regarde quelque chose, et le téléphone en main.
        /// </summary>
        private void LateUpdate()
        {
            if (_rig == null || _resting || _downStage != 0) return;
            Vector3 position = transform.position;
            if (!NearCamera(position, 60f)) return;

            float dt = Time.deltaTime;
            Vector3 up = Vector3.up;
            Vector3 forward = transform.forward;
            Vector3 right = transform.right;

            // --- posture et virages
            Transform spine = _rig.Spine;
            if (spine != null)
            {
                float lean = _posture + (_hurry > 1.2f ? 4f : 0f) + (_activity == Activity.Phone ? 6f * _phoneWeight : 0f);
                float roll = Mathf.Clamp(-_turnRate * 0.035f, -5f, 5f) * Mathf.Clamp01(_currentSpeed);
                spine.rotation = Quaternion.AngleAxis(lean, right) * Quaternion.AngleAxis(roll, forward) * spine.rotation;
            }

            // --- regard
            float wantedYaw = 0f;
            float wantedPitch = 0f;
            Vector3 look;
            if (LookTarget(position, forward, out look, out wantedPitch))
            {
                Transform head = _rig.Head != null ? _rig.Head : transform;
                Vector3 d = look - head.position;
                Vector3 flat = Flat(d);
                if (flat.sqrMagnitude > 0.01f)
                {
                    wantedYaw = Vector3.SignedAngle(Flat(forward), flat, up);
                    wantedPitch = -Mathf.Atan2(d.y, flat.magnitude) * Mathf.Rad2Deg;
                }
            }
            else if (_activity == Activity.Phone)
            {
                wantedPitch = 30f;
            }
            else if (_activity == Activity.LookAround || _activity == Activity.WaitToCross)
            {
                float sweep = _activity == Activity.WaitToCross ? 0.7f : 0.35f;
                wantedYaw = Mathf.Sin(Time.time * sweep + _curiosity * 7f) * 60f;
                wantedPitch = -3f;
            }

            wantedYaw = Mathf.Clamp(wantedYaw, -75f, 75f);
            wantedPitch = Mathf.Clamp(wantedPitch, -20f, 35f);
            _yaw = Mathf.SmoothDampAngle(_yaw, wantedYaw, ref _yawVelocity, 0.28f);
            _pitch = Mathf.SmoothDampAngle(_pitch, wantedPitch, ref _pitchVelocity, 0.3f);

            // Le cou prend 40 %, la tête le reste ; au bavardage, de petits hochements.
            float nod = _activity == Activity.Chat ? Mathf.Sin(Time.time * 4.1f + _curiosity * 5f) * 3f * Mathf.Max(0f, Mathf.Sin(Time.time * 0.9f + _curiosity)) : 0f;
            if (_rig.Neck != null)
            {
                _rig.Neck.rotation = Quaternion.AngleAxis(_yaw * 0.4f, up) * Quaternion.AngleAxis(_pitch * 0.4f, right) * _rig.Neck.rotation;
            }

            if (_rig.Head != null)
            {
                _rig.Head.rotation = Quaternion.AngleAxis(_yaw * 0.6f, up) * Quaternion.AngleAxis(_pitch * 0.6f + nod, right) * _rig.Head.rotation;
            }

            // --- le téléphone
            float phone = _activity == Activity.Phone ? 1f : 0f;
            _phoneWeight = Mathf.MoveTowards(_phoneWeight, phone, dt * 2.2f);
            HoldPhone(forward, right, up);
        }

        /// <summary>Ce qu'il regarde, s'il regarde quelque chose (et une inclinaison de tête en prime).</summary>
        private bool LookTarget(Vector3 position, Vector3 forward, out Vector3 point, out float pitch)
        {
            point = Vector3.zero;
            pitch = 0f;

            if (_activity == Activity.Chat && _partner != null && _partner._rig != null && _partner._rig.Head != null)
            {
                point = _partner._rig.Head.position;
                return true;
            }

            if (Time.time < _attentionUntil)
            {
                point = _attention;
                return true;
            }

            if (_activity == Activity.Window)
            {
                point = _lookPoint;
                return true;
            }

            // Le joueur qui passe tout près, devant lui : un coup d'œil (pas à chaque fois).
            Transform player = TrafficDriver.Player;
            if (player != null && _activity != Activity.Phone)
            {
                Vector3 d = Flat(player.position - position);
                float distance = d.magnitude;
                if (distance < 6.5f && distance > 0.4f && Vector3.Dot(d / distance, forward) > 0.15f)
                {
                    if (Time.time < _lookUntil && _lookPoint == Vector3.zero)
                    {
                        point = player.position + Vector3.up * 1.6f;
                        return true;
                    }

                    if (Time.time >= _playerCooldown)
                    {
                        _playerCooldown = Time.time + Random.Range(7f, 14f);
                        if (Random.value < _curiosity)
                        {
                            _lookPoint = Vector3.zero;
                            _lookUntil = Time.time + Random.Range(1.2f, 2.8f);
                            point = player.position + Vector3.up * 1.6f;
                            return true;
                        }
                    }
                }
            }

            // De temps en temps, un coup d'œil sur le côté (une vitrine, la rue, quelqu'un).
            if (_activity == Activity.None && _currentSpeed > 0.3f)
            {
                if (Time.time >= _nextGlance)
                {
                    _nextGlance = Time.time + Random.Range(5f, 11f) / Mathf.Max(0.3f, _curiosity);
                    float side = Random.value < 0.5f ? -1f : 1f;
                    Vector3 dir = Quaternion.AngleAxis(side * Random.Range(35f, 70f), Vector3.up) * forward;
                    _lookPoint = position + dir * 5f + Vector3.up * Random.Range(1.2f, 2.2f);
                    _lookUntil = Time.time + Random.Range(0.7f, 1.4f);
                }

                if (Time.time < _lookUntil && _lookPoint != Vector3.zero)
                {
                    point = _lookPoint;
                    return true;
                }
            }

            return false;
        }

        /// <summary>Le bras droit remonte devant la poitrine, le téléphone dans la main, l'écran vers le visage.</summary>
        private void HoldPhone(Vector3 forward, Vector3 right, Vector3 up)
        {
            if (_phoneWeight <= 0.01f || !_handMeasured || _rig.RightArm == null || _rig.Chest == null)
            {
                if (_phone != null && _phone.activeSelf) _phone.SetActive(false);
                return;
            }

            IkLimb arm = _rig.RightArm;
            if (arm.End == null || arm.Upper == null || arm.Lower == null) return;

            float w = Mathf.SmoothStep(0f, 1f, _phoneWeight);
            Vector3 target = _rig.Chest.position + forward * 0.3f + right * 0.05f - up * 0.12f;
            Vector3 fingers = (up * 0.85f + forward * 0.35f - right * 0.2f).normalized;
            Vector3 palm = (-forward * 0.9f + up * 0.3f).normalized;
            Quaternion rotation = Quaternion.LookRotation(fingers, palm) * _rightHandRest;

            Vector3 wrist = arm.End.position;
            Vector3 elbow = arm.Lower.position;
            Vector3 animatedPole = elbow - (arm.Upper.position + wrist) * 0.5f;
            Vector3 pole = Vector3.Lerp(animatedPole.normalized, (-up * 0.75f + right * 0.55f - forward * 0.2f).normalized, w);
            arm.ApplyWorldPose(Vector3.Lerp(wrist, target, w), Quaternion.Slerp(arm.End.rotation, rotation, w), pole);
            arm.UpdateTwist();
            if (_rig.RightHand != null) _rig.RightHand.TargetGrip = Mathf.Lerp(0.3f, 0.75f, w);

            if (_phone == null) _phone = MakePhone();
            if (_phone == null) return;
            bool show = w > 0.55f;
            if (_phone.activeSelf != show) _phone.SetActive(show);
            if (!show) return;

            // Dans la paume : l'écran (face +Z du petit pavé) tourné vers le visage.
            Vector3 handFingers = arm.End.rotation * Quaternion.Inverse(_rightHandRest) * Vector3.forward;
            Vector3 handPalm = arm.End.rotation * Quaternion.Inverse(_rightHandRest) * Vector3.up;
            _phone.transform.SetPositionAndRotation(arm.End.position + handFingers * 0.085f + handPalm * 0.03f,
                Quaternion.LookRotation(handPalm, handFingers));
        }

        private GameObject MakePhone()
        {
            GameObject phone = GameObject.CreatePrimitive(PrimitiveType.Cube);
            phone.name = "Telephone (passant)";
            Collider c = phone.GetComponent<Collider>();
            if (c != null) Destroy(c);
            phone.transform.localScale = new Vector3(0.07f, 0.145f, 0.009f);
            phone.transform.SetParent(transform, true);

            if (_phoneMaterial == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Lit");
                if (shader == null) shader = Shader.Find("Standard");
                if (shader != null)
                {
                    _phoneMaterial = new Material(shader);
                    _phoneMaterial.name = "Telephone";
                    Color dark = new Color(0.04f, 0.045f, 0.05f);
                    if (_phoneMaterial.HasProperty("_BaseColor")) _phoneMaterial.SetColor("_BaseColor", dark);
                    if (_phoneMaterial.HasProperty("_Color")) _phoneMaterial.SetColor("_Color", dark);
                    if (_phoneMaterial.HasProperty("_Smoothness")) _phoneMaterial.SetFloat("_Smoothness", 0.8f);
                }
            }

            Renderer renderer = phone.GetComponent<Renderer>();
            if (renderer != null && _phoneMaterial != null) renderer.sharedMaterial = _phoneMaterial;
            if (renderer != null) renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            phone.SetActive(false);
            return phone;
        }

        // ------------------------------------------------------------------ outils

        /// <summary>Le point suivant du tour, dans le sens de marche de ce passant.</summary>
        private int Advance(int index)
        {
            int n = _path.Length;
            if (_oneWay) return Mathf.Min(index + 1, n - 1);
            return _reverse ? (index - 1 + n) % n : (index + 1) % n;
        }

        private static void Wrap(AnimationClipPlayable playable)
        {
            AnimationClip clip = playable.GetAnimationClip();
            if (clip == null || clip.length < 0.01f) return;

            double time = playable.GetTime();
            if (time > clip.length) playable.SetTime(time % clip.length);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static bool NearCamera(Vector3 position, float distance)
        {
            Camera camera = Camera.main;
            if (camera == null) return true;
            return (camera.transform.position - position).sqrMagnitude < distance * distance;
        }

        private static bool Visible(Vector3 position)
        {
            Camera camera = Camera.main;
            if (camera == null) return false;
            Vector3 viewport = camera.WorldToViewportPoint(position + Vector3.up);
            return viewport.z > 0f && viewport.x > -0.1f && viewport.x < 1.1f && viewport.y > -0.1f && viewport.y < 1.1f;
        }
    }
}
