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
    /// - toutes les vraies portes de la ville deviennent des portes (E ouvre, E ferme), les
    ///   portes vitrées de la concession coulissent toutes seules ; les doubles figés des
    ///   portes (le décor « fermé » que le jeu d'origine montrait à leur place) disparaissent,
    ///   les autres façades fermées sont recensées (<see cref="Entrances"/>) : un magasin y
    ///   branche son entrée ;
    /// - les murs de la démo (invisibles, ou barrières de béton autour du quartier jouable)
    ///   disparaissent : toute la ville est ouverte ;
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

        [SerializeField]
        [Tooltip("Objets de la ville caches au chargement (ses vehicules figes, remplaces par les notres).")]
        private string[] _hideInCity = new string[0];

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

        /// <summary>Une façade fermée de la ville : une porte qui ne s'ouvre sur rien.</summary>
        public sealed class Entrance
        {
            /// <summary>La porte (l'objet « … Door (Static) » ou « StaticDoor »).</summary>
            public Transform door;

            /// <summary>Là où l'on se tient pour l'ouvrir, devant elle.</summary>
            public Vector3 access;

            /// <summary>Le bâtiment (« Casino », « North town/Pizzeria », « @Businesses/… »).</summary>
            public string building;

            /// <summary>Chemin complet de la porte dans la ville.</summary>
            public string path;
        }

        private static readonly List<Light> Lamps = new List<Light>();
        private static readonly List<float> LampIntensity = new List<float>();
        private static readonly List<Renderer> Swapped = new List<Renderer>();
        private static readonly List<Material[]> SwapOff = new List<Material[]>();
        private static readonly List<Material[]> SwapOn = new List<Material[]>();
        private static float _night = 1f;
        private static bool _swappedOn = true;

        private static Scene _city;
        private static readonly Dictionary<string, SwingDoor> Doors = new Dictionary<string, SwingDoor>();
        private static readonly List<Entrance> _entrances = new List<Entrance>();

        /// <summary>Les façades fermées de la ville (recensées au chargement).</summary>
        public static IReadOnlyList<Entrance> Entrances { get { return _entrances; } }

        /// <summary>Toutes les vraies portes de la ville, par chemin.</summary>
        public static IEnumerable<KeyValuePair<string, SwingDoor>> AllDoors { get { return Doors; } }

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
            _entrances.Clear();
            Lamps.Clear();
            LampIntensity.Clear();
            Swapped.Clear();
            SwapOff.Clear();
            SwapOn.Clear();
            _night = 1f;
            _swappedOn = true;
        }

        /// <summary>
        /// La nuit tombe (1) ou le jour se lève (0) : les lampadaires de la ville montent ou
        /// baissent, les fenêtres et les têtes de lampadaires s'allument ou s'éteignent.
        /// </summary>
        public static void SetNight(float night)
        {
            _night = Mathf.Clamp01(night);

            for (int i = 0; i < Lamps.Count; i++)
            {
                if (Lamps[i] != null) Lamps[i].intensity = LampIntensity[i] * _night;
            }

            bool on = _night > 0.5f;
            if (on == _swappedOn) return;
            _swappedOn = on;
            for (int i = 0; i < Swapped.Count; i++)
            {
                if (Swapped[i] != null) Swapped[i].sharedMaterials = on ? SwapOn[i] : SwapOff[i];
            }
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

            // En asynchrone, derrière l'écran de chargement : le jeu continue de s'afficher (la
            // barre avance) au lieu de se figer plusieurs secondes sur une image noire.
            UI.LoadingScreen.Hold(this, "HYLAND POINT", "La ville se réveille");
            UI.LoadingScreen.Report(0.02f, "Chargement de la ville");
            _loading = SceneManager.LoadSceneAsync(_sceneName, LoadSceneMode.Additive);
        }

        private AsyncOperation _loading;

        private void ReportLoading()
        {
            if (_loading == null) return;
            if (_loading.isDone) _loading = null;
            else UI.LoadingScreen.Report(_loading.progress / 0.9f * 0.85f, "Chargement de la ville");
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
            _entrances.Clear();
            SwingDoor.Viewer = _player;
            TrafficDriver.Player = _player;
            GameObject[] roots = scene.GetRootGameObjects();

            if (!IsPrepared(roots))
            {
                Debug.LogWarning("[UberBagarre] La ville n'a pas ete preparee dans l'editeur : ses portes restent figees " +
                                 "(fusionnees avec le decor). Menu Uber Bagarre → Preparer la ville, ou reconstruis le monde ouvert.");
            }

            UI.LoadingScreen.Report(0.88f, "Ouverture des portes");
            int blockers = PrepareDoors(roots);

            for (int i = 0; i < _hideInCity.Length; i++)
            {
                Transform hidden = Find(roots, _hideInCity[i]);
                if (hidden != null) hidden.gameObject.SetActive(false);
            }

            for (int i = 0; i < _doors.Length; i++)
            {
                if (Doors.ContainsKey(_doors[i])) continue;
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
                      " rendus, " + lights.Count + " lampes, " + Doors.Count + " portes, " + _entrances.Count +
                      " facades fermees, " + blockers + " murs de la demo retires.");

            TuneForPerformance(scene);

            // L'heure du moment (l'horloge l'a peut-être fixée avant que la ville n'arrive).
            float night = _night;
            _swappedOn = true;
            SetNight(night);

            // Le chargement a pris du temps : la physique doit connaître les nouveaux colliders
            // avant que le joueur ne reprenne son poids.
            Physics.SyncTransforms();

            Ready = true;
            StartCoroutine(Release());
        }

        private IEnumerator Release()
        {
            // Une image pour que la physique ait vu la ville, puis on relâche le joueur.
            UI.LoadingScreen.Report(0.95f, "Réveil du quartier");
            yield return null;
            Hold(false);
            UI.LoadingScreen.Release(this);

            Action handler = Loaded;
            if (handler != null) handler();
        }

        // ------------------------------------------------------------------ performances

        [Header("Performances")]
        [SerializeField, Min(20f)] private float _smallDetailDistance = 110f;
        [SerializeField, Min(40f)] private float _mediumDetailDistance = 260f;
        [SerializeField, Min(10f)] private float _grassDistance = 70f;
        [SerializeField, Range(0f, 1f)] private float _grassDensity = 0.7f;
        [SerializeField, Range(0.3f, 2f)] private float _lodBias = 0.85f;

        /// <summary>
        /// Les petits objets de la ville (rangés dans leurs calques par la préparation) ne sont
        /// dessinés que de près ; l'herbe du terrain s'arrête plus tôt ; les niveaux de détail
        /// passent un peu plus vite au modèle simplifié.
        /// </summary>
        private void TuneForPerformance(Scene scene)
        {
            float[] distances = new float[32];
            distances[CityRules.SmallDetailLayer] = _smallDetailDistance;
            distances[CityRules.MediumDetailLayer] = _mediumDetailDistance;

            Camera[] cameras = Resources.FindObjectsOfTypeAll<Camera>();
            for (int i = 0; i < cameras.Length; i++)
            {
                Camera camera = cameras[i];
                if (camera == null || !camera.gameObject.scene.IsValid()) continue;
                camera.layerCullDistances = distances;
                camera.layerCullSpherical = true;
            }

            GameObject[] roots = scene.GetRootGameObjects();
            for (int r = 0; r < roots.Length; r++)
            {
                Terrain[] terrains = roots[r].GetComponentsInChildren<Terrain>(true);
                for (int i = 0; i < terrains.Length; i++)
                {
                    terrains[i].detailObjectDistance = _grassDistance;
                    terrains[i].detailObjectDensity = _grassDensity;
                    terrains[i].treeDistance = Mathf.Min(terrains[i].treeDistance, 400f);
                    terrains[i].heightmapPixelError = Mathf.Max(terrains[i].heightmapPixelError, 6f);
                }
            }

            QualitySettings.lodBias = _lodBias;
        }

        private static bool IsPrepared(GameObject[] roots)
        {
            string wanted = CityRules.PreparedMarker + " v" + CityRules.PreparedVersion;
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i] != null && roots[i].name == wanted) return true;
            }

            return false;
        }

        /// <summary>
        /// Un seul passage sur toute la ville : les murs de la démo s'éteignent, les gonds
        /// deviennent des portes, les battants coulissants s'animent, les façades fermées sont
        /// triées (double figé d'une vraie porte → caché ; sinon → recensé).
        /// </summary>
        private int PrepareDoors(GameObject[] roots)
        {
            List<Transform> hinges = new List<Transform>(128);
            List<Transform> statics = new List<Transform>(128);
            int blockers = 0;

            for (int r = 0; r < roots.Length; r++)
            {
                if (roots[r] == null) continue;
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);

                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];
                    if (CityRules.IsDemoBlocker(t.name))
                    {
                        if (t.gameObject.activeSelf) blockers++;
                        t.gameObject.SetActive(false);
                        continue;
                    }

                    if (CityRules.IsDoorHinge(t))
                    {
                        hinges.Add(t);
                        continue;
                    }

                    if (CityRules.IsStaticDoor(t))
                    {
                        statics.Add(t);
                        continue;
                    }

                    Transform closed;
                    Transform open;
                    if (CityRules.IsSlidingPanel(t, out closed, out open))
                    {
                        SlidingDoor.Install(t, closed, open, SlidingSound(t));
                    }
                }
            }

            // Une vraie porte éteinte (le jeu d'origine l'allumait selon l'histoire) laisse sa
            // façade figée en place : sans elle, il resterait un trou dans le mur.
            List<Transform> live = new List<Transform>(hinges.Count);
            for (int i = 0; i < hinges.Count; i++)
            {
                string path = CityRules.PathOf(hinges[i]);
                if (!Doors.ContainsKey(path)) Doors[path] = SwingDoor.Install(hinges[i], _doorAngle, false);
                if (hinges[i].gameObject.activeInHierarchy) live.Add(hinges[i]);
            }

            for (int i = 0; i < statics.Count; i++)
            {
                Transform door = statics[i];
                Transform model = door.Find("Model");
                Vector3 at = model != null ? model.position : door.position;

                if (NearHinge(at, live))
                {
                    door.gameObject.SetActive(false);
                    continue;
                }

                if (!door.gameObject.activeInHierarchy) continue;

                Transform access = door.Find("AccessPoint");
                string path = CityRules.PathOf(door);
                _entrances.Add(new Entrance
                {
                    door = door,
                    access = access != null ? access.position : at + door.forward * 0.6f,
                    building = CityRules.BuildingOf(path),
                    path = path
                });
            }

            return blockers;
        }

        private static bool NearHinge(Vector3 at, List<Transform> hinges)
        {
            for (int i = 0; i < hinges.Count; i++)
            {
                Vector3 h = hinges[i].position;
                if (Mathf.Abs(h.y - at.y) > 1.6f) continue;
                if (new Vector2(h.x - at.x, h.z - at.z).sqrMagnitude < 1.6f * 1.6f) return true;
            }

            return false;
        }

        private static AudioSource SlidingSound(Transform panel)
        {
            Transform group = panel.parent != null ? panel.parent.parent : null;
            if (group == null) return null;

            AudioSource[] sources = group.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null && sources[i].clip != null) return sources[i];
            }

            return null;
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
                    Lamps.Add(light);
                    LampIntensity.Add(light.intensity);
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

                if (!changed) continue;
                Swapped.Add(renderer);
                SwapOff.Add(renderer.sharedMaterials);
                SwapOn.Add(shared);
                renderer.sharedMaterials = shared;
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
            ReportLoading();
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

        /// <summary>Objets de la ville à cacher au chargement (chemins).</summary>
        public void HideInCity(string[] paths)
        {
            _hideInCity = paths ?? new string[0];
        }

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
