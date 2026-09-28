using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Story;
using UberBagarre.View;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La distribution de l'histoire : les gens à qui l'on parle (Coach Ray, Nestor, Lina,
    /// maman, M. Chen, Tony, Jeff, Karim, Rosa, Sarkis, l'inspectrice Duval, le commissaire
    /// Brandt, le maire Holt, Mme Keller, Sami, Inès…), et les lieux où l'histoire les pose.
    /// </summary>
    public static partial class OpenWorldSceneBuilder
    {
        private sealed class ActorLook
        {
            public string Id;
            public string Name;
            public string Silhouette;
            public CorpsImporter.Top Top;
            public Color Shirt;
            public Color Pants;
            public bool Walks;
        }

        private static readonly ActorLook[] Cast =
        {
            new ActorLook { Id = "RAY", Name = "COACH RAY", Silhouette = "Costaud", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.34f, 0.35f, 0.40f), Pants = new Color(0.12f, 0.12f, 0.14f) },
            new ActorLook { Id = "NESTOR", Name = "NESTOR", Silhouette = "Colosse", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.45f, 0.33f, 0.20f), Pants = new Color(0.10f, 0.08f, 0.07f) },
            new ActorLook { Id = "GORILLE1", Name = "UN ASSOCIÉ", Silhouette = "Colosse", Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.05f, 0.05f, 0.06f), Pants = new Color(0.08f, 0.08f, 0.09f) },
            new ActorLook { Id = "GORILLE2", Name = "UN ASSOCIÉ", Silhouette = "Costaud", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.07f, 0.07f, 0.08f), Pants = new Color(0.07f, 0.07f, 0.08f) },
            new ActorLook { Id = "LINA", Name = "LINA", Silhouette = "Sec", Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.20f, 0.55f, 0.55f), Pants = new Color(0.18f, 0.48f, 0.48f) },
            new ActorLook { Id = "MAMAN", Name = "MAMAN", Silhouette = "Sec", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.55f, 0.45f, 0.60f), Pants = new Color(0.20f, 0.20f, 0.30f) },
            new ActorLook { Id = "CHEN", Name = "M. CHEN", Silhouette = "Sec", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.86f, 0.86f, 0.83f), Pants = new Color(0.10f, 0.10f, 0.12f) },
            new ActorLook { Id = "TONY", Name = "TONY", Silhouette = "Costaud", Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.90f, 0.90f, 0.90f), Pants = new Color(0.15f, 0.15f, 0.20f) },
            new ActorLook { Id = "JEFF", Name = "JEFF", Silhouette = "Sec", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.70f, 0.20f, 0.20f), Pants = new Color(0.20f, 0.25f, 0.40f) },
            new ActorLook { Id = "KARIM", Name = "KARIM", Silhouette = "Athlete", Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.95f, 0.55f, 0.10f), Pants = new Color(0.15f, 0.15f, 0.18f) },
            new ActorLook { Id = "ROSA", Name = "ROSA DELMAS", Silhouette = "Sec", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.55f, 0.06f, 0.12f), Pants = new Color(0.08f, 0.02f, 0.04f) },
            new ActorLook { Id = "SARKIS", Name = "VICTOR SARKIS", Silhouette = "Costaud", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.34f, 0.35f, 0.37f), Pants = new Color(0.15f, 0.15f, 0.17f) },
            new ActorLook { Id = "DUVAL", Name = "INSPECTRICE DUVAL", Silhouette = "Athlete", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.62f, 0.52f, 0.36f), Pants = new Color(0.12f, 0.12f, 0.15f) },
            new ActorLook { Id = "BRANDT", Name = "COMMISSAIRE BRANDT", Silhouette = "Colosse", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.10f, 0.12f, 0.22f), Pants = new Color(0.08f, 0.08f, 0.12f) },
            new ActorLook { Id = "HOLT", Name = "ARTHUR HOLT", Silhouette = "Sec", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.12f, 0.14f, 0.30f), Pants = new Color(0.12f, 0.14f, 0.30f) },
            new ActorLook { Id = "KELLER", Name = "MME KELLER", Silhouette = "Sec", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.20f, 0.45f, 0.35f), Pants = new Color(0.15f, 0.15f, 0.18f), Walks = true },
            new ActorLook { Id = "SAMI", Name = "SAMI", Silhouette = "Athlete", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.24f, 0.27f, 0.15f), Pants = new Color(0.10f, 0.15f, 0.30f), Walks = true },
            new ActorLook { Id = "INES", Name = "INÈS", Silhouette = "Sec", Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.60f, 0.75f, 0.85f), Pants = new Color(0.60f, 0.75f, 0.85f) },
            new ActorLook { Id = "TAUREAU", Name = "LE TAUREAU", Silhouette = "Colosse", Top = CorpsImporter.Top.Debardeur, Shirt = new Color(0.62f, 0.05f, 0.05f), Pants = new Color(0.06f, 0.06f, 0.07f) },
            new ActorLook { Id = "VIGILE1", Name = "VIGILE", Silhouette = "Costaud", Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.05f, 0.05f, 0.06f), Pants = new Color(0.06f, 0.06f, 0.07f) },
            new ActorLook { Id = "VIGILE2", Name = "VIGILE", Silhouette = "Colosse", Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.05f, 0.05f, 0.06f), Pants = new Color(0.06f, 0.06f, 0.07f) },
            new ActorLook { Id = "VIGILE3", Name = "VIGILE", Silhouette = "Costaud", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.05f, 0.05f, 0.06f), Pants = new Color(0.06f, 0.06f, 0.07f) }
        };

        private static StoryCast BuildStoryCast(GameObject systems, MapPack.Data map, BuildMaterials materials,
            NightMaterialFactory.Palette night, ClubInteriorBuilder.Result club, ShopDirectory shops, List<Vector3[]> walkLoops)
        {
            GameObject root = new GameObject("=== Distribution de l'histoire ===");
            MocapLibrary library = MocapLibraryBuilder.Build();

            List<StoryActor> actors = new List<StoryActor>();
            for (int i = 0; i < Cast.Length; i++)
            {
                actors.Add(BuildActor(root.transform, Cast[i], materials, night, library, i));
            }

            List<StoryCast.Place> places = new List<StoryCast.Place>();

            // --- le motel, le Vertigo, la fosse
            if (map.home != null && map.home.outside != null)
            {
                places.Add(new StoryCast.Place { id = "motel", label = "Devant le motel", position = MapPack.Position(map.home.outside), yaw = MapPack.Yaw(map.home.outside) });
            }

            if (map.vertigo != null && map.vertigo.@out != null)
            {
                places.Add(new StoryCast.Place { id = "vertigo", label = "Le Vertigo", position = MapPack.Position(map.vertigo.@out), yaw = MapPack.Yaw(map.vertigo.@out) });
            }

            if (club != null)
            {
                places.Add(new StoryCast.Place { id = "fosse", label = "La fosse du Vertigo", position = club.ChampionPosition, yaw = club.ChampionYaw });
                Vector3 back = club.Root.TransformPoint(ClubInteriorBuilder.RingCenter + new Vector3(-3.6f, 0f, 5.6f));
                places.Add(new StoryCast.Place { id = "rosa", label = "La salle du fond", position = back, yaw = 150f });
            }

            List<Vector3> walk = new List<Vector3>();
            for (int l = 0; l < walkLoops.Count; l++)
            {
                if (walkLoops[l] != null) walk.AddRange(walkLoops[l]);
            }

            // --- les coins de la ville
            AddSpotPlace(places, map, "taco", "Taco Ticklers", "Derrière le Taco Ticklers");
            AddSpotPlace(places, map, "pizzeria", "pizzeria", "Derrière la pizzeria");
            AddSpotPlace(places, map, "skatepark", "skatepark", "Au skatepark");
            AddSpotPlace(places, map, "casino_back", "casino", "Derrière le casino");
            AddSpotPlace(places, map, "tribunal", "tribunal", "Le parvis de la mairie");
            AddSpotPlace(places, map, "docks", "docks", "L'entrepôt des docks");
            AddSpotPlace(places, map, "chantier", "chantier", "L'usine désaffectée du chantier");
            AddSpotPlace(places, map, "budsbar", "Bud's Bar", "Le Bud's Bar");
            AddSpotPlace(places, map, "poste", "poste", "Le bureau de poste");
            AddSpotPlace(places, map, "medical_ext", "centre médical", "Le centre médical");
            AddSpotPlace(places, map, "pawn_ext", "prêteur", "Le prêteur sur gages");
            AddSpotPlace(places, map, "parking", "parking couvert", "Le parking couvert");

            // --- les boutiques : la salle de Ray, le cabinet de Lina, la laverie de maman…
            AddShopPlace(places, shops, walk, "salle", ShopKind.SalleDeBoxe, null);
            AddShopPlace(places, shops, walk, "cabinet", ShopKind.Medecin, null);
            AddShopPlace(places, shops, walk, "laverie", ShopKind.Laverie, null);
            AddShopPlace(places, shops, walk, "barbier", ShopKind.Barbier, null);
            AddShopPlace(places, shops, walk, "pawn", ShopKind.PreteurSurGages, null);
            AddShopPlace(places, shops, walk, "casino", ShopKind.Casino, null);
            AddShopPlace(places, shops, walk, "chen", ShopKind.Restaurant, "Dragon");
            AddShopPlace(places, shops, walk, "shred", ShopKind.Vetements, "Shred");
            AddShopPlace(places, shops, walk, "avocat", ShopKind.Avocat, null);

            // --- les repères de la carte : le commissariat, le manoir
            if (map.landmarks != null)
            {
                for (int i = 0; i < map.landmarks.Length; i++)
                {
                    MapPack.Mark m = map.landmarks[i];
                    if (m == null || m.p == null || m.p.Length < 2) continue;
                    if (m.label == "Manoir")
                    {
                        places.Add(new StoryCast.Place { id = "manoir", label = "Le manoir", position = new Vector3(m.p[0], 10.5f, m.p[1]), yaw = 150f });
                    }
                    else if (m.label == "Commissariat")
                    {
                        Vector3 at = NearestWalk(walk, new Vector3(m.p[0], 0f, m.p[1]));
                        places.Add(new StoryCast.Place { id = "commissariat", label = "Le commissariat", position = at, yaw = 0f });
                    }
                }
            }

            StoryCast cast = systems.AddComponent<StoryCast>();
            cast.Configure(actors.ToArray(), places.ToArray(), walk.ToArray());
            EditorUtility.SetDirty(cast);
            return cast;
        }

        private static void AddSpotPlace(List<StoryCast.Place> places, MapPack.Data map, string id, string contains, string label)
        {
            if (map.spots == null) return;
            for (int i = 0; i < map.spots.Length; i++)
            {
                MapPack.Spot s = map.spots[i];
                if (s == null || s.p == null || s.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) < 0) continue;
                places.Add(new StoryCast.Place { id = id, label = label, position = MapPack.Position(s.p) + Vector3.up * 0.05f, yaw = s.yaw });
                return;
            }
        }

        /// <summary>
        /// Un lieu dans une boutique : trois pas devant l'entrée d'un intérieur construit, ou
        /// côté clients du comptoir pour un intérieur de la carte. Pour un intérieur construit,
        /// un second lieu « id_ext » attend sur le trottoir, devant la porte de la ville.
        /// </summary>
        private static void AddShopPlace(List<StoryCast.Place> places, ShopDirectory shops, List<Vector3> walk, string id, ShopKind kind,
            string prefer)
        {
            if (shops == null) return;
            Shop found = null;
            IReadOnlyList<Shop> list = shops.Shops;
            for (int i = 0; i < list.Count; i++)
            {
                Shop s = list[i];
                if (s == null || s.Kind != kind) continue;
                bool preferred = prefer != null && s.DisplayName.IndexOf(prefer, System.StringComparison.OrdinalIgnoreCase) >= 0;
                if (prefer != null && !preferred) continue;
                if (found == null || (s.HasInterior && !found.HasInterior)) found = s;
                if (preferred) break;
            }

            if (found == null) return;

            if (found.HasInterior)
            {
                Transform arrival = found.Arrival;
                Vector3 at = arrival.position + arrival.forward * 3f + arrival.right * 1.4f;
                places.Add(new StoryCast.Place { id = id, label = found.DisplayName, position = at, yaw = arrival.eulerAngles.y + 200f });

                Vector3 front;
                if (ShopFronts.TryGetValue(found.DisplayName, out front))
                {
                    places.Add(new StoryCast.Place
                    {
                        id = id + "_ext", label = "Devant " + found.DisplayName, position = NearestWalk(walk, front), yaw = 0f
                    });
                }

                return;
            }

            if (found.Clerk == null) return;
            Transform clerk = found.Clerk.transform;
            Vector3 customer = clerk.position + clerk.forward * 1.9f + clerk.right * 1.3f;
            places.Add(new StoryCast.Place { id = id, label = found.DisplayName, position = customer, yaw = clerk.eulerAngles.y + 120f });
            places.Add(new StoryCast.Place { id = id + "_ext", label = "Devant " + found.DisplayName, position = NearestWalk(walk, clerk.position), yaw = 0f });
        }

        /// <summary>Le point de trottoir le plus proche (les boucles des passants).</summary>
        private static Vector3 NearestWalk(List<Vector3> walk, Vector3 near)
        {
            Vector3 best = near;
            float bestDistance = float.MaxValue;
            for (int i = 0; i < walk.Count; i++)
            {
                Vector3 d = walk[i] - near;
                float flat = d.x * d.x + d.z * d.z;
                if (flat >= bestDistance) continue;
                bestDistance = flat;
                best = walk[i];
            }

            return best;
        }

        private static StoryActor BuildActor(Transform parent, ActorLook look, BuildMaterials materials,
            NightMaterialFactory.Palette night, MocapLibrary library, int index)
        {
            GameObject go = EditorBuildUtility.CreateEmpty(look.Name, parent, new Vector3(index * 2f, 0f, -520f));
            CapsuleCollider capsule = go.AddComponent<CapsuleCollider>();
            capsule.height = 1.75f;
            capsule.radius = 0.28f;
            capsule.center = new Vector3(0f, 0.875f, 0f);

            FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);
            skin.Silhouette = look.Silhouette;
            skin.Top = look.Top;
            skin.Shirt = PrologueSceneBuilder.Jacket(night, "M_Role_Haut_" + look.Id, look.Shirt);
            skin.Pants = PrologueSceneBuilder.Jacket(night, "M_Role_Bas_" + look.Id, look.Pants);
            skin.Crowd = true;

            FighterBuilder.Result body = FighterBuilder.BuildBody(go.transform, go.transform, skin, true, Faction.Neutral, go);
            Hitbox[] hitboxes = go.GetComponentsInChildren<Hitbox>(true);
            for (int h = 0; h < hitboxes.Length; h++) Object.DestroyImmediate(hitboxes[h]);

            Spectator idle = go.AddComponent<Spectator>();
            SerializedWiring.SetObject(idle, "_rig", body.Rig);
            SerializedWiring.SetObject(idle, "_locomotion", body.Locomotion);
            SerializedWiring.SetEnum(idle, "_mood", (int)Spectator.Mood.Accoude);
            SerializedWiring.SetFloat(idle, "_temperament", 0.25f);
            SerializedWiring.SetFloat(idle, "_seed", 3.7f + index * 1.9f);

            MocapWalker walker = null;
            if (look.Walks)
            {
                Rigidbody rb = go.AddComponent<Rigidbody>();
                rb.isKinematic = true;
                walker = go.AddComponent<MocapWalker>();
                SerializedWiring.SetObject(walker, "_library", library);
                SerializedWiring.SetObject(walker, "_rig", body.Rig);
                SerializedWiring.SetObject(walker, "_locomotion", body.Locomotion);
                walker.enabled = false;
            }

            Interactable talk = go.AddComponent<Interactable>();
            SerializedWiring.SetString(talk, "_label", "Parler");
            SerializedWiring.SetString(talk, "_hint", look.Name);
            SerializedWiring.SetFloat(talk, "_range", 2.8f);
            Transform chest = EditorBuildUtility.CreateEmpty("Visee (torse)", go.transform, new Vector3(0f, 1.35f, 0f)).transform;
            SerializedWiring.SetObject(talk, "_focus", chest);

            StoryActor actor = go.AddComponent<StoryActor>();
            actor.Configure(look.Id, look.Name, idle, walker);
            EditorUtility.SetDirty(actor);
            go.SetActive(false);
            return actor;
        }
    }
}
