using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Une vraie porte : E l'ouvre, E la referme. Elle pivote autour de son gond (l'objet qui
    /// porte ce composant), en douceur, et son collider tourne avec elle — ouverte, on passe.
    ///
    /// Les portes de la ville convertie ont perdu leur script d'origine : fermées, elles
    /// muraient les pièces. Ce composant leur est posé au chargement de la ville.
    ///
    /// Elle s'ouvre du côté opposé au joueur, comme on pousse une porte : jamais dans la
    /// figure de celui qui l'ouvre. L'axe du gond est celui des axes de l'objet qui pointe le
    /// plus vers le ciel (les modèles importés ne sont pas tous orientés pareil).
    /// </summary>
    public class SwingDoor : MonoBehaviour
    {
        [SerializeField] private float _openAngle = -100f;

        [SerializeField]
        [Tooltip("S'ouvre du cote oppose au joueur (sinon toujours du cote de _openAngle).")]
        private bool _awayFromViewer = true;
        [SerializeField, Min(10f)] private float _speed = 240f;
        [SerializeField] private string _openLabel = "Ouvrir la porte";
        [SerializeField] private string _closeLabel = "Fermer la porte";
        [SerializeField] private string _lockedLabel = "Fermé à clé";
        [SerializeField] private AudioClip _sound;

        private Interactable _interactable;
        private Quaternion _closed;
        private float _angle;
        private bool _open;
        private bool _locked;
        private string _lockedHint;
        private AudioSource _source;
        private AudioClip _rattle;
        private bool _ownsSound;
        private Vector3 _axis = Vector3.up;
        private Vector3 _leaf;
        private float _openTarget;

        // Baie vitrée : le battant glisse au lieu de pivoter (repères « Closed » / « Open »).
        private bool _slide;
        private Vector3 _slideClosed;
        private Vector3 _slideOpen;
        private float _slideT;

        // L'autre battant d'une porte double : il s'ouvre et se ferme avec celui-ci.
        private SwingDoor _partner;

        /// <summary>Celui qui pousse les portes (le joueur) : elles s'ouvrent loin de lui.</summary>
        public static Transform Viewer { get; set; }

        public bool IsOpen { get { return _open; } }

        /// <summary>
        /// Une porte fermée à clé ne s'ouvre pas : E fait juste trembler la poignée. Les maisons
        /// à vendre sont fermées tant qu'on ne les a pas achetées.
        /// </summary>
        public bool Locked
        {
            get { return _locked; }
        }

        public void SetLocked(bool locked, string hint)
        {
            Prepare();
            _locked = locked;
            _lockedHint = hint;
            if (locked && _open) SetOpen(false, false);
            RefreshLabel();
        }

        /// <summary>Pose une porte sur un gond existant (à l'exécution).</summary>
        public static SwingDoor Install(Transform hinge, float openAngle, bool startOpen)
        {
            if (hinge == null) return null;

            SwingDoor door = hinge.GetComponent<SwingDoor>();
            if (door == null)
            {
                // Un collider qui bouge sans corps rigide oblige la physique à reconstruire la
                // scène statique à chaque image : le battant devient un corps cinématique.
                if (hinge.GetComponent<Rigidbody>() == null)
                {
                    Rigidbody body = hinge.gameObject.AddComponent<Rigidbody>();
                    body.isKinematic = true;
                    body.useGravity = false;
                }

                door = hinge.gameObject.AddComponent<SwingDoor>();
            }

            door._openAngle = openAngle;
            door.Prepare();
            if (startOpen) door.SetOpen(true, true);
            return door;
        }

        /// <summary>
        /// Pose une baie vitrée : le battant glisse de sa place jusqu'au décalage entre les repères
        /// « Closed » et « Open » ; E l'ouvre et la ferme, et elle se verrouille comme une porte.
        /// </summary>
        public static SwingDoor InstallSliding(Transform panel, Transform closed, Transform open)
        {
            if (panel == null || closed == null || open == null || panel.parent == null) return null;

            SwingDoor door = Install(panel, 0f, false);
            door._slide = true;
            door._slideClosed = panel.localPosition;
            door._slideOpen = panel.localPosition + panel.parent.InverseTransformVector(open.position - closed.position);
            door._slideT = 0f;
            return door;
        }

        /// <summary>Les deux battants d'une porte double : ouvrir l'un ouvre l'autre.</summary>
        public static void Pair(SwingDoor a, SwingDoor b)
        {
            if (a == null || b == null || a == b) return;
            a._partner = b;
            b._partner = a;
        }

        /// <summary>Le battant d'en face (porte double), ou null.</summary>
        public SwingDoor Partner { get { return _partner; } }

        /// <summary>
        /// Le battant mesuré (repère du monde) : centre et taille de ses rendus — ce qui sert à
        /// reconnaître les deux battants d'une porte double.
        /// </summary>
        public Bounds LeafBounds
        {
            get
            {
                bool any;
                Bounds b = CityRules.RendererBounds(transform, out any);
                return any ? b : new Bounds(transform.position, Vector3.zero);
            }
        }

        private void Awake()
        {
            Prepare();
        }

        private void Prepare()
        {
            if (_interactable != null) return;

            _closed = transform.localRotation;
            _axis = UpAxis(transform);
            _openTarget = _openAngle;

            // Le battant : là où sont ses rendus (le gond est sur un bord).
            bool any;
            Bounds leaf = CityRules.RendererBounds(transform, out any);
            _leaf = any ? transform.InverseTransformPoint(leaf.center) : Vector3.forward * 0.45f;

            _interactable = GetComponent<Interactable>();
            if (_interactable == null) _interactable = gameObject.AddComponent<Interactable>();
            _interactable.Label = _openLabel;
            _interactable.Activated += OnActivated;

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.maxDistance = 18f;
            if (_sound == null)
            {
                _sound = Creak();
                _ownsSound = true;
            }
        }

        private void RefreshLabel()
        {
            if (_interactable == null) return;
            _interactable.Label = _locked ? _lockedLabel : _open ? _closeLabel : _openLabel;
            _interactable.Hint = _locked ? _lockedHint : null;
        }

        private void OnDestroy()
        {
            if (_interactable != null) _interactable.Activated -= OnActivated;
            if (_ownsSound) Core.SoundBank.Release(_sound);
            Core.SoundBank.Release(_rattle);
        }

        private void OnActivated(Interactable source)
        {
            if (_locked)
            {
                if (_rattle == null) _rattle = Rattle();
                if (_source != null) _source.PlayOneShot(_rattle, 0.6f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
                return;
            }

            SetOpen(!_open, false);

            // Porte double : l'autre battant suit (chacun s'ouvre loin du joueur, donc en miroir).
            if (_partner != null && !_partner._locked && _partner._open != _open) _partner.SetOpen(_open, false, false);
        }

        public void SetOpen(bool open, bool instant)
        {
            SetOpen(open, instant, true);
        }

        private void SetOpen(bool open, bool instant, bool sound)
        {
            Prepare();
            if (open && !_open && _awayFromViewer && !_slide) _openTarget = AwayAngle();
            _open = open;
            RefreshLabel();

            if (instant)
            {
                if (_slide)
                {
                    _slideT = open ? 1f : 0f;
                    transform.localPosition = Vector3.Lerp(_slideClosed, _slideOpen, _slideT);
                    return;
                }

                _angle = open ? _openTarget : 0f;
                transform.localRotation = _closed * Quaternion.AngleAxis(_angle, _axis);
                return;
            }

            if (sound && _source != null && _sound != null) _source.PlayOneShot(_sound, 0.5f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
        }

        private void Update()
        {
            if (_slide)
            {
                float goal = _open ? 1f : 0f;
                if (Mathf.Approximately(_slideT, goal)) return;
                _slideT = Mathf.MoveTowards(_slideT, goal, Time.deltaTime * 1.6f);
                float eased = _slideT * _slideT * (3f - 2f * _slideT);
                transform.localPosition = Vector3.Lerp(_slideClosed, _slideOpen, eased);
                return;
            }

            float target = _open ? _openTarget : 0f;
            if (Mathf.Approximately(_angle, target)) return;

            _angle = Mathf.MoveTowards(_angle, target, _speed * Time.deltaTime);
            transform.localRotation = _closed * Quaternion.AngleAxis(_angle, _axis);
        }

        /// <summary>
        /// L'angle d'ouverture qui éloigne le battant du joueur. Tourner d'un angle positif
        /// autour de la verticale déplace le bord libre suivant (haut × bras de levier) : si ce
        /// sens va vers le joueur, on tourne dans l'autre.
        /// </summary>
        private float AwayAngle()
        {
            float magnitude = Mathf.Abs(_openAngle);
            Transform viewer = Viewer;
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            if (viewer == null) return _openAngle;

            Quaternion closedWorld = transform.parent != null ? transform.parent.rotation * _closed : _closed;
            Vector3 up = closedWorld * _axis;
            Vector3 arm = closedWorld * Vector3.Scale(_leaf, transform.lossyScale);
            arm -= up * Vector3.Dot(arm, up);
            if (arm.sqrMagnitude < 1e-4f) return _openAngle;

            Vector3 sweep = Vector3.Cross(up, arm);
            Vector3 toViewer = viewer.position - transform.position;
            return Vector3.Dot(sweep, toViewer) > 0f ? -magnitude : magnitude;
        }

        /// <summary>L'axe local de l'objet le plus proche de la verticale, orienté vers le haut.</summary>
        private static Vector3 UpAxis(Transform t)
        {
            float y = Vector3.Dot(t.up, Vector3.up);
            float x = Vector3.Dot(t.right, Vector3.up);
            float z = Vector3.Dot(t.forward, Vector3.up);

            if (Mathf.Abs(y) >= Mathf.Abs(x) && Mathf.Abs(y) >= Mathf.Abs(z)) return y >= 0f ? Vector3.up : Vector3.down;
            if (Mathf.Abs(x) >= Mathf.Abs(z)) return x >= 0f ? Vector3.right : Vector3.left;
            return z >= 0f ? Vector3.forward : Vector3.back;
        }

        /// <summary>Un grincement de gond, synthétisé une fois.</summary>
        private static AudioClip Creak()
        {
            AudioClip real = Core.SoundBank.Real("Portes/grincement");
            if (real != null) return real;

            // Un vrai grincement est un frottement qui accroche et lâche (stick-slip) : une suite
            // d'à-coups irréguliers dont le rythme glisse, qui font chanter le bois du battant
            // (trois résonances). Précédé du déclic de la poignée et suivi du souffle de l'air.
            const int rate = 32000;
            int length = Mathf.RoundToInt(rate * 0.9f);
            float[] data = new float[length];
            System.Random random = new System.Random(19);
            float[] modes = { 310f, 760f, 1480f };
            float[] state1 = new float[3];
            float[] state2 = new float[3];
            float next = 0.06f * rate;
            for (int n = 0; n < length; n++)
            {
                float t = n / (float)rate;
                float excite = 0f;

                // Le déclic du pêne.
                if (t < 0.018f) excite += (float)(random.NextDouble() * 2.0 - 1.0) * (1f - t / 0.018f) * 0.8f;

                // Les à-coups du gond, de plus en plus rapprochés puis qui ralentissent.
                if (n >= next && t < 0.78f)
                {
                    float progress = t / 0.78f;
                    float rateHz = 70f + 260f * Mathf.Sin(progress * Mathf.PI) * (0.8f + 0.4f * Mathf.Sin(t * 17f));
                    next = n + rate / Mathf.Max(40f, rateHz) * (0.85f + 0.3f * (float)random.NextDouble());
                    excite += 0.7f + 0.3f * (float)random.NextDouble();
                }

                float y = 0f;
                for (int m = 0; m < 3; m++)
                {
                    // Résonateur à deux pôles (fréquence du mode, amortissement modéré).
                    float w = 2f * Mathf.PI * modes[m] / rate;
                    float r = 0.993f - m * 0.002f;
                    float v = excite + 2f * r * Mathf.Cos(w) * state1[m] - r * r * state2[m];
                    state2[m] = state1[m];
                    state1[m] = v;
                    y += v * (m == 0 ? 0.05f : m == 1 ? 0.035f : 0.02f);
                }

                float air = t > 0.2f ? (float)(random.NextDouble() * 2.0 - 1.0) * 0.015f * Mathf.Sin(Mathf.Clamp01((t - 0.2f) / 0.7f) * Mathf.PI) : 0f;
                float envelope = Mathf.Clamp01((0.9f - t) * 8f);
                data[n] = Mathf.Clamp((y + air) * envelope, -1f, 1f) * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Porte (gond)", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>La poignée qu'on secoue : trois claquements secs de pêne.</summary>
        private static AudioClip Rattle()
        {
            AudioClip real = Core.SoundBank.Real("Portes/verrouillee");
            if (real != null) return real;

            const int rate = 22050;
            int length = (int)(rate * 0.42f);
            float[] data = new float[length];
            System.Random random = new System.Random(7);

            for (int k = 0; k < 3; k++)
            {
                int start = (int)(rate * (0.02f + k * 0.13f));
                for (int n = 0; n < rate / 20 && start + n < length; n++)
                {
                    float t = n / (float)rate;
                    float envelope = Mathf.Exp(-t * 90f);
                    float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                    float ring = Mathf.Sin(t * 2f * Mathf.PI * (1900f + k * 140f));
                    data[start + n] += (noise * 0.5f + ring * 0.5f) * envelope * 0.35f;
                }
            }

            AudioClip clip = AudioClip.Create("Porte (verrouillee)", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
