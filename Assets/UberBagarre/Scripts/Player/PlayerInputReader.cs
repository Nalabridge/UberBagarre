using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Échantillonne les entrées UNE SEULE FOIS par frame et les expose sous forme de propriétés.
    ///
    /// Pourquoi ce composant existe :
    /// - les autres systèmes (déplacement, visée, combat) ne connaissent ni les touches ni le backend ;
    /// - une même entrée lue par deux systèmes dans la même frame renvoie la même valeur
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
        public bool CrouchHeld { get; private set; }
        public bool CrouchPressed { get; private set; }
        public bool GuardHeld { get; private set; }
        public bool StraightPressed { get; private set; }
        public bool HookPressed { get; private set; }
        public bool UppercutPressed { get; private set; }
        public bool KickPressed { get; private set; }
        public bool LowKickPressed { get; private set; }
        public bool HeadbuttPressed { get; private set; }
        public bool ShovePressed { get; private set; }

        // Versions MAINTENUES des attaques. Elles existent pour que la cadence de frappe ne
        // depende pas de la vitesse a laquelle le joueur arrive a cliquer : maintenir enchaine.
        public bool StraightHeld { get; private set; }
        public bool HookHeld { get; private set; }
        public bool UppercutHeld { get; private set; }
        public bool KickHeld { get; private set; }
        public bool LowKickHeld { get; private set; }

        // Entrées "système" : volontairement non coupées par _gameplayInputEnabled,
        // sinon on ne pourrait plus libérer le curseur quand le gameplay est désactivé.
        public bool ReleaseCursorPressed { get; private set; }
        public bool ToggleDebugOverlayPressed { get; private set; }
        public bool ToggleSandboxMenuPressed { get; private set; }
        public bool ToggleObserverPressed { get; private set; }
        public bool ObserverZoomInHeld { get; private set; }
        public bool ObserverZoomOutHeld { get; private set; }
        public bool RestartFightPressed { get; private set; }

        /// <summary>Monde ouvert : la grande carte.</summary>
        public bool MapPressed { get; private set; }

        /// <summary>
        /// Interaction et telephone : lus HORS du bloc de jeu, comme les touches d'interface.
        ///
        /// C'est indispensable pour le prologue : pendant un dialogue ou une transition, les
        /// commandes de combat et de deplacement sont coupees, mais il faut encore pouvoir
        /// repondre au telephone ou passer une replique. Une touche d'histoire coupee en meme
        /// temps que le combat bloquerait le joueur sans rien lui dire.
        /// </summary>
        public bool InteractPressed { get; private set; }

        public bool InteractHeld { get; private set; }
        public bool PhonePressed { get; private set; }

        /// <summary>Crans de molette de cette frame (toujours lus : le téléphone s'en sert même figé).</summary>
        public float ScrollDelta { get; private set; }

        public InputBindings Bindings
        {
            get { return _bindings; }
        }

        /// <summary>Accès direct au backend, pour les cas particuliers (ex. reclic pour recapturer le curseur).</summary>
        public IInputProvider Provider
        {
            get { return _provider; }
        }

        /// <summary>
        /// Vrai si les entrées de jeu passent : l'interrupteur de debug est levé ET personne ne
        /// les bloque. Écrire dans cette propriété ne touche que l'interrupteur de debug.
        /// </summary>
        public bool GameplayInputEnabled
        {
            get { return _gameplayInputEnabled && _gameplayLocks.Count == 0; }
            set { _gameplayInputEnabled = value; }
        }

        /// <summary>
        /// Pose ou retire un verrou de jeu au nom d'un propriétaire.
        ///
        /// Deux systèmes coupent le jeu pour des raisons sans rapport : le curseur (libéré avec
        /// Échap, ou pas encore capturé) et l'histoire (une étape figée). Avec un booléen partagé,
        /// le dernier à écrire gagnait : recapturer la souris dégelait une cinématique, et une
        /// étape qui rendait la main relançait le jeu curseur libéré. Même remède que pour les
        /// coups : le jeu reprend quand PLUS PERSONNE ne le bloque.
        /// </summary>
        public void SetGameplayLock(object owner, bool locked)
        {
            if (owner == null) return;

            if (locked)
            {
                if (!_gameplayLocks.Contains(owner)) _gameplayLocks.Add(owner);
            }
            else
            {
                _gameplayLocks.Remove(owner);
            }
        }

        private readonly System.Collections.Generic.List<object> _gameplayLocks =
            new System.Collections.Generic.List<object>(2);

        /// <summary>
        /// Coupe les COUPS sans couper le deplacement ni la visee.
        ///
        /// Un seul systeme s'en sert : le telephone. Marcher en regardant son ecran est la
        /// posture meme du personnage, mais frapper avec un telephone dans la main ne l'est
        /// pas. Separer les deux verrous evite d'avoir a choisir entre "fige sur place" et
        /// "peut mettre un direct a travers son propre ecran".
        /// </summary>
        public bool CombatInputEnabled
        {
            get { return _combatLocks.Count == 0; }
        }

        /// <summary>
        /// Pose ou retire un verrou de combat au nom d'un propriétaire.
        ///
        /// Plusieurs systèmes coupent les coups : le téléphone levé, un objet tenu en main. Avec
        /// un simple booléen, chacun l'écrirait à chaque image et le dernier à parler gagnerait —
        /// ranger le téléphone rendrait les coups alors qu'on tient encore une bouteille. Un
        /// verrou par propriétaire règle la question : les coups reviennent quand PLUS PERSONNE
        /// ne les bloque.
        /// </summary>
        public void SetCombatLock(object owner, bool locked)
        {
            if (owner == null) return;

            if (locked)
            {
                if (!_combatLocks.Contains(owner)) _combatLocks.Add(owner);
            }
            else
            {
                _combatLocks.Remove(owner);
            }
        }

        private readonly System.Collections.Generic.List<object> _combatLocks =
            new System.Collections.Generic.List<object>(2);

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
            ToggleSandboxMenuPressed = _provider.GetPressedThisFrame(_bindings.toggleSandboxMenu);
            ToggleObserverPressed = _provider.GetPressedThisFrame(_bindings.toggleObserver);
            ObserverZoomInHeld = _provider.GetHeld(_bindings.observerZoomIn);
            ObserverZoomOutHeld = _provider.GetHeld(_bindings.observerZoomOut);
            RestartFightPressed = _provider.GetPressedThisFrame(_bindings.restartFight);
            MapPressed = _provider.GetPressedThisFrame(_bindings.openMap);
            InteractPressed = _provider.GetPressedThisFrame(_bindings.interact);
            InteractHeld = _provider.GetHeld(_bindings.interact);
            PhonePressed = _provider.GetPressedThisFrame(_bindings.phone);
            ScrollDelta = _provider.GetScrollDelta();

            if (!GameplayInputEnabled)
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
            CrouchHeld = _provider.GetHeld(_bindings.crouch);
            CrouchPressed = _provider.GetPressedThisFrame(_bindings.crouch);
            GuardHeld = _provider.GetHeld(_bindings.guard);
            StraightPressed = _provider.GetPressedThisFrame(_bindings.attackStraight);
            HookPressed = _provider.GetPressedThisFrame(_bindings.attackHook);
            UppercutPressed = _provider.GetPressedThisFrame(_bindings.attackUppercut);
            KickPressed = _provider.GetPressedThisFrame(_bindings.attackKick);
            LowKickPressed = _provider.GetPressedThisFrame(_bindings.attackLowKick);
            HeadbuttPressed = _provider.GetPressedThisFrame(_bindings.attackHeadbutt);
            ShovePressed = _provider.GetPressedThisFrame(_bindings.attackShove);

            StraightHeld = _provider.GetHeld(_bindings.attackStraight);
            HookHeld = _provider.GetHeld(_bindings.attackHook);
            UppercutHeld = _provider.GetHeld(_bindings.attackUppercut);
            KickHeld = _provider.GetHeld(_bindings.attackKick);
            LowKickHeld = _provider.GetHeld(_bindings.attackLowKick);

            if (_combatLocks.Count > 0) ClearCombatInput();
        }

        /// <summary>Remet a zero tout ce qui declenche un coup, une garde ou une esquive.</summary>
        private void ClearCombatInput()
        {
            DodgePressed = false;
            GuardHeld = false;

            StraightPressed = false;
            HookPressed = false;
            UppercutPressed = false;
            KickPressed = false;
            LowKickPressed = false;
            HeadbuttPressed = false;
            ShovePressed = false;

            StraightHeld = false;
            HookHeld = false;
            UppercutHeld = false;
            KickHeld = false;
            LowKickHeld = false;
        }

        private void ClearGameplayInput()
        {
            Move = Vector2.zero;
            LookDelta = Vector2.zero;
            JumpPressed = false;
            SprintHeld = false;
            DodgePressed = false;
            CrouchHeld = false;
            CrouchPressed = false;
            GuardHeld = false;
            StraightPressed = false;
            HookPressed = false;
            UppercutPressed = false;
            KickPressed = false;
            LowKickPressed = false;
            HeadbuttPressed = false;
            ShovePressed = false;
            StraightHeld = false;
            HookHeld = false;
            UppercutHeld = false;
            KickHeld = false;
            LowKickHeld = false;
        }
    }
}
