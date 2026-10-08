using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Un rapport sur le rendu, à coller tel quel dans la conversation quand quelque chose
    /// s'affiche mal (rose, blanc, clignotant) : versions, pipeline actif, état de nos shaders
    /// (avec leurs erreurs de compilation), shaders qui posent problème et quels matériaux les
    /// utilisent, caméras et lumières des scènes ouvertes.
    /// </summary>
    public static class RenderDiagnostic
    {
        private static readonly string[] OurShaders =
        {
            "UberBagarre/Peau", "UberBagarre/WetGround", "UberBagarre/Ciel", "UberBagarre/Neon", "UberBagarre/Glow",
            "UberBagarre/Rain", "UberBagarre/Carte/Triplanaire", "UberBagarre/Carte/DoubleFace", "UberBagarre/Carte/Eau"
        };

        private static readonly HashSet<string> BuiltInOnly = new HashSet<string>
        {
            "Standard", "Standard (Specular setup)", "Autodesk Interactive", "Legacy Shaders/Diffuse",
            "Legacy Shaders/Bumped Diffuse", "Legacy Shaders/Specular", "Legacy Shaders/Transparent/Diffuse",
            "Legacy Shaders/Transparent/Cutout/Diffuse", "Mobile/Diffuse", "Nature/Terrain/Standard"
        };

        [MenuItem("Uber Bagarre/0b - Diagnostic du rendu (copie un rapport)", false, 6)]
        private static void Run()
        {
            StringBuilder r = new StringBuilder();
            r.AppendLine("=== Diagnostic du rendu Über Bagarre ===");
            r.AppendLine("Unity " + Application.unityVersion + " — " + SystemInfo.graphicsDeviceType + " — " + SystemInfo.graphicsDeviceName);

            UnityEditor.PackageManager.PackageInfo urp = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.render-pipelines.universal");
            r.AppendLine("Paquet URP : " + (urp != null ? urp.version : "absent"));

            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            r.AppendLine("Pipeline actif : " + (pipeline != null ? pipeline.name + " (" + AssetDatabase.GetAssetPath(pipeline) + ")" : "rendu intégré"));
            r.AppendLine("Pipeline par défaut : " + (GraphicsSettings.defaultRenderPipeline != null ? GraphicsSettings.defaultRenderPipeline.name : "aucun"));
            r.AppendLine("Qualité : " + QualitySettings.names[QualitySettings.GetQualityLevel()] + " — espace couleur " + PlayerSettings.colorSpace +
                         " — MSAA " + QualitySettings.antiAliasing);
            r.AppendLine("Anticrénelage choisi dans le jeu : " + PlayerPrefs.GetInt(UberBagarre.View.GraphicsDirector.PrefsPrefix + "aa", -1) + " (0 aucun, 1 FXAA, 2 TAA, 3 MSAA)");

            r.AppendLine();
            r.AppendLine("--- Nos shaders");
            for (int i = 0; i < OurShaders.Length; i++)
            {
                Shader shader = Shader.Find(OurShaders[i]);
                if (shader == null)
                {
                    r.AppendLine(OurShaders[i] + " : INTROUVABLE");
                    continue;
                }

                string path = AssetDatabase.GetAssetPath(shader);
                bool urpVersion = !string.IsNullOrEmpty(path) && System.IO.File.Exists(path) &&
                                  System.IO.File.ReadAllText(path).Contains("UniversalPipeline");
                r.AppendLine(OurShaders[i] + " : " + (shader.isSupported ? "supporté" : "NON SUPPORTÉ") +
                             (ShaderUtil.ShaderHasError(shader) ? ", ERREURS" : "") +
                             ", version URP dans le fichier : " + (urpVersion ? "oui" : "NON") +
                             ", sous-shader actif : " + ActiveSubshader(shader) + " — " + path);
                AppendMessages(r, shader);
            }

            r.AppendLine();
            r.AppendLine("--- Shaders à problème (et des matériaux qui les utilisent)");
            Dictionary<Shader, List<string>> byShader = new Dictionary<Shader, List<string>>();
            string[] guids = AssetDatabase.FindAssets("t:Material", new[] { "Assets" });
            for (int i = 0; i < guids.Length; i++)
            {
                string path = AssetDatabase.GUIDToAssetPath(guids[i]);
                Material m = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (m == null) continue;

                Shader s = m.shader;
                List<string> list;
                if (!byShader.TryGetValue(s, out list)) byShader[s] = list = new List<string>();
                list.Add(path);
            }

            int problems = 0;
            foreach (KeyValuePair<Shader, List<string>> pair in byShader.OrderByDescending(p => p.Value.Count))
            {
                Shader s = pair.Key;
                string reason = null;
                if (s == null || s.name == "Hidden/InternalErrorShader") reason = "shader manquant";
                else if (!s.isSupported) reason = "non supporté";
                else if (ShaderUtil.ShaderHasError(s)) reason = "erreurs de compilation";
                else if (pipeline != null && BuiltInOnly.Contains(s.name)) reason = "shader du rendu intégré (rose sous URP)";
                if (reason == null) continue;

                problems++;
                r.AppendLine((s != null ? s.name : "(aucun)") + " — " + reason + " — " + pair.Value.Count + " matériau(x), ex. :");
                for (int k = 0; k < Mathf.Min(4, pair.Value.Count); k++) r.AppendLine("    " + pair.Value[k]);
                if (s != null) AppendMessages(r, s);
            }

            if (problems == 0) r.AppendLine("aucun");

            r.AppendLine();
            r.AppendLine("--- Scènes ouvertes : caméras et lumières");
            for (int si = 0; si < SceneManager.sceneCount; si++)
            {
                Scene scene = SceneManager.GetSceneAt(si);
                if (!scene.isLoaded) continue;
                r.AppendLine("Scène " + scene.name);

                foreach (GameObject root in scene.GetRootGameObjects())
                {
                    foreach (Camera c in root.GetComponentsInChildren<Camera>(true))
                    {
                        Behaviour post = c.GetComponent("UberPostProcess") as Behaviour;
                        r.AppendLine("  Caméra " + c.name + " : " + (c.enabled && c.gameObject.activeInHierarchy ? "active" : "inactive") +
                                     ", profondeur " + c.depth + ", fond " + c.clearFlags + (c.targetTexture != null ? ", rend dans une texture" : "") +
                                     (post != null ? ", UberPostProcess " + (post.enabled ? "allumé" : "éteint") : ""));
                    }

                    foreach (Light l in root.GetComponentsInChildren<Light>(true))
                    {
                        if (l.type != LightType.Directional) continue;
                        r.AppendLine("  Lumière " + l.name + " : " + (l.enabled && l.gameObject.activeInHierarchy ? "allumée" : "éteinte") +
                                     ", intensité " + l.intensity.ToString("0.00") + ", ombres " + l.shadows);
                    }
                }
            }

            string report = r.ToString();
            EditorGUIUtility.systemCopyBuffer = report;
            Debug.Log(report);
            EditorUtility.DisplayDialog("Diagnostic du rendu",
                "Le rapport est copié. Colle-le (Ctrl+V) dans la conversation avec Claude.\n\n" +
                "(Il est aussi dans la console.)", "OK");
        }

        /// <summary>Le sous-shader qu'Unity utilise vraiment (0 = la version URP de nos shaders).</summary>
        private static string ActiveSubshader(Shader shader)
        {
            try
            {
                System.Reflection.MethodInfo m = typeof(ShaderUtil).GetMethod("GetShaderActiveSubshaderIndex",
                    System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                return m != null ? m.Invoke(null, new object[] { shader }).ToString() : "?";
            }
            catch (System.Exception)
            {
                return "?";
            }
        }

        private static void AppendMessages(StringBuilder r, Shader shader)
        {
            ShaderMessage[] messages = ShaderUtil.GetShaderMessages(shader);
            int shown = 0;
            for (int i = 0; i < messages.Length && shown < 6; i++)
            {
                if (messages[i].severity != UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error) continue;
                r.AppendLine("    ! " + messages[i].message + " (ligne " + messages[i].line + ", " + messages[i].platform + ")");
                shown++;
            }
        }
    }
}
