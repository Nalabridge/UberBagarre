using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La musique du club : une boucle techno de quatre mesures, SYNTHÉTISÉE au lancement.
    ///
    /// Pourquoi la générer plutôt qu'importer un fichier : le projet n'embarque aucun son, et
    /// tout ce qu'on entend (coups, chocs, verre) est déjà synthétisé. Une musique importée
    /// poserait aussi une question de droits qu'une boucle générée ne pose pas.
    ///
    /// Le vrai intérêt est ailleurs : la musique DONNE LE TEMPO. Les lumières de la piste, les
    /// lyres et les danseurs lisent <see cref="Pulse"/>, qui vient de la position de lecture
    /// réelle de la source audio. Rien ne dérive : si la musique saccade, les lumières saccadent
    /// avec elle, et c'est ce qui fait qu'un club « tient » au lieu d'être une salle éclairée
    /// avec un son par-dessus.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    [DisallowMultipleComponent]
    public class ClubMusic : MonoBehaviour
    {
        private const int SampleRate = 32000;
        private const int Beats = 16;

        [SerializeField, Range(90f, 150f)] private float _bpm = 124f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.55f;
        [SerializeField] private int _seed = 7;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part de spatialisation. 0 = on l'entend partout pareil, 1 = elle vient des enceintes.")]
        private float _spatial = 0.45f;

        private AudioSource _source;
        private AudioClip _clip;
        private int _samplesPerBeat;
        private float _fallbackStart;

        /// <summary>La musique active, s'il y en a une. Les lumières et les danseurs la lisent.</summary>
        public static ClubMusic Current { get; private set; }

        /// <summary>Position dans le temps en cours, de 0 (le coup de grosse caisse) à 1.</summary>
        public float BeatPhase
        {
            get
            {
                float beats = BeatsElapsed;
                return beats - Mathf.Floor(beats);
            }
        }

        /// <summary>Nombre de temps écoulés depuis le début de la boucle, avec la fraction.</summary>
        public float BeatsElapsed
        {
            get
            {
                if (_source != null && _source.isPlaying && _samplesPerBeat > 0)
                {
                    return _source.timeSamples / (float)_samplesPerBeat;
                }

                return (Time.time - _fallbackStart) * _bpm / 60f;
            }
        }

        /// <summary>Impulsion qui retombe après chaque temps : 1 sur la grosse caisse, puis décroît.</summary>
        public float Pulse
        {
            get { return Mathf.Exp(-BeatPhase * 5.5f); }
        }

        /// <summary>Impulsion de la musique active, ou 0 sans musique.</summary>
        public static float GlobalPulse
        {
            get { return Current != null ? Current.Pulse : 0f; }
        }

        /// <summary>Temps écoulés de la musique active, ou le temps de jeu converti à 124 BPM.</summary>
        public static float GlobalBeats
        {
            get { return Current != null ? Current.BeatsElapsed : Time.time * 124f / 60f; }
        }

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.loop = true;
            _source.spatialBlend = _spatial;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 10f;
            _source.maxDistance = 70f;
            _source.dopplerLevel = 0f;
        }

        private void OnEnable()
        {
            Current = this;
            _fallbackStart = Time.time;

            if (_clip == null) _clip = Build();

            _source.clip = _clip;
            _source.volume = _volume;
            _source.Play();
        }

        private void OnDisable()
        {
            if (Current == this) Current = null;
            if (_source != null) _source.Stop();
        }

        private void OnDestroy()
        {
            if (_clip != null) Destroy(_clip);
        }

        // ------------------------------------------------------------------ synthèse

        private AudioClip Build()
        {
            _samplesPerBeat = Mathf.RoundToInt(SampleRate * 60f / _bpm);
            int length = _samplesPerBeat * Beats;
            float[] data = new float[length];
            System.Random random = new System.Random(_seed);

            // Suite d'accords de la boucle, une mesure chacun : la mineur, fa, do, sol. Les
            // fondamentales servent a la basse, les accords a la nappe.
            float[] roots = { 55f, 43.65f, 65.41f, 49f };
            float[][] chords =
            {
                new[] { 220f, 261.63f, 329.63f },
                new[] { 174.61f, 220f, 261.63f },
                new[] { 196f, 261.63f, 329.63f },
                new[] { 196f, 246.94f, 293.66f }
            };

            for (int beat = 0; beat < Beats; beat++)
            {
                int start = beat * _samplesPerBeat;
                int bar = beat / 4;

                Kick(data, start);

                // Charleston ouvert a contretemps, fermé sur les doubles croches.
                HiHat(data, start + _samplesPerBeat / 2, 0.16f, 30f, random);
                HiHat(data, start + _samplesPerBeat / 4, 0.05f, 90f, random);
                HiHat(data, start + _samplesPerBeat * 3 / 4, 0.05f, 90f, random);

                if (beat % 4 == 1 || beat % 4 == 3) Clap(data, start, random);

                // Basse a contretemps, sur la fondamentale de la mesure.
                Bass(data, start + _samplesPerBeat / 2, roots[bar], _samplesPerBeat / 2);
            }

            for (int bar = 0; bar < 4; bar++)
            {
                Pad(data, bar * _samplesPerBeat * 4, _samplesPerBeat * 4, chords[bar]);
            }

            // Saturation douce : les couches se cumulent sans jamais écrêter net.
            for (int i = 0; i < length; i++) data[i] = Tanh(data[i] * 1.1f) * 0.9f;

            AudioClip clip = AudioClip.Create("Musique du club (generee)", length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static void Kick(float[] data, int start)
        {
            int length = (int)(SampleRate * 0.38f);
            double phase = 0.0;

            for (int n = 0; n < length && start + n < data.Length; n++)
            {
                float t = n / (float)SampleRate;
                float frequency = 44f + 90f * Mathf.Exp(-t * 32f);
                phase += 2.0 * System.Math.PI * frequency / SampleRate;

                float body = Mathf.Sin((float)phase) * Mathf.Exp(-t * 8.5f);
                data[start + n] += body * 0.85f;
            }
        }

        private static void HiHat(float[] data, int start, float level, float decay, System.Random random)
        {
            int length = (int)(SampleRate * 0.12f);
            float previous = 0f;

            for (int n = 0; n < length && start + n < data.Length; n++)
            {
                float t = n / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;

                // Différence de deux échantillons : un passe-haut grossier, qui garde le souffle
                // métallique et enlève le grave.
                float high = noise - previous;
                previous = noise;

                data[start + n] += high * level * Mathf.Exp(-t * decay);
            }
        }

        private static void Clap(float[] data, int start, System.Random random)
        {
            int length = (int)(SampleRate * 0.22f);
            float low = 0f;

            for (int n = 0; n < length && start + n < data.Length; n++)
            {
                float t = n / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;
                low += (noise - low) * 0.35f;
                float band = noise - low;

                // Trois claquements rapprochés puis la queue : c'est ce qui fait un « clap ».
                float envelope = Mathf.Exp(-t * 16f);
                if (t < 0.03f) envelope *= 0.6f + 0.4f * Mathf.Abs(Mathf.Sin(t * 330f));

                data[start + n] += band * 0.24f * envelope;
            }
        }

        private static void Bass(float[] data, int start, float frequency, int length)
        {
            float phase = 0f;
            float filtered = 0f;

            for (int n = 0; n < length && start + n < data.Length; n++)
            {
                float t = n / (float)SampleRate;
                phase += frequency / SampleRate;
                phase -= Mathf.Floor(phase);

                float saw = phase * 2f - 1f;
                float cutoff = 0.05f + 0.25f * Mathf.Exp(-t * 14f);
                filtered += (saw - filtered) * cutoff;

                float envelope = Mathf.Exp(-t * 5f) * Mathf.Clamp01(t * 400f);
                data[start + n] += filtered * 0.42f * envelope;
            }
        }

        private static void Pad(float[] data, int start, int length, float[] chord)
        {
            for (int v = 0; v < chord.Length; v++)
            {
                float phaseA = 0f;
                float phaseB = 0.37f;

                for (int n = 0; n < length && start + n < data.Length; n++)
                {
                    float t = n / (float)SampleRate;

                    // Deux oscillateurs légèrement désaccordés : c'est le battement entre eux
                    // qui rend la nappe vivante au lieu d'un sifflement.
                    phaseA += chord[v] / SampleRate;
                    phaseB += chord[v] * 1.004f / SampleRate;

                    float tone = Mathf.Sin(phaseA * 2f * Mathf.PI) + Mathf.Sin(phaseB * 2f * Mathf.PI);

                    // Fondu d'entrée et de sortie de chaque mesure : pas de clic aux jointures.
                    float fade = Mathf.Clamp01(t * 6f) * Mathf.Clamp01((length - n) / (float)SampleRate * 6f);
                    float tremolo = 0.75f + 0.25f * Mathf.Sin(t * 2f * Mathf.PI * 0.5f);

                    data[start + n] += tone * 0.022f * fade * tremolo;
                }
            }
        }

        private static float Tanh(float x)
        {
            float e = Mathf.Exp(2f * Mathf.Clamp(x, -8f, 8f));
            return (e - 1f) / (e + 1f);
        }
    }
}
