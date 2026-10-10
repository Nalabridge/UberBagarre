using System.Collections.Generic;
using UberBagarre.View;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Mesure le crâne de chaque corps pour les coupes de cheveux (<see cref="HairProfile"/>) et
    /// pose les cheveux sur les personnages construits.
    ///
    /// La mesure : un centre de tête (la sphère qui épouse au mieux le haut du crâne), puis,
    /// dans chaque direction d'une grille de 72 × 48, un rayon lancé vers l'extérieur contre la
    /// peau de la tête ; on garde la distance du point le plus loin (la surface extérieure).
    /// Calculée une fois par corps, rangée à côté des maillages générés, refaite si le fichier
    /// du corps change.
    /// </summary>
    public static class HairProfiles
    {
        private const string MaterialName = "M_Cheveux";
        private static readonly Dictionary<string, HairProfile> Loaded = new Dictionary<string, HairProfile>();
        private static int _seed;

        public static HairProfile For(CorpsImporter.Data data)
        {
            if (data == null) return null;
            string path = CorpsImporter.GeneratedFolder + "/Crane_" + data.Name + ".asset";
            long stamp = data.Stamp.Ticks;

            HairProfile profile;
            if (Loaded.TryGetValue(path, out profile) && profile != null && profile.IsValid && profile.sourceStamp == stamp) return profile;

            EditorBuildUtility.EnsureFolder(CorpsImporter.GeneratedFolder);
            profile = AssetDatabase.LoadAssetAtPath<HairProfile>(path);
            if (profile != null && profile.IsValid && profile.sourceStamp == stamp)
            {
                Loaded[path] = profile;
                return profile;
            }

            bool created = profile == null;
            if (created) profile = ScriptableObject.CreateInstance<HairProfile>();
            if (!Measure(data, profile))
            {
                if (created) Object.DestroyImmediate(profile);
                return null;
            }

            profile.sourceStamp = stamp;
            if (created) AssetDatabase.CreateAsset(profile, path);
            EditorUtility.SetDirty(profile);
            Loaded[path] = profile;
            return profile;
        }

        public static Material HairMaterial()
        {
            Material material = EditorBuildUtility.CreateOrUpdateEffectMaterial(CorpsImporter.MaterialsFolder, MaterialName, "UberBagarre/Cheveux");
            if (material == null) return null;
            material.SetColor("_Color", new Color(0.045f, 0.038f, 0.034f));
            material.SetFloat("_Strands", 420f);
            material.SetFloat("_Shine", 0.8f);
            material.SetFloat("_Variation", 0.45f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Coiffe un corps construit. Sans coupe imposée, elle est tirée au sort (de façon
        /// reproductible : la même scène reconstruite garde les mêmes têtes).
        /// </summary>
        public static HairRig Attach(GameObject owner, CorpsImporter.Data data, Transform head, bool detailed, bool shadowsOnly,
            bool chosen, HairCut cut, BeardStyle beard, Color color)
        {
            if (owner == null || head == null) return null;
            HairProfile profile = For(data);
            Material material = HairMaterial();
            if (profile == null || material == null) return null;

            if (!chosen) Pick(owner.name, out cut, out beard, out color);

            HairRig rig = owner.GetComponent<HairRig>();
            if (rig == null) rig = owner.AddComponent<HairRig>();
            rig.Configure(profile, head, material, cut, beard, color, detailed, shadowsOnly);
            EditorUtility.SetDirty(rig);
            return rig;
        }

        private static readonly HairCut[] CutPool =
        {
            HairCut.Courte, HairCut.Courte, HairCut.Courte, HairCut.Courte, HairCut.Courte,
            HairCut.Degrade, HairCut.Degrade, HairCut.Degrade, HairCut.Degrade,
            HairCut.Boule, HairCut.Boule, HairCut.Boule,
            HairCut.Rase, HairCut.Rase,
            HairCut.Plaque, HairCut.Plaque,
            HairCut.Brosse, HairCut.Banane, HairCut.Afro, HairCut.Afro,
            HairCut.MiLong, HairCut.MiLong, HairCut.Chignon, HairCut.Iroquoise
        };

        private static readonly BeardStyle[] BeardPool =
        {
            BeardStyle.Aucune, BeardStyle.Aucune, BeardStyle.Aucune, BeardStyle.Aucune, BeardStyle.Aucune,
            BeardStyle.Courte, BeardStyle.Courte, BeardStyle.Courte,
            BeardStyle.Bouc, BeardStyle.Bouc, BeardStyle.Moustache,
            BeardStyle.Fournie, BeardStyle.Fournie
        };

        private static readonly int[] ColorPool = { 0, 0, 0, 0, 0, 0, 0, 1, 1, 1, 1, 1, 1, 2, 2, 2, 2, 4, 4, 5, 3, 7, 7 };

        private static void Pick(string name, out HairCut cut, out BeardStyle beard, out Color color)
        {
            System.Random random = new System.Random(unchecked(Stable(name) * 31 + _seed++ * 7919));
            cut = CutPool[random.Next(CutPool.Length)];
            beard = BeardPool[random.Next(BeardPool.Length)];
            int tint = ColorPool[random.Next(ColorPool.Length)];

            // Une légère variation de teinte : deux bruns ne sont jamais tout à fait les mêmes.
            Color c = HairCatalog.ColorOf(tint);
            float shade = 0.85f + (float)random.NextDouble() * 0.3f;
            color = new Color(c.r * shade, c.g * shade, c.b * shade, 1f);
        }

        private static int Stable(string text)
        {
            unchecked
            {
                int h = 23;
                for (int i = 0; text != null && i < text.Length; i++) h = h * 31 + text[i];
                return h;
            }
        }

        // ------------------------------------------------------------------ mesure

        private static bool Measure(CorpsImporter.Data data, HairProfile profile)
        {
            int[] triangles;
            // La tête : le visage UMA (Visage.32), ou la peau de la tête des anciens corps (Peau.32).
            if (data.Submeshes == null) return false;
            if (!data.Submeshes.TryGetValue("Visage.32", out triangles) && !data.Submeshes.TryGetValue("Peau.32", out triangles)) return false;
            if (triangles.Length < 300) return false;

            int head = data.Bone("Head");
            int eyes = data.Bone("Yeux");
            if (head < 0) return false;
            float eyeHeight = eyes >= 0 ? data.BindPositions[eyes].y : data.BindPositions[head].y + 0.04f;

            // Le centre : la sphère qui épouse le haut du crâne (au-dessus des yeux).
            HashSet<int> used = new HashSet<int>(triangles);
            List<Vector3> top = new List<Vector3>();
            foreach (int v in used)
            {
                if (data.Positions[v].y > eyeHeight + 0.01f) top.Add(data.Positions[v]);
            }

            Vector3 center;
            if (!FitSphere(top, out center)) return false;

            // Direction de chaque triangle vu du centre : on ne teste vraiment que ceux qui sont
            // dans un cône autour du rayon.
            int count = triangles.Length / 3;
            Vector3[] dirs = new Vector3[count];
            for (int t = 0; t < count; t++)
            {
                Vector3 c = (data.Positions[triangles[t * 3]] + data.Positions[triangles[t * 3 + 1]] + data.Positions[triangles[t * 3 + 2]]) / 3f;
                dirs[t] = (c - center).normalized;
            }

            int columns = HairProfile.Columns, rows = HairProfile.Rows;
            float[] radii = new float[columns * rows];
            byte[] hits = new byte[columns * rows];
            for (int i = 0; i < columns; i++)
            {
                float th = HairProfile.ThetaOf(i);
                for (int j = 0; j < rows; j++)
                {
                    Vector3 d = HairProfile.Direction(th, HairProfile.PhiOf(j));
                    float best = -1f;
                    for (int t = 0; t < count; t++)
                    {
                        if (Vector3.Dot(dirs[t], d) < 0.82f) continue;
                        float hit = Ray(center, d, data.Positions[triangles[t * 3]], data.Positions[triangles[t * 3 + 1]],
                            data.Positions[triangles[t * 3 + 2]]);
                        if (hit > best) best = hit;
                    }

                    radii[i * rows + j] = best;
                    hits[i * rows + j] = (byte)(best > 0f ? 1 : 0);
                }

                // Les directions sans peau (sous le menton, dans le cou) : la dernière valeur trouvée.
                FillColumn(radii, hits, i * rows, rows);
            }

            Quaternion toHead = Quaternion.Inverse(data.BindRotations[head]);
            profile.center = toHead * (center - data.BindPositions[head]);
            profile.toHead = toHead;
            profile.radii = radii;
            profile.hits = hits;
            return true;
        }

        private static void FillColumn(float[] radii, byte[] hits, int start, int rows)
        {
            int first = -1;
            for (int j = 0; j < rows; j++)
            {
                if (hits[start + j] == 0) continue;
                first = j;
                break;
            }

            if (first < 0)
            {
                for (int j = 0; j < rows; j++) radii[start + j] = 0.09f;
                return;
            }

            for (int j = 0; j < first; j++) radii[start + j] = radii[start + first];
            int last = first;
            for (int j = first + 1; j < rows; j++)
            {
                if (hits[start + j] == 0) continue;
                for (int k = last + 1; k < j; k++)
                {
                    radii[start + k] = Mathf.Lerp(radii[start + last], radii[start + j], (k - last) / (float)(j - last));
                }

                last = j;
            }

            for (int j = last + 1; j < rows; j++) radii[start + j] = radii[start + last];
        }

        /// <summary>Möller–Trumbore : la distance le long du rayon, ou -1.</summary>
        private static float Ray(Vector3 origin, Vector3 dir, Vector3 v0, Vector3 v1, Vector3 v2)
        {
            Vector3 e1 = v1 - v0;
            Vector3 e2 = v2 - v0;
            Vector3 h = Vector3.Cross(dir, e2);
            float a = Vector3.Dot(e1, h);
            if (Mathf.Abs(a) < 1e-12f) return -1f;
            float f = 1f / a;
            Vector3 s = origin - v0;
            float u = f * Vector3.Dot(s, h);
            if (u < 0f || u > 1f) return -1f;
            Vector3 q = Vector3.Cross(s, e1);
            float v = f * Vector3.Dot(dir, q);
            if (v < 0f || u + v > 1f) return -1f;
            float t = f * Vector3.Dot(e2, q);
            return t > 0f ? t : -1f;
        }

        /// <summary>Moindres carrés : |p - c|² = r² pour tous les points.</summary>
        private static bool FitSphere(List<Vector3> points, out Vector3 center)
        {
            center = Vector3.zero;
            if (points.Count < 10) return false;

            double[,] m = new double[4, 5];
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 p = points[i];
                double[] row = { 2.0 * p.x, 2.0 * p.y, 2.0 * p.z, 1.0 };
                double f = (double)p.x * p.x + (double)p.y * p.y + (double)p.z * p.z;
                for (int r = 0; r < 4; r++)
                {
                    for (int c = 0; c < 4; c++) m[r, c] += row[r] * row[c];
                    m[r, 4] += row[r] * f;
                }
            }

            // Gauss-Jordan sur le système 4 × 4.
            for (int col = 0; col < 4; col++)
            {
                int pivot = col;
                for (int r = col + 1; r < 4; r++)
                {
                    if (System.Math.Abs(m[r, col]) > System.Math.Abs(m[pivot, col])) pivot = r;
                }

                if (System.Math.Abs(m[pivot, col]) < 1e-12) return false;
                for (int c = 0; c < 5; c++)
                {
                    double tmp = m[col, c];
                    m[col, c] = m[pivot, c];
                    m[pivot, c] = tmp;
                }

                for (int r = 0; r < 4; r++)
                {
                    if (r == col) continue;
                    double k = m[r, col] / m[col, col];
                    for (int c = col; c < 5; c++) m[r, c] -= k * m[col, c];
                }
            }

            center = new Vector3((float)(m[0, 4] / m[0, 0]), (float)(m[1, 4] / m[1, 1]), (float)(m[2, 4] / m[2, 2]));
            return true;
        }
    }
}
