using System;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La carte de la ville : une mini-carte en haut à droite (nord en haut, le joueur en
    /// flèche), la grande carte sur M, et le GPS de la course en cours — un point sur les deux
    /// cartes, un repère à l'écran avec la distance, et une colonne de lumière au-dessus de la
    /// cible, qu'on voit par-dessus les toits.
    ///
    /// Les rues, les îlots et les lieux sont écrits une fois pour toutes par le constructeur de
    /// la ville : la carte dessine ce qui existe vraiment, au mètre près.
    /// </summary>
    public class CityMap : MonoBehaviour
    {
        [Serializable]
        public class Landmark
        {
            public string label;
            public Vector2 position;
            public Color color = Color.white;
        }

        [Header("Ville (x, z monde)")]
        [SerializeField] private Rect _bounds = new Rect(-200f, -170f, 400f, 320f);
        [SerializeField] private Rect[] _roads = new Rect[0];
        [SerializeField] private Rect[] _blocks = new Rect[0];
        [SerializeField] private Rect[] _parks = new Rect[0];
        [SerializeField] private Landmark[] _landmarks = new Landmark[0];

        [Header("References")]
        [SerializeField] private Transform _player;
        [SerializeField] private Camera _camera;
        [SerializeField] private PlayerInputReader _input;
        [SerializeField] private PhoneDevice _phone;

        [SerializeField]
        [Tooltip("La colonne de lumiere du GPS, posee sur la cible.")]
        private GameObject _beam;

        [Header("Mini-carte")]
        [SerializeField, Min(60f)] private float _miniSize = 230f;
        [SerializeField, Min(0.2f)] private float _miniScale = 1.1f;

        private bool _full;
        private bool _hasWaypoint;
        private Vector3 _waypoint;
        private string _waypointLabel;
        private Color _waypointColor = new Color(1f, 0.82f, 0.25f);
        private Transform _waypointFollow;

        private Texture2D _arrow;
        private Texture2D _dot;
        private GUIStyle _label;
        private GUIStyle _small;
        private GUIStyle _title;

        private static readonly Color Water = new Color(0.05f, 0.06f, 0.08f, 0.88f);
        private static readonly Color Road = new Color(0.36f, 0.37f, 0.40f, 1f);
        private static readonly Color Block = new Color(0.13f, 0.13f, 0.15f, 1f);
        private static readonly Color Park = new Color(0.14f, 0.24f, 0.13f, 1f);

        public bool HasWaypoint { get { return _hasWaypoint; } }

        /// <summary>Le constructeur de la ville y écrit ce qu'il a bâti.</summary>
        public void Configure(Rect bounds, Rect[] roads, Rect[] blocks, Rect[] parks, Landmark[] landmarks)
        {
            _bounds = bounds;
            _roads = roads;
            _blocks = blocks;
            _parks = parks;
            _landmarks = landmarks;
        }

        /// <summary>Pose le GPS. <paramref name="follow"/> : il suit cet objet (une cible qui bouge).</summary>
        public void SetWaypoint(Vector3 world, string label, Transform follow)
        {
            _hasWaypoint = true;
            _waypoint = world;
            _waypointLabel = label;
            _waypointFollow = follow;
            if (_beam != null) _beam.SetActive(true);
        }

        public void ClearWaypoint()
        {
            _hasWaypoint = false;
            _waypointFollow = null;
            if (_beam != null) _beam.SetActive(false);
        }

        private void Awake()
        {
            if (_beam != null) _beam.SetActive(false);
        }

        private void OnDestroy()
        {
            if (_arrow != null) Destroy(_arrow);
            if (_dot != null) Destroy(_dot);
        }

        private void Update()
        {
            if (_input != null && _input.MapPressed && !GameMenu.IsOpen) _full = !_full;

            if (_hasWaypoint && _waypointFollow != null) _waypoint = _waypointFollow.position;

            if (_beam != null && _hasWaypoint)
            {
                _beam.transform.position = new Vector3(_waypoint.x, 0f, _waypoint.z);

                // La colonne s'efface de près : à cinq mètres, on n'a plus besoin qu'on nous
                // montre le chemin, et elle cacherait la bagarre.
                float distance = _player != null ? Flat(_player.position - _waypoint) : 100f;
                bool show = distance > 9f;
                if (_beam.activeSelf != show) _beam.SetActive(show);
            }
        }

        // ------------------------------------------------------------------ dessin

        private bool Hidden
        {
            get
            {
                return GameMenu.IsOpen || FightIntro.AnyPlaying || (_phone != null && _phone.CameraMode) ||
                       DoorPortal.AnyPassing;
            }
        }

        private void OnGUI()
        {
            if (Event.current.type != EventType.Repaint || Hidden || _player == null) return;

            EnsureStyles();

            float unit = Screen.height / 1080f;
            DrawMarker(unit);

            if (_full) DrawFull(unit);
            else DrawMini(unit);
        }

        private void DrawMini(float unit)
        {
            float size = _miniSize * unit;
            Rect frame = new Rect(Screen.width - size - 24f * unit, 24f * unit, size, size);

            GuiKit.Fill(new Rect(frame.x - 3f, frame.y - 3f, frame.width + 6f, frame.height + 6f), new Color(0f, 0f, 0f, 0.55f));

            float scale = _miniScale * unit;
            Vector2 center = new Vector2(_player.position.x, _player.position.z);

            GUI.BeginGroup(frame);
            Rect local = new Rect(0f, 0f, frame.width, frame.height);
            GuiKit.Fill(local, Water);
            DrawCity(local, center, scale);

            if (_hasWaypoint)
            {
                Vector2 p = ToMap(local, center, scale, new Vector2(_waypoint.x, _waypoint.z));
                Vector2 clamped = new Vector2(Mathf.Clamp(p.x, 8f, local.width - 8f), Mathf.Clamp(p.y, 8f, local.height - 8f));
                DrawDot(clamped, 12f * unit, _waypointColor);
            }

            DrawPlayer(new Vector2(local.width * 0.5f, local.height * 0.5f), 20f * unit);
            GUI.EndGroup();

            if (_hasWaypoint)
            {
                float distance = Flat(_player.position - _waypoint);
                GuiKit.OutlinedLabel(new Rect(frame.x, frame.yMax + 4f * unit, frame.width, 22f * unit),
                    Mathf.RoundToInt(distance) + " m  ·  " + _waypointLabel, _small, _waypointColor, Color.black, 1f);
            }

            GuiKit.OutlinedLabel(new Rect(frame.x, frame.yMax + (_hasWaypoint ? 24f : 4f) * unit, frame.width, 20f * unit),
                "M  carte", _small, new Color(1f, 1f, 1f, 0.55f), Color.black, 1f);
        }

        private void DrawFull(float unit)
        {
            GuiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0f, 0f, 0f, 0.72f));

            float margin = 70f * unit;
            float scale = Mathf.Min((Screen.width - margin * 2f) / _bounds.width, (Screen.height - margin * 2f - 40f * unit) / _bounds.height);
            Rect area = new Rect((Screen.width - _bounds.width * scale) * 0.5f, margin + 30f * unit,
                _bounds.width * scale, _bounds.height * scale);

            GuiKit.OutlinedLabel(new Rect(0f, margin * 0.35f, Screen.width, 40f * unit), "LA VILLE",
                _title, Color.white, Color.black, 1f);

            GUI.BeginGroup(area);
            Rect local = new Rect(0f, 0f, area.width, area.height);
            GuiKit.Fill(local, Water);
            DrawCity(local, _bounds.center, scale);

            for (int i = 0; i < _landmarks.Length; i++)
            {
                Landmark l = _landmarks[i];
                if (l == null) continue;

                Vector2 p = ToMap(local, _bounds.center, scale, l.position);
                DrawDot(p, 10f * unit, l.color);
                GuiKit.OutlinedLabel(new Rect(p.x + 8f * unit, p.y - 10f * unit, 260f * unit, 20f * unit), l.label,
                    _label, l.color, Color.black, 1f);
            }

            if (_hasWaypoint)
            {
                Vector2 p = ToMap(local, _bounds.center, scale, new Vector2(_waypoint.x, _waypoint.z));
                DrawDot(p, 16f * unit, _waypointColor);
                GuiKit.OutlinedLabel(new Rect(p.x + 10f * unit, p.y + 4f * unit, 300f * unit, 20f * unit), _waypointLabel,
                    _label, _waypointColor, Color.black, 1f);
            }

            DrawPlayer(ToMap(local, _bounds.center, scale, new Vector2(_player.position.x, _player.position.z)), 22f * unit);
            GUI.EndGroup();

            GuiKit.OutlinedLabel(new Rect(0f, area.yMax + 10f * unit, Screen.width, 22f * unit),
                "M  fermer la carte", _small, new Color(1f, 1f, 1f, 0.6f), Color.black, 1f);
        }

        private void DrawCity(Rect local, Vector2 center, float scale)
        {
            for (int i = 0; i < _blocks.Length; i++) FillWorld(local, center, scale, _blocks[i], Block);
            for (int i = 0; i < _parks.Length; i++) FillWorld(local, center, scale, _parks[i], Park);
            for (int i = 0; i < _roads.Length; i++) FillWorld(local, center, scale, _roads[i], Road);
        }

        private void FillWorld(Rect local, Vector2 center, float scale, Rect world, Color color)
        {
            Vector2 a = ToMap(local, center, scale, new Vector2(world.xMin, world.yMax));
            Vector2 b = ToMap(local, center, scale, new Vector2(world.xMax, world.yMin));
            Rect r = Rect.MinMaxRect(a.x, a.y, b.x, b.y);
            if (r.xMax < 0f || r.yMax < 0f || r.xMin > local.width || r.yMin > local.height) return;
            GuiKit.Fill(r, color);
        }

        /// <summary>Monde (x, z) vers la carte : le nord (z+) en haut.</summary>
        private static Vector2 ToMap(Rect local, Vector2 center, float scale, Vector2 world)
        {
            Vector2 d = (world - center) * scale;
            return new Vector2(local.width * 0.5f + d.x, local.height * 0.5f - d.y);
        }

        private void DrawPlayer(Vector2 at, float size)
        {
            Transform view = _camera != null ? _camera.transform : _player;
            Vector3 f = view.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;

            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(yaw, at);
            GUI.DrawTexture(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), _arrow);
            GUI.matrix = saved;
        }

        private void DrawDot(Vector2 at, float size, Color color)
        {
            Color saved = GUI.color;
            GUI.color = Color.black;
            GUI.DrawTexture(new Rect(at.x - size * 0.5f - 2f, at.y - size * 0.5f - 2f, size + 4f, size + 4f), _dot);
            GUI.color = color;
            GUI.DrawTexture(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), _dot);
            GUI.color = saved;
        }

        /// <summary>Le repère du GPS à l'écran : là où est la cible, ou au bord, dans sa direction.</summary>
        private void DrawMarker(float unit)
        {
            if (!_hasWaypoint || _full) return;

            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return;

            Vector3 world = _waypoint + Vector3.up * 2.1f;
            Vector3 screen = camera.WorldToScreenPoint(world);
            bool behind = screen.z < 0f;
            if (behind) screen = -screen;

            float x = screen.x;
            float y = Screen.height - screen.y;
            float margin = 40f * unit;

            bool inside = !behind && x > margin && x < Screen.width - margin && y > margin && y < Screen.height - margin;
            if (!inside)
            {
                Vector2 c = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
                Vector2 d = new Vector2(x, y) - c;
                if (behind) d = -d;
                if (d.sqrMagnitude < 1f) d = Vector2.up;
                float k = Mathf.Min((Screen.width * 0.5f - margin) / Mathf.Max(1e-3f, Mathf.Abs(d.x)),
                    (Screen.height * 0.5f - margin) / Mathf.Max(1e-3f, Mathf.Abs(d.y)));
                x = c.x + d.x * k;
                y = c.y + d.y * k;
            }

            float size = 18f * unit;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(x, y));
            GuiKit.Fill(new Rect(x - size * 0.5f - 2f, y - size * 0.5f - 2f, size + 4f, size + 4f), Color.black);
            GuiKit.Fill(new Rect(x - size * 0.5f, y - size * 0.5f, size, size), _waypointColor);
            GUI.matrix = saved;

            float distance = Flat(_player.position - _waypoint);
            GuiKit.OutlinedLabel(new Rect(x - 80f * unit, y + size * 0.7f, 160f * unit, 20f * unit),
                Mathf.RoundToInt(distance) + " m", _small, _waypointColor, Color.black, 1f);
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        private void EnsureStyles()
        {
            if (_label == null) _label = GuiKit.Style(13, FontStyle.Bold, TextAnchor.MiddleLeft);
            if (_small == null) _small = GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (_title == null) _title = GuiKit.Style(26, FontStyle.Bold, TextAnchor.MiddleCenter);
            if (_arrow == null) _arrow = BuildArrow(48);
            if (_dot == null) _dot = BuildDot(32);
        }

        /// <summary>Une flèche (pointe vers le haut), tracée pixel par pixel.</summary>
        private static Texture2D BuildArrow(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Coordonnées normalisées, y vers le haut de l'image.
                    float u = (x + 0.5f) / size * 2f - 1f;
                    float v = (y + 0.5f) / size * 2f - 1f;

                    // Triangle pointe en haut (v = 0.9), base échancrée en bas.
                    float half = (0.9f - v) * 0.5f;
                    bool body = v < 0.9f && v > -0.8f && Mathf.Abs(u) < half && !(v < -0.3f && Mathf.Abs(u) < (-0.3f - v) * 0.9f);
                    float edge = body ? Mathf.Clamp01((half - Mathf.Abs(u)) * size * 0.25f) : 0f;

                    byte a = (byte)(body ? 255 : 0);
                    byte c = (byte)(edge < 0.5f ? 20 : 255);
                    pixels[y * size + x] = new Color32(c, c, c, a);
                }
            }

            t.SetPixels32(pixels);
            t.Apply(false, true);
            return t;
        }

        private static Texture2D BuildDot(int size)
        {
            Texture2D t = new Texture2D(size, size, TextureFormat.RGBA32, false);
            t.wrapMode = TextureWrapMode.Clamp;
            Color32[] pixels = new Color32[size * size];
            float r = size * 0.5f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = new Vector2(x + 0.5f - r, y + 0.5f - r).magnitude;
                    byte a = (byte)(Mathf.Clamp01(r - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, a);
                }
            }

            t.SetPixels32(pixels);
            t.Apply(false, true);
            return t;
        }
    }
}
