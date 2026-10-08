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

        /// <summary>Ce que dit le joueur pour le commander (« Je te prends un burger. »).</summary>
        public string order;

        /// <summary>Ce que répond la personne au comptoir (le prix est ajouté s'il y en a un).</summary>
        public string reply;

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

        /// <summary>Les répliques de la commande : le joueur, puis le comptoir.</summary>
        public ShopItem Says(string order, string reply)
        {
            this.order = order;
            this.reply = reply;
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
            return Items(kind, null);
        }

        /// <summary>
        /// Ce qu'on trouve au comptoir. Les restaurants ont chacun leur carte (des tacos chez
        /// Taco Ticklers, des burgers au Diner, des nouilles au Dragon d'Or, des pizzas chez
        /// Mamma) ; le nom du commerce la choisit.
        /// </summary>
        public static ShopItem[] Items(ShopKind kind, string shopName)
        {
            string n = string.IsNullOrEmpty(shopName) ? string.Empty : shopName.ToLowerInvariant();
            switch (kind)
            {
                case ShopKind.Epicerie:
                    return new[]
                    {
                        new ShopItem("Sandwich jambon-beurre", 6, ShopEffect.Heal, 25f, "+25 PV. De quoi tenir jusqu'au prochain combat.").Food(30f)
                            .Says("Un jambon-beurre, s'il te plaît.", "Un jambon-beurre, tiens."),
                        new ShopItem("Hot-dog", 4, ShopEffect.Heal, 20f, "+20 PV. Saucisse, oignons frits, moutarde.").Food(25f)
                            .Says("Fais-moi un hot-dog.", "Un hot-dog, avec tout dessus."),
                        new ShopItem("Boisson énergisante", 5, ShopEffect.Buff, 0f, "+12 % de vitesse de frappe pendant 4 minutes.")
                            .Boost(StatType.AttackSpeed, 12f, 4f).Says("Une canette de boisson énergisante.", "La bleue ou la verte ? Peu importe, elles piquent pareil."),
                        new ShopItem("Barre protéinée", 4, ShopEffect.Buff, 0f, "+20 % d'endurance maximale pendant 5 minutes.").Food(10f)
                            .Boost(StatType.MaxStamina, 20f, 5f).Says("Une barre protéinée.", "Goût chocolat, la dernière."),
                        new ShopItem("Pansements et désinfectant", 12, ShopEffect.Heal, 45f, "+45 PV. Ça pique, mais ça tient.")
                            .Says("Des pansements et du désinfectant.", "Encore une bagarre ? Tiens, le grand format."),
                        new ShopItem("Courses de la semaine", 32, ShopEffect.Groceries, 5f, "Cinq repas pour le frigo de la planque (on mange chez soi, gratuitement).").Food(8f)
                            .Says("Je fais les courses de la semaine.", "Pâtes, œufs, conserves… Je te mets tout dans un sac."),
                        new ShopItem("Paquet de clopes", 11, ShopEffect.Buff, 0f, "Ça calme les nerfs : +8 de défense pendant 3 minutes.")
                            .Boost(StatType.Defense, 8f, 3f).Says("Un paquet de clopes.", "Les rouges, comme d'habitude.")
                    };

                case ShopKind.Cave:
                    return new[]
                    {
                        new ShopItem("Pack de bières", 9, ShopEffect.Buff, 0f, "Du courage en canette : +10 % de puissance pendant 4 minutes.")
                            .Boost(StatType.Strength, 10f, 4f).Says("Un pack de bières.", "Un pack de six, bien fraîches."),
                        new ShopItem("Flasque de whisky", 18, ShopEffect.Buff, 0f, "+18 % de puissance pendant 3 minutes. La tête tourne un peu.")
                            .Boost(StatType.Strength, 18f, 3f).Says("Une flasque de whisky.", "Un douze ans d'âge. Pas tout d'un coup, hein."),
                        new ShopItem("Bouteille d'eau", 2, ShopEffect.Heal, 10f, "+10 PV. Le choix raisonnable.")
                            .Says("Juste une bouteille d'eau.", "De l'eau ? Tu es sûr d'être au bon endroit ?"),
                        new ShopItem("Bouteille de champagne", 60, ShopEffect.Reputation, 3f, "Pour arroser une victoire : +3 réputation.").Daily()
                            .Says("Ta meilleure bouteille de champagne.", "On fête quelque chose ? Je te l'emballe.")
                    };

                case ShopKind.Restaurant:
                    if (n.Contains("taco")) return Tacos();
                    if (n.Contains("diner")) return Diner();
                    if (n.Contains("dragon")) return Dragon();
                    if (n.Contains("pizz")) return Pizzeria();
                    return new[]
                    {
                        new ShopItem("Plat du jour", 16, ShopEffect.Heal, 60f, "+60 PV. Un vrai repas, assis.").Food(55f)
                            .Says("Je vais prendre le plat du jour.", "Blanquette aujourd'hui. Vous allez vous régaler."),
                        new ShopItem("Steak-frites", 24, ShopEffect.Buff, 0f, "+25 % d'endurance maximale pendant 8 minutes.").Food(60f)
                            .Boost(StatType.MaxStamina, 25f, 8f).Says("Un steak-frites, saignant.", "Saignant, très bien."),
                        new ShopItem("Menu complet", 38, ShopEffect.HealFull, 0f, "Entrée, plat, dessert : vie et endurance au maximum.").Food(85f)
                            .Says("Le menu complet, s'il vous plaît.", "Entrée, plat, dessert. Prenez votre temps."),
                        new ShopItem("Café serré", 3, ShopEffect.Buff, 0f, "+8 % de vitesse de frappe pendant 3 minutes.")
                            .Boost(StatType.AttackSpeed, 8f, 3f).Says("Un café serré.", "Un serré, tout de suite.")
                    };

                case ShopKind.Cafe:
                    return new[]
                    {
                        new ShopItem("Double espresso", 3, ShopEffect.Buff, 0f, "+10 % de vitesse de frappe pendant 4 minutes.")
                            .Boost(StatType.AttackSpeed, 10f, 4f).Says("Un double espresso.", "Un double ! Ça va te réveiller."),
                        new ShopItem("Croissant", 2, ShopEffect.Heal, 12f, "+12 PV.").Food(12f)
                            .Says("Un croissant, s'il vous plaît.", "Il sort du four."),
                        new ShopItem("Petit-déjeuner complet", 14, ShopEffect.Heal, 45f, "+45 PV, et la journée commence mieux.").Food(40f)
                            .Says("Le petit-déjeuner complet.", "Œufs, tartines, jus d'orange et café. Installe-toi."),
                        new ShopItem("Journal du jour", 2, ShopEffect.Rumor, 0f, "Les faits divers d'Hyland : on y apprend des choses.")
                            .Says("Je prends le journal.", "Tiens. Page trois, il y a encore eu de la casse.")
                    };

                case ShopKind.Bar:
                    return new[]
                    {
                        new ShopItem("Demi pression", 5, ShopEffect.Buff, 0f, "+8 % de puissance pendant 4 minutes.")
                            .Boost(StatType.Strength, 8f, 4f).Says("Mets-moi un demi.", "Un demi, un !"),
                        new ShopItem("Shot de vodka", 7, ShopEffect.Buff, 0f, "+15 % de puissance pendant 2 minutes. Cul sec.")
                            .Boost(StatType.Strength, 15f, 2f).Says("Un shot de vodka.", "Cul sec, champion."),
                        new ShopItem("Cacahuètes", 2, ShopEffect.Heal, 6f, "+6 PV. Salées, pour donner soif.").Food(6f)
                            .Says("Et des cacahuètes.", "Un bol de cacahuètes."),
                        new ShopItem("Tournée générale", 80, ShopEffect.Reputation, 6f, "Tout le bar boit à ta santé : +6 réputation.").Daily()
                            .Says("Tournée générale, c'est pour moi !", "Tournée générale ! Tout le monde trinque à ta santé !"),
                        new ShopItem("Écouter les rumeurs", 0, ShopEffect.Rumor, 0f, "Le barman entend tout. Pour un pourboire, il parle.")
                            .Says("Alors, qu'est-ce qui se dit en ce moment ?", "Penche-toi, je vais te dire un truc.")
                    };

                case ShopKind.BoiteDeNuit:
                    return new[]
                    {
                        new ShopItem("Cocktail maison", 14, ShopEffect.Buff, 0f, "+12 % de puissance pendant 3 minutes.")
                            .Boost(StatType.Strength, 12f, 3f).Says("Un cocktail maison.", "Le Vertigo spécial. Attention, ça monte."),
                        new ShopItem("Carré VIP", 250, ShopEffect.Reputation, 12f, "Une bouteille, une table, tout le monde te voit : +12 réputation.").Daily()
                            .Says("Je prends un carré VIP.", "Carré VIP ! Je t'envoie la bouteille avec les cierges."),
                        new ShopItem("Danser", 0, ShopEffect.Experience, 5f, "Tu te défoules sur la piste : +5 XP (souplesse).")
                            .Says("Je vais aller danser un peu.", "Fais-toi plaisir, la piste est à toi."),
                        new ShopItem("Parler au videur", 0, ShopEffect.Rumor, 0f, "Il connaît tous les durs de la ville.")
                            .Says("Il est où, le videur ?", "Au fond. Il parle peu, mais il sait tout.")
                    };

                case ShopKind.Pharmacie:
                    return new[]
                    {
                        new ShopItem("Antidouleurs", 25, ShopEffect.Buff, 0f, "+12 de défense pendant 5 minutes : tu sens moins les coups.")
                            .Boost(StatType.Defense, 12f, 5f).Says("Une boîte d'antidouleurs.", "Pas plus de trois par jour, hein."),
                        new ShopItem("Trousse de soins", 45, ShopEffect.Medkit, 1f, "À ranger chez toi : on s'y soigne une blessure, et on se remet sur pied.")
                            .Says("Une trousse de soins complète.", "Tout ce qu'il faut. Le mode d'emploi est dedans."),
                        new ShopItem("Premiers secours", 30, ShopEffect.HealFull, 0f, "Sur place : vie et endurance au maximum.")
                            .Says("Vous pouvez me soigner, là, tout de suite ?", "Asseyez-vous. Je nettoie et je recouds."),
                        new ShopItem("Attelle et bandages", 120, ShopEffect.Injury, 1f, "Une blessure en moins (sans attendre une nuit).")
                            .Says("Il me faudrait une attelle.", "Montrez-moi ça… Oui. On va bien serrer."),
                        new ShopItem("Vitamines", 15, ShopEffect.Buff, 0f, "+15 % d'endurance maximale pendant 10 minutes.")
                            .Boost(StatType.MaxStamina, 15f, 10f).Says("Des vitamines.", "Une par jour, avec un grand verre d'eau.")
                    };

                case ShopKind.Medecin:
                    return new[]
                    {
                        new ShopItem("Consultation", 90, ShopEffect.HealFull, 0f, "Le médecin te recoud : vie et endurance au maximum.")
                            .Says("Je viens pour une consultation.", "Allongez-vous. Respirez… Bon, ce n'est pas joli."),
                        new ShopItem("Soigner une blessure", 150, ShopEffect.Injury, 1f, "Une blessure en moins.")
                            .Says("J'ai une blessure qui ne passe pas.", "Faites voir. On va arranger ça."),
                        new ShopItem("Bilan complet", 400, ShopEffect.Injury, 0f, "Toutes tes blessures soignées. Tu repars à zéro.")
                            .Says("Je voudrais un bilan complet.", "Radios, points de suture, le grand jeu. Vous repartirez neuf."),
                        new ShopItem("Certificat de bonne santé", 30, ShopEffect.Reputation, 1f, "Ça rassure les clients : +1 réputation.").Daily()
                            .Says("Il me faudrait un certificat.", "Apte à tout. Surtout à recevoir des coups.")
                    };

                case ShopKind.Barbier:
                    return new[]
                    {
                        new ShopItem("Discuter avec le barbier", 0, ShopEffect.Rumor, 0f, "Il coiffe tout le quartier, il sait tout.")
                            .Says("Alors, quoi de neuf dans le quartier ?", "Ah, si tu savais ce que j'entends dans ce fauteuil…")
                    };

                case ShopKind.Tatoueur:
                    return new[]
                    {
                        new ShopItem("Tatouage « ÜBER » sur l'avant-bras", 180, ShopEffect.Reputation, 10f, "Tout le monde sait pour qui tu cognes : +10 réputation.").Once()
                            .Says("Je veux « ÜBER » sur l'avant-bras.", "En lettres gothiques ? Assieds-toi, ça va piquer."),
                        new ShopItem("Tête de mort sur la main", 240, ShopEffect.Reputation, 12f, "Intimidant : +12 réputation.").Once()
                            .Says("Une tête de mort, sur la main.", "Sur la main, ça se voit. C'est le but."),
                        new ShopItem("Dragon dans le dos", 520, ShopEffect.Reputation, 20f, "Six heures d'aiguille : +20 réputation.").Once()
                            .Says("Un dragon, dans tout le dos.", "Six heures d'aiguille. Tu tiendras ?"),
                        new ShopItem("Piercing", 40, ShopEffect.Reputation, 3f, "+3 réputation.").Once()
                            .Says("Juste un piercing.", "Oreille ou arcade ? Ça, c'est vite fait.")
                    };

                case ShopKind.Quincaillerie:
                    return new[]
                    {
                        new ShopItem("Bandes de boxe", 30, ShopEffect.Buff, 0f, "Des mains bien bandées : +10 % de puissance pendant 10 minutes.")
                            .Boost(StatType.Strength, 10f, 10f).Says("Des bandes pour les mains.", "Coton renforcé, ça protège les jointures."),
                        new ShopItem("Protège-dents", 45, ShopEffect.Buff, 0f, "+10 de défense pendant 10 minutes.")
                            .Boost(StatType.Defense, 10f, 10f).Says("Un protège-dents.", "À faire chauffer et à mordre. Tu garderas tes dents."),
                        new ShopItem("Chaussures de sécurité", 60, ShopEffect.Buff, 0f, "+8 % de puissance de chute (coups de pied) pendant 10 minutes.")
                            .Boost(StatType.KnockdownPower, 8f, 10f).Says("Des chaussures de sécurité.", "Bout coqué. Pour le chantier, bien sûr."),
                        new ShopItem("Jeu de crochets (x3)", 24, ShopEffect.Lockpicks, 3f, "Pour les serrures récalcitrantes. On ne pose pas de questions.")
                            .Says("Un jeu de crochets.", "Pour tes clés perdues, j'imagine."),
                        new ShopItem("Ruban adhésif", 4, ShopEffect.Heal, 8f, "Une arcade qui saigne, on scotche : +8 PV.")
                            .Says("Un rouleau de ruban adhésif.", "Le gris, ça répare tout.")
                    };

                case ShopKind.MarcheNoir:
                    return new[]
                    {
                        new ShopItem("Crochets de pro (x8)", 60, ShopEffect.Lockpicks, 8f, "Acier trempé, huit pièces. Ils ouvrent tout ce qui roule.")
                            .Says("Il me faut des crochets. Des bons.", "Acier trempé. Tu ne les as pas eus ici."),
                        new ShopItem("Poing américain", 600, ShopEffect.Buff, 0f, "Dans la poche, pour les grands soirs : +30 % de puissance pendant 5 minutes.")
                            .Boost(StatType.Strength, 30f, 5f).Says("T'aurais un poing américain ?", "Sous le comptoir. Ne le sors pas devant les flics."),
                        new ShopItem("Stéroïdes", 180, ShopEffect.Buff, 0f, "+40 % d'endurance maximale pendant 6 minutes. À tes risques.")
                            .Boost(StatType.MaxStamina, 40f, 6f).Says("J'ai besoin d'un coup de fouet.", "Ça, ça te tiendra debout. Après, tu paieras."),
                        new ShopItem("Adrénaline", 220, ShopEffect.Buff, 0f, "+20 % de vitesse de frappe pendant 3 minutes.")
                            .Boost(StatType.AttackSpeed, 20f, 3f).Says("De l'adrénaline.", "Une piqûre, et tu vois les coups venir au ralenti."),
                        new ShopItem("Liste des durs du coin", 120, ShopEffect.Rumor, 0f, "Qui cogne, où, et pour qui.")
                            .Says("Qui cogne fort, en ce moment ?", "Pour ce prix-là, je te donne les noms.")
                    };

                case ShopKind.Arcade:
                    return new[]
                    {
                        new ShopItem("Borne de baston", 1, ShopEffect.Experience, 6f, "Tu étudies les enchaînements : +6 XP.")
                            .Says("Un jeton pour la borne de baston.", "Bonne chance pour le record."),
                        new ShopItem("Machine à frapper", 2, ShopEffect.Experience, 10f, "Tu cognes le punching-ball de la borne : +10 XP.").Daily()
                            .Says("Je vais essayer la machine à frapper.", "Vas-y, défoule-toi. Elle en a vu d'autres."),
                        new ShopItem("Flipper", 1, ShopEffect.Reputation, 0f, "Pour le plaisir. Tilt.")
                            .Says("Un jeton pour le flipper.", "Pas trop fort sur les côtés, il fait tilt.")
                    };

                case ShopKind.StandDeTir:
                    return new[]
                    {
                        new ShopItem("Série de tirs", 15, ShopEffect.Buff, 0f, "Le calme et la précision : +6 % de vitesse de frappe pendant 6 minutes.")
                            .Boost(StatType.AttackSpeed, 6f, 6f).Says("Une série de tirs.", "Couloir trois. Casque sur les oreilles."),
                        new ShopItem("Cours de concentration", 45, ShopEffect.Experience, 20f, "+20 XP.").Daily()
                            .Says("Je voudrais un cours.", "On respire, on vise, on presse. Pas on tire.")
                    };

                case ShopKind.SalleDeBoxe:
                    return new[]
                    {
                        new ShopItem("Séance au sac", 10, ShopEffect.Experience, 15f, "+15 XP.").Daily()
                            .Says("Une séance au sac.", "Le sac du fond est libre. Protège tes mains."),
                        new ShopItem("Sparring avec le coach", 40, ShopEffect.Experience, 40f, "+40 XP. Tu en ressors avec un bleu de plus.").Daily()
                            .Says("Je veux faire du sparring.", "Mets le casque. Je ne retiendrai pas mes coups."),
                        new ShopItem("Étirements et récupération", 12, ShopEffect.HealFull, 0f, "Vie et endurance au maximum.")
                            .Says("Je viens récupérer.", "Étirements, bain froid. Tu repars neuf.")
                    };

                case ShopKind.Laverie:
                    return new[]
                    {
                        new ShopItem("Lessive (60 degrés)", 6, ShopEffect.Reputation, 1f, "Plus une trace de sang : +1 réputation.").Daily()
                            .Says("Une machine à 60.", "Machine quatre. La lessive est dans le distributeur."),
                        new ShopItem("Attendre en lisant", 0, ShopEffect.Rumor, 0f, "Les gens parlent devant les machines.")
                            .Says("Je vais attendre un peu.", "Assieds-toi, il y a des magazines.")
                    };

                case ShopKind.Poste:
                    return new[]
                    {
                        new ShopItem("Retirer un colis", 0, ShopEffect.Parcel, 90f, "Des fans t'envoient des enveloppes (une fois par jour).").Daily()
                            .Says("Je viens retirer un colis.", "Une pièce d'identité ? … Voilà, c'est pour vous."),
                        new ShopItem("Envoyer de l'argent à ta mère", 100, ShopEffect.Reputation, 4f, "Elle sera fière de toi : +4 réputation.").Daily()
                            .Says("Un mandat pour ma mère.", "C'est gentil, ça. Elle le recevra demain.")
                    };

                case ShopKind.Avocat:
                    return new[]
                    {
                        new ShopItem("Nettoyer le casier", 0, ShopEffect.ClearRecord, 0f, "Maître Lenoir fait disparaître ton casier judiciaire : 300 € plus 120 € par délit. Il ne juge pas. Il facture.")
                            .Says("Il faudrait nettoyer mon casier.", "Voyons ce dossier… Rien d'impossible. Tout est une question de prix."),
                        new ShopItem("Faire classer une plainte", 250, ShopEffect.Reputation, 15f, "Les clients oublient : +15 réputation.").Daily()
                            .Says("Quelqu'un a porté plainte contre moi.", "Plus maintenant. Le dossier vient de s'égarer."),
                        new ShopItem("Consultation", 60, ShopEffect.Rumor, 0f, "Le droit, et ce qu'on en fait.")
                            .Says("J'aurais besoin d'un conseil.", "Le premier conseil est gratuit : ne parlez à personne. Le reste, non.")
                    };

                case ShopKind.PreteurSurGages:
                    return new[]
                    {
                        new ShopItem("Vendre ta montre", 0, ShopEffect.Sell, 120f, "Il en donne 120 €. Tu n'en avais pas besoin.").Once()
                            .Says("Combien pour cette montre ?", "Cent vingt. C'est ma dernière offre."),
                        new ShopItem("Vendre ta chaîne en or", 0, ShopEffect.Sell, 260f, "260 €. Elle venait de ton oncle.").Once()
                            .Says("Et pour la chaîne en or ?", "Deux cent soixante. Ton oncle comprendra."),
                        new ShopItem("Racheter une chaîne en or", 400, ShopEffect.Reputation, 8f, "Brillant : +8 réputation.").Once()
                            .Says("Je prends la chaîne en vitrine.", "Belle pièce. Elle brille sous les néons.")
                    };

                case ShopKind.Motel:
                    return new[]
                    {
                        new ShopItem("Café du distributeur", 1, ShopEffect.Heal, 5f, "+5 PV. Il a le goût du gobelet.")
                            .Says("Un café du distributeur.", "Tape dessus si ça coince."),
                        new ShopItem("Demander s'il y a du courrier", 0, ShopEffect.Rumor, 0f, "Le gérant voit passer tout le monde.")
                            .Says("Il y a du courrier pour moi ?", "Pas de courrier. Mais j'ai vu passer du monde…")
                    };

                case ShopKind.Commissariat:
                    return new[]
                    {
                        new ShopItem("Payer tes amendes", 120, ShopEffect.Reputation, 5f, "Casier plus léger : +5 réputation.").Daily()
                            .Says("Je viens payer mes amendes.", "Ça change. Signez là."),
                        new ShopItem("Consulter les avis de recherche", 0, ShopEffect.Rumor, 0f, "Les visages affichés au mur.")
                            .Says("Je peux voir les avis de recherche ?", "Au mur. Si vous en reconnaissez un, vous savez où nous trouver.")
                    };

                default:
                    return new ShopItem[0];
            }
        }

        private static ShopItem[] Tacos()
        {
            return new[]
            {
                new ShopItem("Tacos al pastor (x3)", 9, ShopEffect.Heal, 35f, "+35 PV. Porc mariné, ananas, coriandre.").Food(40f)
                    .Says("Trois tacos al pastor, s'il te plaît.", "Trois al pastor, ça marche !"),
                new ShopItem("Burrito géant", 13, ShopEffect.Heal, 55f, "+55 PV. Il pèse un kilo.").Food(60f)
                    .Says("Je vais prendre le burrito géant.", "Le géant ! T'as faim, toi."),
                new ShopItem("Nachos au fromage", 7, ShopEffect.Heal, 20f, "+20 PV. Fromage fondu et piments.").Food(25f)
                    .Says("Des nachos au fromage.", "Avec des jalapeños ? Je t'en mets quand même."),
                new ShopItem("Menu Tickler", 18, ShopEffect.HealFull, 0f, "Tacos, burrito, boisson : vie et endurance au maximum.").Food(80f)
                    .Says("Le menu Tickler, avec tout.", "Le grand jeu ! Ça arrive."),
                new ShopItem("Horchata", 4, ShopEffect.Buff, 0f, "+10 % d'endurance maximale pendant 4 minutes.")
                    .Boost(StatType.MaxStamina, 10f, 4f).Says("Une horchata bien fraîche.", "Une horchata, une !")
            };
        }

        private static ShopItem[] Diner()
        {
            return new[]
            {
                new ShopItem("Burger maison", 12, ShopEffect.Heal, 50f, "+50 PV. Steak haché, cheddar, oignons grillés.").Food(55f)
                    .Says("Je te prends un burger.", "Un burger, un ! Saignant ou à point ?"),
                new ShopItem("Double cheeseburger", 16, ShopEffect.Buff, 0f, "+20 % d'endurance maximale pendant 8 minutes.").Food(70f)
                    .Boost(StatType.MaxStamina, 20f, 8f).Says("Un double cheese, avec des frites.", "Double cheese, frites. Gros appétit !"),
                new ShopItem("Pancakes au sirop d'érable", 8, ShopEffect.Heal, 30f, "+30 PV. Trois étages, sirop à volonté.").Food(35f)
                    .Says("Des pancakes, avec plein de sirop.", "Pancakes, sirop à volonté !"),
                new ShopItem("Milkshake vanille", 5, ShopEffect.Buff, 0f, "+8 % de vitesse de frappe pendant 3 minutes.").Food(15f)
                    .Boost(StatType.AttackSpeed, 8f, 3f).Says("Un milkshake vanille.", "Un milkshake, ça roule."),
                new ShopItem("Café filtre", 2, ShopEffect.Buff, 0f, "+6 % de vitesse de frappe pendant 3 minutes.")
                    .Boost(StatType.AttackSpeed, 6f, 3f).Says("Un café, noir.", "Café noir. Je vous ressers quand vous voulez."),
                new ShopItem("Menu routier", 22, ShopEffect.HealFull, 0f, "Burger, frites, tarte aux pommes : vie et endurance au maximum.").Food(85f)
                    .Says("Le menu routier, s'il vous plaît.", "Le routier ! Vous ne repartirez pas le ventre vide.")
            };
        }

        private static ShopItem[] Dragon()
        {
            return new[]
            {
                new ShopItem("Nouilles sautées au bœuf", 11, ShopEffect.Heal, 45f, "+45 PV. Au wok, bien relevées.").Food(50f)
                    .Says("Des nouilles sautées au bœuf.", "Nouilles bœuf ! Piquant ?"),
                new ShopItem("Canard laqué", 19, ShopEffect.Buff, 0f, "+25 % d'endurance maximale pendant 8 minutes.").Food(60f)
                    .Boost(StatType.MaxStamina, 25f, 8f).Says("Le canard laqué.", "Très bon choix. Le chef le prépare depuis ce matin."),
                new ShopItem("Nems (x4)", 6, ShopEffect.Heal, 20f, "+20 PV. Avec la salade et la menthe.").Food(20f)
                    .Says("Quatre nems.", "Quatre nems, avec la sauce."),
                new ShopItem("Menu dragon", 26, ShopEffect.HealFull, 0f, "Soupe, plat, nems, thé : vie et endurance au maximum.").Food(85f)
                    .Says("Le menu dragon, pour moi.", "Le menu dragon ! Installez-vous."),
                new ShopItem("Thé au jasmin", 3, ShopEffect.Buff, 0f, "+6 de défense pendant 4 minutes. Ça apaise.")
                    .Boost(StatType.Defense, 6f, 4f).Says("Un thé au jasmin.", "Bien chaud. Ça apaise l'esprit.")
            };
        }

        private static ShopItem[] Pizzeria()
        {
            return new[]
            {
                new ShopItem("Pizza margherita", 11, ShopEffect.Heal, 50f, "+50 PV. Tomate, mozzarella, basilic.").Food(55f)
                    .Says("Une margherita.", "Une margherita, au feu de bois !"),
                new ShopItem("Calzone", 13, ShopEffect.Buff, 0f, "+20 % d'endurance maximale pendant 8 minutes.").Food(65f)
                    .Boost(StatType.MaxStamina, 20f, 8f).Says("Une calzone.", "La calzone, elle est énorme, attention."),
                new ShopItem("Part de pizza", 4, ShopEffect.Heal, 20f, "+20 PV. À emporter.").Food(25f)
                    .Says("Juste une part, à emporter.", "Une part, chaude, tiens."),
                new ShopItem("Tiramisu", 6, ShopEffect.Heal, 18f, "+18 PV. Le vrai, celui de la nonna.").Food(15f)
                    .Says("Et un tiramisu.", "Celui de ma mère. Tu m'en diras des nouvelles."),
                new ShopItem("Menu Mamma", 21, ShopEffect.HealFull, 0f, "Pizza, boisson, dessert : vie et endurance au maximum.").Food(85f)
                    .Says("Le menu Mamma.", "Pizza, boisson, dessert. Mangia !")
            };
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
