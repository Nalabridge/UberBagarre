using System.Collections.Generic;
using System;
using UberBagarre.Phone;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La carte de la ville : une mini-carte (nord en haut, le joueur en flèche, les lieux
    /// proches en pictogrammes), la grande carte sur M, et le GPS de la course en cours — un
    /// point sur les deux cartes, un repère à l'écran avec la distance, et une colonne de
    /// lumière au-dessus de la cible, qu'on voit par-dessus les toits.
    ///
    /// Les rues, les îlots et les lieux sont écrits une fois pour toutes par le constructeur de
    /// la ville : la carte dessine ce qui existe vraiment, au mètre près.
    ///
    /// La grande carte (voir CityMap.Full.cs) s'ouvre comme dans GTA : la mini-carte grandit et
    /// recule jusqu'à montrer toute la ville, la liste des lieux glisse à droite. Chaque lieu
    /// est un pictogramme (sa famille en couleur) ; son nom apparaît au survol. Un clic sur un
    /// lieu — sur la carte ou dans la liste — y trace l'itinéraire par les rues.
    /// </summary>
    public partial class CityMap : MonoBehaviour
    {
        [Serializable]
        public class Landmark
        {
            public string label;
            public Vector2 position;
            public Color color = Color.white;

            [Tooltip("Le type du lieu (un ShopKind, « Home »…) : choisit son pictogramme. Vide : deviné d'après le nom.")]
            public string icon;
        }

        [Header("Ville (x, z monde)")]
        [SerializeField] private Rect _bounds = new Rect(-200f, -170f, 400f, 320f);
        [SerializeField] private Rect[] _roads = new Rect[0];
        [SerializeField] private Rect[] _blocks = new Rect[0];
        [SerializeField] private Rect[] _parks = new Rect[0];
        [SerializeField] private Landmark[] _landmarks = new Landmark[0];

        [SerializeField]
        [Tooltip("Optionnel : une vue de dessus de la ville, dessinee sous les rues (la carte convertie).")]
        private Texture2D _background;

        [SerializeField]
        [Tooltip("Rectangle du monde (x, z) couvert par l'image de fond.")]
        private Rect _backgroundBounds;

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

        [Header("Itinéraire")]
        [SerializeField]
        [Tooltip("Les points des rues (les boucles de la circulation, bout à bout).")]
        private Vector3[] _roadPoints = new Vector3[0];

        [SerializeField]
        [Tooltip("L'indice du premier point de chaque boucle dans _roadPoints.")]
        private int[] _roadLoops = new int[0];

        private bool _full;
        private bool _hasWaypoint;
        private Vector3 _waypoint;
        private string _waypointLabel;
        private Color _waypointColor = new Color(1f, 0.82f, 0.25f);
        private Transform _waypointFollow;

        // Le point posé par le joueur (GTA : violet), indépendant du GPS de la course.
        private bool _hasUserPoint;
        private Vector3 _userPoint;
        private string _userPointLabel;
        private static readonly Color UserColor = new Color(0.78f, 0.38f, 1f);

        // La grande carte : zoom et déplacement, et le clic en cours.
        private float _zoom = 1f;
        private Vector2 _pan;
        private bool _dragging;
        private Vector2 _pressAt;
        private float _dragDistance;
        private CursorLockController _cursorLock;

        // Les itinéraires (recalculés quand on bouge ou quand la destination change).
        private RoadGraph _graph;
        private readonly List<Vector2> _userRoute = new List<Vector2>();
        private readonly List<Vector2> _missionRoute = new List<Vector2>();
        private Vector3 _userRouteFrom;
        private Vector3 _missionRouteFrom;
        private Vector3 _userRouteTo;
        private Vector3 _missionRouteTo;
        private float _nextRoute;

        private Texture2D _arrow;
        private Texture2D _dot;
        private GUIStyle _small;

        private static readonly Color Water = new Color(0.05f, 0.06f, 0.08f, 0.88f);
        private static readonly Color Road = new Color(0.36f, 0.37f, 0.40f, 1f);
        private static readonly Color Block = new Color(0.13f, 0.13f, 0.15f, 1f);
        private static readonly Color Park = new Color(0.14f, 0.24f, 0.13f, 1f);

        public bool HasWaypoint { get { return _hasWaypoint; } }

        /// <summary>Où mène le GPS de la course (valable si <see cref="HasWaypoint"/>).</summary>
        public Vector3 WaypointPosition { get { return _waypoint; } }

        /// <summary>Le point posé par le joueur sur la grande carte.</summary>
        public bool HasUserPoint { get { return _hasUserPoint; } }

        /// <summary>La grande carte est ouverte.</summary>
        public bool FullOpen { get { return _full; } }

        /// <summary>
        /// Les horaires des lieux, fournis par qui les connaît (les magasins) : vrai si le lieu
        /// a des horaires ; <paramref name="open"/> dit s'il est ouvert, <paramref name="note"/>
        /// le dit en clair (« Ouvert jusqu'à 22 h », « Fermé · ouvre à 8 h »).
        /// </summary>
        public delegate bool PlaceHours(string label, out bool open, out string note);

        public static PlaceHours Hours;

        public void SetUserPoint(Vector3 world)
        {
            SetUserPoint(world, null);
        }

        /// <summary>Pose son point, avec le nom du lieu visé s'il y en a un.</summary>
        public void SetUserPoint(Vector3 world, string label)
        {
            _hasUserPoint = true;
            _userPoint = world;
            _userPointLabel = label;
            _userRoute.Clear();
            _nextRoute = 0f;
        }

        public void ClearUserPoint()
        {
            _hasUserPoint = false;
            _userPointLabel = null;
            _userRoute.Clear();
        }

        /// <summary>Les rues pour l'itinéraire : les boucles de la circulation.</summary>
        public void SetRoads(IList<Vector3[]> loops)
        {
            List<Vector3> points = new List<Vector3>();
            List<int> starts = new List<int>();
            for (int i = 0; loops != null && i < loops.Count; i++)
            {
                if (loops[i] == null || loops[i].Length < 2) continue;
                starts.Add(points.Count);
                points.AddRange(loops[i]);
            }

            _roadPoints = points.ToArray();
            _roadLoops = starts.ToArray();
            _graph = null;
        }

        /// <summary>Un repère posé par un autre système (la police, un cercle de recherche).</summary>
        public struct Blip
        {
            public Vector2 position;
            public Color color;
            public float size;

            /// <summary>Plus de zéro : un cercle de ce rayon (mètres) au lieu d'un point.</summary>
            public float radius;
        }

        /// <summary>Les repères des autres systèmes : remplis chaque image par qui les pose.</summary>
        public static readonly List<Blip> Overlay = new List<Blip>();

        private void DrawOverlay(Rect local, Vector2 center, float scale, float unit)
        {
            for (int i = 0; i < Overlay.Count; i++)
            {
                Blip b = Overlay[i];
                Vector2 p = ToMap(local, center, scale, b.position);
                if (b.radius > 0f)
                {
                    float r = b.radius * scale;
                    Rect circle = new Rect(p.x - r, p.y - r, r * 2f, r * 2f);
                    GuiKit.Rounded(circle, new Color(b.color.r, b.color.g, b.color.b, b.color.a * 0.18f), r);
                    GuiKit.RoundedOutline(circle, b.color, r, Mathf.Max(1.5f, 2f * unit));
                    continue;
                }

                DrawDot(p, b.size * unit, b.color);
            }
        }

        /// <summary>Le constructeur de la ville y écrit ce qu'il a bâti.</summary>
        public void Configure(Rect bounds, Rect[] roads, Rect[] blocks, Rect[] parks, Landmark[] landmarks)
        {
            _bounds = bounds;
            _roads = roads;
            _blocks = blocks;
            _parks = parks;
            _landmarks = landmarks;
            _placesDirty = true;
        }

        /// <summary>Ajoute des repères (les magasins) à ceux de la ville.</summary>
        public void AddLandmarks(Landmark[] extra)
        {
            if (extra == null || extra.Length == 0) return;
            Landmark[] all = new Landmark[_landmarks.Length + extra.Length];
            _landmarks.CopyTo(all, 0);
            extra.CopyTo(all, _landmarks.Length);
            _landmarks = all;
            _placesDirty = true;
        }

        /// <summary>Une image de la ville vue de dessus, couvrant <paramref name="bounds"/> (x, z monde).</summary>
        public void SetBackground(Texture2D background, Rect bounds)
        {
            _background = background;
            _backgroundBounds = bounds;
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
            _placesDirty = true;
        }

        private void OnDestroy()
        {
            if (_arrow != null) Destroy(_arrow);
            if (_dot != null) Destroy(_dot);
        }

        private void Update()
        {
            if (_input != null && _input.MapPressed && !GameMenu.IsOpen) SetFull(!_full);
            if (_full && (GameMenu.IsOpen || Hidden)) SetFull(false);

            if (_hasWaypoint && _waypointFollow != null) _waypoint = _waypointFollow.position;

            // Arrivé à son point : il s'efface, comme dans GTA.
            if (_hasUserPoint && _player != null && Flat(_player.position - _userPoint) < 9f) ClearUserPoint();

            UpdateRoutes();
            UpdateFull();

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

        /// <summary>
        /// Ouvre ou ferme la grande carte. Ouverte, elle se manipule à la souris : le curseur est
        /// libéré et le joueur ne bouge plus derrière (comme la carte de GTA, qui met en pause).
        /// </summary>
        private void SetFull(bool full)
        {
            if (_full == full) return;
            _full = full;
            _dragging = false;

            if (_cursorLock == null) _cursorLock = FindAnyObjectByType<CursorLockController>();
            if (full)
            {
                // On rouvre toujours sur toute la ville (l'animation part de la mini-carte).
                if (_anim < 0.01f)
                {
                    _zoom = 1f;
                    _pan = Vector2.zero;
                    _hasFocus = false;
                }

                if (_cursorLock != null)
                {
                    _cursorLock.enabled = false;
                    _cursorLock.SetLocked(false);
                }

                if (_input != null) _input.SetGameplayLock(this, true);
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
                _placesDirty |= _places.Count == 0;
                _nextPlaces = 0f;
            }
            else
            {
                if (_input != null) _input.SetGameplayLock(this, false);
                if (_cursorLock != null)
                {
                    _cursorLock.enabled = true;
                    _cursorLock.SetLocked(true);
                }
            }
        }

        private void OnDisable()
        {
            if (_full) SetFull(false);
            _anim = 0f;
        }

        // ------------------------------------------------------------------ itinéraires

        private void UpdateRoutes()
        {
            if (_player == null || _roadPoints == null || _roadPoints.Length < 2) return;
            if (Time.unscaledTime < _nextRoute) return;
            _nextRoute = Time.unscaledTime + 0.5f;

            Vector3 from = _player.position;
            if (_hasUserPoint) Route(_userRoute, ref _userRouteFrom, ref _userRouteTo, from, _userPoint);
            else _userRoute.Clear();

            if (_hasWaypoint) Route(_missionRoute, ref _missionRouteFrom, ref _missionRouteTo, from, _waypoint);
            else _missionRoute.Clear();
        }

        /// <summary>Recalcule un itinéraire si on s'est éloigné du dernier départ, ou si la destination a bougé.</summary>
        private void Route(List<Vector2> route, ref Vector3 lastFrom, ref Vector3 lastTo, Vector3 from, Vector3 to)
        {
            bool stale = route.Count == 0 || Flat(from - lastFrom) > 12f || Flat(to - lastTo) > 6f;
            if (!stale) return;

            if (_graph == null) _graph = new RoadGraph(_roadPoints, _roadLoops);
            lastFrom = from;
            lastTo = to;
            _graph.Find(new Vector2(from.x, from.z), new Vector2(to.x, to.z), route);
        }

        /// <summary>La longueur d'un itinéraire, en mètres.</summary>
        private static float RouteLength(List<Vector2> route)
        {
            float total = 0f;
            for (int i = 1; i < route.Count; i++) total += Vector2.Distance(route[i - 1], route[i]);
            return total;
        }

        // ------------------------------------------------------------------ dessin

        private bool Hidden
        {
            get
            {
                return GameMenu.IsOpen || FightIntro.AnyPlaying || (_phone != null && _phone.CameraMode) ||
                       DoorPortal.AnyPassing || FullScreenPanel.AnyOpen || UberBagarre.View.ShotCamera.Active;
            }
        }

        private void OnGUI()
        {
            if (Hidden || _player == null) return;

            EnsureStyles();
            float unit = Screen.height / 1080f;
            bool showFull = _anim > 0.001f;

            // La grande carte écoute la souris ; le reste ne fait que se dessiner.
            if (Event.current.type != EventType.Repaint)
            {
                if (_full && _anim > 0.6f && Event.current.type != EventType.Layout) HandleFullInput(unit);
                return;
            }

            if (!showFull)
            {
                DrawMarker(unit, _hasWaypoint, _waypoint, _waypointColor);
                DrawMarker(unit, _hasUserPoint, _userPoint, UserColor);
                DrawMini(unit);
                return;
            }

            DrawFull(unit);
        }

        /// <summary>Le cadre de la mini-carte (la grande carte s'ouvre à partir de lui).</summary>
        private Rect MiniFrame(float unit)
        {
            float size = _miniSize * unit;
            return new Rect(Screen.width - size - 24f * unit, 24f * unit, size, size);
        }

        private void DrawMini(float unit)
        {
            Rect frame = MiniFrame(unit);

            GuiKit.Glow(frame, new Color(0f, 0f, 0f, 0.4f), 4f * unit, 10f * unit);
            GuiKit.Fill(new Rect(frame.x - 2f, frame.y - 2f, frame.width + 4f, frame.height + 4f), new Color(0.02f, 0.025f, 0.03f, 0.9f));

            float scale = _miniScale * unit;
            Vector2 center = new Vector2(_player.position.x, _player.position.z);

            GUI.BeginGroup(frame);
            Rect local = new Rect(0f, 0f, frame.width, frame.height);
            GuiKit.Fill(local, Water);
            DrawCity(local, center, scale);
            DrawRoute(local, center, scale, _missionRoute, _waypointColor, 4f * unit);
            DrawRoute(local, center, scale, _userRoute, UserColor, 4f * unit);
            DrawOverlay(local, center, scale, unit);
            DrawMiniPlaces(local, center, scale, unit);

            if (_hasWaypoint)
            {
                Vector2 p = ToMap(local, center, scale, new Vector2(_waypoint.x, _waypoint.z));
                Vector2 clamped = new Vector2(Mathf.Clamp(p.x, 8f, local.width - 8f), Mathf.Clamp(p.y, 8f, local.height - 8f));
                DrawDiamond(clamped, 13f * unit, _waypointColor);
            }

            if (_hasUserPoint)
            {
                Vector2 p = ToMap(local, center, scale, new Vector2(_userPoint.x, _userPoint.z));
                Vector2 clamped = new Vector2(Mathf.Clamp(p.x, 8f, local.width - 8f), Mathf.Clamp(p.y, 8f, local.height - 8f));
                DrawDot(clamped, 12f * unit, UserColor);
            }

            DrawPlayer(new Vector2(local.width * 0.5f, local.height * 0.5f), 20f * unit);

            // Le nord, sur le bord haut.
            Rect north = new Rect(local.width * 0.5f - 9f * unit, 3f * unit, 18f * unit, 18f * unit);
            GuiKit.Rounded(north, new Color(0f, 0f, 0f, 0.6f), 9f * unit);
            UiTheme.Label(north, "N", UiTheme.Text(11f, GuiKit.Weight.Black, TextAnchor.MiddleCenter), UiTheme.Ink);
            GUI.EndGroup();

            float line = frame.yMax + 6f * unit;
            if (_hasWaypoint)
            {
                line = MiniTag(frame, line, unit, _waypointColor, _waypointLabel, Flat(_player.position - _waypoint));
            }

            if (_hasUserPoint)
            {
                string label = string.IsNullOrEmpty(_userPointLabel) ? "Ton point" : _userPointLabel;
                line = MiniTag(frame, line, unit, UserColor, label, Flat(_player.position - _userPoint));
            }

            UiTheme.KeyHint(frame.x, line + 2f * unit, "M", "Carte", unit);
        }

        /// <summary>Une étiquette sous la mini-carte : une pastille de couleur, la destination, la distance.</summary>
        private float MiniTag(Rect frame, float y, float unit, Color color, string label, float distance)
        {
            Rect tag = new Rect(frame.x, y, frame.width, 24f * unit);
            GuiKit.Rounded(tag, new Color(0.03f, 0.035f, 0.045f, 0.82f), 4f * unit);
            GuiKit.Fill(new Rect(tag.x, tag.y, 3f * unit, tag.height), color);
            GUIStyle text = UiTheme.Text(12.5f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            GUIStyle dist = UiTheme.Text(12.5f, GuiKit.Weight.Bold, TextAnchor.MiddleRight);
            UiTheme.Label(new Rect(tag.x + 10f * unit, tag.y, tag.width - 70f * unit, tag.height), Ellipsis(label, text, tag.width - 74f * unit), text, UiTheme.Ink);
            UiTheme.Label(new Rect(tag.x, tag.y, tag.width - 8f * unit, tag.height), FormatDistance(distance), dist, color);
            return tag.yMax + 4f * unit;
        }

        /// <summary>Les lieux autour du joueur, en petits pictogrammes, sur la mini-carte.</summary>
        private void DrawMiniPlaces(Rect local, Vector2 center, float scale, float unit)
        {
            EnsurePlaces();
            float size = 15f * unit;
            for (int i = 0; i < _places.Count; i++)
            {
                Place place = _places[i];
                Vector2 p = ToMap(local, center, scale, place.Position);
                if (p.x < size || p.y < size || p.x > local.width - size || p.y > local.height - size) continue;
                MapIcons.DrawBadge(p, size, place.Icon, 0.95f, !place.Open);
            }
        }

        /// <summary>Un itinéraire : une ligne épaisse, segment par segment.</summary>
        private static void DrawRoute(Rect local, Vector2 center, float scale, List<Vector2> route, Color color, float width)
        {
            if (route == null || route.Count < 2) return;
            Color shadow = new Color(0f, 0f, 0f, 0.6f * color.a);
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 1; i < route.Count; i++)
                {
                    Vector2 a = ToMap(local, center, scale, route[i - 1]);
                    Vector2 b = ToMap(local, center, scale, route[i]);
                    if ((a.x < -50f && b.x < -50f) || (a.y < -50f && b.y < -50f)) continue;
                    if ((a.x > local.width + 50f && b.x > local.width + 50f) || (a.y > local.height + 50f && b.y > local.height + 50f)) continue;
                    Segment(a, b, pass == 0 ? width + 3f : width, pass == 0 ? shadow : color);
                }
            }
        }

        private static void Segment(Vector2 a, Vector2 b, float width, Color color)
        {
            Vector2 d = b - a;
            float length = d.magnitude;
            if (length < 0.5f) return;
            float angle = Mathf.Atan2(d.y, d.x) * Mathf.Rad2Deg;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, a);
            GuiKit.Fill(new Rect(a.x, a.y - width * 0.5f, length + width * 0.4f, width), color);
            GUI.matrix = saved;
        }

        /// <summary>Le point du joueur : une épingle (un rond sur une pointe), façon GTA.</summary>
        private void DrawPin(Vector2 at, float unit)
        {
            float size = 18f * unit;
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, new Vector2(at.x, at.y - size * 0.55f));
            GuiKit.Fill(new Rect(at.x - size * 0.35f, at.y - size * 0.9f, size * 0.7f, size * 0.7f), Color.black);
            GuiKit.Fill(new Rect(at.x - size * 0.28f, at.y - size * 0.83f, size * 0.56f, size * 0.56f), UserColor);
            GUI.matrix = saved;
            DrawDot(new Vector2(at.x, at.y - size * 0.9f), size, UserColor);
            DrawDot(new Vector2(at.x, at.y - size * 0.9f), size * 0.4f, Color.white);
        }

        /// <summary>Le repère de la course : un losange cerclé de noir.</summary>
        private static void DrawDiamond(Vector2 at, float size, Color color)
        {
            Matrix4x4 saved = GUI.matrix;
            GUIUtility.RotateAroundPivot(45f, at);
            GuiKit.Fill(new Rect(at.x - size * 0.5f - 2f, at.y - size * 0.5f - 2f, size + 4f, size + 4f), Color.black);
            GuiKit.Fill(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), color);
            GUI.matrix = saved;
        }

        private void DrawCity(Rect local, Vector2 center, float scale)
        {
            if (_background != null && _backgroundBounds.width > 0f && _backgroundBounds.height > 0f)
            {
                Vector2 a = ToMap(local, center, scale, new Vector2(_backgroundBounds.xMin, _backgroundBounds.yMax));
                Vector2 b = ToMap(local, center, scale, new Vector2(_backgroundBounds.xMax, _backgroundBounds.yMin));
                Color saved = GUI.color;
                GUI.color = new Color(1f, 1f, 1f, GuiKit.Alpha);
                GUI.DrawTexture(Rect.MinMaxRect(a.x, a.y, b.x, b.y), _background, ScaleMode.StretchToFill, false);
                GUI.color = saved;
            }

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

        /// <summary>Carte vers monde (x, z).</summary>
        private static Vector2 FromMap(Rect local, Vector2 center, float scale, Vector2 map)
        {
            return new Vector2(center.x + (map.x - local.width * 0.5f) / scale, center.y - (map.y - local.height * 0.5f) / scale);
        }

        private void DrawPlayer(Vector2 at, float size)
        {
            Transform view = _camera != null ? _camera.transform : _player;
            Vector3 f = view.forward;
            float yaw = Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg;

            Matrix4x4 saved = GUI.matrix;
            Color color = GUI.color;
            GUI.color = new Color(1f, 1f, 1f, GuiKit.Alpha);
            GUIUtility.RotateAroundPivot(yaw, at);
            GUI.DrawTexture(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), _arrow);
            GUI.matrix = saved;
            GUI.color = color;
        }

        private void DrawDot(Vector2 at, float size, Color color)
        {
            Color saved = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, color.a * GuiKit.Alpha);
            GUI.DrawTexture(new Rect(at.x - size * 0.5f - 2f, at.y - size * 0.5f - 2f, size + 4f, size + 4f), _dot);
            GUI.color = new Color(color.r, color.g, color.b, color.a * GuiKit.Alpha);
            GUI.DrawTexture(new Rect(at.x - size * 0.5f, at.y - size * 0.5f, size, size), _dot);
            GUI.color = saved;
        }

        /// <summary>Un repère à l'écran (la course, ou ton point) : là où il est, ou au bord, dans sa direction.</summary>
        private void DrawMarker(float unit, bool has, Vector3 target, Color color)
        {
            if (!has || _anim > 0f) return;

            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return;

            Vector3 world = target + Vector3.up * 2.1f;
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

            DrawDiamond(new Vector2(x, y), 16f * unit, color);

            float distance = Flat(_player.position - target);
            GuiKit.OutlinedLabel(new Rect(x - 80f * unit, y + 13f * unit, 160f * unit, 20f * unit),
                FormatDistance(distance), _small, color, new Color(0f, 0f, 0f, 0.8f), 1f);
        }

        private static float Flat(Vector3 v)
        {
            v.y = 0f;
            return v.magnitude;
        }

        /// <summary>« 80 m », « 1,4 km ».</summary>
        private static string FormatDistance(float meters)
        {
            if (meters < 995f) return (Mathf.Round(meters / 5f) * 5f).ToString("0") + " m";
            return (meters / 1000f).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture).Replace('.', ',') + " km";
        }

        /// <summary>Raccourcit un texte trop long pour sa place (« Quincaillerie de l'E… »).</summary>
        private static string Ellipsis(string text, GUIStyle style, float width)
        {
            if (string.IsNullOrEmpty(text) || style.CalcSize(new GUIContent(text)).x <= width) return text;
            for (int n = text.Length - 1; n > 1; n--)
            {
                string cut = text.Substring(0, n).TrimEnd() + "…";
                if (style.CalcSize(new GUIContent(cut)).x <= width) return cut;
            }

            return text;
        }

        private void EnsureStyles()
        {
            if (_small == null) _small = GuiKit.Style(12, FontStyle.Bold, TextAnchor.MiddleCenter);
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
