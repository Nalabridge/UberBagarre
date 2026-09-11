using UberBagarre.Core;
using UnityEngine;

namespace UberBagarre.Player
{
    /// <summary>
    /// Capture / libération du curseur souris.
    /// Indispensable en FPS dans l'éditeur : sans Échap, on ne peut plus sortir du Game view.
    ///
    /// Quand le curseur est libéré, la visée et les entrées de gameplay sont coupées,
    /// pour ne pas frapper dans le vide en cliquant dans l'éditeur.
    /// </summary>
    public class CursorLockController : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerLook _look;

        [Header("Comportement")]
        [SerializeField] private bool _lockOnStart = true;

        [SerializeField]
        [Tooltip("Recapture le curseur quand on clique dans la fenetre de jeu.")]
        private bool _relockOnClick = true;

        public bool IsLocked
        {
            get { return Cursor.lockState == CursorLockMode.Locked; }
        }

        private void Awake()
        {
            if (_input == null) _input = GetComponentInParent<PlayerInputReader>();
            if (_look == null) _look = GetComponentInParent<PlayerLook>();
        }

        private void Start()
        {
            SetLocked(_lockOnStart);
        }

        private void Update()
        {
            if (_input == null) return;

            if (IsLocked)
            {
                if (_input.ReleaseCursorPressed) SetLocked(false);
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
            Cursor.lockState = locked ? CursorLockMode.Locked : CursorLockMode.None;
            Cursor.visible = !locked;

            if (_look != null) _look.LookEnabled = locked;
            if (_input != null) _input.GameplayInputEnabled = locked;
        }

        private void OnDisable()
        {
            // Sécurité : ne jamais laisser le curseur verrouillé si l'objet est détruit / la scène changée.
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
    }
}
