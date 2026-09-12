using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Interface de combat : vie et endurance du joueur, vie de l'adversaire, réticule.
    ///
    /// Dessinée en IMGUI et non avec un Canvas, volontairement : un projet Unity neuf n'a ni
    /// police, ni TextMeshPro, ni sprite. Cette interface ne dépend donc de RIEN et s'affiche
    /// identiquement dans les trois render pipelines. Tout est réglable dans l'Inspector.
    ///
    /// Le passage à un vrai Canvas se fera en remplaçant ce seul composant : rien d'autre ne
    /// lit ces valeurs.
    /// </summary>
    public class CombatHud : MonoBehaviour
    {
        [Header("Joueur")]
        [SerializeField] private HealthSystem _playerHealth;
        [SerializeField] private StaminaSystem _playerStamina;
        [SerializeField] private Combatant _player;

        [Header("Affichage")]
        [SerializeField] private bool _visible = true;
        [SerializeField, Min(40f)] private float _barWidth = 320f;
        [SerializeField, Min(6f)] private float _barHeight = 18f;
        [SerializeField, Min(0f)] private float _margin = 26f;
        [SerializeField, Min(1f)] private float _borderThickness = 2f;

        [Header("Couleurs")]
        [SerializeField] private Color _healthColor = new Color(0.78f, 0.17f, 0.14f);
        [SerializeField] private Color _healthLowColor = new Color(0.95f, 0.45f, 0.1f);
        [SerializeField] private Color _staminaColor = new Color(0.85f, 0.73f, 0.25f);
        [SerializeField] private Color _backgroundColor = new Color(0.05f, 0.05f, 0.06f, 0.72f);
        [SerializeField] private Color _borderColor = new Color(0f, 0f, 0f, 0.85f);
        [SerializeField] private Color _enemyColor = new Color(0.72f, 0.2f, 0.2f);

        [Header("Reticule")]
        [SerializeField] private bool _showCrosshair = true;
        [SerializeField, Min(1f)] private float _crosshairSize = 4f;
        [SerializeField] private Color _crosshairColor = new Color(1f, 1f, 1f, 0.55f);

        [Header("Adversaire")]
        [SerializeField, Min(0f)]
        [Tooltip("Distance au-dela de laquelle la barre de vie adverse disparait.")]
        private float _enemyBarRange = 14f;

        [SerializeField, Min(40f)] private float _enemyBarWidth = 260f;

        private Texture2D _pixel;

        /// <summary>Vitesse à laquelle la barre de vie « retardée » rattrape la vraie valeur.</summary>
        [Header("Effet de retard")]
        [SerializeField, Min(0f)] private float _trailSpeed = 0.35f;
        [SerializeField] private Color _trailColor = new Color(0.95f, 0.85f, 0.8f, 0.5f);

        private float _playerTrail = 1f;
        private float _enemyTrail = 1f;

        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        private void Awake()
        {
            _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
            _pixel.SetPixel(0, 0, Color.white);
            _pixel.Apply();
        }

        private void OnDestroy()
        {
            if (_pixel != null) Destroy(_pixel);
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            if (_playerHealth != null)
            {
                _playerTrail = Mathf.MoveTowards(_playerTrail, _playerHealth.Normalized, _trailSpeed * dt);
            }

            Combatant enemy = FindEnemy();
            float enemyNormalized = enemy != null && enemy.Health != null ? enemy.Health.Normalized : 1f;
            _enemyTrail = Mathf.MoveTowards(_enemyTrail, enemyNormalized, _trailSpeed * dt);
        }

        private void OnGUI()
        {
            if (!_visible || _pixel == null) return;

            DrawPlayerBars();
            DrawEnemyBar();
            DrawCrosshair();
        }

        private void DrawPlayerBars()
        {
            float x = _margin;
            float y = Screen.height - _margin - _barHeight * 2f - 8f;

            if (_playerHealth != null)
            {
                Color fill = _playerHealth.Normalized <= 0.3f ? _healthLowColor : _healthColor;
                DrawBar(new Rect(x, y, _barWidth, _barHeight), _playerHealth.Normalized, _playerTrail, fill);
                y += _barHeight + 8f;
            }

            if (_playerStamina != null)
            {
                DrawBar(new Rect(x, y, _barWidth * 0.78f, _barHeight * 0.62f),
                    _playerStamina.Normalized, _playerStamina.Normalized, _staminaColor);
            }
        }

        private void DrawEnemyBar()
        {
            Combatant enemy = FindEnemy();
            if (enemy == null || enemy.Health == null || !enemy.Health.IsAlive) return;

            float width = _enemyBarWidth;
            Rect rect = new Rect((Screen.width - width) * 0.5f, _margin, width, _barHeight * 0.8f);

            DrawBar(rect, enemy.Health.Normalized, _enemyTrail, _enemyColor);
        }

        private void DrawCrosshair()
        {
            if (!_showCrosshair) return;

            float size = _crosshairSize;
            Rect rect = new Rect((Screen.width - size) * 0.5f, (Screen.height - size) * 0.5f, size, size);

            Fill(rect, _crosshairColor);
        }

        /// <summary>
        /// Barre à deux couches : la couche « retard » descend lentement derrière la vraie valeur.
        /// C'est ce qui rend un gros coup lisible — on voit combien on vient de perdre.
        /// </summary>
        private void DrawBar(Rect rect, float value, float trail, Color fillColor)
        {
            Fill(new Rect(rect.x - _borderThickness, rect.y - _borderThickness,
                rect.width + _borderThickness * 2f, rect.height + _borderThickness * 2f), _borderColor);

            Fill(rect, _backgroundColor);

            if (trail > value)
            {
                Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(trail), rect.height), _trailColor);
            }

            Fill(new Rect(rect.x, rect.y, rect.width * Mathf.Clamp01(value), rect.height), fillColor);
        }

        private void Fill(Rect rect, Color color)
        {
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;
        }

        private Combatant FindEnemy()
        {
            if (_player == null) return null;
            return _player.FindNearestOpponent(_enemyBarRange);
        }
    }
}
