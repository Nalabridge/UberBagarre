using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// La forme d'un crâne, mesurée une fois pour toutes par l'éditeur : depuis un point au
    /// centre de la tête, la distance jusqu'à la peau dans chaque direction (une grille
    /// d'azimuts × hauteurs). Les coupes de cheveux et les barbes sont des coques posées à cette
    /// distance plus leur épaisseur : elles épousent exactement la tête de chaque corps.
    ///
    /// Repère des directions (celui du corps au repos) : y vers le haut, z vers l'avant.
    /// Azimut θ : 0 devant, +90° à droite (x+), ±180° derrière. Hauteur φ : 0 au sommet du
    /// crâne, 90° à l'horizontale, jusqu'à <see cref="PhiMax"/> sous le menton.
    /// </summary>
    public class HairProfile : ScriptableObject
    {
        public const int Columns = 72;
        public const int Rows = 48;
        public const float PhiMax = 165f;

        [Tooltip("Le centre de la tête, dans le repère de l'os de la tête.")]
        public Vector3 center;

        [Tooltip("Du repère du corps au repos vers celui de l'os de la tête.")]
        public Quaternion toHead = Quaternion.identity;

        [Tooltip("La distance centre → peau, Columns × Rows (azimut majeur), en mètres.")]
        public float[] radii = new float[0];

        [Tooltip("1 là où un rayon a vraiment touché la tête (0 : valeur prolongée).")]
        public byte[] hits = new byte[0];

        [Tooltip("La date du fichier du corps mesuré : la mesure est refaite s'il change.")]
        public long sourceStamp;

        public bool IsValid
        {
            get { return radii != null && radii.Length == Columns * Rows; }
        }

        public static float ThetaOf(int column)
        {
            return -180f + column * 360f / Columns;
        }

        public static float PhiOf(int row)
        {
            return row * PhiMax / (Rows - 1);
        }

        /// <summary>La direction (θ, φ) en degrés, dans le repère du corps au repos.</summary>
        public static Vector3 Direction(float thetaDeg, float phiDeg)
        {
            float t = thetaDeg * Mathf.Deg2Rad;
            float p = phiDeg * Mathf.Deg2Rad;
            float sp = Mathf.Sin(p);
            return new Vector3(sp * Mathf.Sin(t), Mathf.Cos(p), sp * Mathf.Cos(t));
        }

        /// <summary>La distance jusqu'à la peau dans la direction (θ, φ), interpolée.</summary>
        public float Radius(float thetaDeg, float phiDeg)
        {
            float x = Mathf.Repeat((thetaDeg + 180f) / 360f * Columns, Columns);
            float y = Mathf.Clamp(phiDeg / PhiMax * (Rows - 1), 0f, Rows - 1.001f);
            int i = Mathf.FloorToInt(x);
            int j = Mathf.FloorToInt(y);
            float fx = x - i;
            float fy = y - j;
            int i1 = (i + 1) % Columns;
            float a = radii[i * Rows + j], b = radii[i1 * Rows + j];
            float c = radii[i * Rows + j + 1], d = radii[i1 * Rows + j + 1];
            return Mathf.Lerp(Mathf.Lerp(a, b, fx), Mathf.Lerp(c, d, fx), fy);
        }

        /// <summary>Un point à <paramref name="lift"/> mètres au-dessus de la peau, dans le repère de l'os de la tête.</summary>
        public Vector3 Point(float thetaDeg, float phiDeg, float lift)
        {
            return center + toHead * (Direction(thetaDeg, phiDeg) * (Radius(thetaDeg, phiDeg) + lift));
        }
    }
}
