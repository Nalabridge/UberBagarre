using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// La lumière de lecture de l'adversaire : une petite lampe invisible, posée juste devant
    /// et au-dessus de la tête de l'ennemi le plus proche, côté joueur.
    ///
    /// Une rue de nuit bien éclairée pour l'œil l'est rarement là où l'on se bat : entre deux
    /// lampadaires, un type en veste sombre devient une silhouette noire sur un fond noir. Or
    /// en combat, lire l'adversaire — sa garde, son épaule qui recule avant le crochet — est
    /// TOUT le jeu. Le cinéma a la même règle : l'acteur a toujours sa lumière, même dans une
    /// ruelle « sans éclairage ».
    ///
    /// Elle n'éclaire que la face tournée vers le joueur, sur un rayon de quelques mètres, et
    /// suit la cible en douceur : on ne la remarque pas, on remarque seulement qu'on VOIT.
    /// </summary>
    [DisallowMultipleComponent]
    public class OpponentLight : MonoBehaviour
    {
        [SerializeField] private Camera _camera;
        [SerializeField] private Combatant _self;

        [SerializeField, Min(1f)]
        [Tooltip("Distance maximale a laquelle un adversaire recoit la lumiere.")]
        private float _searchRadius = 7f;

        [SerializeField, Min(0f)] private float _intensity = 1.4f;
        [SerializeField, Min(0.5f)] private float _lightRange = 4.5f;
        [SerializeField] private Color _color = new Color(1f, 0.9f, 0.8f);

        [SerializeField]
        [Tooltip("Position de la lampe par rapport a la tete de la cible : vers le joueur, puis en hauteur.")]
        private Vector2 _offset = new Vector2(0.55f, 0.35f);

        private Light _light;
        private Combatant _target;
        private float _weight;
        private float _nextSearch;
        private bool _placed;

        private void Awake()
        {
            if (_self == null) _self = GetComponent<Combatant>();
            if (_camera == null) _camera = GetComponentInChildren<Camera>();

            GameObject go = new GameObject("Lumiere de lecture (adversaire)");
            go.transform.SetParent(transform, false);

            _light = go.AddComponent<Light>();
            _light.type = LightType.Point;
            _light.color = _color;
            _light.range = _lightRange;
            _light.intensity = 0f;
            _light.shadows = LightShadows.None;
            _light.renderMode = LightRenderMode.ForcePixel;
            _light.bounceIntensity = 0f;
            _light.enabled = false;
        }

        private void OnDisable()
        {
            _weight = 0f;
            _placed = false;
            if (_light != null) _light.enabled = false;
        }

        private void LateUpdate()
        {
            if (_camera == null || _light == null) return;

            if (Time.unscaledTime >= _nextSearch)
            {
                _nextSearch = Time.unscaledTime + 0.2f;
                _target = FindTarget();
            }

            bool lit = _target != null && _target.IsAlive && _target.isActiveAndEnabled;
            float dt = Time.unscaledDeltaTime;
            _weight = Mathf.MoveTowards(_weight, lit ? 1f : 0f, dt * 2.5f);

            if (lit)
            {
                Vector3 head = _target.AimPosition;
                Vector3 toward = _camera.transform.position - head;
                toward.y = 0f;
                toward = toward.sqrMagnitude > 0.0001f ? toward.normalized : -_target.transform.forward;

                Vector3 goal = head + toward * _offset.x + Vector3.up * _offset.y;

                // Une nouvelle cible : on y va d'un coup, lampe éteinte. Sinon, on glisse.
                if (!_placed || _weight < 0.05f) _light.transform.position = goal;
                else _light.transform.position = Vector3.Lerp(_light.transform.position, goal, 1f - Mathf.Exp(-12f * dt));

                _placed = true;
            }

            _light.intensity = _intensity * _weight;
            _light.range = _lightRange;
            _light.color = _color;
            _light.enabled = _weight > 0.01f;
        }

        /// <summary>L'ennemi vivant le plus proche, devant la caméra.</summary>
        private Combatant FindTarget()
        {
            Vector3 eye = _camera.transform.position;
            Vector3 forward = _camera.transform.forward;

            Combatant best = null;
            float bestScore = float.MaxValue;

            var all = Combatant.All;
            for (int i = 0; i < all.Count; i++)
            {
                Combatant other = all[i];
                if (other == null || other == _self || !other.IsAlive || !other.isActiveAndEnabled) continue;
                if (_self != null && other.Faction == _self.Faction) continue;

                Vector3 delta = other.AimPosition - eye;
                float distance = delta.magnitude;
                if (distance > _searchRadius || distance < 0.01f) continue;

                // Devant, ou presque : un adversaire dans le dos n'a pas besoin qu'on le lise.
                float facing = Vector3.Dot(delta / distance, forward);
                if (facing < 0.2f) continue;

                float score = distance * (1.6f - facing);
                if (score < bestScore)
                {
                    bestScore = score;
                    best = other;
                }
            }

            return best;
        }
    }
}
