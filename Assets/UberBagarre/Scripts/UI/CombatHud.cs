using UberBagarre.Combat;
using UberBagarre.Feedback;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// L'interface du joueur : la vie, l'endurance, l'étourdissement, et autour du réticule tout
    /// ce qui se joue en un dixième de seconde (garde, parade, charge, riposte).
    ///
    /// La vie est une barre inclinée en dix segments — dix pour cent chacun, on compte les coups
    /// qu'il reste sans lire de chiffre — avec la traînée blanche de ce qu'on vient de perdre, et
    /// sous un quart un battement de cœur qui s'accélère. L'endurance, fine, juste dessous : elle
    /// brille quand elle remonte, vire au rouge et le dit quand on est à bout de souffle. En
    /// combat le bloc est grand et net ; hors combat il se fait petit, sans disparaître.
    ///
    /// Dans le monde ouvert, en bas à droite : l'argent (qui compte quand il bouge) et le niveau ;
    /// au volant, le compteur de vitesse et le régime à la place.
    ///
    /// Dessiné en IMGUI : aucune police importée, aucun sprite, aucun Canvas. L'inclinaison est
    /// une matrice de cisaillement appliquée à GUI.matrix.
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

        [SerializeField]
        [Tooltip("Optionnel. Affiche la jauge d'etourdissement du joueur.")]
        private StunMeter _stun;

        [SerializeField]
        [Tooltip("Optionnel. Affiche la charge en cours et la fenetre de riposte.")]
        private AttackExecutor _executor;

        [SerializeField]
        [Tooltip("Optionnel (monde ouvert) : l'argent et le niveau, en bas a droite.")]
        private PlayerProgress _progress;

        [Header("Affichage")]
        [SerializeField]
        [Tooltip("Optionnel : hors combat, pas de reticule de combat et un bloc de vie reduit.")]
        private CombatPresence _presence;

        [SerializeField] private bool _visible = true;
        [SerializeField, Min(0f)] private float _margin = 34f;
        [SerializeField, Min(120f)] private float _barWidth = 360f;

        [SerializeField, Range(-0.8f, 0.8f)]
        [Tooltip("Cisaillement des barres : c'est ce qui donne l'elan de l'ensemble.")]
        private float _slant = -0.42f;

        [SerializeField, Range(4, 20)] private int _segments = 10;

        [Header("Couleurs")]
        [SerializeField] private Color _healthColor = new Color(0.30f, 0.95f, 0.62f);
        [SerializeField] private Color _healthMidColor = new Color(1f, 0.76f, 0.24f);
        [SerializeField] private Color _healthLowColor = new Color(1f, 0.22f, 0.22f);
        [SerializeField] private Color _staminaColor = new Color(0.36f, 0.82f, 1f);
        [SerializeField] private Color _trailColor = new Color(1f, 0.96f, 0.88f, 0.92f);
        [SerializeField] private Color _slotColor = new Color(0.02f, 0.025f, 0.035f, 0.78f);
        [SerializeField] private Color _edgeColor = new Color(0f, 0f, 0f, 0.9f);
        [SerializeField] private Color _moneyColor = new Color(0.55f, 1f, 0.6f);

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
        [SerializeField, Min(0f)] private float _trailDelay = 0.45f;
        [SerializeField, Min(0.01f)] private float _trailSpeed = 0.5f;
        [SerializeField, Min(0f)] private float _shakeAmplitude = 7f;

        private float _trail = 1f;
        private float _trailHold;
        private float _flash;
        private float _shake;
        private float _heal;
        private float _staminaShown = 1f;
        private float _staminaGain;
        private float _heartPhase;
        private float _money;
        private int _lastMoney = int.MinValue;
        private int _moneyDelta;
        private float _moneyDeltaAge = 99f;
        private float _levelFlash;
        private int _lastLevel = -1;
        private float _compact = 1f;

        public bool Visible
        {
            get { return _visible; }
            set { _visible = value; }
        }

        private void OnEnable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.Damaged += OnDamaged;
                _playerHealth.Healed += OnHealed;
            }
        }

        private void OnDisable()
        {
            if (_playerHealth != null)
            {
                _playerHealth.Damaged -= OnDamaged;
                _playerHealth.Healed -= OnHealed;
            }
        }

        private void OnDamaged(DamageInfo info)
        {
            _flash = 1f;
            _shake = Mathf.Clamp01(0.45f + info.Amount * 0.05f);
            _trailHold = _trailDelay;
        }

        private void OnHealed(float amount)
        {
            if (amount > 0.5f) _heal = 1f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;

            _flash = Mathf.MoveTowards(_flash, 0f, dt * 5f);
            _shake = Mathf.MoveTowards(_shake, 0f, dt * 3.2f);
            _heal = Mathf.MoveTowards(_heal, 0f, dt * 1.5f);

            if (_playerHealth != null)
            {
                if (_trailHold > 0f) _trailHold -= dt;
                else _trail = Mathf.MoveTowards(_trail, _playerHealth.Normalized, _trailSpeed * dt);
                if (_trail < _playerHealth.Normalized) _trail = _playerHealth.Normalized;

                // Le cœur bat plus vite à mesure que la vie baisse : 70 puis jusqu'à 150 par minute.
                float bpm = Mathf.Lerp(150f, 70f, Mathf.Clamp01(_playerHealth.Normalized / 0.25f));
                _heartPhase = Mathf.Repeat(_heartPhase + dt * bpm / 60f, 1f);
            }

            if (_playerStamina != null)
            {
                float value = _playerStamina.Normalized;
                _staminaGain = Mathf.MoveTowards(_staminaGain, value > _staminaShown + 0.0005f ? 1f : 0f, dt * 4f);
                _staminaShown = value;
            }

            float presence = _presence != null ? _presence.Weight : 1f;
            _compact = Mathf.MoveTowards(_compact, presence > 0.5f ? 1f : 0f, dt * 3f);

            if (_progress != null)
            {
                if (_lastMoney == int.MinValue)
                {
                    _lastMoney = _progress.Money;
                    _money = _progress.Money;
                }

                if (_progress.Money != _lastMoney)
                {
                    _moneyDelta = _progress.Money - _lastMoney;
                    _moneyDeltaAge = 0f;
                    _lastMoney = _progress.Money;
                }

                _money = Mathf.MoveTowards(_money, _progress.Money, Mathf.Max(30f, Mathf.Abs(_progress.Money - _money) * 3f) * dt);
                _moneyDeltaAge += dt;

                if (_lastLevel >= 0 && _progress.Level > _lastLevel) _levelFlash = 1f;
                _lastLevel = _progress.Level;
                _levelFlash = Mathf.MoveTowards(_levelFlash, 0f, dt * 0.5f);
            }
        }

        private void OnGUI()
        {
            // La cinematique d'avant-combat et l'ecran titre prennent l'ecran : pas d'interface
            // de jeu par-dessus.
            if (FightIntro.AnyPlaying || GameMenu.ShowingTitle) return;

            if (!_visible) return;

            bool driving = PlayerDriving.IsDriving;

            // Hors combat, le reticule se fait discret ; ce qui sert a se battre n'apparait
            // qu'en combat.
            float combat = _presence != null ? _presence.Weight : 1f;

            if (!driving)
            {
                GuiKit.Alpha = Mathf.Lerp(0.45f, 1f, combat);
                DrawCrosshair();

                GuiKit.Alpha = combat;
                if (combat > 0.01f)
                {
                    DrawAimedZone();
                    DrawGuard();
                    DrawCharge();
                    DrawRiposte();
                }
            }

            GuiKit.Alpha = 1f;
            DrawVitals(combat);

            if (driving) DrawSpeedometer();
            else DrawWallet();

            GuiKit.Alpha = 1f;
            DrawOutdatedWarning();
        }

        // ------------------------------------------------------------------ vie et endurance

        /// <summary>
        /// Le bloc de vie, en bas à gauche : un losange avec le chiffre, la barre de vie en
        /// segments, l'endurance et l'étourdissement dessous. Grand en combat, réduit hors combat.
        /// </summary>
        private void DrawVitals(float combat)
        {
            if (_playerHealth == null) return;

            float scale = Mathf.Lerp(0.78f, 1f, _compact);
            float alpha = Mathf.Lerp(0.82f, 1f, combat);
            float health = _playerHealth.Normalized;
            bool low = health <= 0.25f && _playerHealth.IsAlive;

            float shakeX = Mathf.Sin(Time.unscaledTime * 55f) * _shakeAmplitude * _shake;
            float shakeY = Mathf.Cos(Time.unscaledTime * 47f) * _shakeAmplitude * 0.5f * _shake;

            Vector2 origin = new Vector2(_margin + shakeX, Screen.height - _margin + shakeY);
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.ScaleAroundPivot(new Vector2(scale, scale), origin);
            GuiKit.Alpha = alpha;

            Color healthColor = HealthColor(health);
            float heart = low ? Heartbeat(_heartPhase) : 0f;

            // --- le losange : le chiffre de vie, lisible du coin de l'oeil
            Rect badge = new Rect(origin.x + 8f, origin.y - 74f, 70f, 70f);
            Matrix4x4 beforeBadge = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, badge.center);
            Rect diamond = new Rect(badge.center.x - 25f, badge.center.y - 25f, 50f, 50f);
            if (low)
            {
                float glow = 8f + heart * 10f;
                GuiKit.Fill(new Rect(diamond.x - glow, diamond.y - glow, diamond.width + glow * 2f, diamond.height + glow * 2f),
                    new Color(_healthLowColor.r, _healthLowColor.g, _healthLowColor.b, 0.18f + heart * 0.3f));
            }

            GuiKit.Fill(new Rect(diamond.x + 3f, diamond.y + 3f, diamond.width, diamond.height), new Color(0f, 0f, 0f, 0.45f));
            GuiKit.Outline(diamond, 3f, Color.Lerp(healthColor, Color.white, _flash * 0.7f));
            GuiKit.Fill(diamond, new Color(0.03f, 0.035f, 0.05f, 0.9f));
            GuiKit.Fill(new Rect(diamond.x, diamond.yMax - diamond.height * health, diamond.width, diamond.height * health),
                new Color(healthColor.r, healthColor.g, healthColor.b, 0.22f));
            GUI.matrix = beforeBadge;

            int fontSize = Mathf.RoundToInt(27f + _flash * 6f + heart * 4f);
            GuiKit.OutlinedLabel(new Rect(badge.x - 10f, badge.y + 14f, badge.width + 20f, 40f),
                Mathf.CeilToInt(_playerHealth.Current).ToString(), GuiKit.Style(fontSize, FontStyle.Bold, TextAnchor.MiddleCenter),
                Color.Lerp(healthColor, Color.white, 0.35f + _flash * 0.5f), new Color(0f, 0f, 0f, 0.95f), 2f);

            // --- les barres, cisaillées
            float x0 = origin.x + 90f;
            float width = _barWidth;
            Matrix4x4 beforeShear = GUI.matrix;
            Shear(new Vector2(x0, origin.y - 40f));

            // Titre et chiffres fins au-dessus de la barre.
            GUIStyle tag = GuiKit.Style(11, FontStyle.Bold, TextAnchor.LowerLeft);
            GuiKit.OutlinedLabel(new Rect(x0 + 2f, origin.y - 76f, 200f, 14f), low ? "BLESSE" : "VIE", tag,
                low ? new Color(1f, 0.45f, 0.4f, 0.6f + heart * 0.4f) : new Color(1f, 1f, 1f, 0.6f),
                new Color(0f, 0f, 0f, 0.8f), 1f);
            GuiKit.OutlinedLabel(new Rect(x0 + width - 120f, origin.y - 76f, 120f, 14f),
                Mathf.CeilToInt(_playerHealth.Current) + " / " + Mathf.RoundToInt(_playerHealth.MaxHealth),
                GuiKit.Style(11, FontStyle.Bold, TextAnchor.LowerRight), new Color(1f, 1f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.8f), 1f);

            Rect healthRect = new Rect(x0, origin.y - 60f, width, 20f);
            SegmentedBar(healthRect, health, _trail, healthColor);

            if (_heal > 0.01f)
            {
                GuiKit.Fill(new Rect(healthRect.x, healthRect.y, healthRect.width * health, healthRect.height),
                    new Color(0.7f, 1f, 0.75f, _heal * 0.35f));
            }

            if (_flash > 0.01f) GuiKit.Fill(healthRect, new Color(1f, 1f, 1f, _flash * 0.35f));
            if (low) GuiKit.Outline(healthRect, 2f, new Color(1f, 0.2f, 0.2f, 0.25f + heart * 0.6f));

            DrawStamina(new Rect(x0, origin.y - 33f, width * 0.84f, 9f));
            DrawStun(new Rect(x0, origin.y - 19f, width * 0.6f, 5f));

            GUI.matrix = beforeShear;
            GUI.matrix = previous;
            GuiKit.Alpha = 1f;
        }

        private void DrawStamina(Rect rect)
        {
            if (_playerStamina == null) return;

            bool empty = _playerStamina.IsEmpty || _playerStamina.IsExhausted;
            float value = _playerStamina.Normalized;
            float blink = empty ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 9f) : 1f;
            Color color = empty ? new Color(1f, 0.3f, 0.24f, blink) : Color.Lerp(_staminaColor, new Color(1f, 0.85f, 0.3f), value < 0.3f ? 0.6f : 0f);

            GuiKit.Fill(new Rect(rect.x + 2f, rect.y + 3f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.35f));
            GuiKit.Outline(rect, 1.5f, _edgeColor);
            GuiKit.Fill(rect, _slotColor);
            Rect fill = new Rect(rect.x, rect.y, rect.width * value, rect.height);
            GuiKit.Fill(fill, color);
            GuiKit.Fill(new Rect(fill.x, fill.y, fill.width, fill.height * 0.4f), new Color(1f, 1f, 1f, 0.25f));

            // Elle remonte : un reflet court le long de la barre.
            if (_staminaGain > 0.01f && fill.width > 12f)
            {
                float t = Mathf.Repeat(Time.unscaledTime * 0.9f, 1f);
                float gx = fill.x + fill.width * t;
                GuiKit.Fill(new Rect(gx - 6f, fill.y, 12f, fill.height), new Color(1f, 1f, 1f, 0.45f * _staminaGain));
            }

            // Des repères tous les quarts : un coup lourd coûte environ un quart.
            for (int i = 1; i < 4; i++)
            {
                GuiKit.Fill(new Rect(rect.x + rect.width * i / 4f - 1f, rect.y, 2f, rect.height), new Color(0f, 0f, 0f, 0.55f));
            }

            GUIStyle small = GuiKit.Style(10, FontStyle.Bold, TextAnchor.MiddleLeft);
            GuiKit.OutlinedLabel(new Rect(rect.xMax + 8f, rect.y - 3f, 220f, 14f),
                empty ? "A BOUT DE SOUFFLE" : "ENDURANCE", small,
                empty ? new Color(1f, 0.45f, 0.38f, blink) : new Color(1f, 1f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.8f), 1f);
        }

        private void DrawStun(Rect rect)
        {
            if (_stun == null || _stun.Normalized <= 0.005f) return;

            float value = _stun.Normalized;
            bool near = value > 0.75f;
            float blink = near ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 12f) : 1f;

            GuiKit.Fill(rect, new Color(0f, 0f, 0f, 0.7f));
            GuiKit.Fill(new Rect(rect.x, rect.y, rect.width * value, rect.height),
                Color.Lerp(new Color(1f, 0.82f, 0.3f), Color.white, value * value) * new Color(1f, 1f, 1f, blink));

            if (near)
            {
                GuiKit.OutlinedLabel(new Rect(rect.xMax + 8f, rect.y - 5f, 160f, 14f), "SONNE", GuiKit.Style(10, FontStyle.Bold, TextAnchor.MiddleLeft),
                    new Color(1f, 0.9f, 0.5f, blink), new Color(0f, 0f, 0f, 0.8f), 1f);
            }
        }

        /// <summary>Une barre en segments : chaque case vaut une part égale, la traînée claire derrière.</summary>
        private void SegmentedBar(Rect rect, float value, float trail, Color color)
        {
            int count = Mathf.Max(1, _segments);
            const float gap = 3f;
            float cell = (rect.width - gap * (count - 1)) / count;

            GuiKit.Fill(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.35f));

            for (int i = 0; i < count; i++)
            {
                Rect slot = new Rect(rect.x + i * (cell + gap), rect.y, cell, rect.height);
                GuiKit.Outline(slot, 1.5f, _edgeColor);
                GuiKit.Fill(slot, _slotColor);

                float fill = Mathf.Clamp01(value * count - i);
                float ghost = Mathf.Clamp01(trail * count - i);

                if (ghost > fill) GuiKit.Fill(new Rect(slot.x, slot.y, slot.width * ghost, slot.height), _trailColor);

                if (fill > 0f)
                {
                    Rect f = new Rect(slot.x, slot.y, slot.width * fill, slot.height);
                    GuiKit.Fill(f, color);
                    GuiKit.Fill(new Rect(f.x, f.y, f.width, f.height * 0.36f), new Color(1f, 1f, 1f, 0.24f));
                    GuiKit.Fill(new Rect(f.x, f.yMax - f.height * 0.22f, f.width, f.height * 0.22f), new Color(0f, 0f, 0f, 0.22f));
                }
            }
        }

        private Color HealthColor(float health)
        {
            if (health > 0.55f) return _healthColor;
            if (health > 0.28f) return Color.Lerp(_healthMidColor, _healthColor, (health - 0.28f) / 0.27f * 0.3f);
            return Color.Lerp(_healthLowColor, _healthMidColor, health / 0.28f * 0.25f);
        }

        /// <summary>Le double battement « poum-poum » d'un cœur, de 0 à 1.</summary>
        private static float Heartbeat(float phase)
        {
            float a = Mathf.Exp(-Mathf.Pow((phase - 0.08f) * 18f, 2f));
            float b = Mathf.Exp(-Mathf.Pow((phase - 0.26f) * 18f, 2f)) * 0.7f;
            return Mathf.Clamp01(a + b);
        }

        /// <summary>Cisaille le GUI autour d'une ligne horizontale : x += pente × (y − pivot).</summary>
        private void Shear(Vector2 pivot)
        {
            Matrix4x4 shear = Matrix4x4.identity;
            shear.m01 = _slant;
            shear.m03 = -_slant * pivot.y;
            GUI.matrix = GUI.matrix * shear;
        }

        // ------------------------------------------------------------------ monde ouvert

        /// <summary>L'argent (qui défile quand il change) et le niveau, en bas à droite.</summary>
        private void DrawWallet()
        {
            if (_progress == null) return;

            float right = Screen.width - _margin;
            float bottom = Screen.height - _margin;

            Matrix4x4 previous = GUI.matrix;
            Shear(new Vector2(right, bottom - 30f));

            Rect plate = new Rect(right - 210f, bottom - 72f, 210f, 34f);
            GuiKit.Fill(new Rect(plate.x + 3f, plate.y + 4f, plate.width, plate.height), new Color(0f, 0f, 0f, 0.35f));
            GuiKit.Fill(plate, new Color(0.03f, 0.035f, 0.05f, 0.82f));
            GuiKit.Fill(new Rect(plate.x, plate.y, 4f, plate.height), _moneyColor);

            GuiKit.OutlinedLabel(new Rect(plate.x + 12f, plate.y, plate.width - 22f, plate.height),
                FormatMoney(Mathf.RoundToInt(_money)) + " €", GuiKit.Style(22, FontStyle.Bold, TextAnchor.MiddleRight),
                _moneyColor, new Color(0f, 0f, 0f, 0.9f), 1.5f);

            // Le gain (ou la perte) qui monte et s'efface au-dessus.
            if (_moneyDeltaAge < 2.2f && _moneyDelta != 0)
            {
                float a = 1f - Mathf.Clamp01((_moneyDeltaAge - 1.4f) / 0.8f);
                float rise = _moneyDeltaAge * 14f;
                Color c = _moneyDelta > 0 ? _moneyColor : new Color(1f, 0.4f, 0.35f);
                GuiKit.OutlinedLabel(new Rect(plate.x, plate.y - 26f - rise, plate.width - 10f, 22f),
                    (_moneyDelta > 0 ? "+" : "−") + FormatMoney(Mathf.Abs(_moneyDelta)) + " €",
                    GuiKit.Style(16, FontStyle.Bold, TextAnchor.MiddleRight), new Color(c.r, c.g, c.b, a), new Color(0f, 0f, 0f, 0.9f * a), 1.5f);
            }

            // Niveau et progression.
            Rect level = new Rect(right - 210f, bottom - 20f, 210f, 8f);
            GuiKit.Fill(level, _slotColor);
            GuiKit.Fill(new Rect(level.x, level.y, level.width * Mathf.Clamp01(_progress.LevelProgress), level.height),
                Color.Lerp(new Color(1f, 0.82f, 0.35f), Color.white, _levelFlash));
            GuiKit.Outline(level, 1.5f, _edgeColor);

            float pulse = _levelFlash > 0f ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 10f) : 1f;
            GuiKit.OutlinedLabel(new Rect(level.x, level.y - 16f, 120f, 14f),
                (_levelFlash > 0.01f ? "NIVEAU SUPERIEUR !  " : "NIV. ") + _progress.Level,
                GuiKit.Style(11, FontStyle.Bold, TextAnchor.MiddleLeft),
                new Color(1f, 0.85f, 0.45f, pulse), new Color(0f, 0f, 0f, 0.85f), 1f);

            GUI.matrix = previous;
        }

        private static string FormatMoney(int value)
        {
            string digits = Mathf.Abs(value).ToString();
            System.Text.StringBuilder sb = new System.Text.StringBuilder(digits.Length + 4);
            for (int i = 0; i < digits.Length; i++)
            {
                if (i > 0 && (digits.Length - i) % 3 == 0) sb.Append(' ');
                sb.Append(digits[i]);
            }

            return (value < 0 ? "−" : "") + sb;
        }

        /// <summary>Au volant : la vitesse en gros, le régime en arc de graduations, le rapport.</summary>
        private void DrawSpeedometer()
        {
            PlayerDriving driving = PlayerDriving.Instance;
            DrivableCar car = driving != null ? driving.Vehicle : null;
            if (car == null) return;

            float radius = 74f;
            Vector2 center = new Vector2(Screen.width - _margin - radius - 10f, Screen.height - _margin - radius - 6f);

            GuiKit.Disc(new Rect(center.x - radius * 1.5f, center.y - radius * 1.5f, radius * 3f, radius * 3f), new Color(0f, 0f, 0f, 0.55f));

            // L'arc : 30 graduations sur 240 degrés, allumées selon le régime, rouges en haut.
            const int ticks = 30;
            float rpm = Mathf.Clamp01(car.Rpm);
            Matrix4x4 previous = GUI.matrix;
            for (int i = 0; i < ticks; i++)
            {
                float t = i / (float)(ticks - 1);
                float angle = -120f + t * 240f;
                bool lit = t <= rpm;
                Color color = t > 0.82f ? new Color(1f, 0.25f, 0.2f) : new Color(1f, 1f, 1f);
                color.a = lit ? 0.95f : 0.18f;

                GUI.matrix = previous;
                GUIUtility.RotateAroundPivot(angle, center);
                float length = i % 5 == 0 ? 14f : 9f;
                GuiKit.Fill(new Rect(center.x - 1.5f, center.y - radius, 3f, length), color);
            }

            GUI.matrix = previous;

            int kmh = Mathf.RoundToInt(car.SpeedKmh);
            GuiKit.OutlinedLabel(new Rect(center.x - 70f, center.y - 30f, 140f, 50f), kmh.ToString(),
                GuiKit.Style(42, FontStyle.Bold, TextAnchor.MiddleCenter), Color.white, new Color(0f, 0f, 0f, 0.9f), 2f);
            GuiKit.OutlinedLabel(new Rect(center.x - 70f, center.y + 14f, 140f, 18f), "KM/H",
                GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter), new Color(1f, 1f, 1f, 0.6f), new Color(0f, 0f, 0f, 0.9f), 1f);

            string gear = car.Gear == 0 ? "R" : Mathf.Abs(car.ForwardSpeed) < 0.3f ? "N" : car.Gear.ToString();
            Rect gearBox = new Rect(center.x - 16f, center.y + 36f, 32f, 26f);
            GuiKit.Fill(gearBox, new Color(0.03f, 0.035f, 0.05f, 0.9f));
            GuiKit.Outline(gearBox, 1.5f, new Color(1f, 0.82f, 0.35f, 0.9f));
            GuiKit.OutlinedLabel(gearBox, gear, GuiKit.Style(16, FontStyle.Bold, TextAnchor.MiddleCenter),
                new Color(1f, 0.85f, 0.45f), new Color(0f, 0f, 0f, 0.9f), 1f);

            GuiKit.OutlinedLabel(new Rect(center.x - 120f, center.y - radius - 34f, 240f, 16f), car.DisplayName.ToUpperInvariant(),
                GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter), new Color(1f, 1f, 1f, 0.7f), new Color(0f, 0f, 0f, 0.9f), 1f);

            string hint = driving.Hint;
            GuiKit.OutlinedLabel(new Rect(Screen.width * 0.5f - 300f, Screen.height - 34f, 600f, 18f),
                (string.IsNullOrEmpty(hint) ? "" : hint + "     ") + "ESPACE  frein a main     CLIC  klaxon",
                GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter), new Color(1f, 1f, 1f, 0.65f), new Color(0f, 0f, 0f, 0.9f), 1f);
        }

        /// <summary>
        /// Arc de charge autour du réticule.
        ///
        /// Une charge sans retour visuel est injouable : le joueur relâche au hasard, ne voit pas
        /// la différence, et conclut que la mécanique ne sert à rien. L'arc se remplit, puis
        /// devient blanc et pulse à charge pleine — c'est le signal « lâche maintenant ».
        /// </summary>
        private void DrawCharge()
        {
            if (_executor == null || !_executor.IsCharging) return;

            float level = _executor.ChargeProgress;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            bool full = level >= 0.999f;
            float pulse = full ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;

            Color color = full
                ? new Color(1f, 1f, 1f, pulse)
                : Color.Lerp(new Color(1f, 0.72f, 0.25f, 0.85f), new Color(1f, 0.35f, 0.2f, 0.95f), level);

            // Barre horizontale sous le reticule : lisible sans masquer la cible.
            float width = 86f;
            Rect rail = new Rect(cx - width * 0.5f, cy + 40f, width, 5f);

            GuiKit.Fill(rail, new Color(0f, 0f, 0f, 0.65f));
            GuiKit.Fill(new Rect(rail.x, rail.y, rail.width * level, rail.height), color);

            if (!full) return;

            GuiKit.OutlinedLabel(new Rect(cx - 80f, cy + 48f, 160f, 18f), "CHARGE PLEINE",
                GuiKit.Style(11, FontStyle.Bold, TextAnchor.MiddleCenter),
                new Color(1f, 1f, 1f, pulse), new Color(0f, 0f, 0f, 0.9f), 1f);
        }

        /// <summary>Fenêtre de riposte, juste après une parade réussie. Elle dure moins d'une seconde : elle doit sauter aux yeux.</summary>
        private void DrawRiposte()
        {
            if (_guard == null || !_guard.RiposteReady) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float pulse = 0.65f + 0.35f * Mathf.Sin(Time.unscaledTime * 16f);

            GuiKit.OutlinedLabel(new Rect(cx - 140f, cy - 120f, 280f, 26f),
                "RIPOSTE  x" + _guard.RiposteDamageMultiplier.ToString("0.0"),
                GuiKit.Style(19, FontStyle.Bold, TextAnchor.MiddleCenter),
                new Color(1f, 0.95f, 0.55f, pulse), new Color(0f, 0f, 0f, 0.9f), 2f);
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
