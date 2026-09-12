using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Player;
using UberBagarre.View;
using UnityEditor;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Branche un modèle 3D rigué (Mixamo, asset store, modèle maison) à la place des primitives.
    ///
    /// Le principe : on ne remplace PAS le système d'animation, on lui donne d'autres os.
    /// Les composants (BodyRig, IkLimb, HandRig, Hitbox) restent exactement les mêmes et
    /// gardent tout leur câblage ; seules les références d'os changent. Tous les réglages
    /// de combat, de garde et de locomotion sont donc conservés.
    ///
    /// Le modèle doit être importé en **Animation Type = Humanoid** : c'est ce qui permet à
    /// Unity de dire quel os est le coude gauche, sans avoir à deviner des noms d'os qui
    /// changent d'un logiciel à l'autre.
    /// </summary>
    public static class HumanoidModelBinder
    {
        [MenuItem("Uber Bagarre/5 - Brancher le modele 3D selectionne", false, 50)]
        public static void BindSelected()
        {
            GameObject model = Selection.activeGameObject;

            if (model == null)
            {
                EditorUtility.DisplayDialog("Aucun modele selectionne",
                    "Glisse d'abord ton modele 3D dans la scene, selectionne-le dans la Hierarchy, " +
                    "puis relance cette commande.", "OK");
                return;
            }

            Animator animator = model.GetComponentInChildren<Animator>();
            if (animator == null || animator.avatar == null || !animator.isHuman)
            {
                EditorUtility.DisplayDialog("Modele non humanoide",
                    "Ce modele n'a pas de rig Humanoid.\n\n" +
                    "Selectionne le fichier du modele dans le dossier Project, puis dans l'Inspector :\n" +
                    "onglet Rig > Animation Type = Humanoid > Apply.\n\n" +
                    "Glisse ensuite le modele dans la scene et relance cette commande.", "OK");
                return;
            }

            PlayerMotor player = FindPlayer();
            if (player == null)
            {
                EditorUtility.DisplayDialog("Joueur introuvable",
                    "Aucun joueur dans la scene ouverte. Genere d'abord la scene :\n" +
                    "Uber Bagarre > 2 - Construire la scene Combat Sandbox.", "OK");
                return;
            }

            Bind(player, model, animator);
        }

        private static PlayerMotor FindPlayer()
        {
            Scene scene = SceneManager.GetActiveScene();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < roots.Length; i++)
            {
                PlayerMotor motor = roots[i].GetComponentInChildren<PlayerMotor>(true);
                if (motor != null) return motor;
            }

            return null;
        }

        private static void Bind(PlayerMotor player, GameObject model, Animator animator)
        {
            BodyRig rig = player.GetComponentInChildren<BodyRig>(true);
            ProceduralLocomotion locomotion = player.GetComponentInChildren<ProceduralLocomotion>(true);

            if (rig == null)
            {
                EditorUtility.DisplayDialog("Corps introuvable",
                    "Le joueur n'a pas de BodyRig. Regenere la scene avant de brancher un modele.", "OK");
                return;
            }

            Undo.RegisterFullObjectHierarchyUndo(player.gameObject, "Brancher un modele 3D");

            // Le modele se place a la racine du joueur : ses pieds sont alors au niveau du sol,
            // et il suit le lacet sans suivre le tangage de la camera.
            Undo.SetTransformParent(model.transform, player.transform, "Brancher un modele 3D");
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.identity;

            List<string> report = new List<string>();

            BindSpine(rig, animator, report);
            BindArm(rig, player.transform, animator, true, report);
            BindArm(rig, player.transform, animator, false, report);
            BindLeg(rig, player.transform, animator, true, report);
            BindLeg(rig, player.transform, animator, false, report);

            if (locomotion != null) BindAnkleHeight(locomotion, player.transform, animator, report);

            HidePlaceholderVisuals(rig, model, report);

            // L'Animator ne doit plus toucher aux os : c'est notre systeme qui les pilote.
            animator.enabled = false;

            rig.CaptureRestPose();
            EditorUtility.SetDirty(player.gameObject);
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(player.gameObject.scene);

            Debug.Log("[UberBagarre] Modele branche : " + model.name + "\n  " + string.Join("\n  ", report.ToArray()), player);
        }

        // ------------------------------------------------------------------ colonne

        private static void BindSpine(BodyRig rig, Animator animator, List<string> report)
        {
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform spine = animator.GetBoneTransform(HumanBodyBones.Spine);
            Transform chest = animator.GetBoneTransform(HumanBodyBones.UpperChest);
            if (chest == null) chest = animator.GetBoneTransform(HumanBodyBones.Chest);

            Transform neck = animator.GetBoneTransform(HumanBodyBones.Neck);
            if (neck == null) neck = animator.GetBoneTransform(HumanBodyBones.Head);

            SerializedWiring.SetObject(rig, "_pelvis", hips);
            SerializedWiring.SetObject(rig, "_spine", spine);
            SerializedWiring.SetObject(rig, "_chest", chest);
            SerializedWiring.SetObject(rig, "_neck", neck);

            report.Add("Colonne : " + Name(hips) + " / " + Name(spine) + " / " + Name(chest));
        }

        // ------------------------------------------------------------------ bras et mains

        private static void BindArm(BodyRig rig, Transform root, Animator animator, bool isLeft, List<string> report)
        {
            IkLimb limb = isLeft ? rig.LeftArm : rig.RightArm;
            if (limb == null) return;

            Transform upper = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperArm : HumanBodyBones.RightUpperArm);
            Transform lower = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerArm : HumanBodyBones.RightLowerArm);
            Transform hand = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand);

            if (upper == null || lower == null || hand == null)
            {
                report.Add("Bras " + Side(isLeft) + " : os manquants, ignore");
                return;
            }

            SerializedWiring.SetObject(limb, "_upper", upper);
            SerializedWiring.SetObject(limb, "_lower", lower);
            SerializedWiring.SetObject(limb, "_end", hand);
            SerializedWiring.SetObject(limb, "_poleSpace", root);
            SerializedWiring.SetBool(limb, "_autoMeasureLengths", true);
            limb.CaptureBindPose();

            HandRig handRig = isLeft ? rig.LeftHand : rig.RightHand;
            if (handRig != null) BindHand(handRig, animator, hand, isLeft, report);

            report.Add("Bras " + Side(isLeft) + " : " + Name(upper) + " > " + Name(lower) + " > " + Name(hand) +
                       "  (" + limb.TotalLength.ToString("0.00") + " m)");
        }

        private static void BindHand(HandRig handRig, Animator animator, Transform hand, bool isLeft, List<string> report)
        {
            HumanBodyBones[,] fingerBones = isLeft
                ? new[,]
                {
                    { HumanBodyBones.LeftIndexProximal, HumanBodyBones.LeftIndexIntermediate, HumanBodyBones.LeftIndexDistal },
                    { HumanBodyBones.LeftMiddleProximal, HumanBodyBones.LeftMiddleIntermediate, HumanBodyBones.LeftMiddleDistal },
                    { HumanBodyBones.LeftRingProximal, HumanBodyBones.LeftRingIntermediate, HumanBodyBones.LeftRingDistal },
                    { HumanBodyBones.LeftLittleProximal, HumanBodyBones.LeftLittleIntermediate, HumanBodyBones.LeftLittleDistal },
                    { HumanBodyBones.LeftThumbProximal, HumanBodyBones.LeftThumbIntermediate, HumanBodyBones.LeftThumbDistal }
                }
                : new[,]
                {
                    { HumanBodyBones.RightIndexProximal, HumanBodyBones.RightIndexIntermediate, HumanBodyBones.RightIndexDistal },
                    { HumanBodyBones.RightMiddleProximal, HumanBodyBones.RightMiddleIntermediate, HumanBodyBones.RightMiddleDistal },
                    { HumanBodyBones.RightRingProximal, HumanBodyBones.RightRingIntermediate, HumanBodyBones.RightRingDistal },
                    { HumanBodyBones.RightLittleProximal, HumanBodyBones.RightLittleIntermediate, HumanBodyBones.RightLittleDistal },
                    { HumanBodyBones.RightThumbProximal, HumanBodyBones.RightThumbIntermediate, HumanBodyBones.RightThumbDistal }
                };

            string[] names = { "Index", "Majeur", "Annulaire", "Auriculaire", "Pouce" };
            float[] curlScale = { 1f, 1f, 1f, 1f, 0.55f };
            float[] closeDelay = { 0.15f, 0.08f, 0.03f, 0f, 0.35f };

            Transform index = animator.GetBoneTransform(fingerBones[0, 0]);
            Transform little = animator.GetBoneTransform(fingerBones[3, 0]);
            Transform middle = animator.GetBoneTransform(fingerBones[1, 0]);

            Vector3 handUp = ComputeHandUp(hand, index, little, middle, isLeft);

            SerializedWiring.SetObject(handRig, "_palm", hand);

            if (middle != null)
            {
                SerializedWiring.SetVector3(handRig, "_knuckleOffset", hand.InverseTransformPoint(middle.position));
            }

            SerializedObject so = SerializedWiring.Open(handRig);
            SerializedProperty fingers = so.FindProperty("_fingers");
            if (fingers == null) return;

            int bound = 0;
            fingers.arraySize = names.Length;

            for (int i = 0; i < names.Length; i++)
            {
                Transform proximal = animator.GetBoneTransform(fingerBones[i, 0]);
                Transform intermediate = animator.GetBoneTransform(fingerBones[i, 1]);
                Transform distal = animator.GetBoneTransform(fingerBones[i, 2]);

                SerializedProperty element = fingers.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = names[i];
                element.FindPropertyRelative("proximal").objectReferenceValue = proximal;
                element.FindPropertyRelative("middle").objectReferenceValue = intermediate;
                element.FindPropertyRelative("distal").objectReferenceValue = distal;
                element.FindPropertyRelative("proximalCurl").floatValue = i == 4 ? 42f : 80f;
                element.FindPropertyRelative("middleCurl").floatValue = i == 4 ? 46f : 96f;
                element.FindPropertyRelative("distalCurl").floatValue = i == 4 ? 30f : 64f;
                element.FindPropertyRelative("closeDelay").floatValue = closeDelay[i];
                element.FindPropertyRelative("curlScale").floatValue = curlScale[i];
                element.FindPropertyRelative("curlAxis").vector3Value = ComputeCurlAxis(proximal, intermediate, handUp);

                if (proximal != null) bound++;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
            report.Add("Main " + Side(isLeft) + " : " + bound + "/5 doigts branches");
        }

        /// <summary>
        /// Normale du dos de la main, déduite de la géométrie réelle du modèle.
        /// Sans elle, impossible de savoir dans quel sens un doigt doit se refermer : chaque
        /// logiciel de rig oriente les phalanges différemment.
        /// </summary>
        private static Vector3 ComputeHandUp(Transform hand, Transform index, Transform little, Transform middle, bool isLeft)
        {
            if (index == null || little == null) return hand.up;

            Vector3 across = isLeft ? little.position - index.position : index.position - little.position;
            Vector3 fingerDirection = middle != null ? middle.position - hand.position : hand.forward;

            Vector3 up = Vector3.Cross(across.normalized, fingerDirection.normalized);
            return up.sqrMagnitude < 1e-6f ? hand.up : up.normalized;
        }

        /// <summary>Axe de flexion d'un doigt : perpendiculaire au doigt et au dos de la main.</summary>
        private static Vector3 ComputeCurlAxis(Transform proximal, Transform intermediate, Vector3 handUp)
        {
            if (proximal == null) return Vector3.right;

            Vector3 direction = intermediate != null
                ? (intermediate.position - proximal.position).normalized
                : proximal.forward;

            Vector3 axis = Vector3.Cross(handUp, direction);
            if (axis.sqrMagnitude < 1e-6f) return Vector3.right;

            return proximal.InverseTransformDirection(axis.normalized);
        }

        // ------------------------------------------------------------------ jambes

        private static void BindLeg(BodyRig rig, Transform root, Animator animator, bool isLeft, List<string> report)
        {
            IkLimb limb = rig.Leg(isLeft);
            if (limb == null) return;

            Transform upper = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftUpperLeg : HumanBodyBones.RightUpperLeg);
            Transform lower = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftLowerLeg : HumanBodyBones.RightLowerLeg);
            Transform foot = animator.GetBoneTransform(isLeft ? HumanBodyBones.LeftFoot : HumanBodyBones.RightFoot);

            if (upper == null || lower == null || foot == null)
            {
                report.Add("Jambe " + Side(isLeft) + " : os manquants, ignore");
                return;
            }

            SerializedWiring.SetObject(limb, "_upper", upper);
            SerializedWiring.SetObject(limb, "_lower", lower);
            SerializedWiring.SetObject(limb, "_end", foot);
            SerializedWiring.SetObject(limb, "_poleSpace", root);
            SerializedWiring.SetBool(limb, "_autoMeasureLengths", true);
            limb.CaptureBindPose();

            report.Add("Jambe " + Side(isLeft) + " : " + Name(upper) + " > " + Name(lower) + " > " + Name(foot) +
                       "  (" + limb.TotalLength.ToString("0.00") + " m)");
        }

        /// <summary>
        /// Mesure la hauteur réelle de la cheville du modèle au repos.
        /// La cible d'IK des jambes est la cheville, pas la semelle : sans cette mesure, le
        /// modèle s'enfoncerait dans le sol ou flotterait au-dessus.
        /// </summary>
        private static void BindAnkleHeight(ProceduralLocomotion locomotion, Transform root, Animator animator, List<string> report)
        {
            Transform foot = animator.GetBoneTransform(HumanBodyBones.LeftFoot);
            if (foot == null) return;

            float height = Mathf.Max(0.01f, foot.position.y - root.position.y);
            SerializedWiring.SetFloat(locomotion, "_ankleHeight", height);
            report.Add("Hauteur de cheville mesuree : " + height.ToString("0.000") + " m");
        }

        // ------------------------------------------------------------------ nettoyage

        /// <summary>
        /// Éteint les primitives sans les détruire : si le branchement se passe mal, il suffit
        /// de les rallumer pour revenir à un corps fonctionnel.
        /// </summary>
        private static void HidePlaceholderVisuals(BodyRig rig, GameObject model, List<string> report)
        {
            MeshRenderer[] renderers = rig.GetComponentsInChildren<MeshRenderer>(true);
            int hidden = 0;

            for (int i = 0; i < renderers.Length; i++)
            {
                if (renderers[i].transform.IsChildOf(model.transform)) continue;

                renderers[i].enabled = false;
                hidden++;
            }

            report.Add(hidden + " primitives masquees (non detruites : decochables pour revenir en arriere)");
        }

        private static string Name(Transform t)
        {
            return t == null ? "(absent)" : t.name;
        }

        private static string Side(bool isLeft)
        {
            return isLeft ? "gauche" : "droit";
        }
    }
}
