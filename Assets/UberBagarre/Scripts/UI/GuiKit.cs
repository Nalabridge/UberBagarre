using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Primitives de dessin partagées par toute l'interface.
    ///
    /// Tout est dessiné à partir d'un unique pixel blanc teinté : aucune image, aucune police
    /// importée, aucun Canvas. Un projet Unity neuf n'a rien de tout ça, et cette interface
    /// doit s'afficher à l'identique dans les trois render pipelines.
    ///
    /// Le style visé est celui des jeux de combat : bords épais, biseau clair en haut,
    /// ombre portée, et une couche « retard » qui montre ce qu'on vient de perdre.
    /// </summary>
    public static class GuiKit
    {
        /// <summary>
        /// Opacité globale appliquée à tout ce que dessine GuiKit. Un affichage qui doit
        /// apparaître en fondu la règle avant de dessiner et la remet à 1 après.
        /// </summary>
        public static float Alpha = 1f;

        private static Texture2D _pixel;

        public static Texture2D Pixel
        {
            get
            {
                if (_pixel != null) return _pixel;

                _pixel = new Texture2D(1, 1, TextureFormat.RGBA32, false);
                _pixel.SetPixel(0, 0, Color.white);
                _pixel.Apply();
                _pixel.hideFlags = HideFlags.HideAndDontSave;
                return _pixel;
            }
        }

        private static Texture2D _softDisc;

        /// <summary>Disque doux : opaque au centre, transparent au bord. Sert aux halos lumineux.</summary>
        public static Texture2D SoftDisc
        {
            get
            {
                if (_softDisc != null) return _softDisc;

                const int size = 64;
                _softDisc = new Texture2D(size, size, TextureFormat.RGBA32, false);
                _softDisc.wrapMode = TextureWrapMode.Clamp;
                _softDisc.hideFlags = HideFlags.HideAndDontSave;

                Color[] pixels = new Color[size * size];
                Vector2 centre = new Vector2(size * 0.5f, size * 0.5f);

                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), centre) / (size * 0.5f);
                        float alpha = Mathf.Clamp01(1f - d);

                        // Puissance 3 : coeur lumineux et bord tres doux, sinon on voit un disque net.
                        alpha = alpha * alpha * alpha;
                        pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                    }
                }

                _softDisc.SetPixels(pixels);
                _softDisc.Apply();
                return _softDisc;
            }
        }

        public static void Disc(Rect rect, Color color)
        {
            color.a *= Alpha;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, SoftDisc);
            GUI.color = previous;
        }

        public static void Fill(Rect rect, Color color)
        {
            color.a *= Alpha;
            Color previous = GUI.color;
            GUI.color = color;
            GUI.DrawTexture(rect, Pixel);
            GUI.color = previous;
        }

        /// <summary>Contour dessiné vers l'extérieur du rectangle.</summary>
        public static void Outline(Rect rect, float thickness, Color color)
        {
            Fill(new Rect(rect.x - thickness, rect.y - thickness, rect.width + thickness * 2f, thickness), color);
            Fill(new Rect(rect.x - thickness, rect.yMax, rect.width + thickness * 2f, thickness), color);
            Fill(new Rect(rect.x - thickness, rect.y, thickness, rect.height), color);
            Fill(new Rect(rect.xMax, rect.y, thickness, rect.height), color);
        }

        /// <summary>
        /// Barre de jauge stylisée.
        ///
        /// La couche « retard » (trail) est ce qui rend un gros coup lisible : la vraie valeur
        /// tombe instantanément, la couche claire derrière la rattrape lentement, donc on VOIT
        /// combien on vient de perdre au lieu de le déduire.
        /// </summary>
        public static void Bar(Rect rect, float value, float trail, Color fill, Color trailColor,
            Color background, Color border, float borderThickness, float flash)
        {
            value = Mathf.Clamp01(value);
            trail = Mathf.Clamp01(trail);

            // Ombre portee : detache la barre du decor quelle que soit la scene derriere.
            Fill(new Rect(rect.x + 3f, rect.y + 4f, rect.width, rect.height), new Color(0f, 0f, 0f, 0.35f));

            Outline(rect, borderThickness, border);
            Fill(rect, background);

            if (trail > value)
            {
                Fill(new Rect(rect.x, rect.y, rect.width * trail, rect.height), trailColor);
            }

            Rect fillRect = new Rect(rect.x, rect.y, rect.width * value, rect.height);
            Fill(fillRect, fill);

            // Biseau : une bande claire en haut, une bande sombre en bas. Deux rectangles
            // suffisent a donner du volume, la ou un degrade demanderait une texture.
            if (fillRect.width > 2f)
            {
                Fill(new Rect(fillRect.x, fillRect.y, fillRect.width, fillRect.height * 0.34f),
                    new Color(1f, 1f, 1f, 0.22f));
                Fill(new Rect(fillRect.x, fillRect.yMax - fillRect.height * 0.22f, fillRect.width, fillRect.height * 0.22f),
                    new Color(0f, 0f, 0f, 0.20f));
            }

            if (flash > 0.001f)
            {
                Fill(rect, new Color(1f, 1f, 1f, Mathf.Clamp01(flash) * 0.7f));
            }
        }

        /// <summary>Texte avec contour, seul moyen de rester lisible sur n'importe quel fond.</summary>
        public static void OutlinedLabel(Rect rect, string text, GUIStyle style, Color textColor,
            Color outlineColor, float thickness)
        {
            Color previousContent = GUI.contentColor;
            textColor.a *= Alpha;
            outlineColor.a *= Alpha;

            GUI.contentColor = outlineColor;
            for (int x = -1; x <= 1; x++)
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (x == 0 && y == 0) continue;
                    GUI.Label(new Rect(rect.x + x * thickness, rect.y + y * thickness, rect.width, rect.height), text, style);
                }
            }

            GUI.contentColor = textColor;
            GUI.Label(rect, text, style);
            GUI.contentColor = previousContent;
        }

        private static readonly System.Collections.Generic.Dictionary<int, GUIStyle> Styles =
            new System.Collections.Generic.Dictionary<int, GUIStyle>(64);

        /// <summary>
        /// Un style de texte PARTAGÉ : ne jamais le modifier après l'avoir reçu.
        ///
        /// Il était recréé à chaque appel : une vingtaine d'affichages, plusieurs styles
        /// chacun, deux passages d'OnGUI par image — des centaines d'objets par seconde, avec
        /// finaliseur natif, que le ramasse-miettes devait nettoyer. Ses passages faisaient
        /// saccader l'image, et d'autant plus en combat, où les chiffres de dégâts, les barres
        /// et les combos s'affichent tous en même temps.
        /// </summary>
        public static GUIStyle Style(int fontSize, FontStyle fontStyle, TextAnchor anchor)
        {
            return Style(fontSize, fontStyle, anchor, false);
        }

        /// <summary>Même chose, avec retour à la ligne automatique si <paramref name="wordWrap"/>.</summary>
        public static GUIStyle Style(int fontSize, FontStyle fontStyle, TextAnchor anchor, bool wordWrap)
        {
            int key = (Mathf.Clamp(fontSize, 0, 4095))
                      | ((int)fontStyle << 12)
                      | ((int)anchor << 16)
                      | (wordWrap ? 1 << 24 : 0);

            GUIStyle style;
            if (Styles.TryGetValue(key, out style) && style != null) return style;

            style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.fontStyle = fontStyle;
            style.alignment = anchor;
            style.wordWrap = wordWrap;
            style.padding = new RectOffset(0, 0, 0, 0);

            // La police du jeu (Roboto) : le gras a son propre fichier, plus net qu'un gras
            // synthétisé ; l'italique, lui, reste synthétisé.
            bool bold = fontStyle == FontStyle.Bold || fontStyle == FontStyle.BoldAndItalic;
            Font font = FontOf(bold ? Weight.Bold : Weight.Regular);
            if (font != null)
            {
                style.font = font;
                style.fontStyle = fontStyle == FontStyle.BoldAndItalic || fontStyle == FontStyle.Italic ? FontStyle.Italic : FontStyle.Normal;
            }

            Styles[key] = style;
            return style;
        }

        // ------------------------------------------------------------------ police

        /// <summary>Les graisses de la police du jeu.</summary>
        public enum Weight
        {
            Regular,
            Medium,
            Bold,
            Black
        }

        private static readonly Font[] Fonts = new Font[4];
        private static bool _fontsLoaded;

        /// <summary>La police du jeu (Roboto, Resources/Polices), ou null si elle manque.</summary>
        public static Font FontOf(Weight weight)
        {
            if (!_fontsLoaded)
            {
                _fontsLoaded = true;
                string[] files = { "Roboto-Regular", "Roboto-Medium", "Roboto-Bold", "Roboto-Black" };
                for (int i = 0; i < files.Length; i++) Fonts[i] = Resources.Load<Font>("Polices/" + files[i]);
            }

            Font font = Fonts[(int)weight];
            return font != null ? font : Fonts[(int)Weight.Bold];
        }

        /// <summary>Un style de texte dans une graisse précise de la police du jeu (partagé, lui aussi).</summary>
        public static GUIStyle Text(int fontSize, Weight weight, TextAnchor anchor, bool wordWrap = false)
        {
            int key = (Mathf.Clamp(fontSize, 0, 4095))
                      | ((int)weight << 12)
                      | ((int)anchor << 16)
                      | (wordWrap ? 1 << 24 : 0)
                      | (1 << 26);

            GUIStyle style;
            if (Styles.TryGetValue(key, out style) && style != null) return style;

            style = new GUIStyle(GUI.skin.label);
            style.fontSize = fontSize;
            style.alignment = anchor;
            style.wordWrap = wordWrap;
            style.padding = new RectOffset(0, 0, 0, 0);
            style.richText = false;

            Font font = FontOf(weight);
            if (font != null) style.font = font;
            else style.fontStyle = weight >= Weight.Bold ? FontStyle.Bold : FontStyle.Normal;

            Styles[key] = style;
            return style;
        }

        /// <summary>Texte avec une ombre portée douce (au lieu d'un contour) : le style des menus.</summary>
        public static void ShadowLabel(Rect rect, string text, GUIStyle style, Color color, float shadow = 0.55f)
        {
            Color previous = GUI.contentColor;
            float offset = Mathf.Max(1f, style.fontSize * 0.05f);

            GUI.contentColor = new Color(0f, 0f, 0f, color.a * shadow * Alpha);
            GUI.Label(new Rect(rect.x, rect.y + offset, rect.width, rect.height), text, style);

            GUI.contentColor = new Color(color.r, color.g, color.b, color.a * Alpha);
            GUI.Label(rect, text, style);
            GUI.contentColor = previous;
        }

        // ------------------------------------------------------------------ formes arrondies

        private static readonly System.Collections.Generic.Dictionary<int, GUIStyle> Shapes =
            new System.Collections.Generic.Dictionary<int, GUIStyle>(32);

        /// <summary>Un rectangle aux coins arrondis, plein.</summary>
        public static void Rounded(Rect rect, Color color, float radius)
        {
            DrawShape(rect, color, radius, 0f, 0f);
        }

        /// <summary>Le contour d'un rectangle aux coins arrondis.</summary>
        public static void RoundedOutline(Rect rect, Color color, float radius, float thickness)
        {
            DrawShape(rect, color, radius, Mathf.Max(1f, thickness), 0f);
        }

        /// <summary>
        /// Un halo autour d'un rectangle arrondi (ombre portée en noir, lueur de néon en couleur) :
        /// il déborde de <paramref name="spread"/> pixels.
        /// </summary>
        public static void Glow(Rect rect, Color color, float radius, float spread)
        {
            spread = Mathf.Max(2f, spread);
            Rect outer = new Rect(rect.x - spread, rect.y - spread, rect.width + spread * 2f, rect.height + spread * 2f);
            DrawShape(outer, color, radius + spread, 0f, spread);
        }

        private static void DrawShape(Rect rect, Color color, float radius, float thickness, float blur)
        {
            if (Event.current == null || Event.current.type != EventType.Repaint) return;
            if (rect.width <= 0.5f || rect.height <= 0.5f) return;

            int r = Mathf.Clamp(Mathf.RoundToInt(Mathf.Min(radius, rect.width * 0.5f, rect.height * 0.5f)), 1, 96);
            int t = Mathf.Clamp(Mathf.RoundToInt(thickness), 0, 16);
            int b = Mathf.Clamp(Mathf.RoundToInt(blur), 0, 96);
            if (b > 0) r = Mathf.Max(r, b + 1);

            int key = r | (t << 8) | (b << 16);
            GUIStyle style;
            if (!Shapes.TryGetValue(key, out style) || style == null || style.normal.background == null)
            {
                style = new GUIStyle();
                style.normal.background = ShapeTexture(r, t, b);
                style.border = new RectOffset(r + 1, r + 1, r + 1, r + 1);
                Shapes[key] = style;
            }

            Color previous = GUI.color;
            GUI.color = new Color(color.r, color.g, color.b, color.a * Alpha);
            style.Draw(rect, GUIContent.none, false, false, false, false);
            GUI.color = previous;
        }

        /// <summary>
        /// La texture d'un coin (tranchée en neuf à l'affichage) : rectangle arrondi de rayon r,
        /// anticrénelé ; évidé si t > 0 (contour), estompé sur b pixels si b > 0 (halo).
        /// </summary>
        private static Texture2D ShapeTexture(int r, int t, int b)
        {
            int size = r * 2 + 3;
            Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            texture.hideFlags = HideFlags.HideAndDontSave;

            Color[] pixels = new Color[size * size];
            float centre = size * 0.5f;
            float half = centre - 0.5f;   // demi-côté de la texture

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Distance signée au bord d'un rectangle arrondi (négative dedans).
                    float px = Mathf.Abs(x + 0.5f - centre) - (half - r);
                    float py = Mathf.Abs(y + 0.5f - centre) - (half - r);
                    float outside = new Vector2(Mathf.Max(px, 0f), Mathf.Max(py, 0f)).magnitude + Mathf.Min(Mathf.Max(px, py), 0f) - r;

                    float alpha;
                    if (b > 0)
                    {
                        // Halo : plein à l'intérieur du rectangle, puis décroissance douce.
                        float d = outside + b;
                        alpha = d <= 0f ? 1f : Mathf.Pow(1f - Mathf.Clamp01(d / b), 2.2f);
                    }
                    else
                    {
                        alpha = Mathf.Clamp01(0.5f - outside);
                        if (t > 0) alpha *= Mathf.Clamp01(0.5f + outside + t);
                    }

                    pixels[y * size + x] = new Color(1f, 1f, 1f, alpha);
                }
            }

            texture.SetPixels(pixels);
            texture.Apply();
            return texture;
        }

        /// <summary>
        /// La caméra à utiliser pour projeter : celle qu'on préfère si elle est allumée, sinon
        /// celle du jeu.
        ///
        /// Indispensable dès qu'il existe plus d'un point de vue. Les affichages du monde — barres
        /// de vie, chiffres de dégâts, marqueurs — gardent une référence câblée vers la caméra
        /// première personne ; basculer en caméra d'observation les laisserait projeter depuis une
        /// caméra éteinte, et tout se retrouverait au mauvais endroit de l'écran sans qu'aucune
        /// erreur n'apparaisse.
        /// </summary>
        public static Camera ActiveCamera(Camera preferred)
        {
            if (preferred != null && preferred.isActiveAndEnabled) return preferred;
            return Camera.main;
        }

        /// <summary>Convertit une position monde en coordonnées GUI. Renvoie faux si c'est derrière la caméra.</summary>
        public static bool WorldToGui(Camera camera, Vector3 worldPosition, out Vector2 guiPosition)
        {
            guiPosition = Vector2.zero;
            if (camera == null) return false;

            Vector3 screen = camera.WorldToScreenPoint(worldPosition);
            if (screen.z <= 0f) return false;

            guiPosition = new Vector2(screen.x, Screen.height - screen.y);
            return true;
        }
    }
}
