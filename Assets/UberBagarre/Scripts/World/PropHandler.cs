using UberBagarre.Combat;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Ramasser un objet et le lancer : la bouteille, le plot, la chaise.
    ///
    /// C'est la mécanique qui transforme le décor en arsenal, et c'est la plus « bagarre de
    /// rue » de toutes. Une bouteille ramassée sur le trottoir et lancée à la tête de quelqu'un
    /// raconte plus sur le personnage que dix combos : il se bat avec ce qu'il trouve.
    ///
    /// Trois règles :
    ///
    /// - **On ne ramasse que ce qu'on peut porter d'une main** (masse plafonnée). Une caisse
    ///   pleine ou un présentoir de journaux ne se ramassent pas, ils se bousculent.
    /// - **Tenir un objet coupe les poings**, exactement comme tenir son téléphone : le clic
    ///   gauche lance au lieu de frapper. Sinon le joueur lâcherait sa bouteille en voulant
    ///   la lancer, ou frapperait avec une main qui la tient.
    /// - **Le lancer suit le regard**, avec une petite montée : sans elle, un objet lancé à
    ///   l'horizontale touche le sol à trois mètres, et le joueur conclut que le lancer est
    ///   cassé.
    ///
    /// Les dégâts ne sont pas calculés ici : l'objet lancé les applique lui-même au contact,
    /// par une hurtbox de la victime — donc avec zone, garde et physique des os.
    /// </summary>
    [DisallowMultipleComponent]
    public class PropHandler : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private Camera _camera;

        [SerializeField]
        [Tooltip("Point de tenue, sous la camera : l'objet porte y est accroche.")]
        private Transform _holdPoint;

        [SerializeField]
        [Tooltip("Optionnel. Si l'interaction vise quelque chose (porte, voiture, courrier), E lui " +
                 "revient en priorite : ramasser une bouteille au lieu d'ouvrir la portiere serait " +
                 "exactement le genre d'erreur qui fait rater une sequence.")]
        private InteractionSystem _interaction;

        [SerializeField]
        [Tooltip("Optionnel. Telephone leve = on ne ramasse ni ne lance rien.")]
        private PhoneDevice _phone;

        [SerializeField] private Faction _faction = Faction.Player;

        [Header("Prise")]
        [SerializeField, Min(0.5f)] private float _reach = 2.3f;
        [SerializeField, Min(0.1f)] private float _maxMass = 4.5f;
        [SerializeField, Min(0f)] private float _sweepRadius = 0.18f;

        [Header("Lancer")]
        [SerializeField, Min(1f)] private float _throwSpeed = 17f;
        [SerializeField, Min(0f)] private float _throwLift = 1.6f;
        [SerializeField, Min(0f)] private float _glassBonusDamage = 7f;

        [Header("Affichage")]
        [SerializeField] private Color _accent = new Color(0.98f, 0.86f, 0.42f);

        private readonly RaycastHit[] _hits = new RaycastHit[12];

        private PhysicsProp _looked;
        private PhysicsProp _held;
        private Transform _heldParent;
        private Collider[] _heldColliders;

        public PhysicsProp Held { get { return _held; } }
        public bool IsHolding { get { return _held != null; } }

        /// <summary>Nombre d'objets lancés qui ont touché quelqu'un. Sert au tutoriel de l'histoire.</summary>
        public int ThrowHits { get; private set; }

        /// <summary>Nombre d'objets lancés, touchés ou non.</summary>
        public int Throws { get; private set; }

        private void OnEnable()
        {
            PhysicsProp.AnyHitCombatant += OnPropHit;
        }

        private void OnDisable()
        {
            PhysicsProp.AnyHitCombatant -= OnPropHit;

            if (_held != null) Release(Vector3.zero, false);
            if (_input != null) _input.SetCombatLock(this, false);
        }

        private void Update()
        {
            if (_input == null) return;

            bool phoneUp = _phone != null && _phone.IsRaised;

            // L'objet tenu peut avoir été détruit entre-temps (verre éclaté, décor rechargé).
            // Le test « != null » d'Unity le verrait comme absent sans que la référence C# soit
            // vidée : sans ce nettoyage explicite, le verrou de combat resterait posé pour
            // toujours et le joueur ne pourrait plus jamais frapper.
            if (!ReferenceEquals(_held, null) && (_held == null || _held.IsShattered))
            {
                ClearHeld();
            }

            if (_held != null)
            {
                if (!phoneUp && ThrowPressed())
                {
                    Throw();
                    return;
                }

                if (_input.InteractPressed) Release(Vector3.zero, true);
                return;
            }

            _looked = phoneUp ? null : FindLooked();

            if (_looked == null || !_input.InteractPressed) return;
            if (_interaction != null && _interaction.Focused != null) return;

            Pick(_looked);
        }

        private void LateUpdate()
        {
            if (_held == null || _holdPoint == null) return;

            // Suivi dans LateUpdate, après la caméra : sinon l'objet tenu traîne d'une image
            // derrière le regard et semble flotter à côté de la main.
            _held.transform.SetPositionAndRotation(_holdPoint.position, _holdPoint.rotation);
        }

        private bool ThrowPressed()
        {
            // Lecture directe de la liaison : l'entrée de combat est verrouillée tant qu'on tient
            // quelque chose, donc StraightPressed arrive toujours vide ici.
            if (_input.Provider == null || _input.Bindings == null) return false;
            return _input.Provider.GetPressedThisFrame(_input.Bindings.attackStraight);
        }

        // ------------------------------------------------------------------ prise

        private PhysicsProp FindLooked()
        {
            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return null;

            Ray ray = new Ray(camera.transform.position, camera.transform.forward);
            int count = Physics.SphereCastNonAlloc(ray, _sweepRadius, _hits, _reach, ~0, QueryTriggerInteraction.Ignore);

            PhysicsProp best = null;
            float bestDistance = float.MaxValue;

            for (int i = 0; i < count; i++)
            {
                Rigidbody body = _hits[i].rigidbody;
                if (body == null) continue;

                PhysicsProp prop = body.GetComponent<PhysicsProp>();
                if (prop == null || prop.IsShattered || body.mass > _maxMass) continue;
                if (_hits[i].distance >= bestDistance) continue;

                bestDistance = _hits[i].distance;
                best = prop;
            }

            return best;
        }

        private void Pick(PhysicsProp prop)
        {
            Rigidbody body = prop.Body;
            if (body == null) return;

            _held = prop;
            _heldParent = prop.transform.parent;

            // Colliders coupés pendant la tenue : un objet cinématique qui traverse l'adversaire
            // ou un mur au bout du bras le pousserait, ou se coincerait dedans au lâcher.
            _heldColliders = prop.GetComponentsInChildren<Collider>();
            for (int i = 0; i < _heldColliders.Length; i++) _heldColliders[i].enabled = false;

            body.isKinematic = true;
            body.interpolation = RigidbodyInterpolation.None;

            _input.SetCombatLock(this, true);
        }

        private void Throw()
        {
            Camera camera = GuiKit.ActiveCamera(_camera);
            Vector3 forward = camera != null ? camera.transform.forward : transform.forward;
            Vector3 up = camera != null ? camera.transform.up : Vector3.up;

            PhysicsProp prop = _held;
            Rigidbody body = prop.Body;

            float mass = body != null ? body.mass : 1f;

            // Plus c'est lourd, moins ça part vite — et plus ça fait mal. Une bouteille file,
            // une chaise part en cloche.
            float speed = _throwSpeed / Mathf.Sqrt(Mathf.Max(0.4f, mass));
            speed = Mathf.Clamp(speed, 7f, _throwSpeed * 1.1f);

            float damage = Mathf.Clamp(7f + mass * 3f, 8f, 24f);
            if (prop.Kind == PhysicsProp.Matter.Verre) damage += _glassBonusDamage;

            Release(forward * speed + up * _throwLift, true);

            prop.Launch(gameObject, _faction, damage, 5f + mass * 1.5f);
            Throws++;

            if (body != null) body.angularVelocity = Random.insideUnitSphere * 14f;
        }

        private void Release(Vector3 velocity, bool restore)
        {
            PhysicsProp prop = _held;
            if (prop == null)
            {
                ClearHeld();
                return;
            }

            prop.transform.SetParent(_heldParent, true);

            if (_heldColliders != null)
            {
                for (int i = 0; i < _heldColliders.Length; i++)
                {
                    if (_heldColliders[i] != null) _heldColliders[i].enabled = true;
                }
            }

            Rigidbody body = prop.Body;

            if (body != null && restore)
            {
                body.isKinematic = false;
                body.interpolation = RigidbodyInterpolation.Interpolate;
                body.linearVelocity = velocity;
            }

            ClearHeld();
        }

        private void ClearHeld()
        {
            _held = null;
            _heldParent = null;
            _heldColliders = null;

            if (_input != null) _input.SetCombatLock(this, false);
        }

        private void OnPropHit(PhysicsProp prop, Combatant victim)
        {
            // Seuls les objets lancés par CE joueur comptent pour lui.
            if (prop == null || prop.Thrower != gameObject) return;
            if (victim != null && victim.gameObject != gameObject) ThrowHits++;
        }

        // ------------------------------------------------------------------ affichage

        private void OnGUI()
        {
            string label;
            string key;

            if (_held != null)
            {
                key = "CLIC";
                label = "Lancer   ·   E : lâcher";
            }
            else if (_looked != null && (_interaction == null || _interaction.Focused == null))
            {
                key = "E";
                label = "Ramasser (" + Describe(_looked) + ")";
            }
            else
            {
                return;
            }

            GUIStyle style = GuiKit.Style(14, FontStyle.Bold, TextAnchor.MiddleLeft);
            float width = style.CalcSize(new GUIContent(label)).x + 74f;

            float x = Screen.width * 0.5f - width * 0.5f;
            float y = Screen.height * 0.5f + 46f;

            GuiKit.Fill(new Rect(x, y, width, 34f), new Color(0f, 0f, 0f, 0.6f));

            Rect keyRect = new Rect(x + 8f, y + 7f, 44f, 20f);
            GuiKit.Fill(keyRect, new Color(1f, 1f, 1f, 0.12f));
            GuiKit.Outline(keyRect, 1.5f, _accent);
            GuiKit.OutlinedLabel(keyRect, key, GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter),
                _accent, new Color(0f, 0f, 0f, 0.85f), 1f);

            GuiKit.OutlinedLabel(new Rect(keyRect.xMax + 10f, y + 6f, width - 70f, 22f), label, style,
                Color.white, new Color(0f, 0f, 0f, 0.85f), 1.2f);
        }

        private static string Describe(PhysicsProp prop)
        {
            switch (prop.Kind)
            {
                case PhysicsProp.Matter.Verre: return "bouteille";
                case PhysicsProp.Matter.Plastique: return "plastique";
                case PhysicsProp.Matter.Metal: return "métal";
                case PhysicsProp.Matter.Mou: return "sac";
                default: return "bois";
            }
        }
    }
}
