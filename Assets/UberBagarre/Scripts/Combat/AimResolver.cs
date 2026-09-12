using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Répond à une seule question : quelle zone du corps l'attaquant vise-t-il ?
    ///
    /// Un rayon part de son origine de visée — pour le joueur, la tête, qui porte le tangage de
    /// la caméra, donc exactement la direction du réticule — et renvoie la première zone
    /// touchable adverse rencontrée.
    ///
    /// Pourquoi ce besoin existe : en vue première personne, le poing part de la tête et reste
    /// haut, quoi qu'on fasse. Le bras mesure 56 cm, donc même en regardant ses pieds le poing
    /// ne descend pas sous 1,27 m pour un combattant debout — il ne pourra jamais atteindre
    /// physiquement une zone « jambes » qui s'arrête à 0,92 m. Décider la zone d'après la
    /// position du poing condamnait donc deux zones sur trois à être inatteignables.
    ///
    /// La règle devient « je touche là où je vise », qui est la seule qu'un joueur puisse
    /// apprendre, et la géométrie du poing ne sert plus qu'à savoir SI le coup porte.
    ///
    /// Le même code sert à l'affichage (le réticule annonce la zone visée) et au combat : il ne
    /// peut donc pas y avoir de désaccord entre ce que le joueur lit et ce qu'il obtient.
    /// </summary>
    public static class AimResolver
    {
        private static readonly RaycastHit[] _buffer = new RaycastHit[16];

        /// <summary>Portée de visée par défaut. Au-delà, viser une zone n'a plus de sens en corps à corps.</summary>
        public const float DefaultRange = 3.2f;

        public static Hurtbox Resolve(Transform aimOrigin, Faction attackerFaction, float maxDistance)
        {
            if (aimOrigin == null) return null;

            int count = Physics.RaycastNonAlloc(aimOrigin.position, aimOrigin.forward, _buffer,
                maxDistance, ~0, QueryTriggerInteraction.Collide);

            Hurtbox best = null;
            float bestDistance = float.MaxValue;

            // RaycastNonAlloc ne trie pas par distance : on le fait, sinon la zone retenue
            // dependrait de l'ordre de la physique, exactement le defaut qu'on cherche a eviter.
            for (int i = 0; i < count; i++)
            {
                Collider collider = _buffer[i].collider;
                if (collider == null) continue;

                Hurtbox hurtbox = collider.GetComponentInParent<Hurtbox>();
                if (hurtbox == null || hurtbox.Faction == attackerFaction) continue;
                if (_buffer[i].distance >= bestDistance) continue;

                bestDistance = _buffer[i].distance;
                best = hurtbox;
            }

            return best;
        }

        public static Hurtbox Resolve(Combatant attacker)
        {
            return attacker == null ? null : Resolve(attacker.AimOrigin, attacker.Faction, DefaultRange);
        }
    }
}
