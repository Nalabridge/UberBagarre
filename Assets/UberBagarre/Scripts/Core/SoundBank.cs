using System.Collections.Generic;
using System.Text;
using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// La banque de sons : chaque bruitage du jeu a un nom (« Police/sirene », « Meteo/pluie »,
    /// « Telephone/sonnerie »…). Un vrai enregistrement posé dans
    /// <c>Assets/UberBagarre/Resources/Sons/</c> sous ce nom (wav, ogg ou mp3, sans l'extension
    /// dans le nom) remplace le son fabriqué par le code. Un dossier du même nom rempli de
    /// plusieurs fichiers donne des variantes tirées au hasard.
    ///
    /// Sans fichier, le jeu garde son son fabriqué : rien ne casse s'il manque quelque chose.
    /// La liste complète des noms est dans <c>Docs/SONS.md</c>.
    /// </summary>
    public static class SoundBank
    {
        public const string Root = "Sons/";

        private static readonly Dictionary<string, AudioClip> Singles = new Dictionary<string, AudioClip>();
        private static readonly Dictionary<string, AudioClip[]> Variants = new Dictionary<string, AudioClip[]>();
        private static readonly HashSet<AudioClip> Loaded = new HashSet<AudioClip>();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Singles.Clear();
            Variants.Clear();
            Loaded.Clear();
        }

        /// <summary>L'enregistrement fourni pour <paramref name="key"/>, ou null (on fabriquera le son).</summary>
        public static AudioClip Real(string key)
        {
            if (string.IsNullOrEmpty(key)) return null;
            AudioClip clip;
            if (Singles.TryGetValue(key, out clip)) return clip;

            clip = Resources.Load<AudioClip>(Root + key);
            if (clip == null)
            {
                // Un dossier de variantes sous ce nom : on en prend une.
                AudioClip[] variants = RealVariants(key);
                if (variants.Length > 0) clip = variants[Random.Range(0, variants.Length)];
            }

            Singles[key] = clip;
            if (clip != null) Loaded.Add(clip);
            return clip;
        }

        /// <summary>Toutes les variantes fournies sous le dossier <paramref name="key"/> (vide s'il n'y en a pas).</summary>
        public static AudioClip[] RealVariants(string key)
        {
            AudioClip[] clips;
            if (Variants.TryGetValue(key, out clips)) return clips;
            clips = Resources.LoadAll<AudioClip>(Root + key) ?? new AudioClip[0];
            Variants[key] = clips;
            for (int i = 0; i < clips.Length; i++) Loaded.Add(clips[i]);
            return clips;
        }

        /// <summary>Une variante au hasard, ou null.</summary>
        public static AudioClip RealVariant(string key)
        {
            AudioClip[] clips = RealVariants(key);
            return clips.Length > 0 ? clips[Random.Range(0, clips.Length)] : null;
        }

        /// <summary>
        /// Libère un son fabriqué quand son propriétaire disparaît ; un enregistrement de la
        /// banque, lui, est un fichier du projet : on n'y touche pas.
        /// </summary>
        public static void Release(AudioClip clip)
        {
            if (clip == null || Loaded.Contains(clip)) return;
            Object.Destroy(clip);
        }

        /// <summary>« Babillage Léon » → « babillage_leon » : le nom d'un son dans la banque.</summary>
        public static string Slug(string text)
        {
            if (string.IsNullOrEmpty(text)) return "son";
            string decomposed = text.ToLowerInvariant().Normalize(NormalizationForm.FormD);
            StringBuilder sb = new StringBuilder(decomposed.Length);
            bool underscore = false;
            for (int i = 0; i < decomposed.Length; i++)
            {
                char c = decomposed[i];
                if (System.Globalization.CharUnicodeInfo.GetUnicodeCategory(c) == System.Globalization.UnicodeCategory.NonSpacingMark) continue;
                if ((c >= 'a' && c <= 'z') || (c >= '0' && c <= '9'))
                {
                    sb.Append(c);
                    underscore = false;
                }
                else if (!underscore && sb.Length > 0)
                {
                    sb.Append('_');
                    underscore = true;
                }
            }

            return sb.ToString().Trim('_');
        }
    }
}
