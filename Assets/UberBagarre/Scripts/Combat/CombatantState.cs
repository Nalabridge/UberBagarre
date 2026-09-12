using System;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// États d'un combattant. Le rôle de cette machine est d'empêcher que tout soit possible
    /// en même temps : pas de second coup au milieu d'un coup, pas d'esquive pendant un KO,
    /// pas d'attaque pendant une réaction.
    /// </summary>
    public enum CombatantState
    {
        Idle = 0,
        Moving = 1,
        Guarding = 2,
        Attacking = 3,
        Recovering = 4,
        Dodging = 5,
        Hit = 6,
        Stunned = 7,
        Dead = 8
    }

    /// <summary>
    /// Machine à états minimale, à priorités et à minuteur.
    ///
    /// Une transition n'est acceptée que si le nouvel état est au moins aussi prioritaire que
    /// l'actuel, ou si l'actuel est terminé. Deux lignes de règle plutôt qu'un tableau de
    /// transitions : ajouter un état (parade, contre, projection) ne demandera que de lui
    /// donner une priorité.
    /// </summary>
    public class CombatantStateMachine
    {
        private float _timer;

        public CombatantState Current { get; private set; }

        /// <summary>Temps restant dans l'état courant. 0 = état libre.</summary>
        public float Remaining { get { return _timer; } }

        public event Action<CombatantState, CombatantState> Changed;

        public bool IsBusy { get { return _timer > 0f; } }

        public bool IsDead { get { return Current == CombatantState.Dead; } }

        /// <summary>Vrai si le combattant peut décider d'une action (attaquer, esquiver, bouger).</summary>
        public bool CanAct
        {
            get
            {
                return Current != CombatantState.Dead
                       && Current != CombatantState.Stunned
                       && Current != CombatantState.Hit
                       && Current != CombatantState.Attacking
                       && Current != CombatantState.Dodging
                       && Current != CombatantState.Recovering;
            }
        }

        public bool CanEnter(CombatantState next)
        {
            if (Current == CombatantState.Dead) return false;
            if (next == CombatantState.Dead) return true;
            if (_timer <= 0f) return true;

            return Priority(next) >= Priority(Current);
        }

        public bool TryEnter(CombatantState next, float duration = 0f)
        {
            if (!CanEnter(next)) return false;

            Enter(next, duration);
            return true;
        }

        public void Enter(CombatantState next, float duration = 0f)
        {
            CombatantState previous = Current;
            Current = next;
            _timer = Mathf.Max(0f, duration);

            if (previous == next) return;

            Action<CombatantState, CombatantState> changed = Changed;
            if (changed != null) changed(previous, next);
        }

        public void Tick(float deltaTime)
        {
            if (_timer <= 0f) return;

            _timer -= deltaTime;
            if (_timer > 0f) return;

            _timer = 0f;
            if (Current != CombatantState.Dead) Enter(CombatantState.Idle);
        }

        /// <summary>
        /// Plus la valeur est haute, plus l'état s'impose. C'est ici qu'on décide qu'un coup reçu
        /// interrompt une attaque, mais qu'une attaque n'interrompt pas une esquive.
        /// </summary>
        private static int Priority(CombatantState state)
        {
            switch (state)
            {
                case CombatantState.Dead: return 100;
                case CombatantState.Stunned: return 80;
                case CombatantState.Hit: return 70;
                case CombatantState.Dodging: return 50;
                case CombatantState.Attacking: return 40;
                case CombatantState.Recovering: return 30;
                case CombatantState.Guarding: return 20;
                case CombatantState.Moving: return 10;
                default: return 0;
            }
        }
    }
}
