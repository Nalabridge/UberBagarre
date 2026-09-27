using System;
using System.Collections;
using System.Collections.Generic;
using UberBagarre.Phone;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// L'histoire, jouée dans la ville ouverte.
    ///
    /// PROLOGUE — LA PLANQUE. On se réveille au motel. Le courrier : loyer, électricité, banque.
    ///   Sami appelle : une appli, Über Bagarre. Première course : Bruno Moretti, le videur du
    ///   Vertigo, qui fume devant le club à la fin de son service.
    /// CHAPITRE 1 — DEUX ÉTOILES. Les frères Kovac. Dragan d'abord, sur un parking ; son frère
    ///   Milan a tout vu, et la commande suivante tombe aussitôt.
    /// CHAPITRE 2 — TROIS ÉTOILES. La salle du fond du Vertigo : on entre, on attend, la commande
    ///   tombe quand ils ont choisi l'adversaire. Le Taureau. Et quelqu'un avait commandé ce
    ///   combat-là CONTRE toi.
    /// CHAPITRE 3 — QUATRE ÉTOILES. Qui ? Victor Sarkis, le Comptable, derrière le casino. Au
    ///   sol, il crache le nom : Sami. Ton ami pariait contre toi.
    /// CHAPITRE 4 — CINQ ÉTOILES. Sarkis nettoie derrière lui : une commande tombe à ton nom.
    ///   Sujet : Sami, aux docks.
    /// FIN — la ville continue : courses, casino, le manoir sur la colline.
    ///
    /// Entre deux chapitres, le jeu est libre : les courses ordinaires tombent, on s'entraîne,
    /// on joue, on achète. Un chapitre avance sur des faits (une course gagnée, une nuit de
    /// sommeil, une porte franchie), jamais sur un minuteur. Tout ce qui compte est gardé dans
    /// la sauvegarde (chapitre, drapeaux) : une partie rechargée reprend exactement là.
    /// </summary>
    public class OpenWorldStory : MonoBehaviour
    {
        [Serializable]
        public class Character
        {
            [Tooltip("moretti, dragan, milan, taureau, comptable, sami")]
            public string tag;

            public OpenWorldDirector.Profile profile = new OpenWorldDirector.Profile();
            public OpenWorldDirector.Spot spot = new OpenWorldDirector.Spot();
        }

        public const int Prologue = 0;
        public const int Kovac = 1;
        public const int Taureau = 2;
        public const int Comptable = 3;
        public const int Traitre = 4;
        public const int Fin = 5;

        /// <summary>Les chapitres, tels que le menu les propose.</summary>
        public static readonly string[] ChapterTitles =
        {
            "PROLOGUE  —  LA PLANQUE",
            "CHAPITRE 1  —  DEUX ÉTOILES",
            "CHAPITRE 2  —  TROIS ÉTOILES",
            "CHAPITRE 3  —  QUATRE ÉTOILES",
            "CHAPITRE 4  —  CINQ ÉTOILES"
        };

        public static readonly string[] ChapterPitches =
        {
            "Le motel, le courrier, l'appel de Sami. Bruno Moretti, devant le Vertigo.",
            "Les frères Kovac. L'un après l'autre.",
            "La salle du fond du Vertigo. Le Taureau.",
            "Qui a commandé le combat contre toi ? Derrière le casino.",
            "Une commande à ton nom. Les docks."
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

        [SerializeField, Min(0.5f)]
        [Tooltip("Durée du téléchargement de l'appli, en secondes.")]
        private float _installDuration = 6f;

        [Header("Le Vertigo")]
        [SerializeField] private Vector3 _vertigoDoor;
        [SerializeField] private GameObject _club;
        [SerializeField] private GameObject _ringGate;
        [SerializeField] private RoomZone _backRoom;

        [Header("Personnages")]
        [SerializeField] private Character[] _characters = new Character[0];

        [Header("Loyer")]
        [SerializeField, Min(0)] private int _rent = 450;
        [SerializeField, Min(1)] private int _rentEvery = 7;

        [SerializeField]
        [Tooltip("Sans écran titre (lancement direct) : la partie démarre seule — la sauvegarde si elle existe.")]
        private bool _autoStart = true;

        [SerializeField, Min(30f)]
        [Tooltip("Sans nuit de sommeil, le chapitre suivant s'annonce quand même après ce temps de jeu libre.")]
        private float _chapterPatience = 420f;

        private bool _started;
        private bool _linkConfirmed;
        private bool _installing;
        private bool _scene;
        private float _freeTime;
        private Coroutine _retry;

        public bool Started { get { return _started; } }

        /// <summary>La partie commence (nouvelle, reprise, ou un chapitre choisi).</summary>
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
        }

        private void OnEnable()
        {
            if (_director != null)
            {
                _director.StoryContractFinished += OnContractFinished;
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
        }

        private void OnDisable()
        {
            if (_director != null)
            {
                _director.StoryContractFinished -= OnContractFinished;
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
        }

        private void OnPhoneConfirmed()
        {
            if (_phone != null && _phone.Current == PhoneDevice.Screen.Lien) _linkConfirmed = true;
        }

        private void Start()
        {
            if (!_autoStart) return;
            if (PlayerProgress.HasSave) Continue();
            else NewGame();
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
        /// Commence à un chapitre : une partie neuve, avec ce qu'on aurait gagné jusque-là
        /// (argent, expérience) pour ne pas arriver nu devant un trois étoiles.
        /// </summary>
        public void StartChapter(int chapter)
        {
            if (_progress == null) return;

            _progress.ResetProgress();
            chapter = Mathf.Clamp(chapter, Prologue, Traitre);

            int[] experience = { 0, 150, 800, 1600, 2800 };
            int[] money = { 0, 150, 700, 1400, 2600 };
            if (experience[chapter] > 0) _progress.AddExperience(experience[chapter]);
            if (money[chapter] > 0) _progress.AddMoney(money[chapter], "Économies");

            if (chapter > Prologue)
            {
                _progress.SetFlag("p:reveil");
                _progress.SetFlag("p:courrier");
                _progress.SetFlag("p:appel");
                _progress.SetChapter(chapter);
            }

            // Un chapitre choisi s'annonce tout de suite (pas besoin de dormir d'abord).
            if (chapter == Kovac) _progress.SetFlag("c1:pret");
            if (chapter == Comptable) _progress.SetFlag("c3:pret");

            Begin();
        }

        private void Begin()
        {
            _started = true;
            _scene = false;
            _freeTime = 0f;

            if (_homes != null) _homes.Apply();
            if (_director != null)
            {
                _director.ClearStoryContract();
                _director.ReturnHome();
            }

            RestoreMails();
            StopAllCoroutines();
            StartCoroutine(Opening());

            Action handler = Began;
            if (handler != null) handler();
        }

        private IEnumerator Opening()
        {
            _scene = true;
            if (_fader != null) _fader.SetBlackImmediate();

            // La ville se charge par-dessus : on attend qu'elle soit là.
            float waited = 0f;
            while (!MapStreamer.Ready && waited < 30f)
            {
                waited += Time.unscaledDeltaTime;
                yield return null;
            }

            if (_director != null) _director.ReturnHome();

            int chapter = _progress != null ? _progress.Chapter : Prologue;
            bool fresh = _progress != null && !_progress.HasFlag("p:reveil");

            if (_fader != null)
            {
                _fader.ShowCard(fresh ? "ÜBER BAGARRE" : ChapterName(chapter) + (_progress != null ? "\nJour " + _progress.Day : ""));
                yield return new WaitForSeconds(fresh ? 2.6f : 1.8f);
                if (fresh)
                {
                    _fader.ShowCard("Motel Hyland, chambre 3.\nDeux heures de l'après-midi.");
                    yield return new WaitForSeconds(2.4f);
                }

                _fader.ShowCard(null);
                _fader.FadeIn(1.6f);
                yield return new WaitForSeconds(1f);
            }

            _scene = false;
            Resume();
        }

        // ------------------------------------------------------------------ où en est-on ?

        /// <summary>Relit le chapitre et les drapeaux, et remet en place ce qui doit l'être.</summary>
        private void Resume()
        {
            if (!_started || _progress == null || _director == null) return;

            int chapter = _progress.Chapter;
            _director.Paused = chapter == Prologue;

            switch (chapter)
            {
                case Prologue:
                    if (_phone != null) _phone.AppInstalled = _progress.HasFlag("p:appli");
                    if (!_progress.HasFlag("p:reveil")) StartCoroutine(Wake());
                    else if (!_progress.HasFlag("p:courrier")) Goal("Lis le courrier, sur le bureau.");
                    else if (!_progress.HasFlag("p:appel")) Ring("SAMI");
                    else if (!_progress.HasFlag("p:appli")) StartCoroutine(InstallApp());
                    else Queue("moretti", "Bruno Moretti. Le videur du Vertigo : il sort fumer à la fin de son service.");
                    break;

                case Kovac:
                    if (!_progress.HasFlag("c1:annonce"))
                    {
                        Goal("Fais des courses, entraîne-toi. Dors au motel pour passer au jour suivant.");
                    }
                    else if (!_progress.HasFlag("c1:dragan")) Queue("dragan", null);
                    else Queue("milan", null);
                    break;

                case Taureau:
                    if (!_progress.HasFlag("c2:annonce")) Ring("SAMI");
                    else AskForClub();
                    break;

                case Comptable:
                    if (!_progress.HasFlag("c3:annonce")) Goal("Rentre dormir. Demain, tu sauras qui a commandé ce combat.");
                    else Queue("comptable", null);
                    break;

                case Traitre:
                    if (!_progress.HasFlag("c4:annonce")) StartCoroutine(Announce(Traitre));
                    else Queue("sami", null);
                    break;

                default:
                    Goal(string.Empty);
                    break;
            }
        }

        private void Update()
        {
            if (_installing && _phone != null)
            {
                _phone.DownloadProgress += Time.deltaTime / Mathf.Max(0.5f, _installDuration);
            }

            if (!_started || _scene || GameMenu.IsOpen || _progress == null || _director == null) return;
            if (_director.Busy) return;

            _freeTime += Time.deltaTime;
            int chapter = _progress.Chapter;

            // Les chapitres 1 et 3 s'annoncent au réveil — ou quand on a assez traîné.
            if (chapter == Kovac && !_progress.HasFlag("c1:annonce") &&
                (_progress.HasFlag("c1:pret") || _freeTime > _chapterPatience) && !Speaking)
            {
                StartCoroutine(Announce(Kovac));
                return;
            }

            if (chapter == Comptable && !_progress.HasFlag("c3:annonce") &&
                (_progress.HasFlag("c3:pret") || _freeTime > _chapterPatience) && !Speaking)
            {
                StartCoroutine(Announce(Comptable));
                return;
            }

            // Chapitre 2 : la commande tombe une fois DANS la salle du fond.
            if (chapter == Taureau && _progress.HasFlag("c2:annonce") && !_progress.HasFlag("c2:salle") &&
                _club != null && _club.activeInHierarchy && (_backRoom == null || _backRoom.ContainsPlayer()))
            {
                StartCoroutine(InTheClub());
            }
        }

        private bool Speaking
        {
            get { return _subtitles != null && _subtitles.IsSpeaking; }
        }

        // ------------------------------------------------------------------ scènes

        private IEnumerator Wake()
        {
            _scene = true;
            _progress.SetFlag("p:reveil");
            yield return Lines(
                DialogueLine.Say("MOI", "Trois jours que j'ai rien mangé de chaud."),
                DialogueLine.Say("MOI", "Et il fait plus froid dedans que dehors."));
            Goal("Lis le courrier, sur le bureau.");
            _scene = false;
        }

        private void OnLetters(HomeRegistry.Home home)
        {
            if (!_started || _scene || _progress == null) return;

            if (_progress.Chapter == Prologue && !_progress.HasFlag("p:courrier"))
            {
                StartCoroutine(ReadLetters());
                return;
            }

            // Plus tard : ce que dit le courrier dépend de la semaine.
            int left = _rentEvery - (_progress.Day % _rentEvery);
            string rent = home != null && home.name != "Motel"
                ? "Plus de loyer. Juste des pubs, et une facture d'eau."
                : "Le loyer : " + _rent + " euros, prélevés dans " + left + (left > 1 ? " jours." : " jour.");
            Play(DialogueLine.Say("MOI", rent));
        }

        private IEnumerator ReadLetters()
        {
            _scene = true;

            // Les lettres s'ouvrent à l'écran : l'enveloppe, la lettre qui se déplie, le montant,
            // le tampon. On continue quand le joueur les repose.
            if (_letterReader != null)
            {
                Goal(string.Empty);
                bool closed = false;
                _letterReader.Open(delegate { closed = true; });
                while (!closed) yield return null;
            }

            _progress.SetFlag("p:courrier");
            yield return Lines(
                DialogueLine.Say("MOI", "Loyer. Électricité. Banque."),
                DialogueLine.Say("MOI", "Trois enveloppes et pas une qui apporte de l'argent."),
                DialogueLine.Say("MOI", "Quatre cent cinquante la semaine. J'en ai pas cent."));
            yield return new WaitForSeconds(1.5f);
            _scene = false;
            Ring("SAMI");
        }

        private void Ring(string caller)
        {
            if (_phone == null)
            {
                OnAnswered();
                return;
            }

            _phone.Available = true;
            _phone.Ring(caller);
            Goal("Le téléphone sonne : sors-le (T) et réponds.");
        }

        private void OnAnswered()
        {
            if (!_started || _progress == null || _scene) return;

            if (_progress.Chapter == Prologue && !_progress.HasFlag("p:appel")) StartCoroutine(FirstCall());
            else if (_progress.Chapter == Taureau && !_progress.HasFlag("c2:annonce")) StartCoroutine(ClubCall());
        }

        private IEnumerator FirstCall()
        {
            _scene = true;
            Goal(string.Empty);
            yield return Lines(
                DialogueLine.Say("SAMI", "T'es réveillé ? Tant pis. J'ai un truc pour toi."),
                DialogueLine.Say("SAMI", "Une appli. Über Bagarre. Les gens commandent une bagarre, toi tu la livres."),
                DialogueLine.Say("SAMI", "Tu tapes celui qu'on te dit, tu prends une photo, t'es payé le soir même."),
                DialogueLine.Say("SAMI", "C'est illégal, évidemment. Elle est sur aucun magasin. Je t'envoie le lien."),
                DialogueLine.Say("MOI", "..."),
                DialogueLine.Say("SAMI", "Réfléchis pas trop. C'est ça, ou t'es dehors en avril."));

            _progress.SetFlag("p:appel");
            _scene = false;
            StartCoroutine(InstallApp());
        }

        /// <summary>
        /// Le lien de Sami, dans Messages : c'est le joueur qui l'ouvre et qui installe. La barre
        /// de téléchargement se remplit, puis il lance l'appli lui-même depuis l'accueil.
        /// </summary>
        private IEnumerator InstallApp()
        {
            _scene = true;
            _linkConfirmed = false;

            if (_phone != null)
            {
                _phone.HangUp();
                _phone.AppInstalled = false;
                _phone.DownloadProgress = 0f;
                _phone.SetScreen(PhoneDevice.Screen.Lien);
                Goal("Téléphone (" + _phone.PhoneKeyName.ToUpperInvariant() + ") : ouvre Messages, le lien de Sami, et installe l'appli.");
                while (!_linkConfirmed) yield return null;

                _phone.SetScreen(PhoneDevice.Screen.Installation);
                Goal("Installation…");
                _installing = true;
                Play(DialogueLine.Say("MOI", "Interdite de diffusion. Évidemment."));
                while (_phone.DownloadProgress < 1f) yield return null;
                _installing = false;

                _phone.AppInstalled = true;
                _phone.SetScreen(PhoneDevice.Screen.Verrouille);
                Goal("Ouvre Über Bagarre depuis l'accueil du téléphone.");
            }

            _progress.SetFlag("p:appli");

            // Personne n'ouvre l'appli à la place du joueur : elle se présente quand il la lance.
            float waited = 0f;
            while (_phone != null && !(_phone.IsRaised && _phone.ShowingStoryScreen) && waited < 90f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            yield return Lines(
                DialogueLine.Say("APPLI", "Une course, un contrat. De une à cinq étoiles."),
                DialogueLine.Say("APPLI", "Une étoile, c'est pour apprendre. Cinq, c'est pour finir à l'hôpital."));

            _scene = false;
            Queue("moretti", "Bruno Moretti. Le videur du Vertigo : il sort fumer à la fin de son service.");
        }

        private IEnumerator Announce(int chapter)
        {
            _scene = true;
            _freeTime = 0f;

            switch (chapter)
            {
                case Kovac:
                    _progress.SetFlag("c1:annonce");
                    yield return Lines(
                        DialogueLine.Say("MOI", "Le frigo a tenu un jour et demi."),
                        DialogueLine.Say("MOI", "Et l'appli a pas arrêté de vibrer."),
                        DialogueLine.Say("APPLI", "Nouveau RDV BASTON. Deux étoiles."),
                        DialogueLine.Say("APPLI", "Deux frères, deux courses. Le client paie chaque tête."));
                    _scene = false;
                    Queue("dragan", null);
                    yield break;

                case Comptable:
                    _progress.SetFlag("c3:annonce");
                    yield return Lines(
                        DialogueLine.Say("SAMI", "J'ai demandé autour. Le combat contre toi, au Vertigo..."),
                        DialogueLine.Say("SAMI", "C'est le Comptable qui l'a commandé. Victor Sarkis. Il tient les paris, derrière le casino."),
                        DialogueLine.Say("MOI", "Un comptable qui commande des bagarres."),
                        DialogueLine.Say("SAMI", "Il commande tout ce qui rapporte. Fais gaffe à toi."),
                        DialogueLine.Say("APPLI", "RDV BASTON. Quatre étoiles. Client anonyme."));
                    _scene = false;
                    Queue("comptable", null);
                    yield break;

                case Traitre:
                    _progress.SetFlag("c4:annonce");
                    yield return new WaitForSeconds(6f);
                    Mail("Sami", "T'es où ?", "Rappelle-moi. C'est important.\nS'il te plaît.", true);
                    yield return Lines(
                        DialogueLine.Say("MOI", "Sami répond plus."),
                        DialogueLine.Say("APPLI", "RDV BASTON. Cinq étoiles. Commande passée à ton nom."),
                        DialogueLine.Say("MOI", "Sarkis. Il nettoie derrière lui."));
                    _scene = false;
                    Queue("sami", null);
                    yield break;
            }

            _scene = false;
        }

        private IEnumerator ClubCall()
        {
            _scene = true;
            Goal(string.Empty);
            yield return Lines(
                DialogueLine.Say("SAMI", "Les Kovac. Les deux. Dans le même soir."),
                DialogueLine.Say("SAMI", "T'as vu ta page ? Les gens laissent des avis sur toi, mec."),
                DialogueLine.Say("SAMI", "Ce soir, au Vertigo. La salle du fond. Ils font des combats, et les gens parient."),
                DialogueLine.Say("MOI", "Et l'appli, dans tout ça ?"),
                DialogueLine.Say("SAMI", "T'es sur place, t'attends. La commande tombe quand ils ont choisi ton adversaire."));
            if (_phone != null) _phone.HangUp();

            _progress.SetFlag("c2:annonce");
            _scene = false;
            AskForClub();
        }

        private void AskForClub()
        {
            Goal("Va au Vertigo (ville nord), descends, et passe la porte du fond.");
            if (_map != null) _map.SetWaypoint(_vertigoDoor, "LE VERTIGO", null);
        }

        private IEnumerator InTheClub()
        {
            _scene = true;
            _progress.SetFlag("c2:salle");
            if (_map != null) _map.ClearWaypoint();
            if (_ringGate != null) _ringGate.SetActive(false);

            yield return Lines(
                DialogueLine.Say("MOI", "La fumée, le son, la sueur."),
                DialogueLine.Say("MOI", "Une porte noire, un videur, pas de fenêtre. Ici, personne n'a rien vu."),
                DialogueLine.Say("MOI", "J'y suis. Maintenant, on attend que ça tombe."));
            yield return new WaitForSeconds(3f);
            yield return Lines(
                DialogueLine.Say("APPLI", "RDV BASTON. Trois étoiles. Ici, maintenant."),
                DialogueLine.Say("APPLI", "Le client est dans la salle. Il veut voir ça de près."));

            _scene = false;
            Queue("taureau", null);
        }

        // ------------------------------------------------------------------ courses de l'histoire

        private void Queue(string tag, string objective)
        {
            if (_director == null) return;
            if (_director.PendingStoryTag == tag) return;

            Character c = Find(tag);
            if (c == null || c.profile == null || c.profile.template == null)
            {
                Debug.LogWarning("[UberBagarre] Personnage de l'histoire introuvable : " + tag);
                return;
            }

            _director.QueueStoryContract(new OpenWorldDirector.StoryContract
            {
                Tag = tag,
                Profile = c.profile,
                Spot = c.spot,
                Client = ClientOf(tag),
                Reward = RewardOf(tag),
                Experience = ExperienceOf(tag),
                Goal = GoalOf(tag),
                Intro = IntroOf(tag),
                Lines = LinesOf(tag, c.profile.name)
            });

            Goal(objective ?? "Une course de l'histoire t'attend : sors le téléphone (T) et accepte-la.");
        }

        private Character Find(string tag)
        {
            for (int i = 0; i < _characters.Length; i++)
            {
                if (_characters[i] != null && _characters[i].tag == tag) return _characters[i];
            }

            return null;
        }

        private static string ClientOf(string tag)
        {
            switch (tag)
            {
                case "moretti": return "CLIENT VÉRIFIÉ";
                case "taureau": return "CLIENT VIP (DANS LA SALLE)";
                case "comptable": return "CLIENT ANONYME";
                case "sami": return "V. SARKIS";
                default: return "CLIENT RÉGULIER";
            }
        }

        private static int RewardOf(string tag)
        {
            switch (tag)
            {
                case "moretti": return 150;
                case "dragan":
                case "milan": return 320;
                case "taureau": return 600;
                case "comptable": return 1500;
                case "sami": return 3000;
                default: return 200;
            }
        }

        private static int ExperienceOf(string tag)
        {
            switch (tag)
            {
                case "moretti": return 150;
                case "dragan":
                case "milan": return 260;
                case "taureau": return 420;
                case "comptable": return 700;
                case "sami": return 1000;
                default: return 150;
            }
        }

        private static ContractGoal GoalOf(string tag)
        {
            switch (tag)
            {
                case "dragan": return ContractGoal.CasserNez;
                case "comptable": return ContractGoal.CasserJambe;
                default: return ContractGoal.Aucune;
            }
        }

        private static string IntroOf(string tag)
        {
            switch (tag)
            {
                case "moretti": return "Une étoile, pour commencer. Tu le reconnaîtras : blouson noir, crâne rasé.";
                case "milan": return "Son frère a tout vu. Milan Kovac te cherche — et il a appelé l'appli avant toi.";
                default: return null;
            }
        }

        private static DialogueLine[] LinesOf(string tag, string name)
        {
            switch (tag)
            {
                case "moretti":
                    return new[]
                    {
                        DialogueLine.Say(name, "Qu'est-ce que tu me veux, toi ?"),
                        DialogueLine.Say("MOI", "Bruno Moretti ?"),
                        DialogueLine.Say(name, "Ça dépend. Qui demande ?"),
                        DialogueLine.Say("MOI", "Personne. Quelqu'un a payé pour qu'on se parle."),
                        DialogueLine.Say(name, "Ah. T'es un de ceux-là. J'ai fini mon service... mais pour toi, je fais une heure sup'.")
                    };

                case "dragan":
                    return new[]
                    {
                        DialogueLine.Say(name, "Hé. T'es perdu, toi ?"),
                        DialogueLine.Say("MOI", "Dragan Kovac ?"),
                        DialogueLine.Say(name, "Qui c'est qui demande ?"),
                        DialogueLine.Say("MOI", "Deux étoiles. Rien de personnel.")
                    };

                case "milan":
                    return new[]
                    {
                        DialogueLine.Say(name, "C'est toi. C'est toi qui as fait ça à Dragan."),
                        DialogueLine.Say("MOI", "Il avait une commande à son nom."),
                        DialogueLine.Say(name, "Moi aussi, j'en ai une. Au tien.")
                    };

                case "taureau":
                    return new[]
                    {
                        DialogueLine.Say(name, "Le livreur. On m'a parlé de toi."),
                        DialogueLine.Say("MOI", "On m'a parlé de toi aussi. Onze combats, onze K.O."),
                        DialogueLine.Say(name, "Douze, dans deux minutes."),
                        DialogueLine.Say(name, "Ce soir, t'es pas le seul à avoir reçu une commande."),
                        DialogueLine.Say("MOI", "Alors on va être deux à être payés."),
                        DialogueLine.Say(name, "Non. Un seul.")
                    };

                case "comptable":
                    return new[]
                    {
                        DialogueLine.Say(name, "Tiens. Le livreur. Tu me coûtes cher, tu sais."),
                        DialogueLine.Say("MOI", "Le Taureau. C'est toi qui l'as commandé contre moi."),
                        DialogueLine.Say(name, "Moi, je prends les paris. Je commande rien. Mais puisque t'es là...")
                    };

                case "sami":
                    return new[]
                    {
                        DialogueLine.Say(name, "Alors c'est toi qu'ils ont envoyé."),
                        DialogueLine.Say("MOI", "T'as parié contre moi."),
                        DialogueLine.Say(name, "J'avais des dettes, mec. Plus que toi."),
                        DialogueLine.Say(name, "Et t'étais censé perdre. Tout le monde perd contre le Taureau."),
                        DialogueLine.Say("MOI", "Pas tout le monde.")
                    };
            }

            return null;
        }

        private void OnContractFinished(string tag, bool success)
        {
            if (!_started || _progress == null) return;

            if (!success)
            {
                // Une course de l'histoire ne se perd pas : elle retombe un peu plus tard.
                if (_retry != null) StopCoroutine(_retry);
                _retry = StartCoroutine(Retry(tag));
                return;
            }

            StartCoroutine(AfterWin(tag));
        }

        private IEnumerator Retry(string tag)
        {
            yield return new WaitForSeconds(25f);
            while (_director != null && (_director.Busy || _scene)) yield return null;
            _retry = null;

            Play(DialogueLine.Say("APPLI", "Le client maintient sa commande. Même sujet. Deuxième chance."));
            Resume();
        }

        private IEnumerator AfterWin(string tag)
        {
            _scene = true;

            // Laisse l'appli annoncer le paiement.
            yield return new WaitForSeconds(4f);
            while (Speaking) yield return null;

            switch (tag)
            {
                case "moretti":
                    _progress.SetChapter(Kovac);
                    yield return Lines(
                        DialogueLine.Say("MOI", "Cent cinquante euros."),
                        DialogueLine.Say("MOI", "Le loyer, c'est quatre cent cinquante la semaine."),
                        DialogueLine.Say("MOI", "Faudra en faire d'autres."),
                        DialogueLine.Say("APPLI", "Première course validée. Les commandes vont tomber, maintenant."));
                    Mail("Über Bagarre", "Première course", "Bravo. Ta page de réputation est ouverte : les clients lisent les avis.\nAstuce : l'armoire de ta chambre, c'est ton vestiaire (tenues, entraînement).", true);
                    break;

                case "dragan":
                    _progress.SetFlag("c1:dragan");
                    _scene = false;
                    Queue("milan", null);
                    yield break;

                case "milan":
                    _progress.SetChapter(Taureau);
                    yield return Lines(
                        DialogueLine.Say("MOI", "Deux frères. Deux étoiles. Deux fois payé."),
                        DialogueLine.Say("APPLI", "Les clients lisent les avis. Les tiens commencent à circuler."));
                    yield return new WaitForSeconds(6f);
                    _scene = false;
                    Ring("SAMI");
                    yield break;

                case "taureau":
                    _progress.SetChapter(Comptable);
                    if (_ringGate != null) _ringGate.SetActive(true);
                    yield return Lines(
                        DialogueLine.Say("APPLI", "Le client avait parié sur toi. Il a laissé un avis."),
                        DialogueLine.Say("MOI", "Et quelqu'un avait commandé ce combat-là contre moi."),
                        DialogueLine.Say("MOI", "Faudra savoir qui."));
                    Mail("Anonyme", "Le Taureau", "Tu as coûté beaucoup d'argent à des gens, ce soir.\nLe Taureau ne perdait jamais.\nDemande-toi qui avait parié contre toi.", true);
                    break;

                case "comptable":
                    _progress.SetChapter(Traitre);
                    yield return Lines(
                        DialogueLine.Say("SARKIS", "Attends... attends. Tu crois que c'était moi ?"),
                        DialogueLine.Say("SARKIS", "J'ai juste pris les paris. Celui qui a misé contre toi, tout ce qu'il avait..."),
                        DialogueLine.Say("SARKIS", "C'est ton pote. Sami."),
                        DialogueLine.Say("MOI", "..."));
                    _scene = false;
                    StartCoroutine(Announce(Traitre));
                    yield break;

                case "sami":
                    _progress.SetChapter(Fin);
                    _progress.SetFlag("fin");
                    _progress.ChangeReputation(10, "L'histoire est bouclée");
                    yield return Lines(
                        DialogueLine.Say("MOI", "Cinq étoiles."),
                        DialogueLine.Say("MOI", "C'est le prix d'un ami, apparemment."));
                    Mail("Über Bagarre", "Cinq étoiles", "Les clients les plus riches de la ville t'attendent.\nTon compte est au sommet : les grosses commandes tombent pour toi.", true);

                    if (_fader != null)
                    {
                        _fader.FadeOut(1.4f);
                        yield return new WaitForSeconds(1.6f);
                        _fader.ShowCard("ÜBER BAGARRE\n\nFin de l'histoire.\nLa ville, elle, continue.");
                        yield return new WaitForSeconds(4.5f);
                        _fader.ShowCard(null);
                        _fader.FadeIn(1.4f);
                    }

                    break;
            }

            _scene = false;
            _freeTime = 0f;
            Resume();
        }

        // ------------------------------------------------------------------ nuits, mort

        private string _morning;

        /// <summary>Pendant la nuit, avant la sauvegarde : le loyer, et ce que le réveil déclenche.</summary>
        private void OnSleeping()
        {
            _morning = null;
            if (!_started || _progress == null) return;

            if (_progress.Chapter == Kovac) _progress.SetFlag("c1:pret");
            if (_progress.Chapter == Comptable) _progress.SetFlag("c3:pret");

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
            if (!string.IsNullOrEmpty(_morning)) Play(DialogueLine.Say("MOI", _morning));
            _morning = null;
        }

        private void OnDied()
        {
            // La partie vient d'être rechargée : le chapitre a pu reculer.
            if (!_started) return;
            StopAllCoroutines();
            _scene = false;
            _retry = null;
            if (_director != null) _director.ClearStoryContract();
            if (_homes != null) _homes.Apply();
            RestoreMails();
            Resume();
        }

        // ------------------------------------------------------------------ utilitaires

        private void RestoreMails()
        {
            if (_progress == null) return;
            int chapter = _progress.Chapter;
            if (chapter > Prologue)
            {
                Mail("Über Bagarre", "Première course", "Bravo. Ta page de réputation est ouverte : les clients lisent les avis.\nAstuce : l'armoire de ta chambre, c'est ton vestiaire (tenues, entraînement).", false);
            }

            if (chapter > Taureau)
            {
                Mail("Anonyme", "Le Taureau", "Tu as coûté beaucoup d'argent à des gens, ce soir.\nLe Taureau ne perdait jamais.\nDemande-toi qui avait parié contre toi.", false);
            }

            if (chapter > Traitre)
            {
                Mail("Über Bagarre", "Cinq étoiles", "Les clients les plus riches de la ville t'attendent.\nTon compte est au sommet : les grosses commandes tombent pour toi.", false);
            }
        }

        private void Mail(string from, string subject, string body, bool unread)
        {
            ComputerScreen computer = _computer != null ? _computer : ComputerScreen.Instance;
            if (computer != null) computer.AddMail(from, subject, body, unread);
            if (unread && _director != null) _director.Banner("Nouveau mail : " + from + " — " + subject + " (ordinateur)", 4f);
        }

        private void Goal(string objective)
        {
            if (_objectives != null) _objectives.Set(objective ?? string.Empty);
        }

        private void Play(DialogueLine line)
        {
            if (_subtitles != null) _subtitles.Play(line);
        }

        private IEnumerator Lines(params DialogueLine[] lines)
        {
            if (_subtitles == null || lines == null || lines.Length == 0) yield break;
            _subtitles.Play(lines);
            yield return null;
            while (_subtitles.IsSpeaking) yield return null;
        }

        /// <summary>Le constructeur de la scène y déclare les personnages et le Vertigo.</summary>
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
