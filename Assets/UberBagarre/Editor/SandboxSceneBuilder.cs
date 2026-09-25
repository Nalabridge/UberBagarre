using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Core;
using UberBagarre.Enemy;
using UberBagarre.Feedback;
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
    /// Génère la scène de test complète : arène, joueur, ennemi, sac de frappe, interface.
    ///
    /// Pourquoi un générateur plutôt qu'un fichier .unity livré tel quel : un .unity est un
    /// graphe d'objets liés par GUID, illisible et fragile hors d'Unity. Généré par Unity
    /// lui-même il est forcément valide, et le code de construction documente la scène mieux
    /// qu'une capture d'écran — on y lit exactement quel composant est branché à quoi.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        public const string ScenesFolder = "Assets/UberBagarre/Scenes";
        public const string ScenePath = ScenesFolder + "/CombatSandbox.unity";

        internal const string SettingsFolder = "Assets/UberBagarre/Settings";

        /// <summary>Sert au tiling des matériaux de sol, pas à une dimension d'arène.</summary>
        private const float ArenaSize = 26f;

        private const float PlayerHeight = 1.8f;
        private const float PlayerRadius = 0.28f;
        private const float SpawnDistance = 5f;

        [MenuItem("Uber Bagarre/2 - Construire le bac a sable (test du combat)", false, 20)]
        public static void BuildFromMenu()
        {
            if (!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo()) return;

            bool sceneExists = !string.IsNullOrEmpty(AssetDatabase.AssetPathToGUID(ScenePath));
            if (sceneExists)
            {
                bool rebuild = EditorUtility.DisplayDialog(
                    "Reconstruire la scene Combat Sandbox ?",
                    "La scene existe deja :\n" + ScenePath +
                    "\n\nElle va etre REMPLACEE. Toutes les modifications faites a la main dans cette scene seront perdues.\n\n" +
                    "(Les assets - materiaux, reglages, donnees d'attaque - ne sont pas touches.)",
                    "Reconstruire", "Annuler");

                if (!rebuild) return;
            }

            Build();
        }

        public static void Build()
        {
            EditorBuildUtility.EnsureFolder(ScenesFolder);
            EditorBuildUtility.EnsureFolder(SettingsFolder);
            ProceduralMeshFactory.EnsureLibrary();

            BuildMaterials materials = BuildMaterials.CreateAll(ArenaSize);
            AttackLibraryBuilder.Library attacks = AttackLibraryBuilder.BuildAll(false);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            Light sun;
            Light moon;
            BuildLighting(out sun, out moon);

            NightMaterialFactory.Palette night = NightMaterialFactory.Create(
                NightStreetBuilder.GroundLength, NightStreetBuilder.GroundDepth);

            NightStreetBuilder.Result street = NightStreetBuilder.Build(night);
            BuildPunchingBag(materials);

            Camera gameCamera;
            Camera observerCamera;
            GameObject player = BuildPlayer(materials, attacks, out gameCamera, out observerCamera);

            GameObject enemy = BuildEnemy(materials, attacks);

            GraphicsDirector graphics = BuildRendering(sun, moon, street, gameCamera, observerCamera);
            WireHudAndDebug(player, enemy, attacks.Straight);
            BuildSpawnSystem(player, enemy, graphics);
            BuildOrder(night, player, gameCamera);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved) RegisterSceneInBuildSettings();

            OfferLinearColorSpace();

            Selection.activeGameObject = player;

            Debug.Log("[UberBagarre] Scene Combat Sandbox generee : rue de nuit devant la boite.\n" +
                      "  Render pipeline : " + EditorBuildUtility.ActivePipelineName() + "\n" +
                      "  Espace colorim. : " + PlayerSettings.colorSpace + "\n" +
                      "  Commande     : l'adversaire ATTEND que tu acceptes la course : T = telephone, E = accepter\n" +
                      "  Deplacement  : WASD/ZQSD, souris = visee, Maj = sprint, C = accroupi / glissade\n" +
                      "  Poings       : clic gauche = direct, clic DROIT = crochet, clic MOLETTE = uppercut\n" +
                      "  Pieds        : F = coup de pied de face, V = coup de pied bas (fait tomber)\n" +
                      "  Corps a corps: G = coup de tete, X = bousculer (casse la garde, fait de la place)\n" +
                      "  Objets       : E = ramasser / lacher, clic gauche = lancer. Tout ce qui est leger\n" +
                      "                 bouge quand on le frappe : bouteilles, cones, caisses, sacs, chaise\n" +
                      "  Defense      : Ctrl gauche = garde (les 0,26 premieres secondes PARENT le coup),\n" +
                      "                 Alt gauche = esquive (direction = WASD)\n" +
                      "  Contextuel   : en sprintant = charge d'epaule, en l'air = coup plongeant,\n" +
                      "                 en glissade = balayage, cible au sol + pied = coup de grace\n" +
                      "  Charge       : maintenir uppercut / coup de pied arme le coup (jusqu'a x2,2)\n" +
                      "  Outils       : TAB = menu (PV, degats, profils, vagues, GRAPHISMES, statistiques),\n" +
                      "                 F3 = camera d'observation (+ / - pour le zoom)\n" +
                      "  Debug        : F1 = overlay, R = relancer le combat, Echap = liberer le curseur\n" +
                      "  Graphismes   : bloom, tonemap ACES, reflet planaire sur le bitume mouille,\n" +
                      "                 cycle jour / nuit. Tout se regle dans TAB > Graphismes.\n" +
                      "  Appuie sur Play.");
        }

        /// <summary>
        /// Propose de passer le projet en espace colorimétrique linéaire.
        ///
        /// Ce n'est pas un détail de réglage, c'est la différence entre un éclairage juste et
        /// un éclairage faux. En GAMMA, Unity additionne les contributions des lampes sur des
        /// valeurs déjà encodées pour l'écran : deux lampes d'intensité 1 donnent beaucoup
        /// plus que 2, les hautes lumières se délavent en blanc laiteux, et les dégradés
        /// autour d'un lampadaire cassent en bandes visibles. Une rue de nuit éclairée par
        /// trente sources est exactement le cas où ça se voit le plus.
        ///
        /// C'est demandé et pas imposé : le changement déclenche un réimport complet du
        /// projet, ce qui prend du temps et ne doit jamais être une surprise.
        /// </summary>
        internal static void OfferLinearColorSpace()
        {
            if (PlayerSettings.colorSpace == ColorSpace.Linear) return;

            bool accept = EditorUtility.DisplayDialog(
                "Passer en espace colorimetrique lineaire ?",
                "Le projet est actuellement en GAMMA.\n\n" +
                "En gamma, l'addition de plusieurs lampes est fausse : les zones eclairees " +
                "virent au blanc laiteux et les degrades autour des lampadaires cassent en " +
                "bandes. Avec une trentaine de sources dans la rue, ca se voit tout de suite.\n\n" +
                "Le passage en LINEAIRE declenche un reimport complet du projet (quelques " +
                "minutes selon la machine). Le post-traitement fonctionne dans les deux cas, " +
                "mais il ne peut pas rattraper un eclairage calcule faux en amont.",
                "Passer en lineaire", "Laisser en gamma");

            if (!accept) return;

            PlayerSettings.colorSpace = ColorSpace.Linear;
            Debug.Log("[UberBagarre] Espace colorimetrique passe en LINEAIRE. " +
                      "Laisse Unity terminer le reimport avant de relancer la scene.");
        }

        // ------------------------------------------------------------------ commande

        /// <summary>
        /// Le téléphone et la commande : même dans le bac à sable, le combat ne commence
        /// qu'une fois la course reçue et acceptée. L'adversaire attend dans la rue jusque-là.
        /// </summary>
        private static void BuildOrder(NightMaterialFactory.Palette night, GameObject player, Camera gameCamera)
        {
            GameObject go = new GameObject("=== Commande ===");

            MissionBriefing briefing = go.AddComponent<MissionBriefing>();
            SerializedWiring.SetString(briefing, "_targetName", "L'ADVERSAIRE");
            SerializedWiring.SetString(briefing, "_targetAge", "Age inconnu");
            SerializedWiring.SetString(briefing, "_targetClothing", "Il t'attend au milieu de la rue");
            SerializedWiring.SetString(briefing, "_targetLocation", "Devant le Vertigo");
            SerializedWiring.SetString(briefing, "_meetingTime", "MAINTENANT");
            SerializedWiring.SetString(briefing, "_targetRecord",
                "Bac a sable : sujet d'entrainement.\nIl ne bouge pas tant que la course n'est pas acceptee.");
            SerializedWiring.SetString(briefing, "_clientName", "BAC A SABLE");
            SerializedWiring.SetInt(briefing, "_stars", 2);
            SerializedWiring.SetInt(briefing, "_reward", 200);

            PhoneDevice phone = PrologueSceneBuilder.BuildPhone(night, gameCamera, player, briefing);

            PropHandler props = player.GetComponent<PropHandler>();
            if (props != null) SerializedWiring.SetObject(props, "_phone", phone);

            SandboxOrder order = go.AddComponent<SandboxOrder>();
            SerializedWiring.SetObject(order, "_phone", phone);
            SerializedWiring.SetObject(order, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.Verify(order, "_phone");
        }

        // ------------------------------------------------------------------ décor

        /// <summary>
        /// Les deux sources naturelles : le soleil et la lune.
        ///
        /// Les deux existent en permanence, et c'est le cycle jour / nuit qui décide laquelle
        /// est allumée. Construire « la lumière de la scène » pour une seule heure serait
        /// revenir au problème que le curseur d'heure résout : à midi comme à minuit, la
        /// scène doit tenir sans qu'on aille rééditer un objet.
        ///
        /// De nuit, la lune ne sert PAS à éclairer — elle est presque invisible. Son vrai rôle
        /// est de poser une direction commune aux ombres et de mettre un froid léger sur les
        /// surfaces tournées vers le ciel, ce qui sépare les toits du fond. Tout le reste de
        /// la lumière vient des lampadaires et des enseignes.
        /// </summary>
        internal static void BuildLighting(out Light sun, out Light moon)
        {
            GameObject root = new GameObject("=== Eclairage ===");

            GameObject sunGo = EditorBuildUtility.CreateEmpty("Soleil", root.transform, Vector3.zero);
            sunGo.transform.rotation = Quaternion.Euler(42f, -40f, 0f);

            sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 0f;
            sun.color = new Color(1f, 0.89f, 0.72f);
            sun.shadows = LightShadows.Soft;
            sun.shadowStrength = 0.82f;
            sun.shadowBias = 0.03f;
            sun.shadowNormalBias = 0.25f;
            sun.enabled = false;

            GameObject moonGo = EditorBuildUtility.CreateEmpty("Lune", root.transform, Vector3.zero);
            moonGo.transform.rotation = Quaternion.Euler(28f, 152f, 0f);

            moon = moonGo.AddComponent<Light>();
            moon.type = LightType.Directional;
            moon.intensity = 0.22f;
            moon.color = new Color(0.52f, 0.63f, 0.95f);
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.45f;
            moon.shadowBias = 0.04f;
            moon.shadowNormalBias = 0.3f;
        }

        /// <summary>
        /// Rendu : qualité, post-traitement, cycle jour / nuit, façade de réglages.
        ///
        /// Les deux caméras reçoivent la MÊME chaîne de post-traitement. C'est le point à ne
        /// pas rater : sans cela, passer en caméra d'observation ferait disparaître le bloom
        /// et l'étalonnage, et on conclurait que l'effet ne marche pas alors qu'il n'est
        /// simplement pas installé sur la caméra qu'on regarde.
        /// </summary>
        internal static GraphicsDirector BuildRendering(Light sun, Light moon, NightStreetBuilder.Result street,
            Camera gameCamera, Camera observerCamera)
        {
            GameObject root = new GameObject("=== Rendu ===");

            VisualQuality quality = root.AddComponent<VisualQuality>();
            SerializedWiring.SetInt(quality, "_antiAliasing", 0);
            SerializedWiring.SetFloat(quality, "_shadowDistance", 60f);
            SerializedWiring.SetInt(quality, "_pixelLightCount", 10);

            // La brume et l'ambiante appartiennent au cycle jour / nuit : deux composants qui
            // écriraient dans RenderSettings au démarrage donneraient un résultat dépendant de
            // leur ordre d'exécution, c'est-à-dire indéterminé.
            SerializedWiring.SetBool(quality, "_enableFog", false);
            SerializedWiring.SetBool(quality, "_overrideAmbient", false);

            SunFlare flare = root.AddComponent<SunFlare>();
            SerializedWiring.SetObject(flare, "_sun", sun);
            SerializedWiring.SetObject(flare, "_camera", gameCamera);

            Material sky = BuildSky(gameCamera, observerCamera);

            TimeOfDay time = root.AddComponent<TimeOfDay>();
            SerializedWiring.SetObject(time, "_sun", sun);
            SerializedWiring.SetObject(time, "_moon", moon);
            SerializedWiring.SetObject(time, "_skyMaterial", sky);
            SerializedWiring.SetObject(time, "_cityRoot", street == null ? null : street.CityRoot);
            SerializedWiring.SetFloat(time, "_day", 0f);
            SerializedWiring.Verify(time, "_cityRoot");

            UberPostProcess gamePost = AddPostProcess(gameCamera);
            UberPostProcess observerPost = AddPostProcess(observerCamera);

            GraphicsDirector director = root.AddComponent<GraphicsDirector>();
            SetComponentArray(director, "_post", gamePost, observerPost);

            PlanarReflection reflection = street == null || street.Ground == null
                ? null
                : street.Ground.GetComponent<PlanarReflection>();

            SetComponentArray(director, "_reflections", reflection);
            SerializedWiring.SetObject(director, "_timeOfDay", time);
            SerializedWiring.Verify(director, "_timeOfDay");

            return director;
        }

        internal static UberPostProcess AddPostProcess(Camera camera)
        {
            if (camera == null) return null;

            // Sans HDR, la couleur est écrêtée à 1 AVANT le post-traitement : le seuil de
            // bloom ne distingue alors plus une enseigne d'un mur blanc. C'est le réglage
            // dont tout le reste dépend.
            camera.allowHDR = true;

            // Rendu DIFFERE : chaque lampe est calculee par pixel, sans plafond, avec ses
            // ombres. En rendu avant, Unity n'en calcule qu'une poignee par objet et bascule
            // les autres en approximation par sommet — sur un mur de quatre sommets, elles
            // n'eclairaient donc presque rien. Le MSAA n'existe pas en differe : c'est le FXAA
            // du post-traitement qui le remplace.
            camera.renderingPath = RenderingPath.DeferredShading;
            camera.allowMSAA = false;

            UberPostProcess post = camera.gameObject.AddComponent<UberPostProcess>();

            Material material = EditorBuildUtility.CreateOrUpdateEffectMaterial(
                "Assets/UberBagarre/Art/Materials", "M_PostTraitement", UberPostProcess.ShaderName);

            if (material != null) SerializedWiring.SetObject(post, "_material", material);

            return post;
        }

        /// <summary>
        /// Le ciel. Il est procédural et partagé par les deux caméras : le cycle jour / nuit
        /// en fabrique une copie au démarrage et fait varier son exposition et ses teintes,
        /// pour ne jamais modifier l'asset sur le disque.
        /// </summary>
        private static Material BuildSky(Camera gameCamera, Camera observerCamera)
        {
            Material sky = EditorBuildUtility.CreateOrUpdateProceduralSky(
                "Assets/UberBagarre/Art/Materials", "M_CielNuit",
                new Color(0.16f, 0.20f, 0.34f), new Color(0.05f, 0.05f, 0.08f), 1.9f, 0.30f, 0.02f);

            if (sky != null) RenderSettings.skybox = sky;

            ApplyCameraBackground(gameCamera, sky);
            ApplyCameraBackground(observerCamera, sky);

            return sky;
        }

        private static void ApplyCameraBackground(Camera camera, Material sky)
        {
            if (camera == null) return;

            camera.clearFlags = sky != null ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            camera.backgroundColor = new Color(0.035f, 0.04f, 0.062f);
        }

        // ------------------------------------------------------------------ sac de frappe

        /// <summary>
        /// Cible passive. Elle reste utile même avec un ennemi : on règle les dégâts, les
        /// fenêtres d'impact et le ressenti d'un coup sur une cible qui ne riposte pas.
        /// </summary>
        internal static void BuildPunchingBag(BuildMaterials materials)
        {
            GameObject root = new GameObject("SacDeFrappe");
            root.transform.position = new Vector3(6.2f, 0f, 5.1f);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Poteau", root.transform,
                new Vector3(0.95f, 1.3f, 0f), new Vector3(0.09f, 1.3f, 0.09f), materials.Wall, true);
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Potence", root.transform,
                new Vector3(0.48f, 2.55f, 0f), new Vector3(1.05f, 0.08f, 0.08f), materials.Wall, false);

            GameObject pivot = EditorBuildUtility.CreateEmpty("Pivot", root.transform, new Vector3(0f, 2.5f, 0f));
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "Chaine", pivot.transform,
                new Vector3(0f, -0.16f, 0f), new Vector3(0.03f, 0.16f, 0.03f), materials.Wall, false);

            GameObject bag = EditorBuildUtility.CreatePrimitive(PrimitiveType.Capsule, "Sac", pivot.transform,
                new Vector3(0f, -0.88f, 0f), new Vector3(0.34f, 0.56f, 0.34f), materials.Prop, true);

            HealthSystem health = root.AddComponent<HealthSystem>();
            SerializedWiring.SetFloat(health, "_maxHealth", 99999f);
            SerializedWiring.SetBool(health, "_logDamage", true);

            Hurtbox hurtbox = bag.AddComponent<Hurtbox>();
            SerializedWiring.SetObject(hurtbox, "_health", health);
            SerializedWiring.SetEnum(hurtbox, "_faction", (int)Faction.Neutral);
            SerializedWiring.SetEnum(hurtbox, "_zone", (int)HitZone.Body);

            PunchingBag swing = root.AddComponent<PunchingBag>();
            SerializedWiring.SetObject(swing, "_health", health);
            SerializedWiring.SetObject(swing, "_pivot", pivot.transform);
        }

        // ------------------------------------------------------------------ joueur

        internal static GameObject BuildPlayer(BuildMaterials materials, AttackLibraryBuilder.Library attacks,
            out Camera gameCamera, out Camera observerCamera)
        {
            GameObject playerGo = new GameObject("Player");
            playerGo.transform.position = new Vector3(0f, 0f, -SpawnDistance * 0.5f);

            CharacterController controller = playerGo.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = PlayerRadius;
            controller.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            controller.slopeLimit = 45f;
            controller.stepOffset = 0.32f;
            controller.skinWidth = 0.02f;
            controller.minMoveDistance = 0f;

            // Rig camera : un noeud = un effet, pour qu'ils ne s'ecrasent jamais entre eux.
            GameObject head = EditorBuildUtility.CreateEmpty("Head", playerGo.transform,
                new Vector3(0f, FighterBuilder.EyeHeight, 0f));
            GameObject cameraKnockdown = EditorBuildUtility.CreateEmpty("CameraKnockdown", head.transform, Vector3.zero);
            GameObject cameraBob = EditorBuildUtility.CreateEmpty("CameraBob", cameraKnockdown.transform, Vector3.zero);
            GameObject cameraShakeNode = EditorBuildUtility.CreateEmpty("CameraShake", cameraBob.transform, Vector3.zero);
            GameObject cameraPunchNode = EditorBuildUtility.CreateEmpty("CameraPunch", cameraShakeNode.transform, Vector3.zero);

            GameObject cameraGo = EditorBuildUtility.CreateEmpty("MainCamera", cameraPunchNode.transform, Vector3.zero);
            cameraGo.tag = "MainCamera";

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 300f;
            cameraGo.AddComponent<AudioListener>();

            // Camera d'observation : hors du rig du joueur, pour qu'elle ne herite ni du head bob
            // ni des secousses. Elle porte le MEME tag MainCamera, afin que Camera.main suive le
            // point de vue reellement allume — sinon tous les affichages du monde (barres de vie,
            // chiffres de degats) projetteraient depuis une camera eteinte.
            GameObject observerGo = EditorBuildUtility.CreateEmpty("CameraObservation", null, Vector3.zero);
            observerGo.tag = "MainCamera";

            Camera freeCamera = observerGo.AddComponent<Camera>();
            freeCamera.fieldOfView = 60f;
            freeCamera.nearClipPlane = 0.05f;
            freeCamera.farClipPlane = 400f;
            freeCamera.enabled = false;

            // Tout ce qui doit se coucher quand le joueur tombe vit sous ce noeud.
            GameObject tilt = EditorBuildUtility.CreateEmpty("Inclinaison", playerGo.transform, Vector3.zero);

            FighterBuilder.Result body = FighterBuilder.BuildBody(tilt.transform, playerGo.transform,
                FighterBuilder.Skin.Player(materials), false, Faction.Player, playerGo);

            GameObject aimAnchor = EditorBuildUtility.CreateEmpty("HandsAimAnchor", playerGo.transform, Vector3.zero);
            FirstPersonHands hands = aimAnchor.AddComponent<FirstPersonHands>();

            // --- systèmes de base
            InputBindings bindings = GetOrCreateInputBindings();

            PlayerInputReader input = playerGo.AddComponent<PlayerInputReader>();
            SerializedWiring.SetObject(input, "_bindings", bindings);

            PlayerMotor motor = playerGo.AddComponent<PlayerMotor>();
            SerializedWiring.SetObject(motor, "_input", input);
            SerializedWiring.SetObject(motor, "_eyeTransform", head.transform);

            PlayerLook look = playerGo.AddComponent<PlayerLook>();
            SerializedWiring.SetObject(look, "_input", input);
            SerializedWiring.SetObject(look, "_yawTransform", playerGo.transform);
            SerializedWiring.SetObject(look, "_pitchTransform", head.transform);

            CursorLockController cursor = playerGo.AddComponent<CursorLockController>();
            SerializedWiring.SetObject(cursor, "_input", input);

            HeadBob bob = cameraBob.AddComponent<HeadBob>();
            SerializedWiring.SetObject(bob, "_motor", motor);

            HandsAimAnchor anchor = aimAnchor.AddComponent<HandsAimAnchor>();
            SerializedWiring.SetObject(anchor, "_look", look);
            SerializedWiring.SetObject(anchor, "_positionSource", cameraGo.transform);

            SerializedWiring.SetObject(hands, "_poseSpace", aimAnchor.transform);
            SerializedWiring.SetObject(hands, "_leftArm", body.Rig.LeftArm);
            SerializedWiring.SetObject(hands, "_rightArm", body.Rig.RightArm);
            SerializedWiring.SetObject(hands, "_leftHand", body.Rig.LeftHand);
            SerializedWiring.SetObject(hands, "_rightHand", body.Rig.RightHand);
            SerializedWiring.SetObject(hands, "_locomotion", body.Locomotion);

            ObserverCamera observer = playerGo.AddComponent<ObserverCamera>();
            SerializedWiring.SetObject(observer, "_input", input);
            SerializedWiring.SetObject(observer, "_look", look);
            SerializedWiring.SetObject(observer, "_gameCamera", camera);
            SerializedWiring.SetObject(observer, "_observerCamera", freeCamera);
            SerializedWiring.SetObject(observer, "_target", head.transform);
            SerializedWiring.Verify(observer, "_observerCamera");

            PlayerAvatarDriver driver = playerGo.AddComponent<PlayerAvatarDriver>();
            SerializedWiring.SetObject(driver, "_input", input);
            SerializedWiring.SetObject(driver, "_motor", motor);
            SerializedWiring.SetObject(driver, "_hands", hands);
            SerializedWiring.SetObject(driver, "_locomotion", body.Locomotion);

            // --- combat
            Combatant combatant = AddCombatant(playerGo, Faction.Player, "Joueur", head.transform, 100f, 100f, 12f, 8f);

            GuardSystem guard = AddGuard(playerGo, combatant, 0.26f);
            AddHurtboxes(tilt, combatant, guard, Faction.Player, PlayerHeight);

            CameraShake shake = cameraShakeNode.AddComponent<CameraShake>();
            CameraPunch punch = cameraPunchNode.AddComponent<CameraPunch>();
            HitStop hitStop = playerGo.AddComponent<HitStop>();

            AttackExecutor executor = playerGo.AddComponent<AttackExecutor>();
            SerializedWiring.SetObject(executor, "_combatant", combatant);
            SerializedWiring.SetObject(executor, "_hands", hands);
            SerializedWiring.SetObject(executor, "_locomotion", body.Locomotion);
            SerializedWiring.SetObject(executor, "_cameraPunch", punch);

            // Le front du joueur vit sous la CAMERA : c'est son elan qui porte le coup de tete.
            Hitbox forehead = FighterBuilder.AddHeadHitbox(cameraGo.transform, new Vector3(0f, 0.02f, 0.12f),
                Faction.Player, playerGo);
            SerializedWiring.SetObject(executor, "_headHitbox", forehead);
            SerializedWiring.SetObject(executor, "_hitStop", hitStop);
            SerializedWiring.SetObject(executor, "_footPoseSpace", playerGo.transform);
            SerializedWiring.SetObject(executor, "_leftHitbox", body.LeftHitbox);
            SerializedWiring.SetObject(executor, "_rightHitbox", body.RightHitbox);
            SerializedWiring.SetObject(executor, "_leftFootHitbox", body.LeftFootHitbox);
            SerializedWiring.SetObject(executor, "_rightFootHitbox", body.RightFootHitbox);
            SerializedWiring.SetObject(executor, "_guard", guard);

            AddStunMeter(playerGo, combatant, executor);

            DodgeSystem dodge = playerGo.AddComponent<DodgeSystem>();
            SerializedWiring.SetObject(dodge, "_combatant", combatant);
            SerializedWiring.SetObject(dodge, "_stamina", combatant.Stamina);
            SerializedWiring.SetObject(dodge, "_impulseReceiver", motor);

            HitReaction reaction = playerGo.AddComponent<HitReaction>();
            SerializedWiring.SetObject(reaction, "_combatant", combatant);
            SerializedWiring.SetObject(reaction, "_health", combatant.Health);
            SerializedWiring.SetObject(reaction, "_locomotion", body.Locomotion);
            SerializedWiring.SetObject(reaction, "_executor", executor);
            SerializedWiring.SetObject(reaction, "_impulseReceiver", motor);

            // Physique locale des coups. Sur le joueur, la tete n'existe pas (vue premiere
            // personne) : c'est la CAMERA qui encaisse le choc, dans le sens ou il arrive.
            AddImpactPhysics(playerGo, combatant, body, punch, cameraGo.transform);
            playerGo.AddComponent<CharacterPusher>();

            // L'adversaire a toujours sa lumiere : entre deux lampadaires, une veste sombre
            // devenait une silhouette noire sur fond noir.
            OpponentLight opponentLight = playerGo.AddComponent<OpponentLight>();
            SerializedWiring.SetObject(opponentLight, "_camera", camera);
            SerializedWiring.SetObject(opponentLight, "_self", combatant);

            // Ramasser et lancer : le point de tenue est sous la camera, en bas a droite du
            // champ, la ou une main tiendrait une bouteille sans masquer la cible.
            GameObject holdPoint = EditorBuildUtility.CreateEmpty("PointDeTenue", cameraGo.transform,
                new Vector3(0.20f, -0.20f, 0.42f));

            PropHandler props = playerGo.AddComponent<PropHandler>();
            SerializedWiring.SetObject(props, "_input", input);
            SerializedWiring.SetObject(props, "_camera", camera);
            SerializedWiring.SetObject(props, "_holdPoint", holdPoint.transform);
            SerializedWiring.Verify(props, "_holdPoint");

            KnockdownSystem knockdown = AddKnockdown(playerGo, combatant, body, executor, motor, tilt.transform);
            SerializedWiring.SetObject(knockdown, "_cameraRoot", cameraKnockdown.transform);
            SerializedWiring.SetBool(knockdown, "_collapseOnDeath", true);

            PlayerCombat combat = playerGo.AddComponent<PlayerCombat>();
            SerializedWiring.SetObject(combat, "_input", input);
            SerializedWiring.SetObject(combat, "_executor", executor);
            SerializedWiring.SetObject(combat, "_motor", motor);
            SerializedWiring.SetObject(combat, "_dodge", dodge);
            SerializedWiring.SetObject(combat, "_combatant", combatant);
            SerializedWiring.SetObject(combat, "_guard", guard);
            SerializedWiring.SetObject(combat, "_knockdown", knockdown);
            SerializedWiring.SetObject(combat, "_straight", attacks.Straight);
            SerializedWiring.SetObject(combat, "_hook", attacks.Hook);
            SerializedWiring.SetObject(combat, "_uppercut", attacks.Uppercut);
            SerializedWiring.SetObject(combat, "_kick", attacks.Kick);
            SerializedWiring.SetObject(combat, "_lowKick", attacks.LowKick);
            SerializedWiring.SetObject(combat, "_shoulderCharge", attacks.Charge);
            SerializedWiring.SetObject(combat, "_dive", attacks.Dive);
            SerializedWiring.SetObject(combat, "_sweep", attacks.Sweep);
            SerializedWiring.SetObject(combat, "_stomp", attacks.Stomp);
            SerializedWiring.SetObject(combat, "_headbutt", attacks.Headbutt);
            SerializedWiring.SetObject(combat, "_shove", attacks.Shove);

            SerializedWiring.SetObject(driver, "_guard", guard);

            // Relecture immediate : si une de ces references est restee vide, le coup
            // correspondant sera inerte au lancement. Autant le savoir maintenant.
            SerializedWiring.Verify(combat, "_straight");
            SerializedWiring.Verify(combat, "_hook");
            SerializedWiring.Verify(combat, "_uppercut");
            SerializedWiring.Verify(combat, "_kick");
            SerializedWiring.Verify(combat, "_lowKick");
            SerializedWiring.Verify(combat, "_sweep");
            SerializedWiring.Verify(combat, "_stomp");

            // --- retours
            AudioSource audioSource = playerGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            ImpactAudio audio = playerGo.AddComponent<ImpactAudio>();

            CombatFeedbackRelay relay = playerGo.AddComponent<CombatFeedbackRelay>();
            SerializedWiring.SetObject(relay, "_executor", executor);
            SerializedWiring.SetObject(relay, "_combatant", combatant);
            SerializedWiring.SetObject(relay, "_guard", guard);
            SerializedWiring.SetObject(relay, "_cameraShake", shake);
            SerializedWiring.SetObject(relay, "_audio", audio);

            DamageVignette vignette = playerGo.AddComponent<DamageVignette>();
            SerializedWiring.SetObject(vignette, "_health", combatant.Health);

            ComboTracker combo = playerGo.AddComponent<ComboTracker>();
            SerializedWiring.SetObject(combo, "_owner", combatant);

            FloatingCombatText floatingText = playerGo.AddComponent<FloatingCombatText>();
            SerializedWiring.SetObject(floatingText, "_camera", camera);
            SerializedWiring.SetObject(floatingText, "_owner", combatant);
            SerializedWiring.SetObject(floatingText, "_combo", combo);

            // Pas de marques de coup sur le joueur, et c'est un choix : en vue premiere
            // personne, un bleu pose au point d'impact d'un coup a la tete se retrouve
            // exactement dans l'axe de la camera, a 20 cm de l'oeil. Il masquerait l'ecran.
            // Les consequences visibles sur soi passent par la vignette, pas par la peau.

            gameCamera = camera;
            observerCamera = freeCamera;

            return playerGo;
        }

        // ------------------------------------------------------------------ ennemi

        /// <summary>Ce qu'un combattant construit rend au reste du generateur.</summary>
        internal class FighterParts
        {
            public GameObject Go;
            public Combatant Combatant;
            public EnemyBrain Brain;
            public KnockdownSystem Knockdown;
            public FighterBuilder.Result Body;
            public GuardSystem Guard;
        }

        private static GameObject BuildEnemy(BuildMaterials materials, AttackLibraryBuilder.Library attacks)
        {
            return BuildFighter(materials, attacks, "Ennemi",
                new Vector3(0f, 0f, SpawnDistance * 0.5f), 180f,
                FighterBuilder.Skin.Enemy(materials), 90f, true).Go;
        }

        /// <summary>
        /// Construit un combattant complet, la ou on veut et habille comme on veut.
        ///
        /// Le prologue en a besoin de cinq : la cible et quatre figurants. Les figurants ne sont
        /// PAS des mannequins — ce sont les memes corps, avec le meme rig et le meme pilote
        /// d'animation, simplement prives de cerveau. C'est ce qui rend l'identification
        /// interessante : s'ils ne bougeaient pas comme la cible, on la reconnaitrait a sa
        /// respiration plutot qu'a sa veste.
        /// </summary>
        internal static FighterParts BuildFighter(BuildMaterials materials, AttackLibraryBuilder.Library attacks,
            string displayName, Vector3 position, float yaw, FighterBuilder.Skin skin, float health,
            bool withHealthBar)
        {
            GameObject enemyGo = new GameObject(displayName);
            enemyGo.transform.position = position;
            enemyGo.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

            CharacterController controller = enemyGo.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = 0.30f;
            controller.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.02f;

            GameObject tilt = EditorBuildUtility.CreateEmpty("Inclinaison", enemyGo.transform, Vector3.zero);

            FighterBuilder.Result body = FighterBuilder.BuildBody(tilt.transform, enemyGo.transform,
                skin, true, Faction.Enemy, enemyGo);

            // L'ennemi utilise le MEME composant de bras que le joueur : seul le repere change.
            // A hauteur d'yeux et face a l'avant, les poses de garde ecrites pour la premiere
            // personne fonctionnent telles quelles en troisieme personne.
            // L'ancrage des bras est SOUS le noeud d'inclinaison, et c'est la correction la plus
            // importante de cette passe : c'est lui qui donne aux bras leur cible d'IK. Reste-t-il
            // dehors, et l'adversaire couche au sol garde les deux bras tendus vers le ciel a
            // hauteur d'yeux. La barre de vie, qui s'accroche au meme repere, restait elle aussi
            // suspendue en l'air au-dessus d'un corps allonge.
            GameObject armsAnchor = EditorBuildUtility.CreateEmpty("ArmsAnchor", tilt.transform,
                new Vector3(0f, FighterBuilder.EyeHeight, 0f));
            FirstPersonHands arms = armsAnchor.AddComponent<FirstPersonHands>();
            SerializedWiring.SetObject(arms, "_poseSpace", armsAnchor.transform);
            SerializedWiring.SetObject(arms, "_leftArm", body.Rig.LeftArm);
            SerializedWiring.SetObject(arms, "_rightArm", body.Rig.RightArm);
            SerializedWiring.SetObject(arms, "_leftHand", body.Rig.LeftHand);
            SerializedWiring.SetObject(arms, "_rightHand", body.Rig.RightHand);
            SerializedWiring.SetObject(arms, "_locomotion", body.Locomotion);

            EnemyMotor motor = enemyGo.AddComponent<EnemyMotor>();

            Combatant combatant = AddCombatant(enemyGo, Faction.Enemy, displayName,
                armsAnchor.transform, health, 100f, 10f, 6f);

            // Fenetre de parade tres courte pour l'ennemi, et c'est un choix d'equilibrage :
            // il leve sa garde en reaction, donc avec la meme fenetre que le joueur il parerait
            // presque tous les coups — le joueur n'aurait jamais la main. Il BLOQUE, le joueur
            // PARE : le timing reste une competence du joueur.
            GuardSystem guard = AddGuard(enemyGo, combatant, 0.06f);
            GameObject hurtboxes = AddHurtboxes(tilt, combatant, guard, Faction.Enemy, PlayerHeight);

            AttackExecutor executor = enemyGo.AddComponent<AttackExecutor>();
            SerializedWiring.SetObject(executor, "_combatant", combatant);
            SerializedWiring.SetObject(executor, "_hands", arms);
            SerializedWiring.SetObject(executor, "_locomotion", body.Locomotion);
            SerializedWiring.SetObject(executor, "_footPoseSpace", enemyGo.transform);
            SerializedWiring.SetObject(executor, "_leftHitbox", body.LeftHitbox);
            SerializedWiring.SetObject(executor, "_rightHitbox", body.RightHitbox);
            SerializedWiring.SetObject(executor, "_leftFootHitbox", body.LeftFootHitbox);
            SerializedWiring.SetObject(executor, "_rightFootHitbox", body.RightFootHitbox);
            SerializedWiring.SetObject(executor, "_headHitbox", body.HeadHitbox);
            SerializedWiring.SetObject(executor, "_guard", guard);

            StunMeter stun = AddStunMeter(enemyGo, combatant, executor);

            DodgeSystem dodge = enemyGo.AddComponent<DodgeSystem>();
            SerializedWiring.SetObject(dodge, "_combatant", combatant);
            SerializedWiring.SetObject(dodge, "_stamina", combatant.Stamina);
            SerializedWiring.SetObject(dodge, "_impulseReceiver", motor);

            HitReaction reaction = enemyGo.AddComponent<HitReaction>();
            SerializedWiring.SetObject(reaction, "_combatant", combatant);
            SerializedWiring.SetObject(reaction, "_health", combatant.Health);
            SerializedWiring.SetObject(reaction, "_locomotion", body.Locomotion);
            SerializedWiring.SetObject(reaction, "_executor", executor);
            SerializedWiring.SetObject(reaction, "_impulseReceiver", motor);

            AddKnockdown(enemyGo, combatant, body, executor, motor, tilt.transform);

            EnemyAvatarDriver avatarDriver = enemyGo.AddComponent<EnemyAvatarDriver>();
            SerializedWiring.SetObject(avatarDriver, "_motor", motor);
            SerializedWiring.SetObject(avatarDriver, "_combatant", combatant);
            SerializedWiring.SetObject(avatarDriver, "_arms", arms);
            SerializedWiring.SetObject(avatarDriver, "_locomotion", body.Locomotion);
            SerializedWiring.SetObject(avatarDriver, "_guard", guard);

            EnemyBrain brain = enemyGo.AddComponent<EnemyBrain>();
            SerializedWiring.SetObject(brain, "_self", combatant);
            SerializedWiring.SetObject(brain, "_motor", motor);
            SerializedWiring.SetObject(brain, "_executor", executor);
            SerializedWiring.SetObject(brain, "_dodge", dodge);
            SerializedWiring.SetObject(brain, "_guard", guard);
            ConfigureEnemyAttacks(brain, attacks);

            AudioSource audioSource = enemyGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            ImpactAudio audio = enemyGo.AddComponent<ImpactAudio>();

            CombatFeedbackRelay relay = enemyGo.AddComponent<CombatFeedbackRelay>();
            SerializedWiring.SetObject(relay, "_executor", executor);
            SerializedWiring.SetObject(relay, "_combatant", combatant);
            SerializedWiring.SetObject(relay, "_guard", guard);
            SerializedWiring.SetObject(relay, "_audio", audio);

            if (withHealthBar)
            {
                WorldHealthBar bar = enemyGo.AddComponent<WorldHealthBar>();
                SerializedWiring.SetObject(bar, "_combatant", combatant);
                SerializedWiring.SetObject(bar, "_stun", stun);
            }

            AddBruises(enemyGo, combatant, body, materials);

            AddImpactPhysics(enemyGo, combatant, body, null, null);
            enemyGo.AddComponent<CharacterPusher>();

            // Le ragdoll est pose en DERNIER : il a besoin de connaitre tous les pilotes a couper,
            // donc tous doivent exister.
            DeathRagdoll ragdoll = enemyGo.AddComponent<DeathRagdoll>();
            SerializedWiring.SetObject(ragdoll, "_combatant", combatant);
            SerializedWiring.SetObject(ragdoll, "_rig", body.Rig);
            SerializedWiring.SetObject(ragdoll, "_controller", controller);
            SerializedWiring.SetObject(ragdoll, "_hurtboxRoot", hurtboxes);

            SetComponentArray(ragdoll, "_disableOnDeath",
                body.Locomotion, arms, avatarDriver, brain, motor, executor, reaction, dodge,
                enemyGo.GetComponent<KnockdownSystem>(), guard, stun);

            FighterParts parts = new FighterParts();
            parts.Go = enemyGo;
            parts.Combatant = combatant;
            parts.Brain = brain;
            parts.Knockdown = enemyGo.GetComponent<KnockdownSystem>();
            parts.Body = body;
            parts.Guard = guard;

            return parts;
        }

        /// <summary>
        /// Répertoire de coups de l'ennemi. Les distances se recouvrent volontairement :
        /// à portée moyenne il a le choix, ce qui rend ses enchaînements moins prévisibles
        /// sans avoir besoin d'une IA plus complexe.
        /// </summary>
        private static void ConfigureEnemyAttacks(EnemyBrain brain, AttackLibraryBuilder.Library library)
        {
            SerializedObject so = SerializedWiring.Open(brain);
            SerializedProperty attacks = so.FindProperty("_attacks");
            if (attacks == null) return;

            attacks.arraySize = 7;

            // Les coups de pied portent plus loin (la jambe mesure 20 cm de plus que le bras) et
            // se rechargent plus lentement : ils restent rares, donc ils restent des evenements.
            SetAttackOption(attacks.GetArrayElementAtIndex(0), library.Straight, 3f, 0f, 1.15f, 0.8f);
            SetAttackOption(attacks.GetArrayElementAtIndex(1), library.Hook, 1.4f, 0f, 1.05f, 2.2f);
            SetAttackOption(attacks.GetArrayElementAtIndex(2), library.Uppercut, 0.8f, 0f, 0.95f, 3.4f);
            SetAttackOption(attacks.GetArrayElementAtIndex(3), library.Kick, 0.9f, 0.75f, 1.35f, 4.5f);
            SetAttackOption(attacks.GetArrayElementAtIndex(4), library.LowKick, 1.1f, 0.55f, 1.25f, 3.8f);

            // Le coup de tete n'a de sens que colle au joueur : sa fenetre de distance est tres
            // courte, donc il sort surtout quand le joueur s'approche trop. C'est une punition
            // de placement, pas un coup de routine.
            SetAttackOption(attacks.GetArrayElementAtIndex(5), library.Headbutt, 0.9f, 0f, 0.85f, 5.5f);
            SetAttackOption(attacks.GetArrayElementAtIndex(6), library.Shove, 0.6f, 0f, 1.0f, 6f);

            SerializedProperty sequence = so.FindProperty("_scriptedSequence");
            if (sequence != null)
            {
                // Sequence de test prete a l'emploi, mais desactivee : cocher "Use Scripted
                // Sequence" suffit a obtenir un adversaire au timing parfaitement previsible.
                sequence.arraySize = 4;
                SetScriptedStep(sequence.GetArrayElementAtIndex(0), 2f, library.Straight);
                SetScriptedStep(sequence.GetArrayElementAtIndex(1), 1.5f, library.Hook);
                SetScriptedStep(sequence.GetArrayElementAtIndex(2), 3f, library.Uppercut);
                SetScriptedStep(sequence.GetArrayElementAtIndex(3), 3f, library.LowKick);
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetAttackOption(SerializedProperty element, AttackData attack,
            float weight, float minDistance, float maxDistance, float cooldown)
        {
            element.FindPropertyRelative("attack").objectReferenceValue = attack;
            element.FindPropertyRelative("weight").floatValue = weight;
            element.FindPropertyRelative("minDistance").floatValue = minDistance;
            element.FindPropertyRelative("maxDistance").floatValue = maxDistance;
            element.FindPropertyRelative("cooldown").floatValue = cooldown;
        }

        private static void SetScriptedStep(SerializedProperty element, float delay, AttackData attack)
        {
            element.FindPropertyRelative("delay").floatValue = delay;
            element.FindPropertyRelative("attack").objectReferenceValue = attack;
            element.FindPropertyRelative("holdPosition").boolValue = true;
        }

        // ------------------------------------------------------------------ pièces communes

        private static Combatant AddCombatant(GameObject go, Faction faction, string displayName,
            Transform aimOrigin, float health, float stamina, float strength, float defence)
        {
            HealthSystem healthSystem = go.AddComponent<HealthSystem>();
            SerializedWiring.SetFloat(healthSystem, "_maxHealth", health);

            StaminaSystem staminaSystem = go.AddComponent<StaminaSystem>();
            SerializedWiring.SetFloat(staminaSystem, "_maxStamina", stamina);

            // Fixes explicitement : une valeur par defaut modifiee dans le code ne met PAS a jour
            // un composant deja pose dans une scene existante. Sans ces deux lignes, regenerer la
            // scene laisserait l'ancien delai de regeneration en place.
            SerializedWiring.SetFloat(staminaSystem, "_regenPerSecond", 40f);
            SerializedWiring.SetFloat(staminaSystem, "_regenDelay", 0.3f);

            // Le joueur paie 60 % des couts : sa barre descendait deux fois trop vite pour un
            // combat nerveux. Les adversaires gardent le tarif plein.
            SerializedWiring.SetFloat(staminaSystem, "_costMultiplier", faction == Faction.Player ? 0.6f : 1f);

            CombatantStats stats = go.AddComponent<CombatantStats>();
            SerializedWiring.SetFloat(stats, "_fallbackMaxHealth", health);
            SerializedWiring.SetFloat(stats, "_fallbackMaxStamina", stamina);
            SerializedWiring.SetFloat(stats, "_fallbackStrength", strength);
            SerializedWiring.SetFloat(stats, "_fallbackDefense", defence);

            Combatant combatant = go.AddComponent<Combatant>();
            SerializedWiring.SetEnum(combatant, "_faction", (int)faction);
            SetString(combatant, "_displayName", displayName);
            SerializedWiring.SetObject(combatant, "_health", healthSystem);
            SerializedWiring.SetObject(combatant, "_stamina", staminaSystem);
            SerializedWiring.SetObject(combatant, "_stats", stats);
            SerializedWiring.SetObject(combatant, "_aimOrigin", aimOrigin);

            return combatant;
        }

        /// <summary>
        /// Trois zones touchables : jambes, corps, tête. Les colliders sont en trigger — ils
        /// servent uniquement à être touchés, jamais à bloquer un déplacement, ce dont s'occupe
        /// déjà le CharacterController.
        ///
        /// Les zones se RECOUVRENT volontairement, sur quelques centimètres. Un trou entre la
        /// cuisse et le bas du torse se traduirait par des coups qui ne font rien du tout, ce
        /// qui est le pire ressenti possible. Le recouvrement n'est pas ambigu pour autant :
        /// la hitbox retient la zone la plus proche du point d'impact.
        ///
        /// Les rayons sont plus généreux que le modèle, et c'est la norme en jeu de combat :
        /// une zone collée au personnage donne l'impression de le traverser. Ils peuvent l'être
        /// sans fausser les zones, puisque ce n'est plus la géométrie qui décide laquelle est
        /// touchée mais le réticule (voir <see cref="AimResolver"/>).
        ///
        /// Hauteurs pour un corps de 1,80 m :
        ///   jambes 0,00 → 0,92 (le bassin)   dégâts x 0,55
        ///   corps  0,74 → 1,54               dégâts x 1,00
        ///   tête   1,40 → 1,80               dégâts x 1,60
        /// </summary>
        private static GameObject AddHurtboxes(GameObject parent, Combatant combatant, GuardSystem guard,
            Faction faction, float height)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Hurtboxes", parent.transform, Vector3.zero);

            GameObject legBox = EditorBuildUtility.CreateEmpty("Hurtbox_Jambes", root.transform,
                new Vector3(0f, height * 0.256f, 0f));

            CapsuleCollider legCollider = legBox.AddComponent<CapsuleCollider>();
            legCollider.isTrigger = true;
            legCollider.radius = 0.32f;
            legCollider.height = height * 0.511f;

            // Les jambes encaissent peu : un coup bas ne met pas K.O. Son interet est le
            // desequilibre, pas les degats — et c'est la seule zone qui fait tomber.
            AddHurtbox(legBox, combatant, guard, faction, HitZone.Leg, 0.55f);

            GameObject bodyBox = EditorBuildUtility.CreateEmpty("Hurtbox_Corps", root.transform,
                new Vector3(0f, height * 0.633f, 0f));

            CapsuleCollider bodyCollider = bodyBox.AddComponent<CapsuleCollider>();
            bodyCollider.isTrigger = true;
            bodyCollider.radius = 0.38f;
            bodyCollider.height = height * 0.444f;

            AddHurtbox(bodyBox, combatant, guard, faction, HitZone.Body, 1f);

            GameObject headBox = EditorBuildUtility.CreateEmpty("Hurtbox_Tete", root.transform,
                new Vector3(0f, height * 0.889f, 0f));

            SphereCollider headCollider = headBox.AddComponent<SphereCollider>();
            headCollider.isTrigger = true;
            headCollider.radius = 0.18f;

            AddHurtbox(headBox, combatant, guard, faction, HitZone.Head, 1.6f);

            return root;
        }

        private static void AddHurtbox(GameObject go, Combatant combatant, GuardSystem guard,
            Faction faction, HitZone zone, float multiplier)
        {
            Hurtbox hurtbox = go.AddComponent<Hurtbox>();
            SerializedWiring.SetObject(hurtbox, "_health", combatant.Health);
            SerializedWiring.SetObject(hurtbox, "_stats", combatant.Stats);
            SerializedWiring.SetObject(hurtbox, "_guard", guard);
            SerializedWiring.SetEnum(hurtbox, "_faction", (int)faction);
            SerializedWiring.SetEnum(hurtbox, "_zone", (int)zone);
            SerializedWiring.SetFloat(hurtbox, "_damageMultiplier", multiplier);
        }

        /// <summary>
        /// Garde et parade. La fenêtre de parade est passée en paramètre parce qu'elle n'a pas
        /// la même valeur pour un humain et pour une IA — voir la note côté ennemi.
        /// </summary>
        private static GuardSystem AddGuard(GameObject go, Combatant combatant, float parryWindow)
        {
            GuardSystem guard = go.AddComponent<GuardSystem>();
            SerializedWiring.SetObject(guard, "_combatant", combatant);
            SerializedWiring.SetObject(guard, "_stamina", combatant.Stamina);
            SerializedWiring.SetFloat(guard, "_parryWindow", parryWindow);
            return guard;
        }

        /// <summary>
        /// Chute et relevé. Le transform qui bascule est le CORPS, pas la racine : la racine
        /// porte le CharacterController et la caméra, qui doivent rester debout — une capsule
        /// de collision couchée traverserait le sol et les murs.
        /// </summary>
        private static KnockdownSystem AddKnockdown(GameObject go, Combatant combatant,
            FighterBuilder.Result body, AttackExecutor executor, MonoBehaviour impulseReceiver,
            Transform tiltRoot)
        {
            KnockdownSystem knockdown = go.AddComponent<KnockdownSystem>();
            SerializedWiring.SetObject(knockdown, "_combatant", combatant);
            SerializedWiring.SetObject(knockdown, "_health", combatant.Health);
            SerializedWiring.SetObject(knockdown, "_locomotion", body.Locomotion);
            SerializedWiring.SetObject(knockdown, "_executor", executor);
            SerializedWiring.SetObject(knockdown, "_bodyRoot", tiltRoot);
            SerializedWiring.SetObject(knockdown, "_impulseReceiver", impulseReceiver);
            return knockdown;
        }

        /// <summary>
        /// Jauge d'étourdissement. Elle crée un second axe à côté des points de vie : la pression.
        /// </summary>
        private static StunMeter AddStunMeter(GameObject go, Combatant combatant, AttackExecutor executor)
        {
            StunMeter stun = go.AddComponent<StunMeter>();
            SerializedWiring.SetObject(stun, "_combatant", combatant);
            SerializedWiring.SetObject(stun, "_health", combatant.Health);
            SerializedWiring.SetObject(stun, "_executor", executor);
            return stun;
        }

        private static BruiseSystem AddBruises(GameObject go, Combatant combatant,
            FighterBuilder.Result body, BuildMaterials materials)
        {
            BruiseSystem bruises = go.AddComponent<BruiseSystem>();
            SerializedWiring.SetObject(bruises, "_combatant", combatant);
            SerializedWiring.SetObject(bruises, "_rig", body.Rig);
            SerializedWiring.SetObject(bruises, "_bruiseMaterial", materials.Bruise);
            return bruises;
        }

        internal static void WireHudAndDebug(GameObject player, GameObject enemy, AttackData testAttack)
        {
            Combatant playerCombatant = player.GetComponent<Combatant>();
            Combatant enemyCombatant = enemy.GetComponent<Combatant>();

            CombatHud hud = player.AddComponent<CombatHud>();
            SerializedWiring.SetObject(hud, "_playerHealth", playerCombatant.Health);
            SerializedWiring.SetObject(hud, "_playerStamina", playerCombatant.Stamina);
            SerializedWiring.SetObject(hud, "_player", playerCombatant);
            SerializedWiring.SetObject(hud, "_guard", player.GetComponent<GuardSystem>());
            SerializedWiring.SetObject(hud, "_relay", player.GetComponent<CombatFeedbackRelay>());
            SerializedWiring.SetObject(hud, "_combat", player.GetComponent<PlayerCombat>());
            SerializedWiring.SetObject(hud, "_stun", player.GetComponent<StunMeter>());
            SerializedWiring.SetObject(hud, "_executor", player.GetComponent<AttackExecutor>());

            CombatDebugOverlay overlay = player.AddComponent<CombatDebugOverlay>();
            SerializedWiring.SetObject(overlay, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(overlay, "_player", playerCombatant);
            SerializedWiring.SetObject(overlay, "_playerExecutor", player.GetComponent<AttackExecutor>());
            SerializedWiring.SetObject(overlay, "_playerDodge", player.GetComponent<DodgeSystem>());
            SerializedWiring.SetObject(overlay, "_enemy", enemyCombatant);
            SerializedWiring.SetObject(overlay, "_enemyExecutor", enemy.GetComponent<AttackExecutor>());
            SerializedWiring.SetObject(overlay, "_enemyBrain", enemy.GetComponent<EnemyBrain>());
            SerializedWiring.SetObject(overlay, "_testAttack", testAttack);
            SerializedWiring.SetObject(overlay, "_playerGuard", player.GetComponent<GuardSystem>());
            SerializedWiring.SetObject(overlay, "_enemyGuard", enemy.GetComponent<GuardSystem>());
            SerializedWiring.SetObject(overlay, "_enemyKnockdown", enemy.GetComponent<KnockdownSystem>());
            SerializedWiring.SetObject(overlay, "_playerCombat", player.GetComponent<PlayerCombat>());
        }

        internal static InputBindings GetOrCreateInputBindings()
        {
            // Le dossier peut ne pas exister : sur un projet neuf, c'est le PREMIER constructeur
            // lance qui le cree. Sans cette ligne, construire le jeu avant le bac a sable
            // echouait ici, en plein milieu, et la scene restait inachevee.
            EditorBuildUtility.EnsureFolder(SettingsFolder);
            string path = SettingsFolder + "/InputBindings.asset";
            InputBindings bindings = AssetDatabase.LoadAssetAtPath<InputBindings>(path);

            if (bindings == null)
            {
                bindings = ScriptableObject.CreateInstance<InputBindings>();
                AssetDatabase.CreateAsset(bindings, path);
                return bindings;
            }

            // Un asset cree avant l'ajout des touches de secours n'a pas ZQSD : on complete les
            // deplacements encore sur leurs valeurs d'origine, sans toucher a ce qui a ete
            // personnalise.
            bool migrated = false;
            migrated |= AddAlternate(ref bindings.moveForward, KeyCode.W, KeyCode.Z);
            migrated |= AddAlternate(ref bindings.moveLeft, KeyCode.A, KeyCode.Q);

            if (migrated)
            {
                EditorUtility.SetDirty(bindings);
                Debug.Log("[UberBagarre] InputBindings : ZQSD ajoute en secours de WASD (claviers AZERTY).", bindings);
            }

            return bindings;
        }

        private static bool AddAlternate(ref InputBinding binding, KeyCode original, KeyCode alternate)
        {
            if (binding.source != InputSource.Key || binding.key != original) return false;
            if (binding.alternateKey != KeyCode.None) return false;

            binding.alternateKey = alternate;
            return true;
        }

        // ------------------------------------------------------------------ spawn

        private static void BuildSpawnSystem(GameObject player, GameObject enemy, GraphicsDirector graphics)
        {
            // Le resetter a besoin du directeur de spawn et des deux combattants : il est donc
            // construit ici, une fois que les deux existent.

            GameObject root = new GameObject("=== Systemes ===");

            GameObject playerSpawnGo = EditorBuildUtility.CreateEmpty("PlayerSpawn", root.transform,
                new Vector3(0f, 0f, -SpawnDistance * 0.5f));
            SpawnPoint playerSpawn = playerSpawnGo.AddComponent<SpawnPoint>();
            SetString(playerSpawn, "_label", "Joueur");

            GameObject enemySpawnGo = EditorBuildUtility.CreateEmpty("EnemySpawn", root.transform,
                new Vector3(0f, 0f, SpawnDistance * 0.5f));
            enemySpawnGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            SpawnPoint enemySpawn = enemySpawnGo.AddComponent<SpawnPoint>();
            SetString(enemySpawn, "_label", "Ennemi");

            GameObject directorGo = EditorBuildUtility.CreateEmpty("SpawnDirector", root.transform, Vector3.zero);
            SpawnDirector director = directorGo.AddComponent<SpawnDirector>();

            SerializedObject so = SerializedWiring.Open(director);
            SerializedProperty requests = so.FindProperty("_requests");
            if (requests == null) return;

            requests.arraySize = 2;

            SerializedProperty playerEntry = requests.GetArrayElementAtIndex(0);
            playerEntry.FindPropertyRelative("label").stringValue = "Joueur";
            playerEntry.FindPropertyRelative("spawnPoint").objectReferenceValue = playerSpawn;
            playerEntry.FindPropertyRelative("prefab").objectReferenceValue = null;
            playerEntry.FindPropertyRelative("existingInstance").objectReferenceValue = player;
            playerEntry.FindPropertyRelative("faceTarget").objectReferenceValue = enemySpawn;

            SerializedProperty enemyEntry = requests.GetArrayElementAtIndex(1);
            enemyEntry.FindPropertyRelative("label").stringValue = "Ennemi";
            enemyEntry.FindPropertyRelative("spawnPoint").objectReferenceValue = enemySpawn;
            enemyEntry.FindPropertyRelative("prefab").objectReferenceValue = null;
            enemyEntry.FindPropertyRelative("existingInstance").objectReferenceValue = enemy;
            enemyEntry.FindPropertyRelative("faceTarget").objectReferenceValue = playerSpawn;

            so.ApplyModifiedPropertiesWithoutUndo();

            FightResetter resetter = directorGo.AddComponent<FightResetter>();
            SerializedWiring.SetObject(resetter, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(resetter, "_spawnDirector", director);

            SerializedObject resetterObject = SerializedWiring.Open(resetter);
            SerializedProperty combatants = resetterObject.FindProperty("_combatants");
            if (combatants != null)
            {
                combatants.arraySize = 2;
                combatants.GetArrayElementAtIndex(0).objectReferenceValue = player.GetComponent<Combatant>();
                combatants.GetArrayElementAtIndex(1).objectReferenceValue = enemy.GetComponent<Combatant>();
                resetterObject.ApplyModifiedPropertiesWithoutUndo();
            }

            // Le menu de reglage vit avec les systemes, pas sur le joueur : il regle la scene
            // entiere, et supprimer le joueur ne doit pas emporter l'outil qui sert a le regler.
            WaveDirector waves = directorGo.AddComponent<WaveDirector>();
            SerializedWiring.SetObject(waves, "_enemyTemplate", enemy);
            SerializedWiring.SetObject(waves, "_player", player.GetComponent<Combatant>());

            CombatStatistics statistics = directorGo.AddComponent<CombatStatistics>();
            SerializedWiring.SetObject(statistics, "_player", player.GetComponent<Combatant>());
            SerializedWiring.SetObject(statistics, "_executor", player.GetComponent<AttackExecutor>());
            SerializedWiring.SetObject(statistics, "_guard", player.GetComponent<GuardSystem>());
            SerializedWiring.SetObject(statistics, "_combo", player.GetComponent<ComboTracker>());

            SandboxMenu menu = directorGo.AddComponent<SandboxMenu>();
            SerializedWiring.SetObject(menu, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(menu, "_player", player.GetComponent<Combatant>());
            SerializedWiring.SetObject(menu, "_enemyTemplate", enemy);
            SerializedWiring.SetObject(menu, "_cursor", player.GetComponent<CursorLockController>());
            SerializedWiring.SetObject(menu, "_spawnDirector", director);
            SerializedWiring.SetObject(menu, "_playerCombat", player.GetComponent<PlayerCombat>());
            SerializedWiring.SetObject(menu, "_hitStop", player.GetComponent<HitStop>());
            SerializedWiring.SetObject(menu, "_waves", waves);
            SerializedWiring.SetObject(menu, "_statistics", statistics);
            SerializedWiring.SetObject(menu, "_graphics", graphics);

            DebugCheats cheats = directorGo.AddComponent<DebugCheats>();
            SerializedWiring.SetObject(cheats, "_player", player.GetComponent<Combatant>());
            SerializedWiring.SetObject(cheats, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(cheats, "_motor", player.GetComponent<PlayerMotor>());
            SerializedWiring.SetObject(cheats, "_camera", player.GetComponentInChildren<Camera>());
            SerializedWiring.SetObject(menu, "_cheats", cheats);

            SerializedWiring.Verify(menu, "_graphics");
            SerializedWiring.Verify(menu, "_enemyTemplate");
            SerializedWiring.Verify(menu, "_cursor");
        }

        /// <summary>
        /// Renseigne un tableau de composants sérialisé.
        ///
        /// Utilisé pour la liste des pilotes à couper à la mort. Une liste EXPLICITE plutôt qu'une
        /// recherche automatique : si un pilote oublié continue d'écrire sur les os, il écrase la
        /// physique à chaque image et le ragdoll reste figé debout — en silence, puisque rien
        /// n'est en erreur. Autant que la liste soit lisible ici, à côté de ce qui la remplit.
        /// </summary>
        /// <summary>
        /// Branche la physique d'impact par os. La caméra n'est renseignée que pour le joueur :
        /// l'adversaire, lui, n'a pas de point de vue à secouer.
        /// </summary>
        internal static BodyImpactPhysics AddImpactPhysics(GameObject owner, Combatant combatant,
            FighterBuilder.Result body, CameraPunch cameraPunch, Transform cameraReference)
        {
            BodyImpactPhysics physics = owner.AddComponent<BodyImpactPhysics>();
            SerializedWiring.SetObject(physics, "_rig", body.Rig);
            SerializedWiring.SetObject(physics, "_combatant", combatant);

            if (cameraPunch != null)
            {
                SerializedWiring.SetObject(physics, "_cameraPunch", cameraPunch);
                SerializedWiring.SetObject(physics, "_cameraReference", cameraReference);
            }

            SerializedWiring.Verify(physics, "_rig");
            return physics;
        }

        internal static void SetComponentArray(Object target, string fieldName, params Component[] values)
        {
            SerializedObject so = SerializedWiring.Open(target);
            SerializedProperty array = so.FindProperty(fieldName);

            if (array == null)
            {
                Debug.LogWarning("[UberBagarre] Champ tableau '" + fieldName + "' introuvable sur " +
                                 target.GetType().Name + ".", target);
                return;
            }

            List<Component> kept = new List<Component>();
            for (int i = 0; i < values.Length; i++)
            {
                if (values[i] != null) kept.Add(values[i]);
            }

            array.arraySize = kept.Count;
            for (int i = 0; i < kept.Count; i++)
            {
                array.GetArrayElementAtIndex(i).objectReferenceValue = kept[i];
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            SerializedObject so = SerializedWiring.Open(target);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null) return;

            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        internal static void RegisterSceneInBuildSettings()
        {
            RegisterSceneInBuildSettings(ScenePath);
        }

        /// <summary>
        /// Ajoute une scène aux Build Settings (en tête : la dernière construite est celle qu'une
        /// build lance). Le jeu passait auparavant par ici avec le chemin du bac à sable : sa
        /// propre scène n'était jamais enregistrée.
        /// </summary>
        internal static void RegisterSceneInBuildSettings(string scenePath)
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != scenePath) continue;

                if (!scenes[i].enabled) scenes[i] = new EditorBuildSettingsScene(scenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(scenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
