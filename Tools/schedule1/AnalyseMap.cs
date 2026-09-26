// À copier dans le projet exporté par AssetRipper : ExportedProject/Assets/Editor/AnalyseMap.cs
// (créer le dossier Editor s'il n'existe pas). Puis, avec la scène Main ouverte :
// menu « Uber Bagarre > Analyser la map ouverte ».
//
// Écrit, à la racine du projet exporté (à côté du dossier Assets) :
//   map_analyse.txt      la hiérarchie de la scène, résumée (groupes, nombres d'objets, positions)
//   map_objets.csv       chaque objet dessiné : chemin, maillage, shader, position et taille
//   map_terrains.txt     les terrains (position, taille, couches) + map_terrain_N.png (relief)
//   map_vue_dessus.png   une vue de dessus de toute la scène
//
// Rien n'est modifié dans la scène. Ce fichier ne dépend d'aucun script du jeu.
#if UNITY_EDITOR
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class UberBagarreAnalyseMap
{
    private sealed class Stats
    {
        public int Objects;
        public int Renderers;
        public int Lights;
        public int Terrains;
        public int Colliders;
        public int MissingScripts;
        public bool HasBounds;
        public Bounds Bounds;
        public readonly Dictionary<string, int> Scripts = new Dictionary<string, int>();
    }

    private static readonly Dictionary<Transform, Stats> Cache = new Dictionary<Transform, Stats>();
    private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

    [MenuItem("Uber Bagarre/Analyser la map ouverte")]
    public static void Analyse()
    {
        Scene scene = SceneManager.GetActiveScene();
        string root = Directory.GetParent(Application.dataPath).FullName;
        Cache.Clear();

        GameObject[] roots = scene.GetRootGameObjects();
        Stats total = new Stats();
        for (int i = 0; i < roots.Length; i++)
        {
            EditorUtility.DisplayProgressBar("Analyse de la map", roots[i].name, i / (float)roots.Length);
            Merge(total, Collect(roots[i].transform));
        }

        StringBuilder sb = new StringBuilder();
        sb.AppendLine("Scene : " + scene.path + "   objets racines : " + roots.Length);
        sb.AppendLine("Unity " + Application.unityVersion + "   pipeline : " +
                      (UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline != null
                          ? UnityEngine.Rendering.GraphicsSettings.defaultRenderPipeline.name
                          : "integre (built-in)"));
        sb.AppendLine(Line("TOTAL", total));
        sb.AppendLine();
        sb.AppendLine("=== HIERARCHIE (les objets du meme nom sont regroupes : « x12 ») ===");
        Describe(sb, roots, 0);

        sb.AppendLine();
        sb.AppendLine("=== SHADERS (nombre de materiaux) ===");
        Dictionary<string, int> shaders = new Dictionary<string, int>();
        Dictionary<string, int> lightTypes = new Dictionary<string, int>();
        HashSet<Material> seen = new HashSet<Material>();

        StringBuilder csv = new StringBuilder();
        csv.AppendLine("chemin;maillage;sommets;materiau;shader;statique;cx;cy;cz;sx;sy;sz");

        Renderer[] renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer r = renderers[i];
            if (r.gameObject.scene != scene) continue;
            if (i % 500 == 0) EditorUtility.DisplayProgressBar("Analyse de la map", "Objets dessines", i / (float)renderers.Length);

            Material first = null;
            foreach (Material m in r.sharedMaterials)
            {
                if (m == null) continue;
                if (first == null) first = m;
                if (!seen.Add(m)) continue;
                string s = m.shader != null ? m.shader.name : "(aucun)";
                shaders[s] = shaders.TryGetValue(s, out int n) ? n + 1 : 1;
            }

            Mesh mesh = null;
            MeshFilter filter = r.GetComponent<MeshFilter>();
            if (filter != null) mesh = filter.sharedMesh;
            SkinnedMeshRenderer skinned = r as SkinnedMeshRenderer;
            if (skinned != null) mesh = skinned.sharedMesh;

            Bounds b = r.bounds;
            csv.Append(Clean(Path(r.transform))).Append(';')
                .Append(Clean(mesh != null ? mesh.name : "")).Append(';')
                .Append(mesh != null ? mesh.vertexCount : 0).Append(';')
                .Append(Clean(first != null ? first.name : "")).Append(';')
                .Append(Clean(first != null && first.shader != null ? first.shader.name : "")).Append(';')
                .Append(r.gameObject.isStatic ? 1 : 0).Append(';')
                .Append(F(b.center.x)).Append(';').Append(F(b.center.y)).Append(';').Append(F(b.center.z)).Append(';')
                .Append(F(b.size.x)).Append(';').Append(F(b.size.y)).Append(';').Append(F(b.size.z)).AppendLine();
        }

        foreach (KeyValuePair<string, int> kv in Sorted(shaders)) sb.AppendLine("  " + kv.Value + "  " + kv.Key);

        sb.AppendLine();
        sb.AppendLine("=== LUMIERES ===");
        foreach (Light l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (l.gameObject.scene != scene) continue;
            string k = l.type + (l.bakingOutput.lightmapBakeType == LightmapBakeType.Baked ? " (precalculee)" : "");
            lightTypes[k] = lightTypes.TryGetValue(k, out int n) ? n + 1 : 1;
        }

        foreach (KeyValuePair<string, int> kv in Sorted(lightTypes)) sb.AppendLine("  " + kv.Value + "  " + kv.Key);

        StringBuilder terrains = new StringBuilder();
        int index = 0;
        foreach (Terrain t in Object.FindObjectsByType<Terrain>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (t.gameObject.scene != scene || t.terrainData == null) continue;
            TerrainData d = t.terrainData;
            terrains.AppendLine("Terrain " + index + " : " + Path(t.transform));
            terrains.AppendLine("  position " + V(t.transform.position) + "   taille " + V(d.size) +
                                "   relief " + d.heightmapResolution + "   actif " + t.gameObject.activeInHierarchy);
            foreach (TerrainLayer layer in d.terrainLayers)
            {
                if (layer != null) terrains.AppendLine("  couche : " + layer.name + (layer.diffuseTexture != null ? "  (" + layer.diffuseTexture.name + ")" : ""));
            }

            terrains.AppendLine("  arbres : " + d.treeInstanceCount + "   herbes/details : " + d.detailPrototypes.Length);
            SaveHeightmap(d, System.IO.Path.Combine(root, "map_terrain_" + index + ".png"));
            index++;
        }

        EditorUtility.DisplayProgressBar("Analyse de la map", "Vue de dessus", 1f);
        bool shot = false;
        if (total.HasBounds) shot = TopView(total.Bounds, System.IO.Path.Combine(root, "map_vue_dessus.png"));

        File.WriteAllText(System.IO.Path.Combine(root, "map_analyse.txt"), sb.ToString(), Encoding.UTF8);
        File.WriteAllText(System.IO.Path.Combine(root, "map_objets.csv"), csv.ToString(), Encoding.UTF8);
        File.WriteAllText(System.IO.Path.Combine(root, "map_terrains.txt"), terrains.ToString(), Encoding.UTF8);
        EditorUtility.ClearProgressBar();

        Debug.Log("[Uber Bagarre] Analyse ecrite dans " + root + (shot ? " (avec la vue de dessus)" : " (vue de dessus impossible)"));
        EditorUtility.RevealInFinder(System.IO.Path.Combine(root, "map_analyse.txt"));
    }

    // ------------------------------------------------------------------ hiérarchie

    private static Stats Collect(Transform t)
    {
        Stats s = new Stats();
        s.Objects = 1;
        GameObject go = t.gameObject;

        Renderer r = go.GetComponent<Renderer>();
        if (r != null)
        {
            s.Renderers = 1;
            s.HasBounds = true;
            s.Bounds = r.bounds;
        }

        Terrain terrain = go.GetComponent<Terrain>();
        if (terrain != null && terrain.terrainData != null)
        {
            s.Terrains = 1;
            Bounds tb = new Bounds(t.position + terrain.terrainData.size * 0.5f, terrain.terrainData.size);
            if (s.HasBounds) s.Bounds.Encapsulate(tb);
            else s.Bounds = tb;
            s.HasBounds = true;
        }

        if (go.GetComponent<Light>() != null) s.Lights = 1;
        if (go.GetComponent<Collider>() != null) s.Colliders = 1;
        s.MissingScripts = GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(go);

        foreach (MonoBehaviour mb in go.GetComponents<MonoBehaviour>())
        {
            if (mb == null) continue;
            string n = mb.GetType().Name;
            s.Scripts[n] = s.Scripts.TryGetValue(n, out int c) ? c + 1 : 1;
        }

        for (int i = 0; i < t.childCount; i++) Merge(s, Collect(t.GetChild(i)));
        Cache[t] = s;
        return s;
    }

    private static void Merge(Stats into, Stats from)
    {
        into.Objects += from.Objects;
        into.Renderers += from.Renderers;
        into.Lights += from.Lights;
        into.Terrains += from.Terrains;
        into.Colliders += from.Colliders;
        into.MissingScripts += from.MissingScripts;
        foreach (KeyValuePair<string, int> kv in from.Scripts)
        {
            into.Scripts[kv.Key] = into.Scripts.TryGetValue(kv.Key, out int n) ? n + kv.Value : kv.Value;
        }

        if (!from.HasBounds) return;
        if (into.HasBounds) into.Bounds.Encapsulate(from.Bounds);
        else into.Bounds = from.Bounds;
        into.HasBounds = true;
    }

    private static readonly Regex Suffix = new Regex(@"\s*(\(\d+\)|_\d+|\.\d+)$");

    /// <summary>Les enfants, regroupés par nom (« Lampadaire (12) » → « Lampadaire x40 »).</summary>
    private static void Describe(StringBuilder sb, IList<GameObject> children, int depth)
    {
        Dictionary<string, List<Transform>> groups = new Dictionary<string, List<Transform>>();
        List<string> order = new List<string>();
        foreach (GameObject child in children)
        {
            string key = Suffix.Replace(child.name, "");
            if (!groups.TryGetValue(key, out List<Transform> list))
            {
                list = new List<Transform>();
                groups[key] = list;
                order.Add(key);
            }

            list.Add(child.transform);
        }

        int shown = 0;
        foreach (string key in order)
        {
            List<Transform> list = groups[key];
            Stats s = new Stats();
            bool active = false;
            foreach (Transform t in list)
            {
                Merge(s, Cache[t]);
                active |= t.gameObject.activeSelf;
            }

            if (shown++ >= 80)
            {
                sb.AppendLine(new string(' ', depth * 2) + "... et " + (order.Count - 80) + " autres groupes");
                break;
            }

            string name = key + (list.Count > 1 ? "  x" + list.Count : "") + (active ? "" : "  [INACTIF]");
            sb.AppendLine(new string(' ', depth * 2) + Line(name, s));

            // On descend dans ce qui porte beaucoup de décor (un seul objet du groupe : ils se ressemblent).
            bool deep = depth < 2 || (depth < 5 && s.Renderers >= 40);
            if (!deep || list[0].childCount == 0) continue;

            List<GameObject> sub = new List<GameObject>();
            for (int i = 0; i < list[0].childCount; i++) sub.Add(list[0].GetChild(i).gameObject);
            Describe(sb, sub, depth + 1);
        }
    }

    private static string Line(string name, Stats s)
    {
        StringBuilder sb = new StringBuilder(name);
        sb.Append("   objets ").Append(s.Objects).Append("  dessins ").Append(s.Renderers);
        if (s.Terrains > 0) sb.Append("  TERRAINS ").Append(s.Terrains);
        if (s.Lights > 0) sb.Append("  lumieres ").Append(s.Lights);
        if (s.Colliders > 0) sb.Append("  collisions ").Append(s.Colliders);
        if (s.MissingScripts > 0) sb.Append("  scripts manquants ").Append(s.MissingScripts);
        if (s.HasBounds) sb.Append("  centre ").Append(V(s.Bounds.center)).Append(" taille ").Append(V(s.Bounds.size));

        int k = 0;
        foreach (KeyValuePair<string, int> kv in Sorted(s.Scripts))
        {
            sb.Append(k == 0 ? "  scripts: " : ", ").Append(kv.Key).Append(kv.Value > 1 ? "x" + kv.Value : "");
            if (++k >= 4) break;
        }

        return sb.ToString();
    }

    // ------------------------------------------------------------------ images

    private static void SaveHeightmap(TerrainData d, string path)
    {
        int res = Mathf.Min(1024, d.heightmapResolution);
        float[,] h = d.GetHeights(0, 0, d.heightmapResolution, d.heightmapResolution);
        Texture2D tex = new Texture2D(res, res, TextureFormat.RGB24, false);
        float step = (d.heightmapResolution - 1) / (float)(res - 1);
        for (int y = 0; y < res; y++)
        {
            for (int x = 0; x < res; x++)
            {
                float v = h[Mathf.RoundToInt(y * step), Mathf.RoundToInt(x * step)];
                tex.SetPixel(x, y, new Color(v, v, v));
            }
        }

        File.WriteAllBytes(path, tex.EncodeToPNG());
        Object.DestroyImmediate(tex);
    }

    private static bool TopView(Bounds bounds, string path)
    {
        GameObject go = new GameObject("Camera analyse (temporaire)");
        go.hideFlags = HideFlags.HideAndDontSave;
        RenderTexture rt = null;
        try
        {
            Camera cam = go.AddComponent<Camera>();
            cam.orthographic = true;
            cam.orthographicSize = Mathf.Max(bounds.size.x, bounds.size.z) * 0.5f;
            cam.aspect = 1f;
            cam.transform.position = bounds.center + Vector3.up * (bounds.extents.y + 50f);
            cam.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            cam.nearClipPlane = 1f;
            cam.farClipPlane = bounds.size.y + 200f;
            cam.clearFlags = CameraClearFlags.SolidColor;
            cam.backgroundColor = new Color(0.1f, 0.1f, 0.12f);

            const int size = 4096;
            rt = new RenderTexture(size, size, 24, RenderTextureFormat.ARGB32);
            cam.targetTexture = rt;
            cam.Render();

            RenderTexture.active = rt;
            Texture2D tex = new Texture2D(size, size, TextureFormat.RGB24, false);
            tex.ReadPixels(new Rect(0, 0, size, size), 0, 0);
            tex.Apply();
            RenderTexture.active = null;

            File.WriteAllBytes(path, tex.EncodeToPNG());
            Object.DestroyImmediate(tex);
            File.WriteAllText(System.IO.Path.ChangeExtension(path, ".txt"),
                "Vue de dessus : x de " + F(bounds.min.x) + " a " + F(bounds.max.x) + ", z de " + F(bounds.min.z) + " a " +
                F(bounds.max.z) + " (nord en haut). Carre de " + F(cam.orthographicSize * 2f) + " m centre sur " + V(bounds.center));
            return true;
        }
        catch (System.Exception e)
        {
            Debug.LogWarning("[Uber Bagarre] Vue de dessus impossible : " + e.Message);
            return false;
        }
        finally
        {
            if (rt != null) rt.Release();
            Object.DestroyImmediate(go);
        }
    }

    // ------------------------------------------------------------------ utilitaires

    private static string Path(Transform t)
    {
        string p = t.name;
        while (t.parent != null)
        {
            t = t.parent;
            p = t.name + "/" + p;
        }

        return p;
    }

    private static string Clean(string s)
    {
        return s.Replace(';', ',').Replace('\n', ' ').Replace('\r', ' ');
    }

    private static string F(float v)
    {
        return v.ToString("0.#", Inv);
    }

    private static string V(Vector3 v)
    {
        return "(" + F(v.x) + " " + F(v.y) + " " + F(v.z) + ")";
    }

    private static List<KeyValuePair<string, int>> Sorted(Dictionary<string, int> d)
    {
        List<KeyValuePair<string, int>> list = new List<KeyValuePair<string, int>>(d);
        list.Sort((a, b) => b.Value.CompareTo(a.Value));
        return list;
    }
}
#endif
