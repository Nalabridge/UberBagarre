using System.Collections.Generic;
using UberBagarre.View;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Ce qui fait vivre la ville entre les immeubles : les feux des carrefours (que la
    /// circulation respecte), les enseignes des commerces écrites en néon, les abribus éclairés,
    /// les bancs, les poubelles, les bornes à incendie, la vapeur qui sort des plaques d'égout.
    /// </summary>
    public static partial class CityBuilder
    {
        /// <summary>Une enseigne au néon à écrire une fois les îlots fusionnés.</summary>
        internal struct SignRequest
        {
            public string Text;
            public Vector3 Position;
            public Quaternion Rotation;
            public float Height;
            public Material Material;
            public bool Broken;
        }

        private static readonly string[] ShopNames =
        {
            "PIZZA", "KEBAB", "BAR", "HOTEL", "LAVERIE", "TABAC", "SNACK", "CAFE", "BOXE", "GARAGE",
            "NIGHT SHOP", "COIFFEUR", "CHICHA", "TACOS", "PMU", "BURGER", "CLUB", "SALLE DE SPORT", "OPTIQUE",
            "BOULANGERIE", "PHARMACIE", "LIQUEURS", "DISCO", "MOTEL"
        };

        // ------------------------------------------------------------------ feux tricolores

        /// <summary>Les carrefours que traverse la circulation : avenues × rue du Nord, rue du Sud et boulevard.</summary>
        private static void BuildTrafficLights(Transform parent, Mats m, Result result)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Feux tricolores", parent, Vector3.zero);
            CityMeshBuilder poles = new CityMeshBuilder("Poteaux des feux");

            Material off = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Feu_Eteint",
                new Color(0.04f, 0.04f, 0.045f), 0.85f, 0f);
            Material red = m.Neons[2];
            Material green = m.Neons[3];
            Material amber = m.Neons[5];

            float[] avenues = { -150f, -62f, 62f, 150f };
            float[] streets = { 102f, -122f, -2f };
            int index = 0;

            for (int a = 0; a < avenues.Length; a++)
            {
                for (int s = 0; s < streets.Length; s++, index++)
                {
                    float cx = avenues[a];
                    float cz = streets[s];
                    float hx = 6f;
                    float hz = s == 2 ? 9f : 6f;

                    GameObject go = EditorBuildUtility.CreateEmpty("Carrefour " + (index + 1), root.transform, new Vector3(cx, 0f, cz));
                    TrafficLight light = go.AddComponent<TrafficLight>();

                    List<Renderer> ns = new List<Renderer>();
                    List<Renderer> ew = new List<Renderer>();

                    // Un feu par voie qui arrive, à droite avant le carrefour, tourné vers elle.
                    Head(poles, go.transform, off, new Vector3(cx + hx + 1f, SidewalkHeight, cz - hz - 0.9f), Vector3.back, ns);
                    Head(poles, go.transform, off, new Vector3(cx - hx - 1f, SidewalkHeight, cz + hz + 0.9f), Vector3.forward, ns);
                    Head(poles, go.transform, off, new Vector3(cx - hx - 0.9f, SidewalkHeight, cz - hz - 1f), Vector3.left, ew);
                    Head(poles, go.transform, off, new Vector3(cx + hx + 0.9f, SidewalkHeight, cz + hz + 1f), Vector3.right, ew);

                    SerializedWiring.SetFloat(light, "_offset", index * 3.7f);
                    SerializedObject so = new SerializedObject(light);
                    so.FindProperty("_halfSize").vector2Value = new Vector2(hx, hz);
                    so.ApplyModifiedPropertiesWithoutUndo();
                    SandboxSceneBuilder.SetComponentArray(light, "_northSouth", ns.ToArray());
                    SandboxSceneBuilder.SetComponentArray(light, "_eastWest", ew.ToArray());
                    SerializedWiring.SetObject(light, "_off", off);
                    SerializedWiring.SetObject(light, "_red", red);
                    SerializedWiring.SetObject(light, "_amberLit", amber);
                    SerializedWiring.SetObject(light, "_greenLit", green);
                    EditorUtility.SetDirty(light);
                }
            }

            poles.Flush(root.transform);
        }

        /// <summary>Un poteau, un boîtier, trois lampes (rouge en haut) tournées vers <paramref name="facing"/>.</summary>
        private static void Head(CityMeshBuilder poles, Transform parent, Material off, Vector3 foot, Vector3 facing,
            List<Renderer> lamps)
        {
            float yaw = Quaternion.LookRotation(facing).eulerAngles.y;
            Material pole = off;

            poles.Box(foot + Vector3.up * 1.75f, new Vector3(0.13f, 3.5f, 0.13f), pole, None, true);
            poles.Box(foot + Vector3.up * 3.05f + facing * 0.05f, new Vector3(0.38f, 1.08f, 0.26f), pole, None, false, yaw);

            float[] heights = { 3.4f, 3.05f, 2.7f };
            for (int i = 0; i < 3; i++)
            {
                GameObject lamp = EditorBuildUtility.CreatePrimitive(PrimitiveType.Cube, "Lampe", parent,
                    Vector3.zero, new Vector3(0.24f, 0.24f, 0.05f), off, false);
                lamp.transform.position = foot + Vector3.up * heights[i] + facing * 0.19f;
                lamp.transform.rotation = Quaternion.Euler(0f, yaw, 0f);

                // La visière au-dessus de chaque lampe.
                poles.Box(foot + Vector3.up * (heights[i] + 0.15f) + facing * 0.26f, new Vector3(0.3f, 0.03f, 0.16f), pole, None, false, yaw);

                Renderer renderer = lamp.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                lamps.Add(renderer);
            }
        }

        // ------------------------------------------------------------------ mobilier urbain

        /// <summary>
        /// Le long des trottoirs : bancs, poubelles, bornes à incendie, et quelques abribus éclairés.
        /// Rien devant les carrefours ni sur les trottoirs de la rue du Vertigo (déjà meublés).
        /// </summary>
        private static void BuildStreetFurniture(Transform parent, Mats m, System.Random rng, Result result)
        {
            CityMeshBuilder b = new CityMeshBuilder("Mobilier urbain");
            Material hydrant = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Ville_BorneIncendie",
                new Color(0.55f, 0.06f, 0.05f), 0.45f, 0f);
            Material poster = NightMaterialFactory.CreateEmissive(MaterialsFolder, "M_Ville_Affiche",
                new Color(0.9f, 0.86f, 0.8f), null, Vector2.one, new Color(0.95f, 0.9f, 1f) * 1.3f, null, 0.5f, 0f);
            int shelters = 0;

            for (int xi = 0; xi < 5; xi++)
            {
                for (int zi = 0; zi < 4; zi++)
                {
                    Rect lot = Lot(xi, zi);
                    Vector4 w = SidewalkWidths(xi, zi);

                    // Chaque côté : une ligne à 0,9 m de la bordure, côté chaussée.
                    if (w.w > 0f) FurnitureRow(b, m, rng, result, hydrant, poster, ref shelters, w.w >= 7f, new Vector2(lot.xMin, lot.yMax + w.w - 0.9f), new Vector2(lot.xMax, lot.yMax + w.w - 0.9f), Vector3.forward);
                    if (w.z > 0f) FurnitureRow(b, m, rng, result, hydrant, poster, ref shelters, w.z >= 7f, new Vector2(lot.xMin, lot.yMin - w.z + 0.9f), new Vector2(lot.xMax, lot.yMin - w.z + 0.9f), Vector3.back);
                    if (w.x > 0f) FurnitureRow(b, m, rng, result, hydrant, poster, ref shelters, w.x >= 7f, new Vector2(lot.xMin - w.x + 0.9f, lot.yMin), new Vector2(lot.xMin - w.x + 0.9f, lot.yMax), Vector3.left);
                    if (w.y > 0f) FurnitureRow(b, m, rng, result, hydrant, poster, ref shelters, w.y >= 7f, new Vector2(lot.xMax + w.y - 0.9f, lot.yMin), new Vector2(lot.xMax + w.y - 0.9f, lot.yMax), Vector3.right);
                }
            }

            b.Flush(parent);
        }

        private static void FurnitureRow(CityMeshBuilder b, Mats m, System.Random rng, Result result, Material hydrant,
            Material poster, ref int shelters, bool wide, Vector2 from, Vector2 to, Vector3 toRoad)
        {
            float length = Vector2.Distance(from, to);
            Vector2 dir = (to - from).normalized;
            float yaw = Quaternion.LookRotation(toRoad).eulerAngles.y;

            // Décalé d'une demi-maille par rapport aux lampadaires (tous les 26 m).
            for (float t = 9f; t < length - 9f; t += Range(rng, 11f, 19f))
            {
                Vector2 p2 = from + dir * t;

                bool vertigo = false;
                for (int s = 0; s < StreetSidewalks.Length; s++) vertigo |= StreetSidewalks[s].Contains(p2);
                if (vertigo || Park.Contains(p2)) continue;

                Vector3 p = new Vector3(p2.x, SidewalkHeight, p2.y);
                double pick = rng.NextDouble();

                // Les abribus seulement sur les trottoirs larges (le boulevard) : sur 4 m, ils
                // barreraient le passage.
                if (pick < 0.12 && wide && shelters < 10 && length > 60f)
                {
                    Shelter(b, m, result, poster, p - toRoad * 0.6f, yaw, toRoad);
                    shelters++;
                    t += 6f;
                }
                else if (pick < 0.34)
                {
                    // Banc tourné vers la rue.
                    b.Box(p + Vector3.up * 0.45f - toRoad * 0.2f, new Vector3(1.8f, 0.08f, 0.5f), m.Wood, None, true, yaw);
                    b.Box(p + Vector3.up * 0.8f - toRoad * 0.44f, new Vector3(1.8f, 0.45f, 0.07f), m.Wood, None, false, yaw);
                    for (int k = -1; k <= 1; k += 2)
                    {
                        Vector3 side = Quaternion.Euler(0f, yaw, 0f) * Vector3.right * (k * 0.75f);
                        b.Box(p + side + Vector3.up * 0.22f - toRoad * 0.25f, new Vector3(0.08f, 0.44f, 0.5f), m.DarkMetal, None, false, yaw);
                    }
                }
                else if (pick < 0.58)
                {
                    b.Box(p + Vector3.up * 0.45f, new Vector3(0.5f, 0.9f, 0.5f), m.DarkMetal, None, true, yaw);
                    b.Box(p + Vector3.up * 0.93f, new Vector3(0.56f, 0.06f, 0.56f), m.Metal, None, false, yaw);
                }
                else if (pick < 0.72)
                {
                    b.Box(p + Vector3.up * 0.35f, new Vector3(0.28f, 0.7f, 0.28f), hydrant, None, true, yaw);
                    b.Box(p + Vector3.up * 0.74f, new Vector3(0.2f, 0.1f, 0.2f), hydrant, None, false, yaw + 45f);
                    b.Box(p + Vector3.up * 0.48f, new Vector3(0.44f, 0.1f, 0.1f), hydrant, None, false, yaw);
                }
                else if (pick < 0.8)
                {
                    // Des potelets : on ne se gare pas sur le trottoir.
                    for (int k = -1; k <= 1; k++)
                    {
                        Vector3 along = new Vector3(dir.x, 0f, dir.y) * (k * 1.3f);
                        b.Box(p + along + Vector3.up * 0.45f + toRoad * 0.4f, new Vector3(0.12f, 0.9f, 0.12f), m.DarkMetal, None, true);
                    }
                }
            }
        }

        /// <summary>Un abribus : verre, toit, banc, et son affiche éclairée (qui éclaire vraiment).</summary>
        private static void Shelter(CityMeshBuilder b, Mats m, Result result, Material poster, Vector3 p, float yaw, Vector3 toRoad)
        {
            Quaternion r = Quaternion.Euler(0f, yaw, 0f);
            Vector3 right = r * Vector3.right;

            b.Box(p - toRoad * 0.7f + Vector3.up * 1.2f, new Vector3(3.6f, 2.2f, 0.05f), m.Glass, None, true, yaw);
            b.Box(p + right * 1.8f + Vector3.up * 1.2f, new Vector3(0.05f, 2.2f, 1.3f), m.Glass, None, true, yaw);
            b.Box(p - right * 1.8f + Vector3.up * 1.1f, new Vector3(0.12f, 2.2f, 1.36f), m.DarkMetal, None, true, yaw);
            b.Box(p - right * 1.74f + Vector3.up * 1.2f, new Vector3(0.04f, 1.7f, 1.1f), poster, None, false, yaw);
            b.Box(p + Vector3.up * 2.35f, new Vector3(3.9f, 0.1f, 1.6f), m.DarkMetal, None, false, yaw);
            b.Box(p - toRoad * 0.45f + Vector3.up * 0.45f, new Vector3(2.4f, 0.07f, 0.4f), m.Metal, None, true, yaw);
            b.Box(p + right * 2.3f + toRoad * 0.5f + Vector3.up * 1.4f, new Vector3(0.08f, 2.8f, 0.08f), m.DarkMetal, None, false, yaw);
            b.Box(p + right * 2.3f + toRoad * 0.5f + Vector3.up * 2.7f, new Vector3(0.5f, 0.5f, 0.05f), m.Neons[4], None, false, yaw);

            Light light = NightStreetBuilder.AddLight(result.LightsRoot, "Abribus", Vector3.zero,
                new Color(0.85f, 0.9f, 1f), 1.2f, 6f, false, false);
            light.transform.position = p - right * 1.2f + Vector3.up * 1.4f;
        }

        // ------------------------------------------------------------------ vapeur

        /// <summary>La vapeur des plaques d'égout, sur les chaussées : une ville respire la nuit.</summary>
        private static void BuildSteam(Transform parent, Mats m, System.Random rng)
        {
            Shader shader = Shader.Find("Legacy Shaders/Particles/Alpha Blended");
            Texture2D puff = AssetDatabase.GetBuiltinExtraResource<Texture2D>("Default-Particle.psd");
            if (shader == null) return;

            string path = MaterialsFolder + "/M_Ville_Vapeur.mat";
            Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
            if (material == null)
            {
                material = new Material(shader);
                AssetDatabase.CreateAsset(material, path);
            }

            material.shader = shader;
            material.mainTexture = puff;
            if (material.HasProperty("_TintColor")) material.SetColor("_TintColor", new Color(0.5f, 0.5f, 0.52f, 0.18f));
            EditorUtility.SetDirty(material);

            GameObject root = EditorBuildUtility.CreateEmpty("Vapeur des egouts", parent, Vector3.zero);
            CityMeshBuilder covers = new CityMeshBuilder("Plaques d'egout");
            List<Rect> roads = Roads();
            int placed = 0;

            for (int i = 0; i < 60 && placed < 9; i++)
            {
                Rect road = roads[rng.Next(roads.Count)];
                bool eastWest = road.width > road.height;
                Vector2 c = eastWest
                    ? new Vector2(Range(rng, road.xMin + 20f, road.xMax - 20f), road.center.y + Range(rng, -2f, 2f))
                    : new Vector2(road.center.x + Range(rng, -2f, 2f), Range(rng, road.yMin + 20f, road.yMax - 20f));
                if (StreetGround.Contains(c)) continue;

                Vector3 p = new Vector3(c.x, 0.02f, c.y);
                covers.Box(p, new Vector3(0.8f, 0.03f, 0.8f), m.DarkMetal, None, false, 45f);

                GameObject go = EditorBuildUtility.CreateEmpty("Vapeur", root.transform, p);
                go.transform.rotation = Quaternion.Euler(-90f, 0f, 0f);
                ParticleSystem system = go.AddComponent<ParticleSystem>();

                ParticleSystem.MainModule main = system.main;
                main.loop = true;
                main.startLifetime = new ParticleSystem.MinMaxCurve(2.5f, 4f);
                main.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.2f);
                main.startSize = new ParticleSystem.MinMaxCurve(0.8f, 1.8f);
                main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
                main.startColor = new Color(0.8f, 0.82f, 0.86f, 0.35f);
                main.simulationSpace = ParticleSystemSimulationSpace.World;
                main.maxParticles = 40;

                ParticleSystem.EmissionModule emission = system.emission;
                emission.rateOverTime = 7f;

                ParticleSystem.ShapeModule shape = system.shape;
                shape.shapeType = ParticleSystemShapeType.Cone;
                shape.angle = 12f;
                shape.radius = 0.3f;

                ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
                size.enabled = true;
                size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 0.6f, 1f, 2.4f));

                ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
                color.enabled = true;
                Gradient fade = new Gradient();
                fade.SetKeys(
                    new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                    new[] { new GradientAlphaKey(0f, 0f), new GradientAlphaKey(1f, 0.2f), new GradientAlphaKey(0f, 1f) });
                color.color = new ParticleSystem.MinMaxGradient(fade);

                ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;

                placed++;
            }

            covers.Flush(root.transform, false, false);
        }

        // ------------------------------------------------------------------ enseignes

        /// <summary>Les enseignes demandées par les vitrines, écrites en tubes puis fusionnées.</summary>
        private static void BuildSigns(Transform parent, Result result)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Enseignes", parent, Vector3.zero);

            for (int i = 0; i < result.Signs.Count; i++)
            {
                SignRequest sign = result.Signs[i];
                GameObject go = EditorBuildUtility.CreateEmpty("Enseigne " + sign.Text, root.transform, Vector3.zero);
                go.transform.SetPositionAndRotation(sign.Position, sign.Rotation);

                NeonTextBuilder.Build(go.transform, sign.Text, sign.Height, Mathf.Max(0.035f, sign.Height * 0.09f), sign.Material);
                CityMeshBuilder.Collapse(go, "Enseigne_" + sign.Text + "_" + Mathf.RoundToInt(sign.Height * 100f) + "_" +
                                             (sign.Material != null ? sign.Material.name : "neon"));

                Renderer[] renderers = go.GetComponentsInChildren<Renderer>();
                for (int r = 0; r < renderers.Length; r++)
                {
                    renderers[r].shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                }

                // Un néon sur six est fatigué : il saute, repart, grésille.
                if (sign.Broken) NightStreetBuilder.AddFlicker(go, NeonFlicker.Pattern.Fatigue, 6f, 0.7f, 1f, 11f + i * 7f);
            }
        }

        /// <summary>Demande une enseigne au-dessus d'une vitrine (lettres tenant dans <paramref name="width"/>).</summary>
        private static void RequestSign(Result result, System.Random rng, Vector3 center, Vector3 outward, float width,
            Material material, string text)
        {
            bool billboard = text != null;
            if (text == null) text = ShopNames[rng.Next(ShopNames.Length)];
            float height = Mathf.Min(billboard ? 1.5f : 0.62f,
                width * 0.9f / (text.Length * (NeonTextBuilder.AspectRatio + NeonTextBuilder.Tracking)));
            if (height < 0.22f) return;

            result.Signs.Add(new SignRequest
            {
                Text = text,
                Position = center - Vector3.up * (height * 0.5f),
                Rotation = Quaternion.LookRotation(-outward, Vector3.up),
                Height = height,
                Material = material,
                Broken = rng.NextDouble() < 0.16
            });
        }
    }
}
