using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les lotissements : deux îlots de la ville où les tours laissent la place à des maisons
    /// individuelles. Chacune a son jardin devant et derrière, sa haie ou sa clôture, son allée
    /// et son garage, sa boîte aux lettres et ses poubelles ; une lampe de perron allumée chez
    /// certains, des fenêtres éclairées (et le bleu d'une télé) chez d'autres, une voiture dans
    /// l'allée une fois sur deux. De l'extérieur, une ville où les gens VIVENT — pas un décor de
    /// façades vides.
    ///
    /// Tout est généré dans le repère de la parcelle (x le long de la rue, z en s'éloignant du
    /// trottoir), puis fusionné par îlot comme le reste de la ville.
    /// </summary>
    public static partial class CityBuilder
    {
        /// <summary>Les deux îlots du sud, de part et d'autre de la planque.</summary>
        private static bool IsSuburb(int xi, int zi)
        {
            return zi == 1 && (xi == 1 || xi == 3);
        }

        private sealed class HouseMats
        {
            public Material[] Walls;
            public Vector2[] WallTiles;
            public Material[] Roofs;
            public Material[] Doors;
            public Material Frame, WindowLit, WindowTv, Fence, Bin, Mailbox, GarageDoor, Shutter;
        }

        /// <summary>Une place de voiture dans une allée (position monde, cap, conduisible ou non).</summary>
        private struct CarSpot
        {
            public Vector3 Position;
            public float Yaw;
            public bool Drivable;
        }

        /// <summary>Repère d'une parcelle : origine au bord du trottoir, z vers le fond du jardin.</summary>
        private struct PlotFrame
        {
            public Vector3 Origin;
            public float Yaw;
            public Quaternion Rotation;

            public PlotFrame(Vector3 origin, float yaw)
            {
                Origin = origin;
                Yaw = yaw;
                Rotation = Quaternion.Euler(0f, yaw, 0f);
            }

            public Vector3 P(float x, float y, float z)
            {
                return Origin + Rotation * new Vector3(x, y, z);
            }

            /// <summary>Un rectangle du repère (les caps sont des multiples de 90°) en rectangle monde.</summary>
            public Rect Area(float x0, float z0, float x1, float z1)
            {
                Vector3 a = P(x0, 0f, z0);
                Vector3 b = P(x1, 0f, z1);
                return Rect.MinMaxRect(Mathf.Min(a.x, b.x), Mathf.Min(a.z, b.z), Mathf.Max(a.x, b.x), Mathf.Max(a.z, b.z));
            }
        }

        private static HouseMats CreateHouseMaterials(Mats m)
        {
            HouseMats h = new HouseMats();
            Vector2 render = new Vector2(3f, 3f);

            h.Walls = new[]
            {
                m.Render,
                Mat("M_Ville_CrepiCreme", new Color(0.64f, 0.58f, 0.46f), 0.08f),
                Mat("M_Ville_CrepiOcre", new Color(0.60f, 0.45f, 0.28f), 0.08f),
                Mat("M_Ville_CrepiBlanc", new Color(0.70f, 0.69f, 0.66f), 0.08f),
                Mat("M_Ville_CrepiRose", new Color(0.62f, 0.46f, 0.42f), 0.08f),
                m.Brick
            };
            h.WallTiles = new[] { render, render, render, render, render, BrickTile };

            Material slate = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_Ardoise",
                new Color(0.22f, 0.24f, 0.27f), 0.35f, 0f,
                EditorBuildUtility.CreateOrUpdateBrickTexture(NightMaterialFactory.TexturesFolder, "T_ArdoiseVille", 128, 8,
                    new Color(0.24f, 0.26f, 0.29f), new Color(0.13f, 0.14f, 0.16f)), Vector2.one);
            Material orange = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_TuilesClaires",
                new Color(0.52f, 0.26f, 0.14f), 0.2f, 0f,
                EditorBuildUtility.CreateOrUpdateBrickTexture(NightMaterialFactory.TexturesFolder, "T_TuilesClairesVille", 128, 8,
                    new Color(0.56f, 0.28f, 0.15f), new Color(0.34f, 0.15f, 0.08f)), Vector2.one);
            h.Roofs = new[] { m.Tiles, slate, orange };

            h.Doors = new[]
            {
                m.Wood,
                Mat("M_Ville_PorteRouge", new Color(0.42f, 0.07f, 0.06f), 0.35f),
                Mat("M_Ville_PorteBleue", new Color(0.07f, 0.15f, 0.30f), 0.35f),
                Mat("M_Ville_PorteVerte", new Color(0.07f, 0.21f, 0.13f), 0.35f)
            };

            h.Frame = Mat("M_Ville_Menuiserie", new Color(0.82f, 0.82f, 0.79f), 0.3f);
            h.Shutter = Mat("M_Ville_Volets", new Color(0.20f, 0.30f, 0.34f), 0.2f);
            h.Fence = Mat("M_Ville_Palissade", new Color(0.30f, 0.20f, 0.13f), 0.1f);
            h.Bin = Mat("M_Ville_Poubelle", new Color(0.07f, 0.18f, 0.11f), 0.25f);
            h.Mailbox = Mat("M_Ville_BoiteLettres", new Color(0.78f, 0.62f, 0.10f), 0.4f);
            h.GarageDoor = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_PorteGarage",
                new Color(0.50f, 0.51f, 0.52f), 0.4f, 0.35f);

            // Une fenêtre allumée : pas un néon, une lampe derrière un rideau — chaude, douce.
            h.WindowLit = NightMaterialFactory.CreateEmissive(MaterialsFolder, "M_Ville_FenetreAllumee",
                new Color(0.85f, 0.66f, 0.42f), null, Vector2.one, new Color(1f, 0.70f, 0.38f) * 1.7f, null, 0.6f, 0f);
            h.WindowTv = NightMaterialFactory.CreateEmissive(MaterialsFolder, "M_Ville_FenetreTele",
                new Color(0.45f, 0.55f, 0.8f), null, Vector2.one, new Color(0.42f, 0.58f, 1f) * 1.2f, null, 0.6f, 0f);

            return h;
        }

        private static Material Mat(string name, Color color, float smoothness)
        {
            return EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, name, color, smoothness, 0f);
        }

        /// <summary>
        /// Un îlot de maisons : une rangée de parcelles sur chacun des deux grands côtés, jardins
        /// dos à dos au milieu. Les voitures des allées sont rendues à l'appelant.
        /// </summary>
        private static void BuildSuburb(Transform parent, Mats m, HouseMats h, System.Random rng, Rect lot, string name,
            Result result, List<CarSpot> cars)
        {
            CityMeshBuilder b = new CityMeshBuilder(name);
            float depth = lot.width * 0.5f;
            int count = Mathf.Max(1, Mathf.FloorToInt(lot.height / 17.5f));
            float width = lot.height / count;

            for (int s = 0; s < 2; s++)
            {
                // Côté ouest : la rue est à l'ouest, le jardin s'étend vers l'est (cap 90°).
                // Côté est : l'inverse (cap -90°). Le long de la rue, x va vers le sud à l'ouest,
                // vers le nord à l'est : les parcelles se suivent dans le même sens que x.
                bool west = s == 0;
                float yaw = west ? 90f : -90f;

                for (int i = 0; i < count; i++)
                {
                    float zCenter = west ? lot.yMax - width * (i + 0.5f) : lot.yMin + width * (i + 0.5f);
                    Vector3 origin = new Vector3(west ? lot.xMin : lot.xMax, 0f, zCenter);
                    PlotFrame f = new PlotFrame(origin, yaw);

                    House(b, m, h, rng, f, width, depth, i == 0, i == count - 1, result, cars);
                }
            }

            b.Flush(parent);
        }

        private static void House(CityMeshBuilder b, Mats m, HouseMats h, System.Random rng, PlotFrame f, float w,
            float d, bool first, bool last, Result result, List<CarSpot> cars)
        {
            float hw = w * 0.5f;
            float side = rng.NextDouble() < 0.5 ? 1f : -1f;          // côté de l'allée

            int wall = rng.Next(h.Walls.Length);
            Material wallMat = h.Walls[wall];
            Vector2 wallTile = h.WallTiles[wall];
            Material roofMat = h.Roofs[rng.Next(h.Roofs.Length)];
            Material doorMat = h.Doors[rng.Next(h.Doors.Length)];

            int floors = rng.NextDouble() < 0.55 ? 2 : 1;
            const float floorHeight = 2.9f;
            float wallHeight = floors * floorHeight + 0.3f;
            float houseW = Mathf.Min(w - 3.6f - 3.2f, Range(rng, 9.5f, 12.5f));
            float houseD = Range(rng, 8.5f, 10.5f);
            float front = Range(rng, 6.5f, 8.5f);
            float hx = -side * (hw - 1.4f - houseW * 0.5f);
            float hz = front + houseD * 0.5f;

            const float garageW = 3.6f;
            const float garageD = 6.2f;
            float driveX = hx + side * (houseW * 0.5f + garageW * 0.5f);
            float garageFront = front + 0.8f;
            float doorX = hx - side * 0.9f;

            // --- sol : pelouse, allée, chemin, terrasse derrière
            b.Ground(f.Area(-hw, 0f, hw, d), 0.03f, m.Grass, GrassTile);
            b.Ground(f.Area(driveX - 1.6f, 0f, driveX + 1.6f, garageFront), 0.05f, m.Concrete, ConcreteTile);
            b.Ground(f.Area(doorX - 0.6f, 0f, doorX + 0.6f, front - 0.9f), 0.05f, m.Sidewalk, SidewalkTile);
            b.Ground(f.Area(hx - houseW * 0.45f, front + houseD, hx + houseW * 0.3f, front + houseD + 3f), 0.05f,
                m.Concrete, ConcreteTile);

            // --- clôtures
            FrontBoundary(b, m, h, rng, f, hw, new[] { new Vector2(driveX - 1.7f, driveX + 1.7f), new Vector2(doorX - 0.75f, doorX + 0.75f) });
            SideFence(b, h, f, -hw, front + houseD * 0.4f, d, first);
            if (last) SideFence(b, h, f, hw, front + houseD * 0.4f, d, true);
            b.Box(f.P(0f, 0.9f, d - 0.1f), new Vector3(w, 1.8f, 0.08f), h.Fence, new Vector2(0.9f, 1.8f), true, f.Yaw);

            // --- la maison
            b.Block(f.P(hx, wallHeight * 0.5f, hz), new Vector3(houseW, wallHeight, houseD), wallMat, wallTile, true, f.Yaw);
            b.Box(f.P(hx, 0.25f, hz), new Vector3(houseW + 0.1f, 0.5f, houseD + 0.1f), m.DarkConcrete, ConcreteTile, false, f.Yaw);
            if (floors == 2)
            {
                b.Box(f.P(hx, floorHeight, hz), new Vector3(houseW + 0.1f, 0.14f, houseD + 0.1f), m.Concrete, ConcreteTile, false, f.Yaw);
            }

            float rise = Range(rng, 2f, 2.8f);
            b.GableRoof(f.P(hx, wallHeight, hz), new Vector3(houseW + 0.8f, rise, houseD + 0.9f), roofMat, wallMat, TilesTile, f.Yaw);

            float chimneyX = hx + (rng.NextDouble() < 0.5 ? -1f : 1f) * houseW * 0.28f;
            b.Box(f.P(chimneyX, wallHeight + rise * 0.6f + 0.35f, hz + houseD * 0.18f), new Vector3(0.7f, rise * 1.2f + 0.7f, 0.7f),
                m.Brick, BrickTile, false, f.Yaw);

            // --- façade : fenêtres (allumées ou non), volets, porte, perron
            bool asleep = rng.NextDouble() < 0.18;
            bool shutters = rng.NextDouble() < 0.5;
            float faceZ = front;

            float[] ground = { hx - side * houseW * 0.34f, hx + side * houseW * 0.22f };
            for (int i = 0; i < ground.Length; i++) Window(b, m, h, rng, f, ground[i], 1.55f, faceZ, asleep, shutters);

            if (floors == 2)
            {
                float[] upper = { hx - houseW * 0.33f, hx, hx + houseW * 0.33f };
                for (int i = 0; i < upper.Length; i++) Window(b, m, h, rng, f, upper[i], floorHeight + 1.5f, faceZ, asleep, shutters);
            }

            // Une fenêtre sur le pignon côté jardin, et une à l'arrière : le jardin aussi est habité.
            for (int k = 0; k < floors; k++)
            {
                float y = k * floorHeight + 1.55f;
                Material pane = PaneMaterial(m, h, rng, asleep);
                b.Box(f.P(hx - side * (houseW * 0.5f + 0.03f), y, hz), new Vector3(0.05f, 1.35f, 1.55f), h.Frame, None, false, f.Yaw);
                b.Box(f.P(hx - side * (houseW * 0.5f + 0.06f), y, hz), new Vector3(0.04f, 1.1f, 1.3f), pane, None, false, f.Yaw);

                pane = PaneMaterial(m, h, rng, asleep);
                b.Box(f.P(hx + houseW * 0.15f, y, front + houseD + 0.03f), new Vector3(1.55f, 1.35f, 0.05f), h.Frame, None, false, f.Yaw);
                b.Box(f.P(hx + houseW * 0.15f, y, front + houseD + 0.06f), new Vector3(1.3f, 1.1f, 0.04f), pane, None, false, f.Yaw);
            }

            b.Box(f.P(doorX, 1.12f, faceZ - 0.02f), new Vector3(1.25f, 2.3f, 0.04f), h.Frame, None, false, f.Yaw);
            b.Box(f.P(doorX, 1.05f, faceZ - 0.05f), new Vector3(1f, 2.1f, 0.06f), doorMat, None, false, f.Yaw);
            b.Box(f.P(doorX, 0.09f, faceZ - 0.55f), new Vector3(1.7f, 0.18f, 1.1f), m.Concrete, ConcreteTile, true, f.Yaw);
            b.Box(f.P(doorX, 2.55f, faceZ - 0.55f), new Vector3(1.8f, 0.1f, 1.1f), m.DarkMetal, None, false, f.Yaw);

            if (!asleep || rng.NextDouble() < 0.3)
            {
                // La lampe du perron : la seule lumière vraie de la maison (les fenêtres brillent seules).
                b.Box(f.P(doorX + side * 0.8f, 2.05f, faceZ - 0.08f), new Vector3(0.16f, 0.22f, 0.12f), m.Lamp, None, false, f.Yaw);
                Light porch = NightStreetBuilder.AddLight(result.LightsRoot, "Lampe de perron", Vector3.zero,
                    new Color(1f, 0.72f, 0.42f), 1.1f, 5.5f, false, false);
                porch.transform.position = f.P(doorX + side * 0.8f, 2f, faceZ - 0.5f);
            }

            // --- garage, poubelles, boîte aux lettres
            b.Block(f.P(driveX, 1.35f, garageFront + garageD * 0.5f), new Vector3(garageW, 2.7f, garageD), wallMat, wallTile, true, f.Yaw);
            b.Box(f.P(driveX, 2.76f, garageFront + garageD * 0.5f), new Vector3(garageW + 0.3f, 0.12f, garageD + 0.3f),
                m.DarkConcrete, ConcreteTile, false, f.Yaw);
            b.Box(f.P(driveX, 1.1f, garageFront - 0.03f), new Vector3(2.8f, 2.2f, 0.06f), h.GarageDoor, new Vector2(2.8f, 0.3f), false, f.Yaw);

            float binX = driveX + side * (garageW * 0.5f + 0.45f);
            if (Mathf.Abs(binX) < hw - 0.4f)
            {
                b.Box(f.P(binX, 0.55f, garageFront + 0.5f), new Vector3(0.62f, 1.1f, 0.7f), h.Bin, None, true, f.Yaw);
                b.Box(f.P(binX, 0.5f, garageFront + 1.3f), new Vector3(0.62f, 1f, 0.7f), m.DarkMetal, None, true, f.Yaw);
            }

            float mailX = doorX + (driveX > doorX ? 1.1f : -1.1f);
            b.Box(f.P(mailX, 0.5f, 1.1f), new Vector3(0.08f, 1f, 0.08f), m.DarkMetal, None, false, f.Yaw);
            b.Box(f.P(mailX, 1.08f, 1.1f), new Vector3(0.42f, 0.32f, 0.26f), h.Mailbox, None, true, f.Yaw);

            // --- le jardin de derrière : un arbre, parfois un abri
            float back = front + houseD + 4f;
            if (back < d - 3f)
            {
                float tx = Range(rng, -hw + 2.5f, hw - 2.5f);
                float tz = Range(rng, back, d - 2.5f);
                float th = Range(rng, 3f, 4.4f);
                b.Box(f.P(tx, th * 0.5f, tz), new Vector3(0.3f, th, 0.3f), m.Wood, None, true, f.Yaw);
                float ts = Range(rng, 2.4f, 3.4f);
                b.Box(f.P(tx, th + ts * 0.35f, tz), new Vector3(ts, ts * 0.8f, ts), m.Leaves, None, false, f.Yaw);
                b.Box(f.P(tx, th + ts * 0.35f, tz), new Vector3(ts * 0.8f, ts * 0.95f, ts * 0.8f), m.Leaves, None, false, f.Yaw + 45f);

                if (rng.NextDouble() < 0.45)
                {
                    float sx = tx > 0f ? tx - 4f : tx + 4f;
                    sx = Mathf.Clamp(sx, -hw + 1.6f, hw - 1.6f);
                    float sz = d - 2.2f;
                    b.Block(f.P(sx, 1.05f, sz), new Vector3(2.4f, 2.1f, 2f), h.Fence, new Vector2(0.9f, 2.1f), true, f.Yaw);
                    b.Box(f.P(sx, 2.18f, sz), new Vector3(2.7f, 0.1f, 2.3f), m.DarkMetal, None, false, f.Yaw);
                }
            }

            // --- une voiture dans l'allée, nez vers la rue
            if (rng.NextDouble() < 0.55)
            {
                cars.Add(new CarSpot { Position = f.P(driveX, 0.05f, 3.3f), Yaw = f.Yaw + 180f });
            }
        }

        private static Material PaneMaterial(Mats m, HouseMats h, System.Random rng, bool asleep)
        {
            double r = rng.NextDouble();
            if (asleep) return r < 0.08 ? h.WindowLit : m.Glass;
            if (r < 0.42) return h.WindowLit;
            if (r < 0.52) return h.WindowTv;
            return m.Glass;
        }

        private static void Window(CityMeshBuilder b, Mats m, HouseMats h, System.Random rng, PlotFrame f, float x, float y,
            float faceZ, bool asleep, bool shutters)
        {
            Material pane = PaneMaterial(m, h, rng, asleep);
            b.Box(f.P(x, y, faceZ - 0.03f), new Vector3(1.55f, 1.35f, 0.06f), h.Frame, None, false, f.Yaw);
            b.Box(f.P(x, y, faceZ - 0.07f), new Vector3(1.3f, 1.1f, 0.04f), pane, None, false, f.Yaw);
            b.Box(f.P(x, y - 0.72f, faceZ - 0.1f), new Vector3(1.65f, 0.07f, 0.2f), m.Concrete, None, false, f.Yaw);

            if (!shutters) return;

            for (int s = -1; s <= 1; s += 2)
            {
                b.Box(f.P(x + s * 1.12f, y, faceZ - 0.04f), new Vector3(0.62f, 1.35f, 0.05f), h.Shutter, None, false, f.Yaw);
            }
        }

        /// <summary>Le long du trottoir : haie, muret ou palissade basse, ouverts devant l'allée et le chemin.</summary>
        private static void FrontBoundary(CityMeshBuilder b, Mats m, HouseMats h, System.Random rng, PlotFrame f, float hw,
            Vector2[] gaps)
        {
            int kind = rng.Next(3);
            List<Vector2> pieces = new List<Vector2> { new Vector2(-hw, hw) };

            for (int g = 0; g < gaps.Length; g++)
            {
                List<Vector2> next = new List<Vector2>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    Vector2 p = pieces[i];
                    Vector2 gap = gaps[g];
                    if (gap.y <= p.x || gap.x >= p.y)
                    {
                        next.Add(p);
                        continue;
                    }

                    if (gap.x > p.x) next.Add(new Vector2(p.x, gap.x));
                    if (gap.y < p.y) next.Add(new Vector2(gap.y, p.y));
                }

                pieces = next;
            }

            for (int i = 0; i < pieces.Count; i++)
            {
                float x0 = pieces[i].x;
                float x1 = pieces[i].y;
                float len = x1 - x0;
                if (len < 0.3f) continue;
                float cx = (x0 + x1) * 0.5f;

                switch (kind)
                {
                    case 0:
                        b.Box(f.P(cx, 0.5f, 0.45f), new Vector3(len, 1f, 0.65f), m.Leaves, None, true, f.Yaw);
                        break;
                    case 1:
                        b.Box(f.P(cx, 0.35f, 0.35f), new Vector3(len, 0.7f, 0.3f), m.Render, new Vector2(3f, 3f), true, f.Yaw);
                        b.Box(f.P(cx, 0.74f, 0.35f), new Vector3(len + 0.04f, 0.08f, 0.36f), m.Concrete, None, false, f.Yaw);
                        break;
                    default:
                        // Palissade basse : deux lisses et des lattes.
                        b.Box(f.P(cx, 0.35f, 0.35f), new Vector3(len, 0.08f, 0.05f), h.Frame, None, true, f.Yaw);
                        b.Box(f.P(cx, 0.75f, 0.35f), new Vector3(len, 0.08f, 0.05f), h.Frame, None, false, f.Yaw);
                        int slats = Mathf.Max(2, Mathf.FloorToInt(len / 0.3f));
                        for (int k = 0; k < slats; k++)
                        {
                            float x = x0 + (k + 0.5f) * len / slats;
                            b.Box(f.P(x, 0.5f, 0.38f), new Vector3(0.09f, 1f, 0.03f), h.Frame, None, false, f.Yaw);
                        }

                        break;
                }
            }
        }

        /// <summary>La palissade entre deux jardins (et au bout de la rangée, contre la rue).</summary>
        private static void SideFence(CityMeshBuilder b, HouseMats h, PlotFrame f, float x, float z0, float z1, bool tall)
        {
            float len = z1 - z0;
            if (len < 0.5f) return;
            float height = tall ? 2f : 1.7f;
            b.Box(f.P(x, height * 0.5f, (z0 + z1) * 0.5f), new Vector3(0.08f, height, len), h.Fence, new Vector2(0.9f, height), true, f.Yaw);
        }
    }
}
