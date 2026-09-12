using UberBagarre.Combat;
using UberBagarre.View;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Construit le corps d'un combattant : squelette, chair, mains articulées, hitbox.
    ///
    /// Le MÊME code sert au joueur et à l'ennemi. C'est la vérification concrète de la promesse
    /// d'architecture : si le joueur et l'ennemi avaient besoin de deux corps différents, c'est
    /// que le moteur de combat ne serait pas réellement partagé.
    ///
    /// La géométrie utilise des maillages générés (segments coniques, boîtes adoucies) et non
    /// les primitives d'Unity : un cube reste un cube et une capsule reste un tube d'épaisseur
    /// constante. À courte distance, ça se voit immédiatement.
    /// </summary>
    public static class FighterBuilder
    {
        // Proportions d'un corps de 1m80 : bassin 0.92 + 0.13 + 0.17 + 0.20 place la nuque
        // a 1.42, juste sous les yeux a 1.62.
        public const float PelvisHeight = 0.92f;
        public const float SpineOffset = 0.13f;
        public const float ChestOffset = 0.17f;
        public const float NeckOffset = 0.20f;
        public const float EyeHeight = 1.62f;

        public const float ShoulderOffsetX = 0.19f;
        public const float ShoulderOffsetY = 0.16f;
        public const float UpperArmLength = 0.30f;
        public const float ForearmLength = 0.26f;

        public const float HipOffsetX = 0.10f;
        public const float ThighLength = 0.44f;
        public const float ShinLength = 0.42f;

        /// <summary>Ce que la construction rend au reste du générateur.</summary>
        public class Result
        {
            public GameObject Body;
            public BodyRig Rig;
            public ProceduralLocomotion Locomotion;
            public Hitbox LeftHitbox;
            public Hitbox RightHitbox;
        }

        /// <summary>Palette d'un combattant : permet de distinguer le joueur de l'ennemi.</summary>
        public struct Skin
        {
            public Material Flesh;
            public Material Shirt;
            public Material Pants;
            public Material Shoe;

            public static Skin Player(BuildMaterials m)
            {
                Skin s = new Skin();
                s.Flesh = m.Skin; s.Shirt = m.Shirt; s.Pants = m.Pants; s.Shoe = m.Shoe;
                return s;
            }

            public static Skin Enemy(BuildMaterials m)
            {
                Skin s = new Skin();
                s.Flesh = m.EnemySkin; s.Shirt = m.EnemyShirt; s.Pants = m.Pants; s.Shoe = m.Shoe;
                return s;
            }
        }

        public static Result BuildBody(Transform parent, Skin skin, bool withHead, Faction faction, GameObject owner)
        {
            ProceduralMeshFactory.EnsureLibrary();

            Result result = new Result();
            result.Body = EditorBuildUtility.CreateEmpty("Body", parent, Vector3.zero);

            GameObject pelvis = EditorBuildUtility.CreateEmpty("Pelvis", result.Body.transform,
                new Vector3(0f, PelvisHeight, 0f));
            Box("PelvisVisual", pelvis.transform, new Vector3(0f, -0.02f, 0f),
                new Vector3(0.30f, 0.20f, 0.21f), skin.Pants);

            GameObject spine = EditorBuildUtility.CreateEmpty("Spine", pelvis.transform, new Vector3(0f, SpineOffset, 0f));
            Box("TorsoVisual", spine.transform, new Vector3(0f, 0.10f, 0f),
                new Vector3(0.34f, 0.30f, 0.22f), skin.Shirt);

            GameObject chest = EditorBuildUtility.CreateEmpty("Chest", spine.transform, new Vector3(0f, ChestOffset, 0f));
            Box("ChestVisual", chest.transform, new Vector3(0f, 0.08f, 0f),
                new Vector3(0.39f, 0.25f, 0.24f), skin.Shirt);

            GameObject neck = EditorBuildUtility.CreateEmpty("Neck", chest.transform, new Vector3(0f, NeckOffset, 0f));
            Bone("NeckVisual", neck.transform, Quaternion.Euler(-90f, 0f, 0f), 0.07f, 0.052f, skin.Flesh);

            if (withHead)
            {
                // Le joueur ne voit jamais sa propre tete : elle n'existe que sur l'adversaire.
                Box("HeadVisual", neck.transform, new Vector3(0f, 0.12f, 0.005f),
                    new Vector3(0.16f, 0.21f, 0.18f), skin.Flesh);
            }

            IkLimb leftArm = BuildArm(chest.transform, result.Body.transform, HandSide.Left, skin, faction, owner);
            IkLimb rightArm = BuildArm(chest.transform, result.Body.transform, HandSide.Right, skin, faction, owner);

            IkLimb leftLeg = BuildLeg(pelvis.transform, result.Body.transform, true, skin);
            IkLimb rightLeg = BuildLeg(pelvis.transform, result.Body.transform, false, skin);

            result.LeftHitbox = leftArm.End.GetComponent<Hitbox>();
            result.RightHitbox = rightArm.End.GetComponent<Hitbox>();

            result.Rig = result.Body.AddComponent<BodyRig>();
            SerializedWiring.SetObject(result.Rig, "_pelvis", pelvis.transform);
            SerializedWiring.SetObject(result.Rig, "_spine", spine.transform);
            SerializedWiring.SetObject(result.Rig, "_chest", chest.transform);
            SerializedWiring.SetObject(result.Rig, "_neck", neck.transform);
            SerializedWiring.SetObject(result.Rig, "_leftLeg", leftLeg);
            SerializedWiring.SetObject(result.Rig, "_rightLeg", rightLeg);
            SerializedWiring.SetObject(result.Rig, "_leftArm", leftArm);
            SerializedWiring.SetObject(result.Rig, "_rightArm", rightArm);
            SerializedWiring.SetObject(result.Rig, "_leftHand", leftArm.End.GetComponentInChildren<HandRig>());
            SerializedWiring.SetObject(result.Rig, "_rightHand", rightArm.End.GetComponentInChildren<HandRig>());

            result.Locomotion = result.Body.AddComponent<ProceduralLocomotion>();
            SerializedWiring.SetObject(result.Locomotion, "_rig", result.Rig);
            SerializedWiring.SetObject(result.Locomotion, "_root", parent);

            return result;
        }

        // ------------------------------------------------------------------ bras

        private static IkLimb BuildArm(Transform chest, Transform poleSpace, HandSide side, Skin skin,
            Faction faction, GameObject owner)
        {
            bool isLeft = side == HandSide.Left;
            float sign = isLeft ? -1f : 1f;
            string prefix = isLeft ? "Left" : "Right";

            GameObject shoulder = EditorBuildUtility.CreateEmpty(prefix + "Shoulder", chest,
                new Vector3(sign * ShoulderOffsetX, ShoulderOffsetY, 0f));

            GameObject upperArm = EditorBuildUtility.CreateEmpty(prefix + "UpperArm", shoulder.transform, Vector3.zero);
            GameObject forearm = EditorBuildUtility.CreateEmpty(prefix + "Forearm", upperArm.transform,
                new Vector3(0f, 0f, UpperArmLength));
            GameObject wrist = EditorBuildUtility.CreateEmpty(prefix + "Wrist", forearm.transform,
                new Vector3(0f, 0f, ForearmLength));

            // L'epaule est plus epaisse que le coude, le coude plus que le poignet :
            // c'est cette conicite qui fait la difference avec un assemblage de tubes.
            Bone(prefix + "UpperArmVisual", upperArm.transform, Quaternion.identity, UpperArmLength, 0.060f, skin.Shirt);
            Bone(prefix + "ForearmVisual", forearm.transform, Quaternion.identity, ForearmLength, 0.050f, skin.Flesh);

            Transform knuckles = BuildHand(wrist.transform, side, skin);

            Hitbox hitbox = wrist.AddComponent<Hitbox>();
            SerializedWiring.SetObject(hitbox, "_origin", knuckles);
            SerializedWiring.SetEnum(hitbox, "_ownerFaction", (int)faction);
            SerializedWiring.SetObject(hitbox, "_owner", owner);

            IkLimb limb = shoulder.AddComponent<IkLimb>();
            SerializedWiring.SetObject(limb, "_upper", upperArm.transform);
            SerializedWiring.SetObject(limb, "_lower", forearm.transform);
            SerializedWiring.SetObject(limb, "_end", wrist.transform);
            SerializedWiring.SetFloat(limb, "_upperLength", UpperArmLength);
            SerializedWiring.SetFloat(limb, "_lowerLength", ForearmLength);
            SerializedWiring.SetBool(limb, "_autoMeasureLengths", true);
            SerializedWiring.SetObject(limb, "_poleSpace", poleSpace);
            SerializedWiring.SetVector3(limb, "_poleDirection", new Vector3(sign * 0.25f, -1f, -0.35f));

            return limb;
        }

        // ------------------------------------------------------------------ jambes

        private static IkLimb BuildLeg(Transform pelvis, Transform poleSpace, bool isLeft, Skin skin)
        {
            float sign = isLeft ? -1f : 1f;
            string prefix = isLeft ? "Left" : "Right";

            GameObject hip = EditorBuildUtility.CreateEmpty(prefix + "Hip", pelvis,
                new Vector3(sign * HipOffsetX, -0.02f, 0f));

            GameObject thigh = EditorBuildUtility.CreateEmpty(prefix + "Thigh", hip.transform, Vector3.zero);
            GameObject shin = EditorBuildUtility.CreateEmpty(prefix + "Shin", thigh.transform,
                new Vector3(0f, 0f, ThighLength));
            GameObject ankle = EditorBuildUtility.CreateEmpty(prefix + "Ankle", shin.transform,
                new Vector3(0f, 0f, ShinLength));

            Bone(prefix + "ThighVisual", thigh.transform, Quaternion.identity, ThighLength, 0.082f, skin.Pants);
            Bone(prefix + "ShinVisual", shin.transform, Quaternion.identity, ShinLength, 0.066f, skin.Pants);

            Box(prefix + "FootVisual", ankle.transform, new Vector3(0f, -0.042f, 0.050f),
                new Vector3(0.105f, 0.065f, 0.255f), skin.Shoe);

            IkLimb limb = hip.AddComponent<IkLimb>();
            SerializedWiring.SetObject(limb, "_upper", thigh.transform);
            SerializedWiring.SetObject(limb, "_lower", shin.transform);
            SerializedWiring.SetObject(limb, "_end", ankle.transform);
            SerializedWiring.SetFloat(limb, "_upperLength", ThighLength);
            SerializedWiring.SetFloat(limb, "_lowerLength", ShinLength);
            SerializedWiring.SetBool(limb, "_autoMeasureLengths", true);
            SerializedWiring.SetObject(limb, "_poleSpace", poleSpace);
            SerializedWiring.SetVector3(limb, "_poleDirection", new Vector3(sign * 0.15f, 0.35f, 1f));

            return limb;
        }

        // ------------------------------------------------------------------ main

        private struct FingerSpec
        {
            public string Name;
            public Vector3 Base;
            public Vector3 BaseEuler;
            public float Proximal;
            public float Middle;
            public float Distal;
            public float Radius;
            public float ProximalCurl;
            public float MiddleCurl;
            public float DistalCurl;
            public float CloseDelay;
        }

        /// <summary>
        /// Main articulée : paume adoucie, 5 doigts de 3 phalanges coniques, bosses
        /// d'articulation. Les longueurs et les temps de fermeture diffèrent d'un doigt à
        /// l'autre — quatre doigts identiques se lisent comme un peigne.
        /// </summary>
        private static Transform BuildHand(Transform wrist, HandSide side, Skin skin)
        {
            float sign = side == HandSide.Left ? -1f : 1f;
            string prefix = side == HandSide.Left ? "Left" : "Right";

            Bone(prefix + "WristVisual", wrist, Quaternion.identity, 0.034f, 0.036f, skin.Flesh);

            GameObject palm = EditorBuildUtility.CreateEmpty(prefix + "Palm", wrist, new Vector3(0f, 0f, 0.038f));
            Box(prefix + "PalmVisual", palm.transform, Vector3.zero,
                new Vector3(0.082f, 0.070f, 0.076f), skin.Flesh);

            GameObject knuckles = EditorBuildUtility.CreateEmpty(prefix + "Knuckles", palm.transform,
                new Vector3(0f, -0.004f, 0.042f));

            FingerSpec[] specs =
            {
                Finger("Index",       new Vector3(sign * 0.026f, -0.006f, 0.030f), Vector3.zero, 0.039f, 0.025f, 0.019f, 0.0108f, 78f, 96f, 62f, 0.15f),
                Finger("Majeur",      new Vector3(sign * 0.009f, -0.004f, 0.032f), Vector3.zero, 0.043f, 0.027f, 0.020f, 0.0114f, 82f, 98f, 64f, 0.08f),
                Finger("Annulaire",   new Vector3(sign * -0.009f, -0.006f, 0.030f), Vector3.zero, 0.040f, 0.026f, 0.019f, 0.0104f, 85f, 100f, 66f, 0.03f),
                Finger("Auriculaire", new Vector3(sign * -0.025f, -0.010f, 0.026f), Vector3.zero, 0.033f, 0.022f, 0.017f, 0.0091f, 88f, 102f, 68f, 0f),
                Finger("Pouce",       new Vector3(sign * 0.036f, -0.013f, -0.002f), new Vector3(6f, -sign * 38f, -sign * 52f), 0.035f, 0.027f, 0.020f, 0.0130f, 42f, 48f, 32f, 0.35f)
            };

            Transform[,] joints = new Transform[specs.Length, 3];

            for (int i = 0; i < specs.Length; i++)
            {
                FingerSpec spec = specs[i];

                Box(prefix + spec.Name + "Knuckle", palm.transform, spec.Base,
                    Vector3.one * (spec.Radius * 2.5f), skin.Flesh);

                GameObject proximal = EditorBuildUtility.CreateEmpty(prefix + spec.Name + "1", palm.transform, spec.Base);
                proximal.transform.localRotation = Quaternion.Euler(spec.BaseEuler);

                GameObject middle = EditorBuildUtility.CreateEmpty(prefix + spec.Name + "2", proximal.transform,
                    new Vector3(0f, 0f, spec.Proximal));
                GameObject distal = EditorBuildUtility.CreateEmpty(prefix + spec.Name + "3", middle.transform,
                    new Vector3(0f, 0f, spec.Middle));

                Bone(prefix + spec.Name + "1Visual", proximal.transform, Quaternion.identity, spec.Proximal, spec.Radius, skin.Flesh);
                Bone(prefix + spec.Name + "2Visual", middle.transform, Quaternion.identity, spec.Middle, spec.Radius * 0.93f, skin.Flesh);
                Bone(prefix + spec.Name + "3Visual", distal.transform, Quaternion.identity, spec.Distal, spec.Radius * 0.86f, skin.Flesh);

                joints[i, 0] = proximal.transform;
                joints[i, 1] = middle.transform;
                joints[i, 2] = distal.transform;
            }

            HandRig handRig = wrist.gameObject.AddComponent<HandRig>();
            SerializedWiring.SetEnum(handRig, "_side", side == HandSide.Left ? 0 : 1);
            SerializedWiring.SetObject(handRig, "_palm", palm.transform);
            SerializedWiring.SetVector3(handRig, "_knuckleOffset", new Vector3(0f, -0.004f, 0.042f));

            SerializedObject so = SerializedWiring.Open(handRig);
            SerializedProperty fingers = so.FindProperty("_fingers");

            if (fingers != null)
            {
                fingers.arraySize = specs.Length;

                for (int i = 0; i < specs.Length; i++)
                {
                    SerializedProperty element = fingers.GetArrayElementAtIndex(i);
                    element.FindPropertyRelative("name").stringValue = specs[i].Name;
                    element.FindPropertyRelative("proximal").objectReferenceValue = joints[i, 0];
                    element.FindPropertyRelative("middle").objectReferenceValue = joints[i, 1];
                    element.FindPropertyRelative("distal").objectReferenceValue = joints[i, 2];
                    element.FindPropertyRelative("proximalCurl").floatValue = specs[i].ProximalCurl;
                    element.FindPropertyRelative("middleCurl").floatValue = specs[i].MiddleCurl;
                    element.FindPropertyRelative("distalCurl").floatValue = specs[i].DistalCurl;
                    element.FindPropertyRelative("curlAxis").vector3Value = Vector3.right;
                    element.FindPropertyRelative("closeDelay").floatValue = specs[i].CloseDelay;
                    element.FindPropertyRelative("curlScale").floatValue = 1f;
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }

            return knuckles.transform;
        }

        private static FingerSpec Finger(string name, Vector3 basePosition, Vector3 baseEuler,
            float proximal, float middle, float distal, float radius,
            float proximalCurl, float middleCurl, float distalCurl, float closeDelay)
        {
            FingerSpec spec = new FingerSpec();
            spec.Name = name;
            spec.Base = basePosition;
            spec.BaseEuler = baseEuler;
            spec.Proximal = proximal;
            spec.Middle = middle;
            spec.Distal = distal;
            spec.Radius = radius;
            spec.ProximalCurl = proximalCurl;
            spec.MiddleCurl = middleCurl;
            spec.DistalCurl = distalCurl;
            spec.CloseDelay = closeDelay;
            return spec;
        }

        // ------------------------------------------------------------------ formes

        /// <summary>Segment d'os conique, orienté sur +Z, longueur et rayon de base donnés.</summary>
        private static void Bone(string name, Transform parent, Quaternion rotation, float length, float radius, Material material)
        {
            ProceduralMeshFactory.CreateVisual(ProceduralMeshFactory.TaperedSegment, name, parent,
                Vector3.zero, rotation, new Vector3(radius * 2f, radius * 2f, length), material);
        }

        /// <summary>Boîte adoucie : ni cube ni sphère, ce qu'il faut pour un torse ou un poing.</summary>
        private static void Box(string name, Transform parent, Vector3 localPosition, Vector3 size, Material material)
        {
            ProceduralMeshFactory.CreateVisual(ProceduralMeshFactory.RoundedBox, name, parent,
                localPosition, Quaternion.identity, size, material);
        }
    }
}
