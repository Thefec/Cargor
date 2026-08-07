using System;
using UnityEngine;
using Steamworks;
using NewCss.Voice.Core;

/// <summary>
/// Bir konuşmacı için AudioSource + streaming AudioClip + radyo filtre zinciri (MonoBehaviour).
///
/// KALICI ÇALAN AudioSource: Play() bir kez çağrılır ve klip hiç durdurulmaz (loop=true,
/// PCMReaderCallback veri yokken sessizlik döner). PlayOneShot zinciri KULLANILMAZ — paket başına
/// klip alloc + her 33ms sınırında click + m_RealVoiceCount:32 bütçesini paket sayısı kadar
/// tüketirdi. Sürekli isPlaying kalmak, filtre DSP zincirinin hiç kurulup yıkılmaması (konuşma
/// başında "ısınma" artefaktı olmaması) demek.
///
/// ÖRNEKLEME HIZI: clip frequency'si HER ZAMAN <see cref="RadioVoiceRuntime"/>'ın çözdüğü fiili
/// çıkış hızıyla (AudioSettings.outputSampleRate, 48000 HARDCODE DEĞİL) eşleşir — farklı olursa
/// perde kayar ("robot ses"in ikinci en yaygın sebebi).
///
/// DSP SIRASI: AddComponent çağrı sırası = filtre zinciri sırası (Unity component-order kuralı).
/// HighPass(300Hz) → LowPass(3400Hz, bonus: codec artefaktlarını maskeler) → Distortion(0.18).
/// Değerler TEK yerde toplanır (aşağıdaki const'lar), dağıtılmaz.
/// </summary>
public sealed class RadioVoiceSpeakerSlot : MonoBehaviour
{
    private const float HighPassCutoffHz = 300f;
    private const float LowPassCutoffHz = 3400f;
    private const float DistortionLevel = 0.18f;

    private const double FadeMilliseconds = 2.0;
    private const double TargetBufferSeconds = 1.0; // her slot 1.0s kapasiteli ring buffer — bir kez ayrılır

    // Klip asset'leri HENÜZ YOK (bkz. plans/telsiz-editor-isleri.md) — Resources/Voice/Clicks/* boşsa
    // null kalır, PlayClick null-guard'lı ve oturum başına en fazla bir kez uyarır (log spam yasak).
    [SerializeField] private AudioClip burstStartClip;
    [SerializeField] private AudioClip burstEndClip;

    private AudioSource _voiceSource;
    private AudioSource _clickSource;
    private AudioHighPassFilter _highPass;
    private AudioLowPassFilter _lowPass;
    private AudioDistortionFilter _distortion;

    private VoiceRingBuffer _ring;
    private AudioClip _streamClip;
    private int _sampleRate;

    private byte[] _compressedRaw;
    private PooledByteStream _compressedStream;
    private byte[] _pcmRaw;
    private PooledByteStream _pcmStream;
    private float[] _floatScratch;

    private bool _burstStartClickFired;
    private bool _warnedMissingClickClipThisSession;

    /// <summary>null = havuzda boşta. RadioVoicePlayback bunu Assign/FreeSlot üzerinden değiştirir.</summary>
    public ulong? AssignedSpeakerId { get; set; }

    public VoiceSequenceTracker Tracker { get; } = new VoiceSequenceTracker();

    /// <summary>
    /// Havuz elemanı üretir. Opsiyonel prefab override: Resources/Voice/RadioSpeakerSlot varsa ondan
    /// Instantiate edilir (sanatçı filtreleri elle sanatsal ayarlamış olabilir — bkz.
    /// plans/telsiz-editor-isleri.md), yoksa tüm ses grafiği koddan kurulur. Bu SADECE init'te
    /// (RadioVoiceRuntime.Awake) çağrılır — runtime'da hiçbir Instantiate/AddComponent YOK.
    /// </summary>
    public static RadioVoiceSpeakerSlot CreateSlot(Transform parent, int index, int sampleRate)
    {
        var prefabOverride = Resources.Load<GameObject>("Voice/RadioSpeakerSlot");

        GameObject go;
        RadioVoiceSpeakerSlot slot;
        if (prefabOverride != null)
        {
            go = Instantiate(prefabOverride, parent);
            slot = go.GetComponent<RadioVoiceSpeakerSlot>();
            if (slot == null) slot = go.AddComponent<RadioVoiceSpeakerSlot>();
        }
        else
        {
            go = new GameObject($"RadioVoiceSlot_{index}");
            go.transform.SetParent(parent, false);
            slot = go.AddComponent<RadioVoiceSpeakerSlot>();
        }

        slot.BuildAudioGraphIfNeeded();
        slot.Setup(sampleRate);
        return slot;
    }

    private void BuildAudioGraphIfNeeded()
    {
        _voiceSource = GetComponent<AudioSource>();
        if (_voiceSource == null) _voiceSource = gameObject.AddComponent<AudioSource>();

        _highPass = GetComponent<AudioHighPassFilter>();
        if (_highPass == null) _highPass = gameObject.AddComponent<AudioHighPassFilter>();

        _lowPass = GetComponent<AudioLowPassFilter>();
        if (_lowPass == null) _lowPass = gameObject.AddComponent<AudioLowPassFilter>();

        _distortion = GetComponent<AudioDistortionFilter>();
        if (_distortion == null) _distortion = gameObject.AddComponent<AudioDistortionFilter>();

        _highPass.cutoffFrequency = HighPassCutoffHz;
        _lowPass.cutoffFrequency = LowPassCutoffHz;
        _distortion.distortionLevel = DistortionLevel;

        _voiceSource.spatialBlend = 0f;   // 2D telsiz — proximity DEĞİL
        _voiceSource.dopplerLevel = 0f;
        _voiceSource.bypassReverbZones = true;
        _voiceSource.priority = 0;        // m_RealVoiceCount:32 bütçesinde sanallaştırılmasın
        _voiceSource.loop = true;
        _voiceSource.playOnAwake = false;
        _voiceSource.volume = RadioVoicePrefs.GetVolume(); // Master ile ÇARPILMAZ (bkz. RadioVoicePrefs sınıf yorumu)

        // Clicks: AYRI ve FİLTRESİZ child GameObject'te AudioSource. Aynı GameObject'te iki
        // AudioSource olsaydı, Unity'nin filtre bileşenlerini (HighPass/LowPass/Distortion)
        // GameObject'e uygulaması hangi AudioSource'a ait olduğunu belirsizleştirirdi.
        var clicksChild = transform.Find("Clicks");
        GameObject clicksGo = clicksChild != null ? clicksChild.gameObject : new GameObject("Clicks");
        if (clicksChild == null) clicksGo.transform.SetParent(transform, false);

        _clickSource = clicksGo.GetComponent<AudioSource>();
        if (_clickSource == null) _clickSource = clicksGo.AddComponent<AudioSource>();
        _clickSource.spatialBlend = 0f;
        _clickSource.playOnAwake = false;
        _clickSource.loop = false;
        _clickSource.priority = 0;

        if (burstStartClip == null) burstStartClip = Resources.Load<AudioClip>("Voice/Clicks/BurstStart");
        if (burstEndClip == null) burstEndClip = Resources.Load<AudioClip>("Voice/Clicks/BurstEnd");
    }

    private void Setup(int sampleRate)
    {
        _compressedRaw = new byte[RadioVoiceCapture.MaxCompressedFrameBytes];
        _compressedStream = new PooledByteStream(_compressedRaw);

        BuildRingAndClip(sampleRate);
    }

    /// <summary>
    /// AudioSettings.OnAudioConfigurationChanged tetiklendiğinde (kullanıcı kulaklık taktı/çıkardı,
    /// çıkış hızı değişti) RadioVoiceRuntime tarafından çağrılır. Bu nadir bir olay, runtime hot-path
    /// DEĞİL — burada allocation kabul edilebilir ("buffer'ları init'te bir kez ayır" kuralı per-frame
    /// ses işleme hattı içindir, cihaz değişimi gibi seyrek yeniden-kurulum olayları için değil).
    /// </summary>
    public void Reconfigure(int sampleRate)
    {
        if (sampleRate == _sampleRate && _streamClip != null) return;
        BuildRingAndClip(sampleRate);
    }

    private void BuildRingAndClip(int sampleRate)
    {
        if (sampleRate <= 0) sampleRate = 48000; // son çare — bkz. RadioVoiceRuntime.ResolveTargetSampleRate, normal akışta buraya düşülmemeli

        _sampleRate = sampleRate;

        int capacitySamples = (int)(sampleRate * TargetBufferSeconds);
        int targetDelay = VoiceBufferPolicy.TargetDelaySamples(sampleRate);
        int overrunCeiling = VoiceBufferPolicy.OverrunCeilingSamples(sampleRate);
        int fadeSamples = VoiceBufferPolicy.MillisecondsToSamples(FadeMilliseconds, sampleRate);
        _ring = new VoiceRingBuffer(capacitySamples, targetDelay, overrunCeiling, fadeSamples);

        _pcmRaw = new byte[sampleRate * 2]; // 1s'lik 16-bit mono PCM tavanı (DecompressVoice çıktısı için, tek çağrı bunun ~%3'ünü kullanır)
        _pcmStream = new PooledByteStream(_pcmRaw);
        _floatScratch = new float[sampleRate];

        if (_streamClip != null) Destroy(_streamClip); // native clip sızıntısını önle (nadir olay, önemsiz maliyet)

        if (_voiceSource.isPlaying) _voiceSource.Stop();
        _streamClip = AudioClip.Create($"{name}_Stream", sampleRate, 1, sampleRate, true, OnAudioRead);
        _voiceSource.clip = _streamClip;
        _voiceSource.Play(); // kalıcı çalan kaynak — konfigürasyon değişmediği sürece bir daha ASLA Stop/Play çağrılmaz
    }

    /// <summary>
    /// AUDIO THREAD. Burada lock/allocation/Debug.Log/HERHANGİ BİR Unity API YASAK — VoiceRingBuffer
    /// bu sözleşmeye göre yazıldı (Volatile.Read/Write ile senkron, kilitsiz). `data` Unity'nin
    /// sağladığı bir dizi; ona yazmak "Unity API çağrısı" sayılmaz, yasak olan yeni bir şey
    /// ayırmak/loglamak/kilitlemektir.
    /// </summary>
    private void OnAudioRead(float[] data)
    {
        _ring.Read(data, 0, data.Length);
    }

    /// <summary>Ana thread: gelen bir paketi bu slota işler (dup/reorder/gap kararı + decode + ring buffer'a yaz).</summary>
    public void SubmitPacket(VoicePacketFlags flags, ushort sequence, ArraySegment<byte> compressedPayload, double now)
    {
        var decision = Tracker.Evaluate(sequence, flags, now);
        if (decision == VoicePacketDecision.Dropped || decision == VoicePacketDecision.Duplicate) return;

        if ((flags & VoicePacketFlags.BurstStart) != 0 && !_burstStartClickFired)
        {
            PlayClick(burstStartClip);
            _burstStartClickFired = true;
        }

        DecodeAndWrite(compressedPayload);

        if ((flags & VoicePacketFlags.BurstEnd) != 0)
        {
            PlayClick(burstEndClip);
            _burstStartClickFired = false; // sıradaki burst yeniden click'lesin
        }
    }

    /// <summary>RadioVoicePlayback tarafından burst zaman aşımına uğradığında (400ms sessizlik) veya
    /// zorla serbest bırakılınca çağrılır.</summary>
    public void NotifySpeakerEnded()
    {
        if (AssignedSpeakerId.HasValue && _burstStartClickFired)
        {
            PlayClick(burstEndClip); // zaman aşımı BurstEnd bayrağının kaybolduğu durumu da kapsar
        }
        AssignedSpeakerId = null;
        _burstStartClickFired = false;
        // Ring buffer/clip'e KASITLI dokunulmuyor: AudioSource kalıcı çalıyor, veri akışı kesilince
        // kendi underrun fade-out'uyla (~2ms kosinüs) doğal olarak sessizliğe dönüyor — ekstra bir
        // "durdur" adımı audio thread'e gereksiz senkronizasyon yükü bindirirdi.
    }

    private void DecodeAndWrite(ArraySegment<byte> compressed)
    {
        if (!SteamClient.IsValid) return;
        if (compressed.Count <= 0) return;

        _compressedStream.Reset();
        _compressedStream.Write(compressed.Array, compressed.Offset, compressed.Count);
        _compressedStream.Position = 0; // DecompressVoice'un okuyacağı konuma sar

        _pcmStream.Reset();
        int pcmBytesWritten = SteamUser.DecompressVoice(_compressedStream, compressed.Count, _pcmStream);
        if (pcmBytesWritten <= 0) return;

        int sampleCount = pcmBytesWritten / 2; // tek kanal 16-bit PCM
        if (sampleCount > _floatScratch.Length) sampleCount = _floatScratch.Length; // savunma: scratch kapasitesi aşılmasın

        for (int i = 0; i < sampleCount; i++)
        {
            int b = i * 2;
            short s = (short)(_pcmRaw[b] | (_pcmRaw[b + 1] << 8)); // little-endian 16-bit mono PCM
            _floatScratch[i] = s / 32768f;
        }

        _ring.Write(_floatScratch, 0, sampleCount);
    }

    private void PlayClick(AudioClip clip)
    {
        if (clip == null)
        {
            if (!_warnedMissingClickClipThisSession)
            {
                Debug.LogWarning($"[{name}] Telsiz klik SFX asset'i atanmamış (Resources/Voice/Clicks/BurstStart|BurstEnd) — sessiz geçiliyor. (oturum başına bir kez loglanır)");
                _warnedMissingClickClipThisSession = true;
            }
            return;
        }

        _clickSource.PlayOneShot(clip, RadioVoicePrefs.GetVolume());
    }
}
