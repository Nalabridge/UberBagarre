using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Change un <see cref="InteriorPlan"/> en quelques maillages : toutes les pièces d'un
    /// même matériau, dans une même case de la pièce, fusionnées en un seul lot.
    ///
    /// Pourquoi des cases : le rendu « Forward » d'URP n'éclaire chaque objet qu'avec ses huit
    /// lampes les plus fortes. Un sol de 16 m d'un seul tenant sous seize lustres en perdrait
    /// la moitié — par plaques. Découpé en cases de quatre mètres, chaque morceau ne voit que
    /// les lampes au-dessus de lui. Les grandes boîtes droites (sol, murs, plafond) sont
    /// coupées à la taille des cases ; le placage triplanaire, calculé dans le monde, ne
    /// montre aucune couture.
    ///
    /// Les pièces d'un groupe (« porte »…) ont leurs propres lots : le constructeur en fait
    /// des objets à part (le battant qu'on clique pour sortir).
    ///
    /// Indépendant de l'éditeur : le même code alimente la scène et les rendus de contrôle.
    /// </summary>
    public static class InteriorMesher
    {
        public sealed class Batch
        {
            public string Surface;
            public string Group;
            public int CellX;
            public int CellZ;
            public readonly List<Vector3> Vertices = new List<Vector3>();
            public readonly List<Vector3> Normals = new List<Vector3>();
            public readonly List<Vector2> Uvs = new List<Vector2>();
            public readonly List<int> Triangles = new List<int>();
        }

        public static List<Batch> Build(InteriorPlan plan, float cell)
        {
            Dictionary<string, Batch> batches = new Dictionary<string, Batch>();
            List<Batch> order = new List<Batch>();
            for (int i = 0; i < plan.Parts.Count; i++)
            {
                InteriorPlan.Part part = plan.Parts[i];
                if (part.Surface == null) continue;
                foreach (InteriorPlan.Part piece in Split(part, cell))
                {
                    bool grouped = !string.IsNullOrEmpty(piece.Group);
                    int cx = grouped ? 0 : Mathf.FloorToInt(piece.Position.x / cell);
                    int cz = grouped ? 0 : Mathf.FloorToInt(piece.Position.z / cell);
                    string key = piece.Group + "|" + piece.Surface + "|" + cx + "|" + cz;
                    Batch batch;
                    if (!batches.TryGetValue(key, out batch))
                    {
                        batch = new Batch { Surface = piece.Surface, Group = piece.Group ?? "", CellX = cx, CellZ = cz };
                        batches[key] = batch;
                        order.Add(batch);
                    }

                    switch (piece.Shape)
                    {
                        case InteriorPlan.Shape.Cylinder:
                            Cylinder(batch, piece);
                            break;
                        case InteriorPlan.Shape.Sphere:
                            Sphere(batch, piece);
                            break;
                        default:
                            Box(batch, piece);
                            break;
                    }
                }
            }

            return order;
        }

        /// <summary>Une grande boîte droite (sans rotation, ou tournée d'un quart de tour) coupée en morceaux d'une case au plus.</summary>
        private static IEnumerable<InteriorPlan.Part> Split(InteriorPlan.Part part, float cell)
        {
            if (part.Shape != InteriorPlan.Shape.Box || (part.Size.x <= cell * 1.25f && part.Size.z <= cell * 1.25f) || !Upright(part.Rotation))
            {
                yield return part;
                yield break;
            }

            int nx = Mathf.Max(1, Mathf.CeilToInt(part.Size.x / cell));
            int nz = Mathf.Max(1, Mathf.CeilToInt(part.Size.z / cell));
            Vector3 size = new Vector3(part.Size.x / nx, part.Size.y, part.Size.z / nz);
            for (int i = 0; i < nx; i++)
            {
                for (int k = 0; k < nz; k++)
                {
                    Vector3 local = new Vector3(-part.Size.x * 0.5f + size.x * (i + 0.5f), 0f, -part.Size.z * 0.5f + size.z * (k + 0.5f));
                    InteriorPlan.Part piece = part;
                    piece.Position = part.Position + part.Rotation * local;
                    piece.Size = size;
                    yield return piece;
                }
            }
        }

        /// <summary>La rotation ne fait-elle que tourner autour de la verticale ?</summary>
        private static bool Upright(Quaternion q)
        {
            Vector3 up = q * Vector3.up;
            return up.y > 0.9999f;
        }

        // ================================================================== formes

        private static void Box(Batch b, InteriorPlan.Part part)
        {
            Vector3 h = part.Size * 0.5f;
            Quaternion r = part.Rotation;
            Vector3 c = part.Position;
            for (int axis = 0; axis < 3; axis++)
            {
                for (int sign = -1; sign <= 1; sign += 2)
                {
                    Vector3 n = Vector3.zero, u = Vector3.zero, v = Vector3.zero;
                    float du, dv;
                    switch (axis)
                    {
                        case 0:
                            n = new Vector3(sign, 0f, 0f);
                            u = new Vector3(0f, 0f, h.z);
                            v = new Vector3(0f, h.y, 0f);
                            du = part.Size.z;
                            dv = part.Size.y;
                            break;
                        case 1:
                            n = new Vector3(0f, sign, 0f);
                            u = new Vector3(h.x, 0f, 0f);
                            v = new Vector3(0f, 0f, h.z);
                            du = part.Size.x;
                            dv = part.Size.z;
                            break;
                        default:
                            n = new Vector3(0f, 0f, sign);
                            u = new Vector3(h.x, 0f, 0f);
                            v = new Vector3(0f, h.y, 0f);
                            du = part.Size.x;
                            dv = part.Size.y;
                            break;
                    }

                    Vector3 face = new Vector3(n.x * h.x, n.y * h.y, n.z * h.z);
                    Vector3 wn = r * n;
                    Quad(b, c + r * (face - u - v), c + r * (face + u - v), c + r * (face + u + v), c + r * (face - u + v), wn, du, dv);
                }
            }
        }

        private static void Cylinder(Batch b, InteriorPlan.Part part)
        {
            float a = part.Size.x * 0.5f, c = part.Size.z * 0.5f, hh = part.Size.y * 0.5f;
            // Assez de facettes pour qu'un tonneau soit rond, pas plus qu'il n'en faut pour un goulot.
            int segments = Mathf.Clamp(Mathf.RoundToInt(Mathf.Max(a, c) * 2f * Mathf.PI / 0.045f), Mathf.Max(a, c) < 0.025f ? 6 : 8, 28);
            Quaternion r = part.Rotation;
            Vector3 o = part.Position;
            int start = b.Vertices.Count;

            // Le flanc : deux sommets par segment (et un de plus pour fermer les coordonnées de texture).
            for (int i = 0; i <= segments; i++)
            {
                float t = i / (float)segments * Mathf.PI * 2f;
                float x = Mathf.Cos(t) * a, z = Mathf.Sin(t) * c;
                Vector3 n = r * new Vector3(x / Mathf.Max(a * a, 1e-6f), 0f, z / Mathf.Max(c * c, 1e-6f)).normalized;
                b.Vertices.Add(o + r * new Vector3(x, -hh, z));
                b.Vertices.Add(o + r * new Vector3(x, hh, z));
                b.Normals.Add(n);
                b.Normals.Add(n);
                float u = i / (float)segments * Mathf.PI * (a + c);
                b.Uvs.Add(new Vector2(u, 0f));
                b.Uvs.Add(new Vector2(u, part.Size.y));
            }

            for (int i = 0; i < segments; i++)
            {
                int i0 = start + i * 2;
                Tri(b, i0, i0 + 1, i0 + 3, b.Normals[i0]);
                Tri(b, i0, i0 + 3, i0 + 2, b.Normals[i0]);
            }

            // Les deux disques.
            for (int sign = -1; sign <= 1; sign += 2)
            {
                Vector3 n = r * new Vector3(0f, sign, 0f);
                int center = b.Vertices.Count;
                b.Vertices.Add(o + r * new Vector3(0f, sign * hh, 0f));
                b.Normals.Add(n);
                b.Uvs.Add(Vector2.zero);
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments * Mathf.PI * 2f;
                    float x = Mathf.Cos(t) * a, z = Mathf.Sin(t) * c;
                    b.Vertices.Add(o + r * new Vector3(x, sign * hh, z));
                    b.Normals.Add(n);
                    b.Uvs.Add(new Vector2(x, z));
                }

                for (int i = 0; i < segments; i++) Tri(b, center, center + 1 + i, center + 2 + i, n);
            }
        }

        private static void Sphere(Batch b, InteriorPlan.Part part)
        {
            Vector3 h = part.Size * 0.5f;
            float largest = Mathf.Max(h.x, Mathf.Max(h.y, h.z));
            int rings = Mathf.Clamp(Mathf.RoundToInt(largest * Mathf.PI / 0.05f), largest < 0.06f ? 4 : 6, 14);
            int segments = rings * 2;
            Quaternion r = part.Rotation;
            Vector3 o = part.Position;
            int start = b.Vertices.Count;
            for (int j = 0; j <= rings; j++)
            {
                float phi = j / (float)rings * Mathf.PI;
                float y = -Mathf.Cos(phi), ring = Mathf.Sin(phi);
                for (int i = 0; i <= segments; i++)
                {
                    float t = i / (float)segments * Mathf.PI * 2f;
                    Vector3 unit = new Vector3(Mathf.Cos(t) * ring, y, Mathf.Sin(t) * ring);
                    Vector3 p = new Vector3(unit.x * h.x, unit.y * h.y, unit.z * h.z);
                    Vector3 n = new Vector3(unit.x / Mathf.Max(h.x, 1e-5f), unit.y / Mathf.Max(h.y, 1e-5f), unit.z / Mathf.Max(h.z, 1e-5f)).normalized;
                    b.Vertices.Add(o + r * p);
                    b.Normals.Add(r * n);
                    b.Uvs.Add(new Vector2(i / (float)segments * largest * 6f, j / (float)rings * largest * 3f));
                }
            }

            int row = segments + 1;
            for (int j = 0; j < rings; j++)
            {
                for (int i = 0; i < segments; i++)
                {
                    int a0 = start + j * row + i, a1 = a0 + 1, b0 = a0 + row, b1 = b0 + 1;
                    Vector3 n = b.Normals[a0] + b.Normals[b1];
                    if (j > 0) Tri(b, a0, b1, a1, n);
                    if (j < rings - 1) Tri(b, a0, b0, b1, n);
                }
            }
        }

        // ================================================================== triangles

        /// <summary>Un quadrilatère plat a-b-c-d, de normale <paramref name="n"/>, coordonnées de texture en mètres.</summary>
        private static void Quad(Batch b, Vector3 a, Vector3 bb, Vector3 c, Vector3 d, Vector3 n, float du, float dv)
        {
            int start = b.Vertices.Count;
            b.Vertices.Add(a);
            b.Vertices.Add(bb);
            b.Vertices.Add(c);
            b.Vertices.Add(d);
            for (int i = 0; i < 4; i++) b.Normals.Add(n);
            b.Uvs.Add(new Vector2(0f, 0f));
            b.Uvs.Add(new Vector2(du, 0f));
            b.Uvs.Add(new Vector2(du, dv));
            b.Uvs.Add(new Vector2(0f, dv));
            Tri(b, start, start + 1, start + 2, n);
            Tri(b, start, start + 2, start + 3, n);
        }

        /// <summary>
        /// Un triangle tourné vers <paramref name="facing"/> : Unity voit de face un triangle dont
        /// Cross(p1 − p0, p2 − p0) pointe vers la caméra ; on retourne l'ordre s'il le faut.
        /// </summary>
        private static void Tri(Batch b, int i0, int i1, int i2, Vector3 facing)
        {
            Vector3 p0 = b.Vertices[i0];
            Vector3 cross = Vector3.Cross(b.Vertices[i1] - p0, b.Vertices[i2] - p0);
            if (Vector3.Dot(cross, facing) >= 0f)
            {
                b.Triangles.Add(i0);
                b.Triangles.Add(i1);
                b.Triangles.Add(i2);
            }
            else
            {
                b.Triangles.Add(i0);
                b.Triangles.Add(i2);
                b.Triangles.Add(i1);
            }
        }
    }
}
