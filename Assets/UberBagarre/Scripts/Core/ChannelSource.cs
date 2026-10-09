using UnityEngine;

namespace UberBagarre.Core
{
    /// <summary>
    /// Une source au volume fixe (une musique, une sirène, une boucle d'ambiance) rangée dans une
    /// famille de sons : son volume suit le réglage de la famille, même s'il change en jeu.
    /// Les sources dont le volume bouge sans cesse (moteurs, pluie) multiplient elles-mêmes par
    /// <see cref="GameSettings.Volume"/>.
    /// </summary>
    public class ChannelSource : MonoBehaviour
    {
        [SerializeField] private AudioSource _source;
        [SerializeField] private AudioChannel _channel;
        [SerializeField, Range(0f, 1f)] private float _volume = 1f;

        /// <summary>Range une source dans une famille, à ce volume de base.</summary>
        public static ChannelSource Attach(AudioSource source, AudioChannel channel, float volume)
        {
            if (source == null) return null;
            ChannelSource tag = source.GetComponent<ChannelSource>();
            if (tag == null || tag._source != source) tag = source.gameObject.AddComponent<ChannelSource>();
            tag._source = source;
            tag._channel = channel;
            tag._volume = volume;
            tag.Apply();
            return tag;
        }

        /// <summary>Change le volume de base (la famille s'y applique).</summary>
        public void SetVolume(float volume)
        {
            _volume = volume;
            Apply();
        }

        private void OnEnable()
        {
            GameSettings.Changed += OnChanged;
            Apply();
        }

        private void OnDisable()
        {
            GameSettings.Changed -= OnChanged;
        }

        private void OnChanged(string key)
        {
            if (key.StartsWith("son.")) Apply();
        }

        private void Apply()
        {
            if (_source != null) _source.volume = _volume * GameSettings.Volume(_channel);
        }
    }
}
