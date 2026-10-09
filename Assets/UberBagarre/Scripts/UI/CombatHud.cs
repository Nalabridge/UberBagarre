using UberBagarre.Combat;
using UberBagarre.Core;
using UberBagarre.Feedback;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// L'interface du joueur, sobre, à la manière de GTA : rien qui clignote pour rien, rien de
    /// gros au milieu de l'image quand on se promène.
    ///
    /// - Sous la mini-carte (en bas à gauche), deux barres fines : la vie (avec la traînée de ce
    ///   qu'on vient de perdre, et qui bat quand elle est basse) et l'endurance. L'étourdissement
    ///   s'ajoute en un trait jaune quand il monte.
    /// - En haut à droite : l'argent (le gain ou la perte s'affiche dessous un instant), puis le
    ///   niveau et l'heure — selon le mode choisi (complet, discret, masqué).
    /// - Au centre : un point, discret ; en combat seulement, la zone visée, la garde, la charge,
    ///   la parade, la riposte — tout ce qui se joue en un dixième de seconde.
    /// - Au volant : le compteur, en bas à droite.
    ///
    /// Dessiné en IMGUI : aucune police importée, aucun sprite, aucun Canvas.
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
        [Tooltip("Optionnel (monde ouvert) : l'argent et le niveau, en haut a droite.")]
        private PlayerProgress _progress;
        private PlayerInputReader _keys;

        [Header("Affichage")]
        [SerializeField]
        [Tooltip("Optionnel : hors combat, pas de reticule de combat.")]
        private CombatPresence _presence;

        [SerializeField] private bool _visible = true;

        private static readonly Color HealthColor = new Color(0.36f, 0.78f, 0.42f);
        private static readonly Color HealthLow = new Color(0.86f, 0.24f, 0.22f);
        private static readonly Color StaminaColor = new Color(0.33f, 0.64f, 0.95f);
        private static readonly Color Track = new Color(0.02f, 0.025f, 0.03f, 0.62f);
        private static readonly Color ZoneHead = new Color(1f, 0.5f, 0.4f);
        private static readonly Color ZoneBody = new Color(1f, 0.93f, 0.75f);
        private static readonly Color ZoneLeg = new Color(0.6f, 0.85f, 1f);
        private static readonly Color GuardColor = new Color(0.62f, 0.8f, 1f, 0.75f);
        private static readonly Color ParryColor = new Color(1f, 0.94f, 0.6f);

        private float _trail = 1f;
        private float _trailHold;
        private float _flash;
        private float _heal;
        private float _heartPhase;
        private float _money;
        private int _lastMoney = int.MinValue;
        private int _moneyDelta;
        private float _moneyAge = 99f;
        private int _lastLevel = -1;
        private float _levelAge = 99f;
        private float _lastExperienceProgress = -1f;
        private float _vitalsAge;

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
            _trailHold = 0.5f;
            _vitalsAge = 0f;
        }

        private void OnHealed(float amount)
        {
            if (amount > 0.5f) _heal = 1f;
            _vitalsAge = 0f;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _flash = Mathf.MoveTowards(_flash, 0f, dt * 4f);
            _heal = Mathf.MoveTowards(_heal, 0f, dt * 1.5f);
            _vitalsAge += dt;

            if (_playerHealth != null)
            {
                if (_trailHold > 0f) _trailHold -= dt;
                else _trail = Mathf.MoveTowards(_trail, _playerHealth.Normalized, 0.45f * dt);
                if (_trail < _playerHealth.Normalized) _trail = _playerHealth.Normalized;

                // Le cœur bat plus vite à mesure que la vie baisse : 70 puis jusqu'à 150 par minute.
                float bpm = Mathf.Lerp(150f, 70f, Mathf.Clamp01(_playerHealth.Normalized / 0.25f));
                _heartPhase = Mathf.Repeat(_heartPhase + dt * bpm / 60f, 1f);
            }

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
                    _moneyAge = 0f;
                    _lastMoney = _progress.Money;
                }

                _money = Mathf.MoveTowards(_money, _progress.Money, Mathf.Max(30f, Mathf.Abs(_progress.Money - _money) * 3f) * dt);
                _moneyAge += dt;

                if ((_lastLevel >= 0 && _progress.Level != _lastLevel) ||
                    (_lastExperienceProgress >= 0f && Mathf.Abs(_progress.LevelProgress - _lastExperienceProgress) > 0.0001f))
                {
                    _levelAge = 0f;
                }

                _lastLevel = _progress.Level;
                _lastExperienceProgress = _progress.LevelProgress;
                _levelAge += dt;
            }
        }

        private void OnGUI()
        {
            // La cinematique d'avant-combat et l'ecran titre prennent l'ecran : pas d'interface
            // de jeu par-dessus.
            if (FightIntro.AnyPlaying || GameMenu.ShowingTitle || GameMenu.IsOpen) return;

            // Un plan de caméra (le comptoir d'un magasin, le barbier) : l'image est au décor.
            if (View.ShotCamera.Active || FullScreenPanel.AnyOpen) return;
            if (!_visible || Event.current.type != EventType.Repaint) return;

            int mode = GameSettings.Hud;
            bool driving = PlayerDriving.IsDriving;
            float combat = _presence != null ? _presence.Weight : 1f;
            float u = UiTheme.Unit;

            if (!driving)
            {
                GuiKit.Alpha = Mathf.Lerp(0.55f, 1f, combat);
                DrawCrosshair(u, combat);

                GuiKit.Alpha = combat;
                if (combat > 0.01f)
                {
                    DrawAimedZone(u);
                    DrawGuard(u);
                    DrawCharge(u);
                    DrawRiposte(u);
                }
            }

            GuiKit.Alpha = 1f;
            if (mode < 2)
            {
                DrawVitals(u, combat, mode);
                DrawWallet(u, mode);
                if (driving) DrawSpeedometer(u);
            }

            GuiKit.Alpha = 1f;
            DrawOutdatedWarning();
        }

        // ------------------------------------------------------------------ vie et endurance

        /// <summary>Les deux barres, sous la mini-carte (ou à sa place si elle est masquée).</summary>
        private void DrawVitals(float u, float combat, int mode)
        {
            if (_playerHealth == null) return;

            float health = _playerHealth.Normalized;
            float stamina = _playerStamina != null ? _playerStamina.Normalized : 1f;

            // Discret : les barres s'effacent quand tout va bien et qu'on ne se bat pas.
            if (mode == 1)
            {
                bool matters = combat > 0.05f || health < 0.999f || stamina < 0.98f || _vitalsAge < 4f;
                GuiKit.Alpha = matters ? 1f : 0.35f;
            }

            Rect map = UiTheme.MinimapRect();
            float y = map.yMax + 6f * u;
            float h = 7f * u;
            float gap = 6f * u;
            Rect healthRect = new Rect(map.x, y, map.width * 0.64f - gap * 0.5f, h);
            Rect staminaRect = new Rect(healthRect.xMax + gap, y, map.width - healthRect.width - gap, h);

            bool low = health <= 0.25f && _playerHealth.IsAlive;
            float heart = low ? Heartbeat(_heartPhase) : 0f;
            Color fill = low ? Color.Lerp(HealthLow, Color.white, heart * 0.25f) : Color.Lerp(HealthLow, HealthColor, Mathf.InverseLerp(0.25f, 0.6f, health));

            Bar(healthRect, health, _trail, fill, u);
            if (_heal > 0.01f) GuiKit.Fill(new Rect(healthRect.x, healthRect.y, healthRect.width * health, healthRect.height), new Color(1f, 1f, 1f, _heal * 0.3f));
            if (_flash > 0.01f) GuiKit.Fill(healthRect, new Color(1f, 0.3f, 0.25f, _flash * 0.35f * GameSettings.FlashScale));
            if (low) GuiKit.RoundedOutline(new Rect(healthRect.x - 2f, healthRect.y - 2f, healthRect.width + 4f, healthRect.height + 4f),
                new Color(1f, 0.25f, 0.2f, 0.25f + heart * 0.5f * GameSettings.FlashScale), 3f * u, 1.5f);

            bool empty = _playerStamina != null && (_playerStamina.IsEmpty || _playerStamina.IsExhausted);
            float blink = empty ? 0.55f + 0.45f * Mathf.Sin(Time.unscaledTime * 8f) : 1f;
            Color staminaFill = empty ? new Color(1f, 0.45f, 0.3f, blink) : StaminaColor;
            Bar(staminaRect, stamina, stamina, staminaFill, u);

            // L'étourdissement : un trait jaune au-dessus de la vie quand il monte.
            if (_stun != null && _stun.Normalized > 0.01f)
            {
                float value = _stun.Normalized;
                float pulse = value > 0.75f ? 0.6f + 0.4f * Mathf.Sin(Time.unscaledTime * 12f) : 1f;
                GuiKit.Fill(new Rect(healthRect.x, healthRect.y - 4f * u, healthRect.width * value, 2f * u), new Color(1f, 0.85f, 0.35f, pulse));
            }

            // Les mots, seulement quand ils servent.
            GUIStyle tag = UiTheme.Text(11.5f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            if (low) UiTheme.Label(new Rect(healthRect.x, healthRect.yMax + 2f * u, 200f * u, 14f * u), "BLESSÉ", tag, new Color(1f, 0.5f, 0.45f, 0.7f + heart * 0.3f));
            if (empty) UiTheme.Label(new Rect(staminaRect.x, staminaRect.yMax + 2f * u, 200f * u, 14f * u), "À BOUT DE SOUFFLE", tag, new Color(1f, 0.6f, 0.45f, blink));
            if (_stun != null && _stun.Normalized > 0.75f)
            {
                UiTheme.Label(new Rect(healthRect.x, healthRect.y - 20f * u, 200f * u, 14f * u), "SONNÉ", tag, new Color(1f, 0.88f, 0.5f));
            }

            GuiKit.Alpha = 1f;
        }

        private static void Bar(Rect rect, float value, float trail, Color color, float u)
        {
            value = Mathf.Clamp01(value);
            trail = Mathf.Clamp01(trail);
            GuiKit.Fill(new Rect(rect.x - 1f, rect.y - 1f, rect.width + 2f, rect.height + 2f), new Color(0f, 0f, 0f, 0.55f));
            GuiKit.Fill(rect, Track);
            if (trail > value) GuiKit.Fill(new Rect(rect.x + rect.width * value, rect.y, rect.width * (trail - value), rect.height), new Color(1f, 0.92f, 0.85f, 0.75f));
            GuiKit.Fill(new Rect(rect.x, rect.y, rect.width * value, rect.height), color);
            GuiKit.Fill(new Rect(rect.x, rect.y, rect.width * value, Mathf.Max(1f, rect.height * 0.35f)), new Color(1f, 1f, 1f, 0.18f));
        }

        /// <summary>Le double battement « poum-poum » d'un cœur, de 0 à 1.</summary>
        private static float Heartbeat(float phase)
        {
            float a = Mathf.Exp(-Mathf.Pow((phase - 0.08f) * 18f, 2f));
            float b = Mathf.Exp(-Mathf.Pow((phase - 0.26f) * 18f, 2f)) * 0.7f;
            return Mathf.Clamp01(a + b);
        }

        // ------------------------------------------------------------------ monde ouvert

        /// <summary>En haut à droite : l'argent, le gain qui passe dessous, le niveau, l'heure.</summary>
        private void DrawWallet(float u, int mode)
        {
            if (_progress == null) return;

            float right = Screen.width - 32f * u;
            float y = UiTheme.TopRight(0f);
            bool complete = mode == 0;
            bool moneyFresh = _moneyAge < 4f;

            // Discret : l'argent ne s'affiche que lorsqu'il bouge.
            float moneyAlpha = complete ? 1f : Mathf.Clamp01(1f - (_moneyAge - 3.5f) / 0.6f);
            if (moneyAlpha > 0.01f)
            {
                GuiKit.Alpha = moneyAlpha;
                GUIStyle big = UiTheme.Text(27f, GuiKit.Weight.Bold, TextAnchor.UpperRight);
                GuiKit.ShadowLabel(new Rect(right - 400f * u, y, 400f * u, 36f * u), FormatMoney(Mathf.RoundToInt(_money)) + " €", big,
                    new Color(0.93f, 0.97f, 0.93f), 0.65f);

                if (moneyFresh && _moneyDelta != 0)
                {
                    float a = 1f - Mathf.Clamp01((_moneyAge - 2.6f) / 0.8f);
                    Color c = _moneyDelta > 0 ? new Color(0.5f, 0.95f, 0.55f, a) : new Color(1f, 0.45f, 0.4f, a);
                    GuiKit.ShadowLabel(new Rect(right - 400f * u, y + 34f * u, 400f * u, 24f * u),
                        (_moneyDelta > 0 ? "+" : "−") + FormatMoney(Mathf.Abs(_moneyDelta)) + " €",
                        UiTheme.Text(17f, GuiKit.Weight.Bold, TextAnchor.UpperRight), c, 0.6f);
                }

                GuiKit.Alpha = 1f;
            }

            // Le niveau et l'heure : en mode complet, ou un instant quand l'expérience bouge.
            float levelAlpha = complete ? 1f : Mathf.Clamp01(1f - (_levelAge - 4f) / 0.6f);
            if (levelAlpha <= 0.01f) return;
            GuiKit.Alpha = levelAlpha;

            float line = y + (moneyFresh && _moneyDelta != 0 ? 62f : 40f) * u;
            float barW = 150f * u;
            Rect xp = new Rect(right - barW, line + 18f * u, barW, 3f * u);
            GuiKit.Fill(xp, new Color(0f, 0f, 0f, 0.45f));
            GuiKit.Fill(new Rect(xp.x, xp.y, xp.width * Mathf.Clamp01(_progress.LevelProgress), xp.height), UiTheme.Accent);

            string clock = complete && WorldClock.Instance != null ? WorldClock.Instance.Label + "   ·   " : "";
            GuiKit.ShadowLabel(new Rect(right - 300f * u, line, 300f * u, 16f * u), clock + "Niveau " + _progress.Level,
                UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleRight), UiTheme.Ink, 0.6f);
            GuiKit.Alpha = 1f;
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

        /// <summary>Au volant : la vitesse en gros, le rapport, le régime en un trait fin.</summary>
        private void DrawSpeedometer(float u)
        {
            PlayerDriving driving = PlayerDriving.Instance;
            DrivableCar car = driving != null ? driving.Vehicle : null;
            if (car == null) return;

            float right = Screen.width - 34f * u;
            float bottom = Screen.height - 34f * u;
            int kmh = Mathf.RoundToInt(car.SpeedKmh);

            GUIStyle speed = UiTheme.Text(46f, GuiKit.Weight.Bold, TextAnchor.LowerRight);
            GuiKit.ShadowLabel(new Rect(right - 260f * u, bottom - 70f * u, 200f * u, 58f * u), kmh.ToString(), speed, UiTheme.Ink, 0.6f);
            GuiKit.ShadowLabel(new Rect(right - 54f * u, bottom - 34f * u, 54f * u, 18f * u), "km/h",
                UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft), UiTheme.InkDim, 0.6f);

            string gear = car.Gear == 0 ? "R" : Mathf.Abs(car.ForwardSpeed) < 0.3f ? "N" : car.Gear.ToString();
            Rect gearBox = new Rect(right - 46f * u, bottom - 66f * u, 30f * u, 28f * u);
            GuiKit.Rounded(gearBox, new Color(0.03f, 0.035f, 0.045f, 0.75f), 5f * u);
            UiTheme.Label(gearBox, gear, UiTheme.Text(16f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter), UiTheme.Accent);

            float rpm = Mathf.Clamp01(car.Rpm);
            Rect rail = new Rect(right - 260f * u, bottom - 8f * u, 260f * u, 3f * u);
            GuiKit.Fill(rail, new Color(0f, 0f, 0f, 0.45f));
            GuiKit.Fill(new Rect(rail.x, rail.y, rail.width * rpm, rail.height), rpm > 0.86f ? UiTheme.Bad : UiTheme.Ink);

            GuiKit.ShadowLabel(new Rect(right - 300f * u, bottom - 96f * u, 300f * u, 18f * u), car.DisplayName,
                UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleRight), UiTheme.InkDim, 0.6f);

            string hint = driving.Hint;
            float x = Screen.width * 0.5f - 230f * u;
            if (!string.IsNullOrEmpty(hint))
            {
                GuiKit.ShadowLabel(new Rect(0f, Screen.height - 64f * u, Screen.width, 20f * u), hint,
                    UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleCenter), UiTheme.InkDim, 0.6f);
            }

            if (!GameSettings.KeyHints) return;
            if (_keys == null) _keys = FindAnyObjectByType<PlayerInputReader>();
            Core.InputBindings b = _keys != null ? _keys.Bindings : null;
            float hx = UiTheme.KeyHint(x, Screen.height - 40f * u, b != null ? UiTheme.KeyName(b.jump) : "Espace", "Frein à main", u);
            UiTheme.KeyHint(hx, Screen.height - 40f * u, b != null ? UiTheme.KeyName(b.attackStraight) : "Clic", "Klaxon", u);
        }

        // ------------------------------------------------------------------ centre de l'écran

        private void DrawCrosshair(float u, float combat)
        {
            int style = GameSettings.Crosshair;
            if (style == 2) return;

            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            Color ink = new Color(1f, 1f, 1f, 0.85f);
            Color edge = new Color(0f, 0f, 0f, 0.5f);

            if (style == 0)
            {
                float d = Mathf.Lerp(4f, 5f, combat) * u;
                GuiKit.Rounded(new Rect(cx - d * 0.5f - 1f, cy - d * 0.5f - 1f, d + 2f, d + 2f), edge, d);
                GuiKit.Rounded(new Rect(cx - d * 0.5f, cy - d * 0.5f, d, d), ink, d);
                return;
            }

            float t = 2f * u, gap = 6f * u, length = 7f * u;
            GuiKit.Fill(new Rect(cx - gap - length, cy - t * 0.5f, length, t), ink);
            GuiKit.Fill(new Rect(cx + gap, cy - t * 0.5f, length, t), ink);
            GuiKit.Fill(new Rect(cx - t * 0.5f, cy - gap - length, t, length), ink);
            GuiKit.Fill(new Rect(cx - t * 0.5f, cy + gap, t, length), ink);
        }

        /// <summary>
        /// Arc de charge sous le réticule. Une charge sans retour visuel est injouable : le
        /// trait se remplit, puis devient blanc et pulse à charge pleine — « lâche maintenant ».
        /// </summary>
        private void DrawCharge(float u)
        {
            if (_executor == null || !_executor.IsCharging) return;

            float level = _executor.ChargeProgress;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            bool full = level >= 0.999f;
            float pulse = full ? 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 14f) : 1f;
            Color color = full ? new Color(1f, 1f, 1f, pulse) : Color.Lerp(new Color(1f, 0.74f, 0.29f, 0.9f), new Color(1f, 0.4f, 0.25f, 0.95f), level);

            Rect rail = new Rect(cx - 40f * u, cy + 34f * u, 80f * u, 3f * u);
            GuiKit.Fill(rail, new Color(0f, 0f, 0f, 0.55f));
            GuiKit.Fill(new Rect(rail.x, rail.y, rail.width * level, rail.height), color);
            if (full)
            {
                GuiKit.ShadowLabel(new Rect(cx - 100f * u, cy + 40f * u, 200f * u, 18f * u), "Charge pleine",
                    UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter), new Color(1f, 1f, 1f, pulse), 0.7f);
            }
        }

        /// <summary>Fenêtre de riposte, juste après une parade réussie : moins d'une seconde.</summary>
        private void DrawRiposte(float u)
        {
            if (_guard == null || !_guard.RiposteReady) return;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            float pulse = 0.7f + 0.3f * Mathf.Sin(Time.unscaledTime * 16f);
            GuiKit.ShadowLabel(new Rect(cx - 150f * u, cy - 96f * u, 300f * u, 26f * u),
                "Riposte  ×" + _guard.RiposteDamageMultiplier.ToString("0.0"),
                UiTheme.Text(19f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter), new Color(1f, 0.92f, 0.6f, pulse), 0.7f);
        }

        /// <summary>
        /// Bandeau d'alerte quand les données de coups sont périmées : à l'écran, pas seulement
        /// dans la console — le symptôme est « rien n'a changé », indiscernable d'un oubli.
        /// </summary>
        private void DrawOutdatedWarning()
        {
            if (_combat == null || _combat.OutdatedAttacks <= 0) return;

            Rect banner = new Rect(0f, 0f, Screen.width, 34f);
            float pulse = 0.75f + 0.25f * Mathf.Sin(Time.unscaledTime * 4f);
            GuiKit.Fill(banner, new Color(0.55f, 0.08f, 0.06f, 0.92f * pulse));
            GuiKit.OutlinedLabel(banner,
                _combat.OutdatedAttacks + " coup(s) perimes  —  lance  Uber Bagarre > 2 - Construire la scene",
                GuiKit.Style(15, FontStyle.Bold, TextAnchor.MiddleCenter), Color.white, new Color(0f, 0f, 0f, 0.9f), 2f);
        }

        /// <summary>
        /// La zone visée, sous le réticule : c'est ce qui rend les zones jouables (on sait, AVANT
        /// de frapper, ce qu'on va toucher). Le même résolveur sert ici et au combat.
        /// </summary>
        private void DrawAimedZone(float u)
        {
            if (!GameSettings.AimedZone || _player == null) return;
            Hurtbox zone = AimResolver.Resolve(_player);
            if (zone == null) return;

            Color color = zone.Zone == HitZone.Head ? ZoneHead : zone.Zone == HitZone.Leg ? ZoneLeg : ZoneBody;
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;
            GuiKit.ShadowLabel(new Rect(cx - 100f * u, cy + 16f * u, 200f * u, 16f * u),
                ZoneName(zone.Zone) + "  ×" + zone.DamageMultiplier.ToString("0.0"),
                UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter), new Color(color.r, color.g, color.b, 0.9f), 0.7f);
        }

        private static string ZoneName(HitZone zone)
        {
            switch (zone)
            {
                case HitZone.Head: return "Tête";
                case HitZone.Leg: return "Jambes";
                case HitZone.Arm: return "Bras";
                default: return "Corps";
            }
        }

        /// <summary>
        /// La garde, autour du réticule : quatre coins (couvert), qui se resserrent pendant la
        /// fenêtre de parade ; l'éclat dit ce qui vient de se passer. Au centre de l'écran, parce
        /// qu'une parade se joue en deux dixièmes de seconde.
        /// </summary>
        private void DrawGuard(float u)
        {
            float cx = Screen.width * 0.5f;
            float cy = Screen.height * 0.5f;

            if (_guard != null && _guard.IsGuarding)
            {
                bool parry = _guard.InParryWindow;
                float r = (parry ? 26f : 32f) * u;
                float t = (parry ? 3f : 2f) * u;
                float l = 10f * u;
                Color c = parry ? ParryColor : GuardColor;
                GuiKit.Fill(new Rect(cx - r, cy - r, l, t), c);
                GuiKit.Fill(new Rect(cx - r, cy - r, t, l), c);
                GuiKit.Fill(new Rect(cx + r - l, cy - r, l, t), c);
                GuiKit.Fill(new Rect(cx + r - t, cy - r, t, l), c);
                GuiKit.Fill(new Rect(cx - r, cy + r - t, l, t), c);
                GuiKit.Fill(new Rect(cx - r, cy + r - l, t, l), c);
                GuiKit.Fill(new Rect(cx + r - l, cy + r - t, l, t), c);
                GuiKit.Fill(new Rect(cx + r - t, cy + r - l, t, l), c);
            }

            if (_relay == null) return;
            float flashes = GameSettings.FlashScale;

            if (_relay.BlockFlash > 0.01f)
            {
                float size = Mathf.Lerp(110f, 60f, _relay.BlockFlash) * u;
                GuiKit.Disc(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), new Color(0.6f, 0.8f, 1f, _relay.BlockFlash * 0.25f * flashes));
            }

            if (_relay.ParryFlash > 0.01f)
            {
                float size = Mathf.Lerp(220f, 80f, _relay.ParryFlash) * u;
                GuiKit.Disc(new Rect(cx - size * 0.5f, cy - size * 0.5f, size, size), new Color(ParryColor.r, ParryColor.g, ParryColor.b, _relay.ParryFlash * 0.45f * flashes));
                GuiKit.ShadowLabel(new Rect(cx - 160f * u, cy - 86f * u, 320f * u, 30f * u), "Parade",
                    UiTheme.Text(20f + _relay.ParryFlash * 6f, GuiKit.Weight.Black, TextAnchor.MiddleCenter),
                    new Color(1f, 0.97f, 0.82f, Mathf.Clamp01(_relay.ParryFlash * 1.4f)), 0.7f);
            }
        }
    }
}
