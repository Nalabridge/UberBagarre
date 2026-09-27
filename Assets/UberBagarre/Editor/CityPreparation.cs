using System.Collections.Generic;
using UberBagarre.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Prépare la scène de la ville (Assets/Schedule1/Carte/CarteSchedule1.unity) pour le jeu.
    ///
    /// Le convertisseur a marqué TOUT le décor « statique » : Unity fusionne alors les maillages
    /// au lancement (static batching) et un objet fusionné ne bouge plus à l'écran, même quand
    /// son Transform tourne. C'est pour ça que les portes « ne s'ouvraient pas » : le gond
    /// tournait, le collider suivait, mais on voyait toujours la porte fermée.
    ///
    /// Ici, une fois pour toutes :
    /// - portes (gonds, battants coulissants) et véhicules perdent le marquage qui les fige ;
    /// - les murs de la démo disparaissent (le mur invisible et les barrières de béton qui
    ///   fermaient le quartier jouable, les bloqueurs posés devant certaines portes) ;
    /// - un repère signe la préparation, avec sa version : la refaire ne coûte rien.
    ///
    /// Lancée d'elle-même quand la ville s'ouvre dans l'éditeur et avant chaque construction du
    /// monde ouvert ; aussi au menu Uber Bagarre → Préparer la ville.
    /// </summary>
    public static class CityPreparation
    {
        private const StaticEditorFlags Frozen = StaticEditorFlags.BatchingStatic |
                                                 StaticEditorFlags.OccluderStatic |
                                                 StaticEditorFlags.OccludeeStatic;

        [MenuItem("Uber Bagarre/Preparer la ville (portes, vehicules, murs de la demo)", false, 37)]
        private static void PrepareFromMenu()
        {
            if (!MapPack.Available)
            {
                EditorUtility.DisplayDialog("Uber Bagarre", "La ville (Assets/Schedule1) n'est pas installee.", "OK");
                return;
            }

            Prepare(true);
        }

        /// <summary>La ville ouverte dans l'éditeur porte-t-elle la préparation à jour ?</summary>
        public static bool IsPrepared(Scene city)
        {
            if (!city.IsValid() || !city.isLoaded) return false;
            string wanted = Marker;
            GameObject[] roots = city.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == wanted) return true;
            }

            return false;
        }

        private static string Marker
        {
            get { return CityRules.PreparedMarker + " v" + CityRules.PreparedVersion; }
        }

        /// <summary>
        /// Prépare la ville si besoin (ou toujours, <paramref name="force"/>). Ouvre la scène en
        /// additif si elle ne l'est pas, et la referme ensuite dans ce cas. Vrai si la ville est prête.
        /// </summary>
        public static bool Prepare(bool force)
        {
            if (Application.isPlaying || !MapPack.Available) return false;

            Scene city = SceneManager.GetSceneByPath(MapPack.ScenePath);
            bool opened = false;
            if (!city.IsValid() || !city.isLoaded)
            {
                city = EditorSceneManager.OpenScene(MapPack.ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            if (!city.IsValid() || !city.isLoaded) return false;

            if (!force && IsPrepared(city))
            {
                if (opened) EditorSceneManager.CloseScene(city, true);
                return true;
            }

            Scene active = SceneManager.GetActiveScene();
            Report report = Run(city);

            EditorSceneManager.MarkSceneDirty(city);
            EditorSceneManager.SaveScene(city);
            if (opened) EditorSceneManager.CloseScene(city, true);
            else if (active.IsValid() && active.isLoaded) SceneManager.SetActiveScene(active);

            Debug.Log("[UberBagarre] Ville preparee : " + report.doors + " portes et " + report.sliding +
                      " portes coulissantes rendues mobiles, " + report.vehicles + " vehicules, " + report.blockers +
                      " murs de la demo retires (" + report.objects + " objets defiges).");
            return true;
        }

        private struct Report
        {
            public int doors;
            public int sliding;
            public int vehicles;
            public int blockers;
            public int objects;
        }

        private static Report Run(Scene city)
        {
            Report report = new Report();
            List<GameObject> doomed = new List<GameObject>();
            GameObject[] roots = city.GetRootGameObjects();

            for (int r = 0; r < roots.Length; r++)
            {
                GameObject root = roots[r];
                if (root == null) continue;

                // Une ancienne préparation : on la remplace par celle-ci.
                if (root.name.StartsWith(CityRules.PreparedMarker))
                {
                    doomed.Add(root);
                    continue;
                }

                Transform[] all = root.GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];

                    if (CityRules.IsDemoBlocker(t.name))
                    {
                        doomed.Add(t.gameObject);
                        report.blockers++;
                        continue;
                    }

                    if (CityRules.IsDoorHinge(t))
                    {
                        report.objects += Unfreeze(t);
                        report.doors++;
                        continue;
                    }

                    Transform closed;
                    Transform open;
                    if (CityRules.IsSlidingPanel(t, out closed, out open))
                    {
                        report.objects += Unfreeze(t);
                        report.sliding++;
                        continue;
                    }

                    if (CityRules.IsVehicleRoot(t))
                    {
                        report.objects += Unfreeze(t);
                        report.vehicles++;
                    }
                }
            }

            // Un bloqueur peut être sous un autre (ou sous une ancienne marque) : on ne détruit
            // que ce qui existe encore.
            for (int i = 0; i < doomed.Count; i++)
            {
                if (doomed[i] != null) Object.DestroyImmediate(doomed[i]);
            }

            GameObject marker = new GameObject(Marker);
            SceneManager.MoveGameObjectToScene(marker, city);
            return report;
        }

        /// <summary>Retire le marquage qui fige (fusion, occlusion) sur tout un sous-arbre.</summary>
        public static int Unfreeze(Transform root)
        {
            int count = 0;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                GameObject go = all[i].gameObject;
                StaticEditorFlags flags = GameObjectUtility.GetStaticEditorFlags(go);
                if ((flags & Frozen) == 0) continue;
                GameObjectUtility.SetStaticEditorFlags(go, flags & ~Frozen);
                count++;
            }

            return count;
        }

        /// <summary>Retire tout marquage statique (copies de véhicules posées dans le jeu).</summary>
        public static void ClearStatic(GameObject root)
        {
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++) GameObjectUtility.SetStaticEditorFlags(all[i].gameObject, 0);
        }
    }
}
