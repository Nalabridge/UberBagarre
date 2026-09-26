using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Un mouvement de main venu d'ailleurs que des poses écrites : les coups capturés (voir
    /// <see cref="MocapArms"/>). L'exécuteur garde tout le reste — cible, guidage, élan, gel de
    /// contact, caméra — et ne lui demande que la trajectoire et l'orientation du poing.
    ///
    /// Les poses sont dans l'espace des bras (celui de <see cref="FirstPersonHands"/>), et partent
    /// de la garde du moment : le geste commence et finit là où sont les mains.
    /// </summary>
    public interface IHandMotion
    {
        /// <summary>
        /// Prépare le geste d'un coup. Faux s'il n'en a pas : l'exécuteur garde alors ses poses.
        /// <paramref name="designImpact"/> est l'impact des poses écrites : l'amplitude du geste
        /// capturé s'y cale.
        /// </summary>
        bool Begin(AttackData attack, HandSide side, bool bodyShot, HandPose guard, Vector3 designImpact,
            float impactTime);

        /// <summary>Pose du poing qui frappe et de l'autre main, au temps normalisé du coup.</summary>
        void Sample(float normalized, HandPose strikeGuard, HandPose offGuard, out HandPose strike, out HandPose off);

        /// <summary>Où le geste met le poignet à l'impact : l'écart avec la cible est ce que le guidage comble.</summary>
        Vector3 ImpactPosition(HandPose strikeGuard);
    }
}
