using System;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Le décorateur : il dessine l'intérieur d'un lieu selon son métier — la pièce (sol,
    /// murs à soubassement, plinthes, corniche, vitrine à stores, porte vitrée), l'éclairage
    /// qui va avec (dalles lumineuses, suspensions, spots), puis les meubles et tout ce qui fait
    /// qu'on y croit : produits en rayon, bouteilles derrière le bar, affiches, plantes,
    /// poubelle, extincteur, horloge.
    ///
    /// Repère de la pièce : l'entrée au milieu du mur sud (z = 0), on entre vers +Z, le sol à
    /// y = 0. Les repères rendus : « arrivee », « porte » (le battant de sortie), « comptoir »
    /// (le centre de la zone d'achat) et « vendeur » (debout derrière, tourné vers la salle).
    ///
    /// Tout est tiré d'une graine : le même magasin est le même d'une construction à l'autre.
    /// </summary>
    public static partial class InteriorDesigner
    {
        private sealed class Style
        {
            public string Floor = "carrelage";
            public Color FloorColor = new Color(0.86f, 0.84f, 0.8f);
            public string Wall = "platre";
            public Color WallColor = new Color(0.86f, 0.82f, 0.74f);
            public string Wainscot;
            public Color WainscotColor = new Color(0.36f, 0.24f, 0.15f);
            public string Ceiling = "dalles";
            public Color CeilingColor = new Color(0.93f, 0.93f, 0.91f);
            public Color Trim = new Color(0.22f, 0.17f, 0.13f);
            public string Lighting = "dalles";
            public Color Light = new Color(1f, 0.93f, 0.82f);
            public float LightIntensity = 1.3f;
            public bool Storefront = true;
            public Color Neon = new Color(1f, 0.55f, 0.2f);
        }

        private static System.Random _rng;

        /// <summary>Le nom du lieu (une pizzeria et un diner sont tous deux des « Restaurant »).</summary>
        private static string _name = "";

        // Où va l'enseigne au néon : par défaut en haut du mur du fond ; un plan peut la déplacer.
        private static Vector3 _signAt;
        private static float _signYaw;
        private static float _signWidth;

        private static float Rand()
        {
            return (float)_rng.NextDouble();
        }

        private static float Rand(float min, float max)
        {
            return min + (max - min) * (float)_rng.NextDouble();
        }

        private static int Pick(int count)
        {
            return _rng.Next(count);
        }

        /// <summary>Les dimensions d'un lieu selon son métier (largeur, hauteur, profondeur).</summary>
        public static Vector3 SizeOf(string kind)
        {
            switch (kind)
            {
                case "Casino": return new Vector3(16f, 4.2f, 14f);
                case "SalleDeBoxe": return new Vector3(14f, 4.6f, 14f);
                case "StandDeTir": return new Vector3(10f, 3.4f, 18f);
                case "Restaurant": return new Vector3(12f, 3.3f, 11f);
                case "Bar":
                case "BoiteDeNuit": return new Vector3(11f, 3.3f, 10f);
                case "Arcade": return new Vector3(11f, 3.3f, 11f);
                case "Concession": return new Vector3(14f, 4.2f, 12f);
                case "Laverie": return new Vector3(8f, 3f, 10f);
                case "Motel": return new Vector3(7f, 3f, 6f);
                case "Avocat":
                case "Immobilier": return new Vector3(8f, 3.1f, 7f);
                case "Commissariat": return new Vector3(10f, 3.3f, 9f);
                case "Medecin": return new Vector3(9f, 3f, 9f);
                case "Cafe": return new Vector3(9f, 3.1f, 9f);
                default: return new Vector3(9f, 3.2f, 9f);
            }
        }

        /// <summary>Le plan complet d'un lieu de métier <paramref name="kind"/> (le nom d'un ShopKind).</summary>
        public static InteriorPlan Shop(string kind, string sign, int seed)
        {
            _rng = new System.Random(seed);
            _name = sign ?? "";
            InteriorPlan p = new InteriorPlan();
            p.Size = SizeOf(kind);
            Style style = StyleOf(kind);
            _signAt = new Vector3(0f, p.Size.y - 0.62f, p.Size.z - 0.03f);
            _signYaw = 0f;
            _signWidth = p.Size.x * 0.7f;

            Palette(p, style);
            Shell(p, style);
            Lighting(p, style);

            switch (kind)
            {
                case "Epicerie":
                case "Cave":
                    Grocery(p, kind == "Cave");
                    break;
                case "Pharmacie":
                    Pharmacy(p);
                    break;
                case "Restaurant":
                    if (Named("diner", "taco")) Diner(p);
                    else Trattoria(p);
                    break;
                case "Cafe":
                    Coffee(p);
                    break;
                case "Bar":
                case "BoiteDeNuit":
                    Bar(p, kind == "BoiteDeNuit");
                    break;
                case "Vetements":
                    Clothes(p);
                    break;
                case "Barbier":
                    Barber(p);
                    break;
                case "Tatoueur":
                    Tattoo(p);
                    break;
                case "Quincaillerie":
                    Hardware(p);
                    break;
                case "MarcheNoir":
                    BlackMarket(p);
                    break;
                case "Casino":
                    Casino(p);
                    break;
                case "Arcade":
                    ArcadeHall(p);
                    break;
                case "StandDeTir":
                    Range(p);
                    break;
                case "SalleDeBoxe":
                    Gym(p);
                    break;
                case "Laverie":
                    Laundry(p);
                    break;
                case "Poste":
                    PostOffice(p);
                    break;
                case "Medecin":
                    Clinic(p);
                    break;
                case "Concession":
                    Dealership(p);
                    break;
                case "PreteurSurGages":
                    Pawn(p);
                    break;
                case "Motel":
                    MotelLobby(p);
                    break;
                case "Commissariat":
                    Police(p);
                    break;
                default:
                    Office(p, kind);
                    break;
            }

            // L'enseigne, au-dessus du comptoir (ou là où le plan l'a mise).
            if (!string.IsNullOrEmpty(sign))
            {
                float letters = Mathf.Clamp(_signWidth / Mathf.Max(4, sign.Length) / 0.84f, 0.16f, 0.36f);
                p.Neon(sign, _signAt, _signYaw, letters, "neon");
            }

            return p;
        }

        /// <summary>Le nom du lieu contient-il l'un de ces mots (sans tenir compte de la casse) ?</summary>
        private static bool Named(params string[] words)
        {
            string name = _name.ToLowerInvariant();
            for (int i = 0; i < words.Length; i++)
            {
                if (name.Contains(words[i])) return true;
            }

            return false;
        }

        /// <summary>Déplace l'enseigne au néon (lisible depuis le −Z de son repère).</summary>
        private static void SignAt(Vector3 at, float yaw, float width)
        {
            _signAt = at;
            _signYaw = yaw;
            _signWidth = width;
        }

        // ================================================================== styles

        private static Style StyleOf(string kind)
        {
            Style s = new Style();
            switch (kind)
            {
                case "Epicerie":
                case "Cave":
                    s.Floor = "lino";
                    s.FloorColor = new Color(0.82f, 0.82f, 0.78f);
                    s.WallColor = new Color(0.9f, 0.89f, 0.84f);
                    s.Light = new Color(0.95f, 0.98f, 1f);
                    s.LightIntensity = 1.4f;
                    s.Neon = new Color(1f, 0.25f, 0.2f);
                    break;
                case "Pharmacie":
                case "Medecin":
                    s.Floor = "lino";
                    s.FloorColor = new Color(0.9f, 0.92f, 0.9f);
                    s.WallColor = new Color(0.94f, 0.95f, 0.94f);
                    s.Light = new Color(0.94f, 0.98f, 1f);
                    s.LightIntensity = 1.45f;
                    s.Neon = new Color(0.2f, 1f, 0.45f);
                    break;
                case "Restaurant":
                    s.Floor = "damier";
                    s.FloorColor = new Color(1f, 1f, 1f);
                    s.Wall = "papierpeint";
                    s.WallColor = new Color(0.86f, 0.5f, 0.35f);
                    s.Wainscot = "lambris";
                    s.WainscotColor = new Color(0.62f, 0.12f, 0.1f);
                    s.Ceiling = "platre";
                    s.Lighting = "suspensions";
                    s.Light = new Color(1f, 0.85f, 0.65f);
                    s.Neon = new Color(1f, 0.3f, 0.35f);
                    break;
                case "Cafe":
                    s.Floor = "parquet";
                    s.FloorColor = new Color(1f, 1f, 1f);
                    s.Wall = "brique";
                    s.WallColor = new Color(1f, 1f, 1f);
                    s.Ceiling = "platre";
                    s.CeilingColor = new Color(0.25f, 0.24f, 0.23f);
                    s.Lighting = "suspensions";
                    s.Light = new Color(1f, 0.82f, 0.58f);
                    s.Neon = new Color(1f, 0.7f, 0.35f);
                    break;
                case "Bar":
                case "BoiteDeNuit":
                    s.Floor = "parquet";
                    s.FloorColor = new Color(0.55f, 0.48f, 0.42f);
                    s.Wall = "brique";
                    s.WallColor = new Color(0.62f, 0.5f, 0.45f);
                    s.Wainscot = "lambris";
                    s.WainscotColor = new Color(0.25f, 0.15f, 0.09f);
                    s.Ceiling = "platre";
                    s.CeilingColor = new Color(0.12f, 0.11f, 0.1f);
                    s.Lighting = "suspensions";
                    s.Light = new Color(1f, 0.68f, 0.4f);
                    s.LightIntensity = 1.1f;
                    s.Neon = kind == "BoiteDeNuit" ? new Color(1f, 0.2f, 0.75f) : new Color(1f, 0.3f, 0.15f);
                    break;
                case "Vetements":
                    s.Floor = "parquet";
                    s.FloorColor = new Color(1.1f, 1.05f, 1f);
                    s.WallColor = new Color(0.93f, 0.92f, 0.9f);
                    s.Ceiling = "platre";
                    s.Lighting = "rails";
                    s.Light = new Color(1f, 0.94f, 0.86f);
                    s.Neon = new Color(1f, 1f, 1f);
                    break;
                case "Barbier":
                case "Tatoueur":
                    s.Floor = kind == "Barbier" ? "damier" : "beton";
                    s.FloorColor = kind == "Barbier" ? Color.white : new Color(0.5f, 0.5f, 0.52f);
                    s.Wall = kind == "Barbier" ? "carrelage" : "brique";
                    s.WallColor = kind == "Barbier" ? new Color(0.9f, 0.92f, 0.9f) : new Color(0.55f, 0.5f, 0.5f);
                    s.Wainscot = kind == "Barbier" ? "lambris" : null;
                    s.WainscotColor = new Color(0.16f, 0.24f, 0.2f);
                    s.Ceiling = "platre";
                    s.Lighting = kind == "Barbier" ? "suspensions" : "rails";
                    s.Light = new Color(1f, 0.88f, 0.72f);
                    s.Neon = kind == "Barbier" ? new Color(0.3f, 0.6f, 1f) : new Color(1f, 0.2f, 0.25f);
                    break;
                case "Quincaillerie":
                case "MarcheNoir":
                    s.Floor = "beton";
                    s.FloorColor = new Color(0.62f, 0.6f, 0.56f);
                    s.Wall = kind == "MarcheNoir" ? "brique" : "platre";
                    s.WallColor = kind == "MarcheNoir" ? new Color(0.4f, 0.35f, 0.32f) : new Color(0.84f, 0.82f, 0.72f);
                    s.Ceiling = kind == "MarcheNoir" ? "beton" : "dalles";
                    s.CeilingColor = kind == "MarcheNoir" ? new Color(0.3f, 0.3f, 0.3f) : s.CeilingColor;
                    s.Lighting = kind == "MarcheNoir" ? "baladeuses" : "dalles";
                    s.Light = kind == "MarcheNoir" ? new Color(1f, 0.74f, 0.45f) : new Color(1f, 0.96f, 0.88f);
                    s.LightIntensity = kind == "MarcheNoir" ? 0.9f : 1.35f;
                    s.Storefront = kind != "MarcheNoir";
                    s.Neon = new Color(1f, 0.8f, 0.2f);
                    break;
                case "Casino":
                    s.Floor = "moquette";
                    s.FloorColor = new Color(0.5f, 0.08f, 0.1f);
                    s.Wall = "papierpeint";
                    s.WallColor = new Color(0.32f, 0.1f, 0.14f);
                    s.Wainscot = "lambris";
                    s.WainscotColor = new Color(0.2f, 0.1f, 0.06f);
                    s.Ceiling = "platre";
                    s.CeilingColor = new Color(0.16f, 0.1f, 0.08f);
                    s.Lighting = "lustres";
                    s.Light = new Color(1f, 0.8f, 0.55f);
                    s.Storefront = false;
                    s.Neon = new Color(1f, 0.25f, 0.8f);
                    break;
                case "Arcade":
                    s.Floor = "moquette";
                    s.FloorColor = new Color(0.12f, 0.1f, 0.3f);
                    s.Wall = "platre";
                    s.WallColor = new Color(0.08f, 0.08f, 0.12f);
                    s.Ceiling = "platre";
                    s.CeilingColor = new Color(0.05f, 0.05f, 0.07f);
                    s.Lighting = "neons";
                    s.Light = new Color(0.55f, 0.7f, 1f);
                    s.LightIntensity = 0.9f;
                    s.Storefront = false;
                    s.Neon = new Color(0.2f, 0.9f, 1f);
                    break;
                case "StandDeTir":
                case "SalleDeBoxe":
                    s.Floor = kind == "SalleDeBoxe" ? "moquette" : "beton";
                    s.FloorColor = kind == "SalleDeBoxe" ? new Color(0.18f, 0.18f, 0.2f) : new Color(0.55f, 0.55f, 0.55f);
                    s.Wall = "brique";
                    s.WallColor = new Color(0.75f, 0.7f, 0.66f);
                    s.Ceiling = "beton";
                    s.CeilingColor = new Color(0.32f, 0.32f, 0.33f);
                    s.Lighting = "industriel";
                    s.Light = new Color(1f, 0.95f, 0.85f);
                    s.LightIntensity = 1.5f;
                    s.Storefront = false;
                    s.Neon = new Color(1f, 0.2f, 0.15f);
                    break;
                case "Laverie":
                    s.Floor = "carrelage";
                    s.FloorColor = new Color(0.8f, 0.86f, 0.9f);
                    s.WallColor = new Color(0.86f, 0.92f, 0.95f);
                    s.Light = new Color(0.94f, 0.98f, 1f);
                    s.Neon = new Color(0.3f, 0.7f, 1f);
                    break;
                case "Avocat":
                case "Immobilier":
                    s.Floor = "moquette";
                    s.FloorColor = new Color(0.22f, 0.26f, 0.34f);
                    s.Wall = kind == "Avocat" ? "lambris" : "platre";
                    s.WallColor = kind == "Avocat" ? new Color(0.5f, 0.34f, 0.2f) : new Color(0.9f, 0.89f, 0.86f);
                    s.Ceiling = "platre";
                    s.Lighting = kind == "Avocat" ? "suspensions" : "dalles";
                    s.Light = new Color(1f, 0.9f, 0.76f);
                    s.Neon = new Color(1f, 1f, 1f);
                    break;
                case "Motel":
                    s.Floor = "moquette";
                    s.FloorColor = new Color(0.36f, 0.28f, 0.2f);
                    s.Wall = "papierpeint";
                    s.WallColor = new Color(0.8f, 0.72f, 0.52f);
                    s.Wainscot = "lambris";
                    s.WainscotColor = new Color(0.42f, 0.28f, 0.16f);
                    s.Ceiling = "platre";
                    s.Lighting = "suspensions";
                    s.Light = new Color(1f, 0.84f, 0.62f);
                    s.Neon = new Color(1f, 0.3f, 0.3f);
                    break;
                case "Concession":
                    s.Floor = "carrelage";
                    s.FloorColor = new Color(0.95f, 0.95f, 0.95f);
                    s.WallColor = new Color(0.92f, 0.93f, 0.94f);
                    s.Ceiling = "platre";
                    s.Lighting = "rails";
                    s.Light = new Color(1f, 0.97f, 0.92f);
                    s.LightIntensity = 1.5f;
                    s.Neon = new Color(0.3f, 0.6f, 1f);
                    break;
                case "Commissariat":
                case "Poste":
                    s.Floor = "lino";
                    s.FloorColor = kind == "Poste" ? new Color(0.8f, 0.76f, 0.6f) : new Color(0.62f, 0.66f, 0.7f);
                    s.WallColor = kind == "Poste" ? new Color(0.92f, 0.88f, 0.72f) : new Color(0.78f, 0.82f, 0.86f);
                    s.Wainscot = "lambris";
                    s.WainscotColor = kind == "Poste" ? new Color(0.2f, 0.28f, 0.55f) : new Color(0.22f, 0.26f, 0.36f);
                    s.Light = new Color(0.96f, 0.98f, 1f);
                    s.Neon = kind == "Poste" ? new Color(1f, 0.85f, 0.2f) : new Color(0.3f, 0.5f, 1f);
                    break;
                case "PreteurSurGages":
                    s.Floor = "lino";
                    s.FloorColor = new Color(0.6f, 0.58f, 0.5f);
                    s.WallColor = new Color(0.78f, 0.7f, 0.46f);
                    s.Light = new Color(1f, 0.92f, 0.75f);
                    s.Neon = new Color(0.3f, 0.6f, 1f);
                    break;
            }

            return s;
        }

        private static void Palette(InteriorPlan p, Style s)
        {
            p.Define("sol", s.FloorColor, s.Floor == "moquette" ? 0.1f : s.Floor == "parquet" ? 0.55f : 0.5f, 0f, s.Floor);
            p.Define("mur", s.WallColor, 0.25f, 0f, s.Wall);
            p.Define("soubassement", s.WainscotColor, 0.45f, 0f, s.Wainscot ?? "lambris");
            p.Define("plafond", s.CeilingColor, 0.15f, 0f, s.Ceiling);
            p.Define("moulure", s.Trim, 0.4f, 0f, "bois");

            p.Define("bois", new Color(0.58f, 0.4f, 0.25f), 0.5f, 0f, "bois");
            p.Define("bois_clair", new Color(0.82f, 0.68f, 0.48f), 0.45f, 0f, "bois");
            p.Define("bois_fonce", new Color(0.26f, 0.16f, 0.1f), 0.55f, 0f, "bois");
            p.Define("stratifie", new Color(0.9f, 0.89f, 0.86f), 0.55f, 0f, null);
            p.Define("stratifie_fonce", new Color(0.14f, 0.14f, 0.15f), 0.65f, 0f, null);
            p.Define("metal", new Color(0.72f, 0.73f, 0.75f), 0.55f, 0.85f, "metal");
            p.Define("metal_noir", new Color(0.09f, 0.09f, 0.1f), 0.45f, 0.6f, "metal");
            p.Define("chrome", new Color(0.9f, 0.9f, 0.92f), 0.92f, 1f, null);
            p.Define("laiton", new Color(0.86f, 0.66f, 0.3f), 0.8f, 1f, null);
            p.Define("plastique", new Color(0.92f, 0.92f, 0.9f), 0.5f, 0f, null);
            p.Define("plastique_noir", new Color(0.05f, 0.05f, 0.055f), 0.55f, 0f, null);
            p.Define("cuir_rouge", new Color(0.55f, 0.07f, 0.06f), 0.55f, 0f, "cuir");
            p.Define("cuir_noir", new Color(0.07f, 0.065f, 0.06f), 0.5f, 0f, "cuir");
            p.Define("cuir_brun", new Color(0.36f, 0.2f, 0.1f), 0.5f, 0f, "cuir");
            p.Define("tissu", new Color(0.3f, 0.34f, 0.42f), 0.05f, 0f, "moquette");
            p.Define("tissu_rouge", new Color(0.5f, 0.1f, 0.12f), 0.05f, 0f, "moquette");
            p.Define("feutre", new Color(0.08f, 0.38f, 0.18f), 0.02f, 0f, "feutre");
            p.Define("papier", new Color(0.95f, 0.94f, 0.9f), 0.15f, 0f, null);
            p.Define("carton", new Color(0.62f, 0.46f, 0.3f), 0.1f, 0f, null);
            p.Define("beton_brut", new Color(0.55f, 0.55f, 0.55f), 0.25f, 0f, "beton");
            p.Define("caoutchouc", new Color(0.04f, 0.04f, 0.045f), 0.15f, 0f, null);
            p.Define("terre", new Color(0.18f, 0.12f, 0.08f), 0.05f, 0f, null);
            p.Define("feuillage", new Color(0.16f, 0.38f, 0.14f), 0.3f, 0f, null);
            p.Define("feuillage_clair", new Color(0.3f, 0.52f, 0.2f), 0.3f, 0f, null);
            p.Define("ceramique", new Color(0.9f, 0.88f, 0.84f), 0.75f, 0f, null);
            p.Define("rouge", new Color(0.75f, 0.08f, 0.06f), 0.5f, 0f, null);
            p.Define("jaune", new Color(0.95f, 0.75f, 0.1f), 0.5f, 0f, null);
            p.Define("bleu", new Color(0.1f, 0.25f, 0.6f), 0.5f, 0f, null);
            p.Define("vert", new Color(0.1f, 0.45f, 0.2f), 0.5f, 0f, null);
            p.Define("blanc", new Color(0.95f, 0.95f, 0.94f), 0.4f, 0f, null);
            p.Define("noir", new Color(0.03f, 0.03f, 0.035f), 0.3f, 0f, null);
            p.Define("gris", new Color(0.42f, 0.43f, 0.45f), 0.4f, 0f, null);
            p.Define("orange", new Color(0.95f, 0.45f, 0.08f), 0.5f, 0f, null);
            p.Define("rose", new Color(0.92f, 0.45f, 0.62f), 0.5f, 0f, null);
            p.Define("inox", new Color(0.78f, 0.79f, 0.8f), 0.75f, 0.9f, "metal");
            p.Define("marbre", new Color(0.92f, 0.91f, 0.88f), 0.9f, 0f, null);
            p.Define("marbre_noir", new Color(0.06f, 0.06f, 0.065f), 0.92f, 0f, null);
            p.Define("liege", new Color(0.66f, 0.5f, 0.32f), 0.05f, 0f, "moquette");
            p.Define("bouteille_verte", new Color(0.05f, 0.16f, 0.07f), 0.93f, 0f, null);
            p.Define("bouteille_brune", new Color(0.18f, 0.08f, 0.02f), 0.93f, 0f, null);
            p.Define("bouteille_claire", new Color(0.75f, 0.82f, 0.84f), 0.95f, 0f, null);
            p.Define("liquide_ambre", new Color(0.62f, 0.3f, 0.06f), 0.93f, 0f, null);
            p.Define("peau_mannequin", new Color(0.88f, 0.86f, 0.82f), 0.55f, 0f, null);
            p.Define("chromo", new Color(0.35f, 0.55f, 0.75f), 0.3f, 0f, null);
            p.Define("pelouse", new Color(0.3f, 0.55f, 0.22f), 0.2f, 0f, null);

            p.Glass("verre", new Color(0.82f, 0.9f, 0.92f, 0.22f));
            p.Glass("verre_fume", new Color(0.18f, 0.2f, 0.22f, 0.5f));
            p.Glass("miroir", new Color(0.92f, 0.94f, 0.96f, 1f), 1f).Metallic = 1f;
            p.Glow("vitrine_rue", new Color(0.05f, 0.06f, 0.08f), new Color(0.16f, 0.2f, 0.3f));
            p.Glow("lumiere", new Color(1f, 1f, 1f), s.Light * 2.2f);
            p.Glow("lumiere_chaude", new Color(1f, 0.9f, 0.7f), new Color(1f, 0.75f, 0.45f) * 2.4f);
            p.Glow("ecran", new Color(0.05f, 0.06f, 0.08f), new Color(0.3f, 0.55f, 0.95f) * 1.4f);
            p.Glow("ecran_vert", new Color(0.03f, 0.06f, 0.04f), new Color(0.25f, 0.9f, 0.4f) * 1.2f);
            p.Glow("sortie", new Color(0.1f, 0.5f, 0.2f), new Color(0.2f, 1f, 0.45f) * 1.6f);
            p.Glow("frigo", new Color(0.85f, 0.92f, 0.95f), new Color(0.75f, 0.88f, 1f) * 1.1f);
            p.Glow("neon", s.Neon, s.Neon * 3f);
            p.Glow("neon_rose", new Color(1f, 0.3f, 0.75f), new Color(1f, 0.25f, 0.7f) * 3f);
            p.Glow("neon_cyan", new Color(0.3f, 0.9f, 1f), new Color(0.2f, 0.85f, 1f) * 3f);
            p.Glow("neon_ambre", new Color(1f, 0.7f, 0.3f), new Color(1f, 0.6f, 0.2f) * 3f);
            p.Glow("neon_vert", new Color(0.3f, 1f, 0.45f), new Color(0.2f, 1f, 0.4f) * 3f);
            p.Glow("neon_rouge", new Color(1f, 0.2f, 0.15f), new Color(1f, 0.15f, 0.1f) * 3f);
            p.Glow("feu_rouge", new Color(0.6f, 0.02f, 0.02f), new Color(1f, 0.05f, 0.03f) * 1.5f);
            p.Glow("ecran_rose", new Color(0.06f, 0.03f, 0.05f), new Color(1f, 0.35f, 0.7f) * 1.3f);
            p.Glow("ecran_ambre", new Color(0.06f, 0.04f, 0.02f), new Color(1f, 0.6f, 0.2f) * 1.3f);
            p.Glow("ecran_tele", new Color(0.04f, 0.05f, 0.07f), new Color(0.45f, 0.55f, 0.75f) * 0.9f);
            p.Glow("vitre_cuisine", new Color(0.9f, 0.8f, 0.6f), new Color(1f, 0.82f, 0.55f) * 1.2f);

            // Les produits : douze teintes d'emballages, franches mais pas criardes.
            Color[] products =
            {
                new Color(0.72f, 0.1f, 0.08f), new Color(0.92f, 0.72f, 0.15f), new Color(0.1f, 0.18f, 0.45f),
                new Color(0.13f, 0.42f, 0.2f), new Color(0.92f, 0.92f, 0.9f), new Color(0.9f, 0.45f, 0.1f),
                new Color(0.4f, 0.25f, 0.12f), new Color(0.1f, 0.45f, 0.5f), new Color(0.08f, 0.08f, 0.09f),
                new Color(0.88f, 0.8f, 0.62f), new Color(0.45f, 0.65f, 0.85f), new Color(0.35f, 0.15f, 0.45f)
            };
            for (int i = 0; i < products.Length; i++) p.Define("produit_" + i, products[i], 0.45f, 0f);
        }

        private static string Product()
        {
            return "produit_" + Pick(12);
        }

        // ================================================================== la pièce

        private static void Shell(InteriorPlan p, Style s)
        {
            float w = p.Size.x, h = p.Size.y, d = p.Size.z;
            const float door = 1.2f, doorH = 2.25f;

            p.Group = "";
            p.Box("Sol", new Vector3(0f, -0.05f, d * 0.5f), new Vector3(w + 0.4f, 0.1f, d + 0.4f), "sol", true);
            p.Box("Plafond", new Vector3(0f, h + 0.05f, d * 0.5f), new Vector3(w + 0.4f, 0.1f, d + 0.4f), "plafond", true);
            p.Box("Mur du fond", new Vector3(0f, h * 0.5f, d + 0.1f), new Vector3(w + 0.4f, h, 0.2f), "mur", true);
            p.Box("Mur gauche", new Vector3(-w * 0.5f - 0.1f, h * 0.5f, d * 0.5f), new Vector3(0.2f, h, d), "mur", true);
            p.Box("Mur droit", new Vector3(w * 0.5f + 0.1f, h * 0.5f, d * 0.5f), new Vector3(0.2f, h, d), "mur", true);

            // Le mur de l'entrée : la porte au milieu, une vitrine de chaque côté (ou du mur plein).
            float side = (w - door) * 0.5f;
            for (int k = -1; k <= 1; k += 2)
            {
                float cx = k * (door * 0.5f + side * 0.5f);
                if (s.Storefront && side > 1.6f)
                {
                    float glass = Mathf.Min(side - 0.8f, 3.2f);
                    float gx = k * (door * 0.5f + 0.4f + glass * 0.5f);
                    float sill = 0.75f, top = 2.45f;
                    // Allège, linteau, et le plein qui reste de chaque côté de la vitre.
                    p.Box("Allege", new Vector3(cx, sill * 0.5f, -0.1f), new Vector3(side, sill, 0.2f), "mur", true);
                    p.Box("Imposte", new Vector3(cx, (top + h) * 0.5f, -0.1f), new Vector3(side, h - top, 0.2f), "mur", true);
                    float innerEdge = k * (door * 0.5f + 0.4f);
                    p.Box("Trumeau", new Vector3(k * (door * 0.5f + 0.2f), (sill + top) * 0.5f, -0.1f), new Vector3(0.4f, top - sill, 0.2f), "mur", true);
                    float outer = side - 0.4f - glass;
                    if (outer > 0.01f)
                    {
                        p.Box("Trumeau", new Vector3(k * (w * 0.5f - outer * 0.5f), (sill + top) * 0.5f, -0.1f), new Vector3(outer, top - sill, 0.2f), "mur", true);
                    }

                    // La vitre (la rue, de nuit, floue derrière) et ses montants.
                    p.Box("Vitrine (rue)", new Vector3(gx, (sill + top) * 0.5f, -0.16f), new Vector3(glass, top - sill, 0.02f), "vitrine_rue");
                    p.Box("Vitre", new Vector3(gx, (sill + top) * 0.5f, -0.06f), new Vector3(glass, top - sill, 0.02f), "verre", true);
                    p.Box("Montant", new Vector3(gx, (sill + top) * 0.5f, -0.04f), new Vector3(0.05f, top - sill, 0.06f), "metal_noir");
                    p.Box("Rebord", new Vector3(gx, sill + 0.02f, 0.06f), new Vector3(glass + 0.1f, 0.04f, 0.22f), "moulure");
                    Blinds(p, new Vector3(gx, top - 0.05f, 0.06f), glass, Rand(0.45f, 1.05f));
                    if (Rand() < 0.7f) Sticker(p, new Vector3(gx + Rand(-0.4f, 0.4f) * glass, Rand(1.3f, 1.8f), -0.03f));
                }
                else
                {
                    p.Box("Mur de l'entree", new Vector3(cx, h * 0.5f, -0.1f), new Vector3(side, h, 0.2f), "mur", true);
                }
            }

            p.Box("Dessus de porte", new Vector3(0f, (doorH + h) * 0.5f, -0.1f), new Vector3(door, h - doorH, 0.2f), "mur", true);

            // Soubassement, cimaise, plinthes, corniche.
            if (s.Wainscot != null)
            {
                const float wh = 1.05f;
                p.Box("Soubassement fond", new Vector3(0f, wh * 0.5f, d - 0.01f), new Vector3(w, wh, 0.03f), "soubassement");
                p.Box("Soubassement gauche", new Vector3(-w * 0.5f + 0.01f, wh * 0.5f, d * 0.5f), new Vector3(0.03f, wh, d), "soubassement");
                p.Box("Soubassement droit", new Vector3(w * 0.5f - 0.01f, wh * 0.5f, d * 0.5f), new Vector3(0.03f, wh, d), "soubassement");
                p.Box("Cimaise fond", new Vector3(0f, wh + 0.02f, d - 0.025f), new Vector3(w, 0.05f, 0.06f), "moulure");
                p.Box("Cimaise gauche", new Vector3(-w * 0.5f + 0.025f, wh + 0.02f, d * 0.5f), new Vector3(0.06f, 0.05f, d), "moulure");
                p.Box("Cimaise droite", new Vector3(w * 0.5f - 0.025f, wh + 0.02f, d * 0.5f), new Vector3(0.06f, 0.05f, d), "moulure");
            }

            p.Box("Plinthe fond", new Vector3(0f, 0.06f, d - 0.03f), new Vector3(w, 0.12f, 0.04f), "moulure");
            p.Box("Plinthe gauche", new Vector3(-w * 0.5f + 0.03f, 0.06f, d * 0.5f), new Vector3(0.04f, 0.12f, d), "moulure");
            p.Box("Plinthe droite", new Vector3(w * 0.5f - 0.03f, 0.06f, d * 0.5f), new Vector3(0.04f, 0.12f, d), "moulure");
            p.Box("Corniche fond", new Vector3(0f, h - 0.05f, d - 0.04f), new Vector3(w, 0.1f, 0.08f), "moulure");
            p.Box("Corniche gauche", new Vector3(-w * 0.5f + 0.04f, h - 0.05f, d * 0.5f), new Vector3(0.08f, 0.1f, d), "moulure");
            p.Box("Corniche droite", new Vector3(w * 0.5f - 0.04f, h - 0.05f, d * 0.5f), new Vector3(0.08f, 0.1f, d), "moulure");

            // La porte : chambranle, battant vitré (on sort par là), barre, paillasson, « SORTIE ».
            p.Box("Chambranle gauche", new Vector3(-door * 0.5f - 0.04f, doorH * 0.5f, 0.02f), new Vector3(0.08f, doorH, 0.24f), "metal_noir");
            p.Box("Chambranle droit", new Vector3(door * 0.5f + 0.04f, doorH * 0.5f, 0.02f), new Vector3(0.08f, doorH, 0.24f), "metal_noir");
            p.Box("Linteau", new Vector3(0f, doorH + 0.04f, 0.02f), new Vector3(door + 0.16f, 0.08f, 0.24f), "metal_noir");
            p.Group = "porte";
            p.Box("Battant", new Vector3(0f, doorH * 0.5f - 0.02f, 0.04f), new Vector3(door - 0.06f, doorH - 0.06f, 0.04f), "verre", true);
            p.Group = "";
            p.Box("Cadre du battant", new Vector3(0f, 0.08f, 0.04f), new Vector3(door - 0.06f, 0.16f, 0.05f), "metal_noir");
            p.Box("Cadre du battant", new Vector3(0f, doorH - 0.1f, 0.04f), new Vector3(door - 0.06f, 0.08f, 0.05f), "metal_noir");
            p.Box("Cadre du battant", new Vector3(-door * 0.5f + 0.06f, doorH * 0.5f, 0.04f), new Vector3(0.06f, doorH - 0.06f, 0.05f), "metal_noir");
            p.Box("Cadre du battant", new Vector3(door * 0.5f - 0.06f, doorH * 0.5f, 0.04f), new Vector3(0.06f, doorH - 0.06f, 0.05f), "metal_noir");
            p.Cylinder("Barre", new Vector3(0f, 1.05f, 0.12f), 0.018f, door - 0.3f, "chrome", new Vector3(0f, 0f, 90f));
            p.Box("Paillasson", new Vector3(0f, 0.008f, 0.75f), new Vector3(1.4f, 0.016f, 0.9f), "caoutchouc");
            p.Box("Panneau sortie", new Vector3(0f, doorH + 0.32f, 0.06f), new Vector3(0.36f, 0.14f, 0.05f), "sortie");
            p.Mark("porte", new Vector3(0f, 0f, 0.05f), 0f);
            p.Mark("arrivee", new Vector3(0f, 0.05f, 1.5f), 0f);
        }

        /// <summary>Un store vénitien : des lamelles fines jusqu'à la hauteur <paramref name="drop"/>.</summary>
        private static void Blinds(InteriorPlan p, Vector3 top, float width, float drop)
        {
            p.Box("Caisson de store", top + new Vector3(0f, 0.03f, 0f), new Vector3(width + 0.06f, 0.06f, 0.06f), "plastique");
            int slats = Mathf.Max(3, Mathf.RoundToInt(drop / 0.05f));
            for (int i = 0; i < slats; i++)
            {
                p.Box("Lamelle", top + new Vector3(0f, -0.04f - i * 0.05f, 0f), new Vector3(width, 0.004f, 0.045f), "plastique",
                    new Vector3(25f, 0f, 0f));
            }

            p.Box("Barre de store", top + new Vector3(0f, -0.06f - slats * 0.05f, 0f), new Vector3(width, 0.02f, 0.05f), "plastique");
        }

        /// <summary>Un autocollant sur la vitrine (horaires, carte bancaire acceptée…).</summary>
        private static void Sticker(InteriorPlan p, Vector3 at)
        {
            p.Box("Autocollant", at, new Vector3(Rand(0.14f, 0.3f), Rand(0.1f, 0.22f), 0.004f), Pick(3) == 0 ? "blanc" : Product());
        }

        // ================================================================== éclairage

        private static void Lighting(InteriorPlan p, Style s)
        {
            float w = p.Size.x, h = p.Size.y, d = p.Size.z;
            int cols = Mathf.Max(1, Mathf.RoundToInt(w / 3.6f));
            int rows = Mathf.Max(1, Mathf.RoundToInt(d / 3.6f));

            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    Vector3 at = new Vector3(-w * 0.5f + w * (c + 0.5f) / cols, h, d * (r + 0.5f) / rows);
                    switch (s.Lighting)
                    {
                        case "suspensions":
                        case "lustres":
                            Pendant(p, at, s, s.Lighting == "lustres");
                            break;
                        case "rails":
                            if (c != 0) break;
                            Track(p, new Vector3(0f, h, at.z), w * 0.8f, s);
                            p.Light(new Vector3(0f, h - 0.7f, at.z), s.Light, s.LightIntensity * 0.75f, Mathf.Max(6.5f, w * 0.6f));
                            break;
                        case "baladeuses":
                            Bulb(p, at + new Vector3(Rand(-0.6f, 0.6f), 0f, Rand(-0.6f, 0.6f)), s);
                            break;
                        case "industriel":
                            Industrial(p, at, s);
                            break;
                        case "neons":
                            TubeLight(p, at, s);
                            break;
                        default:
                            Troffer(p, at, s);
                            break;
                    }
                }
            }
        }

        /// <summary>Une dalle lumineuse encastrée (60 × 120), son cadre, sa lumière.</summary>
        private static void Troffer(InteriorPlan p, Vector3 at, Style s)
        {
            p.Box("Dalle lumineuse", at + new Vector3(0f, -0.012f, 0f), new Vector3(0.6f, 0.02f, 1.2f), "lumiere");
            p.Box("Cadre de dalle", at + new Vector3(0f, -0.004f, 0f), new Vector3(0.66f, 0.01f, 1.26f), "metal");
            p.Light(at + new Vector3(0f, -0.3f, 0f), s.Light, s.LightIntensity, 7.5f);
        }

        /// <summary>Une suspension : le fil, l'abat-jour, l'ampoule ; un lustre a ses bras de laiton.</summary>
        private static void Pendant(InteriorPlan p, Vector3 at, Style s, bool chandelier)
        {
            float drop = chandelier ? 0.9f : Rand(0.75f, 1.05f);
            p.Cylinder("Fil", at + new Vector3(0f, -drop * 0.5f, 0f), 0.006f, drop, "noir");
            Vector3 lamp = at + new Vector3(0f, -drop, 0f);
            if (chandelier)
            {
                p.Cylinder("Lustre", lamp, 0.05f, 0.25f, "laiton");
                for (int i = 0; i < 6; i++)
                {
                    float a = i * 60f * Mathf.Deg2Rad;
                    Vector3 arm = new Vector3(Mathf.Cos(a) * 0.34f, -0.08f, Mathf.Sin(a) * 0.34f);
                    p.Cylinder("Bras", lamp + arm * 0.5f, 0.01f, 0.36f, "laiton", new Vector3(0f, -i * 60f, 90f));
                    p.Sphere("Bougie", lamp + arm + new Vector3(0f, 0.06f, 0f), new Vector3(0.05f, 0.09f, 0.05f), "lumiere_chaude");
                }
            }
            else
            {
                p.Cylinder("Abat-jour", lamp + new Vector3(0f, 0.04f, 0f), 0.2f, 0.18f, "metal_noir");
                p.Sphere("Ampoule", lamp - new Vector3(0f, 0.06f, 0f), new Vector3(0.1f, 0.1f, 0.1f), "lumiere_chaude");
            }

            p.Light(lamp - new Vector3(0f, 0.15f, 0f), s.Light, s.LightIntensity * (chandelier ? 1.2f : 1f), 6.5f);
        }

        /// <summary>Un rail de spots au plafond, sur toute la largeur.</summary>
        private static void Track(InteriorPlan p, Vector3 at, float length, Style s)
        {
            p.Box("Rail", at + new Vector3(0f, -0.03f, 0f), new Vector3(length, 0.04f, 0.05f), "metal_noir");
            int spots = Mathf.Max(2, Mathf.RoundToInt(length / 1.5f));
            for (int i = 0; i < spots; i++)
            {
                float x = -length * 0.5f + length * (i + 0.5f) / spots;
                float tilt = (i % 2 == 0 ? 1f : -1f) * 22f;
                Vector3 spot = at + new Vector3(x, -0.12f, 0f);
                p.Cylinder("Spot", spot, 0.045f, 0.14f, "metal_noir", new Vector3(tilt, 0f, 0f));
                p.Spot(spot, new Vector3(90f + tilt, 0f, 0f), s.Light, s.LightIntensity * 2.2f, 7f, 60f);
            }
        }

        private static void Bulb(InteriorPlan p, Vector3 at, Style s)
        {
            float drop = Rand(0.5f, 0.9f);
            p.Cylinder("Fil", at + new Vector3(0f, -drop * 0.5f, 0f), 0.005f, drop, "noir");
            p.Sphere("Ampoule nue", at + new Vector3(0f, -drop - 0.05f, 0f), new Vector3(0.07f, 0.1f, 0.07f), "lumiere_chaude");
            p.Light(at + new Vector3(0f, -drop - 0.1f, 0f), s.Light, s.LightIntensity, 5.5f);
        }

        private static void Industrial(InteriorPlan p, Vector3 at, Style s)
        {
            p.Cylinder("Tige", at + new Vector3(0f, -0.3f, 0f), 0.012f, 0.6f, "metal_noir");
            p.Cylinder("Cloche", at + new Vector3(0f, -0.68f, 0f), 0.3f, 0.22f, "metal");
            p.Cylinder("Diffuseur", at + new Vector3(0f, -0.8f, 0f), 0.24f, 0.02f, "lumiere");
            p.Light(at + new Vector3(0f, -1f, 0f), s.Light, s.LightIntensity, 8f);
        }

        private static void TubeLight(InteriorPlan p, Vector3 at, Style s)
        {
            string tube = Pick(2) == 0 ? "neon_rose" : "neon_cyan";
            p.Cylinder("Tube", at + new Vector3(0f, -0.08f, 0f), 0.02f, 1.6f, tube, new Vector3(0f, 0f, 90f));
            Color c = tube == "neon_rose" ? new Color(1f, 0.3f, 0.75f) : new Color(0.3f, 0.85f, 1f);
            p.Light(at + new Vector3(0f, -0.3f, 0f), c, s.LightIntensity, 6.5f);
        }
    }
}
