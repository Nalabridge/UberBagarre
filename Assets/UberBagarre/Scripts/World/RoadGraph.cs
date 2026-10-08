using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le graphe des rues pour l'itinéraire : les points des boucles de la circulation, reliés à
    /// leurs voisins de boucle, et entre boucles quand ils se touchent (les carrefours, les deux
    /// voies d'une même rue). Un A* y trouve le chemin par les rues.
    /// </summary>
    public sealed class RoadGraph
    {
        private readonly Vector2[] _points;
        private readonly List<int>[] _links;

        public RoadGraph(Vector3[] points, int[] loops)
        {
            int n = points != null ? points.Length : 0;
            _points = new Vector2[n];
            _links = new List<int>[n];
            for (int i = 0; i < n; i++)
            {
                _points[i] = new Vector2(points[i].x, points[i].z);
                _links[i] = new List<int>(4);
            }

            // Les voisins le long de chaque boucle (et la boucle se referme).
            for (int l = 0; loops != null && l < loops.Length; l++)
            {
                int start = loops[l];
                int end = l + 1 < loops.Length ? loops[l + 1] : n;
                for (int i = start; i < end; i++)
                {
                    int next = i + 1 < end ? i + 1 : start;
                    if (next == i) continue;
                    Link(i, next);
                }
            }

            // Les points proches (moins de 5 m) se rejoignent : carrefours et voies parallèles.
            const float cell = 5f;
            Dictionary<long, List<int>> grid = new Dictionary<long, List<int>>();
            for (int i = 0; i < n; i++)
            {
                long key = Key(Mathf.FloorToInt(_points[i].x / cell), Mathf.FloorToInt(_points[i].y / cell));
                List<int> bucket;
                if (!grid.TryGetValue(key, out bucket)) grid[key] = bucket = new List<int>();
                bucket.Add(i);
            }

            for (int i = 0; i < n; i++)
            {
                int cx = Mathf.FloorToInt(_points[i].x / cell);
                int cy = Mathf.FloorToInt(_points[i].y / cell);
                for (int dx = -1; dx <= 1; dx++)
                {
                    for (int dy = -1; dy <= 1; dy++)
                    {
                        List<int> bucket;
                        if (!grid.TryGetValue(Key(cx + dx, cy + dy), out bucket)) continue;
                        for (int k = 0; k < bucket.Count; k++)
                        {
                            int j = bucket[k];
                            if (j <= i) continue;
                            if ((_points[j] - _points[i]).sqrMagnitude < cell * cell) Link(i, j);
                        }
                    }
                }
            }
        }

        private static long Key(int x, int y)
        {
            return ((long)x << 32) ^ (uint)y;
        }

        private void Link(int a, int b)
        {
            if (!_links[a].Contains(b)) _links[a].Add(b);
            if (!_links[b].Contains(a)) _links[b].Add(a);
        }

        private int Nearest(Vector2 p)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _points.Length; i++)
            {
                float d = (_points[i] - p).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = i;
            }

            return best;
        }

        /// <summary>Le chemin par les rues de <paramref name="from"/> à <paramref name="to"/>, dans <paramref name="route"/>.</summary>
        public void Find(Vector2 from, Vector2 to, List<Vector2> route)
        {
            route.Clear();
            int start = Nearest(from);
            int goal = Nearest(to);
            if (start < 0 || goal < 0)
            {
                route.Add(from);
                route.Add(to);
                return;
            }

            int n = _points.Length;
            float[] g = new float[n];
            int[] came = new int[n];
            bool[] closed = new bool[n];
            for (int i = 0; i < n; i++)
            {
                g[i] = float.MaxValue;
                came[i] = -1;
            }

            // Une file de priorité simple (tas binaire) : le graphe compte quelques milliers de points.
            List<KeyValuePair<float, int>> open = new List<KeyValuePair<float, int>>();
            g[start] = 0f;
            Push(open, Vector2.Distance(_points[start], _points[goal]), start);
            bool found = false;
            while (open.Count > 0)
            {
                int current = Pop(open);
                if (closed[current]) continue;
                if (current == goal)
                {
                    found = true;
                    break;
                }

                closed[current] = true;
                List<int> links = _links[current];
                for (int k = 0; k < links.Count; k++)
                {
                    int next = links[k];
                    if (closed[next]) continue;
                    float cost = g[current] + Vector2.Distance(_points[current], _points[next]);
                    if (cost >= g[next]) continue;
                    g[next] = cost;
                    came[next] = current;
                    Push(open, cost + Vector2.Distance(_points[next], _points[goal]), next);
                }
            }

            route.Add(to);
            if (found)
            {
                for (int at = goal; at >= 0; at = came[at])
                {
                    route.Add(_points[at]);
                    if (at == start) break;
                }
            }

            route.Add(from);
            route.Reverse();
        }

        private static void Push(List<KeyValuePair<float, int>> heap, float priority, int item)
        {
            heap.Add(new KeyValuePair<float, int>(priority, item));
            int i = heap.Count - 1;
            while (i > 0)
            {
                int parent = (i - 1) / 2;
                if (heap[parent].Key <= heap[i].Key) break;
                KeyValuePair<float, int> t = heap[parent];
                heap[parent] = heap[i];
                heap[i] = t;
                i = parent;
            }
        }

        private static int Pop(List<KeyValuePair<float, int>> heap)
        {
            int result = heap[0].Value;
            int last = heap.Count - 1;
            heap[0] = heap[last];
            heap.RemoveAt(last);
            int i = 0;
            while (true)
            {
                int l = i * 2 + 1;
                int r = l + 1;
                int smallest = i;
                if (l < heap.Count && heap[l].Key < heap[smallest].Key) smallest = l;
                if (r < heap.Count && heap[r].Key < heap[smallest].Key) smallest = r;
                if (smallest == i) break;
                KeyValuePair<float, int> t = heap[smallest];
                heap[smallest] = heap[i];
                heap[i] = t;
                i = smallest;
            }

            return result;
        }
    }
}
