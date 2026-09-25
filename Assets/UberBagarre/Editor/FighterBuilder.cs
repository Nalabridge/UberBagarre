using UberBagarre.Combat;
using UberBagarre.View;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Construit le corps d'un combattant : squelette, peau, vêtements, mains articulées, hitbox.
    ///
    /// Le MÊME code sert au joueur, aux adversaires et au public. C'est la vérification concrète
    /// de la promesse d'architecture : si le joueur et l'ennemi avaient besoin de deux corps
    /// différents, c'est que le moteur de combat ne serait pas réellement partagé.
    ///
    /// Le corps est un vrai corps humain : un maillage MakeHuman (licence CC0) musclé, rigué,
    /// habillé et peint par Tools/corps/fabrique.py, relu ici par <see cref="CorpsImporter"/>.
    /// Une seule peau déformée par le squelette, des doigts à trois phalanges qui se ferment
    /// en vrai poing — le pouce vient se poser en travers de l'index et du majeur — et des
    /// avant-bras qui tournent autour de leur axe comme un radius autour du cubitus.
    /// </summary>
    public static class FighterBuilder
    {
        /// <summary>Hauteur des yeux du joueur : celle de la caméra.</summary>
        public const float EyeHeight = 1.62f;

        /// <summary>Ce que la construction rend au reste du générateur.</summary>
        public class Result
        {
            public GameObject Body;
            public BodyRig Rig;
            public ProceduralLocomotion Locomotion;
            public Hitbox LeftHitbox;
            public Hitbox RightHitbox;
            public Hitbox LeftFootHitbox;
            public Hitbox RightFootHitbox;

            /// <summary>Hitbox du front. Null sur le joueur : la sienne vit sous la caméra.</summary>
            public Hitbox HeadHitbox;

            /// <summary>La nuque. Les regards (public, figurants) la tournent.</summary>
            public Transform Neck;

            public Transform Head;
            public Transform Pelvis;
            public Transform Chest;

            /// <summary>Le milieu des yeux, dans l'espace du parent passé à BuildBody.</summary>
            public Vector3 EyeLocal;

            public SkinnedMeshRenderer Renderer;
        }

        /// <summary>Palette et silhouette d'un combattant.</summary>
        public struct Skin
        {
            public Material Flesh;
            public Material Shirt;
            public Material Pants;
            public Material Shoe;

            /// <summary>Le corps : Athlete (le joueur), Costaud, Sec, Colosse.</summary>
            public string Silhouette;

            public CorpsImporter.Top Top;

            /// <summary>Version légère (sans subdivision) pour le public du fond de salle.</summary>
            public bool Crowd;

            public static Skin Player(BuildMaterials m)
            {
                Skin s = new Skin();
                s.Flesh = m.Skin; s.Shirt = m.Shirt; s.Pants = m.Pants; s.Shoe = m.Shoe;
                s.Silhouette = "Athlete";
                s.Top = CorpsImporter.Top.TShirt;
                return s;
            }

            public static Skin Enemy(BuildMaterials m)
            {
                Skin s = new Skin();
                s.Flesh = m.EnemySkin; s.Shirt = m.EnemyShirt; s.Pants = m.Pants; s.Shoe = m.Shoe;
                s.Silhouette = "Costaud";
                s.Top = CorpsImporter.Top.Veste;
                return s;
            }
        }

        // Le poing : fermeture de chaque doigt (base, milieu, bout), et la rotation de la base
        // qui les resserre. Réglés par optimisation sur le maillage (Tools/corps : pouce posé
        // sur les phalanges moyennes de l'index et du majeur, sans interpénétration).
        private static readonly string[] FingerNames = { "Index", "Majeur", "Annulaire", "Auriculaire", "Pouce" };
        private static readonly Vector3[] FistCurls =
        {
            new Vector3(70f, 98f, 62f),
            new Vector3(70f, 100f, 64f),
            new Vector3(70f, 100f, 64f),
            new Vector3(68f, 98f, 60f),
            new Vector3(0f, 16.3f, 64f)
        };

        // Main DROITE ; la gauche est son miroir (lacet et roulis inversés).
        private static readonly Vector3[] FistRotations =
        {
            new Vector3(0f, 8f, 0f),
            new Vector3(0f, -4f, 0f),
            new Vector3(0f, -14f, 0f),
            new Vector3(0f, -20f, 0f),
            new Vector3(20.2f, 7.4f, -25.9f)
        };

        private static readonly float[] CloseDelays = { 0.15f, 0.08f, 0.03f, 0f, 0.35f };

        /// <summary>
        /// Construit le corps.
        ///
        /// <paramref name="parent"/> est l'objet SOUS lequel le corps vit — en pratique le nœud
        /// d'inclinaison, qui basculera quand le combattant tombe.
        /// <paramref name="motionRoot"/> est la racine du personnage, qui reste TOUJOURS debout.
        ///
        /// Sans tête (<paramref name="withHead"/> faux), c'est le corps du joueur : il est
        /// reculé pour que ses yeux tombent exactement sur la caméra — on voit ses propres
        /// épaules, son torse et ses pieds en baissant les yeux, comme dans la vraie vie.
        /// </summary>
        public static Result BuildBody(Transform parent, Transform motionRoot, Skin skin, bool withHead,
            Faction faction, GameObject owner)
        {
            if (motionRoot == null) motionRoot = parent;
            if (string.IsNullOrEmpty(skin.Silhouette)) skin.Silhouette = withHead ? "Costaud" : "Athlete";
            if (skin.Top == 0) skin.Top = withHead ? CorpsImporter.Top.Veste : CorpsImporter.Top.TShirt;

            Result result = new Result();
            result.Body = EditorBuildUtility.CreateEmpty("Body", parent, Vector3.zero);

            CorpsImporter.Data data = CorpsImporter.Load(skin.Silhouette, skin.Crowd);
            if (data == null) return result;

            Vector3 eyes = data.BonePosition("Yeux");

            // Joueur : les yeux du corps sur la caméra (0, EyeHeight, 0).
            if (!withHead)
            {
                result.Body.transform.localPosition = new Vector3(0f, EyeHeight - eyes.y, -eyes.z);
            }

            result.EyeLocal = result.Body.transform.localPosition + eyes;

            System.Collections.Generic.List<string> slots;
            Mesh mesh = CorpsImporter.BuildMesh(data, skin.Top, withHead, out slots);
            Material[] materials = CorpsImporter.MaterialsFor(data.Name, slots, skin.Shirt, skin.Pants, skin.Shoe);
            CorpsImporter.Built built = CorpsImporter.BuildSkeleton(data, result.Body.transform, mesh, materials);
            result.Renderer = built.Renderer;

            // Le joueur voit ses bras de tout près : ombres propres, pas de sauts de qualité.
            if (!withHead)
            {
                built.Renderer.updateWhenOffscreen = true;

                // Son corps visible n'a pas de tête (on est dedans) — mais son OMBRE en a une.
                // Un second rendu, ombre seulement, porte le corps entier ; le corps visible ne
                // projette rien. Sans ça, une silhouette sans tête marche à côté de toi au sol.
                System.Collections.Generic.List<string> shadowSlots;
                Mesh whole = CorpsImporter.BuildMesh(data, skin.Top, true, out shadowSlots);
                Material[] shadowMaterials = CorpsImporter.MaterialsFor(data.Name, shadowSlots, skin.Shirt, skin.Pants, skin.Shoe);

                GameObject shadowGo = new GameObject("Ombre");
                shadowGo.transform.SetParent(result.Body.transform, false);
                SkinnedMeshRenderer shadow = shadowGo.AddComponent<SkinnedMeshRenderer>();
                shadow.sharedMesh = whole;
                shadow.bones = built.Bones;
                shadow.rootBone = built["Pelvis"];
                shadow.sharedMaterials = shadowMaterials;
                shadow.localBounds = built.Renderer.localBounds;
                shadow.updateWhenOffscreen = true;
                shadow.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.ShadowsOnly;

                built.Renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            Transform pelvis = built["Pelvis"];
            Transform spine = built["Spine"];
            Transform chest = built["Chest"];
            Transform neck = built["Neck"];
            Transform head = built["Head"];

            if (withHead)
            {
                // Le front : quatre centimètres et demi au-dessus des yeux, un peu en avant.
                // C'est l'os frontal qui frappe, pas le nez.
                Vector3 forehead = eyes + new Vector3(0f, 0.045f, 0.022f);
                result.HeadHitbox = AddHeadHitbox(head, head.InverseTransformPoint(result.Body.transform.TransformPoint(forehead)),
                    faction, owner);
            }

            IkLimb leftArm = BuildArm(built, result.Body.transform, HandSide.Left, faction, owner);
            IkLimb rightArm = BuildArm(built, result.Body.transform, HandSide.Right, faction, owner);
            IkLimb leftLeg = BuildLeg(built, result.Body.transform, true, faction, owner);
            IkLimb rightLeg = BuildLeg(built, result.Body.transform, false, faction, owner);

            result.LeftHitbox = leftArm.End.GetComponent<Hitbox>();
            result.RightHitbox = rightArm.End.GetComponent<Hitbox>();
            result.LeftFootHitbox = leftLeg.End.GetComponent<Hitbox>();
            result.RightFootHitbox = rightLeg.End.GetComponent<Hitbox>();
            result.Neck = neck;
            result.Head = head;
            result.Pelvis = pelvis;
            result.Chest = chest;

            result.Rig = result.Body.AddComponent<BodyRig>();
            SerializedWiring.SetObject(result.Rig, "_pelvis", pelvis);
            SerializedWiring.SetObject(result.Rig, "_spine", spine);
            SerializedWiring.SetObject(result.Rig, "_chest", chest);
            SerializedWiring.SetObject(result.Rig, "_neck", neck);
            SerializedWiring.SetObject(result.Rig, "_head", head);
            SerializedWiring.SetObject(result.Rig, "_leftClavicle", built["LeftClavicle"]);
            SerializedWiring.SetObject(result.Rig, "_rightClavicle", built["RightClavicle"]);
            SerializedWiring.SetObject(result.Rig, "_leftLeg", leftLeg);
            SerializedWiring.SetObject(result.Rig, "_rightLeg", rightLeg);
            SerializedWiring.SetObject(result.Rig, "_leftArm", leftArm);
            SerializedWiring.SetObject(result.Rig, "_rightArm", rightArm);
            SerializedWiring.SetObject(result.Rig, "_leftHand", leftArm.End.GetComponent<HandRig>());
            SerializedWiring.SetObject(result.Rig, "_rightHand", rightArm.End.GetComponent<HandRig>());

            result.Locomotion = result.Body.AddComponent<ProceduralLocomotion>();
            SerializedWiring.SetObject(result.Locomotion, "_rig", result.Rig);
            SerializedWiring.SetObject(result.Locomotion, "_root", motionRoot);

            // La cible d'IK est la cheville : sa hauteur réelle au-dessus du sol.
            SerializedWiring.SetFloat(result.Locomotion, "_ankleHeight", data.BonePosition("LeftAnkle").y);

            return result;
        }

        // ------------------------------------------------------------------ bras

        private static IkLimb BuildArm(CorpsImporter.Built built, Transform poleSpace, HandSide side,
            Faction faction, GameObject owner)
        {
            bool isLeft = side == HandSide.Left;
            float sign = isLeft ? -1f : 1f;
            string prefix = isLeft ? "Left" : "Right";

            Transform shoulder = built[prefix + "Shoulder"];
            Transform upperArm = built[prefix + "UpperArm"];
            Transform forearm = built[prefix + "Forearm"];
            Transform twist = built[prefix + "ForearmTwist"];
            Transform wrist = built[prefix + "Wrist"];
            Transform palm = built[prefix + "Palm"];
            Transform knuckles = built[prefix + "Knuckles"];

            BuildHand(built, wrist, palm, knuckles, side);

            Hitbox hitbox = wrist.gameObject.AddComponent<Hitbox>();
            SerializedWiring.SetObject(hitbox, "_origin", knuckles);
            SerializedWiring.SetEnum(hitbox, "_ownerFaction", (int)faction);
            SerializedWiring.SetObject(hitbox, "_owner", owner);

            IkLimb limb = shoulder.gameObject.AddComponent<IkLimb>();
            SerializedWiring.SetObject(limb, "_upper", upperArm);
            SerializedWiring.SetObject(limb, "_lower", forearm);
            SerializedWiring.SetObject(limb, "_end", wrist);
            SerializedWiring.SetBool(limb, "_autoMeasureLengths", true);
            SerializedWiring.SetObject(limb, "_poleSpace", poleSpace);
            SerializedWiring.SetVector3(limb, "_poleDirection", new Vector3(sign * 0.25f, -1f, -0.35f));
            SerializedWiring.SetBool(limb, "_hingeMode", true);
            SerializedWiring.SetObject(limb, "_twist", twist);
            SerializedWiring.SetFloat(limb, "_twistShare", 0.5f);

            return limb;
        }

        private static void BuildHand(CorpsImporter.Built built, Transform wrist, Transform palm, Transform knuckles, HandSide side)
        {
            string prefix = side == HandSide.Left ? "Left" : "Right";
            float mirror = side == HandSide.Left ? -1f : 1f;

            HandRig handRig = wrist.gameObject.AddComponent<HandRig>();
            SerializedWiring.SetEnum(handRig, "_side", side == HandSide.Left ? 0 : 1);
            SerializedWiring.SetObject(handRig, "_palm", palm);
            SerializedWiring.SetVector3(handRig, "_knuckleOffset", palm.InverseTransformPoint(knuckles.position));

            SerializedObject so = SerializedWiring.Open(handRig);
            SerializedProperty fingers = so.FindProperty("_fingers");
            if (fingers == null) return;

            fingers.arraySize = FingerNames.Length;

            for (int i = 0; i < FingerNames.Length; i++)
            {
                string name = FingerNames[i];
                Vector3 fist = FistRotations[i];
                fist.y *= mirror;
                fist.z *= mirror;

                SerializedProperty element = fingers.GetArrayElementAtIndex(i);
                element.FindPropertyRelative("name").stringValue = name;
                element.FindPropertyRelative("proximal").objectReferenceValue = built[prefix + name + "1"];
                element.FindPropertyRelative("middle").objectReferenceValue = built[prefix + name + "2"];
                element.FindPropertyRelative("distal").objectReferenceValue = built[prefix + name + "3"];
                element.FindPropertyRelative("proximalCurl").floatValue = FistCurls[i].x;
                element.FindPropertyRelative("middleCurl").floatValue = FistCurls[i].y;
                element.FindPropertyRelative("distalCurl").floatValue = FistCurls[i].z;
                element.FindPropertyRelative("curlAxis").vector3Value = Vector3.right;
                element.FindPropertyRelative("closeDelay").floatValue = CloseDelays[i];
                element.FindPropertyRelative("curlScale").floatValue = 1f;
                element.FindPropertyRelative("fistRotation").vector3Value = fist;
            }

            so.ApplyModifiedPropertiesWithoutUndo();
        }

        // ------------------------------------------------------------------ jambes

        private static IkLimb BuildLeg(CorpsImporter.Built built, Transform poleSpace, bool isLeft,
            Faction faction, GameObject owner)
        {
            float sign = isLeft ? -1f : 1f;
            string prefix = isLeft ? "Left" : "Right";

            Transform hip = built[prefix + "Hip"];
            Transform ankle = built[prefix + "Ankle"];
            Transform toe = built[prefix + "Toe"];

            // Le coup de pied part de la POINTE, pas de la cheville ni de la plante : quelques
            // centimètres, mais c'est la différence entre un coup qui touche et un qui passe à côté.
            GameObject tip = EditorBuildUtility.CreateEmpty("PointeDuPied", toe, new Vector3(0f, 0.01f, 0.06f));

            Hitbox footHitbox = ankle.gameObject.AddComponent<Hitbox>();
            SerializedWiring.SetObject(footHitbox, "_origin", tip.transform);
            SerializedWiring.SetEnum(footHitbox, "_ownerFaction", (int)faction);
            SerializedWiring.SetObject(footHitbox, "_owner", owner);

            IkLimb limb = hip.gameObject.AddComponent<IkLimb>();
            SerializedWiring.SetObject(limb, "_upper", built[prefix + "Thigh"]);
            SerializedWiring.SetObject(limb, "_lower", built[prefix + "Shin"]);
            SerializedWiring.SetObject(limb, "_end", ankle);
            SerializedWiring.SetBool(limb, "_autoMeasureLengths", true);
            SerializedWiring.SetObject(limb, "_poleSpace", poleSpace);
            SerializedWiring.SetVector3(limb, "_poleDirection", new Vector3(sign * 0.15f, 0.35f, 1f));
            SerializedWiring.SetBool(limb, "_hingeMode", true);

            return limb;
        }

        // ------------------------------------------------------------------ tête

        /// <summary>
        /// Hitbox du front, pour le coup de tête. Son origine est un point au-dessus des sourcils :
        /// c'est l'os frontal qui frappe, pas le nez — et un coup porté du nez se voit tout de
        /// suite comme une erreur d'animation.
        /// </summary>
        public static Hitbox AddHeadHitbox(Transform parent, Vector3 localPosition, Faction faction, GameObject owner)
        {
            GameObject forehead = EditorBuildUtility.CreateEmpty("Front", parent, localPosition);

            Hitbox hitbox = forehead.AddComponent<Hitbox>();
            SerializedWiring.SetObject(hitbox, "_origin", forehead.transform);
            SerializedWiring.SetEnum(hitbox, "_ownerFaction", (int)faction);
            SerializedWiring.SetObject(hitbox, "_owner", owner);

            return hitbox;
        }
    }
}
