using System.Collections.Generic;
using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Enemy
{
    /// <summary>
    /// IA de combat rapproché.
    ///
    /// Elle tient en quatre décisions : s'approcher, reculer, frapper, esquiver. C'est
    /// volontairement peu — l'objectif de cette phase est un adversaire dont on peut régler
    /// PRÉCISÉMENT le comportement pour tester le système, pas une IA impressionnante.
    ///
    /// D'où le mode séquence scriptée : « après 2 s, jab ; après 1,5 s, crochet ». C'est le seul
    /// moyen de vérifier au timing près qu'une esquive ou une fenêtre d'impact fonctionne.
    /// Une IA aléatoire rend ce genre de test impossible.
    /// </summary>
    public class EnemyBrain : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _self;
        [SerializeField] private EnemyMotor _motor;
        [SerializeField] private AttackExecutor _executor;
        [SerializeField] private DodgeSystem _dodge;

        [SerializeField]
        [Tooltip("Optionnel. Sans lui, l'ennemi encaisse tout sans jamais se couvrir.")]
        private GuardSystem _guard;

        [SerializeField]
        [Tooltip("Cible. Laisse vide : l'ennemi trouvera le combattant hostile le plus proche.")]
        private Combatant _target;

        [Header("Activation")]
        [SerializeField]
        [Tooltip("Decoche pour un ennemi totalement passif : pratique pour regler ses propres coups.")]
        private bool _active = true;

        [SerializeField, Min(0f)] private float _detectionRange = 16f;

        [Header("Distances (metres)")]
        [SerializeField, Min(0.2f)]
        [Tooltip("Distance a laquelle l'ennemi cherche a se tenir.")]
        private float _preferredRange = 0.85f;

        [SerializeField, Min(0.1f)] private float _rangeTolerance = 0.18f;

        [SerializeField, Min(0.2f)]
        [Tooltip("Distance maximale a laquelle il tente un coup.")]
        private float _attackRange = 1.10f;

        [Header("Rythme")]
        [SerializeField, Min(0f)] private float _attackDelayMin = 0.30f;
        [SerializeField, Min(0f)] private float _attackDelayMax = 0.85f;

        [SerializeField, Min(0f)]
        [Tooltip("Temps d'immobilite apres un coup, avant de reprendre l'initiative. Resserre en " +
                 "meme temps que les coups du joueur : un adversaire qui attend une seconde entre " +
                 "deux coups transforme le combat en tour par tour.")]
        private float _postAttackPause = 0.18f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part du temps passee a tourner autour de la cible plutot qu'a rester face a elle.")]
        private float _strafeTendency = 0.4f;

        [Header("Repertoire de coups")]
        [SerializeField] private List<EnemyAttackOption> _attacks = new List<EnemyAttackOption>();

        [Header("Esquive")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Probabilite d'esquiver un coup detecte. 0 = jamais (test de hitbox), 1 = toujours.")]
        private float _dodgeChance = 0.25f;

        [SerializeField, Min(0f)]
        [Tooltip("Delai de reaction avant d'esquiver. A 0 l'ennemi est surhumain.")]
        private float _dodgeReactionTime = 0.12f;

        [SerializeField, Min(0f)] private float _dodgeMaxDistance = 2.0f;

        [Header("Garde")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Probabilite de lever la garde sur un coup detecte, quand il n'esquive pas. " +
                 "C'est ce qui oblige a varier : matraquer la meme touche finit par se faire bloquer.")]
        private float _guardChance = 0.4f;

        [SerializeField, Min(0f)]
        [Tooltip("Delai de reaction avant de lever la garde.")]
        private float _guardReactionTime = 0.1f;

        [SerializeField, Min(0f)]
        [Tooltip("Duree pendant laquelle il tient sa garde apres l'avoir levee.")]
        private float _guardHoldDuration = 0.75f;

        [Header("Sequence de test")]
        [SerializeField]
        [Tooltip("Ignore l'IA et joue la liste ci-dessous en boucle. Pour tester au timing pres.")]
        private bool _useScriptedSequence;

        [SerializeField] private List<ScriptedAttackStep> _scriptedSequence = new List<ScriptedAttackStep>();

        [Header("Debug")]
        [SerializeField] private bool _drawRanges = true;
        [SerializeField] private bool _logDecisions;

        private float _nextAttackTime;
        private float _pendingDodgeTime = -1f;
        private Vector3 _pendingDodgeDirection;
        private int _scriptedIndex;
        private float _scriptedNextTime;
        private float _strafeSign = 1f;
        private float _nextStrafeFlip;
        private AttackExecutor _targetExecutor;
        private float _guardFrom = -1f;
        private float _guardUntil = -1f;

        public Combatant Target { get { return _target; } }
        public float DistanceToTarget { get { return _self != null ? _self.DistanceTo(_target) : 0f; } }

        private void Awake()
        {
            if (_self == null) _self = GetComponent<Combatant>();
            if (_motor == null) _motor = GetComponent<EnemyMotor>();
        }

        private void Start()
        {
            EnsureAttackRepertoire();
            _nextAttackTime = Time.time + Random.Range(_attackDelayMin, _attackDelayMax);
            _scriptedNextTime = Time.time + (_scriptedSequence.Count > 0 ? _scriptedSequence[0].delay : 1f);
        }

        private void OnDisable()
        {
            UnsubscribeFromTarget();
        }

        /// <summary>
        /// Garantit que l'ennemi a de quoi frapper.
        ///
        /// Un répertoire vide donne un ennemi qui s'approche, se place, et ne fait plus rien —
        /// comportement impossible à distinguer d'une IA cassée. On reconstitue donc un
        /// répertoire par défaut depuis Resources, en le signalant.
        /// </summary>
        private void EnsureAttackRepertoire()
        {
            for (int i = 0; i < _attacks.Count; i++)
            {
                if (_attacks[i] != null && _attacks[i].attack != null) return;
            }

            _attacks.Clear();
            AddDefaultOption(AttackData.StraightAsset, 3f, 1.15f, 0.8f);
            AddDefaultOption(AttackData.HookAsset, 1.4f, 1.05f, 2.2f);
            AddDefaultOption(AttackData.UppercutAsset, 0.8f, 0.95f, 3.4f);
            AddDefaultOption(AttackData.KickAsset, 0.9f, 1.35f, 4.5f);
            AddDefaultOption(AttackData.LowKickAsset, 1.1f, 1.25f, 3.8f);

            Debug.LogWarning("[UberBagarre] " + name + " n'avait aucun coup configure : repertoire par defaut " +
                             "charge depuis Resources (" + _attacks.Count + " coups). Regenere la scene " +
                             "(Uber Bagarre > 2) pour un cablage propre.", this);
        }

        private void AddDefaultOption(string assetName, float weight, float maxDistance, float cooldown)
        {
            AttackData attack = AttackData.LoadFromResources(assetName);
            if (attack == null) return;

            EnemyAttackOption option = new EnemyAttackOption();
            option.attack = attack;
            option.weight = weight;
            option.minDistance = 0f;
            option.maxDistance = maxDistance;
            option.cooldown = cooldown;

            _attacks.Add(option);
        }

        /// <summary>
        /// Reporte la vitesse de déplacement des statistiques sur le moteur.
        ///
        /// <see cref="StatType.MoveSpeed"/> existait depuis la phase 6 et rien ne la lisait — même
        /// cas que AttackSpeed. C'est elle qui permet à un archétype rapide d'être réellement
        /// rapide, au lieu de l'être seulement sur le papier.
        /// </summary>
        private void ApplyMoveSpeed()
        {
            if (_motor == null || _self == null || _self.Stats == null) return;

            _motor.SpeedMultiplier = Mathf.Max(0.1f, _self.Stats.Get(StatType.MoveSpeed));
        }

        /// <summary>
        /// Tient TOUS les adversaires en attente : ils se tiennent là, garde baissée, sans
        /// avancer ni frapper.
        ///
        /// C'est la commande reçue sur le téléphone qui lance un combat, pas le chargement de
        /// la scène : dans le bac à sable, l'adversaire attend qu'on ait accepté la course.
        /// Statique parce que la règle vaut pour tout le monde à la fois — y compris les
        /// adversaires qu'une vague fera apparaître pendant l'attente.
        /// </summary>
        public static bool HoldAll { get; set; }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            // Sans rechargement de domaine entre deux Play, un statique survit : une attente
            // interrompue figerait les adversaires de la partie suivante.
            HoldAll = false;
        }

        private void Update()
        {
            ApplyMoveSpeed();

            if (HoldAll)
            {
                if (_guard != null) _guard.SetGuard(false);
                return;
            }

            if (_self == null || !_self.IsAlive)
            {
                UnsubscribeFromTarget();
                if (_guard != null) _guard.SetGuard(false);
                return;
            }

            UpdateGuard();
            AcquireTarget();
            if (_target == null || !_target.IsAlive) return;

            float distance = _self.DistanceTo(_target);
            if (distance > _detectionRange) return;

            _motor.FaceTowards(_target.transform.position);

            ResolvePendingDodge();

            if (!_self.CanAct) return;

            if (_useScriptedSequence) UpdateScripted(distance);
            else UpdateAutonomous(distance);
        }

        // ------------------------------------------------------------------ cible

        private void AcquireTarget()
        {
            if (_target != null && _target.IsAlive)
            {
                if (_targetExecutor == null) SubscribeToTarget();
                return;
            }

            UnsubscribeFromTarget();
            _target = _self.FindNearestOpponent(_detectionRange);
            SubscribeToTarget();
        }

        /// <summary>
        /// L'ennemi « voit » les coups adverses en écoutant l'exécuteur de sa cible.
        /// C'est honnête : il réagit au démarrage réel du coup, avec un temps de réaction
        /// réglable — pas à une intention cachée.
        /// </summary>
        private void SubscribeToTarget()
        {
            if (_target == null) return;

            _targetExecutor = _target.GetComponentInChildren<AttackExecutor>();
            if (_targetExecutor != null) _targetExecutor.AttackStarted += OnTargetAttackStarted;
        }

        private void UnsubscribeFromTarget()
        {
            if (_targetExecutor == null) return;

            _targetExecutor.AttackStarted -= OnTargetAttackStarted;
            _targetExecutor = null;
        }

        private void OnTargetAttackStarted(AttackData attack, View.HandSide side)
        {
            if (!_active) return;
            if (_self.DistanceTo(_target) > _dodgeMaxDistance) return;

            // L'esquive passe avant la garde : elle annule le coup au lieu de l'absorber, donc
            // c'est toujours la meilleure reponse quand elle est disponible.
            if (_dodge != null && _dodgeChance > 0f && Random.value <= _dodgeChance)
            {
                _pendingDodgeTime = Time.time + _dodgeReactionTime;

                // On s'ecarte lateralement : reculer en ligne droite ne sort pas d'un direct.
                float side01 = Random.value < 0.5f ? -1f : 1f;
                _pendingDodgeDirection = transform.right * side01;
                return;
            }

            if (_guard == null || _guardChance <= 0f) return;
            if (Random.value > _guardChance) return;

            _guardFrom = Time.time + _guardReactionTime;
            _guardUntil = _guardFrom + _guardHoldDuration;
        }

        /// <summary>
        /// Garde de l'ennemi : levée en réaction, tenue un court instant, puis relâchée.
        ///
        /// Elle n'est jamais permanente, et c'est le cœur de l'intérêt : un adversaire
        /// éternellement en garde rend le combat injouable, un adversaire qui ne garde jamais
        /// rend inutile de varier ses coups.
        /// </summary>
        private void UpdateGuard()
        {
            if (_guard == null) return;

            // CanAct couvre d'un coup tous les cas ou se couvrir n'a pas de sens : en train de
            // frapper, d'esquiver, d'encaisser, ou au sol. Un ennemi couche qui bloque encore
            // les coups annulerait tout l'interet de l'avoir mis par terre.
            bool canCover = _self != null && _self.CanAct;
            float now = Time.time;

            _guard.SetGuard(canCover && now >= _guardFrom && now < _guardUntil);
        }

        private void ResolvePendingDodge()
        {
            if (_pendingDodgeTime < 0f || Time.time < _pendingDodgeTime) return;

            _pendingDodgeTime = -1f;
            if (_dodge != null) _dodge.TryDodge(_pendingDodgeDirection);
        }

        // ------------------------------------------------------------------ comportement autonome

        private void UpdateAutonomous(float distance)
        {
            if (!_active) return;

            UpdateMovement(distance);

            if (Time.time < _nextAttackTime) return;
            if (distance > _attackRange) return;
            if (_executor == null || !_executor.IsReady) return;

            EnemyAttackOption option = PickAttack(distance);
            if (option == null)
            {
                _nextAttackTime = Time.time + 0.25f;
                return;
            }

            if (!_executor.TryPlay(option.attack))
            {
                _nextAttackTime = Time.time + 0.25f;
                return;
            }

            option.NextAvailableTime = Time.time + option.cooldown;
            _nextAttackTime = Time.time + option.attack.duration + _postAttackPause +
                              Random.Range(_attackDelayMin, _attackDelayMax);

            if (_logDecisions) Debug.Log("[UberBagarre] " + name + " attaque : " + option.attack.displayName, this);
        }

        private void UpdateMovement(float distance)
        {
            if (_executor != null && _executor.IsAttacking) return;

            Vector3 toTarget = _target.transform.position - transform.position;
            toTarget.y = 0f;
            Vector3 forward = toTarget.normalized;

            if (Time.time >= _nextStrafeFlip)
            {
                _strafeSign = Random.value < 0.5f ? -1f : 1f;
                _nextStrafeFlip = Time.time + Random.Range(1.2f, 3f);
            }

            if (distance > _preferredRange + _rangeTolerance)
            {
                _motor.SetMoveIntent(forward, 1f);
            }
            else if (distance < _preferredRange - _rangeTolerance)
            {
                _motor.SetMoveIntent(-forward, 0.8f);
            }
            else if (_strafeTendency > 0f)
            {
                Vector3 strafe = Vector3.Cross(Vector3.up, forward) * _strafeSign;
                _motor.SetMoveIntent(strafe, _strafeTendency);
            }
        }

        private EnemyAttackOption PickAttack(float distance)
        {
            float total = 0f;
            float now = Time.time;

            for (int i = 0; i < _attacks.Count; i++)
            {
                if (_attacks[i].IsUsable(distance, now)) total += _attacks[i].weight;
            }

            if (total <= 0f) return null;

            float roll = Random.value * total;

            for (int i = 0; i < _attacks.Count; i++)
            {
                EnemyAttackOption option = _attacks[i];
                if (!option.IsUsable(distance, now)) continue;

                roll -= option.weight;
                if (roll <= 0f) return option;
            }

            return null;
        }

        // ------------------------------------------------------------------ séquence scriptée

        private void UpdateScripted(float distance)
        {
            if (_scriptedSequence.Count == 0) return;

            ScriptedAttackStep step = _scriptedSequence[_scriptedIndex % _scriptedSequence.Count];

            if (!step.holdPosition) UpdateMovement(distance);

            if (Time.time < _scriptedNextTime) return;

            if (step.attack != null && _executor != null && _executor.IsReady)
            {
                _executor.TryPlay(step.attack);
                if (_logDecisions) Debug.Log("[UberBagarre] Sequence : " + step.attack.displayName, this);
            }

            _scriptedIndex++;
            ScriptedAttackStep next = _scriptedSequence[_scriptedIndex % _scriptedSequence.Count];
            _scriptedNextTime = Time.time + next.delay;
        }

        private void OnDrawGizmosSelected()
        {
            if (!_drawRanges) return;

            Gizmos.color = new Color(0.2f, 0.9f, 0.4f, 0.8f);
            DrawCircle(_preferredRange);

            Gizmos.color = new Color(1f, 0.35f, 0.2f, 0.8f);
            DrawCircle(_attackRange);

            Gizmos.color = new Color(0.4f, 0.6f, 1f, 0.35f);
            DrawCircle(_dodgeMaxDistance);
        }

        private void DrawCircle(float radius)
        {
            const int segments = 32;
            Vector3 previous = transform.position + Vector3.right * radius;

            for (int i = 1; i <= segments; i++)
            {
                float angle = Mathf.PI * 2f * i / segments;
                Vector3 point = transform.position + new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * radius;
                Gizmos.DrawLine(previous, point);
                previous = point;
            }
        }
    }
}
