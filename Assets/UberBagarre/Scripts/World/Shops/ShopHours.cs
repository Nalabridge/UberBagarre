using UnityEngine;

namespace UberBagarre.World
{
    /// <summary>
    /// Les horaires des commerces. Une ville vivante a des rideaux qui se baissent : la
    /// pharmacie ferme à 21 h, le marché noir n'ouvre qu'à la nuit tombée, le barbier est fermé
    /// le lundi, la station-service et la laverie ne ferment jamais.
    ///
    /// Les heures sont celles de l'horloge du monde (<see cref="WorldClock"/>), le jour de la
    /// semaine se déduit du jour de la partie (le premier jour est un lundi). Une fermeture
    /// avant l'ouverture (« 16 h – 2 h ») passe minuit.
    /// </summary>
    public static class ShopHours
    {
        public struct Hours
        {
            /// <summary>Heure d'ouverture (0 à 24).</summary>
            public float Open;

            /// <summary>Heure de fermeture ; plus petite que l'ouverture : le lendemain.</summary>
            public float Close;

            /// <summary>Ouvert jour et nuit.</summary>
            public bool Always;

            /// <summary>Jour de fermeture (0 lundi … 6 dimanche), -1 : aucun.</summary>
            public int ClosedDay;

            public Hours(float open, float close, int closedDay = -1)
            {
                Open = open;
                Close = close;
                Always = false;
                ClosedDay = closedDay;
            }

            public static Hours AllDay
            {
                get { return new Hours { Always = true, ClosedDay = -1 }; }
            }
        }

        private const int Monday = 0;
        private const int Sunday = 6;

        private static readonly string[] DayNames = { "lundi", "mardi", "mercredi", "jeudi", "vendredi", "samedi", "dimanche" };

        /// <summary>Les horaires d'un commerce, d'après ce qu'il est (et son nom, pour les cas à part).</summary>
        public static Hours For(ShopKind kind, string name)
        {
            string n = string.IsNullOrEmpty(name) ? string.Empty : name.ToLowerInvariant();
            if (n.Contains("station")) return Hours.AllDay;
            if (n.Contains("taco")) return new Hours(11f, 3f);
            if (n.Contains("diner")) return new Hours(6f, 24f);
            if (n.Contains("dragon")) return new Hours(11.5f, 22.5f);
            if (n.Contains("pizzeria")) return new Hours(11f, 23.5f);
            if (n.Contains("zinc")) return new Hours(10f, 1f);

            switch (kind)
            {
                case ShopKind.Epicerie: return new Hours(7f, 23f);
                case ShopKind.Cave: return new Hours(10f, 22f);
                case ShopKind.Restaurant: return new Hours(11f, 23f);
                case ShopKind.Cafe: return new Hours(6.5f, 19f);
                case ShopKind.Bar: return new Hours(16f, 2f);
                case ShopKind.Pharmacie: return new Hours(8f, 21f);
                case ShopKind.Medecin: return new Hours(8f, 20f, Sunday);
                case ShopKind.Vetements: return new Hours(10f, 20f, Sunday);
                case ShopKind.Barbier: return new Hours(9f, 20f, Monday);
                case ShopKind.Tatoueur: return new Hours(12f, 22f, Monday);
                case ShopKind.Quincaillerie: return new Hours(8f, 19f, Sunday);
                case ShopKind.MarcheNoir: return new Hours(20f, 4f);
                case ShopKind.Casino: return new Hours(12f, 6f);
                case ShopKind.Arcade: return new Hours(11f, 24f);
                case ShopKind.StandDeTir: return new Hours(10f, 21f);
                case ShopKind.SalleDeBoxe: return new Hours(7f, 23f);
                case ShopKind.Poste: return new Hours(9f, 18f, Sunday);
                case ShopKind.Immobilier: return new Hours(9f, 19f, Sunday);
                case ShopKind.Concession: return new Hours(9f, 20f, Sunday);
                case ShopKind.Avocat: return new Hours(9f, 19f, Sunday);
                case ShopKind.PreteurSurGages: return new Hours(10f, 20f);

                // Le bar du Vertigo sert tant que le club tourne (l'histoire y passe à toute heure),
                // la laverie a ses machines en libre-service, le motel et la police ne ferment pas.
                default: return Hours.AllDay;
            }
        }

        /// <summary>Le jour de la semaine (0 lundi … 6 dimanche) du jour <paramref name="day"/> de la partie.</summary>
        public static int Weekday(int day)
        {
            return ((Mathf.Max(1, day) - 1) % 7 + 7) % 7;
        }

        public static string DayName(int weekday)
        {
            return DayNames[((weekday % 7) + 7) % 7];
        }

        public static bool IsOpen(Hours h, float hour, int weekday)
        {
            if (h.Always) return true;

            // Après minuit, on est encore dans la soirée de la veille.
            bool overnight = h.Close <= h.Open;
            bool late = overnight && hour < h.Close;
            int businessDay = late ? (weekday + 6) % 7 : weekday;
            if (businessDay == h.ClosedDay) return false;

            if (!overnight) return hour >= h.Open && hour < h.Close;
            return hour >= h.Open || hour < h.Close;
        }

        /// <summary>« 8 h », « 22 h 30 », « minuit ».</summary>
        public static string Clock(float hour)
        {
            hour = Mathf.Repeat(hour, 24f);
            int minutes = Mathf.RoundToInt(hour * 60f) % (24 * 60);
            int h = minutes / 60;
            int m = minutes % 60;
            if (h == 0 && m == 0) return "minuit";
            return m == 0 ? h + " h" : h + " h " + m.ToString("00");
        }

        /// <summary>Ce qu'on lit sur la porte : « Ouvert · ferme à 22 h », « Fermé · ouvre à 8 h », « Fermé le lundi ».</summary>
        public static string Describe(Hours h, float hour, int weekday, out bool open)
        {
            open = IsOpen(h, hour, weekday);
            if (h.Always) return "Ouvert 24 h/24";

            if (open)
            {
                float left = Mathf.Repeat(h.Close - hour, 24f);
                return left <= 0.75f ? "Ferme bientôt (" + Clock(h.Close) + ")" : "Ouvert · ferme à " + Clock(h.Close);
            }

            // Fermé aujourd'hui toute la journée, ou en attendant l'ouverture.
            bool overnight = h.Close <= h.Open;
            bool beforeOpen = hour < h.Open && !(overnight && hour < h.Close);
            int opensOn = beforeOpen ? weekday : (weekday + 1) % 7;
            if (opensOn == h.ClosedDay) opensOn = (opensOn + 1) % 7;

            if (weekday == h.ClosedDay && (beforeOpen || hour >= h.Open)) return "Fermé le " + DayName(h.ClosedDay);
            if (opensOn == weekday) return "Fermé · ouvre à " + Clock(h.Open);
            if (opensOn == (weekday + 1) % 7) return "Fermé · ouvre demain à " + Clock(h.Open);
            return "Fermé · ouvre " + DayName(opensOn) + " à " + Clock(h.Open);
        }

        /// <summary>La plage d'ouverture en clair (« 9 h – 20 h, fermé le lundi »).</summary>
        public static string Range(Hours h)
        {
            if (h.Always) return "24 h/24, 7 j/7";
            string text = Clock(h.Open) + " – " + Clock(h.Close);
            if (h.ClosedDay >= 0) text += ", fermé le " + DayName(h.ClosedDay);
            return text;
        }
    }
}
