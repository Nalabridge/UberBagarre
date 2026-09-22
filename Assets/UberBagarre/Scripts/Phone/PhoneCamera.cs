using UberBagarre.Player;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Phone
{
    /// <summary>
    /// L'appareil photo : la preuve à envoyer au client.
    ///
    /// Le dossier en fait une obligation — « une fois la personne mise K.O., vous devez envoyer
    /// une photo à votre contact pour valider la tâche ». C'est une idée juste, et il fallait
    /// éviter de la réduire à un bouton : une photo qu'on ne peut pas rater n'est pas une
    /// preuve, c'est une formalité. Ici il faut viser. Cadrer quelqu'un au sol, se baisser, se
    /// rapprocher — et pendant ce temps, on regarde ce qu'on vient de faire.
    ///
    /// Détail technique qui a l'air anodin : le déclencheur ne lit PAS l'entrée de combat. Le
    /// téléphone en main coupe les coups, donc le clic gauche arrive vidé. Le tir passe donc
    /// directement par la liaison de touche, c'est-à-dire par la même touche que le direct,
    /// sans dépendre du verrou qui l'a désactivée.
    /// </summary>
    [RequireComponent(typeof(PhoneDevice))]
    public class PhoneCamera : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PhoneDevice _device;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _camera;

        [Header("Visee")]
        [SerializeField, Min(1f)] private float _maxDistance = 22f;

        [SerializeField, Min(0f)]
        [Tooltip("Rayon du balayage. Un corps au sol est bas et etroit : viser au pixel pres " +
                 "transformerait la photo en epreuve d'adresse.")]
        private float _sweepRadius = 0.55f;

        [SerializeField] private LayerMask _layers = ~0;

        [Header("Declenchement")]
        [SerializeField, Min(0.05f)] private float _flashDuration = 0.35f;
        [SerializeField, Min(0.1f)] private float _cooldown = 0.6f;

        private readonly RaycastHit[] _hits = new RaycastHit[16];
        private float _flash;
        private float _nextShot;

        private void Awake()
        {
            if (_device == null) _device = GetComponent<PhoneDevice>();
        }

        private void Update()
        {
            _flash = Mathf.MoveTowards(_flash, 0f, Time.unscaledDeltaTime / _flashDuration);

            if (_device == null || _input == null) return;
            if (_device.Current != PhoneDevice.Screen.Photo || !_device.IsRaised) return;
            if (Time.unscaledTime < _nextShot) return;

            // Lecture directe de la liaison : l'entree de combat est coupee tant que le
            // telephone est leve, donc StraightPressed arrive toujours a faux ici.
            if (_input.Provider == null || _input.Bindings == null) return;
            if (!_input.Provider.GetPressedThisFrame(_input.Bindings.attackStraight)) return;

            _nextShot = Time.unscaledTime + _cooldown;
            _flash = 1f;

            _device.TakePhoto(FindAimed());
        }

        private Transform FindAimed()
        {
            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return null;

            Ray ray = new Ray(camera.transform.position, camera.transform.forward);

            int count = Physics.SphereCastNonAlloc(ray, _sweepRadius, _hits, _maxDistance, _layers,
                QueryTriggerInteraction.Collide);

            Transform best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hits[i].collider;
                if (collider == null) continue;

                float distance = _hits[i].distance;
                if (distance >= bestDistance) continue;

                bestDistance = distance;
                best = collider.transform;
            }

            return best;
        }

        private void OnGUI()
        {
            if (_flash <= 0.01f) return;

            // Le flash couvre l'ecran entier et blanchit tres vite : c'est ce qui fait que la
            // photo a ete PRISE, plutot qu'un compteur qui change quelque part.
            GuiKit.Fill(new Rect(0f, 0f, UnityEngine.Screen.width, UnityEngine.Screen.height),
                new Color(1f, 1f, 1f, _flash * _flash * 0.85f));
        }
    }
}
