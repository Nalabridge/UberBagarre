using System;
using System.Collections;
using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// L'histoire, « Hyland cogne », jouée dans la ville ouverte. Pas de chapitres à l'écran :
    /// elle arrive au téléphone (appels, SMS, l'appli), au courrier, dans le journal en ligne,
    /// et par les gens qu'on croise. Le détail complet est dans Docs/Histoire.md.
    ///
    /// Le moteur : un fil principal (Saga) avance d'étape en étape. Chaque étape est gardée par
    /// un drapeau de la sauvegarde : une partie rechargée rejoue le fil depuis le début de
    /// l'acte en sautant ce qui est fait. À côté, la ville (City) fait vivre les histoires
    /// secondaires : Nestor et ses échéances, maman à la laverie, M. Chen, le tournoi de Ray,
    /// les SMS, Hyland Info, et les gens qui restent à leur place pour qu'on leur parle.
    ///
    /// Les actes (en interne seulement) : Prologue (Chambre 3), 1 (Les petites notes),
    /// 2 (La Fosse), 3 (Le Comptable), 4 (Le nom), 5 (Le roi d'Hyland), puis l'épilogue.
    /// </summary>
    public partial class OpenWorldStory : MonoBehaviour
    {
        [Serializable]
        public class Character
        {
            [Tooltip("moretti, karim, dragan, milan, taureau, comptable, sami, facteur, olga, dentiste, sven, brandt…")]
            public string tag;

            public OpenWorldDirector.Profile profile = new OpenWorldDirector.Profile();
            public OpenWorldDirector.Spot spot = new OpenWorldDirector.Spot();
        }

        public const int Prologue = 0;
        public const int Acte1 = 1;
        public const int Acte2 = 2;
        public const int Acte3 = 3;
        public const int Acte4 = 4;
        public const int Acte5 = 5;
        public const int Fin = 6;

        /// <summary>Les actes (menus de test seulement : le jeu, lui, n'affiche jamais de chapitre).</summary>
        public static readonly string[] ChapterTitles =
        {
            "PROLOGUE  —  CHAMBRE 3",
            "ACTE 1  —  LES PETITES NOTES",
            "ACTE 2  —  LA FOSSE",
            "ACTE 3  —  LE COMPTABLE",
            "ACTE 4  —  LE NOM",
            "ACTE 5  —  LE ROI D'HYLAND"
        };

        public static readonly string[] ChapterPitches =
        {
            "Le motel, le courrier, l'appel de Sami. Bruno Moretti. Coach Ray. Nestor.",
            "Deux étoiles. Karim, Dragan Kovac, Lina, la vengeance de Milan, l'inspectrice Duval.",
            "Rosa Delmas, la Ligue, la ceinture, la salle de Ray, le Taureau, SARK Holding.",
            "Sarkis. Le casse du casino. Le carnet noir. Un nom.",
            "Sami. La filature, les docks, la vérité. Jeff. Les serveurs de l'appli.",
            "Mme Keller, la chasse à l'homme, la nuit de l'élection. Holt."
        };

        [Header("References")]
        [SerializeField] private OpenWorldDirector _director;
        [SerializeField] private PlayerProgress _progress;
        [SerializeField] private SubtitleDisplay _subtitles;
        [SerializeField] private ObjectiveDisplay _objectives;
        [SerializeField] private ScreenFader _fader;
        [SerializeField] private PhoneDevice _phone;
        [SerializeField] private CityMap _map;
        [SerializeField] private HomeRegistry _homes;
        [SerializeField] private ComputerScreen _computer;
        [SerializeField] private LetterReader _letterReader;

        [SerializeField]
        [Tooltip("La distribution (les gens à qui l'on parle) et les lieux de l'histoire.")]
        private StoryCast _cast;

        [SerializeField, Min(0.5f)]
        [Tooltip("Durée du téléchargement de l'appli, en secondes.")]
        private float _installDuration = 6f;

        [Header("Le Vertigo")]
        [SerializeField] private Vector3 _vertigoDoor;
        [SerializeField] private GameObject _club;
        [SerializeField] private GameObject _ringGate;
        [SerializeField] private RoomZone _backRoom;

        [Header("Adversaires")]
        [SerializeField] private Character[] _characters = new Character[0];

        [Header("Loyer")]
        [SerializeField, Min(0)] private int _rent = 450;
        [SerializeField, Min(1)] private int _rentEvery = 7;

        [SerializeField]
        [Tooltip("Sans écran titre (lancement direct) : la partie démarre seule — la sauvegarde si elle existe.")]
        private bool _autoStart = true;

        private bool _started;
        private bool _linkConfirmed;
        private bool _installing;
        private bool _answered;
        private string _morning;
        private int _answer;

        private Transform _playerTransform;
        private Combatant _playerCombatant;
        private PlayerInputReader _input;

        private readonly Dictionary<string, int> _talks = new Dictionary<string, int>();
        private readonly Dictionary<string, bool> _results = new Dictionary<string, bool>();
        private readonly Dictionary<string, int> _choices = new Dictionary<string, int>();
        private readonly HashSet<string> _reserved = new HashSet<string>();
        private readonly HashSet<string> _waitingTalk = new HashSet<string>();
        private string _pendingTalk;
        private int _pendingTalkFrame;

        public bool Started { get { return _started; } }

        /// <summary>La partie commence (nouvelle, reprise, ou un acte choisi).</summary>
        public event Action Began;

        public static string ChapterName(int chapter)
        {
            if (chapter >= Fin) return "ÉPILOGUE";
            return ChapterTitles[Mathf.Clamp(chapter, 0, ChapterTitles.Length - 1)];
        }

        // ------------------------------------------------------------------ cycle

        private void Awake()
        {
            // Tant que la partie n'a pas commencé (écran titre), pas de course.
            if (_director != null)
            {
                _director.Paused = true;
                _director.StoryControlsApp = true;
            }

            PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
            if (motor != null)
            {
                _playerTransform = motor.transform;
                _playerCombatant = motor.GetComponent<Combatant>();
                _input = motor.GetComponent<PlayerInputReader>();
            }

            if (_input == null) _input = FindAnyObjectByType<PlayerInputReader>();
        }

        private void OnEnable()
        {
            if (_director != null)
            {
                _director.StoryContractFinished += OnContractFinished;
                _director.StoryChoiceMade += OnStoryChoice;
                _director.Sleeping += OnSleeping;
                _director.Slept += OnSlept;
                _director.Died += OnDied;
            }

            if (_phone != null)
            {
                _phone.Answered += OnAnswered;
                _phone.StoryConfirmed += OnPhoneConfirmed;
            }

            if (_homes != null) _homes.LettersRead += OnLetters;
            if (_cast != null) _cast.Talked += OnTalked;
            if (_progress != null) _progress.DebtPaid += OnDebtPaid;
        }

        private void OnDisable()
        {
            if (_director != null)
            {
                _director.StoryContractFinished -= OnContractFinished;
                _director.StoryChoiceMade -= OnStoryChoice;
                _director.Sleeping -= OnSleeping;
                _director.Slept -= OnSlept;
                _director.Died -= OnDied;
            }

            if (_phone != null)
            {
                _phone.Answered -= OnAnswered;
                _phone.StoryConfirmed -= OnPhoneConfirmed;
            }

            if (_homes != null) _homes.LettersRead -= OnLetters;
            if (_cast != null) _cast.Talked -= OnTalked;
            if (_progress != null) _progress.DebtPaid -= OnDebtPaid;
        }

        private void Start()
        {
            if (!_autoStart) return;
            if (PlayerProgress.HasSave) Continue();
            else NewGame();
        }

        private void Update()
        {
            if (_installing && _phone != null)
            {
                _phone.DownloadProgress += Time.deltaTime / Mathf.Max(0.5f, _installDuration);
            }

            // Quelqu'un à qui l'on parle, et aucune scène ne l'attendait : sa vie à lui.
            if (_pendingTalk != null && Time.frameCount > _pendingTalkFrame + 1)
            {
                string id = _pendingTalk;
                _pendingTalk = null;
                if (_started && _progress != null) StartCoroutine(Visit(id));
            }
        }

        // ------------------------------------------------------------------ lancement

        public void NewGame()
        {
            StartChapter(Prologue);
        }

        public void Continue()
        {
            if (_progress == null || !_progress.Load())
            {
                NewGame();
                return;
            }

            Begin();
        }

        /// <summary>
        /// Commence à un acte (outils de test) : une partie neuve, avec ce qu'on aurait fait et
        /// gagné jusque-là — les choix par défaut sont ceux du Justicier.
        /// </summary>
        public void StartChapter(int chapter)
        {
            if (_progress == null) return;

            _progress.ResetProgress();
            NewsFeed.Clear();
            PlayerPrefs.DeleteKey(FallKey);
            chapter = Mathf.Clamp(chapter, Prologue, Acte5);
            if (chapter > Prologue) SkipTo(chapter);
            Begin();
        }

        private void Begin()
        {
            _started = true;
            StopAllCoroutines();
            ResetRuntime();

            if (_homes != null) _homes.Apply();
            if (_director != null)
            {
                _director.ClearStoryContract();
                _director.ReturnHome();
            }

            StartCoroutine(Opening());

            Action handler = Began;
            if (handler != null) handler();
        }

        private void ResetRuntime()
        {
            _results.Clear();
            _choices.Clear();
            _side.Clear();
            _fall = 0;
            _reserved.Clear();
            _waitingTalk.Clear();
            _pendingTalk = null;
            _installing = false;
            _answered = false;
            _lettersOpened = false;
            if (_cast != null) _cast.HideAll();
            if (PoliceSystem.Instance != null)
            {
                PoliceSystem.Instance.Floor = 0;
                PoliceSystem.Instance.NoBribes = false;
            }

            if (_ringGate != null) _ringGate.SetActive(true);
            if (_director != null) _director.NapUntilHour = -1f;
            if (CombatPresence.Player != null) CombatPresence.Player.Forced = false;
        }

        private IEnumerator Opening()
        {
            if (_fader != null) _fader.SetBlackImmediate();

            // L'écran de chargement : où l'on se réveille, et quel jour. Pas de numéro de
            // chapitre — l'histoire avance d'elle-même, au téléphone.
            bool fresh = _progress != null && !_progress.HasFlag("p:reveil");
            string heading = fresh || _progress == null ? "MOTEL HYLAND" : Weekday(_progress.Day).ToUpperInvariant() + "  ·  JOUR " + _progress.Day;
            string detail = fresh ? "Chambre 3  ·  14 h 00" :
                _progress != null ? _progress.Money + " EUR  ·  dette : " + _progress.Debt + " EUR" : "";
            LoadingScreen.Hold(this, heading, detail);

            // La ville se charge par-dessus : on attend qu'elle soit là.
            float waited = 0f;
            while (!MapStreamer.Ready && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_director != null) _director.ReturnHome();

            // Une partie neuve commence à 14 h : la première course, elle, attendra la nuit.
            if (fresh && WorldClock.Instance != null) WorldClock.Instance.SetHour(14f);

            // Deux images pour que tout soit en place (physique, portes, voitures) derrière l'écran.
            yield return null;
            yield return null;
            LoadingScreen.Release(this);

            waited = 0f;
            while (LoadingScreen.Visible && waited < 20f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_fader != null)
            {
                if (fresh)
                {
                    _fader.ShowCard("Motel Hyland, chambre 3.\nDeux heures de l'après-midi.");
                    yield return new WaitForSeconds(2.4f);
                }

                _fader.ShowCard(null);
                _fader.FadeIn(1.6f);
                yield return new WaitForSeconds(1f);
            }

            Run();
        }

        /// <summary>Remet le monde d'accord avec la sauvegarde, puis lance le fil et la ville.</summary>
        private void Run()
        {
            RestoreWorld();
            StartCoroutine(Saga());
            StartCoroutine(City());
        }

        private IEnumerator Saga()
        {
            while (_progress != null && _progress.Chapter <= Acte5)
            {
                int act = _progress.Chapter;
                if (_director != null) _director.Paused = act == Prologue && !_progress.HasFlag("p:moretti");

                switch (act)
                {
                    case Prologue: yield return PrologueLine(); break;
                    case Acte1: yield return Acte1Line(); break;
                    case Acte2: yield return Acte2Line(); break;
                    case Acte3: yield return Acte3Line(); break;
                    case Acte4: yield return Acte4Line(); break;
                    default: yield return Acte5Line(); break;
                }

                // Un acte qui rend la main sans avancer (fin atteinte) : on s'arrête là.
                if (_progress.Chapter == act) break;
            }

            // Après la fin du Justicier, l'appli n'existe plus.
            if (_director != null) _director.Paused = Done("appli:fermee");
            Goal(string.Empty);
        }

        private void Advance(int act)
        {
            if (_progress != null) _progress.SetChapter(act);
            Goal(string.Empty);
        }

        // ------------------------------------------------------------------ événements

        private void OnPhoneConfirmed()
        {
            if (_phone != null && _phone.Current == PhoneDevice.Screen.Lien) _linkConfirmed = true;
        }

        private void OnAnswered()
        {
            _answered = true;
        }

        private void OnContractFinished(string tag, bool success)
        {
            if (tag != null) _results[tag] = success;
        }

        private void OnStoryChoice(string tag, int answer)
        {
            if (tag != null) _choices[tag] = answer;
        }

        private void OnTalked(StoryActor actor)
        {
            if (actor == null) return;
            int n;
            _talks.TryGetValue(actor.Id, out n);
            _talks[actor.Id] = n + 1;

            // Une scène attend cette personne : c'est elle qui répond. Sinon, sa vie à elle.
            if (_waitingTalk.Contains(actor.Id)) return;
            _pendingTalk = actor.Id;
            _pendingTalkFrame = Time.frameCount;
        }

        private void OnDebtPaid(int amount)
        {
            if (_progress == null) return;
            _progress.SetCounter("nestor:verse", _progress.Counter("nestor:verse") + amount);
        }

        private void OnLetters(HomeRegistry.Home home)
        {
            if (!_started || _progress == null) return;

            // Le premier courrier, c'est le prologue qui l'ouvre.
            if (!_progress.HasFlag("p:courrier"))
            {
                _lettersOpened = true;
                return;
            }

            int left = _rentEvery - (_progress.Day % _rentEvery);
            string rent = home != null && home.name != "Motel"
                ? "Plus de loyer. Juste des pubs, et une facture d'eau."
                : "Le loyer : " + _rent + " euros, prélevés dans " + left + (left > 1 ? " jours." : " jour.");
            Play(L("MOI", rent));
        }

        /// <summary>Pendant la nuit, avant la sauvegarde : le loyer.</summary>
        private void OnSleeping()
        {
            _morning = null;
            if (!_started || _progress == null) return;

            // Le loyer du motel, tous les sept jours.
            if (_rent <= 0 || _progress.Home != "Motel" || _progress.Day <= 1 || _progress.Day % _rentEvery != 0) return;

            // Payé d'avance depuis l'appli Banque : le motel ne prélève rien.
            if (_progress.RentPaidUntil >= _progress.Day) return;

            if (_progress.Spend(_rent, "Loyer motel Hyland"))
            {
                _morning = "Le motel a prélevé le loyer. " + _rent + " euros de moins.";
                return;
            }

            int all = _progress.Money;
            if (all > 0) _progress.AddMoney(-all, "Loyer motel (partiel)");
            Mail("Motel Hyland", "Dernier avertissement (jour " + _progress.Day + ")",
                "Votre compte ne couvrait pas le loyer. Nous avons prélevé " + all + " €.\nLa prochaine fois, nous changeons la serrure.", true);
            _morning = "Le loyer a vidé le compte. Et il manquait encore.";
        }

        private void OnSlept()
        {
            if (!string.IsNullOrEmpty(_morning)) Play(L("MOI", _morning));
            _morning = null;
        }

        private void OnDied()
        {
            // La partie vient d'être rechargée : on reprend à la dernière nuit.
            if (!_started) return;
            CountFallDeath();
            StopAllCoroutines();
            ResetRuntime();
            if (_director != null) _director.ClearStoryContract();
            if (_homes != null) _homes.Apply();
            Run();
        }

        // ------------------------------------------------------------------ le monde, d'après la sauvegarde

        private void RestoreWorld()
        {
            if (_progress == null) return;
            if (_phone != null) _phone.AppInstalled = _progress.HasFlag("p:appli") || _progress.Chapter > Prologue;
            if (PoliceSystem.Instance != null) PoliceSystem.Instance.NoBribes = _progress.HasFlag("carnet:duval");

            // Le journal et les mails déjà reçus (ils ne sont pas dans la sauvegarde : l'histoire les redonne).
            for (int i = 0; i < ArticleIds.Length; i++)
            {
                if (_progress.HasFlag("news:" + ArticleIds[i])) PublishArticle(ArticleIds[i], false);
            }

            for (int i = 0; i < MailIds.Length; i++)
            {
                if (_progress.HasFlag("mail:" + MailIds[i])) SendMail(MailIds[i], false);
            }
        }

        // ------------------------------------------------------------------ outils : état

        private bool Done(string flag)
        {
            return _progress != null && _progress.HasFlag(flag);
        }

        private void Set(string flag)
        {
            if (_progress != null) _progress.SetFlag(flag);
        }

        private int Count(string name)
        {
            return _progress != null ? _progress.Counter(name) : 0;
        }

        private void SetCount(string name, int value)
        {
            if (_progress != null) _progress.SetCounter(name, value);
        }

        private int Day { get { return _progress != null ? _progress.Day : 1; } }

        private float Hour { get { return WorldClock.Instance != null ? WorldClock.Instance.Hour : 12f; } }

        /// <summary>Jour 1 = lundi.</summary>
        private static int WeekdayIndex(int day)
        {
            return ((day - 1) % 7 + 7) % 7;
        }

        private static readonly string[] Days = { "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi", "dimanche" };

        private static string Weekday(int day)
        {
            return Days[WeekdayIndex(day)];
        }

        private static bool IsNight(float hour)
        {
            return hour >= 21.5f || hour < 4f;
        }

        /// <summary>Le Code : négatif = la Brute, positif = le Justicier. Un petit bandeau le dit.</summary>
        private void ChangeCode(int delta, string reason)
        {
            if (_progress == null || delta == 0) return;
            _progress.ChangeCode(delta, reason);
            if (_director != null)
            {
                _director.Banner((delta > 0 ? "▲ JUSTICIER" : "▼ BRUTE") + "  —  " + reason, 3.5f);
            }
        }

        private bool Speaking
        {
            get { return _subtitles != null && _subtitles.IsSpeaking; }
        }

        private Vector3 PlayerPosition
        {
            get { return _playerTransform != null ? _playerTransform.position : Vector3.zero; }
        }

        private bool PlayerAlive
        {
            get { return _playerCombatant == null || _playerCombatant.IsAlive; }
        }

        /// <summary>Rien ne se passe : pas de course, pas de dialogue, pas de menu, pas de fondu.</summary>
        private bool IsCalm
        {
            get
            {
                if (_director == null) return true;
                if (_director.Busy || ChoicePrompt.Open || Speaking || GameMenu.IsOpen || LoadingScreen.Visible) return false;
                if (ModalScreen.Active || !PlayerAlive) return false;
                if (PoliceSystem.Instance != null && PoliceSystem.Instance.Arresting) return false;
                return _fader == null || _fader.IsClear;
            }
        }

        private IEnumerator Calm()
        {
            while (!IsCalm) yield return null;
        }

        // ------------------------------------------------------------------ outils : parler, écrire

        private static DialogueLine L(string speaker, string text)
        {
            return DialogueLine.Say(speaker, text);
        }

        private void Play(DialogueLine line)
        {
            if (_subtitles != null) _subtitles.Play(line);
        }

        private IEnumerator Lines(params DialogueLine[] lines)
        {
            if (_subtitles == null || lines == null || lines.Length == 0) yield break;
            while (Speaking) yield return null;
            _subtitles.Play(lines);
            yield return null;
            while (_subtitles.IsSpeaking) yield return null;
        }

        private void Sms(string from, string text)
        {
            if (_progress != null) _progress.AddSms(from, text);
        }

        /// <summary>Plusieurs SMS à la suite, comme quelqu'un qui tape.</summary>
        private IEnumerator Texts(string from, params string[] texts)
        {
            for (int i = 0; i < texts.Length; i++)
            {
                Sms(from, texts[i]);
                if (i < texts.Length - 1) yield return new WaitForSeconds(1.6f + texts[i].Length * 0.02f);
            }
        }

        /// <summary>
        /// Le téléphone sonne : on répond (on sort le téléphone), la conversation, on raccroche.
        /// Pas de réponse au bout de 30 s : appel manqué, ça rappelle un peu plus tard.
        /// </summary>
        private IEnumerator Call(string caller, params DialogueLine[] lines)
        {
            yield return Calm();
            if (_phone == null)
            {
                yield return Lines(lines);
                yield break;
            }

            string before = _objectives != null ? _objectives.Current : string.Empty;
            while (true)
            {
                _answered = false;
                _phone.Available = true;
                _phone.Ring(caller);
                Goal(caller + " appelle : sors le téléphone (" + _phone.PhoneKeyName.ToUpperInvariant() + ") pour répondre.");

                float t = 0f;
                while (!_answered && t < 30f)
                {
                    t += Time.deltaTime;
                    yield return null;
                }

                if (_answered) break;

                _phone.HangUp();
                if (_director != null) _director.Banner("Appel manqué : " + caller, 3f);
                Goal(before);
                yield return new WaitForSeconds(40f);
                yield return Calm();
            }

            Goal(before);
            yield return Lines(lines);
            if (_phone.Current == PhoneDevice.Screen.EnAppel) _phone.HangUp();
        }

        private void Goal(string objective)
        {
            if (_objectives != null) _objectives.Set(objective ?? string.Empty);
        }

        private void Mark(string place)
        {
            StoryCast.Place p = _cast != null ? _cast.Where(place) : null;
            if (p != null && _map != null) _map.SetWaypoint(p.position, p.label.ToUpperInvariant(), null);
        }

        private void Unmark()
        {
            if (_map != null) _map.ClearWaypoint();
        }

        private void Mail(string from, string subject, string body, bool unread)
        {
            ComputerScreen computer = _computer != null ? _computer : ComputerScreen.Instance;
            if (computer != null) computer.AddMail(from, subject, body, unread);
            if (unread && _director != null) _director.Banner("Nouveau mail : " + from + " — " + subject + " (ordinateur)", 4f);
        }

        // ------------------------------------------------------------------ outils : les gens

        /// <summary>Pose quelqu'un à un lieu ; l'histoire se le réserve (la ville n'y touche plus).</summary>
        private StoryActor Put(string actor, string place, Vector3 offset, float turn = 0f)
        {
            _reserved.Add(actor);
            return _cast != null ? _cast.PlaceAt(actor, place, offset, turn) : null;
        }

        private StoryActor Put(string actor, string place)
        {
            return Put(actor, place, Vector3.zero);
        }

        /// <summary>L'histoire rend la personne à sa vie (la ville la replacera, ou pas).</summary>
        private void Free(string actor, bool hide = true)
        {
            _reserved.Remove(actor);
            if (hide && _cast != null) _cast.Hide(actor);
        }

        private int Talks(string actor)
        {
            int n;
            _talks.TryGetValue(actor, out n);
            return n;
        }

        /// <summary>Attend qu'on parle à cette personne (E). Sans distribution, on passe.</summary>
        private IEnumerator TalkTo(string actor, string place, string objective)
        {
            StoryActor a = _cast != null ? _cast.Actor(actor) : null;
            if (a == null) yield break;
            if (!a.Placed) Put(actor, place);
            _reserved.Add(actor);
            a.SetTalkable(true);
            if (objective != null) Goal(objective);
            if (place != null) Mark(place);

            _waitingTalk.Add(actor);
            int before = Talks(actor);
            while (Talks(actor) == before) yield return null;
            _waitingTalk.Remove(actor);
            if (_pendingTalk == actor) _pendingTalk = null;

            Unmark();
            Goal(string.Empty);
            yield return Calm();
        }

        /// <summary>Attend que le joueur soit à moins de <paramref name="radius"/> mètres d'un lieu.</summary>
        private IEnumerator Reach(string place, float radius, string objective)
        {
            StoryCast.Place p = _cast != null ? _cast.Where(place) : null;
            if (p == null) yield break;
            if (objective != null) Goal(objective);
            Mark(place);
            while (Flat(PlayerPosition - p.position) > radius) yield return null;
            Unmark();
        }

        /// <summary>Le premier de ces lieux qui existe (un intérieur, sinon sa façade).</summary>
        private string Pick(params string[] places)
        {
            for (int i = 0; i < places.Length; i++)
            {
                if (_cast != null && _cast.Where(places[i]) != null) return places[i];
            }

            return places.Length > 0 ? places[places.Length - 1] : null;
        }

        private bool Near(string place, float radius)
        {
            StoryCast.Place p = _cast != null ? _cast.Where(place) : null;
            return p != null && Flat(PlayerPosition - p.position) <= radius;
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>Une question, deux à quatre réponses : l'indice choisi est dans <see cref="_answer"/>.</summary>
        private IEnumerator Choose(string speaker, string question, params string[] options)
        {
            yield return Calm();
            bool answered = false;
            ChoicePrompt.Ask(speaker, question, options, delegate(int i)
            {
                _answer = i;
                answered = true;
            });
            while (!answered) yield return null;
        }

        private IEnumerator UntilHour(float from, string objective)
        {
            if (objective != null) Goal(objective);
            if (_director != null && !IsNight(Hour) && Hour < from) _director.NapUntilHour = from;
            while (!(Hour >= from || (from > 12f && Hour < 4f))) yield return null;
            if (_director != null) _director.NapUntilHour = -1f;
        }

        private IEnumerator UntilNight(string objective)
        {
            yield return UntilHour(21.75f, objective);
        }

        /// <summary>Attend un autre jour (une nuit de sommeil, ou une nuit au poste).</summary>
        private IEnumerator UntilNextDay(int from, string objective)
        {
            if (objective != null) Goal(objective);
            while (Day <= from) yield return null;
            yield return Calm();
        }

        // ------------------------------------------------------------------ outils : les combats

        private Character Find(string role)
        {
            for (int i = 0; i < _characters.Length; i++)
            {
                if (_characters[i] != null && _characters[i].tag == role) return _characters[i];
            }

            return null;
        }

        /// <summary>Une fiche d'adversaire (le rôle), sous un autre nom si besoin.</summary>
        private OpenWorldDirector.Profile Profile(string role, string name = null)
        {
            Character c = Find(role);
            if (c == null || c.profile == null || c.profile.template == null) return null;
            OpenWorldDirector.Profile p = c.profile;
            if (name == null) return p;
            return new OpenWorldDirector.Profile
            {
                name = name, age = p.age, clothing = p.clothing, record = p.record, stars = p.stars, template = p.template
            };
        }

        private OpenWorldDirector.Spot SpotAt(string role, string place, string activity, Vector3 offset)
        {
            StoryCast.Place p = _cast != null && place != null ? _cast.Where(place) : null;
            Character c = Find(role);
            if (p == null) return c != null ? c.spot : null;
            return new OpenWorldDirector.Spot
            {
                name = p.label,
                position = p.position + Quaternion.Euler(0f, p.yaw, 0f) * offset,
                yaw = p.yaw,
                activity = activity ?? (c != null ? c.spot.activity : null)
            };
        }

        /// <summary>Une course de l'histoire : un rôle, une étiquette unique, un lieu.</summary>
        private OpenWorldDirector.StoryContract Job(string role, string tag, string place, string activity = null)
        {
            return new OpenWorldDirector.StoryContract
            {
                Tag = tag,
                Profile = Profile(role),
                Spot = SpotAt(role, place, activity, Vector3.zero),
                Client = "CLIENT VÉRIFIÉ",
                Reward = 250,
                Experience = 200
            };
        }

        /// <summary>Des renforts : n fois le même rôle, numérotés.</summary>
        private OpenWorldDirector.Profile[] Crew(string role, string name, int count)
        {
            List<OpenWorldDirector.Profile> crew = new List<OpenWorldDirector.Profile>();
            for (int i = 0; i < count; i++)
            {
                OpenWorldDirector.Profile p = Profile(role, count > 1 ? name + " " + (i + 1) : name);
                if (p != null) crew.Add(p);
            }

            return crew.ToArray();
        }

        /// <summary>Un style de combat (plus de vie, des coups plus lourds, plus vite…) en pourcentages.</summary>
        private static Action<Combatant> Style(float health, float strength, float attackSpeed, float moveSpeed, float defense, float brain)
        {
            return delegate(Combatant c)
            {
                if (c == null || c.Stats == null) return;
                const string source = "Histoire";
                c.Stats.RemoveBySource(source);
                if (health != 0f) c.Stats.AddModifier(new StatModifier { stat = StatType.MaxHealth, percent = true, value = health, source = source });
                if (strength != 0f) c.Stats.AddModifier(new StatModifier { stat = StatType.Strength, percent = true, value = strength, source = source });
                if (attackSpeed != 0f) c.Stats.AddModifier(new StatModifier { stat = StatType.AttackSpeed, percent = true, value = attackSpeed, source = source });
                if (moveSpeed != 0f) c.Stats.AddModifier(new StatModifier { stat = StatType.MoveSpeed, percent = true, value = moveSpeed, source = source });
                if (defense != 0f) c.Stats.AddModifier(new StatModifier { stat = StatType.Defense, percent = false, value = defense, source = source });
                c.ApplyStats();

                Enemy.EnemyBrain b = c.GetComponent<Enemy.EnemyBrain>();
                if (b != null && brain > 0f) b.ApplyDifficulty(brain);
            };
        }

        /// <summary>Le résultat du dernier combat : 0 = gagné, 1 et plus = une autre réponse au choix d'avant le combat.</summary>
        private int _outcome;

        /// <summary>
        /// Lance une course de l'histoire et attend : gagnée, ou épargnée (réponse au choix). Une
        /// course ratée (K.O., fuite) retombe un peu plus tard — l'histoire ne se perd pas.
        /// </summary>
        /// <param name="once">Une seule tentative (une histoire secondaire) : ratée, <see cref="_outcome"/> vaut -1.</param>
        private IEnumerator Fight(OpenWorldDirector.StoryContract contract, string objective, string retry = null, bool once = false)
        {
            if (_director == null || contract == null || contract.Profile == null || contract.Spot == null)
            {
                Debug.LogWarning("[UberBagarre] Course de l'histoire impossible : " + (contract != null ? contract.Tag : "?"));
                _outcome = 0;
                yield break;
            }

            string tag = contract.Tag;
            while (true)
            {
                // Une seule course d'histoire à la fois.
                while (_director.PendingStoryTag != null) yield return null;
                yield return Calm();

                _results.Remove(tag);
                _choices.Remove(tag);
                _director.QueueStoryContract(contract);
                if (!contract.Direct) Goal(objective ?? "Une course t'attend sur l'appli : sors le téléphone et accepte-la.");

                int answer = 0;
                while (!_results.ContainsKey(tag))
                {
                    if (_choices.TryGetValue(tag, out answer) && answer != 0) break;
                    yield return null;
                }

                Goal(string.Empty);
                if (answer != 0)
                {
                    _outcome = answer;
                    yield break;
                }

                if (_results[tag])
                {
                    _outcome = 0;
                    yield return new WaitForSeconds(3.5f);
                    yield return Calm();
                    yield break;
                }

                if (once)
                {
                    _outcome = -1;
                    yield return new WaitForSeconds(3f);
                    yield return Calm();
                    yield break;
                }

                // Raté : ça retombe (une embuscade revient, un client maintient sa commande).
                yield return new WaitForSeconds(25f);
                yield return Calm();
                Play(L(contract.Direct ? "MOI" : "APPLI", retry ?? (contract.Direct
                    ? "Ils vont revenir. Faut que je sois prêt."
                    : "Le client maintient sa commande. Même sujet. Deuxième chance.")));
                yield return new WaitForSeconds(2f);
            }
        }

        /// <summary>La fosse du Vertigo : on y entre (la grille s'ouvre), on se bat, elle se referme.</summary>
        private IEnumerator Pit(OpenWorldDirector.StoryContract contract)
        {
            contract.Direct = true;
            contract.NoPhoto = true;
            contract.Client = "LA FOSSE";
            if (_ringGate != null) _ringGate.SetActive(false);
            yield return Fight(contract, null, "Rosa t'attend. La Fosse ne pardonne pas deux fois... mais elle aime les revanches.");
            if (_ringGate != null) _ringGate.SetActive(true);
        }

        private bool InBackRoom
        {
            get { return _club != null && _club.activeInHierarchy && (_backRoom == null || _backRoom.ContainsPlayer()); }
        }

        /// <summary>Attend le joueur dans la salle du fond du Vertigo.</summary>
        private IEnumerator BackRoom(string objective)
        {
            Goal(objective);
            if (!InBackRoom && _map != null) _map.SetWaypoint(_vertigoDoor, "LE VERTIGO", null);
            while (!InBackRoom) yield return null;
            Unmark();
        }

        // ------------------------------------------------------------------ outils : fondus, fins

        private IEnumerator Card(string text, float seconds)
        {
            if (_fader == null)
            {
                yield return new WaitForSeconds(seconds);
                yield break;
            }

            _fader.FadeOut(1.2f);
            yield return new WaitForSeconds(1.4f);
            _fader.ShowCard(text);
            yield return new WaitForSeconds(seconds);
            _fader.ShowCard(null);
            _fader.FadeIn(1.2f);
            yield return new WaitForSeconds(1f);
        }

        // ------------------------------------------------------------------ constructeur

        /// <summary>Le constructeur de la scène y déclare les adversaires et le Vertigo.</summary>
        public void Configure(Character[] characters, Vector3 vertigoDoor, GameObject club, GameObject ringGate, RoomZone backRoom,
            bool autoStart, LetterReader letters)
        {
            _backRoom = backRoom;
            _letterReader = letters;
            _characters = characters ?? new Character[0];
            _vertigoDoor = vertigoDoor;
            _club = club;
            _ringGate = ringGate;
            _autoStart = autoStart;
        }
    }
}
