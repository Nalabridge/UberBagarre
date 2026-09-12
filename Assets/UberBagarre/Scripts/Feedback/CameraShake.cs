using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Secousse de caméra, sur son propre nœud du rig.
    ///
    /// Le modèle est celui du « traumatisme » : les impacts ajoutent du trauma, et la secousse
    /// vaut trauma AU CARRÉ. Conséquence voulue : deux petits coups ne font presque rien, un
    /// gros coup se sent nettement. Une secousse proportionnelle donnerait un tremblement
    /// permanent dès qu'on échange des coups.
    ///
    /// Le bruit de Perlin plutôt que du hasard : le mouvement reste continu, donc lisible,
    /// au lieu de sauter d'une frame à l'autre.
    /// </summary>
    [DefaultExecutionOrder(50)]
    public class CameraShake : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;

        [SerializeField, Min(0f)] private float _positionAmplitude = 0.085f;
        [SerializeField, Min(0f)] private float _rotationAmplitude = 2.6f;
        [SerializeField, Min(1f)] private float _frequency = 24f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Vitesse a laquelle la secousse retombe.")]
        private float _decayPerSecond = 1.9f;

        [SerializeField, Range(0f, 1f)] private float _maxTrauma = 1f;

        private float _trauma;
        private float _seed;

        public bool ShakeEnabled
        {
            get { return _enabled; }
            set { _enabled = value; }
        }

        public float Trauma { get { return _trauma; } }

        private void Awake()
        {
            _seed = Random.Range(0f, 100f);
        }

        /// <summary>Ajoute une secousse. L'intensité s'accumule, elle ne se remplace pas.</summary>
        public void Play(float intensity, float duration)
        {
            if (!_enabled || intensity <= 0f) return;

            _trauma = Mathf.Min(_maxTrauma, _trauma + intensity);

            // Une secousse plus longue retombe plus lentement : la duree pilote la decroissance.
            if (duration > 0.01f) _decayPerSecond = Mathf.Clamp(1f / duration, 0.6f, 12f);
        }

        private void LateUpdate()
        {
            if (_trauma <= 0.0001f)
            {
                transform.localPosition = Vector3.zero;
                transform.localRotation = Quaternion.identity;
                return;
            }

            _trauma = Mathf.Max(0f, _trauma - _decayPerSecond * Time.unscaledDeltaTime);

            // Le carre est ce qui separe nettement un petit coup d'un gros.
            float shake = _trauma * _trauma;
            float t = Time.unscaledTime * _frequency;

            Vector3 offset = new Vector3(
                (Mathf.PerlinNoise(_seed, t) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + 11f, t) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + 23f, t) - 0.5f) * 2f);

            Vector3 angles = new Vector3(
                (Mathf.PerlinNoise(_seed + 37f, t) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + 51f, t) - 0.5f) * 2f,
                (Mathf.PerlinNoise(_seed + 67f, t) - 0.5f) * 2f);

            transform.localPosition = offset * (_positionAmplitude * shake);
            transform.localRotation = Quaternion.Euler(angles * (_rotationAmplitude * shake));
        }
    }
}
