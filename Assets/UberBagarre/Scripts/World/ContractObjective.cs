using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>Les consignes spéciales qu'un client ajoute à sa commande.</summary>
    public enum ContractGoal
    {
        Aucune = 0,
        CasserNez = 1,
        CasserJambe = 2,
        CasserCotes = 3,
        Rapide = 4,
        SansVisage = 5,
        FinirUppercut = 6,
        DeuxChutes = 7,
        PoingsSeuls = 8
    }

    /// <summary>
    /// La consigne du client et son suivi pendant la bagarre : « casse-lui le nez », « K.O. en
    /// moins de quarante secondes », « sans prendre un coup au visage »...
    ///
    /// Réussie, elle paie un bonus et le client laisse cinq étoiles ; ratée, la course est quand
    /// même payée, mais l'avis s'en ressent — et la réputation aussi.
    /// </summary>
    public sealed class ContractObjective
    {
        public const float FastLimit = 40f;

        private Combatant _target;
        private Combatant _player;
        private TargetInjuries _injuries;
        private KnockdownSystem _knockdown;
        private float _start;
        private int _falls;
        private bool _faceHit;
        private bool _kicked;
        private string _lastAttack = string.Empty;
        private bool _running;

        public ContractGoal Goal { get; private set; }
        public int Bonus { get; private set; }
        public bool Evaluated { get; private set; }
        public bool Succeeded { get; private set; }

        public ContractObjective(ContractGoal goal, int bonus)
        {
            Goal = goal;
            Bonus = goal == ContractGoal.Aucune ? 0 : Mathf.Max(0, bonus);
        }

        public static string Label(ContractGoal goal)
        {
            switch (goal)
            {
                case ContractGoal.CasserNez: return "Casse-lui le nez";
                case ContractGoal.CasserJambe: return "Casse-lui une jambe (coups de pied bas)";
                case ContractGoal.CasserCotes: return "Casse-lui des côtes (au corps)";
                case ContractGoal.Rapide: return "K.O. en moins de " + Mathf.RoundToInt(FastLimit) + " secondes";
                case ContractGoal.SansVisage: return "Sans prendre un coup au visage";
                case ContractGoal.FinirUppercut: return "Finis-le à l'uppercut";
                case ContractGoal.DeuxChutes: return "Mets-le au sol deux fois";
                case ContractGoal.PoingsSeuls: return "Uniquement aux poings";
                default: return string.Empty;
            }
        }

        public string Text { get { return Label(Goal); } }

        /// <summary>Une consigne au hasard, plus exigeante quand la course vaut plus d'étoiles.</summary>
        public static ContractGoal Pick(int stars, ContractGoal avoid)
        {
            ContractGoal[] easy = { ContractGoal.CasserNez, ContractGoal.PoingsSeuls, ContractGoal.CasserCotes, ContractGoal.FinirUppercut };
            ContractGoal[] hard = { ContractGoal.CasserJambe, ContractGoal.Rapide, ContractGoal.SansVisage, ContractGoal.DeuxChutes, ContractGoal.CasserNez };
            ContractGoal[] pool = stars >= 2 && Random.value < 0.6f ? hard : easy;

            for (int i = 0; i < 6; i++)
            {
                ContractGoal g = pool[Random.Range(0, pool.Length)];
                if (g != avoid) return g;
            }

            return pool[0];
        }

        // ------------------------------------------------------------------ suivi

        /// <summary>La bagarre commence : le chrono part, on écoute les coups.</summary>
        public void Begin(Combatant target, Combatant player)
        {
            End();
            _target = target;
            _player = player;
            _start = Time.time;
            _running = true;

            if (_target != null)
            {
                _target.Damaged += OnTargetDamaged;
                _injuries = _target.GetComponent<TargetInjuries>();
                _knockdown = _target.GetComponentInChildren<KnockdownSystem>(true);
                if (_knockdown != null) _knockdown.KnockedDown += OnFall;
            }

            if (_player != null) _player.Damaged += OnPlayerDamaged;
        }

        public void End()
        {
            if (!_running) return;
            _running = false;

            if (_target != null) _target.Damaged -= OnTargetDamaged;
            if (_knockdown != null) _knockdown.KnockedDown -= OnFall;
            if (_player != null) _player.Damaged -= OnPlayerDamaged;
        }

        private void OnTargetDamaged(Combatant self, DamageInfo info)
        {
            if (info.Blocked || info.AttackerFaction != Faction.Player) return;

            string name = info.Attack != null ? info.Attack.displayName : string.Empty;
            _lastAttack = name ?? string.Empty;

            string lower = _lastAttack.ToLowerInvariant();
            if (lower.Contains("pied") || lower.Contains("balayage") || lower.Contains("tete") || lower.Contains("tête") ||
                lower.Contains("epaule") || lower.Contains("épaule"))
            {
                _kicked = true;
            }
        }

        private void OnPlayerDamaged(Combatant self, DamageInfo info)
        {
            if (!info.Blocked && info.Zone == HitZone.Head) _faceHit = true;
        }

        private void OnFall()
        {
            _falls++;
        }

        /// <summary>Temps écoulé depuis le début de la bagarre.</summary>
        public float Elapsed { get { return _running || Evaluated ? Time.time - _start : 0f; } }

        /// <summary>La ligne de suivi affichée pendant la bagarre (vide = rien à dire).</summary>
        public string Progress
        {
            get
            {
                if (Goal == ContractGoal.Aucune || !_running) return string.Empty;

                switch (Goal)
                {
                    case ContractGoal.CasserNez: return _injuries != null && _injuries.NoseBroken ? "Nez cassé ✓" : "Nez : pas encore";
                    case ContractGoal.CasserJambe: return _injuries != null && _injuries.LegBroken ? "Jambe cassée ✓" : "Jambe : pas encore";
                    case ContractGoal.CasserCotes: return _injuries != null && _injuries.RibsBroken ? "Côtes cassées ✓" : "Côtes : pas encore";
                    case ContractGoal.Rapide:
                        float left = FastLimit - Elapsed;
                        return left > 0f ? Mathf.CeilToInt(left) + " s" : "Trop tard";
                    case ContractGoal.SansVisage: return _faceHit ? "Touché au visage ✗" : "Visage intact";
                    case ContractGoal.DeuxChutes: return "Au sol : " + Mathf.Min(_falls, 2) + " / 2";
                    case ContractGoal.PoingsSeuls: return _kicked ? "Pas que les poings ✗" : "Poings seulement";
                    default: return string.Empty;
                }
            }
        }

        /// <summary>La cible est K.O. : la consigne est-elle tenue ?</summary>
        public bool Evaluate()
        {
            if (Evaluated) return Succeeded;

            float elapsed = Time.time - _start;
            End();
            Evaluated = true;

            switch (Goal)
            {
                case ContractGoal.Aucune: Succeeded = true; break;
                case ContractGoal.CasserNez: Succeeded = _injuries != null && _injuries.NoseBroken; break;
                case ContractGoal.CasserJambe: Succeeded = _injuries != null && _injuries.LegBroken; break;
                case ContractGoal.CasserCotes: Succeeded = _injuries != null && _injuries.RibsBroken; break;
                case ContractGoal.Rapide: Succeeded = elapsed <= FastLimit; break;
                case ContractGoal.SansVisage: Succeeded = !_faceHit; break;
                case ContractGoal.FinirUppercut: Succeeded = _lastAttack.ToLowerInvariant().Contains("uppercut"); break;
                case ContractGoal.DeuxChutes: Succeeded = _falls >= 2; break;
                case ContractGoal.PoingsSeuls: Succeeded = !_kicked; break;
            }

            return Succeeded;
        }
    }
}
