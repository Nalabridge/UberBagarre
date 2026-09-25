using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Core;
using UberBagarre.Enemy;
using UberBagarre.Feedback;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Les triches de test : invincibilité, vol libre, endurance infinie, K.O. en un coup,
    /// ennemis figés, vitesse du temps, et les raccourcis de l'histoire (chapitres, lieux,
    /// étapes).
    ///
    /// Elles existent pour TESTER, pas pour jouer : vérifier le chapitre 2 ne doit pas
    /// demander de rejouer vingt minutes de prologue, et regarder un décor sous tous les
    /// angles ne doit pas demander de le contourner à pied. Le menu (Tab, onglet TRICHE) les
    /// pilote ; ce composant en porte l'état et l'effet, pour que le menu reste un menu.
    ///
    /// Rien n'est sauvegardé : une triche oubliée allumée d'une session à l'autre fausserait
    /// tous les tests suivants sans que rien à l'écran ne le rappelle.
    /// </summary>
    [DisallowMultipleComponent]
    public class DebugCheats : MonoBehaviour
    {
        [Header("Joueur")]
        [SerializeField] private Combatant _player;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PlayerMotor _motor;
        [SerializeField] private Camera _camera;

        [Header("Histoire (optionnel)")]
        [SerializeField] private StoryDirector _story;
        [SerializeField] private PrologueDirector _prologue;
        [SerializeField] private SubtitleDisplay _subtitles;
        [SerializeField] private LocationDirector _locations;
        [SerializeField] private PlayerProgress _progress;

        [Header("Vol libre")]
        [SerializeField, Min(0.5f)] private float _flySpeed = 8f;
        [SerializeField, Min(1f)] private float _fastMultiplier = 3.5f;

        [Header("Raccourcis (sans ouvrir le menu)")]
        [SerializeField] private InputBinding _noclipKey = InputBinding.FromKey(KeyCode.F6);
        [SerializeField] private InputBinding _godModeKey = InputBinding.FromKey(KeyCode.F7);

        private bool _godMode;
        private bool _noclip;
        private bool _fly;
        private bool _infiniteStamina;
        private bool _oneHitKo;
        private bool _freezeEnemies;
        private CharacterController _controller;

        // --------------------------------------------------------------- état

        public bool GodMode
        {
            get { return _godMode; }
            set
            {
                _godMode = value;
                if (_player != null && _player.Health != null) _player.Health.GodMode = value;
            }
        }

        /// <summary>Vol à travers les murs : aucune collision, aucune gravité.</summary>
        public bool Noclip
        {
            get { return _noclip; }
            set
            {
                if (_noclip == value) return;
                _noclip = value;
                if (value) _fly = false;
                ApplyMovementMode();
            }
        }

        /// <summary>Vol libre, mais les murs arrêtent toujours.</summary>
        public bool Fly
        {
            get { return _fly; }
            set
            {
                if (_fly == value) return;
                _fly = value;
                if (value) _noclip = false;
                ApplyMovementMode();
            }
        }

        public bool InfiniteStamina
        {
            get { return _infiniteStamina; }
            set { _infiniteStamina = value; }
        }

        public bool OneHitKo
        {
            get { return _oneHitKo; }
            set { _oneHitKo = value; }
        }

        public bool FreezeEnemies
        {
            get { return _freezeEnemies; }
            set
            {
                _freezeEnemies = value;
                EnemyBrain.HoldAll = value;
            }
        }

        public float TimeScale
        {
            get { return HitStop.BaseTimeScale; }
            set { HitStop.BaseTimeScale = value; }
        }

        public float FlySpeed
        {
            get { return _flySpeed; }
            set { _flySpeed = Mathf.Max(0.5f, value); }
        }

        public bool HasStory
        {
            get { return _story != null || _prologue != null; }
        }

        public LocationDirector Locations
        {
            get { return _locations; }
        }

        public PlayerProgress Progress
        {
            get { return _progress; }
        }

        /// <summary>L'étape d'histoire en cours, pour l'affichage.</summary>
        public string CurrentBeat
        {
            get
            {
                if (_story == null) return string.Empty;
                if (!_story.IsRunning) return _story.IsFinished ? "(terminee)" : "(aucune)";
                return _story.CurrentBeatId + "  (" + (_story.BeatIndex + 1) + "/" + _story.BeatCount + ")";
            }
        }

        // --------------------------------------------------------------- cycle de vie

        private void Awake()
        {
            if (_player != null && _input == null) _input = _player.GetComponent<PlayerInputReader>();
            if (_player != null && _motor == null) _motor = _player.GetComponent<PlayerMotor>();
            if (_player != null && _camera == null) _camera = _player.GetComponentInChildren<Camera>();
            if (_player != null) _controller = _player.GetComponent<CharacterController>();
        }

        private void OnEnable()
        {
            Combatant.AnyDamaged += OnAnyDamaged;
        }

        private void OnDisable()
        {
            Combatant.AnyDamaged -= OnAnyDamaged;

            // Un composant éteint ne laisse aucune triche derrière lui.
            GodMode = false;
            Noclip = false;
            Fly = false;
            if (_freezeEnemies) FreezeEnemies = false;
            if (!Mathf.Approximately(TimeScale, 1f)) TimeScale = 1f;
        }

        private void Update()
        {
            // Jeu en pause : rien ne bouge derriere le menu, pas meme ce qui compte en temps reel.
            if (UberBagarre.UI.GameMenu.IsPaused) return;

            if (_input != null && _input.Provider != null)
            {
                if (_noclipKey.IsAssigned && _input.Provider.GetPressedThisFrame(_noclipKey)) Noclip = !Noclip;
                if (_godModeKey.IsAssigned && _input.Provider.GetPressedThisFrame(_godModeKey)) GodMode = !GodMode;
            }

            if (_infiniteStamina && _player != null && _player.Stamina != null) _player.Stamina.Refill();

            if (_noclip || _fly) UpdateFlight();
        }

        // --------------------------------------------------------------- vol

        private void ApplyMovementMode()
        {
            bool flying = _noclip || _fly;

            if (_motor != null)
            {
                _motor.ResetVelocity();
                _motor.enabled = !flying;
            }

            if (_controller != null) _controller.enabled = !_noclip;
            if (_locations != null) _locations.FallGuard = !flying;

            // Retour au sol : les systèmes qui suivent la position (caméra, IA, lieux) repartent
            // de l'endroit où l'on a atterri, pas de celui où l'on a décollé.
            if (!flying && _player != null)
            {
                Physics.SyncTransforms();
            }
        }

        private void UpdateFlight()
        {
            if (_player == null || _input == null) return;

            // Le menu ouvert (curseur libéré) coupe les entrées : on ne vole pas en réglant.
            if (!_input.GameplayInputEnabled) return;

            Transform view = _camera != null ? _camera.transform : _player.transform;

            Vector2 move = _input.Move;
            Vector3 direction = view.forward * move.y + view.right * move.x;

            // Monter et descendre : les touches de saut et d'accroupissement, lues directement
            // (le saut est un appui, pas un maintien, dans le lecteur d'entrées).
            if (_input.Provider != null && _input.Bindings != null)
            {
                if (_input.Provider.GetHeld(_input.Bindings.jump)) direction += Vector3.up;
                if (_input.CrouchHeld) direction += Vector3.down;
            }

            if (direction.sqrMagnitude > 1f) direction.Normalize();

            float speed = _flySpeed * (_input.SprintHeld ? _fastMultiplier : 1f);
            Vector3 delta = direction * speed * Time.unscaledDeltaTime;

            if (_noclip || _controller == null || !_controller.enabled)
            {
                _player.transform.position += delta;
            }
            else
            {
                _controller.Move(delta);
            }
        }

        // --------------------------------------------------------------- combat

        private void OnAnyDamaged(Combatant victim, DamageInfo info)
        {
            if (!_oneHitKo || victim == null || _player == null) return;
            if (victim == _player || victim.Faction == _player.Faction || !victim.IsAlive) return;
            if (info.Attacker != _player.gameObject) return;

            Kill(victim, info);
        }

        private static void Kill(Combatant victim, DamageInfo template)
        {
            if (victim == null || victim.Health == null || !victim.Health.IsAlive) return;

            DamageInfo lethal = template;
            lethal.Amount = victim.Health.Current + 1000f;
            lethal.Blocked = false;
            victim.Health.ApplyDamage(lethal);
        }

        /// <summary>Met K.O. tous les adversaires debout.</summary>
        public void KillEnemies()
        {
            if (_player == null) return;

            List<Combatant> targets = new List<Combatant>();
            IReadOnlyList<Combatant> all = Combatant.All;

            for (int i = 0; i < all.Count; i++)
            {
                Combatant other = all[i];
                if (other == null || other == _player || other.Faction == _player.Faction || !other.IsAlive) continue;
                if (!other.isActiveAndEnabled) continue;
                targets.Add(other);
            }

            for (int i = 0; i < targets.Count; i++)
            {
                DamageInfo info = new DamageInfo();
                info.Point = targets[i].AimPosition;
                info.Direction = (targets[i].transform.position - _player.transform.position).normalized;
                info.Zone = HitZone.Head;
                info.AttackerFaction = _player.Faction;
                info.Attacker = _player.gameObject;
                info.ImpactForce = 4f;

                Kill(targets[i], info);
            }
        }

        public void HealPlayer()
        {
            if (_player != null) _player.Revive();
        }

        // --------------------------------------------------------------- histoire

        public void SkipLine()
        {
            if (_subtitles != null) _subtitles.Skip();
        }

        public void SkipBeat()
        {
            if (_story != null) _story.SkipBeat();
        }

        /// <summary>0 = prologue, 1 = chapitre 1, 2 = chapitre 2.</summary>
        public void StartChapter(int chapter)
        {
            if (_prologue == null) return;

            Noclip = false;
            Fly = false;

            if (chapter <= 0)
            {
                if (_locations != null) _locations.GoTo(0);
                _prologue.Begin();
                return;
            }

            _prologue.StartAt(chapter == 1 ? "chapitre-1" : "chapitre-2");
        }

        public void GoTo(int location)
        {
            if (_locations == null) return;

            Noclip = false;
            Fly = false;
            _locations.GoTo(location);
        }

        public void AddMoney(int amount)
        {
            if (_progress != null) _progress.AddMoney(amount);
        }

        public void AddExperience(int amount)
        {
            if (_progress != null) _progress.AddExperience(amount);
        }
    }
}
