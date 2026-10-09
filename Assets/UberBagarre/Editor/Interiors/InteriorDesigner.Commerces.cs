using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les commerces : supérette (et cave), pharmacie, vêtements, barbier, tatoueur,
    /// quincaillerie, marché noir, laverie, poste, prêteur sur gages.
    ///
    /// Chaque plan garde libres l'entrée (un couloir de 2,4 m sur 2,2 m devant la porte) et
    /// le chemin jusqu'au comptoir ; les allées font au moins 1,1 m.
    /// </summary>
    public static partial class InteriorDesigner
    {
        // ================================================================== aides de placement

        /// <summary>Ce qu'on trouve partout : extincteur près de la porte, horloge, caméra, poubelle.</summary>
        private static void Basics(InteriorPlan p, Vector3 bin, bool camera, bool clockOverDoor)
        {
            float hw = p.Size.x * 0.5f, h = p.Size.y;
            Extinguisher(p, V(-0.85f, 0f, 0f), 180f);
            if (clockOverDoor && h >= 3.1f) Clock(p, V(0f, h - 0.32f, 0f), 180f);
            if (camera)
            {
                Vector3 at = V(hw - 0.12f, h - 0.12f, 0.12f);
                SecurityCamera(p, at, Facing(at, V(-hw * 0.3f, 0f, p.Size.z * 0.6f)));
            }

            TrashBin(p, bin);
        }

        /// <summary>Les vitrines de la façade : de |x| = x0 à |x| = x1 (vide si la pièce n'en a pas).</summary>
        private static void WindowSpan(InteriorPlan p, out float x0, out float x1)
        {
            float side = (p.Size.x - 1.2f) * 0.5f;
            x0 = 1.0f;
            x1 = 1.0f + Mathf.Min(side - 0.8f, 3.2f);
        }

        /// <summary>Un tonneau cerclé, couché ou debout.</summary>
        private static void Barrel(InteriorPlan p, Vector3 at, string wood, bool lying)
        {
            Vector3 euler = lying ? V(90f, 0f, 0f) : Vector3.zero;
            Vector3 center = at + V(0f, lying ? 0.32f : 0.43f, 0f);
            p.Cylinder("Tonneau", center, 0.3f, 0.86f, wood, euler, true);
            for (int i = -1; i <= 1; i++)
            {
                Vector3 o = lying ? V(0f, 0f, i * 0.32f) : V(0f, i * 0.32f, 0f);
                p.Cylinder("Cercle", center + o, 0.306f, 0.04f, "metal_noir", euler);
            }
        }

        /// <summary>Un grillage (poteaux, maille en losanges, lisse haute) de <paramref name="from"/> à <paramref name="to"/>.</summary>
        private static void ChainLink(InteriorPlan p, Vector3 from, Vector3 to, float height)
        {
            Vector3 d = to - from;
            float length = new Vector2(d.x, d.z).magnitude;
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg - 90f;
            p.Push((from + to) * 0.5f, yaw);
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.5f) + 1);
            for (int i = 0; i < posts; i++)
            {
                p.Cylinder("Poteau", V(-length * 0.5f + length * i / (posts - 1), height * 0.5f, 0f), 0.025f, height, "metal");
            }

            p.Cylinder("Lisse", V(0f, height, 0f), 0.018f, length, "metal", V(0f, 0f, 90f));
            for (float x = -length * 0.5f - height; x < length * 0.5f; x += 0.14f)
            {
                // Les fils en diagonale, coupés au rectangle du grillage.
                float a = Mathf.Max(x, -length * 0.5f), b = Mathf.Min(x + height, length * 0.5f);
                if (b - a < 0.02f) continue;
                float span = (b - a) * 1.4142f;
                float cx = (a + b) * 0.5f, cy = (a - x + b - x) * 0.5f;
                p.Box("Fil", V(cx, cy, 0f), V(0.006f, span, 0.006f), "metal", V(0f, 0f, -45f));
                p.Box("Fil", V(cx, height - cy, 0f), V(0.006f, span, 0.006f), "metal", V(0f, 0f, 45f));
            }

            p.Block("Grillage", V(0f, height * 0.5f, 0f), V(length, height, 0.06f));
            p.Pop();
        }

        /// <summary>Un graffiti sur un mur (le dos en +Z) : des traits de bombe entremêlés.</summary>
        private static void Graffiti(InteriorPlan p, Vector3 at, float yaw, float w, float h)
        {
            p.Push(at, yaw);
            string[] colors = { "rose", "bleu", "jaune", "vert", "rouge", "blanc" };
            string main = colors[Pick(colors.Length)];
            for (int i = 0; i < 16; i++)
            {
                float len = Rand(0.2f, 0.7f);
                p.Box("Trait", V(Rand(-w * 0.5f, w * 0.5f), Rand(-h * 0.5f, h * 0.5f), -0.004f - i * 0.0004f), V(len, Rand(0.04f, 0.1f), 0.002f),
                    i % 3 == 0 ? colors[Pick(colors.Length)] : main, V(0f, 0f, Rand(-60f, 60f)));
            }

            p.Pop();
        }

        // ================================================================== supérette, cave

        private static void Grocery(InteriorPlan p, bool cave)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            string goods = cave ? "cave" : "epicerie";
            bool station = Named("station");

            // Le comptoir près de la porte, à droite ; derrière le vendeur, les cigarettes (ou les alcools forts).
            Counter(p, V(hw - 1.5f, 0f, 2.7f), 90f, 2.4f, CounterStyle.Shop, cave ? "bois" : "stratifie", cave ? "bois_fonce" : "stratifie_fonce");
            if (cave) WallShelf(p, V(hw - 0.17f, 0f, 2.7f), 90f, 2.6f, 4, "bar", "bois_fonce", 0.32f, 0.38f, 0.5f);
            else WallShelf(p, V(hw - 0.15f, 0f, 2.7f), 90f, 2.6f, 6, "tabac", "bois_fonce", 0.28f, 0.16f, 1.0f);
            WallTV(p, V(hw, 2.6f, 2.7f), 90f, 0.9f);

            // Deux gondoles vers le fond, leurs têtes de gondole « promo » tournées vers l'entrée.
            float wall = -hw + ShelfDepth + 0.05f;
            float g1 = wall + 1.15f + ShelfDepth + 0.04f;
            float g2 = g1 + ShelfDepth * 2f + 0.08f + 1.15f;
            const float z0 = 3.0f;
            float z1 = d - 2.0f;
            string[] aisle = { "bleu", "rouge" };
            string[] labels = cave ? new[] { "VINS", "ALCOOLS" } : new[] { "EPICERIE", "MAISON" };
            float[] gx = { g1, g2 };
            for (int i = 0; i < 2; i++)
            {
                Gondola(p, V(gx[i], 0f, (z0 + z1) * 0.5f), 90f, z1 - z0, 4, goods, true);
                Gondola(p, V(gx[i], 0f, z0 - 0.25f), 0f, 0.9f, 4, goods, false);
                p.Box("Affiche promo", V(gx[i], 1.8f, z0 - 0.04f), V(0.8f, 0.28f, 0.02f), "rouge");
                p.Neon("PROMO", V(gx[i], 1.755f, z0 - 0.055f), 0f, 0.09f, "blanc");
                HangingSign(p, V(gx[i], 2.45f, z0 + 1.2f), 0f, 1.3f, aisle[i], h, labels[i]);
            }

            // Les murs : rayonnage à gauche (tout du long), à droite au-delà du comptoir, frigos au fond.
            Gondola(p, V(-hw + 0.235f, 0f, (1.4f + d - 0.9f) * 0.5f), -90f, d - 2.3f, 5, goods, false);
            Gondola(p, V(hw - 0.235f, 0f, (4.4f + d - 1.1f) * 0.5f), 90f, d - 5.5f, 5, goods, false);
            const int doors = 6;
            Fridges(p, V(-hw + doors * 0.38f, 0f, d - 0.39f), 0f, doors, "boissons");

            // Le coin réserve, au fond à droite.
            ServiceDoor(p, V(2.6f, 0f, d), 0f, "metal", "blanc");
            for (int i = 0; i < 4; i++)
            {
                Item(p, "carton", V(1.1f + Rand(-0.05f, 0.05f), i * 0.3f, d - 0.5f), 0.45f, 0.3f, 0.4f, "carton", "blanc");
            }

            // Au milieu : le congélateur à glaces (la cave : deux tonneaux).
            if (cave)
            {
                Barrel(p, V(1.6f, 0f, 5.0f), "bois", false);
                Barrel(p, V(1.6f, 0f, 6.1f), "bois", false);
                for (int i = 0; i < 3; i++) Item(p, "vin", V(1.5f + i * 0.09f, 0.87f, 5.0f), 0.078f, 0.31f, 0.078f, Product(), Product());
            }
            else
            {
                ChestFreezer(p, V(1.6f, 0f, 5.6f), 90f, 1.6f);
            }

            // Devant la vitrine de gauche : une palette de packs d'eau (de caisses de bière), et sa pancarte.
            Pallet(p, V(-2.6f, 0f, 1.3f), 0f);
            for (int layer = 0; layer < 3; layer++)
            {
                for (int i = 0; i < 6; i++)
                {
                    Vector3 at = V(-2.92f + (i % 3) * 0.32f, 0.14f + layer * 0.26f, 1.0f + (i / 3) * 0.6f);
                    if (cave) Crate(p, at, Rand(-3f, 3f), V(0.3f, 0.25f, 0.4f));
                    else p.Box("Pack d'eau", at + V(0f, 0.125f, 0f), V(0.3f, 0.25f, 0.4f), "chromo");
                }
            }

            p.Cylinder("Pique", V(-2.1f, 1.2f, 1.9f), 0.01f, 0.6f, "metal");
            p.Box("Pancarte", V(-2.1f, 1.55f, 1.9f), V(0.5f, 0.32f, 0.02f), "jaune");
            p.Box("Pancarte (prix)", V(-2.1f, 1.55f, 1.888f), V(0.3f, 0.12f, 0.004f), "rouge");

            // Devant la vitrine de droite : le coin café de la station, ou le présentoir à journaux.
            if (station)
            {
                p.Box("Coin cafe", V(2.6f, 0.45f, 0.45f), V(1.6f, 0.9f, 0.55f), "stratifie", true);
                p.Box("Machine a cafe", V(2.2f, 1.15f, 0.5f), V(0.45f, 0.5f, 0.4f), "metal_noir");
                p.Box("Ecran", V(2.2f, 1.25f, 0.7f), V(0.25f, 0.15f, 0.004f), "ecran", V(0f, 180f, 0f));
                for (int i = 0; i < 4; i++) p.Cylinder("Gobelets", V(2.7f + i * 0.12f, 1.05f, 0.4f), 0.04f, 0.3f, "blanc");
                p.Box("Couvercles", V(3.2f, 0.95f, 0.5f), V(0.2f, 0.1f, 0.2f), "plastique_noir");
            }
            else
            {
                p.Box("Presentoir a journaux", V(2.6f, 0.5f, 0.35f), V(1.2f, 1.0f, 0.35f), "metal_noir", true);
                for (int i = 0; i < 6; i++)
                {
                    p.Box("Journal", V(2.15f + (i % 3) * 0.38f, 0.75f - (i / 3) * 0.4f, 0.55f), V(0.32f, 0.36f, 0.02f), Pick(2) == 0 ? "papier" : Product(),
                        V(-15f, 0f, 0f));
                }
            }

            for (int i = 0; i < 2; i++) Frame(p, V(-hw, 2.55f, 3.0f + i * 3.0f), -90f, 0.7f, 0.5f);
            DomeMirror(p, V(hw - 0.4f, h - 0.15f, d - 1.5f));
            Basics(p, V(2.2f, 0f, 4.3f), true, true);
        }

        // ================================================================== pharmacie

        private static void Pharmacy(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            float cz = d - 1.8f;
            Counter(p, V(0f, 0f, cz), 0f, 3.6f, CounterStyle.Shop, "blanc", "marbre", -0.7f);
            p.Push(V(0f, 0f, cz), 0f);
            Monitor(p, V(1.0f, 1.02f, 0.12f), 180f, 0.45f);
            p.Pop();
            WallShelf(p, V(0f, 0f, d - 0.17f), 0f, 6.4f, 6, "pharmacie", "blanc", 0.3f, 0.3f, 0.45f);

            // La croix verte, au mur de gauche.
            p.Push(V(-hw + 0.03f, 2.35f, 6.4f), -90f);
            p.Box("Croix verte", V(0f, 0f, -0.03f), V(0.6f, 0.2f, 0.05f), "neon_vert");
            p.Box("Croix verte", V(0f, 0f, -0.03f), V(0.2f, 0.6f, 0.05f), "neon_vert");
            p.Pop();

            // Rayonnages muraux, deux gondoles basses, leurs têtes de gondole.
            Gondola(p, V(-hw + 0.235f, 0f, 4.2f), -90f, 4.8f, 5, "cosmetique", false);
            Gondola(p, V(hw - 0.235f, 0f, 4.2f), 90f, 4.8f, 5, "pharmacie", false);
            for (int k = -1; k <= 1; k += 2)
            {
                Gondola(p, V(k * 2.0f, 0f, 4.1f), 90f, 3.0f, 4, k < 0 ? "pharmacie" : "cosmetique", true);
                Gondola(p, V(k * 2.0f, 0f, 2.35f), 0f, 0.9f, 4, "cosmetique", false);
                HangingSign(p, V(k * 2.0f, 2.45f, 3.4f), 0f, 1.2f, "vert", h, k < 0 ? "SANTE" : "BEAUTE");
            }

            // La file d'attente, la borne de tension, les sièges, une balance.
            Stanchions(p, V(-0.9f, 0f, 5.3f), V(-0.9f, 0f, 6.2f), false);
            Stanchions(p, V(0.9f, 0f, 5.3f), V(0.9f, 0f, 6.2f), false);
            p.Box("Marquage au sol", V(0f, 0.002f, 5.1f), V(1.2f, 0.004f, 0.06f), "jaune");

            p.Push(V(2.8f, 0f, 0.7f), 180f);
            p.Box("Borne de tension", V(0f, 0.55f, 0.15f), V(0.6f, 1.1f, 0.35f), "blanc", true);
            p.Box("Ecran", V(0f, 1.2f, 0.05f), V(0.4f, 0.3f, 0.04f), "ecran", V(-15f, 0f, 0f));
            p.Cylinder("Brassard", V(0.2f, 0.85f, -0.1f), 0.07f, 0.2f, "bleu", V(90f, 0f, 0f));
            p.Box("Siege", V(0f, 0.45f, -0.4f), V(0.45f, 0.06f, 0.42f), "bleu");
            p.Box("Pied", V(0f, 0.22f, -0.4f), V(0.06f, 0.44f, 0.06f), "metal");
            p.Block("Siege", V(0f, 0.25f, -0.4f), V(0.45f, 0.5f, 0.42f));
            p.Pop();

            WaitingChairs(p, V(-2.6f, 0f, 0.45f), 180f, 3, "bleu");
            p.Box("Balance", V(-1.3f, 0.03f, 2.6f), V(0.35f, 0.06f, 0.35f), "blanc");
            p.Box("Affichage", V(-1.3f, 0.065f, 2.48f), V(0.12f, 0.004f, 0.06f), "ecran_vert");
            Plant(p, V(-hw + 0.4f, 0f, 0.45f), 1);
            Plant(p, V(hw - 0.4f, 0f, 0.45f), 0);
            for (int k = -1; k <= 1; k += 2)
            {
                Frame(p, V(k * hw, 2.5f, 3.0f), k * 90f, 0.5f, 0.6f, "affiche");
                Frame(p, V(k * hw, 2.5f, 5.4f), k * 90f, 0.5f, 0.6f, "affiche");
            }

            Basics(p, V(2.4f, 0f, 6.3f), true, true);
        }

        // ================================================================== vêtements

        private static void Clothes(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            bool skate = Named("shred", "skate");
            bool chic = Named("elegance", "boutique");
            float cz = d - 1.7f;
            Counter(p, V(-2.6f, 0f, cz), 0f, 2.4f, CounterStyle.Shop, chic ? "bois_fonce" : "blanc", chic ? "marbre" : "bois_clair");
            p.Push(V(-2.6f, 0f, cz), 0f);
            for (int i = 0; i < 3; i++) p.Box("Sacs", V(-0.8f, 0.15f + i * 0.02f, 0.2f), V(0.36f, 0.02f, 0.14f), chic ? "noir" : "papier");
            p.Pop();
            WallShelf(p, V(-2.6f, 0f, d - 0.17f), 0f, 3.4f, 4, "vetements", chic ? "bois_fonce" : "blanc", 0.34f, 0.4f, 0.3f);

            // Les cabines d'essayage au fond à droite, le miroir et le pouf à côté.
            string curtain = chic ? "tissu_rouge" : skate ? "noir" : "tissu";
            FittingRoom(p, V(1.5f, 0f, d - 0.62f), 0f, curtain);
            FittingRoom(p, V(2.7f, 0f, d - 0.62f), 0f, curtain);
            p.Box("Miroir", V(hw - 0.02f, 1.2f, 7.0f), V(0.02f, 1.8f, 0.7f), "miroir");
            p.Box("Cadre du miroir", V(hw - 0.012f, 1.2f, 7.0f), V(0.02f, 1.86f, 0.76f), chic ? "laiton" : "noir");
            p.Cylinder("Pouf", V(3.5f, 0.21f, 6.6f), 0.28f, 0.42f, chic ? "tissu_rouge" : "tissu", default(Vector3), true);

            // Les murs : un portant à gauche, chaussures (ou planches de skate) à droite.
            if (skate)
            {
                WallShelf(p, V(-hw + 0.17f, 0f, 4.2f), -90f, 3.4f, 5, "chaussures", "bois", 0.34f, 0.3f, 0.4f);
                for (int i = 0; i < 10; i++)
                {
                    p.Push(V(hw, 1.25f + (i % 2) * 0.95f, 2.4f + (i / 2) * 0.62f), 90f);
                    string deck = Product();
                    p.Box("Planche", V(0f, 0f, -0.04f), V(0.21f, 0.8f, 0.015f), deck, V(0f, 0f, Rand(-4f, 4f)));
                    p.Box("Motif", V(0f, 0f, -0.049f), V(0.15f, 0.4f, 0.003f), Product());
                    for (int k = -1; k <= 1; k += 2)
                    {
                        p.Box("Essieu", V(0f, k * 0.27f, -0.065f), V(0.16f, 0.03f, 0.03f), "metal");
                        p.Cylinder("Roue", V(-0.08f, k * 0.27f, -0.07f), 0.027f, 0.03f, "blanc", V(0f, 0f, 90f));
                        p.Cylinder("Roue", V(0.08f, k * 0.27f, -0.07f), 0.027f, 0.03f, "blanc", V(0f, 0f, 90f));
                    }

                    p.Box("Support", V(0f, 0.4f, -0.015f), V(0.06f, 0.03f, 0.03f), "metal_noir");
                    p.Pop();
                }
            }
            else
            {
                ClothesRack(p, V(-hw + 0.35f, 0f, 4.2f), 90f, 3.6f);
                WallShelf(p, V(hw - 0.17f, 0f, 4.0f), 90f, 3.0f, 5, "chaussures", chic ? "bois_fonce" : "blanc", 0.34f, 0.3f, 0.4f);
            }

            // Au centre : une table de pulls pliés, deux portants de chaque côté.
            Rug(p, V(0f, 0f, 4.2f), 0f, 3.2f, 2.4f, chic ? "tissu_rouge" : "tissu", chic ? "laiton" : "noir");
            FoldTable(p, V(0f, 0f, 4.0f), 0f, 1.4f, 0.8f);
            ClothesRack(p, V(-2.2f, 0f, 4.4f), 90f, 1.8f);
            ClothesRack(p, V(2.2f, 0f, 4.4f), 90f, 1.8f);

            // Les vitrines : une estrade et deux mannequins habillés de chaque côté.
            float x0, x1;
            WindowSpan(p, out x0, out x1);
            for (int k = -1; k <= 1; k += 2)
            {
                float cx = k * (x0 + x1) * 0.5f;
                p.Box("Estrade", V(cx, 0.075f, 0.55f), V(x1 - x0 - 0.2f, 0.15f, 0.9f), chic ? "marbre" : "bois_clair", true);
                if (skate && k > 0)
                {
                    p.Box("Rampe", V(cx, 0.35f, 0.55f), V(1.4f, 0.04f, 0.7f), "bois", V(-20f, 0f, 0f));
                    continue;
                }

                Mannequin(p, V(cx - 0.6f, 0.15f, 0.55f), Rand(-15f, 15f));
                Mannequin(p, V(cx + 0.6f, 0.15f, 0.55f), Rand(-15f, 15f));
            }

            Plant(p, V(-hw + 0.4f, 0f, d - 2.6f), chic ? 2 : 1);
            Frame(p, V(-hw, 2.35f, 3.2f), -90f, 0.6f, 0.8f, "affiche");
            Frame(p, V(-hw, 2.35f, 5.2f), -90f, 0.6f, 0.8f, "affiche");
            Frame(p, V(3.9f, 2.4f, d), 0f, 0.6f, 0.8f, "affiche");
            Basics(p, V(-1.0f, 0f, cz + 0.2f), true, true);
        }

        // ================================================================== barbier

        private static void Barber(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            string leather = Pick(2) == 0 ? "cuir_rouge" : "cuir_noir";

            // Trois postes le long du mur de gauche : miroir, tablette, fauteuil ; des cheveux par terre.
            for (int i = 0; i < 3; i++)
            {
                float z = 3.0f + i * 1.7f;
                BarberStation(p, V(-hw, 0f, z), -90f, leather);
                for (int k = 0; k < 7; k++)
                {
                    p.Sphere("Cheveux", V(-3.55f + Rand(-0.5f, 0.5f), 0.004f, z + Rand(-0.5f, 0.5f)), V(Rand(0.04f, 0.1f), 0.006f, Rand(0.03f, 0.08f)),
                        Pick(2) == 0 ? "bois_fonce" : "noir", V(0f, Rand(0f, 180f), 0f));
                }

                if (i < 2) Frame(p, V(-hw, 2.55f, z + 0.85f), -90f, 0.36f, 0.46f);
            }

            // L'accueil à droite près de l'entrée, les produits derrière.
            Counter(p, V(hw - 1.5f, 0f, 2.4f), 90f, 1.8f, CounterStyle.Shop, "bois_fonce", "marbre");
            WallShelf(p, V(hw - 0.16f, 0f, 2.4f), 90f, 2.0f, 4, "cosmetique", "bois_fonce", 0.3f, 0.3f, 0.7f);

            // L'attente : un chesterfield, la table basse et ses magazines, le portemanteau.
            Sofa(p, V(hw - 0.44f, 0f, 5.4f), 90f, 2.0f, "cuir_brun", true);
            CoffeeTable(p, V(2.75f, 0f, 5.4f), 90f, 1.0f, 0.55f, "bois_fonce");
            Frame(p, V(hw, 1.95f, 5.4f), 90f, 1.1f, 0.65f, "photo");
            CoatRack(p, V(hw - 0.45f, 0f, 7.3f));

            // Le bac à shampooing au fond, la télé.
            Sink(p, V(-1.2f, 0f, d), 0f);
            BarberChair(p, V(-1.2f, 0f, d - 1.25f), 0f, leather);
            WallTV(p, V(1.6f, 2.15f, d), 0f, 1.0f);

            BarberPole(p, V(-hw, 0f, 0.9f), -90f);
            Plant(p, V(hw - 0.4f, 0f, 0.45f), 1);
            Basics(p, V(-1.9f, 0f, 1.0f), false, true);
        }

        // ================================================================== tatoueur

        private static void Tattoo(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            Counter(p, V(hw - 1.6f, 0f, 2.6f), 90f, 1.8f, CounterStyle.Showcase, "bois_fonce", null, 0f, "bijoux");

            // Le mur de « flashs » : les modèles encadrés, derrière l'accueil.
            for (int r = 0; r < 2; r++)
            {
                for (int c = 0; c < 5; c++) Frame(p, V(hw, 1.55f + r * 0.62f, 1.5f + c * 0.5f), 90f, 0.36f, 0.48f, "affiche");
            }

            // Deux postes au fond, séparés par une cloison.
            TattooStation(p, V(-2.2f, 0f, 6.9f), 0f);
            TattooStation(p, V(1.8f, 0f, 6.9f), 0f);
            p.Box("Cloison", V(-0.2f, 1.0f, 7.5f), V(0.08f, 2.0f, 2.6f), "bois_fonce", true);
            Sink(p, V(3.6f, 0f, d), 0f);
            Bookcase(p, V(-hw + 0.16f, 0f, 8.0f), -90f, 1.2f, 2.0f, "bois_fonce", "livres");

            // L'attente : canapé de cuir, books de dessins sur la table basse.
            Sofa(p, V(-hw + 0.44f, 0f, 2.6f), -90f, 2.0f, "cuir_noir");
            CoffeeTable(p, V(-3.0f, 0f, 2.6f), 90f, 0.9f, 0.5f, "metal_noir");
            p.Box("Book", V(-3.0f, 0.47f, 2.4f), V(0.3f, 0.05f, 0.4f), "noir");
            p.Neon("TATTOO", V(-hw + 0.03f, 2.3f, 5.2f), -90f, 0.3f, "neon_rouge");
            for (int i = 0; i < 2; i++) Frame(p, V(-hw, 1.9f, 2.0f + i * 1.2f), -90f, 0.5f, 0.7f, "affiche");
            Plant(p, V(hw - 0.4f, 0f, 0.45f), 1);
            Plant(p, V(-hw + 0.4f, 0f, 0.45f), 0);
            Basics(p, V(-1.4f, 0f, 1.2f), false, true);
            TrashBin(p, V(0.6f, 0f, d - 0.4f), true);
        }

        // ================================================================== quincaillerie

        private static void Hardware(InteriorPlan p)
        {
            float d = p.Size.z, h = p.Size.y, hw = p.Size.x * 0.5f;
            Counter(p, V(hw - 1.5f, 0f, 2.7f), 90f, 2.4f, CounterStyle.Shop, "bois", "bois_clair");
            p.Push(V(hw - 1.5f, 0f, 2.7f), 90f);
            p.Box("Machine a cles", V(-0.75f, 1.12f, 0.1f), V(0.3f, 0.2f, 0.25f), "vert");
            p.Cylinder("Meule", V(-0.75f, 1.26f, 0.05f), 0.06f, 0.02f, "metal", V(0f, 0f, 90f));
            p.Box("Etau", V(-0.68f, 1.25f, 0.12f), V(0.08f, 0.05f, 0.06f), "chrome");
            p.Pop();

            // Derrière le vendeur : le tableau des clés à reproduire, sur son meuble bas.
            p.Box("Meuble bas", V(hw - 0.18f, 0.45f, 2.7f), V(0.35f, 0.9f, 2.6f), "bois", true);
            p.Box("Panneau a cles", V(hw - 0.02f, 1.7f, 2.7f), V(0.02f, 1.0f, 2.4f), "bois_clair");
            for (int r = 0; r < 4; r++)
            {
                for (int c = 0; c < 12; c++)
                {
                    Vector3 at = V(hw - 0.05f, 1.35f + r * 0.22f, 1.65f + c * 0.19f);
                    p.Box("Crochet", at + V(0.01f, 0.05f, 0f), V(0.04f, 0.006f, 0.006f), "metal");
                    p.Box("Cle", at, V(0.004f, 0.06f, 0.025f), Pick(3) == 0 ? "chrome" : "laiton");
                }
            }

            // Les gondoles, comme à la supérette, mais hautes.
            float g1 = -hw + ShelfDepth + 0.05f + 1.15f + ShelfDepth + 0.04f;
            float g2 = g1 + ShelfDepth * 2f + 0.08f + 1.15f;
            const float z0 = 3.0f;
            float z1 = d - 2.2f;
            Gondola(p, V(g1, 0f, (z0 + z1) * 0.5f), 90f, z1 - z0, 5, "quincaillerie", true);
            Gondola(p, V(g2, 0f, (z0 + z1) * 0.5f), 90f, z1 - z0, 5, "quincaillerie", true);
            HangingSign(p, V(g1, 2.55f, z0 + 1.0f), 0f, 1.4f, "orange", h, "OUTILLAGE");
            HangingSign(p, V(g2, 2.55f, z0 + 1.0f), 0f, 1.4f, "vert", h, "PEINTURE");

            // Le mur de gauche : bacs de visserie sur meuble bas, panneau perforé d'outils au-dessus.
            float wz0 = 1.8f, wz1 = d - 1.0f;
            p.Box("Meuble a bacs", V(-hw + 0.22f, 0.4f, (wz0 + wz1) * 0.5f), V(0.44f, 0.8f, wz1 - wz0), "bois", true);
            for (float z = wz0 + 0.2f; z < wz1 - 0.1f; z += 0.3f)
            {
                p.Box("Bac", V(-hw + 0.3f, 0.88f, z), V(0.3f, 0.16f, 0.26f), Pick(3) == 0 ? "rouge" : Pick(2) == 0 ? "bleu" : "jaune");
                p.Box("Visserie", V(-hw + 0.3f, 0.95f, z), V(0.26f, 0.02f, 0.22f), "metal");
            }

            p.Box("Panneau perfore", V(-hw + 0.02f, 1.75f, (wz0 + wz1) * 0.5f), V(0.02f, 1.3f, wz1 - wz0), "bois_clair");
            for (float z = wz0 + 0.25f; z < wz1 - 0.15f; z += 0.36f)
            {
                for (int r = 0; r < 3; r++) Tool(p, V(-hw, 1.35f + r * 0.42f, z + Rand(-0.04f, 0.04f)));
            }

            // Le fond : la peinture et sa machine à teinter, les échelles, le bois debout.
            WallShelf(p, V(-2.4f, 0f, d - 0.22f), 0f, 4.2f, 4, "quincaillerie", "metal_noir", 0.4f, 0.42f, 0.15f);
            p.Box("Machine a teinter", V(0.7f, 0.6f, d - 0.35f), V(0.6f, 1.2f, 0.6f), "blanc", true);
            p.Box("Ecran", V(0.7f, 1.05f, d - 0.652f), V(0.25f, 0.18f, 0.004f), "ecran");
            for (int i = 0; i < 6; i++) p.Cylinder("Buse", V(0.5f + (i % 3) * 0.2f, 1.25f + (i / 3) * 0.08f, d - 0.5f), 0.025f, 0.08f, Product());
            for (int i = 0; i < 2; i++) Ladder(p, V(1.7f + i * 0.6f, 0f, d - 0.25f), 2.4f);
            for (int i = 0; i < 8; i++)
            {
                p.Box("Planche", V(3.4f + i * 0.12f, 1.25f, d - 0.25f), V(0.09f, 2.5f, 0.04f), "bois_clair", V(-6f, Rand(-5f, 5f), 0f));
            }

            // Le mur de droite au-delà du comptoir, et devant : brouette, seaux, sacs de ciment.
            Gondola(p, V(hw - 0.235f, 0f, (4.4f + d - 1.2f) * 0.5f), 90f, d - 5.6f, 5, "quincaillerie", false);
            Wheelbarrow(p, V(2.0f, 0f, 0.8f), 25f);
            for (int i = 0; i < 5; i++) p.Cylinder("Seau", V(3.6f, 0.15f + i * 0.06f, 0.6f), 0.15f - i * 0.002f, 0.3f, "orange");
            Pallet(p, V(-2.6f, 0f, 1.3f), 0f);
            for (int i = 0; i < 10; i++)
            {
                p.Box("Sac de ciment", V(-2.85f + (i % 2) * 0.5f, 0.2f + (i / 2) * 0.12f, 1.3f), V(0.45f, 0.12f, 0.7f), i % 3 == 0 ? "papier" : "gris",
                    V(0f, Rand(-4f, 4f), 0f));
            }

            Basics(p, V(2.2f, 0f, 4.3f), true, true);
        }

        /// <summary>Un outil sur le panneau perforé du mur de gauche (marteau, clé, scie, tournevis, mètre).</summary>
        private static void Tool(InteriorPlan p, Vector3 wall)
        {
            p.Push(wall, -90f);
            p.Box("Crochet", V(0f, 0.12f, -0.04f), V(0.006f, 0.006f, 0.06f), "metal");
            switch (Pick(5))
            {
                case 0:
                    p.Box("Manche", V(0f, -0.02f, -0.05f), V(0.03f, 0.28f, 0.025f), "bois");
                    p.Box("Tete", V(0f, 0.13f, -0.05f), V(0.12f, 0.035f, 0.03f), "metal_noir");
                    break;
                case 1:
                    p.Box("Cle plate", V(0f, 0f, -0.04f), V(0.025f, 0.24f, 0.006f), "chrome", V(0f, 0f, Rand(-8f, 8f)));
                    p.Cylinder("Oeil", V(0f, 0.12f, -0.04f), 0.022f, 0.006f, "chrome", V(90f, 0f, 0f));
                    break;
                case 2:
                    p.Box("Lame", V(0.05f, -0.05f, -0.04f), V(0.12f, 0.36f, 0.004f), "metal");
                    p.Box("Poignee", V(0.03f, 0.15f, -0.04f), V(0.12f, 0.1f, 0.025f), "rouge");
                    break;
                case 3:
                    p.Cylinder("Manche", V(0f, 0.04f, -0.05f), 0.016f, 0.11f, Pick(2) == 0 ? "jaune" : "rouge");
                    p.Cylinder("Tige", V(0f, -0.07f, -0.05f), 0.004f, 0.12f, "chrome");
                    break;
                default:
                    p.Box("Metre", V(0f, 0.02f, -0.05f), V(0.08f, 0.08f, 0.04f), "jaune");
                    break;
            }

            p.Pop();
        }

        /// <summary>Une échelle appuyée contre le mur (le dos en +Z).</summary>
        private static void Ladder(InteriorPlan p, Vector3 at, float height)
        {
            p.Push(at, 0f);
            const float tilt = 10f;
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Montant", V(k * 0.2f, height * 0.5f, -0.1f), V(0.04f, height, 0.07f), "metal", V(-tilt, 0f, 0f));
            }

            for (float y = 0.3f; y < height - 0.1f; y += 0.3f)
            {
                float z = -0.1f - (height * 0.5f - y) * Mathf.Tan(tilt * Mathf.Deg2Rad);
                p.Box("Barreau", V(0f, y, z), V(0.4f, 0.03f, 0.04f), "metal");
            }

            p.Block("Echelle", V(0f, height * 0.5f, -0.15f), V(0.45f, height, 0.3f));
            p.Pop();
        }

        private static void Wheelbarrow(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Caisse", V(0f, 0.55f, 0f), V(0.6f, 0.25f, 0.8f), "vert", V(-8f, 0f, 0f));
            p.Box("Fond", V(0f, 0.42f, 0f), V(0.45f, 0.05f, 0.6f), "vert");
            p.Cylinder("Roue", V(0f, 0.18f, -0.55f), 0.18f, 0.08f, "caoutchouc", V(0f, 0f, 90f));
            for (int k = -1; k <= 1; k += 2)
            {
                p.Cylinder("Bras", V(k * 0.22f, 0.5f, 0.2f), 0.018f, 1.3f, "metal_noir", V(80f, 0f, 0f));
                p.Box("Pied", V(k * 0.22f, 0.2f, 0.35f), V(0.03f, 0.4f, 0.03f), "metal_noir");
            }

            p.Block("Brouette", V(0f, 0.4f, 0f), V(0.6f, 0.8f, 1.4f));
            p.Pop();
        }

        // ================================================================== marché noir

        private static void BlackMarket(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            Counter(p, V(0f, 0f, d - 2.2f), 0f, 2.4f, CounterStyle.Crates, null, null);
            WallShelf(p, V(0f, 0f, d - 0.22f), 0f, 5.0f, 4, "colis", "metal_noir", 0.4f, 0.45f, 0.15f);
            Television(p, V(-1.6f, 1.95f, d - 0.25f), 0f, false);
            Television(p, V(1.6f, 1.95f, d - 0.25f), 0f, false);

            // La cage grillagée au fond à gauche, pleine de caisses.
            ChainLink(p, V(-hw, 0f, 5.4f), V(-1.8f, 0f, 5.4f), 2.2f);
            ChainLink(p, V(-1.8f, 0f, 5.4f), V(-1.8f, 0f, d - 0.6f), 2.2f);
            for (int i = 0; i < 7; i++)
            {
                Vector3 at = V(Rand(-4.1f, -2.4f), 0f, Rand(5.9f, d - 0.6f));
                Vector3 size = V(Rand(0.5f, 0.9f), Rand(0.4f, 0.7f), Rand(0.5f, 0.8f));
                Crate(p, at, Rand(-20f, 20f), size);
                if (Pick(2) == 0) Crate(p, at + V(0f, size.y, 0f), Rand(-20f, 20f), size * 0.8f);
            }

            p.Box("Bache", V(-3.2f, 0.9f, 6.4f), V(1.4f, 0.04f, 1.2f), "tissu", V(6f, 10f, -8f));

            // À gauche devant : deux tables de marchandise.
            string[] goods = { "armes", "electronique" };
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = V(-3.6f, 0f, 2.2f + i * 2.0f);
                Table(p, at, 90f, 1.8f, 0.8f, "stratifie_fonce", false, null);
                p.Push(at, 90f);
                Trinkets(p, goods[i], -0.8f, 0.8f, 0.75f, -0.3f, 0.3f);
                p.Pop();
            }

            // À droite : le vieux canapé, la télé sur une caisse, le bidon où brûle un feu.
            Sofa(p, V(hw - 0.44f, 0f, 2.6f), 90f, 2.0f, "cuir_brun");
            Crate(p, V(2.4f, 0f, 2.6f), 0f, V(0.6f, 0.45f, 0.6f));
            Television(p, V(2.4f, 0.45f, 2.6f), -90f, true);
            p.Cylinder("Bidon", V(1.4f, 0.45f, 4.6f), 0.3f, 0.9f, "metal_noir", default(Vector3), true);
            p.Cylinder("Cercle", V(1.4f, 0.3f, 4.6f), 0.305f, 0.04f, "metal");
            p.Cylinder("Cercle", V(1.4f, 0.6f, 4.6f), 0.305f, 0.04f, "metal");
            p.Sphere("Braises", V(1.4f, 0.92f, 4.6f), V(0.5f, 0.12f, 0.5f), "neon_ambre");
            p.Light(V(1.4f, 1.3f, 4.6f), new Color(1f, 0.55f, 0.25f), 1.2f, 4f);
            for (int i = 0; i < 2; i++)
            {
                Pallet(p, V(3.5f, 0f, 5.8f + i * 1.5f), 90f);
                for (int k = 0; k < 4; k++)
                {
                    Item(p, "carton", V(3.3f + (k % 2) * 0.45f, 0.14f + (k / 2) * 0.32f, 5.8f + i * 1.5f), 0.42f, 0.3f, 0.5f, "carton", "blanc");
                }
            }

            Graffiti(p, V(hw, 1.7f, 4.2f), 90f, 2.2f, 1.0f);
            Graffiti(p, V(-hw, 1.8f, 3.2f), -90f, 2.0f, 0.9f);
            Graffiti(p, V(-2.6f, 1.6f, 0f), 180f, 2.0f, 1.0f);
            Basics(p, V(-1.4f, 0f, 1.2f), true, false);
        }

        // ================================================================== laverie

        private static void Laundry(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            Counter(p, V(2.4f, 0f, d - 1.6f), 0f, 2.0f, CounterStyle.Shop, "blanc", "stratifie");
            WallShelf(p, V(2.4f, 0f, d - 0.17f), 0f, 2.6f, 3, "lessive", "blanc", 0.3f, 0.42f, 0.7f);

            // Les machines à laver à gauche, les séchoirs empilés à droite.
            for (int i = 0; i < 8; i++)
            {
                float z = 1.5f + i * 0.72f;
                Washer(p, V(-hw + 0.34f, 0f, z), -90f, false);
                if (Pick(3) == 0) Item(p, "bidon", V(-hw + 0.3f, 0.92f, z + Rand(-0.15f, 0.15f)), 0.14f, 0.3f, 0.1f, Product(), "blanc");
            }

            for (int c = 0; c < 5; c++)
            {
                float z = 2.0f + c * 0.72f;
                Washer(p, V(hw - 0.34f, 0f, z), 90f, true);
                Washer(p, V(hw - 0.34f, 0f, z), 90f, true, 0.8f);
            }

            // La table à plier, avec du linge, et les chariots.
            Table(p, V(0f, 0f, 4.2f), 90f, 2.4f, 0.8f, "stratifie", false, null, 0.9f);
            p.Push(V(0f, 0f, 4.2f), 90f);
            for (int i = 0; i < 4; i++) Item(p, "pile_vetements", V(-0.8f + i * 0.5f, 0.9f, Rand(-0.1f, 0.1f)), 0.32f, Rand(0.08f, 0.2f), 0.26f, Product(), "blanc");
            p.Pop();
            LaundryCart(p, V(-1.9f, 0f, 7.6f), 20f);
            LaundryCart(p, V(1.3f, 0f, 6.6f), -15f);
            for (int i = 0; i < 2; i++)
            {
                Vector3 at = V(-2.7f + i * 0.6f, 0f, 7.0f - i * 0.4f);
                p.Box("Panier", at + V(0f, 0.15f, 0f), V(0.5f, 0.3f, 0.36f), Pick(2) == 0 ? "bleu" : "rose", default(Vector3), true);
                p.Sphere("Linge", at + V(0f, 0.3f, 0f), V(0.4f, 0.18f, 0.3f), Product());
            }

            // L'attente devant la vitrine ; distributeur et monnayeur au fond ; la télé.
            WaitingChairs(p, V(-2.4f, 0f, 0.45f), 180f, 4, "plastique");
            VendingMachine(p, V(-3.3f, 0f, d - 0.42f), 0f, true);
            p.Box("Monnayeur", V(-2.3f, 0.8f, d - 0.25f), V(0.6f, 1.6f, 0.5f), "metal", true);
            p.Box("Ecran", V(-2.3f, 1.3f, d - 0.502f), V(0.3f, 0.12f, 0.004f), "ecran_vert");
            p.Box("Fente a billets", V(-2.3f, 1.05f, d - 0.505f), V(0.18f, 0.03f, 0.006f), "noir");
            WallTV(p, V(-0.8f, 2.2f, d), 0f, 1.0f);
            CorkBoard(p, V(hw, 2.1f, 6.8f), 90f, 1.2f, 0.6f);
            Clock(p, V(-hw, 2.3f, 4.0f), -90f);
            Plant(p, V(hw - 0.4f, 0f, 0.45f), 1);
            Basics(p, V(0.9f, 0f, 2.6f), true, false);
        }

        // ================================================================== poste

        private static void PostOffice(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            float cz = d - 2.4f;
            Counter(p, V(0.6f, 0f, cz), 0f, 5.0f, CounterStyle.Window, "bois", "stratifie", -1.2f);
            p.Push(V(0.6f, 0f, cz), 0f);
            p.Box("Balance", V(1.6f, 1.045f, -0.1f), V(0.35f, 0.05f, 0.35f), "inox");
            p.Box("Affichage", V(1.6f, 1.05f, -0.282f), V(0.14f, 0.05f, 0.01f), "ecran_vert");
            Monitor(p, V(-1.6f, 1.02f, 0.15f), 180f, 0.45f);
            p.Pop();

            // Derrière : les étagères à colis, un chariot de courrier, une pile de paquets.
            WallShelf(p, V(0.6f, 0f, d - 0.22f), 0f, 6.0f, 4, "colis", "metal", 0.4f, 0.45f, 0.15f);
            LaundryCart(p, V(2.6f, 0f, d - 1.1f), 0f);
            for (int i = 0; i < 5; i++) Item(p, "carton", V(-1.9f + Rand(-0.05f, 0.05f), i * 0.22f, d - 1.1f), 0.4f, 0.22f, 0.35f, "carton", "blanc");

            // Les boîtes postales, la file, le pupitre pour écrire, l'automate à timbres.
            PostBoxes(p, V(-hw, 0f, 4.0f), -90f, 3.2f, 2.2f);
            Stanchions(p, V(-1.6f, 0f, 3.2f), V(-1.6f, 0f, 5.6f), false);
            Stanchions(p, V(0.4f, 0f, 3.2f), V(0.4f, 0f, 5.6f), false);
            Table(p, V(3.2f, 0f, 3.8f), 90f, 1.6f, 0.6f, "bois", false, null, 1.05f);
            p.Push(V(3.2f, 0f, 3.8f), 90f);
            Papers(p, V(-0.3f, 1.05f, 0f), 0f);
            p.Cylinder("Chainette", V(0.4f, 1.0f, 0.25f), 0.003f, 0.12f, "metal");
            p.Cylinder("Stylo", V(0.4f, 1.055f, 0.15f), 0.005f, 0.14f, "noir", V(90f, 0f, 0f));
            p.Pop();
            p.Box("Automate a timbres", V(hw - 0.3f, 0.75f, 5.8f), V(0.5f, 1.5f, 0.6f), "jaune", true);
            p.Box("Ecran", V(hw - 0.552f, 1.15f, 5.8f), V(0.004f, 0.2f, 0.3f), "ecran");
            p.Box("Fente", V(hw - 0.555f, 0.8f, 5.8f), V(0.006f, 0.03f, 0.15f), "noir");

            WaitingChairs(p, V(2.6f, 0f, 0.45f), 180f, 3, "bleu");
            for (int i = 0; i < 3; i++) Frame(p, V(hw, 2.2f, 1.6f + i * 1.0f), 90f, 0.5f, 0.7f, "affiche");
            Clock(p, V(hw, 2.4f, 7.4f), 90f);
            Plant(p, V(-hw + 0.4f, 0f, 0.45f), 0);
            Basics(p, V(1.3f, 0f, 2.6f), true, true);
        }

        // ================================================================== prêteur sur gages

        private static void Pawn(InteriorPlan p)
        {
            float d = p.Size.z, hw = p.Size.x * 0.5f;
            Counter(p, V(0f, 0f, d - 2.2f), 0f, 4.0f, CounterStyle.Showcase, "bois_fonce", null, 0f, "bijoux");
            Safe(p, V(3.2f, 0f, d - 0.4f), 0f);
            WallShelf(p, V(-1.4f, 0f, d - 0.22f), 0f, 3.8f, 4, "electronique", "bois", 0.4f, 0.45f, 0.15f);

            // Les guitares à droite, les vieilles télés à gauche, deux vitrines au milieu.
            for (int i = 0; i < 5; i++) WallGuitar(p, V(hw, 1.4f, 2.0f + i * 0.75f), 90f);
            WallShelf(p, V(-hw + 0.22f, 0f, 3.6f), -90f, 3.2f, 2, "electronique", "bois", 0.42f, 0.5f, 0.2f);
            for (int i = 0; i < 3; i++) Television(p, V(-hw + 0.24f, 1.2f, 2.6f + i * 1.0f), -90f, true);
            DisplayCase(p, V(-1.5f, 0f, 4.0f), 0f, 1.6f, "electronique");
            DisplayCase(p, V(1.7f, 0f, 4.0f), 0f, 1.6f, "armes");

            // Des barreaux devant les vitrines : on est chez un prêteur.
            float x0, x1;
            WindowSpan(p, out x0, out x1);
            for (int k = -1; k <= 1; k += 2)
            {
                for (float x = x0 + 0.06f; x < x1 - 0.03f; x += 0.12f) p.Cylinder("Barreau", V(k * x, 1.6f, 0.12f), 0.01f, 1.7f, "metal_noir");
                p.Box("Traverse", V(k * (x0 + x1) * 0.5f, 0.82f, 0.12f), V(x1 - x0, 0.03f, 0.03f), "metal_noir");
                p.Box("Traverse", V(k * (x0 + x1) * 0.5f, 2.4f, 0.12f), V(x1 - x0, 0.03f, 0.03f), "metal_noir");
            }

            p.Neon("OR ARGENT", V(-hw + 0.03f, 2.45f, 6.0f), -90f, 0.22f, "neon_ambre");
            Frame(p, V(-hw, 2.45f, 2.8f), -90f, 0.6f, 0.45f, "affiche");
            DomeMirror(p, V(-hw + 0.4f, p.Size.y - 0.15f, d - 0.4f));
            Plant(p, V(hw - 0.4f, 0f, 0.6f), 1);
            Basics(p, V(-2.6f, 0f, 6.3f), true, true);
        }
    }
}
