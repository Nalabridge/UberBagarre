using UberBagarre.View;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La rue de nuit devant la boîte : le décor du premier combat décrit dans le dossier.
    ///
    /// Trois principes gouvernent cette construction, et ils comptent plus que la liste
    /// des objets posés :
    ///
    /// 1. LA LUMIÈRE EST LE DÉCOR. De nuit, on ne voit pas des murs, on voit ce qui éclaire
    ///    les murs. Chaque enseigne porte donc une VRAIE lampe en plus de son matériau
    ///    émissif : sans elle, le néon brille mais n'éclaire rien, et la scène se lit
    ///    comme des autocollants lumineux collés sur du carton.
    ///
    /// 2. LE SOL RELIE TOUT. Le bitume mouillé reflète les enseignes, les phares et les
    ///    combattants. C'est le seul élément qui fasse descendre la couleur des néons
    ///    jusqu'aux pieds des personnages, donc le seul qui unifie l'image.
    ///
    /// 3. TROIS PLANS DE PROFONDEUR. Le trottoir et la chaussée (où l'on se bat), les
    ///    façades d'en face (à quinze mètres), et une silhouette de ville au loin. Sans le
    ///    troisième, le ciel touche les toits et la rue devient une boîte.
    ///
    /// Le repère : la rue court le long de X, la boîte est du côté des Z positifs, le
    /// combat a lieu autour de l'origine, au milieu de la chaussée.
    /// </summary>
    public static class NightStreetBuilder
    {
        // ------------------------------------------------------------------ dimensions

        /// <summary>Demi-longueur de la rue, le long de X.</summary>
        public const float StreetHalfLength = 46f;

        /// <summary>Bord de chaussée côté façades d'en face.</summary>
        public const float RoadNear = -11f;

        /// <summary>Bord de chaussée côté boîte de nuit.</summary>
        public const float RoadFar = 7f;

        public const float SidewalkDepth = 7f;
        public const float SidewalkHeight = 0.16f;

        /// <summary>Dimensions du volume de chaussee. Le tiling des textures s'y cale.</summary>
        public const float GroundLength = StreetHalfLength * 2f + 8f;

        public const float GroundDepth = 72f;

        /// <summary>Plan de la façade de la boîte.</summary>
        public const float ClubFront = RoadFar + SidewalkDepth;

        /// <summary>Plan des façades d'en face.</summary>
        public const float OppositeFront = RoadNear - SidewalkDepth;

        public const string ClubName = "LE VERTIGO";

        public class Result
        {
            /// <summary>Le sol mouillé : c'est lui qui porte le reflet planaire.</summary>
            public Renderer Ground;

            /// <summary>Racine de tout l'éclairage artificiel, pour le cycle jour / nuit.</summary>
            public Transform CityRoot;

            public Transform Root;
        }

        // ------------------------------------------------------------------ construction

        /// <summary>
        /// Construit la rue avec sa propre palette.
        ///
        /// Les dimensions passees a la fabrique sont celles du SOL reellement construit, pas
        /// celles de la rue : le tiling des textures en depend directement, et un sol de
        /// 100 x 72 texture d'apres une rue de 92 x 32 etire son grain d'un facteur deux dans
        /// un sens. Ca ne produit aucune erreur, juste un bitume qui ne ressemble a rien.
        /// </summary>
        public static Result Build()
        {
            return Build(NightMaterialFactory.Create(GroundLength, GroundDepth));
        }

        /// <summary>
        /// Construit la rue avec une palette FOURNIE.
        ///
        /// Le prologue a deux lieux — la maison et la rue — et ils partagent le metal, le bois,
        /// le verre, la rouille et la voiture. Deux palettes independantes creeraient deux jeux
        /// de materiaux pour les memes matieres : regler la rouille en corrigerait alors la
        /// moitie, et l'autre moitie resterait comme avant sans qu'on comprenne pourquoi.
        /// </summary>
        public static Result Build(NightMaterialFactory.Palette palette)
        {
            NightMeshFactory.EnsureLibrary();

            GameObject root = new GameObject("=== Rue de nuit ===");
            GameObject city = EditorBuildUtility.CreateEmpty("Lampadaires", root.transform, Vector3.zero);

            Result result = new Result();
            result.Root = root.transform;

            // La racine ENTIERE sert de racine d'eclairage, et pas un noeud dedie : le cycle
            // jour / nuit doit eteindre toutes les sources artificielles, or elles vivent la
            // ou elles ont un sens (l'enseigne sur la facade, le phare sur la voiture).
            // Les regrouper ailleurs rendrait la hierarchie illisible pour gagner un champ.
            result.CityRoot = root.transform;
            result.Ground = BuildGround(root.transform, palette);

            BuildRoadMarkings(root.transform, palette);
            BuildSidewalks(root.transform, palette);
            BuildClub(root.transform, palette);
            BuildOppositeBlock(root.transform, palette);
            BuildCrossStreets(root.transform, palette);
            BuildSkyline(root.transform, palette);
            BuildStreetLamps(city.transform, palette);
            BuildVehicles(root.transform, palette);
            BuildProps(root.transform, palette);
            BuildDrizzle(root.transform);

            return result;
        }

        // ------------------------------------------------------------------ sol

        /// <summary>
        /// La chaussée. Un seul volume, parfaitement plat et à y = 0 : le reflet planaire
        /// est exact pour UN plan, et découper le sol en morceaux à des hauteurs différentes
        /// ferait apparaître des ruptures dans le reflet.
        /// </summary>
        private static Renderer BuildGround(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject ground = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Chaussee", parent,
                new Vector3(0f, -0.25f, -2f), new Vector3(GroundLength, 0.5f, GroundDepth),
                palette.WetAsphalt, true);

            PlanarReflection reflection = ground.AddComponent<PlanarReflection>();
            SerializedWiring.SetBool(reflection, "_usePlaneHeightOverride", true);
            SerializedWiring.SetFloat(reflection, "_planeHeightOverride", 0f);
            SerializedWiring.SetInt(reflection, "_downsample", 2);
            SerializedWiring.SetFloat(reflection, "_clipPlaneOffset", 0.02f);
            SerializedWiring.Verify(reflection, "_planeHeightOverride");

            return ground.GetComponent<Renderer>();
        }

        /// <summary>
        /// Marquage au sol. Il ne sert pas qu'à décorer : une ligne discontinue régulière
        /// est la meilleure règle graduée qui soit pendant un combat — on lit une distance
        /// et une vitesse d'un coup d'œil, ce qu'un bitume uniforme ne permet pas.
        /// </summary>
        private static void BuildRoadMarkings(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Marquage", parent, Vector3.zero);

            float middle = (RoadNear + RoadFar) * 0.5f;

            const float dashLength = 2.2f;
            const float gap = 2.6f;
            float step = dashLength + gap;
            int count = Mathf.CeilToInt(StreetHalfLength * 2f / step);

            for (int i = 0; i <= count; i++)
            {
                float x = -StreetHalfLength + i * step;
                if (x > StreetHalfLength) break;

                Slab(root.transform, "Pointille", new Vector3(x, 0.006f, middle),
                    new Vector3(dashLength, 0.012f, 0.16f), palette.RoadPaint);
            }

            // Lignes de rive continues le long des deux trottoirs.
            Slab(root.transform, "Rive", new Vector3(0f, 0.006f, RoadFar - 0.55f),
                new Vector3(StreetHalfLength * 2f, 0.012f, 0.13f), palette.RoadPaint);

            Slab(root.transform, "Rive", new Vector3(0f, 0.006f, RoadNear + 0.55f),
                new Vector3(StreetHalfLength * 2f, 0.012f, 0.13f), palette.RoadPaint);

            // Un passage piéton devant la boîte : des bandes claires très réfléchissantes,
            // qui attrapent la couleur des néons et cassent le noir de la chaussée.
            for (int i = 0; i < 7; i++)
            {
                float x = -18f + i * 0.95f;

                Slab(root.transform, "Passage", new Vector3(x, 0.007f, (RoadNear + RoadFar) * 0.5f),
                    new Vector3(0.48f, 0.014f, RoadFar - RoadNear - 1.4f), palette.RoadPaint);
            }
        }

        // ------------------------------------------------------------------ trottoirs

        private static void BuildSidewalks(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Trottoirs", parent, Vector3.zero);

            Sidewalk(root.transform, palette, RoadFar, ClubFront, "Trottoir boite");
            Sidewalk(root.transform, palette, OppositeFront, RoadNear, "Trottoir en face");
        }

        private static void Sidewalk(Transform parent, NightMaterialFactory.Palette palette,
            float zFrom, float zTo, string name)
        {
            float depth = zTo - zFrom;
            float center = (zFrom + zTo) * 0.5f;
            float length = StreetHalfLength * 2f + 8f;

            GameObject slab = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, name, parent,
                new Vector3(0f, SidewalkHeight * 0.5f, center),
                new Vector3(length, SidewalkHeight, depth), palette.Sidewalk, true);

            slab.isStatic = true;

            // La bordure : une bande d'une teinte différente le long de l'arête côté
            // chaussée. C'est un détail minuscule, mais c'est lui qui fait lire « trottoir »
            // plutôt que « estrade ».
            float curbZ = zFrom < 0f ? zTo - 0.14f : zFrom + 0.14f;

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Bordure", parent,
                new Vector3(0f, SidewalkHeight * 0.5f + 0.012f, curbZ),
                new Vector3(length, SidewalkHeight + 0.02f, 0.3f), palette.Curb, false);
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

        /// <summary>
        /// Ajoute une lampe. Le mode de rendu est explicite : au-delà de quelques lampes
        /// par pixel, le rendu en avant d'Unity choisit lui-même lesquelles sont calculées
        /// par pixel, et le résultat change selon l'endroit où l'on regarde. Les sources qui
        /// comptent pour le combat sont donc forcées, les décoratives sont en sommet.
        /// </summary>
        private static Light AddLight(Transform parent, string name, Vector3 localPosition, Color color,
            float intensity, float range, bool important, bool shadows)
        {
            GameObject go = EditorBuildUtility.CreateEmpty(name, parent, localPosition);

            Light light = go.AddComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.intensity = intensity;
            light.range = range;
            light.renderMode = important ? LightRenderMode.ForcePixel : LightRenderMode.ForceVertex;
            light.shadows = shadows ? LightShadows.Soft : LightShadows.None;
            light.shadowStrength = 0.75f;
            light.bounceIntensity = 0f;

            return light;
        }

        private static NeonFlicker AddFlicker(GameObject target, NeonFlicker.Pattern pattern,
            float baseIntensity, float amount, float speed, float seed)
        {
            NeonFlicker flicker = target.AddComponent<NeonFlicker>();

            SerializedWiring.SetEnum(flicker, "_pattern", (int)pattern);
            SerializedWiring.SetFloat(flicker, "_baseIntensity", baseIntensity);
            SerializedWiring.SetFloat(flicker, "_amount", amount);
            SerializedWiring.SetFloat(flicker, "_speed", speed);
            SerializedWiring.SetFloat(flicker, "_seed", seed);

            return flicker;
        }

        // ------------------------------------------------------------------ la boîte de nuit

        /// <summary>
        /// La façade de la boîte. C'est l'objet le plus important de la scène : elle est
        /// dans le dos de l'adversaire pendant tout le combat, donc à l'écran en permanence.
        ///
        /// Elle est construite en couches, du fond vers l'avant : mur, socle, renfoncement
        /// d'entrée, marquise, puis enseignes. Cet ordre n'est pas cosmétique — c'est ce qui
        /// crée des DÉCROCHEMENTS, donc des ombres portées les unes sur les autres. Une
        /// façade plate, même couverte de néons, reste un panneau.
        /// </summary>
        private static void BuildClub(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Boite de nuit", parent, new Vector3(0f, 0f, ClubFront));
            Transform t = root.transform;

            // --- volume principal
            Box(t, "Batiment", new Vector3(0f, 6f, 9f), new Vector3(34f, 12f, 18f), palette.DarkBrick, true);

            Box(t, "Corniche", new Vector3(0f, 12.2f, -0.3f), new Vector3(35f, 0.55f, 1.6f), palette.DarkConcrete, false);
            Box(t, "Socle", new Vector3(0f, 0.65f, -0.16f), new Vector3(34f, 1.3f, 0.4f), palette.DarkMetal, false);

            // --- entrée en renfoncement
            Box(t, "Encadrement", new Vector3(0f, 2.35f, 0.18f), new Vector3(7.4f, 4.7f, 0.5f), palette.DarkMetal, false);
            Box(t, "Sas", new Vector3(0f, 2.2f, 0.9f), new Vector3(6.4f, 4.2f, 1.2f), palette.DarkConcrete, false);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(t, "Porte", new Vector3(side * 0.95f, 1.75f, 0.06f),
                    new Vector3(1.75f, 3.5f, 0.12f), palette.Glass, false);

                Cylinder(t, "Poignee", new Vector3(side * 0.28f, 1.75f, -0.06f),
                    new Vector3(0.035f, 0.55f, 0.035f), palette.Chrome, false);
            }

            // Lueur qui s'échappe du sas : la boîte doit sembler pleine. Une pulsation lente
            // suffit à suggérer la musique, sans qu'aucun son ne soit joué.
            GameObject inside = EditorBuildUtility.CreateEmpty("Lumiere interieure", t, new Vector3(0f, 2.2f, 1.2f));
            AddLight(inside.transform, "Lampe", Vector3.zero, new Color(1f, 0.22f, 0.62f), 3.4f, 11f, true, false);
            AddFlicker(inside, NeonFlicker.Pattern.Pulsation, 1f, 0.55f, 2.2f, 3f);

            // --- marquise
            Box(t, "Marquise", new Vector3(0f, 4.95f, -1.75f), new Vector3(13.5f, 0.45f, 3.6f), palette.DarkMetal, false);
            Box(t, "Rebord", new Vector3(0f, 4.68f, -3.5f), new Vector3(13.9f, 0.22f, 0.3f), palette.Chrome, false);

            GameObject marqueeLights = EditorBuildUtility.CreateEmpty("Eclairage marquise", t,
                new Vector3(0f, 4.68f, -1.8f));

            for (int i = -3; i <= 3; i++)
            {
                Cylinder(marqueeLights.transform, "Spot", new Vector3(i * 1.8f, 0f, 0f),
                    new Vector3(0.3f, 0.02f, 0.3f), palette.NeonWarm, false);
            }

            AddLight(marqueeLights.transform, "Lampe gauche", new Vector3(-3.4f, -0.4f, 0f),
                new Color(1f, 0.82f, 0.55f), 2.6f, 9f, true, true);
            AddLight(marqueeLights.transform, "Lampe droite", new Vector3(3.4f, -0.4f, 0f),
                new Color(1f, 0.82f, 0.55f), 2.6f, 9f, true, true);

            AddFlicker(marqueeLights, NeonFlicker.Pattern.Calme, 4.5f, 0.06f, 0.8f, 17f);

            BuildClubSigns(t, palette);
            BuildClubForecourt(t, palette);
        }

        private static void BuildClubSigns(Transform club, NightMaterialFactory.Palette palette)
        {
            // --- enseigne principale
            GameObject sign = EditorBuildUtility.CreateEmpty("Enseigne", club, new Vector3(0f, 7.5f, -0.55f));

            float width = NeonTextBuilder.Build(sign.transform, ClubName, 1.55f, 0.13f, palette.NeonMagenta);

            // Le caisson derrière les lettres : sans lui, les tubes flottent devant la
            // brique et l'enseigne perd son épaisseur. Il sert aussi de fond sombre, ce qui
            // augmente énormément le contraste perçu du néon.
            Box(sign.transform, "Caisson", new Vector3(0f, 0.75f, 0.35f),
                new Vector3(width + 1.4f, 2.5f, 0.35f), palette.DarkMetal, false);

            AddLight(sign.transform, "Halo gauche", new Vector3(-width * 0.3f, 0.8f, -0.6f),
                new Color(1f, 0.2f, 0.62f), 3.2f, 14f, true, false);
            AddLight(sign.transform, "Halo droit", new Vector3(width * 0.3f, 0.8f, -0.6f),
                new Color(1f, 0.2f, 0.62f), 3.2f, 14f, true, false);

            // Nappe additive DEVANT l'enseigne. Le bloom fait deborder la lumiere dans
            // l'image, mais il ne met rien dans l'AIR : sans ce volume, l'enseigne brille
            // sans que la brume autour d'elle en garde la couleur.
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Nappe", sign.transform,
                new Vector3(0f, 0.8f, -0.45f), new Vector3(width * 0.92f, 2.7f, 1.3f),
                palette.GlowMagenta, false);

            AddFlicker(sign, NeonFlicker.Pattern.Calme, 7.5f, 0.07f, 1.1f, 23f);

            // --- sous-titre
            GameObject subtitle = EditorBuildUtility.CreateEmpty("Sous-titre", club, new Vector3(0f, 6.35f, -0.5f));
            NeonTextBuilder.Build(subtitle.transform, "DISCOTHEQUE", 0.42f, 0.055f, palette.NeonCyan);

            AddLight(subtitle.transform, "Halo", new Vector3(0f, 0.2f, -0.5f),
                new Color(0.25f, 0.9f, 1f), 1.5f, 9f, false, false);

            AddFlicker(subtitle, NeonFlicker.Pattern.Bourdonnement, 6.5f, 0.22f, 1f, 41f);

            // --- enseigne drapeau, perpendiculaire à la façade
            //
            // Elle est visible de loin le long de la rue, alors que l'enseigne principale
            // disparaît dès qu'on n'est plus en face. C'est ce qui donne à la rue un
            // point de fuite lumineux plutôt qu'un simple mur éclairé.
            GameObject blade = EditorBuildUtility.CreateEmpty("Enseigne drapeau", club,
                new Vector3(-13.5f, 5.2f, -1.1f));

            blade.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);

            Box(blade.transform, "Panneau", new Vector3(0f, 2.6f, 0.22f),
                new Vector3(1.9f, 6.4f, 0.3f), palette.DarkMetal, false);

            // L'enseigne drapeau est tournee de 90 degres : son axe X local pointe donc le
            // long de la rue, et c'est le long de CET axe que la potence doit rejoindre le
            // mur. Se tromper d'axe ici donne un bras qui part dans le vide, parallele a la
            // facade, sans que rien ne le signale.
            Box(blade.transform, "Potence", new Vector3(-0.6f, 5.4f, 0f),
                new Vector3(1.6f, 0.12f, 0.12f), palette.Metal, false);

            string vertical = "VERTIGO";
            for (int i = 0; i < vertical.Length; i++)
            {
                GameObject line = EditorBuildUtility.CreateEmpty("Ligne", blade.transform,
                    new Vector3(0f, 5.05f - i * 0.78f, 0f));

                NeonTextBuilder.Build(line.transform, vertical.Substring(i, 1), 0.58f, 0.075f, palette.NeonCyan);
            }

            AddLight(blade.transform, "Halo", new Vector3(0f, 2.8f, -0.5f),
                new Color(0.25f, 0.88f, 1f), 3f, 13f, true, false);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Nappe", blade.transform,
                new Vector3(0f, 2.6f, 0f), new Vector3(2.6f, 7.4f, 1.6f), palette.GlowCyan, false);

            AddFlicker(blade, NeonFlicker.Pattern.Fatigue, 6.5f, 0.15f, 0.9f, 59f);

            // --- bandeaux lumineux le long de la façade
            GameObject strips = EditorBuildUtility.CreateEmpty("Bandeaux", club, Vector3.zero);

            Slab(strips.transform, "Bandeau bas", new Vector3(0f, 1.42f, -0.24f),
                new Vector3(33f, 0.09f, 0.09f), palette.NeonBlue);

            Slab(strips.transform, "Bandeau haut", new Vector3(0f, 11.65f, -0.24f),
                new Vector3(33f, 0.09f, 0.09f), palette.NeonBlue);

            for (int i = -2; i <= 2; i++)
            {
                AddLight(strips.transform, "Lueur", new Vector3(i * 7f, 1.6f, -0.9f),
                    new Color(0.3f, 0.45f, 1f), 1.1f, 7f, false, false);
            }

            AddFlicker(strips, NeonFlicker.Pattern.Calme, 4.2f, 0.05f, 0.7f, 73f);

            // --- hublots
            GameObject portholes = EditorBuildUtility.CreateEmpty("Hublots", club, Vector3.zero);

            for (int side = -1; side <= 1; side += 2)
            {
                GameObject porthole = Cylinder(portholes.transform, "Hublot",
                    new Vector3(side * 10.5f, 7.4f, -0.12f), new Vector3(1.7f, 0.08f, 1.7f),
                    palette.NeonWhite, false);

                porthole.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                Cylinder(portholes.transform, "Cerclage", new Vector3(side * 10.5f, 7.4f, -0.05f),
                    new Vector3(2f, 0.06f, 2f), palette.Chrome, false)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                AddLight(portholes.transform, "Lueur", new Vector3(side * 10.5f, 7.4f, -1f),
                    new Color(1f, 0.93f, 0.85f), 1.6f, 8f, false, false);
            }

            AddFlicker(portholes, NeonFlicker.Pattern.Pulsation, 5f, 0.35f, 1.6f, 89f);
        }

        /// <summary>
        /// Le parvis : tapis, cordon de velours, barrières, videur. C'est ce qui raconte
        /// qu'il y a une file d'attente, donc du monde, donc une raison d'être là.
        /// </summary>
        private static void BuildClubForecourt(Transform club, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Parvis", club, Vector3.zero);
            Transform t = root.transform;

            float top = SidewalkHeight;

            Slab(t, "Tapis", new Vector3(0f, top + 0.012f, -3.6f), new Vector3(5.2f, 0.024f, 7f), palette.Carpet);

            // Cordon de velours des deux côtés, avec des poteaux régulièrement espacés.
            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * 2.9f;
                float[] posts = { -1.3f, -3.3f, -5.3f, -7.0f };

                for (int i = 0; i < posts.Length; i++)
                {
                    GameObject post = EditorBuildUtility.CreateEmpty("Poteau", t,
                        new Vector3(x, top, posts[i]));

                    Cylinder(post.transform, "Socle", new Vector3(0f, 0.035f, 0f),
                        new Vector3(0.32f, 0.035f, 0.32f), palette.DarkMetal, false);
                    Cylinder(post.transform, "Fut", new Vector3(0f, 0.48f, 0f),
                        new Vector3(0.07f, 0.48f, 0.07f), palette.Chrome, true);
                    EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Boule", post.transform,
                        new Vector3(0f, 1.0f, 0f), Vector3.one * 0.12f, palette.Chrome, false);

                    if (i == 0) continue;

                    Rope(t, palette, new Vector3(x, top + 0.82f, posts[i - 1]), new Vector3(x, top + 0.82f, posts[i]));
                }
            }

            // Barrières de file, en retrait : elles ferment la perspective sans gêner le combat.
            for (int i = 0; i < 3; i++)
            {
                Barrier(t, palette, new Vector3(-5.6f - i * 0.1f, top, -2.4f - i * 2.3f), 4f + i * 3f);
            }

            // Le pupitre du videur, avec sa petite lampe : un point chaud isolé au sol, qui
            // rompt la symétrie de l'entrée.
            GameObject podium = EditorBuildUtility.CreateEmpty("Pupitre", t, new Vector3(4.1f, top, -2.1f));
            podium.transform.localRotation = Quaternion.Euler(0f, -24f, 0f);

            Box(podium.transform, "Corps", new Vector3(0f, 0.5f, 0f), new Vector3(0.7f, 1f, 0.45f), palette.Wood, true);
            Box(podium.transform, "Plateau", new Vector3(0f, 1.03f, -0.02f), new Vector3(0.8f, 0.06f, 0.55f),
                palette.DarkMetal, false);

            GameObject lamp = EditorBuildUtility.CreateEmpty("Lampe de pupitre", podium.transform,
                new Vector3(0.22f, 1.35f, 0f));

            Cylinder(lamp.transform, "Bras", new Vector3(0f, -0.15f, 0f), new Vector3(0.025f, 0.16f, 0.025f),
                palette.DarkMetal, false);
            Cylinder(lamp.transform, "Abat-jour", Vector3.zero, new Vector3(0.14f, 0.06f, 0.14f),
                palette.NeonWarm, false);

            AddLight(lamp.transform, "Lampe", new Vector3(0f, -0.1f, 0f),
                new Color(1f, 0.78f, 0.48f), 1.4f, 4.5f, false, false);

            AddFlicker(lamp, NeonFlicker.Pattern.Calme, 4f, 0.09f, 1.3f, 101f);
        }

        /// <summary>
        /// Un cordon entre deux poteaux. Il pend : trois segments suffisent à le suggérer,
        /// et un cordon parfaitement droit est le genre de détail qui fait « objet posé ».
        /// </summary>
        private static void Rope(Transform parent, NightMaterialFactory.Palette palette, Vector3 from, Vector3 to)
        {
            const int segments = 4;
            const float sag = 0.22f;

            Vector3 previous = from;

            for (int i = 1; i <= segments; i++)
            {
                float u = (float)i / segments;
                Vector3 point = Vector3.Lerp(from, to, u);
                point.y -= Mathf.Sin(u * Mathf.PI) * sag;

                Vector3 middle = (previous + point) * 0.5f;
                Vector3 delta = point - previous;

                GameObject piece = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Cordon", parent,
                    middle, new Vector3(0.055f, 0.055f, delta.magnitude), palette.Velvet, false);

                piece.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

                previous = point;
            }
        }

        private static void Barrier(Transform parent, NightMaterialFactory.Palette palette, Vector3 position, float yaw)
        {
            GameObject barrier = EditorBuildUtility.CreateEmpty("Barriere", parent, position);
            barrier.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(barrier.transform, "Cadre haut", new Vector3(0f, 1.05f, 0f), new Vector3(2.2f, 0.06f, 0.06f),
                palette.Metal, false);
            Box(barrier.transform, "Cadre bas", new Vector3(0f, 0.12f, 0f), new Vector3(2.2f, 0.06f, 0.06f),
                palette.Metal, false);

            for (int i = -1; i <= 1; i++)
            {
                Box(barrier.transform, "Montant", new Vector3(i * 1.05f, 0.58f, 0f),
                    new Vector3(0.06f, 1.1f, 0.06f), palette.Metal, i == 0);
            }

            for (int i = 0; i < 6; i++)
            {
                Box(barrier.transform, "Barreau", new Vector3(-0.9f + i * 0.36f, 0.58f, 0f),
                    new Vector3(0.035f, 0.95f, 0.035f), palette.Metal, false);
            }
        }

        // ------------------------------------------------------------------ îlot d'en face

        /// <summary>
        /// Les façades d'en face. Elles sont à quinze mètres : assez loin pour qu'on ne
        /// distingue plus les détails, assez près pour que leurs fenêtres éclairent la rue.
        ///
        /// Leurs hauteurs sont volontairement inégales et non périodiques. Une rangée
        /// d'immeubles identiques est ce qui trahit le plus vite une ville générée : l'œil
        /// détecte la période bien avant de reconnaître le bâtiment.
        /// </summary>
        private static void BuildOppositeBlock(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Ilot d'en face", parent, Vector3.zero);

            float[] centers = { -38f, -24f, -10f, 4f, 18f, 33f };
            float[] widths = { 14f, 14f, 14f, 14f, 15f, 14f };
            float[] heights = { 16f, 21.5f, 13f, 18.5f, 24f, 15f };

            for (int i = 0; i < centers.Length; i++)
            {
                Material facade = i % 3 == 0 ? palette.FacadeLitCool
                    : i % 3 == 1 ? palette.FacadeLit
                    : palette.Brick;

                Building(root.transform, palette, facade, centers[i], widths[i], heights[i], i);
            }

            Storefront(root.transform, palette, -24f, "NUIT 24H", palette.NeonGreen, new Color(0.35f, 1f, 0.45f));
            Storefront(root.transform, palette, 4f, "SNACK", palette.NeonRed, new Color(1f, 0.2f, 0.18f));
            Storefront(root.transform, palette, 18f, "TABAC", palette.NeonWarm, new Color(1f, 0.6f, 0.2f));
            Storefront(root.transform, palette, -10f, "PRESSING", palette.NeonBlue, new Color(0.35f, 0.5f, 1f));

            FireEscape(root.transform, palette, -38f, 16f);
        }

        private static void Building(Transform parent, NightMaterialFactory.Palette palette, Material facade,
            float x, float width, float height, int index)
        {
            GameObject building = EditorBuildUtility.CreateEmpty("Immeuble", parent,
                new Vector3(x, 0f, OppositeFront));

            float depth = 16f;

            Box(building.transform, "Corps", new Vector3(0f, height * 0.5f, -depth * 0.5f),
                new Vector3(width, height, depth), facade, true);

            Box(building.transform, "Corniche", new Vector3(0f, height + 0.25f, 0.2f),
                new Vector3(width + 0.7f, 0.5f, 1.2f), palette.DarkConcrete, false);

            // Rez-de-chaussée : un bandeau sombre qui sépare la boutique des étages. Sans
            // lui, la grille de fenêtres descend jusqu'au trottoir et l'immeuble n'a pas de sol.
            Box(building.transform, "Socle", new Vector3(0f, 1.6f, 0.12f),
                new Vector3(width + 0.1f, 3.2f, 0.36f), palette.DarkConcrete, false);

            Box(building.transform, "Bandeau", new Vector3(0f, 3.35f, 0.18f),
                new Vector3(width + 0.3f, 0.32f, 0.5f), palette.Concrete, false);

            // Climatiseurs et paraboles : les objets qui disent « des gens vivent ici ».
            if (index % 2 == 0)
            {
                Box(building.transform, "Climatiseur", new Vector3(width * 0.28f, 5.4f, 0.32f),
                    new Vector3(0.75f, 0.55f, 0.45f), palette.Metal, false);

                Box(building.transform, "Climatiseur", new Vector3(-width * 0.3f, 8.9f, 0.32f),
                    new Vector3(0.75f, 0.55f, 0.45f), palette.Metal, false);
            }

            // Réservoir sur le toit, en silhouette contre le ciel : c'est ce qui casse la
            // ligne droite des toits et rend le haut de l'image lisible.
            if (index % 3 == 1)
            {
                Cylinder(building.transform, "Reservoir", new Vector3(width * 0.2f, height + 1.4f, -2.5f),
                    new Vector3(1.6f, 1.1f, 1.6f), palette.Rust, false);

                for (int i = -1; i <= 1; i += 2)
                {
                    Box(building.transform, "Pied", new Vector3(width * 0.2f + i * 0.55f, height + 0.4f, -2.5f),
                        new Vector3(0.09f, 0.8f, 0.09f), palette.DarkMetal, false);
                }
            }
        }

        /// <summary>
        /// Une devanture avec son enseigne. Chaque commerce a sa couleur : c'est ce qui
        /// donne à la rue une PALETTE, et la palette est ce qui fait qu'on la reconnaît.
        /// </summary>
        private static void Storefront(Transform parent, NightMaterialFactory.Palette palette, float x,
            string label, Material neon, Color lightColor)
        {
            GameObject shop = EditorBuildUtility.CreateEmpty("Commerce " + label, parent,
                new Vector3(x, 0f, OppositeFront));

            // La vitrine est plus lumineuse que tout ce qui l'entoure au niveau du sol :
            // elle pose une deuxième source au ras de la rue, en face de la boîte.
            Box(shop.transform, "Vitrine", new Vector3(0f, 1.75f, -0.06f),
                new Vector3(5.6f, 2.5f, 0.12f), palette.Glass, false);

            Box(shop.transform, "Encadrement", new Vector3(0f, 1.75f, 0.02f),
                new Vector3(6f, 2.9f, 0.16f), palette.DarkMetal, false);

            Box(shop.transform, "Store", new Vector3(0f, 3.25f, -0.55f),
                new Vector3(6.2f, 0.16f, 1.2f), palette.Plastic, false);

            GameObject sign = EditorBuildUtility.CreateEmpty("Enseigne", shop.transform,
                new Vector3(0f, 3.7f, -0.35f));

            NeonTextBuilder.Build(sign.transform, label, 0.5f, 0.065f, neon);

            AddLight(sign.transform, "Halo", new Vector3(0f, 0.25f, -0.7f), lightColor, 2.2f, 10f, false, false);

            AddFlicker(sign, NeonFlicker.Pattern.Bourdonnement, 6f, 0.18f, 1f, Mathf.Abs(x) * 3.7f + 13f);

            // Lumière de l'intérieur de la boutique, blanche et froide : elle découpe la
            // silhouette de tout ce qui passe devant la vitrine.
            GameObject interior = EditorBuildUtility.CreateEmpty("Interieur", shop.transform,
                new Vector3(0f, 2f, 0.9f));

            AddLight(interior.transform, "Neon", Vector3.zero, new Color(0.88f, 0.95f, 1f), 2.4f, 12f, true, false);
            AddFlicker(interior, NeonFlicker.Pattern.Bourdonnement, 3f, 0.14f, 1.3f, Mathf.Abs(x) * 1.9f + 7f);
        }

        private static void FireEscape(Transform parent, NightMaterialFactory.Palette palette, float x, float height)
        {
            GameObject escape = EditorBuildUtility.CreateEmpty("Escalier de secours", parent,
                new Vector3(x + 4.5f, 0f, OppositeFront - 0.1f));

            int levels = Mathf.FloorToInt((height - 4f) / 3.2f);

            for (int i = 0; i < levels; i++)
            {
                float y = 4f + i * 3.2f;

                Box(escape.transform, "Palier", new Vector3(0f, y, -0.75f),
                    new Vector3(3.2f, 0.08f, 1.5f), palette.DarkMetal, false);

                Box(escape.transform, "Garde-corps", new Vector3(0f, y + 0.5f, -1.45f),
                    new Vector3(3.2f, 0.05f, 0.05f), palette.DarkMetal, false);

                for (int r = 0; r < 7; r++)
                {
                    Box(escape.transform, "Barreau", new Vector3(-1.4f + r * 0.47f, y + 0.26f, -1.45f),
                        new Vector3(0.035f, 0.55f, 0.035f), palette.DarkMetal, false);
                }

                // L'échelle entre deux paliers, inclinée : c'est la diagonale qui fait lire
                // un escalier de secours plutôt qu'une série de balcons.
                if (i >= levels - 1) continue;

                GameObject ladder = EditorBuildUtility.CreateEmpty("Echelle", escape.transform,
                    new Vector3(1.1f, y + 1.6f, -1f));

                ladder.transform.localRotation = Quaternion.Euler(-52f, 0f, 0f);

                Box(ladder.transform, "Limon", new Vector3(-0.35f, 0f, 0f), new Vector3(0.06f, 0.06f, 3.6f),
                    palette.DarkMetal, false);
                Box(ladder.transform, "Limon", new Vector3(0.35f, 0f, 0f), new Vector3(0.06f, 0.06f, 3.6f),
                    palette.DarkMetal, false);

                for (int s = 0; s < 8; s++)
                {
                    Box(ladder.transform, "Marche", new Vector3(0f, 0f, -1.6f + s * 0.45f),
                        new Vector3(0.7f, 0.04f, 0.06f), palette.DarkMetal, false);
                }
            }
        }

        // ------------------------------------------------------------------ fermetures et lointain

        /// <summary>
        /// Les deux extrémités de la rue. Sans elles, on voit le ciel au ras du sol de
        /// chaque côté et la rue devient un couloir ouvert sur le vide.
        /// </summary>
        private static void BuildCrossStreets(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Rues transversales", parent, Vector3.zero);

            for (int side = -1; side <= 1; side += 2)
            {
                float x = side * (StreetHalfLength + 4f);

                Box(root.transform, "Pate de maisons", new Vector3(x, 11f, -6f),
                    new Vector3(11f, 22f, 60f), palette.Brick, true);

                Box(root.transform, "Corniche", new Vector3(x, 22.3f, -6f),
                    new Vector3(11.8f, 0.6f, 61f), palette.DarkConcrete, false);
            }
        }

        /// <summary>
        /// La ville au loin : de simples volumes sombres, criblés de fenêtres froides.
        ///
        /// Ils ne sont jamais approchés, donc jamais lus en détail — leur seul rôle est de
        /// remplir le ciel derrière les toits. C'est le plan qui manque presque toujours, et
        /// son absence donne cette impression de décor de studio : au-dessus des immeubles
        /// proches, il n'y a rien.
        /// </summary>
        private static void BuildSkyline(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Ville au loin", parent, Vector3.zero);

            float[] x = { -72f, -44f, -18f, 12f, 40f, 68f, -58f, 26f };
            float[] z = { 62f, 78f, 58f, 70f, 60f, 82f, -62f, -70f };
            float[] h = { 34f, 52f, 28f, 44f, 38f, 60f, 30f, 46f };
            float[] w = { 18f, 22f, 15f, 20f, 17f, 24f, 16f, 21f };

            for (int i = 0; i < x.Length; i++)
            {
                Material material = i % 2 == 0 ? palette.FacadeLitCool : palette.FacadeLit;

                Box(root.transform, "Tour", new Vector3(x[i], h[i] * 0.5f, z[i]),
                    new Vector3(w[i], h[i], w[i]), material, false);

                // Feu de balisage sur les plus hautes : un point rouge qui clignote très
                // lentement, tout en haut de l'image. C'est minuscule, et ça suffit à dire
                // « ville ».
                if (h[i] < 40f) continue;

                GameObject beacon = Cylinder(root.transform, "Balise",
                    new Vector3(x[i], h[i] + 0.3f, z[i]), new Vector3(0.6f, 0.3f, 0.6f), palette.NeonRed, false);

                AddFlicker(beacon, NeonFlicker.Pattern.Pulsation, 8f, 0.95f, 1.1f, i * 13f + 3f);
            }
        }

        // ------------------------------------------------------------------ lampadaires

        private static void BuildStreetLamps(Transform parent, NightMaterialFactory.Palette palette)
        {
            float[] clubSide = { -36f, -22f, 22f, 36f };
            float[] otherSide = { -29f, -15f, 15f, 29f };

            for (int i = 0; i < clubSide.Length; i++)
            {
                StreetLamp(parent, palette, new Vector3(clubSide[i], SidewalkHeight, RoadFar + 0.9f), 180f, i);
            }

            for (int i = 0; i < otherSide.Length; i++)
            {
                StreetLamp(parent, palette, new Vector3(otherSide[i], SidewalkHeight, RoadNear - 0.9f), 0f, i + 4);
            }
        }

        /// <summary>
        /// Un lampadaire au sodium, avec son cône de lumière.
        ///
        /// Le cône est ce qui fait la différence entre « une lampe » et « une nuit humide ».
        /// Une lampe ponctuelle Unity éclaire les surfaces mais laisse l'air parfaitement
        /// transparent : on voit un disque clair au sol sans comprendre d'où il vient. Le
        /// volume additif rend visible le trajet de la lumière, et c'est lui que l'œil lit
        /// comme de la brume.
        /// </summary>
        private static void StreetLamp(Transform parent, NightMaterialFactory.Palette palette,
            Vector3 position, float yaw, int index)
        {
            GameObject lamp = EditorBuildUtility.CreateEmpty("Lampadaire", parent, position);
            lamp.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Cylinder(lamp.transform, "Socle", new Vector3(0f, 0.16f, 0f), new Vector3(0.42f, 0.16f, 0.42f),
                palette.DarkConcrete, true);

            Cylinder(lamp.transform, "Mat", new Vector3(0f, 3.1f, 0f), new Vector3(0.13f, 3.1f, 0.13f),
                palette.DarkMetal, true);

            Box(lamp.transform, "Potence", new Vector3(0f, 6.1f, 0.9f), new Vector3(0.11f, 0.11f, 2f),
                palette.DarkMetal, false);

            Box(lamp.transform, "Lanterne", new Vector3(0f, 5.92f, 1.75f), new Vector3(0.44f, 0.2f, 0.85f),
                palette.DarkMetal, false);

            Box(lamp.transform, "Ampoule", new Vector3(0f, 5.78f, 1.75f),
                new Vector3(0.36f, 0.07f, 0.72f), palette.NeonWarm, false);

            AddLight(lamp.transform, "Lumiere", new Vector3(0f, 5.7f, 1.75f),
                new Color(1f, 0.72f, 0.42f), 3.6f, 17f, index < 4, index < 2);

            // Le cône descend de la lanterne jusqu'au sol, et s'élargit. Il ne touche pas
            // tout à fait la chaussée : un volume additif qui s'arrête net dans le sol y
            // dessine une ellipse claire au bord dur, très visible.
            NightMeshFactory.CreateVisual(NightMeshFactory.LightCone, "Cone de lumiere",
                lamp.transform, new Vector3(0f, 3.05f, 1.75f), Quaternion.identity,
                new Vector3(7.4f, 5.4f, 7.4f), palette.GlowWarm);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Halo", lamp.transform,
                new Vector3(0f, 5.78f, 1.75f), Vector3.one * 1.5f, palette.GlowWarm, false);

            // Un lampadaire sur quatre est fatigué. Pas plus : au-delà, le clignotement
            // devient le sujet de la scène.
            NeonFlicker.Pattern pattern = index % 4 == 3 ? NeonFlicker.Pattern.Fatigue : NeonFlicker.Pattern.Calme;
            NeonFlicker flicker = AddFlicker(lamp, pattern, 5.5f, 0.08f, 1f, index * 7.3f + 1f);
            SerializedWiring.Verify(flicker, "_baseIntensity");
        }

        // ------------------------------------------------------------------ véhicules

        /// <summary>
        /// Les voitures. Le dossier en décrit une précisément : « sale, délabrée, rouillée,
        /// vieille bref nulle ». C'est celle du joueur, garée devant la boîte.
        ///
        /// Une voiture apporte trois choses qu'aucun bâtiment ne donne : une silhouette
        /// basse et horizontale au milieu de la rue, du chrome qui renvoie les néons, et
        /// des phares — c'est-à-dire une source de lumière à HAUTEUR D'HOMME, la seule qui
        /// éclaire les jambes des combattants.
        /// </summary>
        private static void BuildVehicles(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Vehicules", parent, Vector3.zero);

            Car(root.transform, palette, new Vector3(-10.5f, 0f, 4.6f), 3.5f, true, true);
            Car(root.transform, palette, new Vector3(19f, 0f, 4.4f), -2f, false, false);
            Car(root.transform, palette, new Vector3(-26f, 0f, -8.6f), 181f, false, false);
        }

        /// <summary>Partagee avec le lieu « maison » : c'est la meme voiture.</summary>
        internal static void Car(Transform parent, NightMaterialFactory.Palette palette, Vector3 position,
            float yaw, bool rusty, bool headlightsOn)
        {
            GameObject car = EditorBuildUtility.CreateEmpty(rusty ? "Voiture rouillee" : "Voiture", parent, position);
            car.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Material body = rusty ? palette.Rust : palette.CarBody;
            Material roof = palette.CarRoof;

            // --- caisse
            Box(car.transform, "Bas de caisse", new Vector3(0f, 0.46f, 0f), new Vector3(4.35f, 0.34f, 1.88f),
                palette.DarkMetal, false);

            Box(car.transform, "Caisse", new Vector3(0f, 0.78f, 0f), new Vector3(4.3f, 0.52f, 1.84f), body, true);

            Box(car.transform, "Capot", new Vector3(1.35f, 1.03f, 0f), new Vector3(1.55f, 0.12f, 1.7f), body, false);
            Box(car.transform, "Coffre", new Vector3(-1.5f, 1.03f, 0f), new Vector3(1.3f, 0.12f, 1.7f), body, false);

            // --- habitacle
            Box(car.transform, "Habitacle", new Vector3(-0.15f, 1.32f, 0f), new Vector3(2.35f, 0.62f, 1.7f), roof, false);
            Box(car.transform, "Toit", new Vector3(-0.15f, 1.64f, 0f), new Vector3(2.2f, 0.08f, 1.66f), roof, false);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(car.transform, "Vitre laterale", new Vector3(-0.15f, 1.34f, side * 0.87f),
                    new Vector3(2.1f, 0.5f, 0.04f), palette.Glass, false);
            }

            GameObject windshield = Box(car.transform, "Pare-brise", new Vector3(1.08f, 1.32f, 0f),
                new Vector3(0.06f, 0.72f, 1.62f), palette.Glass, false);
            windshield.transform.localRotation = Quaternion.Euler(0f, 0f, -28f);

            GameObject rear = Box(car.transform, "Lunette", new Vector3(-1.32f, 1.32f, 0f),
                new Vector3(0.06f, 0.66f, 1.6f), palette.Glass, false);
            rear.transform.localRotation = Quaternion.Euler(0f, 0f, 26f);

            // --- pare-chocs et chrome
            Box(car.transform, "Pare-chocs avant", new Vector3(2.2f, 0.66f, 0f), new Vector3(0.22f, 0.24f, 1.9f),
                palette.Chrome, false);
            Box(car.transform, "Pare-chocs arriere", new Vector3(-2.2f, 0.66f, 0f), new Vector3(0.22f, 0.24f, 1.9f),
                palette.Chrome, false);
            Box(car.transform, "Calandre", new Vector3(2.18f, 0.92f, 0f), new Vector3(0.1f, 0.26f, 1.3f),
                palette.DarkMetal, false);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(car.transform, "Retroviseur", new Vector3(0.95f, 1.32f, side * 1.02f),
                    new Vector3(0.22f, 0.12f, 0.1f), palette.DarkMetal, false);
            }

            Cylinder(car.transform, "Echappement", new Vector3(-2.25f, 0.42f, -0.55f),
                new Vector3(0.08f, 0.12f, 0.08f), palette.Metal, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);

            Box(car.transform, "Plaque", new Vector3(-2.3f, 0.72f, 0f), new Vector3(0.03f, 0.16f, 0.6f),
                palette.RoadPaint, false);

            // --- roues
            for (int fx = -1; fx <= 1; fx += 2)
            {
                for (int fz = -1; fz <= 1; fz += 2)
                {
                    Vector3 hub = new Vector3(fx * 1.42f, 0.34f, fz * 0.88f);

                    Cylinder(car.transform, "Pneu", hub, new Vector3(0.68f, 0.11f, 0.68f), palette.Rubber, false)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                    Cylinder(car.transform, "Jante", hub + new Vector3(0f, 0f, fz * 0.02f),
                        new Vector3(0.4f, 0.12f, 0.4f), palette.Chrome, false)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                    // Passage de roue : une arche sombre au-dessus du pneu. Sans elle, la
                    // roue semble collée sur le flanc.
                    Box(car.transform, "Passage de roue", new Vector3(fx * 1.42f, 0.86f, fz * 0.94f),
                        new Vector3(1.05f, 0.3f, 0.06f), palette.DarkMetal, false);
                }
            }

            // --- feux
            GameObject lights = EditorBuildUtility.CreateEmpty("Feux", car.transform, Vector3.zero);

            for (int side = -1; side <= 1; side += 2)
            {
                Box(lights.transform, "Phare", new Vector3(2.19f, 0.96f, side * 0.62f),
                    new Vector3(0.09f, 0.22f, 0.38f), palette.Headlight, false);

                Box(lights.transform, "Feu arriere", new Vector3(-2.19f, 0.96f, side * 0.66f),
                    new Vector3(0.08f, 0.18f, 0.34f), palette.Taillight, false);
            }

            if (headlightsOn)
            {
                for (int side = -1; side <= 1; side += 2)
                {
                    GameObject beam = EditorBuildUtility.CreateEmpty("Faisceau", lights.transform,
                        new Vector3(2.3f, 0.96f, side * 0.62f));

                    beam.transform.localRotation = Quaternion.Euler(0f, 0f, -90f);

                    NightMeshFactory.CreateVisual(NightMeshFactory.WideCone, "Cone", beam.transform,
                        new Vector3(0f, 4.2f, 0f), Quaternion.Euler(180f, 0f, 0f),
                        new Vector3(3.2f, 8.4f, 3.2f), palette.GlowWhite);
                }

                GameObject spotGo = EditorBuildUtility.CreateEmpty("Projecteur", lights.transform,
                    new Vector3(2.35f, 0.96f, 0f));

                spotGo.transform.localRotation = Quaternion.Euler(4f, 90f, 0f);

                Light spot = spotGo.AddComponent<Light>();
                spot.type = LightType.Spot;
                spot.spotAngle = 62f;
                spot.range = 26f;
                spot.intensity = 3.2f;
                spot.color = new Color(1f, 0.95f, 0.86f);
                spot.shadows = LightShadows.Soft;
                spot.shadowStrength = 0.8f;
                spot.renderMode = LightRenderMode.ForcePixel;

                AddLight(lights.transform, "Lueur arriere", new Vector3(-2.4f, 0.96f, 0f),
                    new Color(1f, 0.15f, 0.12f), 1.3f, 5f, false, false);

                for (int side = -1; side <= 1; side += 2)
                {
                    EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Nappe arriere",
                        lights.transform, new Vector3(-2.25f, 0.96f, side * 0.66f),
                        new Vector3(0.55f, 0.5f, 0.75f), palette.GlowRed, false);
                }

                AddFlicker(lights, NeonFlicker.Pattern.Calme, 9f, 0.05f, 0.9f, 131f);
            }

            if (!rusty) return;

            // --- plaques de rouille et bosses
            //
            // Elles sont posées EN SURFACE, légèrement décalées : la carrosserie en dessous
            // reste lisse, donc la rouille accroche la lumière différemment. Une simple
            // texture rouillée sur toute la caisse donne au contraire une voiture d'une
            // seule matière, ce qui se lit comme une couleur, pas comme une usure.
            float[] px = { 1.7f, -0.9f, 0.4f, -1.9f };
            float[] py = { 0.72f, 0.92f, 0.62f, 0.8f };
            float[] pz = { 0.95f, -0.95f, 0.94f, -0.94f };
            float[] ps = { 0.55f, 0.75f, 0.4f, 0.6f };

            for (int i = 0; i < px.Length; i++)
            {
                Box(car.transform, "Rouille", new Vector3(px[i], py[i], pz[i] * 1.005f),
                    new Vector3(ps[i], ps[i] * 0.55f, 0.02f), palette.Rust, false);
            }

            Box(car.transform, "Portiere enfoncee", new Vector3(-0.35f, 0.78f, 0.93f),
                new Vector3(1.1f, 0.44f, 0.03f), palette.CarRoof, false);
        }

        // ------------------------------------------------------------------ accessoires

        private static void BuildProps(Transform parent, NightMaterialFactory.Palette palette)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Accessoires", parent, Vector3.zero);
            Transform t = root.transform;

            Dumpster(t, palette, new Vector3(-20f, SidewalkHeight, 10.4f), 12f);
            Dumpster(t, palette, new Vector3(30f, SidewalkHeight, -14.5f), -102f);

            TrashPile(t, palette, new Vector3(-17.4f, SidewalkHeight, 10.9f));
            TrashPile(t, palette, new Vector3(27.6f, SidewalkHeight, -14.9f));

            BusShelter(t, palette, new Vector3(9f, SidewalkHeight, -14.6f));

            Hydrant(t, palette, new Vector3(-6.2f, SidewalkHeight, 7.9f));
            Hydrant(t, palette, new Vector3(24f, SidewalkHeight, -11.9f));

            for (int i = 0; i < 7; i++)
            {
                float x = -13f + i * 4.3f;

                // L'axe de l'entree reste degage : une borne plantee au milieu du tapis
                // rouge bloquerait le passage le plus emprunte de la scene.
                if (Mathf.Abs(x) < 4f) continue;

                Bollard(t, palette, new Vector3(x, SidewalkHeight, RoadFar + 0.5f));
            }

            Cone(t, palette, new Vector3(-14.2f, 0f, 2.1f), 17f);
            Cone(t, palette, new Vector3(-13.1f, 0f, 1.2f), -34f);
            Cone(t, palette, new Vector3(13.6f, 0f, -4.4f), 8f);

            NewsBox(t, palette, new Vector3(-3.4f, SidewalkHeight, 8.2f), -14f);
            BikeRack(t, palette, new Vector3(13.5f, SidewalkHeight, 9.2f));

            Bottles(t, palette, new Vector3(-8.2f, SidewalkHeight, 9.4f), 7);
            Bottles(t, palette, new Vector3(6.4f, 0f, -6.2f), 5);

            Pallet(t, palette, new Vector3(-24.5f, SidewalkHeight, 11.4f), 22f);
            Crate(t, palette, new Vector3(-22.6f, SidewalkHeight, 11.1f), 0.8f, -12f);
            // Centre de masse AU-DESSUS de la caisse du dessous : décalée comme avant, elle
            // basculait toute seule au premier pas de simulation.
            Crate(t, palette, new Vector3(-22.5f, SidewalkHeight + 0.8f, 11.2f), 0.66f, 31f);

            Grate(t, palette, new Vector3(-4f, 0f, RoadFar - 0.9f));
            Grate(t, palette, new Vector3(16f, 0f, RoadNear + 0.9f));
            Manhole(t, palette, new Vector3(2.6f, 0f, -3.4f));
            Manhole(t, palette, new Vector3(-19f, 0f, 0.8f));
        }

        internal static void Dumpster(Transform parent, NightMaterialFactory.Palette palette, Vector3 position, float yaw)
        {
            GameObject dumpster = EditorBuildUtility.CreateEmpty("Benne", parent, position);
            dumpster.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(dumpster.transform, "Cuve", new Vector3(0f, 0.62f, 0f), new Vector3(2.3f, 1.2f, 1.25f),
                palette.Rust, true);

            Box(dumpster.transform, "Couvercle", new Vector3(0f, 1.26f, -0.12f), new Vector3(2.35f, 0.12f, 1.35f),
                palette.DarkMetal, false);

            Box(dumpster.transform, "Couvercle ouvert", new Vector3(0f, 1.62f, 0.76f),
                new Vector3(2.3f, 0.1f, 1.3f), palette.DarkMetal, false)
                .transform.localRotation = Quaternion.Euler(-58f, 0f, 0f);

            for (int i = -1; i <= 1; i += 2)
            {
                Cylinder(dumpster.transform, "Roue", new Vector3(i * 0.95f, 0.11f, 0.5f),
                    new Vector3(0.22f, 0.06f, 0.22f), palette.DarkMetal, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        private static void TrashPile(Transform parent, NightMaterialFactory.Palette palette, Vector3 position)
        {
            GameObject pile = EditorBuildUtility.CreateEmpty("Sacs poubelle", parent, position);

            // Les hauteurs valent le rayon du collider (la moitié de la plus grande échelle) :
            // des sacs physiques qui se chevauchent au départ s'écartent violemment à la
            // première image, et la pile explose avant même qu'on l'ait touchée.
            float[] x = { 0f, 0.55f, -0.45f, 0.2f };
            float[] y = { 0.31f, 0.27f, 0.29f, 0.82f };
            float[] z = { 0f, 0.3f, 0.4f, 0.16f };
            float[] s = { 0.62f, 0.54f, 0.58f, 0.46f };

            for (int i = 0; i < x.Length; i++)
            {
                GameObject bag = EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Sac", pile.transform,
                    new Vector3(x[i], y[i], z[i]), new Vector3(s[i], s[i] * 0.85f, s[i] * 0.92f),
                    palette.DarkMetal, true);

                bag.transform.localRotation = Quaternion.Euler(i * 17f, i * 41f, i * 23f);
                MakePhysical(bag, 3f, PhysicsProp.Matter.Mou, 1.2f);
            }
        }

        /// <summary>
        /// L'abribus. Son panneau publicitaire rétroéclairé est l'une des rares sources
        /// BLANCHES et LARGES de la rue : il découpe en contre-jour tout ce qui passe
        /// devant, ce qu'aucun néon coloré ne fait.
        /// </summary>
        private static void BusShelter(Transform parent, NightMaterialFactory.Palette palette, Vector3 position)
        {
            GameObject shelter = EditorBuildUtility.CreateEmpty("Abribus", parent, position);

            Box(shelter.transform, "Toit", new Vector3(0f, 2.55f, 0f), new Vector3(4.2f, 0.12f, 1.6f),
                palette.DarkMetal, false);

            for (int i = -1; i <= 1; i += 2)
            {
                Box(shelter.transform, "Montant", new Vector3(i * 2f, 1.28f, 0.7f),
                    new Vector3(0.12f, 2.55f, 0.12f), palette.Metal, true);
            }

            Box(shelter.transform, "Paroi arriere", new Vector3(0f, 1.3f, -0.72f),
                new Vector3(4f, 2.3f, 0.06f), palette.Glass, true);

            Box(shelter.transform, "Banc", new Vector3(0f, 0.52f, -0.42f), new Vector3(3f, 0.08f, 0.42f),
                palette.Metal, true);

            GameObject panel = EditorBuildUtility.CreateEmpty("Panneau", shelter.transform,
                new Vector3(2.35f, 1.35f, 0f));

            Box(panel.transform, "Caisson", new Vector3(0f, 0f, 0f), new Vector3(0.18f, 2.5f, 1.4f),
                palette.DarkMetal, true);

            Box(panel.transform, "Affiche", new Vector3(-0.1f, 0f, 0f), new Vector3(0.04f, 2.2f, 1.2f),
                palette.NeonWhite, false);

            AddLight(panel.transform, "Lumiere", new Vector3(-0.6f, 0f, 0f),
                new Color(0.95f, 0.97f, 1f), 2f, 9f, true, false);

            AddFlicker(panel, NeonFlicker.Pattern.Bourdonnement, 3.2f, 0.1f, 1.1f, 149f);
        }

        private static void Hydrant(Transform parent, NightMaterialFactory.Palette palette, Vector3 position)
        {
            GameObject hydrant = EditorBuildUtility.CreateEmpty("Borne incendie", parent, position);

            Cylinder(hydrant.transform, "Corps", new Vector3(0f, 0.36f, 0f), new Vector3(0.28f, 0.36f, 0.28f),
                palette.Plastic, true);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Sphere, "Chapeau", hydrant.transform,
                new Vector3(0f, 0.76f, 0f), new Vector3(0.3f, 0.2f, 0.3f), palette.Plastic, false);

            for (int i = -1; i <= 1; i += 2)
            {
                Cylinder(hydrant.transform, "Sortie", new Vector3(i * 0.18f, 0.5f, 0f),
                    new Vector3(0.12f, 0.06f, 0.12f), palette.Chrome, false)
                    .transform.localRotation = Quaternion.Euler(0f, 0f, 90f);
            }
        }

        internal static void Bollard(Transform parent, NightMaterialFactory.Palette palette, Vector3 position)
        {
            GameObject bollard = EditorBuildUtility.CreateEmpty("Borne", parent, position);

            Cylinder(bollard.transform, "Fut", new Vector3(0f, 0.42f, 0f), new Vector3(0.14f, 0.42f, 0.14f),
                palette.DarkMetal, true);

            Cylinder(bollard.transform, "Bande", new Vector3(0f, 0.74f, 0f), new Vector3(0.155f, 0.045f, 0.155f),
                palette.RoadPaint, false);
        }

        private static void Cone(Transform parent, NightMaterialFactory.Palette palette, Vector3 position, float yaw)
        {
            GameObject cone = EditorBuildUtility.CreateEmpty("Plot", parent, position);
            cone.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(cone.transform, "Embase", new Vector3(0f, 0.025f, 0f), new Vector3(0.42f, 0.05f, 0.42f),
                palette.Plastic, false);

            NightMeshFactory.CreateVisual(NightMeshFactory.LightCone, "Corps", cone.transform,
                new Vector3(0f, 0.31f, 0f), Quaternion.identity, new Vector3(0.62f, 0.56f, 0.62f), palette.Plastic);

            Cylinder(cone.transform, "Bande", new Vector3(0f, 0.34f, 0f), new Vector3(0.21f, 0.035f, 0.21f),
                palette.RoadPaint, false);

            // Deux volumes : une embase plate, qui le fait tenir debout, et une capsule pour le
            // corps, qui le fait ROULER une fois couché — un plot renversé qui glisse à plat
            // comme une boîte se voit tout de suite.
            BoxCollider foot = cone.AddComponent<BoxCollider>();
            foot.center = new Vector3(0f, 0.025f, 0f);
            foot.size = new Vector3(0.42f, 0.05f, 0.42f);

            CapsuleCollider body = cone.AddComponent<CapsuleCollider>();
            body.center = new Vector3(0f, 0.32f, 0f);
            body.radius = 0.12f;
            body.height = 0.56f;
            body.direction = 1;

            MakePhysical(cone, 2.4f, PhysicsProp.Matter.Plastique, 0.15f);
        }

        private static void NewsBox(Transform parent, NightMaterialFactory.Palette palette, Vector3 position, float yaw)
        {
            GameObject box = EditorBuildUtility.CreateEmpty("Distributeur", parent, position);
            box.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(box.transform, "Caisse", new Vector3(0f, 0.6f, 0f), new Vector3(0.55f, 1.2f, 0.45f),
                palette.Plastic, true);

            Box(box.transform, "Vitre", new Vector3(0f, 0.85f, -0.23f), new Vector3(0.42f, 0.5f, 0.03f),
                palette.Glass, false);

            MakePhysical(box, 22f, PhysicsProp.Matter.Metal, 0.1f);

            for (int i = -1; i <= 1; i += 2)
            {
                Box(box.transform, "Pied", new Vector3(i * 0.2f, 0.1f, 0f), new Vector3(0.06f, 0.2f, 0.06f),
                    palette.DarkMetal, false);
            }
        }

        private static void BikeRack(Transform parent, NightMaterialFactory.Palette palette, Vector3 position)
        {
            GameObject rack = EditorBuildUtility.CreateEmpty("Arceaux velo", parent, position);

            for (int i = 0; i < 3; i++)
            {
                GameObject arch = EditorBuildUtility.CreateEmpty("Arceau", rack.transform,
                    new Vector3(0f, 0f, i * 1.1f));

                for (int side = -1; side <= 1; side += 2)
                {
                    Cylinder(arch.transform, "Montant", new Vector3(side * 0.34f, 0.35f, 0f),
                        new Vector3(0.06f, 0.35f, 0.06f), palette.Metal, true);
                }

                Box(arch.transform, "Traverse", new Vector3(0f, 0.7f, 0f), new Vector3(0.74f, 0.06f, 0.06f),
                    palette.Metal, false);
            }
        }

        internal static void Bottles(Transform parent, NightMaterialFactory.Palette palette, Vector3 position, int count)
        {
            GameObject group = EditorBuildUtility.CreateEmpty("Bouteilles", parent, position);

            for (int i = 0; i < count; i++)
            {
                float angle = i * 2.39996f;
                Vector3 offset = new Vector3(Mathf.Cos(angle) * (0.2f + i * 0.13f), 0f,
                    Mathf.Sin(angle) * (0.2f + i * 0.13f));

                bool standing = i % 3 == 0;

                GameObject bottle = Cylinder(group.transform, "Bouteille",
                    offset + new Vector3(0f, standing ? 0.12f : 0.026f, 0f),
                    new Vector3(0.05f, 0.12f, 0.05f), palette.Glass, true);

                bottle.transform.localRotation = standing
                    ? Quaternion.identity
                    : Quaternion.Euler(90f, i * 47f, 0f);

                MakePhysical(bottle, 0.35f, PhysicsProp.Matter.Verre, 0.1f);
            }
        }

        /// <summary>
        /// Rend un objet du décor physique : corps rigide, son d'impact, masse réaliste.
        ///
        /// Le corps rigide va sur la RACINE de l'objet : tous les colliders de ses enfants
        /// deviennent alors un seul collider composé, et une caisse faite de six planches reste
        /// une caisse au lieu de se disloquer en six morceaux.
        ///
        /// L'interpolation est activée parce que ces objets passent souvent très près de la
        /// caméra en volant ; sans elle, ils saccadent au rythme de la physique et non de
        /// l'affichage. Les objets légers passent en détection continue : une bouteille frappée
        /// traverse sinon un mur en une seule image.
        /// </summary>
        internal static Rigidbody MakePhysical(GameObject go, float mass, PhysicsProp.Matter matter, float damping)
        {
            go.isStatic = false;

            Rigidbody body = go.GetComponent<Rigidbody>();
            if (body == null) body = go.AddComponent<Rigidbody>();

            body.mass = mass;
            body.linearDamping = damping;
            body.angularDamping = 0.35f;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = mass < 2f
                ? CollisionDetectionMode.ContinuousDynamic
                : CollisionDetectionMode.Discrete;

            PhysicsProp prop = go.GetComponent<PhysicsProp>();
            if (prop == null) prop = go.AddComponent<PhysicsProp>();

            SerializedWiring.SetEnum(prop, "_matter", (int)matter);
            return body;
        }

        internal static void Pallet(Transform parent, NightMaterialFactory.Palette palette, Vector3 position, float yaw)
        {
            GameObject pallet = EditorBuildUtility.CreateEmpty("Palette", parent, position);
            pallet.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            Box(pallet.transform, "Traverse", new Vector3(0f, 0.04f, 0f), new Vector3(1.2f, 0.08f, 1.2f),
                palette.Wood, true);

            for (int i = 0; i < 4; i++)
            {
                Box(pallet.transform, "Planche", new Vector3(0f, 0.11f, -0.45f + i * 0.3f),
                    new Vector3(1.2f, 0.05f, 0.2f), palette.Wood, false);
            }

            MakePhysical(pallet, 11f, PhysicsProp.Matter.Bois, 0.1f);
        }

        /// <summary>
        /// Une caisse physique. <paramref name="position"/> est le CENTRE DE SA BASE, pas son
        /// centre : une caisse statique enfoncée à moitié dans le trottoir ne se voyait pas,
        /// une caisse physique enfoncée est éjectée au premier pas de simulation.
        /// </summary>
        internal static void Crate(Transform parent, NightMaterialFactory.Palette palette, Vector3 position,
            float size, float yaw)
        {
            GameObject crate = Box(parent, "Caisse", position + Vector3.up * size * 0.5f,
                Vector3.one * size, palette.Wood, true);

            crate.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            MakePhysical(crate, 6f * size, PhysicsProp.Matter.Bois, 0.05f);
        }

        /// <summary>
        /// Grille d'égout : quelques barreaux au ras du sol. Minuscule, mais c'est le genre
        /// d'objet qui prouve que la chaussée est une chaussée et pas un plan texturé.
        /// </summary>
        private static void Grate(Transform parent, NightMaterialFactory.Palette palette, Vector3 position)
        {
            GameObject grate = EditorBuildUtility.CreateEmpty("Grille", parent, position);

            Slab(grate.transform, "Cadre", new Vector3(0f, 0.008f, 0f), new Vector3(0.95f, 0.016f, 0.42f),
                palette.DarkMetal);

            for (int i = 0; i < 6; i++)
            {
                Slab(grate.transform, "Barreau", new Vector3(-0.38f + i * 0.152f, 0.016f, 0f),
                    new Vector3(0.07f, 0.02f, 0.36f), palette.Metal);
            }
        }

        private static void Manhole(Transform parent, NightMaterialFactory.Palette palette, Vector3 position)
        {
            Cylinder(parent, "Cerclage", position + new Vector3(0f, 0.006f, 0f),
                new Vector3(0.82f, 0.006f, 0.82f), palette.DarkMetal, false);

            Cylinder(parent, "Plaque d'egout", position + new Vector3(0f, 0.012f, 0f),
                new Vector3(0.72f, 0.008f, 0.72f), palette.Metal, false);
        }

        // ------------------------------------------------------------------ bruine

        /// <summary>
        /// Une bruine fine, en particules additives.
        ///
        /// Elle justifie le sol mouillé — sans pluie, une chaussée miroir est une décision
        /// arbitraire ; avec elle, c'est une conséquence. Et surtout elle met de la MATIÈRE
        /// dans l'air : chaque goutte attrape la lumière des enseignes, donc l'espace entre
        /// la caméra et les façades cesse d'être du vide.
        ///
        /// Elle est simulée en espace monde et suit le joueur : simuler la pluie sur toute
        /// la rue coûterait cent fois plus de particules pour un résultat identique, puisque
        /// seules celles qui sont devant la caméra sont visibles.
        /// </summary>
        private static void BuildDrizzle(Transform parent)
        {
            Material material = NightMaterialFactory.CreateRain(NightMaterialFactory.MaterialsFolder, "M_Bruine",
                new Color(0.72f, 0.82f, 1f), 1.1f);

            if (material == null) return;

            GameObject go = EditorBuildUtility.CreateEmpty("Bruine", parent, new Vector3(0f, 14f, 0f));

            // Le systeme emet le long de son axe AVANT. Pour que la pluie tombe, c'est donc
            // l'objet qui est bascule vers le bas, pas une gravite qu'on augmenterait : la
            // vitesse initiale garde alors sa direction meme quand le joueur se deplace.
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem system = go.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule main = system.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 1.6f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(9f, 13f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.03f, 0.07f);
            main.startColor = new ParticleSystem.MinMaxGradient(
                new Color(0.72f, 0.82f, 1f, 0.45f), new Color(0.9f, 0.95f, 1f, 0.8f));
            main.gravityModifier = 0.6f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 1400;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 420f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(42f, 34f, 0.5f);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.055f;
            renderer.lengthScale = 2.4f;
            renderer.cameraVelocityScale = 0f;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 10;

            go.AddComponent<FollowCameraFlat>();
        }
    }
}
