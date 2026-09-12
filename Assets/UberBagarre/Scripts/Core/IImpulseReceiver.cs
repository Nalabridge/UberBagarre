using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// Ce qui peut recevoir une poussée : recul d'un coup, élan d'esquive, projection.
    ///
    /// Le joueur et l'ennemi ont des moteurs de déplacement différents, mais les systèmes de
    /// combat n'ont pas à le savoir : ils poussent une interface.
    /// </summary>
    public interface IImpulseReceiver
    {
        void ApplyImpulse(Vector3 velocity);
    }
}
