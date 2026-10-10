using System.Collections.Generic;
using UberBagarre.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les voitures du jeu, faites avec les véhicules de la ville (la compacte, la berline, le
    /// SUV, le pick-up, le coupé de la concession) au lieu des voitures dessinées à la main.
    ///
    /// Un véhicule de la carte n'est qu'un décor : une carrosserie et quatre roues nommées
    /// (« Wheel_FL », « Wheel FR », « MuscleCarWheel_RL »…). On en fait une voiture :
    /// - un repère propre (avant = +Z, sol = 0, centre entre les essieux), mesuré sur les roues ;
    /// - chaque roue sous un pivot (braquage, suspension) et un enfant qui tourne ; la compacte
    ///   et le SUV n'ont pas de jantes dans la carte : ils empruntent celles du pick-up, à leur
    ///   taille ;
    /// - deux boîtes de collision (bas de caisse, habitacle) mesurées sur la carrosserie : les
    ///   colliders d'origine (maillages concaves) ne tiennent pas sur un corps rigide ;
    /// - ses propres phares et feux (les lampes du modèle), les sons de <see cref="DrivableCar"/> ;
    /// - pour la circulation, un conducteur assis au volant (un corps du jeu, posé une fois et
    ///   figé en maillage : il ne coûte pas plus qu'un siège).
    ///
    /// Les modèles restent dans la scène (inactifs) : le jeu en tire la voiture qu'on achète.
    /// </summary>
    internal sealed class CityVehicles
    {
        public sealed class Model
        {
            public string key;
            public string label;
            public int price;
            public float mass;
            public float maxSpeed;
            public float power;
            public GameObject template;
            public float halfWidth;
            public float height;
        }

        /// <summary>Un véhicule garé dans la ville d'origine (remplacé par une vraie voiture).</summary>
        public struct Parked
        {
            public string model;
            public Vector3 position;
            public float yaw;
        }

        private static readonly string[] Corners = { "FL", "FR", "RL", "RR" };
        private static readonly string[] WheelNames = { "Roue avant gauche", "Roue avant droite", "Roue arriere gauche", "Roue arriere droite" };

        // clé, nom affiché, prix, masse, vitesse de pointe (m/s), puissance (N/kg)
        private static readonly object[][] Catalog =
        {
            new object[] { "Shitbox", "Compacte", 3500, 1050f, 30f, 7.6f },
            new object[] { "Sedan", "Berline", 9800, 1380f, 37f, 8.4f },
            new object[] { "Pickup", "Pick-up", 14000, 1850f, 33f, 7.4f },
            new object[] { "SUV", "SUV", 16500, 1900f, 35f, 7.8f },
            new object[] { "Coupe", "Coupé sport", 28000, 1250f, 46f, 10.5f }
        };

        public const string DriversFolder = CityMeshBuilder.Folder + "/Conducteurs";

        private readonly List<Model> _models = new List<Model>();
        private readonly List<Parked> _parked = new List<Parked>();
        private readonly Transform _holder;
        private readonly NightMaterialFactory.Palette _night;
        private readonly List<Mesh> _drivers = new List<Mesh>();
        private readonly List<Material[]> _driverMaterials = new List<Material[]>();

        public IReadOnlyList<Model> Models { get { return _models; } }

        /// <summary>Les véhicules garés de la ville d'origine (hors vitrine de la concession).</summary>
        public IReadOnlyList<Parked> CityParked { get { return _parked; } }

        /// <summary>Ce qu'il faut cacher dans la ville au chargement : ses véhicules figés, remplacés par les nôtres.</summary>
        public string[] HiddenInCity { get { return _hidden.ToArray(); } }

        private const string CityVehiclesPath = "Map/Container/Vehicles";
        private readonly List<string> _hidden = new List<string> { CityVehiclesPath };

        public bool Available { get { return _models.Count > 0; } }

        private CityVehicles(Transform holder, NightMaterialFactory.Palette night)
        {
            _holder = holder;
            _night = night;
        }

        /// <summary>
        /// Lit les véhicules de la ville (ouverte en additif le temps de la lecture) et prépare
        /// un modèle conduisible de chacun sous <paramref name="holder"/>.
        /// </summary>
        public static CityVehicles Load(Transform holder, NightMaterialFactory.Palette night)
        {
            CityVehicles kit = new CityVehicles(holder, night);
            if (!MapPack.Available) return kit;

            Scene city = SceneManager.GetSceneByPath(MapPack.ScenePath);
            bool opened = false;
            if (!city.IsValid() || !city.isLoaded)
            {
                city = EditorSceneManager.OpenScene(MapPack.ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            try
            {
                Physics.SyncTransforms();
                kit.Read(city);
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(city, true);
            }

            kit.BuildDrivers();
            Debug.Log("[UberBagarre] Voitures de la ville : " + kit._models.Count + " modeles, " + kit._parked.Count +
                      " voitures garees remplacees.");
            return kit;
        }

        private void Read(Scene city)
        {
            Dictionary<string, Transform> sources = new Dictionary<string, Transform>();
            GameObject[] roots = city.GetRootGameObjects();

            for (int r = 0; r < roots.Length; r++)
            {
                Transform[] all = roots[r].GetComponentsInChildren<Transform>(true);
                for (int i = 0; i < all.Length; i++)
                {
                    Transform t = all[i];
                    if (!CityRules.IsVehicleRoot(t)) continue;

                    string model = CityRules.VehicleModel(t.name);
                    string path = CityRules.PathOf(t);
                    bool showroom = path.Contains("/Dealer Complex/");
                    bool parkedInCity = t.parent != null && t.parent.name == "Vehicles";
                    if (!showroom && t.gameObject.activeInHierarchy)
                    {
                        if (!path.StartsWith(CityVehiclesPath + "/") && !_hidden.Contains(path)) _hidden.Add(path);

                        Vector3 position;
                        Vector3 forward;
                        float radius;
                        if (Frame(t, out position, out forward, out radius))
                        {
                            _parked.Add(new Parked
                            {
                                model = model,
                                position = position,
                                yaw = Quaternion.LookRotation(forward).eulerAngles.y
                            });
                        }
                    }

                    // Un exemplaire actif de chaque modèle ; celui de la rue plutôt que celui de
                    // la vitrine (la concession a ses propres réglages de roues).
                    Transform known;
                    bool better = !sources.TryGetValue(model, out known) ||
                                  (!known.gameObject.activeInHierarchy && t.gameObject.activeInHierarchy) ||
                                  (parkedInCity && t.gameObject.activeInHierarchy && known.parent != null &&
                                   known.parent.name != "Vehicles");
                    if (better) sources[model] = t;
                }
            }

            // Les roues à emprunter : celles du pick-up (jantes et pneus complets).
            Transform donor;
            sources.TryGetValue("Pickup", out donor);

            for (int i = 0; i < Catalog.Length; i++)
            {
                string key = (string)Catalog[i][0];
                Transform source;
                if (!sources.TryGetValue(key, out source)) continue;

                Model model = new Model
                {
                    key = key,
                    label = (string)Catalog[i][1],
                    price = (int)Catalog[i][2],
                    mass = (float)Catalog[i][3],
                    maxSpeed = (float)Catalog[i][4],
                    power = (float)Catalog[i][5]
                };

                model.template = BuildTemplate(source, model, donor);
                if (model.template != null) _models.Add(model);
            }
        }

        public Model Find(string key)
        {
            for (int i = 0; i < _models.Count; i++)
            {
                if (_models[i].key == key) return _models[i];
            }

            return _models.Count > 0 ? _models[0] : null;
        }

        // ------------------------------------------------------------------ poser

        /// <summary>Une voiture conduisible, roues au sol, à <paramref name="position"/>.</summary>
        public GameObject Spawn(string key, Transform parent, Vector3 position, float yaw, string displayName)
        {
            Model model = Find(key);
            if (model == null) return null;

            GameObject car = Object.Instantiate(model.template, parent);
            car.name = displayName + " (" + model.label + ")";
            car.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
            SerializedWiring.SetString(car.GetComponent<DrivableCar>(), "_displayName", displayName);

            Interactable door = car.GetComponent<Interactable>();
            if (door != null) SerializedWiring.SetString(door, "_hint", model.label);

            car.SetActive(true);
            return car;
        }

        /// <summary>Une voiture de la circulation : la même, avec un conducteur au volant.</summary>
        public GameObject SpawnTraffic(string key, Transform parent, Vector3[] loop, int start, float cruise, int seed)
        {
            Model model = Find(key);
            if (model == null) return null;

            GameObject car = Spawn(key, parent, loop[start], 0f, "Voiture");
            if (car == null) return null;
            car.name = "Circulation " + (seed + 1) + " (" + model.label + ")";

            Interactable door = car.GetComponent<Interactable>();
            if (door != null)
            {
                SerializedWiring.SetString(door, "_label", "Prendre la voiture");
                SerializedWiring.SetString(door, "_hint", model.label + " — son conducteur s'enfuit");
            }

            SerializedWiring.SetEnum(car.GetComponent<DrivableCar>(), "_access", (int)DrivableCar.Access.Circulation);
            GameObject driver = SeatDriver(car.transform, model, seed);

            TrafficDriver pilot = car.AddComponent<TrafficDriver>();
            pilot.SetPath(loop, start, cruise);
            if (driver != null) SerializedWiring.SetObject(pilot, "_driverBody", driver);
            EditorUtility.SetDirty(pilot);
            return car;
        }

        /// <summary>Supprime les modèles (s'ils ne doivent pas rester dans la scène).</summary>
        public void Dispose()
        {
            for (int i = 0; i < _models.Count; i++)
            {
                if (_models[i].template != null) Object.DestroyImmediate(_models[i].template);
            }

            _models.Clear();
        }

        // ------------------------------------------------------------------ mesure

        /// <summary>
        /// Le repère d'un véhicule d'après ses roues : centre entre les essieux au sol, avant,
        /// rayon des roues.
        /// </summary>
        private static bool Frame(Transform root, out Vector3 ground, out Vector3 forward, out float radius)
        {
            Vector3[] hubs;
            float[] radii;
            Transform[] wheels;
            ground = root.position;
            forward = root.forward;
            radius = 0.36f;
            if (!Hubs(root, out wheels, out hubs, out radii)) return false;

            radius = Radius(root, hubs, radii);
            Vector3 front = (hubs[0] + hubs[1]) * 0.5f;
            Vector3 rear = (hubs[2] + hubs[3]) * 0.5f;
            forward = front - rear;
            forward.y = 0f;
            if (forward.sqrMagnitude < 0.01f) return false;
            forward.Normalize();

            Vector3 center = (front + rear) * 0.5f;
            float y = (hubs[0].y + hubs[1].y + hubs[2].y + hubs[3].y) * 0.25f - radius;
            ground = new Vector3(center.x, y, center.z);
            return true;
        }

        private static bool Hubs(Transform root, out Transform[] wheels, out Vector3[] hubs, out float[] radii)
        {
            wheels = new Transform[4];
            hubs = new Vector3[4];
            radii = new float[4];

            string key = CityRules.VehicleModel(root.name);
            Transform body = ModelChild(root, key);
            if (body == null) return false;

            for (int i = 0; i < 4; i++)
            {
                wheels[i] = CityRules.FindWheel(body, Corners[i]);
                if (wheels[i] == null) return false;

                bool any;
                Bounds b = CityRules.RendererBounds(wheels[i], out any);
                hubs[i] = any ? b.center : wheels[i].position;
                radii[i] = any ? b.extents.y : -1f;
            }

            return true;
        }

        /// <summary>
        /// Le rayon des roues : mesuré sur les pneus s'ils existent, sinon la hauteur du moyeu
        /// au-dessus du sol (un rayon vers le bas), sinon une roue de compacte.
        /// </summary>
        private static float Radius(Transform root, Vector3[] hubs, float[] radii)
        {
            float sum = 0f;
            int n = 0;
            for (int i = 0; i < 4; i++)
            {
                if (radii[i] <= 0.1f) continue;
                sum += radii[i];
                n++;
            }

            if (n > 0) return sum / n;

            for (int i = 0; i < 4; i++)
            {
                RaycastHit[] hits = Physics.RaycastAll(hubs[i] + Vector3.up * 0.05f, Vector3.down, 1.2f, ~0, QueryTriggerInteraction.Ignore);
                float best = float.MaxValue;
                for (int h = 0; h < hits.Length; h++)
                {
                    if (hits[h].collider.transform.IsChildOf(root)) continue;
                    best = Mathf.Min(best, hits[h].distance - 0.05f);
                }

                if (best < 1f && best > 0.2f)
                {
                    sum += best;
                    n++;
                }
            }

            return n > 0 ? Mathf.Clamp(sum / n, 0.28f, 0.5f) : 0.36f;
        }

        /// <summary>L'enfant qui porte le modèle (« Sedan », « shitbox »…), casse ignorée.</summary>
        private static Transform ModelChild(Transform root, string key)
        {
            if (root == null || key == null) return null;
            for (int i = 0; i < root.childCount; i++)
            {
                Transform c = root.GetChild(i);
                if (string.Equals(c.name, key, System.StringComparison.OrdinalIgnoreCase) &&
                    CityRules.FindWheel(c, "FL") != null) return c;
            }

            return null;
        }

        // ------------------------------------------------------------------ modèle conduisible

        /// <summary>
        /// Échelle de chaque voiture par rapport à son modèle de la carte. Les modèles de Schedule 1
        /// sont trapus (grosses roues, gros volumes) : à côté des maisons et du joueur, ceux de la
        /// circulation paraissaient trop gros, le pick-up et le SUV surtout. Réduits pour retrouver
        /// des gabarits réels : compacte 3,5 m, berline et coupé 4,1 m, SUV 4,0 m, pick-up 4,9 m.
        /// </summary>
        public static float ScaleOf(string key)
        {
            switch (key)
            {
                case "Pickup": return 0.82f;
                case "SUV": return 0.84f;
                case "Sedan": return 0.85f;
                case "Coupe": return 0.85f;
                default: return 0.88f;
            }
        }

        private GameObject BuildTemplate(Transform source, Model model, Transform donor)
        {
            Transform[] wheels;
            Vector3[] hubs;
            float[] radii;
            if (!Hubs(source, out wheels, out hubs, out radii)) return null;

            Vector3 ground;
            Vector3 forward;
            float radius;
            if (!Frame(source, out ground, out forward, out radius)) return null;

            // Plus petites que les modèles de la carte : à côté des maisons et des portes de la
            // ville, elles paraissaient trop grosses. Tout est mesuré après la réduction (roues,
            // carrosserie, siège, collisions), donc la physique suit.
            float k = ScaleOf(model.key);
            radius *= k;
            for (int i = 0; i < hubs.Length; i++) hubs[i] = ground + (hubs[i] - ground) * k;

            Transform body = ModelChild(source, model.key);

            GameObject root = new GameObject("Modele " + model.label);
            root.transform.SetParent(_holder, false);
            root.transform.SetPositionAndRotation(ground, Quaternion.LookRotation(forward, Vector3.up));

            // Les roues de la carte, repérées AVANT la copie (même ordre de parcours).
            string[] wheelPaths = new string[4];
            for (int i = 0; i < 4; i++) wheelPaths[i] = RelativePath(body, wheels[i]);

            GameObject shell = Object.Instantiate(body.gameObject, root.transform);
            shell.name = "Carrosserie";
            shell.SetActive(true);
            shell.transform.SetPositionAndRotation(ground + (body.position - ground) * k, body.rotation);
            shell.transform.localScale = body.lossyScale * k;
            CityPreparation.ClearStatic(shell);
            Strip(shell.transform);

            // --- roues : pivot (braquage, débattement) et rotation, au centre du moyeu
            Transform[] pivots = new Transform[4];
            Transform[] spins = new Transform[4];
            for (int i = 0; i < 4; i++)
            {
                Vector3 local = root.transform.InverseTransformPoint(hubs[i]);
                pivots[i] = EditorBuildUtility.CreateEmpty(WheelNames[i], root.transform, local).transform;
                spins[i] = EditorBuildUtility.CreateEmpty("Rotation", pivots[i], Vector3.zero).transform;

                Transform wheel = shell.transform.Find(wheelPaths[i]);
                bool drawn = false;
                if (wheel != null)
                {
                    bool any;
                    CityRules.RendererBounds(wheel, out any);
                    if (any)
                    {
                        wheel.SetParent(spins[i], true);
                        CityRules.DedupeWheelModels(wheel);
                        drawn = true;
                    }
                }

                if (!drawn) BorrowWheel(donor, i, spins[i], root.transform, radius);
            }

            // --- la carrosserie mesurée (roues exclues : elles sont sous les pivots)
            Bounds shape = LocalBounds(shell.transform, root.transform);
            float width = Mathf.Max(1.4f, shape.size.x);
            float length = Mathf.Max(3f, shape.size.z);
            float top = shape.max.y;
            float bottom = Mathf.Max(shape.min.y, radius * 0.75f);
            float belt = Mathf.Lerp(bottom, top, 0.5f);
            model.halfWidth = width * 0.5f;
            model.height = top;

            // L'habitacle, taillé sur la coque (avant le corps rigide : la mesure pose des colliders
            // de maillage temporaires, interdits sous un corps rigide).
            CarCabinBuilder.Result cab = CarCabinBuilder.Build(root.transform, shell.transform, model.key, shape, radius);

            BoxCollider lower = root.AddComponent<BoxCollider>();
            lower.center = new Vector3(shape.center.x, (bottom + belt) * 0.5f, shape.center.z);
            lower.size = new Vector3(width * 0.98f, belt - bottom, length * 0.98f);

            BoxCollider cabin = root.AddComponent<BoxCollider>();
            cabin.center = new Vector3(shape.center.x, (belt + top) * 0.5f - 0.01f, shape.center.z - length * 0.05f);
            cabin.size = new Vector3(width * 0.86f, Mathf.Max(0.2f, top - belt - 0.02f), length * 0.52f);

            Rigidbody rb = root.AddComponent<Rigidbody>();
            rb.mass = model.mass;
            rb.interpolation = RigidbodyInterpolation.Interpolate;

            // --- places : le siège conducteur à gauche (celui de l'habitacle), la portière à côté
            Transform seat = EditorBuildUtility.CreateEmpty("Siege conducteur", root.transform, cab.Cabin.Hip).transform;
            Transform doorPoint = EditorBuildUtility.CreateEmpty("Portiere", root.transform,
                new Vector3(-width * 0.5f - 0.05f, Mathf.Min(1.05f, top * 0.6f), seat.localPosition.z + 0.2f)).transform;

            // --- feux : ceux du modèle (projecteurs à l'avant, points à l'arrière)
            Light[] headlights;
            Light brake;
            Lights(shell.transform, root.transform, width, length, top, out headlights, out brake);

            AudioSource engine = Source(root.transform, "Son moteur", 1.2f, 45f);
            AudioSource tires = Source(root.transform, "Son pneus", 1f, 35f);
            AudioSource horn = Source(root.transform, "Klaxon", 1f, 60f);
            AudioSource impacts = Source(root.transform, "Chocs", 1f, 40f);

            Interactable interactable = root.AddComponent<Interactable>();
            SerializedWiring.SetString(interactable, "_label", "Conduire");
            SerializedWiring.SetString(interactable, "_hint", model.label);
            SerializedWiring.SetFloat(interactable, "_range", 3.2f);
            SerializedWiring.SetObject(interactable, "_focus", doorPoint);

            DrivableCar car = root.AddComponent<DrivableCar>();
            SerializedObject so = new SerializedObject(car);
            SerializedProperty list = so.FindProperty("_wheels");
            list.arraySize = 4;
            for (int i = 0; i < 4; i++)
            {
                SerializedProperty w = list.GetArrayElementAtIndex(i);
                w.FindPropertyRelative("pivot").objectReferenceValue = pivots[i];
                w.FindPropertyRelative("spin").objectReferenceValue = spins[i];
                w.FindPropertyRelative("front").boolValue = i < 2;
            }

            SerializedProperty lights = so.FindProperty("_headlights");
            lights.arraySize = headlights.Length;
            for (int i = 0; i < headlights.Length; i++) lights.GetArrayElementAtIndex(i).objectReferenceValue = headlights[i];

            so.FindProperty("_seat").objectReferenceValue = seat;
            so.FindProperty("_brakeLight").objectReferenceValue = brake;
            so.FindProperty("_engine").objectReferenceValue = engine;
            so.FindProperty("_tires").objectReferenceValue = tires;
            so.FindProperty("_horn").objectReferenceValue = horn;
            so.FindProperty("_impacts").objectReferenceValue = impacts;
            so.FindProperty("_displayName").stringValue = model.label;
            so.FindProperty("_wheelRadius").floatValue = radius;
            so.FindProperty("_centerOfMass").vector3Value = new Vector3(0f, radius + 0.1f, 0.06f);
            so.FindProperty("_maxSpeed").floatValue = model.maxSpeed;
            so.FindProperty("_engineForce").floatValue = model.mass * model.power;
            // Le caractère de chaque modèle : la compacte souple, le coupé ferme et vif, les gros
            // (pick-up, SUV) plus hauts, plus mous, qui roulent davantage.
            so.FindProperty("_suspensionFrequency").floatValue = model.key == "Coupe" ? 2.4f : model.key == "Shitbox" ? 1.75f :
                model.key == "Pickup" || model.key == "SUV" ? 1.7f : 1.9f;
            so.FindProperty("_antiRollFront").floatValue = model.key == "Coupe" ? 0.35f : model.key == "Pickup" || model.key == "SUV" ? 0.3f : 0.2f;
            so.FindProperty("_antiRollRear").floatValue = model.key == "Coupe" ? 0.2f : model.key == "Pickup" || model.key == "SUV" ? 0.15f : 0.1f;
            so.FindProperty("_grip").floatValue = model.key == "Coupe" ? 1.15f : model.key == "Pickup" ? 0.98f : 1.05f;
            so.FindProperty("_rearGrip").floatValue = model.key == "Coupe" ? 1.2f : model.key == "Pickup" ? 1.02f : 1.1f;
            so.FindProperty("_maxSteer").floatValue = model.key == "Pickup" || model.key == "SUV" ? 34f : 36f;
            so.ApplyModifiedPropertiesWithoutUndo();

            root.SetActive(false);
            return root;
        }

        private static string RelativePath(Transform root, Transform t)
        {
            string path = t.name;
            for (Transform p = t.parent; p != null && p != root; p = p.parent) path = p.name + "/" + path;
            return path;
        }

        /// <summary>Ce qui ne sert qu'au jeu d'origine : colliders, sons, particules, repères.</summary>
        private static void Strip(Transform shell)
        {
            Collider[] colliders = shell.GetComponentsInChildren<Collider>(true);
            for (int i = 0; i < colliders.Length; i++) Object.DestroyImmediate(colliders[i]);

            ParticleSystem[] particles = shell.GetComponentsInChildren<ParticleSystem>(true);
            for (int i = 0; i < particles.Length; i++)
            {
                if (particles[i] != null) Object.DestroyImmediate(particles[i].gameObject);
            }

            AudioSource[] sources = shell.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < sources.Length; i++)
            {
                if (sources[i] != null) Object.DestroyImmediate(sources[i]);
            }

            Transform[] all = shell.GetComponentsInChildren<Transform>(true);
            for (int i = 0; i < all.Length; i++)
            {
                if (all[i] == null || all[i] == shell) continue;
                string n = all[i].name;
                if (n == "AngleGuide" || n == "AxleConnectionPoint" || n.StartsWith("Audio Source"))
                {
                    Object.DestroyImmediate(all[i].gameObject);
                }
            }
        }

        /// <summary>
        /// Une roue empruntée au pick-up (même coin, pour le sens des jantes), à la taille de
        /// celle qui manque, centrée sur le moyeu.
        /// </summary>
        private static void BorrowWheel(Transform donor, int corner, Transform spin, Transform carRoot, float radius)
        {
            if (donor == null) return;

            Transform[] wheels;
            Vector3[] hubs;
            float[] radii;
            if (!Hubs(donor, out wheels, out hubs, out radii) || radii[corner] <= 0.05f) return;

            Vector3 donorGround;
            Vector3 donorForward;
            float donorRadius;
            if (!Frame(donor, out donorGround, out donorForward, out donorRadius)) return;

            Quaternion donorFrame = Quaternion.LookRotation(donorForward, Vector3.up);
            Transform source = wheels[corner];

            GameObject copy = Object.Instantiate(source.gameObject, spin);
            copy.name = "Roue empruntee";
            copy.SetActive(true);
            CityPreparation.ClearStatic(copy);
            Strip(copy.transform);

            copy.transform.rotation = carRoot.rotation * (Quaternion.Inverse(donorFrame) * source.rotation);
            copy.transform.localScale = source.lossyScale * (radius / radii[corner]);

            bool any;
            Bounds b = CityRules.RendererBounds(copy.transform, out any);
            if (any) copy.transform.position += spin.position - b.center;
        }

        /// <summary>La boîte des rendus d'un sous-arbre, dans le repère de <paramref name="frame"/>.</summary>
        private static Bounds LocalBounds(Transform root, Transform frame)
        {
            bool first = true;
            Bounds result = new Bounds(Vector3.zero, Vector3.zero);
            Renderer[] renderers = root.GetComponentsInChildren<Renderer>(false);

            for (int i = 0; i < renderers.Length; i++)
            {
                Renderer r = renderers[i];
                if (!r.enabled || r is ParticleSystemRenderer) continue;

                Bounds local = r.localBounds;
                for (int c = 0; c < 8; c++)
                {
                    Vector3 corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((c & 1) == 0 ? -1f : 1f, (c & 2) == 0 ? -1f : 1f, (c & 4) == 0 ? -1f : 1f));
                    Vector3 p = frame.InverseTransformPoint(r.transform.TransformPoint(corner));
                    if (first)
                    {
                        result = new Bounds(p, Vector3.zero);
                        first = false;
                    }
                    else
                    {
                        result.Encapsulate(p);
                    }
                }
            }

            return result;
        }

        private static void Lights(Transform shell, Transform root, float width, float length, float height,
            out Light[] headlights, out Light brake)
        {
            List<Light> front = new List<Light>();
            brake = null;

            Light[] all = shell.GetComponentsInChildren<Light>(true);
            for (int i = 0; i < all.Length; i++)
            {
                Light light = all[i];
                float z = root.InverseTransformPoint(light.transform.position).z;
                light.shadows = LightShadows.None;
                light.bounceIntensity = 0f;

                if (light.type == LightType.Spot && z > 0f && front.Count < 2)
                {
                    front.Add(light);
                    continue;
                }

                if (light.type == LightType.Point && z < 0f && brake == null)
                {
                    brake = light;
                    continue;
                }

                Object.DestroyImmediate(light);
            }

            // Pas de phares dans le modèle : on en pose deux, à l'avant.
            while (front.Count < 2)
            {
                float side = front.Count == 0 ? -1f : 1f;
                GameObject go = EditorBuildUtility.CreateEmpty(side < 0f ? "Phare gauche" : "Phare droit", root,
                    new Vector3(side * width * 0.32f, height * 0.5f, length * 0.48f));
                Light spot = go.AddComponent<Light>();
                spot.type = LightType.Spot;
                front.Add(spot);
            }

            for (int i = 0; i < front.Count; i++)
            {
                Light spot = front[i];
                spot.transform.rotation = root.rotation * Quaternion.Euler(6f, 0f, 0f);
                spot.spotAngle = 58f;
                spot.range = 30f;
                spot.intensity = 2.6f;
                spot.color = new Color(1f, 0.95f, 0.86f);
                spot.shadows = LightShadows.None;
                spot.enabled = false;
            }

            if (brake == null)
            {
                brake = NightStreetBuilder.AddLight(root, "Feu stop", new Vector3(0f, height * 0.55f, -length * 0.5f - 0.05f),
                    new Color(1f, 0.08f, 0.06f), 0f, 4.5f, false, false);
            }

            brake.type = LightType.Point;
            brake.color = new Color(1f, 0.08f, 0.06f);
            brake.range = 4.5f;
            brake.intensity = 0f;
            brake.enabled = false;
            headlights = front.ToArray();
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

        // ------------------------------------------------------------------ conducteurs

        /// <summary>
        /// Trois conducteurs, assis une fois pour toutes : un corps du jeu posé au volant
        /// (cuisses vers l'avant, tibias vers les pédales, mains sur le volant), figé en maillage.
        /// </summary>
        private void BuildDrivers()
        {
            EditorBuildUtility.EnsureFolder(DriversFolder);
            string[] silhouettes = { "Sec", "Costaud", "Athlete" };
            Color[] tops = { new Color(0.14f, 0.16f, 0.22f), new Color(0.36f, 0.12f, 0.10f), new Color(0.46f, 0.44f, 0.40f) };

            for (int i = 0; i < silhouettes.Length; i++)
            {
                if (!CorpsImporter.Exists(silhouettes[i])) continue;
                CorpsImporter.Data data = CorpsImporter.Load(silhouettes[i], true);
                if (data == null) continue;

                List<string> slots;
                Mesh mesh = CorpsImporter.BuildMesh(data, CorpsImporter.Top.Veste, true, out slots);
                Material top = PrologueSceneBuilder.Jacket(_night, "M_Conducteur_Haut_" + i, tops[i]);
                Material pants = PrologueSceneBuilder.Jacket(_night, "M_Conducteur_Bas_" + i, new Color(0.10f, 0.12f, 0.2f));
                Material shoes = EditorBuildUtility.CreateOrUpdateMaterial(NightMaterialFactory.MaterialsFolder,
                    "M_Conducteur_Chaussures", new Color(0.06f, 0.05f, 0.05f), 0.3f, 0f);
                Material[] materials = CorpsImporter.MaterialsFor(data.Name, slots, top, pants, shoes);

                GameObject temp = new GameObject("Conducteur (pose)");
                CorpsImporter.Built built = CorpsImporter.BuildSkeleton(data, temp.transform, mesh, materials);
                Sit(built, temp.transform);

                Mesh baked = new Mesh();
                built.Renderer.BakeMesh(baked);
                baked.RecalculateBounds();

                // Le maillage cuit est dans le repère du rendu : on le ramène à celui du corps.
                Matrix4x4 toRoot = temp.transform.worldToLocalMatrix * built.Renderer.transform.localToWorldMatrix;
                Vector3[] vertices = baked.vertices;
                Vector3[] normals = baked.normals;
                for (int v = 0; v < vertices.Length; v++)
                {
                    vertices[v] = toRoot.MultiplyPoint3x4(vertices[v]);
                    if (v < normals.Length) normals[v] = toRoot.MultiplyVector(normals[v]).normalized;
                }

                baked.vertices = vertices;
                baked.normals = normals;
                baked.RecalculateBounds();

                Vector3 pelvis = temp.transform.InverseTransformPoint(built["Pelvis"].position);
                Object.DestroyImmediate(temp);

                // Origine = le bassin : le conducteur se pose sur le siège par là.
                for (int v = 0; v < vertices.Length; v++) vertices[v] -= pelvis;
                baked.vertices = vertices;
                baked.RecalculateBounds();

                _drivers.Add(SaveMesh(baked, "Conducteur_" + silhouettes[i]));
                _driverMaterials.Add(materials);
            }
        }

        private static Mesh SaveMesh(Mesh mesh, string fileName)
        {
            string path = DriversFolder + "/" + fileName + ".asset";
            mesh.name = fileName;

            Mesh existing = AssetDatabase.LoadAssetAtPath<Mesh>(path);
            if (existing == null)
            {
                AssetDatabase.CreateAsset(mesh, path);
                return mesh;
            }

            EditorUtility.CopySerialized(mesh, existing);
            Object.DestroyImmediate(mesh);
            EditorUtility.SetDirty(existing);
            return existing;
        }

        /// <summary>
        /// La pose assise, par visée : chaque os est tourné pour que son enfant parte dans la
        /// direction voulue (repère du corps : avant = +Z, gauche = −X). Indépendant de la pose
        /// de repos du squelette.
        /// </summary>
        private static void Sit(CorpsImporter.Built b, Transform root)
        {
            Vector3 F = root.forward, U = root.up, R = root.right;

            // Légèrement renversé dans le siège.
            Aim(b["Spine"], b["Chest"], (U * 0.96f - F * 0.18f).normalized);
            Aim(b["Chest"], b["Neck"], (U * 0.98f - F * 0.1f).normalized);
            Aim(b["Neck"], b["Head"], (U * 0.97f + F * 0.12f).normalized);

            for (int s = 0; s < 2; s++)
            {
                string side = s == 0 ? "Left" : "Right";
                float sign = s == 0 ? -1f : 1f;

                // Jambes : cuisses vers l'avant (genoux un peu hauts), tibias vers les pédales.
                Aim(b[side + "Thigh"], b[side + "Shin"], (F * 0.97f + U * 0.08f + R * sign * 0.12f).normalized);
                Aim(b[side + "Shin"], b[side + "Ankle"], (-U * 0.8f + F * 0.55f).normalized);
                Aim(b[side + "Ankle"], b[side + "Toe"], (F * 0.9f + U * 0.3f).normalized);

                // Bras : vers le volant, coudes bas, mains à dix heures dix.
                Aim(b[side + "UpperArm"], b[side + "Forearm"], (F * 0.55f - U * 0.72f + R * sign * 0.18f).normalized);
                Aim(b[side + "Forearm"], b[side + "Wrist"], (F * 0.9f + U * 0.36f - R * sign * 0.2f).normalized);
            }
        }

        private static void Aim(Transform bone, Transform child, Vector3 direction)
        {
            if (bone == null || child == null) return;
            Vector3 current = child.position - bone.position;
            if (current.sqrMagnitude < 1e-6f) return;
            bone.rotation = Quaternion.FromToRotation(current, direction) * bone.rotation;
        }

        /// <summary>Un conducteur figé, assis sur le siège de la voiture.</summary>
        private GameObject SeatDriver(Transform car, Model model, int seed)
        {
            if (_drivers.Count == 0) return null;
            int k = Mathf.Abs(seed) % _drivers.Count;

            Transform seat = car.Find("Siege conducteur");
            Vector3 at = seat != null ? seat.localPosition : new Vector3(-0.35f, 0.5f, -0.2f);

            GameObject driver = EditorBuildUtility.CreateEmpty("Conducteur", car, at + new Vector3(0f, 0.08f, 0f));
            driver.AddComponent<MeshFilter>().sharedMesh = _drivers[k];
            MeshRenderer renderer = driver.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = _driverMaterials[k];
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            return driver;
        }
    }
}
