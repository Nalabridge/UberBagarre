using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Construit dans la scène un <see cref="InteriorPlan"/> : les maillages fusionnés (un
    /// par matériau et par case, voir <see cref="InteriorMesher"/>), enregistrés dans un asset
    /// par lieu ; les matériaux (le shader triplanaire « UberBagarre/Interieur » et les
    /// textures procédurales, générées une fois) ; les collisions regroupées ; les lampes ; la
    /// sonde de reflets ; les enseignes au néon ; les repères (arrivée, porte, comptoir,
    /// vendeur).
    /// </summary>
    internal static class InteriorRealizer
    {
        public const string Folder = "Assets/UberBagarre/Art/Interieurs/Generes";
        private const string TextureFolder = Folder + "/Textures";
        private const string MaterialFolder = Folder + "/Materiaux";

        /// <summary>Change ce numéro quand les textures procédurales changent : elles sont alors régénérées.</summary>
        private const int TextureVersion = 1;

        private const int TextureSize = 512;
        private const float Cell = 4f;

        public sealed class Built
        {
            public GameObject Root;
            public readonly Dictionary<string, Transform> Markers = new Dictionary<string, Transform>();
            public readonly Dictionary<string, InteriorPlan.Marker> MarkerData = new Dictionary<string, InteriorPlan.Marker>();
            public readonly Dictionary<string, GameObject> Groups = new Dictionary<string, GameObject>();
        }

        private static readonly Dictionary<string, Texture2D> Albedos = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, Texture2D> Normals = new Dictionary<string, Texture2D>();
        private static readonly Dictionary<string, float> Meters = new Dictionary<string, float>();

        /// <summary>Construit <paramref name="plan"/> sous <paramref name="parent"/>, la pièce à <paramref name="origin"/> (monde).</summary>
        public static Built Realize(InteriorPlan plan, Transform parent, Vector3 origin, string name, string assetName, bool probe = true)
        {
            EditorBuildUtility.EnsureFolder(Folder);
            Built built = new Built();
            GameObject root = new GameObject(name);
            root.transform.SetParent(parent, false);
            root.transform.position = origin;
            root.transform.rotation = Quaternion.identity;
            built.Root = root;
            Transform t = root.transform;

            // --- les maillages
            List<InteriorMesher.Batch> batches = InteriorMesher.Build(plan, Cell);
            List<Mesh> meshes = new List<Mesh>(batches.Count);
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            GameObject decor = EditorBuildUtility.CreateEmpty("Decor", t, Vector3.zero);
            for (int i = 0; i < batches.Count; i++)
            {
                InteriorMesher.Batch batch = batches[i];
                InteriorPlan.Surface surface;
                if (!plan.Surfaces.TryGetValue(batch.Surface, out surface)) continue;

                Material material;
                if (!materials.TryGetValue(batch.Surface, out material))
                {
                    material = MaterialFor(surface);
                    materials[batch.Surface] = material;
                }

                Transform holder = string.IsNullOrEmpty(batch.Group) ? decor.transform : Group(built, t, batch.Group).transform;
                Mesh mesh = ToMesh(batch, assetName + "_" + i);
                meshes.Add(mesh);

                GameObject go = EditorBuildUtility.CreateEmpty(batch.Surface + " " + batch.CellX + "," + batch.CellZ, holder, Vector3.zero);
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                MeshRenderer renderer = go.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                bool glowing = surface.Emission.maxColorComponent > 0.01f;
                bool clear = surface.Glass && surface.Color.a < 0.99f;
                renderer.shadowCastingMode = glowing || clear ? ShadowCastingMode.Off : ShadowCastingMode.On;
                renderer.reflectionProbeUsage = ReflectionProbeUsage.BlendProbes;
                renderer.lightProbeUsage = LightProbeUsage.Off;
            }

            SaveMeshes(meshes, assetName);

            // --- les collisions : les boîtes droites ensemble, une par orientation
            Colliders(plan, built, t);

            // --- les lampes ; l'une d'elles (la plus centrale) porte des ombres
            Lamps(plan, t);

            // --- la sonde de reflets : chrome, vitrines, sols brillants reflètent la pièce
            if (probe)
            {
                EditorBuildUtility.AddReflectionProbe(t, "Sonde de reflexion", new Vector3(0f, plan.Size.y * 0.5f, plan.Size.z * 0.5f),
                    plan.Size + new Vector3(0.4f, 0.2f, 0.4f), true, 1f);
            }

            // --- les enseignes : des tubes lettre par lettre, puis fusionnés en un maillage par couleur
            GameObject signs = EditorBuildUtility.CreateEmpty("Enseignes", t, Vector3.zero);
            for (int i = 0; i < plan.Signs.Count; i++)
            {
                InteriorPlan.Sign sign = plan.Signs[i];
                InteriorPlan.Surface surface;
                if (!plan.Surfaces.TryGetValue(sign.Surface, out surface)) continue;
                Material material;
                if (!materials.TryGetValue(sign.Surface, out material))
                {
                    material = MaterialFor(surface);
                    materials[sign.Surface] = material;
                }

                GameObject go = EditorBuildUtility.CreateEmpty("Enseigne " + sign.Text, signs.transform, sign.Position);
                go.transform.localRotation = sign.Rotation;
                NeonTextBuilder.Build(go.transform, ShopInteriorBuilder.Ascii(sign.Text), sign.Height, sign.Height * 0.1f, material);
            }

            if (plan.Signs.Count > 0)
            {
                CityMeshBuilder.Collapse(signs, assetName + "_Enseignes");
                for (int i = signs.transform.childCount - 1; i >= 0; i--)
                {
                    GameObject child = signs.transform.GetChild(i).gameObject;
                    if (child.GetComponent<MeshFilter>() == null) Object.DestroyImmediate(child);
                }
            }

            // --- les repères
            for (int i = 0; i < plan.Markers.Count; i++)
            {
                InteriorPlan.Marker marker = plan.Markers[i];
                if (built.Markers.ContainsKey(marker.Key)) continue;
                GameObject go = EditorBuildUtility.CreateEmpty("Repere " + marker.Key, t, marker.Position);
                go.transform.localRotation = marker.Rotation;
                built.Markers[marker.Key] = go.transform;
                built.MarkerData[marker.Key] = marker;
            }

            return built;
        }

        private static GameObject Group(Built built, Transform root, string key)
        {
            GameObject go;
            if (built.Groups.TryGetValue(key, out go)) return go;
            go = EditorBuildUtility.CreateEmpty("Groupe " + key, root, Vector3.zero);
            built.Groups[key] = go;
            return go;
        }

        // ================================================================== maillages

        private static Mesh ToMesh(InteriorMesher.Batch batch, string name)
        {
            Mesh mesh = new Mesh { name = name };
            if (batch.Vertices.Count > 65000) mesh.indexFormat = IndexFormat.UInt32;
            mesh.SetVertices(batch.Vertices);
            mesh.SetNormals(batch.Normals);
            mesh.SetUVs(0, batch.Uvs);
            mesh.SetTriangles(batch.Triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }

        /// <summary>Tous les maillages d'un lieu dans un seul fichier (remplacé à chaque construction).</summary>
        private static void SaveMeshes(List<Mesh> meshes, string assetName)
        {
            if (meshes.Count == 0) return;
            string path = Folder + "/" + assetName + ".asset";
            if (AssetDatabase.LoadMainAssetAtPath(path) != null) AssetDatabase.DeleteAsset(path);
            AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Count; i++) AssetDatabase.AddObjectToAsset(meshes[i], meshes[0]);
        }

        // ================================================================== collisions

        private static void Colliders(InteriorPlan plan, Built built, Transform root)
        {
            GameObject holder = EditorBuildUtility.CreateEmpty("Collisions", root, Vector3.zero);
            Dictionary<string, GameObject> byRotation = new Dictionary<string, GameObject>();
            for (int i = 0; i < plan.Parts.Count; i++)
            {
                InteriorPlan.Part part = plan.Parts[i];
                if (!part.Collider || part.Shape == InteriorPlan.Shape.Sphere) continue;

                GameObject owner;
                if (!string.IsNullOrEmpty(part.Group))
                {
                    // Le collider d'un groupe (le battant de la porte) : sur l'objet du groupe, qu'on cliquera.
                    owner = Group(built, root, part.Group);
                    if (owner.GetComponent<Collider>() == null && Quaternion.Angle(part.Rotation, Quaternion.identity) < 0.5f)
                    {
                        owner.transform.localPosition = part.Position;
                        foreach (Transform child in owner.transform) child.localPosition = -part.Position;
                        BoxCollider own = owner.AddComponent<BoxCollider>();
                        own.size = part.Size;
                        continue;
                    }
                }
                else
                {
                    Vector3 e = part.Rotation.eulerAngles;
                    string key = Mathf.RoundToInt(e.x) + "," + Mathf.RoundToInt(e.y) + "," + Mathf.RoundToInt(e.z);
                    if (!byRotation.TryGetValue(key, out owner))
                    {
                        owner = EditorBuildUtility.CreateEmpty("Collisions " + key, holder.transform, Vector3.zero);
                        owner.transform.localRotation = part.Rotation;
                        byRotation[key] = owner;
                    }
                }

                BoxCollider box = owner.AddComponent<BoxCollider>();
                box.center = Quaternion.Inverse(owner.transform.localRotation) * (part.Position - owner.transform.localPosition);
                box.size = part.Size;
            }
        }

        // ================================================================== lumière

        private static void Lamps(InteriorPlan plan, Transform root)
        {
            GameObject holder = EditorBuildUtility.CreateEmpty("Lumieres", root, Vector3.zero);
            int shadowed = -1;
            float best = float.MaxValue;
            Vector3 middle = new Vector3(0f, plan.Size.y, plan.Size.z * 0.55f);
            bool any = false;
            for (int i = 0; i < plan.Lamps.Count; i++)
            {
                if (plan.Lamps[i].Shadows) any = true;
                if (plan.Lamps[i].Spot) continue;
                float d = (plan.Lamps[i].Position - middle).sqrMagnitude;
                if (d < best)
                {
                    best = d;
                    shadowed = i;
                }
            }

            if (any) shadowed = -1;
            for (int i = 0; i < plan.Lamps.Count; i++)
            {
                InteriorPlan.Lamp lamp = plan.Lamps[i];
                bool shadows = lamp.Shadows || i == shadowed;
                Light light = NightStreetBuilder.AddLight(holder.transform, lamp.Spot ? "Spot" : "Lampe", lamp.Position, lamp.Color, lamp.Intensity,
                    lamp.Range, false, shadows);
                if (!lamp.Spot) continue;
                light.type = LightType.Spot;
                light.spotAngle = lamp.Angle;
                light.innerSpotAngle = lamp.Angle * 0.55f;
                light.transform.localRotation = lamp.Rotation;
            }
        }

        // ================================================================== matériaux

        private static Material MaterialFor(InteriorPlan.Surface s)
        {
            EditorBuildUtility.EnsureFolder(MaterialFolder);
            string name = "M_Int_" + s.Key + "_" + Fingerprint(s);
            string path = MaterialFolder + "/" + name + ".mat";
            Material existing = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (existing != null) return existing;

            bool clear = s.Glass && s.Color.a < 0.99f;
            Shader shader = Shader.Find("UberBagarre/Interieur");
            Material m;
            if (clear || shader == null)
            {
                m = EditorBuildUtility.CreateOrUpdateMaterial(MaterialFolder, name, s.Color, s.Smoothness, s.Metallic);
                if (clear) UrpSetup.MakeTransparent(m);
                if (s.Emission.maxColorComponent > 0.01f && m.HasProperty("_EmissionColor"))
                {
                    m.EnableKeyword("_EMISSION");
                    m.SetColor("_EmissionColor", s.Emission);
                    m.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }

                EditorUtility.SetDirty(m);
                return m;
            }

            m = new Material(shader) { name = name };
            m.SetColor("_BaseColor", new Color(s.Color.r, s.Color.g, s.Color.b, 1f));
            m.SetFloat("_Smoothness", s.Smoothness);
            m.SetFloat("_Metallic", s.Metallic);
            m.SetColor("_EmissionColor", s.Emission);
            m.SetFloat("_BlendingEdges", 6f);
            m.SetFloat("_NormalStrength", 1f);
            if (!string.IsNullOrEmpty(s.Texture))
            {
                Texture2D albedo, normal;
                float meters;
                TexturesFor(s.Texture, out albedo, out normal, out meters);
                m.SetTexture("_DiffuseTexture", albedo);
                m.SetTexture("_NormalTexture", normal);
                m.SetFloat("_Tiling", 1f / Mathf.Max(0.05f, meters));
            }
            else
            {
                m.SetFloat("_Tiling", 1f);
            }

            m.enableInstancing = true;
            AssetDatabase.CreateAsset(m, path);
            return m;
        }

        /// <summary>Une empreinte courte des réglages : deux lieux au même parquet partagent le même matériau.</summary>
        private static string Fingerprint(InteriorPlan.Surface s)
        {
            unchecked
            {
                int h = 17;
                h = h * 31 + Mathf.RoundToInt(s.Color.r * 1000f);
                h = h * 31 + Mathf.RoundToInt(s.Color.g * 1000f);
                h = h * 31 + Mathf.RoundToInt(s.Color.b * 1000f);
                h = h * 31 + Mathf.RoundToInt(s.Color.a * 1000f);
                h = h * 31 + Mathf.RoundToInt(s.Smoothness * 1000f);
                h = h * 31 + Mathf.RoundToInt(s.Metallic * 1000f);
                h = h * 31 + Mathf.RoundToInt(s.Emission.r * 100f);
                h = h * 31 + Mathf.RoundToInt(s.Emission.g * 100f);
                h = h * 31 + Mathf.RoundToInt(s.Emission.b * 100f);
                h = h * 31 + (s.Texture ?? "").GetHashCode();
                h = h * 31 + (s.Glass ? 1 : 0);
                h = h * 31 + TextureVersion;
                return (h & 0x7fffffff).ToString("x8");
            }
        }

        /// <summary>Les textures d'un style (carrelage, parquet…), générées au premier besoin et gardées en assets.</summary>
        internal static void TexturesFor(string style, out Texture2D albedo, out Texture2D normal, out float meters)
        {
            if (Albedos.TryGetValue(style, out albedo) && albedo != null && Normals.TryGetValue(style, out normal) && normal != null)
            {
                meters = Meters[style];
                return;
            }

            EditorBuildUtility.EnsureFolder(TextureFolder);
            string albedoPath = TextureFolder + "/T_Int_" + style + "_v" + TextureVersion + ".asset";
            string normalPath = TextureFolder + "/T_Int_" + style + "_N_v" + TextureVersion + ".asset";
            albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            normal = AssetDatabase.LoadAssetAtPath<Texture2D>(normalPath);
            InteriorTextures.Result result = null;
            if (albedo == null || normal == null)
            {
                result = InteriorTextures.Make(style, TextureSize);
                albedo = SaveTexture(result.Albedo, albedoPath, false);
                normal = SaveTexture(InteriorTextures.Normals(result), normalPath, true);
                meters = result.Meters;
            }
            else
            {
                // L'échelle (mètres par répétition) ne dépend que du style : on la redemande sans les pixels.
                meters = InteriorTextures.Make(style, 8).Meters;
            }

            Albedos[style] = albedo;
            Normals[style] = normal;
            Meters[style] = meters;
        }

        private static Texture2D SaveTexture(Color[] pixels, string path, bool linear)
        {
            Texture2D texture = new Texture2D(TextureSize, TextureSize, TextureFormat.RGBA32, true, linear)
            {
                wrapMode = TextureWrapMode.Repeat,
                filterMode = FilterMode.Trilinear,
                anisoLevel = 8
            };
            texture.SetPixels(pixels);
            texture.Apply(true);
            AssetDatabase.CreateAsset(texture, path);
            return texture;
        }
    }
}
