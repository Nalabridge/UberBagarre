using System;
using UberBagarre.Combat;

namespace UberBagarre.World
{
    /// <summary>Ce qu'est un commerce : ce qu'on y fait, pas seulement ce qu'on y achète.</summary>
    public enum ShopKind
    {
        Epicerie,
        Cave,
        Restaurant,
        Cafe,
        Bar,
        BoiteDeNuit,
        Pharmacie,
        Medecin,
        Vetements,
        Barbier,
        Tatoueur,
        Quincaillerie,
        MarcheNoir,
        Casino,
        Arcade,
        StandDeTir,
        SalleDeBoxe,
        Laverie,
        Poste,
        Immobilier,
        Concession,
        Avocat,
        PreteurSurGages,
        Motel,
        Commissariat
    }

    /// <summary>Ce que fait un article une fois payé.</summary>
    public enum ShopEffect
    {
        /// <summary>Rend des points de vie (amount).</summary>
        Heal,

        /// <summary>Vie au maximum, endurance aussi.</summary>
        HealFull,

        /// <summary>Un bonus de combat pour quelques minutes (stat, amount en %, minutes).</summary>
        Buff,

        /// <summary>Réputation (amount).</summary>
        Reputation,

        /// <summary>Une blessure en moins (amount = 1), ou toutes (amount = 0).</summary>
        Injury,

        /// <summary>De l'expérience (amount).</summary>
        Experience,

        /// <summary>Un point d'entraînement (le prix est celui de la salle).</summary>
        Training,

        /// <summary>Un colis, une enveloppe : de l'argent (amount = max), une fois par jour.</summary>
        Parcel,

        /// <summary>Un tuyau : une phrase qui aide.</summary>
        Rumor,

        /// <summary>Vend un objet du joueur (amount = ce qu'on en tire).</summary>
        Sell,

        /// <summary>Des courses : amount repas qui attendent dans le frigo de la planque.</summary>
        Groceries,

        /// <summary>Une trousse de soins à ranger chez soi (on s'en sert à la maison).</summary>
        Medkit,

        /// <summary>Des crochets pour les serrures de voitures (amount = combien).</summary>
        Lockpicks,

        /// <summary>L'avocat nettoie le casier (le prix dépend de son poids).</summary>
        ClearRecord
    }

    [Serializable]
    public class ShopItem
    {
        public string name;
        public string detail;
        public int price;
        public ShopEffect effect;
        public float amount;
        public StatType stat;
        public float minutes;

        /// <summary>Ce que ça remplit l'estomac (0 = ça ne se mange pas).</summary>
        public float food;

        /// <summary>Une seule fois par jour (colis, lessive, tournée).</summary>
        public bool daily;

        /// <summary>Une seule fois pour toute la partie (un tatouage, un poing américain).</summary>
        public bool once;

        public string Key(ShopKind kind)
        {
            return "boutique:" + kind + ":" + name;
        }

        public ShopItem(string name, int price, ShopEffect effect, float amount, string detail)
        {
            this.name = name;
            this.price = price;
            this.effect = effect;
            this.amount = amount;
            this.detail = detail;
        }

        public ShopItem Boost(StatType stat, float percent, float minutes)
        {
            effect = ShopEffect.Buff;
            this.stat = stat;
            amount = percent;
            this.minutes = minutes;
            return this;
        }

        /// <summary>Ça se mange : la faim baisse d'autant.</summary>
        public ShopItem Food(float satiety)
        {
            food = satiety;
            return this;
        }

        public ShopItem Daily()
        {
            daily = true;
            return this;
        }

        public ShopItem Once()
        {
            once = true;
            return this;
        }
    }

    /// <summary>
    /// Le catalogue de chaque commerce. Chaque magasin SERT à quelque chose dans la vie d'un
    /// bagarreur : manger et se soigner avant un combat, boire pour se donner du courage,
    /// s'habiller, se faire tatouer pour la réputation, s'entraîner, jouer, acheter une voiture
    /// ou une maison. Les écrans spéciaux (vêtements, concession, immobilier, casino) sont
    /// dessinés à part par <c>ShopScreen</c> ; leurs catalogues ici ne sont que le complément.
    /// </summary>
    public static class ShopCatalog
    {
        public static string Describe(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Epicerie: return "Épicerie";
                case ShopKind.Cave: return "Cave à alcools";
                case ShopKind.Restaurant: return "Restaurant";
                case ShopKind.Cafe: return "Café";
                case ShopKind.Bar: return "Bar";
                case ShopKind.BoiteDeNuit: return "Boîte de nuit";
                case ShopKind.Pharmacie: return "Pharmacie";
                case ShopKind.Medecin: return "Cabinet médical";
                case ShopKind.Vetements: return "Vêtements";
                case ShopKind.Barbier: return "Barbier";
                case ShopKind.Tatoueur: return "Tatoueur";
                case ShopKind.Quincaillerie: return "Quincaillerie";
                case ShopKind.MarcheNoir: return "Marché noir";
                case ShopKind.Casino: return "Casino";
                case ShopKind.Arcade: return "Salle d'arcade";
                case ShopKind.StandDeTir: return "Stand de tir";
                case ShopKind.SalleDeBoxe: return "Salle de boxe";
                case ShopKind.Laverie: return "Laverie";
                case ShopKind.Poste: return "Bureau de poste";
                case ShopKind.Immobilier: return "Agence immobilière";
                case ShopKind.Concession: return "Concession auto";
                case ShopKind.Avocat: return "Cabinet d'avocat";
                case ShopKind.PreteurSurGages: return "Prêteur sur gages";
                case ShopKind.Motel: return "Accueil du motel";
                default: return "Commissariat";
            }
        }

        /// <summary>Ce que dit la personne derrière le comptoir quand on s'approche.</summary>
        public static string Greeting(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Epicerie: return "Bonsoir. Prends ce que tu veux, on ferme tard.";
                case ShopKind.Cave: return "Pièce d'identité ? ... Laisse tomber, t'as la tête d'un majeur.";
                case ShopKind.Restaurant: return "Une table pour un ? Installe-toi, je t'apporte la carte.";
                case ShopKind.Cafe: return "Un café ? T'as une tête à avoir besoin d'un double.";
                case ShopKind.Bar: return "Qu'est-ce que je te sers ? Et pas de bagarre ici, hein.";
                case ShopKind.BoiteDeNuit: return "Le vestiaire c'est à gauche. Le bar c'est tout droit.";
                case ShopKind.Pharmacie: return "Bonsoir. Vous saignez sur mon comptoir, là.";
                case ShopKind.Medecin: return "Asseyez-vous. Encore une chute dans l'escalier, je suppose ?";
                case ShopKind.Vetements: return "Bienvenue ! Tout est en rayon, les cabines sont au fond.";
                case ShopKind.Barbier: return "Assieds-toi. Je te fais quoi, propre ou méchant ?";
                case ShopKind.Tatoueur: return "Ça pique, je te préviens. Tu veux quoi, et où ?";
                case ShopKind.Quincaillerie: return "Des bandes, du scotch, un protège-dents ? J'ai tout ça.";
                case ShopKind.MarcheNoir: return "Tu ne m'as jamais vu. Qu'est-ce que tu cherches ?";
                case ShopKind.Casino: return "Bienvenue au casino. Les jetons sont à la caisse, la chance est gratuite.";
                case ShopKind.Arcade: return "Une partie ? Le record de la borne de baston tient depuis 1998.";
                case ShopKind.StandDeTir: return "Casque, lunettes, et on vise la cible, pas le voisin.";
                case ShopKind.SalleDeBoxe: return "Tu veux apprendre à cogner proprement ? Le coach est là.";
                case ShopKind.Laverie: return "Les machines sont libres. Le sang, c'est à 60 degrés.";
                case ShopKind.Poste: return "Bonjour. Vous venez chercher un colis ?";
                case ShopKind.Immobilier: return "Vous cherchez à acheter ? Nous avons de très belles affaires.";
                case ShopKind.Concession: return "Elles sont belles, hein ? Toutes révisées. Presque.";
                case ShopKind.Avocat: return "Maître Lenoir. Racontez-moi tout, je ne juge pas. Je facture.";
                case ShopKind.PreteurSurGages: return "Je rachète, je revends. Pas de questions.";
                case ShopKind.Motel: return "Motel Hyland, bonsoir. Chambre, loyer, ou tu te perds ?";
                default: return "Commissariat. C'est pour une plainte ou pour payer ?";
            }
        }

        public static ShopItem[] Items(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Epicerie:
                    return new[]
                    {
                        new ShopItem("Sandwich jambon-beurre", 6, ShopEffect.Heal, 25f, "+25 PV. De quoi tenir jusqu'au prochain combat.").Food(30f),
                        new ShopItem("Boisson énergisante", 5, ShopEffect.Buff, 0f, "+12 % de vitesse de frappe pendant 4 minutes.")
                            .Boost(StatType.AttackSpeed, 12f, 4f),
                        new ShopItem("Barre protéinée", 4, ShopEffect.Buff, 0f, "+20 % d'endurance maximale pendant 5 minutes.").Food(10f)
                            .Boost(StatType.MaxStamina, 20f, 5f),
                        new ShopItem("Pansements et désinfectant", 12, ShopEffect.Heal, 45f, "+45 PV. Ça pique, mais ça tient."),
                        new ShopItem("Courses de la semaine", 32, ShopEffect.Groceries, 5f, "Cinq repas pour le frigo de la planque (on mange chez soi, gratuitement).").Food(8f),
                        new ShopItem("Paquet de clopes", 11, ShopEffect.Buff, 0f, "Ça calme les nerfs : +8 de défense pendant 3 minutes.")
                            .Boost(StatType.Defense, 8f, 3f)
                    };

                case ShopKind.Cave:
                    return new[]
                    {
                        new ShopItem("Pack de bières", 9, ShopEffect.Buff, 0f, "Du courage en canette : +10 % de puissance pendant 4 minutes.")
                            .Boost(StatType.Strength, 10f, 4f),
                        new ShopItem("Flasque de whisky", 18, ShopEffect.Buff, 0f, "+18 % de puissance pendant 3 minutes. La tête tourne un peu.")
                            .Boost(StatType.Strength, 18f, 3f),
                        new ShopItem("Bouteille d'eau", 2, ShopEffect.Heal, 10f, "+10 PV. Le choix raisonnable."),
                        new ShopItem("Bouteille de champagne", 60, ShopEffect.Reputation, 3f, "Pour arroser une victoire : +3 réputation.").Daily()
                    };

                case ShopKind.Restaurant:
                    return new[]
                    {
                        new ShopItem("Plat du jour", 16, ShopEffect.Heal, 60f, "+60 PV. Un vrai repas, assis.").Food(55f),
                        new ShopItem("Steak-frites", 24, ShopEffect.Buff, 0f, "+25 % d'endurance maximale pendant 8 minutes.").Food(60f)
                            .Boost(StatType.MaxStamina, 25f, 8f),
                        new ShopItem("Menu complet", 38, ShopEffect.HealFull, 0f, "Entrée, plat, dessert : vie et endurance au maximum.").Food(85f),
                        new ShopItem("Café serré", 3, ShopEffect.Buff, 0f, "+8 % de vitesse de frappe pendant 3 minutes.")
                            .Boost(StatType.AttackSpeed, 8f, 3f)
                    };

                case ShopKind.Cafe:
                    return new[]
                    {
                        new ShopItem("Double espresso", 3, ShopEffect.Buff, 0f, "+10 % de vitesse de frappe pendant 4 minutes.")
                            .Boost(StatType.AttackSpeed, 10f, 4f),
                        new ShopItem("Croissant", 2, ShopEffect.Heal, 12f, "+12 PV.").Food(12f),
                        new ShopItem("Petit-déjeuner complet", 14, ShopEffect.Heal, 45f, "+45 PV, et la journée commence mieux.").Food(40f),
                        new ShopItem("Journal du jour", 2, ShopEffect.Rumor, 0f, "Les faits divers d'Hyland : on y apprend des choses.")
                    };

                case ShopKind.Bar:
                    return new[]
                    {
                        new ShopItem("Demi pression", 5, ShopEffect.Buff, 0f, "+8 % de puissance pendant 4 minutes.")
                            .Boost(StatType.Strength, 8f, 4f),
                        new ShopItem("Shot de vodka", 7, ShopEffect.Buff, 0f, "+15 % de puissance pendant 2 minutes. Cul sec.")
                            .Boost(StatType.Strength, 15f, 2f),
                        new ShopItem("Tournée générale", 80, ShopEffect.Reputation, 6f, "Tout le bar boit à ta santé : +6 réputation.").Daily(),
                        new ShopItem("Écouter les rumeurs", 0, ShopEffect.Rumor, 0f, "Le barman entend tout. Pour un pourboire, il parle.")
                    };

                case ShopKind.BoiteDeNuit:
                    return new[]
                    {
                        new ShopItem("Cocktail maison", 14, ShopEffect.Buff, 0f, "+12 % de puissance, +10 % de vitesse… pendant 3 minutes.")
                            .Boost(StatType.Strength, 12f, 3f),
                        new ShopItem("Carré VIP", 250, ShopEffect.Reputation, 12f, "Une bouteille, une table, tout le monde te voit : +12 réputation.").Daily(),
                        new ShopItem("Danser", 0, ShopEffect.Experience, 5f, "Tu te défoules sur la piste : +5 XP (souplesse)."),
                        new ShopItem("Parler au videur", 0, ShopEffect.Rumor, 0f, "Il connaît tous les durs de la ville.")
                    };

                case ShopKind.Pharmacie:
                    return new[]
                    {
                        new ShopItem("Antidouleurs", 25, ShopEffect.Buff, 0f, "+12 de défense pendant 5 minutes : tu sens moins les coups.")
                            .Boost(StatType.Defense, 12f, 5f),
                        new ShopItem("Trousse de soins", 45, ShopEffect.Medkit, 1f, "À ranger chez toi : on s'y soigne une blessure, et on se remet sur pied."),
                        new ShopItem("Premiers secours", 30, ShopEffect.HealFull, 0f, "Sur place : vie et endurance au maximum."),
                        new ShopItem("Attelle et bandages", 120, ShopEffect.Injury, 1f, "Une blessure en moins (sans attendre une nuit)."),
                        new ShopItem("Vitamines", 15, ShopEffect.Buff, 0f, "+15 % d'endurance maximale pendant 10 minutes.")
                            .Boost(StatType.MaxStamina, 15f, 10f)
                    };

                case ShopKind.Medecin:
                    return new[]
                    {
                        new ShopItem("Consultation", 90, ShopEffect.HealFull, 0f, "Le médecin te recoud : vie et endurance au maximum."),
                        new ShopItem("Soigner une blessure", 150, ShopEffect.Injury, 1f, "Une blessure en moins."),
                        new ShopItem("Bilan complet", 400, ShopEffect.Injury, 0f, "Toutes tes blessures soignées. Tu repars à zéro."),
                        new ShopItem("Certificat de bonne santé", 30, ShopEffect.Reputation, 1f, "Ça rassure les clients : +1 réputation.").Daily()
                    };

                case ShopKind.Barbier:
                    return new[]
                    {
                        new ShopItem("Coupe", 25, ShopEffect.Reputation, 2f, "Propre sur toi : +2 réputation.").Daily(),
                        new ShopItem("Rasage à l'ancienne", 15, ShopEffect.Reputation, 1f, "Serviette chaude, coupe-chou : +1 réputation.").Daily(),
                        new ShopItem("Discuter avec le barbier", 0, ShopEffect.Rumor, 0f, "Il coiffe tout le quartier, il sait tout.")
                    };

                case ShopKind.Tatoueur:
                    return new[]
                    {
                        new ShopItem("Tatouage « ÜBER » sur l'avant-bras", 180, ShopEffect.Reputation, 10f, "Tout le monde sait pour qui tu cognes : +10 réputation.").Once(),
                        new ShopItem("Tête de mort sur la main", 240, ShopEffect.Reputation, 12f, "Intimidant : +12 réputation.").Once(),
                        new ShopItem("Dragon dans le dos", 520, ShopEffect.Reputation, 20f, "Six heures d'aiguille : +20 réputation.").Once(),
                        new ShopItem("Piercing", 40, ShopEffect.Reputation, 3f, "+3 réputation.").Once()
                    };

                case ShopKind.Quincaillerie:
                    return new[]
                    {
                        new ShopItem("Bandes de boxe", 30, ShopEffect.Buff, 0f, "Des mains bien bandées : +10 % de puissance pendant 10 minutes.")
                            .Boost(StatType.Strength, 10f, 10f),
                        new ShopItem("Protège-dents", 45, ShopEffect.Buff, 0f, "+10 de défense pendant 10 minutes.")
                            .Boost(StatType.Defense, 10f, 10f),
                        new ShopItem("Chaussures de sécurité", 60, ShopEffect.Buff, 0f, "+8 % de puissance de chute (coups de pied) pendant 10 minutes.")
                            .Boost(StatType.KnockdownPower, 8f, 10f),
                        new ShopItem("Jeu de crochets (x3)", 24, ShopEffect.Lockpicks, 3f, "Pour les serrures récalcitrantes. On ne pose pas de questions."),
                        new ShopItem("Ruban adhésif", 4, ShopEffect.Heal, 8f, "Une arcade qui saigne, on scotche : +8 PV.")
                    };

                case ShopKind.MarcheNoir:
                    return new[]
                    {
                        new ShopItem("Crochets de pro (x8)", 60, ShopEffect.Lockpicks, 8f, "Acier trempé, huit pièces. Ils ouvrent tout ce qui roule."),
                        new ShopItem("Poing américain", 600, ShopEffect.Buff, 0f, "Dans la poche, pour les grands soirs : +30 % de puissance pendant 5 minutes.")
                            .Boost(StatType.Strength, 30f, 5f),
                        new ShopItem("Stéroïdes", 180, ShopEffect.Buff, 0f, "+40 % d'endurance maximale pendant 6 minutes. À tes risques.")
                            .Boost(StatType.MaxStamina, 40f, 6f),
                        new ShopItem("Adrénaline", 220, ShopEffect.Buff, 0f, "+20 % de vitesse de frappe pendant 3 minutes.")
                            .Boost(StatType.AttackSpeed, 20f, 3f),
                        new ShopItem("Liste des durs du coin", 120, ShopEffect.Rumor, 0f, "Qui cogne, où, et pour qui.")
                    };

                case ShopKind.Arcade:
                    return new[]
                    {
                        new ShopItem("Borne de baston", 1, ShopEffect.Experience, 6f, "Tu étudies les enchaînements : +6 XP."),
                        new ShopItem("Machine à frapper", 2, ShopEffect.Experience, 10f, "Tu cognes le punching-ball de la borne : +10 XP.").Daily(),
                        new ShopItem("Flipper", 1, ShopEffect.Reputation, 0f, "Pour le plaisir. Tilt.")
                    };

                case ShopKind.StandDeTir:
                    return new[]
                    {
                        new ShopItem("Série de tirs", 15, ShopEffect.Buff, 0f, "Le calme et la précision : +6 % de vitesse de frappe pendant 6 minutes.")
                            .Boost(StatType.AttackSpeed, 6f, 6f),
                        new ShopItem("Cours de concentration", 45, ShopEffect.Experience, 20f, "+20 XP.").Daily()
                    };

                case ShopKind.SalleDeBoxe:
                    return new[]
                    {
                        new ShopItem("Séance au sac", 10, ShopEffect.Experience, 15f, "+15 XP.").Daily(),
                        new ShopItem("Sparring avec le coach", 40, ShopEffect.Experience, 40f, "+40 XP. Tu en ressors avec un bleu de plus.").Daily(),
                        new ShopItem("Étirements et récupération", 12, ShopEffect.HealFull, 0f, "Vie et endurance au maximum.")
                    };

                case ShopKind.Laverie:
                    return new[]
                    {
                        new ShopItem("Lessive (60 degrés)", 6, ShopEffect.Reputation, 1f, "Plus une trace de sang : +1 réputation.").Daily(),
                        new ShopItem("Attendre en lisant", 0, ShopEffect.Rumor, 0f, "Les gens parlent devant les machines.")
                    };

                case ShopKind.Poste:
                    return new[]
                    {
                        new ShopItem("Retirer un colis", 0, ShopEffect.Parcel, 90f, "Des fans t'envoient des enveloppes (une fois par jour).").Daily(),
                        new ShopItem("Envoyer de l'argent à ta mère", 100, ShopEffect.Reputation, 4f, "Elle sera fière de toi : +4 réputation.").Daily()
                    };

                case ShopKind.Avocat:
                    return new[]
                    {
                        new ShopItem("Nettoyer le casier", 0, ShopEffect.ClearRecord, 0f, "Maître Lenoir fait disparaître ton casier judiciaire : 300 € plus 120 € par délit. Il ne juge pas. Il facture."),
                        new ShopItem("Faire classer une plainte", 250, ShopEffect.Reputation, 15f, "Les clients oublient : +15 réputation.").Daily(),
                        new ShopItem("Consultation", 60, ShopEffect.Rumor, 0f, "Le droit, et ce qu'on en fait.")
                    };

                case ShopKind.PreteurSurGages:
                    return new[]
                    {
                        new ShopItem("Vendre ta montre", 0, ShopEffect.Sell, 120f, "Il en donne 120 €. Tu n'en avais pas besoin.").Once(),
                        new ShopItem("Vendre ta chaîne en or", 0, ShopEffect.Sell, 260f, "260 €. Elle venait de ton oncle.").Once(),
                        new ShopItem("Racheter une chaîne en or", 400, ShopEffect.Reputation, 8f, "Brillant : +8 réputation.").Once()
                    };

                case ShopKind.Motel:
                    return new[]
                    {
                        new ShopItem("Café du distributeur", 1, ShopEffect.Heal, 5f, "+5 PV. Il a le goût du gobelet."),
                        new ShopItem("Demander s'il y a du courrier", 0, ShopEffect.Rumor, 0f, "Le gérant voit passer tout le monde.")
                    };

                case ShopKind.Commissariat:
                    return new[]
                    {
                        new ShopItem("Payer tes amendes", 120, ShopEffect.Reputation, 5f, "Casier plus léger : +5 réputation.").Daily(),
                        new ShopItem("Consulter les avis de recherche", 0, ShopEffect.Rumor, 0f, "Les visages affichés au mur.")
                    };

                default:
                    return new ShopItem[0];
            }
        }

        private static readonly string[] Rumors =
        {
            "Il paraît que les Kovac ne sortent jamais sans leur voiture. Surveille le parking.",
            "Le Taureau s'entraîne tous les soirs au Vertigo. Il frappe fort mais il se fatigue vite.",
            "Les combats de rue rapportent plus la nuit : les clients paient mieux quand il fait noir.",
            "Un coup de pied fait tomber plus sûrement qu'un direct. Encore faut-il le placer.",
            "Ceux qui boivent frappent plus fort. Mais ils encaissent moins bien, crois-moi.",
            "La concession fait des prix aux habitués. Enfin, pas encore à toi.",
            "Le docteur du centre recoud sans poser de questions. C'est cher, mais tu repars neuf.",
            "Si tu dors mal, tu cognes mal. Rentre dormir entre deux courses.",
            "Il y a un marché noir près des docks. Frappe deux fois, pas trois.",
            "La salle de boxe du centre social prend tout le monde. Même toi.",
            "Les mauvais avis font fuir les clients. Soigne ta réputation.",
            "Au casino, la maison gagne toujours. Sauf ce soir, peut-être."
        };

        public static string Rumor(int seed)
        {
            return Rumors[(seed % Rumors.Length + Rumors.Length) % Rumors.Length];
        }
    }
}
