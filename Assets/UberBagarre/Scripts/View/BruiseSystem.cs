using System.Collections.Generic;
using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Marques laissées sur le corps là où les coups portent.
    ///
    /// C'est la seule trace persistante d'un échange. Les chiffres de dégâts disparaissent, la
    /// barre de vie est une abstraction : les bleus, eux, racontent où on a frappé. Ils rendent
    /// aussi lisible qu'on travaille au corps plutôt qu'à la tête.
    ///
    /// Les marques sont PEINTES dans la surface, pas posées dessus. La première version collait
    /// des ovales en relief sur le corps : de près on voyait une pastille, de loin une verrue.
    /// Ici, aucun objet n'est créé. Chaque morceau de corps porte la matière « UberBagarre/Peau »,
    /// et reçoit par un bloc de propriétés jusqu'à huit points d'impact (centre dans son espace
    /// propre, rayon, intensité) ; le shader assombrit la peau autour, avec un contour irrégulier
    /// et une marbrure. La marque suit le membre, puisqu'elle est dans son espace.
    ///
    /// Sur un vêtement, la même marque est une trace sombre (poussière, sueur), pas un bleu : un
    /// tee-shirt ne se violace pas. Un coup qui retombe au même endroit fonce la marque et
    /// l'élargit au lieu d'en empiler une deuxième.
    ///
    /// Les matières d'origine (Standard) sont converties au démarrage, une copie par matière et
    /// par combattant : n'importe quel corps en profite, y compris un modèle 3D importé.
    /// </summary>
    public class BruiseSystem : MonoBehaviour
    {
        private const int MarksPerSurface = 8;
        private const string ShaderName = "UberBagarre/Peau";

        private class Surface
        {
            public Renderer Renderer;

            // Corps skinné : les marques vivent dans l'espace de REPOS du maillage.
            public SkinnedMeshRenderer Skinned;
            public Matrix4x4[] BindInverse;
            public Transform[] Bones;
            public int[] BoneChild;
            public Vector3[] RestSamples;
            public readonly Vector4[] Marks = new Vector4[MarksPerSurface];
            public readonly Vector4[] Info = new Vector4[MarksPerSurface];
            public readonly float[] Target = new float[MarksPerSurface];
            public int Count;
            public int Oldest;
            public bool Animating;
        }

        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private BodyRig _rig;

        [SerializeField]
        [Tooltip("Le shader « UberBagarre/Peau ». Reference ici pour etre inclus dans les builds.")]
        private Shader _markShader;

        [Header("Apparence")]
        [SerializeField, Min(0.01f)] private float _minRadius = 0.045f;
        [SerializeField, Min(0.01f)] private float _maxRadius = 0.085f;

        [SerializeField] private Color _skinTint = new Color(0.62f, 0.36f, 0.50f);
        [SerializeField] private Color _skinCore = new Color(0.55f, 0.22f, 0.30f);
        [SerializeField] private Color _clothTint = new Color(0.72f, 0.70f, 0.68f);
        [SerializeField] private Color _clothCore = new Color(0.62f, 0.58f, 0.56f);

        [SerializeField, Min(0.05f)]
        [Tooltip("Temps d'apparition. Un bleu ne surgit pas instantanement.")]
        private float _fadeInDuration = 0.6f;

        private static readonly int MarksId = Shader.PropertyToID("_Marks");
        private static readonly int InfoId = Shader.PropertyToID("_MarkInfo");
        private static readonly int CountId = Shader.PropertyToID("_MarkCount");
        private static readonly int TintId = Shader.PropertyToID("_MarkTint");
        private static readonly int CoreId = Shader.PropertyToID("_MarkCore");
        private static readonly int SpaceId = Shader.PropertyToID("_MarkSpace");

        private readonly List<Surface> _surfaces = new List<Surface>();
        private readonly List<Surface> _animating = new List<Surface>();
        private readonly Dictionary<Material, Material> _converted = new Dictionary<Material, Material>();
        private MaterialPropertyBlock _block;
        private bool _ready;

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_rig == null) _rig = GetComponentInChildren<BodyRig>(true);
            if (_markShader == null) _markShader = Shader.Find(ShaderName);

            _block = new MaterialPropertyBlock();

            if (_markShader == null)
            {
                Debug.LogWarning("[UberBagarre] BruiseSystem sur " + name + " : shader « " + ShaderName +
                                 " » introuvable, les coups ne marqueront pas la peau.", this);
                return;
            }

            CollectSurfaces();
            _ready = _surfaces.Count > 0;
        }

        private void OnEnable()
        {
            if (_combatant != null) _combatant.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_combatant != null) _combatant.Damaged -= OnDamaged;
        }

        private void OnDestroy()
        {
            foreach (Material material in _converted.Values)
            {
                if (material != null) Destroy(material);
            }

            _converted.Clear();
        }

        /// <summary>
        /// Tous les morceaux de corps visibles, passés à la matière qui sait porter des marques.
        /// </summary>
        private void CollectSurfaces()
        {
            Transform root = _rig != null ? _rig.transform : transform;
            Renderer[] found = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < found.Length; i++)
            {
                Renderer renderer = found[i];
                if (renderer == null) continue;
                if (!(renderer is MeshRenderer) && !(renderer is SkinnedMeshRenderer)) continue;

                Material[] materials = renderer.sharedMaterials;
                bool any = false;
                SkinnedMeshRenderer skinned = renderer as SkinnedMeshRenderer;

                for (int m = 0; m < materials.Length; m++)
                {
                    Material converted = Convert(materials[m], skinned != null);
                    if (converted == null) continue;

                    materials[m] = converted;
                    any = true;
                }

                if (!any) continue;

                renderer.sharedMaterials = materials;

                Surface surface = new Surface();
                surface.Renderer = renderer;
                if (skinned != null) PrepareSkinned(surface, skinned);
                _surfaces.Add(surface);
                Push(surface);
            }

            if (_surfaces.Count == 0)
            {
                Debug.LogWarning("[UberBagarre] BruiseSystem sur " + name + " ne trouve aucune surface " +
                                 "visible : aucune marque ne pourra etre posee.", this);
            }
        }

        /// <summary>
        /// Ce qu'il faut pour ramener un point du monde dans l'espace de repos d'un corps
        /// skinné : l'inverse des poses de liaison, les os, et un échantillon de la surface.
        /// </summary>
        private static void PrepareSkinned(Surface surface, SkinnedMeshRenderer skinned)
        {
            Mesh mesh = skinned.sharedMesh;
            Transform[] bones = skinned.bones;
            if (mesh == null || bones == null || bones.Length == 0) return;

            Matrix4x4[] bindposes = mesh.bindposes;
            surface.Skinned = skinned;
            surface.Bones = bones;
            surface.BindInverse = new Matrix4x4[bindposes.Length];
            for (int i = 0; i < bindposes.Length; i++) surface.BindInverse[i] = bindposes[i].inverse;

            // L'os « suivant » de chaque os : on mesure la distance au SEGMENT, pas au pivot —
            // un coup au milieu de l'avant-bras est plus près du coude que du poignet, mais il
            // appartient à l'avant-bras.
            surface.BoneChild = new int[bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                surface.BoneChild[i] = -1;
                if (bones[i] == null) continue;

                for (int j = 0; j < bones.Length; j++)
                {
                    if (bones[j] != null && bones[j].parent == bones[i])
                    {
                        surface.BoneChild[i] = j;
                        break;
                    }
                }
            }

            Vector3[] vertices = mesh.vertices;
            int stride = Mathf.Max(1, vertices.Length / 6000);
            surface.RestSamples = new Vector3[vertices.Length / stride];
            for (int i = 0; i < surface.RestSamples.Length; i++) surface.RestSamples[i] = vertices[i * stride];
        }

        /// <summary>Point du monde -> espace de repos du corps, par l'os le plus proche.</summary>
        private static Vector3 ToRestSpace(Surface surface, Vector3 world)
        {
            int best = -1;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < surface.Bones.Length; i++)
            {
                Transform bone = surface.Bones[i];
                if (bone == null) continue;

                Vector3 a = bone.position;
                int child = surface.BoneChild[i];
                Vector3 b = child >= 0 && surface.Bones[child] != null ? surface.Bones[child].position : a;

                Vector3 ab = b - a;
                float t = ab.sqrMagnitude > 1e-8f ? Mathf.Clamp01(Vector3.Dot(world - a, ab) / ab.sqrMagnitude) : 0f;
                float sqr = (a + ab * t - world).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = i;
            }

            if (best < 0) return surface.Renderer.transform.InverseTransformPoint(world);

            Vector3 local = surface.Bones[best].InverseTransformPoint(world);
            Vector3 rest = surface.BindInverse[best].MultiplyPoint3x4(local);

            // Et on se pose sur la peau : le point du coup est sur la zone touchable, pas sur
            // la surface.
            Vector3 nearest = rest;
            float nearestSqr = float.MaxValue;
            Vector3[] samples = surface.RestSamples;

            for (int i = 0; i < samples.Length; i++)
            {
                float sqr = (samples[i] - rest).sqrMagnitude;
                if (sqr >= nearestSqr) continue;

                nearestSqr = sqr;
                nearest = samples[i];
            }

            return nearest;
        }

        /// <summary>Une copie de la matière avec le shader à marques. Une seule copie par matière d'origine.</summary>
        private Material Convert(Material source, bool skinned)
        {
            if (source == null) return null;

            Material converted;
            if (_converted.TryGetValue(source, out converted)) return converted;

            if (source.shader == _markShader)
            {
                // Déjà la bonne matière : une copie quand même, parce que les marques sont
                // propres à CE combattant (et la matière de peau est partagée par silhouette).
                converted = new Material(source);
                converted.name = source.name + " (marques)";
                if (skinned) converted.SetFloat(SpaceId, 1f);
                _converted[source] = converted;
                return converted;
            }

            converted = new Material(_markShader);
            converted.CopyPropertiesFromMaterial(source);
            converted.name = source.name + " (marques)";
            if (skinned) converted.SetFloat(SpaceId, 1f);

            // La peau bleuit ; le reste (tissu, cuir) se salit.
            bool skin = IsSkin(source);
            converted.SetColor(TintId, skin ? _skinTint : _clothTint);
            converted.SetColor(CoreId, skin ? _skinCore : _clothCore);

            _converted[source] = converted;
            return converted;
        }

        private static bool IsSkin(Material material)
        {
            string name = material.name.ToLowerInvariant();
            return name.Contains("skin") || name.Contains("peau") || name.Contains("flesh") || name.Contains("chair");
        }

        // --------------------------------------------------------------- coups

        private void OnDamaged(Combatant combatant, DamageInfo info)
        {
            if (!_ready) return;
            if (info.Point == Vector3.zero) return;

            // Un coup pris sur les avant-bras ne marque pas la peau.
            if (info.Blocked) return;

            float severity = Mathf.Clamp01(info.Amount / 20f);
            float radius = Mathf.Lerp(_minRadius, _maxRadius, severity);
            float intensity = Mathf.Lerp(0.6f, 1f, severity);
            float seed = Random.value * 10f;

            // Corps skinné (un seul rendu pour tout le corps) : la marque est posée dans
            // l'espace de repos, là où le shader la cherche.
            for (int i = 0; i < _surfaces.Count; i++)
            {
                Surface surface = _surfaces[i];
                if (surface.Skinned == null) continue;

                AddMarkLocal(surface, ToRestSpace(surface, info.Point), Vector3.one, radius, intensity, seed);
            }

            Vector3 centre;
            if (!NearestSurfacePoint(info.Point, out centre)) return;

            // La marque est posee sur TOUS les morceaux qu'elle touche : un coup a la machoire
            // marque la tete et le cou d'une seule tache, sans couture a la jonction.
            for (int i = 0; i < _surfaces.Count; i++)
            {
                Surface surface = _surfaces[i];
                if (surface.Renderer == null || surface.Skinned != null) continue;
                if (surface.Renderer.bounds.SqrDistance(centre) > radius * radius) continue;

                AddMark(surface, centre, radius, intensity, seed);
            }
        }

        /// <summary>
        /// Le point de la surface visible le plus proche de l'impact.
        ///
        /// Le point transporté par le coup est calculé sur la zone touchable, volontairement plus
        /// large que le corps (38 cm pour le torse, qui en fait 19) : sans cette reprojection, la
        /// marque serait posée dans le vide à côté de la peau. On projette sur l'ellipsoïde inscrit
        /// dans la boîte du maillage, qui épouse les formes arrondies du corps.
        /// </summary>
        private bool NearestSurfacePoint(Vector3 world, out Vector3 best)
        {
            best = world;
            float bestSqr = float.MaxValue;
            bool found = false;

            for (int i = 0; i < _surfaces.Count; i++)
            {
                Renderer renderer = _surfaces[i].Renderer;
                if (_surfaces[i].Skinned != null) continue;
                if (renderer == null || !renderer.enabled || !renderer.gameObject.activeInHierarchy) continue;

                Vector3 candidate = ProjectOnSurface(renderer, world);
                float sqr = (candidate - world).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = candidate;
                found = true;
            }

            return found;
        }

        private static Vector3 ProjectOnSurface(Renderer renderer, Vector3 world)
        {
            Transform t = renderer.transform;
            Bounds bounds = renderer.localBounds;
            Vector3 extents = bounds.extents;

            if (extents.x < 1e-5f || extents.y < 1e-5f || extents.z < 1e-5f) return renderer.bounds.ClosestPoint(world);

            Vector3 local = t.InverseTransformPoint(world) - bounds.center;
            Vector3 direction = new Vector3(local.x / extents.x, local.y / extents.y, local.z / extents.z);
            if (direction.sqrMagnitude < 1e-6f) direction = Vector3.forward;
            direction.Normalize();

            return t.TransformPoint(bounds.center + Vector3.Scale(direction, extents));
        }

        private void AddMark(Surface surface, Vector3 worldCentre, float radius, float intensity, float seed)
        {
            Vector3 local = surface.Renderer.transform.InverseTransformPoint(worldCentre);
            AddMarkLocal(surface, local, surface.Renderer.transform.lossyScale, radius, intensity, seed);
        }

        private void AddMarkLocal(Surface surface, Vector3 local, Vector3 scale, float radius, float intensity, float seed)
        {

            // Un coup qui retombe sur une marque existante la fonce et l'elargit.
            for (int i = 0; i < surface.Count; i++)
            {
                Vector4 mark = surface.Marks[i];
                Vector3 offset = Vector3.Scale((Vector3)mark - local, scale);
                if (offset.magnitude > mark.w * 0.6f) continue;

                surface.Marks[i] = new Vector4(mark.x, mark.y, mark.z, Mathf.Min(_maxRadius * 1.6f, mark.w * 1.12f));
                surface.Target[i] = Mathf.Min(1.15f, surface.Target[i] + intensity * 0.3f);
                StartAnimating(surface);
                return;
            }

            // Sinon, une nouvelle marque ; pleine, on remplace la plus ancienne.
            int index;
            if (surface.Count < MarksPerSurface)
            {
                index = surface.Count++;
            }
            else
            {
                index = surface.Oldest;
                surface.Oldest = (surface.Oldest + 1) % MarksPerSurface;
            }

            surface.Marks[index] = new Vector4(local.x, local.y, local.z, radius);
            surface.Info[index] = new Vector4(0f, seed, 0f, 0f);
            surface.Target[index] = intensity;
            StartAnimating(surface);
        }

        private void StartAnimating(Surface surface)
        {
            if (surface.Animating) return;

            surface.Animating = true;
            _animating.Add(surface);
        }

        private void Update()
        {
            if (_animating.Count == 0) return;

            // Temps de jeu : un ralenti d'impact ralentit aussi l'apparition du bleu.
            float step = Time.deltaTime / _fadeInDuration;

            for (int s = _animating.Count - 1; s >= 0; s--)
            {
                Surface surface = _animating[s];
                bool moving = false;

                for (int i = 0; i < surface.Count; i++)
                {
                    float current = surface.Info[i].x;
                    float target = surface.Target[i];
                    if (Mathf.Approximately(current, target)) continue;

                    surface.Info[i].x = Mathf.MoveTowards(current, target, step * Mathf.Max(0.3f, target));
                    moving = true;
                }

                Push(surface);

                if (moving) continue;

                surface.Animating = false;
                _animating.RemoveAt(s);
            }
        }

        private void Push(Surface surface)
        {
            if (surface.Renderer == null) return;

            surface.Renderer.GetPropertyBlock(_block);
            _block.SetVectorArray(MarksId, surface.Marks);
            _block.SetVectorArray(InfoId, surface.Info);
            _block.SetFloat(CountId, surface.Count);
            surface.Renderer.SetPropertyBlock(_block);
        }

        /// <summary>Efface toutes les marques. Appelé à la relance d'un combat.</summary>
        public void Clear()
        {
            for (int i = 0; i < _surfaces.Count; i++)
            {
                Surface surface = _surfaces[i];
                surface.Count = 0;
                surface.Oldest = 0;
                surface.Animating = false;

                for (int m = 0; m < MarksPerSurface; m++)
                {
                    surface.Info[m] = Vector4.zero;
                    surface.Target[m] = 0f;
                }

                Push(surface);
            }

            _animating.Clear();
        }
    }
}
