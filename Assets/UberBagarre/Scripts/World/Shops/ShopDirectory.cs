using System.Collections;
using System.Collections.Generic;
using UberBagarre.Core;
using UberBagarre.Story;
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
                    door.Activated += s => Enter(shop, captured);
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

            Debug.Log("[UberBagarre] Portes de la ville : " + shops + " entrees de magasins, " + homes + " portes de maisons, " +
                      _shops.Length + " magasins.");
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

        private void PlayKnock(Vector3 at)
        {
            if (_knock == null) _knock = KnockClip();
            AudioSource.PlayClipAtPoint(_knock, at, 0.8f);
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
