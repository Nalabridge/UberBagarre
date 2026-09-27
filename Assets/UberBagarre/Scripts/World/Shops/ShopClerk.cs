using UberBagarre.Story;
using UberBagarre.View;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UberBagarre.World
{
    /// <summary>
    /// La personne derrière le comptoir : elle respire (l'attente capturée des passants), se
    /// tourne vers le client qui approche et le salue une fois — pas à chaque pas.
    /// </summary>
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

        private void Update()
        {
            Transform viewer = SwingDoor.Viewer;
            if (viewer == null) return;

            Vector3 to = viewer.position - transform.position;
            to.y = 0f;
            float distance = to.magnitude;
            bool near = distance < _greetDistance;

            // Tourné vers le client, sans jamais lui tourner complètement le dos au comptoir.
            Quaternion wanted = near && distance > 0.2f ? Quaternion.LookRotation(to / distance, Vector3.up) : _rest;
            if (Quaternion.Angle(_rest, wanted) > 75f) wanted = Quaternion.RotateTowards(_rest, wanted, 75f);
            transform.rotation = Quaternion.Slerp(transform.rotation, wanted, 1f - Mathf.Exp(-4f * Time.deltaTime));

            if (near && !_greeted)
            {
                _greeted = true;
                if (_subtitles != null) _subtitles.Play(DialogueLine.Say(_speaker, _greeting));
            }

            // Parti depuis un moment : il saluera de nouveau la prochaine fois.
            _awayFor = near ? 0f : _awayFor + Time.deltaTime;
            if (_awayFor > 20f) _greeted = false;
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
