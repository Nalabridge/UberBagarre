using System;
using UberBagarre.Feedback;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Joue un coup : anime la main, tourne le corps, bouge la caméra, ouvre la fenêtre d'impact.
    ///
    /// Ce composant ne lit aucune entrée et ne choisit aucun coup — on lui en donne un.
    /// C'est ce qui permettra à l'ennemi d'utiliser exactement le même exécuteur, piloté
    /// par son IA plutôt que par la souris.
    ///
    /// Le corps tourne AVEC le bras : c'est ce qui distingue un coup qui a du poids d'un bras
    /// qui se tend tout seul. La rotation du buste est décrite dans les données du coup, donc
    /// ajouter un coude ou un coup de pied plus tard ne demandera aucune ligne ici.
    /// </summary>
    public class AttackExecutor : MonoBehaviour
    {
        [Header("References")]
        [SerializeField]
        [Tooltip("Optionnel. S'il est renseigne, l'etat et l'endurance conditionnent les coups.")]
        private Combatant _combatant;

        [SerializeField] private FirstPersonHands _hands;
        [SerializeField] private ProceduralLocomotion _locomotion;
        [SerializeField] private CameraPunch _cameraPunch;
        [SerializeField] private HitStop _hitStop;

        [Header("Hitbox par main")]
        [SerializeField] private Hitbox _leftHitbox;
        [SerializeField] private Hitbox _rightHitbox;

        [Header("Debug")]
        [SerializeField] private bool _logAttacks;

        [SerializeField]
        [Tooltip("Journalise la RAISON de chaque coup refuse. A laisser actif tant que le combat " +
                 "n'est pas stabilise : sans ca, un refus est totalement silencieux.")]
        private bool _logRefusals = true;

        private AttackData _attack;
        private HandSide _side;
        private int _variantIndex = -1;
        private int _previousVariant = -1;
        private float _time;
        private float _cooldown;
        private bool _hitWindowOpen;
        private bool _lastHandWasLead;

        public event Action<AttackData, HandSide> AttackStarted;
        public event Action<AttackData> AttackEnded;
        public event Action<AttackData, Hurtbox, Vector3> HitLanded;

        /// <summary>Raison du dernier refus. Affichée par l'overlay de debug.</summary>
        public string LastRefusal { get; private set; }

        public bool IsAttacking { get { return _attack != null; } }

        /// <summary>Vrai pendant la fenêtre où le coup peut toucher.</summary>
        public bool IsHitWindowOpen { get { return _hitWindowOpen; } }

        /// <summary>Position du poing qui frappe actuellement. Sert au diagnostic de portée.</summary>
        public Vector3 ActiveFistPosition
        {
            get
            {
                Hitbox hitbox = ActiveHitbox();
                return hitbox != null ? hitbox.Origin.position : transform.position;
            }
        }

        public bool IsReady
        {
            get
            {
                if (_attack != null || _cooldown > 0f) return false;
                return _combatant == null || _combatant.CanAct;
            }
        }
        public AttackData CurrentAttack { get { return _attack; } }

        /// <summary>Progression du coup en cours, 0 à 1.</summary>
        public float Progress
        {
            get { return _attack == null ? 0f : Mathf.Clamp01(_time / Mathf.Max(0.02f, _attack.duration)); }
        }

        private void OnEnable()
        {
            if (_leftHitbox != null) _leftHitbox.Hit += OnHitboxHit;
            if (_rightHitbox != null) _rightHitbox.Hit += OnHitboxHit;
        }

        private void OnDisable()
        {
            if (_leftHitbox != null) _leftHitbox.Hit -= OnHitboxHit;
            if (_rightHitbox != null) _rightHitbox.Hit -= OnHitboxHit;
        }

        public bool TryPlay(AttackData attack)
        {
            if (attack == null) return Refuse("aucune donnee d'attaque assignee (champ vide dans PlayerCombat ?)");
            if (_hands == null) return Refuse("pas de FirstPersonHands assigne sur l'executeur");
            if (_attack != null) return Refuse("un coup est deja en cours");
            if (_cooldown > 0f) return Refuse("temps de repos : " + _cooldown.ToString("0.00") + " s");

            if (_combatant != null && !_combatant.CanAct)
            {
                return Refuse("etat du combattant : " + _combatant.State.Current);
            }

            string problem = attack.Diagnose();
            if (!string.IsNullOrEmpty(problem))
            {
                Debug.LogError("[UberBagarre] L'attaque '" + attack.displayName + "' est incomplete : " + problem +
                               ". Relance 'Uber Bagarre > 4 - Regenerer les coups par defaut'.", attack);
            }

            // L'endurance se verifie AVANT de s'engager : un coup a moitie paye ne veut rien dire.
            if (_combatant != null && _combatant.Stamina != null)
            {
                if (!_combatant.Stamina.CanSpend(attack.staminaCost)) return Refuse("endurance insuffisante");
                _combatant.Stamina.TrySpend(attack.staminaCost);
            }

            if (_combatant != null)
            {
                _combatant.State.TryEnter(CombatantState.Attacking, attack.duration);
            }

            _attack = attack;
            _side = ResolveHand(attack);
            _variantIndex = attack.PickVariant(_previousVariant);
            _previousVariant = _variantIndex;
            _time = 0f;
            _hitWindowOpen = false;

            if (_logAttacks)
            {
                Debug.Log("[UberBagarre] " + attack.displayName + " (" + _side + ", variante " +
                          (_variantIndex + 1) + ")", this);
            }

            LastRefusal = string.Empty;

            Action<AttackData, HandSide> started = AttackStarted;
            if (started != null) started(attack, _side);

            return true;
        }

        private bool Refuse(string reason)
        {
            LastRefusal = reason;

            if (_logRefusals) Debug.LogWarning("[UberBagarre] Coup refuse : " + reason, this);

            return false;
        }

        /// <summary>Interrompt le coup en cours (touché, étourdi, mort).</summary>
        public void Cancel()
        {
            if (_attack == null) return;

            CloseHitWindow();
            if (_hands != null) _hands.ClearAttackPose(_side);
            if (_locomotion != null) _locomotion.CombatBodyEuler = Vector3.zero;
            if (_cameraPunch != null) _cameraPunch.SetDriven(Vector3.zero, Vector3.zero);

            AttackData finished = _attack;
            _attack = null;

            Action<AttackData> ended = AttackEnded;
            if (ended != null) ended(finished);
        }

        private HandSide ResolveHand(AttackData attack)
        {
            switch (attack.hand)
            {
                case AttackHand.Lead: return HandSide.Left;
                case AttackHand.Rear: return HandSide.Right;
                default:
                    _lastHandWasLead = !_lastHandWasLead;
                    return _lastHandWasLead ? HandSide.Left : HandSide.Right;
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;

            if (_attack == null)
            {
                if (_cooldown > 0f) _cooldown -= dt;
                return;
            }

            _time += dt;
            float normalized = Mathf.Clamp01(_time / Mathf.Max(0.02f, _attack.duration));

            ApplyPose(normalized);
            UpdateHitWindow(normalized);

            if (_time >= _attack.duration) Finish();
        }

        private void ApplyPose(float normalized)
        {
            bool mirrored = _side == HandSide.Left;
            float weight = _attack.EvaluateWeight(normalized);

            AttackPoseKey key;
            HandPose pose;

            if (_variantIndex >= 0)
            {
                key = _attack.Sample(_variantIndex, normalized);
                pose = AttackData.Mirror(key.handPosition, key.handEuler, mirrored);
            }
            else
            {
                // Aucune donnee d'animation : plutot que d'envoyer la main a l'origine du repere
                // (c'est-a-dire dans l'oeil du joueur), on fabrique un direct a partir de la garde.
                // Un coup laid vaut mieux qu'un coup invisible qui ne touche rien.
                key = new AttackPoseKey();
                key.grip = 1f;

                HandPose guard = _hands.GetGuardPose(_side);
                float extension = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);
                pose = new HandPose(guard.position + Vector3.forward * (0.30f * extension), guard.euler);
            }

            if (_hands != null) _hands.SetAttackPose(_side, pose, weight, key.grip);

            if (_locomotion != null)
            {
                _locomotion.CombatBodyEuler = AttackData.MirrorEuler(key.bodyEuler, mirrored) * weight;
            }

            if (_cameraPunch != null)
            {
                _cameraPunch.SetDriven(
                    AttackData.MirrorOffset(key.cameraOffset, mirrored) * weight,
                    AttackData.MirrorEuler(key.cameraEuler, mirrored) * weight);
            }
        }

        private void UpdateHitWindow(float normalized)
        {
            bool shouldBeOpen = normalized >= _attack.hitWindowStart && normalized <= _attack.hitWindowEnd;

            if (shouldBeOpen && !_hitWindowOpen) OpenHitWindow();
            else if (!shouldBeOpen && _hitWindowOpen) CloseHitWindow();
        }

        private void OpenHitWindow()
        {
            Hitbox hitbox = ActiveHitbox();
            if (hitbox == null) return;

            DamageInfo template = new DamageInfo();
            template.Amount = _combatant != null
                ? DamageCalculator.ComputeOutgoing(_attack, _combatant.Stats)
                : _attack.damage;
            template.ImpactForce = _attack.impactForce;
            template.Direction = transform.forward;
            template.Attack = _attack;

            hitbox.Open(template, _attack.hitRadius);
            _hitWindowOpen = true;
        }

        private void CloseHitWindow()
        {
            Hitbox hitbox = ActiveHitbox();
            if (hitbox != null) hitbox.Close();
            _hitWindowOpen = false;
        }

        private Hitbox ActiveHitbox()
        {
            return _side == HandSide.Left ? _leftHitbox : _rightHitbox;
        }

        private void OnHitboxHit(Hurtbox hurtbox, Vector3 point)
        {
            if (_attack == null) return;

            if (_hitStop != null) _hitStop.Play(_attack.hitStopDuration);

            if (_cameraPunch != null)
            {
                float strength = _attack.shakeIntensity;
                _cameraPunch.AddImpulse(
                    new Vector3(UnityEngine.Random.Range(-1f, 1f), UnityEngine.Random.Range(-1f, 1f), -1f) * strength,
                    new Vector3(-strength * 60f, UnityEngine.Random.Range(-1f, 1f) * strength * 40f, 0f));
            }

            Action<AttackData, Hurtbox, Vector3> landed = HitLanded;
            if (landed != null) landed(_attack, hurtbox, point);
        }

        private void Finish()
        {
            CloseHitWindow();
            if (_hands != null) _hands.ClearAttackPose(_side);

            if (_locomotion != null) _locomotion.CombatBodyEuler = Vector3.zero;
            if (_cameraPunch != null) _cameraPunch.SetDriven(Vector3.zero, Vector3.zero);

            AttackData finished = _attack;
            _cooldown = finished.cooldown;
            _attack = null;

            Action<AttackData> ended = AttackEnded;
            if (ended != null) ended(finished);
        }
    }
}
