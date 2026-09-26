using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Assemble des centaines de volumes (immeubles, trottoirs, lampadaires) en QUELQUES maillages
    /// par quartier — un par matière — au lieu d'un cube Unity par pièce.
    ///
    /// Deux raisons, qui décident de tout dans une ville :
    /// - le coût : une ville en cubes, c'est des milliers d'objets à dessiner ; fusionnée, c'est
    ///   quelques dizaines ;
    /// - l'aspect : un cube Unity étale sa texture une fois par face, quelle que soit sa taille —
    ///   un trottoir de 80 m étire ses dalles, un immeuble de 30 m grossit ses fenêtres. Ici les
    ///   UV sont en MÈTRES (projetées sur chaque face) : une dalle fait la même taille partout,
    ///   et les étages des immeubles voisins tombent à la même hauteur.
    ///
    /// Les maillages sont enregistrés comme assets (dossier ignoré par git, régénéré à chaque
    /// construction) ; les collisions sont des boîtes, bien plus légères que des maillages.
    /// </summary>
    public sealed class CityMeshBuilder
    {
        public const string Folder = "Assets/UberBagarre/Art/Ville/Generes";

        private sealed class Batch
        {
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        private struct BoxShape
        {
            public Vector3 Center;
            public Vector3 Size;
            public float Yaw;
        }

        private readonly string _name;
        private readonly Dictionary<Material, Batch> _batches = new Dictionary<Material, Batch>();
        private readonly List<Material> _order = new List<Material>();
        private readonly List<BoxShape> _colliders = new List<BoxShape>();

        public CityMeshBuilder(string name)
        {
            _name = name;
        }

        /// <summary>
        /// Un volume. <paramref name="tile"/> : taille d'une répétition de texture, en mètres
        /// (u horizontal, v vertical sur les faces ; x, z sur le dessus). Zéro = une fois par face.
        /// </summary>
        public void Box(Vector3 center, Vector3 size, Material material, Vector2 tile, bool collider, float yaw = 0f)
        {
            if (material == null) return;

            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 x = rotation * Vector3.right;
            Vector3 z = rotation * Vector3.forward;
            Vector3 h = size * 0.5f;

            Face(material, tile, center + z * h.z, z, Vector3.up, h.x, h.y);
            Face(material, tile, center - z * h.z, -z, Vector3.up, h.x, h.y);
            Face(material, tile, center + x * h.x, x, Vector3.up, h.z, h.y);
            Face(material, tile, center - x * h.x, -x, Vector3.up, h.z, h.y);
            Face(material, tile, center + Vector3.up * h.y, Vector3.up, z, h.x, h.z);
            Face(material, tile, center - Vector3.up * h.y, Vector3.down, z, h.x, h.z);

            if (collider) _colliders.Add(new BoxShape { Center = center, Size = size, Yaw = yaw });
        }

        /// <summary>Un volume sans dessous (posé au sol : la face du bas ne se voit jamais).</summary>
        public void Block(Vector3 center, Vector3 size, Material material, Vector2 tile, bool collider, float yaw = 0f)
        {
            if (material == null) return;

            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 x = rotation * Vector3.right;
            Vector3 z = rotation * Vector3.forward;
            Vector3 h = size * 0.5f;

            Face(material, tile, center + z * h.z, z, Vector3.up, h.x, h.y);
            Face(material, tile, center - z * h.z, -z, Vector3.up, h.x, h.y);
            Face(material, tile, center + x * h.x, x, Vector3.up, h.z, h.y);
            Face(material, tile, center - x * h.x, -x, Vector3.up, h.z, h.y);
            Face(material, tile, center + Vector3.up * h.y, Vector3.up, z, h.x, h.z);

            if (collider) _colliders.Add(new BoxShape { Center = center, Size = size, Yaw = yaw });
        }

        /// <summary>Un rectangle horizontal (sol), vu du dessus.</summary>
        public void Ground(Rect area, float height, Material material, Vector2 tile)
        {
            if (material == null || area.width <= 0f || area.height <= 0f) return;

            Face(material, tile, new Vector3(area.center.x, height, area.center.y), Vector3.up, Vector3.forward,
                area.width * 0.5f, area.height * 0.5f);
        }

        /// <summary>Un toit à deux pentes : faîtage le long de l'axe local X, pignons aux bouts.</summary>
        public void GableRoof(Vector3 baseCenter, Vector3 size, Material roof, Material gable, Vector2 tile, float yaw)
        {
            Quaternion rotation = Quaternion.Euler(0f, yaw, 0f);
            Vector3 x = rotation * Vector3.right;
            Vector3 z = rotation * Vector3.forward;
            float hx = size.x * 0.5f;
            float hz = size.z * 0.5f;
            Vector3 top = baseCenter + Vector3.up * size.y;

            // Deux pans.
            Vector3 f0 = baseCenter - z * hz;
            Vector3 b0 = baseCenter + z * hz;
            Quad(roof, tile, f0 - x * hx, top - x * hx, top + x * hx, f0 + x * hx);
            Quad(roof, tile, b0 + x * hx, top + x * hx, top - x * hx, b0 - x * hx);

            // Pignons.
            Tri(gable ?? roof, tile, baseCenter - x * hx + z * hz, top - x * hx, baseCenter - x * hx - z * hz);
            Tri(gable ?? roof, tile, baseCenter + x * hx - z * hz, top + x * hx, baseCenter + x * hx + z * hz);
        }

        // ------------------------------------------------------------------ primitives

        /// <summary>
        /// Une face rectangulaire. <paramref name="up"/> est le « haut » de la face vue de dehors ;
        /// la droite s'en déduit. Sommets dans le sens horaire vu de dehors (face avant Unity).
        /// </summary>
        private void Face(Material material, Vector2 tile, Vector3 center, Vector3 normal, Vector3 up,
            float halfWidth, float halfHeight)
        {
            Vector3 right = Vector3.Cross(normal, up).normalized;
            Vector3 u = up * halfHeight;
            Vector3 r = right * halfWidth;

            Vector3 a = center - r - u;
            Vector3 b = center - r + u;
            Vector3 c = center + r + u;
            Vector3 d = center + r - u;
            Emit(material, tile, normal, right, up, a, b, c, d);
        }

        private void Quad(Material material, Vector2 tile, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Vector3 normal = Vector3.Cross(b - a, d - a).normalized;
            Vector3 up = Mathf.Abs(normal.y) > 0.95f ? Vector3.forward : Vector3.ProjectOnPlane(Vector3.up, normal).normalized;
            Vector3 right = Vector3.Cross(normal, up).normalized;
            Emit(material, tile, normal, right, up, a, b, c, d);
        }

        private void Tri(Material material, Vector2 tile, Vector3 a, Vector3 b, Vector3 c)
        {
            Batch batch = BatchFor(material);
            Vector3 normal = Vector3.Cross(b - a, c - a).normalized;
            Vector3 up = Mathf.Abs(normal.y) > 0.95f ? Vector3.forward : Vector3.up;
            Vector3 right = Vector3.Cross(normal, up).normalized;

            int start = batch.Vertices.Count;
            Add(batch, tile, a, normal, right, up);
            Add(batch, tile, b, normal, right, up);
            Add(batch, tile, c, normal, right, up);
            batch.Triangles.Add(start);
            batch.Triangles.Add(start + 1);
            batch.Triangles.Add(start + 2);
        }

        private void Emit(Material material, Vector2 tile, Vector3 normal, Vector3 right, Vector3 up,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            Batch batch = BatchFor(material);
            int start = batch.Vertices.Count;

            Add(batch, tile, a, normal, right, up);
            Add(batch, tile, b, normal, right, up);
            Add(batch, tile, c, normal, right, up);
            Add(batch, tile, d, normal, right, up);

            batch.Triangles.Add(start);
            batch.Triangles.Add(start + 1);
            batch.Triangles.Add(start + 2);
            batch.Triangles.Add(start);
            batch.Triangles.Add(start + 2);
            batch.Triangles.Add(start + 3);
        }

        private static void Add(Batch batch, Vector2 tile, Vector3 p, Vector3 normal, Vector3 right, Vector3 up)
        {
            batch.Vertices.Add(p);
            batch.Normals.Add(normal);

            Vector2 uv = tile.x > 0f && tile.y > 0f
                ? new Vector2(Vector3.Dot(p, right) / tile.x, Vector3.Dot(p, up) / tile.y)
                : Vector2.zero;
            batch.Uvs.Add(uv);
        }

        private Batch BatchFor(Material material)
        {
            Batch batch;
            if (_batches.TryGetValue(material, out batch)) return batch;

            batch = new Batch();
            _batches[material] = batch;
            _order.Add(material);
            return batch;
        }

        // ------------------------------------------------------------------ sortie

        /// <summary>
        /// Crée les objets : un rendu par matière, et les boîtes de collision sur un objet à part.
        /// <paramref name="meshCollider"/> : le maillage lui-même sert de collision (le sol).
        /// </summary>
        public GameObject Flush(Transform parent, bool meshCollider = false, bool castShadows = true)
        {
            GameObject root = EditorBuildUtility.CreateEmpty(_name, parent, Vector3.zero);
            root.transform.position = Vector3.zero;
            root.isStatic = true;

            EditorBuildUtility.EnsureFolder(Folder);

            for (int i = 0; i < _order.Count; i++)
            {
                Material material = _order[i];
                Batch batch = _batches[material];
                if (batch.Triangles.Count == 0) continue;

                Mesh mesh = new Mesh();
                mesh.name = _name + " - " + material.name;
                mesh.indexFormat = batch.Vertices.Count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
                mesh.SetVertices(batch.Vertices);
                mesh.SetNormals(batch.Normals);
                mesh.SetUVs(0, batch.Uvs);
                mesh.SetTriangles(batch.Triangles, 0);
                mesh.RecalculateBounds();
                mesh.RecalculateTangents();

                mesh = Save(mesh, Sanitize(_name + "_" + material.name));

                GameObject go = new GameObject(material.name);
                go.transform.SetParent(root.transform, false);
                go.isStatic = true;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;

                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;

                if (meshCollider) go.AddComponent<MeshCollider>().sharedMesh = mesh;
            }

            if (_colliders.Count > 0)
            {
                GameObject collisions = new GameObject("Collisions");
                collisions.transform.SetParent(root.transform, false);
                collisions.isStatic = true;

                for (int i = 0; i < _colliders.Count; i++)
                {
                    BoxShape shape = _colliders[i];

                    if (Mathf.Abs(shape.Yaw) < 0.01f)
                    {
                        BoxCollider box = collisions.AddComponent<BoxCollider>();
                        box.center = shape.Center;
                        box.size = shape.Size;
                        continue;
                    }

                    GameObject turned = new GameObject("Collision tournee");
                    turned.transform.SetParent(collisions.transform, false);
                    turned.transform.SetPositionAndRotation(shape.Center, Quaternion.Euler(0f, shape.Yaw, 0f));
                    turned.isStatic = true;
                    turned.AddComponent<BoxCollider>().size = shape.Size;
                }
            }

            return root;
        }

        private static string Sanitize(string name)
        {
            char[] chars = name.ToCharArray();
            for (int i = 0; i < chars.Length; i++)
            {
                char c = chars[i];
                if (!char.IsLetterOrDigit(c) && c != '_' && c != '-') chars[i] = '_';
            }

            return new string(chars);
        }

        private static Mesh Save(Mesh mesh, string fileName)
        {
            string path = Folder + "/" + fileName + ".asset";

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            // Mise à jour en place : les scènes déjà construites gardent leur référence.
            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>
        /// Fusionne tout ce qu'un objet dessine (une voiture faite de trente pièces) en un maillage
        /// par matière, sous ce même objet. Les lampes, colliders et scripts restent.
        /// </summary>
        public static void Collapse(GameObject root, string fileName)
        {
            MeshFilter[] filters = root.GetComponentsInChildren<MeshFilter>(true);
            Dictionary<Material, List<CombineInstance>> groups = new Dictionary<Material, List<CombineInstance>>();
            List<Material> order = new List<Material>();
            Matrix4x4 toLocal = root.transform.worldToLocalMatrix;

            for (int i = 0; i < filters.Length; i++)
            {
                MeshFilter filter = filters[i];
                MeshRenderer renderer = filter.GetComponent<MeshRenderer>();
                if (filter.sharedMesh == null || renderer == null || renderer.sharedMaterial == null) continue;

                Material material = renderer.sharedMaterial;
                List<CombineInstance> list;
                if (!groups.TryGetValue(material, out list))
                {
                    list = new List<CombineInstance>();
                    groups[material] = list;
                    order.Add(material);
                }

                list.Add(new CombineInstance
                {
                    mesh = filter.sharedMesh,
                    transform = toLocal * filter.transform.localToWorldMatrix
                });

                Object.DestroyImmediate(renderer);
                Object.DestroyImmediate(filter);
            }

            EditorBuildUtility.EnsureFolder(Folder);

            for (int i = 0; i < order.Count; i++)
            {
                Material material = order[i];
                Mesh mesh = new Mesh();
                mesh.name = fileName + " - " + material.name;
                mesh.indexFormat = IndexFormat.UInt32;
                mesh.CombineMeshes(groups[material].ToArray(), true, true);
                mesh.RecalculateBounds();
                mesh = Save(mesh, Sanitize(fileName + "_" + material.name));

                GameObject go = new GameObject(material.name);
                go.transform.SetParent(root.transform, false);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
            }
        }
    }
}
