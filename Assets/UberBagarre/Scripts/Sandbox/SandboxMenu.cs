using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Player;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Menu de réglage en jeu (Tab) : faire apparaître des adversaires, choisir leurs PV et leurs
    /// dégâts, et les siens.
    ///
    /// Pourquoi ça vaut plus que ça en a l'air : équilibrer un combat en modifiant des valeurs
    /// dans l'Inspector demande de sortir du mode Play, donc de perdre la situation qu'on voulait
    /// justement tester. On règle alors à l'aveugle, une valeur à la fois, dix secondes de
    /// rechargement à chaque essai. Ici, tout se règle PENDANT le combat, et l'effet se voit au
    /// coup suivant.
    ///
    /// Les adversaires sont des COPIES de celui de la scène. Aucun prefab à maintenir, donc aucun
    /// risque que le prefab et la scène divergent : ce qu'on fait apparaître est, par
    /// construction, exactement l'adversaire qu'on vient d'affronter.
    ///
    /// Les valeurs passent par <see cref="CombatantStats.SetOverride"/>, donc par le même chemin
    /// que n'importe quelle amélioration future — le menu ne contourne pas le système de
    /// statistiques, il s'en sert.
    /// </summary>
    public class SandboxMenu : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Combatant _player;

        [SerializeField]
        [Tooltip("L'adversaire de la scene. Il sert de modele : les adversaires ajoutes sont ses copies.")]
        private GameObject _enemyTemplate;

        [SerializeField]
        [Tooltip("Libere le curseur quand le menu est ouvert. Sans lui on ne peut pas cliquer.")]
        private CursorLockController _cursor;

        [SerializeField] private SpawnDirector _spawnDirector;

        [SerializeField]
        [Tooltip("Optionnel. Permet de regler la nervosite du combat en jouant.")]
        private PlayerCombat _playerCombat;

        [SerializeField] private UberBagarre.Feedback.HitStop _hitStop;

        [Header("Apparition")]
        [SerializeField, Min(1f)]
        [Tooltip("Distance a laquelle les adversaires apparaissent autour du joueur.")]
        private float _spawnDistance = 4.5f;

        [SerializeField, Min(1)] private int _maxEnemies = 12;

        [Header("Bornes des reglages")]
        [SerializeField] private Vector2 _healthRange = new Vector2(10f, 500f);
        [SerializeField] private Vector2 _damageMultiplierRange = new Vector2(0.1f, 5f);
        [SerializeField] private Vector2 _attackSpeedRange = new Vector2(0.5f, 2.5f);
        [SerializeField] private Vector2 _hitStopRange = new Vector2(0.05f, 1f);
        [SerializeField] private Vector2 _inputBufferRange = new Vector2(0f, 0.4f);

        [Header("Apparence")]
        [SerializeField] private Color _panelColor = new Color(0.06f, 0.07f, 0.10f, 0.96f);
        [SerializeField] private Color _accent = new Color(0.98f, 0.80f, 0.30f);
        [SerializeField] private Color _playerAccent = new Color(0.42f, 0.82f, 1f);
        [SerializeField] private Color _enemyAccent = new Color(1f, 0.46f, 0.38f);

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private bool _open;
        private float _playerHealth = 100f;
        private float _playerDamage = 1f;
        private float _enemyHealth = 90f;
        private float _enemyDamage = 1f;
        private float _attackSpeed = 1f;
        private float _hitStopStrength = 0.25f;
        private float _inputBufferSeconds = 0.22f;
        private bool _initialised;

        public bool IsOpen { get { return _open; } }

        private void Start()
        {
            ReadCurrentValues();
            _initialised = true;
        }

        /// <summary>
        /// Les curseurs partent des valeurs réelles de la scène, pas de valeurs écrites en dur.
        ///
        /// Sinon ouvrir le menu appliquerait silencieusement des réglages que personne n'a
        /// demandés — et on chercherait ensuite pourquoi l'équilibrage a changé tout seul.
        /// </summary>
        private void ReadCurrentValues()
        {
            if (_player != null && _player.Health != null) _playerHealth = _player.Health.MaxHealth;
            if (_player != null && _player.Stats != null) _playerDamage = ToMultiplier(_player.Stats.Get(StatType.Strength));

            if (_player != null && _player.Stats != null) _attackSpeed = _player.Stats.Get(StatType.AttackSpeed);
            if (_hitStop != null) _hitStopStrength = _hitStop.SlowTimeScale;
            if (_playerCombat != null) _inputBufferSeconds = _playerCombat.InputBuffer;

            Combatant enemy = FirstEnemy();
            if (enemy == null) return;

            if (enemy.Health != null) _enemyHealth = enemy.Health.MaxHealth;
            if (enemy.Stats != null) _enemyDamage = ToMultiplier(enemy.Stats.Get(StatType.Strength));
        }

        private void Update()
        {
            if (_input == null) return;
            if (_input.ToggleSandboxMenuPressed) SetOpen(!_open);
        }

        public void SetOpen(bool open)
        {
            _open = open;

            if (_cursor == null) return;

            // Le controleur de curseur est SUSPENDU pendant le menu, pas seulement deverrouille :
            // il recapture le curseur au premier clic, ce qui rendrait tout bouton inutilisable
            // (on cliquerait, le curseur se reverrouillerait, et le clic serait perdu).
            _cursor.enabled = !open;
            _cursor.SetLocked(!open);
        }

        // ------------------------------------------------------------------ application

        private static float ToMultiplier(float strength)
        {
            return 1f + strength * 0.01f;
        }

        private static float ToStrength(float multiplier)
        {
            return (multiplier - 1f) * 100f;
        }

        private void ApplyPlayer()
        {
            if (_player == null) return;

            if (_player.Stats != null)
            {
                _player.Stats.SetOverride(StatType.MaxHealth, _playerHealth);
                _player.Stats.SetOverride(StatType.Strength, ToStrength(_playerDamage));
            }

            // ApplyStats reporte MaxHealth sur la vie. On ne remplit PAS a bloc : changer son
            // maximum en plein combat ne doit pas etre un soin gratuit.
            if (_player.Health != null) _player.Health.SetMaxHealth(_playerHealth, false);
            _player.ApplyStats();
        }

        /// <summary>
        /// Applique les réglages de NERVOSITÉ.
        ///
        /// Ils sont dans ce menu pour une raison simple : « ce n'est pas assez nerveux » n'est pas
        /// corrigeable à distance. Cinq choses interviennent en même temps — la durée des coups, le
        /// ralenti d'impact, le tampon d'entrée, l'endurance et la vitesse de déplacement — et rien
        /// ne dit laquelle domine pour un joueur donné. Les régler en jouant prend trois minutes ;
        /// les deviner prend un aller-retour de test par essai.
        ///
        /// « Vitesse des coups » passe par StatType.AttackSpeed, donc par le système de stats, et
        /// s'appliquera aussi bien a une future amélioration de personnage.
        /// </summary>
        private void ApplyFeel()
        {
            if (_player != null && _player.Stats != null)
            {
                _player.Stats.SetOverride(StatType.AttackSpeed, _attackSpeed);
            }

            if (_hitStop != null) _hitStop.SlowTimeScale = _hitStopStrength;
            if (_playerCombat != null) _playerCombat.InputBuffer = _inputBufferSeconds;
        }

        private void ApplyEnemies()
        {
            IReadOnlyList<Combatant> all = Combatant.All;

            for (int i = 0; i < all.Count; i++)
            {
                Combatant combatant = all[i];
                if (combatant == null || combatant.Faction != Faction.Enemy) continue;

                ApplyEnemyStats(combatant);
            }

            // Le modele aussi, meme s'il est desactive : sinon les prochaines copies repartiraient
            // des anciennes valeurs.
            if (_enemyTemplate == null) return;

            Combatant template = _enemyTemplate.GetComponent<Combatant>();
            if (template != null) ApplyEnemyStats(template);
        }

        private void ApplyEnemyStats(Combatant combatant)
        {
            if (combatant.Stats != null)
            {
                combatant.Stats.SetOverride(StatType.MaxHealth, _enemyHealth);
                combatant.Stats.SetOverride(StatType.Strength, ToStrength(_enemyDamage));
            }

            if (combatant.Health != null) combatant.Health.SetMaxHealth(_enemyHealth, false);
            combatant.ApplyStats();
        }

        // ------------------------------------------------------------------ apparition

        private Combatant FirstEnemy()
        {
            IReadOnlyList<Combatant> all = Combatant.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].Faction == Faction.Enemy) return all[i];
            }

            return null;
        }

        private int LiveEnemies()
        {
            IReadOnlyList<Combatant> all = Combatant.All;
            int count = 0;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].Faction == Faction.Enemy && all[i].IsAlive) count++;
            }

            return count;
        }

        /// <summary>Fait apparaître une copie de l'adversaire, autour du joueur.</summary>
        public void SpawnEnemy()
        {
            if (_enemyTemplate == null)
            {
                Debug.LogWarning("[UberBagarre] SandboxMenu : aucun modele d'adversaire assigne, " +
                                 "impossible d'en faire apparaitre.", this);
                return;
            }

            if (_spawned.Count >= _maxEnemies)
            {
                Debug.LogWarning("[UberBagarre] SandboxMenu : limite de " + _maxEnemies +
                                 " adversaires ajoutes atteinte.", this);
                return;
            }

            Vector3 centre = _player != null ? _player.transform.position : transform.position;

            // Repartis en cercle, et non tous au meme endroit : deux adversaires qui apparaissent
            // dans la meme capsule se repoussent violemment des la premiere image.
            float angle = _spawned.Count * 137.5f * Mathf.Deg2Rad;
            Vector3 offset = new Vector3(Mathf.Sin(angle), 0f, Mathf.Cos(angle)) * _spawnDistance;

            GameObject clone = Instantiate(_enemyTemplate, centre + offset, Quaternion.identity);
            clone.name = "Ennemi (ajoute " + (_spawned.Count + 1) + ")";
            clone.transform.rotation = Quaternion.LookRotation(-offset.normalized, Vector3.up);

            _spawned.Add(clone);

            Combatant combatant = clone.GetComponent<Combatant>();
            if (combatant != null) ApplyEnemyStats(combatant);
        }

        public void RemoveSpawned()
        {
            for (int i = 0; i < _spawned.Count; i++)
            {
                if (_spawned[i] != null) Destroy(_spawned[i]);
            }

            _spawned.Clear();
        }

        /// <summary>Remet tout le monde à neuf sans repasser par le mode Play.</summary>
        public void ReviveEverybody()
        {
            IReadOnlyList<Combatant> all = Combatant.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null) all[i].Revive();
            }

            if (_spawnDirector != null) _spawnDirector.SpawnAll();
        }

        // ------------------------------------------------------------------ interface

        private void OnGUI()
        {
            if (!_open || !_initialised) return;

            float width = Mathf.Min(790f, Screen.width - 40f);
            float height = Mathf.Min(580f, Screen.height - 40f);

            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GuiKit.Fill(new Rect(panel.x + 6f, panel.y + 7f, panel.width, panel.height), new Color(0f, 0f, 0f, 0.45f));
            GuiKit.Fill(panel, _panelColor);
            GuiKit.Outline(panel, 3f, _accent);

            GuiKit.Fill(new Rect(panel.x, panel.y, panel.width, 34f), new Color(1f, 1f, 1f, 0.07f));

            GuiKit.OutlinedLabel(new Rect(panel.x + 16f, panel.y + 4f, panel.width - 32f, 26f),
                "BAC A SABLE", GuiKit.Style(17, FontStyle.Bold, TextAnchor.MiddleLeft),
                _accent, new Color(0f, 0f, 0f, 0.9f), 1.5f);

            GuiKit.OutlinedLabel(new Rect(panel.x + 16f, panel.y + 4f, panel.width - 32f, 26f),
                "TAB pour fermer", GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleRight),
                new Color(1f, 1f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.8f), 1f);

            float y = panel.y + 46f;
            float left = panel.x + 18f;

            // Deux colonnes : les reglages a gauche, les commandes a droite. Les commandes vivent
            // ici et pas dans le HUD parce qu'elles ne servent qu'une fois — un rappel permanent a
            // l'ecran est du bruit des qu'on les connait, mais les chercher dans un README en
            // pleine partie est pire.
            float helpWidth = panel.width > 600f ? 300f : 0f;
            float innerWidth = panel.width - 36f - (helpWidth > 0f ? helpWidth + 18f : 0f);

            if (helpWidth > 0f) DrawHelp(new Rect(panel.xMax - helpWidth - 18f, y, helpWidth, height - 64f));

            y = Section(left, y, innerWidth, "TOI", _playerAccent);

            float newPlayerHealth = Row(left, ref y, innerWidth, "Points de vie",
                _playerHealth, _healthRange, "0", _playerAccent);

            float newPlayerDamage = Row(left, ref y, innerWidth, "Degats infliges",
                _playerDamage, _damageMultiplierRange, "x0.00", _playerAccent);

            if (!Mathf.Approximately(newPlayerHealth, _playerHealth) ||
                !Mathf.Approximately(newPlayerDamage, _playerDamage))
            {
                _playerHealth = newPlayerHealth;
                _playerDamage = newPlayerDamage;
                ApplyPlayer();
            }

            y += 10f;
            y = Section(left, y, innerWidth, "ADVERSAIRES", _enemyAccent);

            float newEnemyHealth = Row(left, ref y, innerWidth, "Points de vie",
                _enemyHealth, _healthRange, "0", _enemyAccent);

            float newEnemyDamage = Row(left, ref y, innerWidth, "Degats infliges",
                _enemyDamage, _damageMultiplierRange, "x0.00", _enemyAccent);

            if (!Mathf.Approximately(newEnemyHealth, _enemyHealth) ||
                !Mathf.Approximately(newEnemyDamage, _enemyDamage))
            {
                _enemyHealth = newEnemyHealth;
                _enemyDamage = newEnemyDamage;
                ApplyEnemies();
            }

            y += 10f;
            y = Section(left, y, innerWidth, "NERVOSITE", _accent);

            float newSpeed = Row(left, ref y, innerWidth, "Vitesse des coups",
                _attackSpeed, _attackSpeedRange, "x0.00", _accent);

            float newHitStop = Row(left, ref y, innerWidth, "Ralenti d'impact",
                _hitStopStrength, _hitStopRange, "0.00", _accent);

            float newBuffer = Row(left, ref y, innerWidth, "Tampon de touche",
                _inputBufferSeconds, _inputBufferRange, "0.00 s", _accent);

            if (!Mathf.Approximately(newSpeed, _attackSpeed))
            {
                _attackSpeed = newSpeed;
                ApplyFeel();
            }

            if (!Mathf.Approximately(newHitStop, _hitStopStrength))
            {
                _hitStopStrength = newHitStop;
                ApplyFeel();
            }

            if (!Mathf.Approximately(newBuffer, _inputBufferSeconds))
            {
                _inputBufferSeconds = newBuffer;
                ApplyFeel();
            }

            y += 12f;

            GUIStyle info = GuiKit.Style(12, FontStyle.Normal, TextAnchor.MiddleLeft);
            GuiKit.OutlinedLabel(new Rect(left, y, innerWidth, 18f),
                "Sur le terrain : " + LiveEnemies() + " debout,  " + _spawned.Count + " ajoutes",
                info, new Color(1f, 1f, 1f, 0.65f), new Color(0f, 0f, 0f, 0.8f), 1f);

            y += 24f;

            float half = (innerWidth - 10f) * 0.5f;

            if (Button(new Rect(left, y, half, 32f), "+ UN ADVERSAIRE", _enemyAccent)) SpawnEnemy();
            if (Button(new Rect(left + half + 10f, y, half, 32f), "RETIRER LES AJOUTS", _accent)) RemoveSpawned();

            y += 40f;

            if (Button(new Rect(left, y, half, 32f), "TOUT REMETTRE A NEUF", _playerAccent)) ReviveEverybody();
            if (Button(new Rect(left + half + 10f, y, half, 32f), "VALEURS D'ORIGINE", _accent)) ResetToDefaults();

            y += 46f;

            GUIStyle hint = GuiKit.Style(11, FontStyle.Normal, TextAnchor.UpperLeft);
            GuiKit.OutlinedLabel(new Rect(left, y, innerWidth, 58f),
                "Les reglages s'appliquent immediatement, y compris aux adversaires deja\n" +
                "sur le terrain. Changer un maximum de vie ne soigne pas : c'est un\n" +
                "reglage, pas un bonus.",
                hint, new Color(1f, 1f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.75f), 1f);
        }

        /// <summary>Rappel des commandes. Utile une fois, mais cette fois-là, indispensable.</summary>
        private void DrawHelp(Rect rect)
        {
            GuiKit.Fill(rect, new Color(0f, 0f, 0f, 0.30f));
            GuiKit.Outline(rect, 1f, new Color(1f, 1f, 1f, 0.12f));

            GuiKit.OutlinedLabel(new Rect(rect.x + 12f, rect.y + 6f, rect.width - 24f, 20f),
                "COMMANDES", GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleLeft),
                _accent, new Color(0f, 0f, 0f, 0.85f), 1f);

            string[,] rows =
            {
                { "WASD / ZQSD", "Se deplacer" },
                { "Maj", "Courir  (13 end./s)" },
                { "C", "S'accroupir" },
                { "C en courant", "Glissade  (18 end.)" },
                { "Espace", "Sauter" },
                { "", "" },
                { "Clic gauche", "Direct" },
                { "Clic droit", "Crochet" },
                { "Clic molette", "Uppercut" },
                { "F", "Coup de pied de face" },
                { "V", "Coup de pied bas" },
                { "", "" },
                { "Ctrl maintenu", "Garde  (-72 %)" },
                { "Ctrl au bon moment", "PARADE  (0,26 s)" },
                { "Alt", "Esquive" },
                { "", "" },
                { "F1", "Overlay de diagnostic" },
                { "R", "Relancer le combat" },
                { "Echap", "Liberer le curseur" }
            };

            GUIStyle key = GuiKit.Style(11, FontStyle.Bold, TextAnchor.MiddleLeft);
            GUIStyle action = GuiKit.Style(11, FontStyle.Normal, TextAnchor.MiddleLeft);

            float y = rect.y + 28f;

            for (int i = 0; i < rows.GetLength(0); i++)
            {
                if (string.IsNullOrEmpty(rows[i, 0]))
                {
                    y += 7f;
                    continue;
                }

                GuiKit.OutlinedLabel(new Rect(rect.x + 12f, y, 122f, 16f), rows[i, 0], key,
                    new Color(1f, 1f, 1f, 0.85f), new Color(0f, 0f, 0f, 0.8f), 1f);

                GuiKit.OutlinedLabel(new Rect(rect.x + 138f, y, rect.width - 150f, 16f), rows[i, 1], action,
                    new Color(1f, 1f, 1f, 0.58f), new Color(0f, 0f, 0f, 0.8f), 1f);

                y += 16f;
            }

            GuiKit.OutlinedLabel(new Rect(rect.x + 12f, rect.yMax - 44f, rect.width - 24f, 38f),
                "Viser decide la ZONE touchee. Le reticule\nannonce laquelle, avant de frapper.",
                GuiKit.Style(11, FontStyle.Italic, TextAnchor.UpperLeft),
                new Color(_accent.r, _accent.g, _accent.b, 0.75f), new Color(0f, 0f, 0f, 0.8f), 1f);
        }

        private void ResetToDefaults()
        {
            IReadOnlyList<Combatant> all = Combatant.All;

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] != null && all[i].Stats != null) all[i].Stats.ClearAllOverrides();
            }

            if (_enemyTemplate != null)
            {
                Combatant template = _enemyTemplate.GetComponent<Combatant>();
                if (template != null && template.Stats != null) template.Stats.ClearAllOverrides();
            }

            for (int i = 0; i < all.Count; i++)
            {
                if (all[i] == null) continue;

                if (all[i].Health != null && all[i].Stats != null)
                {
                    all[i].Health.SetMaxHealth(all[i].Stats.Get(StatType.MaxHealth), false);
                }

                all[i].ApplyStats();
            }

            ReadCurrentValues();
        }

        private float Section(float x, float y, float width, string title, Color color)
        {
            GuiKit.Fill(new Rect(x, y + 8f, width, 2f), new Color(color.r, color.g, color.b, 0.35f));

            GUIStyle style = GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleLeft);
            float labelWidth = 8f + title.Length * 8f;

            GuiKit.Fill(new Rect(x, y, labelWidth, 18f), _panelColor);
            GuiKit.OutlinedLabel(new Rect(x, y, labelWidth, 18f), title, style,
                color, new Color(0f, 0f, 0f, 0.85f), 1f);

            return y + 26f;
        }

        /// <summary>Une ligne : intitulé, curseur, valeur. Renvoie la valeur choisie.</summary>
        private float Row(float x, ref float y, float width, string label, float value,
            Vector2 range, string format, Color color)
        {
            GUIStyle name = GuiKit.Style(13, FontStyle.Normal, TextAnchor.MiddleLeft);
            GuiKit.OutlinedLabel(new Rect(x, y, 150f, 22f), label, name,
                new Color(1f, 1f, 1f, 0.9f), new Color(0f, 0f, 0f, 0.8f), 1f);

            float sliderX = x + 150f;
            float sliderWidth = width - 150f - 62f;

            // Rail dessine a la main puis curseur invisible par-dessus : le curseur par defaut
            // d'IMGUI depend du skin de l'editeur et jure avec le reste de l'interface.
            Rect rail = new Rect(sliderX, y + 9f, sliderWidth, 5f);
            GuiKit.Fill(rail, new Color(0f, 0f, 0f, 0.6f));

            float filled = Mathf.InverseLerp(range.x, range.y, value);
            GuiKit.Fill(new Rect(rail.x, rail.y, rail.width * filled, rail.height), color);
            GuiKit.Fill(new Rect(rail.x + rail.width * filled - 3f, y + 4f, 6f, 15f), Color.white);

            Color previous = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, 0f);
            float result = GUI.HorizontalSlider(new Rect(sliderX, y + 2f, sliderWidth, 20f), value, range.x, range.y);
            GUI.color = previous;

            GUIStyle valueStyle = GuiKit.Style(13, FontStyle.Bold, TextAnchor.MiddleRight);
            GuiKit.OutlinedLabel(new Rect(x + width - 58f, y, 58f, 22f), value.ToString(format), valueStyle,
                color, new Color(0f, 0f, 0f, 0.85f), 1f);

            y += 28f;
            return result;
        }

        private bool Button(Rect rect, string label, Color color)
        {
            bool hover = rect.Contains(Event.current.mousePosition);

            GuiKit.Fill(rect, hover ? new Color(color.r * 0.32f, color.g * 0.32f, color.b * 0.32f, 1f)
                                   : new Color(0.12f, 0.13f, 0.16f, 1f));
            GuiKit.Outline(rect, 2f, new Color(color.r, color.g, color.b, hover ? 1f : 0.6f));

            GuiKit.OutlinedLabel(rect, label, GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter),
                hover ? Color.white : new Color(color.r, color.g, color.b, 0.95f),
                new Color(0f, 0f, 0f, 0.85f), 1f);

            return GUI.Button(rect, GUIContent.none, GUIStyle.none);
        }
    }
}
