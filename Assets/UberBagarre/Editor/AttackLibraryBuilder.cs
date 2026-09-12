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

            /// <summary>Tous les coups, dans l'ordre d'apprentissage.</summary>
            public AttackData[] All
            {
                get { return new[] { Straight, Hook, Uppercut, Kick, LowKick }; }
            }
        }

        [MenuItem("Uber Bagarre/4 - Regenerer les coups par defaut", false, 40)]
        public static void RegenerateFromMenu()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Regenerer les coups ?",
                "Les assets d'attaque (Direct, Crochet, Uppercut, Coup de pied, Coup de pied bas) " +
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
                }
            }
            else if (overwrite || !string.IsNullOrEmpty(asset.Diagnose()))
            {
                // Un asset deja present mais incomplet est repare automatiquement. Sans ca, un
                // asset cree par une version anterieure du code reste silencieusement cassé :
                // le coup se declenche, mais la main ne bouge pas et ne touche rien.
                if (!overwrite)
                {
                    Debug.LogWarning("[UberBagarre] '" + assetName + "' etait incomplet (" + asset.Diagnose() +
                                     "). Reparation automatique.", asset);
                }

                configure(asset);
                EditorUtility.SetDirty(asset);
            }

            return asset;
        }

        // ------------------------------------------------------------------ repères de pose

        // Garde au repos, recopiee de FirstPersonHands : une pose de coup qui ne part pas
        // exactement de la garde produit un saut visible a la premiere image.
        private static readonly Vector3 GuardRight = new Vector3(0.148f, -0.165f, 0.275f);
        private static readonly Vector3 GuardRightEuler = new Vector3(-4f, -18f, -8f);
        private static readonly Vector3 GuardLeft = new Vector3(-0.155f, -0.135f, 0.335f);
        private static readonly Vector3 GuardLeftEuler = new Vector3(-6f, 20f, 6f);

        // Garde haute de la main libre : elle monte protéger le menton pendant qu'on frappe.
        private static readonly Vector3 CoverLeft = new Vector3(-0.122f, -0.080f, 0.296f);
        private static readonly Vector3 CoverLeftEuler = new Vector3(-13f, 28f, 10f);

        // Pied droit au repos, dans le repère du personnage (origine au sol), aligné sur
        // ProceduralLocomotion._idleRightFoot + la hauteur de cheville.
        private static readonly Vector3 StanceFoot = new Vector3(0.16f, 0.09f, -0.17f);

        // ------------------------------------------------------------------ coups de poing

        /// <summary>
        /// Direct. Rapide, peu de dégâts, faible rotation du corps : il part du bras et de l'épaule.
        /// C'est le coup qui doit rester utilisable en permanence, donc la récupération est courte.
        ///
        /// La main s'OUVRE à l'armement et se SERRE juste avant l'impact. C'est ce que fait un
        /// boxeur — serrer tout le long fatigue et ralentit le bras — et ça se voit énormément
        /// en vue première personne, où le poing occupe un quart de l'écran.
        /// </summary>
        private static void ConfigureStraight(AttackData a)
        {
            a.displayName = "Direct";
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Alternate;
            a.isHeavy = false;
            a.knockdownChance = 0f;
            a.duration = 0.30f;
            a.cooldown = 0.05f;
            a.hitWindowStart = 0.36f;
            a.hitWindowEnd = 0.68f;
            a.hitRadius = 0.15f;
            a.damage = 9f;
            a.impactForce = 3.2f;
            a.staminaCost = 7f;
            a.shakeIntensity = 0.045f;
            a.shakeDuration = 0.10f;
            a.hitStopDuration = 0.030f;
            a.weightCurve = DefaultWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    Rest(0.00f),
                    Key(0.14f, new Vector3(0.178f, -0.158f, 0.205f), new Vector3(-2f, -13f, -6f), 0.55f,
                        new Vector3(0f, 6f, 0f), new Vector3(0f, 0f, -0.007f), new Vector3(0.6f, -0.8f, 0f),
                        new Vector3(-0.140f, -0.118f, 0.320f), new Vector3(-8f, 22f, 7f)),
                    Key(0.40f, new Vector3(0.075f, -0.080f, 0.455f), new Vector3(0f, -4f, -1f), 1f,
                        new Vector3(0f, -9f, 1f), new Vector3(0f, 0f, 0.009f), new Vector3(-1f, 1.2f, 0f),
                        new Vector3(-0.128f, -0.104f, 0.300f), new Vector3(-12f, 26f, 9f)),
                    Key(0.52f, new Vector3(0.045f, -0.055f, 0.500f), new Vector3(0f, -2f, 0f), 1f,
                        new Vector3(0f, -12f, 1f), new Vector3(0f, -0.002f, 0.013f), new Vector3(-1.4f, 1.8f, 0.4f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.72f, new Vector3(0.095f, -0.105f, 0.395f), new Vector3(-2f, -7f, -3f), 0.95f,
                        new Vector3(0f, -7f, 0f), new Vector3(0f, 0f, 0.004f), new Vector3(-0.4f, 0.6f, 0f),
                        new Vector3(-0.136f, -0.112f, 0.312f), new Vector3(-9f, 23f, 7f)),
                    Rest(1.00f)),

                Variant("02",
                    Rest(0.00f),
                    Key(0.16f, new Vector3(0.185f, -0.178f, 0.198f), new Vector3(-7f, -15f, -9f), 0.5f,
                        new Vector3(0f, 8f, -1f), new Vector3(0f, 0f, -0.008f), new Vector3(0.8f, -1f, 0f),
                        new Vector3(-0.136f, -0.110f, 0.326f), new Vector3(-9f, 24f, 8f)),
                    Key(0.42f, new Vector3(0.082f, -0.094f, 0.450f), new Vector3(-3f, -5f, 1f), 1f,
                        new Vector3(1f, -11f, 1f), new Vector3(0f, -0.003f, 0.010f), new Vector3(-1.2f, 1.1f, 0.6f),
                        new Vector3(-0.124f, -0.098f, 0.298f), new Vector3(-13f, 27f, 10f)),
                    Key(0.54f, new Vector3(0.058f, -0.072f, 0.492f), new Vector3(-2f, -3f, 2f), 1f,
                        new Vector3(1f, -14f, 1f), new Vector3(0f, -0.004f, 0.014f), new Vector3(-1.7f, 1.5f, 1f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.74f, new Vector3(0.108f, -0.122f, 0.380f), new Vector3(-3f, -9f, -4f), 0.9f,
                        new Vector3(0f, -6f, 0f), Vector3.zero, Vector3.zero,
                        new Vector3(-0.140f, -0.116f, 0.316f), new Vector3(-8f, 22f, 7f)),
                    Rest(1.00f)),

                // Troisieme variante plus seche : pic plus tot, moins de buste. Un enchainement
                // de trois directs ne doit pas battre la mesure comme un metronome.
                Variant("03",
                    Rest(0.00f),
                    Key(0.11f, new Vector3(0.170f, -0.150f, 0.222f), new Vector3(-1f, -12f, -5f), 0.6f,
                        new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, -0.005f), new Vector3(0.4f, -0.6f, 0f),
                        new Vector3(-0.144f, -0.122f, 0.318f), new Vector3(-7f, 21f, 7f)),
                    Key(0.38f, new Vector3(0.060f, -0.062f, 0.478f), new Vector3(1f, -2f, 1f), 1f,
                        new Vector3(0f, -8f, 0f), new Vector3(0f, 0f, 0.011f), new Vector3(-1.1f, 1.4f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.48f, new Vector3(0.042f, -0.050f, 0.498f), new Vector3(1f, -1f, 1f), 1f,
                        new Vector3(0f, -10f, 0f), new Vector3(0f, -0.002f, 0.012f), new Vector3(-1.3f, 1.6f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.68f, new Vector3(0.104f, -0.118f, 0.372f), new Vector3(-2f, -8f, -3f), 0.95f,
                        new Vector3(0f, -5f, 0f), Vector3.zero, Vector3.zero,
                        new Vector3(-0.138f, -0.114f, 0.320f), new Vector3(-8f, 22f, 7f)),
                    Rest(1.00f))
            };
        }

        /// <summary>
        /// Crochet. Le poing part de côté et traverse : l'essentiel de la puissance vient de la
        /// rotation du buste, d'où une rotation de corps trois fois plus forte que le direct.
        /// </summary>
        private static void ConfigureHook(AttackData a)
        {
            a.displayName = "Crochet";
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.knockdownChance = 0.06f;
            a.duration = 0.42f;
            a.cooldown = 0.09f;
            a.hitWindowStart = 0.40f;
            a.hitWindowEnd = 0.74f;
            a.hitRadius = 0.17f;
            a.damage = 15f;
            a.impactForce = 6f;
            a.staminaCost = 14f;
            a.shakeIntensity = 0.075f;
            a.shakeDuration = 0.16f;
            a.hitStopDuration = 0.045f;
            a.weightCurve = DefaultWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    Rest(0.00f),
                    Key(0.20f, new Vector3(0.296f, -0.142f, 0.172f), new Vector3(0f, -48f, -12f), 0.6f,
                        new Vector3(0f, 16f, -2f), new Vector3(0.015f, 0f, -0.011f), new Vector3(0f, 3.2f, -1.6f),
                        new Vector3(-0.132f, -0.096f, 0.308f), new Vector3(-12f, 26f, 9f)),
                    Key(0.46f, new Vector3(0.086f, -0.070f, 0.400f), new Vector3(0f, -66f, -8f), 1f,
                        new Vector3(0f, -14f, 2f), new Vector3(-0.006f, 0f, 0.010f), new Vector3(-0.8f, -3f, 1.6f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.58f, new Vector3(-0.030f, -0.055f, 0.415f), new Vector3(0f, -76f, -6f), 1f,
                        new Vector3(0f, -22f, 3f), new Vector3(-0.013f, 0f, 0.014f), new Vector3(-1f, -4.8f, 2.6f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.78f, new Vector3(0.065f, -0.112f, 0.320f), new Vector3(-2f, -42f, -8f), 0.95f,
                        new Vector3(0f, -10f, 1f), new Vector3(-0.004f, 0f, 0.004f), Vector3.zero,
                        new Vector3(-0.142f, -0.118f, 0.318f), new Vector3(-8f, 22f, 7f)),
                    Rest(1.00f)),

                // Crochet court, au corps : trajectoire plus basse et plus ramassee.
                Variant("02 - au corps",
                    Rest(0.00f),
                    Key(0.22f, new Vector3(0.282f, -0.228f, 0.160f), new Vector3(12f, -44f, -14f), 0.6f,
                        new Vector3(3f, 15f, -2f), new Vector3(0.013f, -0.008f, -0.010f), new Vector3(1.4f, 3f, -1.4f),
                        new Vector3(-0.128f, -0.092f, 0.304f), new Vector3(-13f, 27f, 10f)),
                    Key(0.48f, new Vector3(0.070f, -0.205f, 0.392f), new Vector3(16f, -64f, -10f), 1f,
                        new Vector3(4f, -13f, 2f), new Vector3(-0.005f, -0.006f, 0.010f), new Vector3(1.8f, -2.8f, 1.5f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.60f, new Vector3(-0.040f, -0.195f, 0.404f), new Vector3(18f, -74f, -8f), 1f,
                        new Vector3(5f, -20f, 3f), new Vector3(-0.012f, -0.007f, 0.013f), new Vector3(2.2f, -4.4f, 2.4f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.80f, new Vector3(0.062f, -0.170f, 0.310f), new Vector3(8f, -40f, -9f), 0.95f,
                        new Vector3(2f, -9f, 1f), new Vector3(-0.003f, 0f, 0.003f), Vector3.zero,
                        new Vector3(-0.140f, -0.114f, 0.316f), new Vector3(-8f, 22f, 7f)),
                    Rest(1.00f))
            };
        }

        /// <summary>
        /// Uppercut. Le poing descend puis remonte, les jambes poussent : le corps se penche
        /// en avant à l'armement puis se redresse. C'est le coup le plus lent et le plus lourd.
        /// </summary>
        private static void ConfigureUppercut(AttackData a)
        {
            a.displayName = "Uppercut";
            a.limb = AttackLimb.Hand;
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.knockdownChance = 0.12f;
            a.duration = 0.48f;
            a.cooldown = 0.11f;
            a.hitWindowStart = 0.42f;
            a.hitWindowEnd = 0.76f;
            a.hitRadius = 0.17f;
            a.damage = 17f;
            a.impactForce = 7f;
            a.staminaCost = 16f;
            a.shakeIntensity = 0.085f;
            a.shakeDuration = 0.18f;
            a.hitStopDuration = 0.050f;
            a.weightCurve = DefaultWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    Rest(0.00f),
                    Key(0.24f, new Vector3(0.172f, -0.330f, 0.240f), new Vector3(28f, -14f, -6f), 0.55f,
                        new Vector3(8f, 7f, 0f), new Vector3(0f, -0.015f, -0.006f), new Vector3(2.6f, 0f, 0f),
                        new Vector3(-0.130f, -0.098f, 0.306f), new Vector3(-12f, 26f, 9f)),
                    Key(0.48f, new Vector3(0.120f, -0.030f, 0.372f), new Vector3(-28f, -9f, 0f), 1f,
                        new Vector3(-7f, -7f, 1f), new Vector3(0f, 0.011f, 0.009f), new Vector3(2.4f, 0.7f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.60f, new Vector3(0.098f, 0.100f, 0.400f), new Vector3(-50f, -6f, 0f), 1f,
                        new Vector3(-12f, -9f, 2f), new Vector3(0f, 0.017f, 0.010f), new Vector3(3.6f, 1f, 0f),
                        CoverLeft, CoverLeftEuler),
                    Key(0.82f, new Vector3(0.128f, -0.035f, 0.325f), new Vector3(-20f, -12f, -4f), 0.95f,
                        new Vector3(-4f, -4f, 0f), new Vector3(0f, 0.004f, 0f), Vector3.zero,
                        new Vector3(-0.140f, -0.114f, 0.316f), new Vector3(-8f, 22f, 7f)),
                    Rest(1.00f))
            };
        }

        // ------------------------------------------------------------------ coups de pied

        /// <summary>
        /// Coup de pied de face. Le genou monte, la jambe se détend, le buste part en arrière
        /// pour compenser — sans ce contrepoids, un coup de pied ressemble à une jambe qui
        /// s'agite toute seule.
        ///
        /// Le bras opposé s'ouvre : c'est lui qui tient l'équilibre dans un vrai coup de pied.
        /// </summary>
        private static void ConfigureKick(AttackData a)
        {
            a.displayName = "Coup de pied";
            a.limb = AttackLimb.Foot;
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.knockdownChance = 0.25f;
            a.duration = 0.55f;
            a.cooldown = 0.18f;
            a.hitWindowStart = 0.38f;
            a.hitWindowEnd = 0.66f;
            a.hitRadius = 0.19f;
            a.damage = 19f;
            a.impactForce = 9.5f;
            a.staminaCost = 21f;
            a.shakeIntensity = 0.095f;
            a.shakeDuration = 0.20f;
            a.hitStopDuration = 0.055f;
            a.weightCurve = FootWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    FootRest(0.00f),
                    // Armement : le genou monte, le corps s'assied legerement sur l'autre jambe.
                    Key(0.22f, new Vector3(0.170f, 0.440f, 0.100f), new Vector3(-34f, 0f, 0f), 1f,
                        new Vector3(-3f, 4f, 0f), new Vector3(0f, -0.011f, -0.008f), new Vector3(1.6f, 0f, 0f),
                        new Vector3(-0.232f, -0.215f, 0.205f), new Vector3(6f, 34f, 14f)),
                    // Le pied arrive a 0,99 m du sol, donc franchement AU-DESSUS du bassin : le
                    // coup compte comme une touche au corps, pas aux jambes. Viser 10 cm plus bas
                    // le faisait basculer dans la zone « jambes » et diviser ses degats par deux.
                    Key(0.46f, new Vector3(0.120f, 0.900f, 0.400f), new Vector3(-26f, 0f, 0f), 1f,
                        new Vector3(-6f, -3f, 0f), new Vector3(0f, 0.008f, 0.012f), new Vector3(-1.8f, 1f, 0f),
                        new Vector3(-0.268f, -0.248f, 0.118f), new Vector3(10f, 42f, 20f)),
                    Key(0.56f, new Vector3(0.110f, 0.970f, 0.480f), new Vector3(-22f, 0f, 0f), 1f,
                        new Vector3(-7f, -4f, 0f), new Vector3(0f, 0.011f, 0.016f), new Vector3(-2.2f, 1.2f, 0f),
                        new Vector3(-0.272f, -0.252f, 0.110f), new Vector3(11f, 44f, 21f)),
                    Key(0.80f, new Vector3(0.150f, 0.460f, 0.220f), new Vector3(-16f, 0f, 0f), 1f,
                        new Vector3(-3f, -1f, 0f), new Vector3(0f, 0.003f, 0.004f), Vector3.zero,
                        new Vector3(-0.206f, -0.180f, 0.262f), new Vector3(0f, 26f, 9f)),
                    FootRest(1.00f))
            };
        }

        /// <summary>
        /// Coup de pied bas. Il ne fait presque pas de dégâts — son intérêt est ailleurs :
        /// il part dans les jambes, et c'est le coup qui fait TOMBER.
        ///
        /// Les cibles de cheville restent sous 0,82 m de la hanche. La jambe mesure 0,86 m :
        /// viser plus loin la tendrait à l'extrême, l'IK saturerait, et le balayage se
        /// figerait en milieu de course. C'est un calcul, pas un réglage à l'œil.
        /// </summary>
        private static void ConfigureLowKick(AttackData a)
        {
            a.displayName = "Coup de pied bas";
            a.limb = AttackLimb.Foot;
            a.hand = AttackHand.Alternate;
            a.isHeavy = false;
            a.knockdownChance = 0.55f;
            a.duration = 0.42f;
            a.cooldown = 0.12f;
            a.hitWindowStart = 0.34f;
            a.hitWindowEnd = 0.64f;
            a.hitRadius = 0.20f;
            a.damage = 11f;
            a.impactForce = 7f;
            a.staminaCost = 16f;
            a.shakeIntensity = 0.060f;
            a.shakeDuration = 0.14f;
            a.hitStopDuration = 0.040f;
            a.weightCurve = FootWeightCurve();

            a.variants = new List<AttackVariant>
            {
                Variant("01",
                    FootRest(0.00f),
                    Key(0.20f, new Vector3(0.300f, 0.160f, -0.100f), new Vector3(0f, 18f, 0f), 1f,
                        new Vector3(0f, 10f, -2f), new Vector3(0.008f, -0.006f, -0.006f), new Vector3(0.8f, 2f, -1f),
                        new Vector3(-0.136f, -0.110f, 0.318f), new Vector3(-8f, 24f, 8f)),
                    Key(0.48f, new Vector3(0.050f, 0.200f, 0.400f), new Vector3(0f, -26f, 0f), 1f,
                        new Vector3(0f, -14f, 3f), new Vector3(-0.004f, -0.004f, 0.010f), new Vector3(0.8f, 2f, -1f),
                        new Vector3(-0.178f, -0.160f, 0.268f), new Vector3(-2f, 30f, 12f)),
                    Key(0.60f, new Vector3(-0.120f, 0.220f, 0.380f), new Vector3(0f, -40f, 0f), 1f,
                        new Vector3(0f, -18f, 4f), new Vector3(-0.009f, -0.004f, 0.008f), new Vector3(1f, 2.6f, -1.4f),
                        new Vector3(-0.190f, -0.172f, 0.252f), new Vector3(0f, 32f, 13f)),
                    Key(0.82f, new Vector3(0.080f, 0.130f, 0.160f), new Vector3(0f, -12f, 0f), 1f,
                        new Vector3(0f, -6f, 1f), Vector3.zero, Vector3.zero,
                        new Vector3(-0.160f, -0.140f, 0.300f), new Vector3(-5f, 26f, 9f)),
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
                   (attack.limb == AttackLimb.Foot ? "pied" : "poing");
        }

        private static AnimationCurve DefaultWeightCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.12f, 1f),
                new Keyframe(0.78f, 1f),
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
                new Keyframe(0.18f, 1f),
                new Keyframe(0.72f, 1f),
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
