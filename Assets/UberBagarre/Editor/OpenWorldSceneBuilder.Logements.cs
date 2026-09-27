using System.Collections.Generic;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Ce que le monde ouvert sur la ville ajoute autour du joueur : ses logements (la chambre
    /// du motel, le bungalow, le manoir — les mêmes meubles, posés dans la bonne pièce), le
    /// vestiaire et l'ordinateur qui s'y ouvrent, sa tenue, ses entraînements, et les
    /// accessoires que tiennent les cibles.
    /// </summary>
    public static partial class OpenWorldSceneBuilder
    {
        private const string AccessoriesFolder = NightMaterialFactory.MaterialsFolder + "/Accessoires";

        /// <summary>Un logement construit : où l'on se réveille, et ses meubles utiles.</summary>
        private sealed class HomeKit
        {
            public string Name;
            public Transform Root;
            public Transform Arrival;
            public GameObject Furniture;
            public Interactable Bed;
            public Interactable Computer;
            public Interactable Wardrobe;
            public Interactable Letters;
            public Interactable Fridge;
            public Interactable Medkit;
            public string[] Doors = new string[0];
            public string[] OpenForOwner = new string[0];
        }

        /// <summary>Les points d'un logement, qu'il vienne de la planque ou d'un bien à vendre.</summary>
        private struct HomeLayout
        {
            public string Name;
            public string Title;
            public float[] Inside;
            public float[] Bed;
            public float[] Desk;
            public float[] Wardrobe;
            public float[] Letters;
            public float BedWidth;
            public string[] Doors;
            public string[] Open;
        }

        // ------------------------------------------------------------------ logements

        private static List<HomeKit> BuildHomes(MapPack.Data map, NightMaterialFactory.Palette night, Transform parent)
        {
            HouseBuilder.Palette house = HouseBuilder.CreatePalette();
            Material screen = NightMaterialFactory.CreateEmissive(NightMaterialFactory.MaterialsFolder, "M_EcranOrdinateur",
                new Color(0.05f, 0.08f, 0.12f), null, Vector2.one, new Color(0.35f, 0.62f, 1f) * 1.6f, null, 0.8f, 0f);

            List<HomeKit> homes = new List<HomeKit>();

            MapPack.Home motel = map.home;
            homes.Add(BuildHome(new HomeLayout
            {
                Name = "Motel",
                Title = "Planque (chambre du motel)",
                Inside = motel.inside,
                Bed = motel.bed,
                Desk = motel.desk,
                Wardrobe = motel.wardrobe,
                Letters = motel.letters,
                BedWidth = 1.45f,
                Doors = new string[0],
                Open = new string[0]
            }, night, house, screen, parent));

            if (map.properties != null)
            {
                for (int i = 0; i < map.properties.Length; i++)
                {
                    MapPack.Property p = map.properties[i];
                    if (p == null || p.bed == null || p.desk == null || p.wardrobe == null || p.inside == null) continue;

                    homes.Add(BuildHome(new HomeLayout
                    {
                        Name = p.name,
                        Title = "Logement (" + p.name + ")",
                        Inside = p.inside,
                        Bed = p.bed,
                        Desk = p.desk,
                        Wardrobe = p.wardrobe,
                        Letters = p.letters,
                        BedWidth = p.bedWidth > 0.5f ? p.bedWidth : 1.45f,
                        Doors = p.doors ?? new string[0],
                        Open = p.open ?? new string[0]
                    }, night, house, screen, parent));
                }
            }

            return homes;
        }

        private static HomeKit BuildHome(HomeLayout layout, NightMaterialFactory.Palette night, HouseBuilder.Palette house,
            Material screen, Transform parent)
        {
            HomeKit home = new HomeKit { Name = layout.Name, Doors = layout.Doors, OpenForOwner = layout.Open };
            GameObject root = EditorBuildUtility.CreateEmpty(layout.Title, parent, Vector3.zero);
            home.Root = root.transform;

            GameObject arrival = EditorBuildUtility.CreateEmpty("Reveil", root.transform, MapPack.Position(layout.Inside));
            arrival.transform.rotation = Quaternion.Euler(0f, MapPack.Yaw(layout.Inside), 0f);
            home.Arrival = arrival.transform;

            GameObject furniture = EditorBuildUtility.CreateEmpty("Meubles", root.transform, Vector3.zero);
            home.Furniture = furniture;
            Transform f = furniture.transform;

            // --- le lit (tête de lit sur son +z local)
            float w = layout.BedWidth;
            GameObject bed = Anchor("Lit", f, layout.Bed);
            Box(bed.transform, "Cadre", new Vector3(0f, 0.16f, 0f), new Vector3(w, 0.32f, 2.05f), night.Wood, true);
            Box(bed.transform, "Matelas", new Vector3(0f, 0.4f, 0f), new Vector3(w - 0.1f, 0.18f, 1.95f), house.Mattress, false);
            Box(bed.transform, "Couverture", new Vector3(0.03f, 0.5f, -0.3f), new Vector3(w - 0.07f, 0.06f, 1.3f), house.Blanket, false)
                .transform.localRotation = Quaternion.Euler(0f, 3f, 0f);

            int pillows = w > 1.8f ? 2 : 1;
            for (int i = 0; i < pillows; i++)
            {
                float x = pillows == 1 ? 0f : (i == 0 ? -0.42f : 0.42f);
                Box(bed.transform, "Oreiller", new Vector3(x, 0.55f, 0.74f), new Vector3(0.62f, 0.13f, 0.34f), house.Mattress, false);
            }

            Box(bed.transform, "Tete de lit", new Vector3(0f, 0.55f, 1.02f), new Vector3(w, 0.8f, 0.06f), night.Wood, true);
            home.Bed = MakeInteractable(bed, "Dormir", "Sauvegarder, soigner les blessures", new Vector3(w, 0.7f, 2.05f),
                new Vector3(0f, 0.35f, 0f), 2.4f);

            // --- le bureau, la chaise, l'ordinateur, la lampe (dos au mur sur son +z local)
            GameObject desk = Anchor("Bureau", f, layout.Desk);
            Box(desk.transform, "Plateau", new Vector3(0f, 0.74f, 0f), new Vector3(1.25f, 0.04f, 0.6f), house.Laminate, true);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.58f : 0.58f;
                float z = i < 2 ? -0.26f : 0.26f;
                Box(desk.transform, "Pied", new Vector3(x, 0.36f, z), new Vector3(0.04f, 0.72f, 0.04f), night.DarkMetal, false);
            }

            GameObject chair = EditorBuildUtility.CreateEmpty("Chaise", desk.transform, new Vector3(0.05f, 0f, -0.62f));
            chair.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            Box(chair.transform, "Assise", new Vector3(0f, 0.46f, 0f), new Vector3(0.44f, 0.05f, 0.44f), night.Plastic, true);
            Box(chair.transform, "Dossier", new Vector3(0f, 0.76f, -0.2f), new Vector3(0.44f, 0.5f, 0.04f), night.Plastic, false);
            for (int i = 0; i < 4; i++)
            {
                Box(chair.transform, "Pied", new Vector3(i % 2 == 0 ? -0.19f : 0.19f, 0.22f, i < 2 ? -0.19f : 0.19f),
                    new Vector3(0.03f, 0.44f, 0.03f), night.DarkMetal, false);
            }

            GameObject laptop = EditorBuildUtility.CreateEmpty("Ordinateur portable", desk.transform, new Vector3(0.05f, 0.76f, 0.02f));
            Box(laptop.transform, "Base", new Vector3(0f, 0.012f, 0f), new Vector3(0.36f, 0.024f, 0.25f), night.DarkMetal, false);
            GameObject lid = EditorBuildUtility.CreateEmpty("Couvercle", laptop.transform, new Vector3(0f, 0.024f, 0.12f));
            lid.transform.localRotation = Quaternion.Euler(-75f, 0f, 0f);
            Box(lid.transform, "Coque", new Vector3(0f, 0f, 0.12f), new Vector3(0.36f, 0.012f, 0.24f), night.DarkMetal, false);
            Box(lid.transform, "Ecran", new Vector3(0f, 0.007f, 0.12f), new Vector3(0.33f, 0.002f, 0.21f), screen, false);
            NightStreetBuilder.AddLight(laptop.transform, "Lueur de l'ecran", new Vector3(0f, 0.25f, -0.25f),
                new Color(0.45f, 0.65f, 1f), 0.5f, 2.6f, false, false);
            home.Computer = MakeInteractable(laptop, "Allumer l'ordinateur", "Casino, paris, salle de sport, immobilier",
                new Vector3(0.45f, 0.35f, 0.4f), new Vector3(0f, 0.12f, 0f), 2.2f);

            GameObject lamp = EditorBuildUtility.CreateEmpty("Lampe de bureau", desk.transform, new Vector3(0.5f, 0.76f, 0.16f));
            Cylinder(lamp.transform, "Pied", new Vector3(0f, 0.01f, 0f), new Vector3(0.14f, 0.01f, 0.14f), night.DarkMetal, false);
            Cylinder(lamp.transform, "Tige", new Vector3(0f, 0.22f, 0f), new Vector3(0.02f, 0.22f, 0.02f), night.Chrome, false);
            Cylinder(lamp.transform, "Abat-jour", new Vector3(0f, 0.44f, 0f), new Vector3(0.18f, 0.07f, 0.18f), night.NeonWarm, false);
            NightStreetBuilder.AddLight(lamp.transform, "Lumiere", new Vector3(0f, 0.38f, 0f), new Color(1f, 0.78f, 0.52f),
                1.25f, 6f, true, false);

            // --- le mini-frigo à côté du bureau, et la trousse de soins au mur au-dessus
            GameObject fridge = EditorBuildUtility.CreateEmpty("Mini-frigo", desk.transform, new Vector3(1.02f, 0f, 0.02f));
            Box(fridge.transform, "Caisson", new Vector3(0f, 0.43f, 0f), new Vector3(0.52f, 0.86f, 0.52f), night.Metal, true);
            Box(fridge.transform, "Porte", new Vector3(0f, 0.45f, -0.265f), new Vector3(0.5f, 0.78f, 0.02f), house.Porcelain, false);
            Box(fridge.transform, "Poignee", new Vector3(0.19f, 0.55f, -0.285f), new Vector3(0.025f, 0.26f, 0.025f), night.Chrome, false);
            Box(fridge.transform, "Magnet", new Vector3(-0.1f, 0.68f, -0.277f), new Vector3(0.08f, 0.1f, 0.006f), night.NeonWarm, false);
            home.Fridge = MakeInteractable(fridge, "Manger (frigo)", "Les repas achetés à l'épicerie", new Vector3(0.6f, 0.9f, 0.6f),
                new Vector3(0f, 0.45f, 0f), 2.2f);

            GameObject kit = EditorBuildUtility.CreateEmpty("Trousse de soins", desk.transform, new Vector3(1.02f, 1.42f, 0.24f));
            Material red = EditorBuildUtility.CreateOrUpdateMaterial(NightMaterialFactory.MaterialsFolder, "M_TrousseRouge",
                new Color(0.72f, 0.06f, 0.06f), 0.45f, 0f);
            Box(kit.transform, "Boite", Vector3.zero, new Vector3(0.36f, 0.28f, 0.1f), red, false);
            Box(kit.transform, "Croix (verticale)", new Vector3(0f, 0f, -0.052f), new Vector3(0.05f, 0.17f, 0.004f), house.Paper, false);
            Box(kit.transform, "Croix (horizontale)", new Vector3(0f, 0f, -0.052f), new Vector3(0.17f, 0.05f, 0.004f), house.Paper, false);
            home.Medkit = MakeInteractable(kit, "Se soigner (trousse)", "Avec une trousse achetée à la pharmacie", new Vector3(0.45f, 0.4f, 0.4f),
                new Vector3(0f, 0f, -0.1f), 2.2f);

            // --- le courrier, sur le bureau
            if (layout.Letters != null && layout.Letters.Length >= 3)
            {
                GameObject letters = EditorBuildUtility.CreateEmpty("Courrier", f, MapPack.Position(layout.Letters));
                letters.transform.rotation = Quaternion.Euler(0f, MapPack.Yaw(layout.Letters), 0f);
                float[] yaws = { 4f, -13f, 9f };
                for (int i = 0; i < 3; i++)
                {
                    GameObject envelope = Box(letters.transform, "Enveloppe", new Vector3(i * 0.045f, i * 0.006f, i * 0.02f),
                        new Vector3(0.23f, 0.004f, 0.115f), house.Paper, false);
                    envelope.transform.localRotation = Quaternion.Euler(0f, yaws[i], 0f);
                }

                home.Letters = MakeInteractable(letters, "Lire le courrier", "Des relances, encore", new Vector3(0.44f, 0.12f, 0.24f),
                    new Vector3(0.05f, 0.02f, 0.02f), 2.2f);
            }

            // --- l'armoire et son miroir (façade sur son +z local)
            GameObject wardrobe = Anchor("Armoire", f, layout.Wardrobe);
            Box(wardrobe.transform, "Caisson", new Vector3(0f, 1f, 0f), new Vector3(1.15f, 2f, 0.55f), night.Wood, true);
            Box(wardrobe.transform, "Miroir", new Vector3(0.28f, 1.1f, 0.281f), new Vector3(0.48f, 1.5f, 0.01f), night.Chrome, false);
            Box(wardrobe.transform, "Poignee", new Vector3(-0.05f, 1.05f, 0.29f), new Vector3(0.02f, 0.18f, 0.02f), night.Chrome, false);
            home.Wardrobe = MakeInteractable(wardrobe, "Se changer", "Tenue, entraînement", new Vector3(1.2f, 2f, 0.7f),
                new Vector3(0f, 1f, 0.1f), 2.4f);

            // Un tapis au pied du lit : la pièce a l'air habitée.
            if (w > 1.8f)
            {
                GameObject rug = Box(bed.transform, "Tapis", new Vector3(0f, 0.006f, -1.6f), new Vector3(2.2f, 0.012f, 1.4f),
                    house.Blanket, false);
                rug.GetComponent<Renderer>().shadowCastingMode = ShadowCastingMode.Off;
            }

            return home;
        }

        private static HomeRegistry BuildHomeRegistry(GameObject systems, List<HomeKit> homes, PlayerProgress progress,
            OpenWorldDirector director, WardrobeScreen wardrobe, ComputerScreen computer)
        {
            HomeRegistry registry = systems.AddComponent<HomeRegistry>();
            SerializedWiring.SetObject(registry, "_progress", progress);
            SerializedWiring.SetObject(registry, "_director", director);
            SerializedWiring.SetObject(registry, "_wardrobeScreen", wardrobe);
            SerializedWiring.SetObject(registry, "_computerScreen", computer);

            HomeRegistry.Home[] entries = new HomeRegistry.Home[homes.Count];
            for (int i = 0; i < homes.Count; i++)
            {
                HomeKit h = homes[i];
                entries[i] = new HomeRegistry.Home
                {
                    name = h.Name,
                    arrival = h.Arrival,
                    furniture = h.Furniture,
                    bed = h.Bed,
                    computer = h.Computer,
                    wardrobe = h.Wardrobe,
                    letters = h.Letters,
                    fridge = h.Fridge,
                    medkit = h.Medkit,
                    doors = h.Doors,
                    openForOwner = h.OpenForOwner
                };

                // Seule la planque est meublée au départ ; le reste s'achète.
                if (i > 0) h.Furniture.SetActive(false);
            }

            registry.Configure(entries);
            EditorUtility.SetDirty(registry);
            return registry;
        }

        /// <summary>Toutes les portes de la ville qui deviennent de vraies portes.</summary>
        private static string[] CityDoors(MapPack.Data map, List<HomeKit> homes)
        {
            List<string> doors = new List<string>();
            if (map.home != null && !string.IsNullOrEmpty(map.home.door)) doors.Add(map.home.door);
            for (int i = 0; i < homes.Count; i++) doors.AddRange(homes[i].Doors);
            if (map.doors != null) doors.AddRange(map.doors);

            List<string> unique = new List<string>();
            for (int i = 0; i < doors.Count; i++)
            {
                if (!string.IsNullOrEmpty(doors[i]) && !unique.Contains(doors[i])) unique.Add(doors[i]);
            }

            return unique.ToArray();
        }

        // ------------------------------------------------------------------ écrans

        private static WardrobeScreen BuildWardrobeScreen(GameObject systems, GameObject player, PlayerProgress progress)
        {
            WardrobeScreen screen = systems.AddComponent<WardrobeScreen>();
            WirePanel(screen, player, progress);
            return screen;
        }

        private static ComputerScreen BuildComputerScreen(GameObject systems, GameObject player, PlayerProgress progress)
        {
            ComputerScreen screen = systems.AddComponent<ComputerScreen>();
            WirePanel(screen, player, progress);
            return screen;
        }

        private static void WirePanel(FullScreenPanel panel, GameObject player, PlayerProgress progress)
        {
            SerializedWiring.SetObject(panel, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(panel, "_cursor", player.GetComponent<CursorLockController>());
            SerializedWiring.SetObject(panel, "_progress", progress);
            EditorUtility.SetDirty(panel);
        }

        // ------------------------------------------------------------------ le joueur : tenue, entraînement

        private static void WirePlayerProgression(GameObject player, BuildMaterials materials, PlayerProgress progress)
        {
            PlayerUpgrades upgrades = player.AddComponent<PlayerUpgrades>();
            SerializedWiring.SetObject(upgrades, "_progress", progress);
            SerializedWiring.SetObject(upgrades, "_combatant", player.GetComponent<Combat.Combatant>());
            SerializedWiring.SetObject(upgrades, "_motor", player.GetComponent<PlayerMotor>());
            EditorUtility.SetDirty(upgrades);

            SkinnedMeshRenderer body = null;
            SkinnedMeshRenderer shadow = null;
            SkinnedMeshRenderer[] renderers = player.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                SkinnedMeshRenderer r = renderers[i];
                if (r.gameObject.name == "Ombre" && r.shadowCastingMode == ShadowCastingMode.ShadowsOnly) shadow = r;
                else if (body == null && r.shadowCastingMode == ShadowCastingMode.Off) body = r;
            }

            if (body == null) return;

            CorpsImporter.Data data = CorpsImporter.Load("Athlete", false);
            if (data == null) return;

            CorpsImporter.Top[] cuts = { CorpsImporter.Top.TShirt, CorpsImporter.Top.Veste, CorpsImporter.Top.Debardeur };
            PlayerWardrobe.TopVariant[] tops = new PlayerWardrobe.TopVariant[cuts.Length];

            for (int i = 0; i < cuts.Length; i++)
            {
                List<string> slots;
                Mesh mesh = CorpsImporter.BuildMesh(data, cuts[i], false, out slots);
                List<string> shadowSlots;
                Mesh whole = CorpsImporter.BuildMesh(data, cuts[i], true, out shadowSlots);

                tops[i] = new PlayerWardrobe.TopVariant
                {
                    name = PlayerWardrobe.Tops[i].Name,
                    body = mesh,
                    shadow = whole,
                    bodyMaterials = CorpsImporter.MaterialsFor(data.Name, slots, materials.Shirt, materials.Pants, materials.Shoe),
                    shadowMaterials = CorpsImporter.MaterialsFor(data.Name, shadowSlots, materials.Shirt, materials.Pants, materials.Shoe),
                    bodySlots = slots.ToArray(),
                    shadowSlots = shadowSlots.ToArray()
                };
            }

            PlayerWardrobe wardrobe = player.AddComponent<PlayerWardrobe>();
            SerializedWiring.SetObject(wardrobe, "_progress", progress);
            wardrobe.Configure(body, shadow, tops);
            EditorUtility.SetDirty(wardrobe);
        }

        // ------------------------------------------------------------------ accessoires des cibles

        private static TargetActivityKit BuildActivityKit(GameObject systems)
        {
            EditorBuildUtility.EnsureFolder(AccessoriesFolder);
            TargetActivityKit kit = systems.AddComponent<TargetActivityKit>();

            kit.Paper = EditorBuildUtility.CreateOrUpdateMaterial(AccessoriesFolder, "M_Cigarette", new Color(0.92f, 0.9f, 0.85f), 0.1f, 0f);
            kit.Ember = NightMaterialFactory.CreateEmissive(AccessoriesFolder, "M_Braise", new Color(0.3f, 0.08f, 0.02f), null,
                Vector2.one, new Color(1f, 0.35f, 0.08f) * 2.2f, null, 0.2f, 0f);
            kit.Phone = EditorBuildUtility.CreateOrUpdateMaterial(AccessoriesFolder, "M_Telephone", new Color(0.04f, 0.04f, 0.05f), 0.85f, 0.2f);
            kit.Bottle = EditorBuildUtility.CreateOrUpdateMaterial(AccessoriesFolder, "M_Bouteille", new Color(0.12f, 0.28f, 0.1f), 0.92f, 0f);
            kit.Can = EditorBuildUtility.CreateOrUpdateMaterial(AccessoriesFolder, "M_Bombe", new Color(0.85f, 0.15f, 0.45f), 0.6f, 0.7f);
            kit.Rod = EditorBuildUtility.CreateOrUpdateMaterial(AccessoriesFolder, "M_Canne", new Color(0.08f, 0.07f, 0.06f), 0.5f, 0.1f);
            kit.Cash = EditorBuildUtility.CreateOrUpdateMaterial(AccessoriesFolder, "M_Liasse", new Color(0.36f, 0.48f, 0.3f), 0.15f, 0f);
            kit.Graffiti = GraffitiMaterial();
            kit.Particle = SmokeMaterial();

            EditorUtility.SetDirty(kit);
            return kit;
        }

        private static Material SmokeMaterial()
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            if (shader == null) return null;

            string path = AccessoriesFolder + "/M_Fumee.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.5f, 0.5f));
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Le tag : une signature cursive rose au contour sombre, avec ses coulures. Dessinée
        /// ici (pas de police), en boucles et jambages comme un vrai blaze à la bombe.
        /// </summary>
        private static Material GraffitiMaterial()
        {
            const int width = 512;
            const int height = 256;
            string texturePath = AccessoriesFolder + "/T_Tag.png";

            Color[] pixels = new Color[width * height];
            float[] fill = new float[width * height];
            float[] outline = new float[width * height];

            System.Random random = new System.Random(1312);
            List<Vector2> stroke = new List<Vector2>();

            // Des boucles qui avancent : x monte, y ondule, quelques jambages hauts et bas.
            float t = 0f;
            while (t < 1f)
            {
                float x = 50f + t * 400f;
                float loop = Mathf.Sin(t * Mathf.PI * 14f);
                float tall = Mathf.Pow(Mathf.Abs(Mathf.Sin(t * Mathf.PI * 3.5f + 0.6f)), 6f);
                float y = 128f + loop * (28f + 52f * tall) + Mathf.Sin(t * 5f) * 8f;
                stroke.Add(new Vector2(x + Mathf.Cos(t * Mathf.PI * 14f) * 18f, y));
                t += 0.0015f;
            }

            // Le trait de soulignement, lancé d'un geste.
            for (float u = 0f; u <= 1f; u += 0.004f)
            {
                stroke.Add(new Vector2(60f + u * 400f, 58f + Mathf.Sin(u * 3.2f) * 10f - u * 10f));
            }

            for (int i = 0; i < stroke.Count; i++)
            {
                Stamp(outline, width, height, stroke[i], 13.5f);
                Stamp(fill, width, height, stroke[i], 8.5f);
            }

            // Les coulures : sous les points bas du tracé.
            for (int d = 0; d < 9; d++)
            {
                Vector2 start = stroke[random.Next(stroke.Count / 2)];
                float length = 18f + (float)random.NextDouble() * 45f;
                for (float k = 0f; k < length; k += 1f)
                {
                    float r = Mathf.Lerp(3.2f, 1.6f, k / length);
                    Stamp(outline, width, height, start - new Vector2(0f, k), r + 1.8f);
                    Stamp(fill, width, height, start - new Vector2(0f, k), r);
                }
            }

            Color pink = new Color(1f, 0.22f, 0.58f);
            Color dark = new Color(0.06f, 0.03f, 0.08f);
            for (int i = 0; i < pixels.Length; i++)
            {
                float a = Mathf.Max(fill[i], outline[i]);
                float grain = 0.88f + 0.12f * (float)random.NextDouble();
                Color c = Color.Lerp(dark, pink, fill[i]) * grain;
                c.a = Mathf.Clamp01(a * 1.1f) * (0.85f + 0.15f * (float)random.NextDouble());
                pixels[i] = c;
            }

            Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
            texture.SetPixels(pixels);
            texture.Apply();
            System.IO.File.WriteAllBytes(texturePath, texture.EncodeToPNG());
            Object.DestroyImmediate(texture);

            AssetDatabase.ImportAsset(texturePath, ImportAssetOptions.ForceUpdate);
            TextureImporter importer = AssetImporter.GetAtPath(texturePath) as TextureImporter;
            if (importer != null)
            {
                importer.alphaIsTransparency = true;
                importer.wrapMode = TextureWrapMode.Clamp;
                importer.mipmapEnabled = true;
                importer.SaveAndReimport();
            }

            Texture2D tag = AssetDatabase.LoadAssetAtPath<Texture2D>(texturePath);
            Shader shader = Shader.Find("Legacy Shaders/Transparent/Diffuse");
            if (shader == null) return null;

            string path = AccessoriesFolder + "/M_Tag.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = tag;
            material.color = Color.white;
            EditorUtility.SetDirty(material);
            return material;
        }

        private static void Stamp(float[] into, int width, int height, Vector2 center, float radius)
        {
            int x0 = Mathf.Max(0, Mathf.FloorToInt(center.x - radius - 1f));
            int x1 = Mathf.Min(width - 1, Mathf.CeilToInt(center.x + radius + 1f));
            int y0 = Mathf.Max(0, Mathf.FloorToInt(center.y - radius - 1f));
            int y1 = Mathf.Min(height - 1, Mathf.CeilToInt(center.y + radius + 1f));

            for (int y = y0; y <= y1; y++)
            {
                for (int x = x0; x <= x1; x++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), center);
                    float v = Mathf.Clamp01(radius - d + 0.5f);
                    int i = y * width + x;
                    if (v > into[i]) into[i] = v;
                }
            }
        }
    }
}
