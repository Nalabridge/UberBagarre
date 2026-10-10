using System;
using System.Collections.Generic;
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

            [Tooltip("Le courrier sur le bureau : l'histoire y dépose ses lettres.")]
            public Interactable letters;

            [Tooltip("Le frigo : les repas achetés à l'épicerie.")]
            public Interactable fridge;

            [Tooltip("La trousse de soins, au mur : on s'y soigne avec une trousse achetée à la pharmacie.")]
            public Interactable medkit;

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
        [SerializeField] private SubtitleDisplay _subtitles;

        private string _appliedSignature;

        /// <summary>Le joueur a lu le courrier d'un logement.</summary>
        public event Action<Home> LettersRead;

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
                if (home.letters != null) home.letters.Activated += OnLetters;
                if (home.fridge != null) home.fridge.Activated += OnFridge;
                if (home.medkit != null) home.medkit.Activated += OnMedkit;
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
                if (_homes[i] == null) continue;
                if (_homes[i].bed != null) _homes[i].bed.Activated -= OnBed;
                if (_homes[i].letters != null) _homes[i].letters.Activated -= OnLetters;
                if (_homes[i].fridge != null) _homes[i].fridge.Activated -= OnFridge;
                if (_homes[i].medkit != null) _homes[i].medkit.Activated -= OnMedkit;
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

        /// <summary>Le frigo : un repas acheté à l'épicerie, mangé chez soi.</summary>
        private void OnFridge(Interactable source)
        {
            if (_progress == null) return;

            if (_progress.Satiety >= PlayerProgress.MaxSatiety - 5f)
            {
                Tell("Tu n'as pas faim.");
                return;
            }

            if (!_progress.TakeMeal())
            {
                Tell("Le frigo est vide. Une bouteille de ketchup et un citron. L'épicerie vend des courses pour la semaine.");
                return;
            }

            _progress.Eat(45f);
            if (Player.PlayerBuffs.Instance != null) Player.PlayerBuffs.Instance.Heal(20f);
            Tell("Tu réchauffes un plat et tu manges debout. Ça va mieux. (" + _progress.Meals + " repas au frigo)");
        }

        /// <summary>La trousse de soins : une blessure en moins, la vie au maximum.</summary>
        private void OnMedkit(Interactable source)
        {
            if (_progress == null) return;

            if (_progress.Medkits <= 0)
            {
                Tell("La boîte est vide. La pharmacie vend des trousses de soins.");
                return;
            }

            bool hurt = _progress.Injuries > 0;
            Player.PlayerCondition condition = Player.PlayerCondition.Instance;
            if (!hurt && condition != null && condition.RegenCap >= 1f && Player.PlayerBuffs.Instance != null &&
                Player.PlayerBuffs.Instance.HealthNormalized >= 0.98f)
            {
                Tell("Rien à soigner.");
                return;
            }

            _progress.UseMedkit();
            if (hurt) _progress.HealInjuries(false);
            if (Player.PlayerBuffs.Instance != null) Player.PlayerBuffs.Instance.HealFull();
            Tell(hurt ? "Désinfectant, points de suture, attelle. Une blessure en moins. (" + _progress.Medkits + " trousse(s))"
                      : "Tu te recouds devant le miroir. Comme neuf, ou presque. (" + _progress.Medkits + " trousse(s))");
        }

        private void Tell(string text)
        {
            if (_subtitles != null) _subtitles.Play(DialogueLine.Say("", text));
        }

        private void OnLetters(Interactable source)
        {
            Action<Home> handler = LettersRead;
            if (handler == null) return;

            for (int i = 0; i < _homes.Length; i++)
            {
                if (_homes[i] != null && _homes[i].letters == source)
                {
                    handler(_homes[i]);
                    return;
                }
            }
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

                // Les autres portes du même logement (la baie vitrée du bungalow, une porte de
                // derrière) : fermées tant qu'on ne l'a pas acheté, sinon on entrait par là.
                if (home.doors.Length > 0)
                {
                    string building = CityRules.BuildingOf(home.doors[0]) + "/";
                    foreach (KeyValuePair<string, SwingDoor> pair in MapStreamer.AllDoors)
                    {
                        if (pair.Value == null || !pair.Key.StartsWith(building)) continue;
                        if (System.Array.IndexOf(home.doors, pair.Key) >= 0) continue;
                        pair.Value.SetLocked(!owned, "À vendre : l'ordinateur, appli Immobilier.");
                    }
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
