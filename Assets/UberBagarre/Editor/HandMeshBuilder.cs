using System.Collections.Generic;
using UberBagarre.View;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Une vraie main : un seul maillage continu, déformé par les os des doigts.
    ///
    /// L'ancienne main était une paume presque cubique (8 × 7 × 7,6 cm), un cube à chaque
    /// jointure et quinze cylindres de rayon constant — « des saucisses sur un Rubik's cube ».
    /// Le défaut n'était pas le nombre de polygones, c'était la NATURE des pièces : des objets
    /// rigides posés les uns contre les autres ne peuvent pas ressembler à de la peau, parce
    /// que la peau est continue et qu'elle se plie.
    ///
    /// Ici la main est une seule surface, dont chaque sommet suit un ou deux os
    /// (<see cref="SkinnedMeshRenderer"/>). Aux articulations, les sommets sont partagés entre
    /// les deux phalanges : quand le doigt se ferme, la peau se plie au lieu de s'ouvrir en deux
    /// cylindres. Les os eux-mêmes ne changent pas — les poses de poing, de garde et de prise
    /// fonctionnent telles quelles.
    ///
    /// Ce qui fait qu'on lit une main et pas un gant de mousse :
    /// - une paume PLATE (3 cm), plus large aux jointures qu'au poignet, dont la rangée des
    ///   jointures n'est pas droite (l'index et le majeur dépassent, l'auriculaire recule) ;
    /// - l'éminence du pouce et celle de l'auriculaire, les deux bosses de la paume ;
    /// - des doigts à section OVALE (plus larges qu'épais, plus plats sur le dos), qui
    ///   s'affinent de la base au bout, avec un léger renflement à chaque articulation ;
    /// - une pulpe au bout des doigts, et un ongle.
    ///
    /// Les sommets sont exprimés dans le repère du poignet ; les matrices de liaison ne
    /// dépendent que de la hiérarchie de la main, identique pour tous les corps. Un seul
    /// maillage par côté sert donc à tout le monde.
    /// </summary>
    internal static class HandMeshBuilder
    {
        private const int RingSegments = 16;
        private const int PalmSegments = 22;

        internal class Finger
        {
            public Transform Proximal;
            public Transform Middle;
            public Transform Distal;
            public float DistalLength;
            public float Radius;
            public bool IsThumb;
        }

        private static readonly Mesh[] Cache = new Mesh[2];
        private static Material _nailMaterial;

        /// <summary>
        /// Habille la main : ajoute sous le poignet un rendu déformable lié au poignet, à la
        /// paume et aux quinze phalanges.
        /// </summary>
        public static SkinnedMeshRenderer Dress(Transform wrist, Transform palm, Finger[] fingers, HandSide side,
            Material flesh)
        {
            GameObject skinGo = EditorBuildUtility.CreateEmpty(side == HandSide.Left ? "LeftHandSkin" : "RightHandSkin",
                wrist, Vector3.zero);
            Transform skin = skinGo.transform;

            List<Transform> bones = new List<Transform>(2 + fingers.Length * 3);
            bones.Add(wrist);
            bones.Add(palm);
            for (int i = 0; i < fingers.Length; i++)
            {
                bones.Add(fingers[i].Proximal);
                bones.Add(fingers[i].Middle);
                bones.Add(fingers[i].Distal);
            }

            int cacheIndex = side == HandSide.Left ? 0 : 1;
            Mesh mesh = Cache[cacheIndex];

            if (mesh == null)
            {
                mesh = Generate(skin, palm, fingers, side);
                mesh = Store(mesh, side == HandSide.Left ? "M_MainGauche" : "M_MainDroite");
                Cache[cacheIndex] = mesh;
            }

            SkinnedMeshRenderer renderer = skinGo.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones.ToArray();
            renderer.rootBone = wrist;
            renderer.quality = SkinQuality.Bone2;
            renderer.sharedMaterials = new[] { flesh, NailMaterial() };

            // Boîte englobante fixe, assez grande pour une main ouverte comme fermée : sans elle,
            // Unity recalcule ou se trompe, et la main disparaît quand elle frôle le bord de l'écran.
            renderer.localBounds = new Bounds(new Vector3(0f, 0f, 0.06f), new Vector3(0.22f, 0.16f, 0.26f));
            renderer.updateWhenOffscreen = false;

            return renderer;
        }

        private static Material NailMaterial()
        {
            if (_nailMaterial != null) return _nailMaterial;

            // Un ongle est plus clair, plus rose et plus lisse que la peau autour : c'est ce
            // contraste minuscule qui dit « bout de doigt » à trois mètres.
            _nailMaterial = EditorBuildUtility.CreateOrUpdateMaterial(NightMaterialFactory.MaterialsFolder,
                "M_Ongle", new Color(0.86f, 0.70f, 0.66f), 0.46f, 0f);
            return _nailMaterial;
        }

        private static Mesh Store(Mesh mesh, string name)
        {
            EditorBuildUtility.EnsureFolder(ProceduralMeshFactory.MeshesFolder);
            string path = ProceduralMeshFactory.MeshesFolder + "/" + name + ".asset";
            mesh.name = name;

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            // Réécrire le contenu garde l'identifiant de l'asset : les scènes déjà construites
            // gardent une référence valide.
            existing.Clear();
            existing.vertices = mesh.vertices;
            existing.normals = mesh.normals;
            existing.uv = mesh.uv;
            existing.boneWeights = mesh.boneWeights;
            existing.bindposes = mesh.bindposes;
            existing.subMeshCount = mesh.subMeshCount;
            for (int i = 0; i < mesh.subMeshCount; i++) existing.SetTriangles(mesh.GetTriangles(i), i);
            existing.RecalculateBounds();
            existing.RecalculateTangents();
            EditorUtility.SetDirty(existing);

            Object.DestroyImmediate(mesh);
            return existing;
        }

        // ------------------------------------------------------------------ génération

        private class Builder
        {
            public readonly List<Vector3> Vertices = new List<Vector3>(4096);
            public readonly List<Vector2> Uvs = new List<Vector2>(4096);
            public readonly List<BoneWeight> Weights = new List<BoneWeight>(4096);
            public readonly List<int> Skin = new List<int>(16384);
            public readonly List<int> Nails = new List<int>(512);

            /// <summary>Paires de sommets confondus (couture de texture) dont les normales seront moyennées.</summary>
            public readonly List<int> SeamA = new List<int>(512);
            public readonly List<int> SeamB = new List<int>(512);

            public int Add(Vector3 position, Vector2 uv, BoneWeight weight)
            {
                Vertices.Add(position);
                Uvs.Add(uv);
                Weights.Add(weight);
                return Vertices.Count - 1;
            }
        }

        private static Mesh Generate(Transform space, Transform palm, Finger[] fingers, HandSide side)
        {
            float sign = side == HandSide.Left ? -1f : 1f;
            Builder b = new Builder();

            BuildPalm(b, space, palm, fingers, sign);

            for (int i = 0; i < fingers.Length; i++)
            {
                BuildFinger(b, space, fingers[i], 2 + i * 3);
            }

            Mesh mesh = new Mesh();
            mesh.name = "Main";
            mesh.SetVertices(b.Vertices);
            mesh.SetUVs(0, b.Uvs);
            mesh.boneWeights = b.Weights.ToArray();
            mesh.subMeshCount = 2;
            mesh.SetTriangles(b.Skin, 0);
            mesh.SetTriangles(b.Nails, 1);
            mesh.RecalculateNormals();

            // Couture : les deux sommets d'une même position reçoivent la même normale, sinon
            // une ligne d'ombre court le long de chaque doigt.
            Vector3[] normals = mesh.normals;
            for (int i = 0; i < b.SeamA.Count; i++)
            {
                Vector3 n = (normals[b.SeamA[i]] + normals[b.SeamB[i]]).normalized;
                normals[b.SeamA[i]] = n;
                normals[b.SeamB[i]] = n;
            }

            mesh.normals = normals;
            mesh.RecalculateTangents();
            mesh.RecalculateBounds();

            // Matrices de liaison : pose de repos de chaque os, vue depuis le rendu. Elles ne
            // dépendent que de la hiérarchie relative, identique pour tous les corps.
            List<Transform> bones = new List<Transform>();
            bones.Add(space.parent);
            bones.Add(palm);
            for (int i = 0; i < fingers.Length; i++)
            {
                bones.Add(fingers[i].Proximal);
                bones.Add(fingers[i].Middle);
                bones.Add(fingers[i].Distal);
            }

            Matrix4x4[] bindposes = new Matrix4x4[bones.Count];
            for (int i = 0; i < bones.Count; i++)
            {
                bindposes[i] = bones[i].worldToLocalMatrix * space.localToWorldMatrix;
            }

            mesh.bindposes = bindposes;
            return mesh;
        }

        // ------------------------------------------------------------------ paume

        /// <summary>
        /// La paume, en tranches du poignet vers les jointures. Chaque tranche est une ellipse
        /// « carrée » (super-ellipse) : plate sur le dos, pleine côté paume. La tranche n'est pas
        /// plane : son bord avant suit la rangée des jointures, plus avancée côté index.
        /// </summary>
        private static void BuildPalm(Builder b, Transform space, Transform palm, Finger[] fingers, float sign)
        {
            // Bases des quatre doigts longs, dans le repère de la paume, côté pouce positif.
            List<Vector2> bases = new List<Vector2>();
            Vector3 thumbBase = Vector3.zero;

            for (int i = 0; i < fingers.Length; i++)
            {
                Vector3 local = palm.InverseTransformPoint(fingers[i].Proximal.position);
                if (fingers[i].IsThumb)
                {
                    thumbBase = local;
                    continue;
                }

                bases.Add(new Vector2(local.x * sign, local.z));
            }

            bases.Sort((p, q) => p.x.CompareTo(q.x));

            const float back = -0.070f;
            const int rings = 14;

            int[] ringStart = new int[rings + 4];
            int ringCount = 0;

            for (int r = 0; r < rings; r++)
            {
                float u = r / (float)(rings - 1);

                // Tranches plus serrees vers les jointures, ou la forme change le plus.
                u = 1f - (1f - u) * (1f - u) * 0.55f - (1f - u) * 0.45f;

                ringStart[ringCount++] = AddPalmRing(b, space, palm, bases, thumbBase, sign, back, u, 1f, 0f);
            }

            // Bord avant arrondi : trois tranches qui se resserrent, puis le centre.
            float[] shrink = { 0.86f, 0.62f, 0.32f };
            float[] advance = { 0.0035f, 0.0062f, 0.0078f };

            for (int k = 0; k < shrink.Length; k++)
            {
                ringStart[ringCount++] = AddPalmRing(b, space, palm, bases, thumbBase, sign, back, 1f, shrink[k], advance[k]);
            }

            for (int r = 0; r < ringCount - 1; r++)
            {
                Quads(b.Skin, ringStart[r], ringStart[r + 1], PalmSegments);
            }

            // Fond, côté avant-bras : fermé, au cas où la manche laisse voir l'intérieur.
            Vector3 backCenter = space.InverseTransformPoint(palm.TransformPoint(new Vector3(0f, 0f, back)));
            int center = b.Add(backCenter, new Vector2(0.5f, 0f), Weight(0, 1f, 1, 0f));

            for (int j = 0; j < PalmSegments; j++)
            {
                b.Skin.Add(center);
                b.Skin.Add(ringStart[0] + j + 1);
                b.Skin.Add(ringStart[0] + j);
            }

            Vector3 lastCenter = Vector3.zero;
            int last = ringStart[ringCount - 1];
            for (int j = 0; j < PalmSegments; j++) lastCenter += b.Vertices[last + j];
            lastCenter /= PalmSegments;

            int tip = b.Add(lastCenter, new Vector2(0.5f, 1f), Weight(1, 1f, 0, 0f));

            for (int j = 0; j < PalmSegments; j++)
            {
                b.Skin.Add(last + j);
                b.Skin.Add(last + j + 1);
                b.Skin.Add(tip);
            }
        }

        private static int AddPalmRing(Builder b, Transform space, Transform palm, List<Vector2> bases,
            Vector3 thumbBase, float sign, float back, float u, float scale, float advance)
        {
            // Largeur et épaisseur le long de la paume : poignet étroit, jointures larges.
            float halfWidth = Mathf.Lerp(0.027f, 0.044f, Smooth(Mathf.Clamp01(u * 1.35f)));
            float top = Mathf.Lerp(0.017f, 0.0115f, u);
            float bottom = Mathf.Lerp(0.018f, 0.0145f, u);
            float centerY = Mathf.Lerp(0.001f, -0.005f, u);

            int start = b.Vertices.Count;

            for (int j = 0; j <= PalmSegments; j++)
            {
                float theta = j / (float)PalmSegments * Mathf.PI * 2f;
                float c = Mathf.Cos(theta);
                float s = Mathf.Sin(theta);

                float cx = Mathf.Sign(c) * Mathf.Pow(Mathf.Abs(c), 0.75f);
                float cy = Mathf.Sign(s) * Mathf.Pow(Mathf.Abs(s), 0.75f);

                float x = halfWidth * cx * scale;
                float xs = x * sign;
                float y = centerY + (cy > 0f ? top : bottom) * cy * scale;

                if (cy < 0f)
                {
                    // Éminence du pouce (grosse et basse), éminence de l'auriculaire (plus fine),
                    // coussinets sous les jointures.
                    float thenar = 0.0075f * Gauss(xs - 0.024f, 0.015f) * Gauss(u - 0.32f, 0.26f);
                    float hypothenar = 0.0045f * Gauss(xs + 0.028f, 0.012f) * Gauss(u - 0.42f, 0.28f);
                    float pads = 0.0025f * Gauss(u - 0.93f, 0.09f) * Gauss(xs, 0.03f);
                    y -= (thenar + hypothenar + pads) * -cy * scale;
                }
                else
                {
                    // Les jointures pointent sous la peau du dos de la main.
                    float knuckles = 0f;
                    for (int k = 0; k < bases.Count; k++)
                    {
                        knuckles += Gauss(xs - bases[k].x, 0.0075f);
                    }

                    y += 0.0032f * knuckles * Gauss(u - 1f, 0.07f) * cy * scale;
                }

                // Le bord avant suit la rangée des jointures ; le poignet est droit.
                float front = FrontEdge(bases, xs) + 0.0045f;
                float z = Mathf.Lerp(back, front, u) + advance;

                Vector3 local = new Vector3(x, y, z);
                Vector3 position = space.InverseTransformPoint(palm.TransformPoint(local));

                // Côté poignet : le poignet porte, puis la paume prend le relais.
                float palmWeight = Smooth(Mathf.InverseLerp(-0.062f, -0.030f, z));

                int index = b.Add(position, new Vector2(j / (float)PalmSegments, u * 0.9f),
                    Weight(1, palmWeight, 0, 1f - palmWeight));

                if (j == PalmSegments)
                {
                    b.SeamA.Add(start);
                    b.SeamB.Add(index);
                }
            }

            return start;
        }

        private static float FrontEdge(List<Vector2> bases, float xs)
        {
            if (bases.Count == 0) return 0.03f;
            if (xs <= bases[0].x) return bases[0].y - (bases[0].x - xs) * 0.35f;
            if (xs >= bases[bases.Count - 1].x) return bases[bases.Count - 1].y - (xs - bases[bases.Count - 1].x) * 0.35f;

            for (int i = 0; i < bases.Count - 1; i++)
            {
                if (xs > bases[i + 1].x) continue;

                float t = Mathf.InverseLerp(bases[i].x, bases[i + 1].x, xs);
                return Mathf.Lerp(bases[i].y, bases[i + 1].y, Smooth(t));
            }

            return bases[bases.Count - 1].y;
        }

        // ------------------------------------------------------------------ doigts

        /// <summary>
        /// Un doigt : un tube continu de l'intérieur de la paume jusqu'au bout, fermé par une
        /// pulpe arrondie. La section est ovale et s'affine vers le bout, avec un léger
        /// renflement à chaque articulation — c'est ce qui distingue un doigt d'une saucisse.
        /// </summary>
        private static void BuildFinger(Builder b, Transform space, Finger finger, int firstBone)
        {
            Transform p = finger.Proximal;
            Transform m = finger.Middle;
            Transform d = finger.Distal;

            // Les jointures du squelette sont espacées de 1,7 cm : un doigt plus large que ça
            // chevauche son voisin et la main redevient un paquet de saucisses. 0,84 donne des
            // doigts de 1,9 cm à la base, qui se touchent juste quand ils sont serrés.
            float radius = finger.Radius * (finger.IsThumb ? 0.95f : 0.84f);
            float inset = finger.IsThumb ? 0.024f : 0.013f;

            Vector3 axis = space.InverseTransformDirection(p.forward).normalized;
            Vector3 right = space.InverseTransformDirection(p.right).normalized;
            Vector3 up = space.InverseTransformDirection(p.up).normalized;

            Vector3 start = space.InverseTransformPoint(p.position) - axis * inset;
            Vector3 knuckle = space.InverseTransformPoint(p.position);
            Vector3 pip = space.InverseTransformPoint(m.position);
            Vector3 dip = space.InverseTransformPoint(d.position);
            Vector3 end = space.InverseTransformPoint(d.TransformPoint(new Vector3(0f, 0f, finger.DistalLength)));

            float sK = inset;
            float sJ1 = sK + Vector3.Distance(knuckle, pip);
            float sJ2 = sJ1 + Vector3.Distance(pip, dip);
            float sT = sJ2 + Vector3.Distance(dip, end);

            float tipRadius = radius * 0.80f;
            float capStart = sT - tipRadius * 0.92f;

            // Profil de rayon : [position le long du doigt, facteur].
            float lp = sJ1 - sK;
            float lm = sJ2 - sJ1;
            float ld = sT - sJ2;

            Vector2[] profile = finger.IsThumb
                ? new[]
                {
                    new Vector2(0f, 1.35f), new Vector2(sK, 1.15f), new Vector2(sK + lp * 0.5f, 1.0f),
                    new Vector2(sJ1, 1.0f), new Vector2(sJ1 + lm * 0.5f, 0.92f), new Vector2(sJ2, 0.92f),
                    new Vector2(sJ2 + ld * 0.5f, 0.90f), new Vector2(capStart, 0.80f)
                }
                : new[]
                {
                    new Vector2(0f, 1.08f), new Vector2(sK, 1.04f), new Vector2(sK + lp * 0.5f, 0.93f),
                    new Vector2(sJ1, 0.97f), new Vector2(sJ1 + lm * 0.5f, 0.86f), new Vector2(sJ2, 0.88f),
                    new Vector2(sJ2 + ld * 0.45f, 0.86f), new Vector2(capStart, 0.80f)
                };

            // Anneaux : réguliers, plus serrés autour des articulations.
            List<float> stations = new List<float>();
            int steps = 26;
            for (int i = 0; i <= steps; i++)
            {
                float t = i / (float)steps;
                stations.Add(Mathf.Lerp(0f, capStart, t));
            }

            int palmBone = 1;
            int[] chain = { palmBone, firstBone, firstBone + 1, firstBone + 2 };
            float[] bounds = { sK, sJ1, sJ2 };
            float[] blend = { radius * (finger.IsThumb ? 1.3f : 0.8f), radius * 0.6f, radius * 0.55f };

            List<int> ringStarts = new List<int>(stations.Count + 6);

            for (int i = 0; i < stations.Count; i++)
            {
                float s = stations[i];
                float scale = Profile(profile, s);

                // Pulpe : le bout des doigts est plus plein côté paume.
                float pad = 0.14f * Gauss(s - (sJ2 + ld * 0.55f), ld * 0.3f);

                ringStarts.Add(AddFingerRing(b, Centerline(start, axis, s), right, up, radius * scale,
                    pad, s, SkinWeight(s, chain, bounds, blend)));
            }

            // Bout du doigt : quart de sphère, légèrement aplati.
            Vector3 capCenter = Centerline(start, axis, capStart);
            BoneWeight tipWeight = SkinWeight(sT, chain, bounds, blend);

            for (int k = 1; k <= 4; k++)
            {
                float phi = k / 5f * Mathf.PI * 0.5f;
                Vector3 center = capCenter + axis * (tipRadius * 0.92f * Mathf.Sin(phi));
                float r = tipRadius * Mathf.Cos(phi);

                ringStarts.Add(AddFingerRing(b, center, right, up, r, 0.08f * Mathf.Cos(phi), capStart + k * 0.002f, tipWeight));
            }

            for (int r = 0; r < ringStarts.Count - 1; r++)
            {
                Quads(b.Skin, ringStarts[r], ringStarts[r + 1], RingSegments);
            }

            int last = ringStarts[ringStarts.Count - 1];
            int tip = b.Add(capCenter + axis * tipRadius * 0.92f, new Vector2(0.5f, sT * 10f), tipWeight);

            for (int j = 0; j < RingSegments; j++)
            {
                b.Skin.Add(last + j);
                b.Skin.Add(last + j + 1);
                b.Skin.Add(tip);
            }

            BuildNail(b, start, axis, right, up, radius, profile, sJ2, sT, ld, firstBone + 2);
        }

        private static int AddFingerRing(Builder b, Vector3 center, Vector3 right, Vector3 up, float radius,
            float pad, float s, BoneWeight weight)
        {
            int start = b.Vertices.Count;

            for (int j = 0; j <= RingSegments; j++)
            {
                float theta = j / (float)RingSegments * Mathf.PI * 2f;
                float c = Mathf.Cos(theta);
                float sn = Mathf.Sin(theta);

                // Section ovale : plus large qu'épaisse, plus plate sur le dos.
                float width = radius * 1.04f;
                float height = sn > 0f ? radius * 0.80f : radius * (0.90f + pad);

                Vector3 position = center + right * (c * width) + up * (sn * height);

                int index = b.Add(position, new Vector2(j / (float)RingSegments, s * 10f), weight);

                if (j == RingSegments)
                {
                    b.SeamA.Add(start);
                    b.SeamB.Add(index);
                }
            }

            return start;
        }

        /// <summary>
        /// L'ongle : une petite plaque bombée sur le dos de la dernière phalange, à peine
        /// au-dessus de la peau, avec son propre matériau.
        /// </summary>
        private static void BuildNail(Builder b, Vector3 start, Vector3 axis, Vector3 right, Vector3 up,
            float radius, Vector2[] profile, float sJ2, float sT, float ld, int bone)
        {
            const int across = 5;
            const int along = 5;

            float from = sJ2 + ld * 0.38f;
            float to = sT - radius * 0.34f;
            if (to <= from) return;

            BoneWeight weight = Weight(bone, 1f, 0, 0f);
            int first = b.Vertices.Count;

            for (int a = 0; a < along; a++)
            {
                float s = Mathf.Lerp(from, to, a / (float)(along - 1));
                float r = radius * Profile(profile, Mathf.Min(s, profile[profile.Length - 1].x));

                // Vers le bout, l'ongle suit l'arrondi du doigt qui se referme.
                float closing = Mathf.Clamp01((s - (to - radius * 0.3f)) / (radius * 0.3f));
                r *= 1f - closing * 0.12f;

                for (int c = 0; c < across; c++)
                {
                    float theta = Mathf.Lerp(52f, 128f, c / (float)(across - 1)) * Mathf.Deg2Rad;
                    Vector3 surface = Centerline(start, axis, s)
                                      + right * (Mathf.Cos(theta) * r * 1.04f)
                                      + up * (Mathf.Sin(theta) * r * 0.80f + 0.0007f);

                    b.Add(surface, new Vector2(c / (float)(across - 1), a / (float)(along - 1)), weight);
                }
            }

            for (int a = 0; a < along - 1; a++)
            {
                for (int c = 0; c < across - 1; c++)
                {
                    int i0 = first + a * across + c;
                    int i1 = i0 + 1;
                    int i2 = i0 + across;
                    int i3 = i2 + 1;

                    // Même ordre que les anneaux du doigt : la face visible regarde vers l'extérieur,
                    // donc vers le dos de la main.
                    b.Nails.Add(i0);
                    b.Nails.Add(i1);
                    b.Nails.Add(i2);

                    b.Nails.Add(i1);
                    b.Nails.Add(i3);
                    b.Nails.Add(i2);
                }
            }
        }

        // ------------------------------------------------------------------ outils

        private static Vector3 Centerline(Vector3 start, Vector3 axis, float s)
        {
            return start + axis * s;
        }

        /// <summary>Anneaux consécutifs reliés par des quadrilatères, normales vers l'extérieur.</summary>
        private static void Quads(List<int> triangles, int ringA, int ringB, int segments)
        {
            for (int j = 0; j < segments; j++)
            {
                int a = ringA + j;
                int bIndex = ringA + j + 1;
                int c = ringB + j;
                int d = ringB + j + 1;

                triangles.Add(a);
                triangles.Add(bIndex);
                triangles.Add(c);

                triangles.Add(bIndex);
                triangles.Add(d);
                triangles.Add(c);
            }
        }

        /// <summary>
        /// Poids d'un anneau : entièrement à sa phalange, sauf près d'une articulation où il se
        /// partage entre les deux os. C'est ce partage qui plie la peau au lieu de la fendre.
        /// </summary>
        private static BoneWeight SkinWeight(float s, int[] chain, float[] bounds, float[] blend)
        {
            for (int j = 0; j < bounds.Length; j++)
            {
                float delta = s - bounds[j];
                if (Mathf.Abs(delta) < blend[j])
                {
                    float t = Smooth(Mathf.InverseLerp(-blend[j], blend[j], delta));
                    return Weight(chain[j + 1], t, chain[j], 1f - t);
                }
            }

            int segment = 0;
            while (segment < bounds.Length && s >= bounds[segment]) segment++;

            return Weight(chain[segment], 1f, 0, 0f);
        }

        private static BoneWeight Weight(int boneA, float weightA, int boneB, float weightB)
        {
            BoneWeight weight = new BoneWeight();

            if (weightB > weightA)
            {
                int bone = boneA;
                boneA = boneB;
                boneB = bone;

                float w = weightA;
                weightA = weightB;
                weightB = w;
            }

            float total = Mathf.Max(1e-5f, weightA + weightB);
            weight.boneIndex0 = boneA;
            weight.weight0 = weightA / total;
            weight.boneIndex1 = boneB;
            weight.weight1 = weightB / total;
            return weight;
        }

        private static float Profile(Vector2[] keys, float s)
        {
            if (s <= keys[0].x) return keys[0].y;

            for (int i = 0; i < keys.Length - 1; i++)
            {
                if (s > keys[i + 1].x) continue;

                float t = Mathf.InverseLerp(keys[i].x, keys[i + 1].x, s);
                return Mathf.Lerp(keys[i].y, keys[i + 1].y, Smooth(t));
            }

            return keys[keys.Length - 1].y;
        }

        private static float Smooth(float t)
        {
            t = Mathf.Clamp01(t);
            return t * t * (3f - 2f * t);
        }

        private static float Gauss(float delta, float width)
        {
            float k = delta / Mathf.Max(1e-5f, width);
            return Mathf.Exp(-k * k);
        }
    }
}
