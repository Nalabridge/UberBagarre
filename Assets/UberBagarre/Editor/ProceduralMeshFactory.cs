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
        public const string Head = "M_Tete";

        /// <summary>Crée (ou récupère) la bibliothèque de formes de base.</summary>
        public static void EnsureLibrary()
        {
            EditorBuildUtility.EnsureFolder(MeshesFolder);

            // Longueur 1 sur +Z, rayon 0.5 a la base : mis a l'echelle, il devient n'importe
            // quelle phalange, n'importe quel os.
            //
            // Les subdivisions sont volontairement generreuses. Le reproche « ca fait vieux,
            // trop low poly » ne vient pas du nombre de formes mais de leur SILHOUETTE : un
            // bras a 14 cotes montre ses aretes des qu'il passe devant un fond clair. Ces
            // maillages font quelques milliers de triangles en tout, soit une fraction de ce
            // que coute un seul personnage de jeu moderne.
            GetOrCreate(TaperedSegment, () => BuildTaperedCapsule(1f, 0.5f, 0.34f, 22, 8, 7));
            GetOrCreate(EvenSegment, () => BuildTaperedCapsule(1f, 0.5f, 0.46f, 24, 6, 8));
            GetOrCreate(RoundedBox, () => BuildRoundedBox(0.62f, 16));
            GetOrCreate(Knuckle, () => BuildRoundedBox(0.85f, 12));
            GetOrCreate(Head, () => BuildHeadMesh(30, 22));
        }

        public static Mesh Load(string meshName)
        {
            return AssetDatabase.LoadAssetAtPath<Mesh>(MeshesFolder + "/" + meshName + ".asset");
        }

        private delegate Mesh Factory();

        /// <summary>
        /// Récupère le maillage, et le MET À JOUR si sa définition a changé dans le code.
        ///
        /// Un simple « s'il existe, on le garde » a un défaut sérieux : améliorer une forme ici
        /// n'aurait aucun effet sur un projet déjà ouvert une fois, et il faudrait supprimer les
        /// assets à la main sans le savoir. Le nombre de sommets sert donc de signature.
        ///
        /// La mise à jour réécrit le CONTENU de l'asset existant au lieu de le supprimer et de
        /// le recréer : l'identifiant de l'asset est conservé, donc toutes les références de
        /// scène qui pointent dessus restent valides.
        /// </summary>
        private static Mesh GetOrCreate(string meshName, Factory factory)
        {
            string path = MeshesFolder + "/" + meshName + ".asset";
            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);

            Mesh fresh = factory();
            fresh.name = meshName;

            if (existing == null)
            {
                AssetDatabase.CreateAsset(fresh, path);
                return fresh;
            }

            if (existing.vertexCount == fresh.vertexCount)
            {
                Object.DestroyImmediate(fresh);
                return existing;
            }

            Overwrite(existing, fresh);
            EditorUtility.SetDirty(existing);
            Object.DestroyImmediate(fresh);

            Debug.Log("[UberBagarre] Maillage " + meshName + " mis a jour (definition affinee dans le code).", existing);
            return existing;
        }

        /// <summary>
        /// Réécrit un maillage avec le contenu d'un autre, sommet par sommet.
        ///
        /// On recopie les canaux explicitement plutôt que de passer par une copie sérialisée
        /// générique : ici on sait exactement quels canaux existent (sommets, normales, UV,
        /// triangles), et ça reste de l'API Mesh publique et documentée.
        /// </summary>
        private static void Overwrite(Mesh target, Mesh source)
        {
            target.Clear();
            target.indexFormat = source.indexFormat;
            target.SetVertices(new List<Vector3>(source.vertices));
            target.SetNormals(new List<Vector3>(source.normals));

            List<Vector2> uv = new List<Vector2>(source.uv);
            if (uv.Count == target.vertexCount) target.SetUVs(0, uv);

            target.SetTriangles(source.triangles, 0);
            target.RecalculateTangents();
            target.RecalculateBounds();
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

        // ------------------------------------------------------------------ tête

        // Proportions du crâne, en fractions du diamètre. Une tête humaine n'est ni sphérique
        // ni symétrique : elle est plus haute que large, plus profonde que large, et son volume
        // est derrière les oreilles, pas devant.
        private const float SkullWidth = 0.86f;
        private const float SkullHeight = 1.06f;
        private const float SkullDepth = 0.98f;

        /// <summary>
        /// Tête d'un seul maillage, obtenue en déformant une sphère.
        ///
        /// Pourquoi ça remplace un assemblage de boîtes : une tête faite de six primitives
        /// empilées — crâne, mâchoire, arcade, nez — se lit toujours comme six primitives
        /// empilées, quelles que soient les proportions. Les jointures entre les blocs sont
        /// visibles sous tous les angles, et aucune d'elles n'existe sur un vrai visage.
        ///
        /// Une surface continue ne coûte pas plus cher (660 sommets) et la forme peut alors porter
        /// ce qui compte vraiment : le MENTON, qui se resserre et avance, et l'ARRIÈRE DU CRÂNE,
        /// plus volumineux que le front. Ces deux asymétries suffisent à ce qu'on lise
        /// instantanément de quel côté quelqu'un regarde — l'information la plus utile en combat.
        /// </summary>
        private static Mesh BuildHeadMesh(int longitudeSegments, int latitudeSegments)
        {
            longitudeSegments = Mathf.Max(8, longitudeSegments);
            latitudeSegments = Mathf.Max(6, latitudeSegments);

            int perRow = longitudeSegments + 1;
            Vector3[] vertices = new Vector3[perRow * (latitudeSegments + 1)];
            Vector3[] normals = new Vector3[vertices.Length];
            Vector2[] uv = new Vector2[vertices.Length];

            for (int lat = 0; lat <= latitudeSegments; lat++)
            {
                float v = lat / (float)latitudeSegments;
                float phi = v * Mathf.PI;

                for (int lon = 0; lon <= longitudeSegments; lon++)
                {
                    float u = lon / (float)longitudeSegments;
                    float theta = u * Mathf.PI * 2f;

                    Vector3 unit = new Vector3(
                        Mathf.Sin(phi) * Mathf.Sin(theta),
                        Mathf.Cos(phi),
                        Mathf.Sin(phi) * Mathf.Cos(theta));

                    int index = lat * perRow + lon;
                    vertices[index] = ShapeSkull(unit);
                    uv[index] = new Vector2(u, 1f - v);

                    // Normale de l'ellipsoide, calculee et non deduite des triangles. Une sphere
                    // UV duplique ses sommets sur la couture de longitude : RecalculateNormals y
                    // laisserait une ligne d'ombrage verticale en plein milieu du visage.
                    normals[index] = new Vector3(
                        unit.x / (SkullWidth * SkullWidth),
                        unit.y / (SkullHeight * SkullHeight),
                        unit.z / (SkullDepth * SkullDepth)).normalized;
                }
            }

            List<int> triangles = new List<int>(latitudeSegments * longitudeSegments * 6);

            for (int lat = 0; lat < latitudeSegments; lat++)
            {
                for (int lon = 0; lon < longitudeSegments; lon++)
                {
                    int a = lat * perRow + lon;
                    int b = a + 1;
                    int c = a + perRow;
                    int d = c + 1;

                    triangles.Add(a); triangles.Add(c); triangles.Add(b);
                    triangles.Add(b); triangles.Add(c); triangles.Add(d);
                }
            }

            Mesh mesh = new Mesh();
            mesh.vertices = vertices;
            mesh.normals = normals;
            mesh.uv = uv;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Déforme un point de la sphère unitaire en forme de crâne. Résultat de diamètre 1.</summary>
        private static Vector3 ShapeSkull(Vector3 unit)
        {
            Vector3 p = new Vector3(unit.x * SkullWidth, unit.y * SkullHeight, unit.z * SkullDepth);

            // Arriere du crane plus volumineux que le front : c'est la premiere asymetrie qui
            // fait lire une tete comme une tete et non comme un ballon.
            if (p.z < 0f) p.z *= 1f + 0.12f * (-unit.z) * Mathf.Clamp01(unit.y + 0.5f);

            // Menton : la tete se resserre fortement vers le bas et avance. Deuxieme asymetrie,
            // et celle qui donne la direction du regard quand on ne voit pas les yeux.
            float low = Mathf.Clamp01(-unit.y);
            p.x *= 1f - 0.36f * low * low;
            p.z += 0.13f * low * low;
            p.y *= 1f + 0.08f * low;

            // Front legerement aplati : un front spherique fait visage de nourrisson.
            if (unit.z > 0.45f && unit.y > 0.25f) p.z -= 0.06f * (unit.z - 0.45f);

            // Creux des tempes, juste au-dessus des oreilles.
            float temple = Mathf.Clamp01(1f - Mathf.Abs(unit.y - 0.2f) * 4.5f) *
                           Mathf.Clamp01(Mathf.Abs(unit.x) * 1.7f - 0.55f);
            p.x *= 1f - 0.09f * temple;

            return p * 0.5f;
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
            List<Vector3> normals = new List<Vector3>();
            List<int> triangles = new List<int>();

            Vector3[] faces =
            {
                Vector3.forward, Vector3.back, Vector3.up,
                Vector3.down, Vector3.right, Vector3.left
            };

            for (int f = 0; f < faces.Length; f++)
            {
                Vector3 normal = faces[f];
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

                        // Normale calculee, pas deduite des triangles.
                        //
                        // Les six faces ne partagent aucun sommet : RecalculateNormals laisse
                        // donc une cassure nette sur chaque arete du cube, alors que la
                        // geometrie, elle, est arrondie. On melange la normale de face et la
                        // direction radiale dans la MEME proportion que la geometrie : l'ombrage
                        // suit alors la forme reelle, et un poing cesse d'avoir des facettes.
                        normals.Add(Vector3.Lerp(normal, cube.normalized, roundness).normalized);
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
            mesh.SetNormals(normals);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateTangents();
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
