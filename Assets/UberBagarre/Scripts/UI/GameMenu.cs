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

        [SerializeField]
        [Tooltip("L'histoire du monde ouvert (la ville) : écran titre avec CONTINUER, NOUVELLE PARTIE, CHAPITRES.")]
        private World.OpenWorldStory _openWorld;

        [Header("Ecran titre")]
        [SerializeField] private bool _titleOnStart;

        [SerializeField]
        [Tooltip("Survol de la ville : des triplets (depart, arrivee, visee), un plan chacun, enchaines en fondu.")]
        private Transform[] _tour = new Transform[0];

        [SerializeField, Min(4f)] private float _tourShot = 13f;
        [SerializeField] private Transform _shotFrom;
        [SerializeField] private Transform _shotTo;
        [SerializeField] private Transform _shotTarget;
        [SerializeField, Min(5f)] private float _shotDuration = 44f;
        [SerializeField, Range(15f, 80f)] private float _shotFov = 40f;

        [Header("Scenes")]
        [SerializeField] private string _storyScene = "Prologue";
        [SerializeField] private string _sandboxScene = "CombatSandbox";
        [SerializeField] private string _openWorldScene = "MondeOuvert";

        [Header("Son")]
        [SerializeField, Range(0f, 1f)] private float _musicVolume = 0.42f;
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

        // Transition entre deux pages : l'ancienne file vers la droite, puis la nouvelle arrive.
        private const float LeaveDuration = 0.16f;
        private bool _leaving;
        private float _leaveTime;
        private Page _nextPage;

        // Ecran titre : depuis quand il est ouvert (allumage du neon), rapprochement de la
        // camera selon la page, pluie a l'ecran, voiture qui passe.
        private float _titleTime;
        private float _zoom;
        private float _zoomVelocity;
        private float _lastSweep = -100f;
        private float[] _rainX;
        private float[] _rainY;
        private float[] _rainLength;
        private float[] _rainSpeed;
        private float _rainClock;
        private readonly GUIContent _glyph = new GUIContent();
        private static readonly string[] LogoLetters = { "Ü", "B", "E", "R", " ", "B", "A", "G", "A", "R", "R", "E" };

        private AudioSource _music;
        private AudioSource _ui;
        private AudioClip _musicClip;
        private AudioClip _tick;
        private AudioClip _confirm;
        private AudioClip _whoosh;
        private AudioClip _carPass;

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
            _whoosh = Swoosh("Menu (transition)", 0.28f, 0.35f, 3);
            _carPass = Swoosh("Menu (voiture qui passe)", 3.2f, 0.9f, 8);
        }

        private void Start()
        {
            ApplyFov();

            if (_titleOnStart && (_prologue != null || _openWorld != null)) Open(Page.Title);
        }

        private void OnDisable()
        {
            if (_page != Page.None) CloseAll();
        }

        private void OnDestroy()
        {
            if (_musicClip != null) Destroy(_musicClip);
            if (_tick != null) Destroy(_tick);
            if (_confirm != null) Destroy(_confirm);
            if (_whoosh != null) Destroy(_whoosh);
            if (_carPass != null) Destroy(_carPass);
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

            if (_leaving)
            {
                _leaveTime += dt;
                if (_leaveTime >= LeaveDuration)
                {
                    _leaving = false;
                    ShowNow(_nextPage);
                }

                return;
            }

            HandleKeys();
        }

        private bool CanPause()
        {
            // Un écran plein (armoire, ordinateur) : Échap le referme, lui.
            if (FullScreenPanel.AnyOpen) return false;

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
            _leaving = false;

            IsOpen = false;
            ModalScreen.Set(this, false);
            if (_input != null) _input.SetGameplayLock(this, false);

            if (_cursor != null)
            {
                _cursor.enabled = true;
                _cursor.SetLocked(true);
            }
        }

        /// <summary>Change de page : l'ancienne s'efface vers la droite, la nouvelle entre par la gauche.</summary>
        private void Show(Page page)
        {
            if (_page == Page.None || _page == page)
            {
                ShowNow(page);
                return;
            }

            _nextPage = page;
            if (_leaving) return;

            _leaving = true;
            _leaveTime = 0f;
            _dragging = -1;
            Play(_whoosh, 0.7f);
        }

        private void ShowNow(Page page)
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
            if (_prologue == null && _openWorld == null)
            {
                LoadStoryScene();
                return;
            }

            Play(_confirm, 1f);

            // L'écran de chargement couvre tout, puis la partie se met en place derrière lui
            // (au lieu d'une coupe sèche du menu vers le jeu).
            LoadingScreen.Hold(this, beat == "continuer" ? "REPRISE" : "NOUVELLE PARTIE", "");

            // Depuis la pause, on relance tout de suite (la pause fige le temps).
            if (_home == Page.Pause)
            {
                CloseAll();
                Launch(beat);
                LoadingScreen.Release(this);
                return;
            }

            _starting = true;
            _startTimer = 0f;
            _startBeat = beat;
        }

        private void UpdateStart(float dt)
        {
            _startTimer += dt;
            if (_home == Page.Title) UpdateShot(dt);
            if (!LoadingScreen.Covering && _startTimer < 1.5f) return;

            _starting = false;
            if (_fader != null) _fader.SetBlackImmediate();
            CloseAll();
            Launch(_startBeat);
            LoadingScreen.Release(this);
        }

        private void Launch(string beat)
        {
            if (_openWorld != null)
            {
                // « continuer », ou « chapitre:N » (0 = nouvelle partie).
                if (beat == "continuer") _openWorld.Continue();
                else if (beat != null && beat.StartsWith("chapitre:")) _openWorld.StartChapter(int.Parse(beat.Substring(9)));
                else _openWorld.NewGame();
                return;
            }

            if (_prologue == null) return;

            if (string.IsNullOrEmpty(beat)) _prologue.Begin();
            else _prologue.StartAt(beat);
        }

        private void LoadStoryScene()
        {
            if (!Application.CanStreamedLevelBeLoaded(_storyScene)) return;

            Play(_confirm, 1f);
            if (_page != Page.None) CloseAll();
            LoadingScreen.LoadScene(_storyScene, "LE PROLOGUE", "La planque, le courrier, l'appel de Sami");
        }

        private void LoadSandbox()
        {
            if (!Application.CanStreamedLevelBeLoaded(_sandboxScene)) return;

            Play(_confirm, 1f);
            CloseAll();
            LoadingScreen.LoadScene(_sandboxScene, "BAC À SABLE", "L'arène d'entraînement");
        }

        private void LoadOpenWorld()
        {
            if (!Application.CanStreamedLevelBeLoaded(_openWorldScene)) return;

            Play(_confirm, 1f);
            CloseAll();
            LoadingScreen.LoadScene(_openWorldScene, "HYLAND POINT", "La ville se réveille");
        }

        private void BackToTitle()
        {
            Play(_confirm, 1f);

            // L'ecran titre vit dans la scene de l'histoire : on la recharge, proprement.
            string scene = _prologue != null || _openWorld != null ? SceneManager.GetActiveScene().name : _storyScene;
            if (!Application.CanStreamedLevelBeLoaded(scene)) return;

            CloseAll();
            LoadingScreen.LoadScene(scene, "ÜBER BAGARRE", "Retour au menu");
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
            _titleTime = 0f;
            _zoom = 0f;
            _zoomVelocity = 0f;
            _lastSweep = 6f;

            if (_menuCamera == null || !HasShot) return;

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

        private bool HasShot
        {
            get { return (_shotFrom != null && _shotTo != null) || TourShots > 0; }
        }

        private int TourShots
        {
            get { return _tour != null ? _tour.Length / 3 : 0; }
        }

        /// <summary>Le noir entre deux plans du survol (0 = image, 1 = noir).</summary>
        private float TourBlack
        {
            get
            {
                if (TourShots < 2) return 0f;
                float t = _shotTime % _tourShot;
                float edge = Mathf.Min(t, _tourShot - t);
                return 1f - Mathf.Clamp01(edge / 0.7f);
            }
        }

        private void UpdateShot(float dt)
        {
            if (_menuCamera == null || !HasShot) return;

            _shotTime += dt;
            _titleTime += dt;

            Vector3 position;
            Vector3 target;

            if (TourShots > 0)
            {
                // Le survol : un plan après l'autre, chacun glisse doucement de son départ à son
                // arrivée en regardant son point ; un fondu au noir les sépare.
                int shot = Mathf.FloorToInt(_shotTime / _tourShot) % TourShots;
                float k = Mathf.SmoothStep(0f, 1f, (_shotTime % _tourShot) / _tourShot);
                Transform from = _tour[shot * 3];
                Transform to = _tour[shot * 3 + 1];
                Transform look = _tour[shot * 3 + 2];
                position = from != null && to != null ? Vector3.Lerp(from.position, to.position, k) : _menuCamera.transform.position;
                target = look != null ? look.position : position + Vector3.forward;
            }
            else
            {
                // Un aller-retour tres lent, adouci aux extremites : un travelling, pas un manege.
                float phase = Mathf.PingPong(_shotTime / _shotDuration, 1f);
                float t = Mathf.SmoothStep(0f, 1f, phase);

                position = Vector3.Lerp(_shotFrom.position, _shotTo.position, t);
                target = _shotTarget != null ? _shotTarget.position : position + _shotFrom.forward;
            }

            // Chaque page a son cadre : la camera s'avance et tourne un peu quand on entre dans un
            // sous-menu, et recule en revenant au titre. C'est la transition entre les menus.
            Page shown = _leaving ? _nextPage : _page;
            float zoomTarget = shown == Page.Chapters ? 0.45f : shown == Page.Graphics ? 0.6f : shown == Page.Controls ? 0.3f : 0f;
            _zoom = Mathf.SmoothDamp(_zoom, zoomTarget, ref _zoomVelocity, 0.55f, Mathf.Infinity, Mathf.Max(0.0001f, dt));

            Vector3 offset = position - target;
            float swing = shown == Page.Graphics ? -18f : shown == Page.Chapters ? 12f : 0f;
            Vector3 closer = target + Quaternion.Euler(0f, swing * _zoom, 0f) * offset * 0.55f + Vector3.up * 0.25f * _zoom;
            position = Vector3.Lerp(position, closer, _zoom);
            _menuCamera.fieldOfView = Mathf.Lerp(_shotFov, _shotFov * 0.82f, _zoom);

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
                    if (_openWorld != null)
                    {
                        BuildOpenWorldTitle();
                        break;
                    }

                    Add("NOUVELLE PARTIE", "Le prologue : la planque, le courrier, l'appel de Sami.", delegate { StartStory(null); });
                    Add("CHAPITRES", "Reprendre à un chapitre précis.", delegate { Go(Page.Chapters); });
                    if (Application.CanStreamedLevelBeLoaded(_openWorldScene))
                    {
                        Add("MONDE OUVERT", "La ville entière : le Vertigo, la planque, le parking, et les courses qui tombent sur le téléphone.",
                            LoadOpenWorld);
                    }

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
                    if (_openWorld != null)
                    {
                        Add("MENU PRINCIPAL", "Revenir à l'écran titre. Ce qui compte est gardé à la dernière nuit passée au lit.", BackToTitle);
                    }
                    else if (story || Application.CanStreamedLevelBeLoaded(_storyScene))
                    {
                        Add("MENU PRINCIPAL", story ? "Revenir à l'écran titre (la partie en cours est perdue)." : "Quitter le bac à sable.",
                            BackToTitle);
                    }
                    Add("QUITTER LE JEU", null, Quit);
                    break;

                case Page.Chapters:
                    if (_openWorld != null)
                    {
                        for (int i = 0; i < World.OpenWorldStory.ChapterTitles.Length; i++)
                        {
                            int chapter = i;
                            Add(World.OpenWorldStory.ChapterTitles[i], World.OpenWorldStory.ChapterPitches[i] +
                                (PlayerProgress.HasSave ? "  (Ta sauvegarde sera remplacée à la prochaine nuit.)" : ""),
                                delegate { StartStory("chapitre:" + chapter); });
                        }

                        Separator();
                        Add("RETOUR", null, Back);
                        break;
                    }

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

        /// <summary>L'écran titre de la ville : reprendre, recommencer, choisir un chapitre.</summary>
        private void BuildOpenWorldTitle()
        {
            if (PlayerProgress.HasSave)
            {
                Add("CONTINUER", PlayerProgress.SaveDescription(),
                    delegate { StartStory("continuer"); });
            }

            Add("NOUVELLE PARTIE", "Motel Hyland, chambre 3. Douze mille euros de dettes, un téléphone, et une appli qui n'existe pas." +
                                   (PlayerProgress.HasSave ? "  (La sauvegarde sera remplacée à la première nuit.)" : ""),
                delegate { StartStory("chapitre:0"); });
            // Pas de chapitres : l'histoire ne s'annonce pas, elle arrive au téléphone, jour après jour.

            if (Application.CanStreamedLevelBeLoaded(_sandboxScene))
            {
                Add("BAC À SABLE", "L'arène d'entraînement : adversaires, réglages, triche.", LoadSandbox);
            }

            Add("GRAPHISMES", "Image, qualité, affichage, souris et son.", delegate { Go(Page.Graphics); });
            Add("COMMANDES", "Toutes les touches.", delegate { Go(Page.Controls); });
            Add("QUITTER", null, Quit);
        }

        private void BuildGraphics()
        {
            GraphicsDirector g = _graphics;

            if (g != null)
            {
                Add("PRÉRÉGLAGE", "Ville (clair, façon Schedule I), Sobre, Cinéma ou Bâtard : bloom, contraste, vignette, lumière dans l'air.",
                    delegate { return PresetName(_preset); },
                    delegate(int d) { _preset = (GraphicsDirector.Preset)Wrap((int)_preset + d, 4); g.ApplyPreset(_preset); });
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
                _preset = GraphicsDirector.Preset.Ville;
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

        private GraphicsDirector.Preset _preset = GraphicsDirector.Preset.Ville;

        private static string PresetName(GraphicsDirector.Preset preset)
        {
            switch (preset)
            {
                case GraphicsDirector.Preset.Sobre: return "SOBRE";
                case GraphicsDirector.Preset.Batard: return "BÂTARD";
                case GraphicsDirector.Preset.Ville: return "VILLE";
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

            // Fond : sur l'ecran titre, le survol de la ville, la pluie, une voiture qui passe de
            // temps en temps, et un voile sombre en haut et en bas ; en pause, l'image figee
            // assombrie, et un panneau au milieu.
            if (title)
            {
                DrawRain(sw, sh, u);
                DrawPassingCar(sw, sh);
                DrawVignette(sw, sh);

                // Entre deux plans du survol, un noir bref ; tant que la ville se charge, du noir
                // (l'ecran de chargement est par-dessus).
                float black = TourBlack;
                if (_openWorld != null && !World.MapStreamer.Ready && _titleTime < 30f) black = 1f;
                if (black > 0.001f) GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, black));

                // Les bandes de cinema, qui s'ouvrent a l'arrivee de l'ecran titre.
                float bars = Mathf.Lerp(0.5f, 0.055f, Ease(Mathf.Clamp01(_titleTime / 1.2f)));
                GuiKit.Fill(new Rect(0f, 0f, sw, sh * bars), new Color(0f, 0f, 0f, 1f));
                GuiKit.Fill(new Rect(0f, sh * (1f - bars), sw, sh * bars), new Color(0f, 0f, 0f, 1f));
            }
            else
            {
                GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0.02f, 0.018f, 0.035f, 0.74f));
                GuiKit.Disc(new Rect(sw * 0.15f, sh * 0.1f, sw * 0.7f, sh * 0.8f), new Color(Accent.r, Accent.g, Accent.b, 0.07f));
            }

            if (_page == Page.Title)
            {
                float glowPulse = 0.85f + Mathf.Sin(Time.unscaledTime * 2.1f) * 0.08f + Mathf.Sin(Time.unscaledTime * 13f) * 0.02f;
                DrawNeonLogo(sw * 0.5f, sh * 0.13f, u, glowPulse);

                float width = 470f * u;
                Rect column = new Rect((sw - width) * 0.5f, sh * 0.4f, width, sh * 0.5f);
                GuiKit.Disc(new Rect(column.x - 260f * u, column.y - 120f * u, column.width + 520f * u, column.height + 200f * u),
                    new Color(0f, 0f, 0f, 0.45f));
                DrawItems(column, true, u);
            }
            else
            {
                Rect panel = PanelRect(sw, sh, u);
                Rect content = DrawPanel(panel, u);

                if (_page == Page.Controls)
                {
                    float list = 90f * u;
                    DrawControls(new Rect(content.x, content.y, content.width, content.height - list), u);
                    DrawItems(new Rect(content.x, content.yMax - list + 16f * u, content.width, list), false, u);
                }
                else
                {
                    DrawItems(content, _page == Page.Pause, u);
                }
            }

            DrawFooter(u, sw, sh);

            GuiKit.Alpha = previous;
        }

        /// <summary>Le voile de l'écran titre : sombre en haut (sous le logo) et en bas, clair au milieu.</summary>
        private void DrawVignette(float sw, float sh)
        {
            const int steps = 24;
            for (int i = 0; i < steps; i++)
            {
                float t = i / (float)steps;
                float top = Mathf.Pow(1f - t, 2f) * 0.62f;
                float bottom = Mathf.Pow(t, 2.2f) * 0.8f;
                GuiKit.Fill(new Rect(0f, sh * t, sw, sh / steps + 1f), new Color(0f, 0f, 0f, Mathf.Max(top, bottom)));
            }
        }

        /// <summary>La place du panneau d'une page (centré), selon ce qu'il contient.</summary>
        private Rect PanelRect(float sw, float sh, float u)
        {
            Page shown = _page;
            float width;
            float height;

            switch (shown)
            {
                case Page.Graphics:
                    width = Mathf.Min(sw - 80f * u, 1060f * u);
                    height = sh * 0.8f;
                    break;
                case Page.Controls:
                    width = Mathf.Min(sw - 80f * u, 1400f * u);
                    height = Mathf.Min(sh * 0.84f, 560f * u + 200f * u);
                    break;
                case Page.Chapters:
                    width = Mathf.Min(sw - 80f * u, 720f * u);
                    height = Mathf.Min(sh * 0.8f, (150f + _items.Count * 64f + 90f) * u);
                    break;
                default:
                    width = Mathf.Min(sw - 80f * u, 560f * u);
                    height = Mathf.Min(sh * 0.84f, (150f + _items.Count * 72f + 80f) * u);
                    break;
            }

            return new Rect((sw - width) * 0.5f, (sh - height) * 0.5f, width, height);
        }

        /// <summary>
        /// Le panneau d'une page : verre sombre aux coins arrondis, titre en haut à gauche et son
        /// trait d'accent. Il monte un peu en apparaissant et s'efface en changeant de page.
        /// Rend la zone intérieure.
        /// </summary>
        private Rect DrawPanel(Rect panel, float u)
        {
            float appear = Ease(Mathf.Clamp01(_pageTime / 0.28f));
            float leave = _leaving ? Ease(Mathf.Clamp01(_leaveTime / LeaveDuration)) : 0f;
            panel.y += (1f - appear) * 24f * u - leave * 12f * u;

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * appear * (1f - leave);

            float radius = 22f * u;
            GuiKit.Glow(panel, new Color(0f, 0f, 0f, 0.6f), radius, 30f * u);
            GuiKit.Rounded(panel, new Color(0.075f, 0.068f, 0.11f, 0.94f), radius);
            GuiKit.RoundedOutline(panel, new Color(1f, 1f, 1f, 0.07f), radius, 1f);
            GuiKit.Rounded(new Rect(panel.x + radius, panel.y, panel.width - radius * 2f, 3f * u), new Color(Accent.r, Accent.g, Accent.b, 0.8f), 1.5f * u);

            GUIStyle heading = GuiKit.Text(Mathf.RoundToInt(40f * u), GuiKit.Weight.Black, TextAnchor.MiddleLeft);
            GuiKit.ShadowLabel(new Rect(panel.x + 40f * u, panel.y + 26f * u, panel.width - 80f * u, 56f * u), PageName(_page), heading, Ink, 0.5f);

            float grow = Ease(Mathf.Clamp01((_pageTime - 0.08f) / 0.3f));
            GuiKit.Rounded(new Rect(panel.x + 42f * u, panel.y + 84f * u, 64f * u * grow, 4f * u), Accent, 2f * u);

            GuiKit.Alpha = previous;
            return new Rect(panel.x + 36f * u, panel.y + 112f * u, panel.width - 72f * u, panel.height - 140f * u);
        }

        /// <summary>
        /// Le nom du jeu, allumé comme un néon, centré sur <paramref name="centre"/> : les lettres
        /// s'allument une à une en grésillant, puis de temps en temps l'une d'elles clignote, et
        /// toutes les huit secondes l'enseigne « saute » (décalage rouge et cyan).
        /// </summary>
        private void DrawNeonLogo(float centre, float top, float u, float glowPulse)
        {
            GUIStyle huge = GuiKit.Text(Mathf.RoundToInt(118f * u), GuiKit.Weight.Black, TextAnchor.UpperLeft);

            float total = 0f;
            for (int i = 0; i < LogoLetters.Length; i++)
            {
                _glyph.text = LogoLetters[i];
                total += huge.CalcSize(_glyph).x * 0.96f;
            }

            float left = centre - total * 0.5f;

            int lit = 0;
            for (int i = 0; i < LogoLetters.Length; i++)
            {
                if (LetterLight(i) > 0.5f) lit++;
            }

            float ignition = lit / (float)LogoLetters.Length;
            GuiKit.Disc(new Rect(centre - total * 0.5f - 200f * u, top - 90f * u, total + 400f * u, 330f * u),
                new Color(Accent.r, Accent.g, Accent.b, 0.26f * glowPulse * ignition));

            bool glitch = _titleTime > 3f && (_titleTime % 8f) > 7.82f;
            if (glitch)
            {
                DrawLetters(huge, left - 5f * u, top, u, new Color(1f, 0.15f, 0.2f, 0.55f), false);
                DrawLetters(huge, left + 5f * u, top + 2f * u, u, new Color(0.2f, 0.9f, 1f, 0.55f), false);
            }

            DrawLetters(huge, left, top, u, new Color(1f, 0.95f, 0.97f), true);

            float bar = Ease(Mathf.Clamp01((_titleTime - 1.3f) / 0.5f));
            GuiKit.Rounded(new Rect(centre - 90f * u * bar, top + 146f * u, 180f * u * bar, 5f * u), Accent, 2.5f * u);

            float tag = Mathf.Clamp01((_titleTime - 1.6f) / 0.6f);
            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * tag;

            GUIStyle tagline = GuiKit.Text(Mathf.RoundToInt(21f * u), GuiKit.Weight.Bold, TextAnchor.UpperCenter);
            GuiKit.ShadowLabel(new Rect(0f, top + 166f * u + (1f - tag) * 12f * u, Screen.width, 30f * u),
                "LIVRAISON DE BAGARRES À DOMICILE", tagline, new Color(1f, 0.82f, 0.35f), 0.7f);

            GuiKit.Alpha = previous;
        }

        private void DrawLetters(GUIStyle style, float left, float top, float u, Color color, bool flicker)
        {
            float x = left;
            Color outline = new Color(Accent.r * 0.5f, 0f, Accent.b * 0.3f, 0.95f);

            for (int i = 0; i < LogoLetters.Length; i++)
            {
                _glyph.text = LogoLetters[i];
                float width = style.CalcSize(_glyph).x;
                float light = flicker ? LetterLight(i) : (LetterLight(i) > 0.5f ? 1f : 0f);

                if (light > 0.01f && LogoLetters[i] != " ")
                {
                    // Eteinte, une lettre de neon n'est pas invisible : c'est un tube gris sombre.
                    Color c = Color.Lerp(new Color(0.25f, 0.2f, 0.24f, color.a * 0.6f), color, light);
                    Color o = new Color(outline.r, outline.g, outline.b, outline.a * Mathf.Max(0.3f, light));
                    float drop = (1f - Mathf.Clamp01((_titleTime - LetterStart(i)) / 0.25f)) * -10f * u;

                    GuiKit.OutlinedLabel(new Rect(x, top + drop, width + 20f * u, 140f * u), LogoLetters[i], style, c, o, 3f * u);
                }

                x += width * 0.96f;
            }
        }

        private static float LetterStart(int index)
        {
            return 0.3f + index * 0.075f;
        }

        /// <summary>0 = éteinte, 1 = allumée. Grésille à l'allumage, puis clignote rarement.</summary>
        private float LetterLight(int index)
        {
            float t = _titleTime - LetterStart(index);
            if (t < 0f) return 0f;

            if (t < 0.32f)
            {
                float hash = Mathf.Abs(Mathf.Sin((Mathf.Floor(t * 28f) + index * 13.7f) * 12.9898f) * 43758.5453f) % 1f;
                return hash > 0.45f ? 1f : 0.12f;
            }

            // Une lettre fatiguee sur quatre lache une fraction de seconde, de loin en loin.
            if (index % 4 == 1)
            {
                float cycle = (_titleTime + index * 1.37f) % 6.3f;
                if (cycle < 0.09f || (cycle > 0.16f && cycle < 0.21f)) return 0.15f;
            }

            return 1f;
        }

        /// <summary>La pluie, à l'écran : de fines traînées qui tombent vite, en biais.</summary>
        private void DrawRain(float sw, float sh, float u)
        {
            const int count = 70;

            if (_rainX == null)
            {
                _rainX = new float[count];
                _rainY = new float[count];
                _rainLength = new float[count];
                _rainSpeed = new float[count];

                System.Random random = new System.Random(11);
                for (int i = 0; i < count; i++)
                {
                    _rainX[i] = (float)random.NextDouble();
                    _rainY[i] = (float)random.NextDouble();
                    _rainLength[i] = 0.03f + (float)random.NextDouble() * 0.05f;
                    _rainSpeed[i] = 0.9f + (float)random.NextDouble() * 0.8f;
                }

                _rainClock = Time.unscaledTime;
            }

            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(0.1f, Time.unscaledTime - _rainClock);
                _rainClock = Time.unscaledTime;

                for (int i = 0; i < count; i++)
                {
                    _rainY[i] += _rainSpeed[i] * dt;
                    _rainX[i] += _rainSpeed[i] * dt * 0.08f;
                    if (_rainY[i] > 1.1f) { _rainY[i] -= 1.2f; _rainX[i] = (_rainX[i] * 7.31f + 0.37f) % 1f; }
                }
            }

            for (int i = 0; i < count; i++)
            {
                float alpha = 0.05f + (i % 5) * 0.025f;
                GuiKit.Fill(new Rect(_rainX[i] * sw, _rainY[i] * sh, Mathf.Max(1f, 1.4f * u), _rainLength[i] * sh),
                    new Color(0.75f, 0.82f, 0.95f, alpha));
            }
        }

        /// <summary>De temps en temps, une voiture passe dans la rue : un balayage de phares, et son bruit.</summary>
        private void DrawPassingCar(float sw, float sh)
        {
            if (Event.current.type == EventType.Repaint && _titleTime - _lastSweep > 17f)
            {
                _lastSweep = _titleTime;
                Play(_carPass, 1f);
            }

            float age = _titleTime - _lastSweep;
            if (age < 0.4f || age > 3.2f) return;

            float p = (age - 0.4f) / 2.8f;
            float x = Mathf.Lerp(sw * 1.2f, -sw * 0.6f, p);
            float strength = Mathf.Sin(p * Mathf.PI);

            GuiKit.Disc(new Rect(x, sh * 0.35f, sw * 0.55f, sh * 0.5f), new Color(1f, 0.9f, 0.7f, 0.13f * strength));
            GuiKit.Disc(new Rect(x + sw * 0.12f, sh * 0.5f, sw * 0.3f, sh * 0.25f), new Color(1f, 0.95f, 0.85f, 0.1f * strength));
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

        /// <summary>
        /// Les lignes d'une page, dans <paramref name="area"/>. En « pilules » (écran titre,
        /// pause) : de gros boutons arrondis, texte centré, celui choisi allumé en magenta. Sinon
        /// (réglages, chapitres) : des lignes, libellé à gauche, valeur à droite.
        /// </summary>
        private void DrawItems(Rect area, bool pills, float u)
        {
            bool settings = _page == Page.Graphics;
            float rowHeight = (pills ? 60f : settings ? 46f : 58f) * u;
            float gap = (pills ? 12f : 6f) * u;
            float step = rowHeight + gap;
            float hintSpace = 64f * u;
            int visible = Mathf.Max(1, Mathf.FloorToInt((area.height - hintSpace + gap) / step));

            // Le defilement suit la selection au clavier ; la molette le deplace librement.
            if (_selected < _scroll) _scroll = _selected;
            if (_selected >= _scroll + visible) _scroll = _selected - visible + 1;
            _scroll = Mathf.Clamp(_scroll, 0f, Mathf.Max(0f, _items.Count - visible));

            int first = Mathf.FloorToInt(_scroll);
            Event e = Event.current;
            Vector2 mouse = e.mousePosition;

            GUIStyle label = pills
                ? GuiKit.Text(Mathf.RoundToInt(23f * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter)
                : GuiKit.Text(Mathf.RoundToInt((settings ? 19f : 22f) * u), GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            GUIStyle value = GuiKit.Text(Mathf.RoundToInt(18f * u), GuiKit.Weight.Bold, TextAnchor.MiddleRight);
            GUIStyle centred = GuiKit.Text(Mathf.RoundToInt(18f * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle arrow = GuiKit.Text(Mathf.RoundToInt(26f * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter);

            // Les lignes montent l'une apres l'autre, et s'effacent en changeant de page.
            float baseAlpha = GuiKit.Alpha;
            float leave = _leaving ? Ease(Mathf.Clamp01(_leaveTime / LeaveDuration)) : 0f;
            bool interactive = !_leaving;
            float listBottom = area.y;

            for (int row = 0; row < visible && first + row < _items.Count; row++)
            {
                int index = first + row;
                Item item = _items[index];

                float appear = Ease(Mathf.Clamp01((_pageTime - 0.05f - row * 0.04f) / 0.26f));
                float rowAlpha = appear * (1f - leave);
                Rect rect = new Rect(area.x, area.y + row * step + (1f - appear) * 18f * u - leave * 10f * u, area.width, rowHeight);
                listBottom = Mathf.Max(listBottom, area.y + row * step + rowHeight);
                if (rowAlpha <= 0.01f) continue;

                GuiKit.Alpha = baseAlpha * rowAlpha;

                if (item.Separator)
                {
                    GuiKit.Fill(new Rect(rect.x + 20f * u, rect.center.y, rect.width - 40f * u, 1f), new Color(1f, 1f, 1f, 0.1f));
                    continue;
                }

                bool hover = interactive && rect.Contains(mouse);
                if (hover && (e.type == EventType.MouseMove || e.type == EventType.MouseDown) && _selected != index)
                {
                    _selected = index;
                    Play(_tick, 0.6f);
                }

                bool selected = index == _selected;
                float radius = (pills ? rowHeight * 0.5f : 12f * u);

                if (pills)
                {
                    if (selected)
                    {
                        float breathe = 0.4f + Mathf.Sin(Time.unscaledTime * 4f) * 0.08f;
                        Rect big = new Rect(rect.x - 6f * u, rect.y, rect.width + 12f * u, rect.height);
                        GuiKit.Glow(big, new Color(Accent.r, Accent.g, Accent.b, breathe), radius, 20f * u);
                        GuiKit.Rounded(big, Accent, radius);

                        // Un reflet balaie le bouton choisi.
                        float sweep = (Time.unscaledTime * 0.8f) % 1.8f;
                        if (sweep < 1f)
                        {
                            float glint = 70f * u;
                            GuiKit.Rounded(new Rect(big.x + (big.width - glint) * sweep, big.y, glint, big.height),
                                new Color(1f, 1f, 1f, 0.12f * (1f - sweep)), radius);
                        }

                        rect = big;
                    }
                    else
                    {
                        GuiKit.Rounded(rect, new Color(0.07f, 0.06f, 0.1f, 0.78f), radius);
                        GuiKit.RoundedOutline(rect, new Color(1f, 1f, 1f, hover ? 0.22f : 0.1f), radius, 1f);
                    }

                    GuiKit.ShadowLabel(rect, item.Label, label, selected ? Color.white : new Color(0.84f, 0.84f, 0.88f), selected ? 0.35f : 0.5f);
                }
                else
                {
                    if (selected)
                    {
                        GuiKit.Rounded(rect, new Color(Accent.r, Accent.g, Accent.b, 0.16f), radius);
                        GuiKit.RoundedOutline(rect, new Color(Accent.r, Accent.g, Accent.b, 0.75f), radius, 1.5f * u);
                        GuiKit.Rounded(new Rect(rect.x + 8f * u, rect.y + rect.height * 0.25f, 4f * u, rect.height * 0.5f), Accent, 2f * u);
                    }
                    else
                    {
                        GuiKit.Rounded(rect, new Color(1f, 1f, 1f, hover ? 0.07f : 0.035f), radius);
                    }

                    GuiKit.ShadowLabel(new Rect(rect.x + 26f * u, rect.y, rect.width * 0.55f, rect.height), item.Label, label,
                        selected ? Ink : Dim, 0.4f);
                }

                Rect barRect = new Rect(rect.xMax - 340f * u, rect.center.y - 3f * u, 220f * u, 6f * u);

                if (item.Get01 != null)
                {
                    // Barre de reglage : clic ou glisser pour choisir la valeur.
                    float t = Mathf.Clamp01(item.Get01());
                    GuiKit.Rounded(barRect, new Color(1f, 1f, 1f, 0.12f), 3f * u);
                    GuiKit.Rounded(new Rect(barRect.x, barRect.y, Mathf.Max(barRect.height, barRect.width * t), barRect.height),
                        selected ? Accent : new Color(0.7f, 0.7f, 0.76f), 3f * u);
                    float knob = 16f * u;
                    GuiKit.Rounded(new Rect(barRect.x + barRect.width * t - knob * 0.5f, barRect.center.y - knob * 0.5f, knob, knob),
                        selected ? Color.white : new Color(0.8f, 0.8f, 0.84f), knob * 0.5f);

                    Rect grab = new Rect(barRect.x - 8f * u, rect.y, barRect.width + 16f * u, rect.height);
                    if (interactive && e.type == EventType.MouseDown && e.button == 0 && grab.Contains(mouse)) _dragging = index;
                    if (_dragging == index && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
                    {
                        item.Set01((mouse.x - barRect.x) / barRect.width);
                        e.Use();
                    }

                    GuiKit.ShadowLabel(new Rect(rect.xMax - 104f * u, rect.y, 84f * u, rect.height), item.Value(), value,
                        selected ? Ink : Dim, 0.4f);
                }
                else if (item.Value != null)
                {
                    // Choix : fleches cliquables de part et d'autre de la valeur, dans une pastille.
                    Rect valueRect = new Rect(rect.xMax - 340f * u, rect.y + 6f * u, 320f * u, rect.height - 12f * u);
                    GuiKit.Rounded(valueRect, new Color(0f, 0f, 0f, 0.28f), valueRect.height * 0.5f);
                    GuiKit.ShadowLabel(valueRect, item.Value(), centred, selected ? Ink : Dim, 0.4f);

                    Rect leftArrow = new Rect(valueRect.x, valueRect.y, 40f * u, valueRect.height);
                    Rect rightArrow = new Rect(valueRect.xMax - 40f * u, valueRect.y, 40f * u, valueRect.height);
                    GuiKit.ShadowLabel(leftArrow, "‹", arrow, selected ? Accent : Dim, 0.3f);
                    GuiKit.ShadowLabel(rightArrow, "›", arrow, selected ? Accent : Dim, 0.3f);

                    if (interactive && e.type == EventType.MouseDown && e.button == 0)
                    {
                        if (leftArrow.Contains(mouse)) { item.Step(-1); Play(_tick, 1f); e.Use(); }
                        else if (rightArrow.Contains(mouse) || rect.Contains(mouse)) { item.Step(1); Play(_tick, 1f); e.Use(); }
                    }
                }
                else if (e.type == EventType.MouseDown && e.button == 0 && hover && item.Activate != null && _pageTime > 0.12f)
                {
                    Play(_confirm, 0.7f);
                    e.Use();
                    GuiKit.Alpha = baseAlpha;
                    item.Activate();
                    return;
                }
            }

            GuiKit.Alpha = baseAlpha * (1f - leave);

            // Plus de lignes que de place : une barre de defilement fine, a droite.
            if (_items.Count > visible)
            {
                float trackHeight = visible * step - gap;
                Rect track = new Rect(area.xMax + 12f * u, area.y, 4f * u, trackHeight);
                GuiKit.Rounded(track, new Color(1f, 1f, 1f, 0.08f), 2f * u);
                float thumb = trackHeight * visible / _items.Count;
                float at = (trackHeight - thumb) * (_scroll / Mathf.Max(1f, _items.Count - visible));
                GuiKit.Rounded(new Rect(track.x, track.y + at, track.width, thumb), new Color(1f, 1f, 1f, 0.35f), 2f * u);
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
                GUIStyle hint = GuiKit.Text(Mathf.RoundToInt(17f * u), GuiKit.Weight.Medium,
                    pills ? TextAnchor.UpperCenter : TextAnchor.UpperLeft, true);
                float width = pills ? Mathf.Max(area.width, 720f * u) : area.width;
                float x = pills ? area.center.x - width * 0.5f : area.x + 8f * u;
                GuiKit.ShadowLabel(new Rect(x, listBottom + 18f * u, width, 50f * u), _items[_selected].Hint, hint,
                    new Color(0.86f, 0.87f, 0.91f), 0.7f);
            }
        }

        private void DrawControls(Rect area, float u)
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

            int count = rows.GetLength(0);
            int perColumn = Mathf.CeilToInt(count / 2f);
            float columnGap = 28f * u;
            float columnWidth = (area.width - columnGap) * 0.5f;
            float rowHeight = Mathf.Min(40f * u, area.height / perColumn);

            GUIStyle action = GuiKit.Text(Mathf.RoundToInt(17f * u), GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            GUIStyle key = GuiKit.Text(Mathf.RoundToInt(15f * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter);

            float baseAlpha = GuiKit.Alpha;
            float leave = _leaving ? Ease(Mathf.Clamp01(_leaveTime / LeaveDuration)) : 0f;

            for (int i = 0; i < count; i++)
            {
                int column = i / perColumn;
                int row = i % perColumn;

                // Les deux colonnes se remplissent en cascade.
                float appear = Ease(Mathf.Clamp01((_pageTime - (row + column * 0.5f) * 0.03f) / 0.22f));
                GuiKit.Alpha = baseAlpha * appear * (1f - leave);

                Rect rect = new Rect(area.x + column * (columnWidth + columnGap), area.y + row * rowHeight + (1f - appear) * 12f * u,
                    columnWidth, rowHeight - 5f * u);

                GuiKit.Rounded(rect, new Color(1f, 1f, 1f, row % 2 == 0 ? 0.045f : 0.025f), 8f * u);
                GuiKit.ShadowLabel(new Rect(rect.x + 14f * u, rect.y, rect.width * 0.6f, rect.height), rows[i, 0], action, Dim, 0.4f);

                // La touche, dans une pastille façon touche de clavier.
                string text = rows[i, 1].ToUpperInvariant();
                _glyph.text = text;
                float keyWidth = Mathf.Min(rect.width * 0.45f, key.CalcSize(_glyph).x + 22f * u);
                Rect cap = new Rect(rect.xMax - keyWidth - 10f * u, rect.y + 5f * u, keyWidth, rect.height - 10f * u);
                GuiKit.Rounded(cap, new Color(1f, 1f, 1f, 0.1f), 6f * u);
                GuiKit.RoundedOutline(cap, new Color(1f, 1f, 1f, 0.16f), 6f * u, 1f);
                GuiKit.ShadowLabel(cap, text, key, Ink, 0.3f);
            }

            GuiKit.Alpha = baseAlpha;
        }

        /// <summary>En bas au centre : les touches du menu, dans des pastilles.</summary>
        private void DrawFooter(float u, float sw, float sh)
        {
            string[] keys = _page == Page.Graphics
                ? new[] { "↑ ↓", "choisir", "← →", "régler", "ÉCHAP", "retour" }
                : _page == Page.Title
                    ? new[] { "↑ ↓", "choisir", "ENTRÉE", "valider" }
                    : new[] { "↑ ↓", "choisir", "ENTRÉE", "valider", "ÉCHAP", "retour" };

            GUIStyle key = GuiKit.Text(Mathf.RoundToInt(14f * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle word = GuiKit.Text(Mathf.RoundToInt(15f * u), GuiKit.Weight.Medium, TextAnchor.MiddleLeft);

            float total = 0f;
            float[] widths = new float[keys.Length];
            for (int i = 0; i < keys.Length; i++)
            {
                _glyph.text = keys[i];
                widths[i] = (i % 2 == 0 ? key.CalcSize(_glyph).x + 20f * u : word.CalcSize(_glyph).x) + (i % 2 == 0 ? 10f * u : 30f * u);
                total += widths[i];
            }

            float x = (sw - total) * 0.5f;
            float y = sh - 52f * u;
            for (int i = 0; i < keys.Length; i++)
            {
                if (i % 2 == 0)
                {
                    Rect cap = new Rect(x, y, widths[i] - 10f * u, 28f * u);
                    GuiKit.Rounded(cap, new Color(1f, 1f, 1f, 0.12f), 6f * u);
                    GuiKit.RoundedOutline(cap, new Color(1f, 1f, 1f, 0.2f), 6f * u, 1f);
                    GuiKit.ShadowLabel(cap, keys[i], key, Ink, 0.3f);
                }
                else
                {
                    GuiKit.ShadowLabel(new Rect(x, y, widths[i], 28f * u), keys[i], word, new Color(1f, 1f, 1f, 0.7f), 0.6f);
                }

                x += widths[i];
            }
        }

        private void DrawFps()
        {
            GUIStyle style = GuiKit.Style(14, FontStyle.Bold, TextAnchor.UpperLeft);
            float fps = CameraDiagnostics.Fps;
            string text = fps > 0f ? Mathf.RoundToInt(fps) + " IPS" : "— IPS";
            Color color = fps >= 55f || fps <= 0f ? new Color(0.6f, 1f, 0.6f) : fps >= 30f ? new Color(1f, 0.85f, 0.4f) : new Color(1f, 0.4f, 0.4f);

            GuiKit.OutlinedLabel(new Rect(12f, 8f, 200f, 22f), text, style, color, new Color(0f, 0f, 0f, 0.9f), 1f);
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
        /// La musique de l'écran titre, composée ici même : un groove de nuit à 92 BPM. Grosse
        /// caisse, caisse claire sur les temps 2 et 4, charleston en croches balancées, basse
        /// syncopée sur la fondamentale, et une nappe de synthé désaccordée sur quatre accords
        /// (la mineur, fa, do, sol), deux mesures chacun. La boucle retombe sur ses pieds : le
        /// motif tient dans une mesure et la nappe fond aux changements d'accord.
        /// </summary>
        private static AudioClip Music()
        {
            const float bpm = 92f;
            float beat = 60f / bpm;
            float bar = beat * 4f;
            float step = beat / 4f;

            float[][] chords =
            {
                new[] { 110f, 130.81f, 164.81f, 220f },
                new[] { 87.31f, 130.81f, 174.61f, 220f },
                new[] { 98f, 130.81f, 164.81f, 196f },
                new[] { 98f, 123.47f, 146.83f, 196f },
            };

            int bars = chords.Length * 2;
            int length = Mathf.RoundToInt(bar * bars * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(21);

            int[] kicks = { 0, 7, 10 };
            int[] snares = { 4, 12 };
            int[] basses = { 0, 3, 6, 10, 14 };

            float low = 0f;
            float hatLow = 0f;
            float snareLow = 0f;
            float peak = 0.0001f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                int barIndex = Mathf.Min(bars - 1, (int)(t / bar));
                float inBar = t - barIndex * bar;
                int chordIndex = barIndex / 2;
                float[] notes = chords[chordIndex];

                // --- nappe : fondu aux changements d'accord (toutes les deux mesures)
                float inChord = t - chordIndex * bar * 2f;
                float fade = Mathf.Clamp01(inChord / 0.5f) * Mathf.Clamp01((bar * 2f - inChord) / 0.5f);

                float pad = 0f;
                for (int i = 0; i < notes.Length; i++)
                {
                    pad += Saw(notes[i] * t) * 0.5f + Saw(notes[i] * 1.005f * t + 0.3f) * 0.5f;
                }

                pad /= notes.Length;
                float cutoff = 0.035f + 0.03f * (0.5f + 0.5f * Mathf.Sin(t * 0.5f));
                low += (pad - low) * cutoff;

                // --- batterie
                float noise = (float)random.NextDouble() * 2f - 1f;

                float tk = Since(inBar, kicks, step);
                float kickPhase = 48f * tk + 110f * 0.035f * (1f - Mathf.Exp(-tk / 0.035f));
                float kick = Mathf.Sin(2f * Mathf.PI * kickPhase) * Mathf.Exp(-tk / 0.2f) * Mathf.Clamp01(tk / 0.002f);

                float ts = Since(inBar, snares, step);
                snareLow += (noise - snareLow) * 0.5f;
                float snare = (noise - snareLow * 0.6f) * Mathf.Exp(-ts / 0.11f) * 0.8f +
                              Mathf.Sin(2f * Mathf.PI * 190f * ts) * Mathf.Exp(-ts / 0.05f) * 0.5f;

                // Charleston en croches, balancees (la deuxieme croche un peu en retard).
                float eighth = beat / 2f;
                int eighthIndex = (int)(inBar / eighth);
                float swing = eighthIndex % 2 == 1 ? eighth * 0.16f : 0f;
                float th = inBar - eighthIndex * eighth - swing;
                if (th < 0f) th += eighth;
                bool open = eighthIndex == 7;
                hatLow += (noise - hatLow) * 0.6f;
                float hat = (noise - hatLow) * Mathf.Exp(-th / (open ? 0.14f : 0.028f)) * (eighthIndex % 2 == 0 ? 0.5f : 0.32f);

                // --- basse : la fondamentale, deux octaves sous l'accord, pincee en syncope
                float tb = Since(inBar, basses, step);
                float root = notes[0] * 0.5f;
                float bass = (Mathf.Sin(2f * Mathf.PI * root * t) + Saw(root * t) * 0.25f) *
                             Mathf.Exp(-tb / 0.28f) * Mathf.Clamp01(tb / 0.004f);

                float sample = low * 1.1f * fade + bass * 0.34f + kick * 0.7f + snare * 0.26f + hat * 0.12f;
                data[n] = sample;
                peak = Mathf.Max(peak, Mathf.Abs(sample));
            }

            for (int n = 0; n < length; n++) data[n] = data[n] / peak * 0.85f;

            AudioClip clip = AudioClip.Create("Menu (musique)", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Temps écoulé depuis le dernier coup du motif (en doubles croches) dans la mesure.</summary>
        private static float Since(float inBar, int[] steps, float step)
        {
            float best = 10f;
            for (int i = 0; i < steps.Length; i++)
            {
                float at = steps[i] * step;
                if (at <= inBar) best = Mathf.Min(best, inBar - at);
            }

            return best;
        }

        /// <summary>
        /// Un souffle filtré dont la hauteur monte puis retombe. Court : la transition entre deux
        /// pages. Long, plus grave et avec un moteur : une voiture qui passe dans la rue.
        /// </summary>
        private static AudioClip Swoosh(string name, float seconds, float level, int seed)
        {
            bool car = seconds > 1f;
            int length = Mathf.RoundToInt(seconds * SampleRate);
            float[] data = new float[length];
            System.Random random = new System.Random(seed);

            float band = 0f;
            float low = 0f;
            float engine = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)length;
                float shape = Mathf.Sin(t * Mathf.PI);
                float noise = (float)random.NextDouble() * 2f - 1f;

                float cutoff = car ? Mathf.Lerp(0.02f, 0.09f, shape) : Mathf.Lerp(0.05f, 0.4f, shape);
                low += (noise - low) * cutoff;
                band += (low - band) * (car ? 0.01f : 0.03f);

                float sample = (low - band) * shape;

                if (car)
                {
                    // Le moteur, dont la hauteur baisse en passant (effet Doppler).
                    float pitch = Mathf.Lerp(62f, 44f, Mathf.SmoothStep(0f, 1f, t));
                    engine += pitch / SampleRate;
                    engine -= Mathf.Floor(engine);
                    sample += Saw(engine) * 0.12f * shape * shape;
                }

                data[n] = sample * level;
            }

            AudioClip clip = AudioClip.Create(name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static float Ease(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Saw(float phase)
        {
            return 2f * (phase - Mathf.Floor(phase + 0.5f));
        }
    }
}
