using System;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Les logements du joueur : la chambre du motel, puis ce qu'il achète (le bungalow, le
    /// manoir). Chaque logement a son lit (dormir = sauvegarder), son ordinateur, son armoire.
    ///
    /// Ce composant tient la règle d'ensemble :
    /// - on se réveille (après une nuit, un K.O., une mort) dans le logement choisi ;
    /// - les meubles n'existent que dans les logements qu'on possède ;
    /// - la porte d'un logement à vendre reste fermée à clé ; le portail du manoir ne s'ouvre
    ///   qu'à son propriétaire.
    ///
    /// Les portes appartiennent à la ville, qui se charge après la scène : leur état est
    /// réappliqué quand elle est prête (<see cref="MapStreamer.Loaded"/>).
    /// </summary>
    public class HomeRegistry : MonoBehaviour
    {
        [Serializable]
        public class Home
        {
            public string name;

            [Tooltip("Là où l'on se réveille.")]
            public Transform arrival;

            [Tooltip("Les meubles : visibles seulement si le logement est à nous.")]
            public GameObject furniture;

            public Interactable bed;
            public Interactable computer;
            public Interactable wardrobe;

            [Tooltip("Portes de la ville (chemins) : fermées à clé tant que le logement n'est pas à nous.")]
            public string[] doors = new string[0];

            [Tooltip("Objets de la ville (chemins) qui disparaissent pour le propriétaire : le portail.")]
            public string[] openForOwner = new string[0];
        }

        [SerializeField] private PlayerProgress _progress;
        [SerializeField] private OpenWorldDirector _director;
        [SerializeField] private WardrobeScreen _wardrobeScreen;
        [SerializeField] private ComputerScreen _computerScreen;
        [SerializeField] private Home[] _homes = new Home[0];

        private string _appliedSignature;

        public Home Current
        {
            get
            {
                string name = _progress != null ? _progress.Home : null;
                Home found = Get(name);
                return found ?? (_homes.Length > 0 ? _homes[0] : null);
            }
        }

        public bool Has(string name)
        {
            return Get(name) != null;
        }

        public Home Get(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            for (int i = 0; i < _homes.Length; i++)
            {
                if (_homes[i] != null && _homes[i].name == name) return _homes[i];
            }

            return null;
        }

        private void OnEnable()
        {
            for (int i = 0; i < _homes.Length; i++)
            {
                Home home = _homes[i];
                if (home == null) continue;
                if (home.bed != null) home.bed.Activated += OnBed;
                if (_wardrobeScreen != null) _wardrobeScreen.Attach(home.wardrobe);
                if (_computerScreen != null) _computerScreen.Attach(home.computer);
            }

            MapStreamer.Loaded += OnCityLoaded;
            if (_progress != null) _progress.Changed += OnProgressChanged;
        }

        private void OnDisable()
        {
            for (int i = 0; i < _homes.Length; i++)
            {
                if (_homes[i] != null && _homes[i].bed != null) _homes[i].bed.Activated -= OnBed;
            }

            MapStreamer.Loaded -= OnCityLoaded;
            if (_progress != null) _progress.Changed -= OnProgressChanged;
        }

        private void Start()
        {
            Apply();
        }

        private void OnBed(Interactable source)
        {
            if (_director != null) _director.Sleep();
        }

        private void OnCityLoaded()
        {
            _appliedSignature = null;
            Apply();
        }

        private void OnProgressChanged()
        {
            // Changed tombe souvent (argent, expérience) : on ne refait le travail que si un
            // logement a changé de main.
            if (Signature() != _appliedSignature) Apply();
        }

        private string Signature()
        {
            if (_progress == null) return "";
            string s = _progress.Home + "|" + (MapStreamer.Ready ? "1" : "0");
            for (int i = 0; i < _homes.Length; i++)
            {
                if (_homes[i] != null && Owns(_homes[i])) s += "|" + _homes[i].name;
            }

            return s;
        }

        private bool Owns(Home home)
        {
            if (_progress == null) return home == (_homes.Length > 0 ? _homes[0] : null);
            return _progress.Owns(home.name) || _progress.Home == home.name;
        }

        /// <summary>Réapplique tout : où l'on se réveille, meubles, portes, portail.</summary>
        public void Apply()
        {
            _appliedSignature = Signature();

            Home current = Current;
            if (_director != null && current != null && current.arrival != null) _director.Home = current.arrival;

            for (int i = 0; i < _homes.Length; i++)
            {
                Home home = _homes[i];
                if (home == null) continue;

                bool owned = Owns(home);
                if (home.furniture != null && home.furniture.activeSelf != owned) home.furniture.SetActive(owned);

                if (!MapStreamer.Ready) continue;

                for (int d = 0; d < home.doors.Length; d++)
                {
                    SwingDoor door = MapStreamer.Door(home.doors[d]);
                    if (door != null) door.SetLocked(!owned, "À vendre : l'ordinateur, appli Immobilier.");
                }

                for (int o = 0; o < home.openForOwner.Length; o++)
                {
                    Transform t = MapStreamer.FindInCity(home.openForOwner[o]);
                    if (t != null && t.gameObject.activeSelf == owned) t.gameObject.SetActive(!owned);
                }
            }
        }

        /// <summary>Le constructeur de la scène y déclare les logements.</summary>
        public void Configure(Home[] homes)
        {
            _homes = homes ?? new Home[0];
        }
    }
}
