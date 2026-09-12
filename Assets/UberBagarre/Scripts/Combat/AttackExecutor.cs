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

        [SerializeField]
        [Tooltip("Repere des poses de PIED. A laisser sur la racine du personnage : origine au " +
                 "sol et orientation du corps. Si on utilisait le repere de visee, regarder le " +
                 "ciel enverrait le coup de pied en l'air.")]
        private Transform _footPoseSpace;

        [Header("Hitbox par membre")]
        [SerializeField] private Hitbox _leftHitbox;
        [SerializeField] private Hitbox _rightHitbox;
        [SerializeField] private Hitbox _leftFootHitbox;
        [SerializeField] private Hitbox _rightFootHitbox;

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

        /// <summary>
        /// Vrai si le coup en cours est assez avancé pour qu'un autre l'interrompe.
        ///
        /// La fenêtre s'ouvre après la fenêtre d'impact : on ne peut donc pas annuler un coup
        /// avant qu'il ait eu sa chance de toucher.
        /// </summary>
        public bool CanChain
        {
            get { return _attack != null && Progress >= _attack.comboCancelAt; }
        }

        public bool IsReady
        {
            get
            {
                if (_cooldown > 0f) return false;
                if (_attack != null) return CanChain;
                return _combatant == null || _combatant.CanAct;
            }
        }
        public AttackData CurrentAttack { get { return _attack; } }

        /// <summary>
        /// Durée réelle du coup en cours, vitesse d'attaque comprise.
        ///
        /// <see cref="StatType.AttackSpeed"/> existait depuis la phase 6, avec son infobulle
        /// « multiplie la vitesse d'exécution des coups », et AUCUNE ligne de code ne la lisait.
        /// Une statistique qu'on peut régler et qui ne fait rien est pire qu'une statistique
        /// absente : elle fait croire que le levier existe.
        /// </summary>
        public float EffectiveDuration
        {
            get
            {
                if (_attack == null) return 0f;

                float speed = _combatant != null && _combatant.Stats != null
                    ? _combatant.Stats.Get(StatType.AttackSpeed)
                    : 1f;

                return Mathf.Max(0.04f, _attack.duration / Mathf.Max(0.1f, speed));
            }
        }

        /// <summary>Progression du coup en cours, 0 à 1.</summary>
        public float Progress
        {
            get { return _attack == null ? 0f : Mathf.Clamp01(_time / Mathf.Max(0.02f, EffectiveDuration)); }
        }

        private void OnEnable()
        {
            Subscribe(_leftHitbox, true);
            Subscribe(_rightHitbox, true);
            Subscribe(_leftFootHitbox, true);
            Subscribe(_rightFootHitbox, true);
        }

        private void OnDisable()
        {
            Subscribe(_leftHitbox, false);
            Subscribe(_rightHitbox, false);
            Subscribe(_leftFootHitbox, false);
            Subscribe(_rightFootHitbox, false);
        }

        private void Subscribe(Hitbox hitbox, bool add)
        {
            if (hitbox == null) return;

            if (add) hitbox.Hit += OnHitboxHit;
            else hitbox.Hit -= OnHitboxHit;
        }

        public bool TryPlay(AttackData attack)
        {
            if (attack == null) return Refuse("aucune donnee d'attaque assignee (champ vide dans PlayerCombat ?)", true);
            if (_hands == null) return Refuse("pas de FirstPersonHands assigne sur l'executeur", true);
            if (_cooldown > 0f) return Refuse("temps de repos : " + _cooldown.ToString("0.00") + " s", false);

            bool chaining = false;

            if (_attack != null)
            {
                if (!CanChain)
                {
                    return Refuse("coup en cours a " + (Progress * 100f).ToString("0") + " %, " +
                                  "enchainable a partir de " + (_attack.comboCancelAt * 100f).ToString("0") + " %", false);
                }

                chaining = true;
            }

            // L'etat Attacking ne doit pas bloquer un enchainement : c'est NOTRE propre coup qui
            // le tient. Tout autre etat (touche, etourdi, esquive, mort) refuse toujours.
            bool ownAttackState = _combatant != null && _combatant.State.Current == CombatantState.Attacking;

            if (_combatant != null && !_combatant.CanAct && !(chaining && ownAttackState))
            {
                return Refuse("etat du combattant : " + _combatant.State.Current, false);
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
                if (!_combatant.Stamina.CanSpend(attack.staminaCost)) return Refuse("endurance insuffisante", false);
                _combatant.Stamina.TrySpend(attack.staminaCost);
            }

            // Le coup precedent est clos SANS temps de repos : l'enchainement est la recompense
            // d'avoir laisse le coup aller au bout de sa fenetre d'impact, il ne doit pas se payer.
            if (chaining) EndCurrent(false);

            _attack = attack;

            // L'etat dure exactement le coup, vitesse comprise : un coup accelere qui laisserait
            // l'etat Attacking courir a l'ancienne duree bloquerait tout juste apres sa fin.
            if (_combatant != null) _combatant.State.Enter(CombatantState.Attacking, EffectiveDuration);

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

        /// <summary>
        /// Refuse un coup, en distinguant deux natures de refus.
        ///
        /// Un refus de RYTHME (coup en cours, temps de repos, endurance vide) est attendu et
        /// arrive des centaines de fois par combat, d'autant plus depuis que le joueur dispose
        /// d'un tampon d'entrée qui réessaie à chaque image. Le journaliser noierait la console et
        /// masquerait les vrais problèmes.
        ///
        /// Un refus de CONFIGURATION (donnée d'attaque absente, composant non câblé) est un bug :
        /// il doit crier. C'est la distinction qui manquait, et qui m'avait fait mettre tous les
        /// refus au même niveau de bruit.
        ///
        /// Les deux renseignent LastRefusal : l'overlay de diagnostic affiche donc toujours la
        /// dernière raison, même silencieuse.
        /// </summary>
        private bool Refuse(string reason, bool loud)
        {
            LastRefusal = reason;

            if (loud && _logRefusals) Debug.LogError("[UberBagarre] Coup refuse : " + reason, this);

            return false;
        }

        /// <summary>Interrompt le coup en cours (touché, étourdi, mort).</summary>
        public void Cancel()
        {
            EndCurrent(false);
        }

        /// <summary>
        /// Clôt le coup en cours. <paramref name="applyCooldown"/> distingue les trois fins
        /// possibles : le coup est allé au bout (repos normal), il est annulé par un coup reçu
        /// (pas de repos, on est déjà puni par l'état Hit), ou il est enchaîné (pas de repos non
        /// plus, c'est la récompense).
        /// </summary>
        private void EndCurrent(bool applyCooldown)
        {
            if (_attack == null) return;

            CloseHitWindow();
            ClearLimbPose();

            if (_locomotion != null) _locomotion.CombatBodyEuler = Vector3.zero;
            if (_cameraPunch != null) _cameraPunch.SetDriven(Vector3.zero, Vector3.zero);

            AttackData finished = _attack;
            if (applyCooldown) _cooldown = finished.cooldown;
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

            float duration = EffectiveDuration;
            float normalized = Mathf.Clamp01(_time / Mathf.Max(0.02f, duration));

            ApplyPose(normalized);
            UpdateHitWindow(normalized);

            if (_time >= duration) Finish();
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
                // Aucune donnee d'animation : plutot que d'envoyer le membre a l'origine du repere
                // (c'est-a-dire dans l'oeil du joueur), on fabrique un coup a partir de la pose
                // de repos. Un coup laid vaut mieux qu'un coup invisible qui ne touche rien.
                key = new AttackPoseKey();
                key.grip = 1f;

                float extension = Mathf.Sin(Mathf.Clamp01(normalized) * Mathf.PI);

                if (_attack.limb == AttackLimb.Foot)
                {
                    pose = new HandPose(
                        new Vector3(mirrored ? -0.16f : 0.16f, 0.12f + 0.42f * extension, -0.17f + 0.70f * extension),
                        new Vector3(-22f * extension, 0f, 0f));
                }
                else
                {
                    HandPose guard = _hands.GetGuardPose(_side);
                    pose = new HandPose(guard.position + Vector3.forward * (0.30f * extension), guard.euler);
                }
            }

            if (_attack.limb == AttackLimb.Foot) ApplyFootPose(pose, weight);
            else if (_hands != null) _hands.SetAttackPose(_side, pose, weight, key.grip);

            ApplyOffHandPose(key, mirrored, weight);

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

        /// <summary>
        /// Un coup de pied ne s'applique pas comme un coup de poing : la jambe est déjà pilotée
        /// par le cycle de marche. On dépose donc une cible que la locomotion mélangera, au lieu
        /// d'écrire directement sur l'os et de se battre avec elle.
        /// </summary>
        private void ApplyFootPose(HandPose pose, float weight)
        {
            if (_locomotion == null) return;

            Transform space = FootPoseSpace;
            Vector3 world = space.TransformPoint(pose.position);
            Quaternion rotation = space.rotation * pose.Rotation;

            _locomotion.SetFootOverride(_side == HandSide.Left, world, rotation, weight);
        }

        /// <summary>
        /// Repère des coups de pied : la racine du personnage.
        ///
        /// On ne peut pas réutiliser le repère des mains : il suit le tangage de la caméra, donc
        /// lever les yeux déplacerait la cible du pied. Une hauteur de pied n'a de sens que
        /// mesurée depuis le sol.
        /// </summary>
        private Transform FootPoseSpace
        {
            get { return _footPoseSpace != null ? _footPoseSpace : transform; }
        }

        /// <summary>
        /// Pose du membre libre : la main opposée à celle qui frappe.
        ///
        /// Un bras qui part seul pendant que l'autre reste figé, c'est exactement ce qui fait
        /// « animation bricolée ». Sur un coup de poing l'autre main remonte se couvrir ; sur un
        /// coup de pied le bras opposé s'ouvre pour tenir l'équilibre.
        /// </summary>
        private void ApplyOffHandPose(AttackPoseKey key, bool mirrored, float weight)
        {
            if (_hands == null) return;

            HandSide other = _side == HandSide.Left ? HandSide.Right : HandSide.Left;

            // Un coup de pied ne mobilise aucune main : les deux restent libres, et c'est celle
            // du cote oppose au pied qui contrebalance. La regle est donc la meme pour tous les
            // coups, et la donnee seule decide si la main libre bouge.
            if (key.offHandWeight <= 0.001f)
            {
                _hands.ClearAttackPose(other);
                return;
            }

            HandPose pose = AttackData.Mirror(key.offHandPosition, key.offHandEuler, mirrored);
            _hands.SetAttackPose(other, pose, weight * key.offHandWeight, -1f);
        }

        private void ClearLimbPose()
        {
            // Les deux mains sont relachees dans tous les cas : un coup de pied pose lui aussi
            // le bras oppose, et une pose oubliee resterait collee jusqu'au prochain coup.
            if (_hands != null)
            {
                _hands.ClearAttackPose(HandSide.Left);
                _hands.ClearAttackPose(HandSide.Right);
            }

            // Rien a faire pour le pied : la locomotion laisse le poids retomber d'elle-meme
            // des qu'on cesse de le reecrire. Couper net ferait claquer la jambe.
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

            // La zone visee est resolue A L'OUVERTURE du coup, pas a l'impact : c'est ce que le
            // joueur visait quand il a engage son poing qui doit compter, pas ce qui se trouve
            // sous son reticule deux dixiemes de seconde plus tard.
            hitbox.Open(template, _attack.hitRadius, AimResolver.Resolve(_combatant));
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
            bool isLeft = _side == HandSide.Left;

            if (_attack != null && _attack.limb == AttackLimb.Foot)
            {
                return isLeft ? _leftFootHitbox : _rightFootHitbox;
            }

            return isLeft ? _leftHitbox : _rightHitbox;
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
            EndCurrent(true);
        }
    }
}
