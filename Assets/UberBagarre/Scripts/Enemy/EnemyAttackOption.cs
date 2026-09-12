using System;
using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Enemy
{
    /// <summary>
    /// Une entrée du répertoire de coups de l'ennemi.
    ///
    /// Tout est réglable sans toucher au code : quel coup, à quelle distance, à quelle
    /// fréquence, avec quel temps de repos. Mettre un poids à zéro suffit à retirer un coup
    /// du répertoire pour un test, sans rien supprimer.
    /// </summary>
    [Serializable]
    public class EnemyAttackOption
    {
        public AttackData attack;

        [Min(0f)]
        [Tooltip("Probabilite relative. 0 = ce coup n'est jamais choisi.")]
        public float weight = 1f;

        [Min(0f)] public float minDistance;
        [Min(0f)] public float maxDistance = 1.6f;

        [Min(0f)]
        [Tooltip("Temps de repos propre a ce coup, en plus du rythme general.")]
        public float cooldown = 1.2f;

        [NonSerialized] public float NextAvailableTime;

        public bool IsUsable(float distance, float time)
        {
            return attack != null
                   && weight > 0f
                   && time >= NextAvailableTime
                   && distance >= minDistance
                   && distance <= maxDistance;
        }
    }

    /// <summary>Une étape d'une séquence de test : « attends X secondes, puis joue ce coup ».</summary>
    [Serializable]
    public class ScriptedAttackStep
    {
        [Min(0f)]
        [Tooltip("Delai avant ce coup, en secondes.")]
        public float delay = 2f;

        public AttackData attack;

        [Tooltip("Laisse vide pour ne rien faire pendant ce delai (utile pour tester l'attente).")]
        public bool holdPosition = true;
    }
}
