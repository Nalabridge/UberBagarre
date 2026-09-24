using UberBagarre.Core;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Capture / libération du curseur souris.
    /// Indispensable en FPS dans l'éditeur : sans Échap, on ne peut plus sortir du Game view.
    ///
    /// Deux notions sont séparées exprès :
    ///
    /// - **engagé** : le joueur a cliqué dans le jeu et n'a pas appuyé sur Échap. C'est CE choix
    ///   qui ouvre les entrées de jeu ;
    /// - **verrouillé** : le système d'exploitation a bien capturé la souris.
    ///
    /// Les confondre marchait sous Windows, où les deux vont toujours ensemble. Sous Linux, non :
    /// le gestionnaire de fenêtres peut refuser la capture ou la rendre sans prévenir (changement
    /// de focus, Wayland). Lié au verrou, le jeu restait alors bloqué — ni déplacement ni visée —
    /// sans que rien à l'écran ne dise pourquoi. Désormais le jeu suit l'intention du joueur, et
    /// la capture est redemandée tant qu'elle n'est pas obtenue.
    /// </summary>
    public class CursorLockController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;

        [Header("Comportement")]
        [SerializeField] private bool _lockOnStart = true;

        [SerializeField]
        [Tooltip("Recapture le curseur quand on clique dans la fenetre de jeu.")]
        private bool _relockOnClick = true;

        [SerializeField, Min(0.1f)]
        [Tooltip("Delai entre deux tentatives de recapture quand le systeme a rendu la souris.")]
        private float _relockInterval = 0.4f;

        [SerializeField]
        [Tooltip("Affiche « clique pour jouer » quand le jeu n'a pas la main.")]
        private bool _showHint = true;

        private bool _engaged;
        private float _nextRelock;
        private GUIStyle _hintStyle;
        private GUIStyle _subStyle;

        /// <summary>Le joueur a la main (il a cliqué dans le jeu et n'a pas appuyé sur Échap).</summary>
        public bool IsEngaged
        {
            get { return _engaged; }
        }

        /// <summary>Le système a réellement capturé la souris.</summary>
        public bool IsLocked
        {
            get { return Cursor.lockState == CursorLockMode.Locked; }
        }

        private void Awake()
        {
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
        }

        private void Start()
        {
            SetLocked(_lockOnStart);
        }

        private void Update()
        {
            if (_input == null) return;

            if (_engaged)
            {
                if (_input.ReleaseCursorPressed)
                {
                    SetLocked(false);
                    return;
                }

                // Le systeme a rendu la souris sans qu'on l'ait demande : on la redemande, mais
                // seulement quand la fenetre de jeu a le focus — sinon on volerait le curseur a
                // l'Inspector a chaque clic dans l'editeur.
                if (!IsLocked && Application.isFocused && Time.unscaledTime >= _nextRelock)
                {
                    _nextRelock = Time.unscaledTime + _relockInterval;
                    ApplyCursor(true);
                }

                return;
            }

            if (_relockOnClick && _input.Provider != null &&
                _input.Provider.GetPressedThisFrame(InputBinding.FromMouse(0)))
            {
                SetLocked(true);
            }
        }

        public void SetLocked(bool locked)
        {
            _engaged = locked;
            _nextRelock = Time.unscaledTime + _relockInterval;

            ApplyCursor(locked);

            if (_input != null) _input.SetGameplayLock(this, !locked);
        }

        private static void ApplyCursor(bool locked)
        {
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;
        }

        private void OnDisable()
        {
            // Sécurité : ne jamais laisser le curseur verrouillé si l'objet est désactivé / la scène
            // changée. Le verrou de jeu, lui, reste tel quel : le menu de réglage désactive ce
            // composant pendant qu'il est ouvert, et le jeu doit rester coupé derrière le menu.
            ApplyCursor(false);
        }

        private void OnDestroy()
        {
            if (_input != null) _input.SetGameplayLock(this, false);
        }

        // ------------------------------------------------------------------ indication

        /// <summary>
        /// Un jeu qui n'a pas la main doit le DIRE. Sans ce message, « je ne peux pas bouger »
        /// et « le jeu est cassé » se ressemblent exactement.
        /// </summary>
        private void OnGUI()
        {
            if (!_showHint) return;
            if (_engaged && Application.isFocused) return;

            if (_hintStyle == null)
            {
                _hintStyle = GuiKit.Style(26, FontStyle.Bold, TextAnchor.MiddleCenter);
                _subStyle = GuiKit.Style(15, FontStyle.Normal, TextAnchor.MiddleCenter);
            }

            float width = Mathf.Min(620f, Screen.width - 40f);
            Rect band = new Rect((Screen.width - width) * 0.5f, Screen.height * 0.5f - 48f, width, 96f);

            GuiKit.Fill(band, new Color(0f, 0f, 0f, 0.72f));

            GuiKit.OutlinedLabel(new Rect(band.x, band.y + 12f, band.width, 40f),
                "CLIQUE DANS LA FENÊTRE POUR JOUER", _hintStyle, Color.white, new Color(0f, 0f, 0f, 0.8f), 1.5f);

            GuiKit.OutlinedLabel(new Rect(band.x, band.y + 54f, band.width, 28f),
                "Échap libère la souris  ·  ZQSD / WASD pour bouger", _subStyle,
                new Color(0.8f, 0.82f, 0.88f), new Color(0f, 0f, 0f, 0.8f), 1f);
        }
    }
}
