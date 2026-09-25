using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Serialization;

namespace UberBagarre.Story
{
    /// <summary>
    /// La « voix » des répliques : un babillage, pas des mots.
    ///
    /// Les voix de synthèse neuronale ont été abandonnées : une voix artificielle qui dit
    /// vraiment le texte tombe dans l'entre-deux — trop proche d'un humain pour être un style,
    /// trop loin pour être crue. Un babillage, lui, est franchement un choix : « bla-bla-bla »
    /// à la hauteur et au caractère de chaque personnage, comme dans beaucoup de jeux. Il suffit
    /// à rendre la scène vivante, et le sous-titre porte le sens.
    ///
    /// Le babillage n'est pas du bruit au hasard pour autant. Il SUIT le texte : une syllabe par
    /// groupe de voyelles, la voyelle de la syllabe tirée de la lettre écrite (a, é, i, o, ou…),
    /// la consonne d'attaque de celle qui la précède (b, d, g éclatent, m et n bourdonnent, l et r
    /// glissent, s et ch soufflent). Les mots sont séparés, la ponctuation marque des pauses, la
    /// phrase descend en finissant, une question remonte, une exclamation monte et tape plus fort.
    /// La même réplique donne toujours le même babillage : la graine vient du texte.
    ///
    /// Chaque personnage a son timbre : hauteur, débit, étendue de l'intonation, taille du
    /// « crâne » (les formants), souffle. Sami parle vite et haut, dans un combiné ; le Taureau
    /// grogne lentement ; l'application parle en bips.
    /// </summary>
    [DisallowMultipleComponent]
    public class DialogueVoice : MonoBehaviour
    {
        private const int SampleRate = 22050;

        [SerializeField, Range(0f, 1f), FormerlySerializedAs("_babbleVolume")]
        [Tooltip("Volume du babillage.")]
        private float _level = 0.5f;

        private static DialogueVoice _active;

        private AudioSource _source;
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

        private void OnDestroy()
        {
            foreach (AudioClip clip in _babbles.Values)
            {
                if (clip != null) Destroy(clip);
            }

            _babbles.Clear();
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

            if (!HasWords(text)) return 0f;

            AudioClip clip = Babble(speaker, text);
            if (clip == null) return 0f;

            _source.clip = clip;
            _source.volume = _level;
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

        /// <summary>Empreinte FNV-1a 32 bits de « PERSONNAGE|texte » : clé du cache et graine du babillage.</summary>
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

        private static bool HasWords(string text)
        {
            for (int i = 0; i < text.Length; i++)
            {
                if (char.IsLetterOrDigit(text[i])) return true;
            }

            return false;
        }

        // ------------------------------------------------------------------ personnages

        private struct Voice
        {
            public float Pitch;     // hauteur moyenne (Hz)
            public float Range;     // etendue de l'intonation (demi-tons)
            public float Rate;      // syllabes par seconde
            public float Size;      // formants : < 1 grand gabarit, > 1 plus petit
            public float Breath;    // souffle mele a la voix
            public float Growl;     // sous-harmonique : la voix grogne
            public bool Phone;      // passee dans un combine
            public bool Robot;      // des bips, pas une voix
        }

        private static Voice VoiceOf(string speaker)
        {
            Voice v = new Voice { Pitch = 118f, Range = 3.5f, Rate = 9.5f, Size = 1f, Breath = 0.06f };

            switch ((speaker ?? string.Empty).ToUpperInvariant())
            {
                case "MOI":
                    break;

                case "SAMI":
                    v.Pitch = 150f; v.Range = 5.5f; v.Rate = 11.5f; v.Size = 1.05f; v.Phone = true;
                    break;

                case "APPLI":
                    v.Pitch = 330f; v.Range = 0f; v.Rate = 11f; v.Size = 1.15f; v.Breath = 0f; v.Robot = true; v.Phone = true;
                    break;

                case "DRAGAN":
                    v.Pitch = 92f; v.Range = 2.5f; v.Rate = 8f; v.Size = 0.93f; v.Breath = 0.12f; v.Growl = 0.1f;
                    break;

                case "MILAN":
                    v.Pitch = 112f; v.Range = 5f; v.Rate = 10.5f; v.Size = 0.98f;
                    break;

                case "LE TAUREAU":
                    v.Pitch = 76f; v.Range = 2f; v.Rate = 7f; v.Size = 0.86f; v.Breath = 0.14f; v.Growl = 0.3f;
                    break;

                case "BRUNO MORETTI":
                    v.Pitch = 100f; v.Range = 3f; v.Rate = 8.5f; v.Size = 0.94f; v.Breath = 0.09f; v.Growl = 0.06f;
                    break;

                default:
                    // Un personnage non prevu : un timbre stable tire de son nom.
                    int h = Key(speaker, string.Empty).GetHashCode() & 0x7fffffff;
                    v.Pitch = 95f + h % 90;
                    v.Rate = 8.5f + (h / 90) % 4;
                    v.Size = 0.92f + ((h / 7) % 20) * 0.01f;
                    break;
            }

            return v;
        }

        // ------------------------------------------------------------------ lecture du texte

        private enum Onset
        {
            Soft,
            Labial,
            Dental,
            Velar,
            Nasal,
            Liquid,
            Hiss,
            Hush
        }

        private struct Syllable
        {
            public Onset Onset;
            public int Vowel;
            public bool WordEnd;
            public float Pause;
        }

        // F1, F2, F3 d'une voix d'homme : a, é, è, i, o, ou, u, eu.
        private static readonly Vector3[] Vowels =
        {
            new Vector3(730f, 1090f, 2440f),
            new Vector3(400f, 2000f, 2550f),
            new Vector3(550f, 1770f, 2490f),
            new Vector3(280f, 2250f, 2890f),
            new Vector3(470f, 820f, 2600f),
            new Vector3(310f, 870f, 2250f),
            new Vector3(260f, 1750f, 2150f),
            new Vector3(380f, 1450f, 2300f),
        };

        private const int VowelA = 0, VowelE = 1, VowelEh = 2, VowelI = 3, VowelO = 4, VowelOu = 5, VowelU = 6, VowelEu = 7;

        /// <summary>Découpe une phrase en syllabes approximatives, à la française.</summary>
        private static void Parse(string sentence, List<Syllable> output)
        {
            string text = sentence.ToLowerInvariant();
            Onset onset = Onset.Soft;
            bool inVowel = false;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                char next = i + 1 < text.Length ? text[i + 1] : ' ';

                int vowel = VowelOf(c, next);

                if (vowel >= 0)
                {
                    if (inVowel) continue;
                    inVowel = true;

                    // Le « e » muet de fin de mot ne se prononce pas (« table », « mangent »).
                    if (c == 'e' && IsSilentE(text, i) && output.Count > 0 && !output[output.Count - 1].WordEnd)
                    {
                        continue;
                    }

                    output.Add(new Syllable { Onset = onset, Vowel = vowel });
                    onset = Onset.Soft;
                    continue;
                }

                inVowel = false;

                if (char.IsLetter(c))
                {
                    onset = OnsetOf(c, next);
                }
                else if (char.IsDigit(c))
                {
                    output.Add(new Syllable { Onset = Onset.Dental, Vowel = VowelEh });
                }
                else if (output.Count > 0)
                {
                    Syllable last = output[output.Count - 1];
                    last.WordEnd = true;

                    if (c == ',' || c == ';' || c == ':') last.Pause = Mathf.Max(last.Pause, 0.13f);
                    else if (c == '.' || c == '!' || c == '?' || c == '…') last.Pause = Mathf.Max(last.Pause, 0.2f);
                    else last.Pause = Mathf.Max(last.Pause, 0.035f);

                    output[output.Count - 1] = last;
                    onset = Onset.Soft;
                }
            }

            if (output.Count > 0)
            {
                Syllable last = output[output.Count - 1];
                last.WordEnd = true;
                output[output.Count - 1] = last;
            }
        }

        private static int VowelOf(char c, char next)
        {
            switch (c)
            {
                case 'a': case 'à': case 'â': return next == 'i' ? VowelEh : next == 'u' ? VowelO : VowelA;
                case 'é': return VowelE;
                case 'è': case 'ê': case 'ë': return VowelEh;
                case 'e': return next == 'u' ? VowelEu : next == 'a' ? VowelO : next == 'i' ? VowelEh : VowelEu;
                case 'i': case 'î': case 'ï': case 'y': return VowelI;
                case 'o': case 'ô': return next == 'u' ? VowelOu : next == 'i' ? VowelA : VowelO;
                case 'u': case 'ù': case 'û': case 'ü': return VowelU;
                case 'œ': return VowelEu;
                default: return -1;
            }
        }

        private static Onset OnsetOf(char c, char next)
        {
            switch (c)
            {
                case 'b': case 'p': return Onset.Labial;
                case 'd': case 't': return Onset.Dental;
                case 'g': case 'k': case 'q': return Onset.Velar;
                case 'c': return next == 'h' ? Onset.Hush : (next == 'e' || next == 'i' ? Onset.Hiss : Onset.Velar);
                case 'm': case 'n': return Onset.Nasal;
                case 'l': case 'r': case 'w': return Onset.Liquid;
                case 's': case 'z': case 'x': case 'f': case 'ç': return Onset.Hiss;
                case 'j': case 'v': return Onset.Hush;
                default: return Onset.Soft;
            }
        }

        private static bool IsSilentE(string text, int index)
        {
            int end = index + 1;
            while (end < text.Length && char.IsLetter(text[end])) end++;

            string tail = text.Substring(index + 1, end - index - 1);
            return tail.Length == 0 || tail == "s" || tail == "nt";
        }

        // ------------------------------------------------------------------ synthese

        private AudioClip Babble(string speaker, string text)
        {
            string key = Key(speaker, text);

            AudioClip cached;
            if (_babbles.TryGetValue(key, out cached)) return cached;

            Voice voice = VoiceOf(speaker);
            System.Random random = new System.Random(key.GetHashCode());
            List<float> samples = new List<float>(SampleRate * 3);
            List<Syllable> syllables = new List<Syllable>(64);

            // Phrase par phrase : chacune a sa propre ligne melodique.
            int start = 0;
            for (int i = 0; i <= text.Length; i++)
            {
                bool end = i == text.Length || text[i] == '.' || text[i] == '!' || text[i] == '?' || text[i] == '…';
                if (!end) continue;

                int stop = i;
                while (stop + 1 < text.Length && (text[stop + 1] == '.' || text[stop + 1] == '!' || text[stop + 1] == '?')) stop++;

                string sentence = text.Substring(start, Mathf.Min(text.Length, stop + 1) - start);
                char mark = i < text.Length ? text[i] : '.';
                start = stop + 1;
                i = stop;

                syllables.Clear();
                Parse(sentence, syllables);
                if (syllables.Count == 0) continue;

                Sentence(samples, syllables, voice, random, mark == '?', mark == '!',
                    sentence.Contains("...") || sentence.Contains("…"));
            }

            if (samples.Count == 0) return null;

            float[] data = samples.ToArray();
            if (voice.Phone) PhoneFilter(data, voice.Robot);

            float peak = 0.0001f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            for (int i = 0; i < data.Length; i++) data[i] = data[i] / peak * 0.72f;

            AudioClip clip = AudioClip.Create("Babillage " + key, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            _babbles[key] = clip;
            return clip;
        }

        private static void Sentence(List<float> samples, List<Syllable> syllables, Voice voice,
            System.Random random, bool question, bool exclaim, bool trailing)
        {
            int count = syllables.Count;
            float rate = voice.Rate * (trailing ? 0.8f : 1f) * (exclaim ? 1.08f : 1f);

            for (int i = 0; i < count; i++)
            {
                Syllable syllable = syllables[i];
                float progress = count > 1 ? i / (float)(count - 1) : 0f;

                // La ligne melodique : on part un peu haut, on descend en finissant ; une question
                // remonte sur ses deux dernieres syllabes ; un mot sur trois porte un accent.
                float semitones = Mathf.Lerp(1.5f, -2.5f, progress);
                semitones += ((float)random.NextDouble() * 2f - 1f) * voice.Range * 0.5f;
                if (syllable.WordEnd && random.NextDouble() < 0.33) semitones += voice.Range * 0.45f;
                if (question && i >= count - 2) semitones += 4f + (i == count - 1 ? 3f : 0f);
                if (exclaim) semitones += 2f;
                if (trailing && progress > 0.6f) semitones -= 1.5f;

                float pitch = voice.Pitch * Mathf.Pow(2f, semitones / 12f);

                // Les bips de l'appli restent sur une gamme : c'est ce qui les rend « machine ».
                if (voice.Robot) pitch = voice.Pitch * Mathf.Pow(2f, (float)Quantize(random) / 12f);

                float duration = 1f / rate * (0.78f + (float)random.NextDouble() * 0.44f);
                if (syllable.WordEnd) duration *= 1.12f;

                float loudness = (exclaim ? 1.15f : 1f) * (trailing ? 0.8f : 1f) * (0.85f + (float)random.NextDouble() * 0.2f);

                RenderSyllable(samples, voice, syllable, pitch, duration, loudness, random);

                float pause = syllable.Pause;
                if (syllable.WordEnd && pause < 0.03f) pause = 0.03f;
                if (i == count - 1) pause = Mathf.Max(pause, trailing ? 0.35f : 0.18f);

                AddSilence(samples, pause * (0.8f + (float)random.NextDouble() * 0.4f));
            }
        }

        private static int Quantize(System.Random random)
        {
            int[] scale = { 0, 3, 5, 7, 10, 12 };
            return scale[random.Next(scale.Length)];
        }

        private static void RenderSyllable(List<float> samples, Voice voice, Syllable syllable, float pitch,
            float duration, float loudness, System.Random random)
        {
            Vector3 formants = Vowels[syllable.Vowel] * voice.Size;
            int total = Mathf.RoundToInt(duration * SampleRate);

            // --- l'attaque
            int onsetLength = 0;
            float glide = 0f;

            switch (syllable.Onset)
            {
                case Onset.Labial:
                case Onset.Dental:
                case Onset.Velar:
                {
                    // Occlusion, puis eclatement : un petit bruit filtre a la frequence du lieu
                    // d'articulation (levres graves, dents aigues, gorge au milieu).
                    float place = syllable.Onset == Onset.Labial ? 900f : syllable.Onset == Onset.Dental ? 3600f : 1900f;
                    AddSilence(samples, 0.012f);
                    Burst(samples, place, 0.009f, 0.55f * loudness, random);
                    onsetLength = Mathf.RoundToInt(0.021f * SampleRate);
                    glide = 0.03f;
                    break;
                }

                case Onset.Hiss:
                    Noise(samples, 5200f, 0.05f, 0.28f * loudness, random);
                    onsetLength = Mathf.RoundToInt(0.05f * SampleRate);
                    break;

                case Onset.Hush:
                    Noise(samples, 2600f, 0.045f, 0.3f * loudness, random);
                    onsetLength = Mathf.RoundToInt(0.045f * SampleRate);
                    break;

                case Onset.Liquid:
                    glide = 0.045f;
                    break;
            }

            int length = Mathf.Max(Mathf.RoundToInt(0.05f * SampleRate), total - onsetLength);

            // --- la voyelle (et le murmure d'un m ou d'un n avant elle)
            int nasal = syllable.Onset == Onset.Nasal ? Mathf.RoundToInt(0.035f * SampleRate) : 0;
            int glideLength = Mathf.RoundToInt(glide * SampleRate);

            Resonator f1 = new Resonator();
            Resonator f2 = new Resonator();
            Resonator f3 = new Resonator();

            float phase = 0f;
            float sub = 0f;
            float soft = 0f;
            float vibrato = (float)random.NextDouble() * 6.28f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)length;

                // Formants : un m/n bourdonne bas avant la voyelle ; une consonne glisse vers elle.
                if ((n & 15) == 0)
                {
                    Vector3 target = formants;

                    if (n < nasal)
                    {
                        target = new Vector3(260f, 1150f, 2400f) * voice.Size;
                    }
                    else if (n < nasal + glideLength)
                    {
                        float k = (n - nasal) / (float)Mathf.Max(1, glideLength);
                        Vector3 from = syllable.Onset == Onset.Liquid
                            ? new Vector3(360f, 1100f, 2400f) * voice.Size
                            : new Vector3(formants.x * 0.55f, formants.y * 0.9f, formants.z);
                        target = Vector3.Lerp(from, formants, k * k * (3f - 2f * k));
                    }

                    f1.Tune(target.x, 80f);
                    f2.Tune(target.y, 110f);
                    f3.Tune(target.z, 170f);
                }

                float f0 = pitch * (1f - t * 0.06f) * (1f + Mathf.Sin(vibrato + n * 0.0019f) * 0.01f);
                phase += f0 / SampleRate;
                phase -= Mathf.Floor(phase);

                float source;
                if (voice.Robot)
                {
                    source = phase < 0.5f ? 1f : -1f;
                }
                else
                {
                    // Dent de scie adoucie : la glotte ne produit pas d'arete parfaite.
                    float saw = phase * 2f - 1f;
                    soft += (saw - soft) * 0.4f;
                    source = soft + ((float)random.NextDouble() * 2f - 1f) * voice.Breath;
                }

                if (voice.Growl > 0f)
                {
                    sub += f0 * 0.5f / SampleRate;
                    sub -= Mathf.Floor(sub);
                    source += (sub < 0.5f ? 1f : -1f) * voice.Growl;
                }

                float output = n < nasal
                    ? f1.Process(source) * 0.6f
                    : f1.Process(source) + f2.Process(source) * 0.55f + f3.Process(source) * 0.22f;

                // Enveloppe : attaque breve, chute plus lente, un peu moins fort pendant le murmure.
                float attack = Mathf.Clamp01(n / (0.012f * SampleRate));
                float release = Mathf.Clamp01((length - n) / (0.03f * SampleRate));
                float level = attack * release * (n < nasal ? 0.55f : 1f) * loudness;

                samples.Add(output * level);
            }
        }

        private static void Burst(List<float> samples, float frequency, float seconds, float level, System.Random random)
        {
            int length = Mathf.RoundToInt(seconds * SampleRate);
            Resonator r = new Resonator();
            r.Tune(frequency, 900f);

            for (int n = 0; n < length; n++)
            {
                float decay = 1f - n / (float)length;
                samples.Add(r.Process((float)random.NextDouble() * 2f - 1f) * decay * level);
            }
        }

        private static void Noise(List<float> samples, float frequency, float seconds, float level, System.Random random)
        {
            int length = Mathf.RoundToInt(seconds * SampleRate);
            Resonator r = new Resonator();
            r.Tune(frequency, 1600f);

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)length;
                float envelope = Mathf.Sin(Mathf.PI * t);
                samples.Add(r.Process((float)random.NextDouble() * 2f - 1f) * envelope * level);
            }
        }

        private static void AddSilence(List<float> samples, float seconds)
        {
            int length = Mathf.RoundToInt(seconds * SampleRate);
            for (int i = 0; i < length; i++) samples.Add(0f);
        }

        /// <summary>Combiné téléphonique : bande étroite et légère saturation ; plus crue pour les bips.</summary>
        private static void PhoneFilter(float[] data, bool crush)
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
                y = y / (1f + Mathf.Abs(y));

                // Quantification grossiere : le petit haut-parleur d'un telephone bon marche.
                if (crush) y = Mathf.Round(y * 10f) / 10f;

                data[i] = y;
            }
        }

        /// <summary>Résonateur du second ordre, réaccordable : un formant qui peut glisser.</summary>
        private struct Resonator
        {
            private float _a1;
            private float _a2;
            private float _gain;
            private float _y1;
            private float _y2;

            public void Tune(float frequency, float bandwidth)
            {
                frequency = Mathf.Clamp(frequency, 50f, SampleRate * 0.45f);
                float r = Mathf.Exp(-Mathf.PI * bandwidth / SampleRate);
                float theta = 2f * Mathf.PI * frequency / SampleRate;

                _a1 = 2f * r * Mathf.Cos(theta);
                _a2 = -r * r;

                // Gain unitaire au sommet de la resonance, quelle que soit la frequence : sans
                // ca, un « i » (premier formant tres bas) sortait cinq fois plus fort qu'un « a ».
                _gain = (1f - r) * Mathf.Sqrt(1f - 2f * r * Mathf.Cos(2f * theta) + r * r);
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
