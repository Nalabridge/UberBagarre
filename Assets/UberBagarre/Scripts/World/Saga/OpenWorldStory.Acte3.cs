using System.Collections;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// ACTE 3 — « Le Comptable » (jours 17 à 23). Sarkis t'offre 50 000 € pour perdre, comme il
    /// y a trois ans. Le plan : son carnet noir, dans le coffre. Tony trouve un uniforme de
    /// serveur, Jeff repère les rondes, Karim connaît la porte des livraisons. La nuit du casse
    /// (en force, en douceur, ou les deux), Sarkis dans son bureau, le carnet — le gros choix :
    /// Brandt, Duval, ou le garder — et le nom qu'il crache au sol : Sami.
    /// </summary>
    public partial class OpenWorldStory
    {
        private IEnumerator Acte3Line()
        {
            // --- 1. le rendez-vous au casino
            if (!Done("a3:rdv")) yield return MeetSarkis();

            // --- 2. le plan : Tony, Jeff, Karim
            if (!Done("a3:plan"))
            {
                yield return Lines(
                    L("MOI", "Son carnet. Rosa dit que tout est dedans : les noms, les montants, les combats achetés."),
                    L("MOI", "Il le garde dans le coffre de son bureau. Faut que j'entre là-dedans."),
                    L("MOI", "Un uniforme, les rondes des vigiles, une porte. Tony, Jeff, Karim."));
                yield return Plan();
                Set("a3:plan");
            }

            // --- 3. la nuit du casse
            if (!Done("a3:casse")) yield return Heist();

            // --- 4. Sarkis, dans son bureau
            if (!Done("a3:sarkis"))
            {
                OpenWorldDirector.StoryContract sarkis = Job("comptable", "sarkis", Pick("casino", "casino_back"));
                sarkis.Spot = SpotAt("comptable", Pick("casino", "casino_back"), "telephone", new Vector3(0f, 0f, 3.5f));
                sarkis.Direct = true;
                sarkis.NoPhoto = true;
                sarkis.Client = "LE COFFRE";
                sarkis.Reward = 2500;
                sarkis.Experience = 750;
                sarkis.Tune = Style(0.3f, 0.25f, 0.1f, 0f, 3f, 0.9f);
                if (Count("casse:facon") != 1) sarkis.Extras = Crew("vigile", "GARDE DU CORPS", 1);
                sarkis.Lines = Done("sarkis:accepte")
                    ? new[]
                    {
                        L("VICTOR SARKIS", "Monsieur Marchal. Vous aviez accepté, pourtant."),
                        L("VICTOR SARKIS", "Je déteste les mauvais payeurs. Et je déteste encore plus les voleurs."),
                        L("MOI", "Alors on va bien s'entendre. Moi, je déteste les deux.")
                    }
                    : new[]
                    {
                        L("VICTOR SARKIS", "Monsieur Marchal. Vous auriez pu être riche."),
                        L("MOI", "Je préfère être debout."),
                        L("VICTOR SARKIS", "Personne ne reste debout, dans mon carnet.")
                    };
                yield return Fight(sarkis, null, "Sarkis s'est barricadé. Faudra revenir, une autre nuit.");
                News("casino");
                Set("a3:sarkis");
            }

            // --- 5. le carnet, et le nom
            if (!Done("a3:carnet")) yield return Notebook();

            // --- 6. ce que devient le carnet
            if (!Done("a3:suite")) yield return NotebookAftermath();

            Advance(Acte4);
        }

        private IEnumerator MeetSarkis()
        {
            string casino = Pick("casino", "casino_back");
            yield return UntilHour(20f, "Le Casino Royal, ce soir : Victor Sarkis t'attend.");
            Put("SARKIS", casino, new Vector3(0f, 0f, 3f), 180f);
            Put("VIGILE1", casino, new Vector3(-1.5f, 0f, 3.9f), 180f);
            Put("VIGILE2", casino, new Vector3(1.6f, 0f, 3.7f), 180f);

            yield return TalkTo("SARKIS", casino, "Le Casino Royal : Victor Sarkis t'attend.");
            yield return Lines(
                L("SARKIS", "Monsieur Marchal. Asseyez-vous. Un verre ? Non ? Vous avez raison, tout coûte cher, ici."),
                L("SARKIS", "Je vais être direct, je suis comptable. Cinquante mille euros."),
                L("SARKIS", "Votre prochain combat à la Fosse. Au troisième round, vous tombez. Comme il y a trois ans."),
                L("MOI", "Comme il y a trois ans."),
                L("SARKIS", "Oh, ne faites pas cette tête. Tout le monde a un prix. Le vôtre a simplement augmenté."));
            yield return Choose("VICTOR SARKIS", "« Alors ? Cinquante mille. Un seul round à perdre. »",
                "Faire semblant d'accepter.", "Refuser. Et lui dire où il peut se les mettre.");

            if (_answer == 0)
            {
                Set("sarkis:accepte");
                ChangeCode(-3, "Tu as serré la main de Sarkis");
                _progress.AddMoney(5000, "Acompte (V. Sarkis)");
                yield return Lines(
                    L("SARKIS", "Parfait. Je savais que vous étiez raisonnable. Un acompte : cinq mille. Le reste après le combat."),
                    L("MOI", "(Garde ton sourire, Léo. Garde-le.)"));
            }
            else
            {
                Set("sarkis:refuse");
                ChangeCode(3, "Tu as dit non à Sarkis");
                yield return Lines(
                    L("SARKIS", "Dommage. Les gens qui refusent finissent souvent… dans mon carnet."),
                    L("SARKIS", "Raccompagnez monsieur. Gentiment. Pour l'instant."));
            }

            Free("SARKIS");
            Free("VIGILE1");
            Free("VIGILE2");
            Set("a3:rdv");
        }

        // ------------------------------------------------------------------ le plan

        private string Check(string flag, string text)
        {
            return (Done(flag) ? "✓ " : "") + text;
        }

        private IEnumerator Plan()
        {
            string karimPlace = Done("karim:frappe") ? "medical_ext" : "taco";
            Put("TONY", Pick("barbier", "barbier_ext"));
            Put("JEFF", "skatepark");
            Put("KARIM", karimPlace);
            string[] helpers = { "TONY", "JEFF", "KARIM" };

            while (!(Done("casse:uniforme") && Done("casse:rondes") && Done("casse:porte")))
            {
                Goal("Prépare le casse : " + Check("casse:uniforme", "l'uniforme (Tony, le barbier)") + "  ·  " +
                     Check("casse:rondes", "les rondes (Jeff, au skatepark)") + "  ·  " +
                     Check("casse:porte", Done("karim:frappe") ? "la porte (Karim, au centre médical)" : "la porte (Karim, au Taco Ticklers)"));

                int[] before = new int[helpers.Length];
                for (int i = 0; i < helpers.Length; i++)
                {
                    before[i] = Talks(helpers[i]);
                    _waitingTalk.Add(helpers[i]);
                }

                string who = null;
                while (who == null)
                {
                    for (int i = 0; i < helpers.Length && who == null; i++) if (Talks(helpers[i]) != before[i]) who = helpers[i];
                    yield return null;
                }

                for (int i = 0; i < helpers.Length; i++) _waitingTalk.Remove(helpers[i]);
                _pendingTalk = null;
                yield return Calm();

                if (who == "TONY") yield return TonyHelps();
                else if (who == "JEFF") yield return JeffHelps();
                else yield return KarimHelps();
            }

            Goal(string.Empty);
            Free("TONY", false);
            Free("JEFF", false);
            Free("KARIM", false);
            yield return Lines(L("MOI", "L'uniforme, les rondes, la porte. Il ne manque que la nuit."));
        }

        private IEnumerator TonyHelps()
        {
            if (Done("casse:uniforme"))
            {
                yield return Lines(L("TONY", "T'as ton uniforme, champion. Et n'oublie pas : un serveur, ça sourit."));
                yield break;
            }

            yield return Lines(
                L("TONY", "Léo Marchal ! Assieds-toi, je te fais la barbe ? T'en as besoin."),
                L("MOI", "Il me faut un uniforme de serveur. Celui du casino."),
                L("TONY", "… J'ai un cousin qui fait le linge pour eux. Taille L, nœud papillon compris."),
                L("TONY", "Tu me le rends propre. Et t'as jamais mis les pieds ici."));
            Set("casse:uniforme");
        }

        private IEnumerator JeffHelps()
        {
            if (Done("casse:rondes"))
            {
                yield return Lines(L("JEFF", "Toutes les dix minutes, la pause café. Une minute. Pas plus. Je le répète pas trois fois."));
                yield break;
            }

            yield return Lines(
                L("JEFF", "Les rondes ? J'ai passé trois nuits sur le parking avec ma planche. Personne regarde un gamin sur une planche."),
                L("JEFF", "Trois vigiles. Toutes les dix minutes, ils se retrouvent à la machine à café, derrière."),
                L("JEFF", "Pendant une minute, le couloir du bureau est vide. Une minute, pas plus."),
                L("JEFF", "Je t'envoie un SMS quand ça commence. Et ça, c'est gratuit. Pour une fois."));
            Set("casse:rondes");
        }

        private IEnumerator KarimHelps()
        {
            if (Done("casse:porte"))
            {
                yield return Lines(L("KARIM", "1-9-7-4. Tu l'écris pas, tu le retiens."));
                yield break;
            }

            if (!Done("karim:frappe"))
            {
                yield return Lines(
                    L("KARIM", "La porte des livraisons, derrière le casino ? Je la connais par cœur, j'y livre tous les soirs."),
                    L("KARIM", "Le code, c'est 1-9-7-4. L'année de naissance de Sarkis. Il est pas malin, pour un comptable."),
                    L("KARIM", "Tu m'as pas cassé le bras. Je te dois bien ça."));
                Set("casse:porte");
                yield break;
            }

            yield return Lines(
                L("KARIM", "(le bras dans le plâtre) Toi. T'as du culot de venir ici."),
                L("MOI", "La porte des livraisons du casino. T'y livrais, avant."),
                L("KARIM", "Avant que tu m'envoies ici, oui."));
            yield return Choose("KARIM", "« Pourquoi je t'aiderais ? »",
                "Payer l'info (800 €).", "Lui faire peur.", "Lui demander pardon.");

            switch (_answer)
            {
                case 0:
                    if (_progress.Spend(800, "Karim (une info)"))
                    {
                        yield return Lines(L("KARIM", "1-9-7-4. Et maintenant, dégage."));
                        Set("casse:porte");
                    }
                    else
                    {
                        yield return Lines(L("KARIM", "Huit cents. T'as même pas ça ? Reviens quand t'as de quoi."));
                    }

                    break;

                case 1:
                    ChangeCode(-5, "Tu as menacé Karim");
                    yield return Lines(
                        L("MOI", "Il te reste un bras valide, Karim."),
                        L("KARIM", "… 1-9-7-4. T'es content ? T'es exactement comme eux."));
                    Set("casse:porte");
                    break;

                default:
                    ChangeCode(4, "Tu as demandé pardon à Karim");
                    yield return Lines(
                        L("MOI", "J'aurais pas dû. J'avais besoin de l'argent. C'est pas une excuse."),
                        L("KARIM", "… Non. C'est pas une excuse."),
                        L("KARIM", "1-9-7-4. Fais-le tomber, Sarkis. Et on sera quittes. Presque."));
                    Set("casse:porte");
                    break;
            }
        }

        // ------------------------------------------------------------------ le casse

        private IEnumerator Heist()
        {
            if (_director != null) _director.Paused = true;
            yield return UntilHour(23f, "La nuit du casse : derrière le casino, après 23 h.");
            yield return Reach("casino_back", 10f, "Derrière le casino : la porte des livraisons.");
            yield return Choose("LE CASSE", "Comment tu entres ?",
                "En force : la porte, et tout ce qui se trouve derrière.",
                "En douceur : l'uniforme, le code de Karim, le timing de Jeff.",
                "Les deux : en douceur… et on verra bien.");
            int way = _answer;
            SetCount("casse:facon", way);

            if (way == 0)
            {
                OpenWorldDirector.StoryContract guards = Job("vigile", "casse:force", "casino_back");
                guards.Direct = true;
                guards.NoPhoto = true;
                guards.Client = "LE CASSE";
                guards.Reward = 0;
                guards.Experience = 320;
                guards.Extras = Crew("vigile", "VIGILE DU CASINO", 2);
                guards.Lines = new[]
                {
                    L("VIGILE DU CASINO", "Hé. C'est fermé, ici. Les livraisons, c'est le matin."),
                    L("MOI", "J'ai une livraison spéciale.")
                };
                yield return Fight(guards, null, "Les vigiles ont appelé des renforts. Faut revenir une autre nuit.");
                ChangeCode(-3, "Le casse en force");
                yield return Lines(L("MOI", "La porte est ouverte. Le bureau est au fond. Vite, avant que quelqu'un appelle les flics."));
                yield return Reach(Pick("casino", "casino_back"), 5f, "Le bureau de Sarkis : entre dans le casino.");
            }
            else
            {
                yield return Lines(
                    L("MOI", "Le nœud papillon. Le plateau. 1-9-7-4."),
                    L("MOI", "Maintenant, j'attends le signal de Jeff."));
                yield return new WaitForSeconds(6f);
                Sms("JEFF", "c maintenant. pause café. t'as une minute. GO");

                // Une minute pour traverser et atteindre le bureau.
                string office = Pick("casino", "casino_back");
                float window = 60f;
                bool made = false;
                while (window > 0f)
                {
                    Goal("En douceur : entre dans le casino et file au bureau avant la fin de la pause — " + Mathf.CeilToInt(window) + " s");
                    Mark(office);
                    if (Near(office, 6f))
                    {
                        made = true;
                        break;
                    }

                    window -= Time.deltaTime;
                    yield return null;
                }

                Unmark();
                Goal(string.Empty);

                if (made && way == 1)
                {
                    ChangeCode(2, "Le casse en douceur");
                    yield return Lines(L("MOI", "Personne. Le couloir, la porte du bureau. Trop facile."));
                }
                else
                {
                    // Ça dérape : un vigile revient trop tôt.
                    string where = Near(office, 20f) ? office : "casino_back";
                    OpenWorldDirector.StoryContract late = Job("vigile", "casse:derape", where);
                    late.Spot = SpotAt("vigile", where, null, new Vector3(0f, 0f, 2.5f));
                    late.Direct = true;
                    late.NoPhoto = true;
                    late.Client = "LE CASSE";
                    late.Reward = 0;
                    late.Experience = 220;
                    late.Lines = new[]
                    {
                        L("VIGILE DU CASINO", "Hé ! Toi, le serveur ! Depuis quand on sert au bureau ?"),
                        L("MOI", "Depuis ce soir.")
                    };
                    yield return Fight(late, null, "Grillé. Faudra retenter une autre nuit.");
                    if (!made) yield return Reach(office, 5f, "Le bureau de Sarkis : entre dans le casino.");
                }
            }

            if (_director != null) _director.Paused = false;
            Set("a3:casse");
        }

        // ------------------------------------------------------------------ le carnet

        private IEnumerator Notebook()
        {
            yield return Lines(
                L("MOI", "Le coffre est ouvert. Des liasses, et un carnet noir."),
                L("MOI", "Des noms. Des montants. « Le Taureau : 20 000. » « Marchal, finale régionale : 80 000 de paris. Réglé. »"),
                L("MOI", "Et partout, dans la marge, une initiale. « H. »"),
                L("VICTOR SARKIS", "(au sol) Vous voulez savoir… qui a mis le produit dans votre bouteille ?"),
                L("VICTOR SARKIS", "Moi, je paie. Je ne me salis jamais les mains. Celui qui l'a fait, c'était votre ami."),
                L("VICTOR SARKIS", "Sami Benali. Vingt mille euros. Il n'a même pas négocié."),
                L("MOI", "…"));
            Set("a3:nom");

            yield return Choose("LE CARNET", "Le carnet noir de Sarkis. Qu'est-ce que tu en fais ?",
                "Le donner au commissaire Brandt.",
                "Le donner à l'inspectrice Duval.",
                "Le garder. Et faire chanter tout ce petit monde.");
            Set(_answer == 0 ? "carnet:brandt" : _answer == 1 ? "carnet:duval" : "carnet:garde");
            Set("a3:carnet");
        }

        private IEnumerator NotebookAftermath()
        {
            if (Done("carnet:brandt"))
            {
                Put("BRANDT", "commissariat", new Vector3(0f, 0f, 2f), 180f);
                yield return TalkTo("BRANDT", "commissariat", "Le commissariat : le commissaire Brandt t'attend.");
                yield return Lines(
                    L("BRANDT", "Marchal. On m'a dit que vous aviez quelque chose pour moi."),
                    L("BRANDT", "(Il feuillette le carnet. Il sourit.) Merci. Vraiment. Vous n'imaginez pas le service que vous me rendez."),
                    L("BRANDT", "Embarquez-le."));
                Free("BRANDT");

                yield return Card("Garde à vue. Cellule 4.\n\n« Le commissaire te passe le bonjour. »", 3.5f);
                int fine = Mathf.RoundToInt(_progress.Money * 0.3f / 10f) * 10;
                if (fine > 0) _progress.AddMoney(-fine, "Garde à vue (« perdu » au commissariat)");
                _progress.NightInCell();
                if (WorldClock.Instance != null) WorldClock.Instance.SetHour(8f);
                News("carnet_brandt");

                Put("DUVAL", "commissariat", new Vector3(0f, 0f, 2f), 180f);
                yield return Lines(
                    L("DUVAL", "Debout. Je vous sors de là."),
                    L("DUVAL", "Vous avez donné le carnet à Brandt ? BRANDT ?"),
                    L("DUVAL", "C'est son nom qui est à toutes les pages, pauvre idiot. Le carnet est déjà en cendres."),
                    L("DUVAL", "Rentrez chez vous. Et la prochaine fois, réfléchissez avant de faire confiance à un uniforme."));
                SetCount("duval:confiance", Count("duval:confiance") - 1);
                yield return new WaitForSeconds(4f);
                Free("DUVAL");
            }
            else if (Done("carnet:duval"))
            {
                Put("DUVAL", "commissariat", new Vector3(0f, 0f, 2f), 180f);
                yield return TalkTo("DUVAL", "commissariat", "Retrouve l'inspectrice Duval, devant le commissariat.");
                yield return Lines(
                    L("DUVAL", "(Elle feuillette le carnet, longtemps.) C'est… énorme, Marchal."),
                    L("DUVAL", "Des flics. Des élus. Le casino. Et « H. », à toutes les pages."),
                    L("DUVAL", "J'ouvre une enquête. Une vraie. Avec un juge de la capitale, pas d'ici."),
                    L("DUVAL", "Mais à partir de maintenant, les hommes de Brandt vont vous chercher. Plus de petits arrangements avec les patrouilles."),
                    L("DUVAL", "Merci. Je ne le dis pas souvent."));
                SetCount("duval:confiance", Count("duval:confiance") + 2);
                ChangeCode(15, "Le carnet à Duval");
                if (PoliceSystem.Instance != null) PoliceSystem.Instance.NoBribes = true;
                News("carnet_duval");
                yield return new WaitForSeconds(4f);
                Free("DUVAL");
            }
            else
            {
                ChangeCode(-20, "Le chantage");
                yield return Lines(
                    L("MOI", "Un conseiller municipal. Un promoteur. Un juge."),
                    L("MOI", "Une lettre à chacun. Un compte à l'étranger. On va voir combien vaut leur silence."));
                yield return new WaitForSeconds(30f);
                _progress.AddMoney(8000, "Virement anonyme");
                Sms("BANQUE", "Virement reçu : 8 000,00 EUR. Émetteur : non communiqué.");
                News("carnet_garde");
                yield return new WaitForSeconds(20f);
                _progress.AddMoney(7000, "Virement anonyme");
                Sms("BANQUE", "Virement reçu : 7 000,00 EUR. Émetteur : non communiqué.");
                Set("proches:distants");
                yield return new WaitForSeconds(10f);
                Sms("LINA", "On m'a parlé du casino. Et des lettres. Je sais plus qui tu es, Léo.");
                yield return new WaitForSeconds(8f);
                Sms("RAY", "Tu fais chanter des gens, maintenant ? C'est pas ce que je t'ai appris.");
            }

            yield return Calm();
            yield return Lines(
                L("MOI", "Sami."),
                L("MOI", "Vingt mille euros. Mon meilleur ami."));
            Set("a3:suite");
        }
    }
}
