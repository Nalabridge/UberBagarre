using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La ville vit dans sa propre scène (Assets/Schedule1/Carte/CarteSchedule1.unity), chargée
    /// par-dessus le jeu au lancement. Dans l'éditeur, on ne la verrait donc jamais : ce petit
    /// outil l'ouvre en additif dès que la scène du monde ouvert est ouverte, pour qu'on voie la
    /// ville dans la vue Scene. En Play, MapStreamer la trouve déjà chargée et s'en sert.
    ///
    /// Menu Uber Bagarre → « Afficher / masquer la ville dans l'éditeur » pour la fermer
    /// (la vue est plus légère sans elle).
    /// </summary>
    [InitializeOnLoad]
    public static class MapSceneLoader
    {
        private const string Pref = "UberBagarre.AfficherVille";

        static MapSceneLoader()
        {
            EditorSceneManager.sceneOpened += OnSceneOpened;
        }

        private static bool Enabled
        {
            get { return EditorPrefs.GetBool(Pref, true); }
            set { EditorPrefs.SetBool(Pref, value); }
        }

        private static void OnSceneOpened(Scene scene, OpenSceneMode mode)
        {
            if (!Enabled || Application.isPlaying) return;
            if (scene.path != OpenWorldSceneBuilder.ScenePath) return;

            // Après l'ouverture : on ne recharge pas une scène pendant qu'Unity en ouvre une.
            EditorApplication.delayCall += OpenCity;
        }

        /// <summary>Ouvre la ville par-dessus la scène courante (sans rien faire si elle l'est déjà).</summary>
        public static void OpenCity()
        {
            if (Application.isPlaying || !MapPack.Available) return;

            Scene city = SceneManager.GetSceneByPath(MapPack.ScenePath);
            if (city.IsValid() && city.isLoaded) return;

            EditorSceneManager.OpenScene(MapPack.ScenePath, OpenSceneMode.Additive);

            // La scène du jeu reste la scène active : c'est elle qu'on modifie et sauvegarde.
            Scene game = SceneManager.GetSceneByPath(OpenWorldSceneBuilder.ScenePath);
            if (game.IsValid() && game.isLoaded) SceneManager.SetActiveScene(game);
        }

        [MenuItem("Uber Bagarre/Afficher ou masquer la ville dans l'editeur", false, 36)]
        private static void Toggle()
        {
            Scene city = SceneManager.GetSceneByPath(MapPack.ScenePath);
            if (city.IsValid() && city.isLoaded)
            {
                Enabled = false;
                EditorSceneManager.CloseScene(city, true);
                Debug.Log("[UberBagarre] Ville masquee dans l'editeur (elle se charge toujours en Play).");
                return;
            }

            Enabled = true;
            OpenCity();
            Debug.Log("[UberBagarre] Ville affichee dans l'editeur.");
        }
    }
}
