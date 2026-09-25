using System.Collections.Generic;
using UberBagarre.Combat;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace UberBagarre.View
{
    /// <summary>
    /// Anime un adversaire avec des animations CAPTURÉES (FS Melee Combat System) au lieu de
    /// poses calculées : garde de boxe qui danse, déplacements, coups, réactions aux coups,
    /// blocage, chutes, relevé, mort.
    ///
    /// Le combat, lui, ne change pas : c'est toujours notre exécuteur qui décide du coup, de sa
    /// fenêtre d'impact et de ses dégâts, notre système de chute qui décide de la chute, notre
    /// santé qui décide de la mort. Ce composant ne fait que JOUER ces décisions, en calant
    /// chaque clip sur elles :
    ///
    /// - un coup : le clip capturé est accéléré ou ralenti pour que SON impact tombe à l'instant
    ///   où NOTRE fenêtre d'impact s'ouvre ; puis le poing animé est guidé, par IK, vers la
    ///   vraie cible (le menton du joueur), exactement comme les coups calculés ;
    /// - le gel de contact suspend le clip, poing sur la cible ;
    /// - une réaction est choisie d'après le SENS du coup reçu : chaque clip est mesuré au
    ///   démarrage (de quel côté part la tête ?), pas deviné d'après son nom ;
    /// - la mort est une vraie chute animée, en arrière ou en avant selon le coup.
    ///
    /// Tout est joué par un graphe d'animation (Playables), sans contrôleur à maintenir. Les
    /// systèmes procéduraux (marche, bras) sont mis en veille tant que les animations capturées
    /// pilotent ; ils reprennent la main si on les coupe (menu de triche) ou pour un coup sans
    /// clip capturé.
    /// </summary>
    [DefaultExecutionOrder(150)]
    public class MocapDriver : MonoBehaviour
    {
        private enum Kind
        {
            None = 0,
            Attack = 1,
            Reaction = 2,
            Down = 3,
            GetUp = 4,
            Dead = 5,
            Dodge = 6
        }

        private class Slot
        {
            public AnimationClipPlayable Playable;
            public AnimationClip Clip;
            public bool Valid;
            public bool Hold;
            public float Speed = 1f;
        }

        /// <summary>Interrupteur général (menu de triche). Faux = retour aux poses calculées.</summary>
        public static bool GloballyEnabled = true;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            GloballyEnabled = true;
            ImpactCache.Clear();
        }

        [Header("References")]
        [SerializeField] private MocapLibrary _library;
        [SerializeField] private Animator _animator;
        [SerializeField] private BodyRig _rig;

        [SerializeField]
        [Tooltip("Racine du personnage (celle que le moteur deplace, toujours debout).")]
        private Transform _root;

        [SerializeField] private Combatant _combatant;
        [SerializeField] private AttackExecutor _executor;
        [SerializeField] private KnockdownSystem _knockdown;
        [SerializeField] private GuardSystem _guard;
        [SerializeField] private DodgeSystem _dodge;

        [SerializeField]
        [Tooltip("Le cerveau : engage, l'adversaire est en garde ; sinon il attend, bras le long du corps.")]
        private Behaviour _brain;

        [SerializeField]
        [Tooltip("Ce qui calcule le corps quand les animations capturees ne pilotent pas : marche " +
                 "procedurale, bras. Mis en veille tant qu'elles pilotent.")]
        private Behaviour[] _procedural = new Behaviour[0];

        [SerializeField]
        [Tooltip("La capsule du personnage : le deplacement contenu dans les animations passe par " +
                 "elle, donc par les collisions.")]
        private CharacterController _controller;

        [Header("Reglages")]
        [SerializeField, Min(0f)]
        [Tooltip("Ecart maximal corrige entre le poing anime et la vraie cible (m).")]
        private float _maxCorrection = 0.38f;

        [SerializeField, Min(0.02f)] private float _attackFade = 0.08f;
        [SerializeField, Min(0.02f)] private float _returnFade = 0.22f;
        [SerializeField, Min(0.5f)] private float _reactionSpeed = 1.15f;

        [SerializeField, Min(0f)]
        [Tooltip("Fondu de pose (s) quand on passe des animations capturees au corps calcule, ou " +
                 "l'inverse (coup sans clip capture, menu de triche).")]
        private float _switchBlend = 0.18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du pas des coups captures rendue au personnage. Notre elan couvre deja la " +
                 "distance jusqu'a la cible : tout garder la ferait parcourir deux fois.")]
        private float _attackRootMotion = 0.25f;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Part du recul des reactions, des chutes et du releve rendue au personnage.")]
        private float _reactionRootMotion = 1f;

        [Header("Debug")]
        [SerializeField] private bool _log;

        // Instant d'impact trouvé par clip (fraction), partagé par tous les combattants.
        private static readonly Dictionary<AnimationClip, float> ImpactCache = new Dictionary<AnimationClip, float>();

        private PlayableGraph _graph;
        private AnimationLayerMixerPlayable _layers;
        private AnimationMixerPlayable _locomotion;
        private AnimationMixerPlayable _actions;
        private AnimationClipPlayable _blockPlayable;
        private readonly AnimationClipPlayable[] _loco = new AnimationClipPlayable[7];
        private readonly AnimationClip[] _locoClips = new AnimationClip[7];
        private readonly Slot[] _slots = { new Slot(), new Slot() };
        private int _current;
        private float _slotBlend = 1f;
        private float _actionWeight;
        private float _actionTarget;
        private float _actionFadeSpeed = 10f;
        private float _blockWeight;
        private float _engaged;
        private Kind _kind;

        private bool _active;
        private bool _built;
        private bool _sampling;
        private bool _dead;
        private bool _proceduralAttack;

        private Vector3 _lastRootPosition;
        private Vector3 _velocity;

        private MocapMove _move;
        private float _moveImpact;
        private float _moveSpeed = 1f;
        private MocapMove _previousMove;

        private readonly Dictionary<AnimationClip, Vector3> _reactionHead = new Dictionary<AnimationClip, Vector3>();
        private readonly List<MocapMove> _candidates = new List<MocapMove>();

        private Transform[] _bones;
        private Vector3[] _bindPositions;
        private Quaternion[] _bindRotations;

        private readonly Quaternion[] _animated = new Quaternion[4];

        private Vector3[] _fromPositions;
        private Quaternion[] _fromRotations;
        private float _switchTimer = -1f;

        /// <summary>Vrai quand les animations capturées pilotent ce corps.</summary>
        public bool IsActive { get { return _active; } }

        private bool Usable
        {
            get
            {
                return _library != null && _library.IsUsable && _animator != null && _animator.avatar != null &&
                       _animator.avatar.isValid && _animator.avatar.isHuman && _rig != null;
            }
        }

        // ------------------------------------------------------------------ cycle

        private void Awake()
        {
            if (_root == null) _root = transform;
            if (_controller == null) _controller = GetComponent<CharacterController>();
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_executor == null) _executor = GetComponent<AttackExecutor>();
            if (_knockdown == null) _knockdown = GetComponent<KnockdownSystem>();
            if (_guard == null) _guard = GetComponent<GuardSystem>();
            if (_dodge == null) _dodge = GetComponent<DodgeSystem>();

            CaptureBind();
            _lastRootPosition = _root.position;
        }

        private void OnEnable()
        {
            if (_executor != null)
            {
                _executor.AttackStarted += OnAttackStarted;
                _executor.AttackEnded += OnAttackEnded;
                _executor.SideResolver = ResolveSide;
            }

            if (_combatant != null) _combatant.Damaged += OnDamaged;
            if (_dodge != null) _dodge.Dodged += OnDodged;

            if (_knockdown != null)
            {
                _knockdown.KnockedDown += OnKnockedDown;
                _knockdown.GettingUp += OnGettingUp;
                _knockdown.GotUp += OnGotUp;
            }
        }

        private void OnDisable()
        {
            if (_executor != null)
            {
                _executor.AttackStarted -= OnAttackStarted;
                _executor.AttackEnded -= OnAttackEnded;
                if (_executor.SideResolver == (System.Func<AttackData, HandSide, HandSide>)ResolveSide) _executor.SideResolver = null;
            }

            if (_combatant != null) _combatant.Damaged -= OnDamaged;
            if (_dodge != null) _dodge.Dodged -= OnDodged;

            if (_knockdown != null)
            {
                _knockdown.KnockedDown -= OnKnockedDown;
                _knockdown.GettingUp -= OnGettingUp;
                _knockdown.GotUp -= OnGotUp;
            }

            if (_active && !_dead) Deactivate();
        }

        private void OnDestroy()
        {
            if (_graph.IsValid()) _graph.Destroy();
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            // Relance du combat : le mort se relève, la garde reprend.
            if (_dead && _combatant != null && _combatant.IsAlive)
            {
                _dead = false;
                StopAction(_returnFade);
            }

            bool want = GloballyEnabled && Usable && !_proceduralAttack;
            if (_dead) want = _active;
            if (want != _active)
            {
                if (want) Activate();
                else Deactivate();
            }

            // Pendant un fondu de bascule, chaque image repart de la pose de liaison : un os
            // qu'un seul des deux systèmes écrit doit retrouver SA pose, pas la pose fondue de
            // l'image précédente.
            if (_switchTimer >= 0f) RestoreBind();

            if (!_active || dt <= 0f) return;

            UpdateLocomotion(dt);
            UpdateBlock(dt);
            UpdateAction(dt);
        }

        private void LateUpdate()
        {
            if (_active)
            {
                CorrectStrike();

                IkLimb left = _rig.LeftArm;
                IkLimb right = _rig.RightArm;
                if (left != null) left.UpdateTwist();
                if (right != null) right.UpdateTwist();
            }

            BlendSwitch();
        }

        /// <summary>Mémorise la pose affichée : le point de départ du fondu de bascule.</summary>
        private void CaptureSwitchPose()
        {
            if (_bones == null || _switchBlend <= 0f) return;

            if (_fromPositions == null || _fromPositions.Length != _bones.Length)
            {
                _fromPositions = new Vector3[_bones.Length];
                _fromRotations = new Quaternion[_bones.Length];
            }

            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;
                _fromPositions[i] = _bones[i].localPosition;
                _fromRotations[i] = _bones[i].localRotation;
            }

            _switchTimer = 0f;
        }

        /// <summary>
        /// Fond la pose mémorisée dans celle que le nouveau système vient d'écrire : pas de saut
        /// quand un coup sans clip capturé rend la main au corps calculé, ni au retour.
        /// </summary>
        private void BlendSwitch()
        {
            if (_switchTimer < 0f || _bones == null || _fromRotations == null) return;

            _switchTimer += Time.deltaTime;
            float w = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(_switchTimer / Mathf.Max(0.01f, _switchBlend)));
            if (w >= 1f)
            {
                _switchTimer = -1f;
                return;
            }

            for (int i = 0; i < _bones.Length; i++)
            {
                Transform bone = _bones[i];
                if (bone == null) continue;
                bone.localPosition = Vector3.Lerp(_fromPositions[i], bone.localPosition, w);
                bone.localRotation = Quaternion.Slerp(_fromRotations[i], bone.localRotation, w);
            }
        }

        /// <summary>
        /// Le déplacement d'une image d'animation (voir <see cref="MocapRootMotion"/>) : le pas
        /// d'un coup, le recul d'une réaction, la glissade d'une chute. Les clips sont importés
        /// « sur place » ; c'est ici qu'on rend ce déplacement au personnage, par sa capsule.
        /// La marche n'en rend rien : c'est notre moteur qui déplace, le clip s'y cale.
        /// </summary>
        public void ApplyRootMotion(Vector3 delta)
        {
            if (!_active || _sampling) return;

            float share;
            switch (_kind)
            {
                case Kind.Attack:
                    share = _attackRootMotion;
                    break;
                case Kind.Reaction:
                case Kind.Down:
                case Kind.GetUp:
                case Kind.Dead:
                    share = _reactionRootMotion;
                    break;
                default:
                    return;
            }

            delta.y = 0f;
            delta *= share * _actionWeight;
            if (delta.sqrMagnitude < 1e-10f) return;

            if (_controller != null && _controller.enabled) _controller.Move(delta);
            else if (_dead) _root.position += delta;
        }

        // ------------------------------------------------------------------ bascule

        private void Activate()
        {
            if (!_built) Build();
            if (!_built) return;

            CaptureSwitchPose();
            _animator.enabled = true;
            _animator.applyRootMotion = false;
            _animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            for (int i = 0; i < _procedural.Length; i++)
            {
                if (_procedural[i] != null) _procedural[i].enabled = false;
            }

            if (_knockdown != null) _knockdown.TiltEnabled = false;

            _graph.Play();
            _active = true;

            if (_reactionHead.Count == 0) MeasureReactions();
        }

        private void Deactivate()
        {
            _active = false;
            if (_graph.IsValid()) _graph.Stop();
            if (_animator != null) _animator.enabled = false;

            CaptureSwitchPose();
            RestoreBind();

            for (int i = 0; i < _procedural.Length; i++)
            {
                if (_procedural[i] != null) _procedural[i].enabled = true;
            }

            if (_knockdown != null) _knockdown.TiltEnabled = true;
        }

        private void CaptureBind()
        {
            if (_rig == null) return;

            _bones = _rig.GetComponentsInChildren<Transform>(true);
            _bindPositions = new Vector3[_bones.Length];
            _bindRotations = new Quaternion[_bones.Length];

            for (int i = 0; i < _bones.Length; i++)
            {
                _bindPositions[i] = _bones[i].localPosition;
                _bindRotations[i] = _bones[i].localRotation;
            }
        }

        /// <summary>Remet le squelette en pose de liaison : les systèmes calculés repartent d'une base propre.</summary>
        private void RestoreBind()
        {
            if (_bones == null) return;

            for (int i = 0; i < _bones.Length; i++)
            {
                if (_bones[i] == null) continue;
                _bones[i].localPosition = _bindPositions[i];
                _bones[i].localRotation = _bindRotations[i];
            }
        }

        // ------------------------------------------------------------------ graphe

        private void Build()
        {
            if (!Usable) return;

            _graph = PlayableGraph.Create(name + " (animations capturees)");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);

            AnimationPlayableOutput output = AnimationPlayableOutput.Create(_graph, "Corps", _animator);
            _layers = AnimationLayerMixerPlayable.Create(_graph, 3);
            output.SetSourcePlayable(_layers);

            // Couche 0 : garde et déplacements.
            _locoClips[0] = _library.combatIdle;
            _locoClips[1] = _library.walkForward;
            _locoClips[2] = _library.walkBack;
            _locoClips[3] = _library.walkLeft;
            _locoClips[4] = _library.walkRight;
            _locoClips[5] = _library.relaxedIdle != null ? _library.relaxedIdle : _library.combatIdle;
            _locoClips[6] = _library.relaxedWalk != null ? _library.relaxedWalk : _library.walkForward;

            _locomotion = AnimationMixerPlayable.Create(_graph, _locoClips.Length);
            for (int i = 0; i < _locoClips.Length; i++)
            {
                AnimationClip clip = _locoClips[i] != null ? _locoClips[i] : _library.combatIdle;
                _locoClips[i] = clip;
                _loco[i] = AnimationClipPlayable.Create(_graph, clip);
                _loco[i].SetApplyFootIK(true);
                _graph.Connect(_loco[i], 0, _locomotion, i);
                _locomotion.SetInputWeight(i, i == 0 ? 1f : 0f);
            }

            _graph.Connect(_locomotion, 0, _layers, 0);
            _layers.SetInputWeight(0, 1f);

            // Couche 1 : la garde haute (haut du corps seulement), quand il se couvre.
            if (_library.block != null)
            {
                _blockPlayable = AnimationClipPlayable.Create(_graph, _library.block);
                _blockPlayable.SetSpeed(0);
                _graph.Connect(_blockPlayable, 0, _layers, 1);
                _layers.SetLayerMaskFromAvatarMask(1, UpperBodyMask());
            }

            _layers.SetInputWeight(1, 0f);

            // Couche 2 : l'action en cours (coup, réaction, chute), deux emplacements pour fondre.
            _actions = AnimationMixerPlayable.Create(_graph, 2);
            _graph.Connect(_actions, 0, _layers, 2);
            _layers.SetInputWeight(2, 0f);

            _built = true;
        }

        private static AvatarMask UpperBodyMask()
        {
            AvatarMask mask = new AvatarMask();
            for (int i = 0; i < (int)AvatarMaskBodyPart.LastBodyPart; i++)
            {
                mask.SetHumanoidBodyPartActive((AvatarMaskBodyPart)i, false);
            }

            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.Head, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightArm, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.LeftFingers, true);
            mask.SetHumanoidBodyPartActive(AvatarMaskBodyPart.RightFingers, true);
            return mask;
        }

        // ------------------------------------------------------------------ déplacements

        private void UpdateLocomotion(float dt)
        {
            Vector3 position = _root.position;
            Vector3 raw = (position - _lastRootPosition) / dt;
            _lastRootPosition = position;
            raw.y = 0f;

            // Une téléportation (réapparition) ne doit pas se lire comme un sprint.
            if (raw.sqrMagnitude > 100f) raw = Vector3.zero;
            _velocity = Vector3.Lerp(_velocity, raw, 1f - Mathf.Exp(-10f * dt));

            Vector3 local = _root.InverseTransformDirection(_velocity);
            float speed = new Vector2(local.x, local.z).magnitude;
            float move = Mathf.Clamp01((speed - 0.08f) / 0.35f);

            bool engaged = _brain == null || (_brain.enabled && !Enemy.EnemyBrain.HoldAll);
            _engaged = Mathf.MoveTowards(_engaged, engaged ? 1f : 0f, dt * 2.5f);

            float fwd = 0f, back = 0f, left = 0f, right = 0f;
            if (speed > 0.001f)
            {
                fwd = Mathf.Max(0f, local.z) / speed;
                back = Mathf.Max(0f, -local.z) / speed;
                right = Mathf.Max(0f, local.x) / speed;
                left = Mathf.Max(0f, -local.x) / speed;
                float sum = fwd + back + left + right;
                if (sum > 0.0001f)
                {
                    fwd /= sum;
                    back /= sum;
                    left /= sum;
                    right /= sum;
                }
            }

            float e = _engaged;
            float[] w =
            {
                (1f - move) * e,
                move * e * fwd,
                move * e * back,
                move * e * left,
                move * e * right,
                (1f - move) * (1f - e),
                move * (1f - e)
            };

            // Chaque clip de marche joue à la vitesse réelle du corps : pas de pieds qui glissent.
            float forwardRate = WalkRate(speed, _library.walkSpeed);
            float sideRate = WalkRate(speed, _library.strafeSpeed);
            float relaxedRate = WalkRate(speed, _library.relaxedWalkSpeed);

            for (int i = 0; i < w.Length; i++)
            {
                double rate = 1.0;
                if (i == 1 || i == 2) rate = forwardRate;
                else if (i == 3 || i == 4) rate = sideRate;
                else if (i == 6) rate = relaxedRate;

                _locomotion.SetInputWeight(i, w[i]);
                _loco[i].SetSpeed(rate);

                // Bouclage explicite : un clip importé sans « Loop Time » s'arrêterait net.
                AnimationClip clip = _locoClips[i];
                if (clip != null && clip.length > 0.01f)
                {
                    double time = _loco[i].GetTime();
                    if (time > clip.length) _loco[i].SetTime(time % clip.length);
                }
            }
        }

        private static float WalkRate(float speed, float clipSpeed)
        {
            return Mathf.Clamp(speed / Mathf.Max(0.1f, clipSpeed), 0.35f, 1.8f);
        }

        private void UpdateBlock(float dt)
        {
            if (!_blockPlayable.IsValid()) return;

            bool covering = _guard != null && _guard.IsGuarding && _kind != Kind.Attack && !_dead &&
                            (_knockdown == null || !_knockdown.IsDown);
            _blockWeight = Mathf.MoveTowards(_blockWeight, covering ? 1f : 0f, dt * 7f);
            _layers.SetInputWeight(1, _blockWeight);
        }

        // ------------------------------------------------------------------ actions

        private void PlayAction(AnimationClip clip, float speed, float fadeIn, bool hold, Kind kind)
        {
            if (clip == null || !_built) return;

            int next = 1 - _current;
            Slot slot = _slots[next];

            if (slot.Valid)
            {
                _graph.Disconnect(_actions, next);
                slot.Playable.Destroy();
            }

            slot.Playable = AnimationClipPlayable.Create(_graph, clip);
            slot.Playable.SetApplyFootIK(true);
            slot.Playable.SetTime(0);
            slot.Playable.SetSpeed(speed);
            slot.Clip = clip;
            slot.Hold = hold;
            slot.Speed = speed;
            slot.Valid = true;
            _graph.Connect(slot.Playable, 0, _actions, next);

            _current = next;
            _slotBlend = _actionWeight > 0.01f ? 0f : 1f;
            _actionTarget = 1f;
            _actionFadeSpeed = 1f / Mathf.Max(0.02f, fadeIn);
            _kind = kind;

            _actions.SetInputWeight(_current, _slotBlend);
            _actions.SetInputWeight(1 - _current, 1f - _slotBlend);
        }

        private void StopAction(float fadeOut)
        {
            _actionTarget = 0f;
            _actionFadeSpeed = 1f / Mathf.Max(0.02f, fadeOut);
            _kind = Kind.None;
            _move = null;
        }

        private void UpdateAction(float dt)
        {
            _actionWeight = Mathf.MoveTowards(_actionWeight, _actionTarget, _actionFadeSpeed * dt);
            _slotBlend = Mathf.MoveTowards(_slotBlend, 1f, _actionFadeSpeed * dt);
            _layers.SetInputWeight(2, _actionWeight);

            if (!_built) return;
            _actions.SetInputWeight(_current, _slotBlend);
            _actions.SetInputWeight(1 - _current, 1f - _slotBlend);

            Slot slot = _slots[_current];
            if (!slot.Valid) return;

            // Gel de contact : le clip s'arrête avec le geste, poing sur la cible.
            if (_kind == Kind.Attack && _executor != null)
            {
                slot.Playable.SetSpeed(_executor.InContact ? 0.0 : slot.Speed);
            }

            double time = slot.Playable.GetTime();
            double length = slot.Clip.length;

            if (time < length - 0.001) return;

            if (slot.Hold)
            {
                slot.Playable.SetSpeed(0);
                slot.Playable.SetTime(Mathf.Max(0f, (float)length - 0.017f));
            }
            else if (_kind != Kind.None && _actionTarget > 0f)
            {
                StopAction(_returnFade);
            }
        }

        // ------------------------------------------------------------------ coups

        private HandSide ResolveSide(AttackData attack, HandSide proposed)
        {
            if (!_active || attack == null || _library == null) return proposed;

            _library.Find(attack.name, proposed, false, _candidates);
            if (_candidates.Count > 0) return proposed;

            HandSide other = proposed == HandSide.Left ? HandSide.Right : HandSide.Left;
            _library.Find(attack.name, other, false, _candidates);
            return _candidates.Count > 0 ? other : proposed;
        }

        private void OnAttackStarted(AttackData attack, HandSide side)
        {
            if (_dead || !GloballyEnabled || !Usable) return;

            _library.Find(attack.name, side, _executor != null && _executor.CurrentIsBodyShot, _candidates);

            if (_candidates.Count == 0)
            {
                // Pas de clip capturé pour ce coup (coup de tête, bousculade) : le corps calculé
                // le joue, le temps du coup.
                _proceduralAttack = true;
                if (_active) Deactivate();
                return;
            }

            if (!_active) Activate();
            if (!_active) return;

            MocapMove move = _candidates[Random.Range(0, _candidates.Count)];
            if (_candidates.Count > 1 && move == _previousMove)
            {
                move = _candidates[(_candidates.IndexOf(move) + 1) % _candidates.Count];
            }

            _previousMove = move;
            _move = move;

            PlayAction(move.clip, 1f, _attackFade, false, Kind.Attack);

            // Le clip est calé sur NOTRE fenêtre d'impact : accéléré ou ralenti pour que son
            // impact tombe à l'instant où l'exécuteur ouvre la hitbox.
            float impact = move.impact >= 0f ? move.impact : FindImpact(move);
            _moveImpact = impact * move.clip.length;

            float delay = _executor != null ? Mathf.Max(0.05f, _executor.ImpactDelay) : _moveImpact;
            _moveSpeed = Mathf.Clamp(_moveImpact / delay, 0.45f, 2.6f);

            Slot slot = _slots[_current];
            slot.Speed = _moveSpeed;
            slot.Playable.SetSpeed(_moveSpeed);

            if (_log)
            {
                Debug.Log("[UberBagarre] " + name + " : " + attack.displayName + " -> clip « " + move.clip.name +
                          " » x" + _moveSpeed.ToString("0.00") + ", impact a " + _moveImpact.ToString("0.00") + " s", this);
            }
        }

        private void OnAttackEnded(AttackData attack)
        {
            if (_proceduralAttack)
            {
                _proceduralAttack = false;
                return;
            }

            if (_kind == Kind.Attack) StopAction(_returnFade);
        }

        /// <summary>
        /// Le poing animé, guidé vers la vraie cible. Le clip garde sa trajectoire pendant
        /// l'armement ; la correction monte tard (carré d'une montée douce) et vaut 1 à
        /// l'impact : à cet instant, les jointures sont SUR le menton visé, quelle que soit la
        /// taille des deux corps. Puis elle se relâche avec le retour du bras.
        /// </summary>
        private void CorrectStrike()
        {
            if (_kind != Kind.Attack || _move == null || _executor == null) return;
            if (_move.limb != MocapLimb.LeftHand && _move.limb != MocapLimb.RightHand) return;

            StrikeTarget target = _executor.Target;
            if (!target.Valid || target.Anchor == null) return;

            Slot slot = _slots[_current];
            if (!slot.Valid) return;

            float t = (float)slot.Playable.GetTime();
            float impact = Mathf.Max(0.05f, _moveImpact);
            float rise = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / impact));
            float w = t <= impact
                ? rise * rise
                : 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - impact) / Mathf.Max(0.05f, slot.Clip.length * 0.35f)));
            w *= _actionWeight * _slotBlend;
            if (w <= 0.001f) return;

            HandSide side = _move.limb == MocapLimb.LeftHand ? HandSide.Left : HandSide.Right;
            IkLimb arm = _rig.Arm(side);
            if (arm == null || arm.End == null || arm.Upper == null || arm.Lower == null) return;

            Vector3 delta = Vector3.ClampMagnitude(target.WorldPoint - EffectorPosition(_move.limb), _maxCorrection);

            Transform wrist = arm.End;
            Vector3 shoulder = arm.RootPosition;
            Vector3 reach = wrist.position - shoulder;
            Vector3 pole = reach.sqrMagnitude > 1e-6f
                ? Vector3.ProjectOnPlane(arm.Lower.position - shoulder, reach.normalized)
                : Vector3.zero;
            if (pole.sqrMagnitude < 1e-6f) pole = -_root.up;

            // On garde la pose animée pour fondre : au tout début de la correction, l'IK ne doit
            // pas imposer SON roulis du bras à la place de celui de l'animation.
            _animated[0] = arm.Upper.localRotation;
            _animated[1] = arm.Lower.localRotation;
            _animated[2] = wrist.localRotation;

            arm.ApplyWorldPose(wrist.position + delta * w, wrist.rotation, pole.normalized);

            float blend = Mathf.Clamp01(w * 4f);
            arm.Upper.localRotation = Quaternion.Slerp(_animated[0], arm.Upper.localRotation, blend);
            arm.Lower.localRotation = Quaternion.Slerp(_animated[1], arm.Lower.localRotation, blend);
            wrist.localRotation = Quaternion.Slerp(_animated[2], wrist.localRotation, blend);
        }

        /// <summary>
        /// Instant d'impact d'un clip sans donnée : là où le membre qui frappe est le plus loin
        /// devant le bassin. Mesuré une fois par clip, en posant le corps à 16 instants.
        /// </summary>
        private float FindImpact(MocapMove move)
        {
            float cached;
            if (ImpactCache.TryGetValue(move.clip, out cached)) return cached;

            float best = 0.42f;
            float bestScore = float.MinValue;
            float length = move.clip.length;

            for (int i = 0; i <= 15; i++)
            {
                float fraction = Mathf.Lerp(0.15f, 0.8f, i / 15f);
                Vector3 local;
                Transform effector;
                if (!Sample(_current, fraction * length, move.limb, out local, out effector)) break;

                Vector3 pelvis = _root.InverseTransformPoint(_rig.Pelvis.position);
                Vector3 d = local - pelvis;
                float score = d.z + 0.3f * d.y;
                if (score <= bestScore) continue;

                bestScore = score;
                best = fraction;
            }

            ImpactCache[move.clip] = best;
            return best;
        }

        /// <summary>
        /// Pose le corps sur le clip de l'emplacement <paramref name="slot"/> à l'instant donné,
        /// lit la position du membre (dans le repère de la racine), et rend la pose au graphe.
        /// </summary>
        private bool Sample(int slotIndex, float time, MocapLimb limb, out Vector3 local, out Transform effector)
        {
            local = Vector3.zero;
            effector = null;

            Slot slot = _slots[slotIndex];
            if (!_built || !slot.Valid) return false;

            float l0 = _layers.GetInputWeight(0);
            float l1 = _layers.GetInputWeight(1);
            float l2 = _layers.GetInputWeight(2);
            float s0 = _actions.GetInputWeight(0);
            float s1 = _actions.GetInputWeight(1);
            double saved = slot.Playable.GetTime();

            _layers.SetInputWeight(0, 0f);
            _layers.SetInputWeight(1, 0f);
            _layers.SetInputWeight(2, 1f);
            _actions.SetInputWeight(slotIndex, 1f);
            _actions.SetInputWeight(1 - slotIndex, 0f);
            slot.Playable.SetTime(time);
            _sampling = true;
            _graph.Evaluate(0f);
            _sampling = false;

            local = _root.InverseTransformPoint(EffectorPosition(limb));

            slot.Playable.SetTime(saved);
            _layers.SetInputWeight(0, l0);
            _layers.SetInputWeight(1, l1);
            _layers.SetInputWeight(2, l2);
            _actions.SetInputWeight(0, s0);
            _actions.SetInputWeight(1, s1);
            return true;
        }

        private Vector3 EffectorPosition(MocapLimb limb)
        {
            switch (limb)
            {
                case MocapLimb.LeftHand:
                    return _rig.LeftHand != null ? _rig.LeftHand.KnucklePosition : _rig.LeftArm.End.position;
                case MocapLimb.RightHand:
                    return _rig.RightHand != null ? _rig.RightHand.KnucklePosition : _rig.RightArm.End.position;
                case MocapLimb.LeftFoot:
                    return _rig.LeftLeg.End.position;
                default:
                    return _rig.RightLeg.End.position;
            }
        }

        // ------------------------------------------------------------------ réactions

        /// <summary>
        /// Dans quel sens chaque réaction envoie la tête : mesuré, une fois, sur ce corps. Les
        /// noms des clips (« Left Hit », « Right Hit ») ne disent pas de quel côté vient le coup.
        /// </summary>
        private void MeasureReactions()
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            clips.AddRange(_library.lightReactions);
            clips.AddRange(_library.heavyReactions);
            clips.AddRange(_library.counterReactions);
            if (_library.uppercutReaction != null) clips.Add(_library.uppercutReaction);

            Transform head = _rig.Head != null ? _rig.Head : _rig.Neck;
            if (head == null) return;

            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null || _reactionHead.ContainsKey(clip)) continue;

                PlayAction(clip, 0f, 0.02f, false, Kind.None);
                Vector3 start, peak;
                Transform unused;
                Sample(_current, 0f, MocapLimb.RightHand, out start, out unused);
                start = _root.InverseTransformPoint(head.position);
                Sample(_current, Mathf.Min(0.35f, clip.length * 0.4f), MocapLimb.RightHand, out peak, out unused);
                peak = _root.InverseTransformPoint(head.position);
                _reactionHead[clip] = peak - start;
            }

            StopAction(0.02f);
            _actionWeight = 0f;
            _layers.SetInputWeight(2, 0f);
        }

        private void OnDamaged(Combatant self, DamageInfo info)
        {
            if (!_active || _dead) return;

            if (_knockdown != null && _knockdown.IsDown)
            {
                if (_library.groundHit != null && _kind == Kind.Down) PlayAction(_library.groundHit, 1f, 0.06f, true, Kind.Down);
                return;
            }

            if (info.Blocked)
            {
                if (_library.blockHit != null) PlayAction(_library.blockHit, 1.3f, 0.05f, false, Kind.Reaction);
                return;
            }

            AnimationClip clip = ChooseReaction(info);
            if (clip == null) return;

            PlayAction(clip, _reactionSpeed, 0.05f, false, Kind.Reaction);
        }

        /// <summary>
        /// Esquive en arrière : le retrait du buste capturé. Le déplacement reste celui de notre
        /// système d'esquive (le clip n'en rend rien) ; les esquives de côté gardent les pas chassés.
        /// </summary>
        private void OnDodged(Vector3 direction)
        {
            if (!_active || _dead || _library.dodgeBack == null || _kind == Kind.Attack) return;
            if (_knockdown != null && _knockdown.IsDown) return;

            Vector3 local = _root.InverseTransformDirection(direction);
            if (local.z > -0.35f) return;

            PlayAction(_library.dodgeBack, 1f, 0.05f, false, Kind.Dodge);
        }

        private AnimationClip ChooseReaction(DamageInfo info)
        {
            Vector3 strike = info.StrikeDirection;
            Vector3 local = _root.InverseTransformDirection(strike);

            if (info.IsRiposte && _library.counterReactions.Count > 0)
            {
                return Best(_library.counterReactions, local);
            }

            bool uppercut = info.Attack != null && info.Attack.name == AttackData.UppercutAsset;
            if (uppercut && _library.uppercutReaction != null) return _library.uppercutReaction;

            bool heavy = info.IsHeavy || info.Amount >= 14f || info.ChargeLevel > 0.5f;
            if (heavy && _library.heavyReactions.Count > 0) return Best(_library.heavyReactions, local);

            return Best(_library.lightReactions, local);
        }

        /// <summary>La réaction dont la tête part le plus dans le sens du coup.</summary>
        private AnimationClip Best(List<AnimationClip> clips, Vector3 localStrike)
        {
            AnimationClip best = null;
            float bestScore = float.MinValue;
            Vector3 flat = new Vector3(localStrike.x, 0f, localStrike.z);
            if (flat.sqrMagnitude > 1e-6f) flat.Normalize();

            for (int i = 0; i < clips.Count; i++)
            {
                AnimationClip clip = clips[i];
                if (clip == null) continue;

                Vector3 head;
                float score = Random.value * 0.15f;
                if (_reactionHead.TryGetValue(clip, out head))
                {
                    Vector3 h = new Vector3(head.x, 0f, head.z);
                    if (h.sqrMagnitude > 1e-6f) score += Vector3.Dot(h.normalized, flat);
                }

                if (score <= bestScore) continue;
                bestScore = score;
                best = clip;
            }

            return best;
        }

        // ------------------------------------------------------------------ chutes et mort

        private AnimationClip FallClip(Vector3 worldDirection)
        {
            Vector3 flat = worldDirection;
            flat.y = 0f;
            bool forward = flat.sqrMagnitude > 1e-6f && Vector3.Dot(flat.normalized, _root.forward) > 0.2f;

            AnimationClip clip = forward ? _library.knockDownFront : _library.knockDownBack;
            if (clip == null) clip = forward ? _library.knockDownBack : _library.knockDownFront;
            return clip;
        }

        private void OnKnockedDown()
        {
            if (!_active || _dead || _knockdown == null) return;

            AnimationClip clip = FallClip(_knockdown.LastFallDirection);
            if (clip == null) return;

            // Le temps au sol se cale sur la chute animée : on ne se relève pas avant d'avoir
            // touché terre.
            float getUp = _library.gettingUp != null ? _library.gettingUp.length : 1.05f;
            _knockdown.SetTimings(clip.length * 0.9f, 1.1f, getUp);

            PlayAction(clip, 1f, 0.06f, true, Kind.Down);
        }

        private void OnGettingUp()
        {
            if (!_active || _dead || _library.gettingUp == null) return;
            PlayAction(_library.gettingUp, 1f, 0.25f, false, Kind.GetUp);
        }

        private void OnGotUp()
        {
            if (!_active || _dead) return;
            if (_kind == Kind.GetUp || _kind == Kind.Down) StopAction(_returnFade);
        }

        /// <summary>
        /// La mort, animée : chute en arrière (coup de face) ou en avant (coup dans le dos),
        /// puis le corps reste au sol. Renvoie faux si les animations ne peuvent pas la jouer —
        /// le ragdoll prend alors le relais.
        /// </summary>
        public bool PlayDeath(DamageInfo info)
        {
            if (!_active || !_built) return false;

            Vector3 direction = info.StrikeDirection;
            if (direction.sqrMagnitude < 1e-6f) direction = info.Direction;

            AnimationClip clip = FallClip(direction);
            if (clip == null) return false;

            _dead = true;
            _blockWeight = 0f;
            if (_blockPlayable.IsValid()) _layers.SetInputWeight(1, 0f);

            // Déjà en train de tomber (la chute définitive part du même coup) : on laisse
            // cette chute finir au lieu de la recommencer.
            Slot slot = _slots[_current];
            if (_kind == Kind.Down && slot.Valid &&
                (slot.Clip == _library.knockDownBack || slot.Clip == _library.knockDownFront))
            {
                _kind = Kind.Dead;
                return true;
            }

            PlayAction(clip, 0.95f, 0.05f, true, Kind.Dead);
            return true;
        }
    }
}
