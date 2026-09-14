using System;
using System.Collections.Generic;
using UnityEngine;

namespace NewCss.Audio
{
    /// <summary>
    /// Tek bir MusicFamily için karıştırılmış çalma sırasını tutar (bkz.
    /// MusicPlaylistShuffler, Assets/NewCss/Audio/Core/MusicPlaylistShuffler.cs). Aile
    /// değiştiğinde MusicDirector yeni bir MusicQueue oluşturur — sıralama state'i aileler
    /// arası taşınmaz, her aile kendi turunu baştan karıştırır.
    /// </summary>
    public class MusicQueue
    {
        private readonly AudioClip[] _clips;
        private readonly Random _rng;
        private List<int> _order;
        private int _position = -1;
        private int _previousLastIndex = -1;

        public MusicQueue(AudioClip[] clips, Random rng)
        {
            _clips = clips ?? Array.Empty<AudioClip>();
            _rng = rng ?? new Random();
        }

        public bool HasTracks => _clips.Length > 0;

        /// <summary>Sıradaki parçayı döndürür; hiç parça yoksa null (çağıran taraf sessiz kalmalı).</summary>
        public AudioClip Next()
        {
            if (_clips.Length == 0) return null;

            _position++;
            if (_order == null || _position >= _order.Count)
            {
                _order = MusicPlaylistShuffler.ShuffleAvoidingBoundaryRepeat(_clips.Length, _rng, _previousLastIndex);
                _position = 0;
            }

            int clipIndex = _order[_position];
            if (_position == _order.Count - 1) _previousLastIndex = clipIndex;

            return _clips[clipIndex];
        }
    }
}
