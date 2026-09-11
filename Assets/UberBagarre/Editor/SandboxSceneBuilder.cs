using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Core;
using UberBagarre.Player;
using UberBagarre.Feedback;
using UberBagarre.Sandbox;
using UberBagarre.View;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Génère la scène de test "CombatSandbox" : arène, lumière, rig joueur, points de spawn.
    ///
    /// Pourquoi un générateur plutôt qu'un fichier .unity livré tel quel :
    /// un .unity est un graphe d'objets référencés par GUID. Généré par Unity lui-même,
    /// il est forcément valide dans TA version et TON pipeline. Et comme la construction est
    /// du code lisible, tu vois exactement comment la scène est assemblée — et tu peux la
    /// régénérer après chaque phase sans rien remonter à la main.
    /// </summary>
    public static class SandboxSceneBuilder
    {
        public const string ScenesFolder = "Assets/UberBagarre/Scenes";
        public const string ScenePath = ScenesFolder + "/CombatSandbox.unity";

        private const string SettingsFolder = "Assets/UberBagarre/Settings";
        private const string MaterialsFolder = "Assets/UberBagarre/Art/Materials";
        private const string TexturesFolder = "Assets/UberBagarre/Art/Textures";

        // Dimensions de l'arène : assez grand pour reculer et tourner autour de l'ennemi,
        // assez petit pour qu'on ne perde jamais l'ennemi de vue.
        private const float ArenaSize = 22f;
        private const float WallHeight = 4f;
        private const float WallThickness = 0.5f;

        private const float PlayerEyeHeight = 1.62f;
        private const float PlayerHeight = 1.8f;
        private const float PlayerRadius = 0.3f;
        private const float SpawnDistance = 4.5f;

        // Proportions d'un corps de 1m80. Les os se chainent : bassin 0.92 + 0.13 + 0.17 + 0.20
        // place la nuque a 1.42, juste sous les yeux a 1.62.
        private const float PelvisHeight = 0.92f;
        private const float SpineOffset = 0.13f;
        private const float ChestOffset = 0.17f;
        private const float NeckOffset = 0.20f;

        private const float ShoulderOffsetX = 0.19f;
        private const float ShoulderOffsetY = 0.16f;
        private const float UpperArmLength = 0.30f;
        private const float ForearmLength = 0.26f;

        private const float HipOffsetX = 0.10f;
        private const float ThighLength = 0.44f;
        private const float ShinLength = 0.42f;

        [MenuItem("Uber Bagarre/2 - Construire la scene Combat Sandbox", false, 20)]
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
            EditorBuildUtility.EnsureFolder(MaterialsFolder);
            EditorBuildUtility.EnsureFolder(TexturesFolder);

            Materials materials = CreateMaterials();

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            BuildEnvironment(materials);

            GameObject player = BuildPlayerRig(materials);
            BuildPunchingBag(materials);
            SpawnPoint playerSpawn;
            SpawnPoint enemySpawn;
            BuildSpawnSystem(player, out playerSpawn, out enemySpawn);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved) RegisterSceneInBuildSettings();

            Selection.activeGameObject = player;

            Debug.Log("[UberBagarre] Scene Combat Sandbox generee.\n" +
                      "  Render pipeline detecte : " + EditorBuildUtility.ActivePipelineName() + "\n" +
                      "  Scene : " + ScenePath + "\n" +
                      "  Commandes : ZQSD/WASD, souris = visee, Maj = sprint, C = accroupi / glissade.\n" +
                      "  Combat : clic gauche = direct, Ctrl + clic = crochet, Alt + clic = uppercut.\n" +
                      "  Echap libere le curseur.\n" +
                      "  Appuie sur Play.");
        }

        // ------------------------------------------------------------------ matériaux

        private class Materials
        {
            public Material Floor;
            public Material Wall;
            public Material Prop;
            public Material SpawnMarker;
            public Material Skin;
            public Material Shirt;
            public Material Pants;
            public Material Shoe;
        }

        private static Materials CreateMaterials()
        {
            Texture2D checker = EditorBuildUtility.CreateOrUpdateCheckerTexture(
                TexturesFolder, "CheckerFloor", 256, 8,
                new Color(0.34f, 0.34f, 0.36f), new Color(0.27f, 0.27f, 0.29f));

            Materials materials = new Materials();

            materials.Floor = EditorBuildUtility.CreateOrUpdateMaterial(
                MaterialsFolder, "M_SandboxFloor", Color.white, 0.15f, 0f, checker, new Vector2(ArenaSize, ArenaSize));

            materials.Wall = EditorBuildUtility.CreateOrUpdateMaterial(
                MaterialsFolder, "M_SandboxWall", new Color(0.52f, 0.51f, 0.49f), 0.1f, 0f);

            materials.Prop = EditorBuildUtility.CreateOrUpdateMaterial(
                MaterialsFolder, "M_SandboxProp", new Color(0.42f, 0.33f, 0.26f), 0.2f, 0f);

            materials.SpawnMarker = EditorBuildUtility.CreateOrUpdateMaterial(
                MaterialsFolder, "M_SpawnMarker", new Color(0.2f, 0.55f, 0.75f), 0.3f, 0f);

            // Teintes sobres : le jeu vise un rendu credible, pas cartoon.
            materials.Skin = EditorBuildUtility.CreateOrUpdateMaterial(
                MaterialsFolder, "M_Skin", new Color(0.72f, 0.55f, 0.45f), 0.22f, 0f);

            materials.Shirt = EditorBuildUtility.CreateOrUpdateMaterial(
                MaterialsFolder, "M_Shirt", new Color(0.17f, 0.18f, 0.21f), 0.12f, 0f);

            materials.Pants = EditorBuildUtility.CreateOrUpdateMaterial(
                MaterialsFolder, "M_Pants", new Color(0.20f, 0.23f, 0.31f), 0.10f, 0f);

            materials.Shoe = EditorBuildUtility.CreateOrUpdateMaterial(
                MaterialsFolder, "M_Shoe", new Color(0.10f, 0.10f, 0.11f), 0.25f, 0f);

            return materials;
        }

        // ------------------------------------------------------------------ lumière

        private static void BuildLighting()
        {
            GameObject root = new GameObject("=== Eclairage ===");

            GameObject sunGo = EditorBuildUtility.CreateEmpty("Directional Light", root.transform, Vector3.zero);
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            sun.intensity = 1.15f;
            sun.color = new Color(1f, 0.97f, 0.91f);
            sun.shadows = LightShadows.Soft;

            // Seconde lumière très faible à l'opposé : évite des ombres totalement noires
            // sans avoir besoin de configurer de l'éclairage indirect (GI) dans un projet vierge.
            GameObject fillGo = EditorBuildUtility.CreateEmpty("Fill Light", root.transform, Vector3.zero);
            fillGo.transform.rotation = Quaternion.Euler(20f, 160f, 0f);

            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.28f;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fill.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ environnement

        private static void BuildEnvironment(Materials materials)
        {
            GameObject root = new GameObject("=== Environnement ===");

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Sol", root.transform,
                new Vector3(0f, -0.25f, 0f), new Vector3(ArenaSize, 0.5f, ArenaSize), materials.Floor, true);

            float half = ArenaSize * 0.5f;
            float offset = half - WallThickness * 0.5f;

            CreateWall(root.transform, "Mur Nord", new Vector3(0f, WallHeight * 0.5f, offset),
                new Vector3(ArenaSize, WallHeight, WallThickness), materials.Wall);
            CreateWall(root.transform, "Mur Sud", new Vector3(0f, WallHeight * 0.5f, -offset),
                new Vector3(ArenaSize, WallHeight, WallThickness), materials.Wall);
            CreateWall(root.transform, "Mur Est", new Vector3(offset, WallHeight * 0.5f, 0f),
                new Vector3(WallThickness, WallHeight, ArenaSize), materials.Wall);
            CreateWall(root.transform, "Mur Ouest", new Vector3(-offset, WallHeight * 0.5f, 0f),
                new Vector3(WallThickness, WallHeight, ArenaSize), materials.Wall);

            // Quelques repères : sans eux, impossible de juger sa vitesse ni sa distance.
            GameObject props = EditorBuildUtility.CreateEmpty("Reperes", root.transform, Vector3.zero);

            CreateProp(props.transform, "Caisse A", new Vector3(-6.5f, 0.6f, 6f), new Vector3(1.2f, 1.2f, 1.2f), materials.Prop);
            CreateProp(props.transform, "Caisse B", new Vector3(-5.2f, 0.35f, 7.4f), new Vector3(0.7f, 0.7f, 0.7f), materials.Prop);
            CreateProp(props.transform, "Caisse C", new Vector3(7.2f, 0.9f, -5.5f), new Vector3(1.8f, 1.8f, 1.8f), materials.Prop);
            CreateProp(props.transform, "Poteau", new Vector3(6.5f, 1.6f, 6.5f), new Vector3(0.4f, 3.2f, 0.4f), materials.Wall);
        }

        private static void CreateWall(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, name, parent, position, scale, material, true);
        }

        private static void CreateProp(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, name, parent, position, scale, material, true);
        }

        // ------------------------------------------------------------------ rig joueur

        /// <summary>
        /// Hiérarchie du joueur.
        ///
        /// Player                    lacet, déplacement, entrées
        ///  ├─ Body                  le vrai corps : bassin, buste, jambes, bras
        ///  │   └─ Pelvis → Spine → Chest → (Neck, épaules)
        ///  │       └─ hanches → cuisses → tibias → chevilles
        ///  ├─ Head                  tangage
        ///  │   └─ CameraBob → CameraShake → CameraPunch → MainCamera
        ///  └─ HandsAimAnchor        repère de visée des poings + composition des mains
        ///
        /// Le corps est enfant de Player et NON de la caméra : il suit donc le lacet
        /// mais pas le tangage. C'est ce qui permet de baisser les yeux et de voir
        /// son propre torse et ses jambes, au lieu d'un corps qui bascule avec le regard.
        /// </summary>
        private static GameObject BuildPlayerRig(Materials materials)
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

            GameObject head = EditorBuildUtility.CreateEmpty("Head", playerGo.transform, new Vector3(0f, PlayerEyeHeight, 0f));
            GameObject cameraBob = EditorBuildUtility.CreateEmpty("CameraBob", head.transform, Vector3.zero);
            GameObject cameraShake = EditorBuildUtility.CreateEmpty("CameraShake", cameraBob.transform, Vector3.zero);
            GameObject cameraPunch = EditorBuildUtility.CreateEmpty("CameraPunch", cameraShake.transform, Vector3.zero);

            GameObject cameraGo = EditorBuildUtility.CreateEmpty("MainCamera", cameraPunch.transform, Vector3.zero);
            cameraGo.tag = "MainCamera";

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 300f;
            cameraGo.AddComponent<AudioListener>();

            BodyRig bodyRig;
            ProceduralLocomotion locomotion;
            BuildBody(playerGo.transform, materials, out bodyRig, out locomotion);

            GameObject aimAnchor = EditorBuildUtility.CreateEmpty("HandsAimAnchor", playerGo.transform, Vector3.zero);
            FirstPersonHands hands = aimAnchor.AddComponent<FirstPersonHands>();

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
            SerializedWiring.SetObject(cursor, "_look", look);

            HeadBob bob = cameraBob.AddComponent<HeadBob>();
            SerializedWiring.SetObject(bob, "_motor", motor);

            HandsAimAnchor anchorComponent = aimAnchor.AddComponent<HandsAimAnchor>();
            SerializedWiring.SetObject(anchorComponent, "_look", look);
            SerializedWiring.SetObject(anchorComponent, "_positionSource", cameraGo.transform);

            SerializedWiring.SetObject(hands, "_poseSpace", aimAnchor.transform);
            SerializedWiring.SetObject(hands, "_leftArm", bodyRig.LeftArm);
            SerializedWiring.SetObject(hands, "_rightArm", bodyRig.RightArm);
            SerializedWiring.SetObject(hands, "_leftHand", bodyRig.LeftHand);
            SerializedWiring.SetObject(hands, "_rightHand", bodyRig.RightHand);
            SerializedWiring.SetObject(hands, "_locomotion", locomotion);

            PlayerAvatarDriver driver = playerGo.AddComponent<PlayerAvatarDriver>();
            SerializedWiring.SetObject(driver, "_input", input);
            SerializedWiring.SetObject(driver, "_motor", motor);
            SerializedWiring.SetObject(driver, "_hands", hands);
            SerializedWiring.SetObject(driver, "_locomotion", locomotion);

            BuildCombat(playerGo, cameraPunch, bodyRig, hands, locomotion, input, motor);

            return playerGo;
        }

        // ------------------------------------------------------------------ combat

        private static void BuildCombat(GameObject playerGo, GameObject cameraPunchNode, BodyRig bodyRig,
            FirstPersonHands hands, ProceduralLocomotion locomotion, PlayerInputReader input, PlayerMotor motor)
        {
            Hitbox leftHitbox = bodyRig.LeftArm != null && bodyRig.LeftArm.End != null
                ? bodyRig.LeftArm.End.GetComponent<Hitbox>() : null;
            Hitbox rightHitbox = bodyRig.RightArm != null && bodyRig.RightArm.End != null
                ? bodyRig.RightArm.End.GetComponent<Hitbox>() : null;

            if (leftHitbox != null) SerializedWiring.SetObject(leftHitbox, "_owner", playerGo);
            if (rightHitbox != null) SerializedWiring.SetObject(rightHitbox, "_owner", playerGo);

            CameraPunch cameraPunch = cameraPunchNode.AddComponent<CameraPunch>();
            HitStop hitStop = playerGo.AddComponent<HitStop>();

            AttackExecutor executor = playerGo.AddComponent<AttackExecutor>();
            SerializedWiring.SetObject(executor, "_hands", hands);
            SerializedWiring.SetObject(executor, "_locomotion", locomotion);
            SerializedWiring.SetObject(executor, "_cameraPunch", cameraPunch);
            SerializedWiring.SetObject(executor, "_hitStop", hitStop);
            SerializedWiring.SetObject(executor, "_leftHitbox", leftHitbox);
            SerializedWiring.SetObject(executor, "_rightHitbox", rightHitbox);

            AttackData straight, hook, uppercut;
            AttackLibraryBuilder.BuildAll(false, out straight, out hook, out uppercut);

            PlayerCombat combat = playerGo.AddComponent<PlayerCombat>();
            SerializedWiring.SetObject(combat, "_input", input);
            SerializedWiring.SetObject(combat, "_executor", executor);
            SerializedWiring.SetObject(combat, "_motor", motor);
            SerializedWiring.SetObject(combat, "_straight", straight);
            SerializedWiring.SetObject(combat, "_hook", hook);
            SerializedWiring.SetObject(combat, "_uppercut", uppercut);
        }

        // ------------------------------------------------------------------ cible d'entrainement

        /// <summary>
        /// Sac de frappe. Sans cible, impossible de savoir si la fenêtre d'impact fonctionne :
        /// on frappe dans le vide. C'est le banc de test de tout le système de combat,
        /// en attendant l'ennemi.
        /// </summary>
        private static void BuildPunchingBag(Materials materials)
        {
            GameObject root = new GameObject("SacDeFrappe");
            root.transform.position = new Vector3(0f, 0f, SpawnDistance * 0.5f);

            // Portique : un sac qui flotte sans attache ne se lit pas.
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
            SerializedWiring.SetFloat(health, "_maxHealth", 9999f);
            SerializedWiring.SetBool(health, "_logDamage", true);

            Hurtbox hurtbox = bag.AddComponent<Hurtbox>();
            SerializedWiring.SetObject(hurtbox, "_health", health);
            SerializedWiring.SetEnum(hurtbox, "_faction", (int)Faction.Enemy);
            SerializedWiring.SetEnum(hurtbox, "_zone", (int)HitZone.Body);

            PunchingBag swing = root.AddComponent<PunchingBag>();
            SerializedWiring.SetObject(swing, "_health", health);
            SerializedWiring.SetObject(swing, "_pivot", pivot.transform);
        }

        // ------------------------------------------------------------------ corps

        private static void BuildBody(Transform playerRoot, Materials materials,
            out BodyRig rig, out ProceduralLocomotion locomotion)
        {
            GameObject bodyGo = EditorBuildUtility.CreateEmpty("Body", playerRoot, Vector3.zero);

            GameObject pelvis = EditorBuildUtility.CreateEmpty("Pelvis", bodyGo.transform, new Vector3(0f, PelvisHeight, 0f));
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "PelvisVisual", pelvis.transform,
                new Vector3(0f, -0.02f, 0f), new Vector3(0.30f, 0.19f, 0.20f), materials.Pants, false);

            GameObject spine = EditorBuildUtility.CreateEmpty("Spine", pelvis.transform, new Vector3(0f, SpineOffset, 0f));
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "TorsoVisual", spine.transform,
                new Vector3(0f, 0.10f, 0f), new Vector3(0.33f, 0.28f, 0.21f), materials.Shirt, false);

            GameObject chest = EditorBuildUtility.CreateEmpty("Chest", spine.transform, new Vector3(0f, ChestOffset, 0f));
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "ChestVisual", chest.transform,
                new Vector3(0f, 0.08f, 0f), new Vector3(0.38f, 0.24f, 0.23f), materials.Shirt, false);

            GameObject neck = EditorBuildUtility.CreateEmpty("Neck", chest.transform, new Vector3(0f, NeckOffset, 0f));
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cylinder, "NeckVisual", neck.transform,
                new Vector3(0f, 0.02f, 0f), new Vector3(0.11f, 0.05f, 0.11f), materials.Skin, false);

            IkLimb leftArm = BuildArm(chest.transform, HandSide.Left, materials);
            IkLimb rightArm = BuildArm(chest.transform, HandSide.Right, materials);
            HandRig leftHand = leftArm.End.GetComponentInChildren<HandRig>();
            HandRig rightHand = rightArm.End.GetComponentInChildren<HandRig>();

            IkLimb leftLeg = BuildLeg(pelvis.transform, true, materials);
            IkLimb rightLeg = BuildLeg(pelvis.transform, false, materials);

            rig = bodyGo.AddComponent<BodyRig>();
            SerializedWiring.SetObject(rig, "_pelvis", pelvis.transform);
            SerializedWiring.SetObject(rig, "_spine", spine.transform);
            SerializedWiring.SetObject(rig, "_chest", chest.transform);
            SerializedWiring.SetObject(rig, "_neck", neck.transform);
            SerializedWiring.SetObject(rig, "_leftLeg", leftLeg);
            SerializedWiring.SetObject(rig, "_rightLeg", rightLeg);
            SerializedWiring.SetObject(rig, "_leftArm", leftArm);
            SerializedWiring.SetObject(rig, "_rightArm", rightArm);
            SerializedWiring.SetObject(rig, "_leftHand", leftHand);
            SerializedWiring.SetObject(rig, "_rightHand", rightHand);

            locomotion = bodyGo.AddComponent<ProceduralLocomotion>();
            SerializedWiring.SetObject(locomotion, "_rig", rig);
            SerializedWiring.SetObject(locomotion, "_root", playerRoot);
        }

        private static IkLimb BuildArm(Transform chest, HandSide side, Materials materials)
        {
            bool isLeft = side == HandSide.Left;
            float sign = isLeft ? -1f : 1f;
            string prefix = isLeft ? "Left" : "Right";

            GameObject shoulder = EditorBuildUtility.CreateEmpty(prefix + "Shoulder", chest,
                new Vector3(sign * ShoulderOffsetX, ShoulderOffsetY, 0f));

            GameObject upperArm = EditorBuildUtility.CreateEmpty(prefix + "UpperArm", shoulder.transform, Vector3.zero);
            GameObject forearm = EditorBuildUtility.CreateEmpty(prefix + "Forearm", upperArm.transform,
                new Vector3(0f, 0f, UpperArmLength));
            GameObject wrist = EditorBuildUtility.CreateEmpty(prefix + "Wrist", forearm.transform,
                new Vector3(0f, 0f, ForearmLength));

            CreateBoneVisual(upperArm.transform, prefix + "UpperArmVisual", UpperArmLength, 0.058f, materials.Shirt);
            CreateBoneVisual(forearm.transform, prefix + "ForearmVisual", ForearmLength, 0.047f, materials.Skin);
            Transform knuckles = BuildHand(wrist.transform, side, materials);

            // La detection part des articulations, pas du poignet : c'est la surface qui frappe.
            Hitbox hitbox = wrist.AddComponent<Hitbox>();
            SerializedWiring.SetObject(hitbox, "_origin", knuckles);
            SerializedWiring.SetEnum(hitbox, "_ownerFaction", (int)Faction.Player);

            IkLimb limb = shoulder.AddComponent<IkLimb>();
            SerializedWiring.SetObject(limb, "_upper", upperArm.transform);
            SerializedWiring.SetObject(limb, "_lower", forearm.transform);
            SerializedWiring.SetObject(limb, "_end", wrist.transform);
            SerializedWiring.SetFloat(limb, "_upperLength", UpperArmLength);
            SerializedWiring.SetFloat(limb, "_lowerLength", ForearmLength);

            // Coude vers le bas, legerement en arriere et vers l'exterieur : silhouette de garde.
            SerializedWiring.SetVector3(limb, "_poleDirection", new Vector3(sign * 0.25f, -1f, -0.35f));

            return limb;
        }

        private static IkLimb BuildLeg(Transform pelvis, bool isLeft, Materials materials)
        {
            float sign = isLeft ? -1f : 1f;
            string prefix = isLeft ? "Left" : "Right";

            GameObject hip = EditorBuildUtility.CreateEmpty(prefix + "Hip", pelvis,
                new Vector3(sign * HipOffsetX, -0.02f, 0f));

            GameObject thigh = EditorBuildUtility.CreateEmpty(prefix + "Thigh", hip.transform, Vector3.zero);
            GameObject shin = EditorBuildUtility.CreateEmpty(prefix + "Shin", thigh.transform,
                new Vector3(0f, 0f, ThighLength));
            GameObject ankle = EditorBuildUtility.CreateEmpty(prefix + "Ankle", shin.transform,
                new Vector3(0f, 0f, ShinLength));

            CreateBoneVisual(thigh.transform, prefix + "ThighVisual", ThighLength, 0.075f, materials.Pants);
            CreateBoneVisual(shin.transform, prefix + "ShinVisual", ShinLength, 0.060f, materials.Pants);

            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, prefix + "FootVisual", ankle.transform,
                new Vector3(0f, -0.045f, 0.048f), new Vector3(0.10f, 0.06f, 0.25f), materials.Shoe, false);

            IkLimb limb = hip.AddComponent<IkLimb>();
            SerializedWiring.SetObject(limb, "_upper", thigh.transform);
            SerializedWiring.SetObject(limb, "_lower", shin.transform);
            SerializedWiring.SetObject(limb, "_end", ankle.transform);
            SerializedWiring.SetFloat(limb, "_upperLength", ThighLength);
            SerializedWiring.SetFloat(limb, "_lowerLength", ShinLength);

            // Le genou plie vers l'avant : c'est tout ce qui distingue une jambe d'un bras.
            SerializedWiring.SetVector3(limb, "_poleDirection", new Vector3(sign * 0.15f, 0.35f, 1f));

            return limb;
        }

        // ------------------------------------------------------------------ mains

        private struct FingerSpec
        {
            public string Name;
            public Vector3 Base;
            public Vector3 BaseEuler;
            public float Proximal;
            public float Middle;
            public float Distal;
            public float Radius;
            public float ProximalCurl;
            public float MiddleCurl;
            public float DistalCurl;
            public float CloseDelay;
            public Vector3 CurlAxis;
        }

        /// <summary>
        /// Construit une main articulée : paume + 5 doigts de 3 phalanges, aux longueurs
        /// et aux temps de fermeture différents. Le poing est donc le résultat d'une vraie
        /// fermeture de doigts, et non un cube posé au bout du bras.
        /// </summary>
        private static Transform BuildHand(Transform wrist, HandSide side, Materials materials)
        {
            float sign = side == HandSide.Left ? -1f : 1f;
            string prefix = side == HandSide.Left ? "Left" : "Right";

            GameObject palm = EditorBuildUtility.CreateEmpty(prefix + "Palm", wrist, new Vector3(0f, 0f, 0.040f));

            // Un poing est un BLOC : presque aussi epais que large. Une paume fine donnait une
            // planche, avec les doigts replies qui pendaient dessous comme des orteils.
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, prefix + "PalmVisual", palm.transform,
                Vector3.zero, new Vector3(0.085f, 0.050f, 0.072f), materials.Skin, false);

            // Crete des articulations : c'est ce qui fait lire la forme comme un poing,
            // et c'est aussi la surface qui frappe.
            EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, prefix + "KnuckleVisual", palm.transform,
                new Vector3(0f, -0.002f, 0.040f), new Vector3(0.086f, 0.044f, 0.024f), materials.Skin, false);

            GameObject knuckles = EditorBuildUtility.CreateEmpty(prefix + "Knuckles", palm.transform,
                new Vector3(0f, -0.008f, 0.052f));

            FingerSpec[] specs = new FingerSpec[]
            {
                MakeFinger("Index",       new Vector3(sign * 0.029f, -0.010f, 0.034f), Vector3.zero, 0.038f, 0.024f, 0.018f, 0.0105f, 78f,  96f, 62f, 0.15f, Vector3.right),
                MakeFinger("Majeur",      new Vector3(sign * 0.010f, -0.008f, 0.036f), Vector3.zero, 0.042f, 0.026f, 0.019f, 0.0110f, 82f,  98f, 64f, 0.08f, Vector3.right),
                MakeFinger("Annulaire",   new Vector3(sign * -0.010f, -0.010f, 0.034f), Vector3.zero, 0.039f, 0.025f, 0.018f, 0.0100f, 85f, 100f, 66f, 0.03f, Vector3.right),
                MakeFinger("Auriculaire", new Vector3(sign * -0.028f, -0.013f, 0.030f), Vector3.zero, 0.032f, 0.021f, 0.016f, 0.0088f, 88f, 102f, 68f, 0f,    Vector3.right),
                MakeFinger("Pouce",       new Vector3(sign * 0.040f, -0.014f, 0.004f), new Vector3(6f, -sign * 38f, -sign * 50f), 0.034f, 0.026f, 0.019f, 0.0125f, 42f, 48f, 32f, 0.35f, Vector3.right)
            };

            Transform[,] joints = new Transform[specs.Length, 3];

            for (int i = 0; i < specs.Length; i++)
            {
                FingerSpec spec = specs[i];

                GameObject proximal = EditorBuildUtility.CreateEmpty(prefix + spec.Name + "1", palm.transform, spec.Base);
                proximal.transform.localRotation = Quaternion.Euler(spec.BaseEuler);

                GameObject middle = EditorBuildUtility.CreateEmpty(prefix + spec.Name + "2", proximal.transform,
                    new Vector3(0f, 0f, spec.Proximal));
                GameObject distal = EditorBuildUtility.CreateEmpty(prefix + spec.Name + "3", middle.transform,
                    new Vector3(0f, 0f, spec.Middle));

                CreateBoneVisual(proximal.transform, prefix + spec.Name + "1Visual", spec.Proximal, spec.Radius, materials.Skin);
                CreateBoneVisual(middle.transform, prefix + spec.Name + "2Visual", spec.Middle, spec.Radius * 0.92f, materials.Skin);
                CreateBoneVisual(distal.transform, prefix + spec.Name + "3Visual", spec.Distal, spec.Radius * 0.85f, materials.Skin);

                joints[i, 0] = proximal.transform;
                joints[i, 1] = middle.transform;
                joints[i, 2] = distal.transform;
            }

            HandRig handRig = wrist.gameObject.AddComponent<HandRig>();
            SerializedWiring.SetEnum(handRig, "_side", side == HandSide.Left ? 0 : 1);
            SerializedWiring.SetObject(handRig, "_palm", palm.transform);

            SerializedObject so = SerializedWiring.Open(handRig);
            SerializedProperty fingers = so.FindProperty("_fingers");
            if (fingers != null)
            {
                fingers.arraySize = specs.Length;

                for (int i = 0; i < specs.Length; i++)
                {
                    SerializedProperty element = fingers.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("name").stringValue = specs[i].Name;
                    element.FindPropertyRelative("proximal").objectReferenceValue = joints[i, 0];
                    element.FindPropertyRelative("middle").objectReferenceValue = joints[i, 1];
                    element.FindPropertyRelative("distal").objectReferenceValue = joints[i, 2];
                    element.FindPropertyRelative("proximalCurl").floatValue = specs[i].ProximalCurl;
                    element.FindPropertyRelative("middleCurl").floatValue = specs[i].MiddleCurl;
                    element.FindPropertyRelative("distalCurl").floatValue = specs[i].DistalCurl;
                    element.FindPropertyRelative("curlAxis").vector3Value = specs[i].CurlAxis;
                    element.FindPropertyRelative("closeDelay").floatValue = specs[i].CloseDelay;
                    element.FindPropertyRelative("curlScale").floatValue = 1f;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return knuckles.transform;
        }

        private static FingerSpec MakeFinger(string name, Vector3 basePosition, Vector3 baseEuler,
            float proximal, float middle, float distal, float radius,
            float proximalCurl, float middleCurl, float distalCurl, float closeDelay, Vector3 curlAxis)
        {
            FingerSpec spec = new FingerSpec();
            spec.Name = name;
            spec.Base = basePosition;
            spec.BaseEuler = baseEuler;
            spec.Proximal = proximal;
            spec.Middle = middle;
            spec.Distal = distal;
            spec.Radius = radius;
            spec.ProximalCurl = proximalCurl;
            spec.MiddleCurl = middleCurl;
            spec.DistalCurl = distalCurl;
            spec.CloseDelay = closeDelay;
            spec.CurlAxis = curlAxis;
            return spec;
        }

        /// <summary>
        /// Segment d'os visuel. La capsule d'Unity est orientée sur son axe Y et mesure 2 unités :
        /// on la couche sur +Z et on la met à l'échelle pour couvrir exactement la longueur de l'os.
        /// </summary>
        private static void CreateBoneVisual(Transform bone, string name, float length, float radius, Material material)
        {
            GameObject visual = EditorBuildUtility.CreatePrimitive(PrimitiveType.Capsule, name, bone,
                new Vector3(0f, 0f, length * 0.5f),
                new Vector3(radius * 2f, length * 0.5f, radius * 2f), material, false);

            visual.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
        }

        private static InputBindings GetOrCreateInputBindings()
        {
            string path = SettingsFolder + "/InputBindings.asset";
            InputBindings bindings = AssetDatabase.LoadAssetAtPath<InputBindings>(path);

            if (bindings == null)
            {
                bindings = ScriptableObject.CreateInstance<InputBindings>();
                AssetDatabase.CreateAsset(bindings, path);
            }

            return bindings;
        }

        // ------------------------------------------------------------------ spawn

        private static void BuildSpawnSystem(GameObject player, out SpawnPoint playerSpawn, out SpawnPoint enemySpawn)
        {
            GameObject root = new GameObject("=== Systemes ===");

            GameObject playerSpawnGo = EditorBuildUtility.CreateEmpty("PlayerSpawn", root.transform,
                new Vector3(0f, 0f, -SpawnDistance * 0.5f));
            playerSpawn = playerSpawnGo.AddComponent<SpawnPoint>();
            SetString(playerSpawn, "_label", "Joueur");

            GameObject enemySpawnGo = EditorBuildUtility.CreateEmpty("EnemySpawn", root.transform,
                new Vector3(0f, 0f, SpawnDistance * 0.5f));
            enemySpawnGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            enemySpawn = enemySpawnGo.AddComponent<SpawnPoint>();
            SetString(enemySpawn, "_label", "Ennemi");

            GameObject directorGo = EditorBuildUtility.CreateEmpty("SpawnDirector", root.transform, Vector3.zero);
            SpawnDirector director = directorGo.AddComponent<SpawnDirector>();

            SerializedObject so = SerializedWiring.Open(director);
            SerializedProperty requests = so.FindProperty("_requests");
            if (requests != null)
            {
                requests.arraySize = 1;
                SerializedProperty entry = requests.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative("label").stringValue = "Joueur";
                entry.FindPropertyRelative("spawnPoint").objectReferenceValue = playerSpawn;
                entry.FindPropertyRelative("prefab").objectReferenceValue = null;
                entry.FindPropertyRelative("existingInstance").objectReferenceValue = player;
                entry.FindPropertyRelative("faceTarget").objectReferenceValue = enemySpawn;
                so.ApplyModifiedPropertiesWithoutUndo();
            }
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            SerializedObject so = SerializedWiring.Open(target);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null) return;

            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ build settings

        private static void RegisterSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path == ScenePath)
                {
                    if (!scenes[i].enabled) scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                    EditorBuildSettings.scenes = scenes.ToArray();
                    return;
                }
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
