using System.Collections.Generic;
using NewCss;
using NewCss.Voice.Core;
using Steamworks;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// Telsiz HUD'u — "kim konuşuyor" listesi + kendi mikrofon durumu + oturum-içi mute (plan Adım 7).
///
/// KENDİ Screen-Space-Overlay Canvas'ı, bootstrap'ta koddan kurulur + DontDestroyOnLoad. Desen
/// <see cref="RadioVoiceRuntime"/>/<see cref="RadioVoiceSpeakerSlot.CreateSlot"/> ile BİREBİR aynı:
/// opsiyonel prefab override (Resources/Voice/RadioHUD) varsa ondan Instantiate edilir, yoksa tüm
/// UI ağacı koddan kurulur — sistem authoring'siz BUGÜN çalışır, tasarımcı sonradan prefab'ı
/// güzelleştirebilir (bkz. plans/telsiz-editor-isleri.md §5).
///
/// NEDEN KENDİ Canvas'ı / bootstrap singleton (sahne-yerleşimli bir HUD DEĞİL): Tutorial ve The Main
/// Office ayrı sahneler, Tutorial'da NetworkManager/GameStateManager YOK — sahne-yerleşimli bir HUD
/// iki sahnede iki ayrı wiring ister. RadioVoiceRuntime zaten bu deseni kurmuş; HUD BİLEREK AYNI
/// bootstrap singleton'ı DEĞİL, kendi ayrı DontDestroyOnLoad objesini kullanıyor — UI ile ses
/// alt-sistemi hayat döngüsü olarak bağımsız (biri olmadan diğeri de çalışabilir, örn. Runtime henüz
/// kurulmadıysa HUD boş satırlarla sessizce beklemeye devam eder, null-guard'lı).
///
/// NEDEN EKRAN-UZAYI LİSTE, DÜNYA-UZAYI ETİKET DEĞİL: bu iş bittikten sonra ODA-BAZLI GÖRÜNÜRLÜK
/// gelecek (aynı odada olmayan oyuncular birbirini görmeyecek) — dünya-uzayı bir etiket TAM DA
/// telsizin gerekli olduğu anda (konuşmacı görünmezken) kaybolurdu. Köşeye çapalanmış, sabit
/// ekran-uzayı bir liste bu sorunu baştan önlüyor (plan §Context + §UI ve ayarlar).
/// </summary>
public sealed class RadioHudController : MonoBehaviour
{
    private const int RemoteRowPoolSize = 3; // RadioVoicePlayback'in kendi slot havuzuyla (SpeakerSlotCount=3) aynı tavan
    private const double RowHoldSeconds = 0.3; // plan: "burst bittikten sonra satır ~300ms tutulur"
    private const double RowFadeSeconds = 0.2; // ardından kısa fade — tıkırdayan akış listeyi yanıp söndürmesin
    private const float RowHeight = 30f;
    private const float RowSpacing = 4f;
    private const float ContainerMargin = 16f;
    private const float ContainerWidth = 260f;

    public static RadioHudController Instance { get; private set; }

    [Header("Opsiyonel prefab override — dolu bırakılırsa koddan KURULMAZ, bkz. sınıf yorumu")]
    [SerializeField] private RectTransform rowContainer;
    [SerializeField] private RadioSpeakerRow rowPrefab;

    private readonly List<RadioSpeakerRow> _freeRemoteRows = new(RemoteRowPoolSize);
    private readonly Dictionary<ulong, RadioSpeakerRow> _activeRemoteRows = new();
    private readonly Dictionary<ulong, VoiceHudRowTimer> _remoteTimers = new();
    private readonly List<ulong> _activeSpeakerScratch = new(RemoteRowPoolSize);
    private readonly List<ulong> _removeScratch = new(RemoteRowPoolSize);

    private RadioSpeakerRow _ownRow;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return; // DontDestroyOnLoad sahneler arası hayatta kalır, tekrar kurulmasın

        var prefabOverride = Resources.Load<GameObject>("Voice/RadioHUD");
        GameObject go;
        if (prefabOverride != null)
        {
            go = Instantiate(prefabOverride);
        }
        else
        {
            go = new GameObject("[RadioHudController]");
        }

        // Her iki dalda da GEREKİR: prefab senaryosunda tasarımcı component'i prefab'a elle
        // eklemeyi unutmuş olabilir (savunma), koddan kurulum senaryosunda ise bu YEGÂNE ekleme
        // noktasıdır — RadioVoiceRuntime.Bootstrap ile aynı desen (go.AddComponent EN SONA değil,
        // ama component eksikse eklenmesi ZORUNLU).
        if (go.GetComponent<RadioHudController>() == null) go.AddComponent<RadioHudController>();

        go.hideFlags = HideFlags.HideAndDontSave;
        DontDestroyOnLoad(go);
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;

        if (rowContainer == null) BuildCanvasAndContainerFromCode();

        _ownRow = CreateRow();
        _ownRow.SetClickable(false, null); // kendini mute etmek anlamsız

        for (int i = 0; i < RemoteRowPoolSize; i++)
        {
            var row = CreateRow();
            row.SetClickable(true, OnRowClicked);
            row.SetVisible(false);
            _freeRemoteRows.Add(row);
        }

        // Tüm satır metinleri zaten HER frame Update()'te yeniden hesaplanıyor (ResolveSpeakerName/
        // FormatTransmittingLabel/... her çağrıda LocalizationHelper'a taze sorar) — bu yüzden dil
        // değişince ekstra bir "refresh" adımı GEREKMİYOR, bir sonraki frame otomatik doğru dilde
        // yazar. Buna rağmen abone oluyoruz (plan açık talimatı) çünkü bu, ileride bir satırın
        // metnini önbelleğe alacak bir değişiklik yapılırsa (örn. performans için) sessizce
        // bozulacak bir varsayımı EZBERE değil, gözle görülür bir kanca üzerinden koruma altına alır.
        LocalizationHelper.OnLocaleChanged += HandleLocaleChanged;
    }

    private void OnDestroy()
    {
        LocalizationHelper.OnLocaleChanged -= HandleLocaleChanged;
        if (Instance == this) Instance = null;
    }

    private void HandleLocaleChanged()
    {
        // Kasıtlı no-op gövde — bkz. Awake'teki abonelik yorumu: Update() her frame zaten taze
        // localization sorgusu yapıyor, burada ekstra bir şey yapılmasına gerek yok.
    }

    private void Update()
    {
        double now = Time.unscaledTimeAsDouble; // duraklatma/oyun-sonu Time.timeScale=0 yapıyor (bkz. RadioVoiceRuntime) — HUD da aynı saati kullanmalı, aksi halde satırlar duraklatmada donar/yanlış solar

        UpdateOwnRow();
        UpdateRemoteRows(now);
    }

    // -----------------------------------------------------------------------------------------
    // Kendi mikrofon satırı — HER ZAMAN görünür (plan: "her zaman görünür küçük gösterge").
    // Hold/fade zamanlayıcısı BİLEREK kullanılmıyor: bu satır asla kaybolmaz, sadece durumu değişir.
    // -----------------------------------------------------------------------------------------

    private void UpdateOwnRow()
    {
        _ownRow.SetVisible(true);
        _ownRow.SetAlpha(1f);

        var runtime = RadioVoiceRuntime.Instance;

        if (!SteamClient.IsValid)
        {
            // Adım 10: Steam geçersiz — RadioVoiceRuntime zaten bunu oturum başına bir kez loglar,
            // burada SÜREKLİ (log spam değil, HUD durumu) gösteriliyor ki oyuncu "telsiz neden
            // çalışmıyor" sorusuna sahnede cevap bulsun.
            _ownRow.SetLabel(ResolveLocalizedOrFallback("VoiceErrorNoSteam", "Steam bağlantısı yok — telsiz kullanılamıyor"), dimmed: true);
            _ownRow.SetLevel01(0f);
            return;
        }

        if (runtime != null && runtime.Capture.IsMicDegraded)
        {
            _ownRow.SetLabel(ResolveLocalizedOrFallback("VoiceErrorNoMic", "Mikrofon bulunamadı"), dimmed: true);
            _ownRow.SetLevel01(0f);
            return;
        }

        string ownName = SteamClient.Name; // roster'a gerek yok — kendi Steam adın zaten elinde (GameStateManager.BuildLocalRosterName de aynı kaynağı kullanıyor)
        bool transmitting = runtime != null &&
            (runtime.Capture.State == RadioVoiceCapture.CaptureState.Transmitting ||
             runtime.Capture.State == RadioVoiceCapture.CaptureState.Tail);

        if (!transmitting)
        {
            _ownRow.SetLabel(ownName, dimmed: false);
            _ownRow.SetLevel01(0f);
            return;
        }

        _ownRow.SetLabel(FormatTransmittingLabel(ownName), dimmed: false);

        // Seviye çubuğu SADECE "Kendini Dinle" açıkken gerçek RMS gösterir (plan §UI ve ayarlar:
        // "yerelde yalnız sıkıştırılmış veri var, RMS için kendi sesimizi de decompress etmek
        // gerekir → kozmetik bir çubuk için gereksiz. Karar: ikili aç/kapa; RMS yalnız Kendini
        // Dinle açıkken, o modda decompress zaten yapılıyor, bedava"). Kapalıyken ikili gösterge: dolu çubuk.
        float level = 1f;
        if (RadioVoicePrefs.IsSelfMonitorEnabled() && runtime != null)
        {
            runtime.Playback.TryGetSpeakerLevel(RadioVoiceRuntime.SelfMonitorClientId, out level);
        }
        _ownRow.SetLevel01(level);
    }

    // -----------------------------------------------------------------------------------------
    // Uzak konuşmacı satırları — havuzlanmış, hold/fade ile titremesiz.
    // -----------------------------------------------------------------------------------------

    private void UpdateRemoteRows(double now)
    {
        var runtime = RadioVoiceRuntime.Instance;
        _activeSpeakerScratch.Clear();
        if (runtime != null) runtime.Playback.CollectActiveSpeakers(_activeSpeakerScratch);

        for (int i = 0; i < _activeSpeakerScratch.Count; i++)
        {
            ulong speakerId = _activeSpeakerScratch[i];

            // Kendini Dinle / dev-replay pseudo-ID'leri gerçek konuşmacı satırı DEĞİL — kendi mikrofon
            // satırı (own row) SelfMonitor'ı zaten ayrı okuyor, burada ikinci bir satır açılmasın.
            if (speakerId == RadioVoiceRuntime.SelfMonitorClientId || speakerId == RadioVoiceRuntime.DevReplayClientId)
                continue;

            if (!_remoteTimers.TryGetValue(speakerId, out var timer))
            {
                timer = new VoiceHudRowTimer(RowHoldSeconds, RowFadeSeconds);
                _remoteTimers[speakerId] = timer;
            }
            timer.MarkActive(now);

            if (!_activeRemoteRows.TryGetValue(speakerId, out var row))
            {
                row = AcquireRemoteRow();
                if (row == null) continue; // havuz dolu (>3 eşzamanlı) — RadioVoicePlayback'in kendi tavanıyla aynı, pratikte olmaz
                row.Assign(speakerId);
                row.SetVisible(true);
                _activeRemoteRows[speakerId] = row;
            }

            bool muted = runtime.Playback.IsMuted(speakerId);
            string name = ResolveSpeakerName(speakerId);
            row.SetLabel(muted ? FormatMutedLabel(name) : FormatTransmittingLabel(name), dimmed: muted);

            float level = 0f;
            if (!muted) runtime.Playback.TryGetSpeakerLevel(speakerId, out level);
            row.SetLevel01(level);
        }

        _removeScratch.Clear();
        foreach (var kvp in _activeRemoteRows)
        {
            ulong speakerId = kvp.Key;
            var row = kvp.Value;
            var timer = _remoteTimers[speakerId];

            row.SetAlpha(timer.ComputeAlpha(now));

            if (timer.IsFullyFaded(now))
            {
                ReleaseRemoteRow(row);
                _removeScratch.Add(speakerId);
            }
        }

        for (int i = 0; i < _removeScratch.Count; i++)
        {
            ulong id = _removeScratch[i];
            _activeRemoteRows.Remove(id);
            _remoteTimers.Remove(id);
        }
    }

    private RadioSpeakerRow AcquireRemoteRow()
    {
        int lastIndex = _freeRemoteRows.Count - 1;
        if (lastIndex < 0) return null;

        var row = _freeRemoteRows[lastIndex];
        _freeRemoteRows.RemoveAt(lastIndex);
        return row;
    }

    private void ReleaseRemoteRow(RadioSpeakerRow row)
    {
        row.Assign(null);
        row.SetVisible(false);
        row.SetLevel01(0f);
        _freeRemoteRows.Add(row);
    }

    private void OnRowClicked(RadioSpeakerRow row)
    {
        if (!row.SpeakerId.HasValue) return;

        var runtime = RadioVoiceRuntime.Instance;
        if (runtime == null) return;

        bool newMuted = !runtime.Playback.IsMuted(row.SpeakerId.Value);
        runtime.Playback.SetMuted(row.SpeakerId.Value, newMuted);
        // Satırın metni/rengi bir sonraki UpdateRemoteRows çağrısında (bu frame'in geri kalanında
        // değil, ama en fazla 1 frame gecikmeyle) otomatik güncellenir — burada ayrıca yazmaya
        // gerek yok, tek doğruluk kaynağı RadioVoicePlayback._mutedSpeakers.
    }

    // -----------------------------------------------------------------------------------------
    // İsim / metin çözümleme — LocalizationHelper.GetLocalizedString bulamadığı anahtarı olduğu
    // gibi döndürür (Assets/NewCss/Localization/LocalizationHelper.cs:79-105) — StringTable'a 10
    // anahtar authoring'i tamamlanana kadar (bkz. plans/telsiz-editor-isleri.md §1) burada HER
    // zaman "resolved == key" kontrolüyle sabit Türkçe fallback'e düşülüyor (InputBindingManager.
    // ResolvePushToTalkDisplayName ile AYNI desen) — ekranda çıplak anahtar adı GÖRÜNMEZ.
    // -----------------------------------------------------------------------------------------

    private static string ResolveSpeakerName(ulong speakerId)
    {
        var gsm = GameStateManager.Instance;
        if (gsm != null && gsm.TryGetRosterName(speakerId, out var name)) return name;

        // Roster'da kayıt yok (Tutorial, spawn öncesi, reconnect ortası) — plan fallback'i.
        const string key = "VoiceHudUnknownPlayer";
        string resolved = LocalizationHelper.GetLocalizedString(key);
        if (resolved != key) return resolved; // authored metin (örn. "Bilinmeyen Oyuncu") {0} içermez, olduğu gibi göster
        return $"Oyuncu {speakerId}";
    }

    private static string FormatTransmittingLabel(string name)
    {
        const string key = "VoiceHudTransmitting";
        string resolved = LocalizationHelper.GetLocalizedString(key);
        string template = resolved == key ? "{0} konuşuyor" : resolved;
        return SafeFormat(template, name);
    }

    private static string FormatMutedLabel(string name)
    {
        const string key = "VoiceHudMuted";
        string resolved = LocalizationHelper.GetLocalizedString(key);
        string template = resolved == key ? "{0} (Sessize Alındı)" : resolved;
        return SafeFormat(template, name);
    }

    private static string ResolveLocalizedOrFallback(string key, string fallback)
    {
        string resolved = LocalizationHelper.GetLocalizedString(key);
        return resolved == key ? fallback : resolved;
    }

    private static string SafeFormat(string template, string name)
    {
        try { return string.Format(template, name); }
        catch (System.FormatException)
        {
            // Editor'de elle yanlış authored bir template ({0} yerine {1} gibi) çıplak isme düşsün —
            // exception'ın HUD'u tamamen bozmasından (her frame Update'te tekrar tekrar atılırdı) iyi.
            return name;
        }
    }

    // -----------------------------------------------------------------------------------------
    // Koddan kurulum — prefab override yoksa (bkz. sınıf yorumu). Canvas + satır container.
    // -----------------------------------------------------------------------------------------

    private void BuildCanvasAndContainerFromCode()
    {
        var canvas = gameObject.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // Yüksek ama "sonsuz" değil bir sortingOrder: telsiz durumu oyun içi HUD'ların üstünde
        // görünmeli ama modal menüler (ayarlar/escape) muhtemelen daha da yüksek bir katmanda —
        // gerçek değer prefab authoring'de sanatçı tarafından ayarlanabilir (bkz. editor-isleri §5).
        canvas.sortingOrder = 500;

        var scaler = gameObject.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);

        gameObject.AddComponent<GraphicRaycaster>();

        var containerGo = new GameObject("SpeakerRows", typeof(RectTransform));
        containerGo.transform.SetParent(transform, false);

        rowContainer = (RectTransform)containerGo.transform;
        rowContainer.anchorMin = new Vector2(0f, 1f);
        rowContainer.anchorMax = new Vector2(0f, 1f);
        rowContainer.pivot = new Vector2(0f, 1f);
        rowContainer.anchoredPosition = new Vector2(ContainerMargin, -ContainerMargin);
        rowContainer.sizeDelta = new Vector2(ContainerWidth, 0f);

        var vlg = containerGo.AddComponent<VerticalLayoutGroup>();
        vlg.childAlignment = TextAnchor.UpperLeft;
        vlg.spacing = RowSpacing;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;
        vlg.childControlHeight = true;
        vlg.childControlWidth = true;

        var fitter = containerGo.AddComponent<ContentSizeFitter>();
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
    }

    private RadioSpeakerRow CreateRow()
    {
        RadioSpeakerRow row;
        if (rowPrefab != null)
        {
            row = Instantiate(rowPrefab, rowContainer);
        }
        else
        {
            var go = new GameObject("SpeakerRow", typeof(RectTransform));
            go.transform.SetParent(rowContainer, false);
            row = go.AddComponent<RadioSpeakerRow>();
        }

        // HER İKİ yolda da çağrılır — RadioSpeakerRow.EnsureBuilt prefab'ta elle bırakılmış
        // referansları bulur, yoksa koddan kurar (bkz. RadioVoiceSpeakerSlot.BuildAudioGraphIfNeeded
        // ile aynı desen).
        row.EnsureBuilt(RowHeight);
        return row;
    }
}
