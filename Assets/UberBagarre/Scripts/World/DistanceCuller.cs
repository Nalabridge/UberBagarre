using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Éteint les vraies lumières (et, au besoin, des objets entiers) loin du joueur.
    ///
    /// Une ville compte des centaines de lampadaires, d'enseignes et de vitrines. En rendu
    /// différé chaque lampe coûte ce qu'elle couvre à l'écran, et à 150 m une lampe ne couvre
    /// presque rien — mais cent lampes à 150 m, si. Leur tête reste allumée (matière émissive,
    /// que le bloom fait briller) : de loin, c'est tout ce qu'on voit d'une lampe. Ce qui
    /// s'éteint, c'est la lumière qu'elle projette au sol, qu'on ne verrait de toute façon pas.
    ///
    /// Une hystérésis évite qu'une lampe clignote quand on marche pile à la limite, et la
    /// vérification ne tourne que quelques fois par seconde, par tranches.
    /// </summary>
    public class DistanceCuller : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Ce qui compte : en general la camera du joueur. Vide = la camera principale.")]
        private Transform _viewer;

        [SerializeField]
        [Tooltip("Les lampes sous ces racines sont gerees.")]
        private Transform[] _lightRoots = new Transform[0];

        [SerializeField, Min(5f)] private float _lightRadius = 75f;

        [SerializeField]
        [Tooltip("Objets entiers allumes seulement de pres (foule d'un lieu, ambiance, effets).")]
        private GameObject[] _objects = new GameObject[0];

        [SerializeField, Min(5f)] private float _objectRadius = 90f;

        [SerializeField, Min(0f)] private float _hysteresis = 8f;

        [SerializeField, Min(1)]
        [Tooltip("Lampes verifiees par image : la ville entiere est parcourue en quelques images.")]
        private int _perFrame = 64;

        private Light[] _lights;
        private bool[] _lightOn;
        private int _cursor;

        private void Start()
        {
            List<Light> lights = new List<Light>();
            for (int i = 0; i < _lightRoots.Length; i++)
            {
                if (_lightRoots[i] != null) lights.AddRange(_lightRoots[i].GetComponentsInChildren<Light>(true));
            }

            // Le soleil et la lune ne sont jamais sous ces racines, mais une racine mal choisie
            // ne doit pas éteindre le ciel.
            lights.RemoveAll(l => l == null || l.type == LightType.Directional);

            _lights = lights.ToArray();
            _lightOn = new bool[_lights.Length];
            for (int i = 0; i < _lights.Length; i++) _lightOn[i] = _lights[i].enabled;
        }

        private void Update()
        {
            Transform viewer = _viewer;
            if (viewer == null && Camera.main != null) viewer = Camera.main.transform;
            if (viewer == null || _lights == null) return;

            Vector3 eye = viewer.position;

            int count = Mathf.Min(_perFrame, _lights.Length);
            for (int n = 0; n < count; n++)
            {
                if (_cursor >= _lights.Length) _cursor = 0;
                int i = _cursor++;

                Light light = _lights[i];
                if (light == null) continue;

                float distance = (light.transform.position - eye).sqrMagnitude;
                float radius = _lightOn[i] ? _lightRadius + _hysteresis : _lightRadius;
                bool on = distance < radius * radius;
                if (on == _lightOn[i]) continue;

                _lightOn[i] = on;
                light.enabled = on;
            }

            for (int i = 0; i < _objects.Length; i++)
            {
                GameObject target = _objects[i];
                if (target == null) continue;

                bool active = target.activeSelf;
                float radius = active ? _objectRadius + _hysteresis : _objectRadius;
                bool want = (target.transform.position - eye).sqrMagnitude < radius * radius;
                if (want != active) target.SetActive(want);
            }
        }
    }
}
