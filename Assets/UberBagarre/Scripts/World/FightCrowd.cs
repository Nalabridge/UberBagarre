using System.Collections.Generic;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La foule qui se forme autour d'une bagarre de rue.
    ///
    /// Dans la vraie vie, une bagarre n'a pas de public au premier coup : il arrive. Deux
    /// types s'arrêtent, puis cinq, puis un cercle — et c'est ce cercle qui dit « il se passe
    /// quelque chose ici ». Les badauds partent donc de loin, chacun à son heure, marchent
    /// jusqu'à une place autour du combat, s'arrêtent, se tournent vers l'action, et à partir
    /// de là réagissent aux coups comme le public du club (<see cref="Spectator"/>).
    ///
    /// Les places sont tirées autour du centre du combat et vérifiées : pas dans un mur, pas
    /// dans une voiture. Le trajet aussi : un badaud qui traverserait une camionnette pour
    /// rejoindre sa place ferait plus de dégâts à la scène que son absence.
    /// </summary>
    [DisallowMultipleComponent]
    public class FightCrowd : MonoBehaviour
    {
        private class Walker
        {
            public Spectator Spectator;
            public ProceduralLocomotion Locomotion;
            public Collider Body;
            public Vector3 From;
            public Vector3 To;
            public float Speed;
            public float StartAt;
            public bool Started;
            public bool Arrived;
        }

        [SerializeField] private Spectator[] _members = new Spectator[0];

        [SerializeField]
        [Tooltip("Optionnel : le brouhaha et les clameurs. Allume a la premiere arrivee.")]
        private CrowdAudio _audio;

        [Header("Cercle")]
        [SerializeField] private Vector2 _radius = new Vector2(3.6f, 5f);

        [SerializeField, Min(3f)]
        [Tooltip("Distance d'ou partent les badauds.")]
        private float _spawnDistance = 13f;

        [SerializeField] private Vector2 _speed = new Vector2(1.05f, 1.55f);

        [SerializeField]
        [Tooltip("Delai de depart, tire au hasard pour chacun : la foule se forme, elle n'apparait pas.")]
        private Vector2 _delay = new Vector2(0.2f, 7f);

        private readonly List<Walker> _walkers = new List<Walker>(16);
        private readonly RaycastHit[] _hits = new RaycastHit[8];
        private Transform _center;
        private float _clock;
        private bool _gathered;

        public bool Gathered
        {
            get { return _gathered; }
        }

        private void Awake()
        {
            GameObject center = new GameObject("Centre du combat");
            center.transform.SetParent(transform, false);
            _center = center.transform;

            for (int i = 0; i < _members.Length; i++)
            {
                if (_members[i] != null) _members[i].gameObject.SetActive(false);
            }

            if (_audio != null) _audio.enabled = false;
        }

        /// <summary>Fait venir les badauds autour de <paramref name="center"/>.</summary>
        public void Gather(Vector3 center)
        {
            // Tout le groupe (badauds, brouhaha) se recentre sur le combat.
            transform.position = center;
            _center.position = center;

            _walkers.Clear();
            _clock = 0f;
            _gathered = true;

            int count = _members.Length;
            float offset = Random.value * 360f;

            for (int i = 0; i < count; i++)
            {
                Spectator member = _members[i];
                if (member == null) continue;

                float angle = offset + i * 360f / Mathf.Max(1, count) + Random.Range(-12f, 12f);

                Vector3 slot;
                if (!FindSlot(center, angle, out slot)) continue;

                Walker walker = new Walker();
                walker.Spectator = member;
                walker.Locomotion = member.GetComponentInChildren<ProceduralLocomotion>();
                walker.Body = member.GetComponent<Collider>();
                walker.To = slot;
                walker.From = FindStart(center, slot);
                walker.Speed = Random.Range(_speed.x, _speed.y);
                walker.StartAt = Random.Range(_delay.x, _delay.y);

                member.enabled = false;
                member.Focus = _center;
                member.gameObject.SetActive(false);

                _walkers.Add(walker);
            }
        }

        /// <summary>Renvoie tout le monde (fin de chapitre, retour à la maison).</summary>
        public void Disperse()
        {
            _gathered = false;
            _walkers.Clear();

            for (int i = 0; i < _members.Length; i++)
            {
                if (_members[i] != null) _members[i].gameObject.SetActive(false);
            }

            if (_audio != null) _audio.enabled = false;
        }

        private void Update()
        {
            if (_walkers.Count == 0) return;

            float dt = Time.deltaTime;
            _clock += dt;

            for (int i = 0; i < _walkers.Count; i++)
            {
                Walker walker = _walkers[i];
                if (walker.Arrived || _clock < walker.StartAt) continue;

                Transform body = walker.Spectator.transform;

                if (!walker.Started)
                {
                    walker.Started = true;
                    if (walker.Body != null) walker.Body.enabled = false;
                    body.position = Ground(walker.From);
                    body.rotation = Quaternion.LookRotation(Flat(walker.To - walker.From), Vector3.up);
                    walker.Spectator.gameObject.SetActive(true);
                }

                Vector3 toGoal = Flat(walker.To - body.position);
                float remaining = toGoal.magnitude;

                // Il ralentit en arrivant : personne ne s'arrete net au milieu d'un pas.
                float speed = walker.Speed * Mathf.Clamp01(remaining / 0.8f + 0.25f);
                Vector3 velocity = remaining > 0.001f ? toGoal / remaining * speed : Vector3.zero;

                if (remaining < 0.12f)
                {
                    Arrive(walker);
                    continue;
                }

                body.position = Ground(body.position + velocity * dt);
                body.rotation = Quaternion.RotateTowards(body.rotation,
                    Quaternion.LookRotation(toGoal / remaining, Vector3.up), 240f * dt);

                if (walker.Locomotion != null) walker.Locomotion.SetState(velocity, true, 0f, 0f);
            }
        }

        private void Arrive(Walker walker)
        {
            walker.Arrived = true;

            if (walker.Locomotion != null) walker.Locomotion.SetState(Vector3.zero, true, 0f, 0f);
            if (walker.Body != null) walker.Body.enabled = true;

            walker.Spectator.enabled = true;
            walker.Spectator.Cheer(0.35f, 0.8f);

            // Le brouhaha nait avec le premier arrive.
            if (_audio != null && !_audio.enabled) _audio.enabled = true;
        }

        // --------------------------------------------------------------- places et trajets

        private bool FindSlot(Vector3 center, float angle, out Vector3 slot)
        {
            for (int attempt = 0; attempt < 6; attempt++)
            {
                float a = (angle + (attempt % 2 == 0 ? 1f : -1f) * attempt * 9f) * Mathf.Deg2Rad;
                float radius = Random.Range(_radius.x, _radius.y) - attempt * 0.25f;
                Vector3 direction = new Vector3(Mathf.Sin(a), 0f, Mathf.Cos(a));

                slot = center + direction * radius;
                if (Clear(center + Vector3.up, slot + Vector3.up) && Free(slot)) return true;
            }

            slot = center;
            return false;
        }

        private Vector3 FindStart(Vector3 center, Vector3 slot)
        {
            Vector3 outward = Flat(slot - center);
            outward = outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward;

            // D'abord tout droit derriere sa place ; sinon on tourne un peu ; sinon il arrive de pres.
            float[] turns = { 0f, 25f, -25f, 50f, -50f };
            for (int i = 0; i < turns.Length; i++)
            {
                Vector3 direction = Quaternion.Euler(0f, turns[i], 0f) * outward;
                Vector3 start = slot + direction * (_spawnDistance - Random.Range(0f, 3f));
                if (Clear(start + Vector3.up, slot + Vector3.up) && Free(start)) return start;
            }

            return slot + outward * 2.5f;
        }

        /// <summary>Rien de solide entre deux points (les personnages ne comptent pas).</summary>
        private bool Clear(Vector3 from, Vector3 to)
        {
            Vector3 direction = to - from;
            float length = direction.magnitude;
            if (length < 0.01f) return true;

            int count = Physics.SphereCastNonAlloc(from, 0.25f, direction / length, _hits, length, ~0,
                QueryTriggerInteraction.Ignore);

            for (int i = 0; i < count; i++)
            {
                if (!IsCharacter(_hits[i].collider)) return false;
            }

            return true;
        }

        private static bool Free(Vector3 point)
        {
            Collider[] overlaps = Physics.OverlapSphere(point + Vector3.up * 1f, 0.35f, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < overlaps.Length; i++)
            {
                if (!IsCharacter(overlaps[i])) return false;
            }

            return true;
        }

        private static bool IsCharacter(Collider collider)
        {
            if (collider == null) return true;
            if (collider is CharacterController) return true;
            return collider.GetComponentInParent<Spectator>() != null;
        }

        /// <summary>Pose un point sur le sol (trottoir, chaussée, bordure).</summary>
        private Vector3 Ground(Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 1.2f, Vector3.down, _hits, 3f, ~0,
                QueryTriggerInteraction.Ignore);

            float best = float.MaxValue;
            float y = point.y;

            for (int i = 0; i < count; i++)
            {
                if (IsCharacter(_hits[i].collider)) continue;
                if (_hits[i].distance >= best) continue;

                best = _hits[i].distance;
                y = _hits[i].point.y;
            }

            return new Vector3(point.x, y, point.z);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }
    }
}
