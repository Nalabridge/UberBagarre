using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.UI
{
    /// <summary>
    /// L'écran de chargement : entre le menu et la partie, au lancement pendant que la ville se
    /// charge, et d'une scène à l'autre. Au lieu d'une coupe sèche vers le jeu, la ville la
    /// nuit : une ligne d'immeubles aux fenêtres allumées, la pluie, les néons qui bavent dans
    /// le ciel, le nom du jeu au milieu, une notification de l'appli en haut à droite (où, quand)
    /// et une astuce en bas.
    ///
    /// Qui charge le « tient » (<see cref="Hold"/>) et le lâche (<see cref="Release"/>) quand il a
    /// fini ; l'écran s'efface quand plus personne ne le tient, et jamais avant un minimum de
    /// temps (un écran qui clignote un dixième de seconde fait pire que pas d'écran).
    ///
    /// Il survit aux changements de scène (il vit à part), et se dessine au-dessus de tout.
    /// </summary>
    [DefaultExecutionOrder(-80)]
    public class LoadingScreen : MonoBehaviour
    {
        private const float FadeIn = 0.25f;
        private const float FadeOut = 0.7f;
        private const float MinimumShown = 2.4f;
        private const float TipDuration = 6f;

        private static readonly Color Accent = new Color(1f, 0.2f, 0.55f);
        private static readonly Color Cyan = new Color(0.25f, 0.85f, 1f);
        private static readonly Color Ink = new Color(0.95f, 0.95f, 0.97f);
        private static readonly Color Dim = new Color(0.64f, 0.65f, 0.72f);

        /// <summary>Les astuces : vraies, et utiles quand on débute.</summary>
        private static readonly List<string> Tips = new List<string>
        {
            "Le téléphone fait tout : les courses, le GPS, la banque, les photos. Sors-le souvent.",
            "Une course n'est payée qu'avec la preuve : la photo de la cible au sol, cadrée.",
            "Le bonus d'une course est facultatif. Cible à terre : un coup de pied la frappe au sol — c'est là qu'on casse une jambe.",
            "Dormir au motel fait passer au jour suivant, et c'est là que la partie est sauvegardée.",
            "Le loyer du motel tombe toutes les semaines. L'appli Banque permet de l'avancer.",
            "Nestor attend ses 12 000 euros. Chaque virement depuis l'appli Banque fait baisser la dette.",
            "La garde au bon moment pare le coup, et laisse l'adversaire ouvert.",
            "Une étoile de plus sur une course, c'est un adversaire plus rapide, plus solide, qui esquive mieux.",
            "La salle de sport te rend plus fort, plus vite qu'une semaine de bagarres.",
            "Les voitures garées se conduisent. Ta caisse, elle, t'attend devant le motel.",
            "Le Vertigo, c'est en ville nord. La porte du fond, c'est une autre histoire.",
            "Ce qui se dit au téléphone compte : les messages font avancer l'histoire.",
            "La foule ne se forme que dans les endroits à l'abri des regards : ruelles, parkings, arrière-cours."
        };

        private static LoadingScreen _instance;

        private readonly HashSet<object> _holders = new HashSet<object>();
        private string _heading = "HYLAND POINT";
        private string _detail = "";
        private string _status = "Chargement";
        private float _progress;
        private float _shown;
        private float _alpha;
        private float _visibleSince;
        private bool _active;
        private int _tip;
        private float _tipTime;
        private readonly GUIContent _content = new GUIContent();

        // Le décor : une ligne d'immeubles et leurs fenêtres, tirés au hasard une fois pour toutes.
        private Rect[] _buildings;
        private Rect[] _windows;
        private Color[] _windowColors;
        private float[] _rainX, _rainY, _rainSpeed;
        private float _clock;

        /// <summary>L'écran est à l'écran (même en train d'apparaître ou de s'effacer).</summary>
        public static bool Visible
        {
            get { return _instance != null && (_instance._active || _instance._alpha > 0.001f); }
        }

        /// <summary>L'écran couvre tout : ce qui se passe derrière ne se voit pas.</summary>
        public static bool Covering
        {
            get { return _instance != null && _instance._alpha >= 0.999f; }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _instance = null;
        }

        private static LoadingScreen Instance
        {
            get
            {
                if (_instance != null) return _instance;
                GameObject go = new GameObject("Ecran de chargement");
                DontDestroyOnLoad(go);
                _instance = go.AddComponent<LoadingScreen>();
                return _instance;
            }
        }

        // ------------------------------------------------------------------ API

        /// <summary>
        /// Affiche l'écran (s'il ne l'est pas) et le tient au nom de <paramref name="holder"/>.
        /// <paramref name="heading"/> : où l'on va (« MOTEL HYLAND ») ; <paramref name="detail"/> :
        /// quand, ou pourquoi (« Chambre 3 · 14 h 00 »). Null garde ce qui est affiché.
        /// </summary>
        public static void Hold(object holder, string heading, string detail)
        {
            LoadingScreen screen = Instance;
            if (holder != null) screen._holders.Add(holder);
            if (heading != null) screen._heading = heading;
            if (detail != null) screen._detail = detail;

            if (!screen._active)
            {
                screen._active = true;
                screen._visibleSince = Time.unscaledTime;
                if (screen._alpha <= 0.001f)
                {
                    screen._progress = 0f;
                    screen._shown = 0f;
                    screen._tip = Random.Range(0, Tips.Count);
                    screen._tipTime = 0f;
                }
            }
        }

        /// <summary>Lâche l'écran : il s'efface quand plus personne ne le tient.</summary>
        public static void Release(object holder)
        {
            if (_instance == null) return;
            _instance._holders.Remove(holder);
        }

        /// <summary>La progression réelle (0 à 1) et ce qui se fait en ce moment.</summary>
        public static void Report(float progress, string status)
        {
            if (_instance == null) return;
            _instance._progress = Mathf.Max(_instance._progress, Mathf.Clamp01(progress));
            if (!string.IsNullOrEmpty(status)) _instance._status = status;
        }

        /// <summary>Change de scène derrière l'écran de chargement (asynchrone, avec sa progression).</summary>
        public static void LoadScene(string scene, string heading, string detail)
        {
            LoadingScreen screen = Instance;
            Hold(screen, heading, detail);
            screen.StartCoroutine(screen.Load(scene));
        }

        /// <summary>Ajoute une astuce (un système qui arrive en jeu peut s'y présenter).</summary>
        public static void AddTip(string tip)
        {
            if (!string.IsNullOrEmpty(tip) && !Tips.Contains(tip)) Tips.Add(tip);
        }

        private IEnumerator Load(string scene)
        {
            // Qu'il couvre l'écran avant que le jeu ne se fige sur le chargement.
            while (_alpha < 0.999f) yield return null;

            Report(0.05f, "Chargement");
            AsyncOperation operation = SceneManager.LoadSceneAsync(scene);
            if (operation == null)
            {
                Release(this);
                yield break;
            }

            while (!operation.isDone)
            {
                Report(operation.progress / 0.9f * 0.6f, "Chargement");
                yield return null;
            }

            // Deux images : que les objets de la nouvelle scène aient démarré, et que ceux qui
            // chargent encore (la ville) aient pris le relais.
            yield return null;
            yield return null;
            Release(this);
        }

        // ------------------------------------------------------------------ cycle

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            _holders.RemoveWhere(h => h == null || (h is Object && (Object)h == null));

            if (_active)
            {
                // Sans progression réelle, la barre avance d'elle-même, de moins en moins vite.
                float target = _holders.Count == 0 ? 1f : Mathf.Max(_progress, Mathf.Min(0.92f, _shown + (0.95f - _shown) * dt * 0.35f));
                _shown = Mathf.MoveTowards(_shown, target, dt * (_holders.Count == 0 ? 1.4f : 0.6f));

                bool done = _holders.Count == 0 && _shown >= 0.999f && Time.unscaledTime - _visibleSince >= MinimumShown;
                if (done) _active = false;
            }

            _alpha = Mathf.MoveTowards(_alpha, _active ? 1f : 0f, dt / (_active ? FadeIn : FadeOut));

            if (_active || _alpha > 0f)
            {
                _tipTime += dt;
                if (_tipTime >= TipDuration)
                {
                    _tipTime = 0f;
                    _tip = (_tip + 1) % Tips.Count;
                }
            }
        }

        // ------------------------------------------------------------------ dessin

        private void OnGUI()
        {
            if (_alpha <= 0.001f) return;
            GUI.depth = -200;

            float sw = Screen.width;
            float sh = Screen.height;
            float u = Mathf.Max(0.55f, sh / 1080f);
            float t = Time.unscaledTime;

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = Mathf.SmoothStep(0f, 1f, _alpha);

            DrawSky(sw, sh, u, t);
            DrawCity(sw, sh, u, t);
            DrawRain(sw, sh, u);
            DrawLogo(sw, sh, u, t);
            DrawNotification(sw, sh, u, t);
            DrawTip(sw, sh, u);
            DrawProgress(sw, sh, u, t);

            GuiKit.Alpha = previous;
        }

        private static void DrawSky(float sw, float sh, float u, float t)
        {
            GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0.028f, 0.024f, 0.05f));

            // Les néons de la ville bavent dans les nuages bas : deux grandes lueurs qui dérivent.
            float drift = Mathf.Sin(t * 0.17f);
            GuiKit.Disc(new Rect(sw * (-0.2f + drift * 0.04f), sh * 0.25f, sw * 0.8f, sh * 0.9f), new Color(Accent.r, Accent.g * 0.6f, Accent.b, 0.20f));
            GuiKit.Disc(new Rect(sw * (0.45f - drift * 0.05f), sh * 0.05f, sw * 0.75f, sh * 0.8f), new Color(Cyan.r * 0.5f, Cyan.g * 0.6f, Cyan.b, 0.12f));
            GuiKit.Disc(new Rect(sw * 0.3f, sh * 0.55f, sw * 0.5f, sh * 0.5f), new Color(1f, 0.55f, 0.2f, 0.07f));
        }

        private void DrawCity(float sw, float sh, float u, float t)
        {
            if (_buildings == null) BuildCity();

            float horizon = sh * 0.78f;
            Color wall = new Color(0.045f, 0.038f, 0.07f);

            // Une lente dérive latérale, comme vue d'une voiture qui roule au pas.
            float scroll = (t * 0.006f) % 1f;
            for (int pass = 0; pass < 2; pass++)
            {
                float offset = (pass - scroll) * sw;
                for (int i = 0; i < _buildings.Length; i++)
                {
                    Rect b = _buildings[i];
                    Rect r = new Rect(offset + b.x * sw, horizon - b.height * sh, b.width * sw + 1f, b.height * sh + sh);
                    if (r.xMax < 0f || r.x > sw) continue;
                    GuiKit.Fill(r, wall);
                }

                for (int i = 0; i < _windows.Length; i++)
                {
                    Rect w = _windows[i];
                    Rect r = new Rect(offset + w.x * sw, horizon - w.y * sh, Mathf.Max(2f, w.width * sw), Mathf.Max(2f, w.height * sh));
                    if (r.xMax < 0f || r.x > sw) continue;

                    // Une fenêtre sur neuf s'éteint et se rallume, de loin en loin.
                    Color c = _windowColors[i];
                    if (i % 9 == 4 && Mathf.Repeat(t * 0.13f + i * 0.37f, 1f) < 0.3f) c.a *= 0.15f;
                    GuiKit.Fill(r, c);
                }
            }

            // Le bas de l'écran : la rue, sombre, et le reflet mouillé des néons.
            GuiKit.Fill(new Rect(0f, horizon, sw, sh - horizon), new Color(0.02f, 0.018f, 0.035f));
            GuiKit.Disc(new Rect(sw * 0.1f, horizon - 20f * u, sw * 0.8f, 80f * u), new Color(Accent.r, Accent.g, Accent.b, 0.10f));
        }

        private void BuildCity()
        {
            System.Random random = new System.Random(1978);
            List<Rect> buildings = new List<Rect>();
            List<Rect> windows = new List<Rect>();
            List<Color> colors = new List<Color>();

            float x = 0f;
            while (x < 1f)
            {
                float width = 0.035f + (float)random.NextDouble() * 0.07f;
                float height = 0.1f + (float)random.NextDouble() * 0.28f;
                if (random.NextDouble() < 0.15) height += 0.12f;
                buildings.Add(new Rect(x, 0f, width, height));

                // Des fenêtres en grille, peu allumées : il est tard.
                int columns = Mathf.Max(2, Mathf.RoundToInt(width / 0.012f));
                int rows = Mathf.Max(3, Mathf.RoundToInt(height / 0.03f));
                for (int c = 0; c < columns; c++)
                {
                    for (int r = 1; r < rows; r++)
                    {
                        if (random.NextDouble() > 0.16) continue;
                        float wx = x + (c + 0.3f) * width / columns;
                        float wy = height - r * height / rows + 0.012f;
                        windows.Add(new Rect(wx, wy, width / columns * 0.45f, 0.011f));

                        double pick = random.NextDouble();
                        Color color = pick < 0.6 ? new Color(1f, 0.78f, 0.45f) : pick < 0.85 ? new Color(0.6f, 0.85f, 1f) : new Color(1f, 0.35f, 0.7f);
                        color.a = 0.35f + (float)random.NextDouble() * 0.45f;
                        colors.Add(color);
                    }
                }

                x += width + (float)random.NextDouble() * 0.006f;
            }

            _buildings = buildings.ToArray();
            _windows = windows.ToArray();
            _windowColors = colors.ToArray();
        }

        private void DrawRain(float sw, float sh, float u)
        {
            const int count = 90;
            if (_rainX == null)
            {
                _rainX = new float[count];
                _rainY = new float[count];
                _rainSpeed = new float[count];
                System.Random random = new System.Random(7);
                for (int i = 0; i < count; i++)
                {
                    _rainX[i] = (float)random.NextDouble();
                    _rainY[i] = (float)random.NextDouble();
                    _rainSpeed[i] = 0.9f + (float)random.NextDouble() * 0.9f;
                }

                _clock = Time.unscaledTime;
            }

            if (Event.current.type == EventType.Repaint)
            {
                float dt = Mathf.Min(0.1f, Time.unscaledTime - _clock);
                _clock = Time.unscaledTime;
                for (int i = 0; i < count; i++)
                {
                    _rainY[i] += _rainSpeed[i] * dt;
                    _rainX[i] += _rainSpeed[i] * dt * 0.07f;
                    if (_rainY[i] > 1.1f)
                    {
                        _rainY[i] -= 1.2f;
                        _rainX[i] = (_rainX[i] * 7.31f + 0.37f) % 1f;
                    }
                }
            }

            for (int i = 0; i < count; i++)
            {
                GuiKit.Fill(new Rect(_rainX[i] * sw, _rainY[i] * sh, Mathf.Max(1f, 1.3f * u), (0.025f + (i % 7) * 0.006f) * sh),
                    new Color(0.75f, 0.82f, 0.95f, 0.05f + (i % 5) * 0.02f));
            }
        }

        private void DrawLogo(float sw, float sh, float u, float t)
        {
            GUIStyle logo = GuiKit.Text(Mathf.RoundToInt(104f * u), GuiKit.Weight.Black, TextAnchor.MiddleCenter);
            GUIStyle tag = GuiKit.Text(Mathf.RoundToInt(20f * u), GuiKit.Weight.Bold, TextAnchor.MiddleCenter);

            float y = sh * 0.36f;
            float pulse = 0.85f + Mathf.Sin(t * 2.1f) * 0.08f + (Mathf.Repeat(t * 0.31f, 1f) < 0.02f ? -0.3f : 0f);

            GuiKit.Disc(new Rect(sw * 0.5f - 520f * u, y - 170f * u, 1040f * u, 340f * u), new Color(Accent.r, Accent.g, Accent.b, 0.22f * pulse));

            Rect rect = new Rect(0f, y - 70f * u, sw, 140f * u);
            GuiKit.ShadowLabel(rect, "ÜBER BAGARRE", logo, new Color(1f, 0.95f, 0.98f), 0.6f);

            _content.text = "ÜBER BAGARRE";
            float width = logo.CalcSize(_content).x;
            GuiKit.Rounded(new Rect(sw * 0.5f - width * 0.5f, y + 62f * u, width, 5f * u), Accent, 3f * u);

            GuiKit.ShadowLabel(new Rect(0f, y + 78f * u, sw, 30f * u), "LIVRAISON DE BAGARRES À DOMICILE", tag,
                new Color(1f, 0.82f, 0.35f), 0.6f);
        }

        /// <summary>La notification de l'appli : où l'on va, et quand.</summary>
        private void DrawNotification(float sw, float sh, float u, float t)
        {
            float appear = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((Time.unscaledTime - _visibleSince - 0.3f) / 0.45f));
            float width = 440f * u;
            float height = 92f * u;
            Rect card = new Rect(sw - width - 40f * u + (1f - appear) * 60f * u, 40f * u, width, height);

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * appear;

            GuiKit.Glow(card, new Color(0f, 0f, 0f, 0.55f), 18f * u, 18f * u);
            GuiKit.Rounded(card, new Color(0.10f, 0.09f, 0.14f, 0.94f), 18f * u);
            GuiKit.RoundedOutline(card, new Color(1f, 1f, 1f, 0.08f), 18f * u, 1f);

            // L'icône de l'appli : un carré arrondi magenta, un Ü blanc.
            Rect icon = new Rect(card.x + 16f * u, card.y + 16f * u, 60f * u, 60f * u);
            GuiKit.Rounded(icon, Accent, 14f * u);
            GUIStyle glyph = GuiKit.Text(Mathf.RoundToInt(34f * u), GuiKit.Weight.Black, TextAnchor.MiddleCenter);
            GuiKit.ShadowLabel(icon, "Ü", glyph, Color.white, 0.3f);

            GUIStyle app = GuiKit.Text(Mathf.RoundToInt(14f * u), GuiKit.Weight.Bold, TextAnchor.UpperLeft);
            GUIStyle heading = GuiKit.Text(Mathf.RoundToInt(22f * u), GuiKit.Weight.Black, TextAnchor.UpperLeft);
            GUIStyle detail = GuiKit.Text(Mathf.RoundToInt(16f * u), GuiKit.Weight.Medium, TextAnchor.UpperLeft);

            float x = icon.xMax + 14f * u;
            float w = card.xMax - x - 16f * u;
            GuiKit.ShadowLabel(new Rect(x, card.y + 13f * u, w, 18f * u), "ÜBER BAGARRE  ·  maintenant", app, Dim, 0.3f);
            GuiKit.ShadowLabel(new Rect(x, card.y + 32f * u, w, 28f * u), _heading, heading, Ink, 0.3f);
            if (!string.IsNullOrEmpty(_detail))
                GuiKit.ShadowLabel(new Rect(x, card.y + 60f * u, w, 22f * u), _detail, detail, new Color(0.85f, 0.86f, 0.9f), 0.3f);

            GuiKit.Alpha = previous;
        }

        private void DrawTip(float sw, float sh, float u)
        {
            if (Tips.Count == 0) return;

            float fade = Mathf.Clamp01(Mathf.Min(_tipTime / 0.4f, (TipDuration - _tipTime) / 0.4f));
            float width = Mathf.Min(sw - 80f * u, 900f * u);
            Rect card = new Rect((sw - width) * 0.5f, sh - 190f * u, width, 70f * u);

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * fade;

            GUIStyle label = GuiKit.Text(Mathf.RoundToInt(14f * u), GuiKit.Weight.Black, TextAnchor.MiddleCenter);
            GUIStyle text = GuiKit.Text(Mathf.RoundToInt(19f * u), GuiKit.Weight.Medium, TextAnchor.UpperCenter, true);

            Rect pill = new Rect(sw * 0.5f - 44f * u, card.y - 4f * u, 88f * u, 24f * u);
            GuiKit.Rounded(pill, new Color(1f, 0.82f, 0.35f, 0.95f), 12f * u);
            GuiKit.ShadowLabel(pill, "ASTUCE", label, new Color(0.12f, 0.08f, 0.02f), 0f);
            GuiKit.ShadowLabel(new Rect(card.x, card.y + 28f * u, card.width, card.height), Tips[_tip % Tips.Count], text, Ink, 0.7f);

            GuiKit.Alpha = previous;
        }

        private void DrawProgress(float sw, float sh, float u, float t)
        {
            float margin = 60f * u;
            float y = sh - 70f * u;
            Rect track = new Rect(margin, y, sw - margin * 2f, 6f * u);

            GuiKit.Rounded(track, new Color(1f, 1f, 1f, 0.10f), 3f * u);
            Rect fill = new Rect(track.x, track.y, Mathf.Max(track.height, track.width * _shown), track.height);
            GuiKit.Glow(fill, new Color(Accent.r, Accent.g, Accent.b, 0.35f), 3f * u, 8f * u);
            GuiKit.Rounded(fill, Accent, 3f * u);

            // Un reflet qui court sur la partie remplie.
            float sweep = Mathf.Repeat(t * 0.6f, 1.4f);
            if (sweep < 1f)
            {
                Rect glint = new Rect(fill.x + (fill.width - 60f * u) * sweep, fill.y, 60f * u, fill.height);
                GuiKit.Rounded(glint, new Color(1f, 1f, 1f, 0.35f * (1f - sweep)), 3f * u);
            }

            GUIStyle status = GuiKit.Text(Mathf.RoundToInt(16f * u), GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            GUIStyle percent = GuiKit.Text(Mathf.RoundToInt(16f * u), GuiKit.Weight.Bold, TextAnchor.MiddleRight);
            string dots = new string('.', 1 + Mathf.FloorToInt(t * 2.5f) % 3);
            string line = _shown >= 0.999f ? "PRÊT" : _status.ToUpperInvariant() + dots;
            GuiKit.ShadowLabel(new Rect(track.x, y - 32f * u, track.width * 0.6f, 24f * u), line, status, Dim, 0.5f);
            GuiKit.ShadowLabel(new Rect(track.x, y - 32f * u, track.width, 24f * u), Mathf.RoundToInt(_shown * 100f) + " %", percent, Ink, 0.5f);
        }
    }
}
