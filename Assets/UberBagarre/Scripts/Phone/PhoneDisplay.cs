using System.Collections.Generic;
using UberBagarre.Story;
using UberBagarre.UI;
using UnityEngine;

namespace UberBagarre.Phone
{
    /// <summary>
    /// L'interface du téléphone, dessinée SUR l'objet tenu en main.
    ///
    /// Le procédé : on projette les quatre coins de la dalle à l'écran, on en déduit une
    /// transformation affine, et l'interface est dessinée dedans. Le résultat suit l'inclinaison
    /// du poignet, le balancement et la vibration — l'interface appartient à l'objet au lieu
    /// d'être collée par-dessus.
    ///
    /// L'espace de mise en page n'est pas une résolution fixe, c'est la taille PROJETÉE en
    /// pixels : un corps 14 fait 14 pixels quel que soit l'écran, et c'est la mise en page qui
    /// s'adapte. Toutes les dimensions sont donc exprimées en fraction de la hauteur de l'écran.
    ///
    /// Ce qui est affiché vient de <see cref="PhoneOS"/> : l'écran d'accueil, les applis, et les
    /// écrans de l'histoire quand elle en impose un. Le thème est SOMBRE, et la luminosité suit
    /// les réglages du téléphone : de nuit, un écran se baisse, il n'éblouit pas.
    /// </summary>
    [RequireComponent(typeof(PhoneDevice))]
    public class PhoneDisplay : MonoBehaviour
    {
        [Header("References")]
        [SerializeField] private PhoneDevice _device;
        [SerializeField] private Camera _camera;
        [SerializeField] private MissionBriefing _briefing;

        [SerializeField]
        [Tooltip("Progression du joueur : niveau, argent, reputation. Optionnelle — sans elle, " +
                 "l'ecran de profil affiche un compte vierge.")]
        private PlayerProgress _progress;

        [SerializeField]
        [Tooltip("Le systeme du telephone. Sans lui, seul l'ecran impose par l'histoire est affiche.")]
        private PhoneOS _os;

        [Header("Dalle")]
        [SerializeField, Min(0.005f)]
        [Tooltip("Largeur de la dalle en metres, mesuree sur l'axe X local de l'ecran.")]
        private float _screenWidth = 0.070f;

        [SerializeField, Min(0.005f)] private float _screenHeight = 0.148f;

        [SerializeField, Range(0f, 0.1f)]
        [Tooltip("Marge interieure, en fraction de la hauteur. Elle absorbe l'ecart entre le " +
                 "parallelogramme dessine et le trapeze reel de la dalle inclinee.")]
        private float _inset = 0.018f;

        [Header("Couleurs")]
        [SerializeField] private Color _background = new Color(0.035f, 0.035f, 0.05f);
        [SerializeField] private Color _ink = new Color(0.93f, 0.93f, 0.95f);
        [SerializeField] private Color _dim = new Color(0.60f, 0.62f, 0.68f);
        [SerializeField] private Color _brand = new Color(1f, 0.20f, 0.62f);
        [SerializeField] private Color _good = new Color(0.36f, 0.95f, 0.52f);
        [SerializeField] private Color _bad = new Color(1f, 0.32f, 0.30f);
        [SerializeField] private Color _warn = new Color(1f, 0.74f, 0.25f);

        [Header("Horloge")]
        [SerializeField] private string _clock = "02:47";
        [SerializeField] private string _date = "SAM. 14 MARS";
        [SerializeField, Range(0, 100)] private int _battery = 12;

        private float _w;
        private float _h;
        private readonly GUIContent _content = new GUIContent();

        /// <summary>
        /// La course affichée. Le scénario la change d'un chapitre à l'autre : le téléphone
        /// montre toujours LE contrat en cours, jamais une liste.
        /// </summary>
        public MissionBriefing Briefing
        {
            get { return _briefing; }
            set { _briefing = value; }
        }

        /// <summary>Ligne affichée sous le viseur photo (« 1 / 2 »). Vide = rien.</summary>
        public string PhotoCounter { get; set; }

        /// <summary>Capacité à annoncer sur l'écran de profil. Vide = aucune annonce.</summary>
        public string UnlockedAbility { get; set; }

        private void Awake()
        {
            if (_device == null) _device = GetComponent<PhoneDevice>();
            if (_os == null) _os = GetComponent<PhoneOS>();
        }

        private void OnGUI()
        {
            if (_device == null) return;
            if (UberBagarre.UI.GameMenu.ShowingTitle) return;

            // Seuil franc plutot qu'un fondu : GuiKit.Fill impose sa propre couleur a chaque
            // appel, donc un GUI.color global ne ferait pas fondre les aplats — seulement le
            // texte. Un allumage net se lit comme un ecran qui se reveille.
            if (_device.RaiseAmount < 0.45f) return;

            // En mode photo, l'image entiere est le viseur : c'est PhoneCamera qui dessine. Pendant
            // une cinematique, la camera du joueur est eteinte : projeter l'ecran depuis une autre
            // camera le ferait flotter n'importe ou.
            if (_device.CameraMode || FightIntro.AnyPlaying) return;

            Transform dalle = _device.ScreenTransform;
            if (dalle == null) return;

            Camera camera = GuiKit.ActiveCamera(_camera);
            if (camera == null) return;

            Vector3 center = dalle.position;
            Vector3 right = dalle.right * (_screenWidth * 0.5f);
            Vector3 up = dalle.up * (_screenHeight * 0.5f);

            Vector2 topLeft;
            Vector2 topRight;
            Vector2 bottomLeft;

            if (!GuiKit.WorldToGui(camera, center - right + up, out topLeft)) return;
            if (!GuiKit.WorldToGui(camera, center + right + up, out topRight)) return;
            if (!GuiKit.WorldToGui(camera, center - right - up, out bottomLeft)) return;

            _w = (topRight - topLeft).magnitude;
            _h = (bottomLeft - topLeft).magnitude;

            // Sous cette taille, aucune police n'est lisible : mieux vaut ne rien dessiner que
            // d'afficher une bouillie de pixels qui donnera l'impression d'un bug.
            if (_w < 40f || _h < 80f) return;

            Matrix4x4 previous = GUI.matrix;

            Vector2 ex = (topRight - topLeft) / _w;
            Vector2 ey = (bottomLeft - topLeft) / _h;

            Matrix4x4 m = Matrix4x4.identity;
            m.m00 = ex.x; m.m01 = ey.x; m.m03 = topLeft.x;
            m.m10 = ex.y; m.m11 = ey.y; m.m13 = topLeft.y;

            GUI.matrix = m;

            DrawScreen(new Rect(0f, 0f, _w, _h));

            GUI.matrix = previous;
        }

        // ------------------------------------------------------------------ mise en page

        private float U(float fraction)
        {
            return _h * fraction;
        }

        private int Font(float fraction)
        {
            return Mathf.Max(10, Mathf.RoundToInt(_h * fraction));
        }

        private void DrawScreen(Rect screen)
        {
            GuiKit.Fill(screen, _background);

            float inset = U(_inset);
            Rect safe = new Rect(screen.x + inset, screen.y + inset,
                screen.width - inset * 2f, screen.height - inset * 2f);

            bool home = _os != null && _os.Current == PhoneOS.App.Accueil &&
                        _device.Current != PhoneDevice.Screen.Installation && !_device.IsRinging;

            if (home) DrawWallpaper(screen);

            DrawStatusBar(safe);

            Rect body = new Rect(safe.x, safe.y + U(0.045f), safe.width, safe.height - U(0.085f));

            if (_os == null) DrawStory(body);
            else DrawOS(body);

            string toast = _os != null ? _os.Toast : null;
            if (!string.IsNullOrEmpty(toast)) DrawToast(body, toast);

            // La barre de geste en bas : deux pixels de haut, et le rectangle cesse d'etre un
            // rectangle pour devenir un telephone.
            GuiKit.Fill(new Rect(screen.center.x - U(0.09f), screen.yMax - U(0.022f),
                U(0.18f), U(0.006f)), new Color(1f, 1f, 1f, 0.35f));

            // Luminosite et mode nuit, appliques a TOUT l'ecran, texte compris : c'est un
            // voile, comme le vrai reglage d'un telephone.
            float brightness = _os != null ? _os.Brightness : 0.6f;
            GuiKit.Fill(screen, new Color(0f, 0f, 0f, (1f - brightness) * 0.62f));

            if (_os != null && _os.NightMode) GuiKit.Fill(screen, new Color(1f, 0.55f, 0.2f, 0.06f));
        }

        /// <summary>Ce que l'histoire impose, sans système autour (scène ancienne, sans PhoneOS).</summary>
        private void DrawStory(Rect body)
        {
            switch (_device.Current)
            {
                case PhoneDevice.Screen.AppelEntrant: DrawIncomingCall(body); break;
                case PhoneDevice.Screen.EnAppel: DrawOngoingCall(body); break;
                case PhoneDevice.Screen.Lien: DrawLink(body); break;
                case PhoneDevice.Screen.Installation: DrawInstall(body); break;
                case PhoneDevice.Screen.Accueil: DrawContract(body); break;
                case PhoneDevice.Screen.Cible: DrawTarget(body); break;
                case PhoneDevice.Screen.Mission: DrawMission(body); break;
                case PhoneDevice.Screen.Photo: DrawCamera(body); break;
                case PhoneDevice.Screen.Valide: DrawValidated(body); break;
                case PhoneDevice.Screen.Profil: DrawProfile(body); break;
                default: DrawLock(body); break;
            }
        }

        private void DrawOS(Rect body)
        {
            if (_device.Current == PhoneDevice.Screen.Installation)
            {
                DrawInstall(body);
                return;
            }

            switch (_os.Current)
            {
                case PhoneOS.App.UberBagarre: DrawRdv(body); break;
                case PhoneOS.App.Messages: DrawMessages(body); break;
                case PhoneOS.App.Appels: DrawCalls(body); break;
                case PhoneOS.App.Photo: DrawCameraSplash(body); break;
                case PhoneOS.App.Galerie: DrawGallery(body); break;
                case PhoneOS.App.Banque: DrawBank(body); break;
                case PhoneOS.App.Carte: DrawMap(body); break;
                case PhoneOS.App.Reglages: DrawSettings(body); break;
                default: DrawHomeScreen(body); break;
            }
        }

        private void DrawStatusBar(Rect safe)
        {
            Label(new Rect(safe.x, safe.y, safe.width * 0.5f, U(0.032f)), _clock,
                Font(0.026f), FontStyle.Bold, TextAnchor.MiddleLeft, _ink);

            // Reseau : trois barres, dont une eteinte. Un signal plein dans une maison
            // insalubre a 2 h du matin, ca ne raconte rien.
            float x = safe.xMax - U(0.115f);

            for (int i = 0; i < 3; i++)
            {
                float height = U(0.008f + i * 0.006f);

                GuiKit.Fill(new Rect(x + i * U(0.014f), safe.y + U(0.028f) - height, U(0.009f), height),
                    i < 2 ? _ink : new Color(1f, 1f, 1f, 0.22f));
            }

            Rect battery = new Rect(safe.xMax - U(0.055f), safe.y + U(0.008f), U(0.042f), U(0.019f));
            GuiKit.Outline(battery, Mathf.Max(1f, U(0.002f)), new Color(1f, 1f, 1f, 0.55f));

            float charge = Mathf.Clamp01(_battery / 100f);

            GuiKit.Fill(new Rect(battery.x + 2f, battery.y + 2f,
                    Mathf.Max(1f, (battery.width - 4f) * charge), battery.height - 4f),
                charge < 0.2f ? _bad : _good);
        }

        // ------------------------------------------------------------------ accueil

        private void DrawWallpaper(Rect screen)
        {
            // Degrade vertical en bandes : violet nuit en haut, bleu encre en bas.
            Color top = new Color(0.10f, 0.05f, 0.14f);
            Color bottom = new Color(0.03f, 0.05f, 0.10f);
            const int bands = 12;

            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                GuiKit.Fill(new Rect(screen.x, screen.y + screen.height * i / bands, screen.width,
                    screen.height / bands + 1f), Color.Lerp(top, bottom, t));
            }

            // Un halo de neon rose, bas et large : la ville la nuit, vue d'un ecran.
            GuiKit.Disc(new Rect(screen.x - screen.width * 0.4f, screen.yMax - screen.width * 0.9f,
                screen.width * 1.8f, screen.width * 1.4f), new Color(_brand.r, _brand.g, _brand.b, 0.10f));
        }

        private void DrawHomeScreen(Rect body)
        {
            Label(new Rect(body.x, body.y + U(0.02f), body.width, U(0.085f)), _clock,
                Font(0.075f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);

            Label(new Rect(body.x, body.y + U(0.105f), body.width, U(0.035f)), _date,
                Font(0.022f), FontStyle.Normal, TextAnchor.MiddleCenter, _dim);

            IList<PhoneOS.App> apps = _os.Apps;

            float cell = body.width / 3f;
            float icon = Mathf.Min(cell * 0.62f, U(0.078f));
            float rowHeight = U(0.125f);
            float top = body.y + U(0.175f);

            for (int i = 0; i < apps.Count; i++)
            {
                int col = i % 3;
                int row = i / 3;

                Rect slot = new Rect(body.x + col * cell, top + row * rowHeight, cell, rowHeight);
                Rect iconRect = new Rect(slot.center.x - icon * 0.5f, slot.y + U(0.008f), icon, icon);

                bool selected = i == _os.HomeSelection;

                if (selected)
                {
                    GuiKit.Disc(new Rect(iconRect.x - icon * 0.35f, iconRect.y - icon * 0.35f, icon * 1.7f, icon * 1.7f),
                        new Color(1f, 1f, 1f, 0.10f));
                }

                DrawIcon(apps[i], iconRect);

                if (selected) GuiKit.Outline(iconRect, Mathf.Max(1.5f, U(0.004f)), new Color(1f, 1f, 1f, 0.9f));

                int badge = _os.Badge(apps[i]);
                if (badge > 0)
                {
                    float size = U(0.026f);
                    Rect dot = new Rect(iconRect.xMax - size * 0.6f, iconRect.y - size * 0.4f, size, size);
                    GuiKit.Disc(dot, new Color(0.95f, 0.18f, 0.20f));
                    GuiKit.Disc(new Rect(dot.x + size * 0.2f, dot.y + size * 0.2f, size * 0.6f, size * 0.6f),
                        new Color(0.95f, 0.18f, 0.20f));
                    Label(dot, badge.ToString(), Font(0.018f), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);
                }

                Label(new Rect(slot.x - U(0.012f), iconRect.yMax + U(0.006f), slot.width + U(0.024f), U(0.03f)), PhoneOS.AppName(apps[i]),
                    Font(0.0175f), selected ? FontStyle.Bold : FontStyle.Normal, TextAnchor.UpperCenter,
                    selected ? _ink : _dim);
            }

            Label(new Rect(body.x, body.yMax - U(0.075f), body.width, U(0.07f)),
                "FLECHES / MOLETTE : choisir\nENTREE / CLIC / E : ouvrir   —   RETOUR : ranger",
                Font(0.0165f), FontStyle.Normal, TextAnchor.MiddleCenter, new Color(1f, 1f, 1f, 0.38f), true);
        }

        /// <summary>
        /// Les icônes, dessinées avec des rectangles et des disques : aucune image, aucune police
        /// d'icônes. Un pictogramme simple et net vaut mieux qu'une lettre dans un carré.
        /// </summary>
        private void DrawIcon(PhoneOS.App app, Rect r)
        {
            Color color = PhoneOS.AppColor(app);

            // Carre arrondi : un aplat et quatre disques aux coins.
            float round = r.width * 0.22f;
            GuiKit.Fill(new Rect(r.x + round * 0.5f, r.y, r.width - round, r.height), color);
            GuiKit.Fill(new Rect(r.x, r.y + round * 0.5f, r.width, r.height - round), color);
            Corner(new Rect(r.x, r.y, round, round), color);
            Corner(new Rect(r.xMax - round, r.y, round, round), color);
            Corner(new Rect(r.x, r.yMax - round, round, round), color);
            Corner(new Rect(r.xMax - round, r.yMax - round, round, round), color);

            // Reflet haut : un peu de volume.
            GuiKit.Fill(new Rect(r.x + round * 0.5f, r.y, r.width - round, r.height * 0.18f), new Color(1f, 1f, 1f, 0.10f));

            Color white = new Color(1f, 1f, 1f, 0.95f);
            float w = r.width;
            float cx = r.center.x;
            float cy = r.center.y;

            switch (app)
            {
                case PhoneOS.App.UberBagarre:
                    GuiKit.Disc(new Rect(cx - w * 0.36f, cy - w * 0.36f, w * 0.72f, w * 0.72f), new Color(1f, 1f, 1f, 0.25f));
                    Label(r, "U", Mathf.Max(10, Mathf.RoundToInt(w * 0.55f)), FontStyle.Bold, TextAnchor.MiddleCenter, white);
                    break;

                case PhoneOS.App.Messages:
                    GuiKit.Fill(new Rect(cx - w * 0.30f, cy - w * 0.20f, w * 0.60f, w * 0.34f), white);
                    GuiKit.Disc(new Rect(cx - w * 0.36f, cy - w * 0.22f, w * 0.20f, w * 0.38f), white);
                    GuiKit.Disc(new Rect(cx + w * 0.16f, cy - w * 0.22f, w * 0.20f, w * 0.38f), white);
                    GuiKit.Fill(new Rect(cx - w * 0.22f, cy + w * 0.12f, w * 0.09f, w * 0.12f), white);
                    break;

                case PhoneOS.App.Appels:
                {
                    Matrix4x4 saved = BeginRotate(new Vector2(cx, cy), -40f);
                    GuiKit.Fill(new Rect(cx - w * 0.07f, cy - w * 0.26f, w * 0.14f, w * 0.52f), white);
                    GuiKit.Fill(new Rect(cx - w * 0.07f, cy - w * 0.30f, w * 0.26f, w * 0.12f), white);
                    GuiKit.Fill(new Rect(cx - w * 0.07f, cy + w * 0.18f, w * 0.26f, w * 0.12f), white);
                    GUI.matrix = saved;
                    break;
                }

                case PhoneOS.App.Photo:
                    GuiKit.Fill(new Rect(cx - w * 0.32f, cy - w * 0.16f, w * 0.64f, w * 0.40f), white);
                    GuiKit.Fill(new Rect(cx - w * 0.12f, cy - w * 0.25f, w * 0.24f, w * 0.10f), white);
                    GuiKit.Disc(new Rect(cx - w * 0.17f, cy - w * 0.13f, w * 0.34f, w * 0.34f), color);
                    GuiKit.Disc(new Rect(cx - w * 0.10f, cy - w * 0.06f, w * 0.20f, w * 0.20f), new Color(0.12f, 0.12f, 0.16f));
                    break;

                case PhoneOS.App.Galerie:
                    GuiKit.Fill(new Rect(cx - w * 0.30f, cy - w * 0.26f, w * 0.60f, w * 0.52f), white);
                    GuiKit.Fill(new Rect(cx - w * 0.25f, cy - w * 0.21f, w * 0.50f, w * 0.42f), new Color(0.30f, 0.55f, 0.90f));
                    GuiKit.Disc(new Rect(cx + w * 0.04f, cy - w * 0.18f, w * 0.14f, w * 0.14f), new Color(1f, 0.92f, 0.5f));
                    GuiKit.Fill(new Rect(cx - w * 0.25f, cy + w * 0.04f, w * 0.50f, w * 0.17f), new Color(0.24f, 0.62f, 0.32f));
                    break;

                case PhoneOS.App.Banque:
                    Label(r, "€", Mathf.Max(10, Mathf.RoundToInt(w * 0.58f)), FontStyle.Bold, TextAnchor.MiddleCenter, white);
                    break;

                case PhoneOS.App.Carte:
                {
                    GuiKit.Disc(new Rect(cx - w * 0.18f, cy - w * 0.30f, w * 0.36f, w * 0.36f), white);
                    Matrix4x4 saved = BeginRotate(new Vector2(cx, cy + w * 0.02f), 45f);
                    GuiKit.Fill(new Rect(cx - w * 0.12f, cy - w * 0.10f, w * 0.24f, w * 0.24f), white);
                    GUI.matrix = saved;
                    GuiKit.Disc(new Rect(cx - w * 0.07f, cy - w * 0.19f, w * 0.14f, w * 0.14f), color);
                    break;
                }

                case PhoneOS.App.Reglages:
                    for (int i = 0; i < 8; i++)
                    {
                        float a = i * Mathf.PI / 4f;
                        float px = cx + Mathf.Cos(a) * w * 0.25f;
                        float py = cy + Mathf.Sin(a) * w * 0.25f;
                        GuiKit.Fill(new Rect(px - w * 0.06f, py - w * 0.06f, w * 0.12f, w * 0.12f), white);
                    }

                    GuiKit.Disc(new Rect(cx - w * 0.29f, cy - w * 0.29f, w * 0.58f, w * 0.58f), white);
                    GuiKit.Disc(new Rect(cx - w * 0.11f, cy - w * 0.11f, w * 0.22f, w * 0.22f), color);
                    break;
            }
        }

        private static void Corner(Rect rect, Color color)
        {
            // Un disque dur : GuiKit.Disc est doux, on le double pour un bord net.
            GuiKit.Disc(rect, color);
            GuiKit.Disc(rect, color);
            GuiKit.Disc(new Rect(rect.x + rect.width * 0.15f, rect.y + rect.height * 0.15f,
                rect.width * 0.7f, rect.height * 0.7f), color);
        }

        /// <summary>Tourne le dessin autour d'un point. Rendre ensuite la matrice renvoyée à GUI.matrix.</summary>
        private static Matrix4x4 BeginRotate(Vector2 pivot, float angle)
        {
            Matrix4x4 previous = GUI.matrix;
            GUIUtility.RotateAroundPivot(angle, pivot);
            return previous;
        }

        // ------------------------------------------------------------------ applis : cadre

        /// <summary>Barre de titre d'une appli. Renvoie la zone de contenu en dessous.</summary>
        private Rect Header(Rect body, string title, Color accent)
        {
            Label(new Rect(body.x, body.y, body.width * 0.7f, U(0.045f)), title,
                Font(0.030f), FontStyle.Bold, TextAnchor.MiddleLeft, _ink);

            Label(new Rect(body.x + body.width * 0.5f, body.y, body.width * 0.5f, U(0.045f)), "< retour",
                Font(0.017f), FontStyle.Normal, TextAnchor.MiddleRight, new Color(1f, 1f, 1f, 0.35f));

            GuiKit.Fill(new Rect(body.x, body.y + U(0.05f), body.width, Mathf.Max(1f, U(0.003f))),
                new Color(accent.r, accent.g, accent.b, 0.8f));

            return new Rect(body.x, body.y + U(0.065f), body.width, body.height - U(0.065f));
        }

        private void DrawToast(Rect body, string text)
        {
            Rect pill = new Rect(body.x + U(0.01f), body.yMax - U(0.15f), body.width - U(0.02f), U(0.07f));
            GuiKit.Fill(pill, new Color(0.14f, 0.15f, 0.20f, 0.96f));
            GuiKit.Outline(pill, Mathf.Max(1f, U(0.002f)), new Color(1f, 1f, 1f, 0.2f));

            Label(new Rect(pill.x + U(0.015f), pill.y, pill.width - U(0.03f), pill.height), text,
                Font(0.019f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink, true);
        }

        private void Selection(Rect row, bool selected, Color accent)
        {
            if (!selected) return;

            GuiKit.Fill(row, new Color(1f, 1f, 1f, 0.09f));
            GuiKit.Fill(new Rect(row.x, row.y, Mathf.Max(2f, U(0.005f)), row.height), accent);
        }

        // ------------------------------------------------------------------ appli UBER BAGARRE

        private void DrawRdv(Rect body)
        {
            // Onglets : la course (ce que l'histoire affiche) et le profil.
            float half = body.width * 0.5f;
            string[] tabs = { "COURSE", "PROFIL" };

            for (int i = 0; i < 2; i++)
            {
                Rect tab = new Rect(body.x + i * half, body.y, half, U(0.04f));
                bool active = _os.RdvTab == i;

                Label(tab, tabs[i], Font(0.021f), FontStyle.Bold, TextAnchor.MiddleCenter, active ? _ink : _dim);

                if (active)
                {
                    GuiKit.Fill(new Rect(tab.x + half * 0.2f, tab.yMax, half * 0.6f, Mathf.Max(2f, U(0.004f))), _brand);
                }
            }

            Rect content = new Rect(body.x, body.y + U(0.06f), body.width, body.height - U(0.06f));

            if (_os.RdvTab == 1)
            {
                DrawProfile(content);
                return;
            }

            PhoneDevice.Screen view = IsRdvScreen(_device.Current) ? _device.Current : _os.LastRdvScreen;

            switch (view)
            {
                case PhoneDevice.Screen.Accueil: DrawContract(content); break;
                case PhoneDevice.Screen.Cible: DrawTarget(content); break;
                case PhoneDevice.Screen.Mission: DrawMission(content); break;
                case PhoneDevice.Screen.Valide: DrawValidated(content); break;
                case PhoneDevice.Screen.Profil: DrawProfile(content); break;
                default:
                    Logo(new Rect(content.center.x - U(0.12f), content.y + U(0.10f), U(0.24f), U(0.24f)), 0.6f);
                    Label(new Rect(content.x, content.y + U(0.38f), content.width, U(0.12f)),
                        "Aucune course.\nReste joignable : une commande peut tomber a tout moment.",
                        Font(0.021f), FontStyle.Normal, TextAnchor.UpperCenter, _dim, true);
                    break;
            }
        }

        private static bool IsRdvScreen(PhoneDevice.Screen screen)
        {
            return screen == PhoneDevice.Screen.Accueil || screen == PhoneDevice.Screen.Cible ||
                   screen == PhoneDevice.Screen.Mission || screen == PhoneDevice.Screen.Valide ||
                   screen == PhoneDevice.Screen.Profil;
        }

        // ------------------------------------------------------------------ messages

        private void DrawMessages(Rect body)
        {
            if (_os.OpenThread >= 0)
            {
                DrawThread(body, _os.OpenThread);
                return;
            }

            Rect list = Header(body, "Messages", PhoneOS.AppColor(PhoneOS.App.Messages));
            float rowHeight = U(0.085f);

            for (int i = 0; i < _os.ThreadCount; i++)
            {
                Rect row = new Rect(list.x, list.y + i * rowHeight, list.width, rowHeight - U(0.006f));
                bool selected = i == _os.ListSelection;
                bool unread = _os.Unread(i);

                Selection(row, selected, PhoneOS.AppColor(PhoneOS.App.Messages));

                float avatar = U(0.052f);
                Rect disc = new Rect(row.x + U(0.012f), row.center.y - avatar * 0.5f, avatar, avatar);
                GuiKit.Disc(disc, PhoneOS.ThreadColor(i));
                GuiKit.Disc(disc, PhoneOS.ThreadColor(i));

                string name = PhoneOS.ThreadName(i);
                Label(disc, name.Substring(0, 1), Font(0.024f), FontStyle.Bold, TextAnchor.MiddleCenter, Color.white);

                float x = disc.xMax + U(0.014f);

                Label(new Rect(x, row.y + U(0.008f), row.width - (x - row.x) - U(0.02f), U(0.03f)), name,
                    Font(0.021f), unread ? FontStyle.Bold : FontStyle.Normal, TextAnchor.MiddleLeft, _ink);

                IList<PhoneOS.Message> thread = _os.Messages(i);
                string preview = thread.Count > 0 ? thread[thread.Count - 1].Text : "";
                if (preview.Length > 30) preview = preview.Substring(0, 29) + "…";

                Label(new Rect(x, row.y + U(0.040f), row.width - (x - row.x) - U(0.02f), U(0.03f)), preview,
                    Font(0.0175f), FontStyle.Normal, TextAnchor.MiddleLeft, unread ? _ink : _dim);

                if (unread)
                {
                    float dot = U(0.014f);
                    GuiKit.Disc(new Rect(row.xMax - dot - U(0.01f), row.center.y - dot * 0.5f, dot, dot), _good);
                }
            }
        }

        private void DrawThread(Rect body, int index)
        {
            Rect area = Header(body, PhoneOS.ThreadName(index), PhoneOS.ThreadColor(index));

            IList<PhoneOS.Message> messages = _os.Messages(index);
            int count = messages.Count;

            float maxWidth = area.width * 0.78f;
            float padding = U(0.012f);
            float gap = U(0.012f);
            int fontSize = Font(0.0195f);
            GUIStyle style = GuiKit.Style(fontSize, FontStyle.Normal, TextAnchor.UpperLeft, true);

            // Les bulles partent du bas, la plus recente en dernier ; le defilement remonte.
            float y = area.yMax - U(0.02f);
            int last = count - 1 - _os.ThreadScroll;

            for (int i = last; i >= 0; i--)
            {
                PhoneOS.Message message = messages[i];

                float textWidth = maxWidth - padding * 2f;
                _content.text = message.Text;
                float height = style.CalcHeight(_content, textWidth) + padding * 2f;
                if (message.IsLink) height += U(0.07f);

                y -= height;
                if (y < area.y) break;

                float width = message.IsLink ? maxWidth : Mathf.Min(maxWidth,
                    style.CalcSize(_content).x + padding * 2f + 2f);

                float x = message.Mine ? area.xMax - width : area.x;
                Color fill = message.Mine ? new Color(_brand.r * 0.55f, _brand.g * 0.55f, _brand.b * 0.6f)
                                          : new Color(0.16f, 0.17f, 0.22f);

                Rect bubble = new Rect(x, y, width, height);
                GuiKit.Fill(bubble, fill);

                Label(new Rect(bubble.x + padding, bubble.y + padding, textWidth, height - padding * 2f),
                    message.Text, fontSize, message.IsLink ? FontStyle.Bold : FontStyle.Normal,
                    TextAnchor.UpperLeft, message.IsLink ? new Color(0.55f, 0.8f, 1f) : _ink, true);

                if (message.IsLink) DrawLinkAction(bubble, padding);

                y -= gap;
            }
        }

        /// <summary>Le lien de Sami : le bouton d'installation tant que l'appli n'est pas là.</summary>
        private void DrawLinkAction(Rect bubble, float padding)
        {
            Rect button = new Rect(bubble.x + padding, bubble.yMax - U(0.065f), bubble.width - padding * 2f, U(0.052f));

            if (_device.AppInstalled)
            {
                Label(button, "INSTALLEE", Font(0.018f), FontStyle.Bold, TextAnchor.MiddleLeft, _good);
                return;
            }

            // L'avertissement est le coeur de la scene : l'application est illegale, et le jeu
            // doit le dire avant que le joueur ne l'installe, pas apres.
            Label(new Rect(button.x, button.y - U(0.03f), button.width, U(0.028f)), "Source inconnue — non referencee",
                Font(0.016f), FontStyle.Italic, TextAnchor.MiddleLeft, _warn);

            float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.4f));
            GuiKit.Fill(button, new Color(_brand.r, _brand.g, _brand.b, 0.25f + 0.35f * pulse));
            GuiKit.Outline(button, Mathf.Max(1f, U(0.003f)), _brand);

            Label(button, "E — INSTALLER", Font(0.020f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);
        }

        // ------------------------------------------------------------------ appels

        private void DrawCalls(Rect body)
        {
            if (_device.IsRinging)
            {
                DrawIncomingCall(body);
                return;
            }

            if (_device.Current == PhoneDevice.Screen.EnAppel)
            {
                DrawOngoingCall(body);
                return;
            }

            Rect list = Header(body, "Appels", PhoneOS.AppColor(PhoneOS.App.Appels));
            IList<PhoneOS.Call> calls = _os.Calls;
            float rowHeight = U(0.075f);

            for (int i = 0; i < calls.Count; i++)
            {
                Rect row = new Rect(list.x, list.y + i * rowHeight, list.width, rowHeight - U(0.006f));
                Selection(row, i == _os.ListSelection, PhoneOS.AppColor(PhoneOS.App.Appels));

                PhoneOS.Call call = calls[i];

                Label(new Rect(row.x + U(0.02f), row.y + U(0.006f), row.width - U(0.04f), U(0.032f)), call.Name,
                    Font(0.022f), FontStyle.Bold, TextAnchor.MiddleLeft, call.Missed ? _bad : _ink);

                Label(new Rect(row.x + U(0.02f), row.y + U(0.036f), row.width - U(0.04f), U(0.028f)), call.Detail,
                    Font(0.0175f), FontStyle.Normal, TextAnchor.MiddleLeft, _dim);
            }
        }

        // ------------------------------------------------------------------ photo et galerie

        /// <summary>Le temps que le téléphone monte à l'œil : un écran d'ouverture sobre.</summary>
        private void DrawCameraSplash(Rect body)
        {
            GuiKit.Fill(body, new Color(0f, 0f, 0f, 0.8f));
            Label(body, "PHOTO", Font(0.03f), FontStyle.Bold, TextAnchor.MiddleCenter, _dim);
        }

        private void DrawGallery(Rect body)
        {
            int count = PhoneGallery.Count;

            if (_os.OpenPhoto >= 0 && _os.OpenPhoto < count)
            {
                Rect area = Header(body, "Photo " + (_os.OpenPhoto + 1) + " / " + count, PhoneOS.AppColor(PhoneOS.App.Galerie));
                Texture2D photo = PhoneGallery.Get(_os.OpenPhoto);

                if (photo != null)
                {
                    Rect frame = new Rect(area.x, area.y + U(0.1f), area.width, area.width * photo.height / photo.width);
                    GUI.DrawTexture(frame, photo, ScaleMode.ScaleToFit);
                }

                Label(new Rect(area.x, area.yMax - U(0.06f), area.width, U(0.04f)), "< >  photo precedente / suivante",
                    Font(0.017f), FontStyle.Normal, TextAnchor.MiddleCenter, _dim);
                return;
            }

            Rect grid = Header(body, "Galerie", PhoneOS.AppColor(PhoneOS.App.Galerie));

            if (count == 0)
            {
                Label(new Rect(grid.x, grid.y + U(0.2f), grid.width, U(0.12f)),
                    "Aucune photo.\nOuvre Photo, puis clic gauche.", Font(0.021f), FontStyle.Normal,
                    TextAnchor.UpperCenter, _dim, true);
                return;
            }

            float cell = grid.width / 3f;

            for (int i = 0; i < count; i++)
            {
                Rect slot = new Rect(grid.x + (i % 3) * cell, grid.y + (i / 3) * cell, cell, cell);
                if (slot.yMax > grid.yMax) break;

                Rect thumb = new Rect(slot.x + 2f, slot.y + 2f, slot.width - 4f, slot.height - 4f);
                Texture2D photo = PhoneGallery.Get(i);
                if (photo != null) GUI.DrawTexture(thumb, photo, ScaleMode.ScaleAndCrop);

                if (i == _os.ListSelection) GuiKit.Outline(thumb, Mathf.Max(1.5f, U(0.004f)), Color.white);
            }
        }

        // ------------------------------------------------------------------ banque, carte, reglages

        private void DrawBank(Rect body)
        {
            Rect area = Header(body, "Banque", PhoneOS.AppColor(PhoneOS.App.Banque));
            PlayerProgress progress = _os.Progress;
            int wallet = progress != null ? progress.Money : 10;

            Rect account = new Rect(area.x, area.y, area.width, U(0.11f));
            GuiKit.Fill(account, new Color(1f, 1f, 1f, 0.06f));
            Label(new Rect(account.x + U(0.02f), account.y + U(0.01f), account.width, U(0.03f)), "COMPTE COURANT",
                Font(0.017f), FontStyle.Bold, TextAnchor.MiddleLeft, _dim);
            Label(new Rect(account.x + U(0.02f), account.y + U(0.045f), account.width - U(0.04f), U(0.05f)), "-1 240,18 €",
                Font(0.034f), FontStyle.Bold, TextAnchor.MiddleLeft, _bad);

            Rect cash = new Rect(area.x, account.yMax + U(0.015f), area.width, U(0.11f));
            GuiKit.Fill(cash, new Color(1f, 1f, 1f, 0.06f));
            Label(new Rect(cash.x + U(0.02f), cash.y + U(0.01f), cash.width, U(0.03f)), "PORTEFEUILLE UB SERVICES",
                Font(0.017f), FontStyle.Bold, TextAnchor.MiddleLeft, _dim);
            Label(new Rect(cash.x + U(0.02f), cash.y + U(0.045f), cash.width - U(0.04f), U(0.05f)), wallet + ",00 €",
                Font(0.034f), FontStyle.Bold, TextAnchor.MiddleLeft, _good);

            float y = cash.yMax + U(0.03f);
            Label(new Rect(area.x, y, area.width, U(0.03f)), "DERNIERES OPERATIONS", Font(0.017f), FontStyle.Bold,
                TextAnchor.MiddleLeft, _dim);
            y += U(0.04f);

            int contracts = progress != null ? progress.Contracts : 0;
            if (contracts > 0) Row(new Rect(area.x, y, area.width, U(0.04f)), "VIR. UB SERVICES x" + contracts, "reçu", _good);
            if (contracts > 0) y += U(0.045f);

            Row(new Rect(area.x, y, area.width, U(0.04f)), "PRLV LOYER", "rejeté", _bad);
            y += U(0.045f);
            Row(new Rect(area.x, y, area.width, U(0.04f)), "FRAIS DE REJET", "-20,00", _bad);
            y += U(0.045f);
            Row(new Rect(area.x, y, area.width, U(0.04f)), "VIR. MAMAN", "+20,00", _good);
        }

        private void DrawMap(Rect body)
        {
            Rect area = Header(body, "Carte", PhoneOS.AppColor(PhoneOS.App.Carte));
            Rect map = new Rect(area.x, area.y, area.width, area.height - U(0.08f));

            GuiKit.Fill(map, new Color(0.07f, 0.08f, 0.10f));

            // Des rues : quelques bandes grises, et le fleuve en diagonale.
            Color road = new Color(0.20f, 0.21f, 0.25f);
            GuiKit.Fill(new Rect(map.x, map.y + map.height * 0.42f, map.width, U(0.012f)), road);
            GuiKit.Fill(new Rect(map.x, map.y + map.height * 0.72f, map.width, U(0.01f)), road);
            GuiKit.Fill(new Rect(map.x + map.width * 0.35f, map.y, U(0.01f), map.height), road);
            GuiKit.Fill(new Rect(map.x + map.width * 0.72f, map.y, U(0.012f), map.height), road);

            Matrix4x4 saved = BeginRotate(new Vector2(map.center.x, map.center.y), -28f);
            GuiKit.Fill(new Rect(map.x - map.width * 0.2f, map.center.y + map.height * 0.2f, map.width * 1.4f, U(0.018f)),
                new Color(0.12f, 0.20f, 0.32f));
            GUI.matrix = saved;

            string here = _os.LocationName;

            Pin(map, new Vector2(0.24f, 0.78f), "PLANQUE", here == "Maison");
            Pin(map, new Vector2(0.58f, 0.36f), "LE VERTIGO", here == "Rue" || here == "Club");
            Pin(map, new Vector2(0.82f, 0.52f), "PARKING", here == "Parking");

            Label(new Rect(area.x, area.yMax - U(0.07f), area.width, U(0.06f)),
                string.IsNullOrEmpty(here) ? "Position inconnue" : "Tu es ici : " + here,
                Font(0.019f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);
        }

        private void Pin(Rect map, Vector2 at, string name, bool here)
        {
            Vector2 p = new Vector2(map.x + map.width * at.x, map.y + map.height * at.y);
            float size = U(here ? 0.034f : 0.024f);

            if (here)
            {
                float pulse = 0.5f + 0.5f * Mathf.Sin(Time.unscaledTime * 4f);
                float halo = size * (1.8f + pulse);
                GuiKit.Disc(new Rect(p.x - halo * 0.5f, p.y - halo * 0.5f, halo, halo), new Color(0.3f, 0.6f, 1f, 0.35f));
            }

            GuiKit.Disc(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), here ? new Color(0.35f, 0.65f, 1f) : _brand);
            GuiKit.Disc(new Rect(p.x - size * 0.5f, p.y - size * 0.5f, size, size), here ? new Color(0.35f, 0.65f, 1f) : _brand);

            Label(new Rect(p.x - U(0.12f), p.y + size * 0.6f, U(0.24f), U(0.03f)), name, Font(0.016f),
                FontStyle.Bold, TextAnchor.UpperCenter, _ink);
        }

        private void DrawSettings(Rect body)
        {
            Rect area = Header(body, "Réglages", PhoneOS.AppColor(PhoneOS.App.Reglages));
            float rowHeight = U(0.08f);
            Color accent = PhoneOS.AppColor(PhoneOS.App.Reglages);

            // Luminosite : quatre crans.
            Rect row = new Rect(area.x, area.y, area.width, rowHeight - U(0.006f));
            Selection(row, _os.SettingSelection == 0, accent);
            Label(new Rect(row.x + U(0.02f), row.y, row.width * 0.5f, row.height), "Luminosité",
                Font(0.021f), FontStyle.Normal, TextAnchor.MiddleLeft, _ink);

            float segment = U(0.03f);
            for (int i = 0; i < 4; i++)
            {
                Rect seg = new Rect(row.xMax - U(0.02f) - (4 - i) * (segment + U(0.006f)), row.center.y - U(0.008f), segment, U(0.016f));
                GuiKit.Fill(seg, i <= _os.BrightnessLevel ? _warn : new Color(1f, 1f, 1f, 0.15f));
            }

            row.y += rowHeight;
            Selection(row, _os.SettingSelection == 1, accent);
            Label(new Rect(row.x + U(0.02f), row.y, row.width * 0.6f, row.height), "Mode nuit",
                Font(0.021f), FontStyle.Normal, TextAnchor.MiddleLeft, _ink);
            Toggle(row, _os.NightMode);

            row.y += rowHeight;
            Selection(row, _os.SettingSelection == 2, accent);
            Label(new Rect(row.x + U(0.02f), row.y, row.width * 0.6f, row.height), "Sonnerie",
                Font(0.021f), FontStyle.Normal, TextAnchor.MiddleLeft, _ink);
            Toggle(row, _os.Ringer);

            Label(new Rect(area.x, row.yMax + U(0.04f), area.width, U(0.1f)),
                "HAUT / BAS : choisir\nGAUCHE / DROITE / ENTREE : changer",
                Font(0.017f), FontStyle.Normal, TextAnchor.UpperCenter, _dim, true);
        }

        private void Toggle(Rect row, bool on)
        {
            Rect pill = new Rect(row.xMax - U(0.02f) - U(0.07f), row.center.y - U(0.017f), U(0.07f), U(0.034f));
            GuiKit.Fill(pill, on ? new Color(_good.r * 0.7f, _good.g * 0.7f, _good.b * 0.7f) : new Color(1f, 1f, 1f, 0.15f));

            float knob = U(0.028f);
            float x = on ? pill.xMax - knob - U(0.003f) : pill.x + U(0.003f);
            GuiKit.Disc(new Rect(x, pill.center.y - knob * 0.5f, knob, knob), Color.white);
            GuiKit.Disc(new Rect(x, pill.center.y - knob * 0.5f, knob, knob), Color.white);
        }

        // ------------------------------------------------------------------ écrans de l'histoire

        private void DrawLock(Rect body)
        {
            Label(new Rect(body.x, body.y + U(0.16f), body.width, U(0.11f)), _clock,
                Font(0.088f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);

            Label(new Rect(body.x, body.y + U(0.275f), body.width, U(0.04f)), _date,
                Font(0.026f), FontStyle.Normal, TextAnchor.MiddleCenter, _dim);

            // Les notifications font le décor : trois relances d'impayés qu'on lit en une
            // seconde et qui disent la situation du personnage mieux qu'une réplique.
            Notification(new Rect(body.x, body.y + U(0.40f), body.width, U(0.085f)),
                "BANQUE", "Decouvert : -1 240,18 EUR", _bad);

            Notification(new Rect(body.x, body.y + U(0.50f), body.width, U(0.085f)),
                "SFR", "Facture impayee. 3e relance.", _warn);

            Notification(new Rect(body.x, body.y + U(0.60f), body.width, U(0.085f)),
                "AGENCE", "Loyer de fevrier toujours en attente.", _warn);

            Label(new Rect(body.x, body.yMax - U(0.07f), body.width, U(0.04f)),
                "T pour ranger", Font(0.022f), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.35f));
        }

        private void Notification(Rect rect, string source, string text, Color accent)
        {
            GuiKit.Fill(rect, new Color(1f, 1f, 1f, 0.07f));
            GuiKit.Fill(new Rect(rect.x, rect.y, U(0.004f), rect.height), accent);

            Label(new Rect(rect.x + U(0.022f), rect.y + U(0.008f), rect.width - U(0.03f), U(0.028f)),
                source, Font(0.021f), FontStyle.Bold, TextAnchor.MiddleLeft, accent);

            Label(new Rect(rect.x + U(0.022f), rect.y + U(0.038f), rect.width - U(0.03f), U(0.038f)),
                text, Font(0.023f), FontStyle.Normal, TextAnchor.UpperLeft, _dim, true);
        }

        private void DrawIncomingCall(Rect body)
        {
            Label(new Rect(body.x, body.y + U(0.10f), body.width, U(0.04f)), "APPEL ENTRANT",
                Font(0.024f), FontStyle.Bold, TextAnchor.MiddleCenter, _good);

            // Pastille d'avatar : deux lettres suffisent, et ca vaut mieux qu'une photo
            // placeholder qui ferait "asset manquant".
            float size = U(0.19f);
            Rect avatar = new Rect(body.center.x - size * 0.5f, body.y + U(0.17f), size, size);
            GuiKit.Disc(avatar, new Color(0.22f, 0.26f, 0.34f));

            string caller = _device.Caller;
            string initials = caller.Length >= 2 ? caller.Substring(0, 2) : caller;

            Label(avatar, initials, Font(0.075f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);

            Label(new Rect(body.x, body.y + U(0.40f), body.width, U(0.06f)), caller,
                Font(0.048f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);

            Label(new Rect(body.x, body.y + U(0.465f), body.width, U(0.04f)), "mobile",
                Font(0.024f), FontStyle.Normal, TextAnchor.MiddleCenter, _dim);

            float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 4f));

            float buttonSize = U(0.11f);
            float y = body.yMax - U(0.20f);

            Rect accept = new Rect(body.center.x - U(0.13f) - buttonSize * 0.5f, y, buttonSize, buttonSize);
            GuiKit.Disc(accept, new Color(_good.r, _good.g, _good.b, pulse));
            Label(accept, "E", Font(0.05f), FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);

            Rect refuse = new Rect(body.center.x + U(0.13f) - buttonSize * 0.5f, y, buttonSize, buttonSize);
            GuiKit.Disc(refuse, new Color(_bad.r, _bad.g, _bad.b, 0.55f));

            Label(new Rect(body.x, body.yMax - U(0.06f), body.width, U(0.04f)),
                "E POUR REPONDRE", Font(0.024f), FontStyle.Bold, TextAnchor.MiddleCenter,
                new Color(_good.r, _good.g, _good.b, pulse));
        }

        private void DrawOngoingCall(Rect body)
        {
            Label(new Rect(body.x, body.y + U(0.12f), body.width, U(0.06f)), _device.Caller,
                Font(0.048f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);

            int seconds = Mathf.FloorToInt(_device.CallTime);
            string timer = (seconds / 60).ToString("00") + ":" + (seconds % 60).ToString("00");

            Label(new Rect(body.x, body.y + U(0.19f), body.width, U(0.045f)), timer,
                Font(0.030f), FontStyle.Normal, TextAnchor.MiddleCenter, _good);

            // Un niveau audio animé : c'est ce qui distingue « appel en cours » de « écran figé ».
            float baseY = body.y + U(0.35f);

            for (int i = 0; i < 9; i++)
            {
                float wave = Mathf.Abs(Mathf.Sin(Time.unscaledTime * 5f + i * 0.7f));
                float height = U(0.02f + wave * 0.075f);

                GuiKit.Fill(new Rect(body.center.x - U(0.115f) + i * U(0.026f), baseY - height * 0.5f,
                    U(0.012f), height), new Color(_dim.r, _dim.g, _dim.b, 0.8f));
            }

            Label(new Rect(body.x, body.yMax - U(0.09f), body.width, U(0.05f)),
                "HAUT-PARLEUR", Font(0.022f), FontStyle.Bold, TextAnchor.MiddleCenter, _dim);
        }

        private void DrawLink(Rect body)
        {
            Label(new Rect(body.x, body.y, body.width, U(0.04f)), "MESSAGES",
                Font(0.024f), FontStyle.Bold, TextAnchor.MiddleLeft, _dim);

            Label(new Rect(body.x, body.y + U(0.045f), body.width, U(0.045f)),
                _briefing != null ? _briefing.FriendName : "SAMI",
                Font(0.034f), FontStyle.Bold, TextAnchor.MiddleLeft, _ink);

            Rect bubble = new Rect(body.x, body.y + U(0.12f), body.width * 0.92f, U(0.20f));
            GuiKit.Fill(bubble, new Color(0.14f, 0.16f, 0.22f));

            Label(new Rect(bubble.x + U(0.02f), bubble.y + U(0.02f), bubble.width - U(0.04f), U(0.16f)),
                "tiens. tu m'as jamais demande ca.\n\nuberbagarre.apk", Font(0.024f),
                FontStyle.Normal, TextAnchor.UpperLeft, _ink, true);

            // L'avertissement est le coeur de la scene : l'application est illegale, et le jeu
            // doit le dire avant que le joueur ne l'installe, pas apres.
            Rect warning = new Rect(body.x, body.y + U(0.36f), body.width, U(0.155f));
            GuiKit.Fill(warning, new Color(_warn.r * 0.25f, _warn.g * 0.18f, 0f, 0.55f));
            GuiKit.Outline(warning, Mathf.Max(1f, U(0.003f)), _warn);

            Label(new Rect(warning.x + U(0.018f), warning.y + U(0.012f), warning.width - U(0.036f), U(0.03f)),
                "SOURCE INCONNUE", Font(0.024f), FontStyle.Bold, TextAnchor.UpperLeft, _warn);

            Label(new Rect(warning.x + U(0.018f), warning.y + U(0.05f), warning.width - U(0.036f), U(0.10f)),
                "Application non referencee.\nDiffusion interdite sur le territoire.",
                Font(0.021f), FontStyle.Normal, TextAnchor.UpperLeft, _dim, true);

            float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.4f));

            Rect button = new Rect(body.x, body.yMax - U(0.115f), body.width, U(0.075f));
            GuiKit.Fill(button, new Color(_brand.r, _brand.g, _brand.b, 0.25f + 0.35f * pulse));
            GuiKit.Outline(button, Mathf.Max(1f, U(0.003f)), _brand);

            Label(button, "E — INSTALLER QUAND MEME", Font(0.025f), FontStyle.Bold,
                TextAnchor.MiddleCenter, _ink);
        }

        private void DrawInstall(Rect body)
        {
            Logo(new Rect(body.center.x - U(0.11f), body.y + U(0.14f), U(0.22f), U(0.22f)), 1f);

            float progress = _device.DownloadProgress;

            Label(new Rect(body.x, body.y + U(0.42f), body.width, U(0.05f)),
                progress < 0.999f ? "INSTALLATION" : "INSTALLEE",
                Font(0.030f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);

            Rect bar = new Rect(body.x + U(0.03f), body.y + U(0.50f), body.width - U(0.06f), U(0.018f));
            GuiKit.Fill(bar, new Color(1f, 1f, 1f, 0.12f));
            GuiKit.Fill(new Rect(bar.x, bar.y, bar.width * progress, bar.height), _brand);

            Label(new Rect(body.x, body.y + U(0.53f), body.width, U(0.045f)),
                Mathf.RoundToInt(progress * 100f) + " %", Font(0.026f), FontStyle.Normal,
                TextAnchor.MiddleCenter, _dim);

            Label(new Rect(body.x + U(0.02f), body.yMax - U(0.13f), body.width - U(0.04f), U(0.11f)),
                "En poursuivant, vous renoncez a tout recours.\nAucune donnee n'est conservee.\nAucune n'est protegee.",
                Font(0.020f), FontStyle.Italic, TextAnchor.UpperCenter,
                new Color(1f, 1f, 1f, 0.32f), true);
        }

        private void DrawContract(Rect body)
        {
            // Le logo en fond, tres attenue : l'interface epuree avec un logo en fond decrite
            // dans le dossier. Il occupe l'espace sans se disputer la lecture avec le contrat.
            Logo(new Rect(body.center.x - U(0.20f), body.center.y - U(0.24f), U(0.40f), U(0.40f)), 0.10f);

            Label(new Rect(body.x, body.y, body.width, U(0.04f)), "UBER BAGARRE",
                Font(0.026f), FontStyle.Bold, TextAnchor.MiddleLeft, _brand);

            Label(new Rect(body.x, body.y + U(0.045f), body.width, U(0.05f)), "BONSOIR.",
                Font(0.040f), FontStyle.Bold, TextAnchor.MiddleLeft, _ink);

            Label(new Rect(body.x, body.y + U(0.10f), body.width, U(0.04f)),
                ReputationLine(), Font(0.021f), FontStyle.Normal, TextAnchor.MiddleLeft, _dim);

            // La carte de contrat. Elle est seule a l'ecran, et c'est voulu : une liste de
            // missions au premier lancement donnerait un choix que le joueur ne peut pas
            // encore faire.
            Rect card = new Rect(body.x, body.y + U(0.17f), body.width, U(0.37f));
            GuiKit.Fill(card, new Color(1f, 1f, 1f, 0.08f));
            GuiKit.Fill(new Rect(card.x, card.y, card.width, U(0.006f)), _brand);

            Label(new Rect(card.x + U(0.025f), card.y + U(0.025f), card.width - U(0.05f), U(0.04f)),
                "RDV BASTON", Font(0.034f), FontStyle.Bold, TextAnchor.MiddleLeft, _ink);

            Stars(new Rect(card.x + U(0.025f), card.y + U(0.078f), card.width, U(0.03f)),
                _briefing != null ? _briefing.Stars : 1);

            Label(new Rect(card.x + U(0.025f), card.y + U(0.125f), card.width - U(0.05f), U(0.10f)),
                "CE SOIR — " + (_briefing != null ? _briefing.MeetingTime : "02:30") + "\n" +
                (_briefing != null ? _briefing.TargetLocation : "Devant le club"),
                Font(0.023f), FontStyle.Normal, TextAnchor.UpperLeft, _dim, true);

            Label(new Rect(card.x + U(0.025f), card.y + U(0.245f), card.width - U(0.05f), U(0.05f)),
                (_briefing != null ? _briefing.Reward : 150) + " EUR",
                Font(0.040f), FontStyle.Bold, TextAnchor.MiddleLeft, _good);

            float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f));

            Rect button = new Rect(body.x, body.yMax - U(0.115f), body.width, U(0.075f));
            GuiKit.Fill(button, new Color(_brand.r, _brand.g, _brand.b, 0.3f + 0.4f * pulse));

            Label(button, "E / ENTREE — ACCEPTER", Font(0.026f), FontStyle.Bold,
                TextAnchor.MiddleCenter, _ink);
        }

        private void DrawTarget(Rect body)
        {
            Label(new Rect(body.x, body.y, body.width, U(0.04f)), "FICHE DU SUJET",
                Font(0.024f), FontStyle.Bold, TextAnchor.MiddleLeft, _bad);

            // Le portrait est un aplat barre : afficher une photo fausse serait pire que de ne
            // pas en afficher. Le SIGNALEMENT en dessous est la vraie information.
            Rect portrait = new Rect(body.x, body.y + U(0.055f), U(0.22f), U(0.26f));
            GuiKit.Fill(portrait, new Color(0.16f, 0.16f, 0.20f));
            GuiKit.Outline(portrait, Mathf.Max(1f, U(0.003f)), new Color(1f, 1f, 1f, 0.2f));

            Label(portrait, "PAS DE\nPHOTO", Font(0.020f), FontStyle.Normal, TextAnchor.MiddleCenter,
                new Color(1f, 1f, 1f, 0.3f), true);

            float x = portrait.xMax + U(0.025f);
            float w = body.xMax - x;

            Label(new Rect(x, body.y + U(0.06f), w, U(0.045f)),
                _briefing != null ? _briefing.TargetName : "CIBLE",
                Font(0.032f), FontStyle.Bold, TextAnchor.UpperLeft, _ink, true);

            Label(new Rect(x, body.y + U(0.135f), w, U(0.035f)),
                _briefing != null ? _briefing.TargetAge : "",
                Font(0.023f), FontStyle.Normal, TextAnchor.UpperLeft, _dim);

            Label(new Rect(x, body.y + U(0.175f), w, U(0.12f)),
                _briefing != null ? _briefing.TargetRecord : "",
                Font(0.020f), FontStyle.Normal, TextAnchor.UpperLeft, _dim, true);

            Field(new Rect(body.x, body.y + U(0.35f), body.width, U(0.115f)), "SIGNALEMENT",
                _briefing != null ? _briefing.TargetClothing : "", _warn);

            Field(new Rect(body.x, body.y + U(0.48f), body.width, U(0.115f)), "LOCALISATION APPROX.",
                _briefing != null ? _briefing.TargetLocation : "", _dim);

            Rect note = new Rect(body.x, body.y + U(0.615f), body.width, U(0.085f));
            GuiKit.Fill(note, new Color(_bad.r * 0.3f, 0f, 0f, 0.4f));

            Label(new Rect(note.x + U(0.018f), note.y + U(0.012f), note.width - U(0.036f), U(0.065f)),
                "Le client exige un K.O.\nPhoto obligatoire.", Font(0.021f), FontStyle.Bold,
                TextAnchor.UpperLeft, _bad, true);
        }

        private void DrawMission(Rect body)
        {
            Label(new Rect(body.x, body.y, body.width, U(0.04f)), "COURSE EN COURS",
                Font(0.024f), FontStyle.Bold, TextAnchor.MiddleLeft, _brand);

            Rect order = new Rect(body.x, body.y + U(0.06f), body.width, U(0.22f));
            GuiKit.Fill(order, new Color(1f, 1f, 1f, 0.07f));
            GuiKit.Fill(new Rect(order.x, order.y, U(0.005f), order.height), _brand);

            Label(new Rect(order.x + U(0.025f), order.y + U(0.02f), order.width - U(0.05f), U(0.18f)),
                "TROUVER\n" + (_briefing != null ? _briefing.TargetName : "LA CIBLE") + "\nET LE METTRE K.O.",
                Font(0.030f), FontStyle.Bold, TextAnchor.UpperLeft, _ink, true);

            Field(new Rect(body.x, body.y + U(0.30f), body.width, U(0.115f)), "SIGNALEMENT",
                _briefing != null ? _briefing.TargetClothing : "", _warn);

            Field(new Rect(body.x, body.y + U(0.43f), body.width, U(0.115f)), "CLIENT",
                _briefing != null ? _briefing.ClientName : "", _dim);

            Stars(new Rect(body.x, body.y + U(0.57f), body.width, U(0.035f)),
                _briefing != null ? _briefing.Stars : 1);

            Label(new Rect(body.x, body.yMax - U(0.11f), body.width, U(0.06f)),
                (_briefing != null ? _briefing.Reward : 150) + " EUR a la validation",
                Font(0.026f), FontStyle.Bold, TextAnchor.MiddleCenter, _good);
        }

        private void DrawCamera(Rect body)
        {
            // Le viseur est volontairement vide : ce qu'on cadre, c'est le monde derriere
            // l'ecran. Remplir la dalle d'une image rendue coûterait une seconde camera pour
            // une capture d'une seconde.
            GuiKit.Fill(body, new Color(0.05f, 0.06f, 0.08f, 0.55f));

            float corner = U(0.05f);
            float thickness = Mathf.Max(1.5f, U(0.005f));
            Color frame = new Color(1f, 1f, 1f, 0.75f);

            Rect view = new Rect(body.x + U(0.03f), body.y + U(0.14f),
                body.width - U(0.06f), body.height * 0.52f);

            GuiKit.Fill(new Rect(view.x, view.y, corner, thickness), frame);
            GuiKit.Fill(new Rect(view.x, view.y, thickness, corner), frame);
            GuiKit.Fill(new Rect(view.xMax - corner, view.y, corner, thickness), frame);
            GuiKit.Fill(new Rect(view.xMax - thickness, view.y, thickness, corner), frame);
            GuiKit.Fill(new Rect(view.x, view.yMax - thickness, corner, thickness), frame);
            GuiKit.Fill(new Rect(view.x, view.yMax - corner, thickness, corner), frame);
            GuiKit.Fill(new Rect(view.xMax - corner, view.yMax - thickness, corner, thickness), frame);
            GuiKit.Fill(new Rect(view.xMax - thickness, view.yMax - corner, thickness, corner), frame);

            bool blink = Mathf.Repeat(Time.unscaledTime, 1f) < 0.55f;

            Label(new Rect(body.x, body.y + U(0.04f), body.width, U(0.05f)),
                blink ? "REC" : " ", Font(0.028f), FontStyle.Bold, TextAnchor.MiddleCenter, _bad);

            Label(new Rect(body.x, view.yMax + U(0.03f), body.width, U(0.09f)),
                string.IsNullOrEmpty(PhotoCounter) ? "CADRE LA CIBLE AU SOL" : "CADRE LA CIBLE AU SOL\n" + PhotoCounter,
                Font(0.026f), FontStyle.Bold, TextAnchor.UpperCenter, _ink, true);

            float size = U(0.11f);
            Rect shutter = new Rect(body.center.x - size * 0.5f, body.yMax - U(0.15f), size, size);
            GuiKit.Disc(shutter, new Color(1f, 1f, 1f, 0.9f));
            GuiKit.Disc(new Rect(shutter.x + U(0.012f), shutter.y + U(0.012f),
                shutter.width - U(0.024f), shutter.height - U(0.024f)), _background);

            Label(new Rect(body.x, body.yMax - U(0.035f), body.width, U(0.035f)),
                "CLIC GAUCHE", Font(0.021f), FontStyle.Bold, TextAnchor.MiddleCenter, _dim);
        }

        private void DrawValidated(Rect body)
        {
            float age = _device.ScreenAge;
            float pop = Mathf.Clamp01(age * 3f);

            Rect check = new Rect(body.center.x - U(0.09f) * pop, body.y + U(0.10f),
                U(0.18f) * pop, U(0.18f) * pop);

            GuiKit.Disc(check, _good);
            Label(check, "OK", Font(0.055f * pop), FontStyle.Bold, TextAnchor.MiddleCenter, Color.black);

            Label(new Rect(body.x, body.y + U(0.31f), body.width, U(0.05f)), "COURSE VALIDEE",
                Font(0.034f), FontStyle.Bold, TextAnchor.MiddleCenter, _ink);

            Label(new Rect(body.x, body.y + U(0.37f), body.width, U(0.04f)), "Photo envoyee au client",
                Font(0.021f), FontStyle.Normal, TextAnchor.MiddleCenter, _dim);

            Row(new Rect(body.x, body.y + U(0.45f), body.width, U(0.055f)), "PAIEMENT",
                "+" + (_briefing != null ? _briefing.Reward : 150) + " EUR", _good);

            Row(new Rect(body.x, body.y + U(0.515f), body.width, U(0.055f)), "EXPERIENCE",
                "+" + (_briefing != null ? _briefing.Experience : 120) + " XP", _brand);

            // L'avis du client : c'est la premiere ligne du systeme de reputation, et c'est
            // aussi la premiere fois que le jeu dit au joueur ce qu'il est devenu.
            Rect review = new Rect(body.x, body.y + U(0.60f), body.width, U(0.16f));
            GuiKit.Fill(review, new Color(1f, 1f, 1f, 0.07f));

            Stars(new Rect(review.x + U(0.02f), review.y + U(0.015f), review.width, U(0.03f)),
                _briefing != null ? _briefing.ReviewStars : 5);

            Label(new Rect(review.x + U(0.02f), review.y + U(0.055f), review.width - U(0.04f), U(0.10f)),
                "\"" + (_briefing != null ? _briefing.Review : "") + "\"",
                Font(0.021f), FontStyle.Italic, TextAnchor.UpperLeft, _dim, true);
        }

        /// <summary>
        /// Le profil : la « page notée avec des avis » du dossier.
        ///
        /// L'ordre de lecture est voulu : d'abord le niveau et la barre d'expérience (ce qui va
        /// changer le jeu), puis la réputation (ce que les clients pensent), puis le dernier avis
        /// en toutes lettres. Une note seule ne raconte rien ; une phrase de client, si.
        /// </summary>
        private void DrawProfile(Rect body)
        {
            Label(new Rect(body.x, body.y, body.width, U(0.04f)), "PROFIL",
                Font(0.024f), FontStyle.Bold, TextAnchor.MiddleLeft, _warn);

            int level = _progress != null ? _progress.Level : 1;

            Label(new Rect(body.x, body.y + U(0.05f), body.width, U(0.07f)), "NIVEAU " + level,
                Font(0.056f), FontStyle.Bold, TextAnchor.MiddleLeft, _ink);

            float progress = _progress != null ? _progress.LevelProgress : 0f;
            Rect bar = new Rect(body.x, body.y + U(0.135f), body.width, U(0.014f));
            GuiKit.Fill(bar, new Color(1f, 1f, 1f, 0.12f));
            GuiKit.Fill(new Rect(bar.x, bar.y, bar.width * progress, bar.height), _brand);

            Label(new Rect(body.x, body.y + U(0.155f), body.width, U(0.035f)),
                _progress != null ? _progress.Experience + " / " + _progress.NextThreshold + " XP" : "0 XP",
                Font(0.020f), FontStyle.Normal, TextAnchor.MiddleLeft, _dim);

            Row(new Rect(body.x, body.y + U(0.20f), body.width, U(0.05f)), "SOLDE",
                (_progress != null ? _progress.Money : 0) + " EUR", _good);

            Row(new Rect(body.x, body.y + U(0.25f), body.width, U(0.05f)), "COURSES",
                (_progress != null ? _progress.Contracts : 0).ToString(), _ink);

            float reputation = _progress != null ? _progress.Reputation : 0f;

            Label(new Rect(body.x, body.y + U(0.315f), body.width, U(0.035f)),
                reputation > 0f ? "REPUTATION  " + reputation.ToString("0.0") + " / 5" : "REPUTATION  —",
                Font(0.021f), FontStyle.Bold, TextAnchor.MiddleLeft, _warn);

            Stars(new Rect(body.x, body.y + U(0.355f), body.width, U(0.03f)), Mathf.RoundToInt(reputation));

            // L'annonce de capacité : elle occupe la place d'honneur tant qu'elle est neuve.
            if (!string.IsNullOrEmpty(UnlockedAbility))
            {
                float pulse = 0.55f + 0.45f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3f));
                Rect unlock = new Rect(body.x, body.y + U(0.405f), body.width, U(0.10f));

                GuiKit.Fill(unlock, new Color(_brand.r, _brand.g, _brand.b, 0.18f + 0.2f * pulse));
                GuiKit.Outline(unlock, Mathf.Max(1f, U(0.003f)), _brand);

                Label(new Rect(unlock.x + U(0.02f), unlock.y + U(0.01f), unlock.width - U(0.04f), U(0.03f)),
                    "NOUVELLE CAPACITE", Font(0.019f), FontStyle.Bold, TextAnchor.UpperLeft, _brand);

                Label(new Rect(unlock.x + U(0.02f), unlock.y + U(0.045f), unlock.width - U(0.04f), U(0.05f)),
                    UnlockedAbility, Font(0.028f), FontStyle.Bold, TextAnchor.UpperLeft, _ink, true);
            }

            if (_progress == null || _progress.Reviews.Count == 0) return;

            PlayerProgress.Review last = _progress.Reviews[0];
            Rect review = new Rect(body.x, body.y + U(0.525f), body.width, U(0.19f));
            GuiKit.Fill(review, new Color(1f, 1f, 1f, 0.07f));

            Stars(new Rect(review.x + U(0.02f), review.y + U(0.015f), review.width, U(0.03f)), last.Stars);

            Label(new Rect(review.x + U(0.02f), review.y + U(0.055f), review.width - U(0.04f), U(0.10f)),
                "\"" + last.Text + "\"", Font(0.021f), FontStyle.Italic, TextAnchor.UpperLeft, _dim, true);

            Label(new Rect(review.x + U(0.02f), review.yMax - U(0.035f), review.width - U(0.04f), U(0.03f)),
                "— " + last.Client, Font(0.018f), FontStyle.Normal, TextAnchor.MiddleRight,
                new Color(1f, 1f, 1f, 0.4f));
        }

        private string ReputationLine()
        {
            if (_progress == null || _progress.Contracts == 0) return "Reputation : aucune — 0 course";

            return "Reputation : " + _progress.Reputation.ToString("0.0") + " — " + _progress.Contracts +
                   (_progress.Contracts > 1 ? " courses" : " course");
        }

        // ------------------------------------------------------------------ briques

        private void Field(Rect rect, string label, string value, Color accent)
        {
            GuiKit.Fill(rect, new Color(1f, 1f, 1f, 0.055f));
            GuiKit.Fill(new Rect(rect.x, rect.y, U(0.004f), rect.height), accent);

            Label(new Rect(rect.x + U(0.02f), rect.y + U(0.01f), rect.width - U(0.04f), U(0.028f)),
                label, Font(0.019f), FontStyle.Bold, TextAnchor.UpperLeft, accent);

            Label(new Rect(rect.x + U(0.02f), rect.y + U(0.042f), rect.width - U(0.04f), U(0.065f)),
                value, Font(0.024f), FontStyle.Bold, TextAnchor.UpperLeft, _ink, true);
        }

        private void Row(Rect rect, string label, string value, Color accent)
        {
            Label(new Rect(rect.x, rect.y, rect.width * 0.55f, rect.height), label,
                Font(0.022f), FontStyle.Normal, TextAnchor.MiddleLeft, _dim);

            Label(new Rect(rect.x + rect.width * 0.45f, rect.y, rect.width * 0.55f, rect.height),
                value, Font(0.028f), FontStyle.Bold, TextAnchor.MiddleRight, accent);
        }

        private void Stars(Rect rect, int filled)
        {
            float size = U(0.026f);

            for (int i = 0; i < 5; i++)
            {
                Rect star = new Rect(rect.x + i * (size + U(0.008f)), rect.y, size, size);

                // Un losange plutot qu'une etoile : sans police d'icones, une etoile dessinee
                // a coups de rectangles ressemble a un accident. Un losange plein est net.
                GuiKit.Fill(star, i < filled ? _warn : new Color(1f, 1f, 1f, 0.16f));
            }
        }

        private void Logo(Rect rect, float alpha)
        {
            GuiKit.Disc(rect, new Color(_brand.r, _brand.g, _brand.b, alpha));

            float inner = rect.width * 0.62f;
            Rect hole = new Rect(rect.center.x - inner * 0.5f, rect.center.y - inner * 0.5f, inner, inner);
            GuiKit.Disc(hole, new Color(_background.r, _background.g, _background.b, alpha));

            Label(rect, "U", Mathf.Max(8, Mathf.RoundToInt(rect.height * 0.46f)), FontStyle.Bold,
                TextAnchor.MiddleCenter, new Color(_brand.r, _brand.g, _brand.b, alpha));
        }

        private void Label(Rect rect, string text, int fontSize, FontStyle style, TextAnchor anchor,
            Color color, bool wrap = false)
        {
            if (string.IsNullOrEmpty(text)) return;

            GUIStyle gui = GuiKit.Style(fontSize, style, anchor, wrap);

            GuiKit.OutlinedLabel(rect, text, gui, color, new Color(0f, 0f, 0f, 0.75f), 1f);
        }
    }
}
