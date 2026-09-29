using System.Collections.Generic;
using UberBagarre.View;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La météo du monde ouvert (voir <see cref="Weather"/>) : la pluie autour de la caméra,
    /// ses éclaboussures, l'éclair de l'orage, le matériau des flaques et leurs emplacements.
    /// </summary>
    public static partial class OpenWorldSceneBuilder
    {
        private static Weather BuildWeather(GameObject systems, TimeOfDay time, List<Vector3[]> driveLoops,
            List<Vector3[]> walkLoops, MapPack.Data map)
        {
            GameObject root = new GameObject("=== Meteo ===");
            Weather weather = root.AddComponent<Weather>();

            ParticleSystem splashes;
            ParticleSystem rain = BuildRainSystem(root.transform, out splashes);

            // L'éclair : une lampe directionnelle froide, éteinte ; l'orage l'allume par éclats.
            GameObject flash = EditorBuildUtility.CreateEmpty("Eclair", root.transform, Vector3.zero);
            flash.transform.rotation = Quaternion.Euler(62f, 30f, 0f);
            Light lightning = flash.AddComponent<Light>();
            lightning.type = LightType.Directional;
            lightning.color = new Color(0.78f, 0.84f, 1f);
            lightning.intensity = 0f;
            lightning.shadows = LightShadows.None;
            lightning.enabled = false;

            weather.Configure(time, rain, splashes, PuddleMaterial(), lightning, PuddleSpots(driveLoops, walkLoops, map));
            EditorUtility.SetDirty(weather);
            return weather;
        }

        /// <summary>
        /// La pluie : des traits fins qui tombent d'une nappe au-dessus de la caméra. Chaque
        /// goutte meurt en touchant le décor (donc pas de pluie sous un toit) et y laisse une
        /// petite éclaboussure. L'émission est pilotée par la météo (zéro par temps sec).
        /// </summary>
        private static ParticleSystem BuildRainSystem(Transform parent, out ParticleSystem splashes)
        {
            splashes = null;
            Material streak = NightMaterialFactory.CreateRain(NightMaterialFactory.MaterialsFolder, "M_Pluie",
                new Color(0.74f, 0.82f, 1f), 0.9f);
            if (streak == null) return null;

            GameObject go = EditorBuildUtility.CreateEmpty("Pluie", parent, new Vector3(0f, 12f, 0f));
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            ParticleSystem system = go.AddComponent<ParticleSystem>();
            ParticleSystem.MainModule main = system.main;
            main.duration = 5f;
            main.loop = true;
            main.startLifetime = 1.9f;
            main.startSpeed = new ParticleSystem.MinMaxCurve(10f, 14f);
            main.startSize = new ParticleSystem.MinMaxCurve(0.022f, 0.04f);
            main.startColor = new ParticleSystem.MinMaxGradient(new Color(0.72f, 0.82f, 1f, 0.2f), new Color(0.9f, 0.95f, 1f, 0.38f));
            main.gravityModifier = 0.7f;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.maxParticles = 3200;
            main.playOnAwake = true;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.rateOverTime = 0f;

            ParticleSystem.ShapeModule shape = system.shape;
            shape.shapeType = ParticleSystemShapeType.Box;
            shape.scale = new Vector3(44f, 44f, 0.5f);

            // Les gouttes heurtent le décor (toits, auvents, voitures) et meurent : on est au
            // sec sous un abri, et le sol reçoit des éclaboussures.
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.lifetimeLoss = 1f;
            collision.bounce = 0f;
            collision.radiusScale = 0.2f;
            collision.maxCollisionShapes = 96;
            collision.collidesWith = ~(1 << 2);

            ParticleSystemRenderer renderer = go.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Stretch;
            renderer.velocityScale = 0.05f;
            renderer.lengthScale = 2.2f;
            renderer.cameraVelocityScale = 0f;
            renderer.sharedMaterial = streak;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 10;

            FollowCameraFlat follow = go.AddComponent<FollowCameraFlat>();
            SerializedWiring.SetFloat(follow, "_height", 12f);
            SerializedWiring.SetFloat(follow, "_step", 5f);
            SerializedWiring.SetBool(follow, "_relativeHeight", true);

            // --- les éclaboussures : un point clair qui s'étale et s'éteint en un quart de seconde
            Material splash = NightMaterialFactory.CreateRain(NightMaterialFactory.MaterialsFolder, "M_Eclaboussure",
                new Color(0.8f, 0.87f, 1f), 0.8f);
            GameObject splashGo = EditorBuildUtility.CreateEmpty("Eclaboussures", go.transform, Vector3.zero);
            splashes = splashGo.AddComponent<ParticleSystem>();

            ParticleSystem.MainModule splashMain = splashes.main;
            splashMain.loop = false;
            splashMain.playOnAwake = false;
            splashMain.startLifetime = new ParticleSystem.MinMaxCurve(0.18f, 0.3f);
            splashMain.startSpeed = new ParticleSystem.MinMaxCurve(0.6f, 1.6f);
            splashMain.startSize = new ParticleSystem.MinMaxCurve(0.04f, 0.08f);
            splashMain.startColor = new Color(0.85f, 0.9f, 1f, 0.3f);
            splashMain.gravityModifier = 1.2f;
            splashMain.simulationSpace = ParticleSystemSimulationSpace.World;
            splashMain.maxParticles = 900;

            ParticleSystem.EmissionModule splashEmission = splashes.emission;
            splashEmission.rateOverTime = 0f;

            ParticleSystem.ShapeModule splashShape = splashes.shape;
            splashShape.shapeType = ParticleSystemShapeType.Hemisphere;
            splashShape.radius = 0.03f;

            ParticleSystem.SizeOverLifetimeModule grow = splashes.sizeOverLifetime;
            grow.enabled = true;
            grow.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));

            ParticleSystemRenderer splashRenderer = splashGo.GetComponent<ParticleSystemRenderer>();
            splashRenderer.renderMode = ParticleSystemRenderMode.Stretch;
            splashRenderer.velocityScale = 0.08f;
            splashRenderer.lengthScale = 1.2f;
            splashRenderer.sharedMaterial = splash != null ? splash : streak;
            splashRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            splashRenderer.receiveShadows = false;

            ParticleSystem.SubEmittersModule sub = system.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(splashes, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, 0.55f);

            return system;
        }

        /// <summary>
        /// Le matériau des flaques : noir, très lisse, découpé (le masque et le seuil sont
        /// posés par la météo). Un Standard « Cutout » : il passe par le rendu différé, donc
        /// chaque lampe de la rue y laisse son reflet.
        /// </summary>
        private static Material PuddleMaterial()
        {
            Material material = EditorBuildUtility.CreateOrUpdateMaterial(NightMaterialFactory.MaterialsFolder, "M_Flaque",
                new Color(0.55f, 0.58f, 0.64f), 0.97f, 0f);

            material.SetFloat("_Mode", 1f);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.One);
            material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.Zero);
            material.SetInt("_ZWrite", 1);
            material.EnableKeyword("_ALPHATEST_ON");
            material.DisableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.AlphaTest;
            material.SetFloat("_Cutoff", 0.96f);
            material.SetFloat("_AlphaClip", 1f);
            material.SetFloat("_Surface", 0f);
            material.SetFloat("_SpecularHighlights", 1f);
            material.SetFloat("_EnvironmentReflections", 1f);
            material.SetFloat("_GlossyReflections", 1f);
            material.enableInstancing = true;
            EditorUtility.SetDirty(material);
            return material;
        }

        /// <summary>
        /// Où se forment les flaques : le long des routes, vers le trottoir (là où l'eau
        /// s'accumule), quelques-unes sur les trottoirs, et une poignée devant le Vertigo — la
        /// première nuit s'y passe.
        /// </summary>
        private static Vector3[] PuddleSpots(List<Vector3[]> driveLoops, List<Vector3[]> walkLoops, MapPack.Data map)
        {
            List<Vector3> spots = new List<Vector3>();
            System.Random random = new System.Random(97);

            AddAlong(spots, driveLoops, random, 22f, 1.4f, 3.2f, 260);
            AddAlong(spots, walkLoops, random, 34f, 0f, 1.2f, 90);

            if (map.vertigo != null && map.vertigo.door != null)
            {
                Vector3 door = MapPack.Position(map.vertigo.door);
                Quaternion facing = Quaternion.Euler(0f, MapPack.Yaw(map.vertigo.door), 0f);
                for (int i = 0; i < 7; i++)
                {
                    Vector3 local = new Vector3(((float)random.NextDouble() - 0.5f) * 12f, 0f, -2.5f - (float)random.NextDouble() * 9f);
                    spots.Add(door + facing * local);
                }
            }

            return spots.ToArray();
        }

        private static void AddAlong(List<Vector3> spots, List<Vector3[]> loops, System.Random random, float every,
            float minOffset, float maxOffset, int cap)
        {
            int added = 0;
            for (int l = 0; l < loops.Count && added < cap; l++)
            {
                Vector3[] loop = loops[l];
                if (loop == null || loop.Length < 2) continue;

                float carried = (float)random.NextDouble() * every;
                for (int i = 0; i < loop.Length && added < cap; i++)
                {
                    Vector3 a = loop[i];
                    Vector3 b = loop[(i + 1) % loop.Length];
                    Vector3 segment = b - a;
                    segment.y = 0f;
                    float length = segment.magnitude;
                    if (length < 0.1f) continue;

                    Vector3 side = new Vector3(segment.z, 0f, -segment.x) / length;
                    for (float d = carried; d < length && added < cap; d += every * (0.6f + (float)random.NextDouble() * 0.8f))
                    {
                        float offset = Mathf.Lerp(minOffset, maxOffset, (float)random.NextDouble()) * (random.NextDouble() < 0.7 ? 1f : -1f);
                        spots.Add(Vector3.Lerp(a, b, d / length) + side * offset);
                        added++;
                        carried = d + every - length;
                    }
                }
            }
        }
    }
}
