using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Compte ce qui se passe réellement pendant un combat.
    ///
    /// Pourquoi ça vaut un composant : tout l'équilibrage d'un jeu de combat se joue sur des
    /// rapports qu'on ne peut pas estimer en jouant. « Est-ce que je touche souvent ? » n'a pas de
    /// réponse au ressenti — un joueur qui rate la moitié de ses coups a l'impression que
    /// l'adversaire encaisse trop, alors que le problème est sa précision. Un taux de réussite
    /// affiché règle ce débat en une seconde.
    ///
    /// Les compteurs s'abonnent aux événements STATIQUES de combat, donc ils comptent aussi les
    /// adversaires apparus après eux — ce qui est indispensable dès qu'on fait apparaître des
    /// ennemis en jeu.
    /// </summary>
    public class CombatStatistics : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _player;
        [SerializeField] private AttackExecutor _executor;
        [SerializeField] private GuardSystem _guard;
        [SerializeField] private ComboTracker _combo;

        public int AttacksThrown { get; private set; }
        public int AttacksLanded { get; private set; }
        public int Parries { get; private set; }
        public int Blocks { get; private set; }
        public int KnockdownsCaused { get; private set; }
        public int KnockdownsSuffered { get; private set; }
        public int Knockouts { get; private set; }
        public int Deaths { get; private set; }

        public float DamageDealt { get; private set; }
        public float DamageTaken { get; private set; }
        public float HighestHit { get; private set; }
        public int BestCombo { get; private set; }

        /// <summary>Part des coups lancés qui ont touché, 0 à 1.</summary>
        public float Accuracy
        {
            get { return AttacksThrown <= 0 ? 0f : AttacksLanded / (float)AttacksThrown; }
        }

        /// <summary>Rapport dégâts infligés / encaissés. Au-dessus de 1, l'échange est à l'avantage du joueur.</summary>
        public float DamageRatio
        {
            get { return DamageTaken <= 0.01f ? DamageDealt : DamageDealt / DamageTaken; }
        }

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;
            Combatant.AnyDied += OnAnyDied;
            KnockdownSystem.AnyKnockedDown += OnAnyKnockedDown;

            if (_executor != null)
            {
                _executor.AttackStarted += OnAttackStarted;
                _executor.HitLanded += OnHitLanded;
            }

            if (_guard != null)
            {
                _guard.Parried += OnParried;
                _guard.Blocked += OnBlocked;
            }
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;
            Combatant.AnyDied -= OnAnyDied;
            KnockdownSystem.AnyKnockedDown -= OnAnyKnockedDown;

            if (_executor != null)
            {
                _executor.AttackStarted -= OnAttackStarted;
                _executor.HitLanded -= OnHitLanded;
            }

            if (_guard != null)
            {
                _guard.Parried -= OnParried;
                _guard.Blocked -= OnBlocked;
            }
        }

        private void Update()
        {
            if (_combo != null && _combo.Count > BestCombo) BestCombo = _combo.Count;
        }

        private void OnAttackStarted(AttackData attack, View.HandSide side)
        {
            AttacksThrown++;
        }

        private void OnHitLanded(AttackData attack, Hurtbox hurtbox, Vector3 point)
        {
            AttacksLanded++;
        }

        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            bool fromPlayer = _player != null && info.Attacker == _player.gameObject;
            bool onPlayer = _player != null && victim == _player;

            if (fromPlayer)
            {
                DamageDealt += info.Amount;
                if (info.Amount > HighestHit) HighestHit = info.Amount;
            }

            if (onPlayer) DamageTaken += info.Amount;
        }

        private void OnAnyDied(Combatant victim, DamageInfo info)
        {
            if (_player != null && victim == _player) Deaths++;
            else if (_player != null && info.Attacker == _player.gameObject) Knockouts++;
        }

        private void OnAnyKnockedDown(KnockdownSystem knockdown)
        {
            if (_player != null && knockdown.gameObject == _player.gameObject) KnockdownsSuffered++;
            else KnockdownsCaused++;
        }

        private void OnParried(DamageInfo info)
        {
            Parries++;
        }

        private void OnBlocked(DamageInfo info)
        {
            Blocks++;
        }

        public void ResetStatistics()
        {
            AttacksThrown = 0;
            AttacksLanded = 0;
            Parries = 0;
            Blocks = 0;
            KnockdownsCaused = 0;
            KnockdownsSuffered = 0;
            Knockouts = 0;
            Deaths = 0;
            DamageDealt = 0f;
            DamageTaken = 0f;
            HighestHit = 0f;
            BestCombo = 0;
        }
    }
}
