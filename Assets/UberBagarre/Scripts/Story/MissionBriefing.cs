using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// La fiche de mission : une seule source de vérité pour le nom de la cible et son signalement.
    ///
    /// Pourquoi un composant plutôt que des chaînes écrites là où on en a besoin : le nom
    /// « BRUNO MORETTI » apparaît au moins à cinq endroits — la fiche du suspect, l'objectif à
    /// l'écran, l'étiquette au-dessus de sa tête, le message de validation et l'avis du client.
    /// Cinq copies, c'est quatre occasions de n'en corriger que trois.
    ///
    /// Le SIGNALEMENT est le champ le plus important du lot, et il l'est mécaniquement : c'est
    /// la seule information qui permette de distinguer la cible des quatre autres personnes
    /// plantées devant le club. Sans lui, « trouve-le » ne veut rien dire et le joueur tape au
    /// hasard.
    /// </summary>
    public class MissionBriefing : MonoBehaviour
    {
        [Header("Cible")]
        [SerializeField] private string _targetName = "BRUNO MORETTI";
        [SerializeField] private string _targetAge = "38 ans";

        [SerializeField]
        [Tooltip("Ce qui permet de le reconnaitre dans le groupe. C'est le champ qui fait " +
                 "exister l'enigme : il doit decrire quelque chose de VISIBLE sur le modele.")]
        private string _targetClothing = "Veste rouge, crane rase";

        [SerializeField] private string _targetLocation = "Devant le club — Le Vertigo";

        [SerializeField]
        [Tooltip("Heure du rendez-vous, affichee sur la carte de la course.")]
        private string _meetingTime = "02:30";

        [SerializeField, TextArea(2, 4)]
        private string _targetRecord = "Videur. Connu pour cogner d'abord.\nA casse le bras d'un livreur en mars.";

        [Header("Contrat")]
        [SerializeField] private string _clientName = "CLIENT VERIFIE";
        [SerializeField, Range(1, 5)] private int _stars = 1;
        [SerializeField] private int _reward = 150;
        [SerializeField] private int _experience = 120;

        [SerializeField]
        [Tooltip("L'avis laisse par le client une fois la course validee. C'est la premiere " +
                 "brique du systeme de reputation decrit dans le dossier.")]
        private string _review = "Propre et rapide. Il a rien dit, il a fait.";

        [SerializeField, Range(1, 5)]
        [Tooltip("Note laissee par le client avec son avis.")]
        private int _reviewStars = 5;

        [Header("Contact")]
        [SerializeField] private string _friendName = "SAMI";

        /// <summary>
        /// Réécrit la fiche en jeu : le monde ouvert tire une nouvelle commande à chaque course,
        /// et le téléphone relit la fiche à chaque image.
        /// </summary>
        public void Configure(string targetName, string targetAge, string clothing, string location,
            string meetingTime, string record, string client, int stars, int reward, int experience,
            string review, int reviewStars)
        {
            _targetName = targetName;
            _targetAge = targetAge;
            _targetClothing = clothing;
            _targetLocation = location;
            _meetingTime = meetingTime;
            _targetRecord = record;
            _clientName = client;
            _stars = Mathf.Clamp(stars, 1, 5);
            _reward = reward;
            _experience = experience;
            _review = review;
            _reviewStars = Mathf.Clamp(reviewStars, 1, 5);
        }

        /// <summary>
        /// Le contexte et la consigne du monde ouvert : ce que fait la cible, ce que le client
        /// exige en plus (et ce que ça rapporte), le temps qu'il reste pour arriver, et, une fois
        /// la course finie, si la consigne a été tenue et ce que la réputation y a gagné.
        /// </summary>
        public void SetExtras(string scenario, string objective, int bonus)
        {
            Scenario = scenario ?? string.Empty;
            Objective = objective ?? string.Empty;
            ObjectiveBonus = bonus;
            ObjectiveProgress = string.Empty;
            Deadline = -1f;
            ObjectiveResult = 0;
            ReputationDelta = 0;
        }

        public string Scenario { get; private set; }
        public string Objective { get; private set; }
        public int ObjectiveBonus { get; private set; }

        /// <summary>Suivi en direct de la consigne (« Nez : pas encore », « 23 s »).</summary>
        public string ObjectiveProgress { get; set; }

        /// <summary>Secondes restantes pour arriver sur place ; négatif = pas de limite.</summary>
        public float Deadline { get; set; }

        /// <summary>0 = pas encore jugée, 1 = tenue, -1 = ratée.</summary>
        public int ObjectiveResult { get; set; }

        public int ReputationDelta { get; set; }

        public string TargetName { get { return _targetName; } }
        public string TargetAge { get { return _targetAge; } }
        public string TargetClothing { get { return _targetClothing; } }
        public string TargetLocation { get { return _targetLocation; } }
        public string MeetingTime { get { return _meetingTime; } }
        public string TargetRecord { get { return _targetRecord; } }
        public string ClientName { get { return _clientName; } }
        public int Stars { get { return _stars; } }
        public int Reward { get { return _reward; } }
        public int Experience { get { return _experience; } }
        public string Review { get { return _review; } }
        public int ReviewStars { get { return _reviewStars; } }
        public string FriendName { get { return _friendName; } }
    }
}
