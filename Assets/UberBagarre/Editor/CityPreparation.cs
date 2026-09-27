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
                      " murs de la demo retires (" + report.objects + " objets defiges), " + report.roads +
                      " routes et " + report.districtObjects + " elements des quartiers fermes rallumes ; " + report.small +
                      " petits objets et " + report.medium + " objets moyens ranges pour n'etre dessines que de pres.");
            return true;
        }

        private struct Report
        {
            public int doors;
            public int sliding;
            public int vehicles;
            public int blockers;
            public int objects;
            public int roads;
            public int districtObjects;
            public int small;
            public int medium;
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
                        CityRules.DedupeWheelModels(t);
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

            OpenClosedDistricts(city, ref report);
            SortByDistance(city, ref report);

            GameObject marker = new GameObject(Marker);
            SceneManager.MoveGameObjectToScene(marker, city);
            return report;
        }

        // ------------------------------------------------------------------ distances de rendu

        /// <summary>
        /// Une ville de 50 000 rendus dessinée jusqu'à 900 m : la plupart sont des détails (une
        /// canette, un panneau, une poubelle) invisibles au-delà de cent mètres. Chaque rendu
        /// est rangé selon sa taille dans un calque que la caméra ne dessine que jusqu'à une
        /// certaine distance (<see cref="CityRules.SmallDetailLayer"/>, <see cref="CityRules.MediumDetailLayer"/>),
        /// et les plus petits ne projettent plus d'ombre (personne ne voit l'ombre d'un mégot,
        /// mais chacune se calcule).
        /// </summary>
        private static void SortByDistance(Scene city, ref Report report)
        {
            GameObject[] roots = city.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Renderer[] renderers = roots[r].GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Renderer renderer = renderers[i];
                    if (renderer is ParticleSystemRenderer || renderer.GetComponent<Terrain>() != null) continue;

                    Vector3 size = renderer.bounds.size;
                    float largest = Mathf.Max(size.x, Mathf.Max(size.y, size.z));
                    if (largest <= 0.001f) continue;

                    if (largest < 0.7f)
                    {
                        renderer.gameObject.layer = CityRules.SmallDetailLayer;
                        renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                        report.small++;
                    }
                    else if (largest < 2.6f)
                    {
                        renderer.gameObject.layer = CityRules.MediumDetailLayer;
                        report.medium++;
                    }
                }
            }
        }

        // ------------------------------------------------------------------ quartiers fermés de la démo

        /// <summary>
        /// Les bâtiments que la démo avait éteints (quartiers pas encore ouverts) et que
        /// l'histoire utilise : la maison du maire, la banque, l'immeuble des beaux quartiers.
        /// </summary>
        private static readonly string[] ClosedBuildings =
        {
            "Map/Container/Mayor house/House_E_6",
            "Map/Container/Upscale Apartments/Building1C_HIGH",
            "Map/Container/TownCenter/Bank",
            "Map/Container/TownCenter/Fountain"
        };

        private static readonly string[] StreetFurniture = { "Bus stops", "Payphones", "Recyclers" };

        /// <summary>
        /// La démo de la carte fermait des quartiers entiers : leurs routes étaient ÉTEINTES
        /// (80 morceaux : la rue du quartier résidentiel, la rue qui y descend, les rues de l'est,
        /// une partie des Slums). Les maisons, elles, étaient là, posées dans l'herbe. On rallume :
        /// - chaque morceau de route éteint qui ne double pas une route allumée (même endroit, ou
        ///   même axe à moins de 5 m : une variante) ;
        /// - les bouts de route (« Road Cap ») qui fermaient la démo, eux, s'éteignent ;
        /// - les feux, arrêts de bus, cabines et poubelles de ces rues ; les bâtiments éteints que
        ///   l'histoire utilise ;
        /// - et l'herbe du terrain qui poussait à travers la chaussée est arrachée.
        /// </summary>
        private static void OpenClosedDistricts(Scene city, ref Report report)
        {
            Transform container = FindPath(city, "Map/Container");
            Transform roads = container != null ? container.Find("Roads") : null;
            if (roads == null) return;

            List<Transform> pieces = new List<Transform>();
            for (int i = 0; i < roads.childCount; i++)
            {
                Transform c = roads.GetChild(i);
                if (c.name == "GameObject")
                {
                    for (int k = 0; k < c.childCount; k++) pieces.Add(c.GetChild(k));
                    if (!c.gameObject.activeSelf) c.gameObject.SetActive(true);
                }
                else
                {
                    pieces.Add(c);
                }
            }

            List<Transform> active = new List<Transform>();
            List<Transform> closed = new List<Transform>();
            for (int i = 0; i < pieces.Count; i++)
            {
                if (pieces[i].gameObject.activeSelf) active.Add(pieces[i]);
                else closed.Add(pieces[i]);
            }

            List<Transform> opened = new List<Transform>();
            for (int i = 0; i < closed.Count; i++)
            {
                Transform piece = closed[i];
                bool duplicate = false;
                for (int k = 0; k < active.Count && !duplicate; k++)
                {
                    Transform other = active[k];
                    if (IsCap(other)) continue;
                    float d = Flat(other.position - piece.position).magnitude;
                    if (d < 3f) duplicate = true;
                    else if (d < 5f && SameAxis(other, piece)) duplicate = true;
                }

                if (duplicate) continue;
                piece.gameObject.SetActive(true);
                opened.Add(piece);
            }

            // Les bouts qui fermaient la démo, maintenant au milieu d'une rue.
            for (int i = 0; i < active.Count; i++)
            {
                if (!IsCap(active[i])) continue;
                if (Near(opened, active[i].position, 6f)) active[i].gameObject.SetActive(false);
            }

            report.roads = opened.Count;

            // Les feux des carrefours rallumés.
            Transform lights = container.Find("TrafficLights");
            if (lights != null)
            {
                List<Transform> crossings = new List<Transform>();
                for (int i = 0; i < opened.Count; i++)
                {
                    if (opened[i].name.Contains("Intersection")) crossings.Add(opened[i]);
                }

                for (int i = 0; i < lights.childCount; i++)
                {
                    Transform light = lights.GetChild(i);
                    if (light.gameObject.activeSelf || !Near(crossings, light.position, 3f)) continue;
                    light.gameObject.SetActive(true);
                    report.districtObjects++;
                }
            }

            // Le mobilier de ces rues.
            for (int f = 0; f < StreetFurniture.Length; f++)
            {
                Transform group = container.Find(StreetFurniture[f]);
                if (group == null) continue;
                for (int i = 0; i < group.childCount; i++)
                {
                    Transform item = group.GetChild(i);
                    if (item.gameObject.activeSelf || !Near(opened, item.position, 12f)) continue;
                    item.gameObject.SetActive(true);
                    report.districtObjects++;
                }
            }

            for (int i = 0; i < ClosedBuildings.Length; i++)
            {
                Transform building = FindPath(city, ClosedBuildings[i]);
                if (building == null || building.gameObject.activeSelf) continue;
                building.gameObject.SetActive(true);
                report.districtObjects++;
            }

            ClearGrass(city, opened);
        }

        /// <summary>Arrache l'herbe (détails du terrain) sous les routes rallumées.</summary>
        private static void ClearGrass(Scene city, List<Transform> roadPieces)
        {
            List<Terrain> terrains = new List<Terrain>();
            GameObject[] roots = city.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++) terrains.AddRange(roots[i].GetComponentsInChildren<Terrain>(true));

            for (int t = 0; t < terrains.Count; t++)
            {
                TerrainData data = terrains[t].terrainData;
                if (data == null || data.detailPrototypes.Length == 0) continue;

                Vector3 origin = terrains[t].transform.position;
                int resolution = data.detailResolution;
                bool changed = false;

                for (int r = 0; r < roadPieces.Count; r++)
                {
                    bool any;
                    Bounds b = CityRules.RendererBounds(roadPieces[r], out any);
                    if (!any) continue;

                    int x0 = Mathf.FloorToInt((b.min.x - 1f - origin.x) / data.size.x * resolution);
                    int x1 = Mathf.CeilToInt((b.max.x + 1f - origin.x) / data.size.x * resolution);
                    int z0 = Mathf.FloorToInt((b.min.z - 1f - origin.z) / data.size.z * resolution);
                    int z1 = Mathf.CeilToInt((b.max.z + 1f - origin.z) / data.size.z * resolution);
                    x0 = Mathf.Clamp(x0, 0, resolution);
                    x1 = Mathf.Clamp(x1, 0, resolution);
                    z0 = Mathf.Clamp(z0, 0, resolution);
                    z1 = Mathf.Clamp(z1, 0, resolution);
                    if (x1 <= x0 || z1 <= z0) continue;

                    int[,] empty = new int[z1 - z0, x1 - x0];
                    for (int layer = 0; layer < data.detailPrototypes.Length; layer++) data.SetDetailLayer(x0, z0, layer, empty);
                    changed = true;
                }

                if (changed) EditorUtility.SetDirty(data);
            }

            AssetDatabase.SaveAssets();
        }

        private static bool IsCap(Transform t)
        {
            return t.name.StartsWith("Road Cap");
        }

        private static bool SameAxis(Transform a, Transform b)
        {
            float angle = Vector3.Angle(Flat(a.forward), Flat(b.forward));
            return angle < 10f || angle > 170f;
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        private static bool Near(List<Transform> list, Vector3 p, float distance)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (Flat(list[i].position - p).sqrMagnitude < distance * distance) return true;
            }

            return false;
        }

        private static Transform FindPath(Scene city, string path)
        {
            int slash = path.IndexOf('/');
            string head = slash < 0 ? path : path.Substring(0, slash);
            GameObject[] roots = city.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != head) continue;
                if (slash < 0) return roots[i].transform;
                Transform found = roots[i].transform.Find(path.Substring(slash + 1));
                if (found != null) return found;
            }

            return null;
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
