using System.Collections.Generic;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les voitures qu'on peut conduire : le modèle des voitures garées, mais avec ses quatre
    /// roues à part (elles tournent, braquent, suivent le sol), un corps rigide, des phares, un
    /// feu stop, des sons et une portière (E : conduire).
    ///
    /// Un modèle par peinture est fabriqué une fois (carrosserie fusionnée en quelques maillages),
    /// puis recopié partout : les maillages sont partagés.
    /// </summary>
    public static partial class CityBuilder
    {
        internal sealed class CarFactory
        {
            private readonly NightMaterialFactory.Palette _night;
            private readonly Transform _holder;
            private readonly Dictionary<int, GameObject> _templates = new Dictionary<int, GameObject>();
            private readonly Material[] _paints;
            private static readonly string[] PaintNames = { "Rouge", "Bleu", "Noir", "Blanc", "Vert", "Jaune", "Gris" };

            public int PaintCount { get { return _paints.Length; } }

            /// <summary>Peinture spéciale : la vieille caisse rouillée du joueur.</summary>
            public const int Rusty = -1;

            public CarFactory(NightMaterialFactory.Palette night, Transform holder)
            {
                _night = night;
                _holder = holder;

                Color[] colors =
                {
                    new Color(0.42f, 0.04f, 0.04f), new Color(0.05f, 0.11f, 0.30f), new Color(0.025f, 0.025f, 0.03f),
                    new Color(0.72f, 0.72f, 0.70f), new Color(0.07f, 0.19f, 0.12f), new Color(0.78f, 0.58f, 0.05f),
                    new Color(0.30f, 0.31f, 0.33f)
                };

                _paints = new Material[colors.Length];
                for (int i = 0; i < colors.Length; i++)
                {
                    _paints[i] = EditorBuildUtility.CreateOrUpdateMaterial(MaterialsFolder, "M_Voiture_" + PaintNames[i],
                        colors[i], 0.78f, 0.35f);
                }
            }

            /// <summary>Une voiture conduisible, posée roues au sol.</summary>
            public GameObject Spawn(Transform parent, int paint, Vector3 position, float yaw, string displayName)
            {
                if (paint != Rusty) paint = Mathf.Abs(paint) % _paints.Length;

                GameObject template;
                if (!_templates.TryGetValue(paint, out template))
                {
                    template = Build(paint);
                    _templates[paint] = template;
                }

                GameObject car = Object.Instantiate(template, parent);
                car.name = displayName;
                car.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                SerializedWiring.SetString(car.GetComponent<DrivableCar>(), "_displayName", displayName);

                Interactable door = car.GetComponent<Interactable>();
                if (door != null) SerializedWiring.SetString(door, "_hint", displayName);

                car.SetActive(true);
                return car;
            }

            public void Dispose()
            {
                foreach (GameObject template in _templates.Values)
                {
                    if (template != null) Object.DestroyImmediate(template);
                }

                _templates.Clear();
            }

            private GameObject Build(int paint)
            {
                bool rusty = paint == Rusty;
                string fileName = "VoitureConduite_" + (rusty ? "Rouillee" : PaintNames[paint]);
                GameObject holder = EditorBuildUtility.CreateEmpty(fileName, _holder, Vector3.zero);
                GameObject bodyRoot = EditorBuildUtility.CreateEmpty("Carrosserie", holder.transform, Vector3.zero);

                NightStreetBuilder.Car(bodyRoot.transform, _night, Vector3.zero, 0f, rusty, false);

                // Construite le long de +X : l'avant passe à +Z.
                Transform built = bodyRoot.transform.GetChild(0);
                built.localRotation = Quaternion.Euler(0f, -90f, 0f);

                Renderer[] renderers = built.GetComponentsInChildren<Renderer>(true);
                for (int i = 0; i < renderers.Length; i++)
                {
                    if (!rusty && renderers[i].sharedMaterial == _night.CarBody) renderers[i].sharedMaterial = _paints[paint];
                }

                Collider[] stray = built.GetComponentsInChildren<Collider>(true);
                for (int i = 0; i < stray.Length; i++) Object.DestroyImmediate(stray[i]);

                // --- les roues sortent de la carrosserie : un pivot (braquage, débattement) et
                // un enfant qui tourne. Moyeux du modèle : (±1,42 ; 0,34 ; ±0,88) le long de X.
                Transform wheelsRoot = EditorBuildUtility.CreateEmpty("Roues", holder.transform, Vector3.zero).transform;
                Transform[] pivots = new Transform[4];
                Transform[] spins = new Transform[4];
                bool[] front = new bool[4];
                string[] names = { "Roue avant gauche", "Roue avant droite", "Roue arriere gauche", "Roue arriere droite" };

                for (int i = 0; i < 4; i++)
                {
                    front[i] = i < 2;
                    float x = i % 2 == 0 ? -0.88f : 0.88f;
                    float z = front[i] ? 1.42f : -1.42f;
                    pivots[i] = EditorBuildUtility.CreateEmpty(names[i], wheelsRoot, new Vector3(x, 0.34f, z)).transform;
                    spins[i] = EditorBuildUtility.CreateEmpty("Rotation", pivots[i], Vector3.zero).transform;
                }

                List<Transform> parts = new List<Transform>();
                foreach (Transform child in built)
                {
                    if (child.name == "Pneu" || child.name == "Jante") parts.Add(child);
                }

                for (int i = 0; i < parts.Count; i++)
                {
                    Transform part = parts[i];
                    int best = 0;
                    float bestDistance = float.MaxValue;
                    for (int k = 0; k < 4; k++)
                    {
                        float dd = (pivots[k].position - part.position).sqrMagnitude;
                        if (dd < bestDistance)
                        {
                            bestDistance = dd;
                            best = k;
                        }
                    }

                    part.SetParent(spins[best], true);
                }

                CityMeshBuilder.Collapse(bodyRoot, fileName);

                // --- collisions : le bas de caisse (au-dessus des roues) et l'habitacle
                BoxCollider lower = holder.AddComponent<BoxCollider>();
                lower.center = new Vector3(0f, 0.8f, 0f);
                lower.size = new Vector3(1.86f, 0.62f, 4.5f);

                BoxCollider cabin = holder.AddComponent<BoxCollider>();
                cabin.center = new Vector3(0f, 1.34f, -0.15f);
                cabin.size = new Vector3(1.66f, 0.6f, 2.35f);

                Rigidbody rb = holder.AddComponent<Rigidbody>();
                rb.mass = 1250f;
                rb.interpolation = RigidbodyInterpolation.Interpolate;

                // --- places, feux, sons
                Transform seat = EditorBuildUtility.CreateEmpty("Siege conducteur", holder.transform, new Vector3(-0.4f, 0.45f, 0.1f)).transform;
                Transform door = EditorBuildUtility.CreateEmpty("Portiere", holder.transform, new Vector3(-0.95f, 1f, 0.25f)).transform;

                Light[] headlights = new Light[2];
                for (int s = 0; s < 2; s++)
                {
                    GameObject go = EditorBuildUtility.CreateEmpty(s == 0 ? "Phare gauche" : "Phare droit", holder.transform,
                        new Vector3(s == 0 ? -0.62f : 0.62f, 0.96f, 2.3f));
                    go.transform.localRotation = Quaternion.Euler(5f, 0f, 0f);
                    Light spot = go.AddComponent<Light>();
                    spot.type = LightType.Spot;
                    spot.spotAngle = 58f;
                    spot.range = 32f;
                    spot.intensity = 2.8f;
                    spot.color = new Color(1f, 0.95f, 0.86f);
                    spot.shadows = LightShadows.None;
                    spot.renderMode = LightRenderMode.ForcePixel;
                    spot.enabled = false;
                    headlights[s] = spot;
                }

                Light brake = NightStreetBuilder.AddLight(holder.transform, "Feu stop", new Vector3(0f, 0.95f, -2.55f),
                    new Color(1f, 0.08f, 0.06f), 0f, 4.5f, false, false);
                brake.enabled = false;

                AudioSource engine = Source(holder.transform, "Son moteur", 1.2f, 45f);
                AudioSource tires = Source(holder.transform, "Son pneus", 1f, 35f);
                AudioSource horn = Source(holder.transform, "Klaxon", 1f, 60f);
                AudioSource impacts = Source(holder.transform, "Chocs", 1f, 40f);

                Interactable interactable = holder.AddComponent<Interactable>();
                SerializedWiring.SetString(interactable, "_label", "Conduire");
                SerializedWiring.SetString(interactable, "_hint", "Voiture");
                SerializedWiring.SetFloat(interactable, "_range", 3.2f);
                SerializedWiring.SetObject(interactable, "_focus", door);

                DrivableCar car = holder.AddComponent<DrivableCar>();
                SerializedObject so = new SerializedObject(car);
                SerializedProperty wheels = so.FindProperty("_wheels");
                wheels.arraySize = 4;
                for (int i = 0; i < 4; i++)
                {
                    SerializedProperty w = wheels.GetArrayElementAtIndex(i);
                    w.FindPropertyRelative("pivot").objectReferenceValue = pivots[i];
                    w.FindPropertyRelative("spin").objectReferenceValue = spins[i];
                    w.FindPropertyRelative("front").boolValue = front[i];
                }

                SerializedProperty lights = so.FindProperty("_headlights");
                lights.arraySize = 2;
                lights.GetArrayElementAtIndex(0).objectReferenceValue = headlights[0];
                lights.GetArrayElementAtIndex(1).objectReferenceValue = headlights[1];

                so.FindProperty("_seat").objectReferenceValue = seat;
                so.FindProperty("_brakeLight").objectReferenceValue = brake;
                so.FindProperty("_engine").objectReferenceValue = engine;
                so.FindProperty("_tires").objectReferenceValue = tires;
                so.FindProperty("_horn").objectReferenceValue = horn;
                so.FindProperty("_impacts").objectReferenceValue = impacts;
                so.ApplyModifiedPropertiesWithoutUndo();

                holder.SetActive(false);
                return holder;
            }

            private static AudioSource Source(Transform parent, string name, float minDistance, float maxDistance)
            {
                GameObject go = EditorBuildUtility.CreateEmpty(name, parent, new Vector3(0f, 0.8f, 1f));
                AudioSource source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 0.85f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = minDistance;
                source.maxDistance = maxDistance;
                source.dopplerLevel = 0.3f;
                return source;
            }
        }

        /// <summary>
        /// Les voitures des allées et une partie de celles garées le long des rues. Une sur deux
        /// dans les allées, une sur quatre dans la rue : de quoi trouver une voiture à deux pas,
        /// sans transformer chaque trottoir en parking de location.
        /// </summary>
        private static void PlaceDriveways(Transform parent, NightMaterialFactory.Palette night, CarFactory factory,
            System.Random rng, List<CarSpot> spots)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Voitures des allees", parent, Vector3.zero);
            GameObject clean = CarTemplate(root.transform, night, false, "VoitureGaree");
            GameObject rusty = CarTemplate(root.transform, night, true, "VoitureGareeRouillee");

            for (int i = 0; i < spots.Count; i++)
            {
                CarSpot spot = spots[i];
                if (spot.Drivable || i % 2 == 0)
                {
                    factory.Spawn(root.transform, rng.Next(factory.PaintCount), spot.Position, spot.Yaw, "Voiture");
                    continue;
                }

                GameObject car = Object.Instantiate(rng.NextDouble() < 0.3 ? rusty : clean, root.transform);
                car.name = "Voiture garee";
                car.transform.SetPositionAndRotation(spot.Position, Quaternion.Euler(0f, spot.Yaw, 0f));
                car.SetActive(true);
                SetStatic(car.transform);
            }

            Object.DestroyImmediate(clean);
            Object.DestroyImmediate(rusty);
        }
    }
}
