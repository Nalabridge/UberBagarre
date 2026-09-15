using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Boîte à outils partagée par les générateurs de scène / prefabs.
    ///
    /// Le point important ici est <see cref="FindLitShader"/> : on ne code AUCUN nom de shader en dur,
    /// on détecte le render pipeline réellement actif. C'est ce qui évite les matériaux rose fluo
    /// quand on ouvre le projet en URP, en HDRP ou en Built-in.
    /// </summary>
    public static class EditorBuildUtility
    {
        public static void EnsureFolder(string assetFolderPath)
        {
            if (string.IsNullOrEmpty(assetFolderPath) || AssetDatabase.IsValidFolder(assetFolderPath)) return;

            string parent = Path.GetDirectoryName(assetFolderPath);
            if (parent != null) parent = parent.Replace('\\', '/');

            if (!string.IsNullOrEmpty(parent) && parent != "Assets" && !AssetDatabase.IsValidFolder(parent))
            {
                EnsureFolder(parent);
            }

            AssetDatabase.CreateFolder(parent, Path.GetFileName(assetFolderPath));
        }

        /// <summary>Renvoie le shader "Lit" correspondant au render pipeline actif.</summary>
        public static Shader FindLitShader()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;

            if (pipeline != null)
            {
                string pipelineType = pipeline.GetType().FullName ?? string.Empty;

                if (pipelineType.Contains("HDRenderPipeline"))
                {
                    Shader hdrp = Shader.Find("HDRP/Lit");
                    if (hdrp != null) return hdrp;
                }

                Shader urp = Shader.Find("Universal Render Pipeline/Lit");
                if (urp != null) return urp;
            }

            Shader builtIn = Shader.Find("Standard");
            if (builtIn != null) return builtIn;

            // Dernier recours : mieux vaut un shader non éclairé qu'un matériau cassé.
            return Shader.Find("Unlit/Color");
        }

        public static string ActivePipelineName()
        {
            RenderPipelineAsset pipeline = GraphicsSettings.currentRenderPipeline;
            return pipeline == null ? "Built-in Render Pipeline" : pipeline.GetType().Name;
        }

        /// <summary>
        /// Crée (ou met à jour) un matériau d'asset. On assigne les propriétés via HasProperty :
        /// Standard utilise _Color/_Glossiness, URP utilise _BaseColor/_Smoothness.
        /// </summary>
        public static Material CreateOrUpdateMaterial(string folder, string materialName, Color color,
            float smoothness, float metallic, Texture2D albedo = null, Vector2 tiling = default)
        {
            EnsureFolder(folder);
            string path = folder + "/" + materialName + ".mat";

            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            Shader shader = FindLitShader();

            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }
            else if (material.shader != shader)
            {
                material.shader = shader;
            }

            SetColorIfPresent(material, "_BaseColor", color);
            SetColorIfPresent(material, "_Color", color);
            SetFloatIfPresent(material, "_Smoothness", smoothness);
            SetFloatIfPresent(material, "_Glossiness", smoothness);
            SetFloatIfPresent(material, "_Metallic", metallic);

            if (albedo != null)
            {
                SetTextureIfPresent(material, "_BaseMap", albedo);
                SetTextureIfPresent(material, "_MainTex", albedo);

                Vector2 scale = tiling == default(Vector2) ? Vector2.one : tiling;
                if (material.HasProperty("_BaseMap")) material.SetTextureScale("_BaseMap", scale);
                if (material.HasProperty("_MainTex")) material.SetTextureScale("_MainTex", scale);
            }

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Crée (ou met à jour) un matériau d'effet à partir du NOM de son shader.
        ///
        /// Pourquoi passer par un asset plutôt que laisser le composant faire Shader.Find au
        /// démarrage : dans une build, un shader qui n'est référencé par aucun matériau n'est
        /// pas compilé du tout, et Shader.Find renvoie null. Le composant sait retomber sur
        /// une image non traitée, mais l'effet serait silencieusement absent. Un matériau
        /// d'asset référencé par la scène règle la question définitivement.
        ///
        /// Renvoie null si le shader est introuvable — typiquement juste après avoir ajouté
        /// les fichiers, avant qu'Unity ne les ait importés.
        /// </summary>
        public static Material CreateOrUpdateEffectMaterial(string folder, string materialName, string shaderName)
        {
            Shader shader = Shader.Find(shaderName);

            if (shader == null)
            {
                Debug.LogWarning("[UberBagarre] Shader introuvable : " + shaderName +
                                 ". Si les fichiers viennent d'etre ajoutes, laisse Unity finir " +
                                 "l'import puis relance la construction de la scene.");
                return null;
            }

            EnsureFolder(folder);
            string path = folder + "/" + materialName + ".mat";

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

            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Crée (ou met à jour) un ciel procédural chaud de fin d'après-midi.
        ///
        /// Pourquoi ça compte autant : une scène créée vide n'a AUCUN ciel. Le fond est alors la
        /// couleur d'effacement par défaut de la caméra, un bleu-gris plat. Ce fond uni est
        /// probablement le premier responsable de l'impression « vieux jeu » : il n'y a ni
        /// dégradé, ni horizon, ni lumière d'ambiance cohérente avec le soleil.
        ///
        /// Renvoie null si le shader n'existe pas (HDRP gère le ciel par volumes). L'appelant
        /// retombe alors sur une couleur d'effacement chaude, ce qui reste mieux que le défaut.
        /// </summary>
        public static Material CreateOrUpdateProceduralSky(string folder, string materialName,
            Color skyTint, Color groundColor, float atmosphereThickness, float exposure, float sunSize)
        {
            Shader shader = Shader.Find("Skybox/Procedural");
            if (shader == null) return null;

            EnsureFolder(folder);
            string path = folder + "/" + materialName + ".mat";

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

            SetColorIfPresent(material, "_SkyTint", skyTint);
            SetColorIfPresent(material, "_GroundColor", groundColor);
            SetFloatIfPresent(material, "_AtmosphereThickness", atmosphereThickness);
            SetFloatIfPresent(material, "_Exposure", exposure);
            SetFloatIfPresent(material, "_SunSize", sunSize);
            SetFloatIfPresent(material, "_SunSizeConvergence", 5f);

            EditorUtility.SetDirty(material);
            return material;
        }

        private static void SetColorIfPresent(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetFloatIfPresent(Material material, string property, float value)
        {
            if (material.HasProperty(property)) material.SetFloat(property, value);
        }

        private static void SetTextureIfPresent(Material material, string property, Texture texture)
        {
            if (material.HasProperty(property)) material.SetTexture(property, texture);
        }

        /// <summary>
        /// Texture de MATIÈRE : un grain fin par-dessus une teinte de base.
        ///
        /// C'est le levier graphique le plus rentable de tout le projet, et celui qui manquait.
        /// Une couleur plate ne réagit à la lumière que par son orientation : deux surfaces
        /// tournées pareil sont rigoureusement identiques, et le résultat se lit comme une
        /// maquette en plastique — ce que « trop vieux, trop low poly » décrit en réalité.
        /// Un grain, même discret, donne à chaque centimètre carré une valeur légèrement
        /// différente, donc de la matière.
        ///
        /// Trois échelles de bruit superposées, parce qu'une seule se lit comme une trame :
        /// des taches larges (les variations de teinte), un grain moyen (le relief), et un bruit
        /// par pixel très faible (la micro-texture). C'est le même principe que la peau réelle.
        /// </summary>
        public static Texture2D CreateOrUpdateGrainTexture(string folder, string textureName, int size,
            Color baseColor, float patchStrength, float grainStrength, float pixelStrength,
            float patchScale, int seed)
        {
            EnsureFolder(folder);
            string path = folder + "/" + textureName + ".asset";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = texture == null;
            if (isNew) texture = new Texture2D(size, size, TextureFormat.RGBA32, true);

            Color[] pixels = new Color[size * size];
            Random.State previous = Random.state;
            Random.InitState(seed);

            float offset = seed % 97 * 3.7f;

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float patch = Mathf.PerlinNoise(offset + x * patchScale, offset + y * patchScale) - 0.5f;
                    float grain = Mathf.PerlinNoise(offset + x * patchScale * 7f, offset + y * patchScale * 7f) - 0.5f;
                    float pixel = Random.value - 0.5f;

                    float delta = patch * patchStrength + grain * grainStrength + pixel * pixelStrength;

                    pixels[y * size + x] = new Color(
                        Mathf.Clamp01(baseColor.r + delta),
                        Mathf.Clamp01(baseColor.g + delta * 0.96f),
                        Mathf.Clamp01(baseColor.b + delta * 0.92f),
                        1f);
                }
            }

            Random.state = previous;
            return Store(texture, pixels, path, isNew);
        }

        /// <summary>
        /// Texture de TISSU : un tissage régulier, plus du grain.
        ///
        /// Le tissage suffit à ce qu'un vêtement ne soit plus confondu avec de la peau peinte
        /// d'une autre couleur. La trame est volontairement fine : à l'échelle d'un combat au
        /// corps à corps, on ne doit pas voir des carreaux mais sentir une surface.
        /// </summary>
        public static Texture2D CreateOrUpdateFabricTexture(string folder, string textureName, int size,
            Color baseColor, int threadSize, float weaveStrength, int seed)
        {
            EnsureFolder(folder);
            string path = folder + "/" + textureName + ".asset";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = texture == null;
            if (isNew) texture = new Texture2D(size, size, TextureFormat.RGBA32, true);

            threadSize = Mathf.Max(2, threadSize);

            Color[] pixels = new Color[size * size];
            Random.State previous = Random.state;
            Random.InitState(seed);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    // Un fil sur deux passe dessus : c'est ce damier d'un fil de cote qui fait
                    // lire "tisse" plutot que "quadrille".
                    bool warp = ((x / threadSize) + (y / threadSize)) % 2 == 0;

                    float along = (warp ? y : x) % threadSize / (float)threadSize;
                    float round = Mathf.Sin(along * Mathf.PI);

                    float weave = (round - 0.5f) * weaveStrength;
                    float grain = (Random.value - 0.5f) * 0.035f;
                    float patch = (Mathf.PerlinNoise(x * 0.02f, y * 0.02f) - 0.5f) * 0.08f;

                    float delta = weave + grain + patch;

                    pixels[y * size + x] = new Color(
                        Mathf.Clamp01(baseColor.r + delta),
                        Mathf.Clamp01(baseColor.g + delta),
                        Mathf.Clamp01(baseColor.b + delta),
                        1f);
                }
            }

            Random.state = previous;
            return Store(texture, pixels, path, isNew);
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

        /// <summary>
        /// Génère une texture en damier et la sauvegarde en asset.
        /// Pourquoi : sur un sol uni, on ne perçoit pas son propre déplacement.
        /// Un damier rend le réglage des vitesses et de l'inertie immédiatement lisible.
        /// </summary>
        public static Texture2D CreateOrUpdateCheckerTexture(string folder, string textureName,
            int size, int cells, Color colorA, Color colorB)
        {
            EnsureFolder(folder);
            string path = folder + "/" + textureName + ".asset";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = texture == null;

            if (isNew)
            {
                texture = new Texture2D(size, size, TextureFormat.RGBA32, true);
            }

            int cellSize = Mathf.Max(1, size / Mathf.Max(1, cells));
            Color[] pixels = new Color[size * size];

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    bool even = ((x / cellSize) + (y / cellSize)) % 2 == 0;
                    Color baseColor = even ? colorA : colorB;

                    // Liseré plus sombre sur les bords de case : donne une grille discrète.
                    bool border = (x % cellSize == 0) || (y % cellSize == 0);
                    pixels[y * size + x] = border ? baseColor * 0.82f : baseColor;
                }
            }

            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            texture.Apply(true);

            if (isNew)
            {
                AssetDatabase.CreateAsset(texture, path);
            }
            else
            {
                EditorUtility.SetDirty(texture);
            }

            return texture;
        }

        /// <summary>
        /// Texture de bitume : gris sombre bruité, avec des plaques plus claires et quelques
        /// éclats. Un sol uni ne donne aucune sensation de déplacement, et un damier fait
        /// « prototype ». Le bruit fait le travail à lui seul.
        /// </summary>
        public static Texture2D CreateOrUpdateAsphaltTexture(string folder, string textureName, int size)
        {
            EnsureFolder(folder);
            string path = folder + "/" + textureName + ".asset";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = texture == null;
            if (isNew) texture = new Texture2D(size, size, TextureFormat.RGBA32, true);

            Color[] pixels = new Color[size * size];
            Random.State previousState = Random.state;
            Random.InitState(20260912);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float coarse = Mathf.PerlinNoise(x * 0.035f, y * 0.035f);
                    float fine = Random.value;

                    float value = 0.20f + coarse * 0.09f + (fine - 0.5f) * 0.10f;

                    // Quelques gravillons clairs : ils accrochent la lumiere et cassent l'uniformite.
                    if (fine > 0.985f) value += 0.22f;

                    pixels[y * size + x] = new Color(value, value * 1.01f, value * 1.05f, 1f);
                }
            }

            Random.state = previousState;

            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            texture.Apply(true);

            if (isNew) AssetDatabase.CreateAsset(texture, path);
            else EditorUtility.SetDirty(texture);

            return texture;
        }

        /// <summary>Texture de mur de brique : rangées décalées, joints de mortier, teintes variées.</summary>
        public static Texture2D CreateOrUpdateBrickTexture(string folder, string textureName, int size,
            int rows, Color mortar, Color brickBase)
        {
            EnsureFolder(folder);
            string path = folder + "/" + textureName + ".asset";

            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            bool isNew = texture == null;
            if (isNew) texture = new Texture2D(size, size, TextureFormat.RGBA32, true);

            int rowHeight = Mathf.Max(4, size / Mathf.Max(1, rows));
            int brickWidth = rowHeight * 2;
            int jointThickness = Mathf.Max(1, rowHeight / 8);

            Color[] pixels = new Color[size * size];
            Random.State previousState = Random.state;
            Random.InitState(451);

            // Une teinte tiree par brique, pas par pixel : sinon le mur grouille.
            float[] rowSeeds = new float[size / Mathf.Max(1, rowHeight) + 2];
            for (int i = 0; i < rowSeeds.Length; i++) rowSeeds[i] = Random.value;

            for (int y = 0; y < size; y++)
            {
                int row = y / rowHeight;
                int offset = (row % 2) * (brickWidth / 2);
                bool horizontalJoint = (y % rowHeight) < jointThickness;

                for (int x = 0; x < size; x++)
                {
                    int localX = (x + offset) % brickWidth;
                    bool verticalJoint = localX < jointThickness;

                    if (horizontalJoint || verticalJoint)
                    {
                        pixels[y * size + x] = mortar;
                        continue;
                    }

                    int brickIndex = ((x + offset) / brickWidth) + row * 7;
                    float tint = 0.85f + Mathf.Repeat(brickIndex * 0.37f + rowSeeds[row % rowSeeds.Length], 0.3f);

                    pixels[y * size + x] = new Color(brickBase.r * tint, brickBase.g * tint, brickBase.b * tint, 1f);
                }
            }

            Random.state = previousState;

            texture.SetPixels(pixels);
            texture.wrapMode = TextureWrapMode.Repeat;
            texture.filterMode = FilterMode.Bilinear;
            texture.anisoLevel = 4;
            texture.Apply(true);

            if (isNew) AssetDatabase.CreateAsset(texture, path);
            else EditorUtility.SetDirty(texture);

            return texture;
        }

        /// <summary>Crée une primitive. 'withCollider = false' détruit le collider auto (objets purement visuels).</summary>
        public static GameObject CreatePrimitive(PrimitiveType type, string name, Transform parent,
            Vector3 localPosition, Vector3 localScale, Material material, bool withCollider)
        {
            GameObject go = GameObject.CreatePrimitive(type);
            go.name = name;

            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localScale = localScale;

            if (!withCollider)
            {
                Collider collider = go.GetComponent<Collider>();
                if (collider != null) Object.DestroyImmediate(collider);
            }

            if (material != null)
            {
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = material;
            }

            return go;
        }

        public static GameObject CreateEmpty(string name, Transform parent, Vector3 localPosition)
        {
            GameObject go = new GameObject(name);
            if (parent != null) go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.transform.localRotation = Quaternion.identity;
            return go;
        }
    }
}
