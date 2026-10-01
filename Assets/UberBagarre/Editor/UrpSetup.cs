using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Passe le projet sous URP, le moteur de rendu de Schedule 1, en un clic.
    ///
    /// 1. Installe le paquet URP s'il manque (Unity recompile, puis la suite reprend seule).
    /// 2. Crée le pipeline : rendu Forward+ (autant de lampes qu'on veut par objet), ombres douces
    ///    du soleil en 4 cascades jusqu'à 150 m, occlusion ambiante (SSAO), HDR.
    /// 3. L'active pour toutes les qualités, passe l'espace de couleur en linéaire.
    /// 4. Convertit vers « URP Lit » les matériaux restés sur le Standard du rendu intégré (ils
    ///    seraient roses) : ceux du jeu, des add-ons, et de la carte si son correctif URP n'est
    ///    pas installé.
    /// 5. Met le terrain de la ville sur le matériau de terrain d'URP.
    ///
    /// Rien ici ne dépend du paquet URP à la compilation : ses types sont retrouvés par leur nom,
    /// et ses réglages écrits par leurs noms sérialisés. Le projet compile avec ou sans lui.
    /// </summary>
    public static class UrpSetup
    {
        public const string Folder = "Assets/UberBagarre/Settings/Rendu";
        public const string PipelinePath = Folder + "/URP_Schedule1.asset";
        public const string RendererPath = Folder + "/URP_Schedule1_Rendu.asset";
        private const string PackageName = "com.unity.render-pipelines.universal";
        private const string PendingKey = "UberBagarre.UrpSetup.EnAttente";

        // Identifiants fixes des fichiers du paquet URP.
        private const string PostProcessDataGuid = "41439944d30ece34e96484bdb6645b55";
        private const string TerrainLitGuid = "594ea882c5a793440b60ff72d896021e";

        private static AddRequest _request;

        [MenuItem("Uber Bagarre/0 - Passer sous URP (le rendu de Schedule 1)", false, 5)]
        private static void Run()
        {
            if (Find("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset") == null)
            {
                InstallPackage();
                return;
            }

            Configure(true);
        }

        [MenuItem("Uber Bagarre/0 - Passer sous URP (le rendu de Schedule 1)", true)]
        private static bool RunValidate()
        {
            return _request == null;
        }

        // ------------------------------------------------------------------ paquet

        private static void InstallPackage()
        {
            if (!EditorUtility.DisplayDialog("Passer sous URP",
                    "Le paquet « Universal Render Pipeline » va être installé (quelques minutes). " +
                    "Unity recompile ensuite, puis la configuration se termine toute seule.",
                    "Installer", "Annuler"))
            {
                return;
            }

            SessionState.SetBool(PendingKey, true);
            _request = Client.Add(PackageName);
            EditorApplication.update += WaitForPackage;
        }

        private static void WaitForPackage()
        {
            if (_request == null || !_request.IsCompleted) return;
            EditorApplication.update -= WaitForPackage;

            if (_request.Status == StatusCode.Failure)
            {
                SessionState.SetBool(PendingKey, false);
                Debug.LogError("[UberBagarre] Installation d'URP impossible : " +
                               (_request.Error != null ? _request.Error.message : "erreur inconnue") +
                               ". Installe « Universal RP » depuis Window > Package Manager, puis relance ce menu.");
            }
            else
            {
                Debug.Log("[UberBagarre] URP installé (" + _request.Result.version + "). Configuration après la recompilation...");
            }

            _request = null;
        }

        /// <summary>Après la recompilation qui suit l'installation du paquet, on termine.</summary>
        [InitializeOnLoadMethod]
        private static void ResumeAfterInstall()
        {
            if (!SessionState.GetBool(PendingKey, false)) return;

            EditorApplication.delayCall += () =>
            {
                if (Find("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset") == null) return;
                SessionState.SetBool(PendingKey, false);
                Configure(true);
            };
        }

        // ------------------------------------------------------------------ configuration

        public static void Configure(bool showReport)
        {
            EditorBuildUtility.EnsureFolder(Folder);

            ScriptableObject renderer = RendererData();
            RenderPipelineAsset pipeline = Pipeline(renderer);
            if (renderer == null || pipeline == null)
            {
                Debug.LogError("[UberBagarre] URP : création du pipeline impossible (voir les messages ci-dessus).");
                return;
            }

            TuneRenderer(renderer);
            TunePipeline(pipeline);
            AddAmbientOcclusion(renderer);
            AssetDatabase.SaveAssets();

            GraphicsSettings.defaultRenderPipeline = pipeline;
            int current = QualitySettings.GetQualityLevel();
            for (int i = 0; i < QualitySettings.names.Length; i++)
            {
                QualitySettings.SetQualityLevel(i, false);
                QualitySettings.renderPipeline = pipeline;
            }

            QualitySettings.SetQualityLevel(current, false);

            if (PlayerSettings.colorSpace != ColorSpace.Linear) PlayerSettings.colorSpace = ColorSpace.Linear;

            int converted = ConvertMaterials();
            int terrains = FixCityTerrains();
            int shaders = UpdateMapShaders();
            ReimportOurShaders();
            AssetDatabase.SaveAssets();

            string report = "Le jeu tourne maintenant sous URP, le rendu de Schedule 1.\n\n" +
                            "• Pipeline : " + PipelinePath + " (Forward+, ombres douces 4 cascades, SSAO, HDR)\n" +
                            "• Matériaux convertis du rendu intégré vers URP Lit : " + converted + "\n" +
                            "• Terrains de la ville passés sur le matériau d'URP : " + terrains + "\n" +
                            "• Shaders de la carte mis à jour : " + shaders + "\n\n" +
                            "Ensuite :\n" +
                            "1. Si ce n'est pas fait, installe le correctif URP de la carte (CorrectifURP.zip) : " +
                            "les matériaux d'origine de Schedule 1.\n" +
                            "2. Relance « Uber Bagarre > 3b - Construire le MONDE OUVERT ».";
            Debug.Log("[UberBagarre] " + report);
            if (showReport) EditorUtility.DisplayDialog("Passer sous URP", report, "OK");
        }

        private static ScriptableObject RendererData()
        {
            ScriptableObject existing = AssetDatabase.LoadAssetAtPath<ScriptableObject>(RendererPath);
            if (existing != null) return existing;

            Type type = Find("UnityEngine.Rendering.Universal.UniversalRendererData");
            if (type == null) return null;

            ScriptableObject data = ScriptableObject.CreateInstance(type);
            AssetDatabase.CreateAsset(data, RendererPath);

            SerializedObject so = new SerializedObject(data);
            Set(so, "postProcessData", LoadByGuid<ScriptableObject>(PostProcessDataGuid));
            so.ApplyModifiedPropertiesWithoutUndo();
            return data;
        }

        private static RenderPipelineAsset Pipeline(ScriptableObject renderer)
        {
            RenderPipelineAsset existing = AssetDatabase.LoadAssetAtPath<RenderPipelineAsset>(PipelinePath);
            if (existing != null || renderer == null) return existing;

            Type type = Find("UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset");
            MethodInfo create = null;
            if (type != null)
            {
                foreach (MethodInfo m in type.GetMethods(BindingFlags.Public | BindingFlags.Static))
                {
                    if (m.Name == "Create" && m.GetParameters().Length == 1) create = m;
                }
            }

            if (create == null) return null;

            RenderPipelineAsset pipeline = create.Invoke(null, new object[] { renderer }) as RenderPipelineAsset;
            if (pipeline == null) return null;

            AssetDatabase.CreateAsset(pipeline, PipelinePath);
            return pipeline;
        }

        /// <summary>Forward+ : toutes les lampes de la rue sur chaque objet, sans limite de quatre.</summary>
        private static void TuneRenderer(ScriptableObject renderer)
        {
            SerializedObject so = new SerializedObject(renderer);
            Set(so, "m_RenderingMode", 2);
            if (so.FindProperty("postProcessData") != null && so.FindProperty("postProcessData").objectReferenceValue == null)
                Set(so, "postProcessData", LoadByGuid<ScriptableObject>(PostProcessDataGuid));
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(renderer);
        }

        /// <summary>Le rendu d'un jeu PC comme Schedule 1 : HDR, ombres du soleil douces et nettes.</summary>
        private static void TunePipeline(RenderPipelineAsset pipeline)
        {
            SerializedObject so = new SerializedObject(pipeline);
            Set(so, "m_SupportsHDR", true);
            Set(so, "m_RenderScale", 1f);
            Set(so, "m_RequireDepthTexture", true);
            Set(so, "m_MainLightRenderingMode", 1);
            Set(so, "m_MainLightShadowsSupported", true);
            Set(so, "m_MainLightShadowmapResolution", 4096);
            Set(so, "m_AdditionalLightsRenderingMode", 1);
            Set(so, "m_AdditionalLightsPerObjectLimit", 8);
            Set(so, "m_ShadowDistance", 150f);
            Set(so, "m_ShadowCascadeCount", 4);
            Set(so, "m_SoftShadowsSupported", true);
            Set(so, "m_SoftShadowQuality", 3);
            Set(so, "m_ReflectionProbeBlending", true);
            Set(so, "m_ReflectionProbeBoxProjection", true);
            Set(so, "m_ColorGradingMode", 1);
            Set(so, "m_UseSRPBatcher", true);
            Set(so, "m_EnableLODCrossFade", true);
            Set(so, "m_SupportsTerrainHoles", true);
            so.ApplyModifiedPropertiesWithoutUndo();
            EditorUtility.SetDirty(pipeline);
        }

        /// <summary>L'occlusion ambiante : les coins, les pieds des murs et le dessous des voitures s'assombrissent.</summary>
        private static void AddAmbientOcclusion(ScriptableObject renderer)
        {
            Type type = Find("UnityEngine.Rendering.Universal.ScreenSpaceAmbientOcclusion");
            if (type == null) return;

            SerializedObject so = new SerializedObject(renderer);
            SerializedProperty features = so.FindProperty("m_RendererFeatures");
            SerializedProperty map = so.FindProperty("m_RendererFeatureMap");
            if (features == null || map == null) return;

            for (int i = 0; i < features.arraySize; i++)
            {
                UnityEngine.Object f = features.GetArrayElementAtIndex(i).objectReferenceValue;
                if (f != null && f.GetType() == type) return;
            }

            ScriptableObject feature = ScriptableObject.CreateInstance(type);
            feature.name = "Occlusion ambiante";
            AssetDatabase.AddObjectToAsset(feature, renderer);

            string guid;
            long localId;
            AssetDatabase.TryGetGUIDAndLocalFileIdentifier(feature, out guid, out localId);

            features.arraySize++;
            features.GetArrayElementAtIndex(features.arraySize - 1).objectReferenceValue = feature;
            map.arraySize++;
            map.GetArrayElementAtIndex(map.arraySize - 1).longValue = localId;
            so.ApplyModifiedPropertiesWithoutUndo();

            SerializedObject settings = new SerializedObject(feature);
            Set(settings, "m_Settings.Intensity", 1.1f);
            Set(settings, "m_Settings.DirectLightingStrength", 0.25f);
            settings.ApplyModifiedPropertiesWithoutUndo();

            EditorUtility.SetDirty(feature);
            EditorUtility.SetDirty(renderer);

            MethodInfo invalidate = renderer.GetType().GetMethod("SetDirty", BindingFlags.Public | BindingFlags.Instance, null, Type.EmptyTypes, null);
            if (invalidate != null) invalidate.Invoke(renderer, null);
        }

        // ------------------------------------------------------------------ matériaux

        private static readonly HashSet<string> BuiltInLit = new HashSet<string>
        {
            "Standard", "Standard (Specular setup)", "Autodesk Interactive",
            "Legacy Shaders/Diffuse", "Legacy Shaders/Bumped Diffuse", "Legacy Shaders/Specular",
            "Legacy Shaders/Bumped Specular", "Legacy Shaders/VertexLit", "Mobile/Diffuse", "Mobile/Bumped Diffuse",
            "Legacy Shaders/Transparent/Diffuse", "Legacy Shaders/Transparent/Cutout/Diffuse",
            "Legacy Shaders/Transparent/Cutout/Bumped Diffuse", "Legacy Shaders/Transparent/Bumped Diffuse"
        };

        /// <summary>Les matériaux du projet restés sur un shader éclairé du rendu intégré passent sur URP Lit.</summary>
        public static int ConvertMaterials()
        {
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");
            if (lit == null) return 0;

            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            int converted = 0;
            try
            {
                for (int i = 0; i < guids.Length; i++)
                {
                    if (i % 50 == 0 && EditorUtility.DisplayCancelableProgressBar("Passer sous URP", "Matériaux...", (float)i / guids.Length)) break;

                    string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                    if (!path.EndsWith(".mat", StringComparison.OrdinalIgnoreCase)) continue;

                    Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                    if (material == null || material.shader == null || !BuiltInLit.Contains(material.shader.name)) continue;

                    ToUrpLit(material, lit);
                    converted++;
                }
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            return converted;
        }

        /// <summary>Un matériau Standard (ou « Legacy ») vers URP Lit, mêmes textures et mêmes réglages.</summary>
        public static void ToUrpLit(Material m, Shader lit)
        {
            string source = m.shader.name;
            Texture albedo = Tex(m, "_MainTex");
            Vector2 scale = m.HasProperty("_MainTex") ? m.GetTextureScale("_MainTex") : Vector2.one;
            Vector2 offset = m.HasProperty("_MainTex") ? m.GetTextureOffset("_MainTex") : Vector2.zero;
            Color color = m.HasProperty("_Color") ? m.GetColor("_Color") : Color.white;
            float glossiness = Float(m, "_Glossiness", Float(m, "_Shininess", 0.3f));
            float glossScale = Float(m, "_GlossMapScale", glossiness);
            Texture metalMap = Tex(m, "_MetallicGlossMap");
            Texture bump = Tex(m, "_BumpMap");
            Texture occlusion = Tex(m, "_OcclusionMap");
            Texture emissionMap = Tex(m, "_EmissionMap");
            Color emission = m.HasProperty("_EmissionColor") ? m.GetColor("_EmissionColor") : Color.black;
            bool emissive = m.IsKeywordEnabled("_EMISSION") && emission.maxColorComponent > 0.004f;
            bool albedoAlpha = Float(m, "_SmoothnessTextureChannel", 0f) > 0.5f;

            int mode = Mathf.RoundToInt(Float(m, "_Mode", 0f));
            if (!source.StartsWith("Standard") && !source.StartsWith("Autodesk"))
            {
                mode = source.Contains("Cutout") ? 1 : source.Contains("Transparent") ? 2 : 0;
                glossiness = source.Contains("Specular") ? 0.45f : 0.2f;
                glossScale = glossiness;
            }

            m.shader = lit;
            m.shaderKeywords = new string[0];

            m.SetTexture("_BaseMap", albedo);
            m.SetTextureScale("_BaseMap", scale);
            m.SetTextureOffset("_BaseMap", offset);
            m.SetColor("_BaseColor", color);
            m.SetFloat("_WorkflowMode", 1f);
            m.SetFloat("_Smoothness", metalMap != null || albedoAlpha ? glossScale : glossiness);
            m.SetFloat("_SmoothnessTextureChannel", albedoAlpha ? 1f : 0f);
            m.SetFloat("_EnvironmentReflections", 1f);
            m.SetFloat("_SpecularHighlights", 1f);
            m.SetFloat("_Cull", 2f);
            if (source.Contains("Specular")) m.SetFloat("_Metallic", 0f);
            if (metalMap != null) m.SetTexture("_MetallicGlossMap", metalMap);
            if (bump != null) m.SetTexture("_BumpMap", bump);
            if (occlusion != null) m.SetTexture("_OcclusionMap", occlusion);
            if (emissive)
            {
                m.SetColor("_EmissionColor", emission);
                if (emissionMap != null) m.SetTexture("_EmissionMap", emissionMap);
                m.EnableKeyword("_EMISSION");
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                m.SetColor("_EmissionColor", Color.black);
                m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            m.SetFloat("_AlphaClip", mode == 1 ? 1f : 0f);
            m.SetFloat("_Surface", mode >= 2 ? 1f : 0f);
            m.SetFloat("_Blend", mode == 3 ? 1f : 0f);
            m.SetFloat("_BlendModePreserveSpecular", 1f);
            m.renderQueue = -1;

            // URP recalcule lui-même mots-clés, modes de mélange et file de rendu d'après ces réglages.
            if (!UpdateUrpMaterial(m)) SetLitKeywords(m, mode, bump != null, metalMap != null, occlusion != null, albedoAlpha);
            EditorUtility.SetDirty(m);
        }

        /// <summary>Fait appliquer par URP sa propre logique de matériau (celle de l'inspecteur).</summary>
        private static bool UpdateUrpMaterial(Material m)
        {
            try
            {
                Type utils = Find("Unity.Rendering.Universal.ShaderUtils");
                Type updateType = utils != null ? utils.GetNestedType("MaterialUpdateType", BindingFlags.NonPublic | BindingFlags.Public) : null;
                Type shaderId = utils != null ? utils.GetNestedType("ShaderID", BindingFlags.NonPublic | BindingFlags.Public) : null;
                if (updateType == null || shaderId == null) return false;

                MethodInfo update = utils.GetMethod("UpdateMaterial", BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Static, null,
                    new[] { typeof(Material), updateType, shaderId }, null);
                if (update == null) return false;

                update.Invoke(null, new[] { m, Enum.Parse(updateType, "ChangedAssignedShader"), Enum.ToObject(shaderId, -1) });
                return true;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] URP : mise à jour du matériau " + m.name + " : " + e.Message);
                return false;
            }
        }

        /// <summary>Si la logique d'URP n'est pas joignable : les mêmes mots-clés et états, posés à la main.</summary>
        private static void SetLitKeywords(Material m, int mode, bool normal, bool metal, bool occlusion, bool albedoAlpha)
        {
            if (normal) m.EnableKeyword("_NORMALMAP");
            if (metal) m.EnableKeyword("_METALLICSPECGLOSSMAP");
            if (occlusion) m.EnableKeyword("_OCCLUSIONMAP");
            if (albedoAlpha) m.EnableKeyword("_SMOOTHNESS_TEXTURE_ALBEDO_CHANNEL_A");

            if (mode >= 2)
            {
                m.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
                if (mode == 3) m.EnableKeyword("_ALPHAPREMULTIPLY_ON");
                m.SetFloat("_SrcBlend", mode == 3 ? (float)BlendMode.One : (float)BlendMode.SrcAlpha);
                m.SetFloat("_DstBlend", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
                m.SetFloat("_DstBlendAlpha", (float)BlendMode.OneMinusSrcAlpha);
                m.SetFloat("_ZWrite", 0f);
                m.SetOverrideTag("RenderType", "Transparent");
                m.renderQueue = (int)RenderQueue.Transparent;
                return;
            }

            m.SetFloat("_SrcBlend", (float)BlendMode.One);
            m.SetFloat("_DstBlend", (float)BlendMode.Zero);
            m.SetFloat("_SrcBlendAlpha", (float)BlendMode.One);
            m.SetFloat("_DstBlendAlpha", (float)BlendMode.Zero);
            m.SetFloat("_ZWrite", 1f);
            if (mode == 1)
            {
                m.EnableKeyword("_ALPHATEST_ON");
                m.SetOverrideTag("RenderType", "TransparentCutout");
                m.renderQueue = (int)RenderQueue.AlphaTest;
            }
            else
            {
                m.SetOverrideTag("RenderType", "Opaque");
            }
        }

        /// <summary>Prépare un matériau URP Lit transparent (décalques, tags) — ou ne fait rien hors URP.</summary>
        public static void MakeTransparent(Material m)
        {
            if (m == null || m.shader == null || !m.shader.name.StartsWith("Universal Render Pipeline")) return;
            m.SetFloat("_Surface", 1f);
            m.SetFloat("_Blend", 0f);
            m.SetFloat("_AlphaClip", 0f);
            if (!UpdateUrpMaterial(m)) SetLitKeywords(m, 2, false, false, false, false);
        }

        // ------------------------------------------------------------------ shaders de la carte

        /// <summary>
        /// Les shaders de la carte livrés avec son correctif URP sont repris ici depuis le dépôt
        /// (Tools/schedule1/shaders) : une correction de shader n'oblige pas à retélécharger la carte.
        /// Seulement si le correctif URP est installé — les matériaux de la v1 attendent les anciens.
        /// </summary>
        private static int UpdateMapShaders()
        {
            if (!File.Exists(MapPack.Root + "/LISEZMOI_URP.txt")) return 0;

            string source = Path.Combine(Path.GetDirectoryName(Application.dataPath) ?? ".", "Tools/schedule1/shaders");
            if (!Directory.Exists(source)) return 0;

            int updated = 0;
            foreach (string file in Directory.GetFiles(source, "*.shader"))
            {
                string target = MapPack.Root + "/Shaders/" + Path.GetFileName(file);
                if (!File.Exists(target)) continue;

                string text = File.ReadAllText(file);
                if (text == File.ReadAllText(target)) continue;

                File.WriteAllText(target, text);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceUpdate);
                updated++;
            }

            return updated;
        }

        /// <summary>
        /// Nos shaders portent leur version URP dans un SubShader gardé par « PackageRequirements » :
        /// elle n'est retenue qu'à l'import. Importés avant l'arrivée du paquet URP, ils resteraient
        /// sans elle (et roses) : on les réimporte.
        /// </summary>
        public static void ReimportOurShaders()
        {
            foreach (string folder in new[] { "Assets/UberBagarre/Art/Shaders", MapPack.Root + "/Shaders" })
            {
                if (!AssetDatabase.IsValidFolder(folder)) continue;
                foreach (string guid in AssetDatabase.FindAssets("t:Shader", new[] { folder }))
                {
                    AssetDatabase.ImportAsset(AssetDatabase.GUIDToAssetPath(guid), ImportAssetOptions.ForceUpdate);
                }
            }
        }

        // ------------------------------------------------------------------ terrain de la ville

        private static int FixCityTerrains()
        {
            if (!File.Exists(MapPack.ScenePath)) return 0;

            Material terrainLit = LoadByGuid<Material>(TerrainLitGuid);
            if (terrainLit == null)
            {
                Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
                if (shader == null) return 0;

                string path = Folder + "/Terrain_URP.mat";
                terrainLit = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (terrainLit == null)
                {
                    terrainLit = new Material(shader);
                    AssetDatabase.CreateAsset(terrainLit, path);
                }
            }

            Scene city = SceneManager.GetSceneByPath(MapPack.ScenePath);
            bool opened = false;
            if (!city.IsValid() || !city.isLoaded)
            {
                city = EditorSceneManager.OpenScene(MapPack.ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            int count = 0;
            GameObject[] roots = city.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Terrain[] terrains = roots[r].GetComponentsInChildren<Terrain>(true);
                for (int i = 0; i < terrains.Length; i++)
                {
                    if (terrains[i].materialTemplate == terrainLit) continue;
                    terrains[i].materialTemplate = terrainLit;
                    EditorUtility.SetDirty(terrains[i]);
                    count++;
                }
            }

            if (count > 0)
            {
                EditorSceneManager.MarkSceneDirty(city);
                EditorSceneManager.SaveScene(city);
            }

            if (opened) EditorSceneManager.CloseScene(city, true);
            return count;
        }

        // ------------------------------------------------------------------ outils

        private static Type Find(string fullName)
        {
            foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            {
                Type type = assembly.GetType(fullName);
                if (type != null) return type;
            }

            return null;
        }

        private static T LoadByGuid<T>(string guid) where T : UnityEngine.Object
        {
            string path = AssetDatabase.GUIDToAssetPath(guid);
            return string.IsNullOrEmpty(path) ? null : AssetDatabase.LoadAssetAtPath<T>(path);
        }

        private static Texture Tex(Material m, string name)
        {
            return m.HasProperty(name) ? m.GetTexture(name) : null;
        }

        private static float Float(Material m, string name, float fallback)
        {
            return m.HasProperty(name) ? m.GetFloat(name) : fallback;
        }

        /// <summary>Écrit un réglage sérialisé par son nom ; un nom inconnu (autre version d'URP) est signalé et ignoré.</summary>
        private static void Set(SerializedObject so, string path, object value)
        {
            SerializedProperty p = so.FindProperty(path);
            if (p == null)
            {
                Debug.LogWarning("[UberBagarre] URP : réglage « " + path + " » introuvable dans " + so.targetObject.GetType().Name + ", ignoré.");
                return;
            }

            switch (p.propertyType)
            {
                case SerializedPropertyType.Boolean: p.boolValue = Convert.ToBoolean(value); break;
                case SerializedPropertyType.Integer: p.intValue = Convert.ToInt32(value); break;
                case SerializedPropertyType.Enum: p.intValue = Convert.ToInt32(value); break;
                case SerializedPropertyType.Float: p.floatValue = Convert.ToSingle(value); break;
                case SerializedPropertyType.ObjectReference: p.objectReferenceValue = value as UnityEngine.Object; break;
                default:
                    Debug.LogWarning("[UberBagarre] URP : réglage « " + path + " » d'un type inattendu (" + p.propertyType + "), ignoré.");
                    break;
            }
        }
    }
}
