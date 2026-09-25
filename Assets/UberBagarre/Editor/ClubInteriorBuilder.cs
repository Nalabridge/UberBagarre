using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.View;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// L'intérieur du Vertigo : la salle, le bar, la scène du DJ, la piste, et au fond à
    /// droite, derrière les barrières, la fosse où l'on se bat pour de l'argent.
    ///
    /// Trois zones, trois lumières, et c'est volontaire :
    ///
    /// - le BAR est chaud et calme (ambre, suspensions basses) — on s'y repère ;
    /// - la PISTE est colorée et bouge (lyres qui balaient la fumée, dalles qui battent le
    ///   tempo) — c'est le club ;
    /// - la FOSSE est blanche et dure (quatre projecteurs à la verticale, ombres franches) —
    ///   c'est un ring. Le joueur doit la reconnaître au premier coup d'œil, depuis l'entrée.
    ///
    /// Toute la lumière est RÉELLE : ce sont des lampes Unity, rendues par pixel en différé,
    /// avec leurs ombres, et c'est à partir d'elles que le post-traitement calcule les
    /// faisceaux dans la fumée. Le sol en béton ciré les reflète grâce à une sonde à projection
    /// en boîte calée sur la salle — c'est ce qui donne l'aspect « physique » des surfaces.
    ///
    /// Le public est construit avec le même corps que les combattants. Il n'a ni vie ni zones
    /// de frappe : il regarde, réagit, et fait mur autour de la fosse (chaque spectateur a un
    /// collider) — la foule renvoie les combattants au centre, comme une vraie foule.
    /// </summary>
    public static class ClubInteriorBuilder
    {
        public const float HalfWidth = 16f;
        public const float HalfDepth = 12f;
        public const float Height = 6.5f;
        public const float DoorWidth = 2.4f;
        public const float DoorHeight = 2.7f;

        /// <summary>Centre de la fosse, en local.</summary>
        public static readonly Vector3 RingCenter = new Vector3(8.5f, 0f, -3.5f);

        public const float RingRadius = 4.6f;

        private static readonly Vector3 DanceFloorCenter = new Vector3(-2f, 0f, 3.5f);

        public class Result
        {
            public Transform Root;
            public Transform Arrival;

            /// <summary>La porte, de l'intérieur : ressortir du club.</summary>
            public Interactable Exit;

            public Transform Ring;

            /// <summary>La barrière qui ferme la fosse. Ouverte = désactivée.</summary>
            public GameObject RingGate;

            /// <summary>Où attend le champion de la fosse (monde), et vers où il regarde.</summary>
            public Vector3 ChampionPosition;
            public float ChampionYaw;

            public CrowdAudio Crowd;
            public List<Spectator> RingCrowd = new List<Spectator>();
        }

        private class Materials
        {
            public Material Floor;
            public Material Varnish;
            public Material Speaker;
            public Material RingCanvas;
            public readonly List<Material> Shirts = new List<Material>();
            public readonly List<Material> Pants = new List<Material>();
        }

        public static Result Build(NightMaterialFactory.Palette night, BuildMaterials body, Vector3 origin)
        {
            GameObject root = new GameObject("=== Le Vertigo (interieur) ===");
            root.transform.position = origin;
            Transform t = root.transform;

            Materials m = CreateMaterials(night);

            Result result = new Result();
            result.Root = t;

            BuildShell(t, night, m, result);
            BuildBar(t, night, m, body);
            Transform stage = BuildStage(t, night, m, body);
            BuildDanceFloor(t, night, m, body, stage);
            BuildRing(t, night, m, body, result);
            BuildAmbience(t, night);

            // Sonde a projection en boite : chaque surface lisse reflete CETTE salle, a la
            // bonne place. Le beton cire du sol renvoie les dalles de la piste et les lyres.
            EditorBuildUtility.AddReflectionProbe(t, "Sonde de reflexion (salle)",
                new Vector3(0f, Height * 0.5f, 0f), new Vector3(HalfWidth * 2f, Height, HalfDepth * 2f), true, 1f);

            GameObject arrival = EditorBuildUtility.CreateEmpty("Arrivee", t, new Vector3(0f, 0f, -HalfDepth + 1.8f));
            arrival.transform.localRotation = Quaternion.Euler(0f, 25f, 0f);
            result.Arrival = arrival.transform;

            return result;
        }

        // ------------------------------------------------------------------ matériaux

        private static Materials CreateMaterials(NightMaterialFactory.Palette night)
        {
            const string folder = NightMaterialFactory.MaterialsFolder;
            Materials m = new Materials();

            Texture concreteAlbedo = night.Concrete != null ? night.Concrete.mainTexture : null;
            Texture concreteNormal = night.Concrete != null && night.Concrete.HasProperty("_BumpMap")
                ? night.Concrete.GetTexture("_BumpMap")
                : null;

            // Beton cire : sombre, TRES lisse. C'est la surface qui fait le plus pour
            // l'impression de rendu physique, parce qu'elle reflete tout ce qui brille.
            m.Floor = EditorBuildUtility.CreateOrUpdateMaterial(folder, "M_BetonCire",
                new Color(0.30f, 0.30f, 0.32f), 0.74f, 0f, concreteAlbedo as Texture2D, new Vector2(10f, 8f));
            EditorBuildUtility.ApplyNormalMap(m.Floor, concreteNormal as Texture2D, 0.35f);

            m.Varnish = EditorBuildUtility.CreateOrUpdateMaterial(folder, "M_ComptoirVernis",
                new Color(0.17f, 0.09f, 0.05f), 0.82f, 0f);

            m.Speaker = EditorBuildUtility.CreateOrUpdateMaterial(folder, "M_Enceinte",
                new Color(0.03f, 0.03f, 0.035f), 0.28f, 0f);

            m.RingCanvas = EditorBuildUtility.CreateOrUpdateMaterial(folder, "M_ToileFosse",
                new Color(0.30f, 0.05f, 0.05f), 0.22f, 0f);

            // Vêtements du public : des teintes de sortie de nuit, sombres, avec quelques
            // couleurs franches pour que la foule ne soit pas une masse uniforme.
            Color[] shirts =
            {
                new Color(0.08f, 0.08f, 0.09f), new Color(0.55f, 0.08f, 0.12f), new Color(0.85f, 0.85f, 0.82f),
                new Color(0.10f, 0.20f, 0.42f), new Color(0.40f, 0.30f, 0.10f), new Color(0.20f, 0.36f, 0.22f),
                new Color(0.52f, 0.16f, 0.46f), new Color(0.30f, 0.30f, 0.32f)
            };

            for (int i = 0; i < shirts.Length; i++)
            {
                Texture2D weave = EditorBuildUtility.CreateOrUpdateFabricTexture(NightMaterialFactory.TexturesFolder,
                    "T_Public_Haut" + i, 128, shirts[i], 3, 0.10f, 900 + i * 37);

                Material shirt = EditorBuildUtility.CreateOrUpdateMaterial(folder, "M_Public_Haut" + i,
                    Color.white, 0.05f, 0f, weave, new Vector2(10f, 10f));
                shirt.enableInstancing = true;
                m.Shirts.Add(shirt);
            }

            Color[] pants =
            {
                new Color(0.12f, 0.14f, 0.20f), new Color(0.06f, 0.06f, 0.07f),
                new Color(0.34f, 0.36f, 0.42f), new Color(0.22f, 0.18f, 0.14f)
            };

            for (int i = 0; i < pants.Length; i++)
            {
                Material pant = EditorBuildUtility.CreateOrUpdateMaterial(folder, "M_Public_Bas" + i,
                    pants[i], 0.03f, 0f);
                pant.enableInstancing = true;
                m.Pants.Add(pant);
            }

            return m;
        }

        // ------------------------------------------------------------------ murs

        private static void BuildShell(Transform parent, NightMaterialFactory.Palette night, Materials m, Result result)
        {
            GameObject shell = EditorBuildUtility.CreateEmpty("Salle", parent, Vector3.zero);
            Transform t = shell.transform;

            float w = HalfWidth * 2f;
            float d = HalfDepth * 2f;
            const float thick = 0.4f;

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Sol", t, new Vector3(0f, -0.25f, 0f),
                new Vector3(w, 0.5f, d), m.Floor, true);

            NightStreetBuilder.Box(t, "Plafond", new Vector3(0f, Height + 0.2f, 0f),
                new Vector3(w + thick * 2f, 0.4f, d + thick * 2f), night.DarkConcrete, true);

            NightStreetBuilder.Box(t, "Mur nord", new Vector3(0f, Height * 0.5f, HalfDepth + thick * 0.5f),
                new Vector3(w + thick * 2f, Height, thick), night.DarkBrick, true);
            NightStreetBuilder.Box(t, "Mur est", new Vector3(HalfWidth + thick * 0.5f, Height * 0.5f, 0f),
                new Vector3(thick, Height, d), night.DarkBrick, true);
            NightStreetBuilder.Box(t, "Mur ouest", new Vector3(-HalfWidth - thick * 0.5f, Height * 0.5f, 0f),
                new Vector3(thick, Height, d), night.DarkBrick, true);

            // Mur sud, percé de la porte d'entrée.
            float side = (w - DoorWidth) * 0.5f;
            float z = -HalfDepth - thick * 0.5f;

            NightStreetBuilder.Box(t, "Mur sud gauche", new Vector3(-(DoorWidth + side) * 0.5f, Height * 0.5f, z),
                new Vector3(side, Height, thick), night.DarkBrick, true);
            NightStreetBuilder.Box(t, "Mur sud droit", new Vector3((DoorWidth + side) * 0.5f, Height * 0.5f, z),
                new Vector3(side, Height, thick), night.DarkBrick, true);
            NightStreetBuilder.Box(t, "Linteau", new Vector3(0f, (Height + DoorHeight) * 0.5f, z),
                new Vector3(DoorWidth, Height - DoorHeight, thick), night.DarkBrick, true);

            // La porte elle-même, fermée : un double battant métallique au fond du passage.
            NightStreetBuilder.Box(t, "Porte", new Vector3(0f, DoorHeight * 0.5f, z - 0.1f),
                new Vector3(DoorWidth, DoorHeight, 0.08f), night.DarkMetal, true);

            // Sortie de secours : un petit néon vert et sa lueur, comme dans toutes les salles.
            GameObject exitSign = EditorBuildUtility.CreateEmpty("Sortie", t, new Vector3(0f, DoorHeight + 0.3f, -HalfDepth + 0.05f));
            NightStreetBuilder.Box(exitSign.transform, "Panneau", Vector3.zero, new Vector3(0.9f, 0.3f, 0.04f), night.NeonGreen, false);
            NightStreetBuilder.AddLight(exitSign.transform, "Lueur", new Vector3(0f, -0.2f, 0.4f),
                new Color(0.3f, 1f, 0.45f), 0.9f, 4f, false, false);

            GameObject door = EditorBuildUtility.CreateEmpty("Porte (sortie)", t, new Vector3(0f, 1.1f, -HalfDepth + 0.6f));
            BoxCollider trigger = door.AddComponent<BoxCollider>();
            trigger.size = new Vector3(DoorWidth, 2.2f, 1.2f);
            trigger.isTrigger = true;

            Interactable exit = door.AddComponent<Interactable>();
            SerializedWiring.SetString(exit, "_label", "Sortir du club");
            SerializedWiring.SetString(exit, "_hint", "Rentrer a la planque");
            SerializedWiring.SetFloat(exit, "_range", 3f);
            SerializedWiring.SetBool(exit, "_once", true);
            SerializedWiring.SetBool(exit, "_enabledForPlayer", false);
            result.Exit = exit;

            // Charpente : les poutres portent les lampes, et leurs ombres au plafond disent
            // la hauteur de la salle mieux que n'importe quel mur.
            for (int i = -1; i <= 1; i++)
            {
                NightStreetBuilder.Box(t, "Poutre", new Vector3(0f, Height - 0.45f, i * 7f),
                    new Vector3(w, 0.3f, 0.3f), night.DarkMetal, false);
            }

            for (int i = -3; i <= 3; i++)
            {
                NightStreetBuilder.Box(t, "Traverse", new Vector3(i * 4.5f, Height - 0.2f, 0f),
                    new Vector3(0.22f, 0.22f, d), night.DarkMetal, false);
            }

            // Plinthe lumineuse le long des murs : elle dessine le contour de la salle dans le
            // noir, sans rien eclairer d'autre que le pied des murs.
            NightStreetBuilder.Box(t, "Plinthe nord", new Vector3(0f, 0.06f, HalfDepth - 0.03f),
                new Vector3(w, 0.04f, 0.04f), night.NeonBlue, false);
            NightStreetBuilder.Box(t, "Plinthe est", new Vector3(HalfWidth - 0.03f, 0.06f, 0f),
                new Vector3(0.04f, 0.04f, d), night.NeonBlue, false);
            NightStreetBuilder.Box(t, "Plinthe ouest", new Vector3(-HalfWidth + 0.03f, 0.06f, 0f),
                new Vector3(0.04f, 0.04f, d), night.NeonBlue, false);
        }

        // ------------------------------------------------------------------ bar

        private static void BuildBar(Transform parent, NightMaterialFactory.Palette night, Materials m, BuildMaterials body)
        {
            GameObject bar = EditorBuildUtility.CreateEmpty("Bar", parent, new Vector3(-HalfWidth + 2.8f, 0f, 0f));
            Transform t = bar.transform;

            NightStreetBuilder.Box(t, "Comptoir", new Vector3(0f, 0.55f, 0f), new Vector3(1f, 1.1f, 12f), night.DarkMetal, true);
            NightStreetBuilder.Box(t, "Plateau", new Vector3(0.05f, 1.13f, 0f), new Vector3(1.25f, 0.06f, 12.2f), m.Varnish, true);

            // Bandeau lumineux sous le plateau, cote salle : la lumiere rasante sur le metal du
            // comptoir est ce qui le rend lisible depuis la piste.
            NightStreetBuilder.Box(t, "Bandeau", new Vector3(0.52f, 1.02f, 0f), new Vector3(0.03f, 0.04f, 12f), night.NeonWarm, false);

            // Etageres du fond, retroeclairees.
            float back = -2.55f;
            for (int i = 0; i < 3; i++)
            {
                float y = 1.35f + i * 0.55f;
                NightStreetBuilder.Box(t, "Etagere", new Vector3(back + 0.2f, y, 0f), new Vector3(0.4f, 0.05f, 9f), m.Varnish, true);
                NightStreetBuilder.Bottles(t, night, new Vector3(back + 0.2f, y + 0.03f, -3f + i * 1.7f), 3);
            }

            NightStreetBuilder.Box(t, "Fond lumineux", new Vector3(back - 0.02f, 1.9f, 0f), new Vector3(0.04f, 1.5f, 9f), night.NeonWarm, false);
            NightStreetBuilder.AddLight(t, "Lueur des etageres", new Vector3(back + 0.6f, 1.9f, -2.5f),
                new Color(1f, 0.66f, 0.32f), 1.6f, 5f, false, false);
            NightStreetBuilder.AddLight(t, "Lueur des etageres", new Vector3(back + 0.6f, 1.9f, 2.5f),
                new Color(1f, 0.66f, 0.32f), 1.6f, 5f, false, false);

            // Bouteilles sur le comptoir : a portee de main, pour qui cherche quoi lancer.
            NightStreetBuilder.Bottles(t, night, new Vector3(0.05f, 1.16f, -4f), 2);
            NightStreetBuilder.Bottles(t, night, new Vector3(0.05f, 1.16f, 3.2f), 2);

            // Tabourets.
            for (int i = 0; i < 5; i++)
            {
                GameObject stool = EditorBuildUtility.CreateEmpty("Tabouret", t, new Vector3(1.05f, 0f, -4f + i * 2f));
                NightStreetBuilder.Cylinder(stool.transform, "Pied", new Vector3(0f, 0.38f, 0f), new Vector3(0.06f, 0.38f, 0.06f), night.Metal, true);
                NightStreetBuilder.Cylinder(stool.transform, "Assise", new Vector3(0f, 0.78f, 0f), new Vector3(0.38f, 0.04f, 0.38f), night.Velvet, true);
            }

            // Suspensions : trois cones de metal, lumiere chaude et basse vers le comptoir.
            for (int i = -1; i <= 1; i++)
            {
                GameObject pendant = EditorBuildUtility.CreateEmpty("Suspension", t, new Vector3(0.05f, 3.3f, i * 4f));
                NightStreetBuilder.Cylinder(pendant.transform, "Fil", new Vector3(0f, (Height - 3.3f) * 0.5f, 0f),
                    new Vector3(0.01f, (Height - 3.3f) * 0.5f, 0.01f), night.DarkMetal, false);
                NightStreetBuilder.Cylinder(pendant.transform, "Abat-jour", new Vector3(0f, 0.08f, 0f),
                    new Vector3(0.42f, 0.1f, 0.42f), night.DarkMetal, false);
                NightStreetBuilder.Cylinder(pendant.transform, "Ampoule", new Vector3(0f, -0.02f, 0f),
                    new Vector3(0.14f, 0.02f, 0.14f), night.NeonWarm, false);

                Light light = NightStreetBuilder.AddSpot(pendant.transform, "Lumiere", new Vector3(0f, -0.05f, 0f),
                    Vector3.down, new Color(1f, 0.72f, 0.42f), 3.4f, 7f, 78f, i == 0);
                NightStreetBuilder.MakeVolumetric(light, 1.6f);
            }

            // Trois clients accoudes, et le barman.
            Transform focus = EditorBuildUtility.CreateEmpty("Regard du bar", t, new Vector3(4f, 1.5f, 0f)).transform;
            AddSpectator(t, body, m, 0, new Vector3(1.55f, 0f, -3.1f), 270f, Spectator.Mood.Accoude, focus, 0.2f, 1.3f, false);
            AddSpectator(t, body, m, 1, new Vector3(1.6f, 0f, 1.2f), 262f, Spectator.Mood.Accoude, focus, 0.3f, 4.1f, false);
            AddSpectator(t, body, m, 2, new Vector3(1.5f, 0f, 3.9f), 275f, Spectator.Mood.Accoude, focus, 0.1f, 7.7f, false);
            AddSpectator(t, body, m, 7, new Vector3(-1.2f, 0f, -0.5f), 90f, Spectator.Mood.Accoude, focus, 0.1f, 9.9f, false);
        }

        // ------------------------------------------------------------------ scène

        private static Transform BuildStage(Transform parent, NightMaterialFactory.Palette night, Materials m, BuildMaterials body)
        {
            GameObject stage = EditorBuildUtility.CreateEmpty("Scene du DJ", parent, new Vector3(0f, 0f, HalfDepth - 2f));
            Transform t = stage.transform;

            NightStreetBuilder.Box(t, "Estrade", new Vector3(0f, 0.4f, 0f), new Vector3(11f, 0.8f, 4f), night.DarkMetal, true);

            GameObject edge = EditorBuildUtility.CreateEmpty("Nez de scene", t, new Vector3(0f, 0.78f, -2.02f));
            NightStreetBuilder.Box(edge.transform, "Bande", Vector3.zero, new Vector3(11f, 0.05f, 0.04f), night.NeonMagenta, false);
            BeatLight edgeBeat = edge.AddComponent<BeatLight>();
            SerializedWiring.SetFloat(edgeBeat, "_baseIntensity", 7f);
            SerializedWiring.SetFloat(edgeBeat, "_floor", 0.35f);

            NightStreetBuilder.Box(t, "Platines", new Vector3(0f, 1.3f, -0.8f), new Vector3(3.2f, 1f, 0.9f), m.Speaker, true);
            NightStreetBuilder.Box(t, "Façade des platines", new Vector3(0f, 1.2f, -1.26f), new Vector3(3f, 0.6f, 0.02f), night.NeonCyan, false);

            // Mur d'ecrans : une grille de dalles qui s'allument en vague, de gauche a droite.
            Color[] palette = { new Color(1f, 0.16f, 0.62f), new Color(0.18f, 0.92f, 1f), new Color(0.35f, 0.3f, 1f), new Color(1f, 0.5f, 0.12f) };

            for (int x = 0; x < 8; x++)
            {
                for (int y = 0; y < 3; y++)
                {
                    GameObject tile = EditorBuildUtility.CreateEmpty("Ecran", t,
                        new Vector3(-4.2f + x * 1.2f, 2.1f + y * 1.15f, 1.9f));
                    NightStreetBuilder.Box(tile.transform, "Dalle", Vector3.zero, new Vector3(1.1f, 1.05f, 0.05f), night.NeonWhite, false);

                    BeatLight beat = tile.AddComponent<BeatLight>();
                    SerializedWiring.SetFloat(beat, "_baseIntensity", 2.2f);
                    SerializedWiring.SetFloat(beat, "_floor", 0.12f);
                    SerializedWiring.SetFloat(beat, "_offset", x * 0.0625f);
                    SerializedWiring.SetInt(beat, "_paletteOffset", (x / 2 + y) % palette.Length);
                    SetColorArray(beat, "_palette", palette);
                }
            }

            // La lumiere du mur d'ecrans sur la scene : une grande lampe douce qui bat avec lui.
            GameObject wash = EditorBuildUtility.CreateEmpty("Lueur des ecrans", t, new Vector3(0f, 3.2f, 0.8f));
            NightStreetBuilder.AddLight(wash.transform, "Lampe", Vector3.zero, new Color(0.6f, 0.4f, 1f), 2.2f, 10f, true, false);
            BeatLight washBeat = wash.AddComponent<BeatLight>();
            SerializedWiring.SetFloat(washBeat, "_floor", 0.45f);
            SetColorArray(washBeat, "_palette", palette);

            // Enceintes.
            for (int side = -1; side <= 1; side += 2)
            {
                GameObject stack = EditorBuildUtility.CreateEmpty("Enceinte", t, new Vector3(side * 6.4f, 0f, 0.6f));
                NightStreetBuilder.Box(stack.transform, "Caisson", new Vector3(0f, 1.7f, 0f), new Vector3(1.3f, 3.4f, 1.1f), m.Speaker, true);

                for (int k = 0; k < 3; k++)
                {
                    NightStreetBuilder.Cylinder(stack.transform, "Membrane", new Vector3(0f, 0.7f + k * 1.05f, -0.56f),
                        new Vector3(0.75f, 0.02f, 0.75f), night.Rubber, false)
                        .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                }
            }

            // Enseigne au-dessus des ecrans.
            GameObject sign = EditorBuildUtility.CreateEmpty("Enseigne VERTIGO", t, new Vector3(0f, 5.65f, 1.95f));
            NeonTextBuilder.Build(sign.transform, NightStreetBuilder.ClubName, 0.55f, 0.06f, night.NeonMagenta);
            NightStreetBuilder.AddFlicker(sign, NeonFlicker.Pattern.Calme, 7f, 0.06f, 1f, 17f);

            // Lyres : quatre projecteurs etroits qui balaient la piste. Leur faisceau dans la
            // fumee vient de la lampe elle-meme ; une forte diffusion simule la machine a fumee.
            Color magenta = new Color(1f, 0.2f, 0.7f);
            Color cyan = new Color(0.25f, 0.9f, 1f);

            for (int i = 0; i < 4; i++)
            {
                GameObject head = EditorBuildUtility.CreateEmpty("Lyre", t, new Vector3(-5.4f + i * 3.6f, Height - 0.75f, -2.2f));
                NightStreetBuilder.Box(head.transform, "Corps", Vector3.zero, new Vector3(0.36f, 0.42f, 0.36f), night.DarkMetal, false);

                Vector3 aim = new Vector3((i - 1.5f) * 0.25f, -1f, -0.75f);
                Light beam = NightStreetBuilder.AddSpot(head.transform, "Faisceau", new Vector3(0f, -0.25f, 0f), aim,
                    i % 2 == 0 ? magenta : cyan, 7f, 24f, 14f, false);

                NightStreetBuilder.Box(beam.transform, "Lentille", new Vector3(0f, 0f, 0.02f), new Vector3(0.18f, 0.18f, 0.02f),
                    i % 2 == 0 ? night.NeonMagenta : night.NeonCyan, false);

                NightStreetBuilder.MakeVolumetric(beam, 3.2f);

                SweepingLight sweep = beam.gameObject.AddComponent<SweepingLight>();
                SerializedWiring.SetFloat(sweep, "_panAmplitude", 38f);
                SerializedWiring.SetFloat(sweep, "_tiltAmplitude", 14f);
                SerializedWiring.SetFloat(sweep, "_beatsPerCycle", 8f);
                SerializedWiring.SetFloat(sweep, "_phase", i * 0.25f);

                BeatLight punch = beam.gameObject.AddComponent<BeatLight>();
                SerializedWiring.SetFloat(punch, "_floor", 0.55f);
            }

            // Le DJ, derriere ses platines, face a la piste.
            Transform crowdFocus = EditorBuildUtility.CreateEmpty("Regard du DJ", parent, DanceFloorCenter + new Vector3(0f, 1.2f, 0f)).transform;
            AddSpectator(t, body, m, 0, new Vector3(0.3f, 0.8f, 0.2f), 180f, Spectator.Mood.Danseur, crowdFocus, 0.2f, 2.2f, false);

            // La musique vient d'ici.
            GameObject music = EditorBuildUtility.CreateEmpty("Musique", t, new Vector3(0f, 2f, 0f));
            music.AddComponent<AudioSource>();
            music.AddComponent<ClubMusic>();

            return EditorBuildUtility.CreateEmpty("Regard de la piste", t, new Vector3(0f, 1.8f, -0.8f)).transform;
        }

        // ------------------------------------------------------------------ piste

        private static void BuildDanceFloor(Transform parent, NightMaterialFactory.Palette night, Materials m,
            BuildMaterials body, Transform stageFocus)
        {
            GameObject floor = EditorBuildUtility.CreateEmpty("Piste", parent, DanceFloorCenter);
            Transform t = floor.transform;

            Color[] palette = { new Color(1f, 0.16f, 0.62f), new Color(0.18f, 0.92f, 1f), new Color(0.35f, 0.3f, 1f), new Color(0.6f, 0.2f, 1f) };

            for (int x = 0; x < 4; x++)
            {
                for (int z = 0; z < 4; z++)
                {
                    GameObject tile = EditorBuildUtility.CreateEmpty("Dalle", t, new Vector3(-3f + x * 2f, 0.012f, -3f + z * 2f));
                    NightStreetBuilder.Box(tile.transform, "Verre", Vector3.zero, new Vector3(1.9f, 0.02f, 1.9f), night.NeonWhite, false);

                    BeatLight beat = tile.AddComponent<BeatLight>();
                    SerializedWiring.SetFloat(beat, "_baseIntensity", 1.3f);
                    SerializedWiring.SetFloat(beat, "_floor", 0.1f);
                    SerializedWiring.SetFloat(beat, "_offset", ((x + z) % 2) * 0.5f);
                    SerializedWiring.SetInt(beat, "_paletteOffset", (x + z) % palette.Length);
                    SetColorArray(beat, "_palette", palette);
                }
            }

            // Deux lampes au ras de la piste : la couleur des dalles remonte sur les jambes des
            // danseurs, ce qu'un materiau emissif seul ne fait pas.
            for (int i = 0; i < 2; i++)
            {
                GameObject glow = EditorBuildUtility.CreateEmpty("Lueur de piste", t, new Vector3(i == 0 ? -1.8f : 1.8f, 0.35f, 0f));
                NightStreetBuilder.AddLight(glow.transform, "Lampe", Vector3.zero, palette[i], 1.6f, 6f, true, false);

                BeatLight beat = glow.AddComponent<BeatLight>();
                SerializedWiring.SetFloat(beat, "_floor", 0.25f);
                SerializedWiring.SetFloat(beat, "_offset", i * 0.5f);
                SerializedWiring.SetInt(beat, "_paletteOffset", i);
                SetColorArray(beat, "_palette", palette);
            }

            // Les danseurs, face a la scene.
            Vector2[] spots =
            {
                new Vector2(-2.6f, 2.2f), new Vector2(-0.8f, 2.6f), new Vector2(1.2f, 2.0f), new Vector2(2.6f, 2.8f),
                new Vector2(-2.0f, 0.2f), new Vector2(0.2f, 0.6f), new Vector2(2.2f, 0.1f), new Vector2(-0.9f, -1.6f)
            };

            for (int i = 0; i < spots.Length; i++)
            {
                AddSpectator(t, body, m, i + 3, new Vector3(spots[i].x, 0f, spots[i].y), 0f, Spectator.Mood.Danseur,
                    stageFocus, 0.7f, 11f + i * 3.7f, false);
            }
        }

        // ------------------------------------------------------------------ fosse

        private static void BuildRing(Transform parent, NightMaterialFactory.Palette night, Materials m,
            BuildMaterials body, Result result)
        {
            GameObject ring = EditorBuildUtility.CreateEmpty("La fosse", parent, RingCenter);
            Transform t = ring.transform;
            result.Ring = t;

            // Le sol de la fosse : une toile rouge sombre, tachee — on sait ou on est.
            NightStreetBuilder.Cylinder(t, "Toile", new Vector3(0f, 0.011f, 0f),
                new Vector3(RingRadius * 2f - 0.3f, 0.01f, RingRadius * 2f - 0.3f), m.RingCanvas, false);

            // Barrieres en cercle. Celle qui fait face a l'entree de la salle (cote ouest) est
            // la PORTE : elle s'ouvre pour laisser entrer le joueur, et se referme derriere lui.
            const int segments = 14;
            for (int i = 0; i < segments; i++)
            {
                float angle = 180f + i * (360f / segments);
                float radians = angle * Mathf.Deg2Rad;
                Vector3 radial = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians));
                float yaw = Mathf.Atan2(radial.x, radial.z) * Mathf.Rad2Deg;

                GameObject barrier = NightStreetBuilder.Barrier(t, night, radial * RingRadius, yaw);

                // Un collider plein : les montants seuls laisseraient passer un combattant
                // entre deux barreaux, et le ring n'en serait pas un.
                BoxCollider wall = barrier.AddComponent<BoxCollider>();
                wall.center = new Vector3(0f, 0.6f, 0f);
                wall.size = new Vector3(2.2f, 1.2f, 0.12f);

                if (i == 0)
                {
                    barrier.name = "Barriere (porte de la fosse)";
                    result.RingGate = barrier;
                }
            }

            // Quatre projecteurs a la verticale : une lumiere blanche et dure, des ombres nettes.
            // La fosse se voit depuis l'entree, et le combat y est lisible.
            for (int i = 0; i < 4; i++)
            {
                float a = (45f + i * 90f) * Mathf.Deg2Rad;
                Vector3 position = new Vector3(Mathf.Cos(a) * 1.9f, Height - 0.7f, Mathf.Sin(a) * 1.9f);

                GameObject rig = EditorBuildUtility.CreateEmpty("Projecteur de fosse", t, position);
                NightStreetBuilder.Cylinder(rig.transform, "Carter", new Vector3(0f, 0.15f, 0f), new Vector3(0.36f, 0.18f, 0.36f), night.DarkMetal, false);

                Vector3 aim = new Vector3(-position.x * 0.12f, -1f, -position.z * 0.12f);
                Light light = NightStreetBuilder.AddSpot(rig.transform, "Lumiere", Vector3.zero, aim,
                    new Color(1f, 0.95f, 0.88f), 4.2f, 10f, 58f, i % 2 == 0);

                NightStreetBuilder.Cylinder(light.transform, "Lentille", new Vector3(0f, 0f, 0.01f), new Vector3(0.24f, 0.01f, 0.24f), night.NeonWhite, false)
                    .transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

                NightStreetBuilder.MakeVolumetric(light, 1.9f);
            }

            // Au-dessus, sur le mur : LA FOSSE, en rouge.
            GameObject sign = EditorBuildUtility.CreateEmpty("Enseigne LA FOSSE", parent,
                new Vector3(HalfWidth - 0.05f, 4.1f, RingCenter.z));
            sign.transform.localRotation = Quaternion.Euler(0f, 90f, 0f);
            NeonTextBuilder.Build(sign.transform, "LA FOSSE", 0.62f, 0.07f, night.NeonRed);
            NightStreetBuilder.AddLight(sign.transform, "Lueur", new Vector3(0f, 0.3f, -0.6f), new Color(1f, 0.15f, 0.12f), 2f, 9f, true, false);
            NightStreetBuilder.AddFlicker(sign, NeonFlicker.Pattern.Bourdonnement, 7f, 0.12f, 1f, 71f);

            // Tables hautes et caisses autour : de quoi lancer, de quoi buter.
            BuildHighTable(parent, night, RingCenter + new Vector3(-6.2f, 0f, -5.4f));
            BuildHighTable(parent, night, RingCenter + new Vector3(-6.4f, 0f, 3.9f));
            NightStreetBuilder.Crate(parent, night, new Vector3(HalfWidth - 0.9f, 0f, -HalfDepth + 1.2f), 0.7f, 12f);
            NightStreetBuilder.Crate(parent, night, new Vector3(HalfWidth - 1.7f, 0f, -HalfDepth + 1.0f), 0.6f, -18f);
            NightStreetBuilder.Crate(parent, night, new Vector3(HalfWidth - 1.1f, 0.7f, -HalfDepth + 1.2f), 0.55f, 30f);

            // Le public, en deux rangs, face au centre. La porte (cote ouest) reste degagee.
            Transform focus = EditorBuildUtility.CreateEmpty("Centre de la fosse", t, new Vector3(0f, 1.2f, 0f)).transform;

            int index = 0;
            float[] radii = { 5.5f, 6.4f };

            for (int row = 0; row < radii.Length; row++)
            {
                int count = row == 0 ? 12 : 10;

                for (int i = 0; i < count; i++)
                {
                    float angle = 180f + (i + 0.5f + row * 0.5f) * (360f / count);
                    float delta = Mathf.DeltaAngle(angle, 180f);
                    if (Mathf.Abs(delta) < 26f) continue;

                    float radians = angle * Mathf.Deg2Rad;
                    Vector3 local = new Vector3(Mathf.Cos(radians), 0f, Mathf.Sin(radians)) * radii[row];
                    Vector3 inRoom = RingCenter + local;

                    // Pas de spectateur dans les murs ni sur la piste.
                    if (Mathf.Abs(inRoom.x) > HalfWidth - 0.6f || Mathf.Abs(inRoom.z) > HalfDepth - 0.6f) continue;

                    float yaw = Mathf.Atan2(-local.x, -local.z) * Mathf.Rad2Deg;

                    Spectator spectator = AddSpectator(t, body, m, index, local, yaw, Spectator.Mood.Spectateur, focus,
                        0.35f + ((index * 37) % 10) * 0.065f, 30f + index * 5.3f, row == 0);

                    result.RingCrowd.Add(spectator);
                    index++;
                }
            }

            // La voix du public : au-dessus de la fosse.
            GameObject voice = EditorBuildUtility.CreateEmpty("Voix du public", t, new Vector3(0f, 1.6f, 0f));
            result.Crowd = voice.AddComponent<CrowdAudio>();

            // Le champion attend au fond de la fosse, face a la porte.
            result.ChampionPosition = t.TransformPoint(new Vector3(2.2f, 0f, 0f));
            result.ChampionYaw = 270f;
        }

        private static void BuildHighTable(Transform parent, NightMaterialFactory.Palette night, Vector3 position)
        {
            GameObject table = EditorBuildUtility.CreateEmpty("Mange-debout", parent, position);
            NightStreetBuilder.Cylinder(table.transform, "Pied", new Vector3(0f, 0.55f, 0f), new Vector3(0.08f, 0.55f, 0.08f), night.Metal, true);
            NightStreetBuilder.Cylinder(table.transform, "Plateau", new Vector3(0f, 1.1f, 0f), new Vector3(0.75f, 0.025f, 0.75f), night.DarkMetal, true);
            NightStreetBuilder.Bottles(table.transform, night, new Vector3(0f, 1.13f, 0f), 2);
        }

        // ------------------------------------------------------------------ ambiance

        /// <summary>
        /// Quatre projecteurs de couleur dans les angles : ils donnent aux murs leur teinte
        /// de club, et battent doucement le tempo.
        /// </summary>
        private static void BuildAmbience(Transform parent, NightMaterialFactory.Palette night)
        {
            Color[] colors =
            {
                new Color(1f, 0.2f, 0.65f), new Color(0.25f, 0.85f, 1f),
                new Color(1f, 0.18f, 0.14f), new Color(0.35f, 0.35f, 1f)
            };

            Vector3[] corners =
            {
                new Vector3(-HalfWidth + 1f, Height - 1f, HalfDepth - 1f), new Vector3(HalfWidth - 1f, Height - 1f, HalfDepth - 1f),
                new Vector3(HalfWidth - 1f, Height - 1f, -HalfDepth + 1f), new Vector3(-HalfWidth + 1f, Height - 1f, -HalfDepth + 1f)
            };

            for (int i = 0; i < corners.Length; i++)
            {
                GameObject wash = EditorBuildUtility.CreateEmpty("Projecteur d'ambiance", parent, corners[i]);
                Vector3 aim = new Vector3(-corners[i].x, -Height * 0.9f, -corners[i].z);

                Light light = NightStreetBuilder.AddSpot(wash.transform, "Lumiere", Vector3.zero, aim, colors[i], 3.2f, 26f, 70f, false);
                NightStreetBuilder.MakeVolumetric(light, 0.6f);

                BeatLight beat = wash.AddComponent<BeatLight>();
                SerializedWiring.SetFloat(beat, "_floor", 0.65f);
                SerializedWiring.SetFloat(beat, "_offset", i * 0.25f);
            }
        }

        // ------------------------------------------------------------------ public

        /// <summary>
        /// Un spectateur : le corps des combattants, sans rien de ce qui sert à se battre.
        ///
        /// Les zones de frappe des poings et des pieds sont retirées : un public ne doit jamais
        /// pouvoir blesser personne, même par un bug de câblage. Le collider en capsule, lui,
        /// reste : c'est lui qui fait mur autour de la fosse.
        /// </summary>
        /// <summary>
        /// Des passants de la nuit, pour les foules qui se forment dehors autour d'une bagarre :
        /// memes corps et memes reactions que le public du club, caches jusqu'a ce qu'on les appelle.
        /// </summary>
        internal static Spectator[] BuildPassersby(Transform parent, NightMaterialFactory.Palette night,
            BuildMaterials body, int count, int seed)
        {
            Materials m = CreateMaterials(night);
            Spectator[] members = new Spectator[count];

            for (int i = 0; i < count; i++)
            {
                float temperament = 0.3f + ((i * 7 + seed) % 10) * 0.06f;

                members[i] = AddSpectator(parent, body, m, i * 3 + seed, new Vector3(i * 0.8f, 0f, 0f), 0f,
                    Spectator.Mood.Spectateur, null, temperament, seed * 1.7f + i * 2.3f, true);

                members[i].name = "Badaud " + (i + 1);
                members[i].gameObject.SetActive(false);
            }

            return members;
        }

        private static Spectator AddSpectator(Transform parent, BuildMaterials body, Materials m, int look,
            Vector3 localPosition, float yaw, Spectator.Mood mood, Transform focus, float temperament, float seed,
            bool castShadows)
        {
            GameObject go = EditorBuildUtility.CreateEmpty(mood == Spectator.Mood.Danseur ? "Danseur" : "Spectateur",
                parent, localPosition);
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);

            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.75f;
            capsule.radius = 0.27f;
            capsule.center = new Vector3(0f, 0.875f, 0f);

            FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(body);
            skin.Shirt = m.Shirts[look % m.Shirts.Count];
            skin.Pants = m.Pants[(look * 3 + 1) % m.Pants.Count];

            FighterBuilder.Result built = FighterBuilder.BuildBody(go.transform, go.transform, skin, true, Faction.Neutral, go);

            Hitbox[] hitboxes = go.GetComponentsInChildren<Hitbox>(true);
            for (int i = 0; i < hitboxes.Length; i++) Object.DestroyImmediate(hitboxes[i]);

            // Le public n'a pas besoin de projeter d'ombre au fond de la salle : seuls les
            // premiers rangs autour de la fosse, sous les projecteurs, en projettent.
            Renderer[] renderers = go.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++)
            {
                renderers[i].shadowCastingMode = castShadows
                    ? UnityEngine.Rendering.ShadowCastingMode.On
                    : UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Spectator spectator = go.AddComponent<Spectator>();
            SerializedWiring.SetObject(spectator, "_rig", built.Rig);
            SerializedWiring.SetObject(spectator, "_locomotion", built.Locomotion);
            SerializedWiring.SetEnum(spectator, "_mood", (int)mood);
            SerializedWiring.SetObject(spectator, "_focus", focus);
            SerializedWiring.SetFloat(spectator, "_temperament", temperament);
            SerializedWiring.SetFloat(spectator, "_seed", seed);
            SerializedWiring.Verify(spectator, "_rig");

            return spectator;
        }

        private static void SetColorArray(Object target, string fieldName, Color[] colors)
        {
            SerializedObject so = SerializedWiring.Open(target);
            SerializedProperty array = so.FindProperty(fieldName);

            if (array == null)
            {
                Debug.LogWarning("[UberBagarre] Champ '" + fieldName + "' introuvable sur " + target.GetType().Name + ".", target);
                return;
            }

            array.arraySize = colors.Length;
            for (int i = 0; i < colors.Length; i++) array.GetArrayElementAtIndex(i).colorValue = colors[i];

            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
