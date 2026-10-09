using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UberBagarre.View
{
    /// <summary>
    /// Le jeu sous URP, le moteur de rendu de Schedule 1.
    ///
    /// Le projet compile avec ou sans le paquet URP : rien ici ne le nomme au moment de la
    /// compilation (ses types sont retrouvés par leur nom). Tant qu'URP n'est pas le rendu actif,
    /// ce pont ne fait rien et le jeu garde son rendu intégré (UberPostProcess, reflets planaires).
    ///
    /// Quand URP est actif :
    /// - un volume global porte le post-traitement (tonemapping, bloom, étalonnage, vignette,
    ///   grain, aberration), réglé depuis <see cref="GraphicsDirector"/> comme avant ;
    /// - chaque caméra de jeu reçoit ses données URP (post-traitement, anticrénelage), et nos
    ///   effets du rendu intégré, sans objet sous URP, sont coupés ;
    /// - un terrain resté sur le matériau du rendu intégré (rose sous URP) passe sur celui d'URP.
    /// </summary>
    public static class UrpBridge
    {
        private const string UniversalRuntime = "Unity.RenderPipelines.Universal.Runtime";
        private const string CoreRuntime = "Unity.RenderPipelines.Core.Runtime";

        /// <summary>Les réglages d'image (ceux du menu Graphismes).</summary>
        public struct Look
        {
            public bool Post;
            public float Bloom;
            public float Threshold;
            public float Exposure;
            public float Saturation;
            public float Contrast;
            public float Vignette;
            public float Grain;
            public float Aberration;
            public UberPostProcess.AntiAliasingMode AntiAliasing;
        }

        private static readonly Dictionary<string, Type> Types = new Dictionary<string, Type>();
        private static readonly HashSet<Camera> Cameras = new HashSet<Camera>();

        private static Look _look = new Look
        {
            Post = true, Bloom = 0.6f, Threshold = 1.2f, Exposure = 1f, Saturation = 1.1f, Contrast = 1f,
            Vignette = 0.16f, AntiAliasing = UberPostProcess.AntiAliasingMode.Fxaa
        };

        private static bool _booted;
        private static GameObject _volumeObject;
        private static Component _volume;
        private static ScriptableObject _profile;
        private static object _tonemapping;
        private static object _bloom;
        private static object _color;
        private static object _vignette;
        private static object _grain;
        private static object _aberration;
        private static object _mixer;
        private static object _motionBlur;
        private static object _depthOfField;
        private static Material _terrainMaterial;

        // Les réglages du joueur (menu Réglages) appliqués au pipeline.
        private static float _renderScale = 1f;
        private static int _shadowQuality = 2;
        private static bool _ambientOcclusion = true;
        private static int _colorBlind;
        private static float _motionBlurAmount;
        private static bool _focus;
        private static float _focusDistance = 2f;

        /// <summary>Vrai quand le rendu actif est URP.</summary>
        public static bool Active
        {
            get
            {
                RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
                return pipeline != null && pipeline.GetType().FullName == "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset";
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _booted = false;
            Cameras.Clear();
            _volumeObject = null;
            _volume = null;
            _profile = null;
            _terrainMaterial = null;
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_booted || !Active) return;
            _booted = true;

            RenderPipelineManager.beginCameraRendering += OnBeginCamera;
            SceneManager.sceneLoaded += OnSceneLoaded;

            EnsureVolume();
            PushVolume();
            FixTerrains();
        }

        /// <summary>Applique les réglages d'image (appelé par le GraphicsDirector).</summary>
        public static void Apply(Look look)
        {
            _look = look;
            if (!Active) return;

            Boot();
            EnsureVolume();
            PushVolume();
            PushMsaa();

            Cameras.RemoveWhere(c => c == null);
            foreach (Camera camera in Cameras) SetupCamera(camera);
        }

        private static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            FixTerrains();
        }

        private static void OnBeginCamera(ScriptableRenderContext context, Camera camera)
        {
            if (camera == null || camera.cameraType != CameraType.Game || Cameras.Contains(camera)) return;
            Cameras.Add(camera);
            SetupCamera(camera);
        }

        // ------------------------------------------------------------------ caméras

        private static void SetupCamera(Camera camera)
        {
            // Nos effets du rendu intégré (post-traitement maison, miroir du sol mouillé) ne
            // s'exécutent pas sous URP : on les coupe pour qu'ils ne gardent pas leurs textures.
            UberPostProcess post = camera.GetComponent<UberPostProcess>();
            if (post != null && post.enabled) post.enabled = false;

            Type type = Find(UniversalRuntime, "UnityEngine.Rendering.Universal.UniversalAdditionalCameraData");
            if (type == null) return;

            Component data = camera.GetComponent(type);
            if (data == null) data = camera.gameObject.AddComponent(type);

            // Une caméra qui rend dans une texture (l'écran du téléphone, l'appareil photo) n'a
            // pas besoin du post-traitement : c'est l'image finale qui le reçoit.
            SetMember(data, "renderPostProcessing", _look.Post && camera.targetTexture == null);
            SetMember(data, "antialiasing", AntiAliasingName(_look.AntiAliasing));
            SetMember(data, "antialiasingQuality", "High");
        }

        private static string AntiAliasingName(UberPostProcess.AntiAliasingMode mode)
        {
            switch (mode)
            {
                case UberPostProcess.AntiAliasingMode.Aucun: return "None";
                case UberPostProcess.AntiAliasingMode.Taa: return "TemporalAntiAliasing";
                case UberPostProcess.AntiAliasingMode.Msaa: return "SubpixelMorphologicalAntiAliasing";
                default: return "FastApproximateAntialiasing";
            }
        }

        /// <summary>Le MSAA, sous URP, est un réglage du pipeline (x4) ; le SMAA l'accompagne.</summary>
        private static void PushMsaa()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return;
            SetMember(pipeline, "msaaSampleCount", _look.AntiAliasing == UberPostProcess.AntiAliasingMode.Msaa ? 4 : 1);
        }

        // ------------------------------------------------------------------ volume

        private static void EnsureVolume()
        {
            if (_volume != null) return;

            Type volumeType = Find(CoreRuntime, "UnityEngine.Rendering.Volume");
            Type profileType = Find(CoreRuntime, "UnityEngine.Rendering.VolumeProfile");
            if (volumeType == null || profileType == null) return;

            _volumeObject = new GameObject("Post-traitement (URP)");
            UnityEngine.Object.DontDestroyOnLoad(_volumeObject);
            _volume = _volumeObject.AddComponent(volumeType);
            SetMember(_volume, "isGlobal", true);

            _profile = ScriptableObject.CreateInstance(profileType);
            _profile.name = "Ambiance Schedule 1";
            _profile.hideFlags = HideFlags.DontSave;
            SetMember(_volume, "sharedProfile", _profile);

            _tonemapping = AddOverride("UnityEngine.Rendering.Universal.Tonemapping");
            _bloom = AddOverride("UnityEngine.Rendering.Universal.Bloom");
            _color = AddOverride("UnityEngine.Rendering.Universal.ColorAdjustments");
            _vignette = AddOverride("UnityEngine.Rendering.Universal.Vignette");
            _grain = AddOverride("UnityEngine.Rendering.Universal.FilmGrain");
            _aberration = AddOverride("UnityEngine.Rendering.Universal.ChromaticAberration");
            _mixer = AddOverride("UnityEngine.Rendering.Universal.ChannelMixer");
            _motionBlur = AddOverride("UnityEngine.Rendering.Universal.MotionBlur");
            _depthOfField = AddOverride("UnityEngine.Rendering.Universal.DepthOfField");
        }

        private static object AddOverride(string typeName)
        {
            Type type = Find(UniversalRuntime, typeName);
            if (type == null || _profile == null) return null;

            MethodInfo add = _profile.GetType().GetMethod("Add", new[] { typeof(Type), typeof(bool) });
            if (add == null) return null;

            try
            {
                return add.Invoke(_profile, new object[] { type, false });
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] Post-traitement URP : " + typeName + " — " + e.Message);
                return null;
            }
        }

        private static void PushVolume()
        {
            if (_volume == null) return;

            // Le tonemapping « neutre » garde les couleurs des matériaux (celles de Schedule 1) :
            // il ne fait que ramener les hautes lumières dans l'écran, sans virer au filmique.
            SetParameter(_tonemapping, "mode", "Neutral");

            // Les valeurs du GraphicsDirector ont été réglées pour notre post-traitement maison ;
            // ces conversions donnent le même rendu à l'œil sous URP.
            SetParameter(_bloom, "intensity", _look.Bloom * 0.5f);
            SetParameter(_bloom, "threshold", _look.Threshold);
            SetParameter(_bloom, "scatter", 0.62f);
            SetParameter(_bloom, "highQualityFiltering", true);

            SetParameter(_color, "postExposure", Mathf.Log(Mathf.Max(0.05f, _look.Exposure), 2f));
            SetParameter(_color, "saturation", (_look.Saturation - 1f) * 100f);
            SetParameter(_color, "contrast", (_look.Contrast - 1f) * 100f);

            SetParameter(_vignette, "intensity", _look.Vignette);
            SetParameter(_vignette, "smoothness", 0.45f);

            SetParameter(_grain, "intensity", Mathf.Clamp01(_look.Grain * 2f));
            SetParameter(_aberration, "intensity", Mathf.Clamp01(_look.Aberration * 0.25f));

            SetMember(_volume, "weight", _look.Post ? 1f : 0f);

            PushColorBlind();
            SetParameter(_motionBlur, "intensity", _motionBlurAmount * 0.6f);
            SetParameter(_motionBlur, "quality", _motionBlurAmount > 0f ? "Medium" : "Low");
            SetActive(_motionBlur, _motionBlurAmount > 0.01f);

            // Profondeur de champ : seulement dans les plans de caméra (le comptoir, le barbier).
            SetParameter(_depthOfField, "mode", "Bokeh");
            SetParameter(_depthOfField, "focusDistance", _focusDistance);
            SetParameter(_depthOfField, "focalLength", 62f);
            SetParameter(_depthOfField, "aperture", 3.2f);
            SetActive(_depthOfField, _focus);
        }

        // ------------------------------------------------------------------ réglages du joueur

        /// <summary>
        /// Les réglages graphiques du joueur : résolution de rendu, ombres, occlusion ambiante,
        /// filtre pour daltoniens, flou de mouvement.
        /// </summary>
        public static void ApplySettings(float renderScale, int shadowQuality, bool ambientOcclusion, int colorBlind, float motionBlur)
        {
            _renderScale = Mathf.Clamp(renderScale, 0.5f, 1f);
            _shadowQuality = Mathf.Clamp(shadowQuality, 0, 3);
            _ambientOcclusion = ambientOcclusion;
            _colorBlind = Mathf.Clamp(colorBlind, 0, 3);
            _motionBlurAmount = Mathf.Clamp01(motionBlur);
            if (!Active) return;

            Boot();
            EnsureVolume();
            PushPipeline();
            PushVolume();
        }

        /// <summary>La mise au point d'un plan de caméra (null : plus de profondeur de champ).</summary>
        public static void SetFocus(bool on, float distance)
        {
            _focus = on;
            _focusDistance = Mathf.Max(0.3f, distance);
            if (!Active || _depthOfField == null) return;
            SetParameter(_depthOfField, "focusDistance", _focusDistance);
            SetActive(_depthOfField, _focus);
        }

        private static void PushPipeline()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            if (pipeline == null) return;

            SetMember(pipeline, "renderScale", _renderScale);

            int[] resolutions = { 1024, 2048, 4096, 4096 };
            float[] distances = { 60f, 110f, 150f, 220f };
            int[] cascades = { 2, 3, 4, 4 };
            SetMember(pipeline, "shadowDistance", distances[_shadowQuality]);
            SetMember(pipeline, "shadowCascadeCount", cascades[_shadowQuality]);
            SetMember(pipeline, "mainLightShadowmapResolution", resolutions[_shadowQuality]);
            SetMember(pipeline, "additionalLightsShadowmapResolution", resolutions[Mathf.Max(0, _shadowQuality - 1)]);
            SetMember(pipeline, "supportsSoftShadows", _shadowQuality >= 1);

            // L'occlusion ambiante est une « fonction » du moteur de rendu (SSAO).
            FieldInfo list = FindField(pipeline.GetType(), "m_RendererDataList");
            Array renderers = list != null ? list.GetValue(pipeline) as Array : null;
            for (int i = 0; renderers != null && i < renderers.Length; i++)
            {
                object data = renderers.GetValue(i);
                if (data == null) continue;
                PropertyInfo features = FindProperty(data.GetType(), "rendererFeatures");
                System.Collections.IList featureList = features != null ? features.GetValue(data, null) as System.Collections.IList : null;
                for (int k = 0; featureList != null && k < featureList.Count; k++)
                {
                    ScriptableObject feature = featureList[k] as ScriptableObject;
                    if (feature == null || !feature.GetType().Name.Contains("AmbientOcclusion")) continue;
                    MethodInfo setActive = feature.GetType().GetMethod("SetActive", new[] { typeof(bool) });
                    if (setActive != null) setActive.Invoke(feature, new object[] { _ambientOcclusion });
                }
            }
        }

        /// <summary>
        /// Les filtres pour daltoniens : une correction (« daltonisation ») qui reporte sur les
        /// couleurs encore perçues l'écart que l'œil ne voit pas.
        /// </summary>
        private static void PushColorBlind()
        {
            if (_mixer == null) return;
            if (_colorBlind == 0)
            {
                SetActive(_mixer, false);
                return;
            }

            // Simulation (Machado et al., sévérité 1) puis correction de Fidaner.
            float[,] sim = _colorBlind == 1
                ? new float[,] { { 0.152f, 1.053f, -0.205f }, { 0.115f, 0.786f, 0.099f }, { -0.004f, -0.048f, 1.052f } }
                : _colorBlind == 2
                    ? new float[,] { { 0.367f, 0.861f, -0.228f }, { 0.280f, 0.673f, 0.047f }, { -0.012f, 0.043f, 0.969f } }
                    : new float[,] { { 1.256f, -0.077f, -0.179f }, { -0.078f, 0.931f, 0.148f }, { 0.005f, 0.691f, 0.304f } };
            float[,] shift = { { 0f, 0f, 0f }, { 0.7f, 1f, 0f }, { 0.7f, 0f, 1f } };

            float[,] m = new float[3, 3];
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++)
                {
                    // corrigé = I + S × (I − sim)
                    float sum = 0f;
                    for (int k = 0; k < 3; k++) sum += shift[r, k] * ((k == c ? 1f : 0f) - sim[k, c]);
                    m[r, c] = (r == c ? 1f : 0f) + sum;
                }
            }

            string[] rows = { "red", "green", "blue" };
            string[] cols = { "Red", "Green", "Blue" };
            for (int r = 0; r < 3; r++)
            {
                for (int c = 0; c < 3; c++) SetParameter(_mixer, rows[r] + "Out" + cols[c] + "In", Mathf.Clamp(m[r, c] * 100f, -200f, 200f));
            }

            SetActive(_mixer, true);
        }

        /// <summary>Allume ou éteint un effet du volume.</summary>
        private static void SetActive(object component, bool active)
        {
            if (component == null) return;
            SetMember(component, "active", active);
        }

        private static FieldInfo FindField(Type type, string name)
        {
            for (Type t = type; t != null; t = t.BaseType)
            {
                FieldInfo field = t.GetField(name, BindingFlags.NonPublic | BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (field != null) return field;
            }

            return null;
        }

        // ------------------------------------------------------------------ terrain

        /// <summary>Un terrain sur le matériau du rendu intégré serait rose : il passe sur celui d'URP.</summary>
        private static void FixTerrains()
        {
            Terrain[] terrains = UnityEngine.Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            for (int i = 0; i < terrains.Length; i++)
            {
                Material current = terrains[i].materialTemplate;
                if (current != null && current.shader != null && current.shader.name.StartsWith("Universal Render Pipeline")) continue;

                Material urp = TerrainMaterial();
                if (urp != null) terrains[i].materialTemplate = urp;
            }
        }

        private static Material TerrainMaterial()
        {
            if (_terrainMaterial != null) return _terrainMaterial;

            Shader shader = Shader.Find("Universal Render Pipeline/Terrain/Lit");
            if (shader == null) return null;

            _terrainMaterial = new Material(shader) { name = "Terrain (URP)", hideFlags = HideFlags.DontSave };
            return _terrainMaterial;
        }

        // ------------------------------------------------------------------ réflexion

        private static Type Find(string assembly, string fullName)
        {
            Type type;
            if (Types.TryGetValue(fullName, out type)) return type;

            type = Type.GetType(fullName + ", " + assembly);
            if (type == null)
            {
                Assembly[] loaded = AppDomain.CurrentDomain.GetAssemblies();
                for (int i = 0; i < loaded.Length && type == null; i++) type = loaded[i].GetType(fullName);
            }

            Types[fullName] = type;
            return type;
        }

        /// <summary>Change la valeur d'un réglage de volume (ex. bloom.intensity) et le déclare « surchargé ».</summary>
        private static void SetParameter(object component, string name, object value)
        {
            if (component == null) return;

            FieldInfo field = component.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
            object parameter = field != null ? field.GetValue(component) : null;
            if (parameter == null) return;

            PropertyInfo valueProperty = FindProperty(parameter.GetType(), "value");
            PropertyInfo overrideProperty = FindProperty(parameter.GetType(), "overrideState");
            if (valueProperty == null || !valueProperty.CanWrite) return;

            object converted = ConvertTo(value, valueProperty.PropertyType);
            if (converted == null) return;

            try
            {
                valueProperty.SetValue(parameter, converted, null);
                if (overrideProperty != null && overrideProperty.CanWrite) overrideProperty.SetValue(parameter, true, null);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] Réglage URP " + name + " : " + e.Message);
            }
        }

        /// <summary>Écrit un membre public (propriété ou champ, selon la version d'URP).</summary>
        private static void SetMember(object target, string name, object value)
        {
            if (target == null) return;

            try
            {
                PropertyInfo property = FindProperty(target.GetType(), name);
                if (property != null && property.CanWrite)
                {
                    object converted = ConvertTo(value, property.PropertyType);
                    if (converted != null) property.SetValue(target, converted, null);
                    return;
                }

                FieldInfo field = target.GetType().GetField(name, BindingFlags.Public | BindingFlags.Instance);
                if (field != null)
                {
                    object converted = ConvertTo(value, field.FieldType);
                    if (converted != null) field.SetValue(target, converted);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] Réglage URP " + name + " : " + e.Message);
            }
        }

        private static PropertyInfo FindProperty(Type type, string name)
        {
            // Du type le plus dérivé vers la base : un réglage « borné » redéfinit sa valeur.
            for (Type t = type; t != null; t = t.BaseType)
            {
                PropertyInfo property = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly);
                if (property != null) return property;
            }

            return null;
        }

        private static object ConvertTo(object value, Type target)
        {
            if (value == null) return null;
            if (target.IsInstanceOfType(value)) return value;

            try
            {
                if (target.IsEnum)
                {
                    string name = value as string;
                    return name != null ? Enum.Parse(target, name) : Enum.ToObject(target, Convert.ToInt32(value));
                }

                return Convert.ChangeType(value, target);
            }
            catch (Exception)
            {
                return null;
            }
        }
    }
}
