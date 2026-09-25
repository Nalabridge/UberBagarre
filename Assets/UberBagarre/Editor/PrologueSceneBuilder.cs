using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Feedback;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Sandbox;
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
    /// Génère la scène du PROLOGUE : la planque, l'appel, l'application, le premier combat.
    ///
    /// Elle est séparée de la sandbox de combat, et c'est un choix qui se défend en une
    /// phrase : on ne veut pas traverser cinq minutes de narration à chaque fois qu'on règle
    /// la portée d'un crochet. La sandbox reste l'établi, le prologue est le jeu.
    ///
    /// Les deux scènes partagent TOUT ce qui compte — le joueur, le rig de combat, le décor de
    /// nuit, la chaîne de rendu — parce que ce sont les mêmes constructeurs qui les bâtissent.
    /// Régler un coup dans la sandbox règle le même coup dans le prologue.
    ///
    /// Les deux lieux vivent dans cette unique scène, très loin l'un de l'autre, et un seul est
    /// allumé à la fois (voir LocationDirector). Le fondu au noir de la voiture masque le saut.
    /// </summary>
    public static class PrologueSceneBuilder
    {
        public const string ScenePath = SandboxSceneBuilder.ScenesFolder + "/Prologue.unity";

        /// <summary>
        /// Décalage de la planque. Assez loin pour que rien n'en soit visible depuis la rue,
        /// même sans brume et sans désactivation — une marge, pas une limite exacte.
        /// </summary>
        public static readonly Vector3 HouseOrigin = new Vector3(0f, 0f, -600f);

        /// <summary>Le parking du chapitre 1, de l'autre côté de la rue, aussi loin que la planque.</summary>
        public static readonly Vector3 ParkingOrigin = new Vector3(0f, 0f, 600f);

        /// <summary>L'intérieur du Vertigo, sur le côté : aucun des autres lieux ne le voit.</summary>
        public static readonly Vector3 ClubOrigin = new Vector3(600f, 0f, 0f);

        private const float ClubSidewalkZ = NightStreetBuilder.RoadFar + 0.4f;

        [MenuItem("Uber Bagarre/3 - Construire le JEU (histoire complete)", false, 30)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool exists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath));

            if (exists)
            {
                bool rebuild = EditorUtility.DisplayDialog(
                    "Reconstruire la scene Prologue ?",
                    "La scene existe deja :\n" + ScenePath +
                    "\n\nElle va etre REMPLACEE. Toutes les modifications faites a la main seront perdues.\n\n" +
                    "(Les assets - materiaux, reglages, donnees d'attaque - ne sont pas touches.)",
                    "Reconstruire", "Annuler");

                if (!rebuild) return;
            }

            Build();
        }

        public static void Build()
        {
            // Tout ce que le bac a sable preparait et dont le jeu avait besoin sans le dire : le
            // dossier des reglages (touches), et l'espace colorimetrique. Le jeu se construit
            // maintenant seul, sur un projet neuf.
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

            NightStreetBuilder.Result street = NightStreetBuilder.Build(night);
            HouseBuilder.Result house = HouseBuilder.Build(night, HouseOrigin);
            ParkingBuilder.Result parking = ParkingBuilder.Build(night, ParkingOrigin);
            ClubInteriorBuilder.Result club = ClubInteriorBuilder.Build(night, materials, ClubOrigin);

            // Le joueur naît chez lui : le prologue commence dans la planque, pas dans la rue.
            Camera gameCamera;
            Camera observerCamera;
            GameObject player = SandboxSceneBuilder.BuildPlayer(materials, attacks,
                out gameCamera, out observerCamera);

            player.transform.SetPositionAndRotation(house.Arrival.position, house.Arrival.rotation);

            GraphicsDirector graphics = SandboxSceneBuilder.BuildRendering(sun, moon, street,
                gameCamera, observerCamera);

            // Le bitume du parking reflete comme celui de la rue, et obeit au meme reglage.
            SandboxSceneBuilder.SetComponentArray(graphics, "_reflections",
                street.Ground != null ? street.Ground.GetComponent<PlanarReflection>() : null,
                parking.Ground != null ? parking.Ground.GetComponent<PlanarReflection>() : null);

            MissionBriefing briefing = BuildBriefing();
            MissionBriefing briefingTwo = BuildBriefingTwo();
            MissionBriefing briefingThree = BuildBriefingThree();

            SandboxSceneBuilder.FighterParts target;
            List<SandboxSceneBuilder.FighterParts> crowd;
            BuildCrowd(materials, attacks, night, street.Root, briefing, player, out target, out crowd);

            SandboxSceneBuilder.WireHudAndDebug(player, target.Go, attacks.Straight);

            SandboxSceneBuilder.FighterParts[] brothers = BuildBrothers(materials, attacks, night, parking);
            SandboxSceneBuilder.FighterParts champion = BuildChampion(materials, attacks, night, club, briefingThree);

            Interactable clubCar = BuildClubCarDoor(street.Root);
            Interactable clubDoor = BuildClubEntrance(street.Root);

            PhoneDevice phone = BuildPhone(night, gameCamera, player, briefing);

            BuildStory(player, phone, briefing, house, street, clubCar, target, graphics, attacks,
                briefingTwo, parking, brothers);

            WireChapterTwo(player, briefingThree, club, champion, clubDoor);
            WireFightStaging(player, night, materials, street, parking);
            BuildTestTools(player, graphics);

            // Le club reste eteint jusqu'a ce qu'on y entre : sa musique, sa foule et ses
            // lyres ne tournent pas pendant qu'on est a la planque.
            club.Root.gameObject.SetActive(false);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved) SandboxSceneBuilder.RegisterSceneInBuildSettings(ScenePath);

            SandboxSceneBuilder.OfferLinearColorSpace();

            Selection.activeGameObject = player;

            Debug.Log("[UberBagarre] Scene Prologue generee.\n" +
                      "  Lieux        : la planque (" + HouseOrigin.z + " en Z), la rue devant le club,\n" +
                      "                 le parking du Vertigo (" + ParkingOrigin.z + " en Z)\n" +
                      "  Prologue     : reveil, courrier, appel de " + briefing.FriendName +
                      ", installation, course, trajet,\n" +
                      "                 identification dans le groupe, bagarre avec tutoriel, photo, niveau 2\n" +
                      "  Chapitre 1   : deux etoiles, les freres Kovac au parking, bousculade, coup de tete,\n" +
                      "                 objets a lancer, deux photos, niveau 3, appel de " + briefing.FriendName + "\n" +
                      "  Chapitre 2   : le Vertigo de l'interieur, la fosse et son public : le combat ne\n" +
                      "                 commence qu'une fois la commande recue ET acceptee sur le telephone\n" +
                      "  Touches      : E = interagir / repondre / valider / ramasser, T = telephone,\n" +
                      "                 clic gauche = frapper, lancer l'objet tenu, declencher la photo\n" +
                      "                 G = coup de tete (debloque au niveau 2), X = bousculer\n" +
                      "  Test direct  : coche 'Start At Chapter One' ou 'Start At Chapter Two' sur PrologueDirector.\n" +
                      "  Tout le reste des commandes est identique a la sandbox.\n" +
                      "  Appuie sur Play.");
        }

        // ------------------------------------------------------------------ mission

        private static MissionBriefing BuildBriefing()
        {
            GameObject go = new GameObject("=== Mission ===");
            MissionBriefing briefing = go.AddComponent<MissionBriefing>();

            SerializedWiring.SetString(briefing, "_targetName", "BRUNO MORETTI");
            SerializedWiring.SetString(briefing, "_targetAge", "38 ans");

            // Le signalement doit designer quelque chose de VISIBLE sur le modele, et de
            // visible SOUS CET ECLAIRAGE. Une veste rouge franche tient le coup sous un neon
            // magenta ; « crane rase » ne distinguerait rien du tout, puisque personne n'a de
            // cheveux dans ce jeu.
            SerializedWiring.SetString(briefing, "_targetClothing", "Veste rouge, jean clair");
            SerializedWiring.SetString(briefing, "_targetLocation", "Devant le club — Le Vertigo");
            SerializedWiring.SetString(briefing, "_targetRecord",
                "Videur. Connu pour cogner d'abord.\nA casse le bras d'un livreur en mars.");

            SerializedWiring.SetString(briefing, "_clientName", "CLIENT VERIFIE");
            SerializedWiring.SetInt(briefing, "_stars", 1);
            SerializedWiring.SetInt(briefing, "_reward", 150);
            SerializedWiring.SetInt(briefing, "_experience", 120);
            SerializedWiring.SetString(briefing, "_review", "Propre et rapide. Il a rien dit, il a fait.");
            SerializedWiring.SetString(briefing, "_friendName", "SAMI");

            SerializedWiring.Verify(briefing, "_targetClothing");

            return briefing;
        }

        /// <summary>
        /// Le contrat du chapitre 1 : deux étoiles, deux sujets.
        ///
        /// Le signalement désigne ce qui DISTINGUE les frères du décor, pas ce qui les
        /// distingue entre eux : il n'y a personne d'autre au parking. L'énigme du prologue
        /// était « lequel ? », celle-ci est « comment, à un contre deux ? ».
        /// </summary>
        private static MissionBriefing BuildBriefingTwo()
        {
            GameObject go = new GameObject("=== Mission 2 ===");
            MissionBriefing briefing = go.AddComponent<MissionBriefing>();

            SerializedWiring.SetString(briefing, "_targetName", "LES FRÈRES KOVAC");
            SerializedWiring.SetString(briefing, "_targetAge", "31 et 27 ans");
            SerializedWiring.SetString(briefing, "_targetClothing", "Survêtements, un noir, un bordeaux");
            SerializedWiring.SetString(briefing, "_targetLocation", "Parking du Vertigo — niveau -1");
            SerializedWiring.SetString(briefing, "_targetRecord",
                "Revendent ce qui tombe des camions.\nToujours ensemble. Jamais sans une bouteille.");

            SerializedWiring.SetString(briefing, "_clientName", "CLIENT VERIFIE — 2e commande");
            SerializedWiring.SetInt(briefing, "_stars", 2);
            SerializedWiring.SetInt(briefing, "_reward", 320);
            SerializedWiring.SetInt(briefing, "_experience", 260);
            SerializedWiring.SetString(briefing, "_review", "Efficace. Un peu brutal pour le prix.");
            SerializedWiring.SetInt(briefing, "_reviewStars", 4);
            SerializedWiring.SetString(briefing, "_friendName", "SAMI");

            SerializedWiring.Verify(briefing, "_reviewStars");

            return briefing;
        }

        /// <summary>
        /// Le contrat du chapitre 2 : trois étoiles, dans la fosse du Vertigo.
        /// L'heure du rendez-vous est « MAINTENANT » : la commande tombe sur place.
        /// </summary>
        private static MissionBriefing BuildBriefingThree()
        {
            GameObject go = new GameObject("=== Mission 3 ===");
            MissionBriefing briefing = go.AddComponent<MissionBriefing>();

            SerializedWiring.SetString(briefing, "_targetName", "LE TAUREAU");
            SerializedWiring.SetString(briefing, "_targetAge", "34 ans");
            SerializedWiring.SetString(briefing, "_targetClothing", "Débardeur noir, treillis kaki");
            SerializedWiring.SetString(briefing, "_targetLocation", "Le Vertigo — la fosse, salle du fond");
            SerializedWiring.SetString(briefing, "_meetingTime", "MAINTENANT");
            SerializedWiring.SetString(briefing, "_targetRecord",
                "Champion de la fosse.\nOnze combats, onze K.O.");

            SerializedWiring.SetString(briefing, "_clientName", "CLIENT VIP — parieur");
            SerializedWiring.SetInt(briefing, "_stars", 3);
            SerializedWiring.SetInt(briefing, "_reward", 600);
            SerializedWiring.SetInt(briefing, "_experience", 380);
            SerializedWiring.SetString(briefing, "_review", "Il a fait taire la salle. Je rejoue la semaine prochaine.");
            SerializedWiring.SetInt(briefing, "_reviewStars", 5);
            SerializedWiring.SetString(briefing, "_friendName", "SAMI");

            SerializedWiring.Verify(briefing, "_meetingTime");

            return briefing;
        }

        /// <summary>Le champion de la fosse. Il attend, cerveau éteint, que la commande tombe.</summary>
        private static SandboxSceneBuilder.FighterParts BuildChampion(BuildMaterials materials,
            AttackLibraryBuilder.Library attacks, NightMaterialFactory.Palette night, ClubInteriorBuilder.Result club,
            MissionBriefing briefing)
        {
            FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);
            skin.Shirt = Jacket(night, "M_Debardeur", new Color(0.05f, 0.05f, 0.06f));
            skin.Pants = Jacket(night, "M_Treillis", new Color(0.24f, 0.27f, 0.16f));

            SandboxSceneBuilder.FighterParts parts = SandboxSceneBuilder.BuildFighter(materials, attacks,
                briefing.TargetName, club.ChampionPosition, club.ChampionYaw, skin, 230f, true);

            parts.Go.transform.SetParent(club.Root, true);
            if (parts.Brain != null) parts.Brain.enabled = false;

            return parts;
        }

        /// <summary>La porte du club, côté rue : fermée tant que l'histoire ne l'ouvre pas.</summary>
        private static Interactable BuildClubEntrance(Transform streetRoot)
        {
            GameObject door = EditorBuildUtility.CreateEmpty("Porte du club (entree)", streetRoot,
                new Vector3(0f, 1.1f, NightStreetBuilder.ClubFront - 0.7f));

            BoxCollider collider = door.AddComponent<BoxCollider>();
            collider.size = new Vector3(2.6f, 2.2f, 1.4f);
            collider.isTrigger = true;

            Interactable interactable = door.AddComponent<Interactable>();
            SerializedWiring.SetString(interactable, "_label", "Entrer au Vertigo");
            SerializedWiring.SetString(interactable, "_hint", "La salle du fond");
            SerializedWiring.SetFloat(interactable, "_range", 3f);
            SerializedWiring.SetBool(interactable, "_once", true);
            SerializedWiring.SetBool(interactable, "_enabledForPlayer", false);

            return interactable;
        }

        /// <summary>
        /// La mise en scene des bagarres : les badauds de la rue et du parking, et la
        /// cinematique d'avant-combat (portee par le joueur).
        /// </summary>
        private static void WireFightStaging(GameObject player, NightMaterialFactory.Palette night,
            BuildMaterials materials, NightStreetBuilder.Result street, ParkingBuilder.Result parking)
        {
            FightCrowd streetCrowd = SandboxSceneBuilder.BuildFightCrowd(street.Root, night, materials, 10, 0,
                "Badauds (rue)");
            FightCrowd parkingCrowd = SandboxSceneBuilder.BuildFightCrowd(parking.Root, night, materials, 8, 5,
                "Badauds (parking)");

            PrologueDirector prologue = Object.FindAnyObjectByType<PrologueDirector>();
            if (prologue == null) return;

            SerializedWiring.SetObject(prologue, "_intro", player.GetComponent<FightIntro>());
            SerializedWiring.SetObject(prologue, "_streetCrowd", streetCrowd);
            SerializedWiring.SetObject(prologue, "_parkingCrowd", parkingCrowd);
            SerializedWiring.Verify(prologue, "_intro");
        }

        /// <summary>
        /// Le menu de test DANS le jeu (Tab) : les reglages du bac a sable, et l'onglet TRICHE
        /// (godmode, vol libre, chapitres, lieux). Temporaire, le temps de tester l'histoire
        /// sans la rejouer depuis le debut a chaque essai.
        /// </summary>
        private static void BuildTestTools(GameObject player, GraphicsDirector graphics)
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
            SerializedWiring.SetObject(cheats, "_story", Object.FindAnyObjectByType<StoryDirector>());
            SerializedWiring.SetObject(cheats, "_prologue", Object.FindAnyObjectByType<PrologueDirector>());
            SerializedWiring.SetObject(cheats, "_subtitles", Object.FindAnyObjectByType<SubtitleDisplay>());
            SerializedWiring.SetObject(cheats, "_locations", Object.FindAnyObjectByType<LocationDirector>());
            SerializedWiring.SetObject(cheats, "_progress", Object.FindAnyObjectByType<PlayerProgress>());
            SerializedWiring.Verify(cheats, "_prologue");

            SandboxMenu menu = root.AddComponent<SandboxMenu>();
            SerializedWiring.SetObject(menu, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(menu, "_player", combatant);
            SerializedWiring.SetObject(menu, "_cursor", player.GetComponent<CursorLockController>());
            SerializedWiring.SetObject(menu, "_playerCombat", player.GetComponent<PlayerCombat>());
            SerializedWiring.SetObject(menu, "_hitStop", player.GetComponent<HitStop>());
            SerializedWiring.SetObject(menu, "_statistics", statistics);
            SerializedWiring.SetObject(menu, "_graphics", graphics);
            SerializedWiring.SetObject(menu, "_cheats", cheats);
            SerializedWiring.SetBool(menu, "_storyMode", true);
            SerializedWiring.Verify(menu, "_cursor");
        }

        /// <summary>Le club devient un lieu, et le chapitre 2 reçoit tout ce qu'il pilote.</summary>
        private static void WireChapterTwo(GameObject player, MissionBriefing briefingThree,
            ClubInteriorBuilder.Result club, SandboxSceneBuilder.FighterParts champion, Interactable clubDoor)
        {
            PrologueDirector prologue = Object.FindAnyObjectByType<PrologueDirector>();
            LocationDirector locations = Object.FindAnyObjectByType<LocationDirector>();

            if (locations != null)
            {
                SerializedObject so = SerializedWiring.Open(locations);
                SerializedProperty array = so.FindProperty("_locations");

                if (array != null)
                {
                    int index = array.arraySize;
                    array.arraySize = index + 1;

                    SerializedProperty inside = array.GetArrayElementAtIndex(index);
                    inside.FindPropertyRelative("name").stringValue = "Club";
                    inside.FindPropertyRelative("root").objectReferenceValue = club.Root;
                    inside.FindPropertyRelative("arrival").objectReferenceValue = club.Arrival;

                    so.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            if (prologue == null)
            {
                Debug.LogWarning("[UberBagarre] PrologueDirector introuvable : chapitre 2 non cable.");
                return;
            }

            SerializedWiring.SetObject(prologue, "_briefingThree", briefingThree);
            SerializedWiring.SetObject(prologue, "_clubDoor", clubDoor);
            SerializedWiring.SetObject(prologue, "_clubExit", club.Exit);
            SerializedWiring.SetObject(prologue, "_ring", club.Ring);
            SerializedWiring.SetObject(prologue, "_ringGate", club.RingGate);
            SerializedWiring.SetObject(prologue, "_champion", champion.Combatant);
            SerializedWiring.SetObject(prologue, "_championBrain", champion.Brain);
            SerializedWiring.SetObject(prologue, "_crowd", club.Crowd);
            SerializedWiring.SetString(prologue, "_clubLocation", "Club");

            SandboxSceneBuilder.SetComponentArray(prologue, "_ringCrowd", club.RingCrowd.ToArray());

            SerializedWiring.Verify(prologue, "_clubDoor");
            SerializedWiring.Verify(prologue, "_ringGate");
            SerializedWiring.Verify(prologue, "_champion");
        }

        /// <summary>
        /// Les frères Kovac, au fond du parking, en train de vider la camionnette.
        ///
        /// Ils sortent du même constructeur que tous les autres combattants — même corps, même
        /// physique d'impact, mêmes coups. Ce qui en fait un chapitre différent, c'est qu'ils
        /// sont DEUX : il faut se placer pour ne pas finir entre eux, et le décor (bouteilles,
        /// caisses, fûts) devient une arme ou un obstacle.
        ///
        /// Leurs cerveaux sont éteints : ils déchargent, de dos, jusqu'à ce que le scénario les
        /// réveille à l'approche de la camionnette.
        /// </summary>
        private static SandboxSceneBuilder.FighterParts[] BuildBrothers(BuildMaterials materials,
            AttackLibraryBuilder.Library attacks, NightMaterialFactory.Palette night,
            ParkingBuilder.Result parking)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Freres Kovac", parking.Root, Vector3.zero);

            SandboxSceneBuilder.FighterParts elder = BuildBrother(materials, attacks, night, root.transform,
                "Dragan", parking.FirstBrother, 80f, new Color(0.06f, 0.06f, 0.075f), 118f);

            SandboxSceneBuilder.FighterParts younger = BuildBrother(materials, attacks, night, root.transform,
                "Milan", parking.SecondBrother, 37f, new Color(0.32f, 0.05f, 0.11f), 104f);

            return new[] { elder, younger };
        }

        private static SandboxSceneBuilder.FighterParts BuildBrother(BuildMaterials materials,
            AttackLibraryBuilder.Library attacks, NightMaterialFactory.Palette night, Transform root,
            string name, Vector3 position, float yaw, Color tracksuit, float health)
        {
            FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);

            // Veste et pantalon dans le meme tissu : c'est ce qui fait un survetement, et c'est
            // ce qui les rend reconnaissables sous les mats a LED, qui ecrasent les nuances.
            Material suit = Jacket(night, "M_Survetement_" + name, tracksuit);
            skin.Shirt = suit;
            skin.Pants = suit;

            SandboxSceneBuilder.FighterParts parts = SandboxSceneBuilder.BuildFighter(
                materials, attacks, name, position, yaw, skin, health, true);

            parts.Go.transform.SetParent(root, true);
            if (parts.Brain != null) parts.Brain.enabled = false;

            return parts;
        }

        // ------------------------------------------------------------------ le groupe

        /// <summary>
        /// La cible et les quatre figurants.
        ///
        /// Ils sortent du MÊME constructeur que l'adversaire de la sandbox, et c'est ce qui
        /// rend la scène d'identification honnête : ils ont le même corps, le même rig, la même
        /// respiration. Seule la couleur des vêtements change. Le joueur ne peut donc pas
        /// repérer la cible autrement qu'en lisant le signalement — ce qui était tout l'intérêt.
        ///
        /// Aucun n'a de cerveau au départ, y compris la cible : le groupe attend, comme un
        /// groupe devant une boîte. Le scénario n'allume celui de la cible qu'une fois qu'elle
        /// a été reconnue.
        /// </summary>
        private static void BuildCrowd(BuildMaterials materials, AttackLibraryBuilder.Library attacks,
            NightMaterialFactory.Palette night, Transform streetRoot, MissionBriefing briefing,
            GameObject player, out SandboxSceneBuilder.FighterParts target,
            out List<SandboxSceneBuilder.FighterParts> crowd)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Groupe devant le club", streetRoot, Vector3.zero);

            Material redJacket = Jacket(night, "M_VesteRouge", new Color(0.62f, 0.09f, 0.08f));
            Material lightJeans = EditorBuildUtility.CreateOrUpdateMaterial(
                NightMaterialFactory.MaterialsFolder, "M_JeanClair",
                new Color(0.46f, 0.50f, 0.58f), 0.03f, 0f);

            FighterBuilder.Skin targetSkin = FighterBuilder.Skin.Enemy(materials);
            targetSkin.Shirt = redJacket;
            targetSkin.Pants = lightJeans;

            // La cible encaisse plus que l'adversaire de la sandbox : le tutoriel doit avoir le
            // temps de dérouler ses quatre consignes avant qu'il ne tombe. Un K.O. au troisième
            // coup laisserait la moitié des commandes jamais montrées.
            target = SandboxSceneBuilder.BuildFighter(materials, attacks, briefing.TargetName,
                new Vector3(1.8f, 0f, 6.2f), 186f, targetSkin, 165f, true);

            target.Go.transform.SetParent(root.transform, true);
            if (target.Brain != null) target.Brain.enabled = false;

            AddCrowdMember(target, player, briefing.TargetName, true, "Veste rouge. Jean clair.");

            crowd = new List<SandboxSceneBuilder.FighterParts>();

            AddBystander(materials, attacks, night, root.transform, player, crowd,
                "Type en noir", new Vector3(-4.3f, 0f, 6.7f), 166f,
                new Color(0.10f, 0.10f, 0.12f), new Color(0.13f, 0.14f, 0.18f),
                "Veste noire. Capuche.");

            AddBystander(materials, attacks, night, root.transform, player, crowd,
                "Type en vert", new Vector3(-1.9f, 0f, 5.8f), 202f,
                new Color(0.14f, 0.26f, 0.16f), new Color(0.16f, 0.17f, 0.22f),
                "Blouson vert. Pantalon sombre.");

            AddBystander(materials, attacks, night, root.transform, player, crowd,
                "Type en bleu", new Vector3(4.1f, 0f, 6.6f), 176f,
                new Color(0.12f, 0.17f, 0.34f), new Color(0.15f, 0.15f, 0.17f),
                "Veste bleu nuit. Jean foncé.");

            AddBystander(materials, attacks, night, root.transform, player, crowd,
                "Type en gris", new Vector3(5.9f, 0f, 5.9f), 214f,
                new Color(0.32f, 0.32f, 0.33f), new Color(0.14f, 0.15f, 0.19f),
                "Manteau gris. Chaussures de ville.");
        }

        private static void AddBystander(BuildMaterials materials, AttackLibraryBuilder.Library attacks,
            NightMaterialFactory.Palette night, Transform root, GameObject player,
            List<SandboxSceneBuilder.FighterParts> crowd, string name, Vector3 position, float yaw,
            Color jacket, Color trousers, string description)
        {
            FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);
            skin.Shirt = Jacket(night, "M_Veste_" + name.Replace(" ", ""), jacket);

            skin.Pants = EditorBuildUtility.CreateOrUpdateMaterial(
                NightMaterialFactory.MaterialsFolder, "M_Pantalon_" + name.Replace(" ", ""),
                trousers, 0.03f, 0f);

            SandboxSceneBuilder.FighterParts parts = SandboxSceneBuilder.BuildFighter(
                materials, attacks, name, position, yaw, skin, 80f, false);

            parts.Go.transform.SetParent(root, true);

            // Pas de cerveau, pas de ragdoll qui parte tout seul : un figurant qui riposte
            // transformerait la scène d'identification en bagarre générale, et le joueur
            // n'aurait plus aucune raison de lire la fiche.
            if (parts.Brain != null) parts.Brain.enabled = false;

            AddCrowdMember(parts, player, name, false, description);
            crowd.Add(parts);
        }

        private static void AddCrowdMember(SandboxSceneBuilder.FighterParts parts, GameObject player,
            string displayName, bool isTarget, string description)
        {
            if (parts == null || parts.Go == null) return;

            CrowdMember member = parts.Go.AddComponent<CrowdMember>();

            SerializedWiring.SetString(member, "_displayName", displayName);
            SerializedWiring.SetBool(member, "_isTarget", isTarget);
            SerializedWiring.SetString(member, "_description", description);

            // Seule la TETE est confiee a CrowdMember. Le bassin, lui, est deja pilote a
            // chaque image par le cycle de marche : deux composants qui ecrivent la meme
            // rotation produisent un tremblement dont la cause est invisible, puisque les deux
            // ont raison separement. La nuque, elle, n'est touchee par personne d'autre.
            if (parts.Body != null) SerializedWiring.SetObject(member, "_head", parts.Body.Neck);

            SerializedWiring.SetObject(member, "_lookTarget", player.transform);
            SerializedWiring.Verify(member, "_lookTarget");
        }

        private static Material Jacket(NightMaterialFactory.Palette night, string name, Color color)
        {
            // Graine derivee des caracteres du nom, pas de GetHashCode : le hachage de chaine
            // n'est pas garanti stable d'une execution a l'autre, ce qui regenererait la
            // texture a chaque construction pour un resultat different a chaque fois.
            int seed = 17;
            for (int i = 0; i < name.Length; i++) seed = seed * 31 + name[i];

            Texture2D weave = EditorBuildUtility.CreateOrUpdateFabricTexture(
                NightMaterialFactory.TexturesFolder, "T_" + name, 256, color, 4, 0.11f,
                (seed & 0x7FFFFFFF) % 9000);

            return EditorBuildUtility.CreateOrUpdateMaterial(NightMaterialFactory.MaterialsFolder,
                name, Color.white, 0.05f, 0f, weave, new Vector2(12f, 12f));
        }

        // ------------------------------------------------------------------ la voiture du club

        private static Interactable BuildClubCarDoor(Transform streetRoot)
        {
            // La voiture garée devant le club est déjà construite par la rue : on n'ajoute
            // qu'un volume d'interaction à l'endroit de sa portière conducteur.
            GameObject door = EditorBuildUtility.CreateEmpty("Portiere (retour)", streetRoot,
                new Vector3(-10.5f, 1f, 3.5f));

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

        // ------------------------------------------------------------------ le telephone

        /// <summary>
        /// Le téléphone, construit dans la main : un corps, une dalle émissive et une lampe.
        ///
        /// Il est enfant de la CAMÉRA, donc il suit la visée sans une ligne de code. Et comme
        /// c'est un vrai objet, il apparaît dans les reflets du bitume mouillé — consulter son
        /// téléphone au bord d'une flaque, la nuit, se voit dans la flaque.
        /// </summary>
        internal static PhoneDevice BuildPhone(NightMaterialFactory.Palette night, Camera camera,
            GameObject player, MissionBriefing briefing)
        {
            Material body = EditorBuildUtility.CreateOrUpdateMaterial(
                NightMaterialFactory.MaterialsFolder, "M_Telephone",
                new Color(0.055f, 0.055f, 0.065f), 0.72f, 0.5f);

            Material glass = NightMaterialFactory.CreateNeon(NightMaterialFactory.MaterialsFolder,
                "M_EcranTelephone", new Color(0.85f, 0.88f, 1f), 0.5f);

            GameObject phoneGo = EditorBuildUtility.CreateEmpty("Telephone", camera.transform, Vector3.zero);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Coque", phoneGo.transform,
                Vector3.zero, new Vector3(0.076f, 0.154f, 0.009f), body, false);

            // La dalle est légèrement en avant de la coque, côté caméra : c'est le -Z local,
            // puisque le téléphone est orienté comme la caméra et que l'écran nous fait face.
            GameObject screen = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Dalle",
                phoneGo.transform, new Vector3(0f, 0.002f, -0.0052f),
                new Vector3(0.070f, 0.148f, 0.001f), glass, false);

            GameObject lightGo = EditorBuildUtility.CreateEmpty("Lueur d'ecran", phoneGo.transform,
                new Vector3(0f, 0f, -0.05f));

            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            light.range = 1.4f;
            light.intensity = 0.2f;
            light.color = new Color(0.85f, 0.88f, 1f);
            light.renderMode = LightRenderMode.ForcePixel;
            light.shadows = LightShadows.None;

            PlayerInputReader input = player.GetComponent<PlayerInputReader>();

            PhoneDevice device = phoneGo.AddComponent<PhoneDevice>();
            SerializedWiring.SetObject(device, "_anchor", camera.transform);
            SerializedWiring.SetObject(device, "_screenTransform", screen.transform);
            SerializedWiring.SetObject(device, "_screenRenderer", screen.GetComponent<Renderer>());
            SerializedWiring.SetObject(device, "_screenLight", light);
            SerializedWiring.SetObject(device, "_input", input);
            SerializedWiring.SetObject(device, "_hands", player.GetComponentInChildren<FirstPersonHands>());
            SerializedWiring.Verify(device, "_screenTransform");

            PhoneDisplay display = phoneGo.AddComponent<PhoneDisplay>();
            SerializedWiring.SetObject(display, "_device", device);
            SerializedWiring.SetObject(display, "_camera", camera);
            SerializedWiring.SetObject(display, "_briefing", briefing);

            // Le systeme du telephone : ecran d'accueil, applis, navigation.
            PhoneOS os = phoneGo.AddComponent<PhoneOS>();
            SerializedWiring.SetObject(os, "_device", device);
            SerializedWiring.SetObject(os, "_input", input);
            SerializedWiring.SetObject(display, "_os", os);

            PhoneCamera photo = phoneGo.AddComponent<PhoneCamera>();
            SerializedWiring.SetObject(photo, "_device", device);
            SerializedWiring.SetObject(photo, "_input", input);
            SerializedWiring.SetObject(photo, "_camera", camera);
            SerializedWiring.SetObject(photo, "_look", player.GetComponent<PlayerLook>());
            SerializedWiring.SetObject(photo, "_display", display);

            return device;
        }

        // ------------------------------------------------------------------ narration

        private static void BuildStory(GameObject player, PhoneDevice phone, MissionBriefing briefing,
            HouseBuilder.Result house, NightStreetBuilder.Result street, Interactable clubCar,
            SandboxSceneBuilder.FighterParts target, GraphicsDirector graphics,
            AttackLibraryBuilder.Library attacks, MissionBriefing briefingTwo, ParkingBuilder.Result parking,
            SandboxSceneBuilder.FighterParts[] brothers)
        {
            GameObject root = new GameObject("=== Histoire ===");

            PlayerInputReader input = player.GetComponent<PlayerInputReader>();
            Camera camera = player.GetComponentInChildren<Camera>();

            SubtitleDisplay subtitles = root.AddComponent<SubtitleDisplay>();

            // La voix des sous-titres : les fichiers pre-enregistres de Resources/Voix, ou un
            // babillage pour les repliques calculees en jeu.
            root.AddComponent<AudioSource>();
            DialogueVoice voice = root.AddComponent<DialogueVoice>();
            SerializedWiring.SetObject(subtitles, "_voice", voice);

            ObjectiveDisplay objectives = root.AddComponent<ObjectiveDisplay>();
            ScreenFader fader = root.AddComponent<ScreenFader>();
            TutorialPrompt tutorial = root.AddComponent<TutorialPrompt>();
            PlayerProgress progress = root.AddComponent<PlayerProgress>();

            PhoneDisplay display = phone != null ? phone.GetComponent<PhoneDisplay>() : null;
            if (display != null) SerializedWiring.SetObject(display, "_progress", progress);

            PhoneOS os = phone != null ? phone.GetComponent<PhoneOS>() : null;
            if (os != null) SerializedWiring.SetObject(os, "_progress", progress);

            // Le coup de tete se GAGNE : il est verrouille des la construction, et c'est le
            // passage au niveau 2, a la fin du prologue, qui l'ouvre.
            PlayerCombat playerCombat = player.GetComponent<PlayerCombat>();
            if (playerCombat != null) SerializedWiring.SetBool(playerCombat, "_headbuttUnlocked", false);

            StoryDirector story = root.AddComponent<StoryDirector>();
            SerializedWiring.SetObject(story, "_input", input);
            SerializedWiring.SetObject(story, "_subtitles", subtitles);
            SerializedWiring.SetObject(story, "_objectives", objectives);

            // --- interaction, sur le joueur : elle vise depuis sa caméra.
            InteractionSystem interaction = player.AddComponent<InteractionSystem>();
            SerializedWiring.SetObject(interaction, "_input", input);
            SerializedWiring.SetObject(interaction, "_camera", camera);
            SerializedWiring.SetObject(interaction, "_phone", phone);

            // Ramasser passe APRES l'interaction : si une portiere est visee, E lui revient.
            PropHandler props = player.GetComponent<PropHandler>();

            if (props != null)
            {
                SerializedWiring.SetObject(props, "_interaction", interaction);
                SerializedWiring.SetObject(props, "_phone", phone);
            }

            TargetFinder finder = player.AddComponent<TargetFinder>();
            SerializedWiring.SetObject(finder, "_camera", camera);
            SerializedWiring.SetObject(finder, "_briefing", briefing);
            SerializedWiring.SetObject(finder, "_crowdRoot", street.Root);
            SerializedWiring.SetBool(finder, "_searching", false);
            SerializedWiring.Verify(finder, "_crowdRoot");

            // --- les lieux
            GameObject streetArrival = EditorBuildUtility.CreateEmpty("Arrivee rue", street.Root,
                new Vector3(0f, 0f, -6.5f));

            LocationDirector locations = root.AddComponent<LocationDirector>();
            SerializedWiring.SetObject(locations, "_player", player);
            if (os != null) SerializedWiring.SetObject(os, "_locations", locations);
            SerializedWiring.SetInt(locations, "_startIndex", 0);

            SerializedObject so = SerializedWiring.Open(locations);
            SerializedProperty array = so.FindProperty("_locations");

            if (array != null)
            {
                array.arraySize = 3;

                SerializedProperty home = array.GetArrayElementAtIndex(0);
                home.FindPropertyRelative("name").stringValue = "Maison";
                home.FindPropertyRelative("root").objectReferenceValue = house.Root;
                home.FindPropertyRelative("arrival").objectReferenceValue = house.Arrival;

                SerializedProperty outside = array.GetArrayElementAtIndex(1);
                outside.FindPropertyRelative("name").stringValue = "Rue";
                outside.FindPropertyRelative("root").objectReferenceValue = street.Root;
                outside.FindPropertyRelative("arrival").objectReferenceValue = streetArrival.transform;

                SerializedProperty lot = array.GetArrayElementAtIndex(2);
                lot.FindPropertyRelative("name").stringValue = "Parking";
                lot.FindPropertyRelative("root").objectReferenceValue = parking.Root;
                lot.FindPropertyRelative("arrival").objectReferenceValue = parking.Arrival;

                so.ApplyModifiedPropertiesWithoutUndo();
            }
            else
            {
                Debug.LogWarning("[UberBagarre] Champ '_locations' introuvable sur LocationDirector.", locations);
            }

            // --- le scenario
            PrologueDirector prologue = root.AddComponent<PrologueDirector>();

            SerializedWiring.SetObject(prologue, "_story", story);
            SerializedWiring.SetObject(prologue, "_subtitles", subtitles);
            SerializedWiring.SetObject(prologue, "_objectives", objectives);
            SerializedWiring.SetObject(prologue, "_fader", fader);
            SerializedWiring.SetObject(prologue, "_tutorial", tutorial);
            SerializedWiring.SetObject(prologue, "_briefing", briefing);

            SerializedWiring.SetObject(prologue, "_input", input);
            SerializedWiring.SetObject(prologue, "_player", player.GetComponent<Combatant>());
            SerializedWiring.SetObject(prologue, "_playerGuard", player.GetComponent<GuardSystem>());
            SerializedWiring.SetObject(prologue, "_phone", phone);
            SerializedWiring.SetObject(prologue, "_interaction", interaction);
            SerializedWiring.SetObject(prologue, "_look", player.GetComponent<PlayerLook>());

            SerializedWiring.SetObject(prologue, "_locations", locations);
            SerializedWiring.SetObject(prologue, "_finder", finder);
            SerializedWiring.SetObject(prologue, "_letters", house.Letters);
            SerializedWiring.SetObject(prologue, "_carAtHouse", house.Car);
            SerializedWiring.SetObject(prologue, "_carAtClub", clubCar);

            SerializedWiring.SetObject(prologue, "_target", target.Combatant);
            SerializedWiring.SetObject(prologue, "_targetBrain", target.Brain);
            SerializedWiring.SetObject(prologue, "_targetKnockdown", target.Knockdown);
            SerializedWiring.SetObject(prologue, "_targetTransform", target.Go.transform);

            SerializedWiring.SetObject(prologue, "_straight", attacks.Straight);
            SerializedWiring.SetObject(prologue, "_hook", attacks.Hook);
            SerializedWiring.SetObject(prologue, "_headbutt", attacks.Headbutt);
            SerializedWiring.SetObject(prologue, "_shove", attacks.Shove);

            SerializedWiring.SetObject(prologue, "_progress", progress);
            SerializedWiring.SetObject(prologue, "_phoneDisplay", display);
            SerializedWiring.SetObject(prologue, "_playerCombat", playerCombat);
            SerializedWiring.SetObject(prologue, "_props", props);

            // --- chapitre 1
            SerializedWiring.SetObject(prologue, "_briefingTwo", briefingTwo);
            SerializedWiring.SetObject(prologue, "_carAtParking", parking.Car);
            SerializedWiring.SetObject(prologue, "_van", parking.Van);
            SerializedWiring.SetString(prologue, "_parkingLocation", "Parking");

            Component[] brotherBodies = new Component[brothers.Length];
            Component[] brotherBrains = new Component[brothers.Length];

            for (int i = 0; i < brothers.Length; i++)
            {
                brotherBodies[i] = brothers[i] != null ? brothers[i].Combatant : null;
                brotherBrains[i] = brothers[i] != null ? brothers[i].Brain : null;
            }

            SandboxSceneBuilder.SetComponentArray(prologue, "_brothers", brotherBodies);
            SandboxSceneBuilder.SetComponentArray(prologue, "_brotherBrains", brotherBrains);

            // Relecture des références dont l'absence ne provoquerait AUCUNE erreur, seulement
            // un prologue qui s'arrête sans raison visible à l'étape correspondante.
            SerializedWiring.Verify(prologue, "_letters");
            SerializedWiring.Verify(prologue, "_carAtHouse");
            SerializedWiring.Verify(prologue, "_carAtClub");
            SerializedWiring.Verify(prologue, "_targetKnockdown");
            SerializedWiring.Verify(prologue, "_straight");
            SerializedWiring.Verify(prologue, "_carAtParking");
            SerializedWiring.Verify(prologue, "_van");
            SerializedWiring.Verify(prologue, "_progress");

            // Le prologue commence de nuit, quelle que soit la valeur sauvegardee du curseur
            // d'heure : une planque a 2 h du matin en plein soleil n'a plus aucun sens.
            if (graphics == null) return;

            SerializedWiring.SetFloat(graphics, "_day", 0f);
            SerializedWiring.SetBool(graphics, "_restoreDay", false);
            SerializedWiring.Verify(graphics, "_restoreDay");
        }
    }
}
