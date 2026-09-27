using System.Collections.Generic;
using UberBagarre.Story;
using UberBagarre.World;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les voitures du monde ouvert, toutes tirées des véhicules de la ville (voir
    /// <see cref="CityVehicles"/>) : la tienne, celles garées, la circulation.
    /// </summary>
    public static partial class OpenWorldSceneBuilder
    {
        // La circulation : surtout des berlines, des SUV, des pick-up et des compactes ; un coupé
        // de temps en temps.
        private static readonly string[] TrafficMix = { "Sedan", "SUV", "Shitbox", "Pickup", "Sedan", "Coupe", "SUV", "Shitbox", "Pickup", "Sedan" };
        private static readonly string[] ParkedMix = { "Sedan", "Pickup", "Shitbox", "SUV", "Sedan", "Shitbox" };

        private static GameObject BuildCityTraffic(CityVehicles vehicles, List<Vector3[]> loops, int[] perLoop)
        {
            GameObject root = new GameObject("=== Circulation ===");
            if (!vehicles.Available) return root;

            int n = 0;
            for (int l = 0; l < loops.Count; l++)
            {
                Vector3[] loop = loops[l];
                if (loop == null || loop.Length < 3) continue;
                int count = l < perLoop.Length ? perLoop[l] : 2;

                for (int k = 0; k < count; k++, n++)
                {
                    int start = (k * loop.Length / count + l * 2) % loop.Length;
                    float cruise = 9.5f + (n % 4) * 0.9f;
                    vehicles.SpawnTraffic(TrafficMix[n % TrafficMix.Length], root.transform, loop, start, cruise, n);
                }
            }

            return root;
        }

        /// <summary>
        /// Ta caisse (la compacte devant le motel), les véhicules que la ville avait garés — les
        /// mêmes, à la même place, mais on peut les prendre — et quelques autres sur les places
        /// libres. Rend le catalogue (les modèles qu'on peut acheter, la voiture du joueur).
        /// </summary>
        private static VehicleCatalog BuildParkedCars(CityVehicles vehicles, MapPack.Data map, GameObject parked, PlayerProgress progress)
        {
            VehicleCatalog catalog = parked.AddComponent<VehicleCatalog>();
            if (!vehicles.Available) return catalog;

            DrivableCar personal = null;
            Transform spot = null;
            List<Vector3> taken = new List<Vector3>();
            int mineIndex = -1;

            if (map.car != null && map.car.Length >= 3)
            {
                Vector3 p = MapPack.Position(map.car);
                float yaw = MapPack.Yaw(map.car);

                // La place devant le motel est déjà occupée par la compacte de la carte : ta
                // voiture PREND sa place (au lieu de s'emboîter dans une copie à 3 m de là).
                float best = 7f;
                for (int i = 0; i < vehicles.CityParked.Count; i++)
                {
                    float d = Vector3.Distance(vehicles.CityParked[i].position, p);
                    if (d >= best) continue;
                    best = d;
                    mineIndex = i;
                }

                if (mineIndex >= 0)
                {
                    p = vehicles.CityParked[mineIndex].position;
                    yaw = vehicles.CityParked[mineIndex].yaw;
                }

                spot = EditorBuildUtility.CreateEmpty("Place de ta voiture", parked.transform, p).transform;
                spot.rotation = Quaternion.Euler(0f, yaw, 0f);

                GameObject mine = vehicles.Spawn("Shitbox", parked.transform, p, yaw, "Ta caisse");
                if (mine != null) personal = mine.GetComponent<DrivableCar>();
                if (personal != null) SerializedWiring.SetEnum(personal, "_access", (int)DrivableCar.Access.Perso);
                taken.Add(p);
            }

            for (int i = 0; i < vehicles.CityParked.Count; i++)
            {
                if (i == mineIndex) continue;
                CityVehicles.Parked c = vehicles.CityParked[i];
                if (Near(taken, c.position, 6.5f)) continue;
                Parked(vehicles.Spawn(c.model, parked.transform, c.position, c.yaw, "Voiture"), i);
                taken.Add(c.position);
            }

            if (map.cars != null)
            {
                for (int i = 0; i < map.cars.Length; i++)
                {
                    if (map.cars[i] == null || map.cars[i].v == null || map.cars[i].v.Length < 3) continue;
                    Vector3 p = MapPack.Position(map.cars[i].v);
                    if (Near(taken, p, 6.5f)) continue;
                    Parked(vehicles.Spawn(ParkedMix[i % ParkedMix.Length], parked.transform, p, MapPack.Yaw(map.cars[i].v), "Voiture"), i + 31);
                    taken.Add(p);
                }
            }

            List<VehicleCatalog.Entry> entries = new List<VehicleCatalog.Entry>();
            for (int i = 0; i < vehicles.Models.Count; i++)
            {
                CityVehicles.Model m = vehicles.Models[i];
                entries.Add(new VehicleCatalog.Entry { key = m.key, label = m.label, price = m.price, template = m.template });
            }

            catalog.Configure(entries.ToArray(), progress, spot, personal, "Shitbox");
            EditorUtility.SetDirty(catalog);
            return catalog;
        }

        /// <summary>
        /// Une voiture garée par la ville : elle n'est pas à nous. Presque toujours fermée à clé
        /// (il faut la crocheter) ; une sur sept a été oubliée ouverte.
        /// </summary>
        private static void Parked(GameObject car, int index)
        {
            DrivableCar drivable = car != null ? car.GetComponent<DrivableCar>() : null;
            if (drivable == null) return;
            SerializedWiring.SetEnum(drivable, "_access", (int)DrivableCar.Access.Garee);
            SerializedWiring.SetBool(drivable, "_locked", index % 7 != 3);
        }

        private static bool Near(List<Vector3> points, Vector3 p, float distance)
        {
            for (int i = 0; i < points.Count; i++)
            {
                if ((points[i] - p).sqrMagnitude < distance * distance) return true;
            }

            return false;
        }
    }
}
