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
        private static Material _terrainMaterial;

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
