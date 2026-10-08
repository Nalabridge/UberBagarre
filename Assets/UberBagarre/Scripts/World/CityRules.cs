using System.Text;
using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Ce qu'on sait reconnaître dans la ville convertie, par les noms de ses objets. La carte
    /// est arrivée sans ses scripts : ses portes, ses véhicules et ses aides de mise en scène
    /// ne sont plus que des objets nommés. Ces règles sont partagées entre la préparation de la
    /// ville dans l'éditeur (CityPreparation) et son chargement en jeu (MapStreamer).
    /// </summary>
    public static class CityRules
    {
        /// <summary>Nom de l'objet que la préparation de l'éditeur laisse dans la ville.</summary>
        public const string PreparedMarker = "UberBagarre (ville preparee)";

        /// <summary>Calque des petits objets de la ville (dessinés jusqu'à ~110 m).</summary>
        public const int SmallDetailLayer = 26;

        /// <summary>Calque des objets moyens (dessinés jusqu'à ~260 m).</summary>
        public const int MediumDetailLayer = 27;

        /// <summary>Version de la préparation : l'augmenter la fait refaire.</summary>
        public const int PreparedVersion = 3;

        /// <summary>
        /// Les murs de la démo : la ville d'origine était fermée par des murs invisibles et des
        /// barrières de béton autour du quartier jouable, plus quelques bloqueurs devant les
        /// portes. Ici toute la ville est ouverte.
        /// </summary>
        public static bool IsDemoBlocker(string name)
        {
            return name == "DemoBoundaries" || name == "Demo Barrier" || name == "Demo blocker" ||
                   name == "PlayerBlocker";
        }

        /// <summary>Le gond d'une vraie porte : « Container » sous un objet « … Door … ».</summary>
        public static bool IsDoorHinge(Transform t)
        {
            if (t == null || t.name != "Container" || t.parent == null) return false;
            string parent = t.parent.name.ToLowerInvariant();
            return parent.Contains("door") && !parent.Contains("fence") && !parent.Contains("frame");
        }

        /// <summary>
        /// Une porte « de façade » : un décor fermé (« … Door (Static) », « StaticDoor »), avec
        /// son modèle et son point d'accès. Soit le double figé d'une vraie porte (on le cache),
        /// soit l'entrée d'un bâtiment sans intérieur (un magasin y mène ailleurs, une maison
        /// reste close).
        /// </summary>
        public static bool IsStaticDoor(Transform t)
        {
            if (t == null) return false;
            string name = t.name;
            bool named = name.StartsWith("StaticDoor") ||
                         (name.IndexOf("(Static)", System.StringComparison.Ordinal) >= 0 &&
                          name.IndexOf("Door", System.StringComparison.OrdinalIgnoreCase) >= 0);
            return named && t.Find("Model") != null;
        }

        /// <summary>Un battant de porte coulissante (concession, station-service).</summary>
        public static bool IsSlidingPanel(Transform t, out Transform closed, out Transform open)
        {
            closed = null;
            open = null;
            if (t == null || t.parent == null || !t.name.StartsWith("Door")) return false;

            Transform group = t.parent;
            if (group.name != "Door") return false;
            closed = group.Find("Closed");
            open = group.Find("Open");
            return closed != null && open != null && t != closed && t != open;
        }

        /// <summary>
        /// La racine d'un véhicule de la ville : un objet dont un enfant direct porte le même nom
        /// de modèle, casse ignorée (« Sedan (1) » → « Sedan », « Shitbox » → « shitbox »), et
        /// qui a des roues.
        /// </summary>
        public static bool IsVehicleRoot(Transform t)
        {
            if (t == null) return false;
            string model = VehicleModel(t.name);
            if (model == null) return false;

            for (int i = 0; i < t.childCount; i++)
            {
                Transform body = t.GetChild(i);
                if (string.Equals(body.name, model, System.StringComparison.OrdinalIgnoreCase) &&
                    FindWheel(body, "FL") != null) return true;
            }

            return false;
        }

        /// <summary>Le modèle d'un véhicule de la ville d'après son nom, ou null.</summary>
        public static string VehicleModel(string name)
        {
            string bare = StripIndex(name);
            switch (bare)
            {
                case "SUV":
                case "Sedan":
                case "Pickup":
                case "Shitbox":
                case "Coupe":
                case "Van":
                case "Hounddog":
                case "Cheetah":
                case "Veeper":
                case "Bruiser":
                case "Hotbox":
                    return bare;
            }

            return null;
        }

        /// <summary>Une roue : « Wheel_FL », « Wheel FL », « MuscleCarWheel_FL »…</summary>
        public static Transform FindWheel(Transform root, string corner)
        {
            if (root == null) return null;

            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name;
                if (n.Length < 2 + corner.Length) continue;
                if (!n.EndsWith(corner, System.StringComparison.OrdinalIgnoreCase)) continue;

                char sep = n[n.Length - corner.Length - 1];
                if (sep != '_' && sep != ' ') continue;
                // « whee » : la carte a une coquille (« SportsCarWhee_FR »).
                if (n.IndexOf("whee", System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (n.IndexOf("well", System.StringComparison.OrdinalIgnoreCase) >= 0) continue;
                return all[i];
            }

            return null;
        }

        /// <summary>
        /// Certaines roues de la carte portent deux modèles allumés à la fois (la roue générique
        /// « wheel » et celle du modèle, « Muscle Car Wheel », « Sports car wheel ») : le jeu
        /// d'origine n'en gardait qu'une. On éteint la générique quand une autre est là.
        /// Rend le nombre de modèles éteints.
        /// </summary>
        public static int DedupeWheelModels(Transform root)
        {
            int count = 0;
            Transform[] all = root.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Transform model = all[i];
                if (model.name != "Model" || model.childCount < 2) continue;

                Transform generic = null;
                bool specific = false;
                for (int c = 0; c < model.childCount; c++)
                {
                    Transform child = model.GetChild(c);
                    if (!child.gameObject.activeSelf || child.GetComponentInChildren<Renderer>() == null) continue;
                    if (string.Equals(child.name, "wheel", System.StringComparison.OrdinalIgnoreCase)) generic = child;
                    else specific = true;
                }

                if (generic == null || !specific) continue;
                generic.gameObject.SetActive(false);
                count++;
            }

            return count;
        }

        /// <summary>« Sedan (1) » → « Sedan ».</summary>
        public static string StripIndex(string name)
        {
            if (string.IsNullOrEmpty(name)) return name;
            int paren = name.LastIndexOf(" (", System.StringComparison.Ordinal);
            if (paren > 0 && name.EndsWith(")"))
            {
                bool digits = true;
                for (int i = paren + 2; i < name.Length - 1; i++)
                {
                    if (!char.IsDigit(name[i])) digits = false;
                }

                if (digits) return name.Substring(0, paren);
            }

            return name;
        }

        /// <summary>Chemin d'un objet depuis la racine de sa scène (« Map/Container/… »).</summary>
        public static string PathOf(Transform t)
        {
            if (t == null) return null;
            StringBuilder builder = new StringBuilder(t.name);
            for (Transform p = t.parent; p != null; p = p.parent)
            {
                builder.Insert(0, '/');
                builder.Insert(0, p.name);
            }

            return builder.ToString();
        }

        /// <summary>
        /// Le bâtiment d'un chemin : « Map/Container/Casino/… » → « Casino »,
        /// « Map/Container/North town/Pizzeria/… » → « North town/Pizzeria »,
        /// « @Businesses/Laundromat/… » → « @Businesses/Laundromat ».
        /// </summary>
        public static string BuildingOf(string path)
        {
            if (string.IsNullOrEmpty(path)) return path;
            string[] parts = path.Split('/');

            if (parts.Length >= 3 && parts[0] == "Map" && parts[1] == "Container")
            {
                if (parts[2] == "North town" && parts.Length >= 4) return parts[2] + "/" + parts[3];
                return parts[2];
            }

            if (parts.Length >= 2 && parts[0].StartsWith("@")) return parts[0] + "/" + parts[1];
            return parts[0];
        }

        /// <summary>Boîte englobante des rendus actifs sous un objet (vide si aucun).</summary>
        public static Bounds RendererBounds(Transform root, out bool any)
        {
            any = false;
            Bounds bounds = new Bounds(root.position, Vector3.zero);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);
            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (r == null || !r.enabled || r is ParticleSystemRenderer) continue;
                if (!any) bounds = r.bounds;
                else bounds.Encapsulate(r.bounds);
                any = true;
            }

            return bounds;
        }
    }
}
