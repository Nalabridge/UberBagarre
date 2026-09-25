using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Traduit les entrées du joueur en coups.
    ///
    /// C'est la seule pièce qui connaît à la fois la souris et le combat. L'exécuteur, lui,
    /// ne sait pas d'où vient l'ordre — d'où la possibilité de brancher une IA à sa place.
    ///
    /// Schéma par défaut :
    ///   Clic gauche   → direct (alterne gauche / droite)
    ///   Clic droit    → crochet
    ///   Clic molette  → uppercut
    ///   F             → coup de pied de face
    ///   V             → coup de pied bas (celui qui fait tomber)
    ///   Ctrl gauche   → garde ; les premières fractions de seconde sont une PARADE
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private AttackExecutor _executor;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private DodgeSystem _dodge;
        [SerializeField] private Combatant _combatant;

        [SerializeField]
        [Tooltip("Optionnel. Sans lui, la touche de garde ne fait que lever les poings a l'ecran.")]
        private GuardSystem _guard;

        [SerializeField]
        [Tooltip("Optionnel. Sert uniquement a couper le deplacement pendant qu'on est au sol.")]
        private KnockdownSystem _knockdown;

        [Header("Coups")]
        [SerializeField] private AttackData _straight;
        [SerializeField] private AttackData _hook;
        [SerializeField] private AttackData _uppercut;
        [SerializeField] private AttackData _kick;
        [SerializeField] private AttackData _lowKick;

        [SerializeField]
        [Tooltip("Coup de tete (G). Colle a l'adversaire uniquement.")]
        private AttackData _headbutt;

        [SerializeField]
        [Tooltip("Bousculade a deux mains (X). Peu de degats, gros recul.")]
        private AttackData _shove;

        [SerializeField]
        [Tooltip("Le coup de tete est-il disponible ? Dans l'histoire, c'est une CAPACITE qu'on " +
                 "debloque en montant de niveau ; dans le bac a sable, il l'est toujours.")]
        private bool _headbuttUnlocked = true;

        [Header("Coups contextuels")]
        [SerializeField]
        [Tooltip("Remplace le direct quand on sprinte.")]
        private AttackData _shoulderCharge;

        [SerializeField]
        [Tooltip("Remplace le direct quand on est en l'air.")]
        private AttackData _dive;

        [SerializeField]
        [Tooltip("Remplace le coup de pied bas pendant une glissade.")]
        private AttackData _sweep;

        [SerializeField]
        [Tooltip("Remplace le coup de pied quand la cible est au sol.")]
        private AttackData _stomp;

        [SerializeField]
        [Tooltip("Optionnel. Sert a savoir si la cible visee est au sol, pour le coup de grace.")]
        private Combatant _target;

        [Header("Reactivite")]
        [SerializeField, Min(0f)]
        [Tooltip("Duree pendant laquelle une touche d'attaque reste MEMORISEE si le coup ne peut " +
                 "pas encore partir. C'est le reglage le plus important du ressenti : sans tampon, " +
                 "toute touche pressee pendant la partie non annulable d'un coup est jetee en " +
                 "silence. Le joueur clique quatre fois, deux coups sortent, et le jeu passe pour " +
                 "mou alors qu'il a simplement ignore la moitie des ordres.")]
        private float _inputBuffer = 0.26f;

        [SerializeField]
        [Tooltip("Maintenir la touche enchaine le coup. Desactive : un coup = un appui, sinon il " +
                 "suffit de garder le clic enfonce pour marteler.")]
        private bool _repeatWhileHeld;

        [Header("Cadence")]
        [SerializeField, Min(0f)]
        [Tooltip("Intervalle minimal entre deux coups, en secondes, quel que soit le coup.")]
        private float _minAttackInterval = 0.34f;

        [SerializeField, Min(0f)]
        [Tooltip("Temps de recuperation ajoute apres la duree de chaque coup. Un coup lourd " +
                 "immobilise donc plus longtemps qu'un direct.")]
        private float _attackRecovery = 0.08f;

        [Header("Effet sur le deplacement")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Vitesse conservee pendant un coup. Volontairement haut : a 0,42 un joueur qui " +
                 "enchaine etait immobilise en permanence, ce qui se ressent comme de la lourdeur " +
                 "bien plus que comme du poids.")]
        private float _attackSpeedMultiplier = 0.66f;

        [SerializeField, Min(0.5f)] private float _speedRecovery = 10f;

        [Header("Cout du sprint")]
        [SerializeField, Min(0f)]
        [Tooltip("Endurance consommee par seconde de course. Courir doit se payer, sinon la " +
                 "stamina ne limite que le combat et la course devient gratuite.")]
        private float _sprintStaminaPerSecond = 8f;

        [Header("Cout de la glissade")]
        [SerializeField, Min(0f)]
        [Tooltip("Endurance prelevee a chaque DEPART de glissade. Facturer a la seconde ne " +
                 "coutait presque rien quand on enchainait les appuis : la glissade restait " +
                 "spammable alors que la barre baissait a peine.")]
        private float _slideStaminaCost = 18f;

        private float _currentSpeedMultiplier = 1f;
        private AttackData _buffered;
        private float _bufferedUntil;
        private float _nextAttackAllowed;

        /// <summary>
        /// Nombre de coups dont l'asset date d'une version antérieure du code.
        ///
        /// Ce compteur existe parce que le silence sur ce point m'a déjà coûté deux allers-retours
        /// complets. Le générateur ne réécrit pas un asset réglé à la main — bonne règle — mais
        /// tant que la scène n'a pas été régénérée, tout travail sur les timings est invisible.
        /// Vu de l'extérieur, « mon asset est périmé » et « il ne l'a pas fait » sont
        /// indiscernables. L'interface le dit donc franchement.
        /// </summary>
        public int OutdatedAttacks { get; private set; }

        /// <summary>Vrai quand une touche d'attaque attend son tour. Affiché par l'overlay.</summary>
        public bool HasBufferedInput { get { return _buffered != null; } }

        /// <summary>Durée de mémorisation d'une touche d'attaque. Réglable en jeu.</summary>
        public float InputBuffer
        {
            get { return _inputBuffer; }
            set { _inputBuffer = Mathf.Max(0f, value); }
        }

        public bool RepeatWhileHeld
        {
            get { return _repeatWhileHeld; }
            set { _repeatWhileHeld = value; }
        }

        private void Awake()
        {
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
            if (_guard == null) _guard = GetComponentInParent<GuardSystem>();
        }

        private void OnEnable()
        {
            if (_motor != null) _motor.SlideStarted += OnSlideStarted;
        }

        private void OnDisable()
        {
            if (_motor != null) _motor.SlideStarted -= OnSlideStarted;
        }

        /// <summary>
        /// Contrôle de cohérence au démarrage.
        ///
        /// Un coup qui ne part pas produit exactement le même symptôme qu'une touche non lue ou
        /// qu'une donnée manquante : il ne se passe rien. Cette ligne dans la console dit
        /// immédiatement lequel des trois cas on a.
        /// </summary>
        private void Start()
        {
            _straight = ResolveAttack(_straight, AttackData.StraightAsset, "Direct");
            _hook = ResolveAttack(_hook, AttackData.HookAsset, "Crochet");
            _uppercut = ResolveAttack(_uppercut, AttackData.UppercutAsset, "Uppercut");
            _kick = ResolveAttack(_kick, AttackData.KickAsset, "Coup de pied");
            _lowKick = ResolveAttack(_lowKick, AttackData.LowKickAsset, "Coup de pied bas");
            _shoulderCharge = ResolveAttack(_shoulderCharge, AttackData.ChargeAsset, "Charge d'epaule");
            _dive = ResolveAttack(_dive, AttackData.DiveAsset, "Coup plongeant");
            _sweep = ResolveAttack(_sweep, AttackData.SweepAsset, "Balayage");
            _stomp = ResolveAttack(_stomp, AttackData.StompAsset, "Coup de grace");
            _headbutt = ResolveAttack(_headbutt, AttackData.HeadbuttAsset, "Coup de tete");
            _shove = ResolveAttack(_shove, AttackData.ShoveAsset, "Bousculade");

            if (_executor == null) Debug.LogError("[UberBagarre] PlayerCombat : aucun AttackExecutor assigne.", this);
            if (_input == null) Debug.LogError("[UberBagarre] PlayerCombat : aucun PlayerInputReader assigne.", this);

            CountOutdatedAttacks();
        }

        /// <summary>
        /// Récupère un coup, et va le chercher dans Resources si la référence de scène est vide.
        ///
        /// Une référence manquante rendait tout le combat inerte sans rien casser d'autre :
        /// le clic était bien lu, l'exécuteur bien appelé, et il refusait en silence. Le jeu
        /// se répare donc lui-même, et dit clairement qu'il a dû le faire.
        /// </summary>
        private AttackData ResolveAttack(AttackData assigned, string resourceName, string label)
        {
            AttackData attack = assigned;

            if (attack == null)
            {
                attack = AttackData.LoadFromResources(resourceName);

                if (attack != null)
                {
                    Debug.LogWarning("[UberBagarre] Le coup '" + label + "' n'etait pas assigne dans la scene : " +
                                     "recupere depuis Resources. Regenere la scene (Uber Bagarre > 2) pour " +
                                     "retablir un cablage propre.", this);
                }
                else
                {
                    Debug.LogError("[UberBagarre] Le coup '" + label + "' est introuvable, ni dans la scene ni " +
                                   "dans Resources/" + AttackData.ResourceFolder + ". Lance " +
                                   "'Uber Bagarre > 4 - Regenerer les coups par defaut'.", this);
                    return null;
                }
            }

            string problem = attack.Diagnose();

            if (string.IsNullOrEmpty(problem))
            {
                Debug.Log("[UberBagarre] Coup pret : " + label + " (" + attack.displayName + ", " +
                          attack.duration.ToString("0.00") + " s, " + attack.damage.ToString("0") + " degats)", this);
            }
            else
            {
                Debug.LogError("[UberBagarre] Coup '" + label + "' incomplet : " + problem +
                               ". Relance 'Uber Bagarre > 4 - Regenerer les coups par defaut'.", attack);
            }

            return attack;
        }

        /// <summary>Débloqué par la progression dans l'histoire.</summary>
        public bool HeadbuttUnlocked
        {
            get { return _headbuttUnlocked; }
            set { _headbuttUnlocked = value; }
        }

        private void CountOutdatedAttacks()
        {
            AttackData[] attacks =
            {
                _straight, _hook, _uppercut, _kick, _lowKick,
                _shoulderCharge, _dive, _sweep, _stomp, _headbutt, _shove
            };
            OutdatedAttacks = 0;

            for (int i = 0; i < attacks.Length; i++)
            {
                if (attacks[i] != null && attacks[i].IsOutdated) OutdatedAttacks++;
            }

            if (OutdatedAttacks == 0) return;

            Debug.LogError("[UberBagarre] " + OutdatedAttacks + " coup(s) datent d'une version " +
                           "anterieure du code : leurs timings, leurs couts et leurs poses sont les " +
                           "ANCIENS. Lance 'Uber Bagarre > 2 - Construire la scene Combat Sandbox' " +
                           "(ou '4 - Regenerer les coups par defaut').", this);
        }

        private void Update()
        {
            if (_input == null || _executor == null) return;

            // Mort ou au sol : plus d'entrees de gameplay, mais la camera reste libre pour voir
            // ce qui se passe. Marcher normalement en etant couche viderait la chute de tout
            // son sens : c'est la perte de controle qui en fait une punition.
            bool dead = _combatant != null && !_combatant.IsAlive;
            bool down = _knockdown != null && _knockdown.IsDown;

            if (_motor != null) _motor.InputLocked = dead || down;

            if (dead || down)
            {
                // La garde tombe explicitement : sans cette ligne, elle garderait sa derniere
                // valeur et un combattant couche continuerait de bloquer les coups.
                if (_guard != null) _guard.SetGuard(false);
                _executor.CancelCharge();
                _buffered = null;
                return;
            }

            if (_input.DodgePressed) TryDodge();

            UpdateGuard();
            UpdateAttacks();

            UpdateSprintCost();
            UpdateMovementPenalty();
        }

        /// <summary>
        /// Lit l'intention d'attaque, la mémorise, et la rejoue dès que l'exécuteur l'accepte.
        ///
        /// C'est le tampon d'entrée, et c'est ce qui manquait pour que le combat réponde. Un coup
        /// n'est annulable qu'après sa fenêtre d'impact — soit les deux tiers de sa durée. Sans
        /// tampon, toute touche pressée pendant ces deux tiers disparaissait purement et
        /// simplement : l'exécuteur refusait, et personne ne s'en souvenait à l'image suivante.
        ///
        /// Le joueur, lui, avait bien appuyé. Il voyait donc un jeu qui ignore la moitié de ses
        /// ordres, ce qui ne se ressent pas comme « mon timing est mauvais » mais comme « le jeu
        /// est mou ». Aucun réglage de durée n'aurait pu corriger ça.
        /// </summary>
        private void UpdateAttacks()
        {
            if (UpdateCharging()) return;

            AttackData requested = ReadAttackIntent();

            if (requested != null)
            {
                // Anti-martelage : un appui fait TROP tot n'est pas memorise. Seul l'appui fait
                // juste avant la fin de la recuperation est garde (le tampon), ce qui recompense
                // le rythme et pas la vitesse du doigt.
                if (Time.time >= _nextAttackAllowed - _inputBuffer)
                {
                    _buffered = requested;
                    _bufferedUntil = Time.time + _inputBuffer;
                }
            }

            if (_buffered == null) return;

            if (Time.time < _nextAttackAllowed)
            {
                if (Time.time > _bufferedUntil) _buffered = null;
                return;
            }

            if (_executor.TryPlay(_buffered))
            {
                _buffered = null;
                LockNextAttack();
                return;
            }

            // Le tampon a une durée de vie : au-delà, l'ordre n'est plus celui que le joueur
            // voulait. Rejouer un coup demandé une seconde plus tôt serait pire que de l'oublier.
            if (Time.time > _bufferedUntil) _buffered = null;
        }

        /// <summary>
        /// Gère les coups qui se CHARGENT. Renvoie vrai si une charge occupe la frame.
        ///
        /// Les coups lourds se chargent, les coups rapides se répètent, et ce partage n'est pas
        /// arbitraire : l'intérêt d'un direct est de partir tout de suite, donc le maintenir doit
        /// l'enchaîner. L'intérêt d'un uppercut est son poids, donc le maintenir doit l'armer.
        /// Chaque touche garde ainsi un comportement qui découle du coup lui-même.
        /// </summary>
        private bool UpdateCharging()
        {
            // Pas de nouvelle charge pendant la recuperation du coup precedent.
            AttackData held = !_executor.IsCharging && Time.time < _nextAttackAllowed ? null : ReadChargeableHeld();

            if (held != null)
            {
                _executor.HoldCharge(held);
                return true;
            }

            // Touche relachee : le coup part avec la charge accumulee.
            if (_executor.IsCharging)
            {
                bool released = _executor.ReleaseCharge();
                if (_executor.IsAttacking) LockNextAttack();
                return released;
            }

            return false;
        }

        /// <summary>
        /// Fixe le moment où le prochain coup pourra partir : jamais avant l'intervalle minimal,
        /// et jamais avant la fin du coup en cours plus sa récupération. Un coup de pied lourd
        /// laisse donc le joueur exposé plus longtemps qu'un direct.
        /// </summary>
        private void LockNextAttack()
        {
            float duration = _executor != null ? _executor.EffectiveDuration : 0f;
            _nextAttackAllowed = Time.time + Mathf.Max(_minAttackInterval, duration + _attackRecovery);
        }

        /// <summary>Le coup chargeable dont la touche est actuellement maintenue, s'il y en a un.</summary>
        private AttackData ReadChargeableHeld()
        {
            if (_input.LowKickHeld) return Chargeable(Resolve(_lowKick));
            if (_input.KickHeld) return Chargeable(Resolve(_kick));
            if (_input.UppercutHeld) return Chargeable(Resolve(_uppercut));
            return null;
        }

        private static AttackData Chargeable(AttackData attack)
        {
            return attack != null && attack.chargeable ? attack : null;
        }

        /// <summary>
        /// Quel coup le joueur demande. Les pressions gagnent toujours sur les maintiens : appuyer
        /// sur le coup de pied pendant qu'on tient le clic gauche doit sortir le coup de pied.
        /// </summary>
        private AttackData ReadAttackIntent()
        {
            // Ordre volontaire : du coup le plus engageant au plus rapide. Deux touches pressees
            // dans la meme image doivent donner un resultat previsible, pas le coup qui se trouve
            // en premier dans le code.
            // Le coup de tete et la bousculade ne passent PAS par la substitution contextuelle :
            // sprinter puis presser G ne doit pas sortir une charge d'epaule, et un coup de tete
            // sur un homme a terre n'aurait pas de sens.
            if (_input.HeadbuttPressed && _headbuttUnlocked && _headbutt != null) return _headbutt;
            if (_input.ShovePressed && _shove != null) return _shove;

            if (_input.LowKickPressed) return Resolve(_lowKick);
            if (_input.KickPressed) return Resolve(_kick);
            if (_input.UppercutPressed) return Resolve(_uppercut);
            if (_input.HookPressed) return Resolve(_hook);
            if (_input.StraightPressed) return Resolve(_straight);

            if (!_repeatWhileHeld) return null;

            if (_input.HookHeld) return Resolve(_hook);
            if (_input.StraightHeld) return Resolve(_straight);

            return null;
        }

        /// <summary>
        /// Substitue au coup demandé celui que la SITUATION impose, s'il y en a un.
        ///
        /// Sprinter, être en l'air, glisser ou avoir un adversaire au sol transforment le même
        /// ordre en un coup différent. C'est quatre attaques de plus sans une seule touche de plus,
        /// et surtout rien à apprendre : le joueur les découvre en jouant normalement.
        ///
        /// L'ordre des tests est une priorité : être en l'air l'emporte sur tout le reste, parce
        /// qu'aucun autre coup n'a de sens les pieds décollés.
        /// </summary>
        private AttackData Resolve(AttackData requested)
        {
            if (requested == null || _motor == null) return requested;

            if (!_motor.IsGrounded && _dive != null) return _dive;
            if (_motor.IsSliding && _sweep != null) return _sweep;

            // Le coup de grace ne remplace que les coups de PIED : achever quelqu'un au sol d'un
            // crochet demanderait de se pencher, ce que le corps ne sait pas faire.
            if (requested.limb == AttackLimb.Foot && _stomp != null && IsTargetDown()) return _stomp;

            if (_motor.IsSprinting && _shoulderCharge != null && requested.limb == AttackLimb.Hand)
            {
                return _shoulderCharge;
            }

            return requested;
        }

        /// <summary>Vrai si la cible visée est au sol. Sert au coup de grâce.</summary>
        private bool IsTargetDown()
        {
            Combatant target = _target;

            if (target == null && _combatant != null)
            {
                Hurtbox aimed = AimResolver.Resolve(_combatant);
                if (aimed != null) target = aimed.GetComponentInParent<Combatant>();
            }

            if (target == null) return false;

            KnockdownSystem knockdown = target.GetComponent<KnockdownSystem>();
            return knockdown != null && knockdown.IsDown;
        }

        /// <summary>
        /// La garde est tenue, pas déclenchée : c'est la durée de maintien qui distingue une
        /// parade d'un blocage, et c'est le GuardSystem qui mesure ce temps.
        ///
        /// On ne garde pas pendant son propre coup : sinon lever la garde en frappant donnerait
        /// une invulnérabilité gratuite pendant toute l'attaque.
        /// </summary>
        private void UpdateGuard()
        {
            if (_guard == null) return;

            bool canGuard = !_executor.IsAttacking && (_combatant == null || _combatant.IsAlive);
            _guard.SetGuard(_input.GuardHeld && canGuard);
        }

        /// <summary>
        /// La glissade se paie au départ, en une fois.
        ///
        /// Le moteur a déjà démarré la glissade quand on arrive ici : c'est voulu. Interdire
        /// après coup donnerait une glissade qui s'interrompt en plein vol. À la place, elle se
        /// déroule normalement et c'est la SUIVANTE qui est refusée, faute d'endurance.
        /// </summary>
        private void OnSlideStarted()
        {
            if (_combatant == null || _combatant.Stamina == null) return;

            _combatant.Stamina.TrySpend(_slideStaminaCost);
        }

        /// <summary>
        /// L'esquive part de la direction de déplacement voulue. Sans direction, on esquive
        /// vers l'arrière : c'est le réflexe naturel, et ça évite une esquive immobile.
        /// </summary>
        private void TryDodge()
        {
            if (_dodge == null) return;

            Vector2 move = _input.Move;
            Vector3 direction = transform.right * move.x + transform.forward * move.y;

            if (direction.sqrMagnitude < 0.01f) direction = -transform.forward;

            _dodge.TryDodge(direction);
        }

        /// <summary>
        /// Le sprint puise dans l'endurance et s'arrête quand elle est vide.
        /// Sans ça, la stamina ne limiterait que le combat et courir serait gratuit —
        /// or fuir sans coût rend toute la gestion d'endurance sans objet.
        /// </summary>
        private void UpdateSprintCost()
        {
            if (_motor == null || _combatant == null || _combatant.Stamina == null) return;

            StaminaSystem stamina = _combatant.Stamina;

            if (_motor.IsSprinting && _sprintStaminaPerSecond > 0f)
            {
                stamina.TrySpend(_sprintStaminaPerSecond * Time.deltaTime);
            }

            _motor.SprintBlocked = stamina.IsEmpty;

            // La glissade se refuse AVANT de partir : il faut de quoi la payer entierement.
            _motor.SlideBlocked = !stamina.CanSpend(_slideStaminaCost);
        }

        private void UpdateMovementPenalty()
        {
            if (_motor == null) return;

            float target = _executor.IsAttacking ? _attackSpeedMultiplier : 1f;

            _currentSpeedMultiplier = _executor.IsAttacking
                ? target
                : Mathf.MoveTowards(_currentSpeedMultiplier, 1f, _speedRecovery * Time.deltaTime);

            _motor.SpeedMultiplier = _currentSpeedMultiplier;
        }
    }
}
