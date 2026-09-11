using System.Collections.Generic;
using UberBagarre.Core;
using UberBagarre.Player;
using UberBagarre.Sandbox;
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

            GameObject player = BuildPlayerRig();
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
                      "  Commandes : ZQSD/WASD = deplacement, souris = visee, Espace = saut, Echap = liberer le curseur.\n" +
                      "  Appuie sur Play.");
        }

        // ------------------------------------------------------------------ matériaux

        private class Materials
        {
            public Material Floor;
            public Material Wall;
            public Material Prop;
            public Material SpawnMarker;
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
        /// Hiérarchie du joueur. Chaque nœud a UNE responsabilité, ce qui permet d'empiler
        /// les effets de caméra des phases suivantes sans qu'ils s'écrasent entre eux :
        ///
        /// Player            lacet (gauche/droite) + deplacement + entrees
        ///  +- Head          tangage (haut/bas)
        ///      +- CameraBob      oscillation de marche
        ///          +- CameraShake    secousses d'impact        (phase 7)
        ///              +- CameraPunch    recul des coups portes (phase 5)
        ///                  +- MainCamera
        ///                      +- HandsRig   mains FPS          (phase 2)
        /// </summary>
        private static GameObject BuildPlayerRig()
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
            // Un near clip serré est indispensable en FPS : sinon les mains sont coupées par le plan proche.
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 300f;
            cameraGo.AddComponent<AudioListener>();

            // Emplacement réservé aux mains FPS (phase 2) : la hiérarchie est déjà correcte,
            // on n'aura donc pas à déplacer quoi que ce soit ensuite.
            EditorBuildUtility.CreateEmpty("HandsRig", cameraGo.transform, Vector3.zero);

            InputBindings bindings = GetOrCreateInputBindings();

            PlayerInputReader input = playerGo.AddComponent<PlayerInputReader>();
            SerializedWiring.SetObject(input, "_bindings", bindings);

            PlayerMotor motor = playerGo.AddComponent<PlayerMotor>();
            SerializedWiring.SetObject(motor, "_input", input);

            PlayerLook look = playerGo.AddComponent<PlayerLook>();
            SerializedWiring.SetObject(look, "_input", input);
            SerializedWiring.SetObject(look, "_yawTransform", playerGo.transform);
            SerializedWiring.SetObject(look, "_pitchTransform", head.transform);

            CursorLockController cursor = playerGo.AddComponent<CursorLockController>();
            SerializedWiring.SetObject(cursor, "_input", input);
            SerializedWiring.SetObject(cursor, "_look", look);

            HeadBob bob = cameraBob.AddComponent<HeadBob>();
            SerializedWiring.SetObject(bob, "_motor", motor);

            return playerGo;
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
