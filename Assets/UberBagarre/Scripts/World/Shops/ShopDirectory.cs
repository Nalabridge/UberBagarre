using System.Collections;
using System.Collections.Generic;
using UberBagarre.Core;
using UberBagarre.Story;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Branche les portes de la ville sur ce qu'il y a derrière, une fois la ville chargée.
    ///
    /// Chaque façade fermée (<see cref="MapStreamer.Entrances"/>) reçoit une action :
    /// - la porte d'un magasin sans intérieur mène à son intérieur à part (fondu au noir) ; la
    ///   sortie de cet intérieur ramène devant la porte par laquelle on est entré ;
    /// - la porte de service d'un magasin qui a son vrai intérieur reste close (« entre par
    ///   devant ») ;
    /// - une maison, un appartement : on frappe, quelqu'un répond (ou pas) ;
    /// - le reste (mairie, caserne…) est fermé, et le dit.
    ///
    /// Il tient aussi les horaires (<see cref="ShopHours"/>) : un commerce fermé n'a plus de
    /// vendeur derrière le comptoir, ses portes sont verrouillées (« Fermé · ouvre à 8 h »),
    /// la carte le montre grisé ; si on est encore dedans à la fermeture, on nous prie de
    /// sortir.
    /// </summary>
    public class ShopDirectory : MonoBehaviour
    {
        [SerializeField] private Shop[] _shops = new Shop[0];
        [SerializeField] private GameObject _player;
        [SerializeField] private ScreenFader _fader;
        [SerializeField] private SubtitleDisplay _subtitles;
        [SerializeField, Min(0.05f)] private float _fade = 0.35f;

        [SerializeField]
        [Tooltip("Batiments dont on ne touche pas les portes (la facade du Vertigo a la sienne).")]
        private string[] _ignore = { "North town/Nightclub" };

        private readonly List<Interactable> _wired = new List<Interactable>();
        private readonly Dictionary<Shop, List<Interactable>> _fronts = new Dictionary<Shop, List<Interactable>>();
        private readonly Dictionary<Shop, List<SwingDoor>> _doors = new Dictionary<Shop, List<SwingDoor>>();
        private readonly Dictionary<Shop, bool> _state = new Dictionary<Shop, bool>();
        private PlayerProgress _progress;
        private float _nextHours;
        private float _evictAt = -1f;
        private Shop _inside;
        private Vector3 _returnPosition;
        private Quaternion _returnRotation;
        private bool _busy;
        private int _knocks;
        private AudioClip _knock;

        public static ShopDirectory Instance { get; private set; }

        /// <summary>Le magasin (à intérieur à part) où se trouve le joueur, ou null.</summary>
        public Shop Inside { get { return _inside; } }

        public IReadOnlyList<Shop> Shops { get { return _shops; } }

        private static readonly string[] Homes =
        {
            "Residential", "Slums", "Apartment Building", "ApartmentBuilding1", "Upscale Apartments", "Building7_C",
            "Mayor house", "Modern mansion", "Encampment", "Cabin", "North town/North apartments", "Towers",
            "Overpass Building Complex"
        };

        private static readonly string[] Answers =
        {
            "... Personne ne répond.",
            "Une voix derrière la porte : « On n'achète rien ! »",
            "Un chien aboie. Personne ne vient.",
            "« C'est pour quoi ? ... Dégage, ou j'appelle les flics. »",
            "Tu entends la télé. Personne ne bouge.",
            "« Si c'est pour le loyer, je suis pas là. »"
        };

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            MapStreamer.Loaded += Wire;
            if (MapStreamer.Ready) Wire();
            CityMap.Hours = HoursFor;

            for (int i = 0; i < _shops.Length; i++)
            {
                Shop shop = _shops[i];
                if (shop == null || shop.Exit == null) continue;
                shop.Exit.Activated += OnExit;
            }
        }

        private void OnDisable()
        {
            MapStreamer.Loaded -= Wire;
            if (CityMap.Hours == (CityMap.PlaceHours)HoursFor) CityMap.Hours = null;
            for (int i = 0; i < _shops.Length; i++)
            {
                if (_shops[i] != null && _shops[i].Exit != null) _shops[i].Exit.Activated -= OnExit;
            }
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            if (_knock != null) Destroy(_knock);
        }

        public Shop Find(string building, string path)
        {
            for (int i = 0; i < _shops.Length; i++)
            {
                if (_shops[i] != null && _shops[i].Owns(building, path)) return _shops[i];
            }

            return null;
        }

        // ------------------------------------------------------------------ portes de la ville

        private void Wire()
        {
            if (_wired.Count > 0) return;

            IReadOnlyList<MapStreamer.Entrance> entrances = MapStreamer.Entrances;
            int shops = 0, homes = 0;
            for (int i = 0; i < entrances.Count; i++)
            {
                MapStreamer.Entrance entrance = entrances[i];
                if (entrance == null || entrance.door == null) continue;

                if (System.Array.IndexOf(_ignore, entrance.building) >= 0) continue;
                Shop shop = Find(entrance.building, entrance.path);
                Interactable door = Prepare(entrance);

                if (shop != null && shop.HasInterior)
                {
                    door.Label = "Entrer";
                    door.Hint = shop.DisplayName + " — " + ShopCatalog.Describe(shop.Kind);
                    MapStreamer.Entrance captured = entrance;
                    door.Activated += s =>
                    {
                        if (shop.IsOpen || StoryNeeds(shop)) Enter(shop, captured);
                        else Rattle(door);
                    };

                    List<Interactable> fronts;
                    if (!_fronts.TryGetValue(shop, out fronts)) _fronts[shop] = fronts = new List<Interactable>();
                    fronts.Add(door);
                    shops++;
                }
                else if (shop != null)
                {
                    door.Label = "Porte de service";
                    door.Hint = "Fermée. " + shop.DisplayName + " : entre par la porte principale.";
                    door.Activated += s => Rattle(door);
                }
                else if (IsHome(entrance.building))
                {
                    door.Label = "Frapper";
                    door.Hint = null;
                    door.Activated += s => Knock(door);
                    homes++;
                }
                else
                {
                    door.Label = "Fermé";
                    door.Hint = "Ce n'est pas ouvert au public.";
                    door.Activated += s => Rattle(door);
                }
            }

            // Les vraies portes (battantes) des magasins qui ont leur intérieur dans la ville :
            // elles se verrouillent à la fermeture.
            foreach (KeyValuePair<string, SwingDoor> pair in MapStreamer.AllDoors)
            {
                if (pair.Value == null) continue;
                Shop shop = Find(CityRules.BuildingOf(pair.Key), pair.Key);
                if (shop == null || shop.HasInterior) continue;
                List<SwingDoor> list;
                if (!_doors.TryGetValue(shop, out list)) _doors[shop] = list = new List<SwingDoor>();
                list.Add(pair.Value);
            }

            _state.Clear();
            _nextHours = 0f;

            Debug.Log("[UberBagarre] Portes de la ville : " + shops + " entrees de magasins, " + homes + " portes de maisons, " +
                      _shops.Length + " magasins.");
        }

        // ------------------------------------------------------------------ horaires

        private float Hour
        {
            get { return WorldClock.Instance != null ? WorldClock.Instance.Hour : 12f; }
        }

        private int Weekday
        {
            get
            {
                if (_progress == null) _progress = FindAnyObjectByType<PlayerProgress>();
                return ShopHours.Weekday(_progress != null ? _progress.Day : 1);
            }
        }

        private CityMap _map;

        /// <summary>
        /// L'histoire a rendez-vous dans ce commerce (le GPS de la course y mène) : il reste
        /// accessible même fermé — une scène de l'histoire ne doit jamais buter sur un horaire.
        /// </summary>
        public bool StoryNeeds(Shop shop)
        {
            if (shop == null) return false;
            if (_map == null) _map = FindAnyObjectByType<CityMap>();
            if (_map == null || !_map.HasWaypoint) return false;

            Vector3 target = _map.WaypointPosition;
            if (shop.Arrival != null && (shop.Arrival.position - target).sqrMagnitude < 30f * 30f) return true;
            if (shop.Clerk != null && (shop.Clerk.transform.position - target).sqrMagnitude < 25f * 25f) return true;

            List<Interactable> fronts;
            if (_fronts.TryGetValue(shop, out fronts))
            {
                for (int i = 0; i < fronts.Count; i++)
                {
                    if (fronts[i] != null && (fronts[i].transform.position - target).sqrMagnitude < 12f * 12f) return true;
                }
            }

            return false;
        }

        /// <summary>« Ouvert · ferme à 22 h », « Fermé · ouvre à 8 h ».</summary>
        public string HoursNote(Shop shop, out bool open)
        {
            return ShopHours.Describe(shop.Hours, Hour, Weekday, out open);
        }

        /// <summary>Pour la carte : les horaires d'un lieu, d'après son nom.</summary>
        private bool HoursFor(string label, out bool open, out string note)
        {
            open = true;
            note = null;
            for (int i = 0; i < _shops.Length; i++)
            {
                Shop shop = _shops[i];
                if (shop == null || shop.DisplayName != label) continue;
                note = HoursNote(shop, out open);
                return true;
            }

            return false;
        }

        private void Update()
        {
            if (Time.unscaledTime >= _nextHours)
            {
                _nextHours = Time.unscaledTime + 1f;
                UpdateHours();
            }

            // Encore dans un magasin fermé : on sort, une fois la conversation finie.
            if (_evictAt > 0f && Time.time >= _evictAt && !_busy)
            {
                if (_inside == null || _inside.IsOpen || StoryNeeds(_inside) || UberBagarre.UI.FullScreenPanel.AnyOpen)
                {
                    if (_inside == null || _inside.IsOpen || StoryNeeds(_inside)) _evictAt = -1f;
                }
                else
                {
                    _evictAt = -1f;
                    MonoBehaviour host = _fader != null ? (MonoBehaviour)_fader : this;
                    host.StartCoroutine(Pass(_inside, false));
                }
            }
        }

        private void UpdateHours()
        {
            float hour = Hour;
            int weekday = Weekday;
            Vector3 player = _player != null ? _player.transform.position : Vector3.zero;

            for (int i = 0; i < _shops.Length; i++)
            {
                Shop shop = _shops[i];
                if (shop == null) continue;

                bool open;
                string note = ShopHours.Describe(shop.Hours, hour, weekday, out open);
                bool known;
                bool was = _state.TryGetValue(shop, out known) ? known : !open;
                bool changed = !_state.ContainsKey(shop) || was != open;
                _state[shop] = open;

                if (changed)
                {
                    shop.SetOpen(open);
                    if (!open && _inside == shop && !StoryNeeds(shop))
                    {
                        if (shop.Clerk != null && shop.Clerk.gameObject.activeInHierarchy) shop.Clerk.Say(ShopTalk.Closing(shop.Kind, Random.Range(0, 100)));
                        else if (_subtitles != null) _subtitles.Play(DialogueLine.Say("", "Le magasin ferme. Il est temps de sortir."));
                        _evictAt = Time.time + 8f;
                    }
                }

                // La porte de façade affiche l'état.
                List<Interactable> fronts;
                if (_fronts.TryGetValue(shop, out fronts))
                {
                    for (int k = 0; k < fronts.Count; k++)
                    {
                        if (fronts[k] == null) continue;
                        fronts[k].Label = open || StoryNeeds(shop) ? "Entrer" : "Fermé";
                        fronts[k].Hint = shop.DisplayName + " — " + (open ? ShopCatalog.Describe(shop.Kind) + " · " + note : note);
                    }
                }

                // Les vraies portes : verrouillées quand c'est fermé — sauf si le joueur est
                // encore dedans (on ne l'enferme pas).
                List<SwingDoor> doors;
                if (_doors.TryGetValue(shop, out doors))
                {
                    bool near = false;
                    for (int k = 0; k < doors.Count && !near; k++)
                    {
                        if (doors[k] != null && (doors[k].transform.position - player).sqrMagnitude < 14f * 14f) near = true;
                    }

                    bool clerkNear = shop.Clerk != null && (shop.Clerk.transform.position - player).sqrMagnitude < 14f * 14f;
                    bool lockIt = !open && !(near && clerkNear) && !StoryNeeds(shop);
                    for (int k = 0; k < doors.Count; k++)
                    {
                        SwingDoor door = doors[k];
                        if (door == null) continue;
                        if (door.Locked != lockIt) door.SetLocked(lockIt, lockIt ? shop.DisplayName + " — " + note : null);
                        if (lockIt && door.IsOpen) door.SetOpen(false, false);
                    }
                }
            }
        }

        /// <summary>Une action sur la façade : visée sur le milieu de la porte, à bonne portée.</summary>
        private Interactable Prepare(MapStreamer.Entrance entrance)
        {
            GameObject go = entrance.door.gameObject;
            Interactable door = go.GetComponent<Interactable>();
            if (door == null) door = go.AddComponent<Interactable>();

            bool any;
            Bounds bounds = CityRules.RendererBounds(entrance.door, out any);
            GameObject focus = new GameObject("Poignee (visee)");
            focus.transform.SetParent(entrance.door, false);
            focus.transform.position = any ? bounds.center : entrance.door.position + Vector3.up * 1.1f;
            door.SetFocus(focus.transform);
            door.Range = 3f;

            // Une façade sans collider ne serait jamais visée : on lui en donne un, fin.
            if (go.GetComponentInChildren<Collider>() == null && any)
            {
                BoxCollider box = go.AddComponent<BoxCollider>();
                box.center = go.transform.InverseTransformPoint(bounds.center);
                box.size = new Vector3(1.2f, 2.1f, 0.2f);
            }

            _wired.Add(door);
            return door;
        }

        private static bool IsHome(string building)
        {
            if (string.IsNullOrEmpty(building)) return false;
            for (int i = 0; i < Homes.Length; i++)
            {
                if (building == Homes[i]) return true;
            }

            return building.StartsWith("Motel");
        }

        // ------------------------------------------------------------------ entrer, sortir

        public void Enter(Shop shop, MapStreamer.Entrance entrance)
        {
            if (_busy || shop == null || !shop.HasInterior || _player == null) return;

            // Retour : sur le trottoir devant la porte, dos à elle.
            Vector3 outward = entrance.access - (entrance.door.Find("Model") != null ? entrance.door.Find("Model").position : entrance.door.position);
            outward.y = 0f;
            if (outward.sqrMagnitude < 0.01f) outward = entrance.door.forward;
            outward.Normalize();
            _returnPosition = entrance.access + outward * 0.5f + Vector3.up * 0.05f;
            _returnRotation = Quaternion.LookRotation(outward, Vector3.up);

            MonoBehaviour host = _fader != null ? (MonoBehaviour)_fader : this;
            host.StartCoroutine(Pass(shop, true));
        }

        private void OnExit(Interactable source)
        {
            if (_busy || _inside == null) return;
            MonoBehaviour host = _fader != null ? (MonoBehaviour)_fader : this;
            host.StartCoroutine(Pass(_inside, false));
        }

        private IEnumerator Pass(Shop shop, bool entering)
        {
            _busy = true;
            if (_fader != null)
            {
                _fader.FadeOut(_fade);
                float waited = 0f;
                while (!_fader.IsBlack && waited < _fade + 1f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            if (entering) shop.Interior.SetActive(true);

            Vector3 position = entering ? shop.Arrival.position : _returnPosition;
            Quaternion rotation = entering ? shop.Arrival.rotation : _returnRotation;
            ISpawnReceiver[] receivers = _player.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnSpawned(position, rotation);

            if (!entering) shop.Interior.SetActive(false);
            _inside = entering ? shop : null;

            yield return null;
            yield return null;
            if (_fader != null) _fader.FadeIn(_fade * 1.4f);
            _busy = false;
        }

        // ------------------------------------------------------------------ maisons

        private void Knock(Interactable door)
        {
            PlayKnock(door.transform.position);
            if (_subtitles == null) return;
            string answer = Answers[((_knocks++ * 7 + door.GetInstanceID()) & 0x7fffffff) % Answers.Length];
            _subtitles.Play(DialogueLine.Say("", answer));
        }

        private void Rattle(Interactable door)
        {
            PlayKnock(door.transform.position);
            if (_subtitles != null && !string.IsNullOrEmpty(door.Hint)) _subtitles.Play(DialogueLine.Say("", door.Hint));
        }

        /// <summary>Le joueur est-il dans l'intérieur à part de ce magasin ?</summary>
        public bool IsInside(Shop shop)
        {
            return shop != null && _inside == shop;
        }

        private void PlayKnock(Vector3 at)
        {
            if (_knock == null) _knock = KnockClip();
            AudioSource.PlayClipAtPoint(_knock, at, 0.8f * Core.GameSettings.Volume(Core.AudioChannel.Effects));
        }

        /// <summary>Trois coups de phalanges sur du bois.</summary>
        private static AudioClip KnockClip()
        {
            const int rate = 22050;
            int length = (int)(rate * 0.7f);
            float[] data = new float[length];
            System.Random random = new System.Random(11);

            for (int k = 0; k < 3; k++)
            {
                int start = (int)(rate * (0.02f + k * 0.2f));
                for (int n = 0; n < rate / 12 && start + n < length; n++)
                {
                    float t = n / (float)rate;
                    float envelope = Mathf.Exp(-t * 55f);
                    float body = Mathf.Sin(2f * Mathf.PI * (180f - t * 300f) * t);
                    float noise = (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 160f);
                    data[start + n] += (body * 0.8f + noise * 0.5f) * envelope * 0.5f;
                }
            }

            AudioClip clip = AudioClip.Create("Porte (on frappe)", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Configure(Shop[] shops, GameObject player, ScreenFader fader, SubtitleDisplay subtitles)
        {
            _shops = shops ?? new Shop[0];
            _player = player;
            _fader = fader;
            _subtitles = subtitles;
        }
    }
}
