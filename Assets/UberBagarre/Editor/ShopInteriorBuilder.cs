using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les intérieurs des magasins dont la carte n'a que la façade (le casino, la pharmacie, la
    /// pizzeria, les bars, l'arcade, la salle de boxe…). Chacun est une vraie pièce, meublée
    /// selon ce qu'on y fait, posée loin de la ville et allumée seulement quand on y est — comme
    /// le Vertigo. On y entre par la façade (fondu au noir), on en sort par sa porte.
    ///
    /// Repère d'une pièce : l'entrée au milieu du mur sud (z = 0), on entre vers +Z ; le
    /// comptoir est contre le mur du fond ou sur le côté selon le métier.
    /// </summary>
    internal static class ShopInteriorBuilder
    {
        public sealed class Result
        {
            public GameObject Root;
            public Transform Arrival;
            public Interactable Exit;

            /// <summary>Où se tient le vendeur (monde), tourné vers la salle.</summary>
            public Vector3 ClerkPosition;
            public Quaternion ClerkRotation;

            /// <summary>Le comptoir : E dessus ouvre le magasin.</summary>
            public Transform Counter;
        }

        private const string Folder = NightMaterialFactory.MaterialsFolder + "/Magasins";

        private sealed class Kit
        {
            public Material Floor;
            public Material Wall;
            public Material Ceiling;
            public Material Trim;
            public Material Counter;
            public Material CounterTop;
            public Material Accent;
            public Material Neon;
            public Color Light;
        }

        private static Material Mat(string name, Color color, float smooth, float metal)
        {
            return EditorBuildUtility.CreateOrUpdateMaterial(Folder, "M_Magasin_" + name, color, smooth, metal);
        }

        /// <summary>La pièce d'un métier, à <paramref name="origin"/> (monde).</summary>
        public static Result Build(ShopKind kind, string sign, Transform parent, Vector3 origin, NightMaterialFactory.Palette night)
        {
            EditorBuildUtility.EnsureFolder(Folder);
            Vector3 size = Size(kind);
            Kit kit = Palette(kind, night);

            GameObject root = new GameObject("Interieur " + sign);
            root.transform.SetParent(parent, false);
            root.transform.position = origin;

            Result result = new Result { Root = root };
            Transform t = root.transform;
            float w = size.x, d = size.z, h = size.y;

            // --- la pièce : sol, plafond, quatre murs (l'entrée au milieu du mur sud), plinthes
            Box(t, "Sol", new Vector3(0f, -0.05f, d * 0.5f), new Vector3(w, 0.1f, d), kit.Floor, true);
            Box(t, "Plafond", new Vector3(0f, h + 0.05f, d * 0.5f), new Vector3(w, 0.1f, d), kit.Ceiling, true);
            Box(t, "Mur du fond", new Vector3(0f, h * 0.5f, d + 0.1f), new Vector3(w + 0.4f, h, 0.2f), kit.Wall, true);
            Box(t, "Mur gauche", new Vector3(-w * 0.5f - 0.1f, h * 0.5f, d * 0.5f), new Vector3(0.2f, h, d), kit.Wall, true);
            Box(t, "Mur droit", new Vector3(w * 0.5f + 0.1f, h * 0.5f, d * 0.5f), new Vector3(0.2f, h, d), kit.Wall, true);
            Box(t, "Mur de l'entree", new Vector3(0f, h * 0.5f, -0.1f), new Vector3(w + 0.4f, h, 0.2f), kit.Wall, true);
            Box(t, "Plinthe fond", new Vector3(0f, 0.06f, d - 0.01f), new Vector3(w, 0.12f, 0.03f), kit.Trim, false);
            Box(t, "Plinthe gauche", new Vector3(-w * 0.5f + 0.01f, 0.06f, d * 0.5f), new Vector3(0.03f, 0.12f, d), kit.Trim, false);
            Box(t, "Plinthe droite", new Vector3(w * 0.5f - 0.01f, 0.06f, d * 0.5f), new Vector3(0.03f, 0.12f, d), kit.Trim, false);

            // --- la porte de sortie, dans le mur sud
            GameObject frame = EditorBuildUtility.CreateEmpty("Porte (sortie)", t, new Vector3(0f, 0f, 0.02f));
            Box(frame.transform, "Chambranle gauche", new Vector3(-0.55f, 1.08f, 0f), new Vector3(0.08f, 2.16f, 0.12f), kit.Trim, false);
            Box(frame.transform, "Chambranle droit", new Vector3(0.55f, 1.08f, 0f), new Vector3(0.08f, 2.16f, 0.12f), kit.Trim, false);
            Box(frame.transform, "Linteau", new Vector3(0f, 2.18f, 0f), new Vector3(1.18f, 0.08f, 0.12f), kit.Trim, false);
            GameObject leaf = Box(frame.transform, "Battant", new Vector3(0f, 1.06f, 0.02f), new Vector3(1.0f, 2.1f, 0.05f), night.Glass, true);
            Box(frame.transform, "Poignee", new Vector3(0.38f, 1.02f, 0.07f), new Vector3(0.14f, 0.03f, 0.04f), night.Chrome, false);
            Box(frame.transform, "Tapis", new Vector3(0f, 0.005f, 0.8f), new Vector3(1.4f, 0.01f, 0.9f), night.Rubber, false);

            Interactable exit = leaf.AddComponent<Interactable>();
            SerializedWiring.SetString(exit, "_label", "Sortir");
            SerializedWiring.SetString(exit, "_hint", "Retour dans la rue.");
            SerializedWiring.SetFloat(exit, "_range", 3f);
            result.Exit = exit;

            GameObject arrival = EditorBuildUtility.CreateEmpty("Arrivee", t, new Vector3(0f, 0.05f, 1.5f));
            result.Arrival = arrival.transform;

            // --- l'éclairage : des plafonniers, et leurs lampes
            int rows = Mathf.Max(1, Mathf.RoundToInt(d / 4.5f));
            int cols = Mathf.Max(1, Mathf.RoundToInt(w / 5f));
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector3 p = new Vector3(-w * 0.5f + w * (c + 0.5f) / cols, h - 0.04f, d * (r + 0.5f) / rows);
                    Box(t, "Plafonnier", p, new Vector3(1.1f, 0.04f, 0.5f), night.GlowWhite, false);
                    NightStreetBuilder.AddLight(t, "Lampe", p - Vector3.up * 0.35f, kit.Light, 1.35f, Mathf.Max(w, d) * 0.7f, false, false);
                }
            }

            // --- l'enseigne, au-dessus du comptoir
            // Lisible depuis la salle : le néon se lit du côté −Z de son repère.
            GameObject signRoot = EditorBuildUtility.CreateEmpty("Enseigne", t, new Vector3(0f, h - 0.75f, d - 0.02f));
            float letters = Mathf.Clamp(w * 0.8f / Mathf.Max(4, sign.Length) / 0.72f, 0.18f, 0.42f);
            NeonTextBuilder.Build(signRoot.transform, Ascii(sign), letters, letters * 0.1f, kit.Neon);

            // --- le comptoir et le vendeur, puis les meubles du métier
            Counter(t, kind, w, d, kit, night, result);
            Furnish(t, kind, w, d, h, kit, night);

            root.SetActive(false);
            return result;
        }

        // ------------------------------------------------------------------ dimensions et teintes

        private static Vector3 Size(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Casino: return new Vector3(16f, 4.2f, 14f);
                case ShopKind.SalleDeBoxe: return new Vector3(14f, 4.5f, 14f);
                case ShopKind.StandDeTir: return new Vector3(10f, 3.4f, 18f);
                case ShopKind.Restaurant: return new Vector3(12f, 3.3f, 11f);
                case ShopKind.Bar: return new Vector3(11f, 3.2f, 10f);
                case ShopKind.Arcade: return new Vector3(11f, 3.2f, 11f);
                case ShopKind.Motel: return new Vector3(7f, 3f, 6f);
                case ShopKind.Avocat: return new Vector3(8f, 3.1f, 7f);
                default: return new Vector3(9f, 3.2f, 9f);
            }
        }

        private static Kit Palette(ShopKind kind, NightMaterialFactory.Palette night)
        {
            Kit k = new Kit
            {
                Floor = Mat("Carrelage", new Color(0.62f, 0.6f, 0.56f), 0.45f, 0f),
                Wall = Mat("Mur_Creme", new Color(0.78f, 0.74f, 0.66f), 0.1f, 0f),
                Ceiling = Mat("Plafond", new Color(0.86f, 0.86f, 0.84f), 0.05f, 0f),
                Trim = Mat("Plinthe", new Color(0.2f, 0.18f, 0.16f), 0.3f, 0f),
                Counter = Mat("Comptoir_Bois", new Color(0.36f, 0.22f, 0.12f), 0.35f, 0f),
                CounterTop = Mat("Comptoir_Plateau", new Color(0.12f, 0.12f, 0.13f), 0.7f, 0.1f),
                Accent = Mat("Accent_Rouge", new Color(0.6f, 0.08f, 0.08f), 0.3f, 0f),
                Neon = night.NeonWarm,
                Light = new Color(1f, 0.9f, 0.76f)
            };

            switch (kind)
            {
                case ShopKind.Casino:
                    k.Floor = Mat("Moquette_Casino", new Color(0.35f, 0.04f, 0.06f), 0.05f, 0f);
                    k.Wall = Mat("Mur_Casino", new Color(0.14f, 0.05f, 0.08f), 0.2f, 0f);
                    k.Neon = night.NeonMagenta;
                    k.Light = new Color(1f, 0.82f, 0.62f);
                    break;
                case ShopKind.Bar:
                case ShopKind.BoiteDeNuit:
                    k.Floor = Mat("Parquet_Sombre", new Color(0.2f, 0.13f, 0.08f), 0.4f, 0f);
                    k.Wall = Mat("Mur_Bar", new Color(0.24f, 0.14f, 0.1f), 0.1f, 0f);
                    k.Neon = night.NeonRed;
                    k.Light = new Color(1f, 0.7f, 0.45f);
                    break;
                case ShopKind.Arcade:
                    k.Floor = Mat("Moquette_Arcade", new Color(0.06f, 0.05f, 0.14f), 0.05f, 0f);
                    k.Wall = Mat("Mur_Arcade", new Color(0.05f, 0.05f, 0.08f), 0.2f, 0f);
                    k.Neon = night.NeonCyan;
                    k.Light = new Color(0.6f, 0.7f, 1f);
                    break;
                case ShopKind.Pharmacie:
                case ShopKind.Medecin:
                    k.Floor = Mat("Lino_Blanc", new Color(0.82f, 0.84f, 0.82f), 0.55f, 0f);
                    k.Wall = Mat("Mur_Blanc", new Color(0.9f, 0.92f, 0.9f), 0.1f, 0f);
                    k.Counter = Mat("Comptoir_Blanc", new Color(0.92f, 0.93f, 0.92f), 0.4f, 0f);
                    k.Neon = night.NeonGreen;
                    k.Light = new Color(0.92f, 0.97f, 1f);
                    break;
                case ShopKind.Restaurant:
                case ShopKind.Cafe:
                    k.Floor = Mat("Damier", new Color(0.3f, 0.28f, 0.26f), 0.5f, 0f);
                    k.Wall = Mat("Mur_Restaurant", new Color(0.62f, 0.34f, 0.22f), 0.1f, 0f);
                    k.Neon = night.NeonWarm;
                    break;
                case ShopKind.SalleDeBoxe:
                case ShopKind.StandDeTir:
                    k.Floor = Mat("Beton_Salle", new Color(0.36f, 0.36f, 0.37f), 0.2f, 0f);
                    k.Wall = Mat("Brique_Salle", new Color(0.42f, 0.24f, 0.18f), 0.05f, 0f);
                    k.Neon = night.NeonRed;
                    k.Light = new Color(1f, 0.95f, 0.85f);
                    break;
                case ShopKind.Vetements:
                    k.Floor = Mat("Parquet_Clair", new Color(0.66f, 0.5f, 0.34f), 0.4f, 0f);
                    k.Wall = Mat("Mur_Boutique", new Color(0.86f, 0.84f, 0.8f), 0.1f, 0f);
                    k.Neon = night.NeonWhite;
                    break;
                case ShopKind.Avocat:
                    k.Floor = Mat("Moquette_Bureau", new Color(0.18f, 0.2f, 0.26f), 0.05f, 0f);
                    k.Wall = Mat("Boiserie", new Color(0.32f, 0.2f, 0.12f), 0.3f, 0f);
                    k.Neon = night.NeonWhite;
                    break;
                case ShopKind.PreteurSurGages:
                case ShopKind.Cave:
                    k.Floor = Mat("Lino_Gris", new Color(0.4f, 0.4f, 0.38f), 0.3f, 0f);
                    k.Wall = Mat("Mur_Jaune", new Color(0.66f, 0.58f, 0.36f), 0.1f, 0f);
                    k.Neon = night.NeonBlue;
                    break;
            }

            return k;
        }

        // ------------------------------------------------------------------ comptoir

        private static void Counter(Transform t, ShopKind kind, float w, float d, Kit kit, NightMaterialFactory.Palette night, Result result)
        {
            // Par défaut : contre le mur du fond, le vendeur entre le comptoir et le mur.
            Vector3 counter = new Vector3(0f, 0f, d - 1.7f);
            float length = Mathf.Min(3.6f, w * 0.45f);
            float yaw = 0f;

            switch (kind)
            {
                case ShopKind.Casino:
                    counter = new Vector3(w * 0.5f - 1.6f, 0f, 2.6f);
                    yaw = 90f;
                    length = 3f;
                    break;
                case ShopKind.StandDeTir:
                    counter = new Vector3(0f, 0f, 3.2f);
                    length = 3.2f;
                    break;
                case ShopKind.SalleDeBoxe:
                case ShopKind.Arcade:
                    counter = new Vector3(-w * 0.5f + 1.6f, 0f, 2.6f);
                    yaw = -90f;
                    length = 2.6f;
                    break;
            }

            GameObject root = EditorBuildUtility.CreateEmpty("Comptoir", t, counter);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform c = root.transform;

            Box(c, "Caisson", new Vector3(0f, 0.5f, 0f), new Vector3(length, 1.0f, 0.62f), kit.Counter, true);
            Box(c, "Plateau", new Vector3(0f, 1.03f, -0.04f), new Vector3(length + 0.1f, 0.06f, 0.74f), kit.CounterTop, false);
            Box(c, "Caisse", new Vector3(length * 0.28f, 1.16f, 0.05f), new Vector3(0.36f, 0.2f, 0.32f), night.DarkMetal, false);
            Box(c, "Ecran de caisse", new Vector3(length * 0.28f, 1.36f, 0.12f), new Vector3(0.28f, 0.2f, 0.03f), night.GlowCyan, false);
            Box(c, "Terminal", new Vector3(-length * 0.22f, 1.1f, -0.2f), new Vector3(0.1f, 0.06f, 0.16f), night.Plastic, false);

            BoxCollider zone = root.AddComponent<BoxCollider>();
            zone.center = new Vector3(0f, 1.1f, -0.2f);
            zone.size = new Vector3(length, 1.2f, 1.2f);
            zone.isTrigger = true;
            result.Counter = c;

            // Le vendeur : derrière le comptoir (côté +Z local), tourné vers la salle (−Z local).
            result.ClerkPosition = c.TransformPoint(new Vector3(0f, 0f, 0.75f));
            result.ClerkRotation = c.rotation * Quaternion.Euler(0f, 180f, 0f);
        }

        // ------------------------------------------------------------------ meubles

        private static void Furnish(Transform t, ShopKind kind, float w, float d, float h, Kit kit, NightMaterialFactory.Palette night)
        {
            System.Random rng = new System.Random((int)kind * 97 + 13);
            Material[] products =
            {
                Mat("Produit_Rouge", new Color(0.7f, 0.12f, 0.1f), 0.3f, 0f),
                Mat("Produit_Jaune", new Color(0.9f, 0.72f, 0.12f), 0.3f, 0f),
                Mat("Produit_Bleu", new Color(0.12f, 0.3f, 0.7f), 0.3f, 0f),
                Mat("Produit_Vert", new Color(0.16f, 0.5f, 0.22f), 0.3f, 0f),
                Mat("Produit_Blanc", new Color(0.9f, 0.9f, 0.88f), 0.3f, 0f),
                Mat("Produit_Orange", new Color(0.92f, 0.45f, 0.1f), 0.3f, 0f)
            };
            Material shelf = Mat("Etagere", new Color(0.7f, 0.7f, 0.72f), 0.4f, 0.4f);

            switch (kind)
            {
                case ShopKind.Epicerie:
                case ShopKind.Cave:
                case ShopKind.Pharmacie:
                    // Trois allées de rayons, dans le sens de la profondeur.
                    for (int i = 0; i < 3; i++)
                    {
                        Shelves(t, new Vector3(-w * 0.5f + 2f + i * ((w - 4f) / 2f), 0f, d * 0.42f), 90f, d * 0.34f, 4, shelf, products, rng);
                    }

                    Shelves(t, new Vector3(-w * 0.5f + 0.35f, 0f, d * 0.5f), 90f, d * 0.6f, 5, shelf, products, rng);
                    if (kind == ShopKind.Cave) NightStreetBuilder.Bottles(t, night, new Vector3(1.2f, 1.08f, d - 1.8f), 9);
                    if (kind != ShopKind.Pharmacie)
                    {
                        // Les frigos, vitres éclairées.
                        for (int i = 0; i < 3; i++)
                        {
                            Vector3 p = new Vector3(w * 0.5f - 0.45f, 1.05f, 2.2f + i * 1.3f);
                            Box(t, "Frigo", p, new Vector3(0.8f, 2.1f, 1.2f), night.DarkMetal, true);
                            Box(t, "Vitre du frigo", p + new Vector3(-0.41f, 0f, 0f), new Vector3(0.02f, 1.8f, 1.0f), night.GlowWhite, false);
                        }
                    }
                    else
                    {
                        GameObject cross = EditorBuildUtility.CreateEmpty("Croix verte", t, new Vector3(-w * 0.5f + 0.02f, 2.2f, d - 2f));
                        cross.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                        Box(cross.transform, "Barre", Vector3.zero, new Vector3(0.7f, 0.22f, 0.04f), night.NeonGreen, false);
                        Box(cross.transform, "Barre 2", Vector3.zero, new Vector3(0.22f, 0.7f, 0.04f), night.NeonGreen, false);
                    }

                    break;

                case ShopKind.Restaurant:
                case ShopKind.Cafe:
                    for (int r = 0; r < 2; r++)
                    {
                        for (int c = 0; c < 3; c++)
                        {
                            Vector3 p = new Vector3(-w * 0.5f + 2f + c * ((w - 4f) / 2f), 0f, 3.2f + r * 2.8f);
                            if (p.z > d - 3.2f) continue;
                            Table(t, p, kit, night, products[(r * 3 + c) % products.Length]);
                        }
                    }

                    break;

                case ShopKind.Bar:
                case ShopKind.BoiteDeNuit:
                    Shelves(t, new Vector3(0f, 0f, d - 0.3f), 0f, w * 0.5f, 3, kit.Counter, products, rng);
                    NightStreetBuilder.Bottles(t, night, new Vector3(-0.6f, 1.08f, d - 1.8f), 8);
                    for (int i = 0; i < 5; i++)
                    {
                        Vector3 p = new Vector3(-1.6f + i * 0.8f, 0f, d - 2.6f);
                        NightStreetBuilder.Cylinder(t, "Tabouret", p + new Vector3(0f, 0.38f, 0f), new Vector3(0.08f, 0.38f, 0.08f), night.Chrome, false);
                        NightStreetBuilder.Cylinder(t, "Assise", p + new Vector3(0f, 0.78f, 0f), new Vector3(0.38f, 0.04f, 0.38f), night.Velvet, true);
                    }

                    Table(t, new Vector3(-w * 0.5f + 2f, 0f, 3f), kit, night, products[2]);
                    Table(t, new Vector3(w * 0.5f - 2f, 0f, 3f), kit, night, products[0]);
                    // Un billard.
                    Box(t, "Billard", new Vector3(0f, 0.78f, 3.4f), new Vector3(1.4f, 0.12f, 2.5f), Mat("Tapis_Vert", new Color(0.05f, 0.35f, 0.16f), 0.05f, 0f), true);
                    Box(t, "Billard (pied)", new Vector3(0f, 0.36f, 3.4f), new Vector3(1.2f, 0.72f, 2.2f), kit.Counter, true);
                    break;

                case ShopKind.Casino:
                    Material screen = night.GlowMagenta;
                    for (int i = 0; i < 6; i++)
                    {
                        Vector3 p = new Vector3(-w * 0.5f + 0.6f, 0f, 2.5f + i * 1.5f);
                        GameObject machine = EditorBuildUtility.CreateEmpty("Machine a sous", t, p);
                        machine.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                        Box(machine.transform, "Caisson", new Vector3(0f, 0.8f, 0f), new Vector3(0.8f, 1.6f, 0.7f), night.DarkMetal, true);
                        Box(machine.transform, "Ecran", new Vector3(0f, 1.15f, 0.36f), new Vector3(0.6f, 0.45f, 0.02f), i % 2 == 0 ? screen : night.GlowCyan, false);
                        Box(machine.transform, "Fronton", new Vector3(0f, 1.72f, 0.1f), new Vector3(0.8f, 0.24f, 0.5f), night.NeonWarm, false);
                        NightStreetBuilder.Cylinder(machine.transform, "Tabouret", new Vector3(0f, 0.35f, 0.8f), new Vector3(0.36f, 0.35f, 0.36f), night.Velvet, true);
                    }

                    // La roulette et la table de cartes.
                    Material felt = Mat("Tapis_Casino", new Color(0.04f, 0.3f, 0.14f), 0.05f, 0f);
                    Box(t, "Roulette (table)", new Vector3(0f, 0.8f, d * 0.55f), new Vector3(2.6f, 0.1f, 1.4f), felt, true);
                    Box(t, "Roulette (pied)", new Vector3(0f, 0.38f, d * 0.55f), new Vector3(2.2f, 0.76f, 1f), kit.Counter, true);
                    NightStreetBuilder.Cylinder(t, "Roulette (cylindre)", new Vector3(-0.8f, 0.9f, d * 0.55f), new Vector3(0.7f, 0.06f, 0.7f), night.Wood, false);
                    NightStreetBuilder.Cylinder(t, "Table de cartes", new Vector3(1.5f, 0.78f, d * 0.3f), new Vector3(1.8f, 0.05f, 1.8f), felt, true);
                    NightStreetBuilder.Cylinder(t, "Table de cartes (pied)", new Vector3(1.5f, 0.38f, d * 0.3f), new Vector3(0.4f, 0.38f, 0.4f), kit.Counter, true);
                    Box(t, "Tapis rouge", new Vector3(0f, 0.005f, d * 0.4f), new Vector3(2f, 0.01f, d * 0.7f), kit.Accent, false);
                    break;

                case ShopKind.Arcade:
                    Material[] screens = { night.GlowCyan, night.GlowMagenta, night.GlowWarm, night.GlowRed };
                    for (int i = 0; i < 8; i++)
                    {
                        bool left = i < 4;
                        Vector3 p = new Vector3(left ? -w * 0.5f + 0.55f : w * 0.5f - 0.55f, 0f, 4.4f + (i % 4) * 1.4f);
                        GameObject cab = EditorBuildUtility.CreateEmpty("Borne", t, p);
                        cab.transform.localRotation = Quaternion.Euler(0f, left ? 90f : -90f, 0f);
                        Box(cab.transform, "Caisson", new Vector3(0f, 0.9f, 0f), new Vector3(0.8f, 1.8f, 0.8f), products[i % products.Length], true);
                        Box(cab.transform, "Ecran", new Vector3(0f, 1.3f, 0.36f), new Vector3(0.6f, 0.45f, 0.1f), screens[i % screens.Length], false);
                        Box(cab.transform, "Pupitre", new Vector3(0f, 0.95f, 0.5f), new Vector3(0.7f, 0.08f, 0.3f), night.DarkMetal, false);
                        Box(cab.transform, "Fronton", new Vector3(0f, 1.9f, 0.2f), new Vector3(0.8f, 0.2f, 0.4f), screens[(i + 1) % screens.Length], false);
                    }

                    break;

                case ShopKind.StandDeTir:
                    for (int i = 0; i < 4; i++)
                    {
                        float x = -w * 0.5f + 1.25f + i * ((w - 2.5f) / 3f);
                        Box(t, "Cloison", new Vector3(x + 1f, 1.1f, 4.4f), new Vector3(0.06f, 2.2f, 1.6f), shelf, true);
                        Box(t, "Cible", new Vector3(x, 1.4f, d - 0.3f), new Vector3(0.6f, 0.8f, 0.02f), products[4], false);
                        Box(t, "Coeur de cible", new Vector3(x, 1.5f, d - 0.31f), new Vector3(0.18f, 0.18f, 0.02f), night.NeonRed, false);
                    }

                    Box(t, "Barriere", new Vector3(0f, 0.55f, 4.9f), new Vector3(w - 0.4f, 1.1f, 0.12f), kit.Counter, true);
                    break;

                case ShopKind.SalleDeBoxe:
                    // Le ring : estrade, poteaux, trois cordes.
                    Material rope = Mat("Corde", new Color(0.75f, 0.08f, 0.08f), 0.3f, 0f);
                    Vector3 ring = new Vector3(1.5f, 0f, d * 0.55f);
                    float half = 2.6f;
                    Box(t, "Ring", ring + new Vector3(0f, 0.3f, 0f), new Vector3(half * 2f, 0.6f, half * 2f), Mat("Toile_Ring", new Color(0.7f, 0.7f, 0.66f), 0.1f, 0f), true);
                    for (int i = 0; i < 4; i++)
                    {
                        float sx = i % 2 == 0 ? -1f : 1f, sz = i < 2 ? -1f : 1f;
                        NightStreetBuilder.Cylinder(t, "Poteau", ring + new Vector3(sx * half, 1.2f, sz * half), new Vector3(0.12f, 0.6f, 0.12f), night.Chrome, true);
                    }

                    for (int k = 0; k < 3; k++)
                    {
                        float y = 0.95f + k * 0.3f;
                        Box(t, "Corde", ring + new Vector3(0f, y, -half), new Vector3(half * 2f, 0.04f, 0.04f), rope, false);
                        Box(t, "Corde", ring + new Vector3(0f, y, half), new Vector3(half * 2f, 0.04f, 0.04f), rope, false);
                        Box(t, "Corde", ring + new Vector3(-half, y, 0f), new Vector3(0.04f, 0.04f, half * 2f), rope, false);
                        Box(t, "Corde", ring + new Vector3(half, y, 0f), new Vector3(0.04f, 0.04f, half * 2f), rope, false);
                    }

                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 p = new Vector3(-w * 0.5f + 1.2f, 0f, d - 1.6f - i * 2.2f);
                        NightStreetBuilder.Cylinder(t, "Sac de frappe", p + new Vector3(0f, 1.35f, 0f), new Vector3(0.42f, 0.55f, 0.42f), kit.Accent, true);
                        Box(t, "Chaine", p + new Vector3(0f, (h + 1.9f) * 0.5f, 0f), new Vector3(0.03f, h - 1.9f, 0.03f), night.Chrome, false);
                    }

                    Box(t, "Miroir", new Vector3(0f, 1.5f, d - 0.02f), new Vector3(w * 0.6f, 1.6f, 0.02f), night.Chrome, false);
                    break;

                case ShopKind.Vetements:
                    Material bar = night.Chrome;
                    for (int i = 0; i < 4; i++)
                    {
                        Vector3 p = new Vector3(-w * 0.5f + 1.8f + (i % 2) * (w - 3.6f), 0f, 2.8f + (i / 2) * 2.8f);
                        Box(t, "Portant", p + new Vector3(0f, 1.5f, 0f), new Vector3(1.6f, 0.03f, 0.03f), bar, false);
                        Box(t, "Pied", p + new Vector3(-0.78f, 0.75f, 0f), new Vector3(0.03f, 1.5f, 0.03f), bar, false);
                        Box(t, "Pied", p + new Vector3(0.78f, 0.75f, 0f), new Vector3(0.03f, 1.5f, 0.03f), bar, true);
                        for (int k = 0; k < 7; k++)
                        {
                            Box(t, "Vetement", p + new Vector3(-0.6f + k * 0.2f, 1.1f, 0f), new Vector3(0.06f, 0.75f, 0.5f),
                                products[rng.Next(products.Length)], false);
                        }
                    }

                    Box(t, "Miroir", new Vector3(w * 0.5f - 0.02f, 1.3f, d * 0.5f), new Vector3(0.02f, 1.8f, 0.8f), night.Chrome, false);
                    Box(t, "Cabine", new Vector3(-w * 0.5f + 0.8f, 1.1f, d - 1f), new Vector3(1.4f, 2.2f, 0.06f), kit.Accent, true);
                    break;

                case ShopKind.Medecin:
                    for (int i = 0; i < 5; i++) Chair(t, new Vector3(-w * 0.5f + 0.6f, 0f, 2f + i * 0.7f), 90f, night);
                    Box(t, "Table d'examen", new Vector3(w * 0.5f - 1.2f, 0.7f, d * 0.5f), new Vector3(0.8f, 0.12f, 2f), products[4], true);
                    Box(t, "Table d'examen (pied)", new Vector3(w * 0.5f - 1.2f, 0.32f, d * 0.5f), new Vector3(0.6f, 0.64f, 1.6f), night.Metal, true);
                    Box(t, "Paravent", new Vector3(w * 0.5f - 2.2f, 0.9f, d * 0.5f), new Vector3(0.04f, 1.8f, 2.2f), products[2], true);
                    break;

                case ShopKind.Avocat:
                    Box(t, "Bureau", new Vector3(0f, 0.75f, d * 0.55f), new Vector3(2f, 0.06f, 0.9f), kit.Counter, true);
                    Box(t, "Bureau (caisson)", new Vector3(0f, 0.36f, d * 0.55f), new Vector3(1.9f, 0.72f, 0.8f), kit.Counter, true);
                    Chair(t, new Vector3(-0.5f, 0f, d * 0.55f - 0.9f), 0f, night);
                    Chair(t, new Vector3(0.5f, 0f, d * 0.55f - 0.9f), 0f, night);
                    for (int i = 0; i < 2; i++)
                    {
                        Shelves(t, new Vector3(i == 0 ? -w * 0.5f + 0.3f : w * 0.5f - 0.3f, 0f, d * 0.5f), i == 0 ? 90f : -90f, 3f, 5, kit.Counter, products, rng);
                    }

                    break;

                case ShopKind.PreteurSurGages:
                    Material glass = night.Glass;
                    for (int i = 0; i < 3; i++)
                    {
                        Vector3 p = new Vector3(-w * 0.5f + 1.6f + i * 2.4f, 0f, 3.2f);
                        Box(t, "Vitrine", p + new Vector3(0f, 0.45f, 0f), new Vector3(1.8f, 0.9f, 0.7f), kit.Counter, true);
                        Box(t, "Vitrine (verre)", p + new Vector3(0f, 1.05f, 0f), new Vector3(1.8f, 0.3f, 0.7f), glass, false);
                        for (int k = 0; k < 4; k++)
                        {
                            Box(t, "Objet", p + new Vector3(-0.6f + k * 0.4f, 0.96f, 0f), new Vector3(0.14f, 0.1f, 0.14f), k % 2 == 0 ? night.Chrome : products[1], false);
                        }
                    }

                    for (int i = 0; i < 4; i++)
                    {
                        Box(t, "Guitare", new Vector3(w * 0.5f - 0.1f, 1.6f, 2.2f + i * 1f), new Vector3(0.08f, 1f, 0.36f), products[i % products.Length], false);
                    }

                    Box(t, "Television", new Vector3(-w * 0.5f + 0.4f, 1.3f, d * 0.5f), new Vector3(0.5f, 0.6f, 0.9f), night.DarkMetal, true);
                    break;

                case ShopKind.Motel:
                    Box(t, "Canape", new Vector3(-w * 0.5f + 0.6f, 0.35f, 2.2f), new Vector3(0.9f, 0.7f, 2f), kit.Accent, true);
                    Box(t, "Tableau des cles", new Vector3(0f, 1.8f, d - 0.03f), new Vector3(1.2f, 0.8f, 0.04f), kit.Counter, false);
                    Box(t, "Distributeur", new Vector3(w * 0.5f - 0.5f, 0.95f, 2.2f), new Vector3(0.8f, 1.9f, 0.8f), kit.Accent, true);
                    Box(t, "Distributeur (vitre)", new Vector3(w * 0.5f - 0.91f, 1.1f, 2.2f), new Vector3(0.02f, 1.2f, 0.6f), night.GlowWhite, false);
                    break;

                case ShopKind.Barbier:
                case ShopKind.Tatoueur:
                    for (int i = 0; i < 2; i++)
                    {
                        Vector3 p = new Vector3(-w * 0.5f + 1.2f, 0f, 3f + i * 2.2f);
                        Box(t, "Fauteuil", p + new Vector3(0f, 0.55f, 0f), new Vector3(0.7f, 0.3f, 0.7f), kit.Accent, true);
                        Box(t, "Dossier", p + new Vector3(0.3f, 0.95f, 0f), new Vector3(0.12f, 0.8f, 0.7f), kit.Accent, false);
                        Box(t, "Miroir", new Vector3(-w * 0.5f + 0.02f, 1.5f, p.z), new Vector3(0.02f, 1f, 0.8f), night.Chrome, false);
                    }

                    break;
            }

            // Une plante dans un coin, pour tout le monde.
            NightStreetBuilder.Cylinder(t, "Pot", new Vector3(w * 0.5f - 0.5f, 0.25f, 0.6f), new Vector3(0.4f, 0.25f, 0.4f), night.DarkConcrete, true);
            Box(t, "Plante", new Vector3(w * 0.5f - 0.5f, 0.85f, 0.6f), new Vector3(0.6f, 0.8f, 0.6f), Mat("Feuillage", new Color(0.12f, 0.36f, 0.14f), 0.2f, 0f), false);
        }

        private static void Shelves(Transform t, Vector3 position, float yaw, float length, int levels, Material frame, Material[] products,
            System.Random rng)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Rayonnage", t, position);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform s = root.transform;

            float height = 0.35f + levels * 0.38f;
            Box(s, "Montant", new Vector3(-length * 0.5f, height * 0.5f, 0f), new Vector3(0.05f, height, 0.5f), frame, true);
            Box(s, "Montant", new Vector3(length * 0.5f, height * 0.5f, 0f), new Vector3(0.05f, height, 0.5f), frame, true);
            Box(s, "Fond", new Vector3(0f, height * 0.5f, 0f), new Vector3(length, height, 0.03f), frame, true);

            for (int l = 0; l < levels; l++)
            {
                float y = 0.2f + l * 0.38f;
                Box(s, "Tablette", new Vector3(0f, y, 0f), new Vector3(length, 0.03f, 0.5f), frame, false);

                // Des produits des deux côtés, tailles et couleurs variées.
                for (float x = -length * 0.5f + 0.16f; x < length * 0.5f - 0.1f; x += 0.2f + (float)rng.NextDouble() * 0.08f)
                {
                    float ph = 0.14f + (float)rng.NextDouble() * 0.16f;
                    Material m = products[rng.Next(products.Length)];
                    Box(s, "Produit", new Vector3(x, y + 0.015f + ph * 0.5f, 0.13f), new Vector3(0.14f, ph, 0.18f), m, false);
                    Box(s, "Produit", new Vector3(x, y + 0.015f + ph * 0.5f, -0.13f), new Vector3(0.14f, ph, 0.18f), products[rng.Next(products.Length)], false);
                }
            }
        }

        private static void Table(Transform t, Vector3 position, Kit kit, NightMaterialFactory.Palette night, Material cloth)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Table", t, position);
            Transform s = root.transform;
            Box(s, "Plateau", new Vector3(0f, 0.75f, 0f), new Vector3(1f, 0.05f, 1f), kit.Counter, true);
            Box(s, "Pied", new Vector3(0f, 0.37f, 0f), new Vector3(0.1f, 0.74f, 0.1f), night.DarkMetal, true);
            Box(s, "Nappe", new Vector3(0f, 0.78f, 0f), new Vector3(0.6f, 0.01f, 0.6f), cloth, false);
            NightStreetBuilder.Cylinder(s, "Assiette", new Vector3(0.2f, 0.79f, 0.2f), new Vector3(0.24f, 0.01f, 0.24f), night.Plastic, false);
            NightStreetBuilder.Cylinder(s, "Verre", new Vector3(-0.22f, 0.83f, 0.1f), new Vector3(0.07f, 0.06f, 0.07f), night.Glass, false);
            for (int i = 0; i < 4; i++)
            {
                float a = i * 90f;
                Vector3 p = Quaternion.Euler(0f, a, 0f) * new Vector3(0f, 0f, 0.8f);
                Chair(s, p, a + 180f, night);
            }
        }

        private static void Chair(Transform t, Vector3 position, float yaw, NightMaterialFactory.Palette night)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Chaise", t, position);
            root.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform s = root.transform;
            Box(s, "Assise", new Vector3(0f, 0.45f, 0f), new Vector3(0.44f, 0.05f, 0.44f), night.Wood, true);
            Box(s, "Dossier", new Vector3(0f, 0.72f, -0.2f), new Vector3(0.44f, 0.5f, 0.04f), night.Wood, false);
            for (int i = 0; i < 4; i++)
            {
                float sx = i % 2 == 0 ? -0.19f : 0.19f, sz = i < 2 ? -0.19f : 0.19f;
                Box(s, "Pied", new Vector3(sx, 0.22f, sz), new Vector3(0.04f, 0.44f, 0.04f), night.DarkMetal, false);
            }
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider)
        {
            return NightStreetBuilder.Box(parent, name, position, size, material, collider);
        }

        /// <summary>L'enseigne au néon n'a que des majuscules sans accent.</summary>
        private static string Ascii(string text)
        {
            string normalized = text.ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '-' || c == '.' || c == '!') b.Append(c);
                else if (c == '\'' || c == '’') b.Append(' ');
            }

            return b.ToString();
        }
    }
}
