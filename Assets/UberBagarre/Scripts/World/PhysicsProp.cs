using System;
using UberBagarre.Feedback;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Un objet du décor qui réagit : il se renverse, roule, vole, et fait du bruit en tombant.
    ///
    /// Le combat se passe au milieu de bouteilles, de plots, de caisses et de sacs poubelle.
    /// S'ils restent collés au sol quand on les frappe ou qu'on y projette quelqu'un, le décor
    /// se lit comme un fond peint — et le coup perd la moitié de son poids, parce que rien
    /// autour ne témoigne de sa violence. Un plot qui part en tournoyant sous un coup de pied
    /// raté dit « ce coup était fort » mieux que n'importe quel chiffre de dégâts.
    ///
    /// Le son vient de la même synthèse que les coups, pas d'une banque d'effets : sinon le
    /// décor sonnerait comme un autre jeu plaqué sur celui-ci.
    /// </summary>
    [RequireComponent(typeof(Rigidbody))]
    [DisallowMultipleComponent]
    public class PhysicsProp : MonoBehaviour
    {
        public enum Matter
        {
            Verre = 0,
            Bois = 1,
            Metal = 2,
            Plastique = 3,
            Mou = 4
        }

        [SerializeField] private Matter _matter = Matter.Bois;

        [SerializeField, Min(0f)]
        [Tooltip("Vitesse de choc en dessous de laquelle rien ne sonne. Sans seuil, un objet qui " +
                 "se pose en glissant emet une rafale de petits bruits.")]
        private float _minImpactSpeed = 1.4f;

        [SerializeField, Range(0f, 1f)] private float _volume = 0.5f;

        [SerializeField, Min(0.02f)]
        [Tooltip("Intervalle minimal entre deux sons. Un objet qui rebondit trois fois en une " +
                 "demi-seconde doit faire trois bruits, pas trente.")]
        private float _soundCooldown = 0.09f;

        private static AudioClip[] _clips;

        private Rigidbody _body;
        private AudioSource _source;
        private float _nextSound;
        private float _quietUntil;

        /// <summary>Nombre de fois que l'objet a été frappé directement (pas bousculé).</summary>
        public int TimesStruck { get; private set; }

        public Rigidbody Body { get { return _body; } }

        /// <summary>Déclenché quand un coup (poing, pied) touche l'objet.</summary>
        public event Action<PhysicsProp> Struck;

        private void Awake()
        {
            _body = GetComponent<Rigidbody>();

            _source = GetComponent<AudioSource>();
            if (_source == null) _source = gameObject.AddComponent<AudioSource>();

            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Logarithmic;
            _source.minDistance = 1.2f;
            _source.maxDistance = 30f;

            // Au chargement, les objets se posent et se touchent : sans ce silence, la scène
            // démarre sur un fracas de bouteilles qui n'est arrivé à personne.
            _quietUntil = Time.time + 1.2f;
        }

        /// <summary>Appelé par une hitbox qui vient de frapper l'objet.</summary>
        public void NotifyStruck(Vector3 point, Vector3 impulse)
        {
            TimesStruck++;
            PlaySound(Mathf.Clamp01(impulse.magnitude / 8f) * 0.7f + 0.3f);

            Action<PhysicsProp> handler = Struck;
            if (handler != null) handler(this);
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;
            if (speed < _minImpactSpeed) return;

            PlaySound(Mathf.Clamp01((speed - _minImpactSpeed) / 6f) * 0.8f + 0.2f);
        }

        private void PlaySound(float strength)
        {
            if (_source == null || Time.time < _quietUntil || Time.time < _nextSound) return;

            _nextSound = Time.time + _soundCooldown;

            AudioClip clip = Clip(_matter);
            if (clip == null) return;

            _source.pitch = UnityEngine.Random.Range(0.9f, 1.12f);
            _source.PlayOneShot(clip, _volume * strength);
        }

        /// <summary>
        /// Un clip par matière, fabriqué une fois et partagé par tous les objets. Ce sont les
        /// trois réglages de la synthèse qui font la matière : un verre est bref et clair, une
        /// caisse est moyenne, un sac poubelle n'a presque aucun aigu.
        /// </summary>
        private static AudioClip Clip(Matter matter)
        {
            if (_clips == null)
            {
                _clips = new AudioClip[5];
                _clips[(int)Matter.Verre] = ImpactAudio.CreateImpactClip("Choc_Verre", 0.09f, 0.95f, 0.2f, 0.02f);
                _clips[(int)Matter.Bois] = ImpactAudio.CreateImpactClip("Choc_Bois", 0.16f, 0.42f, 0.4f, 0.35f);
                _clips[(int)Matter.Metal] = ImpactAudio.CreateImpactClip("Choc_Metal", 0.24f, 0.78f, 0.2f, 0.18f);
                _clips[(int)Matter.Plastique] = ImpactAudio.CreateImpactClip("Choc_Plastique", 0.11f, 0.6f, 0.3f, 0.12f);
                _clips[(int)Matter.Mou] = ImpactAudio.CreateImpactClip("Choc_Mou", 0.18f, 0.16f, 0.8f, 0.55f);
            }

            int index = (int)matter;
            return index >= 0 && index < _clips.Length ? _clips[index] : null;
        }
    }
}
