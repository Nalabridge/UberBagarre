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
        [Tooltip("Coup de tete : tres court, tres lourd. Il faut etre colle a l'adversaire.")]
        public InputBinding attackHeadbutt = InputBinding.FromKey(KeyCode.G);

        [Tooltip("Bousculade a deux mains : presque pas de degats, mais un gros recul. Pour " +
                 "envoyer quelqu'un dans les poubelles, ou se degager quand on est coince.")]
        public InputBinding attackShove = InputBinding.FromKey(KeyCode.X);

        public InputBinding guard = InputBinding.FromKey(KeyCode.LeftControl);

        [Header("Esquive")]
        [Tooltip("Esquive. Direction donnee par les touches de deplacement, arriere par defaut.")]
        public InputBinding dodge = InputBinding.FromKey(KeyCode.LeftAlt);

        [Header("Systeme (toujours actif, meme curseur libere)")]
        [Tooltip("Libere le curseur souris pour revenir a l'editeur Unity.")]
        public InputBinding releaseCursor = InputBinding.FromKey(KeyCode.Escape);

        [Tooltip("Affiche / masque l'overlay de debug de combat.")]
        public InputBinding toggleDebugOverlay = InputBinding.FromKey(KeyCode.F1);

        [Tooltip("Ouvre / ferme le menu de bac a sable : PV, degats, apparition d'adversaires.")]
        public InputBinding toggleSandboxMenu = InputBinding.FromKey(KeyCode.Tab);

        [Tooltip("Camera d'observation : tourne autour du joueur. Le combat continue pendant ce temps.")]
        public InputBinding toggleObserver = InputBinding.FromKey(KeyCode.F3);

        [Tooltip("Rapproche la camera d'observation.")]
        public InputBinding observerZoomIn = InputBinding.FromKey(KeyCode.Equals);

        [Tooltip("Eloigne la camera d'observation.")]
        public InputBinding observerZoomOut = InputBinding.FromKey(KeyCode.Minus);

        [Tooltip("Relance le combat : tout le monde revient a plein et retourne a son spawn.")]
        public InputBinding restartFight = InputBinding.FromKey(KeyCode.R);

        [Header("Histoire")]
        [Tooltip("Interagir : repondre au telephone, ouvrir une porte, monter en voiture, " +
                 "avancer un dialogue. Une seule touche pour tout : le joueur n'a jamais a se " +
                 "demander laquelle il faut.")]
        public InputBinding interact = InputBinding.FromKey(KeyCode.E);

        [Tooltip("Sortir ou ranger le telephone. T comme telephone — et surtout pas une lettre " +
                 "deja prise par le deplacement en AZERTY.")]
        public InputBinding phone = InputBinding.FromKey(KeyCode.T);

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
