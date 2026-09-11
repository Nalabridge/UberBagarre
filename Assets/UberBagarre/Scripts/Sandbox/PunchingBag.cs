using UberBagarre.Combat;
using UnityEngine;

namespace UberBagarre.Sandbox
{
    /// <summary>
    /// Sac de frappe : une cible qui encaisse et qui bouge.
    ///
    /// Sans cible, impossible de juger un coup — on frappe dans le vide et on ne sait pas si
    /// la fenêtre d'impact fonctionne. Le sac est la brique de test de tout le système de combat,
    /// avant que l'ennemi n'existe.
    ///
    /// Le balancement est un pendule simple : un ressort de rappel vers la verticale plus un
    /// amortissement. Pas de physique Rigidbody, donc rien à régler dans le moteur physique et
    /// un comportement identique à chaque fois — ce qu'on veut d'un banc de test.
    /// </summary>
    public class PunchingBag : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private HealthSystem _health;

        [SerializeField]
        [Tooltip("Pivot du haut. C'est lui qui tourne ; le sac pend en dessous.")]
        private Transform _pivot;

        [Header("Balancement")]
        [SerializeField, Min(0.1f)] private float _stiffness = 26f;
        [SerializeField, Min(0f)] private float _damping = 3.1f;
        [SerializeField, Min(0f)] private float _impulseScale = 22f;
        [SerializeField, Min(1f)] private float _maxAngle = 38f;

        private Vector2 _angle;
        private Vector2 _angularVelocity;

        private void Awake()
        {
            if (_health == null) _health = GetComponent<HealthSystem>();
            if (_pivot == null) _pivot = transform;
        }

        private void OnEnable()
        {
            if (_health != null) _health.Damaged += OnDamaged;
        }

        private void OnDisable()
        {
            if (_health != null) _health.Damaged -= OnDamaged;
        }

        private void OnDamaged(DamageInfo info)
        {
            Vector3 local = _pivot.InverseTransformDirection(info.Direction.normalized);
            float force = Mathf.Max(0.5f, info.ImpactForce) * _impulseScale;

            // Pousse vers l'avant : le bas du sac part en arriere, donc tangage negatif.
            _angularVelocity.x -= local.z * force;
            _angularVelocity.y += local.x * force;
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f) return;

            _angularVelocity += -_stiffness * _angle * dt;
            _angularVelocity -= _angularVelocity * Mathf.Min(1f, _damping * dt);
            _angle += _angularVelocity * dt;

            _angle.x = Mathf.Clamp(_angle.x, -_maxAngle, _maxAngle);
            _angle.y = Mathf.Clamp(_angle.y, -_maxAngle, _maxAngle);

            _pivot.localRotation = Quaternion.Euler(_angle.x, 0f, _angle.y);
        }
    }
}
