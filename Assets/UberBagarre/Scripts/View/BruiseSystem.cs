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
    /// Chaque marque est attachée à l'OS le plus proche du point d'impact : elle suit donc le
    /// mouvement du combattant au lieu de flotter en l'air.
    /// </summary>
    public class BruiseSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private BodyRig _rig;
        [SerializeField] private Material _bruiseMaterial;

        [Header("Apparence")]
        [SerializeField, Min(0.01f)] private float _minSize = 0.055f;
        [SerializeField, Min(0.01f)] private float _maxSize = 0.115f;

        [SerializeField, Min(0f)]
        [Tooltip("Decalage vers l'exterieur, pour eviter que la marque ne disparaisse dans la peau.")]
        private float _surfaceOffset = 0.012f;

        [SerializeField, Min(1)] private int _maxBruises = 16;

        [SerializeField, Min(0.05f)]
        [Tooltip("Temps d'apparition. Un bleu ne surgit pas instantanement.")]
        private float _fadeInDuration = 0.5f;

        private readonly List<Transform> _bruises = new List<Transform>();
        private readonly List<Transform> _bones = new List<Transform>();
        private readonly List<float> _ages = new List<float>();
        private readonly List<Vector3> _targetScales = new List<Vector3>();

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_rig == null) _rig = GetComponentInChildren<BodyRig>(true);

            CollectBones();
        }

        private void OnEnable()
        {
            if (_combatant != null) _combatant.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_combatant != null) _combatant.Damaged -= OnDamaged;
        }

        /// <summary>Os candidats pour accrocher une marque. Volontairement peu nombreux : il s'agit de trouver la zone, pas la position exacte.</summary>
        private void CollectBones()
        {
            if (_rig == null) return;

            AddBone(_rig.Pelvis);
            AddBone(_rig.Spine);
            AddBone(_rig.Chest);
            AddBone(_rig.Neck);

            AddLimbBones(_rig.LeftArm);
            AddLimbBones(_rig.RightArm);
            AddLimbBones(_rig.LeftLeg);
            AddLimbBones(_rig.RightLeg);
        }

        private void AddLimbBones(IkLimb limb)
        {
            if (limb == null) return;

            AddBone(limb.Upper);
            AddBone(limb.End);
        }

        private void AddBone(Transform bone)
        {
            if (bone != null) _bones.Add(bone);
        }

        private void OnDamaged(Combatant combatant, DamageInfo info)
        {
            if (_bruiseMaterial == null || _bones.Count == 0) return;
            if (info.Point == Vector3.zero) return;

            // Un coup pris sur les avant-bras ne marque pas la peau.
            if (info.Blocked) return;

            Transform bone = NearestBone(info.Point);
            if (bone == null) return;

            SpawnBruise(bone, info);
        }

        private Transform NearestBone(Vector3 worldPoint)
        {
            Transform best = null;
            float bestSqr = float.MaxValue;

            for (int i = 0; i < _bones.Count; i++)
            {
                float sqr = (_bones[i].position - worldPoint).sqrMagnitude;
                if (sqr >= bestSqr) continue;

                bestSqr = sqr;
                best = _bones[i];
            }

            return best;
        }

        private void SpawnBruise(Transform bone, DamageInfo info)
        {
            GameObject bruise = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            bruise.name = "Bleu";

            Collider collider = bruise.GetComponent<Collider>();
            if (collider != null) Destroy(collider);

            MeshRenderer renderer = bruise.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _bruiseMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            bruise.transform.SetParent(bone, true);

            // On repousse legerement la marque vers l'exterieur du corps, sinon elle se noie
            // dans la surface et devient invisible sous certains angles.
            Vector3 outward = (info.Point - bone.position);
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : -info.Direction.normalized;

            bruise.transform.position = info.Point + outward * _surfaceOffset;
            bruise.transform.rotation = Quaternion.LookRotation(outward, Vector3.up);

            float severity = Mathf.Clamp01(info.Amount / 20f);
            float size = Mathf.Lerp(_minSize, _maxSize, severity);

            // Aplatie contre la peau plutot que spherique : une bosse ferait verrue.
            Vector3 target = new Vector3(size, size * 0.85f, size * 0.32f);
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
