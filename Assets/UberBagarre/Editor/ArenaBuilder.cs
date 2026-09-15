using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Construit l'arène : une ruelle, pas une boîte en cubes.
    ///
    /// Un décor n'a pas qu'un rôle esthétique dans un jeu de combat. Il donne les repères qui
    /// permettent de juger une distance et une vitesse : sans verticales proches (poteaux,
    /// grillage, façades), on ne perçoit ni son propre déplacement ni celui de l'adversaire.
    /// Le cercle peint au sol sert exactement à ça — il dit où se tient le combat.
    ///
    /// Tout reste fait de formes simples, mais composées : ce qui change la lecture, ce sont
    /// les textures générées, les hauteurs variées et les deux vraies lumières.
    /// </summary>
    public static class ArenaBuilder
    {
        private const string MaterialsFolder = "Assets/UberBagarre/Art/Materials";
        private const string TexturesFolder = "Assets/UberBagarre/Art/Textures";

        public class Palette
        {
            public Material Asphalt;
            public Material Brick;
            public Material Concrete;
            public Material Metal;
            public Material DarkMetal;
            public Material Wood;
            public Material Paint;
            public Material Window;
            public Material Rust;
        }

        public static Palette CreatePalette(float arenaSize)
        {
            Texture2D asphalt = EditorBuildUtility.CreateOrUpdateAsphaltTexture(TexturesFolder, "T_Asphalte", 256);
            Texture2D brick = EditorBuildUtility.CreateOrUpdateBrickTexture(TexturesFolder, "T_Brique", 256, 14,
                new Color(0.38f, 0.36f, 0.34f), new Color(0.46f, 0.27f, 0.22f));

            Palette p = new Palette();

            p.Asphalt = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Asphalte",
                Color.white, 0.08f, 0f, asphalt, new Vector2(arenaSize * 0.35f, arenaSize * 0.35f));

            p.Brick = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Brique",
                Color.white, 0.05f, 0f, brick, new Vector2(6f, 4f));

            p.Concrete = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Beton",
                new Color(0.44f, 0.43f, 0.41f), 0.08f, 0f);

            p.Metal = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Metal",
                new Color(0.52f, 0.54f, 0.57f), 0.55f, 0.8f);

            p.DarkMetal = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_MetalSombre",
                new Color(0.16f, 0.17f, 0.19f), 0.42f, 0.7f);

            p.Wood = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Bois",
                new Color(0.45f, 0.33f, 0.21f), 0.12f, 0f);

            p.Paint = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_PeintureSol",
                new Color(0.86f, 0.74f, 0.18f), 0.20f, 0f);

            p.Window = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Fenetre",
                new Color(0.09f, 0.11f, 0.14f), 0.75f, 0.3f);

            p.Rust = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Rouille",
                new Color(0.34f, 0.20f, 0.13f), 0.15f, 0.2f);

            return p;
        }

        /// <summary>
        /// Ajoute l'arène de jour à la scène OUVERTE, sans rien effacer.
        ///
        /// La scène de test est désormais une rue de nuit : c'est le décor du premier combat
        /// décrit dans le dossier, et c'est aussi celui qui montre ce que fait la chaîne de
        /// rendu. Mais une rue de nuit coûte cher — reflet planaire, trentaine de lampes,
        /// bruine. Cette arène reste donc accessible : elle sert de repli pour régler le
        /// ressenti d'un coup sur une machine modeste, en pleine lumière, sans rien qui
        /// distraie.
        /// </summary>
        [MenuItem("Uber Bagarre/Outils/Ajouter l'arene de jour a la scene ouverte", false, 120)]
        public static void BuildDayArenaFromMenu()
        {
            Build(26f, 3.4f);
            Debug.Log("[UberBagarre] Arene de jour ajoutee a la scene ouverte. " +
                      "Pense a regler l'heure sur 1 dans le menu graphique (Tab) ou sur le composant " +
                      "TimeOfDay, sinon elle restera eclairee comme une rue de nuit.");
        }

        public static void Build(float arenaSize, float ringRadius)
        {
            Palette palette = CreatePalette(arenaSize);
            GameObject root = new GameObject("=== Arene ===");

            BuildGround(root.transform, palette, arenaSize);
            BuildCombatRing(root.transform, palette, ringRadius);
            BuildFacades(root.transform, palette, arenaSize);
            BuildFences(root.transform, palette, arenaSize);
            BuildStreetLamps(root.transform, palette, arenaSize);
            BuildProps(root.transform, palette);
        }

        // ------------------------------------------------------------------ sol

        private static void BuildGround(Transform parent, Palette palette, float size)
        {
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Bitume", parent,
                new Vector3(0f, -0.25f, 0f), new Vector3(size, 0.5f, size), palette.Asphalt, true);

            // Trottoir : une bordure surelevee tout autour. Elle cadre l'espace de combat et
            // sert de repere de distance quand on recule.
            float half = size * 0.5f;
            float kerb = 1.8f;

            Kerb(parent, palette, new Vector3(0f, 0.07f, half - kerb * 0.5f), new Vector3(size, 0.14f, kerb));
            Kerb(parent, palette, new Vector3(0f, 0.07f, -half + kerb * 0.5f), new Vector3(size, 0.14f, kerb));
            Kerb(parent, palette, new Vector3(half - kerb * 0.5f, 0.07f, 0f), new Vector3(kerb, 0.14f, size - kerb * 2f));
            Kerb(parent, palette, new Vector3(-half + kerb * 0.5f, 0.07f, 0f), new Vector3(kerb, 0.14f, size - kerb * 2f));
        }

        private static void Kerb(Transform parent, Palette palette, Vector3 position, Vector3 scale)
        {
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Trottoir", parent, position, scale,
                palette.Concrete, true);
        }

        /// <summary>
        /// Cercle peint au sol, fait de segments. Il indique l'aire de combat, et surtout il
        /// donne une référence de distance : on sait d'un coup d'œil si l'adversaire est à
        /// portée de poing ou non.
        /// </summary>
        private static void BuildCombatRing(Transform parent, Palette palette, float radius)
        {
            GameObject ring = EditorBuildUtility.CreateEmpty("CercleDeCombat", parent, Vector3.zero);

            const int segments = 56;
            float segmentLength = 2f * Mathf.PI * radius / segments * 1.12f;

            for (int i = 0; i < segments; i++)
            {
                // Un segment sur quatre est omis : une ligne pointillee lit mieux qu'un trait
                // continu, et evite le "cercle parfait" qui fait tout de suite synthetique.
                if (i % 4 == 3) continue;

                float angle = Mathf.PI * 2f * i / segments;
                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.012f, Mathf.Sin(angle) * radius);

                GameObject segment = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Segment",
                    ring.transform, position, new Vector3(0.12f, 0.02f, segmentLength), palette.Paint, false);

                segment.transform.localRotation = Quaternion.Euler(0f, -angle * Mathf.Rad2Deg, 0f);
            }
        }

        // ------------------------------------------------------------------ façades

        private static void BuildFacades(Transform parent, Palette palette, float size)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Facades", parent, Vector3.zero);
            float half = size * 0.5f;

            Facade(root.transform, palette, new Vector3(0f, 0f, half), new Vector3(size, 9f, 1.2f), 0f, 5);
            Facade(root.transform, palette, new Vector3(0f, 0f, -half), new Vector3(size, 7.5f, 1.2f), 180f, 4);
            Facade(root.transform, palette, new Vector3(half, 0f, 0f), new Vector3(size, 11f, 1.2f), 90f, 6);
            Facade(root.transform, palette, new Vector3(-half, 0f, 0f), new Vector3(size, 8f, 1.2f), 270f, 4);
        }

        /// <summary>Mur de brique avec des bandeaux de fenêtres. Les hauteurs varient pour éviter la boîte.</summary>
        private static void Facade(Transform parent, Palette palette, Vector3 position, Vector3 size,
            float yaw, int floors)
        {
            GameObject facade = EditorBuildUtility.CreateEmpty("Facade", parent, position);
            facade.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Mur", facade.transform,
                new Vector3(0f, size.y * 0.5f, 0f), new Vector3(size.x, size.y, size.z), palette.Brick, true);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Corniche", facade.transform,
                new Vector3(0f, size.y + 0.15f, -0.1f), new Vector3(size.x, 0.3f, size.z + 0.4f),
                palette.Concrete, false);

            for (int floor = 1; floor <= floors; floor++)
            {
                float y = 1.6f + floor * 1.55f;
                if (y > size.y - 1f) break;

                EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Fenetres", facade.transform,
                    new Vector3(0f, y, -size.z * 0.5f - 0.03f),
                    new Vector3(size.x * 0.82f, 0.85f, 0.1f), palette.Window, false);

                EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Appui", facade.transform,
                    new Vector3(0f, y - 0.5f, -size.z * 0.5f - 0.08f),
                    new Vector3(size.x * 0.84f, 0.1f, 0.24f), palette.Concrete, false);
            }
        }

        // ------------------------------------------------------------------ grillage

        private static void BuildFences(Transform parent, Palette palette, float size)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Grillages", parent, Vector3.zero);
            float half = size * 0.5f - 2.6f;

            FenceLine(root.transform, palette, new Vector3(-half, 0f, half * 0.55f), 90f, 7);
            FenceLine(root.transform, palette, new Vector3(half, 0f, -half * 0.55f), 90f, 6);
        }

        private static void FenceLine(Transform parent, Palette palette, Vector3 origin, float yaw, int panels)
        {
            GameObject line = EditorBuildUtility.CreateEmpty("Grillage", parent, origin);
            line.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            const float panelWidth = 2.2f;
            const float height = 2.4f;

            for (int i = 0; i <= panels; i++)
            {
                EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Poteau", line.transform,
                    new Vector3(0f, height * 0.5f, i * panelWidth), new Vector3(0.07f, height * 0.5f, 0.07f),
                    palette.Metal, true);
            }

            // Trois lisses horizontales : suffisant pour lire un grillage, sans le cout d'une
            // vraie maille (qui demanderait une texture transparente, donc un shader different
            // selon le render pipeline).
            for (int rail = 0; rail < 3; rail++)
            {
                float y = 0.35f + rail * 0.95f;

                EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Lisse", line.transform,
                    new Vector3(0f, y, panels * panelWidth * 0.5f),
                    new Vector3(0.05f, 0.05f, panels * panelWidth), palette.Metal, false);
            }
        }

        // ------------------------------------------------------------------ lampadaires

        private static void BuildStreetLamps(Transform parent, Palette palette, float size)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Lampadaires", parent, Vector3.zero);
            float offset = size * 0.5f - 3.2f;

            StreetLamp(root.transform, palette, new Vector3(-offset, 0f, offset * 0.4f), 35f);
            StreetLamp(root.transform, palette, new Vector3(offset, 0f, -offset * 0.4f), -145f);
        }

        private static void StreetLamp(Transform parent, Palette palette, Vector3 position, float yaw)
        {
            GameObject lamp = EditorBuildUtility.CreateEmpty("Lampadaire", parent, position);
            lamp.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Socle", lamp.transform,
                new Vector3(0f, 0.12f, 0f), new Vector3(0.32f, 0.12f, 0.32f), palette.Concrete, true);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Mat", lamp.transform,
                new Vector3(0f, 2.6f, 0f), new Vector3(0.11f, 2.6f, 0.11f), palette.DarkMetal, true);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Potence", lamp.transform,
                new Vector3(0f, 5.15f, 0.45f), new Vector3(0.09f, 0.09f, 1.1f), palette.DarkMetal, false);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Lanterne", lamp.transform,
                new Vector3(0f, 5.0f, 0.95f), new Vector3(0.34f, 0.16f, 0.5f), palette.Metal, false);

            // Une VRAIE lumiere : c'est elle qui cree les ombres portees au sol, et donc la
            // profondeur. Sans ombre, un combattant semble flotter.
            GameObject lightGo = EditorBuildUtility.CreateEmpty("Lumiere", lamp.transform,
                new Vector3(0f, 4.85f, 0.95f));

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 15f;
            light.intensity = 3.2f;
            light.color = new Color(1f, 0.86f, 0.62f);
            light.shadows = LightShadows.Soft;
        }

        // ------------------------------------------------------------------ accessoires

        private static void BuildProps(Transform parent, Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Accessoires", parent, Vector3.zero);

            Dumpster(root.transform, palette, new Vector3(-6.4f, 0f, -5.8f), 18f);
            Dumpster(root.transform, palette, new Vector3(7.2f, 0f, 6.4f), -105f);

            Barrel(root.transform, palette, new Vector3(5.9f, 0f, -7.1f), 0f);
            Barrel(root.transform, palette, new Vector3(6.6f, 0f, -6.5f), 24f);
            Barrel(root.transform, palette, new Vector3(-8.1f, 0f, 4.2f), -12f);

            CrateStack(root.transform, palette, new Vector3(-7.8f, 0f, 7.6f));
            CrateStack(root.transform, palette, new Vector3(8.4f, 0f, -3.2f));

            Pallet(root.transform, palette, new Vector3(-4.9f, 0f, 8.4f), 22f);
            Pallet(root.transform, palette, new Vector3(9.1f, 0f, 2.4f), -40f);
        }

        private static void Dumpster(Transform parent, Palette palette, Vector3 position, float yaw)
        {
            GameObject dumpster = EditorBuildUtility.CreateEmpty("Benne", parent, position);
            dumpster.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Cuve", dumpster.transform,
                new Vector3(0f, 0.55f, 0f), new Vector3(2.1f, 1.1f, 1.15f), palette.Rust, true);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Couvercle", dumpster.transform,
                new Vector3(0f, 1.16f, -0.1f), new Vector3(2.15f, 0.12f, 1.25f), palette.DarkMetal, false);

            for (int i = -1; i <= 1; i += 2)
            {
                EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Roue", dumpster.transform,
                    new Vector3(i * 0.85f, 0.1f, 0.45f), new Vector3(0.2f, 0.06f, 0.2f), palette.DarkMetal, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static void Barrel(Transform parent, Palette palette, Vector3 position, float yaw)
        {
            GameObject barrel = EditorBuildUtility.CreateEmpty("Baril", parent, position);
            barrel.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Corps", barrel.transform,
                new Vector3(0f, 0.44f, 0f), new Vector3(0.56f, 0.44f, 0.56f), palette.Rust, true);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Cerclage", barrel.transform,
                new Vector3(0f, 0.62f, 0f), new Vector3(0.60f, 0.04f, 0.60f), palette.DarkMetal, false);
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Cerclage", barrel.transform,
                new Vector3(0f, 0.26f, 0f), new Vector3(0.60f, 0.04f, 0.60f), palette.DarkMetal, false);
        }

        private static void CrateStack(Transform parent, Palette palette, Vector3 position)
        {
            GameObject stack = EditorBuildUtility.CreateEmpty("Caisses", parent, position);

            Crate(stack.transform, palette, new Vector3(0f, 0.36f, 0f), 0.72f, 7f);
            Crate(stack.transform, palette, new Vector3(0.78f, 0.30f, 0.2f), 0.60f, -22f);
            Crate(stack.transform, palette, new Vector3(0.1f, 1.05f, -0.08f), 0.65f, 31f);
        }

        private static void Crate(Transform parent, Palette palette, Vector3 position, float size, float yaw)
        {
            GameObject crate = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Caisse", parent,
                position, Vector3.one * size, palette.Wood, true);

            crate.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
        }

        private static void Pallet(Transform parent, Palette palette, Vector3 position, float yaw)
        {
            GameObject pallet = EditorBuildUtility.CreateEmpty("Palette", parent, position);
            pallet.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            for (int i = 0; i < 4; i++)
            {
                EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Planche", pallet.transform,
                    new Vector3(0f, 0.08f, -0.45f + i * 0.3f), new Vector3(1.2f, 0.05f, 0.2f), palette.Wood, false);
            }

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Traverse", pallet.transform,
                new Vector3(0f, 0.03f, 0f), new Vector3(1.2f, 0.06f, 1.2f), palette.Wood, true);
        }
    }
}
