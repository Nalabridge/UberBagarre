using System;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les textures des intérieurs, calculées pixel par pixel : carrelage, damier, parquet,
    /// brique, plâtre peint, moquette, dalles de plafond, bois de meuble, béton, lino, lambris,
    /// métal brossé, papier peint, feutre, cuir.
    ///
    /// Chacune se répète sans couture et couvre une taille réelle (<see cref="Meters"/>) : le
    /// shader des intérieurs la plaque au mètre près, un carreau fait 30 cm partout. Le canal
    /// alpha porte la brillance (le joint est mat, le carreau brille) et la hauteur sert au
    /// relief (le joint est en creux, la brique en saillie).
    ///
    /// Les couleurs sont souvent neutres (le matériau les teinte) ; la brique et le parquet
    /// portent les leurs.
    /// </summary>
    public static class InteriorTextures
    {
        public sealed class Result
        {
            public int Size;
            public Color[] Albedo;
            public float[] Height;
            public float Meters;
            public float Relief = 2f;
        }

        public static readonly string[] Styles =
        {
            "carrelage", "damier", "parquet", "brique", "platre", "moquette", "dalles", "bois", "beton", "lino", "lambris",
            "metal", "papierpeint", "feutre", "cuir"
        };

        public static Result Make(string style, int size)
        {
            Result r = new Result { Size = size, Albedo = new Color[size * size], Height = new float[size * size] };
            switch (style)
            {
                case "carrelage": Tiles(r, 4, 0.035f, 0.05f, 1.2f); break;
                case "damier": Checker(r); break;
                case "parquet": Planks(r); break;
                case "brique": Bricks(r); break;
                case "platre": Plaster(r); break;
                case "moquette": Carpet(r); break;
                case "dalles": CeilingTiles(r); break;
                case "bois": Grain(r, 1f); break;
                case "beton": Concrete(r); break;
                case "lino": Linoleum(r); break;
                case "lambris": Paneling(r); break;
                case "metal": Brushed(r); break;
                case "papierpeint": Wallpaper(r); break;
                case "feutre": Felt(r); break;
                case "cuir": Leather(r); break;
                default: Plaster(r); break;
            }

            return r;
        }

        // ================================================================== motifs

        /// <summary>Des carreaux carrés, joint gris mat, chaque carreau très légèrement différent.</summary>
        private static void Tiles(Result r, int count, float grout, float variation, float meters)
        {
            int n = r.Size;
            r.Meters = meters;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * count, v = y / (float)n * count;
                    int cx = (int)u, cy = (int)v;
                    float fx = u - cx, fy = v - cy;
                    float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
                    float tone = 1f - variation * Hash(cx, cy, 3) + Fbm(x, y, n, 8, 2) * 0.03f;
                    bool joint = edge < grout * 0.5f;
                    // Un léger bombé sur le bord du carreau (biseau).
                    float bevel = Mathf.Clamp01((edge - grout * 0.5f) / (grout * 0.6f));
                    Color tile = Grey(0.92f * tone);
                    Color mortar = Grey(0.55f + Fbm(x, y, n, 32, 2) * 0.08f);
                    Color c = joint ? mortar : tile;
                    c.a = joint ? 0.25f : 0.85f + 0.15f * bevel;
                    Set(r, x, y, c, joint ? 0f : 0.6f + 0.4f * bevel);
                }
            }
        }

        private static void Checker(Result r)
        {
            int n = r.Size;
            r.Meters = 0.6f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * 2f, v = y / (float)n * 2f;
                    int cx = (int)u, cy = (int)v;
                    float fx = u - cx, fy = v - cy;
                    float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
                    bool dark = (cx + cy) % 2 == 1;
                    bool joint = edge < 0.012f;
                    float wear = Fbm(x, y, n, 12, 3) * 0.05f;
                    Color c = joint ? Grey(0.45f) : dark ? Grey(0.07f + wear) : Grey(0.9f - wear);
                    c.a = joint ? 0.2f : 0.9f;
                    Set(r, x, y, c, joint ? 0f : 1f);
                }
            }
        }

        /// <summary>Des lames de parquet : huit rangs, des longueurs décalées, du fil de bois, des tons qui varient.</summary>
        private static void Planks(Result r)
        {
            int n = r.Size;
            r.Meters = 1.6f;
            const int rows = 8;
            for (int y = 0; y < n; y++)
            {
                float v = y / (float)n * rows;
                int row = (int)v;
                float fy = v - row;
                float offset = Hash(row, 7, 1);
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * 2f + offset * 2f;
                    int plank = (int)Mathf.Floor(u);
                    float fx = u - plank;
                    // Deux lames par rang : l'indice « modulo 2 » garde la texture sans couture.
                    int p = ((plank % 2) + 2) % 2;
                    bool gap = fy < 0.03f || fx < 0.008f;
                    float tone = 0.8f + 0.35f * Hash(p, row, 5);
                    float grain = Fbm(x, y, n, 2, 2);
                    float rings = 0.5f + 0.5f * Mathf.Sin(y / (float)n * Mathf.PI * 2f * 24f + grain * 9f + p * 3.3f + row * 1.7f);
                    Color wood = new Color(0.55f, 0.36f, 0.2f) * tone * (0.82f + 0.18f * rings);
                    Color c = gap ? new Color(0.12f, 0.08f, 0.05f) : wood;
                    c.a = gap ? 0.1f : 0.65f;
                    Set(r, x, y, c, gap ? 0f : 0.8f + rings * 0.1f);
                }
            }
        }

        /// <summary>Des briques en appareil courant (21,5 × 6,5 cm), joint de mortier d'un centimètre.</summary>
        private static void Bricks(Result r)
        {
            int n = r.Size;
            r.Meters = 0.9f;
            r.Relief = 3f;
            float brickW = 0.225f / 0.9f;
            float brickH = 0.075f / 0.9f;
            float mortar = 0.01f / 0.9f;
            for (int y = 0; y < n; y++)
            {
                float v = y / (float)n;
                int row = Mathf.FloorToInt(v / brickH);
                float fy = v / brickH - row;
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n + (row % 2 == 0 ? 0f : brickW * 0.5f);
                    int col = Mathf.FloorToInt(u / brickW);
                    float fx = u / brickW - col;
                    bool joint = fy * brickH < mortar || fx * brickW < mortar;
                    float h = Hash(col, row, 11);
                    Color brick = Color.Lerp(new Color(0.52f, 0.2f, 0.13f), new Color(0.66f, 0.33f, 0.2f), h);
                    brick *= 0.88f + Fbm(x, y, n, 24, 3) * 0.24f;
                    Color c = joint ? Grey(0.62f + Fbm(x, y, n, 40, 2) * 0.1f) : brick;
                    c.a = joint ? 0.05f : 0.25f;
                    Set(r, x, y, c, joint ? 0f : 0.7f + Fbm(x, y, n, 16, 2) * 0.3f);
                }
            }
        }

        private static void Plaster(Result r)
        {
            int n = r.Size;
            r.Meters = 2f;
            r.Relief = 0.8f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float low = Fbm(x, y, n, 3, 4);
                    float fine = Fbm(x, y, n, 64, 2);
                    Color c = Grey(0.9f + (low - 0.5f) * 0.06f + (fine - 0.5f) * 0.03f);
                    c.a = 0.45f;
                    Set(r, x, y, c, fine * 0.5f + low * 0.2f);
                }
            }
        }

        private static void Carpet(Result r)
        {
            int n = r.Size;
            r.Meters = 0.5f;
            r.Relief = 1.2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float fibre = Hash(x, y, 13);
                    float tuft = Fbm(x, y, n, 48, 2);
                    float pattern = 0.5f + 0.5f * Mathf.Sin((x + y) / (float)n * Mathf.PI * 8f);
                    Color c = Grey(0.72f + fibre * 0.12f + tuft * 0.12f + pattern * 0.04f);
                    c.a = 0.05f;
                    Set(r, x, y, c, fibre * 0.6f + tuft * 0.4f);
                }
            }
        }

        /// <summary>Les dalles d'un faux plafond (60 cm), leurs petits trous, et le rail métallique entre elles.</summary>
        private static void CeilingTiles(Result r)
        {
            int n = r.Size;
            r.Meters = 1.2f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * 2f, v = y / (float)n * 2f;
                    float fx = u - Mathf.Floor(u), fy = v - Mathf.Floor(v);
                    float edge = Mathf.Min(Mathf.Min(fx, 1f - fx), Mathf.Min(fy, 1f - fy));
                    bool rail = edge < 0.018f;
                    bool hole = Hash(x / 3, y / 3, 17) > 0.86f;
                    Color c = rail ? Grey(0.8f) : Grey(hole ? 0.72f : 0.93f - Fbm(x, y, n, 20, 2) * 0.04f);
                    c.a = rail ? 0.7f : 0.08f;
                    Set(r, x, y, c, rail ? 0.2f : hole ? 0.85f : 1f);
                }
            }
        }

        /// <summary>Le fil d'un bois de meuble : de longues veines, des nœuds discrets.</summary>
        private static void Grain(Result r, float meters)
        {
            int n = r.Size;
            r.Meters = meters;
            r.Relief = 0.6f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float warp = Fbm(x, y, n, 2, 3) * 6f;
                    float rings = 0.5f + 0.5f * Mathf.Sin(y / (float)n * Mathf.PI * 2f * 18f + warp);
                    float fine = Hash(x / 2, y, 19);
                    Color c = Grey(0.78f + rings * 0.16f + fine * 0.05f);
                    c.a = 0.6f;
                    Set(r, x, y, c, rings * 0.5f);
                }
            }
        }

        private static void Concrete(Result r)
        {
            int n = r.Size;
            r.Meters = 2f;
            r.Relief = 1f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float low = Fbm(x, y, n, 4, 4);
                    float mid = Fbm(x, y, n, 16, 3);
                    bool pore = Hash(x, y, 23) > 0.985f;
                    Color c = Grey(0.68f + (low - 0.5f) * 0.14f + (mid - 0.5f) * 0.08f - (pore ? 0.18f : 0f));
                    c.a = 0.3f;
                    Set(r, x, y, c, pore ? 0f : 0.5f + mid * 0.3f);
                }
            }
        }

        private static void Linoleum(Result r)
        {
            int n = r.Size;
            r.Meters = 1f;
            r.Relief = 0.4f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float chip = Hash(x / 2, y / 2, 29);
                    float tone = chip > 0.92f ? 0.7f : chip < 0.06f ? 1.05f : 0.9f;
                    Color c = Grey(tone + Fbm(x, y, n, 6, 3) * 0.04f);
                    c.a = 0.75f;
                    Set(r, x, y, c, 0.5f);
                }
            }
        }

        /// <summary>Un lambris de planches verticales (12 cm), rainurées.</summary>
        private static void Paneling(Result r)
        {
            int n = r.Size;
            r.Meters = 1.2f;
            const int boards = 10;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * boards;
                    int b = (int)u;
                    float fx = u - b;
                    bool groove = fx < 0.04f;
                    float warp = Fbm(x, y, n, 1, 2) * 5f;
                    float rings = 0.5f + 0.5f * Mathf.Sin(x / (float)n * Mathf.PI * 2f * 36f + warp + b * 2f);
                    Color c = groove ? Grey(0.35f) : Grey(0.8f + rings * 0.12f + Hash(b, 0, 31) * 0.1f);
                    c.a = groove ? 0.1f : 0.55f;
                    Set(r, x, y, c, groove ? 0f : 0.8f);
                }
            }
        }

        private static void Brushed(Result r)
        {
            int n = r.Size;
            r.Meters = 0.5f;
            r.Relief = 0.3f;
            for (int y = 0; y < n; y++)
            {
                float streak = Fbm(0, y, n, 1, 1);
                for (int x = 0; x < n; x++)
                {
                    float line = Hash(y, x / 24, 37);
                    Color c = Grey(0.82f + line * 0.1f + streak * 0.05f);
                    c.a = 0.85f;
                    Set(r, x, y, c, line * 0.3f);
                }
            }
        }

        private static void Wallpaper(Result r)
        {
            int n = r.Size;
            r.Meters = 1f;
            r.Relief = 0.3f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float u = x / (float)n * 8f;
                    float stripe = u - Mathf.Floor(u);
                    bool band = stripe < 0.5f;
                    float motif = Mathf.Sin(x / (float)n * Mathf.PI * 32f) * Mathf.Sin(y / (float)n * Mathf.PI * 16f);
                    Color c = Grey(band ? 0.88f + motif * 0.03f : 0.8f);
                    c.a = 0.3f;
                    Set(r, x, y, c, band ? 0.6f : 0.5f);
                }
            }
        }

        private static void Felt(Result r)
        {
            int n = r.Size;
            r.Meters = 0.3f;
            r.Relief = 0.5f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    Color c = Grey(0.85f + Hash(x, y, 41) * 0.1f + Fbm(x, y, n, 10, 2) * 0.05f);
                    c.a = 0.02f;
                    Set(r, x, y, c, Hash(x, y, 41));
                }
            }
        }

        private static void Leather(Result r)
        {
            int n = r.Size;
            r.Meters = 0.4f;
            r.Relief = 1f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float cells = Cells(x, y, n, 14);
                    float mottle = Fbm(x, y, n, 5, 3);
                    Color c = Grey(0.78f + cells * 0.12f + (mottle - 0.5f) * 0.1f);
                    c.a = 0.55f - cells * 0.25f;
                    Set(r, x, y, c, cells);
                }
            }
        }

        // ================================================================== relief

        /// <summary>
        /// La carte de normales (espace tangent, OpenGL) déduite de la hauteur : Sobel avec
        /// repli sur les bords (la texture se répète, le relief aussi).
        /// </summary>
        public static Color[] Normals(Result r)
        {
            int n = r.Size;
            float[] h = Blur(r.Height, n, 1);
            Color[] pixels = new Color[n * n];
            float strength = r.Relief * n / 256f;
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float tl = At(h, n, x - 1, y + 1), t = At(h, n, x, y + 1), tr = At(h, n, x + 1, y + 1);
                    float l = At(h, n, x - 1, y), rr = At(h, n, x + 1, y);
                    float bl = At(h, n, x - 1, y - 1), b = At(h, n, x, y - 1), br = At(h, n, x + 1, y - 1);
                    float dx = (tr + 2f * rr + br) - (tl + 2f * l + bl);
                    float dy = (tl + 2f * t + tr) - (bl + 2f * b + br);
                    float nx = -dx * strength, ny = -dy * strength, nz = 1f;
                    float len = Mathf.Sqrt(nx * nx + ny * ny + nz * nz);
                    pixels[y * n + x] = new Color(nx / len * 0.5f + 0.5f, ny / len * 0.5f + 0.5f, nz / len * 0.5f + 0.5f, 1f);
                }
            }

            return pixels;
        }

        private static float[] Blur(float[] values, int n, int radius)
        {
            float[] a = new float[values.Length];
            float[] b = new float[values.Length];
            float norm = 1f / (radius * 2 + 1);
            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += At(values, n, x + k, y);
                    a[y * n + x] = sum * norm;
                }
            }

            for (int y = 0; y < n; y++)
            {
                for (int x = 0; x < n; x++)
                {
                    float sum = 0f;
                    for (int k = -radius; k <= radius; k++) sum += At(a, n, x, y + k);
                    b[y * n + x] = sum * norm;
                }
            }

            return b;
        }

        private static float At(float[] values, int n, int x, int y)
        {
            x = ((x % n) + n) % n;
            y = ((y % n) + n) % n;
            return values[y * n + x];
        }

        // ================================================================== bruit

        private static void Set(Result r, int x, int y, Color c, float height)
        {
            c.r = Mathf.Clamp01(c.r);
            c.g = Mathf.Clamp01(c.g);
            c.b = Mathf.Clamp01(c.b);
            c.a = Mathf.Clamp01(c.a);
            r.Albedo[y * r.Size + x] = c;
            r.Height[y * r.Size + x] = height;
        }

        private static Color Grey(float v)
        {
            return new Color(v, v, v, 1f);
        }

        /// <summary>Un nombre pseudo-aléatoire stable dans [0, 1) pour (x, y, graine).</summary>
        public static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)(x * 374761393 + y * 668265263 + seed * 2147483647);
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        /// <summary>Bruit de valeur périodique (se répète sur <paramref name="n"/> pixels), <paramref name="octaves"/> octaves.</summary>
        private static float Fbm(float x, float y, int n, int frequency, int octaves)
        {
            float sum = 0f, amp = 0.5f, total = 0f;
            int f = Math.Max(1, frequency);
            for (int o = 0; o < octaves; o++)
            {
                sum += Value(x / n * f, y / n * f, f, o * 7 + 1) * amp;
                total += amp;
                amp *= 0.5f;
                f *= 2;
            }

            return sum / total;
        }

        private static float Value(float x, float y, int period, int seed)
        {
            int x0 = (int)Math.Floor(x), y0 = (int)Math.Floor(y);
            float fx = x - x0, fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int x1 = x0 + 1, y1 = y0 + 1;
            x0 = ((x0 % period) + period) % period;
            x1 = ((x1 % period) + period) % period;
            y0 = ((y0 % period) + period) % period;
            y1 = ((y1 % period) + period) % period;
            float a = Hash(x0, y0, seed), b = Hash(x1, y0, seed), c = Hash(x0, y1, seed), d = Hash(x1, y1, seed);
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Distance à la cellule de Voronoï la plus proche (grain de cuir), périodique.</summary>
        private static float Cells(int x, int y, int n, int count)
        {
            float u = x / (float)n * count, v = y / (float)n * count;
            int cx = (int)Math.Floor(u), cy = (int)Math.Floor(v);
            float best = 9f;
            for (int dy = -1; dy <= 1; dy++)
            {
                for (int dx = -1; dx <= 1; dx++)
                {
                    int gx = cx + dx, gy = cy + dy;
                    int wx = ((gx % count) + count) % count, wy = ((gy % count) + count) % count;
                    float px = gx + Hash(wx, wy, 43), py = gy + Hash(wx, wy, 47);
                    float d = (px - u) * (px - u) + (py - v) * (py - v);
                    if (d < best) best = d;
                }
            }

            return Mathf.Clamp01(Mathf.Sqrt(best));
        }
    }
}
