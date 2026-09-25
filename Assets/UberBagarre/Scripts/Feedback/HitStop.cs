using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Très court ralenti au moment d'un impact.
    ///
    /// C'est l'effet le plus rentable du jeu de combat : quelques centièmes de seconde suffisent
    /// à faire sentir qu'un coup a « accroché » quelque chose.
    ///
    /// Les réglages d'origine étaient un contresens, et c'est la principale raison pour laquelle
    /// le combat ne paraissait pas nerveux : 5 % de vitesse pendant 60 ms. Pris isolément, ça
    /// semble court. Mais sur un enchaînement à 6 coups par seconde, ça fait **360 ms de
    /// quasi-gel par seconde de combat** — plus d'un tiers du temps où le jeu ne répond
    /// pratiquement plus. Plus le joueur enchaînait vite, plus le jeu devenait pâteux : exactement
    /// l'inverse de l'effet recherché.
    ///
    /// Un hit-stop doit se SENTIR sans se VOIR. 25 % de vitesse pendant 30 ms marque l'impact et
    /// coûte 18 ms de temps de jeu au lieu de 57.
    /// </summary>
    public class HitStop : MonoBehaviour
    {
        [SerializeField] private bool _enabled = true;

        [SerializeField, Range(0f, 0.9f)]
        [Tooltip("Vitesse du temps pendant l'arret. 0 = fige completement. En dessous de ~0,15 le " +
                 "jeu arrete de repondre et l'enchainement devient pateux.")]
        private float _slowTimeScale = 0.45f;

        // Ralenti a peine perceptible : a 0,25 pendant 32 ms, chaque coup « coupait » l'image
        // (le temps se fige, la camera saccade) et le combat perdait tout son nerf.
        [SerializeField, Range(0f, 0.12f)] private float _maxDuration = 0.055f;

        [SerializeField, Min(0f)]
        [Tooltip("Temps mort minimal entre deux ralentis. Sans lui, un enchainement rapide " +
                 "declenche un ralenti par coup et le jeu passe son temps au ralenti.")]
        private float _cooldown = 0.09f;

        /// <summary>
        /// Vitesse du temps pendant le ralenti, réglable en jeu. 1 = aucun ralenti.
        ///
        /// Exposée parce que c'est un paramètre de RESSENTI, et qu'un paramètre de ressenti ne se
        /// règle pas en lisant du code : il se règle en jouant, manette en main, jusqu'à ce que ça
        /// tombe juste. Le menu de bac à sable s'en sert.
        /// </summary>
        public float SlowTimeScale
        {
            get { return _slowTimeScale; }
            set { _slowTimeScale = Mathf.Clamp(value, 0f, 1f); }
        }

        /// <summary>
        /// Vitesse « normale » du temps, à laquelle le jeu revient après chaque ralenti. 1 en
        /// jeu ; le menu de triche la change pour ralentir ou accélérer toute la partie.
        /// </summary>
        public static float BaseTimeScale
        {
            get { return _baseTimeScale; }
            set
            {
                _baseTimeScale = Mathf.Clamp(value, 0.05f, 4f);
                Time.timeScale = _baseTimeScale;
            }
        }

        private static float _baseTimeScale = 1f;

        // Sans rechargement du domaine entre deux lancements, un ralenti de triche laisse en
        // place survivrait jusqu'a la partie suivante.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _baseTimeScale = 1f;
        }

        private float _timer;
        private float _cooldownTimer;
        private float _defaultFixedDelta;

        // Ralenti « cinéma » : contre après parade, coup qui met K.O. Plus long qu'un
        // arrêt d'impact, avec une entrée et une sortie adoucies.
        private float _slowmoScale = 1f;
        private float _slowmoDuration;
        private float _slowmoElapsed = -1f;

        /// <summary>Le ralenti de ce combattant (le joueur). Null s'il n'y en a pas.</summary>
        public static HitStop Main { get; private set; }

        /// <summary>Vrai pendant un ralenti cinéma.</summary>
        public bool IsSlowMotion { get { return _slowmoElapsed >= 0f; } }

        private void Awake()
        {
            _defaultFixedDelta = Time.fixedDeltaTime;
            if (Main == null) Main = this;
        }

        private void OnDestroy()
        {
            if (Main == this) Main = null;
        }

        /// <summary>
        /// Ralenti de mise en scène : <paramref name="scale"/> au creux, pendant
        /// <paramref name="duration"/> secondes RÉELLES. Le temps plonge en 60 ms, tient, puis
        /// remonte en douceur sur le dernier tiers — un ralenti qui se coupe net se lit comme un
        /// bug, pas comme un effet.
        /// </summary>
        public void SlowMotion(float scale, float duration)
        {
            if (!_enabled || duration <= 0f) return;

            _slowmoScale = Mathf.Clamp(scale, 0.05f, 1f);
            _slowmoDuration = duration;
            _slowmoElapsed = 0f;
        }

        private float SlowmoFactor()
        {
            if (_slowmoElapsed < 0f) return 1f;

            float t = _slowmoElapsed / Mathf.Max(0.01f, _slowmoDuration);
            if (t >= 1f) return 1f;

            float dive = Mathf.Clamp01(_slowmoElapsed / 0.06f);
            float rise = Mathf.Clamp01((t - 0.66f) / 0.34f);
            float depth = Mathf.SmoothStep(0f, 1f, dive) * (1f - Mathf.SmoothStep(0f, 1f, rise));
            return Mathf.Lerp(1f, _slowmoScale, depth);
        }

        private void ApplyScale()
        {
            float factor = SlowmoFactor();
            if (_timer > 0f) factor = Mathf.Min(factor, _slowTimeScale);

            Time.timeScale = factor * _baseTimeScale;
            Time.fixedDeltaTime = _defaultFixedDelta * Mathf.Max(0.02f, factor);
        }

        public void Play(float duration)
        {
            if (!_enabled || duration <= 0f) return;

            // Un enchainement de cinq coups ne doit pas produire cinq ralentis : le premier
            // marque l'impact, les suivants ne feraient que bloquer le joueur.
            if (_cooldownTimer > 0f) return;

            _cooldownTimer = _cooldown;
            _timer = Mathf.Max(_timer, Mathf.Min(duration, _maxDuration));
            ApplyScale();
        }

        private void Update()
        {
            // Jeu en pause : rien ne bouge derriere le menu, pas meme ce qui compte en temps reel.
            if (UberBagarre.UI.GameMenu.IsPaused) return;

            if (_cooldownTimer > 0f) _cooldownTimer -= Time.unscaledDeltaTime;

            if (_slowmoElapsed >= 0f)
            {
                _slowmoElapsed += Time.unscaledDeltaTime;

                if (_slowmoElapsed >= _slowmoDuration)
                {
                    _slowmoElapsed = -1f;
                    if (_timer <= 0f) Restore();
                }
                else
                {
                    if (_timer > 0f) _timer -= Time.unscaledDeltaTime;
                    ApplyScale();
                    return;
                }
            }

            if (_timer <= 0f) return;

            _timer -= Time.unscaledDeltaTime;
            if (_timer > 0f) return;

            Restore();
        }

        private void OnDisable()
        {
            _cooldownTimer = 0f;
            _slowmoElapsed = -1f;
            Restore();
        }

        /// <summary>
        /// Rend sa vitesse normale au jeu. Ne touche PAS au temps mort : il doit survivre à la fin
        /// du ralenti, sinon il ne sert à rien — c'est justement entre deux ralentis qu'il compte.
        /// </summary>
        private void Restore()
        {
            _timer = 0f;
            Time.timeScale = _baseTimeScale;
            Time.fixedDeltaTime = _defaultFixedDelta;
        }
    }
}
