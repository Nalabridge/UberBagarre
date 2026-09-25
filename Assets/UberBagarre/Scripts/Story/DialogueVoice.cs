using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.Story
{
    /// <summary>
    /// La voix des répliques : chaque sous-titre est DIT, pas seulement écrit.
    ///
    /// Les répliques fixes du scénario ont été enregistrées à l'avance par une voix de synthèse
    /// neuronale (voir Tools/generer_voix.py) : une voix d'homme pour le personnage, plus aiguë
    /// et passée dans un combiné téléphonique pour Sami, plus grave pour les frères Kovac et le
    /// Taureau, une voix de femme pour l'application. Chaque fichier est retrouvé par l'empreinte
    /// de « PERSONNAGE|texte » : changer une réplique dans le code la rend muette jusqu'à ce que
    /// le script soit relancé, jamais fausse.
    ///
    /// Les répliques calculées en jeu (une somme d'argent, un compte de photos) n'ont pas de
    /// fichier. Elles sont BABILLÉES : des syllabes synthétisées à la hauteur de voix du
    /// personnage, au rythme du texte. Un jeu où l'on parle en silence paraît en panne ; un
    /// babillage, lui, se lit comme un choix de style.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueVoice : MonoBehaviour
    {
        private const int SampleRate = 22050;

        [SerializeField, Range(0f, 1f)] private float _volume = 0.95f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Volume du babillage de secours (repliques sans enregistrement).")]
        private float _babbleVolume = 0.45f;

        [SerializeField] private string _resourceFolder = "Voix";

        private static DialogueVoice _active;

        private AudioSource _source;
        private readonly Dictionary<string, AudioClip> _loaded = new Dictionary<string, AudioClip>();
        private readonly Dictionary<string, AudioClip> _babbles = new Dictionary<string, AudioClip>();

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();

            // Voix « dans la tete » du joueur : non spatialisee. Les personnages sont toujours
            // a quelques metres, et une voix qui tourne avec la camera fatigue vite.
            _source.playOnAwake = false;
            _source.loop = false;
            _source.spatialBlend = 0f;
            _source.priority = 16;
        }

        private void OnEnable()
        {
            _active = this;
        }

        private void OnDisable()
        {
            if (_active == this) _active = null;
        }

        /// <summary>Un personnage parle-t-il en ce moment ? Les ambiances s'effacent pendant ce temps.</summary>
        public static bool AnySpeaking
        {
            get { return _active != null && _active.IsSpeaking; }
        }

        /// <summary>
        /// Dit une réplique. Renvoie sa durée en secondes, pour que le sous-titre reste affiché
        /// au moins aussi longtemps que la voix parle.
        /// </summary>
        public float Speak(string speaker, string text)
        {
            if (_source == null || string.IsNullOrEmpty(text)) return 0f;

            _source.Stop();

            AudioClip clip = Recorded(speaker, text);
            float volume = _volume;

            if (clip == null)
            {
                if (!HasWords(text)) return 0f;

                clip = Babble(speaker, text);
                volume = _babbleVolume;
            }

            if (clip == null) return 0f;

            _source.clip = clip;
            _source.volume = volume;
            _source.Play();
            return clip.length;
        }

        public void Stop()
        {
            if (_source != null) _source.Stop();
        }

        public bool IsSpeaking
        {
            get { return _source != null && _source.isPlaying; }
        }

        /// <summary>
        /// Empreinte FNV-1a 32 bits sur les unités UTF-16 de « PERSONNAGE|texte ». Doit rester
        /// IDENTIQUE à celle du script de génération : c'est le seul lien entre les deux.
        /// </summary>
        public static string Key(string speaker, string text)
        {
            string value = (speaker ?? string.Empty).ToUpperInvariant() + "|" + (text ?? string.Empty);

            uint hash = 0x811C9DC5;
            for (int i = 0; i < value.Length; i++)
            {
                hash ^= value[i];
                hash *= 0x01000193;
            }

            return "v_" + hash.ToString("x8");
        }

        private AudioClip Recorded(string speaker, string text)
        {
            string key = Key(speaker, text);

            AudioClip clip;
            if (_loaded.TryGetValue(key, out clip)) return clip;

            clip = Resources.Load<AudioClip>(_resourceFolder + "/" + key);
            _loaded[key] = clip;
            return clip;
        }

        private static bool HasWords(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLetterOrDigit(text[i])) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ babillage

        /// <summary>
        /// Syllabes synthétisées : une source glottale (dent de scie à la hauteur du personnage,
        /// légèrement irrégulière) filtrée par deux formants de voyelle tirés au hasard, avec une
        /// attaque et une chute pour chaque syllabe, et des silences sur la ponctuation.
        /// La même réplique donne toujours le même babillage : la graine vient du texte.
        /// </summary>
        private AudioClip Babble(string speaker, string text)
        {
            string key = Key(speaker, text);

            AudioClip cached;
            if (_babbles.TryGetValue(key, out cached)) return cached;

            float pitch;
            bool phone;
            VoiceOf(speaker, out pitch, out phone);

            System.Random random = new System.Random(key.GetHashCode());
            List<float> samples = new List<float>(SampleRate * 3);

            int letters = 0;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                if (char.IsLetterOrDigit(c))
                {
                    letters++;
                    if (letters % 3 != 0) continue;

                    AddSyllable(samples, random, pitch);
                }
                else if (c == '.' || c == '?' || c == '!')
                {
                    AddSilence(samples, 0.22f);
                }
                else if (c == ',' || c == ':' || c == ';')
                {
                    AddSilence(samples, 0.12f);
                }
            }

            if (samples.Count == 0) return null;

            float[] data = samples.ToArray();
            if (phone) PhoneFilter(data);

            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * 0.7f;

            AudioClip clip = AudioClip.Create("Babillage " + key, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            _babbles[key] = clip;
            return clip;
        }

        private static void VoiceOf(string speaker, out float pitch, out bool phone)
        {
            phone = false;

            switch ((speaker ?? string.Empty).ToUpperInvariant())
            {
                case "APPLI": pitch = 205f; phone = true; break;
                case "SAMI": pitch = 138f; phone = true; break;
                case "DRAGAN": pitch = 96f; break;
                case "MILAN": pitch = 112f; break;
                case "LE TAUREAU": pitch = 84f; break;
                default: pitch = 118f; break;
            }
        }

        private static readonly Vector2[] Vowels =
        {
            new Vector2(730f, 1090f), new Vector2(530f, 1840f), new Vector2(390f, 2300f),
            new Vector2(570f, 840f), new Vector2(300f, 870f), new Vector2(440f, 1020f)
        };

        private static void AddSyllable(List<float> samples, System.Random random, float basePitch)
        {
            float duration = 0.085f + (float)random.NextDouble() * 0.07f;
            int length = Mathf.RoundToInt(duration * SampleRate);

            Vector2 vowel = Vowels[random.Next(Vowels.Length)];
            float pitch = basePitch * (0.9f + (float)random.NextDouble() * 0.25f);

            Resonator a = new Resonator(vowel.x, 80f);
            Resonator b = new Resonator(vowel.y, 120f);

            float phase = 0f;
            for (int n = 0; n < length; n++)
            {
                float t = n / (float)length;
                phase += pitch * (1f - t * 0.08f) / SampleRate;
                phase -= Mathf.Floor(phase);

                float source = phase * 2f - 1f;

                // Consonne d'attaque : un souffle court avant la voyelle.
                if (t < 0.12f) source = source * 0.3f + ((float)random.NextDouble() * 2f - 1f) * 0.6f;

                float envelope = Mathf.Sin(Mathf.PI * Mathf.Clamp01(t)) * (t < 0.12f ? 0.6f : 1f);
                samples.Add((a.Process(source) + b.Process(source) * 0.6f) * envelope * 0.5f);
            }

            AddSilence(samples, 0.015f + (float)random.NextDouble() * 0.03f);
        }

        private static void AddSilence(List<float> samples, float seconds)
        {
            int length = Mathf.RoundToInt(seconds * SampleRate);
            for (int i = 0; i < length; i++) samples.Add(0f);
        }

        /// <summary>Combiné téléphonique : bande étroite et légère saturation.</summary>
        private static void PhoneFilter(float[] data)
        {
            float low = 0f;
            float high = 0f;
            float previous = 0f;

            for (int i = 0; i < data.Length; i++)
            {
                float x = data[i];
                high = 0.92f * (high + x - previous);
                previous = x;
                low += (high - low) * 0.45f;

                float y = low * 2f;
                data[i] = y / (1f + Mathf.Abs(y));
            }
        }

        /// <summary>Résonateur du second ordre : un formant de voyelle.</summary>
        private struct Resonator
        {
            private readonly float _a1;
            private readonly float _a2;
            private readonly float _gain;
            private float _y1;
            private float _y2;

            public Resonator(float frequency, float bandwidth)
            {
                float r = Mathf.Exp(-Mathf.PI * bandwidth / SampleRate);
                float theta = 2f * Mathf.PI * frequency / SampleRate;

                _a1 = 2f * r * Mathf.Cos(theta);
                _a2 = -r * r;
                _gain = 1f - r;
                _y1 = 0f;
                _y2 = 0f;
            }

            public float Process(float x)
            {
                float y = _gain * x + _a1 * _y1 + _a2 * _y2;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }
    }
}
