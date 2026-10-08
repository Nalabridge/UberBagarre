using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>Ce qu'est un lieu de la carte : décide de son pictogramme et de sa couleur.</summary>
    public enum MapIcon
    {
        Generic,
        Home,
        Property,
        Club,
        Food,
        Cafe,
        Bar,
        Grocery,
        Clothes,
        Barber,
        Tattoo,
        Hardware,
        Car,
        CarWash,
        Casino,
        Pawn,
        Medical,
        Pharmacy,
        Police,
        Court,
        Lawyer,
        Mail,
        Laundry,
        Boxing,
        Range,
        Arcade,
        Estate,
        Liquor,
        BlackMarket,
        Hotel,
        Park,
        Docks,
        District,
        Farm,
        Sport,
        Parking
    }

    /// <summary>Les familles de lieux : la liste de la carte les regroupe, les filtres les trient.</summary>
    public enum MapGroup
    {
        Home,
        Food,
        Shops,
        Services,
        Leisure,
        Places
    }

    /// <summary>
    /// Les pictogrammes de la carte, à la manière des repères de GTA : un rond de couleur (la
    /// famille du lieu) et un dessin blanc ou noir (le lieu lui-même) — une fourchette et un
    /// couteau pour manger, un verre à cocktail pour un bar, des ciseaux pour le barbier…
    ///
    /// Les dessins sont tracés par le code (formes simples, suréchantillonnées pour des bords
    /// nets) : aucune image à importer, et ils restent nets à toutes les tailles d'écran.
    /// </summary>
    public static class MapIcons
    {
        private const int Size = 64;
        private static readonly Dictionary<MapIcon, Texture2D> Cache = new Dictionary<MapIcon, Texture2D>();
        private static Texture2D _disc;

        // ------------------------------------------------------------------ familles

        public static MapGroup GroupOf(MapIcon icon)
        {
            switch (icon)
            {
                case MapIcon.Home: return MapGroup.Home;
                case MapIcon.Club:
                case MapIcon.Food:
                case MapIcon.Cafe:
                case MapIcon.Bar: return MapGroup.Food;
                case MapIcon.Grocery:
                case MapIcon.Clothes:
                case MapIcon.Hardware:
                case MapIcon.Liquor:
                case MapIcon.Car:
                case MapIcon.Pawn:
                case MapIcon.BlackMarket: return MapGroup.Shops;
                case MapIcon.Barber:
                case MapIcon.Tattoo:
                case MapIcon.Laundry:
                case MapIcon.Mail:
                case MapIcon.Estate:
                case MapIcon.Lawyer:
                case MapIcon.Medical:
                case MapIcon.Pharmacy:
                case MapIcon.Hotel:
                case MapIcon.CarWash: return MapGroup.Services;
                case MapIcon.Casino:
                case MapIcon.Arcade:
                case MapIcon.Range:
                case MapIcon.Boxing:
                case MapIcon.Sport:
                case MapIcon.Park: return MapGroup.Leisure;
                default: return MapGroup.Places;
            }
        }

        public static string GroupName(MapGroup group)
        {
            switch (group)
            {
                case MapGroup.Home: return "Chez toi";
                case MapGroup.Food: return "Manger et boire";
                case MapGroup.Shops: return "Magasins";
                case MapGroup.Services: return "Services";
                case MapGroup.Leisure: return "Loisirs";
                default: return "Lieux";
            }
        }

        /// <summary>Le nom court d'une famille (les filtres).</summary>
        public static string GroupShort(MapGroup group)
        {
            switch (group)
            {
                case MapGroup.Home: return "Chez toi";
                case MapGroup.Food: return "Manger";
                case MapGroup.Shops: return "Magasins";
                case MapGroup.Services: return "Services";
                case MapGroup.Leisure: return "Loisirs";
                default: return "Lieux";
            }
        }

        public static Color GroupColor(MapGroup group)
        {
            switch (group)
            {
                case MapGroup.Home: return new Color(1f, 0.8f, 0.3f);
                case MapGroup.Food: return new Color(1f, 0.52f, 0.24f);
                case MapGroup.Shops: return new Color(0.36f, 0.64f, 1f);
                case MapGroup.Services: return new Color(0.28f, 0.8f, 0.7f);
                case MapGroup.Leisure: return new Color(1f, 0.4f, 0.6f);
                default: return new Color(0.78f, 0.8f, 0.84f);
            }
        }

        /// <summary>Ce qu'on lit sous le nom du lieu, dans la liste.</summary>
        public static string Describe(MapIcon icon)
        {
            switch (icon)
            {
                case MapIcon.Home: return "Ton logement";
                case MapIcon.Property: return "Propriété";
                case MapIcon.Club: return "Boîte de nuit";
                case MapIcon.Food: return "Restaurant";
                case MapIcon.Cafe: return "Café";
                case MapIcon.Bar: return "Bar";
                case MapIcon.Grocery: return "Épicerie";
                case MapIcon.Clothes: return "Vêtements";
                case MapIcon.Barber: return "Barbier";
                case MapIcon.Tattoo: return "Tatoueur";
                case MapIcon.Hardware: return "Quincaillerie";
                case MapIcon.Car: return "Concession auto";
                case MapIcon.CarWash: return "Lave-auto";
                case MapIcon.Casino: return "Casino";
                case MapIcon.Pawn: return "Prêteur sur gages";
                case MapIcon.Medical: return "Soins";
                case MapIcon.Pharmacy: return "Pharmacie";
                case MapIcon.Police: return "Police";
                case MapIcon.Court: return "Tribunal";
                case MapIcon.Lawyer: return "Avocat";
                case MapIcon.Mail: return "Poste";
                case MapIcon.Laundry: return "Laverie";
                case MapIcon.Boxing: return "Salle de boxe";
                case MapIcon.Range: return "Stand de tir";
                case MapIcon.Arcade: return "Salle d'arcade";
                case MapIcon.Estate: return "Agence immobilière";
                case MapIcon.Liquor: return "Caviste";
                case MapIcon.BlackMarket: return "Marché noir";
                case MapIcon.Hotel: return "Motel";
                case MapIcon.Park: return "Parc";
                case MapIcon.Docks: return "Port";
                case MapIcon.District: return "Quartier";
                case MapIcon.Farm: return "Campagne";
                case MapIcon.Sport: return "Sport";
                case MapIcon.Parking: return "Parking";
                default: return "Lieu";
            }
        }

        // ------------------------------------------------------------------ reconnaissance

        /// <summary>
        /// Le pictogramme d'un lieu : d'après son type quand le constructeur l'a donné (le nom
        /// d'un <c>ShopKind</c>, « Home »…), sinon d'après les mots de son nom.
        /// </summary>
        public static MapIcon Classify(string label, string hint)
        {
            if (!string.IsNullOrEmpty(hint))
            {
                switch (hint)
                {
                    case "Home": return MapIcon.Home;
                    case "Epicerie": return MapIcon.Grocery;
                    case "Cave": return MapIcon.Liquor;
                    case "Restaurant": return MapIcon.Food;
                    case "Cafe": return MapIcon.Cafe;
                    case "Bar": return MapIcon.Bar;
                    case "BoiteDeNuit": return MapIcon.Club;
                    case "Pharmacie": return MapIcon.Pharmacy;
                    case "Medecin": return MapIcon.Medical;
                    case "Vetements": return MapIcon.Clothes;
                    case "Barbier": return MapIcon.Barber;
                    case "Tatoueur": return MapIcon.Tattoo;
                    case "Quincaillerie": return MapIcon.Hardware;
                    case "MarcheNoir": return MapIcon.BlackMarket;
                    case "Casino": return MapIcon.Casino;
                    case "Arcade": return MapIcon.Arcade;
                    case "StandDeTir": return MapIcon.Range;
                    case "SalleDeBoxe": return MapIcon.Boxing;
                    case "Laverie": return MapIcon.Laundry;
                    case "Poste": return MapIcon.Mail;
                    case "Immobilier": return MapIcon.Estate;
                    case "Concession": return MapIcon.Car;
                    case "Avocat": return MapIcon.Lawyer;
                    case "PreteurSurGages": return MapIcon.Pawn;
                    case "Motel": return MapIcon.Hotel;
                    case "Commissariat": return MapIcon.Police;
                }

                try { return (MapIcon)System.Enum.Parse(typeof(MapIcon), hint, true); }
                catch (System.ArgumentException) { }
            }

            string s = Simplify(label);
            if (Has(s, "chez toi", "planque", "ton logement", "ta maison")) return MapIcon.Home;
            if (Has(s, "manoir", "bungalow", "villa")) return MapIcon.Property;
            if (Has(s, "parking")) return MapIcon.Parking;
            if (Has(s, "boxe")) return MapIcon.Boxing;
            if (Has(s, "vertigo", "club", "boite")) return MapIcon.Club;
            if (Has(s, "lave-auto", "lavage")) return MapIcon.CarWash;
            if (Has(s, "laverie")) return MapIcon.Laundry;
            if (Has(s, "poste")) return MapIcon.Mail;
            if (Has(s, "commissariat", "police")) return MapIcon.Police;
            if (Has(s, "tribunal")) return MapIcon.Court;
            if (Has(s, "avocat", "lenoir")) return MapIcon.Lawyer;
            if (Has(s, "pharmacie")) return MapIcon.Pharmacy;
            if (Has(s, "medical", "medecin", "hopital", "clinique")) return MapIcon.Medical;
            if (Has(s, "casino")) return MapIcon.Casino;
            if (Has(s, "preteur", "gages")) return MapIcon.Pawn;
            if (Has(s, "concession", "garage", " auto")) return MapIcon.Car;
            if (Has(s, "tatou", "encre", "tattoo")) return MapIcon.Tattoo;
            if (Has(s, "barbier", "coiff")) return MapIcon.Barber;
            if (Has(s, "taco", "diner", "pizz", "restaurant", "snack", "dragon", "burger", "kebab", "chez marco")) return MapIcon.Food;
            if (Has(s, "cafe")) return MapIcon.Cafe;
            if (Has(s, "bar", "zinc", "pub")) return MapIcon.Bar;
            if (Has(s, "cave", "liquor")) return MapIcon.Liquor;
            if (Has(s, "superette", "epicerie", "tabac", "station")) return MapIcon.Grocery;
            if (Has(s, "fringue", "boutique", "shred")) return MapIcon.Clothes;
            if (Has(s, "quincaillerie", "hardware")) return MapIcon.Hardware;
            if (Has(s, "hangar", "marche noir")) return MapIcon.BlackMarket;
            if (Has(s, "motel", "hotel")) return MapIcon.Hotel;
            if (Has(s, "immobilier")) return MapIcon.Estate;
            if (Has(s, "arcade")) return MapIcon.Arcade;
            if (Has(s, "tir")) return MapIcon.Range;
            if (Has(s, "skate", "basket", "stade")) return MapIcon.Sport;
            if (Has(s, "square", "parc", "jardin", "tilleul")) return MapIcon.Park;
            if (Has(s, "dock", "port")) return MapIcon.Docks;
            if (Has(s, "grange", "ferme")) return MapIcon.Farm;
            if (Has(s, "quartier", "residentiel", "glycines", "moulin")) return MapIcon.District;
            return MapIcon.Generic;
        }

        private static bool Has(string s, params string[] words)
        {
            for (int i = 0; i < words.Length; i++)
            {
                if (s.Contains(words[i])) return true;
            }

            return false;
        }

        /// <summary>Minuscules sans accents, pour comparer des mots.</summary>
        private static string Simplify(string label)
        {
            if (string.IsNullOrEmpty(label)) return string.Empty;
            System.Text.StringBuilder b = new System.Text.StringBuilder(label.Length + 1);
            b.Append(' ');
            string lower = label.ToLowerInvariant();
            for (int i = 0; i < lower.Length; i++)
            {
                char c = lower[i];
                switch (c)
                {
                    case 'à': case 'â': case 'ä': c = 'a'; break;
                    case 'é': case 'è': case 'ê': case 'ë': c = 'e'; break;
                    case 'î': case 'ï': c = 'i'; break;
                    case 'ô': case 'ö': c = 'o'; break;
                    case 'ù': case 'û': case 'ü': c = 'u'; break;
                    case 'ç': c = 'c'; break;
                    case '’': c = '\''; break;
                }

                b.Append(c);
            }

            return b.ToString();
        }

        // ------------------------------------------------------------------ dessin

        /// <summary>
        /// Un repère : le rond de la famille, cerclé de noir, et le pictogramme dedans.
        /// <paramref name="dim"/> grise le repère (un magasin fermé).
        /// </summary>
        public static void DrawBadge(Vector2 center, float size, MapIcon icon, float alpha, bool dim)
        {
            Color group = GroupColor(GroupOf(icon));
            if (dim) group = Color.Lerp(group, new Color(0.42f, 0.44f, 0.48f), 0.7f);
            DrawBadge(center, size, icon, group, alpha);
        }

        public static void DrawBadge(Vector2 center, float size, MapIcon icon, Color color, float alpha)
        {
            Texture2D disc = Disc;
            Color saved = GUI.color;
            float a = alpha * GuiKit.Alpha;

            float ring = Mathf.Max(1.5f, size * 0.09f);
            GUI.color = new Color(0f, 0f, 0f, 0.65f * a);
            GUI.DrawTexture(new Rect(center.x - size * 0.5f - ring, center.y - size * 0.5f - ring + size * 0.06f, size + ring * 2f, size + ring * 2f), disc);
            GUI.color = new Color(0.04f, 0.045f, 0.055f, a);
            GUI.DrawTexture(new Rect(center.x - size * 0.5f - ring, center.y - size * 0.5f - ring, size + ring * 2f, size + ring * 2f), disc);
            GUI.color = new Color(color.r, color.g, color.b, a);
            GUI.DrawTexture(new Rect(center.x - size * 0.5f, center.y - size * 0.5f, size, size), disc);

            bool dark = UiTheme.Luma(color) > 0.62f;
            GUI.color = dark ? new Color(0.06f, 0.06f, 0.08f, a) : new Color(1f, 1f, 1f, a);
            float inner = size * 0.66f;
            GUI.DrawTexture(new Rect(center.x - inner * 0.5f, center.y - inner * 0.5f, inner, inner), Glyph(icon));
            GUI.color = saved;
        }

        /// <summary>Un disque plein, bords adoucis.</summary>
        public static Texture2D Disc
        {
            get
            {
                if (_disc != null) return _disc;
                _disc = Rasterize(p => p.magnitude <= 0.97f);
                return _disc;
            }
        }

        /// <summary>Le pictogramme seul (blanc sur transparent).</summary>
        public static Texture2D Glyph(MapIcon icon)
        {
            Texture2D t;
            if (Cache.TryGetValue(icon, out t) && t != null) return t;
            t = Rasterize(ShapeOf(icon));
            Cache[icon] = t;
            return t;
        }

        private delegate bool Shape(Vector2 p);

        /// <summary>Trace une forme (coordonnées -1..1, y vers le haut) en 4×4 sous-échantillons par pixel.</summary>
        private static Texture2D Rasterize(Shape shape)
        {
            Texture2D t = new Texture2D(Size, Size, TextureFormat.RGBA32, true);
            t.wrapMode = TextureWrapMode.Clamp;
            t.filterMode = FilterMode.Trilinear;
            t.hideFlags = HideFlags.HideAndDontSave;
            Color32[] pixels = new Color32[Size * Size];
            const int S = 4;

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    int hits = 0;
                    for (int sy = 0; sy < S; sy++)
                    {
                        for (int sx = 0; sx < S; sx++)
                        {
                            Vector2 p = new Vector2((x + (sx + 0.5f) / S) / Size * 2f - 1f, (y + (sy + 0.5f) / S) / Size * 2f - 1f);
                            if (shape(p)) hits++;
                        }
                    }

                    pixels[y * Size + x] = new Color32(255, 255, 255, (byte)(hits * 255 / (S * S)));
                }
            }

            t.SetPixels32(pixels);
            t.Apply(true, true);
            return t;
        }

        // --- les briques des dessins

        private static bool Circle(Vector2 p, float cx, float cy, float r)
        {
            float dx = p.x - cx, dy = p.y - cy;
            return dx * dx + dy * dy <= r * r;
        }

        private static bool Ring(Vector2 p, float cx, float cy, float r, float w)
        {
            float d = Mathf.Sqrt((p.x - cx) * (p.x - cx) + (p.y - cy) * (p.y - cy));
            return Mathf.Abs(d - r) <= w * 0.5f;
        }

        private static bool Box(Vector2 p, float cx, float cy, float hx, float hy)
        {
            return Mathf.Abs(p.x - cx) <= hx && Mathf.Abs(p.y - cy) <= hy;
        }

        private static bool RoundBox(Vector2 p, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(p.x - cx) - (hx - r);
            float qy = Mathf.Abs(p.y - cy) - (hy - r);
            float outside = new Vector2(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)).magnitude + Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside <= r;
        }

        private static bool Seg(Vector2 p, float ax, float ay, float bx, float by, float r)
        {
            Vector2 a = new Vector2(ax, ay);
            Vector2 ab = new Vector2(bx, by) - a;
            float t = Mathf.Clamp01(Vector2.Dot(p - a, ab) / Mathf.Max(1e-6f, ab.sqrMagnitude));
            return (p - (a + ab * t)).sqrMagnitude <= r * r;
        }

        private static bool Tri(Vector2 p, float ax, float ay, float bx, float by, float cx, float cy)
        {
            float d1 = (p.x - bx) * (ay - by) - (ax - bx) * (p.y - by);
            float d2 = (p.x - cx) * (by - cy) - (bx - cx) * (p.y - cy);
            float d3 = (p.x - ax) * (cy - ay) - (cx - ax) * (p.y - ay);
            bool neg = d1 < 0f || d2 < 0f || d3 < 0f;
            bool pos = d1 > 0f || d2 > 0f || d3 > 0f;
            return !(neg && pos);
        }

        private static bool Poly(Vector2 p, float[] xy)
        {
            bool inside = false;
            int n = xy.Length / 2;
            for (int i = 0, j = n - 1; i < n; j = i++)
            {
                float xi = xy[i * 2], yi = xy[i * 2 + 1], xj = xy[j * 2], yj = xy[j * 2 + 1];
                if ((yi > p.y) != (yj > p.y) && p.x < (xj - xi) * (p.y - yi) / (yj - yi) + xi) inside = !inside;
            }

            return inside;
        }

        private static float[] StarPoints(float cx, float cy, float outer, float inner)
        {
            float[] xy = new float[20];
            for (int i = 0; i < 10; i++)
            {
                float a = Mathf.PI * 0.5f + i * Mathf.PI / 5f;
                float r = i % 2 == 0 ? outer : inner;
                xy[i * 2] = cx + Mathf.Cos(a) * r;
                xy[i * 2 + 1] = cy + Mathf.Sin(a) * r;
            }

            return xy;
        }

        private static readonly float[] Star = StarPoints(0f, -0.06f, 0.92f, 0.38f);
        private static readonly float[] SmallStar = StarPoints(0f, 0.02f, 0.36f, 0.15f);

        private static readonly float[] Shirt =
        {
            -0.33f, 0.76f, -0.86f, 0.44f, -0.64f, 0.04f, -0.46f, 0.16f, -0.46f, -0.82f,
            0.46f, -0.82f, 0.46f, 0.16f, 0.64f, 0.04f, 0.86f, 0.44f, 0.33f, 0.76f,
            0.18f, 0.58f, 0f, 0.52f, -0.18f, 0.58f
        };

        private static readonly float[] Shield = { 0f, 0.9f, 0.72f, 0.62f, 0.66f, -0.08f, 0f, -0.9f, -0.66f, -0.08f, -0.72f, 0.62f };
        private static readonly float[] Cabin = { -0.52f, 0.06f, -0.3f, 0.48f, 0.34f, 0.48f, 0.58f, 0.06f };
        private static readonly float[] WindowA = { -0.38f, 0.12f, -0.24f, 0.38f, -0.03f, 0.38f, -0.03f, 0.12f };
        private static readonly float[] WindowB = { 0.05f, 0.12f, 0.05f, 0.38f, 0.28f, 0.38f, 0.43f, 0.12f };
        private static readonly float[] Blade = { 0.28f, -0.08f, 0.28f, 0.88f, 0.56f, 0.6f, 0.56f, -0.08f };
        private static readonly float[] Shoulder = { -0.13f, 0.38f, 0.13f, 0.38f, 0.36f, 0.12f, -0.36f, 0.12f };
        private static readonly float[] Barn = { -0.78f, -0.82f, -0.78f, 0.22f, -0.42f, 0.66f, 0.42f, 0.66f, 0.78f, 0.22f, 0.78f, -0.82f };

        private static bool Car(Vector2 p)
        {
            bool body = RoundBox(p, 0f, -0.14f, 0.88f, 0.24f, 0.12f) || Poly(p, Cabin);
            bool windows = Poly(p, WindowA) || Poly(p, WindowB);
            bool arches = Circle(p, -0.5f, -0.42f, 0.29f) || Circle(p, 0.5f, -0.42f, 0.29f);
            bool wheels = Circle(p, -0.5f, -0.42f, 0.2f) || Circle(p, 0.5f, -0.42f, 0.2f);
            return (body && !windows && !arches) || wheels;
        }

        private static bool House(Vector2 p)
        {
            bool roof = Tri(p, -0.9f, 0.06f, 0.9f, 0.06f, 0f, 0.86f) || Box(p, 0.46f, 0.52f, 0.1f, 0.22f);
            bool body = Box(p, 0f, -0.36f, 0.62f, 0.46f);
            bool door = Box(p, 0f, -0.52f, 0.17f, 0.31f);
            return (roof || body) && !door;
        }

        private static Shape ShapeOf(MapIcon icon)
        {
            switch (icon)
            {
                case MapIcon.Home:
                    return House;

                case MapIcon.Property:
                    // Une maison avec une étiquette de prix.
                    return p => (House(p) && !Circle(p, 0.52f, -0.5f, 0.42f)) || (Circle(p, 0.52f, -0.5f, 0.32f) && !Circle(p, 0.52f, -0.5f, 0.1f));

                case MapIcon.Club:
                    return p => Poly(p, Star);

                case MapIcon.Food:
                    return p =>
                        Seg(p, -0.36f, -0.86f, -0.36f, 0.1f, 0.1f) ||
                        Seg(p, -0.58f, 0.12f, -0.58f, 0.82f, 0.06f) ||
                        Seg(p, -0.36f, 0.12f, -0.36f, 0.82f, 0.06f) ||
                        Seg(p, -0.14f, 0.12f, -0.14f, 0.82f, 0.06f) ||
                        RoundBox(p, -0.36f, 0.12f, 0.28f, 0.1f, 0.08f) ||
                        Poly(p, Blade) || Seg(p, 0.42f, -0.86f, 0.42f, -0.08f, 0.11f);

                case MapIcon.Cafe:
                    return p =>
                        (RoundBox(p, -0.12f, -0.12f, 0.5f, 0.44f, 0.18f) && p.y < 0.3f) ||
                        (Ring(p, 0.42f, -0.06f, 0.22f, 0.12f) && p.x > 0.3f) ||
                        Box(p, -0.06f, -0.7f, 0.76f, 0.07f) ||
                        Seg(p, -0.3f, 0.48f, -0.22f, 0.82f, 0.06f) || Seg(p, 0.02f, 0.48f, 0.1f, 0.82f, 0.06f);

                case MapIcon.Bar:
                    return p =>
                        Tri(p, -0.76f, 0.72f, 0.76f, 0.72f, 0f, -0.06f) ||
                        Seg(p, 0f, -0.06f, 0f, -0.68f, 0.075f) ||
                        RoundBox(p, 0f, -0.74f, 0.42f, 0.08f, 0.06f);

                case MapIcon.Grocery:
                    return p =>
                        RoundBox(p, 0f, -0.22f, 0.64f, 0.6f, 0.1f) ||
                        (Ring(p, 0f, 0.38f, 0.3f, 0.11f) && p.y > 0.36f);

                case MapIcon.Clothes:
                    return p => Poly(p, Shirt);

                case MapIcon.Barber:
                    return p =>
                        Ring(p, -0.36f, -0.52f, 0.2f, 0.11f) || Ring(p, 0.36f, -0.52f, 0.2f, 0.11f) ||
                        Seg(p, -0.24f, -0.34f, 0.46f, 0.86f, 0.085f) || Seg(p, 0.24f, -0.34f, -0.46f, 0.86f, 0.085f);

                case MapIcon.Tattoo:
                    return p => Circle(p, 0f, -0.26f, 0.5f) || Tri(p, -0.46f, -0.08f, 0.46f, -0.08f, 0f, 0.88f);

                case MapIcon.Hardware:
                    return p =>
                        (Seg(p, -0.55f, -0.55f, 0.3f, 0.3f, 0.13f) || Circle(p, 0.42f, 0.42f, 0.34f) || Circle(p, -0.56f, -0.56f, 0.22f)) &&
                        !(Mathf.Abs((p.x - 0.58f) - (p.y - 0.58f)) < 0.26f && (p.x - 0.58f) + (p.y - 0.58f) > -0.24f) &&
                        !Circle(p, -0.56f, -0.56f, 0.09f);

                case MapIcon.Car:
                    return Car;

                case MapIcon.CarWash:
                    return p =>
                        Car(new Vector2(p.x * 1.15f, (p.y + 0.3f) * 1.15f)) ||
                        Circle(p, -0.45f, 0.58f, 0.12f) || Circle(p, 0f, 0.72f, 0.12f) || Circle(p, 0.45f, 0.58f, 0.12f) ||
                        Tri(p, -0.55f, 0.6f, -0.35f, 0.6f, -0.45f, 0.82f) || Tri(p, -0.1f, 0.74f, 0.1f, 0.74f, 0f, 0.96f) ||
                        Tri(p, 0.35f, 0.6f, 0.55f, 0.6f, 0.45f, 0.82f);

                case MapIcon.Casino:
                    // Un pique.
                    return p =>
                        Circle(p, -0.32f, -0.12f, 0.36f) || Circle(p, 0.32f, -0.12f, 0.36f) ||
                        Tri(p, -0.66f, -0.02f, 0.66f, -0.02f, 0f, 0.88f) || Tri(p, 0f, -0.3f, -0.3f, -0.86f, 0.3f, -0.86f);

                case MapIcon.Pawn:
                    // Les trois boules du prêteur sur gages.
                    return p =>
                        Seg(p, -0.66f, 0.8f, 0.66f, 0.8f, 0.07f) ||
                        Seg(p, -0.42f, 0.8f, -0.42f, 0.4f, 0.045f) || Seg(p, 0.42f, 0.8f, 0.42f, 0.4f, 0.045f) ||
                        Seg(p, 0f, 0.8f, 0f, -0.3f, 0.045f) ||
                        Circle(p, -0.42f, 0.18f, 0.27f) || Circle(p, 0.42f, 0.18f, 0.27f) || Circle(p, 0f, -0.52f, 0.27f);

                case MapIcon.Medical:
                    return p => RoundBox(p, 0f, 0f, 0.26f, 0.78f, 0.06f) || RoundBox(p, 0f, 0f, 0.78f, 0.26f, 0.06f);

                case MapIcon.Pharmacy:
                    // Une gélule.
                    return p =>
                        Seg(p, -0.42f, -0.42f, 0.42f, 0.42f, 0.34f) &&
                        !(Mathf.Abs(p.x + p.y) < 0.05f && Mathf.Abs(p.x - p.y) < 0.9f);

                case MapIcon.Police:
                    return p => Poly(p, Shield) && !Poly(p, SmallStar);

                case MapIcon.Court:
                case MapIcon.Lawyer:
                    return p =>
                        Tri(p, -0.88f, 0.44f, 0.88f, 0.44f, 0f, 0.88f) || Box(p, 0f, 0.38f, 0.82f, 0.06f) ||
                        Box(p, -0.6f, -0.1f, 0.09f, 0.4f) || Box(p, -0.2f, -0.1f, 0.09f, 0.4f) ||
                        Box(p, 0.2f, -0.1f, 0.09f, 0.4f) || Box(p, 0.6f, -0.1f, 0.09f, 0.4f) ||
                        Box(p, 0f, -0.58f, 0.86f, 0.07f) || Box(p, 0f, -0.74f, 0.92f, 0.06f);

                case MapIcon.Mail:
                    return p =>
                        Box(p, 0f, 0f, 0.86f, 0.56f) &&
                        !Seg(p, -0.86f, 0.56f, 0f, -0.06f, 0.07f) && !Seg(p, 0.86f, 0.56f, 0f, -0.06f, 0.07f);

                case MapIcon.Laundry:
                    return p =>
                        (RoundBox(p, 0f, 0f, 0.7f, 0.86f, 0.12f) && !Circle(p, 0f, -0.14f, 0.44f) && !Box(p, 0f, 0.6f, 0.58f, 0.035f)) ||
                        Circle(p, 0f, -0.14f, 0.3f);

                case MapIcon.Boxing:
                    // Un gant.
                    return p =>
                        RoundBox(p, 0.06f, 0.18f, 0.56f, 0.6f, 0.42f) || Box(p, 0.02f, -0.62f, 0.42f, 0.2f) ||
                        Circle(p, -0.52f, 0.02f, 0.24f);

                case MapIcon.Range:
                    return p =>
                        (Ring(p, 0f, 0f, 0.6f, 0.13f) || Box(p, 0f, 0f, 0.055f, 0.92f) || Box(p, 0f, 0f, 0.92f, 0.055f)) &&
                        !Circle(p, 0f, 0f, 0.16f) || Circle(p, 0f, 0f, 0.08f);

                case MapIcon.Arcade:
                    return p =>
                        RoundBox(p, 0f, 0f, 0.88f, 0.46f, 0.36f) &&
                        !Box(p, -0.44f, 0f, 0.2f, 0.06f) && !Box(p, -0.44f, 0f, 0.06f, 0.2f) &&
                        !Circle(p, 0.36f, 0.1f, 0.1f) && !Circle(p, 0.58f, -0.1f, 0.1f);

                case MapIcon.Estate:
                    // Une clé.
                    return p =>
                        (Circle(p, -0.42f, 0.42f, 0.36f) && !Circle(p, -0.42f, 0.42f, 0.14f)) ||
                        Seg(p, -0.2f, 0.2f, 0.66f, -0.66f, 0.1f) ||
                        Seg(p, 0.44f, -0.44f, 0.62f, -0.26f, 0.08f) || Seg(p, 0.24f, -0.24f, 0.4f, -0.08f, 0.08f);

                case MapIcon.Liquor:
                    return p =>
                        (Box(p, 0f, 0.62f, 0.12f, 0.26f) || Poly(p, Shoulder) || RoundBox(p, 0f, -0.36f, 0.36f, 0.5f, 0.08f)) &&
                        !Box(p, 0f, -0.3f, 0.24f, 0.15f);

                case MapIcon.BlackMarket:
                    // Un œil.
                    return p =>
                        (Circle(p, 0f, -0.62f, 1.02f) && Circle(p, 0f, 0.62f, 1.02f) && !Circle(p, 0f, 0f, 0.32f)) ||
                        Circle(p, 0f, 0f, 0.17f);

                case MapIcon.Hotel:
                    // Un lit.
                    return p =>
                        Box(p, 0f, -0.3f, 0.86f, 0.16f) || Box(p, -0.78f, -0.06f, 0.09f, 0.52f) ||
                        Box(p, -0.78f, -0.56f, 0.09f, 0.2f) || Box(p, 0.78f, -0.56f, 0.09f, 0.2f) ||
                        RoundBox(p, -0.44f, 0.0f, 0.22f, 0.12f, 0.08f) || RoundBox(p, 0.2f, -0.04f, 0.58f, 0.12f, 0.05f);

                case MapIcon.Park:
                    return p => Circle(p, 0f, 0.24f, 0.56f) || Circle(p, -0.34f, 0.0f, 0.36f) || Circle(p, 0.34f, 0.0f, 0.36f) || Box(p, 0f, -0.56f, 0.1f, 0.34f);

                case MapIcon.Docks:
                    // Une ancre.
                    return p =>
                        Ring(p, 0f, 0.66f, 0.15f, 0.09f) || Box(p, 0f, -0.02f, 0.07f, 0.56f) || Box(p, 0f, 0.3f, 0.34f, 0.06f) ||
                        (Ring(p, 0f, -0.08f, 0.62f, 0.12f) && p.y < -0.12f) ||
                        Tri(p, -0.78f, -0.22f, -0.5f, -0.1f, -0.66f, 0.06f) || Tri(p, 0.78f, -0.22f, 0.5f, -0.1f, 0.66f, 0.06f);

                case MapIcon.District:
                    // Des immeubles.
                    return p =>
                        (Box(p, -0.5f, -0.3f, 0.26f, 0.52f) || Box(p, 0.06f, -0.08f, 0.26f, 0.8f) || Box(p, 0.6f, -0.42f, 0.24f, 0.42f)) &&
                        !(Mathf.Repeat(p.y + 0.9f, 0.3f) < 0.12f && Mathf.Repeat(p.x + 0.9f, 0.18f) < 0.08f && p.y > -0.7f);

                case MapIcon.Farm:
                    return p =>
                        Poly(p, Barn) &&
                        !(Box(p, 0f, -0.46f, 0.3f, 0.36f) && !Seg(p, -0.3f, -0.82f, 0.3f, -0.1f, 0.06f) && !Seg(p, 0.3f, -0.82f, -0.3f, -0.1f, 0.06f));

                case MapIcon.Sport:
                    // Un ballon de basket.
                    return p =>
                        Circle(p, 0f, 0f, 0.86f) &&
                        !Box(p, 0f, 0f, 0.035f, 0.9f) && !Box(p, 0f, 0f, 0.9f, 0.035f) &&
                        !Ring(p, -1.1f, 0f, 0.82f, 0.07f) && !Ring(p, 1.1f, 0f, 0.82f, 0.07f);

                case MapIcon.Parking:
                    return p =>
                        Box(p, -0.36f, 0f, 0.14f, 0.84f) || (Ring(p, 0.02f, 0.36f, 0.3f, 0.24f) && p.x >= 0.02f) ||
                        Box(p, -0.17f, 0.66f, 0.19f, 0.12f) || Box(p, -0.17f, 0.06f, 0.19f, 0.12f);

                default:
                    return p => Circle(p, 0f, 0f, 0.42f);
            }
        }
    }
}
