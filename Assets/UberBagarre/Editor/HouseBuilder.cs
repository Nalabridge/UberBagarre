using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La planque : la maison insalubre décrite au début du dossier.
    ///
    /// C'est le premier lieu que le joueur voit, et il a un seul travail : expliquer en dix
    /// secondes, sans une réplique, pourquoi cet homme va accepter d'aller casser la figure
    /// d'un inconnu pour cent cinquante euros.
    ///
    /// Tout le contenu répond à cette question. Il n'y a pas de lit mais un matelas posé au
    /// sol. La cuisine tient en deux meubles dont un est vide. Les bouteilles ne sont pas
    /// rangées : elles sont là où elles sont tombées. Et sur la table du salon, trois
    /// enveloppes que le joueur peut lire — c'est la seule interaction obligatoire de la
    /// séquence, et c'est elle qui porte toute la motivation du personnage.
    ///
    /// Le dossier décrit aussi la maison vue de l'extérieur, sur une parcelle seule et
    /// clôturée, avec un jardin. C'est ce qui est construit autour : le grillage n'est pas un
    /// décor, c'est ce qui dit que le personnage est isolé.
    /// </summary>
    public static class HouseBuilder
    {
        public const float PlotWidth = 28f;
        public const float PlotDepth = 24f;

        public const float RoomWidth = 10f;
        public const float RoomDepth = 8f;
        public const float WallHeight = 2.55f;
        public const float WallThickness = 0.22f;

        /// <summary>Le plan du mur avant (côté jardin) : la porte y est centrée.</summary>
        public const float HouseZ = 3.2f;

        public const float DoorWidth = 1.05f;
        public const float DoorHeight = 2.05f;

        public class Result
        {
            public Transform Root;

            /// <summary>Où le joueur se réveille.</summary>
            public Transform Arrival;

            /// <summary>Le courrier sur la table.</summary>
            public Interactable Letters;

            /// <summary>La voiture dans l'allée.</summary>
            public Interactable Car;
        }

        public class Palette
        {
            public Material Grass;
            public Material Gravel;
            public Material Lino;
            public Material Wallpaper;
            public Material Render;
            public Material Roof;
            public Material Mattress;
            public Material Blanket;
            public Material Paper;
            public Material Porcelain;
            public Material Laminate;
        }

        // ------------------------------------------------------------------ construction

        public static Result Build(NightMaterialFactory.Palette night, Vector3 origin)
        {
            Palette palette = CreatePalette();

            GameObject root = new GameObject("=== La planque ===");
            root.transform.position = origin;

            Result result = new Result();
            result.Root = root.transform;

            BuildPlot(root.transform, night, palette);
            BuildShell(root.transform, night, palette);
            BuildInterior(root.transform, night, palette, result);
            BuildYard(root.transform, night, palette, result);

            // La pluie contre la vitre, la ville derriere les murs : la planque n'est jamais
            // silencieuse, meme a 2 h du matin.
            NightStreetBuilder.AddAmbience(root.transform, "Ambiance (maison)", new Vector3(0f, 0f, HouseZ),
                AmbientSoundscape.Kind.Maison, 0.34f, 0.4f, 1f, 12f);

            // Sonde a projection en boite, calee sur la piece : les reflets du lino, de la
            // vitre et de la bouteille viennent de la piece elle-meme, pas du ciel.
            EditorBuildUtility.AddReflectionProbe(root.transform, "Sonde de reflexion (piece)",
                new Vector3(0f, WallHeight * 0.5f, HouseZ), new Vector3(RoomWidth, WallHeight, RoomDepth), true, 1f);

            GameObject arrival = EditorBuildUtility.CreateEmpty("Arrivee", root.transform,
                new Vector3(-2.4f, 0f, HouseZ + RoomDepth * 0.5f - 1.6f));

            // Le joueur se réveille face au salon, donc face à la table et aux enveloppes.
            // Le décor doit faire le travail avant que le premier objectif ne s'affiche.
            arrival.transform.localRotation = Quaternion.Euler(0f, 180f, 0f);
            result.Arrival = arrival.transform;

            return result;
        }

        private static Palette CreatePalette()
        {
            const string materials = NightMaterialFactory.MaterialsFolder;
            const string textures = NightMaterialFactory.TexturesFolder;

            Palette p = new Palette();

            Texture2D grass = EditorBuildUtility.CreateOrUpdateGrainTexture(textures, "T_Herbe", 256,
                new Color(0.16f, 0.21f, 0.12f), 0.20f, 0.11f, 0.05f, 0.11f, 2211);

            Texture2D gravel = EditorBuildUtility.CreateOrUpdateGrainTexture(textures, "T_Gravier", 256,
                new Color(0.31f, 0.30f, 0.28f), 0.10f, 0.14f, 0.08f, 0.30f, 7788);

            // Le lino est TACHE, et c'est tout l'interet de la texture : un sol uniformement
            // sale se lit comme une couleur choisie, un sol tache se lit comme un sol vecu.
            Texture2D lino = EditorBuildUtility.CreateOrUpdateGrainTexture(textures, "T_Lino", 256,
                new Color(0.46f, 0.42f, 0.35f), 0.17f, 0.06f, 0.03f, 0.07f, 3030);

            Texture2D wallpaper = EditorBuildUtility.CreateOrUpdateGrainTexture(textures, "T_PapierPeint", 256,
                new Color(0.55f, 0.51f, 0.44f), 0.13f, 0.05f, 0.02f, 0.045f, 5151);

            Texture2D render = EditorBuildUtility.CreateOrUpdateGrainTexture(textures, "T_Crepi", 256,
                new Color(0.42f, 0.40f, 0.37f), 0.09f, 0.10f, 0.05f, 0.22f, 6262);

            Texture2D mattress = EditorBuildUtility.CreateOrUpdateFabricTexture(textures, "T_Matelas", 256,
                new Color(0.58f, 0.55f, 0.48f), 5, 0.12f, 1616);

            Texture2D blanket = EditorBuildUtility.CreateOrUpdateFabricTexture(textures, "T_Couverture", 256,
                new Color(0.24f, 0.20f, 0.23f), 4, 0.16f, 1717);

            p.Grass = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Herbe",
                Color.white, 0.06f, 0f, grass, new Vector2(18f, 16f));

            p.Gravel = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Gravier",
                Color.white, 0.10f, 0f, gravel, new Vector2(8f, 8f));

            p.Lino = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Lino",
                Color.white, 0.26f, 0f, lino, new Vector2(7f, 6f));

            p.Wallpaper = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_PapierPeint",
                Color.white, 0.05f, 0f, wallpaper, new Vector2(5f, 2.5f));

            p.Render = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Crepi",
                Color.white, 0.07f, 0f, render, new Vector2(6f, 3f));

            p.Roof = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Toiture",
                new Color(0.20f, 0.19f, 0.19f), 0.12f, 0f);

            p.Mattress = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Matelas",
                Color.white, 0.07f, 0f, mattress, new Vector2(3f, 5f));

            p.Blanket = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Couverture",
                Color.white, 0.06f, 0f, blanket, new Vector2(3f, 3f));

            p.Paper = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Papier",
                new Color(0.86f, 0.84f, 0.78f), 0.12f, 0f);

            p.Porcelain = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Faience",
                new Color(0.80f, 0.80f, 0.78f), 0.55f, 0f);

            p.Laminate = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Stratifie",
                new Color(0.38f, 0.30f, 0.23f), 0.22f, 0f);

            return p;
        }

        // ------------------------------------------------------------------ terrain

        private static void BuildPlot(Transform parent, NightMaterialFactory.Palette night, Palette palette)
        {
            GameObject plot = EditorBuildUtility.CreateEmpty("Parcelle", parent, Vector3.zero);

            Box(plot.transform, "Terrain", new Vector3(0f, -0.2f, 0f),
                new Vector3(PlotWidth, 0.4f, PlotDepth), palette.Grass, true);

            // L'allée de gravier : elle relie le portail à la porte, et c'est elle qui dit où
            // aller au moment où le joueur sort de chez lui pour la première fois.
            Slab(plot.transform, "Allee", new Vector3(2.6f, 0.012f, -2.5f),
                new Vector3(3.2f, 0.03f, PlotDepth * 0.58f), palette.Gravel);

            Slab(plot.transform, "Seuil", new Vector3(0f, 0.012f, HouseZ - RoomDepth * 0.5f - 0.9f),
                new Vector3(2.6f, 0.03f, 1.8f), palette.Gravel);

            Fence(plot.transform, night);

            // Un seul lampadaire, au-delà de la clôture, côté route : c'est la seule source
            // extérieure. Le reste du jardin est noir, et c'est ce noir qui rend l'ampoule de
            // l'intérieur importante.
            GameObject lamp = EditorBuildUtility.CreateEmpty("Lampadaire de rue", plot.transform,
                new Vector3(-8.5f, 0f, -PlotDepth * 0.5f - 1.6f));

            Cylinder(lamp.transform, "Mat", new Vector3(0f, 2.9f, 0f), new Vector3(0.12f, 2.9f, 0.12f),
                night.DarkMetal, true);

            Box(lamp.transform, "Potence", new Vector3(0f, 5.7f, 0.8f), new Vector3(0.1f, 0.1f, 1.8f),
                night.DarkMetal, false);

            Box(lamp.transform, "Lanterne", new Vector3(0f, 5.5f, 1.55f),
                new Vector3(0.4f, 0.18f, 0.75f), night.NeonWarm, false);

            GameObject lightGo = EditorBuildUtility.CreateEmpty("Lumiere", lamp.transform,
                new Vector3(0f, 5.35f, 1.55f));

            lightGo.transform.localRotation = Quaternion.LookRotation(Vector3.down, Vector3.forward);

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 124f;
            light.color = new Color(1f, 0.74f, 0.44f);
            light.intensity = 4.4f;
            light.range = 14f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = LightShadows.Soft;
            light.shadowNormalBias = 0.3f;

            NightStreetBuilder.MakeVolumetric(light, 1f);

            AddFlicker(lamp, night, 5.5f);
        }

        private static void Fence(Transform parent, NightMaterialFactory.Palette night)
        {
            GameObject fence = EditorBuildUtility.CreateEmpty("Cloture", parent, Vector3.zero);

            float halfX = PlotWidth * 0.5f;
            float halfZ = PlotDepth * 0.5f;

            FenceRun(fence.transform, night, new Vector3(-halfX, 0f, halfZ), new Vector3(halfX, 0f, halfZ));
            FenceRun(fence.transform, night, new Vector3(-halfX, 0f, -halfZ), new Vector3(-halfX, 0f, halfZ));
            FenceRun(fence.transform, night, new Vector3(halfX, 0f, -halfZ), new Vector3(halfX, 0f, halfZ));

            // Le portail reste OUVERT : une clôture qu'on ne peut pas franchir transforme le
            // jardin en couloir, et le joueur passe son temps à chercher la sortie.
            FenceRun(fence.transform, night, new Vector3(-halfX, 0f, -halfZ), new Vector3(0.6f, 0f, -halfZ));
            FenceRun(fence.transform, night, new Vector3(4.6f, 0f, -halfZ), new Vector3(halfX, 0f, -halfZ));
        }

        private static void FenceRun(Transform parent, NightMaterialFactory.Palette night,
            Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.4f) return;

            GameObject run = EditorBuildUtility.CreateEmpty("Pan", parent, (from + to) * 0.5f);
            run.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            const float height = 1.55f;

            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 2.1f));

            for (int i = 0; i <= posts; i++)
            {
                float z = -length * 0.5f + length * i / posts;

                Box(run.transform, "Poteau", new Vector3(0f, height * 0.5f, z),
                    new Vector3(0.09f, height, 0.09f), night.Wood, true);
            }

            for (int rail = 0; rail < 2; rail++)
            {
                float y = 0.42f + rail * 0.68f;

                Box(run.transform, "Lisse", new Vector3(0f, y, 0f),
                    new Vector3(0.05f, 0.07f, length), night.Wood, false);
            }

            // Grillage suggéré par des barreaux verticaux serrés : une vraie maille demanderait
            // une texture transparente, donc un shader différent selon le pipeline.
            int slats = Mathf.Max(2, Mathf.RoundToInt(length / 0.28f));

            for (int i = 0; i < slats; i++)
            {
                float z = -length * 0.5f + length * (i + 0.5f) / slats;

                Box(run.transform, "Barreau", new Vector3(0f, 0.75f, z),
                    new Vector3(0.022f, 1.4f, 0.022f), night.Metal, false);
            }
        }

        // ------------------------------------------------------------------ gros oeuvre

        private static void BuildShell(Transform parent, NightMaterialFactory.Palette night, Palette palette)
        {
            GameObject shell = EditorBuildUtility.CreateEmpty("Maison", parent, new Vector3(0f, 0f, HouseZ));

            float halfX = RoomWidth * 0.5f;
            float halfZ = RoomDepth * 0.5f;

            Slab(shell.transform, "Sol", new Vector3(0f, 0.02f, 0f),
                new Vector3(RoomWidth, 0.04f, RoomDepth), palette.Lino);

            // Mur avant, percé d'une porte centrée et d'une fenêtre.
            WallWithOpenings(shell.transform, palette, new Vector3(0f, 0f, -halfZ), 0f, RoomWidth,
                new Vector2(-0.6f, DoorWidth), 0f, DoorHeight,
                new Vector2(2.9f, 1.3f), 0.95f, 2.05f);

            // Mur arrière : une seule fenêtre haute, au-dessus de l'évier.
            WallWithOpenings(shell.transform, palette, new Vector3(0f, 0f, halfZ), 180f, RoomWidth,
                new Vector2(9999f, 0f), 0f, 0f,
                new Vector2(-2.4f, 1.1f), 1.25f, 2.1f);

            WallWithOpenings(shell.transform, palette, new Vector3(-halfX, 0f, 0f), 90f, RoomDepth,
                new Vector2(9999f, 0f), 0f, 0f,
                new Vector2(1.6f, 1.0f), 1.1f, 2f);

            WallWithOpenings(shell.transform, palette, new Vector3(halfX, 0f, 0f), 270f, RoomDepth,
                new Vector2(9999f, 0f), 0f, 0f,
                new Vector2(9999f, 0f), 0f, 0f);

            Slab(shell.transform, "Plafond", new Vector3(0f, WallHeight + 0.06f, 0f),
                new Vector3(RoomWidth, 0.12f, RoomDepth), palette.Wallpaper);

            Box(shell.transform, "Toiture", new Vector3(0f, WallHeight + 0.28f, 0f),
                new Vector3(RoomWidth + 0.7f, 0.3f, RoomDepth + 0.7f), palette.Roof, false);

            Box(shell.transform, "Marche", new Vector3(-0.6f, 0.06f, -halfZ - 0.45f),
                new Vector3(1.6f, 0.12f, 0.9f), night.DarkConcrete, true);

            // Cloison des toilettes, dans l'angle : elle casse le volume et transforme la boîte
            // rectangulaire en logement.
            GameObject bathroom = EditorBuildUtility.CreateEmpty("Toilettes", shell.transform, Vector3.zero);

            Box(bathroom.transform, "Cloison longue", new Vector3(halfX - 1.3f, WallHeight * 0.5f, halfZ - 1.25f),
                new Vector3(0.12f, WallHeight, 2.5f), palette.Wallpaper, true);

            Box(bathroom.transform, "Cloison courte", new Vector3(halfX - 0.65f, WallHeight * 0.5f, halfZ - 2.5f),
                new Vector3(1.3f, WallHeight, 0.12f), palette.Wallpaper, true);

            BuildBathroom(bathroom.transform, night, palette, new Vector3(halfX - 0.65f, 0f, halfZ - 1.2f));
        }

        /// <summary>
        /// Un mur avec au plus deux percements.
        ///
        /// Construire un mur plein puis y « découper » une porte demanderait une opération
        /// booléenne sur maillage, qu'Unity ne fournit pas. On assemble donc le mur en
        /// morceaux : les segments de part et d'autre, le linteau au-dessus, et l'allège en
        /// dessous pour une fenêtre. C'est plus de code, mais chaque morceau reste une boîte,
        /// donc un collider exact et gratuit.
        ///
        /// Une ouverture dont la position est à 9999 est simplement ignorée : c'est la façon la
        /// plus courte de dire « ce mur n'en a pas » sans deux surcharges de plus.
        /// </summary>
        private static void WallWithOpenings(Transform parent, Palette palette, Vector3 position, float yaw,
            float length, Vector2 openingA, float bottomA, float topA,
            Vector2 openingB, float bottomB, float topB)
        {
            GameObject wall = EditorBuildUtility.CreateEmpty("Mur", parent, position);
            wall.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            // Les ouvertures sont triées par abscisse pour que le découpage se fasse de gauche
            // à droite sans se chevaucher.
            bool hasA = Mathf.Abs(openingA.x) < length;
            bool hasB = Mathf.Abs(openingB.x) < length;

            if (hasA && hasB && openingB.x < openingA.x)
            {
                Vector2 swapPos = openingA; openingA = openingB; openingB = swapPos;
                float swapBottom = bottomA; bottomA = bottomB; bottomB = swapBottom;
                float swapTop = topA; topA = topB; topB = swapTop;
            }

            float cursor = -length * 0.5f;

            if (hasA) cursor = Opening(wall.transform, palette, cursor, openingA, bottomA, topA);
            if (hasB) cursor = Opening(wall.transform, palette, cursor, openingB, bottomB, topB);

            Panel(wall.transform, palette, cursor, length * 0.5f, 0f, WallHeight);
        }

        private static float Opening(Transform wall, Palette palette, float cursor, Vector2 opening,
            float bottom, float top)
        {
            float left = opening.x - opening.y * 0.5f;
            float right = opening.x + opening.y * 0.5f;

            Panel(wall, palette, cursor, left, 0f, WallHeight);

            if (bottom > 0.01f) Panel(wall, palette, left, right, 0f, bottom);
            if (top < WallHeight - 0.01f) Panel(wall, palette, left, right, top, WallHeight);

            return right;
        }

        /// <summary>
        /// Un morceau de mur, en DEUX couches : le crépi dehors, le papier peint dedans.
        ///
        /// Un mur d'un seul matériau oblige à choisir : soit l'intérieur ressemble à une façade,
        /// soit la façade ressemble à une chambre. Deux boîtes au lieu d'une règlent la question
        /// définitivement, pour le prix d'un rendu de plus.
        ///
        /// L'extérieur est TOUJOURS du côté -Z local du mur — c'est vrai pour les quatre
        /// orientations utilisées (0, 90, 180, 270 degrés), et c'est ce qui permet d'écrire
        /// cette fonction une seule fois.
        /// </summary>
        private static void Panel(Transform wall, Palette palette, float from, float to,
            float bottom, float top)
        {
            float width = to - from;
            float height = top - bottom;
            if (width <= 0.02f || height <= 0.02f) return;

            Vector3 center = new Vector3((from + to) * 0.5f, (bottom + top) * 0.5f, 0f);

            // Le collider est porté par la couche intérieure, la plus épaisse : une seule
            // surface de collision par morceau de mur, donc aucun risque de coincer le joueur
            // entre deux boîtes qui se touchent.
            Box(wall, "Pan", center + new Vector3(0f, 0f, 0.04f),
                new Vector3(width, height, 0.14f), palette.Wallpaper, true);

            Box(wall, "Crepi", center + new Vector3(0f, 0f, -0.08f),
                new Vector3(width, height, 0.06f), palette.Render, false);
        }

        // ------------------------------------------------------------------ interieur

        private static void BuildInterior(Transform parent, NightMaterialFactory.Palette night,
            Palette palette, Result result)
        {
            GameObject room = EditorBuildUtility.CreateEmpty("Interieur", parent, new Vector3(0f, 0f, HouseZ));
            Transform t = room.transform;

            Bulb(t, night, new Vector3(-0.8f, 0f, 0f));
            Bed(t, night, palette, new Vector3(-3.6f, 0f, 2.4f));
            Kitchen(t, night, palette, new Vector3(0f, 0f, 0f));
            LivingRoom(t, night, palette, result, new Vector3(-2.3f, 0f, -1.5f));
            Clutter(t, night, palette);
        }

        /// <summary>
        /// L'ampoule nue au plafond. C'est la seule source de la pièce, et elle est faible :
        /// une maison bien éclairée ne raconte rien.
        /// </summary>
        private static void Bulb(Transform parent, NightMaterialFactory.Palette night, Vector3 position)
        {
            GameObject fixture = EditorBuildUtility.CreateEmpty("Ampoule", parent,
                position + new Vector3(0f, WallHeight, 0f));

            Cylinder(fixture.transform, "Fil", new Vector3(0f, -0.22f, 0f),
                new Vector3(0.012f, 0.22f, 0.012f), night.DarkMetal, false);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Verre", fixture.transform,
                new Vector3(0f, -0.48f, 0f), new Vector3(0.11f, 0.14f, 0.11f), night.NeonWarm, false);

            GameObject lightGo = EditorBuildUtility.CreateEmpty("Lumiere", fixture.transform,
                new Vector3(0f, -0.5f, 0f));

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = new Color(1f, 0.80f, 0.55f);
            light.intensity = 2.1f;
            light.range = 9f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = LightShadows.Soft;
            light.shadowStrength = 0.7f;
            light.shadowNearPlane = 0.1f;

            // Un peu de poussiere dans l'air de la piece : juste assez pour que l'ampoule ait
            // un halo, calcule a partir d'elle.
            NightStreetBuilder.MakeVolumetric(light, 0.35f);

            // Fatiguée : elle vacille juste assez pour qu'on le remarque sans que ça devienne
            // le sujet de la pièce.
            AddFlicker(fixture, night, 4.5f);
        }

        private static void Bed(Transform parent, NightMaterialFactory.Palette night, Palette palette,
            Vector3 position)
        {
            GameObject bed = EditorBuildUtility.CreateEmpty("Couchage", parent, position);
            bed.transform.localRotation = Quaternion.Euler(0f, 6f, 0f);

            // Pas de sommier : un matelas posé par terre. C'est la différence entre « il vit
            // simplement » et « il n'a pas les moyens d'un lit ».
            Box(bed.transform, "Matelas", new Vector3(0f, 0.11f, 0f),
                new Vector3(1.35f, 0.22f, 2.0f), palette.Mattress, true);

            GameObject blanket = Box(bed.transform, "Couverture", new Vector3(0.14f, 0.26f, -0.26f),
                new Vector3(1.25f, 0.12f, 1.35f), palette.Blanket, false);

            blanket.transform.localRotation = Quaternion.Euler(0f, -9f, 2.5f);

            Box(bed.transform, "Oreiller", new Vector3(-0.1f, 0.28f, 0.78f),
                new Vector3(0.62f, 0.14f, 0.34f), palette.Mattress, false)
                .transform.localRotation = Quaternion.Euler(0f, 12f, 0f);

            NightStreetBuilder.Crate(bed.transform, night, new Vector3(0.95f, 0f, 0.85f), 0.44f, 14f);

            GameObject bottle = Cylinder(bed.transform, "Bouteille", new Vector3(0.95f, 0.57f, 0.85f),
                new Vector3(0.06f, 0.12f, 0.06f), night.Glass, true);

            NightStreetBuilder.MakePhysical(bottle, 0.35f, PhysicsProp.Matter.Verre, 0.1f);
        }

        private static void Kitchen(Transform parent, NightMaterialFactory.Palette night, Palette palette,
            Vector3 origin)
        {
            GameObject kitchen = EditorBuildUtility.CreateEmpty("Cuisine", parent, origin);
            Transform t = kitchen.transform;

            float wallZ = RoomDepth * 0.5f - 0.32f;

            Box(t, "Plan de travail", new Vector3(-1.3f, 0.45f, wallZ),
                new Vector3(3.2f, 0.9f, 0.62f), palette.Laminate, true);

            Box(t, "Paillasse", new Vector3(-1.3f, 0.92f, wallZ),
                new Vector3(3.26f, 0.05f, 0.66f), night.Metal, true);

            // Évier : une cuve creusée par deux boîtes, et un mitigeur. Trois formes, et la
            // cuisine cesse d'être un meuble.
            Box(t, "Evier", new Vector3(-2.3f, 0.86f, wallZ),
                new Vector3(0.72f, 0.14f, 0.5f), night.DarkMetal, false);

            Cylinder(t, "Mitigeur", new Vector3(-2.3f, 1.08f, wallZ + 0.22f),
                new Vector3(0.035f, 0.17f, 0.035f), night.Chrome, false);

            Box(t, "Refrigerateur", new Vector3(1.2f, 0.72f, wallZ - 0.02f),
                new Vector3(0.62f, 1.45f, 0.6f), palette.Porcelain, true);

            Box(t, "Poignee", new Vector3(0.87f, 0.95f, wallZ - 0.33f),
                new Vector3(0.04f, 0.5f, 0.04f), night.Chrome, false);

            NightStreetBuilder.AddAmbience(t, "Ronron du frigo", new Vector3(1.2f, 0.9f, wallZ - 0.35f),
                AmbientSoundscape.Kind.Frigo, 0.16f, 0f, 0.6f, 5.5f);

            // L'horloge de la cuisine : on l'entend avant de la voir.
            GameObject clock = EditorBuildUtility.CreateEmpty("Horloge", t,
                new Vector3(2.4f, 1.85f, RoomDepth * 0.5f - 0.13f));

            Cylinder(clock.transform, "Cadran", Vector3.zero, new Vector3(0.3f, 0.012f, 0.3f),
                palette.Porcelain, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Cylinder(clock.transform, "Cerclage", new Vector3(0f, 0f, 0.006f), new Vector3(0.33f, 0.01f, 0.33f),
                night.DarkMetal, false).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            Box(clock.transform, "Grande aiguille", new Vector3(0.028f, 0.045f, -0.016f),
                new Vector3(0.012f, 0.11f, 0.006f), night.DarkMetal, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -32f);

            Box(clock.transform, "Petite aiguille", new Vector3(-0.024f, -0.012f, -0.016f),
                new Vector3(0.014f, 0.07f, 0.006f), night.DarkMetal, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 112f);

            NightStreetBuilder.AddAmbience(clock.transform, "Tic-tac", Vector3.zero,
                AmbientSoundscape.Kind.Horloge, 0.14f, 0f, 0.5f, 6.5f);

            // Deux plaques posées sur le plan : la cuisine complète tient en deux feux.
            for (int i = 0; i < 2; i++)
            {
                Cylinder(t, "Plaque", new Vector3(-0.5f + i * 0.42f, 0.96f, wallZ),
                    new Vector3(0.26f, 0.015f, 0.26f), night.DarkMetal, false);
            }

            Box(t, "Placard", new Vector3(-1.3f, 1.95f, wallZ + 0.1f),
                new Vector3(1.9f, 0.62f, 0.38f), palette.Laminate, false);

            // La porte du placard pend : une charnière lâchée, et personne ne l'a réparée.
            Box(t, "Porte ouverte", new Vector3(-0.2f, 1.86f, wallZ - 0.22f),
                new Vector3(0.9f, 0.6f, 0.03f), palette.Laminate, false)
                .transform.localRotation = Quaternion.Euler(0f, 62f, 3f);

            NightStreetBuilder.Bottles(t, night, new Vector3(-0.1f, 0.94f, wallZ - 0.04f), 4);

            GameObject bag = EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Sac poubelle",
                t, new Vector3(2.3f, 0.3f, wallZ - 0.5f), new Vector3(0.62f, 0.6f, 0.58f),
                night.DarkMetal, true);

            bag.transform.localRotation = Quaternion.Euler(9f, 24f, 4f);
            NightStreetBuilder.MakePhysical(bag, 3f, PhysicsProp.Matter.Mou, 1.2f);
        }

        private static void LivingRoom(Transform parent, NightMaterialFactory.Palette night, Palette palette,
            Result result, Vector3 origin)
        {
            GameObject living = EditorBuildUtility.CreateEmpty("Salon", parent, origin);
            Transform t = living.transform;

            // --- la table
            GameObject table = EditorBuildUtility.CreateEmpty("Table", t, Vector3.zero);
            table.transform.localRotation = Quaternion.Euler(0f, -8f, 0f);

            Box(table.transform, "Plateau", new Vector3(0f, 0.73f, 0f),
                new Vector3(1.25f, 0.05f, 0.78f), palette.Laminate, true);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(table.transform, "Pied", new Vector3(x * 0.55f, 0.36f, z * 0.33f),
                        new Vector3(0.06f, 0.72f, 0.06f), palette.Laminate, false);
                }
            }

            // --- LE COURRIER : la seule interaction obligatoire du prologue.
            GameObject letters = EditorBuildUtility.CreateEmpty("Courrier", table.transform,
                new Vector3(-0.12f, 0.77f, 0.04f));

            float[] yaw = { 4f, -13f, 9f };
            float[] lift = { 0f, 0.006f, 0.012f };

            for (int i = 0; i < 3; i++)
            {
                GameObject envelope = Box(letters.transform, "Enveloppe",
                    new Vector3(i * 0.045f, lift[i], i * 0.02f),
                    new Vector3(0.23f, 0.004f, 0.115f), palette.Paper, false);

                envelope.transform.localRotation = Quaternion.Euler(0f, yaw[i], 0f);

                // Le bandeau rouge de la relance : deux centimètres de matériau qui disent
                // tout de suite de quel genre de courrier il s'agit.
                Slab(letters.transform, "Bandeau", new Vector3(i * 0.045f - 0.07f, lift[i] + 0.003f, i * 0.02f),
                    new Vector3(0.045f, 0.002f, 0.112f), night.Carpet)
                    .transform.localRotation = Quaternion.Euler(0f, yaw[i], 0f);
            }

            BoxCollider collider = letters.AddComponent<BoxCollider>();
            collider.size = new Vector3(0.44f, 0.12f, 0.24f);
            collider.center = new Vector3(0.05f, 0.02f, 0.02f);

            Interactable interactable = letters.AddComponent<Interactable>();
            SerializedWiring.SetString(interactable, "_label", "Lire le courrier");
            SerializedWiring.SetString(interactable, "_hint", "Trois relances en une semaine");
            SerializedWiring.SetFloat(interactable, "_range", 2.2f);
            SerializedWiring.Verify(interactable, "_label");

            result.Letters = interactable;

            // --- cendrier et mégots
            GameObject ashtray = EditorBuildUtility.CreateEmpty("Cendrier", table.transform,
                new Vector3(0.42f, 0.755f, -0.16f));

            Cylinder(ashtray.transform, "Coupelle", Vector3.zero, new Vector3(0.14f, 0.012f, 0.14f),
                night.Glass, false);

            for (int i = 0; i < 6; i++)
            {
                float angle = i * 1.9f;

                GameObject butt = Cylinder(ashtray.transform, "Megot",
                    new Vector3(Mathf.Cos(angle) * 0.04f, 0.018f, Mathf.Sin(angle) * 0.04f),
                    new Vector3(0.008f, 0.014f, 0.008f), palette.Paper, false);

                butt.transform.localRotation = Quaternion.Euler(88f, angle * 57f, 0f);
            }

            // --- chaise
            GameObject chair = EditorBuildUtility.CreateEmpty("Chaise", t, new Vector3(0.05f, 0f, -0.95f));
            chair.transform.localRotation = Quaternion.Euler(0f, 158f, 0f);

            Box(chair.transform, "Assise", new Vector3(0f, 0.44f, 0f),
                new Vector3(0.42f, 0.05f, 0.42f), palette.Laminate, true);

            Box(chair.transform, "Dossier", new Vector3(0f, 0.72f, -0.19f),
                new Vector3(0.42f, 0.5f, 0.04f), palette.Laminate, false);

            // Les pieds portent des colliders : une chaise physique dont seule l'assise est
            // solide tombe de 44 cm au premier pas de simulation et se pose à plat sur le sol.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(chair.transform, "Pied", new Vector3(x * 0.18f, 0.22f, z * 0.18f),
                        new Vector3(0.04f, 0.44f, 0.04f), palette.Laminate, true);
                }
            }

            NightStreetBuilder.MakePhysical(chair, 4f, PhysicsProp.Matter.Bois, 0.1f);

            // --- téléviseur posé sur une caisse
            NightStreetBuilder.Crate(t, night, new Vector3(2.1f, 0f, -1.5f), 0.6f, -6f);

            GameObject tv = EditorBuildUtility.CreateEmpty("Televiseur", t, new Vector3(2.1f, 0.6f, -1.5f));
            tv.transform.localRotation = Quaternion.Euler(0f, -32f, 0f);

            Box(tv.transform, "Caisson", new Vector3(0f, 0.22f, 0f),
                new Vector3(0.58f, 0.44f, 0.5f), palette.Laminate, true);

            Box(tv.transform, "Dalle", new Vector3(0f, 0.24f, -0.26f),
                new Vector3(0.44f, 0.33f, 0.03f), night.Glass, false);

            NightStreetBuilder.MakePhysical(tv, 11f, PhysicsProp.Matter.Plastique, 0.1f);
        }

        /// <summary>
        /// Le désordre. Il est placé à la main, pas au hasard : des bouteilles réparties
        /// uniformément se lisent comme un motif, alors que des bouteilles groupées près du
        /// matelas et de la table se lisent comme des habitudes.
        /// </summary>
        private static void Clutter(Transform parent, NightMaterialFactory.Palette night, Palette palette)
        {
            GameObject clutter = EditorBuildUtility.CreateEmpty("Desordre", parent, Vector3.zero);

            NightStreetBuilder.Bottles(clutter.transform, night, new Vector3(-2.6f, 0.04f, 1.2f), 5);
            NightStreetBuilder.Bottles(clutter.transform, night, new Vector3(-1.4f, 0.04f, -2.1f), 4);
            NightStreetBuilder.Bottles(clutter.transform, night, new Vector3(3.1f, 0.04f, 0.6f), 3);

            // Une pile de courrier non ouvert au sol, près de la porte : il rentre, il jette.
            for (int i = 0; i < 5; i++)
            {
                GameObject flyer = Slab(clutter.transform, "Prospectus",
                    new Vector3(-0.35f + i * 0.06f, 0.045f + i * 0.004f, -RoomDepth * 0.5f + 0.65f),
                    new Vector3(0.2f, 0.003f, 0.1f), palette.Paper);

                flyer.transform.localRotation = Quaternion.Euler(0f, i * 23f - 40f, 0f);
            }

            GameObject carton = Box(clutter.transform, "Carton", new Vector3(4.0f, 0.18f, -2.3f),
                new Vector3(0.52f, 0.36f, 0.42f), palette.Paper, true);

            carton.transform.localRotation = Quaternion.Euler(0f, 17f, 0f);
            NightStreetBuilder.MakePhysical(carton, 1.5f, PhysicsProp.Matter.Mou, 0.4f);
        }

        private static void BuildBathroom(Transform parent, NightMaterialFactory.Palette night,
            Palette palette, Vector3 origin)
        {
            GameObject bathroom = EditorBuildUtility.CreateEmpty("Sanitaires", parent, origin);
            Transform t = bathroom.transform;

            Slab(t, "Carrelage", new Vector3(0f, 0.045f, 0f), new Vector3(1.25f, 0.02f, 2.4f),
                palette.Porcelain);

            Box(t, "Cuvette", new Vector3(0.28f, 0.2f, 0.75f), new Vector3(0.38f, 0.4f, 0.55f),
                palette.Porcelain, true);

            Box(t, "Reservoir", new Vector3(0.28f, 0.55f, 1.02f), new Vector3(0.4f, 0.32f, 0.16f),
                palette.Porcelain, false);

            Box(t, "Lavabo", new Vector3(0.3f, 0.8f, -0.6f), new Vector3(0.44f, 0.12f, 0.34f),
                palette.Porcelain, true);

            Cylinder(t, "Robinet", new Vector3(0.3f, 0.92f, -0.46f), new Vector3(0.03f, 0.08f, 0.03f),
                night.Chrome, false);

            Box(t, "Miroir", new Vector3(0.38f, 1.35f, -0.6f), new Vector3(0.03f, 0.4f, 0.32f),
                night.Glass, false);
        }

        // ------------------------------------------------------------------ jardin

        private static void BuildYard(Transform parent, NightMaterialFactory.Palette night, Palette palette,
            Result result)
        {
            GameObject yard = EditorBuildUtility.CreateEmpty("Jardin", parent, Vector3.zero);
            Transform t = yard.transform;

            // La voiture du dossier : « sale, délabrée, rouillée, vieille bref nulle ».
            // C'est LA MÊME que celle garée devant le club — même méthode, même palette.
            NightStreetBuilder.Car(t, night, new Vector3(2.6f, 0f, -5.4f), 4f, true, false);

            GameObject door = EditorBuildUtility.CreateEmpty("Portiere conducteur", t,
                new Vector3(2.6f, 1f, -6.4f));

            BoxCollider collider = door.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.4f, 1.8f, 2.4f);
            collider.isTrigger = true;

            Interactable car = door.AddComponent<Interactable>();
            SerializedWiring.SetString(car, "_label", "Monter en voiture");
            SerializedWiring.SetString(car, "_hint", "Direction le club");
            SerializedWiring.SetFloat(car, "_range", 2.8f);
            SerializedWiring.SetBool(car, "_once", true);
            SerializedWiring.SetBool(car, "_enabledForPlayer", false);
            SerializedWiring.Verify(car, "_once");

            result.Car = car;

            NightStreetBuilder.Dumpster(t, night, new Vector3(-7.2f, 0f, 4.6f), 24f);
            NightStreetBuilder.Pallet(t, night, new Vector3(-6.2f, 0f, 1.8f), -18f);
            NightStreetBuilder.Crate(t, night, new Vector3(-5.4f, 0f, 2.6f), 0.7f, 33f);

            // Herbes hautes : des lames plates orientées au hasard. Rien ne dit mieux
            // « personne ne tond ici » et ça coûte quelques boîtes.
            GameObject weeds = EditorBuildUtility.CreateEmpty("Herbes folles", t, Vector3.zero);

            for (int i = 0; i < 34; i++)
            {
                float angle = i * 2.39996f;
                float radius = 2.2f + (i % 7) * 1.35f;

                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.22f,
                    Mathf.Sin(angle) * radius * 0.8f - 3.2f);

                GameObject blade = Slab(weeds.transform, "Touffe", position,
                    new Vector3(0.34f, 0.45f, 0.02f), palette.Grass);

                blade.transform.localRotation = Quaternion.Euler(0f, i * 47f, i % 3 * 7f - 7f);
            }

            NightStreetBuilder.Bottles(t, night, new Vector3(-1.8f, 0f, -4.2f), 4);
        }

        // ------------------------------------------------------------------ utilitaires

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool collider)
        {
            return EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, name, parent, position, size,
                material, collider);
        }

        private static GameObject Slab(Transform parent, string name, Vector3 position, Vector3 size,
            Material material)
        {
            return EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, name, parent, position, size,
                material, false);
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 size,
            Material material, bool collider)
        {
            return EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, name, parent, position, size,
                material, collider);
        }

        private static void AddFlicker(GameObject target, NightMaterialFactory.Palette night, float intensity)
        {
            UberBagarre.View.NeonFlicker flicker = target.AddComponent<UberBagarre.View.NeonFlicker>();

            SerializedWiring.SetEnum(flicker, "_pattern", (int)UberBagarre.View.NeonFlicker.Pattern.Fatigue);
            SerializedWiring.SetFloat(flicker, "_baseIntensity", intensity);
            SerializedWiring.SetFloat(flicker, "_amount", 0.14f);
            SerializedWiring.SetFloat(flicker, "_speed", 0.9f);
            SerializedWiring.SetFloat(flicker, "_seed", Mathf.Abs(target.transform.position.x) * 3.3f + 5f);
        }
    }
}
