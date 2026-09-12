using System.Text;
using UberBagarre.Combat;
using UberBagarre.Enemy;
using UberBagarre.Player;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Overlay de diagnostic (F1).
    ///
    /// Il existe parce qu'un système de combat échoue SILENCIEUSEMENT : un coup refusé, une
    /// fenêtre d'impact qui s'ouvre trop loin ou une donnée d'attaque incomplète produisent
    /// tous le même symptôme à l'écran — il ne se passe rien. Impossible de distinguer
    /// « l'entrée n'est pas lue », « le coup est refusé » et « le coup part mais n'atteint
    /// personne » sans afficher les trois.
    ///
    /// Le bouton de test déclenche un coup en contournant complètement les entrées : s'il
    /// fonctionne alors que le clic ne fait rien, le problème est dans les touches, pas dans
    /// le combat. Une seule manipulation sépare les deux cas.
    /// </summary>
    public class CombatDebugOverlay : MonoBehaviour
    {
        [Header("Joueur")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Combatant _player;
        [SerializeField] private AttackExecutor _playerExecutor;
        [SerializeField] private DodgeSystem _playerDodge;
        [SerializeField] private AttackData _testAttack;

        [Header("Ennemi")]
        [SerializeField] private EnemyBrain _enemyBrain;
        [SerializeField] private Combatant _enemy;
        [SerializeField] private AttackExecutor _enemyExecutor;

        [Header("Affichage")]
        [SerializeField]
        [Tooltip("Visible par defaut tant que le combat n'est pas stabilise. F1 pour masquer.")]
        private bool _visible = true;

        [SerializeField] private Color _background = new Color(0f, 0f, 0f, 0.8f);
        [SerializeField] private Color _text = new Color(0.86f, 0.94f, 0.86f);
        [SerializeField] private Color _alert = new Color(1f, 0.55f, 0.4f);

        private readonly StringBuilder _builder = new StringBuilder();
        private float _straightFlash;
        private float _hookFlash;
        private float _uppercutFlash;

        private void Update()
        {
            if (_input == null) return;

            if (_input.ToggleDebugOverlayPressed) _visible = !_visible;

            float dt = Time.unscaledDeltaTime;
            _straightFlash = _input.StraightPressed ? 1f : Mathf.MoveTowards(_straightFlash, 0f, dt * 3f);
            _hookFlash = _input.HookPressed ? 1f : Mathf.MoveTowards(_hookFlash, 0f, dt * 3f);
            _uppercutFlash = _input.UppercutPressed ? 1f : Mathf.MoveTowards(_uppercutFlash, 0f, dt * 3f);
        }

        private void OnGUI()
        {
            if (!_visible) return;

            Rect panel = new Rect(14f, 14f, 470f, 330f);
            GuiKit.Fill(panel, _background);
            GuiKit.Outline(panel, 2f, new Color(0f, 0f, 0f, 0.9f));

            _builder.Length = 0;
            AppendInputs();
            AppendPlayer();
            AppendEnemy();
            AppendRange();

            GUIStyle style = GuiKit.Style(13, FontStyle.Normal, TextAnchor.UpperLeft);
            style.padding = new RectOffset(12, 12, 10, 10);

            Color previous = GUI.contentColor;
            GUI.contentColor = _text;
            GUI.Label(panel, _builder.ToString(), style);
            GUI.contentColor = previous;

            DrawInputLamps(panel);
            DrawTestButton(panel);
            DrawReachMarkers();
        }

        /// <summary>
        /// Marque à l'écran la position du poing pendant la fenêtre d'impact, et celle de la
        /// cible. C'est la réponse visuelle directe à « pourquoi je ne touche pas » : si les
        /// deux marqueurs ne se chevauchent jamais, le coup passe à côté — et on voit de
        /// combien, ce qu'aucun chiffre ne montre aussi vite.
        /// </summary>
        private void DrawReachMarkers()
        {
            Camera camera = Camera.main;
            if (camera == null || _playerExecutor == null || !_playerExecutor.IsAttacking) return;

            Vector2 fist;
            if (GuiKit.WorldToGui(camera, _playerExecutor.ActiveFistPosition, out fist))
            {
                Color color = _playerExecutor.IsHitWindowOpen ? _alert : new Color(1f, 1f, 1f, 0.35f);
                Marker(fist, 26f, color, _playerExecutor.IsHitWindowOpen ? "POING (impact)" : "poing");
            }

            if (_enemy == null) return;

            Vector2 target;
            if (GuiKit.WorldToGui(camera, _enemy.AimPosition, out target))
            {
                Marker(target, 34f, new Color(0.4f, 0.85f, 1f, 0.8f), "cible");
            }
        }

        private void Marker(Vector2 centre, float size, Color color, string label)
        {
            float half = size * 0.5f;
            float thickness = 2f;

            GuiKit.Fill(new Rect(centre.x - half, centre.y - half, size, thickness), color);
            GuiKit.Fill(new Rect(centre.x - half, centre.y + half, size, thickness), color);
            GuiKit.Fill(new Rect(centre.x - half, centre.y - half, thickness, size), color);
            GuiKit.Fill(new Rect(centre.x + half, centre.y - half, thickness, size + thickness), color);

            GUIStyle style = GuiKit.Style(11, FontStyle.Bold, TextAnchor.MiddleCenter);
            GuiKit.OutlinedLabel(new Rect(centre.x - 60f, centre.y + half + 4f, 120f, 16f), label, style,
                color, new Color(0f, 0f, 0f, 0.85f), 1f);
        }

        private void AppendInputs()
        {
            _builder.AppendLine("=== UBER BAGARRE - DEBUG (F1) ===");

            if (_input == null)
            {
                _builder.AppendLine("!! aucun PlayerInputReader assigne");
                return;
            }

            _builder.AppendLine("Backend d'input : " + (_input.Provider != null ? _input.Provider.DisplayName : "aucun"));
            _builder.AppendLine("Entrees actives : " + (_input.GameplayInputEnabled ? "oui" : "NON (curseur libere ?)"));
            _builder.AppendLine();
        }

        private void AppendPlayer()
        {
            if (_player == null)
            {
                _builder.AppendLine("JOUEUR : absent");
                return;
            }

            _builder.AppendLine("JOUEUR  etat=" + _player.State.Current +
                                (_player.State.IsBusy ? " (" + _player.State.Remaining.ToString("0.00") + "s)" : "") +
                                "   vie=" + Mathf.CeilToInt(_player.Health != null ? _player.Health.Current : 0f) +
                                "   end=" + Mathf.CeilToInt(_player.Stamina != null ? _player.Stamina.Current : 0f));

            if (_playerExecutor != null)
            {
                _builder.AppendLine("  coup    : " + (_playerExecutor.IsAttacking
                    ? _playerExecutor.CurrentAttack.displayName + "  " + (_playerExecutor.Progress * 100f).ToString("0") + "%" +
                      (_playerExecutor.IsHitWindowOpen ? "   >>> IMPACT OUVERT <<<" : "")
                    : _playerExecutor.IsReady ? "pret" : "indisponible"));

                if (!string.IsNullOrEmpty(_playerExecutor.LastRefusal))
                {
                    _builder.AppendLine("  refus   : " + _playerExecutor.LastRefusal);
                }
            }

            if (_playerDodge != null)
            {
                _builder.AppendLine("  esquive : " + (_playerDodge.IsDodging ? "EN COURS" : "prete dans " +
                    _playerDodge.CooldownRemaining.ToString("0.00") + "s") +
                    (_playerDodge.IsInvulnerable ? "  [INVULNERABLE]" : ""));
            }

            _builder.AppendLine();
        }

        private void AppendEnemy()
        {
            if (_enemy == null)
            {
                _builder.AppendLine("ENNEMI : absent");
                return;
            }

            _builder.AppendLine("ENNEMI  etat=" + _enemy.State.Current +
                                "   vie=" + Mathf.CeilToInt(_enemy.Health != null ? _enemy.Health.Current : 0f));

            if (_enemyExecutor != null)
            {
                _builder.AppendLine("  coup    : " + (_enemyExecutor.IsAttacking
                    ? _enemyExecutor.CurrentAttack.displayName + "  " + (_enemyExecutor.Progress * 100f).ToString("0") + "%" +
                      (_enemyExecutor.IsHitWindowOpen ? "   >>> IMPACT OUVERT <<<" : "")
                    : _enemyExecutor.IsReady ? "pret" : "indisponible"));

                if (!string.IsNullOrEmpty(_enemyExecutor.LastRefusal))
                {
                    _builder.AppendLine("  refus   : " + _enemyExecutor.LastRefusal);
                }
            }

            if (_enemyBrain != null)
            {
                _builder.AppendLine("  cible   : " + (_enemyBrain.Target != null ? _enemyBrain.Target.DisplayName : "AUCUNE"));
            }

            _builder.AppendLine();
        }

        /// <summary>
        /// La ligne qui répond à « pourquoi je ne touche pas » : distance réelle entre les deux
        /// combattants, et distance du poing à la cible pendant la fenêtre d'impact.
        /// </summary>
        private void AppendRange()
        {
            if (_player == null || _enemy == null) return;

            float distance = _player.DistanceTo(_enemy);
            _builder.AppendLine("PORTEE  distance entre combattants : " + distance.ToString("0.00") + " m");

            if (_playerExecutor != null && _playerExecutor.IsAttacking)
            {
                float fistDistance = Vector3.Distance(_playerExecutor.ActiveFistPosition, _enemy.AimPosition);
                _builder.AppendLine("        poing -> tete ennemie      : " + fistDistance.ToString("0.00") + " m");
            }
        }

        /// <summary>Trois témoins : ils s'allument à la frame où le clic est LU. Si aucun ne s'allume, le problème est dans les touches.</summary>
        private void DrawInputLamps(Rect panel)
        {
            float y = panel.yMax - 66f;
            Lamp(new Rect(panel.x + 12f, y, 92f, 20f), "DIRECT", _straightFlash);
            Lamp(new Rect(panel.x + 112f, y, 92f, 20f), "CROCHET", _hookFlash);
            Lamp(new Rect(panel.x + 212f, y, 100f, 20f), "UPPERCUT", _uppercutFlash);
        }

        private void Lamp(Rect rect, string label, float flash)
        {
            GuiKit.Fill(rect, Color.Lerp(new Color(0.12f, 0.12f, 0.14f), new Color(0.3f, 0.95f, 0.4f), flash));
            GuiKit.Outline(rect, 1f, new Color(0f, 0f, 0f, 0.9f));

            GUIStyle style = GuiKit.Style(11, FontStyle.Bold, TextAnchor.MiddleCenter);
            GuiKit.OutlinedLabel(rect, label, style, Color.white, new Color(0f, 0f, 0f, 0.85f), 1f);
        }

        private void DrawTestButton(Rect panel)
        {
            if (_playerExecutor == null || _testAttack == null) return;

            Rect button = new Rect(panel.x + 12f, panel.yMax - 38f, 300f, 26f);

            if (GUI.Button(button, "FORCER UN DIRECT (ignore les touches)"))
            {
                bool played = _playerExecutor.TryPlay(_testAttack);

                Debug.Log("[UberBagarre] Test force : " + (played
                    ? "coup joue"
                    : "REFUSE -> " + _playerExecutor.LastRefusal));
            }
        }
    }
}
