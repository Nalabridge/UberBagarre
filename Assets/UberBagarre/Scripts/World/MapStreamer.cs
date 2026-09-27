using System;
using System.Collections;
using System.Collections.Generic;
using UberBagarre.Core;
using UberBagarre.Story;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.World
{
    /// <summary>
    /// La ville vit dans sa propre scène (la carte convertie, dans Assets/Schedule1) : ce
    /// composant la charge par-dessus la scène du jeu dès le démarrage, puis la prépare.
    ///
    /// La carte d'origine arrive sans ses scripts. Ce qui en dépendait est refait ici :
    /// - les portes qu'on doit pouvoir franchir (la chambre du motel) deviennent des portes ;
    /// - la nuit, les lampadaires s'allument (leur lumière, et leur tête devient émissive),
    ///   quelques fenêtres aussi ; les lampes loin du joueur sont confiées au DistanceCuller ;
    /// - tombé à l'eau, le joueur est repêché sur le trottoir le plus proche.
    ///
    /// Tant que la ville n'est pas là, le joueur est tenu immobile : sans sol, il tomberait.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class MapStreamer : MonoBehaviour
    {
        [Serializable]
        public class MaterialSwap
        {
            public Material off;
            public Material on;

            [Range(0f, 1f)]
            [Tooltip("Part des objets concernes (1 = tous). Les fenetres ne s'allument pas toutes.")]
            public float share = 1f;
        }

        [SerializeField] private string _sceneName = "CarteSchedule1";

        [Header("Joueur")]
        [SerializeField] private Transform _player;

        [SerializeField]
        [Tooltip("Coupes tant que la ville n'est pas chargee (deplacement, gravite).")]
        private Behaviour[] _holdUntilLoaded = new Behaviour[0];

        [SerializeField] private CharacterController _controller;

        [Header("Eau")]
        [SerializeField] private float _waterLevel = -6.5f;
        [SerializeField] private ScreenFader _fader;
        [SerializeField] private SubtitleDisplay _subtitles;

        [SerializeField]
        [Tooltip("Points surs ou l'on repeche le joueur (les trottoirs).")]
        private Vector3[] _safePoints = new Vector3[0];

        [Header("Preparation")]
        [SerializeField] private DistanceCuller _culler;

        [SerializeField]
        [Tooltip("Chemins (dans la scene de la ville) des gonds de portes a rendre ouvrables.")]
        private string[] _doors = new string[0];

        [SerializeField] private float _doorAngle = -100f;

        [SerializeField] private MaterialSwap[] _nightSwaps = new MaterialSwap[0];

        [SerializeField]
        [Tooltip("Une lampe dont un parent porte un de ces noms est un lampadaire : allume la nuit.")]
        private string[] _lampParents = { "street light", "streetlight", "street lamp", "lamppost" };

        [SerializeField] private Color _lampColor = new Color(1f, 0.76f, 0.5f);
        [SerializeField, Min(0f)] private float _lampIntensity = 2.1f;
        [SerializeField, Min(1f)] private float _lampRange = 17f;
        [SerializeField, Min(0f)] private float _otherMaxIntensity = 1.4f;
        [SerializeField, Min(1f)] private float _otherMaxRange = 9f;

        private readonly List<Behaviour> _held = new List<Behaviour>();
        private bool _controllerHeld;
        private float _underwater;
        private bool _rescuing;

        private static Scene _city;
        private static readonly Dictionary<string, SwingDoor> Doors = new Dictionary<string, SwingDoor>();

        /// <summary>La ville est chargée et prête.</summary>
        public static bool Ready { get; private set; }

        /// <summary>La ville vient d'être chargée (une fois par partie).</summary>
        public static event Action Loaded;

        /// <summary>Hauteur de l'eau (la mer, le port).</summary>
        public float WaterLevel { get { return _waterLevel; } }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Ready = false;
            Loaded = null;
            _city = default(Scene);
            Doors.Clear();
        }

        /// <summary>Un objet de la ville, par son chemin (« @Properties/Manor/Manor Gate »).</summary>
        public static Transform FindInCity(string path)
        {
            if (!Ready || !_city.IsValid() || !_city.isLoaded) return null;
            return Find(_city.GetRootGameObjects(), path);
        }

        /// <summary>Une porte de la ville devenue vraie porte au chargement (null sinon).</summary>
        public static SwingDoor Door(string path)
        {
            SwingDoor door;
            return path != null && Doors.TryGetValue(path, out door) ? door : null;
        }

        private void Awake()
        {
            Ready = false;
            SceneManager.sceneLoaded += OnSceneLoaded;

            Scene existing = SceneManager.GetSceneByName(_sceneName);
            if (existing.IsValid() && existing.isLoaded)
            {
                Prepare(existing);
                return;
            }

            if (!Application.CanStreamedLevelBeLoaded(_sceneName))
            {
                Debug.LogWarning("[UberBagarre] La ville « " + _sceneName + " » n'est pas dans les Build Settings : " +
                                 "reconstruis le monde ouvert (menu Uber Bagarre).");
                return;
            }

            Hold(true);
            SceneManager.LoadScene(_sceneName, LoadSceneMode.Additive);
        }

        private void OnDestroy()
        {
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (scene.name != _sceneName) return;
            Prepare(scene);
        }

        private void Hold(bool hold)
        {
            if (hold)
            {
                _held.Clear();
                for (int i = 0; i < _holdUntilLoaded.Length; i++)
                {
                    Behaviour b = _holdUntilLoaded[i];
                    if (b == null || !b.enabled) continue;
                    b.enabled = false;
                    _held.Add(b);
                }

                if (_controller != null && _controller.enabled)
                {
                    _controller.enabled = false;
                    _controllerHeld = true;
                }

                return;
            }

            for (int i = 0; i < _held.Count; i++)
            {
                if (_held[i] != null) _held[i].enabled = true;
            }

            _held.Clear();
            if (_controllerHeld && _controller != null) _controller.enabled = true;
            _controllerHeld = false;
        }

        // ------------------------------------------------------------------ preparation

        private void Prepare(Scene scene)
        {
            if (Ready) return;

            _city = scene;
            Doors.Clear();
            GameObject[] roots = scene.GetRootGameObjects();

            for (int i = 0; i < _doors.Length; i++)
            {
                Transform hinge = Find(roots, _doors[i]);
                if (hinge != null) Doors[_doors[i]] = SwingDoor.Install(hinge, _doorAngle, false);
                else Debug.LogWarning("[UberBagarre] Porte introuvable dans la ville : " + _doors[i]);
            }

            List<Light> lights = new List<Light>(256);
            for (int i = 0; i < roots.Length; i++) PrepareLights(roots[i], lights);
            if (_culler != null) _culler.Register(lights);

            for (int i = 0; i < roots.Length; i++) SwapMaterials(roots[i]);

            int renderers = 0;
            for (int i = 0; i < roots.Length; i++) renderers += roots[i].GetComponentsInChildren<Renderer>(true).Length;
            Debug.Log("[UberBagarre] Ville chargee (" + scene.name + ") : " + roots.Length + " racines, " + renderers +
                      " rendus, " + lights.Count + " lampes, " + Doors.Count + " portes.");

            // Le chargement a pris du temps : la physique doit connaître les nouveaux colliders
            // avant que le joueur ne reprenne son poids.
            Physics.SyncTransforms();

            Ready = true;
            StartCoroutine(Release());
        }

        private IEnumerator Release()
        {
            // Une image pour que la physique ait vu la ville, puis on relâche le joueur.
            yield return null;
            Hold(false);

            Action handler = Loaded;
            if (handler != null) handler();
        }

        private static Transform Find(GameObject[] roots, string path)
        {
            if (string.IsNullOrEmpty(path)) return null;

            int slash = path.IndexOf('/');
            string head = slash < 0 ? path : path.Substring(0, slash);

            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] == null || roots[i].name != head) continue;
                if (slash < 0) return roots[i].transform;

                Transform found = roots[i].transform.Find(path.Substring(slash + 1));
                if (found != null) return found;
            }

            return null;
        }

        private void PrepareLights(GameObject root, List<Light> into)
        {
            Light[] all = root.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Light light = all[i];
                if (light == null) continue;

                if (light.type == LightType.Directional)
                {
                    light.enabled = false;
                    continue;
                }

                light.shadows = LightShadows.None;
                light.bounceIntensity = 0f;

                if (IsLamp(light.transform))
                {
                    light.color = _lampColor;
                    light.intensity = _lampIntensity;
                    light.range = light.type == LightType.Spot ? _lampRange * 1.2f : _lampRange;
                    if (light.type == LightType.Spot) light.spotAngle = Mathf.Max(light.spotAngle, 110f);
                    light.enabled = true;
                    if (!light.gameObject.activeSelf) light.gameObject.SetActive(true);
                    into.Add(light);
                    continue;
                }

                if (!light.enabled || !light.gameObject.activeInHierarchy) continue;

                light.intensity = Mathf.Min(light.intensity, _otherMaxIntensity);
                light.range = Mathf.Clamp(light.range, 1f, _otherMaxRange);
                into.Add(light);
            }
        }

        private bool IsLamp(Transform t)
        {
            for (int depth = 0; t != null && depth < 5; depth++, t = t.parent)
            {
                string name = t.name.ToLowerInvariant();
                for (int i = 0; i < _lampParents.Length; i++)
                {
                    if (!string.IsNullOrEmpty(_lampParents[i]) && name.Contains(_lampParents[i])) return true;
                }
            }

            return false;
        }

        private void SwapMaterials(GameObject root)
        {
            if (_nightSwaps == null || _nightSwaps.Length == 0) return;

            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int r = 0; r < renderers.Length; r++)
            {
                Renderer renderer = renderers[r];
                Material[] shared = renderer.sharedMaterials;
                bool changed = false;

                for (int m = 0; m < shared.Length; m++)
                {
                    for (int s = 0; s < _nightSwaps.Length; s++)
                    {
                        MaterialSwap swap = _nightSwaps[s];
                        if (swap == null || swap.off == null || swap.on == null || shared[m] != swap.off) continue;

                        // Tirage stable : la même fenêtre est allumée à chaque partie.
                        if (swap.share < 1f && Hash(renderer.transform.position) > swap.share) break;

                        shared[m] = swap.on;
                        changed = true;
                        break;
                    }
                }

                if (changed) renderer.sharedMaterials = shared;
            }
        }

        private static float Hash(Vector3 p)
        {
            float h = Mathf.Sin(p.x * 12.9898f + p.y * 4.1414f + p.z * 78.233f) * 43758.5453f;
            return h - Mathf.Floor(h);
        }

        // ------------------------------------------------------------------ eau

        private void Update()
        {
            if (!Ready || _player == null || _rescuing || _safePoints.Length == 0) return;

            bool under = _player.position.y < _waterLevel - 1.3f;
            _underwater = under ? _underwater + Time.deltaTime : 0f;
            if (_underwater > 1.1f) StartCoroutine(Rescue());
        }

        private IEnumerator Rescue()
        {
            _rescuing = true;

            if (_fader != null)
            {
                _fader.FadeOut(0.6f);
                yield return new WaitForSeconds(0.8f);
            }

            Vector3 from = _player.position;
            Vector3 best = _safePoints[0];
            float bestDistance = float.MaxValue;
            for (int i = 0; i < _safePoints.Length; i++)
            {
                float d = (new Vector2(_safePoints[i].x, _safePoints[i].z) - new Vector2(from.x, from.z)).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = _safePoints[i];
            }

            Vector3 flat = new Vector3(from.x - best.x, 0f, from.z - best.z);
            Quaternion facing = flat.sqrMagnitude > 0.01f ? Quaternion.LookRotation(-flat.normalized, Vector3.up) : _player.rotation;

            ISpawnReceiver[] receivers = _player.GetComponentsInChildren<ISpawnReceiver>(true);
            for (int i = 0; i < receivers.Length; i++) receivers[i].OnSpawned(best + Vector3.up * 0.1f, facing);

            if (_subtitles != null) _subtitles.Play(DialogueLine.Say("", "Tu sors de l'eau, trempé jusqu'aux os."));

            yield return new WaitForSeconds(0.3f);
            if (_fader != null) _fader.FadeIn(0.8f);

            _underwater = 0f;
            _rescuing = false;
        }

        // ------------------------------------------------------------------ constructeur

        /// <summary>Le constructeur de la scène y écrit ce qu'il sait de la ville.</summary>
        public void Configure(string sceneName, float waterLevel, Vector3[] safePoints, string[] doors, MaterialSwap[] swaps)
        {
            _sceneName = sceneName;
            _waterLevel = waterLevel;
            _safePoints = safePoints ?? new Vector3[0];
            _doors = doors ?? new string[0];
            _nightSwaps = swaps ?? new MaterialSwap[0];
        }
    }
}
