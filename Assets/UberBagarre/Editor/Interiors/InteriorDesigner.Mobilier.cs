using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Le mobilier de tous les jours : chaises, tables dressées, banquettes, tabourets,
    /// canapés, bureaux, bibliothèques ; et ce qui habille une pièce : plantes, cadres,
    /// horloge, extincteur, poubelle, tableaux de menu, tapis, caméras, ventilateurs.
    ///
    /// Même convention que les meubles de vente : longueur le long de X, face vers −Z.
    /// Une chaise « regarde » vers −Z (les genoux de celui qui s'y assoit).
    /// </summary>
    public static partial class InteriorDesigner
    {
        // ================================================================== sièges

        /// <summary>Le cap (degrés) d'un siège posé en <paramref name="seat"/> pour qu'il regarde <paramref name="target"/>.</summary>
        private static float Facing(Vector3 seat, Vector3 target)
        {
            Vector3 d = seat - target;
            return Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
        }

        /// <summary>Une chaise : bistrot (bois courbé), diner (chrome et skaï) ou bureau visiteur.</summary>
        private static void Chair(InteriorPlan p, Vector3 at, float yaw, string seat, string frame, int style = 0)
        {
            p.Push(at, yaw);
            const float sy = 0.46f;
            if (style == 1)
            {
                // Chrome et skaï : piètement tube, assise et dossier rembourrés.
                p.Box("Assise", V(0f, sy, 0f), V(0.44f, 0.07f, 0.42f), seat);
                p.Box("Dossier", V(0f, 0.78f, 0.21f), V(0.42f, 0.34f, 0.06f), seat, V(-6f, 0f, 0f));
                for (int k = -1; k <= 1; k += 2)
                {
                    p.Cylinder("Pied", V(k * 0.19f, sy * 0.5f, -0.17f), 0.013f, sy, "chrome");
                    p.Cylinder("Pied", V(k * 0.19f, (sy + 0.62f) * 0.5f, 0.19f), 0.013f, sy + 0.62f, "chrome");
                }
            }
            else
            {
                p.Box("Assise", V(0f, sy, 0f), V(0.42f, 0.035f, 0.42f), seat);
                for (int i = 0; i < 4; i++)
                {
                    float x = i % 2 == 0 ? -0.18f : 0.18f, z = i < 2 ? -0.18f : 0.18f;
                    p.Cylinder("Pied", V(x, sy * 0.5f, z), 0.016f, sy, frame, V(i < 2 ? -3f : 3f, 0f, x < 0f ? 3f : -3f));
                }

                p.Box("Traverse", V(0f, 0.18f, -0.18f), V(0.36f, 0.02f, 0.02f), frame);
                p.Box("Traverse", V(0f, 0.18f, 0.18f), V(0.36f, 0.02f, 0.02f), frame);
                p.Cylinder("Montant", V(-0.18f, 0.7f, 0.19f), 0.016f, 0.5f, frame, V(-6f, 0f, 0f));
                p.Cylinder("Montant", V(0.18f, 0.7f, 0.19f), 0.016f, 0.5f, frame, V(-6f, 0f, 0f));
                if (style == 2)
                {
                    p.Box("Dossier", V(0f, 0.74f, 0.2f), V(0.38f, 0.3f, 0.04f), seat, V(-6f, 0f, 0f));
                }
                else
                {
                    p.Box("Barreau", V(0f, 0.9f, 0.215f), V(0.38f, 0.07f, 0.02f), frame, V(-6f, 0f, 0f));
                    p.Box("Barreau", V(0f, 0.68f, 0.195f), V(0.36f, 0.03f, 0.02f), frame, V(-6f, 0f, 0f));
                }
            }

            p.Block("Chaise", V(0f, 0.25f, 0f), V(0.42f, 0.5f, 0.42f));
            p.Pop();
        }

        /// <summary>Un tabouret de bar : pied chromé, repose-pieds en anneau, assise ronde.</summary>
        private static void Stool(InteriorPlan p, Vector3 at, string seat, float height = 0.76f)
        {
            p.Cylinder("Socle", at + V(0f, 0.012f, 0f), 0.2f, 0.024f, "chrome");
            p.Cylinder("Pied", at + V(0f, height * 0.5f, 0f), 0.03f, height, "chrome");
            for (int i = 0; i < 8; i++)
            {
                float a = i * 45f;
                Vector3 o = Quaternion.Euler(0f, a, 0f) * V(0f, 0f, 0.17f);
                p.Cylinder("Anneau", at + o + V(0f, height * 0.42f, 0f), 0.009f, 0.135f, "chrome", V(0f, a + 90f, 90f));
            }

            p.Cylinder("Rayon", at + V(0f, height * 0.42f, 0f), 0.008f, 0.34f, "chrome", V(0f, 0f, 90f));
            p.Cylinder("Rayon", at + V(0f, height * 0.42f, 0f), 0.008f, 0.34f, "chrome", V(90f, 0f, 0f));
            p.Cylinder("Assise", at + V(0f, height + 0.04f, 0f), 0.19f, 0.08f, seat);
            p.Cylinder("Jonc", at + V(0f, height + 0.01f, 0f), 0.195f, 0.025f, "chrome");
            p.Block("Tabouret", at + V(0f, height * 0.5f, 0f), V(0.3f, height, 0.3f));
        }

        /// <summary>Une banquette de diner ou de bar : socle, assise et haut dossier capitonnés, couvre-dossier.</summary>
        private static void Booth(InteriorPlan p, Vector3 at, float yaw, float length, string upholstery, string frame)
        {
            p.Push(at, yaw);
            p.Box("Socle", V(0f, 0.18f, 0.02f), V(length, 0.36f, 0.46f), frame);
            p.Box("Assise", V(0f, 0.42f, -0.01f), V(length - 0.02f, 0.12f, 0.5f), upholstery);
            p.Box("Dossier", V(0f, 0.8f, 0.23f), V(length - 0.02f, 0.72f, 0.14f), upholstery, V(-7f, 0f, 0f));
            p.Box("Couvre-dossier", V(0f, 1.19f, 0.26f), V(length, 0.05f, 0.2f), frame);
            p.Box("Joue", V(-length * 0.5f - 0.02f, 0.6f, 0.06f), V(0.04f, 1.2f, 0.62f), frame);
            p.Box("Joue", V(length * 0.5f + 0.02f, 0.6f, 0.06f), V(0.04f, 1.2f, 0.62f), frame);
            for (int i = 1; i < 4; i++)
            {
                p.Box("Capiton", V(0f, 0.55f + i * 0.15f, 0.155f + i * 0.018f), V(length - 0.06f, 0.012f, 0.012f), "noir", V(-7f, 0f, 0f));
            }

            p.Block("Banquette", V(0f, 0.6f, 0.05f), V(length + 0.08f, 1.2f, 0.62f));
            p.Pop();
        }

        /// <summary>Un canapé : socle, coussins d'assise, dossier, accoudoirs, petits pieds.</summary>
        private static void Sofa(InteriorPlan p, Vector3 at, float yaw, float length, string surface, bool chesterfield = false)
        {
            p.Push(at, yaw);
            const float depth = 0.88f;
            p.Box("Socle", V(0f, 0.26f, 0f), V(length, 0.3f, depth), surface);
            int cushions = Mathf.Max(1, Mathf.RoundToInt((length - 0.4f) / 0.7f));
            float cw = (length - 0.42f) / cushions;
            for (int i = 0; i < cushions; i++)
            {
                float x = -length * 0.5f + 0.21f + cw * (i + 0.5f);
                p.Box("Coussin", V(x, 0.48f, -0.06f), V(cw - 0.015f, 0.15f, 0.64f), surface);
                if (!chesterfield) p.Box("Coussin de dos", V(x, 0.72f, 0.24f), V(cw - 0.015f, 0.4f, 0.2f), surface, V(-10f, 0f, 0f));
            }

            p.Box("Dossier", V(0f, 0.62f, 0.37f), V(length, chesterfield ? 0.5f : 0.66f, 0.16f), surface);
            p.Box("Accoudoir", V(-length * 0.5f + 0.1f, 0.5f, 0f), V(0.2f, chesterfield ? 0.5f : 0.36f, depth), surface);
            p.Box("Accoudoir", V(length * 0.5f - 0.1f, 0.5f, 0f), V(0.2f, chesterfield ? 0.5f : 0.36f, depth), surface);
            if (chesterfield)
            {
                p.Cylinder("Rouleau", V(-length * 0.5f + 0.1f, 0.76f, 0f), 0.12f, depth, surface, V(90f, 0f, 0f));
                p.Cylinder("Rouleau", V(length * 0.5f - 0.1f, 0.76f, 0f), 0.12f, depth, surface, V(90f, 0f, 0f));
                for (float x = -length * 0.5f + 0.35f; x < length * 0.5f - 0.3f; x += 0.16f)
                {
                    for (int r = 0; r < 2; r++) p.Sphere("Bouton", V(x + r * 0.08f, 0.62f + r * 0.13f, 0.285f), V(0.02f, 0.02f, 0.01f), "noir");
                }
            }

            for (int i = 0; i < 4; i++)
            {
                p.Cylinder("Pied", V(i % 2 == 0 ? -length * 0.5f + 0.08f : length * 0.5f - 0.08f, 0.05f, i < 2 ? -0.34f : 0.34f), 0.025f, 0.1f,
                    "bois_fonce");
            }

            p.Block("Canape", V(0f, 0.42f, 0f), V(length, 0.84f, depth));
            p.Pop();
        }

        private static void Armchair(InteriorPlan p, Vector3 at, float yaw, string surface)
        {
            p.Push(at, yaw);
            p.Box("Socle", V(0f, 0.26f, 0f), V(0.82f, 0.3f, 0.82f), surface);
            p.Box("Coussin", V(0f, 0.47f, -0.05f), V(0.5f, 0.14f, 0.6f), surface);
            p.Box("Dossier", V(0f, 0.7f, 0.33f), V(0.82f, 0.8f, 0.16f), surface, V(-6f, 0f, 0f));
            p.Box("Accoudoir", V(-0.33f, 0.52f, 0f), V(0.16f, 0.4f, 0.82f), surface);
            p.Box("Accoudoir", V(0.33f, 0.52f, 0f), V(0.16f, 0.4f, 0.82f), surface);
            for (int i = 0; i < 4; i++)
            {
                p.Cylinder("Pied", V(i % 2 == 0 ? -0.34f : 0.34f, 0.05f, i < 2 ? -0.32f : 0.32f), 0.022f, 0.1f, "bois_fonce");
            }

            p.Block("Fauteuil", V(0f, 0.45f, 0f), V(0.82f, 0.9f, 0.82f));
            p.Pop();
        }

        /// <summary>Une rangée de sièges d'attente reliés par une poutre (médecin, poste, laverie).</summary>
        private static void WaitingChairs(InteriorPlan p, Vector3 at, float yaw, int seats, string seat)
        {
            p.Push(at, yaw);
            float length = seats * 0.56f;
            p.Box("Poutre", V(0f, 0.3f, 0.05f), V(length, 0.05f, 0.08f), "metal_noir");
            for (int i = 0; i < seats; i++)
            {
                float x = -length * 0.5f + 0.28f + i * 0.56f;
                p.Box("Assise", V(x, 0.45f, -0.02f), V(0.48f, 0.05f, 0.46f), seat, V(4f, 0f, 0f));
                p.Box("Dossier", V(x, 0.74f, 0.22f), V(0.48f, 0.42f, 0.04f), seat, V(-8f, 0f, 0f));
                p.Box("Attache", V(x, 0.38f, 0.05f), V(0.06f, 0.12f, 0.06f), "metal_noir");
                if (i > 0) p.Box("Accoudoir", V(x - 0.28f, 0.62f, 0f), V(0.04f, 0.04f, 0.4f), "metal_noir");
            }

            for (int k = -1; k <= 1; k += 2)
            {
                float x = k * (length * 0.5f - 0.15f);
                p.Box("Pied", V(x, 0.15f, 0.05f), V(0.05f, 0.3f, 0.05f), "metal_noir");
                p.Box("Patin", V(x, 0.015f, 0.05f), V(0.06f, 0.03f, 0.5f), "metal_noir");
            }

            p.Block("Sieges", V(0f, 0.4f, 0.02f), V(length, 0.8f, 0.5f));
            p.Pop();
        }

        /// <summary>Un fauteuil de bureau : étoile à cinq branches et roulettes, vérin, assise, dossier, accoudoirs.</summary>
        private static void OfficeChair(InteriorPlan p, Vector3 at, float yaw, string surface)
        {
            p.Push(at, yaw);
            for (int i = 0; i < 5; i++)
            {
                float a = i * 72f + 18f;
                Vector3 o = Quaternion.Euler(0f, a, 0f) * V(0f, 0f, 0.15f);
                p.Box("Branche", o + V(0f, 0.08f, 0f), V(0.04f, 0.035f, 0.3f), "plastique_noir", V(0f, a, 0f));
                p.Sphere("Roulette", o * 2f + V(0f, 0.03f, 0f), V(0.05f, 0.05f, 0.05f), "noir");
            }

            p.Cylinder("Verin", V(0f, 0.27f, 0f), 0.025f, 0.34f, "chrome");
            p.Box("Assise", V(0f, 0.48f, 0f), V(0.5f, 0.08f, 0.48f), surface);
            p.Box("Dossier", V(0f, 0.84f, 0.25f), V(0.46f, 0.56f, 0.07f), surface, V(-8f, 0f, 0f));
            p.Box("Lien de dossier", V(0f, 0.55f, 0.24f), V(0.06f, 0.14f, 0.03f), "plastique_noir");
            p.Box("Accoudoir", V(-0.27f, 0.66f, 0f), V(0.05f, 0.03f, 0.3f), "plastique_noir");
            p.Box("Accoudoir", V(0.27f, 0.66f, 0f), V(0.05f, 0.03f, 0.3f), "plastique_noir");
            p.Box("Support", V(-0.27f, 0.57f, 0.02f), V(0.03f, 0.18f, 0.04f), "plastique_noir");
            p.Box("Support", V(0.27f, 0.57f, 0.02f), V(0.03f, 0.18f, 0.04f), "plastique_noir");
            p.Block("Fauteuil de bureau", V(0f, 0.3f, 0f), V(0.5f, 0.6f, 0.5f));
            p.Pop();
        }

        // ================================================================== tables

        /// <summary>
        /// Une table (ronde ou rectangulaire) sur pied central ou quatre pieds, et ce qui la
        /// dresse selon <paramref name="setting"/> : « diner » (porte-serviettes, sauces, sucrier),
        /// « restaurant » (assiettes, verres, couverts), « cafe » (tasses), « bar » (verres de bière).
        /// </summary>
        private static void Table(InteriorPlan p, Vector3 at, float yaw, float w, float d, string top, bool round, string setting,
            float height = 0.75f)
        {
            p.Push(at, yaw);
            if (round)
            {
                p.Oval("Plateau", V(0f, height - 0.02f, 0f), V(w, 0.04f, d), top);
                p.Oval("Chant", V(0f, height - 0.045f, 0f), V(w - 0.02f, 0.01f, d - 0.02f), "metal_noir");
            }
            else
            {
                p.Box("Plateau", V(0f, height - 0.02f, 0f), V(w, 0.04f, d), top);
                p.Box("Chant", V(0f, height - 0.02f, -d * 0.5f), V(w + 0.005f, 0.042f, 0.008f), top == "stratifie" ? "chrome" : top);
            }

            if (w <= 0.95f && d <= 0.95f || round)
            {
                p.Cylinder("Fut", V(0f, (height - 0.04f) * 0.5f, 0f), 0.04f, height - 0.04f, "metal_noir");
                p.Cylinder("Pied", V(0f, 0.015f, 0f), Mathf.Min(w, d) * 0.3f, 0.03f, "metal_noir");
            }
            else
            {
                for (int i = 0; i < 4; i++)
                {
                    float x = (i % 2 == 0 ? -1f : 1f) * (w * 0.5f - 0.06f), z = (i < 2 ? -1f : 1f) * (d * 0.5f - 0.06f);
                    p.Box("Pied", V(x, (height - 0.04f) * 0.5f, z), V(0.05f, height - 0.04f, 0.05f), top == "stratifie" ? "metal_noir" : top);
                }
            }

            p.Block("Table", V(0f, height * 0.5f, 0f), V(w, height, d));
            TableSetting(p, setting, w, d, height);
            p.Pop();
        }

        private static void TableSetting(InteriorPlan p, string setting, float w, float d, float height)
        {
            float y = height;
            switch (setting)
            {
                case "diner":
                    p.Box("Porte-serviettes", V(0f, y + 0.06f, 0f), V(0.12f, 0.12f, 0.08f), "chrome");
                    p.Box("Serviettes", V(0f, y + 0.07f, 0f), V(0.1f, 0.1f, 0.06f), "blanc");
                    p.Cylinder("Ketchup", V(0.12f, y + 0.09f, 0.02f), 0.028f, 0.18f, "rouge");
                    p.Cylinder("Moutarde", V(0.18f, y + 0.09f, -0.02f), 0.028f, 0.18f, "jaune");
                    p.Cylinder("Sel", V(-0.12f, y + 0.04f, 0.02f), 0.018f, 0.08f, "verre");
                    p.Cylinder("Poivre", V(-0.16f, y + 0.04f, 0.02f), 0.018f, 0.08f, "noir");
                    p.Cylinder("Sucrier", V(-0.22f, y + 0.06f, -0.04f), 0.035f, 0.12f, "verre");
                    p.Box("Menu", V(Rand(-0.2f, 0.2f), y + 0.004f, -d * 0.3f), V(0.24f, 0.008f, 0.34f), "rouge", V(0f, Rand(-20f, 20f), 0f));
                    break;
                case "restaurant":
                    for (int k = -1; k <= 1; k += 2)
                    {
                        float z = k * (d * 0.5f - 0.2f);
                        p.Cylinder("Assiette", V(0f, y + 0.008f, z), 0.13f, 0.016f, "ceramique");
                        p.Box("Fourchette", V(-0.17f, y + 0.003f, z), V(0.02f, 0.004f, 0.18f), "chrome");
                        p.Box("Couteau", V(0.17f, y + 0.003f, z), V(0.02f, 0.004f, 0.2f), "chrome");
                        p.Cylinder("Verre", V(0.15f, y + 0.06f, z - k * 0.16f), 0.03f, 0.12f, "verre");
                        p.Box("Serviette", V(-0.17f, y + 0.012f, z), V(0.1f, 0.012f, 0.14f), "tissu_rouge");
                    }

                    p.Cylinder("Bougeoir", V(0f, y + 0.05f, 0f), 0.03f, 0.1f, "verre");
                    p.Cylinder("Bougie", V(0f, y + 0.05f, 0f), 0.018f, 0.07f, "lumiere_chaude");
                    break;
                case "cafe":
                    p.Cylinder("Soucoupe", V(0.12f, y + 0.005f, 0.05f), 0.07f, 0.01f, "ceramique");
                    p.Cylinder("Tasse", V(0.12f, y + 0.045f, 0.05f), 0.04f, 0.07f, "ceramique");
                    p.Cylinder("Cafe", V(0.12f, y + 0.078f, 0.05f), 0.034f, 0.004f, "bois_fonce");
                    if (Pick(2) == 0) p.Box("Carnet", V(-0.12f, y + 0.008f, -0.05f), V(0.15f, 0.016f, 0.21f), Product(), V(0f, Rand(-30f, 30f), 0f));
                    if (Pick(2) == 0) p.Box("Ordinateur portable", V(-0.08f, y + 0.012f, 0.0f), V(0.32f, 0.02f, 0.22f), "metal");
                    break;
                case "bar":
                    p.Cylinder("Verre de biere", V(0.1f, y + 0.08f, 0.05f), 0.035f, 0.16f, "liquide_ambre");
                    p.Cylinder("Mousse", V(0.1f, y + 0.165f, 0.05f), 0.035f, 0.02f, "blanc");
                    if (Pick(2) == 0)
                    {
                        p.Cylinder("Verre de biere", V(-0.12f, y + 0.06f, -0.06f), 0.035f, 0.12f, "liquide_ambre");
                    }

                    p.Box("Sous-bock", V(0.1f, y + 0.002f, 0.05f), V(0.1f, 0.004f, 0.1f), Product());
                    p.Cylinder("Cendrier", V(-0.05f, y + 0.015f, 0.1f), 0.06f, 0.03f, "verre");
                    break;
            }
        }

        /// <summary>Une table et ses chaises tout autour, tournées vers elle.</summary>
        private static void TableSet(InteriorPlan p, Vector3 at, float yaw, float w, float d, int chairs, string top, string seat, string frame,
            bool round, string setting, int chairStyle = 0)
        {
            p.Push(at, yaw);
            Table(p, Vector3.zero, 0f, w, d, top, round, setting);
            for (int i = 0; i < chairs; i++)
            {
                Vector3 c;
                if (chairs == 2) c = V(0f, 0f, (i == 0 ? -1f : 1f) * (d * 0.5f + 0.32f));
                else if (chairs == 4 && !round)
                {
                    c = i < 2 ? V(0f, 0f, (i == 0 ? -1f : 1f) * (d * 0.5f + 0.32f)) : V((i == 2 ? -1f : 1f) * (w * 0.5f + 0.32f), 0f, 0f);
                }
                else
                {
                    float a = i * 360f / chairs + 45f;
                    c = Quaternion.Euler(0f, a, 0f) * V(0f, 0f, Mathf.Max(w, d) * 0.5f + 0.32f);
                }

                // Une chaise un peu tirée ou de travers : on vient de s'y asseoir.
                c += V(Rand(-0.04f, 0.04f), 0f, Rand(-0.04f, 0.04f));
                Chair(p, c, Facing(c, Vector3.zero) + Rand(-12f, 12f), seat, frame, chairStyle);
            }

            p.Pop();
        }

        private static void CoffeeTable(InteriorPlan p, Vector3 at, float yaw, float w, float d, string top)
        {
            p.Push(at, yaw);
            p.Box("Plateau", V(0f, 0.42f, 0f), V(w, 0.04f, d), top);
            p.Box("Tablette", V(0f, 0.12f, 0f), V(w - 0.1f, 0.02f, d - 0.1f), top);
            for (int i = 0; i < 4; i++)
            {
                p.Box("Pied", V((i % 2 == 0 ? -1f : 1f) * (w * 0.5f - 0.04f), 0.2f, (i < 2 ? -1f : 1f) * (d * 0.5f - 0.04f)), V(0.04f, 0.4f, 0.04f),
                    "metal_noir");
            }

            for (int i = 0; i < 3; i++)
            {
                p.Box("Magazine", V(Rand(-w * 0.3f, w * 0.3f), 0.445f + i * 0.006f, Rand(-d * 0.2f, d * 0.2f)), V(0.21f, 0.006f, 0.28f), Product(),
                    V(0f, Rand(-40f, 40f), 0f));
            }

            p.Block("Table basse", V(0f, 0.22f, 0f), V(w, 0.44f, d));
            p.Pop();
        }

        // ================================================================== bureau

        private static void Monitor(InteriorPlan p, Vector3 at, float yaw, float width)
        {
            p.Push(at, yaw);
            float h = width * 0.6f;
            p.Box("Pied d'ecran", V(0f, 0.008f, 0.05f), V(0.2f, 0.016f, 0.16f), "plastique_noir");
            p.Box("Colonne", V(0f, 0.12f, 0.08f), V(0.05f, 0.22f, 0.03f), "plastique_noir");
            p.Box("Ecran", V(0f, 0.14f + h * 0.5f, 0.05f), V(width, h, 0.03f), "plastique_noir");
            p.Box("Affichage", V(0f, 0.14f + h * 0.5f, 0.033f), V(width - 0.03f, h - 0.03f, 0.004f), Pick(3) == 0 ? "ecran_vert" : "ecran");
            p.Pop();
        }

        private static void Keyboard(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Clavier", V(0f, 0.012f, 0f), V(0.44f, 0.024f, 0.15f), "plastique_noir", V(4f, 0f, 0f));
            p.Box("Touches", V(0f, 0.026f, 0.005f), V(0.41f, 0.006f, 0.12f), "gris", V(4f, 0f, 0f));
            p.Box("Souris", V(0.32f, 0.015f, 0.02f), V(0.06f, 0.03f, 0.1f), "plastique_noir");
            p.Pop();
        }

        private static void Papers(InteriorPlan p, Vector3 at, float yaw)
        {
            int n = 2 + Pick(5);
            for (int i = 0; i < n; i++)
            {
                p.Box("Feuille", at + V(Rand(-0.03f, 0.03f), 0.002f + i * 0.003f, Rand(-0.03f, 0.03f)), V(0.21f, 0.002f, 0.297f), "papier",
                    V(0f, yaw + Rand(-12f, 12f), 0f));
            }

            if (Pick(2) == 0) p.Box("Chemise", at + V(0.25f, 0.01f, 0f), V(0.24f, 0.02f, 0.32f), Product(), V(0f, yaw + Rand(-20f, 20f), 0f));
            if (Pick(2) == 0) p.Cylinder("Stylo", at + V(-0.15f, 0.006f, 0.05f), 0.005f, 0.14f, "noir", V(90f, Rand(0f, 180f), 0f));
        }

        /// <summary>Une lampe de bureau à bras (ou une lampe de banquier, abat-jour vert).</summary>
        private static void DeskLamp(InteriorPlan p, Vector3 at, float yaw, bool banker = false)
        {
            p.Push(at, yaw);
            if (banker)
            {
                p.Box("Socle", V(0f, 0.015f, 0f), V(0.2f, 0.03f, 0.14f), "laiton");
                p.Cylinder("Tige", V(0f, 0.17f, 0.03f), 0.01f, 0.3f, "laiton");
                p.Oval("Abat-jour", V(0f, 0.33f, -0.02f), V(0.32f, 0.08f, 0.14f), "vert", V(0f, 0f, 0f));
                p.Box("Lumiere", V(0f, 0.29f, -0.02f), V(0.26f, 0.01f, 0.1f), "lumiere_chaude");
            }
            else
            {
                p.Cylinder("Socle", V(0f, 0.012f, 0f), 0.08f, 0.024f, "metal_noir");
                p.Cylinder("Bras", V(0f, 0.2f, 0.04f), 0.008f, 0.38f, "metal_noir", V(-15f, 0f, 0f));
                p.Cylinder("Bras", V(0f, 0.4f, -0.06f), 0.008f, 0.28f, "metal_noir", V(60f, 0f, 0f));
                p.Cylinder("Reflecteur", V(0f, 0.36f, -0.18f), 0.06f, 0.1f, "metal_noir", V(-30f, 0f, 0f));
                p.Sphere("Ampoule", V(0f, 0.32f, -0.2f), V(0.05f, 0.05f, 0.05f), "lumiere_chaude");
            }

            p.Pop();
        }

        /// <summary>Un bureau de travail complet : poste, fauteuil, papiers, lampe, téléphone, mug.</summary>
        private static void Workstation(InteriorPlan p, Vector3 at, float yaw, float length, string body, string top, string chair)
        {
            p.Push(at, yaw);
            DeskBody(p, length, 0.75f, body, top, true);
            // Le poste fait face à celui qui s'assoit côté +Z.
            Monitor(p, V(0.1f, 0.76f, -0.18f), 180f + Rand(-8f, 8f), 0.52f);
            Keyboard(p, V(0.05f, 0.76f, 0.12f), 180f);
            Papers(p, V(-length * 0.5f + 0.35f, 0.76f, 0f), Rand(-20f, 20f));
            p.Box("Telephone", V(length * 0.5f - 0.25f, 0.79f, 0f), V(0.18f, 0.06f, 0.2f), "plastique_noir");
            p.Cylinder("Mug", V(length * 0.5f - 0.45f, 0.81f, 0.18f), 0.04f, 0.1f, Product());
            if (Pick(2) == 0) p.Cylinder("Pot a crayons", V(-length * 0.5f + 0.15f, 0.81f, -0.25f), 0.04f, 0.1f, "metal_noir");
            OfficeChair(p, V(Rand(-0.1f, 0.1f), 0f, 0.68f), 180f + Rand(-20f, 20f), chair);
            p.Pop();
        }

        /// <summary>Une bibliothèque : montants, tablettes, fond, garnie (livres, classeurs, boîtes).</summary>
        private static void Bookcase(InteriorPlan p, Vector3 at, float yaw, float width, float height, string frame, string kind)
        {
            int levels = Mathf.Max(2, Mathf.FloorToInt((height - 0.1f) / 0.36f));
            WallShelf(p, at, yaw, width, levels, kind, frame, 0.32f, (height - 0.1f) / levels, 0.1f);
        }

        private static void FilingCabinet(InteriorPlan p, Vector3 at, float yaw, int drawers)
        {
            p.Push(at, yaw);
            float h = drawers * 0.33f + 0.04f;
            p.Box("Classeur", V(0f, h * 0.5f, 0f), V(0.46f, h, 0.62f), "metal", true);
            for (int i = 0; i < drawers; i++)
            {
                float y = 0.04f + 0.165f + i * 0.33f;
                p.Box("Tiroir", V(0f, y, -0.313f), V(0.42f, 0.3f, 0.008f), "metal");
                p.Box("Poignee", V(0f, y + 0.06f, -0.322f), V(0.12f, 0.02f, 0.012f), "chrome");
                p.Box("Porte-etiquette", V(0f, y + 0.1f, -0.319f), V(0.08f, 0.03f, 0.004f), "papier");
            }

            if (Pick(2) == 0) Plant(p, V(0f, h, 0f), 3);
            p.Pop();
        }

        // ================================================================== décor

        /// <summary>
        /// Une plante en pot : 0 un buisson, 1 une sansevière (feuilles dressées), 2 un ficus
        /// sur tronc, 3 une petite plante de bureau.
        /// </summary>
        private static void Plant(InteriorPlan p, Vector3 at, int kind)
        {
            if (kind < 0) kind = Pick(3);
            float pot = kind == 3 ? 0.08f : Rand(0.17f, 0.22f);
            float potH = kind == 3 ? 0.12f : Rand(0.32f, 0.45f);
            string potSurface = kind == 3 ? "ceramique" : Pick(3) == 0 ? "beton_brut" : Pick(2) == 0 ? "ceramique" : "plastique_noir";
            p.Cylinder("Pot", at + V(0f, potH * 0.5f, 0f), pot, potH, potSurface);
            p.Cylinder("Bord du pot", at + V(0f, potH - 0.015f, 0f), pot + 0.012f, 0.03f, potSurface);
            p.Cylinder("Terre", at + V(0f, potH - 0.02f, 0f), pot - 0.01f, 0.02f, "terre");
            Vector3 top = at + V(0f, potH, 0f);
            switch (kind)
            {
                case 1:
                    for (int i = 0; i < 9; i++)
                    {
                        float h = Rand(0.45f, 0.85f);
                        Vector3 o = V(Rand(-pot * 0.5f, pot * 0.5f), h * 0.5f, Rand(-pot * 0.5f, pot * 0.5f));
                        p.Box("Feuille", top + o, V(0.06f, h, 0.012f), i % 3 == 0 ? "feuillage_clair" : "feuillage",
                            V(Rand(-12f, 12f), Rand(0f, 180f), Rand(-12f, 12f)));
                    }

                    break;
                case 2:
                    p.Cylinder("Tronc", top + V(0f, 0.6f, 0f), 0.025f, 1.2f, "bois_fonce");
                    for (int i = 0; i < 9; i++)
                    {
                        Vector3 o = V(Rand(-0.3f, 0.3f), Rand(0.95f, 1.5f), Rand(-0.3f, 0.3f));
                        p.Sphere("Feuillage", top + o, V(Rand(0.3f, 0.45f), Rand(0.22f, 0.32f), Rand(0.3f, 0.45f)), i % 3 == 0 ? "feuillage_clair" : "feuillage");
                    }

                    p.Block("Plante", at + V(0f, 0.5f, 0f), V(pot * 2f, 1f, pot * 2f));
                    break;
                case 3:
                    for (int i = 0; i < 4; i++)
                    {
                        p.Sphere("Feuillage", top + V(Rand(-0.04f, 0.04f), Rand(0.05f, 0.12f), Rand(-0.04f, 0.04f)), V(0.12f, 0.09f, 0.12f), "feuillage");
                    }

                    break;
                default:
                    for (int i = 0; i < 7; i++)
                    {
                        Vector3 o = V(Rand(-0.18f, 0.18f), Rand(0.15f, 0.55f), Rand(-0.18f, 0.18f));
                        p.Sphere("Feuillage", top + o, V(Rand(0.25f, 0.4f), Rand(0.22f, 0.32f), Rand(0.25f, 0.4f)), i % 3 == 0 ? "feuillage_clair" : "feuillage");
                    }

                    p.Block("Plante", at + V(0f, 0.4f, 0f), V(pot * 2f, 0.8f, pot * 2f));
                    break;
            }
        }

        /// <summary>
        /// Un cadre au mur (le dos contre le mur, en +Z) : une affiche aux aplats de couleur,
        /// une photo (ciel, colline), ou un diplôme.
        /// </summary>
        private static void Frame(InteriorPlan p, Vector3 at, float yaw, float w, float h, string style = null)
        {
            p.Push(at, yaw);
            string frame = Pick(3) == 0 ? "bois" : Pick(2) == 0 ? "metal_noir" : "laiton";
            p.Box("Cadre", V(0f, 0f, -0.012f), V(w + 0.05f, h + 0.05f, 0.024f), frame);
            p.Box("Fond", V(0f, 0f, -0.025f), V(w, h, 0.004f), style == "diplome" ? "papier" : "blanc");
            switch (style ?? (Pick(2) == 0 ? "affiche" : "photo"))
            {
                case "photo":
                    p.Box("Ciel", V(0f, h * 0.2f, -0.028f), V(w * 0.94f, h * 0.55f, 0.003f), "chromo");
                    p.Box("Colline", V(0f, -h * 0.25f, -0.028f), V(w * 0.94f, h * 0.42f, 0.003f), "pelouse");
                    p.Sphere("Soleil", V(w * 0.25f, h * 0.3f, -0.03f), V(h * 0.15f, h * 0.15f, 0.003f), "jaune");
                    break;
                case "diplome":
                    p.Box("Titre", V(0f, h * 0.3f, -0.028f), V(w * 0.6f, h * 0.08f, 0.003f), "noir");
                    for (int i = 0; i < 4; i++) p.Box("Ligne", V(0f, h * (0.1f - i * 0.1f), -0.028f), V(w * 0.7f, 0.006f, 0.003f), "gris");
                    p.Cylinder("Sceau", V(w * 0.3f, -h * 0.32f, -0.03f), h * 0.08f, 0.004f, "rouge", V(90f, 0f, 0f));
                    break;
                default:
                    p.Box("Aplat", V(Rand(-0.1f, 0.1f) * w, Rand(0f, 0.2f) * h, -0.028f), V(w * Rand(0.4f, 0.9f), h * Rand(0.3f, 0.6f), 0.003f), Product());
                    p.Sphere("Disque", V(Rand(-0.25f, 0.25f) * w, Rand(-0.1f, 0.25f) * h, -0.031f), V(w * 0.35f, w * 0.35f, 0.003f), Product());
                    p.Box("Titre", V(0f, -h * 0.36f, -0.03f), V(w * 0.7f, h * 0.06f, 0.003f), "noir");
                    break;
            }

            p.Pop();
        }

        /// <summary>Une horloge murale : boîtier, cadran, aiguilles à une heure tirée au sort.</summary>
        private static void Clock(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Cylinder("Boitier", V(0f, 0f, -0.025f), 0.17f, 0.05f, "metal_noir", V(90f, 0f, 0f));
            p.Cylinder("Cadran", V(0f, 0f, -0.051f), 0.15f, 0.004f, "blanc", V(90f, 0f, 0f));
            for (int i = 0; i < 12; i++)
            {
                Vector3 o = Quaternion.Euler(0f, 0f, i * 30f) * V(0f, 0.125f, 0f);
                p.Box("Graduation", o + V(0f, 0f, -0.054f), V(0.008f, i % 3 == 0 ? 0.03f : 0.015f, 0.002f), "noir", V(0f, 0f, i * 30f));
            }

            float hour = Rand(0f, 360f), minute = Rand(0f, 360f);
            p.Box("Petite aiguille", Quaternion.Euler(0f, 0f, -hour) * V(0f, 0.04f, 0f) + V(0f, 0f, -0.056f), V(0.012f, 0.08f, 0.002f), "noir",
                V(0f, 0f, -hour));
            p.Box("Grande aiguille", Quaternion.Euler(0f, 0f, -minute) * V(0f, 0.055f, 0f) + V(0f, 0f, -0.058f), V(0.008f, 0.11f, 0.002f), "noir",
                V(0f, 0f, -minute));
            p.Pop();
        }

        /// <summary>Un extincteur sur son support, et son pictogramme rouge au-dessus.</summary>
        private static void Extinguisher(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Support", V(0f, 0.95f, -0.01f), V(0.08f, 0.1f, 0.02f), "metal_noir");
            p.Cylinder("Extincteur", V(0f, 0.75f, -0.09f), 0.075f, 0.46f, "rouge");
            p.Sphere("Dome", V(0f, 0.98f, -0.09f), V(0.15f, 0.08f, 0.15f), "rouge");
            p.Box("Tete", V(0f, 1.04f, -0.09f), V(0.05f, 0.07f, 0.05f), "metal_noir");
            p.Box("Poignee", V(0f, 1.08f, -0.12f), V(0.03f, 0.015f, 0.1f), "metal_noir", V(15f, 0f, 0f));
            p.Cylinder("Tuyau", V(0.08f, 0.85f, -0.1f), 0.012f, 0.3f, "noir", V(0f, 0f, 12f));
            p.Box("Etiquette", V(0f, 0.72f, -0.166f), V(0.08f, 0.12f, 0.003f), "blanc");
            p.Box("Pictogramme", V(0f, 1.4f, -0.006f), V(0.2f, 0.2f, 0.012f), "rouge");
            p.Box("Pictogramme (dessin)", V(0f, 1.4f, -0.013f), V(0.06f, 0.12f, 0.002f), "blanc");
            p.Pop();
        }

        private static void TrashBin(InteriorPlan p, Vector3 at, bool pedal = false)
        {
            string body = pedal ? "inox" : Pick(2) == 0 ? "metal" : "plastique_noir";
            float h = pedal ? 0.45f : 0.62f;
            p.Cylinder("Poubelle", at + V(0f, h * 0.5f, 0f), pedal ? 0.14f : 0.18f, h, body);
            p.Cylinder("Couvercle", at + V(0f, h + 0.01f, 0f), pedal ? 0.145f : 0.185f, 0.02f, pedal ? "inox" : "noir");
            if (pedal) p.Box("Pedale", at + V(0f, 0.03f, -0.16f), V(0.08f, 0.02f, 0.06f), "metal_noir");
            else p.Cylinder("Sac", at + V(0f, h - 0.03f, 0f), 0.182f, 0.06f, "noir");
        }

        /// <summary>
        /// Un tableau de menu : ardoise encadrée (lignes de craie, prix à droite) ou trois écrans
        /// lumineux côte à côte. Accroché au mur (le dos en +Z).
        /// </summary>
        private static void MenuBoard(InteriorPlan p, Vector3 at, float yaw, float w, float h, bool digital)
        {
            p.Push(at, yaw);
            int panels = digital ? Mathf.Max(2, Mathf.RoundToInt(w / 0.9f)) : 1;
            float pw = w / panels;
            for (int k = 0; k < panels; k++)
            {
                float cx = -w * 0.5f + pw * (k + 0.5f);
                p.Box("Cadre", V(cx, 0f, -0.02f), V(pw - 0.02f, h, 0.04f), digital ? "plastique_noir" : "bois");
                p.Box("Fond", V(cx, 0f, -0.042f), V(pw - 0.1f, h - 0.08f, 0.004f), digital ? "ecran_tele" : "stratifie_fonce");
                p.Box("Bandeau", V(cx, h * 0.5f - 0.12f, -0.046f), V(pw * 0.6f, 0.07f, 0.003f), digital ? "neon_ambre" : "blanc");
                int lines = Mathf.FloorToInt((h - 0.3f) / 0.075f);
                for (int i = 0; i < lines; i++)
                {
                    float y = h * 0.5f - 0.24f - i * 0.075f;
                    float len = Rand(0.25f, 0.55f) * (pw - 0.2f);
                    p.Box("Ligne", V(cx - (pw - 0.2f) * 0.5f + len * 0.5f, y, -0.046f), V(len, 0.022f, 0.003f), "blanc");
                    p.Box("Prix", V(cx + (pw - 0.2f) * 0.5f - 0.05f, y, -0.046f), V(0.08f, 0.022f, 0.003f), digital ? "jaune" : "blanc");
                }
            }

            p.Pop();
        }

        /// <summary>Un tapis (bordure d'une autre couleur), à plat.</summary>
        private static void Rug(InteriorPlan p, Vector3 at, float yaw, float w, float d, string surface, string border)
        {
            p.Push(at, yaw);
            p.Box("Tapis", V(0f, 0.006f, 0f), V(w, 0.012f, d), border);
            p.Box("Tapis (champ)", V(0f, 0.008f, 0f), V(w - 0.2f, 0.012f, d - 0.2f), surface);
            p.Pop();
        }

        private static void FloorLamp(InteriorPlan p, Vector3 at)
        {
            p.Cylinder("Socle", at + V(0f, 0.015f, 0f), 0.15f, 0.03f, "metal_noir");
            p.Cylinder("Tige", at + V(0f, 0.8f, 0f), 0.012f, 1.56f, "laiton");
            p.Cylinder("Abat-jour", at + V(0f, 1.55f, 0f), 0.2f, 0.28f, "papier");
            p.Light(at + V(0f, 1.45f, 0f), new Color(1f, 0.8f, 0.55f), 0.7f, 3.5f);
        }

        /// <summary>Une caméra de surveillance dans un angle, son voyant rouge.</summary>
        private static void SecurityCamera(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Support", V(0f, 0f, 0.05f), V(0.04f, 0.04f, 0.1f), "blanc");
            p.Box("Camera", V(0f, -0.06f, -0.04f), V(0.08f, 0.08f, 0.2f), "blanc", V(25f, 0f, 0f));
            p.Cylinder("Objectif", V(0f, -0.1f, -0.14f), 0.025f, 0.02f, "noir", V(115f, 0f, 0f));
            p.Sphere("Voyant", V(0.03f, -0.04f, -0.12f), V(0.012f, 0.012f, 0.012f), "feu_rouge");
            p.Pop();
        }

        /// <summary>Un miroir bombé de surveillance, au plafond dans un coin.</summary>
        private static void DomeMirror(InteriorPlan p, Vector3 at)
        {
            p.Sphere("Miroir de surveillance", at, V(0.5f, 0.25f, 0.5f), "chrome");
            p.Cylinder("Cerclage", at + V(0f, 0.01f, 0f), 0.255f, 0.02f, "noir");
        }

        /// <summary>Un ventilateur de plafond : tige, moteur, cinq pales de bois, globe.</summary>
        private static void CeilingFan(InteriorPlan p, Vector3 at)
        {
            p.Cylinder("Tige", at + V(0f, -0.15f, 0f), 0.012f, 0.3f, "laiton");
            p.Cylinder("Moteur", at + V(0f, -0.36f, 0f), 0.09f, 0.14f, "laiton");
            float start = Rand(0f, 72f);
            for (int i = 0; i < 5; i++)
            {
                float a = start + i * 72f;
                Vector3 o = Quaternion.Euler(0f, a, 0f) * V(0f, 0f, 0.42f);
                p.Box("Pale", at + o + V(0f, -0.36f, 0f), V(0.14f, 0.01f, 0.6f), "bois_fonce", V(0f, a, 8f));
            }

            p.Sphere("Globe", at + V(0f, -0.48f, 0f), V(0.16f, 0.12f, 0.16f), "lumiere_chaude");
        }

        /// <summary>Des poteaux à corde (ou à sangle) pour faire la queue, de <paramref name="from"/> à <paramref name="to"/>.</summary>
        private static void Stanchions(InteriorPlan p, Vector3 from, Vector3 to, bool velvet)
        {
            Vector3 d = to - from;
            float length = new Vector2(d.x, d.z).magnitude;
            int posts = Mathf.Max(2, Mathf.RoundToInt(length / 1.4f) + 1);
            float yaw = Mathf.Atan2(d.x, d.z) * Mathf.Rad2Deg;
            for (int i = 0; i < posts; i++)
            {
                Vector3 at = from + d * (i / (float)(posts - 1));
                p.Cylinder("Socle", at + V(0f, 0.015f, 0f), 0.15f, 0.03f, velvet ? "laiton" : "chrome");
                p.Cylinder("Poteau", at + V(0f, 0.47f, 0f), 0.025f, 0.94f, velvet ? "laiton" : "chrome");
                p.Sphere("Boule", at + V(0f, 0.96f, 0f), V(0.07f, 0.07f, 0.07f), velvet ? "laiton" : "chrome");
                if (i == 0) continue;
                Vector3 prev = from + d * ((i - 1) / (float)(posts - 1));
                Vector3 mid = (prev + at) * 0.5f;
                float span = Vector3.Distance(prev, at);
                if (velvet) p.Cylinder("Corde", mid + V(0f, 0.8f, 0f), 0.02f, span, "tissu_rouge", V(90f, yaw, 0f));
                else p.Box("Sangle", mid + V(0f, 0.88f, 0f), V(0.005f, 0.05f, span), "bleu", V(0f, yaw, 0f));
            }

            p.Block("Barriere", (from + to) * 0.5f + V(0f, 0.5f, 0f), V(0.1f, 1f, length), yaw);
        }

        /// <summary>Un téléviseur plat accroché au mur (le dos en +Z).</summary>
        private static void WallTV(InteriorPlan p, Vector3 at, float yaw, float width)
        {
            p.Push(at, yaw);
            float h = width * 0.58f;
            p.Box("Fixation", V(0f, 0f, -0.02f), V(0.3f, 0.2f, 0.04f), "metal_noir");
            p.Box("Televiseur", V(0f, 0f, -0.065f), V(width, h, 0.05f), "plastique_noir");
            p.Box("Image", V(0f, 0f, -0.092f), V(width - 0.04f, h - 0.04f, 0.004f), "ecran_tele");
            p.Box("Image (bandeau)", V(0f, -h * 0.35f, -0.095f), V(width - 0.06f, h * 0.12f, 0.002f), Pick(2) == 0 ? "neon_rouge" : "bleu");
            p.Pop();
        }

        /// <summary>Un panneau de liège avec des feuilles punaisées (le dos en +Z).</summary>
        private static void CorkBoard(InteriorPlan p, Vector3 at, float yaw, float w, float h)
        {
            p.Push(at, yaw);
            p.Box("Cadre", V(0f, 0f, -0.012f), V(w + 0.05f, h + 0.05f, 0.024f), "bois");
            p.Box("Liege", V(0f, 0f, -0.025f), V(w, h, 0.004f), "liege");
            int n = Mathf.RoundToInt(w * h * 14f);
            for (int i = 0; i < n; i++)
            {
                float fw = Rand(0.12f, 0.22f), fh = Rand(0.15f, 0.3f);
                Vector3 c = V(Rand(-w * 0.5f + fw * 0.5f, w * 0.5f - fw * 0.5f), Rand(-h * 0.5f + fh * 0.5f, h * 0.5f - fh * 0.5f), -0.03f - i * 0.0005f);
                p.Box("Feuille", c, V(fw, fh, 0.002f), Pick(4) == 0 ? "jaune" : "papier", V(0f, 0f, Rand(-6f, 6f)));
                p.Sphere("Punaise", c + V(0f, fh * 0.42f, -0.004f), V(0.015f, 0.015f, 0.01f), Pick(2) == 0 ? "rouge" : "bleu");
            }

            p.Pop();
        }

        /// <summary>
        /// Une porte de service (réserve, toilettes, bureau) dans un mur : chambranle, battant,
        /// poignée, plaque. Fermée — décor seulement ; le mur reste plein derrière.
        /// </summary>
        private static void ServiceDoor(InteriorPlan p, Vector3 at, float yaw, string leaf, string plate)
        {
            p.Push(at, yaw);
            p.Box("Chambranle", V(-0.47f, 1.06f, -0.02f), V(0.06f, 2.12f, 0.04f), "moulure");
            p.Box("Chambranle", V(0.47f, 1.06f, -0.02f), V(0.06f, 2.12f, 0.04f), "moulure");
            p.Box("Chambranle", V(0f, 2.12f, -0.02f), V(1.0f, 0.06f, 0.04f), "moulure");
            p.Box("Battant", V(0f, 1.04f, -0.015f), V(0.88f, 2.08f, 0.03f), leaf);
            p.Box("Panneau", V(0f, 1.45f, -0.032f), V(0.6f, 0.7f, 0.005f), leaf);
            p.Box("Panneau", V(0f, 0.55f, -0.032f), V(0.6f, 0.6f, 0.005f), leaf);
            p.Cylinder("Poignee", V(0.33f, 1.02f, -0.06f), 0.012f, 0.12f, "chrome", V(0f, 0f, 90f));
            p.Cylinder("Rosace", V(0.33f, 1.02f, -0.035f), 0.03f, 0.01f, "chrome", V(90f, 0f, 0f));
            if (plate != null) p.Box("Plaque", V(0f, 1.65f, -0.036f), V(0.22f, 0.08f, 0.006f), plate);
            p.Pop();
        }

        /// <summary>Un panneau suspendu par deux chaînettes (rayons d'un magasin), son rayon écrit des deux côtés.</summary>
        private static void HangingSign(InteriorPlan p, Vector3 at, float yaw, float w, string surface, float ceiling, string label)
        {
            p.Push(at, yaw);
            p.Box("Panneau", V(0f, 0f, 0f), V(w, 0.3f, 0.025f), surface);
            float letters = Mathf.Min(0.13f, (w - 0.15f) / Mathf.Max(3, label.Length) / 0.84f);
            p.Neon(label, V(0f, -letters * 0.5f, -0.016f), 0f, letters, "blanc");
            p.Neon(label, V(0f, -letters * 0.5f, 0.016f), 180f, letters, "blanc");
            float drop = ceiling - at.y - 0.15f;
            if (drop > 0.05f)
            {
                p.Cylinder("Chainette", V(-w * 0.4f, 0.15f + drop * 0.5f, 0f), 0.004f, drop, "metal");
                p.Cylinder("Chainette", V(w * 0.4f, 0.15f + drop * 0.5f, 0f), 0.004f, drop, "metal");
            }

            p.Pop();
        }

        /// <summary>Une fontaine à eau : bonbonne bleutée, robinets, gobelets.</summary>
        private static void WaterCooler(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Fontaine", V(0f, 0.5f, 0f), V(0.32f, 1.0f, 0.32f), "blanc", true);
            p.Cylinder("Bonbonne", V(0f, 1.2f, 0f), 0.14f, 0.4f, "verre");
            p.Cylinder("Eau", V(0f, 1.16f, 0f), 0.13f, 0.3f, "chromo");
            p.Box("Robinet", V(-0.06f, 0.82f, -0.17f), V(0.04f, 0.05f, 0.04f), "bleu");
            p.Box("Robinet", V(0.06f, 0.82f, -0.17f), V(0.04f, 0.05f, 0.04f), "rouge");
            p.Cylinder("Gobelets", V(0.2f, 0.75f, 0f), 0.04f, 0.3f, "blanc");
            p.Pop();
        }

        /// <summary>Un distributeur (boissons ou friandises) : vitrine éclairée, spirales, monnayeur.</summary>
        private static void VendingMachine(InteriorPlan p, Vector3 at, float yaw, bool snacks)
        {
            p.Push(at, yaw);
            string body = snacks ? "plastique_noir" : Pick(2) == 0 ? "rouge" : "bleu";
            p.Box("Distributeur", V(0f, 0.95f, 0f), V(0.9f, 1.9f, 0.8f), body, true);
            p.Box("Vitrine", V(-0.1f, 1.15f, -0.401f), V(0.62f, 1.3f, 0.004f), "frigo");
            for (int r = 0; r < 5; r++)
            {
                for (int c = 0; c < 5; c++)
                {
                    Vector3 o = V(-0.36f + c * 0.13f, 0.6f + r * 0.25f, -0.36f);
                    if (snacks) p.Box("Friandise", o, V(0.1f, 0.14f, 0.03f), Product(), V(-10f, 0f, 0f));
                    else p.Cylinder("Canette", o, 0.03f, 0.12f, Product());
                }
            }

            p.Box("Vitre", V(-0.1f, 1.15f, -0.41f), V(0.64f, 1.32f, 0.01f), "verre");
            p.Box("Monnayeur", V(0.33f, 1.2f, -0.405f), V(0.16f, 0.5f, 0.01f), "metal");
            p.Box("Ecran", V(0.33f, 1.38f, -0.412f), V(0.1f, 0.05f, 0.004f), "ecran_vert");
            p.Box("Trappe", V(-0.1f, 0.3f, -0.405f), V(0.5f, 0.18f, 0.02f), "noir");
            p.Pop();
        }

        /// <summary>Une caisse en bois à lattes (vide ou pas, peu importe : elle est fermée).</summary>
        private static void Crate(InteriorPlan p, Vector3 at, float yaw, Vector3 size)
        {
            p.Push(at, yaw);
            p.Box("Caisse", V(0f, size.y * 0.5f, 0f), size - V(0.02f, 0.02f, 0.02f), "bois_clair", true);
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Latte", V(0f, size.y * 0.5f + k * size.y * 0.3f, -size.z * 0.5f), V(size.x, size.y * 0.18f, 0.02f), "bois");
                p.Box("Latte", V(0f, size.y * 0.5f + k * size.y * 0.3f, size.z * 0.5f), V(size.x, size.y * 0.18f, 0.02f), "bois");
                p.Box("Montant", V(k * (size.x * 0.5f - 0.03f), size.y * 0.5f, -size.z * 0.5f - 0.005f), V(0.06f, size.y, 0.02f), "bois");
            }

            p.Pop();
        }

        private static void Pallet(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            for (int i = 0; i < 5; i++) p.Box("Planche", V(-0.5f + i * 0.25f, 0.13f, 0f), V(0.1f, 0.022f, 1.2f), "bois_clair");
            for (int i = -1; i <= 1; i++) p.Box("Chevron", V(0f, 0.06f, i * 0.52f), V(1.0f, 0.1f, 0.1f), "bois");
            p.Block("Palette", V(0f, 0.07f, 0f), V(1f, 0.14f, 1.2f));
            p.Pop();
        }

        /// <summary>Un portemanteau sur pied, deux vestes accrochées.</summary>
        private static void CoatRack(InteriorPlan p, Vector3 at)
        {
            p.Cylinder("Pied", at + V(0f, 0.9f, 0f), 0.02f, 1.8f, "bois_fonce");
            p.Cylinder("Socle", at + V(0f, 0.02f, 0f), 0.2f, 0.04f, "bois_fonce");
            for (int i = 0; i < 4; i++)
            {
                Vector3 o = Quaternion.Euler(0f, i * 90f, 0f) * V(0f, 0f, 0.12f);
                p.Cylinder("Patere", at + o + V(0f, 1.72f, 0f), 0.01f, 0.18f, "bois_fonce", V(55f, i * 90f, 0f));
            }

            for (int i = 0; i < 2; i++)
            {
                Vector3 o = Quaternion.Euler(0f, i * 180f + 20f, 0f) * V(0f, 0f, 0.14f);
                p.Box("Veste", at + o + V(0f, 1.35f, 0f), V(0.4f, 0.75f, 0.08f), i == 0 ? "cuir_noir" : "tissu", V(0f, i * 180f + 20f, 0f));
            }
        }
    }
}
