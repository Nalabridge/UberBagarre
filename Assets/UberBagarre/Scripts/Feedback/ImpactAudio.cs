using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Sons de combat générés par code.
    ///
    /// Le projet ne contient aucun fichier audio, et le silence est le pire retour d'impact
    /// possible : sans son, un coup qui touche et un coup qui rate se ressemblent.
    ///
    /// La première version sonnait comme un jouet, et la raison est précise : ses impacts étaient
    /// construits autour d'un SINUS. Un sinus a une hauteur, donc on entend une NOTE — et un corps
    /// frappé ne joue pas de note. Le cri de douleur, lui, était deux sinus harmoniques : une voix
    /// synthétique, c'est-à-dire la chose la plus ridicule qu'on puisse produire par accident.
    ///
    /// Règle appliquée partout ici : **du bruit filtré, jamais d'oscillateur audible.** Ce qui
    /// distingue un son d'impact d'un autre n'est pas sa hauteur mais son enveloppe (attaque,
    /// décroissance) et son contenu spectral (à quel point il est sourd). Un poing qui touche, un
    /// avant-bras qui bloque et une expiration ne diffèrent que par ces deux choses.
    ///
    /// Remplacer par de vrais échantillons se fera en assignant les champs ci-dessous ;
    /// la génération ne sert que de secours.
    /// </summary>
    [RequireComponent(typeof(AudioSource))]
    public class ImpactAudio : MonoBehaviour
    {
        private const int SampleRate = 44100;

        [Header("Echantillons (optionnels)")]
        [SerializeField] private AudioClip _lightImpactClip;
        [SerializeField] private AudioClip _heavyImpactClip;
        [SerializeField] private AudioClip _whooshClip;
        [SerializeField] private AudioClip _hurtClip;
        [SerializeField] private AudioClip _blockClip;
        [SerializeField] private AudioClip _parryClip;

        [Header("Volumes")]
        [SerializeField, Range(0f, 1f)] private float _impactVolume = 0.85f;
        [SerializeField, Range(0f, 1f)] private float _whooshVolume = 0.22f;
        [SerializeField, Range(0f, 1f)] private float _hurtVolume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _blockVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _parryVolume = 0.8f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Variation aleatoire de hauteur : deux coups identiques ne sonnent jamais pareil. " +
                 "C'est le seul remede contre l'effet mitraillette quand on enchaine.")]
        private float _pitchVariation = 0.17f;

        private AudioSource _source;
        private AudioClip[] _lightPunches;
        private AudioClip[] _heavyPunches;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            // Les deux impacts partagent leur construction et ne diffèrent que par la masse
            // apparente : plus le coup est lourd, plus il est sourd et plus il traîne.
            // Coups portés : trois variantes de chaque, tirées au hasard. Un seul échantillon
            // répété à chaque coup devient une mitraillette au troisième direct.
            if (_lightImpactClip == null)
            {
                _lightPunches = new AudioClip[3];
                for (int i = 0; i < 3; i++) _lightPunches[i] = BuildPunch("Coup_Leger_" + i, false, 101 + i * 17);
            }

            if (_heavyImpactClip == null)
            {
                _heavyPunches = new AudioClip[3];
                for (int i = 0; i < 3; i++) _heavyPunches[i] = BuildPunch("Coup_Lourd_" + i, true, 707 + i * 29);
            }
            if (_whooshClip == null) _whooshClip = BuildWhoosh("Souffle_Poing");
            if (_hurtClip == null) _hurtClip = BuildBreath("Expiration");
            if (_blockClip == null) _blockClip = BuildImpact("Blocage", 0.070f, 0.18f, 0.10f, 0.25f);
            if (_parryClip == null) _parryClip = BuildParry("Parade");
        }

        public void PlayImpact(bool heavy)
        {
            AudioClip[] bank = heavy ? _heavyPunches : _lightPunches;
            AudioClip clip = bank != null && bank.Length > 0
                ? bank[Random.Range(0, bank.Length)]
                : (heavy ? _heavyImpactClip : _lightImpactClip);

            Play(clip, _impactVolume);
        }

        public void PlayWhoosh()
        {
            Play(_whooshClip, _whooshVolume);
        }

        public void PlayHurt()
        {
            Play(_hurtClip, _hurtVolume);
        }

        /// <summary>
        /// Bloquer et parer DOIVENT s'entendre différemment d'un coup encaissé.
        ///
        /// C'est le seul retour immédiat dont dispose le joueur pour savoir si sa garde a servi :
        /// la barre de vie ne bouge presque pas dans les deux cas.
        /// </summary>
        public void PlayBlock()
        {
            Play(_blockClip, _blockVolume);
        }

        public void PlayParry()
        {
            Play(_parryClip, _parryVolume);
        }

        private void Play(AudioClip clip, float volume)
        {
            if (clip == null || _source == null) return;

            _source.pitch = 1f + Random.Range(-_pitchVariation, _pitchVariation);
            _source.PlayOneShot(clip, volume);
        }

        // ------------------------------------------------------------------ synthèse

        /// <summary>
        /// Impact : un transitoire de bruit, et rien d'autre.
        ///
        /// Trois paramètres suffisent à couvrir tout l'éventail entre un jab sur la joue et un
        /// coup de pied dans les côtes :
        /// - <paramref name="duration"/> : combien de temps ça traîne ;
        /// - <paramref name="brightness"/> : ouverture du filtre. Haut = claquement de peau,
        ///   bas = masse sourde. C'est ce réglage, et lui seul, qui porte le poids du coup ;
        /// - <paramref name="attack"/> : temps de montée. Quelques millisecondes, sinon on perd
        ///   la sensation de choc et le son devient un « wouf » ;
        /// - <paramref name="weight"/> : part de grondement très grave sous le transitoire.
        ///
        /// Aucune hauteur n'est imposée nulle part : le son n'a donc pas de note, ce qui est la
        /// condition pour qu'il ne sonne pas comme un jouet.
        /// </summary>
        /// <summary>
        /// Fabrique un clip d'impact avec la même synthèse que les coups.
        ///
        /// Exposé pour les objets du décor : une bouteille qui tombe et un poing qui touche
        /// doivent venir de la même famille de sons, sinon le décor sonne comme une banque
        /// d'effets différente plaquée sur le combat.
        /// </summary>
        public static AudioClip CreateImpactClip(string clipName, float duration, float brightness,
            float attack, float weight)
        {
            return BuildImpact(clipName, duration, brightness, attack, weight);
        }

        private static AudioClip BuildImpact(string clipName, float duration, float brightness,
            float attack, float weight)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];

            int attackSamples = Mathf.Max(1, Mathf.CeilToInt(attack * 0.1f * SampleRate));

            // Deux poles de passe-bas : un seul laisse passer un souffle de radio trop aigu.
            float lowA = 0f;
            float lowB = 0f;

            // Passe-haut tres bas, pour que le grondement ne devienne pas un bourdonnement continu.
            float rumble = 0f;
            float rumblePrevious = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float noise = Random.value * 2f - 1f;

                // Le filtre se FERME au fil du son : l'impact commence clair et devient sourd,
                // comme une surface qui absorbe. Un filtre fixe donne un son plat et synthetique.
                float cutoff = Mathf.Lerp(brightness, brightness * 0.18f, t);
                lowA = Mathf.Lerp(lowA, noise, cutoff);
                lowB = Mathf.Lerp(lowB, lowA, cutoff);

                // Grondement : du bruit tres fortement filtre, donc sans hauteur identifiable.
                rumble = Mathf.Lerp(rumble, noise, 0.012f);
                float body = rumble - rumblePrevious * 0.5f;
                rumblePrevious = rumble;

                float envelope = i < attackSamples
                    ? i / (float)attackSamples
                    : Mathf.Exp(-(t - attackSamples / (float)samples) * 14f);

                data[i] = Mathf.Clamp((lowB * (1f - weight * 0.45f) + body * weight * 5.5f) * envelope, -1f, 1f);
            }

            return Finish(clipName, data);
        }

        /// <summary>
        /// Un coup de poing qui porte, en trois couches superposées, comme au cinéma :
        /// - la CLAQUE : quelques millisecondes de bruit clair, la peau contre la peau ;
        /// - la MASSE : un grave bref qui plonge (de ~120 à ~50 Hz), la chair qui encaisse ;
        /// - sur un coup lourd, le CRAQUEMENT : une grappe de micro-impulsions juste après,
        ///   cartilage et os.
        /// Aucune note tenue : la « hauteur » du grave tombe trop vite pour être entendue comme
        /// une note, elle se lit comme du poids.
        /// </summary>
        private static AudioClip BuildPunch(string clipName, bool heavy, int seed)
        {
            System.Random random = new System.Random(seed);
            float duration = heavy ? 0.26f : 0.16f;
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];

            float slapLow = 0f;
            float slapPrevious = 0f;
            float phase = 0f;
            float crunch = 0f;
            float crunchLow = 0f;

            float thumpStart = heavy ? 125f : 150f;
            float thumpEnd = heavy ? 48f : 70f;
            float thumpDecay = heavy ? 13f : 22f;
            float thumpGain = heavy ? 1.0f : 0.6f;

            for (int i = 0; i < samples; i++)
            {
                float time = i / (float)SampleRate;
                float noise = (float)random.NextDouble() * 2f - 1f;

                // Claque : bande claire, attaque instantanee, 25 ms.
                slapLow = Mathf.Lerp(slapLow, noise, 0.45f);
                float slap = slapLow - slapPrevious * 0.8f;
                slapPrevious = slapLow;
                float slapEnvelope = Mathf.Min(1f, time * 2500f) * Mathf.Exp(-time * (heavy ? 95f : 130f));

                // Masse : sinus qui plonge.
                float frequency = Mathf.Lerp(thumpEnd, thumpStart, Mathf.Exp(-time * 38f));
                phase += frequency * 2f * Mathf.PI / SampleRate;
                float thump = Mathf.Sin(phase) * Mathf.Min(1f, time * 900f) * Mathf.Exp(-time * thumpDecay) * thumpGain;

                // Craquement : impulsions eparses entre 8 et 60 ms.
                float crack = 0f;
                if (heavy && time > 0.008f && time < 0.06f && random.NextDouble() < 0.012)
                {
                    crunch = (float)random.NextDouble() * 1.4f + 0.4f;
                }

                crunch *= 0.93f;
                crunchLow = Mathf.Lerp(crunchLow, crunch * noise, 0.6f);
                crack = crunchLow * 0.55f;

                data[i] = Mathf.Clamp(slap * slapEnvelope * (heavy ? 0.9f : 1.1f) + thump + crack, -1f, 1f);
            }

            return Finish(clipName, data);
        }

        /// <summary>Bruit filtré en cloche : le déplacement d'air d'un poing qui passe. Discret, sinon il masque l'impact.</summary>
        private static AudioClip BuildWhoosh(string clipName)
        {
            int samples = Mathf.CeilToInt(0.18f * SampleRate);
            float[] data = new float[samples];
            float low = 0f;
            float previous = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float noise = Random.value * 2f - 1f;

                // Le filtre s'ouvre puis se referme : le souffle passe devant l'oreille.
                float cutoff = 0.04f + Mathf.Sin(t * Mathf.PI) * 0.16f;
                low = Mathf.Lerp(low, noise, cutoff);

                // Difference premiere : enleve les tres basses, qui feraient un grondement.
                float band = low - previous;
                previous = low;

                float envelope = Mathf.Sin(t * Mathf.PI);
                data[i] = Mathf.Clamp(band * envelope * envelope * 7f, -1f, 1f);
            }

            return Finish(clipName, data);
        }

        /// <summary>
        /// Expiration de celui qui encaisse. Du souffle, pas de voix.
        ///
        /// La version précédente empilait deux sinus pour imiter des cordes vocales. Une voix
        /// synthétisée par deux oscillateurs ne ressemble à aucune voix humaine — elle ressemble à
        /// un jouet, et c'est le seul son que tout le monde remarque. Ici il n'y a que du bruit
        /// filtré : l'information « il a pris le coup » passe par le souffle, qui est d'ailleurs
        /// ce qu'on entend vraiment quand quelqu'un se fait toucher au corps.
        /// </summary>
        private static AudioClip BuildBreath(string clipName)
        {
            int samples = Mathf.CeilToInt(0.26f * SampleRate);
            float[] data = new float[samples];
            float low = 0f;
            float previous = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float noise = Random.value * 2f - 1f;

                // Le souffle part ouvert et se ferme : une expiration, pas un sifflement continu.
                float cutoff = Mathf.Lerp(0.13f, 0.02f, t);
                low = Mathf.Lerp(low, noise, cutoff);

                float band = low - previous * 0.35f;
                previous = low;

                // Attaque rapide, longue queue : le souffle s'echappe d'un coup puis s'eteint.
                float envelope = Mathf.Min(1f, t * 12f) * Mathf.Exp(-t * 4.2f);
                data[i] = Mathf.Clamp(band * envelope * 4.5f, -1f, 1f);
            }

            return Finish(clipName, data);
        }

        /// <summary>
        /// Parade : un claquement net et très court.
        ///
        /// Il doit se distinguer d'un impact sans pour autant jouer une note — la version
        /// précédente était un « ting » musical montant, exactement le genre de son qui fait
        /// dessin animé. Ici c'est le même transitoire qu'un impact, mais beaucoup plus bref et
        /// beaucoup plus clair : l'oreille lit « sec et réussi » sans qu'aucune hauteur soit jouée.
        /// </summary>
        private static AudioClip BuildParry(string clipName)
        {
            int samples = Mathf.CeilToInt(0.075f * SampleRate);
            float[] data = new float[samples];
            float low = 0f;
            float previous = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;
                float noise = Random.value * 2f - 1f;

                low = Mathf.Lerp(low, noise, 0.55f);

                float bright = low - previous * 0.6f;
                previous = low;

                float envelope = Mathf.Min(1f, t * 60f) * Mathf.Exp(-t * 26f);
                data[i] = Mathf.Clamp(bright * envelope * 1.6f, -1f, 1f);
            }

            return Finish(clipName, data);
        }

        /// <summary>
        /// Normalise et termine le clip.
        ///
        /// La normalisation n'est pas cosmétique : un son synthétisé à partir de bruit a une
        /// amplitude qui dépend du tirage aléatoire, donc deux générations du même clip ne
        /// sortiraient pas au même volume. Régler les volumes dans l'Inspector serait alors
        /// impossible.
        /// </summary>
        private static AudioClip Finish(string clipName, float[] data)
        {
            float peak = 0f;
            for (int i = 0; i < data.Length; i++) peak = Mathf.Max(peak, Mathf.Abs(data[i]));

            if (peak > 0.0001f)
            {
                float gain = 0.92f / peak;
                for (int i = 0; i < data.Length; i++) data[i] *= gain;
            }

            AudioClip clip = AudioClip.Create(clipName, data.Length, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
