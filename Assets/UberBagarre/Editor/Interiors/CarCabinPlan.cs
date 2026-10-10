using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Le plan d'un habitacle de voiture, taillé sur les mesures de la carrosserie (voir
    /// <see cref="CarCabinBuilder"/>) : plancher et moquette, tableau de bord avec son combiné
    /// (compteur et compte-tours à aiguilles), autoradio, aérateurs, console centrale et levier,
    /// volant à trois branches sur sa colonne, pédalier, montants de pare-brise, ciel de toit,
    /// pare-soleil, rétroviseur, panneaux de portières (accoudoir, poignée, haut-parleur), sièges
    /// avant (assise, dossier, appui-tête), banquette arrière, plage arrière.
    ///
    /// Repère : celui de la voiture (x à droite, y vers le haut depuis le sol, z vers l'avant).
    /// Pur : aucune dépendance à l'éditeur, il se vérifie hors Unity.
    /// </summary>
    public static class CarCabinPlan
    {
        /// <summary>Les mesures de la carrosserie, et ce qu'on en déduit pour le conducteur.</summary>
        public struct Cabin
        {
            public float HalfWidth;   // demi-largeur intérieure
            public float Floor;       // hauteur du plancher
            public float RoofY;       // dessous du toit (extérieur)
            public float RoofFront;   // haut du pare-brise (z)
            public float RoofBack;    // haut de la lunette (z)
            public float CowlZ;       // bas du pare-brise (z)
            public float CowlY;       // bas du pare-brise (y)
            public float Rear;        // fond de l'habitacle (z)
            public bool FourDoors;
            public bool RearSeats;
            public string Style;      // "berline", "sport", "utilitaire", "vieille"

            public Vector3 Hip;
            public Vector3 Eye;
            public Vector3 Wheel;
            public Vector3 WheelNormal;
        }

        /// <summary>Le conducteur : bassin, yeux, volant, d'après la forme de l'habitacle.</summary>
        public static Cabin Derive(Cabin c)
        {
            float hx = -c.HalfWidth * 0.5f;
            float hz = Mathf.Max(c.CowlZ - 1.18f, c.RoofBack + 0.25f);
            float hy = c.Floor + 0.27f;
            Vector3 eye = new Vector3(hx, hy + 0.7f, hz - 0.12f);

            // Une voiture de la carte est plus basse qu'une vraie : les yeux restent sous le toit.
            float ceiling = c.RoofY - 0.12f;
            if (eye.y > ceiling)
            {
                float drop = eye.y - ceiling;
                eye.y = ceiling;
                hy -= drop;
            }

            c.Hip = new Vector3(hx, hy, hz);
            c.Eye = eye;

            float wz = Mathf.Min(hz + 0.5f, c.CowlZ - 0.42f);
            float wy = Mathf.Clamp(eye.y - 0.36f, hy + 0.22f, c.CowlY - 0.04f);
            c.Wheel = new Vector3(hx, wy, wz);
            c.WheelNormal = new Vector3(0f, Mathf.Sin(24f * Mathf.Deg2Rad), -Mathf.Cos(24f * Mathf.Deg2Rad));
            return c;
        }

        public static InteriorPlan Make(Cabin c)
        {
            InteriorPlan p = new InteriorPlan();
            p.Size = new Vector3(c.HalfWidth * 2f, c.RoofY, Mathf.Max(1f, c.CowlZ - c.Rear));
            Palette(p, c.Style);

            Floor(p, c);
            Dashboard(p, c);
            SteeringWheel(p, c);
            Cluster(p, c);
            Console(p, c);
            Pillars(p, c);
            Doors(p, c);
            Seats(p, c);

            p.Mark("yeux", c.Eye, 0f);
            return p;
        }

        private static void Palette(InteriorPlan p, string style)
        {
            bool sport = style == "sport";
            bool old = style == "vieille";
            Color trim = old ? new Color(0.36f, 0.3f, 0.24f) : new Color(0.07f, 0.07f, 0.075f);
            Color fabric = sport ? new Color(0.08f, 0.08f, 0.085f) : old ? new Color(0.42f, 0.33f, 0.24f) : new Color(0.2f, 0.2f, 0.21f);

            p.Define("tableau", trim, 0.35f, 0f, "cuir");
            p.Define("plastique_cabine", trim * 1.4f, 0.4f, 0f, null);
            p.Define("plastique_clair", new Color(0.34f, 0.33f, 0.31f), 0.35f, 0f, null);
            p.Define("siege", fabric, sport ? 0.4f : 0.08f, 0f, sport ? "cuir" : "moquette");
            p.Define("siege_bord", fabric * 0.7f, 0.3f, 0f, "cuir");
            p.Define("moquette_auto", new Color(0.06f, 0.06f, 0.065f), 0.05f, 0f, "moquette");
            p.Define("ciel_de_toit", new Color(0.62f, 0.6f, 0.56f), 0.05f, 0f, "moquette");
            p.Define("volant", new Color(0.05f, 0.05f, 0.055f), 0.45f, 0f, "cuir");
            p.Define("chrome_auto", new Color(0.85f, 0.85f, 0.87f), 0.92f, 1f, null);
            p.Define("metal_pedale", new Color(0.55f, 0.56f, 0.58f), 0.6f, 0.9f, null);
            p.Define("miroir", new Color(0.7f, 0.74f, 0.78f), 0.98f, 1f, null);
            p.Define("haut_parleur", new Color(0.03f, 0.03f, 0.03f), 0.1f, 0f, "moquette");
            p.Define("noir_auto", new Color(0.02f, 0.02f, 0.022f), 0.3f, 0f, null);
            p.Glow("cadran", new Color(0.05f, 0.055f, 0.06f), new Color(0.05f, 0.06f, 0.075f));
            p.Glow("cadran_trait", new Color(0.9f, 0.92f, 0.95f), new Color(0.85f, 0.9f, 1f) * 0.9f);
            p.Glow("aiguille", new Color(1f, 0.35f, 0.1f), new Color(1f, 0.32f, 0.08f) * 1.6f);
            p.Glow("autoradio", new Color(0.02f, 0.05f, 0.06f), new Color(0.15f, 0.65f, 0.9f) * 0.8f);
            p.Glow("voyant", new Color(0.2f, 0.9f, 0.3f), new Color(0.2f, 0.9f, 0.3f) * 0.6f);
        }

        private static Vector3 V(float x, float y, float z)
        {
            return new Vector3(x, y, z);
        }

        // ------------------------------------------------------------------ plancher

        private static void Floor(InteriorPlan p, Cabin c)
        {
            float length = c.CowlZ - c.Rear;
            float mid = (c.CowlZ + c.Rear) * 0.5f;
            p.Box("Plancher", V(0f, c.Floor - 0.03f, mid), V(c.HalfWidth * 2f, 0.06f, length), "moquette_auto");
            // Le tablier (sous le tableau de bord, devant les pieds).
            p.Box("Tablier", V(0f, c.Floor + 0.2f, c.CowlZ - 0.05f), V(c.HalfWidth * 2f, 0.45f, 0.06f), "moquette_auto", V(-35f, 0f, 0f));
            // Tunnel de transmission.
            p.Box("Tunnel", V(0f, c.Floor + 0.07f, mid + 0.1f), V(0.24f, 0.14f, length * 0.8f), "moquette_auto");

            // Pédalier : embrayage, frein, accélérateur.
            float pz = c.Hip.z + 0.88f;
            float[] xs = { -0.13f, 0f, 0.13f };
            for (int i = 0; i < 3; i++)
            {
                Vector3 at = V(c.Hip.x + xs[i], c.Floor + 0.12f, Mathf.Min(pz, c.CowlZ - 0.12f));
                p.Box("Pedale", at, V(i == 2 ? 0.05f : 0.08f, i == 2 ? 0.16f : 0.07f, 0.02f), "metal_pedale", V(-40f, 0f, 0f));
                p.Box("Bras de pedale", at + V(0f, 0.12f, 0.04f), V(0.015f, 0.2f, 0.015f), "plastique_cabine", V(-20f, 0f, 0f));
            }
        }

        // ------------------------------------------------------------------ tableau de bord

        private static void Dashboard(InteriorPlan p, Cabin c)
        {
            float w = c.HalfWidth * 2f;
            float top = c.CowlY - 0.04f;
            const float depth = 0.55f;

            // Le dessus (la planche, qui plonge un peu vers le pare-brise : on voit le capot
            // par-dessus), puis la face qui descend vers les genoux.
            p.Box("Planche de bord", V(0f, top - 0.04f, c.CowlZ - depth * 0.5f), V(w, 0.08f, depth), "tableau", V(4f, 0f, 0f));
            p.Box("Face du tableau", V(0f, top - 0.24f, c.CowlZ - depth + 0.04f), V(w, 0.34f, 0.08f), "plastique_cabine", V(12f, 0f, 0f));
            p.Box("Dessous du tableau", V(0f, top - 0.43f, c.CowlZ - depth * 0.6f), V(w, 0.05f, depth * 0.7f), "plastique_cabine");

            // Aérateurs (deux au centre, un à chaque bout) : cadre et lamelles.
            float[] vents = { -c.HalfWidth + 0.12f, -0.12f, 0.12f, c.HalfWidth - 0.12f };
            for (int i = 0; i < vents.Length; i++)
            {
                Vector3 at = V(vents[i], top - 0.12f, c.CowlZ - depth + 0.0f);
                p.Box("Aerateur", at, V(0.13f, 0.06f, 0.02f), "plastique_clair");
                for (int k = -1; k <= 1; k++) p.Box("Lamelle d'aerateur", at + V(0f, k * 0.017f, -0.012f), V(0.12f, 0.006f, 0.012f), "noir_auto");
            }

            // Autoradio et commandes au centre.
            p.Box("Autoradio", V(0f, top - 0.22f, c.CowlZ - depth - 0.005f), V(0.2f, 0.06f, 0.02f), "autoradio");
            for (int k = -2; k <= 2; k++) p.Box("Bouton", V(k * 0.04f, top - 0.29f, c.CowlZ - depth - 0.01f), V(0.022f, 0.016f, 0.02f), "plastique_clair");
            p.Cylinder("Molette", V(-0.13f, top - 0.22f, c.CowlZ - depth - 0.01f), 0.018f, 0.02f, "chrome_auto", V(90f, 0f, 0f));
            p.Cylinder("Molette", V(0.13f, top - 0.22f, c.CowlZ - depth - 0.01f), 0.018f, 0.02f, "chrome_auto", V(90f, 0f, 0f));

            // Boîte à gants côté passager.
            p.Box("Boite a gants", V(c.HalfWidth * 0.48f, top - 0.33f, c.CowlZ - depth + 0.0f), V(0.42f, 0.16f, 0.02f), "plastique_cabine", V(12f, 0f, 0f));
            p.Box("Poignee de boite a gants", V(c.HalfWidth * 0.48f, top - 0.27f, c.CowlZ - depth - 0.02f), V(0.08f, 0.015f, 0.015f), "chrome_auto");
        }

        // ------------------------------------------------------------------ volant

        private static void SteeringWheel(InteriorPlan p, Cabin c)
        {
            Quaternion frame = Quaternion.LookRotation(c.WheelNormal, Vector3.up);
            Vector3 euler = frame.eulerAngles;
            const float r = 0.18f;

            // La colonne (fixe), du volant au tableau de bord.
            Vector3 column = c.Wheel - c.WheelNormal * 0.2f;
            Quaternion along = Quaternion.FromToRotation(Vector3.up, c.WheelNormal);
            p.Cylinder("Colonne de direction", column, 0.035f, 0.34f, "plastique_cabine", along.eulerAngles);
            p.Box("Commodo", c.Wheel - c.WheelNormal * 0.1f + V(-0.1f, 0f, 0f), V(0.1f, 0.018f, 0.02f), "plastique_cabine");
            p.Box("Commodo", c.Wheel - c.WheelNormal * 0.1f + V(0.1f, 0f, 0f), V(0.1f, 0.018f, 0.02f), "plastique_cabine");

            p.Push(c.Wheel, euler);
            p.Mark("pivot_volant", Vector3.zero, 0f);
            p.Group = "volant";
            const int segments = 18;
            for (int i = 0; i < segments; i++)
            {
                float a = i * 360f / segments;
                float rad = a * Mathf.Deg2Rad;
                float chord = 2f * r * Mathf.Sin(Mathf.PI / segments) + 0.006f;
                p.Box("Jante", V(Mathf.Cos(rad) * r, Mathf.Sin(rad) * r, 0f), V(chord, 0.03f, 0.034f), "volant", V(0f, 0f, a + 90f));
            }

            p.Cylinder("Moyeu", V(0f, -0.01f, 0.01f), 0.062f, 0.05f, "volant", V(90f, 0f, 0f));
            p.Box("Coussin", V(0f, -0.01f, 0.035f), V(0.13f, 0.09f, 0.02f), "plastique_cabine");
            float[] spokes = { 180f, 0f, 270f };
            for (int i = 0; i < spokes.Length; i++)
            {
                float rad = spokes[i] * Mathf.Deg2Rad;
                p.Box("Branche", V(Mathf.Cos(rad) * r * 0.55f, Mathf.Sin(rad) * r * 0.55f, 0.01f), V(r * 0.75f, 0.04f, 0.018f), "volant",
                    V(0f, 0f, spokes[i]));
            }

            p.Pop();
        }

        // ------------------------------------------------------------------ combiné d'instruments

        private static void Cluster(InteriorPlan p, Cabin c)
        {
            // Les cadrans, derrière le haut du volant, tournés vers les yeux.
            float dz = Mathf.Min(c.Wheel.z + 0.24f, c.CowlZ - 0.28f);
            float dy = Mathf.Min(c.Wheel.y + 0.12f, c.CowlY - 0.09f);
            Vector3 center = V(c.Hip.x, dy, dz);
            Vector3 normal = (c.Eye - center).normalized;
            Vector3 euler = Quaternion.LookRotation(normal, Vector3.up).eulerAngles;

            p.Push(center, euler);
            p.Box("Fond du combine", V(0f, 0f, -0.02f), V(0.36f, 0.13f, 0.02f), "noir_auto");
            p.Box("Visiere du combine", V(0f, 0.085f, 0.03f), V(0.4f, 0.03f, 0.12f), "tableau", V(-12f, 0f, 0f));
            Dial(p, V(-0.09f, 0f, 0f), "pivot_vitesse", "aiguille_vitesse");
            Dial(p, V(0.09f, 0f, 0f), "pivot_regime", "aiguille_regime");
            // Voyants entre les deux cadrans.
            p.Box("Voyant", V(0f, 0.03f, -0.005f), V(0.012f, 0.008f, 0.004f), "voyant");
            p.Pop();
        }

        private static void Dial(InteriorPlan p, Vector3 at, string pivot, string needle)
        {
            const float r = 0.072f;
            p.Push(at, Vector3.zero);
            p.Cylinder("Cadran", V(0f, 0f, -0.006f), r, 0.008f, "cadran", V(90f, 0f, 0f));
            p.Cylinder("Lunette du cadran", V(0f, 0f, -0.004f), r + 0.006f, 0.006f, "chrome_auto", V(90f, 0f, 0f));

            // Graduations de -125° à +125° (vu du conducteur : en bas à gauche jusqu'en bas à droite).
            for (int i = 0; i <= 10; i++)
            {
                float a = -125f + i * 25f;
                Quaternion q = Quaternion.AngleAxis(a, Vector3.forward);
                Vector3 tip = q * V(0f, r * 0.82f, 0.001f);
                p.Box("Graduation", tip, V(0.004f, i % 2 == 0 ? 0.014f : 0.008f, 0.002f), "cadran_trait", V(0f, 0f, a));
            }

            p.Mark(pivot, Vector3.zero, 0f);
            string group = p.Group;
            p.Group = needle;
            // L'aiguille au repos sur la première graduation (en bas à gauche).
            Quaternion rest = Quaternion.AngleAxis(-125f, Vector3.forward);
            p.Box("Aiguille", rest * V(0f, r * 0.38f, 0.004f), V(0.0045f, r * 0.86f, 0.002f), "aiguille", V(0f, 0f, -125f));
            p.Group = group;
            p.Cylinder("Axe d'aiguille", V(0f, 0f, 0.005f), 0.008f, 0.006f, "noir_auto", V(90f, 0f, 0f));
            p.Pop();
        }

        // ------------------------------------------------------------------ console, levier

        private static void Console(InteriorPlan p, Cabin c)
        {
            float front = c.CowlZ - 0.55f;
            float back = c.Hip.z + 0.05f;
            float length = Mathf.Max(0.3f, front - back);
            float top = c.Hip.y + 0.12f;
            p.Box("Console centrale", V(0f, (c.Floor + top) * 0.5f, back + length * 0.5f), V(0.22f, top - c.Floor, length), "plastique_cabine");
            p.Box("Dessus de console", V(0f, top + 0.005f, back + length * 0.5f), V(0.2f, 0.01f, length - 0.02f), "tableau");
            p.Box("Soufflet de levier", V(0f, top + 0.03f, back + length * 0.62f), V(0.08f, 0.05f, 0.1f), "volant");
            p.Cylinder("Levier de vitesses", V(0f, top + 0.12f, back + length * 0.62f), 0.012f, 0.18f, "chrome_auto");
            p.Sphere("Pommeau", V(0f, top + 0.22f, back + length * 0.62f), V(0.05f, 0.05f, 0.05f), "volant");
            p.Box("Frein a main", V(0f, top + 0.04f, back + length * 0.25f), V(0.035f, 0.035f, 0.22f), "volant", V(-12f, 0f, 0f));
        }

        // ------------------------------------------------------------------ montants, toit

        private static void Pillars(InteriorPlan p, Cabin c)
        {
            float headliner = c.RoofY - 0.035f;
            for (int k = -1; k <= 1; k += 2)
            {
                // Montant de pare-brise (A), du bas du pare-brise au toit.
                Vector3 a0 = V(k * (c.HalfWidth - 0.02f), c.CowlY - 0.02f, c.CowlZ - 0.04f);
                Vector3 a1 = V(k * (c.HalfWidth - 0.06f), headliner - 0.02f, c.RoofFront);
                Strut(p, "Montant de pare-brise", a0, a1, 0.085f, 0.06f, "plastique_cabine");

                // Montant central (B) derrière le conducteur, et le montant arrière.
                float bz = c.Hip.z - 0.32f;
                Strut(p, "Montant central", V(k * (c.HalfWidth - 0.03f), c.CowlY - 0.05f, bz), V(k * (c.HalfWidth - 0.07f), headliner, bz), 0.1f, 0.07f,
                    "plastique_clair");
                Strut(p, "Montant arriere", V(k * (c.HalfWidth - 0.03f), c.CowlY - 0.05f, c.Rear + 0.08f), V(k * (c.HalfWidth - 0.1f), headliner, c.RoofBack),
                    0.14f, 0.08f, "plastique_clair");

                // Pare-soleil, rabattu contre le toit.
                p.Box("Pare-soleil", V(k * c.HalfWidth * 0.48f, headliner - 0.022f, c.RoofFront - 0.1f), V(c.HalfWidth * 0.78f, 0.012f, 0.16f), "ciel_de_toit");
            }

            float roofLength = c.RoofFront - c.RoofBack;
            p.Box("Ciel de toit", V(0f, headliner, (c.RoofFront + c.RoofBack) * 0.5f), V(c.HalfWidth * 2f - 0.06f, 0.03f, roofLength + 0.1f), "ciel_de_toit");
            p.Box("Plafonnier", V(0f, headliner - 0.02f, c.RoofFront - 0.35f), V(0.14f, 0.015f, 0.08f), "plastique_clair");

            // Le rétroviseur intérieur, au milieu du haut du pare-brise, au-dessus de la ligne des yeux.
            Vector3 mirror = V(0f, Mathf.Max(c.Eye.y + 0.06f, headliner - 0.05f), c.RoofFront - 0.04f);
            p.Box("Pied de retroviseur", mirror + V(0f, (headliner - mirror.y) * 0.5f, 0.02f), V(0.018f, Mathf.Max(0.01f, headliner - mirror.y), 0.018f),
                "plastique_cabine");
            p.Box("Retroviseur", mirror, V(0.2f, 0.05f, 0.025f), "plastique_cabine", V(0f, 8f, 0f));
            p.Box("Glace du retroviseur", mirror + V(0f, 0f, -0.013f), V(0.185f, 0.038f, 0.004f), "miroir", V(0f, 8f, 0f));
        }

        /// <summary>Une poutre de section <paramref name="width"/> × <paramref name="depth"/> entre deux points.</summary>
        private static void Strut(InteriorPlan p, string name, Vector3 from, Vector3 to, float width, float depth, string surface)
        {
            Vector3 d = to - from;
            Quaternion q = Quaternion.FromToRotation(Vector3.up, d.normalized);
            p.Box(name, (from + to) * 0.5f, V(width, d.magnitude, depth), surface, q.eulerAngles);
        }

        // ------------------------------------------------------------------ portières

        private static void Doors(InteriorPlan p, Cabin c)
        {
            float belt = c.CowlY - 0.03f;
            float frontStart = c.Hip.z - 0.3f;
            float frontEnd = c.CowlZ - 0.08f;
            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * (c.HalfWidth - 0.03f);
                DoorPanel(p, x, k, c.Floor, belt, frontStart, frontEnd);
                if (c.FourDoors) DoorPanel(p, x, k, c.Floor, belt, c.Rear + 0.12f, frontStart - 0.04f);
                else p.Box("Garniture laterale", V(x, (c.Floor + belt) * 0.5f, (c.Rear + frontStart) * 0.5f), V(0.06f, belt - c.Floor, frontStart - c.Rear), "plastique_clair");
            }
        }

        private static void DoorPanel(InteriorPlan p, float x, int side, float floor, float belt, float z0, float z1)
        {
            float length = Mathf.Max(0.3f, z1 - z0);
            float mid = (z0 + z1) * 0.5f;
            float inward = -side;
            p.Box("Panneau de portiere", V(x, (floor + belt) * 0.5f, mid), V(0.05f, belt - floor, length), "plastique_clair");
            p.Box("Haut de portiere", V(x + inward * 0.025f, belt - 0.02f, mid), V(0.07f, 0.05f, length), "tableau");
            p.Box("Accoudoir", V(x + inward * 0.06f, floor + (belt - floor) * 0.48f, mid - length * 0.05f), V(0.08f, 0.05f, length * 0.5f), "siege_bord");
            p.Box("Poignee interieure", V(x + inward * 0.035f, floor + (belt - floor) * 0.72f, mid + length * 0.25f), V(0.02f, 0.03f, 0.09f), "chrome_auto");
            p.Box("Insert de tissu", V(x + inward * 0.028f, floor + (belt - floor) * 0.66f, mid), V(0.006f, (belt - floor) * 0.3f, length * 0.8f), "siege");
            p.Cylinder("Haut-parleur", V(x + inward * 0.028f, floor + 0.16f, mid + length * 0.2f), 0.07f, 0.008f, "haut_parleur", V(0f, 0f, 90f));
            p.Box("Vide-poche", V(x + inward * 0.05f, floor + 0.08f, mid), V(0.04f, 0.1f, length * 0.6f), "plastique_cabine");
        }

        // ------------------------------------------------------------------ sièges

        private static void Seats(InteriorPlan p, Cabin c)
        {
            Seat(p, c, c.Hip.x, c.Hip.z);
            Seat(p, c, -c.Hip.x, c.Hip.z);

            if (c.RearSeats && c.Hip.z - c.Rear > 0.95f)
            {
                float rz = c.Rear + 0.42f;
                float w = c.HalfWidth * 2f - 0.16f;
                p.Box("Banquette", V(0f, c.Hip.y - 0.04f, rz + 0.12f), V(w, 0.14f, 0.5f), "siege");
                p.Box("Dossier de banquette", V(0f, c.Hip.y + 0.3f, rz - 0.1f), V(w, 0.56f, 0.13f), "siege", V(-12f, 0f, 0f));
                for (int k = -1; k <= 1; k++) p.Box("Appui-tete arriere", V(k * w * 0.33f, c.Hip.y + 0.64f, rz - 0.15f), V(0.24f, 0.13f, 0.08f), "siege");
                p.Box("Plage arriere", V(0f, c.CowlY - 0.06f, c.Rear + 0.1f), V(w, 0.025f, 0.24f), "moquette_auto");
            }
        }

        private static void Seat(InteriorPlan p, Cabin c, float x, float z)
        {
            p.Push(V(x, 0f, z), 0f);
            float y = c.Hip.y;
            p.Box("Rail de siege", V(0f, c.Floor + 0.04f, 0.1f), V(0.36f, 0.06f, 0.5f), "plastique_cabine");
            p.Box("Assise", V(0f, y - 0.05f, 0.18f), V(0.5f, 0.12f, 0.5f), "siege", V(-6f, 0f, 0f));
            p.Box("Bourrelet d'assise", V(-0.23f, y + 0.0f, 0.18f), V(0.06f, 0.1f, 0.5f), "siege_bord", V(-6f, 0f, 0f));
            p.Box("Bourrelet d'assise", V(0.23f, y + 0.0f, 0.18f), V(0.06f, 0.1f, 0.5f), "siege_bord", V(-6f, 0f, 0f));
            p.Box("Dossier", V(0f, y + 0.3f, -0.1f), V(0.5f, 0.62f, 0.12f), "siege", V(-14f, 0f, 0f));
            p.Box("Bourrelet de dossier", V(-0.24f, y + 0.28f, -0.07f), V(0.06f, 0.56f, 0.12f), "siege_bord", V(-14f, 0f, 0f));
            p.Box("Bourrelet de dossier", V(0.24f, y + 0.28f, -0.07f), V(0.06f, 0.56f, 0.12f), "siege_bord", V(-14f, 0f, 0f));
            p.Box("Appui-tete", V(0f, y + 0.7f, -0.2f), V(0.26f, 0.16f, 0.1f), "siege", V(-10f, 0f, 0f));
            p.Cylinder("Tige d'appui-tete", V(-0.07f, y + 0.6f, -0.18f), 0.007f, 0.1f, "chrome_auto");
            p.Cylinder("Tige d'appui-tete", V(0.07f, y + 0.6f, -0.18f), 0.007f, 0.1f, "chrome_auto");
            p.Pop();
        }
    }
}
