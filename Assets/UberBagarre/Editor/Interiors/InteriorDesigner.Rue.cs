using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La rue devant la vitrine. Les intérieurs sont construits loin de la ville, au-dessus de
    /// l'eau : par la porte vitrée et les vitrines, on voyait la mer jusqu'à l'horizon. Devant
    /// chaque magasin, une rue en décor la remplace : trottoir dallé, bordure, chaussée et son
    /// marquage, le trottoir d'en face, une rangée d'immeubles (rez-de-chaussée commerçants,
    /// étages percés de fenêtres), deux immeubles qui ferment la rue de chaque côté, des
    /// lampadaires, un arbre, du mobilier.
    ///
    /// Elle n'a pas de toit : le vrai ciel est au-dessus et le vrai soleil l'éclaire, donc la rue
    /// suit l'heure et la météo sans rien faire. Les façades montent toutes plus haut que l'œil :
    /// l'horizon (et la mer) est caché de partout.
    /// </summary>
    public static partial class InteriorDesigner
    {
        private static readonly string[] ShopNames =
        {
            "PRESSING", "TABAC", "KEBAB", "OPTIQUE", "LAVERIE", "BOULANGERIE", "PHONE", "BAR", "COIFFURE", "AUTO-ECOLE",
            "PIZZA", "BAZAR", "FLEURS", "ASSURANCES"
        };

        private static void StreetPalette(InteriorPlan p)
        {
            p.Define("trottoir", new Color(0.62f, 0.61f, 0.58f), 0.3f, 0f, "paves");
            p.Define("bordure", new Color(0.7f, 0.69f, 0.66f), 0.25f, 0f, "beton");
            p.Define("asphalte", new Color(0.2f, 0.2f, 0.21f), 0.35f, 0f, "asphalte");
            p.Define("peinture_route", new Color(0.92f, 0.9f, 0.82f), 0.35f, 0f, null);
            p.Define("facade_brique", new Color(0.62f, 0.36f, 0.27f), 0.25f, 0f, "brique");
            p.Define("facade_enduit", new Color(0.84f, 0.79f, 0.68f), 0.2f, 0f, "platre");
            p.Define("facade_enduit_bleu", new Color(0.52f, 0.62f, 0.7f), 0.2f, 0f, "platre");
            p.Define("facade_enduit_vert", new Color(0.55f, 0.64f, 0.52f), 0.2f, 0f, "platre");
            p.Define("facade_beton", new Color(0.58f, 0.58f, 0.57f), 0.25f, 0f, "beton");
            p.Define("encadrement", new Color(0.86f, 0.84f, 0.8f), 0.35f, 0f, null);
            p.Define("vitre_sombre", new Color(0.07f, 0.09f, 0.11f), 0.96f, 0.2f, null);
            p.Define("store_rouge", new Color(0.62f, 0.1f, 0.08f), 0.15f, 0f, "moquette");
            p.Define("store_vert", new Color(0.12f, 0.36f, 0.2f), 0.15f, 0f, "moquette");
            p.Define("store_bleu", new Color(0.12f, 0.22f, 0.45f), 0.15f, 0f, "moquette");
            p.Define("ecorce", new Color(0.3f, 0.22f, 0.16f), 0.15f, 0f, "bois");
            p.Glow("lampadaire", new Color(1f, 0.92f, 0.75f), new Color(1f, 0.82f, 0.55f) * 2.2f);
            p.Glow("vitrine_en_face", new Color(0.18f, 0.16f, 0.12f), new Color(1f, 0.85f, 0.6f) * 0.55f);
        }

        /// <summary>La rue devant la façade (z &lt; 0), sur toute la largeur vue depuis l'entrée.</summary>
        private static void Street(InteriorPlan p)
        {
            StreetPalette(p);
            string group = p.Group;
            p.Group = "";

            float w = p.Size.x;
            float half = w * 0.5f + 7f;             // la rue s'arrête à 7 m de chaque côté du magasin
            const float sidewalk = 3.2f;             // trottoir de ce côté
            const float road = 7.2f;                 // chaussée (deux voies)
            const float far = 3f;                    // trottoir d'en face
            float roadNear = -sidewalk;
            float roadFar = roadNear - road;
            float facade = roadFar - far;

            // --- sol
            p.Box("Trottoir", V(0f, -0.07f, -sidewalk * 0.5f - 0.05f), V(half * 2f, 0.12f, sidewalk - 0.1f), "trottoir");
            p.Box("Bordure", V(0f, -0.075f, roadNear - 0.08f), V(half * 2f, 0.14f, 0.16f), "bordure");
            p.Box("Chaussee", V(0f, -0.21f, (roadNear + roadFar) * 0.5f), V(half * 2f, 0.12f, road), "asphalte");
            p.Box("Bordure en face", V(0f, -0.075f, roadFar + 0.08f), V(half * 2f, 0.14f, 0.16f), "bordure");
            p.Box("Trottoir en face", V(0f, -0.07f, (roadFar + facade) * 0.5f), V(half * 2f, 0.12f, far), "trottoir");

            // Marquage : ligne médiane en tirets, passage piéton devant la porte.
            float mid = (roadNear + roadFar) * 0.5f;
            for (float x = -half + 1.5f; x < half - 1.5f; x += 6f)
            {
                p.Box("Tiret", V(x + 1.5f, -0.148f, mid), V(3f, 0.005f, 0.13f), "peinture_route");
            }

            for (int i = -3; i <= 3; i++)
            {
                p.Box("Passage pieton", V(i * 0.9f, -0.148f, mid), V(0.5f, 0.005f, road - 1.2f), "peinture_route");
            }

            // Caniveaux : une bande plus sombre le long des bordures.
            p.Box("Caniveau", V(0f, -0.148f, roadNear - 0.4f), V(half * 2f, 0.004f, 0.5f), "beton_brut");
            p.Box("Caniveau", V(0f, -0.148f, roadFar + 0.4f), V(half * 2f, 0.004f, 0.5f), "beton_brut");

            // --- les immeubles d'en face : une rangée de façades de largeurs et hauteurs variées
            float x0 = -half - 2f;
            int index = Pick(ShopNames.Length);
            while (x0 < half + 2f)
            {
                float width = Rand(6f, 10f);
                float height = Rand(8.5f, 13f);
                Building(p, V(x0 + width * 0.5f, 0f, facade), width, height, 180f, index++);
                x0 += width;
            }

            // --- les deux immeubles qui ferment la rue (vus en enfilade depuis les vitrines)
            for (int k = -1; k <= 1; k += 2)
            {
                float depth = -facade + 0.4f;
                Building(p, V(k * (half + 0.2f), 0f, facade * 0.5f), depth, Rand(9f, 12f), k < 0 ? 90f : -90f, index++);
            }

            // --- de ce côté : lampadaires, arbre, potelets, banc, poubelle, borne à incendie
            for (int k = -1; k <= 1; k += 2)
            {
                StreetLamp(p, V(k * (w * 0.5f + 2.4f), 0f, roadNear + 0.45f), k);
            }

            StreetTree(p, V(-(w * 0.5f + 5.2f), 0f, roadNear + 1.1f));
            StreetTree(p, V(w * 0.5f + 5.6f, 0f, roadFar - 1.2f));
            for (int i = 0; i < 4; i++)
            {
                float x = (i < 2 ? -1f : 1f) * (1.2f + (i % 2) * 1.3f);
                p.Cylinder("Potelet", V(x, 0.45f, roadNear + 0.3f), 0.05f, 0.9f, "metal_noir");
            }

            p.Box("Banc (assise)", V(w * 0.5f + 4.2f, 0.45f, -1.2f), V(1.6f, 0.06f, 0.42f), "bois");
            p.Box("Banc (dossier)", V(w * 0.5f + 4.2f, 0.75f, -1.0f), V(1.6f, 0.36f, 0.05f), "bois", V(-12f, 0f, 0f));
            p.Box("Banc (pied)", V(w * 0.5f + 3.6f, 0.22f, -1.15f), V(0.06f, 0.44f, 0.4f), "metal_noir");
            p.Box("Banc (pied)", V(w * 0.5f + 4.8f, 0.22f, -1.15f), V(0.06f, 0.44f, 0.4f), "metal_noir");
            p.Cylinder("Poubelle de rue", V(-(w * 0.5f + 2.9f), 0.45f, -1.6f), 0.24f, 0.9f, "vert");
            p.Cylinder("Borne a incendie", V(w * 0.5f + 1.4f, 0.35f, roadNear + 0.5f), 0.12f, 0.7f, "rouge");
            p.Sphere("Borne (chapeau)", V(w * 0.5f + 1.4f, 0.72f, roadNear + 0.5f), V(0.24f, 0.16f, 0.24f), "rouge");

            p.Group = group;
        }

        /// <summary>
        /// Un immeuble vu de face (sa façade regarde le −Z de son repère tourné de <paramref name="yaw"/>) :
        /// rez-de-chaussée commerçant (vitrine éclairée, store, enseigne), étages percés de fenêtres
        /// avec appuis et encadrements, corniche en haut.
        /// </summary>
        private static void Building(InteriorPlan p, Vector3 at, float width, float height, float yaw, int index)
        {
            string[] walls = { "facade_brique", "facade_enduit", "facade_enduit_bleu", "facade_beton", "facade_enduit_vert" };
            string[] awnings = { "store_rouge", "store_vert", "store_bleu" };
            string wall = walls[Mathf.Abs(index * 7 + 3) % walls.Length];

            p.Push(at, yaw);
            p.Box("Facade", V(0f, height * 0.5f - 0.2f, 0.3f), V(width, height + 0.4f, 0.6f), wall);
            p.Box("Corniche", V(0f, height - 0.1f, -0.08f), V(width, 0.25f, 0.3f), "encadrement");
            p.Box("Soubassement", V(0f, 0.25f, -0.03f), V(width, 0.5f, 0.1f), "facade_beton");

            // Rez-de-chaussée : une vitrine large, éclairée de l'intérieur, et son store.
            float shop = Mathf.Min(width - 1.6f, 5.2f);
            p.Box("Vitrine d'en face", V(0f, 1.7f, -0.02f), V(shop, 2.4f, 0.05f), "vitrine_en_face");
            p.Box("Cadre de vitrine", V(0f, 2.95f, -0.06f), V(shop + 0.2f, 0.12f, 0.1f), "metal_noir");
            p.Box("Cadre de vitrine", V(-shop * 0.5f - 0.05f, 1.7f, -0.06f), V(0.1f, 2.5f, 0.1f), "metal_noir");
            p.Box("Cadre de vitrine", V(shop * 0.5f + 0.05f, 1.7f, -0.06f), V(0.1f, 2.5f, 0.1f), "metal_noir");
            p.Box("Store banne", V(0f, 3.25f, -0.55f), V(shop + 0.4f, 0.05f, 1.2f), awnings[Mathf.Abs(index) % awnings.Length],
                V(-18f, 0f, 0f));
            p.Box("Bandeau d'enseigne", V(0f, 3.75f, -0.05f), V(shop, 0.55f, 0.08f), "noir");
            p.Neon(ShopNames[Mathf.Abs(index) % ShopNames.Length], V(0f, 3.62f, -0.1f), 0f, 0.3f,
                index % 3 == 0 ? "neon_rose" : index % 3 == 1 ? "neon_ambre" : "neon_cyan");

            // Étages : une rangée de fenêtres tous les 3 m.
            int columns = Mathf.Max(2, Mathf.FloorToInt((width - 1f) / 1.9f));
            float step = (width - 1f) / columns;
            for (float y = 5.4f; y < height - 1.3f; y += 3f)
            {
                for (int c = 0; c < columns; c++)
                {
                    float x = -width * 0.5f + 0.5f + step * (c + 0.5f);
                    p.Box("Fenetre", V(x, y, -0.01f), V(1f, 1.45f, 0.04f), "vitre_sombre");
                    p.Box("Encadrement", V(x, y + 0.78f, -0.05f), V(1.16f, 0.1f, 0.08f), "encadrement");
                    p.Box("Appui", V(x, y - 0.76f, -0.08f), V(1.2f, 0.08f, 0.16f), "encadrement");
                    p.Box("Meneau", V(x, y, -0.04f), V(0.05f, 1.45f, 0.05f), "encadrement");
                    if (Rand() < 0.35f) p.Box("Volet entrouvert", V(x - 0.62f, y, -0.12f), V(0.5f, 1.45f, 0.04f), "bois_fonce", V(0f, 25f, 0f));
                }
            }

            p.Pop();
        }

        private static void StreetLamp(InteriorPlan p, Vector3 at, int side)
        {
            p.Push(at, 0f);
            p.Cylinder("Mat de lampadaire", V(0f, 2.4f, 0f), 0.07f, 4.8f, "metal_noir");
            p.Cylinder("Socle de lampadaire", V(0f, 0.25f, 0f), 0.13f, 0.5f, "metal_noir");
            p.Box("Crosse", V(0f, 4.75f, -0.55f), V(0.06f, 0.06f, 1.1f), "metal_noir");
            p.Box("Lanterne", V(0f, 4.65f, -1.05f), V(0.36f, 0.14f, 0.55f), "metal_noir");
            p.Box("Verre de lanterne", V(0f, 4.57f, -1.05f), V(0.3f, 0.02f, 0.48f), "lampadaire");
            p.Light(V(0f, 4.4f, -1.05f), new Color(1f, 0.82f, 0.58f), 1.4f, 9f);
            p.Pop();
        }

        private static void StreetTree(InteriorPlan p, Vector3 at)
        {
            p.Box("Grille d'arbre", at + V(0f, -0.005f, 0f), V(1.2f, 0.02f, 1.2f), "metal_noir");
            p.Cylinder("Tronc", at + V(0f, 1.6f, 0f), 0.12f, 3.2f, "ecorce");
            p.Sphere("Houppier", at + V(0f, 3.9f, 0f), V(2.6f, 2.2f, 2.6f), "feuillage");
            p.Sphere("Houppier", at + V(0.7f, 3.4f, 0.4f), V(1.8f, 1.6f, 1.8f), "feuillage_clair");
            p.Sphere("Houppier", at + V(-0.6f, 3.5f, -0.5f), V(1.9f, 1.7f, 1.9f), "feuillage");
        }
    }
}
