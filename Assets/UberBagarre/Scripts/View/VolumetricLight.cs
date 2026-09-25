using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Marque une lampe dont la lumière doit se voir DANS L'AIR (faisceau, halo de brume).
    ///
    /// Le rendu volumétrique lit la lampe elle-même — position, cône, couleur, portée,
    /// intensité — à chaque image. Il n'y a donc rien à régler deux fois : une lampe qui
    /// clignote, qu'on éteint au lever du jour ou qu'on oriente autrement fait clignoter,
    /// s'éteindre ou tourner son faisceau avec elle. C'est exactement ce que les cônes en
    /// maillage ne pouvaient pas faire : ils étaient un objet posé À CÔTÉ de la lumière.
    ///
    /// Seules les lampes marquées comptent. Toutes les prendre aurait donné le même poids à
    /// une veilleuse de salle de bain qu'à un projecteur de parking, et chaque néon de la rue
    /// aurait noyé l'image dans une brume colorée uniforme.
    /// </summary>
    [ExecuteAlways]
    [RequireComponent(typeof(Light))]
    [DisallowMultipleComponent]
    public class VolumetricLight : MonoBehaviour
    {
        private static readonly List<VolumetricLight> _all = new List<VolumetricLight>(32);

        [SerializeField, Min(0f)]
        [Tooltip("Multiplicateur de diffusion propre a cette lampe. Plus haut = air plus charge " +
                 "(fumee de club, pluie fine sous un lampadaire).")]
        private float _scattering = 1f;

        private Light _light;

        /// <summary>Toutes les lampes volumétriques actives.</summary>
        public static IReadOnlyList<VolumetricLight> All
        {
            get { return _all; }
        }

        public Light Light
        {
            get
            {
                if (_light == null) _light = GetComponent<Light>();
                return _light;
            }
        }

        public float Scattering
        {
            get { return _scattering; }
            set { _scattering = Mathf.Max(0f, value); }
        }

        private void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }
    }
}
