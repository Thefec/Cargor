using System;
using NewCss;
using UnityEngine;
using UnityEngine.SceneManagement;
using Random = System.Random; // CS0104: UnityEngine.Random ile karisiyor; shuffler seed-li System.Random istiyor

namespace NewCss.Audio
{
    /// <summary>
    /// Faz C müzik sistemi (bkz. plans/ses-tasarimi.md Faz C brief).
    ///
    /// DURUM (2026-09-14, Faz C tamamlandı): [RuntimeInitializeOnLoadMethod] ile hiçbir sahneye
    /// elle eklenmeden kendini kurar (bkz. Bootstrap() altta) — hangi sahneden başlanırsa
    /// başlansın (editörde doğrudan "The Main Office"e basmak dahil) tek bir DontDestroyOnLoad
    /// instance garanti edilir, sıfır sahne dosyası değişikliği gerekmez.
    ///
    /// Ana menü müziği artık BU SINIF üzerinden çalıyor. Eski Assets/Music/MusicPlayer.cs
    /// uyumluluk katmanına dönüştürüldü (kendi başına Play()/Stop() çağırmıyor) — silinmedi,
    /// çünkü AudioSourceRoutingSetup.cs ve UnifiedSettingsManager.musicAudioSource hâlâ o
    /// tipe/GameObject'e bağımlı (bkz. MusicPlayer.cs başındaki not).
    ///
    /// Tek DontDestroyOnLoad instance; sahne adına göre Menu/Gameplay moduna geçer:
    ///   - Menu sahnesinde (varsayılan: "MainMenu") sabit Menu ailesini çalar.
    ///   - Gameplay sahnelerinde (varsayılan: "The Main Office", "Tutorial") DayCycleManager'ı
    ///     SALT OKUMA (currentDay/elapsedTime/CurrentDayDuration/IsDayOver) ve varsa
    ///     CustomerManager.QueueSize'ı okuyarak MusicPhaseSelector ile aile seçer
    ///     (Main/Busy/Tension/Closing). Tutorial'da DayCycleManager hiç yoksa/spawn olmamışsa
    ///     EvaluateGameplayPhase() null-safe — sessiz kalmaz, Main ailesine düşer (kullanıcı
    ///     kararı: Tutorial sakin A ailesiyle çalsın).
    ///   - Aile içinde MusicQueue (karıştırılmış, tur sınırında tekrarsız) sırayla çalar; parça
    ///     sonuna yaklaşınca ya da aile değişince MusicCrossfadeMath ile iki AudioSource arasında
    ///     çapraz geçiş yapılır (equal-power, varsayılan 2.5 sn).
    ///
    /// SESSİZ ÖLÜM BEKÇİSİ (GDD §27): kendi AudioSource'larını RUNTIME'DA kurar/route eder —
    /// Inspector'a clip/source sürüklemeye GEREK YOK. Bu sınıf o hata sınıfını (bağlanmamış
    /// AudioSource, silinen clip referansı) yapısal olarak ortadan kaldırıyor.
    ///
    /// Netcode YOK: müzik tamamen yerel, her oyuncu kendi sırasını duyar; DayCycleManager'dan
    /// yalnızca OKUNUR, hiçbir NetworkVariable/RPC yazılmaz.
    /// </summary>
    public class MusicDirector : MonoBehaviour
    {
        private static MusicDirector _instance;

        /// <summary>
        /// Sahneye elle eklemeye gerek bırakmayan bootstrap — ilk sahne yüklendikten hemen
        /// sonra çalışır (AfterSceneLoad), tek bir DontDestroyOnLoad GameObject kurar. Editörde
        /// domain reload kapalıyken Play'e art arda basılırsa _instance hâlâ ayakta olabilir —
        /// guard bu durumda ikinci bir kurulumu engeller.
        /// </summary>
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            if (_instance != null) return;
            var go = new GameObject("MusicDirector (bootstrap)");
            go.AddComponent<MusicDirector>();
        }

        [Header("Sahne eşlemesi")]
        [Tooltip("Bu sahnelerde sabit Menu ailesi çalar")]
        public string[] menuSceneNames = { "MainMenu" };

        [Tooltip("Bu sahnelerde DayCycleManager'a göre gameplay ailesi (Main/Busy/Tension/Closing) çalar")]
        public string[] gameplaySceneNames = { "The Main Office", "Tutorial" };

        [Header("Çapraz geçiş")]
        [Tooltip("İki parça arası üst üste binme süresi (saniye)")]
        public float crossfadeDuration = 2.5f;

        [Header("Gün fazı eşikleri (playtest'te ayarlanabilir)")]
        public MusicPhaseThresholds phaseThresholds = MusicPhaseThresholds.Default;

        [Tooltip("Gün fazı kontrolünü kaç saniyede bir yap (performans; crossfade zamanlaması ayrı, her frame işlenir)")]
        public float phasePollInterval = 1f;

        private AudioSource _sourceA;
        private AudioSource _sourceB;
        private bool _usingA;
        private bool _crossfading;
        private float _crossfadeElapsed;

        private MusicFamily? _currentFamily;
        private MusicQueue _currentQueue;
        private readonly Random _rng = new Random();

        private float _lastPhasePoll;
        private bool _inMenuScene;
        private bool _inGameplayScene;

        private void Awake()
        {
            if (_instance != null && _instance != this)
            {
                Destroy(gameObject);
                return;
            }
            _instance = this;
            DontDestroyOnLoad(gameObject);

            _sourceA = AdoptOrCreateSourceA();
            _sourceB = CreateSourceB();

            SceneManager.sceneLoaded += OnSceneLoaded;
            EvaluateScene(SceneManager.GetActiveScene().name);
        }

        private void OnDestroy()
        {
            if (_instance == this) _instance = null;
            SceneManager.sceneLoaded -= OnSceneLoaded;
        }

        /// <summary>Bootstrap ile oluşturulan GameObject'te zaten AudioSource varsa (örn. ileride
        /// biri sahneye elle bir tane eklerse) onu benimser; yoksa yenisini kurar. İkisinde de
        /// aynı temiz duruma sıfırlanır — eski atanmış clip/otomatik oynatma miras alınmaz.</summary>
        private AudioSource AdoptOrCreateSourceA()
        {
            var existing = GetComponent<AudioSource>();
            if (existing == null) existing = gameObject.AddComponent<AudioSource>();
            ConfigureSource(existing);
            return existing;
        }

        private AudioSource CreateSourceB()
        {
            var go = new GameObject("MusicSource_B");
            go.transform.SetParent(transform, false);
            var src = go.AddComponent<AudioSource>();
            ConfigureSource(src);
            return src;
        }

        private static void ConfigureSource(AudioSource src)
        {
            if (src.isPlaying) src.Stop();
            src.clip = null;
            src.playOnAwake = false;
            src.loop = false;
            src.spatialBlend = 0f;
            src.volume = 0f;
            AudioRouting.Route(src, AudioCategory.Music);
        }

        private void OnSceneLoaded(Scene scene, LoadSceneMode mode) => EvaluateScene(scene.name);

        private void EvaluateScene(string sceneName)
        {
            _inMenuScene = Array.IndexOf(menuSceneNames, sceneName) >= 0;
            _inGameplayScene = Array.IndexOf(gameplaySceneNames, sceneName) >= 0;

            if (!_inMenuScene && !_inGameplayScene)
            {
                // Ne menü ne gameplay (örn. IntroScene/OnlineRoom) — müziği durdur, o sahne kendi sesini yönetsin.
                StopAll();
                _currentFamily = null;
                return;
            }

            if (_inMenuScene)
            {
                if (_currentFamily != MusicFamily.Menu) SwitchFamily(MusicFamily.Menu);
                return;
            }

            // Gameplay sahnesi: DayCycleManager henüz spawn olmamış olabilir (netcode) —
            // EvaluateGameplayPhase null-safe, Main'e düşer. Update'i beklemeden hemen dene ki
            // sahne geçişinde 1 sn'lik phasePollInterval kadar sessizlik/menü müziği sarkmasın.
            EvaluateGameplayPhase();
            _lastPhasePoll = Time.unscaledTime;
        }

        private void Update()
        {
            if (_crossfading)
            {
                TickCrossfade();
                return; // geçiş sürerken faz kontrolü/parça bitişi kontrolünü atla
            }

            if (_inGameplayScene && Time.unscaledTime - _lastPhasePoll >= phasePollInterval)
            {
                _lastPhasePoll = Time.unscaledTime;
                EvaluateGameplayPhase();
            }

            CheckTrackNearingEnd();
        }

        private void EvaluateGameplayPhase()
        {
            var dayCycle = DayCycleManager.Instance;
            if (dayCycle == null)
            {
                if (_currentFamily != MusicFamily.Main) SwitchFamily(MusicFamily.Main);
                return;
            }

            int queueSize = CustomerManager.Instance != null ? CustomerManager.Instance.QueueSize : 0;

            var family = MusicPhaseSelector.SelectGameplayFamily(
                dayCycle.IsDayOver,
                dayCycle.elapsedTime,
                dayCycle.CurrentDayDuration,
                queueSize,
                phaseThresholds);

            if (family != _currentFamily) SwitchFamily(family);
        }

        private void SwitchFamily(MusicFamily family)
        {
            var clips = MusicLibrary.GetTracks(family);
            _currentQueue = new MusicQueue(clips, _rng);
            _currentFamily = family;

            var next = _currentQueue.Next();
            if (next == null) return; // klasör boş — MusicLibrary zaten uyardı, burada sessiz kal

            BeginCrossfadeTo(next);
        }

        private void CheckTrackNearingEnd()
        {
            var active = _usingA ? _sourceA : _sourceB;
            if (active.clip == null || !active.isPlaying) return;

            float startAt = MusicCrossfadeMath.CrossfadeStartTime(active.clip.length, crossfadeDuration);
            if (active.time >= startAt)
            {
                var next = _currentQueue?.Next();
                if (next != null) BeginCrossfadeTo(next);
            }
        }

        private void BeginCrossfadeTo(AudioClip clip)
        {
            var incoming = _usingA ? _sourceB : _sourceA;
            incoming.clip = clip;
            incoming.volume = 0f;
            incoming.time = 0f;
            incoming.Play();

            _crossfading = true;
            _crossfadeElapsed = 0f;
        }

        private void TickCrossfade()
        {
            _crossfadeElapsed += Time.deltaTime;
            float t = crossfadeDuration > 0f ? _crossfadeElapsed / crossfadeDuration : 1f;

            var outgoing = _usingA ? _sourceA : _sourceB;
            var incoming = _usingA ? _sourceB : _sourceA;

            MusicCrossfadeMath.GetVolumes(t, out float outVol, out float inVol);
            if (outgoing.isPlaying) outgoing.volume = outVol;
            incoming.volume = inVol;

            if (t >= 1f)
            {
                if (outgoing.isPlaying) outgoing.Stop();
                outgoing.clip = null;
                _usingA = !_usingA;
                _crossfading = false;
            }
        }

        private void StopAll()
        {
            if (_sourceA.isPlaying) _sourceA.Stop();
            if (_sourceB.isPlaying) _sourceB.Stop();
            _crossfading = false;
        }
    }
}
