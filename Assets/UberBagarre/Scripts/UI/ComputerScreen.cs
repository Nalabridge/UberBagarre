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
            Banque,
            Mails
        }

        [SerializeField] private HomeRegistry _homes;

        [SerializeField]
        private Property[] _properties =
        {
            new Property { name = "Bungalow", price = 6500, pitch = "Au bord de l'eau, ville ouest. Une vraie chambre, un conteneur, une cour." },
            new Property { name = "Manoir", price = 85000, pitch = "La colline à l'est. Un portail, une allée, la vue sur toute la ville." }
        };

        private static readonly int[] Stakes = { 10, 20, 50, 100, 200, 500, 1000 };

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
                    case AppId.Banque: return "BANQUE";
                    case AppId.Mails: return "MAILS";
                    default: return "BUREAU";
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
            _app = AppId.Bureau;
            _openMail = -1;
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
            _app = app;
            Focus = 0;
            _openMail = -1;
            if (app == AppId.Paris && _fights.Count == 0) NewFights();
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
                case AppId.Banque: DrawBank(area, u); break;
                case AppId.Mails: DrawMails(area, u); break;
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

        // ------------------------------------------------------------------ bureau

        private void DrawDesktop(Rect area, float u)
        {
            _registeredRows = 0;
            string[] names = { "CASINO ROYAL", "PARIS DE COMBAT", "SALLE DE SPORT", "IMMOBILIER", "BANQUE", "MAILS" + (UnreadMails > 0 ? "  (" + UnreadMails + ")" : "") };
            string[] details =
            {
                "Roulette, blackjack, machine à sous.", "Trois combats clandestins ce soir. Mise sur le bon.",
                "Des séances de coach : des points d'entraînement.", "Bungalow, manoir : un logement à la hauteur.",
                "Ton compte, tes gains, tes pertes.", "Le motel, l'appli, Sami..."
            };
            AppId[] apps = { AppId.Casino, AppId.Paris, AppId.Sport, AppId.Immobilier, AppId.Banque, AppId.Mails };

            float w = (area.width - 40f * u) / 3f;
            float h = 150f * u;
            for (int i = 0; i < apps.Length; i++)
            {
                Rect tile = new Rect(area.x + (i % 3) * (w + 20f * u), area.y + (i / 3) * (h + 20f * u), w, h);
                if (Button(tile, names[i], details[i], true, u)) Go(apps[i]);
                _registeredRows++;
            }

            if (_progress != null)
            {
                Text(new Rect(area.x, area.y + 2f * (h + 20f * u) + 20f * u, area.width, 30f * u),
                    "Jour " + _progress.Day + "  ·  réputation " + _progress.Reputation + " (" + _progress.ReputationTitle + ")  ·  niveau " + _progress.Level +
                    "  ·  logement : " + _progress.Home, 17, FontStyle.Normal, TextAnchor.MiddleLeft, Dim, u);
            }
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
            if (_progress == null || !_progress.Spend(amount))
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
                _progress.AddMoney(payout);
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
                        if (_progress.Spend(p.price))
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

        private void DrawBank(Rect area, float u)
        {
            _registeredRows = 0;
            if (_progress == null) return;

            float y = area.y;
            Text(new Rect(area.x, y, area.width, 50f * u), "SOLDE : " + _progress.Money + " €", 36, FontStyle.Bold, TextAnchor.MiddleLeft, Good, u);
            y += 70f * u;

            string[] lines =
            {
                "Courses payées : " + _progress.Contracts + "   ·   ratées : " + _progress.Failures,
                "Casino : +" + _progress.CasinoWon + " € gagnés   ·   -" + _progress.CasinoLost + " € perdus",
                "Logement : " + _progress.Home + "   ·   jour " + _progress.Day,
                "Réputation : " + _progress.Reputation + " / 100 (" + _progress.ReputationTitle + ")   ·   note " + _progress.Rating.ToString("0.0") + " / 5"
            };

            for (int i = 0; i < lines.Length; i++)
            {
                Text(new Rect(area.x, y, area.width, 32f * u), lines[i], 20, FontStyle.Normal, TextAnchor.MiddleLeft, Ink, u);
                y += 38f * u;
            }

            if (Row(new Rect(area.x, y + 20f * u, area.width * 0.4f, 56f * u), "RETOUR", null, true, u)) Go(AppId.Bureau);
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
