using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Le système d'interaction : trouve ce que le joueur regarde et propose de l'activer.
    ///
    /// Il choisit par la VISÉE, pas par la proximité. La différence compte dans une maison
    /// minuscule où la table, le lit et la porte sont à moins de deux mètres les uns des
    /// autres : « le plus proche » y change à chaque pas et l'invite se met à clignoter d'un
    /// objet à l'autre. « Ce que je regarde » ne change que quand le joueur décide de tourner
    /// la tête.
    ///
    /// Deux détecteurs plutôt qu'un : un rayon fin pour ce qu'on vise vraiment, puis un
    /// balayage sphérique un peu large en secours. Sans le second, il faut viser une poignée
    /// de porte au pixel près, ce qui transforme une action banale en épreuve d'adresse.
    /// </summary>
    public class InteractionSystem : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _camera;

        [SerializeField]
        [Tooltip("Optionnel : telephone sorti, E lui appartient et le decor ne reagit plus.")]
        private PhoneDevice _phone;

        [Header("Detection")]
        [SerializeField, Min(0.5f)] private float _maxDistance = 3.2f;

        [SerializeField, Min(0f)]
        [Tooltip("Rayon du balayage de secours. 0 = viser au pixel pres.")]
        private float _sweepRadius = 0.28f;

        [SerializeField] private LayerMask _layers = ~0;

        [Header("Affichage")]
        [SerializeField] private Color _accent = new Color(0.98f, 0.86f, 0.42f);
        [SerializeField] private Color _panelColor = new Color(0f, 0f, 0f, 0.62f);
        [SerializeField] private int _fontSize = 15;

        [SerializeField]
        [Tooltip("Systeme actif. Le scenario le coupe pendant les transitions : une invite " +
                 "« Monter en voiture » qui reste affichee pendant un fondu au noir, et pire, " +
                 "qui reste activable, suffit a casser une sequence.")]
        private bool _active = true;

        private readonly RaycastHit[] _hits = new RaycastHit[12];
        private Interactable _focused;
        private float _visibility;

        public Interactable Focused { get { return _focused; } }

        public bool Active
        {
            get { return _active; }
            set
            {
                _active = value;
                if (!_active) _focused = null;
            }
        }

        private void Update()
        {
            // Telephone sorti : E appartient au telephone (ouvrir une appli, repondre). Une porte
            // ne doit pas s'ouvrir parce qu'on a lance la galerie devant elle.
            bool phoneUp = _phone != null && _phone.IsRaised;
            _focused = _active && !phoneUp && !FightIntro.AnyPlaying && !ModalScreen.Active && !CarThief.Picking ? FindFocused() : null;

            _visibility = Mathf.MoveTowards(_visibility, _focused != null ? 1f : 0f,
                Time.unscaledDeltaTime * 6f);

            if (_focused == null || _input == null || !_input.InteractPressed) return;

            _focused.Activate();
        }

        private Interactable FindFocused()
        {
            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return null;

            Ray ray = new Ray(camera.transform.position, camera.transform.forward);

            Interactable best = null;
            float bestDistance = float.MaxValue;

            int count = Physics.RaycastNonAlloc(ray, _hits, _maxDistance, _layers,
                QueryTriggerInteraction.Collide);

            best = Closest(count, camera.transform.position, ref bestDistance);
            if (best != null) return best;

            if (_sweepRadius <= 0f) return null;

            count = Physics.SphereCastNonAlloc(ray, _sweepRadius, _hits, _maxDistance, _layers,
                QueryTriggerInteraction.Collide);

            bestDistance = float.MaxValue;
            return Closest(count, camera.transform.position, ref bestDistance);
        }

        private Interactable Closest(int count, Vector3 origin, ref float bestDistance)
        {
            Interactable best = null;

            for (int i = 0; i < count; i++)
            {
                Collider collider = _hits[i].collider;
                if (collider == null) continue;

                // Le composant peut vivre sur un parent : les colliders sont souvent posés sur
                // les morceaux visuels, pas sur l'objet qui porte le sens.
                Interactable candidate = collider.GetComponentInParent<Interactable>();
                if (candidate == null || !candidate.Available) continue;

                float distance = Vector3.Distance(origin, candidate.FocusPoint);
                if (distance > candidate.Range || distance >= bestDistance) continue;

                bestDistance = distance;
                best = candidate;
            }

            return best;
        }

        private void OnGUI()
        {
            if (_visibility <= 0.01f || _focused == null) return;

            string label = _focused.Label;
            string hint = _focused.Hint;
            float u = UI.UiTheme.Unit;
            float previous = GuiKit.Alpha;
            GuiKit.Alpha = _visibility;

            string keyText = _input != null && _input.Bindings != null ? UI.UiTheme.KeyName(_input.Bindings.interact) : "E";
            GUIStyle keyStyle = UI.UiTheme.Text(13f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            GUIStyle labelStyle = UI.UiTheme.Text(16f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            GUIStyle hintStyle = UI.UiTheme.Text(12.5f, GuiKit.Weight.Regular, TextAnchor.MiddleLeft);

            float keyW = Mathf.Max(26f * u, keyStyle.CalcSize(new GUIContent(keyText)).x + 14f * u);
            float textW = Mathf.Max(labelStyle.CalcSize(new GUIContent(label)).x,
                string.IsNullOrEmpty(hint) ? 0f : Mathf.Min(420f * u, hintStyle.CalcSize(new GUIContent(hint)).x));
            float width = keyW + textW + 34f * u;
            float height = string.IsNullOrEmpty(hint) ? 38f * u : 56f * u;

            // Sous le réticule, jamais dessus : l'invite ne doit pas masquer ce qu'on vise.
            float x = Screen.width * 0.5f - width * 0.5f;
            float y = Screen.height * 0.5f + 44f * u + (1f - _visibility) * 8f * u;
            Rect panel = new Rect(x, y, width, height);
            GuiKit.Glow(panel, new Color(0f, 0f, 0f, 0.3f), 8f * u, 10f * u);
            GuiKit.Rounded(panel, new Color(0.04f, 0.045f, 0.055f, 0.82f), 8f * u);

            Rect key = new Rect(x + 10f * u, y + 9f * u, keyW, 22f * u);
            GuiKit.Rounded(key, new Color(1f, 1f, 1f, 0.92f), 4f * u);
            UI.UiTheme.Label(key, keyText, keyStyle, new Color(0.06f, 0.07f, 0.09f));
            UI.UiTheme.Label(new Rect(key.xMax + 12f * u, y + 8f * u, textW + 8f, 24f * u), label, labelStyle, UI.UiTheme.Ink);

            if (!string.IsNullOrEmpty(hint))
            {
                UI.UiTheme.Label(new Rect(key.xMax + 12f * u, y + 31f * u, textW + 8f, 18f * u), hint, hintStyle, UI.UiTheme.InkDim);
            }

            GuiKit.Alpha = previous;
        }
    }
}
