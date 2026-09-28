using System.Collections;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// ACTE 4 — « Le nom » (jours 24 à 28). Sami fait comme si de rien n'était ; tu le suis
    /// jusqu'aux docks ; il craque (Inès, vingt mille euros, l'appli qu'on lui a prise). Le
    /// choix : lui pardonner, le frapper (et voir Inès), ou le livrer à Rosa. Milan, rancunier,
    /// enlève Jeff : l'usine du chantier. Et dans les serveurs de l'appli, le compte
    /// administrateur : a.holt@mairie-hyland.fr.
    /// </summary>
    public partial class OpenWorldStory
    {
        private bool _tailOk;

        private IEnumerator Acte4Line()
        {
            // --- 1. le doute
            if (!Done("a4:doute"))
            {
                yield return new WaitForSeconds(8f);
                yield return Calm();
                yield return Texts("SAMI",
                    "frère t'as disparu !",
                    "bière ce soir au bud's bar ? c moi qui paie",
                    "j'ai une surprise pour toi tu vas kiffer");
                yield return UntilHour(19f, "Le Bud's Bar, ce soir : Sami t'attend. Il fait comme si de rien n'était.");
                Put("SAMI", "budsbar");
                yield return TalkTo("SAMI", "budsbar", "Le Bud's Bar : Sami t'attend.");
                yield return Lines(
                    L("SAMI", "Frère ! Le champion de la Fosse ! Tu veux une bière ?"),
                    L("MOI", "… Ça va, Sami ?"),
                    L("SAMI", "Moi ? Tranquille. Pourquoi ?"),
                    L("MOI", "Pour rien. Inès, ça va ?"),
                    L("SAMI", "(Il regarde son téléphone. Il ne te regarde plus.) Ah, mince. Faut que j'y aille. Un truc pour l'appli."),
                    L("SAMI", "On se voit, hein ? Je t'appelle."),
                    L("MOI", "(Il ment. Il a toujours mal menti.)"));
                Set("a4:doute");
            }

            // --- 2. la filature, jusqu'aux docks
            if (!Done("a4:filature"))
            {
                bool retry = false;
                while (true)
                {
                    StoryActor sami = _cast != null ? _cast.Actor("SAMI") : null;
                    if (retry || (sami != null && !sami.Placed))
                    {
                        if (retry) yield return UntilNextDay(Day, "Sami t'a semé. Il repassera au Bud's Bar demain soir.");
                        yield return UntilHour(19f, null);
                        Put("SAMI", "budsbar");
                        yield return Reach("budsbar", 25f, "Le Bud's Bar : Sami y repasse ce soir. Suis-le, de loin.");
                    }

                    yield return Lines(L("MOI", "Il file. Je le suis. Pas trop près."));
                    yield return Tail();
                    if (_tailOk) break;
                    retry = true;
                }

                Set("a4:filature");
            }

            // --- 3. la vérité, aux docks
            if (!Done("a4:verite"))
            {
                Put("SAMI", "docks");
                yield return Reach("docks", 7f, "Les docks : Sami s'est arrêté devant l'entrepôt.");
                yield return Calm();
                yield return Lines(
                    L("SAMI", "(Il se retourne.) Je savais que tu me suivais. T'as jamais su être discret."),
                    L("MOI", "Sarkis m'a donné un nom."),
                    L("SAMI", "…"),
                    L("SAMI", "Inès allait mourir, Léo. Son cœur. L'opération, c'était quarante mille. On les avait pas."),
                    L("SAMI", "Ils m'ont donné vingt mille balles pour mettre un truc dans ta bouteille. Vingt mille. Ta carrière, pour vingt mille."),
                    L("SAMI", "J'ai vendu ta ceinture pour le reste. Et Inès s'est fait opérer."),
                    L("SAMI", "J'ai créé l'appli pour te rendre ton argent. Pour que tu gagnes ce qu'ils t'avaient pris."),
                    L("SAMI", "Et ils me l'ont prise. L'appli. Tout. Maintenant elle sert à tabasser des gens pour quelqu'un que j'ai jamais vu."),
                    L("SAMI", "Vas-y. Dis quelque chose."));
                Set("a4:verite");
            }

            // --- 4. le choix
            if (!Done("a4:choix"))
            {
                yield return Choose("SAMI", "Sami, sur le quai, qui attend. Qu'est-ce que tu fais ?",
                    "Lui pardonner.", "Le frapper.", "Le livrer à Rosa.");

                if (_answer == 0)
                {
                    Set("sami:pardon");
                    ChangeCode(15, "Tu as pardonné à Sami");
                    yield return Lines(
                        L("MOI", "T'es un idiot, Sami. Un idiot qui aime sa sœur."),
                        L("MOI", "J'aurais peut-être fait pareil. Peut-être."),
                        L("SAMI", "… Merci. Je te revaudrai ça. Toute ma vie, s'il faut."),
                        L("SAMI", "J'ai encore un accès aux serveurs. Un vieux mot de passe. Ils l'ont jamais changé."));
                    Free("SAMI");
                }
                else if (_answer == 1)
                {
                    Free("SAMI");
                    OpenWorldDirector.StoryContract fight = Job("sami", "sami:docks", "docks");
                    fight.Direct = true;
                    fight.NoPhoto = true;
                    fight.Client = "TOI";
                    fight.Reward = 0;
                    fight.Experience = 500;
                    fight.Lines = new[]
                    {
                        L("SAMI", "Vas-y. Je le mérite."),
                        L("SAMI", "Mais je vais pas me laisser faire. T'as jamais aimé les combats faciles.")
                    };
                    yield return Fight(fight, null, "Il est parti en courant. Il reviendra aux docks, il a nulle part où aller.");
                    Set("sami:frappe");
                    ChangeCode(-10, "Tu as frappé Sami");
                    yield return Lines(L("MOI", "Trois ans que j'attendais ça. Ça fait pas du bien. Pas du tout."));
                    yield return InesScene();
                }
                else
                {
                    Set("sami:livre");
                    ChangeCode(-25, "Tu as livré Sami");
                    Free("SAMI");
                    yield return Call("ROSA",
                        L("MOI", "Rosa. J'ai quelqu'un pour toi. Celui qui a truqué mon combat."),
                        L("ROSA", "… Tu es sûr ? Chez moi, on ne revient pas en arrière."),
                        L("MOI", "Les docks. Maintenant."),
                        L("ROSA", "Bien. Rentre chez toi, champion. Tu n'as rien vu."));
                    yield return new WaitForSeconds(30f);
                    News("disparition");
                }

                Set("a4:choix");
            }

            // --- 5. la vengeance de Milan : Jeff, à l'usine du chantier
            if (!Done("a4:jeff") && Done("milan:rancune")) yield return RescueJeff();

            // --- 6. les serveurs de l'appli
            if (!Done("a4:serveurs"))
            {
                yield return Calm();
                if (Done("sami:pardon"))
                {
                    yield return Call("SAMI",
                        L("SAMI", "Léo. Je suis entré. Les serveurs, les journaux, tout."),
                        L("SAMI", "Ouvre ta boîte mail. Assieds-toi avant."));
                }
                else
                {
                    yield return Texts("JEFF",
                        "le tel de milan que t'as gardé",
                        "l'appli est en mode admin dessus. j'ai fouillé les serveurs",
                        "regarde tes mails. assieds toi avant");
                }

                SendMail("serveurs", true);
                Goal("L'ordinateur (au motel) : le mail sur les serveurs de l'appli.");
                while (!MailOpened("serveurs")) yield return null;
                Goal(string.Empty);
                yield return new WaitForSeconds(1f);
                yield return Lines(
                    L("MOI", "a.holt@mairie-hyland.fr."),
                    L("MOI", "Arthur Holt. Le maire. Le « H. » du carnet."),
                    L("MOI", "L'appli, c'est la sienne. Il s'en sert pour faire taire ceux qui le gênent, et vider les quartiers qu'il veut raser."),
                    L("MOI", "Et la prochaine sur la liste, c'est Mme Keller. Celle qui se présente contre lui."));
                Set("a4:serveurs");
            }

            Advance(Acte5);
        }

        private bool MailOpened(string id)
        {
            string from, subject, body;
            if (!MailText(id, out from, out subject, out body)) return true;
            ComputerScreen computer = _computer != null ? _computer : ComputerScreen.Instance;
            return computer == null || computer.MailRead(from, subject);
        }

        /// <summary>
        /// La filature : Sami marche du Bud's Bar aux docks par les trottoirs. Trop loin plus de
        /// dix secondes, on le perd ; trop près plus de deux secondes et demie, il se retourne.
        /// </summary>
        private IEnumerator Tail()
        {
            _tailOk = false;
            StoryActor sami = _cast != null ? _cast.Actor("SAMI") : null;
            StoryCast.Place docks = _cast != null ? _cast.Where("docks") : null;
            if (sami == null || docks == null || sami.Walker == null)
            {
                _tailOk = true;
                yield break;
            }

            _reserved.Add("SAMI");
            sami.SetTalkable(false);
            Vector3[] path = _cast.PathBetween(sami.transform.position, docks.position);
            if (!sami.Walk(path, 1.35f))
            {
                _tailOk = true;
                yield break;
            }

            if (_director != null) _director.Paused = true;
            float far = 0f;
            float near = 0f;
            while (!sami.Walker.Arrived)
            {
                float d = Flat(PlayerPosition - sami.transform.position);
                Goal("Suis Sami sans te faire repérer : reste entre 8 et 50 m.   " + Mathf.RoundToInt(d) + " m");
                if (_map != null) _map.SetWaypoint(sami.transform.position, "SAMI", sami.transform);

                far = d > 50f ? far + Time.deltaTime : 0f;
                near = d < 7f ? near + Time.deltaTime : 0f;
                if (far > 10f || near > 2.5f)
                {
                    Unmark();
                    Goal(string.Empty);
                    if (_director != null) _director.Paused = false;
                    yield return Lines(near > 2.5f
                        ? new[] { L("SAMI", "(Il se retourne.) Léo ? Qu'est-ce que tu fais là ?"), L("MOI", "Rien. Je passais."), L("SAMI", "… OK. Salut.") }
                        : new[] { L("MOI", "Perdu. Il connaît le quartier mieux que moi.") });
                    Free("SAMI");
                    yield break;
                }

                yield return null;
            }

            Unmark();
            Goal(string.Empty);
            if (_director != null) _director.Paused = false;
            Put("SAMI", "docks");
            sami.SetTalkable(false);
            _tailOk = true;
        }

        private IEnumerator InesScene()
        {
            yield return new WaitForSeconds(25f);
            yield return Calm();
            yield return Texts("INÈS",
                "c'est inès. la sœur de sami",
                "il est au centre médical. il te demande rien. moi je te demande. tu peux venir ?");
            Put("INES", "medical_ext");
            yield return TalkTo("INES", "medical_ext", "Le centre médical : Inès t'attend dehors.");
            yield return Lines(
                L("INÈS", "Tu es Léo ? Sami parle tout le temps de toi. Tout le temps."),
                L("INÈS", "Il dit que tu étais le meilleur boxeur de la ville. Qu'un jour tu remonterais sur un ring."),
                L("INÈS", "Il pleure la nuit, tu sais. Il croit que je l'entends pas."),
                L("INÈS", "Je sais pas ce qu'il t'a fait. Mais moi, je suis vivante. Alors je peux pas le détester."),
                L("MOI", "…"));
            ChangeCode(3, "Inès");
            Set("ines:vue");
            yield return new WaitForSeconds(3f);
            Free("INES");
        }

        private IEnumerator RescueJeff()
        {
            yield return new WaitForSeconds(20f);
            yield return Calm();
            yield return Texts("JEFF",
                "c milan.",
                "on a ton petit skateur. l'usine du chantier. viens seul",
                "et viens vite. il parle trop");
            yield return Reach("chantier", 35f, "L'usine du chantier : Milan retient Jeff.");

            OpenWorldDirector.StoryContract milan = Job("milan", "milan:usine", "chantier");
            milan.Direct = true;
            milan.NoPhoto = true;
            milan.Client = "MILAN KOVAC";
            milan.Reward = 0;
            milan.Experience = 650;
            milan.Extras = Crew("kovac_gars", "GARS DES KOVAC", 3);
            milan.Tune = Style(0.4f, 0.25f, 0.1f, 0.05f, 3f, 0.85f);
            milan.Lines = new[]
            {
                L("MILAN KOVAC", "Tu m'as humilié devant mes gars. Devant mon frère. Devant tout le motel."),
                L("MILAN KOVAC", "Ce soir, pas d'appli, pas de Fosse. Juste toi et moi."),
                L("MOI", "Et eux trois."),
                L("MILAN KOVAC", "Et eux trois.")
            };
            yield return Fight(milan, null, "Ils ont déplacé Jeff… non, il est encore là. Faut y retourner, vite.");
            Set("milan:battu2");
            News("chantier");

            Put("JEFF", "chantier", new Vector3(2f, 0f, 2f), 200f);
            yield return TalkTo("JEFF", "chantier", "Libère Jeff.");
            yield return Lines(
                L("JEFF", "T'as mis le temps ! J'ai failli devoir me sauver tout seul. J'allais le faire, hein."),
                L("JEFF", "… Merci, Léo."),
                L("JEFF", "Au fait. Pendant qu'ils me gardaient, Milan a eu un appel. Il a dit « oui, monsieur le maire »."),
                L("JEFF", "Et il s'est arrêté net, comme s'il avait dit un gros mot."));
            Free("JEFF", false);
            Set("a4:jeff");
        }
    }
}
