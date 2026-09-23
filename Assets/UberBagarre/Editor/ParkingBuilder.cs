using UberBagarre.View;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Le parking derrière le Vertigo : le décor du chapitre 1.
    ///
    /// Il devait contraster avec la rue, sinon le chapitre aurait l'air de rejouer le prologue.
    /// La rue est CHAUDE — sodium orange, néons magenta, vitrines. Le parking est FROID : des
    /// mâts à LED d'un blanc bleuté, un grillage, du béton. Même nuit, même bitume mouillé, et
    /// pourtant un autre lieu au premier coup d'œil. C'est la couleur de la lumière qui fait
    /// ce travail, pas la géométrie.
    ///
    /// C'est aussi un décor de COMBAT à plusieurs. Deux adversaires demandent de l'espace pour
    /// tourner, et des obstacles pour ne pas se faire prendre en tenaille : l'allée centrale
    /// est large, et elle est bordée de voitures, de fûts et de caisses dans lesquels on peut
    /// les envoyer — la bousculade et le décor physique trouvent ici leur raison d'être.
    ///
    /// Le repère : l'entrée est côté Z négatifs, le mur du club côté Z positifs, et la
    /// camionnette blanche des frères Kovac au milieu de l'allée.
    /// </summary>
    public static class ParkingBuilder
    {
        public const float Width = 46f;
        public const float Depth = 36f;

        public class Result
        {
            public Transform Root;
            public Renderer Ground;
            public Transform Arrival;

            /// <summary>La camionnette : le point que le joueur doit approcher.</summary>
            public Transform Van;

            /// <summary>Portière de la voiture du joueur, pour repartir.</summary>
            public Interactable Car;

            /// <summary>Où se tiennent les deux frères.</summary>
            public Vector3 FirstBrother;
            public Vector3 SecondBrother;
        }

        public static Result Build(NightMaterialFactory.Palette night, Vector3 origin)
        {
            GameObject root = new GameObject("=== Parking du Vertigo ===");
            root.transform.position = origin;
            Transform t = root.transform;

            Material white = EditorBuildUtility.CreateOrUpdateMaterial(NightMaterialFactory.MaterialsFolder,
                "M_CarrosserieBlanche", new Color(0.78f, 0.78f, 0.76f), 0.46f, 0.2f);

            Material red = EditorBuildUtility.CreateOrUpdateMaterial(NightMaterialFactory.MaterialsFolder,
                "M_PlastiqueRouge", new Color(0.62f, 0.06f, 0.05f), 0.4f, 0f);

            Result result = new Result();
            result.Root = t;
            result.Ground = BuildGround(t, night);

            BuildBays(t, night);
            BuildClubWall(t, night);
            BuildFences(t, night);
            BuildEntrance(t, night, red);
            BuildMasts(t, night);

            // Voitures garées : elles bordent l'allée et servent d'obstacles. Leur nez pointe
            // vers l'allée (+X local de la voiture vers -Z du monde), comme dans un vrai parking.
            NightStreetBuilder.Car(t, night, new Vector3(-10.2f, 0f, 10.4f), 90f, false, false);
            NightStreetBuilder.Car(t, night, new Vector3(-4.8f, 0f, 10.6f), 88f, false, false);
            NightStreetBuilder.Car(t, night, new Vector3(10.6f, 0f, 10.3f), 93f, false, false);
            NightStreetBuilder.Car(t, night, new Vector3(-8.0f, 0f, -3.2f), -90f, false, false);

            // La voiture du joueur, garée à l'entrée, phares allumés : il vient d'arriver.
            NightStreetBuilder.Car(t, night, new Vector3(-6.4f, 0f, -12.6f), 12f, true, true);
            result.Car = CarDoor(t, new Vector3(-6.4f, 1f, -13.6f));

            result.Van = BuildVan(t, night, white, new Vector3(5.6f, 0f, 3.2f), 176f);

            BuildProps(t, night);

            result.FirstBrother = t.TransformPoint(new Vector3(3.6f, 0f, 0.4f));
            result.SecondBrother = t.TransformPoint(new Vector3(7.4f, 0f, 1.1f));

            GameObject arrival = EditorBuildUtility.CreateEmpty("Arrivee", t, new Vector3(-3.4f, 0f, -11.4f));
            arrival.transform.localRotation = Quaternion.Euler(0f, 18f, 0f);
            result.Arrival = arrival.transform;

            return result;
        }

        // ------------------------------------------------------------------ sol

        private static Renderer BuildGround(Transform parent, NightMaterialFactory.Palette night)
        {
            GameObject ground = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Bitume", parent,
                new Vector3(0f, -0.25f, 0f), new Vector3(Width, 0.5f, Depth), night.WetAsphalt, true);

            // Même reflet planaire que la rue : sans lui, le parking paraîtrait sec à côté d'elle,
            // alors qu'il pleut sur les deux.
            PlanarReflection reflection = ground.AddComponent<PlanarReflection>();
            SerializedWiring.SetBool(reflection, "_usePlaneHeightOverride", true);
            SerializedWiring.SetFloat(reflection, "_planeHeightOverride", parent.position.y);
            SerializedWiring.SetInt(reflection, "_downsample", 2);

            return ground.GetComponent<Renderer>();
        }

        /// <summary>Marquage des places : deux rangées face à face, de part et d'autre de l'allée.</summary>
        private static void BuildBays(Transform parent, NightMaterialFactory.Palette night)
        {
            GameObject bays = EditorBuildUtility.CreateEmpty("Places", parent, Vector3.zero);

            for (int i = 0; i <= 12; i++)
            {
                float x = -15.6f + i * 2.6f;

                NightStreetBuilder.Slab(bays.transform, "Ligne", new Vector3(x, 0.006f, 10.4f),
                    new Vector3(0.12f, 0.012f, 5f), night.RoadPaint);

                NightStreetBuilder.Slab(bays.transform, "Ligne", new Vector3(x, 0.006f, -3.4f),
                    new Vector3(0.12f, 0.012f, 5f), night.RoadPaint);
            }

            // Flèches de sens de circulation dans l'allée, peintes au sol : un parking sans
            // marquage d'allée se lit comme une esplanade vide.
            for (int i = 0; i < 3; i++)
            {
                GameObject arrow = EditorBuildUtility.CreateEmpty("Fleche", bays.transform,
                    new Vector3(-10f + i * 10f, 0.007f, 3.5f));

                NightStreetBuilder.Slab(arrow.transform, "Fut", Vector3.zero, new Vector3(1.8f, 0.012f, 0.22f), night.RoadPaint);

                NightStreetBuilder.Slab(arrow.transform, "Pointe", new Vector3(0.9f, 0f, 0.28f),
                    new Vector3(0.8f, 0.012f, 0.18f), night.RoadPaint).transform.localRotation = Quaternion.Euler(0f, -40f, 0f);

                NightStreetBuilder.Slab(arrow.transform, "Pointe", new Vector3(0.9f, 0f, -0.28f),
                    new Vector3(0.8f, 0.012f, 0.18f), night.RoadPaint).transform.localRotation = Quaternion.Euler(0f, 40f, 0f);
            }
        }

        // ------------------------------------------------------------------ enceinte

        /// <summary>
        /// L'arrière du club. Une seule porte de service, sous une ampoule verte « SORTIE » :
        /// c'est l'unique touche de couleur chaude dans un décor bleuté, et elle attire l'œil
        /// vers le fond.
        /// </summary>
        private static void BuildClubWall(Transform parent, NightMaterialFactory.Palette night)
        {
            GameObject wall = EditorBuildUtility.CreateEmpty("Mur du club", parent, new Vector3(0f, 0f, Depth * 0.5f));

            NightStreetBuilder.Box(wall.transform, "Mur", new Vector3(0f, 4f, 0.6f),
                new Vector3(Width, 8f, 1.2f), night.DarkBrick, true);

            NightStreetBuilder.Box(wall.transform, "Porte de service", new Vector3(-3f, 1.1f, -0.02f),
                new Vector3(1.2f, 2.2f, 0.1f), night.DarkMetal, false);

            GameObject exit = EditorBuildUtility.CreateEmpty("Sortie", wall.transform, new Vector3(-3f, 2.6f, -0.25f));
            NeonTextBuilder.Build(exit.transform, "SORTIE", 0.18f, 0.03f, night.NeonGreen);
            NightStreetBuilder.AddLight(exit.transform, "Lueur", new Vector3(0f, 0f, -0.6f),
                new Color(0.35f, 1f, 0.45f), 1.4f, 6f, false, false);
            NightStreetBuilder.AddFlicker(exit, NeonFlicker.Pattern.Bourdonnement, 5.5f, 0.2f, 1.2f, 211f);

            // Ventilations et gaines : le mur arrière d'une boîte de nuit est couvert de ce
            // qu'on ne met jamais en façade.
            for (int i = 0; i < 3; i++)
            {
                NightStreetBuilder.Box(wall.transform, "Ventilation", new Vector3(6f + i * 5.5f, 5.8f, -0.25f),
                    new Vector3(1.2f, 0.9f, 0.5f), night.Metal, false);

                NightStreetBuilder.Box(wall.transform, "Gaine", new Vector3(6f + i * 5.5f, 3.3f, -0.2f),
                    new Vector3(0.28f, 4.2f, 0.28f), night.DarkMetal, false);
            }

            NightStreetBuilder.Dumpster(wall.transform, night, new Vector3(-8.5f, 0f, -1.1f), 180f);
            NightStreetBuilder.TrashPile(wall.transform, night, new Vector3(-11f, 0f, -1.4f));
        }

        private static void BuildFences(Transform parent, NightMaterialFactory.Palette night)
        {
            GameObject fences = EditorBuildUtility.CreateEmpty("Grillages", parent, Vector3.zero);

            float halfX = Width * 0.5f;
            float halfZ = Depth * 0.5f;

            Fence(fences.transform, night, new Vector3(-halfX, 0f, -halfZ), new Vector3(-halfX, 0f, halfZ));
            Fence(fences.transform, night, new Vector3(halfX, 0f, -halfZ), new Vector3(halfX, 0f, halfZ));

            // L'entrée laisse un passage de huit mètres : on doit y revenir en courant, pas se
            // demander où est la sortie.
            Fence(fences.transform, night, new Vector3(-halfX, 0f, -halfZ), new Vector3(-10f, 0f, -halfZ));
            Fence(fences.transform, night, new Vector3(-2f, 0f, -halfZ), new Vector3(halfX, 0f, -halfZ));
        }

        private static void Fence(Transform parent, NightMaterialFactory.Palette night, Vector3 from, Vector3 to)
        {
            Vector3 delta = to - from;
            float length = delta.magnitude;
            if (length < 0.4f) return;

            GameObject run = EditorBuildUtility.CreateEmpty("Pan", parent, (from + to) * 0.5f);
            run.transform.localRotation = Quaternion.LookRotation(delta.normalized, Vector3.up);

            const float height = 2.6f;
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 3f));

            for (int i = 0; i <= posts; i++)
            {
                float z = -length * 0.5f + length * i / posts;

                NightStreetBuilder.Cylinder(run.transform, "Poteau", new Vector3(0f, height * 0.5f, z),
                    new Vector3(0.07f, height * 0.5f, 0.07f), night.Metal, true);
            }

            for (int rail = 0; rail < 3; rail++)
            {
                NightStreetBuilder.Box(run.transform, "Lisse", new Vector3(0f, 0.2f + rail * 1.2f, 0f),
                    new Vector3(0.04f, 0.04f, length), night.Metal, false);
            }

            // Le grillage lui-même : un voile de fins montants serrés. Un mur invisible porte le
            // collider, pour qu'on ne passe pas au travers des interstices.
            NightStreetBuilder.Box(run.transform, "Butee", new Vector3(0f, height * 0.5f, 0f),
                new Vector3(0.05f, height, length), null, true).GetComponent<Renderer>().enabled = false;

            int slats = Mathf.Max(2, Mathf.RoundToInt(length / 0.22f));

            for (int i = 0; i < slats; i++)
            {
                float z = -length * 0.5f + length * (i + 0.5f) / slats;

                NightStreetBuilder.Box(run.transform, "Maille", new Vector3(0f, height * 0.5f, z),
                    new Vector3(0.012f, height - 0.1f, 0.012f), night.Metal, false);
            }
        }

        /// <summary>La barrière levante de l'entrée, et la borne à tickets.</summary>
        private static void BuildEntrance(Transform parent, NightMaterialFactory.Palette night, Material red)
        {
            GameObject entrance = EditorBuildUtility.CreateEmpty("Entree", parent,
                new Vector3(-6f, 0f, -Depth * 0.5f + 0.6f));

            NightStreetBuilder.Box(entrance.transform, "Borne", new Vector3(4.6f, 0.65f, 0f),
                new Vector3(0.45f, 1.3f, 0.45f), night.DarkMetal, true);

            NightStreetBuilder.Box(entrance.transform, "Ecran", new Vector3(4.6f, 1.05f, -0.23f),
                new Vector3(0.28f, 0.18f, 0.02f), night.NeonCyan, false);

            GameObject arm = EditorBuildUtility.CreateEmpty("Bras", entrance.transform, new Vector3(4.6f, 1.1f, 0f));

            // Levée, en biais : la barrière est restée ouverte. Personne ne paie ici.
            arm.transform.localRotation = Quaternion.Euler(0f, 0f, 68f);

            for (int i = 0; i < 6; i++)
            {
                NightStreetBuilder.Box(arm.transform, "Segment", new Vector3(-0.35f - i * 0.6f, 0f, 0f),
                    new Vector3(0.6f, 0.08f, 0.06f), i % 2 == 0 ? red : night.RoadPaint, false);
            }

            GameObject sign = EditorBuildUtility.CreateEmpty("Enseigne", entrance.transform, new Vector3(-4.4f, 0f, 0f));

            NightStreetBuilder.Cylinder(sign.transform, "Mat", new Vector3(0f, 2f, 0f),
                new Vector3(0.1f, 2f, 0.1f), night.DarkMetal, true);

            GameObject letters = EditorBuildUtility.CreateEmpty("Mot", sign.transform, new Vector3(0f, 4.2f, -0.1f));
            NeonTextBuilder.Build(letters.transform, "PARKING", 0.42f, 0.055f, night.NeonCyan);

            NightStreetBuilder.AddLight(letters.transform, "Halo", new Vector3(0f, 0.2f, -0.6f),
                new Color(0.25f, 0.9f, 1f), 1.8f, 8f, false, false);

            NightStreetBuilder.AddFlicker(letters, NeonFlicker.Pattern.Fatigue, 6f, 0.2f, 1f, 173f);
        }

        /// <summary>
        /// Les mâts d'éclairage : LED froides, pas sodium. C'est ce seul changement de couleur
        /// qui distingue le parking de la rue.
        /// </summary>
        private static void BuildMasts(Transform parent, NightMaterialFactory.Palette night)
        {
            Vector3[] positions =
            {
                new Vector3(-13f, 0f, 3.5f), new Vector3(13f, 0f, 3.5f),
                new Vector3(0f, 0f, -9.5f), new Vector3(0f, 0f, 14f)
            };

            for (int i = 0; i < positions.Length; i++)
            {
                GameObject mast = EditorBuildUtility.CreateEmpty("Mat d'eclairage", parent, positions[i]);

                NightStreetBuilder.Cylinder(mast.transform, "Fut", new Vector3(0f, 4f, 0f),
                    new Vector3(0.16f, 4f, 0.16f), night.Metal, true);

                NightStreetBuilder.Box(mast.transform, "Tete", new Vector3(0f, 8.05f, 0f),
                    new Vector3(1.1f, 0.14f, 0.5f), night.DarkMetal, false);

                NightStreetBuilder.Box(mast.transform, "Panneau", new Vector3(0f, 7.95f, 0f),
                    new Vector3(1f, 0.04f, 0.42f), night.NeonWhite, false);

                NightStreetBuilder.AddLight(mast.transform, "Lumiere", new Vector3(0f, 7.8f, 0f),
                    new Color(0.82f, 0.9f, 1f), 4.2f, 24f, true, i == 0);

                NightMeshFactory.CreateVisual(NightMeshFactory.WideCone, "Cone", mast.transform,
                    new Vector3(0f, 4.1f, 0f), Quaternion.identity, new Vector3(9f, 7.4f, 9f), night.GlowWhite);

                // Un mât sur quatre est en fin de vie : dans un parking, c'est toujours le cas.
                NeonFlicker.Pattern pattern = i == 2 ? NeonFlicker.Pattern.Fatigue : NeonFlicker.Pattern.Bourdonnement;
                NightStreetBuilder.AddFlicker(mast, pattern, 5f, 0.06f, 1f, i * 19f + 7f);
            }
        }

        // ------------------------------------------------------------------ vehicules

        /// <summary>
        /// La camionnette blanche des frères Kovac, portes arrière ouvertes et soute éclairée.
        /// C'est le repère de la scène : on la voit depuis l'entrée, et c'est vers elle qu'on
        /// marche.
        /// </summary>
        private static Transform BuildVan(Transform parent, NightMaterialFactory.Palette night, Material paint,
            Vector3 position, float yaw)
        {
            GameObject van = EditorBuildUtility.CreateEmpty("Camionnette blanche", parent, position);
            van.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            Transform t = van.transform;

            NightStreetBuilder.Box(t, "Caisse", new Vector3(-0.5f, 1.35f, 0f), new Vector3(3.9f, 1.95f, 2.05f), paint, true);
            NightStreetBuilder.Box(t, "Cabine", new Vector3(2.05f, 1.05f, 0f), new Vector3(1.3f, 1.35f, 2f), paint, true);
            NightStreetBuilder.Box(t, "Capot", new Vector3(2.8f, 0.78f, 0f), new Vector3(0.6f, 0.75f, 1.95f), paint, false);
            NightStreetBuilder.Box(t, "Bas de caisse", new Vector3(0.3f, 0.42f, 0f), new Vector3(5.6f, 0.34f, 2.08f), night.DarkMetal, false);

            NightStreetBuilder.Box(t, "Pare-brise", new Vector3(2.62f, 1.42f, 0f), new Vector3(0.05f, 0.62f, 1.8f), night.Glass, false)
                .transform.localRotation = Quaternion.Euler(0f, 0f, -18f);

            for (int side = -1; side <= 1; side += 2)
            {
                NightStreetBuilder.Box(t, "Vitre", new Vector3(2.05f, 1.42f, side * 1.01f),
                    new Vector3(1f, 0.5f, 0.03f), night.Glass, false);

                // Portes arrière ouvertes en grand : on voit la soute, et ce qui y brille.
                GameObject door = EditorBuildUtility.CreateEmpty("Porte arriere", t,
                    new Vector3(-2.45f, 1.35f, side * 1.0f));

                door.transform.localRotation = Quaternion.Euler(0f, side * 105f, 0f);

                NightStreetBuilder.Box(door.transform, "Battant", new Vector3(-0.5f, 0f, 0f),
                    new Vector3(1f, 1.9f, 0.05f), paint, false);
            }

            for (int fx = -1; fx <= 1; fx += 2)
            {
                for (int fz = -1; fz <= 1; fz += 2)
                {
                    Vector3 hub = new Vector3(fx < 0 ? -1.6f : 2.1f, 0.38f, fz * 0.95f);

                    NightStreetBuilder.Cylinder(t, "Pneu", hub, new Vector3(0.76f, 0.13f, 0.76f), night.Rubber, false)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }

            // La lumiere est juste DERRIERE les portes ouvertes, hors du volume plein de la caisse :
            // c'est elle qui fait croire que la soute est eclairee.
            GameObject cargo = EditorBuildUtility.CreateEmpty("Soute", t, new Vector3(-2.75f, 1.55f, 0f));
            NightStreetBuilder.AddLight(cargo.transform, "Plafonnier", Vector3.zero,
                new Color(1f, 0.86f, 0.62f), 1.8f, 7f, true, false);

            NightStreetBuilder.Box(cargo.transform, "Tube", new Vector3(0.28f, 0.72f, 0f),
                new Vector3(0.06f, 0.04f, 1.4f), night.NeonWarm, false);

            NightStreetBuilder.AddFlicker(cargo, NeonFlicker.Pattern.Fatigue, 5f, 0.25f, 1.3f, 97f);

            // Ce qu'ils ont sorti de la soute ne sera jamais expliqué. Des caisses au sol, derriere
            // les portes, suffisent — DANS la caisse pleine, elles seraient ejectees au demarrage.
            NightStreetBuilder.Crate(t, night, new Vector3(-3.35f, 0f, 0.55f), 0.55f, 8f);
            NightStreetBuilder.Crate(t, night, new Vector3(-3.5f, 0f, -0.4f), 0.5f, -14f);

            return t;
        }

        private static Interactable CarDoor(Transform parent, Vector3 localPosition)
        {
            GameObject door = EditorBuildUtility.CreateEmpty("Portiere (retour)", parent, localPosition);

            BoxCollider collider = door.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.6f, 2f, 2.6f);
            collider.isTrigger = true;

            Interactable interactable = door.AddComponent<Interactable>();
            SerializedWiring.SetString(interactable, "_label", "Reprendre la voiture");
            SerializedWiring.SetString(interactable, "_hint", "Retour a la planque");
            SerializedWiring.SetFloat(interactable, "_range", 3f);
            SerializedWiring.SetBool(interactable, "_once", true);
            SerializedWiring.SetBool(interactable, "_enabledForPlayer", false);

            return interactable;
        }

        // ------------------------------------------------------------------ accessoires

        /// <summary>
        /// Les obstacles du combat. Ils sont répartis autour de l'allée, jamais dedans : il faut
        /// pouvoir tourner autour de deux adversaires sans buter à chaque pas, mais avoir de
        /// quoi en envoyer un dans le décor.
        /// </summary>
        private static void BuildProps(Transform parent, NightMaterialFactory.Palette night)
        {
            GameObject props = EditorBuildUtility.CreateEmpty("Accessoires", parent, Vector3.zero);
            Transform t = props.transform;

            // Fûts métalliques : lourds, on ne les soulève pas, on les renverse.
            Barrel(t, night, new Vector3(12.8f, 0f, -1.2f));
            Barrel(t, night, new Vector3(13.6f, 0f, -0.2f));
            Barrel(t, night, new Vector3(12.9f, 0f, 0.9f));
            Barrel(t, night, new Vector3(-14.2f, 0f, 6.1f));

            NightStreetBuilder.Crate(t, night, new Vector3(9.4f, 0f, 6.4f), 0.8f, 12f);
            NightStreetBuilder.Crate(t, night, new Vector3(10.3f, 0f, 6.6f), 0.7f, -20f);
            NightStreetBuilder.Crate(t, night, new Vector3(9.8f, 0.8f, 6.45f), 0.6f, 35f);

            NightStreetBuilder.Pallet(t, night, new Vector3(-12.6f, 0f, -6.2f), 30f);

            // Les bouteilles des deux frères, au pied de la camionnette : de quoi s'armer.
            NightStreetBuilder.Bottles(t, night, new Vector3(4.2f, 0f, 1.8f), 6);
            NightStreetBuilder.Bottles(t, night, new Vector3(-1.6f, 0f, -5.6f), 3);

            for (int i = 0; i < 4; i++)
            {
                NightStreetBuilder.Cone(t, night, new Vector3(-2.6f + i * 1.3f, 0f, -8.4f), i * 23f);
            }
        }

        private static void Barrel(Transform parent, NightMaterialFactory.Palette night, Vector3 position)
        {
            GameObject barrel = NightStreetBuilder.Cylinder(parent, "Fut", position + new Vector3(0f, 0.45f, 0f),
                new Vector3(0.58f, 0.45f, 0.58f), night.Rust, true);

            NightStreetBuilder.Cylinder(barrel.transform, "Cerclage", new Vector3(0f, 0.35f, 0f),
                new Vector3(1.04f, 0.05f, 1.04f), night.DarkMetal, false);

            NightStreetBuilder.Cylinder(barrel.transform, "Cerclage", new Vector3(0f, -0.35f, 0f),
                new Vector3(1.04f, 0.05f, 1.04f), night.DarkMetal, false);

            NightStreetBuilder.MakePhysical(barrel, 28f, PhysicsProp.Matter.Metal, 0.2f);
        }
    }
}
