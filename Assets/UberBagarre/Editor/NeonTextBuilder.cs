using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Écrit un mot en tubes de néon.
    ///
    /// Pourquoi une police faite de traits plutôt qu'un texte 3D : un TextMesh est un
    /// quad texturé qui reste plat, ne projette rien et ne se reflète pas correctement
    /// dans une flaque. Une enseigne réelle est un TUBE — un volume, avec une épaisseur,
    /// qui occupe de la place devant le mur et dont la lumière s'échappe par les côtés.
    /// C'est cette épaisseur qui la fait exister.
    ///
    /// Chaque lettre est décrite par des polylignes dans un carré unité (0,0 en bas à
    /// gauche, 1,1 en haut à droite). Un segment devient une boîte allongée, orientée
    /// selon sa direction : les diagonales et les arrondis approchés sortent donc
    /// naturellement, ce qu'un afficheur à segments ne permet pas.
    /// </summary>
    public static class NeonTextBuilder
    {
        /// <summary>Largeur d'une lettre par rapport à sa hauteur.</summary>
        public const float AspectRatio = 0.62f;

        /// <summary>Espace entre deux lettres, en fraction de la hauteur.</summary>
        public const float Tracking = 0.22f;

        private static readonly Dictionary<char, string> Glyphs = new Dictionary<char, string>
        {
            { 'A', "0,0 0.5,1 1,0;0.18,0.34 0.82,0.34" },
            { 'B', "0,0 0,1;0,1 0.72,1 0.92,0.8 0.72,0.55 0,0.55;0,0.55 0.8,0.55 1,0.3 0.8,0 0,0" },
            { 'C', "0.96,0.8 0.7,1 0.28,1 0,0.74 0,0.26 0.28,0 0.7,0 0.96,0.2" },
            { 'D', "0,0 0,1 0.58,1 0.96,0.7 0.96,0.3 0.58,0 0,0" },
            { 'E', "1,1 0,1 0,0 1,0;0,0.5 0.76,0.5" },
            { 'F', "1,1 0,1 0,0;0,0.52 0.76,0.52" },
            { 'G', "0.96,0.8 0.7,1 0.28,1 0,0.74 0,0.26 0.28,0 0.7,0 0.96,0.22 0.96,0.46 0.54,0.46" },
            { 'H', "0,0 0,1;1,0 1,1;0,0.5 1,0.5" },
            { 'I', "0.5,0 0.5,1;0.16,1 0.84,1;0.16,0 0.84,0" },
            { 'J', "0.86,1 0.86,0.24 0.6,0 0.28,0 0.04,0.22" },
            { 'K', "0,0 0,1;0,0.44 0.94,1;0.08,0.5 0.96,0" },
            { 'L', "0,1 0,0 0.94,0" },
            { 'M', "0,0 0,1 0.5,0.4 1,1 1,0" },
            { 'N', "0,0 0,1 1,0 1,1" },
            { 'O', "0.28,1 0.72,1 1,0.72 1,0.28 0.72,0 0.28,0 0,0.28 0,0.72 0.28,1" },
            { 'P', "0,0 0,1 0.72,1 0.96,0.78 0.72,0.52 0,0.52" },
            { 'Q', "0.28,1 0.72,1 1,0.72 1,0.28 0.72,0 0.28,0 0,0.28 0,0.72 0.28,1;0.6,0.3 1,-0.08" },
            { 'R', "0,0 0,1 0.72,1 0.96,0.78 0.72,0.52 0,0.52;0.4,0.52 1,0" },
            { 'S', "0.96,0.84 0.7,1 0.26,1 0,0.8 0.1,0.58 0.9,0.44 1,0.22 0.74,0 0.3,0 0.04,0.16" },
            { 'T', "0,1 1,1;0.5,1 0.5,0" },
            { 'U', "0,1 0,0.28 0.28,0 0.72,0 1,0.28 1,1" },
            { 'V', "0,1 0.5,0 1,1" },
            { 'W', "0,1 0.22,0 0.5,0.58 0.78,0 1,1" },
            { 'X', "0,0 1,1;0,1 1,0" },
            { 'Y', "0,1 0.5,0.5 1,1;0.5,0.5 0.5,0" },
            { 'Z', "0,1 1,1 0,0 1,0" },
            { '0', "0.28,1 0.72,1 1,0.72 1,0.28 0.72,0 0.28,0 0,0.28 0,0.72 0.28,1" },
            { '1', "0.18,0.8 0.5,1 0.5,0;0.18,0 0.82,0" },
            { '2', "0,0.8 0.26,1 0.74,1 1,0.78 0,0 1,0" },
            { '3', "0,0.85 0.3,1 0.8,1 0.96,0.78 0.58,0.54 0.96,0.3 0.8,0 0.3,0 0,0.16" },
            { '4', "0.74,0 0.74,1 0,0.34 1,0.34" },
            { '5', "1,1 0.16,1 0.06,0.56 0.7,0.6 0.96,0.4 0.8,0 0.2,0 0,0.16" },
            { '6', "0.9,0.86 0.6,1 0.24,0.9 0,0.5 0,0.2 0.3,0 0.7,0 0.96,0.26 0.7,0.5 0.14,0.42" },
            { '7', "0,1 1,1 0.34,0" },
            { '8', "0.3,0.54 0.1,0.76 0.3,1 0.7,1 0.9,0.76 0.7,0.54 0.3,0.54 0.04,0.3 0.3,0 0.7,0 0.96,0.3 0.7,0.54" },
            { '9', "0.1,0.14 0.4,0 0.76,0.1 1,0.5 1,0.8 0.7,1 0.3,1 0.04,0.74 0.3,0.5 0.86,0.58" },
            { '-', "0.12,0.5 0.88,0.5" },
            { '.', "0.42,0.06 0.58,0.06" },
            { '/', "0.08,0 0.92,1" },
            { '!', "0.5,1 0.5,0.28;0.5,0.14 0.5,0" },
            { ' ', "" }
        };

        /// <summary>
        /// Construit le mot dans le plan XY local du parent, centré horizontalement autour
        /// de zéro, la base des lettres à y = 0. Renvoie la largeur totale.
        /// </summary>
        public static float Build(Transform parent, string text, float letterHeight, float tubeThickness,
            Material material)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            text = text.ToUpperInvariant();

            float letterWidth = letterHeight * AspectRatio;
            float advance = letterWidth + letterHeight * Tracking;
            float total = text.Length * advance - letterHeight * Tracking;

            float cursor = -total * 0.5f;

            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];

                string glyph;
                if (!Glyphs.TryGetValue(c, out glyph)) glyph = "0,0 1,0;0,1 1,1";

                if (!string.IsNullOrEmpty(glyph))
                {
                    GameObject letter = EditorBuildUtility.CreateEmpty("Lettre " + c, parent,
                        new Vector3(cursor, 0f, 0f));

                    BuildGlyph(letter.transform, glyph, letterWidth, letterHeight, tubeThickness, material);
                }

                cursor += advance;
            }

            return total;
        }

        private static void BuildGlyph(Transform parent, string glyph, float width, float height,
            float thickness, Material material)
        {
            string[] polylines = glyph.Split(';');

            for (int p = 0; p < polylines.Length; p++)
            {
                string[] points = polylines[p].Split(' ');
                Vector2 previous = Vector2.zero;
                bool hasPrevious = false;

                for (int i = 0; i < points.Length; i++)
                {
                    if (string.IsNullOrEmpty(points[i])) continue;

                    string[] pair = points[i].Split(',');
                    if (pair.Length != 2) continue;

                    float x, y;
                    if (!float.TryParse(pair[0], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out x)) continue;
                    if (!float.TryParse(pair[1], System.Globalization.NumberStyles.Float,
                            System.Globalization.CultureInfo.InvariantCulture, out y)) continue;

                    Vector2 current = new Vector2(x * width, y * height);

                    if (hasPrevious) Tube(parent, previous, current, thickness, material);

                    previous = current;
                    hasPrevious = true;
                }
            }
        }

        /// <summary>
        /// Un segment de tube. Il dépasse légèrement de chaque côté (la moitié de son
        /// épaisseur) pour que deux segments consécutifs se rejoignent sans laisser un
        /// coin vide — un néon n'a pas de trous à ses angles.
        /// </summary>
        private static void Tube(Transform parent, Vector2 from, Vector2 to, float thickness, Material material)
        {
            Vector2 delta = to - from;
            float length = delta.magnitude;
            if (length < 1e-4f) return;

            Vector2 center = (from + to) * 0.5f;
            float angle = Mathf.Atan2(delta.y, delta.x) * Mathf.Rad2Deg;

            GameObject tube = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Tube", parent,
                new Vector3(center.x, center.y, 0f),
                new Vector3(length + thickness, thickness, thickness), material, false);

            tube.transform.localRotation = Quaternion.Euler(0f, 0f, angle);
        }

        /// <summary>Largeur totale qu'occupera un mot, sans le construire.</summary>
        public static float MeasureWidth(string text, float letterHeight)
        {
            if (string.IsNullOrEmpty(text)) return 0f;

            float letterWidth = letterHeight * AspectRatio;
            float advance = letterWidth + letterHeight * Tracking;
            return text.Length * advance - letterHeight * Tracking;
        }
    }
}
