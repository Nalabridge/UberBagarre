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

        [Tooltip("Laisse sur None pour desactiver le sprint (par defaut : desactive, car Shift sert a l'esquive).")]
        public InputBinding sprint = InputBinding.FromKey(KeyCode.None);

        [Header("Combat")]
        [Tooltip("Attaque principale. Le coup reellement joue est decide par l'AttackInputMap (phase 3).")]
        public InputBinding attackPrimary = InputBinding.FromMouse(0);

        [Tooltip("Garde / blocage (maintenu).")]
        public InputBinding guard = InputBinding.FromMouse(1);

        [Tooltip("Modificateur maintenu qui change le coup joue (ex. Ctrl + clic = crochet).")]
        public InputBinding attackModifier = InputBinding.FromKey(KeyCode.LeftControl);

        [Tooltip("Second modificateur, pour une troisieme famille de coups (ex. uppercut).")]
        public InputBinding attackModifierAlt = InputBinding.FromKey(KeyCode.LeftAlt);

        [Header("Esquive")]
        public InputBinding dodge = InputBinding.FromKey(KeyCode.LeftShift);

        [Header("Systeme (toujours actif, meme curseur libere)")]
        [Tooltip("Libere le curseur souris pour revenir a l'editeur Unity.")]
        public InputBinding releaseCursor = InputBinding.FromKey(KeyCode.Escape);

        [Tooltip("Affiche / masque l'overlay de debug de combat.")]
        public InputBinding toggleDebugOverlay = InputBinding.FromKey(KeyCode.F1);
    }
}
