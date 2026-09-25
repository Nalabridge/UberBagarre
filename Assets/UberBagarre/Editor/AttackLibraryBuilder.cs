using System.Collections.Generic;
using UberBagarre.Combat;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Crée les coups de base sous forme d'assets.
    ///
    /// Une fois créés, ce sont TES assets : tu peux modifier chaque valeur et chaque pose-clé
    /// dans l'Inspector sans toucher au code. Le générateur ne les écrase jamais — il faut
    /// passer par la commande de menu dédiée, qui prévient avant de remplacer.
    ///
    /// Toutes les poses sont écrites pour le CÔTÉ DROIT ; jouées à gauche elles sont mirrorées.
    /// Les coups de poing sont exprimés dans le repère de visée (à hauteur d'yeux), les coups de
    /// pied dans le repère du personnage (origine au sol) : une hauteur de 0,6 y veut dire
    /// « 60 cm au-dessus du sol », ce qui est la seule façon de régler un coup de pied.
    /// </summary>
    public static class AttackLibraryBuilder
    {
        /// <summary>
        /// Les coups sont dans un dossier Resources, et c'est délibéré.
        ///
        /// Une référence de scène peut se perdre : scène non régénérée après une mise à jour,
        /// câblage éditeur échoué, asset recréé avec un nouvel identifiant. Le symptôme est
        /// alors un jeu silencieusement inerte. Depuis Resources, le jeu peut retrouver ses
        /// coups tout seul à l'exécution — la référence de scène devient une optimisation,
        /// plus un point de rupture.
        /// </summary>
        public const string AttacksFolder = "Assets/UberBagarre/Resources/Attaques";

        /// <summary>Chemin de chargement à l'exécution, relatif au dossier Resources.</summary>
        public const string ResourcePath = "Attaques";

        /// <summary>
        /// Le répertoire complet du jeu, renvoyé en un seul objet.
        ///
        /// Cinq paramètres <c>out</c> devenaient illisibles, et chaque nouveau coup obligeait à
        /// modifier toutes les signatures de la chaîne. Ici, ajouter un coude ne touche qu'à
        /// cette classe.
        /// </summary>
        public class Library
        {
            public AttackData Straight;
            public AttackData Hook;
            public AttackData Uppercut;
            public AttackData Kick;
            public AttackData LowKick;

            /// <summary>Coups contextuels : ils remplacent le coup de base dans une situation donnée.</summary>
            public AttackData Charge;
            public AttackData Dive;
            public AttackData Sweep;
            public AttackData Stomp;

            /// <summary>Coups de corps : ils ne passent ni par un poing ni par un pied.</summary>
            public AttackData Headbutt;
            public AttackData Shove;

            /// <summary>Tous les coups, dans l'ordre d'apprentissage.</summary>
            public AttackData[] All
            {
                get
                {
                    return new[]
                    {
                        Straight, Hook, Uppercut, Kick, LowKick, Charge, Dive, Sweep, Stomp, Headbutt, Shove
                    };
                }
            }
        }

        [MenuItem("Uber Bagarre/4 - Regenerer les coups par defaut", false, 40)]
        public static void RegenerateFromMenu()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Regenerer les coups ?",
                "Les assets d'attaque (Direct, Crochet, Uppercut, Coups de pied, Coup de tete, Bousculade...) " +
                "vont etre REMPLACES par les valeurs par defaut.\n\n" +
                "Toutes tes modifications de degats, de timings et de poses seront perdues.",
                "Remplacer", "Annuler");

            if (!confirm) return;

            Library library = BuildAll(true);
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();

            AttackData[] all = library.All;
            string report = "[UberBagarre] Coups regeneres dans " + AttacksFolder;

            for (int i = 0; i < all.Length; i++)
            {
                report += "\n  " + (all[i] != null ? all[i].displayName : "???") + " : " + Describe(all[i]);
            }

            Debug.Log(report);
        }

        /// <summary>Emplacement historique des coups, avant leur passage dans Resources.</summary>
        private const string LegacyAttacksFolder = "Assets/UberBagarre/Combat";

        public static Library BuildAll(bool overwrite)
        {
            RemoveLegacyFolder();
            EditorBuildUtility.EnsureFolder(AttacksFolder);

            Library library = new Library();
            library.Straight = GetOrCreate(AttackData.StraightAsset, overwrite, ConfigureStraight);
            library.Hook = GetOrCreate(AttackData.HookAsset, overwrite, ConfigureHook);
            library.Uppercut = GetOrCreate(AttackData.UppercutAsset, overwrite, ConfigureUppercut);
            library.Kick = GetOrCreate(AttackData.KickAsset, overwrite, ConfigureKick);
            library.LowKick = GetOrCreate(AttackData.LowKickAsset, overwrite, ConfigureLowKick);

            library.Charge = GetOrCreate(AttackData.ChargeAsset, overwrite, ConfigureShoulderCharge);
            library.Dive = GetOrCreate(AttackData.DiveAsset, overwrite, ConfigureDive);
            library.Sweep = GetOrCreate(AttackData.SweepAsset, overwrite, ConfigureSweep);
            library.Stomp = GetOrCreate(AttackData.StompAsset, overwrite, ConfigureStomp);

            library.Headbutt = GetOrCreate(AttackData.HeadbuttAsset, overwrite, ConfigureHeadbutt);
            library.Shove = GetOrCreate(AttackData.ShoveAsset, overwrite, ConfigureShove);

            return library;
        }

        /// <summary>
        /// Supprime l'ancien dossier d'attaques.
        ///
        /// Deux copies d'un même coup, c'est la garantie de régler l'une et de jouer l'autre.
        /// Et supprimer les anciens assets fait tomber à zéro les références de scène qui
        /// pointaient dessus, ce qui déclenche le rattrapage depuis Resources — le jeu se
        /// répare donc même sans régénérer la scène.
        /// </summary>
        private static void RemoveLegacyFolder()
        {
            if (!AssetDatabase.IsValidFolder(LegacyAttacksFolder)) return;

            if (AssetDatabase.DeleteAsset(LegacyAttacksFolder))
            {
                Debug.LogWarning("[UberBagarre] Ancien dossier d'attaques supprime (" + LegacyAttacksFolder +
                                 "). Les coups vivent desormais dans " + AttacksFolder + ".");
            }
        }

        private delegate void Configure(AttackData attack);

        private static AttackData GetOrCreate(string assetName, bool overwrite, Configure configure)
        {
            string path = AttacksFolder + "/" + assetName + ".asset";
            AttackData asset = AssetDatabase.LoadAssetAtPath<AttackData>(path);

            if (asset == null)
            {
                asset = ScriptableObject.CreateInstance<AttackData>();
                configure(asset);
                AssetDatabase.CreateAsset(asset, path);

                if (asset == null)
                {
                    Debug.LogError("[UberBagarre] Impossible de creer l'asset " + path +
                                   ". Verifie que le dossier existe et n'est pas en lecture seule.");
                    return null;
                }
            }
            else
            {
                bool outdated = asset.dataVersion < AttackData.CurrentVersion;
                string problem = asset.Diagnose();

                // Trois raisons de reecrire un asset existant, et une seule de ne pas le faire.
                //
                // On ne le reecrit pas quand il est a jour et complet : ses valeurs sont alors
                // celles que l'utilisateur a reglees a la main, et les effacer serait pire que
                // tout. Mais un asset cree par une VERSION ANTERIEURE du code est un autre cas :
                // affiner un timing ici n'avait aucun effet pour qui avait deja ouvert le projet
                // une fois, et le symptome etait "tu n'as pas fait ce que j'ai demande".
                if (!overwrite && !outdated && string.IsNullOrEmpty(problem)) return asset;

                if (!overwrite)
                {
                    Debug.Log("[UberBagarre] '" + assetName + "' mis a jour (" +
                              (outdated ? "cree par une version anterieure du code" : problem) + ").", asset);
                }

                configure(asset);
                EditorUtility.SetDirty(asset);
            }

            // Pose APRES la configuration, donc impossible a oublier dans un Configure.
            asset.dataVersion = AttackData.CurrentVersion;
            EditorUtility.SetDirty(asset);

            return asset;
        }

        // ------------------------------------------------------------------ repères de pose

        // Garde au repos, recopiee de FirstPersonHands : une pose de coup qui ne part pas
        // exactement de la garde produit un saut visible a la premiere image.
        //
        // Garde haute de bagarreur, pour un bras reel de 52 cm (epaule 23 cm sous les yeux et
        // 10 cm en arriere) : poings a hauteur du menton, jointures vers le haut, coudes bas
        // qui couvrent les cotes. L'ancienne garde, a 30-33 cm devant les yeux, tendait deja
        // presque le bras : le direct n'avait plus de course.
        private static readonly Vector3 GuardRight = new Vector3(0.130f, -0.190f, 0.190f);
        private static readonly Vector3 GuardRightEuler = new Vector3(-50f, -16f, -76f);
        private static readonly Vector3 GuardLeft = new Vector3(-0.115f, -0.155f, 0.245f);
        private static readonly Vector3 GuardLeftEuler = new Vector3(-50f, 18f, 72f);

        // La main libre remonte proteger le menton pendant qu'on frappe.
        private static readonly Vector3 CoverLeft = new Vector3(-0.080f, -0.120f, 0.205f);
        private static readonly Vector3 CoverLeftEuler = new Vector3(-60f, 30f, 80f);

        // Pied droit au repos, dans le repère du personnage (origine au sol), aligné sur
        // ProceduralLocomotion._idleRightFoot + la hauteur de cheville du corps.
        private static readonly Vector3 StanceFoot = new Vector3(0.16f, 0.071f, -0.17f);

        // ------------------------------------------------------------------ coups de poing
        //
        // Chaque coup est ecrit en cinq temps, et la courbe continue de AttackData.Sample
        // les enchaine sans jamais s'arreter entre deux :
        //   ARMEMENT   le poing se charge (recul, ouverture, buste qui se tord a l'oppose)
        //   DEPART     acceleration : c'est le buste qui lance, le bras suit
        //   IMPACT     le poing se visse (rotation a plat), la vue plonge avec lui
        //   ACCOMPAGNEMENT  le geste continue un peu, la vue s'enroule
        //   RETOUR     plus lent que le depart, le poing revient couvrir
        //
        // Les positions d'impact sont celles d'un adversaire « type » a bout de bras ; en jeu,
        // l'executeur les guide vers la vraie cible (menton, plexus, cotes).

        /// <summary>
        /// Direct. Le coup qui doit rester utilisable en permanence : court, peu cher, repos
        /// quasi nul. La main s'OUVRE a l'armement et se SERRE juste avant l'impact.
        /// </summary>
        private static void ConfigureStraight(AttackData a)
        {
            a.displayName = "Direct";
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Alternate;
            a.isHeavy = false;
            a.knockdownChance = 0f;
            a.duration = 0.30f;
            a.cooldown = 0.02f;
            a.hitWindowStart = 0.28f;
            a.hitWindowEnd = 0.60f;
            a.comboCancelAt = 0.62f;
            a.hitRadius = 0.075f;
            a.damage = 9f;
            a.impactForce = 3.6f;
            a.staminaCost = 5f;
            a.shakeIntensity = 0.050f;
            a.shakeDuration = 0.09f;
            a.hitStopDuration = 0.02f;
            a.weightCurve = PunchWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01 - jab",
                    Rest(0.00f),
                    Key(0.10f, new Vector3(0.142f, -0.202f, 0.165f), new Vector3(-40f, -18f, -80f), 0.75f,
                        new Vector3(1f, 8f, -1f), new Vector3(0f, -0.004f, -0.012f), new Vector3(0.8f, 1.2f, -0.4f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.30f, new Vector3(0.100f, -0.120f, 0.330f), new Vector3(-12f, -8f, -45f), 1f,
                        new Vector3(2f, -8f, 1f), new Vector3(0f, -0.002f, 0.012f), new Vector3(-0.8f, -1.6f, 0.8f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.42f, new Vector3(0.050f, -0.070f, 0.430f), new Vector3(0f, -2f, -8f), 1f,
                        new Vector3(3f, -16f, 2f), new Vector3(0f, -0.006f, 0.034f), new Vector3(-1.6f, -3.2f, 1.8f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.52f, new Vector3(0.040f, -0.064f, 0.448f), new Vector3(1f, -1f, -4f), 1f,
                        new Vector3(3f, -17f, 2f), new Vector3(0f, -0.006f, 0.036f), new Vector3(-1.4f, -3.4f, 2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.70f, new Vector3(0.120f, -0.150f, 0.270f), new Vector3(-35f, -12f, -60f), 0.95f,
                        new Vector3(1f, -6f, 0.5f), new Vector3(0f, 0f, 0.010f), new Vector3(-0.3f, -1f, 0.4f),
                        CoverLeft, CoverLeftEuler),
                    Rest(1.00f)),

                Variant("02 - cross",
                    Rest(0.00f),
                    Key(0.12f, new Vector3(0.158f, -0.212f, 0.150f), new Vector3(-38f, -20f, -82f), 0.7f,
                        new Vector3(2f, 14f, -2f), new Vector3(0.004f, -0.006f, -0.016f), new Vector3(1f, 2.2f, -0.8f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.32f, new Vector3(0.090f, -0.118f, 0.340f), new Vector3(-10f, -6f, -40f), 1f,
                        new Vector3(3f, -12f, 2f), new Vector3(-0.004f, -0.004f, 0.020f), new Vector3(-1f, -2.4f, 1.4f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.44f, new Vector3(0.036f, -0.074f, 0.440f), new Vector3(0f, 0f, -4f), 1f,
                        new Vector3(4f, -24f, 3f), new Vector3(-0.010f, -0.008f, 0.045f), new Vector3(-2f, -4.4f, 2.6f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.56f, new Vector3(0.026f, -0.068f, 0.456f), new Vector3(1f, 1f, 0f), 1f,
                        new Vector3(4f, -26f, 3f), new Vector3(-0.012f, -0.008f, 0.046f), new Vector3(-1.8f, -4.8f, 2.8f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.74f, new Vector3(0.118f, -0.152f, 0.265f), new Vector3(-34f, -12f, -62f), 0.95f,
                        new Vector3(1f, -8f, 1f), new Vector3(0f, 0f, 0.010f), new Vector3(-0.4f, -1.2f, 0.5f),
                        CoverLeft, CoverLeftEuler),
                    Rest(1.00f)),

                // Direct au corps : on plie les jambes, le poing part a l'horizontale dans le
                // plexus, la vue plonge avec lui.
                Variant("03 - direct au corps",
                    Rest(0.00f),
                    Key(0.12f, new Vector3(0.148f, -0.232f, 0.160f), new Vector3(-30f, -16f, -80f), 0.7f,
                        new Vector3(6f, 10f, -1f), new Vector3(0f, -0.018f, -0.010f), new Vector3(2.4f, 1.4f, -0.4f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.32f, new Vector3(0.092f, -0.230f, 0.330f), new Vector3(4f, -8f, -40f), 1f,
                        new Vector3(10f, -10f, 1f), new Vector3(0f, -0.030f, 0.016f), new Vector3(3.6f, -1.8f, 1f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.44f, new Vector3(0.050f, -0.270f, 0.420f), new Vector3(12f, -2f, -6f), 1f,
                        new Vector3(13f, -18f, 2f), new Vector3(0f, -0.042f, 0.032f), new Vector3(5f, -3.2f, 1.8f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.56f, new Vector3(0.044f, -0.272f, 0.436f), new Vector3(13f, -1f, -2f), 1f,
                        new Vector3(13f, -19f, 2f), new Vector3(0f, -0.042f, 0.034f), new Vector3(5f, -3.4f, 2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.74f, new Vector3(0.120f, -0.200f, 0.260f), new Vector3(-26f, -12f, -62f), 0.95f,
                        new Vector3(4f, -6f, 0.5f), new Vector3(0f, -0.010f, 0.008f), new Vector3(1.2f, -1f, 0.4f),
                        CoverLeft, CoverLeftEuler),
                    Rest(1.00f))
            };
        }

        /// <summary>
        /// Crochet. Le poing part de cote et traverse : la puissance vient de la rotation du
        /// buste, et la vue s'enroule avec lui.
        /// </summary>
        private static void ConfigureHook(AttackData a)
        {
            a.displayName = "Crochet";
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.knockdownChance = 0.06f;
            a.duration = 0.38f;
            a.cooldown = 0.05f;
            a.hitWindowStart = 0.30f;
            a.hitWindowEnd = 0.64f;
            a.comboCancelAt = 0.70f;
            a.hitRadius = 0.085f;
            a.damage = 15f;
            a.impactForce = 6.5f;
            a.staminaCost = 11f;
            a.shakeIntensity = 0.085f;
            a.shakeDuration = 0.15f;
            a.hitStopDuration = 0.035f;
            a.weightCurve = PunchWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01 - a la tete",
                    Rest(0.00f),
                    Key(0.14f, new Vector3(0.262f, -0.150f, 0.170f), new Vector3(-15f, -60f, -75f), 0.75f,
                        new Vector3(0f, 16f, -2f), new Vector3(0.012f, -0.004f, -0.012f), new Vector3(0.4f, 2.6f, -1.2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.34f, new Vector3(0.180f, -0.092f, 0.360f), new Vector3(-5f, -80f, -40f), 1f,
                        new Vector3(1f, -10f, 2f), new Vector3(-0.006f, -0.004f, 0.014f), new Vector3(-0.8f, -2.2f, 2.2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.44f, new Vector3(0.020f, -0.080f, 0.400f), new Vector3(0f, -95f, -10f), 1f,
                        new Vector3(2f, -26f, 3f), new Vector3(-0.020f, -0.006f, 0.024f), new Vector3(-1.2f, -5f, 4f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.56f, new Vector3(-0.080f, -0.090f, 0.360f), new Vector3(0f, -105f, -5f), 1f,
                        new Vector3(2f, -31f, 3f), new Vector3(-0.026f, -0.006f, 0.022f), new Vector3(-1f, -6f, 4.2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.78f, new Vector3(0.100f, -0.150f, 0.260f), new Vector3(-35f, -40f, -60f), 0.95f,
                        new Vector3(1f, -10f, 1f), new Vector3(-0.006f, 0f, 0.006f), new Vector3(-0.2f, -1.6f, 1f),
                        CoverLeft, CoverLeftEuler),
                    Rest(1.00f)),

                Variant("02 - au corps",
                    Rest(0.00f),
                    Key(0.16f, new Vector3(0.262f, -0.250f, 0.160f), new Vector3(10f, -55f, -78f), 0.75f,
                        new Vector3(8f, 16f, -3f), new Vector3(0.012f, -0.022f, -0.010f), new Vector3(3f, 2.4f, -1.2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.36f, new Vector3(0.170f, -0.270f, 0.340f), new Vector3(18f, -78f, -40f), 1f,
                        new Vector3(12f, -10f, 2f), new Vector3(-0.006f, -0.036f, 0.012f), new Vector3(4.4f, -2.4f, 2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.46f, new Vector3(0.020f, -0.300f, 0.380f), new Vector3(22f, -92f, -12f), 1f,
                        new Vector3(14f, -24f, 3f), new Vector3(-0.018f, -0.044f, 0.020f), new Vector3(5.6f, -4.6f, 3.4f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.58f, new Vector3(-0.070f, -0.300f, 0.350f), new Vector3(22f, -100f, -6f), 1f,
                        new Vector3(14f, -28f, 3f), new Vector3(-0.024f, -0.044f, 0.018f), new Vector3(5.4f, -5.4f, 3.6f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.80f, new Vector3(0.100f, -0.200f, 0.250f), new Vector3(-26f, -40f, -62f), 0.95f,
                        new Vector3(4f, -9f, 1f), new Vector3(-0.006f, -0.010f, 0.006f), new Vector3(1.2f, -1.4f, 1f),
                        CoverLeft, CoverLeftEuler),
                    Rest(1.00f))
            };
        }

        /// <summary>
        /// Uppercut. On plonge, puis tout le corps se deplie : le poing remonte a la verticale,
        /// et la vue se releve avec lui. Le coup le plus lent et le plus lourd.
        /// </summary>
        private static void ConfigureUppercut(AttackData a)
        {
            a.displayName = "Uppercut";
            a.chargeable = true;
            a.maxChargeTime = 0.70f;
            a.chargeDamageMultiplier = 2.2f;
            a.chargeImpactMultiplier = 2.6f;
            a.chargeKnockdownBonus = 0.45f;
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.knockdownChance = 0.12f;
            a.duration = 0.42f;
            a.cooldown = 0.06f;
            a.hitWindowStart = 0.34f;
            a.hitWindowEnd = 0.66f;
            a.comboCancelAt = 0.72f;
            a.hitRadius = 0.085f;
            a.damage = 17f;
            a.impactForce = 7.5f;
            a.staminaCost = 13f;
            a.shakeIntensity = 0.095f;
            a.shakeDuration = 0.17f;
            a.hitStopDuration = 0.04f;
            a.weightCurve = PunchWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01 - au menton",
                    Rest(0.00f),
                    Key(0.18f, new Vector3(0.150f, -0.340f, 0.200f), new Vector3(20f, -15f, -100f), 0.75f,
                        new Vector3(10f, 8f, 0f), new Vector3(0f, -0.030f, -0.006f), new Vector3(3.4f, 0.8f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.40f, new Vector3(0.100f, -0.160f, 0.330f), new Vector3(-30f, -10f, -130f), 1f,
                        new Vector3(-4f, -8f, 1f), new Vector3(0f, 0.004f, 0.012f), new Vector3(-2f, -1.4f, 0.8f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.47f, new Vector3(0.080f, -0.040f, 0.360f), new Vector3(-55f, -6f, -150f), 1f,
                        new Vector3(-12f, -12f, 1f), new Vector3(0f, 0.022f, 0.018f), new Vector3(-5.4f, -2f, 1.2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.60f, new Vector3(0.080f, 0.040f, 0.340f), new Vector3(-62f, -5f, -155f), 1f,
                        new Vector3(-14f, -13f, 1f), new Vector3(0f, 0.028f, 0.016f), new Vector3(-6f, -2.2f, 1.2f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.80f, new Vector3(0.120f, -0.140f, 0.240f), new Vector3(-40f, -12f, -100f), 0.95f,
                        new Vector3(-3f, -4f, 0f), new Vector3(0f, 0.004f, 0.004f), new Vector3(-1f, -0.4f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Rest(1.00f)),

                // Uppercut court au plexus : trajectoire plus ramassee, moins de remontee.
                Variant("02 - au plexus",
                    Rest(0.00f),
                    Key(0.16f, new Vector3(0.150f, -0.360f, 0.190f), new Vector3(18f, -14f, -100f), 0.75f,
                        new Vector3(9f, 7f, 0f), new Vector3(0f, -0.030f, -0.006f), new Vector3(3.6f, 0.6f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.40f, new Vector3(0.100f, -0.250f, 0.330f), new Vector3(-20f, -8f, -130f), 1f,
                        new Vector3(4f, -8f, 1f), new Vector3(0f, -0.018f, 0.014f), new Vector3(2.4f, -1.2f, 0.6f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.48f, new Vector3(0.080f, -0.200f, 0.370f), new Vector3(-40f, -6f, -145f), 1f,
                        new Vector3(2f, -11f, 1f), new Vector3(0f, -0.012f, 0.020f), new Vector3(1.8f, -1.8f, 1f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.60f, new Vector3(0.080f, -0.160f, 0.360f), new Vector3(-48f, -6f, -150f), 1f,
                        new Vector3(0f, -12f, 1f), new Vector3(0f, -0.008f, 0.018f), new Vector3(1.2f, -2f, 1f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.80f, new Vector3(0.120f, -0.180f, 0.240f), new Vector3(-40f, -12f, -100f), 0.95f,
                        new Vector3(2f, -4f, 0f), new Vector3(0f, -0.004f, 0.004f), new Vector3(0.4f, -0.4f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Rest(1.00f))
            };
        }

        // ------------------------------------------------------------------ coups de pied

        /// <summary>
        /// Coup de pied de face. Le genou monte, la jambe se detend, le buste part en arriere pour
        /// compenser — sans ce contrepoids, un coup de pied ressemble a une jambe qui s'agite
        /// toute seule. Le bras oppose s'ouvre : c'est lui qui tient l'equilibre.
        /// </summary>
        private static void ConfigureKick(AttackData a)
        {
            a.displayName = "Coup de pied";
            a.chargeable = true;
            a.maxChargeTime = 0.80f;
            a.chargeDamageMultiplier = 2.0f;
            a.chargeImpactMultiplier = 2.8f;
            a.chargeKnockdownBonus = 0.40f;
            a.limb = AttackLimb.Foot;
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.knockdownChance = 0.25f;
            a.duration = 0.44f;
            a.cooldown = 0.10f;
            a.hitWindowStart = 0.32f;
            a.hitWindowEnd = 0.62f;
            a.comboCancelAt = 0.72f;
            a.hitRadius = 0.19f;
            a.damage = 19f;
            a.impactForce = 9.5f;
            a.staminaCost = 17f;
            a.shakeIntensity = 0.105f;
            a.shakeDuration = 0.19f;
            a.hitStopDuration = 0.032f;
            a.weightCurve = FootWeightCurve();

            a.variants = new List<AttackVariant>
            {
                // Le pied arrive a 0,97 m du sol, franchement AU-DESSUS du bassin : le coup porte
                // au corps. Viser 10 cm plus bas le faisait basculer dans la zone "jambes".
                Variant("01 - de face",
                    FootRest(0.00f),
                    Key(0.18f, new Vector3(0.170f, 0.450f, 0.110f), new Vector3(-34f, 0f, 0f), 1f,
                        new Vector3(-4f, 4f, 0f), new Vector3(0f, -0.012f, -0.008f), new Vector3(1.8f, 0f, 0f),
                        new Vector3(-0.232f, -0.215f, 0.205f), new Vector3(6f, 34f, 59f)),
                    Key(0.42f, new Vector3(0.120f, 0.900f, 0.400f), new Vector3(-26f, 0f, 0f), 1f,
                        new Vector3(-7f, -3f, 0f), new Vector3(0f, 0.009f, 0.013f), new Vector3(-2f, 1f, 0f),
                        new Vector3(-0.268f, -0.248f, 0.118f), new Vector3(10f, 42f, 65f)),
                    Key(0.52f, new Vector3(0.110f, 0.970f, 0.480f), new Vector3(-22f, 0f, 0f), 1f,
                        new Vector3(-8f, -4f, 0f), new Vector3(0f, 0.012f, 0.017f), new Vector3(-2.4f, 1.2f, 0f),
                        new Vector3(-0.272f, -0.252f, 0.110f), new Vector3(11f, 44f, 66f)),
                    Key(0.74f, new Vector3(0.150f, 0.470f, 0.220f), new Vector3(-16f, 0f, 0f), 1f,
                        new Vector3(-3f, -1f, 0f), new Vector3(0f, 0.003f, 0.004f), Vector3.zero,
                        new Vector3(-0.206f, -0.180f, 0.262f), new Vector3(0f, 26f, 54f)),
                    FootRest(1.00f)),

                // Coup de pied circulaire : le pied arrive de cote, le buste accompagne.
                Variant("02 - circulaire",
                    FootRest(0.00f),
                    Key(0.18f, new Vector3(0.330f, 0.420f, 0.030f), new Vector3(-20f, 34f, 0f), 1f,
                        new Vector3(-3f, 12f, -3f), new Vector3(0.010f, -0.010f, -0.007f), new Vector3(1.5f, 2.4f, -1.2f),
                        new Vector3(-0.236f, -0.200f, 0.190f), new Vector3(6f, 36f, 60f)),
                    Key(0.42f, new Vector3(0.150f, 0.860f, 0.390f), new Vector3(-18f, -10f, 0f), 1f,
                        new Vector3(-6f, -12f, 3f), new Vector3(-0.005f, 0.008f, 0.012f), new Vector3(-1.8f, -2.6f, 1.6f),
                        new Vector3(-0.270f, -0.245f, 0.120f), new Vector3(10f, 44f, 65f)),
                    Key(0.52f, new Vector3(-0.020f, 0.900f, 0.450f), new Vector3(-16f, -34f, 0f), 1f,
                        new Vector3(-7f, -20f, 4f), new Vector3(-0.013f, 0.010f, 0.015f), new Vector3(-2.2f, -4.2f, 2.6f),
                        new Vector3(-0.274f, -0.250f, 0.112f), new Vector3(11f, 46f, 67f)),
                    Key(0.74f, new Vector3(0.140f, 0.450f, 0.210f), new Vector3(-14f, -8f, 0f), 1f,
                        new Vector3(-3f, -6f, 1f), new Vector3(0f, 0.003f, 0.003f), Vector3.zero,
                        new Vector3(-0.206f, -0.180f, 0.262f), new Vector3(0f, 26f, 54f)),
                    FootRest(1.00f))
            };
        }

        /// <summary>
        /// Coup de pied bas. Il ne fait presque pas de degats — son interet est ailleurs : il part
        /// dans les jambes, et c'est le coup qui fait TOMBER.
        ///
        /// Les cibles de cheville restent sous 0,82 m de la hanche. La jambe mesure 0,86 m : viser
        /// plus loin la tendrait a l'extreme, l'IK saturerait, et le balayage se figerait en milieu
        /// de course. C'est un calcul, pas un reglage a l'oeil.
        /// </summary>
        private static void ConfigureLowKick(AttackData a)
        {
            a.displayName = "Coup de pied bas";
            a.chargeable = true;
            a.maxChargeTime = 0.60f;
            a.chargeDamageMultiplier = 1.7f;
            a.chargeImpactMultiplier = 2.0f;
            a.chargeKnockdownBonus = 0.30f;
            a.limb = AttackLimb.Foot;
            a.hand = AttackHand.Alternate;
            a.isHeavy = false;
            a.knockdownChance = 0.55f;
            a.duration = 0.34f;
            a.cooldown = 0.07f;
            a.hitWindowStart = 0.28f;
            a.hitWindowEnd = 0.58f;
            a.comboCancelAt = 0.66f;
            a.hitRadius = 0.20f;
            a.damage = 11f;
            a.impactForce = 7f;
            a.staminaCost = 12f;
            a.shakeIntensity = 0.070f;
            a.shakeDuration = 0.13f;
            a.hitStopDuration = 0.026f;
            a.weightCurve = FootWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01 - balayage",
                    FootRest(0.00f),
                    Key(0.16f, new Vector3(0.300f, 0.160f, -0.100f), new Vector3(0f, 20f, 0f), 1f,
                        new Vector3(0f, 12f, -2f), new Vector3(0.009f, -0.006f, -0.006f), new Vector3(0.8f, 2.2f, -1f),
                        new Vector3(-0.136f, -0.110f, 0.318f), new Vector3(-8f, 24f, 53f)),
                    Key(0.42f, new Vector3(0.050f, 0.200f, 0.400f), new Vector3(0f, -28f, 0f), 1f,
                        new Vector3(0f, -16f, 3f), new Vector3(-0.005f, -0.004f, 0.011f), new Vector3(0.9f, 2.2f, -1f),
                        new Vector3(-0.178f, -0.160f, 0.268f), new Vector3(-2f, 30f, 57f)),
                    Key(0.54f, new Vector3(-0.120f, 0.220f, 0.380f), new Vector3(0f, -42f, 0f), 1f,
                        new Vector3(0f, -20f, 4f), new Vector3(-0.010f, -0.004f, 0.009f), new Vector3(1.1f, 2.8f, -1.5f),
                        new Vector3(-0.190f, -0.172f, 0.252f), new Vector3(0f, 32f, 58f)),
                    Key(0.76f, new Vector3(0.080f, 0.130f, 0.160f), new Vector3(0f, -12f, 0f), 1f,
                        new Vector3(0f, -7f, 1f), Vector3.zero, Vector3.zero,
                        new Vector3(-0.160f, -0.140f, 0.300f), new Vector3(-5f, 26f, 54f)),
                    FootRest(1.00f)),

                // Coup direct dans la cuisse : plus sec, moins ample, meme consequence.
                Variant("02 - dans la cuisse",
                    FootRest(0.00f),
                    Key(0.16f, new Vector3(0.190f, 0.300f, -0.060f), new Vector3(-22f, 6f, 0f), 1f,
                        new Vector3(-2f, 7f, 0f), new Vector3(0f, -0.009f, -0.007f), new Vector3(1.4f, 0.8f, 0f),
                        new Vector3(-0.150f, -0.124f, 0.310f), new Vector3(-6f, 26f, 54f)),
                    Key(0.42f, new Vector3(0.140f, 0.340f, 0.400f), new Vector3(-14f, -6f, 0f), 1f,
                        new Vector3(-3f, -9f, 2f), new Vector3(0f, 0.006f, 0.012f), new Vector3(-1.2f, 1.2f, 0f),
                        new Vector3(-0.186f, -0.168f, 0.258f), new Vector3(-1f, 31f, 57f)),
                    Key(0.54f, new Vector3(0.120f, 0.330f, 0.470f), new Vector3(-12f, -10f, 0f), 1f,
                        new Vector3(-4f, -12f, 2f), new Vector3(0f, 0.008f, 0.015f), new Vector3(-1.5f, 1.6f, 0f),
                        new Vector3(-0.192f, -0.174f, 0.250f), new Vector3(0f, 33f, 58f)),
                    Key(0.76f, new Vector3(0.140f, 0.220f, 0.200f), new Vector3(-8f, -5f, 0f), 1f,
                        new Vector3(-2f, -5f, 1f), Vector3.zero, Vector3.zero,
                        new Vector3(-0.160f, -0.140f, 0.300f), new Vector3(-5f, 26f, 54f)),
                    FootRest(1.00f))
            };
        }

        // ------------------------------------------------------------------ coups contextuels

        /// <summary>
        /// Charge d'épaule, en sprintant. Peu de dégâts, énormément de recul.
        ///
        /// Ces quatre coups ne consomment AUCUNE touche nouvelle : ils remplacent le coup de base
        /// quand la situation s'y prête. C'est la façon la moins chère d'ajouter de la variété,
        /// et la plus naturelle — sprinter et frapper n'est pas frapper, et le joueur n'a rien à
        /// apprendre pour le découvrir.
        /// </summary>
        private static void ConfigureShoulderCharge(AttackData a)
        {
            a.displayName = "Charge d'epaule";
            a.context = AttackContext.Sprinting;
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.knockdownChance = 0.45f;
            a.duration = 0.40f;
            a.cooldown = 0.22f;
            a.hitWindowStart = 0.24f;
            a.hitWindowEnd = 0.62f;
            a.comboCancelAt = 0.78f;
            a.hitRadius = 0.26f;
            a.damage = 13f;
            a.impactForce = 14f;
            a.staminaCost = 18f;
            a.shakeIntensity = 0.13f;
            a.shakeDuration = 0.22f;
            a.hitStopDuration = 0.032f;
            a.weightCurve = PunchWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    Rest(0.00f),
                    // L'epaule part en avant : les deux bras se replient, ce n'est pas un coup
                    // de poing mais un choc de tout le corps.
                    Key(0.20f, new Vector3(0.118f, -0.205f, 0.330f), new Vector3(6f, -30f, -74f), 1f,
                        new Vector3(6f, 24f, -6f), new Vector3(0.012f, -0.014f, 0.030f), new Vector3(2.2f, 4.5f, -3f),
                        new Vector3(-0.112f, -0.198f, 0.318f), new Vector3(6f, 34f, 74f)),
                    Key(0.40f, new Vector3(0.098f, -0.188f, 0.352f), new Vector3(8f, -34f, -76f), 1f,
                        new Vector3(9f, 28f, -7f), new Vector3(0.015f, -0.016f, 0.038f), new Vector3(2.8f, 5.5f, -3.6f),
                        new Vector3(-0.098f, -0.186f, 0.330f), new Vector3(8f, 38f, 76f)),
                    Key(0.70f, new Vector3(0.132f, -0.190f, 0.300f), new Vector3(2f, -24f, -70f), 1f,
                        new Vector3(3f, 14f, -3f), new Vector3(0.006f, -0.006f, 0.012f), Vector3.zero,
                        new Vector3(-0.130f, -0.180f, 0.310f), new Vector3(2f, 28f, 70f)),
                    Rest(1.00f))
            };
        }

        /// <summary>Coup plongeant, en l'air. Lent à sortir, mais il fait tomber presque à coup sûr.</summary>
        private static void ConfigureDive(AttackData a)
        {
            a.displayName = "Coup plongeant";
            a.context = AttackContext.Airborne;
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Rear;
            a.isHeavy = true;
            a.knockdownChance = 0.75f;
            a.duration = 0.42f;
            a.cooldown = 0.26f;
            a.hitWindowStart = 0.30f;
            a.hitWindowEnd = 0.70f;
            a.comboCancelAt = 0.85f;
            a.hitRadius = 0.22f;
            a.damage = 24f;
            a.impactForce = 11f;
            a.staminaCost = 24f;
            a.shakeIntensity = 0.15f;
            a.shakeDuration = 0.26f;
            a.hitStopDuration = 0.032f;
            a.weightCurve = PunchWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    Rest(0.00f),
                    // Le poing part tres haut puis s'abat : la trajectoire descendante est ce qui
                    // fait lire "de haut en bas" sans qu'on voie le corps du joueur.
                    Key(0.18f, new Vector3(0.170f, 0.120f, 0.200f), new Vector3(-70f, -12f, -60f), 0.55f,
                        new Vector3(-14f, 8f, 0f), new Vector3(0f, 0.024f, -0.010f), new Vector3(-4.5f, 0f, 0f),
                        new Vector3(-0.126f, -0.092f, 0.302f), new Vector3(-13f, 27f, 70f)),
                    Key(0.46f, new Vector3(0.090f, -0.200f, 0.400f), new Vector3(44f, -4f, -10f), 1f,
                        new Vector3(16f, -10f, 1f), new Vector3(0f, -0.026f, 0.020f), new Vector3(6f, 1.4f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.58f, new Vector3(0.074f, -0.260f, 0.410f), new Vector3(56f, -2f, -1f), 1f,
                        new Vector3(20f, -12f, 2f), new Vector3(0f, -0.032f, 0.024f), new Vector3(7.5f, 1.8f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.80f, new Vector3(0.118f, -0.176f, 0.340f), new Vector3(20f, -10f, -38f), 0.95f,
                        new Vector3(6f, -5f, 0f), new Vector3(0f, -0.008f, 0.004f), Vector3.zero,
                        new Vector3(-0.140f, -0.114f, 0.316f), new Vector3(-8f, 22f, 67f)),
                    Rest(1.00f))
            };
        }

        /// <summary>Balayage, en glissade. La jambe part au ras du sol : c'est le faucheur.</summary>
        private static void ConfigureSweep(AttackData a)
        {
            a.displayName = "Balayage";
            a.context = AttackContext.Sliding;
            a.limb = AttackLimb.Foot;
            a.hand = AttackHand.Alternate;
            a.isHeavy = false;
            a.knockdownChance = 0.85f;
            a.duration = 0.32f;
            a.cooldown = 0.14f;
            a.hitWindowStart = 0.24f;
            a.hitWindowEnd = 0.62f;
            a.comboCancelAt = 0.74f;
            a.hitRadius = 0.24f;
            a.damage = 9f;
            a.impactForce = 8f;
            a.staminaCost = 10f;
            a.shakeIntensity = 0.075f;
            a.shakeDuration = 0.16f;
            a.hitStopDuration = 0.026f;
            a.weightCurve = FootWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    FootRest(0.00f),
                    Key(0.18f, new Vector3(0.260f, 0.110f, -0.060f), new Vector3(0f, 26f, 0f), 1f,
                        new Vector3(0f, 14f, -3f), new Vector3(0.010f, -0.004f, -0.004f), new Vector3(0.6f, 2.6f, -1.2f),
                        new Vector3(-0.140f, -0.120f, 0.312f), new Vector3(-7f, 24f, 53f)),
                    Key(0.44f, new Vector3(0.020f, 0.120f, 0.420f), new Vector3(0f, -34f, 0f), 1f,
                        new Vector3(0f, -19f, 4f), new Vector3(-0.008f, -0.002f, 0.012f), new Vector3(0.8f, 2.6f, -1.2f),
                        new Vector3(-0.182f, -0.164f, 0.262f), new Vector3(-1f, 31f, 57f)),
                    Key(0.56f, new Vector3(-0.180f, 0.125f, 0.340f), new Vector3(0f, -52f, 0f), 1f,
                        new Vector3(0f, -24f, 5f), new Vector3(-0.014f, -0.002f, 0.008f), new Vector3(1.2f, 3.4f, -1.8f),
                        new Vector3(-0.196f, -0.176f, 0.244f), new Vector3(1f, 34f, 59f)),
                    Key(0.78f, new Vector3(0.060f, 0.110f, 0.140f), new Vector3(0f, -14f, 0f), 1f,
                        new Vector3(0f, -8f, 1f), Vector3.zero, Vector3.zero,
                        new Vector3(-0.160f, -0.140f, 0.300f), new Vector3(-5f, 26f, 54f)),
                    FootRest(1.00f))
            };
        }

        /// <summary>
        /// Coup de grâce sur un adversaire au sol.
        ///
        /// Il n'existe que dans cette situation, et c'est ce qui donne un sens à la chute : sans
        /// lui, mettre quelqu'un par terre ne rapporte qu'un temps d'attente. Avec lui, la chute
        /// devient une ouverture.
        /// </summary>
        private static void ConfigureStomp(AttackData a)
        {
            a.displayName = "Coup de grace";
            a.context = AttackContext.TargetDown;
            a.limb = AttackLimb.Foot;
            a.hand = AttackHand.Rear;
            a.isHeavy = true;
            a.knockdownChance = 0f;
            a.duration = 0.46f;
            a.cooldown = 0.30f;
            a.hitWindowStart = 0.34f;
            a.hitWindowEnd = 0.66f;
            a.comboCancelAt = 0.88f;
            a.hitRadius = 0.26f;
            a.damage = 28f;
            a.impactForce = 6f;
            a.staminaCost = 22f;
            a.shakeIntensity = 0.17f;
            a.shakeDuration = 0.28f;
            a.hitStopDuration = 0.032f;
            a.weightCurve = FootWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    FootRest(0.00f),
                    // Le pied monte haut puis s'abat au sol, devant soi.
                    Key(0.24f, new Vector3(0.150f, 0.620f, 0.200f), new Vector3(-40f, 0f, 0f), 1f,
                        new Vector3(-6f, 3f, 0f), new Vector3(0f, -0.018f, -0.006f), new Vector3(-3.2f, 0f, 0f),
                        new Vector3(-0.220f, -0.200f, 0.220f), new Vector3(4f, 32f, 58f)),
                    Key(0.50f, new Vector3(0.140f, 0.110f, 0.470f), new Vector3(14f, 0f, 0f), 1f,
                        new Vector3(12f, -2f, 0f), new Vector3(0f, -0.030f, 0.018f), new Vector3(6.5f, 0.8f, 0f),
                        new Vector3(-0.190f, -0.172f, 0.252f), new Vector3(0f, 32f, 58f)),
                    Key(0.62f, new Vector3(0.138f, 0.085f, 0.480f), new Vector3(18f, 0f, 0f), 1f,
                        new Vector3(14f, -3f, 0f), new Vector3(0f, -0.034f, 0.020f), new Vector3(7.5f, 1f, 0f),
                        new Vector3(-0.192f, -0.174f, 0.250f), new Vector3(0f, 33f, 58f)),
                    Key(0.84f, new Vector3(0.150f, 0.220f, 0.230f), new Vector3(4f, 0f, 0f), 1f,
                        new Vector3(4f, -1f, 0f), new Vector3(0f, -0.008f, 0.004f), Vector3.zero,
                        new Vector3(-0.166f, -0.146f, 0.292f), new Vector3(-4f, 27f, 55f)),
                    FootRest(1.00f))
            };
        }

        // ------------------------------------------------------------------ utilitaires

        /// <summary>Résumé lisible d'un asset, pour que la console dise s'il est réellement exploitable.</summary>
        private static string Describe(AttackData attack)
        {
            if (attack == null) return "ECHEC DE CREATION";

            string problem = attack.Diagnose();
            if (!string.IsNullOrEmpty(problem)) return "INCOMPLET (" + problem + ")";

            return attack.variants.Count + " variante(s), " + attack.damage.ToString("0") + " degats, " +
                   attack.duration.ToString("0.00") + " s, " +
                   (attack.limb == AttackLimb.Foot ? "pied" : "poing") +
                   (attack.chargeable ? ", chargeable" : "") +
                   (attack.context != AttackContext.Any ? ", contexte " + attack.context : "");
        }

        /// <summary>
        /// Poids d'un coup de poing. La montee est franche (8 %) et le plateau tient jusqu'a 84 % :
        /// le poing est deja pleinement engage quand la fenetre d'impact s'ouvre.
        ///
        /// L'ancienne courbe montait sur 12 % et lachait a 78 %. Le coup passait donc un quart de
        /// sa duree a n'etre nulle part : ni en garde, ni en extension. C'est exactement la
        /// sensation de mollesse.
        /// </summary>
        /// <summary>
        /// Coup de tête — celui que le dossier cite à côté du poing et du pied.
        ///
        /// Trois temps, et c'est le deuxième qui fait tout : les deux mains AGRIPPENT le col,
        /// la tête part en arrière pour armer, puis les mains TIRENT pendant que la tête plonge.
        /// Sans l'agrippement, un coup de tête ressemble à une révérence ; sans l'armé en
        /// arrière, il n'a aucun poids.
        ///
        /// C'est la caméra qui porte le coup chez le joueur (le corps n'a pas de tête en vue
        /// première personne) : l'élan de 40 cm vers l'avant emmène la hitbox du front. Portée
        /// très courte par construction — il faut être collé à l'adversaire, et c'est voulu.
        /// </summary>
        private static void ConfigureHeadbutt(AttackData a)
        {
            a.displayName = "Coup de tete";
            a.limb = AttackLimb.Head;
            a.hand = AttackHand.Rear;
            a.isHeavy = true;
            a.chargeable = false;
            a.knockdownChance = 0.22f;
            a.duration = 0.44f;
            a.cooldown = 0.16f;
            a.hitWindowStart = 0.40f;
            a.hitWindowEnd = 0.64f;
            a.comboCancelAt = 0.80f;
            a.hitRadius = 0.20f;
            a.damage = 21f;
            a.impactForce = 8.5f;
            a.staminaCost = 16f;
            a.shakeIntensity = 0.14f;
            a.shakeDuration = 0.20f;
            a.hitStopDuration = 0.032f;
            a.weightCurve = PunchWeightCurve();

            Vector3 grabRight = new Vector3(0.130f, -0.160f, 0.390f);
            Vector3 grabLeft = new Vector3(-0.130f, -0.160f, 0.390f);
            Vector3 pullRight = new Vector3(0.115f, -0.190f, 0.270f);
            Vector3 pullLeft = new Vector3(-0.115f, -0.190f, 0.270f);
            Vector3 grabEulerRight = new Vector3(-8f, -10f, -32f);
            Vector3 grabEulerLeft = new Vector3(-8f, 10f, 32f);

            a.variants = new List<AttackVariant>
            {
                Variant("01 - agrippe et frappe",
                    Rest(0.00f),
                    Key(0.22f, grabRight, grabEulerRight, 1f,
                        new Vector3(-12f, 0f, 0f), new Vector3(0f, 0.040f, -0.070f), new Vector3(-9f, 0f, 0f),
                        grabLeft, grabEulerLeft),
                    Key(0.46f, pullRight, grabEulerRight, 1f,
                        new Vector3(20f, 0f, 0f), new Vector3(0f, -0.070f, 0.400f), new Vector3(17f, 0f, 1.5f),
                        pullLeft, grabEulerLeft),
                    Key(0.58f, pullRight, grabEulerRight, 1f,
                        new Vector3(17f, 0f, 0f), new Vector3(0f, -0.060f, 0.360f), new Vector3(14f, 0f, 1f),
                        pullLeft, grabEulerLeft),
                    Key(0.80f, new Vector3(0.150f, -0.160f, 0.300f), GuardRightEuler, 0.9f,
                        new Vector3(4f, 0f, 0f), new Vector3(0f, -0.010f, 0.060f), new Vector3(3f, 0f, 0f),
                        GuardLeft, GuardLeftEuler),
                    Rest(1.00f))
            };
        }

        /// <summary>
        /// Bousculade à deux mains.
        ///
        /// Presque aucun dégât, et un recul énorme. Ce n'est pas un coup pour gagner, c'est un
        /// coup pour PLACER : envoyer quelqu'un dans une pile de sacs poubelle, se dégager quand
        /// deux adversaires vous coincent, ou casser la distance avant qu'il ne charge. C'est le
        /// coup qui rend le décor physique utile.
        ///
        /// Les paumes sont ouvertes (prise quasi nulle) : une bousculade poings fermés se lit
        /// comme deux directs simultanés.
        /// </summary>
        private static void ConfigureShove(AttackData a)
        {
            a.displayName = "Bousculade";
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Rear;
            a.isHeavy = false;
            a.chargeable = false;
            a.knockdownChance = 0.18f;
            a.duration = 0.36f;
            a.cooldown = 0.22f;
            a.hitWindowStart = 0.32f;
            a.hitWindowEnd = 0.62f;
            a.comboCancelAt = 0.72f;
            a.hitRadius = 0.27f;
            a.damage = 3f;
            a.impactForce = 15f;
            a.staminaCost = 12f;
            a.shakeIntensity = 0.06f;
            a.shakeDuration = 0.12f;
            a.hitStopDuration = 0.018f;
            a.weightCurve = PunchWeightCurve();

            Vector3 palmEulerRight = new Vector3(-72f, -8f, 0f);
            Vector3 palmEulerLeft = new Vector3(-72f, 8f, 0f);

            a.variants = new List<AttackVariant>
            {
                Variant("01 - deux paumes",
                    Rest(0.00f),
                    Key(0.20f, new Vector3(0.130f, -0.170f, 0.200f), palmEulerRight, 0.08f,
                        new Vector3(-6f, 0f, 0f), new Vector3(0f, 0f, -0.040f), new Vector3(-3f, 0f, 0f),
                        new Vector3(-0.130f, -0.170f, 0.200f), palmEulerLeft),
                    Key(0.42f, new Vector3(0.150f, -0.140f, 0.430f), palmEulerRight, 0.05f,
                        new Vector3(14f, 0f, 0f), new Vector3(0f, -0.020f, 0.180f), new Vector3(5f, 0f, 0f),
                        new Vector3(-0.150f, -0.140f, 0.430f), palmEulerLeft),
                    Key(0.60f, new Vector3(0.150f, -0.145f, 0.420f), palmEulerRight, 0.05f,
                        new Vector3(11f, 0f, 0f), new Vector3(0f, -0.015f, 0.150f), new Vector3(4f, 0f, 0f),
                        new Vector3(-0.150f, -0.145f, 0.420f), palmEulerLeft),
                    Key(0.84f, new Vector3(0.150f, -0.160f, 0.320f), GuardRightEuler, 0.7f,
                        new Vector3(2f, 0f, 0f), Vector3.zero, Vector3.zero,
                        GuardLeft, GuardLeftEuler),
                    Rest(1.00f))
            };
        }

        private static AnimationCurve PunchWeightCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.08f, 1f),
                new Keyframe(0.84f, 1f),
                new Keyframe(1f, 0f));
        }

        /// <summary>
        /// Les coups de pied prennent et rendent la jambe plus doucement que les poings.
        ///
        /// La jambe est pilotée par le cycle de marche : reprendre la main en deux images
        /// ferait claquer le pied d'une position à l'autre. On monte donc sur 18 % du coup et
        /// on redonne la main sur les 28 derniers pourcents.
        /// </summary>
        private static AnimationCurve FootWeightCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.14f, 1f),
                new Keyframe(0.78f, 1f),
                new Keyframe(1f, 0f));
        }

        private static AttackVariant Variant(string name, params AttackPoseKey[] keys)
        {
            AttackVariant variant = new AttackVariant();
            variant.name = name;
            variant.keys = new List<AttackPoseKey>(keys);
            return variant;
        }

        /// <summary>Pose de départ et d'arrivée d'un coup de poing : la garde, main libre en garde.</summary>
        private static AttackPoseKey Rest(float time)
        {
            return Key(time, GuardRight, GuardRightEuler, 0.85f, Vector3.zero, Vector3.zero, Vector3.zero,
                GuardLeft, GuardLeftEuler);
        }

        /// <summary>Pose de départ et d'arrivée d'un coup de pied : appui au sol, mains en garde.</summary>
        private static AttackPoseKey FootRest(float time)
        {
            return Key(time, StanceFoot, Vector3.zero, 1f, Vector3.zero, Vector3.zero, Vector3.zero,
                GuardLeft, GuardLeftEuler);
        }

        private static AttackPoseKey Key(float time, Vector3 limbPosition, Vector3 limbEuler, float grip,
            Vector3 bodyEuler, Vector3 cameraOffset, Vector3 cameraEuler,
            Vector3 offHandPosition, Vector3 offHandEuler)
        {
            AttackPoseKey key = new AttackPoseKey();
            key.time = time;
            key.handPosition = limbPosition;
            key.handEuler = limbEuler;
            key.grip = grip;
            key.bodyEuler = bodyEuler;
            key.cameraOffset = cameraOffset;
            key.cameraEuler = cameraEuler;
            key.offHandPosition = offHandPosition;
            key.offHandEuler = offHandEuler;
            key.offHandWeight = 1f;
            return key;
        }
    }
}
