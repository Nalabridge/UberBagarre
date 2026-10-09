using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le chef d'orchestre de la circulation : il prépare chaque boucle une seule fois (route
    /// lissée, carrefours, demi-tours — voir <see cref="TrafficTrack"/>), y inscrit les
    /// conducteurs, et fait tourner le régulateur (<see cref="TrafficFlow"/>) à chaque pas de
    /// physique, AVANT les conducteurs : ils lisent l'accélération qu'il leur a calculée.
    ///
    /// Il signale aussi au régulateur les carrefours où se trouve le joueur — à pied au milieu
    /// de la chaussée, ou au volant engagé dedans : on ne lui envoie pas une voiture dessus.
    /// </summary>
    [DefaultExecutionOrder(-30)]
    public class TrafficSystem : MonoBehaviour
    {
        private static TrafficSystem _instance;

        private readonly Dictionary<long, TrafficFlow> _flows = new Dictionary<long, TrafficFlow>();
        private readonly List<TrafficFlow> _list = new List<TrafficFlow>();
        private float _nextBlocks;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private static TrafficSystem Instance
        {
            get
            {
                if (_instance != null) return _instance;
                GameObject go = new GameObject("Circulation (regulateur)");
                _instance = go.AddComponent<TrafficSystem>();
                return _instance;
            }
        }

        /// <summary>Le régulateur de la boucle <paramref name="path"/> (préparée au premier appel).</summary>
        public static TrafficFlow Join(Vector3[] path, float cruise)
        {
            if (path == null || path.Length < 4) return null;
            TrafficSystem system = Instance;
            long key = Key(path);
            TrafficFlow flow;
            if (system._flows.TryGetValue(key, out flow)) return flow;

            float started = Time.realtimeSinceStartup;
            TrafficTrack track = new TrafficTrack(path, Mathf.Max(12f, cruise));
            flow = new TrafficFlow(track);
            system._flows[key] = flow;
            system._list.Add(flow);
            Debug.Log("[UberBagarre] Circulation : boucle de " + Mathf.RoundToInt(track.Length) + " m, " + track.Turnarounds.Count +
                      " demi-tours, " + track.Zones.Count + " zones (" + Mathf.RoundToInt((Time.realtimeSinceStartup - started) * 1000f) + " ms).");
            return flow;
        }

        /// <summary>La distance à la chaussée de la circulation la plus proche (8 m au plus ; 8 s'il n'y a pas de circulation).</summary>
        public static float RoadDistance(Vector3 position)
        {
            if (_instance == null) return 8f;
            float best = 8f;
            for (int f = 0; f < _instance._list.Count; f++) best = Mathf.Min(best, _instance._list[f].Track.DistanceToRoad(position));
            return best;
        }

        /// <summary>
        /// Une voiture arrive-t-elle sur <paramref name="point"/> dans les <paramref name="seconds"/>
        /// secondes (la circulation, ou celle du joueur) ? Ce que regarde un piéton avant de
        /// traverser.
        /// </summary>
        public static bool CarComing(Vector3 point, float seconds)
        {
            if (_instance != null)
            {
                for (int f = 0; f < _instance._list.Count; f++)
                {
                    TrafficFlow flow = _instance._list[f];
                    TrafficTrack track = flow.Track;
                    for (int a = 0; a < flow.Agents.Count; a++)
                    {
                        TrafficAgent agent = flow.Agents[a];
                        if (agent.Speed < 0.8f) continue;
                        Vector3 car = track.Point(agent.S);
                        Vector3 to = point - car;
                        to.y = 0f;
                        float distance = to.magnitude;
                        if (distance > 35f) continue;
                        Vector3 heading = track.Tangent(agent.S);
                        float along = to.x * heading.x + to.z * heading.z;
                        if (along < -2f) continue;
                        float lateral = Mathf.Abs(to.x * heading.z - to.z * heading.x);
                        // Sur sa trajectoire (ou dans le virage qui y mène), et là bientôt.
                        if (lateral > 6f && along < 8f) continue;
                        if (distance / Mathf.Max(1f, agent.Speed) < seconds) return true;
                    }
                }
            }

            DrivableCar driven = DrivableCar.Driven;
            if (driven != null && driven.Body != null)
            {
                Vector3 v = driven.Body.linearVelocity;
                v.y = 0f;
                float speed = v.magnitude;
                if (speed > 1.5f)
                {
                    Vector3 to = point - driven.transform.position;
                    to.y = 0f;
                    float along = Vector3.Dot(to, v / speed);
                    float lateral = (to - v / speed * along).magnitude;
                    if (along > -2f && lateral < 5f && along / speed < seconds) return true;
                }
            }

            return false;
        }

        private static long Key(Vector3[] path)
        {
            unchecked
            {
                long h = path.Length;
                for (int i = 0; i < path.Length; i += Mathf.Max(1, path.Length / 16))
                {
                    h = h * 31 + Mathf.RoundToInt(path[i].x * 10f);
                    h = h * 31 + Mathf.RoundToInt(path[i].z * 10f);
                }

                return h;
            }
        }

        private void FixedUpdate()
        {
            float dt = Time.fixedDeltaTime;
            bool blocks = Time.time >= _nextBlocks;
            if (blocks) _nextBlocks = Time.time + 0.25f;

            for (int f = 0; f < _list.Count; f++)
            {
                TrafficFlow flow = _list[f];
                if (blocks) UpdateBlocks(flow);
                flow.Step(dt);
            }
        }

        /// <summary>Les carrefours où le joueur est (à pied ou au volant) : personne n'y est envoyé.</summary>
        private static void UpdateBlocks(TrafficFlow flow)
        {
            Transform player = TrafficDriver.Player;
            DrivableCar driven = DrivableCar.Driven;
            List<TrafficTrack.Zone> zones = flow.Track.Zones;

            for (int z = 0; z < zones.Count; z++)
            {
                TrafficTrack.Zone zone = zones[z];
                bool blocked = false;

                if (driven != null)
                {
                    Vector3 p = driven.transform.position;
                    Vector3 v = driven.Body != null ? driven.Body.linearVelocity : Vector3.zero;
                    // Dedans, ou y arrive dans la seconde et demie.
                    blocked = Near(zone, p, 2.5f) || (v.sqrMagnitude > 4f && Near(zone, p + v * 1.5f, 2.5f));
                }
                else if (player != null)
                {
                    blocked = Near(zone, player.position, 0.5f);
                }

                flow.SetBlocked(zone.Id, blocked);
            }
        }

        private static bool Near(TrafficTrack.Zone zone, Vector3 p, float margin)
        {
            float dx = p.x - zone.Center.x;
            float dz = p.z - zone.Center.z;
            float r = zone.Radius + margin;
            return dx * dx + dz * dz < r * r;
        }
    }
}
