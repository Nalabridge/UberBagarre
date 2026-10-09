using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les intérieurs des magasins dont la carte n'a que la façade (le casino, la pharmacie, la
    /// pizzeria, les bars, l'arcade, la salle de boxe…). Chacun est une vraie pièce, meublée
    /// selon son métier par <see cref="InteriorDesigner"/> (comptoir, rayons garnis, frigos,
    /// banquettes, machines à sous, ring…), construite par <see cref="InteriorRealizer"/>,
    /// posée loin de la ville et allumée seulement quand on y est — comme le Vertigo. On y entre
    /// par la façade (fondu au noir), on en sort par sa porte.
    ///
    /// Repère d'une pièce : l'entrée au milieu du mur sud (z = 0), on entre vers +Z.
    /// </summary>
    internal static class ShopInteriorBuilder
    {
        public sealed class Result
        {
            public GameObject Root;
            public Transform Arrival;
            public Interactable Exit;

            /// <summary>Où se tient le vendeur (monde), tourné vers la salle.</summary>
            public Vector3 ClerkPosition;
            public Quaternion ClerkRotation;

            /// <summary>Le comptoir : E dessus ouvre le magasin.</summary>
            public Transform Counter;
        }

        /// <summary>La pièce d'un métier, à <paramref name="origin"/> (monde).</summary>
        public static Result Build(ShopKind kind, string sign, Transform parent, Vector3 origin)
        {
            InteriorPlan plan = InteriorDesigner.Shop(kind.ToString(), sign, Seed(sign));
            InteriorRealizer.Built built = InteriorRealizer.Realize(plan, parent, origin, "Interieur " + sign, "Interieur_" + FileName(sign));
            Result result = new Result { Root = built.Root };

            // La sortie : le battant vitré de la porte.
            GameObject door;
            if (!built.Groups.TryGetValue("porte", out door)) door = built.Root;
            if (door.GetComponent<Collider>() == null)
            {
                BoxCollider leaf = door.AddComponent<BoxCollider>();
                leaf.center = new Vector3(0f, 1.1f, 0.05f);
                leaf.size = new Vector3(1.1f, 2.2f, 0.1f);
            }

            Interactable exit = door.AddComponent<Interactable>();
            SerializedWiring.SetString(exit, "_label", "Sortir");
            SerializedWiring.SetString(exit, "_hint", "Retour dans la rue.");
            SerializedWiring.SetFloat(exit, "_range", 3f);
            result.Exit = exit;

            result.Arrival = Marker(built, "arrivee", new Vector3(0f, 0.05f, 1.5f));

            // Le comptoir : une zone d'achat (déclencheur) devant le vendeur.
            Transform counter = Marker(built, "comptoir", new Vector3(0f, 0f, plan.Size.z - 1.7f));
            InteriorPlan.Marker data;
            float length = built.MarkerData.TryGetValue("comptoir", out data) && data.Length > 0.1f ? data.Length : 2.4f;
            BoxCollider zone = counter.gameObject.AddComponent<BoxCollider>();
            zone.center = new Vector3(0f, 1.1f, -0.2f);
            zone.size = new Vector3(length, 1.2f, 1.2f);
            zone.isTrigger = true;
            counter.name = "Comptoir";
            result.Counter = counter;

            Transform clerk = Marker(built, "vendeur", counter.localPosition + counter.localRotation * new Vector3(0f, 0f, 0.75f));
            result.ClerkPosition = clerk.position;
            result.ClerkRotation = clerk.rotation;

            built.Root.SetActive(false);
            return result;
        }

        /// <summary>Un repère du plan (ou, s'il manque, un repère posé à <paramref name="fallback"/>).</summary>
        private static Transform Marker(InteriorRealizer.Built built, string key, Vector3 fallback)
        {
            Transform marker;
            if (built.Markers.TryGetValue(key, out marker)) return marker;
            Debug.LogWarning("[UberBagarre] Interieur « " + built.Root.name + " » : repere « " + key + " » absent du plan.");
            marker = EditorBuildUtility.CreateEmpty("Repere " + key, built.Root.transform, fallback).transform;
            built.Markers[key] = marker;
            return marker;
        }

        /// <summary>Le même magasin donne le même intérieur d'une construction à l'autre.</summary>
        private static int Seed(string sign)
        {
            unchecked
            {
                int h = 17;
                foreach (char c in sign ?? "") h = h * 31 + c;
                return h & 0x7fffffff;
            }
        }

        private static string FileName(string sign)
        {
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            foreach (char c in Ascii(sign))
            {
                b.Append(char.IsLetterOrDigit(c) ? c : '_');
            }

            return b.ToString();
        }

        /// <summary>L'enseigne au néon n'a que des majuscules sans accent.</summary>
        public static string Ascii(string text)
        {
            string normalized = (text ?? "").ToUpperInvariant().Normalize(System.Text.NormalizationForm.FormD);
            System.Text.StringBuilder b = new System.Text.StringBuilder();
            for (int i = 0; i < normalized.Length; i++)
            {
                char c = normalized[i];
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if ((c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == ' ' || c == '-' || c == '.' || c == '!') b.Append(c);
                else if (c == '\'' || c == '’') b.Append(' ');
            }

            return b.ToString();
        }
    }
}
