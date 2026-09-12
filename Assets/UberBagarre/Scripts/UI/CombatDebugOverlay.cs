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
        [SerializeField] private GuardSystem _playerGuard;
        [SerializeField] private AttackData _testAttack;

        [Header("Ennemi")]
        [SerializeField] private EnemyBrain _enemyBrain;
        [SerializeField] private Combatant _enemy;
        [SerializeField] private AttackExecutor _enemyExecutor;
        [SerializeField] private GuardSystem _enemyGuard;
        [SerializeField] private KnockdownSystem _enemyKnockdown;

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
        private float _kickFlash;
        private float _lowKickFlash;
        private float _guardFlash;
        private Hurtbox[] _enemyZones;

        private void Start()
        {
            // Collectees une fois : chercher les composants a chaque image d'OnGUI allouerait
            // un tableau par frame, et OnGUI est appele plusieurs fois par frame.
            if (_enemy != null) _enemyZones = _enemy.GetComponentsInChildren<Hurtbox>(true);
        }

        private void Update()
        {
            if (_input == null) return;

            if (_input.ToggleDebugOverlayPressed) _visible = !_visible;

            float dt = Time.unscaledDeltaTime;
            _straightFlash = _input.StraightPressed ? 1f : Mathf.MoveTowards(_straightFlash, 0f, dt * 3f);
            _hookFlash = _input.HookPressed ? 1f : Mathf.MoveTowards(_hookFlash, 0f, dt * 3f);
            _uppercutFlash = _input.UppercutPressed ? 1f : Mathf.MoveTowards(_uppercutFlash, 0f, dt * 3f);
            _kickFlash = _input.KickPressed ? 1f : Mathf.MoveTowards(_kickFlash, 0f, dt * 3f);
            _lowKickFlash = _input.LowKickPressed ? 1f : Mathf.MoveTowards(_lowKickFlash, 0f, dt * 3f);
            _guardFlash = _input.GuardHeld ? 1f : Mathf.MoveTowards(_guardFlash, 0f, dt * 3f);
        }

        private void OnGUI()
        {
            if (!_visible) return;

            Rect panel = new Rect(14f, 14f, 500f, 372f);
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
            DrawZoneMarkers();
            DrawReachMarkers();
        }

        /// <summary>
        /// Marque les trois zones touchables de l'adversaire, avec leur multiplicateur.
        ///
        /// Sans ça, les zones sont une règle invisible : le joueur voit des dégâts qui varient
        /// sans savoir où se trouve la frontière entre le torse et les jambes, donc sans jamais
        /// pouvoir apprendre à viser. C'est aussi la façon de vérifier d'un coup d'œil qu'une
        /// zone n'est pas placée n'importe où après un changement de proportions.
        /// </summary>
        private void DrawZoneMarkers()
        {
            if (_enemyZones == null) return;

            Camera camera = Camera.main;
            if (camera == null) return;

            for (int i = 0; i < _enemyZones.Length; i++)
            {
                Hurtbox zone = _enemyZones[i];
                if (zone == null) continue;

                Vector2 gui;
                if (!GuiKit.WorldToGui(camera, zone.transform.position, out gui)) continue;

                Marker(gui, 22f, ZoneColor(zone.Zone),
                    ZoneName(zone.Zone) + "  x" + zone.DamageMultiplier.ToString("0.00"));
            }
        }

        private static Color ZoneColor(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return new Color(1f, 0.45f, 0.35f, 0.85f);
                case HitZone.Leg: return new Color(0.55f, 0.85f, 1f, 0.85f);
                default: return new Color(0.95f, 0.9f, 0.6f, 0.85f);
            }
        }

        private static string ZoneName(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return "TETE";
                case HitZone.Leg: return "JAMBES";
                case HitZone.Arm: return "BRAS";
                default: return "CORPS";
            }
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
            if (!GuiKit.WorldToGui(camera, _playerExecutor.ActiveFistPosition, out fist)) return;

            bool open = _playerExecutor.IsHitWindowOpen;
            Color color = open ? _alert : new Color(1f, 1f, 1f, 0.35f);

            // La HAUTEUR du membre qui frappe est ce qui decide de la zone touchee. L'afficher
            // rend la regle verifiable : si le poing est a 1,27 m, il ne peut pas toucher une
            // zone qui s'arrete a 0,92 m, et le savoir evite de chercher un bug ailleurs.
            string label = (open ? "IMPACT  " : "") +
                           _playerExecutor.ActiveFistPosition.y.ToString("0.00") + " m";

            Marker(fist, 26f, color, label);
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

            if (_playerGuard != null)
            {
                _builder.AppendLine("  garde   : " + (_playerGuard.IsGuarding
                    ? (_playerGuard.InParryWindow ? ">>> FENETRE DE PARADE <<<" : "levee (blocage)")
                    : "baissee"));
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

            if (_enemyGuard != null)
            {
                _builder.AppendLine("  garde   : " + (_enemyGuard.IsGuarding ? "LEVEE (il va bloquer)" : "baissee"));
            }

            if (_enemyKnockdown != null)
            {
                _builder.AppendLine("  au sol  : " + (_enemyKnockdown.IsDown
                    ? "OUI (" + (_enemyKnockdown.Weight * 100f).ToString("0") + "% couche)"
                    : "non"));
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

        /// <summary>Un témoin par attaque : il s'allume à la frame où la touche est LUE. Si aucun ne s'allume, le problème est dans les touches.</summary>
        private void DrawInputLamps(Rect panel)
        {
            float y = panel.yMax - 66f;
            Lamp(new Rect(panel.x + 12f, y, 76f, 20f), "DIRECT", _straightFlash);
            Lamp(new Rect(panel.x + 94f, y, 80f, 20f), "CROCHET", _hookFlash);
            Lamp(new Rect(panel.x + 180f, y, 84f, 20f), "UPPERCUT", _uppercutFlash);
            Lamp(new Rect(panel.x + 270f, y, 62f, 20f), "PIED", _kickFlash);
            Lamp(new Rect(panel.x + 338f, y, 70f, 20f), "PIED BAS", _lowKickFlash);
            Lamp(new Rect(panel.x + 414f, y, 66f, 20f), "GARDE", _guardFlash);
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
