using System;
using UberBagarre.Core;
using UberBagarre.Feedback;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Joue un coup : anime le membre, tourne le corps, entraîne la caméra, ouvre la fenêtre
    /// d'impact — et fait en sorte que le coup ARRIVE sur l'adversaire.
    ///
    /// Ce composant ne lit aucune entrée et ne choisit aucun coup : on lui en donne un. Le
    /// joueur et l'ennemi utilisent exactement le même, piloté par la souris ou par l'IA.
    ///
    /// Ce qui fait qu'un coup se lit comme un vrai coup, et que la version précédente n'avait
    /// pas :
    ///
    /// 1. LA TRAJECTOIRE CONTINUE. Les poses-clés sont reliées par une courbe dont la vitesse
    ///    traverse les clés (voir <see cref="AttackData.Sample"/>) : armement, départ explosif,
    ///    extension, retour, sans l'arrêt robotique à chaque clé.
    /// 2. LA VRAIE CIBLE. Au lancement, on accroche un point de la surface de l'adversaire
    ///    (menton, plexus, côtes — voir <see cref="StrikeTarget"/>) ; au fil du geste, le poing
    ///    est guidé vers lui. L'impact a lieu quand les jointures arrivent sur la peau, pas à
    ///    50 cm devant soi dans le vide.
    /// 3. L'ÉLAN. Hors de portée de quelques dizaines de centimètres, le corps se jette dans le
    ///    coup (un pas glissé) au lieu de frapper l'air.
    /// 4. LE CONTACT. À l'impact, le poing reste collé à la cible quelques centièmes de seconde
    ///    (le « hit-lag » des jeux de combat), puis repart sans traverser : on voit le poing
    ///    s'écraser, la tête partir, la matière gicler.
    /// 5. LA CAMÉRA suit le geste (plongée, enroulement, relevé), zoome au contact, et passe au
    ///    ralenti sur un contre ou sur le coup qui met K.O.
    /// </summary>
    public class AttackExecutor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Optionnel. S'il est renseigne, l'etat et l'endurance conditionnent les coups.")]
        private Combatant _combatant;

        [SerializeField] private FirstPersonHands _hands;
        [SerializeField] private ProceduralLocomotion _locomotion;
        [SerializeField] private CameraPunch _cameraPunch;
        [SerializeField] private HitStop _hitStop;

        [SerializeField]
        [Tooltip("Le moteur de deplacement (IImpulseReceiver) : il recoit l'elan des coups hors de portee.")]
        private MonoBehaviour _impulseReceiver;

        [SerializeField]
        [Tooltip("Repere des poses de PIED. A laisser sur la racine du personnage : origine au " +
                 "sol et orientation du corps. Si on utilisait le repere de visee, regarder le " +
                 "ciel enverrait le coup de pied en l'air.")]
        private Transform _footPoseSpace;

        [Header("Hitbox par membre")]
        [SerializeField] private Hitbox _leftHitbox;
        [SerializeField] private Hitbox _rightHitbox;
        [SerializeField] private Hitbox _leftFootHitbox;
        [SerializeField] private Hitbox _rightFootHitbox;

        [SerializeField]
        [Tooltip("Hitbox du front, pour le coup de tete. Sur le joueur elle vit sous la camera.")]
        private Hitbox _headHitbox;

        [SerializeField]
        [Tooltip("Optionnel. Fournit la riposte : le coup qui suit une parade reussie est renforce.")]
        private GuardSystem _guard;

        [SerializeField]
        [Tooltip("Optionnel (IHandMotion) : des gestes captures pour les coups de poing (MocapArms). " +
                 "Ciblage, guidage, elan et gel de contact restent ceux de l'executeur.")]
        private MonoBehaviour _handMotionSource;

        [Header("Frappe")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Guidage du poing vers la vraie cible. 0 = la pose ecrite, telle quelle.")]
        private float _reachAssist = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Ecart maximal entre la pose ecrite et la cible. Au-dela, le coup part dans le vide.")]
        private float _maxRetarget = 0.45f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Suivi de la cible PENDANT le coup. 1 = le poing suit la tete qui bouge (joueur). " +
                 "Faible pour l'IA : le coup part la ou etait le joueur, et une esquive le fait rater.")]
        private float _tracking = 1f;

        [SerializeField, Min(0f)]
        [Tooltip("Armement tenu en plus, en secondes. Pour l'IA : le coup se VOIT venir.")]
        private float _telegraph;

        [SerializeField, Min(0f)]
        [Tooltip("Vitesse maximale du pas glisse qui accompagne un coup hors de portee (m/s).")]
        private float _maxLungeSpeed = 3.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Amplification de la rotation du buste ecrite dans les coups.")]
        private float _bodyScale = 1.25f;

        [SerializeField]
        [Tooltip("Mise en scene : ralenti et zoom sur un contre et sur le coup qui met K.O. " +
                 "(joueur uniquement).")]
        private bool _cinematic;

        [Header("Debug")]
        [SerializeField] private bool _logAttacks;

        [SerializeField]
        [Tooltip("Journalise la RAISON de chaque coup refuse de configuration.")]
        private bool _logRefusals = true;

        private AttackData _attack;
        private HandSide _side;
        private int _variantIndex = -1;
        private int _previousVariant = -1;
        private float _elapsed;
        private float _cooldown;
        private bool _hitWindowOpen;
        private bool _lastHandWasLead;
        private float _charge;
        private float _riposteMultiplier = 1f;

        private AttackData _charging;
        private float _chargeTime;

        private StrikeTarget _target;
        private bool _hasTargetPose;
        private Vector3 _targetPose;
        private Vector3 _designImpact;
        private bool _motionActive;
        private float _hitLag;
        private bool _contacted;
        private int _chain;
        private float _lastEndTime = -10f;

        private Vector3 _previousFist;
        private bool _hasPreviousFist;
        private Vector3 _fistVelocity;

        public event Action<AttackData, HandSide> AttackStarted;
        public event Action<AttackData> AttackEnded;
        public event Action<AttackData, Hurtbox, Vector3> HitLanded;

        /// <summary>Raison du dernier refus. Affichée par l'overlay de debug.</summary>
        public string LastRefusal { get; private set; }

        /// <summary>Niveau de charge du coup en cours, 0 à 1.</summary>
        public float Charge { get { return _charge; } }

        public float ChargeProgress
        {
            get
            {
                if (_charging == null) return 0f;
                return Mathf.Clamp01(_chargeTime / Mathf.Max(0.05f, _charging.maxChargeTime));
            }
        }

        public bool IsCharging { get { return _charging != null; } }
        public AttackData ChargingAttack { get { return _charging; } }
        public bool IsAttacking { get { return _attack != null; } }
        public bool IsHitWindowOpen { get { return _hitWindowOpen; } }

        /// <summary>Nombre de coups enchaînés sans temps mort (0 = premier coup).</summary>
        public int Chain { get { return _chain; } }

        /// <summary>La cible du coup en cours (non valide si le coup part dans le vide).</summary>
        public StrikeTarget Target { get { return _target; } }

        /// <summary>Le côté du coup en cours.</summary>
        public HandSide CurrentSide { get { return _side; } }

        /// <summary>Nom de la variante jouée (« 02 - au corps »…), vide sans coup.</summary>
        public string CurrentVariantName
        {
            get
            {
                if (_attack == null || _variantIndex < 0 || _attack.variants == null || _variantIndex >= _attack.variants.Count) return string.Empty;
                return _attack.variants[_variantIndex].name ?? string.Empty;
            }
        }

        /// <summary>Vrai si la variante en cours vise le corps (plexus, côtes, foie).</summary>
        public bool CurrentIsBodyShot
        {
            get
            {
                string v = CurrentVariantName;
                return v.Contains("corps") || v.Contains("plexus") || v.Contains("foie") || v.Contains("cotes");
            }
        }

        /// <summary>Vrai pendant le gel de contact : le geste est suspendu, poing sur la cible.</summary>
        public bool InContact { get { return _hitLag > 0f; } }

        /// <summary>
        /// Secondes entre le lancement du coup en cours et son impact prévu, armement de l'IA
        /// compris. Une animation capturée s'y cale : son impact tombe au même instant.
        /// </summary>
        public float ImpactDelay
        {
            get
            {
                if (_attack == null) return 0f;

                float duration = EffectiveDuration;
                float impact = _attack.ImpactTime;
                float telegraph = Telegraph;
                if (telegraph <= 0f) return impact * duration;

                float wind = Mathf.Clamp(_attack.hitWindowStart * 0.5f, 0.05f, 0.3f);
                float windTime = wind * duration + telegraph;
                if (impact <= wind) return impact / wind * windTime;
                return windTime + (impact - wind) * duration;
            }
        }

        /// <summary>
        /// Choix du côté imposé de l'extérieur : les animations capturées n'existent pas
        /// forcément des deux côtés (l'uppercut capturé est un gauche). Reçoit le côté proposé,
        /// rend celui à jouer.
        /// </summary>
        public Func<AttackData, HandSide, HandSide> SideResolver { get; set; }

        public Vector3 ActiveFistPosition
        {
            get
            {
                Hitbox hitbox = ActiveHitbox();
                return hitbox != null ? hitbox.Origin.position : transform.position;
            }
        }

        public bool CanChain
        {
            get { return _attack != null && Progress >= _attack.comboCancelAt; }
        }

        public bool IsReady
        {
            get
            {
                if (_cooldown > 0f) return false;
                if (_attack != null) return CanChain;
                return _combatant == null || _combatant.CanAct;
            }
        }

        public AttackData CurrentAttack { get { return _attack; } }

        /// <summary>Durée du geste, vitesse d'attaque comprise (sans l'armement de l'IA).</summary>
        public float EffectiveDuration
        {
            get
            {
                if (_attack == null) return 0f;

                float speed = _combatant != null && _combatant.Stats != null
                    ? _combatant.Stats.Get(StatType.AttackSpeed)
                    : 1f;

                return Mathf.Max(0.04f, _attack.duration / Mathf.Max(0.1f, speed));
            }
        }

        /// <summary>Durée totale, armement tenu compris.</summary>
        private float TotalDuration
        {
            get { return EffectiveDuration + Telegraph; }
        }

        private float Telegraph
        {
            get { return _attack != null && _attack.limb != AttackLimb.Foot ? _telegraph : _telegraph * 0.6f; }
        }

        /// <summary>Progression du coup en cours, 0 à 1, dans le temps du geste.</summary>
        public float Progress
        {
            get { return _attack == null ? 0f : Normalized(_elapsed); }
        }

        private void OnEnable()
        {
            Subscribe(_leftHitbox, true);
            Subscribe(_rightHitbox, true);
            Subscribe(_leftFootHitbox, true);
            Subscribe(_rightFootHitbox, true);
            Subscribe(_headHitbox, true);
        }

        private void OnDisable()
        {
            Subscribe(_leftHitbox, false);
            Subscribe(_rightHitbox, false);
            Subscribe(_leftFootHitbox, false);
            Subscribe(_rightFootHitbox, false);
            Subscribe(_headHitbox, false);
        }

        private void Subscribe(Hitbox hitbox, bool add)
        {
            if (hitbox == null) return;

            if (add) hitbox.Hit += OnHitboxHit;
            else hitbox.Hit -= OnHitboxHit;
        }

        // ------------------------------------------------------------------ charge

        /// <summary>Maintient une charge. À appeler chaque frame tant que la touche est tenue.</summary>
        public void HoldCharge(AttackData attack)
        {
            if (attack == null || !attack.chargeable)
            {
                _charging = null;
                return;
            }

            if (_attack != null || (_combatant != null && !_combatant.CanAct))
            {
                _charging = null;
                _chargeTime = 0f;
                return;
            }

            if (_charging != attack)
            {
                _charging = attack;
                _chargeTime = 0f;
            }

            _chargeTime += Time.deltaTime;

            if (_hands == null || attack.limb == AttackLimb.Foot) return;

            // L'armement se tient et se creuse : le poing recule, le buste se tord, et un
            // tremblement monte. La charge doit se VOIR, sinon on relache au hasard.
            float level = ChargeProgress;
            HandSide side = attack.hand == AttackHand.Lead ? HandSide.Left : HandSide.Right;

            AttackPoseKey key = attack.Sample(0, Mathf.Lerp(0.05f, attack.hitWindowStart * 0.5f, level));
            HandPose pose = AttackData.Mirror(key.handPosition, key.handEuler, side == HandSide.Left);

            float shake = level * level * 0.007f;
            pose = new HandPose(
                pose.position + new Vector3(
                    (Mathf.PerlinNoise(Time.time * 38f, 0f) - 0.5f) * shake,
                    (Mathf.PerlinNoise(0f, Time.time * 41f) - 0.5f) * shake,
                    -0.03f * level),
                pose.euler);

            _hands.SetAttackPose(side, pose, Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(level * 3f)), Mathf.Lerp(0.6f, 1f, level));

            if (_locomotion != null)
            {
                _locomotion.CombatBodyEuler = AttackData.MirrorEuler(key.bodyEuler, side == HandSide.Left) * (_bodyScale * level);
            }

            if (_cameraPunch != null)
            {
                _cameraPunch.SetDriven(
                    AttackData.MirrorOffset(key.cameraOffset, side == HandSide.Left) * level,
                    AttackData.MirrorEuler(key.cameraEuler, side == HandSide.Left) * level);
            }
        }

        public void CancelCharge()
        {
            if (_charging == null) return;

            if (_hands != null && _charging.limb != AttackLimb.Foot)
            {
                _hands.ClearAttackPose(_charging.hand == AttackHand.Lead ? HandSide.Left : HandSide.Right);
            }

            if (_locomotion != null) _locomotion.CombatBodyEuler = Vector3.zero;
            if (_cameraPunch != null) _cameraPunch.SetDriven(Vector3.zero, Vector3.zero);

            _charging = null;
            _chargeTime = 0f;
        }

        public bool ReleaseCharge()
        {
            if (_charging == null) return false;

            AttackData attack = _charging;
            float level = ChargeProgress;

            _charging = null;
            _chargeTime = 0f;

            return TryPlay(attack, level);
        }

        // ------------------------------------------------------------------ lancement

        public bool TryPlay(AttackData attack)
        {
            return TryPlay(attack, 0f);
        }

        public bool TryPlay(AttackData attack, float charge)
        {
            if (attack == null) return Refuse("aucune donnee d'attaque assignee (champ vide dans PlayerCombat ?)", true);
            if (_hands == null) return Refuse("pas de FirstPersonHands assigne sur l'executeur", true);
            if (_cooldown > 0f) return Refuse("temps de repos : " + _cooldown.ToString("0.00") + " s", false);

            bool chaining = false;

            if (_attack != null)
            {
                if (!CanChain)
                {
                    return Refuse("coup en cours a " + (Progress * 100f).ToString("0") + " %, " +
                                  "enchainable a partir de " + (_attack.comboCancelAt * 100f).ToString("0") + " %", false);
                }

                chaining = true;
            }

            bool ownAttackState = _combatant != null && _combatant.State.Current == CombatantState.Attacking;

            if (_combatant != null && !_combatant.CanAct && !(chaining && ownAttackState))
            {
                return Refuse("etat du combattant : " + _combatant.State.Current, false);
            }

            string problem = attack.Diagnose();
            if (!string.IsNullOrEmpty(problem))
            {
                Debug.LogError("[UberBagarre] L'attaque '" + attack.displayName + "' est incomplete : " + problem +
                               ". Relance 'Uber Bagarre > 4 - Regenerer les coups par defaut'.", attack);
            }

            if (_combatant != null && _combatant.Stamina != null)
            {
                if (!_combatant.Stamina.CanSpend(attack.staminaCost)) return Refuse("endurance insuffisante", false);
                _combatant.Stamina.TrySpend(attack.staminaCost);
            }

            if (chaining) EndCurrent(false);

            // Un coup lancé dans la foulée du précédent fait monter l'enchaînement : la caméra
            // et les impacts montent avec lui.
            _chain = chaining || Time.time - _lastEndTime < 0.35f ? _chain + 1 : 0;

            _attack = attack;
            _charge = attack.chargeable ? Mathf.Clamp01(charge) : 0f;
            _riposteMultiplier = _guard != null ? _guard.ConsumeRiposte() : 1f;

            _side = ResolveHand(attack);
            if (SideResolver != null) _side = SideResolver(attack, _side);
            _variantIndex = attack.PickVariant(_previousVariant);
            _previousVariant = _variantIndex;
            _elapsed = 0f;
            _hitWindowOpen = false;
            _hitLag = 0f;
            _contacted = false;
            _hasPreviousFist = false;
            _fistVelocity = Vector3.zero;

            if (_combatant != null) _combatant.State.Enter(CombatantState.Attacking, TotalDuration);

            AcquireTarget();
            BeginHandMotion();
            Lunge();

            if (_cinematic && _riposteMultiplier > 1.01f)
            {
                // Le contre : le temps se suspend une fraction de seconde, la vue se resserre.
                if (_hitStop != null) _hitStop.SlowMotion(0.42f, 0.32f);
                if (_cameraPunch != null) _cameraPunch.HoldFov(6f, 0.3f);
            }

            if (_logAttacks)
            {
                Debug.Log("[UberBagarre] " + attack.displayName + " (" + _side + ", variante " +
                          (_variantIndex + 1) + ", cible " + (_target.Valid ? _target.Combatant.DisplayName : "aucune") +
                          ", enchainement " + _chain + ")", this);
            }

            LastRefusal = string.Empty;

            Action<AttackData, HandSide> started = AttackStarted;
            if (started != null) started(attack, _side);

            return true;
        }

        private bool Refuse(string reason, bool loud)
        {
            LastRefusal = reason;
            if (loud && _logRefusals) Debug.LogError("[UberBagarre] Coup refuse : " + reason, this);
            return false;
        }

        /// <summary>
        /// Accroche la cible et mémorise où la pose écrite place le poignet à l'instant de
        /// l'impact : l'écart entre les deux est ce que le guidage devra combler.
        /// </summary>
        private void AcquireTarget()
        {
            _target = new StrikeTarget();
            _hasTargetPose = false;

            if (_attack.limb != AttackLimb.Hand || _reachAssist <= 0f || _variantIndex < 0) return;

            string variant = _attack.variants[_variantIndex].name ?? string.Empty;
            bool body = variant.Contains("corps") || variant.Contains("plexus") || variant.Contains("foie") ||
                        variant.Contains("cotes");

            Vector3 from = _hands.ArmRoot(_side);
            _target = StrikeTarget.Acquire(_combatant, from, body);

            AttackPoseKey impact = _attack.Sample(_variantIndex, _attack.ImpactTime);
            _designImpact = AttackData.Mirror(impact.handPosition, impact.handEuler, _side == HandSide.Left).position;
        }

        /// <summary>
        /// Un geste capturé pour ce coup de poing ? Il remplace alors la trajectoire écrite du
        /// poing — et l'impact qu'il vise devient celui que le guidage corrige.
        /// </summary>
        private void BeginHandMotion()
        {
            _motionActive = false;

            IHandMotion motion = _handMotionSource as IHandMotion;
            if (motion == null || _hands == null || _attack.limb != AttackLimb.Hand || _variantIndex < 0) return;

            HandPose guard = _hands.GetGuardPose(_side);
            AttackPoseKey impact = _attack.Sample(_variantIndex, _attack.ImpactTime);
            Vector3 design = AttackData.Mirror(impact.handPosition, impact.handEuler, _side == HandSide.Left).position;

            _motionActive = motion.Begin(_attack, _side, CurrentIsBodyShot, guard, design, _attack.ImpactTime);
            if (_motionActive) _designImpact = motion.ImpactPosition(guard);
        }

        /// <summary>
        /// Le pas glissé : la cible est un peu trop loin, le corps se jette dans le coup. La
        /// vitesse est calculée pour couvrir l'écart juste à l'instant de l'impact.
        /// </summary>
        private void Lunge()
        {
            IImpulseReceiver receiver = _impulseReceiver as IImpulseReceiver;
            if (receiver == null || !_target.Valid || _maxLungeSpeed <= 0f) return;

            Vector3 root = _hands.ArmRoot(_side);
            Vector3 point = _target.WorldPoint;
            // La portée réelle dépasse le bras : le buste qui tourne et l'épaule qui s'avance
            // ajoutent une douzaine de centimètres. Sans eux, l'élan emmène trop près et le
            // direct finit coude plié.
            float reach = _hands.ArmReach(_side) + 0.18f;
            float gap = Vector3.Distance(root, point) - reach;

            if (gap < 0.03f || gap > 1.2f) return;

            float timeToImpact = Mathf.Max(0.08f, _attack.ImpactTime * EffectiveDuration + Telegraph);
            float speed = Mathf.Min(_maxLungeSpeed * (_attack.isHeavy ? 1.1f : 1f), gap / timeToImpact * 1.15f);

            Vector3 direction = point - transform.position;
            direction.y = 0f;
            if (direction.sqrMagnitude < 1e-4f) return;

            receiver.ApplyImpulse(direction.normalized * speed);
        }

        public void Cancel()
        {
            EndCurrent(false);
        }

        private void EndCurrent(bool applyCooldown)
        {
            if (_attack == null) return;

            CloseHitWindow();
            ClearLimbPose();
            _motionActive = false;

            if (_locomotion != null) _locomotion.CombatBodyEuler = Vector3.zero;
            if (_cameraPunch != null)
            {
                _cameraPunch.SetDriven(Vector3.zero, Vector3.zero);
                _cameraPunch.SetStrikeMotion(Vector3.zero, 0f);
            }

            AttackData finished = _attack;
            if (applyCooldown) _cooldown = finished.cooldown;
            _attack = null;
            _target = new StrikeTarget();
            _lastEndTime = Time.time;

            Action<AttackData> ended = AttackEnded;
            if (ended != null) ended(finished);
        }

        private HandSide ResolveHand(AttackData attack)
        {
            switch (attack.hand)
            {
                case AttackHand.Lead: return HandSide.Left;
                case AttackHand.Rear: return HandSide.Right;
                default:
                    _lastHandWasLead = !_lastHandWasLead;
                    return _lastHandWasLead ? HandSide.Left : HandSide.Right;
            }
        }

        // ------------------------------------------------------------------ déroulement

        /// <summary>
        /// Temps du geste (0..1) à partir du temps écoulé. L'armement de l'IA est tenu plus
        /// longtemps ; le reste du geste garde sa vitesse — on voit venir le coup, il part
        /// quand même vite.
        /// </summary>
        private float Normalized(float elapsed)
        {
            if (_attack == null) return 0f;

            float duration = EffectiveDuration;
            float telegraph = Telegraph;
            if (telegraph <= 0f) return Mathf.Clamp01(elapsed / Mathf.Max(0.02f, duration));

            float wind = Mathf.Clamp(_attack.hitWindowStart * 0.5f, 0.05f, 0.3f);
            float windTime = wind * duration + telegraph;
            if (elapsed < windTime) return wind * (elapsed / windTime);

            return Mathf.Clamp01(wind + (elapsed - windTime) / Mathf.Max(0.02f, duration));
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_attack == null)
            {
                if (_cooldown > 0f) _cooldown -= dt;
                return;
            }

            if (_hitLag > 0f)
            {
                // Le poing reste sur la cible : on ne fait pas avancer le geste.
                _hitLag -= Time.unscaledDeltaTime;
            }
            else
            {
                // Après le contact, le poing ne continue pas sa course à travers la cible : on
                // file vers le retour.
                float impact = _attack.ImpactTime;
                bool pastContact = _contacted && Progress < impact + 0.14f;
                _elapsed += dt * (pastContact ? 1.8f : 1f);
            }

            float normalized = Normalized(_elapsed);

            ApplyPose(normalized, dt);
            UpdateHitWindow(normalized);

            if (_elapsed >= TotalDuration) EndCurrent(true);
        }

        /// <summary>Poids du guidage vers la cible : nul au départ, plein à l'impact, rendu au retour.</summary>
        private float RetargetWeight(float t)
        {
            float impact = Mathf.Max(0.05f, _attack.ImpactTime);
            if (t <= impact) return Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - 0.02f) / (impact - 0.02f)));
            return 1f - Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((t - impact) / Mathf.Max(0.05f, 1f - impact)));
        }

        private void ApplyPose(float normalized, float dt)
        {
            bool mirrored = _side == HandSide.Left;
            float weight = _attack.EvaluateWeight(normalized);

            AttackPoseKey key;
            HandPose pose;

            if (_variantIndex >= 0)
            {
                key = _attack.Sample(_variantIndex, normalized);
                pose = AttackData.Mirror(key.handPosition, key.handEuler, mirrored);
            }
            else
            {
                key = new AttackPoseKey();
                key.grip = 1f;

                float extension = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);

                if (_attack.limb == AttackLimb.Foot)
                {
                    pose = new HandPose(
                        new Vector3(mirrored ? -0.16f : 0.16f, 0.12f + 0.42f * extension, -0.17f + 0.70f * extension),
                        new Vector3(-22f * extension, 0f, 0f));
                }
                else
                {
                    HandPose guard = _hands.GetGuardPose(_side);
                    pose = new HandPose(guard.position + Vector3.forward * (0.30f * extension), guard.euler);
                }
            }

            // Geste capturé : la trajectoire et l'orientation du poing viennent de la capture,
            // à partir de la garde du moment ; tout le reste (corps, caméra, fermeture) reste écrit.
            HandPose capturedOff = new HandPose();
            if (_motionActive)
            {
                HandSide free = _side == HandSide.Left ? HandSide.Right : HandSide.Left;
                ((IHandMotion)_handMotionSource).Sample(normalized, _hands.GetGuardPose(_side),
                    _hands.GetGuardPose(free), out pose, out capturedOff);
            }

            if (_attack.limb == AttackLimb.Hand) pose = Retarget(pose, normalized, dt);

            // Gel du contact : le poing tremble à peine contre la cible.
            if (_hitLag > 0f)
            {
                float t = Time.unscaledTime * 70f;
                pose = new HandPose(pose.position + new Vector3(Mathf.Sin(t), Mathf.Cos(t * 1.3f), 0f) * 0.0025f, pose.euler);
            }

            if (_attack.limb == AttackLimb.Foot) ApplyFootPose(pose, weight);
            else if (_hands != null) _hands.SetAttackPose(_side, pose, _motionActive ? 1f : weight, key.grip);

            // Le geste capturé part de la garde et y revient : il se joue à plein poids.
            if (_motionActive) _hands.SetAttackPose(_side == HandSide.Left ? HandSide.Right : HandSide.Left, capturedOff, 1f, -1f);
            else ApplyOffHandPose(key, mirrored, weight);

            float intensity = 1f + Mathf.Min(_chain, 4) * 0.07f + _charge * 0.35f;

            if (_locomotion != null)
            {
                _locomotion.CombatBodyEuler = AttackData.MirrorEuler(key.bodyEuler, mirrored) * (weight * _bodyScale * intensity);
            }

            if (_cameraPunch != null)
            {
                _cameraPunch.SetDriven(
                    AttackData.MirrorOffset(key.cameraOffset, mirrored) * (weight * intensity),
                    AttackData.MirrorEuler(key.cameraEuler, mirrored) * (weight * intensity));

                TrackFist(dt, weight);
            }
        }

        /// <summary>Guide le poignet vers la cible accrochée au lancement.</summary>
        private HandPose Retarget(HandPose pose, float normalized, float dt)
        {
            if (!_target.Valid || _target.Anchor == null || !_target.Combatant.IsAlive) return pose;

            Hitbox hitbox = ActiveHitbox();
            Transform wrist = _hands.ArmEnd(_side);
            if (hitbox == null || wrist == null) return pose;

            // Le point de frappe, ce sont les jointures : le poignet vise la cible moins
            // l'écart poignet-jointures du moment.
            Vector3 knuckleOffset = hitbox.Origin.position - wrist.position;
            Vector3 wristTarget = _target.WorldPoint - knuckleOffset;
            Vector3 local = _hands.PoseSpace.InverseTransformPoint(wristTarget);

            if (!_hasTargetPose)
            {
                _targetPose = local;
                _hasTargetPose = true;
            }
            else if (!_contacted)
            {
                _targetPose = Vector3.Lerp(_targetPose, local, (1f - Mathf.Exp(-14f * dt)) * _tracking);
            }

            Vector3 delta = Vector3.ClampMagnitude(_targetPose - _designImpact, _maxRetarget);
            return new HandPose(pose.position + delta * (RetargetWeight(normalized) * _reachAssist), pose.euler);
        }

        /// <summary>La caméra suit la vitesse du poing, exprimée dans le repère de la vue.</summary>
        private void TrackFist(float dt, float weight)
        {
            Hitbox hitbox = ActiveHitbox();
            if (hitbox == null || dt <= 0f) return;

            Vector3 local = _hands.PoseSpace.InverseTransformPoint(hitbox.Origin.position);

            if (_hasPreviousFist && _hitLag <= 0f)
            {
                Vector3 velocity = (local - _previousFist) / dt;
                _fistVelocity = Vector3.Lerp(_fistVelocity, velocity, 1f - Mathf.Exp(-18f * dt));
            }

            _previousFist = local;
            _hasPreviousFist = true;

            // Un coup de pied ne tourne pas la vue : c'est le corps qui porte, pas le regard.
            float follow = _attack.limb == AttackLimb.Foot ? 0.25f : 1f;
            _cameraPunch.SetStrikeMotion(Vector3.ClampMagnitude(_fistVelocity, 9f) * follow, weight);
        }

        private void ApplyFootPose(HandPose pose, float weight)
        {
            if (_locomotion == null) return;

            Transform space = FootPoseSpace;
            Vector3 world = space.TransformPoint(pose.position);
            Quaternion rotation = space.rotation * pose.Rotation;

            _locomotion.SetFootOverride(_side == HandSide.Left, world, rotation, weight);
        }

        private Transform FootPoseSpace
        {
            get { return _footPoseSpace != null ? _footPoseSpace : transform; }
        }

        private void ApplyOffHandPose(AttackPoseKey key, bool mirrored, float weight)
        {
            if (_hands == null) return;

            HandSide other = _side == HandSide.Left ? HandSide.Right : HandSide.Left;

            if (key.offHandWeight <= 0.001f)
            {
                _hands.ClearAttackPose(other);
                return;
            }

            HandPose pose = AttackData.Mirror(key.offHandPosition, key.offHandEuler, mirrored);
            _hands.SetAttackPose(other, pose, weight * key.offHandWeight, -1f);
        }

        private void ClearLimbPose()
        {
            if (_hands != null)
            {
                _hands.ClearAttackPose(HandSide.Left);
                _hands.ClearAttackPose(HandSide.Right);
            }
        }

        private void UpdateHitWindow(float normalized)
        {
            bool shouldBeOpen = normalized >= _attack.hitWindowStart && normalized <= _attack.hitWindowEnd && !_contacted;

            if (shouldBeOpen && !_hitWindowOpen) OpenHitWindow();
            else if (!shouldBeOpen && _hitWindowOpen) CloseHitWindow();
        }

        private void OpenHitWindow()
        {
            Hitbox hitbox = ActiveHitbox();
            if (hitbox == null) return;

            float chargeDamage = Mathf.Lerp(1f, _attack.chargeDamageMultiplier, _charge);
            float chargeImpact = Mathf.Lerp(1f, _attack.chargeImpactMultiplier, _charge);

            DamageInfo template = new DamageInfo();
            template.Amount = (_combatant != null
                ? DamageCalculator.ComputeOutgoing(_attack, _combatant.Stats)
                : _attack.damage) * chargeDamage * _riposteMultiplier;
            template.ImpactForce = _attack.impactForce * chargeImpact;
            template.Direction = transform.forward;
            template.Attack = _attack;
            template.ChargeLevel = _charge;
            template.IsRiposte = _riposteMultiplier > 1.01f;
            template.BonusKnockdownChance = _attack.chargeKnockdownBonus * _charge;

            // La zone : celle du coup guidé si on en a une, sinon celle sous le réticule.
            Hurtbox aimed = _target.Valid ? _target.Zone : AimResolver.Resolve(_combatant);
            hitbox.Open(template, _attack.hitRadius, aimed);
            _hitWindowOpen = true;
        }

        private void CloseHitWindow()
        {
            Hitbox hitbox = ActiveHitbox();
            if (hitbox != null) hitbox.Close();
            _hitWindowOpen = false;
        }

        private Hitbox ActiveHitbox()
        {
            bool isLeft = _side == HandSide.Left;

            if (_attack != null && _attack.limb == AttackLimb.Foot)
            {
                return isLeft ? _leftFootHitbox : _rightFootHitbox;
            }

            if (_attack != null && _attack.limb == AttackLimb.Head && _headHitbox != null)
            {
                return _headHitbox;
            }

            return isLeft ? _leftHitbox : _rightHitbox;
        }

        // ------------------------------------------------------------------ contact

        private void OnHitboxHit(Hurtbox hurtbox, Vector3 point)
        {
            if (_attack == null) return;

            bool riposte = _riposteMultiplier > 1.01f;
            float strength = (_attack.isHeavy ? 1f : 0.62f) * (1f + _charge * 0.8f) * (riposte ? 1.3f : 1f) *
                             (1f + Mathf.Min(_chain, 4) * 0.08f);

            // Le poing reste collé à la cible : plus le coup est lourd, plus il « s'écrase ».
            _hitLag = (_attack.isHeavy ? 0.085f : 0.05f) * (1f + _charge * 0.6f) * (riposte ? 1.4f : 1f);
            _contacted = true;

            // L'état d'attaque doit couvrir le gel, sinon il expire avant la fin du geste.
            if (_combatant != null && _combatant.State.Current == CombatantState.Attacking)
            {
                float remaining = Mathf.Max(0.02f, TotalDuration - _elapsed) + _hitLag;
                _combatant.State.Enter(CombatantState.Attacking, remaining);
            }

            if (_hitStop != null) _hitStop.Play(_attack.hitStopDuration * (1f + _charge));

            bool killed = hurtbox != null && hurtbox.Health != null && !hurtbox.Health.IsAlive;

            if (_cameraPunch != null)
            {
                // Le contact pousse la vue dans le sens du geste : un crochet l'enroule, un
                // direct la fait plonger puis reculer. Le champ se resserre d'un coup.
                Vector3 swing = _fistVelocity.sqrMagnitude > 0.01f ? _fistVelocity.normalized : Vector3.forward;
                _cameraPunch.AddImpulse(
                    new Vector3(0f, 0f, -0.012f * strength),
                    new Vector3(
                        -2.2f * strength - swing.y * 2f * strength,
                        swing.x * 3.2f * strength,
                        -swing.x * 2.6f * strength + UnityEngine.Random.Range(-0.6f, 0.6f) * strength));
                _cameraPunch.KickFov(2.5f + 2.5f * strength);
            }

            if (_cinematic && killed)
            {
                // Le coup qui met K.O. : le monde ralentit, la vue se serre sur le visage.
                if (_hitStop != null) _hitStop.SlowMotion(0.16f, 1.0f);
                if (_cameraPunch != null) _cameraPunch.HoldFov(10f, 0.8f);
            }
            else if (_cinematic && riposte)
            {
                if (_hitStop != null) _hitStop.SlowMotion(0.3f, 0.45f);
                if (_cameraPunch != null) _cameraPunch.HoldFov(7f, 0.35f);
            }

            Action<AttackData, Hurtbox, Vector3> landed = HitLanded;
            if (landed != null) landed(_attack, hurtbox, point);
        }
    }
}
