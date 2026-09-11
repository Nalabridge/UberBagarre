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
    /// Toutes les poses sont écrites pour la MAIN DROITE ; jouées à gauche elles sont mirrorées.
    /// </summary>
    public static class AttackLibraryBuilder
    {
        public const string AttacksFolder = "Assets/UberBagarre/Combat";

        [MenuItem("Uber Bagarre/4 - Regenerer les coups par defaut", false, 40)]
        public static void RegenerateFromMenu()
        {
            bool confirm = EditorUtility.DisplayDialog(
                "Regenerer les coups ?",
                "Les assets d'attaque (Direct, Crochet, Uppercut) vont etre REMPLACES par les " +
                "valeurs par defaut.\n\nToutes tes modifications de degats, de timings et de poses seront perdues.",
                "Remplacer", "Annuler");

            if (!confirm) return;

            AttackData straight, hook, uppercut;
            BuildAll(true, out straight, out hook, out uppercut);
            AssetDatabase.SaveAssets();

            Debug.Log("[UberBagarre] Coups regeneres dans " + AttacksFolder);
        }

        public static void BuildAll(bool overwrite, out AttackData straight, out AttackData hook, out AttackData uppercut)
        {
            EditorBuildUtility.EnsureFolder(AttacksFolder);

            straight = GetOrCreate("A_Direct", overwrite, ConfigureStraight);
            hook = GetOrCreate("A_Crochet", overwrite, ConfigureHook);
            uppercut = GetOrCreate("A_Uppercut", overwrite, ConfigureUppercut);
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
            }
            else if (overwrite)
            {
                configure(asset);
                EditorUtility.SetDirty(asset);
            }

            return asset;
        }

        // ------------------------------------------------------------------ coups

        /// <summary>
        /// Direct. Rapide, peu de dégâts, faible rotation du corps : il part du bras et de l'épaule.
        /// C'est le coup qui doit rester utilisable en permanence, donc la récupération est courte.
        /// </summary>
        private static void ConfigureStraight(AttackData a)
        {
            a.displayName = "Direct";
            a.hand = AttackHand.Alternate;
            a.isHeavy = false;
            a.duration = 0.30f;
            a.cooldown = 0.05f;
            a.hitWindowStart = 0.42f;
            a.hitWindowEnd = 0.60f;
            a.hitRadius = 0.10f;
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
                    Key(0.00f, new Vector3(0.148f, -0.165f, 0.275f), new Vector3(-4f, -18f, -8f), 1f, Vector3.zero, Vector3.zero, Vector3.zero),
                    Key(0.18f, new Vector3(0.168f, -0.150f, 0.225f), new Vector3(-2f, -14f, -6f), 1f, new Vector3(0f, 5f, 0f), new Vector3(0f, 0f, -0.006f), Vector3.zero),
                    Key(0.50f, new Vector3(0.045f, -0.055f, 0.500f), new Vector3(0f, -2f, 0f), 1f, new Vector3(0f, -12f, 1f), new Vector3(0f, 0f, 0.012f), new Vector3(-1.2f, 1.6f, 0f)),
                    Key(0.68f, new Vector3(0.078f, -0.088f, 0.430f), new Vector3(-2f, -6f, -2f), 1f, new Vector3(0f, -8f, 0f), new Vector3(0f, 0f, 0.004f), Vector3.zero),
                    Key(1.00f, new Vector3(0.148f, -0.165f, 0.275f), new Vector3(-4f, -18f, -8f), 1f, Vector3.zero, Vector3.zero, Vector3.zero)),

                Variant("02",
                    Key(0.00f, new Vector3(0.148f, -0.165f, 0.275f), new Vector3(-4f, -18f, -8f), 1f, Vector3.zero, Vector3.zero, Vector3.zero),
                    Key(0.20f, new Vector3(0.175f, -0.175f, 0.215f), new Vector3(-6f, -16f, -8f), 1f, new Vector3(0f, 7f, 0f), new Vector3(0f, 0f, -0.007f), Vector3.zero),
                    Key(0.50f, new Vector3(0.062f, -0.085f, 0.490f), new Vector3(-3f, -4f, 2f), 1f, new Vector3(1f, -14f, 1f), new Vector3(0f, -0.004f, 0.013f), new Vector3(-1.6f, 1.2f, 1f)),
                    Key(0.70f, new Vector3(0.090f, -0.110f, 0.410f), new Vector3(-3f, -8f, -3f), 1f, new Vector3(0f, -7f, 0f), Vector3.zero, Vector3.zero),
                    Key(1.00f, new Vector3(0.148f, -0.165f, 0.275f), new Vector3(-4f, -18f, -8f), 1f, Vector3.zero, Vector3.zero, Vector3.zero))
            };
        }

        /// <summary>
        /// Crochet. Le poing part de côté et traverse : l'essentiel de la puissance vient de la
        /// rotation du buste, d'où une rotation de corps trois fois plus forte que le direct.
        /// </summary>
        private static void ConfigureHook(AttackData a)
        {
            a.displayName = "Crochet";
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.duration = 0.42f;
            a.cooldown = 0.09f;
            a.hitWindowStart = 0.45f;
            a.hitWindowEnd = 0.66f;
            a.hitRadius = 0.12f;
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
                    Key(0.00f, new Vector3(0.148f, -0.165f, 0.275f), new Vector3(-4f, -18f, -8f), 1f, Vector3.zero, Vector3.zero, Vector3.zero),
                    Key(0.22f, new Vector3(0.290f, -0.145f, 0.180f), new Vector3(0f, -46f, -12f), 1f, new Vector3(0f, 15f, -2f), new Vector3(0.014f, 0f, -0.010f), new Vector3(0f, 3f, -1.5f)),
                    Key(0.55f, new Vector3(-0.030f, -0.055f, 0.415f), new Vector3(0f, -74f, -6f), 1f, new Vector3(0f, -21f, 3f), new Vector3(-0.012f, 0f, 0.014f), new Vector3(-1f, -4.5f, 2.5f)),
                    Key(0.76f, new Vector3(0.065f, -0.112f, 0.320f), new Vector3(-2f, -42f, -8f), 1f, new Vector3(0f, -10f, 1f), new Vector3(-0.004f, 0f, 0.004f), Vector3.zero),
                    Key(1.00f, new Vector3(0.148f, -0.165f, 0.275f), new Vector3(-4f, -18f, -8f), 1f, Vector3.zero, Vector3.zero, Vector3.zero))
            };
        }

        /// <summary>
        /// Uppercut. Le poing descend puis remonte, les jambes poussent : le corps se penche
        /// en avant à l'armement puis se redresse. C'est le coup le plus lent et le plus lourd.
        /// </summary>
        private static void ConfigureUppercut(AttackData a)
        {
            a.displayName = "Uppercut";
            a.hand = AttackHand.Alternate;
            a.isHeavy = true;
            a.duration = 0.48f;
            a.cooldown = 0.11f;
            a.hitWindowStart = 0.46f;
            a.hitWindowEnd = 0.68f;
            a.hitRadius = 0.12f;
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
                    Key(0.00f, new Vector3(0.148f, -0.165f, 0.275f), new Vector3(-4f, -18f, -8f), 1f, Vector3.zero, Vector3.zero, Vector3.zero),
                    Key(0.26f, new Vector3(0.172f, -0.325f, 0.245f), new Vector3(26f, -14f, -6f), 1f, new Vector3(7f, 7f, 0f), new Vector3(0f, -0.014f, -0.006f), new Vector3(2.5f, 0f, 0f)),
                    Key(0.58f, new Vector3(0.098f, 0.100f, 0.400f), new Vector3(-48f, -6f, 0f), 1f, new Vector3(-11f, -9f, 2f), new Vector3(0f, 0.016f, 0.010f), new Vector3(3.5f, 1f, 0f)),
                    Key(0.80f, new Vector3(0.128f, -0.035f, 0.325f), new Vector3(-20f, -12f, -4f), 1f, new Vector3(-4f, -4f, 0f), new Vector3(0f, 0.004f, 0f), Vector3.zero),
                    Key(1.00f, new Vector3(0.148f, -0.165f, 0.275f), new Vector3(-4f, -18f, -8f), 1f, Vector3.zero, Vector3.zero, Vector3.zero))
            };
        }

        // ------------------------------------------------------------------ utilitaires

        private static AnimationCurve DefaultWeightCurve()
        {
            return new AnimationCurve(
                new Keyframe(0f, 0f),
                new Keyframe(0.12f, 1f),
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

        private static AttackPoseKey Key(float time, Vector3 handPosition, Vector3 handEuler, float grip,
            Vector3 bodyEuler, Vector3 cameraOffset, Vector3 cameraEuler)
        {
            AttackPoseKey key = new AttackPoseKey();
            key.time = time;
            key.handPosition = handPosition;
            key.handEuler = handEuler;
            key.grip = grip;
            key.bodyEuler = bodyEuler;
            key.cameraOffset = cameraOffset;
            key.cameraEuler = cameraEuler;
            return key;
        }
    }
}
