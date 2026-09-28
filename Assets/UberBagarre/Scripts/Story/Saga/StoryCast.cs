using System;
using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// La distribution de l'histoire et ses lieux : qui est qui (les <see cref="StoryActor"/>),
    /// et où sont les endroits qui comptent (le motel, la salle de Ray, le cabinet médical, les
    /// docks…). Le constructeur de la scène les déclare ; l'histoire s'en sert.
    /// </summary>
    public class StoryCast : MonoBehaviour
    {
        [Serializable]
        public class Place
        {
            public string id;
            public string label;
            public Vector3 position;
            public float yaw;
        }

        [SerializeField] private StoryActor[] _actors = new StoryActor[0];
        [SerializeField] private Place[] _places = new Place[0];
        [SerializeField] private Vector3[] _walkPoints = new Vector3[0];

        private WalkGraph _graph;

        /// <summary>Le joueur parle à quelqu'un de l'histoire.</summary>
        public event Action<StoryActor> Talked;

        private void Awake()
        {
            for (int i = 0; i < _actors.Length; i++)
            {
                if (_actors[i] == null) continue;
                _actors[i].Talked += OnTalked;
                _actors[i].gameObject.SetActive(false);
            }
        }

        private void OnTalked(StoryActor actor)
        {
            Action<StoryActor> handler = Talked;
            if (handler != null) handler(actor);
        }

        public StoryActor Actor(string id)
        {
            for (int i = 0; i < _actors.Length; i++)
            {
                if (_actors[i] != null && _actors[i].Id == id) return _actors[i];
            }

            return null;
        }

        public Place Where(string id)
        {
            for (int i = 0; i < _places.Length; i++)
            {
                if (_places[i] != null && _places[i].id == id) return _places[i];
            }

            return null;
        }

        /// <summary>Pose un acteur à un lieu (décalé de <paramref name="offset"/>, dans le repère du lieu).</summary>
        public StoryActor PlaceAt(string actor, string place, Vector3 offset, float turn = 0f)
        {
            StoryActor a = Actor(actor);
            Place p = Where(place);
            if (a == null || p == null) return a;
            Vector3 at = p.position + Quaternion.Euler(0f, p.yaw, 0f) * offset;
            a.Place(at, p.yaw + turn);
            return a;
        }

        public void Hide(string actor)
        {
            StoryActor a = Actor(actor);
            if (a != null) a.Hide();
        }

        public void HideAll()
        {
            for (int i = 0; i < _actors.Length; i++) if (_actors[i] != null) _actors[i].Hide();
        }

        /// <summary>Un chemin à pied d'un point à un autre, par les trottoirs.</summary>
        public Vector3[] PathBetween(Vector3 from, Vector3 to)
        {
            if (_graph == null) _graph = new WalkGraph(_walkPoints, 22f);
            return _graph.Find(from, to);
        }

        public void Configure(StoryActor[] actors, Place[] places, Vector3[] walkPoints)
        {
            _actors = actors ?? new StoryActor[0];
            _places = places ?? new Place[0];
            _walkPoints = walkPoints ?? new Vector3[0];
        }
    }

    /// <summary>
    /// Un graphe des trottoirs : les points des boucles de passants, reliés quand ils sont
    /// proches. Un A* y trouve un chemin plausible d'un endroit à un autre.
    /// </summary>
    public sealed class WalkGraph
    {
        private readonly Vector3[] _points;
        private readonly List<int>[] _links;

        public WalkGraph(Vector3[] points, float link)
        {
            _points = points ?? new Vector3[0];
            _links = new List<int>[_points.Length];
            float l2 = link * link;
            for (int i = 0; i < _points.Length; i++)
            {
                _links[i] = new List<int>();
                for (int j = 0; j < _points.Length; j++)
                {
                    if (i == j) continue;
                    Vector3 d = _points[j] - _points[i];
                    if (d.x * d.x + d.z * d.z <= l2 && Mathf.Abs(d.y) < 3f) _links[i].Add(j);
                }
            }
        }

        private int Nearest(Vector3 p)
        {
            int best = -1;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _points.Length; i++)
            {
                float d = (_points[i] - p).sqrMagnitude;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }

        public Vector3[] Find(Vector3 from, Vector3 to)
        {
            int start = Nearest(from);
            int goal = Nearest(to);
            if (start < 0 || goal < 0) return new[] { from, to };

            float[] g = new float[_points.Length];
            int[] came = new int[_points.Length];
            bool[] closed = new bool[_points.Length];
            for (int i = 0; i < g.Length; i++)
            {
                g[i] = float.MaxValue;
                came[i] = -1;
            }

            List<int> open = new List<int> { start };
            g[start] = 0f;
            while (open.Count > 0)
            {
                int current = open[0];
                float best = float.MaxValue;
                for (int i = 0; i < open.Count; i++)
                {
                    float f = g[open[i]] + Vector3.Distance(_points[open[i]], _points[goal]);
                    if (f < best)
                    {
                        best = f;
                        current = open[i];
                    }
                }

                if (current == goal) break;
                open.Remove(current);
                closed[current] = true;

                List<int> links = _links[current];
                for (int k = 0; k < links.Count; k++)
                {
                    int next = links[k];
                    if (closed[next]) continue;
                    float cost = g[current] + Vector3.Distance(_points[current], _points[next]);
                    if (cost >= g[next]) continue;
                    g[next] = cost;
                    came[next] = current;
                    if (!open.Contains(next)) open.Add(next);
                }
            }

            List<Vector3> path = new List<Vector3> { to };
            for (int at = goal; at >= 0; at = came[at])
            {
                path.Add(_points[at]);
                if (at == start) break;
            }

            path.Add(from);
            path.Reverse();
            return path.ToArray();
        }
    }
}
