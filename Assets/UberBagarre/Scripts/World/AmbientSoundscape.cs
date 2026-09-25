using System.Collections.Generic;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le fond sonore d'un lieu : ce qu'on entend quand personne ne parle et que rien ne cogne.
    ///
    /// Un lieu silencieux se lit comme un décor en carton : on remarque le silence avant de
    /// remarquer le décor. Chaque lieu a donc sa « couche » continue et ses événements rares :
    ///
    /// - **Maison** : le ronron de la pièce, la pluie étouffée contre la vitre, la circulation
    ///   à travers les murs ; de temps en temps une voiture passe dehors, une sirène au loin.
    /// - **Rue** : la rumeur de la ville, la bruine et ses gouttes, le chuintement des pneus sur
    ///   le bitume mouillé ; un klaxon, un « pin-pon » qui traverse le quartier.
    /// - **Parking** : le vent, la ville plus loin ; des gouttes qui tombent et résonnent.
    ///
    /// Et des sources PONCTUELLES, spatialisées, posées dans le décor : le frigo qui ronronne,
    /// l'horloge de la cuisine, un néon qui grésille, la basse du club qui passe à travers la
    /// façade. Ce sont elles qui font qu'on « s'approche » de quelque chose.
    ///
    /// Tout est synthétisé au premier passage dans le lieu, comme le reste de l'audio du
    /// projet, puis gardé en mémoire : revenir dans un lieu ne recalcule rien. Le composant vit
    /// sous la racine du lieu : quand le lieu s'éteint, son ambiance s'éteint avec lui.
    ///
    /// Quand un personnage parle, l'ambiance s'efface un peu — la pluie ne doit jamais couvrir
    /// une réplique.
    /// </summary>
    [DisallowMultipleComponent]
    public class AmbientSoundscape : MonoBehaviour
    {
        public enum Kind
        {
            Maison,
            Rue,
            Parking,
            Frigo,
            Horloge,
            BasseDuClub,
            Neon
        }

        private const int SampleRate = 22050;

        [SerializeField] private Kind _kind = Kind.Rue;

        [SerializeField, Range(0f, 1f)] private float _volume = 0.35f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Volume des evenements ponctuels (voiture qui passe, sirene, goutte).")]
        private float _eventVolume = 0.4f;

        [SerializeField]
        [Tooltip("Intervalle aleatoire entre deux evenements, en secondes (min, max).")]
        private Vector2 _eventInterval = new Vector2(7f, 18f);

        [SerializeField, Min(0.05f)] private float _fadeIn = 1.5f;

        [Header("Sources ponctuelles")]
        [SerializeField, Min(0.1f)] private float _minDistance = 1f;
        [SerializeField, Min(0.5f)] private float _maxDistance = 12f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du volume retiree pendant qu'un personnage parle.")]
        private float _dialogueDucking = 0.45f;

        private static readonly Dictionary<Kind, AudioClip> Loops = new Dictionary<Kind, AudioClip>();
        private static readonly Dictionary<Kind, AudioClip[]> Events = new Dictionary<Kind, AudioClip[]>();

        private AudioSource _bed;
        private AudioSource _oneShots;
        private float _fade;
        private float _duck;
        private float _nextEvent;
        private AudioClip[] _events;

        public Kind Type
        {
            get { return _kind; }
        }

        public bool IsPoint
        {
            get { return _kind >= Kind.Frigo; }
        }

        private void Awake()
        {
            _bed = CreateSource(true);
            _oneShots = CreateSource(false);
            _oneShots.spatialBlend = 0f;
        }

        private AudioSource CreateSource(bool loop)
        {
            AudioSource source = gameObject.AddComponent<AudioSource>();
            source.playOnAwake = false;
            source.loop = loop;
            source.dopplerLevel = 0f;
            source.priority = 200;

            if (IsPoint)
            {
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = _minDistance;
                source.maxDistance = Mathf.Max(_minDistance + 0.5f, _maxDistance);
            }
            else
            {
                source.spatialBlend = 0f;
            }

            return source;
        }

        private void OnEnable()
        {
            AudioClip loop = Loop(_kind);
            _events = EventsOf(_kind);

            _fade = 0f;
            _bed.clip = loop;
            _bed.volume = 0f;

            if (loop != null)
            {
                // Chaque source démarre ailleurs dans sa boucle : deux lieux visités à la suite
                // ne recommencent pas sur la même goutte.
                _bed.timeSamples = Random.Range(0, loop.samples);
                _bed.Play();
            }

            ScheduleNextEvent(0.4f);
        }

        private void OnDisable()
        {
            if (_bed != null) _bed.Stop();
            if (_oneShots != null) _oneShots.Stop();
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _fade = Mathf.MoveTowards(_fade, 1f, dt / _fadeIn);

            float duckTarget = DialogueVoice.AnySpeaking ? _dialogueDucking : 0f;
            _duck = Mathf.MoveTowards(_duck, duckTarget, dt * 2.5f);

            float gain = _fade * (1f - _duck);
            _bed.volume = _volume * gain;

            if (_events == null || _events.Length == 0) return;
            if (Time.unscaledTime < _nextEvent) return;

            AudioClip clip = _events[Random.Range(0, _events.Length)];

            _oneShots.panStereo = Random.Range(-0.75f, 0.75f);
            _oneShots.pitch = Random.Range(0.94f, 1.06f);
            _oneShots.PlayOneShot(clip, _eventVolume * gain * Random.Range(0.6f, 1f));

            ScheduleNextEvent(1f);
        }

        private void ScheduleNextEvent(float scale)
        {
            float min = Mathf.Max(1f, Mathf.Min(_eventInterval.x, _eventInterval.y));
            float max = Mathf.Max(min, Mathf.Max(_eventInterval.x, _eventInterval.y));
            _nextEvent = Time.unscaledTime + Random.Range(min, max) * scale;
        }

        // ------------------------------------------------------------------ banque de sons

        private static AudioClip Loop(Kind kind)
        {
            AudioClip clip;
            if (Loops.TryGetValue(kind, out clip) && clip != null) return clip;

            System.Random random = new System.Random(1000 + (int)kind * 37);

            switch (kind)
            {
                case Kind.Maison: clip = HouseLoop(random); break;
                case Kind.Rue: clip = StreetLoop(random); break;
                case Kind.Parking: clip = ParkingLoop(random); break;
                case Kind.Frigo: clip = FridgeLoop(random); break;
                case Kind.Horloge: clip = ClockLoop(random); break;
                case Kind.BasseDuClub: clip = ClubBassLoop(); break;
                case Kind.Neon: clip = NeonLoop(random); break;
                default: clip = null; break;
            }

            Loops[kind] = clip;
            return clip;
        }

        private static AudioClip[] EventsOf(Kind kind)
        {
            AudioClip[] clips;
            if (Events.TryGetValue(kind, out clips) && clips != null && AllAlive(clips)) return clips;

            System.Random random = new System.Random(2000 + (int)kind * 53);

            switch (kind)
            {
                case Kind.Maison:
                    clips = new[]
                    {
                        CarPass("Voiture dehors", random, 480f, 0f),
                        CarPass("Voiture dehors 2", random, 380f, 0f),
                        Siren("Sirene lointaine", random, 520f)
                    };
                    break;

                case Kind.Rue:
                    clips = new[]
                    {
                        CarPass("Voiture", random, 5200f, 0.45f),
                        CarPass("Voiture 2", random, 3800f, 0.4f),
                        CarPass("Scooter", random, 6500f, 0.3f),
                        Siren("Sirene", random, 1900f),
                        Horn("Klaxon", random)
                    };
                    break;

                case Kind.Parking:
                    clips = new[]
                    {
                        Drip("Goutte", random), Drip("Goutte 2", random), Drip("Goutte 3", random),
                        CarPass("Voiture au loin", random, 1100f, 0.1f),
                        Siren("Sirene au loin", random, 1200f)
                    };
                    break;

                default:
                    clips = new AudioClip[0];
                    break;
            }

            Events[kind] = clips;
            return clips;
        }

        private static bool AllAlive(AudioClip[] clips)
        {
            for (int i = 0; i < clips.Length; i++)
            {
                if (clips[i] == null) return false;
            }

            return true;
        }

        // ------------------------------------------------------------------ couches continues

        /// <summary>Maison : pièce calme, pluie étouffée contre la vitre, ville à travers les murs.</summary>
        private static AudioClip HouseLoop(System.Random random)
        {
            float[] data = Buffer(12f);

            Lowpass room = new Lowpass(240f);
            Lowpass traffic = new Lowpass(95f);
            Lowpass rainLow = new Lowpass(850f);
            Lowpass rainHigh = new Lowpass(160f);
            float brown = 0f;

            Swells swells = new Swells(random, data.Length, 5, 1.6f, 3.2f);

            for (int n = 0; n < data.Length; n++)
            {
                float white = White(random);
                brown = brown * 0.995f + white * 0.05f;

                float rain = rainLow.Process(white);
                rain -= rainHigh.Process(rain);

                data[n] = room.Process(brown) * 0.35f
                          + traffic.Process(brown) * (0.4f + 0.8f * swells.At(n))
                          + rain * 0.55f;
            }

            // Gouttes contre la vitre : des « tics » brefs et sourds.
            AddTaps(data, random, 7f, 1500f, 0.012f, 0.18f);

            return Finish("Ambiance maison (generee)", data, 0.55f);
        }

        /// <summary>Rue : rumeur de la ville, bruine, gouttes, vent entre les façades.</summary>
        private static AudioClip StreetLoop(System.Random random)
        {
            float[] data = Buffer(16f);

            Lowpass rumble = new Lowpass(170f);
            Lowpass rainTop = new Lowpass(6200f);
            Lowpass rainBottom = new Lowpass(1200f);
            Bandpass wind = Bandpass.Create(420f, 0.8f);
            float brown = 0f;

            Swells swells = new Swells(random, data.Length, 7, 1.2f, 2.8f);
            Swells gusts = new Swells(random, data.Length, 3, 2.5f, 4.5f);

            for (int n = 0; n < data.Length; n++)
            {
                float white = White(random);
                brown = brown * 0.996f + white * 0.05f;

                float rain = rainTop.Process(white);
                rain -= rainBottom.Process(rain);

                data[n] = rumble.Process(brown) * (0.55f + 0.9f * swells.At(n))
                          + rain * 0.5f
                          + wind.Process(white) * (0.05f + 0.12f * gusts.At(n));
            }

            AddTaps(data, random, 22f, 3200f, 0.006f, 0.09f);

            return Finish("Ambiance rue (generee)", data, 0.6f);
        }

        /// <summary>Parking : plein air, vent, la ville plus loin.</summary>
        private static AudioClip ParkingLoop(System.Random random)
        {
            float[] data = Buffer(14f);

            Lowpass rumble = new Lowpass(130f);
            Bandpass wind = Bandpass.Create(300f, 0.7f);
            Bandpass whistle = Bandpass.Create(900f, 6f);
            float brown = 0f;

            Swells swells = new Swells(random, data.Length, 4, 1.8f, 3.5f);
            Swells gusts = new Swells(random, data.Length, 4, 2f, 4f);

            for (int n = 0; n < data.Length; n++)
            {
                float white = White(random);
                brown = brown * 0.996f + white * 0.05f;

                float gust = gusts.At(n);

                data[n] = rumble.Process(brown) * (0.45f + 0.6f * swells.At(n))
                          + wind.Process(white) * (0.12f + 0.3f * gust)
                          + whistle.Process(white) * 0.05f * gust * gust;
            }

            AddTaps(data, random, 1.5f, 1900f, 0.02f, 0.25f);

            return Finish("Ambiance parking (generee)", data, 0.55f);
        }

        /// <summary>Frigo : bourdonnement du compresseur (50 Hz et harmoniques) et léger souffle.</summary>
        private static AudioClip FridgeLoop(System.Random random)
        {
            float[] data = Buffer(4f);

            Lowpass hiss = new Lowpass(700f);

            for (int n = 0; n < data.Length; n++)
            {
                float t = n / (float)SampleRate;
                float hum = Mathf.Sin(2f * Mathf.PI * 50f * t)
                            + 0.55f * Mathf.Sin(2f * Mathf.PI * 100f * t + 0.4f)
                            + 0.3f * Mathf.Sin(2f * Mathf.PI * 150f * t + 1.1f)
                            + 0.18f * Mathf.Sin(2f * Mathf.PI * 200f * t + 2.3f);

                // Le compresseur « respire » : un battement lent, période entière de la boucle.
                float breathe = 0.85f + 0.15f * Mathf.Sin(2f * Mathf.PI * 0.5f * t);

                data[n] = hum * 0.3f * breathe + hiss.Process(White(random)) * 0.15f;
            }

            return Finish("Frigo (genere)", data, 0.5f);
        }

        /// <summary>Horloge de cuisine : tic, tac, une fois par seconde.</summary>
        private static AudioClip ClockLoop(System.Random random)
        {
            float[] data = Buffer(2f);

            for (int n = 0; n < data.Length; n++)
            {
                float t = n / (float)SampleRate;
                float local = t - Mathf.Floor(t);
                bool tick = (Mathf.FloorToInt(t) % 2) == 0;

                if (local > 0.05f) continue;

                float frequency = tick ? 3400f : 2700f;
                float click = Mathf.Sin(2f * Mathf.PI * frequency * local) * Mathf.Exp(-local / 0.004f)
                              + White(random) * Mathf.Exp(-local / 0.0015f) * 0.4f;

                data[n] = click * (tick ? 1f : 0.8f);
            }

            return Finish("Horloge (generee)", data, 0.45f);
        }

        /// <summary>
        /// La basse du club entendue de la rue : grosse caisse et basse à 124 BPM, et tout ce
        /// qui dépasse 160 Hz arrêté par la façade.
        /// </summary>
        private static AudioClip ClubBassLoop()
        {
            int samplesPerBeat = Mathf.RoundToInt(SampleRate * 60f / 124f);
            int beats = 8;
            int length = samplesPerBeat * beats;
            int fade = SampleRate / 4;
            float[] data = new float[length + fade];

            float[] notes = { 55f, 55f, 49f, 55f, 65.4f, 55f, 49f, 41.2f };
            float bassPhase = 0f;

            for (int n = 0; n < data.Length; n++)
            {
                int beat = (n / samplesPerBeat) % beats;
                float local = (n % samplesPerBeat) / (float)SampleRate;
                float beatLength = samplesPerBeat / (float)SampleRate;

                // Grosse caisse : sinus qui plonge de 120 à 48 Hz.
                float kickFrequency = 48f + 72f * Mathf.Exp(-local / 0.03f);
                float kick = Mathf.Sin(2f * Mathf.PI * kickFrequency * local) * Mathf.Exp(-local / 0.16f);

                // Basse : sur le contretemps.
                float offbeat = local - beatLength * 0.5f;
                float bass = 0f;

                if (offbeat > 0f)
                {
                    bassPhase += notes[beat] / SampleRate;
                    bassPhase -= Mathf.Floor(bassPhase);
                    bass = (bassPhase * 2f - 1f) * Mathf.Exp(-offbeat / 0.14f) * Mathf.Clamp01(offbeat / 0.008f);
                }

                data[n] = kick * 0.9f + bass * 0.5f;
            }

            Lowpass wallA = new Lowpass(160f);
            Lowpass wallB = new Lowpass(160f);
            for (int n = 0; n < data.Length; n++) data[n] = wallB.Process(wallA.Process(data[n]));

            return Finish("Basse du club (generee)", data, 0.75f, fade);
        }

        /// <summary>Néon : bourdonnement à 100 Hz, saturé, et grésillement aigu.</summary>
        private static AudioClip NeonLoop(System.Random random)
        {
            float[] data = Buffer(2f);

            Bandpass sizzle = Bandpass.Create(5200f, 1.5f);

            for (int n = 0; n < data.Length; n++)
            {
                float t = n / (float)SampleRate;
                float wave = Mathf.Sin(2f * Mathf.PI * 100f * t);
                float buzz = Tanh(wave * 3f) * 0.3f;
                float crackle = sizzle.Process(White(random)) * (0.4f + 0.6f * Mathf.Abs(wave));

                data[n] = buzz + crackle * 0.25f;
            }

            return Finish("Neon (genere)", data, 0.4f);
        }

        // ------------------------------------------------------------------ événements

        /// <summary>
        /// Une voiture qui passe : moteur dont la hauteur baisse au passage (effet Doppler),
        /// bruit de roulement, et le chuintement des pneus sur la chaussée mouillée.
        /// <paramref name="cutoff"/> étouffe le tout (à travers un mur, au loin).
        /// </summary>
        private static AudioClip CarPass(string name, System.Random random, float cutoff, float wet)
        {
            float duration = 4.5f + (float)random.NextDouble() * 1.5f;
            int length = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[length];

            float center = duration * (0.45f + (float)random.NextDouble() * 0.1f);
            float speed = 0.5f + (float)random.NextDouble() * 0.4f;
            float engine = 55f + (float)random.NextDouble() * 35f;

            Lowpass road = new Lowpass(900f);
            Lowpass hissLow = new Lowpass(1500f);
            Lowpass muffleA = new Lowpass(cutoff);
            Lowpass muffleB = new Lowpass(cutoff);
            float phase = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                float x = (t - center) / speed;
                float proximity = 1f / (1f + x * x);

                float pitch = engine * (1f - 0.09f * Tanh(x * 1.4f));
                phase += pitch / SampleRate;
                phase -= Mathf.Floor(phase);

                float white = White(random);
                float hiss = white - hissLow.Process(white);

                float sample = (phase * 2f - 1f) * 0.35f
                               + road.Process(white) * 0.8f
                               + hiss * wet;

                float edge = Mathf.Clamp01(t / 0.3f) * Mathf.Clamp01((duration - t) / 0.3f);
                data[n] = muffleB.Process(muffleA.Process(sample)) * proximity * edge;
            }

            return Finish(name + " (generee)", data, 0.8f, 0);
        }

        /// <summary>« Pin-pon » : la sirène à deux tons, qui arrive, passe et s'éloigne, avec
        /// l'écho des façades.</summary>
        private static AudioClip Siren(string name, System.Random random, float cutoff)
        {
            float duration = 8f;
            int length = Mathf.RoundToInt(duration * SampleRate);
            float[] data = new float[length];

            float low = 435f;
            float high = 580f;
            float period = 0.62f + (float)random.NextDouble() * 0.1f;

            Lowpass toneA = new Lowpass(cutoff);
            Lowpass toneB = new Lowpass(cutoff);
            float phase = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                bool upper = Mathf.FloorToInt(t / period) % 2 == 1;

                // Elle approche puis s'éloigne : un léger Doppler au milieu.
                float drift = 1f + 0.025f * (0.5f - Mathf.SmoothStep(0f, 1f, t / duration)) * 2f;
                phase += (upper ? high : low) * drift / SampleRate;
                phase -= Mathf.Floor(phase);

                float wave = Mathf.Sin(2f * Mathf.PI * phase)
                             + Mathf.Sin(6f * Mathf.PI * phase) / 3f
                             + Mathf.Sin(10f * Mathf.PI * phase) / 5f;

                float envelope = Mathf.SmoothStep(0f, 1f, t / 2.5f) * Mathf.SmoothStep(0f, 1f, (duration - t) / 3f);
                data[n] = toneB.Process(toneA.Process(wave)) * envelope;
            }

            Echo(data, 0.13f, 0.35f);
            Echo(data, 0.31f, 0.2f);

            return Finish(name + " (generee)", data, 0.6f, 0);
        }

        private static AudioClip Horn(string name, System.Random random)
        {
            float duration = 0.45f + (float)random.NextDouble() * 0.35f;
            int length = Mathf.RoundToInt((duration + 0.6f) * SampleRate);
            float[] data = new float[length];

            Lowpass tone = new Lowpass(1700f);
            float a = 0f;
            float b = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                if (t > duration + 0.08f) break;

                a += 415f / SampleRate;
                b += 523f / SampleRate;
                a -= Mathf.Floor(a);
                b -= Mathf.Floor(b);

                float envelope = Mathf.Clamp01(t / 0.012f) * Mathf.Clamp01((duration + 0.08f - t) / 0.08f);
                data[n] = tone.Process((a * 2f - 1f) + (b * 2f - 1f)) * envelope;
            }

            Echo(data, 0.17f, 0.3f);
            Echo(data, 0.38f, 0.15f);

            return Finish(name + " (genere)", data, 0.6f, 0);
        }

        /// <summary>Une goutte qui tombe d'une gouttière dans une flaque, et résonne.</summary>
        private static AudioClip Drip(string name, System.Random random)
        {
            int length = Mathf.RoundToInt(0.9f * SampleRate);
            float[] data = new float[length];

            float start = 1400f + (float)random.NextDouble() * 900f;
            float phase = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)SampleRate;
                if (t > 0.08f) break;

                float frequency = start * (1f + 0.7f * Mathf.Exp(-t / 0.01f));
                phase += frequency / SampleRate;
                phase -= Mathf.Floor(phase);

                data[n] = Mathf.Sin(2f * Mathf.PI * phase) * Mathf.Exp(-t / 0.018f);
            }

            Echo(data, 0.09f, 0.4f);
            Echo(data, 0.21f, 0.25f);
            Echo(data, 0.37f, 0.12f);

            return Finish(name + " (generee)", data, 0.7f, 0);
        }

        // ------------------------------------------------------------------ outils

        private static float[] Buffer(float seconds)
        {
            // Une rallonge d'un tiers de seconde : elle sera fondue dans le début de la boucle.
            return new float[Mathf.RoundToInt(seconds * SampleRate) + SampleRate / 3];
        }

        private static AudioClip Finish(string name, float[] data, float peak)
        {
            return Finish(name, data, peak, SampleRate / 3);
        }

        /// <summary>
        /// Normalise, rend la boucle sans couture (la rallonge de fin est fondue dans le début),
        /// et crée le clip. <paramref name="fade"/> = 0 pour un son non bouclé.
        /// </summary>
        private static AudioClip Finish(string name, float[] data, float peak, int fade)
        {
            float[] result = data;

            if (fade > 0 && data.Length > fade * 2)
            {
                int length = data.Length - fade;
                for (int n = 0; n < fade; n++)
                {
                    float k = n / (float)fade;
                    data[n] = data[n] * k + data[length + n] * (1f - k);
                }

                result = new float[length];
                System.Array.Copy(data, result, length);
            }

            float max = 0f;
            for (int i = 0; i < result.Length; i++) max = Mathf.Max(max, Mathf.Abs(result[i]));

            if (max > 1e-5f)
            {
                float gain = peak / max;
                for (int i = 0; i < result.Length; i++) result[i] *= gain;
            }

            AudioClip clip = AudioClip.Create(name, result.Length, 1, SampleRate, false);
            clip.SetData(result, 0);
            return clip;
        }

        /// <summary>Des petits chocs aléatoires (gouttes), <paramref name="rate"/> par seconde.</summary>
        private static void AddTaps(float[] data, System.Random random, float rate, float frequency,
            float decay, float level)
        {
            int count = Mathf.RoundToInt(rate * data.Length / (float)SampleRate);
            int span = Mathf.RoundToInt(decay * 6f * SampleRate);

            for (int i = 0; i < count; i++)
            {
                int start = random.Next(0, Mathf.Max(1, data.Length - span));
                float f = frequency * (0.7f + (float)random.NextDouble() * 0.6f);
                float amplitude = level * (0.3f + (float)random.NextDouble() * 0.7f);

                for (int n = 0; n < span; n++)
                {
                    float t = n / (float)SampleRate;
                    data[start + n] += Mathf.Sin(2f * Mathf.PI * f * t) * Mathf.Exp(-t / decay) * amplitude;
                }
            }
        }

        private static void Echo(float[] data, float delay, float gain)
        {
            int offset = Mathf.RoundToInt(delay * SampleRate);
            for (int n = data.Length - 1; n >= offset; n--) data[n] += data[n - offset] * gain;
        }

        private static float White(System.Random random)
        {
            return (float)random.NextDouble() * 2f - 1f;
        }

        private static float Tanh(float x)
        {
            float e = Mathf.Exp(2f * Mathf.Clamp(x, -10f, 10f));
            return (e - 1f) / (e + 1f);
        }

        /// <summary>Filtre passe-bas du premier ordre.</summary>
        private struct Lowpass
        {
            private readonly float _a;
            private float _y;

            public Lowpass(float cutoff)
            {
                _a = 1f - Mathf.Exp(-2f * Mathf.PI * Mathf.Clamp(cutoff, 10f, SampleRate * 0.45f) / SampleRate);
                _y = 0f;
            }

            public float Process(float x)
            {
                _y += _a * (x - _y);
                return _y;
            }
        }

        /// <summary>Filtre passe-bande du second ordre (formules de R. Bristow-Johnson).</summary>
        private struct Bandpass
        {
            private float _b0, _b2, _a1, _a2;
            private float _x1, _x2, _y1, _y2;

            public static Bandpass Create(float frequency, float q)
            {
                float w0 = 2f * Mathf.PI * Mathf.Clamp(frequency, 20f, SampleRate * 0.45f) / SampleRate;
                float alpha = Mathf.Sin(w0) / (2f * q);
                float a0 = 1f + alpha;

                Bandpass filter = new Bandpass();
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

        /// <summary>
        /// Des « bouffées » lentes (0 à 1) : une voiture qui passe sur le boulevard, une rafale
        /// de vent. Chaque bouffée est une cloche gaussienne posée au hasard dans la boucle.
        /// </summary>
        private struct Swells
        {
            private readonly float[] _centers;
            private readonly float[] _widths;

            public Swells(System.Random random, int length, int count, float minWidth, float maxWidth)
            {
                _centers = new float[count];
                _widths = new float[count];

                for (int i = 0; i < count; i++)
                {
                    _centers[i] = (float)random.NextDouble() * length;
                    _widths[i] = (minWidth + (float)random.NextDouble() * (maxWidth - minWidth)) * SampleRate;
                }
            }

            public float At(int n)
            {
                float value = 0f;
                for (int i = 0; i < _centers.Length; i++)
                {
                    float x = (n - _centers[i]) / _widths[i];
                    value += Mathf.Exp(-x * x);
                }

                return Mathf.Clamp01(value);
            }
        }
    }
}
