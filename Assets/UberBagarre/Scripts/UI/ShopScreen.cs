using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// L'écran d'un magasin, ouvert au comptoir. À gauche ce qu'on y fait (acheter, manger,
    /// boire, se soigner, jouer…), à droite où l'on en est (argent, vie, blessures,
    /// réputation, ce qu'on vient d'apprendre).
    ///
    /// Les commerces particuliers ont leurs propres lignes :
    /// - vêtements : les pièces du catalogue, rangée par rangée (◄ ► pour parcourir) ;
    /// - concession : les voitures de la ville, qui remplacent la tienne ;
    /// - agence immobilière : les logements (les mêmes que sur l'ordinateur) ;
    /// - casino : machine à sous et roulette, pour de vrai (avec ton argent) ;
    /// - salle de boxe : une semaine de coaching (un point d'entraînement).
    /// </summary>
    public class ShopScreen : FullScreenPanel
    {
        [SerializeField] private VehicleCatalog _vehicles;
        [SerializeField] private HomeRegistry _homes;
        [SerializeField] private ComputerScreen _computer;
        [SerializeField] private SubtitleDisplay _subtitles;

        private Shop _shop;
        private ShopItem[] _items = new ShopItem[0];
        private string _note;
        private readonly int[] _browse = new int[4];
        private int _rows;

        // Casino
        private static readonly string[] Symbols = { "7", "BAR", "CERISE", "CLOCHE", "CITRON" };
        private int[] _reels = { 0, 1, 2 };
        private float _spinUntil;
        private int _spinStake;
        private int _rouletteNumber = -1;

        protected override string Title { get { return _shop != null ? _shop.DisplayName.ToUpperInvariant() : "MAGASIN"; } }

        protected override string Subtitle
        {
            get
            {
                if (_shop == null) return null;
                return ShopCatalog.Describe(_shop.Kind) + "   ·   « " + ShopCatalog.Greeting(_shop.Kind) + " »";
            }
        }

        public void Open(Shop shop)
        {
            if (shop == null || IsOpen || AnyOpen) return;
            _shop = shop;
            _items = ShopCatalog.Items(shop.Kind);
            _note = null;
            _rouletteNumber = -1;
            for (int i = 0; i < _browse.Length; i++) _browse[i] = 0;

            if (_progress != null)
            {
                PlayerProgress.Outfit o = _progress.CurrentOutfit;
                _browse[0] = o.top;
                _browse[1] = o.shirt;
                _browse[2] = o.pants;
                _browse[3] = o.shoes;
            }

            Open();
        }

        protected override void OnHorizontal(int direction)
        {
            if (_shop == null || _shop.Kind != ShopKind.Vetements) return;
            int family = Focus;
            if (family < 0 || family > 3) return;
            PlayerWardrobe.Item[] items = PlayerWardrobe.Family(family);
            _browse[family] = (_browse[family] + direction + items.Length) % items.Length;
            PlayTick();
        }

        protected override void DrawContent(Rect area, float u)
        {
            if (_shop == null || _progress == null) return;

            float listWidth = area.width * 0.58f;
            float y = area.y;
            _rows = 0;

            switch (_shop.Kind)
            {
                case ShopKind.Vetements: y = DrawClothes(area.x, y, listWidth, u); break;
                case ShopKind.Concession: y = DrawCars(area.x, y, listWidth, u); break;
                case ShopKind.Immobilier: y = DrawHomes(area.x, y, listWidth, u); break;
                case ShopKind.Casino: y = DrawCasino(area.x, y, listWidth, u); break;
                case ShopKind.SalleDeBoxe: y = DrawCoaching(area.x, y, listWidth, u); break;
            }

            float rowHeight = 74f * u;
            for (int i = 0; i < _items.Length; i++)
            {
                if (y + rowHeight > area.yMax) break;
                DrawItem(_items[i], new Rect(area.x, y, listWidth, rowHeight - 8f * u), u);
                y += rowHeight;
            }

            if (Button(new Rect(area.x, Mathf.Min(y, area.yMax - 52f * u), listWidth, 46f * u), "PARTIR", null, true, u)) Close();

            DrawStatus(new Rect(area.x + listWidth + 32f * u, area.y, area.width - listWidth - 32f * u, area.height), u);
        }

        // ------------------------------------------------------------------ articles

        private void DrawItem(ShopItem item, Rect row, float u)
        {
            string key = item.Key(_shop.Kind);
            bool doneToday = item.daily && _progress.HasFlag(key + ":j" + _progress.Day);
            bool owned = item.once && _progress.IsUnlocked(key);
            bool affordable = item.price <= 0 || _progress.Money >= item.price;

            string price = item.effect == ShopEffect.Sell ? "+" + Mathf.RoundToInt(item.amount) + " €" : item.price <= 0 ? "gratuit" : item.price + " €";
            string state = owned ? "   (déjà fait)" : doneToday ? "   (demain)" : "";
            bool enabled = !owned && !doneToday && affordable;

            if (!Button(row, item.name + "  —  " + price + state, item.detail, enabled, u)) return;

            if (item.price > 0 && !_progress.Spend(item.price, _shop.DisplayName + " — " + item.name))
            {
                Toast("Pas assez d'argent.", true);
                return;
            }

            if (!Apply(item))
            {
                if (item.price > 0) _progress.AddMoney(item.price, "Remboursement " + _shop.DisplayName);
                return;
            }

            if (item.food > 0f && item.effect != ShopEffect.Groceries) _progress.Eat(item.food);

            if (item.daily) _progress.SetFlag(key + ":j" + _progress.Day);
            if (item.once) _progress.Unlock(key);
            if (item.price > 0 || item.effect == ShopEffect.Sell) PlayCash();
        }

        /// <summary>L'effet d'un article. Faux = rien n'a été fait (remboursé).</summary>
        private bool Apply(ShopItem item)
        {
            PlayerBuffs buffs = PlayerBuffs.Instance;
            switch (item.effect)
            {
                case ShopEffect.Heal:
                    if (buffs != null) buffs.Heal(item.amount);
                    Toast(item.name + " : +" + Mathf.RoundToInt(item.amount) + " PV.", false);
                    return true;

                case ShopEffect.HealFull:
                    if (buffs != null) buffs.HealFull();
                    Toast("Vie et endurance au maximum.", false);
                    return true;

                case ShopEffect.Buff:
                    if (buffs != null) buffs.Add(item.name, item.stat, item.amount, item.minutes);
                    Toast(item.name + " : effet pendant " + Mathf.RoundToInt(item.minutes) + " min.", false);
                    return true;

                case ShopEffect.Reputation:
                    if (item.amount > 0f) _progress.ChangeReputation(Mathf.RoundToInt(item.amount), item.name);
                    Toast(item.amount > 0f ? "+" + Mathf.RoundToInt(item.amount) + " réputation." : "C'était sympa.", false);
                    return true;

                case ShopEffect.Injury:
                    if (!_progress.HealInjuries(item.amount <= 0f))
                    {
                        Toast("Tu n'as aucune blessure à soigner.", true);
                        return false;
                    }

                    if (buffs != null) buffs.HealFull();
                    Toast(item.amount <= 0f ? "Toutes tes blessures sont soignées." : "Une blessure en moins.", false);
                    return true;

                case ShopEffect.Experience:
                    _progress.AddExperience(Mathf.RoundToInt(item.amount));
                    Toast("+" + Mathf.RoundToInt(item.amount) + " XP.", false);
                    return true;

                case ShopEffect.Parcel:
                    int gift = Mathf.RoundToInt(Random.Range(item.amount * 0.3f, item.amount));
                    _progress.AddMoney(gift, "Colis — La Poste");
                    PlayCash();
                    Toast("Une enveloppe : " + gift + " € et un mot d'un fan.", false);
                    return true;

                case ShopEffect.Sell:
                    _progress.AddMoney(Mathf.RoundToInt(item.amount), _shop.DisplayName + " — " + item.name);
                    Toast("Vendu : +" + Mathf.RoundToInt(item.amount) + " €.", false);
                    return true;

                case ShopEffect.Groceries:
                    _progress.AddMeals(Mathf.RoundToInt(item.amount));
                    Toast("+" + Mathf.RoundToInt(item.amount) + " repas dans le frigo (" + _progress.Meals + " en tout).", false);
                    return true;

                case ShopEffect.ClearRecord:
                {
                    int weight = _progress.RecordWeight;
                    if (weight <= 0)
                    {
                        Toast("Ton casier est vierge. Revenez quand vous aurez fait quelque chose.", true);
                        return false;
                    }

                    int fee = 300 + 120 * weight;
                    if (!_progress.Spend(fee, "Honoraires — Maître Lenoir"))
                    {
                        Toast("Honoraires : " + fee + " €. Tu ne les as pas.", true);
                        return false;
                    }

                    _progress.ClearRecord();
                    Toast("Casier nettoyé (" + fee + " €). « Vous n'avez jamais existé. »", false);
                    return true;
                }

                case ShopEffect.Lockpicks:
                    _progress.AddLockpicks(Mathf.RoundToInt(item.amount));
                    Toast("+" + Mathf.RoundToInt(item.amount) + " crochets (" + _progress.Lockpicks + ").", false);
                    return true;

                case ShopEffect.Medkit:
                    _progress.AddMedkit();
                    Toast("Trousse rangée pour la maison (" + _progress.Medkits + ").", false);
                    return true;

                case ShopEffect.Rumor:
                    _note = ShopCatalog.Rumor(Random.Range(0, 1000));
                    if (_subtitles != null) _subtitles.Play(DialogueLine.Say("", _note));
                    return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ vêtements

        private float DrawClothes(float x, float y, float w, float u)
        {
            PlayerProgress.Outfit worn = _progress.CurrentOutfit;
            int[] current = { worn.top, worn.shirt, worn.pants, worn.shoes };

            for (int family = 0; family < 4; family++)
            {
                PlayerWardrobe.Item[] items = PlayerWardrobe.Family(family);
                int index = Mathf.Clamp(_browse[family], 0, items.Length - 1);
                PlayerWardrobe.Item item = items[index];
                string key = PlayerWardrobe.Key(family, index);
                bool owned = _progress.IsUnlocked(key) || item.Price <= 0;
                bool levelOk = _progress.Level >= item.Level;
                bool wearing = current[family] == index;

                string state = wearing ? "PORTÉ" : owned ? "Entrée : porter" : !levelOk ? "Niveau " + item.Level + " requis" : "Entrée : acheter " + item.Price + " €";
                Rect row = new Rect(x, y, w, 70f * u);

                if (family > 0)
                {
                    Rect swatch = new Rect(row.xMax - 56f * u, row.y + 14f * u, 40f * u, row.height - 28f * u);
                    GuiKit.Fill(swatch, item.Color);
                    GuiKit.Outline(swatch, 2f, new Color(1f, 1f, 1f, 0.4f));
                }

                bool can = !wearing && (owned || (levelOk && _progress.Money >= item.Price));
                if (Button(row, PlayerWardrobe.FamilyName(family) + "   ◄ " + item.Name + " ►", state + "   ·   ◄ ► pour parcourir", can || wearing, u) && !wearing)
                {
                    if (!owned)
                    {
                        if (!levelOk)
                        {
                            Toast("Il faut être niveau " + item.Level + ".", true);
                        }
                        else if (_progress.Spend(item.Price, _shop.DisplayName + " — " + item.Name))
                        {
                            _progress.Unlock(key);
                            PlayCash();
                            Wear(family, index);
                            Toast(item.Name + " acheté et porté.", false);
                        }
                        else
                        {
                            Toast("Pas assez d'argent.", true);
                        }
                    }
                    else
                    {
                        Wear(family, index);
                        Toast(item.Name + " : porté.", false);
                    }
                }

                y += 78f * u;
            }

            return y + 6f * u;
        }

        private void Wear(int family, int index)
        {
            PlayerProgress.Outfit o = _progress.CurrentOutfit;
            PlayerProgress.Outfit next = new PlayerProgress.Outfit { top = o.top, shirt = o.shirt, pants = o.pants, shoes = o.shoes };
            switch (family)
            {
                case 0: next.top = index; break;
                case 1: next.shirt = index; break;
                case 2: next.pants = index; break;
                default: next.shoes = index; break;
            }

            _progress.Wear(next);
        }

        // ------------------------------------------------------------------ concession

        private float DrawCars(float x, float y, float w, float u)
        {
            if (_vehicles == null) return y;

            VehicleCatalog.Entry mine = _vehicles.Find(_vehicles.PersonalKey);
            for (int i = 0; i < _vehicles.Entries.Count; i++)
            {
                VehicleCatalog.Entry e = _vehicles.Entries[i];
                bool current = e.key == _vehicles.PersonalKey;
                string label = e.label.ToUpperInvariant() + "  —  " + (current ? "c'est la tienne" : e.price.ToString("N0") + " €");
                string detail = current
                    ? "Garée devant le motel."
                    : "Remplace ta " + (mine != null ? mine.label.ToLowerInvariant() : "voiture") + ". Livrée devant le motel.";

                if (Button(new Rect(x, y, w, 66f * u), label, detail, !current && _progress.Money >= e.price, u))
                {
                    if (_vehicles.Buy(e.key))
                    {
                        PlayCash();
                        Toast(e.label + " achetée ! Elle t'attend devant le motel.", false);
                    }
                    else
                    {
                        Toast("Pas assez d'argent.", true);
                    }
                }

                y += 72f * u;
            }

            return y + 6f * u;
        }

        // ------------------------------------------------------------------ immobilier

        private float DrawHomes(float x, float y, float w, float u)
        {
            ComputerScreen.Property[] listings = _computer != null ? _computer.Listings : null;
            if (listings == null) return y;

            for (int i = 0; i < listings.Length; i++)
            {
                ComputerScreen.Property p = listings[i];
                bool owned = _progress.Owns(p.name);
                bool here = _progress.Home == p.name;
                bool available = _homes == null || _homes.Has(p.name);
                string label = p.name.ToUpperInvariant() + "  —  " + (here ? "chez toi" : owned ? "à toi : s'y installer" : p.price.ToString("N0") + " €");
                bool enabled = available && !here && (owned || _progress.Money >= p.price);

                if (Button(new Rect(x, y, w, 70f * u), label, available ? p.pitch : "Pas encore sur le marché.", enabled, u))
                {
                    if (!owned)
                    {
                        if (!_progress.Spend(p.price, "Hyland Immobilier — " + p.name))
                        {
                            Toast("Pas assez d'argent.", true);
                            y += 78f * u;
                            continue;
                        }

                        _progress.Acquire(p.name);
                        PlayCash();
                    }

                    _progress.MoveTo(p.name);
                    if (_homes != null) _homes.Apply();
                    Toast("Tu habites maintenant : " + p.name + ".", false);
                }

                y += 78f * u;
            }

            return y + 6f * u;
        }

        // ------------------------------------------------------------------ salle de boxe

        private float DrawCoaching(float x, float y, float w, float u)
        {
            int price = _progress.TrainingPrice;
            if (Button(new Rect(x, y, w, 66f * u), "SEMAINE DE COACHING  —  " + price + " €",
                    "+1 point d'entraînement, à dépenser à l'armoire. Le prix monte à chaque fois.", _progress.Money >= price, u))
            {
                if (_progress.BuyTraining())
                {
                    PlayCash();
                    Toast("+1 point d'entraînement.", false);
                }
                else
                {
                    Toast("Pas assez d'argent.", true);
                }
            }

            return y + 74f * u;
        }

        // ------------------------------------------------------------------ casino

        private float DrawCasino(float x, float y, float w, float u)
        {
            bool spinning = Time.unscaledTime < _spinUntil;
            if (spinning)
            {
                for (int i = 0; i < 3; i++) _reels[i] = Random.Range(0, Symbols.Length);
            }
            else if (_spinStake > 0)
            {
                Settle();
            }

            // Les rouleaux.
            Rect frame = new Rect(x, y, w, 90f * u);
            GuiKit.Fill(frame, new Color(0.12f, 0.02f, 0.06f, 0.9f));
            GuiKit.Outline(frame, 2f, Warn);
            float cell = (w - 40f * u) / 3f;
            for (int i = 0; i < 3; i++)
            {
                Rect r = new Rect(x + 10f * u + i * (cell + 10f * u), y + 10f * u, cell, 70f * u);
                GuiKit.Fill(r, new Color(1f, 1f, 1f, 0.92f));
                Text(r, Symbols[_reels[i]], 30, FontStyle.Bold, TextAnchor.MiddleCenter, new Color(0.7f, 0.05f, 0.1f), u);
            }

            y += 100f * u;

            int[] stakes = { 10, 50, 100 };
            float bw = (w - 20f * u) / 3f;
            for (int i = 0; i < stakes.Length; i++)
            {
                Rect b = new Rect(x + i * (bw + 10f * u), y, bw, 56f * u);
                if (Button(b, "MACHINE — " + stakes[i] + " €", null, !spinning && _progress.Money >= stakes[i], u)) Spin(stakes[i]);
            }

            y += 64f * u;

            string[] bets = { "ROULETTE : ROUGE (x2) — 25 €", "ROULETTE : NOIR (x2) — 25 €", "ROULETTE : LE 7 (x36) — 10 €" };
            int[] prices = { 25, 25, 10 };
            for (int i = 0; i < bets.Length; i++)
            {
                string detail = i == 0 && _rouletteNumber >= 0
                    ? "Dernier tirage : " + _rouletteNumber + (_rouletteNumber == 0 ? " (vert)" : IsRed(_rouletteNumber) ? " (rouge)" : " (noir)")
                    : null;
                if (Button(new Rect(x, y, w, 56f * u), bets[i], detail, !spinning && _progress.Money >= prices[i], u)) Roulette(i, prices[i]);
                y += 62f * u;
            }

            return y + 6f * u;
        }

        private void Spin(int stake)
        {
            if (!_progress.Spend(stake, "Casino Royal — mise"))
            {
                Toast("Pas assez d'argent.", true);
                return;
            }

            _spinStake = stake;
            _spinUntil = Time.unscaledTime + 1.2f;
        }

        private void Settle()
        {
            int stake = _spinStake;
            _spinStake = 0;

            // Tirage honnête… à la manière d'un casino : un peu moins de chances pour le 7.
            for (int i = 0; i < 3; i++)
            {
                float r = Random.value;
                _reels[i] = r < 0.1f ? 0 : r < 0.3f ? 1 : r < 0.55f ? 2 : r < 0.78f ? 3 : 4;
            }

            int win = 0;
            if (_reels[0] == _reels[1] && _reels[1] == _reels[2]) win = _reels[0] == 0 ? stake * 25 : stake * 8;
            else if (_reels[0] == _reels[1] || _reels[1] == _reels[2]) win = Mathf.RoundToInt(stake * 1.5f);

            if (win > 0)
            {
                _progress.AddMoney(win, "Casino Royal — gain");
                PlayCash();
                Toast("Gagné : " + win + " € !", false);
            }
            else
            {
                Toast("Perdu. La machine te regarde.", true);
            }

            _progress.CountCasino(win, stake);
        }

        private void Roulette(int bet, int stake)
        {
            if (!_progress.Spend(stake, "Casino Royal — mise"))
            {
                Toast("Pas assez d'argent.", true);
                return;
            }

            _rouletteNumber = Random.Range(0, 37);
            bool red = IsRed(_rouletteNumber);
            int win = 0;
            if (bet == 0 && _rouletteNumber != 0 && red) win = stake * 2;
            if (bet == 1 && _rouletteNumber != 0 && !red) win = stake * 2;
            if (bet == 2 && _rouletteNumber == 7) win = stake * 36;

            if (win > 0)
            {
                _progress.AddMoney(win, "Casino Royal — gain");
                PlayCash();
                Toast("Le " + _rouletteNumber + " ! Gagné : " + win + " €.", false);
            }
            else
            {
                Toast("Le " + _rouletteNumber + ". Perdu.", true);
            }

            _progress.CountCasino(win, stake);
        }

        private static bool IsRed(int n)
        {
            int[] reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
            for (int i = 0; i < reds.Length; i++)
            {
                if (reds[i] == n) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ état du joueur

        private void DrawStatus(Rect area, float u)
        {
            GuiKit.Fill(area, new Color(1f, 1f, 1f, 0.03f));
            float x = area.x + 20f * u;
            float w = area.width - 40f * u;
            float y = area.y + 18f * u;

            Text(new Rect(x, y, w, 30f * u), "TOI", 22, FontStyle.Bold, TextAnchor.MiddleLeft, Accent, u);
            y += 44f * u;

            Text(new Rect(x, y, w, 26f * u), "Argent : " + _progress.Money.ToString("N0") + " €", 19, FontStyle.Bold, TextAnchor.MiddleLeft, Good, u);
            y += 34f * u;

            int injuries = _progress.Injuries;
            Text(new Rect(x, y, w, 26f * u), "Blessures : " + injuries + " / " + PlayerProgress.MaxInjuries, 17, FontStyle.Normal,
                TextAnchor.MiddleLeft, injuries >= PlayerProgress.MaxInjuries - 1 ? Bad : injuries > 0 ? Warn : Ink, u);
            y += 30f * u;

            Text(new Rect(x, y, w, 26f * u), "Réputation : " + _progress.Reputation + "  (" + _progress.ReputationTitle + ")", 17,
                FontStyle.Normal, TextAnchor.MiddleLeft, Ink, u);
            y += 30f * u;

            Text(new Rect(x, y, w, 26f * u), "Niveau " + _progress.Level + "   ·   jour " + _progress.Day, 17, FontStyle.Normal,
                TextAnchor.MiddleLeft, Dim, u);
            y += 44f * u;

            if (!string.IsNullOrEmpty(_note))
            {
                Text(new Rect(x, y, w, 24f * u), "CE QU'ON T'A DIT", 15, FontStyle.Bold, TextAnchor.MiddleLeft, Warn, u);
                y += 28f * u;
                Text(new Rect(x, y, w, 120f * u), "« " + _note + " »", 17, FontStyle.Italic, TextAnchor.UpperLeft, Ink, u, true);
            }
        }

        public void Configure(VehicleCatalog vehicles, HomeRegistry homes, ComputerScreen computer, SubtitleDisplay subtitles)
        {
            _vehicles = vehicles;
            _homes = homes;
            _computer = computer;
            _subtitles = subtitles;
        }
    }
}
