using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Les feux d'un carrefour : vert d'un axe, orange, tout rouge une seconde, puis l'autre
    /// axe. La circulation (<see cref="TrafficCar"/>) s'arrête au rouge, passe à l'orange si
    /// elle est trop près pour freiner. Le joueur, lui, fait ce qu'il veut.
    ///
    /// Les lampes sont de vrais objets dont on change la matière : allumée (néon) ou éteinte.
    /// Chaque carrefour a son décalage, pour que la ville ne passe pas au vert d'un seul coup.
    /// </summary>
    public class TrafficLight : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Demi-dimensions du carrefour (x, z) : la ligne d'arret est a son bord.")]
        private Vector2 _halfSize = new Vector2(6f, 6f);

        [SerializeField, Min(1f)] private float _green = 10f;
        [SerializeField, Min(0.5f)] private float _amber = 2.6f;
        [SerializeField, Min(0f)] private float _allRed = 1.2f;
        [SerializeField] private float _offset;

        [Header("Lampes (rouge, orange, vert par tete)")]
        [SerializeField] private Renderer[] _northSouth = new Renderer[0];
        [SerializeField] private Renderer[] _eastWest = new Renderer[0];

        [SerializeField] private Material _off;
        [SerializeField] private Material _red;
        [SerializeField] private Material _amberLit;
        [SerializeField] private Material _greenLit;

        private enum Signal
        {
            Green = 0,
            Amber = 1,
            Red = 2
        }

        private static readonly List<TrafficLight> _all = new List<TrafficLight>();
        private Signal _ns = Signal.Red;
        private Signal _ew = Signal.Red;
        private bool _painted;

        private void OnEnable()
        {
            if (!_all.Contains(this)) _all.Add(this);
            _painted = false;
        }

        private void OnDisable()
        {
            _all.Remove(this);
        }

        private void Update()
        {
            float cycle = 2f * (_green + _amber + _allRed);
            float t = Mathf.Repeat(Time.time + _offset, cycle);
            float half = cycle * 0.5f;

            Signal ns;
            Signal ew;
            if (t < half)
            {
                ns = t < _green ? Signal.Green : t < _green + _amber ? Signal.Amber : Signal.Red;
                ew = Signal.Red;
            }
            else
            {
                float u = t - half;
                ew = u < _green ? Signal.Green : u < _green + _amber ? Signal.Amber : Signal.Red;
                ns = Signal.Red;
            }

            if (_painted && ns == _ns && ew == _ew) return;

            _ns = ns;
            _ew = ew;
            _painted = true;
            Paint(_northSouth, ns);
            Paint(_eastWest, ew);
        }

        private void Paint(Renderer[] lamps, Signal signal)
        {
            for (int i = 0; i + 2 < lamps.Length; i += 3)
            {
                Set(lamps[i], signal == Signal.Red ? _red : _off);
                Set(lamps[i + 1], signal == Signal.Amber ? _amberLit : _off);
                Set(lamps[i + 2], signal == Signal.Green ? _greenLit : _off);
            }
        }

        private static void Set(Renderer renderer, Material material)
        {
            if (renderer != null && material != null && renderer.sharedMaterial != material) renderer.sharedMaterial = material;
        }

        /// <summary>
        /// Distance du pare-chocs avant à la ligne d'arrêt du premier feu qui impose de s'arrêter
        /// sur le chemin, ou MaxValue. À l'orange, on ne s'arrête que si on a la place de freiner.
        /// </summary>
        public static float StopDistance(Vector3 position, Vector3 forward, float halfLength, float speed, float braking)
        {
            float nearest = float.MaxValue;
            bool northSouth = Mathf.Abs(forward.z) >= Mathf.Abs(forward.x);

            for (int i = 0; i < _all.Count; i++)
            {
                TrafficLight light = _all[i];
                if (light == null) continue;

                Vector3 c = light.transform.position;
                float along = northSouth ? (c.z - position.z) * Mathf.Sign(forward.z) : (c.x - position.x) * Mathf.Sign(forward.x);
                float lateral = northSouth ? Mathf.Abs(position.x - c.x) : Mathf.Abs(position.z - c.z);
                float halfAlong = northSouth ? light._halfSize.y : light._halfSize.x;
                float halfAcross = northSouth ? light._halfSize.x : light._halfSize.y;

                if (lateral > halfAcross) continue;

                float toLine = along - halfAlong - halfLength;
                if (toLine < -0.5f || toLine > 45f) continue;

                Signal signal = northSouth ? light._ns : light._ew;
                if (signal == Signal.Green) continue;
                if (signal == Signal.Amber && toLine < speed * speed / (2f * Mathf.Max(0.5f, braking)) + 1f) continue;

                nearest = Mathf.Min(nearest, Mathf.Max(0f, toLine));
            }

            return nearest;
        }
    }
}
