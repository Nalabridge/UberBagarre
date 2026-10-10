using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Une voiture de patrouille : gyrophares rouge et bleu, sirène, et un conducteur qui fonce
    /// vers le joueur (ou vers sa dernière position connue).
    ///
    /// À pied, elle s'arrête près de lui et deux agents descendent. En voiture, elle le colle
    /// et cherche à le percuter (en visant un peu devant lui). Coincée contre un mur, elle
    /// recule et repart.
    /// </summary>
    [RequireComponent(typeof(DrivableCar))]
    public class PoliceCar : MonoBehaviour
    {
        [SerializeField] private Light _red;
        [SerializeField] private Light _blue;
        [SerializeField] private Renderer _redLens;
        [SerializeField] private Renderer _blueLens;
        [SerializeField, Min(5f)] private float _topSpeed = 21f;

        private PoliceSystem _police;
        private DrivableCar _car;
        private AudioSource _siren;
        private AudioClip _sirenClip;
        private bool _duty = true;
        private bool _unloaded;
        private float _stuckFor;
        private float _reverseFor;
        private float _leaveSince;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        // L'itinéraire par les rues jusqu'au joueur (recalculé quand il bouge).
        private static RoadGraph _graph;
        private static bool _graphSearched;
        private readonly System.Collections.Generic.List<Vector2> _route = new System.Collections.Generic.List<Vector2>();
        private Vector3 _routeTo;
        private float _routeAt = -10f;
        private int _routeIndex;

        public bool OnDuty { get { return _duty; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _graph = null;
            _graphSearched = false;
        }

        public void Begin(PoliceSystem police)
        {
            _police = police;
            _car = GetComponent<DrivableCar>();
            _car.Kind = DrivableCar.Access.Circulation;
            _car.Autopilot = true;

            _siren = gameObject.AddComponent<AudioSource>();
            _siren.loop = true;
            _siren.spatialBlend = 1f;
            _siren.minDistance = 6f;
            _siren.maxDistance = 140f;
            Core.ChannelSource.Attach(_siren, Core.AudioChannel.Effects, 0.55f);
            _sirenClip = Siren();
            _siren.clip = _sirenClip;
            _siren.Play();
        }

        private void OnDestroy()
        {
            Core.SoundBank.Release(_sirenClip);
        }

        public void StandDown()
        {
            _duty = false;
            _leaveSince = Time.time;
            if (_siren != null) _siren.Stop();
        }

        public bool CanSee(Vector3 point, float range)
        {
            if (!_duty) return false;
            Vector3 eyes = transform.position + Vector3.up * 1.4f;
            if ((point - eyes).sqrMagnitude > range * range) return false;
            return PoliceSystem.Clear(eyes, point + Vector3.up * 1f);
        }

        private void Update()
        {
            // Les gyrophares : rouge, bleu, en alternance rapide.
            bool phase = Mathf.Repeat(Time.time * 3.2f, 1f) < 0.5f;
            if (_red != null) _red.intensity = _duty && phase ? 4f : 0f;
            if (_blue != null) _blue.intensity = _duty && !phase ? 4f : 0f;
            if (_redLens != null) _redLens.enabled = !_duty || phase;
            if (_blueLens != null) _blueLens.enabled = !_duty || !phase;
        }

        private void FixedUpdate()
        {
            if (_police == null || _car == null || _car.Occupied) return;

            Vector3 player = _police.PlayerPosition;
            float distance = PoliceSystem.Flat(player - transform.position).magnitude;

            if (!_duty)
            {
                _car.SetInput(0.4f, 0f, false);
                if (Time.time - _leaveSince > 15f || distance > 110f) Destroy(gameObject);
                return;
            }

            if (distance > 170f)
            {
                Destroy(gameObject);
                return;
            }

            bool sees = CanSee(player, 60f);
            Vector3 target = sees ? player : _police.LastSeen;

            // En voiture : on vise un peu devant lui, pour le couper.
            if (PlayerDriving.IsDriving && DrivableCar.Driven != null && DrivableCar.Driven.Body != null && sees)
            {
                target += DrivableCar.Driven.Body.linearVelocity * 0.6f;
            }

            // À pied : on s'arrête à quelques mètres, et les agents descendent.
            if (!PlayerDriving.IsDriving && distance < 13f && sees)
            {
                _car.SetInput(0f, 0f, true);
                if (!_unloaded && Mathf.Abs(_car.ForwardSpeed) < 1.5f)
                {
                    _unloaded = true;
                    _police.SpawnOfficer(transform.position + transform.right * 2.2f, true);
                    _police.SpawnOfficer(transform.position - transform.right * 2.2f, true);
                }

                return;
            }

            float dt = Time.fixedDeltaTime;
            if (_reverseFor > 0f)
            {
                _reverseFor -= dt;
                _car.SetInput(-0.7f, -Steer(target), false);
                return;
            }

            // Loin, ou sans le voir : par les rues (pas à travers les maisons). Tout près et en
            // vue : droit sur lui.
            float corner = 0f;
            bool direct = sees && distance < 28f;
            if (!direct)
            {
                Vector3 waypoint;
                if (FollowRoute(target, out waypoint, out corner)) target = waypoint;
            }

            float steer = Avoid(Steer(target));
            float speed = _car.ForwardSpeed;
            float wanted = Mathf.Lerp(6f, _topSpeed, Mathf.InverseLerp(10f, 60f, distance));
            // Ralentir avant les virages de l'itinéraire, comme un vrai conducteur.
            wanted = Mathf.Min(wanted, Mathf.Lerp(_topSpeed, 6.5f, Mathf.InverseLerp(15f, 85f, corner)));
            if (Mathf.Abs(steer) > 0.6f) wanted = Mathf.Min(wanted, 9f);
            float throttle = Mathf.Clamp((wanted - speed) * 0.35f + 0.2f, -1f, 1f);
            _car.SetInput(throttle, steer, false);

            _stuckFor = throttle > 0.3f && Mathf.Abs(speed) < 0.6f ? _stuckFor + dt : 0f;
            if (_stuckFor > 1.8f)
            {
                _stuckFor = 0f;
                _reverseFor = 1.4f;
            }
        }

        /// <summary>
        /// Le point de l'itinéraire à viser (quelques mètres devant), et l'angle du prochain
        /// virage (pour ralentir). Faux s'il n'y a pas de graphe des rues.
        /// </summary>
        private bool FollowRoute(Vector3 goal, out Vector3 waypoint, out float corner)
        {
            waypoint = goal;
            corner = 0f;

            if (!_graphSearched)
            {
                _graphSearched = true;
                CityMap map = FindAnyObjectByType<CityMap>();
                if (map != null) _graph = map.Graph;
            }

            if (_graph == null) return false;

            Vector3 position = transform.position;
            if (Time.time - _routeAt > 1.2f || PoliceSystem.Flat(goal - _routeTo).magnitude > 8f || _route.Count < 2)
            {
                _routeAt = Time.time;
                _routeTo = goal;
                _graph.Find(new Vector2(position.x, position.z), new Vector2(goal.x, goal.z), _route);
                _routeIndex = 0;
            }

            if (_route.Count < 2) return false;

            // Le point de l'itinéraire le plus proche, en avançant seulement.
            Vector2 here = new Vector2(position.x, position.z);
            float best = float.MaxValue;
            for (int i = _routeIndex; i < Mathf.Min(_route.Count, _routeIndex + 12); i++)
            {
                float d = (_route[i] - here).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    _routeIndex = i;
                }
            }

            // Viser plus loin quand on va vite.
            float ahead = 6f + Mathf.Abs(_car.ForwardSpeed) * 0.5f;
            int target = _routeIndex;
            float travelled = 0f;
            while (target + 1 < _route.Count && travelled < ahead)
            {
                travelled += (_route[target + 1] - _route[target]).magnitude;
                target++;
            }

            Vector2 w = _route[target];
            waypoint = new Vector3(w.x, position.y, w.y);

            // Le plus fort changement de cap dans les 25 m à venir.
            travelled = 0f;
            for (int i = _routeIndex + 1; i + 1 < _route.Count && travelled < 25f; i++)
            {
                Vector2 a = _route[i] - _route[i - 1];
                Vector2 b = _route[i + 1] - _route[i];
                travelled += a.magnitude;
                if (a.sqrMagnitude < 0.01f || b.sqrMagnitude < 0.01f) continue;
                corner = Mathf.Max(corner, Vector2.Angle(a, b));
            }

            return true;
        }

        private float Steer(Vector3 target)
        {
            Vector3 local = transform.InverseTransformPoint(target);
            float angle = Mathf.Atan2(local.x, Mathf.Max(0.5f, local.z)) * Mathf.Rad2Deg;
            if (local.z < 0f) angle = local.x >= 0f ? 90f : -90f;
            return Mathf.Clamp(angle / Mathf.Max(8f, _car.SteerLimit), -1f, 1f);
        }

        /// <summary>Deux rayons en éventail : un mur d'un côté, on braque de l'autre.</summary>
        private float Avoid(float steer)
        {
            float reach = 7f + Mathf.Abs(_car.ForwardSpeed) * 0.6f;
            Vector3 origin = transform.position + Vector3.up * 0.8f + transform.forward * 2f;
            float left = Probe(origin, Quaternion.Euler(0f, -22f, 0f) * transform.forward, reach);
            float right = Probe(origin, Quaternion.Euler(0f, 22f, 0f) * transform.forward, reach);
            if (left < reach && left < right) steer += Mathf.Lerp(0.9f, 0.2f, left / reach);
            else if (right < reach) steer -= Mathf.Lerp(0.9f, 0.2f, right / reach);
            return Mathf.Clamp(steer, -1f, 1f);
        }

        private float Probe(Vector3 origin, Vector3 direction, float reach)
        {
            int count = Physics.RaycastNonAlloc(origin, direction, _hits, reach, ~(1 << 2), QueryTriggerInteraction.Ignore);
            float nearest = reach;
            for (int i = 0; i < count; i++)
            {
                Collider c = _hits[i].collider;
                if (c == null || c.transform.IsChildOf(transform)) continue;
                if (DrivableCar.Driven != null && c.transform.IsChildOf(DrivableCar.Driven.transform)) continue;
                if (_hits[i].distance < nearest) nearest = _hits[i].distance;
            }

            return nearest;
        }

        /// <summary>La sirène « deux tons » à la française : 435 Hz / 580 Hz, en alternance.</summary>
        private static AudioClip Siren()
        {
            AudioClip real = Core.SoundBank.Real("Police/sirene");
            if (real != null) return real;

            // Une sirène « wail » : une note qui monte et redescend (650 → 1 450 Hz) toutes les
            // deux secondes, jouée par un haut-parleur à pavillon — une onde riche (harmoniques
            // impaires), la résonance du pavillon, un peu de saturation. La fréquence est
            // ajustée pour que la boucle contienne un nombre entier de périodes : pas de clic.
            const int rate = 32000;
            int length = rate * 4;
            float[] data = new float[length];
            float[] freq = new float[length];
            double cycles = 0.0;
            for (int n = 0; n < length; n++)
            {
                float t = (n / (float)rate) % 2f;
                float u = t < 1.25f ? Mathf.SmoothStep(0f, 1f, t / 1.25f) : 1f - Mathf.SmoothStep(0f, 1f, (t - 1.25f) / 0.75f);
                freq[n] = Mathf.Lerp(650f, 1450f, u);
                cycles += freq[n] / rate;
            }

            float fix = (float)(System.Math.Round(cycles) / cycles);
            float phase = 0f;
            float horn1 = 0f, horn2 = 0f, low = 0f;
            for (int pass = 0; pass < 2; pass++)
            {
                for (int n = 0; n < length; n++)
                {
                    phase += freq[n] * fix / rate;
                    phase -= Mathf.Floor(phase);
                    float w = phase * Mathf.PI * 2f;
                    float tone = Mathf.Sin(w) + Mathf.Sin(3f * w) / 3f + Mathf.Sin(5f * w) / 5f + Mathf.Sin(7f * w) / 7f;
                    // Le pavillon : une résonance large vers 1,8 kHz (filtre à deux pôles), et le grave adouci.
                    horn2 = horn2 * 0.82f + (tone - horn1) * 0.3f;
                    horn1 += horn2 * 0.35f;
                    low = low + (tone - low) * 0.35f;
                    float y = horn1 * 0.9f + low * 0.5f;
                    data[n] = (float)System.Math.Tanh(y * 1.6f) * 0.5f;
                }
            }

            AudioClip clip = AudioClip.Create("Sirene", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
