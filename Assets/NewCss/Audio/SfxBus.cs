using System.Collections.Generic;
using UnityEngine;

namespace NewCss.Audio
{
    /// <summary>
    /// Olay/etkileşim sesleri için tek giriş noktası (bkz. plans/ses-tasarimi.md §5 Faz B).
    /// Statik — MonoBehaviour DEĞİL (EditMode'dan test edilebilir çekirdek mantık Core/ altında:
    /// SfxLibraryLookup + SfxRateLimiter; bkz. NumberRoller/AudioVolumeMath ile aynı desen).
    ///
    /// AudioSource.PlayClipAtPoint KULLANILMAZ (mixer'a uğramaz, Faz A'da projeden temizlendi).
    /// Bunun yerine: 2D çağrılar AudioRouting'in kalıcı SFX fallback source'unu (zaten "havuzlanmış",
    /// tek kalıcı kaynak) kullanır; konumlu/pitch-varyasyonlu çağrılar kendi küçük AudioSource
    /// havuzunu kullanır (Route ile SFX grubuna bağlı, DontDestroyOnLoad).
    /// </summary>
    public static class SfxBus
    {
        private const string LibraryResourcePath = "Audio/SfxLibrary";
        private const int PoolSize = 8;

        private static SfxLibrary _library;
        private static bool _loadAttempted;

        private static readonly Dictionary<SfxId, float> _lastPlayedTime = new Dictionary<SfxId, float>();

        private static GameObject _poolRoot;
        private static AudioSource[] _pool;

        private static SfxLibrary Library
        {
            get
            {
                if (_library == null && !_loadAttempted)
                {
                    _loadAttempted = true;
                    _library = Resources.Load<SfxLibrary>(LibraryResourcePath);
                    if (_library == null)
                    {
                        Debug.LogError($"[SfxBus] Resources/{LibraryResourcePath}.asset bulunamadı — " +
                                        "olay sesleri çalmayacak. Tools ▸ Cargor ▸ Audio ▸ SFX Kutuphanesini Kur.");
                    }
                }
                return _library;
            }
        }

        /// <summary>
        /// 2D (konumsuz) olay sesi. minInterval &gt; 0 ise aynı SfxId bu süreden daha sık
        /// tetiklenmeye çalışılırsa çağrı sessizce yok sayılır (ör. sayaç tık sesi — hızlı
        /// sayarken her rakamda çalarsa rahatsız eder). pitchVariation &gt; 0 ise ±pitchVariation
        /// aralığında rastgele pitch uygulanır (aynı sesin art arda tekrarında monotonluğu kırar).
        /// </summary>
        public static void Play(SfxId id, float minInterval = 0f, float pitchVariation = 0f)
        {
            if (!TryResolveAndGate(id, minInterval, out AudioClip clip)) return;

            if (pitchVariation > 0f)
            {
                PlayPooled(clip, Vector3.zero, spatial: false, pitchVariation);
            }
            else
            {
                AudioRouting.PlayOneShot(AudioCategory.SFX, clip);
            }
        }

        /// <summary>
        /// 3D konumlu olay sesi (ör. sahnedeki bir noktadan). Havuzlanmış AudioSource kullanır —
        /// PlayClipAtPoint YASAK (bkz. sınıf yorumu).
        /// </summary>
        public static void PlayAtPosition(SfxId id, Vector3 position, float minInterval = 0f)
        {
            if (!TryResolveAndGate(id, minInterval, out AudioClip clip)) return;

            PlayPooled(clip, position, spatial: true, pitchVariation: 0f);
        }

        private static bool TryResolveAndGate(SfxId id, float minInterval, out AudioClip clip)
        {
            clip = null;
            var library = Library;
            if (library == null) return false;

            if (!library.TryGetClip(id, out clip) || clip == null)
            {
                Debug.LogWarning($"[SfxBus] '{id}' için klip atanmamış (SfxLibrary'de boş slot) — " +
                                  "bkz. Tools ▸ Cargor ▸ Audio ▸ Bos Ses Slotlarini Kontrol Et.");
                return false;
            }

            float now = Time.unscaledTime;
            if (_lastPlayedTime.TryGetValue(id, out float last) && !SfxRateLimiter.ShouldPlay(last, now, minInterval))
            {
                return false;
            }
            _lastPlayedTime[id] = now;
            return true;
        }

        private static void PlayPooled(AudioClip clip, Vector3 position, bool spatial, float pitchVariation)
        {
            EnsurePool();

            AudioSource source = AcquireFreeSource();
            if (source == null) return;

            source.transform.position = position;
            source.spatialBlend = spatial ? 1f : 0f;
            source.pitch = pitchVariation > 0f ? 1f + Random.Range(-pitchVariation, pitchVariation) : 1f;
            source.clip = clip;
            source.Play();
        }

        private static void EnsurePool()
        {
            if (_pool != null) return;

            _poolRoot = new GameObject("SfxBus_Pool");
            Object.DontDestroyOnLoad(_poolRoot);

            _pool = new AudioSource[PoolSize];
            for (int i = 0; i < PoolSize; i++)
            {
                var go = new GameObject($"SfxBus_PooledSource_{i}");
                go.transform.SetParent(_poolRoot.transform);

                var source = go.AddComponent<AudioSource>();
                source.playOnAwake = false;
                AudioRouting.Route(source, AudioCategory.SFX);

                _pool[i] = source;
            }
        }

        private static AudioSource AcquireFreeSource()
        {
            if (_pool == null || _pool.Length == 0) return null;

            foreach (var source in _pool)
            {
                if (source != null && !source.isPlaying) return source;
            }

            // Hepsi meşgul (havuz boyutu 8 — pratikte nadir): en eskisini keserek çal, sessiz
            // kalmaktan iyidir. Kısa SFX'lerde bu dal fiilen tetiklenmez.
            return _pool[0];
        }
    }
}
