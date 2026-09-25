using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Nœud de recul de caméra, entre les secousses et la caméra elle-même.
    ///
    /// Deux entrées distinctes, volontairement :
    /// - « pilotée » : suit l'animation du coup en cours, et revient à zéro avec elle ;
    /// - « impulsion » : un à-coup ponctuel à l'impact, qui s'amortit tout seul.
    ///
    /// Les deux passent par un RESSORT à amortissement critique, jamais par une valeur posée
    /// d'un coup. C'était la cause des « coupures » en combat : chaque coup porté ajoutait
    /// plusieurs degrés à l'angle de la caméra en UNE image, puis les reprenait en décroissant.
    /// Une vue qui change de 3° entre deux images ne se lit pas comme un choc mais comme un
    /// saut d'image. Avec le ressort, l'impulsion devient une VITESSE : la vue part en ~50 ms,
    /// culmine, et revient sans jamais dépasser — le choc se sent, rien ne saute.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class CameraPunch : MonoBehaviour
    {
        [SerializeField, Min(1f)]
        [Tooltip("Raideur du ressort (rad/s). Plus haut = plus sec. Le pic d'un choc arrive en 1/raideur s.")]
        private float _stiffness = 19f;

        [SerializeField] private bool _enabled = true;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du mouvement de camera des coups (elan, recul) reellement applique. A 1, la " +
                 "vue suivait chaque coup de poing et le combat donnait le mal de mer.")]
        private float _drivenScale = 0.35f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part des chocs (coup recu, coup porte) appliquee a la camera.")]
        private float _impulseScale = 0.45f;

        [SerializeField, Min(0f)]
        [Tooltip("Angle maximal que la camera peut prendre, en degres, quoi qu'il arrive.")]
        private float _maxAngle = 4f;

        [SerializeField, Min(0f)]
        [Tooltip("Deplacement maximal de la camera, en metres.")]
        private float _maxOffset = 0.035f;

        private Vector3 _drivenPosition;
        private Vector3 _drivenEuler;

        private Vector3 _position;
        private Vector3 _positionVelocity;
        private Vector3 _euler;
        private Vector3 _eulerVelocity;

        /// <summary>Suivi continu de l'animation d'attaque. À appeler chaque frame pendant le coup.</summary>
        public void SetDriven(Vector3 position, Vector3 euler)
        {
            _drivenPosition = position * _drivenScale;
            _drivenEuler = euler * _drivenScale;
        }

        /// <summary>
        /// À-coup ponctuel, au contact. Les valeurs sont les PICS voulus (mètres, degrés) : elles
        /// sont converties en vitesse initiale du ressort, pour que la vue y monte en douceur.
        /// </summary>
        public void AddImpulse(Vector3 position, Vector3 euler)
        {
            // Ressort critique lancé à la vitesse v : x(t) = v·t·e^(-ωt), dont le pic vaut v/(e·ω).
            float toVelocity = 2.71828f * _stiffness * _impulseScale;
            _positionVelocity += position * toVelocity;
            _eulerVelocity += euler * toVelocity;
        }

        /// <summary>Remet la caméra au neutre (cinématique, changement de lieu).</summary>
        public void ResetState()
        {
            _drivenPosition = Vector3.zero;
            _drivenEuler = Vector3.zero;
            _position = Vector3.zero;
            _positionVelocity = Vector3.zero;
            _euler = Vector3.zero;
            _eulerVelocity = Vector3.zero;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
        }

        private void LateUpdate()
        {
            // Temps non mis à l'échelle : le ralenti d'impact ne doit pas figer la caméra en
            // pleine course, sinon il la ferait « tenir » un angle pendant quelques images.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (!_enabled)
            {
                ResetState();
                return;
            }

            Step(ref _position, ref _positionVelocity, _drivenPosition, dt);
            Step(ref _euler, ref _eulerVelocity, _drivenEuler, dt);

            _position = Vector3.ClampMagnitude(_position, _maxOffset);
            _euler = Vector3.ClampMagnitude(_euler, _maxAngle);

            transform.localPosition = _position;
            transform.localRotation = Quaternion.Euler(_euler);
        }

        /// <summary>
        /// Pas exact d'un ressort à amortissement critique vers <paramref name="target"/> :
        /// stable quel que soit le pas de temps, donc aussi à 20 images par seconde.
        /// </summary>
        private void Step(ref Vector3 value, ref Vector3 velocity, Vector3 target, float dt)
        {
            Vector3 offset = value - target;
            float decay = Mathf.Exp(-_stiffness * dt);
            Vector3 temp = (velocity + offset * _stiffness) * dt;

            velocity = (velocity - temp * _stiffness) * decay;
            value = target + (offset + temp) * decay;
        }
    }
}
