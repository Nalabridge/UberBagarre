using System;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Les fractures d'une cible : à force de coups au même endroit, quelque chose casse.
    ///
    /// - le NEZ casse quand la tête a encaissé une bonne part de la vie (il saigne) ;
    /// - les CÔTES cassent sous les coups au corps (il récupère son souffle deux fois moins vite) ;
    /// - la JAMBE casse sous les coups de pied bas (il boite : il avance beaucoup moins vite).
    ///
    /// Les seuils sont une part de la vie maximale : un colosse encaisse plus avant de casser.
    /// L'entraînement du joueur (Puissance, Technique) les abaisse.
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public class TargetInjuries : MonoBehaviour
    {
        [SerializeField, Range(0.05f, 1f)] private float _noseThreshold = 0.30f;
        [SerializeField, Range(0.05f, 1f)] private float _ribsThreshold = 0.34f;
        [SerializeField, Range(0.05f, 1f)] private float _legThreshold = 0.16f;

        private Combatant _self;
        private float _head;
        private float _body;
        private float _legs;
        private AudioSource _source;
        private AudioClip _crack;

        public bool NoseBroken { get; private set; }
        public bool RibsBroken { get; private set; }
        public bool LegBroken { get; private set; }

        /// <summary>Une fracture : « nez », « cotes » ou « jambe ».</summary>
        public event Action<TargetInjuries, string> Fractured;

        /// <summary>Même chose, pour tous les combattants (l'appli, la foule, le HUD).</summary>
        public static event Action<TargetInjuries, string> AnyFractured;

        /// <summary>Plus haut = casse plus vite (l'entraînement du joueur). 0 = normal.</summary>
        public static float Ease { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            AnyFractured = null;
            Ease = 0f;
        }

        private void Awake()
        {
            _self = GetComponent<Combatant>();
            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.maxDistance = 25f;
        }

        private void OnEnable()
        {
            if (_self != null) _self.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_self != null) _self.Damaged -= OnDamaged;
        }

        private void OnDestroy()
        {
            if (_crack != null) Destroy(_crack);
        }

        private void OnDamaged(Combatant self, DamageInfo info)
        {
            if (info.Blocked || info.AttackerFaction != Faction.Player) return;

            float max = self.Health != null ? Mathf.Max(1f, self.Health.MaxHealth) : 100f;
            float scale = 1f / Mathf.Max(0.3f, 1f - Mathf.Clamp01(Ease));
            float share = info.Amount / max * scale;

            switch (info.Zone)
            {
                case HitZone.Head:
                    _head += share;
                    if (!NoseBroken && _head >= _noseThreshold) Break("nez");
                    break;

                case HitZone.Body:
                case HitZone.Arm:
                    _body += share;
                    if (!RibsBroken && _body >= _ribsThreshold) Break("cotes");
                    break;

                case HitZone.Leg:
                    _legs += share;
                    if (!LegBroken && _legs >= _legThreshold) Break("jambe");
                    break;
            }
        }

        private void Break(string what)
        {
            switch (what)
            {
                case "nez": NoseBroken = true; break;
                case "cotes": RibsBroken = true; break;
                case "jambe": LegBroken = true; break;
            }

            if (_self != null && _self.Stats != null)
            {
                if (what == "jambe")
                {
                    _self.Stats.AddModifier(new StatModifier { stat = StatType.MoveSpeed, percent = true, value = -0.45f, source = "Fracture" });
                }
                else if (what == "cotes")
                {
                    _self.Stats.AddModifier(new StatModifier { stat = StatType.Defense, percent = false, value = -6f, source = "Fracture" });
                    if (_self.Stamina != null) _self.Stamina.RegenMultiplier *= 0.5f;
                }
            }

            if (_crack == null) _crack = Crack();
            _source.PlayOneShot(_crack, 0.9f);

            Action<TargetInjuries, string> handler = Fractured;
            if (handler != null) handler(this, what);

            Action<TargetInjuries, string> any = AnyFractured;
            if (any != null) any(this, what);
        }

        /// <summary>Le craquement : un claquement sec, court, et un grain d'os.</summary>
        private static AudioClip Crack()
        {
            const int rate = 22050;
            int length = rate / 4;
            float[] data = new float[length];
            System.Random rng = new System.Random(71);

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)rate;
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                float snap = Mathf.Exp(-t * 90f);
                float grain = Mathf.Exp(-t * 22f) * (Mathf.Sin(t * 2f * Mathf.PI * 180f) > 0.6f ? 1f : 0.2f);
                data[n] = (noise * snap * 0.9f + noise * grain * 0.35f) * 0.8f;
            }

            AudioClip clip = AudioClip.Create("Fracture", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
