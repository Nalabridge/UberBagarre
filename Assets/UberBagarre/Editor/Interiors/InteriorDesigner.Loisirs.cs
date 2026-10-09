using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les lieux où l'on reste : diner, restaurant (pizzeria, italien, chinois), café, bar et
    /// boîte, casino, salle d'arcade, stand de tir, salle de boxe ; et les bureaux : médecin,
    /// concession, motel, commissariat, avocat, agence immobilière.
    /// </summary>
    public static partial class InteriorDesigner
    {
        // ================================================================== diner

        private static void Diner(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;

            // Le long comptoir du fond et ses tabourets ; la caisse au bout, à droite.
            float cz = d - 2.0f;
            const float cx = -0.6f, service = 2.0f;
            Counter(p, V(cx, 0f, cz), 0f, 6.4f, CounterStyle.Shop, "rouge", "stratifie", service);
            for (float x = cx - 3.0f; x <= cx + 3.0f; x += 0.75f)
            {
                if (Mathf.Abs(x - (cx + service)) < 0.6f) continue;
                Stool(p, V(x, 0f, cz - 0.72f), "cuir_rouge");
            }

            p.Push(V(cx, 0f, cz), 0f);
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = V(-2.2f + i * 1.4f, 1.02f, -0.12f);
                p.Cylinder("Presentoir a tartes", at + V(0f, 0.02f, 0f), 0.16f, 0.04f, "chrome");
                p.Cylinder("Tarte", at + V(0f, 0.07f, 0f), 0.13f, 0.06f, i == 1 ? "rouge" : "bois_clair");
                p.Sphere("Cloche", at + V(0f, 0.12f, 0f), V(0.32f, 0.26f, 0.32f), "verre");
            }

            p.Pop();

            // Derrière : le plan de travail inox, le passe-plat lumineux de la cuisine, les menus.
            p.Box("Plan de travail", V(cx, 0.45f, d - 0.32f), V(7.0f, 0.9f, 0.6f), "inox", true);
            p.Box("Plinthe", V(cx, 0.05f, d - 0.615f), V(7.0f, 0.1f, 0.01f), "noir");
            for (int i = 0; i < 2; i++)
            {
                Vector3 pot = V(cx + 1.6f + i * 0.3f, 0.92f, d - 0.3f);
                p.Box("Plaque chauffante", pot + V(0f, 0.01f, 0f), V(0.24f, 0.02f, 0.22f), "metal_noir");
                p.Sphere("Verseuse", pot + V(0f, 0.11f, 0f), V(0.15f, 0.17f, 0.15f), "verre");
                p.Sphere("Cafe", pot + V(0f, 0.08f, 0f), V(0.14f, 0.1f, 0.14f), "bois_fonce");
                p.Box("Poignee", pot + V(0.09f, 0.14f, 0f), V(0.03f, 0.1f, 0.03f), "noir");
            }

            p.Box("Machine a milk-shake", V(cx + 2.6f, 1.15f, d - 0.3f), V(0.25f, 0.5f, 0.25f), "chrome");
            for (int i = 0; i < 3; i++) p.Cylinder("Gobelet inox", V(cx + 2.52f + i * 0.08f, 0.98f, d - 0.42f), 0.035f, 0.15f, "inox");
            for (int i = 0; i < 6; i++) p.Cylinder("Tasse", V(cx - 2.8f + i * 0.12f, 0.95f, d - 0.25f), 0.04f, 0.09f, "ceramique");

            Vector3 pass = V(-2.2f, 1.6f, d);
            p.Box("Passe-plat", pass + V(0f, 0f, -0.01f), V(2.3f, 0.85f, 0.02f), "vitre_cuisine");
            for (int i = 0; i < 2; i++) p.Box("Etagere de cuisine", pass + V(0f, 0.12f - i * 0.3f, -0.022f), V(2.3f, 0.025f, 0.004f), "inox");
            for (int i = 0; i < 5; i++)
            {
                p.Box("Casserole", pass + V(-0.9f + i * 0.45f, 0.2f, -0.023f), V(Rand(0.16f, 0.26f), Rand(0.1f, 0.16f), 0.004f), "metal_noir");
            }
            p.Box("Cadre", pass + V(0f, 0.46f, -0.03f), V(2.5f, 0.08f, 0.06f), "inox");
            p.Box("Cadre", pass + V(-1.21f, 0f, -0.03f), V(0.08f, 1.0f, 0.06f), "inox");
            p.Box("Cadre", pass + V(1.21f, 0f, -0.03f), V(0.08f, 1.0f, 0.06f), "inox");
            p.Box("Tablette chaude", pass + V(0f, -0.44f, -0.2f), V(2.4f, 0.04f, 0.4f), "inox");
            for (int i = 0; i < 3; i++)
            {
                p.Box("Lampe chauffante", pass + V(-0.8f + i * 0.8f, 0.36f, -0.2f), V(0.4f, 0.06f, 0.12f), "lumiere_chaude");
                p.Cylinder("Assiette", pass + V(-0.8f + i * 0.8f + Rand(-0.1f, 0.1f), -0.41f, -0.2f), 0.13f, 0.015f, "ceramique");
                p.Sphere("Plat", pass + V(-0.8f + i * 0.8f, -0.38f, -0.2f), V(0.16f, 0.06f, 0.12f), Pick(2) == 0 ? "bois_clair" : "jaune");
            }

            p.Box("Rail a bons", pass + V(0f, 0.36f, -0.06f), V(2.2f, 0.02f, 0.02f), "inox");
            for (int i = 0; i < 5; i++) p.Box("Bon", pass + V(-0.9f + i * Rand(0.3f, 0.45f), 0.27f, -0.07f), V(0.08f, 0.16f, 0.002f), "papier");
            p.Light(pass + V(0f, 0f, -0.6f), new Color(1f, 0.8f, 0.55f), 0.9f, 3.5f);
            MenuBoard(p, V(3.6f, 2.2f, d), 0f, 2.6f, 0.7f, true);

            // Les banquettes le long des deux murs : table contre le mur, une banquette de chaque côté.
            for (int k = -1; k <= 1; k += 2)
            {
                float tx = k * (hw - 0.65f);
                for (int i = 0; i < 3; i++)
                {
                    float tz = 2.0f + i * 2.25f;
                    Table(p, V(tx, 0f, tz), 0f, 1.3f, 0.75f, "stratifie", false, "diner");
                    Booth(p, V(tx, 0f, tz - 0.735f), 180f, 1.3f, "cuir_rouge", "inox");
                    Booth(p, V(tx, 0f, tz + 0.735f), 0f, 1.3f, "cuir_rouge", "inox");
                    p.Box("Applique", V(k * (hw - 0.03f), 1.6f, tz), V(0.06f, 0.18f, 0.12f), "lumiere_chaude");
                }

                Frame(p, V(k * hw, 2.25f, 3.1f), k * 90f, 0.6f, 0.45f, "photo");
                Frame(p, V(k * hw, 2.25f, 5.4f), k * 90f, 0.6f, 0.45f, "affiche");
            }

            // Au milieu, quatre tables chromées.
            for (int i = 0; i < 4; i++)
            {
                Vector3 at = V(i % 2 == 0 ? -2.0f : 2.0f, 0f, i < 2 ? 3.6f : 6.2f);
                TableSet(p, at, 0f, 0.8f, 0.8f, 4, "stratifie", "cuir_rouge", "chrome", false, "diner", 1);
            }

            CeilingFan(p, V(-2.0f, h, 4.9f));
            CeilingFan(p, V(2.0f, h, 4.9f));
            Jukebox(p, V(-hw + 0.32f, 0f, d - 1.6f), -90f);
            CoatRack(p, V(-1.6f, 0f, 0.5f));
            p.Box("Distributeur de bonbons", V(1.6f, 0.5f, 0.6f), V(0.3f, 1.0f, 0.3f), "rouge", true);
            p.Sphere("Boule de verre", V(1.6f, 1.18f, 0.6f), V(0.36f, 0.36f, 0.36f), "verre");
            for (int i = 0; i < 12; i++) p.Sphere("Bonbon", V(1.6f + Rand(-0.1f, 0.1f), 1.1f + Rand(0f, 0.12f), 0.6f + Rand(-0.1f, 0.1f)), V(0.05f, 0.05f, 0.05f), Product());
            Basics(p, V(-0.6f + service + 1.0f, 0f, cz - 1.2f), false, true);
        }

        // ================================================================== restaurant

        /// <summary>Le restaurant à nappes : pizzeria (four à bois), italien, chinois (lanternes, aquarium).</summary>
        private static void Trattoria(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            bool chinese = Named("dragon", "chin");
            bool pizza = Named("pizz");

            // Le bar à droite au fond : caisse, verres ; les vins derrière.
            float cz = d - 1.9f;
            Counter(p, V(3.6f, 0f, cz), 0f, 3.0f, CounterStyle.Bar, chinese ? "rouge" : "bois_fonce", chinese ? "laiton" : "bois", -0.6f);
            WallShelf(p, V(3.6f, 0f, d - 0.17f), 0f, 3.2f, 4, "bar", "bois_fonce", 0.3f, 0.4f, 0.4f);
            MenuBoard(p, V(-0.6f, 2.2f, d), 0f, 1.6f, 0.8f, false);

            // Le four à pizza en briques, la bouche rougeoyante (la pizzeria) ; sinon, une desserte.
            if (pizza)
            {
                p.Define("brique_four", new Color(0.8f, 0.5f, 0.38f), 0.2f, 0f, "brique");
                Vector3 oven = V(-3.6f, 0f, d - 0.9f);
                p.Box("Socle du four", oven + V(0f, 0.45f, 0f), V(1.7f, 0.9f, 1.5f), "brique_four", true);
                p.Sphere("Voute", oven + V(0f, 0.9f, 0.05f), V(1.6f, 1.2f, 1.4f), "brique_four");
                p.Box("Bouche", oven + V(0f, 1.1f, -0.68f), V(0.6f, 0.36f, 0.08f), "noir");
                p.Box("Braises", oven + V(0f, 1.0f, -0.6f), V(0.5f, 0.12f, 0.1f), "neon_ambre");
                p.Cylinder("Conduit", oven + V(0f, (1.45f + h) * 0.5f, 0.2f), 0.15f, h - 1.45f, "metal_noir");
                p.Box("Pelle", oven + V(0.95f, 1.1f, -0.5f), V(0.3f, 0.01f, 0.3f), "metal", V(0f, 0f, 80f));
                p.Cylinder("Manche de pelle", oven + V(1.0f, 0.5f, -0.5f), 0.015f, 1.2f, "bois");
                p.Light(oven + V(0f, 1.1f, -1.1f), new Color(1f, 0.5f, 0.2f), 1.3f, 3.5f);
            }
            else
            {
                p.Box("Desserte", V(-3.6f, 0.45f, d - 0.3f), V(2.0f, 0.9f, 0.55f), "bois_fonce", true);
                for (int i = 0; i < 8; i++) p.Cylinder("Assiettes", V(-4.3f + (i % 4) * 0.42f, 0.95f + (i / 4) * 0.05f, d - 0.3f), 0.13f, 0.04f, "ceramique");
            }

            // Les tables nappées, dressées, en trois rangées ; une banquette le long du mur de droite.
            string seat = chinese ? "tissu_rouge" : "cuir_brun";
            float[] xs = { -3.8f, -1.4f, 1.4f };
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    if (r == 2 && c == 2) continue;
                    Vector3 at = V(xs[c], 0f, 2.6f + r * 2.4f);
                    TableSet(p, at, 0f, 0.9f, 0.9f, 4, "blanc", seat, "bois_fonce", chinese, "restaurant", chinese ? 2 : 0);
                    if (chinese)
                    {
                        p.Oval("Nappe", at + V(0f, 0.69f, 0f), V(1.0f, 0.14f, 1.0f), "blanc");
                        p.Cylinder("Plateau tournant", at + V(0f, 0.765f, 0f), 0.3f, 0.02f, "verre");
                    }
                    else
                    {
                        p.Box("Nappe", at + V(0f, 0.69f, 0f), V(1.02f, 0.14f, 1.02f), "blanc");
                        p.Box("Chemin de table", at + V(0f, 0.762f, 0f), V(0.3f, 0.004f, 1.04f), "tissu_rouge");
                    }
                }
            }

            for (int i = 0; i < 2; i++)
            {
                float z = 2.6f + i * 2.4f;
                Booth(p, V(hw - 0.33f, 0f, z), 90f, 1.6f, seat, "bois_fonce");
                Table(p, V(hw - 1.1f, 0f, z), 0f, 0.7f, 0.9f, "blanc", false, "restaurant");
                Chair(p, V(hw - 1.75f, 0f, z), -90f, seat, "bois_fonce", 2);
            }

            // Le décor : lanternes et aquarium chez le chinois ; ailleurs des paysages et des ficus.
            if (chinese)
            {
                p.Glow("lanterne", new Color(0.9f, 0.15f, 0.1f), new Color(1f, 0.25f, 0.1f) * 1.6f);
                for (int i = 0; i < 6; i++)
                {
                    Vector3 at = V(-3.0f + (i % 3) * 3.0f, h - 0.75f, 3.8f + (i / 3) * 2.6f);
                    p.Cylinder("Fil", at + V(0f, 0.45f, 0f), 0.005f, 0.5f, "noir");
                    p.Sphere("Lanterne", at, V(0.4f, 0.48f, 0.4f), "lanterne");
                    p.Cylinder("Cerclage", at + V(0f, 0.22f, 0f), 0.12f, 0.05f, "laiton");
                    p.Cylinder("Cerclage", at + V(0f, -0.22f, 0f), 0.12f, 0.05f, "laiton");
                    p.Cylinder("Pompon", at + V(0f, -0.38f, 0f), 0.03f, 0.25f, "jaune");
                }

                Vector3 tank = V(-hw + 0.3f, 0f, 1.6f);
                p.Box("Meuble d'aquarium", tank + V(0f, 0.4f, 0f), V(0.55f, 0.8f, 1.6f), "noir", true);
                p.Box("Aquarium", tank + V(0f, 1.15f, 0f), V(0.5f, 0.7f, 1.5f), "verre");
                p.Box("Eau", tank + V(0f, 1.12f, 0f), V(0.46f, 0.62f, 1.46f), "ecran");
                for (int i = 0; i < 6; i++) p.Sphere("Poisson", tank + V(Rand(-0.15f, 0.15f), Rand(0.9f, 1.35f), Rand(-0.6f, 0.6f)), V(0.03f, 0.05f, 0.1f), "orange");
                p.Light(tank + V(0.6f, 1.2f, 0f), new Color(0.4f, 0.7f, 1f), 0.6f, 2.5f);
            }
            else
            {
                Plant(p, V(-hw + 0.4f, 0f, 0.5f), 2);
                Plant(p, V(hw - 0.4f, 0f, 0.5f), 2);
                for (int k = -1; k <= 1; k += 2)
                {
                    for (int i = 0; i < 2; i++) Frame(p, V(k * hw, 2.05f, 3.6f + i * 2.4f), k * 90f, 0.8f, 0.55f, "photo");
                }

                Barrel(p, V(-hw + 0.35f, 0f, 6.2f), "bois", false);
            }

            CoatRack(p, V(-1.7f, 0f, 0.5f));
            Basics(p, V(2.2f, 0f, cz - 0.9f), false, true);
        }

        // ================================================================== café

        private static void Coffee(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            float cz = d - 2.1f;
            Counter(p, V(1.0f, 0f, cz), 0f, 4.0f, CounterStyle.Shop, "bois", "marbre_noir", 0.6f);
            DisplayCase(p, V(-1.6f, 0f, cz), 0f, 1.2f, "patisserie");
            p.Push(V(1.0f, 0f, cz), 0f);
            for (int i = 0; i < 4; i++) p.Cylinder("Gobelets", V(-1.6f + i * 0.1f, 1.12f, 0.15f), 0.04f, 0.2f, "blanc");
            p.Pop();

            // Le plan de travail du fond : la machine à espresso, les étagères de tasses et de cafés.
            p.Box("Plan du fond", V(1.0f, 0.45f, d - 0.3f), V(5.0f, 0.9f, 0.6f), "bois_fonce", true);
            p.Box("Plateau du fond", V(1.0f, 0.915f, d - 0.3f), V(5.04f, 0.03f, 0.62f), "marbre");
            EspressoMachine(p, V(1.2f, 0.93f, d - 0.32f), 0f);
            p.Box("Blender", V(2.6f, 1.1f, d - 0.3f), V(0.18f, 0.35f, 0.18f), "noir");
            for (int s = 0; s < 2; s++)
            {
                float y = 1.45f + s * 0.4f;
                p.Box("Etagere", V(1.0f, y, d - 0.13f), V(5.0f, 0.03f, 0.25f), "bois");
                Goods(p, "cafe", -1.4f, 3.4f, y + 0.015f, d - 0.24f, 0.2f, 0.34f);
            }

            MenuBoard(p, V(1.0f, 2.4f, d), 0f, 2.6f, 0.9f, false);
            SignAt(V(hw - 0.03f, 2.3f, 4.5f), 90f, 3.6f);

            // La vitrine de gauche : une tablette et ses tabourets ; à droite, des petites tables rondes.
            float x0, x1;
            WindowSpan(p, out x0, out x1);
            p.Box("Tablette de vitrine", V(-(x0 + x1) * 0.5f, 1.05f, 0.3f), V(x1 - x0, 0.05f, 0.42f), "bois", true);
            for (int i = 0; i < 3; i++) Stool(p, V(-x0 - 0.5f - i * 1.0f, 0f, 0.85f), "cuir_noir");
            for (int i = 0; i < 2; i++)
            {
                TableSet(p, V(2.9f, 0f, 2.0f + i * 1.9f), 90f, 0.65f, 0.65f, 2, "marbre", "bois", "bois_fonce", true, "cafe");
            }

            // La grande table commune, et le coin fauteuils avec sa bibliothèque.
            TableSet(p, V(-1.6f, 0f, 3.3f), 0f, 1.6f, 0.8f, 4, "bois", "bois", "bois_fonce", false, "cafe", 0);
            Rug(p, V(-3.1f, 0f, 6.0f), 0f, 2.2f, 2.0f, "tissu_rouge", "laiton");
            Armchair(p, V(-3.5f, 0f, 5.3f), 180f + 30f, "cuir_brun");
            Armchair(p, V(-2.9f, 0f, 6.9f), 20f, "tissu");
            CoffeeTable(p, V(-3.2f, 0f, 6.1f), 0f, 0.7f, 0.5f, "bois_fonce");
            Bookcase(p, V(-hw + 0.16f, 0f, 7.4f), -90f, 1.4f, 2.0f, "bois_fonce", "livres");
            FloorLamp(p, V(-hw + 0.35f, 0f, 5.0f));
            p.Box("Station lait et sucre", V(hw - 0.3f, 0.45f, 6.0f), V(0.5f, 0.9f, 0.9f), "bois_fonce", true);
            for (int i = 0; i < 4; i++) p.Cylinder("Pot", V(hw - 0.3f, 0.96f, 5.7f + i * 0.2f), 0.04f, 0.12f, i % 2 == 0 ? "inox" : "verre");
            Plant(p, V(hw - 0.4f, 0f, 0.5f), 2);
            for (int i = 0; i < 3; i++) Frame(p, V(-hw, 2.2f, 2.0f + i * 1.3f), -90f, 0.45f, 0.6f);
            Basics(p, V(3.6f, 0f, cz - 0.6f), false, true);
        }

        // ================================================================== bar, boîte

        private static void Bar(InteriorPlan p, bool club)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;

            // Le bar à droite, sur six mètres : tireuses, caisse au fond ; derrière, le miroir et les bouteilles.
            const float bx = 3.0f, bz = 5.6f, length = 6.0f, service = -2.0f;
            Counter(p, V(bx, 0f, bz), 90f, length, CounterStyle.Bar, club ? "plastique_noir" : "bois_fonce", club ? "marbre_noir" : "bois", service);
            for (float z = bz - length * 0.5f + 0.4f; z <= bz + length * 0.5f - 0.3f; z += 0.75f)
            {
                if (Mathf.Abs(z - (bz - service)) < 0.6f) continue;
                Stool(p, V(bx - 0.88f, 0f, z), club ? "cuir_noir" : "cuir_rouge");
            }

            BackBar(p, V(hw, 0f, bz), 90f, length + 0.4f, club);

            if (club)
            {
                // La piste lumineuse, la cabine du DJ, la boule à facettes, les canapés.
                DanceFloor(p, V(-1.5f, 0f, 6.0f), 4, 4);
                DjBooth(p, V(-1.5f, 0f, d - 0.7f), 0f);
                MirrorBall(p, V(-1.5f, h - 0.7f, 6.0f), h);
                for (int i = 0; i < 2; i++)
                {
                    Sofa(p, V(-hw + 0.44f, 0f, 1.6f + i * 2.4f), -90f, 2.0f, "cuir_noir");
                    CoffeeTable(p, V(-hw + 1.4f, 0f, 1.6f + i * 2.4f), 90f, 0.9f, 0.5f, "marbre_noir");
                }

                for (int k = 0; k < 3; k++)
                {
                    string neon = k == 1 ? "neon_cyan" : "neon_rose";
                    p.Box("Ruban lumineux", V(-hw + 0.03f, h - 0.3f - k * 0.04f, d * 0.5f), V(0.03f, 0.025f, d - 0.4f), neon);
                }

                p.Box("Ruban lumineux", V(0f, h - 0.3f, d - 0.03f), V(p.Size.x - 0.4f, 0.025f, 0.03f), "neon_cyan");
                p.Neon("VIP", V(-hw + 0.03f, 2.2f, 2.8f), -90f, 0.3f, "neon_rose");
                Stanchions(p, V(-1.2f, 0f, 1.0f), V(-1.2f, 0f, 2.6f), true);
            }
            else
            {
                // Les banquettes à gauche, deux mange-debout, le billard, les fléchettes, la télé.
                for (int i = 0; i < 2; i++)
                {
                    float tz = 2.2f + i * 2.25f;
                    float tx = -hw + 0.65f;
                    Table(p, V(tx, 0f, tz), 0f, 1.3f, 0.75f, "bois", false, "bar");
                    Booth(p, V(tx, 0f, tz - 0.735f), 180f, 1.3f, "cuir_brun", "bois_fonce");
                    Booth(p, V(tx, 0f, tz + 0.735f), 0f, 1.3f, "cuir_brun", "bois_fonce");
                }

                for (int i = 0; i < 2; i++)
                {
                    Vector3 at = V(-2.2f, 0f, 2.8f + i * 2.1f);
                    Table(p, at, 0f, 0.6f, 0.6f, "bois", true, "bar", 1.05f);
                    Stool(p, at + V(-0.55f, 0f, 0f), "cuir_brun");
                    Stool(p, at + V(0.55f, 0f, 0f), "cuir_brun");
                }

                PoolTable(p, V(-1.6f, 0f, 7.4f), 0f);
                p.Box("Lampe de billard", V(-1.6f, h - 0.75f, 7.4f), V(0.35f, 0.18f, 1.4f), "vert");
                p.Box("Lampe de billard (dessous)", V(-1.6f, h - 0.85f, 7.4f), V(0.3f, 0.01f, 1.3f), "lumiere_chaude");
                p.Cylinder("Tige", V(-1.6f, h - 0.33f, 7.4f), 0.008f, 0.66f, "metal_noir");
                p.Light(V(-1.6f, h - 1.0f, 7.4f), new Color(1f, 0.85f, 0.6f), 1.2f, 3.5f);
                p.Box("Ratelier a queues", V(-hw + 0.04f, 1.2f, 7.4f), V(0.06f, 1.4f, 0.6f), "bois_fonce");
                for (int i = 0; i < 4; i++) p.Cylinder("Queue", V(-hw + 0.1f, 1.0f, 7.18f + i * 0.14f), 0.012f, 1.45f, "bois_clair", V(4f, 0f, 0f));
                Dartboard(p, V(-4.2f, 1.73f, d), 0f);
                p.Box("Ligne de tir", V(-4.2f, 0.003f, d - 2.37f), V(0.6f, 0.006f, 0.04f), "blanc");
                WallTV(p, V(-hw, 2.35f, 3.3f), -90f, 1.1f);
                Jukebox(p, V(1.0f, 0f, d - 0.32f), 0f);
                ServiceDoor(p, V(-hw, 0f, 8.8f), -90f, "bois_fonce", "laiton");
                p.Neon("BIERE", V(-hw + 0.03f, 2.3f, 5.6f), -90f, 0.22f, "neon_ambre");
                Frame(p, V(-hw, 1.75f, 0.9f), -90f, 0.5f, 0.4f);
                Frame(p, V(-hw, 1.75f, 5.0f), -90f, 0.5f, 0.4f);
                CoatRack(p, V(-1.6f, 0f, 0.5f));
            }

            Basics(p, V(1.4f, 0f, 1.6f), true, true);
        }

        /// <summary>
        /// Le fond du bar (le dos en +Z) : meuble bas, plan, miroir, trois étagères de verre
        /// garnies de bouteilles et rétroéclairées, une rangée de verres.
        /// </summary>
        private static void BackBar(InteriorPlan p, Vector3 wall, float yaw, float length, bool club)
        {
            p.Push(wall, yaw);
            float half = length * 0.5f;
            p.Box("Meuble bas", V(0f, 0.45f, -0.25f), V(length, 0.9f, 0.5f), club ? "plastique_noir" : "bois_fonce", true);
            p.Box("Plan", V(0f, 0.915f, -0.26f), V(length + 0.02f, 0.03f, 0.52f), club ? "marbre_noir" : "bois");
            for (int i = 0; i < 4; i++)
            {
                p.Box("Porte", V(-half + length * (i + 0.5f) / 4f, 0.45f, -0.503f), V(length / 4f - 0.04f, 0.78f, 0.006f), club ? "plastique_noir" : "bois_fonce");
            }

            p.Box("Miroir", V(0f, 1.75f, -0.02f), V(length - 0.2f, 1.5f, 0.01f), "miroir");
            for (int s = 0; s < 3; s++)
            {
                float y = 1.2f + s * 0.42f;
                p.Box("Etagere de verre", V(0f, y, -0.14f), V(length - 0.3f, 0.015f, 0.24f), "verre");
                p.Box("Reglette", V(0f, y - 0.02f, -0.05f), V(length - 0.3f, 0.012f, 0.02f), club ? "neon_rose" : "lumiere_chaude");
                Goods(p, "bar", -half + 0.2f, half - 0.2f, y + 0.008f, -0.25f, 0.2f, 0.36f);
            }

            for (float x = -half + 0.2f; x < half - 0.2f; x += 0.09f)
            {
                if (Rand() < 0.35f) continue;
                p.Cylinder("Verre", V(x, 0.99f, -0.38f), 0.03f, 0.12f, "verre");
            }

            p.Pop();
        }

        // ================================================================== casino

        private static void Casino(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;

            // La caisse grillagée, à droite : comptoir à barreaux, murs de côté, coffre.
            const float cx = 6.4f, cz = 7.0f, length = 3.6f;
            Counter(p, V(cx, 0f, cz), 90f, length, CounterStyle.Window, "bois_fonce", "barreaux");
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Mur de la caisse", V((cx + hw) * 0.5f, 1.4f, cz + k * (length * 0.5f + 0.05f)), V(hw - cx + 0.4f, 2.8f, 0.1f), "bois_fonce", true);
            }

            Safe(p, V(hw - 0.36f, 0f, cz + 1.2f), 90f);
            p.Neon("CAISSE", V(cx - 0.3f, 2.55f, cz), 90f, 0.25f, "neon_ambre");

            // Les machines à sous : le long du mur de gauche, en îlot dos à dos, et au fond.
            for (int i = 0; i < 8; i++) SlotMachine(p, V(-hw + 0.32f, 0f, 2.0f + i * 0.75f), -90f);
            for (int i = 0; i < 6; i++)
            {
                SlotMachine(p, V(-4.5f, 0f, 3.0f + i * 0.75f), 90f);
                SlotMachine(p, V(-3.9f, 0f, 3.0f + i * 0.75f), -90f);
            }

            for (int i = 0; i < 5; i++) SlotMachine(p, V(-6.0f + i * 0.8f, 0f, d - 0.32f), 0f);

            // Les tables : black-jack, poker, roulette.
            CardTable(p, V(2.6f, 0f, 5.0f), 0f, 5);
            CardTable(p, V(2.6f, 0f, 9.6f), 0f, 5);
            RouletteTable(p, V(-1.4f, 0f, 10.0f), 0f);
            for (int i = 0; i < 3; i++) Stool(p, V(-2.4f + i * 0.9f, 0f, 9.0f), "cuir_rouge", 0.66f);

            // L'entrée : tapis noir bordé d'or, cordons de velours ; palmiers dans les coins.
            Rug(p, V(0f, 0f, 2.2f), 0f, 2.2f, 4.4f, "noir", "laiton");
            Stanchions(p, V(-1.25f, 0f, 0.8f), V(-1.25f, 0f, 3.2f), true);
            Stanchions(p, V(1.25f, 0f, 0.8f), V(1.25f, 0f, 3.2f), true);
            Plant(p, V(-hw + 0.5f, 0f, 0.6f), 2);
            Plant(p, V(hw - 0.5f, 0f, 0.6f), 2);
            Plant(p, V(hw - 0.5f, 0f, d - 0.6f), 2);

            // Le salon VIP au fond à droite.
            Sofa(p, V(5.6f, 0f, d - 0.46f), 0f, 2.4f, "cuir_rouge", true);
            CoffeeTable(p, V(5.6f, 0f, d - 1.5f), 0f, 1.0f, 0.55f, "marbre_noir");
            p.Neon("JACKPOT", V(-4.0f, 3.25f, d - 0.03f), 0f, 0.3f, "neon_ambre");
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Colonne", V(k * 1.9f, h * 0.5f, 7.6f), V(0.5f, h, 0.5f), "marbre", true);
                p.Box("Chapiteau", V(k * 1.9f, h - 0.15f, 7.6f), V(0.62f, 0.12f, 0.62f), "laiton");
                p.Box("Base", V(k * 1.9f, 0.1f, 7.6f), V(0.62f, 0.2f, 0.62f), "laiton");
            }

            for (int i = 0; i < 3; i++) Frame(p, V(hw, 2.4f, 1.6f + i * 1.3f), 90f, 0.9f, 0.6f, "affiche");
            DomeMirror(p, V(0f, h - 0.15f, 7.6f));
            Basics(p, V(-1.9f, 0f, 1.0f), true, true);
        }

        // ================================================================== arcade

        private static void ArcadeHall(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            float cz = d - 1.8f;
            Counter(p, V(-2.5f, 0f, cz), 0f, 3.4f, CounterStyle.Showcase, "plastique_noir", null, 0f, "electronique");
            WallShelf(p, V(-2.5f, 0f, d - 0.17f), 0f, 4.4f, 4, "peluches", "plastique_noir", 0.34f, 0.42f, 0.4f);
            p.Neon("LOTS", V(-2.5f, 2.35f, d - 0.03f), 0f, 0.26f, "neon_vert");
            SignAt(V(2.4f, 2.6f, d - 0.03f), 0f, 4.4f);

            // Les bornes : sur les deux murs, et un îlot dos à dos au milieu.
            for (int i = 0; i < 7; i++)
            {
                ArcadeCabinet(p, V(-hw + 0.38f, 0f, 2.0f + i * 0.8f), -90f);
                ArcadeCabinet(p, V(hw - 0.38f, 0f, 2.0f + i * 0.8f), 90f);
            }

            // L'îlot du milieu : quatre bornes tournées vers l'entrée, quatre dos à dos derrière.
            for (int i = 0; i < 4; i++)
            {
                float x = -1.2f + i * 0.8f;
                ArcadeCabinet(p, V(x, 0f, 5.0f), 0f);
                ArcadeCabinet(p, V(x, 0f, 5.72f), 180f);
            }

            AirHockey(p, V(-2.8f, 0f, 3.2f), 0f);
            ClawMachine(p, V(2.2f, 0f, 1.0f), 90f);
            ClawMachine(p, V(3.2f, 0f, 1.0f), 90f);
            Pinball(p, V(2.6f, 0f, 8.3f), 0f);
            Pinball(p, V(3.5f, 0f, 8.3f), 0f);
            p.Box("Changeur de jetons", V(hw - 0.26f, 0.8f, 9.4f), V(0.5f, 1.6f, 0.6f), "jaune", true);
            p.Box("Ecran", V(hw - 0.512f, 1.3f, 9.4f), V(0.004f, 0.15f, 0.3f), "ecran_vert");

            // Les rubans lumineux sur les murs, et la moquette constellée de motifs.
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Ruban lumineux", V(k * (hw - 0.03f), 2.6f, d * 0.5f), V(0.03f, 0.03f, d - 0.4f), k < 0 ? "neon_rose" : "neon_cyan");
            }

            p.Box("Ruban lumineux", V(0f, 2.95f, d - 0.03f), V(p.Size.x - 0.4f, 0.03f, 0.03f), "neon_ambre");
            string[] dots = { "jaune", "rose", "chromo", "orange" };
            for (int i = 0; i < 160; i++)
            {
                p.Box("Motif", V(Rand(-hw + 0.2f, hw - 0.2f), 0.002f, Rand(0.3f, d - 0.3f)), V(Rand(0.08f, 0.22f), 0.003f, 0.035f), dots[Pick(dots.Length)],
                    V(0f, Rand(0f, 180f), 0f));
            }

            Basics(p, V(-1.6f, 0f, 1.2f), true, false);
        }

        // ================================================================== stand de tir

        private static void Range(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            const float wallZ = 6.0f;

            // La boutique : vitrine-comptoir d'armes à gauche, râtelier derrière, étagères de munitions.
            Counter(p, V(-hw + 1.4f, 0f, 3.0f), -90f, 3.0f, CounterStyle.Showcase, "bois_fonce", null, 0f, "armes");
            p.Box("Panneau a fusils", V(-hw + 0.02f, 1.8f, 3.0f), V(0.03f, 1.6f, 3.2f), "bois_fonce");
            for (int i = 0; i < 8; i++)
            {
                float z = 1.7f + i * 0.37f;
                p.Box("Canon", V(-hw + 0.08f, 1.95f, z), V(0.03f, 0.95f, 0.03f), "metal_noir");
                p.Box("Crosse", V(-hw + 0.08f, 1.32f, z + 0.03f), V(0.05f, 0.36f, 0.09f), Pick(2) == 0 ? "bois" : "plastique_noir", V(8f, 0f, 0f));
                p.Box("Support", V(-hw + 0.06f, 2.3f, z), V(0.06f, 0.03f, 0.05f), "metal");
            }

            WallShelf(p, V(hw - 0.17f, 0f, 3.2f), 90f, 3.4f, 4, "munitions", "metal_noir", 0.34f, 0.4f, 0.2f);
            DisplayCase(p, V(1.4f, 0f, 2.6f), 0f, 1.6f, "armes");
            p.Box("Banc", V(1.4f, 0.22f, 4.6f), V(1.6f, 0.06f, 0.4f), "bois", true);
            for (int i = 0; i < 4; i++)
            {
                p.Box("Casque antibruit", V(hw - 0.08f, 2.1f, 1.7f + i * 0.3f), V(0.12f, 0.16f, 0.2f), i % 2 == 0 ? "jaune" : "noir");
            }

            // La cloison vitrée entre la boutique et les pas de tir, son passage à droite.
            const float gap0 = 1.9f, gap1 = 3.1f;
            SplitWall(p, wallZ, -hw, gap0, h);
            SplitWall(p, wallZ, gap1, hw, h);
            p.Box("Linteau", V((gap0 + gap1) * 0.5f, (2.2f + h) * 0.5f, wallZ), V(gap1 - gap0, h - 2.2f, 0.12f), "mur", true);
            SignAt(V(-1.5f, 2.85f, wallZ - 0.07f), 0f, 4.5f);

            // Les cinq pas de tir : cloisons, tablettes, rails de cibles au plafond, cibles à diverses distances.
            for (int i = 0; i < 5; i++)
            {
                float x = -hw + 1.0f + i * 2.0f;
                if (i > 0)
                {
                    p.Box("Cloison de tir", V(x - 1.0f, 1.0f, wallZ + 1.8f), V(0.06f, 2.0f, 1.4f), "gris", true);
                    p.Box("Mousse", V(x - 1.0f, 1.3f, wallZ + 1.8f), V(0.1f, 1.0f, 1.2f), "caoutchouc");
                }

                p.Box("Tablette de tir", V(x, 1.0f, wallZ + 2.3f), V(1.8f, 0.05f, 0.5f), "bois", true);
                p.Box("Boitier", V(x + 0.6f, 1.1f, wallZ + 2.4f), V(0.2f, 0.15f, 0.1f), "plastique_noir");
                p.Box("Bouton", V(x + 0.6f, 1.14f, wallZ + 2.34f), V(0.05f, 0.05f, 0.02f), "feu_rouge");
                p.Box("Rail", V(x, h - 0.08f, (wallZ + 2.5f + d) * 0.5f), V(0.05f, 0.05f, d - wallZ - 2.6f), "metal");
                float tz = wallZ + Rand(4f, d - wallZ - 1.5f);
                p.Cylinder("Fil", V(x, h - 0.6f, tz), 0.004f, 1.0f, "metal");
                p.Box("Cible", V(x, h - 1.5f, tz), V(0.46f, 0.8f, 0.01f), "papier");
                p.Box("Silhouette", V(x, h - 1.6f, tz - 0.008f), V(0.3f, 0.45f, 0.004f), "noir");
                p.Sphere("Tete", V(x, h - 1.24f, tz - 0.008f), V(0.16f, 0.2f, 0.004f), "noir");
                for (int k = 0; k < 5; k++)
                {
                    p.Cylinder("Douille", V(x + Rand(-0.6f, 0.6f), 0.008f, wallZ + Rand(1.4f, 2.2f)), 0.006f, 0.02f, "laiton", V(90f, Rand(0f, 180f), 0f));
                }
            }

            p.Box("Ligne de securite", V(0f, 0.003f, wallZ + 2.7f), V(p.Size.x - 0.2f, 0.006f, 0.08f), "jaune");
            p.Block("Barriere", V(0f, 0.5f, wallZ + 2.6f), V(p.Size.x, 1.0f, 0.1f));
            p.Box("Deflecteur", V(0f, 1.3f, d - 0.6f), V(p.Size.x, 2.4f, 0.1f), "metal_noir", V(-30f, 0f, 0f));
            p.Box("Butte", V(0f, 0.4f, d - 0.5f), V(p.Size.x, 0.8f, 0.8f), "caoutchouc", true);
            for (float z = wallZ + 3.5f; z < d - 1.0f; z += 2.0f)
            {
                p.Box("Chicane", V(0f, h - 0.45f, z), V(p.Size.x, 0.6f, 0.05f), "metal", V(45f, 0f, 0f));
            }

            Frame(p, V(hw, 2.45f, 4.2f), 90f, 0.6f, 0.8f, "affiche");
            Basics(p, V(-1.2f, 0f, 1.2f), true, false);
        }

        /// <summary>Un pan de la cloison vitrée (allège, vitre, imposte) de x0 à x1, à z.</summary>
        private static void SplitWall(InteriorPlan p, float z, float x0, float x1, float h)
        {
            float cx = (x0 + x1) * 0.5f, w = x1 - x0;
            p.Box("Allege", V(cx, 0.5f, z), V(w, 1.0f, 0.12f), "mur", true);
            p.Box("Vitre", V(cx, 1.7f, z), V(w, 1.4f, 0.03f), "verre", true);
            p.Box("Imposte", V(cx, (2.4f + h) * 0.5f, z), V(w, h - 2.4f, 0.12f), "mur", true);
            p.Box("Appui", V(cx, 1.01f, z), V(w, 0.04f, 0.2f), "moulure");
            for (float x = x0 + 1.2f; x < x1 - 0.2f; x += 1.2f) p.Box("Montant", V(x, 1.7f, z), V(0.05f, 1.4f, 0.06f), "metal_noir");
        }

        // ================================================================== salle de boxe

        private static void Gym(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            Counter(p, V(-hw + 1.6f, 0f, 2.6f), -90f, 2.4f, CounterStyle.Shop, "noir", "stratifie_fonce");
            WallShelf(p, V(-hw + 0.17f, 0f, 2.6f), -90f, 2.8f, 4, "complements", "metal_noir", 0.34f, 0.36f, 0.6f);
            WaterCooler(p, V(-hw + 0.25f, 0f, 5.0f), -90f);

            BoxingRing(p, V(2.4f, 0f, 8.4f), 2.6f);
            for (int i = 0; i < 4; i++) PunchingBag(p, V(-hw + 1.4f, 0f, 6.4f + i * 1.8f), h, i % 2 == 0 ? "cuir_rouge" : "cuir_noir");
            Mat(p, V(-hw + 1.4f, 0f, 9.1f), 0f, 2.4f, 7.6f, "bleu");

            // La poire de vitesse, contre le mur du fond.
            p.Box("Plateau de poire", V(-1.0f, 2.1f, d - 0.35f), V(0.8f, 0.06f, 0.7f), "bois_fonce");
            p.Box("Console", V(-1.0f, 1.8f, d - 0.04f), V(0.1f, 0.7f, 0.08f), "metal_noir");
            p.Sphere("Poire", V(-1.0f, 1.82f, d - 0.45f), V(0.2f, 0.28f, 0.2f), "cuir_rouge");

            Lockers(p, V(-4.2f, 0f, d - 0.23f), 0f, 8, true);
            p.Box("Chrono de round", V(-4.2f, 2.6f, d - 0.06f), V(0.9f, 0.35f, 0.1f), "plastique_noir");
            p.Box("Chiffres", V(-4.2f, 2.6f, d - 0.115f), V(0.7f, 0.22f, 0.006f), "neon_rouge");

            // Le coin musculation, à droite près de l'entrée, devant un mur de miroirs.
            DumbbellRack(p, V(hw - 0.3f, 0f, 3.0f), 90f, 2.4f);
            BenchPress(p, V(4.4f, 0f, 2.6f), 90f);
            Mat(p, V(4.6f, 0f, 3.0f), 0f, 3.6f, 4.0f, "caoutchouc");
            p.Box("Miroir", V(hw - 0.015f, 1.35f, 5.4f), V(0.01f, 1.9f, 3.2f), "miroir");
            for (int i = 0; i < 4; i++)
            {
                p.Box("Patere", V(-hw + 0.04f, 1.9f, 5.8f + i * 0.3f), V(0.06f, 0.02f, 0.02f), "metal");
                p.Cylinder("Corde a sauter", V(-hw + 0.08f, 1.5f, 5.8f + i * 0.3f), 0.008f, 0.8f, Pick(2) == 0 ? "noir" : "rouge");
            }

            for (int i = 0; i < 3; i++) Frame(p, V(hw, 2.6f, 7.0f + i * 1.6f), 90f, 0.7f, 1.0f, "affiche");
            Frame(p, V(-hw, 2.8f, 9.0f), -90f, 1.2f, 0.8f, "affiche");
            p.Box("Ventilateur", V(-hw + 0.6f, 1.0f, d - 2.0f), V(0.06f, 2.0f, 0.06f), "metal_noir");
            p.Cylinder("Grille", V(-hw + 0.6f, 1.8f, d - 2.0f), 0.32f, 0.12f, "metal", V(90f, 40f, 0f));
            Basics(p, V(-1.6f, 0f, 1.2f), true, false);
        }

        // ================================================================== médecin

        private static void Clinic(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            Counter(p, V(hw - 1.6f, 0f, 2.8f), 90f, 2.2f, CounterStyle.Shop, "blanc", "stratifie");
            p.Push(V(hw - 1.6f, 0f, 2.8f), 90f);
            Monitor(p, V(-0.5f, 1.02f, 0.12f), 180f, 0.45f);
            p.Pop();
            FilingCabinet(p, V(hw - 0.31f, 0f, 4.4f), 90f, 4);
            FilingCabinet(p, V(hw - 0.31f, 0f, 4.9f), 90f, 4);

            // La salle d'attente : sièges, table basse, fontaine, plante.
            WaitingChairs(p, V(-hw + 0.3f, 0f, 2.6f), -90f, 5, "bleu");
            WaitingChairs(p, V(-2.4f, 0f, 0.45f), 180f, 3, "bleu");
            CoffeeTable(p, V(-3.3f, 0f, 2.6f), 90f, 0.9f, 0.5f, "bois_clair");
            WaterCooler(p, V(-hw + 0.25f, 0f, 4.9f), -90f);
            Plant(p, V(hw - 0.4f, 0f, 0.45f), 2);

            // La cloison du cabinet d'examen, son rideau à moitié tiré.
            const float pz = 5.6f;
            p.Box("Cloison", V(-2.75f, 1.1f, pz), V(3.5f, 2.2f, 0.08f), "blanc", true);
            p.Cylinder("Tringle", V(0.2f, 2.15f, pz), 0.012f, 2.0f, "chrome", V(0f, 0f, 90f));
            for (int i = 0; i < 8; i++)
            {
                p.Box("Pli", V(-0.9f + i * 0.08f, 1.15f, pz + (i % 2) * 0.03f), V(0.09f, 1.95f, 0.02f), "chromo", V(0f, i % 2 == 0 ? 20f : -20f, 0f));
            }

            ExamTable(p, V(-2.6f, 0f, 7.6f), 90f);
            p.Cylinder("Tabouret", V(-1.3f, 0.25f, 7.0f), 0.18f, 0.06f, "cuir_noir");
            p.Cylinder("Pied", V(-1.3f, 0.12f, 7.0f), 0.02f, 0.24f, "chrome");
            MedicalCabinet(p, V(-0.4f, 0f, d), 0f);
            Sink(p, V(1.0f, 0f, d), 0f);
            Workstation(p, V(3.0f, 0f, 7.4f), 90f, 1.4f, "blanc", "stratifie", "cuir_noir");

            // L'échelle d'acuité, l'affiche anatomique, le tensiomètre au mur.
            p.Push(V(-hw, 1.5f, 6.8f), -90f);
            p.Box("Echelle d'acuite", V(0f, 0f, -0.01f), V(0.4f, 0.7f, 0.01f), "blanc");
            for (int i = 0; i < 7; i++) p.Box("Lettres", V(0f, 0.28f - i * 0.085f, -0.017f), V(0.3f - i * 0.04f, 0.05f - i * 0.006f, 0.002f), "noir");
            p.Pop();
            Frame(p, V(-hw, 1.6f, 8.2f), -90f, 0.5f, 0.75f, "affiche");
            p.Box("Tensiometre", V(-0.9f, 1.4f, pz + 0.05f), V(0.18f, 0.24f, 0.06f), "gris");
            for (int i = 0; i < 2; i++) Frame(p, V(-hw, 2.1f, 1.8f + i * 1.4f), -90f, 0.5f, 0.4f, "photo");
            TrashBin(p, V(0.4f, 0f, 6.1f), true);
            Basics(p, V(1.5f, 0f, 4.6f), false, false);
            Clock(p, V(hw, 2.3f, 6.0f), 90f);
        }

        // ================================================================== concession

        private static void Dealership(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            Counter(p, V(4.6f, 0f, d - 2.2f), 0f, 2.6f, CounterStyle.Shop, "blanc", "marbre");
            WallShelf(p, V(4.6f, 0f, d - 0.17f), 0f, 3.0f, 3, "livres", "blanc", 0.3f, 0.4f, 0.7f);
            Frame(p, V(-1.2f, 2.3f, d), 0f, 3.2f, 1.4f, "photo");

            // Les voitures : l'une sur son plateau tournant, l'autre au milieu, prix sur pied.
            Color[] paints = { new Color(0.6f, 0.04f, 0.04f), new Color(0.05f, 0.1f, 0.3f), new Color(0.85f, 0.85f, 0.86f), new Color(0.04f, 0.04f, 0.045f) };
            ShowCar(p, V(-3.4f, 0f, 6.4f), 30f, paints[Pick(2)], true);
            ShowCar(p, V(2.8f, 0f, 4.6f), -20f, paints[2 + Pick(2)], false);
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = i == 0 ? V(-0.6f, 0f, 4.8f) : V(4.4f, 0f, 2.2f);
                p.Cylinder("Pied", at + V(0f, 0.55f, 0f), 0.02f, 1.1f, "chrome");
                p.Box("Prix", at + V(0f, 1.2f, 0f), V(0.4f, 0.3f, 0.02f), "blanc", V(-10f, 0f, 0f));
                p.Box("Prix (chiffre)", at + V(0f, 1.18f, -0.012f), V(0.3f, 0.1f, 0.004f), "rouge", V(-10f, 0f, 0f));
            }

            // Le bureau du vendeur le long du mur de droite, et ses chaises visiteurs.
            Workstation(p, V(hw - 1.4f, 0f, 6.6f), 90f, 1.6f, "blanc", "stratifie", "cuir_noir");
            Chair(p, V(hw - 2.5f, 0f, 6.25f), -90f, "cuir_noir", "chrome", 1);
            Chair(p, V(hw - 2.5f, 0f, 6.95f), -90f, "cuir_noir", "chrome", 1);

            // L'attente : canapé, table basse, machine à café ; plantes, pneus en pile.
            Sofa(p, V(-hw + 0.44f, 0f, d - 2.0f), -90f, 2.4f, "cuir_noir");
            CoffeeTable(p, V(-hw + 1.5f, 0f, d - 2.0f), 90f, 1.0f, 0.55f, "verre");
            p.Box("Machine a cafe", V(-hw + 0.3f, 0.55f, d - 0.5f), V(0.45f, 1.1f, 0.45f), "noir", true);
            for (int i = 0; i < 4; i++) p.Cylinder("Pneu", V(-0.4f, 0.11f + i * 0.22f, d - 0.6f), 0.33f, 0.22f, "caoutchouc");
            Plant(p, V(-hw + 0.5f, 0f, 0.6f), 2);
            Plant(p, V(hw - 0.5f, 0f, 0.6f), 2);
            Plant(p, V(hw - 0.5f, 0f, d - 0.6f), 1);
            for (int i = 0; i < 2; i++) Frame(p, V(-hw, 2.4f, 3.0f + i * 2.0f), -90f, 1.4f, 0.9f, "photo");
            Basics(p, V(1.6f, 0f, 1.0f), true, true);
            p.Light(V(-3.4f, h - 0.4f, 6.4f), new Color(1f, 0.96f, 0.9f), 1.4f, 7f, true);
        }

        // ================================================================== motel

        private static void MotelLobby(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            float cz = d - 1.6f;
            Counter(p, V(0.6f, 0f, cz), 0f, 2.6f, CounterStyle.Shop, "bois_fonce", "bois", -0.3f);
            p.Push(V(0.6f, 0f, cz), 0f);
            p.Cylinder("Sonnette", V(-0.9f, 1.04f, -0.25f), 0.04f, 0.04f, "chrome");
            p.Sphere("Sonnette (dome)", V(-0.9f, 1.07f, -0.25f), V(0.08f, 0.05f, 0.08f), "chrome");
            p.Box("Registre", V(-0.4f, 1.04f, -0.22f), V(0.4f, 0.03f, 0.3f), "cuir_rouge", V(0f, 10f, 0f));
            p.Pop();

            // Le tableau des clés derrière la réception.
            Vector3 board = V(0.6f, 1.7f, d);
            p.Box("Tableau des cles", board + V(0f, 0f, -0.015f), V(1.2f, 0.8f, 0.03f), "bois");
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 6; c++)
                {
                    Vector3 o = board + V(-0.5f + c * 0.2f, 0.25f - r * 0.25f, -0.04f);
                    p.Box("Crochet", o + V(0f, 0.04f, 0.01f), V(0.01f, 0.01f, 0.03f), "laiton");
                    if (Pick(4) == 0) continue;
                    p.Box("Cle", o, V(0.012f, 0.05f, 0.004f), "laiton");
                    p.Box("Porte-cle", o + V(0f, -0.07f, 0f), V(0.04f, 0.08f, 0.008f), "rouge");
                }
            }

            Television(p, V(2.4f, 0.9f, d - 0.3f), 0f, true);
            p.Box("Meuble tele", V(2.4f, 0.45f, d - 0.3f), V(0.8f, 0.9f, 0.5f), "bois_fonce", true);

            // Le petit salon : canapé, table basse, lampadaire, présentoir à dépliants.
            Rug(p, V(-2.1f, 0f, 2.6f), 0f, 2.0f, 2.6f, "tissu_rouge", "bois_clair");
            Sofa(p, V(-hw + 0.44f, 0f, 2.6f), -90f, 2.0f, "tissu");
            CoffeeTable(p, V(-2.1f, 0f, 2.6f), 90f, 0.9f, 0.5f, "bois_fonce");
            FloorLamp(p, V(-hw + 0.35f, 0f, 4.1f));
            p.Box("Presentoir a depliants", V(-hw + 0.12f, 1.2f, 5.0f), V(0.2f, 0.9f, 0.6f), "bois");
            for (int i = 0; i < 6; i++)
            {
                p.Box("Depliant", V(-hw + 0.2f, 0.95f + (i / 2) * 0.28f, 4.85f + (i % 2) * 0.3f), V(0.02f, 0.2f, 0.1f), Product(), V(0f, 0f, -12f));
            }

            VendingMachine(p, V(hw - 0.42f, 0f, 2.2f), 90f, false);
            CeilingFan(p, V(-0.6f, h, 2.8f));
            Plant(p, V(hw - 0.4f, 0f, 0.45f), 2);
            Frame(p, V(-hw, 2.0f, 2.6f), -90f, 0.9f, 0.6f, "photo");
            Frame(p, V(hw, 2.0f, 4.0f), 90f, 0.6f, 0.45f, "photo");
            Clock(p, V(-hw, 2.3f, 4.6f), -90f);
            Basics(p, V(1.6f, 0f, 1.0f), false, false);
        }

        // ================================================================== commissariat

        private static void Police(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            const float cz = 5.6f;
            Counter(p, V(0f, 0f, cz), 0f, 4.4f, CounterStyle.Window, "bois_fonce", "stratifie", 0.8f);
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Muret", V(k * (2.2f + (hw - 2.2f) * 0.5f), 0.55f, cz), V(hw - 2.2f, 1.1f, 0.2f), "bois_fonce", true);
            }

            // Derrière le guichet : deux bureaux, des classeurs, le drapeau, le tableau des avis de recherche.
            Workstation(p, V(-2.6f, 0f, 7.3f), 0f, 1.6f, "gris", "stratifie", "cuir_noir");
            Workstation(p, V(2.6f, 0f, 7.3f), 0f, 1.6f, "gris", "stratifie", "cuir_noir");
            for (int i = 0; i < 3; i++) FilingCabinet(p, V(-0.6f + i * 0.5f, 0f, d - 0.31f), 0f, 4);
            Flag(p, V(hw - 0.4f, 0f, d - 0.4f), "bleu");
            CorkBoard(p, V(hw, 1.6f, 6.6f), 90f, 1.4f, 0.9f);
            ServiceDoor(p, V(-hw, 0f, 7.6f), -90f, "metal_noir", "blanc");
            for (float z = 7.2f; z < 8.05f; z += 0.1f) p.Cylinder("Barreau", V(-hw + 0.06f, 1.4f, z), 0.01f, 0.7f, "chrome");

            // Le hall : portique, banc d'attente, avis de recherche, fontaine, plante.
            MetalDetector(p, V(0f, 0f, 3.0f), 0f);
            WaitingChairs(p, V(-hw + 0.3f, 0f, 2.8f), -90f, 4, "bleu");
            CorkBoard(p, V(-hw, 1.5f, 4.8f), -90f, 1.2f, 0.8f);
            WaterCooler(p, V(hw - 0.25f, 0f, 3.6f), 90f);
            Plant(p, V(hw - 0.4f, 0f, 0.45f), 1);
            for (int i = 0; i < 2; i++) Frame(p, V(hw, 2.2f, 1.4f + i * 1.0f), 90f, 0.5f, 0.6f, "diplome");
            Basics(p, V(-1.6f, 0f, 4.6f), true, true);
        }

        // ================================================================== bureaux

        /// <summary>Le cabinet d'avocat, l'agence immobilière (et tout bureau d'accueil).</summary>
        private static void Office(InteriorPlan p, string kind)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            bool lawyer = kind == "Avocat";
            string wood = lawyer ? "bois_fonce" : "blanc";
            float cz = d - 2.4f;
            Counter(p, V(0f, 0f, cz), 0f, 2.0f, CounterStyle.Desk, wood, lawyer ? "cuir_noir" : "stratifie");
            if (lawyer)
            {
                p.Push(V(0f, 0f, cz), 0f);
                DeskLamp(p, V(0.75f, 0.76f, -0.1f), 180f, true);
                p.Pop();
            }

            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 c = V(k * 0.5f, 0f, cz - 0.95f);
                Chair(p, c, 180f + k * 10f, lawyer ? "cuir_brun" : "tissu", lawyer ? "bois_fonce" : "chrome", lawyer ? 2 : 1);
            }

            Rug(p, V(0f, 0f, cz - 0.3f), 0f, 3.0f, 2.4f, lawyer ? "tissu_rouge" : "tissu", lawyer ? "laiton" : "noir");

            // Le mur du fond : deux bibliothèques et les diplômes (ou les annonces) entre elles.
            Bookcase(p, V(-2.6f, 0f, d - 0.16f), 0f, 1.6f, 2.3f, wood, lawyer ? "livres" : "colis");
            Bookcase(p, V(2.6f, 0f, d - 0.16f), 0f, 1.6f, 2.3f, wood, "livres");
            for (int i = 0; i < 3; i++) Frame(p, V(-0.6f + i * 0.6f, 1.75f, d), 0f, 0.4f, 0.3f, lawyer ? "diplome" : "photo");

            if (lawyer)
            {
                // La balance de la justice sur la bibliothèque, le chesterfield près de la vitrine.
                Vector3 s = V(2.6f, 2.42f, d - 0.16f);
                p.Cylinder("Socle", s + V(0f, 0.02f, 0f), 0.06f, 0.04f, "laiton");
                p.Cylinder("Fut", s + V(0f, 0.17f, 0f), 0.008f, 0.3f, "laiton");
                p.Box("Fleau", s + V(0f, 0.31f, 0f), V(0.3f, 0.008f, 0.008f), "laiton");
                p.Cylinder("Plateau", s + V(-0.14f, 0.2f, 0f), 0.05f, 0.01f, "laiton");
                p.Cylinder("Plateau", s + V(0.14f, 0.2f, 0f), 0.05f, 0.01f, "laiton");
                Sofa(p, V(-2.4f, 0f, 1.25f), 180f, 2.0f, "cuir_brun", true);
                CoffeeTable(p, V(-2.4f, 0f, 2.35f), 0f, 0.9f, 0.5f, "bois_fonce");
                FilingCabinet(p, V(hw - 0.31f, 0f, 3.2f), 90f, 4);
                Plant(p, V(hw - 0.4f, 0f, 0.5f), 2);
            }
            else
            {
                // Les annonces de maisons en vitrine sur les murs, un second bureau, la fontaine.
                for (int k = -1; k <= 1; k += 2)
                {
                    for (int r = 0; r < 2; r++)
                    {
                        for (int c = 0; c < 3; c++) Frame(p, V(k * hw, 1.35f + r * 0.6f, 1.6f + c * 0.8f), k * 90f, 0.5f, 0.4f, "photo");
                    }
                }

                Workstation(p, V(2.4f, 0f, 3.6f), 90f, 1.4f, "blanc", "stratifie", "tissu");
                WaterCooler(p, V(-hw + 0.25f, 0f, d - 1.0f), -90f);
                WaitingChairs(p, V(-2.2f, 0f, 0.45f), 180f, 3, "tissu");
                Plant(p, V(hw - 0.4f, 0f, 0.5f), 1);
            }

            CoatRack(p, V(-hw + 0.4f, 0f, 3.6f));
            Basics(p, V(1.4f, 0f, 1.4f), false, true);
        }
    }
}
