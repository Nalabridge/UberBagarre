using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>À quel camp appartient un combattant. Un coup ne touche jamais son propre camp.</summary>
    public enum Faction
    {
        Player = 0,
        Enemy = 1,
        Neutral = 2
    }

    /// <summary>Zone touchée. Sert de multiplicateur de dégâts et de choix de réaction.</summary>
    public enum HitZone
    {
        Body = 0,
        Head = 1,
        Arm = 2
    }

    /// <summary>
    /// Tout ce qu'un coup transporte, en un seul objet.
    ///
    /// Passer une structure plutôt qu'un simple float de dégâts permet à chaque système en aval
    /// (vie, réaction, secousse de caméra, son, recul) de décider quoi faire à partir des mêmes
    /// informations, sans que l'attaquant ait à les connaître.
    /// </summary>
    public struct DamageInfo
    {
        public float Amount;
        public Vector3 Point;
        public Vector3 Direction;
        public float ImpactForce;
        public HitZone Zone;
        public Faction AttackerFaction;
        public GameObject Attacker;
        public AttackData Attack;

        public bool IsHeavy
        {
            get { return Attack != null && Attack.isHeavy; }
        }
    }

    /// <summary>Ce qui peut recevoir des dégâts. Le joueur, les ennemis, un sac de frappe, un objet cassable.</summary>
    public interface IDamageable
    {
        bool IsAlive { get; }
        void ApplyDamage(DamageInfo info);
    }
}
