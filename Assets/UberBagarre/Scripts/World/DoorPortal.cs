using System.Collections;
using UberBagarre.Core;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Une porte qui mène ailleurs : l'entrée du Vertigo mène à la salle, la sortie de la salle
    /// ramène à la rue. Comme dans tout monde ouvert, l'intérieur vit à part — il est allumé
    /// quand on y est, éteint sinon — et le passage se fait dans un fondu au noir.
    ///
    /// Le déplacement passe par ISpawnReceiver, le seul chemin qui coupe le CharacterController
    /// le temps du saut et réaligne la visée (voir LocationDirector).
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class DoorPortal : MonoBehaviour
    {
        [SerializeField] private GameObject _player;
        [SerializeField] private Transform _destination;
        [SerializeField] private ScreenFader _fader;

        [SerializeField]
        [Tooltip("Allume en passant la porte (l'interieur, quand on entre).")]
        private GameObject[] _activate = new GameObject[0];

        [SerializeField]
        [Tooltip("Eteint en passant la porte (l'interieur, quand on sort).")]
        private GameObject[] _deactivate = new GameObject[0];

        [SerializeField, Min(0.05f)] private float _fade = 0.35f;

        private Interactable _interactable;
        private bool _busy;

        /// <summary>Vrai pendant le passage (fondu compris).</summary>
        public static bool AnyPassing { get; private set; }

        private void Awake()
        {
            _interactable = GetComponent<Interactable>();
        }

        private void OnEnable()
        {
            if (_interactable != null) _interactable.Activated += OnActivated;
        }

        private void OnDisable()
        {
            if (_interactable != null) _interactable.Activated -= OnActivated;
        }

        private void OnActivated(Interactable source)
        {
            if (_busy || _destination == null || _player == null) return;

            // Le passage tourne sur le fondu, qui ne s'éteint jamais : la porte de sortie vit
            // dans la salle, et la salle s'éteint pendant le passage — une coroutine lancée
            // d'ici s'arrêterait avec elle, écran noir pour toujours.
            MonoBehaviour host = _fader != null ? (MonoBehaviour)_fader : this;
            host.StartCoroutine(Pass());
        }

        private IEnumerator Pass()
        {
            _busy = true;
            AnyPassing = true;

            if (_fader != null)
            {
                _fader.FadeOut(_fade);
                float waited = 0f;
                while (!_fader.IsBlack && waited < _fade + 1f)
                {
                    waited += Time.unscaledDeltaTime;
                    yield return null;
                }
            }

            for (int i = 0; i < _activate.Length; i++)
            {
                if (_activate[i] != null) _activate[i].SetActive(true);
            }

            ISpawnReceiver[] receivers = _player.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++)
            {
                receivers[i].OnSpawned(_destination.position, _destination.rotation);
            }

            for (int i = 0; i < _deactivate.Length; i++)
            {
                if (_deactivate[i] != null) _deactivate[i].SetActive(false);
            }

            // Une image au noir : les lampes et la foule du lieu d'arrivée s'installent.
            yield return null;
            yield return null;

            if (_fader != null) _fader.FadeIn(_fade * 1.4f);
            if (_interactable != null) _interactable.ResetUsage();

            _busy = false;
            AnyPassing = false;
        }
    }
}
