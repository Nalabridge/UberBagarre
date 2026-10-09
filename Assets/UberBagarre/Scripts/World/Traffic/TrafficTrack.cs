using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La route d'une boucle de circulation, préparée pour qu'une voiture puisse vraiment la
    /// suivre.
    ///
    /// La boucle d'origine (celle des voitures de Schedule 1) parcourt chaque rue dans les deux
    /// sens, comme un fil qui ferait le tour d'un arbre : elle tourne aux carrefours en angle
    /// vif, et au bout des impasses elle fait demi-tour sur un rayon d'un mètre et demi — ce
    /// qu'aucune voiture ne sait faire. Suivie telle quelle, elle envoyait les voitures dans
    /// les trottoirs et les unes dans les autres.
    ///
    /// Ici :
    /// - les virages sont arrondis (un arc de cercle de 4 à 9 m de rayon, selon la place) ;
    /// - les demi-tours deviennent des <see cref="Turnaround"/> : la voiture s'arrête au bout de
    ///   l'impasse et fait un demi-tour en trois manœuvres, comme un vrai conducteur ;
    /// - la route est rééchantillonnée tous les mètres, avec sa courbure et un profil de vitesse
    ///   (on ralentit AVANT un virage, on réaccélère après, sans à-coups) ;
    /// - les endroits où la boucle se croise elle-même (les carrefours) deviennent des
    ///   <see cref="Zone"/> : une seule voiture à la fois, comme à un stop.
    ///
    /// Le tout en C# pur (pas de physique) : la même classe sert au jeu et aux simulations hors
    /// Unity qui vérifient qu'aucune voiture ne se percute.
    /// </summary>
    public sealed class TrafficTrack
    {
        public const float Step = 1f;

        /// <summary>Un demi-tour au bout d'une impasse : le repère de la manœuvre.</summary>
        public sealed class Turnaround
        {
            /// <summary>Où la voiture s'arrête pour commencer (essieu arrière), et où elle reprend la route.</summary>
            public float Start;
            public float Rejoin;

            /// <summary>Repère : origine sur la voie d'arrivée, à hauteur du bout ; X vers le bout, Y vers la voie de retour.</summary>
            public Vector3 Origin;
            public Vector3 AxisX;
            public Vector3 AxisY;

            /// <summary>L'écart entre les deux voies (m).</summary>
            public float Gap;

            /// <summary>+1 si la voie de retour est à gauche (on braque à gauche en avançant), -1 sinon.</summary>
            public float Side;

            /// <summary>La zone exclusive du demi-tour (une voiture à la fois).</summary>
            public Zone Zone;

            /// <summary>Un point du repère de la manœuvre, en coordonnées monde (hauteur de la route).</summary>
            public Vector3 World(float x, float y)
            {
                return Origin + AxisX * x + AxisY * y;
            }

            public void Local(Vector3 world, out float x, out float y)
            {
                Vector3 d = world - Origin;
                x = d.x * AxisX.x + d.z * AxisX.z;
                y = d.x * AxisY.x + d.z * AxisY.z;
            }
        }

        /// <summary>Un passage de la boucle dans une zone partagée : de l'entrée à la sortie (abscisses).</summary>
        public struct Interval
        {
            public float In;
            public float Out;
        }

        /// <summary>Un carrefour (ou un demi-tour) : là où deux passages de la boucle se coupent.</summary>
        public sealed class Zone
        {
            public int Id;
            public Vector3 Center;
            public float Radius;
            public readonly List<Interval> Intervals = new List<Interval>();
            public Turnaround Turn;
        }

        private readonly Vector3[] _points;
        private readonly Vector3[] _tangents;
        private readonly float[] _curvature;
        private readonly float[] _limit;
        private readonly int _count;
        private readonly float _length;

        public readonly List<Turnaround> Turnarounds = new List<Turnaround>();
        public readonly List<Zone> Zones = new List<Zone>();

        /// <summary>La vitesse de croisière (m/s) : le profil ne la dépasse jamais.</summary>
        public readonly float Cruise;

        // Les réglages de conduite (réalistes pour une ville : 40 km/h, freinages doux).
        public const float LateralAcceleration = 2.3f;
        public const float Deceleration = 2.1f;
        public const float Acceleration = 1.5f;
        public const float ZoneSpeed = 6.5f;

        public float Length { get { return _length; } }
        public int Count { get { return _count; } }

        public TrafficTrack(Vector3[] loop, float cruise)
        {
            Cruise = Mathf.Max(3f, cruise);

            List<Vector3> raw = Clean(loop);
            bool[] inTurn;
            List<int[]> turns = FindTurnarounds(raw, out inTurn);
            List<Vector3> smooth = Fillet(raw, inTurn);

            // Rééchantillonnage tous les mètres, puis les derniers coins trop vifs adoucis.
            List<Vector3> resampled = Resample(smooth, Step);
            Relax(resampled, raw, inTurn);
            _count = resampled.Count;
            _points = resampled.ToArray();
            _length = _count * Step;

            _tangents = new Vector3[_count];
            for (int i = 0; i < _count; i++)
            {
                Vector3 d = Flat(_points[(i + 1) % _count] - _points[(i - 1 + _count) % _count]);
                _tangents[i] = d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
            }

            _curvature = new float[_count];
            for (int i = 0; i < _count; i++)
            {
                Vector3 a = _tangents[(i - 1 + _count) % _count];
                Vector3 b = _tangents[(i + 1) % _count];
                float angle = Mathf.Acos(Mathf.Clamp(a.x * b.x + a.z * b.z, -1f, 1f));
                _curvature[i] = angle / (2f * Step);
            }

            BuildTurnarounds(raw, turns);
            BuildZones();

            _limit = new float[_count];
            BuildSpeedProfile();
        }

        // ================================================================== interrogation

        /// <summary>Ramène une abscisse dans [0, longueur).</summary>
        public float Wrap(float s)
        {
            s %= _length;
            return s < 0f ? s + _length : s;
        }

        /// <summary>La distance en avançant le long de la boucle, de <paramref name="from"/> à <paramref name="to"/>.</summary>
        public float Ahead(float from, float to)
        {
            return Wrap(to - from);
        }

        public Vector3 Point(float s)
        {
            s = Wrap(s);
            int i = Mathf.FloorToInt(s / Step) % _count;
            float t = s / Step - Mathf.Floor(s / Step);
            Vector3 a = _points[i];
            Vector3 b = _points[(i + 1) % _count];
            return a + (b - a) * t;
        }

        public Vector3 Tangent(float s)
        {
            s = Wrap(s);
            int i = Mathf.FloorToInt(s / Step) % _count;
            float t = s / Step - Mathf.Floor(s / Step);
            Vector3 a = _tangents[i];
            Vector3 b = _tangents[(i + 1) % _count];
            Vector3 d = a + (b - a) * t;
            return d.sqrMagnitude > 1e-6f ? d.normalized : a;
        }

        public float Curvature(float s)
        {
            return _curvature[Mathf.FloorToInt(Wrap(s) / Step) % _count];
        }

        /// <summary>La courbure signée : positive quand la route tourne à droite (vue de dessus, comme le volant).</summary>
        public float SignedCurvature(float s)
        {
            int i = Mathf.FloorToInt(Wrap(s) / Step) % _count;
            Vector3 a = _tangents[(i - 1 + _count) % _count];
            Vector3 b = _tangents[(i + 1) % _count];
            float cross = a.x * b.z - a.z * b.x;
            return cross > 0f ? -_curvature[i] : _curvature[i];
        }

        /// <summary>La vitesse permise par la route en ce point (virages, carrefours, demi-tours).</summary>
        public float Limit(float s)
        {
            s = Wrap(s);
            int i = Mathf.FloorToInt(s / Step) % _count;
            float t = s / Step - Mathf.Floor(s / Step);
            return Mathf.Lerp(_limit[i], _limit[(i + 1) % _count], t);
        }

        /// <summary>
        /// L'abscisse du point de la route le plus proche de <paramref name="position"/>,
        /// cherché autour de <paramref name="hint"/> (± <paramref name="window"/> mètres) : la
        /// boucle repasse près d'elle-même (l'autre voie de la rue), une recherche globale
        /// sauterait d'une voie à l'autre. Une fenêtre négative cherche partout.
        /// </summary>
        public float Project(Vector3 position, float hint, float window)
        {
            int first, last;
            if (window < 0f)
            {
                first = 0;
                last = _count - 1;
            }
            else
            {
                int center = Mathf.FloorToInt(Wrap(hint) / Step);
                int half = Mathf.Max(2, Mathf.FloorToInt(window / Step));
                first = center - half;
                last = center + half;
            }

            float best = float.MaxValue;
            float bestS = hint;
            for (int k = first; k <= last; k++)
            {
                int i = ((k % _count) + _count) % _count;
                Vector3 a = _points[i];
                Vector3 b = _points[(i + 1) % _count];
                Vector3 ab = Flat(b - a);
                float len2 = Mathf.Max(1e-6f, ab.sqrMagnitude);
                float t = Mathf.Clamp01(((position.x - a.x) * ab.x + (position.z - a.z) * ab.z) / len2);
                float dx = a.x + ab.x * t - position.x;
                float dz = a.z + ab.z * t - position.z;
                float d = dx * dx + dz * dz;
                if (d < best)
                {
                    best = d;
                    bestS = (i + t) * Step;
                }
            }

            return Wrap(bestS);
        }

        private Dictionary<long, List<int>> _cells;

        /// <summary>La distance (à plat) de <paramref name="position"/> à la route la plus proche, jusqu'à 8 m (au-delà : 8).</summary>
        public float DistanceToRoad(Vector3 position)
        {
            if (_cells == null)
            {
                _cells = new Dictionary<long, List<int>>();
                for (int i = 0; i < _count; i++)
                {
                    long key = Cell(_points[i], 4f);
                    List<int> list;
                    if (!_cells.TryGetValue(key, out list)) _cells[key] = list = new List<int>();
                    list.Add(i);
                }
            }

            float best = 64f;
            int cx = Mathf.FloorToInt(position.x / 4f);
            int cz = Mathf.FloorToInt(position.z / 4f);
            for (int dx = -2; dx <= 2; dx++)
            {
                for (int dz = -2; dz <= 2; dz++)
                {
                    List<int> list;
                    if (!_cells.TryGetValue(Key(cx + dx, cz + dz), out list)) continue;
                    for (int k = 0; k < list.Count; k++)
                    {
                        Vector3 d = _points[list[k]] - position;
                        float d2 = d.x * d.x + d.z * d.z;
                        if (d2 < best) best = d2;
                    }
                }
            }

            return Mathf.Sqrt(best);
        }

        /// <summary>L'écart latéral (signé, + à droite) de <paramref name="position"/> à la route en <paramref name="s"/>.</summary>
        public float Lateral(Vector3 position, float s)
        {
            Vector3 p = Point(s);
            Vector3 t = Tangent(s);
            return (position.x - p.x) * t.z - (position.z - p.z) * t.x;
        }

        // ================================================================== préparation

        private static List<Vector3> Clean(Vector3[] loop)
        {
            List<Vector3> points = new List<Vector3>(loop.Length);
            for (int i = 0; i < loop.Length; i++)
            {
                if (points.Count > 0 && Flat(loop[i] - points[points.Count - 1]).sqrMagnitude < 0.3f * 0.3f) continue;
                points.Add(loop[i]);
            }

            while (points.Count > 3 && Flat(points[0] - points[points.Count - 1]).sqrMagnitude < 0.3f * 0.3f) points.RemoveAt(points.Count - 1);
            return points;
        }

        /// <summary>
        /// Les demi-tours : là où la boucle repart dans l'autre sens après moins de 18 m de
        /// virage. Rend pour chacun l'indice du dernier point de la voie d'arrivée et le premier
        /// de la voie de retour.
        /// </summary>
        private static List<int[]> FindTurnarounds(List<Vector3> raw, out bool[] inTurn)
        {
            int n = raw.Count;
            inTurn = new bool[n];
            List<int[]> turns = new List<int[]>();
            float[] arc = new float[n + 1];
            for (int i = 0; i < n; i++) arc[i + 1] = arc[i] + Flat(raw[(i + 1) % n] - raw[i]).magnitude;

            int iStart = 0;
            while (iStart < n)
            {
                Vector3 inDir = Direction(raw, iStart);
                int found = -1;
                for (int j = iStart + 1; j < iStart + 40; j++)
                {
                    float travelled = Distance(arc, n, iStart, j);
                    if (travelled > 18f) break;
                    Vector3 outDir = Direction(raw, j % n);
                    if (inDir.x * outDir.x + inDir.z * outDir.z < -0.94f)
                    {
                        // Les deux voies sont parallèles, à moins de 7 m l'une de l'autre.
                        Vector3 d = Flat(raw[j % n] - raw[iStart]);
                        float lateral = Mathf.Abs(d.x * inDir.z - d.z * inDir.x);
                        if (lateral < 7f) found = j;
                        break;
                    }
                }

                if (found < 0)
                {
                    iStart++;
                    continue;
                }

                // Le dernier point encore dans l'axe de la voie d'arrivée.
                int a = iStart;
                while (a + 1 < found && Vector3.Dot(Direction(raw, (a + 1) % n), inDir) > 0.97f) a++;
                int b = found;
                turns.Add(new[] { a % n, b % n });
                for (int k = a; k <= b; k++) inTurn[k % n] = true;
                iStart = found + 1;
            }

            return turns;
        }

        private static float Distance(float[] arc, int n, int from, int to)
        {
            float total = arc[n];
            float a = arc[from % n];
            float b = arc[to % n] + (to / n - from / n) * total;
            return b - a;
        }

        private static Vector3 Direction(List<Vector3> raw, int i)
        {
            int n = raw.Count;
            Vector3 d = Flat(raw[(i + 1) % n] - raw[i]);
            return d.sqrMagnitude > 1e-6f ? d.normalized : Vector3.forward;
        }

        /// <summary>
        /// Arrondit chaque changement de cap (hors demi-tours) par un arc de cercle tangent aux
        /// deux côtés, aussi large que la place le permet (9 m au plus).
        /// </summary>
        private static List<Vector3> Fillet(List<Vector3> raw, bool[] inTurn)
        {
            int n = raw.Count;

            // Simplification : les petits zigzags du relevé disparaissent, les coins restent.
            bool[] keep = new bool[n];
            Simplify(raw, 0, n / 2, keep, 0.2f);
            Simplify(raw, n / 2, n, keep, 0.2f);
            for (int i = 0; i < n; i++)
            {
                if (inTurn[i] || inTurn[(i + 1) % n] || inTurn[(i - 1 + n) % n]) keep[i] = true;
            }

            List<Vector3> corners = new List<Vector3>();
            List<bool> frozen = new List<bool>();
            for (int i = 0; i < n; i++)
            {
                if (!keep[i]) continue;
                corners.Add(raw[i]);
                frozen.Add(inTurn[i]);
            }

            int m = corners.Count;
            List<Vector3> result = new List<Vector3>(m * 3);
            for (int i = 0; i < m; i++)
            {
                Vector3 p = corners[i];
                Vector3 prev = corners[(i - 1 + m) % m];
                Vector3 next = corners[(i + 1) % m];
                Vector3 din = Flat(p - prev);
                Vector3 dout = Flat(next - p);
                float lin = din.magnitude;
                float lout = dout.magnitude;

                if (frozen[i] || lin < 0.05f || lout < 0.05f)
                {
                    result.Add(p);
                    continue;
                }

                din /= lin;
                dout /= lout;
                float cos = Mathf.Clamp(din.x * dout.x + din.z * dout.z, -1f, 1f);
                float theta = Mathf.Acos(cos);
                if (theta < 6f * Mathf.Deg2Rad || theta > 170f * Mathf.Deg2Rad)
                {
                    result.Add(p);
                    continue;
                }

                float tanHalf = Mathf.Tan(theta * 0.5f);
                float maxTangent = 0.48f * Mathf.Min(lin, lout);
                float radius = Mathf.Min(9f, maxTangent / tanHalf);
                if (radius < 0.5f)
                {
                    result.Add(p);
                    continue;
                }

                float tangent = radius * tanHalf;
                Vector3 start = p - din * tangent;
                float turn = din.x * dout.z - din.z * dout.x > 0f ? -1f : 1f;
                // Le centre est du côté où l'on tourne.
                Vector3 normal = new Vector3(din.z, 0f, -din.x) * turn;
                Vector3 center = start + normal * radius;

                int steps = Mathf.Max(2, Mathf.CeilToInt(theta * radius / 0.75f));
                Vector3 from = start - center;
                for (int k = 0; k <= steps; k++)
                {
                    float a = -turn * theta * k / steps;
                    float c = Mathf.Cos(a), s = Mathf.Sin(a);
                    Vector3 r = new Vector3(from.x * c - from.z * s, 0f, from.x * s + from.z * c);
                    Vector3 q = center + r;
                    q.y = p.y;
                    result.Add(q);
                }
            }

            return result;
        }

        private static void Simplify(List<Vector3> raw, int first, int last, bool[] keep, float tolerance)
        {
            // Douglas-Peucker sur [first, last] (last exclu, mais le point last % n est gardé).
            int n = raw.Count;
            keep[first % n] = true;
            keep[last % n] = true;
            Stack<int[]> stack = new Stack<int[]>();
            stack.Push(new[] { first, last });
            while (stack.Count > 0)
            {
                int[] span = stack.Pop();
                Vector3 a = raw[span[0] % n];
                Vector3 b = raw[span[1] % n];
                Vector3 ab = Flat(b - a);
                float len = ab.magnitude;
                float worst = 0f;
                int index = -1;
                for (int i = span[0] + 1; i < span[1]; i++)
                {
                    Vector3 ap = Flat(raw[i % n] - a);
                    float d = len > 1e-4f ? Mathf.Abs(ap.x * ab.z - ap.z * ab.x) / len : ap.magnitude;
                    if (d > worst)
                    {
                        worst = d;
                        index = i;
                    }
                }

                if (index >= 0 && worst > tolerance)
                {
                    keep[index % n] = true;
                    stack.Push(new[] { span[0], index });
                    stack.Push(new[] { index, span[1] });
                }
            }
        }

        /// <summary>
        /// Les coins qu'un arc n'a pas pu arrondir (deux coins trop proches, un relevé
        /// cabossé) : chaque point trop courbé glisse vers le milieu de ses voisins, petit à
        /// petit, sans jamais s'éloigner de plus d'un mètre de la route d'origine. Les demi-tours
        /// ne bougent pas (la manœuvre les remplace).
        /// </summary>
        private static void Relax(List<Vector3> path, List<Vector3> raw, bool[] inTurn)
        {
            int n = path.Count;
            const float maxCurvature = 1f / 6f;
            const float maxShift = 1.1f;
            bool[] frozen = new bool[n];
            Vector3[] origin = path.ToArray();

            // Les points proches d'un demi-tour restent où ils sont.
            List<Vector3> turnPoints = new List<Vector3>();
            for (int i = 0; i < raw.Count; i++)
            {
                if (inTurn[i]) turnPoints.Add(raw[i]);
            }

            for (int i = 0; i < n && turnPoints.Count > 0; i++)
            {
                for (int k = 0; k < turnPoints.Count; k++)
                {
                    if (Flat(path[i] - turnPoints[k]).sqrMagnitude < 9f * 9f)
                    {
                        frozen[i] = true;
                        break;
                    }
                }
            }

            for (int pass = 0; pass < 300; pass++)
            {
                bool changed = false;
                for (int i = 0; i < n; i++)
                {
                    if (frozen[i]) continue;
                    Vector3 a = path[(i - 1 + n) % n];
                    Vector3 b = path[i];
                    Vector3 c = path[(i + 1) % n];
                    if (Menger(a, b, c) <= maxCurvature) continue;

                    // Le point et ses deux voisins, vers le milieu.
                    for (int k = -1; k <= 1; k++)
                    {
                        int j = (i + k + n) % n;
                        if (frozen[j]) continue;
                        Vector3 mid = (path[(j - 1 + n) % n] + path[(j + 1) % n]) * 0.5f;
                        Vector3 moved = path[j] + (mid - path[j]) * (k == 0 ? 0.5f : 0.25f);
                        Vector3 shift = Flat(moved - origin[j]);
                        if (shift.sqrMagnitude > maxShift * maxShift) moved = origin[j] + shift.normalized * maxShift + Vector3.up * (moved.y - origin[j].y);
                        moved.y = origin[j].y;
                        path[j] = moved;
                    }

                    changed = true;
                }

                if (!changed) break;
            }
        }

        /// <summary>La courbure du cercle qui passe par trois points (1 / rayon).</summary>
        private static float Menger(Vector3 a, Vector3 b, Vector3 c)
        {
            float abx = b.x - a.x, abz = b.z - a.z;
            float bcx = c.x - b.x, bcz = c.z - b.z;
            float acx = c.x - a.x, acz = c.z - a.z;
            float cross = Mathf.Abs(abx * bcz - abz * bcx);
            float product = Mathf.Sqrt((abx * abx + abz * abz) * (bcx * bcx + bcz * bcz) * (acx * acx + acz * acz));
            return product > 1e-6f ? 2f * cross / product : 0f;
        }

        private static List<Vector3> Resample(List<Vector3> path, float step)
        {
            int n = path.Count;
            List<Vector3> result = new List<Vector3>();
            float carry = 0f;
            for (int i = 0; i < n; i++)
            {
                Vector3 a = path[i];
                Vector3 b = path[(i + 1) % n];
                float len = Flat(b - a).magnitude;
                if (len < 1e-4f) continue;
                float t = carry;
                while (t < len)
                {
                    result.Add(a + (b - a) * (t / len));
                    t += step;
                }

                carry = t - len;
            }

            return result;
        }

        // ------------------------------------------------------------------ demi-tours

        private void BuildTurnarounds(List<Vector3> raw, List<int[]> turns)
        {
            for (int k = 0; k < turns.Count; k++)
            {
                Vector3 a = raw[turns[k][0]];
                Vector3 b = raw[turns[k][1]];
                Vector3 inDir = Direction(raw, (turns[k][0] - 1 + raw.Count) % raw.Count);

                // Le bout de l'impasse : le point du virage le plus loin dans l'axe d'arrivée.
                float tip = 0f;
                for (int i = turns[k][0]; i != turns[k][1]; i = (i + 1) % raw.Count)
                {
                    tip = Mathf.Max(tip, Vector3.Dot(Flat(raw[i] - a), inDir));
                }

                Vector3 across = Flat(b - a);
                float side = inDir.x * across.z - inDir.z * across.x > 0f ? 1f : -1f;
                // Gauche de la direction (x, z) : (-z, x).
                Vector3 left = new Vector3(-inDir.z, 0f, inDir.x);
                Vector3 axisY = left * side;
                float gap = Mathf.Abs(Vector3.Dot(across, axisY));
                if (gap < 1.5f || gap > 8f) continue;

                Turnaround turn = new Turnaround();
                turn.AxisX = inDir;
                turn.AxisY = axisY;
                turn.Origin = a + inDir * tip;
                turn.Gap = gap;
                turn.Side = side;

                // Le départ (centre de la voiture) 6,5 m avant le bout sur la voie d'arrivée ; la
                // reprise 10 m après, sur l'autre voie.
                float sIn = Project(turn.World(-6.5f, 0f), 0f, -1f);
                float sOut = Project(turn.World(-10f, gap), 0f, -1f);
                if (Ahead(sIn, sOut) > 60f) continue;
                turn.Start = sIn;
                turn.Rejoin = sOut;
                Turnarounds.Add(turn);
            }
        }

        // ------------------------------------------------------------------ carrefours

        private void BuildZones()
        {
            // Les points de la boucle qui passent à moins de 2,9 m d'un autre passage (pas les
            // voisins immédiats sur la boucle) : là, deux voitures se toucheraient.
            const float touch = 2.9f;
            const float apart = 25f;
            List<int> conflict = new List<int>();
            bool[] mark = new bool[_count];

            // Grille pour ne pas tout comparer à tout.
            Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < _count; i++)
            {
                long key = Cell(_points[i], 4f);
                List<int> list;
                if (!grid.TryGetValue(key, out list))
                {
                    list = new List<int>();
                    grid[key] = list;
                }

                list.Add(i);
            }

            for (int i = 0; i < _count; i++)
            {
                if (InTurnaround(i * Step)) continue;
                int cx = Mathf.FloorToInt(_points[i].x / 4f);
                int cz = Mathf.FloorToInt(_points[i].z / 4f);
                for (int dx = -1; dx <= 1 && !mark[i]; dx++)
                {
                    for (int dz = -1; dz <= 1 && !mark[i]; dz++)
                    {
                        List<int> list;
                        if (!grid.TryGetValue(Key(cx + dx, cz + dz), out list)) continue;
                        for (int k = 0; k < list.Count; k++)
                        {
                            int j = list[k];
                            float along = Mathf.Abs(i - j) * Step;
                            along = Mathf.Min(along, _length - along);
                            if (along < apart) continue;
                            if (InTurnaround(j * Step)) continue;
                            if (Flat(_points[i] - _points[j]).sqrMagnitude > touch * touch) continue;
                            mark[i] = true;
                            mark[j] = true;
                            break;
                        }
                    }
                }
            }

            for (int i = 0; i < _count; i++)
            {
                if (mark[i]) conflict.Add(i);
            }

            // Regroupement dans l'espace : un carrefour = les points en conflit à moins de 6 m les uns des autres.
            int[] group = new int[_count];
            for (int i = 0; i < _count; i++) group[i] = -1;
            int groups = 0;
            for (int c = 0; c < conflict.Count; c++)
            {
                int seed = conflict[c];
                if (group[seed] >= 0) continue;
                Queue<int> queue = new Queue<int>();
                queue.Enqueue(seed);
                group[seed] = groups;
                while (queue.Count > 0)
                {
                    int p = queue.Dequeue();
                    for (int d = 0; d < conflict.Count; d++)
                    {
                        int q = conflict[d];
                        if (group[q] >= 0) continue;
                        if (Flat(_points[p] - _points[q]).sqrMagnitude > 36f) continue;
                        group[q] = groups;
                        queue.Enqueue(q);
                    }
                }

                groups++;
            }

            List<Zone> zones = new List<Zone>();
            for (int g = 0; g < groups; g++)
            {
                Zone zone = new Zone();
                List<int> members = new List<int>();
                for (int i = 0; i < _count; i++)
                {
                    if (group[i] == g) members.Add(i);
                }

                AddIntervals(zone, members, 1.5f);
                zones.Add(zone);
            }

            // Les demi-tours sont des zones exclusives, elles aussi.
            for (int t = 0; t < Turnarounds.Count; t++)
            {
                Turnaround turn = Turnarounds[t];
                Zone zone = new Zone();
                zone.Turn = turn;
                // L'entrée un peu avant le point de départ : la suivante s'arrête derrière, pas dedans.
                zone.Intervals.Add(new Interval { In = Wrap(turn.Start - 3.5f), Out = turn.Rejoin });
                turn.Zone = zone;
                zones.Add(zone);
            }

            // Deux carrefours trop proches (une voiture arrêtée entre les deux en boucherait
            // un) n'en font qu'un.
            const float room = 10f;
            bool merged = true;
            while (merged)
            {
                merged = false;
                for (int a = 0; a < zones.Count && !merged; a++)
                {
                    for (int b = 0; b < zones.Count && !merged; b++)
                    {
                        if (a == b) continue;
                        if (!Close(zones[a], zones[b], room)) continue;
                        Absorb(zones[a], zones[b]);
                        zones.RemoveAt(b);
                        merged = true;
                    }
                }
            }

            for (int z = 0; z < zones.Count; z++)
            {
                Zone zone = zones[z];
                zone.Id = z;
                Vector3 sum = Vector3.zero;
                int n = 0;
                for (int k = 0; k < zone.Intervals.Count; k++)
                {
                    Interval iv = zone.Intervals[k];
                    for (float s = iv.In; Ahead(iv.In, s) <= Ahead(iv.In, iv.Out) && n < 4000; s += 2f)
                    {
                        sum = sum + Point(s);
                        n++;
                    }
                }

                zone.Center = n > 0 ? sum / n : Point(zone.Intervals[0].In);
                float radius = 0f;
                for (int k = 0; k < zone.Intervals.Count; k++)
                {
                    radius = Mathf.Max(radius, Flat(Point(zone.Intervals[k].In) - zone.Center).magnitude);
                    radius = Mathf.Max(radius, Flat(Point(zone.Intervals[k].Out) - zone.Center).magnitude);
                }

                zone.Radius = radius;
                Zones.Add(zone);
            }
        }

        private void AddIntervals(Zone zone, List<int> members, float margin)
        {
            members.Sort();
            int start = members[0];
            int last = members[0];
            for (int k = 1; k <= members.Count; k++)
            {
                bool end = k == members.Count || members[k] - last > 3;
                if (!end)
                {
                    last = members[k];
                    continue;
                }

                zone.Intervals.Add(new Interval { In = Wrap(start * Step - margin), Out = Wrap(last * Step + margin) });
                if (k < members.Count)
                {
                    start = members[k];
                    last = members[k];
                }
            }

            // La boucle est fermée : un passage coupé par le point de départ se recolle.
            if (zone.Intervals.Count > 1)
            {
                Interval first = zone.Intervals[0];
                Interval final = zone.Intervals[zone.Intervals.Count - 1];
                if (Ahead(final.Out, first.In) < 4f)
                {
                    zone.Intervals[0] = new Interval { In = final.In, Out = first.Out };
                    zone.Intervals.RemoveAt(zone.Intervals.Count - 1);
                }
            }
        }

        private bool Close(Zone a, Zone b, float room)
        {
            for (int i = 0; i < a.Intervals.Count; i++)
            {
                for (int j = 0; j < b.Intervals.Count; j++)
                {
                    Interval x = a.Intervals[i];
                    Interval y = b.Intervals[j];
                    if (Ahead(x.Out, y.In) < room || Ahead(y.Out, x.In) < room) return true;
                    if (Overlap(x, y)) return true;
                }
            }

            return false;
        }

        private bool Overlap(Interval x, Interval y)
        {
            return Ahead(x.In, y.In) <= Ahead(x.In, x.Out) || Ahead(y.In, x.In) <= Ahead(y.In, y.Out);
        }

        private void Absorb(Zone into, Zone from)
        {
            if (from.Turn != null && into.Turn == null)
            {
                into.Turn = from.Turn;
                from.Turn.Zone = into;
            }

            for (int i = 0; i < from.Intervals.Count; i++) into.Intervals.Add(from.Intervals[i]);

            // Recoller les passages qui se chevauchent ou se touchent.
            bool joined = true;
            while (joined)
            {
                joined = false;
                for (int i = 0; i < into.Intervals.Count && !joined; i++)
                {
                    for (int j = 0; j < into.Intervals.Count && !joined; j++)
                    {
                        if (i == j) continue;
                        Interval x = into.Intervals[i];
                        Interval y = into.Intervals[j];
                        if (!Overlap(x, y) && Ahead(x.Out, y.In) >= 10f) continue;
                        // y commence dans x (ou juste après) : l'union va de x.In au plus loin des deux sorties.
                        if (Ahead(x.In, y.In) > Ahead(x.In, x.Out) + 10f) continue;
                        float end = Ahead(x.In, y.Out) > Ahead(x.In, x.Out) ? y.Out : x.Out;
                        into.Intervals[i] = new Interval { In = x.In, Out = end };
                        into.Intervals.RemoveAt(j);
                        joined = true;
                    }
                }
            }

            if (into.Turn != null)
            {
                // Le demi-tour garde son entrée : la manœuvre démarre là.
                into.Turn.Zone = into;
            }
        }

        private bool InTurnaround(float s)
        {
            for (int t = 0; t < Turnarounds.Count; t++)
            {
                Turnaround turn = Turnarounds[t];
                if (Ahead(turn.Start - 4f, s) <= Ahead(turn.Start - 4f, turn.Rejoin)) return true;
            }

            return false;
        }

        /// <summary>Le passage d'une zone dans lequel se trouve (ou vers lequel va) l'abscisse <paramref name="s"/>.</summary>
        public static bool Inside(TrafficTrack track, Interval interval, float s)
        {
            return track.Ahead(interval.In, s) <= track.Ahead(interval.In, interval.Out);
        }

        // ------------------------------------------------------------------ profil de vitesse

        private void BuildSpeedProfile()
        {
            for (int i = 0; i < _count; i++)
            {
                float k = Mathf.Max(1e-4f, _curvature[i]);
                _limit[i] = Mathf.Min(Cruise, Mathf.Sqrt(LateralAcceleration / k));
            }

            // Prudence dans les carrefours.
            for (int z = 0; z < Zones.Count; z++)
            {
                Zone zone = Zones[z];
                for (int k = 0; k < zone.Intervals.Count; k++)
                {
                    Interval iv = zone.Intervals[k];
                    float span = Ahead(iv.In, iv.Out);
                    bool turnaround = zone.Turn != null && Ahead(iv.In, zone.Turn.Start) <= span;
                    for (float d = 0f; d <= span; d += Step)
                    {
                        int i = Mathf.FloorToInt(Wrap(iv.In + d) / Step) % _count;
                        _limit[i] = Mathf.Min(_limit[i], turnaround ? 1.5f : ZoneSpeed);
                    }
                }
            }

            // On arrive au pas au début d'un demi-tour.
            for (int t = 0; t < Turnarounds.Count; t++)
            {
                int i = Mathf.FloorToInt(Wrap(Turnarounds[t].Start) / Step) % _count;
                _limit[i] = Mathf.Min(_limit[i], 1.2f);
            }

            // Freinage anticipé (en remontant la boucle), puis accélération (en la descendant).
            for (int pass = 0; pass < 2; pass++)
            {
                for (int k = 2 * _count - 1; k >= 0; k--)
                {
                    int i = k % _count;
                    int next = (i + 1) % _count;
                    _limit[i] = Mathf.Min(_limit[i], Mathf.Sqrt(_limit[next] * _limit[next] + 2f * Deceleration * Step));
                }

                for (int k = 0; k < 2 * _count; k++)
                {
                    int i = k % _count;
                    int next = (i + 1) % _count;
                    _limit[next] = Mathf.Min(_limit[next], Mathf.Sqrt(_limit[i] * _limit[i] + 2f * Acceleration * Step));
                }
            }
        }

        // ------------------------------------------------------------------ outils

        private static long Cell(Vector3 p, float size)
        {
            return Key(Mathf.FloorToInt(p.x / size), Mathf.FloorToInt(p.z / size));
        }

        private static long Key(int x, int z)
        {
            return ((long)x << 32) ^ (uint)z;
        }

        public static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
