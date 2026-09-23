using System;
using UberBagarre.Combat;
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

        [Header("Arme de fortune")]
        [SerializeField, Min(0f)]
        [Tooltip("Vitesse de choc a partir de laquelle un objet en VERRE eclate, une fois lance " +
                 "ou frappe. Une bouteille qui se pose ne casse pas ; une bouteille qui percute " +
                 "un crane, si.")]
        private float _shatterSpeed = 7.5f;

        private static AudioClip[] _clips;

        private Rigidbody _body;
        private AudioSource _source;
        private float _nextSound;
        private float _quietUntil;

        // --- projectile
        private GameObject _thrower;
        private Faction _throwerFaction;
        private float _throwDamage;
        private float _throwImpact;
        private float _dangerousUntil;
        private bool _hitSomeone;
        private bool _shattered;

        /// <summary>Nombre de fois que l'objet a été frappé directement (pas bousculé).</summary>
        public int TimesStruck { get; private set; }

        public Rigidbody Body { get { return _body; } }

        /// <summary>Déclenché quand un coup (poing, pied) touche l'objet.</summary>
        public event Action<PhysicsProp> Struck;

        /// <summary>Déclenché quand l'objet, lancé, touche quelqu'un.</summary>
        public static event Action<PhysicsProp, Combatant> AnyHitCombatant;

        public Matter Kind { get { return _matter; } }
        public bool IsShattered { get { return _shattered; } }

        /// <summary>Le dernier qui a lancé ou frappé l'objet — celui au nom de qui il blesse.</summary>
        public GameObject Thrower { get { return _thrower; } }

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
        public void NotifyStruck(Vector3 point, Vector3 impulse, GameObject striker, Faction strikerFaction)
        {
            // Un objet frappé devient dangereux un court instant, au nom de celui qui l'a frappé :
            // une bouteille envoyée d'un coup de pied dans la figure de quelqu'un doit lui faire
            // mal, pas juste rebondir sur lui.
            float mass = _body != null ? _body.mass : 1f;

            _thrower = striker;
            _throwerFaction = strikerFaction;
            _throwDamage = Mathf.Clamp(5f + mass * 2.5f, 6f, 18f);
            _throwImpact = 4f;
            _dangerousUntil = Mathf.Max(_dangerousUntil, Time.time + 0.7f);
            _hitSomeone = false;

            TimesStruck++;
            PlaySound(Mathf.Clamp01(impulse.magnitude / 8f) * 0.7f + 0.3f);

            Action<PhysicsProp> handler = Struck;
            if (handler != null) handler(this);
        }

        /// <summary>
        /// L'objet vient d'être LANCÉ par quelqu'un : pendant deux secondes et demie, il blesse
        /// qui il touche, au nom de celui qui l'a lancé.
        ///
        /// Les dégâts passent par une hurtbox de la victime, donc par tout le chemin normal d'un
        /// coup — zone, défense, garde, recul, physique des os, bleus. Une bouteille sur le crâne
        /// compte comme un coup à la tête, et une garde levée l'arrête.
        /// </summary>
        public void Launch(GameObject thrower, Faction faction, float damage, float impactForce)
        {
            _thrower = thrower;
            _throwerFaction = faction;
            _throwDamage = damage;
            _throwImpact = impactForce;
            _dangerousUntil = Time.time + 2.5f;
            _hitSomeone = false;
        }

        private void OnCollisionEnter(Collision collision)
        {
            float speed = collision.relativeVelocity.magnitude;

            Vector3 point = collision.contactCount > 0 ? collision.GetContact(0).point : transform.position;
            TryHit(collision.collider, point, speed);

            if (_shattered) return;

            if (speed >= _shatterSpeed && _matter == Matter.Verre && Time.time <= _dangerousUntil)
            {
                Shatter(point);
                return;
            }

            if (speed < _minImpactSpeed) return;

            PlaySound(Mathf.Clamp01((speed - _minImpactSpeed) / 6f) * 0.8f + 0.2f);
        }

        /// <summary>Les hurtbox sont souvent des déclencheurs : un projectile les traverse sans collision.</summary>
        private void OnTriggerEnter(Collider other)
        {
            if (_body == null) return;

            TryHit(other, other.ClosestPoint(transform.position), _body.linearVelocity.magnitude);
        }

        private void TryHit(Collider other, Vector3 point, float speed)
        {
            if (_hitSomeone || _shattered || Time.time > _dangerousUntil || other == null) return;
            if (speed < 3f) return;

            Combatant victim = other.GetComponentInParent<Combatant>();
            if (victim == null || !victim.IsAlive) return;
            if (_thrower != null && victim.gameObject == _thrower) return;
            if (_thrower != null && victim.Faction == _throwerFaction) return;

            Hurtbox zone = NearestHurtbox(victim, point);
            if (zone == null) return;

            _hitSomeone = true;

            Vector3 velocity = _body != null ? _body.linearVelocity : Vector3.zero;

            DamageInfo info = new DamageInfo();
            info.Amount = Mathf.Max(1f, _throwDamage) * Mathf.Clamp(speed / 14f, 0.5f, 1.3f);
            info.Point = point;
            info.Velocity = velocity;
            info.Direction = velocity.sqrMagnitude > 0.01f ? velocity.normalized : transform.forward;
            info.ImpactForce = _throwImpact;
            info.Attacker = _thrower;
            info.AttackerFaction = _throwerFaction;

            zone.Receive(info);

            Action<PhysicsProp, Combatant> handler = AnyHitCombatant;
            if (handler != null) handler(this, victim);

            if (_matter == Matter.Verre) Shatter(point);
        }

        private static Hurtbox NearestHurtbox(Combatant victim, Vector3 point)
        {
            Hurtbox[] zones = victim.GetComponentsInChildren<Hurtbox>();
            Hurtbox best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < zones.Length; i++)
            {
                Collider collider = zones[i].GetComponent<Collider>();
                if (collider == null || !collider.enabled) continue;

                float distance = (collider.ClosestPoint(point) - point).sqrMagnitude;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = zones[i];
            }

            return best;
        }

        /// <summary>
        /// Le verre éclate : l'objet disparaît, une poignée d'éclats partent avec son élan.
        ///
        /// Les éclats sont de vrais petits corps rigides, pas des particules : ils rebondissent
        /// sur le trottoir, glissent dans les flaques et se voient dans le reflet du bitume.
        /// Ils disparaissent au bout de quelques secondes — une rue qui se couvre d'éclats à
        /// chaque bagarre finirait par coûter plus cher que la bagarre.
        /// </summary>
        public void Shatter(Vector3 point)
        {
            if (_shattered) return;
            _shattered = true;

            _quietUntil = 0f;
            _nextSound = 0f;
            PlaySound(1f);

            Renderer[] renderers = GetComponentsInChildren<Renderer>();
            Material glass = renderers.Length > 0 ? renderers[0].sharedMaterial : null;
            for (int i = 0; i < renderers.Length; i++) renderers[i].enabled = false;

            Collider[] colliders = GetComponentsInChildren<Collider>();
            for (int i = 0; i < colliders.Length; i++) colliders[i].enabled = false;

            Vector3 inherited = _body != null ? _body.linearVelocity * 0.35f : Vector3.zero;

            if (_body != null)
            {
                _body.linearVelocity = Vector3.zero;
                _body.isKinematic = true;
            }

            for (int i = 0; i < 7; i++)
            {
                GameObject shard = GameObject.CreatePrimitive(PrimitiveType.Cube);
                shard.name = "Eclat";
                shard.transform.position = point + UnityEngine.Random.insideUnitSphere * 0.04f;
                shard.transform.rotation = UnityEngine.Random.rotation;
                shard.transform.localScale = new Vector3(
                    UnityEngine.Random.Range(0.012f, 0.035f),
                    UnityEngine.Random.Range(0.004f, 0.01f),
                    UnityEngine.Random.Range(0.012f, 0.03f));

                Renderer shardRenderer = shard.GetComponent<Renderer>();
                if (shardRenderer != null && glass != null) shardRenderer.sharedMaterial = glass;

                Rigidbody body = shard.AddComponent<Rigidbody>();
                body.mass = 0.02f;
                body.linearVelocity = inherited + UnityEngine.Random.insideUnitSphere * 2.6f + Vector3.up * 1.2f;
                body.angularVelocity = UnityEngine.Random.insideUnitSphere * 20f;
                body.collisionDetectionMode = CollisionDetectionMode.ContinuousDynamic;

                Destroy(shard, UnityEngine.Random.Range(3.5f, 5f));
            }

            // L'objet reste une seconde, invisible, le temps que le son se termine.
            Destroy(gameObject, 1f);
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
