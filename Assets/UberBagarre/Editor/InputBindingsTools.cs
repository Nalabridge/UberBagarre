using System.Text;
using UberBagarre.Core;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Remet l'asset de touches à ses valeurs par défaut, depuis la barre de menus.
    ///
    /// Pourquoi cet outil existe : un asset conserve les valeurs enregistrées le jour de sa
    /// création. Changer une valeur par défaut dans le code ne met donc JAMAIS à jour un asset
    /// déjà présent sur le disque — c'est un piège classique d'Unity, et la seule façon
    /// d'appliquer une nouvelle touche par défaut est de réécrire l'asset.
    /// </summary>
    public static class InputBindingsTools
    {
        private const string DefaultFolder = "Assets/UberBagarre/Settings";
        private const string DefaultAssetPath = DefaultFolder + "/InputBindings.asset";

        [MenuItem("Uber Bagarre/3 - Reinitialiser les touches par defaut", false, 30)]
        public static void ResetToDefaults()
        {
            InputBindings asset = FindOrCreateAsset();
            if (asset == null) return;

            InputBindings defaults = ScriptableObject.CreateInstance<InputBindings>();
            string previousName = asset.name;

            EditorUtility.CopySerialized(defaults, asset);
            asset.name = previousName;

            Object.DestroyImmediate(defaults);
            EditorUtility.SetDirty(asset);
            AssetDatabase.SaveAssets();

            Selection.activeObject = asset;
            EditorGUIUtility.PingObject(asset);

            Debug.Log(Describe(asset), asset);
        }

        private static InputBindings FindOrCreateAsset()
        {
            string[] guids = AssetDatabase.FindAssets("t:InputBindings");

            if (guids.Length > 1)
            {
                Debug.LogWarning("[UberBagarre] " + guids.Length + " assets InputBindings trouves. " +
                                 "Seul le premier est reinitialise : " + AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            if (guids.Length > 0)
            {
                return AssetDatabase.LoadAssetAtPath<InputBindings>(AssetDatabase.GUIDToAssetPath(guids[0]));
            }

            EditorBuildUtility.EnsureFolder(DefaultFolder);
            InputBindings created = ScriptableObject.CreateInstance<InputBindings>();
            AssetDatabase.CreateAsset(created, DefaultAssetPath);
            AssetDatabase.SaveAssets();

            Debug.Log("[UberBagarre] Aucun asset de touches trouve : cree a " + DefaultAssetPath);
            return created;
        }

        /// <summary>Affiche les touches réellement enregistrées, pour vérifier d'un coup d'œil que c'est appliqué.</summary>
        private static string Describe(InputBindings b)
        {
            StringBuilder sb = new StringBuilder();
            sb.AppendLine("[UberBagarre] Touches reinitialisees (" + AssetDatabase.GetAssetPath(b) + ") :");
            sb.AppendLine("  Avancer / Reculer     : " + b.moveForward + " / " + b.moveBackward);
            sb.AppendLine("  Gauche / Droite       : " + b.moveLeft + " / " + b.moveRight);
            sb.AppendLine("  Saut                  : " + b.jump);
            sb.AppendLine("  SPRINT                : " + b.sprint);
            sb.AppendLine("  Attaque               : " + b.attackPrimary);
            sb.AppendLine("  Garde                 : " + b.guard);
            sb.AppendLine("  Modificateurs         : " + b.attackModifier + " / " + b.attackModifierAlt);
            sb.AppendLine("  Esquive               : " + b.dodge);
            sb.AppendLine("  Liberer le curseur    : " + b.releaseCursor);
            sb.AppendLine("  Overlay de debug      : " + b.toggleDebugOverlay);
            return sb.ToString();
        }
    }
}
