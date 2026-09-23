using System;
using System.Collections.Generic;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.Combat
{
    /// <summary>
    /// Détection de coup pendant une fenêtre d'impact.
    ///
    /// Trois règles importantes :
    /// - la hitbox n'est active que pendant la fenêtre définie par l'attaque, jamais en permanence ;
    /// - chaque cible n'est comptée QU'UNE FOIS par attaque, sinon un coup qui balaye 6 frames
    ///   infligerait six fois ses dégâts ;
    /// - la zone touchée est celle que l'attaquant VISAIT, et seulement à défaut la plus proche
    ///   du point d'impact.
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

        // Meilleure zone retenue par cible, le temps d'un test de sphere.
        private readonly List<object> _candidateOwners = new List<object>(4);
        private readonly List<Hurtbox> _candidateZones = new List<Hurtbox>(4);
        private readonly List<Vector3> _candidatePoints = new List<Vector3>(4);
        private readonly List<float> _candidateDistances = new List<float>(4);

        private bool _open;
        private float _radius;
        private DamageInfo _template;
        private Vector3 _previousPosition;
        private bool _hasPreviousPosition;
        private Vector3 _velocity;

        [Header("Objets du decor")]
        [SerializeField, Min(0f)]
        [Tooltip("Impulsion transmise a un objet physique touche, par unite de force d'impact. " +
                 "C'est ce qui fait voler une bouteille ou basculer un plot quand le poing passe " +
                 "dedans : un decor qui ne reagit pas aux coups se lit comme un fond peint.")]
        private float _propImpulse = 0.9f;

        /// <summary>Émis à chaque cible touchée. Le retour d'impact s'y abonne.</summary>
        public event Action<Hurtbox, Vector3> Hit;

        public bool IsOpen { get { return _open; } }

        public Transform Origin
        {
            get { return _origin != null ? _origin : transform; }
        }

        /// <summary>
        /// Zone que l'attaquant visait au moment d'ouvrir le coup, ou null s'il ne visait personne.
        ///
        /// Elle prime sur la géométrie, et c'est le cœur du problème que ça résout. Choisir la
        /// zone « la plus proche du poing » paraît juste et ne l'est pas : une zone large rayonne
        /// plus loin qu'une zone petite, donc le torse (38 cm de rayon) gagnait contre la tête
        /// (18 cm) même quand le joueur visait franchement la tête. Autrement dit, plus une zone
        /// est grosse, plus elle volait les coups des autres — et le torse gagnait toujours.
        ///
        /// En vue première personne, la seule règle que le joueur puisse apprendre est « je touche
        /// là où je vise ». Le réticule décide donc, et la géométrie ne sert plus qu'à savoir SI
        /// le coup porte.
        /// </summary>
        public Hurtbox AimedZone { get; set; }

        /// <summary>Ouvre la fenêtre d'impact pour une attaque donnée.</summary>
        public void Open(DamageInfo template, float radius, Hurtbox aimedZone)
        {
            _template = template;
            _radius = Mathf.Max(0.01f, radius);
            AimedZone = aimedZone;
            _open = true;
            _alreadyHit.Clear();
            _hasPreviousPosition = false;
            _velocity = Vector3.zero;
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

            // La vitesse est mesuree AVANT le test : c'est elle qui voyage avec le coup et qui
            // decide dans quel sens le corps touche va partir. Le temps non mis a l'echelle
            // n'est pas voulu ici — pendant un ralenti d'impact, le poing avance moins et la
            // vitesse reelle du geste, rapportee au temps du jeu, reste la meme.
            float dt = Time.deltaTime;
            if (_hasPreviousPosition && dt > 0.0001f) _velocity = (position - _previousPosition) / dt;

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

        /// <summary>
        /// Teste une sphère et retient UNE zone par combattant touché : celle qui était visée si
        /// elle fait partie des candidates, sinon la plus proche du point d'impact.
        ///
        /// Un tri est indispensable dès qu'un combattant a plusieurs zones touchables qui se
        /// recouvrent — et elles se recouvrent forcément, parce qu'il ne doit pas exister de trou
        /// entre la cuisse et le bas du torse. Sans tri, c'est l'ORDRE de retour de
        /// Physics.OverlapSphere qui décidait : le même coup au même endroit pouvait compter
        /// comme jambe, corps ou tête d'une fois sur l'autre, sans qu'aucune erreur apparaisse.
        /// </summary>
        private void TestSphere(Vector3 position)
        {
            int count = Physics.OverlapSphereNonAlloc(position, _radius, _overlapBuffer, ~0, QueryTriggerInteraction.Collide);
            if (count <= 0) return;

            _candidateOwners.Clear();
            _candidateZones.Clear();
            _candidatePoints.Clear();
            _candidateDistances.Clear();

            for (int i = 0; i < count; i++)
            {
                Collider collider = _overlapBuffer[i];
                if (collider == null) continue;

                Hurtbox hurtbox = collider.GetComponentInParent<Hurtbox>();

                if (hurtbox == null)
                {
                    PushProp(collider, position);
                    continue;
                }

                if (hurtbox.Faction == _ownerFaction) continue;
                if (_alreadyHit.Contains(hurtbox.Owner)) continue;

                Vector3 point = collider.ClosestPoint(position);
                float distance = (point - position).sqrMagnitude;

                // La zone visee gagne quelle que soit la distance : c'est elle qui porte
                // l'intention du joueur. On lui donne une distance negative pour qu'aucune autre
                // ne puisse la remplacer.
                bool aimed = AimedZone != null && hurtbox == AimedZone;
                if (aimed) distance = -1f;

                int existing = _candidateOwners.IndexOf(hurtbox.Owner);

                if (existing < 0)
                {
                    _candidateOwners.Add(hurtbox.Owner);
                    _candidateZones.Add(hurtbox);
                    _candidatePoints.Add(point);
                    _candidateDistances.Add(distance);
                }
                else if (distance < _candidateDistances[existing])
                {
                    _candidateZones[existing] = hurtbox;
                    _candidatePoints[existing] = point;
                    _candidateDistances[existing] = distance;
                }
            }

            for (int i = 0; i < _candidateZones.Count; i++)
            {
                Hurtbox hurtbox = _candidateZones[i];
                _alreadyHit.Add(_candidateOwners[i]);

                DamageInfo info = _template;
                info.Point = _candidatePoints[i];
                info.Attacker = _owner;
                info.AttackerFaction = _ownerFaction;
                info.Velocity = _velocity;
                hurtbox.Receive(info);

                Action<Hurtbox, Vector3> hit = Hit;
                if (hit != null) hit(hurtbox, info.Point);
            }
        }

        /// <summary>
        /// Pousse un objet physique du décor traversé par le poing.
        ///
        /// Une seule impulsion par objet et par coup, comme pour les combattants : sans la liste
        /// des objets déjà touchés, un poing qui reste une demi-seconde dans une caisse lui
        /// appliquerait trente impulsions et l'enverrait en orbite.
        /// </summary>
        private void PushProp(Collider collider, Vector3 position)
        {
            Rigidbody body = collider.attachedRigidbody;
            if (body == null || body.isKinematic) return;
            if (_owner != null && body.transform.IsChildOf(_owner.transform)) return;
            if (_alreadyHit.Contains(body)) return;

            _alreadyHit.Add(body);

            Vector3 direction = _velocity.sqrMagnitude > 0.25f ? _velocity.normalized : _template.Direction.normalized;
            if (direction.sqrMagnitude < 0.0001f) direction = transform.forward;

            // Un peu de montée : un objet frappé ne glisse pas au sol, il décolle. Sans elle, une
            // bouteille touchée file à plat comme un palet.
            direction = (direction + Vector3.up * 0.25f).normalized;

            float impulse = _propImpulse * Mathf.Max(1f, _template.ImpactForce);
            Vector3 point = collider.ClosestPoint(position);

            body.AddForceAtPosition(direction * impulse, point, ForceMode.Impulse);

            PhysicsProp prop = body.GetComponent<PhysicsProp>();
            if (prop != null) prop.NotifyStruck(point, direction * impulse);
        }

        private void OnDrawGizmos()
        {
            if (!_drawGizmo || !_open) return;

            Gizmos.color = new Color(1f, 0.25f, 0.15f, 0.7f);
            Gizmos.DrawWireSphere(Origin.position, _radius);
        }
    }
}
