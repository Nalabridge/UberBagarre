using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Maillages propres à la rue de nuit : les cônes de lumière.
    ///
    /// Unity ne fournit pas de cône en primitive, et c'est justement la forme dont on a
    /// besoin : un lampadaire éclaire un disque au sol depuis un point. Un cylindre à sa
    /// place donne un faisceau à bords parallèles qu'on identifie immédiatement comme un
    /// tube posé là.
    ///
    /// Le cône n'a PAS de fond ni de sommet fermés, et c'est volontaire : ces faces
    /// seraient vues de l'intérieur en passant dessous, et un disque additif plein
    /// apparaîtrait brutalement dans l'image. Une nappe de lumière n'a pas de couvercle.
    ///
    /// Le contenu est régénéré à chaque construction plutôt que conservé s'il existe :
    /// garder un asset tel quel, c'est garantir qu'une amélioration du code n'aura aucun
    /// effet sur un projet déjà ouvert une fois — la panne la plus déroutante qui soit,
    /// puisqu'elle ne produit aucune erreur.
    /// </summary>
    public static class NightMeshFactory
    {
        public const string MeshesFolder = "Assets/UberBagarre/Art/Meshes";

        public const string LightCone = "M_ConeLumiere";
        public const string WideCone = "M_ConeLarge";

        public static void EnsureLibrary()
        {
            EditorBuildUtility.EnsureFolder(MeshesFolder);

            Store(LightCone, BuildCone(0.10f, 0.5f, 28));
            Store(WideCone, BuildCone(0.28f, 0.5f, 32));
        }

        public static Mesh Load(string meshName)
        {
            return AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/" + meshName + ".asset");
        }

        public static GameObject CreateVisual(string meshName, string objectName, Transform parent,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Load(meshName);

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            return go;
        }

        /// <summary>
        /// Cône ouvert de hauteur 1, centré sur l'origine (y de -0,5 à +0,5), sommet en haut.
        /// Les shaders de halo lisent la position locale en y pour leur dégradé : la hauteur
        /// unitaire centrée est donc une convention, pas un détail.
        /// </summary>
        private static Mesh BuildCone(float topRadius, float bottomRadius, int segments)
        {
            segments = Mathf.Max(6, segments);

            Vector3[] vertices = new Vector3[(segments + 1) * 2];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];
            int[] triangles = new int[segments * 6];

            // La normale d'un cône n'est pas horizontale : elle suit la pente du flanc.
            // Le shader de halo s'en sert pour effacer la silhouette — avec une normale
            // fausse, le fondu se fait au mauvais endroit et le cône montre ses bords.
            float slope = (bottomRadius - topRadius);
            float normalY = slope / Mathf.Sqrt(slope * slope + 1f);
            float normalXZ = 1f / Mathf.Sqrt(slope * slope + 1f);

            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = t * Mathf.PI * 2f;
                float cos = Mathf.Cos(angle);
                float sin = Mathf.Sin(angle);

                int top = i * 2;
                int bottom = i * 2 + 1;

                vertices[top] = new Vector3(cos * topRadius, 0.5f, sin * topRadius);
                vertices[bottom] = new Vector3(cos * bottomRadius, -0.5f, sin * bottomRadius);

                Vector3 normal = new Vector3(cos * normalXZ, normalY, sin * normalXZ).normalized;
                normals[top] = normal;
                normals[bottom] = normal;

                uv[top] = new Vector2(t, 1f);
                uv[bottom] = new Vector2(t, 0f);
            }

            for (int i = 0; i < segments; i++)
            {
                int index = i * 6;
                int top = i * 2;
                int bottom = i * 2 + 1;
                int nextTop = (i + 1) * 2;
                int nextBottom = (i + 1) * 2 + 1;

                triangles[index + 0] = top;
                triangles[index + 1] = bottom;
                triangles[index + 2] = nextBottom;

                triangles[index + 3] = top;
                triangles[index + 4] = nextBottom;
                triangles[index + 5] = nextTop;
            }

            Mesh mesh = new Mesh();
            mesh.name = "Cone";
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.triangles = triangles;
            mesh.RecalculateBounds();

            return mesh;
        }

        private static void Store(string name, Mesh generated)
        {
            string path = MeshesFolder + "/" + name + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            if (existing == null)
            {
                generated.name = name;
                AssetDatabase.CreateAsset(generated, path);
                return;
            }

            // Réécrire le contenu conserve l'identifiant de l'asset : toutes les références
            // déjà posées dans des scènes ou des prefabs restent valides.
            existing.Clear();
            existing.vertices = generated.vertices;
            existing.normals = generated.normals;
            existing.uv = generated.uv;
            existing.triangles = generated.triangles;
            existing.RecalculateBounds();

            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(generated);
        }
    }
}
