using System.Collections.Generic;
using UberBagarre.Phone;
using UberBagarre.Story;
using UberBagarre.UI;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// L'histoire posée sur la ville : ses personnages (chacun avec son allure, celle que la
    /// fiche de l'appli décrit), l'endroit où chacun passe sa soirée, et l'écran titre qui
    /// survole la ville.
    /// </summary>
    public static partial class OpenWorldSceneBuilder
    {
        private sealed class StoryRole
        {
            public string Tag;
            public Target Look;
        }

        private static readonly StoryRole[] StoryRoles =
        {
            new StoryRole { Tag = "moretti", Look = new Target { Name = "BRUNO MORETTI", Age = "45 ans", Clothing = "Blouson noir, crâne rasé",
                Stars = 1, Silhouette = "Costaud", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.04f, 0.04f, 0.05f),
                Pants = new Color(0.10f, 0.10f, 0.11f), Health = 95f,
                Record = "Videur du Vertigo depuis douze ans.\nSort fumer à la fin de son service, toujours seul." } },
            new StoryRole { Tag = "dragan", Look = new Target { Name = "DRAGAN KOVAC", Age = "34 ans", Clothing = "Veste en cuir marron",
                Stars = 2, Silhouette = "Costaud", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.26f, 0.14f, 0.07f),
                Pants = new Color(0.07f, 0.07f, 0.08f), Health = 125f,
                Record = "L'aîné des Kovac. Décharge des camionnettes\nqu'il vaut mieux ne pas ouvrir." } },
            new StoryRole { Tag = "milan", Look = new Target { Name = "MILAN KOVAC", Age = "29 ans", Clothing = "T-shirt blanc, jean noir",
                Stars = 2, Silhouette = "Sec", Top = CorpsImporter.Top.TShirt, Shirt = new Color(0.82f, 0.82f, 0.80f),
                Pants = new Color(0.05f, 0.05f, 0.06f), Health = 115f,
                Record = "Le cadet. Plus rapide que son frère,\net beaucoup plus rancunier." } },
            new StoryRole { Tag = "taureau", Look = new Target { Name = "LE TAUREAU", Age = "31 ans", Clothing = "Débardeur rouge",
                Stars = 3, Silhouette = "Colosse", Top = CorpsImporter.Top.Debardeur, Shirt = new Color(0.62f, 0.05f, 0.05f),
                Pants = new Color(0.06f, 0.06f, 0.07f), Health = 210f,
                Record = "Champion de la fosse du Vertigo.\nOnze combats, onze K.O." } },
            new StoryRole { Tag = "comptable", Look = new Target { Name = "VICTOR SARKIS", Age = "52 ans", Clothing = "Veste grise, chemise ouverte",
                Stars = 4, Silhouette = "Costaud", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.34f, 0.35f, 0.37f),
                Pants = new Color(0.15f, 0.15f, 0.17f), Health = 190f,
                Record = "« Le Comptable ». Tient les paris clandestins\nde la ville. Ne se salit jamais les mains." } },
            new StoryRole { Tag = "sami", Look = new Target { Name = "SAMI", Age = "30 ans", Clothing = "Veste kaki",
                Stars = 5, Silhouette = "Athlete", Top = CorpsImporter.Top.Veste, Shirt = new Color(0.24f, 0.27f, 0.15f),
                Pants = new Color(0.10f, 0.15f, 0.30f), Health = 240f,
                Record = "Ton ami. Celui qui t'a installé l'appli.\nCelui qui a parié contre toi." } }
        };

        // ------------------------------------------------------------------ personnages

        private static OpenWorldStory.Character[] BuildStoryCharacters(MapPack.Data map, BuildMaterials materials,
            AttackLibraryBuilder.Library attacks, NightMaterialFactory.Palette night, ClubInteriorBuilder.Result club)
        {
            GameObject root = new GameObject("=== Personnages de l'histoire ===");
            root.transform.position = new Vector3(0f, 0f, -460f);

            List<OpenWorldStory.Character> characters = new List<OpenWorldStory.Character>();
            for (int i = 0; i < StoryRoles.Length; i++)
            {
                Target t = StoryRoles[i].Look;

                FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);
                skin.Silhouette = t.Silhouette;
                skin.Top = t.Top;
                skin.Shirt = PrologueSceneBuilder.Jacket(night, "M_Histoire_Haut_" + StoryRoles[i].Tag, t.Shirt);
                skin.Pants = PrologueSceneBuilder.Jacket(night, "M_Histoire_Bas_" + StoryRoles[i].Tag, t.Pants);

                SandboxSceneBuilder.FighterParts parts = SandboxSceneBuilder.BuildFighter(materials, attacks, t.Name,
                    root.transform.position + new Vector3(i * 3f, 0f, 0f), 0f, skin, t.Health, true);
                parts.Go.transform.SetParent(root.transform, true);
                if (parts.Brain != null) parts.Brain.enabled = false;
                parts.Go.SetActive(false);

                characters.Add(new OpenWorldStory.Character
                {
                    tag = StoryRoles[i].Tag,
                    profile = new OpenWorldDirector.Profile
                    {
                        name = t.Name, age = t.Age, clothing = t.Clothing, record = t.Record, stars = t.Stars, template = parts.Go
                    },
                    spot = StorySpot(StoryRoles[i].Tag, map, club)
                });
            }

            return characters.ToArray();
        }

        /// <summary>Où chacun passe sa soirée quand la commande tombe.</summary>
        private static OpenWorldDirector.Spot StorySpot(string tag, MapPack.Data map, ClubInteriorBuilder.Result club)
        {
            Vector3 vertigoOut = map.vertigo != null && map.vertigo.@out != null ? MapPack.Position(map.vertigo.@out) : Vector3.zero;
            float vertigoYaw = map.vertigo != null && map.vertigo.@out != null ? MapPack.Yaw(map.vertigo.@out) : 0f;

            switch (tag)
            {
                case "moretti":
                    // Devant le club, trois pas à côté de la porte, dos au mur : il fume.
                    return new OpenWorldDirector.Spot
                    {
                        name = "Devant le Vertigo", position = vertigoOut + new Vector3(0f, 0.05f, 3.2f), yaw = vertigoYaw, activity = "fume"
                    };

                case "dragan":
                {
                    Vector3 parking = NearestParking(map, vertigoOut, 10f);
                    return new OpenWorldDirector.Spot
                    {
                        name = "Parking près du Vertigo", position = parking + Vector3.up * 0.05f, yaw = vertigoYaw + 180f, activity = "deal"
                    };
                }

                case "milan":
                    return MapSpot(map, "Bud's Bar", "Devant le Bud's Bar", "telephone");

                case "taureau":
                    return new OpenWorldDirector.Spot
                    {
                        name = "La fosse du Vertigo", position = club.ChampionPosition, yaw = club.ChampionYaw, activity = "sentraine"
                    };

                case "comptable":
                    return MapSpot(map, "casino", "Derrière le casino", "telephone");

                default:
                    return MapSpot(map, "docks", "Entrepôt en briques, aux docks", "fume");
            }
        }

        private static OpenWorldDirector.Spot MapSpot(MapPack.Data map, string contains, string label, string activity)
        {
            for (int i = 0; i < map.spots.Length; i++)
            {
                MapPack.Spot s = map.spots[i];
                if (s == null || s.p == null || s.name.IndexOf(contains, System.StringComparison.OrdinalIgnoreCase) < 0) continue;

                return new OpenWorldDirector.Spot
                {
                    name = label, position = MapPack.Position(s.p) + Vector3.up * 0.05f, yaw = s.yaw, activity = activity
                };
            }

            MapPack.Spot first = map.spots[0];
            return new OpenWorldDirector.Spot
            {
                name = label, position = MapPack.Position(first.p) + Vector3.up * 0.05f, yaw = first.yaw, activity = activity
            };
        }

        private static Vector3 NearestParking(MapPack.Data map, Vector3 from, float minimum)
        {
            Vector3 best = from + new Vector3(8f, 0f, 0f);
            float bestDistance = float.MaxValue;

            if (map.parking == null) return best;
            for (int i = 0; i < map.parking.Length; i++)
            {
                if (map.parking[i] == null || map.parking[i].v == null || map.parking[i].v.Length < 3) continue;
                Vector3 p = MapPack.Position(map.parking[i].v);
                float d = Vector3.Distance(new Vector3(p.x, 0f, p.z), new Vector3(from.x, 0f, from.z));
                if (d < minimum || d >= bestDistance || Occupied(map, p)) continue;
                bestDistance = d;
                best = p;
            }

            return best;
        }

        /// <summary>Une voiture est-elle garée là (on ne fait pas apparaître quelqu'un dedans) ?</summary>
        private static bool Occupied(MapPack.Data map, Vector3 p)
        {
            if (map.car != null && map.car.Length >= 3 && Vector3.Distance(MapPack.Position(map.car), p) < 3.5f) return true;
            if (map.cars == null) return false;

            for (int i = 0; i < map.cars.Length; i++)
            {
                if (map.cars[i] == null || map.cars[i].v == null || map.cars[i].v.Length < 3) continue;
                if (Vector3.Distance(MapPack.Position(map.cars[i].v), p) < 3.5f) return true;
            }

            return false;
        }

        private static OpenWorldStory BuildStory(GameObject systems, OpenWorldStory.Character[] characters, MapPack.Data map,
            ClubInteriorBuilder.Result club, OpenWorldDirector director, PlayerProgress progress, SubtitleDisplay subtitles,
            ScreenFader fader, PhoneDevice phone, CityMap cityMap, HomeRegistry homes, ComputerScreen computer)
        {
            ObjectiveDisplay objectives = systems.AddComponent<ObjectiveDisplay>();
            // Sous le panneau de la course (en haut à gauche) : les deux ne se chevauchent pas.
            SerializedWiring.SetVector2(objectives, "_margin", new Vector2(26f, 150f));

            OpenWorldStory story = systems.AddComponent<OpenWorldStory>();
            SerializedWiring.SetObject(story, "_director", director);
            SerializedWiring.SetObject(story, "_progress", progress);
            SerializedWiring.SetObject(story, "_subtitles", subtitles);
            SerializedWiring.SetObject(story, "_objectives", objectives);
            SerializedWiring.SetObject(story, "_fader", fader);
            SerializedWiring.SetObject(story, "_phone", phone);
            SerializedWiring.SetObject(story, "_map", cityMap);
            SerializedWiring.SetObject(story, "_homes", homes);
            SerializedWiring.SetObject(story, "_computer", computer);

            Vector3 door = map.vertigo != null && map.vertigo.door != null ? MapPack.Position(map.vertigo.door) : Vector3.zero;
            // Le courrier, ouvert et lu à l'écran (la version du motel).
            LetterReader letters = systems.AddComponent<LetterReader>();
            SerializedWiring.SetObject(letters, "_input", Object.FindAnyObjectByType<Player.PlayerInputReader>());
            letters.Configure("Monsieur\nMotel Hyland\nChambre 3", true);
            EditorUtility.SetDirty(letters);

            story.Configure(characters, door, club.Root.gameObject, club.RingGate, false, letters);
            EditorUtility.SetDirty(story);
            return story;
        }

        // ------------------------------------------------------------------ écran titre

        /// <summary>
        /// Le survol de la ville pour l'écran titre : cinq plans, chacun un départ, une arrivée
        /// et un point visé. Le long d'une avenue (sur la chaussée : aucun immeuble sur le
        /// chemin), l'enseigne du Vertigo, le motel, le port vu d'en haut, le manoir.
        /// </summary>
        private static Transform[] BuildTitleTour(MapPack.Data map, List<Vector3[]> driveLoops, Transform parent)
        {
            GameObject root = EditorBuildUtility.CreateEmpty("Ecran titre (survol)", parent, Vector3.zero);
            List<Transform> tour = new List<Transform>();

            // 1. l'avenue, à hauteur de réverbère
            if (driveLoops.Count > 0 && driveLoops[0].Length > 120)
            {
                Vector3[] loop = driveLoops[0];
                int start = loop.Length / 3;
                AddShot(root.transform, tour, "Avenue", loop[start] + Vector3.up * 7f, loop[start + 45] + Vector3.up * 5.5f,
                    loop[start + 75] + Vector3.up * 2f);
            }

            // 2. l'enseigne du Vertigo
            if (map.vertigo != null && map.vertigo.sign != null && map.vertigo.@out != null)
            {
                Vector3 sign = MapPack.Position(map.vertigo.sign);
                Vector3 street = MapPack.Position(map.vertigo.@out);
                Vector3 away = new Vector3(street.x - sign.x, 0f, street.z - sign.z).normalized;
                Vector3 along = Vector3.Cross(Vector3.up, away);
                AddShot(root.transform, tour, "Vertigo", sign + away * 15f - along * 9f + Vector3.up * 0.5f,
                    sign + away * 11f + along * 7f - Vector3.up * 1.2f, sign - Vector3.up * 0.8f);
            }

            // 3. le motel, la chambre 3
            if (map.home != null && map.home.outside != null)
            {
                Vector3 motel = MapPack.Position(map.home.outside);
                AddShot(root.transform, tour, "Motel", motel + new Vector3(16f, 5f, -14f), motel + new Vector3(14f, 3.5f, 10f),
                    motel + new Vector3(-4f, 1.8f, 2f));
            }

            // 4. la ville vue du port, en hauteur
            Vector3 docks = new Vector3(-80f, 0f, -50f);
            Vector3 downtown = new Vector3(10f, 0f, 80f);
            AddShot(root.transform, tour, "Port", docks + new Vector3(-50f, 48f, -30f), docks + new Vector3(10f, 44f, -45f), downtown);

            // 5. le manoir sur sa colline
            Vector3 manor = new Vector3(163.5f, 12f, -57f);
            AddShot(root.transform, tour, "Manoir", manor + new Vector3(-22f, 12f, -40f), manor + new Vector3(18f, 9f, -38f), manor);

            return tour.ToArray();
        }

        private static void AddShot(Transform parent, List<Transform> tour, string name, Vector3 from, Vector3 to, Vector3 target)
        {
            tour.Add(EditorBuildUtility.CreateEmpty(name + " (depart)", parent, from).transform);
            tour.Add(EditorBuildUtility.CreateEmpty(name + " (arrivee)", parent, to).transform);
            tour.Add(EditorBuildUtility.CreateEmpty(name + " (visee)", parent, target).transform);
        }

        private static void WireTitle(GameMenu menu, OpenWorldStory story, Transform[] tour, ScreenFader fader)
        {
            SerializedWiring.SetObject(menu, "_openWorld", story);
            SerializedWiring.SetObject(menu, "_fader", fader);
            SerializedWiring.SetBool(menu, "_titleOnStart", true);
            SerializedWiring.SetFloat(menu, "_shotFov", 50f);
            SandboxSceneBuilder.SetComponentArray(menu, "_tour", tour);
            EditorUtility.SetDirty(menu);
        }
    }
}
