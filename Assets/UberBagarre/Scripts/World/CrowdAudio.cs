using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La voix de la foule autour de la fosse : un brouhaha permanent, et des clameurs qui
    /// répondent aux coups.
    ///
    /// Une foule muette est un décor ; une foule qui rugit sur un crochet au menton est un
    /// public. C'est le son, plus que les bras levés, qui dit au joueur que le coup a « porté »
    /// aux yeux de quelqu'un d'autre que lui.
    ///
    /// Tout est synthétisé au lancement, comme le reste de l'audio du projet : des voix
    /// imitées par du bruit filtré sur les formants d'une voyelle (« aaah », « oooh »). C'est
    /// grossier de près, mais une foule n'est jamais entendue de près — c'est un mélange, et
    /// le mélange est précisément ce que cette méthode reproduit bien.
    /// </summary>
    [DisallowMultipleComponent]
    public class CrowdAudio : MonoBehaviour
    {
        private const int SampleRate = 32000;

        [SerializeField, Min(1f)]
        [Tooltip("Rayon autour duquel les coups font reagir la foule.")]
        private float _radius = 16f;

        [SerializeField, Range(0f, 1f)] private float _murmurVolume = 0.32f;
        [SerializeField, Range(0f, 1f)] private float _cheerVolume = 0.85f;

        [SerializeField, Min(0f)]
        [Tooltip("Delai minimal entre deux clameurs : sans lui, un enchainement devient un mur de bruit.")]
        private float _cooldown = 0.45f;

        [SerializeField] private int _seed = 311;

        private AudioSource _murmur;
        private AudioSource _voice;
        private AudioClip _murmurClip;
        private AudioClip _cheerClip;
        private AudioClip _oohClip;
        private AudioClip _roarClip;
        private float _nextCheer;
        private float _excitement;

        /// <summary>Excitation actuelle de la foule, de 0 à 1. Monte avec les coups, retombe seule.</summary>
        public float Excitement
        {
            get { return _excitement; }
        }

        private void Awake()
        {
            _murmur = CreateSource(true);
            _voice = CreateSource(false);
        }

        private AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.spatialBlend = 0.7f;
            source.rolloffMode = AudioRolloffMode.Linear;
            source.minDistance = 6f;
            source.maxDistance = 45f;
            source.dopplerLevel = 0f;
            return source;
        }

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;
            Combatant.AnyDied += OnAnyDied;

            EnsureClips();

            _murmur.clip = _murmurClip;
            _murmur.volume = _murmurVolume;
            _murmur.Play();
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;
            Combatant.AnyDied -= OnAnyDied;

            if (_murmur != null) _murmur.Stop();
        }

        private void OnDestroy()
        {
            if (_murmurClip != null) Destroy(_murmurClip);
            if (_cheerClip != null) Destroy(_cheerClip);
            if (_oohClip != null) Destroy(_oohClip);
            if (_roarClip != null) Destroy(_roarClip);
        }

        private void Update()
        {
            _excitement = Mathf.MoveTowards(_excitement, 0f, 0.18f * Time.deltaTime);

            // Le brouhaha monte avec l'excitation : une salle chauffée ne redevient pas
            // silencieuse entre deux coups.
            if (_murmur != null) _murmur.volume = _murmurVolume * (0.7f + 0.8f * _excitement);
        }

        /// <summary>Fait rugir la foule, par exemple au début d'un combat.</summary>
        public void Roar(float strength)
        {
            EnsureClips();
            _excitement = Mathf.Clamp01(_excitement + strength);
            Play(_roarClip, _cheerVolume * Mathf.Clamp01(strength + 0.3f), 0.95f + Random.value * 0.1f);
        }

        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (victim == null || !InRange(victim.transform.position)) return;
            if (Time.time < _nextCheer) return;

            _nextCheer = Time.time + _cooldown;

            // Un coup bloqué fait « oooh » ; un coup qui passe fait lever la salle.
            if (info.Blocked)
            {
                _excitement = Mathf.Clamp01(_excitement + 0.05f);
                Play(_oohClip, _cheerVolume * 0.45f, 0.95f + Random.value * 0.1f);
                return;
            }

            float strength = Mathf.Clamp01(0.25f + info.Amount / 30f + (info.Zone == HitZone.Head ? 0.2f : 0f));
            _excitement = Mathf.Clamp01(_excitement + strength * 0.35f);

            Play(_cheerClip, _cheerVolume * (0.35f + 0.65f * strength), 0.92f + Random.value * 0.16f);
        }

        private void OnAnyDied(Combatant victim, DamageInfo info)
        {
            if (victim == null || !InRange(victim.transform.position)) return;

            _nextCheer = Time.time + 1.2f;
            Roar(1f);
        }

        private bool InRange(Vector3 point)
        {
            return (point - transform.position).sqrMagnitude <= _radius * _radius;
        }

        private void Play(AudioClip clip, float volume, float pitch)
        {
            if (_voice == null || clip == null) return;

            _voice.pitch = pitch;
            _voice.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ synthèse

        private void EnsureClips()
        {
            if (_murmurClip != null) return;

            System.Random random = new System.Random(_seed);

            _murmurClip = Murmur(random);
            _cheerClip = Voices("Clameur (generee)", random, 1.9f, 26, new[] { 730f, 1090f, 2440f }, 0.10f, 1.4f);
            _oohClip = Voices("Oooh (generee)", random, 1.3f, 18, new[] { 320f, 800f, 2240f }, 0.18f, 1.0f);
            _roarClip = Voices("Ovation (generee)", random, 3.2f, 30, new[] { 700f, 1150f, 2500f }, 0.25f, 2.6f);
        }

        /// <summary>Brouhaha en boucle : des dizaines de voix syllabiques, graves, qui se chevauchent.</summary>
        private static AudioClip Murmur(System.Random random)
        {
            int length = SampleRate * 6;
            float[] data = new float[length];

            for (int v = 0; v < 16; v++)
            {
                float center = 260f + (float)random.NextDouble() * 650f;
                Biquad band = Biquad.Bandpass(center, 3.5f);

                float syllable = 3f + (float)random.NextDouble() * 3f;
                float offset = (float)random.NextDouble() * 10f;
                float level = 0.05f + (float)random.NextDouble() * 0.05f;

                for (int n = 0; n < length; n++)
                {
                    float t = n / (float)SampleRate;
                    float noise = (float)random.NextDouble() * 2f - 1f;

                    // Syllabes : enveloppe lente et irrégulière, faite de deux sinus incommensurables.
                    float envelope = Mathf.Max(0f, Mathf.Sin((t + offset) * syllable * Mathf.PI)
                                                   * Mathf.Sin((t + offset) * syllable * 0.37f * Mathf.PI));

                    data[n] += band.Process(noise) * envelope * level;
                }
            }

            // Boucle sans couture : la fin se fond dans le début.
            int fade = SampleRate / 3;
            for (int n = 0; n < fade; n++)
            {
                float k = n / (float)fade;
                data[n] = data[n] * k + data[length - fade + n] * (1f - k);
            }

            float[] looped = new float[length - fade];
            System.Array.Copy(data, looped, looped.Length);

            Normalize(looped, 0.6f);

            AudioClip clip = AudioClip.Create("Brouhaha (genere)", looped.Length, 1, SampleRate, false);
            clip.SetData(looped, 0);
            return clip;
        }

        /// <summary>
        /// Une clameur : des voix qui démarrent presque ensemble et chantent la même voyelle.
        /// Chaque voix est un souffle filtré sur les formants de la voyelle, avec une attaque
        /// et une durée à elle — l'irrégularité est ce qui rend une foule crédible.
        /// </summary>
        private static AudioClip Voices(string name, System.Random random, float duration, int voices,
            float[] formants, float spread, float sustain)
        {
            int length = Mathf.RoundToInt(SampleRate * duration);
            float[] data = new float[length];

            for (int v = 0; v < voices; v++)
            {
                float start = (float)random.NextDouble() * spread;
                float attack = 0.05f + (float)random.NextDouble() * 0.15f;
                float hold = sustain * (0.5f + (float)random.NextDouble() * 0.6f);
                float shift = 0.85f + (float)random.NextDouble() * 0.35f;

                Biquad a = Biquad.Bandpass(formants[0] * shift, 5f);
                Biquad b = Biquad.Bandpass(formants[1] * shift, 6f);
                Biquad c = Biquad.Bandpass(formants[2] * shift, 7f);

                // Un peu de hauteur : un souffle pur sonne comme du vent, une voix a une note.
                float pitch = 140f + (float)random.NextDouble() * 180f;
                float phase = 0f;

                for (int n = 0; n < length; n++)
                {
                    float t = n / (float)SampleRate - start;
                    if (t < 0f) continue;

                    float envelope = Mathf.Clamp01(t / attack) * Mathf.Exp(-Mathf.Max(0f, t - hold) * 3.2f);
                    if (envelope < 0.0005f && t > hold) break;

                    phase += pitch * (1f + 0.02f * Mathf.Sin(t * 31f)) / SampleRate;
                    phase -= Mathf.Floor(phase);

                    float source = ((float)random.NextDouble() * 2f - 1f) * 0.7f + (phase * 2f - 1f) * 0.5f;
                    float voice = a.Process(source) * 1f + b.Process(source) * 0.6f + c.Process(source) * 0.3f;

                    data[n] += voice * envelope * 0.12f;
                }
            }

            Normalize(data, 0.9f);

            AudioClip clip = AudioClip.Create(name, length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Normalize(float[] data, float peak)
        {
            float max = 0f;
            for (int i = 0; i < data.Length; i++) max = Mathf.Max(max, Mathf.Abs(data[i]));
            if (max < 1e-5f) return;

            float gain = peak / max;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;
        }

        /// <summary>Filtre passe-bande du second ordre (formules de R. Bristow-Johnson).</summary>
        private struct Biquad
        {
            private float _b0, _b2, _a1, _a2;
            private float _x1, _x2, _y1, _y2;

            public static Biquad Bandpass(float frequency, float q)
            {
                float w0 = 2f * Mathf.PI * Mathf.Clamp(frequency, 20f, SampleRate * 0.45f) / SampleRate;
                float alpha = Mathf.Sin(w0) / (2f * q);
                float a0 = 1f + alpha;

                Biquad filter = new Biquad();
                filter._b0 = alpha / a0;
                filter._b2 = -alpha / a0;
                filter._a1 = -2f * Mathf.Cos(w0) / a0;
                filter._a2 = (1f - alpha) / a0;
                return filter;
            }

            public float Process(float x)
            {
                float y = _b0 * x + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1;
                _x1 = x;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }
    }
}
