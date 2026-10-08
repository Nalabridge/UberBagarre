using System.Collections.Generic;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// La grande carte (M), façon GTA.
    ///
    /// - Elle s'ouvre en animation : la mini-carte grandit jusqu'à occuper l'écran en reculant
    ///   jusqu'à montrer toute la ville, le fond s'assombrit, la liste des lieux glisse depuis la
    ///   droite. Elle se referme de la même façon, à l'envers, plus vite.
    /// - Les lieux ne sont plus écrits en toutes lettres (illisible dès qu'ils se touchent) :
    ///   chacun est un pictogramme dans un rond de la couleur de sa famille. Son nom, ce qu'il
    ///   est, s'il est ouvert et à quelle distance il se trouve apparaissent au survol.
    /// - À droite, la liste des endroits où aller, rangés par famille et du plus proche au plus
    ///   loin, avec des filtres (Manger, Magasins, Services, Loisirs…). Un clic sur un lieu —
    ///   dans la liste ou sur la carte — y pose ton point : la carte s'y rend et l'itinéraire se
    ///   trace par les rues. Un second clic l'enlève.
    /// </summary>
    public partial class CityMap
    {
        private sealed class Place
        {
            public string Label;
            public Vector2 Position;
            public MapIcon Icon;
            public MapGroup Group;
            public bool Explicit;
            public float Distance;
            public bool Open = true;
            public string Note;
        }

        private readonly List<Place> _places = new List<Place>();
        private readonly List<Place> _listed = new List<Place>();
        private bool _placesDirty = true;
        private float _nextPlaces;

        // 0 : fermée (la mini-carte) → 1 : ouverte.
        private float _anim;

        // -1 : tout ; sinon une famille (MapGroup).
        private int _filter = -1;
        private float _scroll;
        private float _scrollTarget;
        private float _scrollMax;

        // Le lieu survolé (sur la carte ou dans la liste) : celui de l'image précédente, et celui
        // qu'on trouve pendant qu'on dessine celle-ci.
        private Place _hover;
        private Place _hoverNext;

        // L'animation vers un lieu choisi dans la liste.
        private bool _hasFocus;
        private float _focusZoom;
        private Vector2 _focusPan;

        private const float MaxZoom = 6f;

        // ------------------------------------------------------------------ les lieux

        private void EnsurePlaces()
        {
            if (!_placesDirty) return;
            _placesDirty = false;
            _places.Clear();

            for (int i = 0; _landmarks != null && i < _landmarks.Length; i++)
            {
                Landmark l = _landmarks[i];
                if (l == null || string.IsNullOrEmpty(l.label)) continue;

                MapIcon icon = MapIcons.Classify(l.label, l.icon);
                bool explicitKind = !string.IsNullOrEmpty(l.icon);

                // La carte et les magasins nomment parfois le même endroit (« Pizzeria » et
                // « Pizzeria Mamma ») : un seul repère, au nom du magasin.
                Place twin = null;
                for (int k = 0; k < _places.Count; k++)
                {
                    Place p = _places[k];
                    if (p.Icon != icon || (p.Explicit && explicitKind)) continue;
                    if ((p.Position - l.position).sqrMagnitude > 38f * 38f) continue;
                    twin = p;
                    break;
                }

                if (twin != null)
                {
                    if (explicitKind && !twin.Explicit)
                    {
                        twin.Label = l.label;
                        twin.Position = l.position;
                        twin.Explicit = true;
                    }

                    continue;
                }

                _places.Add(new Place
                {
                    Label = l.label,
                    Position = l.position,
                    Icon = icon,
                    Group = MapIcons.GroupOf(icon),
                    Explicit = explicitKind
                });
            }

            _nextPlaces = 0f;
        }

        /// <summary>Distances, horaires, et la liste triée (par famille, du plus proche au plus loin).</summary>
        private void RefreshPlaces()
        {
            EnsurePlaces();
            if (_player == null || Time.unscaledTime < _nextPlaces) return;
            _nextPlaces = Time.unscaledTime + (_full ? 0.4f : 1.5f);

            Vector2 me = new Vector2(_player.position.x, _player.position.z);
            for (int i = 0; i < _places.Count; i++)
            {
                Place p = _places[i];
                p.Distance = Vector2.Distance(me, p.Position);
                p.Open = true;
                p.Note = null;

                bool open;
                string note;
                if (Hours != null && Hours(p.Label, out open, out note))
                {
                    p.Open = open;
                    p.Note = note;
                }
            }

            _listed.Clear();
            for (int i = 0; i < _places.Count; i++)
            {
                if (_filter < 0 || (int)_places[i].Group == _filter) _listed.Add(_places[i]);
            }

            _listed.Sort((a, b) =>
            {
                int g = ((int)a.Group).CompareTo((int)b.Group);
                return g != 0 ? g : a.Distance.CompareTo(b.Distance);
            });
        }

        private bool Visible(Place place)
        {
            return _filter < 0 || (int)place.Group == _filter;
        }

        private bool IsTarget(Place place)
        {
            return _hasUserPoint && _userPointLabel == place.Label;
        }

        // ------------------------------------------------------------------ animation

        /// <summary>L'ouverture, adoucie : vive au départ, posée à l'arrivée (et l'inverse en refermant).</summary>
        private float Eased
        {
            get { return _full ? UiTheme.EaseOut(_anim) : 1f - UiTheme.EaseOut(1f - _anim); }
        }

        private void UpdateFull()
        {
            float dt = Mathf.Min(Time.unscaledDeltaTime, 0.1f);
            _anim = Mathf.MoveTowards(_anim, _full ? 1f : 0f, dt / (_full ? 0.5f : 0.28f));
            if (_anim <= 0f) _hover = null;

            RefreshPlaces();

            if (_hasFocus)
            {
                float k = 1f - Mathf.Exp(-dt * 7f);
                _zoom = Mathf.Lerp(_zoom, _focusZoom, k);
                _pan = Vector2.Lerp(_pan, _focusPan, k);
                if (Mathf.Abs(_zoom - _focusZoom) < 0.005f && (_pan - _focusPan).sqrMagnitude < 0.04f) _hasFocus = false;
            }

            _scrollTarget = Mathf.Clamp(_scrollTarget, 0f, _scrollMax);
            _scroll = Mathf.Lerp(_scroll, _scrollTarget, 1f - Mathf.Exp(-dt * 18f));
        }

        // ------------------------------------------------------------------ mise en page

        private static float PanelWidth(float unit)
        {
            return Mathf.Clamp(Screen.width * 0.25f, 340f * unit, 430f * unit);
        }

        private static Rect PanelRect(float unit)
        {
            float m = 28f * unit;
            float w = PanelWidth(unit);
            return new Rect(Screen.width - w - m, m, w, Screen.height - m * 2f);
        }

        private static Rect MapRegion(float unit)
        {
            float m = 28f * unit;
            Rect panel = PanelRect(unit);
            return new Rect(m, m, panel.x - m * 2f + 8f * unit, Screen.height - m * 2f);
        }

        private float FitScale(Rect area)
        {
            return Mathf.Min(area.width / Mathf.Max(1f, _bounds.width), area.height / Mathf.Max(1f, _bounds.height));
        }

        /// <summary>Le décalage qui centre <paramref name="wanted"/> sans montrer le vide au-delà de la ville.</summary>
        private Vector2 ClampedPan(Vector2 wanted, float zoom, Rect area)
        {
            float scale = FitScale(area) * zoom;
            Vector2 half = new Vector2(area.width, area.height) * 0.5f / scale;
            float minX = _bounds.xMin + half.x, maxX = _bounds.xMax - half.x;
            float minY = _bounds.yMin + half.y, maxY = _bounds.yMax - half.y;
            Vector2 c = new Vector2(
                minX > maxX ? _bounds.center.x : Mathf.Clamp(wanted.x, minX, maxX),
                minY > maxY ? _bounds.center.y : Mathf.Clamp(wanted.y, minY, maxY));
            return c - _bounds.center;
        }

        /// <summary>
        /// Où et à quelle échelle dessiner la carte, à l'avancement <paramref name="e"/> de
        /// l'ouverture : du cadre de la mini-carte (centrée sur le joueur) à toute la ville.
        /// </summary>
        private void View(float unit, float e, out Rect rect, out float scale, out Vector2 center)
        {
            Rect full = MapRegion(unit);
            Rect mini = MiniFrame(unit);
            float fullScale = FitScale(full) * _zoom;
            float miniScale = _miniScale * unit;

            rect = new Rect(
                Mathf.Lerp(mini.x, full.x, e), Mathf.Lerp(mini.y, full.y, e),
                Mathf.Lerp(mini.width, full.width, e), Mathf.Lerp(mini.height, full.height, e));
            scale = Mathf.Exp(Mathf.Lerp(Mathf.Log(miniScale), Mathf.Log(fullScale), e));
            center = Vector2.Lerp(new Vector2(_player.position.x, _player.position.z), _bounds.center + _pan, e);
        }

        private float BadgeSize(float unit, float e)
        {
            float zoomBoost = Mathf.Lerp(1f, 1.35f, Mathf.InverseLerp(1f, MaxZoom, _zoom));
            return Mathf.Lerp(15f, 25f, e) * unit * zoomBoost;
        }

        /// <summary>Le lieu sous le curseur (coordonnées de la carte), le plus proche dans un rayon d'un repère.</summary>
        private Place PlaceAt(Rect local, Vector2 center, float scale, Vector2 mouse, float badge)
        {
            Place best = null;
            float bestDistance = badge * 0.75f;
            for (int i = 0; i < _places.Count; i++)
            {
                if (!Visible(_places[i])) continue;
                float d = (ToMap(local, center, scale, _places[i].Position) - mouse).magnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = _places[i];
            }

            return best;
        }

        // ------------------------------------------------------------------ actions

        /// <summary>Y aller (ton point, l'itinéraire) — ou annuler si c'est déjà là qu'on va.</summary>
        private void SelectPlace(Place place, bool focus, float unit)
        {
            if (place == null || _player == null) return;
            if (IsTarget(place))
            {
                ClearUserPoint();
                return;
            }

            SetUserPoint(new Vector3(place.Position.x, _player.position.y, place.Position.y), place.Label);
            if (focus) FocusOn(place.Position, unit);
        }

        private void FocusOn(Vector2 world, float unit)
        {
            Rect area = MapRegion(unit);
            _focusZoom = Mathf.Max(_zoom, 2.4f);
            _focusPan = ClampedPan(world, _focusZoom, area);
            _hasFocus = true;
        }

        // ------------------------------------------------------------------ entrées

        private void HandleFullInput(float unit)
        {
            Event e = Event.current;
            Rect panel = PanelRect(unit);
            if (!_dragging && panel.Contains(e.mousePosition))
            {
                Panel(panel, unit, true);
                return;
            }

            HandleMapInput(unit);
        }

        private void HandleMapInput(float unit)
        {
            Event e = Event.current;
            Rect area;
            float scale;
            Vector2 center;
            View(unit, Eased, out area, out scale, out center);
            Rect local = new Rect(0f, 0f, area.width, area.height);
            Vector2 mouse = e.mousePosition - area.position;
            bool over = area.Contains(e.mousePosition);

            switch (e.type)
            {
                case EventType.ScrollWheel:
                    if (!over) break;
                    {
                        // Zoom vers le curseur : le point sous la souris reste sous la souris.
                        _hasFocus = false;
                        Vector2 before = FromMap(local, center, scale, mouse);
                        _zoom = Mathf.Clamp(_zoom * (e.delta.y > 0f ? 0.85f : 1.18f), 1f, MaxZoom);
                        float newScale = FitScale(MapRegion(unit)) * _zoom;
                        Vector2 after = FromMap(local, center, newScale, mouse);
                        _pan = ClampedPan(center + before - after, _zoom, MapRegion(unit));
                    }

                    e.Use();
                    break;

                case EventType.MouseDown:
                    if (!over) break;
                    _dragging = e.button == 0;
                    _pressAt = e.mousePosition;
                    _dragDistance = 0f;
                    if (e.button == 1) ClearUserPoint();
                    e.Use();
                    break;

                case EventType.MouseDrag:
                    if (!_dragging) break;
                    _hasFocus = false;
                    _dragDistance += e.delta.magnitude;
                    _pan = ClampedPan(_bounds.center + _pan + new Vector2(-e.delta.x, e.delta.y) / scale, _zoom, MapRegion(unit));
                    e.Use();
                    break;

                case EventType.MouseUp:
                    if (e.button == 0 && _dragging && _dragDistance < 6f && over)
                    {
                        // Un clic, pas un glissé : sur un lieu, y aller ; ailleurs, poser son point
                        // (ou l'enlever si on clique dessus).
                        Place place = PlaceAt(local, center, scale, mouse, BadgeSize(unit, 1f));
                        if (place != null)
                        {
                            SelectPlace(place, false, unit);
                        }
                        else
                        {
                            Vector2 world = FromMap(local, center, scale, mouse);
                            Vector2 existing = ToMap(local, center, scale, new Vector2(_userPoint.x, _userPoint.z));
                            if (_hasUserPoint && (existing - mouse).magnitude < 14f * unit) ClearUserPoint();
                            else SetUserPoint(new Vector3(world.x, _player.position.y, world.y));
                        }
                    }

                    _dragging = false;
                    e.Use();
                    break;
            }
        }

        // ------------------------------------------------------------------ dessin

        private void DrawFull(float unit)
        {
            float e = Eased;
            _hoverNext = null;

            GuiKit.Fill(new Rect(0f, 0f, Screen.width, Screen.height), new Color(0.01f, 0.015f, 0.02f, 0.82f * e));

            Rect area;
            float scale;
            Vector2 center;
            View(unit, e, out area, out scale, out center);

            GuiKit.Glow(area, new Color(0f, 0f, 0f, 0.5f * e), 6f * unit, 24f * unit);
            GuiKit.Fill(new Rect(area.x - 2f, area.y - 2f, area.width + 4f, area.height + 4f), new Color(0.02f, 0.025f, 0.03f, 0.95f));

            Vector2 mouse = Event.current.mousePosition - area.position;
            bool mouseOnMap = area.Contains(Event.current.mousePosition) && !PanelRect(unit).Contains(Event.current.mousePosition);

            GUI.BeginGroup(area);
            Rect local = new Rect(0f, 0f, area.width, area.height);
            GuiKit.Fill(local, Water);
            DrawCity(local, center, scale);

            float routeWidth = Mathf.Lerp(4f, 5f, e) * unit;
            DrawRoute(local, center, scale, _missionRoute, _waypointColor, routeWidth);
            DrawRoute(local, center, scale, _userRoute, UserColor, routeWidth);
            DrawOverlay(local, center, scale, unit);

            float badge = BadgeSize(unit, e);
            if (mouseOnMap && _anim > 0.6f) _hoverNext = PlaceAt(local, center, scale, mouse, badge);
            DrawPlaces(local, center, scale, unit, badge);

            if (_hasWaypoint)
            {
                Vector2 p = ToMap(local, center, scale, new Vector2(_waypoint.x, _waypoint.z));
                DrawDiamond(p, Mathf.Lerp(13f, 17f, e) * unit, _waypointColor);
                if (e > 0.5f) MapTag(p + new Vector2(0f, -22f * unit), _waypointLabel, _waypointColor, unit);
            }

            if (_hasUserPoint)
            {
                Vector2 p = ToMap(local, center, scale, new Vector2(_userPoint.x, _userPoint.z));
                DrawPin(p, unit);
            }

            Vector2 me = ToMap(local, center, scale, new Vector2(_player.position.x, _player.position.z));
            float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 3f);
            float halo = (16f + 8f * pulse) * unit;
            GuiKit.Rounded(new Rect(me.x - halo, me.y - halo, halo * 2f, halo * 2f), new Color(1f, 1f, 1f, 0.12f * e * (1f - pulse * 0.5f)), halo);
            DrawPlayer(me, Mathf.Lerp(20f, 24f, e) * unit);
            GUI.EndGroup();

            // Ce qui habille la carte n'apparaît qu'une fois qu'elle est ouverte.
            float chrome = Mathf.InverseLerp(0.55f, 1f, e);
            if (chrome > 0f)
            {
                GuiKit.Alpha = chrome;
                DrawCompass(area, unit);
                DrawScaleBar(area, scale, unit);
                DrawMapHints(area, unit);
                GuiKit.Alpha = 1f;
            }

            // La liste glisse depuis la droite.
            Rect panel = PanelRect(unit);
            panel.x += (1f - e) * (panel.width + 60f * unit);
            GuiKit.Alpha = Mathf.Clamp01(e * 1.6f - 0.2f);
            if (GuiKit.Alpha > 0f) Panel(panel, unit, false);
            GuiKit.Alpha = 1f;

            if (_hover != null && chrome > 0f)
            {
                GuiKit.Alpha = chrome;
                Vector2 p = area.position + ToMap(local, center, scale, _hover.Position);
                if (area.Contains(p)) Tooltip(_hover, p, badge, area, unit);
                GuiKit.Alpha = 1f;
            }

            _hover = _hoverNext;
        }

        private void DrawPlaces(Rect local, Vector2 center, float scale, float unit, float badge)
        {
            float margin = badge;
            for (int pass = 0; pass < 2; pass++)
            {
                // Les repères mis en avant (survolé, destination) par-dessus les autres.
                for (int i = 0; i < _places.Count; i++)
                {
                    Place place = _places[i];
                    if (!Visible(place)) continue;
                    bool highlight = place == _hover || IsTarget(place);
                    if (highlight != (pass == 1)) continue;

                    Vector2 p = ToMap(local, center, scale, place.Position);
                    if (p.x < -margin || p.y < -margin || p.x > local.width + margin || p.y > local.height + margin) continue;

                    float size = badge;
                    if (highlight)
                    {
                        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 6f);
                        size *= 1.25f;
                        float ring = size * (0.75f + 0.12f * pulse);
                        Color group = IsTarget(place) ? UserColor : MapIcons.GroupColor(place.Group);
                        GuiKit.RoundedOutline(new Rect(p.x - ring, p.y - ring, ring * 2f, ring * 2f),
                            new Color(group.r, group.g, group.b, 0.85f), ring, 2f * unit);
                    }

                    MapIcons.DrawBadge(p, size, place.Icon, 1f, !place.Open);
                }
            }
        }

        /// <summary>Une petite étiquette sombre au-dessus d'un repère (la course).</summary>
        private void MapTag(Vector2 at, string text, Color color, float unit)
        {
            if (string.IsNullOrEmpty(text)) return;
            GUIStyle style = UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            float w = style.CalcSize(new GUIContent(text)).x + 16f * unit;
            Rect r = new Rect(at.x - w * 0.5f, at.y - 22f * unit, w, 20f * unit);
            GuiKit.Rounded(r, new Color(0.03f, 0.035f, 0.045f, 0.9f), 4f * unit);
            GuiKit.Fill(new Rect(r.x, r.yMax - 2f * unit, r.width, 2f * unit), color);
            UiTheme.Label(r, text, style, UiTheme.Ink);
        }

        private void DrawCompass(Rect area, float unit)
        {
            float s = 34f * unit;
            Rect r = new Rect(area.xMax - s - 16f * unit, area.y + 16f * unit, s, s);
            GuiKit.Rounded(r, new Color(0.03f, 0.035f, 0.045f, 0.85f), s * 0.5f);
            GuiKit.RoundedOutline(r, UiTheme.Line, s * 0.5f, 1f);
            UiTheme.Label(new Rect(r.x, r.y + 1f * unit, r.width, r.height), "N", UiTheme.Text(15f, GuiKit.Weight.Black, TextAnchor.MiddleCenter), UiTheme.Ink);
            GuiKit.Fill(new Rect(r.center.x - 1.5f * unit, r.y - 5f * unit, 3f * unit, 6f * unit), UiTheme.Bad);
        }

        /// <summary>La barre d'échelle (« 100 m »), en bas à droite de la carte.</summary>
        private void DrawScaleBar(Rect area, float scale, float unit)
        {
            float meters = 120f * unit / Mathf.Max(1e-4f, scale);
            float[] nice = { 5f, 10f, 20f, 25f, 50f, 100f, 200f, 250f, 500f, 1000f };
            float pick = nice[0];
            for (int i = 0; i < nice.Length; i++)
            {
                if (nice[i] <= meters) pick = nice[i];
            }

            float length = pick * scale;
            float x = area.xMax - length - 22f * unit;
            float y = area.yMax - 26f * unit;
            Rect back = new Rect(x - 10f * unit, y - 20f * unit, length + 20f * unit, 30f * unit);
            GuiKit.Rounded(back, new Color(0.03f, 0.035f, 0.045f, 0.75f), 5f * unit);
            GuiKit.Fill(new Rect(x, y, length, 2f * unit), UiTheme.Ink);
            GuiKit.Fill(new Rect(x, y - 5f * unit, 2f * unit, 7f * unit), UiTheme.Ink);
            GuiKit.Fill(new Rect(x + length - 2f * unit, y - 5f * unit, 2f * unit, 7f * unit), UiTheme.Ink);
            UiTheme.Label(new Rect(x, y - 19f * unit, length, 16f * unit), pick >= 1000f ? (pick / 1000f).ToString("0") + " km" : pick.ToString("0") + " m",
                UiTheme.Text(11.5f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter), UiTheme.InkDim);
        }

        private void DrawMapHints(Rect area, float unit)
        {
            float y = area.yMax - 38f * unit;
            float x = area.x + 16f * unit;
            Rect back = new Rect(x - 8f * unit, y - 7f * unit, Mathf.Min(area.width - 200f * unit, 660f * unit), 36f * unit);
            GuiKit.Rounded(back, new Color(0.03f, 0.035f, 0.045f, 0.75f), 6f * unit);
            x = UiTheme.KeyHint(x, y, "Clic", "Y aller", unit);
            x = UiTheme.KeyHint(x, y, "Clic droit", "Retirer", unit);
            x = UiTheme.KeyHint(x, y, "Molette", "Zoom", unit);
            x = UiTheme.KeyHint(x, y, "Glisser", "Déplacer", unit);
            UiTheme.KeyHint(x, y, "M", "Fermer", unit);
        }

        /// <summary>La fiche d'un lieu, à côté de son repère.</summary>
        private void Tooltip(Place place, Vector2 at, float badge, Rect area, float unit)
        {
            GUIStyle name = UiTheme.Text(16f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft);
            GUIStyle sub = UiTheme.Text(13f, GuiKit.Weight.Regular, TextAnchor.MiddleLeft);
            GUIStyle hint = UiTheme.Text(12f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);

            string what = MapIcons.Describe(place.Icon);
            string distance = FormatDistance(place.Distance) + "  ·  " + WalkTime(place.Distance);
            string action = IsTarget(place) ? "Clic : retirer l'itinéraire" : "Clic : y aller";

            float w = Mathf.Max(name.CalcSize(new GUIContent(place.Label)).x, sub.CalcSize(new GUIContent(what + "   " + (place.Note ?? string.Empty))).x,
                sub.CalcSize(new GUIContent(distance)).x) + 32f * unit;
            float h = 100f * unit;

            float x = at.x + badge * 0.9f + 8f * unit;
            if (x + w > area.xMax - 8f * unit) x = at.x - badge * 0.9f - 8f * unit - w;
            float y = Mathf.Clamp(at.y - h * 0.5f, area.y + 8f * unit, area.yMax - h - 8f * unit);
            Rect r = new Rect(x, y, w, h);

            GuiKit.Glow(r, new Color(0f, 0f, 0f, 0.45f), 8f * unit, 12f * unit);
            GuiKit.Rounded(r, new Color(0.05f, 0.056f, 0.07f, 0.96f), 8f * unit);
            Color group = MapIcons.GroupColor(place.Group);
            GuiKit.Fill(new Rect(r.x, r.y + 10f * unit, 3f * unit, r.height - 20f * unit), group);

            float tx = r.x + 16f * unit;
            UiTheme.Label(new Rect(tx, r.y + 10f * unit, w, 22f * unit), place.Label, name, UiTheme.Ink);

            float wWhat = sub.CalcSize(new GUIContent(what)).x;
            UiTheme.Label(new Rect(tx, r.y + 34f * unit, wWhat + 4f, 18f * unit), what, sub, group);
            if (!string.IsNullOrEmpty(place.Note))
            {
                UiTheme.Label(new Rect(tx + wWhat + 10f * unit, r.y + 34f * unit, w, 18f * unit), place.Note, sub, place.Open ? UiTheme.Good : UiTheme.Bad);
            }

            UiTheme.Label(new Rect(tx, r.y + 54f * unit, w, 18f * unit), distance, sub, UiTheme.InkDim);
            UiTheme.Label(new Rect(tx, r.y + 75f * unit, w, 16f * unit), action, hint, UiTheme.InkFaint);
        }

        /// <summary>« 3 min à pied » (1,4 m/s, en ville, détours compris).</summary>
        private static string WalkTime(float meters)
        {
            int minutes = Mathf.Max(1, Mathf.RoundToInt(meters * 1.3f / 1.4f / 60f));
            return minutes + " min à pied";
        }

        // ------------------------------------------------------------------ le panneau de droite

        /// <summary>
        /// Le panneau des lieux. Le même code dessine (<paramref name="input"/> faux, pendant le
        /// rendu) et réagit aux clics (vrai) : les zones cliquables tombent exactement là où elles
        /// sont dessinées.
        /// </summary>
        private void Panel(Rect panel, float unit, bool input)
        {
            Event e = Event.current;
            Vector2 mouse = e.mousePosition;
            bool click = input && e.type == EventType.MouseDown && e.button == 0;

            if (!input) UiTheme.DrawPanel(panel, 10f * unit);

            float pad = 22f * unit;
            float x0 = panel.x + pad;
            float w = panel.width - pad * 2f;
            float y = panel.y + 20f * unit;

            // --- en-tête
            if (!input)
            {
                UiTheme.Label(new Rect(x0, y, w, 16f * unit), "CARTE", UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft), UiTheme.Accent);
                UiTheme.Label(new Rect(x0, y + 16f * unit, w, 36f * unit), "Hyland Point", UiTheme.Title(28f), UiTheme.Ink);

                string time = WorldClock.Instance != null ? WorldClock.Instance.Label + "  ·  " : string.Empty;
                int open = 0;
                for (int i = 0; i < _places.Count; i++)
                {
                    if (_places[i].Open) open++;
                }

                UiTheme.Label(new Rect(x0, y + 52f * unit, w, 20f * unit), time + _places.Count + " lieux  ·  " + open + " ouverts",
                    UiTheme.Light(13.5f), UiTheme.InkDim);
            }

            y += 84f * unit;

            // --- les itinéraires en cours
            if (_hasWaypoint)
            {
                y = RouteCard(new Rect(x0, y, w, 58f * unit), unit, input, "COURSE EN COURS", _waypointLabel, _waypointColor,
                    _missionRoute, _waypoint, false) + 8f * unit;
            }

            if (_hasUserPoint)
            {
                y = RouteCard(new Rect(x0, y, w, 58f * unit), unit, input, "TON ITINÉRAIRE",
                    string.IsNullOrEmpty(_userPointLabel) ? "Ton point sur la carte" : _userPointLabel, UserColor, _userRoute, _userPoint, true) + 8f * unit;
            }

            // --- les filtres
            y += 4f * unit;
            float cx = x0;
            float chipH = 28f * unit;
            GUIStyle chipStyle = UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            for (int g = -1; g <= (int)MapGroup.Places; g++)
            {
                string text = g < 0 ? "Tout" : MapIcons.GroupShort((MapGroup)g);
                float tw = chipStyle.CalcSize(new GUIContent(text)).x;
                float cw = tw + (g < 0 ? 24f : 38f) * unit;
                if (cx + cw > x0 + w)
                {
                    cx = x0;
                    y += chipH + 6f * unit;
                }

                Rect chip = new Rect(cx, y, cw, chipH);
                bool selected = _filter == g;
                if (click && chip.Contains(mouse))
                {
                    _filter = g;
                    _scrollTarget = 0f;
                    _nextPlaces = 0f;
                    e.Use();
                    return;
                }

                if (!input)
                {
                    bool hover = chip.Contains(mouse);
                    GuiKit.Rounded(chip, selected ? UiTheme.Ink : (hover ? UiTheme.Hover : UiTheme.Raised), chipH * 0.5f);
                    float tx = chip.x + 12f * unit;
                    if (g >= 0)
                    {
                        Color dot = MapIcons.GroupColor((MapGroup)g);
                        GuiKit.Rounded(new Rect(tx, chip.center.y - 4f * unit, 8f * unit, 8f * unit), dot, 4f * unit);
                        tx += 14f * unit;
                    }

                    UiTheme.Label(new Rect(tx, chip.y, tw + 4f, chip.height), text, chipStyle,
                        selected ? new Color(0.06f, 0.07f, 0.09f) : UiTheme.Ink);
                }

                cx += cw + 6f * unit;
            }

            y += chipH + 14f * unit;
            if (!input) GuiKit.Fill(new Rect(x0, y - 1f, w, 1f), UiTheme.Line);

            // --- la liste
            float footer = 44f * unit;
            Rect view = new Rect(panel.x + 8f * unit, y, panel.width - 16f * unit, panel.yMax - footer - y);
            if (view.height < 40f * unit) return;

            if (input && e.type == EventType.ScrollWheel && view.Contains(mouse))
            {
                _scrollTarget = Mathf.Clamp(_scrollTarget + Mathf.Sign(e.delta.y) * 70f * unit, 0f, _scrollMax);
                e.Use();
                return;
            }

            float rowH = 54f * unit;
            float headH = 32f * unit;
            float content = 0f;
            int lastGroup = -1;
            for (int i = 0; i < _listed.Count; i++)
            {
                int g = (int)_listed[i].Group;
                if (_filter < 0 && g != lastGroup)
                {
                    content += headH;
                    lastGroup = g;
                }

                content += rowH;
            }

            _scrollMax = Mathf.Max(0f, content - view.height + 8f * unit);

            if (!input) GUI.BeginGroup(view);
            Vector2 local = mouse - view.position;
            bool mouseInView = view.Contains(mouse);
            float ry = -_scroll + 4f * unit;
            lastGroup = -1;

            GUIStyle nameStyle = UiTheme.Text(15f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            GUIStyle subStyle = UiTheme.Text(12.5f, GuiKit.Weight.Regular, TextAnchor.MiddleLeft);
            GUIStyle distStyle = UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleRight);
            GUIStyle headStyle = UiTheme.Text(11.5f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft);

            for (int i = 0; i < _listed.Count; i++)
            {
                Place place = _listed[i];
                int g = (int)place.Group;
                if (_filter < 0 && g != lastGroup)
                {
                    lastGroup = g;
                    if (!input && ry + headH > 0f && ry < view.height)
                    {
                        int count = 0;
                        for (int k = 0; k < _listed.Count; k++)
                        {
                            if ((int)_listed[k].Group == g) count++;
                        }

                        Color gc = MapIcons.GroupColor(place.Group);
                        GuiKit.Fill(new Rect(14f * unit, ry + headH * 0.5f + 2f * unit, 3f * unit, 10f * unit), gc);
                        UiTheme.Label(new Rect(24f * unit, ry + 6f * unit, view.width, headH), MapIcons.GroupName(place.Group).ToUpperInvariant(), headStyle, UiTheme.InkDim);
                        UiTheme.Label(new Rect(0f, ry + 6f * unit, view.width - 14f * unit, headH), count.ToString(),
                            UiTheme.Text(11.5f, GuiKit.Weight.Bold, TextAnchor.MiddleRight), UiTheme.InkFaint);
                    }

                    ry += headH;
                }

                Rect row = new Rect(4f * unit, ry, view.width - 12f * unit, rowH - 4f * unit);
                ry += rowH;
                if (row.yMax < 0f || row.y > view.height) continue;

                bool hover = mouseInView && row.Contains(local);
                if (hover) _hoverNext = place;

                if (click && hover)
                {
                    SelectPlace(place, true, unit);
                    e.Use();
                    return;
                }

                if (input) continue;

                bool target = IsTarget(place);
                if (target) GuiKit.Rounded(row, new Color(UserColor.r, UserColor.g, UserColor.b, 0.16f), 6f * unit);
                else if (hover || place == _hover) GuiKit.Rounded(row, UiTheme.Hover, 6f * unit);
                if (target) GuiKit.Fill(new Rect(row.x, row.y + 8f * unit, 3f * unit, row.height - 16f * unit), UserColor);

                float bs = 30f * unit;
                MapIcons.DrawBadge(new Vector2(row.x + 12f * unit + bs * 0.5f, row.center.y), bs, place.Icon, 1f, !place.Open);

                float tx = row.x + 12f * unit + bs + 14f * unit;
                float dw = 70f * unit;
                float tw = row.xMax - tx - dw;
                UiTheme.Label(new Rect(tx, row.y + 6f * unit, tw, 22f * unit), Ellipsis(place.Label, nameStyle, tw), nameStyle,
                    place.Open ? UiTheme.Ink : UiTheme.InkDim);

                string what = MapIcons.Describe(place.Icon);
                float ww = subStyle.CalcSize(new GUIContent(what)).x;
                UiTheme.Label(new Rect(tx, row.y + 27f * unit, ww + 4f, 18f * unit), what, subStyle, UiTheme.InkFaint);
                if (!string.IsNullOrEmpty(place.Note))
                {
                    float nx = tx + ww + 6f * unit;
                    UiTheme.Label(new Rect(nx, row.y + 27f * unit, Mathf.Max(0f, tw - ww - 6f * unit), 18f * unit),
                        Ellipsis("· " + place.Note, subStyle, Mathf.Max(0f, tw - ww - 6f * unit)), subStyle, place.Open ? UiTheme.Good : UiTheme.Bad);
                }

                UiTheme.Label(new Rect(row.xMax - dw, row.y, dw - 10f * unit, row.height), FormatDistance(place.Distance), distStyle,
                    target ? UserColor : UiTheme.InkDim);
            }

            if (!input)
            {
                GUI.EndGroup();

                // La barre de défilement, fine, quand la liste dépasse.
                if (_scrollMax > 1f)
                {
                    float track = view.height - 8f * unit;
                    float thumb = Mathf.Max(30f * unit, track * view.height / (content + 8f * unit));
                    float ty = view.y + 4f * unit + (track - thumb) * (_scroll / _scrollMax);
                    GuiKit.Rounded(new Rect(view.xMax - 4f * unit, ty, 3f * unit, thumb), new Color(1f, 1f, 1f, 0.22f), 1.5f * unit);
                }

                if (_listed.Count == 0)
                {
                    UiTheme.Label(new Rect(view.x, view.y + 20f * unit, view.width, 20f * unit), "Aucun lieu dans cette catégorie.",
                        UiTheme.Text(13.5f, GuiKit.Weight.Regular, TextAnchor.MiddleCenter), UiTheme.InkFaint);
                }

                GuiKit.Fill(new Rect(x0, panel.yMax - footer, w, 1f), UiTheme.Line);
                UiTheme.Label(new Rect(x0, panel.yMax - footer, w, footer), "Clique un lieu pour y tracer l'itinéraire.",
                    UiTheme.Text(12.5f, GuiKit.Weight.Regular, TextAnchor.MiddleLeft), UiTheme.InkFaint);
            }
        }

        /// <summary>Une carte « itinéraire » : où l'on va, à quelle distance par les rues, en combien de temps.</summary>
        private float RouteCard(Rect r, float unit, bool input, string caption, string label, Color color, List<Vector2> route,
            Vector3 target, bool removable)
        {
            Event e = Event.current;
            Rect close = new Rect(r.xMax - 34f * unit, r.y + (r.height - 26f * unit) * 0.5f, 26f * unit, 26f * unit);
            if (input)
            {
                if (removable && e.type == EventType.MouseDown && e.button == 0 && close.Contains(e.mousePosition))
                {
                    ClearUserPoint();
                    e.Use();
                }

                return r.yMax;
            }

            GuiKit.Rounded(r, UiTheme.Raised, 8f * unit);
            GuiKit.Fill(new Rect(r.x, r.y + 10f * unit, 3f * unit, r.height - 20f * unit), color);

            float meters = route.Count > 1 ? RouteLength(route) : Flat(_player.position - target);
            UiTheme.Label(new Rect(r.x + 16f * unit, r.y + 8f * unit, r.width, 16f * unit), caption,
                UiTheme.Text(11f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft), color);
            GUIStyle name = UiTheme.Text(15f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft);
            float nw = r.width - 16f * unit - (removable ? 44f : 12f) * unit - 110f * unit;
            UiTheme.Label(new Rect(r.x + 16f * unit, r.y + 26f * unit, nw, 22f * unit), Ellipsis(label, name, nw), name, UiTheme.Ink);

            float right = removable ? close.x - 8f * unit : r.xMax - 12f * unit;
            UiTheme.Label(new Rect(r.x, r.y + 8f * unit, right - r.x, 20f * unit), FormatDistance(meters),
                UiTheme.Text(14f, GuiKit.Weight.Bold, TextAnchor.MiddleRight), UiTheme.Ink);
            UiTheme.Label(new Rect(r.x, r.y + 28f * unit, right - r.x, 18f * unit), WalkTime(meters / 1.3f),
                UiTheme.Text(12f, GuiKit.Weight.Regular, TextAnchor.MiddleRight), UiTheme.InkDim);

            if (removable)
            {
                bool hover = close.Contains(Event.current.mousePosition);
                GuiKit.Rounded(close, hover ? new Color(1f, 1f, 1f, 0.16f) : new Color(1f, 1f, 1f, 0.07f), 13f * unit);
                UiTheme.Label(close, "×", UiTheme.Text(18f, GuiKit.Weight.Medium, TextAnchor.MiddleCenter), UiTheme.Ink);
            }

            return r.yMax;
        }
    }
}
