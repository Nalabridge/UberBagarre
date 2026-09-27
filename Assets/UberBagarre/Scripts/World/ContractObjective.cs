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
        PoingsSeuls = 8,
        CoupsAuSol = 9
    }

    /// <summary>
    /// La consigne du client et son suivi pendant la bagarre : « casse-lui le nez », « K.O. en
    /// moins de quarante secondes », « sans prendre un coup au visage »...
    ///
    /// Réussie, elle paie un bonus et le client laisse cinq étoiles ; ratée, la course est quand
    /// même payée, mais l'avis s'en ressent — et la réputation aussi. Elle est FACULTATIVE.
    ///
    /// Les consignes de « casse » (nez, jambe, côtes) et les coups au sol restent ouvertes
    /// après le K.O. : la cible est à terre, on peut finir le travail (un coup de pied au sol)
    /// tant que la photo n'est pas prise. Les autres se jugent au moment du K.O.
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
        private bool _knockedOut;
        private int _groundHits;
        private HealthSystem _targetHealth;
        private bool _frozen;
        private bool _frozenResult;

        public const int GroundHitsNeeded = 3;

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
                case ContractGoal.CoupsAuSol: return "Une fois à terre, " + GroundHitsNeeded + " coups au sol";
                default: return string.Empty;
            }
        }

        public string Text { get { return Label(Goal); } }

        /// <summary>Ce que rapporte une consigne, en part du prix de la course : casser une jambe paie plus.</summary>
        public static float BonusShare(ContractGoal goal)
        {
            switch (goal)
            {
                case ContractGoal.CasserJambe: return 0.6f;
                case ContractGoal.CasserCotes: return 0.5f;
                case ContractGoal.SansVisage: return 0.5f;
                case ContractGoal.Rapide: return 0.5f;
                case ContractGoal.CoupsAuSol: return 0.35f;
                default: return 0.42f;
            }
        }

        /// <summary>Une consigne au hasard, plus exigeante quand la course vaut plus d'étoiles.</summary>
        public static ContractGoal Pick(int stars, ContractGoal avoid)
        {
            ContractGoal[] easy = { ContractGoal.CasserNez, ContractGoal.PoingsSeuls, ContractGoal.CasserCotes, ContractGoal.FinirUppercut, ContractGoal.CoupsAuSol };
            ContractGoal[] hard = { ContractGoal.CasserJambe, ContractGoal.Rapide, ContractGoal.SansVisage, ContractGoal.DeuxChutes, ContractGoal.CasserNez, ContractGoal.CasserJambe };
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
                _targetHealth = _target.Health;
                if (_targetHealth != null) _targetHealth.HitWhileDown += OnHitWhileDown;
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
            if (_targetHealth != null) _targetHealth.HitWhileDown -= OnHitWhileDown;
        }

        /// <summary>Un coup sur la cible K.O., au sol.</summary>
        private void OnHitWhileDown(DamageInfo info)
        {
            if (info.AttackerFaction != Faction.Player) return;
            _groundHits++;
        }

        /// <summary>Coups portés à la cible pendant qu'elle était à terre (tombée ou K.O.).</summary>
        public int GroundHits { get { return _groundHits; } }

        /// <summary>
        /// La cible est K.O. : les consignes de rapidité, de style ou de chutes sont jugées ici ;
        /// celles de casse et de coups au sol restent ouvertes jusqu'à la photo.
        /// </summary>
        public void Knockout()
        {
            if (_knockedOut) return;
            _knockedOut = true;

            if (StaysOpen) return;
            _frozenResult = Judge(Time.time - _start);
            _frozen = true;
        }

        /// <summary>La consigne peut encore se remplir après le K.O. (la cible est à terre).</summary>
        public bool StaysOpen
        {
            get
            {
                return Goal == ContractGoal.CasserNez || Goal == ContractGoal.CasserJambe || Goal == ContractGoal.CasserCotes ||
                       Goal == ContractGoal.CoupsAuSol;
            }
        }

        /// <summary>Déjà tenue (avant la photo) : pour l'afficher en vert tout de suite.</summary>
        public bool AlreadyMet
        {
            get { return Goal != ContractGoal.Aucune && (_frozen ? _frozenResult : Judge(Time.time - _start)); }
        }

        private void OnTargetDamaged(Combatant self, DamageInfo info)
        {
            if (info.Blocked || info.AttackerFaction != Faction.Player) return;

            string name = info.Attack != null ? info.Attack.displayName : string.Empty;
            _lastAttack = name ?? string.Empty;
            if (_knockdown != null && _knockdown.IsDown) _groundHits++;

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
                if (_frozen) return _frozenResult ? "Consigne tenue ✓" : "Consigne ratée ✗";

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
                    case ContractGoal.CoupsAuSol: return "Coups au sol : " + Mathf.Min(_groundHits, GroundHitsNeeded) + " / " + GroundHitsNeeded;
                    default: return string.Empty;
                }
            }
        }

        /// <summary>La cible est K.O. : la consigne est-elle tenue ?</summary>
        public bool Evaluate()
        {
            if (Evaluated) return Succeeded;

            float elapsed = Time.time - _start;
            Succeeded = _frozen ? _frozenResult : Judge(elapsed);
            End();
            Evaluated = true;
            return Succeeded;
        }

        private bool Judge(float elapsed)
        {
            switch (Goal)
            {
                case ContractGoal.CasserNez: return _injuries != null && _injuries.NoseBroken;
                case ContractGoal.CasserJambe: return _injuries != null && _injuries.LegBroken;
                case ContractGoal.CasserCotes: return _injuries != null && _injuries.RibsBroken;
                case ContractGoal.Rapide: return elapsed <= FastLimit;
                case ContractGoal.SansVisage: return !_faceHit;
                case ContractGoal.FinirUppercut: return _lastAttack.ToLowerInvariant().Contains("uppercut");
                case ContractGoal.DeuxChutes: return _falls >= 2;
                case ContractGoal.PoingsSeuls: return !_kicked;
                case ContractGoal.CoupsAuSol: return _groundHits >= GroundHitsNeeded;
                default: return true;
            }
        }
    }
}
