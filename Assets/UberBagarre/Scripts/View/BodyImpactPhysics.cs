using UberBagarre.Combat;
using UberBagarre.Feedback;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// La physique du coup, LÀ où il touche et DANS LE SENS où il arrive.
    ///
    /// Le recul existant déplace le combattant entier et incline son buste d'un bloc. C'est
    /// juste, mais c'est la même réponse pour un direct au menton, un crochet dans les côtes et
    /// un coup de pied dans la cuisse — et c'est précisément ce qui rendait le combat « nul » :
    /// le corps n'avait pas de parties, il n'avait qu'une position.
    ///
    /// Ici, chaque articulation est un ressort amorti à trois axes. Un coup n'est pas une
    /// animation qu'on déclenche, c'est une IMPULSION : une force appliquée en un point précis,
    /// dans une direction précise. Chaque os de la chaîne reçoit le moment de cette force par
    /// rapport à son propre pivot — le fameux r × F — divisé par son inertie. Il en sort une
    /// vitesse angulaire, que le ressort ramène ensuite vers la pose animée, en dépassant un peu,
    /// comme un vrai corps.
    ///
    /// Tout le reste découle de cette seule règle, sans un cas particulier :
    /// - un direct au visage envoie la tête en arrière ; un crochet la fait tourner ; un
    ///   uppercut la relève. La trajectoire du poing décide, pas le nom du coup ;
    /// - un coup haut sur l'épaule fait pivoter le buste autour de la colonne, un coup bas dans
    ///   les côtes le fait plier ;
    /// - un coup dans le ventre fait fouetter la tête vers l'avant, parce qu'elle reste en
    ///   arrière pendant que le torse part (le coup du lapin) ;
    /// - un coup de pied dans la cuisse fait plier le genou et tourner le bassin ;
    /// - un coup BLOQUÉ repousse les avant-bras, pas le visage.
    ///
    /// Pourquoi des ressorts et pas un ragdoll actif (des corps rigides motorisés) : un ragdoll
    /// actif se bat contre l'animation procédurale, doit être réglé os par os pour ne pas
    /// trembler, et explose au premier pas de temps trop long. Ces ressorts s'AJOUTENT à la pose
    /// animée au lieu de la remplacer : ils ne peuvent pas diverger, ils ne cassent ni la marche
    /// ni les poses d'attaque, et ils reviennent toujours à zéro. La mort, elle, reste confiée au
    /// vrai ragdoll physique.
    ///
    /// Mécanique d'application : l'offset est ajouté APRÈS toute l'animation (ordre 500), puis
    /// retiré au début de l'image suivante. Sans ce retrait, un os que personne d'autre n'écrit
    /// — la nuque — accumulerait l'offset image après image et finirait tordu.
    /// </summary>
    [DefaultExecutionOrder(500)]
    [DisallowMultipleComponent]
    public class BodyImpactPhysics : MonoBehaviour
    {
        private enum Part
        {
            Pelvis = 0, Spine = 1, Chest = 2, Head = 3,
            UpperArmLeft = 4, ForearmLeft = 5, UpperArmRight = 6, ForearmRight = 7,
            ThighLeft = 8, ShinLeft = 9, ThighRight = 10, ShinRight = 11
        }

        private const int PartCount = 12;

        private class BoneSpring
        {
            public Transform Bone;
            public Transform Tip;
            public int Parent = -1;
            public float Inertia;
            public float Stiffness;
            public float Limit;
            public Vector3 Angle;
            public Vector3 Velocity;
            public Quaternion Animated;
            public bool Applied;
        }

        [Header("References")]
        [SerializeField] private BodyRig _rig;
        [SerializeField] private Combatant _combatant;

        [SerializeField]
        [Tooltip("Joueur uniquement : le choc a la tete est aussi transmis a la camera. En vue " +
                 "premiere personne, c'est le seul endroit ou le joueur peut SENTIR le sens du coup.")]
        private CameraPunch _cameraPunch;

        [SerializeField]
        [Tooltip("Repere dans lequel l'a-coup de camera est exprime. En pratique, la camera.")]
        private Transform _cameraReference;

        [Header("Force")]
        [SerializeField, Min(0f)]
        [Tooltip("Multiplicateur general. 0 = aucune reaction locale ; 2 = poupee de chiffon.")]
        private float _impulseScale = 0.85f;

        [SerializeField, Min(0f)]
        [Tooltip("Impulsion ajoutee par metre/seconde de vitesse du poing. Un coup lance de loin " +
                 "doit porter plus qu'un coup pousse a bout portant, meme s'il s'appelle pareil.")]
        private float _velocityInfluence = 0.18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Fraction transmise quand la garde encaisse. Elle va aux avant-bras, pas au visage.")]
        private float _blockedScale = 0.45f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du choc transmise au parent direct de l'os touche. Les parents suivants " +
                 "en recoivent de moins en moins : la tete ne fait pas basculer le bassin.")]
        private float _chainFalloff = 0.24f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Coup du lapin : quand le torse part, la tete reste en arriere un instant.")]
        private float _whiplash = 0.35f;

        [SerializeField, Min(0f)]
        [Tooltip("Reflexe de pliage sur un coup au ventre. La mecanique seule ne le produit pas : " +
                 "un coup donne a hauteur du pivot pousse le buste sans le faire tourner. Un vrai " +
                 "corps, lui, se REFERME autour du poing — c'est un reflexe, pas une consequence " +
                 "des forces, et il faut donc l'ajouter explicitement.")]
        private float _gutFold = 0.8f;

        [Header("Ressorts")]
        [SerializeField, Range(0.05f, 1.2f)]
        [Tooltip("Amortissement. En dessous de 1, le corps depasse un peu sa pose avant d'y " +
                 "revenir : c'est ce petit rebond qui fait lire un corps et pas une charniere.")]
        private float _dampingRatio = 0.36f;

        [SerializeField, Min(1f)] private float _headStiffness = 230f;
        [SerializeField, Min(1f)] private float _torsoStiffness = 120f;
        [SerializeField, Min(1f)] private float _armStiffness = 160f;
        [SerializeField, Min(1f)] private float _legStiffness = 140f;

        [Header("Camera (joueur)")]
        [SerializeField, Min(0f)] private float _cameraSnap = 0.45f;
        [SerializeField, Min(0f)] private float _cameraSnapLimit = 6f;

        [Header("Debug")]
        [SerializeField] private bool _drawDebug = true;

        /// <summary>
        /// Multiplicateur commun à TOUS les corps, piloté par le menu de réglage.
        ///
        /// Global plutôt que par composant : les adversaires des vagues sont clonés à la
        /// volée, et un réglage posé sur chaque instance existante ne toucherait pas ceux qui
        /// arrivent ensuite. Le curseur ne dirait alors la vérité que jusqu'à la vague suivante.
        /// </summary>
        public static float GlobalScale = 1f;

        private readonly BoneSpring[] _joints = new BoneSpring[PartCount];
        private bool _built;
        private Vector3 _lastPoint;
        private Vector3 _lastImpulse;
        private float _lastTime = -10f;

        /// <summary>Partie du corps touchée en dernier, pour le diagnostic.</summary>
        public string LastPart { get; private set; }

        public float ImpulseScale
        {
            get { return _impulseScale; }
            set { _impulseScale = Mathf.Max(0f, value); }
        }

        // ------------------------------------------------------------------ cycle

        private void Awake()
        {
            if (_rig == null) _rig = GetComponentInChildren<BodyRig>();
            if (_combatant == null) _combatant = GetComponent<Combatant>();

            Build();
        }

        private void OnEnable()
        {
            if (_combatant != null && _combatant.Health != null) _combatant.Health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_combatant != null && _combatant.Health != null) _combatant.Health.Damaged -= OnDamaged;

            Restore();
            ResetState();
        }

        /// <summary>
        /// Retire l'offset de l'image précédente. Update passe AVANT toute écriture d'os (elles
        /// sont toutes en LateUpdate), donc chaque système d'animation repart d'une pose propre.
        /// </summary>
        private void Update()
        {
            if (!Alive())
            {
                // Le ragdoll de mort a la main : rien ne doit plus écrire sur les os, pas même
                // une restauration, sinon on fige le corps dans sa dernière pose animée.
                ResetState();
                return;
            }

            Restore();
        }

        private void LateUpdate()
        {
            if (!_built || !Alive()) return;

            Integrate(Mathf.Min(Time.deltaTime, 0.05f));
            Apply();
        }

        private bool Alive()
        {
            return _combatant == null || _combatant.IsAlive;
        }

        // ------------------------------------------------------------------ squelette

        private void Build()
        {
            if (_rig == null) return;

            AddJoint(Part.Pelvis, _rig.Pelvis, _rig.Spine, -1, 1.1f, _torsoStiffness * 1.3f, 12f);
            AddJoint(Part.Spine, _rig.Spine, _rig.Chest, (int)Part.Pelvis, 0.42f, _torsoStiffness, 18f);
            AddJoint(Part.Chest, _rig.Chest, _rig.Neck, (int)Part.Spine, 0.30f, _torsoStiffness, 24f);
            AddJoint(Part.Head, _rig.Neck, null, (int)Part.Chest, 0.055f, _headStiffness, 52f);

            AddLimb(Part.UpperArmLeft, Part.ForearmLeft, _rig.LeftArm, (int)Part.Chest, 0.07f, 0.035f, _armStiffness, 55f, 45f);
            AddLimb(Part.UpperArmRight, Part.ForearmRight, _rig.RightArm, (int)Part.Chest, 0.07f, 0.035f, _armStiffness, 55f, 45f);
            AddLimb(Part.ThighLeft, Part.ShinLeft, _rig.LeftLeg, (int)Part.Pelvis, 0.30f, 0.16f, _legStiffness, 26f, 22f);
            AddLimb(Part.ThighRight, Part.ShinRight, _rig.RightLeg, (int)Part.Pelvis, 0.30f, 0.16f, _legStiffness, 26f, 22f);

            _built = true;
        }

        private void AddJoint(Part part, Transform bone, Transform tip, int parent, float inertia,
            float stiffness, float limitDegrees)
        {
            if (bone == null) return;

            BoneSpring joint = new BoneSpring();
            joint.Bone = bone;
            joint.Tip = tip;
            joint.Parent = parent;
            joint.Inertia = Mathf.Max(0.005f, inertia);
            joint.Stiffness = Mathf.Max(1f, stiffness);
            joint.Limit = limitDegrees * Mathf.Deg2Rad;
            joint.Animated = bone.localRotation;

            _joints[(int)part] = joint;
        }

        private void AddLimb(Part upper, Part lower, IkLimb limb, int parent, float upperInertia,
            float lowerInertia, float stiffness, float upperLimit, float lowerLimit)
        {
            if (limb == null) return;

            AddJoint(upper, limb.Upper, limb.Lower, parent, upperInertia, stiffness, upperLimit);
            AddJoint(lower, limb.Lower, limb.End, (int)upper, lowerInertia, stiffness * 1.15f, lowerLimit);
        }

        // ------------------------------------------------------------------ impact

        private void OnDamaged(DamageInfo info)
        {
            if (!_built || !Alive()) return;

            float speed = info.Velocity.magnitude;
            float magnitude = (Mathf.Max(0.5f, info.ImpactForce) + speed * _velocityInfluence)
                              * _impulseScale * Mathf.Max(0f, GlobalScale);

            if (info.Blocked) magnitude *= _blockedScale;
            if (magnitude <= 0.0001f) return;

            Vector3 direction = info.StrikeDirection;

            // Une légère montée : un coup n'est jamais parfaitement horizontal, et une force
            // purement horizontale ne fait jamais basculer une tête vers le haut — or un direct
            // au menton relève toujours un peu le visage.
            direction = (direction + Vector3.up * 0.12f).normalized;

            int struck = Struck(info);
            Push(info.Point, direction * magnitude, struck);

            if (!info.Blocked && info.Zone == HitZone.Body) Fold(struck, magnitude);
        }

        /// <summary>
        /// Le buste se referme autour d'un coup au corps : rotation vers l'avant autour de l'axe
        /// gauche-droite du combattant. Plus le coup est bas, plus le pliage est franc.
        /// </summary>
        private void Fold(int struck, float magnitude)
        {
            if (_gutFold <= 0f) return;

            bool low = struck == (int)Part.Spine || struck == (int)Part.Pelvis;
            Vector3 forwardBend = transform.right * magnitude * _gutFold;

            BoneSpring spine = _joints[(int)Part.Spine];
            BoneSpring chest = _joints[(int)Part.Chest];

            if (spine != null) AddVelocity(spine, forwardBend * (low ? 1f : 0.4f));
            if (chest != null) AddVelocity(chest, forwardBend * (low ? 0.6f : 0.3f));
        }

        /// <summary>
        /// Applique une impulsion au corps depuis l'extérieur (chute, projection, objet lancé).
        /// Le point et la force sont en coordonnées monde.
        /// </summary>
        public void Push(Vector3 point, Vector3 impulse)
        {
            if (!_built) return;

            Push(point, impulse, Nearest(point, 0, PartCount));
        }

        private void Push(Vector3 point, Vector3 impulse, int struck)
        {
            if (struck < 0 || _joints[struck] == null) return;

            _lastPoint = point;
            _lastImpulse = impulse;
            _lastTime = Time.time;
            LastPart = ((Part)struck).ToString();

            // L'os touché reçoit tout ; chacun de ses parents reçoit une part décroissante,
            // calculée avec SON propre bras de levier. C'est ce qui fait qu'un même coup de tête
            // incline un peu le buste, et presque pas le bassin.
            float share = 1f;
            int index = struck;
            Vector3 headKick = Vector3.zero;

            while (index >= 0 && share > 0.01f)
            {
                BoneSpring joint = _joints[index];
                if (joint == null) break;

                Vector3 angular = AngularImpulse(joint, point, impulse) * share;
                AddVelocity(joint, angular);

                if (index == (int)Part.Head) headKick += angular;

                // Coup du lapin : un choc sur le TORSE laisse la tête en arrière.
                if ((index == (int)Part.Chest || index == (int)Part.Spine) && _whiplash > 0f)
                {
                    BoneSpring head = _joints[(int)Part.Head];

                    if (head != null)
                    {
                        Vector3 lag = -angular * _whiplash * Mathf.Sqrt(joint.Inertia / head.Inertia) * 0.35f;
                        AddVelocity(head, lag);
                        headKick += lag;
                    }
                }

                index = joint.Parent;
                share *= _chainFalloff;
            }

            KickCamera(headKick);
        }

        /// <summary>
        /// Vitesse angulaire (monde) produite par une impulsion linéaire appliquée en un point :
        /// ω = (r × J) / I. C'est toute la physique du système, et elle tient en une ligne.
        /// </summary>
        private static Vector3 AngularImpulse(BoneSpring joint, Vector3 point, Vector3 impulse)
        {
            Vector3 lever = point - joint.Bone.position;
            return Vector3.Cross(lever, impulse) / joint.Inertia;
        }

        private static void AddVelocity(BoneSpring joint, Vector3 angularWorld)
        {
            Transform parent = joint.Bone.parent;
            Vector3 local = parent != null ? parent.InverseTransformDirection(angularWorld) : angularWorld;

            joint.Velocity += local;
        }

        /// <summary>
        /// Choisit l'os touché à partir de la ZONE du coup, puis de la géométrie.
        ///
        /// La zone passe d'abord, et c'est indispensable : les hurtbox sont des volumes larges
        /// (38 cm de rayon pour le torse), donc le point d'impact est souvent plus près des
        /// avant-bras levés en garde que de la colonne. Choisir l'os « le plus proche » ferait
        /// partir les bras sur chaque coup au corps. La zone dit quelle famille d'os a été
        /// touchée ; la géométrie ne départage qu'à l'intérieur de cette famille.
        /// </summary>
        private int Struck(DamageInfo info)
        {
            if (info.Blocked)
            {
                int forearm = Nearest(info.Point, (int)Part.UpperArmLeft, (int)Part.ThighLeft);
                if (forearm >= 0) return forearm;
            }

            switch (info.Zone)
            {
                case HitZone.Head:
                    return _joints[(int)Part.Head] != null ? (int)Part.Head : (int)Part.Chest;

                case HitZone.Leg:
                    return Nearest(info.Point, (int)Part.ThighLeft, PartCount);

                default:
                    return NearestByHeight(info.Point);
            }
        }

        private int NearestByHeight(Vector3 point)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = (int)Part.Pelvis; i <= (int)Part.Chest; i++)
            {
                BoneSpring joint = _joints[i];
                if (joint == null) continue;

                Vector3 middle = (joint.Bone.position + TipPosition(joint)) * 0.5f;
                float distance = Mathf.Abs(middle.y - point.y);

                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        private int Nearest(Vector3 point, int from, int to)
        {
            int best = -1;
            float bestDistance = float.MaxValue;

            for (int i = from; i < to && i < PartCount; i++)
            {
                BoneSpring joint = _joints[i];
                if (joint == null) continue;

                float distance = DistanceToSegment(point, joint.Bone.position, TipPosition(joint));
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = i;
            }

            return best;
        }

        private static Vector3 TipPosition(BoneSpring joint)
        {
            if (joint.Tip != null) return joint.Tip.position;

            // La tête n'a pas d'os au-dessus d'elle : son segment va de la nuque au sommet du
            // crâne, 26 cm plus haut dans l'axe du cou.
            return joint.Bone.position + joint.Bone.up * 0.26f;
        }

        private static float DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
        {
            Vector3 ab = b - a;
            float lengthSquared = ab.sqrMagnitude;
            if (lengthSquared < 1e-6f) return Vector3.Distance(point, a);

            float t = Mathf.Clamp01(Vector3.Dot(point - a, ab) / lengthSquared);
            return Vector3.Distance(point, a + ab * t);
        }

        private void KickCamera(Vector3 angularWorld)
        {
            if (_cameraPunch == null || _cameraReference == null) return;
            if (angularWorld.sqrMagnitude < 1e-6f) return;

            BoneSpring head = _joints[(int)Part.Head];
            float stiffness = head != null ? head.Stiffness : _headStiffness;

            // Le pic d'un ressort lancé à la vitesse ω vaut à peu près ω / √k. On transmet ce
            // pic à la caméra : la vue part d'autant que la tête partirait, dans le même sens.
            Vector3 local = _cameraReference.InverseTransformDirection(angularWorld)
                            / Mathf.Sqrt(stiffness) * Mathf.Rad2Deg * _cameraSnap;

            local = Vector3.ClampMagnitude(local, _cameraSnapLimit);
            _cameraPunch.AddImpulse(Vector3.zero, local);
        }

        // ------------------------------------------------------------------ simulation

        private void Integrate(float dt)
        {
            if (dt <= 0f) return;

            // Sous-pas : un ressort raide intégré en un seul pas de 50 ms diverge. Découper le
            // pas garde l'intégration stable même quand le jeu rame.
            int steps = Mathf.Clamp(Mathf.CeilToInt(dt / 0.012f), 1, 6);
            float h = dt / steps;

            for (int i = 0; i < PartCount; i++)
            {
                BoneSpring joint = _joints[i];
                if (joint == null) continue;

                if (joint.Angle.sqrMagnitude < 1e-8f && joint.Velocity.sqrMagnitude < 1e-6f)
                {
                    joint.Angle = Vector3.zero;
                    joint.Velocity = Vector3.zero;
                    continue;
                }

                float k = joint.Stiffness;
                float c = 2f * Mathf.Sqrt(k) * _dampingRatio;

                for (int s = 0; s < steps; s++)
                {
                    Vector3 acceleration = -k * joint.Angle - c * joint.Velocity;
                    joint.Velocity += acceleration * h;
                    joint.Angle += joint.Velocity * h;

                    Limit(joint);
                }
            }
        }

        /// <summary>
        /// Butée articulaire. Arrivé à la limite, on supprime la composante de vitesse qui
        /// pousse DEHORS, pas toute la vitesse : sinon un coup violent colle la tête contre sa
        /// butée et elle y reste, au lieu d'en rebondir.
        /// </summary>
        private static void Limit(BoneSpring joint)
        {
            float angle = joint.Angle.magnitude;
            if (angle <= joint.Limit || angle < 1e-6f) return;

            Vector3 axis = joint.Angle / angle;
            joint.Angle = axis * joint.Limit;

            float outward = Vector3.Dot(joint.Velocity, axis);
            if (outward > 0f) joint.Velocity -= axis * outward * 1.4f;
        }

        private void Apply()
        {
            for (int i = 0; i < PartCount; i++)
            {
                BoneSpring joint = _joints[i];
                if (joint == null || joint.Bone == null) continue;

                float angle = joint.Angle.magnitude;
                if (angle < 1e-5f) continue;

                joint.Animated = joint.Bone.localRotation;
                joint.Bone.localRotation = Quaternion.AngleAxis(angle * Mathf.Rad2Deg, joint.Angle / angle)
                                           * joint.Animated;
                joint.Applied = true;
            }
        }

        private void Restore()
        {
            for (int i = 0; i < PartCount; i++)
            {
                BoneSpring joint = _joints[i];
                if (joint == null || !joint.Applied || joint.Bone == null) continue;

                joint.Bone.localRotation = joint.Animated;
                joint.Applied = false;
            }
        }

        private void ResetState()
        {
            for (int i = 0; i < PartCount; i++)
            {
                BoneSpring joint = _joints[i];
                if (joint == null) continue;

                joint.Angle = Vector3.zero;
                joint.Velocity = Vector3.zero;
                joint.Applied = false;
            }
        }

        private void OnDrawGizmos()
        {
            if (!_drawDebug || Time.time - _lastTime > 0.6f) return;

            Gizmos.color = new Color(1f, 0.85f, 0.2f, 0.9f);
            Gizmos.DrawWireSphere(_lastPoint, 0.04f);
            Gizmos.DrawLine(_lastPoint, _lastPoint + _lastImpulse.normalized * 0.35f);
        }
    }
}
