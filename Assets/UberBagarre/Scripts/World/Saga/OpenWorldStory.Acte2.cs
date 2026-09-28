using System.Collections;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// ACTE 2 — « La Fosse » (jours 10 à 16). Sami t'emmène au Vertigo, Rosa Delmas te regarde
    /// combattre en test ; la Ligue (le Facteur, Mama Olga, le Dentiste, Sven), un combat par
    /// soir ; ta ceinture chez le prêteur sur gages, vendue par un certain « S. B. » ; la salle
    /// de Ray menacée par le parking du maire (le tournoi, en histoire secondaire) ; le Taureau
    /// pour le titre, le vendredi, et ce qu'il avoue au sol ; les archives de Hyland Info et
    /// SARK Holding ; puis Rosa : « Sarkis veut te voir. »
    /// </summary>
    public partial class OpenWorldStory
    {
        private static readonly string[] LeagueRoles = { "facteur", "olga", "dentiste", "sven" };
        private static readonly string[] LeagueNames = { "LE FACTEUR", "MAMA OLGA", "LE DENTISTE", "SVEN" };
        private static readonly int[] LeagueRanks = { 10, 7, 4, 2 };

        private IEnumerator Acte2Line()
        {
            // --- 1. l'invitation
            if (!Done("a2:invitation"))
            {
                yield return new WaitForSeconds(6f);
                yield return Call("SAMI",
                    L("SAMI", "Léo ! Milan Kovac, à quatre contre un ? Tout le monde en parle. Même Rosa."),
                    L("MOI", "Rosa ?"),
                    L("SAMI", "Rosa Delmas. Le Vertigo, c'est à elle. Et ce qu'il y a derrière la porte du fond aussi : la Fosse."),
                    L("SAMI", "Pas d'appli, pas de photo. On se bat, les gens parient. Et ceux qui parient, c'est ceux qui ont l'argent de cette ville."),
                    L("SAMI", "Ce soir, je t'emmène. Elle veut te voir cogner."));
                Set("a2:invitation");
            }

            // --- 2. le combat de test
            if (!Done("a2:test"))
            {
                yield return UntilNight("Ce soir, au Vertigo : Sami t'y attend.");
                Put("ROSA", "rosa");
                Put("SAMI", "rosa", new Vector3(1.8f, 0f, -0.8f), -30f);
                yield return BackRoom("Le Vertigo : descends, et passe la porte du fond.");
                yield return Lines(L("SAMI", "Là. La dame en rouge. C'est Rosa. Sois poli, pour une fois."));

                yield return TalkTo("ROSA", "rosa", "Parle à Rosa Delmas.");
                yield return Lines(
                    L("ROSA", "Alors c'est toi, le boxeur radié. Marchal."),
                    L("ROSA", "Ici, pas d'appli, pas de photo. On se bat, les gens parient, je prends ma part. C'est propre."),
                    L("ROSA", "J'ai un code, moi : pas d'armes, pas de coups au sol quand l'autre a tapé, et on ne triche pas chez moi."),
                    L("ROSA", "Montre-moi. Un combat de test. Si tu tiens debout à la fin, on parlera."));

                OpenWorldDirector.StoryContract test = Job("recrue", "fosse:test", "fosse");
                test.Profile = Profile("recrue", "LA RECRUE");
                test.Reward = 200;
                test.Experience = 260;
                test.Lines = new[]
                {
                    L("LA RECRUE", "C'est lui ? Il est vieux."),
                    L("MOI", "Vingt-sept ans."),
                    L("LA RECRUE", "C'est ce que je dis.")
                };
                yield return Pit(test);

                yield return Lines(
                    L("ROSA", "Pas mal. Pas mal du tout."),
                    L("ROSA", "La Ligue : dix combattants classés, du dixième au premier. Tu montes en battant ceux du dessus."),
                    L("ROSA", "Un combat par soir. Et le titre, contre le Taureau, se joue le vendredi."),
                    L("SAMI", "(à voix basse) Tu vois ? Je t'avais dit. C'est ta place, ici."));
                Free("SAMI");
                Free("ROSA", false);
                Sms("ROSA", "La Fosse ouvre à 22 h. Un combat par soir. — R.");
                SetCount("fosse:jour", Day);
                Set("a2:test");
            }

            // --- 3. la Ligue, un combat par soir (la ceinture et la salle de Ray s'invitent entre deux)
            while (Count("ligue") < LeagueRoles.Length)
            {
                int rank = Count("ligue");
                if (rank >= 1 && !Done("a2:ceinture")) yield return Belt();
                if (rank >= 2 && !Done("a2:ray")) yield return RayThreat();

                int last = Count("fosse:jour");
                if (Day <= last) yield return UntilNextDay(last, "La Fosse : un combat par soir. Dors, et reviens demain soir.");
                yield return UntilNight("La Ligue : " + LeagueNames[rank] + " (" + LeagueRanks[rank] + "e), ce soir à la Fosse.");
                Put("ROSA", "rosa");
                yield return BackRoom("La Ligue : " + LeagueNames[rank] + " t'attend dans la Fosse du Vertigo.");
                yield return Lines(LeagueIntro(rank));

                OpenWorldDirector.StoryContract bout = Job(LeagueRoles[rank], "ligue:" + rank, "fosse");
                bout.Reward = 300 + rank * 150;
                bout.Experience = 280 + rank * 90;
                bout.Tune = LeagueStyle(rank);
                bout.Lines = LeagueLines(rank);
                yield return Pit(bout);

                SetCount("ligue", rank + 1);
                SetCount("fosse:jour", Day);
                if (rank == 2) Set("dentiste:rancune");
                yield return Lines(LeagueAfter(rank));
                Free("ROSA", false);
            }

            if (!Done("a2:ceinture")) yield return Belt();
            if (!Done("a2:ray")) yield return RayThreat();

            // --- 4. le Taureau, pour le titre
            if (!Done("a2:taureau")) yield return TitleFight();

            // --- 5. les archives : SARK Holding
            if (!Done("a2:archives"))
            {
                yield return Lines(L("MOI", "« Comme le petit Marchal. » Faut que je relise ce qu'ils ont écrit sur moi, à l'époque."));
                Set("news:archive");
                PublishArticle("archive", true, false);
                Goal("L'ordinateur (au motel) : Hyland Info, les archives de ton combat.");
                while (!ArticleRead("archive")) yield return null;
                Goal(string.Empty);
                yield return new WaitForSeconds(1f);
                yield return Lines(
                    L("MOI", "« Une large part des gains versée par une société écran. SARK Holding. »"),
                    L("MOI", "Domiciliée au casino."),
                    L("MOI", "Six cibles sur huit devaient de l'argent au casino. Et l'argent de mon combat est passé par le casino."));
                Set("a2:archives");
            }

            // --- 6. Rosa convoque
            if (!Done("a2:fin"))
            {
                yield return new WaitForSeconds(15f);
                yield return Call("ROSA",
                    L("ROSA", "Champion. Tu as de la visite demain soir. Enfin, c'est toi qui es de visite."),
                    L("ROSA", "Sarkis veut te voir."),
                    L("MOI", "Qui ?"),
                    L("ROSA", "Victor Sarkis. Le casino. On l'appelle le Comptable : il sait ce que tout le monde doit à tout le monde."),
                    L("ROSA", "Personne ne dit non à Sarkis. Moi non plus. Fais attention à toi, Marchal."));
                Set("a2:fin");
            }

            Advance(Acte3);
        }

        // ------------------------------------------------------------------ la Ligue

        private static DialogueLine[] LeagueIntro(int rank)
        {
            switch (rank)
            {
                case 0:
                    return new[] { L("ROSA", "Le Facteur. Dixième. Il frappe vite, il livre toujours. Ne le laisse pas prendre le rythme.") };
                case 1:
                    return new[] { L("ROSA", "Mama Olga. Septième. Ancienne lutteuse, deux fois championne. Si elle t'attrape, elle t'étrangle.") };
                case 2:
                    return new[]
                    {
                        L("ROSA", "Le Dentiste. Quatrième. Il a un bandage bizarre sur la main droite."),
                        L("ROSA", "Chez moi, on ne triche pas. Si tu le prends la main dans le sac… disons que je fermerai les yeux.")
                    };
                default:
                    return new[] { L("ROSA", "Sven. Deuxième. Un colosse. Lent. Très lent. Et très, très lourd.") };
            }
        }

        private static DialogueLine[] LeagueLines(int rank)
        {
            switch (rank)
            {
                case 0:
                    return new[]
                    {
                        L("LE FACTEUR", "Recommandé avec accusé de réception, Marchal. Signe ici."),
                        L("MOI", "Je signe avec quoi ?"),
                        L("LE FACTEUR", "Avec tes dents.")
                    };
                case 1:
                    return new[]
                    {
                        L("MAMA OLGA", "Viens voir Mama, petit. Mama va te faire un câlin."),
                        L("MOI", "Non merci."),
                        L("MAMA OLGA", "Personne ne dit non à un câlin de Mama.")
                    };
                case 2:
                    return new[]
                    {
                        L("LE DENTISTE", "Ouvre grand. Ça ne fera mal qu'un peu."),
                        L("MOI", "C'est quoi, sous ton bandage ?"),
                        L("LE DENTISTE", "Mon instrument de travail.")
                    };
                default:
                    return new[]
                    {
                        L("SVEN", "…"),
                        L("MOI", "Il parle ?"),
                        L("SVEN", "Non.")
                    };
            }
        }

        private static DialogueLine[] LeagueAfter(int rank)
        {
            switch (rank)
            {
                case 0: return new[] { L("ROSA", "Neuvième, huitième… disons que tu as sauté quelques cases. On passe aux choses sérieuses.") };
                case 1: return new[] { L("MOI", "J'ai encore ses bras autour du cou."), L("ROSA", "Tout le monde dit ça, après Olga.") };
                case 2:
                    return new[]
                    {
                        L("ROSA", "Un poing américain. Dans ma Fosse. Il ne remettra jamais les pieds ici."),
                        L("MOI", "Il avait l'air de m'en vouloir, en partant."),
                        L("ROSA", "Il en veut au monde entier. Garde un œil derrière toi.")
                    };
                default:
                    return new[]
                    {
                        L("ROSA", "Sven au tapis. Personne n'avait vu ça depuis deux ans."),
                        L("ROSA", "Il ne reste que lui. Le Taureau. Vendredi.")
                    };
            }
        }

        private System.Action<Combat.Combatant> LeagueStyle(int rank)
        {
            switch (rank)
            {
                case 0: return Style(-0.1f, -0.1f, 0.4f, 0.25f, 0f, 0.7f);
                case 1: return Style(0.5f, 0.2f, -0.1f, -0.1f, 4f, 0.7f);
                case 2: return Style(0.2f, 0.6f, 0.05f, 0f, 2f, 0.8f);
                default: return Style(0.8f, 0.5f, -0.35f, -0.2f, 6f, 0.75f);
            }
        }

        // ------------------------------------------------------------------ la ceinture, la salle de Ray

        private IEnumerator Belt()
        {
            yield return Calm();
            yield return Texts("JEFF",
                "yo. ta ceinture de champion",
                "elle est dans la vitrine du prêteur sur gages. g une photo",
                "t'es sûr que tu l'as perdue pendant ton déménagement ?");

            yield return Reach(Pick("pawn", "pawn_ext"), 5f, "Le prêteur sur gages : ta ceinture est en vitrine.");
            yield return Calm();
            yield return Lines(
                L("MOI", "Ma ceinture. Champion amateur, mi-lourds. Mon nom gravé dessus."),
                L("LE VENDEUR", "Belle pièce, hein ? Deux mille euros. Prix d'ami."),
                L("MOI", "Qui vous l'a vendue ?"),
                L("LE VENDEUR", "Oh, ça remonte… trois ans. Un jeune, pressé. Il a signé « S. B. » sur le registre."),
                L("LE VENDEUR", "Il en voulait pas cher. Il avait l'air d'avoir besoin d'argent vite."),
                L("MOI", "S. B.…"));
            Set("ceinture:vue");
            Set("a2:ceinture");
            yield return OfferBelt();
        }

        private IEnumerator OfferBelt()
        {
            yield return Choose("LE VENDEUR", "« Alors ? Deux mille, et elle rentre à la maison. »", "La racheter (2 000 €).", "Plus tard.");
            if (_answer != 0)
            {
                yield return Lines(L("LE VENDEUR", "Je vous la garde. Un peu."));
                yield break;
            }

            if (_progress.Spend(2000, "Ceinture (prêteur sur gages)"))
            {
                Set("ceinture:rachetee");
                ChangeCode(2, "Ta ceinture");
                yield return Lines(L("MOI", "Elle est plus lourde que dans mes souvenirs."));
            }
            else
            {
                yield return Lines(L("LE VENDEUR", "Revenez avec l'argent, champion. Deux mille. Pas un centime de moins."));
            }
        }

        private IEnumerator RayThreat()
        {
            yield return Calm();
            News("parking");
            yield return new WaitForSeconds(12f);
            yield return Call("RAY",
                L("RAY", "T'as vu les infos ? Holt veut raser ma salle. Un parking."),
                L("RAY", "Quarante ans de boxe. Des gamins qui seraient en prison sans ces quatre murs. Remplacés par des voitures."),
                L("RAY", "J'organise un tournoi. Entrée payante, les gens du quartier, les anciens. De quoi payer un avocat."));
            yield return Choose("COACH RAY", "« T'en es ? Trois combats à la salle. Sans appli, sans photo. Proprement. »",
                "J'en suis, coach.", "J'ai pas le temps, coach.");

            if (_answer == 0)
            {
                Set("tournoi:oui");
                SetCount("tournoi:jour", Day);
                ChangeCode(3, "Le tournoi de Ray");
                yield return Lines(L("RAY", "Je savais. Premier combat demain soir, à la salle. À partir de 19 h."));
            }
            else
            {
                yield return Lines(L("RAY", "… D'accord. T'as tes affaires. Si tu changes d'avis, tu sais où me trouver."));
            }

            Set("a2:ray");
        }

        // ------------------------------------------------------------------ le titre

        private IEnumerator TitleFight()
        {
            // Le titre se joue le vendredi — sauf si c'est trop loin : le Taureau n'attend pas.
            if (Count("taureau:jour") == 0)
            {
                int from = Mathf.Max(Day, Count("fosse:jour") + 1);
                int target = from;
                for (int d = from; d < from + 7; d++)
                {
                    if (WeekdayIndex(d) != 4) continue;
                    target = d;
                    break;
                }

                if (target - from > 2) target = from;
                SetCount("taureau:jour", target);
                Sms("ROSA", WeekdayIndex(target) == 4
                    ? "Le Taureau. Vendredi soir. Toute la ville a déjà parié. — R."
                    : "Le Taureau ne veut pas attendre vendredi. " + Weekday(target) + " soir. — R.");
            }

            int night = Count("taureau:jour");
            while (Day < night)
            {
                yield return UntilNextDay(Day, "Le titre de la Fosse : le Taureau, " + Weekday(night) + " soir (jour " + night + "). Le lit fait passer le temps.");
            }

            yield return UntilNight("Le titre de la Fosse : le Taureau, ce soir.");
            Put("ROSA", "rosa");
            yield return BackRoom("Le Vertigo, la Fosse : le Taureau t'attend pour le titre.");
            yield return Lines(
                L("ROSA", "Mesdames, messieurs. Le titre."),
                L("ROSA", "À ma gauche, invaincu depuis deux ans : le Taureau. À ma droite, le boxeur que la fédération a jeté : Marchal."));

            OpenWorldDirector.StoryContract bull = Job("taureau", "taureau", "fosse");
            bull.Reward = 1500;
            bull.Experience = 650;
            bull.Tune = Style(0.25f, 0.2f, 0f, 0f, 4f, 0.85f);
            bull.Lines = new[]
            {
                L("LE TAUREAU", "Le livreur. On m'a parlé de toi."),
                L("MOI", "On m'a parlé de toi aussi. Onze combats, onze K.O."),
                L("LE TAUREAU", "Douze, dans deux minutes. Rien de personnel, petit. J'ai une famille à nourrir.")
            };
            yield return Pit(bull);

            Set("taureau:battu");
            News("fosse");
            yield return Lines(
                L("LE TAUREAU", "(au sol) Bien joué, petit. Bien joué."),
                L("LE TAUREAU", "Je vais te dire un truc. Il y a deux ans, un type du casino m'a payé pour tomber. Et moi, j'ai pris l'argent."),
                L("LE TAUREAU", "Tu sais ce qu'il m'a dit, en me tendant l'enveloppe ? « Comme le petit Marchal. »"),
                L("MOI", "…"),
                L("LE TAUREAU", "Ton combat, il y a trois ans. T'as pas perdu, Marchal. On t'a fait perdre."),
                L("ROSA", "Le nouveau champion de la Fosse. Bois un verre, champion. C'est la maison qui offre."));
            Free("ROSA", false);
            Set("a2:taureau");
        }
    }
}
