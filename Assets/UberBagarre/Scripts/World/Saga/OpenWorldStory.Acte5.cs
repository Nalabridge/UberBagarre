using System.Collections;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.World
{
    /// <summary>
    /// ACTE 5 — « Le roi d'Hyland » (la dernière semaine). Mme Keller, la candidate adverse,
    /// escortée deux fois ; la chasse à l'homme de Brandt (recherché en permanence pendant deux
    /// jours) ; les alliés qui reviennent (la planque de Karim, la cave de M. Chen, Duval qui
    /// couvre, Rosa et le Taureau à la mairie) ; la nuit de l'élection : la police corrompue,
    /// Brandt dans le hall, et Holt qui négocie. Puis les fins : le Justicier, le Roi, le
    /// Fantôme, la Chute (trop de blessures, ou trois arrestations dans l'acte), et la fin
    /// secrète.
    /// </summary>
    public partial class OpenWorldStory
    {
        private const string FallKey = "UberBagarre.Chute";

        /// <summary>1 = la prison (trois arrestations), 2 = les funérailles (trop de blessures).</summary>
        private int _fall;

        /// <summary>Duval fait confiance : le carnet lui a été donné, ou un Code élevé et un casier propre.</summary>
        private bool DuvalAlly
        {
            get
            {
                if (_progress == null) return false;
                return Done("carnet:duval") || (Count("duval:confiance") >= 1 && _progress.Code >= 10 && _progress.RecordWeight < 6);
            }
        }

        private bool JusticierPossible
        {
            get { return _progress != null && _progress.Code >= 20 && Done("carnet:duval"); }
        }

        /// <summary>Toutes les histoires secondaires, champion de la Fosse, cinq étoiles d'avis — et Sami toujours là.</summary>
        private bool SecretEnding
        {
            get
            {
                if (_progress == null || Done("sami:livre")) return false;
                return Done("nestor:solde") && Done("chen:fini") && Done("tournoi:fini") && Done("ceinture:rachetee") &&
                       Count("maman:visites") >= 3 && Done("taureau:battu") && _progress.Rating >= 4.5f;
            }
        }

        private IEnumerator Acte5Line()
        {
            if (!Done("a5:debut"))
            {
                SetCount("a5:ko", _progress.KnockedOut);
                SetCount("a5:arrest", _progress.Arrests);
                PlayerPrefs.DeleteKey(FallKey);
                Set("a5:debut");
                News("election");
            }

            StartCoroutine(Downfall());

            // --- 1. la campagne : Mme Keller
            if (!Done("a5:campagne"))
            {
                yield return new WaitForSeconds(8f);
                if (DuvalAlly)
                {
                    yield return Call("DUVAL",
                        L("DUVAL", "Marchal. On m'a transféré un mail. Un certain compte « a.holt »."),
                        L("DUVAL", "Keller, la candidate. Il y a une commande cinq étoiles sur elle, et Brandt a retiré sa protection."),
                        L("DUVAL", "Je ne peux pas mettre mes hommes sur elle sans que Brandt le sache. Vous, si."),
                        L("DUVAL", "Elle fait campagne à pied, sur le parvis de la mairie. Allez-y."));
                }
                else
                {
                    yield return Call("JEFF",
                        L("JEFF", "Léo ! La Keller, celle qui se présente contre Holt. Y a une commande cinq étoiles sur elle."),
                        L("JEFF", "Personne la protège. Les flics, c'est Brandt. Et Brandt, c'est Holt."),
                        L("JEFF", "Elle fait campagne à pied, devant la mairie. Vas-y !"));
                }

                Set("a5:campagne");
            }

            if (!Done("a5:escorte1")) yield return Escort(1);
            if (!Done("a5:escorte2")) yield return Escort(2);

            // --- 2. la contre-attaque : la chasse à l'homme, deux jours
            if (!Done("a5:chasse")) yield return Manhunt();
            if (PoliceSystem.Instance != null) PoliceSystem.Instance.Floor = 0;

            // --- 3. la nuit de l'élection
            if (!Done("a5:hall"))
            {
                yield return UntilNight("La nuit de l'élection : la mairie. Tout se joue ce soir.");
                yield return Reach("tribunal", 35f, "La mairie, la nuit de l'élection.");
                yield return Calm();

                bool allies = Done("taureau:battu");
                if (allies)
                {
                    Put("TAUREAU", "tribunal", new Vector3(-2.5f, 0f, -3f), 20f);
                    Put("ROSA", "tribunal", new Vector3(2.5f, 0f, -3f), -20f);
                    yield return Lines(
                        L("LE TAUREAU", "On t'avait dit qu'on serait là."),
                        L("ROSA", "Chez moi, on paie ses dettes. Les flics de Brandt, c'est pour nous. Toi, va chercher le gros."));
                }

                if (DuvalAlly) Sms("DUVAL", "J'arrive avec mes hommes. Les vrais. Tenez vingt minutes.");

                OpenWorldDirector.StoryContract cops = Job("flic", "mairie:flics", "tribunal");
                cops.Spot = SpotAt("flic", "tribunal", null, new Vector3(0f, 0f, 6f));
                cops.Direct = true;
                cops.NoPhoto = true;
                cops.Client = "COMMISSAIRE BRANDT";
                cops.Reward = 0;
                cops.Experience = 600;
                cops.Extras = Crew("flic", "FLIC DE BRANDT", allies ? 1 : 3);
                cops.Lines = new[]
                {
                    L("FLIC DE BRANDT", "Marchal. Le commissaire a dit : vivant, si possible."),
                    L("MOI", "Et si c'est pas possible ?"),
                    L("FLIC DE BRANDT", "Il a pas précisé.")
                };
                yield return Fight(cops, null, "Ils tiennent le hall. Faut y retourner.");
                Set("a5:hall");
            }

            if (!Done("a5:brandt"))
            {
                OpenWorldDirector.StoryContract brandt = Job("brandt", "brandt", "tribunal");
                brandt.Spot = SpotAt("brandt", "tribunal", null, new Vector3(0f, 0f, 4f));
                brandt.Direct = true;
                brandt.NoPhoto = true;
                brandt.Client = "LE HALL DE LA MAIRIE";
                brandt.Reward = 0;
                brandt.Experience = 1200;
                brandt.Tune = Style(0.3f, 0.35f, 0.1f, 0.05f, 6f, 1f);
                brandt.Lines = new[]
                {
                    L("COMMISSAIRE BRANDT", "Marchal. J'ai boxé chez les flics pendant quinze ans. Champion de la police, trois fois."),
                    L("COMMISSAIRE BRANDT", "Toi, on t'a radié. Moi, on m'a décoré. C'est ça, la différence entre nous."),
                    L("MOI", "Non. La différence, c'est que moi, on m'a drogué pour que je perde.")
                };
                yield return Fight(brandt, null, "Brandt est encore debout. Relève-toi. Il connaît la boxe, toi aussi.");
                Set("a5:brandt");
            }

            // --- 4. Holt
            if (!Done("a5:holt"))
            {
                Put("HOLT", "tribunal", new Vector3(0f, 0f, 5f), 180f);
                yield return TalkTo("HOLT", "tribunal", "Arthur Holt t'attend, dans son bureau.");
                yield return Lines(
                    L("ARTHUR HOLT", "Monsieur Marchal. Enfin. Asseyez-vous, je vous en prie."),
                    L("ARTHUR HOLT", "Vous savez ce que c'est, une ville ? Des gens qui ont peur. Moi, je gère leur peur. Depuis douze ans."),
                    L("ARTHUR HOLT", "L'appli, c'est un outil. Des gens qui dérangent reçoivent une petite visite. Des quartiers se vident. La ville avance."),
                    L("ARTHUR HOLT", "Vous êtes bon. Meilleur que Brandt. Meilleur que Sarkis."),
                    L("ARTHUR HOLT", "Prends l'appli. Prends la ville. Tu seras moi, en plus jeune."));
                yield return Choose("ARTHUR HOLT", "« Alors ? »",
                    "Accepter.", "Refuser. C'est fini, Holt.", "Prendre l'argent du coffre, et partir.");
                SetCount("holt:choix", _answer + 1);
                Set("a5:holt");
            }

            int choice = Count("holt:choix") - 1;
            if (choice == 0) yield return EndKing();
            else if (choice == 1 && JusticierPossible) yield return EndJustice();
            else yield return EndGhost(choice == 1);

            Set("fin:atteinte");
            Free("HOLT");
            Free("TAUREAU");
            Free("ROSA");
            Advance(Fin);
            if (_progress != null) _progress.Save();
        }

        // ------------------------------------------------------------------ Mme Keller

        private IEnumerator Escort(int leg)
        {
            string from = leg == 1 ? "tribunal" : "poste";
            string to = leg == 1 ? "poste" : "tribunal";
            if (leg == 2) yield return UntilNextDay(Count("escorte:jour"), "Demain, Mme Keller retourne à la mairie. Repose-toi.");

            Put("KELLER", from);
            yield return TalkTo("KELLER", from, leg == 1 ? "Le parvis de la mairie : Mme Keller." : "Le bureau de poste : Mme Keller t'attend pour le retour.");
            if (leg == 1)
            {
                yield return Lines(
                    L("MME KELLER", "Vous êtes le garde du corps qu'on m'envoie ? Vous avez une drôle de tête de garde du corps."),
                    L("MOI", "On me le dit souvent."),
                    L("MME KELLER", "Je dois déposer mes listes à la poste. À pied. Je ne me cache pas, c'est tout ce qu'il me reste."));
            }
            else
            {
                yield return Lines(
                    L("MME KELLER", "Encore vous. Tant mieux."),
                    L("MME KELLER", "Un meeting à la mairie, devant les grilles. Holt va adorer."));
            }

            StoryActor keller = _cast != null ? _cast.Actor("KELLER") : null;
            StoryCast.Place dest = _cast != null ? _cast.Where(to) : null;
            if (keller == null || dest == null || keller.Walker == null)
            {
                FinishEscort(leg);
                yield break;
            }

            _reserved.Add("KELLER");
            keller.SetTalkable(false);
            Vector3[] path = _cast.PathBetween(keller.transform.position, dest.position);
            if (!keller.Walk(path, 1.15f))
            {
                FinishEscort(leg);
                yield break;
            }

            float length = 0f;
            for (int i = 1; i < path.Length; i++) length += Flat(path[i] - path[i - 1]);

            if (_director != null) _director.Paused = true;
            bool ambushed = false;
            float walked = 0f;
            Vector3 last = keller.transform.position;
            while (!keller.Walker.Arrived)
            {
                walked += Flat(keller.transform.position - last);
                last = keller.transform.position;

                float d = Flat(PlayerPosition - keller.transform.position);
                if (d > 30f)
                {
                    keller.Walker.HoldStill(0.5f);
                    Goal("Mme Keller t'attend : reste près d'elle (moins de 30 m).");
                }
                else
                {
                    Goal("Escorte Mme Keller jusqu'à " + (leg == 1 ? "la poste" : "la mairie") + ". Reste près d'elle.");
                }

                if (_map != null) _map.SetWaypoint(keller.transform.position, "MME KELLER", keller.transform);

                if (!ambushed && walked > length * 0.45f)
                {
                    ambushed = true;
                    keller.Walker.HoldStill(100000f);
                    Unmark();
                    Goal(string.Empty);

                    OpenWorldDirector.StoryContract hit = Job("casseur", "keller:" + leg, null);
                    hit.Spot = new OpenWorldDirector.Spot
                    {
                        name = "Sur le chemin de Mme Keller",
                        position = keller.transform.position + keller.transform.forward * 5f,
                        yaw = keller.transform.eulerAngles.y + 180f
                    };
                    hit.Direct = true;
                    hit.NoPhoto = true;
                    hit.Client = "COMMANDE CINQ ÉTOILES";
                    hit.Reward = 0;
                    hit.Experience = 380 + 120 * leg;
                    hit.Extras = Crew("casseur", "CASSEUR", leg);
                    hit.Lines = leg == 1
                        ? new[]
                        {
                            L("CASSEUR", "Madame Keller ? On a un message de la part de vos électeurs."),
                            L("MME KELLER", "Mes électeurs ne portent pas de cagoule."),
                            L("MOI", "Reculez, madame. Ça, c'est mon rayon.")
                        }
                        : new[]
                        {
                            L("CASSEUR", "Encore toi. On nous avait dit qu'elle serait seule."),
                            L("MOI", "On vous a mal renseignés.")
                        };
                    yield return Fight(hit, null, "Ils se sont repliés. Ils vont revenir. Reste avec elle.");
                    keller.Walker.Release();
                }

                yield return null;
            }

            Unmark();
            Goal(string.Empty);
            if (_director != null) _director.Paused = false;
            Put("KELLER", to);
            keller.SetTalkable(false);
            yield return Lines(leg == 1
                ? new[] { L("MME KELLER", "Merci, monsieur Marchal. Hyland mérite mieux que Holt."), L("MME KELLER", "Vous aussi, je crois.") }
                : new[] { L("MME KELLER", "Deux fois. Je vous dois deux fois."), L("MME KELLER", "Si je gagne dimanche, cette ville changera. Je vous le promets.") });
            yield return new WaitForSeconds(3f);
            Free("KELLER");
            FinishEscort(leg);
        }

        private void FinishEscort(int leg)
        {
            if (_director != null) _director.Paused = false;
            if (leg == 1)
            {
                News("keller");
                SetCount("escorte:jour", Day);
                Set("a5:escorte1");
            }
            else
            {
                Set("a5:escorte2");
            }
        }

        // ------------------------------------------------------------------ la chasse à l'homme

        private IEnumerator Manhunt()
        {
            yield return Calm();
            if (Count("chasse:fin") == 0)
            {
                SetCount("chasse:fin", Day + 2);
                News("chasse");
                yield return Lines(
                    L("MOI", "Mon nom à la télé. « Individu dangereux. »"),
                    L("MOI", "Brandt lâche toute la ville sur moi. Deux jours avant l'élection."));

                if (Done("karim:allie") || (Done("karim:frappe") && Done("casse:porte")))
                {
                    yield return Texts("KARIM", "t'es à la télé mec", "la réserve derrière le taco ticklers. personne te cherchera là. planque toi quand tu veux");
                }

                if (Done("chen:fini")) yield return Texts("M. CHEN", "Ma cave est à vous, monsieur Léo. Le Dragon d'Or n'a rien vu, rien entendu.");
                if (Done("taureau:battu")) yield return Texts("ROSA", "Le Taureau et moi, on sera à la mairie le soir de l'élection. Chez moi, on paie ses dettes. — R.");
                if (DuvalAlly)
                {
                    yield return Texts("DUVAL", "Je ne peux pas arrêter Brandt. Pas encore. Mais une fois par jour, je peux appeler le central. Tenez bon.");
                }
            }

            PoliceSystem police = PoliceSystem.Instance;
            if (police != null)
            {
                if (Done("karim:allie") || Done("casse:porte"))
                {
                    StoryCast.Place taco = _cast != null ? _cast.Where("taco") : null;
                    if (taco != null) police.AddHideout(taco.position);
                }

                if (Done("chen:fini"))
                {
                    StoryCast.Place chen = _cast != null ? _cast.Where(Pick("chen_ext", "chen")) : null;
                    if (chen != null) police.AddHideout(chen.position);
                }

                police.Floor = DuvalAlly ? 2 : 3;
            }

            float held = 0f;
            int end = Count("chasse:fin");
            while (Day < end || held < 240f)
            {
                held += Time.deltaTime;
                if (IsCalm)
                {
                    string hideouts = (Done("karim:allie") || Done("casse:porte") ? " Planque : le Taco Ticklers." : "") +
                                      (Done("chen:fini") ? " La cave de M. Chen." : "");
                    Goal(Day < end
                        ? "Chasse à l'homme : tiens jusqu'au jour " + end + " (encore " + (end - Day) + " j)." + hideouts
                        : "Chasse à l'homme : encore un peu. Ils vont lâcher avant l'élection." + hideouts);
                }

                // Duval couvre, une fois par jour.
                if (police != null && DuvalAlly && police.Stars >= 3 && Count("duval:couvert") != Day)
                {
                    SetCount("duval:couvert", Day);
                    police.Grace(150f, "Duval a appelé le central : on te lâche un moment.");
                    Sms("DUVAL", "Le central vous oublie quelques minutes. Profitez-en.");
                }

                yield return null;
            }

            Goal(string.Empty);
            if (police != null)
            {
                police.Floor = 0;
                police.Clear("Brandt rappelle ses hommes : ce soir, c'est l'élection.");
            }

            Set("a5:chasse");
        }

        // ------------------------------------------------------------------ la Chute

        /// <summary>Pendant l'acte 5 : trois arrestations, ou trop de blessures, et c'est la Chute.</summary>
        private IEnumerator Downfall()
        {
            while (_progress != null && _progress.Chapter == Acte5 && !Done("fin:atteinte"))
            {
                if (_progress.Arrests - Count("a5:arrest") >= 3) _fall = 1;
                else if (_progress.KnockedOut - Count("a5:ko") >= 3) _fall = 2;
                if (_fall != 0) yield break;
                yield return new WaitForSeconds(1f);
            }
        }

        /// <summary>Une mort dans l'acte 5 recharge la partie : on s'en souvient quand même.</summary>
        private void CountFallDeath()
        {
            if (_progress == null || _progress.Chapter != Acte5 || Done("fin:atteinte")) return;
            int deaths = PlayerPrefs.GetInt(FallKey, 0) + 1;
            PlayerPrefs.SetInt(FallKey, deaths);
            if (deaths >= 2) _fall = 2;
        }

        private void LateUpdate()
        {
            if (_fall == 0 || !_started) return;
            int kind = _fall;
            _fall = 0;
            StopAllCoroutines();
            StartCoroutine(Fall(kind));
        }

        private IEnumerator Fall(int kind)
        {
            PlayerPrefs.DeleteKey(FallKey);
            if (PoliceSystem.Instance != null) PoliceSystem.Instance.Floor = 0;
            News("chute");
            yield return Card(kind == 1
                ? "LA CHUTE\n\nTroisième arrestation. Cette fois, Brandt n'a laissé personne te sortir.\nPrison à vie.\n\nHolt a été réélu dimanche."
                : "LA CHUTE\n\nTon corps n'a pas tenu.\nAu cimetière d'Hyland : Ray, Lina, ta mère. Et la pluie.\n\nHolt a été réélu dimanche.", 7f);
            LoadingScreen.LoadScene(SceneManager.GetActiveScene().name, "ÜBER BAGARRE", "La Chute");
        }

        // ------------------------------------------------------------------ les fins

        private IEnumerator EndKing()
        {
            yield return Lines(
                L("ARTHUR HOLT", "Je savais que vous étiez raisonnable. Les gens raisonnables vivent vieux."),
                L("ARTHUR HOLT", "Le manoir sur la colline est à vous. Brandt ? On le remplacera."));
            Set("fin:roi");
            Set("proches:distants");
            _progress.AddMoney(50000, "Conseiller sécurité (mairie)");
            yield return Card("LE ROI\n\nTu reprends l'appli. Tu vis au manoir. La ville est à toi.\nRay ne te parle plus. Lina est partie.", 5.5f);
            News("roi");
            Teleport("manoir");
            yield return new WaitForSeconds(1f);
            yield return Lines(
                L("MOI", "La terrasse. Toute la ville en bas. Les néons du Vertigo, le motel, la salle de Ray."),
                L("MOI", "Personne à qui le dire."));
            yield return Card("ÜBER BAGARRE\n\nLe Roi.\n\nLa ville continue. Toi aussi. Seul.", 4.5f);
        }

        private IEnumerator EndGhost(bool refused)
        {
            if (refused)
            {
                yield return Lines(
                    L("ARTHUR HOLT", "Fini ? Sans carnet, sans témoin, sans flic honnête pour vous croire ?"),
                    L("ARTHUR HOLT", "Vous êtes un boxeur radié qui vient de saccager une mairie. Rien de plus."),
                    L("MOI", "Alors je prends ce que je peux."));
            }

            yield return Lines(L("MOI", "Le coffre. Des liasses. Assez pour partir. Loin."));
            Set("fin:fantome");
            _progress.AddMoney(30000, "Le coffre de la mairie");
            yield return Card("LE FANTÔME\n\nTu prends l'argent. Tu prends ta mère.\nVous quittez Hyland au lever du soleil.", 5.5f);
            if (WorldClock.Instance != null) WorldClock.Instance.SetHour(6f);
            News("fantome");
            Teleport("motel");
            Put("MAMAN", "motel", new Vector3(1.2f, 0f, 2f), 180f);
            yield return new WaitForSeconds(1.5f);
            yield return Lines(
                L("MAMAN", "On va où, mon grand ?"),
                L("MOI", "Loin. Là où personne nous connaît."),
                L("MAMAN", "… Tu as mangé, au moins ?"));
            yield return Card("ÜBER BAGARRE\n\nLe Fantôme.\n\nQuelque part au nord, un lever de soleil.", 4.5f);
            Free("MAMAN");
        }

        private IEnumerator EndJustice()
        {
            Put("DUVAL", "tribunal", new Vector3(1.6f, 0f, 3f), 180f);
            yield return Lines(
                L("MOI", "Non. C'est fini, Holt."),
                L("DUVAL", "Il a raison, monsieur le maire."),
                L("DUVAL", "Arthur Holt, vous êtes en état d'arrestation. Le carnet de Sarkis, les serveurs de l'appli, les témoins : tout est au dossier."),
                L("ARTHUR HOLT", "Vous ne savez pas à qui vous parlez."),
                L("DUVAL", "Si. À un homme qui va passer beaucoup de temps assis."));
            Set("fin:justicier");
            Set("appli:fermee");
            yield return Card("La nuit de l'élection.\n\nArthur Holt et le commissaire Brandt sont arrêtés.\nHélène Keller est élue. Über Bagarre est fermée.", 5f);
            Free("DUVAL");
            Free("HOLT");
            News("holt");
            News("appli");

            // Le match officiel : la radiation est annulée.
            yield return new WaitForSeconds(8f);
            News("radiation");
            yield return Call("RAY",
                L("RAY", "Léo. La fédération a appelé. Ta radiation est annulée."),
                L("RAY", "Un match officiel. Demain soir, à la salle. Le centre social n'est plus un parking."),
                L("RAY", "Je serai dans ton coin. Comme avant."));
            int today = Day;
            yield return UntilNextDay(today, "Dors. Demain soir, ton premier match officiel depuis trois ans.");
            yield return UntilHour(19f, "Ce soir : le match officiel, à la salle de Ray.");

            Put("RAY", "salle", new Vector3(-1.2f, 0f, 0.5f), 20f);
            Put("MAMAN", "salle", new Vector3(1.5f, 0f, -1f), -20f);
            Put("LINA", "salle", new Vector3(2.4f, 0f, -0.6f), -30f);
            yield return Reach("salle", 6f, "La salle de Ray : ton match officiel.");
            yield return Lines(
                L("RAY", "Garde haute. Tu respires. Et tu fais ce que tu sais faire."),
                L("MAMAN", "Mon grand ! Je suis là ! Au premier rang !"),
                L("LINA", "Évite le visage. J'ai encore des points à te faire, sinon."));

            OpenWorldDirector.StoryContract champion = Job("champion", "officiel", "salle");
            champion.Spot = SpotAt("champion", "salle", "sentraine", new Vector3(-1.5f, 0f, 3f));
            champion.Direct = true;
            champion.NoPhoto = true;
            champion.Client = "FÉDÉRATION RÉGIONALE";
            champion.Reward = 5000;
            champion.Experience = 1500;
            champion.Tune = Style(0.3f, 0.2f, 0.15f, 0.1f, 4f, 1f);
            champion.Lines = new[]
            {
                L("ÉRIC VANCE", "Le fameux Marchal. On m'a dit que t'étais une légende."),
                L("MOI", "On t'a mal renseigné. Je suis juste un gars des Slums."),
                L("RAY", "Boxe !")
            };
            yield return Fight(champion, null, "Relève-toi. Ray est dans ton coin. On recommence.");
            News("officiel");
            yield return Lines(
                L("RAY", "Champion. Le vrai, cette fois."),
                L("MAMAN", "Il a toujours été le meilleur. Il mange mal, mais il est le meilleur."),
                L("LINA", "T'as encore abîmé mon travail. … Bravo, Léo."));

            if (SecretEnding)
            {
                Set("fin:secrete");
                Put("SAMI", "salle", new Vector3(3.4f, 0f, -2.4f), -40f);
                Put("INES", "salle", new Vector3(4.2f, 0f, -2f), -40f);
                yield return new WaitForSeconds(1.5f);
                yield return Lines(
                    L("MOI", "Au fond de la salle. Une casquette que je connais par cœur."),
                    L("SAMI", "… Salut, frère."),
                    L("INÈS", "Je suis guérie, Léo. Complètement. Il voulait que tu le saches."),
                    L("SAMI", "Je te demande rien. Je voulais juste… te voir gagner. Pour de vrai, cette fois."));
            }

            yield return Card(Done("fin:secrete")
                ? "ÜBER BAGARRE\n\nLe Justicier.\n\nTout le monde était là. Même ceux qu'on n'attendait plus."
                : "ÜBER BAGARRE\n\nLe Justicier.\n\nHyland respire. La ville, elle, continue.", 5f);
            Free("RAY", false);
            Free("MAMAN");
            Free("LINA", false);
            Free("SAMI");
            Free("INES");
        }

        /// <summary>Emmène le joueur à un lieu de l'histoire (une fin).</summary>
        private void Teleport(string place)
        {
            StoryCast.Place p = _cast != null ? _cast.Where(place) : null;
            if (p == null || _director == null) return;
            // Le sol sous le lieu (le manoir est sur la colline : sa hauteur n'est pas connue d'avance).
            Vector3 at = p.position;
            RaycastHit hit;
            if (Physics.Raycast(at + Vector3.up * 60f, Vector3.down, out hit, 160f, ~0, QueryTriggerInteraction.Ignore)) at = hit.point;

            GameObject spot = new GameObject("Arrivée (histoire)");
            spot.transform.SetPositionAndRotation(at + Vector3.up * 0.1f, Quaternion.Euler(0f, p.yaw, 0f));
            _director.RestoreAfterArrest(spot.transform);
            Destroy(spot, 1f);
        }
    }
}
