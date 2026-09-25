using System;
using System.Collections.Generic;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.View;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.UI
{
    /// <summary>
    /// Le menu du jeu : l'écran titre au lancement, la pause sur Échap, et les réglages.
    ///
    /// **L'écran titre** montre le jeu lui-même : la caméra de cinéma glisse lentement devant
    /// la maison, de l'autre côté de la rue, sous les lampadaires. Un jeu qui s'ouvre sur son
    /// propre décor dit tout de suite ce qu'il est ; un fond noir avec trois boutons ne dit rien.
    ///
    /// **La pause** fige vraiment tout : le temps du jeu, le son, et les systèmes qui comptent
    /// en temps réel (dialogues, cinématiques, téléphone) consultent <see cref="IsPaused"/>.
    ///
    /// **Les réglages** reprennent ceux du directeur graphique (préréglage, anticrénelage,
    /// reflets, lumière volumétrique, luminosité…) et y ajoutent ceux du joueur : qualité,
    /// plein écran, résolution, champ de vision, sensibilité, volume, compteur d'images.
    ///
    /// Tout se pilote au clavier (flèches, Entrée, Échap ou Retour arrière) ou à la souris
    /// (survol, clic, molette, clic sur une barre pour régler une valeur).
    /// </summary>
    [DefaultExecutionOrder(-60)]
    [DisallowMultipleComponent]
    public class GameMenu : MonoBehaviour
    {
        private enum Page
        {
            None,
            Title,
            Pause,
            Chapters,
            Graphics,
            Controls
        }

        private class Item
        {
            public string Label;
            public string Hint;
            public Action Activate;
            public Func<string> Value;
            public Action<int> Step;
            public Func<float> Get01;
            public Action<float> Set01;
            public bool Separator;
        }

        private const string Prefs = "UberBagarre.Menu.";

        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private CursorLockController _cursor;
        [SerializeField] private GraphicsDirector _graphics;
        [SerializeField] private PlayerLook _look;
        [SerializeField] private Camera _gameCamera;

        [SerializeField]
        [Tooltip("La camera de cinema (celle de l'observation) : elle filme l'ecran titre.")]
        private Camera _menuCamera;

        [SerializeField] private ObserverCamera _observer;
        [SerializeField] private ScreenFader _fader;

        [SerializeField]
        [Tooltip("L'histoire. Absente (bac a sable) : pas d'ecran titre ni de chapitres.")]
        private PrologueDirector _prologue;

        [SerializeField]
        [Tooltip("Le menu de developpement (Tab), referme quand la pause s'ouvre.")]
        private Sandbox.SandboxMenu _devMenu;

        [Header("Ecran titre")]
        [SerializeField] private bool _titleOnStart;
        [SerializeField] private Transform _shotFrom;
        [SerializeField] private Transform _shotTo;
        [SerializeField] private Transform _shotTarget;
        [SerializeField, Min(5f)] private float _shotDuration = 44f;
        [SerializeField, Range(15f, 80f)] private float _shotFov = 40f;

        [Header("Scenes")]
        [SerializeField] private string _storyScene = "Prologue";
        [SerializeField] private string _sandboxScene = "CombatSandbox";

        [Header("Son")]
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _uiVolume = 0.45f;

        private static readonly Color Accent = new Color(1f, 0.2f, 0.55f);
        private static readonly Color Ink = new Color(0.93f, 0.93f, 0.95f);
        private static readonly Color Dim = new Color(0.62f, 0.63f, 0.68f);

        private readonly List<Item> _items = new List<Item>(24);
        private Page _page = Page.None;
        private Page _home = Page.None;
        private int _selected;
        private float _scroll;
        private float _open;
        private float _shotTime;
        private float _previousShotFov = 60f;
        private float _pageTime;
        private bool _starting;
        private float _startTimer;
        private string _startBeat;
        private bool _showFps;
        private float _fov = 70f;
        private float _baseFov = 70f;
        private int _dragging = -1;

        private Texture2D _gradient;
        private AudioSource _music;
        private AudioSource _ui;
        private AudioClip _musicClip;
        private AudioClip _tick;
        private AudioClip _confirm;

        private readonly List<Vector2Int> _resolutions = new List<Vector2Int>(16);

        /// <summary>Un écran du menu est affiché (titre ou pause).</summary>
        public static bool IsOpen { get; private set; }

        /// <summary>Le jeu est en pause : le temps est arrêté, le son coupé.</summary>
        public static bool IsPaused { get; private set; }

        /// <summary>L'écran titre est affiché : l'interface de jeu (viseur, téléphone) se cache.</summary>
        public static bool ShowingTitle
        {
            get { return IsOpen && !IsPaused; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            IsOpen = false;
            IsPaused = false;
        }

        // --------------------------------------------------------------- cycle de vie

        private void Awake()
        {
            if (_gameCamera != null) _baseFov = _gameCamera.fieldOfView;

            _fov = PlayerPrefs.GetFloat(Prefs + "fov", _baseFov);
            _showFps = PlayerPrefs.GetInt(Prefs + "ips", 0) == 1;
            AudioListener.volume = PlayerPrefs.GetFloat(Prefs + "volume", 1f);

            _music = gameObject.AddComponent<AudioSource>();
            _music.playOnAwake = false;
            _music.loop = true;
            _music.spatialBlend = 0f;
            _music.ignoreListenerPause = true;
            _music.volume = 0f;

            _ui = gameObject.AddComponent<AudioSource>();
            _ui.playOnAwake = false;
            _ui.spatialBlend = 0f;
            _ui.ignoreListenerPause = true;

            _tick = Blip("Menu (deplacement)", 1320f, 0.035f, 0.25f);
            _confirm = Blip("Menu (validation)", 660f, 0.12f, 0.5f);
        }

        private void Start()
        {
            ApplyFov();

            if (_titleOnStart && _prologue != null) Open(Page.Title);
        }

        private void OnDisable()
        {
            if (_page != Page.None) CloseAll();
        }

        private void OnDestroy()
        {
            if (_gradient != null) Destroy(_gradient);
            if (_musicClip != null) Destroy(_musicClip);
            if (_tick != null) Destroy(_tick);
            if (_confirm != null) Destroy(_confirm);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _open = Mathf.MoveTowards(_open, _page != Page.None && !_starting ? 1f : 0f, dt / 0.2f);
            _pageTime += dt;

            float music = _page == Page.Title || (_home == Page.Title && _page != Page.None) ? _musicVolume : 0f;
            if (_starting) music = 0f;
            if (_music.clip != null)
            {
                _music.volume = Mathf.MoveTowards(_music.volume, music, dt * 0.35f);
                if (_music.volume <= 0f && _music.isPlaying && music <= 0f) _music.Stop();
            }

            if (_starting)
            {
                UpdateStart(dt);
                return;
            }

            if (_page == Page.None)
            {
                if (_input != null && _input.ReleaseCursorPressed && CanPause()) Open(Page.Pause);
                return;
            }

            if (_home == Page.Title) UpdateShot(dt);

            HandleKeys();
        }

        private bool CanPause()
        {
            // Le menu de developpement ouvert : Echap le referme, c'est tout.
            if (_devMenu != null && _devMenu.IsOpen)
            {
                _devMenu.SetOpen(false);
                return false;
            }

            return true;
        }

        // --------------------------------------------------------------- ouverture

        private void Open(Page page)
        {
            _home = page;

            IsOpen = true;
            ModalScreen.Set(this, true);
            if (_input != null) _input.SetGameplayLock(this, true);

            // Le controleur de curseur est suspendu : sinon le premier clic sur un bouton
            // recapturerait la souris.
            if (_cursor != null)
            {
                _cursor.enabled = false;
                _cursor.SetLocked(false);
            }

            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;

            if (page == Page.Pause)
            {
                IsPaused = true;
                Time.timeScale = 0f;
                AudioListener.pause = true;
            }
            else
            {
                BeginShot();
            }

            Show(page);
        }

        private void CloseAll()
        {
            if (_home == Page.Pause)
            {
                IsPaused = false;
                Time.timeScale = Feedback.HitStop.BaseTimeScale;
                AudioListener.pause = false;
            }

            if (_home == Page.Title) EndShot();

            _page = Page.None;
            _home = Page.None;
            _dragging = -1;

            IsOpen = false;
            ModalScreen.Set(this, false);
            if (_input != null) _input.SetGameplayLock(this, false);

            if (_cursor != null)
            {
                _cursor.enabled = true;
                _cursor.SetLocked(true);
            }
        }

        private void Show(Page page)
        {
            _page = page;
            _pageTime = 0f;
            _scroll = 0f;
            _dragging = -1;
            BuildItems(page);

            _selected = 0;
            while (_selected < _items.Count && _items[_selected].Separator) _selected++;
        }

        private void Back()
        {
            Play(_tick, 0.8f);

            if (_page == Page.Pause)
            {
                CloseAll();
                return;
            }

            if (_page == Page.Title) return;

            Show(_home);
        }

        // --------------------------------------------------------------- lancement d'une partie

        private void StartStory(string beat)
        {
            if (_prologue == null)
            {
                LoadStoryScene();
                return;
            }

            Play(_confirm, 1f);

            // Depuis la pause, on relance tout de suite (la pause fige le fondu).
            if (_home == Page.Pause)
            {
                CloseAll();
                Launch(beat);
                return;
            }

            _starting = true;
            _startTimer = 0f;
            _startBeat = beat;
            if (_fader != null) _fader.FadeOut(0.7f);
        }

        private void UpdateStart(float dt)
        {
            _startTimer += dt;
            if (_home == Page.Title) UpdateShot(dt);
            if (_startTimer < 0.8f) return;

            _starting = false;
            CloseAll();
            Launch(_startBeat);
        }

        private void Launch(string beat)
        {
            if (_prologue == null) return;

            if (string.IsNullOrEmpty(beat)) _prologue.Begin();
            else _prologue.StartAt(beat);
        }

        private void LoadStoryScene()
        {
            if (!Application.CanStreamedLevelBeLoaded(_storyScene)) return;

            Play(_confirm, 1f);
            if (_page != Page.None) CloseAll();
            SceneManager.LoadScene(_storyScene);
        }

        private void LoadSandbox()
        {
            if (!Application.CanStreamedLevelBeLoaded(_sandboxScene)) return;

            Play(_confirm, 1f);
            CloseAll();
            SceneManager.LoadScene(_sandboxScene);
        }

        private void BackToTitle()
        {
            Play(_confirm, 1f);

            // L'ecran titre vit dans la scene de l'histoire : on la recharge, proprement.
            string scene = _prologue != null ? SceneManager.GetActiveScene().name : _storyScene;
            if (!Application.CanStreamedLevelBeLoaded(scene)) return;

            CloseAll();
            SceneManager.LoadScene(scene);
        }

        private static void Quit()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }

        // --------------------------------------------------------------- plan de l'ecran titre

        private void BeginShot()
        {
            _shotTime = 0f;

            if (_menuCamera == null || _shotFrom == null || _shotTo == null) return;

            if (_observer != null)
            {
                _observer.SetActive(false);
                _observer.enabled = false;
            }

            _previousShotFov = _menuCamera.fieldOfView;
            _menuCamera.fieldOfView = _shotFov;
            _menuCamera.enabled = true;
            if (_gameCamera != null) _gameCamera.enabled = false;

            if (_musicClip == null) _musicClip = Music();
            _music.clip = _musicClip;
            _music.volume = 0f;
            _music.Play();

            UpdateShot(0f);
        }

        private void EndShot()
        {
            if (_menuCamera != null)
            {
                _menuCamera.enabled = false;
                _menuCamera.fieldOfView = _previousShotFov;
            }

            if (_gameCamera != null) _gameCamera.enabled = true;
            if (_observer != null) _observer.enabled = true;
        }

        private void UpdateShot(float dt)
        {
            if (_menuCamera == null || _shotFrom == null || _shotTo == null) return;

            _shotTime += dt;

            // Un aller-retour tres lent, adouci aux extremites : un travelling, pas un manege.
            float phase = Mathf.PingPong(_shotTime / _shotDuration, 1f);
            float t = Mathf.SmoothStep(0f, 1f, phase);

            Vector3 position = Vector3.Lerp(_shotFrom.position, _shotTo.position, t);
            Vector3 target = _shotTarget != null ? _shotTarget.position : position + _shotFrom.forward;

            // Un souffle de camera a l'epaule, a peine perceptible.
            position += new Vector3(Mathf.Sin(_shotTime * 0.7f), Mathf.Sin(_shotTime * 0.53f + 1f), 0f) * 0.02f;

            Transform camera = _menuCamera.transform;
            camera.position = position;
            camera.rotation = Quaternion.LookRotation((target - position).normalized, Vector3.up);
        }

        // --------------------------------------------------------------- contenu des pages

        private void BuildItems(Page page)
        {
            _items.Clear();
            bool story = _prologue != null;

            switch (page)
            {
                case Page.Title:
                    Add("NOUVELLE PARTIE", "Le prologue : la planque, le courrier, l'appel de Sami.", delegate { StartStory(null); });
                    Add("CHAPITRES", "Reprendre à un chapitre précis.", delegate { Go(Page.Chapters); });
                    if (Application.CanStreamedLevelBeLoaded(_sandboxScene))
                    {
                        Add("BAC À SABLE", "L'arène d'entraînement : adversaires, réglages, triche.", LoadSandbox);
                    }
                    Add("GRAPHISMES", "Image, qualité, affichage, souris et son.", delegate { Go(Page.Graphics); });
                    Add("COMMANDES", "Toutes les touches.", delegate { Go(Page.Controls); });
                    Add("QUITTER", null, Quit);
                    break;

                case Page.Pause:
                    Add("REPRENDRE", null, delegate { Play(_confirm, 0.7f); CloseAll(); });
                    if (story) Add("CHAPITRES", "Rejouer un chapitre depuis son début.", delegate { Go(Page.Chapters); });
                    Add("GRAPHISMES", "Image, qualité, affichage, souris et son.", delegate { Go(Page.Graphics); });
                    Add("COMMANDES", "Toutes les touches.", delegate { Go(Page.Controls); });
                    if (story || Application.CanStreamedLevelBeLoaded(_storyScene))
                    {
                        Add("MENU PRINCIPAL", story ? "Revenir à l'écran titre (la partie en cours est perdue)." : "Quitter le bac à sable.",
                            BackToTitle);
                    }
                    Add("QUITTER LE JEU", null, Quit);
                    break;

                case Page.Chapters:
                    Add("PROLOGUE  —  LA PLANQUE", "Une étoile. Bruno Moretti, devant le Vertigo.", delegate { StartStory(null); });
                    Add("CHAPITRE 1  —  DEUX ÉTOILES", "Les frères Kovac, au parking.", delegate { StartStory("chapitre-1"); });
                    Add("CHAPITRE 2  —  TROIS ÉTOILES", "Le Taureau, dans la fosse du club.", delegate { StartStory("chapitre-2"); });
                    Separator();
                    Add("RETOUR", null, Back);
                    break;

                case Page.Graphics:
                    BuildGraphics();
                    break;

                case Page.Controls:
                    Add("RETOUR", null, Back);
                    break;
            }
        }

        private void BuildGraphics()
        {
            GraphicsDirector g = _graphics;

            if (g != null)
            {
                Add("PRÉRÉGLAGE", "Sobre, Cinéma ou Bâtard : bloom, contraste, vignette, lumière dans l'air.",
                    delegate { return PresetName(_preset); },
                    delegate(int d) { _preset = (GraphicsDirector.Preset)Wrap((int)_preset + d, 3); g.ApplyPreset(_preset); });
            }

            Add("QUALITÉ", "Ombres, textures et détails selon les niveaux du projet.",
                delegate { string[] n = QualitySettings.names; int q = QualitySettings.GetQualityLevel(); return q < n.Length ? n[q].ToUpperInvariant() : q.ToString(); },
                delegate(int d)
                {
                    int count = QualitySettings.names.Length;
                    if (count == 0) return;
                    QualitySettings.SetQualityLevel(Wrap(QualitySettings.GetQualityLevel() + d, count), true);
                    if (g != null) g.Push();
                });

            Add("PLEIN ÉCRAN", null,
                delegate { return Screen.fullScreen ? "OUI" : "NON"; },
                delegate(int d) { Screen.fullScreen = !Screen.fullScreen; });

            Add("RÉSOLUTION", "Sans effet dans l'éditeur : la fenêtre de jeu garde sa taille.",
                delegate { return Screen.width + " × " + Screen.height; },
                delegate(int d) { StepResolution(d); });

            if (g != null)
            {
                Add("SYNCHRO VERTICALE", "Évite que l'image se déchire quand la vue tourne vite.",
                    delegate { return g.VSync ? "OUI" : "NON"; },
                    delegate(int d) { g.VSync = !g.VSync; g.Save(); });

                Add("ANTICRÉNELAGE", "FXAA conseillé. MSAA est plus fin mais coûte cher avec beaucoup de lampes.",
                    delegate { return AaName(g.AntiAliasing); },
                    delegate(int d)
                    {
                        Array values = Enum.GetValues(typeof(UberPostProcess.AntiAliasingMode));
                        int index = Array.IndexOf(values, g.AntiAliasing);
                        g.AntiAliasing = (UberPostProcess.AntiAliasingMode)values.GetValue(Wrap(index + d, values.Length));
                        g.Save();
                    });

                Add("REFLETS DU SOL", "La chaussée mouillée reflète la rue. Un second rendu de la scène.",
                    delegate { return g.ReflectionsEnabled ? "OUI" : "NON"; },
                    delegate(int d) { g.ReflectionsEnabled = !g.ReflectionsEnabled; g.Save(); });

                Add("FINESSE DES REFLETS", null,
                    delegate { return g.ReflectionDownsample <= 1 ? "PLEINE" : g.ReflectionDownsample == 2 ? "DEMI" : "QUART"; },
                    delegate(int d)
                    {
                        int[] steps = { 1, 2, 4 };
                        int index = Mathf.Max(0, Array.IndexOf(steps, g.ReflectionDownsample));
                        g.ReflectionDownsample = steps[Wrap(index - d, steps.Length)];
                        g.Save();
                    });

                Slider("LUMIÈRE DANS L'AIR", "Les faisceaux des lampes dans la bruine.", 0f, 2f,
                    delegate { return g.Volumetric; }, delegate(float v) { g.Volumetric = v; });
                Slider("LUMINOSITÉ", null, 0.5f, 2f, delegate { return g.Exposure; }, delegate(float v) { g.Exposure = v; });
                Slider("HALO (BLOOM)", "Le halo autour des néons et des lampes.", 0f, 3f,
                    delegate { return g.Bloom; }, delegate(float v) { g.Bloom = v; });
                Slider("CONTRASTE", null, 0.8f, 1.4f, delegate { return g.Contrast; }, delegate(float v) { g.Contrast = v; });
                Slider("SATURATION", null, 0f, 1.6f, delegate { return g.Saturation; }, delegate(float v) { g.Saturation = v; });
                Slider("VIGNETTE", null, 0f, 1f, delegate { return g.Vignette; }, delegate(float v) { g.Vignette = v; });
                Slider("GRAIN", null, 0f, 0.1f, delegate { return g.Grain; }, delegate(float v) { g.Grain = v; });
            }

            Separator();

            Slider("CHAMP DE VISION", "En degrés, verticalement.", 55f, 95f,
                delegate { return _fov; }, delegate(float v) { _fov = Mathf.Round(v); ApplyFov(); PlayerPrefs.SetFloat(Prefs + "fov", _fov); },
                delegate { return Mathf.RoundToInt(_fov) + "°"; });

            if (_look != null)
            {
                Slider("SENSIBILITÉ SOURIS", null, 0.2f, 3f,
                    delegate { return _look.UserSensitivity; }, delegate(float v) { _look.UserSensitivity = v; },
                    delegate { return _look.UserSensitivity.ToString("0.00"); });
            }

            Slider("VOLUME", null, 0f, 1f,
                delegate { return AudioListener.volume; },
                delegate(float v) { AudioListener.volume = v; PlayerPrefs.SetFloat(Prefs + "volume", v); },
                delegate { return Mathf.RoundToInt(AudioListener.volume * 100f) + " %"; });

            Add("COMPTEUR D'IMAGES", "Affiche les images par seconde en haut à gauche.",
                delegate { return _showFps ? "OUI" : "NON"; },
                delegate(int d) { _showFps = !_showFps; PlayerPrefs.SetInt(Prefs + "ips", _showFps ? 1 : 0); });

            Separator();

            Add("RÉINITIALISER", "Revenir aux réglages d'origine.", delegate
            {
                Play(_confirm, 0.8f);
                _preset = GraphicsDirector.Preset.Cinema;
                if (g != null)
                {
                    g.ApplyPreset(_preset);
                    g.AntiAliasing = UberPostProcess.AntiAliasingMode.Fxaa;
                    g.ReflectionsEnabled = true;
                    g.ReflectionDownsample = 2;
                    g.VSync = true;
                    g.Save();
                }

                _fov = _baseFov;
                ApplyFov();
                PlayerPrefs.SetFloat(Prefs + "fov", _fov);
                if (_look != null) _look.UserSensitivity = 1f;
                AudioListener.volume = 1f;
                PlayerPrefs.SetFloat(Prefs + "volume", 1f);
            });

            Add("RETOUR", null, Back);
        }

        private GraphicsDirector.Preset _preset = GraphicsDirector.Preset.Cinema;

        private static string PresetName(GraphicsDirector.Preset preset)
        {
            switch (preset)
            {
                case GraphicsDirector.Preset.Sobre: return "SOBRE";
                case GraphicsDirector.Preset.Batard: return "BÂTARD";
                default: return "CINÉMA";
            }
        }

        private static string AaName(UberPostProcess.AntiAliasingMode mode)
        {
            string name = mode.ToString().ToUpperInvariant();
            return name == "NONE" ? "AUCUN" : name;
        }

        private void StepResolution(int direction)
        {
            if (_resolutions.Count == 0)
            {
                Resolution[] all = Screen.resolutions;
                for (int i = 0; i < all.Length; i++)
                {
                    Vector2Int size = new Vector2Int(all[i].width, all[i].height);
                    if (!_resolutions.Contains(size)) _resolutions.Add(size);
                }
            }

            if (_resolutions.Count == 0) return;

            int current = _resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            int next = current < 0 ? _resolutions.Count - 1 : Wrap(current + direction, _resolutions.Count);
            Screen.SetResolution(_resolutions[next].x, _resolutions[next].y, Screen.fullScreenMode);
        }

        private void ApplyFov()
        {
            if (_gameCamera != null) _gameCamera.fieldOfView = Mathf.Clamp(_fov, 40f, 110f);
        }

        private void Go(Page page)
        {
            Play(_confirm, 0.7f);
            Show(page);
        }

        private void Add(string label, string hint, Action activate)
        {
            _items.Add(new Item { Label = label, Hint = hint, Activate = activate });
        }

        private void Add(string label, string hint, Func<string> value, Action<int> step)
        {
            _items.Add(new Item { Label = label, Hint = hint, Value = value, Step = step, Activate = delegate { step(1); } });
        }

        private void Slider(string label, string hint, float min, float max, Func<float> get, Action<float> set)
        {
            Slider(label, hint, min, max, get, set, null);
        }

        private void Slider(string label, string hint, float min, float max, Func<float> get, Action<float> set,
            Func<string> format)
        {
            Item item = new Item();
            item.Label = label;
            item.Hint = hint;
            item.Get01 = delegate { return Mathf.InverseLerp(min, max, get()); };
            item.Set01 = delegate(float t) { set(Mathf.Lerp(min, max, Mathf.Clamp01(t))); };
            item.Value = format ?? (Func<string>)delegate { return get().ToString("0.00"); };
            item.Step = delegate(int d) { item.Set01(item.Get01() + d * 0.05f); };
            _items.Add(item);
        }

        private void Separator()
        {
            _items.Add(new Item { Separator = true });
        }

        private static int Wrap(int value, int count)
        {
            return ((value % count) + count) % count;
        }

        // --------------------------------------------------------------- clavier

        private void HandleKeys()
        {
            if (_input == null || _input.Provider == null || _input.Bindings == null) return;

            var provider = _input.Provider;
            var bindings = _input.Bindings;

            if (_pageTime < 0.12f) return;

            if (provider.GetPressedThisFrame(bindings.phoneUp) || provider.GetPressedThisFrame(bindings.moveForward)) Move(-1);
            if (provider.GetPressedThisFrame(bindings.phoneDown) || provider.GetPressedThisFrame(bindings.moveBackward)) Move(1);

            Item current = _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;

            if (current != null && current.Step != null)
            {
                if (provider.GetPressedThisFrame(bindings.phoneLeft) || provider.GetPressedThisFrame(bindings.moveLeft))
                {
                    current.Step(-1);
                    Play(_tick, 1f);
                }

                if (provider.GetPressedThisFrame(bindings.phoneRight) || provider.GetPressedThisFrame(bindings.moveRight))
                {
                    current.Step(1);
                    Play(_tick, 1f);
                }
            }

            if (current != null && current.Activate != null &&
                (provider.GetPressedThisFrame(bindings.phoneSelect) || _input.InteractPressed || provider.GetPressedThisFrame(bindings.jump)))
            {
                if (current.Get01 == null)
                {
                    if (current.Step == null) Play(_confirm, 0.7f);
                    else Play(_tick, 1f);
                    current.Activate();
                }
            }

            if (_input.ReleaseCursorPressed || provider.GetPressedThisFrame(bindings.phoneBack)) Back();

            float wheel = _input.ScrollDelta;
            if (Mathf.Abs(wheel) > 0.01f) _scroll = Mathf.Max(0f, _scroll - Mathf.Sign(wheel) * 1.5f);
        }

        private void Move(int direction)
        {
            if (_items.Count == 0) return;

            int index = _selected;
            for (int i = 0; i < _items.Count; i++)
            {
                index = Wrap(index + direction, _items.Count);
                if (!_items[index].Separator) break;
            }

            if (index != _selected) Play(_tick, 1f);
            _selected = index;
        }

        // --------------------------------------------------------------- dessin

        private void OnGUI()
        {
            if (_showFps && _page == Page.None) DrawFps();
            if (_open <= 0.001f) return;

            GUI.depth = -40;

            float sw = Screen.width;
            float sh = Screen.height;
            float u = Mathf.Max(0.55f, sh / 1080f);

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = _open;

            bool title = _home == Page.Title;

            // Fond : sur l'ecran titre, un degrade qui laisse voir la scene a droite ; en
            // pause, un voile sur toute l'image figee.
            if (title)
            {
                EnsureGradient();
                Color color = GUI.color;
                GUI.color = new Color(0f, 0f, 0f, 0.92f * _open);
                GUI.DrawTexture(new Rect(0f, 0f, sw * 0.62f, sh), _gradient);
                GUI.color = color;

                GuiKit.Fill(new Rect(0f, 0f, sw, sh * 0.07f), new Color(0f, 0f, 0f, 1f));
                GuiKit.Fill(new Rect(0f, sh * 0.93f, sw, sh * 0.07f), new Color(0f, 0f, 0f, 1f));
            }
            else
            {
                GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0.02f, 0.02f, 0.04f, 0.72f));
            }

            float left = 110f * u;
            float top = title ? sh * 0.14f : sh * 0.12f;

            DrawLogo(left, top, u, title);

            float listTop = top + (title ? 230f : 150f) * u;
            if (_page == Page.Controls) DrawControls(left, listTop, u, sw, sh);
            DrawItems(left, _page == Page.Controls ? sh * 0.84f - 50f * u : listTop, u, sw, sh);
            DrawFooter(u, sw, sh);

            GuiKit.Alpha = previous;
        }

        private void DrawLogo(float left, float top, float u, bool title)
        {
            float glowPulse = 0.85f + Mathf.Sin(Time.unscaledTime * 2.1f) * 0.08f + Mathf.Sin(Time.unscaledTime * 13f) * 0.02f;

            if (title)
            {
                // Le halo du neon derriere le nom.
                GuiKit.Disc(new Rect(left - 120f * u, top - 90f * u, 900f * u, 330f * u), new Color(Accent.r, Accent.g, Accent.b, 0.22f * glowPulse));

                GUIStyle huge = GuiKit.Style(Mathf.RoundToInt(112f * u), FontStyle.Bold, TextAnchor.UpperLeft);
                GuiKit.OutlinedLabel(new Rect(left, top, 1200f * u, 130f * u), "ÜBER BAGARRE", huge,
                    new Color(1f, 0.95f, 0.97f), new Color(Accent.r * 0.5f, 0f, Accent.b * 0.3f, 0.95f), 3f * u);

                GuiKit.Fill(new Rect(left + 4f * u, top + 132f * u, 150f * u, 6f * u), Accent);

                GUIStyle tagline = GuiKit.Style(Mathf.RoundToInt(22f * u), FontStyle.Bold, TextAnchor.UpperLeft);
                GuiKit.OutlinedLabel(new Rect(left + 4f * u, top + 150f * u, 900f * u, 30f * u),
                    "LIVRAISON DE BAGARRES À DOMICILE", tagline, new Color(1f, 0.82f, 0.35f), new Color(0f, 0f, 0f, 0.9f), 1.5f);
                return;
            }

            string heading = PageName(_page);
            GUIStyle big = GuiKit.Style(Mathf.RoundToInt(64f * u), FontStyle.Bold, TextAnchor.UpperLeft);
            GuiKit.OutlinedLabel(new Rect(left, top, 1200f * u, 80f * u), heading, big, Ink, new Color(0f, 0f, 0f, 0.9f), 2f);
            GuiKit.Fill(new Rect(left + 3f * u, top + 82f * u, 110f * u, 5f * u), Accent);
        }

        private string PageName(Page page)
        {
            switch (page)
            {
                case Page.Pause: return "PAUSE";
                case Page.Chapters: return "CHAPITRES";
                case Page.Graphics: return "GRAPHISMES";
                case Page.Controls: return "COMMANDES";
                default: return "ÜBER BAGARRE";
            }
        }

        private void DrawItems(float left, float top, float u, float sw, float sh)
        {
            bool settings = _page == Page.Graphics;
            float rowHeight = (settings ? 44f : 58f) * u;
            float width = settings ? Mathf.Min(sw - left * 2f, 900f * u) : 620f * u;
            float bottom = sh * 0.86f;
            int visible = Mathf.Max(3, Mathf.FloorToInt((bottom - top) / rowHeight));

            // Le defilement suit la selection au clavier ; la molette le deplace librement.
            if (_selected < _scroll) _scroll = _selected;
            if (_selected >= _scroll + visible) _scroll = _selected - visible + 1;
            _scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, _items.Count - visible));

            int first = Mathf.FloorToInt(_scroll);
            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            GUIStyle label = GuiKit.Style(Mathf.RoundToInt((settings ? 21f : 30f) * u), FontStyle.Bold, TextAnchor.MiddleLeft);
            GUIStyle value = GuiKit.Style(Mathf.RoundToInt(20f * u), FontStyle.Bold, TextAnchor.MiddleRight);

            for (int row = 0; row < visible && first + row < _items.Count; row++)
            {
                int index = first + row;
                Item item = _items[index];
                Rect rect = new Rect(left, top + row * rowHeight, width, rowHeight - 6f * u);

                if (item.Separator)
                {
                    GuiKit.Fill(new Rect(rect.x, rect.center.y, rect.width, 1f), new Color(1f, 1f, 1f, 0.12f));
                    continue;
                }

                bool hover = rect.Contains(mouse);
                if (hover && (e.type == EventType.MouseMove || e.type == EventType.MouseDown) && _selected != index)
                {
                    _selected = index;
                    Play(_tick, 0.6f);
                }

                bool selected = index == _selected;
                float slide = selected ? 14f * u : 0f;

                if (selected)
                {
                    GuiKit.Fill(rect, new Color(1f, 1f, 1f, 0.07f));
                    GuiKit.Fill(new Rect(rect.x, rect.y, 5f * u, rect.height), Accent);
                }

                Color textColor = selected ? Ink : Dim;
                GuiKit.OutlinedLabel(new Rect(rect.x + 18f * u + slide, rect.y, rect.width * 0.55f, rect.height), item.Label, label,
                    textColor, new Color(0f, 0f, 0f, 0.85f), 1.5f);

                Rect barRect = new Rect(rect.xMax - 330f * u, rect.center.y - 5f * u, 220f * u, 10f * u);

                if (item.Get01 != null)
                {
                    // Barre de reglage : clic ou glisser pour choisir la valeur.
                    float t = Mathf.Clamp01(item.Get01());
                    GuiKit.Fill(barRect, new Color(1f, 1f, 1f, 0.12f));
                    GuiKit.Fill(new Rect(barRect.x, barRect.y, barRect.width * t, barRect.height), selected ? Accent : new Color(0.7f, 0.7f, 0.75f));
                    GuiKit.Fill(new Rect(barRect.x + barRect.width * t - 3f * u, barRect.y - 5f * u, 6f * u, barRect.height + 10f * u), Ink);

                    Rect grab = new Rect(barRect.x - 8f * u, rect.y, barRect.width + 16f * u, rect.height);
                    if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(mouse)) _dragging = index;
                    if (_dragging == index && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                    {
                        item.Set01((mouse.x - barRect.x) / barRect.width);
                        e.Use();
                    }

                    GuiKit.OutlinedLabel(new Rect(rect.xMax - 100f * u, rect.y, 90f * u, rect.height), item.Value(), value,
                        selected ? Ink : Dim, new Color(0f, 0f, 0f, 0.85f), 1f);
                }
                else if (item.Value != null)
                {
                    // Choix : fleches cliquables de part et d'autre de la valeur.
                    Rect valueRect = new Rect(rect.xMax - 330f * u, rect.y, 300f * u, rect.height);
                    GUIStyle centred = GuiKit.Style(Mathf.RoundToInt(20f * u), FontStyle.Bold, TextAnchor.MiddleCenter);
                    GuiKit.OutlinedLabel(valueRect, item.Value(), centred, selected ? Ink : Dim, new Color(0f, 0f, 0f, 0.85f), 1f);

                    Rect leftArrow = new Rect(valueRect.x, rect.y, 40f * u, rect.height);
                    Rect rightArrow = new Rect(valueRect.xMax - 40f * u, rect.y, 40f * u, rect.height);
                    GuiKit.OutlinedLabel(leftArrow, "‹", centred, selected ? Accent : Dim, new Color(0f, 0f, 0f, 0.85f), 1f);
                    GuiKit.OutlinedLabel(rightArrow, "›", centred, selected ? Accent : Dim, new Color(0f, 0f, 0f, 0.85f), 1f);

                    if (e.type == EventType.MouseDown && e.button == 0)
                    {
                        if (leftArrow.Contains(mouse)) { item.Step(-1); Play(_tick, 1f); e.Use(); }
                        else if (rightArrow.Contains(mouse) || rect.Contains(mouse)) { item.Step(1); Play(_tick, 1f); e.Use(); }
                    }
                }
                else if (e.type == EventType.MouseDown && e.button == 0 && hover && item.Activate != null && _pageTime > 0.12f)
                {
                    Play(_confirm, 0.7f);
                    e.Use();
                    item.Activate();
                    return;
                }
            }

            if (e.type == EventType.MouseUp) _dragging = -1;

            if (e.type == EventType.ScrollWheel)
            {
                _scroll = Mathf.Clamp(_scroll + Mathf.Sign(e.delta.y) * 1.5f, 0f, Mathf.Max(0f, _items.Count - visible));
                e.Use();
            }

            // L'explication de la ligne choisie, sous la liste.
            if (_selected >= 0 && _selected < _items.Count && !string.IsNullOrEmpty(_items[_selected].Hint))
            {
                GUIStyle hint = GuiKit.Style(Mathf.RoundToInt(18f * u), FontStyle.Normal, TextAnchor.UpperLeft, true);
                GuiKit.OutlinedLabel(new Rect(left + 18f * u, Mathf.Min(bottom, top + visible * rowHeight) + 8f * u, width, 50f * u),
                    _items[_selected].Hint, hint, new Color(0.85f, 0.86f, 0.9f), new Color(0f, 0f, 0f, 0.85f), 1f);
            }
        }

        private void DrawControls(float left, float top, float u, float sw, float sh)
        {
            if (_input == null || _input.Bindings == null) return;
            var b = _input.Bindings;

            string[,] rows =
            {
                { "Se déplacer", "ZQSD / WASD" },
                { "Sprinter", b.sprint.ToString() },
                { "Sauter", b.jump.ToString() },
                { "S'accroupir  (en courant : glissade)", b.crouch.ToString() },
                { "Direct", b.attackStraight.ToString() },
                { "Crochet", b.attackHook.ToString() },
                { "Uppercut", b.attackUppercut.ToString() },
                { "Coup de pied  /  balayette", b.attackKick + "  /  " + b.attackLowKick },
                { "Coup de tête", b.attackHeadbutt.ToString() },
                { "Bousculer", b.attackShove.ToString() },
                { "Garde  (au bon moment : parade)", b.guard.ToString() },
                { "Esquive", b.dodge.ToString() },
                { "Interagir, passer un dialogue", b.interact.ToString() },
                { "Téléphone", b.phone.ToString() },
                { "Téléphone : naviguer / valider / retour", "Flèches  /  Entrée  /  Retour arrière" },
                { "Pause", b.releaseCursor.ToString() },
                { "Caméra d'observation", b.toggleObserver.ToString() },
                { "Menu du bac à sable, triche", b.toggleSandboxMenu.ToString() },
            };

            float rowHeight = 33f * u;
            int count = rows.GetLength(0);
            int perColumn = Mathf.CeilToInt(count / 2f);
            float columnWidth = Mathf.Min((sw - left * 2f) * 0.5f, 760f * u);

            GUIStyle action = GuiKit.Style(Mathf.RoundToInt(19f * u), FontStyle.Normal, TextAnchor.MiddleLeft);
            GUIStyle key = GuiKit.Style(Mathf.RoundToInt(19f * u), FontStyle.Bold, TextAnchor.MiddleRight);

            for (int i = 0; i < count; i++)
            {
                int column = i / perColumn;
                int row = i % perColumn;
                Rect rect = new Rect(left + column * (columnWidth + 40f * u), top + row * rowHeight, columnWidth, rowHeight - 4f * u);

                GuiKit.Fill(rect, new Color(1f, 1f, 1f, row % 2 == 0 ? 0.05f : 0.02f));
                GuiKit.OutlinedLabel(new Rect(rect.x + 12f * u, rect.y, rect.width * 0.6f, rect.height), rows[i, 0], action,
                    Dim, new Color(0f, 0f, 0f, 0.85f), 1f);
                GuiKit.OutlinedLabel(new Rect(rect.x, rect.y, rect.width - 12f * u, rect.height), rows[i, 1].ToUpperInvariant(), key,
                    Ink, new Color(0f, 0f, 0f, 0.85f), 1f);
            }
        }

        private void DrawFooter(float u, float sw, float sh)
        {
            GUIStyle style = GuiKit.Style(Mathf.RoundToInt(17f * u), FontStyle.Normal, TextAnchor.MiddleRight);
            string text = _page == Page.Graphics
                ? "↑ ↓  choisir     ← →  régler     clic : régler     ÉCHAP  retour"
                : "↑ ↓  choisir     ENTRÉE  valider     ÉCHAP  retour";

            GuiKit.OutlinedLabel(new Rect(0f, sh - 44f * u, sw - 60f * u, 30f * u), text, style,
                new Color(1f, 1f, 1f, 0.6f), new Color(0f, 0f, 0f, 0.9f), 1f);
        }

        private void DrawFps()
        {
            GUIStyle style = GuiKit.Style(14, FontStyle.Bold, TextAnchor.UpperLeft);
            float fps = CameraDiagnostics.Fps;
            string text = fps > 0f ? Mathf.RoundToInt(fps) + " IPS" : "— IPS";
            Color color = fps >= 55f || fps <= 0f ? new Color(0.6f, 1f, 0.6f) : fps >= 30f ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.4f, 0.4f);

            GuiKit.OutlinedLabel(new Rect(12f, 8f, 200f, 22f), text, style, color, new Color(0f, 0f, 0f, 0.9f), 1f);
        }

        private void EnsureGradient()
        {
            if (_gradient != null) return;

            _gradient = new Texture2D(256, 1, TextureFormat.RGBA32, false);
            _gradient.wrapMode = TextureWrapMode.Clamp;
            _gradient.hideFlags = HideFlags.HideAndDontSave;

            for (int x = 0; x < 256; x++)
            {
                float t = x / 255f;
                float alpha = 1f - Mathf.SmoothStep(0.25f, 1f, t);
                _gradient.SetPixel(x, 0, new Color(1f, 1f, 1f, alpha));
            }

            _gradient.Apply();
        }

        // --------------------------------------------------------------- sons

        private void Play(AudioClip clip, float volume)
        {
            if (_ui != null && clip != null) _ui.PlayOneShot(clip, volume * _uiVolume);
        }

        private const int SampleRate = 22050;

        private static AudioClip Blip(string name, float frequency, float seconds, float level)
        {
            int length = Mathf.RoundToInt(seconds * SampleRate);
            float[] data = new float[length];

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                float envelope = Mathf.Exp(-t / (seconds * 0.35f));
                data[n] = (Mathf.Sin(2f * Mathf.PI * frequency * t) * 0.7f + Mathf.Sin(4f * Mathf.PI * frequency * t) * 0.3f) *
                          envelope * level;
            }

            AudioClip clip = AudioClip.Create(name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// La musique de l'écran titre : une nappe sombre de synthétiseur sur quatre accords
        /// (la mineur, fa, do, sol), une basse qui pulse doucement, bouclée sans couture.
        /// Composée ici même, faute de fichier son dans le projet.
        /// </summary>
        private static AudioClip Music()
        {
            const float bar = 4f;
            float[][] chords =
            {
                new[] { 110f, 130.81f, 164.81f, 220f },
                new[] { 87.31f, 130.81f, 174.61f, 220f },
                new[] { 98f, 130.81f, 164.81f, 196f },
                new[] { 98f, 123.47f, 146.83f, 196f },
            };

            int length = Mathf.RoundToInt(bar * chords.Length * SampleRate);
            float[] data = new float[length];
            float low = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                int chord = Mathf.Min(chords.Length - 1, (int)(t / bar));
                float inBar = t - chord * bar;

                // Fondu entre deux accords, pour que la boucle et les changements ne claquent pas.
                float fade = Mathf.Clamp01(inBar / 0.6f) * Mathf.Clamp01((bar - inBar) / 0.6f);

                float pad = 0f;
                float[] notes = chords[chord];
                for (int i = 0; i < notes.Length; i++)
                {
                    float f = notes[i];
                    // Deux voix un peu desaccordees par note : c'est ce qui fait « nappe ».
                    pad += Saw(f * t) * 0.5f + Saw(f * 1.004f * t + 0.3f) * 0.5f;
                }

                pad /= notes.Length;

                // Filtre passe-bas qui respire lentement.
                float cutoff = 0.04f + 0.03f * (0.5f + 0.5f * Mathf.Sin(t * 0.4f));
                low += (pad - low) * cutoff;

                // Basse : la fondamentale une octave en dessous, qui pulse sur chaque temps.
                float beat = t % 1f;
                float bass = Mathf.Sin(2f * Mathf.PI * notes[0] * 0.5f * t) * Mathf.Exp(-beat * 3.2f) *
                             Mathf.Clamp01(beat / 0.012f) * 0.45f;

                data[n] = (low * 0.55f + bass) * fade * 0.8f;
            }

            AudioClip clip = AudioClip.Create("Menu (musique)", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Saw(float phase)
        {
            return 2f * (phase - Mathf.Floor(phase + 0.5f));
        }
    }
}
