using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Échantillonne les entrées UNE SEULE FOIS par frame et les expose sous forme de propriétés.
    ///
    /// Pourquoi ce composant existe :
    /// - les autres systèmes (déplacement, visée, combat) ne connaissent ni les touches ni le backend ;
    /// - "AttackPressed" lu par deux systèmes différents dans la même frame renvoie la même valeur
    ///   (avec Input.GetKeyDown appelé deux fois, ça marche aussi, mais on perd la possibilité
    ///   de rejouer / simuler / désactiver les entrées, ce qui est très pratique pour tester).
    ///
    /// DefaultExecutionOrder négatif : ce composant est mis à jour avant tous les autres.
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class PlayerInputReader : MonoBehaviour
    {
        [Header("Configuration")]
        [SerializeField]
        [Tooltip("Asset de touches. Si vide, des valeurs par defaut sont creees au lancement.")]
        private InputBindings _bindings;

        [SerializeField]
        [Tooltip("Backend d'input. Auto = utilise ce qui est disponible dans le projet.")]
        private InputBackend _preferredBackend = InputBackend.Auto;

        [Header("Debug")]
        [SerializeField]
        [Tooltip("Decoche pour couper toutes les entrees de gameplay (pratique pour tester l'IA ennemie seule).")]
        private bool _gameplayInputEnabled = true;

        private IInputProvider _provider;

        /// <summary>Déplacement voulu. x = droite/gauche, y = avant/arrière. Magnitude &lt;= 1.</summary>
        public Vector2 Move { get; private set; }

        /// <summary>Déplacement souris de la frame, déjà normalisé entre backends.</summary>
        public Vector2 LookDelta { get; private set; }

        public bool JumpPressed { get; private set; }
        public bool SprintHeld { get; private set; }
        public bool DodgePressed { get; private set; }
        public bool GuardHeld { get; private set; }
        public bool AttackPressed { get; private set; }
        public bool AttackModifierHeld { get; private set; }
        public bool AttackModifierAltHeld { get; private set; }

        // Entrées "système" : volontairement non coupées par _gameplayInputEnabled,
        // sinon on ne pourrait plus libérer le curseur quand le gameplay est désactivé.
        public bool ReleaseCursorPressed { get; private set; }
        public bool ToggleDebugOverlayPressed { get; private set; }

        public InputBindings Bindings
        {
            get { return _bindings; }
        }

        /// <summary>Accès direct au backend, pour les cas particuliers (ex. reclic pour recapturer le curseur).</summary>
        public IInputProvider Provider
        {
            get { return _provider; }
        }

        public bool GameplayInputEnabled
        {
            get { return _gameplayInputEnabled; }
            set { _gameplayInputEnabled = value; }
        }

        private void Awake()
        {
            if (_bindings == null)
            {
                _bindings = ScriptableObject.CreateInstance<InputBindings>();
                _bindings.name = "InputBindings (defauts runtime)";
                Debug.LogWarning("[UberBagarre] Aucun asset InputBindings assigne sur " + name +
                                 " : utilisation des touches par defaut (non sauvegardees).", this);
            }

            _provider = InputProviderFactory.Create(_preferredBackend);
        }

        private void Update()
        {
            ReleaseCursorPressed = _provider.GetPressedThisFrame(_bindings.releaseCursor);
            ToggleDebugOverlayPressed = _provider.GetPressedThisFrame(_bindings.toggleDebugOverlay);

            if (!_gameplayInputEnabled)
            {
                ClearGameplayInput();
                return;
            }

            float x = (_provider.GetHeld(_bindings.moveRight) ? 1f : 0f) - (_provider.GetHeld(_bindings.moveLeft) ? 1f : 0f);
            float y = (_provider.GetHeld(_bindings.moveForward) ? 1f : 0f) - (_provider.GetHeld(_bindings.moveBackward) ? 1f : 0f);
            Move = Vector2.ClampMagnitude(new Vector2(x, y), 1f);

            LookDelta = _provider.GetLookDelta();

            JumpPressed = _provider.GetPressedThisFrame(_bindings.jump);
            SprintHeld = _provider.GetHeld(_bindings.sprint);
            DodgePressed = _provider.GetPressedThisFrame(_bindings.dodge);
            GuardHeld = _provider.GetHeld(_bindings.guard);
            AttackPressed = _provider.GetPressedThisFrame(_bindings.attackPrimary);
            AttackModifierHeld = _provider.GetHeld(_bindings.attackModifier);
            AttackModifierAltHeld = _provider.GetHeld(_bindings.attackModifierAlt);
        }

        private void ClearGameplayInput()
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            JumpPressed = false;
            SprintHeld = false;
            DodgePressed = false;
            GuardHeld = false;
            AttackPressed = false;
            AttackModifierHeld = false;
            AttackModifierAltHeld = false;
        }
    }
}
