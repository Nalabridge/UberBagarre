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
    /// Les mélanger dans une seule valeur rendrait impossible d'avoir un coup qui accompagne
    /// la caméra ET un choc sec au moment du contact.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class CameraPunch : MonoBehaviour
    {
        [SerializeField, Min(0.5f)] private float _impulseDecay = 9f;
        [SerializeField, Min(0.5f)] private float _drivenResponse = 22f;
        [SerializeField] private bool _enabled = true;

        private Vector3 _drivenPosition;
        private Vector3 _drivenEuler;
        private Vector3 _smoothedPosition;
        private Vector3 _smoothedEuler;
        private Vector3 _impulsePosition;
        private Vector3 _impulseEuler;

        /// <summary>Suivi continu de l'animation d'attaque. À appeler chaque frame pendant le coup.</summary>
        public void SetDriven(Vector3 position, Vector3 euler)
        {
            _drivenPosition = position;
            _drivenEuler = euler;
        }

        /// <summary>À-coup ponctuel, au contact.</summary>
        public void AddImpulse(Vector3 position, Vector3 euler)
        {
            _impulsePosition += position;
            _impulseEuler += euler;
        }

        private void LateUpdate()
        {
            float dt = Time.unscaledDeltaTime;

            if (!_enabled)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }

            float t = 1f - Mathf.Exp(-_drivenResponse * dt);
            _smoothedPosition = Vector3.Lerp(_smoothedPosition, _drivenPosition, t);
            _smoothedEuler = Vector3.Lerp(_smoothedEuler, _drivenEuler, t);

            float decay = Mathf.Exp(-_impulseDecay * dt);
            _impulsePosition *= decay;
            _impulseEuler *= decay;

            transform.localPosition = _smoothedPosition + _impulsePosition;
            transform.localRotation = Quaternion.Euler(_smoothedEuler + _impulseEuler);
        }
    }
}
