using UnityEngine;

namespace UberBagarre.Feedback
{
    /// <summary>
    /// Nœud de recul de caméra, entre les secousses et la caméra elle-même.
    ///
    /// Deux entrées distinctes, volontairement :
    /// - « pilotée » : suit l'animation du coup en cours, et revient à zéro avec elle ;
    /// - « impulsion » : un à-coup ponctuel à l'impact, qui s'amortit tout seul.
    ///
    /// Les deux passent par un RESSORT à amortissement critique, jamais par une valeur posée
    /// d'un coup. C'était la cause des « coupures » en combat : chaque coup porté ajoutait
    /// plusieurs degrés à l'angle de la caméra en UNE image, puis les reprenait en décroissant.
    /// Une vue qui change de 3° entre deux images ne se lit pas comme un choc mais comme un
    /// saut d'image. Avec le ressort, l'impulsion devient une VITESSE : la vue part en ~50 ms,
    /// culmine, et revient sans jamais dépasser — le choc se sent, rien ne saute.
    /// </summary>
    [DefaultExecutionOrder(60)]
    public class CameraPunch : MonoBehaviour
    {
        [SerializeField, Min(1f)]
        [Tooltip("Raideur du ressort (rad/s). Plus haut = plus sec. Le pic d'un choc arrive en 1/raideur s.")]
        private float _stiffness = 16f;

        [SerializeField] private bool _enabled = true;

        [SerializeField, Range(0f, 1.5f)]
        [Tooltip("Part du mouvement de camera des coups (elan du buste, accompagnement du poing). " +
                 "La vue SUIT le geste : elle plonge avec le direct, s'enroule avec le crochet, " +
                 "se releve avec l'uppercut. Tout passe par le ressort : ca glisse, ca ne saute pas.")]
        private float _drivenScale = 1f;

        [SerializeField, Range(0f, 1f)]
        [Tooltip("Part des chocs (coup recu, coup porte) appliquee a la camera.")]
        private float _impulseScale = 0.6f;

        [SerializeField, Min(0f)]
        [Tooltip("Angle maximal que la camera peut prendre, en degres, quoi qu'il arrive.")]
        private float _maxAngle = 11f;

        [SerializeField, Min(0f)]
        [Tooltip("Deplacement maximal de la camera, en metres.")]
        private float _maxOffset = 0.09f;

        [Header("Suivi du poing")]
        [SerializeField, Min(0f)]
        [Tooltip("Roulis par m/s de vitesse laterale du poing : la vue s'enroule dans le crochet.")]
        private float _rollPerSpeed = 1.3f;

        [SerializeField, Min(0f)]
        [Tooltip("Lacet par m/s de vitesse laterale du poing : le regard accompagne la trajectoire.")]
        private float _yawPerSpeed = 0.55f;

        [SerializeField, Min(0f)]
        [Tooltip("Tangage par m/s de vitesse verticale : l'uppercut releve la vue.")]
        private float _pitchPerSpeed = 0.5f;

        [Header("Champ de vision")]
        [SerializeField, Min(1f)] private float _fovStiffness = 14f;
        [SerializeField, Min(0f)] private float _maxFovKick = 14f;

        private Vector3 _drivenPosition;
        private Vector3 _drivenEuler;

        private Vector3 _position;
        private Vector3 _positionVelocity;
        private Vector3 _euler;
        private Vector3 _eulerVelocity;

        private Vector3 _strikeEuler;
        private Camera _camera;
        private float _fov;
        private float _fovVelocity;
        private float _fovHold;
        private float _fovHoldTimer;
        private float _lastWrittenFov = -1f;
        private float _baseFov = -1f;

        /// <summary>Suivi continu de l'animation d'attaque. À appeler chaque frame pendant le coup.</summary>
        public void SetDriven(Vector3 position, Vector3 euler)
        {
            _drivenPosition = position * _drivenScale;
            _drivenEuler = euler * _drivenScale;
        }

        /// <summary>
        /// Vitesse du poing qui frappe, dans le repère de la vue (m/s). La caméra la suit : un
        /// crochet qui traverse de droite à gauche enroule la vue, un uppercut la relève. Zéro
        /// quand aucun coup n'est en cours.
        /// </summary>
        public void SetStrikeMotion(Vector3 fistVelocity, float weight)
        {
            Vector3 v = fistVelocity * Mathf.Clamp01(weight);
            _strikeEuler = new Vector3(-v.y * _pitchPerSpeed, v.x * _yawPerSpeed, -v.x * _rollPerSpeed);
        }

        /// <summary>
        /// Coup de zoom : le champ se resserre de <paramref name="degrees"/> puis revient. Négatif
        /// = élargit (un coup reçu « souffle » la vue).
        /// </summary>
        public void KickFov(float degrees)
        {
            _fovVelocity -= degrees * 2.71828f * _fovStiffness;
        }

        /// <summary>
        /// Zoom tenu (contre, K.O.) : le champ se resserre et RESTE serré pendant
        /// <paramref name="seconds"/>, puis se relâche par le ressort.
        /// </summary>
        public void HoldFov(float degrees, float seconds)
        {
            _fovHold = -Mathf.Abs(degrees);
            _fovHoldTimer = seconds;
        }

        /// <summary>
        /// À-coup ponctuel, au contact. Les valeurs sont les PICS voulus (mètres, degrés) : elles
        /// sont converties en vitesse initiale du ressort, pour que la vue y monte en douceur.
        /// </summary>
        public void AddImpulse(Vector3 position, Vector3 euler)
        {
            // Ressort critique lancé à la vitesse v : x(t) = v·t·e^(-ωt), dont le pic vaut v/(e·ω).
            float toVelocity = 2.71828f * _stiffness * _impulseScale;
            _positionVelocity += position * toVelocity;
            _eulerVelocity += euler * toVelocity;
        }

        /// <summary>Remet la caméra au neutre (cinématique, changement de lieu).</summary>
        public void ResetState()
        {
            _drivenPosition = Vector3.zero;
            _drivenEuler = Vector3.zero;
            _position = Vector3.zero;
            _positionVelocity = Vector3.zero;
            _euler = Vector3.zero;
            _eulerVelocity = Vector3.zero;
            _strikeEuler = Vector3.zero;
            _fov = 0f;
            _fovVelocity = 0f;
            _fovHoldTimer = 0f;
            transform.localPosition = Vector3.zero;
            transform.localRotation = Quaternion.identity;
            ApplyFov();
        }

        private void LateUpdate()
        {
            // Temps non mis à l'échelle : le ralenti d'impact ne doit pas figer la caméra en
            // pleine course, sinon il la ferait « tenir » un angle pendant quelques images.
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);

            if (!_enabled)
            {
                ResetState();
                return;
            }

            Step(ref _position, ref _positionVelocity, _drivenPosition, dt);
            Step(ref _euler, ref _eulerVelocity, _drivenEuler + _strikeEuler, dt);

            _position = Vector3.ClampMagnitude(_position, _maxOffset);
            _euler = Vector3.ClampMagnitude(_euler, _maxAngle);

            transform.localPosition = _position;
            transform.localRotation = Quaternion.Euler(_euler);

            if (_fovHoldTimer > 0f) _fovHoldTimer -= dt;
            float fovTarget = _fovHoldTimer > 0f ? _fovHold : 0f;
            StepScalar(ref _fov, ref _fovVelocity, fovTarget, _fovStiffness, dt);
            _fov = Mathf.Clamp(_fov, -_maxFovKick, _maxFovKick);
            ApplyFov();
        }

        /// <summary>
        /// Le champ de vision est aussi réglé par le menu (curseur FOV) et par l'appareil photo
        /// du téléphone. On n'écrase donc jamais leur valeur : si elle a changé depuis notre
        /// dernière écriture, elle devient la nouvelle base, et on n'y ajoute que notre écart.
        /// </summary>
        private void ApplyFov()
        {
            if (_camera == null) _camera = GetComponentInChildren<Camera>();
            if (_camera == null) return;

            float current = _camera.fieldOfView;
            if (_baseFov < 0f || Mathf.Abs(current - _lastWrittenFov) > 0.001f) _baseFov = current;

            float value = Mathf.Clamp(_baseFov + _fov, 20f, 130f);
            _camera.fieldOfView = value;
            _lastWrittenFov = value;
        }

        private static void StepScalar(ref float value, ref float velocity, float target, float stiffness, float dt)
        {
            float offset = value - target;
            float decay = Mathf.Exp(-stiffness * dt);
            float temp = (velocity + offset * stiffness) * dt;

            velocity = (velocity - temp * stiffness) * decay;
            value = target + (offset + temp) * decay;
        }

        /// <summary>
        /// Pas exact d'un ressort à amortissement critique vers <paramref name="target"/> :
        /// stable quel que soit le pas de temps, donc aussi à 20 images par seconde.
        /// </summary>
        private void Step(ref Vector3 value, ref Vector3 velocity, Vector3 target, float dt)
        {
            Vector3 offset = value - target;
            float decay = Mathf.Exp(-_stiffness * dt);
            Vector3 temp = (velocity + offset * _stiffness) * dt;

            velocity = (velocity - temp * _stiffness) * decay;
            value = target + (offset + temp) * decay;
        }
    }
}
