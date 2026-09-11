using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Repère dans lequel les poses de mains sont exprimées.
    ///
    /// Il suit la caméra en position et en lacet, mais seulement une fraction de son tangage.
    /// Raison : les épaules sont désormais sur un vrai buste, qui ne bascule pas quand on
    /// regarde ses pieds. Si les poings suivaient le tangage à 100 %, ils descendraient
    /// jusqu'au sol avec le regard et les bras se tordraient. À l'inverse, à 0 %, la garde
    /// sortirait de l'écran dès qu'on lève les yeux.
    ///
    /// La valeur par défaut garde les poings cadrés tout en laissant le corps crédible.
    /// </summary>
    [DefaultExecutionOrder(90)]
    public class HandsAimAnchor : MonoBehaviour
    {
        [SerializeField] private PlayerLook _look;

        [SerializeField]
        [Tooltip("La camera : donne la position (donc le head bob et les secousses sont suivis).")]
        private Transform _positionSource;

        [SerializeField, Range(0f, 1f)] private float _pitchInfluence = 0.45f;

        private void LateUpdate()
        {
            if (_positionSource != null)
            {
                transform.position = _positionSource.position;
            }

            if (_look != null)
            {
                transform.rotation = Quaternion.Euler(_look.Pitch * _pitchInfluence, _look.Yaw, 0f);
            }
        }
    }
}
