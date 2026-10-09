using System;
using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>Les familles de sons, chacune avec son volume dans les réglages.</summary>
    public enum AudioChannel
    {
        Music,
        Effects,
        Voices,
        Ambience,
        Interface
    }

    /// <summary>
    /// Les réglages du joueur, tous au même endroit, enregistrés d'une partie à l'autre.
    ///
    /// Chaque système lit ce qui le concerne (le HUD son mode, les sons leur volume, la caméra
    /// ses secousses…) et s'abonne à <see cref="Changed"/> s'il doit réagir tout de suite. Les
    /// graphismes et l'affichage sont appliqués par <c>UI.SettingsApplier</c>.
    /// </summary>
    public static class GameSettings
    {
        private const string Prefix = "UberBagarre.Reglages.";

        /// <summary>Un réglage a changé (le nom de la clé).</summary>
        public static event Action<string> Changed;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Changed = null;
        }

        private static float GetFloat(string key, float fallback)
        {
            return PlayerPrefs.GetFloat(Prefix + key, fallback);
        }

        private static int GetInt(string key, int fallback)
        {
            return PlayerPrefs.GetInt(Prefix + key, fallback);
        }

        private static void Set(string key, float value)
        {
            if (Mathf.Approximately(PlayerPrefs.GetFloat(Prefix + key, float.NaN), value)) return;
            PlayerPrefs.SetFloat(Prefix + key, value);
            Raise(key);
        }

        private static void Set(string key, int value)
        {
            if (PlayerPrefs.HasKey(Prefix + key) && PlayerPrefs.GetInt(Prefix + key) == value) return;
            PlayerPrefs.SetInt(Prefix + key, value);
            Raise(key);
        }

        private static void Raise(string key)
        {
            Action<string> changed = Changed;
            if (changed != null) changed(key);
        }

        /// <summary>Écrit les réglages sur le disque (à la fermeture des menus).</summary>
        public static void Save()
        {
            PlayerPrefs.Save();
        }

        /// <summary>Efface une famille de réglages (le bouton « Par défaut » d'un onglet).</summary>
        public static void Reset(params string[] keys)
        {
            for (int i = 0; i < keys.Length; i++)
            {
                PlayerPrefs.DeleteKey(Prefix + keys[i]);
                Raise(keys[i]);
            }
        }

        // ------------------------------------------------------------------ jeu

        /// <summary>0 facile, 1 normal, 2 difficile.</summary>
        public static int Difficulty { get { return Mathf.Clamp(GetInt("difficulte", 1), 0, 2); } set { Set("difficulte", Mathf.Clamp(value, 0, 2)); } }

        /// <summary>Ce que coûtent les coups reçus par le joueur.</summary>
        public static float DamageTaken
        {
            get { return Difficulty == 0 ? 0.65f : Difficulty == 2 ? 1.35f : 1f; }
        }

        /// <summary>Ce que font les coups du joueur.</summary>
        public static float DamageDealt
        {
            get { return Difficulty == 0 ? 1.25f : Difficulty == 2 ? 0.9f : 1f; }
        }

        /// <summary>0 complet, 1 discret (seulement quand ça compte), 2 masqué.</summary>
        public static int Hud { get { return Mathf.Clamp(GetInt("hud", 1), 0, 2); } set { Set("hud", Mathf.Clamp(value, 0, 2)); } }

        public static bool Minimap { get { return GetInt("minicarte", 1) == 1; } set { Set("minicarte", value ? 1 : 0); } }

        /// <summary>0 point, 1 croix, 2 aucun.</summary>
        public static int Crosshair { get { return Mathf.Clamp(GetInt("reticule", 0), 0, 2); } set { Set("reticule", Mathf.Clamp(value, 0, 2)); } }

        public static bool AimedZone { get { return GetInt("zone", 1) == 1; } set { Set("zone", value ? 1 : 0); } }
        public static bool DamageNumbers { get { return GetInt("chiffres", 0) == 1; } set { Set("chiffres", value ? 1 : 0); } }
        public static bool EnemyHealthBars { get { return GetInt("barres", 0) == 1; } set { Set("barres", value ? 1 : 0); } }

        /// <summary>Secousses de caméra (coups, chocs), 0 à 1.</summary>
        public static float CameraShake { get { return Mathf.Clamp01(GetFloat("secousses", 1f)); } set { Set("secousses", Mathf.Clamp01(value)); } }

        /// <summary>Balancement de la tête en marchant, 0 à 1.</summary>
        public static float HeadBob { get { return Mathf.Clamp01(GetFloat("balancement", 1f)); } set { Set("balancement", Mathf.Clamp01(value)); } }

        /// <summary>Le champ de vision de la caméra du joueur, en degrés (verticalement).</summary>
        public const float DefaultFieldOfView = 64f;

        public static float FieldOfView
        {
            // L'ancien menu l'enregistrait sous une autre clé : on la reprend tant qu'il n'y a pas la nouvelle.
            get { return Mathf.Clamp(GetFloat("fov", PlayerPrefs.GetFloat("UberBagarre.Menu.fov", DefaultFieldOfView)), 55f, 95f); }
            set { Set("fov", Mathf.Clamp(Mathf.Round(value), 55f, 95f)); }
        }

        /// <summary>Le compteur d'images par seconde, en haut à gauche.</summary>
        public static bool ShowFps
        {
            get { return GetInt("ips", PlayerPrefs.GetInt("UberBagarre.Menu.ips", 0)) == 1; }
            set { Set("ips", value ? 1 : 0); }
        }

        /// <summary>Les rappels de touches (« E  Ouvrir », le pied de la carte…).</summary>
        public static bool KeyHints { get { return GetInt("aides", 1) == 1; } set { Set("aides", value ? 1 : 0); } }

        // ------------------------------------------------------------------ sous-titres

        public static bool Subtitles { get { return GetInt("soustitres", 1) == 1; } set { Set("soustitres", value ? 1 : 0); } }

        /// <summary>0 petits, 1 normaux, 2 grands, 3 très grands.</summary>
        public static int SubtitleSize { get { return Mathf.Clamp(GetInt("soustitres.taille", 1), 0, 3); } set { Set("soustitres.taille", Mathf.Clamp(value, 0, 3)); } }

        public static float SubtitleScale
        {
            get { return SubtitleSize == 0 ? 0.85f : SubtitleSize == 2 ? 1.25f : SubtitleSize == 3 ? 1.5f : 1f; }
        }

        /// <summary>Opacité du fond derrière les sous-titres, 0 à 1.</summary>
        public static float SubtitleBackground { get { return Mathf.Clamp01(GetFloat("soustitres.fond", 0.6f)); } set { Set("soustitres.fond", Mathf.Clamp01(value)); } }

        /// <summary>Le nom de celui qui parle, au-dessus de la réplique.</summary>
        public static bool SubtitleSpeaker { get { return GetInt("soustitres.nom", 1) == 1; } set { Set("soustitres.nom", value ? 1 : 0); } }

        // ------------------------------------------------------------------ son

        public static float MasterVolume { get { return Mathf.Clamp01(GetFloat("son.general", 1f)); } set { Set("son.general", Mathf.Clamp01(value)); } }

        public static float ChannelVolume(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return Mathf.Clamp01(GetFloat("son.musique", 0.8f));
                case AudioChannel.Effects: return Mathf.Clamp01(GetFloat("son.effets", 1f));
                case AudioChannel.Voices: return Mathf.Clamp01(GetFloat("son.voix", 1f));
                case AudioChannel.Ambience: return Mathf.Clamp01(GetFloat("son.ambiance", 0.9f));
                default: return Mathf.Clamp01(GetFloat("son.interface", 0.8f));
            }
        }

        public static void SetChannelVolume(AudioChannel channel, float value)
        {
            Set(ChannelKey(channel), Mathf.Clamp01(value));
        }

        public static string ChannelKey(AudioChannel channel)
        {
            switch (channel)
            {
                case AudioChannel.Music: return "son.musique";
                case AudioChannel.Effects: return "son.effets";
                case AudioChannel.Voices: return "son.voix";
                case AudioChannel.Ambience: return "son.ambiance";
                default: return "son.interface";
            }
        }

        /// <summary>Le volume d'une famille de sons (le général passe par l'écouteur).</summary>
        public static float Volume(AudioChannel channel)
        {
            return ChannelVolume(channel);
        }

        /// <summary>Couper le son quand la fenêtre du jeu n'est plus au premier plan.</summary>
        public static bool MuteInBackground { get { return GetInt("son.arriereplan", 1) == 1; } set { Set("son.arriereplan", value ? 1 : 0); } }

        // ------------------------------------------------------------------ commandes

        public static bool InvertY { get { return GetInt("inverser", 0) == 1; } set { Set("inverser", value ? 1 : 0); } }

        /// <summary>Lissage de la souris, 0 (brut) à 1.</summary>
        public static float MouseSmoothing { get { return Mathf.Clamp01(GetFloat("lissage", 0f)); } set { Set("lissage", Mathf.Clamp01(value)); } }

        // ------------------------------------------------------------------ accessibilité

        /// <summary>Taille de l'interface, 0,8 à 1,3.</summary>
        public static float UiScale { get { return Mathf.Clamp(GetFloat("interface.taille", 1f), 0.8f, 1.3f); } set { Set("interface.taille", Mathf.Clamp(value, 0.8f, 1.3f)); } }

        /// <summary>0 aucun, 1 protanopie, 2 deutéranopie, 3 tritanopie.</summary>
        public static int ColorBlind { get { return Mathf.Clamp(GetInt("daltonisme", 0), 0, 3); } set { Set("daltonisme", Mathf.Clamp(value, 0, 3)); } }

        /// <summary>Atténue les éclairs (parade, dégâts, impacts).</summary>
        public static bool ReduceFlashes { get { return GetInt("eclairs", 0) == 1; } set { Set("eclairs", value ? 1 : 0); } }

        public static float FlashScale
        {
            get { return ReduceFlashes ? 0.3f : 1f; }
        }

        // ------------------------------------------------------------------ graphismes (complément du directeur graphique)

        /// <summary>Résolution de rendu (URP), 0,5 à 1.</summary>
        public static float RenderScale { get { return Mathf.Clamp(GetFloat("rendu.echelle", 1f), 0.5f, 1f); } set { Set("rendu.echelle", Mathf.Clamp(value, 0.5f, 1f)); } }

        /// <summary>0 basses, 1 moyennes, 2 hautes, 3 ultra.</summary>
        public static int ShadowQuality { get { return Mathf.Clamp(GetInt("ombres", 2), 0, 3); } set { Set("ombres", Mathf.Clamp(value, 0, 3)); } }

        /// <summary>Distance d'affichage (facteur), 0,5 à 2.</summary>
        public static float ViewDistance { get { return Mathf.Clamp(GetFloat("distance", 1f), 0.5f, 2f); } set { Set("distance", Mathf.Clamp(value, 0.5f, 2f)); } }

        /// <summary>Densité de l'herbe, 0 à 1,5.</summary>
        public static float Vegetation { get { return Mathf.Clamp(GetFloat("vegetation", 0.8f), 0f, 1.5f); } set { Set("vegetation", Mathf.Clamp(value, 0f, 1.5f)); } }

        /// <summary>0 pleine, 1 moitié, 2 quart.</summary>
        public static int TextureQuality { get { return Mathf.Clamp(GetInt("textures", 0), 0, 2); } set { Set("textures", Mathf.Clamp(value, 0, 2)); } }

        public static bool AmbientOcclusion { get { return GetInt("ao", 1) == 1; } set { Set("ao", value ? 1 : 0); } }

        /// <summary>Images par seconde maximum (0 : illimité).</summary>
        public static int FrameLimit { get { return Mathf.Max(0, GetInt("ips.max", 0)); } set { Set("ips.max", Mathf.Max(0, value)); } }

        /// <summary>0 plein écran, 1 fenêtré sans bordure, 2 fenêtré.</summary>
        public static int DisplayMode { get { return Mathf.Clamp(GetInt("affichage", 1), 0, 2); } set { Set("affichage", Mathf.Clamp(value, 0, 2)); } }

        /// <summary>Flou de mouvement (URP), 0 à 1.</summary>
        public static float MotionBlur { get { return Mathf.Clamp01(GetFloat("flou", 0f)); } set { Set("flou", Mathf.Clamp01(value)); } }

        /// <summary>L'ambiance de l'image choisie (préréglage du directeur graphique), -1 si retouchée à la main.</summary>
        public static int LookPreset { get { return Mathf.Clamp(GetInt("ambiance", 3), -1, 3); } set { Set("ambiance", Mathf.Clamp(value, -1, 3)); } }

        /// <summary>Profondeur de champ dans les plans de caméra (magasins, cinématiques).</summary>
        public static bool DepthOfField { get { return GetInt("profondeur", 1) == 1; } set { Set("profondeur", value ? 1 : 0); } }
    }
}
