using UnityEditor;
using UnityEngine;
using Random = UnityEngine.Random;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Textures et matériaux de la rue de nuit.
    ///
    /// Séparé de BuildMaterials parce que ce sont deux problèmes différents : BuildMaterials
    /// habille des combattants (peau, tissu, cuir), ici on habille un lieu — et un lieu de nuit
    /// se lit presque entièrement à ses SOURCES lumineuses, pas à ses surfaces. D'où trois
    /// familles de matériaux qu'un décor de jour n'a pas :
    ///
    ///   - les néons, non éclairés et au-dessus de 1, pour que le bloom les fasse déborder ;
    ///   - les volumes de lumière additifs, qui rendent l'air visible entre la lampe et le sol ;
    ///   - le bitume mouillé, qui reflète réellement tout ce qui précède.
    ///
    /// Les textures sont générées et stockées en assets : aucun fichier binaire à versionner,
    /// et le code qui les fabrique documente à quoi elles ressemblent.
    /// </summary>
    public static class NightMaterialFactory
    {
        public const string MaterialsFolder = "Assets/UberBagarre/Art/Materials";
        public const string TexturesFolder = "Assets/UberBagarre/Art/Textures";

        public const string NeonShaderName = "UberBagarre/Neon";
        public const string GlowShaderName = "UberBagarre/Glow";
        public const string WetGroundShaderName = "UberBagarre/WetGround";
        public const string RainShaderName = "UberBagarre/Rain";

        // ------------------------------------------------------------------ palette

        public class Palette
        {
            public Material WetAsphalt;
            public Material Sidewalk;
            public Material Curb;
            public Material RoadPaint;
            public Material Brick;
            public Material DarkBrick;
            public Material Concrete;
            public Material DarkConcrete;
            public Material Metal;
            public Material DarkMetal;
            public Material Chrome;
            public Material Rust;
            public Material Glass;
            public Material Wood;
            public Material Rubber;
            public Material Carpet;
            public Material Velvet;
            public Material Plastic;
            public Material CarBody;
            public Material CarRoof;

            public Material FacadeLit;
            public Material FacadeLitCool;

            public Material NeonMagenta;
            public Material NeonCyan;
            public Material NeonWarm;
            public Material NeonRed;
            public Material NeonGreen;
            public Material NeonBlue;
            public Material NeonWhite;

            public Material GlowWarm;
            public Material GlowMagenta;
            public Material GlowCyan;
            public Material GlowWhite;
            public Material GlowRed;

            public Material Headlight;
            public Material Taillight;
        }

        /// <summary>
        /// Construit toute la palette. Les deux dimensions sont celles du SOL : le tiling des
        /// textures de chaussee, de trottoir et de flaques s'y cale, pour que le grain garde
        /// la meme taille au metre dans les deux directions.
        /// </summary>
        public static Palette Create(float groundLength, float groundDepth)
        {
            Palette p = new Palette();

            // --------------------------------------------------------- textures
            Texture2D asphalt = EditorBuildUtility.CreateOrUpdateAsphaltTexture(TexturesFolder, "T_Asphalte", 256);
            Texture2D puddles = CreatePuddleMask(TexturesFolder, "T_Flaques", 256, 0.46f, 9182);

            Texture2D slabs = CreateSlabTexture(TexturesFolder, "T_Dalles", 256, 4,
                new Color(0.40f, 0.40f, 0.39f), new Color(0.26f, 0.26f, 0.26f), 4451);

            Texture2D brickNight = EditorBuildUtility.CreateOrUpdateBrickTexture(TexturesFolder, "T_BriqueNuit", 256, 14,
                new Color(0.20f, 0.19f, 0.19f), new Color(0.31f, 0.20f, 0.18f));

            Texture2D darkBrick = EditorBuildUtility.CreateOrUpdateBrickTexture(TexturesFolder, "T_BriqueSombre", 256, 12,
                new Color(0.12f, 0.12f, 0.13f), new Color(0.17f, 0.15f, 0.16f));

            Texture2D concreteGrain = EditorBuildUtility.CreateOrUpdateGrainTexture(TexturesFolder, "T_BetonNuit", 256,
                new Color(0.42f, 0.42f, 0.42f), 0.10f, 0.05f, 0.02f, 0.05f, 6161);

            Texture2D rustGrain = EditorBuildUtility.CreateOrUpdateGrainTexture(TexturesFolder, "T_Rouille", 256,
                new Color(0.32f, 0.18f, 0.11f), 0.22f, 0.09f, 0.03f, 0.09f, 3311);

            Texture2D metalGrain = EditorBuildUtility.CreateOrUpdateGrainTexture(TexturesFolder, "T_MetalBrosse", 256,
                new Color(0.48f, 0.49f, 0.52f), 0.05f, 0.04f, 0.02f, 0.14f, 8080);

            Texture2D windowsAlbedo;
            Texture2D windowsEmission;
            CreateWindowGrid(TexturesFolder, "T_FenetresChaudes", 256, 6, 8, 0.42f,
                new Color(1f, 0.78f, 0.45f), 7331, out windowsAlbedo, out windowsEmission);

            Texture2D windowsCoolAlbedo;
            Texture2D windowsCoolEmission;
            CreateWindowGrid(TexturesFolder, "T_FenetresFroides", 256, 5, 7, 0.30f,
                new Color(0.62f, 0.80f, 1f), 1279, out windowsCoolAlbedo, out windowsCoolEmission);

            // --------------------------------------------------------- surfaces
            //
            // Le tiling du bitume est calé sur les dimensions réelles de la rue : sans ça, un
            // sol de 90 m étire un grain de 256 px sur toute sa longueur et redevient un aplat.
            p.WetAsphalt = CreateWetGround(MaterialsFolder, "M_BitumeMouille", asphalt,
                new Vector2(groundLength * 0.28f, groundDepth * 0.28f), puddles,
                new Vector2(groundLength * 0.055f, groundDepth * 0.055f), 0.85f);

            p.Sidewalk = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Trottoir",
                new Color(0.62f, 0.62f, 0.62f), 0.22f, 0f, slabs, new Vector2(groundLength * 0.25f, 2f));

            p.Curb = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Bordure",
                new Color(0.70f, 0.69f, 0.66f), 0.16f, 0f, concreteGrain, new Vector2(groundLength * 0.5f, 1f));

            p.RoadPaint = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_MarquageSol",
                new Color(0.78f, 0.76f, 0.70f), 0.28f, 0f);

            p.Brick = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_BriqueNuit",
                Color.white, 0.07f, 0f, brickNight, new Vector2(8f, 5f));

            p.DarkBrick = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_BriqueSombre",
                Color.white, 0.10f, 0f, darkBrick, new Vector2(9f, 6f));

            p.Concrete = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_BetonNuit",
                Color.white, 0.12f, 0f, concreteGrain, new Vector2(4f, 4f));

            p.DarkConcrete = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_BetonSombre",
                new Color(0.34f, 0.34f, 0.35f), 0.14f, 0f, concreteGrain, new Vector2(4f, 4f));

            p.Metal = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_MetalNuit",
                Color.white, 0.62f, 0.85f, metalGrain, new Vector2(3f, 3f));

            p.DarkMetal = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_MetalNoir",
                new Color(0.10f, 0.10f, 0.11f), 0.48f, 0.75f, metalGrain, new Vector2(3f, 3f));

            // Le chrome est le seul matériau de la scène qui renvoie franchement les néons :
            // sans une ou deux surfaces vraiment spéculaires, la couleur des enseignes reste
            // confinée aux enseignes elles-mêmes.
            p.Chrome = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Chrome",
                new Color(0.86f, 0.87f, 0.90f), 0.93f, 1f);

            p.Rust = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_RouilleNuit",
                Color.white, 0.14f, 0.15f, rustGrain, new Vector2(3f, 3f));

            p.Glass = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_VitreNuit",
                new Color(0.035f, 0.045f, 0.06f), 0.92f, 0.35f);

            p.Wood = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_BoisNuit",
                new Color(0.26f, 0.19f, 0.13f), 0.10f, 0f);

            p.Rubber = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Caoutchouc",
                new Color(0.045f, 0.045f, 0.05f), 0.07f, 0f);

            p.Carpet = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_TapisRouge",
                new Color(0.34f, 0.045f, 0.055f), 0.06f, 0f);

            p.Velvet = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Velours",
                new Color(0.28f, 0.035f, 0.09f), 0.05f, 0f);

            p.Plastic = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Plastique",
                new Color(0.55f, 0.24f, 0.06f), 0.40f, 0f);

            p.CarBody = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_CarrosserieUsee",
                new Color(0.30f, 0.33f, 0.31f), 0.36f, 0.25f, rustGrain, new Vector2(2.5f, 2.5f));

            p.CarRoof = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_CarrosserieToit",
                new Color(0.22f, 0.25f, 0.24f), 0.30f, 0.25f, rustGrain, new Vector2(2.5f, 2.5f));

            // --------------------------------------------------------- façades habitées
            p.FacadeLit = CreateEmissive(MaterialsFolder, "M_FacadeHabitee", Color.white, windowsAlbedo,
                new Vector2(3f, 4f), new Color(1f, 0.74f, 0.42f) * 2.6f, windowsEmission, 0.18f, 0f);

            p.FacadeLitCool = CreateEmissive(MaterialsFolder, "M_FacadeHabiteeFroide", Color.white, windowsCoolAlbedo,
                new Vector2(3f, 4f), new Color(0.58f, 0.78f, 1f) * 2.2f, windowsCoolEmission, 0.18f, 0f);

            // --------------------------------------------------------- néons
            p.NeonMagenta = CreateNeon(MaterialsFolder, "M_NeonMagenta", new Color(1f, 0.16f, 0.62f), 7.5f);
            p.NeonCyan = CreateNeon(MaterialsFolder, "M_NeonCyan", new Color(0.18f, 0.92f, 1f), 6.5f);
            p.NeonWarm = CreateNeon(MaterialsFolder, "M_NeonAmbre", new Color(1f, 0.66f, 0.26f), 5.5f);
            p.NeonRed = CreateNeon(MaterialsFolder, "M_NeonRouge", new Color(1f, 0.12f, 0.14f), 6f);
            p.NeonGreen = CreateNeon(MaterialsFolder, "M_NeonVert", new Color(0.32f, 1f, 0.42f), 5.5f);
            p.NeonBlue = CreateNeon(MaterialsFolder, "M_NeonBleu", new Color(0.26f, 0.42f, 1f), 6f);
            p.NeonWhite = CreateNeon(MaterialsFolder, "M_NeonBlanc", new Color(1f, 0.96f, 0.90f), 5f);

            // --------------------------------------------------------- volumes lumineux
            p.GlowWarm = CreateGlow(MaterialsFolder, "M_HaloAmbre", new Color(1f, 0.70f, 0.34f, 0.16f), 1.0f, 2.6f, 0.85f, false);
            p.GlowMagenta = CreateGlow(MaterialsFolder, "M_HaloMagenta", new Color(1f, 0.22f, 0.66f, 0.13f), 1.0f, 2.4f, 0.7f, false);
            p.GlowCyan = CreateGlow(MaterialsFolder, "M_HaloCyan", new Color(0.26f, 0.9f, 1f, 0.12f), 1.0f, 2.4f, 0.7f, false);
            p.GlowWhite = CreateGlow(MaterialsFolder, "M_HaloBlanc", new Color(1f, 0.95f, 0.88f, 0.14f), 1.0f, 2.8f, 0.8f, false);
            p.GlowRed = CreateGlow(MaterialsFolder, "M_HaloRouge", new Color(1f, 0.18f, 0.16f, 0.14f), 1.0f, 2.4f, 0.6f, false);

            p.Headlight = CreateNeon(MaterialsFolder, "M_Phare", new Color(1f, 0.94f, 0.82f), 9f);
            p.Taillight = CreateNeon(MaterialsFolder, "M_Feu", new Color(1f, 0.12f, 0.10f), 4.5f);

            return p;
        }

        // ------------------------------------------------------------------ matériaux spéciaux

        /// <summary>
        /// Matériau de néon. Si le shader manque (projet pas encore réimporté, pipeline
        /// exotique), on retombe sur un Standard émissif : moins beau, mais jamais rose fluo.
        /// </summary>
        public static Material CreateNeon(string folder, string name, Color color, float intensity)
        {
            Shader shader = Shader.Find(NeonShaderName);

            if (shader == null)
            {
                return CreateEmissive(folder, name, color * 0.6f, null, Vector2.one, color * intensity, null, 0.3f, 0f);
            }

            Material material = LoadOrCreate(folder, name, shader);
            SetColor(material, "_Color", color);
            SetFloat(material, "_Intensity", intensity);
            SetFloat(material, "_CoreBoost", 0.55f);
            SetFloat(material, "_RimPower", 2.4f);

            EditorUtility.SetDirty(material);
            return material;
        }

        public static Material CreateGlow(string folder, string name, Color color, float intensity,
            float edgeSoftness, float topFade, bool flip)
        {
            Shader shader = Shader.Find(GlowShaderName);
            if (shader == null) return null;

            Material material = LoadOrCreate(folder, name, shader);
            SetColor(material, "_Color", color);
            SetFloat(material, "_Intensity", intensity);
            SetFloat(material, "_EdgeSoftness", edgeSoftness);
            SetFloat(material, "_TopFade", topFade);
            SetFloat(material, "_Flip", flip ? 1f : 0f);

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Matériau de bruine. Renvoie null si le shader manque : mieux vaut aucune pluie
        /// qu'une pluie de rectangles blancs opaques.
        /// </summary>
        public static Material CreateRain(string folder, string name, Color color, float intensity)
        {
            Shader shader = Shader.Find(RainShaderName);
            if (shader == null) return null;

            Material material = LoadOrCreate(folder, name, shader);
            SetColor(material, "_Color", color);
            SetFloat(material, "_Intensity", intensity);
            SetFloat(material, "_Sharpness", 2.4f);

            EditorUtility.SetDirty(material);
            return material;
        }

        public static Material CreateWetGround(string folder, string name, Texture2D albedo, Vector2 albedoTiling,
            Texture2D wetMask, Vector2 wetTiling, float wetLevel)
        {
            Shader shader = Shader.Find(WetGroundShaderName);

            if (shader == null)
            {
                // Sans le shader, au moins un bitume très lisse : il attrapera les reflets
                // spéculaires des lampes, ce qui reste bien mieux qu'un sol mat.
                return EditorBuildUtility.CreateOrUpdateMaterial(folder, name, Color.white, 0.62f, 0f,
                    albedo, albedoTiling);
            }

            Material material = LoadOrCreate(folder, name, shader);

            SetColor(material, "_Color", Color.white);
            SetTexture(material, "_MainTex", albedo);
            material.SetTextureScale("_MainTex", albedoTiling);

            SetTexture(material, "_WetMask", wetMask);
            if (material.HasProperty("_WetMask")) material.SetTextureScale("_WetMask", wetTiling);

            SetFloat(material, "_WetLevel", wetLevel);
            SetFloat(material, "_WetDarken", 0.58f);
            SetFloat(material, "_Glossiness", 0.13f);
            SetFloat(material, "_Metallic", 0f);
            SetFloat(material, "_ReflectionStrength", 1f);
            SetColor(material, "_ReflectionTint", new Color(0.92f, 0.94f, 1f));
            SetFloat(material, "_FresnelPower", 3.2f);
            SetFloat(material, "_FresnelBase", 0.12f);
            SetFloat(material, "_RippleStrength", 0.010f);
            SetFloat(material, "_RippleScale", 1.6f);
            SetFloat(material, "_RippleSpeed", 0.35f);

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Matériau Standard avec émission. Utilisé pour les façades habitées : leurs fenêtres
        /// allumées doivent éclairer sans qu'on place une lampe par appartement.
        /// </summary>
        public static Material CreateEmissive(string folder, string name, Color albedoColor, Texture2D albedo,
            Vector2 tiling, Color emission, Texture2D emissionMap, float smoothness, float metallic)
        {
            Material material = EditorBuildUtility.CreateOrUpdateMaterial(folder, name, albedoColor,
                smoothness, metallic, albedo, tiling);

            // Sans le mot-clé, la couleur d'émission est stockée mais jamais lue : le
            // matériau paraît correct dans l'inspecteur et reste noir à l'écran.
            material.EnableKeyword("_EMISSION");
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;

            SetColor(material, "_EmissionColor", emission);

            if (emissionMap != null)
            {
                SetTexture(material, "_EmissionMap", emissionMap);
                if (material.HasProperty("_EmissionMap")) material.SetTextureScale("_EmissionMap", tiling);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        private static Material LoadOrCreate(string folder, string name, Shader shader)
        {
            EditorBuildUtility.EnsureFolder(folder);
            string path = folder + "/" + name + ".mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            return material;
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloat(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetTexture(Material material, string property, Texture texture)
        {
            if (texture != null && material.HasProperty(property)) material.SetTexture(property, texture);
        }

        // ------------------------------------------------------------------ textures

        /// <summary>
        /// Masque de flaques : des taches douces, pas un bruit uniforme.
        ///
        /// Une chaussée mouillée partout ressemble à une patinoire ; une chaussée sèche
        /// partout n'a aucun reflet. Ce sont les FLAQUES, c'est-à-dire l'alternance, qui
        /// se lisent comme de l'eau. Deux échelles de bruit : les grandes décident où est
        /// la flaque, les petites en découpent le bord pour qu'il ne soit pas circulaire.
        /// </summary>
        public static Texture2D CreatePuddleMask(string folder, string name, int size, float coverage, int seed)
        {
            EditorBuildUtility.EnsureFolder(folder);
            string path = folder + "/" + name + ".asset";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = texture == null;
            if (isNew) texture = new Texture2D(size, size, TextureFormat.RGBA32, true);

            Color[] pixels = new Color[size * size];
            float offset = seed % 131 * 5.3f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float u = (float)x / size;
                    float v = (float)y / size;

                    float broad = Mathf.PerlinNoise(offset + u * 3.5f, offset + v * 3.5f);
                    float detail = Mathf.PerlinNoise(offset + 41f + u * 13f, offset + 41f + v * 13f);

                    float value = broad * 0.78f + detail * 0.22f;

                    // Le seuil doux évite un bord net : une flaque réelle s'amincit sur
                    // plusieurs centimètres avant de disparaître.
                    float wet = Mathf.SmoothStep(0f, 1f, Mathf.InverseLerp(1f - coverage - 0.12f, 1f - coverage + 0.16f, value));

                    pixels[y * size + x] = new Color(wet, wet, wet, 1f);
                }
            }

            return Store(texture, pixels, path, isNew);
        }

        /// <summary>Dalles de trottoir : un grain, plus des joints creusés.</summary>
        public static Texture2D CreateSlabTexture(string folder, string name, int size, int cells,
            Color slab, Color joint, int seed)
        {
            EditorBuildUtility.EnsureFolder(folder);
            string path = folder + "/" + name + ".asset";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = texture == null;
            if (isNew) texture = new Texture2D(size, size, TextureFormat.RGBA32, true);

            Random.State previous = Random.state;
            Random.InitState(seed);

            int cell = Mathf.Max(8, size / Mathf.Max(1, cells));
            int jointWidth = Mathf.Max(1, cell / 16);

            // Une valeur propre par dalle : sans ça, toutes les dalles sont identiques et
            // la texture se lit comme une grille dessinée, pas comme du béton posé.
            float[] tint = new float[cells * cells + cells + 1];
            for (int i = 0; i < tint.Length; i++) tint[i] = Random.Range(-0.055f, 0.055f);

            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = x / cell;
                    int cy = y / cell;
                    int index = (cy * cells + cx) % tint.Length;
                    if (index < 0) index = 0;

                    int inX = x % cell;
                    int inY = y % cell;

                    bool isJoint = inX < jointWidth || inY < jointWidth;

                    float grain = (Mathf.PerlinNoise(x * 0.25f, y * 0.25f) - 0.5f) * 0.06f;
                    float speck = Random.value < 0.03f ? -0.05f : 0f;

                    Color color = isJoint ? joint : slab;
                    color.r = Mathf.Clamp01(color.r + tint[index] + grain + speck);
                    color.g = Mathf.Clamp01(color.g + tint[index] + grain + speck);
                    color.b = Mathf.Clamp01(color.b + tint[index] + grain + speck);
                    color.a = 1f;

                    pixels[y * size + x] = color;
                }
            }

            Random.state = previous;
            return Store(texture, pixels, path, isNew);
        }

        /// <summary>
        /// Grille de fenêtres, en deux textures : l'albédo (ce que la façade montre) et
        /// l'émission (ce qui brille).
        ///
        /// Pourquoi deux : si on réutilise l'albédo comme carte d'émission, le MUR entre les
        /// fenêtres émet aussi, faiblement, et la façade entière se met à luire — l'immeuble
        /// devient une lanterne. L'émission doit être strictement noire partout sauf dans les
        /// carreaux allumés.
        ///
        /// Toutes les fenêtres ne sont pas allumées, et les allumées n'ont pas toutes la même
        /// intensité : un immeuble dont toutes les fenêtres brillent pareil se lit comme une
        /// texture répétée, ce qu'il est.
        /// </summary>
        public static void CreateWindowGrid(string folder, string name, int size, int columns, int rows,
            float litChance, Color litColor, int seed, out Texture2D albedo, out Texture2D emission)
        {
            EditorBuildUtility.EnsureFolder(folder);

            string albedoPath = folder + "/" + name + ".asset";
            string emissionPath = folder + "/" + name + "_Emission.asset";

            albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            emission = AssetDatabase.LoadAssetAtPath<Texture2D>(emissionPath);

            bool albedoIsNew = albedo == null;
            bool emissionIsNew = emission == null;

            if (albedoIsNew) albedo = new Texture2D(size, size, TextureFormat.RGBA32, true);
            if (emissionIsNew) emission = new Texture2D(size, size, TextureFormat.RGBA32, true);

            Random.State previous = Random.state;
            Random.InitState(seed);

            int cellW = Mathf.Max(8, size / Mathf.Max(1, columns));
            int cellH = Mathf.Max(8, size / Mathf.Max(1, rows));

            int marginX = Mathf.Max(2, cellW / 5);
            int marginY = Mathf.Max(2, cellH / 4);

            int count = columns * rows;
            bool[] lit = new bool[count];
            float[] level = new float[count];
            Color[] hue = new Color[count];

            for (int i = 0; i < count; i++)
            {
                lit[i] = Random.value < litChance;
                level[i] = Random.Range(0.45f, 1f);

                // Un appartement sur cinq a une lumière d'une autre teinte (écran, lampe de
                // couleur). C'est un détail minuscule qui casse définitivement la grille.
                hue[i] = Random.value < 0.2f
                    ? new Color(Random.Range(0.4f, 1f), Random.Range(0.4f, 1f), Random.Range(0.5f, 1f))
                    : Color.white;
            }

            Color wall = new Color(0.14f, 0.135f, 0.145f, 1f);
            Color darkGlass = new Color(0.045f, 0.05f, 0.065f, 1f);

            Color[] albedoPixels = new Color[size * size];
            Color[] emissionPixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int cx = Mathf.Clamp(x / cellW, 0, columns - 1);
                    int cy = Mathf.Clamp(y / cellH, 0, rows - 1);
                    int index = cy * columns + cx;

                    int inX = x - cx * cellW;
                    int inY = y - cy * cellH;

                    bool inWindow = inX >= marginX && inX < cellW - marginX
                                    && inY >= marginY && inY < cellH - marginY;

                    float grain = (Mathf.PerlinNoise(x * 0.3f, y * 0.3f) - 0.5f) * 0.05f;

                    if (!inWindow)
                    {
                        Color c = wall;
                        c.r = Mathf.Clamp01(c.r + grain);
                        c.g = Mathf.Clamp01(c.g + grain);
                        c.b = Mathf.Clamp01(c.b + grain);

                        albedoPixels[y * size + x] = c;
                        emissionPixels[y * size + x] = Color.black;
                        continue;
                    }

                    if (!lit[index])
                    {
                        albedoPixels[y * size + x] = darkGlass;
                        emissionPixels[y * size + x] = Color.black;
                        continue;
                    }

                    // Le haut d'une fenêtre allumée est plus lumineux que le bas : le
                    // plafonnier est en haut. Sans ce dégradé, chaque carreau est un aplat.
                    float vertical = 1f - (float)inY / Mathf.Max(1, cellH - marginY * 2) * 0.35f;
                    float value = Mathf.Clamp01(level[index] * vertical);

                    Color glow = litColor * hue[index] * value;
                    glow.a = 1f;

                    albedoPixels[y * size + x] = glow;
                    emissionPixels[y * size + x] = glow;
                }
            }

            Random.state = previous;

            albedo = Store(albedo, albedoPixels, albedoPath, albedoIsNew);
            emission = Store(emission, emissionPixels, emissionPath, emissionIsNew);
        }

        private static Texture2D Store(Texture2D texture, Color[] pixels, string path, bool isNew)
        {
            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 8;
            texture.Apply(true);

            if (isNew) AssetDatabase.CreateAsset(texture, path);
            else EditorUtility.SetDirty(texture);

            return texture;
        }
    }
}
