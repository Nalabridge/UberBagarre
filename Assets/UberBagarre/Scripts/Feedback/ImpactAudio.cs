using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Sons de combat générés par code.
    ///
    /// Le projet ne contient aucun fichier audio, et le silence est le pire retour d'impact
    /// possible : sans son, un coup qui touche et un coup qui rate se ressemblent. Ces clips
    /// sont synthétisés au démarrage — ils sont grossiers, mais ils portent l'information.
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
        [SerializeField, Range(0f, 1f)] private float _impactVolume = 0.8f;
        [SerializeField, Range(0f, 1f)] private float _whooshVolume = 0.3f;
        [SerializeField, Range(0f, 1f)] private float _hurtVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _blockVolume = 0.7f;
        [SerializeField, Range(0f, 1f)] private float _parryVolume = 0.85f;

        [SerializeField, Range(0f, 0.5f)]
        [Tooltip("Variation aleatoire de hauteur : deux coups identiques ne sonnent jamais pareil.")]
        private float _pitchVariation = 0.12f;

        private AudioSource _source;

        private void Awake()
        {
            _source = GetComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0f;

            if (_lightImpactClip == null) _lightImpactClip = BuildImpact("Impact_Leger", 0.18f, 150f, 0.55f);
            if (_heavyImpactClip == null) _heavyImpactClip = BuildImpact("Impact_Lourd", 0.32f, 85f, 0.8f);
            if (_whooshClip == null) _whooshClip = BuildWhoosh("Whoosh");
            if (_hurtClip == null) _hurtClip = BuildGrunt("Douleur");
            if (_blockClip == null) _blockClip = BuildBlock("Blocage");
            if (_parryClip == null) _parryClip = BuildParry("Parade");
        }

        public void PlayImpact(bool heavy)
        {
            Play(heavy ? _heavyImpactClip : _lightImpactClip, _impactVolume);
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
        /// la barre de vie ne bouge pas dans les deux cas, et un coup bloqué à 28 % ressemble
        /// beaucoup à un coup qui rate. Le son est donc l'information, pas une décoration.
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
        /// Un impact = un « corps » grave qui donne le poids, plus un claquement bruité bref
        /// qui donne la netteté. L'un sans l'autre sonne soit mou, soit creux.
        /// </summary>
        private static AudioClip BuildImpact(string clipName, float duration, float baseFrequency, float bodyMix)
        {
            int samples = Mathf.CeilToInt(duration * SampleRate);
            float[] data = new float[samples];

            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;

                // La hauteur descend pendant l'impact : c'est ce qui fait "thump" et non "bip".
                float frequency = baseFrequency * Mathf.Lerp(1f, 0.45f, t);
                phase += frequency / SampleRate * Mathf.PI * 2f;

                float body = Mathf.Sin(phase) * Mathf.Exp(-t * 9f);
                float crack = (Random.value * 2f - 1f) * Mathf.Exp(-t * 55f);

                data[i] = Mathf.Clamp(body * bodyMix + crack * (1f - bodyMix * 0.5f), -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Bruit filtré avec une enveloppe en cloche : le déplacement d'air d'un poing.</summary>
        private static AudioClip BuildWhoosh(string clipName)
        {
            int samples = Mathf.CeilToInt(0.22f * SampleRate);
            float[] data = new float[samples];
            float previous = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;

                float noise = Random.value * 2f - 1f;

                // Filtre passe-bas du pauvre : un bruit brut serait un sifflement de radio.
                previous = Mathf.Lerp(previous, noise, 0.22f);

                float envelope = Mathf.Sin(t * Mathf.PI);
                data[i] = previous * envelope * envelope * 0.8f;
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Blocage : un « tac » mat et sourd, sans résonance. Le coup s'arrête sur l'avant-bras,
        /// donc pas de claquement — c'est l'absence de brillance qui dit « absorbé ».
        /// </summary>
        private static AudioClip BuildBlock(string clipName)
        {
            int samples = Mathf.CeilToInt(0.14f * SampleRate);
            float[] data = new float[samples];
            float phase = 0f;
            float previous = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;

                float frequency = 190f * Mathf.Lerp(1f, 0.55f, t);
                phase += frequency / SampleRate * Mathf.PI * 2f;

                // Le bruit est fortement filtre : un claquement net sonnerait comme un impact
                // reussi, et le joueur croirait avoir pris le coup.
                previous = Mathf.Lerp(previous, Random.value * 2f - 1f, 0.10f);

                float envelope = Mathf.Exp(-t * 22f);
                data[i] = Mathf.Clamp((Mathf.Sin(phase) * 0.7f + previous * 0.5f) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>
        /// Parade : un « ting » clair et montant. À l'opposé exact du blocage — une parade est
        /// une réussite, et ça doit s'entendre dès la première fois, sans explication.
        /// </summary>
        private static AudioClip BuildParry(string clipName)
        {
            int samples = Mathf.CeilToInt(0.26f * SampleRate);
            float[] data = new float[samples];
            float phase = 0f;
            float harmonicPhase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;

                // La hauteur MONTE : c'est ce qui distingue une reussite d'un impact, dont la
                // hauteur descend toujours.
                float frequency = 780f * Mathf.Lerp(1f, 1.35f, Mathf.Sqrt(t));
                phase += frequency / SampleRate * Mathf.PI * 2f;
                harmonicPhase += frequency * 2.51f / SampleRate * Mathf.PI * 2f;

                float tone = Mathf.Sin(phase) + Mathf.Sin(harmonicPhase) * 0.30f;
                float click = (Random.value * 2f - 1f) * Mathf.Exp(-t * 120f) * 0.4f;

                data[i] = Mathf.Clamp(tone * 0.5f * Mathf.Exp(-t * 11f) + click, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }

        /// <summary>Souffle grave et court : la réaction de celui qui encaisse.</summary>
        private static AudioClip BuildGrunt(string clipName)
        {
            int samples = Mathf.CeilToInt(0.30f * SampleRate);
            float[] data = new float[samples];
            float phase = 0f;

            for (int i = 0; i < samples; i++)
            {
                float t = i / (float)samples;

                float frequency = 115f * Mathf.Lerp(1.1f, 0.75f, t) * (1f + Mathf.Sin(t * 40f) * 0.03f);
                phase += frequency / SampleRate * Mathf.PI * 2f;

                float voice = Mathf.Sin(phase) + Mathf.Sin(phase * 2f) * 0.35f;
                float breath = (Random.value * 2f - 1f) * 0.25f;
                float envelope = Mathf.Sin(Mathf.Clamp01(t * 1.4f) * Mathf.PI) * Mathf.Exp(-t * 2.5f);

                data[i] = Mathf.Clamp((voice * 0.45f + breath) * envelope, -1f, 1f);
            }

            AudioClip clip = AudioClip.Create(clipName, samples, 1, SampleRate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
