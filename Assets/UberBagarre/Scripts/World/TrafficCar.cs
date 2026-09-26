using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Une voiture qui roule : elle suit sa boucle de rues, sur la voie de droite, ralentit
    /// dans les virages et freine pour ce qui bouge devant elle — un piéton, le joueur, une
    /// autre voiture. Le décor fixe, elle le connaît : sa route ne passe pas dedans.
    ///
    /// Deux voitures qui s'attendent à un carrefour ne s'attendront pas pour toujours : au bout
    /// de quelques secondes arrêtée, la plus patiente passe.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    public class TrafficCar : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Points de la boucle (monde), deja decales sur la voie de droite.")]
        private Vector3[] _path = new Vector3[0];

        [SerializeField] private int _startIndex;
        [SerializeField, Min(1f)] private float _cruise = 9f;
        [SerializeField, Min(0.5f)] private float _acceleration = 2.8f;
        [SerializeField, Min(0.5f)] private float _braking = 7f;

        [SerializeField, Min(0.5f)]
        [Tooltip("Demi-largeur du balayage devant le pare-chocs.")]
        private float _scanRadius = 0.9f;

        [SerializeField, Min(1f)] private float _halfLength = 2.3f;

        [SerializeField]
        [Tooltip("Klaxon, quand le joueur reste plante devant.")]
        private AudioSource _horn;

        private Rigidbody _body;
        private int _next;
        private float _speed;
        private float _stoppedFor;
        private float _patientUntil;
        private float _honkAt;
        private readonly RaycastHit[] _hits = new RaycastHit[12];

        public void SetPath(Vector3[] path, int start, float cruise)
        {
            _path = path;
            _startIndex = start;
            _cruise = cruise;
        }

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();
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

            int start = _startIndex % _path.Length;
            _next = (start + 1) % _path.Length;
            transform.position = _path[start];

            Vector3 direction = _path[_next] - _path[start];
            direction.y = 0f;
            if (direction.sqrMagnitude > 1e-4f) transform.rotation = Quaternion.LookRotation(direction.normalized, Vector3.up);
        }

        private void FixedUpdate()
        {
            if (_path == null || _path.Length < 2) return;

            float dt = Time.fixedDeltaTime;
            Vector3 position = _body.position;
            Vector3 target = _path[_next];
            Vector3 to = target - position;
            to.y = 0f;

            if (to.magnitude < 1.2f)
            {
                _next = (_next + 1) % _path.Length;
                return;
            }

            // On regarde un peu plus loin que le prochain point : les virages s'arrondissent.
            Vector3 after = _path[(_next + 1) % _path.Length] - target;
            after.y = 0f;
            float corner = after.sqrMagnitude > 1e-4f
                ? Vector3.Angle(to, after) / 90f
                : 0f;
            float approach = Mathf.Clamp01(to.magnitude / 18f);
            float wanted = _cruise * Mathf.Lerp(0.45f, 1f, Mathf.Lerp(1f - Mathf.Clamp01(corner), 1f, approach));

            float obstacle = Obstacle(position, transform.forward);
            if (obstacle < float.MaxValue)
            {
                float room = Mathf.Max(0f, obstacle - 1.5f);
                wanted = Mathf.Min(wanted, Mathf.Sqrt(2f * _braking * room));
            }

            float rate = wanted < _speed ? _braking : _acceleration;
            _speed = Mathf.MoveTowards(_speed, wanted, rate * dt);

            _stoppedFor = _speed < 0.2f ? _stoppedFor + dt : 0f;

            Vector3 heading = Vector3.Slerp(transform.forward, to.normalized, 1f - Mathf.Exp(-2.4f * dt * Mathf.Max(1f, _speed * 0.4f)));
            heading.y = 0f;
            if (heading.sqrMagnitude > 1e-4f) _body.MoveRotation(Quaternion.LookRotation(heading.normalized, Vector3.up));

            Vector3 step = transform.forward * (_speed * dt);
            Vector3 next = position + step;
            next.y = Mathf.Lerp(position.y, target.y, 1f - Mathf.Exp(-4f * dt));
            _body.MovePosition(next);
        }

        /// <summary>Distance au premier obstacle MOBILE devant le pare-chocs, ou MaxValue.</summary>
        private float Obstacle(Vector3 position, Vector3 forward)
        {
            float reach = 4f + _speed * _speed / (2f * _braking) + _speed * 0.6f;
            Vector3 origin = position + Vector3.up * 1f + forward * (_halfLength - _scanRadius);

            int count = Physics.SphereCastNonAlloc(origin, _scanRadius, forward, _hits, reach, ~0,
                QueryTriggerInteraction.Ignore);

            float nearest = float.MaxValue;
            bool player = false;

            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;

                bool person = c is CharacterController || c.GetComponentInParent<MocapWalker>() != null ||
                              c.GetComponentInParent<Combatant>() != null;
                bool car = c.GetComponentInParent<TrafficCar>() != null;
                if (!person && !car) continue;

                // Une autre voiture, et on attend depuis trop longtemps : on passe.
                if (car && Time.time < _patientUntil) continue;

                if (_hits[i].distance < nearest)
                {
                    nearest = _hits[i].distance;
                    player = c is CharacterController && c.GetComponent<Combatant>() != null &&
                             c.GetComponent<Combatant>().Faction == Faction.Player;
                }
            }

            if (nearest < float.MaxValue && _stoppedFor > 4f && !player) _patientUntil = Time.time + 2.5f;

            if (player && _stoppedFor > 2.5f && Time.time >= _honkAt && _horn != null)
            {
                _horn.Play();
                _honkAt = Time.time + 4f;
            }

            return nearest;
        }
    }
}
