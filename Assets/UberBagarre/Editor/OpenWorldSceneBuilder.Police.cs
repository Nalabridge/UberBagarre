using System.Collections.Generic;
using UberBagarre.Combat;
using UberBagarre.Player;
using UberBagarre.Story;
using UberBagarre.UI;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La police du monde ouvert (voir <see cref="PoliceSystem"/>) : les agents en uniforme, la
    /// brigade de Brandt, la voiture de patrouille et son gyrophare, les points d'apparition,
    /// la sortie du commissariat.
    /// </summary>
    public static partial class OpenWorldSceneBuilder
    {
        private static PoliceSystem BuildPolice(GameObject systems, GameObject player, BuildMaterials materials,
            AttackLibraryBuilder.Library attacks, NightMaterialFactory.Palette night, CityVehicles vehicles, MapPack.Data map,
            List<Vector3[]> walkLoops, List<Vector3[]> driveLoops, PlayerProgress progress, SubtitleDisplay subtitles,
            ScreenFader fader, OpenWorldDirector director)
        {
            GameObject root = new GameObject("=== Police (modeles) ===");
            root.transform.position = new Vector3(0f, 0f, -500f);

            // --- les agents : chemise bleu marine, pantalon sombre
            Material shirt = PrologueSceneBuilder.Jacket(night, "M_Police_Chemise", new Color(0.12f, 0.16f, 0.32f));
            Material pants = PrologueSceneBuilder.Jacket(night, "M_Police_Pantalon", new Color(0.07f, 0.08f, 0.13f));
            string[] silhouettes = { "Athlete", "Costaud" };
            List<GameObject> officers = new List<GameObject>();
            for (int i = 0; i < silhouettes.Length; i++)
            {
                officers.Add(PoliceTemplate(root.transform, materials, attacks, "Agent", silhouettes[i], CorpsImporter.Top.TShirt,
                    shirt, pants, 120f, i));
            }

            // --- la brigade de Brandt : des colosses en blouson noir
            Material jacket = PrologueSceneBuilder.Jacket(night, "M_Brigade_Blouson", new Color(0.05f, 0.05f, 0.06f));
            GameObject brandt = PoliceTemplate(root.transform, materials, attacks, "Brigade", "Colosse", CorpsImporter.Top.Veste,
                jacket, pants, 220f, 3);

            // --- la voiture de patrouille : une berline de la ville, un gyrophare sur le toit
            GameObject car = PoliceCarTemplate(root.transform, vehicles, night);

            // --- où ils apparaissent, où l'on sort du commissariat
            List<Vector3> foot = new List<Vector3>();
            for (int l = 0; l < walkLoops.Count; l++)
            {
                if (walkLoops[l] == null) continue;
                for (int i = 0; i < walkLoops[l].Length; i += 2) foot.Add(walkLoops[l][i]);
            }

            List<Vector3> road = new List<Vector3>();
            for (int l = 0; l < driveLoops.Count; l++)
            {
                if (driveLoops[l] == null) continue;
                for (int i = 0; i < driveLoops[l].Length; i++) road.Add(driveLoops[l][i]);
            }

            Transform release = StationExit(map, foot, systems.transform);

            PoliceSystem police = systems.AddComponent<PoliceSystem>();
            police.Configure(player.GetComponent<Combatant>(), player.GetComponent<PlayerInputReader>(), progress, subtitles, fader,
                director, officers.ToArray(), brandt, car, foot.ToArray(), road.ToArray(), release);
            EditorUtility.SetDirty(police);
            return police;
        }

        private static GameObject PoliceTemplate(Transform parent, BuildMaterials materials, AttackLibraryBuilder.Library attacks,
            string name, string silhouette, CorpsImporter.Top top, Material shirt, Material pants, float health, int index)
        {
            FighterBuilder.Skin skin = FighterBuilder.Skin.Enemy(materials);
            skin.Silhouette = silhouette;
            skin.Top = top;
            skin.Shirt = shirt;
            skin.Pants = pants;

            SandboxSceneBuilder.FighterParts parts = SandboxSceneBuilder.BuildFighter(materials, attacks, name,
                parent.position + new Vector3(index * 3f, 0f, 0f), 0f, skin, health, true);
            parts.Go.transform.SetParent(parent, true);
            if (parts.Brain != null) parts.Brain.enabled = false;
            parts.Go.AddComponent<PoliceOfficer>();
            if (parts.Go.GetComponent<TargetInjuries>() == null) parts.Go.AddComponent<TargetInjuries>();
            parts.Go.SetActive(false);
            return parts.Go;
        }

        /// <summary>Une berline de la ville avec, sur le toit, une rampe rouge et bleue et ses deux lampes.</summary>
        private static GameObject PoliceCarTemplate(Transform parent, CityVehicles vehicles, NightMaterialFactory.Palette night)
        {
            if (vehicles == null || !vehicles.Available) return null;

            GameObject car = vehicles.Spawn("Sedan", parent, parent.position + new Vector3(0f, 0f, 12f), 0f, "Police");
            if (car == null) car = vehicles.Spawn("SUV", parent, parent.position + new Vector3(0f, 0f, 12f), 0f, "Police");
            if (car == null) return null;
            car.name = "Voiture de police (modele)";
            SerializedWiring.SetEnum(car.GetComponent<DrivableCar>(), "_access", (int)DrivableCar.Access.Circulation);

            bool any;
            Bounds bounds = CityRules.RendererBounds(car.transform, out any);
            float roof = any ? bounds.max.y - car.transform.position.y : 1.5f;

            GameObject bar = EditorBuildUtility.CreateEmpty("Gyrophare", car.transform, new Vector3(0f, roof + 0.06f, -0.1f));
            Box(bar.transform, "Socle", Vector3.zero, new Vector3(1.1f, 0.08f, 0.26f), night.DarkMetal, false);
            GameObject red = Box(bar.transform, "Feu rouge", new Vector3(-0.3f, 0.1f, 0f), new Vector3(0.46f, 0.12f, 0.22f), night.NeonRed, false);
            GameObject blue = Box(bar.transform, "Feu bleu", new Vector3(0.3f, 0.1f, 0f), new Vector3(0.46f, 0.12f, 0.22f), night.NeonBlue, false);
            Light redLight = NightStreetBuilder.AddLight(bar.transform, "Lueur rouge", new Vector3(-0.4f, 0.3f, 0f), new Color(1f, 0.12f, 0.1f), 0f, 13f, true, false);
            Light blueLight = NightStreetBuilder.AddLight(bar.transform, "Lueur bleue", new Vector3(0.4f, 0.3f, 0f), new Color(0.2f, 0.35f, 1f), 0f, 13f, true, false);

            PoliceCar police = car.AddComponent<PoliceCar>();
            SerializedWiring.SetObject(police, "_red", redLight);
            SerializedWiring.SetObject(police, "_blue", blueLight);
            SerializedWiring.SetObject(police, "_redLens", red.GetComponent<Renderer>());
            SerializedWiring.SetObject(police, "_blueLens", blue.GetComponent<Renderer>());
            EditorUtility.SetDirty(police);

            car.SetActive(false);
            return car;
        }

        /// <summary>La sortie du commissariat : le point de trottoir le plus proche du repère de la carte.</summary>
        private static Transform StationExit(MapPack.Data map, List<Vector3> foot, Transform parent)
        {
            Vector2 station = new Vector2(15f, 36f);
            if (map.landmarks != null)
            {
                for (int i = 0; i < map.landmarks.Length; i++)
                {
                    MapPack.Mark m = map.landmarks[i];
                    if (m != null && m.p != null && m.p.Length >= 2 && m.label == "Commissariat") station = new Vector2(m.p[0], m.p[1]);
                }
            }

            Vector3 best = new Vector3(station.x, 0f, station.y);
            float bestDistance = float.MaxValue;
            for (int i = 0; i < foot.Count; i++)
            {
                float d = (new Vector2(foot[i].x, foot[i].z) - station).sqrMagnitude;
                if (d >= bestDistance) continue;
                bestDistance = d;
                best = foot[i];
            }

            GameObject exit = EditorBuildUtility.CreateEmpty("Sortie du commissariat", parent, best + Vector3.up * 0.1f);
            Vector3 look = new Vector3(station.x, best.y, station.y) - best;
            if (look.sqrMagnitude > 0.1f) exit.transform.rotation = Quaternion.LookRotation(-look.normalized, Vector3.up);
            return exit.transform;
        }
    }
}
