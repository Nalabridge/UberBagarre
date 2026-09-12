using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// Toutes les touches du jeu, au même endroit, éditables dans l'Inspector.
    /// Créé automatiquement par l'outil de génération de scène dans Assets/UberBagarre/Settings.
    /// (Clic droit > Create > Uber Bagarre > Input Bindings pour en créer un autre, ex. un preset gaucher.)
    /// </summary>
    [CreateAssetMenu(fileName = "InputBindings", menuName = "Uber Bagarre/Input Bindings", order = 0)]
    public class InputBindings : ScriptableObject
    {
        [Header("Deplacement")]
        public InputBinding moveForward = InputBinding.FromKey(KeyCode.W);
        public InputBinding moveBackward = InputBinding.FromKey(KeyCode.S);
        public InputBinding moveLeft = InputBinding.FromKey(KeyCode.A);
        public InputBinding moveRight = InputBinding.FromKey(KeyCode.D);
        public InputBinding jump = InputBinding.FromKey(KeyCode.Space);

        [Tooltip("Maintenu pour sprinter. Laisse sur None pour desactiver le sprint.")]
        public InputBinding sprint = InputBinding.FromKey(KeyCode.LeftShift);

        [Tooltip("Maintenu pour s'accroupir. Appuye pendant une course = glissade.")]
        public InputBinding crouch = InputBinding.FromKey(KeyCode.C);

        [Header("Combat")]
        [Tooltip("Direct : rapide, alterne gauche et droite.")]
        public InputBinding attackStraight = InputBinding.FromMouse(0);

        [Tooltip("Crochet : plus lent, plus fort, le buste tourne.")]
        public InputBinding attackHook = InputBinding.FromMouse(1);

        [Tooltip("Uppercut : le plus lent et le plus lourd.")]
        public InputBinding attackUppercut = InputBinding.FromMouse(2);

        [Tooltip("Coup de pied de face : lourd, lent, il repousse.")]
        public InputBinding attackKick = InputBinding.FromKey(KeyCode.F);

        [Tooltip("Coup de pied bas : peu de degats, mais c'est lui qui fait tomber.")]
        public InputBinding attackLowKick = InputBinding.FromKey(KeyCode.V);

        [Tooltip("Garde / blocage (maintenu). Les premieres fractions de seconde sont une PARADE.")]
        public InputBinding guard = InputBinding.FromKey(KeyCode.LeftControl);

        [Header("Esquive")]
        [Tooltip("Esquive. Direction donnee par les touches de deplacement, arriere par defaut.")]
        public InputBinding dodge = InputBinding.FromKey(KeyCode.LeftAlt);

        [Header("Systeme (toujours actif, meme curseur libere)")]
        [Tooltip("Libere le curseur souris pour revenir a l'editeur Unity.")]
        public InputBinding releaseCursor = InputBinding.FromKey(KeyCode.Escape);

        [Tooltip("Affiche / masque l'overlay de debug de combat.")]
        public InputBinding toggleDebugOverlay = InputBinding.FromKey(KeyCode.F1);

        [Tooltip("Relance le combat : tout le monde revient a plein et retourne a son spawn.")]
        public InputBinding restartFight = InputBinding.FromKey(KeyCode.R);

#if UNITY_EDITOR
        /// <summary>
        /// Restaure toutes les touches par défaut.
        /// Accessible via le menu contextuel de l'Inspector (icône ⋮ en haut à droite du composant).
        ///
        /// Utile parce qu'un asset garde les valeurs enregistrées le jour de sa création :
        /// changer une valeur par défaut dans le code ne met PAS à jour un asset déjà existant.
        /// </summary>
        [ContextMenu("Reinitialiser aux touches par defaut")]
        private void ResetToDefaults()
        {
            InputBindings defaults = CreateInstance<InputBindings>();
            string previousName = name;

            UnityEditor.EditorUtility.CopySerialized(defaults, this);
            name = previousName;

            DestroyImmediate(defaults);
            UnityEditor.EditorUtility.SetDirty(this);
            UnityEditor.AssetDatabase.SaveAssets();

            Debug.Log("[UberBagarre] Touches reinitialisees aux valeurs par defaut.", this);
        }
#endif
    }
}
