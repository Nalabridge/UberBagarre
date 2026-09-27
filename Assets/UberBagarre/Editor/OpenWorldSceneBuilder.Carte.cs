using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UberBagarre.Combat;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.View;
using UberBagarre.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Le monde ouvert construit SUR la ville convertie (Assets/Schedule1).
    ///
    /// La ville elle-même n'est pas copiée dans la scène : elle reste dans la sienne, chargée
    /// par-dessus au lancement (MapStreamer). Ici on ne pose que ce qui est à nous :
    /// - la planque : la chambre du motel, meublée (lit, bureau et ordinateur, armoire,
    ///   courrier) — sa porte devient une vraie porte au chargement ;
    /// - le Vertigo : une porte et une enseigne sur la façade du club désaffecté de la ville
    ///   nord, la salle vivant à l'écart comme avant ;
    /// - la circulation et les passants, sur les boucles calculées d'après les rues de la
    ///   ville (à droite pour les voitures, sur le trottoir pour les piétons) ;
    /// - des voitures conduisibles sur des places de parking, et la vieille caisse devant le motel ;
    /// - les lieux des courses : les coins louches de la ville, chacun avec ce que la cible y fait ;
    /// - la carte (vue de dessus de la ville) et ses points de repère.
    /// </summary>
    public static partial class OpenWorldSceneBuilder
    {
        private const string NightFolder = "Assets/UberBagarre/Art/Ville/Generes/Nuit";

        /// <summary>Ce que la planque offre : là où l'on se réveille, et ses meubles utiles.</summary>
        private sealed class MotelHome
        {
            public Transform Root;
            public Transform Arrival;
            public Interactable Bed;
            public Interactable Computer;
            public Interactable Wardrobe;
            public Interactable Letters;
        }

        private static void BuildOnMap(MapPack.Data map)
        {
            EditorBuildUtility.EnsureFolder(SandboxSceneBuilder.ScenesFolder);
            EditorBuildUtility.EnsureFolder(SandboxSceneBuilder.SettingsFolder);
            ProceduralMeshFactory.EnsureLibrary();
            NightMeshFactory.EnsureLibrary();

            BuildMaterials materials = BuildMaterials.CreateAll(26f);
            AttackLibraryBuilder.Library attacks = AttackLibraryBuilder.BuildAll(false);
            NightMaterialFactory.Palette night = NightMaterialFactory.Create(
                NightStreetBuilder.GroundLength, NightStreetBuilder.GroundDepth);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Light sun;
            Light moon;
            SandboxSceneBuilder.BuildLighting(out sun, out moon);

            // --- ce qui est à nous, posé dans la ville
            GameObject world = new GameObject("=== Monde (sur la ville) ===");

            // Le cycle jour / nuit allume et éteint nos enseignes ; la ville a ses propres
            // lampes, que MapStreamer allume.
            NightStreetBuilder.Result street = new NightStreetBuilder.Result { Root = world.transform, CityRoot = world.transform };

            MotelHome home = BuildMotelRoom(map, night, world.transform);
            ClubInteriorBuilder.Result club = ClubInteriorBuilder.Build(night, materials, ClubInteriorOrigin);

            // --- le joueur
            Camera gameCamera;
            Camera observerCamera;
            GameObject player = SandboxSceneBuilder.BuildPlayer(materials, attacks, out gameCamera, out observerCamera);
            player.transform.SetPositionAndRotation(home.Arrival.position, home.Arrival.rotation);

            // Une ville de 800 m : de la jetée on doit voir l'autre rive.
            gameCamera.farClipPlane = 900f;
            observerCamera.farClipPlane = 1000f;

            GraphicsDirector graphics = SandboxSceneBuilder.BuildRendering(sun, moon, street, gameCamera, observerCamera);

            // --- téléphone, histoire minimale, interactions
            GameObject systems = new GameObject("=== Monde ouvert ===");
            MissionBriefing briefing = systems.AddComponent<MissionBriefing>();
            PhoneDevice phone = PrologueSceneBuilder.BuildPhone(night, gameCamera, player, briefing);

            SubtitleDisplay subtitles = systems.AddComponent<SubtitleDisplay>();
            systems.AddComponent<AudioSource>();
            DialogueVoice voice = systems.AddComponent<DialogueVoice>();
            SerializedWiring.SetObject(subtitles, "_voice", voice);

            ScreenFader fader = systems.AddComponent<ScreenFader>();
            PlayerProgress progress = systems.AddComponent<PlayerProgress>();

            PhoneDisplay display = phone.GetComponent<PhoneDisplay>();
            if (display != null) SerializedWiring.SetObject(display, "_progress", progress);
            PhoneOS os = phone.GetComponent<PhoneOS>();
            if (os != null) SerializedWiring.SetObject(os, "_progress", progress);

            PlayerInputReader input = player.GetComponent<PlayerInputReader>();
            InteractionSystem interaction = player.AddComponent<InteractionSystem>();
            SerializedWiring.SetObject(interaction, "_input", input);
            SerializedWiring.SetObject(interaction, "_camera", gameCamera);
            SerializedWiring.SetObject(interaction, "_phone", phone);

            PropHandler props = player.GetComponent<PropHandler>();
            if (props != null)
            {
                SerializedWiring.SetObject(props, "_interaction", interaction);
                SerializedWiring.SetObject(props, "_phone", phone);
            }

            WireHud(player, progress);
            WireDriving(player, interaction, phone, gameCamera, observerCamera);

            // --- le Vertigo : une porte sur la façade, la salle derrière
            BuildVertigoFront(map, night, world.transform, player, club, fader);

            // --- la carte
            CityMap cityMap = BuildMapOnCity(map, player, gameCamera, input, phone);

            // --- cibles, badauds, passants, circulation
            List<OpenWorldDirector.Profile> profiles = BuildTargets(materials, attacks, night);
            FightCrowd crowd = SandboxSceneBuilder.BuildFightCrowd(world.transform, night, materials, 8, 71, "Badauds (monde ouvert)");

            List<Vector3[]> walkLoops = MapPack.Loops(map.walk);
            List<Vector3[]> driveLoops = MapPack.Loops(map.drive);
            GameObject walkers = BuildPedestrians(walkLoops, night, materials, 12, 14);
            GameObject traffic = BuildTraffic(driveLoops, night, new[] { 9, 6, 6 });

            // --- voitures conduisibles : la vieille caisse devant le motel, d'autres garées en ville
            GameObject parked = new GameObject("=== Voitures garees ===");
            CityBuilder.CarFactory cars = new CityBuilder.CarFactory(night, parked.transform);
            if (map.car != null && map.car.Length >= 3)
            {
                cars.Spawn(parked.transform, CityBuilder.CarFactory.Rusty, MapPack.Position(map.car), MapPack.Yaw(map.car), "Ta caisse");
            }

            if (map.cars != null)
            {
                for (int i = 0; i < map.cars.Length; i++)
                {
                    if (map.cars[i] == null || map.cars[i].v == null) continue;
                    cars.Spawn(parked.transform, i * 3 + 1, MapPack.Position(map.cars[i].v), MapPack.Yaw(map.cars[i].v), "Voiture");
                }
            }

            // --- le directeur des courses
            OpenWorldDirector director = systems.AddComponent<OpenWorldDirector>();
            SerializedWiring.SetObject(director, "_phone", phone);
            SerializedWiring.SetObject(director, "_display", display);
            SerializedWiring.SetObject(director, "_input", input);
            SerializedWiring.SetObject(director, "_player", player.GetComponent<Combatant>());
            SerializedWiring.SetObject(director, "_progress", progress);
            SerializedWiring.SetObject(director, "_briefing", briefing);
            SerializedWiring.SetObject(director, "_intro", player.GetComponent<FightIntro>());
            SerializedWiring.SetObject(director, "_crowd", crowd);
            SerializedWiring.SetObject(director, "_map", cityMap);
            SerializedWiring.SetObject(director, "_fader", fader);
            SerializedWiring.SetObject(director, "_subtitles", subtitles);
            SerializedWiring.SetObject(director, "_home", home.Arrival);
            SerializedWiring.SetObject(director, "_targetsRoot", world.transform);
            SerializedWiring.SetFloat(director, "_minimumDistance", 60f);
            director.Configure(Spots(map), profiles.ToArray());
            EditorUtility.SetDirty(director);

            // --- les lumières loin du joueur s'éteignent ; la ville y ajoute les siennes au chargement
            DistanceCuller culler = systems.AddComponent<DistanceCuller>();
            SerializedWiring.SetObject(culler, "_viewer", gameCamera.transform);
            SerializedWiring.SetFloat(culler, "_lightRadius", 70f);
            SandboxSceneBuilder.SetComponentArray(culler, "_lightRoots", world.transform, traffic.transform, parked.transform);

            // --- la ville, chargée par-dessus au lancement
            MapStreamer streamer = systems.AddComponent<MapStreamer>();
            SerializedWiring.SetObject(streamer, "_player", player.transform);
            SerializedWiring.SetObject(streamer, "_controller", player.GetComponent<CharacterController>());
            SerializedWiring.SetObject(streamer, "_fader", fader);
            SerializedWiring.SetObject(streamer, "_subtitles", subtitles);
            SerializedWiring.SetObject(streamer, "_culler", culler);
            SandboxSceneBuilder.SetComponentArray(streamer, "_holdUntilLoaded", player.GetComponent<PlayerMotor>());
            streamer.Configure(MapPack.SceneName, map.water, SafePoints(walkLoops), new[] { map.home.door }, NightSwaps());
            EditorUtility.SetDirty(streamer);

            // --- menu du jeu, menu de triche
            BuildTestTools(player, graphics, progress, subtitles);
            SandboxSceneBuilder.BuildGameMenu(player, graphics, gameCamera, observerCamera);

            club.Root.gameObject.SetActive(false);
            cars.Dispose();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved)
            {
                SandboxSceneBuilder.RegisterSceneInBuildSettings(ScenePath);
                AppendSceneToBuildSettings(MapPack.ScenePath);
            }

            SandboxSceneBuilder.OfferLinearColorSpace();
            Selection.activeGameObject = player;

            Debug.Log("[UberBagarre] Monde ouvert genere SUR LA VILLE (Assets/Schedule1) : " + ScenePath + "\n" +
                      "  La planque    : la chambre du motel (lit, bureau et ordinateur, armoire, courrier)\n" +
                      "  Le Vertigo    : la porte du club desaffecte, ville nord (E pour entrer)\n" +
                      "  Les courses   : " + map.spots.Length + " coins de la ville, chacun avec ce que la cible y fait\n" +
                      "  Passants      : " + walkers.transform.childCount + ", voitures : " + traffic.transform.childCount +
                      ", garees : " + parked.transform.childCount + "\n" +
                      "  La ville se charge par-dessus au lancement (quelques secondes la premiere fois).\n" +
                      "  Appuie sur Play.");
        }

        // ------------------------------------------------------------------ lieux

        private static OpenWorldDirector.Spot[] Spots(MapPack.Data map)
        {
            List<OpenWorldDirector.Spot> spots = new List<OpenWorldDirector.Spot>();
            for (int i = 0; i < map.spots.Length; i++)
            {
                MapPack.Spot s = map.spots[i];
                if (s == null || s.p == null || s.p.Length < 3) continue;

                spots.Add(new OpenWorldDirector.Spot
                {
                    name = s.name,
                    position = MapPack.Position(s.p) + Vector3.up * 0.05f,
                    yaw = s.yaw,
                    activity = s.kind,
                    lookAt = s.face != null && s.face.Length >= 3 ? MapPack.Position(s.face) : Vector3.zero
                });
            }

            return spots.ToArray();
        }

        /// <summary>Un point de trottoir tous les ~10 m : là où l'on repêche le joueur tombé à l'eau.</summary>
        private static Vector3[] SafePoints(List<Vector3[]> loops)
        {
            List<Vector3> points = new List<Vector3>();
            for (int l = 0; l < loops.Count; l++)
            {
                for (int i = 0; i < loops[l].Length; i += 5) points.Add(loops[l][i]);
            }

            return points.ToArray();
        }

        private static void AppendSceneToBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != scenePath) continue;
                if (!scenes[i].enabled) scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }

            // En fin de liste : la ville ne doit jamais devenir la scène de lancement.
            scenes.Add(new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }

        // ------------------------------------------------------------------ la planque

        private static MotelHome BuildMotelRoom(MapPack.Data map, NightMaterialFactory.Palette night, Transform parent)
        {
            MapPack.Home data = map.home;
            HouseBuilder.Palette house = HouseBuilder.CreatePalette();

            MotelHome home = new MotelHome();
            GameObject root = EditorBuildUtility.CreateEmpty("Planque (chambre du motel)", parent, Vector3.zero);
            home.Root = root.transform;

            GameObject arrival = EditorBuildUtility.CreateEmpty("Reveil", root.transform, MapPack.Position(data.inside));
            arrival.transform.rotation = Quaternion.Euler(0f, MapPack.Yaw(data.inside), 0f);
            home.Arrival = arrival.transform;

            // --- le lit
            GameObject bed = Anchor("Lit", root.transform, data.bed);
            Box(bed.transform, "Cadre", new Vector3(0f, 0.16f, 0f), new Vector3(1.45f, 0.32f, 2.05f), night.Wood, true);
            Box(bed.transform, "Matelas", new Vector3(0f, 0.4f, 0f), new Vector3(1.35f, 0.18f, 1.95f), house.Mattress, false);
            Box(bed.transform, "Couverture", new Vector3(0.03f, 0.5f, -0.3f), new Vector3(1.38f, 0.06f, 1.3f), house.Blanket, false)
                .transform.localRotation = Quaternion.Euler(0f, 3f, 0f);
            Box(bed.transform, "Oreiller", new Vector3(0f, 0.55f, 0.74f), new Vector3(0.62f, 0.13f, 0.34f), house.Mattress, false);
            Box(bed.transform, "Tete de lit", new Vector3(0f, 0.55f, 1.02f), new Vector3(1.45f, 0.8f, 0.06f), night.Wood, true);
            home.Bed = MakeInteractable(bed, "Dormir", "Sauvegarder, soigner les blessures", new Vector3(1.45f, 0.7f, 2.05f),
                new Vector3(0f, 0.35f, 0f), 2.4f);

            // --- le bureau et l'ordinateur
            GameObject desk = Anchor("Bureau", root.transform, data.desk);
            Box(desk.transform, "Plateau", new Vector3(0f, 0.74f, 0f), new Vector3(1.25f, 0.04f, 0.6f), house.Laminate, true);
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.58f : 0.58f;
                float z = i < 2 ? -0.26f : 0.26f;
                Box(desk.transform, "Pied", new Vector3(x, 0.36f, z), new Vector3(0.04f, 0.72f, 0.04f), night.DarkMetal, false);
            }

            // La chaise, tirée : on s'y assoit en face de l'écran.
            GameObject chair = EditorBuildUtility.CreateEmpty("Chaise", desk.transform, new Vector3(0.05f, 0f, -0.62f));
            chair.transform.localRotation = Quaternion.Euler(0f, 8f, 0f);
            Box(chair.transform, "Assise", new Vector3(0f, 0.46f, 0f), new Vector3(0.44f, 0.05f, 0.44f), night.Plastic, true);
            Box(chair.transform, "Dossier", new Vector3(0f, 0.76f, -0.2f), new Vector3(0.44f, 0.5f, 0.04f), night.Plastic, false);
            for (int i = 0; i < 4; i++)
            {
                Box(chair.transform, "Pied", new Vector3(i % 2 == 0 ? -0.19f : 0.19f, 0.22f, i < 2 ? -0.19f : 0.19f),
                    new Vector3(0.03f, 0.44f, 0.03f), night.DarkMetal, false);
            }

            // Le bureau est adossé au mur (son +z local) : l'écran regarde la chaise (-z).
            GameObject laptop = EditorBuildUtility.CreateEmpty("Ordinateur portable", desk.transform, new Vector3(0.05f, 0.76f, 0.02f));
            Box(laptop.transform, "Base", new Vector3(0f, 0.012f, 0f), new Vector3(0.36f, 0.024f, 0.25f), night.DarkMetal, false);
            GameObject lid = EditorBuildUtility.CreateEmpty("Couvercle", laptop.transform, new Vector3(0f, 0.024f, 0.12f));
            lid.transform.localRotation = Quaternion.Euler(-75f, 0f, 0f);
            Box(lid.transform, "Coque", new Vector3(0f, 0f, 0.12f), new Vector3(0.36f, 0.012f, 0.24f), night.DarkMetal, false);
            Material screen = NightMaterialFactory.CreateEmissive(NightMaterialFactory.MaterialsFolder, "M_EcranOrdinateur",
                new Color(0.05f, 0.08f, 0.12f), null, Vector2.one, new Color(0.35f, 0.62f, 1f) * 1.6f, null, 0.8f, 0f);
            Box(lid.transform, "Ecran", new Vector3(0f, 0.007f, 0.12f), new Vector3(0.33f, 0.002f, 0.21f), screen, false);
            NightStreetBuilder.AddLight(laptop.transform, "Lueur de l'ecran", new Vector3(0f, 0.25f, -0.25f),
                new Color(0.45f, 0.65f, 1f), 0.5f, 2.6f, false, false);
            home.Computer = MakeInteractable(laptop, "Allumer l'ordinateur", "Jeux en ligne, paris, boutique, immobilier",
                new Vector3(0.45f, 0.35f, 0.4f), new Vector3(0f, 0.12f, 0f), 2.2f);

            // La lampe de bureau : la chambre a une lumière même quand la ville dort.
            GameObject lamp = EditorBuildUtility.CreateEmpty("Lampe de bureau", desk.transform, new Vector3(0.5f, 0.76f, 0.16f));
            Cylinder(lamp.transform, "Pied", new Vector3(0f, 0.01f, 0f), new Vector3(0.14f, 0.01f, 0.14f), night.DarkMetal, false);
            Cylinder(lamp.transform, "Tige", new Vector3(0f, 0.22f, 0f), new Vector3(0.02f, 0.22f, 0.02f), night.Chrome, false);
            Cylinder(lamp.transform, "Abat-jour", new Vector3(0f, 0.44f, 0f), new Vector3(0.18f, 0.07f, 0.18f), night.NeonWarm, false);
            NightStreetBuilder.AddLight(lamp.transform, "Lumiere", new Vector3(0f, 0.38f, 0f), new Color(1f, 0.78f, 0.52f),
                1.25f, 6f, true, false);

            // --- le courrier, sur le bureau
            GameObject letters = EditorBuildUtility.CreateEmpty("Courrier", root.transform, MapPack.Position(data.letters));
            letters.transform.rotation = Quaternion.Euler(0f, MapPack.Yaw(data.letters), 0f);
            float[] yaws = { 4f, -13f, 9f };
            for (int i = 0; i < 3; i++)
            {
                GameObject envelope = Box(letters.transform, "Enveloppe", new Vector3(i * 0.045f, i * 0.006f, i * 0.02f),
                    new Vector3(0.23f, 0.004f, 0.115f), house.Paper, false);
                envelope.transform.localRotation = Quaternion.Euler(0f, yaws[i], 0f);
            }

            home.Letters = MakeInteractable(letters, "Lire le courrier", "Des relances, encore", new Vector3(0.44f, 0.12f, 0.24f),
                new Vector3(0.05f, 0.02f, 0.02f), 2.2f);

            // --- l'armoire et son miroir
            GameObject wardrobe = Anchor("Armoire", root.transform, data.wardrobe);
            Box(wardrobe.transform, "Caisson", new Vector3(0f, 1f, 0f), new Vector3(1.15f, 2f, 0.55f), night.Wood, true);
            Box(wardrobe.transform, "Miroir", new Vector3(0.28f, 1.1f, 0.281f), new Vector3(0.48f, 1.5f, 0.01f), night.Chrome, false);
            Box(wardrobe.transform, "Poignee", new Vector3(-0.05f, 1.05f, 0.29f), new Vector3(0.02f, 0.18f, 0.02f), night.Chrome, false);
            home.Wardrobe = MakeInteractable(wardrobe, "Se changer", "Tenue, couleurs, entraînement", new Vector3(1.2f, 2f, 0.7f),
                new Vector3(0f, 1f, 0.1f), 2.4f);

            return home;
        }

        private static GameObject Anchor(string name, Transform parent, float[] data)
        {
            GameObject go = EditorBuildUtility.CreateEmpty(name, parent, MapPack.Position(data));
            go.transform.rotation = Quaternion.Euler(0f, MapPack.Yaw(data), 0f);
            return go;
        }

        private static Interactable MakeInteractable(GameObject go, string label, string hint, Vector3 size, Vector3 center, float range)
        {
            BoxCollider trigger = go.AddComponent<BoxCollider>();
            trigger.isTrigger = true;
            trigger.size = size;
            trigger.center = center;

            Interactable interactable = go.AddComponent<Interactable>();
            SerializedWiring.SetString(interactable, "_label", label);
            SerializedWiring.SetString(interactable, "_hint", hint);
            SerializedWiring.SetFloat(interactable, "_range", range);
            return interactable;
        }

        private static GameObject Box(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider)
        {
            return EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, name, parent, position, size, material, collider);
        }

        private static GameObject Cylinder(Transform parent, string name, Vector3 position, Vector3 size, Material material, bool collider)
        {
            return EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, name, parent, position, size, material, collider);
        }

        // ------------------------------------------------------------------ le Vertigo

        private static void BuildVertigoFront(MapPack.Data map, NightMaterialFactory.Palette night, Transform parent,
            GameObject player, ClubInteriorBuilder.Result club, ScreenFader fader)
        {
            MapPack.Club data = map.vertigo;
            GameObject root = EditorBuildUtility.CreateEmpty("Vertigo (facade)", parent, Vector3.zero);

            // --- l'enseigne : lisible depuis la rue, c'est-à-dire tournée vers l'extérieur de la façade
            Vector3 signPosition = MapPack.Position(data.sign);
            float outward = MapPack.Yaw(data.sign);
            GameObject sign = EditorBuildUtility.CreateEmpty("Enseigne", root.transform, signPosition);
            sign.transform.rotation = Quaternion.Euler(0f, outward + 180f, 0f);

            float width = NeonTextBuilder.Build(sign.transform, "VERTIGO", 0.95f, 0.09f, night.NeonMagenta);
            Box(sign.transform, "Caisson", new Vector3(0f, 0.48f, 0.22f), new Vector3(width + 0.9f, 1.55f, 0.22f), night.DarkMetal, false);
            Light halo = NightStreetBuilder.AddLight(sign.transform, "Halo", new Vector3(0f, 0.5f, -0.6f),
                new Color(1f, 0.2f, 0.62f), 2.6f, 11f, true, false);
            NightStreetBuilder.MakeVolumetric(halo, 0.15f);
            NightStreetBuilder.AddFlicker(sign, NeonFlicker.Pattern.Calme, 7.5f, 0.07f, 1.1f, 23f);

            // --- le projecteur du videur, au-dessus de la porte
            Vector3 doorPosition = MapPack.Position(data.door);
            float doorYaw = MapPack.Yaw(data.door);
            Quaternion inward = Quaternion.Euler(0f, doorYaw, 0f);
            NightStreetBuilder.AddSpot(root.transform, "Projecteur de la porte", doorPosition + Vector3.up * 2.2f - inward * Vector3.forward * 0.6f,
                new Vector3(0f, -1f, 0f) - inward * Vector3.forward * 0.4f, new Color(1f, 0.86f, 0.66f), 2.4f, 9f, 80f, false);

            // --- la porte : E pour entrer
            GameObject door = EditorBuildUtility.CreateEmpty("Porte du Vertigo (entree)", root.transform, doorPosition);
            door.transform.rotation = inward;
            BoxCollider trigger = door.AddComponent<BoxCollider>();
            trigger.size = new Vector3(2.6f, 2.2f, 1.4f);
            trigger.isTrigger = true;

            Interactable entrance = door.AddComponent<Interactable>();
            SerializedWiring.SetString(entrance, "_label", "Entrer au Vertigo");
            SerializedWiring.SetString(entrance, "_hint", "La salle du fond, la fosse, le bar.");

            DoorPortal inPortal = door.AddComponent<DoorPortal>();
            SerializedWiring.SetObject(inPortal, "_player", player);
            SerializedWiring.SetObject(inPortal, "_destination", club.Arrival);
            SerializedWiring.SetObject(inPortal, "_fader", fader);
            SandboxSceneBuilder.SetObjectArray(inPortal, "_activate", club.Root.gameObject);

            // --- dedans, la sortie ramène sur le trottoir, face à la rue
            GameObject back = EditorBuildUtility.CreateEmpty("Retour sur le trottoir", root.transform, MapPack.Position(data.@out));
            back.transform.rotation = Quaternion.Euler(0f, MapPack.Yaw(data.@out), 0f);

            if (club.Exit != null)
            {
                club.Exit.Label = "Sortir dans la rue";
                DoorPortal outPortal = club.Exit.gameObject.AddComponent<DoorPortal>();
                SerializedWiring.SetObject(outPortal, "_player", player);
                SerializedWiring.SetObject(outPortal, "_destination", back.transform);
                SerializedWiring.SetObject(outPortal, "_fader", fader);
                SandboxSceneBuilder.SetObjectArray(outPortal, "_deactivate", club.Root.gameObject);
            }
        }

        // ------------------------------------------------------------------ carte

        private static CityMap BuildMapOnCity(MapPack.Data map, GameObject player, Camera camera, PlayerInputReader input,
            PhoneDevice phone)
        {
            GameObject go = new GameObject("=== Carte ===");
            CityMap cityMap = go.AddComponent<CityMap>();
            SerializedWiring.SetObject(cityMap, "_player", player.transform);
            SerializedWiring.SetObject(cityMap, "_camera", camera);
            SerializedWiring.SetObject(cityMap, "_input", input);
            SerializedWiring.SetObject(cityMap, "_phone", phone);
            SerializedWiring.SetFloat(cityMap, "_miniScale", 1.35f);

            Material glow = NightMaterialFactory.CreateGlow(NightMaterialFactory.MaterialsFolder, "M_ColonneGPS",
                new Color(1f, 0.82f, 0.25f, 0.22f), 1.2f, 2.2f, 0.9f, false);
            GameObject beam = EditorBuildUtility.CreateEmpty("Colonne GPS", go.transform, Vector3.zero);
            GameObject column = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Halo", beam.transform,
                new Vector3(0f, 45f, 0f), new Vector3(1.1f, 45f, 1.1f), glow, false);
            Renderer columnRenderer = column.GetComponent<Renderer>();
            if (columnRenderer != null) columnRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SerializedWiring.SetObject(cityMap, "_beam", beam);

            List<CityMap.Landmark> landmarks = new List<CityMap.Landmark>();
            if (map.landmarks != null)
            {
                for (int i = 0; i < map.landmarks.Length; i++)
                {
                    MapPack.Mark m = map.landmarks[i];
                    if (m == null || m.p == null || m.p.Length < 2) continue;

                    Color color = m.color != null && m.color.Length >= 3 ? new Color(m.color[0], m.color[1], m.color[2]) : Color.white;
                    landmarks.Add(new CityMap.Landmark { label = m.label, position = new Vector2(m.p[0], m.p[1]), color = color });
                }
            }

            cityMap.Configure(MapPack.Rect(map.play), new Rect[0], new Rect[0], new Rect[0], landmarks.ToArray());

            Texture2D top = AssetDatabase.LoadAssetAtPath<Texture2D>(MapPack.TopViewPath);
            if (top != null) cityMap.SetBackground(top, MapPack.Rect(map.bounds));
            EditorUtility.SetDirty(cityMap);
            return cityMap;
        }

        // ------------------------------------------------------------------ la nuit de la ville

        /// <summary>
        /// Les matériaux « éteints » de la ville (lampadaires, néons, enseignes, fenêtres) et
        /// leur version allumée : celle de la ville quand elle existe (« ... on »), sinon une
        /// copie émissive fabriquée ici.
        /// </summary>
        private static MapStreamer.MaterialSwap[] NightSwaps()
        {
            EditorBuildUtility.EnsureFolder(NightFolder);
            List<MapStreamer.MaterialSwap> swaps = new List<MapStreamer.MaterialSwap>();
            Regex offToken = new Regex(@"(^|[ _])off($|[ _])", RegexOptions.IgnoreCase);

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { MapPack.MaterialsFolder });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                string name = Path.GetFileNameWithoutExtension(path);
                string lower = name.ToLowerInvariant();

                bool lamp = lower.Contains("lightoff") || lower.Contains("light off");
                bool off = lamp || offToken.IsMatch(name);
                bool window = lower == "window_opaque_material";
                if (!off && !window) continue;

                Material source = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (source == null || source.shader == null || source.shader.name != "Standard") continue;

                Material on = null;
                if (off)
                {
                    string onName = lamp ? Regex.Replace(name, "off", "on", RegexOptions.IgnoreCase) : offToken.Replace(name, "$1on$2");
                    on = MapPack.FindMaterial(onName);
                    if (on == null) on = MapPack.FindMaterial(offToken.Replace(name, "$2").Trim());
                    if (on == source) on = null;
                }

                if (on == null)
                {
                    Color tint = window ? new Color(1f, 0.74f, 0.44f) * 1.6f : lamp ? new Color(1f, 0.86f, 0.62f) * 3.2f : Color.white * 2.4f;
                    on = EmissiveCopy(source, tint, !window);
                }

                swaps.Add(new MapStreamer.MaterialSwap { off = source, on = on, share = window ? 0.32f : 1f });
            }

            return swaps.ToArray();
        }

        private static Material EmissiveCopy(Material source, Color emission, bool useAlbedo)
        {
            string path = NightFolder + "/" + source.name + " (allume).mat";
            Material copy = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (copy == null)
            {
                copy = new Material(source);
                AssetDatabase.CreateAsset(copy, path);
            }
            else
            {
                copy.CopyPropertiesFromMaterial(source);
            }

            Texture albedo = source.HasProperty("_MainTex") ? source.GetTexture("_MainTex") : null;
            copy.EnableKeyword("_EMISSION");
            copy.SetColor("_EmissionColor", emission);
            if (useAlbedo && albedo != null) copy.SetTexture("_EmissionMap", albedo);
            copy.globalIlluminationFlags = MaterialGlobalIlluminationFlags.None;
            EditorUtility.SetDirty(copy);
            return copy;
        }
    }
}
