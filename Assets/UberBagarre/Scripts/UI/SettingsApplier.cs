using UberBagarre.Core;
using UberBagarre.View;
using UberBagarre.World;
using UnityEngine;

namespace UberBagarre.UI
{
    /// <summary>
    /// Applique les réglages du joueur (<see cref="GameSettings"/>) qui touchent au moteur :
    /// le mode d'affichage, la limite d'images, la qualité des textures, la distance
    /// d'affichage et l'herbe, les réglages graphiques d'URP, le volume général — et la mise en
    /// sourdine quand la fenêtre passe en arrière-plan.
    ///
    /// Il se crée tout seul au lancement et vit d'une scène à l'autre.
    /// </summary>
    public class SettingsApplier : MonoBehaviour
    {
        private static SettingsApplier _instance;
        private bool _focused = true;
        private float _nextWorld;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Boot()
        {
            if (_instance != null) return;
            GameObject go = new GameObject("Reglages du joueur");
            DontDestroyOnLoad(go);
            _instance = go.AddComponent<SettingsApplier>();
        }

        private void OnEnable()
        {
            GameSettings.Changed += OnChanged;
            ApplyAll();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= OnChanged;
        }

        private void OnApplicationFocus(bool focus)
        {
            _focused = focus;
            ApplyVolume();
        }

        private void OnChanged(string key)
        {
            if (key.StartsWith("son.")) ApplyVolume();
            else if (key == "affichage") ApplyDisplay();
            else if (key == "ips.max") ApplyFrameLimit();
            else if (key == "textures") ApplyTextures();
            else if (key == "distance" || key == "vegetation") ApplyWorld();
            else ApplyGraphics();
        }

        private void Update()
        {
            // La ville se charge après le menu : on repasse ses réglages une fois prête.
            if (Time.unscaledTime < _nextWorld) return;
            _nextWorld = Time.unscaledTime + 2f;
            if (!Mathf.Approximately(MapStreamer.ViewDistance, GameSettings.ViewDistance) ||
                !Mathf.Approximately(MapStreamer.GrassDensity, GameSettings.Vegetation))
            {
                ApplyWorld();
            }
        }

        public static void ApplyAll()
        {
            if (_instance == null) return;
            _instance.ApplyVolume();
            _instance.ApplyDisplay();
            _instance.ApplyFrameLimit();
            _instance.ApplyTextures();
            _instance.ApplyWorld();
            _instance.ApplyGraphics();
        }

        private void ApplyVolume()
        {
            bool mute = !_focused && GameSettings.MuteInBackground && !Application.isEditor;
            AudioListener.volume = mute ? 0f : GameSettings.MasterVolume;
        }

        private void ApplyDisplay()
        {
            if (Application.isEditor) return;
            FullScreenMode mode = GameSettings.DisplayMode == 0 ? FullScreenMode.ExclusiveFullScreen
                : GameSettings.DisplayMode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;
            if (Screen.fullScreenMode != mode) Screen.fullScreenMode = mode;
        }

        private void ApplyFrameLimit()
        {
            int limit = GameSettings.FrameLimit;
            Application.targetFrameRate = limit > 0 ? limit : -1;
        }

        private void ApplyTextures()
        {
#if UNITY_2022_2_OR_NEWER
            QualitySettings.globalTextureMipmapLimit = GameSettings.TextureQuality;
#else
            QualitySettings.masterTextureLimit = GameSettings.TextureQuality;
#endif
        }

        private void ApplyWorld()
        {
            MapStreamer.GrassDensity = GameSettings.Vegetation;
            MapStreamer streamer = FindAnyObjectByType<MapStreamer>();
            if (streamer != null) streamer.ApplyViewDistance(GameSettings.ViewDistance);
            else MapStreamer.ViewDistance = GameSettings.ViewDistance;
        }

        private void ApplyGraphics()
        {
            UrpBridge.ApplySettings(GameSettings.RenderScale, GameSettings.ShadowQuality, GameSettings.AmbientOcclusion,
                GameSettings.ColorBlind, GameSettings.MotionBlur);

            // Rendu intégré : les ombres passent par les réglages de qualité.
            if (!UrpBridge.Active)
            {
                float[] distances = { 40f, 80f, 120f, 180f };
                QualitySettings.shadowDistance = distances[GameSettings.ShadowQuality];
            }
        }
    }
}
