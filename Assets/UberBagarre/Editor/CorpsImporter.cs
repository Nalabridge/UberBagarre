using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les corps réalistes : lecture des fichiers « Corps_*.bytes » produits par
    /// Tools/corps/fabrique.py (MakeHuman, licence CC0), et construction dans Unity.
    ///
    /// Un fichier contient UN corps et TOUTES ses tenues : la peau découpée par zones (chaque
    /// zone sait quels vêtements la cachent), les yeux, le tee-shirt, la veste, le débardeur,
    /// le jean, la ceinture, les chaussures et leurs semelles. Pour une tenue donnée, on
    /// assemble un maillage qui ne garde que ce qui se voit — pas de peau qui traverse le
    /// tissu, et rien d'inutile à animer. Le maillage est enregistré comme asset (dossier
    /// Generes) et partagé par tous les personnages qui portent la même tenue.
    ///
    /// Le squelette est reconstruit avec les noms que le reste du jeu attend (Pelvis, Spine,
    /// Chest, Neck, Head, LeftUpperArm…), directement depuis les positions de liaison du
    /// fichier : les os tombent exactement sur les articulations du corps.
    /// </summary>
    public static class CorpsImporter
    {
        public const string Folder = "Assets/UberBagarre/Art/Models/Corps";
        public const string GeneratedFolder = "Assets/UberBagarre/Art/Models/Corps/Generes";
        public const string MaterialsFolder = "Assets/UberBagarre/Art/Models/Corps/Matieres";

        private const string Magic = "UBCORPS2";

        // Bits de zones de peau (voir vetements.py).
        private const int Jean = 8;
        private const int Chaussures = 16;
        private const int Tete = 32;

        /// <summary>Le haut porté. La valeur est le bit du vêtement dans les masques de peau.</summary>
        public enum Top
        {
            TShirt = 1,
            Veste = 2,
            Debardeur = 4
        }

        /// <summary>Emplacements de matière, dans l'ordre des sous-maillages.</summary>
        public static readonly string[] Slots = { "Peau", "Yeux", "Haut", "Jean", "Ceinture", "Chaussures", "Semelle" };

        public sealed class Data
        {
            public string Name;
            public string[] BoneNames;
            public int[] Parents;
            public Vector3[] BindPositions;
            public Quaternion[] BindRotations;
            public Vector3[] Positions;
            public Vector3[] Normals;
            public Vector2[] Uvs;
            public byte[] BoneIds;
            public float[] Weights;
            public Dictionary<string, int[]> Submeshes;
            public DateTime Stamp;

            public int Bone(string name)
            {
                return Array.IndexOf(BoneNames, name);
            }

            public Vector3 BonePosition(string name)
            {
                int i = Bone(name);
                return i >= 0 ? BindPositions[i] : Vector3.zero;
            }
        }

        /// <summary>Un corps construit : son rendu et ses os, par nom.</summary>
        public sealed class Built
        {
            public Data Data;
            public SkinnedMeshRenderer Renderer;
            public Transform[] Bones;
            public readonly Dictionary<string, Transform> ByName = new Dictionary<string, Transform>();

            public Transform this[string name]
            {
                get
                {
                    Transform t;
                    return ByName.TryGetValue(name, out t) ? t : null;
                }
            }
        }

        private static readonly Dictionary<string, Data> Cache = new Dictionary<string, Data>();
        private static readonly Dictionary<string, Mesh> Meshes = new Dictionary<string, Mesh>();
        private static readonly Dictionary<string, Material> SkinMaterials = new Dictionary<string, Material>();

        public static bool Exists(string silhouette)
        {
            return File.Exists(Folder + "/Corps_" + silhouette + ".bytes");
        }

        // ------------------------------------------------------------------ lecture

        public static Data Load(string silhouette, bool crowd)
        {
            string key = silhouette + (crowd ? "_Foule" : "");
            string path = Folder + "/Corps_" + key + ".bytes";

            if (!File.Exists(path))
            {
                if (crowd) return Load(silhouette, false);
                Debug.LogError("[UberBagarre] Corps introuvable : " + path + ". Lance Tools/corps/fabrique.py.");
                return null;
            }

            DateTime stamp = File.GetLastWriteTimeUtc(path);
            Data data;
            if (Cache.TryGetValue(key, out data) && data.Stamp == stamp) return data;

            data = Parse(File.ReadAllBytes(path));
            data.Name = key;
            data.Stamp = stamp;
            Cache[key] = data;
            return data;
        }

        private static Data Parse(byte[] compressed)
        {
            byte[] bytes;
            using (MemoryStream input = new MemoryStream(compressed))
            using (GZipStream gzip = new GZipStream(input, CompressionMode.Decompress))
            using (MemoryStream output = new MemoryStream())
            {
                gzip.CopyTo(output);
                bytes = output.ToArray();
            }

            using (BinaryReader reader = new BinaryReader(new MemoryStream(bytes)))
            {
                string magic = Encoding.ASCII.GetString(reader.ReadBytes(8));
                if (magic != Magic) throw new InvalidDataException("Fichier de corps inattendu (" + magic + ")");

                Data data = new Data();

                int boneCount = reader.ReadInt32();
                data.BoneNames = new string[boneCount];
                data.Parents = new int[boneCount];
                data.BindPositions = new Vector3[boneCount];
                data.BindRotations = new Quaternion[boneCount];

                for (int i = 0; i < boneCount; i++)
                {
                    data.BoneNames[i] = ReadString(reader);
                    data.Parents[i] = reader.ReadInt32();
                    data.BindPositions[i] = ReadVector3(reader);
                    data.BindRotations[i] = new Quaternion(reader.ReadSingle(), reader.ReadSingle(),
                        reader.ReadSingle(), reader.ReadSingle());
                }

                int n = reader.ReadInt32();
                data.Positions = new Vector3[n];
                data.Normals = new Vector3[n];
                data.Uvs = new Vector2[n];

                for (int i = 0; i < n; i++) data.Positions[i] = ReadVector3(reader);
                for (int i = 0; i < n; i++) data.Normals[i] = ReadVector3(reader);
                for (int i = 0; i < n; i++) data.Uvs[i] = new Vector2(reader.ReadSingle(), reader.ReadSingle());

                data.BoneIds = reader.ReadBytes(n * 4);
                data.Weights = new float[n * 4];
                for (int i = 0; i < n * 4; i++) data.Weights[i] = reader.ReadSingle();

                int subCount = reader.ReadInt32();
                data.Submeshes = new Dictionary<string, int[]>();

                for (int s = 0; s < subCount; s++)
                {
                    string name = ReadString(reader);
                    int count = reader.ReadInt32();
                    int[] indices = new int[count];
                    for (int i = 0; i < count; i++) indices[i] = reader.ReadInt32();
                    data.Submeshes[name] = indices;
                }

                return data;
            }
        }

        private static string ReadString(BinaryReader reader)
        {
            int length = reader.ReadInt32();
            return Encoding.UTF8.GetString(reader.ReadBytes(length));
        }

        private static Vector3 ReadVector3(BinaryReader reader)
        {
            return new Vector3(reader.ReadSingle(), reader.ReadSingle(), reader.ReadSingle());
        }

        // ------------------------------------------------------------------ maillage

        /// <summary>
        /// Le maillage d'une tenue : peau visible + vêtements portés, sommets compactés.
        /// Les sous-maillages suivent <see cref="Slots"/>, les emplacements vides en moins ;
        /// <paramref name="slots"/> dit lesquels restent, dans l'ordre.
        /// </summary>
        public static Mesh BuildMesh(Data data, Top top, bool withHead, out List<string> slots)
        {
            string key = data.Name + "_" + top + (withHead ? "" : "_SansTete");
            int worn = (int)top | Jean | Chaussures | (withHead ? 0 : Tete);

            Dictionary<string, List<int>> bySlot = new Dictionary<string, List<int>>();
            foreach (KeyValuePair<string, int[]> pair in data.Submeshes)
            {
                string slot = SlotOf(pair.Key, top, withHead, worn);
                if (slot == null) continue;

                List<int> list;
                if (!bySlot.TryGetValue(slot, out list))
                {
                    list = new List<int>();
                    bySlot[slot] = list;
                }

                list.AddRange(pair.Value);
            }

            slots = new List<string>();
            for (int i = 0; i < Slots.Length; i++)
            {
                if (bySlot.ContainsKey(Slots[i])) slots.Add(Slots[i]);
            }

            Mesh cached;
            if (Meshes.TryGetValue(key, out cached) && cached != null && cached.name == key + "@" + data.Stamp.Ticks)
            {
                return cached;
            }

            // Compactage : seuls les sommets réellement utilisés par la tenue.
            int[] remap = new int[data.Positions.Length];
            for (int i = 0; i < remap.Length; i++) remap[i] = -1;

            List<int> order = new List<int>();
            for (int s = 0; s < slots.Count; s++)
            {
                List<int> tris = bySlot[slots[s]];
                for (int i = 0; i < tris.Count; i++)
                {
                    int v = tris[i];
                    if (remap[v] >= 0) continue;
                    remap[v] = order.Count;
                    order.Add(v);
                }
            }

            int count = order.Count;
            Vector3[] positions = new Vector3[count];
            Vector3[] normals = new Vector3[count];
            Vector2[] uvs = new Vector2[count];
            List<Vector3> rest = new List<Vector3>(count);
            BoneWeight[] weights = new BoneWeight[count];

            for (int i = 0; i < count; i++)
            {
                int v = order[i];
                positions[i] = data.Positions[v];
                normals[i] = data.Normals[v];
                uvs[i] = data.Uvs[v];
                rest.Add(data.Positions[v]);

                BoneWeight w = new BoneWeight();
                w.boneIndex0 = data.BoneIds[v * 4];
                w.boneIndex1 = data.BoneIds[v * 4 + 1];
                w.boneIndex2 = data.BoneIds[v * 4 + 2];
                w.boneIndex3 = data.BoneIds[v * 4 + 3];
                w.weight0 = data.Weights[v * 4];
                w.weight1 = data.Weights[v * 4 + 1];
                w.weight2 = data.Weights[v * 4 + 2];
                w.weight3 = data.Weights[v * 4 + 3];
                weights[i] = w;
            }

            Mesh mesh = new Mesh();
            mesh.name = key + "@" + data.Stamp.Ticks;
            mesh.indexFormat = count > 65000 ? IndexFormat.UInt32 : IndexFormat.UInt16;
            mesh.vertices = positions;
            mesh.normals = normals;
            mesh.uv = uvs;

            // Canal 3 : la position de repos. Le shader de peau y pose les bleus, pour qu'ils
            // restent collés à la chair quand le membre bouge (voir BruiseSystem).
            mesh.SetUVs(2, rest);
            mesh.boneWeights = weights;

            Matrix4x4[] bindposes = new Matrix4x4[data.BoneNames.Length];
            for (int b = 0; b < bindposes.Length; b++)
            {
                bindposes[b] = Matrix4x4.TRS(data.BindPositions[b], data.BindRotations[b], Vector3.one).inverse;
            }

            mesh.bindposes = bindposes;
            mesh.subMeshCount = slots.Count;

            for (int s = 0; s < slots.Count; s++)
            {
                List<int> tris = bySlot[slots[s]];
                int[] local = new int[tris.Count];
                for (int i = 0; i < local.Length; i++) local[i] = remap[tris[i]];
                mesh.SetTriangles(local, s, false);
            }

            mesh.RecalculateBounds();
            mesh.RecalculateTangents();

            mesh = SaveMesh(mesh, key);
            Meshes[key] = mesh;
            return mesh;
        }

        private static string SlotOf(string submesh, Top top, bool withHead, int worn)
        {
            if (submesh.StartsWith("Peau."))
            {
                int mask;
                if (!int.TryParse(submesh.Substring(5), out mask)) return null;
                return (mask & worn) == 0 ? "Peau" : null;
            }

            if (submesh == "Yeux") return withHead ? "Yeux" : null;
            if (submesh == top.ToString()) return "Haut";
            if (submesh == "Jean" || submesh == "Ceinture" || submesh == "Chaussures" || submesh == "Semelle") return submesh;
            return null;
        }

        private static Mesh SaveMesh(Mesh mesh, string key)
        {
            EditorBuildUtility.EnsureFolder(GeneratedFolder);
            string path = GeneratedFolder + "/Corps_" + key + ".asset";

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            // Mise à jour en place : les scènes déjà construites gardent leur référence.
            EditorUtility.CopySerialized(mesh, existing);
            UnityEngine.Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        // ------------------------------------------------------------------ squelette

        /// <summary>
        /// Crée les os sous <paramref name="root"/>, place le rendu skinné, et rend le tout.
        /// Les positions du fichier sont celles du corps debout, pieds au sol, dans l'espace de
        /// <paramref name="root"/>.
        /// </summary>
        public static Built BuildSkeleton(Data data, Transform root, Mesh mesh, Material[] materials)
        {
            Built built = new Built();
            built.Data = data;
            built.Bones = new Transform[data.BoneNames.Length];

            for (int i = 0; i < data.BoneNames.Length; i++)
            {
                GameObject go = new GameObject(data.BoneNames[i]);
                int parent = data.Parents[i];
                Transform parentTransform = parent >= 0 ? built.Bones[parent] : root;
                go.transform.SetParent(parentTransform, false);

                if (parent >= 0)
                {
                    Quaternion parentRotation = data.BindRotations[parent];
                    go.transform.localPosition = Quaternion.Inverse(parentRotation) *
                                                 (data.BindPositions[i] - data.BindPositions[parent]);
                    go.transform.localRotation = Quaternion.Inverse(parentRotation) * data.BindRotations[i];
                }
                else
                {
                    go.transform.localPosition = data.BindPositions[i];
                    go.transform.localRotation = data.BindRotations[i];
                }

                built.Bones[i] = go.transform;
                built.ByName[data.BoneNames[i]] = go.transform;
            }

            GameObject skin = new GameObject("Corps");
            skin.transform.SetParent(root, false);

            SkinnedMeshRenderer renderer = skin.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = built.Bones;
            renderer.rootBone = built["Pelvis"];
            renderer.quality = SkinQuality.Bone4;
            renderer.sharedMaterials = materials;
            renderer.skinnedMotionVectors = true;

            // Boîte large autour du bassin : un corps plié, couché ou en pleine frappe ne doit
            // jamais disparaître parce que sa boîte de repos est sortie du champ.
            renderer.localBounds = new Bounds(new Vector3(0f, -0.1f, 0f), new Vector3(2.4f, 2.6f, 2.4f));

            built.Renderer = renderer;
            return built;
        }

        // ------------------------------------------------------------------ matières

        /// <summary>
        /// Les matières d'une tenue, dans l'ordre des emplacements. La peau et les yeux
        /// viennent du corps ; le haut, le bas et les chaussures de la palette du personnage.
        /// </summary>
        public static Material[] MaterialsFor(string silhouette, List<string> slots, Material top, Material pants, Material shoes)
        {
            Material[] materials = new Material[slots.Count];

            for (int i = 0; i < slots.Count; i++)
            {
                switch (slots[i])
                {
                    case "Peau": materials[i] = SkinMaterial(silhouette); break;
                    case "Yeux": materials[i] = EyeMaterial(); break;
                    case "Haut": materials[i] = top; break;
                    case "Jean": materials[i] = pants; break;
                    case "Ceinture": materials[i] = Plain("M_Ceinture", new Color(0.075f, 0.05f, 0.035f), 0.5f); break;
                    case "Chaussures": materials[i] = shoes; break;
                    case "Semelle": materials[i] = Plain("M_Semelle", new Color(0.80f, 0.78f, 0.74f), 0.18f); break;
                }
            }

            return materials;
        }

        public static Material SkinMaterial(string silhouette)
        {
            string key = silhouette.Replace("_Foule", "");
            Material cached;
            if (SkinMaterials.TryGetValue(key, out cached) && cached != null) return cached;

            string albedoPath = Folder + "/Peau_" + key + "_Albedo.jpg";
            string reliefPath = Folder + "/Peau_" + key + "_Relief.jpg";
            ConfigureTexture(albedoPath, false);
            ConfigureTexture(reliefPath, true);

            Texture2D albedo = AssetDatabase.LoadAssetAtPath<Texture2D>(albedoPath);
            Texture2D relief = AssetDatabase.LoadAssetAtPath<Texture2D>(reliefPath);

            Material material = EditorBuildUtility.CreateOrUpdateEffectMaterial(MaterialsFolder, "M_Peau_" + key, "UberBagarre/Peau");
            if (material == null)
            {
                material = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Peau_" + key, Color.white, 0.3f, 0f, albedo);
            }

            if (material.HasProperty("_MainTex") && albedo != null) material.SetTexture("_MainTex", albedo);
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);

            // La peau a un léger voile humide : un peu de lissage, pas du vinyle.
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.34f);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_MarkSpace")) material.SetFloat("_MarkSpace", 1f);
            if (relief != null) EditorBuildUtility.ApplyNormalMap(material, relief, 1f);

            EditorUtility.SetDirty(material);
            SkinMaterials[key] = material;
            return material;
        }

        private static Material EyeMaterial()
        {
            string path = Folder + "/Yeux_Marron.png";
            ConfigureTexture(path, false);
            Texture2D texture = AssetDatabase.LoadAssetAtPath<Texture2D>(path);
            return EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Yeux", Color.white, 0.86f, 0f, texture);
        }

        private static Material Plain(string name, Color color, float smoothness)
        {
            return EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, name, color, smoothness, 0f);
        }

        private static void ConfigureTexture(string path, bool normalMap)
        {
            TextureImporter importer = AssetImporter.GetAtPath(path) as TextureImporter;
            if (importer == null) return;

            bool changed = false;
            TextureImporterType type = normalMap ? TextureImporterType.NormalMap : TextureImporterType.Default;

            if (importer.textureType != type)
            {
                importer.textureType = type;
                changed = true;
            }

            if (importer.maxTextureSize != 2048)
            {
                importer.maxTextureSize = 2048;
                changed = true;
            }

            if (importer.anisoLevel != 4)
            {
                importer.anisoLevel = 4;
                changed = true;
            }

            if (changed) importer.SaveAndReimport();
        }
    }
}
