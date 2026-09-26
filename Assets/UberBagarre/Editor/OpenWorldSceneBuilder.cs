using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Sandbox;
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
    /// La scène du monde ouvert : la ville entière, d'un seul tenant, et les courses qui tombent
    /// sur le téléphone.
    ///
    /// Tout ce qui existe déjà est réutilisé tel quel, posé à sa place dans la ville : la rue du
    /// Vertigo (ouverte aux deux bouts), la planque dans son lotissement, le parking derrière le
    /// club, la salle du Vertigo (derrière sa porte). Autour : les rues, les immeubles, les
    /// passants, la circulation (CityBuilder). Le joueur, son téléphone, ses coups, le menu, le
    /// menu de triche : les mêmes que dans l'histoire.
    /// </summary>
    public static class OpenWorldSceneBuilder
    {
        public const string ScenePath = SandboxSceneBuilder.ScenesFolder + "/MondeOuvert.unity";

        /// <summary>La salle du Vertigo vit à l'écart, comme dans l'histoire.</summary>
        public static readonly Vector3 ClubInteriorOrigin = new Vector3(600f, 0f, 0f);

        private sealed class Target
        {
            public string Name;
            public string Age;
            public string Clothing;
            public string Record;
            public int Stars;
            public string Silhouette;
            public CorpsImporter.Top Top;
            public Color Shirt;
            public Color Pants;
            public float Health;
        }

        private static readonly Target[] Targets =
        {
            new Target { Name = "RAYAN", Age = "24 ans", Clothing = "Survêtement noir", Stars = 1, Silhouette = "Sec",
                Top = CorpsImporter.Top.Veste, Shirt = new Color(0.05f, 0.05f, 0.06f), Pants = new Color(0.2f, 0.2f, 0.22f), Health = 80f,
                Record = "Deale au coin de la rue du Nord.\nDoit de l'argent à la moitié du quartier." },
            new Target { Name = "KEVIN DUBOIS", Age = "29 ans", Clothing = "T-shirt gris, jean", Stars = 1, Silhouette = "Athlete",
                Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.36f, 0.36f, 0.38f), Pants = new Color(0.10f, 0.16f, 0.30f), Health = 85f,
                Record = "Harcèle son ex depuis des mois.\nLa cliente veut qu'il comprenne." },
            new Target { Name = "MARCO TESTA", Age = "41 ans", Clothing = "Veste bordeaux", Stars = 2, Silhouette = "Costaud",
                Top = CorpsImporter.Top.Veste, Shirt = new Color(0.30f, 0.04f, 0.07f), Pants = new Color(0.08f, 0.08f, 0.09f), Health = 120f,
                Record = "Ancien videur du Vertigo, viré pour avoir\ncogné un client. Rancunier." },
            new Target { Name = "SACHA", Age = "22 ans", Clothing = "T-shirt rouge", Stars = 2, Silhouette = "Sec",
                Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.62f, 0.07f, 0.06f), Pants = new Color(0.12f, 0.18f, 0.32f), Health = 105f,
                Record = "Rackette les livreurs à la sortie du métro.\nToujours pressé, jamais seul longtemps." },
            new Target { Name = "LE GITAN", Age = "35 ans", Clothing = "Débardeur blanc", Stars = 3, Silhouette = "Athlete",
                Top = CorpsImporter.Top.Debardeur, Shirt = new Color(0.82f, 0.82f, 0.80f), Pants = new Color(0.07f, 0.07f, 0.08f), Health = 150f,
                Record = "Ancien boxeur amateur.\nFrappe encore comme un pro." },
            new Target { Name = "DIMITRI", Age = "38 ans", Clothing = "Veste verte militaire", Stars = 3, Silhouette = "Colosse",
                Top = CorpsImporter.Top.Veste, Shirt = new Color(0.20f, 0.24f, 0.13f), Pants = new Color(0.05f, 0.05f, 0.05f), Health = 180f,
                Record = "Recouvreur de dettes.\nIl en a envoyé plus d'un à l'hôpital." }
        };

        [MenuItem("Uber Bagarre/3b - Construire le MONDE OUVERT (la ville)", false, 35)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool exists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath));
            if (exists && !EditorUtility.DisplayDialog("Reconstruire le monde ouvert ?",
                    "La scene existe deja :\n" + ScenePath + "\n\nElle va etre REMPLACEE.", "Reconstruire", "Annuler"))
            {
                return;
            }

            Build();
        }

        public static void Build()
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

            // --- les lieux, à leur place dans la ville
            GameObject world = new GameObject("=== Monde ===");

            NightStreetBuilder.Result street = NightStreetBuilder.Build(night, true);
            street.Root.SetParent(world.transform, true);

            HouseBuilder.Result house = HouseBuilder.Build(night, CityBuilder.HouseOrigin);
            house.Root.SetParent(world.transform, true);
            OpenHouseStreet(house.Root);

            // Le courrier et la voiture de l'allée appartiennent à l'histoire : ici, ils ne
            // proposent rien (une action affichée qui ne fait rien est pire que pas d'action).
            DisableInteraction(house.Letters);
            DisableInteraction(house.Car);

            ParkingBuilder.Result parking = ParkingBuilder.Build(night, CityBuilder.ParkingOrigin);
            parking.Root.rotation = Quaternion.Euler(0f, CityBuilder.ParkingYaw, 0f);
            parking.Root.SetParent(world.transform, true);
            DisableInteraction(parking.Car);

            CityBuilder.Result city = CityBuilder.Build(night);
            city.Root.SetParent(world.transform, true);

            // La vieille caisse de l'allée : ici, on la conduit.
            BuildPlayerCar(house, city, world.transform);

            ClubInteriorBuilder.Result club = ClubInteriorBuilder.Build(night, materials, ClubInteriorOrigin);

            // Le cycle jour / nuit éteint TOUTES les enseignes de la ville, pas seulement celles
            // de la rue.
            street.CityRoot = world.transform;

            // --- le joueur
            Camera gameCamera;
            Camera observerCamera;
            GameObject player = SandboxSceneBuilder.BuildPlayer(materials, attacks, out gameCamera, out observerCamera);
            player.transform.SetPositionAndRotation(house.Arrival.position, house.Arrival.rotation);

            // Une ville de 400 m : on doit voir l'autre bout du boulevard.
            gameCamera.farClipPlane = 700f;
            observerCamera.farClipPlane = 800f;

            GraphicsDirector graphics = SandboxSceneBuilder.BuildRendering(sun, moon, street, gameCamera, observerCamera);
            SandboxSceneBuilder.SetComponentArray(graphics, "_reflections",
                street.Ground != null ? street.Ground.GetComponent<PlanarReflection>() : null,
                parking.Ground != null ? parking.Ground.GetComponent<PlanarReflection>() : null);

            // --- téléphone, histoire minimale (sous-titres, fondu, progression), interactions
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

            // --- la salle du Vertigo, derrière sa porte
            BuildClubDoors(player, street, club, fader);

            // --- carte, GPS
            CityMap map = BuildMap(city, player, gameCamera, input, phone, night);

            // --- cibles, badauds, passants, circulation
            List<OpenWorldDirector.Profile> profiles = BuildTargets(materials, attacks, night);
            FightCrowd crowd = SandboxSceneBuilder.BuildFightCrowd(world.transform, night, materials, 8, 71, "Badauds (monde ouvert)");
            GameObject walkers = BuildPedestrians(city, night, materials);
            GameObject traffic = BuildTraffic(city, night);

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
            SerializedWiring.SetObject(director, "_map", map);
            SerializedWiring.SetObject(director, "_fader", fader);
            SerializedWiring.SetObject(director, "_subtitles", subtitles);
            SerializedWiring.SetObject(director, "_home", house.Arrival);
            SerializedWiring.SetObject(director, "_targetsRoot", world.transform);
            director.Configure(city.Spots.ToArray(), profiles.ToArray());
            EditorUtility.SetDirty(director);
            SerializedWiring.Verify(director, "_phone");

            // --- les lumières loin du joueur s'éteignent (leur tête reste allumée)
            DistanceCuller culler = systems.AddComponent<DistanceCuller>();
            SerializedWiring.SetObject(culler, "_viewer", gameCamera.transform);
            SandboxSceneBuilder.SetComponentArray(culler, "_lightRoots",
                city.LightsRoot, street.Root, house.Root, parking.Root, traffic.transform);

            // --- menu du jeu, menu de triche
            BuildTestTools(player, graphics, progress, subtitles);
            SandboxSceneBuilder.BuildGameMenu(player, graphics, gameCamera, observerCamera);

            club.Root.gameObject.SetActive(false);
            if (city.Cars != null) city.Cars.Dispose();

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved) SandboxSceneBuilder.RegisterSceneInBuildSettings(ScenePath);

            SandboxSceneBuilder.OfferLinearColorSpace();
            Selection.activeGameObject = player;

            Debug.Log("[UberBagarre] Monde ouvert genere : " + ScenePath + "\n" +
                      "  La ville      : le boulevard du Vertigo, rues Nord et Sud, quatre avenues, " + city.Spots.Count + " lieux de rendez-vous\n" +
                      "  Les lieux     : la planque (depart), le Vertigo (E a la porte pour entrer), le parking, le square\n" +
                      "  Les courses   : une commande tombe sur le telephone (T), E pour l'accepter, suivre le GPS,\n" +
                      "                  la cible se retourne a quelques metres, K.O., photo (appli Photo), paiement\n" +
                      "  La carte      : mini-carte en haut a droite, M = grande carte\n" +
                      "  K.O.          : reveil a la planque, l'hopital prend sa part\n" +
                      "  Passants      : " + walkers.transform.childCount + ", voitures : " + traffic.transform.childCount + "\n" +
                      "  Appuie sur Play (ou lance-la depuis le menu principal : MONDE OUVERT).");
        }

        // ------------------------------------------------------------------ lieux

        /// <summary>
        /// La planque a été construite comme un décor fermé : sa rue a des murs invisibles aux
        /// deux bouts et en face. Dans la ville, cette rue est une vraie rue de lotissement.
        /// </summary>
        private static void OpenHouseStreet(Transform house)
        {
            Transform[] all = house.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] != null && all[i].name.StartsWith("Limite")) Object.DestroyImmediate(all[i].gameObject);
            }
        }

        /// <summary>
        /// La voiture de la planque devient conduisible : le modèle figé de l'allée est remplacé,
        /// à la même place, par la même caisse rouillée — avec des roues qui tournent.
        /// </summary>
        private static void BuildPlayerCar(HouseBuilder.Result house, CityBuilder.Result city, Transform parent)
        {
            if (house.Car == null || city.Cars == null) return;

            Transform yard = house.Car.transform.parent;
            Transform still = yard != null ? yard.Find("Voiture rouillee") : null;
            if (still == null) return;

            // Le modèle de l'allée est construit le long de +X ; la voiture conduisible, le long de +Z.
            Vector3 position = still.position;
            float yaw = still.eulerAngles.y + 90f;
            still.gameObject.SetActive(false);
            house.Car.gameObject.SetActive(false);

            city.Cars.Spawn(parent, CityBuilder.CarFactory.Rusty, position, yaw, "Ta caisse");
        }

        /// <summary>Au volant : ce qui s'arrête (marcher, viser, frapper) et les deux caméras.</summary>
        private static void WireDriving(GameObject player, InteractionSystem interaction, PhoneDevice phone, Camera gameCamera,
            Camera chaseCamera)
        {
            PlayerDriving driving = player.AddComponent<PlayerDriving>();
            Combatant combatant = player.GetComponent<Combatant>();

            SerializedWiring.SetObject(driving, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(driving, "_controller", player.GetComponent<CharacterController>());
            SerializedWiring.SetObject(driving, "_combatant", combatant);
            SerializedWiring.SetObject(driving, "_knockdown", player.GetComponentInChildren<KnockdownSystem>(true));
            SerializedWiring.SetObject(driving, "_visuals", player.transform);
            SerializedWiring.SetObject(driving, "_interaction", interaction);
            SerializedWiring.SetObject(driving, "_phone", phone);
            SerializedWiring.SetObject(driving, "_gameCamera", gameCamera);
            SerializedWiring.SetObject(driving, "_chaseCamera", chaseCamera);

            List<Component> paused = new List<Component>();
            AddIfAny(paused, player.GetComponent<PlayerMotor>());
            AddIfAny(paused, player.GetComponent<PlayerLook>());
            AddIfAny(paused, player.GetComponent<PlayerCombat>());
            AddIfAny(paused, player.GetComponent<DodgeSystem>());
            AddIfAny(paused, player.GetComponent<CharacterPusher>());
            AddIfAny(paused, player.GetComponent<PropHandler>());
            AddIfAny(paused, player.GetComponent<ObserverCamera>());
            AddIfAny(paused, player.GetComponentInChildren<HeadBob>(true));
            SandboxSceneBuilder.SetComponentArray(driving, "_pauseWhileDriving", paused.ToArray());
            EditorUtility.SetDirty(driving);
        }

        private static void AddIfAny(List<Component> list, Component component)
        {
            if (component != null) list.Add(component);
        }

        private static void DisableInteraction(Interactable interactable)
        {
            if (interactable != null) SerializedWiring.SetBool(interactable, "_enabledForPlayer", false);
        }

        private static void BuildClubDoors(GameObject player, NightStreetBuilder.Result street, ClubInteriorBuilder.Result club,
            ScreenFader fader)
        {
            // Dehors : la porte du club. E pour entrer.
            GameObject door = EditorBuildUtility.CreateEmpty("Porte du Vertigo (entree)", street.Root,
                new Vector3(0f, 1.1f, NightStreetBuilder.ClubFront - 0.7f));
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

            // Dedans : la sortie ramène sur le trottoir, face à la rue.
            GameObject back = EditorBuildUtility.CreateEmpty("Retour sur le trottoir", street.Root,
                new Vector3(0f, NightStreetBuilder.SidewalkHeight, NightStreetBuilder.ClubFront - 2.6f));
            back.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

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

        // ------------------------------------------------------------------ HUD, triche

        private static void WireHud(GameObject player, PlayerProgress progress)
        {
            Combatant combatant = player.GetComponent<Combatant>();

            CombatHud hud = player.AddComponent<CombatHud>();
            SerializedWiring.SetObject(hud, "_presence", player.GetComponent<CombatPresence>());
            SerializedWiring.SetObject(hud, "_playerHealth", combatant.Health);
            SerializedWiring.SetObject(hud, "_playerStamina", combatant.Stamina);
            SerializedWiring.SetObject(hud, "_player", combatant);
            SerializedWiring.SetObject(hud, "_guard", player.GetComponent<GuardSystem>());
            SerializedWiring.SetObject(hud, "_relay", player.GetComponent<Feedback.CombatFeedbackRelay>());
            SerializedWiring.SetObject(hud, "_combat", player.GetComponent<PlayerCombat>());
            SerializedWiring.SetObject(hud, "_stun", player.GetComponent<StunMeter>());
            SerializedWiring.SetObject(hud, "_executor", player.GetComponent<AttackExecutor>());
            SerializedWiring.SetObject(hud, "_progress", progress);
        }

        private static void BuildTestTools(GameObject player, GraphicsDirector graphics, PlayerProgress progress,
            SubtitleDisplay subtitles)
        {
            GameObject root = new GameObject("=== Outils de test ===");
            Combatant combatant = player.GetComponent<Combatant>();

            CombatStatistics statistics = root.AddComponent<CombatStatistics>();
            SerializedWiring.SetObject(statistics, "_player", combatant);
            SerializedWiring.SetObject(statistics, "_executor", player.GetComponent<AttackExecutor>());
            SerializedWiring.SetObject(statistics, "_guard", player.GetComponent<GuardSystem>());
            SerializedWiring.SetObject(statistics, "_combo", player.GetComponent<ComboTracker>());

            DebugCheats cheats = root.AddComponent<DebugCheats>();
            SerializedWiring.SetObject(cheats, "_player", combatant);
            SerializedWiring.SetObject(cheats, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(cheats, "_motor", player.GetComponent<PlayerMotor>());
            SerializedWiring.SetObject(cheats, "_camera", player.GetComponentInChildren<Camera>());
            SerializedWiring.SetObject(cheats, "_subtitles", subtitles);
            SerializedWiring.SetObject(cheats, "_progress", progress);

            SandboxMenu menu = root.AddComponent<SandboxMenu>();
            SerializedWiring.SetObject(menu, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(menu, "_player", combatant);
            SerializedWiring.SetObject(menu, "_cursor", player.GetComponent<CursorLockController>());
            SerializedWiring.SetObject(menu, "_playerCombat", player.GetComponent<PlayerCombat>());
            SerializedWiring.SetObject(menu, "_hitStop", player.GetComponent<Feedback.HitStop>());
            SerializedWiring.SetObject(menu, "_statistics", statistics);
            SerializedWiring.SetObject(menu, "_graphics", graphics);
            SerializedWiring.SetObject(menu, "_cheats", cheats);
            SerializedWiring.SetBool(menu, "_storyMode", true);
        }

        // ------------------------------------------------------------------ carte

        private static CityMap BuildMap(CityBuilder.Result city, GameObject player, Camera camera, PlayerInputReader input,
            PhoneDevice phone, NightMaterialFactory.Palette night)
        {
            GameObject go = new GameObject("=== Carte ===");
            CityMap map = go.AddComponent<CityMap>();
            SerializedWiring.SetObject(map, "_player", player.transform);
            SerializedWiring.SetObject(map, "_camera", camera);
            SerializedWiring.SetObject(map, "_input", input);
            SerializedWiring.SetObject(map, "_phone", phone);

            // La colonne du GPS : un halo vertical, visible par-dessus les toits.
            Material glow = NightMaterialFactory.CreateGlow(NightMaterialFactory.MaterialsFolder, "M_ColonneGPS",
                new Color(1f, 0.82f, 0.25f, 0.22f), 1.2f, 2.2f, 0.9f, false);
            GameObject beam = EditorBuildUtility.CreateEmpty("Colonne GPS", go.transform, Vector3.zero);
            GameObject column = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Halo", beam.transform,
                new Vector3(0f, 45f, 0f), new Vector3(1.1f, 45f, 1.1f), glow != null ? glow : night.NeonWarm, false);
            Renderer columnRenderer = column.GetComponent<Renderer>();
            if (columnRenderer != null) columnRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            SerializedWiring.SetObject(map, "_beam", beam);

            map.Configure(CityBuilder.Bounds, city.MapRoads.ToArray(), city.MapBlocks.ToArray(), city.MapParks.ToArray(),
                city.Landmarks.ToArray());
            EditorUtility.SetDirty(map);
            return map;
        }

        // ------------------------------------------------------------------ cibles

        private static List<OpenWorldDirector.Profile> BuildTargets(BuildMaterials materials, AttackLibraryBuilder.Library attacks,
            NightMaterialFactory.Palette night)
        {
            GameObject root = new GameObject("=== Modeles de cibles ===");
            root.transform.position = new Vector3(0f, 0f, -420f);

            List<OpenWorldDirector.Profile> profiles = new List<OpenWorldDirector.Profile>();

            for (int i = 0; i < Targets.Length; i++)
            {
                Target t = Targets[i];

                FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);
                skin.Silhouette = t.Silhouette;
                skin.Top = t.Top;
                skin.Shirt = PrologueSceneBuilder.Jacket(night, "M_Cible_Haut_" + i, t.Shirt);
                skin.Pants = PrologueSceneBuilder.Jacket(night, "M_Cible_Bas_" + i, t.Pants);

                SandboxSceneBuilder.FighterParts parts = SandboxSceneBuilder.BuildFighter(materials, attacks, t.Name,
                    root.transform.position + new Vector3(i * 3f, 0f, 0f), 0f, skin, t.Health, true);

                parts.Go.transform.SetParent(root.transform, true);
                if (parts.Brain != null) parts.Brain.enabled = false;
                parts.Go.SetActive(false);

                profiles.Add(new OpenWorldDirector.Profile
                {
                    name = t.Name, age = t.Age, clothing = t.Clothing, record = t.Record, stars = t.Stars, template = parts.Go
                });
            }

            return profiles;
        }

        // ------------------------------------------------------------------ passants

        private static GameObject BuildPedestrians(CityBuilder.Result city, NightMaterialFactory.Palette night,
            BuildMaterials materials)
        {
            GameObject root = new GameObject("=== Passants ===");
            MocapLibrary library = MocapLibraryBuilder.Build();

            Color[] tops =
            {
                new Color(0.12f, 0.14f, 0.20f), new Color(0.35f, 0.30f, 0.24f), new Color(0.08f, 0.08f, 0.08f),
                new Color(0.40f, 0.10f, 0.10f), new Color(0.18f, 0.26f, 0.18f), new Color(0.50f, 0.48f, 0.44f)
            };
            Color[] bottoms = { new Color(0.10f, 0.14f, 0.26f), new Color(0.06f, 0.06f, 0.07f), new Color(0.26f, 0.24f, 0.20f) };
            string[] silhouettes = { "Sec", "Athlete", "Costaud", "Sec", "Colosse" };
            CorpsImporter.Top[] cuts = { CorpsImporter.Top.Veste, CorpsImporter.Top.TShirt, CorpsImporter.Top.Veste, CorpsImporter.Top.Debardeur };

            int n = 0;
            for (int l = 0; l < city.WalkLoops.Count; l++)
            {
                Vector3[] loop = city.WalkLoops[l];
                int count = l == city.WalkLoops.Count - 1 ? 4 : 3;

                for (int k = 0; k < count; k++, n++)
                {
                    int start = (k * loop.Length / count + l) % loop.Length;

                    GameObject go = EditorBuildUtility.CreateEmpty("Passant " + (n + 1), root.transform, loop[start]);
                    CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
                    capsule.height = 1.75f;
                    capsule.radius = 0.28f;
                    capsule.center = new Vector3(0f, 0.875f, 0f);

                    FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);
                    skin.Silhouette = silhouettes[n % silhouettes.Length];
                    skin.Top = cuts[(n * 3 + 1) % cuts.Length];
                    skin.Shirt = PrologueSceneBuilder.Jacket(night, "M_Passant_Haut_" + (n % tops.Length), tops[n % tops.Length]);
                    skin.Pants = PrologueSceneBuilder.Jacket(night, "M_Passant_Bas_" + (n % bottoms.Length), bottoms[n % bottoms.Length]);
                    skin.Crowd = true;

                    FighterBuilder.Result body = FighterBuilder.BuildBody(go.transform, go.transform, skin, true, Faction.Neutral, go);

                    Hitbox[] hitboxes = go.GetComponentsInChildren<Hitbox>(true);
                    for (int h = 0; h < hitboxes.Length; h++) Object.DestroyImmediate(hitboxes[h]);

                    Animator animator = null;
                    Avatar avatar = library != null ? CorpsAvatarBuilder.For(body.Data, body.Body.name) : null;
                    if (avatar != null)
                    {
                        animator = body.Body.AddComponent<Animator>();
                        animator.avatar = avatar;
                        animator.applyRootMotion = false;
                        animator.enabled = false;
                    }

                    MocapWalker walker = go.AddComponent<MocapWalker>();
                    walker.SetPath(loop, start, 1.05f + (n % 5) * 0.09f);
                    SerializedWiring.SetObject(walker, "_library", library);
                    SerializedWiring.SetObject(walker, "_animator", animator);
                    SerializedWiring.SetObject(walker, "_rig", body.Rig);
                    SerializedWiring.SetObject(walker, "_locomotion", body.Locomotion);
                    EditorUtility.SetDirty(walker);
                }
            }

            return root;
        }

        // ------------------------------------------------------------------ circulation

        private static GameObject BuildTraffic(CityBuilder.Result city, NightMaterialFactory.Palette night)
        {
            GameObject root = new GameObject("=== Circulation ===");
            GameObject template = CityBuilder.CarTemplate(root.transform, night, false, "VoitureCirculation");

            // Avec les feux, des files se forment aux carrefours : un peu plus de monde sur les boucles.
            int[] perLoop = { 3, 3, 4 };
            int n = 0;

            for (int l = 0; l < city.DriveLoops.Count; l++)
            {
                Vector3[] loop = city.DriveLoops[l];
                int count = l < perLoop.Length ? perLoop[l] : 2;

                for (int k = 0; k < count; k++, n++)
                {
                    int start = (k * loop.Length / count + l * 2) % loop.Length;

                    GameObject car = Object.Instantiate(template, root.transform);
                    car.name = "Voiture " + (n + 1);
                    car.transform.position = loop[start];
                    car.SetActive(true);

                    // Les phares : un seul projecteur, sans ombre — il éclaire la chaussée devant.
                    GameObject headlights = EditorBuildUtility.CreateEmpty("Phares", car.transform, new Vector3(0f, 0.9f, 2.4f));
                    headlights.transform.localRotation = Quaternion.Euler(8f, 0f, 0f);
                    Light spot = headlights.AddComponent<Light>();
                    spot.type = LightType.Spot;
                    spot.spotAngle = 64f;
                    spot.range = 24f;
                    spot.intensity = 2.6f;
                    spot.color = new Color(1f, 0.95f, 0.86f);
                    spot.shadows = LightShadows.None;
                    spot.bounceIntensity = 0f;

                    AudioSource horn = car.AddComponent<AudioSource>();
                    horn.playOnAwake = false;
                    horn.spatialBlend = 1f;
                    horn.maxDistance = 40f;

                    TrafficCar driver = car.AddComponent<TrafficCar>();
                    driver.SetPath(loop, start, 8.5f + (n % 3) * 0.8f);
                    SerializedWiring.SetObject(driver, "_horn", horn);
                    EditorUtility.SetDirty(driver);
                }
            }

            Object.DestroyImmediate(template);
            return root;
        }
    }
}
