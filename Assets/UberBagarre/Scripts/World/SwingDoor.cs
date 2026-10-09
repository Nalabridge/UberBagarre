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
            if (_ownsSound && _sound != null) Destroy(_sound);
            if (_rattle != null) Destroy(_rattle);
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
        }

        public void SetOpen(bool open, bool instant)
        {
            Prepare();
            if (open && !_open && _awayFromViewer) _openTarget = AwayAngle();
            _open = open;
            RefreshLabel();

            if (instant)
            {
                _angle = open ? _openTarget : 0f;
                transform.localRotation = _closed * Quaternion.AngleAxis(_angle, _axis);
                return;
            }

            if (_source != null && _sound != null) _source.PlayOneShot(_sound, 0.5f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
        }

        private void Update()
        {
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
            const int rate = 22050;
            int length = rate / 2;
            float[] data = new float[length];
            float phase = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)rate;
                float pitch = 240f + 60f * Mathf.Sin(t * 23f) + 30f * Mathf.Sin(t * 57f);
                phase += pitch / rate;
                phase -= Mathf.Floor(phase);

                float envelope = Mathf.Clamp01(t * 30f) * Mathf.Clamp01((0.5f - t) * 6f);
                float saw = phase * 2f - 1f;
                data[n] = saw * envelope * 0.12f * (0.6f + 0.4f * Mathf.Sin(t * 140f));
            }

            AudioClip clip = AudioClip.Create("Porte (gond)", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>La poignée qu'on secoue : trois claquements secs de pêne.</summary>
        private static AudioClip Rattle()
        {
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
