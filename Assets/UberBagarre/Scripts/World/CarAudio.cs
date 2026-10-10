using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le son d'une voiture, fait comme dans les jeux de voiture et non comme un jouet.
    ///
    /// **Le moteur** n'est plus un accord de sinus dont on change la hauteur : c'est un vrai
    /// train d'explosions (quatre cylindres, deux explosions par tour, chacune un peu différente)
    /// qui traverse un échappement — un tuyau qui résonne (écho de 13 ms), trois résonances de
    /// silencieux, un filtre — plus le bruit de combustion, l'aspiration à haut régime, la
    /// mécanique. Quatre boucles en sont tirées : bas et haut régime, pied sur l'accélérateur ou
    /// levé (le moteur qui retient sonne creux et plus aigu). En jeu, les quatre jouent ensemble,
    /// chacune accordée au régime réel et dosée selon le régime et la pédale : c'est ce mélange
    /// qui fait entendre les rapports, la charge, le rupteur.
    ///
    /// **Le klaxon** : deux avertisseurs à membrane (420 et 525 Hz, une tierce majeure comme sur
    /// une berline), une impulsion étroite très riche filtrée par la résonance de la membrane
    /// (≈ 2,3 kHz), saturée doucement, avec le battement des deux pavillons — et une attaque et un
    /// relâchement, au lieu de deux carrés qui s'allument et s'éteignent.
    ///
    /// **Les pneus** : un bruit de roulement qui suit la vitesse, et le crissement (bruit passé
    /// dans trois résonances étroites qui ondulent) qui suit le glissement. **Les chocs** : un coup
    /// sourd, la tôle (modes inharmoniques), le froissement, et le verre pour les plus violents.
    ///
    /// Tout peut être remplacé par de vrais enregistrements : un fichier posé dans
    /// <c>Resources/Sons/Voitures/</c> sous le bon nom (voir <see cref="Override"/>) est pris à
    /// la place du son fabriqué.
    /// </summary>
    public class CarAudio : MonoBehaviour
    {
        // Régimes auxquels les boucles du moteur ont été fabriquées (et ceux qu'on suppose aux
        // enregistrements fournis).
        public const float LowRpm = 1600f;
        public const float HighRpm = 4600f;

        private DrivableCar _car;
        private AudioSource _lowOn;
        private AudioSource _highOn;
        private AudioSource _lowOff;
        private AudioSource _highOff;
        private AudioSource _road;
        private AudioSource _tires;
        private AudioSource _horn;
        private AudioSource _impacts;

        private bool _running;
        private bool _honking;
        private float _hornLevel;
        private float _load;
        private float _lastCrash;

        private static AudioClip _clipLowOn;
        private static AudioClip _clipHighOn;
        private static AudioClip _clipLowOff;
        private static AudioClip _clipHighOff;
        private static AudioClip _clipRoad;
        private static AudioClip _clipTires;
        private static AudioClip _clipHorn;
        private static AudioClip _clipHornClick;
        private static AudioClip[] _clipCrash;
        private static AudioClip _clipDoor;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _clipLowOn = _clipHighOn = _clipLowOff = _clipHighOff = null;
            _clipRoad = _clipTires = _clipHorn = _clipHornClick = _clipDoor = null;
            _clipCrash = null;
        }

        /// <summary>Le claquement d'une portière (fichier « portiere » s'il existe).</summary>
        public static AudioClip DoorClip
        {
            get
            {
                if (_clipDoor == null) _clipDoor = Override("portiere") ?? SynthDoor();
                return _clipDoor;
            }
        }

        /// <summary>Branche les sources posées par le constructeur (moteur, pneus, klaxon, chocs).</summary>
        public void Bind(DrivableCar car, AudioSource engine, AudioSource tires, AudioSource horn, AudioSource impacts)
        {
            _car = car;
            BuildClips();

            Transform anchor = engine != null ? engine.transform : transform;
            _lowOn = Layer(engine, anchor, "Moteur bas regime", _clipLowOn);
            _highOn = Layer(null, anchor, "Moteur haut regime", _clipHighOn);
            _lowOff = Layer(null, anchor, "Moteur retenue bas", _clipLowOff);
            _highOff = Layer(null, anchor, "Moteur retenue haut", _clipHighOff);
            _road = Layer(null, tires != null ? tires.transform : transform, "Roulement", _clipRoad);
            _tires = Layer(tires, tires != null ? tires.transform : transform, "Crissement", _clipTires);
            _horn = Layer(horn, horn != null ? horn.transform : transform, "Klaxon", _clipHorn);
            _horn.maxDistance = Mathf.Max(_horn.maxDistance, 70f);

            _impacts = impacts;
            if (_impacts == null)
            {
                _impacts = gameObject.AddComponent<AudioSource>();
                _impacts.spatialBlend = 0.9f;
                _impacts.minDistance = 1.5f;
                _impacts.maxDistance = 45f;
                _impacts.rolloffMode = AudioRolloffMode.Linear;
            }

            _impacts.playOnAwake = false;
        }

        private AudioSource Layer(AudioSource existing, Transform anchor, string name, AudioClip clip)
        {
            AudioSource source = existing;
            if (source == null)
            {
                GameObject go = new GameObject(name);
                go.transform.SetParent(anchor, false);
                source = go.AddComponent<AudioSource>();
                source.spatialBlend = 0.85f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 1.2f;
                source.maxDistance = 45f;
                source.dopplerLevel = 0.3f;
            }

            source.clip = clip;
            source.loop = true;
            source.playOnAwake = false;
            source.volume = 0f;
            return source;
        }

        public void EngineRunning(bool on)
        {
            _running = on;
            if (!on)
            {
                Stop(_lowOn);
                Stop(_highOn);
                Stop(_lowOff);
                Stop(_highOff);
            }
        }

        public void Honk(bool on)
        {
            if (on && !_honking && _impacts != null && _clipHornClick != null)
            {
                _impacts.PlayOneShot(_clipHornClick, 0.35f * GameSettings.Volume(AudioChannel.Effects));
            }

            _honking = on;
        }

        /// <summary>Un choc : un son selon la violence, jamais deux à la suite pour un même frottement.</summary>
        public void Crash(float impact, Vector3 at)
        {
            if (_impacts == null || _clipCrash == null || Time.time - _lastCrash < 0.12f) return;
            _lastCrash = Time.time;

            int variant = impact < 6f ? 0 : impact < 12f ? 1 : 2;
            _impacts.pitch = Random.Range(0.9f, 1.08f);
            _impacts.PlayOneShot(_clipCrash[variant], Mathf.Clamp01(impact / 14f) * GameSettings.Volume(AudioChannel.Effects));
        }

        /// <summary>Appelé par la voiture à chaque image.</summary>
        public void Tick(float dt)
        {
            if (_car == null) return;

            float effects = GameSettings.Volume(AudioChannel.Effects);
            bool player = _car.Occupied;
            float master = (player ? 0.95f : 0.5f) * effects;

            // --- moteur
            float rpm = _car.EngineRpm;
            float wanted = _car.EngineLoad;
            _load = Mathf.MoveTowards(_load, wanted, dt * (wanted > _load ? 9f : 5f));
            if (_running)
            {
                float high = Smooth(2300f, 3900f, rpm);
                float low = 1f - high;
                float on = Mathf.Pow(_load, 0.6f);
                float off = 1f - on;
                float dip = 1f - _car.ShiftDip * 0.3f;
                float pitchLow = Mathf.Clamp(rpm / LowRpm, 0.48f, 2.4f) * (1f - _car.ShiftDip * 0.04f);
                float pitchHigh = Mathf.Clamp(rpm / HighRpm, 0.42f, 1.6f) * (1f - _car.ShiftDip * 0.04f);
                // Plus fort quand ça tire et quand ça monte dans les tours.
                float body = master * dip * (0.55f + 0.45f * Mathf.Clamp01(rpm / _car.Redline));

                Drive(_lowOn, Mathf.Sqrt(low) * on * body, pitchLow);
                Drive(_highOn, Mathf.Sqrt(high) * on * body, pitchHigh);
                Drive(_lowOff, Mathf.Sqrt(low) * off * body * 0.55f, pitchLow);
                Drive(_highOff, Mathf.Sqrt(high) * off * body * 0.6f, pitchHigh);
            }

            // --- roulement et crissement
            float speed = Mathf.Abs(_car.ForwardSpeed);
            Drive(_road, Mathf.Clamp01(speed / 30f) * (player ? 0.32f : 0.18f) * effects, 0.75f + speed / 60f);

            float slip = _car.TireSlip;
            float squeal = Mathf.Clamp01((slip - 1.2f) / 5f) * 0.75f;
            if (_car.Handbrake && speed > 4f) squeal = Mathf.Max(squeal, 0.45f);
            Drive(_tires, squeal * effects * (player ? 1f : 0.7f), 0.9f + Mathf.Min(0.25f, slip * 0.02f));

            // --- klaxon : attaque 25 ms, relâchement 70 ms
            _hornLevel = Mathf.MoveTowards(_hornLevel, _honking ? 1f : 0f, dt * (_honking ? 40f : 14f));
            Drive(_horn, _hornLevel * 0.8f * effects, 1f);
        }

        private static float Smooth(float a, float b, float x)
        {
            float t = Mathf.Clamp01((x - a) / (b - a));
            return t * t * (3f - 2f * t);
        }

        private static void Drive(AudioSource source, float volume, float pitch)
        {
            if (source == null) return;
            source.volume = volume;
            source.pitch = pitch;
            if (volume > 0.004f)
            {
                if (!source.isPlaying && source.clip != null)
                {
                    // Chaque voiture démarre sa boucle ailleurs : pas deux moteurs en phase.
                    source.timeSamples = Random.Range(0, Mathf.Max(1, source.clip.samples - 1));
                    source.Play();
                }
            }
            else
            {
                Stop(source);
            }
        }

        private static void Stop(AudioSource source)
        {
            if (source != null && source.isPlaying) source.Stop();
        }

        // ================================================================== banque de sons

        /// <summary>
        /// Un vrai enregistrement fourni par le joueur, s'il existe :
        /// <c>Resources/Sons/Voitures/&lt;nom&gt;</c> (wav, ogg ou mp3). Noms : moteur_bas_charge,
        /// moteur_bas_relache, moteur_haut_charge, moteur_haut_relache (boucles enregistrées vers
        /// 1 600 et 4 600 tr/min), roulement, crissement, klaxon, choc_leger, choc_moyen, choc_fort,
        /// portiere.
        /// </summary>
        public static AudioClip Override(string name)
        {
            return SoundBank.Real("Voitures/" + name);
        }

        private static void BuildClips()
        {
            if (_clipLowOn != null) return;

            _clipLowOn = Override("moteur_bas_charge") ?? SynthEngine("Moteur bas (charge)", LowRpm, true, 11);
            _clipHighOn = Override("moteur_haut_charge") ?? SynthEngine("Moteur haut (charge)", HighRpm, true, 23);
            _clipLowOff = Override("moteur_bas_relache") ?? SynthEngine("Moteur bas (retenue)", LowRpm, false, 37);
            _clipHighOff = Override("moteur_haut_relache") ?? SynthEngine("Moteur haut (retenue)", HighRpm, false, 41);
            _clipRoad = Override("roulement") ?? SynthRoad();
            _clipTires = Override("crissement") ?? SynthSqueal();
            _clipHorn = Override("klaxon") ?? SynthHorn();
            _clipHornClick = SynthClick();
            _clipCrash = new[]
            {
                Override("choc_leger") ?? SynthCrash("Choc leger", 0, 101),
                Override("choc_moyen") ?? SynthCrash("Choc moyen", 1, 202),
                Override("choc_fort") ?? SynthCrash("Choc fort", 2, 303)
            };
        }

        // ------------------------------------------------------------------ filtres

        /// <summary>Un biquad (Robert Bristow-Johnson) : passe-bande à gain unité au pic, ou passe-bas.</summary>
        private struct Biquad
        {
            private float _b0, _b1, _b2, _a1, _a2, _x1, _x2, _y1, _y2;

            public static Biquad BandPass(float rate, float frequency, float q)
            {
                float w = 2f * Mathf.PI * frequency / rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float a0 = 1f + alpha;
                return new Biquad
                {
                    _b0 = alpha / a0, _b1 = 0f, _b2 = -alpha / a0,
                    _a1 = -2f * Mathf.Cos(w) / a0, _a2 = (1f - alpha) / a0
                };
            }

            public static Biquad LowPass(float rate, float frequency, float q)
            {
                float w = 2f * Mathf.PI * frequency / rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float c = Mathf.Cos(w);
                float a0 = 1f + alpha;
                return new Biquad
                {
                    _b0 = (1f - c) * 0.5f / a0, _b1 = (1f - c) / a0, _b2 = (1f - c) * 0.5f / a0,
                    _a1 = -2f * c / a0, _a2 = (1f - alpha) / a0
                };
            }

            public static Biquad HighPass(float rate, float frequency, float q)
            {
                float w = 2f * Mathf.PI * frequency / rate;
                float alpha = Mathf.Sin(w) / (2f * q);
                float c = Mathf.Cos(w);
                float a0 = 1f + alpha;
                return new Biquad
                {
                    _b0 = (1f + c) * 0.5f / a0, _b1 = -(1f + c) / a0, _b2 = (1f + c) * 0.5f / a0,
                    _a1 = -2f * c / a0, _a2 = (1f - alpha) / a0
                };
            }

            public float Run(float x)
            {
                float y = _b0 * x + _b1 * _x1 + _b2 * _x2 - _a1 * _y1 - _a2 * _y2;
                _x2 = _x1;
                _x1 = x;
                _y2 = _y1;
                _y1 = y;
                return y;
            }
        }

        private static AudioClip Clip(string name, float[] data, int rate)
        {
            // Normalisé à -3 dB crête : les volumes se règlent en jeu, pas dans le fichier.
            float peak = 1e-5f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));
            float gain = 0.7f / peak;
            for (int i = 0; i < data.Length; i++) data[i] *= gain;

            AudioClip clip = AudioClip.Create(name, data.Length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        // ------------------------------------------------------------------ moteur

        /// <summary>
        /// Une boucle de moteur à <paramref name="rpm"/> : un nombre entier de cycles (deux tours),
        /// calculée deux fois de suite et gardée la seconde (filtres et échos déjà établis) — la
        /// boucle se raccorde sans clic.
        /// </summary>
        private static AudioClip SynthEngine(string name, float rpm, bool onLoad, int seed)
        {
            const int rate = 32000;
            float cycle = 120f / rpm;                                  // deux tours
            int cycles = Mathf.Max(4, Mathf.RoundToInt(1.1f / cycle));
            int length = Mathf.RoundToInt(cycles * cycle * rate);
            float firing = length / (float)(cycles * 4);              // échantillons entre deux explosions

            System.Random random = new System.Random(seed);
            float[] noise = new float[length];
            for (int i = 0; i < length; i++) noise[i] = (float)(random.NextDouble() * 2.0 - 1.0);

            // Chaque cylindre a sa signature (un peu plus fort, un peu en retard) : c'est ce qui
            // fait « tourner » un moteur au lieu de bourdonner.
            float[] strength = { 1f, 0.9f, 1.07f, 0.95f };
            float[] lag = { 0f, 0.035f, -0.02f, 0.015f };

            Biquad pipe = Biquad.BandPass(rate, onLoad ? 92f : 110f, 1.1f);
            Biquad muffler = Biquad.BandPass(rate, onLoad ? 330f : 420f, 2.2f);
            Biquad rasp = Biquad.BandPass(rate, onLoad ? 1450f : 1900f, 1.6f);
            Biquad intake = Biquad.BandPass(rate, 2800f, 1.3f);
            Biquad tone = Biquad.LowPass(rate, onLoad ? 2600f : 3600f, 0.7f);
            Biquad rumble = Biquad.HighPass(rate, 28f, 0.7f);

            int delay = Mathf.RoundToInt(0.0128f * rate);             // aller-retour dans un échappement de 2,2 m
            float[] echo = new float[delay];
            int echoAt = 0;
            float feedback = onLoad ? 0.5f : 0.6f;

            float[] data = new float[length];
            float bang = onLoad ? 1f : 0.42f;
            float hiss = onLoad ? Mathf.Clamp01((rpm - 2500f) / 3000f) * 0.35f : 0.05f;

            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < length; i++)
                {
                    // Position dans la suite d'explosions.
                    float f = i / firing;
                    int k = Mathf.FloorToInt(f);
                    int cylinder = ((k % 4) + 4) % 4;
                    float t = (f - k - lag[cylinder]) * firing / rate;     // secondes depuis l'explosion
                    if (t < 0f) t += firing / rate;

                    float pulse = 0f;
                    if (t < 0.012f)
                    {
                        float rise = Mathf.Clamp01(t / 0.0004f);
                        pulse = rise * Mathf.Exp(-t / (onLoad ? 0.0024f : 0.0016f)) * strength[cylinder] * bang;
                        pulse += noise[i] * Mathf.Exp(-t / 0.004f) * 0.35f * bang;
                    }

                    // L'échappement : le tuyau qui renvoie l'onde, puis les chambres du silencieux.
                    float x = pulse + echo[echoAt] * feedback;
                    echo[echoAt] = x;
                    echoAt = (echoAt + 1) % delay;

                    float y = pipe.Run(x) * 1.1f + muffler.Run(x) * (onLoad ? 0.55f : 0.75f) + rasp.Run(x) * (onLoad ? 0.22f : 0.35f);
                    y += intake.Run(noise[(i * 7) % length]) * hiss;
                    // Mécanique : un léger cliquetis de soupapes, au rythme de l'arbre à cames.
                    float cam = Mathf.Repeat(i / (firing * 2f), 1f);
                    y += noise[(i * 13) % length] * (cam < 0.05f ? 0.04f : 0.008f);

                    y = tone.Run(y);
                    y = rumble.Run(y);
                    // Une saturation douce : le grain d'un vrai échappement, pas un filtre propre.
                    data[i] = Tanh(y * (onLoad ? 2.2f : 1.6f));
                }
            }

            return Clip(name, data, rate);
        }

        private static float Tanh(float x)
        {
            if (x > 4f) return 1f;
            if (x < -4f) return -1f;
            float e = Mathf.Exp(2f * x);
            return (e - 1f) / (e + 1f);
        }

        // ------------------------------------------------------------------ route, pneus

        private static AudioClip SynthRoad()
        {
            const int rate = 22050;
            int length = rate * 2;
            System.Random random = new System.Random(17);
            Biquad low = Biquad.LowPass(rate, 320f, 0.7f);
            Biquad body = Biquad.BandPass(rate, 850f, 0.9f);
            float[] noise = Noise(length, random);
            float[] data = new float[length];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < length; i++)
                {
                    float n = noise[i];
                    data[i] = low.Run(n) * 1.4f + body.Run(n) * 0.25f;
                }
            }

            return Clip("Roulement (synthese)", data, rate);
        }

        private static AudioClip SynthSqueal()
        {
            const int rate = 32000;
            int length = rate * 2;
            System.Random random = new System.Random(29);
            float[] noise = Noise(length, random);
            float[] freqs = { 780f, 1180f, 1720f };
            Biquad[] modes = new Biquad[freqs.Length];
            for (int m = 0; m < freqs.Length; m++) modes[m] = Biquad.BandPass(rate, freqs[m], 22f);
            Biquad scrub = Biquad.BandPass(rate, 2200f, 0.8f);

            float[] data = new float[length];
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)rate;
                    float y = 0f;
                    for (int m = 0; m < modes.Length; m++)
                    {
                        // Chaque mode ondule à son rythme (un nombre entier de fois sur la boucle).
                        float wobble = 0.55f + 0.45f * Mathf.Sin(2f * Mathf.PI * (3f + m * 2f) * t + m);
                        y += modes[m].Run(noise[i]) * wobble * (m == 0 ? 1f : 0.7f);
                    }

                    y += scrub.Run(noise[(i * 5) % length]) * 0.25f;
                    data[i] = Tanh(y * 3f);
                }
            }

            return Clip("Crissement (synthese)", data, rate);
        }

        private static float[] Noise(int length, System.Random random)
        {
            float[] noise = new float[length];
            for (int i = 0; i < length; i++) noise[i] = (float)(random.NextDouble() * 2.0 - 1.0);
            return noise;
        }

        // ------------------------------------------------------------------ klaxon

        private static AudioClip SynthHorn()
        {
            const int rate = 44100;
            int length = rate;                                         // 1 s : 420 et 525 périodes entières
            float[] data = new float[length];
            float[] tones = { 420f, 525f };
            Biquad[] membrane = { Biquad.BandPass(rate, 2300f, 2.5f), Biquad.BandPass(rate, 2450f, 2.5f) };
            Biquad[] body = { Biquad.LowPass(rate, 1400f, 0.8f), Biquad.LowPass(rate, 1500f, 0.8f) };
            Biquad air = Biquad.LowPass(rate, 6000f, 0.7f);

            for (int pass = 0; pass < 2; pass++)
            {
                float[] phase = { 0f, 0.37f };
                for (int i = 0; i < length; i++)
                {
                    float t = i / (float)rate;
                    float y = 0f;
                    for (int h = 0; h < 2; h++)
                    {
                        // Une impulsion étroite (la membrane qui claque contre l'électroaimant).
                        float vibrato = 1f + 0.003f * Mathf.Sin(2f * Mathf.PI * 6f * t + h);
                        phase[h] = Mathf.Repeat(phase[h] + tones[h] * vibrato / rate, 1f);
                        float p = phase[h] < 0.22f ? 1f : -0.28f;
                        y += membrane[h].Run(p) * 1.4f + body[h].Run(p) * 0.9f;
                    }

                    data[i] = air.Run(Tanh(y * 1.8f));
                }
            }

            return Clip("Klaxon (synthese)", data, rate);
        }

        private static AudioClip SynthClick()
        {
            const int rate = 44100;
            int length = rate / 25;
            float[] data = new float[length];
            System.Random random = new System.Random(5);
            Biquad tick = Biquad.BandPass(rate, 3200f, 3f);
            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                data[i] = tick.Run((float)(random.NextDouble() * 2.0 - 1.0)) * Mathf.Exp(-t * 180f);
            }

            return Clip("Relais du klaxon", data, rate);
        }

        // ------------------------------------------------------------------ chocs, portière

        /// <summary>
        /// Un choc de carrosserie : le coup sourd (bruit résonant grave), la tôle (modes
        /// inharmoniques qui décroissent chacun à sa vitesse), le froissement (craquements épars),
        /// et le verre pour le plus fort.
        /// </summary>
        private static AudioClip SynthCrash(string name, int severity, int seed)
        {
            const int rate = 44100;
            float seconds = severity == 0 ? 0.35f : severity == 1 ? 0.7f : 1.2f;
            int length = Mathf.RoundToInt(rate * seconds);
            System.Random random = new System.Random(seed);
            float[] data = new float[length];

            Biquad thud = Biquad.BandPass(rate, 70f + severity * 10f, 1.4f);
            float[] modeFreq = { 230f, 517f, 893f, 1330f, 2010f, 2710f, 3450f };
            Biquad[] modes = new Biquad[modeFreq.Length];
            float[] decay = new float[modeFreq.Length];
            for (int m = 0; m < modes.Length; m++)
            {
                float f = modeFreq[m] * (0.92f + 0.16f * (float)random.NextDouble());
                modes[m] = Biquad.BandPass(rate, f, 30f + m * 6f);
                decay[m] = (0.12f + 0.4f * (float)random.NextDouble()) * (0.6f + severity * 0.4f);
            }

            Biquad crumple = Biquad.BandPass(rate, 2600f, 1.2f);
            Biquad glass = Biquad.BandPass(rate, 6500f, 4f);

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float n = (float)(random.NextDouble() * 2.0 - 1.0);
                float hit = n * Mathf.Exp(-t * 60f);

                float y = thud.Run(hit) * (2.5f + severity);
                for (int m = 0; m < modes.Length; m++) y += modes[m].Run(hit) * Mathf.Exp(-t / decay[m]) * 6f;

                // Le froissement : des craquements épars pendant que la tôle se plie.
                if (t < 0.12f + severity * 0.12f && random.NextDouble() < 0.02 + severity * 0.02)
                {
                    y += crumple.Run(n * 3f) * (1f - t * 2f);
                }
                else
                {
                    y += crumple.Run(0f);
                }

                if (severity == 2 && t > 0.03f && t < 0.6f && random.NextDouble() < 0.004)
                {
                    y += glass.Run(n * 4f) * (0.6f - t);
                }
                else if (severity == 2)
                {
                    y += glass.Run(0f);
                }

                data[i] = Tanh(y);
            }

            return Clip(name, data, rate);
        }

        /// <summary>La portière : le déclic du pêne, puis le claquement sourd et la tôle qui vibre un peu.</summary>
        private static AudioClip SynthDoor()
        {
            const int rate = 44100;
            int length = Mathf.RoundToInt(rate * 0.6f);
            float[] data = new float[length];
            System.Random random = new System.Random(5);
            Biquad latch = Biquad.BandPass(rate, 2900f, 4f);
            Biquad slam = Biquad.BandPass(rate, 95f, 1.6f);
            Biquad panel = Biquad.BandPass(rate, 410f, 12f);
            Biquad seal = Biquad.LowPass(rate, 900f, 0.7f);

            for (int i = 0; i < length; i++)
            {
                float t = i / (float)rate;
                float n = (float)(random.NextDouble() * 2.0 - 1.0);
                float y = latch.Run(t < 0.02f ? n * Mathf.Exp(-t * 300f) : 0f) * 0.8f;
                float s = t - 0.3f;
                float hit = s > 0f ? n * Mathf.Exp(-s * 70f) : 0f;
                y += slam.Run(hit) * 4f + panel.Run(hit) * 2f * Mathf.Exp(-Mathf.Max(0f, s) * 9f) + seal.Run(hit) * 0.8f;
                data[i] = Tanh(y * 1.5f);
            }

            return Clip("Portiere (synthese)", data, rate);
        }
    }
}
