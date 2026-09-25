using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Le point que le coup va chercher : un point de la SURFACE de l'adversaire (menton,
    /// pommette, plexus, côtes), accroché à la zone touchable qui suit son corps.
    ///
    /// C'est ce qui manquait pour qu'un coup « touche » à l'écran. Les poses de coup étaient
    /// écrites dans le repère de l'attaquant, à 50 cm devant ses yeux : que l'adversaire soit à
    /// 40 cm ou à 90, plus grand ou penché, le poing finissait au même endroit — dans le vide,
    /// ou à travers la tête. Désormais le poing part de la pose prévue et se fait guider, au fil
    /// du geste, vers ce point précis ; l'impact a lieu quand les jointures arrivent sur la peau.
    /// </summary>
    public struct StrikeTarget
    {
        public Combatant Combatant;
        public Hurtbox Zone;
        public Transform Anchor;
        public Vector3 LocalPoint;
        public bool Valid;

        public Vector3 WorldPoint
        {
            get { return Anchor != null ? Anchor.TransformPoint(LocalPoint) : Vector3.zero; }
        }

        /// <summary>Distance maximale à laquelle on cherche une cible devant soi.</summary>
        public const float SearchRange = 2.7f;

        /// <summary>
        /// Trouve la cible d'un coup. D'abord la zone sous le réticule (on touche là où on vise),
        /// sinon l'adversaire le plus proche devant soi, à la tête — ou au corps pour un coup au
        /// corps (<paramref name="body"/>).
        /// </summary>
        public static StrikeTarget Acquire(Combatant attacker, Vector3 strikeFrom, bool body)
        {
            StrikeTarget target = new StrikeTarget();
            if (attacker == null) return target;

            Hurtbox aimed = AimResolver.Resolve(attacker);
            Combatant opponent = aimed != null ? OwnerCombatant(aimed) : null;

            if (opponent == null || !opponent.IsAlive) opponent = NearestInFront(attacker);
            if (opponent == null) return target;

            HitZone wanted = body ? HitZone.Body : HitZone.Head;
            if (aimed != null && OwnerCombatant(aimed) == opponent && aimed.Zone != HitZone.Leg) wanted = aimed.Zone;

            Hurtbox zone = FindZone(opponent, wanted);
            if (zone == null) zone = FindZone(opponent, HitZone.Body);
            if (zone == null) return target;

            Collider collider = zone.GetComponent<Collider>();
            if (collider == null) return target;

            Vector3 centre = collider.bounds.center;
            Vector3 surface;

            if (zone.Zone == HitZone.Head)
            {
                // La tête : on vise la MÂCHOIRE, pas le centre du crâne. Le point le plus proche
                // de l'épaule tombait sur l'arête du nez ; un vrai coup cherche le menton (de
                // face) ou l'angle de la mâchoire (de côté, pour un crochet).
                Vector3 toward = strikeFrom - centre;
                toward = Vector3.ProjectOnPlane(toward, zone.transform.up);
                if (toward.sqrMagnitude < 1e-6f) toward = zone.transform.forward;
                surface = centre - zone.transform.up * 0.12f + toward.normalized * 0.07f;
            }
            else
            {
                // Un coup au corps vise le plexus et les côtes, pas le haut de la poitrine : on
                // cherche le point de surface le plus proche d'un point plus bas que l'épaule.
                Vector3 from = body ? strikeFrom + Vector3.down * 0.28f : strikeFrom;
                surface = collider.ClosestPoint(from);

                // Enfoncé de quelques centimètres : le poing doit ENTRER en contact, pas
                // s'arrêter pile au bord de la zone.
                Vector3 inward = centre - surface;
                if (inward.sqrMagnitude > 1e-6f) surface += inward.normalized * 0.03f;
            }

            target.Combatant = opponent;
            target.Zone = zone;
            target.Anchor = zone.transform;
            target.LocalPoint = zone.transform.InverseTransformPoint(surface);
            target.Valid = true;

            // Adversaire en garde : le poing ne traverse pas ses bras pour aller au visage, il
            // s'écrase sur l'avant-bras qui couvre. On vise entre le poignet le plus proche de
            // la trajectoire et la surface visée.
            GuardSystem guard = opponent.GetComponent<GuardSystem>();
            if (guard != null && guard.IsGuarding && zone.Zone == HitZone.Head)
            {
                Transform wrist = GuardingWrist(opponent, strikeFrom, surface);
                if (wrist != null)
                {
                    Vector3 onGuard = Vector3.Lerp(surface, wrist.position, 0.55f);
                    target.Anchor = wrist;
                    target.LocalPoint = wrist.InverseTransformPoint(onGuard);
                }
            }

            return target;
        }

        /// <summary>Le poignet de l'adversaire le plus proche de la ligne épaule → cible.</summary>
        private static Transform GuardingWrist(Combatant opponent, Vector3 from, Vector3 to)
        {
            View.BodyRig rig = opponent.GetComponentInChildren<View.BodyRig>();
            if (rig == null) return null;

            Transform best = null;
            float bestDistance = float.MaxValue;
            Vector3 line = to - from;
            float lineLength = line.magnitude;
            if (lineLength < 1e-4f) return null;
            line /= lineLength;

            for (int i = 0; i < 2; i++)
            {
                View.HandRig hand = rig.Hand(i == 0 ? View.HandSide.Left : View.HandSide.Right);
                if (hand == null) continue;

                Vector3 p = hand.transform.position;
                float along = Mathf.Clamp(Vector3.Dot(p - from, line), 0f, lineLength);
                float distance = Vector3.Distance(p, from + line * along);
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = hand.transform;
            }

            return bestDistance < 0.25f ? best : null;
        }

        public static Combatant OwnerCombatant(Hurtbox hurtbox)
        {
            if (hurtbox == null || hurtbox.Health == null) return null;
            return hurtbox.Health.GetComponent<Combatant>();
        }

        public static Hurtbox FindZone(Combatant opponent, HitZone zone)
        {
            Hurtbox[] zones = opponent.GetComponentsInChildren<Hurtbox>();
            for (int i = 0; i < zones.Length; i++)
            {
                if (zones[i] != null && zones[i].isActiveAndEnabled && zones[i].Zone == zone) return zones[i];
            }

            return null;
        }

        private static Combatant NearestInFront(Combatant attacker)
        {
            Transform aim = attacker.AimOrigin;
            Vector3 forward = aim.forward;
            forward.y = 0f;
            if (forward.sqrMagnitude < 1e-4f) forward = attacker.transform.forward;
            forward.Normalize();

            Combatant best = null;
            float bestScore = float.MaxValue;

            var all = Combatant.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant other = all[i];
                if (other == null || other == attacker || !other.IsAlive) continue;
                if (other.Faction == attacker.Faction || other.Faction == Faction.Neutral) continue;

                Vector3 delta = other.transform.position - attacker.transform.position;
                delta.y = 0f;
                float distance = delta.magnitude;
                if (distance > SearchRange || distance < 0.01f) continue;

                float angle = Vector3.Angle(forward, delta);
                if (angle > 55f) continue;

                // Un peu d'angle coûte comme un peu de distance : on préfère celui qu'on regarde.
                float score = distance + angle * 0.012f;
                if (score >= bestScore) continue;

                bestScore = score;
                best = other;
            }

            return best;
        }
    }
}
