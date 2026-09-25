using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Enemy;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// Le prologue, écrit d'un seul tenant.
    ///
    /// C'est délibérément UN fichier et UNE liste : la séquence du prologue se lit ici de la
    /// première à la dernière étape, comme un synopsis. Éclatée en vingt objets de scène à
    /// remplir dans l'Inspector, la même chose serait illisible — pour savoir ce qui se passe
    /// après l'appel il faudrait cliquer sur six objets, et personne ne le ferait.
    ///
    /// Le prologue suit le dossier à la lettre : la maison insalubre et ses relances d'impayés,
    /// l'appel de l'ami, l'application illégale qu'on installe quand même, la course à une
    /// étoile, la fiche du suspect avec son signalement, le groupe devant le club, la bagarre,
    /// la photo pour valider, le retour à la planque.
    ///
    /// Deux principes ont décidé du reste :
    ///
    /// - **Le décor parle avant les répliques.** Le découvert bancaire, la troisième relance et
    ///   les bouteilles vides disent la situation en trois secondes. Ce que le personnage n'a
    ///   pas besoin de dire, il ne le dit pas.
    /// - **Le tutoriel s'apprend en réussissant.** Chaque consigne attend un résultat, pas un
    ///   délai. Une consigne qui s'efface toute seule après trois secondes n'a rien enseigné à
    ///   qui regardait ailleurs.
    /// </summary>
    public class PrologueDirector : MonoBehaviour
    {
        [Header("Narration")]
        [SerializeField] private StoryDirector _story;
        [SerializeField] private SubtitleDisplay _subtitles;
        [SerializeField] private ObjectiveDisplay _objectives;
        [SerializeField] private ScreenFader _fader;
        [SerializeField] private TutorialPrompt _tutorial;
        [SerializeField] private MissionBriefing _briefing;

        [Header("Joueur")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Combatant _player;
        [SerializeField] private GuardSystem _playerGuard;
        [SerializeField] private PhoneDevice _phone;
        [SerializeField] private InteractionSystem _interaction;

        [Header("Monde")]
        [SerializeField] private LocationDirector _locations;
        [SerializeField] private TargetFinder _finder;

        [SerializeField]
        [Tooltip("Le courrier sur la table du salon. C'est la premiere chose que le jeu montre, " +
                 "et la seule qui explique pourquoi le personnage va accepter.")]
        private Interactable _letters;

        [SerializeField] private Interactable _carAtHouse;
        [SerializeField] private Interactable _carAtClub;

        [Header("Cible")]
        [SerializeField] private Combatant _target;
        [SerializeField] private EnemyBrain _targetBrain;
        [SerializeField] private KnockdownSystem _targetKnockdown;
        [SerializeField] private Transform _targetTransform;

        [Header("Coups du tutoriel")]
        [SerializeField] private AttackData _straight;
        [SerializeField] private AttackData _hook;
        [SerializeField] private AttackData _headbutt;
        [SerializeField] private AttackData _shove;

        [Header("Progression")]
        [SerializeField] private PlayerProgress _progress;
        [SerializeField] private PhoneDisplay _phoneDisplay;
        [SerializeField] private PlayerCombat _playerCombat;
        [SerializeField] private PropHandler _props;

        [Header("Chapitre 1 — Deux etoiles")]
        [SerializeField] private MissionBriefing _briefingTwo;
        [SerializeField] private Interactable _carAtParking;

        [SerializeField]
        [Tooltip("La camionnette blanche : il faut s'en approcher pour declencher l'embuscade.")]
        private Transform _van;

        [SerializeField] private Combatant[] _brothers = new Combatant[0];
        [SerializeField] private EnemyBrain[] _brotherBrains = new EnemyBrain[0];
        [SerializeField] private string _parkingLocation = "Parking";

        [SerializeField]
        [Tooltip("Commencer directement au chapitre 1, avec la progression du prologue deja " +
                 "acquise. Pour tester la suite sans rejouer vingt minutes d'histoire.")]
        private bool _startAtChapterOne;

        [Header("Chapitre 2 — Trois etoiles")]
        [SerializeField] private MissionBriefing _briefingThree;

        [SerializeField]
        [Tooltip("La porte du club, cote rue.")]
        private Interactable _clubDoor;

        [SerializeField]
        [Tooltip("La porte du club, cote salle : pour ressortir.")]
        private Interactable _clubExit;

        [SerializeField] private Transform _ring;

        [SerializeField]
        [Tooltip("La barriere qui ferme la fosse. Desactivee = ouverte.")]
        private GameObject _ringGate;

        [SerializeField] private Combatant _champion;
        [SerializeField] private EnemyBrain _championBrain;
        [SerializeField] private CrowdAudio _crowd;
        [SerializeField] private Spectator[] _ringCrowd = new Spectator[0];
        [SerializeField] private string _clubLocation = "Club";

        [SerializeField]
        [Tooltip("Commencer directement au chapitre 2 (le club), avec la progression des deux " +
                 "courses precedentes deja acquise.")]
        private bool _startAtChapterTwo;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Endurance gagnee au niveau 4 (capacite SECOND SOUFFLE), en fraction.")]
        private float _secondWindBonus = 0.3f;

        [Header("Reglages")]
        [SerializeField, Min(0.5f)] private float _installDuration = 3.2f;

        [SerializeField, Min(1f)]
        [Tooltip("Points de vie du joueur pendant le prologue. Le contrat est a une etoile : la " +
                 "premiere bagarre doit s'apprendre, pas se gagner. Une reserve confortable vaut " +
                 "mieux qu'un ecran de defaite qui n'existe pas encore.")]
        private float _playerHealth = 200f;
        [SerializeField, Min(0f)]
        [Tooltip("PV gagnes au niveau 3 (capacite ENCAISSEUR).")]
        private float _toughnessBonus = 15f;

        [SerializeField]
        [Tooltip("Le loyer du mois. C'est le chiffre contre lequel le joueur compte son argent.")]
        private int _rent = 640;

        [SerializeField, Min(2f)]
        [Tooltip("Distance a la camionnette qui declenche l'embuscade des freres.")]
        private float _ambushDistance = 9f;

        [SerializeField] private string _houseLocation = "Maison";
        [SerializeField] private string _streetLocation = "Rue";

        [SerializeField]
        [Tooltip("Demarrer le prologue automatiquement. Decocher pour tester le combat seul.")]
        private bool _playOnStart = true;

        // --- compteurs de tutoriel, remplis par les evenements de combat
        private bool _confirm;
        private bool _lettersRead;
        private bool _installing;
        private int _straightHits;
        private int _hookHits;
        private int _headHits;
        private int _guardEvents;
        private bool _targetDown;
        private bool _photoValidated;
        private bool _leftHouse;
        private bool _leftClub;
        private bool _leftParking;
        private int _shoveHits;
        private int _headbuttHits;
        private int _throwHitsAtStart;
        private bool _brothersProvoked;
        private bool _toughnessApplied;
        private bool _secondWindApplied;
        private bool _enteredClub;
        private bool _leftClubInside;
        private float _baseStamina;

        // --- preuves photo : ce qui doit etre photographie, et ce qui l'a ete
        private readonly List<Transform> _photoRequired = new List<Transform>(2);
        private readonly List<Transform> _photographed = new List<Transform>(2);

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;

            if (_playerGuard != null)
            {
                _playerGuard.Blocked += OnGuardEvent;
                _playerGuard.Parried += OnGuardEvent;
            }

            if (_targetKnockdown != null) _targetKnockdown.KnockedDown += OnTargetKnockedDown;
            if (_letters != null) _letters.Activated += OnLettersRead;
            if (_carAtHouse != null) _carAtHouse.Activated += OnCarAtHouse;
            if (_carAtClub != null) _carAtClub.Activated += OnCarAtClub;
            if (_carAtParking != null) _carAtParking.Activated += OnCarAtParking;
            if (_phone != null) _phone.PhotoTaken += OnPhotoTaken;
            if (_progress != null) _progress.LeveledUp += OnLeveledUp;
            if (_clubDoor != null) _clubDoor.Activated += OnClubDoor;
            if (_clubExit != null) _clubExit.Activated += OnClubExit;
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;

            if (_playerGuard != null)
            {
                _playerGuard.Blocked -= OnGuardEvent;
                _playerGuard.Parried -= OnGuardEvent;
            }

            if (_targetKnockdown != null) _targetKnockdown.KnockedDown -= OnTargetKnockedDown;
            if (_letters != null) _letters.Activated -= OnLettersRead;
            if (_carAtHouse != null) _carAtHouse.Activated -= OnCarAtHouse;
            if (_carAtClub != null) _carAtClub.Activated -= OnCarAtClub;
            if (_carAtParking != null) _carAtParking.Activated -= OnCarAtParking;
            if (_phone != null) _phone.PhotoTaken -= OnPhotoTaken;
            if (_progress != null) _progress.LeveledUp -= OnLeveledUp;
            if (_clubDoor != null) _clubDoor.Activated -= OnClubDoor;
            if (_clubExit != null) _clubExit.Activated -= OnClubExit;
        }

        private void Start()
        {
            if (!_playOnStart) return;

            Begin();
        }

        [ContextMenu("Rejouer le prologue")]
        public void Begin()
        {
            ResetState();

            if (_player != null && _player.Health != null) _player.Health.SetMaxHealth(_playerHealth, true);

            if (_story == null) return;

            _story.Play(BuildBeats());

            if (_startAtChapterTwo) _story.JumpTo("chapitre-2");
            else if (_startAtChapterOne) _story.JumpTo("chapitre-1");
        }

        private void Update()
        {
            // Une confirmation sur le telephone est une pression sur E pendant qu'il est LEVE.
            // Le distinguer d'une interaction avec le decor evite qu'ouvrir une porte valide en
            // meme temps l'ecran affiche.
            if (_input != null && _input.InteractPressed && _phone != null && _phone.IsRaised)
            {
                _confirm = true;
            }

            if (_installing && _phone != null)
            {
                _phone.DownloadProgress += Time.deltaTime / Mathf.Max(0.5f, _installDuration);
            }

            KeepPlayerAlive();
        }

        /// <summary>
        /// Le joueur ne peut pas perdre le prologue.
        ///
        /// Ce n'est pas de la complaisance : une mort au milieu d'un tutoriel laisse la
        /// séquence bloquée sur une étape dont la condition ne se réalisera jamais, et le
        /// joueur se retrouve vivant devant un objectif impossible sans comprendre pourquoi.
        /// Tant qu'il n'y a pas d'écran de défaite, ne pas mourir est la seule option cohérente.
        /// </summary>
        private void KeepPlayerAlive()
        {
            if (_player == null || _player.Health == null || _player.Health.IsAlive) return;

            _player.Revive();

            Debug.Log("[UberBagarre] Prologue : le joueur est tombe, il est releve. " +
                      "Un prologue ne se perd pas.", this);
        }

        private void ResetState()
        {
            _confirm = false;
            _lettersRead = false;
            _installing = false;
            _straightHits = 0;
            _hookHits = 0;
            _headHits = 0;
            _guardEvents = 0;
            _targetDown = false;
            _photoValidated = false;
            _leftHouse = false;
            _leftClub = false;
            _leftParking = false;
            _shoveHits = 0;
            _headbuttHits = 0;
            _throwHitsAtStart = 0;
            _brothersProvoked = false;
            _toughnessApplied = false;
            _secondWindApplied = false;
            _enteredClub = false;
            _leftClubInside = false;

            // L'endurance de depart est memorisee une fois : c'est elle que le niveau 4 augmente,
            // et elle qu'une partie relancee doit retrouver.
            if (_player != null && _player.Stamina != null)
            {
                if (_baseStamina <= 0f) _baseStamina = _player.Stamina.MaxStamina;
                _player.Stamina.MaxStamina = _baseStamina;
                _player.Stamina.Refill();
            }

            if (_championBrain != null) _championBrain.enabled = false;
            if (_ringGate != null) _ringGate.SetActive(true);
            if (_targetTransform != null) _targetTransform.gameObject.SetActive(true);

            if (_clubDoor != null)
            {
                _clubDoor.ResetUsage();
                _clubDoor.SetAvailable(false);
            }

            if (_clubExit != null)
            {
                _clubExit.ResetUsage();
                _clubExit.SetAvailable(false);
            }
            _photoRequired.Clear();
            _photographed.Clear();

            if (_progress != null) _progress.ResetProgress();

            // Le coup de tete est une CAPACITE : il se gagne a la fin du prologue.
            if (_playerCombat != null) _playerCombat.HeadbuttUnlocked = false;

            if (_phoneDisplay != null)
            {
                _phoneDisplay.Briefing = _briefing;
                _phoneDisplay.UnlockedAbility = null;
                _phoneDisplay.PhotoCounter = null;
            }

            for (int i = 0; i < _brotherBrains.Length; i++)
            {
                if (_brotherBrains[i] != null) _brotherBrains[i].enabled = false;
            }

            if (_carAtParking != null)
            {
                _carAtParking.ResetUsage();
                _carAtParking.SetAvailable(false);
            }

            if (_phone != null)
            {
                _phone.DownloadProgress = 0f;
                _phone.SetScreen(PhoneDevice.Screen.Verrouille);
                _phone.Available = true;
                _phone.Lower();
            }

            if (_targetBrain != null) _targetBrain.enabled = false;
            if (_finder != null) _finder.Searching = false;
            if (_tutorial != null) _tutorial.Hide();

            if (_carAtHouse != null)
            {
                _carAtHouse.ResetUsage();
                _carAtHouse.SetAvailable(false);
            }

            if (_carAtClub != null)
            {
                _carAtClub.ResetUsage();
                _carAtClub.SetAvailable(false);
            }

            if (_letters != null)
            {
                _letters.ResetUsage();
                _letters.SetAvailable(true);
            }
        }

        // ------------------------------------------------------------------ le scenario

        private List<StoryBeat> BuildBeats()
        {
            List<StoryBeat> beats = new List<StoryBeat>();
            string target = _briefing != null ? _briefing.TargetName : "LA CIBLE";
            string friend = _briefing != null ? _briefing.FriendName : "SAMI";
            string clothing = _briefing != null ? _briefing.TargetClothing : "";

            // ---------------------------------------------------------- la planque
            beats.Add(new StoryBeat("reveil")
                .Freeze()
                .Wait(2.6f)
                .Enter(delegate
                {
                    if (_fader == null) return;
                    _fader.SetBlackImmediate();
                    _fader.ShowCard("LA PLANQUE\n02:47");
                })
                .Exit(delegate
                {
                    if (_fader != null) _fader.FadeIn(1.8f);
                }));

            beats.Add(new StoryBeat("premiere-nuit")
                .Wait(2f)
                .Say("MOI", "Trois jours que j'ai rien mangé de chaud.")
                .Say("MOI", "Et il fait plus froid dedans que dehors."));

            beats.Add(new StoryBeat("courrier")
                .Goal("Regarde le courrier sur la table")
                .Until(delegate { return _lettersRead; }));

            beats.Add(new StoryBeat("courrier-lu")
                .Say("MOI", "Loyer. Électricité. Banque.")
                .Say("MOI", "Trois enveloppes et pas une qui apporte de l'argent."));

            // ---------------------------------------------------------- l'appel
            beats.Add(new StoryBeat("sonnerie")
                .Goal("Réponds au téléphone")
                .Enter(delegate
                {
                    if (_phone != null) _phone.Ring(friend);
                })
                .Until(delegate
                {
                    return _phone != null && _phone.Current == PhoneDevice.Screen.EnAppel;
                }));

            // Les quatre repliques du dossier, pas une de plus : l'explication de l'appli tient
            // en quatre phrases ou elle ne tient pas.
            beats.Add(new StoryBeat("appel")
                .Say(friend, "T'es réveillé ? Tant pis. J'ai un truc pour toi.")
                .Say(friend, "Une appli. Fighting delivery. Les gens commandent une bagarre, toi tu la livres.")
                .Say(friend, "Tu tapes celui qu'on te dit, tu prends une photo, t'es payé le soir même.")
                .Say(friend, "C'est illégal, évidemment. Elle est sur aucun magasin. Je t'envoie le lien.")
                .Say("MOI", "...")
                .Say(friend, "Réfléchis pas trop. C'est ça, ou t'es dehors en avril.")
                .Exit(delegate
                {
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Lien);
                }));

            beats.Add(new StoryBeat("lien")
                .Goal("Installe l'application")
                .Enter(delegate
                {
                    _confirm = false;
                    if (_phone != null) _phone.Raise();
                })
                .Until(delegate { return _confirm; })
                .Exit(delegate
                {
                    _installing = true;
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Installation);
                }));

            beats.Add(new StoryBeat("installation")
                .Goal("Installation…")
                .Say("MOI", "Interdite de diffusion. Évidemment.")
                .Until(delegate { return _phone != null && _phone.DownloadProgress >= 1f; })
                .Exit(delegate
                {
                    _installing = false;
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Accueil);
                }));

            // ---------------------------------------------------------- l'application
            beats.Add(new StoryBeat("appli")
                .Goal("Accepte la course")
                .Say("APPLI", "Une course, un contrat. De une à cinq étoiles.")
                .Say("APPLI", "Une étoile, c'est pour apprendre. Cinq, c'est pour finir à l'hôpital.")
                .Say("APPLI", "Un RDV BASTON t'attend ce soir. Une étoile.")
                .Enter(delegate { _confirm = false; })
                .Until(delegate { return _confirm; })
                .Exit(delegate
                {
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Cible);
                }));

            beats.Add(new StoryBeat("fiche")
                .Goal("Lis la fiche du sujet")
                .Say("APPLI", "Sujet : " + target + ". Videur. Une étoile, mais il rend les coups.")
                .Say("APPLI", "Signalement : " + clothing + ".")
                .Say("MOI", "Retiens ça. Ils seront plusieurs devant le club.")
                .Enter(delegate { _confirm = false; })
                .Wait(1.5f));

            beats.Add(new StoryBeat("depart")
                .Goal("Rejoins ta voiture")
                .Enter(delegate
                {
                    if (_carAtHouse != null) _carAtHouse.SetAvailable(true);
                    if (_phone != null) _phone.Lower();
                })
                .Until(delegate { return _leftHouse; }));

            // ---------------------------------------------------------- la route
            beats.Add(new StoryBeat("route")
                .Freeze()
                .Wait(4.4f)
                .Enter(delegate
                {
                    if (_phone != null) _phone.Available = false;
                    if (_interaction != null) _interaction.Active = false;
                    if (_fader == null) return;

                    _fader.FadeOut(1f);
                    _fader.ShowCard("20 MINUTES PLUS TARD");
                })
                .Exit(delegate
                {
                    if (_locations != null) _locations.GoTo(_streetLocation);
                    if (_phone != null)
                    {
                        _phone.Available = true;
                        _phone.SetScreen(PhoneDevice.Screen.Mission);
                    }

                    if (_interaction != null) _interaction.Active = true;
                    if (_fader != null) _fader.FadeIn(1.2f);
                }));

            // La carte est affichee APRES le fondu au noir : une seconde d'ecran noir muet
            // ressemble a un chargement, la meme seconde avec deux mots ressemble a une ellipse.
            beats.Add(new StoryBeat("arrivee")
                .Wait(1.4f)
                .Enter(delegate
                {
                    if (_fader != null) _fader.ShowCard(null);
                })
                .Say("MOI", "Le Vertigo. Deux heures du matin.")
                .Say("MOI", clothing + ". C'est tout ce que j'ai."));

            // ---------------------------------------------------------- l'identification
            beats.Add(new StoryBeat("recherche")
                .Goal("Trouve " + target + " dans le groupe")
                .Enter(delegate
                {
                    if (_finder != null)
                    {
                        _finder.Collect();
                        _finder.Searching = true;
                    }
                })
                .Until(delegate { return _finder != null && _finder.IsIdentified; }));

            beats.Add(new StoryBeat("identifie")
                .Say("MOI", "C'est lui.")
                .Wait(0.8f)
                .Exit(delegate
                {
                    if (_targetBrain != null) _targetBrain.enabled = true;
                }));

            // ---------------------------------------------------------- la bagarre
            beats.Add(Lesson("tuto-direct", "CLIC GAUCHE", "Place deux directs", 2,
                delegate { return _straightHits; }, TargetDefeated)
                .Goal("Mets " + target + " K.O.")
                .Say("MOI", "Bon."));

            beats.Add(Lesson("tuto-crochet", "CLIC DROIT", "Un crochet : plus lent, plus lourd", 1,
                delegate { return _hookHits; }, TargetDefeated));

            beats.Add(Lesson("tuto-garde", "CTRL GAUCHE", "Garde. Au bon moment, c'est une parade", 1,
                delegate { return _guardEvents; }, TargetDefeated));

            // La reference au dossier : le systeme de degats par zone, facon Skate 3. La tete
            // vaut le double, et c'est la seule facon de finir vite.
            beats.Add(Lesson("tuto-tete", "VISE HAUT", "La tête encaisse le double", 1,
                delegate { return _headHits; }, TargetDefeated));

            // La condition est la MORT, pas la chute. Un adversaire au sol se releve — c'est
            // tout l'interet du systeme de chute — donc terminer l'etape sur une chute ferait
            // passer a la photo pendant qu'il se remet debout. Le K.O. du dossier, c'est le
            // corps qui reste par terre.
            beats.Add(new StoryBeat("ko")
                .Goal("Mets " + target + " K.O.")
                .Enter(delegate
                {
                    if (_tutorial != null) _tutorial.Hide();
                })
                .Until(TargetKnockedOut));

            // ---------------------------------------------------------- la preuve
            beats.Add(new StoryBeat("photo")
                .Goal("Envoie la photo au client")
                .Say("APPLI", "Preuve exigée. Photo du sujet au sol.")
                .Enter(delegate
                {
                    RequirePhotos(_targetTransform);

                    // Le rappel de touche sert ici de mode d'emploi, pas d'exercice : le
                    // compteur est a zero, la consigne reste affichee tant que la photo n'est
                    // pas prise. Sans elle, « envoie la photo » ne dit pas quelle touche.
                    if (_tutorial != null) _tutorial.Show("CLIC GAUCHE", "T pour sortir le téléphone, puis cadre-le", 0);

                    if (_phone == null) return;
                    _phone.SetScreen(PhoneDevice.Screen.Photo);
                    _phone.Raise();
                })
                .Until(delegate { return _photoValidated; })
                .Exit(delegate
                {
                    if (_tutorial != null) _tutorial.Hide();
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Valide);
                }));

            beats.Add(new StoryBeat("validee")
                .Say("APPLI", "Course validée. " + (_briefing != null ? _briefing.Reward : 150) + " euros.")
                .Say("APPLI", "Le client a laissé un avis.")
                .Enter(delegate { Pay(_briefing); })
                .Wait(2.5f));

            // La premiere course fait passer le premier palier : le joueur doit VOIR le systeme
            // de progression exister avant de rentrer, pas le decouvrir dans un menu.
            beats.Add(new StoryBeat("niveau")
                .Say("APPLI", "Première course. Ta page de réputation est ouverte.")
                .Say("APPLI", "Niveau 2. Capacité débloquée : coup de tête.")
                .Enter(delegate
                {
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Profil);
                })
                .Wait(2f));

            beats.Add(new StoryBeat("retour")
                .Goal("Retourne à ta voiture")
                .Enter(delegate
                {
                    if (_carAtClub != null) _carAtClub.SetAvailable(true);
                    if (_phone != null) _phone.Lower();
                })
                .Until(delegate { return _leftClub; }));

            beats.Add(new StoryBeat("rentree")
                .Freeze()
                .Wait(4f)
                .Enter(delegate
                {
                    if (_phone != null) _phone.Available = false;
                    if (_interaction != null) _interaction.Active = false;
                    if (_fader != null)
                    {
                        _fader.FadeOut(1f);
                        _fader.ShowCard("LA PLANQUE\n03:26");
                    }
                })
                .Exit(delegate
                {
                    if (_locations != null) _locations.GoTo(_houseLocation);
                    if (_phone != null) _phone.Available = true;
                    if (_interaction != null) _interaction.Active = true;
                    if (_fader != null) _fader.FadeIn(1.4f);
                }));

            beats.Add(new StoryBeat("fin")
                .Wait(1.2f)
                .Say("MOI", "Cent cinquante euros.")
                .Say("MOI", "Le loyer, c'est six cent quarante.")
                .Say("MOI", "Faudra en faire d'autres.")
                .Exit(delegate
                {
                    if (_fader == null) return;
                    _fader.FadeOut(2f);
                    _fader.ShowCard("ÜBER BAGARRE\nPROLOGUE");
                }));

            // Pas de retour a l'image entre les deux cartons : le noir du prologue devient le
            // noir du chapitre. Un fondu d'une seconde entre deux ecrans noirs ressemble a un
            // chargement rate.
            beats.Add(new StoryBeat("carton-final")
                .Freeze()
                .Wait(4.5f)
                .Exit(delegate
                {
                    if (_fader != null) _fader.ShowCard(null);
                }));

            beats.Add(new StoryBeat("entracte")
                .Freeze()
                .Wait(1f));

            AddChapterOne(beats, friend);

            return beats;
        }

        // ------------------------------------------------------------------ chapitre 1

        /// <summary>
        /// Chapitre 1 : deux étoiles, deux frères.
        ///
        /// Le chapitre enseigne ce que le prologue ne pouvait pas enseigner, parce qu'il faut
        /// être DEUX en face pour que ça ait un sens : bousculer pour faire de la place, cogner de
        /// la tête quand on est collé, et se servir du décor. Le parking est rempli de bouteilles,
        /// de caisses et de fûts pour ça — tout y bouge quand on le frappe.
        ///
        /// La récompense du prologue (le coup de tête) est utilisée ici même, dans la première
        /// bagarre qui suit : une capacité débloquée qu'on n'essaie pas tout de suite est une
        /// capacité oubliée.
        /// </summary>
        private void AddChapterOne(List<StoryBeat> beats, string friend)
        {
            string targets = _briefingTwo != null ? _briefingTwo.TargetName : "LES FRÈRES KOVAC";
            string clothing = _briefingTwo != null ? _briefingTwo.TargetClothing : "";
            string elder = BrotherName(0, "DRAGAN");
            string younger = BrotherName(1, "MILAN");

            beats.Add(new StoryBeat("chapitre-1")
                .Freeze()
                .Wait(3.4f)
                .Enter(delegate
                {
                    EnsureChapterOneState();

                    if (_fader == null) return;
                    _fader.SetBlackImmediate();
                    _fader.ShowCard("CHAPITRE 1\nDEUX ÉTOILES");
                })
                .Exit(delegate
                {
                    if (_fader == null) return;
                    _fader.ShowCard(null);
                    _fader.FadeIn(1.6f);
                }));

            beats.Add(new StoryBeat("deux-jours")
                .Wait(1.6f)
                .Say("MOI", "Deux jours. Le frigo a tenu un jour et demi.")
                .Say("MOI", "Et l'appli a pas arrêté de vibrer."));

            // ---------------------------------------------------------- la commande
            beats.Add(new StoryBeat("notification")
                .Goal("Accepte le RDV BASTON")
                .Say("APPLI", "Nouveau RDV BASTON. Deux étoiles.")
                .Say("APPLI", "Deux sujets, une seule course. Le client paie le double.")
                .Enter(delegate
                {
                    _confirm = false;

                    if (_phoneDisplay != null) _phoneDisplay.Briefing = _briefingTwo;
                    if (_phone == null) return;

                    _phone.Available = true;
                    _phone.SetScreen(PhoneDevice.Screen.Accueil);
                    _phone.Raise();
                })
                .Until(delegate { return _confirm; })
                .Exit(delegate
                {
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Cible);
                }));

            beats.Add(new StoryBeat("fiche-2")
                .Goal("Lis la fiche des sujets")
                .Say("APPLI", "Sujets : " + targets + ". " + clothing + ".")
                .Say("APPLI", "Parking du Vertigo, niveau moins un. Ils déchargent une camionnette blanche.")
                .Say("MOI", "Deux frères. Ça frappe en famille, ce genre-là.")
                .Say("MOI", "Faudra pas rester entre les deux.")
                .Wait(1.5f));

            beats.Add(new StoryBeat("depart-2")
                .Goal("Rejoins ta voiture")
                .Enter(delegate
                {
                    _leftHouse = false;

                    if (_carAtHouse != null)
                    {
                        _carAtHouse.ResetUsage();
                        _carAtHouse.SetAvailable(true);
                    }

                    if (_phone != null) _phone.Lower();
                })
                .Until(delegate { return _leftHouse; }));

            // ---------------------------------------------------------- le parking
            beats.Add(new StoryBeat("route-2")
                .Freeze()
                .Wait(4.4f)
                .Enter(delegate
                {
                    if (_phone != null) _phone.Available = false;
                    if (_interaction != null) _interaction.Active = false;
                    if (_fader == null) return;

                    _fader.FadeOut(1f);
                    _fader.ShowCard("PARKING DU VERTIGO\n23:52");
                })
                .Exit(delegate
                {
                    if (_locations != null) _locations.GoTo(_parkingLocation);
                    if (_phone != null)
                    {
                        _phone.Available = true;
                        _phone.SetScreen(PhoneDevice.Screen.Mission);
                    }

                    if (_interaction != null) _interaction.Active = true;
                    if (_fader != null) _fader.FadeIn(1.2f);
                }));

            beats.Add(new StoryBeat("arrivee-2")
                .Wait(1.4f)
                .Enter(delegate
                {
                    if (_fader != null) _fader.ShowCard(null);
                })
                .Say("MOI", "Niveau moins un. Ça sent l'huile et la pisse.")
                .Say("MOI", "La camionnette blanche, au fond. Ils sont à côté."));

            // L'embuscade part de la camionnette OU du premier coup : un joueur qui ouvre les
            // hostilites de loin, avec une bouteille, ne doit pas attendre que l'histoire le
            // rattrape.
            beats.Add(new StoryBeat("approche")
                .Goal("Approche-toi de la camionnette")
                .Until(delegate { return _brothersProvoked || PlayerNear(_van, _ambushDistance); }));

            beats.Add(new StoryBeat("embuscade")
                .Goal("Mets les deux frères K.O.")
                .Say(elder, "Hé. T'es perdu, toi ?")
                .Say(younger, "Regarde-le. Il a une tête d'appli.")
                .Say("MOI", "Rien de personnel.")
                .Enter(delegate
                {
                    SetBrothersActive(true);
                    _throwHitsAtStart = _props != null ? _props.ThrowHits : 0;
                }));

            // ---------------------------------------------------------- ce qui change a deux
            beats.Add(Lesson("tuto-bousculade", "X", "Bouscule : ça les écarte et ça casse leur garde", 1,
                delegate { return _shoveHits; }, BrothersKnockedOut)
                .Goal("Mets les deux frères K.O."));

            beats.Add(Lesson("tuto-coup-de-tete", "G", "Coup de tête : de tout près, ça sonne", 1,
                delegate { return _headbuttHits; }, BrothersKnockedOut)
                .Goal("Mets les deux frères K.O."));

            beats.Add(Lesson("tuto-objet", "E  puis  CLIC GAUCHE", "Ramasse une bouteille et lance-la", 1,
                delegate { return _props != null ? _props.ThrowHits - _throwHitsAtStart : 1; },
                BrothersKnockedOut)
                .Goal("Mets les deux frères K.O."));

            beats.Add(new StoryBeat("ko-2")
                .Goal("Mets les deux frères K.O.")
                .Enter(delegate
                {
                    if (_tutorial != null) _tutorial.Hide();
                })
                .Until(BrothersKnockedOut));

            // ---------------------------------------------------------- deux preuves
            beats.Add(new StoryBeat("photo-2")
                .Goal("Photographie les deux frères")
                .Say("APPLI", "Preuve exigée. Une photo par sujet.")
                .Enter(delegate
                {
                    Transform[] proofs = new Transform[_brothers.Length];
                    for (int i = 0; i < _brothers.Length; i++)
                    {
                        proofs[i] = _brothers[i] != null ? _brothers[i].transform : null;
                    }

                    RequirePhotos(proofs);

                    if (_tutorial != null) _tutorial.Show("CLIC GAUCHE", "T pour le téléphone. Un cliché par frère", 0);

                    if (_phone == null) return;
                    _phone.SetScreen(PhoneDevice.Screen.Photo);
                    _phone.Raise();
                })
                .Until(delegate { return _photoValidated; })
                .Exit(delegate
                {
                    if (_tutorial != null) _tutorial.Hide();
                    if (_phoneDisplay != null) _phoneDisplay.PhotoCounter = null;
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Valide);
                }));

            beats.Add(new StoryBeat("validee-2")
                .Say("APPLI", "Course validée. " + (_briefingTwo != null ? _briefingTwo.Reward : 320) + " euros.")
                .Say("APPLI", "Deux sujets, deux preuves. Le client a laissé un avis.")
                .Enter(delegate { Pay(_briefingTwo); })
                .Wait(2.5f));

            beats.Add(new StoryBeat("niveau-3")
                .Say("APPLI", "Niveau 3. Capacité débloquée : encaisseur.")
                .Say("APPLI", "Les clients lisent les avis. Les tiens commencent à circuler.")
                .Enter(delegate
                {
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Profil);
                })
                .Wait(2f));

            beats.Add(new StoryBeat("retour-2")
                .Goal("Retourne à ta voiture")
                .Enter(delegate
                {
                    _leftParking = false;

                    if (_carAtParking != null)
                    {
                        _carAtParking.ResetUsage();
                        _carAtParking.SetAvailable(true);
                    }

                    if (_phone != null) _phone.Lower();
                })
                .Until(delegate { return _leftParking; }));

            beats.Add(new StoryBeat("rentree-2")
                .Freeze()
                .Wait(4f)
                .Enter(delegate
                {
                    if (_phone != null) _phone.Available = false;
                    if (_interaction != null) _interaction.Active = false;
                    if (_fader != null)
                    {
                        _fader.FadeOut(1f);
                        _fader.ShowCard("LA PLANQUE\n01:05");
                    }
                })
                .Exit(delegate
                {
                    SetBrothersActive(false);

                    if (_locations != null) _locations.GoTo(_houseLocation);
                    if (_phone != null) _phone.Available = true;
                    if (_interaction != null) _interaction.Active = true;
                    if (_fader != null) _fader.FadeIn(1.4f);
                }));

            // Le compte est calcule a l'entree, pas ecrit en dur : c'est la vraie somme du
            // joueur, et c'est elle qui doit tomber a cote du loyer.
            beats.Add(new StoryBeat("compte")
                .Wait(1.2f)
                .Enter(delegate
                {
                    int money = _progress != null ? _progress.Money : 0;
                    int missing = Mathf.Max(0, _rent - money);

                    List<DialogueLine> lines = new List<DialogueLine>(3);
                    lines.Add(DialogueLine.Say("MOI", money + " euros sur la table."));

                    if (missing > 0)
                    {
                        lines.Add(DialogueLine.Say("MOI", "Il en manque " + missing + " pour le loyer."));
                    }
                    else
                    {
                        lines.Add(DialogueLine.Say("MOI", "Le loyer est payé. Pour la première fois depuis l'hiver."));
                    }

                    if (_subtitles != null) _subtitles.Play(lines);
                }));

            beats.Add(new StoryBeat("sonnerie-2")
                .Goal("Réponds au téléphone")
                .Enter(delegate
                {
                    if (_phone != null) _phone.Ring(friend);
                })
                .Until(delegate
                {
                    return _phone != null && _phone.Current == PhoneDevice.Screen.EnAppel;
                }));

            beats.Add(new StoryBeat("appel-2")
                .Say(friend, "Les Kovac. Les deux. Dans le même soir.")
                .Say(friend, "T'as vu ta page ? Les gens laissent des avis sur toi, mec.")
                .Say(friend, "Vendredi, au Vertigo. La salle du fond. Ils font des combats, et les gens parient.")
                .Say("MOI", "Et l'appli, dans tout ça ?")
                .Say(friend, "T'es sur place, t'attends. La commande tombe quand ils ont choisi ton adversaire.")
                .Exit(delegate
                {
                    if (_phone != null) _phone.HangUp();
                    if (_fader == null) return;

                    _fader.FadeOut(2f);
                    _fader.ShowCard("ÜBER BAGARRE\nCHAPITRE 1");
                }));

            beats.Add(new StoryBeat("carton-chapitre-1")
                .Freeze()
                .Wait(4.5f)
                .Exit(delegate
                {
                    if (_fader != null) _fader.ShowCard(null);
                    if (_phone != null) _phone.Lower();
                }));

            beats.Add(new StoryBeat("entracte-2")
                .Freeze()
                .Wait(1f));

            AddChapterTwo(beats, friend);
        }

        // ------------------------------------------------------------------ chapitre 2

        /// <summary>
        /// Chapitre 2 : trois étoiles, le Vertigo de l'intérieur, la fosse.
        ///
        /// La règle du jeu y est poussée au bout : le joueur se rend sur place SANS contrat, et
        /// le combat ne commence que lorsque la commande tombe sur le téléphone et qu'il l'a
        /// acceptée. Tant qu'elle n'est pas là, le champion attend dans la fosse et la salle
        /// danse. C'est la course qui déclenche la bagarre, jamais le décor.
        ///
        /// C'est aussi le premier combat devant un PUBLIC : la foule regarde, rugit sur les
        /// coups, fait mur autour de la fosse. Le prologue apprenait à frapper, le chapitre 1 à
        /// se battre à deux contre un ; celui-ci apprend qu'on se bat pour quelqu'un.
        /// </summary>
        private void AddChapterTwo(List<StoryBeat> beats, string friend)
        {
            string champion = _briefingThree != null ? _briefingThree.TargetName : "LE TAUREAU";
            string clothing = _briefingThree != null ? _briefingThree.TargetClothing : "";

            beats.Add(new StoryBeat("chapitre-2")
                .Freeze()
                .Wait(3.4f)
                .Enter(delegate
                {
                    EnsureChapterTwoState();

                    if (_fader == null) return;
                    _fader.SetBlackImmediate();
                    _fader.ShowCard("CHAPITRE 2\nTROIS ÉTOILES");
                })
                .Exit(delegate
                {
                    if (_fader == null) return;
                    _fader.ShowCard(null);
                    _fader.FadeIn(1.6f);
                }));

            beats.Add(new StoryBeat("vendredi")
                .Wait(1.4f)
                .Say("MOI", "Vendredi. Pas de commande, pas de fiche. Juste une adresse.")
                .Say("MOI", "Faut être sur place quand ça tombe."));

            beats.Add(new StoryBeat("depart-3")
                .Goal("Rejoins ta voiture")
                .Enter(delegate
                {
                    _leftHouse = false;

                    if (_carAtHouse != null)
                    {
                        _carAtHouse.ResetUsage();
                        _carAtHouse.SetAvailable(true);
                    }

                    if (_phone != null) _phone.Lower();
                })
                .Until(delegate { return _leftHouse; }));

            beats.Add(new StoryBeat("route-3")
                .Freeze()
                .Wait(4.4f)
                .Enter(delegate
                {
                    if (_phone != null) _phone.Available = false;
                    if (_interaction != null) _interaction.Active = false;
                    if (_fader == null) return;

                    _fader.FadeOut(1f);
                    _fader.ShowCard("LE VERTIGO\n00:40");
                })
                .Exit(delegate
                {
                    // Trois nuits ont passe : le videur du prologue n'est plus etendu devant
                    // l'entree.
                    if (_targetTransform != null) _targetTransform.gameObject.SetActive(false);

                    if (_locations != null) _locations.GoTo(_streetLocation);
                    if (_phone != null)
                    {
                        _phone.Available = true;
                        _phone.SetScreen(PhoneDevice.Screen.Verrouille);
                    }

                    if (_interaction != null) _interaction.Active = true;
                    if (_fader != null) _fader.FadeIn(1.2f);
                }));

            beats.Add(new StoryBeat("arrivee-3")
                .Wait(1.2f)
                .Enter(delegate
                {
                    if (_fader != null) _fader.ShowCard(null);

                    if (_clubDoor == null) return;
                    _clubDoor.ResetUsage();
                    _clubDoor.SetAvailable(true);
                })
                .Say("MOI", "Le Vertigo. La dernière fois, je suis resté dehors.")
                .Say("MOI", "Ce soir, je rentre par la porte."));

            beats.Add(new StoryBeat("entree-club")
                .Goal("Entre dans le club")
                .Until(delegate { return _enteredClub; }));

            beats.Add(new StoryBeat("dedans")
                .Freeze()
                .Wait(1.3f)
                .Enter(delegate
                {
                    if (_interaction != null) _interaction.Active = false;
                    if (_fader != null) _fader.FadeOut(0.6f);
                })
                .Exit(delegate
                {
                    if (_locations != null) _locations.GoTo(_clubLocation);
                    if (_interaction != null) _interaction.Active = true;
                    if (_fader != null) _fader.FadeIn(1f);
                }));

            beats.Add(new StoryBeat("salle")
                .Goal("Rejoins la fosse, au fond à droite")
                .Say("MOI", "La fumée, le son, la sueur.")
                .Say("MOI", "La salle du fond, c'est là où il y a du monde. Derrière les barrières.")
                .Until(delegate { return PlayerNear(_ring, 8.5f); }));

            beats.Add(new StoryBeat("attente")
                .Goal("Attends la commande")
                .Say("MOI", "J'y suis. Maintenant, on attend que ça tombe.")
                .Wait(3f));

            // LA règle : la bagarre commence quand la commande est reçue ET acceptée.
            beats.Add(new StoryBeat("commande-3")
                .Goal("Accepte le RDV BASTON")
                .Say("APPLI", "RDV BASTON. Trois étoiles. Ici, maintenant.")
                .Say("APPLI", "Le client est dans la salle. Il veut voir ça de près.")
                .Enter(delegate
                {
                    _confirm = false;

                    if (_phoneDisplay != null) _phoneDisplay.Briefing = _briefingThree;
                    if (_phone == null) return;

                    _phone.Available = true;
                    _phone.SetScreen(PhoneDevice.Screen.Accueil);
                    _phone.Raise();
                })
                .Until(delegate { return _confirm; })
                .Exit(delegate
                {
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Cible);
                }));

            beats.Add(new StoryBeat("fiche-3")
                .Goal("Lis la fiche du sujet")
                .Say("APPLI", "Sujet : " + champion + ". Champion de la fosse. Onze combats, onze K.O.")
                .Say("APPLI", "Signalement : " + clothing + ".")
                .Say("MOI", "Pas besoin de le chercher. Il m'attend.")
                .Wait(1.2f)
                .Exit(delegate
                {
                    if (_phone != null)
                    {
                        _phone.SetScreen(PhoneDevice.Screen.Mission);
                        _phone.Lower();
                    }

                    // La barriere s'ouvre, la salle comprend.
                    if (_ringGate != null) _ringGate.SetActive(false);
                    if (_crowd != null) _crowd.Roar(0.5f);
                    CheerRing(0.5f, 1.2f);
                }));

            beats.Add(new StoryBeat("entree-fosse")
                .Goal("Entre dans la fosse")
                .Until(delegate { return PlayerNear(_ring, 3.2f); }));

            beats.Add(new StoryBeat("gong")
                .Goal("Mets " + champion + " K.O.")
                .Say(champion, "Le livreur. On m'a parlé de toi.")
                .Say(champion, "Ce soir, t'es pas le seul à avoir reçu une commande.")
                .Say("MOI", "Alors on va être deux à être payés.")
                .Enter(delegate
                {
                    // La barriere se referme derriere le joueur, et le champion avance.
                    if (_ringGate != null) _ringGate.SetActive(true);
                    if (_championBrain != null) _championBrain.enabled = true;
                    if (_crowd != null) _crowd.Roar(1f);
                    CheerRing(0.9f, 2.5f);
                }));

            beats.Add(new StoryBeat("ko-3")
                .Goal("Mets " + champion + " K.O.")
                .Until(ChampionKnockedOut));

            beats.Add(new StoryBeat("photo-3")
                .Goal("Envoie la photo au client")
                .Say("APPLI", "Preuve exigée. Photo du sujet au sol.")
                .Enter(delegate
                {
                    RequirePhotos(_champion != null ? _champion.transform : null);

                    if (_tutorial != null) _tutorial.Show("CLIC GAUCHE", "T pour le téléphone, puis cadre-le", 0);

                    if (_phone == null) return;
                    _phone.SetScreen(PhoneDevice.Screen.Photo);
                    _phone.Raise();
                })
                .Until(delegate { return _photoValidated; })
                .Exit(delegate
                {
                    if (_tutorial != null) _tutorial.Hide();
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Valide);
                }));

            beats.Add(new StoryBeat("validee-3")
                .Say("APPLI", "Course validée. " + (_briefingThree != null ? _briefingThree.Reward : 600) + " euros.")
                .Say("APPLI", "Le client avait parié sur toi. Il a laissé un avis.")
                .Enter(delegate { Pay(_briefingThree); })
                .Wait(2.5f));

            beats.Add(new StoryBeat("niveau-4")
                .Say("APPLI", "Niveau 4. Capacité débloquée : second souffle.")
                .Say("APPLI", "Trois étoiles au compteur. Les grosses commandes vont arriver.")
                .Enter(delegate
                {
                    if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Profil);
                })
                .Wait(2f));

            beats.Add(new StoryBeat("sortie-3")
                .Goal("Sors du club")
                .Enter(delegate
                {
                    _leftClubInside = false;

                    if (_ringGate != null) _ringGate.SetActive(false);
                    if (_phone != null) _phone.Lower();

                    if (_clubExit == null) return;
                    _clubExit.ResetUsage();
                    _clubExit.SetAvailable(true);
                })
                .Until(delegate { return _leftClubInside; }));

            beats.Add(new StoryBeat("rentree-3")
                .Freeze()
                .Wait(4f)
                .Enter(delegate
                {
                    if (_phone != null) _phone.Available = false;
                    if (_interaction != null) _interaction.Active = false;
                    if (_fader != null)
                    {
                        _fader.FadeOut(1f);
                        _fader.ShowCard("LA PLANQUE\n02:10");
                    }
                })
                .Exit(delegate
                {
                    if (_championBrain != null) _championBrain.enabled = false;

                    if (_locations != null) _locations.GoTo(_houseLocation);
                    if (_phone != null) _phone.Available = true;
                    if (_interaction != null) _interaction.Active = true;
                    if (_fader != null) _fader.FadeIn(1.4f);
                }));

            beats.Add(new StoryBeat("compte-3")
                .Wait(1.2f)
                .Enter(delegate
                {
                    int money = _progress != null ? _progress.Money : 0;

                    List<DialogueLine> lines = new List<DialogueLine>(4);
                    lines.Add(DialogueLine.Say("MOI", money + " euros."));

                    lines.Add(money >= _rent
                        ? DialogueLine.Say("MOI", "Le loyer est payé. Pour la première fois depuis l'hiver.")
                        : DialogueLine.Say("MOI", "Il en manque encore " + (_rent - money) + "."));

                    lines.Add(DialogueLine.Say("MOI", "Et quelqu'un a commandé ce combat-là contre moi."));
                    lines.Add(DialogueLine.Say("MOI", "Faudra savoir qui."));

                    if (_subtitles != null) _subtitles.Play(lines);
                })
                .Exit(delegate
                {
                    if (_fader == null) return;

                    _fader.FadeOut(2f);
                    _fader.ShowCard("ÜBER BAGARRE\nCHAPITRE 2\n\nÀ SUIVRE");
                }));

            beats.Add(new StoryBeat("carton-chapitre-2")
                .Freeze()
                .Wait(5f)
                .Exit(delegate
                {
                    if (_fader != null)
                    {
                        _fader.ShowCard(null);
                        _fader.FadeIn(1.5f);
                    }

                    if (_phone != null) _phone.Lower();
                    if (_objectives != null) _objectives.Set("Chapitre 2 terminé");
                }));
        }

        /// <summary>
        /// Met le monde dans l'état de la fin du chapitre 1, pour un départ direct au chapitre 2.
        /// Mêmes chemins que le jeu : les courses sont encaissées, les niveaux passent, les
        /// capacités s'ouvrent.
        /// </summary>
        private void EnsureChapterTwoState()
        {
            EnsureChapterOneState();

            if (_progress != null && _progress.Contracts < 2) Pay(_briefingTwo);

            SetBrothersActive(false);
            if (_championBrain != null) _championBrain.enabled = false;
            if (_ringGate != null) _ringGate.SetActive(true);
        }

        /// <summary>Le public autour de la fosse exulte, chacun avec son propre retard.</summary>
        private void CheerRing(float strength, float duration)
        {
            for (int i = 0; i < _ringCrowd.Length; i++)
            {
                if (_ringCrowd[i] != null) _ringCrowd[i].Cheer(strength, duration);
            }
        }

        private bool ChampionKnockedOut()
        {
            return _champion == null || (_champion.Health != null && !_champion.Health.IsAlive);
        }

        /// <summary>
        /// Met le monde dans l'état exact de la fin du prologue, pour un départ direct au
        /// chapitre 1. Sans effet quand on y arrive en jouant : la course est déjà encaissée.
        /// </summary>
        private void EnsureChapterOneState()
        {
            if (_player != null && _player.Health != null) _player.Health.ResetToFull();
            if (_targetBrain != null) _targetBrain.enabled = false;
            if (_finder != null) _finder.Searching = false;
            if (_tutorial != null) _tutorial.Hide();

            if (_progress == null || _progress.Contracts > 0) return;

            Pay(_briefing);

            if (_locations != null) _locations.GoTo(_houseLocation);
            if (_phone != null)
            {
                _phone.Available = true;
                _phone.SetScreen(PhoneDevice.Screen.Verrouille);
                _phone.Lower();
            }
        }

        private string BrotherName(int index, string fallback)
        {
            if (index >= _brothers.Length || _brothers[index] == null) return fallback;

            string name = _brothers[index].DisplayName;
            return string.IsNullOrEmpty(name) ? fallback : name.ToUpperInvariant();
        }

        private void SetBrothersActive(bool active)
        {
            for (int i = 0; i < _brotherBrains.Length; i++)
            {
                if (_brotherBrains[i] != null) _brotherBrains[i].enabled = active;
            }
        }

        private bool IsBrother(Combatant combatant)
        {
            if (combatant == null) return false;

            for (int i = 0; i < _brothers.Length; i++)
            {
                if (_brothers[i] == combatant) return true;
            }

            return false;
        }

        /// <summary>Les deux frères sont K.O. — morts au sens du contrat, pas juste au sol.</summary>
        private bool BrothersKnockedOut()
        {
            int counted = 0;

            for (int i = 0; i < _brothers.Length; i++)
            {
                Combatant brother = _brothers[i];
                if (brother == null) continue;

                counted++;
                if (brother.Health != null && brother.Health.IsAlive) return false;
            }

            return counted > 0;
        }

        private bool PlayerNear(Transform point, float distance)
        {
            if (point == null || _player == null) return true;

            Vector3 offset = _player.transform.position - point.position;
            offset.y = 0f;

            return offset.sqrMagnitude <= distance * distance;
        }

        // ------------------------------------------------------------------ progression

        /// <summary>Encaisse une course. Le passage de niveau arrive par l'événement de la progression.</summary>
        private void Pay(MissionBriefing contract)
        {
            if (_progress == null || contract == null) return;

            _progress.CompleteContract(contract.Reward, contract.Experience, contract.ReviewStars,
                contract.Review, contract.ClientName);
        }

        /// <summary>
        /// Ce que chaque niveau débloque. C'est ICI, et pas dans la table d'expérience, que se
        /// décide l'effet de jeu : la progression compte, le scénario récompense.
        /// </summary>
        private void OnLeveledUp(int level)
        {
            if (level >= 2 && _playerCombat != null) _playerCombat.HeadbuttUnlocked = true;

            if (level == 2 && _phoneDisplay != null)
            {
                _phoneDisplay.UnlockedAbility = "COUP DE TÊTE — touche G";
            }

            if (level >= 3 && !_toughnessApplied)
            {
                _toughnessApplied = true;

                if (_player != null && _player.Health != null && _toughnessBonus > 0f)
                {
                    _player.Health.SetMaxHealth(_player.Health.MaxHealth + _toughnessBonus, true);
                }

                if (_phoneDisplay != null)
                {
                    _phoneDisplay.UnlockedAbility = "ENCAISSEUR — +" + Mathf.RoundToInt(_toughnessBonus) + " PV";
                }
            }

            if (level >= 4 && !_secondWindApplied)
            {
                _secondWindApplied = true;

                if (_player != null && _player.Stamina != null && _secondWindBonus > 0f)
                {
                    _player.Stamina.MaxStamina = _player.Stamina.MaxStamina * (1f + _secondWindBonus);
                    _player.Stamina.Refill();
                }

                if (_phoneDisplay != null)
                {
                    _phoneDisplay.UnlockedAbility = "SECOND SOUFFLE — +" + Mathf.RoundToInt(_secondWindBonus * 100f) + " % d'endurance";
                }
            }

            Debug.Log("[UberBagarre] Niveau " + level + " atteint.", this);
        }

        /// <summary>
        /// Une leçon : une consigne, un compteur, et une étape qui ne passe pas avant que le
        /// geste soit réussi — ou que l'adversaire soit au sol, parce qu'un joueur qui met K.O.
        /// avant la fin du tutoriel a déjà prouvé qu'il avait compris.
        /// </summary>
        private StoryBeat Lesson(string id, string key, string instruction, int required,
            System.Func<int> counter, System.Func<bool> skip)
        {
            StoryBeat beat = new StoryBeat(id);

            beat.Enter(delegate
            {
                if (_tutorial != null) _tutorial.Show(key, instruction, required);
            });

            beat.Until(delegate
            {
                if (skip != null && skip()) return true;

                int done = counter();
                if (_tutorial == null) return done >= required;

                // Le compteur affiche est rattrape sur le compteur reel : les evenements de
                // combat arrivent quand ils arrivent, pas au rythme de l'interface.
                while (_tutorial.Progress < done && _tutorial.Progress < required) _tutorial.Score();

                return done >= required;
            });

            beat.Exit(delegate
            {
                if (_tutorial != null) _tutorial.Hide();
            });

            return beat;
        }

        /// <summary>
        /// Assez avance pour qu'une lecon du tutoriel n'ait plus de raison d'attendre : au sol
        /// ou hors de combat. Un joueur qui met son adversaire a terre avant d'avoir appris le
        /// crochet a deja prouve qu'il n'en avait pas besoin.
        /// </summary>
        private bool TargetDefeated()
        {
            if (_target == null) return false;
            if (TargetKnockedOut()) return true;

            return _targetDown;
        }

        /// <summary>K.O. au sens du contrat : il ne se releve plus.</summary>
        private bool TargetKnockedOut()
        {
            return _target != null && _target.Health != null && !_target.Health.IsAlive;
        }

        // ------------------------------------------------------------------ evenements

        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (_player == null || victim == null) return;
            if (info.Attacker != _player.gameObject) return;

            bool onBrother = IsBrother(victim);
            bool onTarget = _target == null ? !onBrother : victim == _target;
            if (!onTarget && !onBrother) return;

            if (onBrother) _brothersProvoked = true;

            if (_shove != null && info.Attack == _shove) _shoveHits++;
            else if (_headbutt != null && info.Attack == _headbutt) _headbuttHits++;

            if (!onTarget) return;

            if (info.Zone == HitZone.Head) _headHits++;

            if (_straight != null && info.Attack == _straight) _straightHits++;
            else if (_hook != null && info.Attack == _hook) _hookHits++;
        }

        private void OnGuardEvent(DamageInfo info)
        {
            _guardEvents++;
        }

        private void OnTargetKnockedDown()
        {
            _targetDown = true;
        }

        private void OnLettersRead(Interactable source)
        {
            _lettersRead = true;
        }

        private void OnCarAtHouse(Interactable source)
        {
            _leftHouse = true;
        }

        private void OnCarAtClub(Interactable source)
        {
            _leftClub = true;
        }

        private void OnCarAtParking(Interactable source)
        {
            _leftParking = true;
        }

        private void OnClubDoor(Interactable source)
        {
            _enteredClub = true;
        }

        private void OnClubExit(Interactable source)
        {
            _leftClubInside = true;
        }

        /// <summary>
        /// Prépare une demande de preuve : la liste de ce qui doit être photographié.
        /// Une liste vide valide la première photo — c'est le cas d'une scène sans cible.
        /// </summary>
        private void RequirePhotos(params Transform[] subjects)
        {
            _photoValidated = false;
            _photoRequired.Clear();
            _photographed.Clear();

            if (subjects != null)
            {
                for (int i = 0; i < subjects.Length; i++)
                {
                    if (subjects[i] != null) _photoRequired.Add(subjects[i]);
                }
            }

            UpdatePhotoCounter();
        }

        private void UpdatePhotoCounter()
        {
            if (_phoneDisplay == null) return;

            _phoneDisplay.PhotoCounter = _photoRequired.Count > 1
                ? _photographed.Count + " / " + _photoRequired.Count
                : null;
        }

        /// <summary>
        /// La photo n'est valide que si un sujet exigé est réellement dans le cadre — et au sol.
        ///
        /// C'est une petite exigence, et c'est elle qui fait la différence entre « appuyer sur
        /// un bouton » et « fournir une preuve ». Le dossier est explicite : la photo VALIDE la
        /// tâche. Elle doit donc pouvoir être ratée. Avec plusieurs sujets, chacun compte une
        /// fois : photographier deux fois le même frère ne prouve rien sur l'autre.
        /// </summary>
        private void OnPhotoTaken(Transform aimed)
        {
            if (_photoRequired.Count == 0)
            {
                _photoValidated = true;
                return;
            }

            Transform subject = null;

            for (int i = 0; i < _photoRequired.Count; i++)
            {
                Transform candidate = _photoRequired[i];
                if (aimed != null && (aimed == candidate || aimed.IsChildOf(candidate)))
                {
                    subject = candidate;
                    break;
                }
            }

            if (subject == null)
            {
                Say("APPLI", "Sujet absent du cadre.");
                return;
            }

            if (_photographed.Contains(subject))
            {
                Say("APPLI", "Déjà envoyé. Il en manque " + (_photoRequired.Count - _photographed.Count) + ".");
                return;
            }

            Combatant proof = subject.GetComponentInParent<Combatant>();
            if (proof != null && proof.IsAlive)
            {
                Say("APPLI", "Refusé. Le sujet doit être au sol.");
                return;
            }

            _photographed.Add(subject);
            UpdatePhotoCounter();

            if (_photographed.Count >= _photoRequired.Count)
            {
                _photoValidated = true;
                return;
            }

            Say("APPLI", "Reçu. Encore " + (_photoRequired.Count - _photographed.Count) + ".");
        }

        private void Say(string speaker, string text)
        {
            if (_subtitles != null) _subtitles.Play(DialogueLine.Say(speaker, text));
        }
    }
}
