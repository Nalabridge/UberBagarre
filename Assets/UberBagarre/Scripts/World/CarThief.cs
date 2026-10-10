using System.Collections;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Voler une voiture, à la manière de Thief Simulator.
    ///
    /// **Une voiture garée** est presque toujours fermée à clé. On la crochète : la serrure a
    /// plusieurs goupilles (deux à quatre, selon la voiture). Pour chacune, on place le crochet
    /// (souris ou ← →) et on met de la tension (espace ou clic) : si le crochet est au bon
    /// endroit, le barillet tourne jusqu'au bout et la goupille se cale (clic) ; sinon il se
    /// bloque à mi-course, et le crochet force — il s'use, et finit par casser. Plus on est
    /// près, plus le barillet tourne loin avant de bloquer, et le crochet vibre : c'est ce qu'on
    /// apprend à sentir. Il faut des crochets (quincaillerie, marché noir).
    ///
    /// Une fois ouverte, pas de clé : on fait les fils sous le volant. Et parfois l'alarme se
    /// déclenche. Tout ça se signale à la police (<see cref="Crimes"/>), qui décide qui a vu.
    ///
    /// **Une voiture de la circulation**, arrêtée : on sort le conducteur. C'est plus rapide,
    /// et beaucoup plus grave.
    /// </summary>
    public class CarThief : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerProgress _progress;
        [SerializeField] private SubtitleDisplay _subtitles;

        [SerializeField, Range(2f, 25f)]
        [Tooltip("Tolérance d'une goupille, en degrés de part et d'autre du bon angle.")]
        private float _tolerance = 9f;

        [SerializeField, Range(0f, 1f)] private float _alarmChance = 0.35f;
        [SerializeField, Min(0.1f)] private float _mouseSpeed = 0.35f;

        private DrivableCar _car;
        private float[] _sweet;
        private int _pin;
        private float _pick;
        private float _turn;
        private float _wear;
        private float _shake;
        private float _nextTick;
        private float _openedAt;
        private bool _busy;
        private float _cooldownUntil;
        private string _flash;
        private float _flashUntil;
        private AudioSource _audio;
        private AudioClip _tick, _set, _snap, _open;

        public static bool Picking { get; private set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Picking = false;
        }

        private void Awake()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _tick = Click("Crochet (grattement)", 3800f, 0.012f, 0.12f);
            _set = Click("Crochet (goupille)", 2200f, 0.03f, 0.35f);
            _snap = Click("Crochet (casse)", 900f, 0.08f, 0.6f);
            _open = Click("Serrure (ouverte)", 1400f, 0.12f, 0.5f);
        }

        private void OnEnable()
        {
            DrivableCar.LockpickRequested += OnLockpick;
            DrivableCar.CarjackRequested += OnCarjack;
        }

        private void OnDisable()
        {
            DrivableCar.LockpickRequested -= OnLockpick;
            DrivableCar.CarjackRequested -= OnCarjack;
            if (Picking) Stop(false);
        }

        private void OnDestroy()
        {
            if (_tick != null) Destroy(_tick);
            if (_set != null) Destroy(_set);
            if (_snap != null) Destroy(_snap);
            if (_open != null) Destroy(_open);
        }

        private void Start()
        {
            LoadingScreen.AddTip("Voiture garée, portière fermée : crochète-la (souris pour placer le crochet, espace pour tourner). Il faut des crochets.");
            LoadingScreen.AddTip("Voler une voiture, ça se voit. Regarde qui traîne autour avant de crocheter.");
        }

        // ------------------------------------------------------------------ crochetage

        private void OnLockpick(DrivableCar car)
        {
            if (Picking || _busy || car == null || Time.unscaledTime < _cooldownUntil) return;

            if (_progress != null && _progress.Lockpicks <= 0)
            {
                Tell("Fermée à clé. Il te faut des crochets (quincaillerie, marché noir).");
                return;
            }

            _car = car;
            int pins = 2 + Mathf.Abs(car.GetInstanceID() / 7) % 3;
            _sweet = new float[pins];
            for (int i = 0; i < pins; i++) _sweet[i] = Random.Range(-75f, 75f);
            _pin = 0;
            _pick = 0f;
            _turn = 0f;
            _wear = 0f;
            _openedAt = Time.unscaledTime;
            Picking = true;
            if (_input != null) _input.SetGameplayLock(this, true);
        }

        private void Stop(bool success)
        {
            Picking = false;
            _cooldownUntil = Time.unscaledTime + 0.5f;
            if (_input != null) _input.SetGameplayLock(this, false);

            DrivableCar car = _car;
            _car = null;
            if (!success || car == null) return;

            car.Unlock(true);
            _audio.PlayOneShot(_open, 0.8f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
            Crimes.Report(Crime.VolDeVoiture, car.transform.position, car.gameObject);

            if (Random.value < _alarmChance) StartCoroutine(Alarm(car));
            else Tell("Clac. La portière cède.");
        }

        private void Update()
        {
            if (!Picking || _car == null || _input == null || _input.Provider == null) return;

            var provider = _input.Provider;
            var bindings = _input.Bindings;
            float dt = Time.deltaTime;

            // Abandonner : E, Échap, ou s'éloigner (on s'est fait bousculer).
            bool quit = Time.unscaledTime - _openedAt > 0.25f &&
                        (provider.GetPressedThisFrame(bindings.interact) || provider.GetPressedThisFrame(bindings.releaseCursor));
            Transform viewer = SwingDoor.Viewer != null ? SwingDoor.Viewer : Camera.main != null ? Camera.main.transform : null;
            if (viewer != null && Vector3.Distance(viewer.position, _car.transform.position) > 5f) quit = true;
            if (quit)
            {
                Stop(false);
                return;
            }

            // Placer le crochet : la souris, ou les flèches.
            Vector2 look = provider.GetLookDelta();
            float keys = (provider.GetHeld(bindings.moveRight) ? 1f : 0f) - (provider.GetHeld(bindings.moveLeft) ? 1f : 0f);
            _pick = Mathf.Clamp(_pick + look.x * _mouseSpeed + keys * 90f * dt, -85f, 85f);

            bool tension = provider.GetHeld(bindings.jump) || provider.GetHeld(bindings.attackStraight);
            float error = Mathf.Abs(_pick - _sweet[_pin]);
            float allowed = 1f - Mathf.Clamp01((error - _tolerance) / 40f);

            if (tension)
            {
                _turn = Mathf.MoveTowards(_turn, allowed, dt * 1.7f);
                bool jammed = allowed < 0.999f && _turn >= allowed - 0.02f;
                _shake = jammed ? Mathf.Lerp(0.3f, 1f, 1f - allowed) : 0f;

                if (jammed)
                {
                    _wear += dt * Mathf.Lerp(0.35f, 0.9f, 1f - allowed);
                    if (_wear >= 1f) BreakPick();
                }
                else if (allowed >= 0.999f && _turn >= 0.98f)
                {
                    _audio.PlayOneShot(_set, 0.7f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
                    _pin++;
                    _turn = 0f;
                    if (_pin >= _sweet.Length)
                    {
                        Stop(true);
                        return;
                    }
                }
            }
            else
            {
                _turn = Mathf.MoveTowards(_turn, 0f, dt * 2.5f);
                _shake = 0f;
            }

            // Le grattement du crochet sur les goupilles : plus serré quand on approche.
            if (Mathf.Abs(look.x) + Mathf.Abs(keys) > 0.01f && Time.unscaledTime >= _nextTick)
            {
                float near = 1f - Mathf.Clamp01(error / 60f);
                _nextTick = Time.unscaledTime + Mathf.Lerp(0.16f, 0.05f, near);
                _audio.pitch = Mathf.Lerp(0.8f, 1.35f, near);
                _audio.PlayOneShot(_tick, Mathf.Lerp(0.25f, 0.7f, near) * Core.GameSettings.Volume(Core.AudioChannel.Effects));
            }
        }

        private void BreakPick()
        {
            _wear = 0f;
            _turn = 0f;
            _audio.pitch = 1f;
            _audio.PlayOneShot(_snap, 0.9f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
            if (_progress != null) _progress.UseLockpick();
            Flash("Le crochet casse.");

            if (_progress == null || _progress.Lockpicks > 0) return;
            Tell("Plus de crochets.");
            Stop(false);
        }

        /// <summary>L'alarme : le klaxon hurle par saccades, les phares clignotent, la rue regarde.</summary>
        private IEnumerator Alarm(DrivableCar car)
        {
            Tell("L'alarme se déclenche !");
            Crimes.Report(Crime.VolDeVoiture, car.transform.position, car.gameObject);

            float until = Time.time + 9f;
            bool on = false;
            while (Time.time < until && car != null && !car.Occupied)
            {
                on = !on;
                car.Honk(on);
                yield return new WaitForSeconds(0.35f);
            }

            if (car != null) car.Honk(false);
        }

        // ------------------------------------------------------------------ conducteur

        private void OnCarjack(DrivableCar car)
        {
            if (Picking || _busy || car == null || Time.unscaledTime < _cooldownUntil) return;

            if (Mathf.Abs(car.ForwardSpeed) > 1.5f)
            {
                Tell("Elle roule. Attends qu'elle s'arrête.");
                return;
            }

            StartCoroutine(Carjack(car));
        }

        private IEnumerator Carjack(DrivableCar car)
        {
            _busy = true;
            TrafficDriver driver = car.GetComponent<TrafficDriver>();
            if (driver != null) driver.Evict();
            car.TakenByForce();
            Crimes.Report(Crime.Carjacking, car.transform.position, car.gameObject);
            Tell("Tu ouvres la portière et tu le sors du siège. Il détale sans demander son reste.");
            yield return new WaitForSeconds(0.4f);
            _busy = false;
            car.RequestEnter();
        }

        // ------------------------------------------------------------------ dessin

        private void OnGUI()
        {
            if (!string.IsNullOrEmpty(_flash) && Time.unscaledTime < _flashUntil && !Picking)
            {
                DrawFlash(UiTheme.Unit);
            }

            if (!Picking || _car == null || GameMenu.IsOpen) return;
            GUI.depth = -30;

            float u = UiTheme.Unit;
            float sw = Screen.width, sh = Screen.height;
            Rect panel = new Rect((sw - 560f * u) * 0.5f, (sh - 520f * u) * 0.5f, 560f * u, 520f * u);

            UiTheme.DrawPanel(panel, 14f * u);

            GUIStyle title = UiTheme.Title(26f);
            GUIStyle small = UiTheme.Text(15f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            UiTheme.Label(new Rect(panel.x + 30f * u, panel.y + 18f * u, panel.width, 36f * u), "CROCHETAGE", title, UiTheme.Ink);
            UiTheme.Label(new Rect(panel.x + 30f * u, panel.y + 52f * u, panel.width, 22f * u), _car.DisplayName, small, UiTheme.InkDim);

            // Les goupilles.
            for (int i = 0; i < _sweet.Length; i++)
            {
                Rect pin = new Rect(panel.xMax - 30f * u - (_sweet.Length - i) * 34f * u, panel.y + 30f * u, 26f * u, 26f * u);
                Color c = i < _pin ? UiTheme.Good : i == _pin ? UiTheme.Accent : new Color(1f, 1f, 1f, 0.15f);
                GuiKit.Rounded(pin, c, 13f * u);
            }

            // La serrure : un disque, le barillet qui tourne, le crochet par le haut.
            Vector2 centre = new Vector2(panel.center.x, panel.y + 250f * u);
            float shake = _shake * Mathf.Sin(Time.unscaledTime * 70f) * 3f * u;
            float lockRadius = 130f * u;
            Rect face = new Rect(centre.x - lockRadius + shake, centre.y - lockRadius, lockRadius * 2f, lockRadius * 2f);
            GuiKit.Glow(face, new Color(0f, 0f, 0f, 0.5f), lockRadius, 10f * u);
            GuiKit.Rounded(face, new Color(0.62f, 0.58f, 0.48f), lockRadius);
            Rect cylinder = new Rect(centre.x - 88f * u + shake, centre.y - 88f * u, 176f * u, 176f * u);
            GuiKit.Rounded(cylinder, new Color(0.78f, 0.72f, 0.56f), 88f * u);
            GuiKit.RoundedOutline(cylinder, new Color(0f, 0f, 0f, 0.25f), 88f * u, 2f * u);

            Matrix4x4 matrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(_turn * 90f, new Vector2(centre.x + shake, centre.y));
            GuiKit.Rounded(new Rect(centre.x - 9f * u + shake, centre.y - 40f * u, 18f * u, 56f * u), new Color(0.08f, 0.07f, 0.06f), 9f * u);
            GuiKit.Rounded(new Rect(centre.x - 17f * u + shake, centre.y - 58f * u, 34f * u, 34f * u), new Color(0.08f, 0.07f, 0.06f), 17f * u);
            GUI.matrix = matrix;

            // Le crochet : une tige fine qui entre par la serrure, orientée par la souris.
            GUIUtility.RotateAroundPivot(_pick, new Vector2(centre.x + shake, centre.y - 30f * u));
            GuiKit.Rounded(new Rect(centre.x - 2.5f * u + shake, centre.y - 230f * u, 5f * u, 200f * u), new Color(0.82f, 0.84f, 0.88f), 2.5f * u);
            GuiKit.Rounded(new Rect(centre.x - 7f * u + shake, centre.y - 240f * u, 14f * u, 60f * u), new Color(0.25f, 0.25f, 0.3f), 5f * u);
            GUI.matrix = matrix;

            // La clé de tension, en bas.
            GuiKit.Rounded(new Rect(centre.x - 4f * u + shake, centre.y + 20f * u, 8f * u, 110f * u), new Color(0.5f, 0.52f, 0.58f), 3f * u);
            GuiKit.Rounded(new Rect(centre.x - 4f * u + shake, centre.y + 124f * u, 70f * u, 8f * u), new Color(0.5f, 0.52f, 0.58f), 3f * u);

            // L'usure du crochet, et combien il en reste.
            Rect wear = new Rect(panel.x + 30f * u, panel.yMax - 96f * u, panel.width - 60f * u, 8f * u);
            GuiKit.Rounded(wear, new Color(1f, 1f, 1f, 0.1f), 4f * u);
            GuiKit.Rounded(new Rect(wear.x, wear.y, Mathf.Max(wear.height, wear.width * _wear), wear.height),
                Color.Lerp(new Color(1f, 0.82f, 0.35f), new Color(1f, 0.3f, 0.25f), _wear), 4f * u);
            UiTheme.Label(new Rect(wear.x, wear.y - 24f * u, wear.width, 20f * u),
                "Crochet : " + (_wear > 0.66f ? "il va casser" : _wear > 0.3f ? "il force" : "intact") +
                "      Crochets : " + (_progress != null ? _progress.Lockpicks : 0), small, UiTheme.InkDim);

            Core.InputBindings b = _input != null ? _input.Bindings : null;
            float hy = panel.yMax - 50f * u;
            float hx = panel.x + 30f * u;
            hx = UiTheme.KeyHint(hx, hy, "Souris", "Placer le crochet", u);
            hx = UiTheme.KeyHint(hx, hy, b != null ? UiTheme.KeyName(b.jump) : "Espace", "Tourner", u);
            UiTheme.KeyHint(hx, hy, b != null ? UiTheme.KeyName(b.interact) : "E", "Abandonner", u);
        }

        private void Flash(string text)
        {
            _flash = text;
            _flashUntil = Time.unscaledTime + 1.6f;
        }

        private void DrawFlash(float u)
        {
            UiTheme.DrawToast(_flash, UiTheme.Bad, Screen.height * 0.3f, 1f);
        }

        private void Tell(string text)
        {
            if (_subtitles != null) _subtitles.Play(DialogueLine.Say("", text));
        }

        /// <summary>Un clic métallique bref (le son du crochet, d'une goupille, d'une casse).</summary>
        private static AudioClip Click(string name, float frequency, float seconds, float level)
        {
            AudioClip real = Core.SoundBank.Real("Vol/" + Core.SoundBank.Slug(name));
            if (real != null) return real;

            const int rate = 22050;
            int length = Mathf.Max(32, (int)(rate * seconds));
            float[] data = new float[length];
            System.Random random = new System.Random(name.Length * 31);
            for (int n = 0; n < length; n++)
            {
                float t = n / (float)rate;
                float env = Mathf.Exp(-t / seconds * 5f);
                float tone = Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.6f;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0) * 0.5f;
                data[n] = (tone + noise) * env * level;
            }

            AudioClip clip = AudioClip.Create(name, length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Configure(PlayerInputReader input, PlayerProgress progress, SubtitleDisplay subtitles)
        {
            _input = input;
            _progress = progress;
            _subtitles = subtitles;
        }
    }
}
