using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Core;
using UberBagarre.Enemy;
using UberBagarre.Feedback;
using UberBagarre.Player;
using UberBagarre.Sandbox;
using UberBagarre.UI;
using UberBagarre.View;
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

        private const string SettingsFolder = "Assets/UberBagarre/Settings";

        private const float ArenaSize = 26f;
        private const float RingRadius = 3.4f;

        private const float PlayerHeight = 1.8f;
        private const float PlayerRadius = 0.28f;
        private const float SpawnDistance = 5f;

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
            ProceduralMeshFactory.EnsureLibrary();

            BuildMaterials materials = BuildMaterials.CreateAll(ArenaSize);

            AttackData straight, hook, uppercut;
            AttackLibraryBuilder.BuildAll(false, out straight, out hook, out uppercut);

            Scene scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);

            BuildLighting();
            ArenaBuilder.Build(ArenaSize, RingRadius);
            BuildPunchingBag(materials);

            GameObject player = BuildPlayer(materials, straight, hook, uppercut);
            GameObject enemy = BuildEnemy(materials, straight, hook, uppercut);

            WireHudAndDebug(player, enemy, straight);
            BuildSpawnSystem(player, enemy);

            EditorSceneManager.MarkSceneDirty(scene);
            bool saved = EditorSceneManager.SaveScene(scene, ScenePath);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            if (saved) RegisterSceneInBuildSettings();

            Selection.activeGameObject = player;

            Debug.Log("[UberBagarre] Scene Combat Sandbox generee.\n" +
                      "  Render pipeline : " + EditorBuildUtility.ActivePipelineName() + "\n" +
                      "  Deplacement  : WASD/ZQSD, souris = visee, Maj = sprint, C = accroupi / glissade\n" +
                      "  Combat       : clic gauche = direct, clic DROIT = crochet, clic MOLETTE = uppercut\n" +
                      "  Defense      : Ctrl gauche = garde, Alt gauche = esquive (direction = WASD)\n" +
                      "  Debug        : F1 = overlay, R = relancer le combat, Echap = liberer le curseur\n" +
                      "  Appuie sur Play.");
        }

        // ------------------------------------------------------------------ décor

        private static void BuildLighting()
        {
            GameObject root = new GameObject("=== Eclairage ===");

            GameObject sunGo = EditorBuildUtility.CreateEmpty("Directional Light", root.transform, Vector3.zero);
            sunGo.transform.rotation = Quaternion.Euler(48f, -35f, 0f);

            Light sun = sunGo.AddComponent<Light>();
            sun.type = LightType.Directional;
            // Lumiere de fin de journee, volontairement faible : ce sont les lampadaires qui
            // eclairent le combat, ce qui donne du contraste et des ombres portees lisibles.
            sun.intensity = 0.62f;
            sun.color = new Color(0.78f, 0.82f, 0.95f);
            sun.shadows = LightShadows.Soft;

            // Lumiere d'appoint faible a l'oppose : evite des ombres totalement noires sans
            // avoir a configurer d'eclairage indirect dans un projet vierge.
            GameObject fillGo = EditorBuildUtility.CreateEmpty("Fill Light", root.transform, Vector3.zero);
            fillGo.transform.rotation = Quaternion.Euler(20f, 160f, 0f);

            Light fill = fillGo.AddComponent<Light>();
            fill.type = LightType.Directional;
            fill.intensity = 0.18f;
            fill.color = new Color(0.75f, 0.82f, 1f);
            fill.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------ sac de frappe

        /// <summary>
        /// Cible passive. Elle reste utile même avec un ennemi : on règle les dégâts, les
        /// fenêtres d'impact et le ressenti d'un coup sur une cible qui ne riposte pas.
        /// </summary>
        private static void BuildPunchingBag(BuildMaterials materials)
        {
            GameObject root = new GameObject("SacDeFrappe");
            root.transform.position = new Vector3(-3.6f, 0f, 3.4f);

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

        private static GameObject BuildPlayer(BuildMaterials materials, AttackData straight, AttackData hook, AttackData uppercut)
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
            GameObject cameraBob = EditorBuildUtility.CreateEmpty("CameraBob", head.transform, Vector3.zero);
            GameObject cameraShakeNode = EditorBuildUtility.CreateEmpty("CameraShake", cameraBob.transform, Vector3.zero);
            GameObject cameraPunchNode = EditorBuildUtility.CreateEmpty("CameraPunch", cameraShakeNode.transform, Vector3.zero);

            GameObject cameraGo = EditorBuildUtility.CreateEmpty("MainCamera", cameraPunchNode.transform, Vector3.zero);
            cameraGo.tag = "MainCamera";

            Camera camera = cameraGo.AddComponent<Camera>();
            camera.fieldOfView = 75f;
            camera.nearClipPlane = 0.04f;
            camera.farClipPlane = 300f;
            cameraGo.AddComponent<AudioListener>();

            FighterBuilder.Result body = FighterBuilder.BuildBody(playerGo.transform,
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
            SerializedWiring.SetObject(cursor, "_look", look);

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

            PlayerAvatarDriver driver = playerGo.AddComponent<PlayerAvatarDriver>();
            SerializedWiring.SetObject(driver, "_input", input);
            SerializedWiring.SetObject(driver, "_motor", motor);
            SerializedWiring.SetObject(driver, "_hands", hands);
            SerializedWiring.SetObject(driver, "_locomotion", body.Locomotion);

            // --- combat
            Combatant combatant = AddCombatant(playerGo, Faction.Player, "Joueur", head.transform, 100f, 100f, 12f, 8f);

            AddHurtboxes(playerGo, combatant, Faction.Player, PlayerHeight);

            CameraShake shake = cameraShakeNode.AddComponent<CameraShake>();
            CameraPunch punch = cameraPunchNode.AddComponent<CameraPunch>();
            HitStop hitStop = playerGo.AddComponent<HitStop>();

            AttackExecutor executor = playerGo.AddComponent<AttackExecutor>();
            SerializedWiring.SetObject(executor, "_combatant", combatant);
            SerializedWiring.SetObject(executor, "_hands", hands);
            SerializedWiring.SetObject(executor, "_locomotion", body.Locomotion);
            SerializedWiring.SetObject(executor, "_cameraPunch", punch);
            SerializedWiring.SetObject(executor, "_hitStop", hitStop);
            SerializedWiring.SetObject(executor, "_leftHitbox", body.LeftHitbox);
            SerializedWiring.SetObject(executor, "_rightHitbox", body.RightHitbox);

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

            PlayerCombat combat = playerGo.AddComponent<PlayerCombat>();
            SerializedWiring.SetObject(combat, "_input", input);
            SerializedWiring.SetObject(combat, "_executor", executor);
            SerializedWiring.SetObject(combat, "_motor", motor);
            SerializedWiring.SetObject(combat, "_dodge", dodge);
            SerializedWiring.SetObject(combat, "_combatant", combatant);
            SerializedWiring.SetObject(combat, "_straight", straight);
            SerializedWiring.SetObject(combat, "_hook", hook);
            SerializedWiring.SetObject(combat, "_uppercut", uppercut);

            // Relecture immediate : si une de ces trois references est restee vide, le combat
            // sera inerte au lancement. Autant le savoir maintenant.
            SerializedWiring.Verify(combat, "_straight");
            SerializedWiring.Verify(combat, "_hook");
            SerializedWiring.Verify(combat, "_uppercut");

            // --- retours
            AudioSource audioSource = playerGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            ImpactAudio audio = playerGo.AddComponent<ImpactAudio>();

            CombatFeedbackRelay relay = playerGo.AddComponent<CombatFeedbackRelay>();
            SerializedWiring.SetObject(relay, "_executor", executor);
            SerializedWiring.SetObject(relay, "_combatant", combatant);
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

            return playerGo;
        }

        // ------------------------------------------------------------------ ennemi

        private static GameObject BuildEnemy(BuildMaterials materials, AttackData straight, AttackData hook, AttackData uppercut)
        {
            GameObject enemyGo = new GameObject("Ennemi");
            enemyGo.transform.position = new Vector3(0f, 0f, SpawnDistance * 0.5f);
            enemyGo.transform.rotation = Quaternion.Euler(0f, 180f, 0f);

            CharacterController controller = enemyGo.AddComponent<CharacterController>();
            controller.height = PlayerHeight;
            controller.radius = 0.30f;
            controller.center = new Vector3(0f, PlayerHeight * 0.5f, 0f);
            controller.stepOffset = 0.3f;
            controller.skinWidth = 0.02f;

            FighterBuilder.Result body = FighterBuilder.BuildBody(enemyGo.transform,
                FighterBuilder.Skin.Enemy(materials), true, Faction.Enemy, enemyGo);

            // L'ennemi utilise le MEME composant de bras que le joueur : seul le repere change.
            // A hauteur d'yeux et face a l'avant, les poses de garde ecrites pour la premiere
            // personne fonctionnent telles quelles en troisieme personne.
            GameObject armsAnchor = EditorBuildUtility.CreateEmpty("ArmsAnchor", enemyGo.transform,
                new Vector3(0f, FighterBuilder.EyeHeight, 0f));
            FirstPersonHands arms = armsAnchor.AddComponent<FirstPersonHands>();
            SerializedWiring.SetObject(arms, "_poseSpace", armsAnchor.transform);
            SerializedWiring.SetObject(arms, "_leftArm", body.Rig.LeftArm);
            SerializedWiring.SetObject(arms, "_rightArm", body.Rig.RightArm);
            SerializedWiring.SetObject(arms, "_leftHand", body.Rig.LeftHand);
            SerializedWiring.SetObject(arms, "_rightHand", body.Rig.RightHand);
            SerializedWiring.SetObject(arms, "_locomotion", body.Locomotion);

            EnemyMotor motor = enemyGo.AddComponent<EnemyMotor>();

            Combatant combatant = AddCombatant(enemyGo, Faction.Enemy, "Bagarreur",
                armsAnchor.transform, 90f, 100f, 10f, 6f);

            AddHurtboxes(enemyGo, combatant, Faction.Enemy, PlayerHeight);

            AttackExecutor executor = enemyGo.AddComponent<AttackExecutor>();
            SerializedWiring.SetObject(executor, "_combatant", combatant);
            SerializedWiring.SetObject(executor, "_hands", arms);
            SerializedWiring.SetObject(executor, "_locomotion", body.Locomotion);
            SerializedWiring.SetObject(executor, "_leftHitbox", body.LeftHitbox);
            SerializedWiring.SetObject(executor, "_rightHitbox", body.RightHitbox);

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

            EnemyAvatarDriver avatarDriver = enemyGo.AddComponent<EnemyAvatarDriver>();
            SerializedWiring.SetObject(avatarDriver, "_motor", motor);
            SerializedWiring.SetObject(avatarDriver, "_combatant", combatant);
            SerializedWiring.SetObject(avatarDriver, "_arms", arms);
            SerializedWiring.SetObject(avatarDriver, "_locomotion", body.Locomotion);

            EnemyBrain brain = enemyGo.AddComponent<EnemyBrain>();
            SerializedWiring.SetObject(brain, "_self", combatant);
            SerializedWiring.SetObject(brain, "_motor", motor);
            SerializedWiring.SetObject(brain, "_executor", executor);
            SerializedWiring.SetObject(brain, "_dodge", dodge);
            ConfigureEnemyAttacks(brain, straight, hook, uppercut);

            AudioSource audioSource = enemyGo.AddComponent<AudioSource>();
            audioSource.playOnAwake = false;
            ImpactAudio audio = enemyGo.AddComponent<ImpactAudio>();

            CombatFeedbackRelay relay = enemyGo.AddComponent<CombatFeedbackRelay>();
            SerializedWiring.SetObject(relay, "_executor", executor);
            SerializedWiring.SetObject(relay, "_combatant", combatant);
            SerializedWiring.SetObject(relay, "_audio", audio);

            WorldHealthBar bar = enemyGo.AddComponent<WorldHealthBar>();
            SerializedWiring.SetObject(bar, "_combatant", combatant);

            return enemyGo;
        }

        /// <summary>
        /// Répertoire de coups de l'ennemi. Les distances se recouvrent volontairement :
        /// à portée moyenne il a le choix, ce qui rend ses enchaînements moins prévisibles
        /// sans avoir besoin d'une IA plus complexe.
        /// </summary>
        private static void ConfigureEnemyAttacks(EnemyBrain brain, AttackData straight, AttackData hook, AttackData uppercut)
        {
            SerializedObject so = SerializedWiring.Open(brain);
            SerializedProperty attacks = so.FindProperty("_attacks");
            if (attacks == null) return;

            attacks.arraySize = 3;

            SetAttackOption(attacks.GetArrayElementAtIndex(0), straight, 3f, 0f, 1.15f, 0.8f);
            SetAttackOption(attacks.GetArrayElementAtIndex(1), hook, 1.4f, 0f, 1.05f, 2.2f);
            SetAttackOption(attacks.GetArrayElementAtIndex(2), uppercut, 0.8f, 0f, 0.95f, 3.4f);

            SerializedProperty sequence = so.FindProperty("_scriptedSequence");
            if (sequence != null)
            {
                // Sequence de test prete a l'emploi, mais desactivee : cocher "Use Scripted
                // Sequence" suffit a obtenir un adversaire au timing parfaitement previsible.
                sequence.arraySize = 3;
                SetScriptedStep(sequence.GetArrayElementAtIndex(0), 2f, straight);
                SetScriptedStep(sequence.GetArrayElementAtIndex(1), 1.5f, hook);
                SetScriptedStep(sequence.GetArrayElementAtIndex(2), 3f, uppercut);
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
        /// Deux zones touchables : tête et corps. Les colliders sont en trigger — ils servent
        /// uniquement à être touchés, jamais à bloquer un déplacement, ce dont s'occupe déjà
        /// le CharacterController.
        /// </summary>
        private static void AddHurtboxes(GameObject go, Combatant combatant, Faction faction, float height)
        {
            GameObject bodyBox = EditorBuildUtility.CreateEmpty("Hurtbox_Corps", go.transform,
                new Vector3(0f, height * 0.55f, 0f));

            // Hurtbox volontairement plus large que le corps. En jeu de combat, une zone
            // touchable genereuse est la norme : elle pardonne l'imprecision du joueur, alors
            // qu'une zone collee au modele donne l'impression de traverser l'adversaire.
            CapsuleCollider bodyCollider = bodyBox.AddComponent<CapsuleCollider>();
            bodyCollider.isTrigger = true;
            bodyCollider.radius = 0.38f;
            bodyCollider.height = height * 0.85f;

            Hurtbox bodyHurtbox = bodyBox.AddComponent<Hurtbox>();
            SerializedWiring.SetObject(bodyHurtbox, "_health", combatant.Health);
            SerializedWiring.SetObject(bodyHurtbox, "_stats", combatant.Stats);
            SerializedWiring.SetEnum(bodyHurtbox, "_faction", (int)faction);
            SerializedWiring.SetEnum(bodyHurtbox, "_zone", (int)HitZone.Body);
            SerializedWiring.SetFloat(bodyHurtbox, "_damageMultiplier", 1f);

            GameObject headBox = EditorBuildUtility.CreateEmpty("Hurtbox_Tete", go.transform,
                new Vector3(0f, height * 0.89f, 0f));

            SphereCollider headCollider = headBox.AddComponent<SphereCollider>();
            headCollider.isTrigger = true;
            headCollider.radius = 0.21f;

            Hurtbox headHurtbox = headBox.AddComponent<Hurtbox>();
            SerializedWiring.SetObject(headHurtbox, "_health", combatant.Health);
            SerializedWiring.SetObject(headHurtbox, "_stats", combatant.Stats);
            SerializedWiring.SetEnum(headHurtbox, "_faction", (int)faction);
            SerializedWiring.SetEnum(headHurtbox, "_zone", (int)HitZone.Head);
            SerializedWiring.SetFloat(headHurtbox, "_damageMultiplier", 1.6f);
        }

        private static void WireHudAndDebug(GameObject player, GameObject enemy, AttackData testAttack)
        {
            Combatant playerCombatant = player.GetComponent<Combatant>();
            Combatant enemyCombatant = enemy.GetComponent<Combatant>();

            CombatHud hud = player.AddComponent<CombatHud>();
            SerializedWiring.SetObject(hud, "_playerHealth", playerCombatant.Health);
            SerializedWiring.SetObject(hud, "_playerStamina", playerCombatant.Stamina);
            SerializedWiring.SetObject(hud, "_player", playerCombatant);

            CombatDebugOverlay overlay = player.AddComponent<CombatDebugOverlay>();
            SerializedWiring.SetObject(overlay, "_input", player.GetComponent<PlayerInputReader>());
            SerializedWiring.SetObject(overlay, "_player", playerCombatant);
            SerializedWiring.SetObject(overlay, "_playerExecutor", player.GetComponent<AttackExecutor>());
            SerializedWiring.SetObject(overlay, "_playerDodge", player.GetComponent<DodgeSystem>());
            SerializedWiring.SetObject(overlay, "_enemy", enemyCombatant);
            SerializedWiring.SetObject(overlay, "_enemyExecutor", enemy.GetComponent<AttackExecutor>());
            SerializedWiring.SetObject(overlay, "_enemyBrain", enemy.GetComponent<EnemyBrain>());
            SerializedWiring.SetObject(overlay, "_testAttack", testAttack);
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

        private static void BuildSpawnSystem(GameObject player, GameObject enemy)
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
        }

        private static void SetString(Object target, string fieldName, string value)
        {
            SerializedObject so = SerializedWiring.Open(target);
            SerializedProperty property = so.FindProperty(fieldName);
            if (property == null) return;

            property.stringValue = value;
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void RegisterSceneInBuildSettings()
        {
            List<EditorBuildSettingsScene> scenes = new List<EditorBuildSettingsScene>(EditorBuildSettings.scenes);

            for (int i = 0; i < scenes.Count; i++)
            {
                if (scenes[i].path != ScenePath) continue;

                if (!scenes[i].enabled) scenes[i] = new EditorBuildSettingsScene(ScenePath, true);
                EditorBuildSettings.scenes = scenes.ToArray();
                return;
            }

            scenes.Insert(0, new EditorBuildSettingsScene(ScenePath, true));
            EditorBuildSettings.scenes = scenes.ToArray();
        }
    }
}
