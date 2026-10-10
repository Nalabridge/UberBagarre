using UberBagarre.Combat;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Ce que fait la cible quand on arrive : elle n'attend pas plantée au milieu du trottoir,
    /// elle vit sa soirée. Elle fume contre un mur, parle au téléphone, boit une bière, tague
    /// une façade, boxe dans le vide, pêche au bout du quai, compte ses billets, s'engueule au
    /// téléphone, retire au distributeur.
    ///
    /// Le corps garde son animation (debout, détendu) ; ce composant la corrige par-dessus :
    /// un bras (ou les deux) guidé en IK vers la bouche, l'oreille, le mur, la tête qui
    /// regarde autour, et l'accessoire dans la main (cigarette, téléphone, bouteille,
    /// bombe de peinture, canne, liasse). Quand elle remarque le joueur, elle lâche tout —
    /// l'accessoire tombe — et se retourne.
    /// </summary>
    [DefaultExecutionOrder(250)]
    public class TargetActivity : MonoBehaviour
    {
        public enum Kind
        {
            Attend = 0,
            Fume = 1,
            Telephone = 2,
            Boit = 3,
            Tague = 4,
            Sentraine = 5,
            Peche = 6,
            Deal = 7,
            Dispute = 8,
            Distributeur = 9
        }

        private BodyRig _rig;
        private Kind _kind;
        private Vector3 _lookAt;
        private bool _hasLookAt;
        private float _weight;
        private bool _active;
        private float _clock;
        private float _seed;
        private TargetActivityKit _kit;

        private GameObject _prop;
        private GameObject _prop2;
        private Transform _ember;
        private ParticleSystem _fx;
        private GameObject _tag;
        private float _tagGrowth;
        private AudioSource _audio;
        private Quaternion _headOffset = Quaternion.identity;

        private readonly Quaternion[] _animated = new Quaternion[3];

        public Kind Activity { get { return _kind; } }

        /// <summary>Distance à laquelle elle remarque le joueur (absorbée, elle voit moins).</summary>
        public float NoticeDistance
        {
            get
            {
                switch (_kind)
                {
                    case Kind.Tague: return 4f;
                    case Kind.Telephone: case Kind.Dispute: return 4.8f;
                    case Kind.Peche: return 4.2f;
                    case Kind.Sentraine: return 8f;
                    default: return 6.2f;
                }
            }
        }

        public static Kind Parse(string activity)
        {
            switch ((activity ?? string.Empty).ToLowerInvariant())
            {
                case "fume": return Kind.Fume;
                case "telephone": case "voiture": return Kind.Telephone;
                case "boit": return Kind.Boit;
                case "tague": return Kind.Tague;
                case "sentraine": return Kind.Sentraine;
                case "peche": return Kind.Peche;
                case "deal": return Kind.Deal;
                case "dispute": return Kind.Dispute;
                case "distributeur": return Kind.Distributeur;
                default: return Kind.Attend;
            }
        }

        /// <summary>La phrase du signalement : ce qu'il fait, et ce que ça change pour toi.</summary>
        public static string Describe(Kind kind)
        {
            switch (kind)
            {
                case Kind.Fume: return "Il fume sa clope, adossé. Il ne se méfie pas.";
                case Kind.Telephone: return "Il est pendu au téléphone. Tu peux arriver dans son dos.";
                case Kind.Boit: return "Il boit une bière tout seul. Déjà un peu chaud.";
                case Kind.Tague: return "Il tague un mur. Concentré, il n'entendra rien.";
                case Kind.Sentraine: return "Il boxe dans le vide pour s'échauffer. Il te verra venir.";
                case Kind.Peche: return "Il pêche au bout du quai. Attention à l'eau.";
                case Kind.Deal: return "Il compte sa recette. Il a les mains prises.";
                case Kind.Dispute: return "Il s'engueule au téléphone. Il est déjà énervé.";
                case Kind.Distributeur: return "Il retire du liquide au distributeur.";
                default: return "Il attend quelqu'un. Il regarde autour de lui.";
            }
        }

        /// <summary>Ce qu'il lâche en te voyant (la réplique de la présentation).</summary>
        public static string Reaction(Kind kind)
        {
            switch (kind)
            {
                case Kind.Fume: return "Tu veux une clope ? ... Ah. C'est toi.";
                case Kind.Telephone: return "Attends, je te rappelle. Y a un mec, là.";
                case Kind.Boit: return "Tu veux ma bière dans la gueule ?";
                case Kind.Tague: return "Hé ! C'est mon mur. Dégage.";
                case Kind.Sentraine: return "Parfait. J'étais chaud, justement.";
                case Kind.Peche: return "Tu fais fuir le poisson, toi.";
                case Kind.Deal: return "Touche pas à l'oseille. Recule.";
                case Kind.Dispute: return "Toi aussi tu me cherches ? Ce soir c'est pas le jour.";
                case Kind.Distributeur: return "Tu regardais mon code ? Hein ?";
                default: return "Qu'est-ce que tu me veux, toi ?";
            }
        }

        /// <summary>Pose l'activité sur la cible qui vient d'apparaître.</summary>
        public static TargetActivity Begin(GameObject target, Kind kind, Vector3 lookAt, TargetActivityKit kit)
        {
            if (target == null) return null;

            TargetActivity activity = target.AddComponent<TargetActivity>();
            activity._kind = kind;
            activity._kit = kit;
            activity._lookAt = lookAt;
            activity._hasLookAt = lookAt.sqrMagnitude > 0.01f;
            activity._rig = target.GetComponentInChildren<BodyRig>(true);
            activity._seed = Random.Range(0f, 100f);
            activity._active = true;
            activity._weight = 1f;
            activity.Prepare();
            return activity;
        }

        /// <summary>Elle a vu le joueur : elle lâche ce qu'elle tient et se retourne.</summary>
        public void Stop()
        {
            if (!_active) return;
            _active = false;

            Drop(_prop);
            Drop(_prop2);
            _prop = null;
            _prop2 = null;

            if (_fx != null) _fx.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            if (_audio != null) _audio.Stop();
        }

        // ------------------------------------------------------------------ préparation

        private void Prepare()
        {
            _audio = gameObject.AddComponent<AudioSource>();
            _audio.playOnAwake = false;
            _audio.spatialBlend = 1f;
            _audio.maxDistance = 14f;
            _audio.rolloffMode = AudioRolloffMode.Linear;

            switch (_kind)
            {
                case Kind.Fume:
                    _prop = Stick("Cigarette", new Vector3(0.008f, 0.045f, 0.008f), Kit(k => k.Paper));
                    _ember = Stick("Braise", new Vector3(0.009f, 0.004f, 0.009f), Kit(k => k.Ember)).transform;
                    _ember.SetParent(_prop.transform, false);
                    _ember.localPosition = new Vector3(0f, 1.05f, 0f);
                    _ember.localScale = new Vector3(1.1f, 0.1f, 1.1f);
                    _fx = Smoke(new Color(0.8f, 0.8f, 0.82f, 0.35f));
                    break;

                case Kind.Telephone:
                case Kind.Dispute:
                    _prop = Block("Telephone", new Vector3(0.07f, 0.145f, 0.009f), Kit(k => k.Phone));
                    _audio.clip = Murmur(_kind == Kind.Dispute);
                    _audio.loop = true;
                    Core.ChannelSource.Attach(_audio, Core.AudioChannel.Ambience, _kind == Kind.Dispute ? 0.55f : 0.3f);
                    _audio.Play();
                    break;

                case Kind.Boit:
                    _prop = Stick("Bouteille", new Vector3(0.065f, 0.11f, 0.065f), Kit(k => k.Bottle));
                    break;

                case Kind.Tague:
                    _prop = Stick("Bombe", new Vector3(0.06f, 0.09f, 0.06f), Kit(k => k.Can));
                    _fx = Smoke(Kit(k => k.SprayColor, new Color(1f, 0.2f, 0.55f, 0.7f)));
                    _audio.clip = Hiss();
                    _audio.loop = true;
                    Core.ChannelSource.Attach(_audio, Core.AudioChannel.Effects, 0.25f);
                    PlaceTag();
                    break;

                case Kind.Peche:
                    _prop = Stick("Canne", new Vector3(0.018f, 1.2f, 0.018f), Kit(k => k.Rod));
                    break;

                case Kind.Deal:
                    _prop = Block("Liasse", new Vector3(0.075f, 0.018f, 0.16f), Kit(k => k.Cash));
                    _prop2 = Block("Sachet", new Vector3(0.05f, 0.012f, 0.06f), Kit(k => k.Paper));
                    break;
            }
        }

        private delegate T Pick<T>(TargetActivityKit kit);

        private Material Kit(Pick<Material> pick)
        {
            return _kit != null ? pick(_kit) : null;
        }

        private Color Kit(Pick<Color> pick, Color fallback)
        {
            return _kit != null ? pick(_kit) : fallback;
        }

        private GameObject Stick(string name, Vector3 size, Material material)
        {
            return Primitive(PrimitiveType.Cylinder, name, size, material);
        }

        private GameObject Block(string name, Vector3 size, Material material)
        {
            return Primitive(PrimitiveType.Cube, name, size, material);
        }

        private GameObject Primitive(PrimitiveType type, string name, Vector3 size, Material material)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;
            Collider c = go.GetComponent<Collider>();
            if (c != null) Destroy(c);
            go.transform.localScale = size;
            go.transform.SetParent(transform, true);

            Renderer r = go.GetComponent<Renderer>();
            if (r != null)
            {
                if (material != null) r.sharedMaterial = material;
                r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return go;
        }

        private ParticleSystem Smoke(Color color)
        {
            if (_kit == null || _kit.Particle == null) return null;

            GameObject go = new GameObject("Fumee");
            go.transform.SetParent(transform, false);
            ParticleSystem ps = go.AddComponent<ParticleSystem>();
            ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = ps.main;
            main.startLifetime = new ParticleSystem.MinMaxCurve(1.2f, 2.2f);
            main.startSpeed = new ParticleSystem.MinMaxCurve(0.1f, 0.3f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.08f, 0.2f);
            main.startColor = color;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 60;
            main.gravityModifier = -0.02f;

            ParticleSystem.EmissionModule emission = ps.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.shapeType = ParticleSystemShapeType.Cone;
            shape.angle = 12f;
            shape.radius = 0.01f;

            ParticleSystem.SizeOverLifetimeModule size = ps.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));

            ParticleSystem.ColorOverLifetimeModule fade = ps.colorOverLifetime;
            fade.enabled = true;
            Gradient g = new Gradient();
            g.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.15f), new GradientAlphaKey(0f, 1f) });
            fade.color = g;

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.sharedMaterial = _kit.Particle;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            ps.Play();
            return ps;
        }

        private void PlaceTag()
        {
            if (_kit == null || _kit.Graffiti == null) return;

            // Le mur le plus proche, devant, sur les côtés ou derrière : il se tourne vers lui.
            Vector3 origin = transform.position + Vector3.up * 1.4f;
            Vector3 forward = transform.forward;
            float distance = 0.9f;
            Vector3 normal = -forward;
            float best = float.MaxValue;

            for (int i = 0; i < 4; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, i * 90f, 0f) * transform.forward;
                RaycastHit hit;
                if (!Physics.Raycast(origin, direction, out hit, 2.5f, ~0, QueryTriggerInteraction.Ignore)) continue;
                if (hit.transform.IsChildOf(transform) || Mathf.Abs(hit.normal.y) > 0.5f || hit.distance >= best) continue;

                best = hit.distance;
                distance = hit.distance - 0.02f;
                normal = hit.normal;
                forward = direction;
            }

            if (best < float.MaxValue)
            {
                Vector3 flat = new Vector3(-normal.x, 0f, -normal.z);
                if (flat.sqrMagnitude > 0.01f) transform.rotation = Quaternion.LookRotation(flat.normalized, Vector3.up);
                forward = transform.forward;
                distance = Mathf.Max(0.4f, best - 0.02f);
            }

            _tag = GameObject.CreatePrimitive(PrimitiveType.Quad);
            _tag.name = "Tag";
            Collider c = _tag.GetComponent<Collider>();
            if (c != null) Destroy(c);
            _tag.transform.position = origin + forward * distance;
            _tag.transform.rotation = Quaternion.LookRotation(-normal, Vector3.up);
            _tag.transform.localScale = new Vector3(0.05f, 0.05f, 1f);
            Renderer r = _tag.GetComponent<Renderer>();
            r.sharedMaterial = _kit.Graffiti;
            r.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            _lookAt = _tag.transform.position;
            _hasLookAt = true;
        }

        private static void Drop(GameObject prop)
        {
            if (prop == null) return;

            prop.transform.SetParent(null, true);
            prop.AddComponent<BoxCollider>();
            Rigidbody body = prop.AddComponent<Rigidbody>();
            body.mass = 0.2f;
            body.AddForce(Random.insideUnitSphere * 0.6f + Vector3.up * 0.8f, ForceMode.VelocityChange);
            Destroy(prop, 40f);
        }

        private void OnDestroy()
        {
            if (_prop != null) Destroy(_prop);
            if (_prop2 != null) Destroy(_prop2);
            if (_tag != null) Destroy(_tag, 60f);
        }

        // ------------------------------------------------------------------ chaque image

        private void LateUpdate()
        {
            float dt = Time.deltaTime;
            _clock += dt;
            _weight = Mathf.MoveTowards(_weight, _active ? 1f : 0f, dt / (_active ? 0.6f : 0.25f));

            if (_rig == null || _weight <= 0.001f)
            {
                if (!_active && _weight <= 0.001f) enabled = false;
                return;
            }

            Transform root = transform;
            Vector3 forward = root.forward;
            Vector3 right = root.right;
            Vector3 up = Vector3.up;
            Transform head = _rig.Head != null ? _rig.Head : root;
            Vector3 mouth = head.position + forward * 0.11f - up * 0.07f;
            float t = _clock + _seed;

            switch (_kind)
            {
                case Kind.Fume:
                {
                    // Une bouffée toutes les six secondes : la main monte, la braise rougit, la fumée sort.
                    float cycle = Mathf.Repeat(t, 6.5f);
                    float toMouth = Bump(cycle, 0.6f, 2.2f);
                    Vector3 rest = Shoulder(HandSide.Right) - up * 0.42f + forward * 0.14f + right * 0.06f;
                    Vector3 target = Vector3.Lerp(rest, mouth + right * 0.03f, toMouth);
                    Arm(HandSide.Right, target, Quaternion.LookRotation(Vector3.Lerp(forward, up, toMouth * 0.7f), -right));
                    Hold(_prop, HandSide.Right, new Vector3(0f, 0.02f, 0.04f), Quaternion.Euler(80f, 0f, 0f));
                    if (_fx != null)
                    {
                        _fx.transform.position = cycle > 2.3f && cycle < 3.4f ? mouth : (_prop != null ? _prop.transform.position : mouth);
                        _fx.transform.rotation = Quaternion.LookRotation(cycle > 2.3f && cycle < 3.4f ? forward + up * 0.4f : up);
                        ParticleSystem.EmissionModule e = _fx.emission;
                        e.rateOverTime = cycle > 2.3f && cycle < 3.4f ? 26f : 2f;
                    }

                    Look(Mathf.Sin(t * 0.4f) * 25f, cycle < 2.5f ? -8f : 4f);
                    break;
                }

                case Kind.Telephone:
                case Kind.Dispute:
                {
                    Vector3 ear = head.position + right * 0.09f + forward * 0.03f - up * 0.02f;
                    Arm(HandSide.Right, ear, Quaternion.LookRotation(up, -right));
                    Hold(_prop, HandSide.Right, new Vector3(0f, 0.03f, 0.05f), Quaternion.Euler(0f, 90f, 0f));

                    if (_kind == Kind.Dispute)
                    {
                        // L'autre main gesticule : il explique, il s'énerve.
                        float g = Mathf.PerlinNoise(t * 1.3f, _seed);
                        Vector3 gesture = Shoulder(HandSide.Left) - up * (0.25f - 0.3f * g) + forward * (0.25f + 0.1f * Mathf.Sin(t * 5f)) - right * 0.05f;
                        Arm(HandSide.Left, gesture, Quaternion.LookRotation(forward, up));
                        Look(Mathf.Sin(t * 1.7f) * 30f, Mathf.Sin(t * 2.3f) * 6f);
                    }
                    else
                    {
                        Look(Mathf.Sin(t * 0.35f) * 45f, -6f);
                        root.Rotate(0f, Mathf.Sin(t * 0.25f) * 8f * Time.deltaTime, 0f);
                    }

                    break;
                }

                case Kind.Boit:
                {
                    float cycle = Mathf.Repeat(t, 7f);
                    float drink = Bump(cycle, 0.8f, 2.4f);
                    Vector3 rest = Shoulder(HandSide.Right) - up * 0.36f + forward * 0.2f + right * 0.03f;
                    Arm(HandSide.Right, Vector3.Lerp(rest, mouth + forward * 0.02f, drink),
                        Quaternion.LookRotation(Vector3.Lerp(forward, up + forward * 0.2f, drink), -right));
                    Hold(_prop, HandSide.Right, new Vector3(0f, 0.01f, 0.05f), Quaternion.Euler(Mathf.Lerp(0f, 60f, drink), 0f, 0f));
                    Look(Mathf.Sin(t * 0.5f) * 20f, -drink * 22f);
                    break;
                }

                case Kind.Tague:
                {
                    Vector3 wall = _hasLookAt ? _lookAt : root.position + up * 1.4f + forward * 0.9f;
                    Vector3 stroke = new Vector3(Mathf.Sin(t * 3.1f) * 0.18f, Mathf.Sin(t * 2.3f) * 0.12f, 0f);
                    Vector3 target = wall - forward * 0.35f + right * stroke.x + up * stroke.y;
                    Arm(HandSide.Right, target, Quaternion.LookRotation(forward, up));
                    Hold(_prop, HandSide.Right, new Vector3(0f, 0f, 0.05f), Quaternion.Euler(90f, 0f, 0f));
                    if (_fx != null)
                    {
                        _fx.transform.position = _prop != null ? _prop.transform.position + forward * 0.06f : target;
                        _fx.transform.rotation = Quaternion.LookRotation(forward);
                        ParticleSystem.EmissionModule e = _fx.emission;
                        e.rateOverTime = 40f;
                    }

                    if (_audio != null && !_audio.isPlaying && _active) _audio.Play();
                    if (_tag != null)
                    {
                        _tagGrowth = Mathf.Min(1f, _tagGrowth + dt * 0.04f);
                        float s = Mathf.Lerp(0.2f, 1.25f, _tagGrowth);
                        _tag.transform.localScale = new Vector3(s * 1.6f, s, 1f);
                    }

                    Look(0f, 4f);
                    break;
                }

                case Kind.Sentraine:
                {
                    // Deux directs, un crochet, un temps : le rythme d'un échauffement.
                    float beat = Mathf.Repeat(t * 1.6f, 4f);
                    Vector3 guardR = head.position + forward * 0.22f - up * 0.12f + right * 0.1f;
                    Vector3 guardL = head.position + forward * 0.28f - up * 0.1f - right * 0.1f;
                    float jab = Bump(beat, 0f, 0.35f);
                    float cross = Bump(beat, 1f, 0.4f);
                    float hook = Bump(beat, 2f, 0.45f);
                    Vector3 left = Vector3.Lerp(guardL, head.position + forward * 0.62f - up * 0.06f, jab);
                    Vector3 rightHand = Vector3.Lerp(guardR, head.position + forward * 0.64f - up * 0.06f, cross);
                    rightHand = Vector3.Lerp(rightHand, head.position + forward * 0.4f - up * 0.04f - right * 0.2f, hook);
                    Arm(HandSide.Left, left, Quaternion.LookRotation(forward, up));
                    Arm(HandSide.Right, rightHand, Quaternion.LookRotation(forward, up));
                    Fist(1f);
                    if (_rig.Pelvis != null) _rig.Pelvis.position += up * (Mathf.Abs(Mathf.Sin(t * 5f)) * 0.03f * _weight);
                    Look(Mathf.Sin(t * 0.7f) * 12f, 6f);
                    break;
                }

                case Kind.Peche:
                {
                    Vector3 hands = Shoulder(HandSide.Right) - up * 0.3f + forward * 0.32f - right * 0.08f;
                    Arm(HandSide.Right, hands, Quaternion.LookRotation(forward + up * 0.6f, -right));
                    Arm(HandSide.Left, hands - right * 0.12f + up * 0.05f, Quaternion.LookRotation(forward + up * 0.6f, right));
                    Hold(_prop, HandSide.Right, new Vector3(0f, 0f, 0.55f), Quaternion.Euler(-55f + Mathf.Sin(t * 0.8f) * 4f, 0f, 0f));
                    Look(Mathf.Sin(t * 0.2f) * 10f, 12f);
                    break;
                }

                case Kind.Deal:
                {
                    Vector3 belly = Shoulder(HandSide.Right) - up * 0.3f + forward * 0.3f - right * 0.15f;
                    float flick = Mathf.Repeat(t * 1.8f, 1f) < 0.2f ? 1f : 0f;
                    Arm(HandSide.Left, belly - right * 0.05f, Quaternion.LookRotation(forward, up));
                    Arm(HandSide.Right, belly + right * 0.07f + up * 0.04f * flick, Quaternion.LookRotation(forward, up));
                    Hold(_prop, HandSide.Left, new Vector3(0f, 0.02f, 0.05f), Quaternion.identity);
                    Hold(_prop2, HandSide.Right, new Vector3(0f, 0.02f, 0.05f), Quaternion.identity);
                    Look(Mathf.Sin(t * 0.9f) * 35f, -18f);
                    break;
                }

                case Kind.Distributeur:
                {
                    Vector3 keypad = _hasLookAt ? _lookAt + up * 1.05f - forward * 0.12f : root.position + up * 1.1f + forward * 0.5f;
                    float tap = Mathf.Repeat(t * 2.2f, 1f) < 0.25f ? 0.03f : 0f;
                    Arm(HandSide.Right, keypad - forward * (0.04f - tap), Quaternion.LookRotation(forward, up));
                    Look(0f, -10f);
                    break;
                }

                default:
                {
                    // Il attend : il regarde à droite, à gauche, sa montre.
                    float cycle = Mathf.Repeat(t, 9f);
                    float watch = Bump(cycle, 5f, 1.6f);
                    Vector3 wrist = Shoulder(HandSide.Left) - up * (0.45f - 0.3f * watch) + forward * (0.1f + 0.25f * watch);
                    Arm(HandSide.Left, wrist, Quaternion.LookRotation(forward, up));
                    Look(Mathf.Sin(t * 0.5f) * 55f * (1f - watch), watch > 0.3f ? -25f : 0f);
                    break;
                }
            }

            ApplyHead();
        }

        // ------------------------------------------------------------------ outils de pose

        /// <summary>Une bosse douce de 0 à 1 puis 0, commençant à <paramref name="start"/>.</summary>
        private static float Bump(float time, float start, float length)
        {
            float x = (time - start) / length;
            if (x <= 0f || x >= 1f) return 0f;
            return Mathf.Sin(x * Mathf.PI);
        }

        private Vector3 Shoulder(HandSide side)
        {
            IkLimb arm = _rig.Arm(side);
            return arm != null ? arm.RootPosition : transform.position + Vector3.up * 1.45f;
        }

        private void Arm(HandSide side, Vector3 target, Quaternion rotation)
        {
            IkLimb arm = _rig.Arm(side);
            if (arm == null || arm.End == null || arm.Upper == null || arm.Lower == null) return;

            _animated[0] = arm.Upper.localRotation;
            _animated[1] = arm.Lower.localRotation;
            _animated[2] = arm.End.localRotation;

            Vector3 animated = arm.End.position;
            Vector3 goal = Vector3.Lerp(animated, target, _weight);
            arm.ApplyWorldPose(goal, rotation);

            float blend = Mathf.Clamp01(_weight * 1.5f);
            arm.Upper.localRotation = Quaternion.Slerp(_animated[0], arm.Upper.localRotation, blend);
            arm.Lower.localRotation = Quaternion.Slerp(_animated[1], arm.Lower.localRotation, blend);
            arm.End.localRotation = Quaternion.Slerp(_animated[2], arm.End.localRotation, blend);

            HandRig hand = _rig.Hand(side);
            if (hand != null && _kind != Kind.Sentraine) hand.TargetGrip = 0.55f;
        }

        private void Fist(float grip)
        {
            HandRig left = _rig.Hand(HandSide.Left);
            HandRig right = _rig.Hand(HandSide.Right);
            if (left != null) left.TargetGrip = grip;
            if (right != null) right.TargetGrip = grip;
        }

        private void Hold(GameObject prop, HandSide side, Vector3 offset, Quaternion local)
        {
            if (prop == null) return;

            HandRig hand = _rig.Hand(side);
            Transform palm = hand != null && hand.Palm != null ? hand.Palm : (_rig.Arm(side) != null ? _rig.Arm(side).End : null);
            if (palm == null) return;

            Quaternion frame = Quaternion.LookRotation(transform.forward, Vector3.up);
            prop.transform.SetPositionAndRotation(palm.position + frame * offset, frame * local);
        }

        private float _lookYaw;
        private float _lookPitch;

        private void Look(float yaw, float pitch)
        {
            float k = 1f - Mathf.Exp(-3f * Time.deltaTime);
            _lookYaw = Mathf.Lerp(_lookYaw, yaw * _weight, k);
            _lookPitch = Mathf.Lerp(_lookPitch, pitch * _weight, k);
        }

        private void ApplyHead()
        {
            Transform neck = _rig.Neck;
            if (neck == null) return;

            // Rotation autour des axes du corps, ajoutée à l'animation : la tête suit l'activité.
            Quaternion look = Quaternion.AngleAxis(_lookYaw * 0.6f, Vector3.up) * Quaternion.AngleAxis(_lookPitch * 0.6f, transform.right);
            neck.rotation = look * neck.rotation;
        }

        // ------------------------------------------------------------------ sons

        private static AudioClip Murmur(bool angry)
        {
            AudioClip real = Core.SoundBank.Real(angry ? "Rue/engueulade" : "Rue/conversation");
            if (real != null) return real;

            const int rate = 16000;
            int length = rate * 4;
            float[] data = new float[length];
            System.Random rng = new System.Random(angry ? 13 : 7);
            float phase = 0f;
            float pitch = angry ? 190f : 150f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)rate;
                float syllable = Mathf.Sin(t * (angry ? 11f : 7f)) * 0.5f + 0.5f;
                float talking = Mathf.PerlinNoise(t * 1.3f, 0.3f) > 0.42f ? 1f : 0f;
                pitch = Mathf.Lerp(pitch, (angry ? 170f : 130f) + (float)rng.NextDouble() * 80f, 0.0008f);
                phase += pitch / rate;
                phase -= Mathf.Floor(phase);
                float voice = Mathf.Sin(phase * Mathf.PI * 2f) + 0.4f * Mathf.Sin(phase * Mathf.PI * 4f) + 0.2f * Mathf.Sin(phase * Mathf.PI * 6f);
                data[n] = voice * syllable * talking * (angry ? 0.28f : 0.18f);
            }

            AudioClip clip = AudioClip.Create(angry ? "Engueulade" : "Conversation", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        private static AudioClip Hiss()
        {
            AudioClip real = Core.SoundBank.Real("Rue/bombe_peinture");
            if (real != null) return real;

            const int rate = 16000;
            int length = rate;
            float[] data = new float[length];
            System.Random rng = new System.Random(3);
            float low = 0f;

            for (int n = 0; n < length; n++)
            {
                float noise = (float)(rng.NextDouble() * 2.0 - 1.0);
                low = Mathf.Lerp(low, noise, 0.6f);
                data[n] = (noise - low) * 0.3f;
            }

            AudioClip clip = AudioClip.Create("Bombe de peinture", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }
    }
}
