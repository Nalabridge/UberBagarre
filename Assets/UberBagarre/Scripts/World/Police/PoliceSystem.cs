using System.Collections;
using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Enemy;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La police d'Hyland.
    ///
    /// **Ce qui déclenche** : un délit (<see cref="Crimes"/>) vu par la police — tout de suite —
    /// ou par des passants. Un témoin s'arrête et appelle : si on le laisse faire, les étoiles
    /// tombent au bout d'une quinzaine de secondes (moins si le casier est lourd). Si on file
    /// hors de sa vue, ou si on va lui « parler » (E : « T'as rien vu. »), pas d'appel.
    ///
    /// **Ce que fait la police** selon les étoiles :
    /// ★ une patrouille à pied vient voir ; les agents veulent te parler — tu peux te rendre
    ///   (E), glisser un billet (R, 200 €), ou fuir ;
    /// ★★ deux voitures, sirènes ; on t'interpelle de force ;
    /// ★★★ renforts, poursuites en voiture ;
    /// ★★★★ la brigade de Brandt : des flics costauds qui cognent comme des boss ;
    /// ★★★★★ toute la police de la ville.
    ///
    /// **Fuir et se cacher** : hors de vue, un cercle de recherche apparaît sur la carte. Il faut
    /// en sortir et attendre qu'il s'efface ; se cacher dans un magasin, chez soi, changer de
    /// tenue ou de voiture accélère les choses.
    ///
    /// **Se faire arrêter** (se rendre, ou K.O. face à la police) : une nuit au poste, une
    /// amende (10 % de l'argent), de la réputation en moins, l'appli suspendue jusqu'au
    /// lendemain, et le casier qui s'alourdit. Maître Lenoir, l'avocat, le nettoie — cher.
    /// </summary>
    public class PoliceSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _player;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerProgress _progress;
        [SerializeField] private SubtitleDisplay _subtitles;
        [SerializeField] private ScreenFader _fader;
        [SerializeField] private OpenWorldDirector _director;

        [Header("Unités")]
        [SerializeField] private GameObject[] _officerTemplates = new GameObject[0];
        [SerializeField] private GameObject _brandtTemplate;
        [SerializeField] private GameObject _carTemplate;
        [SerializeField] private Vector3[] _footSpawns = new Vector3[0];
        [SerializeField] private Vector3[] _carSpawns = new Vector3[0];
        [SerializeField] private Transform _release;

        [Header("Réglages")]
        [SerializeField, Min(3f)] private float _callDuration = 15f;
        [SerializeField, Min(5f)] private float _witnessRange = 24f;
        [SerializeField, Min(5f)] private float _policeSight = 38f;
        [SerializeField, Min(0)] private int _bribe = 200;

        private static readonly int[] OfficersFor = { 0, 2, 3, 4, 5, 6 };
        private static readonly int[] CarsFor = { 0, 0, 1, 2, 2, 3 };

        private class Witness
        {
            public MocapWalker walker;
            public Crime crime;
            public Vector3 place;
            public float progress;
            public float unseenFor;
        }

        private readonly List<Witness> _witnesses = new List<Witness>();
        private readonly List<PoliceOfficer> _officers = new List<PoliceOfficer>();
        private readonly List<PoliceCar> _cars = new List<PoliceCar>();
        private readonly List<string> _episode = new List<string>();

        private int _stars;
        private Vector3 _lastSeen;
        private float _unseenFor;
        private float _escape;
        private bool _outfitUsed;
        private float _nextSpawn;
        private bool _arresting;
        private float _flash;
        private float _noticeUntil;
        private string _notice;
        private DrivableCar _lastCar;
        private AudioSource _radio;
        private AudioClip _radioClip;

        public static PoliceSystem Instance { get; private set; }

        /// <summary>Les étoiles de recherche (0 à 5).</summary>
        public int Stars { get { return _stars; } }

        public bool Wanted { get { return _stars > 0; } }

        /// <summary>Le joueur est-il vu par un policier en ce moment ?</summary>
        public bool Seen { get { return _unseenFor < 0.5f; } }

        /// <summary>Le centre du cercle de recherche (dernière position connue).</summary>
        public Vector3 LastSeen { get { return _lastSeen; } }

        public float SearchRadius { get { return 45f + 20f * _stars; } }

        /// <summary>Un agent parle au joueur (pour l'affichage du choix).</summary>
        public PoliceOfficer Talking { get; set; }

        /// <summary>
        /// La chasse à l'homme de Brandt (l'histoire) : les étoiles ne redescendent jamais sous
        /// ce niveau. 0 = pas de chasse. Perdre sa trace les envoie chercher ailleurs, c'est tout.
        /// </summary>
        public int Floor { get; set; }

        /// <summary>Plus de pots-de-vin : les flics de Brandt savent (le carnet est chez Duval).</summary>
        public bool NoBribes { get; set; }

        private float _graceUntil;
        private readonly List<Vector3> _hideouts = new List<Vector3>();

        /// <summary>Une planque d'allié (la cave de M. Chen, l'arrière du Taco Ticklers) : on s'y fait oublier vite.</summary>
        public void AddHideout(Vector3 at)
        {
            for (int i = 0; i < _hideouts.Count; i++) if ((_hideouts[i] - at).sqrMagnitude < 1f) return;
            _hideouts.Add(at);
        }

        /// <summary>Quelqu'un couvre le joueur (Duval appelle le central) : plus d'étoiles, et un répit.</summary>
        public void Grace(float seconds, string reason)
        {
            _graceUntil = Time.time + seconds;
            Clear(reason);
        }

        public Combatant Player { get { return _player; } }

        /// <summary>Une interpellation est en cours (fondu, garde à vue).</summary>
        public bool Arresting { get { return _arresting; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Instance = null;
        }

        private void Awake()
        {
            Instance = this;
            _radio = gameObject.AddComponent<AudioSource>();
            _radio.playOnAwake = false;
            _radio.spatialBlend = 0f;
            _radioClip = RadioClip();
        }

        private void OnEnable()
        {
            Crimes.Committed += OnCrime;
            HealthSystem.AnyHitWhileDown += OnHitWhileDown;
        }

        private void OnDisable()
        {
            Crimes.Committed -= OnCrime;
            HealthSystem.AnyHitWhileDown -= OnHitWhileDown;
        }

        private float _lastBeating = -100f;

        /// <summary>S'acharner sur quelqu'un au sol : c'est un délit à part, et ça se voit.</summary>
        private void OnHitWhileDown(HealthSystem victim, DamageInfo info)
        {
            if (info.AttackerFaction != Faction.Player || victim == null) return;
            if (_player != null && victim == _player.Health) return;
            if (Time.time - _lastBeating < 6f) return;
            _lastBeating = Time.time;
            if (_progress != null) _progress.ChangeCode(-2, "Acharnement");
            Crimes.Report(Crime.Acharnement, victim.transform.position, victim.gameObject);
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_radioClip != null) Destroy(_radioClip);
        }

        private void Start()
        {
            LoadingScreen.AddTip("Un témoin qui sort son téléphone appelle la police : file hors de sa vue, ou va lui dire qu'il n'a rien vu.");
            LoadingScreen.AddTip("Recherché : sors du cercle sur la carte et fais-toi oublier. Un magasin, ta planque, une autre tenue aident.");
            LoadingScreen.AddTip("Maître Lenoir, l'avocat, nettoie le casier judiciaire. Pas donné.");
        }

        // ------------------------------------------------------------------ délits

        private static int Severity(Crime crime)
        {
            switch (crime)
            {
                case Crime.Carjacking: return 2;
                case Crime.Delit: return 2;
                case Crime.AgressionPolicier: return 3;
                case Crime.Acharnement: return 2;
                default: return 1;
            }
        }

        private void OnCrime(Crime crime, Vector3 place, GameObject victim)
        {
            if (_arresting || _player == null) return;

            // Frapper un agent : il est déjà là, pas besoin de témoin.
            if (crime == Crime.AgressionPolicier)
            {
                AddStars(Mathf.Max(_stars + 1, Severity(crime)), place, crime);
                return;
            }

            // Un policier voit la scène : tout de suite.
            for (int i = 0; i < _officers.Count; i++)
            {
                PoliceOfficer officer = _officers[i];
                if (officer != null && officer.CanSee(place, _policeSight))
                {
                    AddStars(Mathf.Max(_stars, Severity(crime)), place, crime);
                    return;
                }
            }

            // Sinon, les passants qui ont vu sortent leur téléphone (deux au plus).
            int callers = 0;
            MocapWalker[] walkers = FindObjectsByType<MocapWalker>(FindObjectsSortMode.None);
            for (int i = 0; i < walkers.Length && callers < 2; i++)
            {
                MocapWalker walker = walkers[i];
                if (walker == null || !walker.isActiveAndEnabled || walker.IsDown) continue;
                if (victim != null && walker.gameObject == victim) continue;
                if (walker.GetComponent<Story.StoryActor>() != null) continue;
                if (IsCalling(walker)) continue;
                Vector3 eyes = walker.transform.position + Vector3.up * 1.6f;
                if ((eyes - place).sqrMagnitude > _witnessRange * _witnessRange) continue;
                if (!Clear(eyes, place + Vector3.up * 1.1f)) continue;

                walker.HoldStill(_callDuration + 3f);
                _witnesses.Add(new Witness { walker = walker, crime = crime, place = place });
                callers++;
            }

            // Personne n'a rien vu : ça reste au casier de la conscience, pas à celui de la police.
        }

        private bool IsCalling(MocapWalker walker)
        {
            for (int i = 0; i < _witnesses.Count; i++)
            {
                if (_witnesses[i].walker == walker) return true;
            }

            return false;
        }

        /// <summary>Le joueur est allé « parler » au témoin : il range son téléphone.</summary>
        public bool Intimidate(MocapWalker walker)
        {
            for (int i = 0; i < _witnesses.Count; i++)
            {
                if (_witnesses[i].walker != walker) continue;
                _witnesses.RemoveAt(i);
                walker.Release();
                walker.Hurry(10f);
                Say("MOI", "T'as rien vu. Pas vrai ?");
                if (_progress != null) _progress.ChangeCode(-1, "Intimidation");
                return true;
            }

            return false;
        }

        private void UpdateWitnesses(float dt)
        {
            float duration = _callDuration * (_progress != null && _progress.RecordWeight >= 10 ? 0.65f : 1f);
            Vector3 player = PlayerPosition;

            for (int i = _witnesses.Count - 1; i >= 0; i--)
            {
                Witness w = _witnesses[i];
                if (w.walker == null || !w.walker.isActiveAndEnabled || w.walker.IsDown)
                {
                    _witnesses.RemoveAt(i);
                    continue;
                }

                Vector3 eyes = w.walker.transform.position + Vector3.up * 1.6f;

                // Tout près de lui (à pied) : il comprend le message.
                if (!PlayerDriving.IsDriving && (player - w.walker.transform.position).sqrMagnitude < 3f * 3f)
                {
                    Intimidate(w.walker);
                    continue;
                }

                // Hors de sa vue assez longtemps, et loin : il n'a qu'une description vague.
                bool sees = (eyes - player).sqrMagnitude < 45f * 45f && Clear(eyes, player + Vector3.up * 1.2f);
                w.unseenFor = sees ? 0f : w.unseenFor + dt;
                if (w.unseenFor > 3f && (eyes - player).sqrMagnitude > 35f * 35f)
                {
                    w.walker.Release();
                    _witnesses.RemoveAt(i);
                    continue;
                }

                w.progress += dt / duration;
                if (w.progress < 1f) continue;

                w.walker.Release();
                _witnesses.RemoveAt(i);
                AddStars(Mathf.Max(_stars, Severity(w.crime)), sees ? player : w.place, w.crime);
                Radio("Central à toutes les unités : " + Crimes.Describe(w.crime).ToLowerInvariant() + " signalé. Suspect : homme, la trentaine, Hyland.");
            }
        }

        // ------------------------------------------------------------------ étoiles

        public void AddStars(int level, Vector3 at, Crime crime)
        {
            int before = _stars;
            _stars = Mathf.Clamp(Mathf.Max(_stars, level), 0, 5);
            _lastSeen = at;
            _unseenFor = 0f;
            _escape = 0f;
            _episode.Add(Crimes.Describe(crime));
            if (_progress != null) _progress.ChangeCode(-1, Crimes.Describe(crime));

            if (_stars > before)
            {
                _flash = 1f;
                Notice(new string('★', _stars) + "  RECHERCHÉ");
                _nextSpawn = Time.time + (before == 0 ? 3f : 1f);
            }
        }

        /// <summary>La police perd ta trace (ou une arrangement a été trouvé).</summary>
        public void Clear(string reason)
        {
            if (_stars == 0) return;
            _stars = 0;
            _episode.Clear();
            _outfitUsed = false;
            Notice(reason);
            for (int i = 0; i < _officers.Count; i++) if (_officers[i] != null) _officers[i].StandDown();
            for (int i = 0; i < _cars.Count; i++) if (_cars[i] != null) _cars[i].StandDown();
        }

        /// <summary>Changer de tenue, hors de vue : une étoile de moins (une fois par recherche).</summary>
        public void OutfitChanged()
        {
            if (!Wanted || Seen || _outfitUsed) return;
            _outfitUsed = true;
            _stars = Mathf.Max(0, _stars - 1);
            _escape += 8f;
            Notice(_stars == 0 ? "Nouvelle tenue : plus personne ne te reconnaît." : "Nouvelle tenue : une étoile de moins.");
        }

        // ------------------------------------------------------------------ cycle

        private void Update()
        {
            if (GameMenu.IsPaused || _player == null) return;
            float dt = Time.deltaTime;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 0.8f);

            UpdateWitnesses(dt);
            Prune();

            // La chasse à l'homme : jamais moins que le plancher (sauf un répit accordé).
            if (Floor > 0 && !_arresting && _stars < Floor && Time.time >= _graceUntil)
            {
                _stars = Mathf.Clamp(Floor, 0, 5);
                _lastSeen = PlayerPosition;
                _unseenFor = 0f;
                _escape = 0f;
                _flash = 1f;
                Notice(new string('★', _stars) + "  CHASSE À L'HOMME");
                _nextSpawn = Time.time + 3f;
            }

            if (_stars > 0 && !_arresting)
            {
                UpdateSight(dt);
                UpdateSearch(dt);
                UpdateUnits();
            }

            UpdateMap();
            WatchCar();
        }

        private void Prune()
        {
            for (int i = _officers.Count - 1; i >= 0; i--) if (_officers[i] == null) _officers.RemoveAt(i);
            for (int i = _cars.Count - 1; i >= 0; i--) if (_cars[i] == null) _cars.RemoveAt(i);
        }

        private void UpdateSight(float dt)
        {
            Vector3 player = PlayerPosition;
            bool seen = false;
            for (int i = 0; i < _officers.Count && !seen; i++)
            {
                if (_officers[i] != null && _officers[i].CanSee(player, _policeSight)) seen = true;
            }

            for (int i = 0; i < _cars.Count && !seen; i++)
            {
                if (_cars[i] != null && _cars[i].CanSee(player, _policeSight + 15f)) seen = true;
            }

            if (seen)
            {
                _unseenFor = 0f;
                _lastSeen = player;
                _escape = 0f;
            }
            else
            {
                _unseenFor += dt;
            }
        }

        /// <summary>
        /// Hors de vue : on sort du cercle et on attend. Caché (un magasin, chez soi, le club),
        /// ça va trois fois plus vite.
        /// </summary>
        private void UpdateSearch(float dt)
        {
            if (_unseenFor < 2f) return;

            Vector3 player = PlayerPosition;
            float distance = Flat(player - _lastSeen).magnitude;
            bool outside = distance > SearchRadius;
            bool hidden = (ShopDirectory.Instance != null && ShopDirectory.Instance.Inside != null) || HiddenAtHome(player) ||
                          InHideout(player);

            float rate = outside ? 1f : 0.35f;
            if (hidden) rate *= 3f;
            _escape += dt * rate;

            float needed = 18f + 7f * _stars;
            if (_escape < needed) return;

            if (Floor > 0 && Time.time >= _graceUntil)
            {
                // Pendant la chasse, ils ne lâchent pas : ils cherchent ailleurs.
                Vector2 away = Random.insideUnitCircle.normalized * (SearchRadius + 80f);
                _lastSeen = player + new Vector3(away.x, 0f, away.y);
                _escape = 0f;
                Notice("Ils ont perdu ta trace. La chasse continue.");
                return;
            }

            Clear(hidden ? "Planqué : la police a laissé tomber." : "La police a perdu ta trace.");
        }

        private bool InHideout(Vector3 player)
        {
            for (int i = 0; i < _hideouts.Count; i++) if (Flat(_hideouts[i] - player).sqrMagnitude < 8f * 8f) return true;
            return false;
        }

        private static bool HiddenAtHome(Vector3 player)
        {
            HomeRegistry homes = FindAnyObjectByType<HomeRegistry>();
            HomeRegistry.Home home = homes != null ? homes.Current : null;
            return home != null && home.arrival != null && Vector3.Distance(home.arrival.position, player) < 7f;
        }

        /// <summary>Monter dans une autre voiture, hors de vue : ils cherchent la mauvaise.</summary>
        private void WatchCar()
        {
            DrivableCar car = DrivableCar.Driven;
            if (car == _lastCar) return;
            if (car != null && Wanted && !Seen && _lastCar != null) _escape += 10f;
            _lastCar = car;
        }

        // ------------------------------------------------------------------ unités

        private void UpdateUnits()
        {
            if (Time.time < _nextSpawn) return;
            _nextSpawn = Time.time + 2.5f;

            int officers = 0;
            for (int i = 0; i < _officers.Count; i++) if (_officers[i] != null && _officers[i].OnDuty) officers++;
            int cars = 0;
            for (int i = 0; i < _cars.Count; i++) if (_cars[i] != null && _cars[i].OnDuty) cars++;

            if (cars < CarsFor[_stars] && _carTemplate != null) SpawnCar();
            else if (officers < OfficersFor[_stars] && _officerTemplates.Length > 0) SpawnOfficer(SpawnPoint(_footSpawns, 30f, 60f), false);
        }

        /// <summary>Un point d'apparition hors de vue, entre deux distances du joueur.</summary>
        private Vector3 SpawnPoint(Vector3[] points, float min, float max)
        {
            Vector3 player = PlayerPosition;
            Camera camera = Camera.main;
            Vector3 fallback = player + Random.onUnitSphere * min;
            fallback.y = player.y;
            if (points == null || points.Length == 0) return fallback;

            for (int tries = 0; tries < 40; tries++)
            {
                Vector3 p = points[Random.Range(0, points.Length)];
                float d = Flat(p - player).magnitude;
                if (d < min || d > max) continue;
                if (camera != null)
                {
                    Vector3 v = camera.WorldToViewportPoint(p + Vector3.up);
                    bool onScreen = v.z > 0f && v.x > -0.1f && v.x < 1.1f && v.y > -0.1f && v.y < 1.1f;
                    if (onScreen && Clear(camera.transform.position, p + Vector3.up)) continue;
                }

                return p;
            }

            return fallback;
        }

        public PoliceOfficer SpawnOfficer(Vector3 at, bool fromCar)
        {
            bool brigade = _stars >= 4;
            GameObject template = brigade && _brandtTemplate != null ? _brandtTemplate : _officerTemplates[Random.Range(0, _officerTemplates.Length)];
            if (template == null) return null;

            Vector3 toPlayer = Flat(PlayerPosition - at);
            Quaternion rotation = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer.normalized) : Quaternion.identity;
            GameObject go = Instantiate(template, at, rotation, transform);
            go.name = brigade ? "Brigade de Brandt" : "Agent de police";
            go.SetActive(true);

            Core.ISpawnReceiver[] receivers = go.GetComponentsInChildren<Core.ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnSpawned(at, rotation);

            PoliceOfficer officer = go.GetComponent<PoliceOfficer>();
            if (officer == null) officer = go.AddComponent<PoliceOfficer>();
            officer.Begin(this, _stars, brigade);
            _officers.Add(officer);
            return officer;
        }

        private void SpawnCar()
        {
            Vector3 at = SpawnPoint(_carSpawns, 55f, 95f);
            Vector3 toPlayer = Flat(PlayerPosition - at);
            Quaternion rotation = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer.normalized) : Quaternion.identity;

            GameObject go = Instantiate(_carTemplate, at + Vector3.up * 0.3f, rotation, transform);
            go.name = "Voiture de police";
            go.SetActive(true);
            PoliceCar car = go.GetComponent<PoliceCar>();
            if (car == null) car = go.AddComponent<PoliceCar>();
            car.Begin(this);
            _cars.Add(car);
        }

        // ------------------------------------------------------------------ interpellation

        /// <summary>Le joueur est K.O. : si la police est là, c'est elle qui le ramasse.</summary>
        public bool ClaimKnockout()
        {
            if (!Wanted || _player == null) return false;

            Vector3 player = PlayerPosition;
            for (int i = 0; i < _officers.Count; i++)
            {
                if (_officers[i] != null && Flat(_officers[i].transform.position - player).magnitude < 20f)
                {
                    StartCoroutine(Arrest(false));
                    return true;
                }
            }

            return false;
        }

        /// <summary>Le joueur se rend (E devant un agent).</summary>
        public void Surrender()
        {
            if (_arresting) return;
            StartCoroutine(Arrest(true));
        }

        /// <summary>Un billet pour qu'ils regardent ailleurs (R devant un agent).</summary>
        public bool Bribe()
        {
            if (_arresting || _progress == null || _stars > 2) return false;
            if (NoBribes || Floor > 0)
            {
                Say("AGENT", "Ton fric, garde-le. Le commissaire veut ta peau, pas ton portefeuille.");
                return false;
            }

            if (!_progress.Spend(_bribe, "Arrangement (police)"))
            {
                Say("AGENT", "Tu te fous de moi ? T'as même pas de quoi.");
                return false;
            }

            _progress.ChangeCode(-6, "Corruption");
            _progress.SetFlag("police:corruption");
            Say("AGENT", "On t'a pas vu. Et toi non plus, tu nous as pas vus.");
            Clear("Arrangement : la patrouille regarde ailleurs.");
            return true;
        }

        private IEnumerator Arrest(bool surrendered)
        {
            _arresting = true;
            if (_input != null) _input.SetGameplayLock(this, true);
            Say("AGENT", surrendered ? "Mains derrière le dos. Doucement." : "Il est à nous. Embarquez-le.");
            yield return new WaitForSeconds(1.6f);

            if (_fader != null)
            {
                _fader.FadeOut(1.2f);
                yield return new WaitForSeconds(1.4f);
                _fader.ShowCard("Garde à vue.\nCommissariat d'Hyland.");
            }

            // Le casier retient tout ce qui s'est passé.
            if (_progress != null)
            {
                for (int i = 0; i < _episode.Count; i++) _progress.AddToRecord(_episode[i], 1 + _stars / 2);
                int fine = Mathf.RoundToInt(_progress.Money * 0.1f / 10f) * 10;
                if (fine > 0) _progress.AddMoney(-fine, "Amende — Commissariat");
                _progress.ChangeReputation(-8, "Arrestation");
                _progress.NightInCell();

                yield return new WaitForSeconds(2.4f);
                if (_fader != null) _fader.ShowCard("Relâché au petit matin.\nAmende : " + fine + " €.  Appli suspendue pour la journée.");
            }

            _episode.Clear();
            _stars = 0;
            _witnesses.Clear();
            for (int i = 0; i < _officers.Count; i++) if (_officers[i] != null) Destroy(_officers[i].gameObject);
            for (int i = 0; i < _cars.Count; i++) if (_cars[i] != null) Destroy(_cars[i].gameObject);
            _officers.Clear();
            _cars.Clear();

            if (PlayerDriving.Instance != null && PlayerDriving.IsDriving) PlayerDriving.Instance.ForceExit();
            if (WorldClock.Instance != null) WorldClock.Instance.SetHour(7f);
            if (_director != null) _director.RestoreAfterArrest(_release);

            yield return new WaitForSeconds(2.2f);
            if (_fader != null)
            {
                _fader.ShowCard(null);
                _fader.FadeIn(1.4f);
            }

            if (_input != null) _input.SetGameplayLock(this, false);
            _arresting = false;
            // Relâché : de quoi sortir du commissariat avant que la chasse reprenne.
            _graceUntil = Time.time + 90f;
            Say("MOI", "Une nuit sur un banc en ferraille. Il me faut un café. Et un avocat.");
        }

        // ------------------------------------------------------------------ carte, HUD

        private void UpdateMap()
        {
            CityMap.Overlay.RemoveAll(b => b.color == SearchColor || b.color == OfficerColor || b.color == WitnessColor);
            if (_stars > 0 && _unseenFor >= 2f)
            {
                CityMap.Overlay.Add(new CityMap.Blip { position = new Vector2(_lastSeen.x, _lastSeen.z), color = SearchColor, radius = SearchRadius });
            }

            for (int i = 0; i < _officers.Count; i++)
            {
                if (_officers[i] == null || !_officers[i].OnDuty) continue;
                Vector3 p = _officers[i].transform.position;
                CityMap.Overlay.Add(new CityMap.Blip { position = new Vector2(p.x, p.z), color = OfficerColor, size = 9f });
            }

            for (int i = 0; i < _cars.Count; i++)
            {
                if (_cars[i] == null || !_cars[i].OnDuty) continue;
                Vector3 p = _cars[i].transform.position;
                CityMap.Overlay.Add(new CityMap.Blip { position = new Vector2(p.x, p.z), color = OfficerColor, size = 12f });
            }

            for (int i = 0; i < _witnesses.Count; i++)
            {
                if (_witnesses[i].walker == null) continue;
                Vector3 p = _witnesses[i].walker.transform.position;
                CityMap.Overlay.Add(new CityMap.Blip { position = new Vector2(p.x, p.z), color = WitnessColor, size = 8f });
            }
        }

        private static readonly Color SearchColor = new Color(1f, 0.25f, 0.25f, 0.9f);
        private static readonly Color OfficerColor = new Color(0.3f, 0.55f, 1f, 1f);
        private static readonly Color WitnessColor = new Color(1f, 0.85f, 0.2f, 1f);

        private void OnGUI()
        {
            if (GameMenu.IsOpen || ModalScreen.Active || FightIntro.AnyPlaying) return;
            float u = Mathf.Max(0.6f, Screen.height / 1080f);

            DrawWitnesses(u);
            DrawStars(u);
            DrawTalk(u);

            if (!string.IsNullOrEmpty(_notice) && Time.unscaledTime < _noticeUntil)
            {
                Rect r = new Rect(Screen.width * 0.5f - 250f * u, 112f * u, 500f * u, 36f * u);
                GuiKit.Rounded(r, new Color(0.07f, 0.064f, 0.1f, 0.85f), 18f * u);
                GuiKit.RoundedOutline(r, new Color(0.4f, 0.6f, 1f, 0.6f), 18f * u, 1f);
                GuiKit.ShadowLabel(r, _notice, GuiKit.Text(Mathf.RoundToInt(17 * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter), Color.white, 0.4f);
            }
        }

        /// <summary>
        /// Les étoiles, en haut à droite sous l'argent, comme dans GTA : pleines pour le niveau
        /// de recherche, elles clignotent rouge et bleu quand on est vu ; dessous, où on en est.
        /// </summary>
        private void DrawStars(float u)
        {
            if (_stars <= 0) return;
            u = UI.UiTheme.Unit;

            float right = Screen.width - 32f * u;
            float y = UI.UiTheme.TopRight(96f);
            bool blink = Seen && Mathf.Repeat(Time.unscaledTime * 2.5f, 1f) < 0.5f;
            Color siren = blink ? new Color(1f, 0.3f, 0.28f) : new Color(0.4f, 0.62f, 1f);

            GUIStyle stars = GuiKit.Text(Mathf.RoundToInt(26 * u), GuiKit.Weight.Black, TextAnchor.MiddleRight);
            string full = new string('★', _stars);
            string empty = new string('★', 5 - _stars);
            float emptyWidth = stars.CalcSize(new GUIContent(empty)).x;
            GuiKit.ShadowLabel(new Rect(right - 300f * u, y, 300f * u, 30f * u), empty, stars, new Color(1f, 1f, 1f, 0.22f), 0.5f);
            GuiKit.ShadowLabel(new Rect(right - 300f * u - emptyWidth, y, 300f * u, 30f * u), full, stars,
                Seen ? Color.Lerp(Color.white, siren, 0.35f + 0.4f * _flash) : new Color(1f, 1f, 1f, 0.8f), 0.6f);

            GUIStyle small = GuiKit.Text(Mathf.RoundToInt(13 * u), GuiKit.Weight.Medium, TextAnchor.MiddleRight);
            string state = Seen ? "Ils te voient" : Flat(PlayerPosition - _lastSeen).magnitude > SearchRadius
                ? "Hors du cercle  ·  " + Mathf.RoundToInt(Mathf.Clamp01(_escape / (18f + 7f * _stars)) * 100f) + " %"
                : "Recherche en cours  ·  sors du cercle";
            GuiKit.ShadowLabel(new Rect(right - 400f * u, y + 30f * u, 400f * u, 18f * u), state, small, Seen ? siren : UI.UiTheme.InkDim, 0.6f);
        }

        /// <summary>Au-dessus des témoins qui appellent : un combiné et l'appel qui avance.</summary>
        private void DrawWitnesses(float u)
        {
            Camera camera = Camera.main;
            if (camera == null) return;
            GUIStyle style = GuiKit.Text(Mathf.RoundToInt(13 * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter);

            for (int i = 0; i < _witnesses.Count; i++)
            {
                Witness w = _witnesses[i];
                if (w.walker == null) continue;
                Vector2 at;
                if (!GuiKit.WorldToGui(camera, w.walker.transform.position + Vector3.up * 2.15f, out at)) continue;

                Rect pill = new Rect(at.x - 58f * u, at.y - 14f * u, 116f * u, 26f * u);
                GuiKit.Rounded(pill, new Color(0.07f, 0.064f, 0.1f, 0.85f), 13f * u);
                GuiKit.Rounded(new Rect(pill.x, pill.y, pill.width * Mathf.Clamp01(w.progress), pill.height),
                    new Color(1f, 0.3f, 0.25f, 0.55f), 13f * u);
                GuiKit.ShadowLabel(pill, "APPELLE LA POLICE", style, Color.white, 0.4f);
            }
        }

        /// <summary>Un agent parle au joueur : se rendre, payer, ou fuir.</summary>
        private void DrawTalk(float u)
        {
            if (Talking == null || _arresting) return;

            Rect r = new Rect(Screen.width * 0.5f - 280f * u, Screen.height * 0.68f, 560f * u, 64f * u);
            GuiKit.Glow(r, new Color(0f, 0f, 0f, 0.5f), 18f * u, 12f * u);
            GuiKit.Rounded(r, new Color(0.07f, 0.064f, 0.1f, 0.92f), 18f * u);
            GuiKit.RoundedOutline(r, new Color(0.3f, 0.55f, 1f, 0.7f), 18f * u, 1.5f * u);
            GUIStyle big = GuiKit.Text(Mathf.RoundToInt(18 * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle small = GuiKit.Text(Mathf.RoundToInt(14 * u), GuiKit.Weight.Medium, TextAnchor.MiddleCenter);
            GuiKit.ShadowLabel(new Rect(r.x, r.y + 6f * u, r.width, 28f * u), "« Police. Pas un geste. »", big, Color.white, 0.4f);
            string bribe = _stars <= 2 ? "   ·   R  glisser " + _bribe + " €" : "";
            GuiKit.ShadowLabel(new Rect(r.x, r.y + 34f * u, r.width, 22f * u), "E  te rendre" + bribe + "   ·   ou fuis", small,
                new Color(0.75f, 0.8f, 1f), 0.4f);
        }

        // ------------------------------------------------------------------ outils

        private void Notice(string text)
        {
            _notice = text;
            _noticeUntil = Time.unscaledTime + 3.5f;
        }

        public void Say(string speaker, string line)
        {
            if (_subtitles != null) _subtitles.Play(DialogueLine.Say(speaker, line));
        }

        private void Radio(string line)
        {
            if (_radio != null && _radioClip != null) _radio.PlayOneShot(_radioClip, 0.35f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
            Say("RADIO", line);
        }

        public Vector3 PlayerPosition
        {
            get
            {
                if (DrivableCar.Driven != null) return DrivableCar.Driven.transform.position;
                return _player != null ? _player.transform.position : Vector3.zero;
            }
        }

        /// <summary>Rien entre deux points (le décor seulement ; les gens et les voitures ne cachent pas).</summary>
        public static bool Clear(Vector3 from, Vector3 to)
        {
            Vector3 d = to - from;
            float length = d.magnitude;
            if (length < 0.1f) return true;

            RaycastHit[] hits = Physics.RaycastAll(from, d / length, length, ~(1 << 2), QueryTriggerInteraction.Ignore);
            for (int i = 0; i < hits.Length; i++)
            {
                Collider c = hits[i].collider;
                if (c == null || c is CharacterController) continue;
                if (c.attachedRigidbody != null) continue;
                if (c.GetComponentInParent<Combatant>() != null || c.GetComponentInParent<MocapWalker>() != null) continue;
                return false;
            }

            return true;
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        /// <summary>Le grésillement bref d'une radio de police.</summary>
        private static AudioClip RadioClip()
        {
            const int rate = 22050;
            int length = rate / 3;
            float[] data = new float[length];
            System.Random random = new System.Random(5);
            for (int n = 0; n < length; n++)
            {
                float t = n / (float)rate;
                float noise = (float)(random.NextDouble() * 2.0 - 1.0);
                float env = t < 0.04f ? t / 0.04f : Mathf.Exp(-(t - 0.04f) * 9f);
                data[n] = (noise * 0.5f + Mathf.Sin(t * 2f * Mathf.PI * 1250f) * 0.2f) * env * 0.5f;
            }

            AudioClip clip = AudioClip.Create("Radio de police", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Configure(Combatant player, PlayerInputReader input, PlayerProgress progress, SubtitleDisplay subtitles,
            ScreenFader fader, OpenWorldDirector director, GameObject[] officers, GameObject brandt, GameObject car,
            Vector3[] footSpawns, Vector3[] carSpawns, Transform release)
        {
            _player = player;
            _input = input;
            _progress = progress;
            _subtitles = subtitles;
            _fader = fader;
            _director = director;
            _officerTemplates = officers ?? new GameObject[0];
            _brandtTemplate = brandt;
            _carTemplate = car;
            _footSpawns = footSpawns ?? new Vector3[0];
            _carSpawns = carSpawns ?? new Vector3[0];
            _release = release;
        }
    }
}
