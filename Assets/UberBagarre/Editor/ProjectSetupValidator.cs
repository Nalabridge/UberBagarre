using System.Text;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Diagnostic de configuration du projet.
    ///
    /// Raison d'être : les deux pannes les plus fréquentes et les plus déroutantes sur un projet FPS
    /// neuf sont (1) le mauvais réglage de "Active Input Handling" — le joueur ne bouge pas du tout,
    /// sans aucune erreur en console — et (2) un shader qui ne correspond pas au render pipeline.
    /// Cet outil répond aux deux en un clic, au lieu de chercher pendant une heure.
    /// </summary>
    public static class ProjectSetupValidator
    {
        private const string ProjectSettingsAssetPath = "ProjectSettings/ProjectSettings.asset";
        private const string ActiveInputHandlerProperty = "activeInputHandler";

        [MenuItem("Uber Bagarre/1 - Verifier la configuration du projet", false, 10)]
        public static void Validate()
        {
            StringBuilder report = new StringBuilder();
            report.AppendLine("=== Uber Bagarre : diagnostic du projet ===");
            report.AppendLine("Version Unity        : " + Application.unityVersion);
            report.AppendLine("Render pipeline      : " + EditorBuildUtility.ActivePipelineName());
            report.AppendLine("Shader 'Lit' utilise : " + ShaderName());
            report.AppendLine("Espace colorimetrique: " + PlayerSettings.colorSpace);

#if ENABLE_LEGACY_INPUT_MANAGER
            report.AppendLine("Ancien Input Manager : ACTIF (ENABLE_LEGACY_INPUT_MANAGER)");
#else
            report.AppendLine("Ancien Input Manager : inactif");
#endif

#if ENABLE_INPUT_SYSTEM
            report.AppendLine("Nouveau Input System : ACTIF (ENABLE_INPUT_SYSTEM)");
#else
            report.AppendLine("Nouveau Input System : inactif / package absent");
#endif

            int handler;
            if (TryGetActiveInputHandler(out handler))
            {
                report.AppendLine("Active Input Handling: " + DescribeInputHandler(handler));
            }

            bool inputOk;
#if ENABLE_LEGACY_INPUT_MANAGER || ENABLE_INPUT_SYSTEM
            inputOk = true;
#else
            inputOk = false;
#endif

            if (inputOk)
            {
                report.AppendLine();
                report.AppendLine("RESULTAT : configuration d'input OK, le joueur pourra etre controle.");
                Debug.Log(report.ToString());
            }
            else
            {
                report.AppendLine();
                report.AppendLine("PROBLEME : aucun backend d'input n'est compile, rien ne repondra aux touches.");
                report.AppendLine("Correction : Project Settings > Player > Active Input Handling = 'Both', puis redemarre Unity.");
                Debug.LogError(report.ToString());

                OfferInputHandlingFix();
            }
        }

        private static string ShaderName()
        {
            Shader shader = EditorBuildUtility.FindLitShader();
            return shader == null ? "AUCUN (probleme de pipeline)" : shader.name;
        }

        private static string DescribeInputHandler(int value)
        {
            switch (value)
            {
                case 0: return "Input Manager (Old)";
                case 1: return "Input System Package (New)";
                case 2: return "Both";
                default: return "inconnu (" + value + ")";
            }
        }

        /// <summary>
        /// "Active Input Handling" n'est pas exposé par une API publique :
        /// on lit directement la propriété sérialisée de ProjectSettings.asset.
        /// </summary>
        private static bool TryGetActiveInputHandler(out int value)
        {
            value = -1;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsAssetPath);
            if (assets == null || assets.Length == 0) return false;

            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty property = so.FindProperty(ActiveInputHandlerProperty);
            if (property == null) return false;

            value = property.intValue;
            return true;
        }

        private static void OfferInputHandlingFix()
        {
            bool fix = EditorUtility.DisplayDialog(
                "Aucun systeme d'input actif",
                "Le projet n'a ni l'ancien Input Manager ni le nouveau Input System actif : " +
                "aucune touche ne repondra.\n\nRegler 'Active Input Handling' sur 'Both' maintenant ?\n\n" +
                "Unity devra redemarrer pour que le changement prenne effet.",
                "Regler sur 'Both'", "Je le ferai moi-meme");

            if (!fix) return;

            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(ProjectSettingsAssetPath);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError("[UberBagarre] Impossible d'ouvrir " + ProjectSettingsAssetPath +
                               ". Regle 'Active Input Handling' a la main dans Project Settings > Player.");
                return;
            }

            SerializedObject so = new SerializedObject(assets[0]);
            SerializedProperty property = so.FindProperty(ActiveInputHandlerProperty);
            if (property == null)
            {
                Debug.LogError("[UberBagarre] Propriete '" + ActiveInputHandlerProperty + "' introuvable. " +
                               "Regle 'Active Input Handling' a la main dans Project Settings > Player.");
                return;
            }

            property.intValue = 2; // Both
            so.ApplyModifiedProperties();
            AssetDatabase.SaveAssets();

            Debug.LogWarning("[UberBagarre] 'Active Input Handling' regle sur 'Both'. REDEMARRE Unity pour appliquer.");
        }
    }
}
