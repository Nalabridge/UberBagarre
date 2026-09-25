using System.Collections.Generic;
using UberBagarre.Core;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.Phone
{
    /// <summary>
    /// Le système du téléphone : un écran d'accueil, des applis, et la navigation entre elles.
    ///
    /// Avant, le téléphone n'affichait que ce que l'histoire voulait, quand elle le voulait : un
    /// écran verrouillé le reste du temps. On le sortait, il n'y avait rien à faire, et on le
    /// rangeait — un accessoire, pas un téléphone. Maintenant il se sort QUAND ON VEUT (T) et
    /// il contient une vie : les messages de Sami, de la mère, de la banque ; le journal des
    /// appels manqués ; l'appareil photo et sa galerie ; le compte en banque qui plonge ; la
    /// carte du quartier ; les réglages (dont la luminosité — la nuit, un écran se baisse).
    ///
    /// L'histoire garde la main sur ce qui compte : quand elle affiche un écran (un appel, le
    /// lien de Sami, une course), le téléphone ouvre l'appli correspondante, exactement comme une
    /// notification qu'on touche. Le joueur peut en sortir et y revenir ; une validation ne
    /// compte que si l'écran de l'histoire est VRAIMENT affiché — ouvrir la galerie avec Entrée
    /// ne doit jamais accepter une course par accident.
    ///
    /// Commandes, téléphone sorti : flèches ou molette pour choisir, Entrée / clic gauche / E pour
    /// ouvrir, Retour arrière / clic droit pour revenir (sur l'accueil : ranger le téléphone).
    /// </summary>
    [RequireComponent(typeof(PhoneDevice))]
    public class PhoneOS : MonoBehaviour
    {
        public enum App
        {
            Accueil = 0,
            UberBagarre = 1,
            Messages = 2,
            Appels = 3,
            Photo = 4,
            Galerie = 5,
            Banque = 6,
            Carte = 7,
            Reglages = 8
        }

        public struct Message
        {
            public bool Mine;
            public string Text;
            public bool IsLink;
        }

        public struct Call
        {
            public string Name;
            public string Detail;
            public bool Missed;
        }

        private const string PrefsPrefix = "UberBagarre.Tel.";
        private const int SettingCount = 3;

        private static readonly float[] BrightnessLevels = { 0.3f, 0.5f, 0.75f, 1f };

        [Header("References")]
        [SerializeField] private PhoneDevice _device;
        [SerializeField] private PlayerInputReader _input;

        [SerializeField]
        [Tooltip("Optionnel : argent, courses, avis. Sans lui, les applis affichent un compte vierge.")]
        private PlayerProgress _progress;

        [SerializeField]
        [Tooltip("Optionnel : la carte montre ou l'on se trouve.")]
        private LocationDirector _locations;

        [Header("Son")]
        [SerializeField, Range(0f, 1f)] private float _uiVolume = 0.35f;
        [SerializeField, Range(0f, 1f)] private float _ringVolume = 0.55f;

        private App _app = App.Accueil;
        private int _homeSelection;
        private int _listSelection;
        private int _thread = -1;
        private int _threadScroll;
        private int _openPhoto = -1;
        private int _rdvTab;
        private int _settingSelection;

        private int _brightness = 1;
        private bool _nightMode = true;
        private bool _ringer = true;

        private PhoneDevice.Screen _lastStory = PhoneDevice.Screen.Verrouille;
        private PhoneDevice.Screen _lastRdv = PhoneDevice.Screen.Verrouille;
        private bool _linkSent;
        private bool _callReceived;
        private bool _callsSeen;
        private bool _rdvUnseen;
        private bool _wasRaised;
        private readonly int[] _seen = new int[5];

        private string _toast;
        private float _toastUntil;

        private readonly List<App> _apps = new List<App>(9);
        private readonly List<Message> _messages = new List<Message>(12);
        private readonly List<Message> _scratch = new List<Message>(12);
        private List<Message> _target;
        private readonly List<Call> _calls = new List<Call>(6);

        private AudioSource _uiSource;
        private AudioSource _ringSource;
        private AudioClip _tick;
        private AudioClip _open;
        private AudioClip _back;
        private AudioClip _ringtone;
        private AudioClip _buzz;

        // --------------------------------------------------------------- lecture

        public App Current { get { return _app; } }
        public int HomeSelection { get { return _homeSelection; } }
        public int ListSelection { get { return _listSelection; } }
        public int OpenThread { get { return _thread; } }
        public int ThreadScroll { get { return _threadScroll; } }
        public int OpenPhoto { get { return _openPhoto; } }
        public int RdvTab { get { return _rdvTab; } }
        public int SettingSelection { get { return _settingSelection; } }
        public PhoneDevice.Screen LastRdvScreen { get { return _lastRdv; } }
        public PlayerProgress Progress { get { return _progress; } }
        public float Brightness { get { return BrightnessLevels[Mathf.Clamp(_brightness, 0, BrightnessLevels.Length - 1)]; } }
        public int BrightnessLevel { get { return _brightness; } }
        public bool NightMode { get { return _nightMode; } }
        public bool Ringer { get { return _ringer; } }

        public string LocationName
        {
            get { return _locations != null ? _locations.CurrentName : string.Empty; }
        }

        public string Toast
        {
            get { return Time.unscaledTime < _toastUntil ? _toast : null; }
        }

        /// <summary>Les applis de l'écran d'accueil, dans l'ordre. ÜBER BAGARRE n'y est qu'une fois installée.</summary>
        public IList<App> Apps
        {
            get
            {
                _apps.Clear();
                if (_device == null || _device.AppInstalled) _apps.Add(App.UberBagarre);
                _apps.Add(App.Messages);
                _apps.Add(App.Appels);
                _apps.Add(App.Photo);
                _apps.Add(App.Galerie);
                _apps.Add(App.Banque);
                _apps.Add(App.Carte);
                _apps.Add(App.Reglages);
                return _apps;
            }
        }

        public static string AppName(App app)
        {
            switch (app)
            {
                case App.UberBagarre: return "Über Bagarre";
                case App.Messages: return "Messages";
                case App.Appels: return "Appels";
                case App.Photo: return "Photo";
                case App.Galerie: return "Galerie";
                case App.Banque: return "Banque";
                case App.Carte: return "Carte";
                case App.Reglages: return "Réglages";
                default: return "Accueil";
            }
        }

        public static Color AppColor(App app)
        {
            switch (app)
            {
                case App.UberBagarre: return new Color(0.92f, 0.16f, 0.52f);
                case App.Messages: return new Color(0.20f, 0.72f, 0.36f);
                case App.Appels: return new Color(0.16f, 0.62f, 0.30f);
                case App.Photo: return new Color(0.30f, 0.32f, 0.38f);
                case App.Galerie: return new Color(0.94f, 0.58f, 0.16f);
                case App.Banque: return new Color(0.18f, 0.36f, 0.78f);
                case App.Carte: return new Color(0.16f, 0.56f, 0.62f);
                case App.Reglages: return new Color(0.40f, 0.42f, 0.48f);
                default: return Color.gray;
            }
        }

        /// <summary>Pastille de notification d'une appli (0 = aucune).</summary>
        public int Badge(App app)
        {
            switch (app)
            {
                case App.Messages:
                    int unread = 0;
                    for (int i = 0; i < ThreadCount; i++)
                    {
                        if (Unread(i)) unread++;
                    }

                    return unread;

                case App.Appels: return _callsSeen ? 0 : 4;
                case App.UberBagarre: return _rdvUnseen ? 1 : 0;
                default: return 0;
            }
        }

        // --------------------------------------------------------------- messages

        public int ThreadCount { get { return 5; } }

        public static string ThreadName(int thread)
        {
            switch (thread)
            {
                case 0: return "SAMI";
                case 1: return "MAMAN";
                case 2: return "BANQUE";
                case 3: return "SFR";
                default: return "AGENCE";
            }
        }

        public static Color ThreadColor(int thread)
        {
            switch (thread)
            {
                case 0: return new Color(0.26f, 0.70f, 0.46f);
                case 1: return new Color(0.86f, 0.46f, 0.58f);
                case 2: return new Color(0.26f, 0.44f, 0.86f);
                case 3: return new Color(0.86f, 0.20f, 0.20f);
                default: return new Color(0.76f, 0.58f, 0.26f);
            }
        }

        public bool Unread(int thread)
        {
            Build(thread, _scratch);
            return _scratch.Count > _seen[Mathf.Clamp(thread, 0, _seen.Length - 1)];
        }

        /// <summary>
        /// Le fil d'une conversation, construit selon l'avancée de l'histoire. La liste renvoyée
        /// est réutilisée : la lire jusqu'au bout avant de redemander un autre fil.
        /// </summary>
        public IList<Message> Messages(int thread)
        {
            Build(thread, _messages);
            return _messages;
        }

        private void Build(int thread, List<Message> into)
        {
            _target = into;
            into.Clear();
            int contracts = _progress != null ? _progress.Contracts : 0;

            switch (thread)
            {
                case 0:
                    Them("t'es réveillé ?");
                    if (_linkSent || (_device != null && _device.AppInstalled && contracts > 0))
                    {
                        Them("tiens. tu m'as jamais demandé ça.");
                        into.Add(new Message { Text = "uberbagarre.apk", IsLink = true });
                    }

                    if (contracts >= 1)
                    {
                        Them("alors ?");
                        Me("150.");
                        Them("je t'avais dit. fais gaffe à toi quand même");
                    }

                    if (contracts >= 2)
                    {
                        Them("les frères Kovac ?? t'es malade");
                        Me("ils étaient deux. j'ai deux mains.");
                    }

                    if (contracts >= 3)
                    {
                        Them("le Taureau. t'as mis le TAUREAU par terre.");
                        Them("tout le quartier en parle, fais-toi discret");
                    }

                    break;

                case 1:
                    Them("tu manges bien ?");
                    Them("appelle moi quand tu peux mon grand");
                    Them("je t'ai fait un virement de 20€ ne dis rien à ton père");
                    break;

                case 2:
                    Them("Votre compte présente un solde débiteur de 1 240,18 EUR.");
                    Them("Frais de rejet de prélèvement : 20,00 EUR.");
                    Them("Merci de régulariser votre situation sous 8 jours.");
                    break;

                case 3:
                    Them("Facture impayée. 3e relance.");
                    Them("Sans règlement, votre ligne sera suspendue le 20/03.");
                    break;

                default:
                    Them("Bonjour, le loyer de février est toujours en attente.");
                    Them("Sans règlement sous 8 jours, nous engagerons une procédure.");
                    break;
            }
        }

        private void Them(string text)
        {
            _target.Add(new Message { Text = text });
        }

        private void Me(string text)
        {
            _target.Add(new Message { Mine = true, Text = text });
        }

        // --------------------------------------------------------------- appels

        public IList<Call> Calls
        {
            get
            {
                _calls.Clear();
                if (_callReceived) _calls.Add(new Call { Name = "SAMI", Detail = "entrant · 02:47" });
                _calls.Add(new Call { Name = "MAMAN", Detail = "manqué · hier 19:02", Missed = true });
                _calls.Add(new Call { Name = "BANQUE", Detail = "manqué (3) · hier", Missed = true });
                _calls.Add(new Call { Name = "INCONNU", Detail = "manqué · 23:12", Missed = true });
                _calls.Add(new Call { Name = "SFR", Detail = "manqué · lundi", Missed = true });
                return _calls;
            }
        }

        // --------------------------------------------------------------- cycle

        private void Awake()
        {
            if (_device == null) _device = GetComponent<PhoneDevice>();
            LoadSettings();

            _uiSource = gameObject.AddComponent<AudioSource>();
            _uiSource.playOnAwake = false;
            _uiSource.spatialBlend = 0f;

            _ringSource = gameObject.AddComponent<AudioSource>();
            _ringSource.playOnAwake = false;
            _ringSource.loop = true;
            _ringSource.spatialBlend = 0f;

            _tick = Tone("Tel tic", 2100f, 2100f, 0.012f, 0.5f);
            _open = Tone("Tel ouvre", 1300f, 2100f, 0.04f, 0.6f);
            _back = Tone("Tel retour", 1500f, 800f, 0.04f, 0.6f);
            _ringtone = Ringtone();
            _buzz = Buzz();
        }

        private void OnDestroy()
        {
            DestroyClip(_tick);
            DestroyClip(_open);
            DestroyClip(_back);
            DestroyClip(_ringtone);
            DestroyClip(_buzz);
        }

        private static void DestroyClip(AudioClip clip)
        {
            if (clip != null) Destroy(clip);
        }

        private void Update()
        {
            if (_device == null) return;

            SyncStory();
            UpdateRing();

            bool raised = _device.IsRaised;

            // Ressortir le telephone ramene a l'accueil, sauf si l'histoire attend quelque chose
            // de precis a l'ecran : on ne cache pas une course qui vient de tomber.
            if (raised && !_wasRaised && !IsStoryView() && _app != App.Photo) Open(App.Accueil, false);
            _wasRaised = raised;

            _device.CameraMode = _app == App.Photo;
            _device.ScreenBrightness = Brightness;

            if (_app == App.UberBagarre && raised) _rdvUnseen = false;
            if (_app == App.Appels && raised) _callsSeen = true;
            if (_app == App.Messages && _thread >= 0 && raised) _seen[_thread] = Messages(_thread).Count;

            if (raised && _input != null && _input.Provider != null && _input.Bindings != null && !_device.IsRinging)
            {
                HandleInput();
            }

            _device.ShowingStoryScreen = IsStoryView();
        }

        // --------------------------------------------------------------- histoire

        private void SyncStory()
        {
            PhoneDevice.Screen screen = _device.Current;

            if (_device.IsRinging && _app != App.Appels) Open(App.Appels, false);
            if (screen == _lastStory) return;

            _lastStory = screen;

            switch (screen)
            {
                case PhoneDevice.Screen.AppelEntrant:
                case PhoneDevice.Screen.EnAppel:
                    _callReceived = true;
                    Open(App.Appels, false);
                    break;

                case PhoneDevice.Screen.Lien:
                    _linkSent = true;
                    Open(App.Messages, false);
                    _thread = 0;
                    _threadScroll = 0;
                    break;

                case PhoneDevice.Screen.Installation:
                    _linkSent = true;
                    break;

                case PhoneDevice.Screen.Accueil:
                case PhoneDevice.Screen.Cible:
                case PhoneDevice.Screen.Mission:
                case PhoneDevice.Screen.Valide:
                case PhoneDevice.Screen.Profil:
                    _device.AppInstalled = true;
                    _lastRdv = screen;
                    _rdvTab = 0;
                    if (!_device.IsRaised || _app != App.UberBagarre) _rdvUnseen = true;
                    Open(App.UberBagarre, false);
                    break;

                case PhoneDevice.Screen.Photo:
                    Open(App.Photo, false);
                    break;

                default:
                    if (_app == App.Appels || _app == App.Photo) Open(App.Accueil, false);
                    break;
            }
        }

        /// <summary>L'écran affiché est-il celui que l'histoire attend ?</summary>
        public bool IsStoryView()
        {
            PhoneDevice.Screen screen = _device.Current;

            if (screen == PhoneDevice.Screen.Installation) return true;
            if (_device.IsRinging || screen == PhoneDevice.Screen.EnAppel) return _app == App.Appels;

            switch (screen)
            {
                case PhoneDevice.Screen.Lien:
                    return _app == App.Messages && _thread == 0;

                case PhoneDevice.Screen.Accueil:
                case PhoneDevice.Screen.Cible:
                case PhoneDevice.Screen.Mission:
                case PhoneDevice.Screen.Valide:
                case PhoneDevice.Screen.Profil:
                    return _app == App.UberBagarre && _rdvTab == 0;

                case PhoneDevice.Screen.Photo:
                    return _app == App.Photo;

                default:
                    return false;
            }
        }

        /// <summary>L'écran affiché attend-il une validation (installer, accepter) ?</summary>
        public bool AwaitingConfirm
        {
            get
            {
                PhoneDevice.Screen screen = _device.Current;
                return (screen == PhoneDevice.Screen.Lien || screen == PhoneDevice.Screen.Accueil) && IsStoryView();
            }
        }

        // --------------------------------------------------------------- navigation

        private void HandleInput()
        {
            InputBindings b = _input.Bindings;
            IInputProvider p = _input.Provider;

            float scroll = _input.ScrollDelta;
            bool camera = _app == App.Photo;

            bool up = p.GetPressedThisFrame(b.phoneUp) || (!camera && scroll > 0.5f);
            bool down = p.GetPressedThisFrame(b.phoneDown) || (!camera && scroll < -0.5f);
            bool left = p.GetPressedThisFrame(b.phoneLeft);
            bool right = p.GetPressedThisFrame(b.phoneRight);
            bool back = p.GetPressedThisFrame(b.phoneBack) || p.GetPressedThisFrame(b.attackHook);

            // Le clic gauche appartient au declencheur en mode photo ; E, lui, reste l'interaction.
            bool select = p.GetPressedThisFrame(b.phoneSelect) || _input.InteractPressed ||
                          (!camera && p.GetPressedThisFrame(b.attackStraight));

            if (back)
            {
                Back();
                return;
            }

            // Une validation de l'histoire passe AVANT la navigation : c'est l'ecran affiche au
            // debut de l'image qui compte, pas celui qu'on ouvrirait avec la meme touche.
            if (select && AwaitingConfirm)
            {
                Play(_open);
                _device.ConfirmStory();
                return;
            }

            switch (_app)
            {
                case App.Accueil: NavigateHome(up, down, left, right, select); break;
                case App.UberBagarre: NavigateRdv(left, right); break;
                case App.Messages: NavigateMessages(up, down, select); break;
                case App.Appels: NavigateCalls(up, down, select); break;
                case App.Galerie: NavigateGallery(up, down, left, right, select); break;
                case App.Reglages: NavigateSettings(up, down, left, right, select); break;
            }
        }

        private void NavigateHome(bool up, bool down, bool left, bool right, bool select)
        {
            int count = Apps.Count;
            int before = _homeSelection;

            if (left) _homeSelection--;
            if (right) _homeSelection++;
            if (up) _homeSelection -= 3;
            if (down) _homeSelection += 3;

            _homeSelection = Mathf.Clamp(_homeSelection, 0, count - 1);
            if (_homeSelection != before) Play(_tick);

            if (select) Open(Apps[_homeSelection], true);
        }

        private void NavigateRdv(bool left, bool right)
        {
            int before = _rdvTab;
            if (left) _rdvTab = 0;
            if (right) _rdvTab = 1;
            if (_rdvTab != before) Play(_tick);
        }

        private void NavigateMessages(bool up, bool down, bool select)
        {
            if (_thread < 0)
            {
                _listSelection = Step(_listSelection, up, down, ThreadCount);
                if (!select) return;

                _thread = _listSelection;
                _threadScroll = 0;
                Play(_open);
                return;
            }

            int count = Messages(_thread).Count;
            if (up) _threadScroll = Mathf.Min(_threadScroll + 1, Mathf.Max(0, count - 1));
            if (down) _threadScroll = Mathf.Max(0, _threadScroll - 1);

            if (select && _thread == 0 && _device.AppInstalled && _linkSent)
            {
                ShowToast("Application déjà installée.");
            }
        }

        private void NavigateCalls(bool up, bool down, bool select)
        {
            if (_device.Current == PhoneDevice.Screen.EnAppel) return;

            IList<Call> calls = Calls;
            _listSelection = Step(_listSelection, up, down, calls.Count);

            if (!select) return;

            string name = calls[_listSelection].Name;
            ShowToast(name == "SAMI" ? "Messagerie : « C'est Sami. Laisse un message. »" : "Forfait épuisé. Appel impossible.");
            Play(_back);
        }

        private void NavigateGallery(bool up, bool down, bool left, bool right, bool select)
        {
            int count = PhoneGallery.Count;
            if (count == 0) return;

            if (_openPhoto >= 0)
            {
                int before = _openPhoto;
                if (left || up) _openPhoto--;
                if (right || down) _openPhoto++;
                _openPhoto = Mathf.Clamp(_openPhoto, 0, count - 1);
                if (_openPhoto != before) Play(_tick);
                return;
            }

            int previous = _listSelection;
            if (left) _listSelection--;
            if (right) _listSelection++;
            if (up) _listSelection -= 3;
            if (down) _listSelection += 3;
            _listSelection = Mathf.Clamp(_listSelection, 0, count - 1);
            if (_listSelection != previous) Play(_tick);

            if (!select) return;

            _openPhoto = _listSelection;
            Play(_open);
        }

        private void NavigateSettings(bool up, bool down, bool left, bool right, bool select)
        {
            _settingSelection = Step(_settingSelection, up, down, SettingCount);

            if (!left && !right && !select) return;

            switch (_settingSelection)
            {
                case 0:
                    int direction = left ? -1 : 1;
                    if (select) _brightness = (_brightness + 1) % BrightnessLevels.Length;
                    else _brightness = Mathf.Clamp(_brightness + direction, 0, BrightnessLevels.Length - 1);
                    break;

                case 1:
                    _nightMode = !_nightMode;
                    break;

                default:
                    _ringer = !_ringer;
                    break;
            }

            Play(_tick);
            SaveSettings();
        }

        private int Step(int value, bool up, bool down, int count)
        {
            int before = value;
            if (up) value--;
            if (down) value++;
            value = Mathf.Clamp(value, 0, Mathf.Max(0, count - 1));
            if (value != before) Play(_tick);
            return value;
        }

        private void Back()
        {
            Play(_back);

            if (_app == App.Messages && _thread >= 0)
            {
                _thread = -1;
                return;
            }

            if (_app == App.Galerie && _openPhoto >= 0)
            {
                _openPhoto = -1;
                return;
            }

            if (_app == App.Accueil)
            {
                _device.Lower();
                return;
            }

            Open(App.Accueil, false);
        }

        /// <summary>Ouvre une appli. <paramref name="fromHome"/> : choisie par le joueur (son d'ouverture).</summary>
        public void Open(App app, bool fromHome)
        {
            if (fromHome) Play(_open);

            if (_app != app)
            {
                _listSelection = 0;
                _openPhoto = -1;
                if (app != App.Messages) _thread = -1;
            }

            // Revenir a l'accueil garde la case d'ou l'on vient : on retrouve son appli.
            if (app == App.Accueil && _app != App.Accueil)
            {
                int index = Apps.IndexOf(_app);
                if (index >= 0) _homeSelection = index;
            }

            if (app == App.UberBagarre && fromHome) _rdvTab = 0;
            _app = app;
        }

        public void ShowToast(string text)
        {
            _toast = text;
            _toastUntil = Time.unscaledTime + 2.2f;
        }

        // --------------------------------------------------------------- sonnerie et sons

        private void UpdateRing()
        {
            if (_ringSource == null) return;

            bool ringing = _device.IsRinging;
            AudioClip wanted = _ringer ? _ringtone : _buzz;

            if (!ringing)
            {
                if (_ringSource.isPlaying) _ringSource.Stop();
                return;
            }

            if (_ringSource.clip != wanted)
            {
                _ringSource.clip = wanted;
                _ringSource.Stop();
            }

            _ringSource.volume = _ringVolume;
            if (!_ringSource.isPlaying) _ringSource.Play();
        }

        private void Play(AudioClip clip)
        {
            if (_uiSource != null && clip != null) _uiSource.PlayOneShot(clip, _uiVolume);
        }

        private const int SampleRate = 22050;

        private static AudioClip Tone(string name, float from, float to, float duration, float level)
        {
            int length = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[length];
            float phase = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)length;
                phase += Mathf.Lerp(from, to, t) / SampleRate;
                phase -= Mathf.Floor(phase);

                float envelope = Mathf.Clamp01(t * 12f) * (1f - t) * (1f - t);
                data[n] = Mathf.Sin(phase * Mathf.PI * 2f) * envelope * level;
            }

            AudioClip clip = AudioClip.Create(name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Sonnerie : quatre notes claires, deux fois, puis un silence. Boucle de 2,4 s.</summary>
        private static AudioClip Ringtone()
        {
            int length = Mathf.RoundToInt(2.4f * SampleRate);
            float[] data = new float[length];
            float[] notes = { 1318.5f, 987.8f, 1174.7f, 1568f };

            for (int repeat = 0; repeat < 2; repeat++)
            {
                for (int i = 0; i < notes.Length; i++)
                {
                    int start = Mathf.RoundToInt((repeat * 0.62f + i * 0.14f) * SampleRate);
                    int count = Mathf.RoundToInt(0.22f * SampleRate);

                    for (int n = 0; n < count && start + n < length; n++)
                    {
                        float t = n / (float)SampleRate;
                        float envelope = Mathf.Exp(-t * 14f) * Mathf.Clamp01(t * 400f);

                        // Timbre de marimba : fondamentale et un partiel a 4x, amorti plus vite.
                        float sample = Mathf.Sin(2f * Mathf.PI * notes[i] * t)
                                       + 0.25f * Mathf.Sin(2f * Mathf.PI * notes[i] * 4f * t) * Mathf.Exp(-t * 30f);

                        data[start + n] += sample * envelope * 0.4f;
                    }
                }
            }

            AudioClip clip = AudioClip.Create("Sonnerie", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Vibreur : bourdonnement grave 0,45 s, puis 0,55 s de silence.</summary>
        private static AudioClip Buzz()
        {
            int length = SampleRate;
            float[] data = new float[length];

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                if (t > 0.45f) break;

                float envelope = Mathf.Clamp01(t * 60f) * Mathf.Clamp01((0.45f - t) * 60f);
                float wave = Mathf.Sin(2f * Mathf.PI * 165f * t);
                data[n] = Mathf.Sign(wave) * Mathf.Pow(Mathf.Abs(wave), 0.4f) * envelope * 0.35f;
            }

            AudioClip clip = AudioClip.Create("Vibreur", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // --------------------------------------------------------------- réglages

        private void LoadSettings()
        {
            _brightness = Mathf.Clamp(PlayerPrefs.GetInt(PrefsPrefix + "luminosite", _brightness), 0,
                BrightnessLevels.Length - 1);
            _nightMode = PlayerPrefs.GetInt(PrefsPrefix + "nuit", _nightMode ? 1 : 0) != 0;
            _ringer = PlayerPrefs.GetInt(PrefsPrefix + "sonnerie", _ringer ? 1 : 0) != 0;
        }

        private void SaveSettings()
        {
            PlayerPrefs.SetInt(PrefsPrefix + "luminosite", _brightness);
            PlayerPrefs.SetInt(PrefsPrefix + "nuit", _nightMode ? 1 : 0);
            PlayerPrefs.SetInt(PrefsPrefix + "sonnerie", _ringer ? 1 : 0);
            PlayerPrefs.Save();
        }
    }
}
