using System.Collections.Generic;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La ville du monde ouvert, autour de la rue du Vertigo.
    ///
    /// Un quadrillage de rues (vu du dessus, x à l'est, z au nord) :
    /// - le BOULEVARD, d'est en ouest : c'est la rue du Vertigo, prolongée des deux côtés ;
    /// - la rue du Nord et la rue du Sud, parallèles ;
    /// - quatre avenues nord-sud, dont les deux qui encadrent le Vertigo.
    /// Entre les rues, des îlots bordés de trottoirs. Ceux du centre sont des lieux du jeu : au
    /// nord du boulevard le Vertigo et son parking, au sud l'îlot d'en face, la planque dans son
    /// lotissement et le square. Les autres sont des pâtés d'immeubles, et ceux du bord ferment la
    /// ville (plus hauts, jusque contre les limites).
    ///
    /// Tout est généré avec une graine fixe : deux constructions donnent la même ville.
    /// </summary>
    public static partial class CityBuilder
    {
        public const float SidewalkHeight = NightStreetBuilder.SidewalkHeight;

        /// <summary>Limites de la ville (x, z).</summary>
        public static readonly Rect Bounds = Rect.MinMaxRect(-200f, -172f, 200f, 152f);

        public static readonly Vector3 HouseOrigin = new Vector3(10f, 0f, -64f);
        public static readonly Vector3 ParkingOrigin = new Vector3(0f, 0f, 62f);
        public const float ParkingYaw = 180f;

        /// <summary>Le sol de la rue du Vertigo (NightStreetBuilder) : la ville l'évite.</summary>
        private static readonly Rect StreetGround = Rect.MinMaxRect(-50f, -38f, 50f, 34f);
        private static readonly Rect ParkingGround = Rect.MinMaxRect(-23f, 44f, 23f, 80f);
        private static readonly Rect HouseGround = Rect.MinMaxRect(-22f, -101.1f, 42f, -52f);
        private static readonly Rect Park = Rect.MinMaxRect(-50f, -100f, -26f, -40f);

        /// <summary>Les trottoirs de la rue du Vertigo existent déjà.</summary>
        private static readonly Rect[] StreetSidewalks =
        {
            Rect.MinMaxRect(-54f, 7f, 54f, 14f),
            Rect.MinMaxRect(-54f, -18f, 54f, -11f)
        };

        // Îlots : bornes en x et en z (entre les trottoirs).
        private static readonly float[] LotX = { -200f, -160f, -140f, -72f, -52f, 52f, 72f, 140f, 160f, 200f };
        private static readonly float[] LotZ = { -172f, -132f, -112f, -18f, 14f, 92f, 112f, 152f };

        public sealed class Result
        {
            public Transform Root;
            public Transform LightsRoot;
            public readonly List<OpenWorldDirector.Spot> Spots = new List<OpenWorldDirector.Spot>();
            public readonly List<Vector3[]> WalkLoops = new List<Vector3[]>();
            public readonly List<Vector3[]> DriveLoops = new List<Vector3[]>();
            public readonly List<Rect> MapRoads = new List<Rect>();
            public readonly List<Rect> MapBlocks = new List<Rect>();
            public readonly List<Rect> MapParks = new List<Rect>();
            public readonly List<CityMap.Landmark> Landmarks = new List<CityMap.Landmark>();

            /// <summary>Fabrique des voitures conduisibles (à libérer avec Dispose une fois la scène finie).</summary>
            internal CarFactory Cars;

            /// <summary>Enseignes au néon demandées par les façades, écrites à la fin.</summary>
            internal readonly List<SignRequest> Signs = new List<SignRequest>();
        }

        private sealed class Mats
        {
            public Material Asphalt, Sidewalk, Curb, Paint, Brick, DarkBrick, Concrete, DarkConcrete;
            public Material FacadeWarm, FacadeCool, Metal, DarkMetal, Glass, Grass, Leaves, Wood, Tiles, Render;
            public Material Lamp;
            public Material[] Neons;
            public Material[] Awnings;
            public Color[] NeonColors;
        }

        private enum Style
        {
            Centre = 0,
            Ville = 1,
            Bord = 2,
            Pavillons = 3
        }

        private static readonly Vector2 AsphaltTile = new Vector2(3.6f, 3.6f);
        private static readonly Vector2 SidewalkTile = new Vector2(4f, 3.5f);
        private static readonly Vector2 CurbTile = new Vector2(2f, 1f);
        private static readonly Vector2 BrickTile = new Vector2(1.75f, 3.2f);
        private static readonly Vector2 DarkBrickTile = new Vector2(1.55f, 2.7f);
        private static readonly Vector2 ConcreteTile = new Vector2(3.5f, 4f);
        private static readonly Vector2 FacadeTile = new Vector2(12f, 16f);
        private static readonly Vector2 GrassTile = new Vector2(2.5f, 2.5f);
        private static readonly Vector2 TilesTile = new Vector2(1.5f, 1.5f);
        private static readonly Vector2 None = Vector2.zero;

        // ------------------------------------------------------------------ construction

        public static Result Build(NightMaterialFactory.Palette night)
        {
            Mats m = CreateMaterials(night);
            HouseMats houses = CreateHouseMaterials(m);
            System.Random rng = new System.Random(20260926);

            Result result = new Result();
            GameObject root = new GameObject("=== Ville ===");
            result.Root = root.transform;
            result.LightsRoot = EditorBuildUtility.CreateEmpty("Lumieres de la ville", root.transform, Vector3.zero).transform;

            BuildGround(root.transform, m, result);
            BuildSidewalks(root.transform, m, result);
            BuildMarkings(root.transform, m);

            List<Rect> reserved = Reserved();
            List<Rect> placed = new List<Rect>();
            List<CarSpot> driveways = new List<CarSpot>();

            for (int xi = 0; xi < 5; xi++)
            {
                for (int zi = 0; zi < 4; zi++)
                {
                    Rect lot = Lot(xi, zi);

                    if (IsSuburb(xi, zi))
                    {
                        BuildSuburb(root.transform, m, houses, rng, lot, xi == 1 ? "Lotissement des Glycines" : "Lotissement du Moulin",
                            result, driveways);
                        result.WalkLoops.Add(WalkLoop(lot, xi, zi));
                        continue;
                    }

                    Style style = StyleOf(xi, zi);
                    CityMeshBuilder builder = new CityMeshBuilder("Ilot " + (char)('A' + xi) + (zi + 1));
                    FillLot(builder, m, rng, lot, style, xi, zi, reserved, placed, result);
                    builder.Flush(root.transform);

                    if (!IsEdge(xi, zi)) result.WalkLoops.Add(WalkLoop(lot, xi, zi));
                }
            }

            BuildPark(root.transform, m, rng, result);
            BuildLamps(root.transform, m, result);
            BuildTrafficLights(root.transform, m, result);
            BuildStreetFurniture(root.transform, m, rng, result);
            BuildSteam(root.transform, m, rng);
            BuildBoundary(root.transform, m, rng);
            BuildSkyline(root.transform, m, rng);

            result.Cars = new CarFactory(night, root.transform);
            BuildParkedCars(root.transform, night, rng, result.Cars);
            PlaceDriveways(root.transform, night, result.Cars, rng, driveways);

            BuildSigns(root.transform, result);

            AddDriveLoops(result);
            AddSpots(result);
            AddLandmarks(result);

            // La ville entière reflète ses néons dans ce qui brille : sonde large, basse résolution.
            EditorBuildUtility.AddReflectionProbe(root.transform, "Sonde de reflexion (ville)",
                new Vector3(0f, 12f, -10f), new Vector3(Bounds.width, 40f, Bounds.height), false, 0.8f);

            return result;
        }

        // ------------------------------------------------------------------ géométrie de la ville

        private static Rect Lot(int xi, int zi)
        {
            return Rect.MinMaxRect(LotX[xi * 2], LotZ[zi * 2], LotX[xi * 2 + 1], LotZ[zi * 2 + 1]);
        }

        private static bool IsEdge(int xi, int zi)
        {
            return xi == 0 || xi == 4 || zi == 0 || zi == 3;
        }

        private static Style StyleOf(int xi, int zi)
        {
            if (IsEdge(xi, zi)) return Style.Bord;
            if (xi == 2 && zi == 1) return Style.Pavillons;
            if (xi == 2) return Style.Centre;
            return Style.Ville;
        }

        /// <summary>Largeur des trottoirs d'un îlot : ouest, est, sud, nord.</summary>
        private static Vector4 SidewalkWidths(int xi, int zi)
        {
            float west = xi == 0 ? 0f : 4f;
            float east = xi == 4 ? 0f : 4f;
            float south = zi == 0 ? 0f : zi == 2 ? 7f : 4f;
            float north = zi == 3 ? 0f : zi == 1 ? 7f : 4f;
            return new Vector4(west, east, south, north);
        }

        /// <summary>Les chaussées, bord de trottoir à bord de trottoir.</summary>
        private static List<Rect> Roads()
        {
            List<Rect> roads = new List<Rect>
            {
                Rect.MinMaxRect(Bounds.xMin, -11f, Bounds.xMax, 7f),
                Rect.MinMaxRect(Bounds.xMin, 96f, Bounds.xMax, 108f),
                Rect.MinMaxRect(Bounds.xMin, -128f, Bounds.xMax, -116f)
            };

            float[] avenues = { -150f, -62f, 62f, 150f };
            for (int i = 0; i < avenues.Length; i++)
            {
                roads.Add(Rect.MinMaxRect(avenues[i] - 6f, Bounds.yMin, avenues[i] + 6f, Bounds.yMax));
            }

            return roads;
        }

        /// <summary>Ce que les immeubles ne doivent pas recouvrir : les lieux du jeu.</summary>
        private static List<Rect> Reserved()
        {
            return new List<Rect>
            {
                Rect.MinMaxRect(-47f, -36f, 42f, -18f),     // l'îlot d'en face (NightStreetBuilder)
                Rect.MinMaxRect(-18f, 14f, 18f, 33f),       // le Vertigo
                Rect.MinMaxRect(-25f, 33f, 25f, 92f),       // ruelle, parking, entrée du parking
                Rect.MinMaxRect(-24f, -103f, 44f, -50f),    // la planque et sa rue
                Rect.MinMaxRect(42f, -87f, 56f, -76f),      // la rue de la planque débouche sur l'avenue
                Rect.MinMaxRect(-26f, 14f, -18f, 33f),      // passage vers la ruelle, côté ouest du club
                Rect.MinMaxRect(18f, 14f, 26f, 33f),        // et côté est
                Park                                          // le square
            };
        }

        private static void BuildGround(Transform parent, Mats m, Result result)
        {
            CityMeshBuilder ground = new CityMeshBuilder("Sol de la ville");

            // Le sol découpé autour des lieux qui ont le leur : une grille sur toutes leurs arêtes,
            // et on garde les cases qui ne tombent dans aucun trou.
            Rect[] holes = { StreetGround, ParkingGround, HouseGround };
            List<float> xs = new List<float> { Bounds.xMin, Bounds.xMax };
            List<float> zs = new List<float> { Bounds.yMin, Bounds.yMax };
            for (int i = 0; i < holes.Length; i++)
            {
                xs.Add(holes[i].xMin);
                xs.Add(holes[i].xMax);
                zs.Add(holes[i].yMin);
                zs.Add(holes[i].yMax);
            }

            xs.Sort();
            zs.Sort();

            for (int i = 0; i + 1 < xs.Count; i++)
            {
                for (int j = 0; j + 1 < zs.Count; j++)
                {
                    Rect cell = Rect.MinMaxRect(xs[i], zs[j], xs[i + 1], zs[j + 1]);
                    if (cell.width < 0.01f || cell.height < 0.01f) continue;

                    bool hole = false;
                    for (int h = 0; h < holes.Length && !hole; h++) hole = holes[h].Contains(cell.center);
                    if (!hole) ground.Ground(cell, 0f, m.Asphalt, AsphaltTile);
                }
            }

            ground.Flush(parent, true, false);

            List<Rect> roads = Roads();
            result.MapRoads.AddRange(roads);
        }

        private static void BuildSidewalks(Transform parent, Mats m, Result result)
        {
            CityMeshBuilder walks = new CityMeshBuilder("Trottoirs");

            for (int xi = 0; xi < 5; xi++)
            {
                for (int zi = 0; zi < 4; zi++)
                {
                    Rect lot = Lot(xi, zi);
                    Vector4 w = SidewalkWidths(xi, zi);
                    result.MapBlocks.Add(lot);

                    // Nord et sud sur toute la largeur (coins compris), ouest et est entre les deux.
                    Walk(walks, m, Rect.MinMaxRect(lot.xMin - w.x, lot.yMax, lot.xMax + w.y, lot.yMax + w.w), Vector3.forward);
                    Walk(walks, m, Rect.MinMaxRect(lot.xMin - w.x, lot.yMin - w.z, lot.xMax + w.y, lot.yMin), Vector3.back);
                    Walk(walks, m, Rect.MinMaxRect(lot.xMin - w.x, lot.yMin, lot.xMin, lot.yMax), Vector3.left);
                    Walk(walks, m, Rect.MinMaxRect(lot.xMax, lot.yMin, lot.xMax + w.y, lot.yMax), Vector3.right);
                }
            }

            walks.Flush(parent);
        }

        /// <summary>Une dalle de trottoir et sa bordure côté chaussée, hors trottoirs du Vertigo.</summary>
        private static void Walk(CityMeshBuilder builder, Mats m, Rect area, Vector3 roadSide)
        {
            if (area.width < 0.05f || area.height < 0.05f) return;

            List<Rect> pieces = Subtract(area, StreetSidewalks);
            for (int i = 0; i < pieces.Count; i++)
            {
                Rect r = pieces[i];
                if (r.width < 0.05f || r.height < 0.05f) continue;

                builder.Box(new Vector3(r.center.x, SidewalkHeight * 0.5f, r.center.y),
                    new Vector3(r.width, SidewalkHeight, r.height), m.Sidewalk, SidewalkTile, true);

                // La bordure, une bande plus claire le long de l'arête côté chaussée.
                if (roadSide.z != 0f)
                {
                    float z = roadSide.z > 0f ? r.yMax - 0.15f : r.yMin + 0.15f;
                    builder.Box(new Vector3(r.center.x, SidewalkHeight * 0.5f + 0.01f, z),
                        new Vector3(r.width, SidewalkHeight + 0.02f, 0.3f), m.Curb, CurbTile, false);
                }
                else
                {
                    float x = roadSide.x > 0f ? r.xMax - 0.15f : r.xMin + 0.15f;
                    builder.Box(new Vector3(x, SidewalkHeight * 0.5f + 0.01f, r.center.y),
                        new Vector3(0.3f, SidewalkHeight + 0.02f, r.height), m.Curb, CurbTile, false);
                }
            }
        }

        /// <summary>Rectangle moins des exclusions (découpe en x : les exclusions couvrent toute la profondeur).</summary>
        private static List<Rect> Subtract(Rect area, Rect[] cuts)
        {
            List<Rect> pieces = new List<Rect> { area };

            for (int c = 0; c < cuts.Length; c++)
            {
                List<Rect> next = new List<Rect>();
                for (int i = 0; i < pieces.Count; i++)
                {
                    Rect p = pieces[i];
                    Rect cut = cuts[c];
                    bool overlaps = p.xMin < cut.xMax && p.xMax > cut.xMin && p.yMin < cut.yMax && p.yMax > cut.yMin;
                    if (!overlaps)
                    {
                        next.Add(p);
                        continue;
                    }

                    if (p.xMin < cut.xMin) next.Add(Rect.MinMaxRect(p.xMin, p.yMin, cut.xMin, p.yMax));
                    if (p.xMax > cut.xMax) next.Add(Rect.MinMaxRect(cut.xMax, p.yMin, p.xMax, p.yMax));
                    if (p.yMin < cut.yMin) next.Add(Rect.MinMaxRect(Mathf.Max(p.xMin, cut.xMin), p.yMin, Mathf.Min(p.xMax, cut.xMax), cut.yMin));
                    if (p.yMax > cut.yMax) next.Add(Rect.MinMaxRect(Mathf.Max(p.xMin, cut.xMin), cut.yMax, Mathf.Min(p.xMax, cut.xMax), p.yMax));
                }

                pieces = next;
            }

            return pieces;
        }

        /// <summary>Lignes discontinues au milieu des chaussées (pas dans les carrefours, pas devant le Vertigo).</summary>
        private static void BuildMarkings(Transform parent, Mats m)
        {
            CityMeshBuilder paint = new CityMeshBuilder("Marquage");
            List<Rect> roads = Roads();

            for (int i = 0; i < roads.Count; i++)
            {
                Rect road = roads[i];
                bool eastWest = road.width > road.height;
                float length = eastWest ? road.width : road.height;

                for (float t = 2f; t < length - 2f; t += 4.8f)
                {
                    Vector2 c = eastWest
                        ? new Vector2(road.xMin + t, road.center.y)
                        : new Vector2(road.center.x, road.yMin + t);

                    if (StreetGround.Contains(c)) continue;

                    bool crossing = false;
                    for (int j = 0; j < roads.Count && !crossing; j++)
                    {
                        if (j != i && roads[j].Contains(c)) crossing = true;
                    }

                    if (crossing) continue;

                    Vector3 size = eastWest ? new Vector3(2.2f, 0.012f, 0.16f) : new Vector3(0.16f, 0.012f, 2.2f);
                    paint.Box(new Vector3(c.x, 0.006f, c.y), size, m.Paint, None, false);
                }
            }

            paint.Flush(parent, false, false);
        }

        // ------------------------------------------------------------------ immeubles

        private static void FillLot(CityMeshBuilder b, Mats m, System.Random rng, Rect lot, Style style, int xi, int zi,
            List<Rect> reserved, List<Rect> placed, Result result)
        {
            // Côtés qui donnent sur une rue (les bords de la ville n'en ont que vers l'intérieur,
            // mais on les garnit aussi côté limite : entre deux immeubles, on doit voir un mur,
            // pas le vide).
            Vector2[] sides = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };

            for (int s = 0; s < sides.Length; s++)
            {
                Vector2 outward = sides[s];
                bool street = (outward.x < 0f && xi > 0) || (outward.x > 0f && xi < 4) ||
                              (outward.y < 0f && zi > 0) || (outward.y > 0f && zi < 3);

                FillSide(b, m, rng, lot, style, outward, street, reserved, placed, result);
            }
        }

        private static void FillSide(CityMeshBuilder b, Mats m, System.Random rng, Rect lot, Style style, Vector2 outward,
            bool street, List<Rect> reserved, List<Rect> placed, Result result)
        {
            bool horizontal = outward.y != 0f;
            float length = horizontal ? lot.width : lot.height;
            float depthLimit = (horizontal ? lot.height : lot.width) * 0.5f;

            float t = 0f;
            int guard = 0;

            while (t < length - 4f && guard++ < 200)
            {
                float w = Range(rng, style == Style.Pavillons ? 7f : 9f, style == Style.Pavillons ? 11f : 20f);
                if (t + w > length) w = length - t;
                if (w < (style == Style.Pavillons ? 6f : 7f)) break;

                float d = Mathf.Min(depthLimit, Range(rng, style == Style.Pavillons ? 7f : 11f, style == Style.Pavillons ? 9f : 18f));

                Rect footprint;
                if (horizontal)
                {
                    float x0 = lot.xMin + t;
                    footprint = outward.y > 0f
                        ? Rect.MinMaxRect(x0, lot.yMax - d, x0 + w, lot.yMax)
                        : Rect.MinMaxRect(x0, lot.yMin, x0 + w, lot.yMin + d);
                }
                else
                {
                    float z0 = lot.yMin + t;
                    footprint = outward.x > 0f
                        ? Rect.MinMaxRect(lot.xMax - d, z0, lot.xMax, z0 + w)
                        : Rect.MinMaxRect(lot.xMin, z0, lot.xMin + d, z0 + w);
                }

                if (Overlaps(footprint, reserved, 0.5f) || Overlaps(footprint, placed, 0.3f))
                {
                    t += 2.5f;
                    continue;
                }

                placed.Add(footprint);
                float height = Height(rng, style);

                if (style == Style.Pavillons) Pavilion(b, m, rng, footprint, outward, height);
                else Building(b, m, rng, footprint, outward, height, style, street, result);

                // Une ruelle de temps en temps : c'est là qu'on se bat à l'abri des regards.
                t += w + (rng.NextDouble() < 0.28 ? Range(rng, 2f, 4.5f) : 0f);
            }
        }

        private static float Height(System.Random rng, Style style)
        {
            switch (style)
            {
                case Style.Pavillons: return Range(rng, 3.2f, 5.5f);
                case Style.Centre: return Range(rng, 9f, 21f);
                case Style.Bord: return Range(rng, 18f, 44f);
                default: return Range(rng, 10f, 32f);
            }
        }

        private static void Building(CityMeshBuilder b, Mats m, System.Random rng, Rect f, Vector2 outward, float height,
            Style style, bool street, Result result)
        {
            int pick = rng.Next(5);
            Material facade;
            Vector2 tile;
            switch (pick)
            {
                case 0: facade = m.FacadeWarm; tile = FacadeTile; break;
                case 1: facade = m.FacadeCool; tile = FacadeTile; break;
                case 2: facade = m.Brick; tile = BrickTile; break;
                case 3: facade = m.DarkBrick; tile = DarkBrickTile; break;
                default: facade = m.FacadeWarm; tile = FacadeTile; break;
            }

            Vector3 center = new Vector3(f.center.x, height * 0.5f, f.center.y);
            b.Block(center, new Vector3(f.width, height, f.height), facade, tile, true);

            // Corniche : le haut des immeubles se découpe sur le ciel.
            b.Box(new Vector3(f.center.x, height + 0.25f, f.center.y), new Vector3(f.width + 0.5f, 0.5f, f.height + 0.5f),
                m.DarkConcrete, ConcreteTile, false);

            // Le rez-de-chaussée, côté rue : un socle en béton un peu en saillie.
            Vector3 n = new Vector3(outward.x, 0f, outward.y);
            float faceWidth = outward.y != 0f ? f.width : f.height;
            Vector3 face = new Vector3(f.center.x, 0f, f.center.y) + Vector3.Scale(n, new Vector3(f.width * 0.5f, 0f, f.height * 0.5f));
            float yaw = outward.y > 0f ? 0f : outward.y < 0f ? 180f : outward.x > 0f ? 90f : -90f;

            b.Block(face + n * 0.14f + Vector3.up * 1.6f, new Vector3(faceWidth + 0.1f, 3.2f, 0.28f), m.Concrete, ConcreteTile, false, yaw);

            // Une vitrine sur les rues passantes : verre sombre, enseigne au néon, et sa lueur.
            if (street && style != Style.Bord && rng.NextDouble() < 0.55)
            {
                float glass = Mathf.Min(faceWidth * 0.7f, 9f);
                b.Box(face + n * 0.3f + Vector3.up * 1.45f, new Vector3(glass, 2.3f, 0.08f), m.Glass, None, false, yaw);

                int neon = rng.Next(m.Neons.Length);
                if (rng.NextDouble() < 0.7)
                {
                    // Le nom du commerce en lettres de néon, sur un caisson sombre.
                    b.Box(face + n * 0.33f + Vector3.up * 3.55f, new Vector3(glass * 0.92f, 0.85f, 0.12f), m.DarkMetal, None, false, yaw);
                    RequestSign(result, rng, face + n * 0.44f + Vector3.up * 3.55f, n, glass * 0.85f, m.Neons[neon], null);
                }
                else
                {
                    b.Box(face + n * 0.42f + Vector3.up * 3.55f, new Vector3(glass * 0.8f, 0.42f, 0.16f), m.Neons[neon], None, false, yaw);
                }

                // Un store au-dessus de la vitrine, une fois sur trois.
                if (rng.NextDouble() < 0.35)
                {
                    b.Box(face + n * 0.9f + Vector3.up * 2.9f, new Vector3(glass + 0.4f, 0.07f, 1.5f),
                        m.Awnings[rng.Next(m.Awnings.Length)], None, false, yaw);
                    b.Box(face + n * 1.62f + Vector3.up * 2.72f, new Vector3(glass + 0.4f, 0.36f, 0.05f),
                        m.Awnings[rng.Next(m.Awnings.Length)], None, false, yaw);
                }

                Light light = NightStreetBuilder.AddLight(result.LightsRoot, "Vitrine",
                    face + n * 1.4f + Vector3.up * 2.6f, m.NeonColors[neon], 1.5f, 7.5f, false, false);
                light.transform.position = face + n * 1.4f + Vector3.up * 2.6f;
            }

            // Des balcons vitrés sur une façade de logements sur trois, côté rue.
            if (street && style != Style.Bord && rng.NextDouble() < 0.33 && faceWidth > 8f)
            {
                int floors = Mathf.Min(8, Mathf.FloorToInt(height / 3.2f) - 1);
                float span = faceWidth * Range(rng, 0.35f, 0.6f);
                for (int k = 1; k <= floors; k++)
                {
                    float y = k * 3.2f + 0.1f;
                    b.Box(face + n * 0.6f + Vector3.up * y, new Vector3(span, 0.12f, 1.2f), m.Concrete, None, false, yaw);
                    b.Box(face + n * 1.18f + Vector3.up * (y + 0.5f), new Vector3(span, 0.9f, 0.04f), m.Glass, None, false, yaw);
                    b.Box(face + n * 1.18f + Vector3.up * (y + 0.96f), new Vector3(span, 0.05f, 0.07f), m.DarkMetal, None, false, yaw);
                }
            }

            // Un panneau publicitaire sur les toits bas, face à la rue.
            if (street && style == Style.Ville && height < 20f && rng.NextDouble() < 0.16)
            {
                float bw = Mathf.Min(faceWidth * 0.75f, 12f);
                Vector3 across = Quaternion.Euler(0f, yaw, 0f) * Vector3.right;
                Vector3 foot = face - n * 1.2f + Vector3.up * height;
                b.Box(foot + across * (bw * 0.35f) + Vector3.up * 1.3f, new Vector3(0.22f, 2.6f, 0.22f), m.DarkMetal, None, false, yaw);
                b.Box(foot - across * (bw * 0.35f) + Vector3.up * 1.3f, new Vector3(0.22f, 2.6f, 0.22f), m.DarkMetal, None, false, yaw);
                b.Box(foot + Vector3.up * 3.6f, new Vector3(bw, 2.8f, 0.25f), m.DarkMetal, None, false, yaw);

                string[] ads = { "UBER BAGARRE", "LE VERTIGO", "HOTEL DE NUIT", "BOXE CLUB", "24H/24", "TAXI" };
                RequestSign(result, rng, foot + n * 0.16f + Vector3.up * 3.75f, n, bw * 0.9f, m.Neons[rng.Next(m.Neons.Length)],
                    ads[rng.Next(ads.Length)]);
            }

            // Sur le toit : une ou deux machines. Rien ne se voit d'en bas, sauf leur silhouette.
            if (rng.NextDouble() < 0.6)
            {
                b.Box(new Vector3(f.center.x + Range(rng, -f.width * 0.25f, f.width * 0.25f), height + 1f,
                        f.center.y + Range(rng, -f.height * 0.25f, f.height * 0.25f)),
                    new Vector3(Range(rng, 1.5f, 3f), 1.5f, Range(rng, 1.5f, 3f)), m.Metal, new Vector2(3f, 3f), false);
            }
        }

        /// <summary>Un pavillon : un rez-de-chaussée crépi, un toit à deux pentes, une fenêtre parfois allumée.</summary>
        private static void Pavilion(CityMeshBuilder b, Mats m, System.Random rng, Rect f, Vector2 outward, float height)
        {
            float w = f.width - 1.5f;
            float d = f.height - 1.5f;
            if (w < 3f || d < 3f) return;

            Vector3 center = new Vector3(f.center.x, height * 0.5f, f.center.y);
            b.Block(center, new Vector3(w, height, d), m.Render, new Vector2(3f, 3f), true);

            // Faîtage parallèle à la rue.
            float yaw = outward.y != 0f ? 0f : 90f;
            Vector3 roofSize = outward.y != 0f ? new Vector3(w + 0.6f, 1.8f, d + 0.6f) : new Vector3(d + 0.6f, 1.8f, w + 0.6f);
            b.GableRoof(new Vector3(f.center.x, height, f.center.y), roofSize, m.Tiles, m.Render, TilesTile, yaw);

            Vector3 n = new Vector3(outward.x, 0f, outward.y);
            Vector3 face = new Vector3(f.center.x, 0f, f.center.y) + Vector3.Scale(n, new Vector3(w * 0.5f, 0f, d * 0.5f));
            float faceYaw = outward.y > 0f ? 0f : outward.y < 0f ? 180f : outward.x > 0f ? 90f : -90f;

            Material window = rng.NextDouble() < 0.45 ? m.Neons[m.Neons.Length - 1] : m.Glass;
            b.Box(face + n * 0.04f + Vector3.up * 1.6f, new Vector3(1.3f, 1.1f, 0.06f), window, None, false, faceYaw);
            b.Box(face + n * 0.04f + Vector3.up * 1.05f + Quaternion.Euler(0f, faceYaw, 0f) * Vector3.right * 2.2f,
                new Vector3(1f, 2.1f, 0.06f), m.Wood, None, false, faceYaw);
        }

        private static bool Overlaps(Rect r, List<Rect> list, float inset)
        {
            Rect a = Rect.MinMaxRect(r.xMin + inset, r.yMin + inset, r.xMax - inset, r.yMax - inset);
            for (int i = 0; i < list.Count; i++)
            {
                Rect b = list[i];
                if (a.xMin < b.xMax && a.xMax > b.xMin && a.yMin < b.yMax && a.yMax > b.yMin) return true;
            }

            return false;
        }

        private static float Range(System.Random rng, float a, float b)
        {
            return a + (float)rng.NextDouble() * (b - a);
        }

        // ------------------------------------------------------------------ square

        private static void BuildPark(Transform parent, Mats m, System.Random rng, Result result)
        {
            CityMeshBuilder b = new CityMeshBuilder("Square des Tilleuls");
            result.MapParks.Add(Park);

            b.Ground(Park, 0.03f, m.Grass, GrassTile);

            // Allée en croix, bordure basse.
            b.Ground(Rect.MinMaxRect(Park.center.x - 1.3f, Park.yMin, Park.center.x + 1.3f, Park.yMax), 0.05f, m.Sidewalk, SidewalkTile);
            b.Ground(Rect.MinMaxRect(Park.xMin, Park.center.y - 1.3f, Park.xMax, Park.center.y + 1.3f), 0.05f, m.Sidewalk, SidewalkTile);
            b.Box(new Vector3(Park.center.x, 0.2f, Park.yMin + 0.1f), new Vector3(Park.width, 0.4f, 0.2f), m.Curb, CurbTile, false);
            b.Box(new Vector3(Park.center.x, 0.2f, Park.yMax - 0.1f), new Vector3(Park.width, 0.4f, 0.2f), m.Curb, CurbTile, false);

            // Des arbres : un tronc, et un feuillage en blocs croisés qui s'arrondit la nuit.
            for (int i = 0; i < 14; i++)
            {
                float x = Range(rng, Park.xMin + 2f, Park.xMax - 2f);
                float z = Range(rng, Park.yMin + 2f, Park.yMax - 2f);
                if (Mathf.Abs(x - Park.center.x) < 2.5f || Mathf.Abs(z - Park.center.y) < 2.5f) continue;

                float h = Range(rng, 3.2f, 4.6f);
                b.Box(new Vector3(x, h * 0.5f, z), new Vector3(0.32f, h, 0.32f), m.Wood, None, true);

                float s = Range(rng, 2.4f, 3.4f);
                b.Box(new Vector3(x, h + s * 0.35f, z), new Vector3(s, s * 0.8f, s), m.Leaves, None, false);
                b.Box(new Vector3(x, h + s * 0.35f, z), new Vector3(s * 0.8f, s * 0.95f, s * 0.8f), m.Leaves, None, false, 45f);
            }

            // Bancs le long de l'allée.
            for (int i = 0; i < 4; i++)
            {
                float z = Park.yMin + 8f + i * 13f;
                Vector3 seat = new Vector3(Park.center.x + 2.2f, 0.45f, z);
                b.Box(seat, new Vector3(0.5f, 0.08f, 1.8f), m.Wood, None, true);
                b.Box(seat + new Vector3(0.22f, 0.35f, 0f), new Vector3(0.08f, 0.5f, 1.8f), m.Wood, None, false);
                b.Box(seat + new Vector3(0f, -0.22f, 0f), new Vector3(0.4f, 0.44f, 0.12f), m.DarkMetal, None, false);
            }

            b.Flush(parent);

            // Deux lampes de square, basses et chaudes.
            for (int i = 0; i < 3; i++)
            {
                Vector3 p = new Vector3(Park.center.x - 1.8f, 0f, Park.yMin + 10f + i * 20f);
                CityMeshBuilder lamp = new CityMeshBuilder("Lampe du square " + (i + 1));
                lamp.Box(p + Vector3.up * 1.6f, new Vector3(0.1f, 3.2f, 0.1f), m.DarkMetal, None, false);
                lamp.Box(p + Vector3.up * 3.3f, new Vector3(0.36f, 0.36f, 0.36f), m.Lamp, None, false);
                lamp.Flush(parent, false, false);

                Light light = NightStreetBuilder.AddLight(result.LightsRoot, "Lampe du square", Vector3.zero,
                    new Color(1f, 0.78f, 0.5f), 1.6f, 11f, false, false);
                light.transform.position = p + Vector3.up * 3.1f;
            }
        }

        // ------------------------------------------------------------------ lampadaires

        private static void BuildLamps(Transform parent, Mats m, Result result)
        {
            CityMeshBuilder lamps = new CityMeshBuilder("Lampadaires de la ville");

            for (int xi = 0; xi < 5; xi++)
            {
                for (int zi = 0; zi < 4; zi++)
                {
                    Rect lot = Lot(xi, zi);
                    Vector4 w = SidewalkWidths(xi, zi);

                    if (w.w > 0f) LampRow(lamps, m, result, new Vector2(lot.xMin, lot.yMax + w.w - 0.6f), new Vector2(lot.xMax, lot.yMax + w.w - 0.6f), Vector3.forward);
                    if (w.z > 0f) LampRow(lamps, m, result, new Vector2(lot.xMin, lot.yMin - w.z + 0.6f), new Vector2(lot.xMax, lot.yMin - w.z + 0.6f), Vector3.back);
                    if (w.x > 0f) LampRow(lamps, m, result, new Vector2(lot.xMin - w.x + 0.6f, lot.yMin), new Vector2(lot.xMin - w.x + 0.6f, lot.yMax), Vector3.left);
                    if (w.y > 0f) LampRow(lamps, m, result, new Vector2(lot.xMax + w.y - 0.6f, lot.yMin), new Vector2(lot.xMax + w.y - 0.6f, lot.yMax), Vector3.right);
                }
            }

            lamps.Flush(parent);
        }

        private static void LampRow(CityMeshBuilder b, Mats m, Result result, Vector2 from, Vector2 to, Vector3 toRoad)
        {
            float length = Vector2.Distance(from, to);
            int count = Mathf.Max(1, Mathf.FloorToInt(length / 26f));
            Vector2 dir = (to - from).normalized;

            for (int i = 0; i < count; i++)
            {
                Vector2 p2 = from + dir * (length * (i + 0.5f) / count);

                // La rue du Vertigo a ses propres lampadaires.
                bool vertigo = false;
                for (int s = 0; s < StreetSidewalks.Length; s++) vertigo |= StreetSidewalks[s].Contains(p2);
                if (vertigo) continue;

                Vector3 p = new Vector3(p2.x, SidewalkHeight, p2.y);
                float yaw = Quaternion.LookRotation(toRoad).eulerAngles.y;

                b.Box(p + Vector3.up * 3.2f, new Vector3(0.16f, 6.4f, 0.16f), m.DarkMetal, None, true);
                b.Box(p + toRoad * 0.8f + Vector3.up * 6.35f, new Vector3(0.1f, 0.1f, 1.7f), m.DarkMetal, None, false, yaw);
                b.Box(p + toRoad * 1.55f + Vector3.up * 6.25f, new Vector3(0.34f, 0.14f, 0.62f), m.Lamp, None, false, yaw);

                GameObject go = new GameObject("Lampadaire");
                go.transform.SetParent(result.LightsRoot, false);
                go.transform.position = p + toRoad * 1.55f + Vector3.up * 6.1f;
                go.transform.rotation = Quaternion.LookRotation(Vector3.down, toRoad);

                Light light = go.AddComponent<Light>();
                light.type = LightType.Spot;
                light.spotAngle = 118f;
                light.range = 17f;
                light.intensity = 2.3f;
                light.color = new Color(1f, 0.74f, 0.44f);
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.Auto;
                light.bounceIntensity = 0f;
            }
        }

        // ------------------------------------------------------------------ bords de la ville

        private static void BuildBoundary(Transform parent, Mats m, System.Random rng)
        {
            CityMeshBuilder b = new CityMeshBuilder("Limites de la ville");

            // Un mur d'immeubles le long des quatre bords : entre deux tours, on voit une façade.
            float t = 1.5f;
            Rect inner = Rect.MinMaxRect(Bounds.xMin + t, Bounds.yMin + t, Bounds.xMax - t, Bounds.yMax - t);
            b.Block(new Vector3(inner.center.x, 20f, Bounds.yMax - 0.75f), new Vector3(Bounds.width, 40f, 1.5f), m.FacadeCool, FacadeTile, true);
            b.Block(new Vector3(inner.center.x, 20f, Bounds.yMin + 0.75f), new Vector3(Bounds.width, 40f, 1.5f), m.FacadeWarm, FacadeTile, true);
            b.Block(new Vector3(Bounds.xMin + 0.75f, 20f, inner.center.y), new Vector3(1.5f, 40f, Bounds.height), m.FacadeWarm, FacadeTile, true);
            b.Block(new Vector3(Bounds.xMax - 0.75f, 20f, inner.center.y), new Vector3(1.5f, 40f, Bounds.height), m.FacadeCool, FacadeTile, true);

            // Au bout des rues, une barrière de chantier devant le mur : la rue ne « finit » pas
            // dans le vide, elle est barrée.
            List<Rect> roads = Roads();
            for (int i = 0; i < roads.Count; i++)
            {
                Rect road = roads[i];
                bool eastWest = road.width > road.height;

                for (int end = -1; end <= 1; end += 2)
                {
                    Vector3 c = eastWest
                        ? new Vector3(end < 0 ? Bounds.xMin + 4f : Bounds.xMax - 4f, 0.6f, road.center.y)
                        : new Vector3(road.center.x, 0.6f, end < 0 ? Bounds.yMin + 4f : Bounds.yMax - 4f);
                    Vector3 size = eastWest ? new Vector3(0.3f, 1.2f, road.height) : new Vector3(road.width, 1.2f, 0.3f);

                    b.Box(c, size, m.Neons[2], None, true);
                    b.Box(c + Vector3.down * 0.4f, Vector3.Scale(size, new Vector3(1.4f, 0.3f, 1.4f)), m.DarkConcrete, ConcreteTile, false);
                }
            }

            b.Flush(parent);
        }

        /// <summary>La ville au-delà de la ville : des tours lointaines, jamais atteintes.</summary>
        private static void BuildSkyline(Transform parent, Mats m, System.Random rng)
        {
            CityMeshBuilder b = new CityMeshBuilder("Ville au loin");

            for (int i = 0; i < 36; i++)
            {
                float angle = i / 36f * Mathf.PI * 2f + Range(rng, -0.05f, 0.05f);
                float radius = Range(rng, 280f, 360f);
                float h = Range(rng, 40f, 110f);
                float w = Range(rng, 20f, 38f);
                Vector3 c = new Vector3(Mathf.Cos(angle) * radius, h * 0.5f, Mathf.Sin(angle) * radius * 0.85f);
                b.Block(c, new Vector3(w, h, w), i % 2 == 0 ? m.FacadeCool : m.FacadeWarm, FacadeTile * 1.6f, false);
            }

            b.Flush(parent, false, false);
        }

        // ------------------------------------------------------------------ voitures garées

        private static void BuildParkedCars(Transform parent, NightMaterialFactory.Palette night, System.Random rng,
            CarFactory drivable)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Voitures garees", parent, Vector3.zero);

            // Deux modèles, fusionnés une fois, recopiés partout : trente pièces par voiture
            // deviennent une poignée de maillages partagés.
            GameObject clean = CarTemplate(root.transform, night, false, "VoitureGaree");
            GameObject rusty = CarTemplate(root.transform, night, true, "VoitureGareeRouillee");

            List<Rect> roads = Roads();
            int placed = 0;

            for (int i = 0; i < roads.Count && placed < 22; i++)
            {
                Rect road = roads[i];
                bool eastWest = road.width > road.height;
                float half = (eastWest ? road.height : road.width) * 0.5f;
                float length = eastWest ? road.width : road.height;

                for (float t = 20f; t < length - 20f && placed < 22; t += Range(rng, 34f, 70f))
                {
                    for (int side = -1; side <= 1; side += 2)
                    {
                        if (rng.NextDouble() < 0.55) continue;

                        Vector2 c = eastWest
                            ? new Vector2(road.xMin + t, road.center.y + side * (half - 1.1f))
                            : new Vector2(road.center.x + side * (half - 1.1f), road.yMin + t);

                        if (StreetGround.Contains(c) || NearCrossing(c, roads, i, 13f)) continue;

                        // Dans l'axe de la rue, nez dans le sens de la voie (on roule à droite).
                        float yaw = eastWest ? (side > 0 ? -90f : 90f) : (side > 0 ? 0f : 180f);

                        // Une sur quatre n'est pas fermée à clé.
                        if (drivable != null && placed % 4 == 1)
                        {
                            drivable.Spawn(root.transform, rng.Next(drivable.PaintCount), new Vector3(c.x, 0.02f, c.y), yaw, "Voiture");
                            placed++;
                            continue;
                        }

                        GameObject car = Object.Instantiate(rng.NextDouble() < 0.3 ? rusty : clean, root.transform);
                        car.name = "Voiture garee";
                        car.transform.SetPositionAndRotation(new Vector3(c.x, 0f, c.y), Quaternion.Euler(0f, yaw, 0f));
                        car.SetActive(true);
                        SetStatic(car.transform);
                        placed++;
                    }
                }
            }

            Object.DestroyImmediate(clean);
            Object.DestroyImmediate(rusty);
        }

        private static void SetStatic(Transform t)
        {
            t.gameObject.isStatic = true;
            for (int i = 0; i < t.childCount; i++) SetStatic(t.GetChild(i));
        }

        private static bool NearCrossing(Vector2 c, List<Rect> roads, int self, float margin)
        {
            for (int j = 0; j < roads.Count; j++)
            {
                if (j == self) continue;
                Rect r = roads[j];
                Rect grown = Rect.MinMaxRect(r.xMin - margin, r.yMin - margin, r.xMax + margin, r.yMax + margin);
                if (grown.Contains(c)) return true;
            }

            return false;
        }

        /// <summary>Une voiture fusionnée, avec sa collision : le modèle des voitures garées et de la circulation.</summary>
        internal static GameObject CarTemplate(Transform parent, NightMaterialFactory.Palette night, bool rusty, string fileName)
        {
            GameObject holder = EditorBuildUtility.CreateEmpty(fileName, parent, Vector3.zero);
            NightStreetBuilder.Car(holder.transform, night, Vector3.zero, 0f, rusty, false);

            // NightStreetBuilder.Car construit la voiture le long de +X : on la tourne pour que
            // l'avant soit +Z, comme tout le reste du jeu (et comme les roues de la circulation).
            Transform built = holder.transform.GetChild(0);
            built.localRotation = Quaternion.Euler(0f, -90f, 0f);

            CityMeshBuilder.Collapse(holder, fileName);

            BoxCollider box = holder.AddComponent<BoxCollider>();
            box.center = new Vector3(0f, 0.75f, 0f);
            box.size = new Vector3(1.9f, 1.5f, 4.4f);

            holder.SetActive(false);
            return holder;
        }

        // ------------------------------------------------------------------ parcours

        /// <summary>Le tour d'un îlot au milieu du trottoir (au ras des façades le long du Vertigo, où il y a du mobilier).</summary>
        private static Vector3[] WalkLoop(Rect lot, int xi, int zi)
        {
            Vector4 w = SidewalkWidths(xi, zi);
            float west = lot.xMin - w.x * 0.5f;
            float east = lot.xMax + w.y * 0.5f;
            float south = lot.yMin - w.z * 0.5f;
            float north = lot.yMax + w.w * 0.5f;

            if (zi == 2) south = lot.yMin - 1.1f;
            if (zi == 1) north = lot.yMax + 0.8f;

            float y = SidewalkHeight;
            return new[]
            {
                new Vector3(west, y, north), new Vector3((west + east) * 0.5f, y, north), new Vector3(east, y, north),
                new Vector3(east, y, (north + south) * 0.5f), new Vector3(east, y, south),
                new Vector3((west + east) * 0.5f, y, south), new Vector3(west, y, south),
                new Vector3(west, y, (north + south) * 0.5f)
            };
        }

        /// <summary>
        /// Boucles de circulation (sens horaire vu du dessus, voie de droite = côté intérieur) :
        /// l'est, l'ouest et le grand tour. Le boulevard, encombré devant le Vertigo, en est exclu.
        /// </summary>
        private static void AddDriveLoops(Result result)
        {
            result.DriveLoops.Add(Lane(new[] { new Vector2(62f, 102f), new Vector2(150f, 102f), new Vector2(150f, -122f), new Vector2(62f, -122f) }));
            result.DriveLoops.Add(Lane(new[] { new Vector2(-150f, 102f), new Vector2(-62f, 102f), new Vector2(-62f, -122f), new Vector2(-150f, -122f) }));
            result.DriveLoops.Add(Lane(new[] { new Vector2(-150f, 102f), new Vector2(150f, 102f), new Vector2(150f, -122f), new Vector2(-150f, -122f) }));
        }

        private static Vector3[] Lane(Vector2[] corners)
        {
            const float offset = 3f;
            int n = corners.Length;
            Vector2[] lane = new Vector2[n];

            // Chaque coin décalé des deux côtés « droite » : la boucle tourne dans le sens horaire,
            // la voie de droite est à l'intérieur.
            for (int i = 0; i < n; i++)
            {
                Vector2 dIn = (corners[i] - corners[(i + n - 1) % n]).normalized;
                Vector2 dOut = (corners[(i + 1) % n] - corners[i]).normalized;
                lane[i] = corners[i] + (new Vector2(dIn.y, -dIn.x) + new Vector2(dOut.y, -dOut.x)) * offset;
            }

            List<Vector3> points = new List<Vector3>();
            for (int i = 0; i < n; i++)
            {
                Vector2 a = lane[i];
                Vector2 b = lane[(i + 1) % n];
                points.Add(new Vector3(a.x, 0f, a.y));

                // Des points intermédiaires : la voiture garde sa voie au lieu de couper.
                int steps = Mathf.FloorToInt(Vector2.Distance(a, b) / 30f);
                for (int k = 1; k < steps; k++)
                {
                    Vector2 p = Vector2.Lerp(a, b, k / (float)steps);
                    points.Add(new Vector3(p.x, 0f, p.y));
                }
            }

            return points.ToArray();
        }

        private static void AddSpots(Result result)
        {
            Spot(result, "Devant le Vertigo", new Vector3(9.5f, 0.2f, 11f), 200f);
            Spot(result, "Ruelle derrière le Vertigo", new Vector3(-6f, 0.05f, 38.5f), 90f);
            Spot(result, "Parking du Vertigo", new Vector3(0f, 0.05f, 64f), 180f);
            Spot(result, "Square des Tilleuls", new Vector3(-35f, 0.1f, -72f), 0f);
            Spot(result, "Devant le tabac", new Vector3(18f, 0.2f, -16.4f), 180f);
            Spot(result, "Avenue Est, côté parc", new Vector3(70f, 0.2f, 50f), -90f);
            Spot(result, "Avenue Ouest", new Vector3(-70f, 0.2f, -60f), 90f);
            Spot(result, "Rue du Nord", new Vector3(-100f, 0.2f, 94f), 180f);
            Spot(result, "Rue du Sud", new Vector3(100f, 0.2f, -114f), 0f);
            Spot(result, "Boulevard, côté ouest", new Vector3(-120f, 0.2f, 11f), 180f);
            Spot(result, "Boulevard, côté est", new Vector3(120f, 0.2f, -15f), 0f);
            Spot(result, "Lotissement des pavillons", new Vector3(-10f, 0.05f, -81.6f), 90f);
            Spot(result, "Rue des Glycines", new Vector3(-142f, 0.2f, -60f), 90f);
            Spot(result, "Allée du Moulin", new Vector3(142f, 0.2f, -80f), -90f);
        }

        private static void Spot(Result result, string name, Vector3 position, float yaw)
        {
            result.Spots.Add(new OpenWorldDirector.Spot { name = name, position = position, yaw = yaw });
        }

        private static void AddLandmarks(Result result)
        {
            Landmark(result, "Le Vertigo", new Vector2(0f, 20f), new Color(1f, 0.25f, 0.7f));
            Landmark(result, "Parking du Vertigo", new Vector2(0f, 62f), new Color(0.4f, 0.9f, 1f));
            Landmark(result, "La planque", new Vector2(HouseOrigin.x, HouseOrigin.z), new Color(0.5f, 1f, 0.55f));
            Landmark(result, "Square des Tilleuls", new Vector2(Park.center.x, Park.center.y), new Color(0.45f, 0.85f, 0.4f));
            Landmark(result, "Tabac · Snack · Nuit 24h", new Vector2(0f, -17f), new Color(1f, 0.7f, 0.3f));
            Landmark(result, "Les Glycines", new Vector2(-106f, -65f), new Color(0.75f, 0.6f, 1f));
            Landmark(result, "Le Moulin", new Vector2(106f, -65f), new Color(0.75f, 0.6f, 1f));
        }

        private static void Landmark(Result result, string label, Vector2 position, Color color)
        {
            result.Landmarks.Add(new CityMap.Landmark { label = label, position = position, color = color });
        }

        // ------------------------------------------------------------------ matières

        private const string MaterialsFolder = "Assets/UberBagarre/Art/Ville/Matieres";

        private static Mats CreateMaterials(NightMaterialFactory.Palette night)
        {
            Mats m = new Mats();
            m.Asphalt = Copy(night.WetAsphalt, "M_Ville_Bitume");
            if (m.Asphalt != null)
            {
                // Les UV sont déjà en carreaux de 3,6 m : les flaques, elles, font 18 m.
                if (m.Asphalt.HasProperty("_WetMask")) m.Asphalt.SetTextureScale("_WetMask", new Vector2(0.2f, 0.2f));
                // Pas de reflet planaire sur la ville entière (un second rendu de toute la
                // ville) : la brillance mouillée et la sonde font le travail.
                if (m.Asphalt.HasProperty("_ReflectionStrength")) m.Asphalt.SetFloat("_ReflectionStrength", 0f);
                EditorUtility.SetDirty(m.Asphalt);
            }

            m.Sidewalk = Copy(night.Sidewalk, "M_Ville_Trottoir");
            m.Curb = Copy(night.Curb, "M_Ville_Bordure");
            m.Paint = night.RoadPaint;
            m.Brick = Copy(night.Brick, "M_Ville_Brique");
            m.DarkBrick = Copy(night.DarkBrick, "M_Ville_BriqueSombre");
            m.Concrete = Copy(night.Concrete, "M_Ville_Beton");
            m.DarkConcrete = Copy(night.DarkConcrete, "M_Ville_BetonSombre");
            m.FacadeWarm = Copy(night.FacadeLit, "M_Ville_FacadeChaude");
            m.FacadeCool = Copy(night.FacadeLitCool, "M_Ville_FacadeFroide");
            m.Metal = Copy(night.Metal, "M_Ville_Metal");
            m.DarkMetal = night.DarkMetal;
            m.Glass = night.Glass;
            m.Wood = night.Wood;
            m.Lamp = night.NeonWarm;

            Texture2D grass = EditorBuildUtility.CreateOrUpdateGrainTexture(NightMaterialFactory.TexturesFolder, "T_HerbeVille", 256,
                new Color(0.14f, 0.19f, 0.11f), 0.20f, 0.11f, 0.05f, 0.11f, 4411);
            m.Grass = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_Herbe", Color.white, 0.12f, 0f, grass, Vector2.one);
            m.Leaves = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_Feuillage", new Color(0.07f, 0.13f, 0.07f), 0.1f, 0f);
            m.Tiles = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_Tuiles", new Color(0.30f, 0.14f, 0.10f), 0.2f, 0f,
                EditorBuildUtility.CreateOrUpdateBrickTexture(NightMaterialFactory.TexturesFolder, "T_TuilesVille", 128, 8,
                    new Color(0.34f, 0.17f, 0.12f), new Color(0.22f, 0.10f, 0.08f)), Vector2.one);
            m.Render = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_Crepi", new Color(0.46f, 0.43f, 0.39f), 0.08f, 0f);

            m.Neons = new[] { night.NeonMagenta, night.NeonCyan, night.NeonRed, night.NeonGreen, night.NeonBlue, night.NeonWarm };
            m.Awnings = new[]
            {
                EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_StoreRouge", new Color(0.40f, 0.05f, 0.05f), 0.1f, 0f),
                EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_StoreVert", new Color(0.05f, 0.22f, 0.12f), 0.1f, 0f),
                EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_StoreBleu", new Color(0.06f, 0.10f, 0.26f), 0.1f, 0f),
                EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_StoreCreme", new Color(0.55f, 0.50f, 0.40f), 0.1f, 0f)
            };
            m.NeonColors = new[]
            {
                new Color(1f, 0.2f, 0.62f), new Color(0.2f, 0.9f, 1f), new Color(1f, 0.16f, 0.14f),
                new Color(0.32f, 1f, 0.42f), new Color(0.3f, 0.45f, 1f), new Color(1f, 0.65f, 0.28f)
            };

            return m;
        }

        /// <summary>
        /// Copie d'une matière de la rue, répétition ramenée à 1 : les UV de la ville sont déjà en
        /// mètres. Mise à jour en place si elle existe (les scènes gardent leur référence).
        /// </summary>
        private static Material Copy(Material source, string name)
        {
            if (source == null) return null;

            EditorBuildUtility.EnsureFolder(MaterialsFolder);
            string path = MaterialsFolder + "/" + name + ".mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(source);
                AssetDatabase.CreateAsset(material, path);
            }
            else
            {
                material.shader = source.shader;
                material.CopyPropertiesFromMaterial(source);
            }

            material.name = name;
            material.mainTextureScale = Vector2.one;
            material.mainTextureOffset = Vector2.zero;
            EditorUtility.SetDirty(material);
            return material;
        }
    }
}
