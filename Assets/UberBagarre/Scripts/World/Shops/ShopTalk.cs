using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Ce qui se dit au comptoir : l'accueil (selon l'heure et le commerce), la commande du
    /// joueur, la réponse avec le prix, le refus quand on n'a pas de quoi payer, l'au revoir,
    /// l'annonce de la fermeture.
    ///
    /// Chaque réplique a plusieurs versions : un vendeur qui répète mot pour mot la même phrase
    /// à chaque passage sonne comme une machine.
    /// </summary>
    public static class ShopTalk
    {
        public const string Player = "TOI";

        private static string Pick(string[] lines, int seed)
        {
            if (lines == null || lines.Length == 0) return string.Empty;
            return lines[((seed % lines.Length) + lines.Length) % lines.Length];
        }

        private static bool Night(float hour)
        {
            return hour >= 18.5f || hour < 5f;
        }

        /// <summary>Le nom affiché au-dessus des répliques du comptoir (« TONY », « LE BARMAN »…).</summary>
        public static string Speaker(ShopKind kind, string shopName)
        {
            if (!string.IsNullOrEmpty(shopName))
            {
                if (shopName.StartsWith("Chez "))
                {
                    string rest = shopName.Substring(5);
                    int comma = rest.IndexOf(',');
                    if (comma > 0) rest = rest.Substring(0, comma);
                    return rest.Trim().ToUpperInvariant();
                }

                if (shopName.Contains("Lenoir")) return "MAÎTRE LENOIR";
                if (shopName.StartsWith("Jeff")) return "JEFF";
                if (shopName.Contains("Mamma")) return "MAMMA";
            }

            switch (kind)
            {
                case ShopKind.Epicerie: return "LE CAISSIER";
                case ShopKind.Cave: return "LE CAVISTE";
                case ShopKind.Restaurant: return "LE SERVEUR";
                case ShopKind.Cafe: return "LA SERVEUSE";
                case ShopKind.Bar:
                case ShopKind.BoiteDeNuit: return "LE BARMAN";
                case ShopKind.Pharmacie: return "LA PHARMACIENNE";
                case ShopKind.Medecin: return "LE DOCTEUR";
                case ShopKind.Vetements: return "LA VENDEUSE";
                case ShopKind.Barbier: return "LE BARBIER";
                case ShopKind.Tatoueur: return "LE TATOUEUR";
                case ShopKind.MarcheNoir: return "L'HOMME DU HANGAR";
                case ShopKind.Casino: return "LE CROUPIER";
                case ShopKind.Arcade: return "LE GÉRANT";
                case ShopKind.StandDeTir: return "LE MONITEUR";
                case ShopKind.SalleDeBoxe: return "LE COACH";
                case ShopKind.Laverie: return "LA GÉRANTE";
                case ShopKind.Poste: return "LE GUICHETIER";
                case ShopKind.Immobilier: return "L'AGENT IMMOBILIER";
                case ShopKind.Concession: return "LE VENDEUR";
                case ShopKind.Avocat: return "L'AVOCAT";
                case ShopKind.PreteurSurGages: return "LE PRÊTEUR";
                case ShopKind.Motel: return "LE GÉRANT";
                case ShopKind.Commissariat: return "L'AGENT";
                default: return "LE VENDEUR";
            }
        }

        // ------------------------------------------------------------------ accueil

        public static string Greeting(ShopKind kind, float hour, int seed)
        {
            bool night = Night(hour);
            string hello = night ? "Bonsoir." : hour < 11f ? "Bonjour !" : "Salut.";
            switch (kind)
            {
                case ShopKind.Epicerie:
                    return Pick(night
                        ? new[] { "Bonsoir. Prends ce qu'il te faut, je ferme les yeux sur l'heure.", "Encore toi ? Fais vite, je suis crevé.", "Bonsoir. Les sandwichs du soir sont à moitié prix… non, je plaisante." }
                        : new[] { hello + " Prends ce qu'il te faut.", hello + " Les sandwichs sont frais de ce matin.", "Salut ! Il y a une promo sur les barres protéinées." }, seed);
                case ShopKind.Cave:
                    return Pick(new[] { "Pièce d'identité ? … Laisse tomber, t'as la tête d'un majeur.", hello + " On a reçu un whisky qui arrache.", "Tu cherches quelque chose de fort ?" }, seed);
                case ShopKind.Restaurant:
                    return Pick(night
                        ? new[] { "Bonsoir ! Une table pour un ? Je vous apporte la carte.", "Bonsoir, la cuisine tourne encore. Qu'est-ce qui vous ferait plaisir ?", "Installez-vous. Ce soir, le chef est en forme." }
                        : new[] { hello + " Une table pour un ? Installez-vous.", hello + " Qu'est-ce que je vous sers ?", "Le plat du jour est excellent, je dis ça comme ça." }, seed);
                case ShopKind.Cafe:
                    return Pick(new[] { "Un café ? T'as une tête à avoir besoin d'un double.", hello + " Qu'est-ce que je te sers ?", "Ça sent le croissant chaud, hein ?" }, seed);
                case ShopKind.Bar:
                    return Pick(new[] { "Qu'est-ce que je te sers ? Et pas de bagarre ici, hein.", "Salut, champion. La même chose que d'habitude ?", "Pose-toi au comptoir. Qu'est-ce que tu bois ?" }, seed);
                case ShopKind.BoiteDeNuit:
                    return Pick(new[] { "Qu'est-ce que je te sers ? Faut crier, avec la musique !", "Le bar, c'est ici. Tu bois quoi ?", "Salut ! Ce soir, cocktail maison à l'honneur." }, seed);
                case ShopKind.Pharmacie:
                    return Pick(new[] { hello + " Vous saignez sur mon comptoir, là.", "Bonjour. Encore une mauvaise chute ?", "Qu'est-ce qu'il vous faut ? Ça a l'air de faire mal." }, seed);
                case ShopKind.Medecin:
                    return Pick(new[] { "Asseyez-vous. Encore une chute dans l'escalier, je suppose ?", "Bonjour. Qu'est-ce qui vous amène ? … À part l'évidence.", "Entrez. On va regarder ça." }, seed);
                case ShopKind.Vetements:
                    return Pick(new[] { "Bienvenue ! Tout est en rayon, les cabines sont au fond.", hello + " Je peux vous aider à trouver quelque chose ?", "Nouvelle collection ! Ça vous irait bien, ça." }, seed);
                case ShopKind.Barbier:
                    return Pick(new[] { "Assieds-toi. Je te fais quoi, propre ou méchant ?", "Installe-toi dans le fauteuil. On rafraîchit ou on change tout ?", hello + " Viens là, je vois déjà ce qu'il te faut." }, seed);
                case ShopKind.Tatoueur:
                    return Pick(new[] { "Ça pique, je te préviens. Tu veux quoi, et où ?", "Salut. T'as une idée, ou tu regardes les catalogues ?", "Entre. Tout est stérilisé, promis." }, seed);
                case ShopKind.Quincaillerie:
                    return Pick(new[] { "Des bandes, du scotch, un protège-dents ? J'ai tout ça.", hello + " Vous cherchez quoi ?", "Bricolage ou… autre chose ? Je ne pose pas de questions." }, seed);
                case ShopKind.MarcheNoir:
                    return Pick(new[] { "Tu ne m'as jamais vu. Qu'est-ce que tu cherches ?", "Parle bas. Qu'est-ce qu'il te faut ?", "T'as été suivi ? … Bon. Je t'écoute." }, seed);
                case ShopKind.Casino:
                    return Pick(new[] { "Bienvenue au casino. Les jetons sont à la caisse, la chance est gratuite.", "Bonsoir monsieur. Machines ou roulette ?", "Faites vos jeux !" }, seed);
                case ShopKind.Arcade:
                    return Pick(new[] { "Une partie ? Le record de la borne de baston tient depuis 1998.", "Salut ! Les jetons, c'est ici.", "T'es venu battre le record ?" }, seed);
                case ShopKind.StandDeTir:
                    return Pick(new[] { "Casque, lunettes, et on vise la cible, pas le voisin.", hello + " Une série ? Les couloirs sont libres.", "Bienvenue au stand. Règle numéro un : le canon vers les cibles." }, seed);
                case ShopKind.SalleDeBoxe:
                    return Pick(new[] { "Tu veux apprendre à cogner proprement ? Le coach est là.", "Salut ! Gants, bandes, et au boulot.", "T'as l'air d'en vouloir. Ça tombe bien." }, seed);
                case ShopKind.Laverie:
                    return Pick(new[] { "Les machines sont libres. Le sang, c'est à 60 degrés.", "Salut. La quatre marche mieux que les autres.", "Bonjour. La lessive est dans le distributeur." }, seed);
                case ShopKind.Poste:
                    return Pick(new[] { "Bonjour. Vous venez chercher un colis ?", "Au suivant ! … Bonjour, c'est pour quoi ?", "Bonjour. Un envoi ou un retrait ?" }, seed);
                case ShopKind.Immobilier:
                    return Pick(new[] { "Vous cherchez à acheter ? Nous avons de très belles affaires.", "Bonjour ! Asseyez-vous, je vous montre nos biens.", "Bienvenue chez Hyland Immobilier. Que puis-je pour vous ?" }, seed);
                case ShopKind.Concession:
                    return Pick(new[] { "Elles sont belles, hein ? Toutes révisées. Presque.", "Bonjour ! Vous cherchez une voiture ? Vous êtes au bon endroit.", "Faites le tour, et dites-moi laquelle vous fait de l'œil." }, seed);
                case ShopKind.Avocat:
                    return Pick(new[] { "Maître Lenoir. Racontez-moi tout, je ne juge pas. Je facture.", "Asseyez-vous. Qu'avez-vous encore fait ?", "Bonjour. Tout ce que vous direz restera entre nous. Moyennant honoraires." }, seed);
                case ShopKind.PreteurSurGages:
                    return Pick(new[] { "Je rachète, je revends. Pas de questions.", "T'as quelque chose à vendre ?", "Regarde la vitrine. Ou montre-moi ce que t'as." }, seed);
                case ShopKind.Motel:
                    return Pick(new[] { "Motel Hyland, " + (night ? "bonsoir" : "bonjour") + ". Chambre, loyer, ou tu te perds ?", "Salut voisin. Un souci avec la chambre ?", "Oui ? Je regardais le match." }, seed);
                default:
                    return Pick(new[] { "Commissariat. C'est pour une plainte ou pour payer ?", "Bonjour. Vous avez un ticket ?", "Asseyez-vous, on va prendre votre déposition." }, seed);
            }
        }

        /// <summary>Une relance courte, quand on vient déjà de se saluer.</summary>
        public static string Prompt(ShopKind kind, int seed)
        {
            switch (kind)
            {
                case ShopKind.Bar:
                case ShopKind.BoiteDeNuit:
                    return Pick(new[] { "Qu'est-ce que je te sers ?", "Je t'écoute.", "Alors, ce sera quoi ?" }, seed);
                case ShopKind.Barbier:
                    return Pick(new[] { "Installe-toi. On fait quoi ?", "Alors, qu'est-ce que je te fais ?" }, seed);
                case ShopKind.Medecin:
                case ShopKind.Avocat:
                case ShopKind.Immobilier:
                    return Pick(new[] { "Je vous écoute.", "Que puis-je faire pour vous ?" }, seed);
                default:
                    return Pick(new[] { "Je t'écoute.", "Qu'est-ce qu'il te faut ?", "Oui ?", "Dis-moi." }, seed);
            }
        }

        // ------------------------------------------------------------------ commande

        /// <summary>Ce que dit le joueur pour commander un article.</summary>
        public static string Order(ShopItem item, int seed)
        {
            if (!string.IsNullOrEmpty(item.order)) return item.order;
            return Pick(new[] { "Je vais prendre ça : " + item.name.ToLowerInvariant() + ".", item.name + ", s'il vous plaît." }, seed);
        }

        /// <summary>La réponse du comptoir, avec le prix.</summary>
        public static string Reply(ShopItem item, int price, int seed)
        {
            string line = !string.IsNullOrEmpty(item.reply) ? item.reply : Pick(new[] { "Très bien.", "Ça marche.", "Tout de suite." }, seed);
            return price > 0 ? line + " " + Price(price, seed) : line;
        }

        public static string Price(int price, int seed)
        {
            return Pick(new[] { "Ça fera " + price + " €.", price + " €, s'il te plaît.", "Ça te fait " + price + " €.", price + " euros." }, seed);
        }

        /// <summary>Le joueur n'a pas de quoi payer.</summary>
        public static string Broke(ShopKind kind, int missing, int seed)
        {
            switch (kind)
            {
                case ShopKind.MarcheNoir:
                    return Pick(new[] { "Reviens quand t'auras l'argent. Et ne reviens pas les mains vides.", "Ici, on ne fait pas crédit." }, seed);
                case ShopKind.Bar:
                case ShopKind.BoiteDeNuit:
                    return Pick(new[] { "Il te manque " + missing + " €, mon grand. On ne fait pas d'ardoise.", "Ta carte ne passe pas. Essaie le liquide ?" }, seed);
                case ShopKind.Medecin:
                case ShopKind.Avocat:
                    return Pick(new[] { "Je crains que votre compte ne suive pas. Il manque " + missing + " €.", "Revenez avec les fonds, nous verrons." }, seed);
                default:
                    return Pick(new[] { "Il te manque " + missing + " €.", "Désolé, ça ne passe pas. Il manque " + missing + " €.", "Pas assez, mon ami. Reviens plus tard." }, seed);
            }
        }

        /// <summary>Ce que dit le joueur en partant.</summary>
        public static string PlayerBye(bool bought, int seed)
        {
            return bought
                ? Pick(new[] { "Merci, à plus.", "Merci. Bonne journée.", "Parfait, merci.", "Ciao." }, seed)
                : Pick(new[] { "Je vais réfléchir.", "Rien pour moi aujourd'hui.", "Une autre fois.", "Je repasserai." }, seed);
        }

        /// <summary>L'au revoir du comptoir.</summary>
        public static string Goodbye(ShopKind kind, float hour, bool bought, int seed)
        {
            switch (kind)
            {
                case ShopKind.Bar:
                case ShopKind.BoiteDeNuit:
                    return Pick(new[] { "Rentre bien.", "À la prochaine, champion.", "Et pas de bêtises dehors !" }, seed);
                case ShopKind.Medecin:
                case ShopKind.Pharmacie:
                    return Pick(new[] { "Ménagez-vous.", "Et évitez les escaliers.", "Revenez si ça ne passe pas." }, seed);
                case ShopKind.MarcheNoir:
                    return Pick(new[] { "Tu ne m'as jamais vu.", "Sors par derrière.", "Et pas un mot." }, seed);
                case ShopKind.Restaurant:
                case ShopKind.Cafe:
                    return bought ? Pick(new[] { "Bon appétit !", "Régalez-vous !", "Bonne dégustation !" }, seed)
                                  : Pick(new[] { "À une prochaine fois !", "Revenez quand vous aurez faim." }, seed);
                default:
                    return bought
                        ? Pick(new[] { Night(hour) ? "Bonne soirée !" : "Bonne journée !", "Merci, à la prochaine !", "Reviens quand tu veux." }, seed)
                        : Pick(new[] { "Pas de souci. À plus.", "Reviens quand tu veux.", "Comme tu veux." }, seed);
            }
        }

        /// <summary>L'annonce de la fermeture, quand on est encore dedans.</summary>
        public static string Closing(ShopKind kind, int seed)
        {
            return Pick(new[] { "On ferme ! Je vais te demander de sortir.", "C'est l'heure, on ferme. Allez, dehors.", "On baisse le rideau. À demain !" }, seed);
        }

        /// <summary>Ce qu'on lit en secouant la porte d'un commerce fermé.</summary>
        public static string ClosedDoor(string name, string note)
        {
            return name + " : " + (string.IsNullOrEmpty(note) ? "fermé." : note + ".");
        }

        // ------------------------------------------------------------------ barbier

        public static string AskCut(View.HairCut cut, int seed)
        {
            switch (cut)
            {
                case View.HairCut.Rase: return Pick(new[] { "Rase-moi tout.", "Boule à zéro… non, encore plus court. Tout." }, seed);
                case View.HairCut.Boule: return "Boule à zéro, trois millimètres.";
                case View.HairCut.Courte: return Pick(new[] { "Court sur les côtés, un peu dessus.", "Une coupe courte, propre." }, seed);
                case View.HairCut.Degrade: return Pick(new[] { "Fais-moi un dégradé.", "Un dégradé, bien net sur les côtés." }, seed);
                case View.HairCut.Brosse: return "Une brosse. Bien plate dessus.";
                case View.HairCut.Banane: return "Une banane, gominée.";
                case View.HairCut.Plaque: return "Plaqué en arrière, avec du gel.";
                case View.HairCut.Iroquoise: return "Une iroquoise. Je veux qu'on me remarque.";
                case View.HairCut.Afro: return "Taille-moi un afro, bien rond.";
                case View.HairCut.MiLong: return "Laisse-les longs, juste un peu de forme.";
                default: return "Un chignon, attaché derrière.";
            }
        }

        public static string BarberDone(int seed)
        {
            return Pick(new[] { "Et voilà ! Regarde-moi cette tête.", "Terminé. T'as l'air d'un champion.", "Voilà le travail. Tu vas faire des jaloux.", "Propre. Les filles du Vertigo vont te remarquer." }, seed);
        }
    }
}
