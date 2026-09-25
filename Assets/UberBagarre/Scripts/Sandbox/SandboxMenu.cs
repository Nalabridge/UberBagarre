using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Feedback;
using UberBagarre.Player;
using UberBagarre.UI;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Le tableau de bord du bac à sable (Tab) : tout ce qui se règle pendant qu'on joue.
    ///
    /// Pourquoi ça vaut largement son code : équilibrer un combat depuis l'Inspector demande de
    /// sortir du mode Play, donc de perdre la situation qu'on voulait tester. On règle alors à
    /// l'aveugle, une valeur à la fois, avec dix secondes de rechargement par essai. Ici tout se
    /// règle PENDANT le combat et l'effet se voit au coup suivant.
    ///
    /// Les réglages passent par <see cref="CombatantStats.SetOverride"/>, donc par le même chemin
    /// que n'importe quelle amélioration future : le menu se sert du système de statistiques, il ne
    /// le contourne pas. Et ils sont SAUVEGARDÉS, parce qu'un réglage trouvé après dix minutes
    /// d'essais et perdu au redémarrage ne vaut rien.
    /// </summary>
    public class SandboxMenu : MonoBehaviour
    {
        private enum Tab
        {
            Combat = 0,
            Waves = 1,
            Stats = 2,
            Graphics = 3,
            Help = 4
        }

        /// <summary>
        /// Profils d'adversaire.
        ///
        /// Quatre profils très écartés plutôt que des variations molles : un adversaire qui diffère
        /// de 10 % du précédent ne se joue pas différemment, donc il n'apprend rien. Un boxeur qui
        /// frappe deux fois plus vite mais tombe en trois coups demande une autre approche qu'une
        /// brute qui encaisse tout — et c'est cette différence d'approche qui révèle si les
        /// mécaniques tiennent.
        /// </summary>
        private enum Archetype
        {
            Voyou = 0,
            Boxeur = 1,
            Cogneur = 2,
            Brute = 3
        }

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
        [SerializeField] private WaveDirector _waves;
        [SerializeField] private CombatStatistics _statistics;

        [SerializeField]
        [Tooltip("Optionnel. Permet de regler la nervosite du combat en jouant.")]
        private PlayerCombat _playerCombat;

        [SerializeField] private HitStop _hitStop;

        [SerializeField]
        [Tooltip("Facade des reglages graphiques. Elle pousse chaque valeur vers TOUTES les cameras " +
                 "a la fois : sans elle, regler le bloom ne toucherait que le point de vue actif.")]
        private GraphicsDirector _graphics;

        [Header("Apparition")]
        [SerializeField, Min(1f)] private float _spawnDistance = 4.5f;
        [SerializeField, Min(1)] private int _maxEnemies = 12;

        [Header("Bornes des reglages")]
        [SerializeField] private Vector2 _healthRange = new Vector2(10f, 500f);
        [SerializeField] private Vector2 _damageMultiplierRange = new Vector2(0.1f, 5f);
        [SerializeField] private Vector2 _attackSpeedRange = new Vector2(0.5f, 2.5f);
        [SerializeField] private Vector2 _hitStopRange = new Vector2(0.05f, 1f);
        [SerializeField] private Vector2 _inputBufferRange = new Vector2(0f, 0.4f);
        [SerializeField] private Vector2 _impactPhysicsRange = new Vector2(0f, 2.5f);

        [Header("Apparence")]
        [SerializeField] private Color _panelColor = new Color(0.06f, 0.07f, 0.10f, 0.96f);
        [SerializeField] private Color _accent = new Color(0.98f, 0.80f, 0.30f);
        [SerializeField] private Color _playerAccent = new Color(0.42f, 0.82f, 1f);
        [SerializeField] private Color _enemyAccent = new Color(1f, 0.46f, 0.38f);

        private const string PrefsPrefix = "UberBagarre.Sandbox.";

        private readonly List<GameObject> _spawned = new List<GameObject>();

        private Tab _tab = Tab.Combat;
        private bool _open;
        private bool _initialised;

        private float _playerHealth = 100f;
        private float _playerDamage = 1f;
        private float _enemyHealth = 90f;
        private float _enemyDamage = 1f;
        private float _attackSpeed = 1f;
        private float _hitStopStrength = 0.25f;
        private float _inputBufferSeconds = 0.22f;
        private float _impactPhysics = 1f;
        private Archetype _archetype = Archetype.Voyou;

        public bool IsOpen { get { return _open; } }

        private void Start()
        {
            ReadCurrentValues();
            LoadPreferences();
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

            if (_player != null && _player.Stats != null)
            {
                _playerDamage = ToMultiplier(_player.Stats.Get(StatType.Strength));
                _attackSpeed = _player.Stats.Get(StatType.AttackSpeed);
            }

            if (_hitStop != null) _hitStopStrength = _hitStop.SlowTimeScale;
            if (_playerCombat != null) _inputBufferSeconds = _playerCombat.InputBuffer;
            _impactPhysics = BodyImpactPhysics.GlobalScale;

            Combatant enemy = FirstEnemy();
            if (enemy == null) return;

            if (enemy.Health != null) _enemyHealth = enemy.Health.MaxHealth;
            if (enemy.Stats != null) _enemyDamage = ToMultiplier(enemy.Stats.Get(StatType.Strength));
        }

        // ------------------------------------------------------------------ persistance

        /// <summary>
        /// Relit les réglages d'une session précédente.
        ///
        /// Un réglage de ressenti se trouve par essais successifs, parfois longs. Le perdre au
        /// redémarrage oblige à tout refaire, ce qui est exactement la friction que ce menu existe
        /// pour supprimer.
        /// </summary>
        private void LoadPreferences()
        {
            if (!PlayerPrefs.HasKey(PrefsPrefix + "saved")) return;

            _playerHealth = PlayerPrefs.GetFloat(PrefsPrefix + "playerHealth", _playerHealth);
            _playerDamage = PlayerPrefs.GetFloat(PrefsPrefix + "playerDamage", _playerDamage);
            _enemyHealth = PlayerPrefs.GetFloat(PrefsPrefix + "enemyHealth", _enemyHealth);
            _enemyDamage = PlayerPrefs.GetFloat(PrefsPrefix + "enemyDamage", _enemyDamage);
            _attackSpeed = PlayerPrefs.GetFloat(PrefsPrefix + "attackSpeed", _attackSpeed);
            _hitStopStrength = PlayerPrefs.GetFloat(PrefsPrefix + "hitStop", _hitStopStrength);
            _inputBufferSeconds = PlayerPrefs.GetFloat(PrefsPrefix + "inputBuffer", _inputBufferSeconds);
            _impactPhysics = PlayerPrefs.GetFloat(PrefsPrefix + "impactPhysics", _impactPhysics);
            _archetype = (Archetype)PlayerPrefs.GetInt(PrefsPrefix + "archetype", (int)_archetype);

            ApplyPlayer();
            ApplyEnemies();
            ApplyFeel();

            Debug.Log("[UberBagarre] Reglages du bac a sable restaures (Tab pour les revoir).", this);
        }

        private void SavePreferences()
        {
            PlayerPrefs.SetInt(PrefsPrefix + "saved", 1);
            PlayerPrefs.SetFloat(PrefsPrefix + "playerHealth", _playerHealth);
            PlayerPrefs.SetFloat(PrefsPrefix + "playerDamage", _playerDamage);
            PlayerPrefs.SetFloat(PrefsPrefix + "enemyHealth", _enemyHealth);
            PlayerPrefs.SetFloat(PrefsPrefix + "enemyDamage", _enemyDamage);
            PlayerPrefs.SetFloat(PrefsPrefix + "attackSpeed", _attackSpeed);
            PlayerPrefs.SetFloat(PrefsPrefix + "hitStop", _hitStopStrength);
            PlayerPrefs.SetFloat(PrefsPrefix + "inputBuffer", _inputBufferSeconds);
            PlayerPrefs.SetFloat(PrefsPrefix + "impactPhysics", _impactPhysics);
            PlayerPrefs.SetInt(PrefsPrefix + "archetype", (int)_archetype);
            PlayerPrefs.Save();
        }

        private void ForgetPreferences()
        {
            PlayerPrefs.DeleteKey(PrefsPrefix + "saved");
            PlayerPrefs.Save();
        }

        private void Update()
        {
            if (_input == null) return;
            if (_input.ToggleSandboxMenuPressed) SetOpen(!_open);
        }

        public void SetOpen(bool open)
        {
            _open = open;

            if (!open) SavePreferences();
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

            // On ne remplit PAS a bloc : changer son maximum en plein combat ne doit pas etre un
            // soin gratuit.
            if (_player.Health != null) _player.Health.SetMaxHealth(_playerHealth, false);
            _player.ApplyStats();
        }

        private void ApplyFeel()
        {
            if (_player != null && _player.Stats != null)
            {
                _player.Stats.SetOverride(StatType.AttackSpeed, _attackSpeed);
            }

            if (_hitStop != null) _hitStop.SlowTimeScale = _hitStopStrength;
            if (_playerCombat != null) _playerCombat.InputBuffer = _inputBufferSeconds;

            BodyImpactPhysics.GlobalScale = _impactPhysics;
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

            // Le modele aussi : sinon les prochaines copies repartiraient des anciennes valeurs.
            if (_enemyTemplate == null) return;

            Combatant template = _enemyTemplate.GetComponent<Combatant>();
            if (template != null) ApplyEnemyStats(template);
        }

        /// <summary>
        /// Applique profil ET réglages manuels à un adversaire.
        ///
        /// L'ordre compte : le profil pose d'abord une base cohérente (un boxeur est rapide,
        /// fragile et mobile), puis les curseurs de vie et de dégâts l'emportent. Le joueur peut
        /// donc prendre un boxeur et lui donner 300 points de vie sans perdre sa vitesse.
        /// </summary>
        private void ApplyEnemyStats(Combatant combatant)
        {
            if (combatant.Stats == null)
            {
                if (combatant.Health != null) combatant.Health.SetMaxHealth(_enemyHealth, false);
                return;
            }

            ApplyArchetype(combatant.Stats);

            combatant.Stats.SetOverride(StatType.MaxHealth, _enemyHealth);
            combatant.Stats.SetOverride(StatType.Strength, ToStrength(_enemyDamage));

            if (combatant.Health != null) combatant.Health.SetMaxHealth(_enemyHealth, false);
            combatant.ApplyStats();
        }

        private void ApplyArchetype(CombatantStats stats)
        {
            switch (_archetype)
            {
                case Archetype.Boxeur:
                    Profile(stats, 6f, 1.55f, 1.30f);
                    break;

                case Archetype.Cogneur:
                    Profile(stats, 20f, 0.72f, 0.82f);
                    break;

                case Archetype.Brute:
                    Profile(stats, 30f, 0.58f, 0.68f);
                    break;

                default:
                    Profile(stats, 10f, 1f, 1f);
                    break;
            }
        }

        private static void Profile(CombatantStats stats, float defence, float attackSpeed, float moveSpeed)
        {
            stats.SetOverride(StatType.Defense, defence);
            stats.SetOverride(StatType.AttackSpeed, attackSpeed);
            stats.SetOverride(StatType.MoveSpeed, moveSpeed);
        }

        /// <summary>Vie suggérée par le profil. Le curseur peut toujours l'écraser ensuite.</summary>
        private float ArchetypeHealth()
        {
            switch (_archetype)
            {
                case Archetype.Boxeur: return 70f;
                case Archetype.Cogneur: return 170f;
                case Archetype.Brute: return 280f;
                default: return 90f;
            }
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
                Debug.LogWarning("[UberBagarre] SandboxMenu : aucun modele d'adversaire assigne.", this);
                return;
            }

            if (_spawned.Count >= _maxEnemies)
            {
                Debug.LogWarning("[UberBagarre] SandboxMenu : limite de " + _maxEnemies + " adversaires.", this);
                return;
            }

            Vector3 centre = _player != null ? _player.transform.position : transform.position;

            // Repartis selon l'angle d'or, et non tous au meme endroit : deux adversaires qui
            // apparaissent dans la meme capsule se repoussent violemment des la premiere image.
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

            _archetype = Archetype.Voyou;
            BodyImpactPhysics.GlobalScale = 1f;
            ForgetPreferences();
            ReadCurrentValues();
        }

        // ------------------------------------------------------------------ interface

        private void OnGUI()
        {
            if (!_open || !_initialised) return;

            float width = Mathf.Min(800f, Screen.width - 40f);
            float height = Mathf.Min(600f, Screen.height - 40f);

            Rect panel = new Rect((Screen.width - width) * 0.5f, (Screen.height - height) * 0.5f, width, height);

            GuiKit.Fill(new Rect(panel.x + 6f, panel.y + 7f, panel.width, panel.height), new Color(0f, 0f, 0f, 0.45f));
            GuiKit.Fill(panel, _panelColor);
            GuiKit.Outline(panel, 3f, _accent);

            GuiKit.Fill(new Rect(panel.x, panel.y, panel.width, 34f), new Color(1f, 1f, 1f, 0.07f));

            GuiKit.OutlinedLabel(new Rect(panel.x + 16f, panel.y + 4f, 260f, 26f),
                "UBER BAGARRE  —  BAC A SABLE", GuiKit.Style(15, FontStyle.Bold, TextAnchor.MiddleLeft),
                _accent, new Color(0f, 0f, 0f, 0.9f), 1.5f);

            GuiKit.OutlinedLabel(new Rect(panel.xMax - 180f, panel.y + 4f, 164f, 26f),
                "TAB pour fermer", GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleRight),
                new Color(1f, 1f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.8f), 1f);

            DrawTabs(new Rect(panel.x + 14f, panel.y + 40f, panel.width - 28f, 26f));

            Rect content = new Rect(panel.x + 18f, panel.y + 76f, panel.width - 36f, panel.height - 94f);

            switch (_tab)
            {
                case Tab.Waves: DrawWavesTab(content); break;
                case Tab.Stats: DrawStatsTab(content); break;
                case Tab.Graphics: DrawGraphicsTab(content); break;
                case Tab.Help: DrawHelpTab(content); break;
                default: DrawCombatTab(content); break;
            }
        }

        private void DrawTabs(Rect rect)
        {
            string[] names = { "COMBAT", "VAGUES", "STATISTIQUES", "GRAPHISMES", "COMMANDES" };
            float width = rect.width / names.Length;

            for (int i = 0; i < names.Length; i++)
            {
                Rect tab = new Rect(rect.x + i * width, rect.y, width - 4f, rect.height);
                bool active = (int)_tab == i;

                GuiKit.Fill(tab, active ? new Color(_accent.r * 0.28f, _accent.g * 0.24f, _accent.b * 0.12f, 1f)
                                        : new Color(0f, 0f, 0f, 0.35f));

                if (active) GuiKit.Fill(new Rect(tab.x, tab.yMax - 2f, tab.width, 2f), _accent);

                GuiKit.OutlinedLabel(tab, names[i], GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter),
                    active ? _accent : new Color(1f, 1f, 1f, 0.55f), new Color(0f, 0f, 0f, 0.85f), 1f);

                if (GUI.Button(tab, GUIContent.none, GUIStyle.none)) _tab = (Tab)i;
            }
        }

        // ------------------------------------------------------------------ onglet combat

        private void DrawCombatTab(Rect rect)
        {
            float column = (rect.width - 20f) * 0.5f;
            float y = rect.y;
            float left = rect.x;

            y = Section(left, y, column, "TOI", _playerAccent);

            float newPlayerHealth = Row(left, ref y, column, "Points de vie", _playerHealth, _healthRange, "0", _playerAccent);
            float newPlayerDamage = Row(left, ref y, column, "Degats infliges", _playerDamage, _damageMultiplierRange, "x0.00", _playerAccent);

            if (Changed(newPlayerHealth, _playerHealth) || Changed(newPlayerDamage, _playerDamage))
            {
                _playerHealth = newPlayerHealth;
                _playerDamage = newPlayerDamage;
                ApplyPlayer();
            }

            y += 10f;
            y = Section(left, y, column, "NERVOSITE", _accent);

            float newSpeed = Row(left, ref y, column, "Vitesse des coups", _attackSpeed, _attackSpeedRange, "x0.00", _accent);
            float newHitStop = Row(left, ref y, column, "Ralenti d'impact", _hitStopStrength, _hitStopRange, "0.00", _accent);
            float newBuffer = Row(left, ref y, column, "Tampon de touche", _inputBufferSeconds, _inputBufferRange, "0.00 s", _accent);
            float newPhysics = Row(left, ref y, column, "Physique des coups", _impactPhysics, _impactPhysicsRange, "x0.00", _accent);

            if (Changed(newSpeed, _attackSpeed) || Changed(newHitStop, _hitStopStrength)
                || Changed(newBuffer, _inputBufferSeconds) || Changed(newPhysics, _impactPhysics))
            {
                _attackSpeed = newSpeed;
                _hitStopStrength = newHitStop;
                _inputBufferSeconds = newBuffer;
                _impactPhysics = newPhysics;
                ApplyFeel();
            }

            // ----- colonne de droite
            float right = rect.x + column + 20f;
            float ry = rect.y;

            ry = Section(right, ry, column, "ADVERSAIRES", _enemyAccent);

            float newEnemyHealth = Row(right, ref ry, column, "Points de vie", _enemyHealth, _healthRange, "0", _enemyAccent);
            float newEnemyDamage = Row(right, ref ry, column, "Degats infliges", _enemyDamage, _damageMultiplierRange, "x0.00", _enemyAccent);

            bool enemyChanged = Changed(newEnemyHealth, _enemyHealth) || Changed(newEnemyDamage, _enemyDamage);

            _enemyHealth = newEnemyHealth;
            _enemyDamage = newEnemyDamage;

            ry += 6f;
            ry = Section(right, ry, column, "PROFIL", _enemyAccent);

            if (DrawArchetypes(new Rect(right, ry, column, 28f)))
            {
                _enemyHealth = ArchetypeHealth();
                enemyChanged = true;
            }

            ry += 36f;

            GUIStyle hint = GuiKit.Style(11, FontStyle.Italic, TextAnchor.UpperLeft);
            GuiKit.OutlinedLabel(new Rect(right, ry, column, 30f), ArchetypeDescription(), hint,
                new Color(1f, 1f, 1f, 0.55f), new Color(0f, 0f, 0f, 0.8f), 1f);

            ry += 34f;

            if (enemyChanged) ApplyEnemies();

            GuiKit.OutlinedLabel(new Rect(right, ry, column, 18f),
                "Sur le terrain : " + LiveEnemies() + " debout,  " + _spawned.Count + " ajoutes",
                GuiKit.Style(12, FontStyle.Normal, TextAnchor.MiddleLeft),
                new Color(1f, 1f, 1f, 0.65f), new Color(0f, 0f, 0f, 0.8f), 1f);

            ry += 26f;

            float half = (column - 10f) * 0.5f;

            if (Button(new Rect(right, ry, half, 30f), "+ ADVERSAIRE", _enemyAccent)) SpawnEnemy();
            if (Button(new Rect(right + half + 10f, ry, half, 30f), "RETIRER", _accent)) RemoveSpawned();

            ry += 38f;

            if (Button(new Rect(right, ry, half, 30f), "TOUT A NEUF", _playerAccent)) ReviveEverybody();
            if (Button(new Rect(right + half + 10f, ry, half, 30f), "D'ORIGINE", _accent)) ResetToDefaults();
        }

        private bool DrawArchetypes(Rect rect)
        {
            string[] names = { "Voyou", "Boxeur", "Cogneur", "Brute" };
            float width = (rect.width - 18f) / names.Length;
            bool changed = false;

            for (int i = 0; i < names.Length; i++)
            {
                Rect slot = new Rect(rect.x + i * (width + 6f), rect.y, width, rect.height);
                bool active = (int)_archetype == i;

                GuiKit.Fill(slot, active ? new Color(_enemyAccent.r * 0.30f, _enemyAccent.g * 0.18f, _enemyAccent.b * 0.16f, 1f)
                                         : new Color(0.12f, 0.13f, 0.16f, 1f));
                GuiKit.Outline(slot, 2f, new Color(_enemyAccent.r, _enemyAccent.g, _enemyAccent.b, active ? 1f : 0.45f));

                GuiKit.OutlinedLabel(slot, names[i], GuiKit.Style(11, FontStyle.Bold, TextAnchor.MiddleCenter),
                    active ? Color.white : new Color(1f, 1f, 1f, 0.6f), new Color(0f, 0f, 0f, 0.85f), 1f);

                if (!GUI.Button(slot, GUIContent.none, GUIStyle.none)) continue;
                if ((int)_archetype == i) continue;

                _archetype = (Archetype)i;
                changed = true;
            }

            return changed;
        }

        private string ArchetypeDescription()
        {
            switch (_archetype)
            {
                case Archetype.Boxeur: return "Rapide et mobile, mais fragile. Punit l'hesitation.";
                case Archetype.Cogneur: return "Lent et solide. Il faut le faire tomber pour l'ouvrir.";
                case Archetype.Brute: return "Tres lente, tres dure. Le balayage est la seule reponse.";
                default: return "Equilibre. La reference pour regler le reste.";
            }
        }

        // ------------------------------------------------------------------ onglet vagues

        private void DrawWavesTab(Rect rect)
        {
            float y = rect.y;
            y = Section(rect.x, y, rect.width, "MODE VAGUES", _accent);

            if (_waves == null)
            {
                GuiKit.OutlinedLabel(new Rect(rect.x, y, rect.width, 22f),
                    "Aucun WaveDirector dans la scene. Regenere la scene (Uber Bagarre > 2).",
                    GuiKit.Style(13, FontStyle.Normal, TextAnchor.MiddleLeft),
                    new Color(1f, 0.6f, 0.5f), new Color(0f, 0f, 0f, 0.85f), 1f);
                return;
            }

            GUIStyle big = GuiKit.Style(40, FontStyle.Bold, TextAnchor.MiddleLeft);
            GuiKit.OutlinedLabel(new Rect(rect.x, y, 260f, 50f),
                _waves.Running ? "VAGUE " + _waves.Wave : "A L'ARRET", big,
                _waves.Running ? _accent : new Color(1f, 1f, 1f, 0.45f), new Color(0f, 0f, 0f, 0.9f), 2.5f);

            y += 56f;

            GUIStyle info = GuiKit.Style(13, FontStyle.Normal, TextAnchor.MiddleLeft);

            GuiKit.OutlinedLabel(new Rect(rect.x, y, rect.width, 20f),
                "Adversaires debout : " + _waves.AliveCount, info,
                new Color(1f, 1f, 1f, 0.8f), new Color(0f, 0f, 0f, 0.8f), 1f);

            y += 24f;

            float wait = _waves.NextWaveIn;
            GuiKit.OutlinedLabel(new Rect(rect.x, y, rect.width, 20f),
                _waves.Running
                    ? (wait > 0f ? "Prochaine vague dans " + wait.ToString("0.0") + " s" : "Vague en cours")
                    : "Lance les vagues pour te battre a un contre plusieurs.",
                info, new Color(1f, 1f, 1f, 0.65f), new Color(0f, 0f, 0f, 0.8f), 1f);

            y += 34f;

            float half = (rect.width - 14f) * 0.5f;

            if (!_waves.Running)
            {
                if (Button(new Rect(rect.x, y, half, 34f), "LANCER LES VAGUES", _enemyAccent)) _waves.StartWaves();
            }
            else if (Button(new Rect(rect.x, y, half, 34f), "ARRETER", _accent))
            {
                _waves.Stop();
            }

            if (Button(new Rect(rect.x + half + 14f, y, half, 34f), "VIDER LE TERRAIN", _accent))
            {
                _waves.Clear();
                RemoveSpawned();
            }

            y += 48f;

            GuiKit.OutlinedLabel(new Rect(rect.x, y, rect.width, 76f),
                "Chaque vague ajoute des adversaires ET les renforce : plus de vie, plus de degats,\n" +
                "des coups plus rapides. Multiplier les adversaires sans les renforcer rendrait les\n" +
                "vagues plus longues, pas plus dures — et allonger un test n'apprend rien.\n" +
                "Le profil choisi dans l'onglet COMBAT s'applique aussi aux vagues.",
                GuiKit.Style(12, FontStyle.Italic, TextAnchor.UpperLeft),
                new Color(1f, 1f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.8f), 1f);
        }

        // ------------------------------------------------------------------ onglet graphismes

        /// <summary>
        /// Les réglages d'image, en direct.
        ///
        /// Ils sont là pour une raison précise : « c'est trop » et « ce n'est pas assez » ne sont
        /// pas des critiques qu'on peut traiter à distance. Le même bloom paraît discret sur un
        /// écran calibré et aveuglant sur un autre, et personne ne peut régler ça à la place de
        /// celui qui regarde. Ces curseurs transforment donc un désaccord en manipulation : on
        /// pousse jusqu'à ce que ce soit bien, et c'est sauvegardé.
        /// </summary>
        private void DrawGraphicsTab(Rect rect)
        {
            float y = rect.y;

            if (_graphics == null)
            {
                y = Section(rect.x, y, rect.width, "GRAPHISMES", _accent);

                GuiKit.OutlinedLabel(new Rect(rect.x, y, rect.width, 22f),
                    "Aucun GraphicsDirector dans la scene. Regenere la scene (Uber Bagarre > 2).",
                    GuiKit.Style(13, FontStyle.Normal, TextAnchor.MiddleLeft),
                    new Color(1f, 0.6f, 0.5f), new Color(0f, 0f, 0f, 0.85f), 1f);
                return;
            }

            float column = (rect.width - 20f) * 0.5f;
            float right = rect.x + column + 20f;
            float ly = y;
            float ry = y;

            // --- colonne gauche : la lumiere
            ly = Section(rect.x, ly, column, "LUMIERE", _accent);

            float day = Row(rect.x, ref ly, column, "Heure", _graphics.Day, new Vector2(0f, 1f), "0.00", _accent);
            if (Changed(day, _graphics.Day)) _graphics.Day = day;

            float bloom = Row(rect.x, ref ly, column, "Bloom", _graphics.Bloom, new Vector2(0f, 4f), "0.00", _accent);
            if (Changed(bloom, _graphics.Bloom)) _graphics.Bloom = bloom;

            float threshold = Row(rect.x, ref ly, column, "Seuil de bloom", _graphics.Threshold,
                new Vector2(0.2f, 2.5f), "0.00", _accent);
            if (Changed(threshold, _graphics.Threshold)) _graphics.Threshold = threshold;

            float exposure = Row(rect.x, ref ly, column, "Exposition", _graphics.Exposure,
                new Vector2(0.3f, 2.5f), "0.00", _accent);
            if (Changed(exposure, _graphics.Exposure)) _graphics.Exposure = exposure;

            float volumetric = Row(rect.x, ref ly, column, "Lumiere dans l'air", _graphics.Volumetric,
                new Vector2(0f, 3f), "0.00", _accent);
            if (Changed(volumetric, _graphics.Volumetric)) _graphics.Volumetric = volumetric;

            ly += 6f;
            ly = Section(rect.x, ly, column, "COULEUR", _playerAccent);

            float saturation = Row(rect.x, ref ly, column, "Saturation", _graphics.Saturation,
                new Vector2(0f, 2f), "0.00", _playerAccent);
            if (Changed(saturation, _graphics.Saturation)) _graphics.Saturation = saturation;

            float contrast = Row(rect.x, ref ly, column, "Contraste", _graphics.Contrast,
                new Vector2(0.5f, 2f), "0.00", _playerAccent);
            if (Changed(contrast, _graphics.Contrast)) _graphics.Contrast = contrast;

            // --- colonne droite : l'objectif
            ry = Section(right, ry, column, "OBJECTIF", _enemyAccent);

            float vignette = Row(right, ref ry, column, "Vignette", _graphics.Vignette,
                new Vector2(0f, 1f), "0.00", _enemyAccent);
            if (Changed(vignette, _graphics.Vignette)) _graphics.Vignette = vignette;

            float aberration = Row(right, ref ry, column, "Aberration", _graphics.Aberration,
                new Vector2(0f, 4f), "0.00", _enemyAccent);
            if (Changed(aberration, _graphics.Aberration)) _graphics.Aberration = aberration;

            float grain = Row(right, ref ry, column, "Grain", _graphics.Grain,
                new Vector2(0f, 0.3f), "0.000", _enemyAccent);
            if (Changed(grain, _graphics.Grain)) _graphics.Grain = grain;

            ry += 6f;
            ry = Section(right, ry, column, "PERFORMANCE", _accent);

            float quality = Row(right, ref ry, column, "Finesse du reflet",
                9 - _graphics.ReflectionDownsample, new Vector2(1f, 8f), "0", _accent);

            int downsample = 9 - Mathf.RoundToInt(quality);
            if (downsample != _graphics.ReflectionDownsample) _graphics.ReflectionDownsample = downsample;

            float half = (column - 10f) * 0.5f;

            if (Button(new Rect(right, ry, half, 28f),
                    _graphics.PostEnabled ? "POST : ON" : "POST : OFF",
                    _graphics.PostEnabled ? _playerAccent : _enemyAccent))
            {
                _graphics.PostEnabled = !_graphics.PostEnabled;
                _graphics.Save();
            }

            if (Button(new Rect(right + half + 10f, ry, half, 28f),
                    _graphics.ReflectionsEnabled ? "REFLETS : ON" : "REFLETS : OFF",
                    _graphics.ReflectionsEnabled ? _playerAccent : _enemyAccent))
            {
                _graphics.ReflectionsEnabled = !_graphics.ReflectionsEnabled;
                _graphics.Save();
            }

            ry += 36f;

            // Un bouton qui fait defiler les quatre modes : TAA (le plus stable), MSAA (aretes
            // nettes en rendu avant), FXAA (le plus leger), aucun.
            UberPostProcess.AntiAliasingMode aa = _graphics.AntiAliasing;

            if (Button(new Rect(right, ry, column, 28f), "ANTICRENELAGE : " + AntiAliasingLabel(aa),
                    aa == UberPostProcess.AntiAliasingMode.Aucun ? _enemyAccent : _playerAccent))
            {
                _graphics.AntiAliasing = NextAntiAliasing(aa);
                _graphics.Save();
            }

            ry += 36f;

            // --- ambiances
            float bottom = Mathf.Max(ly, ry) + 10f;
            float third = (rect.width - 28f) / 3f;

            if (Button(new Rect(rect.x, bottom, third, 32f), "SOBRE", _playerAccent))
            {
                _graphics.ApplyPreset(GraphicsDirector.Preset.Sobre);
            }

            if (Button(new Rect(rect.x + third + 14f, bottom, third, 32f), "CINEMA", _accent))
            {
                _graphics.ApplyPreset(GraphicsDirector.Preset.Cinema);
            }

            if (Button(new Rect(rect.x + (third + 14f) * 2f, bottom, third, 32f), "BATARD", _enemyAccent))
            {
                _graphics.ApplyPreset(GraphicsDirector.Preset.Batard);
            }

            // Sauvegarde au RELACHEMENT, pas a chaque image : ecrire les preferences a
            // chaque pixel de deplacement du curseur ferait des centaines d'ecritures disque
            // pour un seul reglage.
            if (Event.current.type == EventType.MouseUp) _graphics.Save();

            bottom += 40f;

            GuiKit.OutlinedLabel(new Rect(rect.x, bottom, rect.width, 82f),
                "HEURE a 0 = nuit, a 1 = plein jour : le soleil, la lune, le ciel, la brume et toutes\n" +
                "les enseignes suivent le meme curseur. SEUIL DE BLOOM decide a partir de quelle\n" +
                "luminosite une surface deborde — en dessous de 1, meme un mur eclaire se met a briller.\n" +
                "LUMIERE DANS L'AIR : le faisceau des lampes dans la brume, calcule a partir des vraies lampes.\n" +
                "REFLETS coute un second rendu de la scene : c'est le premier reglage a baisser si ca rame.",
                GuiKit.Style(12, FontStyle.Italic, TextAnchor.UpperLeft),
                new Color(1f, 1f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.8f), 1f);
        }

        // ------------------------------------------------------------------ onglet statistiques

        private void DrawStatsTab(Rect rect)
        {
            float y = rect.y;
            y = Section(rect.x, y, rect.width, "CE COMBAT", _playerAccent);

            if (_statistics == null)
            {
                GuiKit.OutlinedLabel(new Rect(rect.x, y, rect.width, 22f),
                    "Aucun CombatStatistics dans la scene. Regenere la scene (Uber Bagarre > 2).",
                    GuiKit.Style(13, FontStyle.Normal, TextAnchor.MiddleLeft),
                    new Color(1f, 0.6f, 0.5f), new Color(0f, 0f, 0f, 0.85f), 1f);
                return;
            }

            float column = (rect.width - 20f) * 0.5f;
            float ly = y;
            float ry = y;

            Stat(rect.x, ref ly, column, "Coups lances", _statistics.AttacksThrown.ToString(), _playerAccent);
            Stat(rect.x, ref ly, column, "Coups au but", _statistics.AttacksLanded.ToString(), _playerAccent);
            Stat(rect.x, ref ly, column, "Reussite", (_statistics.Accuracy * 100f).ToString("0") + " %", _accent);
            Stat(rect.x, ref ly, column, "Meilleur combo", _statistics.BestCombo.ToString(), _accent);
            Stat(rect.x, ref ly, column, "Plus gros coup", _statistics.HighestHit.ToString("0"), _accent);
            Stat(rect.x, ref ly, column, "Parades", _statistics.Parries.ToString(), _playerAccent);
            Stat(rect.x, ref ly, column, "Blocages", _statistics.Blocks.ToString(), _playerAccent);

            float right = rect.x + column + 20f;

            Stat(right, ref ry, column, "Degats infliges", _statistics.DamageDealt.ToString("0"), _playerAccent);
            Stat(right, ref ry, column, "Degats encaisses", _statistics.DamageTaken.ToString("0"), _enemyAccent);
            Stat(right, ref ry, column, "Rapport", _statistics.DamageRatio.ToString("0.00"), _accent);
            Stat(right, ref ry, column, "Chutes provoquees", _statistics.KnockdownsCaused.ToString(), _playerAccent);
            Stat(right, ref ry, column, "Chutes subies", _statistics.KnockdownsSuffered.ToString(), _enemyAccent);
            Stat(right, ref ry, column, "K.O. infliges", _statistics.Knockouts.ToString(), _playerAccent);
            Stat(right, ref ry, column, "Fois mis K.O.", _statistics.Deaths.ToString(), _enemyAccent);

            float bottom = Mathf.Max(ly, ry) + 12f;

            if (Button(new Rect(rect.x, bottom, 200f, 30f), "REMETTRE A ZERO", _accent))
            {
                _statistics.ResetStatistics();
            }

            bottom += 40f;

            GuiKit.OutlinedLabel(new Rect(rect.x, bottom, rect.width, 54f),
                "La REUSSITE est le chiffre le plus utile : un joueur qui rate la moitie de ses coups\n" +
                "a l'impression que l'adversaire encaisse trop, alors que le probleme est sa precision.\n" +
                "Le RAPPORT au-dessus de 1 veut dire que l'echange tourne a ton avantage.",
                GuiKit.Style(12, FontStyle.Italic, TextAnchor.UpperLeft),
                new Color(1f, 1f, 1f, 0.5f), new Color(0f, 0f, 0f, 0.8f), 1f);
        }

        private void Stat(float x, ref float y, float width, string label, string value, Color color)
        {
            GuiKit.Fill(new Rect(x, y, width, 22f), new Color(1f, 1f, 1f, 0.035f));

            GuiKit.OutlinedLabel(new Rect(x + 8f, y, width - 80f, 22f), label,
                GuiKit.Style(12, FontStyle.Normal, TextAnchor.MiddleLeft),
                new Color(1f, 1f, 1f, 0.75f), new Color(0f, 0f, 0f, 0.8f), 1f);

            GuiKit.OutlinedLabel(new Rect(x + width - 78f, y, 70f, 22f), value,
                GuiKit.Style(13, FontStyle.Bold, TextAnchor.MiddleRight),
                color, new Color(0f, 0f, 0f, 0.85f), 1f);

            y += 26f;
        }

        // ------------------------------------------------------------------ onglet commandes

        private void DrawHelpTab(Rect rect)
        {
            float column = (rect.width - 20f) * 0.5f;

            string[,] moves =
            {
                { "WASD / ZQSD", "Se deplacer" },
                { "Maj", "Courir" },
                { "C", "S'accroupir" },
                { "C en courant", "Glissade" },
                { "Espace", "Sauter" },
                { "Alt", "Esquive" },
                { "", "" },
                { "Ctrl maintenu", "Garde  (-72 %)" },
                { "Ctrl au bon moment", "PARADE, puis RIPOSTE x2,2" },
                { "", "" },
                { "Tab", "Ce menu" },
                { "F1", "Diagnostic" },
                { "F3", "Camera d'observation" },
                { "+ / -", "Zoom de l'observation" },
                { "R", "Relancer le combat" },
                { "Echap", "Liberer le curseur" }
            };

            string[,] attacks =
            {
                { "Clic gauche", "Direct" },
                { "Clic droit", "Crochet" },
                { "Clic molette", "Uppercut  (chargeable)" },
                { "F", "Coup de pied  (chargeable)" },
                { "V", "Coup de pied bas  (chargeable)" },
                { "", "" },
                { "Maintenir un coup lourd", "CHARGE : jusqu'a x2,2 degats" },
                { "Cadence", "Un coup = un appui, 0,5 s mini entre deux" },
                { "Endurance a zero", "EPUISE : plus rien pendant 1,2 s" },
                { "", "" },
                { "En sprintant", "Charge d'epaule" },
                { "En l'air", "Coup plongeant" },
                { "En glissade", "Balayage  (85 % de chute)" },
                { "Cible au sol + pied", "COUP DE GRACE  (28 degats)" }
            };

            DrawKeyList(new Rect(rect.x, rect.y, column, rect.height), "DEPLACEMENT ET DEFENSE", moves);
            DrawKeyList(new Rect(rect.x + column + 20f, rect.y, column, rect.height), "ATTAQUES", attacks);
        }

        private void DrawKeyList(Rect rect, string title, string[,] rows)
        {
            float y = Section(rect.x, rect.y, rect.width, title, _accent);

            GUIStyle key = GuiKit.Style(11, FontStyle.Bold, TextAnchor.MiddleLeft);
            GUIStyle action = GuiKit.Style(11, FontStyle.Normal, TextAnchor.MiddleLeft);

            for (int i = 0; i < rows.GetLength(0); i++)
            {
                if (string.IsNullOrEmpty(rows[i, 0]))
                {
                    y += 8f;
                    continue;
                }

                GuiKit.OutlinedLabel(new Rect(rect.x, y, 146f, 17f), rows[i, 0], key,
                    new Color(1f, 1f, 1f, 0.88f), new Color(0f, 0f, 0f, 0.8f), 1f);

                GuiKit.OutlinedLabel(new Rect(rect.x + 150f, y, rect.width - 150f, 17f), rows[i, 1], action,
                    new Color(1f, 1f, 1f, 0.58f), new Color(0f, 0f, 0f, 0.8f), 1f);

                y += 17f;
            }
        }

        // ------------------------------------------------------------------ primitives

        private static string AntiAliasingLabel(UberPostProcess.AntiAliasingMode mode)
        {
            switch (mode)
            {
                case UberPostProcess.AntiAliasingMode.Taa: return "TAA";
                case UberPostProcess.AntiAliasingMode.Msaa: return "MSAA x8";
                case UberPostProcess.AntiAliasingMode.Fxaa: return "FXAA";
                default: return "AUCUN";
            }
        }

        private static UberPostProcess.AntiAliasingMode NextAntiAliasing(UberPostProcess.AntiAliasingMode mode)
        {
            switch (mode)
            {
                case UberPostProcess.AntiAliasingMode.Taa: return UberPostProcess.AntiAliasingMode.Msaa;
                case UberPostProcess.AntiAliasingMode.Msaa: return UberPostProcess.AntiAliasingMode.Fxaa;
                case UberPostProcess.AntiAliasingMode.Fxaa: return UberPostProcess.AntiAliasingMode.Aucun;
                default: return UberPostProcess.AntiAliasingMode.Taa;
            }
        }

        private static bool Changed(float a, float b)
        {
            return !Mathf.Approximately(a, b);
        }

        private float Section(float x, float y, float width, string title, Color color)
        {
            GuiKit.Fill(new Rect(x, y + 8f, width, 2f), new Color(color.r, color.g, color.b, 0.35f));

            GUIStyle style = GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleLeft);
            float labelWidth = 10f + title.Length * 8f;

            GuiKit.Fill(new Rect(x, y, labelWidth, 18f), _panelColor);
            GuiKit.OutlinedLabel(new Rect(x, y, labelWidth, 18f), title, style,
                color, new Color(0f, 0f, 0f, 0.85f), 1f);

            return y + 26f;
        }

        /// <summary>Une ligne : intitulé, curseur, valeur. Renvoie la valeur choisie.</summary>
        private float Row(float x, ref float y, float width, string label, float value,
            Vector2 range, string format, Color color)
        {
            float labelWidth = Mathf.Min(138f, width * 0.42f);
            float valueWidth = 58f;

            GuiKit.OutlinedLabel(new Rect(x, y, labelWidth, 22f), label,
                GuiKit.Style(12, FontStyle.Normal, TextAnchor.MiddleLeft),
                new Color(1f, 1f, 1f, 0.9f), new Color(0f, 0f, 0f, 0.8f), 1f);

            float sliderX = x + labelWidth + 4f;
            float sliderWidth = Mathf.Max(30f, width - labelWidth - valueWidth - 8f);

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

            GuiKit.OutlinedLabel(new Rect(x + width - valueWidth, y, valueWidth, 22f), value.ToString(format),
                GuiKit.Style(13, FontStyle.Bold, TextAnchor.MiddleRight),
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
