using System;
using System.Collections.Generic;
using UberBagarre.Core;
using UberBagarre.Player;
using UberBagarre.View;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// L'écran des réglages, comme dans un jeu fini : des onglets en haut (Jeu, Interface,
    /// Graphismes, Image, Son, Commandes, Accessibilité), la liste des réglages à gauche, et à
    /// droite ce que fait le réglage choisi, sa valeur d'origine et, quand ça aide, un aperçu
    /// (l'interface, le réticule, les sous-titres).
    ///
    /// Tout s'applique tout de suite — derrière le menu, l'image change en direct — et tout est
    /// gardé d'une partie à l'autre. Deux exceptions qui se confirment, comme partout : le mode
    /// d'affichage et la résolution reviennent d'eux-mêmes en arrière au bout de quinze secondes
    /// si on ne dit pas « Garder » (un écran noir ne doit pas bloquer le joueur).
    ///
    /// Les touches se réassignent : Entrée sur une action, puis la nouvelle touche ; si elle
    /// servait déjà, les deux actions échangent leurs touches.
    ///
    /// Au clavier : ↑ ↓ pour choisir, ← → pour régler, Tab pour changer d'onglet (ou ↑ jusqu'aux
    /// onglets), Échap pour revenir. À la souris : survol, clic, glisser sur une barre, molette.
    /// </summary>
    public sealed class SettingsPage
    {
        public enum Sound
        {
            Tick,
            Confirm,
            Page
        }

        private enum Kind
        {
            Header,
            Choice,
            Slider,
            Key,
            Button
        }

        private sealed class Row
        {
            public Kind Kind;
            public string Label;
            public string Hint;

            public string[] Options;
            public Func<int> Index;
            public Action<int> Pick;
            public string Custom;

            public float Min;
            public float Max;
            public float Step;
            public Func<float> Get;
            public Action<float> Set;
            public Func<float, string> Format;

            public string Field;
            public Action Press;

            public Func<bool> Enabled;
            public Func<string> Note;
            public string Default;
            public string Preview;

            public bool IsEnabled
            {
                get { return Kind != Kind.Header && (Enabled == null || Enabled()); }
            }
        }

        private static readonly string[] Tabs = { "JEU", "INTERFACE", "GRAPHISMES", "IMAGE", "SON", "COMMANDES", "ACCESSIBILITÉ" };

        public const int TabGame = 0;
        public const int TabControls = 5;

        private static readonly int[] FrameLimits = { 0, 30, 60, 75, 90, 120, 144, 165, 240 };

        private readonly List<Row> _rows = new List<Row>(48);
        private readonly List<float> _rowTop = new List<float>(48);
        private readonly List<Vector2Int> _resolutions = new List<Vector2Int>(24);
        private readonly GUIContent _content = new GUIContent();

        private readonly GraphicsDirector _graphics;
        private readonly PlayerLook _look;
        private readonly PlayerInputReader _input;
        private readonly Action<Sound> _play;
        private readonly Action _back;

        private int _tab;
        private int _selected;
        private bool _onTabs;
        private float _time;
        private float _tabTime;
        private float _scroll;
        private float _scrollTarget;
        private bool _ensureVisible;
        private float _viewHeight = 1f;
        private float _contentHeight;
        private int _dragging = -1;
        private bool _graphicsDirty;

        private int _capturing = -1;
        private int _captureFrame;
        private int _captureEnded = -10;
        private string _notice;
        private float _noticeUntil;

        private float _resetArmedUntil = -1f;

        private Action _revert;
        private float _confirmUntil;
        private int _confirmChoice;

        private int _holdDirection;
        private float _repeatAt;

        public SettingsPage(GraphicsDirector graphics, PlayerLook look, PlayerInputReader input, Action<Sound> play, Action back)
        {
            _graphics = graphics;
            _look = look;
            _input = input;
            _play = play;
            _back = back;
        }

        /// <summary>Une touche est attendue : le menu ne doit rien faire d'autre.</summary>
        public bool Capturing
        {
            get { return _capturing >= 0; }
        }

        /// <summary>Les onglets où l'on regarde l'image : le voile du menu s'éclaircit à droite.</summary>
        public bool ShowsImage
        {
            get { return _tab == 2 || _tab == 3; }
        }

        public void Open(int tab)
        {
            _tab = Mathf.Clamp(tab, 0, Tabs.Length - 1);
            _time = 0f;
            _onTabs = false;
            _capturing = -1;
            _revert = null;
            Build();
        }

        /// <summary>En quittant : tout est écrit sur le disque. Un affichage pas encore confirmé est gardé.</summary>
        public void Close()
        {
            _capturing = -1;
            _revert = null;
            _dragging = -1;
            if (_graphicsDirty && _graphics != null) _graphics.Save();
            _graphicsDirty = false;
            GameSettings.Save();
        }

        // ================================================================== contenu

        private void Build()
        {
            _rows.Clear();

            switch (_tab)
            {
                case 0: BuildGame(); break;
                case 1: BuildInterface(); break;
                case 2: BuildGraphics(); break;
                case 3: BuildImage(); break;
                case 4: BuildSound(); break;
                case 5: BuildControls(); break;
                default: BuildAccessibility(); break;
            }

            Header(null);
            Row reset = Button("Rétablir les valeurs par défaut", "Remet tous les réglages de cet onglet comme à l'origine. Appuie deux fois : la première arme le bouton.", ResetTab);
            reset.Note = delegate { return _resetArmedUntil > _time ? "Appuie encore pour confirmer." : null; };

            _selected = Next(-1, 1);
            _scroll = 0f;
            _scrollTarget = 0f;
            _tabTime = 0f;
            _resetArmedUntil = -1f;
            _dragging = -1;
        }

        private void BuildGame()
        {
            Header("PARTIE");
            Choice("Difficulté",
                "Facile : les coups reçus font un tiers de dégâts en moins, et les tiens portent davantage. Difficile : chaque erreur se paie, les adversaires encaissent mieux. Se change à tout moment, même en pleine course.",
                new[] { "Facile", "Normale", "Difficile" },
                delegate { return GameSettings.Difficulty; }, delegate(int v) { GameSettings.Difficulty = v; }, "Normale");

            Header("CAMÉRA");
            Slider("Champ de vision",
                "L'angle de vue, en degrés (verticalement). Plus large, on voit davantage autour de soi — utile en combat contre plusieurs ; plus serré, l'image est plus proche de ce que voit l'œil.",
                55f, 95f, 1f, delegate { return GameSettings.FieldOfView; }, delegate(float v) { GameSettings.FieldOfView = v; },
                delegate(float v) { return Mathf.RoundToInt(v) + "°"; }, Mathf.RoundToInt(GameSettings.DefaultFieldOfView) + "°");
            Percent("Secousses de la caméra",
                "Les à-coups de la caméra quand un coup porte, une chute, un choc en voiture. À zéro, l'image reste parfaitement stable.",
                0f, 1f, delegate { return GameSettings.CameraShake; }, delegate(float v) { GameSettings.CameraShake = v; }, "100 %");
            Percent("Balancement en marchant",
                "Le mouvement de tête quand on marche ou qu'on court. À baisser si le mouvement incommode.",
                0f, 1f, delegate { return GameSettings.HeadBob; }, delegate(float v) { GameSettings.HeadBob = v; }, "100 %");
            Toggle("Profondeur de champ",
                "Le flou d'arrière-plan pendant les plans de caméra : au comptoir d'un magasin, dans le fauteuil du barbier.",
                delegate { return GameSettings.DepthOfField; }, delegate(bool v) { GameSettings.DepthOfField = v; }, true);

            Header("SOURIS");
            Slider("Sensibilité", "La vitesse de la visée à la souris.", 0.1f, 4f, 0.05f, Sensitivity, SetSensitivity,
                delegate(float v) { return v.ToString("0.00"); }, "1,00");
            Toggle("Inverser l'axe vertical", "Pousser la souris vers l'avant fait regarder vers le bas, comme un manche d'avion.",
                delegate { return GameSettings.InvertY; }, delegate(bool v) { GameSettings.InvertY = v; }, false);
            Percent("Lissage", "Adoucit les mouvements de la visée. À zéro, la visée suit la souris au plus près (conseillé en combat).",
                0f, 1f, delegate { return GameSettings.MouseSmoothing; }, delegate(float v) { GameSettings.MouseSmoothing = v; }, "0 %");
        }

        private void BuildInterface()
        {
            Header("AFFICHAGE TÊTE HAUTE");
            Row hud = Choice("Interface",
                "Complète : tout reste affiché. Discrète : la vie, l'argent et le niveau n'apparaissent que quand ils changent ou qu'ils comptent (combat, blessure). Masquée : rien que l'image ; la carte reste à portée de touche.",
                new[] { "Complète", "Discrète", "Masquée" },
                delegate { return GameSettings.Hud; }, delegate(int v) { GameSettings.Hud = v; }, "Discrète");
            hud.Preview = "hud";

            Row minimap = Toggle("Mini-carte", "La carte en bas à gauche, avec le GPS et les lieux proches.",
                delegate { return GameSettings.Minimap; }, delegate(bool v) { GameSettings.Minimap = v; }, true);
            minimap.Enabled = delegate { return GameSettings.Hud != 2; };
            minimap.Note = delegate { return GameSettings.Hud == 2 ? "L'interface est masquée." : null; };
            minimap.Preview = "hud";

            Row crosshair = Choice("Réticule", "Le repère au centre de l'écran.", new[] { "Point", "Croix", "Aucun" },
                delegate { return GameSettings.Crosshair; }, delegate(int v) { GameSettings.Crosshair = v; }, "Point");
            crosshair.Preview = "crosshair";

            Toggle("Zone visée", "En combat, la zone que vise ton coup (tête, corps, jambes) et l'état de la garde en face.",
                delegate { return GameSettings.AimedZone; }, delegate(bool v) { GameSettings.AimedZone = v; }, true);
            Toggle("Chiffres de dégâts", "Les dégâts de chaque coup, qui s'envolent de l'adversaire. Pratique pour comprendre le combat, moins pour l'immersion.",
                delegate { return GameSettings.DamageNumbers; }, delegate(bool v) { GameSettings.DamageNumbers = v; }, false);
            Toggle("Barres de vie des adversaires",
                "Une barre au-dessus de chaque adversaire. Sans elle, c'est leur corps qui dit leur état : ils titubent, saignent, baissent la garde.",
                delegate { return GameSettings.EnemyHealthBars; }, delegate(bool v) { GameSettings.EnemyHealthBars = v; }, false);
            Toggle("Rappels de touches", "Les touches rappelées à l'écran : sur la carte, au volant, au comptoir des magasins.",
                delegate { return GameSettings.KeyHints; }, delegate(bool v) { GameSettings.KeyHints = v; }, true);

            Header("DIVERS");
            Slider("Taille de l'interface", "Agrandit ou réduit les textes et les panneaux, partout (ce menu compris).",
                0.8f, 1.3f, 0.05f, delegate { return GameSettings.UiScale; }, delegate(float v) { GameSettings.UiScale = v; },
                delegate(float v) { return Mathf.RoundToInt(v * 100f) + " %"; }, "100 %");
            Toggle("Compteur d'images", "Le nombre d'images par seconde, en haut à gauche de l'écran.",
                delegate { return GameSettings.ShowFps; }, delegate(bool v) { GameSettings.ShowFps = v; }, false);
        }

        private void BuildGraphics()
        {
            GraphicsDirector g = _graphics;
            Func<string> editorOnly = delegate { return Application.isEditor ? "Sans effet dans l'éditeur : c'est la vue Game qui décide." : null; };

            Header("AFFICHAGE");
            Row display = Choice("Mode d'affichage",
                "Plein écran : le plus rapide. Sans bordure : tout l'écran, mais on passe à une autre fenêtre sans attendre. Fenêtré : une fenêtre classique.",
                new[] { "Plein écran", "Sans bordure", "Fenêtré" },
                delegate { return GameSettings.DisplayMode; }, PickDisplayMode, "Sans bordure");
            display.Note = editorOnly;

            Row resolution = Choice("Résolution", "La taille de l'image. Pour gagner des images par seconde sans flouter l'interface, préfère la résolution de rendu, plus bas.",
                ResolutionNames(), ResolutionIndex, PickResolution, "Celle de l'écran");
            resolution.Custom = Screen.width + " × " + Screen.height;
            resolution.Note = editorOnly;

            if (g != null)
            {
                Toggle("Synchro verticale", "L'image attend l'écran : plus de déchirure quand la vue tourne vite. Ajoute un très léger retard.",
                    delegate { return g.VSync; }, delegate(bool v) { g.VSync = v; _graphicsDirty = true; }, true);
            }

            Row limit = Choice("Limite d'images", "Le maximum d'images par seconde. Utile sur un portable : moins de chaleur, moins de bruit, plus de batterie.",
                new[] { "Illimitée", "30", "60", "75", "90", "120", "144", "165", "240" },
                delegate { return Mathf.Max(0, Array.IndexOf(FrameLimits, GameSettings.FrameLimit)); },
                delegate(int v) { GameSettings.FrameLimit = FrameLimits[v]; }, "Illimitée");
            limit.Enabled = delegate { return g == null || !g.VSync; };
            limit.Note = delegate { return g != null && g.VSync ? "La synchro verticale est active : c'est l'écran qui cadence." : null; };

            Header("QUALITÉ");
            Row quality = Choice("Qualité générale", "Règle d'un coup tout ce qui suit. Toucher l'un des réglages en dessous passe en « Personnalisée ».",
                new[] { "Basse", "Moyenne", "Haute", "Ultra" }, QualityIndex, ApplyQuality, "Haute");
            quality.Custom = "Personnalisée";

            Row scale = Percent("Résolution de rendu",
                "La ville est calculée à cette fraction de la résolution, puis agrandie ; l'interface reste nette. Le gain le plus franc quand le jeu rame.",
                0.5f, 1f, delegate { return GameSettings.RenderScale; }, delegate(float v) { GameSettings.RenderScale = v; }, "100 %");
            scale.Enabled = delegate { return UrpBridge.Active; };
            scale.Note = UrpOnly;

            if (g != null)
            {
                bool urp = UrpBridge.Active;
                Choice("Anticrénelage",
                    urp ? "Lisse les bords en escalier. FXAA : rapide. TAA : le plus stable en mouvement, un peu plus doux. SMAA + MSAA : le plus net, le plus cher."
                        : "Lisse les bords en escalier. FXAA : rapide. TAA : le plus stable en mouvement. MSAA : le plus net, mais cher avec beaucoup de lampes.",
                    new[] { "Aucun", "FXAA", "TAA", urp ? "SMAA + MSAA" : "MSAA ×8" },
                    delegate { return (int)g.AntiAliasing; },
                    delegate(int v) { g.AntiAliasing = (UberPostProcess.AntiAliasingMode)v; _graphicsDirty = true; }, "FXAA");
            }

            Choice("Ombres", "La finesse et la portée des ombres du soleil et des lampes.", new[] { "Basses", "Moyennes", "Hautes", "Ultra" },
                delegate { return GameSettings.ShadowQuality; }, delegate(int v) { GameSettings.ShadowQuality = v; }, "Hautes");
            Percent("Distance d'affichage", "Jusqu'où la ville est dessinée : bâtiments, arbres, voitures. Le réglage qui coûte le plus.",
                0.5f, 2f, delegate { return GameSettings.ViewDistance; }, delegate(float v) { GameSettings.ViewDistance = v; }, "100 %");
            Percent("Végétation", "La densité de l'herbe et des petites plantes.",
                0f, 1.5f, delegate { return GameSettings.Vegetation; }, delegate(float v) { GameSettings.Vegetation = v; }, "80 %");
            Choice("Textures", "La finesse des images posées sur les objets. À baisser si la carte graphique a peu de mémoire.",
                new[] { "Basses", "Moyennes", "Hautes" },
                delegate { return 2 - GameSettings.TextureQuality; }, delegate(int v) { GameSettings.TextureQuality = 2 - v; }, "Hautes");

            Row ao = Toggle("Occlusion ambiante", "Les ombres douces dans les coins, sous les objets, entre les bâtiments. Donne du relief ; coûte un peu.",
                delegate { return GameSettings.AmbientOcclusion; }, delegate(bool v) { GameSettings.AmbientOcclusion = v; }, true);
            ao.Enabled = delegate { return UrpBridge.Active; };
            ao.Note = UrpOnly;

            Row blur = Percent("Flou de mouvement", "Un léger flou quand la vue tourne vite ou en voiture.",
                0f, 1f, delegate { return GameSettings.MotionBlur; }, delegate(float v) { GameSettings.MotionBlur = v; }, "0 %");
            blur.Enabled = delegate { return UrpBridge.Active; };
            blur.Note = UrpOnly;

            if (g != null && !UrpBridge.Active)
            {
                Toggle("Reflets du sol", "La chaussée mouillée reflète la rue. Un second rendu de la scène : cher.",
                    delegate { return g.ReflectionsEnabled; }, delegate(bool v) { g.ReflectionsEnabled = v; _graphicsDirty = true; }, true);
                Percent("Lumière dans l'air", "Les faisceaux des lampes dans la bruine.",
                    0f, 2f, delegate { return g.Volumetric; }, delegate(float v) { g.Volumetric = v; _graphicsDirty = true; }, "55 %");
            }
        }

        private static string UrpOnly()
        {
            return UrpBridge.Active ? null : "Seulement avec le rendu URP.";
        }

        private void BuildImage()
        {
            GraphicsDirector g = _graphics;
            if (g == null)
            {
                Header("INDISPONIBLE DANS CETTE SCÈNE");
                return;
            }

            Header("AMBIANCE");
            Row look = Choice("Ambiance",
                "Ville : clair et franc, façon Schedule I. Sobre : des couleurs naturelles. Cinéma : plus de contraste et de halo. Bâtard : tout poussé trop loin, pour rire.",
                new[] { "Ville", "Sobre", "Cinéma", "Bâtard" }, LookIndex, PickLook, "Ville");
            look.Custom = "Retouchée";

            Header("RÉGLAGES FINS");
            Fine("Luminosité", "L'exposition de l'image. Règle-la pour que les ruelles de nuit restent sombres sans être noires.",
                0.5f, 2f, 0.05f, delegate { return g.Exposure; }, delegate(float v) { g.Exposure = v; });
            Fine("Contraste", "L'écart entre les ombres et les lumières.",
                0.8f, 1.4f, 0.01f, delegate { return g.Contrast; }, delegate(float v) { g.Contrast = v; });
            Fine("Saturation", "La vivacité des couleurs. À zéro, le noir et blanc.",
                0f, 1.6f, 0.02f, delegate { return g.Saturation; }, delegate(float v) { g.Saturation = v; });
            Fine("Halo lumineux", "Le halo autour des néons, des phares et du soleil.",
                0f, 3f, 0.05f, delegate { return g.Bloom; }, delegate(float v) { g.Bloom = v; });
            Fine("Vignette", "L'assombrissement des coins de l'image.",
                0f, 1f, 0.02f, delegate { return g.Vignette; }, delegate(float v) { g.Vignette = v; });
            Fine("Grain", "Un grain de pellicule, très fin.",
                0f, 0.1f, 0.005f, delegate { return g.Grain; }, delegate(float v) { g.Grain = v; }, 0.1f);
            Fine("Aberration chromatique", "Les bords de l'image légèrement irisés, comme à travers un objectif bon marché.",
                0f, 1.5f, 0.05f, delegate { return g.Aberration; }, delegate(float v) { g.Aberration = v; }, 1.5f);
        }

        private void BuildSound()
        {
            Header("VOLUMES");
            Percent("Volume général", "Tout le son du jeu.", 0f, 1f,
                delegate { return GameSettings.MasterVolume; }, delegate(float v) { GameSettings.MasterVolume = v; }, "100 %");
            Channel("Musique", "La musique des menus, du club, des autoradios.", AudioChannel.Music, "80 %");
            Channel("Effets", "Les coups, les pas, les portes, les moteurs, la police.", AudioChannel.Effects, "100 %");
            Channel("Voix", "Les répliques des personnages.", AudioChannel.Voices, "100 %");
            Channel("Ambiance", "La ville : la rumeur, le vent, la pluie, les oiseaux, la foule.", AudioChannel.Ambience, "90 %");
            Channel("Interface", "Les sons des menus, du téléphone, de la carte et des magasins.", AudioChannel.Interface, "80 %");

            Header("DIVERS");
            Toggle("Couper le son en arrière-plan", "Le jeu se tait quand tu passes à une autre fenêtre.",
                delegate { return GameSettings.MuteInBackground; }, delegate(bool v) { GameSettings.MuteInBackground = v; }, true);
        }

        private void BuildControls()
        {
            string group = null;
            for (int i = 0; i < KeyRemap.Actions.Length; i++)
            {
                KeyRemap.Action action = KeyRemap.Actions[i];
                if (action.Group != group)
                {
                    group = action.Group;
                    Header(group);
                }

                Row row = Add(Kind.Key, action.Label, action.Hint);
                row.Field = action.Field;
                row.Enabled = delegate { return _input != null && _input.Bindings != null; };
            }
        }

        private void BuildAccessibility()
        {
            Header("SOUS-TITRES");
            Func<bool> on = delegate { return GameSettings.Subtitles; };

            Row subtitles = Toggle("Sous-titres", "Les répliques écrites en bas de l'écran.",
                delegate { return GameSettings.Subtitles; }, delegate(bool v) { GameSettings.Subtitles = v; }, true);
            subtitles.Preview = "subtitles";

            Row size = Choice("Taille", "La taille du texte des sous-titres.", new[] { "Petits", "Normaux", "Grands", "Très grands" },
                delegate { return GameSettings.SubtitleSize; }, delegate(int v) { GameSettings.SubtitleSize = v; }, "Normaux");
            size.Enabled = on;
            size.Preview = "subtitles";

            Row background = Percent("Fond", "Un bandeau sombre derrière le texte, pour le lire sur n'importe quelle image.",
                0f, 1f, delegate { return GameSettings.SubtitleBackground; }, delegate(float v) { GameSettings.SubtitleBackground = v; }, "60 %");
            background.Enabled = on;
            background.Preview = "subtitles";

            Row speaker = Toggle("Nom de qui parle", "Le nom du personnage au-dessus de sa réplique.",
                delegate { return GameSettings.SubtitleSpeaker; }, delegate(bool v) { GameSettings.SubtitleSpeaker = v; }, true);
            speaker.Enabled = on;
            speaker.Preview = "subtitles";

            Header("VISION");
            Row colors = Choice("Mode daltonien",
                "Corrige les couleurs de l'image pour qu'elles restent distinctes : rouge et vert (protanopie, deutéranopie) ou bleu et jaune (tritanopie).",
                new[] { "Désactivé", "Protanopie", "Deutéranopie", "Tritanopie" },
                delegate { return GameSettings.ColorBlind; }, delegate(int v) { GameSettings.ColorBlind = v; }, "Désactivé");
            colors.Enabled = delegate { return UrpBridge.Active; };
            colors.Note = UrpOnly;

            Toggle("Réduire les flashs", "Atténue les éclairs à l'écran : parade, coups reçus, impacts, gyrophares.",
                delegate { return GameSettings.ReduceFlashes; }, delegate(bool v) { GameSettings.ReduceFlashes = v; }, false);
        }

        // ------------------------------------------------------------------ fabrique de lignes

        private Row Add(Kind kind, string label, string hint)
        {
            Row row = new Row { Kind = kind, Label = label, Hint = hint };
            _rows.Add(row);
            return row;
        }

        private void Header(string label)
        {
            Add(Kind.Header, label, null);
        }

        private Row Choice(string label, string hint, string[] options, Func<int> index, Action<int> pick, string fallback)
        {
            Row row = Add(Kind.Choice, label, hint);
            row.Options = options;
            row.Index = index;
            row.Pick = pick;
            row.Default = fallback;
            return row;
        }

        private Row Toggle(string label, string hint, Func<bool> get, Action<bool> set, bool fallback)
        {
            return Choice(label, hint, new[] { "Désactivé", "Activé" },
                delegate { return get() ? 1 : 0; }, delegate(int v) { set(v == 1); }, fallback ? "Activé" : "Désactivé");
        }

        private Row Slider(string label, string hint, float min, float max, float step, Func<float> get, Action<float> set,
            Func<float, string> format, string fallback)
        {
            Row row = Add(Kind.Slider, label, hint);
            row.Min = min;
            row.Max = max;
            row.Step = step;
            row.Get = get;
            row.Set = set;
            row.Format = format;
            row.Default = fallback;
            return row;
        }

        private Row Percent(string label, string hint, float min, float max, Func<float> get, Action<float> set, string fallback)
        {
            return Slider(label, hint, min, max, 0.05f, get, set, delegate(float v) { return Mathf.RoundToInt(v * 100f) + " %"; }, fallback);
        }

        private void Channel(string label, string hint, AudioChannel channel, string fallback)
        {
            Percent(label, hint, 0f, 1f, delegate { return GameSettings.ChannelVolume(channel); },
                delegate(float v) { GameSettings.SetChannelVolume(channel, v); }, fallback);
        }

        /// <summary>Un réglage fin de l'image : le toucher fait passer l'ambiance en « Retouchée ».</summary>
        private void Fine(string label, string hint, float min, float max, float step, Func<float> get, Action<float> set, float full = 1f)
        {
            Slider(label, hint, min, max, step, get, delegate(float v)
                {
                    set(v);
                    GameSettings.LookPreset = -1;
                    _graphicsDirty = true;
                },
                delegate(float v) { return Mathf.RoundToInt(v / full * 100f) + " %"; }, "Selon l'ambiance");
        }

        private Row Button(string label, string hint, Action press)
        {
            Row row = Add(Kind.Button, label, hint);
            row.Press = press;
            return row;
        }

        // ------------------------------------------------------------------ valeurs particulières

        private float Sensitivity()
        {
            return _look != null ? _look.UserSensitivity : PlayerPrefs.GetFloat(PlayerLook.UserSensitivityKey, 1f);
        }

        private void SetSensitivity(float value)
        {
            value = Mathf.Clamp(value, 0.1f, 4f);
            if (_look != null) _look.UserSensitivity = value;
            else PlayerPrefs.SetFloat(PlayerLook.UserSensitivityKey, value);
        }

        private void PickDisplayMode(int mode)
        {
            int previous = GameSettings.DisplayMode;
            if (previous == mode) return;
            GameSettings.DisplayMode = mode;
            AskToKeep(delegate { GameSettings.DisplayMode = previous; });
        }

        private string[] ResolutionNames()
        {
            _resolutions.Clear();
            Resolution[] all = Screen.resolutions;
            for (int i = 0; i < all.Length; i++)
            {
                Vector2Int size = new Vector2Int(all[i].width, all[i].height);
                if (size.x >= 800 && !_resolutions.Contains(size)) _resolutions.Add(size);
            }

            _resolutions.Sort(delegate(Vector2Int a, Vector2Int b) { return a.x != b.x ? a.x.CompareTo(b.x) : a.y.CompareTo(b.y); });
            if (_resolutions.Count == 0) _resolutions.Add(new Vector2Int(Screen.width, Screen.height));

            string[] names = new string[_resolutions.Count];
            for (int i = 0; i < names.Length; i++) names[i] = _resolutions[i].x + " × " + _resolutions[i].y;
            return names;
        }

        private int ResolutionIndex()
        {
            int index = _resolutions.IndexOf(new Vector2Int(Screen.width, Screen.height));
            return index >= 0 ? index : _resolutions.Count;
        }

        private void PickResolution(int index)
        {
            if (index < 0 || index >= _resolutions.Count) return;
            Vector2Int previous = new Vector2Int(Screen.width, Screen.height);
            Vector2Int size = _resolutions[index];
            if (size == previous) return;
            Screen.SetResolution(size.x, size.y, Screen.fullScreenMode);
            AskToKeep(delegate { Screen.SetResolution(previous.x, previous.y, Screen.fullScreenMode); });
        }

        // Les quatre niveaux de qualité : échelle de rendu, ombres, distance, végétation, textures, occlusion.
        private static readonly float[] QualityScale = { 0.75f, 0.9f, 1f, 1f };
        private static readonly int[] QualityShadows = { 0, 1, 2, 3 };
        private static readonly float[] QualityDistance = { 0.6f, 0.85f, 1f, 1.5f };
        private static readonly float[] QualityGrass = { 0.25f, 0.55f, 0.8f, 1.2f };
        private static readonly int[] QualityTextures = { 1, 0, 0, 0 };
        private static readonly bool[] QualityOcclusion = { false, false, true, true };

        private static int QualityIndex()
        {
            for (int i = 0; i < 4; i++)
            {
                if (Mathf.Abs(GameSettings.RenderScale - QualityScale[i]) < 0.01f &&
                    GameSettings.ShadowQuality == QualityShadows[i] &&
                    Mathf.Abs(GameSettings.ViewDistance - QualityDistance[i]) < 0.01f &&
                    Mathf.Abs(GameSettings.Vegetation - QualityGrass[i]) < 0.01f &&
                    GameSettings.TextureQuality == QualityTextures[i] &&
                    GameSettings.AmbientOcclusion == QualityOcclusion[i])
                {
                    return i;
                }
            }

            return 4;
        }

        private static void ApplyQuality(int level)
        {
            level = Mathf.Clamp(level, 0, 3);
            GameSettings.RenderScale = QualityScale[level];
            GameSettings.ShadowQuality = QualityShadows[level];
            GameSettings.ViewDistance = QualityDistance[level];
            GameSettings.Vegetation = QualityGrass[level];
            GameSettings.TextureQuality = QualityTextures[level];
            GameSettings.AmbientOcclusion = QualityOcclusion[level];
        }

        // Ordre du menu → préréglage du directeur graphique.
        private static readonly GraphicsDirector.Preset[] Looks =
        {
            GraphicsDirector.Preset.Ville, GraphicsDirector.Preset.Sobre, GraphicsDirector.Preset.Cinema, GraphicsDirector.Preset.Batard
        };

        private static int LookIndex()
        {
            int preset = GameSettings.LookPreset;
            if (preset < 0) return Looks.Length;
            for (int i = 0; i < Looks.Length; i++)
            {
                if ((int)Looks[i] == preset) return i;
            }

            return Looks.Length;
        }

        private void PickLook(int index)
        {
            if (_graphics == null) return;
            GraphicsDirector.Preset preset = Looks[Mathf.Clamp(index, 0, Looks.Length - 1)];
            _graphics.ApplyPreset(preset);
            GameSettings.LookPreset = (int)preset;
        }

        private void ResetTab()
        {
            if (_resetArmedUntil < _time)
            {
                _resetArmedUntil = _time + 3f;
                Play(Sound.Tick);
                return;
            }

            _resetArmedUntil = -1f;

            switch (_tab)
            {
                case 0:
                    GameSettings.Reset("difficulte", "fov", "secousses", "balancement", "profondeur", "inverser", "lissage");
                    PlayerPrefs.DeleteKey("UberBagarre.Menu.fov");
                    GameSettings.FieldOfView = GameSettings.DefaultFieldOfView;
                    SetSensitivity(1f);
                    break;

                case 1:
                    GameSettings.Reset("hud", "minicarte", "reticule", "zone", "chiffres", "barres", "aides", "interface.taille", "ips");
                    PlayerPrefs.DeleteKey("UberBagarre.Menu.ips");
                    break;

                case 2:
                    GameSettings.Reset("ips.max", "rendu.echelle", "ombres", "distance", "vegetation", "textures", "ao", "flou");
                    if (_graphics != null)
                    {
                        _graphics.VSync = true;
                        _graphics.AntiAliasing = UberPostProcess.AntiAliasingMode.Fxaa;
                        _graphics.ReflectionsEnabled = true;
                        _graphics.ReflectionDownsample = 2;
                        _graphicsDirty = true;
                    }

                    break;

                case 3:
                    PickLook(0);
                    break;

                case 4:
                    GameSettings.Reset("son.general", "son.musique", "son.effets", "son.voix", "son.ambiance", "son.interface", "son.arriereplan");
                    break;

                case 5:
                    if (_input != null) KeyRemap.ResetAll(_input.Bindings);
                    break;

                default:
                    GameSettings.Reset("soustitres", "soustitres.taille", "soustitres.fond", "soustitres.nom", "daltonisme", "eclairs");
                    break;
            }

            _notice = "Valeurs par défaut rétablies.";
            _noticeUntil = _time + 3f;
            Play(Sound.Confirm);
        }

        /// <summary>Un changement d'affichage à confirmer : sans réponse, il est annulé.</summary>
        private void AskToKeep(Action revert)
        {
            if (Application.isEditor) return;
            _revert = revert;
            _confirmUntil = _time + 15f;
            _confirmChoice = 0;
        }

        private void Revert()
        {
            Action revert = _revert;
            _revert = null;
            if (revert != null) revert();
            Play(Sound.Tick);
        }

        // ================================================================== clavier

        /// <summary>Les touches du menu, une fois par image.</summary>
        public void Tick(float dt, IInputProvider provider, InputBindings b)
        {
            _time += dt;
            _tabTime += dt;
            _scroll = Mathf.Lerp(_scroll, _scrollTarget, 1f - Mathf.Exp(-16f * dt));

            if (provider == null || b == null) return;

            if (Capturing)
            {
                PollCapture(provider);
                return;
            }

            if (_revert != null)
            {
                if (_time >= _confirmUntil)
                {
                    Revert();
                    return;
                }

                if (Pressed(provider, b.phoneLeft) || Pressed(provider, b.moveLeft) || Pressed(provider, b.phoneRight) || Pressed(provider, b.moveRight))
                {
                    _confirmChoice = 1 - _confirmChoice;
                    Play(Sound.Tick);
                }

                if (Pressed(provider, b.phoneSelect) || (_input != null && _input.InteractPressed))
                {
                    if (_confirmChoice == 0)
                    {
                        _revert = null;
                        Play(Sound.Confirm);
                    }
                    else
                    {
                        Revert();
                    }
                }
                else if (Pressed(provider, b.releaseCursor) || Pressed(provider, b.phoneBack))
                {
                    Revert();
                }

                return;
            }

            // La dernière image d'une capture : la touche choisie ne doit rien faire d'autre.
            if (Time.frameCount - _captureEnded < 2) return;

            bool up = Pressed(provider, b.phoneUp) || Pressed(provider, b.moveForward);
            bool down = Pressed(provider, b.phoneDown) || Pressed(provider, b.moveBackward);
            bool left = Pressed(provider, b.phoneLeft) || Pressed(provider, b.moveLeft);
            bool right = Pressed(provider, b.phoneRight) || Pressed(provider, b.moveRight);
            bool select = Pressed(provider, b.phoneSelect) || (_input != null && _input.InteractPressed) || Pressed(provider, b.jump);
            bool back = Pressed(provider, b.releaseCursor) || Pressed(provider, b.phoneBack);
            bool nextTab = provider.GetPressedThisFrame(InputBinding.FromKey(KeyCode.Tab));
            bool shift = provider.GetHeld(InputBinding.FromKey(KeyCode.LeftShift)) || provider.GetHeld(InputBinding.FromKey(KeyCode.RightShift));

            if (back)
            {
                _back();
                return;
            }

            if (nextTab)
            {
                SwitchTab(shift ? -1 : 1);
                return;
            }

            if (_onTabs)
            {
                if (left) SwitchTab(-1);
                if (right) SwitchTab(1);
                if (down || select)
                {
                    _onTabs = false;
                    _selected = Next(-1, 1);
                    _ensureVisible = true;
                    Play(Sound.Tick);
                }

                return;
            }

            if (up)
            {
                int previous = Next(_selected, -1);
                if (previous < 0)
                {
                    _onTabs = true;
                    _scrollTarget = 0f;
                }
                else
                {
                    _selected = previous;
                    _ensureVisible = true;
                }

                Play(Sound.Tick);
            }

            if (down)
            {
                int next = Next(_selected, 1);
                if (next >= 0)
                {
                    _selected = next;
                    _ensureVisible = true;
                    Play(Sound.Tick);
                }
            }

            Row row = _selected >= 0 && _selected < _rows.Count ? _rows[_selected] : null;
            if (row == null || !row.IsEnabled) return;

            // Gauche / droite : un cran ; maintenu sur une barre, la valeur défile.
            int direction = left ? -1 : right ? 1 : 0;
            if (direction != 0)
            {
                Adjust(row, direction);
                _holdDirection = direction;
                _repeatAt = _time + 0.38f;
            }
            else if (_holdDirection != 0)
            {
                bool held = _holdDirection < 0
                    ? provider.GetHeld(b.phoneLeft) || provider.GetHeld(b.moveLeft)
                    : provider.GetHeld(b.phoneRight) || provider.GetHeld(b.moveRight);
                if (!held) _holdDirection = 0;
                else if (row.Kind == Kind.Slider && _time >= _repeatAt)
                {
                    Adjust(row, _holdDirection);
                    _repeatAt = _time + 0.045f;
                }
            }

            if (select) Activate(row);
        }

        private static bool Pressed(IInputProvider provider, InputBinding binding)
        {
            return binding.IsAssigned && provider.GetPressedThisFrame(binding);
        }

        private void SwitchTab(int direction)
        {
            if (_graphicsDirty && _graphics != null)
            {
                _graphics.Save();
                _graphicsDirty = false;
            }

            _tab = (_tab + direction + Tabs.Length) % Tabs.Length;
            Build();
            Play(Sound.Page);
        }

        private void SelectTab(int tab)
        {
            if (tab == _tab) return;
            SwitchTab(tab - _tab);
        }

        /// <summary>La ligne réglable suivante dans un sens (les titres et les lignes grisées sont sautés), -1 au bout.</summary>
        private int Next(int from, int direction)
        {
            for (int i = from + direction; i >= 0 && i < _rows.Count; i += direction)
            {
                if (_rows[i].IsEnabled) return i;
            }

            return -1;
        }

        private void Adjust(Row row, int direction)
        {
            if (row.Kind == Kind.Choice)
            {
                int count = row.Options.Length;
                int index = row.Index();
                int next = index >= count || index < 0 ? (direction > 0 ? 0 : count - 1) : (index + direction + count) % count;
                row.Pick(next);
                Play(Sound.Tick);
            }
            else if (row.Kind == Kind.Slider)
            {
                float value = Mathf.Clamp(Snap(row, row.Get() + direction * row.Step), row.Min, row.Max);
                row.Set(value);
                Play(Sound.Tick);
            }
        }

        private static float Snap(Row row, float value)
        {
            return row.Step > 0f ? Mathf.Round(value / row.Step) * row.Step : value;
        }

        private void Activate(Row row)
        {
            switch (row.Kind)
            {
                case Kind.Button:
                    row.Press();
                    break;
                case Kind.Key:
                    BeginCapture();
                    break;
                case Kind.Choice:
                    Adjust(row, 1);
                    break;
            }
        }

        private void BeginCapture()
        {
            _capturing = _selected;
            _captureFrame = Time.frameCount;
            _notice = null;
            Play(Sound.Tick);
        }

        private void PollCapture(IInputProvider provider)
        {
            if (Time.frameCount <= _captureFrame) return;

            if (provider.GetPressedThisFrame(InputBinding.FromKey(KeyCode.Escape)))
            {
                _capturing = -1;
                _captureEnded = Time.frameCount;
                Play(Sound.Tick);
                return;
            }

            for (int m = 0; m < 5; m++)
            {
                if (provider.GetPressedThisFrame(InputBinding.FromMouse(m)))
                {
                    Commit(InputBinding.FromMouse(m));
                    return;
                }
            }

            KeyCode[] keys = KeyRemap.Capturable;
            for (int i = 0; i < keys.Length; i++)
            {
                if (keys[i] == KeyCode.Escape) continue;
                if (provider.GetPressedThisFrame(InputBinding.FromKey(keys[i])))
                {
                    Commit(InputBinding.FromKey(keys[i]));
                    return;
                }
            }
        }

        private void Commit(InputBinding binding)
        {
            Row row = _capturing >= 0 && _capturing < _rows.Count ? _rows[_capturing] : null;
            _capturing = -1;
            _captureEnded = Time.frameCount;
            if (row == null || _input == null || _input.Bindings == null) return;

            string swapped;
            KeyRemap.Assign(_input.Bindings, row.Field, binding, out swapped);
            _notice = swapped != null ? "Touche échangée avec « " + swapped + " »." : null;
            _noticeUntil = _time + 4f;
            Play(Sound.Confirm);
        }

        private void Play(Sound sound)
        {
            if (_play != null) _play(sound);
        }

        // ================================================================== dessin

        /// <summary>Dessine la page sur tout l'écran (le menu a déjà posé son fond).</summary>
        public void Draw(string origin)
        {
            float u = UiTheme.Unit;
            float sw = Screen.width;
            float sh = Screen.height;
            Event e = Event.current;
            bool mouseLocked = Capturing || _revert != null || Time.frameCount - _captureEnded < 2;

            // Le voile : sombre derrière la liste, plus léger à droite — beaucoup plus sur les
            // onglets d'image, où l'on regarde le résultat en direct.
            float right = ShowsImage ? 0.18f : 0.5f;
            const int bands = 24;
            for (int i = 0; i < bands; i++)
            {
                float t = i / (float)(bands - 1);
                float a = Mathf.Lerp(0.86f, right, Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(0.3f, 0.75f, t)));
                GuiKit.Fill(new Rect(sw * i / bands, 0f, sw / bands + 1f, sh), new Color(0.01f, 0.012f, 0.018f, a));
            }

            float margin = Mathf.Max(40f * u, sw * 0.055f);
            float appear = UiTheme.EaseOut(_time / 0.35f);
            float slide = (1f - appear) * 16f * u;

            // --- titre
            UiTheme.Label(new Rect(margin, 44f * u + slide, 600f * u, 20f * u), origin, UiTheme.Text(13f, GuiKit.Weight.Bold, TextAnchor.MiddleLeft),
                UiTheme.InkFaint);
            UiTheme.Label(new Rect(margin, 62f * u + slide, 800f * u, 54f * u), "RÉGLAGES", UiTheme.Title(42f), UiTheme.Ink);

            // --- onglets
            GUIStyle tabStyle = UiTheme.Text(16f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            float x = margin - 14f * u;
            float tabY = 134f * u + slide;
            for (int i = 0; i < Tabs.Length; i++)
            {
                _content.text = Tabs[i];
                float w = tabStyle.CalcSize(_content).x + 28f * u;
                Rect tab = new Rect(x, tabY, w, 40f * u);
                bool current = i == _tab;
                bool hover = !mouseLocked && tab.Contains(e.mousePosition);

                if (current && _onTabs) GuiKit.Rounded(tab, UiTheme.Raised, 8f * u);
                else if (hover) GuiKit.Rounded(tab, new Color(1f, 1f, 1f, 0.04f), 8f * u);

                UiTheme.Label(tab, Tabs[i], tabStyle, current ? UiTheme.Ink : hover ? UiTheme.InkDim : UiTheme.InkFaint);
                if (current)
                {
                    float grow = UiTheme.EaseOut(_tabTime / 0.25f);
                    float lw = (w - 28f * u) * grow;
                    GuiKit.Fill(new Rect(tab.center.x - lw * 0.5f, tab.yMax + 2f * u, lw, 3f * u), UiTheme.Accent);
                }

                if (hover && e.type == EventType.MouseDown && e.button == 0)
                {
                    SelectTab(i);
                    _onTabs = false;
                    e.Use();
                }

                x += w + 4f * u;
            }

            GuiKit.Fill(new Rect(margin, tabY + 50f * u, sw - margin * 2f, 1f), UiTheme.Line);

            // --- liste et panneau d'explication
            float top = tabY + 72f * u;
            float listWidth = Mathf.Min(880f * u, (sw - margin * 2f) * 0.56f);
            Rect list = new Rect(margin, top, listWidth, sh - top - 96f * u);
            float infoX = list.xMax + 48f * u;
            Rect info = new Rect(infoX, top, sw - margin - infoX, Mathf.Min(list.height, 600f * u));

            float previous = GuiKit.Alpha;
            GuiKit.Alpha = previous * UiTheme.EaseOut(_tabTime / 0.22f);
            DrawRows(list, u, e, mouseLocked);
            if (info.width > 220f * u) DrawInfo(info, u);
            GuiKit.Alpha = previous;

            DrawFooter(margin, sh - 60f * u, u);

            if (_revert != null) DrawConfirm(u, e);
            else if (Capturing && (e.type == EventType.MouseDown || e.type == EventType.MouseUp)) e.Use();
        }

        private void DrawRows(Rect list, float u, Event e, bool mouseLocked)
        {
            float rowHeight = 50f * u;
            float headerHeight = 44f * u;
            float gap = 4f * u;

            // Disposition
            _rowTop.Clear();
            float y = 0f;
            for (int i = 0; i < _rows.Count; i++)
            {
                bool header = _rows[i].Kind == Kind.Header;
                if (header && i == 0) y -= 10f * u;
                _rowTop.Add(y);
                y += (header ? headerHeight : rowHeight) + gap;
            }

            _contentHeight = y;
            _viewHeight = list.height;
            float maxScroll = Mathf.Max(0f, _contentHeight - _viewHeight);

            if (_ensureVisible && _selected >= 0 && _selected < _rows.Count)
            {
                _ensureVisible = false;
                float rowTop = _rowTop[_selected];
                // Le titre de section au-dessus de la ligne reste visible lui aussi.
                if (_selected > 0 && _rows[_selected - 1].Kind == Kind.Header) rowTop = _rowTop[_selected - 1];
                if (rowTop < _scrollTarget) _scrollTarget = rowTop;
                if (_rowTop[_selected] + rowHeight > _scrollTarget + _viewHeight) _scrollTarget = _rowTop[_selected] + rowHeight - _viewHeight;
            }

            if (!mouseLocked && e.type == EventType.ScrollWheel && list.Contains(e.mousePosition))
            {
                _scrollTarget += e.delta.y * 18f * u;
                e.Use();
            }

            _scrollTarget = Mathf.Clamp(_scrollTarget, 0f, maxScroll);
            _scroll = Mathf.Clamp(_scroll, 0f, maxScroll);

            GUI.BeginGroup(list);
            Vector2 mouse = e.mousePosition;
            bool mouseInside = mouse.x >= 0f && mouse.y >= 0f && mouse.x <= list.width && mouse.y <= list.height;

            GUIStyle label = UiTheme.Body(18f);
            GUIStyle headerStyle = UiTheme.Text(12.5f, GuiKit.Weight.Bold, TextAnchor.LowerLeft);
            GUIStyle value = UiTheme.Text(17f, GuiKit.Weight.Medium, TextAnchor.MiddleCenter);
            GUIStyle number = UiTheme.Text(16f, GuiKit.Weight.Medium, TextAnchor.MiddleRight);
            GUIStyle arrow = UiTheme.Text(24f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            float baseAlpha = GuiKit.Alpha;

            for (int i = 0; i < _rows.Count; i++)
            {
                Row row = _rows[i];
                bool header = row.Kind == Kind.Header;
                float height = header ? headerHeight : rowHeight;
                Rect rect = new Rect(0f, _rowTop[i] - _scroll, list.width - 14f * u, height);
                if (rect.yMax < 0f || rect.y > list.height) continue;

                // Les lignes arrivent en cascade quand on change d'onglet.
                float cascade = UiTheme.EaseOut((_tabTime - i * 0.012f) / 0.25f);
                rect.x += (1f - cascade) * 14f * u;

                if (header)
                {
                    if (string.IsNullOrEmpty(row.Label)) continue;
                    GuiKit.Alpha = baseAlpha * cascade;
                    UiTheme.Label(new Rect(rect.x + 2f * u, rect.y, rect.width, rect.height - 8f * u), row.Label, headerStyle, UiTheme.Accent);
                    continue;
                }

                bool enabled = row.IsEnabled;
                bool hover = enabled && !mouseLocked && mouseInside && rect.Contains(mouse);
                if (hover && (UiTheme.MouseMoved || e.type == EventType.MouseDown) && (_selected != i || _onTabs))
                {
                    _selected = i;
                    _onTabs = false;
                }

                bool selected = i == _selected && !_onTabs;
                GuiKit.Alpha = baseAlpha * cascade * (enabled ? 1f : 0.38f);

                if (selected)
                {
                    GuiKit.Rounded(rect, new Color(1f, 1f, 1f, 0.075f), 8f * u);
                    GuiKit.Rounded(new Rect(rect.x, rect.y + 10f * u, 3f * u, rect.height - 20f * u), UiTheme.Accent, 1.5f * u);
                }
                else if (hover)
                {
                    GuiKit.Rounded(rect, new Color(1f, 1f, 1f, 0.035f), 8f * u);
                }

                Color ink = selected ? UiTheme.Ink : UiTheme.InkDim;
                UiTheme.Label(new Rect(rect.x + 22f * u, rect.y, rect.width * 0.5f, rect.height), row.Label, label, ink);

                Rect area = new Rect(rect.x + rect.width * 0.52f, rect.y, rect.width * 0.48f - 14f * u, rect.height);

                switch (row.Kind)
                {
                    case Kind.Choice:
                        DrawChoice(row, area, selected, enabled && !mouseLocked, u, e, value, arrow);
                        break;
                    case Kind.Slider:
                        DrawSlider(row, i, area, selected, enabled && !mouseLocked, u, e, number);
                        break;
                    case Kind.Key:
                        DrawKey(row, i, area, selected, u);
                        if (hover && e.type == EventType.MouseDown && e.button == 0)
                        {
                            BeginCapture();
                            e.Use();
                        }

                        break;
                    case Kind.Button:
                        UiTheme.Label(new Rect(area.xMax - 30f * u, area.y, 30f * u, area.height), "›", arrow, selected ? UiTheme.Accent : UiTheme.InkFaint);
                        if (hover && e.type == EventType.MouseDown && e.button == 0)
                        {
                            row.Press();
                            e.Use();
                        }

                        break;
                }
            }

            GuiKit.Alpha = baseAlpha;
            GUI.EndGroup();

            if (e.type == EventType.MouseUp) _dragging = -1;

            // Une barre de défilement fine, quand la liste dépasse.
            if (maxScroll > 1f)
            {
                Rect track = new Rect(list.xMax - 4f * u, list.y, 3f * u, list.height);
                GuiKit.Rounded(track, new Color(1f, 1f, 1f, 0.06f), 1.5f * u);
                float thumb = Mathf.Max(30f * u, list.height * list.height / _contentHeight);
                float at = (list.height - thumb) * (_scroll / maxScroll);
                GuiKit.Rounded(new Rect(track.x, track.y + at, track.width, thumb), new Color(1f, 1f, 1f, 0.3f), 1.5f * u);
            }
        }

        private void DrawChoice(Row row, Rect area, bool selected, bool interactive, float u, Event e, GUIStyle value, GUIStyle arrow)
        {
            int count = row.Options.Length;
            int index = row.Index();
            bool custom = index < 0 || index >= count;
            string text = custom ? (row.Custom ?? "—") : row.Options[index];

            Rect leftArrow = new Rect(area.x, area.y, 34f * u, area.height);
            Rect rightArrow = new Rect(area.xMax - 34f * u, area.y, 34f * u, area.height);
            Color arrowColor = selected ? UiTheme.Accent : new Color(1f, 1f, 1f, 0.28f);
            UiTheme.Label(leftArrow, "‹", arrow, arrowColor);
            UiTheme.Label(rightArrow, "›", arrow, arrowColor);
            UiTheme.Label(new Rect(area.x + 34f * u, area.y - 3f * u, area.width - 68f * u, area.height), text, value,
                selected ? UiTheme.Ink : UiTheme.InkDim);

            // Les crans : un trait par valeur, celui de la valeur courante allumé.
            if (count > 1 && count <= 9)
            {
                float segment = Mathf.Min(24f * u, (area.width - 90f * u) / count - 4f * u);
                float total = count * (segment + 4f * u) - 4f * u;
                float sx = area.center.x - total * 0.5f;
                for (int i = 0; i < count; i++)
                {
                    Color c = i == index ? (selected ? UiTheme.Accent : UiTheme.InkDim) : new Color(1f, 1f, 1f, 0.14f);
                    GuiKit.Fill(new Rect(sx + i * (segment + 4f * u), area.yMax - 11f * u, segment, 2f * u), c);
                }
            }

            if (interactive && e.type == EventType.MouseDown && e.button == 0)
            {
                Rect whole = new Rect(area.x - area.width, area.y, area.width * 2f, area.height);
                if (leftArrow.Contains(e.mousePosition))
                {
                    Adjust(row, -1);
                    e.Use();
                }
                else if (rightArrow.Contains(e.mousePosition) || whole.Contains(e.mousePosition))
                {
                    Adjust(row, 1);
                    e.Use();
                }
            }
        }

        private void DrawSlider(Row row, int index, Rect area, bool selected, bool interactive, float u, Event e, GUIStyle number)
        {
            float t = Mathf.InverseLerp(row.Min, row.Max, row.Get());
            Rect track = new Rect(area.x + 8f * u, area.center.y - 2f * u, area.width - 84f * u, 4f * u);
            GuiKit.Rounded(track, new Color(1f, 1f, 1f, 0.12f), 2f * u);
            GuiKit.Rounded(new Rect(track.x, track.y, Mathf.Max(track.height, track.width * t), track.height),
                selected ? UiTheme.Accent : new Color(1f, 1f, 1f, 0.55f), 2f * u);
            float knob = (selected ? 16f : 12f) * u;
            GuiKit.Rounded(new Rect(track.x + track.width * t - knob * 0.5f, track.center.y - knob * 0.5f, knob, knob),
                selected ? UiTheme.Ink : new Color(0.8f, 0.82f, 0.86f), knob * 0.5f);

            UiTheme.Label(new Rect(area.xMax - 70f * u, area.y, 70f * u, area.height), row.Format(row.Get()), number,
                selected ? UiTheme.Ink : UiTheme.InkDim);

            if (!interactive) return;
            Rect grab = new Rect(track.x - 10f * u, area.y, track.width + 20f * u, area.height);
            if (e.type == EventType.MouseDown && e.button == 0 && grab.Contains(e.mousePosition)) _dragging = index;
            if (_dragging == index && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag))
            {
                float v = Mathf.Lerp(row.Min, row.Max, Mathf.Clamp01((e.mousePosition.x - track.x) / track.width));
                row.Set(Mathf.Clamp(Snap(row, v), row.Min, row.Max));
                e.Use();
            }
        }

        private void DrawKey(Row row, int index, Rect area, bool selected, float u)
        {
            if (_capturing == index)
            {
                float pulse = 0.55f + 0.45f * Mathf.Sin(_time * 7f);
                Rect box = new Rect(area.xMax - 230f * u, area.y + 9f * u, 230f * u, area.height - 18f * u);
                GuiKit.RoundedOutline(box, new Color(UiTheme.Accent.r, UiTheme.Accent.g, UiTheme.Accent.b, pulse), 6f * u, 1.5f * u);
                UiTheme.Label(box, "Appuie sur une touche…", UiTheme.Text(14f, GuiKit.Weight.Medium, TextAnchor.MiddleCenter), UiTheme.Accent);
                return;
            }

            InputBinding binding = _input != null ? KeyRemap.Get(_input.Bindings, row.Field) : default(InputBinding);
            float x = area.xMax;

            if (binding.source == InputSource.Key && binding.alternateKey != KeyCode.None && binding.alternateKey != binding.key)
            {
                x = KeyCap(x, UiTheme.KeyName(binding.alternateKey), area, selected, u) - 6f * u;
            }

            x = KeyCap(x, binding.IsAssigned ? UiTheme.KeyName(binding) : "—", area, selected, u);

            // Une touche changée par le joueur porte un point d'accent.
            if (_input != null && !KeyRemap.IsDefault(_input.Bindings, row.Field))
            {
                float d = 6f * u;
                GuiKit.Rounded(new Rect(x - 14f * u, area.center.y - d * 0.5f, d, d), UiTheme.Accent, d * 0.5f);
            }
        }

        /// <summary>Un cabochon de touche, aligné à droite sur <paramref name="right"/> ; rend son bord gauche.</summary>
        private float KeyCap(float right, string text, Rect area, bool selected, float u)
        {
            GUIStyle style = UiTheme.Text(13f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter);
            _content.text = text;
            float h = 28f * u;
            float w = Mathf.Max(h, style.CalcSize(_content).x + 18f * u);
            Rect cap = new Rect(right - w, area.center.y - h * 0.5f, w, h);
            GuiKit.Rounded(cap, selected ? new Color(1f, 1f, 1f, 0.92f) : new Color(1f, 1f, 1f, 0.12f), 5f * u);
            if (!selected) GuiKit.RoundedOutline(cap, new Color(1f, 1f, 1f, 0.16f), 5f * u, 1f);
            UiTheme.Label(cap, text, style, selected ? new Color(0.06f, 0.07f, 0.09f) : UiTheme.Ink);
            return cap.x;
        }

        private void DrawInfo(Rect info, float u)
        {
            Row row = !_onTabs && _selected >= 0 && _selected < _rows.Count ? _rows[_selected] : null;
            UiTheme.DrawPanel(info, 14f * u);

            float pad = 28f * u;
            float x = info.x + pad;
            float width = info.width - pad * 2f;
            float y = info.y + pad;

            if (row == null)
            {
                UiTheme.Label(new Rect(x, y, width, 34f * u), Tabs[_tab], UiTheme.Heading(24f), UiTheme.Ink);
                y += 46f * u;
                UiTheme.Label(new Rect(x, y, width, 200f * u), TabSummary(_tab), WrapStyle(16f), UiTheme.InkDim);
                return;
            }

            GUIStyle title = UiTheme.Heading(24f);
            UiTheme.Label(new Rect(x, y, width, 34f * u), row.Label, title, UiTheme.Ink);
            y += 48f * u;

            string hint = row.Hint;
            if (row.Kind == Kind.Key)
            {
                hint = (string.IsNullOrEmpty(hint) ? "" : hint + "\n\n") +
                       "Entrée ou clic : choisir une nouvelle touche. Échap : annuler. Si la touche sert déjà, les deux actions échangent leurs touches.";
            }

            if (!string.IsNullOrEmpty(hint))
            {
                GUIStyle body = WrapStyle(16.5f);
                _content.text = hint;
                float h = body.CalcHeight(_content, width);
                UiTheme.Label(new Rect(x, y, width, h), hint, body, UiTheme.InkDim);
                y += h + 18f * u;
            }

            string fallback = row.Kind == Kind.Key && _input != null ? DefaultKey(row.Field) : row.Default;
            if (!string.IsNullOrEmpty(fallback))
            {
                UiTheme.Label(new Rect(x, y, width, 20f * u), "Par défaut : " + fallback, UiTheme.Text(14f, GuiKit.Weight.Medium, TextAnchor.MiddleLeft),
                    UiTheme.InkFaint);
                y += 28f * u;
            }

            string note = row.Note != null ? row.Note() : null;
            if (_notice != null && _time < _noticeUntil) note = _notice;
            if (!string.IsNullOrEmpty(note))
            {
                GUIStyle accent = WrapStyle(15f);
                _content.text = note;
                float h = accent.CalcHeight(_content, width - 16f * u);
                GuiKit.Fill(new Rect(x, y + 2f * u, 2f * u, h - 4f * u), UiTheme.Accent);
                UiTheme.Label(new Rect(x + 12f * u, y, width - 12f * u, h), note, accent, UiTheme.Accent);
                y += h + 16f * u;
            }

            if (row.Preview == null) return;
            float previewHeight = Mathf.Min(width * 9f / 16f, info.yMax - pad - y);
            if (previewHeight < 90f * u) return;
            Rect preview = new Rect(x, info.yMax - pad - previewHeight, previewHeight * 16f / 9f, previewHeight);
            preview.x = x + (width - preview.width) * 0.5f;

            switch (row.Preview)
            {
                case "hud": PreviewHud(preview, u); break;
                case "crosshair": PreviewCrosshair(preview, u); break;
                case "subtitles": PreviewSubtitles(preview, u); break;
            }
        }

        private GUIStyle WrapStyle(float px)
        {
            return GuiKit.Text(UiTheme.Size(px), GuiKit.Weight.Regular, TextAnchor.UpperLeft, true);
        }

        private string DefaultKey(string field)
        {
            InputBinding binding = KeyRemap.Default(field);
            string text = UiTheme.KeyName(binding);
            if (binding.source == InputSource.Key && binding.alternateKey != KeyCode.None && binding.alternateKey != binding.key)
            {
                text += " ou " + UiTheme.KeyName(binding.alternateKey);
            }

            return text;
        }

        private static string TabSummary(int tab)
        {
            switch (tab)
            {
                case 0: return "La difficulté, la caméra et la souris.";
                case 1: return "Ce qui s'affiche par-dessus l'image : vie, argent, mini-carte, réticule, repères de combat.";
                case 2: return "L'affichage et la qualité du rendu. Tout s'applique tout de suite : regarde l'image derrière le menu.";
                case 3: return "L'ambiance de l'image : lumière, couleurs, halo. Le résultat se voit en direct derrière le menu.";
                case 4: return "Le volume de chaque famille de sons.";
                case 5: return "Toutes les touches, réassignables. Une touche changée porte un point orange.";
                default: return "Sous-titres, couleurs et éclairs.";
            }
        }

        // ------------------------------------------------------------------ aperçus

        /// <summary>Un petit écran de jeu : un ciel, une rue, des façades.</summary>
        private static void PreviewScene(Rect r, float u)
        {
            GuiKit.Rounded(r, new Color(0.38f, 0.52f, 0.66f, 1f), 8f * u);
            GuiKit.Fill(new Rect(r.x, r.y + r.height * 0.62f, r.width, r.height * 0.38f - 8f * u), new Color(0.2f, 0.21f, 0.23f, 1f));
            GuiKit.Rounded(new Rect(r.x, r.yMax - 16f * u, r.width, 16f * u), new Color(0.2f, 0.21f, 0.23f, 1f), 8f * u);
            float[] heights = { 0.34f, 0.46f, 0.3f, 0.52f, 0.4f, 0.28f };
            float w = r.width / heights.Length;
            for (int i = 0; i < heights.Length; i++)
            {
                float h = r.height * heights[i];
                Color c = Color.Lerp(new Color(0.52f, 0.44f, 0.4f), new Color(0.62f, 0.6f, 0.55f), (i * 0.37f) % 1f);
                GuiKit.Fill(new Rect(r.x + i * w + 3f * u, r.y + r.height * 0.62f - h, w - 6f * u, h), c);
            }

            GuiKit.RoundedOutline(r, UiTheme.Line, 8f * u, 1f);
        }

        private static void PreviewHud(Rect r, float u)
        {
            PreviewScene(r, u);
            float sx = r.width / 1920f;
            float sy = r.height / 1080f;
            int hud = GameSettings.Hud;
            float faint = hud == 1 ? 0.4f : 1f;

            if (hud != 2)
            {
                if (GameSettings.Minimap)
                {
                    Rect map = new Rect(r.x + 30f * sx, r.y + r.height - (30f + 18f + 188f) * sy, 300f * sx, 188f * sy);
                    GuiKit.Rounded(map, new Color(0.08f, 0.1f, 0.12f, 0.92f), 4f * u);
                    GuiKit.Fill(new Rect(map.x + map.width * 0.2f, map.center.y, map.width * 0.6f, 2f), new Color(1f, 0.74f, 0.29f, 0.9f));
                }

                Rect bars = new Rect(r.x + 30f * sx, r.y + r.height - 38f * sy, 300f * sx, 6f * sy);
                GuiKit.Fill(new Rect(bars.x, bars.y, bars.width * 0.64f, Mathf.Max(2f, bars.height)), new Color(0.94f, 0.94f, 0.95f, faint));
                GuiKit.Fill(new Rect(bars.x + bars.width * 0.66f, bars.y, bars.width * 0.34f, Mathf.Max(2f, bars.height)), new Color(0.4f, 0.7f, 1f, faint));

                GuiKit.Fill(new Rect(r.xMax - 170f * sx, r.y + 30f * sy, 140f * sx, Mathf.Max(5f, 24f * sy)), new Color(0.38f, 0.85f, 0.52f, faint));
                if (hud == 0) GuiKit.Fill(new Rect(r.xMax - 120f * sx, r.y + 64f * sy, 90f * sx, Mathf.Max(2f, 6f * sy)), new Color(1f, 1f, 1f, 0.7f));
            }

            DrawCrosshairAt(r.center, u * 0.8f, GameSettings.Crosshair);
        }

        private static void PreviewCrosshair(Rect r, float u)
        {
            PreviewScene(r, u);
            DrawCrosshairAt(r.center, u * 1.6f, GameSettings.Crosshair);
        }

        private static void DrawCrosshairAt(Vector2 c, float u, int style)
        {
            if (style == 2) return;
            Color ink = new Color(1f, 1f, 1f, 0.9f);
            if (style == 0)
            {
                float d = 4.5f * u;
                GuiKit.Rounded(new Rect(c.x - d * 0.5f - 1f, c.y - d * 0.5f - 1f, d + 2f, d + 2f), new Color(0f, 0f, 0f, 0.5f), d);
                GuiKit.Rounded(new Rect(c.x - d * 0.5f, c.y - d * 0.5f, d, d), ink, d);
                return;
            }

            float t = Mathf.Max(1f, 2f * u), gap = 6f * u, length = 7f * u;
            GuiKit.Fill(new Rect(c.x - gap - length, c.y - t * 0.5f, length, t), ink);
            GuiKit.Fill(new Rect(c.x + gap, c.y - t * 0.5f, length, t), ink);
            GuiKit.Fill(new Rect(c.x - t * 0.5f, c.y - gap - length, t, length), ink);
            GuiKit.Fill(new Rect(c.x - t * 0.5f, c.y + gap, t, length), ink);
        }

        private void PreviewSubtitles(Rect r, float u)
        {
            PreviewScene(r, u);
            if (!GameSettings.Subtitles) return;

            // Le vrai rendu des sous-titres, à l'échelle du petit écran.
            float k = r.height / Screen.height;
            float scale = GameSettings.SubtitleScale;
            const string speaker = "SAMI";
            const string line = "T'as vu l'heure ? Ramène-toi au Vertigo, on t'attend.";

            GUIStyle text = GuiKit.Text(Mathf.Max(7, Mathf.RoundToInt(19f * u * scale * k * 1.6f)), GuiKit.Weight.Medium, TextAnchor.UpperCenter, true);
            GUIStyle name = GuiKit.Text(Mathf.Max(6, Mathf.RoundToInt(12.5f * u * scale * k * 1.6f)), GuiKit.Weight.Bold, TextAnchor.UpperCenter);
            float width = r.width * 0.86f;
            _content.text = line;
            float textHeight = text.CalcHeight(_content, width - 12f * u);
            float textWidth = Mathf.Min(width, text.CalcSize(_content).x + 16f * u);
            bool showSpeaker = GameSettings.SubtitleSpeaker;
            float speakerHeight = showSpeaker ? name.fontSize * 1.4f : 0f;
            float height = textHeight + speakerHeight + 10f * u;
            Rect panel = new Rect(r.center.x - textWidth * 0.5f, r.yMax - height - 12f * u, textWidth, height);

            float background = GameSettings.SubtitleBackground;
            if (background > 0.01f) GuiKit.Rounded(panel, new Color(0f, 0f, 0f, background * 0.85f), 5f * u);

            float y = panel.y + 5f * u;
            if (showSpeaker)
            {
                GuiKit.ShadowLabel(new Rect(r.center.x - width * 0.5f, y, width, speakerHeight), speaker, name, new Color(1f, 0.8f, 0.45f),
                    background > 0.3f ? 0.2f : 0.7f);
                y += speakerHeight;
            }

            GuiKit.ShadowLabel(new Rect(r.center.x - width * 0.5f + 6f * u, y, width - 12f * u, textHeight), line, text, Color.white,
                background > 0.3f ? 0.3f : 0.8f);
        }

        // ------------------------------------------------------------------ bas de l'écran, confirmation

        private void DrawFooter(float x, float y, float u)
        {
            InputBindings b = _input != null ? _input.Bindings : null;
            string select = b != null ? UiTheme.KeyName(b.phoneSelect) : "Entrée";
            string back = b != null ? UiTheme.KeyName(b.releaseCursor) : "Échap";

            if (Capturing)
            {
                UiTheme.KeyHint(x, y, "Échap", "Annuler", u);
                return;
            }

            if (_revert != null) return;

            Row row = !_onTabs && _selected >= 0 && _selected < _rows.Count ? _rows[_selected] : null;
            if (_onTabs)
            {
                x = UiTheme.KeyHint(x, y, "← →", "Onglet", u);
                x = UiTheme.KeyHint(x, y, "↓", "Entrer", u);
            }
            else
            {
                x = UiTheme.KeyHint(x, y, "↑ ↓", "Choisir", u);
                if (row != null && (row.Kind == Kind.Choice || row.Kind == Kind.Slider)) x = UiTheme.KeyHint(x, y, "← →", "Régler", u);
                if (row != null && row.Kind == Kind.Key) x = UiTheme.KeyHint(x, y, select, "Changer la touche", u);
                if (row != null && row.Kind == Kind.Button) x = UiTheme.KeyHint(x, y, select, "Valider", u);
                x = UiTheme.KeyHint(x, y, "Tab", "Onglet suivant", u);
            }

            UiTheme.KeyHint(x, y, back, "Retour", u);
        }

        private void DrawConfirm(float u, Event e)
        {
            float sw = Screen.width;
            float sh = Screen.height;
            GuiKit.Fill(new Rect(0f, 0f, sw, sh), new Color(0f, 0f, 0f, 0.5f));

            Rect box = new Rect((sw - 560f * u) * 0.5f, (sh - 230f * u) * 0.5f, 560f * u, 230f * u);
            UiTheme.DrawPanel(box, 14f * u);
            UiTheme.Label(new Rect(box.x + 32f * u, box.y + 26f * u, box.width - 64f * u, 34f * u), "Garder cet affichage ?", UiTheme.Heading(24f),
                UiTheme.Ink);
            int seconds = Mathf.Max(0, Mathf.CeilToInt(_confirmUntil - _time));
            UiTheme.Label(new Rect(box.x + 32f * u, box.y + 66f * u, box.width - 64f * u, 48f * u),
                "Sans réponse, l'affichage d'avant revient dans " + seconds + " s.", WrapStyle(16f), UiTheme.InkDim);

            string[] labels = { "Garder", "Annuler" };
            float bw = (box.width - 64f * u - 16f * u) * 0.5f;
            for (int i = 0; i < 2; i++)
            {
                Rect button = new Rect(box.x + 32f * u + i * (bw + 16f * u), box.yMax - 74f * u, bw, 46f * u);
                bool hover = button.Contains(e.mousePosition);
                if (hover && UiTheme.MouseMoved) _confirmChoice = i;
                bool on = _confirmChoice == i;
                GuiKit.Rounded(button, on ? new Color(1f, 1f, 1f, 0.92f) : UiTheme.Raised, 8f * u);
                UiTheme.Label(button, labels[i], UiTheme.Text(17f, GuiKit.Weight.Bold, TextAnchor.MiddleCenter),
                    on ? new Color(0.06f, 0.07f, 0.09f) : UiTheme.Ink);

                if (hover && e.type == EventType.MouseDown && e.button == 0)
                {
                    if (i == 0)
                    {
                        _revert = null;
                        Play(Sound.Confirm);
                    }
                    else
                    {
                        Revert();
                    }

                    e.Use();
                }
            }

            if (e.type == EventType.MouseDown || e.type == EventType.MouseUp) e.Use();
        }
    }
}
