using System;
using System.Collections;
using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Core;
using UberBagarre.Enemy;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le monde ouvert : la ville vit, et l'appli fait tomber des courses.
    ///
    /// Une course, c'est la boucle du jeu entier :
    /// 1. le téléphone vibre — une commande : une cible, ce qu'elle est en train de faire, un
    ///    lieu, un prix, et souvent une consigne du client (« casse-lui le nez », « K.O. en
    ///    moins de 40 s »...) qui paie un bonus ;
    /// 2. on l'accepte dans l'appli (sinon elle part à un autre), le GPS s'allume, et le client
    ///    n'attend pas éternellement : trop lent, il annule — et ta réputation en prend un coup ;
    /// 3. sur place, la cible vit sa soirée (elle fume, téléphone, tague...) : il faut la
    ///    reconnaître ; à quelques mètres, elle lâche tout et se retourne ;
    /// 4. au sol, une photo pour la preuve ; l'appli paie, le client note selon la consigne, la
    ///    réputation monte ; la course et sa photo rejoignent l'historique de l'appli ;
    /// 5. quelques instants plus tard, une autre commande tombe ailleurs.
    ///
    /// K.O. soi-même : la course est ratée (réputation en baisse), on se réveille à la planque
    /// et l'hôpital prend sa part — avec une blessure de plus. À la troisième blessure, le
    /// prochain K.O. tue : la partie reprend au dernier sommeil. Dormir soigne et sauvegarde.
    ///
    /// L'histoire peut glisser ses propres courses (un chapitre) entre les courses ordinaires.
    /// </summary>
    public class OpenWorldDirector : MonoBehaviour
    {
        [Serializable]
        public class Spot
        {
            public string name = "Coin de rue";
            public Vector3 position;
            public float yaw;

            [Tooltip("Ce que la cible y fait quand on arrive (fume, telephone, boit, tague...). Vide = elle attend.")]
            public string activity;

            [Tooltip("Optionnel : ce qu'elle regarde (un distributeur, un mur).")]
            public Vector3 lookAt;
        }

        [Serializable]
        public class Profile
        {
            public string name = "CIBLE";
            public string age = "30 ans";
            public string clothing = "Veste sombre";

            [TextArea(2, 3)] public string record = "";

            [Range(1, 3)] public int stars = 1;
            public GameObject template;
        }

        /// <summary>Une course imposée par l'histoire (un chapitre).</summary>
        public class StoryContract
        {
            public string Tag;
            public Profile Profile;
            public Spot Spot;
            public string Client = "CLIENT VIP";
            public int Reward = 300;
            public int Experience = 250;
            public ContractGoal Goal = ContractGoal.Aucune;
            public string Intro;

            /// <summary>Ce qui se dit au moment où la cible se retourne (à la place de sa réaction).</summary>
            public DialogueLine[] Lines;
        }

        private enum Stage
        {
            Waiting = 0,
            Offered = 1,
            EnRoute = 2,
            Fighting = 3,
            Proof = 4,
            Paid = 5,
            FaceOff = 6
        }

        [Header("References")]
        [SerializeField] private PhoneDevice _phone;
        [SerializeField] private PhoneDisplay _display;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Combatant _player;
        [SerializeField] private PlayerProgress _progress;
        [SerializeField] private MissionBriefing _briefing;
        [SerializeField] private FightIntro _intro;
        [SerializeField] private FightCrowd _crowd;
        [SerializeField] private CityMap _map;
        [SerializeField] private ScreenFader _fader;
        [SerializeField] private SubtitleDisplay _subtitles;
        [SerializeField] private TargetActivityKit _kit;

        [SerializeField]
        [Tooltip("Le regard du joueur : pendant le face-à-face, il glisse vers la cible.")]
        private PlayerLook _look;

        [SerializeField]
        [Tooltip("Ou l'on se reveille apres un K.O. : la planque.")]
        private Transform _home;

        [SerializeField] private Transform _targetsRoot;

        [Header("Courses")]
        [SerializeField] private Spot[] _spots = new Spot[0];
        [SerializeField] private Profile[] _profiles = new Profile[0];

        [SerializeField, Min(0f)] private float _firstOrder = 14f;
        [SerializeField, Min(0f)] private float _betweenOrders = 22f;

        [SerializeField, Min(5f)]
        [Tooltip("Une course non acceptee part a quelqu'un d'autre apres ce delai.")]
        private float _offerTimeout = 50f;

        [SerializeField, Min(1f)]
        [Tooltip("La course tombe loin : il faut traverser un bout de ville.")]
        private float _minimumDistance = 45f;

        [SerializeField, Min(1f)]
        [Tooltip("Distance a laquelle la cible remarque le joueur et se retourne (sans activite).")]
        private float _engageDistance = 6.5f;

        [SerializeField, Min(0f)] private float _regenPerSecond = 2.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part de l'argent perdue a l'hopital apres un K.O.")]
        private float _hospitalShare = 0.15f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Probabilite qu'un client ajoute une consigne.")]
        private float _goalChance = 0.85f;

        [Header("Reputation")]
        [SerializeField] private int _knockedOutPenalty = 12;
        [SerializeField] private int _latePenalty = 6;
        [SerializeField, Min(10f)] private float _suspension = 120f;

        private static readonly string[] Clients =
        {
            "CLIENT VÉRIFIÉ", "CLIENTE VÉRIFIÉE", "CLIENT ANONYME", "CLIENT VIP", "CLIENT RÉGULIER"
        };

        private static readonly string[] Reviews =
        {
            "Propre, net, pas un mot de trop.", "Il a compris le message. Merci.", "Rapide. Je recommande.",
            "Un peu violent, mais efficace.", "Parfait. Je repasse commande la semaine prochaine.",
            "Il marchera droit maintenant.", "Enfin quelqu'un de sérieux dans cette appli."
        };

        private static readonly string[] HalfReviews =
        {
            "Payé, mais j'avais demandé autre chose.", "Le travail est fait. À moitié.", "Correct. Sans plus.",
            "Il est par terre, d'accord. C'était pas la consigne."
        };

        private Stage _stage;
        private float _timer;
        private Spot _spot;
        private Profile _profile;
        private GameObject _targetGo;
        private Combatant _target;
        private EnemyBrain _targetBrain;
        private TargetActivity _activity;
        private ContractObjective _objective;
        private int _reward;
        private int _experience;
        private int _lastSpot = -1;
        private int _lastProfile = -1;
        private ContractGoal _lastGoal;
        private bool _knockedOut;
        private bool _sleeping;
        private GameObject _toClean;
        private float _cleanAt;
        private float _deadline;
        private bool _warned;
        private float _suspendedUntil;
        private StoryContract _story;
        private StoryContract _running;
        private GUIStyle _banner;
        private GUIStyle _small;
        private GUIStyle _title;

        private string _repNotice;
        private float _repNoticeUntil;
        private bool _repGood;

        /// <summary>L'histoire installe l'appli elle-même (le lien de Sami) : le directeur n'y touche pas.</summary>
        public bool StoryControlsApp { get; set; }

        /// <summary>Plus de courses ordinaires tant que l'histoire le demande (une scène, un chapitre).</summary>
        public bool Paused { get; set; }

        /// <summary>Une course de l'histoire s'est terminée : son étiquette, et si elle est réussie.</summary>
        public event Action<string, bool> StoryContractFinished;

        /// <summary>Le joueur est mort : la partie vient d'être rechargée.</summary>
        public event Action Died;

        /// <summary>La nuit passe : le jour a changé, la partie n'est pas encore sauvegardée (loyer, drapeaux).</summary>
        public event Action Sleeping;

        /// <summary>Le joueur vient de dormir (partie sauvegardée).</summary>
        public event Action Slept;

        /// <summary>Une course est en cours (acceptée, pas encore payée).</summary>
        public bool Busy
        {
            get { return _stage == Stage.EnRoute || _stage == Stage.FaceOff || _stage == Stage.Fighting || _stage == Stage.Proof; }
        }

        public Transform Home { get { return _home; } set { _home = value; } }

        /// <summary>Le constructeur de la scène y range les lieux de rendez-vous et les cibles.</summary>
        public void Configure(Spot[] spots, Profile[] profiles)
        {
            _spots = spots;
            _profiles = profiles;
        }

        public IList<Profile> Profiles { get { return _profiles; } }
        public IList<Spot> Spots { get { return _spots; } }

        /// <summary>L'histoire impose la prochaine course (elle tombe dès que possible).</summary>
        public void QueueStoryContract(StoryContract contract)
        {
            _story = contract;
            if (_stage == Stage.Waiting) _timer = Mathf.Min(_timer, 4f);
        }

        /// <summary>Oublie la course d'histoire en attente (la partie a été rechargée).</summary>
        public void ClearStoryContract()
        {
            _story = null;
            if (_stage == Stage.Offered && _running != null)
            {
                _running = null;
                if (_phone != null && _phone.Current == PhoneDevice.Screen.Accueil) _phone.SetScreen(PhoneDevice.Screen.Verrouille);
                _stage = Stage.Waiting;
                _timer = 4f;
            }
        }

        /// <summary>La course d'histoire en attente ou en cours (son étiquette), sinon null.</summary>
        public string PendingStoryTag
        {
            get { return _running != null ? _running.Tag : _story != null ? _story.Tag : null; }
        }

        // ------------------------------------------------------------------ cycle

        private void Start()
        {
            EnemyBrain.HoldAll = false;
            _stage = Stage.Waiting;
            _timer = _firstOrder;

            if (_phone != null)
            {
                _phone.Available = true;
                if (!StoryControlsApp) _phone.AppInstalled = true;
                _phone.SetScreen(PhoneDevice.Screen.Verrouille);
            }

            if (_display != null) _display.PhotoCounter = string.Empty;
            if (_map != null) _map.ClearWaypoint();

            PhoneGallery.LoadSaved();
        }

        private void OnEnable()
        {
            if (_phone != null)
            {
                _phone.StoryConfirmed += OnConfirmed;
                _phone.PhotoTaken += OnPhotoTaken;
            }

            if (_progress != null) _progress.ReputationChanged += OnReputationChanged;
            TargetInjuries.AnyFractured += OnFracture;
        }

        private void OnDisable()
        {
            if (_phone != null)
            {
                _phone.StoryConfirmed -= OnConfirmed;
                _phone.PhotoTaken -= OnPhotoTaken;
            }

            if (_progress != null) _progress.ReputationChanged -= OnReputationChanged;
            TargetInjuries.AnyFractured -= OnFracture;
            if (_objective != null) _objective.End();
        }

        private void Update()
        {
            if (GameMenu.IsPaused) return;

            float dt = Time.deltaTime;
            Regenerate(dt);
            CleanUp();

            if (_player != null && !_player.IsAlive)
            {
                if (!_knockedOut) StartCoroutine(KnockedOut());
                return;
            }

            // Filet de sécurité : passé sous la ville (un trou, le vol de triche coupé en l'air),
            // on revient à la planque plutôt que de tomber pour toujours.
            if (_player != null && _home != null && _player.transform.position.y < -25f && !_knockedOut)
            {
                Teleport(_home);
            }

            switch (_stage)
            {
                case Stage.Waiting:
                    if (Paused && _story == null) break;
                    _timer -= dt;
                    if (_timer <= 0f) Offer();
                    break;

                case Stage.Offered:
                    _timer -= dt;
                    if (_timer <= 0f) Expire();
                    break;

                case Stage.EnRoute:
                    UpdateEnRoute(dt);
                    break;

                case Stage.FaceOff:
                    UpdateFaceOff(dt);
                    break;

                case Stage.Fighting:
                    if (_target == null)
                    {
                        Abandon();
                        break;
                    }

                    if (_briefing != null && _objective != null) _briefing.ObjectiveProgress = _objective.Progress;
                    if (!_target.IsAlive) BeginProof();
                    break;

                case Stage.Paid:
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        _stage = Stage.Waiting;
                        _timer = _betweenOrders;
                        if (_phone != null && _phone.Current == PhoneDevice.Screen.Valide) _phone.SetScreen(PhoneDevice.Screen.Verrouille);
                    }
                    break;
            }
        }

        // ------------------------------------------------------------------ commande

        private void Offer()
        {
            if (_progress != null && _progress.Suspended)
            {
                if (_suspendedUntil <= 0f)
                {
                    _suspendedUntil = Time.time + _suspension;
                    Say("APPLI", "Compte suspendu : trop de courses ratées. Réessaie plus tard.");
                }

                if (Time.time < _suspendedUntil)
                {
                    _timer = 5f;
                    return;
                }

                _suspendedUntil = 0f;
                _progress.ChangeReputation(8, "Seconde chance");
                Say("APPLI", "Ton compte est réactivé. Dernière chance : ne nous déçois pas.");
            }

            if (_story != null)
            {
                OfferStory(_story);
                _story = null;
                return;
            }

            if (_profiles.Length == 0 || _spots.Length == 0)
            {
                _timer = 30f;
                return;
            }

            int maxStars = _progress != null ? _progress.MaxStarsOffered : 1;

            _profile = PickProfile(maxStars);
            _spot = PickSpot();
            if (_profile == null || _spot == null)
            {
                _timer = 10f;
                return;
            }

            int stars = _profile.stars;
            float pay = (stars == 1 ? 160 : stars == 2 ? 320 : 580) * UnityEngine.Random.Range(0.85f, 1.2f) *
                        (_progress != null ? _progress.PayMultiplier : 1f);
            _reward = Mathf.RoundToInt(pay / 10f) * 10;
            _experience = stars == 1 ? 120 : stars == 2 ? 240 : 380;

            ContractGoal goal = UnityEngine.Random.value < _goalChance ? ContractObjective.Pick(stars, _lastGoal) : ContractGoal.Aucune;
            _lastGoal = goal;
            int bonus = Mathf.RoundToInt(_reward * 0.45f / 10f) * 10;

            Present(Clients[UnityEngine.Random.Range(0, Clients.Length)], goal, bonus, null);
        }

        private void OfferStory(StoryContract contract)
        {
            _running = contract;
            _profile = contract.Profile;
            _spot = contract.Spot;
            _reward = contract.Reward;
            _experience = contract.Experience;

            int bonus = Mathf.RoundToInt(_reward * 0.4f / 10f) * 10;
            Present(contract.Client, contract.Goal, bonus, contract.Intro);
        }

        private void Present(string client, ContractGoal goal, int bonus, string intro)
        {
            _objective = new ContractObjective(goal, bonus);
            TargetActivity.Kind kind = TargetActivity.Parse(_spot.activity);

            if (_briefing != null)
            {
                _briefing.Configure(_profile.name, _profile.age, _profile.clothing, _spot.name, "MAINTENANT",
                    _profile.record, client, _profile.stars, _reward, _experience,
                    Reviews[UnityEngine.Random.Range(0, Reviews.Length)], 5);
                _briefing.SetExtras(TargetActivity.Describe(kind), _objective.Text, _objective.Bonus);
            }

            if (_phone != null)
            {
                _phone.Available = true;
                _phone.SetScreen(PhoneDevice.Screen.Accueil);
            }

            if (_display != null) _display.PhotoCounter = string.Empty;
            if (!string.IsNullOrEmpty(intro)) Say("SAMI", intro);

            _stage = Stage.Offered;
            _timer = _running != null ? 600f : _offerTimeout;
        }

        /// <summary>Personne n'a répondu : la course part à un autre bagarreur.</summary>
        private void Expire()
        {
            if (_phone != null && _phone.Current == PhoneDevice.Screen.Accueil) _phone.SetScreen(PhoneDevice.Screen.Verrouille);

            if (_running != null)
            {
                // Une course de l'histoire revient : elle ne part pas à un autre.
                _story = _running;
                _running = null;
            }
            else
            {
                Banner("Course expirée : un autre bagarreur l'a prise.", 3f);
            }

            _stage = Stage.Waiting;
            _timer = _betweenOrders * 0.6f;
        }

        private Profile PickProfile(int maxStars)
        {
            for (int tries = 0; tries < 12; tries++)
            {
                int i = UnityEngine.Random.Range(0, _profiles.Length);
                Profile p = _profiles[i];
                if (p == null || p.template == null || p.stars > maxStars) continue;
                if (i == _lastProfile && _profiles.Length > 1) continue;
                _lastProfile = i;
                return p;
            }

            for (int i = 0; i < _profiles.Length; i++)
            {
                if (_profiles[i] != null && _profiles[i].template != null && _profiles[i].stars <= maxStars) return _profiles[i];
            }

            for (int i = 0; i < _profiles.Length; i++)
            {
                if (_profiles[i] != null && _profiles[i].template != null) return _profiles[i];
            }

            return null;
        }

        private Spot PickSpot()
        {
            Vector3 from = _player != null ? _player.transform.position : Vector3.zero;

            for (int tries = 0; tries < 24; tries++)
            {
                int i = UnityEngine.Random.Range(0, _spots.Length);
                Spot s = _spots[i];
                if (s == null || i == _lastSpot) continue;
                float distance = (s.position - from).magnitude;
                if (tries < 16 && (distance < _minimumDistance || distance > 320f)) continue;
                _lastSpot = i;
                return s;
            }

            return _spots.Length > 0 ? _spots[0] : null;
        }

        /// <summary>La carte de la course validée sur le téléphone : on y va.</summary>
        private void OnConfirmed()
        {
            if (_stage != Stage.Offered || _phone == null || _phone.Current != PhoneDevice.Screen.Accueil) return;

            SpawnTarget();

            _phone.SetScreen(PhoneDevice.Screen.Mission);
            _phone.Lower();

            if (_map != null && _targetGo != null)
            {
                _map.SetWaypoint(_targetGo.transform.position, _profile.name + " — " + _spot.name, null);
            }

            // Le client n'attend pas éternellement : de quoi traverser la ville, sans flâner.
            float distance = _player != null ? Vector3.Distance(_player.transform.position, _spot.position) : 100f;
            _deadline = _running != null ? -1f : 60f + distance / 3.2f;
            _warned = false;

            Say("APPLI", "Course acceptée. " + _profile.name + ", " + _spot.name + ". Signalement : " + _profile.clothing + "." +
                         (_objective != null && _objective.Goal != ContractGoal.Aucune ? " Consigne : " + _objective.Text + "." : ""));
            _stage = Stage.EnRoute;
        }

        private void SpawnTarget()
        {
            GameObject template = _profile.template;
            Quaternion rotation = Quaternion.Euler(0f, _spot.yaw, 0f);

            _targetGo = Instantiate(template, _spot.position, rotation, _targetsRoot);
            _targetGo.name = _profile.name;
            _targetGo.SetActive(true);

            ISpawnReceiver[] receivers = _targetGo.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnSpawned(_spot.position, rotation);

            _target = _targetGo.GetComponent<Combatant>();
            if (_target != null) _target.SetDisplayName(_profile.name);
            if (_targetGo.GetComponent<TargetInjuries>() == null && _target != null) _targetGo.AddComponent<TargetInjuries>();

            // Il vit sa soirée, ne se doute de rien : pas de cerveau, une activité.
            _targetBrain = _targetGo.GetComponent<EnemyBrain>();
            if (_targetBrain != null) _targetBrain.enabled = false;
            Toughen(_profile.stars);

            _activity = TargetActivity.Begin(_targetGo, TargetActivity.Parse(_spot.activity), _spot.lookAt, _kit);

            if (_target != null) _target.Damaged += OnTargetDamaged;
        }

        /// <summary>
        /// Plus la course a d'étoiles, plus la cible est coriace : plus de vie, des coups plus
        /// lourds, une meilleure défense, un cerveau plus vif. Même une course à une étoile n'est
        /// plus une promenade.
        /// </summary>
        private void Toughen(int stars)
        {
            float d = Mathf.Clamp01((stars - 1) / 4f);
            if (_targetBrain != null) _targetBrain.ApplyDifficulty(0.3f + 0.7f * d);
            if (_target == null || _target.Stats == null) return;

            const string source = "Difficulte";
            _target.Stats.RemoveBySource(source);
            _target.Stats.AddModifier(new StatModifier { stat = StatType.MaxHealth, percent = true, value = 0.25f + 0.75f * d, source = source });
            _target.Stats.AddModifier(new StatModifier { stat = StatType.Strength, percent = true, value = 0.15f + 0.45f * d, source = source });
            _target.Stats.AddModifier(new StatModifier { stat = StatType.Defense, percent = false, value = 3f + 9f * d, source = source });
            _target.ApplyStats();
        }

        // ------------------------------------------------------------------ sur place

        private void UpdateEnRoute(float dt)
        {
            if (_target == null)
            {
                Abandon();
                return;
            }

            if (_player == null) return;

            if (_deadline > 0f)
            {
                _deadline -= dt;
                if (_briefing != null) _briefing.Deadline = _deadline;

                if (!_warned && _deadline < 20f)
                {
                    _warned = true;
                    Say("APPLI", "Le client s'impatiente : plus que 20 secondes.");
                }

                if (_deadline <= 0f)
                {
                    Fail("Trop lent : le client a annulé.", _latePenalty, false);
                    return;
                }
            }

            Vector3 d = _target.transform.position - _player.transform.position;
            d.y = 0f;

            // On ne se bat pas au volant : la cible attend qu'on descende.
            float notice = _activity != null ? _activity.NoticeDistance : _engageDistance;
            if (PlayerDriving.IsDriving)
            {
                if (d.magnitude <= notice * 3f) Banner("Gare-toi et descends : il t'attend.", 0.2f);
                return;
            }

            if (d.magnitude <= notice) Engage(false);
        }

        /// <summary>Frappé avant de se retourner : il se retourne, et c'est tout de suite la bagarre.</summary>
        private void OnTargetDamaged(Combatant self, DamageInfo info)
        {
            if (_stage == Stage.EnRoute) Engage(true);
            else if (_stage == Stage.FaceOff) StartFight();
        }

        private static readonly string[] Openers =
        {
            "C'est toi, {0} ?", "On t'a commandé, mon grand.", "Rien de personnel.", "Quelqu'un a payé pour ça.",
            "T'as énervé la mauvaise personne.", "Je viens de la part d'un client."
        };

        private static readonly string[] Threats =
        {
            "Viens, alors.", "Tu vas le regretter.", "T'es sérieux, là ?", "Allez. Montre-moi.", "Mauvaise soirée pour toi.",
            "Je vais te renvoyer à ton client en morceaux."
        };

        /// <summary>
        /// La cible se retourne. Si le joueur l'a frappée, c'est la bagarre tout de suite ; sinon
        /// un face-à-face, comme dans le prologue : le jeu se fige, les bandes noires
        /// descendent, elle vient se planter devant le joueur, et on se parle (E passe une
        /// réplique). La présentation « BAGARRE ! » et le combat viennent APRÈS les paroles.
        /// </summary>
        private void Engage(bool provoked)
        {
            if (_stage != Stage.EnRoute || _target == null) return;

            if (_briefing != null) _briefing.Deadline = -1f;

            TargetActivity.Kind kind = _activity != null ? _activity.Activity : TargetActivity.Kind.Attend;
            if (_activity != null) _activity.Stop();

            if (_map != null) _map.SetWaypoint(_target.transform.position, _profile.name, _target.transform);

            if (provoked)
            {
                StartFight();
                return;
            }

            _stage = Stage.FaceOff;
            _faceOffTime = 0f;

            if (_intro != null) _intro.Bars = true;
            if (_input != null) _input.SetGameplayLock(this, true);
            if (_phone != null) _phone.Lower();

            DialogueLine[] lines = _running != null && _running.Lines != null && _running.Lines.Length > 0
                ? _running.Lines
                : new[]
                {
                    DialogueLine.Say(_profile.name, TargetActivity.Reaction(kind)),
                    DialogueLine.Say("MOI", string.Format(Openers[UnityEngine.Random.Range(0, Openers.Length)], Capitalized(_profile.name))),
                    DialogueLine.Say(_profile.name, Threats[UnityEngine.Random.Range(0, Threats.Length)])
                };

            if (_subtitles != null) _subtitles.Play(lines);
        }

        private float _faceOffTime;

        /// <summary>Chaque image du face-à-face : elle approche au pas, le regard du joueur se pose sur elle.</summary>
        private void UpdateFaceOff(float dt)
        {
            if (_target == null)
            {
                EndFaceOff();
                Abandon();
                return;
            }

            _faceOffTime += dt;

            if (_player != null)
            {
                Vector3 player = _player.transform.position;
                EnemyMotor motor = _target.GetComponent<EnemyMotor>();
                if (motor != null)
                {
                    Vector3 offset = player - _target.transform.position;
                    offset.y = 0f;
                    motor.FaceTowards(player);
                    if (offset.magnitude > 2.1f) motor.SetMoveIntent(offset, 0.45f);
                }

                if (_look != null && _look.Head != null)
                {
                    Vector3 direction = _target.AimPosition - Vector3.up * 0.05f - _look.Head.position;
                    float flat = Mathf.Sqrt(direction.x * direction.x + direction.z * direction.z);
                    if (flat > 0.05f)
                    {
                        float yaw = Mathf.Atan2(direction.x, direction.z) * Mathf.Rad2Deg;
                        float pitch = -Mathf.Atan2(direction.y, flat) * Mathf.Rad2Deg;
                        float t = 1f - Mathf.Exp(-Time.unscaledDeltaTime * 3f);
                        _look.SetLookAngles(Mathf.LerpAngle(_look.Yaw, yaw, t), Mathf.LerpAngle(_look.Pitch, pitch, t));
                    }
                }
            }

            bool speaking = _subtitles != null && _subtitles.IsSpeaking;
            if (speaking && _input != null && _input.InteractPressed && _faceOffTime > 0.3f) _subtitles.Skip();

            // Les paroles d'abord ; au pire, on n'attend pas plus de 30 s.
            if ((!speaking && _faceOffTime > 0.8f) || _faceOffTime > 30f) StartFight();
        }

        private void EndFaceOff()
        {
            if (_input != null) _input.SetGameplayLock(this, false);
            if (_intro != null) _intro.Bars = false;
        }

        /// <summary>La présentation (bandes, gros plan, « BAGARRE ! »), puis le combat.</summary>
        private void StartFight()
        {
            if (_target == null) return;

            EndFaceOff();
            if (_subtitles != null && _stage == Stage.FaceOff) _subtitles.Clear();

            _stage = Stage.Fighting;
            if (_targetBrain != null) _targetBrain.enabled = true;

            // La foule ne se forme que dans les coins abrités (ruelle, parking, hangar) : en plein
            // découvert, les gens passent leur chemin.
            if (_crowd != null && _player != null)
            {
                Vector3 center = (_target.transform.position + _player.transform.position) * 0.5f;
                if (FightCrowd.Enclosure(center) >= 0.4f) _crowd.Gather(center);
            }

            if (_objective != null) _objective.Begin(_target, _player);

            // La cinématique tient les adversaires (EnemyBrain.HoldAll) jusqu'au « BAGARRE ! ».
            string stars = new string('★', Mathf.Clamp(_profile.stars, 1, 5));
            if (_intro != null) _intro.Play(_target, _profile.name, "COURSE " + stars + "  ·  " + _reward + " EUR", null);
        }

        private static string Capitalized(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            string lower = name.ToLowerInvariant();
            char[] chars = lower.ToCharArray();
            bool start = true;
            for (int i = 0; i < chars.Length; i++)
            {
                if (start && char.IsLetter(chars[i])) chars[i] = char.ToUpperInvariant(chars[i]);
                start = chars[i] == ' ' || chars[i] == '-' || chars[i] == '\'';
            }

            return new string(chars);
        }

        private void BeginProof()
        {
            _stage = Stage.Proof;
            bool kept = _objective == null || _objective.Evaluate();
            if (_briefing != null && _objective != null && _objective.Goal != ContractGoal.Aucune) _briefing.ObjectiveResult = kept ? 1 : -1;

            if (_display != null) _display.PhotoCounter = "0 / 1";

            // L'appareil n'envoie une photo que sur cet écran : c'est lui qui dit « preuve ».
            if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Photo);
            if (_map != null) _map.SetWaypoint(_target.transform.position, "PREUVE : PHOTO", _target.transform);

            string goal = _objective != null && _objective.Goal != ContractGoal.Aucune
                ? (kept ? " Consigne tenue : bonus débloqué." : " Consigne ratée : pas de bonus.")
                : "";
            Say("APPLI", "Cible au sol." + goal + " Preuve requise : sors le téléphone, appli Photo, cadre-le.");
        }

        private void OnPhotoTaken(Transform aimed)
        {
            if (_stage != Stage.Proof || _targetGo == null) return;

            if (aimed == null || !(aimed == _targetGo.transform || aimed.IsChildOf(_targetGo.transform)))
            {
                Say("APPLI", "Sujet absent du cadre.");
                return;
            }

            if (_target != null && _target.IsAlive)
            {
                Say("APPLI", "Refusé. Le sujet doit être au sol.");
                return;
            }

            Pay();
        }

        private void Pay()
        {
            _stage = Stage.Paid;
            _timer = 7f;

            bool hasGoal = _objective != null && _objective.Goal != ContractGoal.Aucune;
            bool kept = !hasGoal || _objective.Succeeded;
            int reward = _reward + (hasGoal && kept ? _objective.Bonus : 0);
            int reviewStars = hasGoal ? (kept ? 5 : 3) : UnityEngine.Random.Range(4, 6);
            string review = kept ? Reviews[UnityEngine.Random.Range(0, Reviews.Length)] : HalfReviews[UnityEngine.Random.Range(0, HalfReviews.Length)];
            int reputation = 2 + _profile.stars + (hasGoal ? (kept ? 3 : -2) : 0);

            if (_display != null) _display.PhotoCounter = "1 / 1";
            if (_map != null) _map.ClearWaypoint();
            if (_crowd != null) _crowd.Disperse();

            if (_briefing != null)
            {
                _briefing.Configure(_briefing.TargetName, _briefing.TargetAge, _briefing.TargetClothing, _briefing.TargetLocation,
                    _briefing.MeetingTime, _briefing.TargetRecord, _briefing.ClientName, _briefing.Stars, reward, _experience,
                    review, reviewStars);
                _briefing.ReputationDelta = reputation;
                _briefing.ObjectiveResult = hasGoal ? (kept ? 1 : -1) : 0;
            }

            if (_progress != null)
            {
                _progress.CompleteContract(reward, _experience, reviewStars, review, _briefing != null ? _briefing.ClientName : "CLIENT");
                _progress.ChangeReputation(reputation, kept ? "Course réussie" : "Course réussie, consigne ratée");
                _progress.Record(new PlayerProgress.ContractRecord
                {
                    target = _profile.name,
                    place = _spot.name,
                    reward = reward,
                    stars = reviewStars,
                    success = true,
                    objective = hasGoal ? _objective.Text : string.Empty,
                    objectiveDone = kept,
                    photo = Time.unscaledTime - PhotoArchive.LastSavedTime < 5f ? PhotoArchive.LastSaved : string.Empty,
                    note = review,
                    reputation = reputation
                });
            }

            if (_phone != null)
            {
                _phone.SetScreen(PhoneDevice.Screen.Valide);
                _phone.Raise();
            }

            FinishStory(true);
            ScheduleCleanup();
        }

        /// <summary>Course ratée (trop lent, K.O.) : historique, réputation, nettoyage.</summary>
        private void Fail(string reason, int penalty, bool knockedOut)
        {
            EndFaceOff();
            if (_progress != null && _profile != null)
            {
                _progress.ChangeReputation(-penalty, reason);
                _progress.Record(new PlayerProgress.ContractRecord
                {
                    target = _profile.name,
                    place = _spot != null ? _spot.name : string.Empty,
                    reward = 0,
                    stars = 1,
                    success = false,
                    objective = _objective != null ? _objective.Text : string.Empty,
                    note = reason,
                    reputation = -penalty
                });
                _progress.AddReview(knockedOut ? 1 : 2, knockedOut ? "Il s'est fait étaler. Remboursé." : "Jamais venu. Je change d'appli.",
                    _briefing != null ? _briefing.ClientName : "CLIENT");
            }

            if (_map != null) _map.ClearWaypoint();
            if (_crowd != null) _crowd.Disperse();
            if (_display != null) _display.PhotoCounter = string.Empty;
            if (_phone != null && (_phone.Current == PhoneDevice.Screen.Mission || _phone.Current == PhoneDevice.Screen.Photo))
            {
                _phone.SetScreen(PhoneDevice.Screen.Verrouille);
            }

            if (!knockedOut) Say("APPLI", reason + " Réputation -" + penalty + ".");

            FinishStory(false);

            // La cible part si personne ne la regarde ; K.O., on la retire tout de suite.
            if (knockedOut && _targetGo != null)
            {
                Destroy(_targetGo);
                _targetGo = null;
            }

            ScheduleCleanup();
            _stage = Stage.Waiting;
            _timer = _betweenOrders;
        }

        private void FinishStory(bool success)
        {
            if (_running == null) return;

            string tag = _running.Tag;
            _running = null;

            Action<string, bool> handler = StoryContractFinished;
            if (handler != null) handler(tag, success);
        }

        /// <summary>La cible a disparu (détruite, tombée hors de la ville) : la course est annulée.</summary>
        private void Abandon()
        {
            EndFaceOff();
            if (_map != null) _map.ClearWaypoint();
            if (_display != null) _display.PhotoCounter = string.Empty;
            if (_phone != null && _phone.Current == PhoneDevice.Screen.Mission) _phone.SetScreen(PhoneDevice.Screen.Verrouille);

            if (_running != null)
            {
                // Une course de l'histoire ne se perd pas dans un trou : elle retombera.
                _story = _running;
                _running = null;
            }

            ScheduleCleanup();
            _stage = Stage.Waiting;
            _timer = _betweenOrders;
        }

        private void ScheduleCleanup()
        {
            if (_target != null) _target.Damaged -= OnTargetDamaged;
            if (_objective != null) _objective.End();

            if (_targetGo != null)
            {
                if (_toClean != null) Destroy(_toClean);
                _toClean = _targetGo;
                _cleanAt = Time.time + 25f;
            }

            _targetGo = null;
            _target = null;
            _targetBrain = null;
            _activity = null;
        }

        /// <summary>Le corps part quand plus personne ne le regarde (loin, ou après un long moment).</summary>
        private void CleanUp()
        {
            if (_toClean == null || Time.time < _cleanAt) return;

            float distance = _player != null ? (_toClean.transform.position - _player.transform.position).magnitude : 999f;
            if (distance < 30f && Time.time < _cleanAt + 90f) return;

            Destroy(_toClean);
            _toClean = null;
        }

        private void OnFracture(TargetInjuries target, string what)
        {
            if (_progress != null) _progress.CountFracture(what);

            string label = what == "nez" ? "NEZ CASSÉ" : what == "jambe" ? "JAMBE CASSÉE" : "CÔTES CASSÉES";
            Banner(label + " !", 1.6f);
        }

        // ------------------------------------------------------------------ K.O. du joueur, mort

        private IEnumerator KnockedOut()
        {
            _knockedOut = true;
            bool contract = Busy;

            yield return new WaitForSeconds(2.5f);

            bool dead = _progress != null && _progress.TakeKnockout();

            if (_fader != null)
            {
                _fader.FadeOut(1.2f);
                yield return new WaitForSeconds(1.4f);
                _fader.ShowCard(dead ? "T'ES MORT." : "Plus tard...");
            }

            if (contract) Fail("K.O. : la course est ratée.", _knockedOutPenalty, true);

            if (dead)
            {
                yield return new WaitForSeconds(2.6f);
                if (_fader != null) _fader.ShowCard("Tu reprends au dernier endroit où tu as dormi.");
                yield return new WaitForSeconds(2.2f);

                if (_progress != null && !_progress.Load()) _progress.ResetProgress();
                if (_toClean != null) Destroy(_toClean);
                _toClean = null;
            }

            int bill = 0;
            if (!dead && _progress != null)
            {
                bill = Mathf.RoundToInt(_progress.Money * _hospitalShare / 10f) * 10;
                if (bill > 0) _progress.AddMoney(-bill, "Frais d'hôpital");
            }

            RestorePlayer();

            yield return new WaitForSeconds(1f);
            if (_fader != null)
            {
                _fader.ShowCard(null);
                _fader.FadeIn(1.5f);
            }

            if (dead)
            {
                Say("SAMI", "Mec... on a cru qu'on t'avait perdu. Fais gaffe. Vraiment.");
                Action handler = Died;
                if (handler != null) handler();
            }
            else
            {
                int injuries = _progress != null ? _progress.Injuries : 0;
                string warning = injuries >= PlayerProgress.MaxInjuries - 1
                    ? " Blessures : " + injuries + "/" + PlayerProgress.MaxInjuries + ". Un K.O. de plus et c'est fini. Va dormir."
                    : " Blessures : " + injuries + "/" + PlayerProgress.MaxInjuries + ". Une nuit de sommeil et ça ira.";
                Say("SAMI", (bill > 0 ? "Ils t'ont ramassé sur le trottoir. L'hosto t'a pris " + bill + " balles." : "Ils t'ont ramassé sur le trottoir.") + warning);
            }

            _stage = Stage.Waiting;
            _timer = _betweenOrders;
            _knockedOut = false;
        }

        private void RestorePlayer()
        {
            if (_player == null) return;

            _player.Revive();

            KnockdownSystem knockdown = _player.GetComponentInChildren<KnockdownSystem>(true);
            if (knockdown != null) knockdown.ForceStand();

            BruiseSystem bruises = _player.GetComponentInChildren<BruiseSystem>(true);
            if (bruises != null) bruises.Clear();

            if (_home != null) Teleport(_home);
        }

        /// <summary>Ramène le joueur chez lui (début de partie, partie rechargée).</summary>
        public void ReturnHome()
        {
            if (_home != null) Teleport(_home);
        }

        private void Teleport(Transform where)
        {
            if (_player == null || where == null) return;

            ISpawnReceiver[] receivers = _player.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnSpawned(where.position, where.rotation);
        }

        // ------------------------------------------------------------------ sommeil

        /// <summary>Dormir : un jour passe, les blessures guérissent, la vie remonte, la partie est sauvegardée.</summary>
        public void Sleep()
        {
            if (_sleeping || _knockedOut) return;

            if (Busy)
            {
                Say("", "Pas maintenant : une course est en cours.");
                return;
            }

            StartCoroutine(SleepRoutine());
        }

        private IEnumerator SleepRoutine()
        {
            _sleeping = true;
            if (_input != null) _input.SetGameplayLock(this, true);

            if (_fader != null)
            {
                _fader.FadeOut(1f);
                yield return new WaitForSeconds(1.2f);
            }

            if (_stage == Stage.Offered) Expire();

            if (_progress != null)
            {
                _progress.Sleep();
                Action sleeping = Sleeping;
                if (sleeping != null) sleeping();
                bool saved = _progress.Save();
                if (_fader != null) _fader.ShowCard("Jour " + _progress.Day + (saved ? "\nPartie sauvegardée." : ""));
            }

            if (_player != null)
            {
                _player.Revive();
                BruiseSystem bruises = _player.GetComponentInChildren<BruiseSystem>(true);
                if (bruises != null) bruises.Clear();
            }

            yield return new WaitForSeconds(2.2f);

            if (_fader != null)
            {
                _fader.ShowCard(null);
                _fader.FadeIn(1.2f);
            }

            if (_input != null) _input.SetGameplayLock(this, false);
            _sleeping = false;
            _timer = Mathf.Max(_timer, 6f);

            Action handler = Slept;
            if (handler != null) handler();
        }

        // ------------------------------------------------------------------ utilitaires

        /// <summary>Hors combat, on récupère — lentement : une course par soirée, pas trois d'affilée.</summary>
        private void Regenerate(float dt)
        {
            if (_player == null || !_player.IsAlive || _player.Health == null) return;
            if (CombatPresence.Player != null && CombatPresence.Player.InCombat) return;
            if (_player.Health.Normalized >= 1f) return;

            _player.Health.Heal(_regenPerSecond * dt);
        }

        private void Say(string speaker, string text)
        {
            if (_subtitles != null) _subtitles.Play(DialogueLine.Say(speaker, text));
        }

        private void OnReputationChanged(int delta, string reason)
        {
            _repGood = delta > 0;
            _repNotice = (delta > 0 ? "+" : "") + delta + " RÉPUTATION  ·  " + reason;
            _repNoticeUntil = Time.unscaledTime + 3.5f;
        }

        private string _notice;
        private float _noticeUntil;

        /// <summary>Une consigne passagère en haut de l'écran.</summary>
        public void Banner(string text, float duration)
        {
            _notice = text;
            _noticeUntil = Time.time + duration;
        }

        private void OnGUI()
        {
            if (GameMenu.IsOpen || FightIntro.AnyPlaying || ModalScreen.Active) return;
            EnsureStyles();

            float unit = Screen.height / 1080f;
            DrawContractPanel(unit);
            DrawReputationNotice(unit);

            string text = null;
            if (Time.time < _noticeUntil && !string.IsNullOrEmpty(_notice))
            {
                text = _notice;
            }
            else if (_stage == Stage.Offered)
            {
                if (_phone != null && _phone.IsRaised && _phone.Current == PhoneDevice.Screen.Accueil) return;
                text = PlayerDriving.IsDriving
                    ? "NOUVELLE COURSE — gare-toi et descends pour lire le téléphone"
                    : "NOUVELLE COURSE — T pour sortir le téléphone, E pour accepter  (" + Mathf.CeilToInt(Mathf.Max(0f, _timer)) + " s)";
            }
            else if (_progress != null && _progress.Suspended && _suspendedUntil > 0f)
            {
                text = "COMPTE SUSPENDU — " + Mathf.CeilToInt(Mathf.Max(0f, _suspendedUntil - Time.time)) + " s";
            }

            if (text == null) return;

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            Rect band = new Rect(Screen.width * 0.5f - 340f * unit, 24f * unit, 680f * unit, 38f * unit);
            GuiKit.Glow(band, new Color(0f, 0f, 0f, 0.45f), 19f * unit, 10f * unit);
            GuiKit.Rounded(band, new Color(0.07f, 0.064f, 0.1f, 0.85f), 19f * unit);
            GuiKit.RoundedOutline(band, new Color(1f, 0.85f, 0.4f, 0.35f * pulse), 19f * unit, 1f);
            GuiKit.ShadowLabel(band, text, _banner, new Color(1f, 0.85f, 0.4f, 0.6f + 0.4f * pulse), 0.5f);
        }

        /// <summary>La course en cours, en haut à gauche : qui, où, le temps, la consigne.</summary>
        private void DrawContractPanel(float unit)
        {
            if (!Busy || _profile == null) return;

            float x = 24f * unit;
            float y = 24f * unit;
            float w = 380f * unit;
            bool hasGoal = _objective != null && _objective.Goal != ContractGoal.Aucune;
            float h = (hasGoal ? 104f : 70f) * unit;

            Rect card = new Rect(x, y, w, h);
            GuiKit.Glow(card, new Color(0f, 0f, 0f, 0.45f), 14f * unit, 12f * unit);
            GuiKit.Rounded(card, new Color(0.07f, 0.064f, 0.1f, 0.82f), 14f * unit);
            GuiKit.RoundedOutline(card, new Color(1f, 1f, 1f, 0.07f), 14f * unit, 1f);
            GuiKit.Rounded(new Rect(x + 6f * unit, y + 12f * unit, 4f * unit, h - 24f * unit), new Color(1f, 0.2f, 0.62f, 0.95f), 2f * unit);

            string header = "COURSE " + new string('★', Mathf.Clamp(_profile.stars, 1, 3)) + "  ·  " + _profile.name;
            GuiKit.ShadowLabel(new Rect(x + 20f * unit, y + 8f * unit, w - 28f * unit, 24f * unit), header, _title, Color.white, 0.5f);

            string line = _spot != null ? _spot.name : "";
            if (_stage == Stage.EnRoute && _deadline > 0f)
            {
                int s = Mathf.CeilToInt(_deadline);
                line += "   ·   " + (s / 60) + ":" + (s % 60).ToString("00");
            }
            else if (_stage == Stage.Proof)
            {
                line = "PREUVE : une photo du sujet au sol";
            }

            Color lineColor = _stage == Stage.EnRoute && _deadline > 0f && _deadline < 20f
                ? new Color(1f, 0.35f, 0.3f)
                : new Color(0.85f, 0.85f, 0.9f);
            GuiKit.ShadowLabel(new Rect(x + 20f * unit, y + 36f * unit, w - 28f * unit, 22f * unit), line, _small, lineColor, 0.5f);

            if (!hasGoal) return;

            string goal = "CONSIGNE : " + _objective.Text + "  (+" + _objective.Bonus + " €)";
            GuiKit.ShadowLabel(new Rect(x + 20f * unit, y + 58f * unit, w - 28f * unit, 20f * unit), goal, _small,
                new Color(1f, 0.8f, 0.35f), 0.5f);

            string progress = _objective.Evaluated ? (_objective.Succeeded ? "Consigne tenue ✓" : "Consigne ratée ✗") : _objective.Progress;
            if (!string.IsNullOrEmpty(progress))
            {
                GuiKit.ShadowLabel(new Rect(x + 20f * unit, y + 78f * unit, w - 28f * unit, 20f * unit), progress, _small,
                    new Color(0.7f, 1f, 0.75f), 0.5f);
            }
        }

        private void DrawReputationNotice(float unit)
        {
            if (Time.unscaledTime > _repNoticeUntil || string.IsNullOrEmpty(_repNotice)) return;

            float a = Mathf.Clamp01((_repNoticeUntil - Time.unscaledTime) / 0.6f);
            Rect r = new Rect(Screen.width * 0.5f - 270f * unit, 70f * unit, 540f * unit, 32f * unit);
            Color tone = _repGood ? new Color(0.45f, 1f, 0.55f, a) : new Color(1f, 0.4f, 0.35f, a);
            GuiKit.Rounded(r, new Color(0.07f, 0.064f, 0.1f, 0.8f * a), 16f * unit);
            GuiKit.RoundedOutline(r, new Color(tone.r, tone.g, tone.b, 0.5f * a), 16f * unit, 1f);
            GuiKit.ShadowLabel(r, _repNotice, _banner, tone, 0.5f);
        }

        private void EnsureStyles()
        {
            if (_banner == null) _banner = GuiKit.Style(18, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (_small == null) _small = GuiKit.Style(14, FontStyle.Bold, TextAnchor.MiddleLeft);
            if (_title == null) _title = GuiKit.Style(17, FontStyle.Bold, TextAnchor.MiddleLeft);
        }
    }
}
