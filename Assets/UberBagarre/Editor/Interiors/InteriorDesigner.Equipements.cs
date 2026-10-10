using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// Les équipements propres à un métier : bornes d'arcade et machines à sous, roulette et
    /// tables de jeu, ring et sacs de frappe, machines à laver, fauteuils de barbier, table de
    /// tatoueur, voiture d'exposition, portants et mannequins, billard, platines de DJ…
    ///
    /// Même convention : longueur le long de X, face (côté utilisateur) vers −Z, posé au sol.
    /// </summary>
    public static partial class InteriorDesigner
    {
        // ================================================================== jeux

        /// <summary>Une borne d'arcade : caisson, pupitre incliné (stick, boutons), écran, fronton lumineux, monnayeur.</summary>
        private static void ArcadeCabinet(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            string side = Pick(3) == 0 ? "rouge" : Pick(2) == 0 ? "bleu" : "plastique_noir";
            string screen = Pick(3) == 0 ? "ecran_rose" : Pick(2) == 0 ? "ecran" : "ecran_vert";
            string marquee = Pick(3) == 0 ? "neon_rose" : Pick(2) == 0 ? "neon_cyan" : "neon_ambre";
            p.Block("Borne", V(0f, 0.95f, 0f), V(0.74f, 1.9f, 0.7f));
            p.Box("Caisson bas", V(0f, 0.45f, 0.02f), V(0.66f, 0.9f, 0.62f), "plastique_noir");
            p.Box("Pupitre", V(0f, 0.98f, -0.3f), V(0.7f, 0.1f, 0.32f), "plastique_noir", V(-12f, 0f, 0f));
            p.Box("Decor du pupitre", V(0f, 1.034f, -0.3f), V(0.66f, 0.004f, 0.28f), side, V(-12f, 0f, 0f));
            p.Cylinder("Manche", V(-0.18f, 1.1f, -0.32f), 0.008f, 0.1f, "metal");
            p.Sphere("Boule", V(-0.18f, 1.16f, -0.32f), V(0.04f, 0.04f, 0.04f), "rouge");
            for (int i = 0; i < 6; i++)
            {
                Vector3 b = V(0.02f + (i % 3) * 0.07f, 1.05f + (i / 3) * 0.012f, -0.35f + (i / 3) * 0.06f);
                p.Cylinder("Bouton", b, 0.016f, 0.02f, i % 2 == 0 ? "jaune" : "rouge", V(-12f, 0f, 0f));
            }

            p.Box("Caisson haut", V(0f, 1.4f, 0.06f), V(0.66f, 0.72f, 0.54f), "plastique_noir");
            p.Box("Ecran", V(0f, 1.38f, -0.21f), V(0.56f, 0.46f, 0.02f), screen, V(-12f, 0f, 0f));
            p.Box("Fronton", V(0f, 1.86f, -0.08f), V(0.7f, 0.22f, 0.24f), marquee);
            p.Box("Chapeau", V(0f, 1.98f, 0.04f), V(0.72f, 0.04f, 0.56f), "plastique_noir");
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Flanc", V(k * 0.35f, 1.0f, 0.02f), V(0.03f, 2.0f, 0.66f), side);
                p.Box("Liseré", V(k * 0.367f, 1.0f, -0.1f), V(0.004f, 1.6f, 0.04f), marquee);
            }

            p.Box("Porte du monnayeur", V(0f, 0.55f, -0.295f), V(0.24f, 0.32f, 0.01f), "metal");
            p.Box("Fente", V(-0.05f, 0.62f, -0.302f), V(0.03f, 0.05f, 0.005f), "feu_rouge");
            p.Box("Fente", V(0.05f, 0.62f, -0.302f), V(0.03f, 0.05f, 0.005f), "feu_rouge");
            p.Pop();
        }

        /// <summary>Une machine à sous : rouleaux derrière la vitre, écran, fronton lumineux, levier, boutons, tabouret.</summary>
        private static void SlotMachine(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            string top = Pick(3) == 0 ? "neon_rose" : Pick(2) == 0 ? "neon_ambre" : "neon_cyan";
            p.Block("Machine a sous", V(0f, 0.9f, 0f), V(0.66f, 1.8f, 0.6f));
            p.Box("Socle", V(0f, 0.38f, 0.02f), V(0.62f, 0.76f, 0.56f), "plastique_noir");
            p.Box("Plinthe", V(0f, 0.03f, -0.255f), V(0.6f, 0.06f, 0.01f), "chrome");
            p.Box("Pupitre", V(0f, 0.8f, -0.24f), V(0.62f, 0.07f, 0.24f), "chrome", V(-10f, 0f, 0f));
            for (int i = 0; i < 4; i++)
            {
                p.Box("Bouton", V(-0.2f + i * 0.13f, 0.84f, -0.28f), V(0.08f, 0.02f, 0.05f), i == 3 ? "neon_rouge" : "neon_ambre", V(-10f, 0f, 0f));
            }

            p.Box("Corps", V(0f, 1.25f, 0.04f), V(0.62f, 0.86f, 0.5f), "plastique_noir");
            p.Box("Fenetre des rouleaux", V(0f, 1.1f, -0.212f), V(0.48f, 0.22f, 0.01f), "blanc");
            for (int r = 0; r < 3; r++)
            {
                float x = -0.15f + r * 0.15f;
                p.Box("Symbole", V(x, 1.1f, -0.22f), V(0.07f, 0.07f, 0.004f), r == Pick(3) ? "rouge" : Pick(2) == 0 ? "jaune" : "vert");
            }

            p.Box("Vitre", V(0f, 1.1f, -0.222f), V(0.5f, 0.24f, 0.006f), "verre");
            p.Box("Ecran", V(0f, 1.42f, -0.212f), V(0.5f, 0.3f, 0.01f), "ecran");
            p.Box("Fronton", V(0f, 1.8f, -0.06f), V(0.64f, 0.26f, 0.36f), top);
            p.Box("Cadre", V(0f, 1.26f, -0.21f), V(0.62f, 0.86f, 0.004f), "chrome");
            p.Cylinder("Levier", V(0.36f, 1.18f, 0.02f), 0.012f, 0.4f, "chrome");
            p.Sphere("Pommeau", V(0.36f, 1.4f, 0.02f), V(0.07f, 0.07f, 0.07f), "rouge");
            Stool(p, V(0f, 0f, -0.62f), "cuir_rouge", 0.62f);
            p.Pop();
        }

        /// <summary>La table de roulette : tapis numéroté, bord rembourré, cylindre en bois et laiton, jetons.</summary>
        private static void RouletteTable(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            const float w = 2.6f, d = 1.25f, y = 0.82f;
            p.Box("Pietement", V(0f, 0.38f, 0f), V(w - 0.4f, 0.76f, d - 0.3f), "bois_fonce", true);
            p.Box("Tapis", V(0f, y, 0f), V(w, 0.04f, d), "feutre");
            p.Box("Bord", V(0f, y + 0.02f, -d * 0.5f), V(w + 0.1f, 0.07f, 0.1f), "cuir_noir");
            p.Box("Bord", V(0f, y + 0.02f, d * 0.5f), V(w + 0.1f, 0.07f, 0.1f), "cuir_noir");
            p.Box("Bord", V(-w * 0.5f, y + 0.02f, 0f), V(0.1f, 0.07f, d + 0.1f), "cuir_noir");
            p.Box("Bord", V(w * 0.5f, y + 0.02f, 0f), V(0.1f, 0.07f, d + 0.1f), "cuir_noir");

            // Le tableau des mises : douze colonnes de trois cases, rouges et noires, le zéro vert.
            for (int c = 0; c < 12; c++)
            {
                for (int r = 0; r < 3; r++)
                {
                    int n = c * 3 + r;
                    p.Box("Case", V(-0.25f + c * 0.11f, y + 0.022f, -0.2f + r * 0.13f), V(0.1f, 0.003f, 0.12f), n % 2 == 0 ? "rouge" : "noir");
                }
            }

            p.Box("Zero", V(-0.36f, y + 0.022f, -0.07f), V(0.1f, 0.003f, 0.38f), "vert");
            p.Box("Douzaines", V(0.355f, y + 0.022f, -0.36f), V(1.3f, 0.003f, 0.12f), "blanc");

            // Le cylindre.
            Vector3 wheel = V(-0.85f, y + 0.02f, 0f);
            p.Cylinder("Cuvette", wheel + V(0f, 0.06f, 0f), 0.4f, 0.12f, "bois_fonce");
            p.Cylinder("Plateau tournant", wheel + V(0f, 0.125f, 0f), 0.3f, 0.02f, "rouge");
            p.Cylinder("Moyeu", wheel + V(0f, 0.15f, 0f), 0.08f, 0.04f, "laiton");
            for (int i = 0; i < 4; i++) p.Box("Croisillon", wheel + V(0f, 0.2f, 0f), V(0.22f, 0.015f, 0.015f), "laiton", V(0f, i * 45f, 0f));
            p.Sphere("Bille", wheel + V(0.2f, 0.14f, 0.1f), V(0.02f, 0.02f, 0.02f), "blanc");
            Chips(p, V(0.6f, y + 0.02f, 0.4f), 5);
            Chips(p, V(0.1f, y + 0.02f, -0.1f), 2);
            p.Pop();
        }

        /// <summary>Des piles de jetons de couleur.</summary>
        private static void Chips(InteriorPlan p, Vector3 at, int stacks)
        {
            for (int i = 0; i < stacks; i++)
            {
                float h = Rand(0.01f, 0.07f);
                p.Cylinder("Jetons", at + V(i * 0.045f, h * 0.5f, Rand(-0.02f, 0.02f)), 0.02f, h, Pick(3) == 0 ? "rouge" : Pick(2) == 0 ? "bleu" : "noir");
            }
        }

        /// <summary>Une table de cartes ovale (poker, black-jack) : tapis, bord en cuir, cartes, jetons, chaises.</summary>
        private static void CardTable(InteriorPlan p, Vector3 at, float yaw, int seats)
        {
            p.Push(at, yaw);
            const float y = 0.78f;
            p.Oval("Bord", V(0f, y - 0.01f, 0f), V(2.3f, 0.08f, 1.3f), "cuir_noir");
            p.Oval("Tapis", V(0f, y + 0.012f, 0f), V(2.1f, 0.04f, 1.1f), "feutre");
            p.Cylinder("Pied", V(0f, 0.36f, 0f), 0.2f, 0.72f, "bois_fonce");
            p.Oval("Socle", V(0f, 0.02f, 0f), V(1.1f, 0.04f, 0.6f), "bois_fonce");
            p.Block("Table de cartes", V(0f, 0.4f, 0f), V(2.2f, 0.8f, 1.2f));
            p.Box("Rack du croupier", V(0f, y + 0.045f, 0.42f), V(0.5f, 0.03f, 0.12f), "noir");
            Chips(p, V(-0.2f, y + 0.032f, 0.42f), 9);
            for (int i = 0; i < seats; i++)
            {
                float a = 110f + i * (140f / Mathf.Max(1, seats - 1));
                Vector3 c = Quaternion.Euler(0f, a, 0f) * V(0f, 0f, 1f);
                c = V(c.x * 1.3f, 0f, c.z * 0.85f);
                Vector3 card = c * 0.62f + V(0f, y + 0.034f, 0f);
                p.Box("Carte", card, V(0.06f, 0.002f, 0.09f), "blanc", V(0f, a + Rand(-10f, 10f), 0f));
                p.Box("Carte", card + V(0.03f, 0.002f, 0.01f), V(0.06f, 0.002f, 0.09f), "blanc", V(0f, a + Rand(-10f, 10f), 0f));
                Chips(p, c * 0.5f + V(0f, y + 0.032f, 0f), 2);
                Chair(p, c * 1.25f, Facing(c * 1.25f, Vector3.zero), "cuir_rouge", "laiton", 2);
            }

            p.Pop();
        }

        /// <summary>Un billard : plateau de feutre, bandes, poches, pieds massifs, queues et boules.</summary>
        private static void PoolTable(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            const float w = 1.3f, d = 2.4f, y = 0.8f;
            p.Box("Caisse", V(0f, y - 0.12f, 0f), V(w + 0.1f, 0.2f, d + 0.1f), "bois_fonce");
            p.Box("Tapis", V(0f, y, 0f), V(w, 0.03f, d), "feutre");
            p.Box("Bande", V(-w * 0.5f - 0.05f, y + 0.03f, 0f), V(0.12f, 0.06f, d + 0.2f), "bois_fonce");
            p.Box("Bande", V(w * 0.5f + 0.05f, y + 0.03f, 0f), V(0.12f, 0.06f, d + 0.2f), "bois_fonce");
            p.Box("Bande", V(0f, y + 0.03f, -d * 0.5f - 0.05f), V(w + 0.2f, 0.06f, 0.12f), "bois_fonce");
            p.Box("Bande", V(0f, y + 0.03f, d * 0.5f + 0.05f), V(w + 0.2f, 0.06f, 0.12f), "bois_fonce");
            for (int i = 0; i < 6; i++)
            {
                float x = i % 2 == 0 ? -w * 0.5f : w * 0.5f;
                float z = (i / 2 - 1) * d * 0.5f;
                p.Cylinder("Poche", V(x, y + 0.02f, z), 0.06f, 0.05f, "noir");
            }

            for (int i = 0; i < 4; i++)
            {
                p.Box("Pied", V(i % 2 == 0 ? -w * 0.4f : w * 0.4f, (y - 0.2f) * 0.5f, i < 2 ? -d * 0.4f : d * 0.4f), V(0.16f, y - 0.2f, 0.16f), "bois_fonce");
            }

            p.Block("Billard", V(0f, y * 0.5f, 0f), V(w + 0.2f, y + 0.05f, d + 0.2f));
            for (int i = 0; i < 9; i++)
            {
                Vector3 b = V(Rand(-w * 0.4f, w * 0.4f), y + 0.045f, Rand(-d * 0.4f, d * 0.4f));
                p.Sphere("Boule", b, V(0.057f, 0.057f, 0.057f), i == 0 ? "blanc" : i == 8 ? "noir" : Product());
            }

            p.Cylinder("Queue", V(0.3f, y + 0.04f, 0.2f), 0.012f, 1.45f, "bois_clair", V(90f, Rand(-30f, 30f), 0f));
            p.Pop();
        }

        /// <summary>Une cible de fléchettes au mur (le dos en +Z), trois fléchettes plantées.</summary>
        private static void Dartboard(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Panneau", V(0f, 0f, -0.01f), V(0.7f, 0.7f, 0.02f), "bois_fonce");
            p.Cylinder("Cible", V(0f, 0f, -0.04f), 0.23f, 0.04f, "noir", V(90f, 0f, 0f));
            p.Cylinder("Anneau", V(0f, 0f, -0.061f), 0.17f, 0.004f, "rouge", V(90f, 0f, 0f));
            p.Cylinder("Anneau", V(0f, 0f, -0.063f), 0.14f, 0.004f, "bois_clair", V(90f, 0f, 0f));
            p.Cylinder("Mouche", V(0f, 0f, -0.066f), 0.02f, 0.006f, "rouge", V(90f, 0f, 0f));
            for (int i = 0; i < 3; i++)
            {
                Vector3 o = V(Rand(-0.12f, 0.12f), Rand(-0.12f, 0.12f), -0.1f);
                p.Cylinder("Flechette", o, 0.004f, 0.1f, "metal", V(90f, 0f, 0f));
                p.Box("Empennage", o + V(0f, 0f, -0.05f), V(0.025f, 0.025f, 0.02f), Product());
            }

            p.Pop();
        }

        /// <summary>Un juke-box : caisson arrondi, vitre de disques, néons sur les flancs.</summary>
        private static void Jukebox(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Juke-box", V(0f, 0.65f, 0f), V(0.85f, 1.3f, 0.6f), "bois_fonce", true);
            p.Oval("Arche", V(0f, 1.3f, 0f), V(0.85f, 0.6f, 0.6f), "bois_fonce", V(90f, 0f, 0f));
            p.Oval("Arche lumineuse", V(0f, 1.3f, -0.005f), V(0.7f, 0.6f, 0.45f), "neon_ambre", V(90f, 0f, 0f));
            p.Box("Vitre des disques", V(0f, 1.0f, -0.305f), V(0.6f, 0.4f, 0.01f), "vitre_cuisine");
            p.Box("Grille du haut-parleur", V(0f, 0.45f, -0.305f), V(0.6f, 0.35f, 0.01f), "laiton");
            p.Box("Neon", V(-0.4f, 0.75f, -0.29f), V(0.04f, 1.1f, 0.04f), "neon_rose");
            p.Box("Neon", V(0.4f, 0.75f, -0.29f), V(0.04f, 1.1f, 0.04f), "neon_rose");
            p.Pop();
        }

        /// <summary>Une machine à pince : vitrine sur caisson, peluches entassées, pince suspendue.</summary>
        private static void ClawMachine(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            string body = Pick(2) == 0 ? "rose" : "bleu";
            p.Block("Machine a pince", V(0f, 0.95f, 0f), V(0.9f, 1.9f, 0.9f));
            p.Box("Caisson", V(0f, 0.42f, 0f), V(0.9f, 0.84f, 0.9f), body);
            p.Box("Pupitre", V(0f, 0.9f, -0.5f), V(0.8f, 0.08f, 0.2f), "plastique_noir");
            p.Cylinder("Manette", V(-0.15f, 0.99f, -0.5f), 0.01f, 0.1f, "metal");
            p.Sphere("Boule", V(-0.15f, 1.05f, -0.5f), V(0.04f, 0.04f, 0.04f), "rouge");
            p.Cylinder("Bouton", V(0.15f, 0.95f, -0.5f), 0.03f, 0.03f, "neon_rouge");
            for (int i = 0; i < 4; i++)
            {
                p.Box("Montant", V(i % 2 == 0 ? -0.43f : 0.43f, 1.3f, i < 2 ? -0.43f : 0.43f), V(0.04f, 0.92f, 0.04f), body);
            }

            p.Box("Vitre", V(0f, 1.3f, -0.44f), V(0.84f, 0.88f, 0.01f), "verre");
            p.Box("Vitre", V(-0.44f, 1.3f, 0f), V(0.01f, 0.88f, 0.84f), "verre");
            p.Box("Vitre", V(0.44f, 1.3f, 0f), V(0.01f, 0.88f, 0.84f), "verre");
            p.Box("Fond", V(0f, 1.3f, 0.44f), V(0.84f, 0.88f, 0.01f), "miroir");
            p.Box("Toit", V(0f, 1.82f, 0f), V(0.92f, 0.16f, 0.92f), body);
            p.Box("Enseigne", V(0f, 1.82f, -0.465f), V(0.7f, 0.1f, 0.01f), "neon_cyan");
            for (int i = 0; i < 14; i++)
            {
                float s = Rand(0.14f, 0.2f);
                p.Sphere("Peluche", V(Rand(-0.32f, 0.32f), 0.9f + Rand(0f, 0.12f), Rand(-0.32f, 0.32f)), V(s, s * 0.9f, s), Product());
            }

            p.Cylinder("Cable", V(0.1f, 1.6f, 0.05f), 0.004f, 0.3f, "metal");
            for (int i = 0; i < 3; i++)
            {
                p.Box("Griffe", V(0.1f, 1.4f, 0.05f) + Quaternion.Euler(0f, i * 120f, 0f) * V(0f, 0f, 0.04f), V(0.012f, 0.12f, 0.012f), "chrome",
                    V(20f, i * 120f, 0f));
            }

            p.Light(V(0f, 1.6f, 0f), new Color(1f, 0.6f, 0.9f), 0.5f, 2f);
            p.Pop();
        }

        /// <summary>Un air hockey : plateau lumineux, bandes, buts, palets.</summary>
        private static void AirHockey(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            const float w = 1.1f, d = 2.1f, y = 0.8f;
            p.Box("Caisson", V(0f, y * 0.5f, 0f), V(w, y, d), "plastique_noir", true);
            p.Box("Plateau", V(0f, y + 0.01f, 0f), V(w - 0.1f, 0.02f, d - 0.1f), "blanc");
            p.Box("Ligne", V(0f, y + 0.022f, 0f), V(w - 0.1f, 0.003f, 0.02f), "rouge");
            p.Cylinder("Rond central", V(0f, y + 0.022f, 0f), 0.18f, 0.002f, "bleu");
            p.Box("Bande", V(-w * 0.5f, y + 0.05f, 0f), V(0.05f, 0.1f, d), "neon_cyan");
            p.Box("Bande", V(w * 0.5f, y + 0.05f, 0f), V(0.05f, 0.1f, d), "neon_cyan");
            p.Box("Bande", V(0f, y + 0.05f, -d * 0.5f), V(w, 0.1f, 0.05f), "neon_rose");
            p.Box("Bande", V(0f, y + 0.05f, d * 0.5f), V(w, 0.1f, 0.05f), "neon_rose");
            p.Cylinder("Palet", V(Rand(-0.3f, 0.3f), y + 0.03f, Rand(-0.5f, 0.5f)), 0.04f, 0.01f, "rouge");
            p.Cylinder("Poussoir", V(0f, y + 0.05f, -0.8f), 0.05f, 0.05f, "rouge");
            p.Cylinder("Poussoir", V(0.1f, y + 0.05f, 0.85f), 0.05f, 0.05f, "bleu");
            p.Pop();
        }

        /// <summary>Un flipper : caisse inclinée sur pieds, plateau lumineux, fronton, monnayeur.</summary>
        private static void Pinball(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Block("Flipper", V(0f, 0.9f, 0.1f), V(0.75f, 1.8f, 1.4f));
            p.Box("Caisse", V(0f, 0.9f, 0.05f), V(0.7f, 0.22f, 1.25f), Product(), V(-6f, 0f, 0f));
            p.Box("Plateau", V(0f, 1.015f, 0.05f), V(0.64f, 0.01f, 1.18f), "ecran_ambre", V(-6f, 0f, 0f));
            p.Box("Vitre", V(0f, 1.03f, 0.05f), V(0.66f, 0.006f, 1.2f), "verre", V(-6f, 0f, 0f));
            p.Box("Fronton", V(0f, 1.42f, 0.66f), V(0.7f, 0.7f, 0.15f), "plastique_noir");
            p.Box("Fronton lumineux", V(0f, 1.45f, 0.58f), V(0.62f, 0.5f, 0.01f), Pick(2) == 0 ? "ecran_rose" : "ecran");
            for (int i = 0; i < 4; i++)
            {
                p.Box("Pied", V(i % 2 == 0 ? -0.3f : 0.3f, 0.4f, i < 2 ? -0.5f : 0.55f), V(0.05f, 0.8f, 0.05f), "chrome");
            }

            p.Box("Monnayeur", V(0f, 0.65f, -0.57f), V(0.2f, 0.18f, 0.01f), "metal");
            p.Pop();
        }

        // ================================================================== sport

        /// <summary>Un ring : estrade, tablier, toile, quatre poteaux à coussins, quatre cordes, marchepied.</summary>
        private static void BoxingRing(InteriorPlan p, Vector3 at, float half)
        {
            p.Push(at, 0f);
            const float deck = 0.9f;
            float size = half * 2f;
            p.Box("Estrade", V(0f, deck * 0.5f, 0f), V(size + 0.5f, deck, size + 0.5f), "noir", true);
            p.Box("Tablier", V(0f, deck * 0.5f, -size * 0.5f - 0.255f), V(size + 0.5f, deck - 0.1f, 0.01f), "rouge");
            p.Box("Tablier", V(0f, deck * 0.5f, size * 0.5f + 0.255f), V(size + 0.5f, deck - 0.1f, 0.01f), "rouge");
            p.Box("Tablier", V(-size * 0.5f - 0.255f, deck * 0.5f, 0f), V(0.01f, deck - 0.1f, size + 0.5f), "bleu");
            p.Box("Tablier", V(size * 0.5f + 0.255f, deck * 0.5f, 0f), V(0.01f, deck - 0.1f, size + 0.5f), "bleu");
            p.Box("Toile", V(0f, deck + 0.02f, 0f), V(size + 0.4f, 0.04f, size + 0.4f), "tissu");
            p.Box("Logo", V(0f, deck + 0.042f, 0f), V(1.6f, 0.004f, 1.6f), "rouge", V(0f, 45f, 0f));
            string[] pads = { "rouge", "blanc", "bleu", "blanc" };
            for (int i = 0; i < 4; i++)
            {
                float x = (i == 0 || i == 3 ? -1f : 1f) * half, z = (i < 2 ? -1f : 1f) * half;
                p.Cylinder("Poteau", V(x, deck + 0.7f, z), 0.05f, 1.4f, "metal_noir");
                p.Box("Coussin de coin", V(x * 0.97f, deck + 0.85f, z * 0.97f), V(0.2f, 0.9f, 0.2f), pads[i], V(0f, 45f, 0f));
            }

            string[] ropes = { "rouge", "blanc", "bleu", "blanc" };
            for (int k = 0; k < 4; k++)
            {
                float y = deck + 0.4f + k * 0.32f;
                p.Cylinder("Corde", V(0f, y, -half), 0.02f, size, ropes[k], V(0f, 0f, 90f));
                p.Cylinder("Corde", V(0f, y, half), 0.02f, size, ropes[k], V(0f, 0f, 90f));
                p.Cylinder("Corde", V(-half, y, 0f), 0.02f, size, ropes[k], V(90f, 0f, 0f));
                p.Cylinder("Corde", V(half, y, 0f), 0.02f, size, ropes[k], V(90f, 0f, 0f));
            }

            p.Block("Cordes", V(0f, deck + 0.75f, -half), V(size, 1.5f, 0.1f));
            p.Block("Cordes", V(0f, deck + 0.75f, half), V(size, 1.5f, 0.1f));
            p.Block("Cordes", V(-half, deck + 0.75f, 0f), V(0.1f, 1.5f, size));
            p.Block("Cordes", V(half, deck + 0.75f, 0f), V(0.1f, 1.5f, size));
            for (int s = 0; s < 3; s++)
            {
                p.Box("Marche", V(-half - 0.6f - s * 0.28f, deck * (3 - s) / 4f * 0.5f, -half + 0.3f),
                    V(0.3f, deck * (3 - s) / 4f, 0.7f), "metal", true);
            }

            p.Pop();
        }

        /// <summary>Un sac de frappe pendu au plafond (chaîne, émerillon, sangles).</summary>
        private static void PunchingBag(InteriorPlan p, Vector3 at, float ceiling, string leather)
        {
            const float bagTop = 1.75f, bagH = 1.05f;
            float chain = ceiling - bagTop - 0.2f;
            p.Cylinder("Chaine", at + V(0f, bagTop + 0.2f + chain * 0.5f, 0f), 0.008f, chain, "metal");
            p.Box("Fixation", at + V(0f, ceiling - 0.03f, 0f), V(0.2f, 0.06f, 0.2f), "metal_noir");
            p.Sphere("Emerillon", at + V(0f, bagTop + 0.18f, 0f), V(0.05f, 0.06f, 0.05f), "chrome");
            for (int i = 0; i < 4; i++)
            {
                Vector3 o = Quaternion.Euler(0f, i * 90f + 45f, 0f) * V(0f, 0f, 0.09f);
                p.Cylinder("Chainette", at + o * 0.5f + V(0f, bagTop + 0.08f, 0f), 0.005f, 0.2f, "metal",
                    V(i % 2 == 0 ? 25f : -25f, i * 90f + 45f, 0f));
            }

            p.Cylinder("Sac de frappe", at + V(0f, bagTop - bagH * 0.5f, 0f), 0.18f, bagH, leather);
            p.Cylinder("Couvercle", at + V(0f, bagTop, 0f), 0.17f, 0.04f, "cuir_noir");
            p.Cylinder("Fond", at + V(0f, bagTop - bagH, 0f), 0.17f, 0.04f, "cuir_noir");
            p.Cylinder("Bande", at + V(0f, bagTop - bagH * 0.35f, 0f), 0.183f, 0.1f, "blanc");
            p.Block("Sac de frappe", at + V(0f, bagTop - bagH * 0.5f, 0f), V(0.36f, bagH, 0.36f));
        }

        /// <summary>Un râtelier d'haltères : deux étages, paires d'haltères de tailles croissantes.</summary>
        private static void DumbbellRack(InteriorPlan p, Vector3 at, float yaw, float length)
        {
            p.Push(at, yaw);
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Montant", V(k * (length * 0.5f - 0.05f), 0.4f, 0f), V(0.06f, 0.8f, 0.5f), "metal_noir");
            }

            for (int t = 0; t < 2; t++)
            {
                float y = 0.35f + t * 0.35f;
                float z = t == 0 ? -0.1f : 0.1f;
                p.Box("Tablette", V(0f, y, z), V(length, 0.04f, 0.25f), "metal_noir", V(-10f, 0f, 0f));
                int n = Mathf.FloorToInt((length - 0.2f) / 0.32f);
                for (int i = 0; i < n; i++)
                {
                    float size = 0.08f + (i + t * n) * 0.006f;
                    float x = -length * 0.5f + 0.25f + i * 0.32f;
                    p.Cylinder("Poignee", V(x, y + 0.02f + size * 0.5f, z), 0.016f, 0.3f, "chrome", V(90f, 0f, 0f));
                    p.Cylinder("Tete", V(x, y + 0.02f + size * 0.5f, z - 0.13f), size * 0.55f, 0.08f, "caoutchouc", V(90f, 0f, 0f));
                    p.Cylinder("Tete", V(x, y + 0.02f + size * 0.5f, z + 0.13f), size * 0.55f, 0.08f, "caoutchouc", V(90f, 0f, 0f));
                }
            }

            p.Block("Ratelier", V(0f, 0.4f, 0f), V(length, 0.8f, 0.55f));
            p.Pop();
        }

        /// <summary>Un banc de développé couché : banc, montants, barre chargée de disques.</summary>
        private static void BenchPress(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Banc", V(0f, 0.44f, 0f), V(0.3f, 0.08f, 1.2f), "cuir_noir");
            p.Box("Pied", V(0f, 0.2f, -0.5f), V(0.06f, 0.4f, 0.4f), "metal_noir");
            p.Box("Pied", V(0f, 0.2f, 0.45f), V(0.06f, 0.4f, 0.3f), "metal_noir");
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Montant", V(k * 0.55f, 0.6f, 0.5f), V(0.06f, 1.2f, 0.06f), "metal_noir");
                p.Box("Pied du montant", V(k * 0.55f, 0.03f, 0.4f), V(0.08f, 0.06f, 0.5f), "metal_noir");
                for (int d = 0; d < 2; d++)
                {
                    p.Cylinder("Disque", V(k * (0.75f + d * 0.05f), 1.1f, 0.5f), 0.22f - d * 0.04f, 0.04f, "caoutchouc", V(0f, 0f, 90f));
                }
            }

            p.Cylinder("Barre", V(0f, 1.1f, 0.5f), 0.014f, 2.0f, "chrome", V(0f, 0f, 90f));
            p.Block("Banc de muscu", V(0f, 0.5f, 0.2f), V(1.2f, 1.2f, 1.4f));
            p.Pop();
        }

        /// <summary>Un tapis de sol de salle (mousse), à plat.</summary>
        private static void Mat(InteriorPlan p, Vector3 at, float yaw, float w, float d, string surface)
        {
            p.Box("Tapis de sol", at + V(0f, 0.02f, 0f), V(w, 0.04f, d), surface, V(0f, yaw, 0f));
        }

        /// <summary>Une rangée de casiers métalliques : portes à ouïes, poignées, numéros, banc devant.</summary>
        private static void Lockers(InteriorPlan p, Vector3 at, float yaw, int count, bool bench)
        {
            p.Push(at, yaw);
            float length = count * 0.38f;
            string color = Pick(2) == 0 ? "bleu" : "gris";
            p.Box("Casiers", V(0f, 0.95f, 0f), V(length, 1.9f, 0.45f), color, true);
            for (int i = 0; i < count; i++)
            {
                float x = -length * 0.5f + 0.19f + i * 0.38f;
                p.Box("Porte", V(x, 0.95f, -0.227f), V(0.35f, 1.84f, 0.006f), color);
                for (int v = 0; v < 4; v++) p.Box("Ouie", V(x, 1.65f - v * 0.04f, -0.232f), V(0.2f, 0.012f, 0.004f), "noir");
                p.Box("Poignee", V(x + 0.12f, 1.0f, -0.236f), V(0.03f, 0.12f, 0.02f), "chrome");
                p.Box("Numero", V(x, 1.45f, -0.232f), V(0.06f, 0.04f, 0.003f), "blanc");
                if (Pick(3) == 0) p.Box("Cadenas", V(x + 0.12f, 0.92f, -0.25f), V(0.035f, 0.045f, 0.015f), "laiton");
            }

            if (bench)
            {
                p.Box("Banc", V(0f, 0.43f, -0.75f), V(length * 0.8f, 0.05f, 0.3f), "bois_clair");
                p.Box("Pied", V(-length * 0.35f, 0.21f, -0.75f), V(0.05f, 0.42f, 0.25f), "metal_noir");
                p.Box("Pied", V(length * 0.35f, 0.21f, -0.75f), V(0.05f, 0.42f, 0.25f), "metal_noir");
                p.Block("Banc", V(0f, 0.22f, -0.75f), V(length * 0.8f, 0.45f, 0.3f));
                if (Pick(2) == 0) p.Box("Serviette", V(Rand(-0.4f, 0.4f), 0.47f, -0.75f), V(0.3f, 0.03f, 0.25f), "blanc", V(0f, Rand(-30f, 30f), 0f));
            }

            p.Pop();
        }

        // ================================================================== laverie

        /// <summary>Une machine à laver (ou un sèche-linge à grand hublot) de laverie, en inox.</summary>
        private static void Washer(InteriorPlan p, Vector3 at, float yaw, bool dryer, float baseY = 0f)
        {
            p.Push(at + V(0f, baseY, 0f), yaw);
            float h = dryer ? 0.78f : 0.92f;
            p.Box(dryer ? "Seche-linge" : "Machine a laver", V(0f, h * 0.5f, 0f), V(0.68f, h, 0.66f), dryer ? "blanc" : "inox", true);
            p.Box("Pupitre", V(0f, h - 0.08f, -0.335f), V(0.66f, 0.14f, 0.012f), "plastique_noir");
            p.Box("Afficheur", V(-0.18f, h - 0.08f, -0.342f), V(0.12f, 0.05f, 0.004f), "ecran_vert");
            p.Cylinder("Bouton", V(0.05f, h - 0.08f, -0.35f), 0.025f, 0.02f, "chrome", V(90f, 0f, 0f));
            p.Box("Monnayeur", V(0.22f, h - 0.08f, -0.345f), V(0.1f, 0.1f, 0.01f), "metal");
            float r = dryer ? 0.26f : 0.2f;
            float cy = dryer ? h * 0.42f : h * 0.42f;
            p.Cylinder("Hublot", V(0f, cy, -0.345f), r, 0.04f, "chrome", V(90f, 0f, 0f));
            p.Cylinder("Vitre du hublot", V(0f, cy, -0.36f), r - 0.04f, 0.02f, "verre_fume", V(90f, 0f, 0f));
            if (Pick(2) == 0) p.Sphere("Linge", V(0f, cy - r * 0.35f, -0.2f), V(r * 1.2f, r * 0.8f, 0.3f), Product());
            p.Box("Poignee", V(r + 0.02f, cy, -0.37f), V(0.03f, 0.1f, 0.03f), "plastique_noir");
            p.Pop();
        }

        /// <summary>Un chariot de laverie : cadre chromé, panier de toile, roulettes, tringle.</summary>
        private static void LaundryCart(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Panier", V(0f, 0.6f, 0f), V(0.6f, 0.45f, 0.45f), Pick(2) == 0 ? "tissu" : "bleu");
            for (int i = 0; i < 4; i++)
            {
                float x = i % 2 == 0 ? -0.3f : 0.3f, z = i < 2 ? -0.22f : 0.22f;
                p.Cylinder("Montant", V(x, 0.45f, z), 0.012f, 0.8f, "chrome");
                p.Sphere("Roulette", V(x, 0.03f, z), V(0.06f, 0.06f, 0.04f), "noir");
            }

            p.Cylinder("Tringle", V(0f, 1.5f, 0.22f), 0.012f, 0.6f, "chrome", V(0f, 0f, 90f));
            p.Cylinder("Mat", V(-0.3f, 1.15f, 0.22f), 0.012f, 0.7f, "chrome");
            p.Cylinder("Mat", V(0.3f, 1.15f, 0.22f), 0.012f, 0.7f, "chrome");
            p.Block("Chariot", V(0f, 0.45f, 0f), V(0.62f, 0.9f, 0.47f));
            p.Pop();
        }

        // ================================================================== coiffure, tatouage

        /// <summary>Un fauteuil de barbier : pied chromé, assise et dossier en cuir, appuie-tête, repose-pieds.</summary>
        private static void BarberChair(InteriorPlan p, Vector3 at, float yaw, string leather)
        {
            p.Push(at, yaw);
            p.Cylinder("Socle", V(0f, 0.03f, 0f), 0.3f, 0.06f, "chrome");
            p.Cylinder("Verin", V(0f, 0.25f, 0f), 0.08f, 0.4f, "chrome");
            p.Box("Assise", V(0f, 0.52f, 0f), V(0.56f, 0.14f, 0.55f), leather);
            p.Box("Dossier", V(0f, 0.92f, 0.3f), V(0.52f, 0.72f, 0.12f), leather, V(-10f, 0f, 0f));
            p.Box("Appuie-tete", V(0f, 1.38f, 0.38f), V(0.3f, 0.16f, 0.1f), leather, V(-10f, 0f, 0f));
            p.Cylinder("Tige", V(0f, 1.25f, 0.4f), 0.012f, 0.12f, "chrome");
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Accoudoir", V(k * 0.33f, 0.74f, -0.02f), V(0.1f, 0.06f, 0.52f), leather);
                p.Box("Bras chrome", V(k * 0.33f, 0.64f, 0.05f), V(0.05f, 0.18f, 0.35f), "chrome");
            }

            p.Box("Repose-pieds", V(0f, 0.22f, -0.42f), V(0.4f, 0.03f, 0.16f), "chrome", V(15f, 0f, 0f));
            p.Box("Lien", V(0f, 0.36f, -0.3f), V(0.06f, 0.06f, 0.3f), "chrome", V(-40f, 0f, 0f));
            p.Block("Fauteuil", V(0f, 0.6f, 0f), V(0.7f, 1.2f, 0.7f));
            p.Pop();
        }

        /// <summary>
        /// Un poste de coiffeur au mur (le dos en +Z) : grand miroir encadré, tablette garnie
        /// (bouteilles, tondeuse, pot de peignes bleu), séchoir accroché, fauteuil devant.
        /// </summary>
        private static void BarberStation(InteriorPlan p, Vector3 at, float yaw, string leather)
        {
            p.Push(at, yaw);
            p.Box("Cadre du miroir", V(0f, 1.55f, -0.02f), V(0.9f, 1.1f, 0.04f), "bois_fonce");
            p.Box("Miroir", V(0f, 1.55f, -0.042f), V(0.8f, 1.0f, 0.004f), "miroir");
            p.Box("Applique", V(0f, 2.18f, -0.06f), V(0.7f, 0.06f, 0.08f), "lumiere_chaude");
            p.Box("Tablette", V(0f, 0.95f, -0.16f), V(0.95f, 0.04f, 0.3f), "marbre");
            p.Box("Meuble", V(0f, 0.46f, -0.14f), V(0.9f, 0.9f, 0.26f), "bois_fonce", true);
            p.Box("Tiroir", V(0f, 0.75f, -0.272f), V(0.8f, 0.18f, 0.008f), "bois_fonce");
            p.Box("Poignee", V(0f, 0.75f, -0.28f), V(0.12f, 0.015f, 0.012f), "laiton");
            for (int i = 0; i < 4; i++) Bottle(p, V(-0.38f + i * 0.08f, 0.97f, -0.08f), Pick(2) == 0 ? "liquide_ambre" : "bouteille_claire", 0.55f);
            p.Cylinder("Pot de desinfectant", V(0.2f, 1.08f, -0.18f), 0.045f, 0.22f, "neon_cyan");
            for (int i = 0; i < 3; i++) p.Box("Peigne", V(0.2f + (i - 1) * 0.015f, 1.2f, -0.18f), V(0.006f, 0.18f, 0.03f), "noir");
            p.Box("Tondeuse", V(0.0f, 0.99f, -0.22f), V(0.05f, 0.04f, 0.16f), "noir", V(0f, 20f, 0f));
            p.Box("Ciseaux", V(-0.12f, 0.973f, -0.24f), V(0.04f, 0.006f, 0.14f), "chrome", V(0f, -15f, 0f));
            p.Box("Serviettes", V(0.38f, 1.02f, -0.18f), V(0.2f, 0.1f, 0.2f), "blanc");
            p.Box("Sechoir", V(-0.5f, 1.2f, -0.06f), V(0.08f, 0.2f, 0.08f), "noir");
            p.Pop();
            BarberChair(p, at + Quaternion.Euler(0f, yaw, 0f) * V(0f, 0f, -0.95f), yaw + 180f, leather);
        }

        /// <summary>L'enseigne tournante du barbier : cylindre à bandes rouge, blanc, bleu sous globe.</summary>
        private static void BarberPole(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Support", V(0f, 1.6f, 0.03f), V(0.12f, 0.9f, 0.04f), "chrome");
            p.Sphere("Globe", V(0f, 2.1f, -0.1f), V(0.14f, 0.14f, 0.14f), "chrome");
            p.Cylinder("Fut", V(0f, 1.6f, -0.1f), 0.075f, 0.8f, "verre");
            string[] bands = { "rouge", "blanc", "bleu", "blanc" };
            for (int i = 0; i < 12; i++)
            {
                p.Cylinder("Bande", V(0f, 1.25f + i * 0.06f, -0.1f), 0.065f, 0.06f, bands[i % 4], V(0f, 0f, 18f));
            }

            p.Sphere("Pied", V(0f, 1.18f, -0.1f), V(0.12f, 0.08f, 0.12f), "chrome");
            p.Pop();
        }

        /// <summary>Le poste de tatouage : table réglable, tabouret, chariot d'encres, lampe articulée.</summary>
        private static void TattooStation(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Socle", V(0f, 0.15f, 0f), V(0.5f, 0.3f, 0.6f), "metal_noir");
            p.Cylinder("Verin", V(0f, 0.45f, 0f), 0.06f, 0.4f, "chrome");
            p.Box("Matelas", V(0f, 0.7f, 0.15f), V(0.62f, 0.1f, 1.3f), "cuir_noir");
            p.Box("Dossier", V(0f, 0.95f, -0.62f), V(0.62f, 0.1f, 0.6f), "cuir_noir", V(-50f, 0f, 0f));
            p.Box("Appui-bras", V(0.5f, 0.85f, -0.1f), V(0.18f, 0.05f, 0.6f), "cuir_noir");
            p.Cylinder("Tige", V(0.5f, 0.6f, -0.1f), 0.02f, 0.5f, "chrome");
            p.Block("Table de tatouage", V(0f, 0.5f, 0f), V(0.7f, 1f, 1.8f));
            // Le tabouret de l'artiste.
            p.Cylinder("Assise", V(-0.7f, 0.55f, 0.2f), 0.2f, 0.08f, "cuir_noir");
            p.Cylinder("Pied", V(-0.7f, 0.28f, 0.2f), 0.025f, 0.5f, "chrome");
            for (int i = 0; i < 5; i++)
            {
                p.Box("Branche", V(-0.7f, 0.04f, 0.2f) + Quaternion.Euler(0f, i * 72f, 0f) * V(0f, 0f, 0.13f), V(0.03f, 0.03f, 0.26f), "chrome",
                    V(0f, i * 72f, 0f));
            }

            // Le chariot : deux plateaux, des flacons d'encre de toutes les couleurs, la machine.
            Vector3 cart = V(-0.75f, 0f, -0.6f);
            for (int t = 0; t < 2; t++)
            {
                p.Box("Plateau", cart + V(0f, 0.45f + t * 0.4f, 0f), V(0.5f, 0.02f, 0.38f), "inox");
            }

            for (int i = 0; i < 4; i++) p.Cylinder("Montant", cart + V(i % 2 == 0 ? -0.24f : 0.24f, 0.45f, i < 2 ? -0.18f : 0.18f), 0.01f, 0.9f, "chrome");
            for (int i = 0; i < 8; i++)
            {
                p.Cylinder("Encre", cart + V(-0.18f + (i % 4) * 0.08f, 0.91f, -0.08f + (i / 4) * 0.08f), 0.02f, 0.1f, Product());
            }

            p.Box("Machine", cart + V(0.12f, 0.88f, 0.1f), V(0.05f, 0.06f, 0.14f), "metal_noir");
            p.Box("Film", cart + V(0f, 0.475f, 0f), V(0.3f, 0.03f, 0.2f), "blanc");
            p.Block("Chariot", cart + V(0f, 0.45f, 0f), V(0.5f, 0.9f, 0.38f));
            // La lampe articulée, au-dessus de la table.
            p.Cylinder("Pied de lampe", V(0.55f, 0.85f, 0.7f), 0.015f, 1.7f, "metal_noir");
            p.Cylinder("Bras", V(0.35f, 1.7f, 0.5f), 0.012f, 0.6f, "metal_noir", V(0f, -45f, 60f));
            p.Cylinder("Loupe", V(0.12f, 1.55f, 0.3f), 0.15f, 0.04f, "metal");
            p.Cylinder("Lumiere", V(0.12f, 1.53f, 0.3f), 0.12f, 0.01f, "lumiere");
            p.Pop();
        }

        // ================================================================== vêtements

        /// <summary>Un portant : pieds en T, tringle chromée, vêtements sur cintres (chemises, vestes, robes).</summary>
        private static void ClothesRack(InteriorPlan p, Vector3 at, float yaw, float length)
        {
            p.Push(at, yaw);
            float half = length * 0.5f;
            for (int k = -1; k <= 1; k += 2)
            {
                p.Cylinder("Montant", V(k * half, 0.76f, 0f), 0.016f, 1.52f, "chrome");
                p.Box("Pied", V(k * half, 0.02f, 0f), V(0.04f, 0.04f, 0.55f), "chrome");
            }

            p.Cylinder("Tringle", V(0f, 1.52f, 0f), 0.014f, length + 0.04f, "chrome", V(0f, 0f, 90f));
            float x = -half + 0.08f;
            while (x < half - 0.08f)
            {
                string color = Product();
                int group = 2 + Pick(5);
                float len = Rand(0.6f, 1.05f);
                float width = Rand(0.4f, 0.52f);
                for (int i = 0; i < group && x < half - 0.08f; i++)
                {
                    p.Box("Cintre", V(x, 1.47f, 0f), V(0.01f, 0.06f, width - 0.06f), "bois");
                    p.Box("Vetement", V(x, 1.45f - len * 0.5f, 0f), V(0.035f, len, width), color, V(0f, 0f, Rand(-2f, 2f)));
                    x += Rand(0.05f, 0.075f);
                }

                x += 0.03f;
            }

            p.Block("Portant", V(0f, 0.76f, 0f), V(length, 1.52f, 0.55f));
            p.Pop();
        }

        /// <summary>Un mannequin habillé sur son pied (torse, bassin, bras, tête lisse).</summary>
        private static void Mannequin(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            string top = Product(), bottom = Pick(2) == 0 ? "bleu" : "noir";
            p.Cylinder("Socle", V(0f, 0.015f, 0f), 0.2f, 0.03f, "chrome");
            p.Cylinder("Tige", V(0f, 0.45f, 0.05f), 0.012f, 0.9f, "chrome");
            p.Sphere("Bassin", V(0f, 0.98f, 0f), V(0.34f, 0.24f, 0.22f), bottom);
            p.Cylinder("Jambe", V(-0.09f, 0.55f, 0f), 0.065f, 0.8f, bottom);
            p.Cylinder("Jambe", V(0.09f, 0.55f, 0f), 0.065f, 0.8f, bottom);
            p.Sphere("Torse", V(0f, 1.3f, 0f), V(0.4f, 0.55f, 0.24f), top);
            p.Cylinder("Bras", V(-0.24f, 1.25f, 0f), 0.045f, 0.55f, top, V(0f, 0f, -8f));
            p.Cylinder("Bras", V(0.24f, 1.25f, 0f), 0.045f, 0.55f, top, V(0f, 0f, 8f));
            p.Sphere("Main", V(-0.27f, 0.96f, 0f), V(0.06f, 0.09f, 0.04f), "peau_mannequin");
            p.Sphere("Main", V(0.27f, 0.96f, 0f), V(0.06f, 0.09f, 0.04f), "peau_mannequin");
            p.Cylinder("Cou", V(0f, 1.6f, 0f), 0.04f, 0.1f, "peau_mannequin");
            p.Sphere("Tete", V(0f, 1.73f, 0f), V(0.17f, 0.23f, 0.2f), "peau_mannequin");
            p.Block("Mannequin", V(0f, 0.9f, 0f), V(0.45f, 1.8f, 0.3f));
            p.Pop();
        }

        /// <summary>Une table de présentation : piles de vêtements pliés, de toutes les couleurs.</summary>
        private static void FoldTable(InteriorPlan p, Vector3 at, float yaw, float w, float d)
        {
            p.Push(at, yaw);
            p.Box("Plateau", V(0f, 0.78f, 0f), V(w, 0.05f, d), "bois_clair");
            p.Box("Caisson", V(0f, 0.4f, 0f), V(w - 0.2f, 0.72f, d - 0.2f), "blanc");
            p.Block("Table", V(0f, 0.4f, 0f), V(w, 0.8f, d));
            for (float x = -w * 0.5f + 0.22f; x < w * 0.5f - 0.15f; x += 0.38f)
            {
                for (float z = -d * 0.5f + 0.18f; z < d * 0.5f - 0.1f; z += 0.32f)
                {
                    Item(p, "pile_vetements", V(x, 0.805f, z), 0.32f, Rand(0.08f, 0.24f), 0.26f, Product(), Product());
                }
            }

            p.Pop();
        }

        /// <summary>Une cabine d'essayage : deux cloisons, une tringle, un rideau plissé, un tabouret.</summary>
        private static void FittingRoom(InteriorPlan p, Vector3 at, float yaw, string curtain)
        {
            p.Push(at, yaw);
            p.Box("Cloison", V(-0.6f, 1.1f, 0f), V(0.05f, 2.2f, 1.2f), "blanc", true);
            p.Box("Cloison", V(0.6f, 1.1f, 0f), V(0.05f, 2.2f, 1.2f), "blanc", true);
            p.Cylinder("Tringle", V(0f, 2.1f, -0.58f), 0.012f, 1.2f, "chrome", V(0f, 0f, 90f));
            for (int i = 0; i < 9; i++)
            {
                p.Box("Pli", V(-0.55f + i * 0.08f, 1.12f, -0.58f + (i % 2) * 0.03f), V(0.09f, 1.95f, 0.02f), curtain, V(0f, i % 2 == 0 ? 20f : -20f, 0f));
            }

            p.Box("Miroir", V(0f, 1.2f, 0.58f), V(0.5f, 1.4f, 0.01f), "miroir");
            p.Cylinder("Tabouret", V(0.35f, 0.22f, 0.3f), 0.15f, 0.44f, "tissu");
            p.Box("Patere", V(-0.55f, 1.6f, 0.2f), V(0.02f, 0.02f, 0.08f), "chrome");
            p.Pop();
        }

        // ================================================================== bar de nuit

        /// <summary>La cabine du DJ : pupitre éclairé, deux platines, table de mixage, ordinateur, enceintes.</summary>
        private static void DjBooth(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Pupitre", V(0f, 0.55f, 0f), V(2.0f, 1.1f, 0.7f), "plastique_noir", true);
            p.Box("Facade lumineuse", V(0f, 0.55f, -0.352f), V(1.9f, 0.9f, 0.006f), "neon_rose");
            p.Box("Facade (grille)", V(0f, 0.55f, -0.358f), V(1.9f, 0.9f, 0.004f), "verre_fume");
            p.Box("Plateau", V(0f, 1.12f, 0f), V(2.0f, 0.04f, 0.7f), "metal_noir");
            for (int k = -1; k <= 1; k += 2)
            {
                Vector3 deck = V(k * 0.58f, 1.16f, 0.05f);
                p.Box("Platine", deck, V(0.45f, 0.06f, 0.35f), "metal");
                p.Cylinder("Disque", deck + V(-0.04f, 0.035f, 0f), 0.14f, 0.008f, "noir");
                p.Cylinder("Etiquette", deck + V(-0.04f, 0.04f, 0f), 0.04f, 0.004f, Product());
                p.Box("Bras", deck + V(0.15f, 0.045f, 0.02f), V(0.012f, 0.012f, 0.2f), "chrome", V(0f, 20f, 0f));
            }

            p.Box("Table de mixage", V(0f, 1.17f, 0.05f), V(0.32f, 0.08f, 0.38f), "plastique_noir");
            for (int i = 0; i < 8; i++) p.Cylinder("Potentiometre", V(-0.1f + (i % 4) * 0.065f, 1.22f, -0.05f + (i / 4) * 0.12f), 0.012f, 0.02f, "neon_cyan");
            p.Box("Portable", V(0.2f, 1.25f, 0.28f), V(0.32f, 0.22f, 0.02f), "metal", V(-20f, 0f, 0f));
            p.Box("Ecran du portable", V(0.2f, 1.25f, 0.268f), V(0.29f, 0.19f, 0.003f), "ecran", V(-20f, 0f, 0f));
            for (int k = -1; k <= 1; k += 2)
            {
                Speaker(p, V(k * 1.45f, 0f, 0.1f), Facing(V(k * 1.45f, 0f, 0.1f), V(0f, 0f, -4f)), 1.6f);
            }

            p.Pop();
        }

        /// <summary>Une enceinte de sono : caisson, deux haut-parleurs, un tweeter.</summary>
        private static void Speaker(InteriorPlan p, Vector3 at, float yaw, float height)
        {
            p.Push(at, yaw);
            p.Box("Enceinte", V(0f, height * 0.5f, 0f), V(0.6f, height, 0.55f), "plastique_noir", true);
            p.Cylinder("Haut-parleur", V(0f, height * 0.3f, -0.28f), 0.22f, 0.02f, "noir", V(90f, 0f, 0f));
            p.Cylinder("Haut-parleur", V(0f, height * 0.62f, -0.28f), 0.18f, 0.02f, "noir", V(90f, 0f, 0f));
            p.Cylinder("Cache", V(0f, height * 0.3f, -0.29f), 0.07f, 0.02f, "metal", V(90f, 0f, 0f));
            p.Box("Tweeter", V(0f, height * 0.88f, -0.28f), V(0.25f, 0.1f, 0.02f), "metal");
            p.Pop();
        }

        /// <summary>Une piste de danse en dalles lumineuses de couleurs alternées.</summary>
        private static void DanceFloor(InteriorPlan p, Vector3 center, int cols, int rows)
        {
            const float tile = 0.8f;
            string[] colors = { "neon_rose", "neon_cyan", "neon_ambre", "neon_vert" };
            p.Box("Estrade", center + V(0f, 0.04f, 0f), V(cols * tile + 0.1f, 0.08f, rows * tile + 0.1f), "metal_noir", true);
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    Vector3 at = center + V(-cols * tile * 0.5f + tile * (c + 0.5f), 0.083f, -rows * tile * 0.5f + tile * (r + 0.5f));
                    p.Box("Dalle", at, V(tile - 0.04f, 0.006f, tile - 0.04f), (c + r) % 2 == 0 ? colors[(c / 2 + r) % colors.Length] : "verre_fume");
                }
            }
        }

        /// <summary>Une boule à facettes, et deux projecteurs de couleur qui balaient la piste.</summary>
        private static void MirrorBall(InteriorPlan p, Vector3 at, float ceiling)
        {
            p.Cylinder("Fil", at + V(0f, (ceiling - at.y) * 0.5f, 0f), 0.005f, ceiling - at.y, "metal");
            p.Sphere("Boule a facettes", at, V(0.4f, 0.4f, 0.4f), "chrome");
            p.Spot(at + V(-1.5f, ceiling - at.y - 0.2f, 0f), V(70f, 30f, 0f), new Color(1f, 0.3f, 0.8f), 3f, 9f, 35f);
            p.Spot(at + V(1.5f, ceiling - at.y - 0.2f, 0f), V(70f, -30f, 0f), new Color(0.3f, 0.8f, 1f), 3f, 9f, 35f);
        }

        // ================================================================== café

        /// <summary>Une machine à espresso deux groupes, son moulin, ses tasses chauffées dessus.</summary>
        private static void EspressoMachine(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Machine a cafe", V(0f, 0.25f, 0f), V(0.75f, 0.5f, 0.55f), "inox");
            p.Box("Facade", V(0f, 0.3f, -0.276f), V(0.7f, 0.3f, 0.004f), "rouge");
            p.Box("Grille", V(0f, 0.03f, -0.12f), V(0.7f, 0.02f, 0.3f), "metal_noir");
            for (int k = -1; k <= 1; k += 2)
            {
                p.Cylinder("Groupe", V(k * 0.17f, 0.2f, -0.3f), 0.04f, 0.06f, "chrome");
                p.Box("Porte-filtre", V(k * 0.17f, 0.16f, -0.38f), V(0.03f, 0.03f, 0.16f), "noir");
                p.Cylinder("Tasse", V(k * 0.17f, 0.08f, -0.3f), 0.035f, 0.06f, "ceramique");
                p.Cylinder("Buse vapeur", V(k * 0.34f, 0.18f, -0.3f), 0.006f, 0.2f, "chrome", V(0f, 0f, k * 12f));
            }

            p.Cylinder("Manometre", V(0f, 0.42f, -0.28f), 0.03f, 0.01f, "blanc", V(90f, 0f, 0f));
            for (int i = 0; i < 8; i++) p.Cylinder("Tasse chauffee", V(-0.28f + (i % 4) * 0.13f, 0.53f, -0.1f + (i / 4) * 0.15f), 0.035f, 0.06f, "ceramique");
            // Le moulin.
            p.Box("Moulin", V(0.55f, 0.18f, 0f), V(0.2f, 0.36f, 0.25f), "noir");
            p.Cylinder("Tremie", V(0.55f, 0.45f, 0f), 0.08f, 0.2f, "verre");
            p.Cylinder("Grains", V(0.55f, 0.41f, 0f), 0.075f, 0.12f, "bois_fonce");
            p.Pop();
        }

        // ================================================================== véhicule d'exposition

        /// <summary>
        /// Une voiture d'exposition : caisse, capot, coffre, habitacle vitré, toit, roues et
        /// jantes chromées, phares, feux, calandre, rétroviseurs — sur un plateau tournant.
        /// </summary>
        private static void ShowCar(InteriorPlan p, Vector3 at, float yaw, Color paint, bool turntable)
        {
            string body = "carrosserie_" + p.Surfaces.Count;
            p.Define(body, paint, 0.88f, 0.45f);
            p.Push(at, yaw);
            float lift = 0f;
            if (turntable)
            {
                p.Cylinder("Plateau tournant", V(0f, 0.05f, 0f), 2.7f, 0.1f, "metal", default(Vector3), true);
                // Le liseré lumineux : un disque à peine plus large, caché sous le plateau sauf sur son pourtour.
                p.Cylinder("Liseré", V(0f, 0.05f, 0f), 2.73f, 0.05f, "neon_cyan");
                lift = 0.1f;
            }

            p.Push(V(0f, lift, 0f), 0f);
            const float len = 4.3f, wid = 1.8f;
            p.Box("Caisse", V(0f, 0.55f, 0f), V(wid, 0.5f, len), body);
            p.Box("Bas de caisse", V(0f, 0.32f, 0f), V(wid - 0.04f, 0.1f, len - 0.4f), "plastique_noir");
            p.Box("Capot", V(0f, 0.83f, -1.45f), V(wid - 0.06f, 0.1f, 1.3f), body, V(-4f, 0f, 0f));
            p.Box("Coffre", V(0f, 0.85f, 1.65f), V(wid - 0.06f, 0.12f, 0.9f), body);
            p.Box("Habitacle", V(0f, 1.08f, 0.1f), V(wid - 0.2f, 0.5f, 2.0f), "verre_fume");
            p.Box("Toit", V(0f, 1.35f, 0.15f), V(wid - 0.26f, 0.06f, 1.5f), body);
            p.Box("Pare-brise (montant)", V(0f, 1.1f, -0.88f), V(wid - 0.22f, 0.05f, 0.6f), body, V(-55f, 0f, 0f));
            p.Box("Calandre", V(0f, 0.55f, -2.152f), V(0.9f, 0.2f, 0.01f), "noir");
            p.Box("Pare-chocs", V(0f, 0.35f, -2.17f), V(wid, 0.16f, 0.06f), "plastique_noir");
            p.Box("Pare-chocs", V(0f, 0.35f, 2.17f), V(wid, 0.16f, 0.06f), "plastique_noir");
            for (int k = -1; k <= 1; k += 2)
            {
                p.Box("Phare", V(k * 0.65f, 0.66f, -2.155f), V(0.35f, 0.12f, 0.02f), "lumiere");
                p.Box("Feu arriere", V(k * 0.65f, 0.7f, 2.155f), V(0.35f, 0.1f, 0.02f), "feu_rouge");
                p.Box("Retroviseur", V(k * 0.98f, 0.98f, -0.6f), V(0.16f, 0.1f, 0.06f), body);
                p.Box("Poignee", V(k * 0.905f, 0.72f, 0.2f), V(0.01f, 0.03f, 0.14f), "chrome");
                for (int a = -1; a <= 1; a += 2)
                {
                    Vector3 wheel = V(k * 0.82f, 0.33f, a * 1.35f);
                    p.Cylinder("Pneu", wheel, 0.33f, 0.24f, "caoutchouc", V(0f, 0f, 90f));
                    p.Cylinder("Jante", wheel + V(k * 0.005f, 0f, 0f), 0.22f, 0.245f, "chrome", V(0f, 0f, 90f));
                    p.Cylinder("Moyeu", wheel + V(k * 0.01f, 0f, 0f), 0.05f, 0.25f, "metal_noir", V(0f, 0f, 90f));
                }
            }

            p.Block("Voiture", V(0f, 0.7f, 0f), V(wid, 1.4f, len));
            p.Pop();
            p.Pop();
        }

        // ================================================================== prêteur, poste, police, santé

        /// <summary>Une guitare accrochée au mur (le dos en +Z) : caisse, rosace, manche, tête.</summary>
        private static void WallGuitar(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            string body = Pick(3) == 0 ? "rouge" : Pick(2) == 0 ? "bois_clair" : "noir";
            p.Sphere("Caisse", V(0f, -0.25f, -0.06f), V(0.36f, 0.42f, 0.09f), body);
            p.Sphere("Caisse (haut)", V(0f, 0.0f, -0.06f), V(0.28f, 0.3f, 0.09f), body);
            p.Cylinder("Rosace", V(0f, -0.08f, -0.106f), 0.05f, 0.004f, "noir", V(90f, 0f, 0f));
            p.Box("Manche", V(0f, 0.38f, -0.07f), V(0.05f, 0.6f, 0.025f), "bois_fonce");
            p.Box("Tete", V(0f, 0.72f, -0.07f), V(0.08f, 0.16f, 0.02f), "bois_fonce");
            p.Box("Crochet", V(0f, 0.68f, -0.02f), V(0.04f, 0.04f, 0.06f), "metal_noir");
            p.Pop();
        }

        /// <summary>Un vieux téléviseur à tube (ou un écran plat posé), image allumée.</summary>
        private static void Television(InteriorPlan p, Vector3 at, float yaw, bool tube)
        {
            p.Push(at, yaw);
            if (tube)
            {
                p.Box("Televiseur", V(0f, 0.22f, 0.05f), V(0.55f, 0.44f, 0.45f), Pick(2) == 0 ? "bois_fonce" : "plastique_noir");
                p.Box("Ecran", V(0f, 0.24f, -0.172f), V(0.4f, 0.32f, 0.004f), "ecran_tele");
                p.Cylinder("Bouton", V(0.23f, 0.3f, -0.175f), 0.015f, 0.01f, "metal", V(90f, 0f, 0f));
                p.Cylinder("Bouton", V(0.23f, 0.22f, -0.175f), 0.015f, 0.01f, "metal", V(90f, 0f, 0f));
            }
            else
            {
                p.Box("Pied", V(0f, 0.01f, 0f), V(0.3f, 0.02f, 0.18f), "plastique_noir");
                p.Box("Ecran", V(0f, 0.3f, 0f), V(0.8f, 0.48f, 0.04f), "plastique_noir");
                p.Box("Image", V(0f, 0.3f, -0.021f), V(0.76f, 0.44f, 0.004f), "ecran_tele");
            }

            p.Pop();
        }

        /// <summary>Un coffre-fort : caisse épaisse, porte, molette, poignée.</summary>
        private static void Safe(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Coffre-fort", V(0f, 0.5f, 0f), V(0.7f, 1.0f, 0.65f), "metal_noir", true);
            p.Box("Porte", V(0f, 0.5f, -0.33f), V(0.6f, 0.9f, 0.02f), "gris");
            p.Cylinder("Molette", V(-0.1f, 0.6f, -0.35f), 0.07f, 0.03f, "chrome", V(90f, 0f, 0f));
            p.Cylinder("Poignee", V(0.15f, 0.5f, -0.36f), 0.012f, 0.2f, "chrome");
            p.Pop();
        }

        /// <summary>Un mur de boîtes postales (le dos en +Z) : petites portes numérotées, serrures.</summary>
        private static void PostBoxes(InteriorPlan p, Vector3 at, float yaw, float w, float h)
        {
            p.Push(at, yaw);
            p.Box("Boites postales", V(0f, h * 0.5f, -0.15f), V(w, h, 0.3f), "laiton", true);
            int cols = Mathf.FloorToInt(w / 0.2f), rows = Mathf.FloorToInt((h - 0.3f) / 0.16f);
            for (int c = 0; c < cols; c++)
            {
                for (int r = 0; r < rows; r++)
                {
                    Vector3 o = V(-w * 0.5f + 0.1f + c * (w - 0.2f) / Mathf.Max(1, cols - 1), 0.3f + r * 0.16f, -0.302f);
                    p.Box("Porte", o, V(0.18f, 0.14f, 0.006f), "laiton");
                    p.Box("Fenetre", o + V(-0.03f, 0.02f, -0.004f), V(0.08f, 0.05f, 0.003f), "verre_fume");
                    p.Cylinder("Serrure", o + V(0.06f, 0f, -0.005f), 0.01f, 0.006f, "metal_noir", V(90f, 0f, 0f));
                }
            }

            p.Pop();
        }

        /// <summary>Une table d'examen médical : matelas, rouleau de papier, marchepied, tiroirs.</summary>
        private static void ExamTable(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Caisson", V(0f, 0.35f, 0f), V(0.65f, 0.7f, 1.6f), "blanc");
            for (int i = 0; i < 2; i++) p.Box("Tiroir", V(0f, 0.25f + i * 0.25f, -0.801f), V(0.55f, 0.2f, 0.006f), "blanc");
            p.Box("Matelas", V(0f, 0.76f, 0.1f), V(0.66f, 0.1f, 1.45f), "cuir_noir");
            p.Box("Dossier", V(0f, 0.92f, 0.85f), V(0.66f, 0.1f, 0.5f), "cuir_noir", V(35f, 0f, 0f));
            p.Box("Papier", V(0f, 0.815f, 0.05f), V(0.5f, 0.004f, 1.4f), "papier");
            p.Cylinder("Rouleau", V(0f, 0.85f, 1.12f), 0.06f, 0.52f, "papier", V(0f, 0f, 90f));
            p.Box("Marchepied", V(0f, 0.12f, -0.95f), V(0.5f, 0.24f, 0.3f), "inox");
            p.Block("Table d'examen", V(0f, 0.45f, 0.1f), V(0.66f, 0.9f, 1.9f));
            p.Pop();
        }

        /// <summary>Une armoire médicale vitrée (le dos en +Z) : boîtes, flacons, bocaux de coton.</summary>
        private static void MedicalCabinet(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Armoire", V(0f, 0.95f, -0.2f), V(0.9f, 1.9f, 0.4f), "blanc", true);
            for (int s = 0; s < 3; s++)
            {
                float y = 1.05f + s * 0.27f;
                p.Box("Tablette", V(0f, y, -0.2f), V(0.84f, 0.015f, 0.36f), "verre");
                Goods(p, "pharmacie", -0.4f, 0.4f, y + 0.008f, -0.37f, 0.3f, 0.22f);
            }

            p.Box("Porte vitree", V(-0.225f, 1.4f, -0.405f), V(0.44f, 0.86f, 0.01f), "verre");
            p.Box("Porte vitree", V(0.225f, 1.4f, -0.405f), V(0.44f, 0.86f, 0.01f), "verre");
            p.Box("Porte basse", V(0f, 0.48f, -0.405f), V(0.88f, 0.85f, 0.01f), "blanc");
            p.Box("Croix", V(0f, 0.6f, -0.412f), V(0.18f, 0.05f, 0.004f), "rouge");
            p.Box("Croix", V(0f, 0.6f, -0.412f), V(0.05f, 0.18f, 0.004f), "rouge");
            p.Pop();
        }

        /// <summary>Un lavabo sur meuble (le dos en +Z) : vasque, robinet, distributeur de savon, essuie-mains.</summary>
        private static void Sink(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Meuble", V(0f, 0.42f, -0.28f), V(0.8f, 0.84f, 0.55f), "blanc", true);
            p.Box("Plan", V(0f, 0.86f, -0.28f), V(0.84f, 0.04f, 0.58f), "marbre");
            p.Box("Vasque", V(0f, 0.85f, -0.3f), V(0.45f, 0.03f, 0.35f), "ceramique");
            p.Cylinder("Robinet", V(0f, 0.97f, -0.1f), 0.015f, 0.2f, "chrome");
            p.Box("Bec", V(0f, 1.06f, -0.17f), V(0.025f, 0.025f, 0.14f), "chrome");
            p.Box("Savon", V(0.3f, 1.25f, -0.06f), V(0.1f, 0.18f, 0.1f), "blanc");
            p.Box("Essuie-mains", V(-0.45f, 1.35f, -0.08f), V(0.3f, 0.35f, 0.14f), "blanc");
            p.Box("Miroir", V(0f, 1.5f, -0.01f), V(0.6f, 0.75f, 0.01f), "miroir");
            p.Pop();
        }

        /// <summary>Un drapeau sur mât, dans un coin.</summary>
        private static void Flag(InteriorPlan p, Vector3 at, string color)
        {
            p.Cylinder("Socle", at + V(0f, 0.04f, 0f), 0.18f, 0.08f, "laiton");
            p.Cylinder("Mat", at + V(0f, 1.1f, 0f), 0.018f, 2.2f, "bois_fonce");
            p.Sphere("Pommeau", at + V(0f, 2.22f, 0f), V(0.06f, 0.06f, 0.06f), "laiton");
            for (int i = 0; i < 5; i++)
            {
                p.Box("Drapeau", at + V(0.06f + i * 0.04f, 1.75f, (i % 2) * 0.03f), V(0.05f, 0.75f, 0.01f), color, V(0f, i % 2 == 0 ? 25f : -25f, 0f));
            }
        }

        /// <summary>Un portique détecteur de métaux, à l'entrée.</summary>
        private static void MetalDetector(InteriorPlan p, Vector3 at, float yaw)
        {
            p.Push(at, yaw);
            p.Box("Pilier", V(-0.6f, 1.05f, 0f), V(0.12f, 2.1f, 0.5f), "gris", true);
            p.Box("Pilier", V(0.6f, 1.05f, 0f), V(0.12f, 2.1f, 0.5f), "gris", true);
            p.Box("Traverse", V(0f, 2.15f, 0f), V(1.32f, 0.12f, 0.5f), "gris");
            p.Box("Voyant", V(0f, 2.15f, -0.252f), V(0.2f, 0.04f, 0.004f), "ecran_vert");
            p.Pop();
        }
    }
}
