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
        [Tooltip("Optionnel : hors combat, esquive et glissade sont desactivees.")]
        private CombatPresence _presence;

        [SerializeField]
        [Tooltip("Optionnel. Sans lui, la touche de garde ne fait que lever les poings a l'ecran.")]
        private GuardSystem _guard;

        [SerializeField]
        [Tooltip("Optionnel. Sert uniquement a couper le deplacement pendant qu'on est au sol.")]
        private KnockdownSystem _knockdown;

        [SerializeField]
        [Tooltip("Optionnel (cherche sur le joueur) : la visee, que l'aide au ciblage oriente.")]
        private PlayerLook _look;

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
        [Tooltip("Intervalle minimal entre deux coups, en secondes, quel que soit le coup. Le " +
                 "vrai rythme vient du point d'enchainement de chaque coup (fin de sa fenetre " +
                 "d'impact) : un direct en enchaine un autre en ~0,2 s, un crochet en ~0,27 s.")]
        private float _chainInterval = 0.22f;

        [Header("Aide au ciblage")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part de l'ecart vers l'adversaire que la vue rattrape quand un coup part : le " +
                 "verrouillage doux a la GTA. 0 = aucune aide, 1 = le coup recentre la vue sur lui.")]
        private float _aimAssist = 0.7f;

        [SerializeField, Range(0f, 60f)]
        [Tooltip("Au-dela de cet angle, l'adversaire n'est pas celui qu'on vise : pas d'aide.")]
        private float _aimAssistMaxAngle = 28f;

        [Header("Effet sur le deplacement")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Vitesse conservee pendant un coup. Volontairement haut : on frappe en tournant " +
                 "autour de l'adversaire, comme dans GTA ; a 0,42 un joueur qui enchaine etait " +
                 "immobilise en permanence, ce qui se ressent comme de la lourdeur, pas du poids.")]
        private float _moveWhileAttacking = 0.75f;

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
        private AttackKey _bufferedKey;
        private AttackKey _holdKey;
        private float _bufferedUntil;
        private float _nextAttackAllowed;

        /// <summary>La touche qui a demandé un coup : sert à savoir si elle est encore tenue (charge).</summary>
        private enum AttackKey
        {
            None,
            Straight,
            Hook,
            Uppercut,
            Kick,
            LowKick,
            Headbutt,
            Shove
        }

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
            if (_presence == null) _presence = GetComponent<CombatPresence>();
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
            if (_guard == null) _guard = GetComponentInParent<GuardSystem>();
            if (_look == null) _look = GetComponentInParent<PlayerLook>();
            if (_look == null && _motor != null) _look = _motor.GetComponentInChildren<PlayerLook>();
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

        /// <summary>Hors combat, esquive et glissade sont rangées. Sans CombatPresence : toujours en combat.</summary>
        private bool InCombat
        {
            get { return _presence == null || _presence.InCombat; }
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

            // Hors combat, pas d'esquive : une roulade en rentrant chez soi n'a aucun sens.
            if (_input.DodgePressed && InCombat) TryDodge();

            UpdateGuard();
            UpdateAttacks();

            UpdateSprintCost();
            UpdateMovementPenalty();
        }

        /// <summary>
        /// Lit l'intention d'attaque, la mémorise, et la rejoue dès que l'exécuteur l'accepte.
        ///
        /// C'est le tampon d'entrée, et c'est ce qui fait que le combat répond. Sans lui, toute
        /// touche pressée pendant la partie non annulable d'un coup disparaissait : le joueur
        /// voyait un jeu qui ignore la moitié de ses ordres, ce qui ne se ressent pas comme « mon
        /// timing est mauvais » mais comme « le jeu est mou ».
        ///
        /// Façon GTA : un appui pendant un coup est GARDÉ (un seul, le dernier demandé) et part à
        /// l'instant où le coup en cours devient enchaînable — fin de sa fenêtre d'impact, et pas
        /// fin de tout le geste. Clic, clic, clic : les coups s'enchaînent sans temps mort. Le
        /// martelage n'y gagne rien de plus : un seul ordre en attente, et chaque coup coûte de
        /// l'endurance.
        /// </summary>
        private void UpdateAttacks()
        {
            // Coup chargeable tenu : il est déjà parti ; lâcher la touche le libère.
            if (_executor.IsHolding && !KeyHeld(_holdKey)) _executor.ReleaseHold();

            AttackKey key;
            AttackData requested = ReadAttackIntent(out key);

            if (requested != null)
            {
                _buffered = requested;
                _bufferedKey = key;
                _bufferedUntil = Mathf.Max(Time.time, _nextAttackAllowed) + _inputBuffer;
            }

            if (_buffered == null) return;

            // Le coup en cours n'est pas encore enchaînable (gel de contact, coup lourd raté) :
            // l'ordre attend sans se périmer — il partira dès que possible.
            bool waiting = Time.time < _nextAttackAllowed || (_executor.IsAttacking && !_executor.CanChain);
            if (waiting) return;

            // Un coup chargeable dont la touche est encore enfoncée part tout de suite et tient
            // son armement : appui bref = coup sec, appui tenu = coup chargé.
            bool hold = _buffered.chargeable && KeyHeld(_bufferedKey);
            bool played = hold ? _executor.TryPlayHeld(_buffered) : _executor.TryPlay(_buffered);

            if (played)
            {
                _holdKey = hold ? _bufferedKey : AttackKey.None;
                _buffered = null;
                LockNextAttack();
                AssistAim();
                return;
            }

            // Le tampon a une durée de vie : au-delà, l'ordre n'est plus celui que le joueur
            // voulait. Rejouer un coup demandé une seconde plus tôt serait pire que de l'oublier.
            if (Time.time > _bufferedUntil) _buffered = null;
        }

        /// <summary>
        /// Fixe le moment le plus tôt où le coup suivant pourra partir : le point d'enchaînement
        /// du coup (fin de sa fenêtre d'impact), jamais moins que l'intervalle minimal. L'exécuteur
        /// a le dernier mot (<see cref="AttackExecutor.CanChain"/>) : un coup lourd parti dans le
        /// vide s'enchaîne plus tard, c'est le prix du coup raté.
        /// </summary>
        private void LockNextAttack()
        {
            float duration = _executor != null ? _executor.EffectiveDuration : 0f;
            AttackData current = _executor != null ? _executor.CurrentAttack : null;
            float chainAt = current != null ? current.comboCancelAt : 1f;
            _nextAttackAllowed = Time.time + Mathf.Max(_chainInterval, duration * chainAt);
        }

        /// <summary>
        /// Le verrouillage doux : quand un coup part vers un adversaire proche du centre de la
        /// vue, la vue rattrape une partie de l'écart. On frappe là où on regarde À PEU PRÈS,
        /// comme dans GTA, au lieu de fendre l'air pour quelques degrés.
        /// </summary>
        private void AssistAim()
        {
            if (_look == null || _aimAssist <= 0f || _combatant == null) return;

            StrikeTarget target = _executor.Target;
            Vector3 point;
            bool aimPitch = target.Valid;

            if (target.Valid)
            {
                point = target.WorldPoint;
            }
            else
            {
                Combatant opponent = StrikeTarget.NearestInFront(_combatant);
                if (opponent == null) return;
                point = opponent.transform.position + Vector3.up * 1.2f;
            }

            Transform head = _look.Head;
            Vector3 to = point - head.position;
            Vector3 flat = new Vector3(to.x, 0f, to.z);
            Vector3 forward = head.forward;
            forward.y = 0f;
            if (flat.sqrMagnitude < 0.01f || forward.sqrMagnitude < 1e-4f) return;

            float yaw = Vector3.SignedAngle(forward, flat, Vector3.up);
            if (Mathf.Abs(yaw) > _aimAssistMaxAngle) return;

            float pitch = 0f;
            if (aimPitch)
            {
                float wanted = -Mathf.Atan2(to.y, flat.magnitude) * Mathf.Rad2Deg;
                pitch = Mathf.Clamp(Mathf.DeltaAngle(_look.Pitch, wanted), -12f, 12f) * 0.5f;
            }

            _look.Nudge(yaw * _aimAssist, pitch * _aimAssist);
        }

        private bool KeyHeld(AttackKey key)
        {
            switch (key)
            {
                case AttackKey.Uppercut: return _input.UppercutHeld;
                case AttackKey.Kick: return _input.KickHeld;
                case AttackKey.LowKick: return _input.LowKickHeld;
                case AttackKey.Hook: return _input.HookHeld;
                case AttackKey.Straight: return _input.StraightHeld;
                default: return false;
            }
        }

        /// <summary>
        /// Quel coup le joueur demande. Les pressions gagnent toujours sur les maintiens : appuyer
        /// sur le coup de pied pendant qu'on tient le clic gauche doit sortir le coup de pied.
        /// </summary>
        private AttackData ReadAttackIntent(out AttackKey key)
        {
            key = AttackKey.None;

            // Ordre volontaire : du coup le plus engageant au plus rapide. Deux touches pressees
            // dans la meme image doivent donner un resultat previsible, pas le coup qui se trouve
            // en premier dans le code.
            // Le coup de tete et la bousculade ne passent PAS par la substitution contextuelle :
            // sprinter puis presser G ne doit pas sortir une charge d'epaule, et un coup de tete
            // sur un homme a terre n'aurait pas de sens.
            if (_input.HeadbuttPressed && _headbuttUnlocked && _headbutt != null) { key = AttackKey.Headbutt; return _headbutt; }
            if (_input.ShovePressed && _shove != null) { key = AttackKey.Shove; return _shove; }

            if (_input.LowKickPressed) { key = AttackKey.LowKick; return Resolve(_lowKick); }
            if (_input.KickPressed) { key = AttackKey.Kick; return Resolve(_kick); }
            if (_input.UppercutPressed) { key = AttackKey.Uppercut; return Resolve(_uppercut); }
            if (_input.HookPressed) { key = AttackKey.Hook; return Resolve(_hook); }
            if (_input.StraightPressed) { key = AttackKey.Straight; return Resolve(_straight); }

            // Maintien : seulement si l'option est active, et jamais pendant un coup tenu (la
            // touche tenue CHARGE, elle ne doit pas aussi relancer).
            if (!_repeatWhileHeld || _executor.IsHolding) return null;

            if (_input.HookHeld) { key = AttackKey.Hook; return Resolve(_hook); }
            if (_input.StraightHeld) { key = AttackKey.Straight; return Resolve(_straight); }

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

            // La glissade se refuse AVANT de partir : il faut de quoi la payer entierement. Et
            // comme l'esquive, elle est rangee hors combat.
            _motor.SlideBlocked = !stamina.CanSpend(_slideStaminaCost) || !InCombat;
        }

        private void UpdateMovementPenalty()
        {
            if (_motor == null) return;

            float target = _executor.IsAttacking ? _moveWhileAttacking : 1f;

            _currentSpeedMultiplier = _executor.IsAttacking
                ? target
                : Mathf.MoveTowards(_currentSpeedMultiplier, 1f, _speedRecovery * Time.deltaTime);

            _motor.SpeedMultiplier = _currentSpeedMultiplier;
        }
    }
}
