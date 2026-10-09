using UnityEngine;

namespace UberBagarre.View
{
    /// <summary>
    /// Le repère réel d'une main du corps (vers où pointent les doigts, de quel côté est la
    /// paume), mesuré sur la pose de liaison. On oriente ensuite la main par ce repère, quelle
    /// que soit la convention d'axes des os : « doigts vers l'avant, paume vers le visage » se
    /// dit en directions du monde.
    /// </summary>
    public static class HandFrame
    {
        /// <summary>
        /// Mesure la main au bout de <paramref name="arm"/> ; <paramref name="rest"/> convertit
        /// ensuite un repère (doigts, paume) en rotation de l'os du poignet :
        /// <c>wrist.rotation = Quaternion.LookRotation(doigts, paume) * rest</c>.
        /// </summary>
        public static bool Measure(IkLimb arm, bool right, out Quaternion rest)
        {
            rest = Quaternion.identity;
            if (arm == null || arm.End == null) return false;
            Transform wrist = arm.End;
            Transform middle = FindDeep(wrist, "Majeur1");
            Transform index = FindDeep(wrist, "Index1");
            Transform little = FindDeep(wrist, "Auriculaire1");
            if (middle == null || index == null || little == null) return false;

            Vector3 fingers = (middle.position - wrist.position).normalized;
            Vector3 thumbSide = (index.position - little.position).normalized;
            Vector3 palm = Vector3.Cross(fingers, thumbSide) * (right ? 1f : -1f);
            if (palm.sqrMagnitude < 1e-6f) return false;
            rest = Quaternion.Inverse(Quaternion.LookRotation(fingers, palm)) * wrist.rotation;
            return true;
        }

        public static Transform FindDeep(Transform root, string suffix)
        {
            for (int i = 0; i < root.childCount; i++)
            {
                Transform child = root.GetChild(i);
                if (child.name.EndsWith(suffix)) return child;
                Transform found = FindDeep(child, suffix);
                if (found != null) return found;
            }

            return null;
        }
    }
}
