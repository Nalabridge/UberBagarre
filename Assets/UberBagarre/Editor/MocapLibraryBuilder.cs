using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Feedback;
using UberBagarre.View;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Range les animations capturées de FS Melee Combat System (Fantacode Studios, Asset Store)
    /// dans une <see cref="MocapLibrary"/> : quel clip joue quel coup de NOTRE jeu, de quel côté,
    /// avec quel membre, et où tombe son impact.
    ///
    /// Les membres et les instants d'impact ont été mesurés sur les clips eux-mêmes (le poing ou
    /// le pied qui accélère puis s'arrête net), pas devinés d'après les noms : « Left Body
    /// Punch » frappe bien du gauche, mais son impact tombe à 60 % du clip quand celui du direct
    /// tombe à 52 %. Un clip sans mesure (impact négatif) serait mesuré au lancement du jeu.
    ///
    /// Sans le paquet (dossier absent), rien n'est construit et les adversaires gardent leurs
    /// poses calculées : le jeu ne dépend pas de l'achat.
    /// </summary>
    public static class MocapLibraryBuilder
    {
        public const string FsRoot = "Assets/Fantacode Studios/";
        public const string Folder = "Assets/UberBagarre/Art/Animations/Generes";
        public const string AssetPath = Folder + "/Mocap_FS.asset";

        private const string Unarmed = "Melee Combat System/Animations/Unarmed/";
        private const string Moves = Unarmed + "Attacks and Reactions/";
        private const string Steps = Unarmed + "Locomotion/";
        private const string Core = "Combat Core/Animations/";
        private const string Walk = "Third Person Controller/Animations/Locomotion/";

        private static MocapLibrary _built;

        /// <summary>Vrai si le paquet FS Melee Combat System est présent dans le projet.</summary>
        public static bool PackagePresent
        {
            get { return AssetDatabase.IsValidFolder(FsRoot + "Melee Combat System"); }
        }

        /// <summary>
        /// La bibliothèque, construite une fois par session. Null sans le paquet, ou s'il manque
        /// la garde (le seul clip indispensable).
        /// </summary>
        public static MocapLibrary Build()
        {
            if (_built != null) return _built;
            if (!PackagePresent) return null;

            MocapLibrary library = AssetDatabase.LoadAssetAtPath<MocapLibrary>(AssetPath);
            bool created = library == null;
            if (created) library = ScriptableObject.CreateInstance<MocapLibrary>();

            Fill(library);

            if (!library.IsUsable)
            {
                Debug.LogWarning("[UberBagarre] FS Melee Combat System present mais incomplet (garde ou coups " +
                                 "introuvables) : les adversaires gardent leurs poses calculees.");
                if (created) Object.DestroyImmediate(library);
                return null;
            }

            if (created)
            {
                EditorBuildUtility.EnsureFolder(Folder);
                AssetDatabase.CreateAsset(library, AssetPath);
            }
            else
            {
                EditorUtility.SetDirty(library);
            }

            _built = library;
            Debug.Log("[UberBagarre] Animations capturees (FS Melee Combat System) : " + library.moves.Count +
                      " coups, " + (library.lightReactions.Count + library.heavyReactions.Count) +
                      " reactions, chutes et releve ranges dans " + AssetPath + ".");
            return library;
        }

        private static void Fill(MocapLibrary library)
        {
            // Garde de boxe qui danse, pas chassés, et la marche tranquille d'avant la bagarre.
            library.combatIdle = Clip(Steps + "Combat Idle.fbx");
            library.walkForward = Clip(Steps + "Combat Walk Fwd.fbx");
            library.walkBack = Clip(Steps + "Combat Walk Back.fbx");
            library.walkLeft = Clip(Steps + "Combat Walk Left.fbx");
            library.walkRight = Clip(Steps + "Combat Walk Right.fbx");
            library.walkSpeed = 1.66f;
            library.strafeSpeed = 1.34f;
            library.relaxedIdle = Clip(Walk + "Idle.fbx");
            library.relaxedWalk = Clip(Walk + "Walk.fbx");
            library.relaxedWalkSpeed = 1.3f;

            library.block = Clip(Moves + "Block.fbx");
            library.blockHit = Clip(Moves + "Block Hit.fbx");
            library.dodgeBack = Clip(Moves + "Dodge Back.fbx");

            library.knockDownBack = Clip(Core + "Knock Down Back.fbx");
            library.knockDownFront = Clip(Core + "Knock Down Front.fbx");
            library.lyingDown = Clip(Moves + "Lying Down.fbx");
            library.gettingUp = Clip(Core + "Getting Up.fbx");
            library.groundHit = Clip(Moves + "Ground Attack Reaction.fbx");

            // Les réactions restent debout (les clips qui finissent au sol sont des chutes, et
            // c'est notre système de chute qui décide d'une chute).
            library.lightReactions = Clips(Core + "Left Hit.fbx", Core + "Right Hit.fbx");
            library.heavyReactions = Clips(Moves + "Round Kick Reaction.fbx", Moves + "Spin Kick Reaction.fbx");
            library.uppercutReaction = Clip(Moves + "Uppercut Reaction.fbx");
            library.counterReactions = Clips(Moves + "Left Jab Counter - Reaction.fbx");

            List<MocapMove> moves = new List<MocapMove>();

            // Directs : jab du gauche, cross du droit ; au corps, les coups au foie et au plexus.
            Add(moves, AttackData.StraightAsset, HandSide.Left, false, MocapLimb.LeftHand, "Left Jab.fbx", 0.53f);
            Add(moves, AttackData.StraightAsset, HandSide.Right, false, MocapLimb.RightHand, "Right Cross.fbx", 0.52f);
            Add(moves, AttackData.StraightAsset, HandSide.Left, true, MocapLimb.LeftHand, "Left Body Punch.fbx", 0.60f);
            Add(moves, AttackData.StraightAsset, HandSide.Right, true, MocapLimb.RightHand, "Right Body Punch.fbx", 0.48f);

            // Crochets : deux gauches, et à droite le crochet, le coup par-dessus et la gifle.
            Add(moves, AttackData.HookAsset, HandSide.Left, false, MocapLimb.LeftHand, "Left Hook.fbx", 0.28f);
            Add(moves, AttackData.HookAsset, HandSide.Left, false, MocapLimb.LeftHand, "lefthook 5.fbx", 0.37f);
            Add(moves, AttackData.HookAsset, HandSide.Right, false, MocapLimb.RightHand, "Right Hook.fbx", 0.34f);
            Add(moves, AttackData.HookAsset, HandSide.Right, false, MocapLimb.RightHand, "Right Overhand.fbx", 0.40f);
            Add(moves, AttackData.HookAsset, HandSide.Right, false, MocapLimb.RightHand, "Right Slap.fbx", 0.37f);
            Add(moves, AttackData.HookAsset, HandSide.Left, true, MocapLimb.LeftHand, "Left Body Punch.fbx", 0.60f);
            Add(moves, AttackData.HookAsset, HandSide.Right, true, MocapLimb.RightHand, "Right Body Punch.fbx", 0.48f);

            // L'uppercut capturé est un gauche : l'exécuteur le jouera du gauche.
            Add(moves, AttackData.UppercutAsset, HandSide.Left, false, MocapLimb.LeftHand, "Uppercut.fbx", 0.40f);

            // Coups de pied (jambe droite) : le coup de pied de face et le circulaire.
            Add(moves, AttackData.KickAsset, HandSide.Right, false, MocapLimb.RightFoot, "Finisher Kick.fbx", 0.64f);
            Add(moves, AttackData.KickAsset, HandSide.Right, false, MocapLimb.RightFoot, "Round Kick.fbx", 0.41f);

            // Pas de clip pour le coup de pied bas, le coup de tête ni la bousculade : le corps
            // calculé les joue (avec un fondu), comme avant.

            library.moves = moves;
        }

        private static void Add(List<MocapMove> moves, string attack, HandSide side, bool body, MocapLimb limb,
            string file, float impact)
        {
            AnimationClip clip = Clip(Moves + file);
            if (clip == null) return;

            MocapMove move = new MocapMove();
            move.attack = attack;
            move.side = side;
            move.body = body;
            move.limb = limb;
            move.clip = clip;
            move.impact = impact;
            moves.Add(move);
        }

        private static List<AnimationClip> Clips(params string[] files)
        {
            List<AnimationClip> clips = new List<AnimationClip>();
            for (int i = 0; i < files.Length; i++)
            {
                AnimationClip clip = Clip(files[i]);
                if (clip != null) clips.Add(clip);
            }

            return clips;
        }

        /// <summary>
        /// Les bruitages du paquet dans l'audio d'impact d'un combattant : vrais coups (légers,
        /// lourds), garde touchée, souffle du poing, corps qui tombe. Sans le paquet, les sons
        /// synthétisés restent.
        /// </summary>
        public static void AssignSounds(ImpactAudio audio)
        {
            if (audio == null || !PackagePresent) return;

            const string sfx = "Melee Combat System/Sfx/";
            SerializedObject so = SerializedWiring.Open(audio);
            SetClips(so, "_punchSamples", sfx + "hit 2.wav", sfx + "hit 3.wav");
            SetClips(so, "_heavySamples", sfx + "hit.wav", sfx + "Slam.wav");
            SetClips(so, "_blockSamples", sfx + "hit blocked.wav");
            SetClips(so, "_whooshSamples", sfx + "hand swoosh.wav");
            SetClips(so, "_fallSamples", sfx + "Fall and Take Hit.wav");
            so.ApplyModifiedPropertiesWithoutUndo();
        }

        private static void SetClips(SerializedObject so, string field, params string[] files)
        {
            SerializedProperty array = so.FindProperty(field);
            if (array == null)
            {
                Debug.LogWarning("[UberBagarre] Champ '" + field + "' introuvable sur ImpactAudio.");
                return;
            }

            List<AudioClip> clips = new List<AudioClip>();
            for (int i = 0; i < files.Length; i++)
            {
                AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(FsRoot + files[i]);
                if (clip != null) clips.Add(clip);
                else Debug.LogWarning("[UberBagarre] Son introuvable : " + FsRoot + files[i]);
            }

            array.arraySize = clips.Count;
            for (int i = 0; i < clips.Count; i++) array.GetArrayElementAtIndex(i).objectReferenceValue = clips[i];
        }

        /// <summary>Le clip d'un FBX du paquet (un seul par fichier), sans l'aperçu que Unity y ajoute.</summary>
        private static AnimationClip Clip(string relative)
        {
            string path = FsRoot + relative;
            Object[] assets = AssetDatabase.LoadAllAssetsAtPath(path);

            for (int i = 0; i < assets.Length; i++)
            {
                AnimationClip clip = assets[i] as AnimationClip;
                if (clip == null || clip.name.StartsWith("__preview__")) continue;
                return clip;
            }

            Debug.LogWarning("[UberBagarre] Animation introuvable : " + path);
            return null;
        }
    }
}
