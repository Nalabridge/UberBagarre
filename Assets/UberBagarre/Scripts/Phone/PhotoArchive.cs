using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace UberBagarre.Phone
{
    /// <summary>
    /// Les photos qu'on garde : celles des preuves (une par course réussie) et celles de la
    /// pellicule, écrites sur le disque à côté de la sauvegarde. L'appli retrouve ainsi la photo
    /// de chaque K.O. dans l'historique et sur le profil, d'une partie à l'autre.
    /// </summary>
    public static class PhotoArchive
    {
        private const string Folder = "photos";
        private const int Keep = 60;

        private static readonly Dictionary<string, Texture2D> Cache = new Dictionary<string, Texture2D>();

        /// <summary>Le nom de la dernière photo écrite (la preuve qu'on vient d'envoyer).</summary>
        public static string LastSaved { get; private set; }

        public static float LastSavedTime { get; private set; }

        public static string Directory
        {
            get { return Path.Combine(Application.persistentDataPath, Folder); }
        }

        /// <summary>Écrit la photo, renvoie son nom de fichier (vide en cas d'échec).</summary>
        public static string Save(Texture2D photo, string prefix)
        {
            if (photo == null) return string.Empty;

            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                string name = (string.IsNullOrEmpty(prefix) ? "photo" : prefix) + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + ".jpg";
                byte[] bytes = photo.EncodeToJPG(85);
                File.WriteAllBytes(Path.Combine(Directory, name), bytes);
                Cache[name] = photo;
                LastSaved = name;
                LastSavedTime = Time.unscaledTime;
                Prune();
                return name;
            }
            catch (Exception e)
            {
                Debug.LogWarning("[UberBagarre] Photo non enregistree : " + e.Message);
                return string.Empty;
            }
        }

        /// <summary>La photo de ce nom (chargée une fois), ou null.</summary>
        public static Texture2D Load(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;

            Texture2D texture;
            if (Cache.TryGetValue(name, out texture) && texture != null) return texture;

            try
            {
                string path = Path.Combine(Directory, name);
                if (!File.Exists(path)) return null;

                texture = new Texture2D(2, 2, TextureFormat.RGB24, false);
                texture.LoadImage(File.ReadAllBytes(path));
                texture.name = name;
                Cache[name] = texture;
                return texture;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>Les noms des photos gardées, de la plus récente à la plus ancienne.</summary>
        public static List<string> List()
        {
            List<string> names = new List<string>();
            try
            {
                if (!System.IO.Directory.Exists(Directory)) return names;

                string[] files = System.IO.Directory.GetFiles(Directory, "*.jpg");
                Array.Sort(files, (a, b) => string.CompareOrdinal(Path.GetFileName(b).Substring(Path.GetFileName(b).IndexOf('_') + 1),
                    Path.GetFileName(a).Substring(Path.GetFileName(a).IndexOf('_') + 1)));
                for (int i = 0; i < files.Length; i++) names.Add(Path.GetFileName(files[i]));
            }
            catch (Exception)
            {
            }

            return names;
        }

        private static void Prune()
        {
            // Les preuves et les photos pour le plaisir ont chacune leur quota : cinquante
            // photos de la rue ne doivent pas effacer celle d'un K.O. que l'historique affiche.
            List<string> all = List();
            List<string> old = new List<string>();
            int proofs = 0;
            int others = 0;
            for (int i = 0; i < all.Count; i++)
            {
                bool proof = all[i].StartsWith("preuve");
                int count = proof ? ++proofs : ++others;
                if (count > Keep / 2) old.Add(all[i]);
            }

            for (int i = 0; i < old.Count; i++)
            {
                try
                {
                    File.Delete(Path.Combine(Directory, old[i]));
                    Cache.Remove(old[i]);
                }
                catch (Exception)
                {
                }
            }
        }
    }
}
