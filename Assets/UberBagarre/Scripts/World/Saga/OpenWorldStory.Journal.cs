using UberBagarre.UI;

namespace UberBagarre.World
{
    /// <summary>
    /// Ce que la ville écrit : les articles de <i>Hyland Info</i> (le journal en ligne, sur
    /// l'ordinateur, avec une alerte SMS) et les mails de l'histoire. Tout est rangé ici par
    /// identifiant : la sauvegarde ne retient que « tel article est paru », le texte revient
    /// d'ici au chargement.
    /// </summary>
    public partial class OpenWorldStory
    {
        private static readonly string[] ArticleIds =
        {
            "moretti", "karim", "kovac", "motel", "agressions", "parking", "fosse", "archive", "casino",
            "carnet_duval", "carnet_brandt", "carnet_garde", "disparition", "chantier", "election", "keller",
            "chasse", "holt", "appli", "radiation", "roi", "fantome", "chute", "tournoi", "chen", "officiel"
        };

        private static readonly string[] MailIds = { "premiere", "carte", "serveurs", "lenoir" };

        /// <summary>Un article paraît (alerte SMS « HYLAND INFO » si <paramref name="notify"/>).</summary>
        private void News(string id)
        {
            if (Done("news:" + id)) return;
            Set("news:" + id);
            PublishArticle(id, true);
        }

        private void PublishArticle(string id, bool notify, bool alert = true)
        {
            string title, lead, body;
            int day;
            if (!Article(id, out title, out lead, out body, out day)) return;
            if (day <= 0) day = Day;
            NewsFeed.Publish(id, day, title, lead, body, notify);
            if (notify && alert) Sms("HYLAND INFO", "ALERTE — " + title);
        }

        /// <summary>L'article a été ouvert sur l'ordinateur.</summary>
        private static bool ArticleRead(string id)
        {
            var articles = NewsFeed.Articles;
            for (int i = 0; i < articles.Count; i++)
            {
                if (articles[i].id == id) return !articles[i].unread;
            }

            return false;
        }

        private void SendMail(string id, bool unread)
        {
            string from, subject, body;
            if (!MailText(id, out from, out subject, out body)) return;
            if (unread) Set("mail:" + id);
            Mail(from, subject, body, unread);
        }

        private bool Article(string id, out string title, out string lead, out string body, out int day)
        {
            day = 0;
            switch (id)
            {
                case "moretti":
                    title = "Nuit agitée devant le Vertigo";
                    lead = "Le videur Bruno Moretti retrouvé au sol à la fermeture. Personne n'a rien vu.";
                    body = "Bruno Moretti, 45 ans, videur du Vertigo depuis douze ans, a été retrouvé au sol devant le club vers minuit. " +
                           "« Il est sorti fumer, comme tous les soirs », raconte un habitué. Aucun témoin, aucune plainte. " +
                           "Seul détail troublant : un passant affirme avoir vu quelqu'un photographier la victime avec son téléphone avant de repartir.";
                    return true;

                case "karim":
                    title = "Un livreur de Taco Ticklers hospitalisé";
                    lead = "Karim, 22 ans, frappé derrière le restaurant. Son patron parle d'une « expédition punitive ».";
                    body = "Karim B., livreur, a été admis au centre médical avec un bras cassé. « Il avait refusé une livraison, c'est tout ce que je sais », " +
                           "confie son patron. La victime refuse de parler. C'est la septième agression de ce type en un mois à Hyland.";
                    return true;

                case "kovac":
                    title = "Règlement de comptes derrière la pizzeria";
                    lead = "Dragan Kovac, figure connue des docks, retrouvé le nez en sang.";
                    body = "Les frères Kovac, qui tiennent officieusement les docks d'Hyland, n'ont pas porté plainte. " +
                           "« Dans ce milieu, on règle ses affaires soi-même », glisse un policier sous couvert d'anonymat. Certains craignent des représailles.";
                    return true;

                case "motel":
                    title = "Bagarre générale au motel Hyland";
                    lead = "Quatre hommes contre un seul, à la tombée de la nuit. Les quatre sont repartis en boitant.";
                    body = "Le gérant du motel Hyland décrit « un vrai film » : quatre individus ont attendu un locataire à la sortie de sa chambre. " +
                           "« Je sais pas ce qu'il mange, le petit de la 3, mais j'en veux. » Milan Kovac, le cadet de la fratrie, faisait partie des blessés.";
                    return true;

                case "agressions":
                    title = "Agressions « commandées » : une inspectrice de la capitale enquête";
                    lead = "Onze victimes en un mois, toutes photographiées au sol. La police locale parle de « faits divers ».";
                    body = "L'inspectrice Camille Duval, mutée à Hyland il y a trois semaines, a reçu la charge de ces dossiers. Point commun des victimes : " +
                           "des dettes, des conflits, ou un « non » dit à la mauvaise personne. Le commissaire Brandt, lui, dément tout phénomène organisé : " +
                           "« Hyland est une ville tranquille. »";
                    return true;

                case "parking":
                    title = "Le maire Holt annonce le « Parking de l'Avenir »";
                    lead = "Le centre social et sa salle de boxe rasés d'ici l'été. « Hyland doit se moderniser. »";
                    body = "Arthur Holt, réélu depuis douze ans, a présenté hier son projet phare : un parking de 400 places à la place du centre social des Slums. " +
                           "Ray Dumont, 64 ans, qui y entraîne les jeunes du quartier depuis quarante ans, n'a pas été consulté. " +
                           "« On remplace des gamins par des voitures », a-t-il réagi. Le maire promet « une concertation ».";
                    return true;

                case "fosse":
                    title = "Fosse clandestine : la rumeur d'un nouveau champion";
                    lead = "Le Taureau serait tombé. Le nom qui circule : un certain Marchal.";
                    body = "Officiellement, la « Fosse » n'existe pas. Officieusement, tout Hyland parie dessus. Le Taureau, invaincu depuis deux ans, " +
                           "aurait perdu vendredi soir face à un ancien boxeur radié par la fédération. Rosa Delmas, propriétaire du Vertigo, n'a pas répondu à nos questions.";
                    return true;

                case "archive":
                    title = "[ARCHIVES] Finale régionale : Léo Marchal radié à vie";
                    lead = "Il y a trois ans. Effondré au 3e round, le favori est accusé d'avoir truqué son combat.";
                    body = "Léo Marchal, 24 ans, s'est effondré au troisième round de la finale régionale, à l'ancienne salle des fêtes. " +
                           "Vue trouble, jambes « en coton », selon son entraîneur Ray Dumont. La fédération a découvert près de 80 000 € de paris contre lui " +
                           "et l'a radié à vie. Selon nos informations, une large part des gains a été versée par une société écran, SARK HOLDING, " +
                           "domiciliée à l'adresse du Casino Royal. L'enquête a été classée sans suite par le commissariat d'Hyland.";
                    day = 1;
                    return true;

                case "casino":
                    title = "Casino Royal : nuit agitée, le directeur hospitalisé";
                    lead = "Victor Sarkis retrouvé dans son bureau, le coffre ouvert. « Rien n'a été volé », assure la direction.";
                    body = "Le directeur du Casino Royal a été transporté au centre médical dans la nuit. La direction parle d'un « malaise ». " +
                           "Les vigiles, eux, évoquent un intrus « en uniforme de serveur ». Aucune plainte n'a été déposée.";
                    return true;

                case "carnet_duval":
                    title = "Enquête ouverte sur des « paiements occultes » à Hyland";
                    lead = "L'inspectrice Duval saisit la justice. Des élus et des policiers seraient cités.";
                    body = "Un document manuscrit, surnommé « le carnet noir », serait entre les mains de la police judiciaire. " +
                           "On y trouverait des noms, des montants, et une initiale qui revient partout : « H. ». Le commissaire Brandt dénonce « une manipulation ».";
                    return true;

                case "carnet_brandt":
                    title = "Un suspect « tombé dans l'escalier » au commissariat";
                    lead = "Le commissaire Brandt assure que la garde à vue s'est déroulée « dans le respect des règles ».";
                    body = "Un homme placé en garde à vue a été retrouvé couvert d'ecchymoses au petit matin. Selon le commissariat, il serait tombé dans l'escalier. " +
                           "Il n'y a pas d'escalier entre les cellules et le bureau du commissaire. L'inspectrice Duval a demandé sa libération immédiate.";
                    return true;

                case "carnet_garde":
                    title = "Des notables d'Hyland victimes de chantage ?";
                    lead = "Plusieurs personnalités auraient reçu des lettres anonymes. Aucune n'a porté plainte.";
                    body = "Un conseiller municipal, un promoteur, un juge : tous auraient versé des sommes importantes ces derniers jours. " +
                           "Personne ne veut expliquer pourquoi. En ville, on parle d'un carnet volé au Casino Royal.";
                    return true;

                case "disparition":
                    title = "Disparition d'un jeune développeur des Slums";
                    lead = "Sami Benali, 27 ans, n'a plus donné de nouvelles depuis trois jours.";
                    body = "Sa petite sœur Inès, 15 ans, hospitalisée pour une maladie cardiaque, a lancé un appel : « Sami ne m'aurait jamais laissée seule. » " +
                           "La police n'a pas ouvert d'enquête.";
                    return true;

                case "chantier":
                    title = "Rixe à l'usine désaffectée du chantier";
                    lead = "Un adolescent retrouvé sain et sauf, les frères Kovac introuvables.";
                    body = "Jeff, 16 ans, skateur bien connu du Shred Shack, aurait été retenu plusieurs heures à l'usine chimique désaffectée. " +
                           "Il a été libéré par « un type qui cogne comme un camion », selon ses propres mots.";
                    return true;

                case "election":
                    title = "Élection municipale : Holt contre Keller, dernière semaine";
                    lead = "Le maire sortant, favori, face à une candidate qui promet de « rendre la ville à ses habitants ».";
                    body = "Hélène Keller, ancienne directrice d'école, fait campagne à pied dans les Slums. Elle dénonce « une ville gérée par la peur ». " +
                           "Plusieurs de ses soutiens ont été agressés ces derniers mois. Arthur Holt, lui, promet « la sécurité pour tous ».";
                    return true;

                case "keller":
                    title = "Mme Keller prise à partie pendant sa campagne";
                    lead = "Des individus cagoulés repoussés par un homme qui l'accompagnait.";
                    body = "La candidate a été attaquée entre la mairie et le bureau de poste. « Un jeune homme s'est interposé. Il a une drôle de tête de garde du corps, " +
                           "mais je lui dois beaucoup », a-t-elle déclaré. Le commissariat n'a pas souhaité commenter.";
                    return true;

                case "chasse":
                    title = "Avis de recherche : Léo Marchal, « individu dangereux »";
                    lead = "Le commissaire Brandt mobilise toutes ses unités. « Il sera arrêté avant l'élection. »";
                    body = "L'ancien boxeur Léo Marchal est recherché pour « troubles graves à l'ordre public ». Toute personne qui l'hébergerait serait poursuivie. " +
                           "L'inspectrice Duval, interrogée, s'est contentée d'un « tiens donc ».";
                    return true;

                case "holt":
                    title = "Séisme à la mairie : Arthur Holt et le commissaire Brandt arrêtés";
                    lead = "La nuit de l'élection, l'inspectrice Duval a interpellé le maire dans son propre bureau.";
                    body = "Corruption, association de malfaiteurs, violences commanditées : la liste est longue. Selon l'enquête, le maire était le véritable " +
                           "propriétaire d'Über Bagarre, une application utilisée pour faire taire ses opposants et vider les quartiers qu'il voulait raser. " +
                           "Hélène Keller a été élue avec 61 % des voix.";
                    return true;

                case "appli":
                    title = "Über Bagarre fermée par la justice";
                    lead = "L'application des agressions commandées n'existe plus. Ses serveurs ont été saisis.";
                    body = "Des centaines de « courses » ont été retrouvées dans les journaux de l'application. Les victimes sont invitées à se faire connaître. " +
                           "Le projet de parking à la place du centre social est abandonné.";
                    return true;

                case "radiation":
                    title = "Léo Marchal réhabilité : sa radiation annulée";
                    lead = "La fédération reconnaît que sa finale avait été truquée. Il remontera sur un ring officiel.";
                    body = "Trois ans après, la vérité : le boxeur avait été drogué avant sa finale régionale. " +
                           "« Je l'ai toujours su », a déclaré son entraîneur Ray Dumont, qui sera dans son coin pour son retour.";
                    return true;

                case "roi":
                    title = "Holt réélu, et un nouveau « conseiller sécurité »";
                    lead = "Le maire s'entoure d'un ancien boxeur. Les agressions ont cessé. Tout le monde a compris pourquoi.";
                    body = "Léo Marchal s'installe au manoir sur la colline. Interrogé sur son rôle, le maire sourit : « Hyland a besoin de quelqu'un qui connaît la rue. » " +
                           "Ray Dumont a fermé sa salle. Une infirmière du cabinet médical a démissionné.";
                    return true;

                case "fantome":
                    title = "Léo Marchal introuvable depuis la nuit de l'élection";
                    lead = "Sa mère aurait quitté la ville le même matin. Une vieille voiture vue sur la route du nord.";
                    body = "La mairie a été saccagée, le coffre vidé. Arthur Holt, réélu de justesse, refuse de commenter. " +
                           "Quelque part, au nord, un lever de soleil.";
                    return true;

                case "chute":
                    title = "La fin d'un boxeur";
                    lead = "L'ancien espoir de la boxe d'Hyland n'aura pas vu l'élection.";
                    body = "Arthur Holt a été réélu dimanche. Au centre social, Ray Dumont a accroché une vieille photo au mur, à côté du ring.";
                    return true;

                case "tournoi":
                    title = "Le centre social fait salle comble";
                    lead = "Le tournoi de Ray Dumont a récolté de quoi payer un avocat contre le projet de parking.";
                    body = "Trois soirs, des combats propres, des gants, des arbitres : « de la vraie boxe ». Le public est venu en nombre. " +
                           "Un ancien boxeur radié y a fait sensation. La mairie, invitée, n'est pas venue.";
                    return true;

                case "chen":
                    title = "Le Dragon d'Or respire";
                    lead = "Les racketteurs du jeudi ne passent plus chez M. Chen.";
                    body = "Depuis vingt ans, M. Chen sert les meilleurs raviolis du quartier. Depuis deux ans, il payait « une assurance » aux hommes des Kovac. " +
                           "« C'est fini », sourit-il, sans vouloir dire pourquoi. Il offre désormais le thé à tous les boxeurs.";
                    return true;

                case "officiel":
                    title = "Retour gagnant pour Marchal";
                    lead = "Premier match officiel depuis trois ans. Victoire, sous les yeux de sa mère.";
                    body = "Au centre social, sauvé du parking, la salle était pleine. Ray Dumont dans le coin, sa mère au premier rang. " +
                           "« Il a toujours été le meilleur », a-t-elle dit. « Il mange mal, mais il est le meilleur. »";
                    return true;
            }

            title = lead = body = null;
            return false;
        }

        private bool MailText(string id, out string from, out string subject, out string body)
        {
            switch (id)
            {
                case "premiere":
                    from = "Über Bagarre";
                    subject = "Première course";
                    body = "Bravo. Ta page de réputation est ouverte : les clients lisent les avis.\n" +
                           "Astuce : l'armoire de ta chambre, c'est ton vestiaire (tenues, entraînement). Le frigo, c'est ta vie.";
                    return true;

                case "carte":
                    from = "C. Duval";
                    subject = "Ma carte";
                    body = "Inspectrice Camille Duval, police judiciaire.\nLigne directe, jour et nuit.\n\n" +
                           "« Le jour où vous en aurez assez d'avoir peur, appelez-moi. »";
                    return true;

                case "serveurs":
                    from = Done("sami:pardon") ? "Sami" : "Jeff";
                    subject = "Les serveurs de l'appli";
                    body = (Done("sami:pardon")
                               ? "Mon vieux mot de passe marche encore. J'ai tout copié.\n\n"
                               : "Le tel de Milan avait l'appli en mode admin. J'ai fouillé.\n\n") +
                           "JOURNAL DES CONNEXIONS — ÜBER BAGARRE\n" +
                           "Compte administrateur : a.holt@mairie-hyland.fr\n" +
                           "Dernières commandes validées par l'administrateur :\n" +
                           "  — « Centre social, Slums » : vider le quartier avant les travaux.\n" +
                           "  — « H. Keller » : cinq étoiles. Avant l'élection.\n\n" +
                           "H. Comme dans le carnet.";
                    return true;

                case "lenoir":
                    from = "Cabinet Lenoir";
                    subject = "Votre casier";
                    body = "Maître Lenoir, avocat.\nNous nettoyons les casiers judiciaires. Discrétion garantie, tarifs sur place.\n" +
                           "Cabinet Lenoir, centre-ville.";
                    return true;
            }

            from = subject = body = null;
            return false;
        }
    }
}
