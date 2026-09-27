using System;
using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace UberBagarre.EditorTools
{
    /// <summary>
    /// La ville convertie (la carte de Schedule I, passée au rendu du projet) : un dossier
    /// Assets/Schedule1 qu'on ne met PAS dans git, livré à part (paquet de la release).
    ///
    /// Le dossier contient la scène de la ville, ses maillages, matériaux et textures, et
    /// Carte/reseau.json : tout ce que le jeu doit savoir de la ville sans l'ouvrir — les
    /// boucles de la circulation et des passants, les lieux des courses, la chambre du motel,
    /// la porte du Vertigo, les places de parking, les points de repère de la carte.
    ///
    /// Le fichier est écrit par l'outil de conversion (Tools/schedule1) ; ici on ne fait que
    /// le lire. Absent : le monde ouvert retombe sur la ville procédurale.
    /// </summary>
    internal static class MapPack
    {
        public const string Root = "Assets/Schedule1";
        public const string SceneName = "CarteSchedule1";
        public const string ScenePath = Root + "/Carte/" + SceneName + ".unity";
        public const string DataPath = Root + "/Carte/reseau.json";
        public const string TopViewPath = Root + "/Carte/CarteDessus.png";
        public const string MaterialsFolder = Root + "/Materiaux";

        [Serializable]
        public class Home
        {
            public string name;
            public float[] inside;
            public float[] outside;
            public string door;
            public float[] room;
            public float[] bed;
            public float[] desk;
            public float[] wardrobe;
            public float[] letters;
        }

        /// <summary>
        /// Un logement à vendre. Mêmes meubles que la planque (lit, bureau, armoire, courrier),
        /// posés dans la pièce choisie ; ses portes (chemins dans la ville) restent fermées à clé
        /// tant qu'il n'est pas acheté, et <c>open</c> disparaît pour le propriétaire (portail).
        /// </summary>
        [Serializable]
        public class Property
        {
            public string name;
            public int price;
            public float[] inside;
            public float[] outside;
            public float[] bed;
            public float[] desk;
            public float[] wardrobe;
            public float[] letters;
            public string[] doors;
            public string[] open;
            public float bedWidth = 1.45f;
        }

        [Serializable]
        public class Club
        {
            public float[] door;
            public float[] @out;
            public float[] sign;
        }

        [Serializable]
        public class Spot
        {
            public string name;
            public string kind;
            public float[] p;
            public float yaw;
            public float[] face;
        }

        [Serializable]
        public class Vec
        {
            public float[] v;
        }

        [Serializable]
        public class Path
        {
            public float[] points;
        }

        [Serializable]
        public class Mark
        {
            public string label;
            public float[] p;
            public float[] color;
        }

        [Serializable]
        public class Data
        {
            public int version;
            public float[] bounds;
            public float[] play;
            public float water = -6.5f;
            public Home home;
            public Property[] properties;
            public Club vertigo;
            public float[] car;
            public Vec[] cars;
            public Spot[] spots;
            public Vec[] parking;
            public Path[] drive;
            public Path[] walk;
            public Mark[] landmarks;
            public Vec[] smoke;
            public Vec[] atm;
            public Vec[] vending;
            public string[] doors;
        }

        /// <summary>La ville est-elle installée dans le projet ?</summary>
        public static bool Available
        {
            get { return File.Exists(ScenePath) && File.Exists(DataPath); }
        }

        /// <summary>Lit reseau.json ; null si la ville n'est pas installée ou si le fichier est illisible.</summary>
        public static Data Load()
        {
            if (!Available) return null;

            try
            {
                Data data = JsonUtility.FromJson<Data>(File.ReadAllText(DataPath));
                if (data == null || data.home == null || data.spots == null || data.spots.Length == 0)
                {
                    Debug.LogWarning("[UberBagarre] " + DataPath + " est incomplet : monde ouvert sur la ville procédurale.");
                    return null;
                }

                return data;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] " + DataPath + " illisible (" + e.Message + ") : monde ouvert sur la ville procédurale.");
                return null;
            }
        }

        // ------------------------------------------------------------------ lecture

        public static Vector3 Position(float[] v)
        {
            if (v == null || v.Length < 3) return Vector3.zero;
            return new Vector3(v[0], v[1], v[2]);
        }

        /// <summary>Le 4e nombre d'un point (x, y, z, cap) : le cap en degrés.</summary>
        public static float Yaw(float[] v)
        {
            return v != null && v.Length >= 4 ? v[3] : 0f;
        }

        public static Rect Rect(float[] v)
        {
            if (v == null || v.Length < 4) return new Rect(-200f, -170f, 400f, 320f);
            return new Rect(v[0], v[1], v[2], v[3]);
        }

        public static Vector3[] Points(Path path)
        {
            if (path == null || path.points == null) return new Vector3[0];

            Vector3[] points = new Vector3[path.points.Length / 3];
            for (int i = 0; i < points.Length; i++)
            {
                points[i] = new Vector3(path.points[i * 3], path.points[i * 3 + 1], path.points[i * 3 + 2]);
            }

            return points;
        }

        public static List<Vector3[]> Loops(Path[] paths)
        {
            List<Vector3[]> loops = new List<Vector3[]>();
            if (paths == null) return loops;

            for (int i = 0; i < paths.Length; i++)
            {
                Vector3[] points = Points(paths[i]);
                if (points.Length >= 4) loops.Add(points);
            }

            return loops;
        }

        /// <summary>Un matériau de la ville, par son nom (sans extension). Null s'il n'existe pas.</summary>
        public static Material FindMaterial(string name)
        {
            string path = MaterialsFolder + "/" + name + ".mat";
            return AssetDatabase.LoadAssetAtPath<Material>(path);
        }
    }
}
