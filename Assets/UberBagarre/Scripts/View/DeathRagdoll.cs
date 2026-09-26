using System.Collections.Generic;
using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Vrai ragdoll physique, construit À L'INSTANT DE LA MORT et pas avant.
    ///
    /// Pourquoi ce composant existe, et pourquoi la chute sur coup aux jambes n'en utilise pas :
    /// tant que le combattant est vivant, son squelette est piloté à chaque image par l'IK des
    /// quatre membres et par le cycle de marche. Un ragdoll permanent se battrait avec eux, et il
    /// faudrait désactiver puis resynchroniser toute la chaîne à chaque chute, pour un résultat
    /// différent à chaque fois — donc impossible à régler. La chute sur coup bas doit se terminer
    /// par un relevé reproductible : elle reste procédurale.
    ///
    /// La mort, elle, est définitive. Plus rien n'a besoin d'être reproductible ni de se relever,
    /// et c'est donc le seul moment où la physique peut prendre la main sans rien casser. C'est
    /// aussi le moment où ça compte le plus : un corps qui s'effondre, rebondit sur le sol et
    /// finit de travers raconte la fin du combat mieux qu'une animation.
    ///
    /// Les pilotes d'animation sont coupés d'un coup (liste explicite, pas de recherche magique),
    /// puis Rigidbody, colliders et articulations sont posés sur les os existants.
    /// </summary>
    public class DeathRagdoll : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private BodyRig _rig;

        [SerializeField]
        [Tooltip("Tout ce qui pilote le squelette ou le deplacement, et qui doit se taire a la " +
                 "mort : locomotion, bras, IA, moteur, executeur de coups, chute procedurale. " +
                 "Liste explicite et non devinee : un pilote oublie se battrait avec la physique.")]
        private MonoBehaviour[] _disableOnDeath = new MonoBehaviour[0];

        [SerializeField]
        [Tooltip("Optionnel : les animations capturees. Si elles savent jouer la mort, le corps " +
                 "s'effondre en animation (chute arriere ou avant selon le coup) au lieu de " +
                 "partir en poupee de chiffon.")]
        private MocapDriver _mocap;

        [SerializeField]
        [Tooltip("Le CharacterController. Il doit partir : sa capsule continuerait de pousser le " +
                 "cadavre et de le maintenir debout.")]
        private CharacterController _controller;

        [SerializeField]
        [Tooltip("Les zones touchables. Desactivees a la mort : on ne frappe pas un cadavre, et " +
                 "leurs triggers perturberaient la physique du ragdoll.")]
        private GameObject _hurtboxRoot;

        [Header("Masses (kg)")]
        [SerializeField, Min(0.1f)] private float _pelvisMass = 12f;
        [SerializeField, Min(0.1f)] private float _torsoMass = 20f;
        [SerializeField, Min(0.1f)] private float _headMass = 5f;
        [SerializeField, Min(0.1f)] private float _upperArmMass = 2.5f;
        [SerializeField, Min(0.1f)] private float _forearmMass = 1.8f;
        [SerializeField, Min(0.1f)] private float _thighMass = 8f;
        [SerializeField, Min(0.1f)] private float _shinMass = 4f;

        [Header("Epaisseurs (metres)")]
        [SerializeField, Min(0.01f)] private float _pelvisRadius = 0.14f;
        [SerializeField, Min(0.01f)] private float _torsoRadius = 0.17f;
        [SerializeField, Min(0.01f)] private float _headRadius = 0.115f;
        [SerializeField, Min(0.01f)] private float _upperArmRadius = 0.058f;
        [SerializeField, Min(0.01f)] private float _forearmRadius = 0.050f;
        [SerializeField, Min(0.01f)] private float _thighRadius = 0.088f;
        [SerializeField, Min(0.01f)] private float _shinRadius = 0.070f;

        [Header("Effondrement")]
        [SerializeField, Min(0f)]
        [Tooltip("Poussee appliquee au bassin dans la direction du coup fatal. Assez pour que la " +
                 "chute raconte d'ou venait le coup, pas assez pour faire voler le corps.")]
        private float _deathImpulse = 2.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Poussee supplementaire sur la partie touchee. C'est elle qui fait que la tete " +
                 "partent en arriere sur un uppercut.")]
        private float _localImpulse = 1.8f;

        [SerializeField, Min(1)]
        [Tooltip("Iterations du solveur. Montees volontairement : un ragdoll genere a la volee " +
                 "part en vrille bien plus facilement qu'un ragdoll regle a la main.")]
        private int _solverIterations = 12;

        [SerializeField, Min(0.5f)]
        [Tooltip("Vitesse maximale de separation. Elle borne les explosions quand deux colliders " +
                 "du corps se retrouvent imbriques a la premiere image.")]
        private float _maxDepenetrationVelocity = 2f;

        [Header("Debug")]
        [SerializeField] private bool _logBuild;

        private readonly List<Rigidbody> _bodies = new List<Rigidbody>();
        private bool _built;

        public bool IsRagdoll { get { return _built; } }

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_rig == null) _rig = GetComponentInChildren<BodyRig>(true);
            if (_controller == null) _controller = GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            if (_combatant != null && _combatant.Health != null) _combatant.Health.Died += OnDied;
        }

        private void OnDisable()
        {
            if (_combatant != null && _combatant.Health != null) _combatant.Health.Died -= OnDied;
        }

        private void OnDied(DamageInfo info)
        {
            Build(info);
        }

        /// <summary>Bascule le corps en ragdoll. Sans effet si c'est déjà fait.</summary>
        public void Build(DamageInfo info)
        {
            if (_built) return;

            if (_rig == null)
            {
                Debug.LogWarning("[UberBagarre] DeathRagdoll sur " + name + " n'a pas de BodyRig : " +
                                 "pas de ragdoll, le corps restera tel quel.", this);
                return;
            }

            _built = true;

            SilenceDrivers();

            if (_mocap != null && _mocap.PlayDeath(info))
            {
                AddCorpseShapes();
                if (_logBuild) Debug.Log("[UberBagarre] Mort animee sur " + name + ".", this);
                return;
            }

            // Le bassin porte tout le reste : il est construit en premier et sert de parent
            // d'articulation a la colonne comme aux cuisses.
            Rigidbody pelvis = AddSegment(_rig.Pelvis, _rig.Spine, _pelvisMass, _pelvisRadius, true, null,
                0f, 0f, 0f, 0f);

            Rigidbody torso = AddSegment(_rig.Spine, _rig.Neck, _torsoMass, _torsoRadius, true, pelvis,
                20f, 25f, 15f, 0f);

            Rigidbody head = AddHead(torso);

            AddArm(_rig.LeftArm, torso);
            AddArm(_rig.RightArm, torso);
            AddLeg(_rig.LeftLeg, pelvis);
            AddLeg(_rig.RightLeg, pelvis);

            ApplyDeathImpulse(info, pelvis, torso, head);

            if (_logBuild)
            {
                Debug.Log("[UberBagarre] Ragdoll construit sur " + name + " : " + _bodies.Count + " segments.", this);
            }
        }

        /// <summary>
        /// Le corps d'une mort ANIMÉE garde une forme qu'on peut viser. Sans elle, il n'avait plus
        /// un seul collider (zones touchables coupées, capsule partie, pas de ragdoll) : le
        /// viseur de l'appareil photo passait à travers, et la preuve du K.O. était impossible.
        /// Des déclencheurs cinématiques, collés aux os : ils suivent la chute animée, ne
        /// poussent rien, et le lancer de rayon de la photo (qui voit les déclencheurs) les trouve.
        /// </summary>
        private void AddCorpseShapes()
        {
            AddShape(_rig.Pelvis, _rig.Spine, _pelvisRadius, true);
            AddShape(_rig.Spine, _rig.Neck, _torsoRadius, true);

            Transform neck = _rig.Neck;
            if (neck != null)
            {
                SphereCollider head = neck.gameObject.AddComponent<SphereCollider>();
                head.center = new Vector3(0f, _headRadius + 0.06f, 0f);
                head.radius = _headRadius;
                head.isTrigger = true;
                Kinematic(neck);
            }

            AddLimbShapes(_rig.LeftArm, _upperArmRadius, _forearmRadius);
            AddLimbShapes(_rig.RightArm, _upperArmRadius, _forearmRadius);
            AddLimbShapes(_rig.LeftLeg, _thighRadius, _shinRadius);
            AddLimbShapes(_rig.RightLeg, _thighRadius, _shinRadius);
        }

        private void AddLimbShapes(IkLimb limb, float upperRadius, float lowerRadius)
        {
            if (limb == null) return;

            AddShape(limb.Upper, limb.Lower, upperRadius, false);
            AddShape(limb.Lower, limb.End, lowerRadius, false);
        }

        private static void AddShape(Transform bone, Transform child, float radius, bool vertical)
        {
            if (bone == null) return;

            float length = child != null ? Vector3.Distance(bone.position, child.position) : radius * 2.4f;
            length = Mathf.Max(length, radius * 2.1f);

            CapsuleCollider collider = bone.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = length + radius;
            collider.direction = vertical ? 1 : 2;
            collider.center = (vertical ? Vector3.up : Vector3.forward) * (length * 0.5f);
            collider.isTrigger = true;
            Kinematic(bone);
        }

        private static void Kinematic(Transform bone)
        {
            Rigidbody body = bone.GetComponent<Rigidbody>();
            if (body == null) body = bone.gameObject.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
        }

        /// <summary>
        /// Coupe tout ce qui écrivait sur le squelette.
        ///
        /// L'ordre compte : si un seul pilote continue d'appliquer une pose d'IK, il écrasera la
        /// physique à chaque image et le ragdoll restera figé debout — panne silencieuse typique,
        /// puisque rien n'est en erreur.
        /// </summary>
        private void SilenceDrivers()
        {
            for (int i = 0; i < _disableOnDeath.Length; i++)
            {
                if (_disableOnDeath[i] != null) _disableOnDeath[i].enabled = false;
            }

            if (_controller != null) _controller.enabled = false;
            if (_hurtboxRoot != null) _hurtboxRoot.SetActive(false);
        }

        // ------------------------------------------------------------------ construction

        private void AddArm(IkLimb arm, Rigidbody torso)
        {
            if (arm == null) return;

            Rigidbody upper = AddSegment(arm.Upper, arm.Lower, _upperArmMass, _upperArmRadius, false, torso,
                45f, 80f, 45f, 0f);

            // Le coude ne vrille pas et ne plie que dans un plan : limites tres serrees sur
            // l'axe de twist et sur le second swing.
            AddSegment(arm.Lower, arm.End, _forearmMass, _forearmRadius, false, upper, 8f, 85f, 6f, 0f);
        }

        private void AddLeg(IkLimb leg, Rigidbody pelvis)
        {
            if (leg == null) return;

            Rigidbody thigh = AddSegment(leg.Upper, leg.Lower, _thighMass, _thighRadius, false, pelvis,
                25f, 70f, 35f, 0f);

            AddSegment(leg.Lower, leg.End, _shinMass, _shinRadius, false, thigh, 6f, 80f, 6f, 0f);
        }

        private Rigidbody AddHead(Rigidbody torso)
        {
            Transform neck = _rig.Neck;
            if (neck == null) return torso;

            Rigidbody body = PrepareBody(neck, _headMass);

            SphereCollider collider = neck.gameObject.AddComponent<SphereCollider>();
            collider.center = new Vector3(0f, _headRadius + 0.06f, 0f);
            collider.radius = _headRadius;

            Join(neck, body, torso, Vector3.up, Vector3.right, 40f, 35f, 25f);
            return body;
        }

        /// <summary>
        /// Pose un segment : Rigidbody, capsule, et articulation vers le parent.
        ///
        /// La LONGUEUR est mesurée sur place, à partir de l'os enfant, au lieu d'être écrite en
        /// dur. C'est ce qui permet au même code de fonctionner sur un modèle 3D importé, dont les
        /// proportions ne sont pas celles du corps généré.
        /// </summary>
        private Rigidbody AddSegment(Transform bone, Transform child, float mass, float radius,
            bool vertical, Rigidbody parent, float twist, float swing1, float swing2, float unused)
        {
            if (bone == null) return parent;

            float length = child != null
                ? Vector3.Distance(bone.position, child.position)
                : radius * 2.4f;

            length = Mathf.Max(length, radius * 2.1f);

            Rigidbody body = PrepareBody(bone, mass);

            CapsuleCollider collider = bone.gameObject.AddComponent<CapsuleCollider>();
            collider.radius = radius;
            collider.height = length + radius;
            collider.direction = vertical ? 1 : 2;
            collider.center = (vertical ? Vector3.up : Vector3.forward) * (length * 0.5f);

            if (parent != null)
            {
                Join(bone, body, parent, vertical ? Vector3.up : Vector3.forward, Vector3.right,
                    twist, swing1, swing2);
            }

            return body;
        }

        private Rigidbody PrepareBody(Transform bone, float mass)
        {
            Rigidbody body = bone.gameObject.GetComponent<Rigidbody>();
            if (body == null) body = bone.gameObject.AddComponent<Rigidbody>();

            body.mass = mass;
            body.solverIterations = _solverIterations;
            body.maxDepenetrationVelocity = _maxDepenetrationVelocity;
            body.interpolation = RigidbodyInterpolation.Interpolate;
            body.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            _bodies.Add(body);
            return body;
        }

        private static void Join(Transform bone, Rigidbody body, Rigidbody parent,
            Vector3 axis, Vector3 swingAxis, float twist, float swing1, float swing2)
        {
            CharacterJoint joint = bone.gameObject.AddComponent<CharacterJoint>();
            joint.connectedBody = parent;

            // L'ancre est a l'ORIGINE de l'os, et c'est gratuit : chaque os du rig a deja son
            // pivot sur son articulation. Rien a mesurer.
            joint.autoConfigureConnectedAnchor = true;
            joint.anchor = Vector3.zero;
            joint.axis = axis;
            joint.swingAxis = swingAxis;

            SoftJointLimit low = joint.lowTwistLimit;
            low.limit = -twist;
            joint.lowTwistLimit = low;

            SoftJointLimit high = joint.highTwistLimit;
            high.limit = twist;
            joint.highTwistLimit = high;

            SoftJointLimit first = joint.swing1Limit;
            first.limit = swing1;
            joint.swing1Limit = first;

            SoftJointLimit second = joint.swing2Limit;
            second.limit = swing2;
            joint.swing2Limit = second;

            // La projection rattrape les articulations qui se sont separees. Sans elle, un
            // ragdoll genere a la volee finit regulierement avec un membre a deux metres du corps.
            joint.enableProjection = true;
            joint.projectionDistance = 0.08f;
            joint.projectionAngle = 12f;
        }

        private void ApplyDeathImpulse(DamageInfo info, Rigidbody pelvis, Rigidbody torso, Rigidbody head)
        {
            Vector3 direction = info.Direction;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) direction = -transform.forward;
            direction.Normalize();

            // Une legere composante verticale : un corps qui s'effondre strictement a
            // l'horizontale glisse au sol au lieu de tomber.
            Vector3 push = (direction + Vector3.up * 0.22f).normalized * _deathImpulse;

            if (pelvis != null) pelvis.AddForce(push, ForceMode.VelocityChange);

            // Le coup fatal marque la zone qu'il a touchee : un uppercut renverse la tete en
            // arriere, un coup au corps plie le buste. C'est ce qui fait qu'aucune mort ne
            // ressemble a la precedente, sans une seule animation.
            Rigidbody local = info.Zone == HitZone.Head ? head : torso;

            if (local != null && local != pelvis)
            {
                local.AddForce(push.normalized * _localImpulse, ForceMode.VelocityChange);
            }
        }
    }
}
