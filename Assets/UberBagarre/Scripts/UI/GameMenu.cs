using System;
using System.Collections.Generic;
using UberBagarre.Core;
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
    /// **L'écran titre** montre le jeu lui-même : la caméra de cinéma glisse lentement au-dessus
    /// de la ville, sous la pluie, avec le néon du titre qui s'allume lettre par lettre. Un jeu
    /// qui s'ouvre sur son propre décor dit tout de suite ce qu'il est.
    ///
    /// **La pause** fige vraiment tout : le temps du jeu, le son, et les systèmes qui comptent
    /// en temps réel (dialogues, cinématiques, téléphone) consultent <see cref="IsPaused"/>. À
    /// gauche, ce qu'on peut faire ; à droite, où on en est (le jour, l'heure, l'argent, la
    /// dette, le niveau, la réputation, l'objectif, la faim et les blessures) — comme dans les
    /// jeux où l'on ouvre la pause pour se rappeler sa situation.
    ///
    /// **Les réglages** (<see cref="SettingsPage"/>) : sept onglets, tout ce qu'un jeu fini
    /// propose — difficulté, caméra, souris, interface, affichage, qualité, image, volumes par
    /// famille de sons, touches réassignables, sous-titres et accessibilité.
    ///
    /// Tout se pilote au clavier (flèches, Entrée, Échap ou Retour arrière) ou à la souris
    /// (survol, clic, molette, glisser sur une barre).
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
            Settings
        }

        private class Item
        {
            public string Label;
            public string Hint;
            public Action Activate;
            public bool Separator;
        }

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

        // Le néon du titre garde sa couleur : c'est l'enseigne du jeu, pas l'interface.
        private static readonly Color Neon = new Color(1f, 0.2f, 0.55f);

        private readonly List<Item> _items = new List<Item>(24);
        private Page _page = Page.None;
        private Page _home = Page.None;
        private int _selected;
        private float _open;
        private float _shotTime;
        private float _previousShotFov = 60f;
        private float _pageTime;
        private bool _starting;
        private float _startTimer;
        private string _startBeat;

        private SettingsPage _settings;
        private int _settingsTab;

        // Ce que la pause raconte de la partie (cherché à l'ouverture).
        private PlayerProgress _progress;
        private ObjectiveDisplay _objective;
        private World.CityMap _map;

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
            // Le volume général est appliqué par les réglages (SettingsApplier), plus ici.
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

            _settings = new SettingsPage(_graphics, _look, _input, PlaySettingsSound, Back);
        }

        private void OnEnable()
        {
            GameSettings.Changed += OnSettingChanged;
        }

        private void Start()
        {
            ApplyFov();

            if (_titleOnStart && (_prologue != null || _openWorld != null)) Open(Page.Title);
        }

        private void OnDisable()
        {
            GameSettings.Changed -= OnSettingChanged;
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

        private void OnSettingChanged(string key)
        {
            if (key == "fov") ApplyFov();
        }

        private void ApplyFov()
        {
            if (_gameCamera != null) _gameCamera.fieldOfView = GameSettings.FieldOfView;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _open = Mathf.MoveTowards(_open, _page != Page.None && !_starting ? 1f : 0f, dt / 0.2f);
            _pageTime += dt;

            float music = _page == Page.Title || (_home == Page.Title && _page != Page.None) ? _musicVolume : 0f;
            if (_starting) music = 0f;
            music *= GameSettings.Volume(AudioChannel.Music);
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

            HandleKeys(dt);
        }

        private bool CanPause()
        {
            // Un écran plein (armoire, ordinateur, comptoir) : Échap le referme, lui.
            if (FullScreenPanel.AnyOpen) return false;

            // Le crochetage d'une portière : Échap l'abandonne.
            if (World.CarThief.Picking) return false;

            // La grande carte ouverte : Échap la referme.
            if (_map == null) _map = FindAnyObjectByType<World.CityMap>();
            if (_map != null && _map.FullOpen) return false;

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

                if (_progress == null) _progress = FindAnyObjectByType<PlayerProgress>();
                if (_objective == null) _objective = FindAnyObjectByType<ObjectiveDisplay>();
                if (_map == null) _map = FindAnyObjectByType<World.CityMap>();
            }
            else
            {
                BeginShot();
            }

            Show(page);
        }

        private void CloseAll()
        {
            if (_page == Page.Settings || _nextPage == Page.Settings) _settings.Close();

            if (_home == Page.Pause)
            {
                IsPaused = false;
                Time.timeScale = Feedback.HitStop.BaseTimeScale;
                AudioListener.pause = false;
            }

            if (_home == Page.Title) EndShot();

            _page = Page.None;
            _home = Page.None;
            _leaving = false;

            IsOpen = false;
            ModalScreen.Set(this, false);
            if (_input != null) _input.SetGameplayLock(this, false);

            if (_cursor != null)
            {
                _cursor.enabled = true;
                _cursor.SetLocked(true);
            }

            GameSettings.Save();
        }

        /// <summary>Change de page : l'ancienne s'efface, la nouvelle arrive.</summary>
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
            Play(_whoosh, 0.7f);
        }

        private void ShowNow(Page page)
        {
            if (_page == Page.Settings && page != Page.Settings) _settings.Close();

            _page = page;
            _pageTime = 0f;
            BuildItems(page);

            if (page == Page.Settings) _settings.Open(_settingsTab);

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

        private void OpenSettings(int tab)
        {
            _settingsTab = tab;
            Go(Page.Settings);
        }

        private void PlaySettingsSound(SettingsPage.Sound sound)
        {
            switch (sound)
            {
                case SettingsPage.Sound.Confirm: Play(_confirm, 0.6f); break;
                case SettingsPage.Sound.Page: Play(_whoosh, 0.45f); break;
                default: Play(_tick, 0.8f); break;
            }
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
            float zoomTarget = shown == Page.Chapters ? 0.45f : shown == Page.Settings ? 0.6f : 0f;
            _zoom = Mathf.SmoothDamp(_zoom, zoomTarget, ref _zoomVelocity, 0.55f, Mathf.Infinity, Mathf.Max(0.0001f, dt));

            Vector3 offset = position - target;
            float swing = shown == Page.Settings ? -18f : shown == Page.Chapters ? 12f : 0f;
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
            const string settingsHint = "Jeu, interface, graphismes, image, son, commandes et accessibilité.";

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

                    Add("RÉGLAGES", settingsHint, delegate { OpenSettings(SettingsPage.TabGame); });
                    Add("QUITTER", null, Quit);
                    break;

                case Page.Pause:
                    Add("REPRENDRE", null, delegate { Play(_confirm, 0.7f); CloseAll(); });
                    if (_map != null && _openWorld != null)
                    {
                        Add("CARTE", "La ville en grand : les lieux, leurs horaires, et un point à placer pour le GPS.", OpenMap);
                    }

                    Add("RÉGLAGES", settingsHint, delegate { OpenSettings(SettingsPage.TabGame); });
                    Add("COMMANDES", "Toutes les touches, et les changer.", delegate { OpenSettings(SettingsPage.TabControls); });
                    if (story) Add("CHAPITRES", "Rejouer un chapitre depuis son début.", delegate { Go(Page.Chapters); });

                    if (_openWorld != null)
                    {
                        Add("MENU PRINCIPAL", "Revenir à l'écran titre. Ce qui compte est gardé à la dernière nuit passée au lit.", BackToTitle);
                    }
                    else if (story || Application.CanStreamedLevelBeLoaded(_storyScene))
                    {
                        Add("MENU PRINCIPAL", story ? "Revenir à l'écran titre (la partie en cours est perdue)." : "Quitter le bac à sable.",
                            BackToTitle);
                    }

                    Add("QUITTER LE JEU", _openWorld != null ? "La partie est gardée à la dernière nuit passée au lit." : null, Quit);
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
            }
        }

        /// <summary>L'écran titre de la ville : reprendre, recommencer, régler, quitter.</summary>
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

            Add("RÉGLAGES", "Jeu, interface, graphismes, image, son, commandes et accessibilité.",
                delegate { OpenSettings(SettingsPage.TabGame); });
            Add("QUITTER", null, Quit);
        }

        private void OpenMap()
        {
            Play(_confirm, 0.7f);
            World.CityMap map = _map;
            CloseAll();
            if (map != null) map.OpenFull();
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

        private void Separator()
        {
            _items.Add(new Item { Separator = true });
        }

        private static int Wrap(int value, int count)
        {
            return ((value % count) + count) % count;
        }

        // --------------------------------------------------------------- clavier

        private void HandleKeys(float dt)
        {
            if (_page == Page.Settings)
            {
                _settings.Tick(dt, _input != null ? _input.Provider : null, _input != null ? _input.Bindings : null);
                return;
            }

            if (_input == null || _input.Provider == null || _input.Bindings == null) return;

            var provider = _input.Provider;
            var bindings = _input.Bindings;


            if (_pageTime < 0.12f) return;

            if (provider.GetPressedThisFrame(bindings.phoneUp) || provider.GetPressedThisFrame(bindings.moveForward)) Move(-1);
            if (provider.GetPressedThisFrame(bindings.phoneDown) || provider.GetPressedThisFrame(bindings.moveBackward)) Move(1);

            Item current = _selected >= 0 && _selected < _items.Count ? _items[_selected] : null;

            if (current != null && current.Activate != null &&
                (provider.GetPressedThisFrame(bindings.phoneSelect) || _input.InteractPressed || provider.GetPressedThisFrame(bindings.jump)))
            {
                Play(_confirm, 0.7f);
                current.Activate();
                return;
            }

            if (_input.ReleaseCursorPressed || provider.GetPressedThisFrame(bindings.phoneBack)) Back();
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
            if (GameSettings.ShowFps && _page == Page.None) DrawFps();
            if (_open <= 0.001f) return;

            GUI.depth = -40;

            float sw = Screen.width;
            float sh = Screen.height;
            float u = UiTheme.Unit;

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = _open;

            bool title = _home == Page.Title;
            Page shown = _page;
            float leave = _leaving ? UiTheme.EaseOut(_leaveTime / LeaveDuration) : 0f;

            // Fond : sur l'écran titre, le survol de la ville, la pluie, une voiture qui passe de
            // temps en temps, et un voile sombre en haut et en bas ; en pause, l'image figée,
            // assombrie à gauche (là où est le menu) et laissée visible à droite.
            if (title)
            {
                DrawRain(sw, sh, u);
                DrawPassingCar(sw, sh);
                if (shown != Page.Settings) DrawVignette(sw, sh);

                float black = TourBlack;
                if (_openWorld != null && !World.MapStreamer.Ready && _titleTime < 30f) black = 1f;
                if (black > 0.001f) GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, black));

                // Les bandes de cinéma, qui s'ouvrent à l'arrivée de l'écran titre.
                float bars = Mathf.Lerp(0.5f, 0.055f, Ease(Mathf.Clamp01(_titleTime / 1.2f)));
                GuiKit.Fill(new Rect(0f, 0f, sw, sh * bars), new Color(0f, 0f, 0f, 1f));
                GuiKit.Fill(new Rect(0f, sh * (1f - bars), sw, sh * bars), new Color(0f, 0f, 0f, 1f));
            }
            else if (shown != Page.Settings)
            {
                const int bands = 20;
                for (int i = 0; i < bands; i++)
                {
                    float t = i / (float)(bands - 1);
                    float a = Mathf.Lerp(0.82f, 0.38f, Mathf.SmoothStep(0f, 1f, t));
                    GuiKit.Fill(new Rect(sw * i / bands, 0f, sw / bands + 1f, sh), new Color(0.01f, 0.012f, 0.018f, a));
                }
            }

            GuiKit.Alpha = _open * (1f - leave);

            switch (shown)
            {
                case Page.Title:
                    float glowPulse = 0.85f + Mathf.Sin(Time.unscaledTime * 2.1f) * 0.08f + Mathf.Sin(Time.unscaledTime * 13f) * 0.02f;
                    DrawNeonLogo(sw * 0.5f, sh * 0.13f, u, glowPulse);
                    DrawTitleItems(sw, sh, u);
                    break;

                case Page.Pause:
                    DrawPause(sw, sh, u);
                    break;

                case Page.Chapters:
                    DrawChapters(sw, sh, u);
                    break;

                case Page.Settings:
                    _settings.Draw(title ? "ÜBER BAGARRE" : "PAUSE");
                    break;
            }

            if (shown != Page.Settings) DrawFooter(u, sw, sh);

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
                new Color(Neon.r, Neon.g, Neon.b, 0.26f * glowPulse * ignition));

            bool glitch = _titleTime > 3f && (_titleTime % 8f) > 7.82f;
            if (glitch)
            {
                DrawLetters(huge, left - 5f * u, top, u, new Color(1f, 0.15f, 0.2f, 0.55f), false);
                DrawLetters(huge, left + 5f * u, top + 2f * u, u, new Color(0.2f, 0.9f, 1f, 0.55f), false);
            }

            DrawLetters(huge, left, top, u, new Color(1f, 0.95f, 0.97f), true);

            float bar = Ease(Mathf.Clamp01((_titleTime - 1.3f) / 0.5f));
            GuiKit.Rounded(new Rect(centre - 90f * u * bar, top + 146f * u, 180f * u * bar, 5f * u), Neon, 2.5f * u);

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
            Color outline = new Color(Neon.r * 0.5f, 0f, Neon.b * 0.3f, 0.95f);

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

        // --------------------------------------------------------------- pages : titre, pause, chapitres

        /// <summary>Les boutons de l'écran titre, centrés sous le néon : le choisi est une pastille claire.</summary>
        private void DrawTitleItems(float sw, float sh, float u)
        {
            float width = 440f * u;
            float rowHeight = 52f * u;
            float gap = 8f * u;
            float x = (sw - width) * 0.5f;
            float top = sh * 0.42f;
            int count = _items.Count;

            GuiKit.Disc(new Rect(x - 260f * u, top - 120f * u, width + 520f * u, count * (rowHeight + gap) + 260f * u),
                new Color(0f, 0f, 0f, 0.45f));

            Event e = Event.current;
            GUIStyle label = UiTheme.Text(20f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            float baseAlpha = GuiKit.Alpha;
            float bottom = top;

            for (int i = 0; i < count; i++)
            {
                Item item = _items[i];
                float appear = UiTheme.EaseOut((_pageTime - 0.05f - i * 0.045f) / 0.3f);
                Rect rect = new Rect(x, top + i * (rowHeight + gap) + (1f - appear) * 16f * u, width, rowHeight);
                bottom = rect.yMax;
                if (item.Separator) continue;

                GuiKit.Alpha = baseAlpha * appear;
                bool hover = !_leaving && rect.Contains(e.mousePosition);
                if (hover && (UiTheme.MouseMoved || e.type == EventType.MouseDown) && _selected != i)
                {
                    _selected = i;
                    Play(_tick, 0.6f);
                }

                bool selected = i == _selected;
                float radius = rowHeight * 0.5f;
                if (selected)
                {
                    GuiKit.Glow(rect, new Color(1f, 1f, 1f, 0.1f), radius, 16f * u);
                    GuiKit.Rounded(rect, new Color(0.96f, 0.96f, 0.97f, 0.95f), radius);
                    UiTheme.Label(rect, item.Label, label, new Color(0.06f, 0.07f, 0.09f));
                }
                else
                {
                    if (hover) GuiKit.Rounded(rect, new Color(1f, 1f, 1f, 0.07f), radius);
                    GuiKit.ShadowLabel(rect, item.Label, label, new Color(0.9f, 0.9f, 0.92f, 0.86f), 0.6f);
                }

                if (hover && e.type == EventType.MouseDown && e.button == 0 && item.Activate != null && _pageTime > 0.12f)
                {
                    Play(_confirm, 0.7f);
                    e.Use();
                    GuiKit.Alpha = baseAlpha;
                    item.Activate();
                    return;
                }
            }

            GuiKit.Alpha = baseAlpha;
            DrawHint(new Rect(sw * 0.5f - Mathf.Max(width, 760f * u) * 0.5f, bottom + 22f * u, Mathf.Max(width, 760f * u), 60f * u),
                TextAnchor.UpperCenter);
        }

        /// <summary>
        /// La pause : à gauche le titre, le jour et l'heure, et ce qu'on peut faire ; à droite,
        /// où en est la partie.
        /// </summary>
        private void DrawPause(float sw, float sh, float u)
        {
            float margin = Mathf.Max(40f * u, sw * 0.055f);
            float appear = UiTheme.EaseOut(_pageTime / 0.3f);
            float top = sh * 0.17f + (1f - appear) * 14f * u;

            UiTheme.Label(new Rect(margin, top, 600f * u, 64f * u), "PAUSE", UiTheme.Title(56f), UiTheme.Ink);
            string when = DayLine();
            if (when.Length > 0)
            {
                UiTheme.Label(new Rect(margin, top + 62f * u, 600f * u, 24f * u), when, UiTheme.Body(17f), UiTheme.InkDim);
            }

            Rect list = new Rect(margin - 20f * u, top + 112f * u, 440f * u, sh - top - 220f * u);
            float bottom = DrawList(list, u, 54f * u, 23f);
            DrawHint(new Rect(margin, bottom + 16f * u, 420f * u, 70f * u), TextAnchor.UpperLeft);

            if (_progress != null && sw > 1100f * u) DrawStatus(new Rect(sw - margin - 430f * u, top + 6f * u, 430f * u, 0f), u);
        }

        private void DrawChapters(float sw, float sh, float u)
        {
            float width = Mathf.Min(sw - 80f * u, 760f * u);
            float height = Mathf.Min(sh * 0.8f, (150f + _items.Count * 58f + 90f) * u);
            float appear = UiTheme.EaseOut(_pageTime / 0.3f);
            Rect panel = new Rect((sw - width) * 0.5f, (sh - height) * 0.5f + (1f - appear) * 16f * u, width, height);
            UiTheme.DrawPanel(panel, 16f * u);

            UiTheme.Label(new Rect(panel.x + 36f * u, panel.y + 28f * u, width - 72f * u, 50f * u), "CHAPITRES", UiTheme.Title(38f), UiTheme.Ink);
            Rect list = new Rect(panel.x + 18f * u, panel.y + 100f * u, width - 36f * u, height - 190f * u);
            float bottom = DrawList(list, u, 52f * u, 19f);
            DrawHint(new Rect(panel.x + 36f * u, Mathf.Min(bottom + 12f * u, panel.yMax - 76f * u), width - 72f * u, 60f * u), TextAnchor.UpperLeft);
        }

        /// <summary>Une liste de lignes alignées à gauche (pause, chapitres). Rend le bas de la dernière ligne.</summary>
        private float DrawList(Rect area, float u, float rowHeight, float textSize)
        {
            Event e = Event.current;
            GUIStyle label = UiTheme.Text(textSize, GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            float baseAlpha = GuiKit.Alpha;
            float y = area.y;

            for (int i = 0; i < _items.Count; i++)
            {
                Item item = _items[i];
                if (item.Separator)
                {
                    GuiKit.Fill(new Rect(area.x + 20f * u, y + 9f * u, area.width - 40f * u, 1f), UiTheme.Line);
                    y += 18f * u;
                    continue;
                }

                float appear = UiTheme.EaseOut((_pageTime - 0.04f - i * 0.035f) / 0.26f);
                Rect rect = new Rect(area.x + (1f - appear) * -14f * u, y, area.width, rowHeight);
                y += rowHeight + 4f * u;
                if (rect.yMax > area.yMax + rowHeight) continue;

                GuiKit.Alpha = baseAlpha * appear;
                bool hover = !_leaving && rect.Contains(e.mousePosition);
                if (hover && (UiTheme.MouseMoved || e.type == EventType.MouseDown) && _selected != i)
                {
                    _selected = i;
                    Play(_tick, 0.6f);
                }

                bool selected = i == _selected;
                if (selected)
                {
                    GuiKit.Rounded(rect, new Color(1f, 1f, 1f, 0.08f), 8f * u);
                    GuiKit.Rounded(new Rect(rect.x, rect.y + 12f * u, 4f * u, rect.height - 24f * u), UiTheme.Accent, 2f * u);
                }
                else if (hover)
                {
                    GuiKit.Rounded(rect, new Color(1f, 1f, 1f, 0.04f), 8f * u);
                }

                UiTheme.Label(new Rect(rect.x + 20f * u, rect.y, rect.width - 30f * u, rect.height), item.Label, label,
                    selected ? UiTheme.Ink : hover ? new Color(0.85f, 0.86f, 0.89f) : UiTheme.InkDim);

                if (hover && e.type == EventType.MouseDown && e.button == 0 && item.Activate != null && _pageTime > 0.12f)
                {
                    Play(_confirm, 0.7f);
                    e.Use();
                    GuiKit.Alpha = baseAlpha;
                    item.Activate();
                    return y;
                }
            }

            GuiKit.Alpha = baseAlpha;
            return y;
        }

        /// <summary>L'explication de la ligne choisie.</summary>
        private void DrawHint(Rect rect, TextAnchor anchor)
        {
            if (_selected < 0 || _selected >= _items.Count || string.IsNullOrEmpty(_items[_selected].Hint)) return;
            GUIStyle hint = GuiKit.Text(UiTheme.Size(16.5f), GuiKit.Weight.Regular, anchor, true);
            GuiKit.ShadowLabel(rect, _items[_selected].Hint, hint, new Color(0.8f, 0.82f, 0.86f), 0.6f);
        }

        /// <summary>« Jour 4  ·  Mardi 14:32 ».</summary>
        private string DayLine()
        {
            string line = _progress != null ? "Jour " + _progress.Day : "";
            World.WorldClock clock = World.WorldClock.Instance;
            if (clock != null && _progress != null)
            {
                string day = World.ShopHours.DayName(World.ShopHours.Weekday(_progress.Day));
                if (day.Length > 0) day = char.ToUpperInvariant(day[0]) + day.Substring(1);
                line += "  ·  " + day + "  " + clock.Label;
            }
            else if (clock != null)
            {
                line = clock.Label;
            }

            return line;
        }

        /// <summary>Le panneau de droite de la pause : argent, niveau, réputation, objectif, état.</summary>
        private void DrawStatus(Rect r, float u)
        {
            PlayerProgress p = _progress;
            float pad = 26f * u;
            float width = r.width - pad * 2f;
            GUIStyle caption = UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            GUIStyle body = UiTheme.Body(16f);
            GUIStyle wrap = GuiKit.Text(UiTheme.Size(16f), GuiKit.Weight.Regular, TextAnchor.UpperLeft, true);

            string objective = _objective != null ? _objective.Current : null;
            float objectiveHeight = 0f;
            if (!string.IsNullOrEmpty(objective))
            {
                _glyph.text = objective;
                objectiveHeight = wrap.CalcHeight(_glyph, width) + 30f * u + 26f * u;
            }

            float height = pad + 96f * u + 22f * u + 64f * u + 22f * u + 56f * u + 22f * u + objectiveHeight + 88f * u + pad;
            r.height = height;
            UiTheme.DrawPanel(r, 14f * u);

            float x = r.x + pad;
            float y = r.y + pad;

            // --- argent et dette
            UiTheme.Label(new Rect(x, y, width, 16f * u), "ARGENT", caption, UiTheme.InkFaint);
            UiTheme.Label(new Rect(x, y + 18f * u, width, 44f * u), Euros(p.Money), UiTheme.Title(34f), UiTheme.Ink);
            int debt = p.Debt;
            UiTheme.Label(new Rect(x, y + 66f * u, width, 22f * u), debt > 0 ? "Dette : " + Euros(debt) : "Aucune dette", body,
                debt > 0 ? new Color(0.95f, 0.55f, 0.5f) : UiTheme.Good);
            y += 96f * u;
            Divider(x, ref y, width, u);

            // --- niveau
            UiTheme.Label(new Rect(x, y, width, 16f * u), "NIVEAU", caption, UiTheme.InkFaint);
            UiTheme.Label(new Rect(x, y + 16f * u, width, 30f * u), p.Level.ToString(), UiTheme.Heading(24f), UiTheme.Ink);
            UiTheme.Label(new Rect(x, y + 16f * u, width, 30f * u), p.Experience + " / " + p.NextThreshold + " XP",
                UiTheme.Text(14f, GuiKit.Weight.Medium, TextAnchor.MiddleRight), UiTheme.InkDim);
            Rect bar = new Rect(x, y + 52f * u, width, 4f * u);
            GuiKit.Rounded(bar, new Color(1f, 1f, 1f, 0.1f), 2f * u);
            GuiKit.Rounded(new Rect(bar.x, bar.y, Mathf.Max(bar.height, bar.width * p.LevelProgress), bar.height), UiTheme.Accent, 2f * u);
            y += 64f * u;
            Divider(x, ref y, width, u);

            // --- réputation : le titre, et la note moyenne des clients en cinq pastilles
            UiTheme.Label(new Rect(x, y, width, 16f * u), "RÉPUTATION", caption, UiTheme.InkFaint);
            UiTheme.Label(new Rect(x, y + 18f * u, width, 28f * u), p.ReputationTitle, UiTheme.Heading(20f), UiTheme.Ink);
            float rating = p.Rating;
            float d = 11f * u;
            for (int i = 0; i < 5; i++)
            {
                Rect dot = new Rect(r.xMax - pad - (5 - i) * (d + 5f * u) + 5f * u, y + 26f * u, d, d);
                float fill = Mathf.Clamp01(rating - i);
                GuiKit.Rounded(dot, new Color(1f, 1f, 1f, 0.14f), d * 0.5f);
                if (fill > 0.05f) GuiKit.Rounded(new Rect(dot.x, dot.y, dot.width * fill, dot.height), UiTheme.Accent, d * 0.5f);
            }

            y += 56f * u;
            Divider(x, ref y, width, u);

            // --- objectif
            if (!string.IsNullOrEmpty(objective))
            {
                UiTheme.Label(new Rect(x, y, width, 16f * u), "OBJECTIF", caption, UiTheme.InkFaint);
                float h = objectiveHeight - 56f * u;
                UiTheme.Label(new Rect(x, y + 22f * u, width, h + 4f * u), objective, wrap, UiTheme.Ink);
                y += objectiveHeight - 22f * u;
                Divider(x, ref y, width, u);
            }

            // --- état : faim, blessures, trousses de soin
            UiTheme.Label(new Rect(x, y, width, 16f * u), "ÉTAT", caption, UiTheme.InkFaint);
            float satiety = Mathf.Clamp01(p.Satiety / PlayerProgress.MaxSatiety);
            string hunger = satiety > 0.6f ? "Rassasié" : satiety > 0.25f ? "Un petit creux" : "Affamé";
            Color hungerColor = satiety > 0.6f ? UiTheme.Good : satiety > 0.25f ? UiTheme.Accent : UiTheme.Bad;
            UiTheme.Label(new Rect(x, y + 20f * u, width * 0.5f, 24f * u), hunger, body, UiTheme.Ink);
            Rect food = new Rect(x, y + 48f * u, width * 0.42f, 4f * u);
            GuiKit.Rounded(food, new Color(1f, 1f, 1f, 0.1f), 2f * u);
            GuiKit.Rounded(new Rect(food.x, food.y, Mathf.Max(food.height, food.width * satiety), food.height), hungerColor, 2f * u);

            float rx = x + width * 0.55f;
            int injuries = p.Injuries;
            UiTheme.Label(new Rect(rx, y + 20f * u, width * 0.45f, 24f * u), injuries == 0 ? "Pas de blessure" : injuries == 1 ? "1 blessure" : injuries + " blessures",
                body, injuries > 0 ? new Color(0.95f, 0.55f, 0.5f) : UiTheme.Ink);
            for (int i = 0; i < PlayerProgress.MaxInjuries; i++)
            {
                Rect dot = new Rect(rx + i * (d + 5f * u), y + 46f * u, d, d);
                GuiKit.Rounded(dot, i < injuries ? UiTheme.Bad : new Color(1f, 1f, 1f, 0.14f), d * 0.5f);
            }

            UiTheme.Label(new Rect(x, y + 60f * u, width, 22f * u),
                p.Medkits == 0 ? "Aucune trousse de soin" : p.Medkits == 1 ? "1 trousse de soin" : p.Medkits + " trousses de soin",
                UiTheme.Text(14f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft), UiTheme.InkDim);
        }

        private static void Divider(float x, ref float y, float width, float u)
        {
            GuiKit.Fill(new Rect(x, y + 10f * u, width, 1f), UiTheme.Line);
            y += 22f * u;
        }

        /// <summary>« 12 400 € » (espaces entre les milliers, sans dépendre de la langue du système).</summary>
        private static string Euros(int value)
        {
            string digits = Mathf.Abs(value).ToString();
            System.Text.StringBuilder text = new System.Text.StringBuilder(digits.Length + 6);
            if (value < 0) text.Append('-');
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) text.Append(' ');
                text.Append(digits[i]);
            }

            return text.Append(" €").ToString();
        }

        /// <summary>En bas au centre : les touches du menu.</summary>
        private void DrawFooter(float u, float sw, float sh)
        {
            InputBindings b = _input != null ? _input.Bindings : null;
            string select = b != null ? UiTheme.KeyName(b.phoneSelect) : "Entrée";
            string back = b != null ? UiTheme.KeyName(b.releaseCursor) : "Échap";

            string[] keys = _page == Page.Title
                ? new[] { "↑ ↓", "Choisir", select, "Valider" }
                : new[] { "↑ ↓", "Choisir", select, "Valider", back, _page == Page.Pause ? "Reprendre" : "Retour" };

            GUIStyle keyStyle = UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle actionStyle = UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            float total = 0f;
            for (int i = 0; i < keys.Length; i += 2)
            {
                _glyph.text = keys[i];
                float cap = Mathf.Max(22f * u, keyStyle.CalcSize(_glyph).x + 12f * u);
                _glyph.text = keys[i + 1];
                total += cap + 7f * u + actionStyle.CalcSize(_glyph).x + 22f * u;
            }

            float x = _page == Page.Pause ? Mathf.Max(40f * u, sw * 0.055f) : (sw - total + 22f * u) * 0.5f;
            float y = sh - 54f * u;
            for (int i = 0; i < keys.Length; i += 2) x = UiTheme.KeyHint(x, y, keys[i], keys[i + 1], u);
        }

        /// <summary>Le compteur d'images, discret, dans le coin.</summary>
        private void DrawFps()
        {
            float u = UiTheme.Unit;
            float fps = CameraDiagnostics.Fps;
            string text = fps > 0f ? Mathf.RoundToInt(fps) + " IPS" : "— IPS";
            Color color = fps >= 55f || fps <= 0f ? UiTheme.Good : fps >= 30f ? UiTheme.Accent : UiTheme.Bad;
            GUIStyle style = UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            _glyph.text = text;
            float w = style.CalcSize(_glyph).x + 14f * u;
            Rect rect = new Rect(6f * u, 6f * u, w, 20f * u);
            GuiKit.Rounded(rect, new Color(0f, 0f, 0f, 0.45f), 4f * u);
            UiTheme.Label(rect, text, style, color);
        }

        // --------------------------------------------------------------- sons

        private AudioClip _lastClip;
        private float _lastClipTime;

        private void Play(AudioClip clip, float volume)
        {
            if (_ui == null || clip == null) return;

            // Un même son deux fois dans la même image (le clic, puis l'action qui le joue aussi) : une seule fois.
            if (clip == _lastClip && Time.unscaledTime - _lastClipTime < 0.05f) return;
            _lastClip = clip;
            _lastClipTime = Time.unscaledTime;
            _ui.PlayOneShot(clip, volume * _uiVolume * GameSettings.Volume(AudioChannel.Interface));
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
