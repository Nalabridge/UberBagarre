using System.Collections.Generic;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Donne un habitacle à chaque voiture de la ville : les modèles de la carte n'ont qu'une
    /// coque (vue du dedans, elle ne se dessine pas : on voit dehors).
    ///
    /// La coque est d'abord mesurée : des rayons tirés d'en haut, tous les 5 cm, le long de
    /// l'axe et sur les côtés, donnent la silhouette — hauteur et étendue du toit, hauteur du
    /// capot, et le pied du pare-brise (là où la silhouette, en partant du toit vers l'avant,
    /// redescend au niveau du capot). L'habitacle (<see cref="CarCabinPlan"/>) est taillé sur ces
    /// mesures, puis construit comme les intérieurs des magasins (mêmes matières, mêmes textures)
    /// et rattaché à la voiture, avec son volant et ses aiguilles mobiles.
    /// </summary>
    internal static class CarCabinBuilder
    {
        public sealed class Result
        {
            public CarCabinPlan.Cabin Cabin;
            public Transform Root;
        }

        /// <summary>Mesure la coque <paramref name="shell"/> (dans le repère de <paramref name="car"/>) et construit l'habitacle.</summary>
        public static Result Build(Transform car, Transform shell, string key, Bounds shape, float radius)
        {
            CarCabinPlan.Cabin cabin;
            if (!Measure(car, shell, shape, radius, out cabin))
            {
                Debug.LogWarning("[UberBagarre] Habitacle de " + key + " : silhouette illisible, proportions par defaut.");
                cabin = Default(shape, radius);
            }

            cabin.FourDoors = key == "Sedan" || key == "SUV";
            cabin.RearSeats = key != "Pickup" && key != "Coupe";
            cabin.Style = key == "Coupe" ? "sport" : key == "Shitbox" ? "vieille" : key == "Pickup" ? "utilitaire" : "berline";
            cabin = CarCabinPlan.Derive(cabin);

            InteriorPlan plan = CarCabinPlan.Make(cabin);
            InteriorRealizer.Built built = InteriorRealizer.Realize(plan, car, car.position, "Habitacle", "Habitacle_" + key, false);
            Transform root = built.Root.transform;
            root.localPosition = Vector3.zero;
            root.localRotation = Quaternion.identity;

            // L'habitacle ne doit pas peser dans la physique de la voiture : aucun collider.
            Collider[] colliders = root.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Object.DestroyImmediate(colliders[i]);

            // Pas d'ombre portée par l'intérieur (elle est dans celle de la carrosserie).
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(true);
            for (int i = 0; i < renderers.Length; i++) renderers[i].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;

            Transform wheel = Pivot(built, "pivot_volant", "volant");
            Transform speed = Pivot(built, "pivot_vitesse", "aiguille_vitesse");
            Transform rpm = Pivot(built, "pivot_regime", "aiguille_regime");
            Transform eye;
            built.Markers.TryGetValue("yeux", out eye);

            // La coque vue de l'intérieur : une copie sans rien dans le volume de l'habitacle (des
            // modèles sont des blocs pleins, couvercle à hauteur de vitre), et les vitres opaques
            // à cacher.
            List<MeshFilter> filters = new List<MeshFilter>();
            List<Mesh> inside = new List<Mesh>();
            List<Renderer> windows = new List<Renderer>();
            List<Mesh> saved = new List<Mesh>();
            MeshFilter[] all = shell.GetComponentsInChildren<MeshFilter>(true);
            for (int i = 0; i < all.Length; i++)
            {
                MeshFilter f = all[i];
                if (f.sharedMesh == null) continue;
                string n = f.name.ToLowerInvariant();
                if (n.Contains("window") || n.Contains("glass") || n.Contains("vitre"))
                {
                    Renderer r = f.GetComponent<Renderer>();
                    if (r != null) windows.Add(r);
                    continue;
                }

                Mesh trimmed = Trim(f, car, cabin);
                if (trimmed == null) continue;
                trimmed.name = "Habitacle_" + key + "_coque_" + saved.Count;
                saved.Add(trimmed);
                filters.Add(f);
                inside.Add(trimmed);
            }

            SaveMeshes(saved, "Habitacle_" + key + "_coque");

            CarCockpit cockpit = car.GetComponent<CarCockpit>();
            if (cockpit == null) cockpit = car.gameObject.AddComponent<CarCockpit>();
            cockpit.Configure(eye, wheel, speed, rpm, shell.GetComponentsInChildren<Renderer>(true), filters.ToArray(), inside.ToArray(),
                windows.ToArray());

            Debug.Log(string.Format("[UberBagarre] Habitacle {0} : toit {1:0.00} m ({2:0.00} a {3:0.00}), pied de pare-brise z {4:0.00} y {5:0.00}, yeux {6}.",
                key, cabin.RoofY, cabin.RoofBack, cabin.RoofFront, cabin.CowlZ, cabin.CowlY, cabin.Eye));
            return new Result { Cabin = cabin, Root = root };
        }

        /// <summary>
        /// Une copie du maillage sans les triangles dont le centre est dans le volume de
        /// l'habitacle (null si rien n'y est : on garde l'original).
        /// </summary>
        private static Mesh Trim(MeshFilter filter, Transform car, CarCabinPlan.Cabin c)
        {
            Mesh source = filter.sharedMesh;
            Vector3[] vertices = source.vertices;
            Vector3[] local = new Vector3[vertices.Length];
            for (int v = 0; v < vertices.Length; v++) local[v] = car.InverseTransformPoint(filter.transform.TransformPoint(vertices[v]));

            bool removed = false;
            List<int[]> kept = new List<int[]>(source.subMeshCount);
            for (int sub = 0; sub < source.subMeshCount; sub++)
            {
                int[] tris = source.GetTriangles(sub);
                List<int> keep = new List<int>(tris.Length);
                for (int t = 0; t + 2 < tris.Length; t += 3)
                {
                    Vector3 m = (local[tris[t]] + local[tris[t + 1]] + local[tris[t + 2]]) / 3f;
                    bool inCabin = Mathf.Abs(m.x) < c.HalfWidth - 0.02f && m.y > c.Floor + 0.02f && m.y < c.RoofY - 0.03f &&
                                   m.z > c.Rear && m.z < c.CowlZ - 0.03f;
                    if (inCabin)
                    {
                        removed = true;
                        continue;
                    }

                    keep.Add(tris[t]);
                    keep.Add(tris[t + 1]);
                    keep.Add(tris[t + 2]);
                }

                kept.Add(keep.ToArray());
            }

            if (!removed) return null;
            Mesh copy = Object.Instantiate(source);
            for (int sub = 0; sub < kept.Count; sub++) copy.SetTriangles(kept[sub], sub);
            return copy;
        }

        private static void SaveMeshes(List<Mesh> meshes, string assetName)
        {
            if (meshes.Count == 0) return;
            string path = InteriorRealizer.Folder + "/" + assetName + ".asset";
            UnityEditor.AssetDatabase.DeleteAsset(path);
            UnityEditor.AssetDatabase.CreateAsset(meshes[0], path);
            for (int i = 1; i < meshes.Count; i++) UnityEditor.AssetDatabase.AddObjectToAsset(meshes[i], path);
            UnityEditor.AssetDatabase.SaveAssets();
        }

        /// <summary>Range le groupe mobile sous son pivot (il tourne alors autour de son axe).</summary>
        private static Transform Pivot(InteriorRealizer.Built built, string marker, string group)
        {
            Transform pivot;
            GameObject moving;
            if (!built.Markers.TryGetValue(marker, out pivot)) return null;
            if (built.Groups.TryGetValue(group, out moving)) moving.transform.SetParent(pivot, true);
            return pivot;
        }

        // ------------------------------------------------------------------ mesure

        private static bool Measure(Transform car, Transform shell, Bounds shape, float radius, out CarCabinPlan.Cabin cabin)
        {
            cabin = default(CarCabinPlan.Cabin);
            List<MeshCollider> probes = new List<MeshCollider>();
            MeshFilter[] filters = shell.GetComponentsInChildren<MeshFilter>(false);
            for (int i = 0; i < filters.Length; i++)
            {
                if (filters[i].sharedMesh == null) continue;
                MeshCollider c = filters[i].gameObject.AddComponent<MeshCollider>();
                c.sharedMesh = filters[i].sharedMesh;
                probes.Add(c);
            }

            try
            {
                Physics.SyncTransforms();
                HashSet<Collider> mine = new HashSet<Collider>(probes.ToArray());

                const float step = 0.05f;
                int count = Mathf.CeilToInt(shape.size.z / step);
                float[] center = new float[count];
                float[] zs = new float[count];
                float sideX = shape.size.x * 0.22f;
                for (int i = 0; i < count; i++)
                {
                    float z = shape.min.z + (i + 0.5f) * step;
                    zs[i] = z;
                    center[i] = Mathf.Max(Top(car, mine, new Vector3(0f, 0f, z), shape), Mathf.Max(Top(car, mine, new Vector3(-sideX, 0f, z), shape),
                        Top(car, mine, new Vector3(sideX, 0f, z), shape)));
                }

                float roof = float.MinValue;
                for (int i = 0; i < count; i++) roof = Mathf.Max(roof, center[i]);
                if (roof <= radius) return false;

                float roofFront = float.MinValue, roofBack = float.MaxValue;
                for (int i = 0; i < count; i++)
                {
                    if (center[i] < roof - 0.05f) continue;
                    roofFront = Mathf.Max(roofFront, zs[i]);
                    roofBack = Mathf.Min(roofBack, zs[i]);
                }

                List<float> hood = new List<float>();
                for (int i = 0; i < count; i++)
                {
                    if (zs[i] > roofFront + 0.3f && zs[i] < shape.max.z - 0.3f && center[i] > 0f) hood.Add(center[i]);
                }

                if (hood.Count == 0) return false;
                hood.Sort();
                float hoodY = hood[hood.Count / 2];

                float cowlZ = float.NaN, cowlY = float.NaN;
                for (int i = 0; i < count; i++)
                {
                    if (zs[i] <= roofFront || center[i] <= 0f) continue;
                    if (center[i] < hoodY + 0.12f)
                    {
                        cowlZ = zs[i];
                        cowlY = center[i];
                        break;
                    }
                }

                if (float.IsNaN(cowlZ) || cowlZ - roofFront < 0.15f || cowlZ - roofFront > 1.6f) return false;

                // La largeur à hauteur d'épaule, prise de l'extérieur vers l'intérieur (les
                // rétroviseurs gonflent la boîte englobante de 20 cm de chaque côté).
                float side = Mathf.Min(Side(car, mine, shape, cowlZ - 0.9f, cowlY - 0.15f, 1f), Side(car, mine, shape, cowlZ - 0.9f, cowlY - 0.15f, -1f));
                cabin.HalfWidth = side > 0.4f ? side - 0.09f : shape.size.x * 0.5f - 0.2f;
                cabin.RoofY = roof;
                cabin.RoofFront = roofFront;
                cabin.RoofBack = roofBack;
                cabin.CowlZ = cowlZ;
                cabin.CowlY = cowlY;
                cabin.Floor = Mathf.Clamp(cowlY - 0.74f, Mathf.Max(0.12f, radius * 0.45f), 0.6f);
                cabin.Rear = Mathf.Max(shape.min.z + 0.4f, roofBack - 0.35f);
                return true;
            }
            finally
            {
                for (int i = 0; i < probes.Count; i++)
                {
                    if (probes[i] != null) Object.DestroyImmediate(probes[i]);
                }
            }
        }

        /// <summary>Le dessus de la coque en (x, z) de la voiture : un rayon vertical, seulement contre la coque.</summary>
        private static float Top(Transform car, HashSet<Collider> mine, Vector3 local, Bounds shape)
        {
            Vector3 from = car.TransformPoint(new Vector3(local.x, shape.max.y + 1f, local.z));
            RaycastHit[] hits = Physics.RaycastAll(from, -car.up, shape.size.y + 2f, ~0, QueryTriggerInteraction.Collide);
            float best = float.MinValue;
            for (int i = 0; i < hits.Length; i++)
            {
                if (!mine.Contains(hits[i].collider)) continue;
                best = Mathf.Max(best, car.InverseTransformPoint(hits[i].point).y);
            }

            return best;
        }

        /// <summary>Le flanc de la coque à (z, y) : un rayon horizontal tiré de l'extérieur, du côté <paramref name="sign"/>.</summary>
        private static float Side(Transform car, HashSet<Collider> mine, Bounds shape, float z, float y, float sign)
        {
            Vector3 from = car.TransformPoint(new Vector3(sign * (shape.extents.x + 1f), y, z));
            RaycastHit[] hits = Physics.RaycastAll(from, -car.right * sign, shape.size.x + 2f, ~0, QueryTriggerInteraction.Collide);
            float best = -1f;
            for (int i = 0; i < hits.Length; i++)
            {
                if (!mine.Contains(hits[i].collider)) continue;
                float x = Mathf.Abs(car.InverseTransformPoint(hits[i].point).x);
                if (x > best) best = x;
            }

            return best;
        }

        /// <summary>Si la silhouette ne se lit pas : des proportions de berline, à la taille de la coque.</summary>
        private static CarCabinPlan.Cabin Default(Bounds shape, float radius)
        {
            float top = shape.max.y;
            return new CarCabinPlan.Cabin
            {
                HalfWidth = shape.size.x * 0.5f - 0.2f,
                RoofY = top,
                RoofFront = shape.center.z + shape.size.z * 0.05f,
                RoofBack = shape.center.z - shape.size.z * 0.2f,
                CowlZ = shape.center.z + shape.size.z * 0.2f,
                CowlY = top * 0.72f,
                Floor = Mathf.Max(0.15f, radius * 0.5f),
                Rear = shape.center.z - shape.size.z * 0.3f
            };
        }
    }
}
