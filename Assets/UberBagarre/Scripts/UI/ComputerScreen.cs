using System;
using System.Collections.Generic;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// L'ordinateur portable de la planque. Un bureau, des applis :
    /// - CASINO ROYAL : roulette, blackjack, machine à sous — de l'argent vite gagné, et plus
    ///   vite perdu ;
    /// - PARIS DE COMBAT : trois combats clandestins ce soir, des cotes, on mise et on regarde ;
    /// - SALLE DE SPORT : acheter des séances (points d'entraînement) ;
    /// - IMMOBILIER : quitter le motel pour mieux (le bungalow, le manoir) ;
    /// - BANQUE : le compte, ce que le casino a pris ou donné ;
    /// - MAILS : le propriétaire, l'appli, Sami, la banque — l'histoire y écrit aussi.
    /// </summary>
    public class ComputerScreen : FullScreenPanel
    {
        [Serializable]
        public class Property
        {
            public string name;
            public int price;
            [TextArea(1, 3)] public string pitch;
        }

        public struct Mail
        {
            public string From;
            public string Subject;
            public string Body;
            public bool Unread;
        }

        private enum AppId
        {
            Bureau,
            Casino,
            Roulette,
            Blackjack,
            Machine,
            Paris,
            Sport,
            Immobilier,
            Mails,
            Journal
        }

        [SerializeField] private HomeRegistry _homes;

        [SerializeField]
        private Property[] _properties =
        {
            new Property { name = "Bungalow", price = 6500, pitch = "Au bord de l'eau, ville ouest. Une vraie chambre, un conteneur, une cour." },
            new Property { name = "Manoir", price = 85000, pitch = "La colline à l'est. Un portail, une allée, la vue sur toute la ville." }
        };

        private static readonly int[] Stakes = { 10, 20, 50, 100, 200, 500, 1000 };

        /// <summary>Les biens en vente (l'agence immobilière de la ville vend les mêmes).</summary>
        public Property[] Listings { get { return _properties; } }

        private AppId _app;
        private int _stake = 2;
        private readonly Dictionary<int, int> _rows = new Dictionary<int, int>();
        private readonly List<Mail> _mails = new List<Mail>();
        private int _openMail = -1;

        public static ComputerScreen Instance { get; private set; }

        protected override string Title
        {
            get
            {
                switch (_app)
                {
                    case AppId.Casino: return "CASINO ROYAL";
                    case AppId.Roulette: return "ROULETTE";
                    case AppId.Blackjack: return "BLACKJACK";
                    case AppId.Machine: return "MACHINE À SOUS";
                    case AppId.Paris: return "PARIS DE COMBAT";
                    case AppId.Sport: return "SALLE DE SPORT";
                    case AppId.Immobilier: return "IMMOBILIER";
                    case AppId.Mails: return "MAILS";
                    case AppId.Journal: return "HYLAND INFO";
                    default: return "NOUVEL ONGLET";
                }
            }
        }

        protected override string Subtitle
        {
            get
            {
                switch (_app)
                {
                    case AppId.Casino: return "Jeu en ligne. Interdit aux mineurs. La maison gagne toujours — presque.";
                    case AppId.Roulette: return "Européenne : un seul zéro. Rouge, noir, pair, douzaines ou numéro plein.";
                    case AppId.Blackjack: return "Le croupier tire jusqu'à 17. Blackjack payé 3 pour 2.";
                    case AppId.Machine: return "Trois rouleaux. Trois Ü : cinquante fois la mise.";
                    case AppId.Paris: return "Ce soir, trois combats dans les caves de la ville. Choisis ton homme.";
                    case AppId.Immobilier: return "Quitter le motel. Chaque logement a son lit, son ordinateur, son armoire.";
                    default: return "Motel, chambre 3. Le wifi marche une minute sur deux.";
                }
            }
        }

        protected override void Awake()
        {
            base.Awake();
            Instance = this;
            AddMail("Über Bagarre", "Bienvenue", "Ton compte est actif. Accepte les courses depuis ton téléphone.\nUne course ratée fait baisser ta réputation. Trop bas, ton compte est suspendu.\nLes clients paient mieux les bagarreurs réputés.", false);
            AddMail("Motel Hyland", "Loyer de la chambre 3", "Rappel : le loyer de la semaine (450 €) est prélevé tous les sept jours.\nSans règlement, nous serons contraints de récupérer la chambre.", true);
        }

        protected override void OnDestroy()
        {
            base.OnDestroy();
            if (Instance == this) Instance = null;
        }

        protected override void OnOpened()
        {
            _openMail = -1;
            _tabs.Clear();
            _tabPage.Clear();
            _history.Clear();
            _historyIndex = -1;
            Navigate(AppId.Bureau, true);
        }

        /// <summary>Un mail dans la boîte (l'histoire en envoie).</summary>
        public void AddMail(string from, string subject, string body, bool unread)
        {
            for (int i = 0; i < _mails.Count; i++)
            {
                if (_mails[i].From == from && _mails[i].Subject == subject) return;
            }

            _mails.Insert(0, new Mail { From = from, Subject = subject, Body = body, Unread = unread });
            if (_mails.Count > 20) _mails.RemoveAt(_mails.Count - 1);
        }

        /// <summary>Ce mail a-t-il été ouvert ? (faux s'il n'est pas dans la boîte)</summary>
        public bool MailRead(string from, string subject)
        {
            for (int i = 0; i < _mails.Count; i++)
            {
                if (_mails[i].From == from && _mails[i].Subject == subject) return !_mails[i].Unread;
            }

            return false;
        }

        public int UnreadMails
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _mails.Count; i++)
                {
                    if (_mails[i].Unread) n++;
                }

                return n;
            }
        }

        protected override bool OnBack()
        {
            if (_app == AppId.Mails && _openMail >= 0)
            {
                _openMail = -1;
                return true;
            }

            if (_app == AppId.Roulette || _app == AppId.Blackjack || _app == AppId.Machine)
            {
                if (Busy()) return true;
                Go(AppId.Casino);
                return true;
            }

            if (_app == AppId.Paris && _fightRunning) return true;

            if (_app != AppId.Bureau)
            {
                Go(AppId.Bureau);
                return true;
            }

            return false;
        }

        private void Go(AppId app)
        {
            Navigate(app, true);
        }

        private bool Busy()
        {
            return _spinning || _reelsSpinning || (_hand != null && !_handOver);
        }

        protected override void OnHorizontal(int direction)
        {
            int row;
            if (!_rows.TryGetValue(Focus, out row)) return;

            switch (row)
            {
                case 1: _stake = Mathf.Clamp(_stake + direction, 0, Stakes.Length - 1); break;
                case 2: _rouletteBet = (_rouletteBet + direction + RouletteBets.Length) % RouletteBets.Length; break;
                case 3: _rouletteNumber = (_rouletteNumber + direction + 37) % 37; break;
                case 4: _fightIndex = (_fightIndex + direction + Mathf.Max(1, _fights.Count)) % Mathf.Max(1, _fights.Count); break;
                case 5: _fightPick = 1 - _fightPick; break;
                default: return;
            }

            PlayTick();
        }

        protected override void DrawContent(Rect area, float u)
        {
            if (Event.current.type == EventType.Repaint) _rows.Clear();

            switch (_app)
            {
                case AppId.Casino: DrawCasino(area, u); break;
                case AppId.Roulette: DrawRoulette(area, u); break;
                case AppId.Blackjack: DrawBlackjack(area, u); break;
                case AppId.Machine: DrawMachine(area, u); break;
                case AppId.Paris: DrawBets(area, u); break;
                case AppId.Sport: DrawSport(area, u); break;
                case AppId.Immobilier: DrawEstate(area, u); break;
                case AppId.Mails: DrawMails(area, u); break;
                case AppId.Journal: DrawJournal(area, u); break;
                default: DrawDesktop(area, u); break;
            }
        }

        private int _registeredRows;

        /// <summary>Une ligne réglable (gauche / droite) : mise, type de pari, numéro...</summary>
        private bool ValueRow(Rect rect, string label, string value, int row, float u)
        {
            // L'index du focus est celui du prochain bouton : on le note avant de le dessiner.
            int index = _registeredRows;
            bool pressed = Button(rect, label + "   ◄  " + value + "  ►", "Gauche / droite pour changer", true, u);
            if (Event.current.type == EventType.Repaint) _rows[index] = row;
            return pressed;
        }

        private bool Row(Rect rect, string label, string detail, bool enabled, float u)
        {
            return Button(rect, label, detail, enabled, u);
        }

        private int Stake { get { return Stakes[Mathf.Clamp(_stake, 0, Stakes.Length - 1)]; } }

        private void OnGUI_CountRow()
        {
            if (Event.current.type == EventType.Repaint) _registeredRows++;
        }

        // Les lignes sont comptées dans l'ordre de dessin, comme les boutons de la base.
        private Rect NextRow(ref float y, float x, float width, float height, float u)
        {
            Rect r = new Rect(x, y, width, height);
            y += height + 10f * u;
            return r;
        }

        // ------------------------------------------------------------------ page d'accueil

        /// <summary>Le nouvel onglet : la barre de recherche, et les sites du joueur en vignettes.</summary>
        private void DrawDesktop(Rect area, float u)
        {
            _registeredRows = 0;

            Rect logo = new Rect(area.x, area.y + 10f * u, area.width, 60f * u);
            Text(logo, "Gogol", 46, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.45f, 0.7f, 1f), u);

            Rect search = new Rect(area.x + area.width * 0.2f, area.y + 84f * u, area.width * 0.6f, 44f * u);
            GuiKit.Fill(search, new Color(0.16f, 0.17f, 0.2f));
            GuiKit.Outline(search, 1f, new Color(1f, 1f, 1f, 0.18f));
            Text(new Rect(search.x + 18f * u, search.y, search.width - 30f * u, search.height), "🔍  Rechercher ou saisir une adresse",
                17, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, u);

            Site[] sites = { Site.Casino, Site.Paris, Site.Sport, Site.Immo, Site.Mail, Site.Journal };
            float w = (area.width * 0.8f - 4f * 16f * u) / 5f;
            float h = 150f * u;
            for (int i = 0; i < sites.Length; i++)
            {
                Rect tile = new Rect(area.x + area.width * 0.1f + i * (w + 16f * u), area.y + 170f * u, w, h);
                string label = SiteName(sites[i]) + (sites[i] == Site.Mail && UnreadMails > 0 ? "  (" + UnreadMails + ")" : "");
                if (Button(tile, label, SiteHost(sites[i]), true, u)) Go(SiteHome(sites[i]));
                _registeredRows++;
            }

            if (_progress != null)
            {
                Text(new Rect(area.x, area.y + 350f * u, area.width, 30f * u),
                    "Jour " + _progress.Day + "  ·  réputation " + _progress.Reputation + " (" + _progress.ReputationTitle + ")  ·  niveau " +
                    _progress.Level + "  ·  logement : " + _progress.Home, 16, FontStyle.Normal, TextAnchor.MiddleCenter, Dim, u);
            }
        }

        // ------------------------------------------------------------------ le navigateur

        private enum Site
        {
            Accueil,
            Casino,
            Paris,
            Sport,
            Immo,
            Mail,
            Journal
        }

        private readonly List<Site> _tabs = new List<Site>();
        private readonly Dictionary<Site, AppId> _tabPage = new Dictionary<Site, AppId>();
        private readonly List<AppId> _history = new List<AppId>();
        private int _historyIndex = -1;
        private float _loading;

        protected override bool CustomFrame
        {
            get { return true; }
        }

        private static Site SiteOf(AppId app)
        {
            switch (app)
            {
                case AppId.Casino:
                case AppId.Roulette:
                case AppId.Blackjack:
                case AppId.Machine: return Site.Casino;
                case AppId.Paris: return Site.Paris;
                case AppId.Sport: return Site.Sport;
                case AppId.Immobilier: return Site.Immo;
                case AppId.Mails: return Site.Mail;
                case AppId.Journal: return Site.Journal;
                default: return Site.Accueil;
            }
        }

        private static AppId SiteHome(Site site)
        {
            switch (site)
            {
                case Site.Casino: return AppId.Casino;
                case Site.Paris: return AppId.Paris;
                case Site.Sport: return AppId.Sport;
                case Site.Immo: return AppId.Immobilier;
                case Site.Mail: return AppId.Mails;
                case Site.Journal: return AppId.Journal;
                default: return AppId.Bureau;
            }
        }

        private static string SiteName(Site site)
        {
            switch (site)
            {
                case Site.Casino: return "Casino Royal";
                case Site.Paris: return "La Cave — paris";
                case Site.Sport: return "Iron Gym";
                case Site.Immo: return "Hyland Immo";
                case Site.Mail: return "Webmail";
                case Site.Journal: return "Hyland Info";
                default: return "Nouvel onglet";
            }
        }

        private static string SiteHost(Site site)
        {
            switch (site)
            {
                case Site.Casino: return "casinoroyal.bet";
                case Site.Paris: return "lacave7xk2qf.onion";
                case Site.Sport: return "irongym.fr";
                case Site.Immo: return "hyland-immo.fr";
                case Site.Mail: return "webmail.hylandnet.fr";
                case Site.Journal: return "hylandinfo.fr";
                default: return "gogol.fr";
            }
        }

        private static Color SiteColor(Site site)
        {
            switch (site)
            {
                case Site.Casino: return new Color(0.95f, 0.72f, 0.2f);
                case Site.Paris: return new Color(0.35f, 0.9f, 0.45f);
                case Site.Sport: return new Color(1f, 0.5f, 0.12f);
                case Site.Immo: return new Color(0.3f, 0.6f, 1f);
                case Site.Mail: return new Color(0.2f, 0.8f, 0.8f);
                case Site.Journal: return new Color(0.85f, 0.85f, 0.88f);
                default: return new Color(0.45f, 0.7f, 1f);
            }
        }

        private static Color SiteBackground(Site site)
        {
            switch (site)
            {
                case Site.Casino: return new Color(0.16f, 0.03f, 0.04f);
                case Site.Paris: return new Color(0.02f, 0.05f, 0.03f);
                case Site.Sport: return new Color(0.09f, 0.07f, 0.06f);
                case Site.Immo: return new Color(0.05f, 0.08f, 0.14f);
                case Site.Mail: return new Color(0.05f, 0.1f, 0.11f);
                case Site.Journal: return new Color(0.08f, 0.08f, 0.1f);
                default: return new Color(0.11f, 0.12f, 0.14f);
            }
        }

        private string Url(AppId app)
        {
            Site site = SiteOf(app);
            string path;
            switch (app)
            {
                case AppId.Roulette: path = "/roulette"; break;
                case AppId.Blackjack: path = "/blackjack"; break;
                case AppId.Machine: path = "/machine-a-sous"; break;
                case AppId.Paris: path = "/ce-soir"; break;
                case AppId.Sport: path = "/seances"; break;
                case AppId.Immobilier: path = "/annonces"; break;
                case AppId.Mails: path = _openMail >= 0 ? "/message/" + (_openMail + 1) : "/boite-de-reception"; break;
                case AppId.Journal: path = _openArticle >= 0 ? "/article/" + (_openArticle + 1) : "/"; break;
                default: path = "/"; break;
            }

            return (site == Site.Paris ? "http://" : "https://") + SiteHost(site) + path;
        }

        private void RememberNavigation(AppId app)
        {
            Site site = SiteOf(app);
            if (!_tabs.Contains(site)) _tabs.Add(site);
            _tabPage[site] = app;
            _loading = 0.35f;
        }

        private void PushHistory(AppId app)
        {
            if (_historyIndex >= 0 && _historyIndex < _history.Count && _history[_historyIndex] == app) return;
            if (_historyIndex < _history.Count - 1) _history.RemoveRange(_historyIndex + 1, _history.Count - _historyIndex - 1);
            _history.Add(app);
            if (_history.Count > 30) _history.RemoveAt(0);
            _historyIndex = _history.Count - 1;
        }

        private void Navigate(AppId app, bool record)
        {
            _app = app;
            Focus = 0;
            _openMail = -1;
            if (app == AppId.Paris && _fights.Count == 0) NewFights();
            RememberNavigation(app);
            if (record) PushHistory(app);
        }

        private void CloseTab(Site site)
        {
            int index = _tabs.IndexOf(site);
            if (index < 0) return;
            _tabs.RemoveAt(index);
            _tabPage.Remove(site);

            if (SiteOf(_app) != site) return;
            if (_tabs.Count == 0)
            {
                Navigate(AppId.Bureau, true);
                return;
            }

            Site next = _tabs[Mathf.Clamp(index - 1, 0, _tabs.Count - 1)];
            AppId page;
            Navigate(_tabPage.TryGetValue(next, out page) ? page : SiteHome(next), true);
        }

        /// <summary>Un bouton de la fenêtre (onglet, flèches, favoris) : clic seulement.</summary>
        private bool ChromeButton(Rect rect, string label, Color text, Color fill, float u, int size = 15)
        {
            bool hover = rect.Contains(Event.current.mousePosition);
            if (fill.a > 0f || hover) GuiKit.Fill(rect, hover ? Color.Lerp(fill, Color.white, 0.12f) : fill);
            Text(rect, label, size, FontStyle.Normal, TextAnchor.MiddleCenter, text, u);

            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover)
            {
                Event.current.Use();
                PlayTick();
                return true;
            }

            return false;
        }

        /// <summary>
        /// Le bureau et le navigateur : fond d'écran, barre des tâches, et la fenêtre avec ses
        /// onglets, ses flèches, sa barre d'adresse, ses favoris et la page du site.
        /// </summary>
        protected override Rect DrawFrame(float sw, float sh, float u)
        {
            if (Event.current.type == EventType.Repaint && _loading > 0f) _loading = Mathf.Max(0f, _loading - Time.unscaledDeltaTime);

            // --- le bureau
            GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0.05f, 0.06f, 0.12f));
            GuiKit.Disc(new Rect(sw * 0.35f, sh * 0.1f, sw * 0.9f, sh * 1.1f), new Color(0.35f, 0.12f, 0.45f, 0.35f));
            GuiKit.Disc(new Rect(-sw * 0.2f, sh * 0.4f, sw * 0.7f, sh * 0.9f), new Color(0.1f, 0.25f, 0.5f, 0.3f));

            float bar = 44f * u;
            Rect taskbar = new Rect(0f, sh - bar, sw, bar);
            GuiKit.Fill(taskbar, new Color(0.04f, 0.04f, 0.06f, 0.96f));
            GuiKit.Fill(new Rect(14f * u, sh - bar + 8f * u, 28f * u, 28f * u), new Color(0.45f, 0.7f, 1f));
            GuiKit.Fill(new Rect(54f * u, sh - bar + 6f * u, 32f * u, 32f * u), new Color(1f, 1f, 1f, 0.12f));
            GuiKit.Disc(new Rect(58f * u, sh - bar + 10f * u, 24f * u, 24f * u), new Color(0.95f, 0.45f, 0.15f));
            GuiKit.Fill(new Rect(58f * u, sh - 5f * u, 24f * u, 3f * u), new Color(0.45f, 0.7f, 1f));
            string clock = _progress != null ? "Jour " + _progress.Day + "   " + System.DateTime.Now.ToString("HH:mm") : System.DateTime.Now.ToString("HH:mm");
            Text(new Rect(sw - 260f * u, sh - bar, 240f * u, bar), "wifi  ▮▮▮    " + clock, 15, FontStyle.Normal, TextAnchor.MiddleRight, Ink, u);

            // --- la fenêtre
            Rect window = new Rect(36f * u, 24f * u, sw - 72f * u, sh - bar - 44f * u);
            GuiKit.Fill(new Rect(window.x + 6f * u, window.y + 8f * u, window.width, window.height), new Color(0f, 0f, 0f, 0.35f));
            GuiKit.Fill(window, new Color(0.13f, 0.14f, 0.16f));

            // onglets
            float tabsH = 40f * u;
            float x = window.x + 90f * u;
            GuiKit.Disc(new Rect(window.x + 16f * u, window.y + 14f * u, 12f * u, 12f * u), new Color(1f, 0.37f, 0.34f));
            GuiKit.Disc(new Rect(window.x + 36f * u, window.y + 14f * u, 12f * u, 12f * u), new Color(1f, 0.75f, 0.2f));
            GuiKit.Disc(new Rect(window.x + 56f * u, window.y + 14f * u, 12f * u, 12f * u), new Color(0.3f, 0.8f, 0.3f));

            if (_tabs.Count == 0) RememberNavigation(_app);
            Site current = SiteOf(_app);
            for (int i = 0; i < _tabs.Count; i++)
            {
                Site site = _tabs[i];
                Rect tab = new Rect(x, window.y + 6f * u, 220f * u, tabsH - 6f * u);
                bool active = site == current;
                GuiKit.Fill(tab, active ? new Color(0.2f, 0.21f, 0.24f) : new Color(0.1f, 0.1f, 0.12f));
                GuiKit.Fill(new Rect(tab.x + 10f * u, tab.y + 11f * u, 12f * u, 12f * u), SiteColor(site));

                Rect label = new Rect(tab.x + 28f * u, tab.y, tab.width - 60f * u, tab.height);
                if (ChromeButton(label, "", Ink, new Color(0f, 0f, 0f, 0f), u))
                {
                    AppId last;
                    Navigate(_tabPage.TryGetValue(site, out last) ? last : SiteHome(site), true);
                }

                Text(label, SiteName(site), 14, active ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft, active ? Ink : Dim, u);
                if (ChromeButton(new Rect(tab.xMax - 28f * u, tab.y + 6f * u, 22f * u, tab.height - 12f * u), "×", Dim,
                        new Color(0f, 0f, 0f, 0f), u, 16))
                {
                    CloseTab(site);
                    break;
                }

                x += tab.width + 4f * u;
            }

            if (ChromeButton(new Rect(x + 4f * u, window.y + 10f * u, 26f * u, 26f * u), "+", Ink, new Color(0f, 0f, 0f, 0f), u, 18))
            {
                Navigate(AppId.Bureau, true);
            }

            // barre d'outils
            float toolY = window.y + tabsH;
            float toolH = 46f * u;
            GuiKit.Fill(new Rect(window.x, toolY, window.width, toolH), new Color(0.2f, 0.21f, 0.24f));

            bool canBack = _historyIndex > 0;
            bool canForward = _historyIndex >= 0 && _historyIndex < _history.Count - 1;
            if (ChromeButton(new Rect(window.x + 12f * u, toolY + 8f * u, 30f * u, 30f * u), "◄", canBack ? Ink : Dim,
                    new Color(0f, 0f, 0f, 0f), u) && canBack && !Busy())
            {
                _historyIndex--;
                Navigate(_history[_historyIndex], false);
            }

            if (ChromeButton(new Rect(window.x + 46f * u, toolY + 8f * u, 30f * u, 30f * u), "►", canForward ? Ink : Dim,
                    new Color(0f, 0f, 0f, 0f), u) && canForward && !Busy())
            {
                _historyIndex++;
                Navigate(_history[_historyIndex], false);
            }

            if (ChromeButton(new Rect(window.x + 80f * u, toolY + 8f * u, 30f * u, 30f * u), _loading > 0f ? "×" : "⟳", Ink,
                    new Color(0f, 0f, 0f, 0f), u))
            {
                _loading = 0.35f;
            }

            Rect address = new Rect(window.x + 122f * u, toolY + 8f * u, window.width - 122f * u - 230f * u, 30f * u);
            GuiKit.Fill(address, new Color(0.11f, 0.12f, 0.14f));
            bool secure = current != Site.Paris;
            Text(new Rect(address.x + 10f * u, address.y, 24f * u, address.height), secure ? "🔒" : "⚠", 14, FontStyle.Normal,
                TextAnchor.MiddleCenter, secure ? Good : Warn, u);
            string url = _app == AppId.Bureau ? "Rechercher avec Gogol ou saisir une adresse" : Url(_app);
            Text(new Rect(address.x + 38f * u, address.y, address.width - 48f * u, address.height), url, 15, FontStyle.Normal,
                TextAnchor.MiddleLeft, _app == AppId.Bureau ? Dim : Ink, u);
            if (!secure)
            {
                Text(new Rect(address.xMax - 150f * u, address.y, 140f * u, address.height), "Non sécurisé", 13, FontStyle.Normal,
                    TextAnchor.MiddleRight, Warn, u);
            }

            // Le porte-monnaie et le courrier, à droite.
            if (_progress != null)
            {
                Text(new Rect(window.xMax - 220f * u, toolY, 150f * u, toolH), _progress.Money + " €", 18, FontStyle.Bold,
                    TextAnchor.MiddleRight, Good, u);
            }

            int unread = UnreadMails;
            if (ChromeButton(new Rect(window.xMax - 58f * u, toolY + 8f * u, 44f * u, 30f * u), unread > 0 ? "✉ " + unread : "✉",
                    unread > 0 ? Warn : Dim, new Color(0f, 0f, 0f, 0f), u) && !Busy())
            {
                Navigate(AppId.Mails, true);
            }

            // favoris
            float favY = toolY + toolH;
            float favH = 30f * u;
            GuiKit.Fill(new Rect(window.x, favY, window.width, favH), new Color(0.17f, 0.18f, 0.2f));
            Site[] favorites = { Site.Casino, Site.Paris, Site.Sport, Site.Immo, Site.Mail, Site.Journal };
            float fx = window.x + 14f * u;
            for (int i = 0; i < favorites.Length; i++)
            {
                Rect fav = new Rect(fx, favY + 3f * u, 150f * u, favH - 6f * u);
                GuiKit.Fill(new Rect(fav.x + 6f * u, fav.y + 7f * u, 10f * u, 10f * u), SiteColor(favorites[i]));
                if (ChromeButton(new Rect(fav.x + 20f * u, fav.y, fav.width - 20f * u, fav.height), SiteName(favorites[i]), Ink,
                        new Color(0f, 0f, 0f, 0f), u, 13) && !Busy())
                {
                    Navigate(SiteHome(favorites[i]), true);
                }

                fx += fav.width + 6f * u;
            }

            // la page
            Rect page = new Rect(window.x, favY + favH, window.width, window.yMax - favY - favH - 22f * u);
            GuiKit.Fill(page, SiteBackground(current));

            float content = page.y + 20f * u;
            if (current != Site.Accueil)
            {
                Rect header = new Rect(page.x, page.y, page.width, 78f * u);
                GuiKit.Fill(header, new Color(0f, 0f, 0f, 0.35f));
                GuiKit.Fill(new Rect(page.x, header.yMax - 3f * u, page.width, 3f * u), SiteColor(current));
                Text(new Rect(page.x + 40f * u, header.y + 8f * u, page.width * 0.6f, 40f * u), Title, 30, FontStyle.Bold,
                    TextAnchor.MiddleLeft, SiteColor(current), u);
                if (!string.IsNullOrEmpty(Subtitle))
                {
                    Text(new Rect(page.x + 40f * u, header.y + 46f * u, page.width - 80f * u, 24f * u), Subtitle, 15,
                        FontStyle.Normal, TextAnchor.MiddleLeft, Dim, u);
                }

                content = header.yMax + 20f * u;
            }

            // barre d'état
            Rect status = new Rect(window.x, window.yMax - 22f * u, window.width, 22f * u);
            GuiKit.Fill(status, new Color(0.1f, 0.1f, 0.12f));
            Text(new Rect(status.x + 12f * u, status.y, status.width - 24f * u, status.height),
                _loading > 0f ? "Chargement de " + SiteHost(current) + "…" : "Flèches / souris : choisir     Entrée / clic : valider     Échap : retour",
                12, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, u);

            return new Rect(page.x + 40f * u, content, page.width - 80f * u, page.yMax - content - 16f * u);
        }

        private void DrawCasino(Rect area, float u)
        {
            _registeredRows = 0;
            float y = area.y;
            if (Row(NextRow(ref y, area.x, area.width * 0.5f, 80f * u, u), "ROULETTE", "Rouge ou noir, douzaines, numéro plein (x36).", true, u)) Go(AppId.Roulette);
            _registeredRows++;
            if (Row(NextRow(ref y, area.x, area.width * 0.5f, 80f * u, u), "BLACKJACK", "Approche 21 sans le dépasser. Bats le croupier.", true, u)) { Go(AppId.Blackjack); _hand = null; }
            _registeredRows++;
            if (Row(NextRow(ref y, area.x, area.width * 0.5f, 80f * u, u), "MACHINE À SOUS", "Tire le levier. Trois symboles identiques.", true, u)) Go(AppId.Machine);
            _registeredRows++;
            if (Row(NextRow(ref y, area.x, area.width * 0.5f, 60f * u, u), "RETOUR", null, true, u)) Go(AppId.Bureau);
            _registeredRows++;

            if (_progress != null)
            {
                int net = _progress.CasinoWon - _progress.CasinoLost;
                Text(new Rect(area.x + area.width * 0.55f, area.y, area.width * 0.45f, 40f * u), "Bilan casino : " + (net >= 0 ? "+" : "") + net + " €",
                    24, FontStyle.Bold, TextAnchor.MiddleLeft, net >= 0 ? Good : Bad, u);
            }
        }

        private bool Take(int amount)
        {
            if (_progress == null || !_progress.Spend(amount, "Mise casinoroyal.bet"))
            {
                Toast("Pas assez d'argent pour miser " + amount + " €.", true);
                return false;
            }

            return true;
        }

        private void Win(int stake, int payout, string label)
        {
            if (_progress == null) return;

            if (payout > 0)
            {
                _progress.AddMoney(payout, "Gain casinoroyal.bet");
                PlayCash();
            }

            int net = payout - stake;
            _progress.CountCasino(Mathf.Max(0, net), Mathf.Max(0, -net));
            Toast(label, net < 0);
        }

        // ------------------------------------------------------------------ roulette

        private static readonly string[] RouletteBets =
        {
            "ROUGE (x2)", "NOIR (x2)", "PAIR (x2)", "IMPAIR (x2)", "MANQUE 1-18 (x2)", "PASSE 19-36 (x2)",
            "1re DOUZAINE (x3)", "2e DOUZAINE (x3)", "3e DOUZAINE (x3)", "NUMÉRO PLEIN (x36)"
        };

        private static readonly int[] Reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };

        private int _rouletteBet;
        private int _rouletteNumber = 17;
        private bool _spinning;
        private float _spinTime;
        private int _spinResult = -1;
        private int _spinShown;
        private int _spinStake;

        private static bool IsRed(int n)
        {
            return Array.IndexOf(Reds, n) >= 0;
        }

        private void DrawRoulette(Rect area, float u)
        {
            _registeredRows = 0;
            float x = area.x;
            float w = area.width * 0.5f;
            float y = area.y;

            ValueRow(NextRow(ref y, x, w, 64f * u, u), "PARI", RouletteBets[_rouletteBet], 2, u);
            _registeredRows++;
            if (_rouletteBet == 9)
            {
                ValueRow(NextRow(ref y, x, w, 64f * u, u), "NUMÉRO", _rouletteNumber.ToString(), 3, u);
                _registeredRows++;
            }

            ValueRow(NextRow(ref y, x, w, 64f * u, u), "MISE", Stake + " €", 1, u);
            _registeredRows++;

            if (Row(NextRow(ref y, x, w, 70f * u, u), _spinning ? "LA BILLE TOURNE..." : "LANCER LA BILLE", null, !_spinning, u) && !_spinning)
            {
                if (Take(Stake))
                {
                    _spinStake = Stake;
                    _spinning = true;
                    _spinTime = 0f;
                    _spinResult = UnityEngine.Random.Range(0, 37);
                }
            }

            _registeredRows++;
            if (Row(NextRow(ref y, x, w, 56f * u, u), "RETOUR", null, !_spinning, u)) Go(AppId.Casino);
            _registeredRows++;

            // --- le cylindre : le numéro défile, ralentit, se fixe
            Rect wheel = new Rect(area.x + area.width * 0.58f, area.y, area.width * 0.36f, area.width * 0.36f);
            GuiKit.Disc(wheel, new Color(0.25f, 0.13f, 0.05f));
            Rect inner = new Rect(wheel.x + wheel.width * 0.12f, wheel.y + wheel.height * 0.12f, wheel.width * 0.76f, wheel.height * 0.76f);
            int shown = _spinning ? _spinShown : Mathf.Max(0, _spinResult);
            Color face = _spinResult < 0 && !_spinning ? new Color(0.1f, 0.3f, 0.12f) : shown == 0 ? new Color(0.1f, 0.5f, 0.15f) : IsRed(shown) ? new Color(0.7f, 0.08f, 0.08f) : new Color(0.08f, 0.08f, 0.09f);
            GuiKit.Disc(inner, face);
            Text(inner, _spinResult < 0 && !_spinning ? "?" : shown.ToString(), 90, FontStyle.Bold, TextAnchor.MiddleCenter, Color.white, u);

            if (!_spinning || Event.current.type != EventType.Repaint) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _spinTime += dt;
            float speed = Mathf.Lerp(30f, 2f, Mathf.Clamp01(_spinTime / 3.2f));
            if (UnityEngine.Random.value < speed * dt) _spinShown = UnityEngine.Random.Range(0, 37);

            if (_spinTime < 3.6f) return;

            _spinning = false;
            _spinShown = _spinResult;
            int payout = RoulettePayout(_spinResult, _spinStake);
            Win(_spinStake, payout, payout > 0 ? _spinResult + " ! Gagné : +" + payout + " €" : _spinResult + (_spinResult == 0 ? " (zéro)" : IsRed(_spinResult) ? " rouge" : " noir") + ". Perdu.");
        }

        private int RoulettePayout(int n, int stake)
        {
            bool win;
            int multiplier = 2;
            switch (_rouletteBet)
            {
                case 0: win = n != 0 && IsRed(n); break;
                case 1: win = n != 0 && !IsRed(n); break;
                case 2: win = n != 0 && n % 2 == 0; break;
                case 3: win = n % 2 == 1; break;
                case 4: win = n >= 1 && n <= 18; break;
                case 5: win = n >= 19; break;
                case 6: win = n >= 1 && n <= 12; multiplier = 3; break;
                case 7: win = n >= 13 && n <= 24; multiplier = 3; break;
                case 8: win = n >= 25; multiplier = 3; break;
                default: win = n == _rouletteNumber; multiplier = 36; break;
            }

            return win ? stake * multiplier : 0;
        }

        // ------------------------------------------------------------------ blackjack

        private List<int> _shoe;
        private List<int> _hand;
        private List<int> _dealer;
        private bool _handOver = true;
        private int _handStake;
        private string _handResult;

        private int Draw()
        {
            if (_shoe == null || _shoe.Count < 20)
            {
                _shoe = new List<int>(312);
                for (int d = 0; d < 6; d++)
                {
                    for (int c = 0; c < 52; c++) _shoe.Add(c);
                }

                for (int i = _shoe.Count - 1; i > 0; i--)
                {
                    int j = UnityEngine.Random.Range(0, i + 1);
                    int t = _shoe[i];
                    _shoe[i] = _shoe[j];
                    _shoe[j] = t;
                }
            }

            int card = _shoe[_shoe.Count - 1];
            _shoe.RemoveAt(_shoe.Count - 1);
            return card;
        }

        private static int Value(List<int> cards)
        {
            int total = 0;
            int aces = 0;
            for (int i = 0; i < cards.Count; i++)
            {
                int rank = cards[i] % 13;
                if (rank == 0)
                {
                    aces++;
                    total += 11;
                }
                else
                {
                    total += Mathf.Min(10, rank + 1);
                }
            }

            while (total > 21 && aces > 0)
            {
                total -= 10;
                aces--;
            }

            return total;
        }

        private void DrawBlackjack(Rect area, float u)
        {
            _registeredRows = 0;
            float x = area.x;
            float w = area.width * 0.36f;
            float y = area.y;

            if (_hand == null || _handOver)
            {
                ValueRow(NextRow(ref y, x, w, 64f * u, u), "MISE", Stake + " €", 1, u);
                _registeredRows++;
                if (Row(NextRow(ref y, x, w, 70f * u, u), "DISTRIBUER", null, true, u)) Deal();
                _registeredRows++;
                if (Row(NextRow(ref y, x, w, 56f * u, u), "RETOUR", null, true, u)) Go(AppId.Casino);
                _registeredRows++;
            }
            else
            {
                if (Row(NextRow(ref y, x, w, 70f * u, u), "TIRER", "Une carte de plus.", true, u)) Hit();
                _registeredRows++;
                if (Row(NextRow(ref y, x, w, 70f * u, u), "RESTER", "Au croupier de jouer.", true, u)) Stand();
                _registeredRows++;
                bool canDouble = _hand.Count == 2 && _progress != null && _progress.Money >= _handStake;
                if (Row(NextRow(ref y, x, w, 70f * u, u), "DOUBLER", "Mise doublée, une seule carte.", canDouble, u)) DoubleDown();
                _registeredRows++;
            }

            float cx = area.x + area.width * 0.42f;
            if (_dealer != null)
            {
                Text(new Rect(cx, area.y, 400f * u, 30f * u), "CROUPIER" + (_handOver ? "  ·  " + Value(_dealer) : ""), 20, FontStyle.Bold, TextAnchor.MiddleLeft, Dim, u);
                DrawCards(_dealer, new Vector2(cx, area.y + 36f * u), u, !_handOver);
            }

            if (_hand != null)
            {
                Text(new Rect(cx, area.y + 220f * u, 400f * u, 30f * u), "TOI  ·  " + Value(_hand), 20, FontStyle.Bold, TextAnchor.MiddleLeft, Ink, u);
                DrawCards(_hand, new Vector2(cx, area.y + 256f * u), u, false);
            }

            if (!string.IsNullOrEmpty(_handResult))
            {
                Text(new Rect(cx, area.y + 440f * u, area.xMax - cx, 40f * u), _handResult, 26, FontStyle.Bold, TextAnchor.MiddleLeft,
                    _handResult.Contains("Perdu") || _handResult.Contains("Sauté") ? Bad : Good, u);
            }
        }

        private void DrawCards(List<int> cards, Vector2 at, float u, bool hideSecond)
        {
            string[] ranks = { "A", "2", "3", "4", "5", "6", "7", "8", "9", "10", "V", "D", "R" };
            string[] suits = { "♠", "♥", "♦", "♣" };

            for (int i = 0; i < cards.Count; i++)
            {
                Rect card = new Rect(at.x + i * 96f * u, at.y, 86f * u, 128f * u);
                bool hidden = hideSecond && i == 1;
                GuiKit.Fill(card, hidden ? new Color(0.5f, 0.08f, 0.2f) : new Color(0.95f, 0.94f, 0.9f));
                GuiKit.Outline(card, 2f, new Color(0f, 0f, 0f, 0.6f));
                if (hidden) continue;

                int suit = cards[i] / 13 % 4;
                Color ink = suit == 1 || suit == 2 ? new Color(0.75f, 0.05f, 0.08f) : new Color(0.05f, 0.05f, 0.06f);
                GuiKit.OutlinedLabel(new Rect(card.x + 8f * u, card.y + 6f * u, card.width, 34f * u), ranks[cards[i] % 13],
                    GuiKit.Style(Mathf.RoundToInt(26 * u), FontStyle.Bold, TextAnchor.UpperLeft), ink, new Color(0, 0, 0, 0), 0f);
                GuiKit.OutlinedLabel(card, suits[suit], GuiKit.Style(Mathf.RoundToInt(44 * u), FontStyle.Bold, TextAnchor.MiddleCenter),
                    ink, new Color(0, 0, 0, 0), 0f);
            }
        }

        private void Deal()
        {
            if (!Take(Stake)) return;

            _handStake = Stake;
            _hand = new List<int> { Draw(), Draw() };
            _dealer = new List<int> { Draw(), Draw() };
            _handOver = false;
            _handResult = null;

            if (Value(_hand) == 21) Stand();
        }

        private void Hit()
        {
            _hand.Add(Draw());
            if (Value(_hand) > 21) Settle();
        }

        private void DoubleDown()
        {
            if (!Take(_handStake)) return;
            _handStake *= 2;
            _hand.Add(Draw());
            Stand();
        }

        private void Stand()
        {
            if (Value(_hand) <= 21)
            {
                while (Value(_dealer) < 17) _dealer.Add(Draw());
            }

            Settle();
        }

        private void Settle()
        {
            _handOver = true;
            int me = Value(_hand);
            int him = Value(_dealer);
            bool blackjack = me == 21 && _hand.Count == 2;
            int payout;

            if (me > 21)
            {
                payout = 0;
                _handResult = "Sauté (" + me + "). Perdu.";
            }
            else if (blackjack && !(him == 21 && _dealer.Count == 2))
            {
                payout = _handStake + _handStake * 3 / 2;
                _handResult = "BLACKJACK ! +" + payout + " €";
            }
            else if (him > 21 || me > him)
            {
                payout = _handStake * 2;
                _handResult = "Gagné ! +" + payout + " €";
            }
            else if (me == him)
            {
                payout = _handStake;
                _handResult = "Égalité : mise rendue.";
            }
            else
            {
                payout = 0;
                _handResult = "Perdu (" + me + " contre " + him + ").";
            }

            Win(_handStake, payout, _handResult);
        }

        // ------------------------------------------------------------------ machine à sous

        private static readonly string[] Symbols = { "♥", "♦", "★", "BAR", "7", "Ü" };
        private static readonly int[] Weights = { 30, 24, 18, 12, 8, 4 };
        private static readonly int[] Pays = { 3, 4, 6, 10, 20, 50 };

        private readonly int[] _reels = { 5, 5, 5 };
        private readonly int[] _reelResult = new int[3];
        private bool _reelsSpinning;
        private float _reelTime;
        private int _reelStake;

        private static int WeightedSymbol()
        {
            int total = 0;
            for (int i = 0; i < Weights.Length; i++) total += Weights[i];
            int r = UnityEngine.Random.Range(0, total);
            for (int i = 0; i < Weights.Length; i++)
            {
                if (r < Weights[i]) return i;
                r -= Weights[i];
            }

            return 0;
        }

        private void DrawMachine(Rect area, float u)
        {
            _registeredRows = 0;
            float x = area.x;
            float w = area.width * 0.36f;
            float y = area.y;

            ValueRow(NextRow(ref y, x, w, 64f * u, u), "MISE", Stake + " €", 1, u);
            _registeredRows++;
            if (Row(NextRow(ref y, x, w, 70f * u, u), _reelsSpinning ? "..." : "TIRER LE LEVIER", null, !_reelsSpinning, u) && !_reelsSpinning)
            {
                if (Take(Stake))
                {
                    _reelStake = Stake;
                    _reelsSpinning = true;
                    _reelTime = 0f;
                    for (int i = 0; i < 3; i++) _reelResult[i] = WeightedSymbol();
                }
            }

            _registeredRows++;
            if (Row(NextRow(ref y, x, w, 56f * u, u), "RETOUR", null, !_reelsSpinning, u)) Go(AppId.Casino);
            _registeredRows++;

            string pays = "Trois identiques :  ";
            for (int i = Symbols.Length - 1; i >= 0; i--) pays += Symbols[i] + " x" + Pays[i] + "   ";
            Text(new Rect(x, y + 20f * u, w, 80f * u), pays + "\nDeux ♥ : mise rendue.", 16, FontStyle.Normal, TextAnchor.UpperLeft, Dim, u, true);

            // --- les rouleaux
            float rx = area.x + area.width * 0.45f;
            for (int i = 0; i < 3; i++)
            {
                Rect reel = new Rect(rx + i * 170f * u, area.y + 20f * u, 150f * u, 200f * u);
                GuiKit.Fill(reel, new Color(0.95f, 0.94f, 0.9f));
                GuiKit.Outline(reel, 3f, Warn);
                int symbol = _reels[i];
                Color ink = symbol == 5 ? Accent : symbol == 4 ? new Color(0.8f, 0.1f, 0.1f) : new Color(0.1f, 0.1f, 0.12f);
                GuiKit.OutlinedLabel(reel, Symbols[symbol], GuiKit.Style(Mathf.RoundToInt(64 * u), FontStyle.Bold, TextAnchor.MiddleCenter), ink, new Color(0, 0, 0, 0), 0f);
            }

            if (!_reelsSpinning || Event.current.type != EventType.Repaint) return;

            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            _reelTime += dt;
            for (int i = 0; i < 3; i++)
            {
                float stopAt = 1.2f + i * 0.55f;
                if (_reelTime < stopAt) _reels[i] = UnityEngine.Random.Range(0, Symbols.Length);
                else _reels[i] = _reelResult[i];
            }

            if (_reelTime < 1.2f + 2f * 0.55f + 0.2f) return;

            _reelsSpinning = false;
            int payout = 0;
            if (_reels[0] == _reels[1] && _reels[1] == _reels[2]) payout = _reelStake * Pays[_reels[0]];
            else
            {
                int hearts = (_reels[0] == 0 ? 1 : 0) + (_reels[1] == 0 ? 1 : 0) + (_reels[2] == 0 ? 1 : 0);
                if (hearts == 2) payout = _reelStake;
            }

            Win(_reelStake, payout, payout > _reelStake ? "JACKPOT ! +" + payout + " €" : payout > 0 ? "Mise rendue." : "Rien. Encore ?");
        }

        // ------------------------------------------------------------------ paris de combat

        private class Fight
        {
            public string A;
            public string B;
            public int RatingA;
            public int RatingB;
            public float OddsA;
            public float OddsB;
        }

        private static readonly string[] Fighters =
        {
            "Le Boucher", "Kader « Marteau »", "Yohan le Dingue", "Tito", "Le Gitan", "Mamadou « Mur »", "Slim",
            "Rocco", "Le Vieux Lion", "Karim Sans-Peur", "Dédé la Brique", "Nico le Rapide", "Big Moussa", "Le Russe"
        };

        private readonly List<Fight> _fights = new List<Fight>();
        private int _fightIndex;
        private int _fightPick;
        private bool _fightRunning;
        private float _fightTime;
        private readonly List<string> _fightLog = new List<string>();
        private bool _fightWinnerA;
        private int _fightStake;
        private bool _fightOver;

        private void NewFights()
        {
            _fights.Clear();
            List<string> pool = new List<string>(Fighters);
            for (int i = 0; i < 3; i++)
            {
                Fight f = new Fight();
                f.A = Take(pool);
                f.B = Take(pool);
                f.RatingA = UnityEngine.Random.Range(40, 95);
                f.RatingB = UnityEngine.Random.Range(40, 95);
                float pA = Probability(f.RatingA, f.RatingB);
                f.OddsA = Mathf.Max(1.08f, Mathf.Round(0.92f / pA * 100f) / 100f);
                f.OddsB = Mathf.Max(1.08f, Mathf.Round(0.92f / (1f - pA) * 100f) / 100f);
                _fights.Add(f);
            }

            _fightIndex = 0;
            _fightPick = 0;
            _fightOver = false;
            _fightLog.Clear();
        }

        private static string Take(List<string> pool)
        {
            int i = UnityEngine.Random.Range(0, pool.Count);
            string s = pool[i];
            pool.RemoveAt(i);
            return s;
        }

        private static float Probability(int a, int b)
        {
            return 1f / (1f + Mathf.Pow(10f, (b - a) / 40f));
        }

        private void DrawBets(Rect area, float u)
        {
            _registeredRows = 0;
            if (_fights.Count == 0) NewFights();
            Fight f = _fights[Mathf.Clamp(_fightIndex, 0, _fights.Count - 1)];

            float x = area.x;
            float w = area.width * 0.44f;
            float y = area.y;

            if (!_fightRunning && !_fightOver)
            {
                ValueRow(NextRow(ref y, x, w, 64f * u, u), "COMBAT", (_fightIndex + 1) + " / " + _fights.Count, 4, u);
                _registeredRows++;
                ValueRow(NextRow(ref y, x, w, 64f * u, u), "PARIER SUR", (_fightPick == 0 ? f.A + "  (cote " + f.OddsA.ToString("0.00") : f.B + "  (cote " + f.OddsB.ToString("0.00")) + ")", 5, u);
                _registeredRows++;
                ValueRow(NextRow(ref y, x, w, 64f * u, u), "MISE", Stake + " €", 1, u);
                _registeredRows++;

                if (Row(NextRow(ref y, x, w, 70f * u, u), "REGARDER LE COMBAT", "Gain possible : " + Mathf.RoundToInt(Stake * (_fightPick == 0 ? f.OddsA : f.OddsB)) + " €", true, u))
                {
                    if (Take(Stake)) StartFight(f);
                }

                _registeredRows++;
                if (Row(NextRow(ref y, x, w, 56f * u, u), "RETOUR", null, true, u)) Go(AppId.Bureau);
                _registeredRows++;
            }
            else if (_fightOver)
            {
                if (Row(NextRow(ref y, x, w, 70f * u, u), "AUTRES COMBATS", null, true, u)) NewFights();
                _registeredRows++;
                if (Row(NextRow(ref y, x, w, 56f * u, u), "RETOUR", null, true, u)) { NewFights(); Go(AppId.Bureau); }
                _registeredRows++;
            }

            // --- l'affiche, puis le récit du combat
            float cx = area.x + area.width * 0.5f;
            float cw = area.width * 0.5f;
            Text(new Rect(cx, area.y, cw, 40f * u), f.A + "  vs  " + f.B, 30, FontStyle.Bold, TextAnchor.MiddleLeft, Ink, u);
            Text(new Rect(cx, area.y + 42f * u, cw, 28f * u), "Forme : " + f.RatingA + "  contre  " + f.RatingB + "     Cotes : " + f.OddsA.ToString("0.00") + " / " + f.OddsB.ToString("0.00"),
                17, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, u);

            for (int i = 0; i < _fightLog.Count; i++)
            {
                Text(new Rect(cx, area.y + 90f * u + i * 30f * u, cw, 30f * u), _fightLog[i], 18, FontStyle.Normal, TextAnchor.MiddleLeft,
                    i == _fightLog.Count - 1 && _fightOver ? Warn : Ink, u);
            }

            if (_fightRunning && Event.current.type == EventType.Repaint) RunFight(f);
        }

        private void StartFight(Fight f)
        {
            _fightStake = Stake;
            _fightRunning = true;
            _fightOver = false;
            _fightTime = 0f;
            _fightLog.Clear();
            _fightWinnerA = UnityEngine.Random.value < Probability(f.RatingA, f.RatingB);
            _fightLog.Add("Les deux hommes se jaugent sous l'ampoule...");
        }

        private static readonly string[] Blows =
        {
            "{0} place un crochet au foie.", "{0} ouvre l'arcade de {1}.", "{1} vacille sur un direct de {0}.",
            "{0} enchaîne deux uppercuts.", "La foule hurle : {0} met {1} au sol !", "{1} se relève, le nez en sang.",
            "{0} esquive et contre.", "{1} tente un coup de tête, {0} l'évite."
        };

        private void RunFight(Fight f)
        {
            float before = _fightTime;
            _fightTime += Mathf.Min(Time.unscaledDeltaTime, 0.05f);
            int stepsBefore = Mathf.FloorToInt(before / 1.1f);
            int steps = Mathf.FloorToInt(_fightTime / 1.1f);
            if (steps == stepsBefore) return;

            string winner = _fightWinnerA ? f.A : f.B;
            string loser = _fightWinnerA ? f.B : f.A;

            if (steps < 6)
            {
                bool winnerStrikes = UnityEngine.Random.value < 0.6f;
                string line = Blows[UnityEngine.Random.Range(0, Blows.Length)];
                _fightLog.Add(string.Format(line, winnerStrikes ? winner : loser, winnerStrikes ? loser : winner));
                PlayTick();
                return;
            }

            _fightRunning = false;
            _fightOver = true;
            _fightLog.Add("K.O. ! " + winner + " l'emporte.");

            bool won = (_fightPick == 0) == _fightWinnerA;
            int payout = won ? Mathf.RoundToInt(_fightStake * (_fightPick == 0 ? f.OddsA : f.OddsB)) : 0;
            Win(_fightStake, payout, won ? "Pari gagné : +" + payout + " €" : "Pari perdu.");
        }

        // ------------------------------------------------------------------ salle, immobilier, banque, mails

        private void DrawSport(Rect area, float u)
        {
            _registeredRows = 0;
            float y = area.y;
            int price = _progress != null ? _progress.TrainingPrice : 0;
            if (Row(NextRow(ref y, area.x, area.width * 0.5f, 80f * u, u), "RÉSERVER UNE SEMAINE DE COACHING — " + price + " €",
                    "+1 point d'entraînement (à dépenser à l'armoire).", _progress != null && _progress.Money >= price, u))
            {
                if (_progress.BuyTraining())
                {
                    PlayCash();
                    Toast("Séances réservées : +1 point.", false);
                }
            }

            _registeredRows++;
            if (Row(NextRow(ref y, area.x, area.width * 0.5f, 56f * u, u), "RETOUR", null, true, u)) Go(AppId.Bureau);
            _registeredRows++;

            if (_progress != null)
            {
                Text(new Rect(area.x, y + 20f * u, area.width * 0.5f, 30f * u), "Points disponibles : " + _progress.SkillPoints, 20, FontStyle.Bold,
                    TextAnchor.MiddleLeft, Good, u);
            }
        }

        private void DrawEstate(Rect area, float u)
        {
            _registeredRows = 0;
            float y = area.y;
            float w = area.width * 0.6f;

            if (Row(NextRow(ref y, area.x, w, 80f * u, u), "MOTEL — CHAMBRE 3" + (_progress != null && _progress.Home == "Motel" ? "   (chez toi)" : ""),
                    "Toujours là. Revenir y habiter.", _progress != null && _progress.Home != "Motel", u))
            {
                MoveTo("Motel");
            }

            _registeredRows++;

            for (int i = 0; i < _properties.Length; i++)
            {
                Property p = _properties[i];
                bool owned = _progress != null && _progress.Owns(p.name);
                bool here = _progress != null && _progress.Home == p.name;
                bool available = _homes == null || _homes.Has(p.name);
                string label = p.name.ToUpperInvariant() + "  —  " + (here ? "chez toi" : owned ? "à toi : s'y installer" : p.price.ToString("N0") + " €");
                bool enabled = available && !here && (owned || (_progress != null && _progress.Money >= p.price));

                if (Row(NextRow(ref y, area.x, w, 80f * u, u), label, available ? p.pitch : "Pas encore sur le marché.", enabled, u))
                {
                    if (!owned)
                    {
                        if (_progress.Spend(p.price, "Achat " + p.name))
                        {
                            _progress.Acquire(p.name);
                            PlayCash();
                        }
                        else
                        {
                            Toast("Pas assez d'argent.", true);
                            continue;
                        }
                    }

                    MoveTo(p.name);
                }

                _registeredRows++;
            }

            if (Row(NextRow(ref y, area.x, w, 56f * u, u), "RETOUR", null, true, u)) Go(AppId.Bureau);
            _registeredRows++;
        }

        private void MoveTo(string home)
        {
            if (_progress == null) return;
            _progress.MoveTo(home);
            if (_homes != null) _homes.Apply();
            Toast("Tu habites maintenant : " + home + ". Tu t'y réveilleras.", false);
        }

        private int _openArticle = -1;

        /// <summary>Le journal de la ville : ses articles, les plus récents d'abord.</summary>
        private void DrawJournal(Rect area, float u)
        {
            _registeredRows = 0;
            IReadOnlyList<NewsFeed.Article> articles = NewsFeed.Articles;

            if (_openArticle >= 0 && _openArticle < articles.Count)
            {
                NewsFeed.Article a = articles[_openArticle];
                Text(new Rect(area.x, area.y, area.width * 0.8f, 40f * u), a.title, 28, FontStyle.Bold, TextAnchor.MiddleLeft, Ink, u);
                Text(new Rect(area.x, area.y + 44f * u, area.width * 0.8f, 26f * u), "Hyland Info  ·  jour " + a.day, 16, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, u);
                Text(new Rect(area.x, area.y + 84f * u, area.width * 0.72f, 60f * u), a.lead, 20, FontStyle.Bold, TextAnchor.UpperLeft, Ink, u, true);
                Text(new Rect(area.x, area.y + 150f * u, area.width * 0.72f, area.height - 240f * u), a.body, 19, FontStyle.Normal, TextAnchor.UpperLeft, Ink, u, true);
                if (Row(new Rect(area.x, area.yMax - 70f * u, area.width * 0.3f, 56f * u), "RETOUR", null, true, u)) _openArticle = -1;
                _registeredRows++;
                return;
            }

            float y = area.y;
            if (articles.Count == 0)
            {
                Text(new Rect(area.x, y, area.width, 40f * u), "Rien de neuf à Hyland. Pour l'instant.", 20, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, u);
                y += 60f * u;
            }

            for (int i = 0; i < articles.Count && i < 8; i++)
            {
                NewsFeed.Article a = articles[i];
                if (Row(NextRow(ref y, area.x, area.width * 0.75f, 64f * u, u), (a.unread ? "● " : "") + a.title, a.lead, true, u))
                {
                    a.unread = false;
                    _openArticle = i;
                    Focus = 0;
                }

                _registeredRows++;
            }

            if (Row(NextRow(ref y, area.x, area.width * 0.3f, 56f * u, u), "RETOUR", null, true, u)) Go(AppId.Bureau);
            _registeredRows++;
        }

        private void DrawMails(Rect area, float u)
        {
            _registeredRows = 0;

            if (_openMail >= 0 && _openMail < _mails.Count)
            {
                Mail m = _mails[_openMail];
                Text(new Rect(area.x, area.y, area.width, 36f * u), m.Subject, 28, FontStyle.Bold, TextAnchor.MiddleLeft, Ink, u);
                Text(new Rect(area.x, area.y + 40f * u, area.width, 26f * u), "De : " + m.From, 17, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, u);
                Text(new Rect(area.x, area.y + 86f * u, area.width * 0.7f, area.height - 200f * u), m.Body, 20, FontStyle.Normal, TextAnchor.UpperLeft, Ink, u, true);
                if (Row(new Rect(area.x, area.yMax - 70f * u, area.width * 0.3f, 56f * u), "RETOUR", null, true, u)) _openMail = -1;
                _registeredRows++;
                return;
            }

            float y = area.y;
            for (int i = 0; i < _mails.Count && i < 8; i++)
            {
                Mail m = _mails[i];
                if (Row(NextRow(ref y, area.x, area.width * 0.7f, 64f * u, u), (m.Unread ? "● " : "") + m.Subject, "De : " + m.From, true, u))
                {
                    m.Unread = false;
                    _mails[i] = m;
                    _openMail = i;
                    Focus = 0;
                }

                _registeredRows++;
            }

            if (Row(NextRow(ref y, area.x, area.width * 0.3f, 56f * u, u), "RETOUR", null, true, u)) Go(AppId.Bureau);
            _registeredRows++;
        }
    }
}
