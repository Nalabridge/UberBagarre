using System;
using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// La progression du personnage : argent, expérience, niveau, réputation.
    ///
    /// C'est le cœur du dossier : « faire évoluer son personnage avec des capacités, de l'exp
    /// remportée par chaque bagarre », et « une réputation, style une page notée avec des avis
    /// que les clients lui ont laissés ». Les quatre valeurs ne servent pas au même but :
    ///
    /// - l'ARGENT raconte la situation (150 € contre 640 € de loyer) ;
    /// - l'EXPÉRIENCE débloque des capacités — c'est la récompense qui change le jeu ;
    /// - la RÉPUTATION est la note moyenne des avis — c'est la récompense qui change le regard ;
    /// - les AVIS sont du texte, et c'est ce texte qui dit au joueur ce qu'il est devenu.
    ///
    /// Ce composant ne décide d'aucun effet de jeu : il compte, et il prévient. C'est le
    /// scénario qui décide que le niveau 2 débloque le coup de tête. Mélanger les deux ferait
    /// d'une table de niveaux un endroit où l'on règle aussi des dégâts.
    /// </summary>
    public class PlayerProgress : MonoBehaviour
    {
        [Serializable]
        public struct Review
        {
            public int Stars;
            public string Text;
            public string Client;
        }

        [Header("Niveaux")]
        [SerializeField]
        [Tooltip("Experience totale requise pour chaque niveau. Le premier palier est franchi par " +
                 "la toute premiere course : le joueur doit sentir le systeme exister des la fin " +
                 "du prologue, pas apres trois heures.")]
        private int[] _thresholds = { 0, 100, 350, 700, 1200, 1900 };

        [Header("Depart")]
        [SerializeField] private int _startMoney = 10;

        private readonly List<Review> _reviews = new List<Review>();

        public int Money { get; private set; }
        public int Experience { get; private set; }
        public int Level { get; private set; }
        public int Contracts { get; private set; }

        public IList<Review> Reviews { get { return _reviews; } }

        /// <summary>Déclenché à chaque niveau franchi, avec le nouveau niveau.</summary>
        public event Action<int> LeveledUp;

        /// <summary>Note moyenne des avis, 0 s'il n'y en a aucun.</summary>
        public float Reputation
        {
            get
            {
                if (_reviews.Count == 0) return 0f;

                float total = 0f;
                for (int i = 0; i < _reviews.Count; i++) total += _reviews[i].Stars;
                return total / _reviews.Count;
            }
        }

        /// <summary>Avancement vers le niveau suivant, de 0 à 1.</summary>
        public float LevelProgress
        {
            get
            {
                int current = ThresholdOf(Level);
                int next = ThresholdOf(Level + 1);
                if (next <= current) return 1f;

                return Mathf.Clamp01((Experience - current) / (float)(next - current));
            }
        }

        public int NextThreshold { get { return ThresholdOf(Level + 1); } }

        private void Awake()
        {
            ResetProgress();
        }

        public void ResetProgress()
        {
            Money = _startMoney;
            Experience = 0;
            Level = 1;
            Contracts = 0;
            _reviews.Clear();
        }

        /// <summary>
        /// Encaisse une course terminée. Les niveaux franchis sont annoncés UN PAR UN : une course
        /// qui fait passer deux paliers doit débloquer deux capacités, pas une seule.
        /// </summary>
        public void CompleteContract(int reward, int experience, int stars, string review, string client)
        {
            Money += Mathf.Max(0, reward);
            Contracts++;

            Review entry = new Review();
            entry.Stars = Mathf.Clamp(stars, 1, 5);
            entry.Text = review ?? string.Empty;
            entry.Client = client ?? string.Empty;
            _reviews.Insert(0, entry);

            Experience += Mathf.Max(0, experience);

            while (Level < _thresholds.Length && Experience >= ThresholdOf(Level + 1))
            {
                Level++;

                Action<int> handler = LeveledUp;
                if (handler != null) handler(Level);
            }
        }

        private int ThresholdOf(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, _thresholds.Length - 1);
            if (level - 1 >= _thresholds.Length) return _thresholds[_thresholds.Length - 1] + 1000 * (level - _thresholds.Length);

            return _thresholds[index];
        }
    }
}
