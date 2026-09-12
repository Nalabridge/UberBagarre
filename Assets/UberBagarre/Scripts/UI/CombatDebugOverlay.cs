using System.Text;
using UberBagarre.Combat;
using UberBagarre.Enemy;
using UberBagarre.Player;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Overlay de diagnostic, affiché avec F1.
    ///
    /// Il répond aux questions qu'on se pose en réglant un jeu de combat et auxquelles on ne
    /// peut PAS répondre à l'œil : dans quel état est chacun, combien de temps reste-t-il de
    /// récupération, la fenêtre d'impact est-elle ouverte, à quelle distance exacte sommes-nous.
    ///
    /// Désactivé par défaut, il ne coûte rien en jeu normal.
    /// </summary>
    public class CombatDebugOverlay : MonoBehaviour
    {
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Combatant _player;
        [SerializeField] private AttackExecutor _playerExecutor;
        [SerializeField] private DodgeSystem _playerDodge;
        [SerializeField] private EnemyBrain _enemyBrain;
        [SerializeField] private Combatant _enemy;
        [SerializeField] private AttackExecutor _enemyExecutor;

        [SerializeField] private bool _visible;
        [SerializeField] private Color _background = new Color(0f, 0f, 0f, 0.72f);
        [SerializeField] private Color _text = new Color(0.85f, 0.95f, 0.85f);

        private Texture2D _pixel;
        private readonly StringBuilder _builder = new StringBuilder();

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
            if (_input != null && _input.ToggleDebugOverlayPressed) _visible = !_visible;
        }

        private void OnGUI()
        {
            if (!_visible) return;

            _builder.Length = 0;
            _builder.AppendLine("=== UBER BAGARRE - DEBUG (F1) ===");

            AppendCombatant("JOUEUR", _player, _playerExecutor);

            if (_playerDodge != null)
            {
                _builder.AppendLine("  esquive      : " + (_playerDodge.IsDodging ? "EN COURS" : "prete dans " +
                    _playerDodge.CooldownRemaining.ToString("0.00") + " s") +
                    (_playerDodge.IsInvulnerable ? "  [INVULNERABLE]" : ""));
            }

            _builder.AppendLine();
            AppendCombatant("ENNEMI", _enemy, _enemyExecutor);

            if (_enemyBrain != null)
            {
                _builder.AppendLine("  distance     : " + _enemyBrain.DistanceToTarget.ToString("0.00") + " m");
                _builder.AppendLine("  cible        : " + (_enemyBrain.Target != null ? _enemyBrain.Target.DisplayName : "aucune"));
            }

            _builder.AppendLine();
            _builder.AppendLine("Temps : x" + Time.timeScale.ToString("0.00"));

            string content = _builder.ToString();
            Rect rect = new Rect(14f, 14f, 420f, 260f);

            Color previous = GUI.color;
            GUI.color = _background;
            GUI.DrawTexture(rect, _pixel);
            GUI.color = previous;

            GUIStyle style = new GUIStyle(GUI.skin.label);
            style.normal.textColor = _text;
            style.wordWrap = false;
            style.padding = new RectOffset(12, 12, 10, 10);

            GUI.Label(rect, content, style);
        }

        private void AppendCombatant(string label, Combatant combatant, AttackExecutor executor)
        {
            if (combatant == null)
            {
                _builder.AppendLine(label + " : absent");
                return;
            }

            _builder.AppendLine(label + " (" + combatant.DisplayName + ")");
            _builder.AppendLine("  etat         : " + combatant.State.Current +
                                (combatant.State.IsBusy ? "  (" + combatant.State.Remaining.ToString("0.00") + " s)" : ""));

            if (combatant.Health != null)
            {
                _builder.AppendLine("  vie          : " + combatant.Health.Current.ToString("0") + " / " +
                                    combatant.Health.MaxHealth.ToString("0"));
            }

            if (combatant.Stamina != null)
            {
                _builder.AppendLine("  endurance    : " + combatant.Stamina.Current.ToString("0") + " / " +
                                    combatant.Stamina.Max.ToString("0"));
            }

            if (executor != null)
            {
                _builder.AppendLine("  attaque      : " +
                    (executor.IsAttacking
                        ? executor.CurrentAttack.displayName + "  " + (executor.Progress * 100f).ToString("0") + " %"
                        : executor.IsReady ? "prete" : "recuperation"));
            }
        }
    }
}
