using System;
using System.Collections;
using UberBagarre.Combat;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// PROLOGUE — « Chambre 3 » (jours 1 et 2). Le réveil au motel, le courrier (le loyer, la
    /// lettre de Nestor, la carte postale de maman), l'appel de Sami et l'appli installée à la
    /// main, Bruno Moretti devant le Vertigo sous la pluie, Coach Ray à la salle, et le premier
    /// soir : Nestor et ses deux gros bras devant le motel. Sept jours pour 1 500 €.
    /// </summary>
    public partial class OpenWorldStory
    {
        private bool _lettersOpened;

        private IEnumerator PrologueLine()
        {
            // --- 1. le réveil
            if (!Done("p:reveil"))
            {
                Set("p:reveil");
                yield return Lines(
                    L("MOI", "Trois loyers de retard. Et le chauffage qui fait semblant."),
                    L("MOI", "Le courrier est arrivé. Autant savoir tout de suite."));
            }

            // --- 2. le courrier : on l'ouvre soi-même, sur le bureau
            if (!Done("p:courrier"))
            {
                Goal("Lis le courrier, sur le bureau.");
                while (!_lettersOpened) yield return null;
                Goal(string.Empty);

                if (_letterReader != null)
                {
                    bool closed = false;
                    _letterReader.Open(delegate { closed = true; });
                    while (!closed) yield return null;
                }

                Set("p:courrier");
                yield return Lines(
                    L("MOI", "Le loyer. Nestor. Et maman."),
                    L("MOI", "« Mon grand, j'espère que tu manges. » Si elle savait."),
                    L("MOI", "Douze mille euros. Pour l'hôpital de maman. Il a été très poli, Nestor. C'est ça qui fait peur."));
                yield return new WaitForSeconds(1.5f);
            }

            // --- 3. Sami appelle
            if (!Done("p:appel"))
            {
                yield return Call("SAMI",
                    L("SAMI", "Frère ! T'es réveillé ? Tant pis, je parle quand même."),
                    L("SAMI", "J'ai un truc pour toi. Une appli. Über Bagarre."),
                    L("SAMI", "Les gens commandent une bagarre, toi tu la livres. Tu tapes celui qu'on te dit, une photo, t'es payé le soir même."),
                    L("MOI", "Sami. C'est illégal."),
                    L("SAMI", "Évidemment. Elle est sur aucun magasin. Je t'envoie le lien."),
                    L("SAMI", "T'inquiète frère, c'est juste une appli. Tu cognes, tu encaisses, t'es payé."),
                    L("MOI", "..."),
                    L("SAMI", "Douze mille à Nestor, Léo. Réfléchis pas trop."));
                Set("p:appel");
            }

            // --- 4. le lien, l'installation : c'est le joueur qui le fait
            if (!Done("p:appli")) yield return InstallApp();

            // --- 5. la première nuit : Moretti, sous la pluie et les néons du Vertigo
            if (!Done("p:moretti")) yield return FirstNight();

            // --- 6. le lendemain : Coach Ray
            if (!Done("p:ray")) yield return MeetRay();

            // --- 7. le premier soir : Nestor
            if (!Done("p:nestor")) yield return NestorVisit();

            Advance(Acte1);
        }

        /// <summary>
        /// Le lien de Sami, dans Messages : c'est le joueur qui l'ouvre et qui installe. La barre
        /// de téléchargement se remplit, puis il lance l'appli lui-même depuis l'accueil.
        /// </summary>
        private IEnumerator InstallApp()
        {
            _linkConfirmed = false;

            if (_phone != null)
            {
                _phone.HangUp();
                _phone.AppInstalled = false;
                _phone.DownloadProgress = 0f;
                _phone.SetScreen(PhoneDevice.Screen.Lien);
                Goal("Téléphone (" + _phone.PhoneKeyName.ToUpperInvariant() + ") : ouvre Messages, le lien de Sami, et installe l'appli.");
                while (!_linkConfirmed) yield return null;

                _phone.SetScreen(PhoneDevice.Screen.Installation);
                Goal("Installation…");
                _installing = true;
                Play(L("MOI", "Interdite de diffusion. Évidemment."));
                while (_phone.DownloadProgress < 1f) yield return null;
                _installing = false;

                _phone.AppInstalled = true;
                _phone.SetScreen(PhoneDevice.Screen.Verrouille);
                Goal("Ouvre Über Bagarre depuis l'accueil du téléphone.");
            }

            Set("p:appli");

            // Personne n'ouvre l'appli à la place du joueur : elle se présente quand il la lance.
            float waited = 0f;
            while (_phone != null && !(_phone.IsRaised && _phone.ShowingStoryScreen) && waited < 90f)
            {
                waited += Time.deltaTime;
                yield return null;
            }

            yield return Lines(
                L("APPLI", "Une course, un contrat. De une à cinq étoiles."),
                L("APPLI", "Une étoile, c'est pour apprendre. Cinq, c'est pour finir à l'hôpital."));
        }

        /// <summary>Moretti sort fumer vers 23 h : la première course se fait de nuit, sous la pluie.</summary>
        private IEnumerator FirstNight()
        {
            yield return UntilNight("Moretti finit son service vers 23 h. D'ici là, fais un tour en ville… ou une sieste au motel (le lit).");

            if (Weather.Instance != null) Weather.Instance.Force(0.45f, 5f);
            yield return Texts("SAMI",
                "moretti finit à 23h. il sort fumer par la porte de devant, toujours",
                "il pleut. tant mieux, personne traîne dehors. vas-y");

            OpenWorldDirector.StoryContract moretti = Job("moretti", "moretti", null);
            moretti.Reward = 150;
            moretti.Experience = 150;
            moretti.Intro = "Une étoile, pour commencer. Tu le reconnaîtras : blouson noir, crâne rasé.";
            moretti.Lines = new[]
            {
                L("BRUNO MORETTI", "Qu'est-ce que tu me veux, toi ?"),
                L("MOI", "Bruno Moretti ?"),
                L("BRUNO MORETTI", "Ça dépend. Qui demande ?"),
                L("MOI", "Personne. Quelqu'un a payé pour qu'on se parle."),
                L("BRUNO MORETTI", "Ah. T'es un de ceux-là. J'ai fini mon service… mais pour toi, je fais une heure sup'.")
            };
            yield return Fight(moretti, "Première course : sors le téléphone (" + PhoneKey + ") et accepte-la dans l'appli.");

            Set("p:moretti");
            SetCount("p:moretti_jour", Day);
            if (_director != null) _director.Paused = false;
            News("moretti");
            SendMail("premiere", true);

            yield return Lines(
                L("MOI", "Cent cinquante euros."),
                L("MOI", "Nestor en veut douze mille. Faudra en faire d'autres."));
            yield return Texts("SAMI", "t'as vu ? facile", "rentre dormir. demain y a quelqu'un qui veut te voir");
        }

        private string PhoneKey
        {
            get { return _phone != null ? _phone.PhoneKeyName.ToUpperInvariant() : "T"; }
        }

        /// <summary>Le lendemain : Ray appelle, la salle, les bases (la garde, l'esquive, un sparring).</summary>
        private IEnumerator MeetRay()
        {
            yield return UntilNextDay(Count("p:moretti_jour"), "Rentre dormir au motel (le lit).");
            yield return new WaitForSeconds(4f);

            yield return Call("RAY",
                L("RAY", "Marchal. C'est Ray."),
                L("RAY", "Sami m'a dit que tu remontais sur le ring. Enfin… sur le trottoir."),
                L("MOI", "Coach…"),
                L("RAY", "Passe à la salle. Aujourd'hui. Le Boxe Club, au centre social. Et viens à jeun, je veux pas nettoyer."));

            Put("RAY", "salle");
            yield return TalkTo("RAY", "salle", "La salle de Ray : le Boxe Club, au centre social.");
            yield return Lines(
                L("RAY", "Je t'ai vu à la télé. T'as l'air d'un chien battu."),
                L("MOI", "Merci, coach. Toi, t'as pas changé."),
                L("RAY", "Trois ans que t'as pas mis les pieds ici. Trois ans que tu te caches."),
                L("RAY", "La rue t'apprend à frapper. Moi je t'apprends à pas te faire tuer. On reprend les bases."));

            yield return Drills();

            yield return Lines(
                L("RAY", "Bon. T'as pas tout oublié."),
                L("RAY", "Ce qui s'est passé il y a trois ans… J'ai jamais cru que t'avais triché. Jamais."),
                L("RAY", "Reviens quand tu veux. La porte est ouverte."));
            Free("RAY", false);
            Set("p:ray");
            Sms("RAY", "La porte est ouverte. 6 h – 22 h. Pas d'excuse.");
        }

        /// <summary>Les exercices de Ray : la garde tenue, trois esquives, puis un sparring contre Petit Louis.</summary>
        private IEnumerator Drills()
        {
            GuardSystem guard = _playerTransform != null ? _playerTransform.GetComponent<GuardSystem>() : null;
            DodgeSystem dodge = _playerTransform != null ? _playerTransform.GetComponent<DodgeSystem>() : null;
            Core.InputBindings keys = _input != null ? _input.Bindings : null;
            CombatPresence presence = CombatPresence.Player;
            if (presence != null) presence.Forced = true;

            if (guard != null)
            {
                yield return Lines(L("RAY", "Garde haute. Les poings devant le menton. Et tu la tiens."));
                string key = keys != null ? keys.guard.ToString() : "Ctrl";
                float held = 0f;
                while (held < 3f)
                {
                    held = guard.IsGuarding ? held + Time.deltaTime : 0f;
                    Goal("Exercice de Ray : la garde (" + key + "), tiens-la trois secondes.  " + held.ToString("0.0") + " s");
                    yield return null;
                }

                Goal(string.Empty);
                yield return Lines(L("RAY", "Voilà. Ta tête, c'est ton gagne-pain. Tu la protèges."));
            }

            if (dodge != null)
            {
                yield return Lines(L("RAY", "Maintenant tu bouges. Un coup qui touche pas, c'est un coup qui fatigue l'autre."));
                string key = keys != null ? keys.dodge.ToString() : "Alt";
                int dodges = 0;
                Action<Vector3> counted = delegate { dodges++; };
                dodge.Dodged += counted;
                while (dodges < 3)
                {
                    Goal("Exercice de Ray : esquive (" + key + " + une direction) — " + dodges + " / 3");
                    yield return null;
                }

                dodge.Dodged -= counted;
                Goal(string.Empty);
                yield return Lines(L("RAY", "T'as encore des jambes. C'est déjà ça."));
            }

            if (presence != null) presence.Forced = false;

            // Le sparring : des enchaînements sur quelqu'un qui rend les coups.
            yield return Lines(
                L("RAY", "Et maintenant, des enchaînements. Sur quelqu'un qui cogne en retour."),
                L("RAY", "Petit Louis ! Viens par là. Tu vas faire le sac."));

            OpenWorldDirector.StoryContract sparring = Job("recrue", "ray:sparring", "salle");
            sparring.Profile = Profile("recrue", "PETIT LOUIS (SPARRING)");
            sparring.Spot = SpotAt("recrue", "salle", "sentraine", new Vector3(-1.5f, 0f, 2.5f));
            sparring.Direct = true;
            sparring.NoPhoto = true;
            sparring.Client = "COACH RAY";
            sparring.Reward = 0;
            sparring.Experience = 120;
            sparring.Tune = Style(-0.4f, -0.45f, 0f, 0f, 0f, 0.3f);
            sparring.Lines = new[]
            {
                L("PETIT LOUIS", "C'est lui, le champion ? Il a l'air fatigué."),
                L("RAY", "Enchaîne, Léo. Jab, crochet, et tu sors. Allez !")
            };
            yield return Fight(sparring, null, "Relève-toi. On recommence, et cette fois tu gardes la garde.");
        }

        /// <summary>Le premier soir : Nestor attend devant le motel, avec deux gros bras.</summary>
        private IEnumerator NestorVisit()
        {
            yield return UntilHour(19f, "Fais des courses, visite la ville. Ce soir, rentre au motel.");

            Put("NESTOR", "motel", new Vector3(0f, 0f, 3.5f), 180f);
            Put("GORILLE1", "motel", new Vector3(-1.4f, 0f, 4.4f), 180f);
            Put("GORILLE2", "motel", new Vector3(1.5f, 0f, 4.3f), 180f);
            yield return Reach("motel", 11f, "Rentre au motel.");
            yield return Calm();

            yield return Lines(
                L("NESTOR", "Léo. Mon garçon préféré."),
                L("NESTOR", "Tu sais pourquoi je suis là. Douze mille, c'est beaucoup. Mais je suis un homme patient."),
                L("NESTOR", "Alors on va faire simple. Sept jours. Mille cinq cents euros. Un premier acompte."),
                L("NESTOR", "Et après, mille cinq cents chaque semaine. Comme un loyer. Mais avec des gens moins gentils que ton motel."),
                L("MOI", "Et si j'ai pas l'argent ?"),
                L("NESTOR", "Alors mes amis viendront te dire bonjour. Ils disent très mal bonjour."),
                L("NESTOR", "Virement accepté. On vit avec son temps."));

            SetCount("nestor:echeance", Day + 7);
            SetCount("nestor:rang", 0);
            Set("p:nestor");
            Sms("NESTOR", "Jour " + (Day + 7) + " : 1 500 €. Appli Banque, « Rembourser Nestor ». Bonne soirée, Léo.");

            yield return new WaitForSeconds(6f);
            Free("NESTOR");
            Free("GORILLE1");
            Free("GORILLE2");
            yield return Lines(L("MOI", "Mille cinq cents en sept jours. Et l'appli qui vibre déjà."));
        }
    }
}
