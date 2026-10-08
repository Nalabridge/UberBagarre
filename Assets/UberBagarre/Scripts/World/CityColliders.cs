using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.World
{
    /// <summary>
    /// Des troncs solides pour les arbres de la ville.
    ///
    /// Dans la carte d'origine, les sapins du terrain (des centaines) et beaucoup d'arbres posés
    /// dans les rues n'ont aucune collision : on passait au travers à pied comme en voiture. Au
    /// chargement de la ville, chaque arbre sans collider reçoit une capsule à la taille de son
    /// tronc (le feuillage reste traversable, comme partout) — les arbres du terrain d'après
    /// leurs instances, les autres d'après leurs rendus.
    /// </summary>
    public static class CityColliders
    {
        private static readonly string[] TreeWords = { "tree", "sapin", "pine", "fir", "oak", "palm", "birch", "arbre", "conifer", "spruce", "maple" };

        public static int AddTreeTrunks(Scene scene, GameObject[] roots)
        {
            GameObject holder = new GameObject("Troncs (collisions)");
            if (scene.IsValid()) SceneManager.MoveGameObjectToScene(holder, scene);

            int added = 0;
            HashSet<Transform> done = new HashSet<Transform>();
            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == null) continue;

                Terrain[] terrains = roots[r].GetComponentsInChildren<Terrain>(true);
                for (int t = 0; t < terrains.Length; t++) added += TerrainTrunks(terrains[t], holder.transform);

                Renderer[] renderers = roots[r].GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    Transform tree = TreeRoot(renderers[i].transform);
                    if (tree == null || !done.Add(tree)) continue;
                    if (tree.GetComponentInChildren<Collider>(true) != null) continue;
                    if (AddTrunk(tree, holder.transform)) added++;
                }
            }

            if (added == 0) Object.Destroy(holder);
            return added;
        }

        public static bool IsTree(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            string n = name.ToLowerInvariant();
            if (n.Contains("street") || n.Contains("rustle") || (n.Contains("leaf") && n.Contains("pile"))) return false;
            for (int i = 0; i < TreeWords.Length; i++) if (n.Contains(TreeWords[i])) return true;
            return false;
        }

        /// <summary>L'objet « arbre » le plus haut dans la hiérarchie (l'arbre entier, pas une LOD ou une branche).</summary>
        public static Transform TreeRoot(Transform t)
        {
            Transform found = null;
            for (Transform at = t; at != null; at = at.parent)
            {
                if (IsTree(at.name)) found = at;
            }

            return found;
        }

        private static bool AddTrunk(Transform tree, Transform holder)
        {
            Renderer[] renderers = tree.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0) return false;
            Bounds b = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++) b.Encapsulate(renderers[i].bounds);

            // Trop petit pour être un arbre (un buisson, une plante en pot) : on laisse passer.
            if (b.size.y < 2.2f) return false;

            float radius = Mathf.Clamp(Mathf.Min(b.extents.x, b.extents.z) * 0.12f, 0.15f, 0.45f);
            float height = Mathf.Clamp(b.size.y * 0.6f, 1.8f, 6f);
            Vector3 foot = new Vector3(b.center.x, b.min.y, b.center.z);
            Capsule(holder, tree.name, foot, radius, height);
            return true;
        }

        private static int TerrainTrunks(Terrain terrain, Transform holder)
        {
            TerrainData data = terrain != null ? terrain.terrainData : null;
            if (data == null) return 0;

            TreePrototype[] prototypes = data.treePrototypes;
            TreeInstance[] trees = data.treeInstances;
            if (trees == null || trees.Length == 0) return 0;

            // Un prototype qui a déjà son collider : le terrain s'en occupe.
            bool[] solid = new bool[prototypes.Length];
            for (int p = 0; p < prototypes.Length; p++)
            {
                GameObject prefab = prototypes[p] != null ? prototypes[p].prefab : null;
                solid[p] = prefab != null && prefab.GetComponentInChildren<Collider>(true) != null;
            }

            Vector3 origin = terrain.GetPosition();
            Vector3 size = data.size;
            int added = 0;
            for (int i = 0; i < trees.Length; i++)
            {
                TreeInstance tree = trees[i];
                if (tree.prototypeIndex >= 0 && tree.prototypeIndex < solid.Length && solid[tree.prototypeIndex]) continue;

                Vector3 foot = origin + Vector3.Scale(tree.position, size);
                float radius = Mathf.Clamp(0.28f * tree.widthScale, 0.15f, 0.6f);
                float height = Mathf.Clamp(4f * tree.heightScale, 2f, 8f);
                Capsule(holder, "Arbre du terrain", foot, radius, height);
                added++;
            }

            return added;
        }

        private static void Capsule(Transform holder, string name, Vector3 foot, float radius, float height)
        {
            GameObject go = new GameObject(name + " (tronc)");
            go.transform.SetParent(holder, false);
            go.transform.position = foot;
            go.isStatic = true;
            CapsuleCollider c = go.AddComponent<CapsuleCollider>();
            c.radius = radius;
            c.height = Mathf.Max(height, radius * 2f);
            c.center = new Vector3(0f, c.height * 0.5f, 0f);
        }
    }
}
