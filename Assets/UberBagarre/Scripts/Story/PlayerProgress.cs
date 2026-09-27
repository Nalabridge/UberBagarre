using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>Les cinq qualités qu'on entraîne.</summary>
    public enum Stat
    {
        Puissance = 0,
        Endurance = 1,
        Encaisse = 2,
        Vitesse = 3,
        Technique = 4
    }

    /// <summary>
    /// La progression du personnage : argent, expérience, niveau, réputation — et depuis le
    /// monde ouvert, tout ce qu'une partie doit retenir d'une soirée à l'autre.
    ///
    /// Les valeurs ne servent pas au même but :
    /// - l'ARGENT raconte la situation (150 € contre 640 € de loyer) ;
    /// - l'EXPÉRIENCE fait monter de niveau, et chaque niveau donne des points d'entraînement ;
    /// - la RÉPUTATION (0 à 100) est ce que l'appli pense de toi : elle monte quand une course
    ///   est propre, elle chute quand tu te rates — et elle décide des courses qu'on te propose ;
    /// - la NOTE est la moyenne des avis ; les AVIS sont du texte, et c'est ce texte qui dit au
    ///   joueur ce qu'il est devenu ;
    /// - les BLESSURES s'accumulent à chaque K.O. et ne partent qu'en dormant : à la troisième,
    ///   un K.O. de plus te tue — et la partie reprend au dernier sommeil.
    ///
    /// Ce composant ne décide d'aucun effet de jeu : il compte, il prévient, il sauvegarde.
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

        /// <summary>Une course, réussie ou non, telle que l'appli la garde.</summary>
        [Serializable]
        public class ContractRecord
        {
            public string target;
            public string place;
            public int reward;
            public int stars;
            public bool success;
            public string objective;
            public bool objectiveDone;
            public string photo;
            public int day;
            public string note;
            public int reputation;
        }

        /// <summary>La tenue : indices dans les listes du vestiaire.</summary>
        [Serializable]
        public class Outfit
        {
            public int top;
            public int shirt;
            public int pants;
            public int shoes;
        }

        /// <summary>Une ligne du relevé de compte (l'appli Banque).</summary>
        [Serializable]
        public class Transaction
        {
            public string label;
            public int amount;
            public int day;
        }

        /// <summary>Tout ce qui s'écrit sur le disque.</summary>
        [Serializable]
        private class SaveData
        {
            public int version = 1;
            public int money;
            public int experience;
            public int level = 1;
            public int contracts;
            public int failures;
            public int fightsWon;
            public int knockedOut;
            public int deaths;
            public int reputation = 20;
            public int injuries;
            public int skillPoints;
            public int trainingsBought;
            public int[] stats = new int[5];
            public Outfit outfit = new Outfit();
            public List<string> unlocked = new List<string>();
            public string home = "Motel";
            public string car = "Shitbox";
            public List<Transaction> ledger = new List<Transaction>();

            /// <summary>Ce qu'on doit à Nestor, l'usurier (l'histoire).</summary>
            public int debt = 12000;

            /// <summary>Loyer du motel payé jusqu'à ce jour (inclus).</summary>
            public int rentPaidUntil;

            /// <summary>Envoyé à maman, depuis le début.</summary>
            public int sentHome;

            /// <summary>La faim : 100 = rassasié, 0 = le ventre vide. Trois jours sans rien de chaud, au début.</summary>
            public float satiety = 35f;

            /// <summary>Les repas qui attendent dans le frigo de la planque.</summary>
            public int meals;

            /// <summary>Les trousses de soins rangées à la maison.</summary>
            public int medkits;
            public List<string> owned = new List<string>();
            public List<string> flags = new List<string>();
            public int chapter;
            public int day = 1;
            public List<Review> reviews = new List<Review>();
            public List<ContractRecord> history = new List<ContractRecord>();
            public int nosesBroken;
            public int legsBroken;
            public int ribsBroken;
            public int casinoWon;
            public int casinoLost;
            public string savedAt;
        }

        public const int MaxStatLevel = 5;
        public const int MaxInjuries = 3;

        private const string SaveFile = "uberbagarre_partie.json";

        [Header("Niveaux")]
        [SerializeField]
        [Tooltip("Experience totale requise pour chaque niveau. Le premier palier est franchi par " +
                 "la toute premiere course : le joueur doit sentir le systeme exister des la fin " +
                 "du prologue, pas apres trois heures.")]
        private int[] _thresholds = { 0, 100, 350, 700, 1200, 1900, 2800, 3900, 5200, 6800 };

        [SerializeField, Min(0)]
        [Tooltip("Points d'entrainement gagnes a chaque niveau.")]
        private int _pointsPerLevel = 2;

        [Header("Depart")]
        [SerializeField] private int _startMoney = 10;
        [SerializeField, Range(0, 100)] private int _startReputation = 20;

        private SaveData _data = new SaveData();

        public int Money { get { return _data.money; } }
        public int Experience { get { return _data.experience; } }
        public int Level { get { return _data.level; } }
        public int Contracts { get { return _data.contracts; } }
        public int Failures { get { return _data.failures; } }
        public int FightsWon { get { return _data.fightsWon; } }
        public int KnockedOut { get { return _data.knockedOut; } }
        public int Deaths { get { return _data.deaths; } }
        public int Injuries { get { return _data.injuries; } }
        public int SkillPoints { get { return _data.skillPoints; } }
        public int TrainingsBought { get { return _data.trainingsBought; } }
        public int Chapter { get { return _data.chapter; } }
        public int Day { get { return _data.day; } }
        public string Home { get { return _data.home; } }
        public Outfit CurrentOutfit { get { return _data.outfit; } }
        public int NosesBroken { get { return _data.nosesBroken; } }
        public int LegsBroken { get { return _data.legsBroken; } }
        public int RibsBroken { get { return _data.ribsBroken; } }
        public int CasinoWon { get { return _data.casinoWon; } }
        public int CasinoLost { get { return _data.casinoLost; } }

        /// <summary>Réputation dans l'appli, de 0 à 100.</summary>
        public int Reputation { get { return _data.reputation; } }

        public IList<Review> Reviews { get { return _data.reviews; } }
        public IList<ContractRecord> History { get { return _data.history; } }

        /// <summary>Déclenché à chaque niveau franchi, avec le nouveau niveau.</summary>
        public event Action<int> LeveledUp;

        /// <summary>La réputation a bougé : l'écart et la raison (affichée par l'appli).</summary>
        public event Action<int, string> ReputationChanged;

        /// <summary>N'importe quelle valeur a changé (tenue, stats, argent...).</summary>
        public event Action Changed;

        /// <summary>Note moyenne des avis, 0 s'il n'y en a aucun.</summary>
        public float Rating
        {
            get
            {
                if (_data.reviews.Count == 0) return 0f;

                float total = 0f;
                for (int i = 0; i < _data.reviews.Count; i++) total += _data.reviews[i].Stars;
                return total / _data.reviews.Count;
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

        // ------------------------------------------------------------------ réputation

        /// <summary>Le titre que l'appli donne selon la réputation.</summary>
        public string ReputationTitle { get { return TitleOf(_data.reputation); } }

        public static string TitleOf(int reputation)
        {
            if (reputation >= 90) return "LÉGENDE";
            if (reputation >= 75) return "TERREUR";
            if (reputation >= 55) return "COGNEUR";
            if (reputation >= 35) return "BAGARREUR";
            if (reputation >= 15) return "INCONNU";
            return "GRILLÉ";
        }

        /// <summary>Le nombre d'étoiles maximum des courses qu'on te propose.</summary>
        public int MaxStarsOffered
        {
            get
            {
                int byLevel = Mathf.Clamp(1 + Level / 2, 1, 3);
                int byReputation = _data.reputation >= 60 ? 3 : _data.reputation >= 35 ? 2 : 1;
                return Mathf.Min(byLevel, byReputation);
            }
        }

        /// <summary>Ce que la réputation fait au prix d'une course (0,8 à 1,4).</summary>
        public float PayMultiplier
        {
            get { return 0.8f + _data.reputation / 100f * 0.6f; }
        }

        /// <summary>Réputation trop basse : l'appli ne propose plus rien.</summary>
        public bool Suspended { get { return _data.reputation <= 0; } }

        public void ChangeReputation(int delta, string reason)
        {
            int before = _data.reputation;
            _data.reputation = Mathf.Clamp(_data.reputation + delta, 0, 100);
            int applied = _data.reputation - before;

            Action<int, string> handler = ReputationChanged;
            if (handler != null && applied != 0) handler(applied, reason);
            RaiseChanged();
        }

        // ------------------------------------------------------------------ cycle

        private void Awake()
        {
            ResetProgress();
        }

        public void ResetProgress()
        {
            _data = new SaveData();
            _data.money = _startMoney;
            _data.reputation = _startReputation;
            _data.unlocked.Add("haut:0");
            _data.unlocked.Add("couleur-haut:0");
            _data.unlocked.Add("couleur-bas:0");
            _data.unlocked.Add("chaussures:0");
            _data.owned.Add("Motel");
            RaiseChanged();
        }

        /// <summary>
        /// Encaisse une course terminée. Les niveaux franchis sont annoncés UN PAR UN : une course
        /// qui fait passer deux paliers doit débloquer deux capacités, pas une seule.
        /// </summary>
        public void CompleteContract(int reward, int experience, int stars, string review, string client)
        {
            _data.money += Mathf.Max(0, reward);
            Record("Vir. UB Services — " + client, Mathf.Max(0, reward));
            _data.contracts++;
            _data.fightsWon++;

            AddReview(stars, review, client);
            AddExperience(experience);
        }

        public void AddReview(int stars, string review, string client)
        {
            Review entry = new Review();
            entry.Stars = Mathf.Clamp(stars, 1, 5);
            entry.Text = review ?? string.Empty;
            entry.Client = client ?? string.Empty;
            _data.reviews.Insert(0, entry);
            if (_data.reviews.Count > 40) _data.reviews.RemoveAt(_data.reviews.Count - 1);
            RaiseChanged();
        }

        /// <summary>La course est gardée dans l'historique de l'appli (la plus récente d'abord).</summary>
        public void Record(ContractRecord record)
        {
            if (record == null) return;
            record.day = _data.day;
            _data.history.Insert(0, record);
            if (_data.history.Count > 30) _data.history.RemoveAt(_data.history.Count - 1);
            if (!record.success) _data.failures++;
            RaiseChanged();
        }

        /// <summary>De l'argent sans course (la triche, le casino, les factures).</summary>
        public void AddMoney(int amount)
        {
            AddMoney(amount, amount >= 0 ? "Versement" : "Prélèvement");
        }

        /// <summary>Un mouvement d'argent, inscrit au relevé de compte.</summary>
        public void AddMoney(int amount, string label)
        {
            int before = _data.money;
            _data.money = Mathf.Max(0, _data.money + amount);
            Record(label, _data.money - before);
            RaiseChanged();
        }

        /// <summary>Payer : faux (et rien ne bouge) si on n'a pas assez.</summary>
        public bool Spend(int amount)
        {
            return Spend(amount, "Paiement");
        }

        public bool Spend(int amount, string label)
        {
            if (amount < 0 || _data.money < amount) return false;
            _data.money -= amount;
            Record(label, -amount);
            RaiseChanged();
            return true;
        }

        private void Record(string label, int amount)
        {
            if (amount == 0) return;
            if (_data.ledger == null) _data.ledger = new List<Transaction>();
            _data.ledger.Add(new Transaction { label = string.IsNullOrEmpty(label) ? "Opération" : label, amount = amount, day = _data.day });
            if (_data.ledger.Count > 60) _data.ledger.RemoveAt(0);
        }

        /// <summary>Le relevé : les dernières opérations, la plus récente à la fin.</summary>
        public IList<Transaction> Ledger
        {
            get
            {
                if (_data.ledger == null) _data.ledger = new List<Transaction>();
                return _data.ledger;
            }
        }

        /// <summary>Ce qu'il reste à rembourser à Nestor.</summary>
        public int Debt { get { return Mathf.Max(0, _data.debt); } }

        public event Action<int> DebtPaid;

        /// <summary>Rembourse Nestor (virement). Rend ce qui a été versé.</summary>
        public int PayDebt(int amount)
        {
            amount = Mathf.Min(amount, Mathf.Min(Debt, _data.money));
            if (amount <= 0) return 0;
            Spend(amount, "Virement Nestor");
            _data.debt -= amount;
            RaiseChanged();
            if (DebtPaid != null) DebtPaid(amount);
            return amount;
        }

        /// <summary>Change la dette (l'histoire : intérêts, pénalités, remise).</summary>
        public void SetDebt(int value)
        {
            _data.debt = Mathf.Max(0, value);
            RaiseChanged();
        }

        public int RentPaidUntil { get { return _data.rentPaidUntil; } }

        /// <summary>Paie une semaine de loyer d'avance (jusqu'au jour indiqué).</summary>
        public bool PayRent(int amount, int untilDay)
        {
            if (!Spend(amount, "Loyer motel Hyland")) return false;
            _data.rentPaidUntil = Mathf.Max(_data.rentPaidUntil, untilDay);
            RaiseChanged();
            return true;
        }

        public int SentHome { get { return _data.sentHome; } }

        public bool SendHome(int amount)
        {
            if (!Spend(amount, "Virement maman")) return false;
            _data.sentHome += amount;
            RaiseChanged();
            return true;
        }

        public void AddExperience(int experience)
        {
            _data.experience += Mathf.Max(0, experience);

            while (Experience >= ThresholdOf(Level + 1))
            {
                _data.level++;
                _data.skillPoints += _pointsPerLevel;

                Action<int> handler = LeveledUp;
                if (handler != null) handler(Level);
            }

            RaiseChanged();
        }

        // ------------------------------------------------------------------ blessures, mort

        /// <summary>Un K.O. de plus : une blessure. Renvoie vrai si c'était celui de trop (la mort).</summary>
        public bool TakeKnockout()
        {
            _data.knockedOut++;
            if (_data.injuries >= MaxInjuries - 1)
            {
                _data.deaths++;
                RaiseChanged();
                return true;
            }

            _data.injuries++;
            RaiseChanged();
            return false;
        }

        /// <summary>Un soin payé (pharmacie, médecin) : une blessure de moins, ou toutes.</summary>
        public bool HealInjuries(bool all)
        {
            if (_data.injuries <= 0) return false;
            _data.injuries = all ? 0 : _data.injuries - 1;
            RaiseChanged();
            return true;
        }

        // ------------------------------------------------------------------ faim, frigo, trousse

        public const float MaxSatiety = 100f;

        /// <summary>La faim : 100 = rassasié, 0 = le ventre vide.</summary>
        public float Satiety { get { return _data.satiety; } }

        /// <summary>Manger : la jauge remonte.</summary>
        public void Eat(float amount)
        {
            _data.satiety = Mathf.Clamp(_data.satiety + amount, 0f, MaxSatiety);
            RaiseChanged();
        }

        /// <summary>Le temps passe, les coups coûtent : la jauge descend (sans prévenir tout le monde).</summary>
        public void Starve(float amount)
        {
            _data.satiety = Mathf.Clamp(_data.satiety - amount, 0f, MaxSatiety);
        }

        public int Meals { get { return _data.meals; } }

        public void AddMeals(int count)
        {
            _data.meals = Mathf.Max(0, _data.meals + count);
            RaiseChanged();
        }

        /// <summary>Un repas pris dans le frigo (faux s'il est vide).</summary>
        public bool TakeMeal()
        {
            if (_data.meals <= 0) return false;
            _data.meals--;
            RaiseChanged();
            return true;
        }

        public int Medkits { get { return _data.medkits; } }

        public void AddMedkit()
        {
            _data.medkits++;
            RaiseChanged();
        }

        /// <summary>Une trousse utilisée (faux s'il n'y en a plus).</summary>
        public bool UseMedkit()
        {
            if (_data.medkits <= 0) return false;
            _data.medkits--;
            RaiseChanged();
            return true;
        }

        /// <summary>Une nuit de sommeil : les blessures guérissent, un jour passe.</summary>
        public void Sleep()
        {
            _data.injuries = 0;
            _data.day++;
            // Une nuit creuse l'estomac.
            _data.satiety = Mathf.Max(0f, _data.satiety - 18f);
            RaiseChanged();
        }

        public void CountFracture(string kind)
        {
            switch (kind)
            {
                case "nez": _data.nosesBroken++; break;
                case "jambe": _data.legsBroken++; break;
                case "cotes": _data.ribsBroken++; break;
            }

            RaiseChanged();
        }

        public void CountCasino(int won, int lost)
        {
            _data.casinoWon += Mathf.Max(0, won);
            _data.casinoLost += Mathf.Max(0, lost);
        }

        // ------------------------------------------------------------------ entraînement

        public int StatLevel(Stat stat)
        {
            int i = (int)stat;
            return i >= 0 && i < _data.stats.Length ? _data.stats[i] : 0;
        }

        /// <summary>Points nécessaires pour passer ce niveau (le suivant) : 1, 2, 3, 4, 5.</summary>
        public int NextStatCost(Stat stat)
        {
            return StatLevel(stat) + 1;
        }

        public bool CanTrain(Stat stat)
        {
            return StatLevel(stat) < MaxStatLevel && _data.skillPoints >= NextStatCost(stat);
        }

        public bool Train(Stat stat)
        {
            if (!CanTrain(stat)) return false;

            _data.skillPoints -= NextStatCost(stat);
            _data.stats[(int)stat]++;
            RaiseChanged();
            return true;
        }

        /// <summary>Prix d'un point d'entraînement acheté (salle de sport) : il monte à chaque achat.</summary>
        public int TrainingPrice { get { return 300 + 150 * _data.trainingsBought; } }

        public bool BuyTraining()
        {
            if (!Spend(TrainingPrice, "Coaching boxe")) return false;
            _data.trainingsBought++;
            _data.skillPoints++;
            RaiseChanged();
            return true;
        }

        public void GrantSkillPoints(int points)
        {
            _data.skillPoints = Mathf.Max(0, _data.skillPoints + points);
            RaiseChanged();
        }

        // ------------------------------------------------------------------ vestiaire

        public bool IsUnlocked(string item)
        {
            return _data.unlocked.Contains(item);
        }

        public void Unlock(string item)
        {
            if (string.IsNullOrEmpty(item) || _data.unlocked.Contains(item)) return;
            _data.unlocked.Add(item);
            RaiseChanged();
        }

        public void Wear(Outfit outfit)
        {
            if (outfit == null) return;
            _data.outfit = new Outfit { top = outfit.top, shirt = outfit.shirt, pants = outfit.pants, shoes = outfit.shoes };
            RaiseChanged();
        }

        // ------------------------------------------------------------------ logement, histoire

        public bool Owns(string property)
        {
            return _data.owned.Contains(property);
        }

        public void Acquire(string property)
        {
            if (!_data.owned.Contains(property)) _data.owned.Add(property);
            RaiseChanged();
        }

        /// <summary>Le modèle de la voiture du joueur (« Shitbox » au départ, puis ce qu'il achète).</summary>
        public string Car { get { return string.IsNullOrEmpty(_data.car) ? "Shitbox" : _data.car; } }

        public void SetCar(string model)
        {
            if (string.IsNullOrEmpty(model)) return;
            _data.car = model;
            RaiseChanged();
        }

        public void MoveTo(string property)
        {
            _data.home = property;
            RaiseChanged();
        }

        public bool HasFlag(string flag)
        {
            return _data.flags.Contains(flag);
        }

        public void SetFlag(string flag)
        {
            if (!string.IsNullOrEmpty(flag) && !_data.flags.Contains(flag)) _data.flags.Add(flag);
        }

        public void SetChapter(int chapter)
        {
            _data.chapter = Mathf.Max(_data.chapter, chapter);
            RaiseChanged();
        }

        // ------------------------------------------------------------------ sauvegarde

        public static string SavePath
        {
            get { return Path.Combine(Application.persistentDataPath, SaveFile); }
        }

        public static bool HasSave
        {
            get { return File.Exists(SavePath); }
        }

        /// <summary>Le chapitre de la sauvegarde (-1 sans sauvegarde lisible).</summary>
        public static int SavedChapter()
        {
            try
            {
                if (!HasSave) return -1;
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                return data != null ? data.chapter : -1;
            }
            catch (Exception)
            {
                return -1;
            }
        }

        /// <summary>Quand la sauvegarde a été écrite (texte), vide s'il n'y en a pas.</summary>
        public static string SaveDescription()
        {
            try
            {
                if (!HasSave) return string.Empty;
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null) return string.Empty;
                return "Jour " + data.day + "  ·  niveau " + data.level + "  ·  " + data.money + " EUR  ·  " + TitleOf(data.reputation);
            }
            catch (Exception)
            {
                return string.Empty;
            }
        }

        public bool Save()
        {
            try
            {
                _data.savedAt = DateTime.Now.ToString("yyyy-MM-dd HH:mm");
                File.WriteAllText(SavePath, JsonUtility.ToJson(_data, true));
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] Sauvegarde impossible : " + e.Message);
                return false;
            }
        }

        public bool Load()
        {
            try
            {
                if (!HasSave) return false;
                SaveData data = JsonUtility.FromJson<SaveData>(File.ReadAllText(SavePath));
                if (data == null) return false;

                if (data.stats == null || data.stats.Length != 5)
                {
                    int[] stats = new int[5];
                    if (data.stats != null) Array.Copy(data.stats, stats, Mathf.Min(5, data.stats.Length));
                    data.stats = stats;
                }

                if (data.outfit == null) data.outfit = new Outfit();
                if (data.unlocked == null) data.unlocked = new List<string>();
                if (data.owned == null) data.owned = new List<string> { "Motel" };
                if (data.flags == null) data.flags = new List<string>();
                if (data.reviews == null) data.reviews = new List<Review>();
                if (data.history == null) data.history = new List<ContractRecord>();

                _data = data;
                RaiseChanged();
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] Sauvegarde illisible : " + e.Message);
                return false;
            }
        }

        public static void DeleteSave()
        {
            try
            {
                if (HasSave) File.Delete(SavePath);
            }
            catch (Exception)
            {
            }
        }

        // ------------------------------------------------------------------ utilitaires

        private void RaiseChanged()
        {
            Action handler = Changed;
            if (handler != null) handler();
        }

        private int ThresholdOf(int level)
        {
            int index = Mathf.Clamp(level - 1, 0, _thresholds.Length - 1);
            if (level - 1 >= _thresholds.Length) return _thresholds[_thresholds.Length - 1] + 1800 * (level - _thresholds.Length);

            return _thresholds[index];
        }
    }
}
