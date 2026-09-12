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
    /// Chaque marque est attachée au MORCEAU DE CORPS VISIBLE le plus proche du point d'impact,
    /// et posée sur sa surface. C'est le point crucial, et la première version s'est trompée
    /// dessus : le point d'impact transporté par le coup est calculé sur la zone touchable, dont
    /// le rayon est volontairement généreux — 38 cm pour le torse, alors que le torse visible en
    /// fait 19. Les marques apparaissaient donc à une vingtaine de centimètres de la peau, en
    /// suspension dans le vide à côté du personnage. Elles étaient bien là, simplement nulle part
    /// où on pouvait les voir.
    ///
    /// On reprojette donc le point sur la boîte englobante du rendu le plus proche. Ça ne demande
    /// aucun collider sur la chair, et ça marche aussi avec un modèle 3D importé.
    /// </summary>
    public class BruiseSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private BodyRig _rig;
        [SerializeField] private Material _bruiseMaterial;

        [Header("Apparence")]
        [SerializeField, Min(0.01f)] private float _minSize = 0.085f;
        [SerializeField, Min(0.01f)] private float _maxSize = 0.170f;

        [SerializeField, Min(0f)]
        [Tooltip("Decalage vers l'exterieur, pour eviter que la marque ne disparaisse dans la peau.")]
        private float _surfaceOffset = 0.008f;

        [SerializeField, Min(1)] private int _maxBruises = 16;

        [SerializeField, Min(0.05f)]
        [Tooltip("Temps d'apparition. Un bleu ne surgit pas instantanement.")]
        private float _fadeInDuration = 0.5f;

        private readonly List<Transform> _bruises = new List<Transform>();
        private readonly List<Renderer> _surfaces = new List<Renderer>();
        private readonly List<float> _ages = new List<float>();
        private readonly List<Vector3> _targetScales = new List<Vector3>();

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_rig == null) _rig = GetComponentInChildren<BodyRig>(true);

            CollectSurfaces();
        }

        private void OnEnable()
        {
            if (_combatant != null) _combatant.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_combatant != null) _combatant.Damaged -= OnDamaged;
        }

        /// <summary>
        /// Tous les morceaux de corps visibles. On cherche une SURFACE, pas un os.
        ///
        /// Le rendu le plus proche du point d'impact donne à la fois l'endroit où coller la
        /// marque et le transform auquel l'accrocher, donc elle suit forcément le mouvement du
        /// membre touché. Chercher l'os le plus proche laissait le choix de la position ouvert,
        /// et c'est là que la première version posait les marques dans le vide.
        /// </summary>
        private void CollectSurfaces()
        {
            Transform root = _rig != null ? _rig.transform : transform;

            Renderer[] found = root.GetComponentsInChildren<Renderer>(true);

            for (int i = 0; i < found.Length; i++)
            {
                if (found[i] == null) continue;
                if (found[i].GetComponent<ParticleSystem>() != null) continue;

                _surfaces.Add(found[i]);
            }

            if (_surfaces.Count == 0)
            {
                Debug.LogWarning("[UberBagarre] BruiseSystem sur " + name + " ne trouve aucune surface " +
                                 "visible : aucune marque ne pourra etre posee.", this);
            }
        }

        private void OnDamaged(Combatant combatant, DamageInfo info)
        {
            if (_bruiseMaterial == null || _surfaces.Count == 0) return;
            if (info.Point == Vector3.zero) return;

            // Un coup pris sur les avant-bras ne marque pas la peau.
            if (info.Blocked) return;

            Vector3 surfacePoint;
            Renderer surface = NearestSurface(info.Point, out surfacePoint);
            if (surface == null) return;

            SpawnBruise(AttachPointOf(surface), surfacePoint, info);
        }

        /// <summary>
        /// Le morceau de corps visible dont la surface est la plus proche du point d'impact, et
        /// le point de cette surface où poser la marque.
        /// </summary>
        private Renderer NearestSurface(Vector3 worldPoint, out Vector3 surfacePoint)
        {
            Renderer best = null;
            surfacePoint = worldPoint;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < _surfaces.Count; i++)
            {
                Renderer renderer = _surfaces[i];
                if (renderer == null) continue;

                Vector3 candidate = renderer.bounds.ClosestPoint(worldPoint);
                float sqr = (candidate - worldPoint).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                surfacePoint = candidate;
                best = renderer;
            }

            return best;
        }

        /// <summary>
        /// À quel transform accrocher la marque : l'OS qui porte le visuel, pas le visuel.
        ///
        /// Les morceaux de chair sont des maillages unitaires mis à l'échelle — un torse, c'est la
        /// boîte adoucie redimensionnée en (0,34 ; 0,30 ; 0,22). Accrocher la marque dessus la
        /// ferait hériter de cette échelle non uniforme : elle serait étirée en largeur et écrasée
        /// en profondeur, et d'autant plus déformée qu'elle est posée en biais. Les os, eux, sont
        /// à l'échelle 1.
        /// </summary>
        private static Transform AttachPointOf(Renderer surface)
        {
            Transform visual = surface.transform;
            return visual.parent != null ? visual.parent : visual;
        }

        private void SpawnBruise(Transform surface, Vector3 surfacePoint, DamageInfo info)
        {
            GameObject bruise = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bruise.name = "Bleu";

            Collider collider = bruise.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            MeshRenderer renderer = bruise.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _bruiseMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            // On repousse legerement la marque vers l'exterieur, sinon elle se noie dans la
            // surface et devient invisible sous certains angles. La direction vient du coup
            // lui-meme : c'est la normale la plus juste qu'on ait sans collider sur la chair.
            Vector3 outward = -info.Direction;
            if (outward.sqrMagnitude < 0.0001f) outward = Vector3.forward;
            outward.Normalize();

            // Le parent est pris APRES avoir fixe la pose monde, pour que l'echelle locale du
            // membre ne deforme pas la marque.
            bruise.transform.position = surfacePoint + outward * _surfaceOffset;
            bruise.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);
            bruise.transform.SetParent(surface, true);

            float severity = Mathf.Clamp01(info.Amount / 20f);
            float size = Mathf.Lerp(_minSize, _maxSize, severity);

            // Aplatie contre la peau plutot que spherique : une bosse ferait verrue.
            Vector3 target = new Vector3(size, size * 0.85f, size * 0.30f);
            bruise.transform.localScale = Vector3.zero;

            _bruises.Add(bruise.transform);
            _ages.Add(0f);
            _targetScales.Add(target);

            TrimOldest();
        }

        private void TrimOldest()
        {
            while (_bruises.Count > _maxBruises)
            {
                if (_bruises[0] != null) Destroy(_bruises[0].gameObject);

                _bruises.RemoveAt(0);
                _ages.RemoveAt(0);
                _targetScales.RemoveAt(0);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            for (int i = 0; i < _bruises.Count; i++)
            {
                if (_bruises[i] == null) continue;
                if (_ages[i] >= _fadeInDuration) continue;

                _ages[i] += dt;
                float t = Mathf.Clamp01(_ages[i] / _fadeInDuration);

                // Depassement puis retour : la marque "gonfle" comme un vrai hematome.
                float overshoot = 1f + Mathf.Sin(t * Mathf.PI) * 0.25f;
                _bruises[i].localScale = _targetScales[i] * (t * overshoot);
            }
        }

        /// <summary>Efface toutes les marques. Appelé à la relance d'un combat.</summary>
        public void Clear()
        {
            for (int i = 0; i < _bruises.Count; i++)
            {
                if (_bruises[i] != null) Destroy(_bruises[i].gameObject);
            }

            _bruises.Clear();
            _ages.Clear();
            _targetScales.Clear();
        }
    }
}
