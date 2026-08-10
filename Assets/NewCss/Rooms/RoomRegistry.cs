using System.Collections.Generic;
using NewCss.Rooms.Core;
using UnityEngine;

namespace NewCss
{
    /// <summary>
    /// Sahnedeki tüm RoomVolume'leri toplayan statik kayıt defteri +
    /// RoomResolver'a sarmalayıcı ("bu nokta hangi odada" sorusunun tek girişi).
    ///
    /// Kayıt değişince (Register/Unregister) kutu cache'i geçersiz sayılır ve bir
    /// sonraki Resolve çağrısında yeniden kurulur — her karede yeniden allocate
    /// etmemek için.
    /// </summary>
    public static class RoomRegistry
    {
        private static readonly List<RoomVolume> _volumes = new List<RoomVolume>();
        private static RoomEntry[] _cache;
        private static bool _cacheDirty = true;

        public static int Count => _volumes.Count;

        public static void Register(RoomVolume volume)
        {
            if (volume == null || _volumes.Contains(volume))
            {
                return;
            }

            _volumes.Add(volume);
            _cacheDirty = true;
        }

        public static void Unregister(RoomVolume volume)
        {
            if (volume == null)
            {
                return;
            }

            if (_volumes.Remove(volume))
            {
                _cacheDirty = true;
            }
        }

        public static int Resolve(int current, Vector3 position)
        {
            return Resolve(current, position.x, position.y, position.z);
        }

        public static int Resolve(int current, float x, float y, float z)
        {
            RebuildCacheIfNeeded();
            return RoomResolver.Resolve(current, x, y, z, _cache);
        }

        private static void RebuildCacheIfNeeded()
        {
            if (!_cacheDirty)
            {
                return;
            }

            _cache = new RoomEntry[_volumes.Count];
            for (int i = 0; i < _volumes.Count; i++)
            {
                RoomVolume v = _volumes[i];
                Bounds b = v.worldBounds;
                Vector3 min = b.min;
                Vector3 max = b.max;
                _cache[i] = new RoomEntry(v.roomId, new RoomBox(min.x, min.y, min.z, max.x, max.y, max.z));
            }

            _cacheDirty = false;
        }

        /// <summary>
        /// Domain reload kapalıyken (fast enter play mode) statik alanların bir
        /// önceki oturumdan bayat kalmaması için oyun her başladığında temizlenir.
        /// Sahnedeki RoomVolume'ler kendi OnEnable'larında yeniden kayıt olur.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetOnStartup()
        {
            _volumes.Clear();
            _cache = null;
            _cacheDirty = true;
        }
    }
}
