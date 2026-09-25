using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.Phone
{
    /// <summary>
    /// La pellicule du téléphone : les photos prises pendant la session.
    ///
    /// Une photo qu'on prend et qui disparaît aussitôt n'est pas une photo, c'est un bouton.
    /// Les garder — et pouvoir les revoir dans la Galerie — fait de l'appareil un objet : on
    /// photographie un K.O. pour le client, et on le retrouve plus tard, avec la rue derrière.
    ///
    /// Rien n'est écrit sur le disque : c'est la mémoire d'une partie, pas un album.
    /// </summary>
    public static class PhoneGallery
    {
        private const int Capacity = 24;

        private static readonly List<Texture2D> _photos = new List<Texture2D>(Capacity);

        public static int Count
        {
            get
            {
                Prune();
                return _photos.Count;
            }
        }

        /// <summary>La photo d'indice donné, de la plus récente (0) à la plus ancienne.</summary>
        public static Texture2D Get(int index)
        {
            Prune();
            if (index < 0 || index >= _photos.Count) return null;
            return _photos[_photos.Count - 1 - index];
        }

        public static Texture2D Latest
        {
            get { return Get(0); }
        }

        public static void Add(Texture2D photo)
        {
            if (photo == null) return;

            Prune();
            if (_photos.Count >= Capacity)
            {
                Object.Destroy(_photos[0]);
                _photos.RemoveAt(0);
            }

            _photos.Add(photo);
        }

        // Sans rechargement du domaine, une texture detruite a la fin d'une partie resterait
        // dans la liste : on retire ce qui n'existe plus.
        private static void Prune()
        {
            for (int i = _photos.Count - 1; i >= 0; i--)
            {
                if (_photos[i] == null) _photos.RemoveAt(i);
            }
        }
    }
}
