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
    ///   Clic gauche              → direct (alterne gauche / droite)
    ///   Ctrl + clic gauche       → crochet
    ///   Alt  + clic gauche       → uppercut
    /// </summary>
    public class PlayerCombat : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private AttackExecutor _executor;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private DodgeSystem _dodge;
        [SerializeField] private Combatant _combatant;

        [Header("Coups")]
        [SerializeField] private AttackData _straight;
        [SerializeField] private AttackData _hook;
        [SerializeField] private AttackData _uppercut;

        [Header("Effet sur le deplacement")]
        [SerializeField, Range(0f, 1f)]
        [Tooltip("Vitesse conservee pendant un coup. On ne court pas en frappant.")]
        private float _attackSpeedMultiplier = 0.42f;

        [SerializeField, Min(0.5f)] private float _speedRecovery = 4f;

        [Header("Cout du sprint")]
        [SerializeField, Min(0f)]
        [Tooltip("Endurance consommee par seconde de course. Courir doit se payer, sinon la " +
                 "stamina ne limite que le combat et la course devient gratuite.")]
        private float _sprintStaminaPerSecond = 13f;

        private float _currentSpeedMultiplier = 1f;

        private void Awake()
        {
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_motor == null) _motor = GetComponentInParent<PlayerMotor>();
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

            if (_executor == null) Debug.LogError("[UberBagarre] PlayerCombat : aucun AttackExecutor assigne.", this);
            if (_input == null) Debug.LogError("[UberBagarre] PlayerCombat : aucun PlayerInputReader assigne.", this);
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

        private void Update()
        {
            if (_input == null || _executor == null) return;

            // Mort : plus d'entrees de gameplay, mais la camera reste libre pour voir ce qui se passe.
            bool dead = _combatant != null && !_combatant.IsAlive;
            if (_motor != null) _motor.InputLocked = dead;
            if (dead) return;

            if (_input.DodgePressed) TryDodge();

            if (_input.UppercutPressed) _executor.TryPlay(_uppercut);
            else if (_input.HookPressed) _executor.TryPlay(_hook);
            else if (_input.StraightPressed) _executor.TryPlay(_straight);

            UpdateSprintCost();
            UpdateMovementPenalty();
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
