using System;
using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Détection de coup pendant une fenêtre d'impact.
    ///
    /// Deux règles importantes :
    /// - la hitbox n'est active que pendant la fenêtre définie par l'attaque, jamais en permanence ;
    /// - chaque cible n'est comptée QU'UNE FOIS par attaque, sinon un coup qui balaye 6 frames
    ///   infligerait six fois ses dégâts.
    ///
    /// Détection par sphère plutôt que par collider physique : le poing traverse beaucoup de
    /// distance en une frame, et une sphère testée à chaque frame sur la position réelle du poing
    /// est plus fiable qu'un trigger qui peut passer entre deux images.
    /// </summary>
    public class Hitbox : MonoBehaviour
    {
        [SerializeField]
        [Tooltip("Point de detection : les articulations du poing.")]
        private Transform _origin;

        [SerializeField] private Faction _ownerFaction = Faction.Player;
        [SerializeField] private GameObject _owner;

        [Header("Debug")]
        [SerializeField] private bool _drawGizmo = true;

        private readonly List<object> _alreadyHit = new List<object>(8);
        private readonly Collider[] _overlapBuffer = new Collider[16];

        private bool _open;
        private float _radius;
        private DamageInfo _template;
        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;

        /// <summary>Émis à chaque cible touchée. Le retour d'impact s'y abonne.</summary>
        public event Action<Hurtbox, Vector3> Hit;

        public bool IsOpen { get { return _open; } }

        public Transform Origin
        {
            get { return _origin != null ? _origin : transform; }
        }

        /// <summary>Ouvre la fenêtre d'impact pour une attaque donnée.</summary>
        public void Open(DamageInfo template, float radius)
        {
            _template = template;
            _radius = Mathf.Max(0.01f, radius);
            _open = true;
            _alreadyHit.Clear();
            _hasPreviousPosition = false;
        }

        public void Close()
        {
            _open = false;
            _alreadyHit.Clear();
        }

        private void LateUpdate()
        {
            if (!_open) return;

            Vector3 position = Origin.position;

            // Un poing rapide peut franchir sa propre largeur en une frame : on teste aussi
            // le segment parcouru depuis la frame precedente, pas seulement la position finale.
            if (_hasPreviousPosition) TestSegment(_previousPosition, position);
            else TestSphere(position);

            _previousPosition = position;
            _hasPreviousPosition = true;
        }

        private void TestSegment(Vector3 from, Vector3 to)
        {
            float distance = Vector3.Distance(from, to);
            int steps = Mathf.Clamp(Mathf.CeilToInt(distance / (_radius * 0.8f)), 1, 6);

            for (int i = 1; i <= steps; i++)
            {
                TestSphere(Vector3.Lerp(from, to, i / (float)steps));
            }
        }

        private void TestSphere(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, _radius, _overlapBuffer, ~0, QueryTriggerInteraction.Collide);

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlapBuffer[i];
                if (collider == null) continue;

                Hurtbox hurtbox = collider.GetComponentInParent<Hurtbox>();
                if (hurtbox == null || hurtbox.Faction == _ownerFaction) continue;
                if (_alreadyHit.Contains(hurtbox.Owner)) continue;

                _alreadyHit.Add(hurtbox.Owner);

                DamageInfo info = _template;
                info.Point = collider.ClosestPoint(position);
                info.Attacker = _owner;
                info.AttackerFaction = _ownerFaction;
                hurtbox.Receive(info);

                Action<Hurtbox, Vector3> hit = Hit;
                if (hit != null) hit(hurtbox, info.Point);
            }
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmo || !_open) return;

            Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.7f);
            Gizmos.DrawWireSphere(Origin.position, _radius);
        }
    }
}
