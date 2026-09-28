using System;
using System.Collections.Generic;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Le journal en ligne de la ville, <i>Hyland Info</i> : ses articles paraissent au fil de
    /// l'histoire (elle en publie, certains parlent de toi) et se lisent sur l'ordinateur.
    /// </summary>
    public static class NewsFeed
    {
        [Serializable]
        public class Article
        {
            public string id;
            public string title;
            public string lead;
            public string body;
            public int day;
            public bool unread = true;
        }

        private static readonly List<Article> _articles = new List<Article>();

        public static IReadOnlyList<Article> Articles { get { return _articles; } }

        /// <summary>Un article paraît (une seule fois par identifiant).</summary>
        public static event Action<Article> Published;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _articles.Clear();
            Published = null;
        }

        public static bool Has(string id)
        {
            for (int i = 0; i < _articles.Count; i++) if (_articles[i].id == id) return true;
            return false;
        }

        public static void Publish(string id, int day, string title, string lead, string body, bool notify = true)
        {
            if (Has(id)) return;
            Article a = new Article { id = id, day = day, title = title, lead = lead, body = body, unread = notify };
            _articles.Insert(0, a);
            if (!notify) return;
            Action<Article> handler = Published;
            if (handler != null) handler(a);
        }

        public static void Clear()
        {
            _articles.Clear();
        }
    }
}
