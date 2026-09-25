using System;
using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>Le membre qui porte un coup capturé.</summary>
    public enum MocapLimb
    {
        LeftHand = 0,
        RightHand = 1,
        LeftFoot = 2,
        RightFoot = 3
    }

    /// <summary>
    /// Un coup capturé, rangé pour une de nos attaques : quel asset d'attaque (A_Direct…), quel
    /// côté, et s'il s'agit d'une variante au corps.
    /// </summary>
    [Serializable]
    public class MocapMove
    {
        [Tooltip("Nom de l'asset d'attaque du jeu (A_Direct, A_Crochet...).")]
        public string attack;

        public HandSide side;

        [Tooltip("Variante au corps (direct au corps, crochet au corps...).")]
        public bool body;

        public MocapLimb limb;
        public AnimationClip clip;

        [Tooltip("Instant de l'impact dans le clip (0-1). Negatif = trouve automatiquement " +
                 "(extension maximale du membre).")]
        public float impact = -1f;
    }

    /// <summary>
    /// Les animations capturées (FS Melee Combat System, Fantacode Studios) et leur rôle dans
    /// le jeu. Rempli par l'éditeur (MocapLibraryBuilder) ; lu par <see cref="MocapDriver"/>.
    /// </summary>
    [CreateAssetMenu(fileName = "Mocap", menuName = "Uber Bagarre/Bibliotheque d'animations", order = 3)]
    public class MocapLibrary : ScriptableObject
    {
        [Header("Garde et deplacements")]
        public AnimationClip combatIdle;
        public AnimationClip walkForward;
        public AnimationClip walkBack;
        public AnimationClip walkLeft;
        public AnimationClip walkRight;

        [Tooltip("Vitesse du corps dans les clips de marche avant / arriere (m/s), mesuree sur les clips.")]
        public float walkSpeed = 1.66f;

        [Tooltip("Vitesse du corps dans les clips de pas chasses (m/s).")]
        public float strafeSpeed = 1.34f;

        [Tooltip("Hors combat : debout, bras le long du corps.")]
        public AnimationClip relaxedIdle;

        public AnimationClip relaxedWalk;

        [Tooltip("Vitesse du corps dans la marche hors combat (m/s).")]
        public float relaxedWalkSpeed = 1.3f;

        [Header("Defense")]
        public AnimationClip block;
        public AnimationClip blockHit;
        public AnimationClip dodgeBack;

        [Header("Chutes")]
        public AnimationClip knockDownBack;
        public AnimationClip knockDownFront;
        public AnimationClip lyingDown;
        public AnimationClip gettingUp;

        [Tooltip("Frappe recue au sol.")]
        public AnimationClip groundHit;

        [Header("Reactions aux coups")]
        [Tooltip("Coups legers : la tete part d'un cote ou de l'autre (classees a l'execution).")]
        public List<AnimationClip> lightReactions = new List<AnimationClip>();

        [Tooltip("Coups lourds : le corps entier encaisse.")]
        public List<AnimationClip> heavyReactions = new List<AnimationClip>();

        [Tooltip("Uppercut recu : la tete part en arriere et vers le haut.")]
        public AnimationClip uppercutReaction;

        [Tooltip("Contre (riposte apres parade) : la reaction la plus violente.")]
        public List<AnimationClip> counterReactions = new List<AnimationClip>();

        [Header("Coups")]
        public List<MocapMove> moves = new List<MocapMove>();

        public bool IsUsable
        {
            get { return combatIdle != null && moves != null && moves.Count > 0; }
        }

        /// <summary>
        /// Les coups capturés pour cette attaque, de ce côté (et de cette nature, tête ou corps).
        /// Une variante au corps sans clip propre retombe sur les clips « tête ».
        /// </summary>
        public void Find(string attack, HandSide side, bool body, List<MocapMove> result)
        {
            result.Clear();
            if (moves == null || string.IsNullOrEmpty(attack)) return;

            for (int pass = 0; pass < 2 && result.Count == 0; pass++)
            {
                bool wantBody = pass == 0 ? body : !body;

                for (int i = 0; i < moves.Count; i++)
                {
                    MocapMove move = moves[i];
                    if (move == null || move.clip == null) continue;
                    if (move.attack != attack || move.side != side || move.body != wantBody) continue;
                    result.Add(move);
                }
            }
        }

        /// <summary>Vrai si cette attaque a au moins un clip, quel que soit le côté.</summary>
        public bool Has(string attack)
        {
            if (moves == null) return false;

            for (int i = 0; i < moves.Count; i++)
            {
                if (moves[i] != null && moves[i].clip != null && moves[i].attack == attack) return true;
            }

            return false;
        }
    }
}
