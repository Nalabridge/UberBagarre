using System.Collections;
using System.Collections.Generic;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Ce qui fait vivre la ville entre deux étapes de l'histoire :
    /// - les habitués, à leur place selon l'heure (Ray à la salle, maman à la laverie, Lina au
    ///   cabinet, Jeff au skatepark, Karim au Taco Ticklers, M. Chen au Dragon d'Or, Tony chez
    ///   lui, Rosa et le Taureau à la Fosse la nuit) : on va les voir, ils répondent selon ce
    ///   qu'ils savent de toi (le Code, les journaux, les arrestations) ;
    /// - Nestor : 1 500 € par semaine, un rappel deux jours avant ; en retard, des intérêts et
    ///   ses encaisseurs devant le motel, un de plus à chaque fois ;
    /// - M. Chen et le racket des Kovac, le tournoi de Ray (trois combats à la salle), la
    ///   revanche du Dentiste, la ceinture à racheter ;
    /// - les SMS : maman (la chapelle le dimanche), Lina, les livraisons de Karim, Rosa le
    ///   vendredi (la Fosse), Jeff le samedi (le casino).
    /// </summary>
    public partial class OpenWorldStory
    {
        private static readonly string[] Boxers = { "DJIBRIL", "LE BOUCHER", "GROS MARCO" };

        private readonly HashSet<string> _side = new HashSet<string>();
        private float _beltNext;

        private IEnumerator City()
        {
            _side.Clear();
            int lastDay = Day;
            while (true)
            {
                yield return new WaitForSeconds(1f);
                if (_progress == null) continue;

                Residents();

                if (Day != lastDay)
                {
                    lastDay = Day;
                    Side("jour", NewDay());
                }

                SideQuests();
            }
        }

        /// <summary>Lance une histoire secondaire si elle ne tourne pas déjà.</summary>
        private void Side(string id, IEnumerator routine)
        {
            if (_side.Contains(id)) return;
            _side.Add(id);
            StartCoroutine(RunSide(id, routine));
        }

        private IEnumerator RunSide(string id, IEnumerator routine)
        {
            yield return routine;
            _side.Remove(id);
        }

        private static bool Between(float hour, float from, float to)
        {
            return from <= to ? hour >= from && hour < to : hour >= from || hour < to;
        }

        // ------------------------------------------------------------------ les habitués

        private void Residents()
        {
            float h = Hour;
            bool ended = Done("fin:atteinte");
            bool left = Done("fin:fantome");

            Resident("RAY", Done("p:ray") && Between(h, 6f, 22f), "salle", new Vector3(0.5f, 0f, 1f), 0f);
            Resident("MAMAN", Done("p:nestor") && !left && Between(h, 8f, 19f), "laverie", Vector3.zero, 0f);
            Resident("LINA", Done("a1:lina") && Between(h, 8f, 20f) && !(ended && Done("fin:roi")), "cabinet", Vector3.zero, 0f);
            Resident("JEFF", Done("a1:jeff") && Between(h, 10f, 22f) && !(Done("a4:choix") && !Done("a4:jeff") && Done("milan:rancune")),
                "skatepark", Vector3.zero, 0f);
            Resident("KARIM", Done("karim:allie") && Between(h, 11f, 23f), "taco", Vector3.zero, 0f);
            Resident("CHEN", Done("chen:propose") && Between(h, 11f, 23f), Pick("chen", "chen_ext"), Vector3.zero, 0f);
            Resident("TONY", Done("a3:rdv") && Between(h, 9f, 19f), Pick("barbier", "barbier_ext"), Vector3.zero, 0f);
            Resident("ROSA", Done("a2:test") && Between(h, 21f, 4f), "rosa", Vector3.zero, 0f);
            Resident("TAUREAU", Done("taureau:battu") && Between(h, 21f, 4f), "rosa", new Vector3(2.2f, 0f, 1.2f), -40f);
        }

        private void Resident(string id, bool present, string place, Vector3 offset, float turn)
        {
            if (_cast == null || _reserved.Contains(id)) return;
            StoryActor a = _cast.Actor(id);
            if (a == null) return;

            if (present)
            {
                if (!a.Placed) _cast.PlaceAt(id, place, offset, turn);
                a.SetTalkable(true);
            }
            else if (a.Placed && Flat(PlayerPosition - a.transform.position) > 25f)
            {
                // On ne disparaît pas sous les yeux du joueur : on attend qu'il soit loin.
                a.Hide();
            }
        }

        // ------------------------------------------------------------------ les déclencheurs

        private void SideQuests()
        {
            if (Done("fin:atteinte") && !Done("fin:justicier")) return;

            // Nestor : l'échéance, et la dette soldée.
            if (Done("p:nestor") && !Done("nestor:solde"))
            {
                if (_progress.Debt <= 0) Side("nestor", DebtCleared());
                else if (Day >= Count("nestor:echeance") && !Done("nestor:encaisseurs")) Side("nestor", NestorDue());
                if (Done("nestor:encaisseurs")) Side("encaisseurs", Collectors());
            }

            // M. Chen : Karim ou Jeff en parlent, puis le racket du soir.
            if (Done("a1:dragan") && !Done("chen:propose")) Side("chen", ChenIntro());
            if (Done("chen:aide") && !Done("chen:fini")) Side("racket", ChenRacket());

            // Le tournoi de Ray.
            if (Done("tournoi:oui") && !Done("tournoi:fini")) Side("tournoi", Tournament());

            // Le Dentiste n'a pas digéré la Fosse.
            if (Done("dentiste:rancune") && !Done("dentiste:revanche") && _progress.Chapter >= Acte3) Side("dentiste", DentistRevenge());

            // La ceinture, toujours en vitrine.
            if (Done("ceinture:vue") && !Done("ceinture:rachetee") && Time.time > _beltNext && Near(Pick("pawn", "pawn_ext"), 4f) && IsCalm)
            {
                _beltNext = Time.time + 120f;
                Side("ceinture", BeltAgain());
            }

            // Duval voit tout, même les billets glissés aux agents.
            if (Done("a1:duval") && Done("police:corruption") && !Done("duval:corruption"))
            {
                Set("duval:corruption");
                SetCount("duval:confiance", Count("duval:confiance") - 1);
                Sms("DUVAL", "Un agent de Brandt a encore empoché votre argent. Je ne suis pas aveugle, Marchal.");
            }
        }

        // ------------------------------------------------------------------ chaque matin

        private IEnumerator NewDay()
        {
            yield return new WaitForSeconds(6f);
            int weekday = WeekdayIndex(Day);

            // Nestor rappelle, deux jours avant.
            if (Done("p:nestor") && !Done("nestor:solde") && Count("nestor:echeance") - Day == 2)
            {
                int required = 1500 * (Count("nestor:rang") + 1);
                int paid = Count("nestor:verse");
                Sms("NESTOR", "Dans deux jours, Léo. Tu en es à " + paid + " sur " + required + ". Je compte sur toi.");
                yield return new WaitForSeconds(4f);
            }

            // Maman : le dimanche, la chapelle ; sinon, un jour sur deux, des nouvelles.
            if (Done("p:nestor") && !Done("fin:fantome"))
            {
                if (weekday == 6) Sms("MAMAN", "je vais à la chapelle ce matin. j'ai mis un cierge pour toi mon grand");
                else if (Day % 2 == 0) Sms("MAMAN", MomText());
                yield return new WaitForSeconds(5f);
            }

            // Lina, tous les trois jours.
            if (Done("a1:lina") && Day % 3 == 1 && !Done("fin:roi"))
            {
                Sms("LINA", LinaText());
                yield return new WaitForSeconds(5f);
            }

            // Karim livre, un jour sur deux.
            if (Done("karim:allie") && Day % 2 == 1)
            {
                _progress.AddMeals(2);
                Sms("KARIM", "deux tacos dans ton frigo. c'est la maison qui régale");
                yield return new WaitForSeconds(4f);
            }

            // Le rythme de la semaine.
            if (weekday == 4 && Done("a2:test") && !Done("fin:atteinte")) Sms("ROSA", "Vendredi. La Fosse ce soir, et les gros paris. — R.");
            if (weekday == 5 && Done("a1:jeff")) Sms("JEFF", "samedi = soirée casino. les riches sortent, les vigiles dorment");
            if (weekday == 2 && Done("chen:aide") && !Done("chen:fini")) Sms("M. CHEN", "Demain, c'est jeudi, monsieur Léo. Ils viennent le soir.");
        }

        private string MomText()
        {
            if (Done("fin:justicier")) return "tout le quartier parle de toi. je suis fière de toi. mange quand même";
            if (_progress.Chapter >= Acte5 && Done("news:chasse")) return "la télé dit des horreurs sur toi. je crois pas un mot. appelle moi";
            if (_progress.Arrests > 0 && Day % 4 == 0) return "la voisine dit qu'elle t'a vu dans une voiture de police. elle a mal vu hein ?";
            if (_progress.Code <= -30) return "tu as une drôle de voix au téléphone ces temps ci. tu es sûr que ça va ?";
            if (_progress.SentHome > 0 && Day % 3 == 0) return "merci pour le virement mon grand. il fallait pas. j'ai payé l'électricité";
            string[] pool =
            {
                "tu manges bien ?", "il fait froid, mets ton écharpe", "la laverie a encore une machine en panne. le patron s'en fiche",
                "j'ai vu ray au marché. il m'a demandé de tes nouvelles", "tu passes à la laverie cette semaine ?"
            };
            return pool[Day % pool.Length];
        }

        private string LinaText()
        {
            if (Done("proches:distants") || _progress.Code <= -40) return "Théo a fait un cauchemar cette nuit. Il criait « la photo, la photo ». Je sais pas pourquoi je te dis ça.";
            if (_progress.Code >= 30) return "Pause café à 16 h, si jamais. Je dis ça, je dis rien. — L.";
            if (_progress.Injuries > 0) return "On m'a dit que t'avais une sale tête. Passe au cabinet, je regarde ça.";
            return "Théo a remarché sans canne jusqu'au bout de la rue. Petite victoire. — L.";
        }

        // ------------------------------------------------------------------ Nestor

        private IEnumerator NestorDue()
        {
            int rank = Count("nestor:rang");
            int required = 1500 * (rank + 1);
            if (Count("nestor:verse") >= required)
            {
                SetCount("nestor:rang", rank + 1);
                SetCount("nestor:echeance", Count("nestor:echeance") + 7);
                Sms("NESTOR", "Reçu. Tu vois, Léo, quand on veut… À la semaine prochaine. Même jour.");
                yield break;
            }

            _progress.SetDebt(_progress.Debt + 300);
            SetCount("nestor:echeance", Day + 3);
            Set("nestor:encaisseurs");
            Sms("NESTOR", "Tu me déçois, Léo. Trois cents euros d'intérêts. Et mes amis passent te dire bonjour.");
        }

        private IEnumerator DebtCleared()
        {
            Set("nestor:solde");
            _progress.ClearFlag("nestor:encaisseurs");
            yield return Calm();
            yield return Texts("NESTOR", "Le compte est bon. On est quittes, Léo.", "C'était un plaisir. Si un jour tu as besoin… tu sais où me trouver.");
            yield return Lines(L("MOI", "Plus un centime à Nestor. Je crois que j'ai jamais respiré aussi bien."));
        }

        /// <summary>Les encaisseurs attendent le soir, devant le motel. Un de plus à chaque visite.</summary>
        private IEnumerator Collectors()
        {
            while (!(Between(Hour, 19f, 4f) && Near("motel", 40f))) yield return new WaitForSeconds(1f);
            while (_director != null && _director.PendingStoryTag != null) yield return new WaitForSeconds(1f);

            int visits = Count("encaisseurs");
            OpenWorldDirector.StoryContract c = Job("encaisseur", "nestor:encaisseurs:" + visits, "motel");
            c.Spot = SpotAt("encaisseur", "motel", null, new Vector3(-2f, 0f, 5f));
            c.Direct = true;
            c.NoPhoto = true;
            c.Client = "NESTOR";
            c.Reward = 0;
            c.Experience = 250 + 60 * visits;
            c.Extras = Crew("encaisseur", "ENCAISSEUR", Mathf.Min(1 + visits, 3));
            c.Tune = Style(0.15f * visits, 0.1f * visits, 0f, 0f, 2f * visits, 0.55f + 0.1f * Mathf.Min(visits, 4));
            c.Lines = new[]
            {
                L("ENCAISSEUR", visits == 0 ? "Monsieur Nestor te passe le bonjour." : "Encore nous. Monsieur Nestor aime pas se répéter."),
                L("MOI", "Dites-lui que je paie bientôt."),
                L("ENCAISSEUR", "On transmettra. Après.")
            };
            yield return Fight(c, null, null, true);

            if (_outcome == 0)
            {
                SetCount("encaisseurs", visits + 1);
                Sms("NESTOR", "Tu as abîmé mes amis. Ça aussi, ça se paie. Nouvelle échéance : jour " + Count("nestor:echeance") + ".");
            }
            else
            {
                int take = _progress.PayDebt(Mathf.Min(_progress.Money, 1000));
                Sms("NESTOR", take > 0 ? "Mes amis se sont servis : " + take + " €. On est presque bons amis, maintenant." : "Mes amis sont repartis les mains vides. La prochaine fois, ce sera les os.");
            }

            _progress.ClearFlag("nestor:encaisseurs");
        }

        // ------------------------------------------------------------------ M. Chen

        private IEnumerator ChenIntro()
        {
            yield return new WaitForSeconds(30f);
            yield return Calm();
            if (Done("karim:allie"))
            {
                yield return Texts("KARIM", "tu connais m. chen ? le dragon d'or",
                    "les gars des kovac le rackettent tous les jeudis. il ose rien dire",
                    "si t'as 5 min... il fait les meilleurs raviolis de la ville");
            }
            else
            {
                yield return Texts("JEFF", "le vieux chen du dragon d'or",
                    "les kovac le rackettent. 300 balles par semaine. il a plus rien",
                    "ça c'est une info gratuite. parce que ça m'énerve");
            }

            Set("chen:propose");
        }

        private IEnumerator ChenRacket()
        {
            string front = Pick("chen_ext", "chen");
            while (!(Between(Hour, 19f, 23.5f) && Near(front, 25f))) yield return new WaitForSeconds(1f);
            while (_director != null && _director.PendingStoryTag != null) yield return new WaitForSeconds(1f);

            OpenWorldDirector.StoryContract c = Job("kovac_gars", "chen:racket", front);
            c.Profile = Profile("kovac_gars", "RACKETTEUR DES KOVAC");
            c.Spot = SpotAt("kovac_gars", front, "fume", new Vector3(0f, 0f, 2f));
            c.Direct = true;
            c.NoPhoto = true;
            c.Client = "M. CHEN";
            c.Reward = 0;
            c.Experience = 300;
            c.Extras = Crew("kovac_gars", "RACKETTEUR", 1);
            c.Lines = new[]
            {
                L("RACKETTEUR DES KOVAC", "C'est jeudi, le vieux. L'enveloppe."),
                L("MOI", "Le vieux, il a plus d'enveloppe. Mais moi, j'ai un truc pour vous.")
            };
            yield return Fight(c, null, null, true);
            if (_outcome != 0)
            {
                yield return new WaitForSeconds(60f);
                yield break;
            }

            Set("chen:fini");
            News("chen");
            _progress.AddMeals(3);
            ChangeCode(8, "M. Chen");
            yield return Calm();
            yield return Texts("M. CHEN", "Monsieur Léo. Vous êtes un homme bien.", "Ici, vous mangerez toujours gratuitement. Trois repas dans votre frigo, pour commencer.");
        }

        // ------------------------------------------------------------------ le tournoi de Ray

        private IEnumerator Tournament()
        {
            while (Count("tournoi") < 3)
            {
                int bout = Count("tournoi");
                int last = Count("tournoi:jour");
                while (Day <= last) yield return new WaitForSeconds(2f);

                if (Count("tournoi:annonce") != Day)
                {
                    SetCount("tournoi:annonce", Day);
                    Sms("RAY", "Tournoi, combat n°" + (bout + 1) + " : ce soir à partir de 19 h, à la salle. Contre " + Boxers[bout] + ".");
                }

                while (!(Between(Hour, 19f, 23.5f) && Near("salle", 9f))) yield return new WaitForSeconds(1f);
                while (_director != null && _director.PendingStoryTag != null) yield return new WaitForSeconds(1f);
                yield return Calm();

                Put("RAY", "salle", new Vector3(0.5f, 0f, 1f));
                yield return Lines(
                    L("RAY", "Combat numéro " + (bout + 1) + ". " + Boxers[bout] + ". Trois reprises, des gants, un arbitre."),
                    L("RAY", "Et pas de coups au sol. Ici, c'est de la boxe."));

                OpenWorldDirector.StoryContract c = Job("recrue", "tournoi:" + bout, "salle");
                c.Profile = Profile("recrue", Boxers[bout]);
                c.Spot = SpotAt("recrue", "salle", "sentraine", new Vector3(-1.5f, 0f, 3f));
                c.Direct = true;
                c.NoPhoto = true;
                c.Client = "TOURNOI DE RAY";
                c.Reward = 400 + 100 * bout;
                c.Experience = 300 + 80 * bout;
                c.Tune = Style(0.15f * bout, 0.1f * bout, 0.05f * bout, 0f, 2f * bout, 0.5f + 0.15f * bout);
                c.Lines = new[] { L(Boxers[bout], "Le fameux Marchal. On va voir si t'es aussi bon qu'on le dit."), L("RAY", "Boxe !") };
                yield return Fight(c, null, null, true);

                SetCount("tournoi:jour", Day);
                if (_outcome != 0)
                {
                    yield return Lines(L("RAY", "Pas grave. Le public est resté. Reviens demain soir, on refait le combat."));
                    Free("RAY", false);
                    continue;
                }

                SetCount("tournoi", bout + 1);
                yield return Lines(L("RAY", bout < 2 ? "La salle est pleine, Léo. Pleine ! Ça faisait des années." : "Trois sur trois."));
                Free("RAY", false);
            }

            Set("tournoi:fini");
            News("tournoi");
            ChangeCode(5, "La salle de Ray");
            yield return Calm();
            yield return Texts("RAY", "Douze mille euros. De quoi payer un avocat contre Holt et son parking.", "Merci, champion.");
        }

        // ------------------------------------------------------------------ le Dentiste, la ceinture

        private IEnumerator DentistRevenge()
        {
            while (!(IsNight(Hour) && Near("motel", 45f))) yield return new WaitForSeconds(1f);
            while (_director != null && _director.PendingStoryTag != null) yield return new WaitForSeconds(1f);

            OpenWorldDirector.StoryContract c = Job("dentiste", "dentiste:revanche", "motel");
            c.Spot = SpotAt("dentiste", "motel", null, new Vector3(2f, 0f, 6f));
            c.Direct = true;
            c.NoPhoto = true;
            c.Client = "LE DENTISTE";
            c.Reward = 0;
            c.Experience = 450;
            c.Extras = Crew("recrue", "L'ASSISTANT", 1);
            c.Tune = Style(0.4f, 0.7f, 0.1f, 0f, 3f, 0.85f);
            c.Lines = new[]
            {
                L("LE DENTISTE", "Tu m'as fait virer de la Fosse. Et j'ai perdu deux dents."),
                L("LE DENTISTE", "Ce soir, c'est moi qui fais les extractions.")
            };
            yield return Fight(c, null, null, true);
            if (_outcome != 0)
            {
                yield return new WaitForSeconds(120f);
                yield break;
            }

            Set("dentiste:revanche");
            yield return Lines(L("MOI", "Il reviendra plus. Il a plus assez de dents pour ça."));
        }

        private IEnumerator BeltAgain()
        {
            yield return Lines(L("LE VENDEUR", "Toujours là, votre ceinture. Je la fais briller tous les matins."));
            yield return OfferBelt();
        }

        // ------------------------------------------------------------------ aller voir quelqu'un

        /// <summary>On parle à un habitué, sans que l'histoire l'attende : il répond selon ce qu'il sait.</summary>
        private IEnumerator Visit(string id)
        {
            if (!IsCalm) yield break;

            switch (id)
            {
                case "MAMAN": yield return VisitMom(); break;
                case "RAY": yield return VisitRay(); break;
                case "LINA": yield return VisitLina(); break;
                case "JEFF": yield return VisitJeff(); break;
                case "KARIM": yield return VisitKarim(); break;
                case "CHEN": yield return VisitChen(); break;
                case "TONY":
                    yield return Lines(Done("fin:atteinte")
                        ? L("TONY", "Le héros du quartier ! Assieds-toi, c'est offert. Enfin, la moitié.")
                        : L("TONY", "Tu viens pour une coupe ou pour un service ? Avec toi, on sait jamais."));
                    break;
                case "ROSA":
                    yield return Lines(Done("taureau:battu")
                        ? L("ROSA", "Le champion. Bois un verre. Et ne perds jamais chez moi : j'ai parié sur toi.")
                        : L("ROSA", "La Ligue t'attend. Un combat par soir, Marchal. Pas plus : je tiens à mes champions."));
                    break;
                case "TAUREAU":
                    yield return Lines(
                        L("LE TAUREAU", "Deux gamins et une femme qui travaille de nuit. Voilà pourquoi j'ai pris l'enveloppe, à l'époque."),
                        L("LE TAUREAU", "Toi, t'as rien pris. Et t'as tout perdu quand même. C'est pas juste."));
                    break;
                default:
                    yield return Lines(L("MOI", "…"));
                    break;
            }
        }

        /// <summary>Une visite par semaine à la laverie : ce qu'elle sait de toi change ce qu'elle dit.</summary>
        private IEnumerator VisitMom()
        {
            int week = (Day - 1) / 7 + 1;
            if (Count("maman:semaine") == week)
            {
                yield return Lines(L("MAMAN", "Encore toi ? Tu vas finir par me faire croire que tu t'ennuies. Reviens la semaine prochaine, mon grand."));
                yield break;
            }

            SetCount("maman:semaine", week);
            int visits = Count("maman:visites") + 1;
            SetCount("maman:visites", visits);

            List<DialogueLine> lines = new List<DialogueLine>();
            lines.Add(L("MAMAN", visits == 1 ? "Mon grand ! Tu es venu ! Attends, je finis de plier ça." : "Te voilà. J'ai gardé un café pour toi."));
            if (Done("fin:justicier")) lines.Add(L("MAMAN", "Tout le quartier parle de toi. Le patron de la supérette m'a demandé de revenir. J'ai dit non. Ça fait du bien, de dire non."));
            else if (_progress.Chapter >= Acte5 && Done("news:chasse")) lines.Add(L("MAMAN", "Ils disent que tu es dangereux, à la télé. Moi, je sais que tu as peur. C'est pas pareil."));
            else if (Done("news:motel") && visits <= 2) lines.Add(L("MAMAN", "La voisine dit qu'il y a eu une bagarre au motel. Tu n'étais pas là, hein ?"));
            else if (_progress.Arrests > 0) lines.Add(L("MAMAN", "On m'a dit que tu avais passé une nuit au poste. Je n'ai rien dit à ta tante."));
            else if (_progress.Code <= -30) lines.Add(L("MAMAN", "Tu as les mains abîmées. Et le regard de ton père, quand il rentrait tard. Ça me fait peur."));
            else if (_progress.Code >= 30) lines.Add(L("MAMAN", "Tu as l'air plus droit. Je sais pas ce que tu fais, mais tu as l'air plus droit."));
            else lines.Add(L("MAMAN", "Tu manges bien ? Tu dors ? Tu as maigri. Ne me mens pas, j'ai vu."));

            if (Done("taureau:battu")) lines.Add(L("MAMAN", "Ray dit que tu t'entraînes à nouveau. Tu fais attention à ta tête, promis ?"));
            if (_progress.Debt > 0 && visits == 1) lines.Add(L("MAMAN", "Et l'argent de l'hôpital… Tu m'avais dit que c'était une aide de l'État. C'était vrai ?"));
            yield return Lines(lines.ToArray());

            yield return Choose("MAMAN", "« Tu restes un peu ? »", "Lui laisser 200 €.", "L'embrasser, et y aller.");
            if (_answer == 0 && _progress.SendHome(200))
            {
                ChangeCode(2, "Maman");
                yield return Lines(L("MAMAN", "Il ne fallait pas… Merci, mon grand. Je le mets de côté pour toi."));
            }
            else
            {
                yield return Lines(L("MAMAN", "Va. Et mange quelque chose de chaud, pour une fois."));
            }
        }

        private IEnumerator VisitRay()
        {
            if (Done("a2:ray") && !Done("tournoi:oui"))
            {
                yield return Choose("COACH RAY", "« Le tournoi. Toujours pas le temps ? »", "J'en suis, coach.", "Toujours pas.");
                if (_answer == 0)
                {
                    Set("tournoi:oui");
                    SetCount("tournoi:jour", Day);
                    ChangeCode(3, "Le tournoi de Ray");
                    yield return Lines(L("RAY", "Voilà qui me plaît. Premier combat demain soir, 19 h."));
                }

                yield break;
            }

            if (Done("proches:distants") || _progress.Code <= -40)
            {
                yield return Lines(L("RAY", "Je t'ai appris à boxer. Pas à devenir ce que tu deviens. Va-t'en, Léo."));
                yield break;
            }

            string[] advice =
            {
                "Garde haute. Toujours. Même quand t'es fatigué. Surtout quand t'es fatigué.",
                "Un coup qui touche pas, c'est un coup qui fatigue l'autre. Bouge.",
                "Mange. Dors. Un boxeur affamé, c'est un boxeur qui tombe.",
                "Le plus dur, c'est pas de frapper. C'est de savoir quand arrêter."
            };
            yield return Lines(L("RAY", advice[Day % advice.Length]));
        }

        private IEnumerator VisitLina()
        {
            if (Done("proches:distants") || _progress.Code <= -40)
            {
                yield return Lines(L("LINA", "Je soigne tout le monde. Même toi. Mais ne me demande pas de sourire."));
            }
            else if (_progress.Code >= 30)
            {
                yield return Lines(L("LINA", "Tiens, le boxeur. Tu viens pour une blessure, ou pour me voir ? … Réponds pas."));
            }
            else
            {
                yield return Lines(L("LINA", "Montre tes mains. Hmm. T'as encore tapé dans quelque chose de dur."));
            }

            if (_progress.Injuries > 0)
            {
                _progress.HealInjuries(true);
                yield return Lines(L("LINA", "Voilà. Recousu, désinfecté. Gratuit, pour cette fois."));
            }
        }

        private IEnumerator VisitJeff()
        {
            yield return Choose("JEFF", "« Vingt balles l'info. »", "Payer (20 €).", "Pas aujourd'hui.");
            if (_answer != 0 || !_progress.Spend(20, "Jeff (une info)"))
            {
                yield return Lines(L("JEFF", "Pas d'argent, pas d'info. C'est le marché, mec."));
                yield break;
            }

            yield return Lines(L("JEFF", JeffTip()));
        }

        private string JeffTip()
        {
            int act = _progress.Chapter;
            if (act <= Acte1) return "Les courses à deux étoiles paient mieux. Mais les mecs cognent plus fort. Mange avant.";
            if (act == Acte2) return "Rosa triche jamais. Mais le Dentiste, lui, oui. Regarde sa main droite.";
            if (act == Acte3) return "Le coffre de Sarkis est dans son bureau, au fond du casino. Et ses vigiles adorent le café.";
            if (act == Acte4) return "Sami traîne au Bud's Bar le soir. Il regarde tout le temps derrière lui, depuis quelques jours.";
            if (act == Acte5) return "Si les flics te lâchent pas : change de tenue, change de voiture, planque-toi dans un magasin.";
            return "Plus rien à vendre. La ville est calme. C'est presque ennuyeux.";
        }

        private IEnumerator VisitKarim()
        {
            if (Count("karim:repas") != Day)
            {
                SetCount("karim:repas", Day);
                _progress.AddMeals(1);
                yield return Lines(L("KARIM", "Un taco pour la route. Et un pour ton frigo. T'as une tête à sauter des repas."));
            }
            else
            {
                yield return Lines(L("KARIM", "Les Kovac passent plus devant le resto depuis que tu t'en es mêlé. Merci, mec."));
            }
        }

        private IEnumerator VisitChen()
        {
            if (!Done("chen:aide"))
            {
                yield return Lines(
                    L("M. CHEN", "Vous êtes le jeune homme qui a battu Dragan Kovac ?"),
                    L("M. CHEN", "Tous les jeudis, ses hommes viennent chercher « l'enveloppe ». Trois cents euros. Je n'ai plus rien."),
                    L("M. CHEN", "Vingt ans que je fais des raviolis dans ce quartier. Je ne veux pas partir."));
                yield return Choose("M. CHEN", "« Est-ce que vous pouvez… leur parler ? »", "Je m'en occupe.", "Pas mes affaires.");
                if (_answer == 0)
                {
                    Set("chen:aide");
                    yield return Lines(L("M. CHEN", "Merci. Ils viennent le soir, après 19 h. Devant le restaurant."));
                }
                else
                {
                    yield return Lines(L("M. CHEN", "Je comprends. Tout le monde a ses affaires."));
                }

                yield break;
            }

            if (!Done("chen:fini"))
            {
                yield return Lines(L("M. CHEN", "Le soir, après 19 h. Devant le restaurant. Soyez prudent."));
                yield break;
            }

            if (Count("chen:repas") != Day)
            {
                SetCount("chen:repas", Day);
                _progress.Eat(35f);
                yield return Lines(L("M. CHEN", "Asseyez-vous, monsieur Léo. Des raviolis. Pour vous, c'est toujours gratuit."));
            }
            else
            {
                yield return Lines(L("M. CHEN", "Encore faim ? Revenez demain. Un boxeur doit rester léger."));
            }
        }

        // ------------------------------------------------------------------ outils de test : sauter à un acte

        /// <summary>Comme si l'on avait joué jusque-là (les choix du Justicier, l'argent et l'expérience d'alors).</summary>
        private void SkipTo(int act)
        {
            string[][] steps =
            {
                new[] { "p:reveil", "p:courrier", "p:appel", "p:appli", "p:moretti", "p:ray", "p:nestor", "news:moretti", "mail:premiere" },
                new[]
                {
                    "a1:debut", "a1:courses", "a1:karim", "karim:allie", "a1:jeff", "a1:dragan", "a1:lina", "a1:milan", "milan:rancune",
                    "a1:duval", "a1:fin", "news:kovac", "news:motel", "news:agressions"
                },
                new[]
                {
                    "a2:invitation", "a2:test", "a2:ceinture", "ceinture:vue", "a2:ray", "tournoi:oui", "a2:taureau", "taureau:battu",
                    "dentiste:rancune", "a2:archives", "a2:fin", "news:archive", "news:parking", "news:fosse"
                },
                new[]
                {
                    "a3:rdv", "sarkis:refuse", "a3:plan", "casse:uniforme", "casse:rondes", "casse:porte", "a3:casse", "a3:sarkis", "a3:nom",
                    "a3:carnet", "carnet:duval", "a3:suite", "news:casino", "news:carnet_duval"
                },
                new[] { "a4:doute", "a4:filature", "a4:verite", "a4:choix", "sami:pardon", "a4:jeff", "milan:battu2", "a4:serveurs", "news:chantier", "mail:serveurs" }
            };

            for (int a = 0; a < act && a < steps.Length; a++)
            {
                for (int i = 0; i < steps[a].Length; i++) _progress.SetFlag(steps[a][i]);
            }

            int[] experience = { 0, 450, 1500, 2900, 4300, 5800 };
            int[] money = { 0, 700, 1600, 3200, 4600, 6000 };
            int[] reputation = { 0, 18, 32, 45, 55, 62 };
            int[] code = { 0, 10, 14, 32, 45, 50 };
            _progress.AddExperience(experience[act]);
            _progress.AddMoney(money[act], "Économies");
            _progress.ChangeReputation(reputation[act], "Reprise");
            _progress.ChangeCode(code[act], "Reprise");

            _progress.SetCounter("nestor:echeance", _progress.Day + 7);
            _progress.SetCounter("nestor:verse", 1500 * Mathf.Max(0, act - 1));
            _progress.SetCounter("nestor:rang", Mathf.Max(0, act - 1));
            _progress.SetDebt(12000 - 1500 * Mathf.Max(0, act - 1));
            if (act > Acte1) _progress.SetCounter("duval:confiance", act > Acte3 ? 3 : 1);
            if (act > Acte2) _progress.SetCounter("ligue", LeagueRoles.Length);
            _progress.SetChapter(act);
        }
    }
}
