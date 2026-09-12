using UberBagarre.Combat;
using UberBagarre.Feedback;
using UberBagarre.Player;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// HUD de combat stylisé, inspiré des jeux de combat : plaque inclinée, gros chiffre à
    /// contour épais, jauges biseautées.
    ///
    /// Le gros chiffre existe pour une raison précise : en pleine action, on ne lit pas une
    /// barre, on la perçoit. Un chiffre qui change de couleur et qui tressaute donne l'état de
    /// santé d'un coup d'œil périphérique, sans quitter l'adversaire des yeux.
    ///
    /// Dessiné en IMGUI : aucune police importée, aucun sprite, aucun Canvas — donc rendu
    /// identique dans les trois render pipelines d'un projet neuf.
    /// </summary>
    public class CombatHud : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HealthSystem _playerHealth;
        [SerializeField] private StaminaSystem _playerStamina;
        [SerializeField] private Combatant _player;

        [SerializeField]
        [Tooltip("Optionnel. Affiche l'etat de la garde et la fenetre de parade.")]
        private GuardSystem _guard;

        [SerializeField]
        [Tooltip("Optionnel. Fournit les eclats de parade et de blocage.")]
        private CombatFeedbackRelay _relay;

        [SerializeField]
        [Tooltip("Optionnel. Sert a prevenir a l'ecran quand les donnees de coups sont perimees.")]
        private PlayerCombat _combat;

        [Header("Affichage")]
        [SerializeField] private bool _visible = true;
        [SerializeField, Min(0f)] private float _margin = 30f;
        [SerializeField, Min(80f)] private float _panelWidth = 330f;
        [SerializeField, Min(50f)] private float _panelHeight = 116f;

        [SerializeField, Range(-12f, 12f)]
        [Tooltip("Inclinaison de la plaque. C'est ce qui donne le cachet 'jeu de combat'.")]
        private float _tiltAngle = -3.5f;

        [Header("Couleurs")]
        [SerializeField] private Color _panelColor = new Color(0.07f, 0.08f, 0.11f, 0.88f);
        [SerializeField] private Color _panelBorder = new Color(0.95f, 0.85f, 0.45f, 0.95f);
        [SerializeField] private Color _healthColor = new Color(0.36f, 0.82f, 0.38f);
        [SerializeField] private Color _healthMidColor = new Color(0.95f, 0.78f, 0.2f);
        [SerializeField] private Color _healthLowColor = new Color(0.92f, 0.25f, 0.2f);
        [SerializeField] private Color _staminaColor = new Color(0.35f, 0.7f, 0.95f);
        [SerializeField] private Color _trailColor = new Color(1f, 0.95f, 0.85f, 0.9f);
        [SerializeField] private Color _barBackground = new Color(0.03f, 0.03f, 0.05f, 0.92f);
        [SerializeField] private Color _barBorder = new Color(0f, 0f, 0f, 0.95f);

        [Header("Reticule")]
        [SerializeField] private bool _showCrosshair = true;
        [SerializeField, Min(1f)] private float _crosshairSize = 3f;
        [SerializeField, Min(1f)] private float _crosshairGap = 7f;
        [SerializeField, Min(1f)] private float _crosshairLength = 7f;
        [SerializeField] private Color _crosshairColor = new Color(1f, 1f, 1f, 0.6f);

        [Header("Zone visee")]
        [SerializeField]
        [Tooltip("Annonce sous le reticule la zone du corps actuellement visee. A laisser actif : " +
                 "c'est la seule facon pour le joueur d'apprendre ou viser.")]
        private bool _showAimedZone = true;

        [SerializeField] private Color _zoneHeadColor = new Color(1f, 0.45f, 0.35f);
        [SerializeField] private Color _zoneBodyColor = new Color(1f, 0.93f, 0.72f);
        [SerializeField] private Color _zoneLegColor = new Color(0.58f, 0.86f, 1f);

        [Header("Garde")]
        [SerializeField] private Color _guardColor = new Color(0.55f, 0.75f, 1f, 0.75f);
        [SerializeField] private Color _parryColor = new Color(1f, 0.95f, 0.55f);
        [SerializeField] private Color _blockColor = new Color(0.6f, 0.8f, 1f);

        [Header("Animation de degats")]
        [SerializeField, Min(0f)] private float _trailDelay = 0.4f;
        [SerializeField, Min(0.01f)] private float _trailSpeed = 0.45f;
        [SerializeField, Min(0f)] private float _shakeAmplitude = 9f;

        private float _trail = 1f;
        private float _trailHold;
        private float _flash;
        private float _shake;

        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        private void OnEnable()
        {
            if (_playerHealth != null) _playerHealth.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_playerHealth != null) _playerHealth.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            _flash = 1f;
            _shake = 1f;
            _trailHold = _trailDelay;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _flash = Mathf.MoveTowards(_flash, 0f, dt * 6f);
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 3.2f);

            if (_trailHold > 0f) _trailHold -= dt;
            else if (_playerHealth != null) _trail = Mathf.MoveTowards(_trail, _playerHealth.Normalized, _trailSpeed * dt);
        }

        private void OnGUI()
        {
            if (!_visible) return;

            DrawCrosshair();
            DrawAimedZone();
            DrawGuard();
            DrawPlayerPanel();
            DrawOutdatedWarning();
        }

        /// <summary>
        /// Bandeau d'alerte quand les données de coups sont périmées.
        ///
        /// Il est à l'ÉCRAN et pas seulement dans la console, parce qu'une console peut très bien
        /// ne jamais être regardée — et que le symptôme de données périmées est « rien n'a changé »,
        /// donc indiscernable de « il ne l'a pas fait ». Une alerte muette sur ce point précis
        /// coûte un aller-retour de test complet.
        /// </summary>
        private void DrawOutdatedWarning()
        {
            if (_combat == null || _combat.OutdatedAttacks <= 0) return;

            float height = 34f;
            Rect banner = new Rect(0f, 0f, Screen.width, height);

            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            GuiKit.Fill(banner, new Color(0.55f, 0.08f, 0.06f, 0.92f * pulse));

            GuiKit.OutlinedLabel(banner,
                _combat.OutdatedAttacks + " coup(s) perimes  —  lance  Uber Bagarre > 2 - Construire la scene",
                GuiKit.Style(15, FontStyle.Bold, TextAnchor.MiddleCenter),
                Color.white, new Color(0f, 0f, 0f, 0.9f), 2f);
        }

        /// <summary>
        /// Nom de la zone visée, juste sous le réticule.
        ///
        /// C'est l'information qui rend le système de zones jouable. Trois multiplicateurs de
        /// dégâts différents ne valent rien si le joueur ne sait pas, AVANT de frapper, lequel
        /// il est en train de cibler : il frapperait au hasard et conclurait que les dégâts
        /// sont aléatoires. Le chiffre affiché après le coup arrive trop tard pour viser.
        ///
        /// Le même résolveur sert ici et au combat, donc ce qui est annoncé est exactement ce
        /// qui sera touché — il ne peut pas y avoir de désaccord entre les deux.
        /// </summary>
        private void DrawAimedZone()
        {
            if (!_showAimedZone || _player == null) return;

            Hurtbox zone = AimResolver.Resolve(_player);
            if (zone == null) return;

            Color color = ZoneColor(zone.Zone);
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            GUIStyle style = GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter);

            GuiKit.OutlinedLabel(new Rect(cx - 90f, cy + 22f, 180f, 18f),
                ZoneName(zone.Zone) + "   x" + zone.DamageMultiplier.ToString("0.0"), style,
                new Color(color.r, color.g, color.b, 0.9f), new Color(0f, 0f, 0f, 0.9f), 1.5f);
        }

        private Color ZoneColor(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return _zoneHeadColor;
                case HitZone.Leg: return _zoneLegColor;
                default: return _zoneBodyColor;
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
        /// État de la garde, autour du réticule.
        ///
        /// Tout ce qui concerne la défense s'affiche AU CENTRE de l'écran, et pas dans le coin
        /// avec la vie : une parade se joue en deux dixièmes de seconde, et on n'a pas le temps
        /// de regarder ailleurs que l'adversaire. Les crochets disent « je suis couvert », le
        /// cercle plein dit « fenêtre de parade ouverte », l'éclat dit ce qui vient de se passer.
        /// </summary>
        private void DrawGuard()
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            if (_guard != null && _guard.IsGuarding)
            {
                bool parryWindow = _guard.InParryWindow;
                float radius = parryWindow ? 30f : 36f;
                float thickness = parryWindow ? 4f : 2.5f;
                Color color = parryWindow ? _parryColor : _guardColor;

                // Quatre crochets plutot qu'un cercle : ils ne masquent pas la cible, et leur
                // resserrement pendant la fenetre de parade se lit du coin de l'oeil.
                GuiKit.Fill(new Rect(cx - radius, cy - radius, 12f, thickness), color);
                GuiKit.Fill(new Rect(cx - radius, cy - radius, thickness, 12f), color);
                GuiKit.Fill(new Rect(cx + radius - 12f, cy - radius, 12f, thickness), color);
                GuiKit.Fill(new Rect(cx + radius - thickness, cy - radius, thickness, 12f), color);
                GuiKit.Fill(new Rect(cx - radius, cy + radius - thickness, 12f, thickness), color);
                GuiKit.Fill(new Rect(cx - radius, cy + radius - 12f, thickness, 12f), color);
                GuiKit.Fill(new Rect(cx + radius - 12f, cy + radius - thickness, 12f, thickness), color);
                GuiKit.Fill(new Rect(cx + radius - thickness, cy + radius - 12f, thickness, 12f), color);
            }

            if (_relay == null) return;

            if (_relay.BlockFlash > 0.01f)
            {
                float size = Mathf.Lerp(120f, 64f, _relay.BlockFlash);
                GuiKit.Disc(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size),
                    new Color(_blockColor.r, _blockColor.g, _blockColor.b, _relay.BlockFlash * 0.30f));
            }

            if (_relay.ParryFlash > 0.01f)
            {
                float size = Mathf.Lerp(260f, 90f, _relay.ParryFlash);
                GuiKit.Disc(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size),
                    new Color(_parryColor.r, _parryColor.g, _parryColor.b, _relay.ParryFlash * 0.55f));

                GUIStyle style = GuiKit.Style(Mathf.RoundToInt(20f + _relay.ParryFlash * 10f),
                    FontStyle.Bold, TextAnchor.MiddleCenter);

                GuiKit.OutlinedLabel(new Rect(cx - 160f, cy - 92f, 320f, 34f), "PARADE !", style,
                    new Color(1f, 0.97f, 0.8f, Mathf.Clamp01(_relay.ParryFlash * 1.4f)),
                    new Color(0f, 0f, 0f, 0.9f), 2.5f);
            }
        }

        private void DrawPlayerPanel()
        {
            if (_playerHealth == null) return;

            float shakeX = Mathf.Sin(Time.unscaledTime * 55f) * _shakeAmplitude * _shake;
            float shakeY = Mathf.Cos(Time.unscaledTime * 47f) * _shakeAmplitude * 0.5f * _shake;

            Rect panel = new Rect(
                _margin + shakeX,
                Screen.height - _margin - _panelHeight + shakeY,
                _panelWidth, _panelHeight);

            Matrix4x4 previousMatrix = GUI.matrix;
            GUIUtility.RotateAroundPivot(_tiltAngle, panel.center);

            GuiKit.Fill(new Rect(panel.x + 4f, panel.y + 5f, panel.width, panel.height), new Color(0f, 0f, 0f, 0.4f));
            GuiKit.Outline(panel, 3f, _panelBorder);
            GuiKit.Fill(panel, _panelColor);

            // Bandeau superieur : rappelle la plaque de nom des jeux de combat.
            GuiKit.Fill(new Rect(panel.x, panel.y, panel.width, 22f), new Color(1f, 1f, 1f, 0.07f));

            GUIStyle nameStyle = GuiKit.Style(15, FontStyle.Bold, TextAnchor.MiddleLeft);
            GuiKit.OutlinedLabel(new Rect(panel.x + 14f, panel.y + 2f, 200f, 20f), "JOUEUR", nameStyle,
                new Color(0.95f, 0.9f, 0.75f), new Color(0f, 0f, 0f, 0.9f), 1.5f);

            float normalized = _playerHealth.Normalized;
            Color healthColor = normalized > 0.55f
                ? _healthColor
                : normalized > 0.28f ? _healthMidColor : _healthLowColor;

            // Gros chiffre : on le percoit en vision peripherique, contrairement a une barre.
            int fontSize = Mathf.RoundToInt(46f + _flash * 8f);
            GUIStyle bigStyle = GuiKit.Style(fontSize, FontStyle.Bold, TextAnchor.MiddleLeft);

            GuiKit.OutlinedLabel(new Rect(panel.x + 14f, panel.y + 26f, 160f, 52f),
                Mathf.CeilToInt(_playerHealth.Current).ToString(), bigStyle,
                Color.Lerp(healthColor, Color.white, _flash * 0.8f), new Color(0f, 0f, 0f, 0.95f), 3f);

            GUIStyle maxStyle = GuiKit.Style(16, FontStyle.Bold, TextAnchor.MiddleLeft);
            GuiKit.OutlinedLabel(new Rect(panel.x + 14f, panel.y + 62f, 160f, 20f),
                "/ " + Mathf.RoundToInt(_playerHealth.MaxHealth), maxStyle,
                new Color(1f, 1f, 1f, 0.55f), new Color(0f, 0f, 0f, 0.8f), 1.5f);

            float barX = panel.x + 118f;
            float barWidth = panel.width - 132f;

            GuiKit.Bar(new Rect(barX, panel.y + 34f, barWidth, 20f), normalized, _trail,
                healthColor, _trailColor, _barBackground, _barBorder, 2.5f, _flash);

            if (_playerStamina != null)
            {
                bool empty = _playerStamina.IsEmpty;

                // Endurance vide = coups refuses, sprint coupe, glissade interdite. C'est la
                // premiere cause de "je ne peux plus rien faire", et elle etait signalee par une
                // barre grise de 11 pixels. Elle clignote maintenant et se nomme, parce qu'une
                // regle qui bloque le joueur doit lui dire qu'elle le bloque.
                float blink = empty ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;

                Color staminaColor = empty
                    ? new Color(0.95f, 0.32f, 0.25f, blink)
                    : _staminaColor;

                GuiKit.Bar(new Rect(barX, panel.y + 62f, barWidth * 0.86f, 11f),
                    _playerStamina.Normalized, _playerStamina.Normalized,
                    staminaColor, _trailColor, _barBackground, _barBorder, 2f, 0f);

                GUIStyle small = GuiKit.Style(11, FontStyle.Bold, TextAnchor.MiddleLeft);

                GuiKit.OutlinedLabel(new Rect(barX, panel.y + 78f, 200f, 16f),
                    empty ? "ENDURANCE EPUISEE" : "ENDURANCE  " + Mathf.CeilToInt(_playerStamina.Current),
                    small,
                    empty ? new Color(1f, 0.45f, 0.38f, blink) : new Color(1f, 1f, 1f, 0.5f),
                    new Color(0f, 0f, 0f, 0.8f), 1f);
            }

            GUI.matrix = previousMatrix;
        }

        /// <summary>Réticule en quatre traits : il marque le centre sans masquer la cible.</summary>
        private void DrawCrosshair()
        {
            if (!_showCrosshair) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float t = _crosshairSize;
            float gap = _crosshairGap;
            float length = _crosshairLength;

            GuiKit.Fill(new Rect(cx - gap - length, cy - t * 0.5f, length, t), _crosshairColor);
            GuiKit.Fill(new Rect(cx + gap, cy - t * 0.5f, length, t), _crosshairColor);
            GuiKit.Fill(new Rect(cx - t * 0.5f, cy - gap - length, t, length), _crosshairColor);
            GuiKit.Fill(new Rect(cx - t * 0.5f, cy + gap, t, length), _crosshairColor);
        }
    }
}
