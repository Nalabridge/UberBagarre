using System.Collections;
using UberBagarre.Combat;
using UberBagarre.Core;
using UberBagarre.Enemy;
using UberBagarre.Story;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Un passant qu'on peut provoquer (E : « T'as un problème ? ») ou frapper sans raison.
    ///
    /// Chacun a son tempérament : le timide s'enfuit, le râleur répond et passe son chemin, le
    /// sanguin se met en garde. Frappé, presque tout le monde rend les coups — sauf les plus
    /// craintifs, qui détalent. Quand il se bat, le passant devient un vrai combattant (un
    /// modèle de la même silhouette, habillé de ses propres vêtements) ; K.O., on peut fouiller
    /// ses poches. Tout ça se signale à la police : une agression reste une agression.
    ///
    /// Plus tard, loin des regards, il se relève et reprend sa promenade.
    /// </summary>
    [RequireComponent(typeof(Interactable))]
    public class Passerby : MonoBehaviour
    {
        [SerializeField] private MocapWalker _walker;
        [SerializeField] private GameObject _fighterTemplate;
        [SerializeField] private Material _top;
        [SerializeField] private Material _pants;
        [SerializeField] private string _displayName = "Passant";
        [SerializeField, Range(0f, 1f)] private float _temper = 0.5f;

        /// <summary>Le nom des matériaux que les modèles de passants portent à la place des vrais vêtements.</summary>
        public const string TopMarker = "M_Rue_Haut";
        public const string PantsMarker = "M_Rue_Bas";

        private static readonly string[] Insults =
        {
            "T'as un problème, toi ?", "Qu'est-ce que tu regardes ?", "Dégage de mon trottoir.",
            "Tu veux ma photo ?", "Belle veste. Tu l'as volée à ta grand-mère ?", "Pousse-toi, gros."
        };

        private static readonly string[] Timid = { "Laissez-moi tranquille !", "J'appelle la police !", "Au secours !" };
        private static readonly string[] Grumpy = { "Va te faire voir.", "Pauvre type.", "T'as rien d'autre à faire ?" };
        private static readonly string[] Angry = { "Répète un peu, pour voir ?", "Tu l'auras cherché.", "Viens là, toi." };

        private Interactable _interactable;
        private HealthSystem _health;
        private int _provoked;
        private float _cooldown;
        private bool _fighting;

        public string DisplayName { get { return _displayName; } }

        private void Awake()
        {
            _interactable = GetComponent<Interactable>();
            _health = GetComponent<HealthSystem>();
            if (_walker == null) _walker = GetComponent<MocapWalker>();
        }

        private void OnEnable()
        {
            if (_interactable != null) _interactable.Activated += OnProvoked;
            if (_health != null) _health.Damaged += OnHit;
        }

        private void OnDisable()
        {
            if (_interactable != null) _interactable.Activated -= OnProvoked;
            if (_health != null) _health.Damaged -= OnHit;
        }

        // ------------------------------------------------------------------ provoquer

        private void OnProvoked(Interactable source)
        {
            if (_fighting || Time.time < _cooldown || (_walker != null && _walker.IsDown)) return;
            _cooldown = Time.time + 2.5f;

            // Il était en train d'appeler la police : on va lui « parler ».
            if (PoliceSystem.Instance != null && _walker != null && PoliceSystem.Instance.Intimidate(_walker)) return;
            _provoked++;

            Say("MOI", Insults[Mathf.Abs(_provoked * 7 + GetInstanceID()) % Insults.Length]);

            bool fights = _fighterTemplate != null && (_temper > 0.62f || (_provoked >= 2 && _temper > 0.35f));
            if (fights)
            {
                StartCoroutine(SquareUp(Angry[Mathf.Abs(GetInstanceID()) % Angry.Length], 0f));
                return;
            }

            if (_temper > 0.35f)
            {
                Say(_displayName, Grumpy[Mathf.Abs(_provoked + GetInstanceID()) % Grumpy.Length], 0.9f);
                if (_walker != null) _walker.Hurry(6f);
                return;
            }

            Flee();
        }

        // ------------------------------------------------------------------ frappé

        private void OnHit(DamageInfo info)
        {
            if (_fighting || info.AttackerFaction != Faction.Player) return;

            Crimes.Report(Crime.Agression, transform.position, gameObject);

            if (_fighterTemplate != null && _temper > 0.25f)
            {
                StartCoroutine(SquareUp(Angry[Mathf.Abs(GetInstanceID() + 1) % Angry.Length], info.Amount));
                return;
            }

            // Les plus craintifs tombent (ou trébuchent) et s'enfuient.
            if (_walker != null) _walker.KnockOver(info.Direction * 4f);
            Flee();
        }

        private void Flee()
        {
            Say(_displayName, Timid[Mathf.Abs(GetInstanceID()) % Timid.Length], 0.5f);
            if (_walker != null) _walker.Hurry(15f);
        }

        // ------------------------------------------------------------------ bagarre

        /// <summary>Il se met en garde : un vrai combattant prend sa place, avec ses vêtements.</summary>
        private IEnumerator SquareUp(string line, float damage)
        {
            _fighting = true;
            Say(_displayName, line, 0.4f);
            yield return new WaitForSeconds(0.35f);

            Transform viewer = SwingDoor.Viewer;
            Vector3 position = transform.position;
            Vector3 toPlayer = viewer != null ? viewer.position - position : transform.forward;
            toPlayer.y = 0f;
            Quaternion rotation = toPlayer.sqrMagnitude > 0.01f ? Quaternion.LookRotation(toPlayer.normalized) : transform.rotation;

            GameObject fighter = Instantiate(_fighterTemplate, position, rotation, transform.parent);
            fighter.name = _displayName + " (bagarre)";
            Dress(fighter);
            fighter.SetActive(true);

            ISpawnReceiver[] receivers = fighter.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnSpawned(position, rotation);

            Combatant combatant = fighter.GetComponent<Combatant>();
            if (combatant != null)
            {
                combatant.SetDisplayName(_displayName);
                if (fighter.GetComponent<TargetInjuries>() == null) fighter.AddComponent<TargetInjuries>();
                if (damage > 0f && combatant.Health != null) combatant.Health.ApplyDamage(new DamageInfo { Amount = damage, Zone = HitZone.Body });
            }

            EnemyBrain brain = fighter.GetComponent<EnemyBrain>();
            if (brain != null)
            {
                brain.ApplyDifficulty(Mathf.Lerp(0.15f, 0.45f, _temper));
                brain.enabled = true;
            }

            // Une bagarre en pleine rue, c'est aussi pour la police.
            Crimes.Report(Crime.Bagarre, position, fighter);

            StreetBrawls.Watch(this, fighter, combatant);
            gameObject.SetActive(false);
        }

        /// <summary>Le modèle porte des vêtements « marqueurs » : on y met ceux du passant.</summary>
        private void Dress(GameObject fighter)
        {
            Renderer[] renderers = fighter.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Material[] materials = renderers[r].sharedMaterials;
                bool changed = false;
                for (int m = 0; m < materials.Length; m++)
                {
                    if (materials[m] == null) continue;
                    if (_top != null && materials[m].name.StartsWith(TopMarker)) { materials[m] = _top; changed = true; }
                    else if (_pants != null && materials[m].name.StartsWith(PantsMarker)) { materials[m] = _pants; changed = true; }
                }

                if (changed) renderers[r].sharedMaterials = materials;
            }
        }

        /// <summary>La bagarre est finie (et on ne regarde plus) : il reprend sa promenade.</summary>
        public void Return()
        {
            _fighting = false;
            _provoked = 0;
            if (_health != null) _health.Heal(_health.MaxHealth);
            gameObject.SetActive(true);
            if (_walker != null) _walker.Hurry(20f);
        }

        private static void Say(string speaker, string line, float delay = 0f)
        {
            SubtitleDisplay subtitles = StreetBrawls.Subtitles;
            if (subtitles == null) return;
            subtitles.Play(DialogueLine.Say(speaker, line));
        }

        public void Configure(MocapWalker walker, GameObject template, Material top, Material pants, string displayName, float temper)
        {
            _walker = walker;
            _fighterTemplate = template;
            _top = top;
            _pants = pants;
            _displayName = displayName;
            _temper = temper;
        }
    }

    /// <summary>
    /// Les bagarres de rue en cours : on surveille le combattant, on propose de fouiller ses
    /// poches quand il est K.O., et on rend le passant à la rue quand plus personne ne regarde.
    /// </summary>
    public class StreetBrawls : MonoBehaviour
    {
        private class Brawl
        {
            public Passerby passerby;
            public GameObject fighter;
            public Combatant combatant;
            public float downSince = -1f;
            public bool searched;
            public Interactable pockets;
        }

        private static StreetBrawls _instance;
        private readonly System.Collections.Generic.List<Brawl> _brawls = new System.Collections.Generic.List<Brawl>();
        private SubtitleDisplay _subtitles;

        public static SubtitleDisplay Subtitles
        {
            get
            {
                if (_instance == null) Ensure();
                if (_instance._subtitles == null) _instance._subtitles = FindAnyObjectByType<SubtitleDisplay>();
                return _instance._subtitles;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private static void Ensure()
        {
            if (_instance != null) return;
            _instance = new GameObject("Bagarres de rue").AddComponent<StreetBrawls>();
        }

        public static void Watch(Passerby passerby, GameObject fighter, Combatant combatant)
        {
            Ensure();
            _instance._brawls.Add(new Brawl { passerby = passerby, fighter = fighter, combatant = combatant });
        }

        private void Update()
        {
            Transform viewer = SwingDoor.Viewer != null ? SwingDoor.Viewer : Camera.main != null ? Camera.main.transform : null;

            for (int i = _brawls.Count - 1; i >= 0; i--)
            {
                Brawl b = _brawls[i];
                if (b.fighter == null)
                {
                    if (b.passerby != null) b.passerby.Return();
                    _brawls.RemoveAt(i);
                    continue;
                }

                bool down = b.combatant != null && !b.combatant.IsAlive;
                if (down && b.downSince < 0f)
                {
                    b.downSince = Time.time;
                    OfferPockets(b);
                }

                float distance = viewer != null ? Vector3.Distance(viewer.position, b.fighter.transform.position) : 0f;
                bool gone = distance > 45f || (down && Time.time - b.downSince > 60f && distance > 15f);
                if (!gone) continue;

                Destroy(b.fighter);
                if (b.passerby != null) b.passerby.Return();
                _brawls.RemoveAt(i);
            }
        }

        /// <summary>K.O. : on peut lui faire les poches (quelques euros, et un délit de plus).</summary>
        private void OfferPockets(Brawl b)
        {
            Interactable pockets = b.fighter.GetComponent<Interactable>();
            if (pockets == null) pockets = b.fighter.AddComponent<Interactable>();
            pockets.Label = "Fouiller ses poches";
            pockets.Hint = b.passerby != null ? b.passerby.DisplayName : null;
            pockets.Range = 2.4f;
            pockets.SetAvailable(true);
            b.pockets = pockets;
            pockets.Activated += source =>
            {
                if (b.searched) return;
                b.searched = true;
                pockets.SetAvailable(false);

                int money = Random.Range(3, 46);
                PlayerProgress progress = FindAnyObjectByType<PlayerProgress>();
                if (progress != null) progress.AddMoney(money, "Poches d'un passant");
                Crimes.Report(Crime.Agression, b.fighter.transform.position, b.fighter);
                if (Subtitles != null) Subtitles.Play(DialogueLine.Say("", money + " €, un ticket de bus et un briquet."));
            };
        }
    }
}
