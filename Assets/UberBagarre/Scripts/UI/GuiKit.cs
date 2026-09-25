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

            Styles[key] = style;
            return style;
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
