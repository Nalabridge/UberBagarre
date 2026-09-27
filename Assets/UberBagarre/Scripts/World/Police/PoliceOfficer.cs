using UberBagarre.Combat;
using UberBagarre.Enemy;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Un agent de police, construit sur le même corps et le même cerveau de combat que les
    /// adversaires.
    ///
    /// Il vient à la dernière position connue, fouille le coin, et quand il te voit :
    /// - à une étoile, il veut te parler (se rendre, payer, ou fuir — fuir aggrave) ;
    /// - au-delà, il t'interpelle de force (le cerveau de combat prend la main) ;
    /// - si tu es en voiture, il court derrière et laisse les voitures de patrouille faire.
    /// Frapper un agent, c'est une étoile de plus. La brigade de Brandt cogne comme un boss.
    /// </summary>
    [RequireComponent(typeof(Combatant))]
    public class PoliceOfficer : MonoBehaviour
    {
        private enum Mode
        {
            Approach,
            Talk,
            Fight,
            Leave
        }

        private PoliceSystem _police;
        private Combatant _self;
        private EnemyBrain _brain;
        private EnemyMotor _motor;
        private Mode _mode;
        private int _stars;
        private bool _duty = true;
        private float _talkSince;
        private float _leaveSince;
        private Vector3 _wander;
        private float _nextWander;

        /// <summary>En service : debout, et pas en train de repartir.</summary>
        public bool OnDuty { get { return _duty && _self != null && _self.IsAlive; } }

        public void Begin(PoliceSystem police, int stars, bool brigade)
        {
            _police = police;
            _stars = stars;
            _self = GetComponent<Combatant>();
            _brain = GetComponent<EnemyBrain>();
            _motor = GetComponent<EnemyMotor>();

            if (_self != null)
            {
                _self.SetDisplayName(brigade ? "BRIGADE DE BRANDT" : "AGENT");
                _self.Damaged += OnDamaged;

                const string source = "Police";
                _self.Stats.RemoveBySource(source);
                float d = brigade ? 1f : Mathf.Clamp01((stars - 1) / 3f);
                _self.Stats.AddModifier(new StatModifier { stat = StatType.MaxHealth, percent = true, value = 0.2f + 0.9f * d, source = source });
                _self.Stats.AddModifier(new StatModifier { stat = StatType.Strength, percent = true, value = 0.1f + 0.6f * d, source = source });
                _self.Stats.AddModifier(new StatModifier { stat = StatType.Defense, percent = false, value = 4f + 10f * d, source = source });
                _self.ApplyStats();
            }

            if (_brain != null)
            {
                _brain.ApplyDifficulty(brigade ? 0.95f : 0.35f + 0.15f * stars);
                _brain.DetectionRange = 3f;
                _brain.enabled = false;
            }

            _mode = Mode.Approach;
        }

        private void OnDestroy()
        {
            if (_self != null) _self.Damaged -= OnDamaged;
            if (_police != null && _police.Talking == this) _police.Talking = null;
        }

        private void OnDamaged(Combatant self, DamageInfo info)
        {
            if (info.AttackerFaction != Faction.Player || _police == null) return;
            if (_mode != Mode.Fight) Crimes.Report(Crime.AgressionPolicier, transform.position, gameObject);
            GoFight();
        }

        /// <summary>Plus recherché : il range sa matraque et s'en va.</summary>
        public void StandDown()
        {
            _duty = false;
            _mode = Mode.Leave;
            _leaveSince = Time.time;
            if (_brain != null) _brain.enabled = false;
            if (_police != null && _police.Talking == this) _police.Talking = null;
        }

        public bool CanSee(Vector3 point, float range)
        {
            if (!OnDuty) return false;
            Vector3 eyes = transform.position + Vector3.up * 1.65f;
            if ((point - eyes).sqrMagnitude > range * range) return false;
            return PoliceSystem.Clear(eyes, point + Vector3.up * 1.1f);
        }

        private void Update()
        {
            if (_police == null || _self == null) return;
            if (!_self.IsAlive)
            {
                if (_police.Talking == this) _police.Talking = null;
                return;
            }

            Vector3 player = _police.PlayerPosition;
            float distance = PoliceSystem.Flat(player - transform.position).magnitude;

            if (_mode == Mode.Leave)
            {
                Move(transform.position - player, 0.6f);
                if (Time.time - _leaveSince > 12f || distance > 70f) Destroy(gameObject);
                return;
            }

            if (distance > 140f)
            {
                Destroy(gameObject);
                return;
            }

            bool sees = CanSee(player, 38f);
            bool driving = PlayerDriving.IsDriving;

            switch (_mode)
            {
                case Mode.Approach:
                    if (sees && distance < 5f && !driving)
                    {
                        // En pleine bagarre, on ne discute pas : on sépare, à la matraque.
                        bool brawling = UberBagarre.Player.CombatPresence.Player != null && UberBagarre.Player.CombatPresence.Player.InCombat;
                        if (_police.Stars <= 1 && !brawling) StartTalk();
                        else GoFight();
                        break;
                    }

                    Vector3 goal = sees ? player : Search(player);
                    Move(goal - transform.position, sees ? 1.9f : 1f);
                    break;

                case Mode.Talk:
                    UpdateTalk(player, distance);
                    break;

                case Mode.Fight:
                    // En voiture, on ne se bat pas avec une portière : il court derrière.
                    if (driving || distance > 14f)
                    {
                        if (_brain != null) _brain.enabled = false;
                        Move(player - transform.position, 1.9f);
                    }
                    else if (_brain != null && !_brain.enabled)
                    {
                        _brain.DetectionRange = 40f;
                        _brain.enabled = true;
                        if (_motor != null) _motor.SpeedMultiplier = 1f;
                    }

                    if (_police.Stars == 0) StandDown();
                    break;
            }
        }

        /// <summary>Il fouille le cercle de recherche : il marche d'un point à l'autre.</summary>
        private Vector3 Search(Vector3 player)
        {
            if (Time.time >= _nextWander || PoliceSystem.Flat(_wander - transform.position).magnitude < 2f)
            {
                _nextWander = Time.time + 6f;
                Vector2 r = Random.insideUnitCircle * _police.SearchRadius * 0.6f;
                _wander = _police.LastSeen + new Vector3(r.x, 0f, r.y);
            }

            return _wander;
        }

        private void StartTalk()
        {
            _mode = Mode.Talk;
            _talkSince = Time.time;
            _police.Talking = this;
            _police.Say("AGENT", "Police. Pas un geste. On a deux mots à te dire.");
        }

        private void UpdateTalk(Vector3 player, float distance)
        {
            if (_motor != null) _motor.FaceTowards(player);

            var input = PlayerInput;
            if (input != null && input.Provider != null && input.Bindings != null)
            {
                if (input.InteractPressed)
                {
                    _police.Talking = null;
                    _police.Surrender();
                    return;
                }

                if (input.Provider.GetPressedThisFrame(input.Bindings.restartFight) && _police.Bribe())
                {
                    _police.Talking = null;
                    return;
                }
            }

            // Il s'en va : refus d'obtempérer. Il traîne : on l'embarque de force.
            if (distance > 7f || Time.time - _talkSince > 14f)
            {
                _police.Talking = null;
                if (distance > 7f)
                {
                    _police.Say("AGENT", "Hé ! Reviens ici !");
                    _police.AddStars(2, player, Crime.Bagarre);
                }

                GoFight();
            }
        }

        private void GoFight()
        {
            if (_mode == Mode.Fight || !_duty) return;
            if (_police.Talking == this) _police.Talking = null;
            _mode = Mode.Fight;
            if (_brain != null)
            {
                _brain.DetectionRange = 40f;
                _brain.enabled = !PlayerDriving.IsDriving;
            }
        }

        private void Move(Vector3 direction, float speed)
        {
            if (_motor == null) return;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.25f) return;
            _motor.SpeedMultiplier = speed;
            _motor.SetMoveIntent(direction, 1f);
            _motor.FaceTowards(transform.position + direction);
        }

        private static UberBagarre.Player.PlayerInputReader _input;

        private static UberBagarre.Player.PlayerInputReader PlayerInput
        {
            get
            {
                if (_input == null) _input = FindAnyObjectByType<UberBagarre.Player.PlayerInputReader>();
                return _input;
            }
        }
    }
}
