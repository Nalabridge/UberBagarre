using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Génère des maillages sur mesure, sauvegardés en assets.
    ///
    /// Pourquoi ne pas se contenter des primitives d'Unity : un cube reste un cube et une
    /// capsule reste un tube d'épaisseur constante. À 30 cm de l'œil, une main faite de cubes
    /// et de tubes se lit exactement pour ce qu'elle est. Il faut deux formes qu'Unity ne
    /// fournit pas :
    ///
    /// - le SEGMENT CONIQUE à bouts arrondis : une phalange est plus épaisse à la base qu'à
    ///   la pointe. Sans cette conicité, les doigts ressemblent à des saucisses ;
    /// - la BOÎTE ADOUCIE : un poing n'a ni arêtes vives ni forme sphérique.
    ///
    /// Les maillages sont générés une fois et stockés dans Art/Meshes. Unitaires, ils sont
    /// réutilisés partout par simple mise à l'échelle : une poignée d'assets suffit pour
    /// tout le corps.
    /// </summary>
    public static class ProceduralMeshFactory
    {
        public const string MeshesFolder = "Assets/UberBagarre/Art/Meshes";

        public const string TaperedSegment = "M_SegmentConique";
        public const string EvenSegment = "M_SegmentDroit";
        public const string RoundedBox = "M_BoiteAdoucie";
        public const string Knuckle = "M_Articulation";

        /// <summary>Crée (ou récupère) la bibliothèque de formes de base.</summary>
        public static void EnsureLibrary()
        {
            EditorBuildUtility.EnsureFolder(MeshesFolder);

            // Longueur 1 sur +Z, rayon 0.5 a la base : mis a l'echelle, il devient n'importe
            // quelle phalange, n'importe quel os.
            GetOrCreate(TaperedSegment, () => BuildTaperedCapsule(1f, 0.5f, 0.34f, 14, 6, 5));
            GetOrCreate(EvenSegment, () => BuildTaperedCapsule(1f, 0.5f, 0.46f, 16, 4, 6));
            GetOrCreate(RoundedBox, () => BuildRoundedBox(0.62f, 10));
            GetOrCreate(Knuckle, () => BuildRoundedBox(0.85f, 8));
        }

        public static Mesh Load(string meshName)
        {
            return AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/" + meshName + ".asset");
        }

        private delegate Mesh Factory();

        private static Mesh GetOrCreate(string meshName, Factory factory)
        {
            string path = MeshesFolder + "/" + meshName + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing != null) return existing;

            Mesh mesh = factory();
            mesh.name = meshName;
            AssetDatabase.CreateAsset(mesh, path);
            return mesh;
        }

        // ------------------------------------------------------------------ segment conique

        /// <summary>
        /// Tube conique à calottes hémisphériques, orienté sur +Z, base en z = 0.
        ///
        /// C'est la forme d'un os vivant : une phalange, un avant-bras et une cuisse sont tous
        /// plus épais à une extrémité qu'à l'autre. C'est cette différence d'épaisseur qui
        /// distingue un doigt d'un bâton.
        /// </summary>
        private static Mesh BuildTaperedCapsule(float length, float startRadius, float endRadius,
            int radialSegments, int bodySegments, int capSegments)
        {
            radialSegments = Mathf.Max(4, radialSegments);
            bodySegments = Mathf.Max(1, bodySegments);
            capSegments = Mathf.Max(1, capSegments);

            // Les calottes doivent tenir dans la longueur, sinon la forme se retourne.
            float maxCap = length * 0.45f;
            startRadius = Mathf.Min(startRadius, maxCap);
            endRadius = Mathf.Min(endRadius, maxCap);

            List<Vector3> ringCentres = new List<Vector3>();
            List<float> ringRadii = new List<float>();

            for (int i = 0; i <= capSegments; i++)
            {
                float a = Mathf.PI * 0.5f * i / capSegments;
                ringCentres.Add(new Vector3(0f, 0f, startRadius - startRadius * Mathf.Cos(a)));
                ringRadii.Add(startRadius * Mathf.Sin(a));
            }

            float bodyStart = startRadius;
            float bodyEnd = length - endRadius;

            for (int i = 1; i <= bodySegments; i++)
            {
                float t = i / (float)bodySegments;
                ringCentres.Add(new Vector3(0f, 0f, Mathf.Lerp(bodyStart, bodyEnd, t)));
                ringRadii.Add(Mathf.Lerp(startRadius, endRadius, t));
            }

            for (int i = 1; i <= capSegments; i++)
            {
                float a = Mathf.PI * 0.5f * i / capSegments;
                ringCentres.Add(new Vector3(0f, 0f, bodyEnd + endRadius * Mathf.Sin(a)));
                ringRadii.Add(endRadius * Mathf.Cos(a));
            }

            return BuildLathe(ringCentres, ringRadii, radialSegments);
        }

        /// <summary>Assemble une suite d'anneaux en une surface fermée.</summary>
        private static Mesh BuildLathe(List<Vector3> centres, List<float> radii, int radialSegments)
        {
            int rings = centres.Count;
            int perRing = radialSegments + 1;

            Vector3[] vertices = new Vector3[rings * perRing];
            Vector2[] uv = new Vector2[vertices.Length];

            for (int r = 0; r < rings; r++)
            {
                for (int s = 0; s < perRing; s++)
                {
                    float angle = Mathf.PI * 2f * s / radialSegments;
                    int index = r * perRing + s;

                    vertices[index] = centres[r] + new Vector3(
                        Mathf.Cos(angle) * radii[r],
                        Mathf.Sin(angle) * radii[r],
                        0f);

                    uv[index] = new Vector2(s / (float)radialSegments, r / (float)(rings - 1));
                }
            }

            List<int> triangles = new List<int>((rings - 1) * radialSegments * 6);

            for (int r = 0; r < rings - 1; r++)
            {
                for (int s = 0; s < radialSegments; s++)
                {
                    int a = r * perRing + s;
                    int b = a + 1;
                    int c = a + perRing;
                    int d = c + 1;

                    // L'ordre compte : a,c,b donnerait des faces tournees vers l'interieur
                    // et le maillage serait invisible de l'exterieur.
                    triangles.Add(a); triangles.Add(b); triangles.Add(c);
                    triangles.Add(b); triangles.Add(d); triangles.Add(c);
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.uv = uv;
            mesh.triangles = triangles.ToArray();
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ boîte adoucie

        /// <summary>
        /// Cube dont les sommets sont partiellement projetés sur une sphère.
        ///
        /// À 0 on garde un cube, à 1 on obtient une sphère : entre les deux on a la forme d'un
        /// poing fermé, qui n'est ni l'un ni l'autre. Le maillage reste un cube subdivisé, donc
        /// simple et sans coutures.
        /// </summary>
        private static Mesh BuildRoundedBox(float roundness, int subdivisions)
        {
            subdivisions = Mathf.Max(2, subdivisions);
            roundness = Mathf.Clamp01(roundness);

            List<Vector3> vertices = new List<Vector3>();
            List<int> triangles = new List<int>();

            Vector3[] normals =
            {
                Vector3.forward, Vector3.back, Vector3.up,
                Vector3.down, Vector3.right, Vector3.left
            };

            for (int f = 0; f < normals.Length; f++)
            {
                Vector3 normal = normals[f];
                Vector3 axisA = new Vector3(normal.y, normal.z, normal.x);
                Vector3 axisB = Vector3.Cross(normal, axisA);

                int start = vertices.Count;

                for (int y = 0; y <= subdivisions; y++)
                {
                    for (int x = 0; x <= subdivisions; x++)
                    {
                        float u = x / (float)subdivisions * 2f - 1f;
                        float v = y / (float)subdivisions * 2f - 1f;

                        Vector3 cube = normal + axisA * u + axisB * v;
                        vertices.Add(Vector3.Lerp(cube, cube.normalized, roundness) * 0.5f);
                    }
                }

                int stride = subdivisions + 1;

                for (int y = 0; y < subdivisions; y++)
                {
                    for (int x = 0; x < subdivisions; x++)
                    {
                        int a = start + y * stride + x;
                        int b = a + 1;
                        int c = a + stride;
                        int d = c + 1;

                        triangles.Add(a); triangles.Add(b); triangles.Add(c);
                        triangles.Add(b); triangles.Add(d); triangles.Add(c);
                    }
                }
            }

            Mesh mesh = new Mesh();
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        // ------------------------------------------------------------------ instanciation

        /// <summary>Crée un objet visuel portant un de ces maillages, sans collider.</summary>
        public static GameObject CreateVisual(string meshName, string objectName, Transform parent,
            Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
        {
            EnsureLibrary();

            GameObject go = new GameObject(objectName);
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = localRotation;
            go.transform.localScale = localScale;

            MeshFilter filter = go.AddComponent<MeshFilter>();
            filter.sharedMesh = Load(meshName);

            MeshRenderer renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;

            return go;
        }
    }
}
