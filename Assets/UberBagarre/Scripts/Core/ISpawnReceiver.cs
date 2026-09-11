using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// Implémenté par les composants qui ont besoin de réagir à un (re)positionnement au spawn.
    /// Exemple : un CharacterController doit être désactivé pendant la téléportation,
    /// et PlayerLook doit réaligner son angle interne de visée, sinon il écrase la rotation
    /// appliquée par le spawn dès la frame suivante.
    ///
    /// Le SpawnDirector appelle cette méthode sur tous les composants de la hiérarchie :
    /// il n'a donc pas à connaître PlayerMotor ni PlayerLook.
    /// </summary>
    public interface ISpawnReceiver
    {
        void OnSpawned(Vector3 position, Quaternion rotation);
    }
}
