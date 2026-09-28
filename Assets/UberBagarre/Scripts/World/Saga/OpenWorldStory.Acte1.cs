using System.Collections;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// ACTE 1 — « Les petites notes » (jours 3 à 9). Les courses pour atteindre deux étoiles et
    /// payer Nestor ; Karim, le livreur (premier choix moral) ; Jeff, le skateur qui voit tout ;
    /// Dragan Kovac derrière la pizzeria ; Lina au cabinet médical ; la vengeance de Milan à
    /// quatre contre un devant le motel ; l'inspectrice Duval qui frappe à la porte ; et, dans
    /// le téléphone de Milan, six cibles sur huit qui devaient de l'argent au casino.
    /// </summary>
    public partial class OpenWorldStory
    {
        private IEnumerator Acte1Line()
        {
            if (!Done("a1:debut"))
            {
                SetCount("a1:base", _progress.Contracts);
                Set("a1:debut");
                yield return new WaitForSeconds(3f);
                yield return Texts("SAMI",
                    "bienvenue dans la vraie vie",
                    "l'appli te propose des courses toute seule maintenant. accepte, cogne, photo",
                    "fais-en 3 ou 4. monte à 2 étoiles. et paie ce gros porc de nestor");
            }

            // --- 1. les courses : trois, et deux étoiles
            if (!Done("a1:courses"))
            {
                int basis = Count("a1:base");
                while (_progress.Contracts - basis < 3 || _progress.MaxStarsOffered < 2)
                {
                    if (IsCalm)
                    {
                        int done = Mathf.Clamp(_progress.Contracts - basis, 0, 3);
                        string stars = _progress.MaxStarsOffered >= 2 ? "deux étoiles : ok" : "vise deux étoiles (réputation et niveau)";
                        Goal("Fais des courses sur l'appli : " + done + " / 3 — " + stars + ".  Nestor : 1 500 € le jour " +
                             Count("nestor:echeance") + ".");
                    }

                    yield return new WaitForSeconds(1f);
                }

                Goal(string.Empty);
                Set("a1:courses");
                yield return Lines(L("MOI", "Deux étoiles. Les commandes changent de tête : plus chères, plus lourdes."));
            }

            // --- 2. Karim, le livreur : le premier choix
            if (!Done("a1:karim"))
            {
                yield return new WaitForSeconds(5f);
                OpenWorldDirector.StoryContract karim = Job("karim", "karim", "taco", "telephone");
                karim.Client = "CLIENT VÉRIFIÉ";
                karim.Reward = 300;
                karim.Experience = 220;
                karim.Intro = "Un livreur de Taco Ticklers qui « a mal parlé à un client ». Trois cents euros.";
                karim.Lines = new[]
                {
                    L("KARIM", "C'est pour moi ? Sérieux ?"),
                    L("MOI", "Karim ? Il paraît que t'as mal parlé à un client."),
                    L("KARIM", "Un client ? Quel client ? Je livre des tacos, moi !")
                };
                karim.ChoiceQuestion = "Karim recule, les mains en l'air : « Attends ! Attends, je vais t'expliquer ! »";
                karim.ChoiceOptions = new[] { "Le frapper. Trois cents euros, c'est trois cents euros.", "Le laisser parler." };
                yield return Fight(karim, "Nouvelle course sur l'appli : un livreur, derrière le Taco Ticklers.");

                if (_outcome == 0)
                {
                    Set("karim:frappe");
                    ChangeCode(-8, "Karim à l'hôpital");
                    News("karim");
                    yield return Lines(L("MOI", "Trois cents euros. Il tenait son bras comme un oiseau mort."));
                }
                else
                {
                    Set("karim:allie");
                    ChangeCode(10, "Tu as écouté Karim");
                    _progress.ChangeReputation(-3, "Course non livrée");
                    yield return Lines(
                        L("KARIM", "Le « client », c'est les Kovac. Ils voulaient que je livre des sachets avec les tacos."),
                        L("KARIM", "J'ai dit non. Alors ils ont commandé ma raclée. Sur ton appli."),
                        L("MOI", "C'est pas mon appli."),
                        L("KARIM", "Ah ouais ? C'est toi qui es venu, pourtant."),
                        L("KARIM", "… Merci, mec. Je te dois un taco. Plusieurs, même."));
                    yield return Texts("KARIM", "c karim. enregistre mon num", "livraison gratuite quand tu veux, je passe au motel");
                }

                Set("a1:karim");
            }

            // --- 3. Jeff, au skatepark
            if (!Done("a1:jeff"))
            {
                yield return new WaitForSeconds(8f);
                yield return Calm();
                yield return Texts("JEFF",
                    "yo c jeff. du shred shack",
                    Done("karim:frappe") ? "g vu ce que t'as fait au livreur. violent" : "g vu que t'as laissé partir le livreur. respect",
                    "passe au skatepark jai un truc pour toi");

                Put("JEFF", "skatepark");
                yield return TalkTo("JEFF", "skatepark", "Le skatepark : Jeff veut te voir.");
                yield return Lines(
                    L("JEFF", "T'es le mec de l'appli ! Je t'ai vu devant le Vertigo, t'as fait voler le videur."),
                    L("JEFF", "Moi je vois tout. Personne regarde un gamin sur une planche."),
                    L("JEFF", "Les Kovac, par exemple. Dragan, le grand, il fait ses affaires derrière la pizzeria. Tous les soirs."),
                    L("MOI", "Pourquoi tu me dis ça ?"),
                    L("JEFF", "Parce qu'ils ont cassé la planche de mon pote. Et parce que t'as une tête à avoir besoin d'infos."),
                    L("JEFF", "Vingt balles l'info. La première est gratuite."));
                Free("JEFF", false);
                Set("a1:jeff");
            }

            // --- 4. Dragan Kovac, derrière la pizzeria
            if (!Done("a1:dragan"))
            {
                yield return UntilHour(18f, "Dragan Kovac fait ses affaires derrière la pizzeria, le soir.");
                OpenWorldDirector.StoryContract dragan = Job("dragan", "dragan", "pizzeria", "deal");
                dragan.Client = "CLIENT ANONYME";
                dragan.Reward = 420;
                dragan.Experience = 300;
                dragan.Goal = ContractGoal.CasserNez;
                dragan.Intro = "Dragan Kovac. Les docks, les camionnettes. Quelqu'un paie cher pour qu'il saigne du nez.";
                dragan.Lines = new[]
                {
                    L("DRAGAN KOVAC", "Hé. T'es perdu, toi ?"),
                    L("MOI", "Dragan Kovac ?"),
                    L("DRAGAN KOVAC", "Tu sais qui je suis, et tu viens quand même. Soit t'es courageux, soit t'es bête."),
                    L("MOI", "Deux étoiles. Rien de personnel."),
                    L("DRAGAN KOVAC", "Mon frère va t'enterrer.")
                };
                yield return Fight(dragan, "Une course à deux étoiles : Dragan Kovac, derrière la pizzeria.");
                Set("a1:dragan");
                News("kovac");
                yield return Lines(
                    L("MOI", "Il avait des bagues, le Dragan. J'ai l'arcade ouverte."),
                    L("MOI", "Ça pisse le sang. Faut que quelqu'un regarde ça."));
            }

            // --- 5. Lina, au cabinet médical
            if (!Done("a1:lina"))
            {
                Put("LINA", "cabinet");
                yield return TalkTo("LINA", "cabinet", "Le cabinet médical : fais-toi recoudre.");
                yield return Lines(
                    L("LINA", "Asseyez-vous. Montrez-moi ça."),
                    L("LINA", "… Attendez. Je vous connais. T'es le boxeur radié. Marchal."),
                    L("MOI", "Ex-boxeur. Maintenant je… livre."),
                    L("LINA", "Tu livres quoi ? Des coquards ?"),
                    L("LINA", "Mon frère, Théo, s'est fait tabasser il y a deux mois. Par un type qui a pris une photo de lui, par terre."),
                    L("LINA", "Une appli, apparemment. Théo ne marche plus sans canne."));
                if (Done("karim:frappe"))
                {
                    yield return Lines(
                        L("LINA", "Et le livreur du Taco Ticklers, en bas, avec le bras cassé. Même histoire."),
                        L("MOI", "…"));
                }

                yield return Lines(
                    L("LINA", "Si j'apprends que t'as quelque chose à voir avec ça, je te recouds la bouche."),
                    L("LINA", "Voilà. Trois points. Évite de te faire frapper au visage, ça abîme mon travail."));
                _progress.HealInjuries(true);
                Free("LINA", false);
                Set("a1:lina");
                Sms("LINA", "Garde le pansement sec. Et pas de bêtises. — Lina");
            }

            // --- 6. la vengeance de Milan : quatre contre un, devant le motel
            if (!Done("a1:milan"))
            {
                yield return UntilNight("La journée a été longue. Rentre au motel.");
                yield return Reach("motel", 40f, "Rentre au motel.");

                OpenWorldDirector.StoryContract milan = Job("milan", "milan", "motel", "telephone");
                milan.Spot = SpotAt("milan", "motel", "telephone", new Vector3(0f, 0f, 5f));
                milan.Direct = true;
                milan.NoPhoto = true;
                milan.Client = "MILAN KOVAC";
                milan.Reward = 0;
                milan.Experience = 420;
                milan.Extras = Crew("kovac_gars", "GARS DES KOVAC", 3);
                milan.Lines = new[]
                {
                    L("MILAN KOVAC", "Tu te souviens de mon frère ? Lui se souvient de toi. Il mange à la paille."),
                    L("MILAN KOVAC", "On est quatre. Toi t'es un. Même un livreur sait compter."),
                    L("MOI", "Je sais compter. C'est pour ça que je commence par toi.")
                };
                yield return Fight(milan, null, "Milan va revenir. Avec ses gars. Faut que je sois prêt.");

                Set("a1:milan");
                Set("milan:rancune");
                SetCount("a1:milan_jour", Day);
                News("motel");
                yield return Lines(
                    L("MOI", "Quatre."),
                    L("MOI", "Il a laissé tomber son téléphone en partant. Je le garde."));
            }

            // --- 7. l'inspectrice Duval frappe à la porte
            if (!Done("a1:duval"))
            {
                yield return UntilNextDay(Count("a1:milan_jour"), "Rentre dormir. Tu l'as bien mérité.");
                yield return new WaitForSeconds(3f);
                yield return Lines(L("MOI", "On frappe. Qui frappe à la porte d'un motel à huit heures du matin ?"));

                Put("DUVAL", "motel", new Vector3(0f, 0f, 2.5f), 180f);
                yield return TalkTo("DUVAL", "motel", "Quelqu'un frappe à la porte. Sors voir.");
                yield return Lines(
                    L("DUVAL", "Monsieur Marchal ? Inspectrice Camille Duval. Police judiciaire."),
                    L("DUVAL", "Je suis arrivée de la capitale il y a trois semaines. En trois semaines : onze agressions."),
                    L("DUVAL", "Des gens tabassés, puis photographiés par terre. Tous des perdants de la ville."),
                    L("DUVAL", "Des gens qui doivent de l'argent. Qui dérangent. Qui ont dit non à la mauvaise personne."));
                yield return Choose("INSPECTRICE DUVAL", "« Une appli qui s'appelle… Über Bagarre. Ça vous dit quelque chose ? »",
                    "Jamais entendu parler.", "Je livre des pizzas, madame.", "Si j'entends quelque chose, je vous appelle.");

                int trust = 0;
                switch (_answer)
                {
                    case 0:
                        yield return Lines(L("DUVAL", "Bien sûr. Personne n'en a jamais entendu parler. C'est fou comme elle marche bien, pour une appli que personne ne connaît."));
                        trust = -1;
                        break;
                    case 1:
                        yield return Lines(L("DUVAL", "Des pizzas. Avec ces mains-là. D'accord."));
                        break;
                    default:
                        yield return Lines(L("DUVAL", "… Vous êtes le premier à me dire ça. On verra si c'est vrai."));
                        trust = 1;
                        ChangeCode(2, "Duval");
                        break;
                }

                if (Done("police:corruption"))
                {
                    yield return Lines(L("DUVAL", "Et arrêtez de glisser des billets aux agents de Brandt. Ça se sait. Même moi, je le sais."));
                    trust--;
                }

                yield return Lines(
                    L("DUVAL", "(Elle tend une carte.) Mon numéro. Ligne directe."),
                    L("DUVAL", "Le jour où vous en aurez assez d'avoir peur, appelez-moi."));
                SetCount("duval:confiance", trust);
                Sms("DUVAL", "Inspectrice C. Duval. Ma ligne directe, jour et nuit.");
                News("agressions");
                Free("DUVAL");
                Set("a1:duval");
            }

            // --- 8. le téléphone de Milan
            if (!Done("a1:fin"))
            {
                yield return new WaitForSeconds(20f);
                yield return Calm();
                yield return Lines(
                    L("MOI", "Le téléphone de Milan. Pas de code. Évidemment."),
                    L("MOI", "Des captures de l'appli. Des commandes. Des photos de gens par terre. Huit ce mois-ci."),
                    L("MOI", "Et à côté de six noms sur huit, la même note : « dette casino »."),
                    L("MOI", "Six sur huit devaient de l'argent au casino."));
                yield return Texts("JEFF",
                    "le casino c un autre monde",
                    "si tu veux entrer dans ce monde là faut passer par la fosse du vertigo. tout le monde le dit");
                Set("a1:fin");
            }

            Advance(Acte2);
        }
    }
}
