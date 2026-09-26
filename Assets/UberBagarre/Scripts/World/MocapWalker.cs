using UberBagarre.Combat;
using UberBagarre.View;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UberBagarre.World
{
    /// <summary>
    /// Un passant : il fait le tour de son pâté de maisons sur le trottoir, à son pas, avec la
    /// marche et l'attente capturées du paquet FS (Third Person Controller).
    ///
    /// Ce n'est pas un combattant : ni vie, ni cerveau, ni zones de frappe — une ville a besoin
    /// de monde qui passe, pas de cibles. Il s'arrête quand on lui barre la route, attend un
    /// peu à certains coins (on s'arrête pour regarder son téléphone, pour attendre quelqu'un),
    /// et presse le pas quand une bagarre éclate près de lui.
    ///
    /// Une voiture qui fonce sur lui : il se jette sur le côté. Trop tard : il est renversé
    /// (la chute capturée), reste un moment au sol et se relève.
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

        private Rigidbody _body;
        private int _next;
        private float _pause;
        private float _currentSpeed;
        private float _hurry = 1f;
        private float _nextScan;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable _idle;
        private AnimationClipPlayable _walk;
        private bool _animated;
        private AnimationClipPlayable _react;

        private Collider _collider;
        private float _dodgeUntil;
        private Vector3 _dodge;
        private int _downStage;          // 0 debout, 1 chute, 2 au sol, 3 relevage
        private float _downTimer;
        private Vector3 _slide;

        /// <summary>Vrai tant qu'il est au sol (renversé) ou en train de se relever.</summary>
        public bool IsDown { get { return _downStage != 0; } }

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
        }

        private void Start()
        {
            if (_path == null || _path.Length < 2)
            {
                enabled = false;
                return;
            }

            _next = (_startIndex + 1) % _path.Length;
            transform.position = _path[_startIndex % _path.Length];
            Face(_path[_next] - transform.position, 1f);

            BuildGraph();

            if (_locomotion != null) _locomotion.enabled = !_animated;

            // Des doigts à demi fermés : une main qui marche ne serre pas le poing.
            if (_rig != null)
            {
                if (_rig.LeftHand != null) _rig.LeftHand.TargetGrip = 0.3f;
                if (_rig.RightHand != null) _rig.RightHand.TargetGrip = 0.3f;
            }
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
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

        private void FixedUpdate()
        {
            if (_path == null || _path.Length < 2) return;

            float dt = Time.fixedDeltaTime;
            Vector3 position = _body.position;

            if (_downStage != 0)
            {
                // Renversé : il glisse sur l'élan du choc, puis ne bouge plus.
                _slide = Vector3.MoveTowards(_slide, Vector3.zero, dt * 9f);
                _currentSpeed = 0f;
                if (_slide.sqrMagnitude > 1e-4f) _body.MovePosition(position + _slide * dt);
                return;
            }

            if (Time.time < _dodgeUntil)
            {
                _currentSpeed = Mathf.MoveTowards(_currentSpeed, 3.4f, dt * 14f);
                _body.MovePosition(position + _dodge * (_currentSpeed * dt));
                Face(_dodge, 1f - Mathf.Exp(-10f * dt));
                return;
            }

            Vector3 target = _path[_next];
            Vector3 to = target - position;
            to.y = 0f;

            if (to.magnitude < 0.35f)
            {
                _next = (_next + 1) % _path.Length;
                if (Random.value < _pauseChance) _pause = Random.Range(_pauseDuration.x, _pauseDuration.y);
                return;
            }

            if (Time.time >= _nextScan)
            {
                _nextScan = Time.time + 0.25f + Random.value * 0.1f;
                Scan(position, to.normalized);
            }

            float wanted = _pause > 0f || _blocked ? 0f : _speed * _hurry;
            if (_pause > 0f) _pause -= dt;

            _currentSpeed = Mathf.MoveTowards(_currentSpeed, wanted, dt * 2.2f);

            Vector3 direction = to.normalized;
            Vector3 step = direction * (_currentSpeed * dt);
            Vector3 next = position + step;
            next.y = Mathf.Lerp(position.y, target.y, 1f - Mathf.Exp(-6f * dt));

            _body.MovePosition(next);
            Face(direction, 1f - Mathf.Exp(-5f * dt));
        }

        private bool _blocked;

        /// <summary>Quelqu'un tout près, sur le chemin ? On s'arrête, on ne lui marche pas dessus.</summary>
        private void Scan(Vector3 position, Vector3 forward)
        {
            _blocked = false;
            _hurry = Time.time < _hurryUntil ? 1.55f : 1f;

            WatchTraffic(position);

            var all = Combatant.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant other = all[i];
                if (other == null) continue;

                Vector3 d = other.transform.position - position;
                d.y = 0f;
                if (d.magnitude < _personalSpace && Vector3.Dot(d, forward) > 0.2f)
                {
                    _blocked = true;
                    return;
                }
            }
        }

        private float _hurryUntil;

        /// <summary>La voiture du joueur arrive droit dessus ? Un bond de côté.</summary>
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

            _dodge = away;
            _dodgeUntil = Time.time + Mathf.Lerp(0.5f, 0.85f, Random.value);
            _hurryUntil = Time.time + 10f;
            _pause = 0f;
        }

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

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;
        }

        /// <summary>Une bagarre éclate à côté : on presse le pas, sans courir.</summary>
        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (victim == null) return;
            if ((victim.transform.position - transform.position).sqrMagnitude < _fightRadius * _fightRadius)
            {
                _hurryUntil = Time.time + 8f;
            }
        }

        private void Face(Vector3 direction, float t)
        {
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;

            Quaternion wanted = Quaternion.LookRotation(direction.normalized, Vector3.up);
            _body.MoveRotation(Quaternion.Slerp(_body.rotation, wanted, t));
        }

        private void Update()
        {
            float walk = Mathf.Clamp01(_currentSpeed / 0.35f);
            UpdateDown(Time.deltaTime);

            if (_animated && _mixer.IsValid())
            {
                float down = _downStage != 0 ? 1f : 0f;
                _mixer.SetInputWeight(0, (1f - walk) * (1f - down));
                _mixer.SetInputWeight(1, walk * (1f - down));
                _mixer.SetInputWeight(2, down);

                float clipSpeed = _library != null ? Mathf.Max(0.3f, _library.relaxedWalkSpeed) : 1.3f;
                _walk.SetSpeed(Mathf.Clamp(_currentSpeed / clipSpeed, 0.5f, 1.8f));

                Wrap(_walk);
                Wrap(_idle);
            }
            else if (_locomotion != null)
            {
                _locomotion.SetState(transform.forward * _currentSpeed, true, 0f, 0f);
            }
        }

        private static void Wrap(AnimationClipPlayable playable)
        {
            AnimationClip clip = playable.GetAnimationClip();
            if (clip == null || clip.length < 0.01f) return;

            double time = playable.GetTime();
            if (time > clip.length) playable.SetTime(time % clip.length);
        }
    }
}
