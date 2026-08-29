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

        /// <summary>
        /// Kayıt defteri her değiştiğinde artan sayaç. RoomViewController karartma maskesini
        /// (tüm kutuların shader'a gönderilen listesi) her karede yeniden kopyalamak yerine
        /// yalnız bu değer değişince tazeler.
        /// </summary>
        public static int Version { get; private set; }

        public static void Register(RoomVolume volume)
        {
            if (volume == null || _volumes.Contains(volume))
            {
                return;
            }

            _volumes.Add(volume);
            _cacheDirty = true;
            Version++;
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
                Version++;
            }
        }

        public static int Resolve(int current, Vector3 position)
        {
            return Resolve(current, position.x, position.y, position.z);
        }

        public static int Resolve(int current, float x, float y, float z)
        {
            RebuildCacheIfNeeded();
            return RoomResolver.Resolve(current, x, y, z, _cache, DoorwayMargin);
        }

        /// <summary>
        /// Kapı ağzı toleransı: sahnedeki RoomViewSettings paneli varsa oradan, yoksa
        /// RoomResolver'ın kendi varsayılanından gelir. Tek noktada okunur ki çözücünün
        /// iki çağıranı (RoomViewController ve PlayerRoomVisibility) asla farklı marj
        /// kullanmasın — farklı marj = iki taraf farklı odaya karar verir.
        /// </summary>
        private static float DoorwayMargin =>
            RoomViewSettings.Active != null
                ? RoomViewSettings.Active.doorwayMargin
                : RoomResolver.DefaultMargin;

        /// <summary>
        /// Verilen roomId'ye ait tüm RoomVolume kutularının BİRLEŞİM (union) AABB'sini döner.
        /// Karartma shader'ı §H5 gereği tek kutu bilir; L-şekilli/çok-kutulu odalarda (aynı
        /// roomId'yi paylaşan birden çok RoomVolume) v1'de tam kapsam yerine bu union kullanılır
        /// (plan §5, §H5 — "shader tek kutu, union gerekirse v1'de YAPMA" notunun izin verdiği
        /// en basit yaklaşım). Eşleşme yoksa false döner, box dokunulmamış/varsayılan kalır.
        /// </summary>
        public static bool TryGetBounds(int roomId, out RoomBox box)
        {
            RebuildCacheIfNeeded();

            bool found = false;
            RoomBox union = default;

            for (int i = 0; i < _cache.Length; i++)
            {
                if (_cache[i].RoomId != roomId)
                {
                    continue;
                }

                union = found ? UnionOf(union, _cache[i].Box) : _cache[i].Box;
                found = true;
            }

            box = union;
            return found;
        }

        /// <summary>
        /// Kayıtlı HER RoomVolume kutusunu (roomId'ye göre union DEĞİL — L şekilli bir odanın her
        /// parçası ayrı kutu olarak kalmalı, aksi halde maske odanın kaplamadığı boşlukları da
        /// yutardı) verilen dizilere yazar ve yazılan kutu sayısını döner.
        ///
        /// Karartma maskesi için kullanılır: shader "bu nokta HERHANGİ bir odanın içinde mi"
        /// sorusunu bu listeyle yanıtlar; hiçbirinin içinde değilse (dış mekan) karartma uygulanmaz.
        ///
        /// Allocation YAPMAZ — diziler çağırana aittir ve tekrar tekrar kullanılır. Diziler
        /// yetmezse yazma orada kesilir; toplam kutu sayısını çağıran <see cref="Count"/> ile
        /// karşılaştırıp uyarabilir.
        /// </summary>
        public static int CopyBoxes(Vector4[] min, Vector4[] max)
        {
            if (min == null || max == null)
            {
                return 0;
            }

            RebuildCacheIfNeeded();

            int capacity = Mathf.Min(min.Length, max.Length);
            int written = 0;

            for (int i = 0; i < _cache.Length && written < capacity; i++)
            {
                RoomBox box = _cache[i].Box;
                min[written] = new Vector4(box.MinX, box.MinY, box.MinZ, 0f);
                max[written] = new Vector4(box.MaxX, box.MaxY, box.MaxZ, 0f);
                written++;
            }

            return written;
        }

        private static RoomBox UnionOf(RoomBox a, RoomBox b)
        {
            return new RoomBox(
                Mathf.Min(a.MinX, b.MinX), Mathf.Min(a.MinY, b.MinY), Mathf.Min(a.MinZ, b.MinZ),
                Mathf.Max(a.MaxX, b.MaxX), Mathf.Max(a.MaxY, b.MaxY), Mathf.Max(a.MaxZ, b.MaxZ));
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
            // Version SIFIRLANMAZ, artırılır: sıfırlamak elinde eski bir Version tutan bir
            // tüketicinin "değişmedi" sanıp bayat kutu listesini kullanmasına yol açabilirdi.
            // Monoton artan sayaç bu tuzağı tanım gereği kapatır.
            Version++;
        }
    }
}
