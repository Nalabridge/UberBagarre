using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// L'armoire de la planque : deux voies de progression.
    ///
    /// ENTRAÎNEMENT — cinq qualités (Puissance, Endurance, Encaisse, Vitesse, Technique), cinq
    /// paliers chacune, chaque palier nommé et décrit. Les points viennent des niveaux (deux
    /// par niveau) ou s'achètent à la salle (de plus en plus cher).
    ///
    /// TENUE — le haut, sa couleur, le pantalon, les chaussures. Chaque article demande un
    /// niveau, et s'achète une fois ; ensuite on le porte quand on veut. Le mannequin à droite
    /// montre ce qu'on porte.
    /// </summary>
    public class WardrobeScreen : FullScreenPanel
    {

        private int _tab;
        private readonly int[] _browse = new int[4];

        protected override string Title { get { return _tab == 0 ? "ENTRAÎNEMENT" : "TENUE"; } }

        protected override string Subtitle
        {
            get
            {
                return _tab == 0
                    ? "Chaque niveau donne 2 points. Chaque palier coûte autant de points que son rang."
                    : "Chaque article s'achète une fois, puis se porte quand on veut.";
            }
        }

        protected override void OnOpened()
        {
            if (_progress == null) return;
            PlayerProgress.Outfit o = _progress.CurrentOutfit;
            _browse[0] = o.top;
            _browse[1] = o.shirt;
            _browse[2] = o.pants;
            _browse[3] = o.shoes;
        }

        protected override void OnHorizontal(int direction)
        {
            // Sur la tenue, gauche / droite font défiler l'article de la ligne choisie ;
            // sur la première ligne (les onglets), ils changent d'onglet.
            if (Focus == 0 || _tab == 0)
            {
                _tab = 1 - _tab;
                Focus = 0;
                PlayTick();
                return;
            }

            int family = Focus - 1;
            if (family < 0 || family > 3) return;

            PlayerWardrobe.Item[] items = PlayerWardrobe.Family(family);
            _browse[family] = (_browse[family] + direction + items.Length) % items.Length;
            PlayTick();
        }

        protected override void DrawContent(Rect area, float u)
        {
            if (_progress == null) return;

            // --- onglets
            Rect tabs = new Rect(area.x, area.y, area.width * 0.45f, 44f * u);
            if (Button(tabs, _tab == 0 ? "◄  ENTRAÎNEMENT  ·  tenue  ►" : "◄  entraînement  ·  TENUE  ►", null, true, u))
            {
                _tab = 1 - _tab;
            }

            Rect body = new Rect(area.x, area.y + 64f * u, area.width, area.height - 64f * u);
            if (_tab == 0) DrawTraining(body, u);
            else DrawOutfit(body, u);
        }

        // ------------------------------------------------------------------ entraînement

        private void DrawTraining(Rect area, float u)
        {
            float left = area.width * 0.52f;
            Text(new Rect(area.x, area.y, left, 30f * u), "POINTS À DÉPENSER : " + _progress.SkillPoints, 22, FontStyle.Bold, TextAnchor.MiddleLeft,
                _progress.SkillPoints > 0 ? Good : Dim, u);

            float y = area.y + 44f * u;
            float h = 74f * u;
            Stat shown = Stat.Puissance;

            for (int i = 0; i < 5; i++)
            {
                Stat stat = (Stat)i;
                int level = _progress.StatLevel(stat);
                bool max = level >= PlayerProgress.MaxStatLevel;
                string bars = new string('■', level) + new string('□', PlayerProgress.MaxStatLevel - level);
                string label = PlayerUpgrades.StatName(stat) + "   " + bars;
                string detail = max
                    ? "Maîtrisé. " + PlayerUpgrades.StatPitch(stat)
                    : "Palier " + (level + 1) + " « " + PlayerUpgrades.Paths[i][level].Name + " » — " + _progress.NextStatCost(stat) + " point(s) : " +
                      PlayerUpgrades.Paths[i][level].Effect;

                Rect row = new Rect(area.x, y, left, h - 8f * u);
                if (Button(row, label, detail, !max && _progress.CanTrain(stat), u))
                {
                    if (_progress.Train(stat)) Toast(PlayerUpgrades.StatName(stat) + " : " + PlayerUpgrades.Paths[i][level].Name + " !", false);
                }

                if (Focus == i + 1) shown = stat;
                y += h;
            }

            int price = _progress.TrainingPrice;
            Rect buy = new Rect(area.x, y + 6f * u, left, 64f * u);
            if (Button(buy, "ACHETER UN POINT À LA SALLE  —  " + price + " €", "Un coach, une semaine de séances. Le prix monte à chaque fois.",
                    _progress.Money >= price, u))
            {
                if (_progress.BuyTraining())
                {
                    PlayCash();
                    Toast("+1 point d'entraînement.", false);
                }
            }

            // --- la voie choisie, palier par palier
            Rect path = new Rect(area.x + left + 40f * u, area.y, area.width - left - 40f * u, area.height);
            GuiKit.Fill(path, new Color(1f, 1f, 1f, 0.04f));
            int s = (int)shown;
            Text(new Rect(path.x + 20f * u, path.y + 14f * u, path.width - 40f * u, 36f * u), PlayerUpgrades.StatName(shown), 28, FontStyle.Bold,
                TextAnchor.MiddleLeft, Accent, u);
            Text(new Rect(path.x + 20f * u, path.y + 52f * u, path.width - 40f * u, 26f * u), PlayerUpgrades.StatPitch(shown), 17, FontStyle.Italic,
                TextAnchor.MiddleLeft, Dim, u);

            int current = _progress.StatLevel(shown);
            for (int p = 0; p < PlayerProgress.MaxStatLevel; p++)
            {
                bool done = p < current;
                bool next = p == current;
                float py = path.y + 96f * u + p * 70f * u;
                Rect step = new Rect(path.x + 20f * u, py, path.width - 40f * u, 62f * u);
                GuiKit.Fill(step, done ? new Color(Good.r, Good.g, Good.b, 0.12f) : next ? new Color(Warn.r, Warn.g, Warn.b, 0.12f) : new Color(1f, 1f, 1f, 0.03f));
                Text(new Rect(step.x + 14f * u, step.y + 4f * u, step.width - 28f * u, 28f * u),
                    (done ? "✓  " : (p + 1) + ".  ") + PlayerUpgrades.Paths[s][p].Name + (done ? "" : "   (" + (p + 1) + " pt)"), 19, FontStyle.Bold,
                    TextAnchor.MiddleLeft, done ? Good : next ? Warn : Dim, u);
                Text(new Rect(step.x + 14f * u, step.y + 30f * u, step.width - 28f * u, 28f * u), PlayerUpgrades.Paths[s][p].Effect, 15, FontStyle.Normal,
                    TextAnchor.MiddleLeft, Dim, u, true);
            }
        }

        // ------------------------------------------------------------------ tenue

        private void DrawOutfit(Rect area, float u)
        {
            float left = area.width * 0.52f;
            float y = area.y;
            float h = 86f * u;
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
                string label = PlayerWardrobe.FamilyName(family) + "   ◄ " + item.Name + " ►";
                Rect row = new Rect(area.x, y, left, h - 10f * u);
                bool can = !wearing && (owned || (levelOk && _progress.Money >= item.Price));

                if (family > 0)
                {
                    Rect swatch = new Rect(row.xMax - 60f * u, row.y + 16f * u, 44f * u, row.height - 32f * u);
                    GuiKit.Fill(swatch, item.Color);
                    GuiKit.Outline(swatch, 2f, new Color(1f, 1f, 1f, 0.4f));
                }

                if (Button(row, label, state + "   ·   " + (index + 1) + " / " + items.Length, can || wearing, u) && !wearing)
                {
                    if (!owned)
                    {
                        if (!levelOk)
                        {
                            Toast("Il faut être niveau " + item.Level + ".", true);
                        }
                        else if (_progress.Spend(item.Price, "Vêtements — " + item.Name))
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

                y += h;
            }

            Text(new Rect(area.x, y + 10f * u, left, 60f * u), "Gauche / droite pour faire défiler une ligne. Baisse les yeux en jeu : tu verras tes vêtements.",
                15, FontStyle.Italic, TextAnchor.UpperLeft, Dim, u, true);

            DrawMannequin(new Rect(area.x + left + 60f * u, area.y, area.width - left - 60f * u, area.height), u);
        }

        private void Wear(int family, int index)
        {
            PlayerProgress.Outfit o = _progress.CurrentOutfit;
            PlayerProgress.Outfit next = o.Copy();
            switch (family)
            {
                case 0: next.top = index; break;
                case 1: next.shirt = index; break;
                case 2: next.pants = index; break;
                default: next.shoes = index; break;
            }

            _progress.Wear(next);
            if (World.PoliceSystem.Instance != null) World.PoliceSystem.Instance.OutfitChanged();
        }

        /// <summary>Le mannequin : ce qu'on essaie (la ligne en cours) par-dessus ce qu'on porte.</summary>
        private void DrawMannequin(Rect area, float u)
        {
            GuiKit.Fill(area, new Color(1f, 1f, 1f, 0.03f));

            PlayerProgress.Outfit o = _progress.CurrentOutfit;
            int top = Focus == 1 ? _browse[0] : o.top;
            Color shirt = PlayerWardrobe.ShirtColors[Mathf.Clamp(Focus == 2 ? _browse[1] : o.shirt, 0, PlayerWardrobe.ShirtColors.Length - 1)].Color;
            Color pants = PlayerWardrobe.PantsColors[Mathf.Clamp(Focus == 3 ? _browse[2] : o.pants, 0, PlayerWardrobe.PantsColors.Length - 1)].Color;
            Color shoes = PlayerWardrobe.ShoeColors[Mathf.Clamp(Focus == 4 ? _browse[3] : o.shoes, 0, PlayerWardrobe.ShoeColors.Length - 1)].Color;
            Color skin = new Color(0.78f, 0.6f, 0.48f);

            float cx = area.center.x;
            float scale = Mathf.Min(area.height / 620f, area.width / 360f) * 1f;
            float y0 = area.y + 30f * scale;

            GuiKit.Disc(new Rect(cx - 34f * scale, y0, 68f * scale, 80f * scale), skin);
            Rect neck = new Rect(cx - 14f * scale, y0 + 74f * scale, 28f * scale, 18f * scale);
            GuiKit.Fill(neck, skin);

            Rect torso = new Rect(cx - 70f * scale, y0 + 90f * scale, 140f * scale, 200f * scale);
            float sleeve = top == 1 ? 1f : top == 0 ? 0.35f : 0f;
            for (int side = -1; side <= 1; side += 2)
            {
                Rect arm = new Rect(cx + side * 96f * scale - 22f * scale, y0 + 96f * scale, 44f * scale, 190f * scale);
                GuiKit.Fill(arm, skin);
                if (sleeve > 0f) GuiKit.Fill(new Rect(arm.x, arm.y, arm.width, arm.height * sleeve), shirt);
                GuiKit.Disc(new Rect(arm.x + 2f * scale, arm.yMax - 10f * scale, 40f * scale, 40f * scale), skin);
            }

            GuiKit.Fill(torso, top == 2 ? Color.Lerp(shirt, skin, 0f) : shirt);
            if (top == 2)
            {
                // Débardeur : les épaules nues.
                GuiKit.Fill(new Rect(torso.x, torso.y, 30f * scale, 40f * scale), skin);
                GuiKit.Fill(new Rect(torso.xMax - 30f * scale, torso.y, 30f * scale, 40f * scale), skin);
            }
            else if (top == 1)
            {
                // Veste : la fermeture éclair.
                GuiKit.Fill(new Rect(cx - 2f * scale, torso.y, 4f * scale, torso.height), new Color(0f, 0f, 0f, 0.35f));
            }

            Rect belt = new Rect(torso.x, torso.yMax, torso.width, 14f * scale);
            GuiKit.Fill(belt, new Color(0.08f, 0.05f, 0.04f));
            for (int side = -1; side <= 1; side += 2)
            {
                Rect leg = new Rect(cx + side * 36f * scale - 32f * scale, belt.yMax, 64f * scale, 250f * scale);
                GuiKit.Fill(leg, pants);
                GuiKit.Fill(new Rect(leg.x - 4f * scale, leg.yMax, leg.width + 16f * scale, 26f * scale), shoes);
            }
        }
    }
}
