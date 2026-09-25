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

            /// <summary>
            /// Le plan de l'écran titre : un lent travelling de l'autre côté de la rue, de
            /// <see cref="MenuFrom"/> à <see cref="MenuTo"/>, qui regarde <see cref="MenuTarget"/>.
            /// </summary>
            public Transform MenuFrom;
            public Transform MenuTo;
            public Transform MenuTarget;
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
            public Material Stain;
            public Material Sofa;
            public Material Hedge;
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

            // L'ecran titre filme la maison depuis le trottoir d'en face : la facade, le porche
            // allume, la voiture sur l'allee, les lampadaires.
            GameObject shot = EditorBuildUtility.CreateEmpty("Plan de l'ecran titre", root.transform, Vector3.zero);
            result.MenuFrom = EditorBuildUtility.CreateEmpty("Depart", shot.transform, new Vector3(12.5f, 1.45f, -22.3f)).transform;
            result.MenuTo = EditorBuildUtility.CreateEmpty("Arrivee", shot.transform, new Vector3(4.2f, 1.8f, -21.9f)).transform;
            result.MenuTarget = EditorBuildUtility.CreateEmpty("Visee", shot.transform, new Vector3(3.4f, 1.9f, -0.5f)).transform;

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

            // Des tuiles, pas une plaque grise : a contre-jour du lampadaire, c'est le relief
            // des rangees qui fait lire un toit.
            Texture2D tiles = EditorBuildUtility.CreateOrUpdateBrickTexture(textures, "T_Tuiles", 256, 9,
                new Color(0.10f, 0.09f, 0.09f), new Color(0.34f, 0.20f, 0.16f));

            p.Roof = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Toiture",
                new Color(0.72f, 0.66f, 0.64f), 0.14f, 0f, tiles, new Vector2(6f, 3f));

            Texture2D stain = EditorBuildUtility.CreateOrUpdateGrainTexture(textures, "T_Humidite", 128,
                new Color(0.27f, 0.24f, 0.17f), 0.35f, 0.08f, 0.04f, 0.25f, 4545);

            p.Stain = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Humidite",
                Color.white, 0.04f, 0f, stain, Vector2.one);

            Texture2D sofa = EditorBuildUtility.CreateOrUpdateFabricTexture(textures, "T_Canape", 256,
                new Color(0.31f, 0.26f, 0.19f), 4, 0.15f, 1818);

            p.Sofa = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Canape",
                Color.white, 0.05f, 0f, sofa, new Vector2(3f, 2f));

            Texture2D hedge = EditorBuildUtility.CreateOrUpdateGrainTexture(textures, "T_Haie", 256,
                new Color(0.08f, 0.13f, 0.06f), 0.35f, 0.22f, 0.12f, 0.18f, 9191);

            p.Hedge = EditorBuildUtility.CreateOrUpdateMaterial(materials, "M_Haie",
                Color.white, 0.08f, 0f, hedge, new Vector2(3f, 2f));

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

            // Le chemin pieton : du portillon a la porte, un ruban de gravier et des dalles
            // posees de travers. C'est lui qui dit ou aller en sortant de chez soi.
            Slab(plot.transform, "Chemin", new Vector3(FootpathX, 0.012f, (-PlotDepth * 0.5f + HouseZ - RoomDepth * 0.5f - 0.9f) * 0.5f),
                new Vector3(1.3f, 0.03f, PlotDepth * 0.5f + HouseZ - RoomDepth * 0.5f - 0.9f), palette.Gravel);

            for (int i = 0; i < 14; i++)
            {
                float z = -PlotDepth * 0.5f + 0.7f + i * 0.78f;
                if (z > HouseZ - RoomDepth * 0.5f - 1.1f) break;

                Slab(plot.transform, "Dalle", new Vector3(FootpathX + (i % 2 == 0 ? 0.06f : -0.07f), 0.03f, z),
                    new Vector3(0.62f, 0.03f, 0.5f), night.Concrete)
                    .transform.localRotation = Quaternion.Euler(0f, i * 13f % 17f - 8f, 0f);
            }

            Slab(plot.transform, "Seuil", new Vector3(FootpathX, 0.012f, HouseZ - RoomDepth * 0.5f - 0.9f),
                new Vector3(2.6f, 0.03f, 1.8f), palette.Gravel);

            Driveway(plot.transform, night, palette);
            Fence(plot.transform, night, palette);
            Street(plot.transform, night, palette);

            // Deux lampadaires, au-dela de la cloture, cote route : ce sont les seules sources
            // exterieures avec la lampe du porche. Le reste du jardin est noir, et c'est ce
            // noir qui rend l'ampoule de l'interieur importante.
            StreetLamp(plot.transform, night, new Vector3(-8.5f, 0f, -PlotDepth * 0.5f - 1.6f));
            StreetLamp(plot.transform, night, new Vector3(11.2f, 0f, -PlotDepth * 0.5f - 1.6f));
        }

        /// <summary>X du chemin pieton : dans l'axe de la porte.</summary>
        private const float FootpathX = -0.6f;

        /// <summary>L'allee carrossable, le long du flanc droit de la maison.</summary>
        private const float DrivewayX = 7.8f;
        private const float DrivewayWidth = 3.4f;

        /// <summary>Où dort la voiture, sur l'allée : à hauteur de l'avant de la maison.</summary>
        private const float CarZ = 0.6f;

        /// <summary>
        /// L'allee, comme devant n'importe quelle maison : une bande de beton qui part de la
        /// rue, passe le portail et longe le flanc de la maison. La voiture y dort le nez vers
        /// le fond du terrain — pas en travers de la porte d'entree.
        /// </summary>
        private static void Driveway(Transform parent, NightMaterialFactory.Palette night, Palette palette)
        {
            GameObject drive = EditorBuildUtility.CreateEmpty("Allee", parent, Vector3.zero);
            Transform t = drive.transform;

            float from = -PlotDepth * 0.5f - 0.1f;
            float to = HouseZ + RoomDepth * 0.5f - 2.4f;
            float length = to - from;

            Box(t, "Beton", new Vector3(DrivewayX, 0.015f, (from + to) * 0.5f),
                new Vector3(DrivewayWidth, 0.05f, length), night.Concrete, true);

            // Les joints de dilatation, et deux fissures : un beton sans defaut est un sol de
            // parking neuf.
            for (float z = from + 2.5f; z < to - 0.5f; z += 2.5f)
            {
                Slab(t, "Joint", new Vector3(DrivewayX, 0.042f, z), new Vector3(DrivewayWidth, 0.004f, 0.03f),
                    night.DarkConcrete);
            }

            Slab(t, "Fissure", new Vector3(DrivewayX - 0.5f, 0.042f, -6.2f), new Vector3(0.02f, 0.004f, 1.9f),
                night.DarkConcrete).transform.localRotation = Quaternion.Euler(0f, 24f, 0f);

            Slab(t, "Fissure", new Vector3(DrivewayX + 0.7f, 0.042f, -1.4f), new Vector3(0.02f, 0.004f, 1.2f),
                night.DarkConcrete).transform.localRotation = Quaternion.Euler(0f, -31f, 0f);

            // La tache d'huile, sous le moteur : la voiture dort toujours au meme endroit.
            Cylinder(t, "Tache d'huile", new Vector3(DrivewayX + 0.1f, 0.041f, CarZ + 1.4f),
                new Vector3(0.9f, 0.002f, 0.7f), night.DarkMetal, false);

            Cylinder(t, "Tache d'huile", new Vector3(DrivewayX - 0.2f, 0.041f, CarZ + 1.1f),
                new Vector3(0.45f, 0.002f, 0.38f), night.DarkMetal, false);

            // Des herbes dans les joints, le long des bords.
            for (int i = 0; i < 12; i++)
            {
                float z = from + 0.8f + i * length / 12f;
                float x = DrivewayX + (i % 2 == 0 ? -1f : 1f) * (DrivewayWidth * 0.5f - 0.08f);

                Slab(t, "Touffe", new Vector3(x, 0.14f, z), new Vector3(0.24f, 0.24f, 0.02f), palette.Grass)
                    .transform.localRotation = Quaternion.Euler(0f, i * 53f, 0f);
            }
        }

        /// <summary>
        /// La rue devant le terrain : trottoir, bordure, chaussee mouillee, et deux maisons en
        /// face. Sans elle, la cloture donnait sur le vide, et une allee qui ne mene nulle part
        /// n'est pas une allee.
        /// </summary>
        private static void Street(Transform parent, NightMaterialFactory.Palette night, Palette palette)
        {
            GameObject street = EditorBuildUtility.CreateEmpty("Rue", parent, Vector3.zero);
            Transform t = street.transform;

            const float length = 64f;
            float fence = -PlotDepth * 0.5f;
            float near = fence - 2.1f;
            float far = near - 7f;

            Box(t, "Trottoir", new Vector3(0f, -0.18f, fence - 1.05f), new Vector3(length, 0.4f, 2.1f),
                night.Sidewalk, true);

            Box(t, "Bordure", new Vector3(0f, -0.14f, near - 0.07f), new Vector3(length, 0.36f, 0.14f),
                night.Curb, true);

            Box(t, "Chaussee", new Vector3(0f, -0.34f, (near + far) * 0.5f), new Vector3(length, 0.4f, 7f),
                night.WetAsphalt, true);

            for (float x = -length * 0.5f + 2f; x < length * 0.5f; x += 4.5f)
            {
                Slab(t, "Marquage", new Vector3(x, -0.138f, (near + far) * 0.5f), new Vector3(2.2f, 0.005f, 0.12f),
                    night.RoadPaint);
            }

            Box(t, "Bordure en face", new Vector3(0f, -0.14f, far + 0.07f), new Vector3(length, 0.36f, 0.14f),
                night.Curb, true);

            Box(t, "Trottoir en face", new Vector3(0f, -0.18f, far - 1f), new Vector3(length, 0.4f, 2f),
                night.Sidewalk, true);

            Box(t, "Terrain en face", new Vector3(0f, -0.2f, far - 9f), new Vector3(length, 0.4f, 14f),
                palette.Grass, true);

            // Les voisins : des maisons pareilles a la sienne, une fenetre allumee chez l'un.
            NeighbourHouse(t, night, palette, new Vector3(-9.5f, 0f, far - 8.5f), true);
            NeighbourHouse(t, night, palette, new Vector3(8.5f, 0f, far - 9.5f), false);

            // Limites invisibles : la rue se voit, elle ne se traverse pas. Le jeu est dans la
            // maison et la voiture ; un joueur qui part a pied vers les voisins ne trouverait
            // qu'un bord de monde.
            Wall(t, "Limite (en face)", new Vector3(0f, 1f, far - 1.5f), new Vector3(length, 3f, 0.2f));

            // Sur les cotes, la limite est dans l'alignement de la cloture : au-dela, le terrain
            // s'arrete, et un joueur qui longerait le trottoir tomberait.
            float side = PlotWidth * 0.5f + 0.2f;
            Wall(t, "Limite (gauche)", new Vector3(-side, 1f, (fence + far - 1.5f) * 0.5f), new Vector3(0.2f, 3f, fence - far + 1.5f));
            Wall(t, "Limite (droite)", new Vector3(side, 1f, (fence + far - 1.5f) * 0.5f), new Vector3(0.2f, 3f, fence - far + 1.5f));
        }

        private static void NeighbourHouse(Transform parent, NightMaterialFactory.Palette night, Palette palette,
            Vector3 position, bool lit)
        {
            GameObject house = EditorBuildUtility.CreateEmpty(lit ? "Voisins (allume)" : "Voisins", parent, position);
            Transform t = house.transform;

            const float width = 9f;
            const float depth = 7f;

            Box(t, "Murs", new Vector3(0f, WallHeight * 0.5f, 0f), new Vector3(width, WallHeight, depth), palette.Render, true);
            Roof(t, palette, width, depth, WallHeight, 28f, 0.4f);

            // Facade cote rue (+Z) : une porte et deux fenetres.
            Box(t, "Porte", new Vector3(-1.2f, 1f, depth * 0.5f + 0.02f), new Vector3(1f, 2f, 0.05f), night.Wood, false);

            for (int i = 0; i < 2; i++)
            {
                float x = i == 0 ? 1.6f : -3.4f;
                Material glass = lit && i == 0 ? night.GlowWarm : night.Glass;

                Box(t, "Fenetre", new Vector3(x, 1.45f, depth * 0.5f + 0.02f), new Vector3(1.2f, 1f, 0.04f), glass, false);
                Box(t, "Appui", new Vector3(x, 0.92f, depth * 0.5f + 0.08f), new Vector3(1.35f, 0.05f, 0.15f),
                    palette.Porcelain, false);
            }

            if (lit)
            {
                // La fenetre allumee eclaire vraiment un peu la pelouse devant elle.
                NightStreetBuilder.AddLight(t, "Lumiere de salon", new Vector3(1.6f, 1.5f, depth * 0.5f + 0.6f),
                    new Color(1f, 0.76f, 0.46f), 0.9f, 4.5f, false, false);
            }

            // La cloture basse du jardin de devant.
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Muret", new Vector3(side * (width * 0.5f + 1.2f), 0.35f, depth * 0.5f + 1.5f),
                    new Vector3(0.18f, 0.7f, 3f), night.DarkConcrete, false);
            }
        }

        private static void StreetLamp(Transform parent, NightMaterialFactory.Palette night, Vector3 position)
        {
            GameObject lamp = EditorBuildUtility.CreateEmpty("Lampadaire de rue", parent, position);

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

        /// <summary>Un mur invisible : un collider sans rendu.</summary>
        private static void Wall(Transform parent, string name, Vector3 position, Vector3 size)
        {
            GameObject wall = EditorBuildUtility.CreateEmpty(name, parent, position);
            BoxCollider collider = wall.AddComponent<BoxCollider>();
            collider.size = size;
        }

        private static void Fence(Transform parent, NightMaterialFactory.Palette night, Palette palette)
        {
            GameObject fence = EditorBuildUtility.CreateEmpty("Cloture", parent, Vector3.zero);

            float halfX = PlotWidth * 0.5f;
            float halfZ = PlotDepth * 0.5f;

            FenceRun(fence.transform, night, new Vector3(-halfX, 0f, halfZ), new Vector3(halfX, 0f, halfZ));
            FenceRun(fence.transform, night, new Vector3(-halfX, 0f, -halfZ), new Vector3(-halfX, 0f, halfZ));
            FenceRun(fence.transform, night, new Vector3(halfX, 0f, -halfZ), new Vector3(halfX, 0f, halfZ));

            // Deux passages cote rue, grands ouverts : le portillon du chemin et le portail de
            // l'allee. Une cloture qu'on ne peut pas franchir transforme le jardin en couloir,
            // et le joueur passe son temps a chercher la sortie.
            float gateLeft = FootpathX - 0.8f;
            float gateRight = FootpathX + 0.8f;
            float driveLeft = DrivewayX - DrivewayWidth * 0.5f - 0.2f;
            float driveRight = DrivewayX + DrivewayWidth * 0.5f + 0.2f;

            FenceRun(fence.transform, night, new Vector3(-halfX, 0f, -halfZ), new Vector3(gateLeft, 0f, -halfZ));
            FenceRun(fence.transform, night, new Vector3(gateRight, 0f, -halfZ), new Vector3(driveLeft, 0f, -halfZ));
            FenceRun(fence.transform, night, new Vector3(driveRight, 0f, -halfZ), new Vector3(halfX, 0f, -halfZ));

            // Le portillon, ouvert vers l'interieur et reste la : il ne ferme plus depuis longtemps.
            GameObject gate = EditorBuildUtility.CreateEmpty("Portillon", fence.transform, new Vector3(gateRight, 0f, -halfZ));
            gate.transform.localRotation = Quaternion.Euler(0f, 112f, 0f);

            for (int i = 0; i < 6; i++)
            {
                Box(gate.transform, "Barreau", new Vector3(-0.1f - i * 0.25f, 0.62f, 0f),
                    new Vector3(0.03f, 1.15f, 0.03f), night.Metal, false);
            }

            Box(gate.transform, "Traverse", new Vector3(-0.72f, 1.12f, 0f), new Vector3(1.45f, 0.05f, 0.04f), night.Rust, false);
            Box(gate.transform, "Traverse", new Vector3(-0.72f, 0.2f, 0f), new Vector3(1.45f, 0.05f, 0.04f), night.Rust, false);

            // Les piliers du portail de l'allee.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side < 0 ? driveLeft : driveRight;
                Box(fence.transform, "Pilier", new Vector3(x, 0.8f, -halfZ), new Vector3(0.36f, 1.6f, 0.36f),
                    night.DarkConcrete, true);
                Box(fence.transform, "Chapeau", new Vector3(x, 1.63f, -halfZ), new Vector3(0.44f, 0.06f, 0.44f),
                    night.Concrete, false);
            }

            Mailbox(fence.transform, night, palette, new Vector3(gateLeft - 0.35f, 0f, -halfZ - 0.12f));
        }

        /// <summary>La boîte aux lettres, qui déborde : le courrier ne s'arrête pas parce qu'on ne l'ouvre plus.</summary>
        private static void Mailbox(Transform parent, NightMaterialFactory.Palette night, Palette palette, Vector3 position)
        {
            GameObject box = EditorBuildUtility.CreateEmpty("Boite aux lettres", parent, position);
            Transform t = box.transform;

            Box(t, "Poteau", new Vector3(0f, 0.55f, 0f), new Vector3(0.08f, 1.1f, 0.08f), night.Wood, true);
            Box(t, "Boite", new Vector3(0f, 1.24f, 0f), new Vector3(0.38f, 0.3f, 0.26f), night.Rust, false);
            Box(t, "Toit", new Vector3(0f, 1.41f, 0f), new Vector3(0.42f, 0.04f, 0.3f), night.DarkMetal, false);

            // Des enveloppes qui depassent de la fente.
            for (int i = 0; i < 3; i++)
            {
                Slab(t, "Enveloppe", new Vector3(-0.06f + i * 0.05f, 1.33f + i * 0.012f, -0.14f),
                    new Vector3(0.2f, 0.1f, 0.004f), palette.Paper)
                    .transform.localRotation = Quaternion.Euler(-20f + i * 6f, 0f, i * 9f - 8f);
            }
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

            // Un seul collider pour tout le pan : les barreaux n'en ont pas, et entre deux
            // poteaux on passait a travers le grillage.
            BoxCollider solid = run.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, height * 0.5f, 0f);
            solid.size = new Vector3(0.12f, height, length);

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

            // Un vrai toit a deux pans, pas une dalle : c'est la silhouette qui fait lire
            // « maison » depuis l'allee.
            Roof(shell.transform, palette, RoomWidth + WallThickness, RoomDepth + WallThickness, WallHeight,
                RoofPitch, 0.45f);
            RoofDetails(shell.transform, night);

            Door(shell.transform, night, palette);
            Porch(shell.transform, night);

            // Les fenetres : vitres, cadres, appuis. Volets et carton scotche devant, un drap en
            // guise de rideau sur le cote.
            Window(shell.transform, night, palette, new Vector3(0f, 0f, -halfZ), 0f, 2.9f, 1.3f, 0.95f, 2.05f,
                WindowShutters | WindowCardboard);
            Window(shell.transform, night, palette, new Vector3(0f, 0f, halfZ), 180f, -2.4f, 1.1f, 1.25f, 2.1f, 0);
            Window(shell.transform, night, palette, new Vector3(-halfX, 0f, 0f), 90f, 1.6f, 1.0f, 1.1f, 2f,
                WindowCurtain);

            InteriorWalls(shell.transform, night, palette);

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

        private const float RoofPitch = 30f;

        private const int WindowShutters = 1;
        private const int WindowCurtain = 2;
        private const int WindowCardboard = 4;

        /// <summary>
        /// Toit à deux pans, faîtage le long de X, posé sur des murs de <paramref name="width"/>
        /// sur <paramref name="depth"/> (faces extérieures) dont le haut est à <paramref name="baseY"/>.
        /// Les pignons ferment les triangles aux deux bouts.
        /// </summary>
        private static void Roof(Transform parent, Palette palette, float width, float depth, float baseY,
            float pitch, float overhang)
        {
            GameObject roof = EditorBuildUtility.CreateEmpty("Toit", parent, Vector3.zero);
            Transform t = roof.transform;

            float halfZ = depth * 0.5f;
            float tan = Mathf.Tan(pitch * Mathf.Deg2Rad);
            float rise = halfZ * tan;
            float run = halfZ + overhang;
            float slope = run / Mathf.Cos(pitch * Mathf.Deg2Rad);
            const float thickness = 0.14f;

            for (int side = -1; side <= 1; side += 2)
            {
                // La face inferieure du pan passe par le haut du mur et monte jusqu'au faitage.
                Quaternion rotation = Quaternion.Euler(side * pitch, 0f, 0f);
                Vector3 normal = rotation * Vector3.up;
                Vector3 centre = new Vector3(0f, baseY + (rise - overhang * tan) * 0.5f, side * run * 0.5f) +
                                 normal * thickness * 0.5f;

                Box(t, "Pan", centre, new Vector3(width + overhang * 1.2f, thickness, slope), palette.Roof, false)
                    .transform.localRotation = rotation;
            }

            Box(t, "Faitage", new Vector3(0f, baseY + rise + 0.1f, 0f), new Vector3(width + overhang * 1.2f + 0.05f, 0.2f, 0.2f),
                palette.Roof, false).transform.localRotation = Quaternion.Euler(45f, 0f, 0f);

            Mesh prism = NightMeshFactory.Load(NightMeshFactory.Gable);
            if (prism == null)
            {
                NightMeshFactory.EnsureLibrary();
                prism = NightMeshFactory.Load(NightMeshFactory.Gable);
            }

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject gable = new GameObject("Pignon");
                gable.transform.SetParent(t, false);
                gable.transform.localPosition = new Vector3(side * (width * 0.5f - 0.12f), baseY - 0.02f, 0f);
                gable.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
                gable.transform.localScale = new Vector3(depth, rise + 0.02f, 0.24f);

                gable.AddComponent<MeshFilter>().sharedMesh = prism;
                gable.AddComponent<MeshRenderer>().sharedMaterial = palette.Render;
            }
        }

        /// <summary>Gouttières, descentes, cheminée, antenne : ce qui dépasse d'un toit habité.</summary>
        private static void RoofDetails(Transform shell, NightMaterialFactory.Palette night)
        {
            GameObject details = EditorBuildUtility.CreateEmpty("Toit (details)", shell, Vector3.zero);
            Transform t = details.transform;

            float halfX = (RoomWidth + WallThickness) * 0.5f;
            float halfZ = (RoomDepth + WallThickness) * 0.5f;
            float tan = Mathf.Tan(RoofPitch * Mathf.Deg2Rad);
            const float overhang = 0.45f;
            float eaveY = WallHeight - overhang * tan - 0.05f;
            float eaveZ = halfZ + overhang + 0.05f;
            float length = RoomWidth + WallThickness + overhang * 1.2f;

            for (int side = -1; side <= 1; side += 2)
            {
                Cylinder(t, "Gouttiere", new Vector3(0f, eaveY, side * eaveZ), new Vector3(0.13f, length * 0.5f, 0.13f),
                    night.DarkMetal, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }

            // Deux descentes, en diagonale ; celle de l'avant est decrochee en bas.
            Cylinder(t, "Descente", new Vector3(halfX + 0.12f, eaveY * 0.5f, -eaveZ), new Vector3(0.08f, eaveY * 0.5f, 0.08f),
                night.DarkMetal, false);
            Cylinder(t, "Descente", new Vector3(-halfX - 0.12f, eaveY * 0.5f + 0.2f, eaveZ), new Vector3(0.08f, eaveY * 0.5f - 0.2f, 0.08f),
                night.DarkMetal, false).transform.localRotation = Quaternion.Euler(4f, 0f, -3f);

            // La cheminee sort du pan arriere.
            float z = 1.9f;
            float roofY = WallHeight + (halfZ - z) * tan;
            float ridgeY = WallHeight + halfZ * tan;
            float top = ridgeY + 0.35f;

            Box(t, "Cheminee", new Vector3(2.6f, (roofY - 0.2f + top) * 0.5f, z), new Vector3(0.62f, top - roofY + 0.2f, 0.62f),
                night.DarkBrick, false);
            Box(t, "Couronne", new Vector3(2.6f, top + 0.04f, z), new Vector3(0.72f, 0.08f, 0.72f), night.DarkConcrete, false);
            Cylinder(t, "Conduit", new Vector3(2.6f, top + 0.18f, z), new Vector3(0.16f, 0.12f, 0.16f), night.Rust, false);

            // L'antenne rateau, un peu de travers.
            GameObject antenna = EditorBuildUtility.CreateEmpty("Antenne", t, new Vector3(-3.1f, ridgeY + 0.1f, 0f));
            antenna.transform.localRotation = Quaternion.Euler(0f, 30f, 6f);

            Cylinder(antenna.transform, "Mat", new Vector3(0f, 0.6f, 0f), new Vector3(0.035f, 0.6f, 0.035f), night.Metal, false);
            Box(antenna.transform, "Bras", new Vector3(0f, 1.12f, 0f), new Vector3(0.03f, 0.03f, 1.1f), night.Metal, false);

            for (int i = 0; i < 5; i++)
            {
                Box(antenna.transform, "Brin", new Vector3(0f, 1.12f, -0.45f + i * 0.22f), new Vector3(0.7f - i * 0.08f, 0.015f, 0.015f),
                    night.Metal, false);
            }
        }

        /// <summary>La porte d'entrée : encadrement, battant resté entrouvert, poignée.</summary>
        private static void Door(Transform shell, NightMaterialFactory.Palette night, Palette palette)
        {
            float halfZ = RoomDepth * 0.5f;
            float x = FootpathX;
            GameObject door = EditorBuildUtility.CreateEmpty("Porte d'entree", shell, new Vector3(x, 0f, -halfZ));
            Transform t = door.transform;

            // Encadrement (des deux cotes du mur).
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Montant", new Vector3(side * (DoorWidth * 0.5f + 0.04f), DoorHeight * 0.5f, 0f),
                    new Vector3(0.08f, DoorHeight + 0.04f, 0.3f), palette.Laminate, false);
            }

            Box(t, "Linteau", new Vector3(0f, DoorHeight + 0.04f, 0f), new Vector3(DoorWidth + 0.16f, 0.08f, 0.3f),
                palette.Laminate, false);

            Box(t, "Seuil", new Vector3(0f, 0.03f, 0f), new Vector3(DoorWidth, 0.03f, 0.26f), night.Metal, false);

            // Le battant, ouvert vers l'interieur contre le mur : on le voit, il ne gene pas.
            GameObject hinge = EditorBuildUtility.CreateEmpty("Gond", t, new Vector3(-DoorWidth * 0.5f + 0.02f, 0f, 0.12f));
            hinge.transform.localRotation = Quaternion.Euler(0f, -76f, 0f);

            Box(hinge.transform, "Battant", new Vector3(DoorWidth * 0.5f - 0.02f, DoorHeight * 0.5f, 0f),
                new Vector3(DoorWidth - 0.04f, DoorHeight - 0.03f, 0.045f), night.Wood, false);

            // Une vitre depolie en haut du battant, et la poignee.
            Box(hinge.transform, "Imposte", new Vector3(DoorWidth * 0.5f - 0.02f, DoorHeight - 0.42f, 0f),
                new Vector3(0.5f, 0.42f, 0.05f), night.Glass, false);

            Cylinder(hinge.transform, "Poignee", new Vector3(DoorWidth - 0.14f, 1.02f, 0.05f), new Vector3(0.03f, 0.06f, 0.03f),
                night.Chrome, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
        }

        /// <summary>
        /// Le porche : un auvent, une applique fatiguée au-dessus de la porte, un paillasson et le
        /// numéro. La lampe du porche est la seule lumière de la façade : elle dit où est l'entrée.
        /// </summary>
        private static void Porch(Transform shell, NightMaterialFactory.Palette night)
        {
            float halfZ = RoomDepth * 0.5f + WallThickness * 0.5f;
            GameObject porch = EditorBuildUtility.CreateEmpty("Porche", shell, new Vector3(FootpathX, 0f, -halfZ));
            Transform t = porch.transform;

            Box(t, "Auvent", new Vector3(0f, DoorHeight + 0.32f, -0.42f), new Vector3(1.7f, 0.06f, 0.9f), night.DarkMetal, false)
                .transform.localRotation = Quaternion.Euler(-10f, 0f, 0f);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Console", new Vector3(side * 0.72f, DoorHeight + 0.14f, -0.28f), new Vector3(0.05f, 0.3f, 0.55f),
                    night.DarkMetal, false).transform.localRotation = Quaternion.Euler(-34f, 0f, 0f);
            }

            Box(t, "Applique", new Vector3(-0.85f, DoorHeight - 0.05f, -0.08f), new Vector3(0.16f, 0.24f, 0.12f),
                night.NeonWarm, false);

            Light light = NightStreetBuilder.AddLight(t, "Lampe du porche", new Vector3(-0.85f, DoorHeight - 0.1f, -0.45f),
                new Color(1f, 0.72f, 0.42f), 1.3f, 6f, false, true);
            light.shadowNormalBias = 0.3f;

            AddFlicker(porch, night, 1.3f);

            Slab(t, "Paillasson", new Vector3(0f, 0.125f, -0.55f), new Vector3(0.85f, 0.015f, 0.5f), night.Rubber);

            Box(t, "Numero", new Vector3(0.8f, 1.6f, -0.02f), new Vector3(0.18f, 0.14f, 0.02f), night.Chrome, false);
        }

        /// <summary>
        /// Une fenêtre dans un percement de mur : cadre, deux vitres, appuis dehors et dedans.
        /// Le repère est celui du mur (<paramref name="wallPosition"/>, <paramref name="yaw"/>) :
        /// dehors vers -Z local, comme pour <see cref="Panel"/>.
        /// </summary>
        private static void Window(Transform shell, NightMaterialFactory.Palette night, Palette palette,
            Vector3 wallPosition, float yaw, float along, float width, float bottom, float top, int style)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            GameObject window = EditorBuildUtility.CreateEmpty("Fenetre", shell, wallPosition + rotation * new Vector3(along, 0f, 0f));
            window.transform.localRotation = rotation;
            Transform t = window.transform;

            float height = top - bottom;
            float middle = (bottom + top) * 0.5f;

            // Cadre en PVC jauni.
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Montant", new Vector3(side * (width * 0.5f - 0.03f), middle, 0f), new Vector3(0.06f, height, 0.08f),
                    palette.Porcelain, false);
            }

            Box(t, "Traverse haute", new Vector3(0f, top - 0.03f, 0f), new Vector3(width, 0.06f, 0.08f), palette.Porcelain, false);
            Box(t, "Traverse basse", new Vector3(0f, bottom + 0.03f, 0f), new Vector3(width, 0.06f, 0.08f), palette.Porcelain, false);
            Box(t, "Meneau", new Vector3(0f, middle, 0f), new Vector3(0.05f, height, 0.07f), palette.Porcelain, false);

            // Les vitres, et un seul collider pour tout le percement.
            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Vitre", new Vector3(side * width * 0.25f, middle, 0f), new Vector3(width * 0.5f - 0.08f, height - 0.12f, 0.012f),
                    night.Glass, false);
            }

            BoxCollider solid = window.AddComponent<BoxCollider>();
            solid.center = new Vector3(0f, middle, 0f);
            solid.size = new Vector3(width, height, 0.1f);

            Box(t, "Appui", new Vector3(0f, bottom - 0.025f, -0.17f), new Vector3(width + 0.14f, 0.05f, 0.2f),
                night.Concrete, false);
            Box(t, "Tablette", new Vector3(0f, bottom - 0.015f, 0.17f), new Vector3(width + 0.06f, 0.03f, 0.14f),
                palette.Laminate, false);

            if ((style & WindowShutters) != 0)
            {
                // Deux volets rabattus contre la facade ; celui de droite ne tient plus qu'a un gond.
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject leaf = EditorBuildUtility.CreateEmpty("Volet", t,
                        new Vector3(side * (width * 0.75f + 0.03f), middle, -0.16f));

                    if (side > 0) leaf.transform.localRotation = Quaternion.Euler(0f, -14f, -8f);

                    Box(leaf.transform, "Battant", Vector3.zero, new Vector3(width * 0.5f, height + 0.04f, 0.03f), night.Wood, false);

                    for (int i = 0; i < 6; i++)
                    {
                        Box(leaf.transform, "Lame", new Vector3(0f, -height * 0.4f + i * height * 0.16f, -0.02f),
                            new Vector3(width * 0.46f, 0.03f, 0.015f), night.Wood, false)
                            .transform.localRotation = Quaternion.Euler(-30f, 0f, 0f);
                    }
                }
            }

            if ((style & WindowCardboard) != 0)
            {
                // Une vitre fendue, bouchee au carton et au scotch.
                Slab(t, "Carton", new Vector3(width * 0.25f, bottom + height * 0.62f, 0.02f),
                    new Vector3(width * 0.5f - 0.1f, height * 0.5f, 0.01f), palette.Paper);

                for (int i = -1; i <= 1; i += 2)
                {
                    Slab(t, "Scotch", new Vector3(width * 0.25f, bottom + height * 0.62f + i * height * 0.2f, 0.028f),
                        new Vector3(width * 0.5f - 0.02f, 0.045f, 0.004f), night.Plastic)
                        .transform.localRotation = Quaternion.Euler(0f, 0f, i * 6f);
                }
            }

            if ((style & WindowCurtain) != 0)
            {
                Cylinder(t, "Tringle", new Vector3(0f, top + 0.1f, 0.2f), new Vector3(0.025f, width * 0.5f + 0.2f, 0.025f),
                    night.DarkMetal, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

                // Un drap en guise de rideau, tire d'un cote seulement.
                Slab(t, "Drap", new Vector3(-width * 0.2f, middle - 0.05f, 0.24f), new Vector3(width * 0.62f, height + 0.3f, 0.012f),
                    palette.Blanket).transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
                Box(t, "Drap (plis)", new Vector3(width * 0.5f + 0.1f, middle - 0.05f, 0.24f), new Vector3(0.16f, height + 0.25f, 0.07f),
                    palette.Blanket, false);
            }
        }

        /// <summary>
        /// Les murs de l'intérieur : plinthes, taches d'humidité, papier peint qui se décolle,
        /// interrupteur. Personne ne regarde une plinthe ; tout le monde remarque qu'il n'y en a pas.
        /// </summary>
        private static void InteriorWalls(Transform shell, NightMaterialFactory.Palette night, Palette palette)
        {
            GameObject walls = EditorBuildUtility.CreateEmpty("Murs (details)", shell, Vector3.zero);
            Transform t = walls.transform;

            float inX = RoomWidth * 0.5f - 0.12f;
            float inZ = RoomDepth * 0.5f - 0.12f;

            // Plinthes. Celle du mur avant s'interrompt a la porte.
            Box(t, "Plinthe", new Vector3(-inX, 0.05f, 0f), new Vector3(0.02f, 0.08f, RoomDepth - 0.24f), palette.Laminate, false);
            Box(t, "Plinthe", new Vector3(inX, 0.05f, 0f), new Vector3(0.02f, 0.08f, RoomDepth - 0.24f), palette.Laminate, false);
            Box(t, "Plinthe", new Vector3(0f, 0.05f, inZ), new Vector3(RoomWidth - 0.24f, 0.08f, 0.02f), palette.Laminate, false);

            float doorLeft = FootpathX - DoorWidth * 0.5f - 0.1f;
            float doorRight = FootpathX + DoorWidth * 0.5f + 0.1f;
            Box(t, "Plinthe", new Vector3((-inX + doorLeft) * 0.5f, 0.05f, -inZ), new Vector3(doorLeft + inX, 0.08f, 0.02f),
                palette.Laminate, false);
            Box(t, "Plinthe", new Vector3((inX + doorRight) * 0.5f, 0.05f, -inZ), new Vector3(inX - doorRight, 0.08f, 0.02f),
                palette.Laminate, false);

            // Humidite : des taches qui se chevauchent dans les angles, la ou l'air ne circule pas.
            Stain(t, palette, new Vector3(-inX + 0.005f, 2.2f, 2.6f), 90f, 1.4f, 0.8f, 11);
            Stain(t, palette, new Vector3(-inX + 0.005f, 0.45f, 3.2f), 90f, 0.9f, 0.6f, 12);
            Stain(t, palette, new Vector3(1.6f, 2.25f, inZ - 0.005f), 180f, 1.2f, 0.7f, 13);
            Stain(t, palette, new Vector3(3.2f, 2.3f, -inZ + 0.005f), 0f, 0.9f, 0.55f, 14);

            // Au plafond, au-dessus du matelas.
            for (int i = 0; i < 4; i++)
            {
                Cylinder(t, "Tache (plafond)", new Vector3(-3.3f + i * 0.28f, WallHeight - 0.004f, 2.2f + (i % 2) * 0.3f),
                    new Vector3(0.9f - i * 0.12f, 0.002f, 0.7f - i * 0.1f), palette.Stain, false);
            }

            // Le papier peint qui se decolle en lambeaux, en haut du mur de gauche.
            for (int i = 0; i < 2; i++)
            {
                GameObject strip = EditorBuildUtility.CreateEmpty("Lambeau", t, new Vector3(-inX + 0.01f, 2.4f, 0.4f + i * 0.55f));
                strip.transform.localRotation = Quaternion.Euler(0f, 90f, 0f) * Quaternion.Euler(-14f - i * 10f, 0f, i * 4f);
                Slab(strip.transform, "Papier", new Vector3(0f, -0.32f, 0.01f), new Vector3(0.42f - i * 0.1f, 0.64f, 0.004f),
                    palette.Wallpaper);
            }

            Box(t, "Interrupteur", new Vector3(FootpathX + 0.85f, 1.1f, -inZ + 0.01f), new Vector3(0.08f, 0.12f, 0.02f),
                palette.Porcelain, false);
        }

        /// <summary>Une tache organique : quelques disques aplatis qui se chevauchent, plaqués au mur.</summary>
        private static void Stain(Transform parent, Palette palette, Vector3 position, float yaw, float width, float height, int seed)
        {
            GameObject stain = EditorBuildUtility.CreateEmpty("Tache d'humidite", parent, position);
            stain.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            System.Random random = new System.Random(seed);

            for (int i = 0; i < 5; i++)
            {
                float x = ((float)random.NextDouble() - 0.5f) * width * 0.6f;
                float y = ((float)random.NextDouble() - 0.5f) * height * 0.6f;
                float w = width * (0.35f + (float)random.NextDouble() * 0.45f);
                float h = height * (0.35f + (float)random.NextDouble() * 0.45f);

                Cylinder(stain.transform, "Aureole", new Vector3(x, y, 0.001f * i), new Vector3(w, 0.002f, h), palette.Stain, false)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }
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
                new Vector3(0.45f, 2.1f, RoomDepth * 0.5f - 0.13f));

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

            // La vaisselle de trois jours dans l'evier, une casserole sur le feu, un micro-ondes.
            for (int i = 0; i < 4; i++)
            {
                Cylinder(t, "Assiette", new Vector3(-2.32f + i * 0.015f, 0.82f + i * 0.018f, wallZ - 0.02f),
                    new Vector3(0.24f, 0.008f, 0.24f), palette.Porcelain, false)
                    .transform.localRotation = Quaternion.Euler(i * 5f - 6f, 0f, 8f - i * 3f);
            }

            Cylinder(t, "Tasse", new Vector3(-2.1f, 0.86f, wallZ + 0.1f), new Vector3(0.08f, 0.05f, 0.08f),
                palette.Porcelain, false);

            Cylinder(t, "Casserole", new Vector3(-0.5f, 1.04f, wallZ), new Vector3(0.22f, 0.07f, 0.22f), night.Metal, false);
            Box(t, "Queue", new Vector3(-0.28f, 1.08f, wallZ - 0.12f), new Vector3(0.24f, 0.025f, 0.035f), night.DarkMetal, false)
                .transform.localRotation = Quaternion.Euler(0f, 28f, 0f);

            Box(t, "Micro-ondes", new Vector3(-1.55f, 1.11f, wallZ + 0.06f), new Vector3(0.5f, 0.3f, 0.36f), night.DarkMetal, false);
            Box(t, "Hublot", new Vector3(-1.62f, 1.11f, wallZ - 0.125f), new Vector3(0.3f, 0.2f, 0.01f), night.Glass, false);

            Cylinder(t, "Bouilloire", new Vector3(0.05f, 1.06f, wallZ + 0.08f), new Vector3(0.16f, 0.1f, 0.16f), night.Plastic, false);

            // Le calendrier de la poste, jamais tourne depuis janvier.
            GameObject calendar = EditorBuildUtility.CreateEmpty("Calendrier", t, new Vector3(3.3f, 1.45f, RoomDepth * 0.5f - 0.125f));
            Slab(calendar.transform, "Feuilles", Vector3.zero, new Vector3(0.3f, 0.42f, 0.006f), palette.Paper);
            Slab(calendar.transform, "Image", new Vector3(0f, 0.11f, -0.004f), new Vector3(0.28f, 0.17f, 0.004f), night.Carpet);
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

            // --- le coin tele : un canape defonce contre le mur de droite, la tele en face,
            // posee sur une caisse. (Positions relatives au salon, qui est decale de -2,3 / -1,5.)
            Sofa(t, night, palette, new Vector3(6.72f, 0f, 0.3f));

            NightStreetBuilder.Crate(t, night, new Vector3(4.2f, 0f, 0.3f), 0.6f, -6f);

            GameObject tv = EditorBuildUtility.CreateEmpty("Televiseur", t, new Vector3(4.2f, 0.6f, 0.3f));
            tv.transform.localRotation = Quaternion.Euler(0f, -84f, 0f);

            // La rallonge, du mur a la tele, en travers du passage.
            Cylinder(t, "Rallonge", new Vector3(5.84f, 0.02f, 1.45f), new Vector3(0.014f, 1.34f, 0.014f), night.Rubber, false)
                .transform.localRotation = Quaternion.Euler(0f, 4f, 90f);

            Radiator(t, night, palette, new Vector3(-2.5f, 0f, -0.1f));
            Entry(t, night, palette, new Vector3(3.25f, 0f, -2.38f));

            Box(tv.transform, "Caisson", new Vector3(0f, 0.22f, 0f),
                new Vector3(0.58f, 0.44f, 0.5f), palette.Laminate, true);

            Box(tv.transform, "Dalle", new Vector3(0f, 0.24f, -0.26f),
                new Vector3(0.44f, 0.33f, 0.03f), night.Glass, false);

            NightStreetBuilder.MakePhysical(tv, 11f, PhysicsProp.Matter.Plastique, 0.1f);
        }

        /// <summary>
        /// Le canapé, récupéré sur le trottoir : assise creusée, un coussin de travers, une
        /// couverture jetée dessus. Son dos est contre le mur, il regarde la télé.
        /// </summary>
        private static void Sofa(Transform parent, NightMaterialFactory.Palette night, Palette palette, Vector3 position)
        {
            GameObject sofa = EditorBuildUtility.CreateEmpty("Canape", parent, position);
            sofa.transform.localRotation = Quaternion.Euler(0f, -90f, 0f);
            Transform t = sofa.transform;

            Box(t, "Socle", new Vector3(0f, 0.2f, 0f), new Vector3(1.9f, 0.34f, 0.85f), palette.Sofa, true);
            Box(t, "Dossier", new Vector3(0f, 0.62f, -0.34f), new Vector3(1.9f, 0.52f, 0.2f), palette.Sofa, true);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Accoudoir", new Vector3(side * 0.86f, 0.5f, 0f), new Vector3(0.18f, 0.3f, 0.85f), palette.Sofa, false);
            }

            // Deux coussins d'assise, dont un affaisse.
            Box(t, "Coussin", new Vector3(-0.39f, 0.43f, 0.06f), new Vector3(0.74f, 0.13f, 0.64f), palette.Sofa, false);
            Box(t, "Coussin", new Vector3(0.39f, 0.41f, 0.06f), new Vector3(0.74f, 0.1f, 0.64f), palette.Sofa, false)
                .transform.localRotation = Quaternion.Euler(-4f, 0f, 3f);

            Box(t, "Coussin de dos", new Vector3(0.45f, 0.72f, -0.18f), new Vector3(0.5f, 0.42f, 0.14f), palette.Sofa, false)
                .transform.localRotation = Quaternion.Euler(-16f, 12f, 9f);

            Box(t, "Couverture", new Vector3(-0.55f, 0.52f, 0.02f), new Vector3(0.7f, 0.05f, 0.8f), palette.Blanket, false)
                .transform.localRotation = Quaternion.Euler(0f, 14f, -4f);
            Box(t, "Couverture (pan)", new Vector3(-0.62f, 0.36f, 0.45f), new Vector3(0.6f, 0.34f, 0.04f), palette.Blanket, false)
                .transform.localRotation = Quaternion.Euler(8f, 14f, 0f);

            // Les pieds.
            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(t, "Pied", new Vector3(x * 0.85f, 0.02f, z * 0.36f), new Vector3(0.06f, 0.05f, 0.06f), night.Wood, false);
                }
            }

            // La telecommande, et une canette par terre au pied du canape.
            Box(t, "Telecommande", new Vector3(0.62f, 0.5f, 0.18f), new Vector3(0.05f, 0.02f, 0.17f), night.Plastic, false)
                .transform.localRotation = Quaternion.Euler(0f, 22f, 0f);
            Cylinder(t, "Canette", new Vector3(0.2f, 0.06f, 0.62f), new Vector3(0.066f, 0.06f, 0.066f), night.Chrome, false);
        }

        /// <summary>Le radiateur en fonte sous la fenêtre. Froid : l'électricité coûte trop cher.</summary>
        private static void Radiator(Transform parent, NightMaterialFactory.Palette night, Palette palette, Vector3 position)
        {
            GameObject radiator = EditorBuildUtility.CreateEmpty("Radiateur", parent, position);
            Transform t = radiator.transform;

            for (int i = 0; i < 9; i++)
            {
                Box(t, "Element", new Vector3(0f, 0.5f, -0.4f + i * 0.1f), new Vector3(0.09f, 0.56f, 0.07f), palette.Porcelain, false);
            }

            Cylinder(t, "Tuyau", new Vector3(0f, 0.08f, 0.52f), new Vector3(0.03f, 0.08f, 0.03f), night.Rust, false);
            Box(t, "Chaussettes", new Vector3(0.02f, 0.8f, 0.1f), new Vector3(0.12f, 0.03f, 0.32f), palette.Blanket, false);
        }

        /// <summary>L'entrée : un porte-manteau avec le blouson, des chaussures jetées en dessous.</summary>
        private static void Entry(Transform parent, NightMaterialFactory.Palette night, Palette palette, Vector3 position)
        {
            GameObject entry = EditorBuildUtility.CreateEmpty("Entree", parent, position);
            Transform t = entry.transform;

            Box(t, "Patere", new Vector3(0f, 1.75f, 0.03f), new Vector3(0.62f, 0.06f, 0.03f), night.Wood, false);

            for (int i = 0; i < 3; i++)
            {
                Cylinder(t, "Crochet", new Vector3(-0.22f + i * 0.22f, 1.74f, 0.08f), new Vector3(0.02f, 0.05f, 0.02f),
                    night.Chrome, false).transform.localRotation = Quaternion.Euler(70f, 0f, 0f);
            }

            Box(t, "Blouson", new Vector3(-0.2f, 1.36f, 0.11f), new Vector3(0.46f, 0.72f, 0.12f), night.Rubber, false)
                .transform.localRotation = Quaternion.Euler(4f, 0f, 3f);
            Box(t, "Sweat", new Vector3(0.12f, 1.42f, 0.12f), new Vector3(0.38f, 0.6f, 0.1f), palette.Blanket, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -5f);

            for (int i = 0; i < 2; i++)
            {
                Box(t, "Chaussure", new Vector3(-0.3f + i * 0.2f, 0.05f, 0.35f + i * 0.08f), new Vector3(0.11f, 0.1f, 0.29f),
                    night.Rubber, false).transform.localRotation = Quaternion.Euler(0f, i * 38f - 12f, i * 70f);
            }
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

            GameObject carton = Box(clutter.transform, "Carton", new Vector3(3.9f, 0.18f, -3.25f),
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
            // Elle dort sur l'allee, le nez vers le fond du terrain, comme devant n'importe
            // quelle maison — pas plantee devant la porte d'entree.
            NightStreetBuilder.Car(t, night, new Vector3(DrivewayX, 0.04f, CarZ), -90f, true, false);

            // Le conducteur monte cote gauche, donc cote maison.
            GameObject door = EditorBuildUtility.CreateEmpty("Portiere conducteur", t,
                new Vector3(DrivewayX - 1.15f, 1f, CarZ + 0.2f));

            BoxCollider collider = door.AddComponent<BoxCollider>();
            collider.size = new Vector3(1.4f, 1.8f, 2.2f);
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

            SideOfHouse(t, night);
            Bins(t, night, new Vector3(DrivewayX + DrivewayWidth * 0.5f + 0.9f, 0f, -PlotDepth * 0.5f + 1.1f));
            Clothesline(t, night, palette, new Vector3(-7.5f, 0f, 9.4f), new Vector3(-1.5f, 0f, 10.2f));
            Tyres(t, night, new Vector3(-10.4f, 0f, -1.8f));
            GardenChair(t, night, new Vector3(-8.6f, 0f, 3.2f));
            Hedge(t, palette, new Vector3(-PlotWidth * 0.5f + 0.55f, 0f, -PlotDepth * 0.5f + 1f),
                new Vector3(-PlotWidth * 0.5f + 0.55f, 0f, PlotDepth * 0.5f - 1f));

            // Herbes hautes : des lames plates orientées au hasard. Rien ne dit mieux
            // « personne ne tond ici » et ça coûte quelques boîtes.
            GameObject weeds = EditorBuildUtility.CreateEmpty("Herbes folles", t, Vector3.zero);

            for (int i = 0; i < 34; i++)
            {
                float angle = i * 2.39996f;
                float radius = 2.2f + (i % 7) * 1.35f;

                Vector3 position = new Vector3(Mathf.Cos(angle) * radius, 0.22f,
                    Mathf.Sin(angle) * radius * 0.8f - 3.2f);

                // Pas d'herbes hautes sur l'allee ni sur le chemin.
                if (Mathf.Abs(position.x - DrivewayX) < DrivewayWidth * 0.5f + 0.2f) continue;
                if (Mathf.Abs(position.x - FootpathX) < 0.8f && position.z < HouseZ - RoomDepth * 0.5f) continue;

                GameObject blade = Slab(weeds.transform, "Touffe", position,
                    new Vector3(0.34f, 0.45f, 0.02f), palette.Grass);

                blade.transform.localRotation = Quaternion.Euler(0f, i * 47f, i % 3 * 7f - 7f);
            }

            NightStreetBuilder.Bottles(t, night, new Vector3(-1.8f, 0f, -4.2f), 4);
        }

        /// <summary>
        /// Le flanc de la maison côté allée : le compteur électrique et une applique à
        /// détecteur, qui éclaire la voiture. Sans elle, la voiture qu'on doit rejoindre
        /// serait la seule chose invisible du jardin.
        /// </summary>
        private static void SideOfHouse(Transform parent, NightMaterialFactory.Palette night)
        {
            float x = RoomWidth * 0.5f + WallThickness * 0.5f;
            GameObject side = EditorBuildUtility.CreateEmpty("Flanc (allee)", parent, new Vector3(x, 0f, HouseZ));
            Transform t = side.transform;

            Box(t, "Coffret electrique", new Vector3(0.1f, 1.15f, -2.6f), new Vector3(0.18f, 0.62f, 0.42f), night.Plastic, false);
            Box(t, "Gaine", new Vector3(0.06f, 0.42f, -2.6f), new Vector3(0.06f, 0.84f, 0.06f), night.DarkMetal, false);

            Box(t, "Applique", new Vector3(0.1f, 2.25f, -3.2f), new Vector3(0.16f, 0.14f, 0.22f), night.NeonWhite, false);

            GameObject lightGo = EditorBuildUtility.CreateEmpty("Lumiere de l'allee", t, new Vector3(0.3f, 2.2f, -3.2f));
            lightGo.transform.localRotation = Quaternion.LookRotation(new Vector3(0.8f, -1f, 0.1f).normalized, Vector3.up);

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Spot;
            light.spotAngle = 110f;
            light.color = new Color(0.86f, 0.9f, 1f);
            light.intensity = 1.8f;
            light.range = 9f;
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = LightShadows.Soft;
            light.shadowNormalBias = 0.3f;

            NightStreetBuilder.MakeVolumetric(light, 0.4f);
        }

        /// <summary>Deux poubelles à roulettes à l'entrée de l'allée, un sac posé à côté.</summary>
        private static void Bins(Transform parent, NightMaterialFactory.Palette night, Vector3 position)
        {
            GameObject bins = EditorBuildUtility.CreateEmpty("Poubelles", parent, position);
            Transform t = bins.transform;

            for (int i = 0; i < 2; i++)
            {
                GameObject bin = EditorBuildUtility.CreateEmpty("Poubelle", t, new Vector3(i * 0.72f, 0f, i * 0.1f));
                bin.transform.localRotation = Quaternion.Euler(0f, i * 9f - 4f, 0f);

                Material body = i == 0 ? night.Plastic : night.DarkMetal;
                Box(bin.transform, "Cuve", new Vector3(0f, 0.5f, 0f), new Vector3(0.58f, 0.9f, 0.7f), body, true);

                // Le couvercle de la premiere ne ferme plus : elle deborde.
                Box(bin.transform, "Couvercle", new Vector3(0f, i == 0 ? 1.0f : 0.97f, i == 0 ? 0.05f : 0f),
                    new Vector3(0.62f, 0.05f, 0.74f), body, false)
                    .transform.localRotation = Quaternion.Euler(i == 0 ? -18f : 0f, 0f, 0f);

                for (int side = -1; side <= 1; side += 2)
                {
                    Cylinder(bin.transform, "Roue", new Vector3(side * 0.24f, 0.09f, -0.34f), new Vector3(0.18f, 0.03f, 0.18f),
                        night.Rubber, false).transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
                }
            }

            GameObject bag = EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Sac", t,
                new Vector3(1.35f, 0.26f, 0.2f), new Vector3(0.5f, 0.5f, 0.46f), night.DarkMetal, true);
            NightStreetBuilder.MakePhysical(bag, 2f, PhysicsProp.Matter.Mou, 1.2f);
        }

        /// <summary>L'étendoir derrière la maison : un tee-shirt et une serviette oubliés sous la bruine.</summary>
        private static void Clothesline(Transform parent, NightMaterialFactory.Palette night, Palette palette, Vector3 from, Vector3 to)
        {
            GameObject line = EditorBuildUtility.CreateEmpty("Etendoir", parent, (from + to) * 0.5f);
            Transform t = line.transform;

            Vector3 delta = to - from;
            float length = delta.magnitude;
            line.transform.localRotation = Quaternion.LookRotation(delta / length, Vector3.up);

            for (int side = -1; side <= 1; side += 2)
            {
                Cylinder(t, "Poteau", new Vector3(0f, 0.95f, side * length * 0.5f), new Vector3(0.06f, 0.95f, 0.06f), night.Metal, true);
                Box(t, "Traverse", new Vector3(0f, 1.86f, side * length * 0.5f), new Vector3(0.5f, 0.04f, 0.04f), night.Metal, false);
            }

            for (int wire = -1; wire <= 1; wire += 2)
            {
                Cylinder(t, "Fil", new Vector3(wire * 0.2f, 1.84f, 0f), new Vector3(0.01f, length * 0.5f, 0.01f), night.DarkMetal, false)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            Box(t, "Tee-shirt", new Vector3(-0.2f, 1.52f, -0.8f), new Vector3(0.04f, 0.62f, 0.52f), palette.Paper, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 4f);
            Box(t, "Serviette", new Vector3(0.2f, 1.46f, 1.1f), new Vector3(0.03f, 0.72f, 0.7f), palette.Blanket, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -3f);
        }

        /// <summary>Une pile de vieux pneus contre la clôture.</summary>
        private static void Tyres(Transform parent, NightMaterialFactory.Palette night, Vector3 position)
        {
            GameObject tyres = EditorBuildUtility.CreateEmpty("Pneus", parent, position);
            Transform t = tyres.transform;

            for (int i = 0; i < 3; i++)
            {
                Cylinder(t, "Pneu", new Vector3(i * 0.04f, 0.1f + i * 0.2f, -i * 0.03f), new Vector3(0.66f, 0.1f, 0.66f), night.Rubber, i == 0)
                    .transform.localRotation = Quaternion.Euler(i * 3f, 0f, -i * 2f);
                Cylinder(t, "Creux", new Vector3(i * 0.04f, 0.2f + i * 0.2f, -i * 0.03f), new Vector3(0.36f, 0.005f, 0.36f), night.DarkMetal, false);
            }

            // Un quatrieme, couche contre la pile.
            Cylinder(t, "Pneu", new Vector3(0.55f, 0.33f, 0.2f), new Vector3(0.66f, 0.1f, 0.66f), night.Rubber, false)
                .transform.localRotation = Quaternion.Euler(0f, 30f, 72f);
        }

        /// <summary>Une chaise de jardin en plastique, renversée depuis la dernière tempête.</summary>
        private static void GardenChair(Transform parent, NightMaterialFactory.Palette night, Vector3 position)
        {
            GameObject chair = EditorBuildUtility.CreateEmpty("Chaise de jardin", parent, position);
            chair.transform.localRotation = Quaternion.Euler(0f, 37f, 0f) * Quaternion.Euler(0f, 0f, 84f);
            Transform t = chair.transform;

            Box(t, "Assise", new Vector3(0f, 0.44f, 0f), new Vector3(0.46f, 0.04f, 0.44f), night.Plastic, false);
            Box(t, "Dossier", new Vector3(0f, 0.72f, -0.21f), new Vector3(0.46f, 0.52f, 0.04f), night.Plastic, false)
                .transform.localRotation = Quaternion.Euler(-8f, 0f, 0f);

            for (int x = -1; x <= 1; x += 2)
            {
                for (int z = -1; z <= 1; z += 2)
                {
                    Box(t, "Pied", new Vector3(x * 0.2f, 0.22f, z * 0.19f), new Vector3(0.04f, 0.44f, 0.04f), night.Plastic, false);
                }
            }

            chair.transform.localPosition += new Vector3(0f, 0.24f, 0f);
        }

        /// <summary>Une haie mal taillée le long d'une clôture : des blocs de hauteurs inégales.</summary>
        private static void Hedge(Transform parent, Palette palette, Vector3 from, Vector3 to)
        {
            GameObject hedge = EditorBuildUtility.CreateEmpty("Haie", parent, Vector3.zero);
            Vector3 delta = to - from;
            float length = delta.magnitude;
            int blocks = Mathf.Max(1, Mathf.RoundToInt(length / 1.6f));
            System.Random random = new System.Random(77);

            for (int i = 0; i < blocks; i++)
            {
                Vector3 position = from + delta * ((i + 0.5f) / blocks);
                float height = 1.2f + (float)random.NextDouble() * 0.7f;
                float width = 0.8f + (float)random.NextDouble() * 0.35f;

                Box(hedge.transform, "Bloc", position + new Vector3(0f, height * 0.5f, 0f),
                    new Vector3(width, height, length / blocks + 0.35f), palette.Hedge, i % 3 == 0)
                    .transform.localRotation = Quaternion.Euler(0f, (float)random.NextDouble() * 8f - 4f, 0f);
            }
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
