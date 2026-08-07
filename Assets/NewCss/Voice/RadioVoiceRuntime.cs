using Steamworks;
using Unity.Netcode;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Telsiz sisteminin kendi kendini kuran DontDestroyOnLoad bootstrap singleton'ı. Düz MonoBehaviour
/// (NetworkBehaviour DEĞİL, NetworkObject YOK) — desen WorldSpaceCanvasCameraBinder.cs:26-32'den
/// birebir kopyalandı.
///
/// NEDEN sahne-yerleşimli bir obje DEĞİL: Tutorial sahnesinde NetworkManager/GameStateManager YOK
/// (grep ile teyitli) — sahne-yerleşimli bir obje iki sahnede iki ayrı wiring ister. Ayrıca oyuncu
/// spawn'ı asenkron (PlayerSpawner.cs) — PTT'ye spawn'dan önce basılabilir. Düz MonoBehaviour'dan
/// CustomMessagingManager'a (Dalga 3) erişilebildiği için NetworkObject/ownership/OnNetworkSpawn
/// timing'ine hiç ihtiyaç yok — bu, UpgradePanel.cs:361-378'de belgelenmiş "geç-join'de NetworkList
/// replay etmiyor" bug sınıfını baştan devre dışı bırakıyor: ses akışında senkronize edilecek kalıcı
/// durum yok, sadece geçici veri var.
///
/// DALGA 3 (Adım 6): RadioVoiceCapture'ın ürettiği paket artık SelfMonitor loopback'e EK olarak
/// <see cref="RadioVoiceTransport"/> üzerinden ağa da çıkıyor (bkz. OnCapturedPacket) — capture/
/// playback'in kendisi HİÇ değişmedi, sadece OnCapturedPacket'e ikinci bir tüketici eklendi.
///
/// NetworkManager.Singleton Awake sırasında yoktur/sonradan gelir (Tutorial'da hiç yok, diğer
/// sahnelerde asenkron kurulur) — bu yüzden Transport'un ağ handler kaydı NetworkManager'ın kendi
/// OnClientStarted/OnServerStarted event'lerine bağlanıyor, Awake'te DEĞİL (CustomMessagingManager
/// NetworkManager başlamadan null). Update() her frame NetworkManager.Singleton'ın DEĞİŞİP
/// DEĞİŞMEDİĞİNİ kontrol eder (SyncNetworkLifecycleHook) — sahneler arası yeniden host/join
/// senaryosunda yeni bir NetworkManager örneği gelirse eski event aboneliği söküp yeniye geçilir.
/// </summary>
public sealed class RadioVoiceRuntime : MonoBehaviour
{
    /// <summary>Kendini dinleme (loopback) için rezerve pseudo-clientId. Gerçek NGO clientId'leri
    /// (Dalga 3) küçük, host-atanan tam sayılardır — ulong.MaxValue asla çakışmaz.</summary>
    public const ulong SelfMonitorClientId = ulong.MaxValue;

    /// <summary>DevTools disk-replay'i için rezerve pseudo-clientId (bkz. RadioVoiceDevTools). SelfMonitorClientId'den
    /// farklı olmalı ki ikisi aynı anda test edilirse aynı slotu paylaşmasınlar.</summary>
    public const ulong DevReplayClientId = ulong.MaxValue - 1;

    private const int SpeakerSlotCount = 3;

    public static RadioVoiceRuntime Instance { get; private set; }

    public RadioVoiceCapture Capture { get; private set; }
    public RadioVoicePlayback Playback { get; private set; }
    public RadioVoiceTransport Transport { get; private set; }

    private NetworkManager _hookedNetworkManager; // Update()'te NetworkManager.Singleton'ın değişip değişmediğini tespit etmek için

    private AudioSource _localClickSource;
    private AudioClip _pttOnClip;
    private AudioClip _pttOffClip;
    private bool _warnedMissingPttClickThisSession;

    private bool _teardownDone;
    private bool _finalDisposed;
    private bool _lastSteamValid;
    private bool _warnedNoSteamThisSession;
    private RadioVoiceCapture.CaptureState _prevCaptureState = RadioVoiceCapture.CaptureState.Disabled;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return; // DontDestroyOnLoad sahneler arası hayatta kalır, tekrar kurulmasın

        var go = new GameObject("[RadioVoiceRuntime]");
        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
        go.AddComponent<RadioVoiceRuntime>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        Capture = new RadioVoiceCapture();
        Capture.PacketProduced += OnCapturedPacket;

        int initialSampleRate = ResolveTargetSampleRate();

        var slots = new RadioVoiceSpeakerSlot[SpeakerSlotCount];
        for (int i = 0; i < SpeakerSlotCount; i++)
        {
            slots[i] = RadioVoiceSpeakerSlot.CreateSlot(transform, i, initialSampleRate);
        }
        Playback = new RadioVoicePlayback(slots);
        Transport = new RadioVoiceTransport(Playback);

        _localClickSource = gameObject.AddComponent<AudioSource>();
        _localClickSource.spatialBlend = 0f;
        _localClickSource.playOnAwake = false;
        _localClickSource.loop = false;
        _localClickSource.priority = 0;
        // Klip asset'leri HENÜZ YOK (bkz. plans/telsiz-editor-isleri.md) — null-guard PlayLocalClick içinde.
        _pttOnClip = Resources.Load<AudioClip>("Voice/Clicks/PttOn");
        _pttOffClip = Resources.Load<AudioClip>("Voice/Clicks/PttOff");

        AudioSettings.OnAudioConfigurationChanged += OnAudioConfigurationChanged;
        SceneManager.activeSceneChanged += OnActiveSceneChanged;

        ApplySteamSampleRateIfValid(initialSampleRate);
    }

    private void Update()
    {
        // TeardownVoice yorumundaki gibi: bu obje DontDestroyOnLoad olduğu için sahne değişiminden
        // sağ çıkar. Bayrak burada her frame otomatik sıfırlanır — yalnızca "aynı frame içinde birden
        // fazla teardown kancasının üst üste binmesini" bastırır, kalıcı bir kilit DEĞİLDİR.
        _teardownDone = false;

        SyncNetworkLifecycleHook();

        bool steamValid = SteamClient.IsValid;
        if (steamValid && !_lastSteamValid)
        {
            // Steam az önce geçerli hâle geldi (SteamManager'ın asenkron InitSteamworks'ü bitti) —
            // encoder/decoder'ın kullanacağı örnekleme hızını şimdi uygula. Update her frame zaten
            // bu bool'u okuduğu için ayrı bir "5s'lik yavaş retry" zamanlayıcısına gerek yok — geçiş
            // aynı frame içinde anında yakalanıyor (bkz. rapor: plandan bilinçli sadeleştirme).
            ApplySteamSampleRateIfValid(ResolveTargetSampleRate());
        }
        if (!steamValid && !_warnedNoSteamThisSession)
        {
            Debug.LogWarning("[RadioVoiceRuntime] Steam bağlantısı yok — telsiz devre dışı. (oturum başına bir kez loglanır; Steam geçerli olur olmaz otomatik devreye girer)");
            _warnedNoSteamThisSession = true;
        }
        _lastSteamValid = steamValid;

        bool preconditionsOk = steamValid && RadioVoicePrefs.IsEnabled();
        bool pttHeld = preconditionsOk && InputBindingManager.GetAction(InputBindingManager.GameAction.PushToTalk);

        double now = Time.unscaledTimeAsDouble; // Time.time/deltaTime YASAK — duraklatma/oyun-sonu timeScale=0 yapıyor
        Capture.Tick(now, preconditionsOk, pttHeld);

        HandleLocalPttClickFeedback();

        Playback.Tick(now);
    }

    /// <summary>PTT basınca/bırakınca yerelde kısık klik — "mikrofonum açık mı" sorusunun tek cevabı.
    /// Bu, konuşmacı slot'larının BurstStart/BurstEnd click'inden AYRI bir mekanizma: SelfMonitor
    /// kapalıyken bile (yani ses hiçbir slot'a gitmese de) çalışır, çünkü doğrudan durum makinesi
    /// geçişini dinler.</summary>
    private void HandleLocalPttClickFeedback()
    {
        var state = Capture.State;
        if (state == _prevCaptureState) return;

        bool startedTransmitting = state == RadioVoiceCapture.CaptureState.Transmitting && _prevCaptureState != RadioVoiceCapture.CaptureState.Tail;
        bool stoppedTransmitting = state == RadioVoiceCapture.CaptureState.Idle && _prevCaptureState == RadioVoiceCapture.CaptureState.Tail;

        if (startedTransmitting) PlayLocalClick(_pttOnClip);
        if (stoppedTransmitting) PlayLocalClick(_pttOffClip);

        _prevCaptureState = state;
    }

    private void PlayLocalClick(AudioClip clip)
    {
        if (clip == null)
        {
            if (!_warnedMissingPttClickThisSession)
            {
                Debug.LogWarning("[RadioVoiceRuntime] Yerel PTT klik SFX asset'i atanmamış (Resources/Voice/Clicks/PttOn|PttOff) — sessiz geçiliyor. (oturum başına bir kez loglanır)");
                _warnedMissingPttClickThisSession = true;
            }
            return;
        }
        _localClickSource.PlayOneShot(clip, RadioVoicePrefs.GetVolume());
    }

    /// <summary>
    /// Yakalanan her paket buraya düşer. SelfMonitor açıksa doğrudan yerel bir playback slot'una
    /// beslenir (bu dalganın TEK doğrulama yolu — ağ henüz yok). Dalga 3, aynı 'packet' segmentini
    /// burada CustomMessagingManager'a da verecek; bu event ağdan bağımsız olduğu için tüketiciler
    /// (loopback burada, ağ Dalga 3'te) birbirinden habersiz eklenir.
    /// </summary>
    private void OnCapturedPacket(System.ArraySegment<byte> packet)
    {
        if (RadioVoicePrefs.IsSelfMonitorEnabled())
        {
            Playback.HandleIncomingPacket(SelfMonitorClientId, packet);
        }

        // Loopback'e EK — yerine geçmiyor. Transport ağ yoksa/tek oyuncuysa kendi içinde sessizce
        // düşürür (bkz. RadioVoiceTransport.SendCapturedPacket), burası hiçbir koşul kontrol etmez.
        Transport.SendCapturedPacket(packet);
    }

    /// <summary>
    /// NetworkManager.Singleton Awake sırasında yoktur/sonradan asenkron kurulur (Tutorial'da hiç
    /// yok) — bu yüzden Transport'un ağ handler kaydı buradan, her frame Singleton'ın değişip
    /// değişmediğine bakılarak, NetworkManager'ın KENDİ Started/Stopped event'lerine bağlanır.
    /// Farklı bir NetworkManager örneği gelirse (sahneler arası yeniden host/join) eski abonelik
    /// söküp yeniye geçilir — aksi hâlde eski (yok olmuş) örneğe asılı kalınır ve yeni oturumda
    /// telsiz hiç ağa çıkmaz.
    /// </summary>
    private void SyncNetworkLifecycleHook()
    {
        var nm = NetworkManager.Singleton;
        if (nm == _hookedNetworkManager) return;

        UnhookNetworkLifecycle();

        _hookedNetworkManager = nm;
        if (_hookedNetworkManager != null)
        {
            _hookedNetworkManager.OnClientStarted += HandleNetClientStarted;
            _hookedNetworkManager.OnServerStarted += HandleNetServerStarted;
            _hookedNetworkManager.OnClientStopped += HandleNetClientStopped;
            _hookedNetworkManager.OnServerStopped += HandleNetServerStopped;
        }
    }

    private void UnhookNetworkLifecycle()
    {
        if (_hookedNetworkManager == null) return;

        _hookedNetworkManager.OnClientStarted -= HandleNetClientStarted;
        _hookedNetworkManager.OnServerStarted -= HandleNetServerStarted;
        _hookedNetworkManager.OnClientStopped -= HandleNetClientStopped;
        _hookedNetworkManager.OnServerStopped -= HandleNetServerStopped;
        _hookedNetworkManager = null;
    }

    private void HandleNetClientStarted() => Transport.OnNetworkClientStarted();
    private void HandleNetServerStarted() => Transport.OnNetworkServerStarted();

    /// <summary>
    /// SteamUser.VoiceRecord süreç-global — ağ koparsa/durursa mikrofonun açık kalmaması için
    /// TeardownVoice buradan da çağrılıyor (Dalga 2 bunu bilerek dışarıda bırakmıştı, bu dalganın işi).
    /// </summary>
    private void HandleNetClientStopped(bool wasHost)
    {
        Transport.OnNetworkClientStopped();
        TeardownVoice();
    }

    private void HandleNetServerStopped(bool wasClient)
    {
        Transport.OnNetworkServerStopped();
        TeardownVoice();
    }

    private static int ResolveTargetSampleRate()
    {
        int outputRate = AudioSettings.outputSampleRate;
        if (outputRate > 0) return outputRate;

        // outputSampleRate bazı headless/no-audio-device senaryolarda 0 dönebilir — Steam'in
        // önerdiği hıza düş (plan riski #2: SampleRate setter'ı Steam probunda doğrulandı, var).
        if (SteamClient.IsValid)
        {
            uint optimal = SteamUser.OptimalSampleRate;
            if (optimal > 0) return (int)optimal;
        }

        return 48000; // son çare — hem outputSampleRate hem Steam sinyali yoksa; normal oyun akışında buraya düşülmemeli
    }

    private void ApplySteamSampleRateIfValid(int sampleRate)
    {
        if (!SteamClient.IsValid) return;
        SteamUser.SampleRate = (uint)sampleRate; // encoder VE decoder aynı process-global değeri kullanır (bkz. Steam API probu)
    }

    private void OnAudioConfigurationChanged(bool deviceWasChanged)
    {
        int sampleRate = ResolveTargetSampleRate();
        ApplySteamSampleRateIfValid(sampleRate);
        Playback.Reconfigure(sampleRate);
    }

    private void OnActiveSceneChanged(Scene previous, Scene next) => TeardownVoice();
    private void OnDisable() => TeardownVoice();

    private void OnDestroy()
    {
        TeardownVoice();
        FinalDispose();
    }

    private void OnApplicationQuit()
    {
        TeardownVoice();
        FinalDispose();
    }

    /// <summary>
    /// TEK, İDEMPOTENT teardown noktası. SteamUser.VoiceRecord süreç-global ve mikrofonu fiziksel
    /// olarak açık tutar — bu yüzden tek bir kaçan çağrı yolu = ana menüye dönüldükten sonra
    /// mikrofonun açık kalması. Desen UpgradePanel.cs:346-353 (OnDestroy) + :438-451 (OnNetworkDespawn)
    /// ile aynı: birden çok yaşam döngüsü kancasından çağrılır, hepsi idempotent olduğu için
    /// zararsızca üst üste biner.
    ///
    /// _teardownDone bayrağı yalnızca "aynı frame içinde birden fazla kancadan tetiklenme" durumunu
    /// bastırır (örn. OnDisable + OnDestroy aynı frame'de) — kalıcı bir kilit DEĞİL: Update() başında
    /// her frame otomatik sıfırlanır, böylece activeSceneChanged her sahne değişiminde yeniden
    /// mikrofonu güvenle kapatabilir (obje DontDestroyOnLoad olduğu için sahne geçişinden sağ çıkar).
    /// </summary>
    private void TeardownVoice()
    {
        if (_teardownDone) return;
        _teardownDone = true;

        Capture?.ForceStopImmediate(); // VoiceRecord=false — içinde de SteamClient.IsValid guard var

        // Transport'un AĞ HANDLER KAYDINA (Register/Unregister) buradan DOKUNULMUYOR — o kayıt
        // SADECE HandleNetClientStopped/HandleNetServerStopped ile 1:1 eşleşir (bkz. Transport.
        // OnNetworkClientStopped yorumu). Burada sadece ağdan bağımsız, her zaman güvenli bir
        // bellek hijyeni: yarım kalmış reassembly durumu temizlenir.
        Transport?.ClearReassemblyState();

        // Adım 10 denetimi bulgusu: bu satır ÖNCEDEN YOKTU — sahne değişiminde mikrofon kapanıyordu
        // ama çalmakta olan uzak konuşmacı slotları (ring buffer + AssignedSpeakerId) hayatta
        // kalıyordu, yani önceki sahnenin sesi yeni sahnede birkaç yüz ms daha çalmaya devam
        // edebilirdi (plan §Yaşam döngüsü: "Sahne geçişi → tüm slotlar temizlenir, yayın zorla
        // durur"). Mute seti KASITLI olarak buradan temizlenmiyor (bkz. RadioVoicePlayback.ClearAllSlots).
        Playback?.ClearAllSlots();
    }

    /// <summary>
    /// GERÇEK son temizlik — sadece obje TAMAMEN yok olurken (OnDestroy/OnApplicationQuit) çalışır.
    /// TeardownVoice'un aksine buradaki event unsubscribe'lar KALICIDIR — bu yüzden activeSceneChanged
    /// gibi TeardownVoice'u TETİKLEYEN event'lerin abonelikleri BURADA çözülür, TeardownVoice'un
    /// içinde DEĞİL (aksi hâlde ilk sahne değişiminden sonra kendi kendini susturur ve bir daha hiç
    /// tetiklenmez — bu, farkına vararak kaçınılan bir tasarım hatasıdır).
    /// </summary>
    private void FinalDispose()
    {
        if (_finalDisposed) return;
        _finalDisposed = true;

        if (Capture != null) Capture.PacketProduced -= OnCapturedPacket;
        AudioSettings.OnAudioConfigurationChanged -= OnAudioConfigurationChanged;
        SceneManager.activeSceneChanged -= OnActiveSceneChanged;
        UnhookNetworkLifecycle();

        if (Instance == this) Instance = null;
    }
}
