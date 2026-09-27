using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using UberBagarre.Combat;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
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

            // La ville, ouverte à côté le temps de la construction : on y lit ses véhicules et ses
            // magasins. D'abord préparée (portes et véhicules défigés, murs de la démo retirés).
            Scene city = EditorSceneManager.OpenScene(MapPack.ScenePath, OpenSceneMode.Additive);
            SceneManager.SetActiveScene(scene);
            CityPreparation.Prepare(false);

            Light sun;
            Light moon;
            SandboxSceneBuilder.BuildLighting(out sun, out moon);

            // --- ce qui est à nous, posé dans la ville
            GameObject world = new GameObject("=== Monde (sur la ville) ===");

            // Le cycle jour / nuit allume et éteint nos enseignes ; la ville a ses propres
            // lampes, que MapStreamer allume.
            NightStreetBuilder.Result street = new NightStreetBuilder.Result { Root = world.transform, CityRoot = world.transform };

            List<HomeKit> homes = BuildHomes(map, night, world.transform);
            HomeKit home = homes[0];
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
            TuneCityRendering(graphics, sun);

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

            // Entraînement (voies de progression) et tenue (coupes, couleurs).
            WirePlayerProgression(player, materials, progress);

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
            FightCrowd crowd = SandboxSceneBuilder.BuildFightCrowd(world.transform, night, materials, 14, 71, "Badauds (monde ouvert)");

            List<Vector3[]> walkLoops = MapPack.Loops(map.walk);
            List<Vector3[]> driveLoops = MapPack.Loops(map.drive);
            GameObject walkers = BuildPedestrians(walkLoops, night, materials, 9, 11);
            // --- les voitures : celles de la ville (compacte, berline, SUV, pick-up, coupé),
            // devenues conduisibles. La compacte devant le motel est à toi ; les véhicules garés
            // de la ville sont remplacés par des copies qu'on peut prendre ; la circulation roule
            // pour de vrai (volant, pédales), un conducteur assis au volant.
            GameObject garage = new GameObject("=== Modeles de voitures ===");
            CityVehicles vehicles = CityVehicles.Load(garage.transform, night);
            GameObject traffic = BuildCityTraffic(vehicles, driveLoops, new[] { 7, 5, 5 });
            GameObject parked = new GameObject("=== Voitures garees ===");
            VehicleCatalog catalog = BuildParkedCars(vehicles, map, parked, progress);
            // --- le directeur des courses
            OpenWorldDirector director = systems.AddComponent<OpenWorldDirector>();
            SerializedWiring.SetObject(director, "_phone", phone);
            SerializedWiring.SetObject(director, "_display", display);
            SerializedWiring.SetObject(director, "_input", input);
            SerializedWiring.SetObject(director, "_player", player.GetComponent<Combatant>());
            SerializedWiring.SetObject(director, "_progress", progress);
            SerializedWiring.SetObject(director, "_briefing", briefing);
            SerializedWiring.SetObject(director, "_intro", player.GetComponent<FightIntro>());
            SerializedWiring.SetObject(director, "_look", player.GetComponent<PlayerLook>());
            SerializedWiring.SetObject(director, "_crowd", crowd);
            SerializedWiring.SetObject(director, "_map", cityMap);
            SerializedWiring.SetObject(director, "_fader", fader);
            SerializedWiring.SetObject(director, "_subtitles", subtitles);
            SerializedWiring.SetObject(director, "_home", home.Arrival);
            SerializedWiring.SetObject(director, "_targetsRoot", world.transform);
            SerializedWiring.SetObject(director, "_kit", BuildActivityKit(systems));
            SerializedWiring.SetFloat(director, "_minimumDistance", 60f);
            director.Configure(Spots(map), profiles.ToArray());
            EditorUtility.SetDirty(director);

            // --- les logements : vestiaire et ordinateur dans chacun, lit = dormir
            WardrobeScreen wardrobeScreen = BuildWardrobeScreen(systems, player, progress);
            ComputerScreen computerScreen = BuildComputerScreen(systems, player, progress);
            HomeRegistry registry = BuildHomeRegistry(systems, homes, progress, director, wardrobeScreen, computerScreen);
            SerializedWiring.SetObject(computerScreen, "_homes", registry);

            // --- les magasins : chacun son vendeur, son comptoir, son usage
            ShopDirectory shops = BuildShops(systems, player, progress, night, materials, subtitles, fader, catalog, registry,
                computerScreen, club, cityMap);

            // --- l'histoire : ses personnages, ses lieux, ses chapitres
            OpenWorldStory.Character[] characters = BuildStoryCharacters(map, materials, attacks, night, club);
            OpenWorldStory story = BuildStory(systems, characters, map, club, director, progress, subtitles, fader, phone, cityMap,
                registry, computerScreen);

            // --- les lumières loin du joueur s'éteignent ; la ville y ajoute les siennes au chargement
            DistanceCuller culler = systems.AddComponent<DistanceCuller>();
            SerializedWiring.SetObject(culler, "_viewer", gameCamera.transform);
            SerializedWiring.SetFloat(culler, "_lightRadius", 70f);
            SandboxSceneBuilder.SetComponentArray(culler, "_lightRoots", world.transform, traffic.transform, parked.transform);

            // --- l'heure de la ville : le jour se lève, le soleil tourne, la nuit tombe
            WorldClock clock = systems.AddComponent<WorldClock>();
            clock.Configure(graphics, graphics.GetComponent<TimeOfDay>(), director, 8f);
            EditorUtility.SetDirty(clock);

            // --- la ville, chargée par-dessus au lancement
            MapStreamer streamer = systems.AddComponent<MapStreamer>();
            SerializedWiring.SetObject(streamer, "_player", player.transform);
            SerializedWiring.SetObject(streamer, "_controller", player.GetComponent<CharacterController>());
            SerializedWiring.SetObject(streamer, "_fader", fader);
            SerializedWiring.SetObject(streamer, "_subtitles", subtitles);
            SerializedWiring.SetObject(streamer, "_culler", culler);
            SandboxSceneBuilder.SetComponentArray(streamer, "_holdUntilLoaded", player.GetComponent<PlayerMotor>());
            streamer.Configure(MapPack.SceneName, map.water, SafePoints(walkLoops), CityDoors(map, homes), NightSwaps());
            if (vehicles.Available) streamer.HideInCity(vehicles.HiddenInCity);
            EditorUtility.SetDirty(streamer);

            // --- menu du jeu, menu de triche
            BuildTestTools(player, graphics, progress, subtitles);
            GameMenu menu = SandboxSceneBuilder.BuildGameMenu(player, graphics, gameCamera, observerCamera);
            WireTitle(menu, story, BuildTitleTour(map, driveLoops, world.transform), fader);

            club.Root.gameObject.SetActive(false);

            // La ville a fini de servir : on ne sauvegarde que la scène du jeu.
            if (city.IsValid() && city.isLoaded) EditorSceneManager.CloseScene(city, true);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved)
            {
                SandboxSceneBuilder.RegisterSceneInBuildSettings(ScenePath);
                MakeLaunchScene(ScenePath);
                AppendSceneToBuildSettings(MapPack.ScenePath);
            }

            SandboxSceneBuilder.OfferLinearColorSpace();
            Selection.activeGameObject = player;

            // La ville, visible dans la vue Scene (elle se charge de toute façon en Play).
            EditorApplication.delayCall += MapSceneLoader.OpenCity;

            Debug.Log("[UberBagarre] Monde ouvert genere SUR LA VILLE (Assets/Schedule1) : " + ScenePath + "\n" +
                      "  Logements     : " + homes.Count + " (le motel au départ ; bungalow et manoir à l'achat, sur l'ordinateur)\n" +
                      "  Le Vertigo    : la porte du club desaffecte, ville nord (E pour entrer)\n" +
                      "  L'histoire    : prologue + 4 chapitres (Moretti, les Kovac, le Taureau, le Comptable, Sami)\n" +
                      "  Ecran titre   : survol de la ville, CONTINUER / NOUVELLE PARTIE / CHAPITRES\n" +
                      "  Les courses   : " + map.spots.Length + " coins de la ville, chacun avec ce que la cible y fait\n" +
                      "  Passants      : " + walkers.transform.childCount + ", voitures : " + traffic.transform.childCount +
                      ", garees : " + parked.transform.childCount + "\n" +
                      "  Magasins      : " + shops.Shops.Count + " (chacun son vendeur et son usage)\n" +
                      "  La ville est ouverte par-dessus dans l'editeur, et se charge seule en Play.\n" +
                      "  Appuie sur Play.");
        }

        // ------------------------------------------------------------------ rendu de la ville

        /// <summary>
        /// Le rendu de la ville en plein jour, au plus près de Schedule I : couleurs franches et
        /// un peu saturées, contraste doux, presque pas de vignette, bloom discret ; un soleil
        /// chaud aux ombres douces, une ambiante claire, une brume légère et bleutée au loin.
        /// La nuit garde ses lampadaires, mais sans la brume épaisse des ruelles du prologue.
        /// </summary>
        private static void TuneCityRendering(GraphicsDirector graphics, Light sun)
        {
            SerializedWiring.SetFloat(graphics, "_bloom", 0.6f);
            SerializedWiring.SetFloat(graphics, "_threshold", 1.2f);
            SerializedWiring.SetFloat(graphics, "_exposure", 1.05f);
            SerializedWiring.SetFloat(graphics, "_saturation", 1.14f);
            SerializedWiring.SetFloat(graphics, "_contrast", 1.0f);
            SerializedWiring.SetFloat(graphics, "_vignette", 0.16f);
            SerializedWiring.SetFloat(graphics, "_volumetric", 0.55f);
            SerializedWiring.SetBool(graphics, "_restoreDay", false);
            SerializedWiring.SetFloat(graphics, "_day", 1f);

            TimeOfDay time = graphics.GetComponent<TimeOfDay>();
            if (time != null)
            {
                SerializedWiring.SetFloat(time, "_sunIntensity", 1.25f);
                SerializedWiring.SetColor(time, "_sunColor", new Color(1f, 0.94f, 0.84f));
                SerializedWiring.SetFloat(time, "_ambientDay", 1.2f);
                SerializedWiring.SetColor(time, "_fogDay", new Color(0.72f, 0.8f, 0.9f));
                SerializedWiring.SetFloat(time, "_fogDensityDay", 0.0035f);
                SerializedWiring.SetFloat(time, "_fogDensityNight", 0.009f);
                SerializedWiring.SetFloat(time, "_ambientNight", 0.34f);
                SerializedWiring.SetColor(time, "_skyTintDay", new Color(0.5f, 0.66f, 0.92f));
                SerializedWiring.SetFloat(time, "_skyExposureDay", 1.3f);
                EditorUtility.SetDirty(time);
            }

            if (sun != null)
            {
                sun.shadowStrength = 0.65f;
                sun.shadows = LightShadows.Soft;
            }

            UberBagarre.Feedback.VisualQuality quality = graphics.GetComponent<UberBagarre.Feedback.VisualQuality>();
            if (quality != null)
            {
                // Deux cascades et une résolution haute (pas « très haute ») : la moitié du coût
                // des ombres, pour une différence qu'on ne voit qu'au pied des murs lointains.
                SerializedWiring.SetFloat(quality, "_shadowDistance", 85f);
                SerializedWiring.SetInt(quality, "_shadowCascades", 2);
                SerializedWiring.SetEnum(quality, "_shadowResolution", (int)UnityEngine.ShadowResolution.High);
            }
            EditorUtility.SetDirty(graphics);
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

        /// <summary>
        /// L'histoire se joue désormais dans la ville : c'est cette scène que le jeu lance (la
        /// première des Build Settings), avec son écran titre.
        /// </summary>
        private static void MakeLaunchScene(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);
            int index = scenes.FindIndex(s => s.path == scenePath);
            if (index <= 0) return;

            EditorBuildSettingsScene scene = scenes[index];
            scenes.RemoveAt(index);
            scenes.Insert(0, scene);
            EditorBuildSettings.scenes = scenes.ToArray();
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
