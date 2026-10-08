using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Un commerce de la ville. Son comptoir (E) ouvre l'écran du magasin ; son vendeur salue
    /// quand on entre.
    ///
    /// Deux sortes de magasins :
    /// - ceux dont la carte a un vrai intérieur : on y entre par la porte, le comptoir est
    ///   posé à la caisse ;
    /// - ceux dont la carte n'a qu'une façade : la porte mène (fondu au noir) à un intérieur
    ///   construit pour le jeu, à l'écart de la ville (<see cref="Interior"/>), comme le Vertigo.
    /// </summary>
    public class Shop : MonoBehaviour
    {
        [SerializeField] private string _displayName = "Magasin";
        [SerializeField] private ShopKind _kind;

        [SerializeField]
        [Tooltip("Batiment de la ville (« Casino », « North town/Pizzeria », « @Businesses/Laundromat »).")]
        private string _building;

        [SerializeField]
        [Tooltip("Si rempli : seules les portes dont le chemin contient ce texte menent ici (l'accueil du motel).")]
        private string _doorFilter;

        [SerializeField] private Interactable _counter;
        [SerializeField] private ShopScreen _screen;
        [SerializeField] private ShopClerk _clerk;

        [Header("Interieur a part (facades)")]
        [SerializeField] private GameObject _interior;
        [SerializeField] private Transform _arrival;
        [SerializeField] private Interactable _exit;

        public string DisplayName { get { return _displayName; } }
        public ShopKind Kind { get { return _kind; } }
        public string Building { get { return _building; } }

        /// <summary>Cette porte de la ville (bâtiment, chemin) est-elle une porte de ce magasin ?</summary>
        public bool Owns(string building, string path)
        {
            if (building != _building) return false;
            return string.IsNullOrEmpty(_doorFilter) || (path != null && path.Contains(_doorFilter));
        }
        public GameObject Interior { get { return _interior; } }
        public Transform Arrival { get { return _arrival; } }
        public Interactable Exit { get { return _exit; } }
        public ShopClerk Clerk { get { return _clerk; } }
        public bool HasInterior { get { return _interior != null && _arrival != null; } }
        public Interactable Counter { get { return _counter; } }

        private ShopHours.Hours _hours;
        private bool _hoursReady;

        /// <summary>Les horaires d'ouverture (voir <see cref="ShopHours"/>).</summary>
        public ShopHours.Hours Hours
        {
            get
            {
                if (!_hoursReady)
                {
                    _hours = ShopHours.For(_kind, _displayName);
                    _hoursReady = true;
                }

                return _hours;
            }
        }

        /// <summary>Ouvert en ce moment (tenu à jour par <see cref="ShopDirectory"/>).</summary>
        public bool IsOpen { get; private set; } = true;

        public void SetOpen(bool open)
        {
            IsOpen = open;
            if (_clerk != null && _clerk.gameObject.activeSelf != open && !(open == false && _screen != null && _screen.IsServing(this)))
            {
                _clerk.gameObject.SetActive(open);
            }
        }

        private void Awake()
        {
            if (_clerk != null) _clerk.Bind(this);
        }

        private void OnEnable()
        {
            if (_counter != null) _counter.Activated += OnCounter;
        }

        private void OnDisable()
        {
            if (_counter != null) _counter.Activated -= OnCounter;
        }

        private void OnCounter(Interactable source)
        {
            if (_screen != null && IsOpen) _screen.Open(this);
        }

        /// <summary>Le constructeur de la scène y écrit le magasin.</summary>
        public void Configure(string displayName, ShopKind kind, string building, Interactable counter, ShopScreen screen,
            ShopClerk clerk, GameObject interior, Transform arrival, Interactable exit, string doorFilter)
        {
            _doorFilter = doorFilter;
            _displayName = displayName;
            _kind = kind;
            _building = building;
            _counter = counter;
            _screen = screen;
            _clerk = clerk;
            _interior = interior;
            _arrival = arrival;
            _exit = exit;
        }
    }
}
