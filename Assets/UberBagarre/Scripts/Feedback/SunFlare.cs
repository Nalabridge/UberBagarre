using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Halo et reflets du soleil, dessinés à l'écran.
    ///
    /// Unity fournit bien un composant de flare, mais il dépend du render pipeline : celui du
    /// Built-in demande un asset Flare, celui d'URP/HDRP est un composant différent. Dessiner
    /// les halos nous-mêmes est le seul moyen d'obtenir le même rendu partout, sans asset.
    ///
    /// Les reflets se placent sur la droite qui relie le soleil au centre de l'écran, et
    /// repassent de l'autre côté : c'est le comportement d'un vrai empilement de lentilles,
    /// et c'est ce qui fait lire l'effet comme « soleil » plutôt que comme « tache blanche ».
    /// </summary>
    public class SunFlare : MonoBehaviour
    {
        [System.Serializable]
        private struct Ghost
        {
            [Tooltip("Position sur la droite soleil -> centre. 0 = sur le soleil, 1 = au centre, negatif = de l'autre cote.")]
            public float position;

            public float size;
            public Color color;
        }

        [Header("References")]
        [SerializeField] private Light _sun;
        [SerializeField] private Camera _camera;

        [Header("Halo principal")]
        [SerializeField] private bool _enabled = true;
        [SerializeField] private Color _haloColor = new Color(1f, 0.86f, 0.62f, 0.55f);
        [SerializeField, Min(10f)] private float _haloSize = 340f;
        [SerializeField, Min(10f)] private float _coreSize = 90f;

        [Header("Reflets")]
        [SerializeField]
        private Ghost[] _ghosts =
        {
            new Ghost { position = 0.32f, size = 55f, color = new Color(1f, 0.72f, 0.35f, 0.16f) },
            new Ghost { position = 0.62f, size = 32f, color = new Color(0.7f, 0.9f, 1f, 0.13f) },
            new Ghost { position = 1.25f, size = 78f, color = new Color(1f, 0.55f, 0.3f, 0.10f) },
            new Ghost { position = 1.7f, size = 44f, color = new Color(0.85f, 1f, 0.8f, 0.09f) }
        };

        [Header("Occultation")]
        [SerializeField]
        [Tooltip("Le halo disparait si un mur passe devant le soleil.")]
        private bool _checkOcclusion = true;

        [SerializeField, Min(1f)] private float _occlusionDistance = 60f;
        [SerializeField, Min(0.5f)] private float _fadeSpeed = 6f;

        private float _visibility;

        private void Awake()
        {
            if (_camera == null) _camera = Camera.main;
        }

        private void Update()
        {
            _camera = GuiKit.ActiveCamera(_camera);

            float target = ComputeTargetVisibility();
            _visibility = Mathf.MoveTowards(_visibility, target, _fadeSpeed * Time.unscaledDeltaTime);
        }

        private float ComputeTargetVisibility()
        {
            if (!_enabled || _sun == null || _camera == null) return 0f;

            Vector3 toSun = -_sun.transform.forward;

            // Derriere la camera : rien a dessiner.
            float facing = Vector3.Dot(_camera.transform.forward, toSun);
            if (facing <= 0.05f) return 0f;

            if (_checkOcclusion &&
                Physics.Raycast(_camera.transform.position, toSun, _occlusionDistance, ~0, QueryTriggerInteraction.Ignore))
            {
                return 0f;
            }

            // Le halo s'intensifie quand on regarde vers le soleil, comme un vrai eblouissement.
            return Mathf.Clamp01(facing);
        }

        private void OnGUI()
        {
            if (_visibility <= 0.005f || _camera == null || _sun == null) return;

            Vector3 sunWorld = _camera.transform.position - _sun.transform.forward * 500f;

            Vector2 sunScreen;
            if (!GuiKit.WorldToGui(_camera, sunWorld, out sunScreen)) return;

            Vector2 centre = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            Vector2 toCentre = centre - sunScreen;

            float strength = _visibility;

            Disc(sunScreen, _haloSize, _haloColor, strength);
            Disc(sunScreen, _coreSize, new Color(1f, 0.97f, 0.88f, 0.8f), strength);

            for (int i = 0; i < _ghosts.Length; i++)
            {
                Ghost ghost = _ghosts[i];
                Vector2 position = sunScreen + toCentre * ghost.position;
                Disc(position, ghost.size, ghost.color, strength);
            }
        }

        private static void Disc(Vector2 centre, float size, Color color, float strength)
        {
            Color tinted = new Color(color.r, color.g, color.b, color.a * strength);
            GuiKit.Disc(new Rect(centre.x - size * 0.5f, centre.y - size * 0.5f, size, size), tinted);
        }
    }
}
