using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Très court ralenti au moment d'un impact.
    ///
    /// C'est l'effet le plus rentable du jeu de combat : quelques centièmes de seconde suffisent
    /// à faire sentir qu'un coup a « accroché » quelque chose. Au-delà de ~60 ms, ça devient
    /// visible et le jeu paraît saccadé — d'où le plafond.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;

        [SerializeField, Range(0f, 0.9f)]
        [Tooltip("Vitesse du temps pendant l'arret. 0 = fige completement.")]
        private float _slowTimeScale = 0.05f;

        [SerializeField, Range(0f, 0.12f)] private float _maxDuration = 0.06f;

        private float _timer;
        private float _defaultFixedDelta;

        private void Awake()
        {
            _defaultFixedDelta = Time.fixedDeltaTime;
        }

        public void Play(float duration)
        {
            if (!_enabled || duration <= 0f) return;

            _timer = Mathf.Max(_timer, Mathf.Min(duration, _maxDuration));
            Time.timeScale = _slowTimeScale;
            Time.fixedDeltaTime = _defaultFixedDelta * Mathf.Max(0.02f, _slowTimeScale);
        }

        private void Update()
        {
            if (_timer <= 0f) return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;

            Restore();
        }

        private void OnDisable()
        {
            Restore();
        }

        private void Restore()
        {
            _timer = 0f;
            Time.timeScale = 1f;
            Time.fixedDeltaTime = _defaultFixedDelta;
        }
    }
}
