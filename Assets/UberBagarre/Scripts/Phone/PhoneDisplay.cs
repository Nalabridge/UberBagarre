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
    /// Une décision technique mérite d'être expliquée, parce qu'elle a l'air d'un détail et
    /// qu'elle décide en réalité de la LISIBILITÉ : l'espace de mise en page n'est pas une
    /// résolution fixe, c'est la taille PROJETÉE en pixels. Avec une résolution de maquette
    /// fixe, la matrice d'affichage vaudrait par exemple 0,6 sur un écran donné, et toutes les
    /// polices seraient réduites d'autant — un texte écrit en corps 14 s'afficherait en 8
    /// pixels, illisible, et personne ne saurait pourquoi. En travaillant à l'échelle 1, un
    /// corps 14 fait 14 pixels quel que soit l'écran, et c'est la mise en page qui s'adapte.
    /// Toutes les dimensions sont donc exprimées en fraction de la hauteur de l'écran.
    ///
    /// La contrepartie assumée : une transformation affine représente exactement un
    /// parallélogramme, pas un trapèze. L'inclinaison de la pose levée reste donc faible, et
    /// l'interface garde une marge intérieure qui l'empêche de déborder du cadre.
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
        [SerializeField] private Color _ink = new Color(0.95f, 0.95f, 0.97f);
        [SerializeField] private Color _dim = new Color(0.62f, 0.64f, 0.70f);
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
        }

        private void OnGUI()
        {
            if (_device == null) return;

            // Seuil franc plutot qu'un fondu : GuiKit.Fill impose sa propre couleur a chaque
            // appel, donc un GUI.color global ne ferait pas fondre les aplats — seulement le
            // texte. Un ecran a moitie fondu ou seules les lettres s'attenuent serait pire
            // qu'un allumage net, qui se lit comme un ecran qui se reveille.
            if (_device.RaiseAmount < 0.45f) return;

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
            return Mathf.Max(8, Mathf.RoundToInt(_h * fraction));
        }

        private void DrawScreen(Rect screen)
        {
            GuiKit.Fill(screen, _background);

            float inset = U(_inset);
            Rect safe = new Rect(screen.x + inset, screen.y + inset,
                screen.width - inset * 2f, screen.height - inset * 2f);

            DrawStatusBar(safe);

            Rect body = new Rect(safe.x, safe.y + U(0.045f), safe.width, safe.height - U(0.085f));

            switch (_device.Current)
            {
                case PhoneDevice.Screen.AppelEntrant: DrawIncomingCall(body); break;
                case PhoneDevice.Screen.EnAppel: DrawOngoingCall(body); break;
                case PhoneDevice.Screen.Lien: DrawLink(body); break;
                case PhoneDevice.Screen.Installation: DrawInstall(body); break;
                case PhoneDevice.Screen.Accueil: DrawHome(body); break;
                case PhoneDevice.Screen.Cible: DrawTarget(body); break;
                case PhoneDevice.Screen.Mission: DrawMission(body); break;
                case PhoneDevice.Screen.Photo: DrawCamera(body); break;
                case PhoneDevice.Screen.Valide: DrawValidated(body); break;
                case PhoneDevice.Screen.Profil: DrawProfile(body); break;
                default: DrawLock(body); break;
            }

            // La barre de geste en bas : deux pixels de haut, et le rectangle cesse d'etre un
            // rectangle pour devenir un telephone.
            GuiKit.Fill(new Rect(screen.center.x - U(0.09f), screen.yMax - U(0.022f),
                U(0.18f), U(0.006f)), new Color(1f, 1f, 1f, 0.35f));
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

        // ------------------------------------------------------------------ écrans

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

        private void DrawHome(Rect body)
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
                "CE SOIR — 02:30\n" + (_briefing != null ? _briefing.TargetLocation : "Devant le club"),
                Font(0.023f), FontStyle.Normal, TextAnchor.UpperLeft, _dim, true);

            Label(new Rect(card.x + U(0.025f), card.y + U(0.245f), card.width - U(0.05f), U(0.05f)),
                (_briefing != null ? _briefing.Reward : 150) + " EUR",
                Font(0.040f), FontStyle.Bold, TextAnchor.MiddleLeft, _good);

            float pulse = 0.5f + 0.5f * Mathf.Abs(Mathf.Sin(Time.unscaledTime * 3.2f));

            Rect button = new Rect(body.x, body.yMax - U(0.115f), body.width, U(0.075f));
            GuiKit.Fill(button, new Color(_brand.r, _brand.g, _brand.b, 0.3f + 0.4f * pulse));

            Label(button, "E — ACCEPTER LA COURSE", Font(0.026f), FontStyle.Bold,
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

            GUIStyle gui = GuiKit.Style(fontSize, style, anchor);
            gui.wordWrap = wrap;

            GuiKit.OutlinedLabel(rect, text, gui, color, new Color(0f, 0f, 0f, 0.75f), 1f);
        }
    }
}
