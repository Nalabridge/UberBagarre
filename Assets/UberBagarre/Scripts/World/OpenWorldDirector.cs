using System;
using System.Collections;
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
    /// Une course, c'est la boucle du jeu entier, sans scénario autour :
    /// 1. le téléphone vibre — une commande : une cible, son signalement, un lieu, un prix ;
    /// 2. on l'accepte dans l'appli, le GPS s'allume (mini-carte, repère à l'écran, colonne
    ///    de lumière au-dessus des toits) ;
    /// 3. sur place, la cible traîne, mains dans les poches — il faut la reconnaître ; à quelques
    ///    mètres (ou au premier coup), elle se retourne : présentation, bagarre ;
    /// 4. au sol, une photo pour la preuve ; l'appli paie, le client laisse son avis ;
    /// 5. quelques instants plus tard, une autre commande tombe ailleurs.
    ///
    /// K.O. soi-même : on se réveille à la planque, la course est perdue, et l'hôpital n'est
    /// pas gratuit. Entre deux bagarres, on récupère (lentement, hors combat).
    ///
    /// Les cibles sont des modèles construits d'avance (inactifs) : chaque course en tire un
    /// neuf, qu'on détruit une fois la course finie et le joueur parti.
    /// </summary>
    public class OpenWorldDirector : MonoBehaviour
    {
        [Serializable]
        public class Spot
        {
            public string name = "Coin de rue";
            public Vector3 position;
            public float yaw;
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

        private enum Stage
        {
            Waiting = 0,
            Offered = 1,
            EnRoute = 2,
            Fighting = 3,
            Proof = 4,
            Paid = 5
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

        [SerializeField]
        [Tooltip("Ou l'on se reveille apres un K.O. : la planque.")]
        private Transform _home;

        [SerializeField] private Transform _targetsRoot;

        [Header("Courses")]
        [SerializeField] private Spot[] _spots = new Spot[0];
        [SerializeField] private Profile[] _profiles = new Profile[0];

        [SerializeField, Min(0f)] private float _firstOrder = 14f;
        [SerializeField, Min(0f)] private float _betweenOrders = 22f;

        [SerializeField, Min(1f)]
        [Tooltip("La course tombe loin : il faut traverser un bout de ville.")]
        private float _minimumDistance = 45f;

        [SerializeField, Min(1f)]
        [Tooltip("Distance a laquelle la cible remarque le joueur et se retourne.")]
        private float _engageDistance = 6.5f;

        [SerializeField, Min(0f)] private float _regenPerSecond = 2.5f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part de l'argent perdue a l'hopital apres un K.O.")]
        private float _hospitalShare = 0.15f;

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

        private Stage _stage;
        private float _timer;
        private Spot _spot;
        private Profile _profile;
        private GameObject _targetGo;
        private Combatant _target;
        private EnemyBrain _targetBrain;
        private int _reward;
        private int _experience;
        private int _lastSpot = -1;
        private int _lastProfile = -1;
        private bool _knockedOut;
        private GameObject _toClean;
        private float _cleanAt;
        private GUIStyle _banner;

        /// <summary>Le constructeur de la scène y range les lieux de rendez-vous et les cibles.</summary>
        public void Configure(Spot[] spots, Profile[] profiles)
        {
            _spots = spots;
            _profiles = profiles;
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
                _phone.AppInstalled = true;
                _phone.SetScreen(PhoneDevice.Screen.Verrouille);
            }

            if (_display != null) _display.PhotoCounter = string.Empty;
            if (_map != null) _map.ClearWaypoint();

            Say("SAMI", "T'es en ville. Garde le téléphone allumé, les courses vont tomber.");
        }

        private void OnEnable()
        {
            if (_phone != null)
            {
                _phone.StoryConfirmed += OnConfirmed;
                _phone.PhotoTaken += OnPhotoTaken;
            }
        }

        private void OnDisable()
        {
            if (_phone != null)
            {
                _phone.StoryConfirmed -= OnConfirmed;
                _phone.PhotoTaken -= OnPhotoTaken;
            }
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
                ISpawnReceiver[] receivers = _player.GetComponentsInChildren<ISpawnReceiver>(true);
                for (int i = 0; i < receivers.Length; i++) receivers[i].OnSpawned(_home.position, _home.rotation);
            }

            switch (_stage)
            {
                case Stage.Waiting:
                    _timer -= dt;
                    if (_timer <= 0f) Offer();
                    break;

                case Stage.EnRoute:
                    UpdateEnRoute();
                    break;

                case Stage.Fighting:
                    if (_target == null)
                    {
                        Abandon();
                        break;
                    }

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
            if (_profiles.Length == 0 || _spots.Length == 0)
            {
                _timer = 30f;
                return;
            }

            int level = _progress != null ? _progress.Level : 1;
            int maxStars = Mathf.Clamp(1 + level / 2, 1, 3);

            _profile = PickProfile(maxStars);
            _spot = PickSpot();
            if (_profile == null || _spot == null)
            {
                _timer = 10f;
                return;
            }

            int stars = _profile.stars;
            _reward = Mathf.RoundToInt((stars == 1 ? 160 : stars == 2 ? 320 : 580) * UnityEngine.Random.Range(0.85f, 1.2f) / 10f) * 10;
            _experience = stars == 1 ? 120 : stars == 2 ? 240 : 380;

            if (_briefing != null)
            {
                _briefing.Configure(_profile.name, _profile.age, _profile.clothing, _spot.name, "MAINTENANT",
                    _profile.record, Clients[UnityEngine.Random.Range(0, Clients.Length)], stars, _reward, _experience,
                    Reviews[UnityEngine.Random.Range(0, Reviews.Length)], UnityEngine.Random.Range(4, 6));
            }

            if (_phone != null)
            {
                _phone.Available = true;
                _phone.SetScreen(PhoneDevice.Screen.Accueil);
            }

            if (_display != null) _display.PhotoCounter = string.Empty;

            _stage = Stage.Offered;
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
                if (_profiles[i] != null && _profiles[i].template != null) return _profiles[i];
            }

            return null;
        }

        private Spot PickSpot()
        {
            Vector3 from = _player != null ? _player.transform.position : Vector3.zero;

            for (int tries = 0; tries < 20; tries++)
            {
                int i = UnityEngine.Random.Range(0, _spots.Length);
                Spot s = _spots[i];
                if (s == null || i == _lastSpot) continue;
                if (tries < 14 && (s.position - from).magnitude < _minimumDistance) continue;
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

            Say("APPLI", "Course acceptée. " + _profile.name + ", " + _spot.name + ". Signalement : " + _profile.clothing + ".");
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

            // Il attend, ne se doute de rien : pas de cerveau, bras le long du corps.
            _targetBrain = _targetGo.GetComponent<EnemyBrain>();
            if (_targetBrain != null) _targetBrain.enabled = false;

            if (_target != null) _target.Damaged += OnTargetDamaged;
        }

        // ------------------------------------------------------------------ sur place

        private void UpdateEnRoute()
        {
            if (_target == null)
            {
                Abandon();
                return;
            }

            if (_player == null) return;

            Vector3 d = _target.transform.position - _player.transform.position;
            d.y = 0f;

            // On ne se bat pas au volant : la cible attend qu'on descende.
            if (PlayerDriving.IsDriving)
            {
                if (d.magnitude <= _engageDistance * 3f) Banner("Gare-toi et descends : il t'attend.", 0.2f);
                return;
            }

            if (d.magnitude <= _engageDistance) Engage();
        }

        /// <summary>Frappé avant de se retourner : il se retourne.</summary>
        private void OnTargetDamaged(Combatant self, DamageInfo info)
        {
            if (_stage == Stage.EnRoute) Engage();
        }

        private void Engage()
        {
            if (_stage != Stage.EnRoute || _target == null) return;

            _stage = Stage.Fighting;
            if (_targetBrain != null) _targetBrain.enabled = true;

            if (_map != null) _map.SetWaypoint(_target.transform.position, _profile.name, _target.transform);

            if (_crowd != null && _player != null)
            {
                _crowd.Gather((_target.transform.position + _player.transform.position) * 0.5f);
            }

            string stars = new string('★', Mathf.Clamp(_profile.stars, 1, 3));
            if (_intro != null) _intro.Play(_target, _profile.name, "COURSE " + stars + "  ·  " + _reward + " EUR", null);
        }

        private void BeginProof()
        {
            _stage = Stage.Proof;
            if (_display != null) _display.PhotoCounter = "0 / 1";

            // L'appareil n'envoie une photo que sur cet écran : c'est lui qui dit « preuve ».
            if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Photo);
            if (_map != null) _map.SetWaypoint(_target.transform.position, "PREUVE : PHOTO", _target.transform);

            Say("APPLI", "Cible au sol. Preuve requise : sors le téléphone, appli Photo, cadre-le.");
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

            if (_display != null) _display.PhotoCounter = "1 / 1";
            if (_map != null) _map.ClearWaypoint();
            if (_crowd != null) _crowd.Disperse();

            if (_progress != null && _briefing != null)
            {
                _progress.CompleteContract(_reward, _experience, _briefing.ReviewStars, _briefing.Review, _briefing.ClientName);
            }

            if (_phone != null)
            {
                _phone.SetScreen(PhoneDevice.Screen.Valide);
                _phone.Raise();
            }

            ScheduleCleanup();
        }

        /// <summary>La cible a disparu (détruite, tombée hors de la ville) : la course est annulée.</summary>
        private void Abandon()
        {
            if (_map != null) _map.ClearWaypoint();
            if (_display != null) _display.PhotoCounter = string.Empty;
            if (_phone != null && _phone.Current == PhoneDevice.Screen.Mission) _phone.SetScreen(PhoneDevice.Screen.Verrouille);

            ScheduleCleanup();
            _stage = Stage.Waiting;
            _timer = _betweenOrders;
        }

        private void ScheduleCleanup()
        {
            if (_target != null) _target.Damaged -= OnTargetDamaged;

            if (_targetGo != null)
            {
                if (_toClean != null) Destroy(_toClean);
                _toClean = _targetGo;
                _cleanAt = Time.time + 25f;
            }

            _targetGo = null;
            _target = null;
            _targetBrain = null;
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

        // ------------------------------------------------------------------ K.O. du joueur

        private IEnumerator KnockedOut()
        {
            _knockedOut = true;

            yield return new WaitForSeconds(2.5f);

            if (_fader != null)
            {
                _fader.FadeOut(1.2f);
                yield return new WaitForSeconds(1.4f);
                _fader.ShowCard("Plus tard...");
            }

            if (_stage == Stage.EnRoute || _stage == Stage.Fighting || _stage == Stage.Proof)
            {
                Destroy(_targetGo);
                _targetGo = null;
                _target = null;
                if (_map != null) _map.ClearWaypoint();
                if (_crowd != null) _crowd.Disperse();
                if (_display != null) _display.PhotoCounter = string.Empty;
                if (_phone != null) _phone.SetScreen(PhoneDevice.Screen.Verrouille);
            }

            int bill = 0;
            if (_progress != null)
            {
                bill = Mathf.RoundToInt(_progress.Money * _hospitalShare / 10f) * 10;
                if (bill > 0) _progress.AddMoney(-bill);
            }

            if (_player != null)
            {
                _player.Revive();

                KnockdownSystem knockdown = _player.GetComponentInChildren<KnockdownSystem>(true);
                if (knockdown != null) knockdown.ForceStand();

                BruiseSystem bruises = _player.GetComponentInChildren<BruiseSystem>(true);
                if (bruises != null) bruises.Clear();

                if (_home != null)
                {
                    ISpawnReceiver[] receivers = _player.GetComponentsInChildren<ISpawnReceiver>(true);
                    for (int i = 0; i < receivers.Length; i++) receivers[i].OnSpawned(_home.position, _home.rotation);
                }
            }

            yield return new WaitForSeconds(1f);
            if (_fader != null) _fader.FadeIn(1.5f);

            Say("SAMI", bill > 0
                ? "Ils t'ont ramassé sur le trottoir. L'hosto t'a pris " + bill + " balles. Repose-toi, et reprends les courses."
                : "Ils t'ont ramassé sur le trottoir. Repose-toi, et reprends les courses.");

            _stage = Stage.Waiting;
            _timer = _betweenOrders;
            _knockedOut = false;
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

        private string _notice;
        private float _noticeUntil;

        /// <summary>Une consigne passagère en haut de l'écran.</summary>
        private void Banner(string text, float duration)
        {
            _notice = text;
            _noticeUntil = Time.time + duration;
        }

        private void OnGUI()
        {
            if (GameMenu.IsOpen || FightIntro.AnyPlaying) return;

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
                    : "NOUVELLE COURSE — T pour sortir le téléphone, E pour accepter";
            }

            if (text == null) return;

            if (_banner == null) _banner = GuiKit.Style(18, FontStyle.Bold, TextAnchor.MiddleCenter);

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 5f);
            Rect band = new Rect(Screen.width * 0.5f - 300f, 24f, 600f, 34f);
            GuiKit.Fill(band, new Color(0f, 0f, 0f, 0.6f));
            GuiKit.OutlinedLabel(band, text, _banner,
                new Color(1f, 0.85f, 0.4f, pulse), new Color(0f, 0f, 0f, 0.8f), 1f);
        }
    }
}
