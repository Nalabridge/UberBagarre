using System;
using System.Collections.Generic;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Avec quoi le coup est porté.
    ///
    /// Le membre change entièrement la mécanique : un poing suit le repère de visée, un pied
    /// doit reprendre la main sur la jambe que le cycle de marche est en train de piloter.
    /// </summary>
    public enum AttackLimb
    {
        Hand = 0,
        Foot = 1
    }

    /// <summary>Quelle main porte le coup.</summary>
    public enum AttackHand
    {
        Lead = 0,
        Rear = 1,

        /// <summary>Alterne à chaque utilisation : c'est ce qui donne un enchaînement gauche-droite naturel.</summary>
        Alternate = 2
    }

    /// <summary>
    /// Une pose-clé du coup, à un instant donné de son déroulement.
    ///
    /// Tout est exprimé pour la MAIN DROITE. Jouée à gauche, la pose est mirrorée automatiquement.
    /// Un seul jeu de clés sert donc aux deux côtés : moitié moins de réglages, et un crochet
    /// gauche reste forcément le symétrique exact du droit.
    /// </summary>
    [Serializable]
    public struct AttackPoseKey
    {
        [Range(0f, 1f)]
        [Tooltip("Instant dans le coup : 0 = debut, 1 = fin.")]
        public float time;

        [Tooltip("Position du poignet, en espace de visee.")]
        public Vector3 handPosition;

        [Tooltip("Rotation du poignet, en degres.")]
        public Vector3 handEuler;

        [Range(0f, 1f)]
        [Tooltip("Fermeture de la main. On arme parfois main relachee et on serre a l'impact.")]
        public float grip;

        [Tooltip("Rotation du buste. C'est d'ici que vient le poids du coup.")]
        public Vector3 bodyEuler;

        [Tooltip("Deplacement de la camera : recul et accompagnement du corps.")]
        public Vector3 cameraOffset;

        public Vector3 cameraEuler;

        [Header("Membre libre")]
        [Tooltip("Pose de la main OPPOSEE au membre qui frappe. Un boxeur ne lance jamais un " +
                 "bras seul : l'autre remonte se couvrir, et c'est ce qui rend le coup credible.")]
        public Vector3 offHandPosition;

        public Vector3 offHandEuler;

        [Range(0f, 1f)]
        [Tooltip("Poids de la pose ci-dessus. A 0 la main libre garde sa garde habituelle.")]
        public float offHandWeight;
    }

    /// <summary>
    /// Une variante d'animation d'un même coup (Jab_01, Jab_02...).
    /// Le but est qu'un coup répété ne soit jamais exactement identique.
    /// </summary>
    [Serializable]
    public class AttackVariant
    {
        public string name = "01";
        public List<AttackPoseKey> keys = new List<AttackPoseKey>();
    }

    /// <summary>
    /// Un coup, entièrement décrit par des données.
    ///
    /// Ajouter un coude, un coup de pied ou une attaque lourde ne demandera aucun code :
    /// un nouvel asset, des clés, et il est jouable par le joueur comme par l'ennemi.
    /// </summary>
    [CreateAssetMenu(fileName = "Attaque", menuName = "Uber Bagarre/Attaque", order = 1)]
    public class AttackData : ScriptableObject
    {
        [Header("Identite")]
        public string displayName = "Jab";
        public AttackLimb limb = AttackLimb.Hand;
        public AttackHand hand = AttackHand.Alternate;

        [Tooltip("Un coup dans les jambes peut faire chuter. 0 = jamais.")]
        [Range(0f, 1f)] public float knockdownChance;

        [Tooltip("Un coup lourd produit plus de secousse, de recul et de retour d'impact.")]
        public bool isHeavy;

        [Header("Duree (secondes)")]
        [Min(0.02f)] public float duration = 0.30f;

        [Tooltip("Temps mort avant de pouvoir relancer un coup, apres la fin de celui-ci.")]
        [Min(0f)] public float cooldown = 0.04f;

        [Header("Enchainement")]
        [Range(0.3f, 1f)]
        [Tooltip("A partir de quelle fraction du coup un AUTRE coup peut l'interrompre. " +
                 "C'est ce qui rend le combat nerveux : sans annulation d'enchainement, il faut " +
                 "attendre que la main soit revenue a la garde avant de relancer, et chaque coup " +
                 "se paie de sa duree complete. A garder superieur a hitWindowEnd, sinon on " +
                 "peut annuler son propre coup avant qu'il ne touche.")]
        public float comboCancelAt = 0.68f;

        [Header("Fenetre d'impact (fraction de la duree)")]
        [Range(0f, 1f)] public float hitWindowStart = 0.42f;
        [Range(0f, 1f)] public float hitWindowEnd = 0.62f;

        [Tooltip("Rayon de la sphere de detection, centree sur les articulations du poing.")]
        [Min(0.01f)] public float hitRadius = 0.11f;

        [Header("Effet")]
        [Min(0f)] public float damage = 9f;
        [Min(0f)] public float impactForce = 3.5f;
        [Min(0f)] public float staminaCost = 8f;

        [Header("Retour d'impact")]
        [Min(0f)] public float shakeIntensity = 0.05f;
        [Min(0f)] public float shakeDuration = 0.12f;
        [Min(0f)] public float hitStopDuration = 0.035f;

        [Header("Melange avec la garde")]
        [Tooltip("Poids de l'animation d'attaque au fil du coup. A 0 la main revient a sa garde.")]
        public AnimationCurve weightCurve = new AnimationCurve(
            new Keyframe(0f, 0f), new Keyframe(0.12f, 1f), new Keyframe(0.75f, 1f), new Keyframe(1f, 0f));

        [Header("Animation")]
        [Tooltip("Plusieurs variantes evitent que chaque coup soit rigoureusement identique.")]
        public List<AttackVariant> variants = new List<AttackVariant>();

        /// <summary>
        /// Version du format de données.
        ///
        /// Elle existe pour une raison très concrète : le générateur ne réécrit jamais un asset
        /// existant, pour ne pas effacer les réglages faits à la main. Conséquence, affiner un
        /// timing dans le code n'avait AUCUN effet pour qui avait déjà ouvert le projet une fois —
        /// et le symptôme était « tu n'as pas fait ce que j'ai demandé ». En comparant cette
        /// version, le générateur sait distinguer « asset réglé par l'utilisateur » de « asset
        /// créé par une version antérieure du code ».
        /// </summary>
        public const int CurrentVersion = 3;

        [HideInInspector]
        public int dataVersion;

        /// <summary>
        /// Vrai si cet asset a été créé par une version antérieure du code et n'a pas été
        /// régénéré. Ses timings, ses coûts et ses poses sont alors les anciens.
        /// </summary>
        public bool IsOutdated { get { return dataVersion < CurrentVersion; } }

        /// <summary>Nom d'asset des coups de base, pour le chargement de secours depuis Resources.</summary>
        public const string ResourceFolder = "Attaques";
        public const string StraightAsset = "A_Direct";
        public const string HookAsset = "A_Crochet";
        public const string UppercutAsset = "A_Uppercut";
        public const string KickAsset = "A_CoupDePied";
        public const string LowKickAsset = "A_CoupDePiedBas";

        /// <summary>
        /// Charge un coup depuis Resources. Renvoie null si l'asset n'existe pas.
        ///
        /// Sert de filet quand une référence de scène est vide : plutôt qu'un jeu inerte et
        /// muet, on récupère le coup et on signale que la scène devrait être régénérée.
        /// </summary>
        public static AttackData LoadFromResources(string assetName)
        {
            return Resources.Load<AttackData>(ResourceFolder + "/" + assetName);
        }

        /// <summary>Vrai si ce coup possède au moins une variante contenant des poses.</summary>
        public bool HasUsableAnimation
        {
            get
            {
                if (variants == null) return false;

                for (int i = 0; i < variants.Count; i++)
                {
                    if (variants[i] != null && variants[i].keys != null && variants[i].keys.Count >= 2) return true;
                }

                return false;
            }
        }

        /// <summary>
        /// Poids du mélange à cet instant du coup.
        ///
        /// Passe par cette méthode et jamais par la courbe directement : une AnimationCurve vide
        /// renvoie 0 à l'évaluation. Un asset mal initialisé donnerait donc un poids nul du début
        /// à la fin — le coup se déclencherait, la hitbox s'ouvrirait, mais la main ne bougerait
        /// pas d'un millimètre et ne toucherait rien. Panne parfaitement silencieuse.
        /// </summary>
        public float EvaluateWeight(float normalizedTime)
        {
            if (weightCurve != null && weightCurve.length >= 2)
            {
                return Mathf.Clamp01(weightCurve.Evaluate(normalizedTime));
            }

            // Repli : montee rapide, plateau, retour a la garde.
            if (normalizedTime < 0.12f) return Mathf.Clamp01(normalizedTime / 0.12f);
            if (normalizedTime > 0.78f) return Mathf.Clamp01((1f - normalizedTime) / 0.22f);
            return 1f;
        }

        /// <summary>Décrit ce qui manque à cet asset, ou une chaîne vide s'il est complet.</summary>
        public string Diagnose()
        {
            if (!HasUsableAnimation) return "aucune variante d'animation (liste 'Variants' vide)";
            if (weightCurve == null || weightCurve.length < 2) return "courbe de melange vide";
            if (duration <= 0.02f) return "duree nulle";
            return string.Empty;
        }

        /// <summary>Choisit une variante sans répéter la précédente, tant qu'il y en a plusieurs.</summary>
        public int PickVariant(int previousIndex)
        {
            if (variants == null || variants.Count == 0) return -1;
            if (variants.Count == 1) return 0;

            int index = UnityEngine.Random.Range(0, variants.Count);
            if (index == previousIndex) index = (index + 1) % variants.Count;
            return index;
        }

        /// <summary>Interpole les clés d'une variante à l'instant normalisé demandé.</summary>
        public AttackPoseKey Sample(int variantIndex, float normalizedTime)
        {
            AttackPoseKey result = new AttackPoseKey();
            if (variants == null || variantIndex < 0 || variantIndex >= variants.Count) return result;

            List<AttackPoseKey> keys = variants[variantIndex].keys;
            if (keys == null || keys.Count == 0) return result;
            if (keys.Count == 1) return keys[0];

            normalizedTime = Mathf.Clamp01(normalizedTime);

            for (int i = 0; i < keys.Count - 1; i++)
            {
                AttackPoseKey a = keys[i];
                AttackPoseKey b = keys[i + 1];

                if (normalizedTime > b.time && i < keys.Count - 2) continue;

                float span = Mathf.Max(0.0001f, b.time - a.time);
                float t = Mathf.Clamp01((normalizedTime - a.time) / span);

                // Adoucissement aux extremites : un coup ne demarre ni ne s'arrete brutalement.
                t = t * t * (3f - 2f * t);

                result.time = normalizedTime;
                result.handPosition = Vector3.Lerp(a.handPosition, b.handPosition, t);
                result.handEuler = Vector3.Lerp(a.handEuler, b.handEuler, t);
                result.grip = Mathf.Lerp(a.grip, b.grip, t);
                result.bodyEuler = Vector3.Lerp(a.bodyEuler, b.bodyEuler, t);
                result.cameraOffset = Vector3.Lerp(a.cameraOffset, b.cameraOffset, t);
                result.cameraEuler = Vector3.Lerp(a.cameraEuler, b.cameraEuler, t);
                result.offHandPosition = Vector3.Lerp(a.offHandPosition, b.offHandPosition, t);
                result.offHandEuler = Vector3.Lerp(a.offHandEuler, b.offHandEuler, t);
                result.offHandWeight = Mathf.Lerp(a.offHandWeight, b.offHandWeight, t);
                return result;
            }

            return keys[keys.Count - 1];
        }

        /// <summary>Passe une pose de la main droite à la main gauche.</summary>
        public static HandPose Mirror(Vector3 position, Vector3 euler, bool mirrored)
        {
            if (!mirrored) return new HandPose(position, euler);

            return new HandPose(
                new Vector3(-position.x, position.y, position.z),
                new Vector3(euler.x, -euler.y, -euler.z));
        }

        public static Vector3 MirrorEuler(Vector3 euler, bool mirrored)
        {
            return mirrored ? new Vector3(euler.x, -euler.y, -euler.z) : euler;
        }

        public static Vector3 MirrorOffset(Vector3 offset, bool mirrored)
        {
            return mirrored ? new Vector3(-offset.x, offset.y, offset.z) : offset;
        }
    }
}
