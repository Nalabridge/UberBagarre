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

        [Header("Reglages")]
        [SerializeField, Min(0.5f)] private float _installDuration = 3.2f;

        [SerializeField, Min(1f)]
        [Tooltip("Points de vie du joueur pendant le prologue. Le contrat est a une etoile : la " +
                 "premiere bagarre doit s'apprendre, pas se gagner. Une reserve confortable vaut " +
                 "mieux qu'un ecran de defaite qui n'existe pas encore.")]
        private float _playerHealth = 200f;
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
            if (_phone != null) _phone.PhotoTaken += OnPhotoTaken;
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
            if (_phone != null) _phone.PhotoTaken -= OnPhotoTaken;
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

            if (_story != null) _story.Play(BuildBeats());
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
                delegate { return _straightHits; })
                .Goal("Mets " + target + " K.O.")
                .Say("MOI", "Bon."));

            beats.Add(Lesson("tuto-crochet", "CLIC DROIT", "Un crochet : plus lent, plus lourd", 1,
                delegate { return _hookHits; }));

            beats.Add(Lesson("tuto-garde", "CTRL GAUCHE", "Garde. Au bon moment, c'est une parade", 1,
                delegate { return _guardEvents; }));

            // La reference au dossier : le systeme de degats par zone, facon Skate 3. La tete
            // vaut le double, et c'est la seule facon de finir vite.
            beats.Add(Lesson("tuto-tete", "VISE HAUT", "La tête encaisse le double", 1,
                delegate { return _headHits; }));

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
                .Wait(2.5f));

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

            beats.Add(new StoryBeat("carton-final")
                .Freeze()
                .Wait(4.5f)
                .Exit(delegate
                {
                    if (_fader != null) _fader.FadeIn(1.5f);
                    if (_objectives != null) _objectives.Set("Prologue terminé");
                }));

            return beats;
        }

        /// <summary>
        /// Une leçon : une consigne, un compteur, et une étape qui ne passe pas avant que le
        /// geste soit réussi — ou que l'adversaire soit au sol, parce qu'un joueur qui met K.O.
        /// avant la fin du tutoriel a déjà prouvé qu'il avait compris.
        /// </summary>
        private StoryBeat Lesson(string id, string key, string instruction, int required,
            System.Func<int> counter)
        {
            StoryBeat beat = new StoryBeat(id);

            beat.Enter(delegate
            {
                if (_tutorial != null) _tutorial.Show(key, instruction, required);
            });

            beat.Until(delegate
            {
                if (TargetDefeated()) return true;

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
            if (_target != null && victim != _target) return;

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

        /// <summary>
        /// La photo n'est valide que si la cible est réellement dans le cadre.
        ///
        /// C'est une petite exigence, et c'est elle qui fait la différence entre « appuyer sur
        /// un bouton » et « fournir une preuve ». Le dossier est explicite : la photo VALIDE la
        /// tâche. Elle doit donc pouvoir être ratée.
        /// </summary>
        private void OnPhotoTaken(Transform aimed)
        {
            if (_targetTransform == null)
            {
                _photoValidated = true;
                return;
            }

            if (aimed != null && (aimed == _targetTransform || aimed.IsChildOf(_targetTransform)))
            {
                _photoValidated = true;
                return;
            }

            if (_subtitles != null) _subtitles.Play(DialogueLine.Say("APPLI", "Sujet absent du cadre."));
        }
    }
}
