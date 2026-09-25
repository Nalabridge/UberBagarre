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
        Foot = 1,

        /// <summary>
        /// La tête. Les mains agrippent (elles suivent le même chemin que pour un coup de
        /// poing), mais c'est une hitbox au FRONT qui porte le coup, emmenée par l'élan du buste
        /// et de la caméra.
        /// </summary>
        Head = 2
    }

    /// <summary>
    /// Situation dans laquelle un coup remplace le coup de base.
    ///
    /// C'est ce qui donne de la variété sans ajouter une seule touche : la même commande produit
    /// un coup différent selon ce que le joueur est en train de faire. Sprinter et frapper n'est
    /// pas frapper, et sauter et frapper encore moins.
    /// </summary>
    public enum AttackContext
    {
        Any = 0,
        Sprinting = 1,
        Airborne = 2,
        Sliding = 3,

        /// <summary>La cible est au sol. Permet un coup de finition.</summary>
        TargetDown = 4
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

        [Header("Charge")]
        [Tooltip("Maintenir la touche arme le coup au lieu de le relancer. Reserve aux coups " +
                 "LOURDS : sur un coup rapide, charger n'a pas de sens puisque sa valeur est " +
                 "justement de partir tout de suite.")]
        public bool chargeable;

        [Min(0.05f)]
        [Tooltip("Duree de maintien pour une charge complete.")]
        public float maxChargeTime = 0.75f;

        [Min(1f)]
        [Tooltip("Multiplicateur de degats a charge pleine.")]
        public float chargeDamageMultiplier = 2.1f;

        [Min(1f)] public float chargeImpactMultiplier = 2.4f;

        [Range(0f, 1f)]
        [Tooltip("Chance de chute ajoutee a charge pleine.")]
        public float chargeKnockdownBonus = 0.35f;

        [Header("Conditions de declenchement")]
        [Tooltip("Ce coup remplace le coup normal dans une situation precise. Aucun = coup de base.")]
        public AttackContext context = AttackContext.Any;

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
        public const int CurrentVersion = 6;

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
        public const string ChargeAsset = "A_ChargeEpaule";
        public const string DiveAsset = "A_CoupPlongeant";
        public const string SweepAsset = "A_Balayage";
        public const string StompAsset = "A_CoupDeGrace";
        public const string HeadbuttAsset = "A_CoupDeTete";
        public const string ShoveAsset = "A_Bousculade";

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

        /// <summary>
        /// Interpole les clés d'une variante à l'instant normalisé demandé.
        ///
        /// Courbe cubique MONOTONE (Steffen) entre les clés, et c'est ce qui change tout au
        /// ressenti. L'ancienne version adoucissait chaque segment séparément (smoothstep de clé
        /// à clé) : la vitesse retombait à zéro sur CHAQUE clé, et un coup de cinq clés devenait
        /// cinq petits mouvements qui s'arrêtent — exactement la démarche d'un robot. Ici la
        /// vitesse traverse les clés : le poing ne ralentit que là où le geste s'inverse
        /// réellement (fin d'armement, extension maximale), jamais au milieu d'une accélération.
        /// Monotone : la courbe ne dépasse jamais les clés, donc pas de boucle parasite entre
        /// deux poses rapprochées.
        /// </summary>
        public AttackPoseKey Sample(int variantIndex, float normalizedTime)
        {
            AttackPoseKey result = new AttackPoseKey();
            if (variants == null || variantIndex < 0 || variantIndex >= variants.Count) return result;

            List<AttackPoseKey> keys = variants[variantIndex].keys;
            if (keys == null || keys.Count == 0) return result;
            if (keys.Count == 1) return keys[0];

            normalizedTime = Mathf.Clamp01(normalizedTime);

            int i = 0;
            while (i < keys.Count - 2 && normalizedTime > keys[i + 1].time) i++;

            AttackPoseKey a = keys[i];
            AttackPoseKey b = keys[i + 1];
            float h = Mathf.Max(0.0001f, b.time - a.time);
            float u = Mathf.Clamp01((normalizedTime - a.time) / h);

            AttackPoseKey prev = i > 0 ? keys[i - 1] : a;
            AttackPoseKey next = i + 2 < keys.Count ? keys[i + 2] : b;
            bool hasPrev = i > 0;
            bool hasNext = i + 2 < keys.Count;

            float h0 = Mathf.Max(0.0001f, a.time - prev.time);
            float h2 = Mathf.Max(0.0001f, next.time - b.time);

            result.time = normalizedTime;
            result.handPosition = Cubic(prev.handPosition, a.handPosition, b.handPosition, next.handPosition, h0, h, h2, u, hasPrev, hasNext);
            result.handEuler = Cubic(prev.handEuler, a.handEuler, b.handEuler, next.handEuler, h0, h, h2, u, hasPrev, hasNext);
            result.bodyEuler = Cubic(prev.bodyEuler, a.bodyEuler, b.bodyEuler, next.bodyEuler, h0, h, h2, u, hasPrev, hasNext);
            result.cameraOffset = Cubic(prev.cameraOffset, a.cameraOffset, b.cameraOffset, next.cameraOffset, h0, h, h2, u, hasPrev, hasNext);
            result.cameraEuler = Cubic(prev.cameraEuler, a.cameraEuler, b.cameraEuler, next.cameraEuler, h0, h, h2, u, hasPrev, hasNext);
            result.offHandPosition = Cubic(prev.offHandPosition, a.offHandPosition, b.offHandPosition, next.offHandPosition, h0, h, h2, u, hasPrev, hasNext);
            result.offHandEuler = Cubic(prev.offHandEuler, a.offHandEuler, b.offHandEuler, next.offHandEuler, h0, h, h2, u, hasPrev, hasNext);
            result.grip = Mathf.Clamp01(Cubic(prev.grip, a.grip, b.grip, next.grip, h0, h, h2, u, hasPrev, hasNext));
            result.offHandWeight = Mathf.Clamp01(Cubic(prev.offHandWeight, a.offHandWeight, b.offHandWeight, next.offHandWeight, h0, h, h2, u, hasPrev, hasNext));
            return result;
        }

        /// <summary>Instant de l'impact prévu : le milieu de la fenêtre, un peu avant.</summary>
        public float ImpactTime
        {
            get { return Mathf.Lerp(hitWindowStart, hitWindowEnd, 0.42f); }
        }

        private static Vector3 Cubic(Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, float h0, float h1, float h2,
            float u, bool hasPrev, bool hasNext)
        {
            return new Vector3(
                Cubic(p0.x, p1.x, p2.x, p3.x, h0, h1, h2, u, hasPrev, hasNext),
                Cubic(p0.y, p1.y, p2.y, p3.y, h0, h1, h2, u, hasPrev, hasNext),
                Cubic(p0.z, p1.z, p2.z, p3.z, h0, h1, h2, u, hasPrev, hasNext));
        }

        /// <summary>Hermite entre y1 et y2, tangentes de Steffen (monotones), nulles aux extrémités.</summary>
        private static float Cubic(float y0, float y1, float y2, float y3, float h0, float h1, float h2,
            float u, bool hasPrev, bool hasNext)
        {
            float d1 = (y2 - y1) / h1;
            float m1 = hasPrev ? SteffenSlope((y1 - y0) / h0, d1, h0, h1) : 0f;
            float m2 = hasNext ? SteffenSlope(d1, (y3 - y2) / h2, h1, h2) : 0f;

            float u2 = u * u;
            float u3 = u2 * u;
            return (2f * u3 - 3f * u2 + 1f) * y1 + (u3 - 2f * u2 + u) * h1 * m1 +
                   (-2f * u3 + 3f * u2) * y2 + (u3 - u2) * h1 * m2;
        }

        private static float SteffenSlope(float left, float right, float hLeft, float hRight)
        {
            if (left * right <= 0f) return 0f;

            float p = (left * hRight + right * hLeft) / (hLeft + hRight);
            float limit = 2f * Mathf.Min(Mathf.Abs(left), Mathf.Abs(right));
            return Mathf.Sign(left) * Mathf.Min(Mathf.Abs(p), limit);
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
