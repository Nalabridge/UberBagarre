using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>Une voiture de la circulation, vue par le régulateur : où elle en est sur sa boucle.</summary>
    public sealed class TrafficAgent
    {
        public int Id;

        /// <summary>L'abscisse du centre de la voiture sur la boucle.</summary>
        public float S;

        /// <summary>Sa vitesse le long de la route (m/s).</summary>
        public float Speed;

        public float HalfLength = 2.3f;

        /// <summary>Loin du joueur : le régulateur la fait avancer lui-même, sans physique.</summary>
        public bool Asleep;

        /// <summary>Le demi-tour en cours (-1 : aucun). Pendant la manœuvre, l'abscisse reste au début du demi-tour.</summary>
        public int Turning = -1;

        /// <summary>Endormie : le temps qu'il reste à la manœuvre (simulée).</summary>
        public float TurnTimer;

        /// <summary>Vitesse de croisière propre (chaque conducteur a son pied).</summary>
        public float Cruise = 11f;

        /// <summary>Un obstacle hors circulation devant (le joueur, un passant, une voiture garée) : la distance au pare-chocs, sa vitesse.</summary>
        public float ObstacleGap = float.MaxValue;
        public float ObstacleSpeed;

        /// <summary>Accélération voulue, calculée par le régulateur.</summary>
        public float Desired;

        /// <summary>Pourquoi elle s'arrête (pour le débogage et le klaxon).</summary>
        public string Reason = "";

        /// <summary>Depuis combien de temps elle ne roule plus alors qu'elle le voudrait.</summary>
        public float StoppedFor;

        public object Owner;

        public readonly List<TrafficTrack.Zone> Held = new List<TrafficTrack.Zone>(2);
    }

    /// <summary>
    /// Le régulateur d'une boucle de circulation : qui suit qui, qui passe au carrefour, qui
    /// fait son demi-tour.
    ///
    /// - **Suivre** : le modèle du conducteur intelligent (IDM, Treiber 2000), celui des
    ///   simulateurs de trafic — garder un temps d'avance (1,3 s), freiner tôt et doucement,
    ///   repartir sans à-coups. Les voitures sont rangées le long de la boucle : chacune connaît
    ///   exactement celle de devant, même dans un virage ou derrière un immeuble.
    /// - **Carrefours** : chaque zone où la boucle se croise est réservée par une seule voiture
    ///   à la fois, dans l'ordre d'arrivée (comme un stop à quatre voies). On n'y entre pas si
    ///   la sortie est bouchée (on ne bloque pas le carrefour).
    /// - **Demi-tours** : une seule voiture à la fois dans l'impasse ; les suivantes attendent.
    /// </summary>
    public sealed class TrafficFlow
    {
        // Le modèle IDM : accélération, freinage confortable, temps d'avance, distance à l'arrêt.
        public const float IdmAcceleration = 1.5f;
        public const float IdmBraking = 2.2f;
        public const float Headway = 1.3f;
        public const float Standstill = 2.4f;
        public const float StopLine = 1.0f;

        private sealed class ZoneState
        {
            public TrafficAgent Owner;
            public float OwnedFor;
            public readonly List<TrafficAgent> Queue = new List<TrafficAgent>();

            /// <summary>Quelque chose hors circulation est dedans (le joueur) : on n'y envoie personne.</summary>
            public bool Blocked;
        }

        public readonly TrafficTrack Track;
        public readonly List<TrafficAgent> Agents = new List<TrafficAgent>();
        private readonly ZoneState[] _zones;
        private readonly List<TrafficAgent> _sorted = new List<TrafficAgent>();

        public TrafficFlow(TrafficTrack track)
        {
            Track = track;
            _zones = new ZoneState[track.Zones.Count];
            for (int i = 0; i < _zones.Length; i++) _zones[i] = new ZoneState();
        }

        public void Add(TrafficAgent agent)
        {
            if (!Agents.Contains(agent)) Agents.Add(agent);
        }

        public void Remove(TrafficAgent agent)
        {
            Agents.Remove(agent);
            for (int z = 0; z < _zones.Length; z++)
            {
                ZoneState state = _zones[z];
                state.Queue.Remove(agent);
                if (state.Owner == agent) state.Owner = null;
            }

            agent.Held.Clear();
        }

        /// <summary>Marque une zone comme occupée par le joueur (ou la libère).</summary>
        public void SetBlocked(int zone, bool blocked)
        {
            if (zone >= 0 && zone < _zones.Length) _zones[zone].Blocked = blocked;
        }

        public TrafficAgent OwnerOf(int zone)
        {
            return zone >= 0 && zone < _zones.Length ? _zones[zone].Owner : null;
        }

        // ================================================================== pas de temps

        /// <summary>
        /// Calcule l'accélération voulue de chaque voiture (champ <see cref="TrafficAgent.Desired"/>),
        /// tient les réservations des carrefours, et fait avancer celles qui dorment.
        /// </summary>
        public void Step(float dt)
        {
            _sorted.Clear();
            _sorted.AddRange(Agents);
            _sorted.Sort((a, b) => a.S.CompareTo(b.S));

            for (int z = 0; z < _zones.Length; z++)
            {
                if (_zones[z].Owner != null) _zones[z].OwnedFor += dt;
                else _zones[z].OwnedFor = 0f;
            }

            for (int i = 0; i < _sorted.Count; i++) Plan(_sorted[i], i, dt);

            for (int i = 0; i < _sorted.Count; i++)
            {
                TrafficAgent agent = _sorted[i];
                if (!agent.Asleep) continue;
                Glide(agent, dt);
            }
        }

        private void Plan(TrafficAgent agent, int index, float dt)
        {
            TrafficTrack track = Track;
            float v = Mathf.Max(0f, agent.Speed);
            float front = agent.S + agent.HalfLength;

            // --- la route : vitesse permise un peu devant (le profil anticipe déjà les virages).
            float v0 = Mathf.Min(agent.Cruise, track.Limit(agent.S));
            float probe = Mathf.Min(agent.Cruise, track.Limit(front + v * 0.8f));
            v0 = Mathf.Max(0.5f, Mathf.Min(v0, probe));

            float accel = IdmAcceleration * (1f - Pow4(v / v0));
            agent.Reason = "";

            // --- la voiture de devant sur la boucle
            TrafficAgent leader = _sorted.Count > 1 ? _sorted[(index + 1) % _sorted.Count] : null;
            if (leader != null && leader != agent)
            {
                float gap = track.Ahead(agent.S, leader.S) - agent.HalfLength - leader.HalfLength;
                if (gap < 120f)
                {
                    float a = Follow(v, v0, gap, v - Mathf.Max(0f, leader.Speed), Standstill);
                    if (a < accel)
                    {
                        accel = a;
                        agent.Reason = "suit";
                    }
                }
            }

            // --- un obstacle hors circulation (vu par les capteurs de la voiture)
            if (agent.ObstacleGap < 60f)
            {
                float a = Follow(v, v0, agent.ObstacleGap, v - Mathf.Max(0f, agent.ObstacleSpeed), 2f);
                if (a < accel)
                {
                    accel = a;
                    agent.Reason = "obstacle";
                }
            }

            // --- carrefours et demi-tours : réserver, sinon s'arrêter à la ligne
            float horizon = v * v / (2f * IdmBraking) + 14f;
            UpdateZones(agent, front, horizon, ref accel);

            agent.Desired = Mathf.Clamp(accel, -7f, IdmAcceleration);
            agent.StoppedFor = v < 0.3f && agent.Turning < 0 ? agent.StoppedFor + dt : 0f;
        }

        /// <summary>L'accélération IDM derrière quelque chose à <paramref name="gap"/> mètres, qui va moins vite de <paramref name="closing"/>.</summary>
        public static float Follow(float v, float v0, float gap, float closing, float standstill)
        {
            float free = 1f - Pow4(v / Mathf.Max(0.5f, v0));
            float wanted = standstill + Mathf.Max(0f, v * Headway + v * closing / (2f * Mathf.Sqrt(IdmAcceleration * IdmBraking)));
            float ratio = wanted / Mathf.Max(0.1f, gap);
            float a = IdmAcceleration * (free - ratio * ratio);
            if (gap <= 0.2f) a = -7f;
            return a;
        }

        private static float Pow4(float x)
        {
            float x2 = x * x;
            return x2 * x2;
        }

        private void UpdateZones(TrafficAgent agent, float front, float horizon, ref float accel)
        {
            TrafficTrack track = Track;
            float rear = agent.S - agent.HalfLength;

            // Libérer ce qui est derrière nous.
            for (int h = agent.Held.Count - 1; h >= 0; h--)
            {
                TrafficTrack.Zone zone = agent.Held[h];
                if (agent.Turning >= 0 && zone.Turn != null && zone.Turn == track.Turnarounds[agent.Turning]) continue;
                if (StillIn(zone, agent, rear, front)) continue;
                Release(zone, agent);
            }

            for (int z = 0; z < track.Zones.Count; z++)
            {
                TrafficTrack.Zone zone = track.Zones[z];
                ZoneState state = _zones[zone.Id];
                bool concerned = false;

                for (int k = 0; k < zone.Intervals.Count; k++)
                {
                    TrafficTrack.Interval interval = zone.Intervals[k];
                    float toLine = track.Ahead(front, interval.In);
                    bool approaching = toLine < horizon;
                    // Déjà engagée (le nez dans la zone) : elle la prend, elle ne s'arrête pas au milieu.
                    bool inside = TrafficTrack.Inside(track, interval, front) && track.Ahead(interval.In, front) < 25f;
                    if (!approaching && !inside) continue;
                    concerned = true;

                    if (state.Owner == agent) continue;

                    if (!state.Queue.Contains(agent)) state.Queue.Add(agent);

                    bool free = state.Owner == null && !state.Blocked && state.Queue[0] == agent && ExitClear(agent, interval);
                    if (inside && state.Owner == null) free = true;

                    if (free)
                    {
                        state.Owner = agent;
                        state.OwnedFor = 0f;
                        state.Queue.Remove(agent);
                        if (!agent.Held.Contains(zone)) agent.Held.Add(zone);
                        continue;
                    }

                    if (inside) continue;

                    // Pas notre tour : on s'arrête à la ligne.
                    float gap = toLine - StopLine;
                    float a = Follow(Mathf.Max(0f, agent.Speed), Mathf.Max(1f, agent.Cruise), gap, Mathf.Max(0f, agent.Speed), 0.4f);
                    if (a < accel)
                    {
                        accel = a;
                        agent.Reason = zone.Turn != null ? "attend le demi-tour" : "cède le passage";
                    }
                }

                // Plus concernée (partie ailleurs, replacée) : elle ne garde pas sa place dans la file.
                if (!concerned && state.Owner != agent) state.Queue.Remove(agent);
            }

            // Une réservation qui dure trop (une voiture coincée dans le carrefour) finit par sauter.
            for (int z = 0; z < _zones.Length; z++)
            {
                ZoneState state = _zones[z];
                if (state.Owner == agent && state.OwnedFor > 40f && agent.Turning < 0)
                {
                    Release(track.Zones[z], agent);
                }
            }
        }

        private bool StillIn(TrafficTrack.Zone zone, TrafficAgent agent, float rear, float front)
        {
            TrafficTrack track = Track;
            for (int k = 0; k < zone.Intervals.Count; k++)
            {
                TrafficTrack.Interval interval = zone.Intervals[k];
                float span = track.Ahead(interval.In, interval.Out);
                // Positions relatives à l'entrée, dans (-L/2, L/2] : négatives avant la zone.
                float frontRel = Relative(interval.In, front);
                float rearRel = Relative(interval.In, rear);
                if (frontRel >= -30f && rearRel <= span) return true;
            }

            return false;
        }

        private float Relative(float origin, float s)
        {
            float d = Track.Ahead(origin, s);
            return d > Track.Length * 0.5f ? d - Track.Length : d;
        }

        /// <summary>La place, après la zone, pour en sortir entièrement (on ne bloque pas un carrefour).</summary>
        private bool ExitClear(TrafficAgent agent, TrafficTrack.Interval interval)
        {
            TrafficTrack track = Track;
            int index = _sorted.IndexOf(agent);
            if (index < 0 || _sorted.Count < 2) return true;
            TrafficAgent leader = _sorted[(index + 1) % _sorted.Count];
            if (leader == agent) return true;
            if (leader.Speed > 3f) return true;
            float leaderRear = leader.S - leader.HalfLength;
            float through = track.Ahead(agent.S, interval.Out);
            float toLeader = track.Ahead(agent.S, leaderRear);
            if (toLeader < through) return leader.Speed > 1f;
            return toLeader - through > agent.HalfLength * 2f + 2f;
        }

        private void Release(TrafficTrack.Zone zone, TrafficAgent agent)
        {
            ZoneState state = _zones[zone.Id];
            if (state.Owner == agent) state.Owner = null;
            state.Queue.Remove(agent);
            agent.Held.Remove(zone);
        }

        /// <summary>Demi-tour : la voiture peut-elle commencer (elle tient la zone, elle est arrivée) ?</summary>
        public int TurnaroundAt(TrafficAgent agent)
        {
            TrafficTrack track = Track;
            for (int t = 0; t < track.Turnarounds.Count; t++)
            {
                TrafficTrack.Turnaround turn = track.Turnarounds[t];
                float to = track.Ahead(agent.S, turn.Start);
                float past = track.Ahead(turn.Start, agent.S);
                if ((to < 0.6f || past < 3f) && agent.Held.Contains(turn.Zone)) return t;
            }

            return -1;
        }

        /// <summary>Le demi-tour est fini : la voiture reprend la boucle sur la voie de retour.</summary>
        public void FinishTurn(TrafficAgent agent, float s)
        {
            agent.Turning = -1;
            agent.TurnTimer = 0f;
            agent.S = Track.Wrap(s);
        }

        // ================================================================== loin du joueur

        private void Glide(TrafficAgent agent, float dt)
        {
            if (agent.Turning >= 0)
            {
                agent.Speed = 0f;
                agent.TurnTimer -= dt;
                if (agent.TurnTimer <= 0f) FinishTurn(agent, Track.Turnarounds[agent.Turning].Rejoin);
                return;
            }

            float v = Mathf.Max(0f, agent.Speed + agent.Desired * dt);
            agent.S = Track.Wrap(agent.S + (agent.Speed + v) * 0.5f * dt);
            agent.Speed = v;

            int turn = TurnaroundAt(agent);
            if (turn >= 0 && v < 2f)
            {
                agent.Turning = turn;
                agent.TurnTimer = 9f;
                agent.S = Track.Turnarounds[turn].Start;
                agent.Speed = 0f;
            }
        }
    }

    /// <summary>
    /// Le volant : le contrôleur de Stanley (celui de la voiture autonome de Stanford, 2005).
    /// Il regarde l'essieu avant, pas un point lointain : il corrige l'écart à la route et
    /// l'angle du capot, plus la courbure de la route qui arrive. Contrairement à la
    /// « poursuite pure » d'avant, il ne coupe pas les virages — couper un virage serré, dans
    /// une rue à deux voies, c'est mordre sur la voie d'en face.
    /// </summary>
    public static class TrafficSteering
    {
        public static float Gain = 4f;
        public static float Softening = 1f;
        public static float FeedForward = 0f;
        public static float Preview = 0f;

        /// <summary>
        /// L'angle de braquage voulu, en degrés (positif à droite, comme le volant de Unity).
        /// <paramref name="center"/>/<paramref name="forward"/> : la voiture ; <paramref name="s"/>
        /// : son abscisse ; <paramref name="offset"/> : un décalage latéral voulu (+ à droite, pour
        /// contourner un obstacle).
        /// </summary>
        public static float Angle(TrafficTrack track, float s, Vector3 center, Vector3 forward, float speed, float wheelbase, float offset)
        {
            Vector3 front = center + forward * (wheelbase * 0.5f);
            float sf = track.Project(front, s + wheelbase * 0.5f, 6f);
            Vector3 tangent = track.Tangent(sf);

            float error = track.Lateral(front, sf) - offset;
            float headingError = Mathf.Atan2(forward.z * tangent.x - forward.x * tangent.z, forward.x * tangent.x + forward.z * tangent.z);
            float lookahead = Mathf.Max(0f, speed) * Preview;
            float feedForward = FeedForward * Mathf.Atan(wheelbase * track.SignedCurvature(sf + lookahead));
            float correction = -Mathf.Atan2(Gain * error, Mathf.Abs(speed) + Softening);

            return (headingError + feedForward + correction) * Mathf.Rad2Deg;
        }
    }

    /// <summary>
    /// Le demi-tour en trois manœuvres, comme au permis : en avant en braquant à fond vers la
    /// voie de retour jusqu'au bord, en arrière en braquant de l'autre côté, puis en avant
    /// pour repartir. Autant d'allers-retours qu'il faut si la rue est étroite.
    ///
    /// Les bords sont ceux de la chaussée supposée (deux mètres au-delà de chaque voie, le bout
    /// de l'impasse) ; la voiture s'arrête aussi plus tôt si ses capteurs voient un mur, un
    /// poteau ou quelqu'un.
    /// </summary>
    public sealed class TurnaroundManeuver
    {
        public enum Phase
        {
            Forward,
            Reverse,
            Pause,
            Done
        }

        public Phase Current = Phase.Forward;
        private Phase _after;
        private float _pause;
        public int Swings;

        // La voiture : du centre aux pare-chocs, demi-largeur.
        public float Front = 2.3f;
        public float Rear = 2.3f;
        public float HalfWidth = 0.95f;

        public const float Edge = 2.0f;
        public const float EndRoom = 1.8f;

        public void Reset()
        {
            Current = Phase.Forward;
            Swings = 0;
            _pause = 0f;
        }

        /// <summary>
        /// Un pas de manœuvre. <paramref name="x"/>, <paramref name="y"/> : le centre de la
        /// voiture dans le repère du demi-tour ; <paramref name="heading"/> : son cap (radians,
        /// 0 = vers le bout, π = repartie). Rend la vitesse voulue (négative = marche arrière)
        /// et le braquage (-1..1, dans le sens du repère : + vers la voie de retour en avançant).
        /// </summary>
        public void Step(TrafficTrack.Turnaround turn, float x, float y, float heading, float speed, float dt,
            bool blockedAhead, bool blockedBehind, out float targetSpeed, out float steer)
        {
            targetSpeed = 0f;
            steer = 0f;

            switch (Current)
            {
                case Phase.Pause:
                    _pause -= dt;
                    steer = _after == Phase.Reverse ? -1f : 1f;
                    if (_pause <= 0f && Mathf.Abs(speed) < 0.15f) Current = _after;
                    return;

                case Phase.Forward:
                    steer = 1f;
                    targetSpeed = 1.6f;
                    if (heading > 160f * Mathf.Deg2Rad)
                    {
                        Current = Phase.Done;
                        return;
                    }

                    if (blockedAhead || !Fits(turn, x, y, heading, 0.45f, 1f))
                    {
                        Swings++;
                        _after = Phase.Reverse;
                        _pause = 0.35f;
                        Current = Phase.Pause;
                        targetSpeed = 0f;
                    }

                    return;

                case Phase.Reverse:
                    steer = -1f;
                    targetSpeed = -1.3f;
                    // Assez tourné pour repartir en avant vers la voie de retour.
                    bool enough = heading > 150f * Mathf.Deg2Rad;
                    if (enough || blockedBehind || !Fits(turn, x, y, heading, 0.45f, -1f))
                    {
                        Swings++;
                        _after = Phase.Forward;
                        _pause = 0.35f;
                        Current = Phase.Pause;
                        targetSpeed = 0f;
                    }

                    return;
            }
        }

        /// <summary>
        /// La voiture tient-elle encore dans la chaussée si elle avance (ou recule) de
        /// <paramref name="step"/> mètres en braquant à fond ?
        /// </summary>
        public bool Fits(TrafficTrack.Turnaround turn, float x, float y, float heading, float step, float direction)
        {
            // Rayon de braquage d'une voiture de ville (au centre) : environ 4,5 m.
            const float radius = 4.5f;
            // En avant (braqué vers la voie de retour) comme en arrière (braqué de l'autre côté),
            // le cap tourne dans le même sens : c'est tout le principe du demi-tour.
            float h = heading + step / radius;
            float cx = x + Mathf.Cos(heading) * step * direction;
            float cy = y + Mathf.Sin(heading) * step * direction;

            float c = Mathf.Cos(h), s = Mathf.Sin(h);
            float xmax = EndRoom;
            float ymin = -Edge;
            float ymax = turn.Gap + Edge;

            for (int i = 0; i < 4; i++)
            {
                float fx = i < 2 ? Front : -Rear;
                float fy = i % 2 == 0 ? HalfWidth : -HalfWidth;
                float px = cx + c * fx - s * fy;
                float py = cy + s * fx + c * fy;
                // On ne vérifie que le côté de la voiture qui avance.
                bool leading = direction > 0f ? i < 2 : i >= 2;
                if (!leading) continue;
                if (px > xmax || py < ymin || py > ymax) return false;
            }

            return true;
        }
    }
}
