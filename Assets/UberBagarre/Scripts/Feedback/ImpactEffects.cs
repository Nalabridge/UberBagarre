using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Ce qui gicle au contact : gouttelettes (sueur, salive, et du sang sur un gros coup au
    /// visage), bouffée de poussière de tissu sur un coup au corps, éclair très bref au point
    /// d'impact.
    ///
    /// Tout est fabriqué à l'exécution — systèmes de particules, texture ronde, matière — sur
    /// un shader toujours présent dans les builds (Sprites/Default). Aucun asset à câbler :
    /// n'importe quel coup, du joueur comme de l'ennemi, appelle <see cref="Spawn"/>.
    ///
    /// Pas d'étoiles ni de chiffres : un coup se lit à ce qui en sort, comme dans un film.
    /// </summary>
    public class ImpactEffects : MonoBehaviour
    {
        private static ImpactEffects _instance;

        private ParticleSystem _droplets;
        private ParticleSystem _dust;
        private ParticleSystem _flash;
        private Material _material;
        private Texture2D _dot;

        private static ImpactEffects Instance
        {
            get
            {
                if (_instance != null) return _instance;

                GameObject go = new GameObject("Effets d'impact");
                _instance = go.AddComponent<ImpactEffects>();
                _instance.Build();
                return _instance;
            }
        }

        /// <summary>
        /// Un impact. <paramref name="direction"/> : sens du coup (vers où la matière part).
        /// <paramref name="strength"/> : 0 = effleuré, 1 = coup plein, au-delà = coup chargé.
        /// </summary>
        public static void Spawn(Vector3 point, Vector3 direction, float strength, bool head, bool blocked)
        {
            if (strength <= 0f) return;
            Instance.Emit(point, direction, strength, head, blocked);
        }

        // Tous les coups, de tout le monde : un seul point d'entrée, branché au lancement.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Hook()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;
            Combatant.AnyDamaged += OnAnyDamaged;
        }

        private static void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (info.Point == Vector3.zero) return;

            float strength = Mathf.Clamp(info.Amount / 14f, 0.3f, 2f);

            // Sur le joueur, l'impact est à vingt centimètres de l'oeil : on n'en garde qu'une
            // trace, sinon la gerbe masquerait l'écran à chaque coup reçu.
            if (victim != null && victim.Faction == Faction.Player) strength *= 0.35f;

            Spawn(info.Point, info.StrikeDirection, strength, info.Zone == HitZone.Head, info.Blocked);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            if (_material != null) Destroy(_material);
            if (_dot != null) Destroy(_dot);
        }

        private void Build()
        {
            _dot = new Texture2D(32, 32, TextureFormat.RGBA32, false);
            _dot.wrapMode = TextureWrapMode.Clamp;
            for (int y = 0; y < 32; y++)
            {
                for (int x = 0; x < 32; x++)
                {
                    float dx = (x + 0.5f) / 16f - 1f;
                    float dy = (y + 0.5f) / 16f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    a = a * a * (3f - 2f * a);
                    _dot.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            _dot.Apply();

            Shader shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("UI/Default");
            _material = new Material(shader);
            _material.mainTexture = _dot;

            _droplets = Create("Gouttelettes", true);
            _dust = Create("Poussiere", false);
            _flash = Create("Eclair", false);
        }

        private ParticleSystem Create(string name, bool stretched)
        {
            GameObject go = new GameObject(name);
            go.transform.SetParent(transform, false);

            ParticleSystem system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.playOnAwake = false;
            main.loop = false;
            main.maxParticles = 400;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.startSpeed = 0f;
            main.startLifetime = 0.4f;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;

            if (stretched)
            {
                renderer.renderMode = ParticleSystemRenderMode.Stretch;
                renderer.velocityScale = 0.035f;
                renderer.lengthScale = 1.2f;
            }

            system.Play();
            return system;
        }

        private void Emit(Vector3 point, Vector3 direction, float strength, bool head, bool blocked)
        {
            if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
            direction.Normalize();

            float s = Mathf.Clamp(strength, 0f, 2f);

            // Gouttelettes : un cône dans le sens du coup, retombant sous la gravité.
            int drops = Mathf.RoundToInt((blocked ? 4f : 10f) + s * (head ? 16f : 8f));
            bool blood = head && !blocked && s > 0.85f && Random.value < 0.55f;

            ParticleSystem.EmitParams p = new ParticleSystem.EmitParams();
            for (int i = 0; i < drops; i++)
            {
                Vector3 spread = Random.insideUnitSphere * 0.55f;
                Vector3 velocity = (direction + spread + Vector3.up * 0.15f).normalized * Random.Range(1.6f, 4.6f) * (0.6f + 0.4f * s);

                p.position = point + Random.insideUnitSphere * 0.02f;
                p.velocity = velocity;
                p.startLifetime = Random.Range(0.22f, 0.5f);
                p.startSize = Random.Range(0.005f, 0.013f);

                bool red = blood && Random.value < 0.7f;
                p.startColor = red
                    ? new Color(0.42f, 0.02f, 0.03f, 0.95f)
                    : new Color(0.86f, 0.9f, 0.95f, 0.55f);

                _droplets.Emit(p, 1);
            }

            ParticleSystem.MainModule dropMain = _droplets.main;
            dropMain.gravityModifier = 1.4f;

            // Bouffée : poussière de tissu ou de peau, lente, qui s'étale.
            int puffs = blocked ? 2 : (head ? 2 : 4);
            for (int i = 0; i < puffs; i++)
            {
                p.position = point + Random.insideUnitSphere * 0.03f;
                p.velocity = (direction * 0.6f + Random.insideUnitSphere * 0.35f) * (0.5f + 0.5f * s);
                p.startLifetime = Random.Range(0.25f, 0.45f);
                p.startSize = Random.Range(0.06f, 0.12f) * (0.8f + 0.4f * s);
                p.startColor = new Color(0.8f, 0.78f, 0.74f, head ? 0.10f : 0.18f);
                _dust.Emit(p, 1);
            }

            ParticleSystem.SizeOverLifetimeModule size = _dust.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 2.2f));

            ParticleSystem.ColorOverLifetimeModule fade = _dust.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            // Éclair : un seul point chaud, 60 ms. Il marque l'instant du contact, sans plus.
            p.position = point;
            p.velocity = Vector3.zero;
            p.startLifetime = 0.06f;
            p.startSize = (blocked ? 0.08f : 0.13f) * (0.8f + 0.3f * s);
            p.startColor = blocked ? new Color(0.75f, 0.85f, 1f, 0.35f) : new Color(1f, 0.93f, 0.8f, 0.45f);
            _flash.Emit(p, 1);
        }
    }
}
