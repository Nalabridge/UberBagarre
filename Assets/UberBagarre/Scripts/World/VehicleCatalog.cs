using System;
using System.Collections.Generic;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Les voitures de la ville qu'on peut posséder (celles de la concession), et la voiture du
    /// joueur. Le modèle possédé est sauvegardé (<see cref="PlayerProgress.Car"/>) : au
    /// chargement, la bonne voiture attend sur sa place, devant le motel.
    ///
    /// Acheter une voiture remplace l'ancienne : on ne collectionne pas, on change de caisse.
    /// Les modèles vivent inactifs dans la scène ; on en tire une copie.
    /// </summary>
    public class VehicleCatalog : MonoBehaviour
    {
        [Serializable]
        public class Entry
        {
            public string key;
            public string label;
            public int price;
            public GameObject template;
        }

        [SerializeField] private Entry[] _entries = new Entry[0];
        [SerializeField] private PlayerProgress _progress;

        [SerializeField]
        [Tooltip("La place de la voiture du joueur.")]
        private Transform _spot;

        [SerializeField] private DrivableCar _personal;
        [SerializeField] private string _personalKey = "Shitbox";
        [SerializeField] private string _personalName = "Ta caisse";

        public IReadOnlyList<Entry> Entries { get { return _entries; } }

        /// <summary>La voiture du joueur (peut être null si elle a été détruite).</summary>
        public DrivableCar Personal { get { return _personal; } }

        public string PersonalKey { get { return _personalKey; } }

        public event Action<Entry> Bought;

        private void OnEnable()
        {
            if (_progress != null) _progress.Changed += Sync;
        }

        private void OnDisable()
        {
            if (_progress != null) _progress.Changed -= Sync;
        }

        private void Start()
        {
            Sync();
        }

        /// <summary>La sauvegarde (chargée après le démarrage, ou remise à zéro) dit quelle voiture est la nôtre.</summary>
        private void Sync()
        {
            if (_progress != null && _progress.Car != _personalKey && Find(_progress.Car) != null) Swap(_progress.Car);
        }

        public Entry Find(string key)
        {
            for (int i = 0; i < _entries.Length; i++)
            {
                if (_entries[i] != null && _entries[i].key == key) return _entries[i];
            }

            return null;
        }

        /// <summary>Achète une voiture : payée, elle remplace l'ancienne sur la place du joueur.</summary>
        public bool Buy(string key)
        {
            Entry entry = Find(key);
            if (entry == null || key == _personalKey || _progress == null) return false;
            if (!_progress.Spend(entry.price, "Hyland Auto — " + entry.label)) return false;

            _progress.SetCar(key);
            if (_personalKey != key) Swap(key);
            if (Bought != null) Bought(entry);
            return true;
        }

        private void Swap(string key)
        {
            Entry entry = Find(key);
            if (entry == null || entry.template == null) return;

            // On ne retire pas la voiture de sous les fesses du joueur.
            if (_personal != null && _personal != DrivableCar.Driven) Destroy(_personal.gameObject);

            Vector3 position = _spot != null ? _spot.position : transform.position;
            Quaternion rotation = _spot != null ? _spot.rotation : transform.rotation;

            GameObject car = Instantiate(entry.template, position + Vector3.up * 0.1f, rotation, transform);
            car.name = _personalName + " (" + entry.label + ")";
            car.SetActive(true);

            _personal = car.GetComponent<DrivableCar>();
            if (_personal != null)
            {
                _personal.Rename(_personalName);
                _personal.Kind = DrivableCar.Access.Perso;
                _personal.Locked = false;
            }
            _personalKey = key;
        }

        /// <summary>Le constructeur de la scène y écrit les modèles et la voiture de départ.</summary>
        public void Configure(Entry[] entries, PlayerProgress progress, Transform spot, DrivableCar personal, string personalKey)
        {
            _entries = entries ?? new Entry[0];
            _progress = progress;
            _spot = spot;
            _personal = personal;
            _personalKey = personalKey;
        }
    }
}
