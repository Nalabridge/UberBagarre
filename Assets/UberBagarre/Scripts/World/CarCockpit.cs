using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// L'habitacle vu de la place du conducteur : le volant tourne avec les roues (un tour et
    /// demi de butée à butée, comme une vraie direction), les aiguilles du compteur et du
    /// compte-tours suivent la vitesse et le régime, la caméra se pose aux yeux du conducteur.
    ///
    /// La carrosserie des modèles de la carte n'a pas d'intérieur : vue du dedans, ses faces
    /// (tournées vers l'extérieur) ne se dessinent pas — on voit donc dehors par les vitres, et
    /// l'habitacle construit par l'éditeur (tableau de bord, montants, ciel de toit, portières,
    /// sièges) donne le reste. Si un matériau de carrosserie est dessiné des deux côtés, on le
    /// remplace, le temps de la vue intérieure, par une copie dessinée d'un seul côté.
    /// </summary>
    public class CarCockpit : MonoBehaviour
    {
        [SerializeField] private Transform _eye;
        [SerializeField] private Transform _wheel;
        [SerializeField] private Transform _speedNeedle;
        [SerializeField] private Transform _rpmNeedle;

        [SerializeField]
        [Tooltip("Rendus de la carrosserie (pour la vue interieure).")]
        private Renderer[] _body = new Renderer[0];

        [SerializeField]
        [Tooltip("Maillages de la coque remplaces, en vue interieure, par une copie videe de l'habitacle.")]
        private MeshFilter[] _bodyFilters = new MeshFilter[0];

        [SerializeField] private Mesh[] _insideMeshes = new Mesh[0];

        [SerializeField]
        [Tooltip("Vitres opaques du modele : cachees en vue interieure.")]
        private Renderer[] _windows = new Renderer[0];

        [SerializeField, Min(1f)] private float _steeringRatio = 14f;
        [SerializeField, Min(10f)] private float _dialMaxKmh = 220f;
        [SerializeField] private float _dialSweep = 250f;

        private DrivableCar _car;
        private Quaternion _wheelRest;
        private Quaternion _speedRest;
        private Quaternion _rpmRest;
        private bool _inside;
        private readonly Dictionary<Renderer, Material[]> _saved = new Dictionary<Renderer, Material[]>();

        /// <summary>Les yeux du conducteur (repère qui suit la voiture).</summary>
        public Transform Eye { get { return _eye; } }

        public void Configure(Transform eye, Transform wheel, Transform speedNeedle, Transform rpmNeedle, Renderer[] body,
            MeshFilter[] bodyFilters, Mesh[] insideMeshes, Renderer[] windows)
        {
            _eye = eye;
            _wheel = wheel;
            _speedNeedle = speedNeedle;
            _rpmNeedle = rpmNeedle;
            _body = body ?? new Renderer[0];
            _bodyFilters = bodyFilters ?? new MeshFilter[0];
            _insideMeshes = insideMeshes ?? new Mesh[0];
            _windows = windows ?? new Renderer[0];
        }

        private Mesh[] _outsideMeshes;

        private void Awake()
        {
            _car = GetComponent<DrivableCar>();
            if (_wheel != null) _wheelRest = _wheel.localRotation;
            if (_speedNeedle != null) _speedRest = _speedNeedle.localRotation;
            if (_rpmNeedle != null) _rpmRest = _rpmNeedle.localRotation;
            _outsideMeshes = new Mesh[_bodyFilters.Length];
            for (int i = 0; i < _bodyFilters.Length; i++) _outsideMeshes[i] = _bodyFilters[i] != null ? _bodyFilters[i].sharedMesh : null;
        }

        private void OnDisable()
        {
            SetInside(false);
        }

        /// <summary>La caméra passe dans l'habitacle (ou en sort).</summary>
        public void SetInside(bool inside)
        {
            if (_inside == inside) return;
            _inside = inside;

            // La coque vidée de l'habitacle, les vitres opaques cachées (et l'inverse en sortant).
            for (int i = 0; i < _bodyFilters.Length && i < _insideMeshes.Length; i++)
            {
                if (_bodyFilters[i] == null) continue;
                _bodyFilters[i].sharedMesh = inside ? _insideMeshes[i] : (_outsideMeshes != null && i < _outsideMeshes.Length ? _outsideMeshes[i] : _bodyFilters[i].sharedMesh);
            }

            for (int i = 0; i < _windows.Length; i++)
            {
                if (_windows[i] != null) _windows[i].enabled = !inside;
            }

            if (inside)
            {
                for (int i = 0; i < _body.Length; i++)
                {
                    Renderer r = _body[i];
                    if (r == null) continue;
                    Material[] shared = r.sharedMaterials;
                    bool any = false;
                    Material[] single = new Material[shared.Length];
                    for (int m = 0; m < shared.Length; m++)
                    {
                        single[m] = shared[m];
                        if (shared[m] == null || !TwoSided(shared[m])) continue;
                        Material copy = new Material(shared[m]);
                        copy.name = shared[m].name + " (vue interieure)";
                        if (copy.HasProperty("_Cull")) copy.SetFloat("_Cull", 2f);
                        if (copy.HasProperty("_CullMode")) copy.SetFloat("_CullMode", 2f);
                        single[m] = copy;
                        any = true;
                    }

                    if (!any) continue;
                    _saved[r] = shared;
                    r.sharedMaterials = single;
                }
            }
            else
            {
                foreach (KeyValuePair<Renderer, Material[]> pair in _saved)
                {
                    if (pair.Key == null) continue;
                    Material[] current = pair.Key.sharedMaterials;
                    pair.Key.sharedMaterials = pair.Value;
                    for (int m = 0; m < current.Length; m++)
                    {
                        if (current[m] != null && System.Array.IndexOf(pair.Value, current[m]) < 0) Destroy(current[m]);
                    }
                }

                _saved.Clear();
            }
        }

        private static bool TwoSided(Material material)
        {
            if (material.HasProperty("_Cull")) return material.GetFloat("_Cull") < 0.5f;
            if (material.HasProperty("_CullMode")) return material.GetFloat("_CullMode") < 0.5f;
            return false;
        }

        private void LateUpdate()
        {
            if (_car == null) return;

            // Hors de vue, l'habitacle est figé : seule la voiture conduite (ou toute proche) bouge ses aiguilles.
            if (!_car.Occupied && !_inside) return;

            if (_wheel != null)
            {
                float turn = Mathf.Clamp(_car.WheelAngle * _steeringRatio, -540f, 540f);
                // Vu du conducteur (au bout de l'axe), un angle positif tourne dans le sens des aiguilles d'une montre.
                _wheel.localRotation = _wheelRest * Quaternion.AngleAxis(turn, Vector3.forward);
            }

            if (_speedNeedle != null)
            {
                float k = Mathf.Clamp01(_car.SpeedKmh / _dialMaxKmh);
                _speedNeedle.localRotation = _speedRest * Quaternion.AngleAxis(_dialSweep * k, Vector3.forward);
            }

            if (_rpmNeedle != null)
            {
                float k = Mathf.Clamp01(_car.EngineRpm / 8000f);
                _rpmNeedle.localRotation = _rpmRest * Quaternion.AngleAxis(_dialSweep * k, Vector3.forward);
            }
        }
    }
}
