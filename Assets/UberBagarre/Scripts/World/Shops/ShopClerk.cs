using UberBagarre.Story;
using UberBagarre.View;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UberBagarre.World
{
    /// <summary>
    /// La personne derrière le comptoir.
    ///
    /// Elle respire (l'attente capturée des passants), se tourne vers le client qui approche et
    /// le salue une fois — pas à chaque pas, et « bonjour » ou « bonsoir » selon l'heure. Quand
    /// on lui parle (<see cref="BeginService"/>), elle se tourne vraiment vers lui et, s'il y a
    /// un comptoir devant elle, s'y accoude : le buste se penche, les avant-bras se posent à
    /// plat sur le comptoir (bras placés par IK par-dessus l'animation), la tête se relève pour
    /// regarder le client dans les yeux. Elle hoche la tête quand elle parle.
    /// </summary>
    [DefaultExecutionOrder(200)]
    public class ShopClerk : MonoBehaviour
    {
        [SerializeField] private Animator _animator;
        [SerializeField] private MocapLibrary _library;
        [SerializeField] private SubtitleDisplay _subtitles;
        [SerializeField] private string _speaker = "VENDEUR";
        [SerializeField] private string _greeting = "Bonjour.";
        [SerializeField, Min(1f)] private float _greetDistance = 4f;

        [SerializeField]
        [Tooltip("La marche calculee du corps : coupee quand l'attente capturee joue.")]
        private Behaviour _locomotion;

        private PlayableGraph _graph;
        private Quaternion _rest;
        private bool _greeted;
        private float _awayFor;
        private Shop _shop;
        private int _lines;

        // Le service : le client, l'accoudoir, la parole.
        private Transform _customer;
        private bool _serving;
        private float _lean;
        private bool _counterChecked;
        private bool _hasCounter;
        private float _counterY;
        private float _talkUntil;
        private BodyRig _rig;
        private Quaternion _leftHandRest = Quaternion.identity;
        private Quaternion _rightHandRest = Quaternion.identity;
        private bool _handsMeasured;

        public string Speaker { get { return _speaker; } }

        /// <summary>Quand il a parlé pour la dernière fois (pour ne pas saluer deux fois de suite).</summary>
        public float LastSpoke { get; private set; } = -100f;

        /// <summary>Accoudé (ou en train de s'accouder) au comptoir.</summary>
        public bool Serving { get { return _serving; } }

        /// <summary>Le haut du corps, pour cadrer le plan du comptoir.</summary>
        public Vector3 HeadPosition
        {
            get
            {
                if (_rig != null && _rig.Head != null) return _rig.Head.position;
                return transform.position + Vector3.up * 1.6f;
            }
        }

        public Transform HeadBone
        {
            get { return _rig != null ? _rig.Head : null; }
        }

        private void Awake()
        {
            _rig = GetComponentInChildren<BodyRig>(true);
            if (_rig != null)
            {
                // Le repère réel de chaque main (vers où pointent les doigts, de quel côté est le
                // pouce), mesuré sur la pose de liaison : c'est lui qu'on oriente ensuite, quelle
                // que soit la convention d'axes des os.
                _handsMeasured = MeasureHand(_rig.LeftArm, false, out _leftHandRest) &
                                 MeasureHand(_rig.RightArm, true, out _rightHandRest);
            }
        }

        private static bool MeasureHand(IkLimb arm, bool right, out Quaternion rest)
        {
            rest = Quaternion.identity;
            if (arm == null || arm.End == null) return false;
            Transform wrist = arm.End;
            Transform middle = FindDeep(wrist, "Majeur1");
            Transform index = FindDeep(wrist, "Index1");
            Transform little = FindDeep(wrist, "Auriculaire1");
            if (middle == null || index == null || little == null) return false;

            Vector3 fingers = (middle.position - wrist.position).normalized;
            Vector3 thumbSide = (index.position - little.position).normalized;
            Vector3 palm = Vector3.Cross(fingers, thumbSide) * (right ? 1f : -1f);
            if (palm.sqrMagnitude < 1e-6f) return false;
            rest = Quaternion.Inverse(Quaternion.LookRotation(fingers, palm)) * wrist.rotation;
            return true;
        }

        private static Transform FindDeep(Transform root, string suffix)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.EndsWith(suffix)) return child;
                Transform found = FindDeep(child, suffix);
                if (found != null) return found;
            }

            return null;
        }

        private void Start()
        {
            _rest = transform.rotation;

            if (_library == null || _animator == null || _animator.avatar == null || !_animator.avatar.isHuman) return;
            AnimationClip idle = _library.relaxedIdle != null ? _library.relaxedIdle : _library.combatIdle;
            if (idle == null) return;

            _graph = PlayableGraph.Create(name + " (attente)");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Corps", _animator);
            AnimationClipPlayable clip = AnimationClipPlayable.Create(_graph, idle);
            clip.SetTime(Random.value * idle.length);
            output.SetSourcePlayable(clip);
            _animator.enabled = true;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.CullCompletely;
            _graph.Play();
            if (_locomotion != null) _locomotion.enabled = false;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        /// <summary>Le magasin se présente à son vendeur (le nom, l'heure d'accueil).</summary>
        public void Bind(Shop shop)
        {
            _shop = shop;
        }

        // ------------------------------------------------------------------ service

        /// <summary>Le client lui parle : il se tourne vers lui et s'accoude au comptoir s'il y en a un.</summary>
        public void BeginService(Transform customer)
        {
            _customer = customer;
            _serving = true;
            if (!_counterChecked) FindCounter();
        }

        public void EndService()
        {
            _serving = false;
            _customer = null;
        }

        /// <summary>Une réplique (sous-titre et voix), avec les hochements de tête qui vont avec.</summary>
        public void Say(string text)
        {
            if (string.IsNullOrEmpty(text)) return;
            if (_subtitles != null) _subtitles.Play(DialogueLine.Say(_speaker, text));
            _talkUntil = Time.time + Mathf.Clamp(text.Length * 0.06f, 1f, 5f);
            LastSpoke = Time.time;
            _lines++;
        }

        /// <summary>L'accueil, selon le commerce et l'heure.</summary>
        public string Greeting()
        {
            if (_shop == null) return _greeting;
            float hour = WorldClock.Instance != null ? WorldClock.Instance.Hour : 12f;
            return ShopTalk.Greeting(_shop.Kind, hour, Random.Range(0, 1000));
        }

        /// <summary>Cherche le comptoir devant soi : une surface entre 75 cm et 1,25 m, à portée de bras.</summary>
        private void FindCounter()
        {
            _counterChecked = true;
            _hasCounter = false;
            Vector3 feet = transform.position;
            float[] reach = { 0.42f, 0.55f, 0.32f };
            for (int i = 0; i < reach.Length; i++)
            {
                Vector3 from = feet + transform.forward * reach[i] + Vector3.up * 1.4f;
                RaycastHit[] hits = Physics.RaycastAll(from, Vector3.down, 1.0f, ~0, QueryTriggerInteraction.Ignore);
                float best = -1f;
                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider.transform.IsChildOf(transform)) continue;
                    if (hits[h].normal.y < 0.7f) continue;
                    float height = hits[h].point.y - feet.y;
                    if (height < 0.75f || height > 1.25f) continue;
                    if (height > best) best = height;
                }

                if (best > 0f)
                {
                    _hasCounter = true;
                    _counterY = feet.y + best;
                    return;
                }
            }
        }

        // ------------------------------------------------------------------ chaque image

        private void Update()
        {
            Transform viewer = _serving && _customer != null ? _customer : SwingDoor.Viewer;
            if (viewer == null) return;

            Vector3 to = viewer.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            bool near = distance < _greetDistance;

            // Tourné vers le client, sans jamais lui tourner complètement le dos au comptoir —
            // sauf pendant le service, où il lui fait face.
            Quaternion wanted = (near || _serving) && distance > 0.2f ? Quaternion.LookRotation(to / distance, Vector3.up) : _rest;
            float limit = _serving ? 110f : 75f;
            if (Quaternion.Angle(_rest, wanted) > limit) wanted = Quaternion.RotateTowards(_rest, wanted, limit);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-(_serving ? 6f : 4f) * Time.deltaTime));

            if (near && !_greeted && !_serving)
            {
                _greeted = true;
                Say(Greeting());
            }

            // Parti depuis un moment : il saluera de nouveau la prochaine fois.
            _awayFor = near ? 0f : _awayFor + Time.deltaTime;
            if (_awayFor > 20f) _greeted = false;

            float target = _serving && _hasCounter ? 1f : 0f;
            _lean = Mathf.MoveTowards(_lean, target, Time.deltaTime / 0.6f);
        }

        private void LateUpdate()
        {
            if (_rig == null) return;
            float w = _lean * _lean * (3f - 2f * _lean);
            float talk = Time.time < _talkUntil ? 1f : 0f;
            if (w < 0.001f && talk <= 0f) return;

            Vector3 right = transform.right;
            Vector3 forward = transform.forward;

            if (w > 0.001f)
            {
                // Le buste se penche sur le comptoir.
                if (_rig.Spine != null) _rig.Spine.rotation = Quaternion.AngleAxis(13f * w, right) * _rig.Spine.rotation;
                if (_rig.Chest != null) _rig.Chest.rotation = Quaternion.AngleAxis(11f * w, right) * _rig.Chest.rotation;

                if (_handsMeasured)
                {
                    PlaceForearm(_rig.LeftArm, _leftHandRest, -1f, w, right, forward);
                    PlaceForearm(_rig.RightArm, _rightHandRest, 1f, w, right, forward);
                }
            }

            Transform head = _rig.Head;
            if (head == null) return;

            // La tête regarde le client (ce qui compense le buste penché).
            if (_customer != null)
            {
                Vector3 eye = _customer.position + Vector3.up * 1.55f;
                Camera main = Camera.main;
                if (main != null && (main.transform.position - _customer.position).sqrMagnitude < 4f) eye = main.transform.position;
                Vector3 toCustomer = (eye - head.position).normalized;
                Vector3 facing = Quaternion.AngleAxis(24f * w, right) * forward;
                Quaternion look = Quaternion.FromToRotation(facing, toCustomer);
                head.rotation = Quaternion.Slerp(Quaternion.identity, look, 0.75f * Mathf.Max(w, _serving ? 0.6f : 0f)) * head.rotation;
            }

            // Il parle : de petits hochements.
            if (talk > 0f)
            {
                float nod = Mathf.Sin(Time.time * 9f) * 2.2f + Mathf.Sin(Time.time * 3.7f) * 1.4f;
                head.rotation = Quaternion.AngleAxis(nod, right) * head.rotation;
            }
        }

        /// <summary>Un avant-bras à plat sur le comptoir, paume vers le bas, les doigts vers l'autre main.</summary>
        private void PlaceForearm(IkLimb arm, Quaternion handRest, float side, float w, Vector3 right, Vector3 forward)
        {
            if (arm == null || arm.End == null || arm.Upper == null || arm.Lower == null) return;

            Vector3 target = transform.position + forward * 0.44f + right * (side * 0.14f);
            target.y = _counterY + 0.035f;

            // Doigts vers l'avant et un peu vers l'autre main, paume à plat vers le comptoir.
            Vector3 fingers = (forward * 0.88f - right * (side * 0.47f)).normalized;
            Quaternion rotation = Quaternion.LookRotation(fingers, Vector3.down) * handRest;

            Vector3 wrist = arm.End.position;
            Vector3 elbow = arm.Lower.position;
            Vector3 animatedPole = elbow - (arm.Upper.position + wrist) * 0.5f;
            Vector3 pole = Vector3.Lerp(animatedPole.normalized, (-Vector3.up * 0.8f + right * (side * 0.5f) - forward * 0.25f).normalized, w);

            arm.ApplyWorldPose(Vector3.Lerp(wrist, target, w), Quaternion.Slerp(arm.End.rotation, rotation, w), pole);
            arm.UpdateTwist();
        }

        public void Configure(Animator animator, MocapLibrary library, SubtitleDisplay subtitles, string speaker, string greeting,
            Behaviour locomotion)
        {
            _locomotion = locomotion;
            _animator = animator;
            _library = library;
            _subtitles = subtitles;
            _speaker = speaker;
            _greeting = greeting;
        }
    }
}
