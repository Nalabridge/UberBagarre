using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Une lyre de club : un projecteur qui balaie la salle.
    ///
    /// Le balayage se fait AUTOUR DE LA VERTICALE du monde, à partir de l'orientation de
    /// départ : quelle que soit l'inclinaison donnée à la lyre, le faisceau décrit un cône
    /// au-dessus de la piste au lieu de tourner sur lui-même. Le faisceau visible dans la
    /// fumée vient de la lampe (VolumetricLight) : c'est la même lumière qui éclaire le sol,
    /// les danseurs et l'air, comme dans une vraie salle.
    ///
    /// La vitesse s'accorde au tempo : une lyre calée sur la musique se lit comme un
    /// spectacle, une lyre libre comme un gyrophare.
    /// </summary>
    [DisallowMultipleComponent]
    public class SweepingLight : MonoBehaviour
    {
        [SerializeField, Range(0f, 180f)] private float _panAmplitude = 40f;
        [SerializeField, Range(0f, 60f)] private float _tiltAmplitude = 15f;

        [SerializeField, Min(0.25f)]
        [Tooltip("Nombre de temps pour un aller-retour complet.")]
        private float _beatsPerCycle = 8f;

        [SerializeField] private float _phase;

        private Quaternion _rest;
        private bool _captured;

        private void OnEnable()
        {
            if (_captured) return;

            _rest = transform.rotation;
            _captured = true;
        }

        private void LateUpdate()
        {
            float cycle = (ClubMusic.GlobalBeats / Mathf.Max(0.25f, _beatsPerCycle) + _phase) * Mathf.PI * 2f;

            float pan = Mathf.Sin(cycle) * _panAmplitude;
            float tilt = Mathf.Sin(cycle * 1.5f + 1.3f) * _tiltAmplitude;

            transform.rotation = Quaternion.AngleAxis(pan, Vector3.up) * _rest * Quaternion.AngleAxis(tilt, Vector3.right);
        }
    }
}
