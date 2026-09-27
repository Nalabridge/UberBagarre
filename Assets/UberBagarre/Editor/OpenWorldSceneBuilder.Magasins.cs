using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UberBagarre.View;
using UberBagarre.World;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les commerces de la ville : chacun a un vendeur, un comptoir, et un usage (voir
    /// <see cref="ShopCatalog"/>).
    ///
    /// - Ceux que la carte a meublés (la laverie, la poste, les stations-service, la supérette,
    ///   les quincailleries, le barbier, le tatoueur, l'agence, la concession…) : on y entre par
    ///   leur vraie porte ; le vendeur est posé là où la carte mettait son employé (le repère
    ///   « StandPoint »), sinon derrière la caisse, sinon à quelques pas dans l'entrée.
    /// - Ceux dont la carte n'a que la façade (le casino, la pharmacie, les bars, les
    ///   restaurants, l'arcade, la salle de boxe…) : leur porte mène à un intérieur construit
    ///   pour eux (<see cref="ShopInteriorBuilder"/>), à l'écart de la ville.
    /// - Le bar du Vertigo, dans le club.
    /// </summary>
    public static partial class OpenWorldSceneBuilder
    {
        private struct ShopSpec
        {
            public string Building;
            public string Name;
            public ShopKind Kind;
            public bool Facade;
            public string Filter;

            public ShopSpec(string building, string name, ShopKind kind, bool facade, string filter = null)
            {
                Building = building;
                Name = name;
                Kind = kind;
                Facade = facade;
                Filter = filter;
            }
        }

        private static readonly ShopSpec[] ShopSpecs =
        {
            // --- les intérieurs de la carte
            new ShopSpec("@Businesses/Laundromat", "Laverie Bulles", ShopKind.Laverie, false),
            new ShopSpec("@Businesses/PostOffice", "La Poste", ShopKind.Poste, false),
            new ShopSpec("@Businesses/Taco Ticklers", "Taco Ticklers", ShopKind.Restaurant, false),
            new ShopSpec("Barbershop", "Chez Tony, barbier", ShopKind.Barbier, false),
            new ShopSpec("Clothing store with interior", "Hyland Fringues", ShopKind.Vetements, false),
            new ShopSpec("Dark Market Area", "Le Hangar", ShopKind.MarcheNoir, false),
            new ShopSpec("Diner", "Diner de la 5e", ShopKind.Restaurant, false),
            new ShopSpec("Gas Station", "Station-service", ShopKind.Epicerie, false),
            new ShopSpec("Slums Gas Station", "Station du sud", ShopKind.Epicerie, false),
            new ShopSpec("GroceryStore", "Supérette Hyland", ShopKind.Epicerie, false),
            new ShopSpec("HardwardStore", "Quincaillerie de l'Est", ShopKind.Quincaillerie, false),
            new ShopSpec("North town/Hardware Store", "Chez Dan, quincaillerie", ShopKind.Quincaillerie, false),
            new ShopSpec("RE Office", "Hyland Immobilier", ShopKind.Immobilier, false),
            new ShopSpec("Tattoo Parlour New", "Encre Noire", ShopKind.Tatoueur, false),
            new ShopSpec("North town/Jeff's Shred Shack", "Jeff's Shred Shack", ShopKind.Vetements, false),
            new ShopSpec("Dealer Complex", "Hyland Auto", ShopKind.Concession, false),

            // --- les façades : un intérieur construit pour elles
            new ShopSpec("Casino", "Casino Royal", ShopKind.Casino, true),
            new ShopSpec("Boutique Store", "Boutique Elegance", ShopKind.Vetements, true),
            new ShopSpec("Cafe", "Cafe du Port", ShopKind.Cafe, true),
            new ShopSpec("LawOffice", "Cabinet Lenoir", ShopKind.Avocat, true),
            new ShopSpec("North town/Chinese Restaurant", "Le Dragon d'Or", ShopKind.Restaurant, true),
            new ShopSpec("North town/North Bar", "Bar du Nord", ShopKind.Bar, true),
            new ShopSpec("North town/Pharmacy", "Pharmacie du Nord", ShopKind.Pharmacie, true),
            new ShopSpec("North town/Pizzeria", "Pizzeria Mamma", ShopKind.Restaurant, true),
            new ShopSpec("North town/Shooting range", "Stand de tir", ShopKind.StandDeTir, true),
            new ShopSpec("North town/Arcade (1)", "Arcade 2000", ShopKind.Arcade, true),
            new ShopSpec("Community center", "Boxe Club", ShopKind.SalleDeBoxe, true),
            new ShopSpec("Bar", "Le Zinc", ShopKind.Bar, true),
            new ShopSpec("Liquor Store", "La Cave", ShopKind.Cave, true),
            new ShopSpec("Medical Practice", "Cabinet medical", ShopKind.Medecin, true),
            new ShopSpec("Pawn shop", "Preteur sur gages", ShopKind.PreteurSurGages, true),
            new ShopSpec("Restaurant", "Chez Marco", ShopKind.Restaurant, true),
            new ShopSpec("Motel", "Motel Hyland", ShopKind.Motel, true, "Office_Complete")
        };

        private static readonly Vector3 ShopInteriorsOrigin = new Vector3(1000f, 0.5f, -240f);

        private static string CounterLabel(ShopKind kind)
        {
            switch (kind)
            {
                case ShopKind.Restaurant:
                case ShopKind.Cafe:
                case ShopKind.Bar:
                case ShopKind.BoiteDeNuit: return "Commander";
                case ShopKind.Casino:
                case ShopKind.Arcade: return "Jouer";
                case ShopKind.StandDeTir:
                case ShopKind.SalleDeBoxe: return "S'inscrire";
                case ShopKind.Laverie: return "Faire une lessive";
                case ShopKind.Poste:
                case ShopKind.Commissariat: return "Aller au guichet";
                case ShopKind.Immobilier: return "Voir les biens";
                case ShopKind.Concession: return "Voir les voitures";
                case ShopKind.Avocat:
                case ShopKind.Medecin: return "Consulter";
                case ShopKind.Barbier:
                case ShopKind.Tatoueur: return "S'installer";
                case ShopKind.Motel: return "À l'accueil";
                default: return "Acheter";
            }
        }

        private static ShopDirectory BuildShops(GameObject systems, GameObject player, PlayerProgress progress,
            NightMaterialFactory.Palette night, BuildMaterials materials, SubtitleDisplay subtitles, ScreenFader fader,
            VehicleCatalog vehicles, HomeRegistry registry, ComputerScreen computer, ClubInteriorBuilder.Result club, CityMap cityMap)
        {
            List<CityMap.Landmark> marks = new List<CityMap.Landmark>();
            Color shopColor = new Color(1f, 0.8f, 0.3f);
            ShopScreen screen = systems.AddComponent<ShopScreen>();
            WirePanel(screen, player, progress);
            screen.Configure(vehicles, registry, computer, subtitles);
            EditorUtility.SetDirty(screen);

            PlayerBuffs buffs = player.AddComponent<PlayerBuffs>();
            SerializedWiring.SetObject(buffs, "_combatant", player.GetComponent<Combatant>());

            GameObject root = new GameObject("=== Magasins ===");
            MocapLibrary library = MocapLibraryBuilder.Build();
            List<Shop> shops = new List<Shop>();

            Scene city = SceneManager.GetSceneByPath(MapPack.ScenePath);
            bool opened = false;
            if (!city.IsValid() || !city.isLoaded)
            {
                city = EditorSceneManager.OpenScene(MapPack.ScenePath, OpenSceneMode.Additive);
                opened = true;
            }

            Physics.SyncTransforms();
            int facades = 0;
            int seed = 0;

            try
            {
                for (int i = 0; i < ShopSpecs.Length; i++)
                {
                    ShopSpec spec = ShopSpecs[i];
                    GameObject go = EditorBuildUtility.CreateEmpty(spec.Name, root.transform, Vector3.zero);
                    Shop shop = go.AddComponent<Shop>();

                    if (spec.Facade)
                    {
                        Vector3 origin = ShopInteriorsOrigin + new Vector3(facades * 40f, 0f, 0f);
                        facades++;
                        ShopInteriorBuilder.Result room = ShopInteriorBuilder.Build(spec.Kind, spec.Name, go.transform, origin, night);
                        ShopClerk clerk = BuildClerk(room.Root.transform, room.ClerkPosition, room.ClerkRotation, spec.Kind, seed++,
                            materials, night, library, subtitles);
                        Interactable counter = CounterOn(room.Counter.gameObject, spec);
                        shop.Configure(spec.Name, spec.Kind, spec.Building, counter, screen, clerk, room.Root, room.Arrival, room.Exit, spec.Filter);

                        Vector3 front;
                        if (FacadePosition(city, spec, out front))
                        {
                            marks.Add(new CityMap.Landmark { label = spec.Name, position = new Vector2(front.x, front.z), color = shopColor });
                        }
                    }
                    else
                    {
                        Vector3 position;
                        Quaternion rotation;
                        if (!FindCounter(city, spec.Building, out position, out rotation))
                        {
                            Debug.LogWarning("[UberBagarre] Magasin « " + spec.Name + " » : batiment introuvable dans la ville (" + spec.Building + ").");
                            Object.DestroyImmediate(go);
                            continue;
                        }

                        ShopClerk clerk = BuildClerk(go.transform, position, rotation, spec.Kind, seed++, materials, night, library, subtitles);
                        Interactable counter = CounterOn(clerk.gameObject, spec);
                        shop.Configure(spec.Name, spec.Kind, spec.Building, counter, screen, clerk, null, null, null, spec.Filter);
                        marks.Add(new CityMap.Landmark { label = spec.Name, position = new Vector2(position.x, position.z), color = shopColor });
                    }

                    EditorUtility.SetDirty(shop);
                    shops.Add(shop);
                }
            }
            finally
            {
                if (opened) EditorSceneManager.CloseScene(city, true);
            }

            // --- le bar du Vertigo : derrière le comptoir du club
            Transform bar = club != null && club.Root != null ? club.Root.Find("Bar") : null;
            if (bar != null)
            {
                GameObject go = EditorBuildUtility.CreateEmpty("Bar du Vertigo", root.transform, Vector3.zero);
                Shop shop = go.AddComponent<Shop>();
                ShopSpec spec = new ShopSpec("Vertigo", "Bar du Vertigo", ShopKind.BoiteDeNuit, false);
                ShopClerk clerk = BuildClerk(bar, bar.TransformPoint(new Vector3(-1f, 0f, 0f)), Quaternion.LookRotation(bar.right, Vector3.up),
                    spec.Kind, seed++, materials, night, library, subtitles);
                Interactable counter = CounterOn(clerk.gameObject, spec);
                shop.Configure(spec.Name, spec.Kind, spec.Building, counter, screen, clerk, null, null, null, null);
                EditorUtility.SetDirty(shop);
                shops.Add(shop);
            }

            if (cityMap != null)
            {
                cityMap.AddLandmarks(marks.ToArray());
                EditorUtility.SetDirty(cityMap);
            }

            ShopDirectory directory = systems.AddComponent<ShopDirectory>();
            directory.Configure(shops.ToArray(), player, fader, subtitles);
            EditorUtility.SetDirty(directory);

            Debug.Log("[UberBagarre] Magasins : " + shops.Count + " (dont " + facades + " interieurs construits derriere une facade).");
            return directory;
        }

        /// <summary>L'action du comptoir : sur l'objet donné (un comptoir, un vendeur).</summary>
        private static Interactable CounterOn(GameObject go, ShopSpec spec)
        {
            Interactable counter = go.GetComponent<Interactable>();
            if (counter == null) counter = go.AddComponent<Interactable>();
            SerializedWiring.SetString(counter, "_label", CounterLabel(spec.Kind));
            SerializedWiring.SetString(counter, "_hint", spec.Name + " — " + ShopCatalog.Describe(spec.Kind));
            SerializedWiring.SetFloat(counter, "_range", 3.2f);
            return counter;
        }

        // ------------------------------------------------------------------ placement dans la ville

        /// <summary>La racine d'un bâtiment de la ville d'après sa clé (« North town/Pizzeria »…).</summary>
        private static Transform BuildingRoot(Scene city, string building)
        {
            string path = building.StartsWith("@") ? building : "Map/Container/" + building;
            int slash = path.IndexOf('/');
            string head = path.Substring(0, slash);
            GameObject[] roots = city.GetRootGameObjects();
            for (int i = 0; i < roots.Length; i++)
            {
                if (roots[i].name != head) continue;
                Transform found = roots[i].transform.Find(path.Substring(slash + 1));
                if (found != null) return found;
            }

            return null;
        }

        /// <summary>
        /// Où se tient le vendeur d'un magasin de la carte : le repère d'employé (« StandPoint »)
        /// s'il existe, sinon derrière la caisse (du côté opposé à la porte), sinon trois pas
        /// dans l'entrée. Tourné vers la porte.
        /// </summary>
        private static bool FindCounter(Scene city, string building, out Vector3 position, out Quaternion rotation)
        {
            position = Vector3.zero;
            rotation = Quaternion.identity;
            Transform root = BuildingRoot(city, building);
            if (root == null) return false;

            Transform stand = null, register = null;
            List<Transform> doors = new List<Transform>();
            Transform[] all = root.GetComponentsInChildren<Transform>(false);
            for (int i = 0; i < all.Length; i++)
            {
                string n = all[i].name.ToLowerInvariant();
                if (stand == null && (n.Contains("standpoint") || n.Contains("stand point"))) stand = all[i];
                if (register == null && (n == "cash register" || n == "cashcounter" || n == "counter" || n == "frontdesk" || n == "ornate desk")) register = all[i];
                if (CityRules.IsDoorHinge(all[i])) doors.Add(all[i]);
            }

            // Pour la concession : pas de gond, des portes coulissantes ; la vitrine suffit.
            Vector3 door = doors.Count > 0 ? doors[0].position : root.position;
            Vector3 inward = Vector3.forward;
            if (doors.Count > 0)
            {
                Transform inner = doors[0].Find("InteriorIntObj");
                Transform outer = doors[0].Find("ExteriorIntObj");
                if (inner != null && outer != null) inward = Flat(inner.position - outer.position).normalized;
            }

            if (stand != null)
            {
                position = stand.position;
                Vector3 look = register != null ? Flat(register.position - stand.position) : Flat(door - stand.position);
                rotation = look.sqrMagnitude > 0.01f ? Quaternion.LookRotation(look.normalized, Vector3.up) : Quaternion.identity;
                position = Ground(position + Vector3.up * 0.5f, position.y);
                return true;
            }

            if (register != null)
            {
                if (doors.Count > 0)
                {
                    float best = float.MaxValue;
                    for (int i = 0; i < doors.Count; i++)
                    {
                        float dd = (doors[i].position - register.position).sqrMagnitude;
                        if (dd < best)
                        {
                            best = dd;
                            door = doors[i].position;
                        }
                    }
                }

                Vector3 away = Flat(register.position - door);
                if (away.sqrMagnitude < 0.01f) away = inward;
                away.Normalize();
                position = Ground(register.position + away * 0.75f + Vector3.up * 0.3f, register.position.y - 0.9f);
                rotation = Quaternion.LookRotation(-away, Vector3.up);
                return true;
            }

            if (doors.Count == 0) return false;

            // Rien : quelques pas dans l'entrée, là où il y a de la place.
            float[] steps = { 3f, 2.4f, 3.8f, 1.8f };
            float[] sides = { 0f, 1.2f, -1.2f };
            Vector3 lateral = Vector3.Cross(Vector3.up, inward);
            for (int s = 0; s < steps.Length; s++)
            {
                for (int k = 0; k < sides.Length; k++)
                {
                    Vector3 p = door + inward * steps[s] + lateral * sides[k];
                    p = Ground(p + Vector3.up * 0.5f, doors[0].position.y - 1f);
                    if (Physics.CheckCapsule(p + Vector3.up * 0.35f, p + Vector3.up * 1.5f, 0.3f, ~0, QueryTriggerInteraction.Ignore)) continue;
                    position = p;
                    rotation = Quaternion.LookRotation(-inward, Vector3.up);
                    return true;
                }
            }

            position = Ground(door + inward * 2.5f + Vector3.up * 0.5f, doors[0].position.y - 1f);
            rotation = Quaternion.LookRotation(-inward, Vector3.up);
            return true;
        }

        /// <summary>La porte de façade d'un magasin (pour la carte), sinon le bâtiment.</summary>
        private static bool FacadePosition(Scene city, ShopSpec spec, out Vector3 position)
        {
            position = Vector3.zero;
            Transform root = BuildingRoot(city, spec.Building);
            if (root == null) return false;

            position = root.position;
            Transform[] all = root.GetComponentsInChildren<Transform>(false);
            for (int i = 0; i < all.Length; i++)
            {
                if (!CityRules.IsStaticDoor(all[i])) continue;
                if (!string.IsNullOrEmpty(spec.Filter) && !CityRules.PathOf(all[i]).Contains(spec.Filter)) continue;
                position = all[i].position;
                break;
            }

            return true;
        }

        /// <summary>Le sol sous un point (rayon vers le bas), sinon la hauteur donnée.</summary>
        private static Vector3 Ground(Vector3 from, float fallback)
        {
            RaycastHit hit;
            if (Physics.Raycast(from + Vector3.up * 0.8f, Vector3.down, out hit, 3.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                return new Vector3(from.x, hit.point.y, from.z);
            }

            return new Vector3(from.x, fallback, from.z);
        }

        private static Vector3 Flat(Vector3 v)
        {
            v.y = 0f;
            return v;
        }

        // ------------------------------------------------------------------ vendeurs

        private static readonly string[] ClerkSilhouettes = { "Sec", "Athlete", "Costaud", "Sec" };

        private static ShopClerk BuildClerk(Transform parent, Vector3 position, Quaternion rotation, ShopKind kind, int n,
            BuildMaterials materials, NightMaterialFactory.Palette night, MocapLibrary library, SubtitleDisplay subtitles)
        {
            GameObject go = new GameObject("Vendeur");
            go.transform.SetParent(parent, true);
            go.transform.SetPositionAndRotation(position, rotation);

            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.75f;
            capsule.radius = 0.28f;
            capsule.center = new Vector3(0f, 0.875f, 0f);

            Color[] tops =
            {
                new Color(0.72f, 0.1f, 0.12f), new Color(0.1f, 0.24f, 0.46f), new Color(0.86f, 0.86f, 0.84f),
                new Color(0.12f, 0.36f, 0.2f), new Color(0.08f, 0.08f, 0.09f)
            };

            FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);
            skin.Silhouette = ClerkSilhouettes[n % ClerkSilhouettes.Length];
            skin.Top = n % 3 == 0 ? CorpsImporter.Top.TShirt : CorpsImporter.Top.Veste;
            skin.Shirt = PrologueSceneBuilder.Jacket(night, "M_Vendeur_Haut_" + (n % tops.Length), tops[n % tops.Length]);
            skin.Pants = PrologueSceneBuilder.Jacket(night, "M_Vendeur_Bas", new Color(0.12f, 0.12f, 0.14f));
            skin.Crowd = true;

            FighterBuilder.Result body = FighterBuilder.BuildBody(go.transform, go.transform, skin, true, Faction.Neutral, go);
            Hitbox[] hitboxes = go.GetComponentsInChildren<Hitbox>(true);
            for (int h = 0; h < hitboxes.Length; h++) Object.DestroyImmediate(hitboxes[h]);

            Animator animator = null;
            Avatar avatar = library != null && body.Data != null ? CorpsAvatarBuilder.For(body.Data, body.Body.name) : null;
            if (avatar != null)
            {
                animator = body.Body.AddComponent<Animator>();
                animator.avatar = avatar;
                animator.applyRootMotion = false;
                animator.enabled = false;
            }

            ShopClerk clerk = go.AddComponent<ShopClerk>();
            clerk.Configure(animator, library, subtitles, ShopCatalog.Describe(kind).ToUpperInvariant(), ShopCatalog.Greeting(kind), body.Locomotion);
            EditorUtility.SetDirty(clerk);
            return clerk;
        }
    }
}
