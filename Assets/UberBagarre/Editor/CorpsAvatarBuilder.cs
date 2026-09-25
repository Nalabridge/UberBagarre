using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// L'avatar humanoïde d'un corps (MakeHuman, voir <see cref="CorpsImporter"/>) : ce qui
    /// permet à Unity de rejouer sur NOS squelettes des animations capturées sur un autre
    /// (FS Melee Combat System). Unity ne recopie pas des rotations d'os — nos axes n'ont rien
    /// à voir avec ceux du squelette d'origine — mais des « muscles » : flexion du coude, rotation
    /// du bras… Pour les lire sur notre corps, il lui faut deux choses :
    ///
    /// - quel os est quoi (notre « Forearm » est son « LowerArm ») ;
    /// - la T-pose du corps : bras à l'horizontale, paumes vers le sol, jambes droites, pieds
    ///   vers l'avant. C'est la référence commune des deux squelettes.
    ///
    /// Nos corps ne sont pas en T-pose : MakeHuman les livre bras le long du corps, écartés,
    /// paumes vers les cuisses, coudes à peine pliés. On les y met ici, par la rotation la plus
    /// courte de chaque segment vers sa direction de T-pose. C'est exactement le mouvement
    /// d'une abduction : bras levés sur les côtés, les paumes tournées vers les cuisses finissent
    /// vers le sol et les plis des coudes vers l'avant — vérifié sur les quatre silhouettes
    /// (axe du coude vertical, dos de la main vers le haut, pouce vers l'avant).
    ///
    /// Les doigts ne sont pas confiés à l'avatar : c'est notre HandRig qui ferme les poings.
    /// </summary>
    public static class CorpsAvatarBuilder
    {
        // À changer si la T-pose ou la correspondance des os change : les avatars déjà
        // enregistrés sont alors reconstruits.
        private const int Version = 1;

        private static readonly string[] Map =
        {
            "Pelvis", "Hips",
            "Spine", "Spine",
            "Chest", "Chest",
            "Neck", "Neck",
            "Head", "Head",
            "LeftClavicle", "LeftShoulder",
            "LeftUpperArm", "LeftUpperArm",
            "LeftForearm", "LeftLowerArm",
            "LeftWrist", "LeftHand",
            "RightClavicle", "RightShoulder",
            "RightUpperArm", "RightUpperArm",
            "RightForearm", "RightLowerArm",
            "RightWrist", "RightHand",
            "LeftThigh", "LeftUpperLeg",
            "LeftShin", "LeftLowerLeg",
            "LeftAnkle", "LeftFoot",
            "LeftToe", "LeftToes",
            "RightThigh", "RightUpperLeg",
            "RightShin", "RightLowerLeg",
            "RightAnkle", "RightFoot",
            "RightToe", "RightToes"
        };

        private static readonly Dictionary<string, Avatar> Built = new Dictionary<string, Avatar>();

        /// <summary>
        /// L'avatar de ce corps, construit une fois par session (et enregistré avec les maillages
        /// générés). <paramref name="rootName"/> est le nom du GameObject qui portera l'Animator :
        /// Unity retrouve les os par leur chemin sous lui. Null si l'avatar ne peut pas être
        /// construit — le combattant garde alors ses poses calculées.
        /// </summary>
        public static Avatar For(CorpsImporter.Data data, string rootName)
        {
            if (data == null) return null;

            string key = data.Name + "_" + rootName;
            Avatar avatar;
            if (Built.TryGetValue(key, out avatar) && avatar != null) return avatar;

            avatar = Build(data, rootName);
            if (avatar == null) return null;

            avatar = Save(avatar, "Avatar_" + data.Name + "_v" + Version);
            Built[key] = avatar;
            return avatar;
        }

        private static Avatar Build(CorpsImporter.Data data, string rootName)
        {
            GameObject root = new GameObject(rootName);

            try
            {
                Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
                Transform[] ordered = new Transform[data.BoneNames.Length];

                for (int i = 0; i < data.BoneNames.Length; i++)
                {
                    GameObject go = new GameObject(data.BoneNames[i]);
                    int parent = data.Parents[i];
                    go.transform.SetParent(parent >= 0 ? ordered[parent] : root.transform, false);
                    go.transform.position = data.BindPositions[i];
                    go.transform.rotation = data.BindRotations[i];
                    ordered[i] = go.transform;
                    bones[data.BoneNames[i]] = go.transform;
                }

                for (int i = 0; i < Map.Length; i += 2)
                {
                    if (!bones.ContainsKey(Map[i]))
                    {
                        Debug.LogWarning("[UberBagarre] Avatar de " + data.Name + " : os « " + Map[i] +
                                         " » absent, animations capturees indisponibles pour ce corps.");
                        return null;
                    }
                }

                PoseT(bones, "Left", Vector3.left);
                PoseT(bones, "Right", Vector3.right);

                HumanDescription description = new HumanDescription();
                description.human = Human();
                description.skeleton = Skeleton(root.transform, ordered);
                description.upperArmTwist = 0.5f;
                description.lowerArmTwist = 0.5f;
                description.upperLegTwist = 0.5f;
                description.lowerLegTwist = 0.5f;
                description.armStretch = 0.05f;
                description.legStretch = 0.05f;
                description.feetSpacing = 0f;
                description.hasTranslationDoF = false;

                Avatar avatar = AvatarBuilder.BuildHumanAvatar(root, description);
                if (avatar == null || !avatar.isValid || !avatar.isHuman)
                {
                    Debug.LogWarning("[UberBagarre] Avatar humanoide invalide pour " + data.Name +
                                     " : animations capturees indisponibles pour ce corps.");
                    if (avatar != null) UnityEngine.Object.DestroyImmediate(avatar);
                    return null;
                }

                avatar.name = "Avatar_" + data.Name;
                return avatar;
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        /// <summary>Un côté du corps en T-pose : bras vers <paramref name="outward"/>, jambe droite, pied vers l'avant.</summary>
        private static void PoseT(Dictionary<string, Transform> bones, string side, Vector3 outward)
        {
            Align(bones[side + "UpperArm"], bones[side + "Forearm"], outward);
            Align(bones[side + "Forearm"], bones[side + "Wrist"], outward);

            Transform middle;
            if (bones.TryGetValue(side + "Majeur1", out middle)) Align(bones[side + "Wrist"], middle, outward);

            Align(bones[side + "Thigh"], bones[side + "Shin"], Vector3.down);
            Align(bones[side + "Shin"], bones[side + "Ankle"], Vector3.down);

            // Le pied de liaison pointe un peu vers l'extérieur (5 degrés) : droit devant en T-pose.
            Transform ankle = bones[side + "Ankle"];
            Vector3 foot = bones[side + "Toe"].position - ankle.position;
            foot.y = 0f;
            if (foot.sqrMagnitude > 1e-8f) ankle.rotation = Quaternion.FromToRotation(foot, Vector3.forward) * ankle.rotation;
        }

        /// <summary>Tourne <paramref name="bone"/> (et ce qu'il porte) pour que son segment pointe vers <paramref name="direction"/>.</summary>
        private static void Align(Transform bone, Transform tip, Vector3 direction)
        {
            Vector3 current = tip.position - bone.position;
            if (current.sqrMagnitude < 1e-10f) return;
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        private static HumanBone[] Human()
        {
            HumanBone[] human = new HumanBone[Map.Length / 2];

            for (int i = 0; i < human.Length; i++)
            {
                HumanBone bone = new HumanBone();
                bone.boneName = Map[i * 2];
                bone.humanName = Map[i * 2 + 1];
                bone.limit.useDefaultValues = true;
                human[i] = bone;
            }

            return human;
        }

        private static SkeletonBone[] Skeleton(Transform root, Transform[] ordered)
        {
            List<SkeletonBone> skeleton = new List<SkeletonBone>(ordered.Length + 1);
            skeleton.Add(Bone(root));
            for (int i = 0; i < ordered.Length; i++) skeleton.Add(Bone(ordered[i]));
            return skeleton.ToArray();
        }

        private static SkeletonBone Bone(Transform transform)
        {
            SkeletonBone bone = new SkeletonBone();
            bone.name = transform.name;
            bone.position = transform.localPosition;
            bone.rotation = transform.localRotation;
            bone.scale = transform.localScale;
            return bone;
        }

        private static Avatar Save(Avatar avatar, string fileName)
        {
            EditorBuildUtility.EnsureFolder(CorpsImporter.GeneratedFolder);
            string path = CorpsImporter.GeneratedFolder + "/" + fileName + ".asset";

            Avatar existing = AssetDatabase.LoadAssetAtPath<Avatar>(path);
            if (existing != null)
            {
                // Mise à jour en place : les scènes déjà construites gardent leur référence.
                EditorUtility.CopySerialized(avatar, existing);
                if (existing.isValid && existing.isHuman)
                {
                    UnityEngine.Object.DestroyImmediate(avatar);
                    EditorUtility.SetDirty(existing);
                    return existing;
                }

                AssetDatabase.DeleteAsset(path);
            }

            AssetDatabase.CreateAsset(avatar, path);
            return avatar;
        }
    }
}
