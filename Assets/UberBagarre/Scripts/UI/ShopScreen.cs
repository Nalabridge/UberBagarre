using System;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.View;
using UberBagarre.World;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UberBagarre.UI
{
    /// <summary>
    /// Le comptoir d'un magasin, comme dans GTA : on parle au vendeur, la caméra quitte nos yeux
    /// pour cadrer la personne derrière le comptoir (accoudée, à gauche de l'image), et un menu
    /// glisse à droite. Chaque achat est une petite scène : on commande (« Je te prends un
    /// burger. »), on nous répond avec le prix, la caisse sonne.
    ///
    /// Les commerces particuliers ont leur propre façon de faire :
    /// - le barbier : la caméra nous regarde en face, comme dans le miroir ; on essaie chaque
    ///   coupe, chaque barbe, chaque couleur en direct avant de payer ;
    /// - les vêtements : la caméra recule, on se voit en pied, on essaie avant d'acheter ;
    /// - la concession, l'agence immobilière, le casino, la salle de boxe.
    ///
    /// Échap (ou « Partir ») : on dit au revoir, la caméra revient dans nos yeux.
    /// </summary>
    public class ShopScreen : FullScreenPanel
    {
        [SerializeField] private VehicleCatalog _vehicles;
        [SerializeField] private HomeRegistry _homes;
        [SerializeField] private ComputerScreen _computer;
        [SerializeField] private SubtitleDisplay _subtitles;

        private enum Mode
        {
            Items,
            Barber,
            Clothes,
            Cars,
            Homes,
            Casino
        }

        private enum Phase
        {
            Arriving,
            Browsing,
            Talking,
            Leaving
        }

        private Shop _shop;
        private ShopClerk _clerk;
        private ShopItem[] _items = new ShopItem[0];
        private Mode _mode;
        private Phase _phase;
        private float _phaseTime;
        private int _tab;
        private bool _bought;
        private int _seed;

        private string _feedback;
        private bool _feedbackBad;
        private float _feedbackUntil;
        private string _note;

        // L'échange en cours : la réponse du comptoir arrive après la commande.
        private float _replyAt = -1f;
        private Action _reply;
        private float _browseAt;

        // Le barbier : le rideau noir pendant la coupe.
        private float _curtain = -1f;
        private Action _curtainAction;

        // L'aperçu (barbier, vêtements).
        private Transform _player;
        private Camera _playerCamera;
        private PlayerWardrobe _wardrobe;
        private int _previewKey = -1;
        private bool _wholeBody;

        // Casino.
        private static readonly string[] Symbols = { "7", "BAR", "CERISE", "CLOCHE", "CITRON" };
        private readonly int[] _reels = { 0, 1, 2 };
        private float _spinUntil;
        private int _spinStake;
        private int _rouletteNumber = -1;

        private static readonly string[] BarberTabs = { "Coupe", "Barbe", "Couleur", "Discuter" };
        private static readonly string[] ClothesTabs = { "Haut", "Couleur", "Pantalon", "Chaussures" };

        protected override string Title { get { return _shop != null ? _shop.DisplayName : "Magasin"; } }

        protected override bool CustomFrame { get { return true; } }

        protected override Rect DrawFrame(float sw, float sh, float u)
        {
            return new Rect(0f, 0f, sw, sh);
        }

        /// <summary>Ce magasin est-il en train de servir le joueur ?</summary>
        public bool IsServing(Shop shop)
        {
            return IsOpen && _shop == shop;
        }

        // ------------------------------------------------------------------ ouverture

        public void Open(Shop shop)
        {
            if (shop == null || IsOpen || AnyOpen || GameMenu.IsOpen) return;
            _shop = shop;
            _clerk = shop.Clerk;
            _items = ShopCatalog.Items(shop.Kind, shop.DisplayName);
            _mode = ModeOf(shop.Kind);
            _tab = 0;
            _bought = false;
            _feedback = null;
            _note = null;
            _replyAt = -1f;
            _reply = null;
            _curtain = -1f;
            _rouletteNumber = -1;
            _spinStake = 0;
            _previewKey = -1;
            _seed = Random.Range(0, 10000);

            _player = _input != null ? _input.transform : null;
            _playerCamera = Camera.main;
            _wardrobe = _player != null ? _player.GetComponent<PlayerWardrobe>() : null;

            Open();
        }

        private static Mode ModeOf(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Barbier: return Mode.Barber;
                case ShopKind.Vetements: return Mode.Clothes;
                case ShopKind.Concession: return Mode.Cars;
                case ShopKind.Immobilier: return Mode.Homes;
                case ShopKind.Casino: return Mode.Casino;
                default: return Mode.Items;
            }
        }

        protected override void OnOpened()
        {
            _phase = Phase.Arriving;
            _phaseTime = 0f;

            // Le joueur se tourne vers le comptoir, tête droite (le barbier va le regarder en face).
            PlayerLook look = _player != null ? _player.GetComponent<PlayerLook>() : null;
            if (look != null && _clerk != null)
            {
                Vector3 to = _clerk.transform.position - _player.position;
                to.y = 0f;
                float yaw = to.sqrMagnitude > 0.01f ? Mathf.Atan2(to.x, to.z) * Mathf.Rad2Deg : look.Yaw;
                look.SetLookAngles(yaw, 0f);
            }

            if (_clerk != null)
            {
                _clerk.BeginService(_player);
                bool justSpoke = Time.time - _clerk.LastSpoke < 8f;
                _clerk.Say(justSpoke ? ShopTalk.Prompt(_shop.Kind, _seed) : _clerk.Greeting());
            }

            Frame(0.75f);
        }

        /// <summary>Le cadrage : le vendeur accoudé, ou le joueur (barbier : le visage ; vêtements : en pied).</summary>
        private void Frame(float duration)
        {
            if (_playerCamera == null) return;

            if (_mode == Mode.Barber || _mode == Mode.Clothes)
            {
                Transform head = PlayerHead();
                if (_player == null || head == null) return;
                ShowWholeBody(true);

                Vector3 forward = _player.forward;
                forward.y = 0f;
                forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
                Vector3 right = Vector3.Cross(Vector3.up, forward);

                Vector3 position, look;
                float fov;
                if (_mode == Mode.Barber)
                {
                    // En face, comme dans le miroir : la tête et les épaules, décalées à gauche.
                    position = head.position + forward * 0.78f + right * 0.2f + Vector3.up * 0.03f;
                    look = head.position + right * 0.16f + Vector3.down * 0.02f;
                    fov = 33f;
                }
                else
                {
                    // En pied, un peu de recul.
                    Vector3 feet = _player.position;
                    position = feet + forward * 2.6f + right * 0.75f + Vector3.up * 1.15f;
                    look = feet + right * 0.6f + Vector3.up * 0.92f;
                    fov = 40f;
                }

                position = Clear(head.position, position);
                ShotCamera.Begin(_playerCamera, position, look, null, fov, duration);
                return;
            }

            if (_clerk == null) return;
            Vector3 eye = _playerCamera.transform.position;
            Vector3 clerkHead = _clerk.HeadPosition;
            Vector3 dir = clerkHead - eye;
            dir.y = 0f;
            if (dir.sqrMagnitude < 0.01f) dir = -_clerk.transform.forward;
            float distance = Mathf.Clamp(dir.magnitude * 0.72f, 1.25f, 1.9f);
            dir.Normalize();
            Vector3 side = Vector3.Cross(Vector3.up, dir);

            // Le vendeur à gauche de l'image (le menu occupe la droite).
            Vector3 cam = clerkHead - dir * distance + side * 0.45f + Vector3.down * 0.1f;
            Vector3 target = clerkHead + side * 0.42f + Vector3.down * 0.16f;
            cam = Clear(clerkHead, cam);
            ShotCamera.Begin(_playerCamera, cam, target, _clerk.HeadBone, 38f, duration);
        }

        /// <summary>Rapproche la caméra s'il y a un mur entre le sujet et elle.</summary>
        private Vector3 Clear(Vector3 subject, Vector3 camera)
        {
            Vector3 d = camera - subject;
            float length = d.magnitude;
            if (length < 0.01f) return camera;
            RaycastHit[] hits = Physics.SphereCastAll(subject, 0.12f, d / length, length, ~0, QueryTriggerInteraction.Ignore);
            float nearest = length;
            for (int i = 0; i < hits.Length; i++)
            {
                Transform t = hits[i].collider.transform;
                if (_player != null && t.IsChildOf(_player)) continue;
                if (_clerk != null && t.IsChildOf(_clerk.transform)) continue;
                if (hits[i].distance < nearest) nearest = hits[i].distance;
            }

            return nearest < length ? subject + d / length * Mathf.Max(0.35f, nearest - 0.05f) : camera;
        }

        private Transform PlayerHead()
        {
            if (_player == null) return null;
            BodyRig rig = _player.GetComponentInChildren<BodyRig>(true);
            return rig != null ? rig.Head : null;
        }

        private void ShowWholeBody(bool show)
        {
            if (_wholeBody == show) return;
            _wholeBody = show;
            if (_wardrobe != null) _wardrobe.ShowWholeBody(show);
        }

        // ------------------------------------------------------------------ départ

        protected override bool OnBack()
        {
            if (_phase == Phase.Leaving || _curtain >= 0f) return true;
            Leave();
            return true;
        }

        private void Leave()
        {
            _phase = Phase.Leaving;
            _phaseTime = 0f;
            _replyAt = -1f;
            _reply = null;

            // On repose ce qu'on essayait et qu'on n'a pas pris.
            if (_wardrobe != null && _progress != null) _wardrobe.Preview(_progress.CurrentOutfit);

            Say(ShopTalk.Player, ShopTalk.PlayerBye(_bought, _seed++));
            float hour = WorldClock.Instance != null ? WorldClock.Instance.Hour : 12f;
            if (_clerk != null) _clerk.Say(ShopTalk.Goodbye(_shop.Kind, hour, _bought, _seed++));

            ShotCamera.End(0.6f, Finish);
        }

        private void Finish()
        {
            ShowWholeBody(false);
            if (_clerk != null) _clerk.EndService();
            Close();
        }

        protected override void OnClosed()
        {
            if (ShotCamera.Active) ShotCamera.Cancel();
            ShowWholeBody(false);
            if (_clerk != null) _clerk.EndService();
            if (_wardrobe != null && _progress != null) _wardrobe.Preview(_progress.CurrentOutfit);
        }

        // ------------------------------------------------------------------ chaque image

        protected override void Update()
        {
            base.Update();
            if (!IsOpen) return;

            float dt = Time.unscaledDeltaTime;
            _phaseTime += dt;
            if (_phase == Phase.Arriving && _phaseTime > 0.55f) _phase = Phase.Browsing;

            if (_replyAt > 0f && Time.unscaledTime >= _replyAt)
            {
                _replyAt = -1f;
                Action reply = _reply;
                _reply = null;
                if (reply != null) reply();
                _browseAt = Time.unscaledTime + 0.5f;
            }

            if (_phase == Phase.Talking && _replyAt < 0f && _curtain < 0f && Time.unscaledTime >= _browseAt) _phase = Phase.Browsing;

            if (_curtain >= 0f)
            {
                float before = _curtain;
                _curtain += dt;
                if (before < 0.45f && _curtain >= 0.45f && _curtainAction != null)
                {
                    Action action = _curtainAction;
                    _curtainAction = null;
                    action();
                }

                if (_curtain >= 1.3f) _curtain = -1f;
            }

            // Le magasin ferme pendant qu'on est au comptoir : on finit, et on s'en va.
            if (_shop != null && !_shop.IsOpen && _phase == Phase.Browsing)
            {
                if (_clerk != null) _clerk.Say(ShopTalk.Closing(_shop.Kind, _seed++));
                Leave();
            }

            if (_mode == Mode.Casino) UpdateCasino();
        }

        private void Say(string speaker, string text)
        {
            if (_subtitles != null && !string.IsNullOrEmpty(text)) _subtitles.Play(DialogueLine.Say(speaker, text));
        }

        private void Feedback(string text, bool bad)
        {
            _feedback = text;
            _feedbackBad = bad;
            _feedbackUntil = Time.unscaledTime + 3.5f;
            if (bad) PlayDeny();
        }

        /// <summary>
        /// Une commande : le joueur parle, puis, un instant après, le comptoir répond, encaisse,
        /// et l'effet s'applique.
        /// </summary>
        private void Exchange(string order, Action reply)
        {
            _phase = Phase.Talking;
            Say(ShopTalk.Player, order);
            _reply = reply;
            _replyAt = Time.unscaledTime + Mathf.Clamp(0.55f + order.Length * 0.028f, 0.9f, 2.2f);
        }

        private bool Ready
        {
            get { return _phase == Phase.Browsing && _curtain < 0f; }
        }

        protected override void OnHorizontal(int direction)
        {
            int tabs = _mode == Mode.Barber ? BarberTabs.Length : _mode == Mode.Clothes ? ClothesTabs.Length : 0;
            if (tabs == 0 || !Ready) return;
            _tab = (_tab + direction + tabs) % tabs;
            _previewKey = -1;
            ResetFocus();
            PlayTick();
        }

        // ------------------------------------------------------------------ dessin

        protected override void DrawContent(Rect area, float u)
        {
            if (_shop == null || _progress == null) return;

            // Le rideau du barbier (le temps de la coupe).
            if (_curtain >= 0f)
            {
                float a = _curtain < 0.45f ? _curtain / 0.45f : _curtain < 0.85f ? 1f : 1f - (_curtain - 0.85f) / 0.45f;
                GuiKit.Fill(new Rect(0f, 0f, area.width, area.height), new Color(0f, 0f, 0f, Mathf.Clamp01(a)));
                if (a > 0.6f)
                {
                    UiTheme.Label(new Rect(0f, area.height * 0.5f - 20f * u, area.width, 40f * u), "Coupe en cours…",
                        UiTheme.Text(18f, GuiKit.Weight.Medium, TextAnchor.MiddleCenter), new Color(1f, 1f, 1f, 0.6f));
                }

                return;
            }

            float slide = UiTheme.EaseOut(Mathf.Clamp01(OpenTime / 0.45f));
            if (_phase == Phase.Leaving) slide = 1f - UiTheme.EaseOut(Mathf.Clamp01(_phaseTime / 0.3f));
            float width = Mathf.Min(470f * u, area.width * 0.4f);
            float x = area.width - width - 36f * u + (1f - slide) * (width + 60f * u);
            float top = 60f * u;
            float maxHeight = area.height - top - 240f * u;

            float saved = GuiKit.Alpha;
            GuiKit.Alpha *= slide;

            float height = Mathf.Min(maxHeight, PanelHeight(u));
            Rect panel = new Rect(x, top, width, height);
            UiTheme.DrawPanel(panel, 10f * u);
            DrawPanel(panel, u);

            GuiKit.Alpha = saved;
        }

        private int RowCount()
        {
            switch (_mode)
            {
                case Mode.Barber:
                    return _tab == 0 ? HairCatalog.Cuts.Length : _tab == 1 ? HairCatalog.Beards.Length : _tab == 2 ? HairCatalog.Colors.Length : _items.Length;
                case Mode.Clothes:
                    return PlayerWardrobe.Family(_tab).Length;
                case Mode.Cars:
                    return (_vehicles != null ? _vehicles.Entries.Count : 0) + _items.Length;
                case Mode.Homes:
                    return (_computer != null && _computer.Listings != null ? _computer.Listings.Length : 0) + _items.Length;
                case Mode.Casino:
                    return 6;
                default:
                    return _items.Length + (_shop.Kind == ShopKind.SalleDeBoxe ? 1 : 0);
            }
        }

        private float PanelHeight(float u)
        {
            float tabs = _mode == Mode.Barber || _mode == Mode.Clothes ? 54f : 0f;
            float casino = _mode == Mode.Casino ? 94f : 0f;
            return (112f + tabs + casino + Mathf.Min(RowCount(), 9) * 58f + 120f + 60f) * u;
        }

        private void DrawPanel(Rect panel, float u)
        {
            float pad = 22f * u;
            float x0 = panel.x + pad;
            float w = panel.width - pad * 2f;
            float y = panel.y + 18f * u;

            // --- en-tête : ce que c'est, le nom, les horaires
            UiTheme.Label(new Rect(x0, y, w, 16f * u), ShopCatalog.Describe(_shop.Kind).ToUpperInvariant(),
                UiTheme.Text(12f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft), UiTheme.Accent);
            UiTheme.Label(new Rect(x0, y + 16f * u, w, 38f * u), _shop.DisplayName, UiTheme.Title(27f), UiTheme.Ink);

            bool open;
            string hours = ShopDirectory.Instance != null ? ShopDirectory.Instance.HoursNote(_shop, out open) : null;
            if (!string.IsNullOrEmpty(hours))
            {
                UiTheme.Label(new Rect(x0, y + 56f * u, w, 20f * u), hours + "   ·   " + ShopHours.Range(_shop.Hours),
                    UiTheme.Light(13f), UiTheme.InkDim);
            }

            y += 92f * u;
            GuiKit.Fill(new Rect(x0, y, w, 1f), UiTheme.Line);
            y += 10f * u;

            // --- onglets
            if (_mode == Mode.Barber || _mode == Mode.Clothes)
            {
                string[] tabs = _mode == Mode.Barber ? BarberTabs : ClothesTabs;
                y = Tabs(tabs, x0, y, w, u) + 10f * u;
            }

            if (_mode == Mode.Casino) y = Reels(x0, y, w, u) + 10f * u;

            // --- la liste
            float rowH = 58f * u;
            int count = RowCount();
            int visible = Mathf.Max(1, Mathf.Min(count, Mathf.FloorToInt((panel.yMax - y - 178f * u) / rowH)));
            int focus = Mathf.Clamp(FocusIndex, 0, Mathf.Max(0, count - 1));
            int first = Mathf.Clamp(focus - visible / 2, 0, Mathf.Max(0, count - visible));

            string detail = null;
            for (int i = 0; i < count; i++)
            {
                bool shown = i >= first && i < first + visible;
                Rect row = shown ? new Rect(x0 - 8f * u, y + (i - first) * rowH, w + 16f * u, rowH - 4f * u) : new Rect(-10000f, -10000f, 1f, 1f);
                string d = Row(i, row, shown, u);
                if (i == focus) detail = d;
            }

            GUIStyle arrow = UiTheme.Text(10f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            if (first > 0) UiTheme.Label(new Rect(x0, y - 12f * u, w, 12f * u), "▲", arrow, UiTheme.InkFaint);
            y += visible * rowH;
            if (first + visible < count) UiTheme.Label(new Rect(x0, y - 4f * u, w, 12f * u), "▼", arrow, UiTheme.InkFaint);

            // --- le détail de ce qu'on regarde, et ce qui vient de se passer
            y += 8f * u;
            GuiKit.Fill(new Rect(x0, y, w, 1f), UiTheme.Line);
            y += 10f * u;
            Rect info = new Rect(x0, y, w, 88f * u);
            string text = !string.IsNullOrEmpty(_note) ? "« " + _note + " »" : detail ?? string.Empty;
            UiTheme.Label(new Rect(info.x, info.y, info.width, info.height - 30f * u), text,
                GuiKit.Text(UiTheme.Size(14f), GuiKit.Weight.Regular, TextAnchor.UpperLeft, true), UiTheme.InkDim);

            if (!string.IsNullOrEmpty(_feedback) && Time.unscaledTime < _feedbackUntil)
            {
                Rect chip = new Rect(x0, info.yMax - 26f * u, w, 26f * u);
                Color tone = _feedbackBad ? UiTheme.Bad : UiTheme.Good;
                GuiKit.Rounded(chip, new Color(tone.r, tone.g, tone.b, 0.14f), 6f * u);
                UiTheme.Label(new Rect(chip.x + 10f * u, chip.y, chip.width - 20f * u, chip.height), _feedback,
                    UiTheme.Text(13.5f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft), tone);
            }

            // --- le pied : l'argent, partir
            float footY = panel.yMax - 54f * u;
            GuiKit.Fill(new Rect(x0, footY, w, 1f), UiTheme.Line);
            UiTheme.Label(new Rect(x0, footY + 9f * u, 160f * u, 16f * u), "TON ARGENT", UiTheme.Text(11f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft), UiTheme.InkFaint);
            UiTheme.Label(new Rect(x0, footY + 24f * u, 200f * u, 24f * u), _progress.Money.ToString("N0") + " €",
                UiTheme.Text(18f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft), UiTheme.Good);

            if (_phase == Phase.Talking)
            {
                float dots = Mathf.Repeat(Time.unscaledTime * 2.5f, 3f);
                string talk = dots < 1f ? "·" : dots < 2f ? "· ·" : "· · ·";
                UiTheme.Label(new Rect(x0 + 150f * u, footY + 10f * u, w - 290f * u, 34f * u), talk,
                    UiTheme.Text(18f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter), UiTheme.InkFaint);
            }

            Rect leave = new Rect(panel.xMax - pad - 128f * u, footY + 11f * u, 128f * u, 32f * u);
            bool hover = leave.Contains(Event.current.mousePosition);
            GuiKit.Rounded(leave, hover ? UiTheme.Hover : UiTheme.Raised, 6f * u);
            UiTheme.Label(leave, "Partir  ·  Échap", UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleCenter), UiTheme.Ink);
            if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover && Ready)
            {
                Event.current.Use();
                Leave();
            }
        }

        private float Tabs(string[] tabs, float x0, float y, float w, float u)
        {
            GUIStyle style = UiTheme.Text(13f, GuiKit.Weight.Medium, TextAnchor.MiddleCenter);
            float h = 30f * u;
            float gap = 6f * u;
            float cw = (w - gap * (tabs.Length - 1)) / tabs.Length;
            for (int i = 0; i < tabs.Length; i++)
            {
                Rect chip = new Rect(x0 + i * (cw + gap), y, cw, h);
                bool selected = _tab == i;
                bool hover = chip.Contains(Event.current.mousePosition);
                GuiKit.Rounded(chip, selected ? UiTheme.Ink : hover ? UiTheme.Hover : UiTheme.Raised, h * 0.5f);
                UiTheme.Label(chip, tabs[i], style, selected ? new Color(0.06f, 0.07f, 0.09f) : UiTheme.Ink);
                if (Event.current.type == EventType.MouseDown && Event.current.button == 0 && hover && Ready && !selected)
                {
                    Event.current.Use();
                    _tab = i;
                    _previewKey = -1;
                    ResetFocus();
                    PlayTick();
                }
            }

            UiTheme.Label(new Rect(x0, y + h + 3f * u, w, 14f * u), "◄ ►  changer d'onglet",
                UiTheme.Text(10.5f, GuiKit.Weight.Regular, TextAnchor.MiddleRight), UiTheme.InkFaint);
            return y + h + 16f * u;
        }

        /// <summary>
        /// Une ligne de la liste : la dessine (si elle est visible), réagit au clic ou à Entrée,
        /// et renvoie son texte de détail.
        /// </summary>
        private string Row(int i, Rect row, bool shown, float u)
        {
            switch (_mode)
            {
                case Mode.Barber: return BarberRow(i, row, shown, u);
                case Mode.Clothes: return ClothesRow(i, row, shown, u);
                case Mode.Cars:
                {
                    int cars = _vehicles != null ? _vehicles.Entries.Count : 0;
                    return i < cars ? CarRow(i, row, shown, u) : ItemRow(_items[i - cars], row, shown, u);
                }
                case Mode.Homes:
                {
                    int listings = _computer != null && _computer.Listings != null ? _computer.Listings.Length : 0;
                    return i < listings ? HomeRow(i, row, shown, u) : ItemRow(_items[i - listings], row, shown, u);
                }
                case Mode.Casino: return CasinoRow(i, row, shown, u);
                default:
                    if (_shop.Kind == ShopKind.SalleDeBoxe)
                    {
                        if (i == 0) return CoachingRow(row, shown, u);
                        return ItemRow(_items[i - 1], row, shown, u);
                    }

                    return ItemRow(_items[i], row, shown, u);
            }
        }

        /// <summary>Le dessin commun d'une ligne : nom, sous-titre, prix (et une étiquette d'état).</summary>
        private bool DrawRow(Rect row, bool shown, string name, string sub, string price, Color priceColor, string tag, bool enabled,
            float u, Color? swatch = null)
        {
            bool focused, hover;
            bool activated = Item(row, enabled, out focused, out hover) && Ready;
            if (!shown || Event.current.type != EventType.Repaint) return activated;

            if (focused)
            {
                GuiKit.Rounded(row, new Color(1f, 1f, 1f, enabled ? 0.11f : 0.06f), 7f * u);
                GuiKit.Rounded(new Rect(row.x, row.y + 10f * u, 3f * u, row.height - 20f * u), enabled ? UiTheme.Accent : UiTheme.InkFaint, 1.5f * u);
            }
            else if (hover)
            {
                GuiKit.Rounded(row, UiTheme.Hover, 7f * u);
            }

            float tx = row.x + 16f * u;
            if (swatch.HasValue)
            {
                Rect s = new Rect(tx, row.center.y - 11f * u, 22f * u, 22f * u);
                GuiKit.Rounded(s, swatch.Value, 11f * u);
                GuiKit.RoundedOutline(s, new Color(1f, 1f, 1f, 0.35f), 11f * u, 1f);
                tx += 32f * u;
            }

            GUIStyle priceStyle = UiTheme.Text(15f, GuiKit.Weight.Bold, TextAnchor.MiddleRight);
            float pw = string.IsNullOrEmpty(price) ? 0f : priceStyle.CalcSize(new GUIContent(price)).x + 8f * u;
            float tw = row.xMax - 14f * u - pw - tx;
            Color ink = enabled ? UiTheme.Ink : UiTheme.InkDim;

            UiTheme.Label(new Rect(tx, row.y + 7f * u, tw, 22f * u), name, UiTheme.Text(15.5f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft), ink);
            string second = string.IsNullOrEmpty(tag) ? sub : tag + (string.IsNullOrEmpty(sub) ? "" : "  ·  " + sub);
            if (!string.IsNullOrEmpty(second))
            {
                UiTheme.Label(new Rect(tx, row.y + 29f * u, tw, 18f * u), second, UiTheme.Text(12.5f, GuiKit.Weight.Regular, TextAnchor.MiddleLeft),
                    string.IsNullOrEmpty(tag) ? UiTheme.InkFaint : UiTheme.Accent);
            }

            if (!string.IsNullOrEmpty(price))
            {
                UiTheme.Label(new Rect(row.xMax - 14f * u - pw, row.y, pw, row.height), price, priceStyle, enabled ? priceColor : UiTheme.InkFaint);
            }

            return activated;
        }

        private static string Money(int price)
        {
            return price <= 0 ? "Gratuit" : price.ToString("N0") + " €";
        }

        private bool CanPay(int price)
        {
            if (price <= 0 || _progress.Money >= price) return true;
            if (_clerk != null) _clerk.Say(ShopTalk.Broke(_shop.Kind, price - _progress.Money, _seed++));
            Feedback("Il te manque " + (price - _progress.Money).ToString("N0") + " €.", true);
            return false;
        }

        // ------------------------------------------------------------------ articles

        private string ItemRow(ShopItem item, Rect row, bool shown, float u)
        {
            string key = item.Key(_shop.Kind);
            bool doneToday = item.daily && _progress.HasFlag(key + ":j" + _progress.Day);
            bool owned = item.once && _progress.IsUnlocked(key);
            bool affordable = item.price <= 0 || _progress.Money >= item.price;

            string price = item.effect == ShopEffect.Sell ? "+" + Mathf.RoundToInt(item.amount) + " €" : Money(item.price);
            Color priceColor = item.effect == ShopEffect.Sell ? UiTheme.Good : UiTheme.Ink;
            string tag = owned ? "Déjà fait" : doneToday ? "Revenir demain" : !affordable ? "Trop cher" : null;
            bool enabled = !owned && !doneToday;

            if (DrawRow(row, shown, item.name, Effect(item), price, priceColor, tag, enabled, u)) Buy(item);
            return item.detail;
        }

        /// <summary>L'effet, en très court (« +25 PV », « +12 % vitesse · 4 min »).</summary>
        private static string Effect(ShopItem item)
        {
            switch (item.effect)
            {
                case ShopEffect.Heal: return "+" + Mathf.RoundToInt(item.amount) + " PV" + (item.food > 0f ? " · rassasie" : "");
                case ShopEffect.HealFull: return "Vie et endurance au max" + (item.food > 0f ? " · rassasie" : "");
                case ShopEffect.Buff:
                    return "+" + Mathf.RoundToInt(item.amount) + (item.stat == Combat.StatType.Defense ? "" : " %") + " " + StatName(item.stat) +
                           " · " + Mathf.RoundToInt(item.minutes) + " min";
                case ShopEffect.Reputation: return item.amount > 0f ? "+" + Mathf.RoundToInt(item.amount) + " réputation" : "Pour le plaisir";
                case ShopEffect.Injury: return item.amount <= 0f ? "Soigne toutes les blessures" : "Soigne une blessure";
                case ShopEffect.Experience: return "+" + Mathf.RoundToInt(item.amount) + " XP";
                case ShopEffect.Parcel: return "Une fois par jour";
                case ShopEffect.Rumor: return "Un tuyau";
                case ShopEffect.Sell: return "Vendre";
                case ShopEffect.Groceries: return "+" + Mathf.RoundToInt(item.amount) + " repas au frigo";
                case ShopEffect.Medkit: return "Trousse pour la maison";
                case ShopEffect.Lockpicks: return "+" + Mathf.RoundToInt(item.amount) + " crochets";
                case ShopEffect.ClearRecord: return "Casier judiciaire";
                default: return null;
            }
        }

        private static string StatName(Combat.StatType stat)
        {
            switch (stat)
            {
                case Combat.StatType.AttackSpeed: return "vitesse";
                case Combat.StatType.Strength: return "puissance";
                case Combat.StatType.MaxStamina: return "endurance";
                case Combat.StatType.Defense: return "défense";
                case Combat.StatType.KnockdownPower: return "puissance de chute";
                default: return stat.ToString().ToLowerInvariant();
            }
        }

        private void Buy(ShopItem item)
        {
            int price = item.price;
            if (!CanPay(price)) return;

            int seed = _seed++;
            Exchange(ShopTalk.Order(item, seed), () =>
            {
                if (price > 0 && !_progress.Spend(price, _shop.DisplayName + " — " + item.name))
                {
                    if (_clerk != null) _clerk.Say(ShopTalk.Broke(_shop.Kind, price - _progress.Money, seed));
                    return;
                }

                string result;
                if (!Apply(item, out result))
                {
                    if (price > 0) _progress.AddMoney(price, "Remboursement " + _shop.DisplayName);
                    Feedback(result, true);
                    if (_clerk != null) _clerk.Say(result);
                    return;
                }

                if (_clerk != null) _clerk.Say(ShopTalk.Reply(item, price, seed));
                if (item.effect == ShopEffect.Rumor && _clerk != null && !string.IsNullOrEmpty(_note)) _clerk.Say(_note);

                if (item.food > 0f && item.effect != ShopEffect.Groceries) _progress.Eat(item.food);
                string key = item.Key(_shop.Kind);
                if (item.daily) _progress.SetFlag(key + ":j" + _progress.Day);
                if (item.once) _progress.Unlock(key);
                if (price > 0 || item.effect == ShopEffect.Sell) PlayCash();
                _bought = true;
                Feedback(result, false);
            });
        }

        /// <summary>L'effet d'un article. Faux = rien n'a été fait (remboursé) ; <paramref name="result"/> le dit.</summary>
        private bool Apply(ShopItem item, out string result)
        {
            PlayerBuffs buffs = PlayerBuffs.Instance;
            switch (item.effect)
            {
                case ShopEffect.Heal:
                    if (buffs != null) buffs.Heal(item.amount);
                    result = item.name + " : +" + Mathf.RoundToInt(item.amount) + " PV.";
                    return true;

                case ShopEffect.HealFull:
                    if (buffs != null) buffs.HealFull();
                    result = "Vie et endurance au maximum.";
                    return true;

                case ShopEffect.Buff:
                    if (buffs != null) buffs.Add(item.name, item.stat, item.amount, item.minutes);
                    result = item.name + " : effet pendant " + Mathf.RoundToInt(item.minutes) + " min.";
                    return true;

                case ShopEffect.Reputation:
                    if (item.amount > 0f) _progress.ChangeReputation(Mathf.RoundToInt(item.amount), item.name);
                    result = item.amount > 0f ? "+" + Mathf.RoundToInt(item.amount) + " réputation." : "C'était sympa.";
                    return true;

                case ShopEffect.Injury:
                    if (!_progress.HealInjuries(item.amount <= 0f))
                    {
                        result = "Vous n'avez aucune blessure à soigner.";
                        return false;
                    }

                    if (buffs != null) buffs.HealFull();
                    result = item.amount <= 0f ? "Toutes tes blessures sont soignées." : "Une blessure en moins.";
                    return true;

                case ShopEffect.Experience:
                    _progress.AddExperience(Mathf.RoundToInt(item.amount));
                    result = "+" + Mathf.RoundToInt(item.amount) + " XP.";
                    return true;

                case ShopEffect.Parcel:
                {
                    int gift = Mathf.RoundToInt(Random.Range(item.amount * 0.3f, item.amount));
                    _progress.AddMoney(gift, "Colis — La Poste");
                    PlayCash();
                    result = "Une enveloppe : " + gift + " € et un mot d'un fan.";
                    return true;
                }

                case ShopEffect.Sell:
                    _progress.AddMoney(Mathf.RoundToInt(item.amount), _shop.DisplayName + " — " + item.name);
                    result = "Vendu : +" + Mathf.RoundToInt(item.amount) + " €.";
                    return true;

                case ShopEffect.Groceries:
                    _progress.AddMeals(Mathf.RoundToInt(item.amount));
                    result = "+" + Mathf.RoundToInt(item.amount) + " repas dans le frigo (" + _progress.Meals + " en tout).";
                    return true;

                case ShopEffect.ClearRecord:
                {
                    int weight = _progress.RecordWeight;
                    if (weight <= 0)
                    {
                        result = "Votre casier est vierge. Revenez quand vous aurez fait quelque chose.";
                        return false;
                    }

                    int fee = 300 + 120 * weight;
                    if (!_progress.Spend(fee, "Honoraires — Maître Lenoir"))
                    {
                        result = "Mes honoraires : " + fee + " €. Vous ne les avez pas.";
                        return false;
                    }

                    _progress.ClearRecord();
                    result = "Casier nettoyé (" + fee + " €). « Vous n'avez jamais existé. »";
                    return true;
                }

                case ShopEffect.Lockpicks:
                    _progress.AddLockpicks(Mathf.RoundToInt(item.amount));
                    result = "+" + Mathf.RoundToInt(item.amount) + " crochets (" + _progress.Lockpicks + ").";
                    return true;

                case ShopEffect.Medkit:
                    _progress.AddMedkit();
                    result = "Trousse rangée pour la maison (" + _progress.Medkits + ").";
                    return true;

                case ShopEffect.Rumor:
                    _note = ShopCatalog.Rumor(Random.Range(0, 1000));
                    result = "Bon à savoir.";
                    return true;
            }

            result = "Rien ne se passe.";
            return false;
        }

        // ------------------------------------------------------------------ barbier

        private string BarberRow(int i, Rect row, bool shown, float u)
        {
            if (_tab == 3) return i < _items.Length ? ItemRow(_items[i], row, shown, u) : null;

            PlayerProgress.Outfit worn = _progress.CurrentOutfit;
            string name, detail;
            int price;
            bool current;
            Color? swatch = null;
            if (_tab == 0)
            {
                HairCatalog.Entry e = HairCatalog.Cuts[i];
                name = e.Name;
                detail = e.Detail;
                price = e.Price;
                current = worn.hair == i;
            }
            else if (_tab == 1)
            {
                HairCatalog.Entry e = HairCatalog.Beards[i];
                name = e.Name;
                detail = e.Detail;
                price = e.Price;
                current = worn.beard == i;
            }
            else
            {
                HairCatalog.Tint t = HairCatalog.Colors[i];
                name = t.Name;
                detail = "Teinture « " + t.Name.ToLowerInvariant() + " », pour les cheveux et la barbe.";
                price = Mathf.Max(15, t.Price);
                current = worn.hairColor == i;
                swatch = t.Color;
            }

            // L'essai en direct : ce qu'on survole se voit tout de suite.
            if (FocusIndex == i && Event.current.type == EventType.Repaint) PreviewHair(_tab, i);

            bool affordable = _progress.Money >= price;
            if (DrawRow(row, shown, name, null, current ? null : Money(price), UiTheme.Ink, current ? "Actuel" : !affordable ? "Trop cher" : null,
                    !current, u, swatch))
            {
                BuyHair(_tab, i, name, price);
            }

            return detail;
        }

        private void PreviewHair(int tab, int index)
        {
            int key = tab * 100 + index;
            if (key == _previewKey || _wardrobe == null) return;
            _previewKey = key;
            _wardrobe.Preview(WithHair(_progress.CurrentOutfit.Copy(), tab, index));
        }

        private static PlayerProgress.Outfit WithHair(PlayerProgress.Outfit o, int tab, int index)
        {
            if (tab == 0) o.hair = index;
            else if (tab == 1) o.beard = index;
            else o.hairColor = index;
            return o;
        }

        private void BuyHair(int tab, int index, string name, int price)
        {
            if (!CanPay(price)) return;

            string ask = tab == 0 ? ShopTalk.AskCut((HairCut)index, _seed++)
                : tab == 1 ? (index == 0 ? "Rase-moi la barbe, de près." : "Taille-moi " + Article(name) + ".")
                : "Fais-moi une couleur : " + name.ToLowerInvariant() + ".";
            int seed = _seed++;

            Exchange(ask, () =>
            {
                if (!_progress.Spend(price, _shop.DisplayName + " — " + name))
                {
                    if (_clerk != null) _clerk.Say(ShopTalk.Broke(_shop.Kind, price - _progress.Money, seed));
                    return;
                }

                if (_clerk != null) _clerk.Say("Ça marche. " + ShopTalk.Price(price, seed));
                PlayCash();

                // Le temps de la coupe : un fondu au noir, et la nouvelle tête.
                _curtain = 0f;
                _curtainAction = () =>
                {
                    PlayerProgress.Outfit o = WithHair(_progress.CurrentOutfit.Copy(), tab, index);
                    _progress.Wear(o);
                    if (_wardrobe != null) _wardrobe.Preview(o);
                    _previewKey = -1;

                    string flag = "boutique:Barbier:soin:j" + _progress.Day;
                    if (!_progress.HasFlag(flag))
                    {
                        _progress.SetFlag(flag);
                        _progress.ChangeReputation(2, "Chez le barbier");
                        Feedback(name + " · +2 réputation (propre sur toi).", false);
                    }
                    else
                    {
                        Feedback(name + ".", false);
                    }

                    if (_clerk != null) _clerk.Say(ShopTalk.BarberDone(seed));
                    _bought = true;
                };
            });
        }

        private static string Article(string name)
        {
            string lower = name.ToLowerInvariant();
            if (lower.StartsWith("barbe")) return "une " + lower;
            if (lower.StartsWith("moustache")) return "la moustache";
            return "un " + lower;
        }

        // ------------------------------------------------------------------ vêtements

        private string ClothesRow(int i, Rect row, bool shown, float u)
        {
            int family = _tab;
            PlayerWardrobe.Item item = PlayerWardrobe.Family(family)[i];
            PlayerProgress.Outfit worn = _progress.CurrentOutfit;
            int[] current = { worn.top, worn.shirt, worn.pants, worn.shoes };
            string key = PlayerWardrobe.Key(family, i);
            bool owned = _progress.IsUnlocked(key) || item.Price <= 0;
            bool levelOk = _progress.Level >= item.Level;
            bool wearing = current[family] == i;

            if (FocusIndex == i && Event.current.type == EventType.Repaint) PreviewClothes(family, i);

            string tag = wearing ? "Porté" : owned ? "À toi" : !levelOk ? "Niveau " + item.Level + " requis" : null;
            string price = owned ? null : Money(item.Price);
            Color? swatch = family > 0 ? item.Color : (Color?)null;
            bool enabled = !wearing && (owned || levelOk);

            if (DrawRow(row, shown, item.Name, PlayerWardrobe.FamilyName(family).ToLowerInvariant(), price, UiTheme.Ink, tag, enabled, u, swatch))
            {
                BuyClothes(family, i, item, owned);
            }

            return owned ? "Dans ton armoire. Entrée : le porter." : "Entrée : l'acheter et le porter. Niveau " + item.Level + " minimum.";
        }

        private void PreviewClothes(int family, int index)
        {
            int key = family * 100 + index;
            if (key == _previewKey || _wardrobe == null) return;
            _previewKey = key;
            _wardrobe.Preview(With(_progress.CurrentOutfit.Copy(), family, index));
        }

        private static PlayerProgress.Outfit With(PlayerProgress.Outfit o, int family, int index)
        {
            switch (family)
            {
                case 0: o.top = index; break;
                case 1: o.shirt = index; break;
                case 2: o.pants = index; break;
                default: o.shoes = index; break;
            }

            return o;
        }

        private void BuyClothes(int family, int index, PlayerWardrobe.Item item, bool owned)
        {
            if (owned)
            {
                _progress.Wear(With(_progress.CurrentOutfit.Copy(), family, index));
                Feedback(item.Name + " : porté.", false);
                return;
            }

            if (!CanPay(item.Price)) return;

            int seed = _seed++;
            string[] asks = { "Je prends ça.", "Celui-là, je le prends.", "Parfait, je le garde sur moi." };
            Exchange(asks[seed % asks.Length], () =>
            {
                if (!_progress.Spend(item.Price, _shop.DisplayName + " — " + item.Name))
                {
                    if (_clerk != null) _clerk.Say(ShopTalk.Broke(_shop.Kind, item.Price - _progress.Money, seed));
                    return;
                }

                _progress.Unlock(PlayerWardrobe.Key(family, index));
                _progress.Wear(With(_progress.CurrentOutfit.Copy(), family, index));
                PlayCash();
                string[] replies = { "Excellent choix.", "Il vous va très bien.", "Ça vous change !" };
                if (_clerk != null) _clerk.Say(replies[seed % replies.Length] + " " + ShopTalk.Price(item.Price, seed));
                Feedback(item.Name + " acheté et porté.", false);
                _bought = true;
            });
        }

        // ------------------------------------------------------------------ concession, immobilier, boxe

        private string CarRow(int i, Rect row, bool shown, float u)
        {
            VehicleCatalog.Entry e = _vehicles.Entries[i];
            bool current = e.key == _vehicles.PersonalKey;
            if (DrawRow(row, shown, e.label, current ? null : "Livrée devant chez toi", current ? null : Money(e.price), UiTheme.Ink,
                    current ? "C'est la tienne" : _progress.Money < e.price ? "Trop cher" : null, !current, u))
            {
                if (CanPay(e.price))
                {
                    int seed = _seed++;
                    Exchange("Je prends la " + e.label.ToLowerInvariant() + ".", () =>
                    {
                        if (!_vehicles.Buy(e.key))
                        {
                            if (_clerk != null) _clerk.Say(ShopTalk.Broke(_shop.Kind, e.price - _progress.Money, seed));
                            return;
                        }

                        PlayCash();
                        if (_clerk != null) _clerk.Say("Félicitations ! Elle t'attend devant chez toi. " + ShopTalk.Price(e.price, seed));
                        Feedback(e.label + " achetée.", false);
                        _bought = true;
                    });
                }
            }

            return current ? "Ta voiture actuelle." : "Remplace ta voiture actuelle. Livrée devant chez toi.";
        }

        private string HomeRow(int i, Rect row, bool shown, float u)
        {
            ComputerScreen.Property p = _computer.Listings[i];
            bool owned = _progress.Owns(p.name);
            bool here = _progress.Home == p.name;
            bool available = _homes == null || _homes.Has(p.name);
            string tag = here ? "Chez toi" : owned ? "À toi : s'y installer" : !available ? "Pas encore sur le marché" : null;

            if (DrawRow(row, shown, p.name, null, owned ? null : Money(p.price), UiTheme.Ink, tag, available && !here, u))
            {
                if (owned || CanPay(p.price))
                {
                    Exchange(owned ? "Je m'installe à " + p.name + "." : "Je prends " + p.name + ".", () =>
                    {
                        if (!owned)
                        {
                            if (!_progress.Spend(p.price, "Hyland Immobilier — " + p.name)) return;
                            _progress.Acquire(p.name);
                            PlayCash();
                        }

                        _progress.MoveTo(p.name);
                        if (_homes != null) _homes.Apply();
                        if (_clerk != null) _clerk.Say(owned ? "Les clés sont à vous." : "Signez ici… et ici. Félicitations, c'est chez vous.");
                        Feedback("Tu habites maintenant : " + p.name + ".", false);
                        _bought = true;
                    });
                }
            }

            return available ? p.pitch : "Pas encore sur le marché.";
        }

        private string CoachingRow(Rect row, bool shown, float u)
        {
            int price = _progress.TrainingPrice;
            if (DrawRow(row, shown, "Semaine de coaching", "+1 point d'entraînement", Money(price), UiTheme.Ink,
                    _progress.Money < price ? "Trop cher" : null, true, u) && CanPay(price))
            {
                int seed = _seed++;
                Exchange("Je veux une semaine de coaching.", () =>
                {
                    if (!_progress.BuyTraining()) return;
                    PlayCash();
                    if (_clerk != null) _clerk.Say("On commence demain, six heures. Sois à l'heure. " + ShopTalk.Price(price, seed));
                    Feedback("+1 point d'entraînement (à dépenser à l'armoire).", false);
                    _bought = true;
                });
            }

            return "Un point d'entraînement, à dépenser à l'armoire de ta planque. Le prix monte à chaque fois.";
        }

        // ------------------------------------------------------------------ casino

        private float Reels(float x0, float y, float w, float u)
        {
            Rect frame = new Rect(x0, y, w, 80f * u);
            GuiKit.Rounded(frame, new Color(0.18f, 0.03f, 0.07f, 0.9f), 8f * u);
            float cell = (w - 40f * u) / 3f;
            for (int i = 0; i < 3; i++)
            {
                Rect r = new Rect(x0 + 10f * u + i * (cell + 10f * u), y + 10f * u, cell, 60f * u);
                GuiKit.Rounded(r, new Color(1f, 1f, 1f, 0.94f), 6f * u);
                UiTheme.Label(r, Symbols[_reels[i]], UiTheme.Text(22f, GuiKit.Weight.Black, TextAnchor.MiddleCenter), new Color(0.7f, 0.05f, 0.1f));
            }

            return y + 84f * u;
        }

        private string CasinoRow(int i, Rect row, bool shown, float u)
        {
            bool spinning = Time.unscaledTime < _spinUntil || _spinStake > 0;
            if (i < 3)
            {
                int[] stakes = { 10, 50, 100 };
                int stake = stakes[i];
                if (DrawRow(row, shown, "Machine à sous", "Trois identiques : x8 · trois 7 : x25", Money(stake), UiTheme.Ink,
                        _progress.Money < stake ? "Trop cher" : null, !spinning, u))
                {
                    Spin(stake);
                }

                return "Une partie à " + stake + " €. Deux symboles identiques côte à côte : x1,5.";
            }

            string[] bets = { "Roulette : rouge", "Roulette : noir", "Roulette : le 7" };
            string[] subs = { "Gain x2", "Gain x2", "Gain x36" };
            int[] prices = { 25, 25, 10 };
            int bet = i - 3;
            if (DrawRow(row, shown, bets[bet], subs[bet], Money(prices[bet]), UiTheme.Ink, _progress.Money < prices[bet] ? "Trop cher" : null, !spinning, u))
            {
                Roulette(bet, prices[bet]);
            }

            return _rouletteNumber >= 0
                ? "Dernier tirage : " + _rouletteNumber + (_rouletteNumber == 0 ? " (vert)" : IsRed(_rouletteNumber) ? " (rouge)" : " (noir)") + "."
                : "Faites vos jeux.";
        }

        private void UpdateCasino()
        {
            if (Time.unscaledTime < _spinUntil)
            {
                for (int i = 0; i < 3; i++) _reels[i] = Random.Range(0, Symbols.Length);
            }
            else if (_spinStake > 0)
            {
                Settle();
            }
        }

        private void Spin(int stake)
        {
            if (!_progress.Spend(stake, "Casino Royal — mise"))
            {
                Feedback("Pas assez d'argent.", true);
                return;
            }

            _spinStake = stake;
            _spinUntil = Time.unscaledTime + 1.3f;
        }

        private void Settle()
        {
            int stake = _spinStake;
            _spinStake = 0;

            // Tirage honnête… à la manière d'un casino : un peu moins de chances pour le 7.
            for (int i = 0; i < 3; i++)
            {
                float r = Random.value;
                _reels[i] = r < 0.1f ? 0 : r < 0.3f ? 1 : r < 0.55f ? 2 : r < 0.78f ? 3 : 4;
            }

            int win = 0;
            if (_reels[0] == _reels[1] && _reels[1] == _reels[2]) win = _reels[0] == 0 ? stake * 25 : stake * 8;
            else if (_reels[0] == _reels[1] || _reels[1] == _reels[2]) win = Mathf.RoundToInt(stake * 1.5f);

            if (win > 0)
            {
                _progress.AddMoney(win, "Casino Royal — gain");
                PlayCash();
                Feedback("Gagné : " + win + " € !", false);
                if (_clerk != null && win >= stake * 8) _clerk.Say("Jackpot ! La maison salue le champion.");
            }
            else
            {
                Feedback("Perdu. La machine te regarde.", true);
            }

            _progress.CountCasino(win, stake);
        }

        private void Roulette(int bet, int stake)
        {
            if (!_progress.Spend(stake, "Casino Royal — mise"))
            {
                Feedback("Pas assez d'argent.", true);
                return;
            }

            _rouletteNumber = Random.Range(0, 37);
            bool red = IsRed(_rouletteNumber);
            int win = 0;
            if (bet == 0 && _rouletteNumber != 0 && red) win = stake * 2;
            if (bet == 1 && _rouletteNumber != 0 && !red) win = stake * 2;
            if (bet == 2 && _rouletteNumber == 7) win = stake * 36;

            if (_clerk != null) _clerk.Say("Rien ne va plus… Le " + _rouletteNumber + (_rouletteNumber == 0 ? ", vert." : red ? ", rouge." : ", noir."));

            if (win > 0)
            {
                _progress.AddMoney(win, "Casino Royal — gain");
                PlayCash();
                Feedback("Le " + _rouletteNumber + " ! Gagné : " + win + " €.", false);
            }
            else
            {
                Feedback("Le " + _rouletteNumber + ". Perdu.", true);
            }

            _progress.CountCasino(win, stake);
        }

        private static bool IsRed(int n)
        {
            int[] reds = { 1, 3, 5, 7, 9, 12, 14, 16, 18, 19, 21, 23, 25, 27, 30, 32, 34, 36 };
            for (int i = 0; i < reds.Length; i++)
            {
                if (reds[i] == n) return true;
            }

            return false;
        }

        public void Configure(VehicleCatalog vehicles, HomeRegistry homes, ComputerScreen computer, SubtitleDisplay subtitles)
        {
            _vehicles = vehicles;
            _homes = homes;
            _computer = computer;
            _subtitles = subtitles;
        }
    }
}
