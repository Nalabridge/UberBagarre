using System.Collections.Generic;
using UberBagarre.Story;
using UberBagarre.UI;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La météo de la ville : du ciel dégagé à l'orage.
    ///
    /// Chaque jour a son programme, tiré du numéro du jour (le même à chaque partie) : souvent
    /// sec, parfois une averse de quelques heures, de loin en loin une nuit d'orage. L'histoire
    /// peut aussi commander la pluie (<see cref="Force"/>) — la première nuit au Vertigo, par
    /// exemple.
    ///
    /// La pluie se VOIT et s'ENTEND, et elle MOUILLE :
    /// - des gouttes autour de la caméra, qui s'arrêtent sous les toits (elles heurtent le
    ///   décor) et éclaboussent en touchant le sol ;
    /// - le ciel se couvre (<see cref="TimeOfDay.Overcast"/>) : soleil voilé, brume plus épaisse ;
    /// - le sol de la ville s'assombrit et devient brillant, puis des flaques se forment et
    ///   grandissent (elles apparaissent là où l'eau s'accumule, le long des trottoirs) ; les
    ///   lampadaires et les néons s'y reflètent. Après la pluie, tout sèche lentement ;
    /// - le bruit de la pluie, étouffé quand on est à l'abri ; sous l'orage, des éclairs.
    /// </summary>
    public class Weather : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private TimeOfDay _time;
        [SerializeField] private ParticleSystem _rain;
        [SerializeField] private ParticleSystem _splashes;
        [SerializeField] private Material _puddleMaterial;
        [SerializeField] private Light _lightning;

        [Header("Flaques")]
        [SerializeField] private Vector3[] _puddleSpots = new Vector3[0];
        [SerializeField, Min(0.3f)] private float _puddleMin = 1.1f;
        [SerializeField, Min(0.3f)] private float _puddleMax = 3.4f;

        [Header("Réglages")]
        [SerializeField, Min(10f)] private float _maxDrops = 1500f;
        [SerializeField, Range(0f, 1f)] private float _chanceOfRain = 0.38f;

        [SerializeField, Min(1f)]
        [Tooltip("Heures de jeu pour qu'une pluie battante trempe complètement le sol.")]
        private float _hoursToSoak = 0.5f;

        [SerializeField, Min(1f)]
        [Tooltip("Heures de jeu pour que le sol sèche complètement.")]
        private float _hoursToDry = 2.5f;

        public enum Sky
        {
            Degage,
            Nuageux,
            Bruine,
            Pluie,
            Orage
        }

        private struct Wet
        {
            public Material material;
            public Color color;
            public bool hasColor;
            public float gloss;
            public bool hasGloss;
            public bool hasSmooth;
            public float smooth;
        }

        private readonly List<Wet> _wet = new List<Wet>();
        private readonly Dictionary<Material, Material> _copies = new Dictionary<Material, Material>();
        private GameObject _puddleRoot;
        private Material _puddles;
        private Texture2D _puddleMask;

        private float _intensity;      // pluie actuelle (0 à 1)
        private float _clouds;         // couverture nuageuse actuelle
        private float _wetness;        // le sol : 0 sec, 1 trempé
        private float _forcedUntil = -1f;
        private float _forcedRain;
        private int _planDay = -1;
        private float _rainStart, _rainEnd, _rainPeak, _cloudBase;
        private bool _storm;
        private float _lastApplied = -1f;

        private AudioSource _audio;
        private AudioLowPassFilter _lowpass;
        private AudioClip _loop;
        private AudioClip _thunder;
        private float _nextFlash = 20f;
        private float _flashTime = -1f;
        private float _thunderAt = -1f;
        private float _sheltered;
        private PlayerProgress _progress;

        public static Weather Instance { get; private set; }

        /// <summary>La pluie qui tombe (0 à 1).</summary>
        public float Rain { get { return _intensity; } }

        /// <summary>Le sol : 0 sec, 1 trempé (les flaques en dépendent).</summary>
        public float Wetness { get { return _wetness; } }

        public Sky Current
        {
            get
            {
                if (_intensity > 0.75f && _storm) return Sky.Orage;
                if (_intensity > 0.3f) return Sky.Pluie;
                if (_intensity > 0.04f) return Sky.Bruine;
                return _clouds > 0.35f ? Sky.Nuageux : Sky.Degage;
            }
        }

        /// <summary>« Pluie · 11 °C » : pour le téléphone.</summary>
        public string Describe()
        {
            string sky;
            switch (Current)
            {
                case Sky.Orage: sky = "Orage"; break;
                case Sky.Pluie: sky = "Pluie"; break;
                case Sky.Bruine: sky = "Bruine"; break;
                case Sky.Nuageux: sky = "Nuageux"; break;
                default: sky = "Dégagé"; break;
            }

            float hour = WorldClock.Instance != null ? WorldClock.Instance.Hour : 12f;
            float temperature = 9f + 7f * Mathf.Sin((hour - 9f) / 24f * Mathf.PI * 2f) - _intensity * 3f;
            return sky + "  ·  " + Mathf.RoundToInt(temperature) + " °C";
        }

        /// <summary>L'histoire commande la pluie : <paramref name="rain"/> pendant <paramref name="hours"/> heures de jeu.</summary>
        public void Force(float rain, float hours)
        {
            _forcedRain = Mathf.Clamp01(rain);
            _forcedUntil = GameHours + Mathf.Max(0.1f, hours);
        }

        // Des heures de jeu qui ne font que croître (le jour compte) : pour les échéances.
        private float GameHours
        {
            get
            {
                float hour = WorldClock.Instance != null ? WorldClock.Instance.Hour : 12f;
                int day = _progress != null ? _progress.Day : 0;
                return day * 24f + hour;
            }
        }

        // ------------------------------------------------------------------ cycle

        private void Awake()
        {
            Instance = this;
        }

        private void OnEnable()
        {
            MapStreamer.Loaded += OnCityLoaded;
            if (MapStreamer.Ready) OnCityLoaded();
        }

        private void OnDisable()
        {
            MapStreamer.Loaded -= OnCityLoaded;
        }

        private void Start()
        {
            _progress = FindAnyObjectByType<PlayerProgress>();

            _audio = gameObject.AddComponent<AudioSource>();
            _audio.loop = true;
            _audio.playOnAwake = false;
            _audio.spatialBlend = 0f;
            _audio.volume = 0f;
            _loop = RainLoop();
            _thunder = Thunder();
            _audio.clip = _loop;
            _lowpass = gameObject.AddComponent<AudioLowPassFilter>();
            _lowpass.cutoffFrequency = 22000f;

            if (_rain != null) SetEmission(_rain, 0f);
            if (_lightning != null) _lightning.enabled = false;

            LoadingScreen.AddTip("Quand il pleut, le sol brille et des flaques se forment. Les lampadaires s'y reflètent.");
        }

        private void OnDestroy()
        {
            if (Instance == this) Instance = null;
            foreach (KeyValuePair<Material, Material> pair in _copies)
            {
                if (pair.Value != null) Destroy(pair.Value);
            }

            if (_puddles != null) Destroy(_puddles);
            if (_puddleMask != null) Destroy(_puddleMask);
            if (_loop != null) Destroy(_loop);
            if (_thunder != null) Destroy(_thunder);
        }

        private void Update()
        {
            bool paused = GameMenu.IsPaused || Time.timeScale <= 0f;
            float dt = Time.deltaTime;
            float gameHours = dt / 60f; // une minute de jeu par seconde : 1/60 d'heure

            if (!paused)
            {
                Plan();
                float target = TargetRain();
                float clouds = Mathf.Max(_cloudBase, Mathf.Clamp01(target * 1.4f));

                // La pluie arrive et s'en va en quelques secondes réelles, pas d'un coup.
                _intensity = Mathf.MoveTowards(_intensity, target, dt * 0.08f);
                _clouds = Mathf.MoveTowards(_clouds, clouds, dt * 0.05f);

                if (_intensity > 0.03f) _wetness = Mathf.Min(1f, _wetness + gameHours * _intensity / _hoursToSoak);
                else _wetness = Mathf.Max(0f, _wetness - gameHours / _hoursToDry);
            }

            Apply();
            UpdateAudio(dt);
            UpdateLightning();
        }

        /// <summary>Le programme du jour : une averse ou non, quand, combien de temps, quelle force.</summary>
        private void Plan()
        {
            int day = _progress != null ? _progress.Day : 0;
            if (day == _planDay) return;
            _planDay = day;

            System.Random random = new System.Random(day * 7919 + 17);
            _cloudBase = (float)random.NextDouble() * 0.45f;
            bool rains = random.NextDouble() < _chanceOfRain;
            _rainStart = (float)random.NextDouble() * 22f;
            _rainEnd = rains ? _rainStart + 1.5f + (float)random.NextDouble() * 4.5f : -1f;
            _rainPeak = 0.35f + (float)random.NextDouble() * 0.65f;
            _storm = rains && random.NextDouble() < 0.3;
        }

        private float TargetRain()
        {
            if (_forcedUntil > 0f && GameHours < _forcedUntil) return _forcedRain;

            float hour = WorldClock.Instance != null ? WorldClock.Instance.Hour : 12f;
            if (_rainEnd < 0f) return 0f;

            // L'averse peut déborder sur minuit.
            float h = hour < _rainStart && _rainEnd > 24f ? hour + 24f : hour;
            if (h < _rainStart || h > _rainEnd) return 0f;

            // Montée, plateau, descente.
            float t = Mathf.InverseLerp(_rainStart, _rainEnd, h);
            float shape = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 5f)) * Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) * 4f));
            return _rainPeak * shape;
        }

        private void Apply()
        {
            if (_time != null) _time.Overcast = Mathf.Clamp01(Mathf.Max(_clouds * 0.6f, _intensity));

            if (_rain != null) SetEmission(_rain, _intensity * _maxDrops);

            // Le sol et les flaques : pas la peine de tout réécrire à chaque image.
            if (Mathf.Abs(_wetness - _lastApplied) < 0.004f) return;
            _lastApplied = _wetness;

            for (int i = 0; i < _wet.Count; i++)
            {
                Wet w = _wet[i];
                if (w.material == null) continue;

                // Mouillé, un sol est plus sombre et beaucoup plus lisse : il reflète.
                if (w.hasColor) w.material.color = Color.Lerp(w.color, w.color * 0.62f, _wetness);
                if (w.hasGloss) w.material.SetFloat("_Glossiness", Mathf.Lerp(w.gloss, Mathf.Max(w.gloss, 0.78f), _wetness));
                if (w.hasSmooth) w.material.SetFloat("_Smoothness", Mathf.Lerp(w.smooth, Mathf.Max(w.smooth, 0.78f), _wetness));
            }

            if (_puddleRoot != null)
            {
                bool show = _wetness > 0.04f;
                if (_puddleRoot.activeSelf != show) _puddleRoot.SetActive(show);

                // Les flaques grandissent depuis leur creux : le seuil de découpe baisse.
                if (_puddles != null) _puddles.SetFloat("_Cutoff", Mathf.Lerp(0.96f, 0.3f, Mathf.SmoothStep(0f, 1f, _wetness)));
            }
        }

        private static void SetEmission(ParticleSystem system, float rate)
        {
            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = rate;
            if (rate > 0.5f && !system.isPlaying) system.Play();
        }

        // ------------------------------------------------------------------ la ville mouillée

        private void OnCityLoaded()
        {
            if (_wet.Count > 0 || _puddleRoot != null) return;

            GameObject[] roots = MapStreamer.CityRoots();
            int renderers = 0;
            for (int r = 0; r < roots.Length; r++)
            {
                MeshRenderer[] all = roots[r].GetComponentsInChildren<MeshRenderer>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    if (WetSurface(all[i])) renderers++;
                }
            }

            BuildPuddles();
            _lastApplied = -1f;
            Debug.Log("[UberBagarre] Météo : " + renderers + " surfaces de sol, " + _wet.Count + " matériaux mouillables, " +
                      (_puddleRoot != null ? _puddleRoot.transform.childCount : 0) + " flaques.");
        }

        /// <summary>
        /// Un sol : un rendu plat et étendu (route, trottoir, parking, place). Ses matériaux
        /// sont remplacés par des copies qu'on peut mouiller sans toucher aux originaux.
        /// </summary>
        private bool WetSurface(MeshRenderer renderer)
        {
            if (renderer == null || MapStreamer.IsNightSwapped(renderer)) return false;

            Bounds b = renderer.bounds;
            if (b.size.y > 0.9f || b.size.x * b.size.z < 8f) return false;

            Material[] shared = renderer.sharedMaterials;
            bool changed = false;
            for (int m = 0; m < shared.Length; m++)
            {
                Material original = shared[m];
                if (original == null) continue;
                if (!original.HasProperty("_Glossiness") && !original.HasProperty("_Smoothness")) continue;
                if (original.renderQueue >= 2500) continue;

                Material copy;
                if (!_copies.TryGetValue(original, out copy))
                {
                    copy = new Material(original);
                    copy.name = original.name + " (mouillé)";
                    _copies[original] = copy;

                    Wet w = new Wet();
                    w.material = copy;
                    w.hasColor = original.HasProperty("_Color");
                    w.color = w.hasColor ? original.color : Color.white;
                    w.hasGloss = original.HasProperty("_Glossiness");
                    w.gloss = w.hasGloss ? original.GetFloat("_Glossiness") : 0f;
                    w.hasSmooth = original.HasProperty("_Smoothness");
                    w.smooth = w.hasSmooth ? original.GetFloat("_Smoothness") : 0f;
                    _wet.Add(w);
                }

                shared[m] = copy;
                changed = true;
            }

            if (changed) renderer.sharedMaterials = shared;
            return changed;
        }

        /// <summary>
        /// Les flaques : un quadrilatère posé à plat sur le sol (un rayon depuis le ciel trouve
        /// la hauteur exacte), matériau noir et très lisse, découpé par un masque en forme de
        /// tache. Toutes partagent le même matériau : un seul seuil les fait toutes grandir.
        /// </summary>
        private void BuildPuddles()
        {
            if (_puddleMaterial == null || _puddleSpots == null || _puddleSpots.Length == 0) return;

            _puddleMask = PuddleMask(128, 5);
            _puddles = new Material(_puddleMaterial);
            _puddles.name = "Flaques (partagé)";
            _puddles.mainTexture = _puddleMask;
            _puddles.enableInstancing = true;

            Mesh quad = FlatQuad();
            _puddleRoot = new GameObject("=== Flaques ===");
            _puddleRoot.transform.SetParent(transform, false);

            System.Random random = new System.Random(4242);
            int mask = ~(1 << 2);
            for (int i = 0; i < _puddleSpots.Length; i++)
            {
                Vector3 spot = _puddleSpots[i];
                RaycastHit hit;
                if (!Physics.Raycast(spot + Vector3.up * 8f, Vector3.down, out hit, 16f, mask, QueryTriggerInteraction.Ignore)) continue;
                if (hit.normal.y < 0.97f || hit.rigidbody != null || hit.collider is CharacterController) continue;

                GameObject go = new GameObject("Flaque");
                go.transform.SetParent(_puddleRoot.transform, false);
                go.transform.position = hit.point + hit.normal * 0.012f;
                go.transform.rotation = Quaternion.FromToRotation(Vector3.up, hit.normal) *
                                        Quaternion.Euler(0f, (float)random.NextDouble() * 360f, 0f);

                float size = Mathf.Lerp(_puddleMin, _puddleMax, (float)random.NextDouble());
                float stretch = 1f + (float)random.NextDouble() * 0.9f;
                go.transform.localScale = new Vector3(size * stretch, 1f, size);

                go.AddComponent<MeshFilter>().sharedMesh = quad;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = _puddles;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = true;
            }

            _puddleRoot.SetActive(false);
        }

        private static Mesh FlatQuad()
        {
            Mesh mesh = new Mesh();
            mesh.name = "Flaque (quad)";
            mesh.vertices = new[] { new Vector3(-0.5f, 0f, -0.5f), new Vector3(-0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, 0.5f), new Vector3(0.5f, 0f, -0.5f) };
            mesh.uv = new[] { new Vector2(0f, 0f), new Vector2(0f, 1f), new Vector2(1f, 1f), new Vector2(1f, 0f) };
            mesh.normals = new[] { Vector3.up, Vector3.up, Vector3.up, Vector3.up };
            mesh.tangents = new[] { new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f), new Vector4(1f, 0f, 0f, 1f) };
            mesh.triangles = new[] { 0, 1, 2, 0, 2, 3 };
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>
        /// Le masque d'une flaque (alpha) : haut au creux, qui descend vers les bords avec du
        /// bruit. Découpé à un seuil élevé, il ne reste qu'une petite tache ; à un seuil bas,
        /// une grande flaque aux bords irréguliers.
        /// </summary>
        private static Texture2D PuddleMask(int size, int seed)
        {
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.name = "Masque de flaque";
            Color[] pixels = new Color[size * size];
            float ox = seed * 13.1f, oy = seed * 7.7f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = x / (float)(size - 1) * 2f - 1f;
                    float v = y / (float)(size - 1) * 2f - 1f;
                    float r = Mathf.Sqrt(u * u + v * v);

                    float noise = Mathf.PerlinNoise(ox + u * 2.2f, oy + v * 2.2f) * 0.6f +
                                  Mathf.PerlinNoise(ox * 2f + u * 5.5f, oy * 2f + v * 5.5f) * 0.3f +
                                  Mathf.PerlinNoise(ox * 3f + u * 12f, oy * 3f + v * 12f) * 0.1f;
                    float alpha = Mathf.Clamp01(1.05f - r * 1.15f + (noise - 0.5f) * 0.75f);

                    // Un bord de sécurité : jamais rien au ras du carré.
                    alpha *= Mathf.Clamp01((1f - Mathf.Max(Mathf.Abs(u), Mathf.Abs(v))) * 8f);
                    pixels[y * size + x] = new Color(0.08f, 0.085f, 0.095f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply(true);
            return texture;
        }

        // ------------------------------------------------------------------ son

        private void UpdateAudio(float dt)
        {
            if (_audio == null) return;

            // À l'abri (un toit au-dessus de la caméra) : la pluie tape dehors, étouffée.
            Camera camera = Camera.main;
            bool shelter = false;
            if (camera != null && _intensity > 0.01f)
            {
                shelter = Physics.Raycast(camera.transform.position + Vector3.up * 0.3f, Vector3.up, 40f, ~0, QueryTriggerInteraction.Ignore);
            }

            _sheltered = Mathf.MoveTowards(_sheltered, shelter ? 1f : 0f, Time.unscaledDeltaTime * 2f);
            _lowpass.cutoffFrequency = Mathf.Lerp(22000f, 900f, _sheltered);

            float volume = Mathf.Clamp01(_intensity * 1.1f) * Mathf.Lerp(0.55f, 0.28f, _sheltered);
            if (GameMenu.IsPaused) volume = 0f;
            _audio.volume = Mathf.MoveTowards(_audio.volume, volume, Time.unscaledDeltaTime * 0.5f);

            if (_audio.volume > 0.001f && !_audio.isPlaying) _audio.Play();
            else if (_audio.volume <= 0.001f && _audio.isPlaying) _audio.Stop();
        }

        /// <summary>Quatre secondes de pluie en boucle : un souffle filtré et des gouttes qui claquent.</summary>
        private static AudioClip RainLoop()
        {
            const int rate = 22050;
            int length = rate * 4;
            float[] data = new float[length];
            System.Random random = new System.Random(3);
            float low = 0f, band = 0f;

            for (int n = 0; n < length; n++)
            {
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.35f;
                band += (low - band) * 0.04f;
                data[n] = (low - band) * 0.55f;
            }

            // Les gouttes : de brefs clics, plus ou moins proches.
            for (int k = 0; k < 900; k++)
            {
                int start = random.Next(0, length - 200);
                float level = 0.05f + (float)random.NextDouble() * 0.2f;
                for (int n = 0; n < 120; n++)
                {
                    float t = n / (float)rate;
                    data[start + n] += (float)(random.NextDouble() * 2.0 - 1.0) * Mathf.Exp(-t * 900f) * level;
                }
            }

            // Fondu de bouclage : la fin rejoint le début sans clic.
            int fade = rate / 5;
            for (int n = 0; n < fade; n++)
            {
                float t = n / (float)fade;
                data[n] = data[n] * t + data[length - fade + n] * (1f - t);
            }

            AudioClip clip = AudioClip.Create("Pluie", length - fade, 1, rate, false);
            float[] trimmed = new float[length - fade];
            System.Array.Copy(data, trimmed, trimmed.Length);
            clip.SetData(trimmed, 0);
            return clip;
        }

        // ------------------------------------------------------------------ orage

        private void UpdateLightning()
        {
            bool storm = Current == Sky.Orage && !GameMenu.IsPaused;
            float now = Time.time;

            if (storm && now >= _nextFlash)
            {
                _flashTime = now;
                _nextFlash = now + Random.Range(18f, 55f);
                _thunderAt = now + Random.Range(0.6f, 3.2f);
            }

            if (_lightning != null)
            {
                float age = now - _flashTime;
                float flash = age < 0f || _flashTime < 0f ? 0f
                    : age < 0.07f ? 1f : age < 0.14f ? 0.15f : age < 0.22f ? 0.8f : Mathf.Max(0f, 0.4f - (age - 0.22f) * 2f);
                _lightning.intensity = flash * 2.6f;
                _lightning.enabled = flash > 0.01f;
            }

            if (_thunderAt > 0f && now >= _thunderAt)
            {
                _thunderAt = -1f;
                if (_audio != null && _thunder != null) _audio.PlayOneShot(_thunder, Mathf.Lerp(0.9f, 0.5f, _sheltered));
            }
        }

        /// <summary>Le tonnerre : un craquement, puis un grondement grave qui roule et s'éteint.</summary>
        private static AudioClip Thunder()
        {
            const int rate = 22050;
            int length = rate * 5;
            float[] data = new float[length];
            System.Random random = new System.Random(9);
            float low = 0f, lower = 0f;

            for (int n = 0; n < length; n++)
            {
                float t = n / (float)rate;
                float white = (float)(random.NextDouble() * 2.0 - 1.0);
                low += (white - low) * 0.05f;
                lower += (low - lower) * 0.02f;

                float crack = t < 0.25f ? white * Mathf.Exp(-t * 18f) * 0.5f : 0f;
                float roll = (1f + 0.5f * Mathf.Sin(t * 5.3f) * Mathf.Sin(t * 1.7f)) * Mathf.Exp(-t * 0.7f) * Mathf.Clamp01(t * 6f);
                data[n] = crack + lower * roll * 9f;
            }

            AudioClip clip = AudioClip.Create("Tonnerre", length, 1, rate, false);
            clip.SetData(data, 0);
            return clip;
        }

        public void Configure(TimeOfDay time, ParticleSystem rain, ParticleSystem splashes, Material puddle, Light lightning,
            Vector3[] puddleSpots)
        {
            _time = time;
            _rain = rain;
            _splashes = splashes;
            _puddleMaterial = puddle;
            _lightning = lightning;
            _puddleSpots = puddleSpots ?? new Vector3[0];
        }
    }
}
