using System.Collections.Generic;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les arbres qui rentrent dans les bâtiments.
    ///
    /// La carte d'origine plante ses sapins sur le terrain sans regarder les maisons posées
    /// ensuite : leur feuillage traverse les murs et les toits, et on voit des branches au milieu
    /// des salons. Chaque arbre dont la ramure (une capsule autour du tronc, depuis un tiers de
    /// sa hauteur jusqu'à la cime) touche un bâtiment est retiré — les arbres du terrain comme
    /// ceux posés dans la scène. Un « bâtiment », c'est un collider solide d'au moins 2 m de haut
    /// et 1,5 m de large : les clôtures, poteaux, panneaux et voitures n'en sont pas.
    /// </summary>
    public static partial class CityPreparation
    {
        private static int ClearTreesFromBuildings(Scene city)
        {
            Physics.SyncTransforms();
            Collider[] hits = new Collider[32];
            int removed = 0;

            GameObject[] roots = city.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Terrain[] terrains = roots[r].GetComponentsInChildren<Terrain>(true);
                for (int t = 0; t < terrains.Length; t++) removed += ClearTerrainTrees(terrains[t], hits);
            }

            HashSet<Transform> done = new HashSet<Transform>();
            for (int r = 0; r < roots.Length; r++)
            {
                Renderer[] renderers = roots[r].GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Transform tree = CityColliders.TreeRoot(renderers[i].transform);
                    if (tree == null || !done.Add(tree) || !tree.gameObject.activeInHierarchy) continue;

                    Bounds b = TreeBounds(tree);
                    if (b.size.y < 2.5f) continue;

                    float radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 0.6f, 0.5f, 3f);
                    Vector3 foot = new Vector3(b.center.x, b.min.y, b.center.z);
                    if (!CanopyHitsBuilding(foot, b.size.y, radius, hits, tree)) continue;

                    tree.gameObject.SetActive(false);
                    removed++;
                }
            }

            return removed;
        }

        private static int ClearTerrainTrees(Terrain terrain, Collider[] hits)
        {
            TerrainData data = terrain.terrainData;
            if (data == null || data.treeInstanceCount == 0) return 0;

            // Taille de chaque prototype (hauteur, rayon de la ramure) d'après son modèle.
            TreePrototype[] prototypes = data.treePrototypes;
            Vector2[] sizes = new Vector2[prototypes.Length];
            for (int p = 0; p < prototypes.Length; p++)
            {
                sizes[p] = new Vector2(10f, 3f);
                GameObject prefab = prototypes[p] != null ? prototypes[p].prefab : null;
                if (prefab == null) continue;

                Bounds local = PrefabBounds(prefab);
                if (local.size.y > 0.5f) sizes[p] = new Vector2(local.size.y, Mathf.Max(local.extents.x, local.extents.z));
            }

            Vector3 origin = terrain.GetPosition();
            Vector3 size = data.size;
            TreeInstance[] trees = data.treeInstances;
            List<TreeInstance> kept = new List<TreeInstance>(trees.Length);

            for (int i = 0; i < trees.Length; i++)
            {
                TreeInstance tree = trees[i];
                Vector2 s = tree.prototypeIndex >= 0 && tree.prototypeIndex < sizes.Length ? sizes[tree.prototypeIndex] : new Vector2(10f, 3f);
                float height = s.x * Mathf.Max(0.2f, tree.heightScale);
                float radius = Mathf.Clamp(s.y * Mathf.Max(0.2f, tree.widthScale) * 0.6f, 0.5f, 3f);
                Vector3 foot = origin + Vector3.Scale(tree.position, size);

                if (CanopyHitsBuilding(foot, height, radius, hits, null)) continue;
                kept.Add(tree);
            }

            int removed = trees.Length - kept.Count;
            if (removed == 0) return 0;

            data.treeInstances = kept.ToArray();
            EditorUtility.SetDirty(data);
            AssetDatabase.SaveAssets();

            TerrainCollider collider = terrain.GetComponent<TerrainCollider>();
            if (collider != null) collider.terrainData = data;
            terrain.Flush();
            return removed;
        }

        private static bool CanopyHitsBuilding(Vector3 foot, float height, float radius, Collider[] hits, Transform self)
        {
            float bottom = Mathf.Max(height * 0.3f, 1.6f) + radius;
            float top = Mathf.Max(bottom, height - radius);
            int count = Physics.OverlapCapsuleNonAlloc(foot + Vector3.up * bottom, foot + Vector3.up * top, radius, hits,
                ~0, QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                Collider c = hits[i];
                if (c == null || c is TerrainCollider) continue;
                if (self != null && c.transform.IsChildOf(self)) continue;
                if (c.attachedRigidbody != null && !c.attachedRigidbody.isKinematic) continue;

                Bounds b = c.bounds;
                if (b.size.y < 2f || Mathf.Max(b.size.x, b.size.z) < 1.5f) continue;
                if (IsVehicle(c.transform) || CityColliders.TreeRoot(c.transform) != null) continue;
                return true;
            }

            return false;
        }

        private static bool IsVehicle(Transform t)
        {
            for (Transform at = t; at != null; at = at.parent)
            {
                if (CityRules.IsVehicleRoot(at)) return true;
            }

            return false;
        }

        private static Bounds TreeBounds(Transform tree)
        {
            Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(true);
            Bounds b = renderers.Length > 0 ? renderers[0].bounds : new Bounds(tree.position, Vector3.zero);
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);
            return b;
        }

        /// <summary>Les dimensions d'un modèle d'arbre, dans son repère, d'après ses maillages.</summary>
        private static Bounds PrefabBounds(GameObject prefab)
        {
            bool any = false;
            Bounds total = new Bounds();
            MeshFilter[] filters = prefab.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < filters.Length; i++)
            {
                Mesh mesh = filters[i].sharedMesh;
                if (mesh == null) continue;

                Matrix4x4 m = prefab.transform.worldToLocalMatrix * filters[i].transform.localToWorldMatrix;
                Bounds mb = mesh.bounds;
                Vector3 c = mb.center, e = mb.extents;
                for (int k = 0; k < 8; k++)
                {
                    Vector3 corner = c + new Vector3((k & 1) == 0 ? -e.x : e.x, (k & 2) == 0 ? -e.y : e.y, (k & 4) == 0 ? -e.z : e.z);
                    Vector3 p = m.MultiplyPoint3x4(corner);
                    if (!any) { total = new Bounds(p, Vector3.zero); any = true; }
                    else total.Encapsulate(p);
                }
            }

            return total;
        }
    }
}
