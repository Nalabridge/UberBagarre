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
        Arm = 2,

        /// <summary>Les jambes encaissent peu, mais font perdre l'equilibre.</summary>
        Leg = 3
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

        /// <summary>
        /// Vrai si la garde du défenseur a absorbé ce coup.
        ///
        /// Le drapeau voyage AVEC les dégâts, et ce n'est pas un détail : sans lui, chaque
        /// système en aval (réaction, chute, marques) traiterait un coup bloqué exactement
        /// comme un coup reçu en pleine face. Bloquer ne changerait que le nombre de dégâts —
        /// et comme on serait quand même sonné et mis au sol, lever sa garde ne servirait
        /// pratiquement à rien.
        /// </summary>
        public bool Blocked;

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
