using System;
using Steamworks;
using UnityEngine;
using NewCss.Voice.Core;

/// <summary>
/// Telsiz mikrofon yakalama: sabit 30 Hz örnekleme + PTT durum makinesi + paketleme.
///
/// AĞDAN TAMAMEN BAĞIMSIZ: ürettiği paketi <see cref="PacketProduced"/> event'i ile dışarı verir,
/// kim dinlediğini (loopback / disk replay / Dalga 3'te ağ) bilmez. Bu ayrım sayesinde bu dalgada
/// (Adım 3) hiç ağ kodu yazmadan loopback ile uçtan uca doğrulama yapılabiliyor.
///
/// NEDEN Time.unscaledTimeAsDouble: Time.time/deltaTime YASAK — EscapeMenuManager.cs:404 (duraklatma)
/// ve GameStateManager.cs:109/115 (oyun sonu) Time.timeScale = 0 yapıyor. Ölçekli saatle örnekleme
/// yaparsak duraklatma/oyun-sonu sırasında konuşma donar VE Steam'in iç ses tamponu taşar (ReadVoiceData
/// çağrılmadığı için sıkıştırılmış ses birikir) — bu yüzden saat KESİNLİKLE ölçeksiz.
///
/// NEDEN SteamUser.VoiceRecord SADECE Transmitting+Tail'de true: SteamUser.VoiceRecord süreç-global
/// ve mikrofonu işletim sistemi seviyesinde fiziksel olarak açık tutar. Idle'da true bırakmak, ana
/// menüye dönüldüğünde bile mikrofonun açık kalması demek — bu yüzden durum makinesi katı: sadece
/// gerçekten konuşma potansiyeli olan iki durumda (basılıyken + 200ms tail) mikrofon açık.
///
/// NEDEN Tail (200ms) ZORUNLU: PTT bırakılınca VoiceRecord'u hemen kapatırsak son hece kesilir —
/// PTT sistemlerinin klasik hatası. Tail süresince okumaya/beslemeye devam edilir, sonra kapatılır.
/// </summary>
public sealed class RadioVoiceCapture
{
    /// <summary>
    /// Tek bir Steam voice frame'inin en fazla kaç byte sıkıştırılmış veri taşıyabileceği varsayımı.
    /// Gerçek bitrate bu dalgada ÖLÇÜLMEDİ (canlı, giriş yapılmış bir Steam oturumu gerektiriyor —
    /// plan riski #1, batchmode/headless'te ölçülemez). Bu değer muhafazakâr bir tavan: aşılırsa
    /// PooledByteStream sessizce kırpar (Truncated), o frame düşürülür ama çökme olmaz. Gerçek
    /// playtest sonrası bu sayı ölçüme göre küçültülebilir (plan: 30Hz'de beklenen ~70-270 B/paket).
    /// </summary>
    public const int MaxCompressedFrameBytes = 4096;

    private const double SampleIntervalSeconds = 1.0 / 30.0; // 30 Hz sabit — her frame DEĞİL
    private const double MaxCatchupBehindSeconds = 3.0 * SampleIntervalSeconds; // >3 aralık geç kalınırsa sürükleneni telafi etmeye çalışma, resetle
    private const double TailSeconds = 0.2; // PTT bırakılınca son heceyi kesmemek için 200ms devam

    public enum CaptureState { Disabled, Idle, Transmitting, Tail }

    /// <summary>Üretilen her paket (4B VoicePacket başlığı + sıkıştırılmış ses). Ağdan bağımsız —
    /// loopback/disk-replay/ağ (Dalga 3) hepsi bu tek çıkışa takılır.</summary>
    public event Action<ArraySegment<byte>> PacketProduced;

    public CaptureState State { get; private set; } = CaptureState.Disabled;

    // TEK ayırma (kurulumda) — runtime'da (Tick içinde) asla yeniden ayırma. Kabul kriteri:
    // PTT basılıyken frame başına 0 B GC alloc.
    private readonly byte[] _rawBuffer;
    private readonly PooledByteStream _voiceStream;

    private double _nextSampleTime;
    private double _tailDeadline;
    private double _transmittingStartTime;
    private bool _pendingBurstStart;
    private bool _everProducedDataThisBurst;
    private ushort _sequence;

    private bool _warnedTruncatedThisSession;
    private bool _warnedNoMicDataThisSession;

    public RadioVoiceCapture()
    {
        _rawBuffer = new byte[VoicePacket.HeaderSize + MaxCompressedFrameBytes];
        _voiceStream = new PooledByteStream(_rawBuffer);
    }

    /// <summary>
    /// Her frame çağrılır. <paramref name="preconditionsOk"/> false ise (Steam geçersiz veya
    /// RadioVoicePrefs.IsEnabled() kapalı) aktif kayıt anında güvenle durdurulur — bu durumda tail
    /// beklenmez, çünkü "konuşma doğal olarak bitti" değil "sistem şu an kullanılamıyor" durumudur.
    /// </summary>
    public void Tick(double nowUnscaled, bool preconditionsOk, bool pttHeld)
    {
        if (!preconditionsOk)
        {
            if (State != CaptureState.Disabled)
            {
                StopRecordingSafely();
                State = CaptureState.Disabled;
                _pendingBurstStart = false;
            }
            return;
        }

        if (State == CaptureState.Disabled)
        {
            State = CaptureState.Idle; // önkoşullar geri geldi
        }

        switch (State)
        {
            case CaptureState.Idle:
                if (pttHeld) EnterTransmitting(nowUnscaled, isNewBurst: true);
                break;

            case CaptureState.Transmitting:
                if (!pttHeld) EnterTail(nowUnscaled);
                else SampleIfDue(nowUnscaled, force: false, markBurstEnd: false);
                break;

            case CaptureState.Tail:
                if (pttHeld)
                {
                    // Tail bitmeden yeniden basıldı: AYNI burst'ün devamı — BurstStart tekrar YOK.
                    EnterTransmitting(nowUnscaled, isNewBurst: false);
                    SampleIfDue(nowUnscaled, force: false, markBurstEnd: false);
                    break;
                }

                bool tailExpired = nowUnscaled >= _tailDeadline;
                // Tail süresi dolduğunda 30Hz kapısını beklemeden ZORLA bir final örnek al: bu,
                // hem Steam'in tamponunda kalan son sesi flush eder hem de BurstEnd bayrağını taşıyan
                // paketin kesin gönderilmesini garantiler (alıcı tarafın zaman aşımına düşmeden).
                SampleIfDue(nowUnscaled, force: tailExpired, markBurstEnd: tailExpired);

                if (tailExpired)
                {
                    StopRecordingSafely();
                    State = CaptureState.Idle;
                }
                break;
        }
    }

    /// <summary>Teardown yolu: mikrofonu tail beklemeden ANINDA kapatır, Idle'a döner. Idempotent —
    /// RadioVoiceRuntime.TeardownVoice bunu çağırır.</summary>
    public void ForceStopImmediate()
    {
        StopRecordingSafely();
        State = CaptureState.Idle;
        _pendingBurstStart = false;
    }

    private void EnterTransmitting(double now, bool isNewBurst)
    {
        State = CaptureState.Transmitting;

        if (isNewBurst)
        {
            _pendingBurstStart = true;
            _everProducedDataThisBurst = false;
            _transmittingStartTime = now;
            _nextSampleTime = now; // Idle'da biriken zamanı taşımadan hemen örnekle (BurstStart gecikmesin)
            StartRecordingSafely();
        }
        // Tail'den devam ediliyorsa: VoiceRecord zaten açık, 30Hz cadence korunuyor, BurstStart tekrar YOK.
    }

    private void EnterTail(double now)
    {
        State = CaptureState.Tail;
        _tailDeadline = now + TailSeconds;
    }

    private void SampleIfDue(double now, bool force, bool markBurstEnd)
    {
        if (!force)
        {
            if (now - _nextSampleTime > MaxCatchupBehindSeconds)
            {
                _nextSampleTime = now; // büyük sapma (uzun duraklama/donma) — sürükleneni telafi etmeye çalışma
            }
            if (now < _nextSampleTime) return;

            _nextSampleTime += SampleIntervalSeconds; // sapma birikmesin: asla '= now + interval'
        }
        else
        {
            _nextSampleTime = now + SampleIntervalSeconds;
        }

        CaptureAndEmit(markBurstEnd, now);
    }

    private void CaptureAndEmit(bool markBurstEnd, double now)
    {
        if (!SteamClient.IsValid) return; // FacepunchTransport.OnDestroy() Shutdown() çağırmış olabilir, yıkım sırası garanti değil

        _voiceStream.Reset();
        _voiceStream.Position = VoicePacket.HeaderSize; // başlık alanını atla, Steam veriyi bundan sonraya yazacak

        int written = SteamUser.ReadVoiceData(_voiceStream);

        if (_voiceStream.Truncated)
        {
            written = 0; // yarım/bozuk paket göndermek yerine bu frame'i tamamen düşür
            if (!_warnedTruncatedThisSession)
            {
                Debug.LogWarning($"[RadioVoiceCapture] Sıkıştırılmış ses frame'i {MaxCompressedFrameBytes} B tavanını aştı, kırpıldı ve düşürüldü. (oturum başına bir kez loglanır — gerçek bitrate ölçümü playtest'te netleşecek)");
                _warnedTruncatedThisSession = true;
            }
        }

        if (written <= 0 && !markBurstEnd) return; // veri yok ve zorlanmış son paket de değil — gönderilecek bir şey yok

        if (written > 0) _everProducedDataThisBurst = true;

        if (!_everProducedDataThisBurst && !_warnedNoMicDataThisSession && (now - _transmittingStartTime) >= 1.5)
        {
            Debug.LogWarning("[RadioVoiceCapture] PTT 1.5s+ basılı ama Steam'den hiç ses verisi gelmedi — mikrofon yok/izinsiz olabilir. (oturum başına bir kez loglanır)");
            _warnedNoMicDataThisSession = true;
        }

        var flags = VoicePacketFlags.None;
        if (_pendingBurstStart)
        {
            flags |= VoicePacketFlags.BurstStart;
            _pendingBurstStart = false;
        }
        else
        {
            flags |= VoicePacketFlags.Continuation;
        }
        if (markBurstEnd) flags |= VoicePacketFlags.BurstEnd;

        VoicePacket.TryWriteHeader(_rawBuffer, 0, _rawBuffer.Length, flags, _sequence);
        unchecked { _sequence++; }

        PacketProduced?.Invoke(new ArraySegment<byte>(_rawBuffer, 0, VoicePacket.HeaderSize + written));
    }

    private void StartRecordingSafely()
    {
        if (!SteamClient.IsValid) return;
        SteamUser.VoiceRecord = true;
    }

    private void StopRecordingSafely()
    {
        if (!SteamClient.IsValid) return;
        SteamUser.VoiceRecord = false;
    }
}
