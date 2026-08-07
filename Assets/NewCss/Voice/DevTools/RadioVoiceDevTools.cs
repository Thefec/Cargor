#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEditor;
using UnityEngine;
using NewCss.Voice.Core;

namespace NewCss.DevTools
{
    /// <summary>
    /// SADECE EDİTÖR — telsiz geliştirici araçları: (1) paket kaydet/oynat (Adım 4b, Dalga 2),
    /// (2) ağ simülatörü + (3) istatistik overlay (Adım 9, Dalga 4). Üçü de BAĞIMSIZ menü
    /// toggle'larıyla açılır, varsayılan HEPSİ KAPALI — yanlışlıkla açık kalıp gerçek Steam
    /// playtest'ini kirletmesin.
    ///
    /// ═══ 1) KAYIT/OYNATMA (Dalga 2, DEĞİŞMEDİ — bkz. aşağıdaki #region Kayıt-Oynatma) ═══
    /// Amaç: aynı makinede/hesapta muhtemelen iki Steam-voice sürecinin (iki Unity instance'ı)
    /// aynı mikrofonu paylaşamaması sorununu aşmak. "Konuşan" taraf canlı mikrofon yerine burada
    /// kaydedilmiş bir akışı OYNATIR — kayıt doğrudan RadioVoicePlayback.HandleIncomingPacket'e
    /// RadioVoiceRuntime.DevReplayClientId ile beslenir.
    ///
    /// ═══ 2) AĞ SİMÜLATÖRÜ (Adım 9) ═══
    /// Gerçek relay gecikmesi/jitter/kayıp oranı normalde YALNIZCA 2 makine + 2 Steam hesabıyla
    /// test edilebiliyor (plan Katman D). Bu araç o testi beklemeden, TEK makinede kötü ağ
    /// koşullarını taklit eder. ALIM yolunda çalışır — RadioVoicePlayback.HandleIncomingPacket'e
    /// giren HER paketi (loopback / disk-replay / gerçek ağ, hepsi aynı giriş noktasından geçiyor,
    /// bkz. RadioVoicePlayback sınıf yorumu) yakalayıp gecikme/jitter/kayıp/dup/sıra-bozma
    /// enjekte ettikten SONRA gerçek işleme fonksiyonuna (HandleIncomingPacketImmediate) besler.
    /// NEDEN ALIM YOLU (gönderim değil): gönderim yolunu bozmak gerçek ağ davranışını taklit
    /// etmez — biz ALICI'nın jitter buffer'ını (VoiceRingBuffer) test etmek istiyoruz.
    /// VoiceSequenceTracker'ın dup/reorder/gap kararları BU YÜZDEN zaten var — simülatör tam o
    /// kod yolunu tetikliyor, yeni bir karar mekanizması İCAT ETMİYOR.
    /// Kanca (RadioVoicePlayback.DevIncomingInterceptor) SADECE #if UNITY_EDITOR içinde var —
    /// PLAYER BUILD'de o kod bloğu tamamen derleme dışı kalır, sıcak yolda (Transport/Capture/
    /// Playback'in üretim davranışı) hiçbir ekstra dallanma maliyeti yoktur.
    ///
    /// ═══ 3) İSTATİSTİK OVERLAY (Adım 9) ═══
    /// Bitrate (gönderilen/alınan KB/s + gözlenen tepe paket boyutu — plan riski #1'in ÖLÇÜMÜ),
    /// slot başına ring buffer doluluğu (ms, 120ms hedefe göre), underrun/overrun/dropped
    /// sayaçları, aktif konuşmacı sayısı, PTT/capture durumu.
    /// 🔴 VoiceRingBuffer'ın sayaçları audio thread'de Interlocked ile artıyor ama BURADA hep ANA
    /// THREAD'den (Update/OnGUI) okunuyor — yeni bir kilit/API EKLENMEDİ, mevcut public sayaçlar
    /// (UnderrunCount/OverrunCount/DroppedSamples/AvailableSamples) kullanılıyor.
    /// 🔴 Overlay'in KENDİSİ ölçtüğü şeyi bozmamasın diye: metin SADECE düşük frekansta (~6 Hz,
    /// bkz. StatsRefreshIntervalSeconds) yeniden StringBuilder ile inşa edilir ve TEK bir
    /// ToString() ile önbelleklenir (_statsDisplayText); OnGUI her repaint'te bu ÖNBELLEĞİ
    /// GÖSTERİR, yeniden birleştirmez. Adım 3'ün kabul kriteri ("PTT basılıyken 0 B/frame alloc")
    /// bu overlay AÇIKKEN de ihlal edilmiyor — sayaç biriktirme (int/long toplama) alloc'suz,
    /// tek allocation 6 Hz'de bir ToString() çağrısı.
    ///
    /// Toggle'lar KAPALIYKEN bu araç hiçbir şey yapmaz (GameObject oluşturulmaz) → normal
    /// playtest'e karışmaz. NOT: [RuntimeInitializeOnLoadMethod] sadece sahne yüklenirken BİR KEZ
    /// kontrol eder — Play BAŞLAMADAN ÖNCE menüden açılması gerekir (mevcut kayıt/oynatma
    /// aracındaki gibi, Play ortasında menüyü ilk kez açmak GameObject'i geriye dönük oluşturmaz).
    /// </summary>
    public class RadioVoiceDevTools : MonoBehaviour
    {
        // ─── Menü toggle'ları — üç bağımsız özellik, üçü de varsayılan KAPALI ──────────────────
        private const string MENU_RECORD = "Tools/Cargor/Voice/Kayıt-Oynatma Aracı";
        private const string MENU_NETSIM = "Tools/Cargor/Voice/Ağ Simülatörü (Alım - Gecikme-Jitter-Kayıp)";
        private const string MENU_STATS = "Tools/Cargor/Voice/İstatistik Overlay";

        private const string PREF_RECORD_ENABLED = "Cargor_VoiceDevTools_Enabled";
        private const string PREF_NETSIM_ENABLED = "Cargor_VoiceNetSim_Enabled";
        private const string PREF_STATS_ENABLED = "Cargor_VoiceStatsOverlay_Enabled";

        [MenuItem(MENU_RECORD)]
        private static void ToggleRecord() => EditorPrefs.SetBool(PREF_RECORD_ENABLED, !IsRecordEnabled());

        [MenuItem(MENU_RECORD, true)]
        private static bool ToggleRecordValidate()
        {
            UnityEditor.Menu.SetChecked(MENU_RECORD, IsRecordEnabled());
            return true;
        }

        private static bool IsRecordEnabled() => EditorPrefs.GetBool(PREF_RECORD_ENABLED, false);

        [MenuItem(MENU_NETSIM)]
        private static void ToggleNetSim() => EditorPrefs.SetBool(PREF_NETSIM_ENABLED, !IsNetSimEnabled());

        [MenuItem(MENU_NETSIM, true)]
        private static bool ToggleNetSimValidate()
        {
            UnityEditor.Menu.SetChecked(MENU_NETSIM, IsNetSimEnabled());
            return true;
        }

        private static bool IsNetSimEnabled() => EditorPrefs.GetBool(PREF_NETSIM_ENABLED, false);

        [MenuItem(MENU_STATS)]
        private static void ToggleStats() => EditorPrefs.SetBool(PREF_STATS_ENABLED, !IsStatsEnabled());

        [MenuItem(MENU_STATS, true)]
        private static bool ToggleStatsValidate()
        {
            UnityEditor.Menu.SetChecked(MENU_STATS, IsStatsEnabled());
            return true;
        }

        private static bool IsStatsEnabled() => EditorPrefs.GetBool(PREF_STATS_ENABLED, false);

        private static readonly string RecordingPath =
            Path.Combine(Application.persistentDataPath, "CargorVoiceRecordings", "capture.bin");

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void AutoBootstrap()
        {
            if (!IsRecordEnabled() && !IsNetSimEnabled() && !IsStatsEnabled()) return;

            var go = new GameObject("[RadioVoiceDevTools]");
            go.AddComponent<RadioVoiceDevTools>();
            DontDestroyOnLoad(go);
        }

        // RadioVoiceRuntime.Instance, bu bileşen [RuntimeInitializeOnLoadMethod] ile aynı
        // AfterSceneLoad aşamasında oluştuğu için henüz null olabilir (script'ler arası çalıştırma
        // sırası garanti değil) — bu yüzden OnEnable'da değil, Update'te "geç bağlanma" ile abone
        // olunuyor: Instance her değiştiğinde (null->var ya da obje yeniden kurulunca) yeniden bağlan.
        private RadioVoiceRuntime _subscribedRuntime;

        // Hangi opsiyonel kancaların şu an takılı olduğu (toggle'lar Play ortasında menüden
        // değişebilir — her frame IsXEnabled() ile senkronlanır, bkz. SyncOptionalHooks).
        private bool _statsSentHooked;
        private bool _statsRecvHooked;
        private bool _netSimHooked;

        private void OnDisable() => UnhookRuntime();

        private void Update()
        {
            var runtime = RadioVoiceRuntime.Instance;
            if (runtime != _subscribedRuntime)
            {
                UnhookRuntime();
                _subscribedRuntime = runtime;
                HookRuntimeIfNeeded();
            }
            else
            {
                SyncOptionalHooks(); // toggle durumu Play ortasında menüden değişmiş olabilir
            }

            double now = Time.unscaledTimeAsDouble;

            if (_subscribedRuntime != null)
            {
                ProcessDuePackets(now);
            }

            UpdateReplay(now);

            if (IsStatsEnabled() && _subscribedRuntime != null && now >= _statsNextRefresh)
            {
                _statsNextRefresh = now + StatsRefreshIntervalSeconds;
                RefreshStatsText(now);
            }
        }

        private void HookRuntimeIfNeeded()
        {
            if (_subscribedRuntime == null) return;
            _subscribedRuntime.Capture.PacketProduced += OnPacketForRecording; // mevcut davranış: koşulsuz hook, OnPacketForRecording içi _isRecording ile kendi kendini bastırıyor
            SyncOptionalHooks();
        }

        private void UnhookRuntime()
        {
            if (_subscribedRuntime == null) return;

            _subscribedRuntime.Capture.PacketProduced -= OnPacketForRecording;

            if (_statsSentHooked) { _subscribedRuntime.Capture.PacketProduced -= OnPacketForStatsSent; _statsSentHooked = false; }
            if (_statsRecvHooked) { _subscribedRuntime.Playback.DevPacketReceived -= OnPacketReceivedForStats; _statsRecvHooked = false; }
            if (_netSimHooked) { _subscribedRuntime.Playback.DevIncomingInterceptor = null; _netSimHooked = false; }

            _pending.Clear();
            _subscribedRuntime = null;
        }

        private void SyncOptionalHooks()
        {
            if (_subscribedRuntime == null) return;

            bool wantStats = IsStatsEnabled();
            if (wantStats && !_statsSentHooked)
            {
                _subscribedRuntime.Capture.PacketProduced += OnPacketForStatsSent;
                _statsSentHooked = true;
            }
            else if (!wantStats && _statsSentHooked)
            {
                _subscribedRuntime.Capture.PacketProduced -= OnPacketForStatsSent;
                _statsSentHooked = false;
            }

            if (wantStats && !_statsRecvHooked)
            {
                _subscribedRuntime.Playback.DevPacketReceived += OnPacketReceivedForStats;
                _statsRecvHooked = true;
            }
            else if (!wantStats && _statsRecvHooked)
            {
                _subscribedRuntime.Playback.DevPacketReceived -= OnPacketReceivedForStats;
                _statsRecvHooked = false;
            }

            bool wantNetSim = IsNetSimEnabled();
            if (wantNetSim && !_netSimHooked)
            {
                LoadNetSimPrefsIfNeeded();
                _subscribedRuntime.Playback.DevIncomingInterceptor = OnInterceptIncoming;
                _netSimHooked = true;
            }
            else if (!wantNetSim && _netSimHooked)
            {
                _subscribedRuntime.Playback.DevIncomingInterceptor = null;
                _netSimHooked = false;
                _pending.Clear(); // simülatör kapatıldı — kuyrukta bekleyen paketler kasıtlı olarak kaybolur (dev aracı, kritik değil)
            }
        }

        private void OnGUI()
        {
            DrawRecordPanel();
            DrawNetSimPanel();
            DrawStatsPanel();
        }

        #region Kayıt-Oynatma (Adım 4b, Dalga 2 — DEĞİŞMEDİ)

        private bool _isRecording;
        private double _recordStartTime;
        private readonly List<(double t, byte[] data)> _recordedPackets = new();

        private bool _isReplaying;
        private double _replayStartTime;
        private int _replayIndex;
        private List<(double t, byte[] data)> _replayPackets;

        private void UpdateReplay(double nowUnscaled)
        {
            if (!_isReplaying || _replayPackets == null || _subscribedRuntime == null) return;

            double elapsed = nowUnscaled - _replayStartTime;

            while (_replayIndex < _replayPackets.Count && _replayPackets[_replayIndex].t <= elapsed)
            {
                var (_, data) = _replayPackets[_replayIndex];
                // Kayıt/oynatma da HandleIncomingPacket üzerinden gider — ağ simülatörü AÇIKSA
                // replay akışı da onun içinden geçer (aynı canned ses akışı + kontrollü ağ bozulması
                // birlikte test edilebilir, tesadüfi bir avantaj — bkz. sınıf yorumu unified entry point).
                _subscribedRuntime.Playback.HandleIncomingPacket(RadioVoiceRuntime.DevReplayClientId, new ArraySegment<byte>(data));
                _replayIndex++;
            }

            if (_replayIndex >= _replayPackets.Count)
            {
                _isReplaying = false; // akış bitti, otomatik durdu
            }
        }

        private void OnPacketForRecording(ArraySegment<byte> packet)
        {
            if (!_isRecording) return;

            // RadioVoiceCapture'ın paylaşılan tek buffer'ı bir sonraki paketle üzerine yazılacak —
            // burada TUTMAK güvenli değil, bu yüzden KOPYALANIYOR. Bu allocation sadece kayıt AÇIKKEN
            // ve 30Hz'de olur; bu dosya ana thread'de çalışan editor-only bir araç (audio thread
            // DEĞİL) — RadioVoiceCapture'ın kendi "0 B GC alloc" kuralı bu dosyaya uygulanmaz.
            byte[] copy = new byte[packet.Count];
            Buffer.BlockCopy(packet.Array, packet.Offset, copy, 0, packet.Count);
            _recordedPackets.Add((Time.unscaledTimeAsDouble - _recordStartTime, copy));
        }

        private void DrawRecordPanel()
        {
            if (!IsRecordEnabled()) return;

            GUILayout.BeginArea(new Rect(300, 10, 340, 220), GUI.skin.box);
            GUILayout.Label("● TELSİZ PAKET KAYIT/OYNATMA (Adım 4b)");

            if (_subscribedRuntime == null)
            {
                GUILayout.Label("RadioVoiceRuntime henüz kurulmadı (sahne yükleniyor olabilir).");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Dosya: {RecordingPath}");

            if (!_isRecording)
            {
                using (new EditorGUI.DisabledScope(_isReplaying))
                {
                    if (GUILayout.Button("● Kaydı Başlat")) StartRecording();
                }
            }
            else
            {
                GUILayout.Label($"KAYITTA... {_recordedPackets.Count} paket");
                if (GUILayout.Button("■ Kaydı Durdur ve Kaydet")) StopRecordingAndSave();
            }

            GUILayout.Space(8);

            if (!_isReplaying)
            {
                bool hasFile = File.Exists(RecordingPath);
                using (new EditorGUI.DisabledScope(!hasFile || _isRecording))
                {
                    if (GUILayout.Button("▶ Oynatmayı Başlat")) StartReplay();
                }
                if (!hasFile) GUILayout.Label("(disk üzerinde kayıt yok)");
            }
            else
            {
                GUILayout.Label($"OYNATILIYOR... {_replayIndex}/{_replayPackets?.Count ?? 0}");
                if (GUILayout.Button("■ Oynatmayı Durdur")) _isReplaying = false;
            }

            GUILayout.EndArea();
        }

        private void StartRecording()
        {
            _recordedPackets.Clear();
            _recordStartTime = Time.unscaledTimeAsDouble;
            _isRecording = true;
        }

        private void StopRecordingAndSave()
        {
            _isRecording = false;

            var dir = Path.GetDirectoryName(RecordingPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir)) Directory.CreateDirectory(dir);

            using (var stream = new FileStream(RecordingPath, FileMode.Create, FileAccess.Write))
            using (var writer = new BinaryWriter(stream))
            {
                writer.Write(_recordedPackets.Count);
                foreach (var (t, data) in _recordedPackets)
                {
                    writer.Write(t);
                    writer.Write(data.Length);
                    writer.Write(data);
                }
            }

            Debug.Log($"[RadioVoiceDevTools] {_recordedPackets.Count} paket kaydedildi -> {RecordingPath}");
        }

        private void StartReplay()
        {
            var loaded = new List<(double t, byte[] data)>();

            using (var stream = new FileStream(RecordingPath, FileMode.Open, FileAccess.Read))
            using (var reader = new BinaryReader(stream))
            {
                int count = reader.ReadInt32();
                for (int i = 0; i < count; i++)
                {
                    double t = reader.ReadDouble();
                    int len = reader.ReadInt32();
                    byte[] data = reader.ReadBytes(len);
                    loaded.Add((t, data));
                }
            }

            _replayPackets = loaded;
            _replayIndex = 0;
            _replayStartTime = Time.unscaledTimeAsDouble;
            _isReplaying = true;

            Debug.Log($"[RadioVoiceDevTools] {loaded.Count} paket yüklendi, oynatma başladı <- {RecordingPath}");
        }

        #endregion

        #region Ağ Simülatörü (Adım 9)

        // 30Hz yakalama kadansına göre "bir örnekleme aralığı" — sıra-bozma enjeksiyonu bunun
        // katları kadar ekstra gecikme uygulayarak paketi 1-2 sonraki paketin ARDINA atıyor.
        private const double CaptureIntervalSeconds = 1.0 / 30.0;
        private const double ReorderExtraDelaySeconds = 2.0 * CaptureIntervalSeconds; // ~66ms

        private const string PREF_SIM_DELAY_MS = "Cargor_VoiceNetSim_DelayMs";
        private const string PREF_SIM_JITTER_MS = "Cargor_VoiceNetSim_JitterMs";
        private const string PREF_SIM_LOSS_PCT = "Cargor_VoiceNetSim_LossPct";
        private const string PREF_SIM_DUP_PCT = "Cargor_VoiceNetSim_DupPct";
        private const string PREF_SIM_REORDER_PCT = "Cargor_VoiceNetSim_ReorderPct";

        private bool _netSimPrefsLoaded;
        private float _simDelayMs;
        private float _simJitterMs;
        private float _simLossPct;
        private float _simDupPct;
        private float _simReorderPct;

        private readonly System.Random _simRng = new System.Random();

        private struct PendingPacket
        {
            public double ReleaseTime;
            public ulong SpeakerId;
            public byte[] Buffer;
            public int Length;
        }

        // Sıralı DEĞİL (jitter/sıra-bozma kasıtlı olarak varış sırasını karıştırıyor) — her Update
        // "vadesi gelmiş" (ReleaseTime <= now) her elemanı serbest bırakır, kalanlar listede kalır.
        // Editor-only stres testi aracı; kapasite küçük (3 konuşmacı x makul kuyruk derinliği),
        // O(n) tarama burada sorun değil.
        private readonly List<PendingPacket> _pending = new();

        private int _simDroppedCountTotal;
        private int _simDuplicatedCountTotal;
        private int _simReorderedCountTotal;

        private void LoadNetSimPrefsIfNeeded()
        {
            if (_netSimPrefsLoaded) return;
            _simDelayMs = EditorPrefs.GetFloat(PREF_SIM_DELAY_MS, 0f);
            _simJitterMs = EditorPrefs.GetFloat(PREF_SIM_JITTER_MS, 0f);
            _simLossPct = EditorPrefs.GetFloat(PREF_SIM_LOSS_PCT, 0f);
            _simDupPct = EditorPrefs.GetFloat(PREF_SIM_DUP_PCT, 0f);
            _simReorderPct = EditorPrefs.GetFloat(PREF_SIM_REORDER_PCT, 0f);
            _netSimPrefsLoaded = true;
        }

        /// <summary>
        /// RadioVoicePlayback.DevIncomingInterceptor olarak kaydedilir. ALIM yolunda ANA THREAD'den
        /// çağrılır (Transport'un mesaj handler'ları / Runtime'ın self-monitor loopback'i / bu
        /// dosyanın kendi replay'i — hepsi aynı HandleIncomingPacket'ten geçtiği için buraya düşer).
        /// Kayıp/dup/jitter/sıra-bozma kararını verir, paketi KOPYALAYIP (kaynak buffer paylaşımlı ve
        /// bir sonraki paketle üzerine yazılacak — bkz. OnPacketForRecording'in aynı gerekçesi)
        /// kuyruğa koyar; gerçek işleme (HandleIncomingPacketImmediate) Update()'te vadesi gelince olur.
        /// </summary>
        private void OnInterceptIncoming(ulong speakerId, ArraySegment<byte> fullPacket)
        {
            // KAYIP: paket kuyruğa hiç girmez.
            if (_simLossPct > 0f && _simRng.NextDouble() * 100.0 < _simLossPct)
            {
                _simDroppedCountTotal++;
                return;
            }

            int copies = 1;
            if (_simDupPct > 0f && _simRng.NextDouble() * 100.0 < _simDupPct)
            {
                copies = 2;
                _simDuplicatedCountTotal++;
            }

            for (int c = 0; c < copies; c++)
            {
                double extraDelay = 0.0;
                if (_simReorderPct > 0f && _simRng.NextDouble() * 100.0 < _simReorderPct)
                {
                    extraDelay = ReorderExtraDelaySeconds; // paketi 1-2 sonraki paketin ARDINA at
                    _simReorderedCountTotal++;
                }

                double jitter = _simJitterMs > 0f ? (_simRng.NextDouble() * 2.0 - 1.0) * _simJitterMs : 0.0;
                double delaySeconds = Math.Max(0.0, (_simDelayMs + jitter) / 1000.0) + extraDelay;

                // Editor-only ana thread aracı, audio thread DEĞİL — RadioVoiceCapture'ın "0B/frame"
                // kuralı burada da uygulanmıyor (bkz. OnPacketForRecording'in aynı gerekçesi).
                byte[] buf = new byte[fullPacket.Count];
                Buffer.BlockCopy(fullPacket.Array, fullPacket.Offset, buf, 0, fullPacket.Count);

                _pending.Add(new PendingPacket
                {
                    ReleaseTime = Time.unscaledTimeAsDouble + delaySeconds,
                    SpeakerId = speakerId,
                    Buffer = buf,
                    Length = fullPacket.Count,
                });
            }
        }

        private void ProcessDuePackets(double now)
        {
            for (int i = _pending.Count - 1; i >= 0; i--)
            {
                var p = _pending[i];
                if (p.ReleaseTime > now) continue;

                // Immediate'e DOĞRUDAN gidiyor — HandleIncomingPacket'e tekrar girseydi ve simülatör
                // hâlâ açıksa paket sonsuza kadar kendi kuyruğuna geri düşerdi (kendi kendini besleme).
                _subscribedRuntime.Playback.HandleIncomingPacketImmediate(p.SpeakerId, new ArraySegment<byte>(p.Buffer, 0, p.Length));
                _pending.RemoveAt(i);
            }
        }

        private void DrawNetSimPanel()
        {
            if (!IsNetSimEnabled()) return;
            LoadNetSimPrefsIfNeeded();

            GUILayout.BeginArea(new Rect(300, 240, 340, 260), GUI.skin.box);
            GUILayout.Label("● AĞ SİMÜLATÖRÜ — ALIM YOLU (Adım 9)");

            if (_subscribedRuntime == null)
            {
                GUILayout.Label("RadioVoiceRuntime henüz kurulmadı.");
                GUILayout.EndArea();
                return;
            }

            GUILayout.Label($"Gecikme: {_simDelayMs:F0} ms");
            float newDelay = GUILayout.HorizontalSlider(_simDelayMs, 0f, 500f);
            if (!Mathf.Approximately(newDelay, _simDelayMs)) SetSimParam(ref _simDelayMs, newDelay, PREF_SIM_DELAY_MS);

            GUILayout.Label($"Jitter: ± {_simJitterMs:F0} ms");
            float newJitter = GUILayout.HorizontalSlider(_simJitterMs, 0f, 300f);
            if (!Mathf.Approximately(newJitter, _simJitterMs)) SetSimParam(ref _simJitterMs, newJitter, PREF_SIM_JITTER_MS);

            GUILayout.Label($"Kayıp: %{_simLossPct:F0}");
            float newLoss = GUILayout.HorizontalSlider(_simLossPct, 0f, 50f);
            if (!Mathf.Approximately(newLoss, _simLossPct)) SetSimParam(ref _simLossPct, newLoss, PREF_SIM_LOSS_PCT);

            GUILayout.Label($"Duplike: %{_simDupPct:F0}");
            float newDup = GUILayout.HorizontalSlider(_simDupPct, 0f, 50f);
            if (!Mathf.Approximately(newDup, _simDupPct)) SetSimParam(ref _simDupPct, newDup, PREF_SIM_DUP_PCT);

            GUILayout.Label($"Sıra bozma: %{_simReorderPct:F0}");
            float newReorder = GUILayout.HorizontalSlider(_simReorderPct, 0f, 50f);
            if (!Mathf.Approximately(newReorder, _simReorderPct)) SetSimParam(ref _simReorderPct, newReorder, PREF_SIM_REORDER_PCT);

            GUILayout.Space(6);
            // Plandaki kabul kriteri harfiyen: "%10 kayıp + 80 ms jitter altında ses ANLAŞILIR kalmalı".
            if (GUILayout.Button("Kabul Testi Ön Ayarı (%10 kayıp + 80ms jitter)"))
            {
                SetSimParam(ref _simDelayMs, 0f, PREF_SIM_DELAY_MS);
                SetSimParam(ref _simJitterMs, 80f, PREF_SIM_JITTER_MS);
                SetSimParam(ref _simLossPct, 10f, PREF_SIM_LOSS_PCT);
                SetSimParam(ref _simDupPct, 0f, PREF_SIM_DUP_PCT);
                SetSimParam(ref _simReorderPct, 0f, PREF_SIM_REORDER_PCT);
            }

            GUILayout.Label($"Kuyrukta: {_pending.Count}  |  Kayıp: {_simDroppedCountTotal}  Dup: {_simDuplicatedCountTotal}  Reorder: {_simReorderedCountTotal}");
            if (GUILayout.Button("Sayaçları Sıfırla"))
            {
                _simDroppedCountTotal = 0;
                _simDuplicatedCountTotal = 0;
                _simReorderedCountTotal = 0;
            }

            GUILayout.EndArea();
        }

        private void SetSimParam(ref float field, float value, string prefKey)
        {
            field = value;
            EditorPrefs.SetFloat(prefKey, value);
        }

        #endregion

        #region İstatistik Overlay (Adım 9)

        private const double StatsRefreshIntervalSeconds = 1.0 / 6.0; // düşük frekans (~6Hz) — "her frame string" GC çöpünü önler

        private double _statsNextRefresh;
        private double _statsLastRefreshTime;
        private readonly StringBuilder _statsSb = new StringBuilder(512); // tek buffer, aracın ömrü boyunca bir kez ayrılır, her refresh'te Clear()'lanır
        private string _statsDisplayText = "(henüz veri yok)";

        // Gönderilen (Capture.PacketProduced) — pencere biriktiricileri, her refresh'te sıfırlanır. Sayaç toplama alloc'suz.
        private long _sentBytesWindow;
        private int _sentPacketsWindow;
        private int _sentMaxPacketSizeEver; // oturum boyunca gözlenen tepe — SIFIRLANMAZ (800B kapağına yaklaşma sinyali kalıcı olsun)

        // Alınan (Playback.DevPacketReceived)
        private long _recvBytesWindow;
        private int _recvPacketsWindow;
        private int _recvMaxPacketSizeEver;

        private void OnPacketForStatsSent(ArraySegment<byte> packet)
        {
            _sentBytesWindow += packet.Count;
            _sentPacketsWindow++;
            if (packet.Count > _sentMaxPacketSizeEver) _sentMaxPacketSizeEver = packet.Count;
        }

        private void OnPacketReceivedForStats(ulong speakerId, int byteCount)
        {
            _recvBytesWindow += byteCount;
            _recvPacketsWindow++;
            if (byteCount > _recvMaxPacketSizeEver) _recvMaxPacketSizeEver = byteCount;
        }

        /// <summary>
        /// ~6Hz'de bir çağrılır (Update). TÜM string inşası burada — OnGUI SADECE bu metodun ürettiği
        /// _statsDisplayText'i gösterir, kendisi hiçbir birleştirme yapmaz. Sayısal pencere
        /// biriktiricileri burada sıfırlanır (bir sonraki KB/s hesabı yeni pencereyi ölçer).
        /// </summary>
        private void RefreshStatsText(double now)
        {
            double elapsed = now - _statsLastRefreshTime;
            if (elapsed <= 0.0) elapsed = StatsRefreshIntervalSeconds;
            _statsLastRefreshTime = now;

            float sentKBps = (float)((_sentBytesWindow / 1024.0) / elapsed);
            float recvKBps = (float)((_recvBytesWindow / 1024.0) / elapsed);
            float sentAvg = _sentPacketsWindow > 0 ? (float)_sentBytesWindow / _sentPacketsWindow : 0f;
            float recvAvg = _recvPacketsWindow > 0 ? (float)_recvBytesWindow / _recvPacketsWindow : 0f;

            _sentBytesWindow = 0; _sentPacketsWindow = 0;
            _recvBytesWindow = 0; _recvPacketsWindow = 0;

            _statsSb.Clear();
            _statsSb.Append("● TELSİZ İSTATİSTİK (Adım 9)\n");
            _statsSb.Append("Gönderilen: ").Append(sentKBps.ToString("F2")).Append(" KB/s  ort ").Append(sentAvg.ToString("F0"))
                .Append("B  TEPE ").Append(_sentMaxPacketSizeEver).Append("B\n");
            _statsSb.Append("Alınan:      ").Append(recvKBps.ToString("F2")).Append(" KB/s  ort ").Append(recvAvg.ToString("F0"))
                .Append("B  TEPE ").Append(_recvMaxPacketSizeEver).Append("B\n");

            // Plan riski #1: "gerçek Steam bitrate'i ÖLÇÜLMEDİ, 800B kapağı buna bağlı" — bu satır
            // o riski somutlaştırıyor: tepe boyut kapağa yaklaşıyorsa kapak yeniden değerlendirilmeli.
            int worstPeak = Math.Max(_sentMaxPacketSizeEver, _recvMaxPacketSizeEver);
            if (worstPeak >= 700)
                _statsSb.Append("  ⚠ Tepe paket boyutu 800B kapağına yaklaşıyor (gözlenen: ").Append(worstPeak).Append("B) — kapak yeniden değerlendirilmeli!\n");

            var runtime = _subscribedRuntime;
            if (runtime != null)
            {
                _statsSb.Append("Yakalama: ").Append(runtime.Capture.State);
                if (runtime.Capture.IsMicDegraded) _statsSb.Append(" (MİKROFON YOK)");
                _statsSb.Append("\n");

                var pool = runtime.Playback.DevPool;
                int active = 0;
                for (int i = 0; i < pool.Length; i++)
                {
                    var slot = pool[i];
                    if (!slot.AssignedSpeakerId.HasValue) continue;
                    active++;

                    var ring = slot.DevRing;
                    int sr = slot.DevSampleRate;
                    double occupancyMs = (ring != null && sr > 0) ? VoiceBufferPolicy.SamplesToMilliseconds(ring.AvailableSamples, sr) : 0.0;

                    _statsSb.Append("  Slot").Append(i).Append(" [spk ").Append(slot.AssignedSpeakerId.Value).Append("]: ")
                        .Append(occupancyMs.ToString("F0")).Append("/").Append(VoiceBufferPolicy.TargetDelayMs.ToString("F0")).Append("ms")
                        .Append("  under=").Append(ring?.UnderrunCount ?? 0)
                        .Append(" over=").Append(ring?.OverrunCount ?? 0)
                        .Append(" drop(örnek)=").Append(ring?.DroppedSamples ?? 0)
                        .Append("\n");
                }
                _statsSb.Append("Aktif konuşmacı: ").Append(active).Append("/").Append(pool.Length).Append("\n");
            }

            if (IsNetSimEnabled())
            {
                _statsSb.Append("— Ağ Simülatörü — kuyruk:").Append(_pending.Count)
                    .Append(" kayıp:").Append(_simDroppedCountTotal)
                    .Append(" dup:").Append(_simDuplicatedCountTotal)
                    .Append(" reorder:").Append(_simReorderedCountTotal).Append("\n");
            }

            _statsDisplayText = _statsSb.ToString(); // TEK allocation, ~6Hz'de bir
        }

        private void DrawStatsPanel()
        {
            if (!IsStatsEnabled()) return;

            GUILayout.BeginArea(new Rect(650, 10, 380, 460), GUI.skin.box);
            GUILayout.Label(_statsDisplayText); // önbelleklenmiş metin — burada HİÇBİR birleştirme yok
            GUILayout.EndArea();
        }

        #endregion
    }
}
#endif
