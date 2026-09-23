using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Un personnage bouscule les objets physiques qu'il heurte.
    ///
    /// Le CharacterController d'Unity n'a pas de masse : il s'arrête contre un objet physique
    /// mais ne le pousse jamais. Sans ce composant, un adversaire projeté par un direct
    /// s'arrêterait net contre un plot de chantier comme contre un mur — ce qui trahit
    /// immédiatement que le plot est un décor et que la projection n'a pas de poids.
    ///
    /// La poussée tient compte de la masse de l'objet : un plot part à la vitesse du corps
    /// qui le percute, une caisse pleine à peine, une benne pas du tout. Et elle ne fait
    /// jamais aller l'objet plus vite que le personnage — sinon marcher contre une bouteille
    /// la ferait partir comme une balle.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [DisallowMultipleComponent]
    public class CharacterPusher : MonoBehaviour
    {
        [SerializeField, Min(0f)]
        [Tooltip("Fraction de la vitesse du personnage transmise par image de contact.")]
        private float _pushPower = 0.35f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Masse a laquelle la poussee est divisee par deux. Au-dela, l'objet resiste.")]
        private float _halfPushMass = 14f;

        [SerializeField, Min(0f)]
        [Tooltip("Vitesse en dessous de laquelle on ne pousse rien : rester immobile contre une " +
                 "caisse ne doit pas la faire glisser toute seule.")]
        private float _minSpeed = 0.25f;

        private CharacterController _controller;

        private void Awake()
        {
            _controller = GetComponent<CharacterController>();
        }

        private void OnControllerColliderHit(ControllerColliderHit hit)
        {
            Rigidbody body = hit.collider.attachedRigidbody;
            if (body == null || body.isKinematic) return;

            // Marcher SUR un objet ne doit pas l'enfoncer dans le sol.
            if (hit.moveDirection.y < -0.3f) return;

            Vector3 velocity = _controller != null ? _controller.velocity : Vector3.zero;
            velocity.y = 0f;

            float speed = velocity.magnitude;
            if (speed < _minSpeed) return;

            Vector3 direction = new Vector3(hit.moveDirection.x, 0f, hit.moveDirection.z);
            if (direction.sqrMagnitude < 0.0001f) return;
            direction.Normalize();

            float along = Vector3.Dot(body.linearVelocity, direction);
            if (along >= speed) return;

            float resistance = 1f + body.mass / _halfPushMass;
            Vector3 change = direction * Mathf.Min(speed - along, speed * _pushPower / resistance);

            body.AddForceAtPosition(change, hit.point, ForceMode.VelocityChange);
        }
    }
}
