using UberBagarre.Combat;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Phone
{
    /// <summary>
    /// L'appareil photo, façon téléphone de jeu en monde ouvert : l'image ENTIÈRE devient le viseur.
    ///
    /// Avant, on cadrait à travers un petit rectangle vide dessiné sur la dalle : on ne voyait pas
    /// ce qu'on photographiait, et la « photo » n'existait nulle part. Maintenant, ouvrir Photo
    /// monte le téléphone à l'œil : les mains sortent du champ, la vue devient celle de
    /// l'objectif, avec sa grille, son cadre de mise au point, son zoom à la molette et ses
    /// filtres. Le déclencheur prend une VRAIE image (un rendu de la caméra, post-traitement
    /// compris) qui file en vignette dans le coin et se retrouve dans la Galerie.
    ///
    /// La preuve exigée par l'histoire passe par le même geste : le cadre dit qui est visé, et
    /// s'il est au sol. Il faut toujours viser — une preuve qu'on ne peut pas rater n'en est pas
    /// une — mais on sait ce qu'on vise.
    ///
    /// Commandes en mode photo : clic gauche = photo, molette ou haut/bas = zoom, gauche/droite =
    /// filtre, clic droit ou Retour arrière = quitter.
    /// </summary>
    [RequireComponent(typeof(PhoneDevice))]
    public class PhoneCamera : MonoBehaviour
    {
        private static readonly string[] FilterNames = { "NATUREL", "NOIR & BLANC", "VINTAGE", "NUIT", "NEON" };

        [Header("References")]
        [SerializeField] private PhoneDevice _device;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _camera;

        [SerializeField]
        [Tooltip("Optionnel : baisse la sensibilite de la visee quand on zoome.")]
        private PlayerLook _look;

        [SerializeField]
        [Tooltip("Optionnel : le compteur de preuves (« 1 / 2 ») affiche dans le viseur.")]
        private PhoneDisplay _display;

        [Header("Visee")]
        [SerializeField, Min(1f)] private float _maxDistance = 30f;

        [SerializeField, Min(0f)]
        [Tooltip("Rayon du balayage. Un corps au sol est bas et etroit : viser au pixel pres " +
                 "transformerait la photo en epreuve d'adresse.")]
        private float _sweepRadius = 0.45f;

        [SerializeField] private LayerMask _layers = ~0;

        [Header("Objectif")]
        [SerializeField, Range(1f, 8f)] private float _maxZoom = 5f;
        [SerializeField, Min(8)] private int _photoWidth = 640;

        [Header("Declenchement")]
        [SerializeField, Min(0.05f)] private float _flashDuration = 0.18f;
        [SerializeField, Min(0.1f)] private float _cooldown = 0.45f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];

        private bool _active;
        private float _baseFov = 75f;
        private float _zoom = 1f;
        private float _zoomTarget = 1f;
        private int _filter;
        private float _flash;
        private float _nextShot;
        private float _captureAge = 10f;
        private Texture2D _lastCapture;

        private UberPostProcess _post;
        private GraphicsDirector _graphics;
        private float _baseSaturation = 1f;
        private float _baseContrast = 1f;
        private float _baseExposure = 1f;

        private string _subject;
        private bool _subjectDown;
        private bool _subjectIsFighter;
        private float _nextAim;

        private AudioSource _audio;
        private AudioClip _shutter;

        private void Awake()
        {
            if (_device == null) _device = GetComponent<PhoneDevice>();
            if (_display == null) _display = GetComponent<PhoneDisplay>();

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _shutter = ShutterSound();
        }

        private void Start()
        {
            if (_camera != null) _post = _camera.GetComponent<UberPostProcess>();
            _graphics = FindAnyObjectByType<GraphicsDirector>();
            if (_look == null && _input != null) _look = _input.GetComponent<PlayerLook>();
        }

        private void OnDisable()
        {
            if (_active) Exit();
        }

        private void OnDestroy()
        {
            if (_shutter != null) Destroy(_shutter);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _flash = Mathf.MoveTowards(_flash, 0f, dt / _flashDuration);
            _captureAge += dt;

            if (_device == null) return;

            bool wanted = _device.CameraMode && _device.RaiseAmount > 0.92f;

            if (wanted && !_active) Enter();
            else if (!wanted && _active) Exit();

            if (!_active) return;

            HandleInput();
            ApplyZoom(dt);
            ApplyFilter();
            UpdateAim();
        }

        // --------------------------------------------------------------- entrée / sortie

        private void Enter()
        {
            _active = true;
            _zoom = 1f;
            _zoomTarget = 1f;

            if (_camera != null) _baseFov = _camera.fieldOfView;

            if (_post != null)
            {
                _baseSaturation = _post.Saturation;
                _baseContrast = _post.Contrast;
                _baseExposure = _post.Exposure;
            }
        }

        private void Exit()
        {
            _active = false;

            if (_camera != null) _camera.fieldOfView = _baseFov;
            if (_look != null) _look.SensitivityScale = 1f;

            // Le filtre est rendu au directeur graphique : il repousse les reglages du joueur.
            if (_graphics != null) _graphics.Push();
            else if (_post != null)
            {
                _post.Saturation = _baseSaturation;
                _post.Contrast = _baseContrast;
                _post.Exposure = _baseExposure;
            }
        }

        // --------------------------------------------------------------- commandes

        private void HandleInput()
        {
            if (_input == null || _input.Provider == null || _input.Bindings == null) return;

            float scroll = _input.ScrollDelta;
            if (_input.Provider.GetPressedThisFrame(_input.Bindings.phoneUp)) scroll += 1f;
            if (_input.Provider.GetPressedThisFrame(_input.Bindings.phoneDown)) scroll -= 1f;

            if (Mathf.Abs(scroll) > 0.01f)
            {
                _zoomTarget = Mathf.Clamp(_zoomTarget * Mathf.Pow(1.25f, scroll), 1f, _maxZoom);
            }

            if (_input.Provider.GetPressedThisFrame(_input.Bindings.phoneLeft))
            {
                _filter = (_filter + FilterNames.Length - 1) % FilterNames.Length;
            }

            if (_input.Provider.GetPressedThisFrame(_input.Bindings.phoneRight))
            {
                _filter = (_filter + 1) % FilterNames.Length;
            }

            if (Time.unscaledTime < _nextShot) return;

            // Lecture directe de la liaison : l'entree de combat est coupee tant que le
            // telephone est leve, donc StraightPressed arrive toujours a faux ici.
            if (!_input.Provider.GetPressedThisFrame(_input.Bindings.attackStraight)) return;

            _nextShot = Time.unscaledTime + _cooldown;
            Shoot();
        }

        private void ApplyZoom(float dt)
        {
            _zoom = Mathf.Lerp(_zoom, _zoomTarget, 1f - Mathf.Exp(-12f * dt));

            if (_camera != null)
            {
                float half = Mathf.Atan(Mathf.Tan(_baseFov * 0.5f * Mathf.Deg2Rad) / _zoom);
                _camera.fieldOfView = half * 2f * Mathf.Rad2Deg;
            }

            if (_look != null) _look.SensitivityScale = 1f / _zoom;
        }

        private void ApplyFilter()
        {
            if (_post == null) return;

            float saturation = _baseSaturation;
            float contrast = _baseContrast;
            float exposure = _baseExposure;

            switch (_filter)
            {
                case 1: saturation = 0f; contrast = _baseContrast * 1.18f; break;
                case 2: saturation = 0.42f; contrast = _baseContrast * 0.9f; exposure = _baseExposure * 1.06f; break;
                case 3: saturation = 0.55f; exposure = _baseExposure * 2.1f; break;
                case 4: saturation = 1.7f; contrast = _baseContrast * 1.22f; break;
            }

            _post.Saturation = saturation;
            _post.Contrast = contrast;
            _post.Exposure = exposure;
        }

        // --------------------------------------------------------------- prise de vue

        private void Shoot()
        {
            _flash = 1f;
            _captureAge = 0f;

            if (_audio != null && _shutter != null) _audio.PlayOneShot(_shutter, 0.7f);

            _lastCapture = Capture();
            if (_lastCapture != null) PhoneGallery.Add(_lastCapture);

            // La preuve n'est envoyee que quand l'histoire la demande : une photo de la rue pour
            // le plaisir ne doit pas passer pour un envoi au client.
            if (_device.Current == PhoneDevice.Screen.Photo) _device.TakePhoto(FindAimed());
        }

        /// <summary>
        /// Un rendu de la caméra dans une texture, post-traitement compris, sans l'interface : la
        /// photo est l'image du monde, pas celle du viseur.
        /// </summary>
        private Texture2D Capture()
        {
            if (_camera == null) return null;

            float aspect = Mathf.Max(0.5f, _camera.aspect);
            int width = Mathf.Max(64, _photoWidth);
            int height = Mathf.Max(36, Mathf.RoundToInt(width / aspect));

            RenderTexture target = RenderTexture.GetTemporary(width, height, 24, RenderTextureFormat.ARGB32);
            RenderTexture previousTarget = _camera.targetTexture;
            RenderTexture previousActive = RenderTexture.active;

            _camera.targetTexture = target;
            _camera.Render();
            _camera.targetTexture = previousTarget;

            RenderTexture.active = target;
            Texture2D photo = new Texture2D(width, height, TextureFormat.RGB24, false);
            photo.ReadPixels(new Rect(0f, 0f, width, height), 0, 0);
            photo.Apply(false, false);
            photo.wrapMode = TextureWrapMode.Clamp;
            photo.name = "Photo " + System.DateTime.Now.ToString("HH:mm:ss");

            RenderTexture.active = previousActive;
            RenderTexture.ReleaseTemporary(target);

            return photo;
        }

        private void UpdateAim()
        {
            if (Time.unscaledTime < _nextAim) return;
            _nextAim = Time.unscaledTime + 0.1f;

            Transform aimed = FindAimed();
            Combatant fighter = aimed != null ? aimed.GetComponentInParent<Combatant>() : null;

            _subjectIsFighter = fighter != null;
            _subject = fighter != null ? fighter.DisplayName : null;
            _subjectDown = fighter != null && !fighter.IsAlive;
        }

        private Transform FindAimed()
        {
            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return null;

            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            float radius = _sweepRadius / Mathf.Max(1f, Mathf.Sqrt(_zoom));

            int count = Physics.SphereCastNonAlloc(ray, radius, _hits, _maxDistance, _layers,
                QueryTriggerInteraction.Collide);

            Transform best = null;
            float bestDistance = float.MaxValue;
            Transform self = _input != null ? _input.transform : null;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hits[i].collider;
                if (collider == null) continue;
                if (self != null && collider.transform.IsChildOf(self)) continue;

                float distance = _hits[i].distance;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = collider.transform;
            }

            return best;
        }

        // --------------------------------------------------------------- viseur

        private void OnGUI()
        {
            if (FightIntro.AnyPlaying) return;
            if (_active) DrawViewfinder();

            if (_flash > 0.01f)
            {
                GuiKit.Fill(new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height),
                    new Color(1f, 1f, 1f, _flash * _flash * 0.35f));
            }
        }

        private void DrawViewfinder()
        {
            float sw = UnityEngine.Screen.width;
            float sh = UnityEngine.Screen.height;
            float unit = sh / 1080f;

            Color line = new Color(1f, 1f, 1f, 0.16f);
            Color bar = new Color(0f, 0f, 0f, 0.38f);

            // Grille des tiers.
            GuiKit.Fill(new Rect(sw / 3f, 0f, 1f, sh), line);
            GuiKit.Fill(new Rect(sw * 2f / 3f, 0f, 1f, sh), line);
            GuiKit.Fill(new Rect(0f, sh / 3f, sw, 1f), line);
            GuiKit.Fill(new Rect(0f, sh * 2f / 3f, sw, 1f), line);

            // Bandeaux haut et bas.
            float top = 70f * unit;
            float bottom = 150f * unit;
            GuiKit.Fill(new Rect(0f, 0f, sw, top), bar);
            GuiKit.Fill(new Rect(0f, sh - bottom, sw, bottom), bar);

            GUIStyle small = GuiKit.Style(Mathf.Max(11, Mathf.RoundToInt(20f * unit)), FontStyle.Bold, TextAnchor.MiddleLeft);
            GUIStyle smallRight = GuiKit.Style(Mathf.Max(11, Mathf.RoundToInt(20f * unit)), FontStyle.Bold, TextAnchor.MiddleRight);
            GUIStyle center = GuiKit.Style(Mathf.Max(11, Mathf.RoundToInt(22f * unit)), FontStyle.Bold, TextAnchor.MiddleCenter);
            GUIStyle hint = GuiKit.Style(Mathf.Max(10, Mathf.RoundToInt(17f * unit)), FontStyle.Normal, TextAnchor.MiddleCenter);
            Color outline = new Color(0f, 0f, 0f, 0.8f);

            GuiKit.OutlinedLabel(new Rect(30f * unit, 0f, 500f * unit, top), "PHOTO   ·   " + FilterNames[_filter],
                small, Color.white, outline, 1f);

            GuiKit.OutlinedLabel(new Rect(sw - 530f * unit, 0f, 500f * unit, top), "x" + _zoom.ToString("0.0"),
                smallRight, Color.white, outline, 1f);

            // Cadre de mise au point : vert sur un sujet au sol, jaune sur un combattant debout.
            float frame = 190f * unit;
            Rect focus = new Rect(sw * 0.5f - frame * 0.5f, sh * 0.5f - frame * 0.5f, frame, frame);
            Color focusColor = !_subjectIsFighter ? new Color(1f, 1f, 1f, 0.8f)
                : _subjectDown ? new Color(0.4f, 1f, 0.5f) : new Color(1f, 0.82f, 0.3f);

            Brackets(focus, 34f * unit, Mathf.Max(2f, 3f * unit), focusColor);

            if (_subjectIsFighter)
            {
                string state = _subjectDown ? "AU SOL" : "DEBOUT";
                GuiKit.OutlinedLabel(new Rect(focus.x - 200f * unit, focus.yMax + 8f * unit, focus.width + 400f * unit, 30f * unit),
                    _subject + "  —  " + state, center, focusColor, outline, 1f);
            }

            // Zoom : un rail vertical a droite.
            Rect rail = new Rect(sw - 46f * unit, sh * 0.3f, 4f * unit, sh * 0.4f);
            GuiKit.Fill(rail, new Color(1f, 1f, 1f, 0.25f));
            float knobY = Mathf.Lerp(rail.yMax, rail.y, Mathf.InverseLerp(1f, _maxZoom, _zoom));
            GuiKit.Disc(new Rect(rail.center.x - 11f * unit, knobY - 11f * unit, 22f * unit, 22f * unit), Color.white);

            // Declencheur.
            float shutter = 92f * unit;
            Rect button = new Rect(sw * 0.5f - shutter * 0.5f, sh - bottom * 0.5f - shutter * 0.5f, shutter, shutter);
            GuiKit.Disc(button, Color.white);
            GuiKit.Disc(button, Color.white);
            GuiKit.Disc(new Rect(button.x + 9f * unit, button.y + 9f * unit, button.width - 18f * unit, button.height - 18f * unit),
                new Color(0.1f, 0.1f, 0.12f, 0.9f));
            GuiKit.Disc(new Rect(button.x + 14f * unit, button.y + 14f * unit, button.width - 28f * unit, button.height - 28f * unit),
                Color.white);

            // Derniere photo, en vignette ; juste apres la prise, elle y file depuis le plein ecran.
            Rect thumb = new Rect(40f * unit, sh - bottom * 0.5f - 45f * unit, 80f * unit, 80f * unit);
            Texture2D latest = PhoneGallery.Latest;

            if (latest != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_captureAge / 0.4f));
                Rect from = new Rect(0f, 0f, sw, sh);
                Rect at = new Rect(Mathf.Lerp(from.x, thumb.x, t), Mathf.Lerp(from.y, thumb.y, t),
                    Mathf.Lerp(from.width, thumb.width, t), Mathf.Lerp(from.height, thumb.height, t));

                GuiKit.Fill(new Rect(at.x - 3f, at.y - 3f, at.width + 6f, at.height + 6f), Color.white);
                GUI.DrawTexture(at, latest, t < 1f ? ScaleMode.StretchToFill : ScaleMode.ScaleAndCrop);
            }

            GuiKit.OutlinedLabel(new Rect(sw - 330f * unit, sh - bottom, 300f * unit, bottom), "CLIC DROIT : retour",
                smallRight, new Color(1f, 1f, 1f, 0.7f), outline, 1f);

            GuiKit.OutlinedLabel(new Rect(0f, sh - bottom - 36f * unit, sw, 30f * unit),
                "CLIC GAUCHE photo   ·   MOLETTE zoom   ·   GAUCHE / DROITE filtre",
                hint, new Color(1f, 1f, 1f, 0.75f), outline, 1f);

            // La consigne de l'histoire, quand une preuve est attendue.
            if (_device.Current != PhoneDevice.Screen.Photo) return;

            string counter = _display != null ? _display.PhotoCounter : null;
            string order = string.IsNullOrEmpty(counter) ? "PREUVE : CADRE LA CIBLE AU SOL" : "PREUVE : CADRE LA CIBLE AU SOL   " + counter;

            GuiKit.OutlinedLabel(new Rect(0f, top + 14f * unit, sw, 34f * unit), order, center,
                new Color(1f, 0.85f, 0.4f), outline, 1f);
        }

        private static void Brackets(Rect r, float length, float thickness, Color color)
        {
            GuiKit.Fill(new Rect(r.x, r.y, length, thickness), color);
            GuiKit.Fill(new Rect(r.x, r.y, thickness, length), color);
            GuiKit.Fill(new Rect(r.xMax - length, r.y, length, thickness), color);
            GuiKit.Fill(new Rect(r.xMax - thickness, r.y, thickness, length), color);
            GuiKit.Fill(new Rect(r.x, r.yMax - thickness, length, thickness), color);
            GuiKit.Fill(new Rect(r.x, r.yMax - length, thickness, length), color);
            GuiKit.Fill(new Rect(r.xMax - length, r.yMax - thickness, length, thickness), color);
            GuiKit.Fill(new Rect(r.xMax - thickness, r.yMax - length, thickness, length), color);
        }

        /// <summary>Déclencheur : un clic sec, un souffle de rideau, un second clic.</summary>
        private static AudioClip ShutterSound()
        {
            const int rate = 22050;
            int length = Mathf.RoundToInt(0.16f * rate);
            float[] data = new float[length];
            System.Random random = new System.Random(71);

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)rate;
                float noise = (float)random.NextDouble() * 2f - 1f;

                float first = Mathf.Exp(-t / 0.004f);
                float second = t > 0.085f ? Mathf.Exp(-(t - 0.085f) / 0.005f) : 0f;
                float curtain = Mathf.Exp(-t / 0.05f) * 0.25f;

                data[n] = noise * (first + second * 0.8f + curtain) * 0.6f;
            }

            AudioClip clip = AudioClip.Create("Declencheur", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
