using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>Les coupes de cheveux (l'ordre est celui du barbier et de la sauvegarde).</summary>
    public enum HairCut
    {
        Rase,
        Boule,
        Courte,
        Degrade,
        Brosse,
        Banane,
        Plaque,
        Iroquoise,
        Afro,
        MiLong,
        Chignon
    }

    /// <summary>Les barbes (en plus de la barbe de trois jours peinte sur la peau).</summary>
    public enum BeardStyle
    {
        Aucune,
        Moustache,
        Bouc,
        Courte,
        Fournie
    }

    /// <summary>
    /// Le catalogue du barbier : les coupes, les barbes, les couleurs, leurs noms et leurs prix.
    /// </summary>
    public static class HairCatalog
    {
        public struct Entry
        {
            public string Name;
            public string Detail;
            public int Price;

            public Entry(string name, int price, string detail)
            {
                Name = name;
                Price = price;
                Detail = detail;
            }
        }

        public static readonly Entry[] Cuts =
        {
            new Entry("Crâne rasé", 10, "À la tondeuse, puis au rasoir. Rien qui dépasse."),
            new Entry("Boule à zéro", 12, "Trois millimètres partout. Le classique du ring."),
            new Entry("Coupe courte", 20, "Courte sur les côtés, un peu plus dessus. Propre."),
            new Entry("Dégradé", 28, "Côtés rasés en fondu, dessus fourni. La coupe du quartier."),
            new Entry("Brosse", 25, "Dessus plat et dru, comme un tapis. Ça impressionne."),
            new Entry("Banane", 35, "Une vague devant, gominée. Style d'avant, toujours chic."),
            new Entry("Plaqué en arrière", 30, "Tout en arrière, brillant. Façon homme d'affaires."),
            new Entry("Iroquoise", 45, "Une crête au milieu, côtés à blanc. On te remarque."),
            new Entry("Afro", 40, "Du volume, bien rond, bien taillé."),
            new Entry("Mi-longs", 35, "Jusqu'à la nuque, un peu en bataille."),
            new Entry("Chignon", 38, "Plaqué, noué derrière. Pratique pour cogner.")
        };

        public static readonly Entry[] Beards =
        {
            new Entry("Rasé de près", 8, "Serviette chaude, coupe-chou. Il reste l'ombre de trois jours."),
            new Entry("Moustache", 10, "Taillée net au-dessus de la lèvre."),
            new Entry("Bouc", 12, "Moustache et menton, joues nettes."),
            new Entry("Barbe courte", 15, "Taillée court, bien dessinée sur les joues."),
            new Entry("Barbe fournie", 18, "Épaisse, jusque sous la mâchoire. Ça cache les bleus.")
        };

        public struct Tint
        {
            public string Name;
            public Color Color;
            public int Price;

            public Tint(string name, Color color, int price)
            {
                Name = name;
                Color = color;
                Price = price;
            }
        }

        public static readonly Tint[] Colors =
        {
            new Tint("Noir", new Color(0.045f, 0.038f, 0.034f), 0),
            new Tint("Brun", new Color(0.13f, 0.075f, 0.045f), 0),
            new Tint("Châtain", new Color(0.27f, 0.16f, 0.085f), 0),
            new Tint("Roux", new Color(0.52f, 0.2f, 0.07f), 25),
            new Tint("Blond foncé", new Color(0.46f, 0.33f, 0.18f), 25),
            new Tint("Blond", new Color(0.74f, 0.6f, 0.38f), 30),
            new Tint("Platine", new Color(0.88f, 0.86f, 0.8f), 45),
            new Tint("Gris", new Color(0.45f, 0.44f, 0.43f), 20),
            new Tint("Bleu électrique", new Color(0.06f, 0.22f, 0.8f), 50),
            new Tint("Rose Über", new Color(0.88f, 0.16f, 0.5f), 60)
        };

        public static Color ColorOf(int index)
        {
            return Colors[Mathf.Clamp(index, 0, Colors.Length - 1)].Color;
        }
    }

    /// <summary>
    /// Les maillages des coupes et des barbes, construits à la demande sur le profil d'un crâne
    /// (<see cref="HairProfile"/>) et partagés : tous les personnages d'un même corps et d'une
    /// même coupe utilisent le même maillage.
    ///
    /// Une coupe est une coque : de la ligne d'implantation (le front, les tempes, le dessus
    /// des oreilles, la nuque) jusqu'au sommet, posée sur la peau, plus ou moins épaisse selon
    /// l'endroit — c'est cette épaisseur qui fait la coupe (le volume d'une banane, la crête
    /// d'une iroquoise, les côtés rasés d'un dégradé). Elle s'amincit à zéro sur son bord : pas
    /// de marche entre la peau et les cheveux.
    ///
    /// Dans chaque sommet : uv = (tour de tête, du sommet au bord), couleur.r = proximité du
    /// bord (0 au milieu, 1 au bord), couleur.a = densité (1 plein, peu : rasé). La tangente
    /// suit le sens des mèches. Le shader des cheveux (UberBagarre/Cheveux) en tire les mèches,
    /// les reflets et un bord effrangé.
    /// </summary>
    public static class HairBuilder
    {
        private static readonly Dictionary<string, Mesh> Cache = new Dictionary<string, Mesh>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Cache.Clear();
        }

        // La ligne d'implantation : azimut (|θ|, degrés) → hauteur φ du bord.
        private static readonly float[] LineTheta = { 0, 25, 40, 52, 62, 70, 78, 86, 94, 104, 116, 128, 145, 165, 180 };
        private static readonly float[] LinePhi = { 56, 57, 62, 72, 86, 108, 118, 96, 86, 86, 104, 122, 134, 140, 141 };

        // Les cheveux longs couvrent les oreilles.
        private static readonly float[] EarsTheta = { 60, 80, 100, 130, 180 };
        private static readonly float[] EarsPhi = { 0, 122, 128, 132, 141 };

        public static Mesh Hair(HairProfile profile, HairCut cut, bool detailed)
        {
            if (profile == null || !profile.IsValid || cut == HairCut.Rase) return null;
            string key = profile.GetInstanceID() + "/h/" + (int)cut + (detailed ? "+" : "-");
            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            mesh = BuildHair(profile, cut, detailed);
            Cache[key] = mesh;
            return mesh;
        }

        public static Mesh Beard(HairProfile profile, BeardStyle beard)
        {
            if (profile == null || !profile.IsValid || beard == BeardStyle.Aucune) return null;
            string key = profile.GetInstanceID() + "/b/" + (int)beard;
            Mesh mesh;
            if (Cache.TryGetValue(key, out mesh) && mesh != null) return mesh;
            mesh = BuildBeard(profile, beard);
            Cache[key] = mesh;
            return mesh;
        }

        // ------------------------------------------------------------------ outils

        private static float Smooth(float e0, float e1, float x)
        {
            float t = Mathf.Clamp01((x - e0) / (e1 - e0));
            return t * t * (3f - 2f * t);
        }

        private static float Interp(float x, float[] xs, float[] ys)
        {
            if (x <= xs[0]) return ys[0];
            for (int i = 1; i < xs.Length; i++)
            {
                if (x <= xs[i]) return Mathf.Lerp(ys[i - 1], ys[i], (x - xs[i - 1]) / (xs[i] - xs[i - 1]));
            }

            return ys[ys.Length - 1];
        }

        private static float Hash(float i, float j, float seed)
        {
            return Mathf.Repeat(Mathf.Sin(i * 127.1f + j * 311.7f + seed * 74.7f) * 43758.5453f, 1f);
        }

        private static float Noise(float x, float y, float seed)
        {
            float i = Mathf.Floor(x), j = Mathf.Floor(y);
            float fx = x - i, fy = y - j;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            float a = Mathf.Lerp(Hash(i, j, seed), Hash(i + 1f, j, seed), fx);
            float b = Mathf.Lerp(Hash(i, j + 1f, seed), Hash(i + 1f, j + 1f, seed), fx);
            return Mathf.Lerp(a, b, fy);
        }

        /// <summary>La hauteur φ du bord des cheveux à l'azimut θ.</summary>
        private static float Hairline(float thetaDeg, HairCut cut)
        {
            float a = Mathf.Abs(Mathf.Repeat(thetaDeg + 180f, 360f) - 180f);
            float p = Interp(a, LineTheta, LinePhi);
            if (cut == HairCut.MiLong) p = Mathf.Max(p, Interp(a, EarsTheta, EarsPhi));
            if (cut == HairCut.Afro) p += 4f;
            return p;
        }

        /// <summary>L'épaisseur (mètres) et la densité de la coupe à cet endroit.</summary>
        private static void Shape(HairCut cut, float th, float s, float ph, out float t, out float alpha)
        {
            float a = Mathf.Abs(th);
            alpha = 1f;
            switch (cut)
            {
                case HairCut.Boule:
                    t = 0.0025f;
                    alpha = 0.82f;
                    break;

                case HairCut.Courte:
                {
                    float side = Smooth(35f, 80f, a);
                    t = (0.011f * (1f - side) + 0.006f * side) * (1f - Smooth(0.9f, 1f, s) * 0.7f);
                    break;
                }

                case HairCut.Degrade:
                {
                    float side = Smooth(30f, 70f, a);
                    float low = Smooth(0.45f, 0.95f, s) * side;
                    t = (0.016f * (1f - low) + 0.0015f * low) * (1f - Smooth(0.92f, 1f, s) * 0.6f);
                    alpha = 1f - low * 0.75f;
                    break;
                }

                case HairCut.Brosse:
                {
                    float side = Smooth(35f, 75f, a);
                    t = 0.004f + 0.02f * (1f - Smooth(0.15f, 0.75f, s)) * (1f - 0.3f * side);
                    break;
                }

                case HairCut.Banane:
                {
                    float front = Mathf.Exp(-(a / 38f) * (a / 38f)) * Smooth(0.15f, 0.55f, s) * (1f - Smooth(0.9f, 1f, s));
                    float side = Smooth(40f, 80f, a);
                    t = 0.008f * (1f - side) + 0.004f * side + 0.045f * front;
                    break;
                }

                case HairCut.Plaque:
                case HairCut.Chignon:
                {
                    float side = Smooth(40f, 90f, a);
                    t = (0.010f - 0.004f * side) * (1f - Smooth(0.93f, 1f, s) * 0.5f);
                    break;
                }

                case HairCut.Iroquoise:
                {
                    float on = 1f - Smooth(10f, 16f, Mathf.Abs(Mathf.Sin(th * Mathf.Deg2Rad)) * ph);
                    float spikes = 0.75f + 0.5f * Noise(ph / 9f, th / 7f, 3f);
                    t = 0.002f + 0.055f * on * spikes * (1f - Smooth(0.85f, 1f, s));
                    alpha = 0.35f + 0.65f * on;
                    break;
                }

                case HairCut.Afro:
                {
                    float n = Noise(th / 14f, ph / 12f, 5f) * 0.5f + Noise(th / 6f, ph / 6f, 9f) * 0.5f;
                    t = (0.042f + 0.012f * n) * (1f - Smooth(0.82f, 1f, s) * 0.85f);
                    break;
                }

                case HairCut.MiLong:
                    t = (0.014f + 0.01f * Smooth(100f, 170f, a) * Smooth(0.5f, 0.95f, s)) * (1f - Smooth(0.95f, 1f, s) * 0.5f);
                    break;

                default:
                    t = 0.008f;
                    break;
            }

            // Le bord s'amincit jusqu'à la peau.
            t *= 1f - Smooth(0.84f, 1f, s);
        }

        // ------------------------------------------------------------------ coupes

        private static Mesh BuildHair(HairProfile profile, HairCut cut, bool detailed)
        {
            int columns = detailed ? 96 : 56;
            int rows = detailed ? 40 : 22;
            const float Lift = 0.0012f;

            // Un sommet au pôle (le haut du crâne), puis des anneaux du sommet jusqu'au bord.
            int count = 1 + columns * (rows - 1);
            Vector3[] vertices = new Vector3[count];
            Vector2[] uvs = new Vector2[count];
            Color[] colors = new Color[count];
            Vector4[] tangents = new Vector4[count];

            float t0, a0;
            Shape(cut, 0f, 0f, 0f, out t0, out a0);
            vertices[0] = profile.Point(0f, 0f, Lift + t0);
            uvs[0] = new Vector2(0f, 0f);
            colors[0] = new Color(0f, 0f, 0f, a0);

            for (int i = 0; i < columns; i++)
            {
                float th = -180f + i * 360f / columns;
                float line = Hairline(th, cut);
                for (int j = 1; j < rows; j++)
                {
                    float s = j / (float)(rows - 1);
                    float ph = s * line;
                    float t, alpha;
                    Shape(cut, th, s, ph, out t, out alpha);

                    int v = 1 + i * (rows - 1) + (j - 1);
                    vertices[v] = profile.Point(th, ph, Lift + t);
                    uvs[v] = new Vector2(i / (float)columns, s);
                    colors[v] = new Color(Smooth(0.7f, 1f, s), 0f, 0f, alpha);
                }
            }

            List<int> triangles = new List<int>(columns * (rows - 1) * 6);
            for (int i = 0; i < columns; i++)
            {
                int i1 = (i + 1) % columns;
                // θ croît vers la droite (x+), le bord est en bas : faces tournées vers l'extérieur
                // (sens horaire vu de dehors, la règle d'Unity).
                triangles.Add(0);
                triangles.Add(1 + i * (rows - 1));
                triangles.Add(1 + i1 * (rows - 1));

                for (int j = 1; j < rows - 1; j++)
                {
                    int a = 1 + i * (rows - 1) + (j - 1);
                    int b = 1 + i1 * (rows - 1) + (j - 1);
                    triangles.Add(a);
                    triangles.Add(b + 1);
                    triangles.Add(b);
                    triangles.Add(a);
                    triangles.Add(a + 1);
                    triangles.Add(b + 1);
                }
            }

            // Les mèches vont du sommet vers le bord.
            for (int i = 0; i < columns; i++)
            {
                for (int j = 1; j < rows; j++)
                {
                    int v = 1 + i * (rows - 1) + (j - 1);
                    Vector3 prev = j == 1 ? vertices[0] : vertices[v - 1];
                    Vector3 next = j == rows - 1 ? vertices[v] : vertices[v + 1];
                    Vector3 dir = (next - prev).normalized;
                    tangents[v] = new Vector4(dir.x, dir.y, dir.z, 1f);
                }
            }

            Vector3 back = profile.toHead * Vector3.back;
            tangents[0] = new Vector4(back.x, back.y, back.z, 1f);

            Mesh mesh = new Mesh();
            mesh.name = "Cheveux " + cut + (detailed ? "" : " (loin)");

            if (cut == HairCut.Chignon)
            {
                AppendBun(profile, ref vertices, ref uvs, ref colors, ref tangents, triangles);
            }

            mesh.vertices = vertices;
            mesh.uv = uvs;
            mesh.colors = colors;
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.tangents = tangents;
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Le chignon : une boule un peu aplatie, nouée derrière le haut du crâne.</summary>
        private static void AppendBun(HairProfile profile, ref Vector3[] vertices, ref Vector2[] uvs, ref Color[] colors,
            ref Vector4[] tangents, List<int> triangles)
        {
            const int Lon = 18;
            const int Lat = 10;
            Vector3 center = profile.Point(180f, 86f, 0.04f);
            Vector3 outward = (center - profile.center).normalized;
            Quaternion frame = Quaternion.LookRotation(outward, profile.toHead * Vector3.up);
            Vector3 radius = new Vector3(0.036f, 0.032f, 0.03f);

            int start = vertices.Length;
            int added = (Lat + 1) * (Lon + 1);
            System.Array.Resize(ref vertices, start + added);
            System.Array.Resize(ref uvs, start + added);
            System.Array.Resize(ref colors, start + added);
            System.Array.Resize(ref tangents, start + added);

            for (int y = 0; y <= Lat; y++)
            {
                float v = y / (float)Lat;
                float lat = Mathf.Lerp(-90f, 90f, v) * Mathf.Deg2Rad;
                for (int x = 0; x <= Lon; x++)
                {
                    float u = x / (float)Lon;
                    float lon = u * Mathf.PI * 2f;
                    Vector3 p = new Vector3(Mathf.Cos(lat) * Mathf.Cos(lon) * radius.x, Mathf.Sin(lat) * radius.y,
                        Mathf.Cos(lat) * Mathf.Sin(lon) * radius.z);
                    int k = start + y * (Lon + 1) + x;
                    vertices[k] = center + frame * p;
                    uvs[k] = new Vector2(u * 0.5f, v);
                    colors[k] = new Color(0.2f, 0f, 0f, 1f);
                    Vector3 along = frame * new Vector3(-Mathf.Sin(lon), 0f, Mathf.Cos(lon));
                    tangents[k] = new Vector4(along.x, along.y, along.z, 1f);
                }
            }

            for (int y = 0; y < Lat; y++)
            {
                for (int x = 0; x < Lon; x++)
                {
                    int a = start + y * (Lon + 1) + x;
                    int b = a + Lon + 1;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(a + 1);
                    triangles.Add(a + 1);
                    triangles.Add(b);
                    triangles.Add(b + 1);
                }
            }
        }

        // ------------------------------------------------------------------ barbes

        /// <summary>Où pousse la barbe (0 à 1), sur la grille du visage.</summary>
        private static float BeardCover(BeardStyle beard, float th, float ph)
        {
            float a = Mathf.Abs(th);
            float moustache = (1f - Smooth(24f, 30f, a)) * Smooth(125f, 127.5f, ph) * (1f - Smooth(131f, 133f, ph));
            switch (beard)
            {
                case BeardStyle.Moustache:
                    return moustache;

                case BeardStyle.Bouc:
                {
                    float chin = (1f - Smooth(20f, 27f, a)) * Smooth(139f, 141f, ph) * (1f - Smooth(155f, 159f, ph));
                    float corners = (1f - Smooth(26f, 30f, a)) * Smooth(128f, 130f, ph) * (1f - Smooth(141f, 143f, ph)) * Smooth(18f, 22f, a);
                    return Mathf.Max(moustache, Mathf.Max(chin, corners));
                }

                default:
                {
                    // Le haut des joues : des pattes (|θ| = 82°) aux commissures des lèvres.
                    float cheek = Interp(a, new float[] { 0, 24, 45, 65, 82, 100 }, new float[] { 129, 131, 126, 121, 116, 112 });
                    float low = Interp(a, new float[] { 0, 20, 40, 60, 80, 100 }, new float[] { 160, 158, 153, 146, 138, 130 });
                    if (beard == BeardStyle.Fournie) low += 4f;
                    float lips = (1f - Smooth(20f, 26f, a)) * Smooth(131.5f, 133f, ph) * (1f - Smooth(139f, 141f, ph));
                    float c = Smooth(cheek - 1.5f, cheek + 1.5f, ph) * (1f - Smooth(94f, 102f, a)) * (1f - Smooth(low - 3f, low, ph));
                    return c * (1f - lips);
                }
            }
        }

        private static float BeardThickness(BeardStyle beard)
        {
            switch (beard)
            {
                case BeardStyle.Moustache: return 0.0028f;
                case BeardStyle.Bouc: return 0.004f;
                case BeardStyle.Courte: return 0.0035f;
                default: return 0.010f;
            }
        }

        private static Mesh BuildBeard(HairProfile profile, BeardStyle beard)
        {
            const int Columns = 80;
            const int Rows = 44;
            const float ThetaRange = 104f;
            const float PhiFrom = 108f, PhiTo = 168f;

            float[,] cover = new float[Columns, Rows];
            for (int i = 0; i < Columns; i++)
            {
                float th = Mathf.Lerp(-ThetaRange, ThetaRange, i / (Columns - 1f));
                for (int j = 0; j < Rows; j++)
                {
                    float ph = Mathf.Lerp(PhiFrom, PhiTo, j / (Rows - 1f));
                    cover[i, j] = BeardCover(beard, th, ph);
                }
            }

            int[,] index = new int[Columns, Rows];
            List<Vector3> vertices = new List<Vector3>();
            List<Vector2> uvs = new List<Vector2>();
            List<Color> colors = new List<Color>();
            List<Vector4> tangents = new List<Vector4>();
            float thickness = BeardThickness(beard);

            for (int i = 0; i < Columns; i++)
            {
                for (int j = 0; j < Rows; j++)
                {
                    index[i, j] = -1;
                    bool used = false;
                    for (int di = -1; di <= 1 && !used; di++)
                    {
                        for (int dj = -1; dj <= 1 && !used; dj++)
                        {
                            int x = i + di, y = j + dj;
                            if (x >= 0 && y >= 0 && x < Columns && y < Rows && cover[x, y] > 0.01f) used = true;
                        }
                    }

                    if (!used) continue;

                    float th = Mathf.Lerp(-ThetaRange, ThetaRange, i / (Columns - 1f));
                    float ph = Mathf.Lerp(PhiFrom, PhiTo, j / (Rows - 1f));
                    float c = cover[i, j];
                    index[i, j] = vertices.Count;
                    vertices.Add(profile.Point(th, ph, 0.0008f + thickness * Smooth(0f, 0.6f, c)));
                    uvs.Add(new Vector2((th + 180f) / 360f, (ph - PhiFrom) / (PhiTo - PhiFrom)));
                    colors.Add(new Color(1f - c, 0f, 0f, c));
                    Vector3 down = (profile.Point(th, ph + 2f, 0f) - profile.Point(th, ph - 2f, 0f)).normalized;
                    tangents.Add(new Vector4(down.x, down.y, down.z, 1f));
                }
            }

            List<int> triangles = new List<int>();
            for (int i = 0; i < Columns - 1; i++)
            {
                for (int j = 0; j < Rows - 1; j++)
                {
                    int a = index[i, j], b = index[i + 1, j], c = index[i + 1, j + 1], d = index[i, j + 1];
                    if (a < 0 || b < 0 || c < 0 || d < 0) continue;
                    if (Mathf.Max(Mathf.Max(cover[i, j], cover[i + 1, j]), Mathf.Max(cover[i + 1, j + 1], cover[i, j + 1])) <= 0.01f) continue;

                    // θ croît vers la droite (x+), φ vers le bas : faces tournées vers l'extérieur.
                    triangles.Add(a);
                    triangles.Add(d);
                    triangles.Add(c);
                    triangles.Add(a);
                    triangles.Add(c);
                    triangles.Add(b);
                }
            }

            Mesh mesh = new Mesh();
            mesh.name = "Barbe " + beard;
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uvs);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.SetTangents(tangents);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
