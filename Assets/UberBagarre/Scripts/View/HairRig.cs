using UnityEngine;
using UnityEngine.Rendering;

namespace UberBagarre.View
{
    /// <summary>
    /// Les cheveux et la barbe d'un personnage : deux coques posées sur l'os de la tête (voir
    /// <see cref="HairBuilder"/>), qui suivent la tête sans peau ni os en plus.
    ///
    /// Le joueur est en vue subjective : sa tête n'est pas dessinée (on est dedans), ses
    /// cheveux non plus — seulement leur ombre. Le barbier, la photo, la cinématique les
    /// montrent (<see cref="SetVisible"/>).
    /// </summary>
    public class HairRig : MonoBehaviour
    {
        [SerializeField] private HairProfile _profile;
        [SerializeField] private Transform _head;
        [SerializeField] private Material _material;
        [SerializeField] private HairCut _cut = HairCut.Courte;
        [SerializeField] private BeardStyle _beard = BeardStyle.Aucune;
        [SerializeField] private Color _color = new Color(0.045f, 0.038f, 0.034f);

        [SerializeField]
        [Tooltip("Coques fines (beaucoup de sommets) : le joueur, les personnages de l'histoire. Sinon, la foule.")]
        private bool _detailed;

        [SerializeField]
        [Tooltip("Vue subjective : les cheveux ne se voient pas, ils ne font que leur ombre.")]
        private bool _shadowsOnly;

        private MeshFilter _hairFilter;
        private MeshRenderer _hairRenderer;
        private MeshFilter _beardFilter;
        private MeshRenderer _beardRenderer;
        private MaterialPropertyBlock _block;
        private bool _visible;
        private bool _built;

        private static readonly int ColorId = Shader.PropertyToID("_Color");

        public HairCut Cut { get { return _cut; } }
        public BeardStyle Beard { get { return _beard; } }
        public Color HairColor { get { return _color; } }

        private void Start()
        {
            Apply();
        }

        /// <summary>Coiffe le personnage (reconstruit seulement ce qui change).</summary>
        public void Set(HairCut cut, BeardStyle beard, Color color)
        {
            bool same = _built && cut == _cut && beard == _beard && color == _color;
            _cut = cut;
            _beard = beard;
            _color = color;
            if (!same) Apply();
        }

        /// <summary>Montre les cheveux (barbier, photo) ou ne garde que leur ombre (vue subjective).</summary>
        public void SetVisible(bool visible)
        {
            _visible = visible;
            ApplyMode(_hairRenderer);
            ApplyMode(_beardRenderer);
        }

        public void Apply()
        {
            if (_profile == null || _head == null || _material == null) return;
            _built = true;
            if (_block == null) _block = new MaterialPropertyBlock();

            Mesh hair = HairBuilder.Hair(_profile, _cut, _detailed);
            Mesh beard = HairBuilder.Beard(_profile, _beard);
            Place(ref _hairFilter, ref _hairRenderer, "Cheveux", hair);
            Place(ref _beardFilter, ref _beardRenderer, "Barbe", beard);
        }

        private void Place(ref MeshFilter filter, ref MeshRenderer renderer, string name, Mesh mesh)
        {
            if (filter == null)
            {
                Transform existing = _head.Find(name);
                GameObject go = existing != null ? existing.gameObject : new GameObject(name);
                go.transform.SetParent(_head, false);
                go.transform.localPosition = Vector3.zero;
                go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;
                go.layer = _head.gameObject.layer;
                filter = go.GetComponent<MeshFilter>();
                if (filter == null) filter = go.AddComponent<MeshFilter>();
                renderer = go.GetComponent<MeshRenderer>();
                if (renderer == null) renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _material;
                renderer.lightProbeUsage = LightProbeUsage.BlendProbes;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
            }

            filter.sharedMesh = mesh;
            renderer.enabled = mesh != null;
            if (mesh == null) return;

            _block.Clear();
            _block.SetColor(ColorId, _color);
            renderer.SetPropertyBlock(_block);
            ApplyMode(renderer);
        }

        private void ApplyMode(MeshRenderer renderer)
        {
            if (renderer == null) return;
            bool hidden = _shadowsOnly && !_visible;
            renderer.shadowCastingMode = hidden ? ShadowCastingMode.ShadowsOnly : ShadowCastingMode.On;
        }

        /// <summary>Le constructeur des personnages y écrit la tête et la coupe.</summary>
        public void Configure(HairProfile profile, Transform head, Material material, HairCut cut, BeardStyle beard, Color color,
            bool detailed, bool shadowsOnly)
        {
            _profile = profile;
            _head = head;
            _material = material;
            _cut = cut;
            _beard = beard;
            _color = color;
            _detailed = detailed;
            _shadowsOnly = shadowsOnly;
        }
    }
}
