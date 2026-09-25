using UberBagarre.Player;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Le mouchard de la caméra : il repère les sauts d'image et dit D'OÙ ils viennent.
    ///
    /// « La caméra saccade » peut avoir dix causes, et elles se ressemblent toutes à l'œil :
    /// un effet de caméra trop brutal, le personnage repoussé par une collision, le temps de
    /// calcul d'une image qui explose, le ramasse-miettes qui s'arrête, ou l'écran qui se
    /// déchire faute de synchronisation verticale. Ce composant mesure, image par image, ce qui
    /// a bougé entre deux rendus et le compare à ce que la souris et les jambes expliquent. Le
    /// reste est un saut, et le nœud du rig qui l'a produit est nommé dans la console.
    ///
    /// Il sert aussi l'overlay F1 : images par seconde, pire image des deux dernières
    /// secondes, passages du ramasse-miettes.
    /// </summary>
    [DefaultExecutionOrder(10000)]
    public class CameraDiagnostics : MonoBehaviour
    {
        [SerializeField] private PlayerLook _look;

        [SerializeField]
        [Tooltip("La racine du joueur : ce qui se deplace avec les jambes.")]
        private Transform _root;

        [SerializeField]
        [Tooltip("La tete (tangage). Tout ce qui est entre elle et la camera est un effet de camera.")]
        private Transform _head;

        [SerializeField]
        [Tooltip("Les noeuds du rig, de la tete vers la camera : chute, pas, secousse, recul.")]
        private Transform[] _nodes = new Transform[0];

        [SerializeField, Min(0.1f)]
        [Tooltip("Rotation en une image, hors souris, au-dela de laquelle on parle de saut (degres).")]
        private float _angleThreshold = 1.5f;

        [SerializeField, Min(0.01f)]
        [Tooltip("Deplacement en une image, au-dela de ce que la vitesse explique, juge anormal (m).")]
        private float _positionThreshold = 0.05f;

        [SerializeField] private bool _logJumps = true;

        private const int FrameWindow = 120;

        private readonly float[] _frameTimes = new float[FrameWindow];
        private int _frameIndex;
        private int _frameCount;

        private Quaternion[] _previousNodes;
        private Quaternion _previousRig = Quaternion.identity;
        private Vector3 _previousRoot;
        private Vector3 _previousRootVelocity;
        private bool _hasPrevious;
        private float _nextLog;

        private int _gcAtWindowStart;
        private float _gcWindowStart;

        /// <summary>Images par seconde, moyennées sur ~2 s.</summary>
        public static float Fps { get; private set; }

        /// <summary>Pire image des ~2 dernières secondes, en millisecondes.</summary>
        public static float WorstFrameMs { get; private set; }

        /// <summary>Passages du ramasse-miettes pendant les 10 dernières secondes.</summary>
        public static int GcPerTenSeconds { get; private set; }

        /// <summary>Sauts de caméra détectés depuis le lancement.</summary>
        public static int Jumps { get; private set; }

        /// <summary>Dernier saut détecté, en clair.</summary>
        public static string LastJump { get; private set; }

        private void Awake()
        {
            if (_look == null) _look = GetComponentInParent<PlayerLook>();
            _previousNodes = new Quaternion[_nodes.Length];
            _gcAtWindowStart = System.GC.CollectionCount(0);
            _gcWindowStart = Time.unscaledTime;
            LastJump = "aucun";
        }

        private void LateUpdate()
        {
            RecordFrameTime();
            RecordGarbage();
            DetectJumps();
        }

        private void RecordFrameTime()
        {
            float dt = Time.unscaledDeltaTime;
            _frameTimes[_frameIndex] = dt;
            _frameIndex = (_frameIndex + 1) % FrameWindow;
            _frameCount = Mathf.Min(_frameCount + 1, FrameWindow);

            float sum = 0f;
            float worst = 0f;

            for (int i = 0; i < _frameCount; i++)
            {
                sum += _frameTimes[i];
                worst = Mathf.Max(worst, _frameTimes[i]);
            }

            Fps = sum > 0f ? _frameCount / sum : 0f;
            WorstFrameMs = worst * 1000f;
        }

        private void RecordGarbage()
        {
            if (Time.unscaledTime - _gcWindowStart < 10f) return;

            int now = System.GC.CollectionCount(0);
            GcPerTenSeconds = now - _gcAtWindowStart;
            _gcAtWindowStart = now;
            _gcWindowStart = Time.unscaledTime;
        }

        private void DetectJumps()
        {
            if (_head == null || _root == null) return;

            // Tout ce qui sépare la tête de la caméra : les effets. La souris, elle, tourne la
            // tête et le corps — elle est donc exclue d'office.
            Quaternion rig = Quaternion.Inverse(_head.rotation) * transform.rotation;
            Vector3 root = _root.position;
            float dt = Mathf.Max(0.0001f, Time.deltaTime);

            if (!_hasPrevious)
            {
                StorePrevious(rig, root, Vector3.zero);
                _hasPrevious = true;
                return;
            }

            float rigJump = Quaternion.Angle(_previousRig, rig);

            // Un déplacement est anormal s'il s'écarte de celui de l'image précédente : une
            // marche, une course, même une esquive changent de vitesse progressivement, un
            // rejet de collision ou une téléportation non.
            Vector3 rootVelocity = (root - _previousRoot) / dt;
            Vector3 change = (rootVelocity - _previousRootVelocity) * dt;

            // Le saut vertical d'un saut volontaire (4 m/s d'un coup) n'est pas un defaut : on
            // ne regarde la verticale qu'au-dela d'un franc rejet.
            float rootJump = new Vector2(change.x, change.z).magnitude;
            if (Mathf.Abs(change.y) > 0.15f) rootJump = Mathf.Max(rootJump, Mathf.Abs(change.y));

            if (rigJump > _angleThreshold) Report(rigJump.ToString("0.0") + " deg d'effet de camera", Culprit());
            else if (rootJump > _positionThreshold && rootJump < 5f)
            {
                Report((rootJump * 100f).ToString("0") + " cm de deplacement brusque du joueur",
                    "collision ou poussee (CharacterController)");
            }

            StorePrevious(rig, root, rootVelocity);
        }

        private string Culprit()
        {
            string best = "?";
            float bestAngle = 0f;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i] == null) continue;

                float angle = Quaternion.Angle(_previousNodes[i], _nodes[i].localRotation);
                if (angle <= bestAngle) continue;

                bestAngle = angle;
                best = _nodes[i].name + " (" + angle.ToString("0.0") + " deg)";
            }

            return best;
        }

        private void Report(string what, string source)
        {
            Jumps++;
            LastJump = what + " : " + source;

            if (!_logJumps || Time.unscaledTime < _nextLog) return;
            _nextLog = Time.unscaledTime + 0.5f;

            Debug.Log("[UberBagarre] Saut de camera : " + LastJump +
                      "  (image " + (Time.unscaledDeltaTime * 1000f).ToString("0") + " ms, " +
                      Fps.ToString("0") + " i/s)", this);
        }

        private void StorePrevious(Quaternion rig, Vector3 root, Vector3 rootVelocity)
        {
            _previousRig = rig;
            _previousRoot = root;
            _previousRootVelocity = rootVelocity;

            for (int i = 0; i < _nodes.Length; i++)
            {
                if (_nodes[i] != null) _previousNodes[i] = _nodes[i].localRotation;
            }
        }
    }
}
