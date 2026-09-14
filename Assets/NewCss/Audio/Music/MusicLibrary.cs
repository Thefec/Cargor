using System.Collections.Generic;
using UnityEngine;

namespace NewCss.Audio
{
    /// <summary>
    /// MusicFamily -&gt; AudioClip[] çözen, Resources.LoadAll ile klasörü DOĞRUDAN okuyan statik
    /// önbellek. Yeni parça eklemek için ilgili Resources klasörüne dosya atmak yeterlidir, kod
    /// DEĞİŞMEZ (bkz. plans/ses-tasarimi.md Faz C brief madde 1). AssetDatabase KULLANILMAZ —
    /// o editor-only'dir, build'de yok. Resources.LoadAll build'de de çalışır (bu tuzağa bilerek
    /// düşülmedi — brief'te açıkça uyarılmıştı).
    ///
    /// Fiziksel klasörler (Resources-göreli yol, "Resources/" öneki YOK):
    ///   Assets/Resources/Music/Gameplay/Main    -&gt; "Music/Gameplay/Main"
    ///   Assets/Resources/Music/Gameplay/Busy    -&gt; "Music/Gameplay/Busy"
    ///   Assets/Resources/Music/Gameplay/Closing -&gt; "Music/Gameplay/Closing"
    ///   Assets/Resources/Music/Gameplay/Tension -&gt; "Music/Gameplay/Tension"
    ///   Assets/Resources/Music/Menu             -&gt; "Music/Menu"
    ///
    /// Bu klasör yapısı Faz B'nin "Assets/Resources/Audio/SfxLibrary*" deseniyle tutarlı
    /// (AudioRoutingConfig.cs'nin de kullandığı proje kuralı: tek paylaşılan Assets/Resources/
    /// kökü, altında sistem başına alt klasör).
    ///
    /// Stinger BURADA YOK — Faz B kendi playlist/yükleme mekanizmasını kullanıyor, taşınmadı.
    /// </summary>
    public static class MusicLibrary
    {
        private static readonly Dictionary<MusicFamily, AudioClip[]> _cache = new Dictionary<MusicFamily, AudioClip[]>();

        public static AudioClip[] GetTracks(MusicFamily family)
        {
            if (_cache.TryGetValue(family, out var cached)) return cached;

            string path = ResourcePathFor(family);
            AudioClip[] loaded = string.IsNullOrEmpty(path) ? null : Resources.LoadAll<AudioClip>(path);

            if (loaded == null || loaded.Length == 0)
            {
                Debug.LogWarning($"[MusicLibrary] Resources/{path} altında hiç AudioClip bulunamadı — " +
                                  $"{family} ailesi sessiz kalacak. Klasöre parça eklendiğinden emin ol.");
                loaded = System.Array.Empty<AudioClip>();
            }

            _cache[family] = loaded;
            return loaded;
        }

        private static string ResourcePathFor(MusicFamily family)
        {
            switch (family)
            {
                case MusicFamily.Menu: return "Music/Menu";
                case MusicFamily.Main: return "Music/Gameplay/Main";
                case MusicFamily.Busy: return "Music/Gameplay/Busy";
                case MusicFamily.Closing: return "Music/Gameplay/Closing";
                case MusicFamily.Tension: return "Music/Gameplay/Tension";
                default: return null;
            }
        }
    }
}
