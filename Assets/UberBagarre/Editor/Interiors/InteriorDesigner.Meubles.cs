using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les meubles de vente : le comptoir (et ses variantes : bar, bureau, vitrine, guichet,
    /// caisses de bois), la caisse enregistreuse, les rayonnages garnis, les frigos vitrés,
    /// les vitrines à bijoux ou à gâteaux.
    ///
    /// Chaque meuble se dessine dans son propre repère : sa longueur le long de X, sa face
    /// avant (le côté client) vers −Z, posé au sol (y = 0).
    /// </summary>
    public static partial class InteriorDesigner
    {
        private static Vector3 V(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        // ================================================================== comptoirs

        private enum CounterStyle
        {
            /// <summary>Le comptoir de magasin : caisson, façade à lames, étagère côté vendeur.</summary>
            Shop,

            /// <summary>Le bar : plateau épais à nez arrondi, repose-pieds en laiton, bac à glace.</summary>
            Bar,

            /// <summary>Un grand bureau (avocat, agence) : plateau, caissons à tiroirs, voile de fond.</summary>
            Desk,

            /// <summary>Une vitrine éclairée : on voit ce qu'on vend (bijoux, gâteaux, armes…).</summary>
            Showcase,

            /// <summary>Le guichet : une vitre au-dessus du comptoir (poste, commissariat), ou des barreaux (casino).</summary>
            Window,

            /// <summary>Des caisses de bois et une planche (le marché noir).</summary>
            Crates
        }

        /// <summary>
        /// Le comptoir, centré sur <paramref name="at"/> : sa longueur le long de X, le client du
        /// côté −Z, le vendeur derrière (+Z), à <paramref name="service"/> de son milieu.
        /// Repères « comptoir » (la zone d'achat) et « vendeur ».
        /// </summary>
        private static void Counter(InteriorPlan p, Vector3 at, float yaw, float length, CounterStyle style, string body, string top,
            float service = 0f, string goods = null)
        {
            p.Push(at, yaw);
            float half = length * 0.5f;
            float topY = 1.02f;
            bool bars = top == "barreaux";
            if (bars) top = "marbre_noir";

            switch (style)
            {
                case CounterStyle.Bar:
                    BarBody(p, length, body, top);
                    break;
                case CounterStyle.Desk:
                    topY = 0.76f;
                    DeskBody(p, length, 0.85f, body, top, true);
                    break;
                case CounterStyle.Showcase:
                    topY = 1.0f;
                    ShowcaseBody(p, length, body, goods ?? "bijoux");
                    break;
                case CounterStyle.Crates:
                    topY = 0.95f;
                    CratesBody(p, length);
                    break;
                default:
                    ShopBody(p, length, body, top);
                    break;
            }

            if (style == CounterStyle.Window) WindowScreen(p, length, bars);

            // Ce qui est posé dessus : la caisse devant le vendeur, le terminal de carte côté client.
            float till = Mathf.Clamp(service + 0.38f, -half + 0.25f, half - 0.25f);
            if (style == CounterStyle.Crates)
            {
                CashBox(p, V(till, topY, 0.05f));
            }
            else if (style == CounterStyle.Desk)
            {
                Monitor(p, V(service + 0.25f, topY, 0.12f), 180f, 0.5f);
                Keyboard(p, V(service, topY, 0.3f), 180f);
                p.Box("Sous-main", V(service - 0.1f, topY + 0.003f, 0.05f), V(0.6f, 0.006f, 0.42f), "cuir_noir");
                Papers(p, V(service - 0.55f, topY, 0.0f), Rand(0f, 30f));
                DeskLamp(p, V(-half + 0.3f, topY, 0.2f), 200f);
                p.Box("Plaque", V(service, topY + 0.04f, -0.3f), V(0.26f, 0.07f, 0.02f), "laiton", V(-20f, 0f, 0f));
                p.Box("Telephone", V(half - 0.35f, topY + 0.03f, 0.1f), V(0.18f, 0.06f, 0.2f), "plastique_noir");
            }
            else
            {
                Register(p, V(till, topY + 0.005f, 0.06f));
                CardTerminal(p, V(till - 0.42f, topY + 0.005f, -0.18f));
                if (style != CounterStyle.Bar && Rand() < 0.7f) Impulse(p, V(Mathf.Clamp(till + 0.55f, -half + 0.2f, half - 0.2f), topY + 0.005f, -0.18f));
                if (Rand() < 0.5f) p.Cylinder("Pot a pourboires", V(till - 0.75f, topY + 0.07f, -0.25f), 0.05f, 0.14f, "verre");
                if (Rand() < 0.6f) p.Box("Bloc de recus", V(till + 0.28f, topY + 0.02f, -0.25f), V(0.1f, 0.03f, 0.08f), "papier");
            }

            p.Mark("comptoir", Vector3.zero, 0f, length);
            p.Mark("vendeur", V(service, 0f, 0.75f), 180f);
            p.Pop();
        }

        private static void ShopBody(InteriorPlan p, float length, string body, string top)
        {
            float half = length * 0.5f;
            p.Box("Plinthe", V(0f, 0.04f, -0.07f), V(length - 0.04f, 0.08f, 0.34f), "noir");
            p.Box("Caisson", V(0f, 0.55f, -0.1f), V(length, 0.94f, 0.4f), body, true);
            p.Box("Joue", V(-half + 0.015f, 0.51f, 0.2f), V(0.03f, 1.0f, 0.2f), body);
            p.Box("Joue", V(half - 0.015f, 0.51f, 0.2f), V(0.03f, 1.0f, 0.2f), body);
            p.Box("Fond d'etagere", V(0f, 0.1f, 0.2f), V(length - 0.06f, 0.02f, 0.2f), body);
            p.Box("Etagere", V(0f, 0.52f, 0.2f), V(length - 0.06f, 0.02f, 0.2f), body);
            p.Box("Plateau", V(0f, 0.995f, -0.03f), V(length + 0.06f, 0.05f, 0.72f), top);
            p.Box("Chant", V(0f, 0.995f, -0.392f), V(length + 0.06f, 0.05f, 0.006f), "metal");

            // La façade : des lames verticales sur un fond plus sombre.
            p.Box("Facade", V(0f, 0.53f, -0.302f), V(length - 0.04f, 0.82f, 0.004f), "bois_fonce");
            for (float x = -half + 0.08f; x < half - 0.05f; x += 0.1f)
            {
                p.Box("Lame", V(x, 0.53f, -0.31f), V(0.075f, 0.8f, 0.012f), body);
            }

            // Côté vendeur : des sacs, des rouleaux, une boîte, un classeur.
            for (float x = -half + 0.2f; x < half - 0.2f; x += Rand(0.35f, 0.6f))
            {
                switch (Pick(4))
                {
                    case 0:
                        p.Box("Sacs", V(x, 0.15f, 0.2f), V(0.3f, 0.08f, 0.16f), Pick(2) == 0 ? "papier" : "blanc");
                        break;
                    case 1:
                        p.Cylinder("Rouleau", V(x, 0.57f, 0.2f), 0.04f, 0.08f, "papier", V(0f, 0f, 90f));
                        break;
                    case 2:
                        p.Box("Boite", V(x, 0.2f, 0.19f), V(0.22f, 0.18f, 0.16f), "carton");
                        break;
                    default:
                        p.Box("Classeur", V(x, 0.66f, 0.2f), V(0.06f, 0.26f, 0.16f), Product());
                        break;
                }
            }
        }

        private static void BarBody(InteriorPlan p, float length, string body, string top)
        {
            float half = length * 0.5f;
            p.Box("Plinthe", V(0f, 0.06f, -0.08f), V(length - 0.04f, 0.12f, 0.46f), "noir");
            p.Box("Caisson", V(0f, 0.6f, -0.08f), V(length, 0.98f, 0.5f), body, true);
            p.Box("Plateau", V(0f, 1.11f, -0.06f), V(length + 0.1f, 0.06f, 0.66f), top);
            p.Cylinder("Nez du plateau", V(0f, 1.1f, -0.39f), 0.04f, length + 0.1f, top, V(0f, 0f, 90f));

            // Des panneaux à cadre, et le repose-pieds en laiton sur ses consoles.
            int panels = Mathf.Max(1, Mathf.RoundToInt(length / 0.9f));
            for (int i = 0; i < panels; i++)
            {
                float x = -half + length * (i + 0.5f) / panels;
                float pw = length / panels - 0.12f;
                p.Box("Cadre", V(x, 0.62f, -0.335f), V(pw, 0.68f, 0.012f), "moulure");
                p.Box("Panneau", V(x, 0.62f, -0.34f), V(pw - 0.1f, 0.58f, 0.012f), body);
            }

            p.Cylinder("Repose-pieds", V(0f, 0.24f, -0.48f), 0.025f, length - 0.1f, "laiton", V(0f, 0f, 90f));
            for (float x = -half + 0.3f; x <= half - 0.25f; x += 1.1f)
            {
                p.Box("Console", V(x, 0.24f, -0.41f), V(0.04f, 0.04f, 0.14f), "laiton");
            }

            // Côté barman : l'évier, le bac à glace, le rail de bouteilles.
            p.Box("Plan de travail", V(0f, 0.86f, 0.32f), V(length - 0.1f, 0.04f, 0.22f), "inox");
            p.Box("Evier", V(-half * 0.5f, 0.83f, 0.32f), V(0.4f, 0.05f, 0.2f), "metal_noir");
            p.Box("Bac a glace", V(half * 0.2f, 0.84f, 0.32f), V(0.5f, 0.04f, 0.2f), "blanc");
            p.Box("Rail", V(0f, 0.7f, 0.43f), V(length - 0.2f, 0.12f, 0.04f), "inox");
            for (float x = -half + 0.3f; x < half - 0.3f; x += 0.11f)
            {
                if (Rand() < 0.3f) continue;
                Bottle(p, V(x, 0.7f, 0.43f), Pick(2) == 0 ? "bouteille_claire" : "bouteille_verte", 0.6f);
            }

            // Les tireuses : une colonne chromée, ses robinets et leurs manettes de couleur.
            Vector3 tower = V(half * 0.45f, 1.14f, -0.02f);
            p.Box("Colonne a biere", tower + V(0f, 0.22f, 0f), V(0.5f, 0.08f, 0.1f), "chrome");
            p.Cylinder("Pied de colonne", tower + V(0f, 0.1f, 0f), 0.04f, 0.2f, "chrome");
            for (int i = 0; i < 4; i++)
            {
                Vector3 tap = tower + V(-0.18f + i * 0.12f, 0.2f, -0.06f);
                p.Cylinder("Robinet", tap, 0.012f, 0.06f, "chrome", V(90f, 0f, 0f));
                p.Box("Manette", tap + V(0f, 0.1f, 0f), V(0.03f, 0.14f, 0.03f), Product());
            }

            p.Box("Egouttoir", tower + V(0f, 0.005f, -0.06f), V(0.55f, 0.01f, 0.16f), "metal_noir");
            for (int i = 0; i < 3; i++)
            {
                p.Box("Sous-bock", V(-half + 0.6f + i * Rand(0.8f, 1.4f), 1.142f, -0.22f), V(0.1f, 0.004f, 0.1f), Product());
            }
        }

        /// <summary>Un bureau : plateau, deux caissons à tiroirs (poignées laiton), voile de fond côté visiteur.</summary>
        private static void DeskBody(InteriorPlan p, float length, float depth, string body, string top, bool collider)
        {
            float half = length * 0.5f;
            p.Box("Plateau", V(0f, 0.74f, 0f), V(length, 0.04f, depth), top);
            p.Box("Ceinture", V(0f, 0.69f, 0f), V(length - 0.04f, 0.06f, depth - 0.04f), body);
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * (half - 0.24f);
                p.Box("Caisson", V(x, 0.34f, 0f), V(0.46f, 0.68f, depth - 0.06f), body);
                for (int d = 0; d < 3; d++)
                {
                    float y = 0.13f + d * 0.21f;
                    p.Box("Tiroir", V(x, y, depth * 0.5f - 0.025f), V(0.42f, 0.19f, 0.01f), body);
                    p.Box("Poignee", V(x, y + 0.04f, depth * 0.5f - 0.015f), V(0.12f, 0.015f, 0.015f), "laiton");
                }
            }

            p.Box("Voile de fond", V(0f, 0.42f, -depth * 0.5f + 0.04f), V(length - 0.96f, 0.5f, 0.025f), body);
            if (collider) p.Block("Bureau", V(0f, 0.38f, 0f), V(length, 0.76f, depth));
        }

        /// <summary>Une vitrine : socle, verre sur trois côtés et dessus, fond de velours, lumière dedans.</summary>
        private static void ShowcaseBody(InteriorPlan p, float length, string body, string goods)
        {
            float half = length * 0.5f;
            const float depth = 0.6f;
            p.Box("Socle", V(0f, 0.36f, 0f), V(length, 0.72f, depth), body);
            p.Box("Plinthe", V(0f, 0.04f, -0.01f), V(length - 0.04f, 0.08f, depth - 0.04f), "noir");
            p.Box("Fond de vitrine", V(0f, 0.73f, 0f), V(length - 0.04f, 0.02f, depth - 0.04f), goods == "patisserie" ? "blanc" : "tissu_rouge");
            p.Box("Vitre avant", V(0f, 0.86f, -depth * 0.5f + 0.005f), V(length, 0.26f, 0.01f), "verre");
            p.Box("Vitre du dessus", V(0f, 0.995f, 0f), V(length, 0.01f, depth), "verre");
            p.Box("Vitre de cote", V(-half + 0.005f, 0.86f, 0f), V(0.01f, 0.26f, depth), "verre");
            p.Box("Vitre de cote", V(half - 0.005f, 0.86f, 0f), V(0.01f, 0.26f, depth), "verre");
            p.Box("Arete", V(0f, 1.0f, -depth * 0.5f), V(length, 0.015f, 0.015f), "chrome");
            p.Box("Arete", V(-half, 0.86f, -depth * 0.5f), V(0.015f, 0.28f, 0.015f), "chrome");
            p.Box("Arete", V(half, 0.86f, -depth * 0.5f), V(0.015f, 0.28f, 0.015f), "chrome");
            p.Box("Reglette", V(0f, 0.98f, 0.2f), V(length - 0.1f, 0.012f, 0.03f), "lumiere");
            p.Box("Portes coulissantes", V(0f, 0.86f, depth * 0.5f - 0.005f), V(length, 0.26f, 0.01f), "verre");
            p.Block("Vitrine", V(0f, 0.5f, 0f), V(length, 1.0f, depth));
            Trinkets(p, goods, -half + 0.08f, half - 0.08f, 0.74f, -depth * 0.5f + 0.06f, depth * 0.5f - 0.06f);
        }

        /// <summary>Ce qu'on voit dans une vitrine, posé sur le velours entre z0 et z1.</summary>
        private static void Trinkets(InteriorPlan p, string goods, float x0, float x1, float y, float z0, float z1)
        {
            for (float x = x0; x < x1; x += Rand(0.1f, 0.18f))
            {
                float z = Rand(z0, z1);
                switch (goods)
                {
                    case "patisserie":
                        if (Pick(2) == 0)
                        {
                            p.Cylinder("Gateau", V(x, y + 0.04f, z), Rand(0.06f, 0.1f), 0.08f, Pick(2) == 0 ? "rose" : "bois_clair");
                            p.Cylinder("Glacage", V(x, y + 0.085f, z), 0.05f, 0.01f, "blanc");
                        }
                        else
                        {
                            p.Sphere("Croissant", V(x, y + 0.025f, z), V(0.12f, 0.05f, 0.06f), "jaune", V(0f, Rand(0f, 180f), 0f));
                        }

                        break;
                    case "armes":
                        p.Box("Pistolet", V(x, y + 0.012f, z), V(0.17f, 0.025f, 0.03f), "metal_noir", V(0f, Rand(-25f, 25f), 0f));
                        p.Box("Crosse", V(x - 0.06f, y + 0.012f, z + 0.04f), V(0.04f, 0.024f, 0.08f), Pick(2) == 0 ? "bois" : "plastique_noir",
                            V(0f, Rand(-25f, 25f), 0f));
                        x += 0.1f;
                        break;
                    case "electronique":
                        p.Box("Telephone", V(x, y + 0.006f, z), V(0.07f, 0.01f, 0.14f), "plastique_noir", V(0f, Rand(-15f, 15f), 0f));
                        if (Pick(2) == 0) p.Box("Montre", V(x + 0.05f, y + 0.01f, z - 0.08f), V(0.04f, 0.015f, 0.04f), "chrome");
                        break;
                    case "cosmetique":
                        Bottle(p, V(x, y, z), Product(), 0.5f);
                        break;
                    default:
                        // Bijoux : bagues, montres, chaînes, sur leurs présentoirs.
                        if (Pick(3) == 0) p.Box("Presentoir", V(x, y + 0.03f, z), V(0.08f, 0.06f, 0.05f), "noir");
                        p.Cylinder("Bague", V(x, y + 0.07f, z), 0.012f, 0.005f, Pick(2) == 0 ? "laiton" : "chrome", V(80f, 0f, 0f));
                        if (Pick(2) == 0) p.Box("Chaine", V(x + 0.04f, y + 0.003f, z + 0.05f), V(0.008f, 0.004f, 0.16f), "laiton", V(0f, Rand(0f, 90f), 0f));
                        break;
                }
            }
        }

        private static void CratesBody(InteriorPlan p, float length)
        {
            int n = Mathf.Max(2, Mathf.RoundToInt(length / 0.6f));
            float w = length / n;
            for (int i = 0; i < n; i++)
            {
                float x = -length * 0.5f + w * (i + 0.5f);
                Crate(p, V(x, 0f, -0.08f), Rand(-3f, 3f), V(w - 0.02f, 0.45f, 0.48f));
                Crate(p, V(x + Rand(-0.03f, 0.03f), 0.45f, -0.08f), Rand(-4f, 4f), V(w - 0.04f, 0.45f, 0.46f));
            }

            p.Box("Planche", V(0f, 0.92f, -0.05f), V(length + 0.1f, 0.04f, 0.62f), "bois_clair", V(0f, Rand(-1f, 1f), 0f));
            p.Block("Comptoir", V(0f, 0.47f, -0.05f), V(length, 0.94f, 0.6f));
        }

        /// <summary>La vitre du guichet (ou les barreaux de la caisse du casino), sur le comptoir.</summary>
        private static void WindowScreen(InteriorPlan p, float length, bool bars)
        {
            float half = length * 0.5f;
            const float y0 = 1.02f, y1 = 2.1f;
            p.Box("Montant", V(-half + 0.03f, (y0 + y1) * 0.5f, 0f), V(0.06f, y1 - y0, 0.06f), "metal_noir");
            p.Box("Montant", V(half - 0.03f, (y0 + y1) * 0.5f, 0f), V(0.06f, y1 - y0, 0.06f), "metal_noir");
            p.Box("Traverse", V(0f, y1, 0f), V(length, 0.08f, 0.08f), "metal_noir");
            if (bars)
            {
                for (float x = -half + 0.12f; x < half - 0.08f; x += 0.11f)
                {
                    if (Mathf.Abs(x) < 0.25f) continue;
                    p.Cylinder("Barreau", V(x, (y0 + 0.25f + y1) * 0.5f, 0f), 0.009f, y1 - y0 - 0.25f, "laiton");
                }

                p.Box("Traverse", V(0f, y0 + 0.25f, 0f), V(length, 0.03f, 0.04f), "laiton");
                p.Block("Grille", V(0f, (y0 + y1) * 0.5f, 0f), V(length, y1 - y0, 0.06f));
                return;
            }

            int panes = Mathf.Max(1, Mathf.RoundToInt(length / 1.4f));
            for (int i = 0; i < panes; i++)
            {
                float x = -half + length * (i + 0.5f) / panes;
                float pw = length / panes - 0.06f;
                p.Box("Vitre", V(x, (y0 + 0.12f + y1) * 0.5f, 0f), V(pw, y1 - y0 - 0.12f, 0.012f), "verre", true);
                p.Cylinder("Hygiaphone", V(x, 1.45f, -0.008f), 0.07f, 0.006f, "metal", V(90f, 0f, 0f));
                if (i > 0) p.Box("Montant", V(-half + length * i / panes, (y0 + y1) * 0.5f, 0f), V(0.04f, y1 - y0, 0.05f), "metal_noir");
            }
        }

        // ================================================================== sur le comptoir

        /// <summary>La caisse : tiroir, clavier incliné, écran tourné vers le vendeur, afficheur côté client.</summary>
        private static void Register(InteriorPlan p, Vector3 at)
        {
            p.Push(at, 0f);
            p.Box("Tiroir-caisse", V(0f, 0.05f, 0f), V(0.4f, 0.1f, 0.38f), "plastique_noir");
            p.Box("Fente du tiroir", V(0f, 0.05f, -0.192f), V(0.36f, 0.005f, 0.004f), "metal");
            p.Box("Clavier", V(0f, 0.12f, 0.06f), V(0.3f, 0.04f, 0.18f), "plastique", V(12f, 0f, 0f));
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 4; c++)
                {
                    p.Box("Touche", V(-0.1f + c * 0.065f, 0.145f + r * 0.012f, 0.11f - r * 0.045f), V(0.05f, 0.012f, 0.035f),
                        r == 0 && c == 3 ? "vert" : "blanc", V(12f, 0f, 0f));
                }
            }

            p.Cylinder("Pied d'ecran", V(0f, 0.18f, -0.1f), 0.015f, 0.18f, "plastique_noir");
            p.Box("Ecran", V(0f, 0.36f, -0.1f), V(0.32f, 0.22f, 0.03f), "plastique_noir", V(-10f, 0f, 0f));
            p.Box("Affichage", V(0f, 0.36f, -0.083f), V(0.28f, 0.18f, 0.004f), "ecran", V(-10f, 0f, 0f));
            p.Box("Afficheur client", V(0f, 0.31f, -0.122f), V(0.14f, 0.045f, 0.01f), "ecran_vert", V(-10f, 0f, 0f));
            p.Pop();
        }

        private static void CardTerminal(InteriorPlan p, Vector3 at)
        {
            p.Box("Socle du terminal", at + V(0f, 0.01f, 0f), V(0.1f, 0.02f, 0.12f), "plastique_noir");
            p.Box("Terminal de carte", at + V(0f, 0.04f, 0.01f), V(0.08f, 0.03f, 0.16f), "plastique_noir", V(-18f, 0f, 0f));
            p.Box("Ecran du terminal", at + V(0f, 0.058f, 0.04f), V(0.06f, 0.004f, 0.05f), "ecran_vert", V(-18f, 0f, 0f));
        }

        /// <summary>Le présentoir de caisse : chewing-gums, bonbons, piles — ce qu'on achète en attendant.</summary>
        private static void Impulse(InteriorPlan p, Vector3 at)
        {
            p.Box("Presentoir", at + V(0f, 0.07f, 0f), V(0.36f, 0.14f, 0.2f), "metal_noir", V(-15f, 0f, 0f));
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    p.Box("Bonbons", at + V(-0.14f + c * 0.07f, 0.12f + r * 0.03f, -0.05f + r * 0.08f), V(0.055f, 0.03f, 0.07f), Product(),
                        V(-15f, 0f, 0f));
                }
            }
        }

        private static void CashBox(InteriorPlan p, Vector3 at)
        {
            p.Box("Caisse en fer", at + V(0f, 0.06f, 0f), V(0.34f, 0.12f, 0.26f), "vert");
            p.Box("Couvercle", at + V(0f, 0.13f, 0.12f), V(0.34f, 0.02f, 0.26f), "vert", V(-70f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                p.Box("Liasse", at + V(-0.25f - i * 0.02f, 0.012f + i * 0.02f, -0.1f), V(0.15f, 0.018f, 0.07f), "pelouse", V(0f, Rand(-20f, 20f), 0f));
            }

            p.Box("Calculatrice", at + V(0.3f, 0.01f, -0.05f), V(0.1f, 0.02f, 0.16f), "plastique_noir");
        }

        // ================================================================== rayonnages

        private const float ShelfDepth = 0.42f;
        private const float ShelfSpacing = 0.36f;
        private const float ShelfDeck = 0.14f;

        /// <summary>
        /// Une gondole : socle, fond, montants tous les mètres, tablettes à porte-étiquettes,
        /// garnie de produits (<paramref name="kind"/>), d'un ou des deux côtés.
        /// </summary>
        private static void Gondola(InteriorPlan p, Vector3 at, float yaw, float length, int levels, string kind, bool twoSided)
        {
            p.Push(at, yaw);
            float height = ShelfDeck + levels * ShelfSpacing + 0.04f;
            float backZ = twoSided ? 0f : ShelfDepth * 0.5f + 0.015f;
            p.Box("Fond", V(0f, height * 0.5f, backZ), V(length, height, twoSided ? 0.04f : 0.03f), "metal");
            int bays = Mathf.Max(1, Mathf.RoundToInt(length));
            for (int b = 0; b <= bays; b++)
            {
                float x = -length * 0.5f + length * b / bays;
                p.Box("Montant", V(x, height * 0.5f, backZ), V(0.045f, height + 0.01f, twoSided ? 0.08f : 0.05f), "metal_noir");
            }

            p.Block("Rayonnage", V(0f, height * 0.5f, 0f), V(length, height, twoSided ? ShelfDepth * 2f + 0.08f : ShelfDepth + 0.05f));
            if (twoSided)
            {
                GondolaSide(p, length, levels, kind, -0.02f);
                p.Push(Vector3.zero, 180f);
                GondolaSide(p, length, levels, kind, -0.02f);
                p.Pop();
            }
            else
            {
                GondolaSide(p, length, levels, kind, ShelfDepth * 0.5f);
            }

            p.Pop();
        }

        private static void GondolaSide(InteriorPlan p, float length, int levels, string kind, float back)
        {
            float front = back - ShelfDepth;
            float mid = back - ShelfDepth * 0.5f;
            p.Box("Socle", V(0f, ShelfDeck * 0.5f - 0.01f, mid), V(length, ShelfDeck - 0.02f, ShelfDepth), "metal_noir");
            p.Box("Plancher", V(0f, ShelfDeck - 0.01f, mid), V(length, 0.02f, ShelfDepth), "metal");
            string strip = Pick(3) == 0 ? "jaune" : "blanc";
            for (int l = 0; l < levels; l++)
            {
                float y = ShelfDeck + l * ShelfSpacing;
                if (l > 0) p.Box("Tablette", V(0f, y - 0.0125f, mid), V(length - 0.02f, 0.025f, ShelfDepth - 0.02f), "metal");
                p.Box("Porte-etiquettes", V(0f, y - 0.022f, front - 0.004f), V(length - 0.02f, 0.04f, 0.008f), strip);
                for (float x = -length * 0.5f + 0.12f; x < length * 0.5f - 0.05f; x += Rand(0.22f, 0.4f))
                {
                    p.Box("Etiquette", V(x, y - 0.022f, front - 0.009f), V(0.045f, 0.028f, 0.003f), strip == "blanc" ? "jaune" : "blanc");
                }

                if (Rand() < 0.12f)
                {
                    p.Box("Affichette promo", V(Rand(-length * 0.4f, length * 0.4f), y - 0.08f, front - 0.012f), V(0.09f, 0.1f, 0.003f), "rouge");
                }

                Goods(p, kind, -length * 0.5f + 0.03f, length * 0.5f - 0.03f, y, front + 0.012f, ShelfDepth - 0.03f, ShelfSpacing - 0.05f);
            }
        }

        /// <summary>Une étagère murale simple (bois ou métal), garnie : le dos contre le mur (+Z).</summary>
        private static void WallShelf(InteriorPlan p, Vector3 at, float yaw, float length, int levels, string kind, string frame,
            float depth = 0.34f, float spacing = 0.38f, float bottom = 0.12f)
        {
            p.Push(at, yaw);
            float height = bottom + levels * spacing;
            float half = length * 0.5f;
            p.Box("Dos", V(0f, height * 0.5f, depth * 0.5f - 0.01f), V(length, height, 0.02f), frame);
            p.Box("Joue", V(-half + 0.015f, height * 0.5f, 0f), V(0.03f, height, depth), frame);
            p.Box("Joue", V(half - 0.015f, height * 0.5f, 0f), V(0.03f, height, depth), frame);
            p.Box("Chapeau", V(0f, height + 0.015f, 0f), V(length, 0.03f, depth), frame);
            p.Box("Socle", V(0f, bottom * 0.5f, 0f), V(length - 0.06f, bottom, depth - 0.02f), frame);
            p.Block("Etagere", V(0f, height * 0.5f, 0f), V(length, height, depth));
            for (int l = 0; l < levels; l++)
            {
                float y = bottom + l * spacing;
                if (l > 0) p.Box("Tablette", V(0f, y - 0.012f, 0f), V(length - 0.06f, 0.024f, depth - 0.02f), frame);
                Goods(p, kind, -half + 0.05f, half - 0.05f, y, -depth * 0.5f + 0.02f, depth - 0.05f, spacing - 0.05f);
            }

            p.Pop();
        }

        // ================================================================== produits

        /// <summary>
        /// Garnit une tablette de <paramref name="x0"/> à <paramref name="x1"/> : des produits par
        /// « facings » (deux à cinq fois le même côte à côte), la face à <paramref name="front"/>
        /// et rangés vers +Z ; de temps en temps un trou (un produit déjà vendu, celui de derrière
        /// recule).
        /// </summary>
        private static void Goods(InteriorPlan p, string kind, float x0, float x1, float y, float front, float depth, float maxH)
        {
            float x = x0;
            while (x < x1 - 0.03f)
            {
                string type = ItemType(kind);
                float w, h, d;
                ItemSize(type, out w, out h, out d);
                if (h > maxH)
                {
                    float k = maxH / h;
                    h *= k;
                    w *= Mathf.Lerp(1f, k, 0.5f);
                }

                d = Mathf.Min(d, depth);
                string color = Product();
                string accent = Pick(3) == 0 ? "blanc" : Product();
                int count = type == "livre" || type == "cigarettes" ? 1 + Pick(3) : 2 + Pick(4);
                for (int i = 0; i < count && x + w <= x1; i++)
                {
                    float back = Rand() < 0.08f ? Rand(0.04f, Mathf.Max(0.05f, depth - d)) : 0f;
                    if (type == "livre") color = BookColor();
                    Item(p, type, V(x + w * 0.5f, y, front + d * 0.5f + back), w, h, d, color, accent);
                    x += w + (type == "cigarettes" || type == "livre" ? 0.002f : Rand(0.004f, 0.014f));
                }

                x += type == "livre" ? 0f : Rand(0f, 0.025f);
            }
        }

        private static string BookColor()
        {
            switch (Pick(7))
            {
                case 0: return "cuir_brun";
                case 1: return "cuir_rouge";
                case 2: return "noir";
                case 3: return "bleu";
                case 4: return "vert";
                case 5: return "bois_fonce";
                default: return Product();
            }
        }

        private static string ItemType(string kind)
        {
            switch (kind)
            {
                case "epicerie":
                {
                    string[] t = { "cereales", "boite", "boite", "conserve", "bouteille", "bocal", "sachet", "sachet", "pile_conserves" };
                    return t[Pick(t.Length)];
                }
                case "boissons":
                {
                    string[] t = { "soda", "soda", "canettes", "eau", "biere" };
                    return t[Pick(t.Length)];
                }
                case "cave":
                case "bar":
                {
                    string[] t = { "vin", "vin", "spiritueux", "spiritueux", "liqueur" };
                    return t[Pick(t.Length)];
                }
                case "pharmacie":
                {
                    string[] t = { "medicament", "medicament", "medicament", "pilulier", "flacon", "tube" };
                    return t[Pick(t.Length)];
                }
                case "cosmetique":
                {
                    string[] t = { "flacon", "flacon", "pompe", "medicament", "pot" };
                    return t[Pick(t.Length)];
                }
                case "quincaillerie":
                {
                    string[] t = { "peinture", "peinture", "aerosol", "carton", "boite", "boite_outils" };
                    return t[Pick(t.Length)];
                }
                case "tabac": return "cigarettes";
                case "livres": return "livre";
                case "vetements": return "pile_vetements";
                case "chaussures": return "chaussures";
                case "peluches": return "peluche";
                case "cafe":
                {
                    string[] t = { "cafe", "cafe", "tasse", "bocal" };
                    return t[Pick(t.Length)];
                }
                case "electronique":
                {
                    string[] t = { "appareil", "appareil", "boite", "carton" };
                    return t[Pick(t.Length)];
                }
                case "complements":
                {
                    string[] t = { "pot", "pot", "pot", "flacon" };
                    return t[Pick(t.Length)];
                }
                case "colis": return "carton";
                case "lessive":
                {
                    string[] t = { "baril", "bidon", "bidon" };
                    return t[Pick(t.Length)];
                }
                default: return "boite";
            }
        }

        private static void ItemSize(string type, out float w, out float h, out float d)
        {
            switch (type)
            {
                case "cereales": w = Rand(0.17f, 0.21f); h = Rand(0.26f, 0.32f); d = 0.07f; break;
                case "boite": w = Rand(0.1f, 0.16f); h = Rand(0.12f, 0.2f); d = Rand(0.05f, 0.09f); break;
                case "conserve": w = 0.075f; h = 0.11f; d = 0.075f; break;
                case "pile_conserves": w = 0.085f; h = 0.18f; d = 0.085f; break;
                case "bouteille": w = Rand(0.07f, 0.09f); h = Rand(0.24f, 0.31f); d = 0.08f; break;
                case "bocal": w = 0.09f; h = Rand(0.11f, 0.15f); d = 0.09f; break;
                case "sachet": w = Rand(0.16f, 0.22f); h = Rand(0.22f, 0.28f); d = 0.08f; break;
                case "soda": w = 0.075f; h = Rand(0.24f, 0.3f); d = 0.075f; break;
                case "canettes": w = 0.068f; h = 0.24f; d = 0.068f; break;
                case "eau": w = 0.085f; h = 0.3f; d = 0.085f; break;
                case "biere": w = 0.065f; h = 0.23f; d = 0.065f; break;
                case "vin": w = 0.078f; h = 0.31f; d = 0.078f; break;
                case "spiritueux": w = Rand(0.08f, 0.1f); h = Rand(0.26f, 0.32f); d = 0.08f; break;
                case "liqueur": w = 0.085f; h = Rand(0.2f, 0.26f); d = 0.085f; break;
                case "medicament": w = Rand(0.06f, 0.12f); h = Rand(0.05f, 0.14f); d = Rand(0.03f, 0.06f); break;
                case "pilulier": w = 0.05f; h = 0.09f; d = 0.05f; break;
                case "flacon": w = Rand(0.06f, 0.08f); h = Rand(0.16f, 0.22f); d = 0.05f; break;
                case "pompe": w = 0.07f; h = 0.22f; d = 0.07f; break;
                case "tube": w = 0.05f; h = 0.17f; d = 0.035f; break;
                case "pot": w = 0.12f; h = Rand(0.14f, 0.2f); d = 0.12f; break;
                case "peinture": w = 0.17f; h = 0.19f; d = 0.17f; break;
                case "aerosol": w = 0.06f; h = 0.21f; d = 0.06f; break;
                case "carton": w = Rand(0.2f, 0.32f); h = Rand(0.12f, 0.26f); d = Rand(0.18f, 0.3f); break;
                case "boite_outils": w = 0.4f; h = 0.18f; d = 0.2f; break;
                case "cigarettes": w = 0.056f; h = 0.088f; d = 0.024f; break;
                case "livre": w = Rand(0.02f, 0.05f); h = Rand(0.18f, 0.28f); d = Rand(0.15f, 0.2f); break;
                case "pile_vetements": w = 0.3f; h = Rand(0.12f, 0.24f); d = 0.26f; break;
                case "chaussures": w = 0.22f; h = 0.11f; d = 0.28f; break;
                case "peluche": w = Rand(0.18f, 0.28f); h = w * 1.2f; d = w * 0.8f; break;
                case "cafe": w = 0.12f; h = 0.2f; d = 0.07f; break;
                case "tasse": w = 0.1f; h = 0.09f; d = 0.08f; break;
                case "appareil": w = Rand(0.2f, 0.4f); h = Rand(0.1f, 0.26f); d = Rand(0.15f, 0.25f); break;
                case "baril": w = 0.24f; h = 0.3f; d = 0.18f; break;
                case "bidon": w = 0.14f; h = 0.3f; d = 0.1f; break;
                default: w = 0.12f; h = 0.15f; d = 0.08f; break;
            }
        }

        /// <summary>Un produit, posé par le centre de sa base.</summary>
        private static void Item(InteriorPlan p, string type, Vector3 at, float w, float h, float d, string color, string accent)
        {
            switch (type)
            {
                case "conserve":
                    p.Cylinder("Conserve", at + V(0f, h * 0.5f, 0f), w * 0.5f, h, "metal");
                    p.Cylinder("Etiquette", at + V(0f, h * 0.5f, 0f), w * 0.5f + 0.002f, h * 0.7f, color);
                    break;
                case "pile_conserves":
                    for (int i = 0; i < 3; i++)
                    {
                        float ch = h / 3f;
                        p.Cylinder("Boite ronde", at + V(0f, ch * (i + 0.5f), 0f), w * 0.5f, ch - 0.004f, i == 1 ? accent : color);
                    }

                    break;
                case "bouteille":
                case "soda":
                case "eau":
                case "biere":
                case "vin":
                case "liqueur":
                {
                    string glass = type == "vin" ? (Pick(2) == 0 ? "bouteille_verte" : "bouteille_brune")
                        : type == "biere" ? "bouteille_brune"
                        : type == "eau" ? "bouteille_claire"
                        : type == "liqueur" ? "liquide_ambre"
                        : color;
                    float body = type == "vin" || type == "biere" ? 0.6f : 0.68f;
                    float r = w * 0.5f;
                    p.Cylinder("Bouteille", at + V(0f, h * body * 0.5f, 0f), r, h * body, glass);
                    p.Sphere("Epaule", at + V(0f, h * body, 0f), V(w, w * 0.55f, w), glass);
                    p.Cylinder("Goulot", at + V(0f, h * (body + 1f) * 0.5f, 0f), r * 0.36f, h * (1f - body), glass);
                    p.Cylinder("Bouchon", at + V(0f, h - 0.012f, 0f), r * 0.42f, 0.03f, type == "vin" ? accent : "metal");
                    p.Cylinder("Etiquette", at + V(0f, h * body * 0.5f, 0f), r + 0.002f, h * 0.22f, type == "eau" ? "bleu" : accent);
                    break;
                }
                case "canettes":
                    for (int i = 0; i < 2; i++)
                    {
                        p.Cylinder("Canette", at + V(0f, 0.062f + i * 0.122f, 0f), w * 0.5f, 0.12f, color);
                        p.Cylinder("Couvercle", at + V(0f, 0.123f + i * 0.122f, 0f), w * 0.42f, 0.004f, "metal");
                    }

                    break;
                case "spiritueux":
                    p.Box("Spiritueux", at + V(0f, h * 0.34f, 0f), V(w, h * 0.68f, d), Pick(2) == 0 ? "liquide_ambre" : "bouteille_claire");
                    p.Cylinder("Goulot", at + V(0f, h * 0.8f, 0f), w * 0.16f, h * 0.26f, "bouteille_claire");
                    p.Cylinder("Bouchon", at + V(0f, h - 0.015f, 0f), w * 0.22f, 0.04f, accent == "blanc" ? "noir" : accent);
                    p.Box("Etiquette", at + V(0f, h * 0.36f, -d * 0.5f - 0.002f), V(w * 0.8f, h * 0.3f, 0.003f), Pick(2) == 0 ? "papier" : "noir");
                    break;
                case "bocal":
                case "pot":
                    p.Cylinder("Pot", at + V(0f, h * 0.42f, 0f), w * 0.5f, h * 0.84f, type == "pot" ? color : "bouteille_claire");
                    if (type == "bocal") p.Cylinder("Contenu", at + V(0f, h * 0.36f, 0f), w * 0.46f, h * 0.68f, color);
                    p.Cylinder("Couvercle", at + V(0f, h * 0.92f, 0f), w * 0.5f, h * 0.16f, type == "pot" ? "noir" : accent);
                    break;
                case "sachet":
                case "cafe":
                    p.Box("Sachet", at + V(0f, h * 0.5f, 0.01f), V(w, h, d), type == "cafe" ? "carton" : color, V(-6f, 0f, 0f));
                    p.Box("Soudure", at + V(0f, h + 0.005f, 0.02f), V(w, 0.02f, 0.012f), type == "cafe" ? "noir" : color, V(-6f, 0f, 0f));
                    p.Box("Visuel", at + V(0f, h * 0.45f, -d * 0.5f + 0.008f), V(w * 0.7f, h * 0.4f, 0.004f), accent, V(-6f, 0f, 0f));
                    break;
                case "medicament":
                    p.Box("Boite", at + V(0f, h * 0.5f, 0f), V(w, h, d), "blanc");
                    p.Box("Bande", at + V(0f, h * 0.62f, -d * 0.5f - 0.0015f), V(w, h * 0.22f, 0.003f), color);
                    break;
                case "pilulier":
                    p.Cylinder("Pilulier", at + V(0f, h * 0.4f, 0f), w * 0.5f, h * 0.8f, Pick(2) == 0 ? "orange" : "blanc");
                    p.Cylinder("Capuchon", at + V(0f, h * 0.9f, 0f), w * 0.52f, h * 0.2f, "blanc");
                    break;
                case "flacon":
                case "pompe":
                case "bidon":
                    p.Box("Flacon", at + V(0f, h * 0.4f, 0f), V(w, h * 0.8f, d), color);
                    p.Box("Etiquette", at + V(0f, h * 0.4f, -d * 0.5f - 0.0015f), V(w * 0.8f, h * 0.4f, 0.003f), accent);
                    if (type == "pompe")
                    {
                        p.Cylinder("Pompe", at + V(0f, h * 0.88f, 0f), 0.012f, h * 0.16f, "blanc");
                        p.Box("Bec", at + V(0f, h * 0.96f, -0.025f), V(0.016f, 0.016f, 0.05f), "blanc");
                    }
                    else
                    {
                        p.Cylinder("Bouchon", at + V(type == "bidon" ? w * 0.25f : 0f, h * 0.88f, 0f), Mathf.Min(w, d) * 0.3f, h * 0.16f,
                            type == "bidon" ? "blanc" : accent);
                    }

                    if (type == "bidon") p.Box("Poignee", at + V(-w * 0.15f, h * 0.83f, 0f), V(w * 0.4f, 0.04f, 0.025f), color);
                    break;
                case "tube":
                    p.Box("Tube", at + V(0f, h * 0.5f, 0f), V(w, h, d), color, V(0f, 0f, 0f));
                    p.Cylinder("Capuchon", at + V(0f, 0.015f, 0f), d * 0.45f, 0.03f, "blanc");
                    break;
                case "peinture":
                    p.Cylinder("Pot de peinture", at + V(0f, h * 0.5f, 0f), w * 0.5f, h, "metal");
                    p.Cylinder("Etiquette", at + V(0f, h * 0.45f, 0f), w * 0.5f + 0.002f, h * 0.62f, color);
                    p.Cylinder("Couvercle", at + V(0f, h + 0.004f, 0f), w * 0.48f, 0.008f, color);
                    p.Cylinder("Anse", at + V(0f, h * 0.95f, 0f), 0.004f, w, "metal", V(0f, 0f, 90f));
                    break;
                case "aerosol":
                    p.Cylinder("Aerosol", at + V(0f, h * 0.38f, 0f), w * 0.5f, h * 0.76f, color);
                    p.Cylinder("Capot", at + V(0f, h * 0.86f, 0f), w * 0.46f, h * 0.24f, accent);
                    break;
                case "boite_outils":
                    p.Box("Caisse a outils", at + V(0f, h * 0.45f, 0f), V(w, h * 0.9f, d), Pick(2) == 0 ? "rouge" : "jaune");
                    p.Box("Poignee", at + V(0f, h, 0f), V(w * 0.5f, 0.03f, 0.03f), "noir");
                    p.Box("Fermoir", at + V(0f, h * 0.7f, -d * 0.5f - 0.005f), V(0.06f, 0.04f, 0.01f), "metal");
                    break;
                case "carton":
                    p.Box("Carton", at + V(0f, h * 0.5f, 0f), V(w, h, d), "carton");
                    p.Box("Adhesif", at + V(0f, h + 0.001f, 0f), V(0.05f, 0.002f, d), "bois_clair");
                    if (Pick(2) == 0) p.Box("Etiquette", at + V(w * 0.2f, h * 0.6f, -d * 0.5f - 0.002f), V(w * 0.3f, h * 0.25f, 0.003f), "blanc");
                    break;
                case "cigarettes":
                    p.Box("Paquet", at + V(0f, h * 0.5f, 0f), V(w, h, d), Pick(3) == 0 ? "blanc" : color);
                    p.Box("Bandeau", at + V(0f, h * 0.78f, -d * 0.5f - 0.0015f), V(w, h * 0.2f, 0.003f), Pick(2) == 0 ? "noir" : "blanc");
                    break;
                case "livre":
                    p.Box("Livre", at + V(0f, h * 0.5f, 0f), V(w, h, d), color, V(0f, 0f, Rand() < 0.08f ? Rand(-7f, 7f) : 0f));
                    if (Pick(2) == 0) p.Box("Titre", at + V(0f, h * 0.72f, -d * 0.5f - 0.0015f), V(w * 0.7f, 0.025f, 0.003f), "laiton");
                    break;
                case "pile_vetements":
                {
                    int n = Mathf.Max(2, Mathf.RoundToInt(h / 0.04f));
                    for (int i = 0; i < n; i++)
                    {
                        p.Box("Vetement plie", at + V(Rand(-0.01f, 0.01f), 0.02f + i * 0.04f, 0f), V(w, 0.038f, d), i % 3 == 2 ? accent : color,
                            V(0f, Rand(-3f, 3f), 0f));
                    }

                    break;
                }
                case "chaussures":
                    for (int k = -1; k <= 1; k += 2)
                    {
                        p.Box("Chaussure", at + V(k * 0.05f, 0.04f, 0.02f), V(0.09f, 0.08f, 0.24f), color);
                        p.Box("Semelle", at + V(k * 0.05f, 0.008f, 0.02f), V(0.095f, 0.016f, 0.25f), accent == "blanc" ? "blanc" : "caoutchouc");
                        p.Box("Tige", at + V(k * 0.05f, 0.09f, 0.08f), V(0.085f, 0.08f, 0.1f), color);
                    }

                    break;
                case "peluche":
                    p.Sphere("Corps", at + V(0f, h * 0.32f, 0f), V(w, h * 0.62f, d), color);
                    p.Sphere("Tete", at + V(0f, h * 0.78f, -d * 0.05f), V(w * 0.72f, h * 0.45f, d * 0.8f), color);
                    p.Sphere("Oreille", at + V(-w * 0.25f, h * 0.98f, 0f), V(w * 0.22f, h * 0.2f, d * 0.2f), color);
                    p.Sphere("Oreille", at + V(w * 0.25f, h * 0.98f, 0f), V(w * 0.22f, h * 0.2f, d * 0.2f), color);
                    p.Sphere("Museau", at + V(0f, h * 0.72f, -d * 0.42f), V(w * 0.3f, h * 0.18f, d * 0.2f), "blanc");
                    break;
                case "tasse":
                    p.Cylinder("Tasse", at + V(0f, h * 0.5f, 0f), w * 0.4f, h, Pick(2) == 0 ? "ceramique" : color);
                    p.Box("Anse", at + V(w * 0.45f, h * 0.5f, 0f), V(0.015f, h * 0.6f, 0.02f), "ceramique");
                    break;
                case "appareil":
                    p.Box("Appareil", at + V(0f, h * 0.5f, 0f), V(w, h, d), Pick(2) == 0 ? "plastique_noir" : "metal");
                    p.Box("Facade", at + V(0f, h * 0.55f, -d * 0.5f - 0.002f), V(w * 0.8f, h * 0.5f, 0.004f),
                        Pick(3) == 0 ? "ecran_tele" : "plastique_noir");
                    break;
                case "baril":
                    p.Box("Baril de lessive", at + V(0f, h * 0.5f, 0f), V(w, h, d), color);
                    p.Box("Poignee", at + V(0f, h + 0.01f, 0f), V(w * 0.5f, 0.02f, 0.03f), "blanc");
                    p.Box("Visuel", at + V(0f, h * 0.5f, -d * 0.5f - 0.002f), V(w * 0.7f, h * 0.45f, 0.003f), accent);
                    break;
                default:
                    p.Box("Produit", at + V(0f, h * 0.5f, 0f), V(w, h, d), color);
                    if (Pick(2) == 0) p.Box("Visuel", at + V(0f, h * 0.55f, -d * 0.5f - 0.0015f), V(w * 0.8f, h * 0.35f, 0.003f), accent);
                    break;
            }
        }

        /// <summary>Une bouteille seule (bar, coiffeur), à l'échelle <paramref name="scale"/>.</summary>
        private static void Bottle(InteriorPlan p, Vector3 at, string glass, float scale)
        {
            float r = 0.04f * scale, h = 0.3f * scale;
            p.Cylinder("Bouteille", at + V(0f, h * 0.33f, 0f), r, h * 0.66f, glass);
            p.Sphere("Epaule", at + V(0f, h * 0.66f, 0f), V(r * 2f, r * 1.2f, r * 2f), glass);
            p.Cylinder("Goulot", at + V(0f, h * 0.82f, 0f), r * 0.35f, h * 0.34f, glass);
            p.Cylinder("Bec verseur", at + V(0f, h + 0.01f, 0f), r * 0.2f, 0.03f * scale, "chrome");
        }

        // ================================================================== frigos

        /// <summary>
        /// Une rangée de frigos vitrés : caisson, fond lumineux, clayettes garnies, portes à
        /// cadre et poignée, bandeau éclairé, grille de ventilation. Une lampe froide devant.
        /// </summary>
        private static void Fridges(InteriorPlan p, Vector3 at, float yaw, int doors, string kind)
        {
            p.Push(at, yaw);
            const float door = 0.76f, depth = 0.78f, height = 2.12f;
            float width = doors * door;
            float half = width * 0.5f;
            float face = -depth * 0.5f;

            p.Block("Frigos", V(0f, height * 0.5f, 0f), V(width, height, depth));
            p.Box("Fond eclaire", V(0f, 1.1f, depth * 0.5f - 0.03f), V(width - 0.06f, 1.7f, 0.02f), "frigo");
            p.Box("Plafond eclaire", V(0f, 1.94f, 0f), V(width - 0.08f, 0.02f, depth - 0.08f), "lumiere");
            p.Box("Caisson haut", V(0f, height - 0.08f, 0f), V(width, 0.16f, depth), "plastique_noir");
            p.Box("Bandeau", V(0f, height - 0.08f, face - 0.005f), V(width - 0.06f, 0.11f, 0.01f), "neon");
            p.Box("Socle", V(0f, 0.1f, 0f), V(width, 0.2f, depth), "plastique_noir");
            p.Box("Grille", V(0f, 0.1f, face - 0.005f), V(width - 0.08f, 0.12f, 0.01f), "metal_noir");
            p.Box("Joue", V(-half + 0.02f, height * 0.5f, 0f), V(0.04f, height, depth), "metal");
            p.Box("Joue", V(half - 0.02f, height * 0.5f, 0f), V(0.04f, height, depth), "metal");

            float[] ys = { 0.21f, 0.55f, 0.89f, 1.23f, 1.57f };
            for (int i = 0; i < ys.Length; i++)
            {
                float y = ys[i];
                float next = i + 1 < ys.Length ? ys[i + 1] : 1.92f;
                p.Box("Clayette", V(0f, y - 0.008f, 0.03f), V(width - 0.08f, 0.016f, depth - 0.14f), "metal");
                p.Box("Bord de clayette", V(0f, y + 0.012f, face + 0.1f), V(width - 0.08f, 0.04f, 0.006f), "blanc");
                Goods(p, kind, -half + 0.05f, half - 0.05f, y, face + 0.11f, depth - 0.2f, next - y - 0.03f);
            }

            for (int k = 0; k < doors; k++)
            {
                float x0 = -half + k * door;
                p.Box("Cadre de porte", V(x0 + 0.025f, 1.08f, face - 0.03f), V(0.05f, 1.76f, 0.05f), "metal_noir");
                p.Box("Cadre de porte", V(x0 + door - 0.025f, 1.08f, face - 0.03f), V(0.05f, 1.76f, 0.05f), "metal_noir");
                p.Box("Cadre de porte", V(x0 + door * 0.5f, 0.22f, face - 0.03f), V(door, 0.05f, 0.05f), "metal_noir");
                p.Box("Cadre de porte", V(x0 + door * 0.5f, 1.94f, face - 0.03f), V(door, 0.05f, 0.05f), "metal_noir");
                p.Box("Porte vitree", V(x0 + door * 0.5f, 1.08f, face - 0.03f), V(door - 0.08f, 1.68f, 0.012f), "verre");
                p.Cylinder("Poignee", V(x0 + door - 0.09f, 1.1f, face - 0.1f), 0.013f, 0.72f, "chrome");
                p.Box("Patte", V(x0 + door - 0.09f, 1.42f, face - 0.07f), V(0.02f, 0.02f, 0.06f), "chrome");
                p.Box("Patte", V(x0 + door - 0.09f, 0.78f, face - 0.07f), V(0.02f, 0.02f, 0.06f), "chrome");
            }

            p.Light(V(0f, 1.3f, face - 0.45f), new Color(0.8f, 0.9f, 1f), 0.55f, 2.6f);
            p.Pop();
        }

        /// <summary>Un congélateur coffre : caisse blanche, couvercles vitrés coulissants, glaces dedans.</summary>
        private static void ChestFreezer(InteriorPlan p, Vector3 at, float yaw, float length)
        {
            p.Push(at, yaw);
            const float depth = 0.72f, height = 0.86f;
            p.Box("Congelateur", V(0f, height * 0.5f - 0.04f, 0f), V(length, height - 0.08f, depth), "blanc", true);
            p.Box("Bandeau", V(0f, 0.55f, -depth * 0.5f - 0.004f), V(length - 0.1f, 0.2f, 0.008f), "bleu");
            p.Box("Socle", V(0f, 0.04f, 0f), V(length - 0.04f, 0.08f, depth - 0.04f), "plastique_noir");
            p.Box("Fond", V(0f, height - 0.2f, 0f), V(length - 0.1f, 0.02f, depth - 0.1f), "frigo");
            for (float x = -length * 0.5f + 0.1f; x < length * 0.5f - 0.1f; x += 0.13f)
            {
                for (float z = -depth * 0.5f + 0.12f; z < depth * 0.5f - 0.08f; z += 0.16f)
                {
                    p.Box("Glace", V(x, height - 0.15f, z), V(0.11f, 0.08f, 0.14f), Product(), V(0f, Rand(-8f, 8f), 0f));
                }
            }

            p.Box("Couvercle", V(-length * 0.25f, height - 0.02f, 0f), V(length * 0.5f, 0.02f, depth - 0.04f), "verre");
            p.Box("Couvercle", V(length * 0.25f, height - 0.035f, 0f), V(length * 0.5f, 0.02f, depth - 0.04f), "verre");
            p.Box("Rebord", V(0f, height - 0.01f, -depth * 0.5f + 0.02f), V(length, 0.04f, 0.04f), "metal");
            p.Box("Rebord", V(0f, height - 0.01f, depth * 0.5f - 0.02f), V(length, 0.04f, 0.04f), "metal");
            p.Pop();
        }

        /// <summary>Une vitrine indépendante (bijouterie, prêteur), sur son meuble.</summary>
        private static void DisplayCase(InteriorPlan p, Vector3 at, float yaw, float length, string goods)
        {
            p.Push(at, yaw);
            ShowcaseBody(p, length, "bois_fonce", goods);
            p.Pop();
        }
    }
}
