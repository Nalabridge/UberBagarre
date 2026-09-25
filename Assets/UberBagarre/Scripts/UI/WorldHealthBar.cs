using UberBagarre.Combat;
using UberBagarre.Enemy;
using UberBagarre.Player;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Barre de vie affichée au-dessus d'un combattant, dans le monde.
    ///
    /// En première personne, une barre reléguée en haut de l'écran oblige à quitter des yeux
    /// l'adversaire au moment précis où il faut le regarder. Au-dessus de sa tête,
    /// l'information est là où le regard se trouve déjà.
    ///
    /// La barre rétrécit avec la distance et disparaît au-delà d'une portée : elle ne pollue
    /// pas l'écran quand l'ennemi est loin.
    /// </summary>
    public class WorldHealthBar : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private Combatant _combatant;
        [SerializeField] private Camera _camera;

        [SerializeField]
        [Tooltip("Optionnel. Affiche la jauge d'etourdissement sous la barre de vie.")]
        private StunMeter _stun;

        [Header("Placement")]
        [SerializeField, Min(0f)] private float _heightAboveHead = 0.45f;
        [SerializeField, Min(1f)] private float _maxDistance = 18f;

        [SerializeField, Min(20f)] private float _referenceWidth = 190f;
        [SerializeField, Min(4f)] private float _referenceHeight = 16f;

        [SerializeField, Min(0.1f)]
        [Tooltip("Distance a laquelle la barre a sa taille de reference.")]
        private float _referenceDistance = 3.5f;

        [SerializeField] private Vector2 _scaleClamp = new Vector2(0.5f, 1.5f);

        [Header("Style")]
        [SerializeField] private Color _fillColor = new Color(0.85f, 0.22f, 0.18f);
        [SerializeField] private Color _lowColor = new Color(0.95f, 0.55f, 0.12f);
        [SerializeField] private Color _trailColor = new Color(1f, 0.92f, 0.75f, 0.85f);
        [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.05f, 0.07f, 0.85f);
        [SerializeField] private Color _borderColor = new Color(0f, 0f, 0f, 0.95f);
        [SerializeField] private Color _stunColor = new Color(1f, 0.86f, 0.25f);
        [SerializeField] private bool _showName = true;

        [Header("Animation")]
        [SerializeField, Min(0f)] private float _trailDelay = 0.35f;
        [SerializeField, Min(0.01f)] private float _trailSpeed = 0.55f;
        [SerializeField, Min(0.01f)] private float _flashDuration = 0.14f;
        [SerializeField, Min(0f)] private float _shakeAmplitude = 7f;

        private float _trail = 1f;
        private float _visibility;
        private float _lastHit = -100f;
        private EnemyBrain _brain;
        private float _trailHold;
        private float _flash;
        private float _shake;

        private void Awake()
        {
            if (_combatant == null) _combatant = GetComponent<Combatant>();
            if (_camera == null) _camera = Camera.main;
            _brain = GetComponent<EnemyBrain>();
        }

        private void OnEnable()
        {
            if (_combatant != null) _combatant.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_combatant != null) _combatant.Damaged -= OnDamaged;
        }

        private void OnDamaged(Combatant combatant, DamageInfo info)
        {
            _flash = 1f;
            _shake = 1f;
            _trailHold = _trailDelay;
            _lastHit = Time.time;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _flash = Mathf.MoveTowards(_flash, 0f, dt / Mathf.Max(0.01f, _flashDuration));

            // La barre n'existe que pendant un combat : un passant, ou l'adversaire avant qu'il
            // ne s'engage, n'a pas de jauge au-dessus de la tete.
            bool engaged = _brain == null || (_brain.enabled && !EnemyBrain.HoldAll);
            bool shown = (engaged || Time.time - _lastHit < 5f) &&
                         (CombatPresence.Player == null || CombatPresence.Player.InCombat);
            _visibility = Mathf.MoveTowards(_visibility, shown ? 1f : 0f, dt * 3f);
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 3.5f);

            if (_trailHold > 0f)
            {
                // La couche de retard reste en place un instant avant de descendre :
                // c'est ce temps d'arret qui rend le coup lisible.
                _trailHold -= dt;
            }
            else if (_combatant != null && _combatant.Health != null)
            {
                _trail = Mathf.MoveTowards(_trail, _combatant.Health.Normalized, _trailSpeed * dt);
            }
        }

        private void OnGUI()
        {
            // La cinematique d'avant-combat prend l'ecran : pas d'interface de jeu par-dessus.
            if (FightIntro.AnyPlaying) return;

            if (_combatant == null || _combatant.Health == null) return;
            if (!_combatant.Health.IsAlive || _visibility < 0.01f) return;

            GuiKit.Alpha = _visibility;
            Draw();
            GuiKit.Alpha = 1f;
        }

        private void Draw()
        {

            _camera = GuiKit.ActiveCamera(_camera);
            if (_camera == null) return;

            Vector3 worldPosition = _combatant.AimPosition + Vector3.up * _heightAboveHead;

            float distance = Vector3.Distance(_camera.transform.position, worldPosition);
            if (distance > _maxDistance) return;

            Vector2 gui;
            if (!GuiKit.WorldToGui(_camera, worldPosition, out gui)) return;

            float scale = Mathf.Clamp(_referenceDistance / Mathf.Max(0.2f, distance), _scaleClamp.x, _scaleClamp.y);
            float width = _referenceWidth * scale;
            float height = _referenceHeight * scale;

            // Secousse laterale a l'impact : la barre encaisse visuellement, elle aussi.
            float shakeOffset = Mathf.Sin(Time.unscaledTime * 60f) * _shakeAmplitude * _shake * scale;

            Rect rect = new Rect(gui.x - width * 0.5f + shakeOffset, gui.y - height * 0.5f, width, height);

            float normalized = _combatant.Health.Normalized;
            Color fill = normalized <= 0.3f ? _lowColor : _fillColor;

            GuiKit.Bar(rect, normalized, _trail, fill, _trailColor, _backgroundColor, _borderColor,
                Mathf.Max(1f, 2f * scale), _flash);

            DrawStun(rect, scale);

            if (!_showName) return;

            GUIStyle style = GuiKit.Style(Mathf.RoundToInt(14f * scale), FontStyle.Bold, TextAnchor.MiddleCenter);
            GuiKit.OutlinedLabel(new Rect(rect.x, rect.y - 20f * scale, rect.width, 18f * scale),
                _combatant.DisplayName, style, new Color(1f, 0.95f, 0.9f), new Color(0f, 0f, 0f, 0.9f), 1.5f);
        }

        /// <summary>
        /// Jauge d'étourdissement, fine, sous la barre de vie.
        ///
        /// Elle doit se voir SUR l'adversaire et pas dans un coin de l'écran : c'est une ressource
        /// qu'on remplit en le frappant, donc l'information appartient à lui. Sans affichage, la
        /// mécanique existe mais personne ne peut jouer avec — on ne saurait jamais qu'on est à
        /// deux coups de l'ouvrir, et c'est précisément cette anticipation qui la rend utile.
        /// </summary>
        private void DrawStun(Rect healthRect, float scale)
        {
            if (_stun == null) return;

            float value = _stun.Normalized;
            if (value <= 0.005f && !_stun.Immune) return;

            Rect rect = new Rect(healthRect.x, healthRect.yMax + 2f * scale,
                healthRect.width, Mathf.Max(2f, 5f * scale));

            GuiKit.Fill(rect, new Color(0f, 0f, 0f, 0.7f));

            // Pendant le temps mort qui suit un etourdissement, la jauge se grise : le joueur voit
            // qu'il ne sert a rien d'insister pour re-sonner tout de suite.
            Color color = _stun.Immune
                ? new Color(0.45f, 0.45f, 0.48f, 0.8f)
                : Color.Lerp(_stunColor, Color.white, value * value);

            GuiKit.Fill(new Rect(rect.x, rect.y, rect.width * (_stun.Immune ? 1f : value), rect.height), color);
        }
    }
}
