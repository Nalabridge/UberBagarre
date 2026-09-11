using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Cinématique inverse analytique à deux os. Sert aux bras (bras + avant-bras)
    /// ET aux jambes (cuisse + tibia) : c'est le même problème géométrique.
    ///
    /// Le problème : on veut pouvoir dire « le poing va ici » et obtenir automatiquement
    /// une épaule et un coude crédibles. Sans ça, il faudrait régler à la main les angles
    /// d'épaule et de coude de chaque image-clé de chaque variante de chaque attaque.
    ///
    /// La méthode : le bras, l'avant-bras et la ligne épaule→cible forment un triangle
    /// dont on connaît les trois longueurs. La loi des cosinus donne directement l'angle
    /// à l'épaule — pas d'itération, pas d'instabilité, résultat identique à chaque frame.
    ///
    /// La direction du coude est imposée par un « pôle » : c'est lui qui fait que le coude
    /// pointe vers le bas et vers l'extérieur, comme en garde de boxe, et pas n'importe où.
    /// </summary>
    public static class TwoBoneIkSolver
    {
        public struct Result
        {
            /// <summary>Rotation monde du bras. Convention : l'axe +Z de l'os suit l'os.</summary>
            public Quaternion UpperRotation;

            /// <summary>Rotation monde du second os (avant-bras ou tibia).</summary>
            public Quaternion LowerRotation;

            /// <summary>Position monde de l'articulation intermediaire (coude ou genou).</summary>
            public Vector3 JointPosition;

            /// <summary>Position réellement atteinte : diffère de la cible si elle était hors de portée.</summary>
            public Vector3 ReachedPosition;
        }

        public static Result Solve(Vector3 shoulder, Vector3 target, float upperLength, float forearmLength,
            Vector3 poleDirection)
        {
            upperLength = Mathf.Max(0.01f, upperLength);
            forearmLength = Mathf.Max(0.01f, forearmLength);

            Vector3 toTarget = target - shoulder;
            float distance = toTarget.magnitude;

            // Cible confondue avec l'épaule : direction arbitraire mais stable.
            Vector3 direction = distance > 1e-5f ? toTarget / distance : Vector3.forward;

            // Bras jamais complètement tendu ni complètement replié : évite les angles dégénérés
            // et garde une silhouette humaine.
            float maxReach = (upperLength + forearmLength) * 0.995f;
            float minReach = Mathf.Abs(upperLength - forearmLength) * 1.02f + 0.01f;
            distance = Mathf.Clamp(distance, minReach, maxReach);

            // Composante du pôle perpendiculaire à l'axe épaule→cible : c'est le côté vers
            // lequel le coude va se déplacer.
            Vector3 pole = poleDirection.sqrMagnitude > 1e-6f ? poleDirection.normalized : Vector3.down;
            Vector3 perpendicular = pole - direction * Vector3.Dot(pole, direction);

            if (perpendicular.sqrMagnitude < 1e-6f)
            {
                // Pôle parallèle à la direction : on prend n'importe quelle perpendiculaire.
                perpendicular = Vector3.Cross(direction, Vector3.up);
                if (perpendicular.sqrMagnitude < 1e-6f) perpendicular = Vector3.Cross(direction, Vector3.right);
            }

            perpendicular.Normalize();

            // Loi des cosinus : angle entre l'axe épaule→cible et le bras.
            float cosAlpha = (upperLength * upperLength + distance * distance - forearmLength * forearmLength)
                             / (2f * upperLength * distance);
            float alpha = Mathf.Acos(Mathf.Clamp(cosAlpha, -1f, 1f));

            Vector3 upperDirection = direction * Mathf.Cos(alpha) + perpendicular * Mathf.Sin(alpha);

            Result result = new Result();
            result.JointPosition = shoulder + upperDirection * upperLength;
            result.ReachedPosition = shoulder + direction * distance;

            Vector3 forearmDirection = result.ReachedPosition - result.JointPosition;
            forearmDirection = forearmDirection.sqrMagnitude > 1e-6f ? forearmDirection.normalized : upperDirection;

            // Axe de flexion : perpendiculaire au plan du bras, donc perpendiculaire aux deux os.
            // L'utiliser comme référence pour le "up" évite toute dégénérescence de LookRotation.
            Vector3 bendAxis = Vector3.Cross(direction, perpendicular);

            result.UpperRotation = Quaternion.LookRotation(upperDirection, Vector3.Cross(bendAxis, upperDirection));
            result.LowerRotation = Quaternion.LookRotation(forearmDirection, Vector3.Cross(bendAxis, forearmDirection));

            return result;
        }
    }
}
